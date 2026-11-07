using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.GameTemplates.Privacy;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// PRIVACY-P4 (s148, doc 10 §57): whether this computer plays the private development session instead of normal
    /// online play. OFF unless switched on: in the Editor from Console Cards › Debug › Privacy, in a development build
    /// by a file named <see cref="FlagFileName"/> next to the game. A release build has none of this code.
    /// </summary>
    public static class PrivateDevSession
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const string FlagFileName = "private-dev-session.txt";
        private static bool editorOn;
#if !UNITY_EDITOR
        private static int buildFlag = -1;
#endif

        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                return editorOn;
#else
                if (buildFlag < 0)
                {
                    try
                    {
                        buildFlag = File.Exists(Path.Combine(UnityEngine.Application.dataPath, "..", FlagFileName)) ? 1 : 0;
                    }
                    catch (Exception)
                    {
                        buildFlag = 0;
                    }

                    if (buildFlag == 1) Debug.Log("[PrivateSession] Private development session is ON for this run (flag file found).");
                }

                return buildFlag == 1;
#endif
            }
        }

        public static void SetEditorEnabled(bool on) => editorOn = on;
#else
        public static bool Enabled => false;
#endif
    }

    internal sealed partial class OnlinePlayPresenter
    {
        // PRIVACY-P4: the private development session's protocol. A normal room and a private one refuse each other's
        // hello (the hello's protocol must match), so a private computer never joins a normal room and back.
        private const int PrivateDevProtocol = 9001;

        private const byte MsgPrivatePackage = 40;    // host → guest: the guest's own recipient package
        private const byte MsgPrivateRequest = 41;    // guest → host: one action intent (PrivateRequestCodec bytes)
        private const byte MsgPrivateUpdate = 42;     // host → guest: one recipient-specific event and digest
        private const byte MsgPrivateVerdict = 43;    // host → guest: what became of the guest's request
        private const byte MsgPrivateNeedPackage = 44; // guest → host: my table no longer matches; send my package again
        private const byte MsgPrivateInventory = 45;  // guest → host: what my scene holds (dev evidence, checked by the host)

        private static bool IsPrivateMessage(byte type) => type >= MsgPrivatePackage && type <= MsgPrivateInventory;

        // Normal-play table messages: never used, and refused, in a private session.
        private static bool IsLegacyTableMessage(byte type) =>
            type == MsgCommand || type == MsgTable || type == MsgNeedTable || type == MsgPoses || type == MsgCheck
            || type == MsgDeal || type == MsgUndo || type == MsgEndTurn || type == MsgLive;

        private int HelloProtocol => PrivateDevSession.Enabled ? PrivateDevProtocol : Protocol;

        private bool privateHosting; // this computer hosts a private development session
        private bool privateGuest;   // this computer is a guest of one (its table is only its own package)

        /// <summary>True while this computer takes part in the private development session.</summary>
        internal bool InPrivateSession => privateHosting || privateGuest;

        internal bool IsPrivateGuest => privateGuest;
        internal bool IsPrivateHost => privateHosting;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PrivateTableHost privateHost;
        private MatchState privateHostMatch;
        private bool privateHostPending;
        private int privateSeed;
        private bool privateSeedKnown;
        private int legacyRefused;
        private int packagesSent;
        private int updatesSent;
        private int packagesReceived;
        private int updatesReceived;
        private int digestMismatches;
        private Guid lastRequestId;
        private byte[] lastRequestBytes;
        private TabletopObjectId lastPlacedReference;
        // PRIVACY-P5: a reference this computer knew a card by before it turned into a back here (a stale public ref).
        private TabletopObjectId lastWithdrawnReference;
        // PRIVACY-P5 (s149): how the scene follows an ordinary update. True: only the moved card's view and its two
        // containers (the default). False: the whole scene is built again from this computer's own table (the s148
        // behaviour, kept for measuring). A package (join, reconnect, resync, a mismatch) always builds the whole scene.
        internal bool IncrementalScene = true;
        private int fullBuilds;
        private int incrementalUpdates;
        private PendingSceneMeasure pendingMeasure;
        private readonly float[] frameTimes = new float[60];
        private int frameTimeCount;
        private int frameTimeNext;
        private readonly List<string> privateLog = new List<string>(64);
        private PrivateSessionPanel privatePanel;

        internal IReadOnlyList<string> PrivateLog => privateLog;

        private void PrivateNote(string line)
        {
            Debug.Log("[PrivateSession] " + line);
            privateLog.Add(line);
            if (privateLog.Count > 60) privateLog.RemoveAt(0);
            try
            {
                string folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, (privateHosting ? "PrivateSession-host" : "PrivateSession-guest") + ".txt"),
                    DateTime.Now.ToString("HH:mm:ss") + " " + line + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }

        private void EnsurePrivatePanel()
        {
            if (privatePanel != null || !PrivateDevSession.Enabled) return;
            privatePanel = host.gameObject.AddComponent<PrivateSessionPanel>();
            privatePanel.Online = this;
        }

        // ---------- host: starting ----------

        // From the host's START (PrepareHostedStart): the seats are set as in normal play, but nothing is shared: the
        // host deals its own table, then each guest is sent its own package (TickPrivate).
        private bool StartPrivateHosting()
        {
            privateHosting = true;
            privateHostPending = true;
            privateHost = null;
            privateHostMatch = null;
            EnsurePrivatePanel();
            PrivateNote("Private development session: the host deals its own table; no seed, table or command will be sent.");
            return true;
        }

        // The deal's seed stays on the host (it is noted only so the sent bytes can be checked for it).
        private bool PreparePrivateHostSeed(out int seed)
        {
            seed = 0;
            if (!privateHosting || !hosting) return false;
            lock (SeedRandom) seed = SeedRandom.Next();
            privateSeed = seed;
            privateSeedKnown = true;
            return true;
        }

        private void TickPrivate()
        {
            TickPrivateGuestMeasure();
            if (!privateHosting || !hosting || session == null) return;
            if (!host.IsInitialized || host.matchState == null || host.trapFloorTemplate == null || host.trapFloorTurnState == null) return;
            if (!privateHostPending && ReferenceEquals(privateHostMatch, host.matchState)) return;
            // A new table on the host (the first deal, a reset, an Undo step): each guest gets its own package again.
            bool first = privateHostPending;
            privateHostPending = false;
            privateHostMatch = host.matchState;
            privateHost = new PrivateTableHost(host.matchState, new RoomSeats(this), host.trapFloorTemplate.CardLabels, RecipientSnapshotProjection.NewSessionKey())
            {
                Layout = host.BuildPublicLayout(),
            };
            PrivateNote(first ? "Host table dealt: sending each guest its own package." : "The host's table was replaced: sending each guest its own package again.");
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.IsHost || !member.Connected || member.Connection < 0) continue;
                SendPrivatePackage(member);
            }
        }

        // The recipient a member is: its seat's player when seated at this table, else a spectator. From the room's
        // own records (the connection the message came on), never from the message.
        private PlayerId RecipientOf(Member member)
        {
            if (member == null || member.Player == Guid.Empty) return RecipientSnapshotProjection.Spectator;
            PlayerId player = new PlayerId(member.Player);
            return new RoomSeats(this).IsSeated(player) ? player : RecipientSnapshotProjection.Spectator;
        }

        private void SendPrivatePackage(Member member)
        {
            if (privateHost == null || member == null || !member.Connected || member.Connection < 0) return;
            PlayerId recipient = RecipientOf(member);
            byte[] package = privateHost.Join(recipient);
            CheckSentBytes(package, recipient, $"package to {member.Name}");
            SendPrivateBytes(member, MsgPrivatePackage, package);
            packagesSent++;
        }

        private void SendPrivateBytes(Member member, byte type, byte[] payload)
        {
            BeginMessage(type);
            writer.Write(payload);
            SendTo(member);
        }

        // The host checks the exact bytes it sends (doc 11 §20): no real id of a card hidden from this recipient, no
        // definition only hidden cards have, no name only hidden cards have, not the deal's seed. Counts only: nothing
        // secret is written. The guest logs the SHA-256 of what it received, so both ends can be matched.
        // PRIVACY-P5: an update names the card by the reference the recipient knew it by BEFORE the move. When a public
        // card turns hidden (a Console card back to a hand, a card discarded face down), that is its real id, which this
        // recipient already held while it was public: it is counted apart, so the check still shows any other.
        private void CheckSentBytes(byte[] bytes, PlayerId recipient, string what, TabletopObjectId knewBefore = default)
        {
            MatchState full = host.matchState;
            RecipientSnapshotProjection.Classify(full, recipient, out HashSet<TabletopObjectId> hidden, out _);
            int ids = 0;
            bool previousPublic = false;
            foreach (TabletopObjectId id in hidden)
            {
                if (!TabletopComposition.Contains(bytes, id.Value.ToByteArray())) continue;
                if (!knewBefore.IsEmpty && id == knewBefore) previousPublic = true;
                else ids++;
            }
            HashSet<ObjectDefinitionId> definitions = TabletopComposition.HiddenOnlyDefinitions(full, hidden);
            int defs = 0;
            foreach (ObjectDefinitionId definition in definitions) if (TabletopComposition.Contains(bytes, definition.Value.ToByteArray())) defs++;
            HashSet<string> names = TabletopComposition.HiddenOnlyNames(full, hidden, host.trapFloorTemplate.CardLabels);
            int named = 0;
            foreach (string name in names) if (TabletopComposition.Contains(bytes, Encoding.UTF8.GetBytes(name))) named++;
            bool seed = privateSeedKnown && TabletopComposition.Contains(bytes, BitConverter.GetBytes(privateSeed));
            PrivateNote($"Sent {what}: {bytes.Length} bytes, sha256 {Sha(bytes)}; hidden real ids {ids}/{hidden.Count}"
                + (previousPublic ? " (plus the card's previous reference: its real id, which this recipient could identify before the move)" : string.Empty)
                + $", hidden-only definitions {defs}/{definitions.Count}, hidden-only names {named}/{names.Count}, deal seed {(seed ? "FOUND" : "absent")}.");
        }

        private static string Sha(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(bytes)).Substring(0, 16);
        }

        // ---------- host: reading ----------

        private void HostReadPrivate(Member member, byte type, byte[] data)
        {
            if (!privateHosting || privateHost == null)
            {
                PrivateNote($"Refused private message {type} from {member.Name}: no private session here.");
                return;
            }

            switch (type)
            {
                case MsgPrivateRequest:
                    HostPrivateRequest(member, data);
                    break;
                case MsgPrivateNeedPackage:
                    PrivateNote($"{member.Name} asked for its package again (its table did not match).");
                    SendPrivatePackage(member);
                    break;
                case MsgPrivateInventory:
                    HostCheckInventory(member, data);
                    break;
                default:
                    PrivateNote($"Refused private message {type} from {member.Name}: hosts do not take it.");
                    break;
            }
        }

        private void HostPrivateRequest(Member member, byte[] data)
        {
            // The sender's seat comes from the connection's member, never from the request (which names none).
            PlayerId sender = member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            Guid requestId = Guid.Empty;
            PrivateRequestVerdict verdict;
            Dictionary<PlayerId, byte[]> updates = null;
            ContainerId from = ContainerId.Empty, to = ContainerId.Empty;
            string what;
            string before = HostTableFingerprint();
            // Who could identify the card BEFORE the move (for the sent-bytes check); filled after an accepted move.
            Dictionary<PlayerId, bool> knownBefore = new Dictionary<PlayerId, bool>();
            TabletopObjectId movedCard = TabletopObjectId.Empty;
            Dictionary<TabletopObjectId, HashSet<PlayerId>> publicBefore = PublicCardsByRecipient();
            if (!PrivateRequestCodec.TryRead(data, 1, out byte kind, out PrivateDrawRequest draw, out PrivatePlaceRequest place, out PrivateCardRequest card))
            {
                verdict = PrivateRequestVerdict.UnknownReference;
                what = "a malformed request";
            }
            else if (kind == PrivateRequestCodec.KindDraw)
            {
                requestId = draw.RequestId;
                what = "Draw";
                RoomSeats seats = new RoomSeats(this);
                from = draw.Deck;
                to = seats.IsSeated(sender) ? seats.HandOf(sender) : ContainerId.Empty;
                verdict = privateHost.Draw(sender, draw, out updates);
            }
            else if (kind == PrivateRequestCodec.KindPlace)
            {
                requestId = place.RequestId;
                what = "Place face up";
                verdict = privateHost.Place(sender, place, out updates);
            }
            else if (kind == PrivateRequestCodec.KindReturn)
            {
                requestId = card.RequestId;
                what = "Return to hand";
                verdict = privateHost.Return(sender, card, out updates);
            }
            else
            {
                requestId = card.RequestId;
                what = "Discard";
                verdict = privateHost.Discard(sender, card, out updates);
            }

            if (verdict == PrivateRequestVerdict.Accepted)
            {
                from = privateHost.LastFrom;
                to = privateHost.LastTo;
                movedCard = privateHost.LastCard;
                foreach (PlayerId r in privateHost.Recipients)
                    knownBefore[r] = publicBefore.TryGetValue(movedCard, out HashSet<PlayerId> who) && who.Contains(r);
            }

            int sent = 0;
            if (verdict == PrivateRequestVerdict.Accepted && updates != null)
            {
                // The host's own scene follows its table.
                host.ViewSync.SyncContainer(from);
                host.ViewSync.SyncContainer(to);
                host.RefreshCardContentVisibility();
                host.RefreshAcceptedCardFaces();
                for (int i = 0; i < members.Count; i++)
                {
                    Member other = members[i];
                    if (other.IsHost || !other.Connected || other.Connection < 0) continue;
                    PlayerId recipient = RecipientOf(other);
                    if (!updates.TryGetValue(recipient, out byte[] update)) continue;
                    // The previous reference counts apart only when it was this card's real id AND the recipient could
                    // identify the card before the move (it was public then); a handle is never a real id.
                    TabletopObjectId knewBefore = TabletopObjectId.Empty;
                    if (knownBefore.TryGetValue(recipient, out bool knew) && knew) knewBefore = movedCard;
                    CheckSentBytes(update, recipient, $"{what} update to {other.Name}", knewBefore);
                    SendPrivateBytes(other, MsgPrivateUpdate, update);
                    updatesSent++;
                    sent++;
                }
            }
            else
            {
                string after = HostTableFingerprint();
                PrivateNote($"Refused {what} from {member.Name}: {verdict} (assist {PlayAssist.Level}); host table {(before == after ? "unchanged" : "CHANGED")}.");
            }

            if (verdict == PrivateRequestVerdict.Accepted) PrivateNote($"Accepted {what} from {member.Name} (assist {PlayAssist.Level}); one recipient-specific update sent to each of the {sent} guest computer(s) here.");
            BeginMessage(MsgPrivateVerdict);
            WriteGuid(requestId);
            writer.Write((byte)verdict);
            SendTo(member);
        }

        // Which recipients may identify each card right now (host side, for the sent-bytes check of the next update).
        private Dictionary<TabletopObjectId, HashSet<PlayerId>> PublicCardsByRecipient()
        {
            Dictionary<TabletopObjectId, HashSet<PlayerId>> known = new Dictionary<TabletopObjectId, HashSet<PlayerId>>();
            if (privateHost == null) return known;
            foreach (PlayerId r in privateHost.Recipients)
                foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in host.matchState.Cards)
                    if (RecipientSnapshotProjection.MayKnowIdentity(host.matchState, pair.Value, r))
                    {
                        if (!known.TryGetValue(pair.Key, out HashSet<PlayerId> set)) known[pair.Key] = set = new HashSet<PlayerId>();
                        set.Add(r);
                    }
            return known;
        }

        // Where every card is and how it lies (for the "a refused request changed nothing" check). Host side.
        private string HostTableFingerprint()
        {
            MatchState table = host.matchState;
            List<string> lines = new List<string>(table.Cards.Count + table.Containers.Count);
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in table.Cards)
                lines.Add(pair.Key.Value.ToString("N") + pair.Value.Face + pair.Value.BaseState.ContainerId.Value.ToString("N"));
            foreach (KeyValuePair<ContainerId, ContainerState> pair in table.Containers)
            {
                StringBuilder order = new StringBuilder(pair.Key.Value.ToString("N"));
                foreach (TabletopObjectId id in pair.Value.ObjectIds) order.Append(id.Value.ToString("N"));
                lines.Add(order.ToString());
            }

            lines.Sort(string.CompareOrdinal);
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
        }

        // The guest's own description of its scene, checked here against what is hidden from it (counts only).
        private void HostCheckInventory(Member member, byte[] data)
        {
            PlayerId recipient = RecipientOf(member);
            MatchState full = host.matchState;
            RecipientSnapshotProjection.Classify(full, recipient, out HashSet<TabletopObjectId> hidden, out _);
            HashSet<ObjectDefinitionId> definitions = TabletopComposition.HiddenOnlyDefinitions(full, hidden);
            HashSet<string> names = TabletopComposition.HiddenOnlyNames(full, hidden, host.trapFloorTemplate.CardLabels);
            HashSet<Texture> pictures = host.HiddenOnlyPictures(full, hidden, definitions);
            HashSet<string> pictureNames = new HashSet<string>();
            foreach (Texture picture in pictures) if (picture != null && !string.IsNullOrEmpty(picture.name)) pictureNames.Add(picture.name);

            PrivateSceneInventory inventory = PrivateSceneInventory.Read(data, 1);
            int ids = 0, defs = 0, texts = 0, cardNames = 0, textures = 0;
            foreach (Guid id in inventory.CardIds) if (hidden.Contains(new TabletopObjectId(id))) ids++;
            foreach (Guid definition in inventory.Definitions) if (definitions.Contains(new ObjectDefinitionId(definition))) defs++;
            foreach (string text in inventory.Texts)
                foreach (string name in names) if (text.IndexOf(name, StringComparison.Ordinal) >= 0) { texts++; break; }
            foreach (string name in inventory.CardObjectNames)
                foreach (string hiddenName in names) if (name.IndexOf(hiddenName, StringComparison.Ordinal) >= 0) { cardNames++; break; }
            foreach (string texture in inventory.TextureNames) if (pictureNames.Contains(texture)) textures++;
            PrivateNote($"Guest scene check for {member.Name} ({inventory.CardIds.Count} card views, {inventory.Texts.Count} texts, {inventory.TextureNames.Count} card pictures): "
                + $"hidden real ids {ids}, hidden-only definitions {defs}, texts naming a hidden-only card {texts}, card objects named after one {cardNames}, hidden-only pictures {textures}. "
                + $"Guest says: {inventory.Summary}");
        }

        private sealed class RoomSeats : IPrivateSeats
        {
            private readonly OnlinePlayPresenter online;
            private readonly List<PlayerId> seated = new List<PlayerId>(4);

            public RoomSeats(OnlinePlayPresenter online)
            {
                this.online = online;
                foreach (TrapFloorPlayerSetupDefinition setup in online.host.trapFloorTemplate.Players)
                    if (online.host.matchState.Seats.TryGetValue(setup.SeatId, out SeatState seat) && !seat.OccupantPlayerId.IsEmpty)
                        seated.Add(seat.OccupantPlayerId);
            }

            private TrapFloorPlayerSetupDefinition SetupOf(PlayerId player)
            {
                foreach (TrapFloorPlayerSetupDefinition setup in online.host.trapFloorTemplate.Players)
                    if (online.host.matchState.Seats.TryGetValue(setup.SeatId, out SeatState seat) && seat.OccupantPlayerId == player) return setup;
                return null;
            }

            public bool IsSeated(PlayerId player) => !player.IsEmpty && seated.Contains(player);

            // PRIVACY-P5 (s149), mode policy. Free and Reminders: tabletop freedom (any seated player, any time; the
            // seat, source, destination, reference, privacy and duplicate checks still apply). Full: the game's own turn
            // on the host's table (the active player, in its player turn, on a floor still being played).
            public bool IsTurnOf(PlayerId player)
            {
                if (!IsSeated(player)) return false;
                if (PlayAssist.Level != PlayAssistLevel.Full) return true;
                TrapFloorTurnState turn = online.host.trapFloorTurnState;
                return turn != null && !turn.IsCurrentFloorFailed && turn.Phase == TrapFloorTurnPhase.PlayerTurn && turn.ActivePlayerId == player;
            }

            // Full: the game's hand limit (its Controller configuration), as its draw bar uses it. Free / Reminders: none.
            public int DrawLimit(PlayerId player) =>
                PlayAssist.Level == PlayAssistLevel.Full ? online.host.trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize : -1;

            public ContainerId DeckOf(PlayerId player) => SetupOf(player)?.ControllerDeckId ?? ContainerId.Empty;

            public ContainerId HandOf(PlayerId player) => SetupOf(player)?.HandContainerId ?? ContainerId.Empty;

            public bool IsOwnConsoleSlot(PlayerId player, ContainerId slot)
            {
                TrapFloorPlayerSetupDefinition setup = SetupOf(player);
                return setup != null
                    && online.host.matchState.Seats.TryGetValue(setup.SeatId, out SeatState seat)
                    && seat.Console != null
                    && seat.Console.ContainsSlot(slot);
            }

            public IReadOnlyList<PlayerId> Seated => seated;
        }

        // ---------- guest ----------

        private void ClientReadPrivate(byte type, byte[] data)
        {
            if (!PrivateDevSession.Enabled)
            {
                Debug.LogWarning($"[PrivateSession] Ignored private message {type}: the private development session is off here.");
                return;
            }

            byte[] payload = new byte[data.Length - 1];
            Array.Copy(data, 1, payload, 0, payload.Length);
            switch (type)
            {
                case MsgPrivatePackage:
                    GuestPackage(payload);
                    break;
                case MsgPrivateUpdate:
                    GuestUpdate(payload);
                    break;
                case MsgPrivateVerdict:
                    GuestVerdict(payload);
                    break;
                default:
                    PrivateNote($"Ignored private message {type}: guests do not take it.");
                    break;
            }
        }

        private void GuestPackage(byte[] bytes)
        {
            privateGuest = true;
            EnsurePrivatePanel();
            packagesReceived++;
            SaveReceived("package", bytes);
            RecipientBootstrapPackage package;
            try
            {
                package = (RecipientBootstrapPackage)ConsoleCards.Application.Authority.TableWire.Read(bytes, 0, bytes.Length, false);
            }
            catch (Exception exception)
            {
                PrivateNote("The package could not be read: " + exception.Message);
                return;
            }

            if (IsFrontEndOpen())
            {
                host.GameSelectPanel.returnToGameSelect = false;
                host.runtimeUi.CloseGameSelect(); // the game starts: to the table
            }

            PlayerId me = myPlayer == Guid.Empty ? RecipientSnapshotProjection.Spectator : new PlayerId(myPlayer);
            PendingSceneMeasure measure = BeginSceneMeasure("full scene build from a package");
            bool built = host.BuildPrivateRecipientScene(package, me, host.CurrentBoardCardBack(), host.TrapFloorGame.BoardMatPicture, out RecipientClientTable client, out string error);
            fullBuilds++;
            EndSceneWork(measure);
            if (!built)
            {
                PrivateNote("The scene could not be built from the package: " + error);
                return;
            }

            int hidden = 0;
            foreach (ClientCardView card in client.Cards) if (card.IsHidden) hidden++;
            PrivateNote($"Received package {packagesReceived}: {bytes.Length} bytes, sha256 {Sha(bytes)}; {client.Cards.Count} cards ({hidden} as backs); "
                + $"own hand {OwnHandSummary()}; scene built from it alone (legacy table messages dropped so far: {legacyRefused}).");
            host.ShowMessage("Private development session: your table is built from your own package only.");
        }

        private void GuestUpdate(byte[] bytes)
        {
            updatesReceived++;
            SaveReceived("update", bytes);
            PrivateUpdate update;
            try
            {
                update = RecipientEventApplier.Read(bytes);
            }
            catch (Exception exception)
            {
                PrivateNote("An update could not be read: " + exception.Message);
                AskForOwnPackage();
                return;
            }

            string problem = RecipientEventApplier.Apply(host.matchState, update);
            if (problem != null)
            {
                digestMismatches++;
                PrivateNote($"Update {updatesReceived} ({bytes.Length} bytes) did not apply: {problem}. Asking the host for this computer's package (never a full table).");
                AskForOwnPackage();
                return;
            }

            RecipientCardEvent ev = update.Event;
            bool identity = !ev.Definition.Equals(RecipientSnapshotProjection.HiddenDefinition);
            if (!identity && !ev.PreviousReference.Equals(ev.Reference)) lastWithdrawnReference = ev.PreviousReference;
            PendingSceneMeasure measure = BeginSceneMeasure(IncrementalScene ? "incremental update" : "full scene rebuild");
            string error;
            bool shown = IncrementalScene ? host.ApplyPrivateEventToScene(ev, out error) : host.RebuildPrivateSceneFromOwnTable(out error);
            if (!shown && IncrementalScene)
            {
                // The views could not follow the event: the whole scene is built again from this computer's own table
                // (never from anything else). Counted, so a run shows how often it happens.
                PrivateNote("The incremental update could not be shown (" + error + "): building the scene again from this computer's own table.");
                measure.What = "full scene rebuild (after a failed incremental update)";
                shown = host.RebuildPrivateSceneFromOwnTable(out error);
                fullBuilds++;
            }
            else if (IncrementalScene) incrementalUpdates++;
            else fullBuilds++;
            EndSceneWork(measure);
            if (!shown)
            {
                PrivateNote("The scene could not follow the update: " + error);
                return;
            }

            string kind = host.matchState.Containers.TryGetValue(ev.To, out ContainerState toContainer) ? toContainer.Kind.ToString() : "?";
            PrivateNote($"Update {updatesReceived}: {bytes.Length} bytes, sha256 {Sha(bytes)}; a card moved {(identity ? "WITH its identity (" + CardName(ev.Definition) + ")" : "as a back")} "
                + $"into a {kind} now holding {ev.ToCount}{(ev.PreviousReference.Equals(ev.Reference) ? "" : " (new reference here)")}; digest matches the host's; own hand {OwnHandSummary()}.");
        }

        private void GuestVerdict(byte[] bytes)
        {
            if (bytes.Length < 17) return;
            byte[] id = new byte[16];
            Array.Copy(bytes, 0, id, 0, 16);
            PrivateRequestVerdict verdict = (PrivateRequestVerdict)bytes[16];
            PrivateNote($"Host's answer to request {new Guid(id).ToString().Substring(0, 8)}: {verdict}.");
            host.ShowMessage($"Host: {verdict}.");
        }

        private void AskForOwnPackage()
        {
            BeginMessage(MsgPrivateNeedPackage);
            SendToHost();
        }

        private void SaveReceived(string kind, byte[] bytes)
        {
            try
            {
                string folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Logs", "PrivateReceived"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, $"{(packagesReceived + updatesReceived):D3}-{kind}.bin"), bytes);
            }
            catch (Exception)
            {
            }
        }

        private string CardName(ObjectDefinitionId definition) =>
            host.TryGetCardLook(definition, out ConsoleCards.Definitions.CardDefinition card) ? card.DisplayName : "a card";

        private RecipientSeatLayout OwnSeat()
        {
            RecipientPublicLayout layout = host.privateViewLayout;
            if (layout == null || myPlayer == Guid.Empty || host.matchState == null) return null;
            foreach (RecipientSeatLayout seat in layout.Seats)
                if (host.matchState.Seats.TryGetValue(seat.Seat, out SeatState state) && state.OccupantPlayerId.Value == myPlayer) return seat;
            return null;
        }

        private string OwnHandSummary()
        {
            RecipientSeatLayout seat = OwnSeat();
            if (seat == null || !host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand)) return "none (spectator)";
            int faces = 0;
            foreach (TabletopObjectId id in hand.ObjectIds)
                if (host.matchState.Cards.TryGetValue(id, out CardInstanceState card) && !card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) faces++;
            return $"{hand.Count} cards, {faces} with their faces";
        }

        // ---------- guest: the development controls (PrivateSessionPanel) ----------

        internal string PrivateStatus()
        {
            if (privateHosting)
                return $"HOST · packages sent {packagesSent}, updates sent {updatesSent}, legacy table messages refused {legacyRefused}";
            if (privateGuest)
                return $"GUEST · packages {packagesReceived}, updates {updatesReceived}, digest mismatches {digestMismatches}, legacy table messages dropped {legacyRefused} · hand {OwnHandSummary()}\n"
                    + $"scene: {(IncrementalScene ? "incremental" : "FULL REBUILD (measuring)")} · full builds {fullBuilds}, incremental updates {incrementalUpdates}";
            return InRoom ? "In a room (private development session ON): waiting for the host's table" : "Private development session ON: host or join a room";
        }

        internal void PrivateDraw()
        {
            RecipientSeatLayout seat = OwnSeat();
            if (!privateGuest || seat == null) { PrivateNote("Draw: no seat of yours in the private table."); return; }
            SendPrivateRequest(PrivateRequestCodec.Draw(new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = seat.ControllerDeck }), "Draw");
        }

        internal void PrivatePlace() => PlaceInto(OwnFreeShownSlot(), false, "Place face up");

        // PRIVACY-P5: the first card on this seat's Console back into its hand.
        internal void PrivateReturn()
        {
            TabletopObjectId card = FirstOwnConsoleCard();
            if (!privateGuest || card.IsEmpty) { PrivateNote("Return: no card on your Console."); return; }
            SendPrivateRequest(PrivateRequestCodec.Card(PrivateRequestCodec.KindReturn, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = card }), "Return to hand");
        }

        // PRIVACY-P5: the first card in this seat's hand (else on its Console) onto the first public discard pile.
        internal void PrivateDiscard()
        {
            RecipientSeatLayout seat = OwnSeat();
            TabletopObjectId card = TabletopObjectId.Empty;
            if (seat != null && host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) && hand.Count > 0) card = hand.GetObjectAt(0);
            if (card.IsEmpty) card = FirstOwnConsoleCard();
            ContainerId pile = PublicDiscardPile();
            if (!privateGuest || card.IsEmpty || pile.IsEmpty) { PrivateNote("Discard: no card of yours, or no public discard pile."); return; }
            SendPrivateRequest(PrivateRequestCodec.Card(PrivateRequestCodec.KindDiscard, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = card, Destination = pile }), "Discard");
        }

        // PRIVACY-P5 bad requests: a card in ANOTHER seat's hand (this computer's handle for that back), and the old
        // public reference of a card that has since become a back here.
        internal void PrivateDiscardOthersCard()
        {
            TabletopObjectId card = FirstCardInAnotherHand();
            ContainerId pile = PublicDiscardPile();
            if (card.IsEmpty || pile.IsEmpty) { PrivateNote("Other's card: no card in another hand."); return; }
            SendPrivateRequest(PrivateRequestCodec.Card(PrivateRequestCodec.KindDiscard, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = card, Destination = pile }),
                "Discard a card in ANOTHER seat's hand by this computer's handle (expect InvalidSource)");
        }

        internal void PrivateDiscardWithdrawn()
        {
            ContainerId pile = PublicDiscardPile();
            if (lastWithdrawnReference.IsEmpty || pile.IsEmpty) { PrivateNote("Old public ref: no card has turned into a back here yet."); return; }
            SendPrivateRequest(PrivateRequestCodec.Card(PrivateRequestCodec.KindDiscard, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = lastWithdrawnReference, Destination = pile }),
                "Discard by the OLD public reference of a card now hidden here (expect HiddenRealId)");
        }

        // Discard into a seat's Console slot (not a public discard pile): expect InvalidDestination.
        internal void PrivateDiscardIntoSlot()
        {
            RecipientSeatLayout seat = OwnSeat();
            ContainerId slot = OwnFreeShownSlot();
            if (seat == null || slot.IsEmpty || !host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) || hand.Count == 0) { PrivateNote("Bad discard: no card or slot."); return; }
            SendPrivateRequest(PrivateRequestCodec.Card(PrivateRequestCodec.KindDiscard, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = hand.GetObjectAt(0), Destination = slot }),
                "Discard into a Console slot, not a discard pile (expect InvalidDestination)");
        }

        internal void ToggleSceneMode()
        {
            IncrementalScene = !IncrementalScene;
            PrivateNote(IncrementalScene ? "Scene mode: incremental (only the moved card and its containers)." : "Scene mode: FULL REBUILD per update (s148 behaviour, for measuring).");
        }

        private TabletopObjectId FirstOwnConsoleCard()
        {
            RecipientSeatLayout seat = OwnSeat();
            if (seat == null || !host.matchState.Seats.TryGetValue(seat.Seat, out SeatState state) || state.Console == null) return TabletopObjectId.Empty;
            for (int i = 1; i < state.Console.SlotCount; i++)
                if (host.matchState.Containers.TryGetValue(state.Console.SlotContainerIds[i], out ContainerState slot) && slot.Count > 0) return slot.GetObjectAt(slot.TopIndex);
            return TabletopObjectId.Empty;
        }

        private TabletopObjectId FirstCardInAnotherHand()
        {
            RecipientSeatLayout mine = OwnSeat();
            if (host.privateViewLayout == null) return TabletopObjectId.Empty;
            foreach (RecipientSeatLayout seat in host.privateViewLayout.Seats)
            {
                if (mine != null && seat.Seat == mine.Seat) continue;
                if (host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) && hand.Count > 0) return hand.GetObjectAt(0);
            }

            return TabletopObjectId.Empty;
        }

        // A discard pile of the received table that no seat owns and that shows its cards (the host decides again).
        private ContainerId PublicDiscardPile()
        {
            foreach (KeyValuePair<ContainerId, ContainerState> pair in host.matchState.Containers)
                if (pair.Value.Kind == ContainerKind.DiscardPile && pair.Value.OwnerSeatId.IsEmpty && pair.Value.Visibility == ObjectVisibility.Public) return pair.Key;
            return ContainerId.Empty;
        }

        // ---------- PRIVACY-P5: measuring how the scene follows an update ----------

        private sealed class PendingSceneMeasure
        {
            public string What;
            public HashSet<GameObject> Before;
            public double CensusMs;
            public double WorkMs;
            public long Allocated;
            public int CardViewsBefore;
            public int CardViewsAfter;
            public System.Diagnostics.Stopwatch Clock;
            public long AllocatedAtStart;
            public int CollectionsAtStart;
            public int Collections;
            public int FramesSeen;
            public float WorstFrame;
        }

        private static HashSet<GameObject> GameObjectCensus()
        {
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            HashSet<GameObject> objects = new HashSet<GameObject>();
            for (int i = 0; i < all.Length; i++) if (all[i] != null) objects.Add(all[i].gameObject);
            return objects;
        }

        private PendingSceneMeasure BeginSceneMeasure(string what)
        {
            System.Diagnostics.Stopwatch census = System.Diagnostics.Stopwatch.StartNew();
            PendingSceneMeasure m = new PendingSceneMeasure { What = what, Before = GameObjectCensus(), CardViewsBefore = host.Registry.cardViews.Count };
            census.Stop();
            m.CensusMs = census.Elapsed.TotalMilliseconds;
            m.AllocatedAtStart = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            m.CollectionsAtStart = GC.CollectionCount(0);
            m.Clock = System.Diagnostics.Stopwatch.StartNew();
            return m;
        }

        private void EndSceneWork(PendingSceneMeasure m)
        {
            m.Clock.Stop();
            m.WorkMs = m.Clock.Elapsed.TotalMilliseconds;
            m.Allocated = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - m.AllocatedAtStart;
            m.Collections = GC.CollectionCount(0) - m.CollectionsAtStart;
            m.CardViewsAfter = host.Registry.cardViews.Count;
            pendingMeasure = m; // finished on the next frame (destroyed objects are gone by then; that frame's time includes this one's work)
        }

        // Every frame on a guest: keeps recent frame times, and finishes a pending measurement.
        private void TickPrivateGuestMeasure()
        {
            if (!privateGuest) return;
            float dt = Time.unscaledDeltaTime * 1000f;
            if (pendingMeasure == null)
            {
                frameTimes[frameTimeNext] = dt;
                frameTimeNext = (frameTimeNext + 1) % frameTimes.Length;
                if (frameTimeCount < frameTimes.Length) frameTimeCount++;
                return;
            }

            // The work may land in the frame before or the one this Tick runs in (network messages are read at
            // different points of the frame), so the worst of the next three frames is taken.
            PendingSceneMeasure m = pendingMeasure;
            m.WorstFrame = Math.Max(m.WorstFrame, dt);
            if (++m.FramesSeen < 3) return;
            dt = m.WorstFrame;
            pendingMeasure = null;
            HashSet<GameObject> after = GameObjectCensus();
            int created = 0, destroyed = 0;
            foreach (GameObject o in after) if (!m.Before.Contains(o)) created++;
            foreach (GameObject o in m.Before) if (!after.Contains(o)) destroyed++;
            float[] sorted = new float[frameTimeCount];
            Array.Copy(frameTimes, sorted, frameTimeCount);
            Array.Sort(sorted);
            float median = frameTimeCount > 0 ? sorted[frameTimeCount / 2] : 0f;
            PrivateNote($"Scene measure ({m.What}): work {m.WorkMs:F1} ms; worst of the next 3 frames {dt:F1} ms ({Math.Max(0d, dt - m.CensusMs):F1} ms without the measuring census of {m.CensusMs:F1} ms; "
                + $"median of the last {frameTimeCount} frames {median:F1} ms); GameObjects created {created}, destroyed {destroyed} (scene now {after.Count}); "
                + $"managed heap growth {m.Allocated / 1024d:F0} KB ({m.Collections} GC collections during the work); card views {m.CardViewsBefore} → {m.CardViewsAfter}.");
        }

        internal void PrivateResendLast()
        {
            if (lastRequestBytes == null) { PrivateNote("No request to send again."); return; }
            BeginMessage(MsgPrivateRequest);
            writer.Write(lastRequestBytes);
            SendToHost();
            PrivateNote("Sent the last request again (same request id): expect Duplicate.");
        }

        internal void PrivatePlaceOnOtherSeat() => PlaceInto(OtherSeatSlot(), false, "Place into ANOTHER seat's slot (expect InvalidDestination)");

        internal void PrivatePlaceForged()
        {
            RecipientSeatLayout seat = OwnSeat();
            ContainerId slot = OwnFreeShownSlot();
            if (seat == null || slot.IsEmpty) { PrivateNote("Forged place: no slot."); return; }
            SendPrivateRequest(PrivateRequestCodec.Place(new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = new TabletopObjectId(Guid.NewGuid()), Destination = slot }),
                "Place a made-up reference (expect UnknownReference)");
        }

        internal void PrivatePlaceStale()
        {
            ContainerId slot = OwnFreeShownSlot();
            if (lastPlacedReference.IsEmpty || slot.IsEmpty) { PrivateNote("Stale place: place a card first."); return; }
            SendPrivateRequest(PrivateRequestCodec.Place(new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = lastPlacedReference, Destination = slot }),
                "Place the card already placed again (expect InvalidSource)");
        }

        internal void PrivateResync()
        {
            AskForOwnPackage();
            PrivateNote("Asked the host for this computer's own package (recipient-specific resync).");
        }

        private void PlaceInto(ContainerId slot, bool _, string what)
        {
            RecipientSeatLayout seat = OwnSeat();
            if (!privateGuest || seat == null || !host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) || hand.Count == 0)
            {
                PrivateNote(what + ": no card in your hand.");
                return;
            }

            if (slot.IsEmpty) { PrivateNote(what + ": no slot."); return; }
            TabletopObjectId card = hand.GetObjectAt(0); // the card is named by this computer's own reference for it
            lastPlacedReference = card;
            SendPrivateRequest(PrivateRequestCodec.Place(new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = card, Destination = slot }), what);
        }

        private void SendPrivateRequest(byte[] request, string what)
        {
            lastRequestBytes = request;
            lastRequestId = new Guid(new ReadOnlySpan<byte>(request, 1, 16));
            BeginMessage(MsgPrivateRequest);
            writer.Write(request);
            SendToHost();
            PrivateNote($"Sent {what} (request {lastRequestId.ToString().Substring(0, 8)}).");
        }

        // The first empty slot on the shown side of this seat's Console (after the Main slot).
        private ContainerId OwnFreeShownSlot()
        {
            RecipientSeatLayout seat = OwnSeat();
            if (seat == null || !host.matchState.Seats.TryGetValue(seat.Seat, out SeatState state) || state.Console == null) return ContainerId.Empty;
            ConsoleLayoutData layout = host.GetConsoleLayout();
            bool back = state.Console.ShowsBack;
            for (int i = 1; i < state.Console.SlotCount && i < layout.CardSlots.Count; i++)
            {
                if ((layout.CardSlots[i].Side == ConsoleMatSide.Back) != back) continue;
                ContainerId slot = state.Console.SlotContainerIds[i];
                if (host.matchState.Containers.TryGetValue(slot, out ContainerState container) && container.Count == 0) return slot;
            }

            return ContainerId.Empty;
        }

        private ContainerId OtherSeatSlot()
        {
            RecipientSeatLayout mine = OwnSeat();
            if (host.privateViewLayout == null) return ContainerId.Empty;
            foreach (RecipientSeatLayout seat in host.privateViewLayout.Seats)
            {
                if (mine != null && seat.Seat == mine.Seat) continue;
                if (host.matchState.Seats.TryGetValue(seat.Seat, out SeatState state) && state.Console != null && state.Console.SlotCount > 1)
                    return state.Console.SlotContainerIds[1];
            }

            return ContainerId.Empty;
        }

        /// <summary>The guest's scene, described for the host's check (ids, definitions, texts, card names, pictures).</summary>
        internal void PrivateSendInventory()
        {
            if (!privateGuest) return;
            PrivateSceneInventory inventory = new PrivateSceneInventory();
            foreach (CardView view in UnityEngine.Object.FindObjectsByType<CardView>(FindObjectsInactive.Include))
            {
                if (view == null) continue;
                inventory.CardObjectNames.Add(view.gameObject.name);
                if (view.CardState == null) continue;
                inventory.CardIds.Add(view.CardState.BaseState.Id.Value);
                inventory.Definitions.Add(view.CardState.BaseState.DefinitionId.Value);
                foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.sharedMaterial == null) continue;
                    Texture main = renderer.sharedMaterial.mainTexture;
                    if (main != null && !string.IsNullOrEmpty(main.name)) inventory.TextureNames.Add(main.name);
                    if (renderer.sharedMaterial.HasProperty("_BaseMap") && renderer.sharedMaterial.GetTexture("_BaseMap") is Texture baseMap && !string.IsNullOrEmpty(baseMap.name))
                        inventory.TextureNames.Add(baseMap.name);
                }
            }

            foreach (TextMesh text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include))
                if (text != null && !string.IsNullOrEmpty(text.text)) inventory.Texts.Add(text.text);
            foreach (KeyValuePair<TabletopObjectId, string> label in host.labelsByCardId)
                if (!string.IsNullOrEmpty(label.Value)) inventory.Texts.Add(label.Value);
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in host.matchState.Cards)
            {
                inventory.CardIds.Add(pair.Key.Value);
                inventory.Definitions.Add(pair.Value.BaseState.DefinitionId.Value);
            }

            int backs = 0;
            foreach (CardInstanceState card in host.matchState.Cards.Values)
                if (card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) backs++;
            inventory.Summary = $"scene table {host.matchState.Cards.Count} cards ({backs} backs); shared table {sharedTable}; command door {Replicated.Mode}; "
                + $"seed received {(lastSeed != 0 ? "YES" : "no")}; table setups received {(applySetup != null ? "YES" : "none")}; legacy table messages dropped {legacyRefused}; "
                + $"commands refused here {host.PrivateViewAuthority.RefusedSummary()}.";
            BeginMessage(MsgPrivateInventory);
            writer.Write(inventory.Write());
            SendToHost();
            PrivateNote("Sent this scene's inventory to the host for its check. " + inventory.Summary);
        }

        // ---------- ending ----------

        private void EndPrivateSession(string why)
        {
            if (!privateGuest && !privateHosting) return;
            bool guest = privateGuest;
            PrivateNote("Private development session ended: " + why);
            privateGuest = false;
            privateHosting = false;
            privateHost = null;
            privateHostMatch = null;
            privateHostPending = false;
            if (guest && host.privateViewActive)
            {
                // The filtered table is not kept as if it were a game: the scene goes back to an empty table.
                host.SessionService.TryReplaceTable(TabletopSessionSelection.EmptyCustom);
                host.ShowMessage("The host left: the private development session ended. (A guest never takes over a private session.)");
            }
        }
#else
        private bool StartPrivateHosting() => false;
        private bool PreparePrivateHostSeed(out int seed) { seed = 0; return false; }
        private void TickPrivate() { }
        private void HostReadPrivate(Member member, byte type, byte[] data) { }
        private void ClientReadPrivate(byte type, byte[] data) { }
        private void SendPrivatePackage(Member member) { }
        private void EndPrivateSession(string why) { }
        private int legacyRefused;
        private void PrivateNote(string line) { }
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>PRIVACY-P4: what a guest's scene holds, sent to the host for its check. Plain bytes (no object graph).</summary>
    internal sealed class PrivateSceneInventory
    {
        public readonly List<Guid> CardIds = new List<Guid>();
        public readonly List<Guid> Definitions = new List<Guid>();
        public readonly List<string> Texts = new List<string>();
        public readonly List<string> CardObjectNames = new List<string>();
        public readonly List<string> TextureNames = new List<string>();
        public string Summary = string.Empty;

        public byte[] Write()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(stream, Encoding.UTF8))
            {
                WriteGuids(w, CardIds);
                WriteGuids(w, Definitions);
                WriteStrings(w, Texts);
                WriteStrings(w, CardObjectNames);
                WriteStrings(w, TextureNames);
                w.Write(Summary ?? string.Empty);
                w.Flush();
                return stream.ToArray();
            }
        }

        public static PrivateSceneInventory Read(byte[] data, int offset)
        {
            PrivateSceneInventory inventory = new PrivateSceneInventory();
            using (BinaryReader r = new BinaryReader(new MemoryStream(data, offset, data.Length - offset, false), Encoding.UTF8))
            {
                ReadGuids(r, inventory.CardIds);
                ReadGuids(r, inventory.Definitions);
                ReadStrings(r, inventory.Texts);
                ReadStrings(r, inventory.CardObjectNames);
                ReadStrings(r, inventory.TextureNames);
                inventory.Summary = r.ReadString();
            }

            return inventory;
        }

        private const int MaxItems = 100000;

        private static void WriteGuids(BinaryWriter w, List<Guid> items)
        {
            w.Write(items.Count);
            foreach (Guid g in items) w.Write(g.ToByteArray());
        }

        private static void ReadGuids(BinaryReader r, List<Guid> items)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > MaxItems) throw new InvalidDataException("bad count");
            for (int i = 0; i < count; i++) items.Add(new Guid(r.ReadBytes(16)));
        }

        private static void WriteStrings(BinaryWriter w, List<string> items)
        {
            w.Write(items.Count);
            foreach (string s in items) w.Write(s ?? string.Empty);
        }

        private static void ReadStrings(BinaryReader r, List<string> items)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > MaxItems) throw new InvalidDataException("bad count");
            for (int i = 0; i < count; i++) items.Add(r.ReadString());
        }
    }

    /// <summary>
    /// PRIVACY-P4: the private development session's controls, drawn only while the session is on (Editor or a
    /// development build with the flag file). Every button sends the same request the game's own drag will send later.
    /// </summary>
    internal sealed class PrivateSessionPanel : MonoBehaviour
    {
        internal OnlinePlayPresenter Online;
        private Vector2 scroll;
        private bool folded;

        private void OnGUI()
        {
            if (Online == null || !PrivateDevSession.Enabled) return;
            const float width = 560f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 12f, 12f, width, folded ? 80f : 480f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("PRIVATE DEV SESSION · development only · not normal online play");
            if (GUILayout.Button(folded ? "Show" : "Hide", GUILayout.Width(60f))) folded = !folded;
            GUILayout.EndHorizontal();
            GUILayout.Label(Online.PrivateStatus());
            if (!folded)
            {
                if (Online.IsPrivateGuest)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Draw")) Online.PrivateDraw();
                    if (GUILayout.Button("Place face up")) Online.PrivatePlace();
                    if (GUILayout.Button("Return to hand")) Online.PrivateReturn();
                    if (GUILayout.Button("Discard")) Online.PrivateDiscard();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Resync (my package)")) Online.PrivateResync();
                    if (GUILayout.Button("Send scene check")) Online.PrivateSendInventory();
                    if (GUILayout.Button(Online.IncrementalScene ? "Scene: incremental" : "Scene: FULL rebuild")) Online.ToggleSceneMode();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Bad: repeat last")) Online.PrivateResendLast();
                    if (GUILayout.Button("Bad: other seat")) Online.PrivatePlaceOnOtherSeat();
                    if (GUILayout.Button("Bad: made-up ref")) Online.PrivatePlaceForged();
                    if (GUILayout.Button("Bad: stale ref")) Online.PrivatePlaceStale();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Bad: other's card")) Online.PrivateDiscardOthersCard();
                    if (GUILayout.Button("Bad: old public ref")) Online.PrivateDiscardWithdrawn();
                    if (GUILayout.Button("Bad: discard to slot")) Online.PrivateDiscardIntoSlot();
                    GUILayout.EndHorizontal();
                }

                scroll = GUILayout.BeginScrollView(scroll);
                IReadOnlyList<string> lines = Online.PrivateLog;
                for (int i = lines.Count - 1; i >= 0; i--) GUILayout.Label(lines[i]);
                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();
        }
    }
#endif
}
