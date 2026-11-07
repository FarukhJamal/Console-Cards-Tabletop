using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
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
        private const byte MsgPrivateTurn = 46;       // PRIVACY-P6A host → guest: one accepted turn transition (public, with its revision)
        private const byte MsgPrivateTurnVerdict = 47; // PRIVACY-P6A host → guest: what became of the guest's End Turn request

        private static bool IsPrivateMessage(byte type) => type >= MsgPrivatePackage && type <= MsgPrivateTurnVerdict;

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
        // PRIVACY-P6A (s150): one pipeline for every seat's actions, the host's own included.
        private PrivateHostInterceptor privateInterceptor;
        private readonly TrapFloorEndTurnAuthority privateEndTurnAuthority = new TrapFloorEndTurnAuthority();
        private bool privateEndTurnRunning; // the host runs a checked End Turn through the game's own turn service
        private int hostOwnAccepted;
        private int hostOwnRefused;
        private int turnTransitions;
        private int resyncRequests;     // guests' NeedPackage requests received (host)
        // Guest: the host's revision this computer's table shows, and how updates arrived.
        private long privateRevision = -1;
        private int staleIgnored;
        private int revisionGaps;
        private int turnUpdatesReceived;
        private PrivateTurnSnapshot privateTurn;
        private byte[] lastEndTurnBytes;
        // PRIVACY-P6B (s151)
        private int fallbackPackages;   // host: packages sent because a change could not be told as object changes
        private int sweeps;             // host: change sets published for changes made outside a private request
        private int operationsAccepted;
        private int operationsRefused;
        private float nextSweepAt;
        private int lastPublishedFingerprint;
        private string lastPublishedHudSignature;
        private PrivateHudSnapshot privateHud; // guest: the status card the host sent
        private float nextHudRenderAt;
        private int changeSetsApplied;
        private int forwardedCommands;  // guest: table gestures sent to the host as private requests

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
            TickPrivateGuestHud();
            if (!privateHosting || !hosting || session == null) return;
            if (!host.IsInitialized || host.matchState == null || host.trapFloorTemplate == null || host.trapFloorTurnState == null) return;
            if (privateHostPending || privateHost == null)
            {
                StartPrivateTable();
                return;
            }

            if (!ReferenceEquals(privateHostMatch, host.matchState))
            {
                PrivateTableReplaced("the host's table was replaced (Undo, reset or a new floor)");
                return;
            }

            // PRIVACY-P6B: anything that changed the host's table outside a private request (a pose, a game step the
            // host ran) reaches every guest as a filtered change set; checked cheaply four times a second.
            if (Time.unscaledTime < nextSweepAt) return;
            nextSweepAt = Time.unscaledTime + 0.25f;
            int fingerprint = HostFingerprintFast();
            PrivateHudSnapshot hud = CurrentHud();
            string hudSignature = hud?.Signature();
            if (fingerprint == lastPublishedFingerprint && hudSignature == lastPublishedHudSignature) return;
            Dictionary<PlayerId, byte[]> updates = privateHost.PublishChanges(null, hud);
            if (updates != null) sweeps++;
            SendPublish(updates, "change made outside a private request");
        }

        // The first deal: each guest gets its own package.
        private void StartPrivateTable()
        {
            bool first = privateHostPending;
            privateHostPending = false;
            privateHostMatch = host.matchState;
            privateHost = new PrivateTableHost(host.matchState, new RoomSeats(this), host.trapFloorTemplate.CardLabels, RecipientSnapshotProjection.NewSessionKey())
            {
                Layout = host.BuildPublicLayout(),
                // PRIVACY-P6B: Draw/Place/Return/Discard run through the table's own command door.
                TransferExecutor = ExecuteTransferOnAuthority,
                HudSource = CurrentHud,
            };
            privateHost.SetInitialTurn(CurrentTurnSnapshot());
            privateHost.SetInitialHud(CurrentHud());
            privateEndTurnAuthority.Clear();
            // PRIVACY-P6A: from here every command made on the host's own computer meets the private pipeline first.
            if (privateInterceptor == null) privateInterceptor = new PrivateHostInterceptor(this);
            host.Replicated.Interceptor = privateInterceptor;
            PrivateNote(first ? "Host table dealt: sending each guest its own package." : "A new private table: sending each guest its own package.");
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.IsHost || !member.Connected || member.Connection < 0) continue;
                SendPrivatePackage(member);
            }

            lastPublishedFingerprint = HostFingerprintFast();
            lastPublishedHudSignature = privateHost.Hud?.Signature();
        }

        // PRIVACY-P6B: the host's table object was replaced by one that continues it (Undo, a reset, a new floor). The
        // private authority keeps its handles, revision and what each guest was sent: the difference goes out as change
        // sets, or, where it cannot be told as object changes (containers made or removed), as that guest's package.
        private void PrivateTableReplaced(string why)
        {
            privateHostMatch = host.matchState;
            privateHost.Rebind(host.matchState);
            privateHost.Layout = host.BuildPublicLayout();
            PrivateNote("Private table: " + why + "; sending what changed.");
            HostSceneFollow(null);
            SendPublish(privateHost.PublishChanges(CurrentTurnSnapshot(), CurrentHud()), why);
        }

        // Sends one publish: each guest its own update, or its own package when its change could not be told.
        private void SendPublish(Dictionary<PlayerId, byte[]> updates, string what)
        {
            if (privateHost == null) return;
            lastPublishedFingerprint = HostFingerprintFast();
            lastPublishedHudSignature = privateHost.Hud?.Signature();
            if (updates == null) return;
            int sent = 0, packages = 0;
            for (int i = 0; i < members.Count; i++)
            {
                Member other = members[i];
                if (other.IsHost || !other.Connected || other.Connection < 0) continue;
                PlayerId recipient = RecipientOf(other);
                if (privateHost.NeedPackage.Contains(recipient))
                {
                    SendPrivatePackage(other);
                    fallbackPackages++;
                    packages++;
                    continue;
                }

                if (!updates.TryGetValue(recipient, out byte[] update)) continue;
                CheckSentBytes(update, recipient, $"{what} (revision {privateHost.Revision}) to {other.Name}");
                SendPrivateBytes(other, MsgPrivateUpdate, update);
                updatesSent++;
                sent++;
            }

            if (packages > 0) PrivateNote($"Revision {privateHost.Revision} ({what}): {packages} guest(s) took their own package instead (fallback; the change made or removed a container or piece). Total fallbacks {fallbackPackages}.");
        }

        // A cheap fingerprint of the host's table (every object's place, face and pose; every container's order).
        private int HostFingerprintFast()
        {
            MatchState table = host.matchState;
            if (table == null) return 0;
            unchecked
            {
                int hash = 17;
                foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in table.Cards)
                {
                    TabletopObjectState state = pair.Value.BaseState;
                    hash = hash * 31 + pair.Key.GetHashCode();
                    hash = hash * 31 + state.ContainerId.GetHashCode();
                    hash = hash * 31 + (int)pair.Value.Face;
                    if (state.ContainerId.IsEmpty) hash = hash * 31 + PoseHash(state);
                }

                foreach (KeyValuePair<TabletopObjectId, PawnState> pair in table.Pawns) hash = hash * 31 + PoseHash(pair.Value.BaseState);
                foreach (KeyValuePair<TabletopObjectId, ConsoleCards.Core.Domain.Dice.DieState> pair in table.Dice) hash = hash * 31 + PoseHash(pair.Value.BaseState);
                foreach (KeyValuePair<TabletopObjectId, TokenState> pair in table.Tokens) hash = hash * 31 + PoseHash(pair.Value.BaseState);
                foreach (KeyValuePair<ContainerId, ContainerState> pair in table.Containers)
                {
                    hash = hash * 31 + pair.Key.GetHashCode();
                    for (int i = 0; i < pair.Value.Count; i++) hash = hash * 7 + pair.Value.GetObjectAt(i).GetHashCode();
                }

                foreach (KeyValuePair<SeatId, SeatState> pair in table.Seats) hash = hash * 3 + (pair.Value.Console != null && pair.Value.Console.ShowsBack ? 1 : 0);
                return hash;
            }
        }

        private static int PoseHash(TabletopObjectState state)
        {
            unchecked
            {
                return (int)Math.Round(state.Pose.Position.X * 500) * 73856093 ^ (int)Math.Round(state.Pose.Position.Y * 500) * 19349663 ^ (int)Math.Round(state.Pose.RotationDegrees * 5);
            }
        }

        // PRIVACY-P6B: Draw/Place/Return/Discard on the authority, through the table's own command door (the game's
        // rules, its Undo history and its views follow). One Undo step.
        private bool ExecuteTransferOnAuthority(PlayerId sender, TabletopObjectId real, ContainerState from, ContainerState to, CardFace? face)
        {
            bool passing = privateInterceptor.PassThrough;
            privateInterceptor.PassThrough = true;
            host.SessionService.BeginUndoBatch();
            try
            {
                bool moved = from.Kind == ContainerKind.Deck && to.Kind == ContainerKind.Hand && from.TryPeekTop(out TabletopObjectId top) && top == real
                    ? host.TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(new DrawCardsCommand(Ctx(sender), from.Id, to.Id, 1)).Succeeded
                    : host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(TransferCardCommand.ToContainer(Ctx(sender), real, from.Id, to.Id)).Succeeded;
                if (moved && face.HasValue && host.matchState.Cards.TryGetValue(real, out CardInstanceState card) && card.Face != face.Value
                    && !host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(Ctx(sender), real, face.Value)).Succeeded)
                    PrivateNote("The card moved but the table refused to turn it over (it stays as it lies).");
                return moved;
            }
            finally
            {
                host.SessionService.EndUndoBatch();
                privateInterceptor.PassThrough = passing;
            }
        }

        private CommandContext Ctx(PlayerId sender) => new CommandContext(CommandId.New(), host.matchState.Id, sender, null);

        // PRIVACY-P6B: the host's own views follow a change made for another seat (the table's own view sync leaves
        // a change to the computer whose gesture made it, and on the host every seat counts as played here).
        private sealed class HostSceneState
        {
            public readonly Dictionary<TabletopObjectId, long> Cards = new Dictionary<TabletopObjectId, long>();
            public readonly Dictionary<ContainerId, int> Orders = new Dictionary<ContainerId, int>();
        }

        private HostSceneState CaptureHostScene()
        {
            HostSceneState state = new HostSceneState();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in host.matchState.Cards)
                state.Cards[pair.Key] = ((long)pair.Value.BaseState.ContainerId.GetHashCode() << 8) ^ (long)pair.Value.Face ^ ((long)PoseHash(pair.Value.BaseState) << 20);
            foreach (KeyValuePair<ContainerId, ContainerState> pair in host.matchState.Containers)
            {
                int hash = pair.Value.Count;
                unchecked { for (int i = 0; i < pair.Value.Count; i++) hash = hash * 31 + pair.Value.GetObjectAt(i).GetHashCode(); }
                state.Orders[pair.Key] = hash;
            }

            return state;
        }

        private void HostSceneFollow(HostSceneState before)
        {
            try
            {
                HostSceneState after = CaptureHostScene();
                foreach (KeyValuePair<ContainerId, int> pair in after.Orders)
                    if (before == null || !before.Orders.TryGetValue(pair.Key, out int was) || was != pair.Value) host.ViewSync.SyncContainer(pair.Key);
                foreach (KeyValuePair<TabletopObjectId, long> pair in after.Cards)
                    if (before != null && before.Cards.TryGetValue(pair.Key, out long was) && was != pair.Value && host.matchState.Cards[pair.Key].BaseState.ContainerId.IsEmpty)
                        host.PresentAcceptedObject(pair.Key);
                host.RefreshCardContentVisibility();
                host.RefreshAcceptedCardFaces();
            }
            catch (Exception exception)
            {
                PrivateNote("The host's views could not follow a change for another seat: " + exception.Message);
            }
        }

        private PrivateHudSnapshot CurrentHud()
        {
            if (host.TrapFloorGame == null || !host.TrapFloorGame.TryBuildPublicHud(out string round, out string phase, out Color? color, out List<TrapFloorHudPlayer> players, out TrapFloorPlayerCardModel card))
                return null;
            PrivateHudSnapshot hud = new PrivateHudSnapshot { Round = round, Phase = phase, HasTurnColor = color.HasValue };
            if (color.HasValue) { hud.TurnR = color.Value.r; hud.TurnG = color.Value.g; hud.TurnB = color.Value.b; }
            if (players != null)
                foreach (TrapFloorHudPlayer player in players)
                    hud.Players.Add(new PrivateHudPlayer { Name = player.Name, R = player.Color.r, G = player.Color.g, B = player.Color.b, A = player.Color.a, Active = player.Active });
            if (card != null)
            {
                hud.HasCard = true;
                hud.CardTitle = card.Title;
                hud.CardAvatar = card.Avatar;
                hud.CardKeys = card.Keys;
                hud.CardStatus = card.Status;
                hud.CardInHand = card.InHand;
                hud.CardFloor = card.Floor;
            }

            return hud;
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
        // PRIVACY-P6A: strict. A card that turns hidden for a recipient is named by its old public place, not by its
        // real id (P5 still sent that id and counted it apart), so every hidden real id in the bytes counts.
        private void CheckSentBytes(byte[] bytes, PlayerId recipient, string what)
        {
            MatchState full = host.matchState;
            RecipientSnapshotProjection.Classify(full, recipient, out HashSet<TabletopObjectId> hidden, out _);
            int ids = 0;
            foreach (TabletopObjectId id in hidden)
                if (TabletopComposition.Contains(bytes, id.Value.ToByteArray())) ids++;
            HashSet<ObjectDefinitionId> definitions = TabletopComposition.HiddenOnlyDefinitions(full, hidden);
            int defs = 0;
            foreach (ObjectDefinitionId definition in definitions) if (TabletopComposition.Contains(bytes, definition.Value.ToByteArray())) defs++;
            HashSet<string> names = TabletopComposition.HiddenOnlyNames(full, hidden, host.trapFloorTemplate.CardLabels);
            int named = 0;
            foreach (string name in names) if (TabletopComposition.Contains(bytes, Encoding.UTF8.GetBytes(name))) named++;
            bool seed = privateSeedKnown && TabletopComposition.Contains(bytes, BitConverter.GetBytes(privateSeed));
            PrivateNote($"Sent {what}: {bytes.Length} bytes, sha256 {Sha(bytes)}; hidden real ids {ids}/{hidden.Count}"
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
                    resyncRequests++;
                    PrivateNote($"{member.Name} asked for its package again (resync request {resyncRequests}).");
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
            if (PrivateOperationCodec.IsOperation(data, 1))
            {
                Guid operationId = Guid.Empty;
                PrivateRequestVerdict operationVerdict = PrivateOperationCodec.TryRead(data, 1, out PrivateOperation op)
                    ? RunPrivateOperation(member, op, null, out operationId)
                    : PrivateRequestVerdict.UnknownReference;
                if (op == null) PrivateNote($"Refused a malformed operation from {member.Name}.");
                BeginMessage(MsgPrivateVerdict);
                WriteGuid(operationId);
                writer.Write((byte)operationVerdict);
                SendTo(member);
                return;
            }

            if (data.Length == 1 + PrivateRequestCodec.Size && data[1] == PrivateRequestCodec.KindEndTurn)
            {
                TrapFloorEndTurnVerdict turnVerdict = RunPrivateEndTurn(member, data, 1, out Guid turnRequestId);
                BeginMessage(MsgPrivateTurnVerdict);
                WriteGuid(turnRequestId);
                writer.Write((byte)turnVerdict);
                SendTo(member);
                return;
            }

            PrivateRequestVerdict verdict = RunPrivateCardRequest(member, data, 1, out Guid requestId);
            BeginMessage(MsgPrivateVerdict);
            WriteGuid(requestId);
            writer.Write((byte)verdict);
            SendTo(member);
        }

        // PRIVACY-P6A (Task 1): THE pipeline for a card action, whoever made it. A guest's request (its bytes as they
        // came) and the host's own action (the same codec bytes, built on this computer) both come here; the sender is
        // the room member the action came from (a guest's connection, or the host's own seat), never a value read from
        // the request. The host's seat gets no exception: the same seat, turn, hand-limit, reference, source,
        // destination and duplicate checks apply. One accepted action: one revision, one recipient-specific update
        // for every guest, the host's own scene following its table.
        private PrivateRequestVerdict RunPrivateCardRequest(Member member, byte[] data, int offset, out Guid requestId)
        {
            PlayerId sender = member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            string actor = member.IsHost ? "the host's own seat" : member.Name;
            requestId = Guid.Empty;
            PrivateRequestVerdict verdict;
            Dictionary<PlayerId, byte[]> updates = null;
            ContainerId from = ContainerId.Empty, to = ContainerId.Empty;
            string what;
            string before = HostTableFingerprint();
            long revisionBefore = privateHost.Revision;
            HostSceneState sceneBefore = CaptureHostScene();
            if (!PrivateRequestCodec.TryRead(data, offset, out byte kind, out PrivateDrawRequest draw, out PrivatePlaceRequest place, out PrivateCardRequest card))
            {
                verdict = PrivateRequestVerdict.UnknownReference;
                what = "a malformed request";
            }
            else if (kind == PrivateRequestCodec.KindDraw)
            {
                requestId = draw.RequestId;
                what = "Draw";
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

            if (verdict == PrivateRequestVerdict.Accepted && updates != null)
            {
                from = privateHost.LastFrom;
                to = privateHost.LastTo;
                // The host's own scene follows its table.
                host.ViewSync.SyncContainer(from);
                host.ViewSync.SyncContainer(to);
                HostSceneFollow(sceneBefore);
                SendPublish(updates, what + " update");
                if (member.IsHost) hostOwnAccepted++;
                PrivateNote($"Accepted {what} from {actor} (assist {PlayAssist.Level}); revision {revisionBefore} → {privateHost.Revision}; each guest computer here got its own change set.");
            }
            else
            {
                if (member.IsHost) hostOwnRefused++;
                string after = HostTableFingerprint();
                PrivateNote($"Refused {what} from {actor}: {verdict} (assist {PlayAssist.Level}); host table {(before == after ? "unchanged" : "CHANGED")}, revision {(privateHost.Revision == revisionBefore ? "unchanged" : "CHANGED")} ({privateHost.Revision}).");
            }

            return verdict;
        }

        // PRIVACY-P6A (Task 3): End Turn on the private path, for a guest and for the host's own seat alike. The check is
        // the game's own End Turn authority (seat from the connection, turn owner, round/phase still current, request id
        // once); the transition is the game's own turn service (AdvanceTrapFloorTurn), run once. PRIVACY-P6B: every
        // guest then gets one change set with the public turn, the status card and whatever the game's own turn steps
        // changed on the table (a tile turned over, an ability offer shuffled back), filtered for it.
        private TrapFloorEndTurnVerdict RunPrivateEndTurn(Member member, byte[] data, int offset, out Guid requestId)
        {
            requestId = Guid.Empty;
            string actor = member.IsHost ? "the host's own seat" : member.Name;
            if (!PrivateRequestCodec.TryReadEndTurn(data, offset, out PrivateEndTurnRequest wire))
            {
                PrivateNote($"Refused a malformed End Turn from {actor}.");
                return TrapFloorEndTurnVerdict.MissingRequestId;
            }

            requestId = wire.RequestId;
            PlayerId sender = member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            if (!new RoomSeats(this).IsSeated(sender)) sender = PlayerId.Empty; // a watcher has no turn to end
            TrapFloorEndTurnRequest request = new TrapFloorEndTurnRequest(wire.RequestId, wire.Round, (TrapFloorTurnPhase)wire.Phase, wire.TurnOwner, wire.Skipped);
            bool won = host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon;
            long revisionBefore = privateHost.Revision;
            TrapFloorEndTurnVerdict verdict = privateEndTurnAuthority.Check(host.trapFloorTurnState, sender, request, won);
            if (verdict != TrapFloorEndTurnVerdict.Accepted)
            {
                PrivateNote($"Refused End Turn from {actor}: {verdict} (asked for round {wire.Round}, {(TrapFloorTurnPhase)wire.Phase}); turn and revision unchanged ({privateHost.Revision}).");
                return verdict;
            }

            TrapFloorTurnState turn = host.trapFloorTurnState;
            int roundBefore = turn.CurrentRound;
            TrapFloorTurnPhase phaseBefore = turn.Phase;
            PlayerId ownerBefore = TrapFloorEndTurnAuthority.TurnOwnerOf(turn);
            int cardsBeforeFingerprint = HostFingerprintFast();
            HostSceneState sceneBefore = CaptureHostScene();
            privateEndTurnRunning = true;
            privateInterceptor.PassThrough = true; // the game's own turn steps use the game's own commands
            hostActingFor = member.Player;
            try
            {
                host.TrapFloorGame.AdvanceTrapFloorTurn(request.Skipped);
            }
            finally
            {
                hostActingFor = Guid.Empty;
                privateInterceptor.PassThrough = false;
                privateEndTurnRunning = false;
            }

            TrapFloorTurnState now = host.trapFloorTurnState;
            bool moved = now == null || now.CurrentRound != roundBefore || now.Phase != phaseBefore || now.IsCurrentFloorFailed
                || TrapFloorEndTurnAuthority.TurnOwnerOf(now) != ownerBefore;
            if (moved) turnTransitions++;
            // PRIVACY-P6B: what the game's own turn steps changed (a tile turned over, an ability offer shuffled back, a
            // pawn moved by a Trap) goes out with the turn in one change set per guest, one revision.
            HostSceneFollow(sceneBefore);
            Dictionary<PlayerId, byte[]> updates = privateHost.PublishChanges(moved ? CurrentTurnSnapshot() : null, CurrentHud());
            SendPublish(updates, "End Turn");
            PrivateTurnSnapshot snap = CurrentTurnSnapshot();
            PrivateNote($"Accepted End Turn from {actor}: " + (moved
                    ? $"round {roundBefore} {phaseBefore} → round {snap.Round} {(TrapFloorTurnPhase)snap.Phase}, turn of {SeatName(new PlayerId(snap.TurnOwner))}"
                    : "the game's own turn service did not end the turn (a step is still waiting)")
                + $"; revision {revisionBefore} → {privateHost.Revision}; card or piece changes from the turn's own steps: {(updates != null && HostFingerprintFast() != cardsBeforeFingerprint ? "yes (in the same change set)" : "none")}.");
            return verdict;
        }

        // PRIVACY-P6B: the host player's own Undo on a private host (see MayUndo).
        private bool MayUndoOnPrivateHost(ActiveSessionUndoTransaction transaction, bool redo)
        {
            Member me = HostMember();
            string refusal = null;
            if (redo) refusal = "Private session: Redo is not available yet.";
            else if (transaction == null || me == null) refusal = "There is nothing to undo.";
            else if (transaction.ActorPlayerId.IsEmpty || transaction.ActorPlayerId.Value != me.Player)
                refusal = $"The latest change is {(transaction.ActorPlayerId.IsEmpty ? "a game step" : NameOf(transaction.ActorPlayerId) ?? "another player's")}: only its own player can undo it.";
            else if (host.undoHistory.TryPeekUndo(out SessionUndoSnapshot before, out ActiveSessionUndoTransaction latest)
                && latest.CommandId == transaction.CommandId
                && host.TrapFloorGame.IsRulesResultStep(before.GameState, host.undoHistory.CurrentState.GameState, transaction.CommandId))
                refusal = HostUndoPolicy.Describe(HostUndoVerdict.RulesResult, false);
            if (refusal == null) return true;
            undoRefusal = refusal;
            host.ShowMessage(refusal);
            PrivateNote("Refused the host player's own Undo: " + refusal);
            return false;
        }

        private PrivateTurnSnapshot CurrentTurnSnapshot()
        {
            TrapFloorTurnState turn = host.trapFloorTurnState;
            if (turn == null) return new PrivateTurnSnapshot { Round = 0, Phase = 0, Ended = true };
            return new PrivateTurnSnapshot
            {
                Round = turn.CurrentRound,
                Phase = (int)turn.Phase,
                TurnOwner = TrapFloorEndTurnAuthority.TurnOwnerOf(turn).Value,
                ActivePlayer = turn.ActivePlayerId.Value,
                Ended = turn.IsCurrentFloorFailed || (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon)
            };
        }

        // "seat 2" for a player of this table (public: the seat layout), else "a watcher".
        private string SeatName(PlayerId player)
        {
            if (player.IsEmpty || host.matchState == null) return "nobody";
            RecipientPublicLayout layout = privateHosting ? privateHost?.Layout : host.privateViewLayout;
            if (layout != null)
                for (int i = 0; i < layout.Seats.Count; i++)
                    if (host.matchState.Seats.TryGetValue(layout.Seats[i].Seat, out SeatState state) && state.OccupantPlayerId == player)
                        return (player.Value == myPlayer ? "YOU, " : string.Empty) + "seat " + (i + 1);
            return "a player";
        }

        private Member HostMember()
        {
            for (int i = 0; i < members.Count; i++) if (members[i].IsHost) return members[i];
            return null;
        }

        // PRIVACY-P6A: the host's own action (its panel, or its own drag through the interceptor) as the same bytes a
        // guest sends, run through the same pipeline with the host's own seat as the sender.
        private bool RunHostOwnRequest(byte[] request, string what)
        {
            Member me = HostMember();
            if (!privateHosting || privateHost == null || me == null) { PrivateNote(what + ": no private table here yet."); return false; }
            if (request.Length == PrivateRequestCodec.Size && request[0] == PrivateRequestCodec.KindEndTurn)
            {
                TrapFloorEndTurnVerdict turnVerdict = RunPrivateEndTurn(me, request, 0, out _);
                if (turnVerdict != TrapFloorEndTurnVerdict.Accepted) host.ShowMessage($"Private session: End Turn refused ({turnVerdict}).");
                return turnVerdict == TrapFloorEndTurnVerdict.Accepted;
            }

            PrivateRequestVerdict verdict = RunPrivateCardRequest(me, request, 0, out _);
            if (verdict != PrivateRequestVerdict.Accepted) host.ShowMessage($"Private session: {what} refused ({verdict}).");
            return verdict == PrivateRequestVerdict.Accepted;
        }

        // PRIVACY-P6B: THE pipeline for a general card or deck operation, whoever asks: a guest's request bytes, or
        // the host's own gesture (its own references for the cards, then the gesture's own command). The same check
        // (PrivateOperationPolicy) for every seat; the table's own command does the work on the authority; every
        // guest gets its own change set. <paramref name="runOwnCommand"/>: the host's gesture's command (null: build it).
        private PrivateRequestVerdict RunPrivateOperation(Member member, PrivateOperation op, Func<bool> runOwnCommand, out Guid requestId)
        {
            requestId = op.RequestId;
            string actor = member.IsHost ? "the host's own seat" : member.Name;
            PlayerId sender = member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            if (privateHost == null) return PrivateRequestVerdict.NotSeated;
            if (op.Kind == PrivateOperationKind.Undo) return RunPrivateUndo(member, op);
            long revisionBefore = privateHost.Revision;
            string before = HostTableFingerprint();
            PrivateRequestVerdict verdict = PrivateOperationPolicy.Check(host.matchState, new RoomSeats(this), privateHost, sender, op, out PrivateResolvedOperation resolved);
            if (verdict != PrivateRequestVerdict.Accepted)
            {
                operationsRefused++;
                PrivateNote($"Refused {op.Kind} from {actor}: {verdict} (assist {PlayAssist.Level}); host table {(before == HostTableFingerprint() ? "unchanged" : "CHANGED")}, revision {(privateHost.Revision == revisionBefore ? "unchanged" : "CHANGED")} ({privateHost.Revision}).");
                return verdict;
            }

            HostSceneState sceneBefore = CaptureHostScene();
            bool done = false;
            Dictionary<PlayerId, byte[]> updates = privateHost.Apply(() =>
            {
                bool passing = privateInterceptor.PassThrough;
                privateInterceptor.PassThrough = true;
                try
                {
                    done = runOwnCommand != null ? runOwnCommand() : ExecuteOperation(sender, resolved);
                }
                finally
                {
                    privateInterceptor.PassThrough = passing;
                }
            }, null, (Func<PrivateHudSnapshot>)CurrentHud);
            HostSceneFollow(sceneBefore);
            SendPublish(updates, op.Kind + " update");
            if (!done)
            {
                operationsRefused++;
                PrivateNote($"Refused {op.Kind} from {actor}: the table's own rules refused it (RuleRefused); revision {revisionBefore} → {privateHost.Revision}.");
                return PrivateRequestVerdict.RuleRefused;
            }

            operationsAccepted++;
            if (member.IsHost) hostOwnAccepted++;
            PrivateNote($"Accepted {op.Kind} from {actor} (assist {PlayAssist.Level}); revision {revisionBefore} → {privateHost.Revision}; each guest computer here got its own change set.");
            return PrivateRequestVerdict.Accepted;
        }

        // The table's own command for a checked operation, with the real ids the check resolved.
        private bool ExecuteOperation(PlayerId sender, PrivateResolvedOperation resolved)
        {
            PrivateOperation op = resolved.Operation;
            switch (op.Kind)
            {
                case PrivateOperationKind.Transfer:
                    return host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(op.HasPose
                        ? TransferCardCommand.ToTabletop(Ctx(sender), resolved.RealCard, op.Container, op.Pose)
                        : TransferCardCommand.ToContainer(Ctx(sender), resolved.RealCard, op.Container, op.Destination)).Succeeded;
                case PrivateOperationKind.Flip:
                    return host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(Ctx(sender), resolved.RealCard, op.FaceUp ? CardFace.FaceUp : CardFace.FaceDown)).Succeeded;
                case PrivateOperationKind.Shuffle:
                    // The host's own random source; neither the seed nor the order leaves this computer.
                    return host.TableAuthority.Submit<ShuffleDeckCommand, ShuffleDeckResult>(new ShuffleDeckCommand(Ctx(sender), op.Container)).Succeeded;
                case PrivateOperationKind.Reorder:
                    return host.TableAuthority.Submit<ReorderContainerCommand, ReorderContainerResult>(new ReorderContainerCommand(Ctx(sender), op.Container, resolved.RealCard, resolved.FromIndex, op.B)).Succeeded;
                case PrivateOperationKind.DrawCount:
                    return host.TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(new DrawCardsCommand(Ctx(sender), op.Container, op.Destination, op.A)).Succeeded;
                default:
                    return false;
            }
        }

        // PRIVACY-P6B: Undo on the private path. Only the authority's own history; only the sender's own latest
        // change (HostUndoPolicy, as for a joined computer in normal play); in Full a rules result stands; a request
        // made before something else happened (another revision) is stale; a request id runs once. The table that
        // results reaches every guest as its own change set (or its own package where a container came back).
        private PrivateRequestVerdict RunPrivateUndo(Member member, PrivateOperation op)
        {
            string actor = member.IsHost ? "the host's own seat" : member.Name;
            PlayerId sender = member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            if (!new RoomSeats(this).IsSeated(sender)) return PrivateRequestVerdict.NotSeated;
            ActiveSessionUndoTransaction next = host.undoHistory.NextUndo;
            bool rulesResult = false;
            if (next != null && host.undoHistory.TryPeekUndo(out SessionUndoSnapshot before, out _))
                rulesResult = host.TrapFloorGame.IsRulesResultStep(before.GameState, host.undoHistory.CurrentState.GameState, next.CommandId);
            if (op.Revision != privateHost.Revision)
            {
                operationsRefused++;
                undoPolicy.Check(sender, op.RequestId, CommandId.Empty, false, CommandId.Empty, PlayerId.Empty); // the id is used up
                PrivateNote($"Refused Undo from {actor}: stale (its table shows revision {op.Revision}, the host's is {privateHost.Revision}); nothing taken back.");
                return PrivateRequestVerdict.StaleRevision;
            }

            HostUndoVerdict verdict = undoPolicy.Check(sender, op.RequestId, next != null ? next.CommandId : CommandId.Empty, next != null,
                next != null ? next.CommandId : CommandId.Empty, next != null ? next.ActorPlayerId : PlayerId.Empty, rulesResult);
            if (verdict != HostUndoVerdict.Allowed)
            {
                operationsRefused++;
                PrivateNote($"Refused Undo from {actor}: {verdict}; nothing taken back (revision {privateHost.Revision}).");
                switch (verdict)
                {
                    case HostUndoVerdict.Duplicate: return PrivateRequestVerdict.Duplicate;
                    case HostUndoVerdict.NotYourChange:
                    case HostUndoVerdict.NoPlayer: return PrivateRequestVerdict.NotYourChange;
                    case HostUndoVerdict.RulesResult: return PrivateRequestVerdict.RuleRefused;
                    default: return PrivateRequestVerdict.NothingToUndo;
                }
            }

            long revisionBefore = privateHost.Revision;
            undoRequestPlayer = member.Player;
            bool done;
            try
            {
                done = host.SessionService.UndoLatestAction(sender);
            }
            finally
            {
                undoRequestPlayer = Guid.Empty;
            }

            if (!done) { operationsRefused++; PrivateNote($"Undo from {actor}: the table did not take it back."); return PrivateRequestVerdict.NothingToUndo; }
            PrivateTableReplaced($"Undo of {next.Description} for {actor}");
            operationsAccepted++;
            PrivateNote($"Accepted Undo from {actor}: revision {revisionBefore} → {privateHost.Revision}.");
            return PrivateRequestVerdict.Accepted;
        }

        /// <summary>
        /// PRIVACY-P6B: the host's own table gesture (a card drag, a draw, a flip, a shuffle, a hand reorder) as the same
        /// operation a guest sends, named by the host's own references; the gesture's own command then runs.
        /// </summary>
        internal bool TryRunHostOwnCommand<TCommand, TResult>(TCommand command, out TResult result) where TCommand : class
        {
            result = default;
            Member me = HostMember();
            if (me == null || privateHost == null) return false;
            PlayerId self = new PlayerId(me.Player);
            PrivateOperation op = new PrivateOperation { RequestId = Guid.NewGuid() };
            switch (command)
            {
                case TransferCardCommand t:
                    op.Kind = PrivateOperationKind.Transfer;
                    op.Card = privateHost.ReferenceFor(self, t.CardObjectId);
                    op.Container = t.ExpectedSourceContainerId;
                    op.Destination = t.DestinationContainerId;
                    op.HasPose = t.TargetTablePose.HasValue;
                    if (op.HasPose) op.Pose = t.TargetTablePose.Value;
                    break;
                case DrawCardsCommand d:
                    op.Kind = PrivateOperationKind.DrawCount;
                    op.Container = d.SourceDeckContainerId;
                    op.Destination = d.DestinationContainerId;
                    op.A = d.Count;
                    break;
                case FlipCardCommand f:
                    op.Kind = PrivateOperationKind.Flip;
                    op.Card = privateHost.ReferenceFor(self, f.ObjectId);
                    op.FaceUp = f.TargetFace == CardFace.FaceUp;
                    break;
                case ShuffleDeckCommand sh:
                    op.Kind = PrivateOperationKind.Shuffle;
                    op.Container = sh.DeckContainerId;
                    break;
                case ReorderContainerCommand ro:
                    op.Kind = PrivateOperationKind.Reorder;
                    op.Container = ro.ContainerId;
                    op.Card = privateHost.ReferenceFor(self, ro.ObjectId);
                    op.A = ro.FromIndex;
                    op.B = ro.ToIndex;
                    break;
                default:
                    return false;
            }

            TResult captured = default;
            PrivateRequestVerdict verdict = RunPrivateOperation(me, op, () =>
            {
                captured = host.TableAuthority.Submit<TCommand, TResult>(command);
                return ResultSucceeded(captured);
            }, out _);
            if (verdict != PrivateRequestVerdict.Accepted && verdict != PrivateRequestVerdict.RuleRefused)
                host.ShowMessage($"Private session: {op.Kind} refused ({verdict}).");
            result = captured;
            return true;
        }

        private static bool ResultSucceeded(object result)
        {
            switch (result)
            {
                case TransferCardResult r: return r.Succeeded;
                case DrawCardsResult r: return r.Succeeded;
                case FlipCardResult r: return r.Succeeded;
                case ShuffleDeckResult r: return r.Succeeded;
                case ReorderContainerResult r: return r.Succeeded;
                default: return false;
            }
        }

        /// <summary>PRIVACY-P6A: the host's own End Turn button (the game's AdvanceTrapFloorTurn asks here first) goes through the same check.</summary>
        internal bool TryRunHostOwnPrivateEndTurn(TrapFloorEndTurnRequest request)
        {
            if (!privateHosting || !hosting || privateHost == null || privateEndTurnRunning) return false;
            byte[] bytes = PrivateRequestCodec.EndTurn(new PrivateEndTurnRequest
            {
                RequestId = request.RequestId, Round = request.Round, Phase = (int)request.Phase, TurnOwner = request.TurnOwner, Skipped = request.Skipped
            });
            lastEndTurnBytes = bytes;
            RunHostOwnRequest(bytes, "End Turn");
            return true;
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
                case MsgPrivateTurn:
                    GuestTurn(payload);
                    break;
                case MsgPrivateTurnVerdict:
                    GuestTurnVerdict(payload);
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
            privateRevision = package.Revision; // PRIVACY-P6A: this table now shows the host's revision
            privateTurn = package.Turn;
            privateHud = package.Hud;
            // PRIVACY-P6B: this computer's own table gestures go to the host as private requests (never run here).
            host.PrivateViewAuthority.Forward = GuestForward;
            RenderPrivateHud();
            PrivateNote($"Received package {packagesReceived} (revision {package.Revision}; turn {TurnText(privateTurn)}): {bytes.Length} bytes, sha256 {Sha(bytes)}; {client.Cards.Count} cards ({hidden} as backs); "
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

            // PRIVACY-P6A: ordering. An update this table already shows (or older) is ignored; one after a gap means an
            // update was missed: the table asks for its own package (recovery, counted).
            PrivateRevisionStep step = PrivateRevisionGate.Next(privateRevision, update.Revision);
            if (step == PrivateRevisionStep.AlreadyShown)
            {
                staleIgnored++;
                PrivateNote($"Update {updatesReceived} (revision {update.Revision}) ignored: this table already shows revision {privateRevision}.");
                return;
            }

            if (step == PrivateRevisionStep.Gap)
            {
                revisionGaps++;
                PrivateNote($"Update {updatesReceived} has revision {update.Revision} but this table shows {privateRevision}: one was missed. Asking for this computer's package.");
                AskForOwnPackage();
                return;
            }

            if (update.Changes != null)
            {
                GuestChangeSet(update, bytes);
                return;
            }

            string problem = RecipientEventApplier.Apply(host.matchState, update);
            if (problem != null)
            {
                digestMismatches++;
                PrivateNote($"Update {updatesReceived} ({bytes.Length} bytes, revision {update.Revision}) did not apply: {problem}. Asking the host for this computer's package (never a full table).");
                AskForOwnPackage();
                return;
            }

            privateRevision = update.Revision;

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
            PrivateNote($"Update {updatesReceived} (revision {update.Revision}): {bytes.Length} bytes, sha256 {Sha(bytes)}; a card moved {(identity ? "WITH its identity (" + CardName(ev.Definition) + ")" : "as a back")} "
                + $"into a {kind} now holding {ev.ToCount}{(ev.PreviousReference.Equals(ev.Reference) ? "" : " (new reference here)")}; digest matches the host's; own hand {OwnHandSummary()}.");
        }

        // PRIVACY-P6B: one change set (any number of cards, the turn, the status card) on this computer's own table,
        // then on its scene: only the changed cards' views and their containers.
        private void GuestChangeSet(PrivateUpdate update, byte[] bytes)
        {
            PrivateChangeSet set = update.Changes;
            if (set.NeedsPackage)
            {
                PrivateNote($"Update {updatesReceived} (revision {update.Revision}) says this computer's package follows ({set.PackageReason}).");
                return; // the host sends the package itself
            }

            PrivateChangeSummary summary = new PrivateChangeSummary();
            string problem = RecipientEventApplier.Apply(host.matchState, update, summary);
            if (problem != null)
            {
                digestMismatches++;
                PrivateNote($"Change set {updatesReceived} ({bytes.Length} bytes, revision {update.Revision}) did not apply: {problem}. Asking the host for this computer's package (never a full table).");
                AskForOwnPackage();
                return;
            }

            privateRevision = update.Revision;
            changeSetsApplied++;
            if (summary.Withdrawn.Count > 0) lastWithdrawnReference = summary.Withdrawn[0];
            if (set.Turn != null) privateTurn = set.Turn;
            if (set.Hud != null) privateHud = set.Hud;
            if (set.Turn != null || set.Hud != null) RenderPrivateHud();
            bool objects = summary.Released.Count + summary.Created.Count + summary.Moved.Count + summary.Containers.Count > 0;
            if (objects)
            {
                PendingSceneMeasure measure = BeginSceneMeasure(IncrementalScene ? $"incremental change set ({summary.Created.Count} card views made)" : "full scene rebuild");
                string error;
                bool shown = IncrementalScene ? host.ApplyPrivateChangesToScene(summary, out error) : host.RebuildPrivateSceneFromOwnTable(out error);
                if (!shown && IncrementalScene)
                {
                    PrivateNote("The change set could not be shown incrementally (" + error + "): building the scene again from this computer's own table.");
                    measure.What = "full scene rebuild (after a failed incremental change set)";
                    shown = host.RebuildPrivateSceneFromOwnTable(out error);
                    fullBuilds++;
                }
                else if (IncrementalScene) incrementalUpdates++;
                else fullBuilds++;
                EndSceneWork(measure);
                if (!shown) { PrivateNote("The scene could not follow the change set: " + error); return; }
            }

            int backs = 0, faces = 0;
            foreach (TabletopObjectId id in summary.Created)
                if (host.matchState.Cards.TryGetValue(id, out CardInstanceState card))
                {
                    if (card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) backs++;
                    else faces++;
                }

            PrivateNote($"Change set {updatesReceived} (revision {update.Revision}): {bytes.Length} bytes, sha256 {Sha(bytes)}; {summary.Released.Count} card view(s) taken down, "
                + $"{summary.Created.Count} made ({faces} with their faces, {backs} as backs), {summary.Moved.Count} piece(s) moved, {set.Orders.Count} order(s), {summary.Containers.Count} container(s) laid out"
                + $"{(set.Turn != null ? "; turn: " + TurnText(privateTurn) : string.Empty)}; digest matches the host's; own hand {OwnHandSummary()}.");
            if (set.Turn != null) host.ShowMessage("Private session: " + TurnText(privateTurn) + ".");
        }

        // PRIVACY-P6B: a table gesture made on this guest (its own references) goes to the host as a private request.
        // True: sent (nothing runs here; the host's change set moves the cards). False: no private request for it.
        private bool GuestForward(object command)
        {
            if (!privateGuest) return false;
            PrivateOperation op = new PrivateOperation { RequestId = Guid.NewGuid() };
            string what;
            switch (command)
            {
                case TransferCardCommand t:
                    op.Kind = PrivateOperationKind.Transfer;
                    op.Card = t.CardObjectId;
                    op.Container = t.ExpectedSourceContainerId;
                    op.Destination = t.DestinationContainerId;
                    op.HasPose = t.TargetTablePose.HasValue;
                    if (op.HasPose) op.Pose = t.TargetTablePose.Value;
                    what = "card move (table gesture)";
                    break;
                case DrawCardsCommand d:
                    op.Kind = PrivateOperationKind.DrawCount;
                    op.Container = d.SourceDeckContainerId;
                    op.Destination = d.DestinationContainerId;
                    op.A = d.Count;
                    what = $"draw {d.Count} (table gesture)";
                    break;
                case FlipCardCommand f:
                    op.Kind = PrivateOperationKind.Flip;
                    op.Card = f.ObjectId;
                    op.FaceUp = f.TargetFace == CardFace.FaceUp;
                    what = "flip (table gesture)";
                    break;
                case ShuffleDeckCommand sh when !sh.CutOnly:
                    op.Kind = PrivateOperationKind.Shuffle;
                    op.Container = sh.DeckContainerId;
                    what = "shuffle (table gesture)";
                    break;
                case ReorderContainerCommand ro:
                    op.Kind = PrivateOperationKind.Reorder;
                    op.Container = ro.ContainerId;
                    op.Card = ro.ObjectId;
                    op.A = ro.FromIndex;
                    op.B = ro.ToIndex;
                    what = "hand reorder (table gesture)";
                    break;
                default:
                    return false;
            }

            forwardedCommands++;
            SendPrivateRequest(PrivateOperationCodec.Write(op), what);
            return true;
        }

        // PRIVACY-P6B: the status card the host sent, on this guest's own HUD (with its End Turn and Undo).
        private void RenderPrivateHud()
        {
            if (!privateGuest || privateHud == null || host.runtimeUi == null) return;
            try
            {
                List<TrapFloorHudPlayer> players = new List<TrapFloorHudPlayer>();
                foreach (PrivateHudPlayer p in privateHud.Players) players.Add(new TrapFloorHudPlayer(p.Name, new Color(p.R, p.G, p.B, p.A), p.Active));
                TrapFloorPlayerCardModel card = privateHud.HasCard
                    ? new TrapFloorPlayerCardModel(privateHud.CardTitle, privateHud.CardAvatar, privateHud.CardKeys, privateHud.CardStatus, privateHud.CardInHand, privateHud.CardFloor)
                    : null;
                TrapFloorStatusModel status = new TrapFloorStatusModel(
                    privateHud.Round, privateHud.Phase, string.Empty, string.Empty, string.Empty, string.Empty,
                    privateHud.HasTurnColor ? new Color(privateHud.TurnR, privateHud.TurnG, privateHud.TurnB) : (Color?)null,
                    players, card);
                bool mine = privateTurn != null && !privateTurn.Ended && privateTurn.TurnOwner == myPlayer;
                List<PopupActionOption> actions = new List<PopupActionOption>
                {
                    new PopupActionOption(privateTurn != null && privateTurn.Phase == (int)TrapFloorTurnPhase.FloorTurn ? "End Floor Turn" : "End Turn", mine, () => PrivateEndTurn()),
                    new PopupActionOption("Undo my last", true, PrivateUndo),
                };
                host.runtimeUi.ShowTrapFloorStatus(status, new FloorfallStatusModel(false, string.Empty, string.Empty, string.Empty), actions);
                nextHudRenderAt = Time.unscaledTime + 1f;
            }
            catch (Exception exception)
            {
                PrivateNote("The private status card could not be shown: " + exception.Message);
                privateHud = null;
            }
        }

        /// <summary>PRIVACY-P6B: the table's own status refresh on a private guest shows the host's public HUD (never clears it).</summary>
        internal void ShowPrivateGuestHud() => RenderPrivateHud();

        // The table's own status refresh may clear the card; it is shown again once a second while the session runs.
        private void TickPrivateGuestHud()
        {
            if (!privateGuest || privateHud == null || Time.unscaledTime < nextHudRenderAt) return;
            RenderPrivateHud();
        }

        /// <summary>PRIVACY-P6B: Undo this seat's own latest change (the host decides; nothing is taken back here).</summary>
        internal void PrivateUndo()
        {
            if (!InPrivateSession) return;
            long shown = privateHosting ? (privateHost?.Revision ?? 0) : privateRevision;
            SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation { Kind = PrivateOperationKind.Undo, RequestId = Guid.NewGuid(), Revision = shown }), $"Undo my last change (table shows revision {shown})");
        }

        internal void PrivateUndoStale()
        {
            if (!InPrivateSession) return;
            long shown = (privateHosting ? (privateHost?.Revision ?? 0) : privateRevision) - 1;
            SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation { Kind = PrivateOperationKind.Undo, RequestId = Guid.NewGuid(), Revision = Math.Max(0, shown) }), "Undo against an OLDER revision (expect StaleRevision)");
        }

        // PRIVACY-P6B dev controls: flip the first card of this seat's Console (else of its hand); shuffle a deck this
        // seat may use; move the first hand card to the end of the hand; draw three.
        // PRIVACY-P6B: the host's own panel names a card the way a guest would: by the host seat's own reference for it
        // (a hidden card's real id is refused for every seat, the host's own included). A guest's table holds its
        // references already.
        private TabletopObjectId PanelRef(TabletopObjectId card)
        {
            if (!privateHosting || privateHost == null || card.IsEmpty) return card;
            Member me = HostMember();
            return me == null ? card : privateHost.ReferenceFor(new PlayerId(me.Player), card);
        }

        internal void PrivateFlip()
        {
            TabletopObjectId card = FirstOwnConsoleCard();
            RecipientSeatLayout seat = OwnSeat();
            if (card.IsEmpty && seat != null && host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) && hand.Count > 0) card = hand.GetObjectAt(0);
            if (card.IsEmpty || !host.matchState.Cards.TryGetValue(card, out CardInstanceState state)) { PrivateNote("Flip: no card of yours."); return; }
            SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation
            {
                Kind = PrivateOperationKind.Flip, RequestId = Guid.NewGuid(), Card = PanelRef(card), FaceUp = state.Face != CardFace.FaceUp
            }), state.Face == CardFace.FaceUp ? "Flip face down" : "Flip face up");
        }

        internal void PrivateShuffle(bool othersDeck = false)
        {
            RecipientSeatLayout seat = OwnSeat();
            ContainerId deck = ContainerId.Empty;
            if (!othersDeck && seat != null) deck = seat.ControllerDeck;
            if (othersDeck)
            {
                RecipientPublicLayout layout = privateHosting ? privateHost?.Layout : host.privateViewLayout;
                if (layout != null) foreach (RecipientSeatLayout other in layout.Seats) if (seat == null || other.Seat != seat.Seat) { deck = other.ControllerDeck; break; }
            }

            if (deck.IsEmpty) { PrivateNote("Shuffle: no deck."); return; }
            SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation { Kind = PrivateOperationKind.Shuffle, RequestId = Guid.NewGuid(), Container = deck }),
                othersDeck ? "Shuffle ANOTHER seat's deck (expect WrongSeat unless it is shared)" : "Shuffle my deck");
        }

        internal void PrivateReorder()
        {
            RecipientSeatLayout seat = OwnSeat();
            if (seat == null || !host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) || hand.Count < 2) { PrivateNote("Reorder: fewer than 2 cards in hand."); return; }
            SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation
            {
                Kind = PrivateOperationKind.Reorder, RequestId = Guid.NewGuid(), Container = seat.Hand, Card = PanelRef(hand.GetObjectAt(0)), A = 0, B = hand.Count - 1
            }), "Move my first hand card to the end");
        }

        internal void PrivateDrawThree()
        {
            RecipientSeatLayout seat = OwnSeat();
            if (seat == null) { PrivateNote("Draw 3: no seat."); return; }
            SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation
            {
                Kind = PrivateOperationKind.DrawCount, RequestId = Guid.NewGuid(), Container = seat.ControllerDeck, Destination = seat.Hand, A = 3
            }), "Draw 3");
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

        private void GuestTurn(byte[] bytes)
        {
            turnUpdatesReceived++;
            SaveReceived("turn", bytes);
            PrivateTurnUpdate update;
            try
            {
                update = (PrivateTurnUpdate)ConsoleCards.Application.Authority.TableWire.Read(bytes, 0, bytes.Length, false);
            }
            catch (Exception exception)
            {
                PrivateNote("A turn update could not be read: " + exception.Message);
                AskForOwnPackage();
                return;
            }

            PrivateRevisionStep step = PrivateRevisionGate.Next(privateRevision, update.Revision);
            if (step == PrivateRevisionStep.AlreadyShown)
            {
                staleIgnored++;
                PrivateNote($"Turn update (revision {update.Revision}) ignored: this table already shows revision {privateRevision}.");
                return;
            }

            if (step == PrivateRevisionStep.Gap || update.Turn == null)
            {
                revisionGaps++;
                PrivateNote($"Turn update has revision {update.Revision} but this table shows {privateRevision}: asking for this computer's package.");
                AskForOwnPackage();
                return;
            }

            privateRevision = update.Revision;
            privateTurn = update.Turn;
            PrivateNote($"Turn update (revision {update.Revision}, {bytes.Length} bytes, sha256 {Sha(bytes)}): now {TurnText(privateTurn)}.");
            host.ShowMessage("Private session: " + TurnText(privateTurn) + ".");
        }

        private void GuestTurnVerdict(byte[] bytes)
        {
            if (bytes.Length < 17) return;
            byte[] id = new byte[16];
            Array.Copy(bytes, 0, id, 0, 16);
            TrapFloorEndTurnVerdict verdict = (TrapFloorEndTurnVerdict)bytes[16];
            PrivateNote($"Host's answer to End Turn {new Guid(id).ToString().Substring(0, 8)}: {verdict}.");
            host.ShowMessage($"Host: End Turn {verdict}.");
        }

        private string TurnText(PrivateTurnSnapshot turn)
        {
            if (turn == null) return "no turn known";
            if (turn.Ended) return $"round {turn.Round}, the floor has ended";
            return $"round {turn.Round}, {(TrapFloorTurnPhase)turn.Phase}, turn of {SeatName(new PlayerId(turn.TurnOwner))}";
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
            RecipientPublicLayout layout = privateHosting ? privateHost?.Layout : host.privateViewLayout;
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
                return $"HOST · revision {privateHost?.Revision ?? 0} · packages sent {packagesSent}, updates sent {updatesSent}, resync requests {resyncRequests}, legacy refused {legacyRefused}\n"
                    + $"own actions accepted {hostOwnAccepted}, refused {hostOwnRefused}; turn transitions {turnTransitions}; operations accepted {operationsAccepted}, refused {operationsRefused}; sweeps {sweeps}; fallback packages {fallbackPackages}; "
                    + $"commands not private yet {privateInterceptor?.RefusedSummary() ?? "none"} · {TurnText(privateHost?.Turn)} · hand {OwnHandSummary()}";
            if (privateGuest)
                return $"GUEST · revision {privateRevision} · packages {packagesReceived}, updates {updatesReceived}, turn updates {turnUpdatesReceived}, digest mismatches {digestMismatches}, gaps {revisionGaps}, stale ignored {staleIgnored}, legacy dropped {legacyRefused}\n"
                    + $"{TurnText(privateTurn)} · hand {OwnHandSummary()} · scene: {(IncrementalScene ? "incremental" : "FULL REBUILD (measuring)")} · full builds {fullBuilds}, incremental updates {incrementalUpdates}, change sets {changeSetsApplied}, gestures sent {forwardedCommands}";
            return InRoom ? "In a room (private development session ON): waiting for the host's table" : "Private development session ON: host or join a room";
        }

        internal void PrivateDraw()
        {
            RecipientSeatLayout seat = OwnSeat();
            if (!InPrivateSession || seat == null) { PrivateNote("Draw: no seat of yours in the private table."); return; }
            SendPrivateRequest(PrivateRequestCodec.Draw(new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = seat.ControllerDeck }), "Draw");
        }

        internal void PrivatePlace() => PlaceInto(OwnFreeShownSlot(), false, "Place face up");

        // PRIVACY-P5: the first card on this seat's Console back into its hand.
        internal void PrivateReturn()
        {
            TabletopObjectId card = FirstOwnConsoleCard();
            if (!InPrivateSession || card.IsEmpty) { PrivateNote("Return: no card on your Console."); return; }
            SendPrivateRequest(PrivateRequestCodec.Card(PrivateRequestCodec.KindReturn, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = PanelRef(card) }), "Return to hand");
        }

        // PRIVACY-P5: the first card in this seat's hand (else on its Console) onto the first public discard pile.
        internal void PrivateDiscard()
        {
            RecipientSeatLayout seat = OwnSeat();
            TabletopObjectId card = TabletopObjectId.Empty;
            if (seat != null && host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) && hand.Count > 0) card = hand.GetObjectAt(0);
            if (card.IsEmpty) card = FirstOwnConsoleCard();
            ContainerId pile = PublicDiscardPile();
            if (!InPrivateSession || card.IsEmpty || pile.IsEmpty) { PrivateNote("Discard: no card of yours, or no public discard pile."); return; }
            SendPrivateRequest(PrivateRequestCodec.Card(PrivateRequestCodec.KindDiscard, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = PanelRef(card), Destination = pile }), "Discard");
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
            SendPrivateRequest(PrivateRequestCodec.Card(PrivateRequestCodec.KindDiscard, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = PanelRef(hand.GetObjectAt(0)), Destination = slot }),
                "Discard into a Console slot, not a discard pile (expect InvalidDestination)");
        }

        // PRIVACY-P6A: End Turn for the turn this computer believes it is (the guest's last turn update or package; the
        // host's own turn state). staleRound: one round earlier (expect StaleTurn, or NotTurnOwner when not yours).
        internal void PrivateEndTurn(bool staleRound = false)
        {
            PrivateTurnSnapshot turn = privateHosting ? CurrentTurnSnapshot() : privateTurn;
            if (!InPrivateSession || turn == null) { PrivateNote("End Turn: no turn known here yet."); return; }
            byte[] bytes = PrivateRequestCodec.EndTurn(new PrivateEndTurnRequest
            {
                RequestId = Guid.NewGuid(),
                Round = staleRound ? turn.Round - 1 : turn.Round,
                Phase = turn.Phase,
                TurnOwner = new PlayerId(turn.TurnOwner == Guid.Empty ? Guid.NewGuid() : turn.TurnOwner),
                Skipped = false
            });
            lastEndTurnBytes = bytes;
            SendPrivateRequest(bytes, staleRound ? "End Turn for an EARLIER round (expect StaleTurn or NotTurnOwner)" : $"End Turn ({TurnText(turn)})");
        }

        internal void PrivateResendEndTurn()
        {
            if (lastEndTurnBytes == null) { PrivateNote("No End Turn to send again."); return; }
            SendPrivateRequest(lastEndTurnBytes, "the last End Turn again, same request id (expect Duplicate)");
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
            if (privateHosting) { RunHostOwnRequest(lastRequestBytes, "the last request again, same request id (expect Duplicate)"); return; }
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
            if (!InPrivateSession || seat == null || !host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) || hand.Count == 0)
            {
                PrivateNote(what + ": no card in your hand.");
                return;
            }

            if (slot.IsEmpty) { PrivateNote(what + ": no slot."); return; }
            TabletopObjectId card = PanelRef(hand.GetObjectAt(0)); // the card is named by this computer's own reference for it
            lastPlacedReference = card;
            SendPrivateRequest(PrivateRequestCodec.Place(new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = card, Destination = slot }), what);
        }

        private void SendPrivateRequest(byte[] request, string what)
        {
            if (request[0] != PrivateRequestCodec.KindEndTurn) lastRequestBytes = request;
            lastRequestId = new Guid(new ReadOnlySpan<byte>(request, 1, 16));
            if (privateHosting)
            {
                // PRIVACY-P6A: the host's own panel goes through the same pipeline as a guest's request.
                PrivateNote($"Host's own {what} (request {lastRequestId.ToString().Substring(0, 8)}).");
                if (PrivateOperationCodec.IsOperation(request, 0))
                {
                    Member me = HostMember();
                    PrivateRequestVerdict v = me != null && PrivateOperationCodec.TryRead(request, 0, out PrivateOperation op) ? RunPrivateOperation(me, op, null, out _) : PrivateRequestVerdict.UnknownReference;
                    if (v != PrivateRequestVerdict.Accepted) host.ShowMessage($"Private session: {what} refused ({v}).");
                    return;
                }

                RunHostOwnRequest(request, what);
                return;
            }

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
            if (host.Replicated != null && ReferenceEquals(host.Replicated.Interceptor, privateInterceptor)) host.Replicated.Interceptor = null;
            privateRevision = -1;
            privateTurn = null;
            privateHud = null;
            host.PrivateViewAuthority.Forward = null;
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
        internal bool TryRunHostOwnPrivateEndTurn(TrapFloorEndTurnRequest request) => false;
        internal void PrivateUndo() { }
        internal void ShowPrivateGuestHud() { }
        private bool MayUndoOnPrivateHost(ActiveSessionUndoTransaction transaction, bool redo) => true;
        private int legacyRefused;
        private void PrivateNote(string line) { }
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// PRIVACY-P6A (s150, Task 1): on the host of a private development session, every command made on this computer
    /// meets this first (ReplicatedTableAuthority.Interceptor). The host's own card actions are not run on its table
    /// directly: a card drag or draw becomes the same private request a guest sends, through the same pipeline
    /// (OnlinePlayPresenter.RunPrivateCardRequest), so its seat gets no exception and every guest gets its update.
    /// Commands that only move or turn a piece on the table (no container, face or identity change) still run on the
    /// host's own table (their sync is the later physics milestone). Every other table command (shuffle, flip, split,
    /// create, delete, a draw into another container...) has no private request yet and is refused, never run silently
    /// on the host's table alone. While the host runs an accepted End Turn through the game's own turn service the
    /// game's own commands pass (PassThrough); whatever they change in cards is then sent as packages (counted).
    /// </summary>
    internal sealed class PrivateHostInterceptor : ITableCommandInterceptor
    {
        // By type name: these only set a piece's place, turn or lock on the table (no container, face or identity).
        private static readonly HashSet<string> PoseOnly = new HashSet<string>
        {
            "MoveObjectCommand",
            "CommitPhysicalObjectCommand",
            "RotateObjectCommand",
            "MoveContainerCommand",
            "MoveBoardCommand",
            "SetObjectLockedCommand",
        };

        private readonly OnlinePlayPresenter online;
        private readonly Dictionary<Type, int> refused = new Dictionary<Type, int>();
        private readonly Dictionary<Type, object> notRun = new Dictionary<Type, object>();
        internal bool PassThrough;

        internal PrivateHostInterceptor(OnlinePlayPresenter online) => this.online = online;

        internal string RefusedSummary()
        {
            if (refused.Count == 0) return "none";
            List<string> parts = new List<string>();
            foreach (KeyValuePair<Type, int> pair in refused) parts.Add(pair.Key.Name + " " + pair.Value);
            return string.Join(", ", parts);
        }

        public bool TrySubmit<TCommand, TResult>(TCommand command, out TResult result) where TCommand : class
        {
            result = default;
            if (PassThrough || command == null) return false;
            // PRIVACY-P6B: card and deck gestures (drag, draw, flip, shuffle, hand reorder) go through the private
            // pipeline as the host's own seat; the gesture's own command then runs on the authority.
            if (online.TryRunHostOwnCommand(command, out result)) return true;

            if (PoseOnly.Contains(typeof(TCommand).Name)) return false;
            int seen = refused.TryGetValue(typeof(TCommand), out int count) ? count : 0;
            refused[typeof(TCommand)] = seen + 1;
            if (seen == 0) Debug.LogWarning($"[PrivateSession] Refused the host's own {typeof(TCommand).Name}: no private request for it yet; it is not run on the host's table alone.");
            if (!typeof(TResult).IsValueType)
            {
                if (!notRun.TryGetValue(typeof(TResult), out object empty))
                {
                    empty = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TResult));
                    notRun.Add(typeof(TResult), empty);
                }

                result = (TResult)empty;
            }

            return true;
        }
    }

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

        // PRIVACY-P6B: the general operations, the same row on host and guests.
        private void DrawOperationRow()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Flip")) Online.PrivateFlip();
            if (GUILayout.Button("Shuffle my deck")) Online.PrivateShuffle();
            if (GUILayout.Button("Hand: first→last")) Online.PrivateReorder();
            if (GUILayout.Button("Draw 3")) Online.PrivateDrawThree();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Undo my last")) Online.PrivateUndo();
            if (GUILayout.Button("Bad: stale Undo")) Online.PrivateUndoStale();
            if (GUILayout.Button("Bad: other's deck")) Online.PrivateShuffle(true);
            GUILayout.EndHorizontal();
        }

        private void OnGUI()
        {
            if (Online == null || !PrivateDevSession.Enabled) return;
            const float width = 560f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 12f, 12f, width, folded ? 80f : 560f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("PRIVATE DEV SESSION · development only · not normal online play");
            if (GUILayout.Button(folded ? "Show" : "Hide", GUILayout.Width(60f))) folded = !folded;
            GUILayout.EndHorizontal();
            GUILayout.Label(Online.PrivateStatus());
            if (!folded)
            {
                if (Online.IsPrivateHost)
                {
                    // PRIVACY-P6A: the host's own seat, through the same pipeline as a guest's request.
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Draw")) Online.PrivateDraw();
                    if (GUILayout.Button("Place face up")) Online.PrivatePlace();
                    if (GUILayout.Button("Return to hand")) Online.PrivateReturn();
                    if (GUILayout.Button("Discard")) Online.PrivateDiscard();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("End Turn")) Online.PrivateEndTurn();
                    if (GUILayout.Button("Bad: End Turn again")) Online.PrivateResendEndTurn();
                    if (GUILayout.Button("Bad: stale End Turn")) Online.PrivateEndTurn(true);
                    if (GUILayout.Button("Bad: repeat last")) Online.PrivateResendLast();
                    GUILayout.EndHorizontal();
                    DrawOperationRow();
                }

                if (Online.IsPrivateGuest)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Draw")) Online.PrivateDraw();
                    if (GUILayout.Button("Place face up")) Online.PrivatePlace();
                    if (GUILayout.Button("Return to hand")) Online.PrivateReturn();
                    if (GUILayout.Button("Discard")) Online.PrivateDiscard();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("End Turn")) Online.PrivateEndTurn();
                    if (GUILayout.Button("Bad: End Turn again")) Online.PrivateResendEndTurn();
                    if (GUILayout.Button("Bad: stale End Turn")) Online.PrivateEndTurn(true);
                    GUILayout.EndHorizontal();
                    DrawOperationRow();
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
