using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MP-2 (owner, 2026-10-26/27): online play's room and players. PLAY ONLINE on the Player profile screen connects
    /// to the room list (Lobby); a player makes a room (public: in the list; private: joined by its code) or joins one;
    /// in the Room everyone picks a colour and a seat and says READY; the host chooses the game and starts it for
    /// everyone. The host is one player's computer: it keeps the room's list of players and puts the table's changes in
    /// one order (OnlinePlayPresenter.Table.cs).
    /// A player is known by a key that stays the same while the game runs, so a computer that lost its connection and
    /// comes back gets its own seat (and the table) again; a seat whose player is away is played by the host until they
    /// are back. When the host leaves, the room closes and every computer keeps the table, offline.
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        internal const int MaxMembers = 8;
        private const int Protocol = 1;
        private const float HelloSeconds = 15f;
        private const float LobbyGraceSeconds = 60f;

        private const byte MsgHello = 1;
        private const byte MsgWelcome = 2;
        private const byte MsgRoster = 3;
        private const byte MsgReady = 4;
        private const byte MsgColour = 5;
        private const byte MsgSeat = 6;
        private const byte MsgCommand = 7;
        private const byte MsgTable = 8;
        private const byte MsgNeedTable = 9;
        private const byte MsgPoses = 10;
        private const byte MsgKick = 11;
        private const byte MsgCheck = 12;
        private const byte MsgDeal = 13;
        private const byte MsgBye = 14;

        // This computer in a room: the same while the game runs (a lost connection keeps it, a restart does not).
        private static Guid clientKey = Guid.NewGuid();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void NewKey() => clientKey = Guid.NewGuid();

        private readonly TabletopComposition host;
        private IMultiplayerSession session;
        private bool subscribed;
        private MultiplayerSessionState lastState = MultiplayerSessionState.Offline;
        private bool hosting;
        private bool welcomed;
        private string roomGame = string.Empty;
        private bool roomStarted;
        private string notice;
        private string roomNotice; // shown on the Room screen for a few seconds
        private float roomNoticeUntil;
        private readonly List<Member> members = new List<Member>(MaxMembers);
        private readonly List<Waiting> waiting = new List<Waiting>(4);
        private readonly Queue<Incoming> inbox = new Queue<Incoming>(32);
        private readonly MemoryStream outStream = new MemoryStream(1024);
        private readonly BinaryWriter writer;

        private sealed class Member
        {
            public Guid Key;
            public string Name = string.Empty;
            public int Colour;
            public bool Ready;
            public bool Connected = true;
            public bool IsHost;
            public Guid Player; // the table seat this computer plays (empty: watching)
            public int Connection = -1; // host only
            public float LeftAt = -1f; // host only
            public long SentSeq; // host only: ordered messages sent to this computer
            public readonly Inbox FromClient = new Inbox(); // host only: ordered messages from it
        }

        private struct Waiting
        {
            public int Connection;
            public float Until;
            public bool Kicked;
        }

        private struct Incoming
        {
            public int Connection;
            public byte[] Data;
        }

        private sealed class Inbox
        {
            public long Expected = 1;
            public readonly SortedDictionary<long, byte[]> Early = new SortedDictionary<long, byte[]>();
            public float GapSince = -1f;

            public void Reset()
            {
                Expected = 1;
                Early.Clear();
                GapSince = -1f;
            }
        }

        internal OnlinePlayPresenter(TabletopComposition host)
        {
            this.host = host;
            writer = new BinaryWriter(outStream, Encoding.UTF8, true);
        }

        // ---------- what the rest of the game asks ----------

        /// <summary>True while this computer is in a room (or getting back into one).</summary>
        internal bool InRoom => session != null
            && (session.State == MultiplayerSessionState.InRoom || session.State == MultiplayerSessionState.Reconnecting);

        /// <summary>True when this computer hosts its room.</summary>
        internal bool IsHosting => hosting && InRoom;

        internal int MemberCount => members.Count;

        /// <summary>The name a seat's player plays under (null: no one in the room has that seat).</summary>
        internal string NameOf(PlayerId player)
        {
            if (!sharedTable || player.IsEmpty) return null;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Player == player.Value) return members[i].Name;
            }

            return null;
        }

        /// <summary>
        /// True when the seat is played on this computer: offline every seat is; online a joined computer plays its
        /// own seat, and the host its own, the open seats and the seats of players who are away.
        /// </summary>
        internal bool IsLocalPlayer(PlayerId player)
        {
            if (!sharedTable || player.IsEmpty) return true;
            Guid id = player.Value;
            if (!hosting) return id == myPlayer;
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.Player == id) return member.IsHost || !member.Connected;
            }

            return true;
        }

        // ---------- the lobby ----------

        /// <summary>PLAY ONLINE: connects to the room list and shows it.</summary>
        internal void OpenLobby()
        {
            if (!MultiplayerSessions.IsAvailable)
            {
                host.GameSelectPanel.ShowGameSelect("Online play is not set up in this build (the networking package is missing).");
                return;
            }

            Connect();
            ShowFrontEnd(InRoom ? FrontEndPage.Room : FrontEndPage.Lobby, null);
            if (session.State == MultiplayerSessionState.Failed || session.State == MultiplayerSessionState.Offline)
            {
                session.ConnectToLobby();
            }
        }

        internal void CreateRoom(string title, bool isPrivate)
        {
            Connect();
            notice = null;
            session.CreateRoom(new OnlineRoomOptions
            {
                Title = string.IsNullOrWhiteSpace(title) ? DefaultRoomTitle() : title,
                IsPrivate = isPrivate,
                MaxPlayers = MaxMembers,
            });
        }

        internal void JoinRoom(string code)
        {
            Connect();
            notice = null;
            session.JoinRoom(code);
        }

        internal void LeaveRoom()
        {
            if (session == null || !InRoom) return;
            if (!hosting) SendSmall(MsgBye);
            session.LeaveRoom();
        }

        internal void ToggleReady()
        {
            Member me = Me();
            if (me == null || hosting) return;
            me.Ready = !me.Ready;
            BeginMessage(MsgReady);
            writer.Write(me.Ready);
            SendToHost();
            RefreshFrontEnd();
        }

        internal void CycleColour(int step)
        {
            Member me = Me();
            if (me == null) return;
            if (hosting)
            {
                me.Colour = NextFreeColour(me, step);
                BroadcastRoster();
                RefreshFrontEnd();
                return;
            }

            BeginMessage(MsgColour);
            writer.Write(step);
            SendToHost();
        }

        internal void CycleSeat(int step)
        {
            Member me = Me();
            if (me == null || sharedTable) return;
            if (hosting)
            {
                MoveSeat(me, step);
                BroadcastRoster();
                RefreshFrontEnd();
                return;
            }

            BeginMessage(MsgSeat);
            writer.Write(step);
            SendToHost();
        }

        private void Connect()
        {
            if (session == null) session = MultiplayerSessions.Current;
            if (session == null || subscribed) return;
            session.Changed += OnSessionChanged;
            session.ConnectionJoined += OnConnectionJoined;
            session.ConnectionLeft += OnConnectionLeft;
            session.MessageReceived += OnMessageReceived;
            subscribed = true;
            lastState = session.State;
        }

        /// <summary>The game's scene is closing (a profile switch reloads it): leave the room cleanly.</summary>
        internal void Dispose()
        {
            if (session == null) return;
            if (InRoom && !hosting) SendSmall(MsgBye);
            session.Disconnect(); // the scene is going (Play mode ends, or a profile switch reloads it): no reconnecting
            if (subscribed)
            {
                session.Changed -= OnSessionChanged;
                session.ConnectionJoined -= OnConnectionJoined;
                session.ConnectionLeft -= OnConnectionLeft;
                session.MessageReceived -= OnMessageReceived;
                subscribed = false;
            }

            StopSharing();
        }

        // ---------- the session ----------

        private void OnSessionChanged()
        {
            MultiplayerSessionState now = session.State;
            bool wasIn = lastState == MultiplayerSessionState.InRoom || lastState == MultiplayerSessionState.Reconnecting;
            bool isIn = now == MultiplayerSessionState.InRoom || now == MultiplayerSessionState.Reconnecting;
            MultiplayerSessionState before = lastState;
            lastState = now;
            if (now != before) roomNotice = null;

            if (now == MultiplayerSessionState.InRoom && before != MultiplayerSessionState.InRoom)
            {
                EnteredRoom(before == MultiplayerSessionState.Reconnecting);
            }
            else if (wasIn && !isIn)
            {
                LeftRoom(session.Status);
            }
            else if (now == MultiplayerSessionState.Reconnecting && before == MultiplayerSessionState.InRoom && sharedTable)
            {
                host.ShowMessage("Connection lost: getting back into the room… (your moves wait until you are back)");
            }

            RefreshFrontEnd();
        }

        private void EnteredRoom(bool again)
        {
            hosting = session.IsHost;
            welcomed = false;
            clientInbox.Reset();
            clientSeq = 0;
            if (hosting)
            {
                members.Clear();
                waiting.Clear();
                members.Add(new Member
                {
                    Key = clientKey,
                    Name = ProfileName(),
                    Colour = 0,
                    Ready = true,
                    IsHost = true,
                    Connection = session.LocalConnection,
                    Player = Guid.NewGuid(),
                });
                roomGame = string.Empty;
                roomStarted = false;
                StopSharing();
                session.UpdateRoom(roomGame, true);
                ShowFrontEnd(FrontEndPage.Room, null);
                return;
            }

            SendHello();
            if (again)
            {
                if (sharedTable) host.ShowMessage("Back in the room: getting the table…");
                return;
            }

            members.Clear();
            ShowFrontEnd(FrontEndPage.Room, null);
        }

        private void LeftRoom(string why)
        {
            bool hadTable = sharedTable;
            StopSharing();
            members.Clear();
            waiting.Clear();
            inbox.Clear();
            hosting = false;
            welcomed = false;
            roomGame = string.Empty;
            roomStarted = false;
            if (hadTable)
            {
                host.ShowMessage($"{why} The table stays on this computer, offline.");
                if (IsFrontEndOpen()) host.runtimeUi.KeepPanelsOnNextRefresh(); // LEAVE on the Room screen goes to the Lobby
                if (host.IsInitialized) host.SessionService.ShowActiveSessionUi(); // the seats' names and Undo come back
            }

            if (IsFrontEndOpen()) ShowFrontEnd(FrontEndPage.Lobby, null);
        }

        private void OnConnectionJoined(int connection)
        {
            if (!hosting) return;
            waiting.Add(new Waiting { Connection = connection, Until = Time.unscaledTime + HelloSeconds });
        }

        private void OnConnectionLeft(int connection)
        {
            if (!hosting) return;
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                if (waiting[i].Connection == connection) waiting.RemoveAt(i);
            }

            Member member = MemberAt(connection);
            if (member == null) return;
            member.Connected = false;
            member.Connection = -1;
            member.Ready = false;
            member.LeftAt = Time.unscaledTime;
            if (sharedTable) host.ShowMessage($"{member.Name} lost the connection; their seat waits for them (you play it meanwhile).");
            BroadcastRoster();
            RefreshFrontEnd();
        }

        // Messages are read in the table's Update (never in the middle of a move or a rebuild).
        private void OnMessageReceived(int connection, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            inbox.Enqueue(new Incoming { Connection = connection, Data = data });
        }

        /// <summary>Every frame, from the table's Update: messages, the room's timers and the table's sync.</summary>
        internal void Tick()
        {
            if (session == null) return;
            float now = Time.unscaledTime;
            int budget = 64;
            while (inbox.Count > 0 && budget-- > 0 && !host.rebuildingFromUndo && !Replicated.InsideCommand)
            {
                Incoming message = inbox.Dequeue();
                try
                {
                    if (hosting) HostRead(message.Connection, message.Data);
                    else ClientRead(message.Data);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Online] A message could not be read: {exception.Message}");
                    if (!hosting && sharedTable) RequestTable("a message could not be read");
                }
            }

            if (hosting) TickHostRoom(now);
            TickTable(now);
        }

        private void TickHostRoom(float now)
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                Waiting item = waiting[i];
                if (now < item.Until) continue;
                waiting.RemoveAt(i);
                session.Disconnect(item.Connection); // never said hello, or was told why it cannot stay
            }

            bool removed = false;
            for (int i = members.Count - 1; i >= 0; i--)
            {
                Member member = members[i];
                if (member.IsHost || member.Connected || sharedTable || member.LeftAt < 0f) continue;
                if (now - member.LeftAt < LobbyGraceSeconds) continue;
                members.RemoveAt(i);
                removed = true;
            }

            if (removed)
            {
                BroadcastRoster();
                RefreshFrontEnd();
            }
        }

        // ---------- the host reads ----------

        private void HostRead(int connection, byte[] data)
        {
            byte type = data[0];
            if (type == MsgHello)
            {
                ReadHello(connection, data);
                return;
            }

            Member member = MemberAt(connection);
            if (member == null) return; // no hello yet
            switch (type)
            {
                case MsgReady:
                    member.Ready = data.Length > 1 && data[1] != 0;
                    BroadcastRoster();
                    RefreshFrontEnd();
                    break;
                case MsgColour:
                    member.Colour = NextFreeColour(member, BitConverter.ToInt32(data, 1));
                    BroadcastRoster();
                    RefreshFrontEnd();
                    break;
                case MsgSeat:
                    if (!sharedTable) MoveSeat(member, BitConverter.ToInt32(data, 1));
                    BroadcastRoster();
                    RefreshFrontEnd();
                    break;
                case MsgBye:
                    if (!sharedTable) members.Remove(member);
                    else
                    {
                        member.Connected = false;
                        member.Ready = false;
                        member.LeftAt = Time.unscaledTime;
                        host.ShowMessage($"{member.Name} left the room; their seat waits for them (you play it meanwhile).");
                    }

                    member.Connection = -1;
                    BroadcastRoster();
                    RefreshFrontEnd();
                    break;
                default:
                    HostReadTable(member, type, data);
                    break;
            }
        }

        private void ReadHello(int connection, byte[] data)
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                if (waiting[i].Connection == connection && !waiting[i].Kicked) waiting.RemoveAt(i);
            }

            BinaryReader reader = Reader(data);
            reader.ReadByte();
            int protocol = reader.ReadInt32();
            string version = reader.ReadString();
            Guid key = ReadGuid(reader);
            string name = Clean(reader.ReadString(), 24);
            int colour = reader.ReadInt32();
            if (protocol != Protocol || !string.Equals(version, UnityEngine.Application.version, StringComparison.Ordinal))
            {
                Kick(connection, $"This room plays version {UnityEngine.Application.version} of the game and yours is {version}: both need the same version.");
                return;
            }

            Member member = MemberWithKey(key);
            bool back = member != null;
            if (member == null)
            {
                if (members.Count >= MaxMembers)
                {
                    Kick(connection, "The room is full.");
                    return;
                }

                member = new Member { Key = key, Colour = -1 };
                members.Add(member);
                member.Colour = NextFreeColour(member, 0, colour);
                if (sharedTable) member.Player = OpenSeat();
            }
            else if (member.Connected && member.Connection >= 0 && member.Connection != connection)
            {
                session.Disconnect(member.Connection); // the same computer's old connection
            }

            member.Name = name.Length > 0 ? name : "Player";
            member.Connection = connection;
            member.Connected = true;
            member.LeftAt = -1f;
            member.SentSeq = 0;
            member.FromClient.Reset();
            BeginMessage(MsgWelcome);
            SendTo(member);
            BroadcastRoster();
            if (sharedTable)
            {
                SendTable(member);
                host.ShowMessage(back
                    ? $"{member.Name} is back."
                    : member.Player == Guid.Empty ? $"{member.Name} joined and is watching." : $"{member.Name} joined and plays an open seat.");
            }

            RefreshFrontEnd();
        }

        private void Kick(int connection, string reason)
        {
            BeginMessage(MsgKick);
            writer.Write(reason);
            Flush();
            session.SendTo(connection, outStream.GetBuffer(), 0, (int)outStream.Length);
            waiting.Add(new Waiting { Connection = connection, Until = Time.unscaledTime + 2f, Kicked = true });
        }

        // ---------- a joined computer reads ----------

        private void ClientRead(byte[] data)
        {
            byte type = data[0];
            switch (type)
            {
                case MsgWelcome:
                    welcomed = true; // the numbering of this connection started with our hello (EnteredRoom)
                    break;
                case MsgRoster:
                    ReadRoster(data);
                    break;
                case MsgKick:
                    {
                        BinaryReader reader = Reader(data);
                        reader.ReadByte();
                        notice = reader.ReadString();
                        host.ShowMessage(notice);
                        session.LeaveRoom();
                        break;
                    }

                default:
                    ClientReadTable(type, data);
                    break;
            }
        }

        private void ReadRoster(byte[] data)
        {
            BinaryReader reader = Reader(data);
            reader.ReadByte();
            roomGame = reader.ReadString();
            roomStarted = reader.ReadBoolean();
            int count = Math.Min((int)reader.ReadByte(), MaxMembers);
            while (members.Count > count) members.RemoveAt(members.Count - 1);
            for (int i = 0; i < count; i++)
            {
                if (i >= members.Count) members.Add(new Member());
                Member member = members[i];
                member.Key = ReadGuid(reader);
                member.Name = reader.ReadString();
                member.Colour = reader.ReadInt32();
                member.Ready = reader.ReadBoolean();
                member.Connected = reader.ReadBoolean();
                member.IsHost = reader.ReadBoolean();
                member.Player = ReadGuid(reader);
            }

            Member me = Me();
            myPlayer = me != null ? me.Player : Guid.Empty;
            RefreshFrontEnd();
        }

        // ---------- sending ----------

        private void SendHello()
        {
            BeginMessage(MsgHello);
            writer.Write(Protocol);
            writer.Write(UnityEngine.Application.version);
            WriteGuid(clientKey);
            writer.Write(ProfileName());
            Member me = Me();
            writer.Write(me != null ? me.Colour : 0);
            SendToHost();
        }

        private void BroadcastRoster()
        {
            if (!hosting) return;
            BeginMessage(MsgRoster);
            writer.Write(roomGame ?? string.Empty);
            writer.Write(roomStarted);
            writer.Write((byte)members.Count);
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                WriteGuid(member.Key);
                writer.Write(member.Name ?? string.Empty);
                writer.Write(member.Colour);
                writer.Write(member.Ready);
                writer.Write(member.Connected);
                writer.Write(member.IsHost);
                WriteGuid(member.Player);
            }

            Flush();
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.IsHost || !member.Connected || member.Connection < 0) continue;
                session.SendTo(member.Connection, outStream.GetBuffer(), 0, (int)outStream.Length);
            }

            session.UpdateRoom(roomGame, members.Count < MaxMembers);
        }

        private void SendSmall(byte type)
        {
            BeginMessage(type);
            SendToHost();
        }

        private void BeginMessage(byte type)
        {
            outStream.SetLength(0);
            outStream.Position = 0;
            writer.Write(type);
        }

        private void Flush() => writer.Flush();

        private void SendToHost()
        {
            Flush();
            session?.SendToHost(outStream.GetBuffer(), 0, (int)outStream.Length);
        }

        private void SendTo(Member member)
        {
            Flush();
            if (member.Connected && member.Connection >= 0) session.SendTo(member.Connection, outStream.GetBuffer(), 0, (int)outStream.Length);
        }

        private void WriteGuid(Guid value)
        {
            Span<byte> bytes = stackalloc byte[16];
            value.TryWriteBytes(bytes);
            writer.Write(bytes);
        }

        private static Guid ReadGuid(BinaryReader reader)
        {
            Span<byte> bytes = stackalloc byte[16];
            if (reader.Read(bytes) != 16) throw new EndOfStreamException();
            return new Guid(bytes);
        }

        private static BinaryReader Reader(byte[] data) => new BinaryReader(new MemoryStream(data, false), Encoding.UTF8);

        // ---------- members ----------

        private Member Me() => MemberWithKey(clientKey);

        private Member MemberWithKey(Guid key)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Key == key) return members[i];
            }

            return null;
        }

        private Member MemberAt(int connection)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Connection == connection && members[i].Connected) return members[i];
            }

            return null;
        }

        // The next colour no one else has (the colours are the table's seat colours). Step 0: the wanted colour, or
        // the first free one after it.
        private int NextFreeColour(Member member, int step, int wanted = -1)
        {
            int count = TabletopComposition.SeatColourCount;
            int start = wanted >= 0 ? wanted : Math.Max(0, member.Colour);
            if (step == 0)
            {
                for (int tries = 0; tries < count; tries++)
                {
                    int candidate = (start + tries) % count;
                    if (!ColourTaken(candidate, member)) return candidate;
                }
            }
            else
            {
                int direction = step < 0 ? -1 : 1;
                for (int tries = 1; tries <= count; tries++)
                {
                    int candidate = (((start + direction * tries) % count) + count) % count;
                    if (!ColourTaken(candidate, member)) return candidate;
                }
            }

            return Mathf.Clamp(member.Colour, 0, count - 1);
        }

        private bool ColourTaken(int colour, Member except)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != except && members[i].Colour == colour) return true;
            }

            return false;
        }

        private void MoveSeat(Member member, int step)
        {
            int index = members.IndexOf(member);
            int target = Mathf.Clamp(index + (step < 0 ? -1 : 1), 0, members.Count - 1);
            if (index < 0 || target == index) return;
            members[index] = members[target];
            members[target] = member;
        }

        private bool AllReady()
        {
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (!member.IsHost && member.Connected && !member.Ready) return false;
            }

            return true;
        }

        private string ProfileName()
        {
            try
            {
                string name = host.EnsureProfile().Active.displayName;
                return string.IsNullOrWhiteSpace(name) ? "Player" : Clean(name, 24);
            }
            catch (Exception)
            {
                return "Player";
            }
        }

        private string DefaultRoomTitle() => ProfileName() + "'s table";

        private static string Clean(string text, int length)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string trimmed = text.Trim();
            return trimmed.Length > length ? trimmed.Substring(0, length) : trimmed;
        }

        // ---------- the front end ----------

        private bool IsFrontEndOpen() => host.runtimeUi != null && host.runtimeUi.IsGameSelectOpen;

        /// <summary>Shows a line on the Room screen (and goes back to it).</summary>
        internal void ShowRoomNotice(string text)
        {
            if (!InRoom) return;
            roomNotice = text;
            roomNoticeUntil = Time.unscaledTime + 10f;
            ShowFrontEnd(FrontEndPage.Room, null);
        }

        private void ShowFrontEnd(FrontEndPage page, string status)
        {
            if (host.runtimeUi == null) return;
            if (status != null) notice = status;
            GameSelectPresenter.pendingFrontEndPage = page;
            host.GameSelectPanel.ShowGameSelect(string.Empty);
        }

        // Redraws the open front end (the room list, the players, ready…); not while an editor is open over it.
        private void RefreshFrontEnd()
        {
            if (!IsFrontEndOpen() || host.GameSelectPanel.returnToGameSelect) return;
            host.GameSelectPanel.ShowGameSelect(string.Empty);
        }

        /// <summary>What the Lobby and Room screens show.</summary>
        internal OnlineScreenModel BuildScreenModel()
        {
            OnlineScreenModel model = new OnlineScreenModel
            {
                Available = MultiplayerSessions.IsAvailable,
                DefaultRoomTitle = DefaultRoomTitle(),
            };
            if (session == null)
            {
                model.Status = model.Available ? "Not connected." : "Online play is not set up in this build.";
                return model;
            }

            MultiplayerSessionState state = session.State;
            model.Status = !string.IsNullOrEmpty(notice) && state != MultiplayerSessionState.InRoom ? notice + "  " + session.Status : session.Status;
            if (!string.IsNullOrEmpty(roomNotice) && InRoom && Time.unscaledTime < roomNoticeUntil) model.Status = roomNotice;
            model.Connected = state == MultiplayerSessionState.InLobby;
            model.Busy = state == MultiplayerSessionState.Connecting || state == MultiplayerSessionState.JoiningRoom
                || state == MultiplayerSessionState.Reconnecting;
            model.InRoom = InRoom;
            model.IsHost = IsHosting;
            if (model.InRoom && !hosting && !welcomed) model.Busy = true; // the host has not answered yet
            model.RoomTitle = session.RoomTitle;
            model.RoomCode = session.RoomCode;
            model.RoomPrivate = session.RoomIsPrivate;

            IReadOnlyList<OnlineRoomListing> rooms = session.Rooms;
            List<OnlineRoomRow> roomRows = new List<OnlineRoomRow>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                OnlineRoomListing room = rooms[i];
                string game = string.IsNullOrEmpty(room.Game) ? "choosing a game" : room.Game;
                roomRows.Add(new OnlineRoomRow
                {
                    Code = room.Code,
                    Title = room.Title,
                    Detail = $"{game}  ·  {room.Players}/{room.MaxPlayers}",
                    CanJoin = room.IsOpen,
                });
            }

            model.Rooms = roomRows;
            if (!model.InRoom) return model;

            Member me = Me();
            int seats = sharedTable ? tablePlayerCount : members.Count;
            List<OnlineMemberRow> rows = new List<OnlineMemberRow>(members.Count);
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                bool watching = sharedTable && member.Player == Guid.Empty;
                string tags = member.IsHost ? "HOST" : !member.Connected ? "RECONNECTING…" : member.Ready ? "READY" : "NOT READY";
                if (watching) tags = member.Connected ? "WATCHING" : "AWAY";
                if (member == me) tags = "YOU  ·  " + tags;
                rows.Add(new OnlineMemberRow
                {
                    Badge = watching ? "—" : "P" + (SeatNumberOf(member, i) + 1),
                    Name = member.Name,
                    Colour = TabletopComposition.ColourByIndex(member.Colour),
                    Tags = tags,
                    IsYou = member == me,
                });
            }

            model.Members = rows;
            model.GameText = roomStarted && !string.IsNullOrEmpty(roomGame)
                ? "PLAYING: " + roomGame.ToUpperInvariant()
                : model.IsHost ? "Choose the game with CHOOSE GAME; START is on its last step." : "The host chooses the game and starts it.";
            model.Started = roomStarted;
            model.Ready = me != null && me.Ready;
            model.CanReady = me != null && !hosting && !sharedTable;
            model.ColourName = me != null ? TabletopComposition.ColourNameByIndex(me.Colour) : string.Empty;
            model.Colour = me != null ? TabletopComposition.ColourByIndex(me.Colour) : Color.grey;
            int mine = me != null ? members.IndexOf(me) : -1;
            model.SeatText = mine >= 0 ? $"Seat {mine + 1} of {members.Count}" : string.Empty;
            model.CanChangeSeat = !sharedTable && members.Count > 1;
            model.AllReady = hosting ? AllReady() : true;
            model.Seats = seats;
            return model;
        }

        private int SeatNumberOf(Member member, int index)
        {
            if (!sharedTable || member.Player == Guid.Empty) return index;
            for (int i = 0; i < seatPlayers.Count; i++)
            {
                if (seatPlayers[i] == member.Player) return i;
            }

            return index;
        }

        /// <summary>The Players and Review steps online: the room's players in seat order, with their colours.</summary>
        internal void FillSeats(string[] names, Color[] colours, string[] colourNames)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (i < members.Count)
                {
                    names[i] = members[i].Name;
                    colours[i] = TabletopComposition.ColourByIndex(members[i].Colour);
                    colourNames[i] = TabletopComposition.ColourNameByIndex(members[i].Colour);
                }
                else
                {
                    names[i] = $"Open seat {i + 1} (host)";
                }
            }
        }
    }
}
