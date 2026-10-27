using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MP-2: what a table sent between computers is made of. The setup says how to deal the same table (the game, its
    /// mode, the players, the seed of its deal, the table's shape and the seat colours); the snapshot is the table as
    /// it is now, the same data an Undo step keeps. Plain data, written with <see cref="TableWire"/>.
    /// </summary>
    internal sealed class OnlineTableSetup
    {
        public int Kind; // 0: Empty Table, 1: a game
        public Guid TemplateId;
        public int DealPlayers;
        public int WinMode = -1;
        public int Anticlockwise = -1;
        public int FloorIndex;
        public int Seed;
        public Guid[] Players;
        public Guid MatchId;
        public int[] Colours;
        public TableSpec Table;
        public long Revision;
        public string ContentHash; // MP-4: the host content the table was dealt with
        public string RuleSetId; // MP-4: the host's house rules in play (empty: the game's default rules)
        public string PlayerGame; // MP-4: a game the host made (its save, JSON), else empty
    }

    internal sealed class OnlineTablePacket
    {
        public OnlineTableSetup Setup;
        public SessionUndoSnapshot Snapshot;
    }

    /// <summary>
    /// MP-2: the shared table (doc 10 §4, doc 30 A6). Every computer runs every change on its own copy:
    /// <list type="bullet">
    /// <item>A change made here runs at once and is sent (a command, a few hundred bytes). The host runs the joined
    /// computers' changes on its table and sends every accepted change to everyone, in one order, each computer's
    /// messages numbered so nothing is applied twice or out of order.</item>
    /// <item>What a game does outside the command door (Trap Floor's turns, a floor collapsing) and drawings are sent
    /// as the whole table (the computer of the player who did it sends it, a moment after).</item>
    /// <item>Pieces held in a hand are shown moving on the other computers ten times a second; the piece's physics runs
    /// on the computer of the player who moves it, and its resting place is sent as a command.</item>
    /// <item>Every few seconds the host sends a check of its table; a joined computer whose table differs (two players
    /// grabbed the same card at the same moment, a lost message) asks for the whole table, so tables never stay apart.</item>
    /// </list>
    /// A computer that comes back after losing its connection gets the whole table. Allocation-free per frame; messages
    /// and tables allocate when they are sent or read.
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const float PushDelaySeconds = 0.25f;
        private const float PoseSeconds = 0.1f;
        private const float CheckSeconds = 3f;
        private const float GapSeconds = 4f;
        private const float TableRequestRepeatSeconds = 6f;
        private const int MaxPoses = 32;

        private bool sharedTable;
        private bool applying;
        private OnlineTableSetup applySetup;
        private Guid myPlayer;
        private readonly List<Guid> seatPlayers = new List<Guid>(MaxMembers);
        private int tablePlayerCount;
        private int[] currentColours;
        private int lastSeed;
        private int lastFloorIndex;
        private readonly Inbox clientInbox = new Inbox();
        private long clientSeq;
        private MatchState trackedMatch;
        private long lastRevision;
        private bool matchReplaced;
        private bool dirty;
        private float dirtyAt;
        private bool awaitingTable;
        private float lastTableRequest = -100f;
        private float nextPoseAt;
        private float nextCheckAt;
        private int mismatches;
        private Action<AuthoritativeActionAcceptance> acceptanceHandler;
        private Action<object> localCommandHandler;
        private Func<PlayerId, bool> playerCheck;
        private readonly TabletopObjectId[] poseIds = new TabletopObjectId[MaxPoses];
        private readonly Vector3[] posePositions = new Vector3[MaxPoses];
        private readonly Quaternion[] poseRotations = new Quaternion[MaxPoses];
        private static readonly System.Random SeedRandom = new System.Random();

        /// <summary>True while this table is shared with the room.</summary>
        internal bool TableShared => sharedTable;

        /// <summary>True on a joined computer while the table is shared.</summary>
        internal bool IsClientTable => sharedTable && !hosting;

        /// <summary>True while another computer's table is being put on this one.</summary>
        internal bool ApplyingTable => applying;

        private ReplicatedTableAuthority Replicated => host.Replicated;

        // ---------- sharing on and off ----------

        private void StartSharing(bool asHost)
        {
            sharedTable = true;
            host.hotSeatMode = false; // each computer plays its own seat
            ReplicatedTableAuthority door = Replicated;
            door.Mode = asHost ? TableReplicationMode.Host : TableReplicationMode.Client;
            if (playerCheck == null) playerCheck = host.IsPlayerOnThisComputer;
            door.IsLocalPlayer = playerCheck;
            if (localCommandHandler == null)
            {
                localCommandHandler = OnLocalCommand;
                door.LocalCommandAccepted += localCommandHandler;
            }

            door.ForgetAppliedCommands();
            dirty = false;
            matchReplaced = false;
            awaitingTable = false;
            mismatches = 0;
            nextCheckAt = Time.unscaledTime + CheckSeconds;
        }

        private void StopSharing()
        {
            sharedTable = false;
            applying = false;
            applySetup = null;
            ReplicatedTableAuthority door = host.ReplicatedIfMade;
            if (door != null)
            {
                door.Mode = TableReplicationMode.Offline;
                door.IsLocalPlayer = null;
                door.ForgetAppliedCommands();
            }

            if (trackedMatch != null && acceptanceHandler != null) trackedMatch.AuthoritativeActionAccepted -= acceptanceHandler;
            trackedMatch = null;
            seatPlayers.Clear();
            myPlayer = Guid.Empty;
            dirty = false;
            matchReplaced = false;
            awaitingTable = false;
        }

        // ---------- starting a game (host) ----------

        /// <summary>
        /// The host's START: the room's players sit in their seat order (the game's seats; more players watch, fewer
        /// leave open seats the host plays), each in their colour, and the table is shared. False with the reason.
        /// </summary>
        internal bool PrepareHostedStart(int seats, string gameName, out string problem)
        {
            problem = null;
            if (!IsHosting)
            {
                problem = "Only the host starts the game.";
                return false;
            }

            if (!AllReady())
            {
                problem = "Waiting for everyone in the room to press READY.";
                return false;
            }

            seats = Mathf.Clamp(seats, 1, MaxMembers);
            seatPlayers.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (i < seats)
                {
                    if (member.Player == Guid.Empty) member.Player = Guid.NewGuid();
                    seatPlayers.Add(member.Player);
                }
                else
                {
                    member.Player = Guid.Empty; // watches this game
                }
            }

            while (seatPlayers.Count < seats) seatPlayers.Add(Guid.NewGuid()); // open seats: the host plays them
            tablePlayerCount = seats;
            currentColours = ColourOrder(seats);
            TabletopComposition.SetSeatColourOrder(currentColours);
            myPlayer = members.Count > 0 ? members[0].Player : Guid.Empty;
            Member me = Me();
            if (me != null) myPlayer = me.Player;
            roomGame = gameName ?? string.Empty;
            roomStarted = true;
            for (int i = 0; i < members.Count; i++) members[i].Ready = members[i].IsHost; // the next game asks again
            StartSharing(true);
            BroadcastRoster();
            SendContentToRoom(); // MP-4: everyone gets the host's content before the table dealt with it
            return true;
        }

        // Seat i plays the colour its player chose; the colours no one chose follow in their usual order.
        private int[] ColourOrder(int seats)
        {
            int[] order = new int[TabletopComposition.SeatColourCount];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = 0; i < seats && i < members.Count && i < order.Length; i++)
            {
                int wanted = members[i].Colour;
                int at = Array.IndexOf(order, wanted);
                if (at < 0 || at == i) continue;
                order[at] = order[i];
                order[i] = wanted;
            }

            return order;
        }

        // An open seat for a player who joins a game in progress (null: none, they watch).
        private Guid OpenSeat()
        {
            for (int i = 0; i < seatPlayers.Count; i++)
            {
                Guid seat = seatPlayers[i];
                bool taken = false;
                for (int m = 0; m < members.Count && !taken; m++) taken = members[m].Player == seat;
                if (!taken) return seat;
            }

            return Guid.Empty;
        }

        // ---------- the table's set-up (called by TableSessionService while a table is made) ----------

        /// <summary>
        /// False when this computer may not make a new table now: a joined computer asks the host to deal the next
        /// floor or restart the game (the host deals it for everyone); any other table is the host's choice.
        /// </summary>
        internal bool AllowTableReplacement(TabletopSessionSelection selection)
        {
            if (!sharedTable || hosting || applying) return true;
            if (selection.Kind == TabletopSessionKind.GameTemplate
                && host.activeSession != null
                && host.activeSession.Selection.Kind == TabletopSessionKind.GameTemplate
                && host.activeSession.Selection.GameTemplateId.Equals(selection.GameTemplateId))
            {
                BeginMessage(MsgDeal);
                WriteGuid(selection.GameTemplateId.Value);
                writer.Write(host.trapFloorNextFloorIndex);
                SendToHost();
                host.ShowMessage("Asked the host to deal the table for everyone.");
            }
            else
            {
                host.ShowMessage("In online play the host chooses the table.");
            }

            return false;
        }

        /// <summary>The deal's random numbers: the host picks a seed and sends it; a joined computer deals with it.</summary>
        internal void PrepareTableBuild()
        {
            int seed;
            if (applying && applySetup != null)
            {
                seed = applySetup.Seed;
                lastFloorIndex = applySetup.FloorIndex;
            }
            else if (sharedTable && hosting)
            {
                lock (SeedRandom) seed = SeedRandom.Next();
                lastFloorIndex = host.trapFloorNextFloorIndex;
            }
            else
            {
                return;
            }

            lastSeed = seed;
            if (host.authoritativeRandomValueSource == null) host.authoritativeRandomValueSource = new SystemRandomValueSource(seed);
            else host.authoritativeRandomValueSource.Reseed(seed);
        }

        /// <summary>
        /// The table's players online: the room's seats (the host's), or the players of the table being copied from
        /// the host (a joined computer). <paramref name="players"/> comes in with the game's own count.
        /// </summary>
        internal void OverrideTablePlayers(TabletopSessionSelection selection, List<PlayerId> players, ref PlayerId requester, ref MatchId matchId)
        {
            if (applying && applySetup != null && applySetup.Players != null && applySetup.Players.Length > 0)
            {
                players.Clear();
                for (int i = 0; i < applySetup.Players.Length; i++) players.Add(new PlayerId(applySetup.Players[i]));
                requester = TablePlayerHere(players);
                matchId = new MatchId(applySetup.MatchId);
                return;
            }

            if (!sharedTable || !hosting || seatPlayers.Count == 0) return;
            int count = selection.Kind == TabletopSessionKind.EmptyCustom ? seatPlayers.Count : players.Count;
            players.Clear();
            for (int i = 0; i < count; i++)
            {
                if (i >= seatPlayers.Count) seatPlayers.Add(Guid.NewGuid());
                players.Add(new PlayerId(seatPlayers[i]));
            }

            tablePlayerCount = count;
            requester = TablePlayerHere(players);
        }

        // This computer's seat in the table's players (a watcher sees the table from the first seat).
        private PlayerId TablePlayerHere(List<PlayerId> players)
        {
            Member me = Me();
            Guid mine = me != null ? me.Player : myPlayer;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Value == mine) return players[i];
            }

            return players[0];
        }

        // ---------- every frame ----------

        private void TickTable(float now)
        {
            if (!sharedTable || !host.IsInitialized || applying || host.rebuildingFromUndo || Replicated.InsideCommand) return;
            TrackMatch();

            if (!hosting)
            {
                if (clientInbox.GapSince >= 0f && now - clientInbox.GapSince > GapSeconds)
                {
                    SkipGap(clientInbox, null);
                    RequestTable("some changes did not arrive");
                }

                if (awaitingTable && now - lastTableRequest > TableRequestRepeatSeconds) RequestTable("still waiting for the table");
            }
            else
            {
                for (int i = 0; i < members.Count; i++)
                {
                    Member member = members[i];
                    if (member.IsHost || member.FromClient.GapSince < 0f || now - member.FromClient.GapSince <= GapSeconds) continue;
                    SkipGap(member.FromClient, member);
                    SendTable(member);
                }
            }

            if (session.State != MultiplayerSessionState.InRoom) return; // getting back in: nothing goes out meanwhile
            // A change outside the command door that raised no event (a game's round step): the table is sent.
            if (host.matchState != null && host.matchState.Revision != lastRevision) MarkDirty();
            if (matchReplaced)
            {
                matchReplaced = false;
                dirty = false;
                SendWholeTable();
            }
            else if (dirty && now - dirtyAt >= PushDelaySeconds)
            {
                dirty = false;
                SendWholeTable();
            }

            if (now >= nextPoseAt)
            {
                nextPoseAt = now + PoseSeconds;
                SendPoses();
            }

            if (hosting && now >= nextCheckAt)
            {
                nextCheckAt = now + CheckSeconds;
                SendChecks();
            }
        }

        // Follows the match in play: its game steps (outside the command door) make the table be sent again.
        private void TrackMatch()
        {
            MatchState current = host.matchState;
            if (ReferenceEquals(current, trackedMatch)) return;
            if (acceptanceHandler == null) acceptanceHandler = OnAcceptance;
            if (trackedMatch != null) trackedMatch.AuthoritativeActionAccepted -= acceptanceHandler;
            trackedMatch = current;
            if (current == null) return;
            current.AuthoritativeActionAccepted += acceptanceHandler;
            lastRevision = current.Revision;
            if (!applying) matchReplaced = true; // a new table here (the host's Undo, Reset, a new game or floor)
        }

        private void OnAcceptance(AuthoritativeActionAcceptance acceptance)
        {
            if (trackedMatch != null) lastRevision = trackedMatch.Revision;
            if (applying || !sharedTable) return;
            if (host.CommandIds.IsRunning(acceptance.CommandId.Value)) return; // a command: it is sent as one
            if (Replicated.Replaying) return; // the computer that made the change sends what follows from it
            if (!host.IsPlayerOnThisComputer(acceptance.ActorPlayerId)) return; // that seat's computer sends it
            MarkDirty();
        }

        /// <summary>After each command (made here or replayed): the table's revision is where the commands left it.</summary>
        internal void OnCommandEnded()
        {
            if (trackedMatch != null && ReferenceEquals(trackedMatch, host.matchState)) lastRevision = trackedMatch.Revision;
        }

        /// <summary>A change that is not a command (a drawing, a game step): the whole table is sent a moment later.</summary>
        internal void MarkDirty()
        {
            if (!sharedTable || applying || dirty) return;
            dirty = true;
            dirtyAt = Time.unscaledTime;
        }

        private void SendWholeTable()
        {
            if (host.matchState == null) return;
            if (hosting) BroadcastTable(null);
            else if (!awaitingTable) PushTableToHost();
            lastRevision = host.matchState.Revision;
        }

        // ---------- commands ----------

        // A command made here was accepted: the host sends it to everyone, a joined computer to the host.
        private void OnLocalCommand(object command)
        {
            if (!sharedTable || applying || session == null) return;
            byte[] payload;
            try
            {
                payload = TableWire.Write(command, false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] {command.GetType().Name} could not be sent ({exception.Message}); the whole table is sent instead.");
                Replicated.ForgetCommand(command);
                MarkDirty();
                return;
            }

            if (hosting)
            {
                BeginOrdered(MsgCommand);
                writer.Write(payload);
                BroadcastPrepared(null);
            }
            else
            {
                SendOrderedToHost(MsgCommand, payload);
            }
        }

        private void HostCommand(Member member, byte[] message)
        {
            if (!sharedTable) return;
            object command = TableWire.Read(message, 9, message.Length - 9, false);
            CommandContext context = Replicated.ContextOf(command);
            if (member.Player == Guid.Empty || context.RequestedByPlayerId.Value != member.Player)
            {
                SendTable(member); // a watcher, or a seat that is not theirs: their table is set right
                return;
            }

            ReplayOutcome outcome = Replicated.Replay(command);
            switch (outcome)
            {
                case ReplayOutcome.Accepted:
                    BeginOrdered(MsgCommand);
                    writer.Write(message, 9, message.Length - 9);
                    BroadcastPrepared(null); // the sender too: it knows its own and skips it
                    break;
                case ReplayOutcome.OtherTable:
                    break; // made on a table replaced since; the new one is already on its way to them
                default:
                    SendTable(member); // it did not fit the host's table: theirs is set right
                    break;
            }
        }

        private void ClientCommand(byte[] message)
        {
            if (!sharedTable || awaitingTable) return;
            object command = TableWire.Read(message, 9, message.Length - 9, false);
            ReplayOutcome outcome = Replicated.Replay(command);
            if (outcome == ReplayOutcome.Rejected || outcome == ReplayOutcome.Unknown) RequestTable("a change from the host did not fit this table");
        }

        // ---------- whole tables ----------

        private byte[] TablePayload()
        {
            if (host.activeSession == null || host.matchState == null) return null;
            try
            {
                return TableWire.Write(new OnlineTablePacket
                {
                    Setup = CaptureSetup(),
                    Snapshot = host.SessionService.CaptureUndoSnapshot(),
                }, true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Online] The table could not be packed to send: {exception}");
                return null;
            }
        }

        private OnlineTableSetup CaptureSetup()
        {
            TabletopSessionSelection selection = host.activeSession.Selection;
            bool game = selection.Kind == TabletopSessionKind.GameTemplate;
            IReadOnlyList<PlayerId> players = host.activeSession.Request.ActivePlayerIds;
            Guid[] ids = new Guid[players.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = players[i].Value;
            int deal = 0;
            if (game && host.Cartridges.TryGetTemplateSetup(selection.GameTemplateId, out IGameCartridge owner, out _)) deal = owner.DealPlayerCount;
            TableSpec table = host.CurrentTableSpec;
            return new OnlineTableSetup
            {
                Kind = game ? 1 : 0,
                TemplateId = game ? selection.GameTemplateId.Value : Guid.Empty,
                DealPlayers = deal,
                WinMode = (int)host.TrapFloorGame.WinMode,
                Anticlockwise = host.TrapFloorGame.Anticlockwise ? 1 : 0,
                FloorIndex = lastFloorIndex,
                Seed = lastSeed,
                Players = ids,
                MatchId = host.matchState.Id.Value,
                Colours = currentColours,
                Table = table != null ? table.Copy() : null,
                Revision = host.matchState.Revision,
                ContentHash = hosting ? hostContentHash : roomContentHash,
                RuleSetId = host.RulesCardPanel.ActiveRuleSetId,
                PlayerGame = host.PlayerGamesPanel.activePlayerGame != null ? JsonUtility.ToJson(host.PlayerGamesPanel.activePlayerGame) : string.Empty,
            };
        }

        private void SendTable(Member member)
        {
            byte[] payload = TablePayload();
            if (payload == null || !member.Connected || member.Connection < 0) return;
            BeginOrdered(MsgTable);
            writer.Write(payload);
            Flush();
            SendPrepared(member);
        }

        private void BroadcastTable(Member except)
        {
            if (!AnyClientConnected(except)) return;
            byte[] payload = TablePayload();
            if (payload == null) return;
            BeginOrdered(MsgTable);
            writer.Write(payload);
            BroadcastPrepared(except);
        }

        private void PushTableToHost()
        {
            byte[] payload = TablePayload();
            if (payload != null) SendOrderedToHost(MsgTable, payload);
        }

        // A joined computer's table (after a game step it made): the host takes it and sends it to the others.
        private void HostPush(Member member, byte[] message)
        {
            if (!sharedTable) return;
            OnlineTablePacket packet = TableWire.Read(message, 9, message.Length - 9, true) as OnlineTablePacket;
            if (packet == null || packet.Setup == null) return;
            if (host.matchState == null || packet.Setup.MatchId != host.matchState.Id.Value)
            {
                SendTable(member); // made on an older table
                return;
            }

            if (ApplyPacket(packet)) BroadcastTable(member);
            else SendTable(member);
        }

        private void ClientTable(byte[] message)
        {
            OnlineTablePacket packet = TableWire.Read(message, 9, message.Length - 9, true) as OnlineTablePacket;
            if (packet == null || packet.Setup == null) return;
            if (!HasContentFor(packet)) return; // MP-4: dealt once the host's content is here
            if (!sharedTable) StartSharing(false);
            ApplyPacket(packet);
        }

        // Puts another computer's table here: dealt again when it is another table, then shown as it is now.
        private bool ApplyPacket(OnlineTablePacket packet)
        {
            OnlineTableSetup setup = packet.Setup;
            if (setup == null || packet.Snapshot == null) return false;
            bool ok;
            string error = null;
            applying = true;
            applySetup = setup;
            try
            {
                tablePlayerCount = setup.Players != null ? setup.Players.Length : 0;
                lastSeed = setup.Seed;
                lastFloorIndex = setup.FloorIndex;
                if (!hosting)
                {
                    seatPlayers.Clear();
                    if (setup.Players != null) seatPlayers.AddRange(setup.Players);
                    currentColours = setup.Colours;
                }

                bool another = host.activeSession == null || host.matchState == null || host.matchState.Id.Value != setup.MatchId;
                ok = true;
                if (another)
                {
                    if (!hosting && IsFrontEndOpen())
                    {
                        host.GameSelectPanel.returnToGameSelect = false;
                        host.runtimeUi.CloseGameSelect(); // the game starts: everyone goes to the table
                    }

                    ok = BuildFromSetup(setup, out error);
                }

                if (ok) ok = host.RestoreOnlineSnapshot(packet.Snapshot, setup.Revision, out error);
            }
            catch (Exception exception)
            {
                ok = false;
                error = exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                applySetup = null;
                TrackMatch(); // the new match is followed without being sent back
                applying = false;
            }

            Replicated.ForgetAppliedCommands();
            dirty = false;
            matchReplaced = false;
            awaitingTable = false;
            mismatches = 0;
            if (host.matchState != null) lastRevision = host.matchState.Revision;
            if (!ok)
            {
                Debug.LogWarning($"[Online] The table from {(hosting ? "a player" : "the host")} could not be shown here: {error}");
                host.ShowMessage($"The shared table could not be shown here: {error}");
            }

            return ok;
        }

        private bool BuildFromSetup(OnlineTableSetup setup, out string error)
        {
            error = null;
            if (setup.Colours != null) TabletopComposition.SetSeatColourOrder(setup.Colours);
            host.UseOnlineTableSpec(setup.Table);
            TabletopSessionSelection selection;
            if (setup.Kind == 1)
            {
                GameTemplateId template = new GameTemplateId(setup.TemplateId);
                if (!host.Cartridges.TryGetTemplateSetup(template, out _, out _))
                {
                    error = "this computer does not have the host's game (or another version of it)";
                    return false;
                }

                selection = TabletopSessionSelection.FromGameTemplate(template);
                host.TrapFloorGame.PendingWinMode = setup.WinMode >= 0 ? (TrapFloorWinMode)setup.WinMode : (TrapFloorWinMode?)null;
                host.TrapFloorGame.PendingAnticlockwise = setup.Anticlockwise >= 0 ? setup.Anticlockwise == 1 : (bool?)null;
            }
            else
            {
                selection = TabletopSessionSelection.EmptyCustom;
            }

            host.trapFloorNextFloorIndex = setup.FloorIndex;
            if (!hosting) host.RulesCardPanel.ReloadHouseRules(setup.RuleSetId); // MP-4: the host's house rules
            try
            {
                bool built = host.SessionService.TryReplaceTable(selection, setup.DealPlayers);
                if (!built) error = host.gameTemplatesPanelError ?? "the table could not be set up";
                if (built && !hosting) UseHostsPlayerGame(setup.PlayerGame);
                return built;
            }
            finally
            {
                host.trapFloorNextFloorIndex = 0;
            }
        }

        // MP-4: a game the host made is shown as that game here too (its name and written rules); its pieces come from
        // the host's table, never laid out a second time.
        private void UseHostsPlayerGame(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                host.PlayerGamesPanel.activePlayerGame = JsonUtility.FromJson<PlayerGameFile>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] The host's game could not be read: {exception.Message}");
            }
        }

        // A joined computer asks the host to deal the next floor or restart the same game.
        private void HostDeal(byte[] message)
        {
            if (!sharedTable || host.activeSession == null) return;
            BinaryReader reader = Reader(message);
            reader.ReadByte();
            Guid template = ReadGuid(reader);
            int floor = reader.ReadInt32();
            TabletopSessionSelection current = host.activeSession.Selection;
            if (current.Kind != TabletopSessionKind.GameTemplate || current.GameTemplateId.Value != template) return;
            host.TrapFloorGame.PendingWinMode = null;
            host.TrapFloorGame.PendingAnticlockwise = null;
            host.trapFloorNextFloorIndex = Mathf.Max(0, floor);
            try
            {
                host.SessionService.TryReplaceTable(current);
            }
            finally
            {
                host.trapFloorNextFloorIndex = 0;
            }
        }

        private void RequestTable(string reason)
        {
            if (hosting || session == null) return;
            float now = Time.unscaledTime;
            if (awaitingTable && now - lastTableRequest < TableRequestRepeatSeconds) return;
            awaitingTable = true;
            lastTableRequest = now;
            Debug.Log($"[Online] Asking the host for the whole table ({reason}).");
            SendSmall(MsgNeedTable);
        }

        // ---------- the check ----------

        private void SendChecks()
        {
            if (host.matchState == null || !AnyClientConnected(null)) return;
            ulong hash = MatchHash(host.matchState);
            BeginOrdered(MsgCheck);
            WriteGuid(host.matchState.Id.Value);
            writer.Write(hash);
            BroadcastPrepared(null);
        }

        private void ClientCheck(byte[] message)
        {
            BinaryReader reader = Reader(message);
            reader.ReadBytes(9);
            Guid match = ReadGuid(reader);
            ulong hash = reader.ReadUInt64();
            if (awaitingTable || dirty || applying || Replicated.UnconfirmedCount > 0
                || host.matchState == null || host.matchState.Id.Value != match)
            {
                mismatches = 0;
                return;
            }

            if (MatchHash(host.matchState) == hash)
            {
                mismatches = 0;
                return;
            }

            if (++mismatches >= 2)
            {
                mismatches = 0;
                RequestTable("this table differed from the host's");
            }
        }

        // The table's state as one number, whatever order its parts were added in on each computer.
        private static ulong MatchHash(MatchState match)
        {
            return Sum(match.Cards) * 3UL
                + Sum(match.Pawns) * 5UL
                + Sum(match.Tokens) * 7UL
                + Sum(match.Dice) * 11UL
                + Sum(match.Containers) * 13UL
                + Sum(match.ContainerPlacements) * 17UL
                + Sum(match.Seats) * 19UL
                + Sum(match.PlacedConsoles) * 23UL
                + Sum(match.PlacedBoards) * 29UL
                + Sum(match.PlayAreas) * 31UL;
        }

        private static ulong Sum<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> items)
        {
            ulong sum = (ulong)items.Count;
            foreach (KeyValuePair<TKey, TValue> item in items)
            {
                byte[] bytes = TableWire.Write(item.Value, false);
                ulong hash = TableWire.Hash(bytes, 0, bytes.Length);
                hash ^= hash >> 33;
                hash *= 0xFF51AFD7ED558CCDUL;
                hash ^= hash >> 33;
                sum += hash;
            }

            return sum;
        }

        // ---------- pieces in a hand ----------

        private void SendPoses()
        {
            LocalPhysicalObjectAuthority physics = host.physicalAuthority;
            if (physics == null) return;
            int count = physics.CopyHeldPoses(poseIds, posePositions, poseRotations);
            if (count == 0) return;
            BeginMessage(MsgPoses);
            writer.Write((byte)count);
            for (int i = 0; i < count; i++)
            {
                WriteGuid(poseIds[i].Value);
                Vector3 p = posePositions[i];
                Quaternion r = poseRotations[i];
                writer.Write(p.x);
                writer.Write(p.y);
                writer.Write(p.z);
                writer.Write(r.x);
                writer.Write(r.y);
                writer.Write(r.z);
                writer.Write(r.w);
            }

            Flush();
            if (!hosting)
            {
                session.SendToHost(outStream.GetBuffer(), 0, (int)outStream.Length);
                return;
            }

            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.IsHost || !member.Connected || member.Connection < 0) continue;
                session.SendTo(member.Connection, outStream.GetBuffer(), 0, (int)outStream.Length);
            }
        }

        private void ApplyPoses(byte[] message)
        {
            LocalPhysicalObjectAuthority physics = host.physicalAuthority;
            if (physics == null || !sharedTable || applying) return;
            BinaryReader reader = Reader(message);
            reader.ReadByte();
            int count = Math.Min((int)reader.ReadByte(), MaxPoses);
            for (int i = 0; i < count; i++)
            {
                TabletopObjectId id = new TabletopObjectId(ReadGuid(reader));
                Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                physics.FollowRemote(id, position, rotation);
            }
        }

        // ---------- reading the table's messages ----------

        private void HostReadTable(Member member, byte type, byte[] data)
        {
            switch (type)
            {
                case MsgCommand:
                case MsgTable:
                    ReceiveOrdered(member.FromClient, data, member);
                    break;
                case MsgNeedTable:
                    if (sharedTable) SendTable(member);
                    break;
                case MsgPoses:
                    ApplyPoses(data);
                    for (int i = 0; i < members.Count; i++)
                    {
                        Member other = members[i];
                        if (other == member || other.IsHost || !other.Connected || other.Connection < 0) continue;
                        session.SendTo(other.Connection, data, 0, data.Length);
                    }

                    break;
                case MsgDeal:
                    HostDeal(data);
                    break;
            }
        }

        private void ClientReadTable(byte type, byte[] data)
        {
            switch (type)
            {
                case MsgCommand:
                case MsgTable:
                case MsgCheck:
                    ReceiveOrdered(clientInbox, data, null);
                    break;
                case MsgPoses:
                    ApplyPoses(data);
                    break;
            }
        }

        // Messages numbered by their sender are used in that order; one that comes early waits for the ones before it.
        private void ReceiveOrdered(Inbox box, byte[] data, Member from)
        {
            if (data.Length < 9) return;
            long seq = BitConverter.ToInt64(data, 1);
            if (seq < box.Expected) return; // already used
            if (seq > box.Expected)
            {
                box.Early[seq] = data;
                if (box.GapSince < 0f) box.GapSince = Time.unscaledTime;
                return;
            }

            UseOrdered(data, from);
            box.Expected++;
            while (box.Early.Count > 0 && box.Early.TryGetValue(box.Expected, out byte[] next))
            {
                box.Early.Remove(box.Expected);
                UseOrdered(next, from);
                box.Expected++;
            }

            if (box.Early.Count == 0) box.GapSince = -1f;
        }

        // A message that never came: carry on from the next one (the whole table follows).
        private void SkipGap(Inbox box, Member from)
        {
            box.GapSince = -1f;
            if (box.Early.Count == 0) return;
            long first = long.MaxValue;
            foreach (long seq in box.Early.Keys)
            {
                first = seq;
                break;
            }

            box.Expected = first;
            while (box.Early.Count > 0 && box.Early.TryGetValue(box.Expected, out byte[] next))
            {
                box.Early.Remove(box.Expected);
                UseOrdered(next, from);
                box.Expected++;
            }
        }

        private void UseOrdered(byte[] data, Member from)
        {
            try
            {
                switch (data[0])
                {
                    case MsgCommand:
                        if (from != null) HostCommand(from, data);
                        else ClientCommand(data);
                        break;
                    case MsgTable:
                        if (from != null) HostPush(from, data);
                        else ClientTable(data);
                        break;
                    case MsgCheck:
                        if (from == null) ClientCheck(data);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] A change could not be used here: {exception.Message}");
                if (from != null) SendTable(from);
                else RequestTable("a change could not be used");
            }
        }

        // ---------- numbered sending ----------

        private void BeginOrdered(byte type)
        {
            BeginMessage(type);
            writer.Write(0L); // its number, written per computer
        }

        private void SendOrderedToHost(byte type, byte[] payload)
        {
            BeginOrdered(type);
            writer.Write(payload);
            Flush();
            WriteSeq(outStream.GetBuffer(), ++clientSeq);
            session.SendToHost(outStream.GetBuffer(), 0, (int)outStream.Length);
        }

        private void SendPrepared(Member member)
        {
            Flush();
            if (!member.Connected || member.Connection < 0) return;
            WriteSeq(outStream.GetBuffer(), ++member.SentSeq);
            session.SendTo(member.Connection, outStream.GetBuffer(), 0, (int)outStream.Length);
        }

        private void BroadcastPrepared(Member except)
        {
            Flush();
            byte[] buffer = outStream.GetBuffer();
            int length = (int)outStream.Length;
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member == except || member.IsHost || !member.Connected || member.Connection < 0) continue;
                WriteSeq(buffer, ++member.SentSeq);
                session.SendTo(member.Connection, buffer, 0, length);
            }
        }

        private bool AnyClientConnected(Member except)
        {
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member != except && !member.IsHost && member.Connected && member.Connection >= 0) return true;
            }

            return false;
        }

        private static void WriteSeq(byte[] buffer, long seq)
        {
            for (int i = 0; i < 8; i++) buffer[1 + i] = (byte)(seq >> (8 * i));
        }
    }
}
