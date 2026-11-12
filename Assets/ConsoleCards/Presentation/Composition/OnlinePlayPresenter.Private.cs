using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Events;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.GameTemplates.Privacy;
using ConsoleCards.Presentation.Interaction;
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

        // P8A (owner, 2026-11-08: "I need to test the original build, not the test build"): the developer test panel can
        // be switched off while the private session keeps working. Editor: Console Cards › Debug › Privacy › Hide test
        // panel (kept between Play runs). Guest build: a file named PanelHiddenFileName next to the game (the guest build
        // menu writes it when the Editor setting is on).
        public const string PanelHiddenFileName = "hide-dev-panel.txt";
        public const string PanelHiddenPrefKey = "ConsoleCards.HideDevPanel";
#if !UNITY_EDITOR
        private static int panelHiddenFlag = -1;
#endif

        public static bool PanelHidden
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(PanelHiddenPrefKey, false);
#else
                if (panelHiddenFlag < 0)
                {
                    try { panelHiddenFlag = File.Exists(Path.Combine(UnityEngine.Application.dataPath, "..", PanelHiddenFileName)) ? 1 : 0; }
                    catch (Exception) { panelHiddenFlag = 0; }
                }

                return panelHiddenFlag == 1;
#endif
            }
        }
#else
        public static bool Enabled => false;
        public static bool PanelHidden => true;
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
        private const byte MsgPrivateShop = 48;       // PRIVACY-P6C host → guest: the Shop's public list (game data, as the Shop shows every player)
        private const byte MsgPrivateRuleVote = 49;   // P7 host → guest: a rule proposal opened, changed or closed (public)
        private const byte MsgPrivateRuleBallot = 50; // P7 guest → host: this computer's player's vote on that proposal
        private const byte MsgPrivatePrompt = 51;     // P7 host → guest: one of the game's popups for this computer's player
        private const byte MsgPrivatePromptAnswer = 52; // P7 guest → host: the button that player pressed
        private const byte MsgPrivateSelection = 53;    // P7 (s155) host → guest: one of the game's card choices for this computer's player
        private const byte MsgPrivateSelectionAnswer = 54; // P7 guest → host: the cards that player picked, or none (closed)
        private const byte MsgPrivateAbilityDraw = 55;  // P8A guest → host: "Draw 3 Abilities" on this player's turn
        private const byte MsgPrivateAbilityOffer = 56; // P8A host → guest: the Abilities drawn (game definitions; public once drawn)
        private const byte MsgPrivateTableRoles = 57;   // P8A host → guest: public roles of table parts (the Ability deck) and the room's rules level

        private static bool IsPrivateMessage(byte type) => type >= MsgPrivatePackage && type <= MsgPrivateTableRoles;

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

        // P8A (owner, 2026-11-08: "privacy of the data, not a game that behaves differently"): the public facts a guest's
        // own menus need to offer what the host's offer (which deck is the Ability deck, whether the room plays Full).
        private ContainerId guestAbilityDeckId;
        private bool guestRoomFull;
        internal ContainerId GuestAbilityDeckId => privateGuest ? guestAbilityDeckId : ContainerId.Empty;
        internal bool GuestRoomFull => privateGuest && guestRoomFull;
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
        private int purchasesAccepted;  // PRIVACY-P6C: Shop purchases the host ran (any seat)
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

#if UNITY_EDITOR
        // P7 (s159): host-only scenario setup from the panel (Editor only; never in a build).
        internal void ScenarioStage(string target)
        {
            if (!privateHosting) return;
            PrivateNote(TrapFloorDevFixtures.StageActivePawnNextTo(host, target));
        }

        internal void ScenarioAimDice(string exitKind)
        {
            if (!privateHosting) return;
            PrivateNote(TrapFloorDevFixtures.AimFloorDiceAt(host, exitKind));
        }

        internal void ScenarioReport()
        {
            if (!privateHosting) return;
            foreach (string line in TrapFloorDevFixtures.Report(host).Split('\n')) PrivateNote(line.TrimEnd('\r'));
        }
#endif

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
#if UNITY_EDITOR
            // P7 (s155) DEV-FIXTURE: an Editor host may fix its own deal for a reproducible test floor. The seed stays on
            // this computer (a private host never sends a seed); every guest still gets only its own package.
            if (TrapFloorDevFixtures.TryTakeDealSeed(out int fixtureSeed)) seed = fixtureSeed;
#endif
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

            // P8A: the room's rules level changed (Settings or the panel): every guest's menus follow it.
            bool fullNow = PlayAssist.Level == PlayAssistLevel.Full;
            if (fullNow != lastRolesFull)
            {
                lastRolesFull = fullNow;
                for (int i = 0; i < members.Count; i++) if (!members[i].IsHost) SendPrivateTableRoles(members[i]);
            }

            if (!ReferenceEquals(privateHostMatch, host.matchState))
            {
                PrivateTableReplaced("the host's table was replaced (Undo, reset or a new floor)");
                return;
            }

            TickPrivateDepartures(); // P7: a departed guest's staged payment goes back once its wait is over

            // PRIVACY-P6B: anything that changed the host's table outside a private request (a pose, a game step the
            // host ran) reaches every guest as a filtered change set; checked cheaply four times a second.
            if (Time.unscaledTime < nextSweepAt || privateTransactionDepth > 0) return;
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
                Layout = BuildPrivateLayout(),
                // PRIVACY-P6B: Draw/Place/Return/Discard run through the table's own command door.
                TransferExecutor = ExecuteTransferOnAuthority,
                HudSource = CurrentHud,
            };
            privateShopCatalog = BuildShopCatalog();
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

        // PRIVACY-P6C: the public layout, with each seat's payment slots (its Console slots but Main) for the Shop.
        private RecipientPublicLayout BuildPrivateLayout()
        {
            RecipientPublicLayout layout = host.BuildPublicLayout();
            if (layout == null || host.trapFloorTemplate == null) return layout;
            foreach (RecipientSeatLayout seat in layout.Seats)
                foreach (TrapFloorPlayerSetupDefinition setup in host.trapFloorTemplate.Players)
                    if (setup.SeatId == seat.Seat)
                        for (int i = 0; i < setup.SideSlotContainerIds.Count; i++) seat.SideSlots.Add(setup.SideSlotContainerIds[i]);
            return layout;
        }

        // PRIVACY-P6C: the Shop's public list (the game's own catalog and prices, the Controller Cards' buttons).
        private PrivateShopCatalog BuildShopCatalog()
        {
            GameDefinitionData game = host.trapFloorTemplate?.GameDefinition;
            if (game == null) return null;
            List<CardDefinitionData> controllers = new List<CardDefinitionData>();
            for (int i = 0; i < game.Cards.Count; i++) if (game.Cards[i].RepresentedControllerInput.HasValue) controllers.Add(game.Cards[i]);
            return PrivateShopCatalog.From(TrapFloorShop.Catalog(game), controllers);
        }

        private PrivateShopCatalog privateShopCatalog;

        // PRIVACY-P6C: the Shop's public list, its own message after the package (so the package's byte check stays
        // strict). Checked too: no real id of a hidden card, no definition only hidden cards have, not the seed. Its
        // names are the Shop's (every player may open the Shop and read them).
        private void SendShopCatalog(Member member)
        {
            if (privateShopCatalog == null || member == null || !member.Connected || member.Connection < 0) return;
            byte[] bytes = TableWire.Write(privateShopCatalog, false);
            PlayerId recipient = RecipientOf(member);
            RecipientSnapshotProjection.Classify(host.matchState, recipient, out HashSet<TabletopObjectId> hidden, out _);
            int ids = 0, defs = 0;
            foreach (TabletopObjectId id in hidden) if (TabletopComposition.Contains(bytes, id.Value.ToByteArray())) ids++;
            HashSet<ObjectDefinitionId> definitions = TabletopComposition.HiddenOnlyDefinitions(host.matchState, hidden);
            foreach (ObjectDefinitionId definition in definitions) if (TabletopComposition.Contains(bytes, definition.Value.ToByteArray())) defs++;
            bool seed = privateSeedKnown && TabletopComposition.Contains(bytes, BitConverter.GetBytes(privateSeed));
            PrivateNote($"Sent the Shop's public list to {member.Name}: {bytes.Length} bytes, {privateShopCatalog.Entries.Count} Abilities, {privateShopCatalog.Controllers.Count} Controller Cards; "
                + $"hidden real ids {ids}/{hidden.Count}, hidden-only definition ids {defs}/{definitions.Count}, deal seed {(seed ? "FOUND" : "absent")}.");
            SendPrivateBytes(member, MsgPrivateShop, bytes);
            SendPrivateTableRoles(member);
        }

        // P8A: which container is the Ability deck (public: every player sees the deck) and the room's rules level.
        private bool lastRolesFull = true;

        private void SendPrivateTableRoles(Member member)
        {
            if (host.trapFloorTemplate == null) return;
            byte[] roles = new byte[17];
            host.trapFloorTemplate.AbilityDeckId.Value.ToByteArray().CopyTo(roles, 0);
            roles[16] = (byte)(PlayAssist.Level == PlayAssistLevel.Full ? 1 : 0);
            SendPrivateBytes(member, MsgPrivateTableRoles, roles);
        }

        private void GuestTableRoles(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 17) return;
            byte[] id = new byte[16];
            Array.Copy(bytes, 0, id, 0, 16);
            guestAbilityDeckId = new ContainerId(new Guid(id));
            guestRoomFull = bytes[16] == 1;
        }

        // PRIVACY-P6B: the host's table object was replaced by one that continues it (Undo, a reset, a new floor). The
        // private authority keeps its handles, revision and what each guest was sent: the difference goes out as change
        // sets, or, where it cannot be told as object changes (containers made or removed), as that guest's package.
        private void PrivateTableReplaced(string why)
        {
            privateHostMatch = host.matchState;
            privateHost.Rebind(host.matchState);
            privateHost.Layout = BuildPrivateLayout();
            privateShopCatalog = BuildShopCatalog();
            PrivateNote("Private table: " + why + "; sending what changed.");
            RetireGuestPopups(why);
            HostSceneFollow(null);
            SendPublish(privateHost.PublishChanges(CurrentTurnSnapshot(), CurrentHud()), why);
        }

        // Sends one publish: each guest its own update, or its own package when its change could not be told.
        // P7 (s155) PHYS-DEDUP diagnostics: where each pawn and die last rested when the table was published, and how it
        // moved since. Says what a sweep's revision carried (the extra revision after a move was "1 piece moved").
        private readonly Dictionary<TabletopObjectId, PhysicalObjectState> restAtPublish = new Dictionary<TabletopObjectId, PhysicalObjectState>();
        private int piecePublishes, pieceResettles;

        private string NoteRestChanges()
        {
            MatchState table = host.matchState;
            if (table == null) return string.Empty;
            StringBuilder text = null;
            void Check(TabletopObjectId id, TabletopObjectState state, string kind, int face)
            {
                PhysicalObjectState rest = PrivateChangeSetBuilder.RestingPlace(state.PhysicalState);
                if (rest == null) return;
                if (restAtPublish.TryGetValue(id, out PhysicalObjectState was) && was != null)
                {
                    float dx = rest.Position.X - was.Position.X, dy = rest.Position.Y - was.Position.Y, dz = rest.Position.Z - was.Position.Z;
                    float cm = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) * 100f;
                    if (ReferenceEquals(rest, was)) return;
                    if (text == null) text = new StringBuilder(); else text.Append("; ");
                    string name = id.Value.ToString(); // the end of the id tells pieces apart (the start is shared by a game's pawns)
                    text.Append($"{kind} …{name.Substring(Math.Max(0, name.Length - 6))} {cm:0.0} cm{(face > 0 ? $" face {face}" : string.Empty)}");
                    if (cm < 2f) pieceResettles++;
                }

                restAtPublish[id] = rest;
            }

            foreach (var pair in table.Pawns) Check(pair.Key, pair.Value.BaseState, "pawn", 0);
            foreach (var pair in table.Dice) Check(pair.Key, pair.Value.BaseState, "die", pair.Value.CurrentValue);
            return text == null ? string.Empty : text.ToString();
        }

        private void SendPublish(Dictionary<PlayerId, byte[]> updates, string what)
        {
            if (privateHost == null) return;
            string pieces = NoteRestChanges();
            if (pieces.Length > 0 && updates != null) { piecePublishes++; PrivateNote($"Pieces in this publish ({what}): {pieces}."); }
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
                    if (state.ContainerId.IsEmpty) hash = hash * 31 + PoseHash(state) + RestHash(state);
                }

                // P7 (s154): a piece the physics put down changes only its physical state; a die its face too.
                foreach (KeyValuePair<TabletopObjectId, PawnState> pair in table.Pawns) hash = hash * 31 + PoseHash(pair.Value.BaseState) + RestHash(pair.Value.BaseState);
                foreach (KeyValuePair<TabletopObjectId, ConsoleCards.Core.Domain.Dice.DieState> pair in table.Dice) hash = hash * 31 + PoseHash(pair.Value.BaseState) + RestHash(pair.Value.BaseState) + pair.Value.CurrentValue * 7919;
                foreach (KeyValuePair<TabletopObjectId, TokenState> pair in table.Tokens) hash = hash * 31 + PoseHash(pair.Value.BaseState) + RestHash(pair.Value.BaseState);
                foreach (KeyValuePair<ContainerId, ContainerState> pair in table.Containers)
                {
                    hash = hash * 31 + pair.Key.GetHashCode();
                    for (int i = 0; i < pair.Value.Count; i++) hash = hash * 7 + pair.Value.GetObjectAt(i).GetHashCode();
                }

                foreach (KeyValuePair<SeatId, SeatState> pair in table.Seats) hash = hash * 3 + (pair.Value.Console != null && pair.Value.Console.ShowsBack ? 1 : 0);
                return hash;
            }
        }

        // Where a piece rests (its physical state once at rest; nothing while it moves: sent once it rests).
        private static int RestHash(TabletopObjectState state)
        {
            PhysicalObjectState rest = PrivateChangeSetBuilder.RestingPlace(state.PhysicalState);
            if (rest == null) return 0;
            unchecked
            {
                // Coarse (2 cm, about a degree): a hair's settling is not a change to send (the change set has the same rule).
                return (int)Math.Round(rest.Position.X * 50) * 83492791 ^ (int)Math.Round(rest.Position.Y * 50) * 2654435 ^ (int)Math.Round(rest.Position.Z * 50) * 40503
                    ^ (int)Math.Round(rest.Rotation.W * 50) * 97 ^ (int)Math.Round(rest.Rotation.Y * 50) * 31;
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
            // PRIVACY-P6C: inside a batch already open (Cancel Payment's), this step joins it (a second Begin used to
            // reset the open batch and end it early, so the cancel became several Undo steps).
            bool ownBatch = !host.undoBatchActive;
            if (ownBatch) host.SessionService.BeginUndoBatch();
            try
            {
                bool moved = AbilityDrawRefusedInFull(from.Id, to.Id) ? false
                    : from.Kind == ContainerKind.Deck && to.Kind == ContainerKind.Hand && from.TryPeekTop(out TabletopObjectId top) && top == real
                    ? host.TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(new DrawCardsCommand(Ctx(sender), from.Id, to.Id, 1)).Succeeded
                    : host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(TransferCardCommand.ToContainer(Ctx(sender), real, from.Id, to.Id)).Succeeded;
                if (moved && face.HasValue && host.matchState.Cards.TryGetValue(real, out CardInstanceState card) && card.Face != face.Value
                    && !host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(Ctx(sender), real, face.Value)).Succeeded)
                    PrivateNote("The card moved but the table refused to turn it over (it stays as it lies).");
                return moved;
            }
            finally
            {
                if (ownBatch) host.SessionService.EndUndoBatch();
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
            PrivateHudSnapshot hud = new PrivateHudSnapshot { Round = round, Phase = phase, HasTurnColor = color.HasValue, ShopOpen = host.TrapFloorGame.IsShopOpen() };
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
            SendShopCatalog(member);
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
                case MsgPrivateRuleBallot:
                    HostRuleBallot(member, data);
                    break;
                case MsgPrivateAbilityDraw:
                    HostAbilityDraw(member);
                    break;
                case MsgPrivatePromptAnswer:
                    HostPromptAnswer(member, data);
                    break;
                case MsgPrivateSelectionAnswer:
                    HostSelectionAnswer(member, data);
                    break;
                default:
                    PrivateNote($"Refused private message {type} from {member.Name}: hosts do not take it.");
                    break;
            }
        }

        private void HostPrivateRequest(Member member, byte[] data)
        {
            if (PrivateTransactionCodec.IsTransaction(data, 1))
            {
                Guid transactionId = Guid.Empty;
                byte detail = 0;
                PrivateRequestVerdict transactionVerdict = PrivateTransactionCodec.TryRead(data, 1, out PrivateTransaction tx)
                    ? RunPrivateTransaction(member, tx, null, out transactionId, out detail)
                    : PrivateRequestVerdict.UnknownReference;
                if (tx == null) PrivateNote($"Refused a malformed payment request from {member.Name}.");
                BeginMessage(MsgPrivateVerdict);
                WriteGuid(transactionId);
                writer.Write((byte)transactionVerdict);
                writer.Write(detail); // the game's own reason (TrapFloorShopError) when its purchase refused it
                SendTo(member);
                return;
            }

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
                // P7 (s154): a guest's card put into its own Console slot is a staged payment until it is confirmed or
                // cancelled; where it came from is remembered so it can go back if that computer goes away.
                List<TabletopObjectId> handBefore = null;
                Dictionary<TabletopObjectId, bool> facesBefore = null;
                RoomSeats stageSeats = new RoomSeats(this);
                if (!member.IsHost && host.matchState.Containers.TryGetValue(stageSeats.HandOf(sender), out ContainerState handState))
                {
                    handBefore = new List<TabletopObjectId>(handState.ObjectIds);
                    facesBefore = new Dictionary<TabletopObjectId, bool>();
                    foreach (TabletopObjectId id in handBefore)
                        if (host.matchState.Cards.TryGetValue(id, out CardInstanceState c)) facesBefore[id] = c.Face == CardFace.FaceUp;
                }

                verdict = privateHost.Place(sender, place, out updates);
                if (verdict == PrivateRequestVerdict.Accepted && handBefore != null && privateHost.LastFrom == stageSeats.HandOf(sender)
                    && stageSeats.IsOwnConsoleSlot(sender, privateHost.LastTo) && handBefore.Contains(privateHost.LastCard))
                {
                    if (!hostStaged.TryGetValue(sender.Value, out List<StagedOrigin> staged)) hostStaged[sender.Value] = staged = new List<StagedOrigin>();
                    staged.RemoveAll(o => o.Card == privateHost.LastCard);
                    staged.Add(new StagedOrigin
                    {
                        Card = privateHost.LastCard, Slot = privateHost.LastTo, From = privateHost.LastFrom,
                        Index = handBefore.IndexOf(privateHost.LastCard), FaceUp = facesBefore.TryGetValue(privateHost.LastCard, out bool up) && up, Valid = true,
                    });
                }
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
                // P7 (s157): a card put into a Console slot tells the cartridge in play exactly as a drop on this
                // computer's own table does (its own Console rules: an Ability offers itself, a payment card is counted).
                // Generic: the cartridge decides what an insertion means; nothing here knows the game.
                if (kind == PrivateRequestCodec.KindPlace) RaisePrivateConsoleInsertion(sender, privateHost.LastCard, privateHost.LastTo);
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
            if (moved) RetireGuestPopups(snap.Ended ? "the floor ended" : "the turn moved on", snap.Ended ? null : snap);
            PrivateNote($"Accepted End Turn from {actor}: " + (moved
                    ? $"round {roundBefore} {phaseBefore} → round {snap.Round} {(TrapFloorTurnPhase)snap.Phase}, turn of {SeatName(new PlayerId(snap.TurnOwner))}"
                    : "the game's own turn service did not end the turn (a step is still waiting)")
                + $"; revision {revisionBefore} → {privateHost.Revision}; card or piece changes from the turn's own steps: {(updates != null && HostFingerprintFast() != cardsBeforeFingerprint ? "yes (in the same change set)" : "none")}.");
            if (!moved && host.TrapFloorGame.lastFloorTurnRefusal != null)
            {
                PrivateNote($"Refused End Turn from {actor} by the game's rule: {host.TrapFloorGame.lastFloorTurnRefusal}");
                return TrapFloorEndTurnVerdict.RuleRefused;
            }

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

        // The member the host is acting for now (a guest's checked decision running here), else the host itself.
        private Member ActingMember()
        {
            if (hostActingFor != Guid.Empty)
                for (int i = 0; i < members.Count; i++) if (!members[i].IsHost && members[i].Player == hostActingFor) return members[i];
            return HostMember();
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
            operationConsoleInteractions.Clear();
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
            StagedOrigin staging = StagingOriginFor(member, sender, resolved);
            bool done = false;
            if (privateTransactionDepth > 0)
            {
                // PRIVACY-P6C: one step of a transaction the host's own game runs (Cancel Payment puts several cards
                // back): it runs now, and every guest gets the transaction's result once, when it ends.
                bool passingNow = privateInterceptor.PassThrough;
                privateInterceptor.PassThrough = true;
                try { done = runOwnCommand != null ? runOwnCommand() : ExecuteOperation(sender, resolved); }
                finally { privateInterceptor.PassThrough = passingNow; }
                HostSceneFollow(sceneBefore);
                privateTransactionSteps++;
                if (done) operationsAccepted++; else operationsRefused++;
                PrivateNote($"{(done ? "Accepted" : "Refused (table rules)")} {op.Kind} from {actor} as step {privateTransactionSteps} of a transaction; sent when it ends.");
                return done ? PrivateRequestVerdict.Accepted : PrivateRequestVerdict.RuleRefused;
            }

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
            NoteStaging(sender, resolved, staging);
            RaiseOperationConsoleInteractions();
            PrivateNote($"Accepted {op.Kind} from {actor} (assist {PlayAssist.Level}); revision {revisionBefore} → {privateHost.Revision}; each guest computer here got its own change set.");
            return PrivateRequestVerdict.Accepted;
        }

        private readonly List<IConsoleCardInteraction> operationConsoleInteractions = new List<IConsoleCardInteraction>(2);

        // P7 (s157): generic. A card put into (or taken out of) a Console slot by a private request reaches the cartridge in
        // play exactly as the same drop on the host's own table does; the cartridge decides what it means (Trap Floor: an
        // owned Ability offers itself to the Console's owner only; a payment card is counted).
        private void RaiseOperationConsoleInteractions()
        {
            if (operationConsoleInteractions.Count == 0) return;
            IConsoleCardInteraction[] raised = operationConsoleInteractions.ToArray();
            operationConsoleInteractions.Clear();
            foreach (IConsoleCardInteraction interaction in raised) host.HandleConsoleCardInteractionAccepted(interaction);
        }

        // The table's own command for a checked operation, with the real ids the check resolved.
        private bool ExecuteOperation(PlayerId sender, PrivateResolvedOperation resolved)
        {
            PrivateOperation op = resolved.Operation;
            switch (op.Kind)
            {
                case PrivateOperationKind.Transfer:
                {
                    TransferCardResult moved = host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(op.HasPose
                        ? TransferCardCommand.ToTabletop(Ctx(sender), resolved.RealCard, op.Container, op.Pose)
                        : TransferCardCommand.ToContainer(Ctx(sender), resolved.RealCard, op.Container, op.Destination));
                    // P7 (s157): what the move did to a Console (a card in or out of a slot), told to the cartridge in play
                    // once the change is published, as a drop on this computer's own table tells it.
                    if (moved.Succeeded) operationConsoleInteractions.AddRange(moved.ConsoleInteractions);
                    return moved.Succeeded;
                }
                case PrivateOperationKind.Flip:
                    return host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(Ctx(sender), resolved.RealCard, op.FaceUp ? CardFace.FaceUp : CardFace.FaceDown)).Succeeded;
                case PrivateOperationKind.Shuffle:
                    // The host's own random source; neither the seed nor the order leaves this computer.
                    return host.TableAuthority.Submit<ShuffleDeckCommand, ShuffleDeckResult>(new ShuffleDeckCommand(Ctx(sender), op.Container)).Succeeded;
                case PrivateOperationKind.Reorder:
                    return host.TableAuthority.Submit<ReorderContainerCommand, ReorderContainerResult>(new ReorderContainerCommand(Ctx(sender), op.Container, resolved.RealCard, resolved.FromIndex, op.B)).Succeeded;
                case PrivateOperationKind.DrawCount:
                    if (AbilityDrawRefusedInFull(op.Container, op.Destination)) return false;
                    return host.TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(new DrawCardsCommand(Ctx(sender), op.Container, op.Destination, op.A)).Succeeded;
                case PrivateOperationKind.MovePiece:
                    return ExecutePieceMove(sender, resolved.RealCard, op.Pose);
                case PrivateOperationKind.ThrowDie:
                {
                    bool thrownDone = host.TrapFloorGame.TryHostThrowDie(sender, resolved.RealCard, op.Pose, op.A, out int shownValue, out string dieRefusal);
                    if (thrownDone) diceThrowsAccepted++; else { diceThrowsRefused++; PrivateNote("Die throw refused: " + dieRefusal); }
                    if (thrownDone && shownValue != op.A) diceThrowsReplaced++;
                    if (thrownDone) PrivateNote($"Die throw: the table shows {shownValue}{(shownValue != op.A ? $" (the host's result; the thrower's die showed {op.A})" : " (as thrown)")}.");
                    return thrownDone;
                }
                default:
                    return false;
            }
        }

        private int pieceMovesAccepted, pieceMovesRefused;
        private int diceThrowsAccepted, diceThrowsRefused, diceThrowsReplaced;

        /// <summary>
        /// P7 Gate 1 (s153): a pawn put down by a seat, on the authority. In Full the game's own host move check
        /// (AUTH-MOVE: turn, own pawn, out players, holes, straight line, distance, Sticky, Blind, lit tiles) decides
        /// first; Free and Reminders keep tabletop freedom (that check allows everything there). The pawn then rests where
        /// the table's own surface resolver puts it (as the game's own guided move does), so the game's landing check
        /// runs on this table exactly as for a pawn dropped here, and its rule steps follow as rule commands.
        /// </summary>
        private bool ExecutePieceMove(PlayerId sender, TabletopObjectId pieceId, TabletopPose pose)
        {
            if (!host.Registry.TryGetPawnView(pieceId, out PawnView pawnView) || !host.matchState.ContainsObject(pieceId)) return false;
            TabletopObjectState piece = host.matchState.GetObject(pieceId);
            TabletopPose target = new TabletopPose(pose.Position, pose.RotationDegrees, piece.Pose.Layer, piece.Pose.LocalOrder);
            if (host.trapFloorTemplate != null
                && !host.TrapFloorGame.HostAllowsCommand(new MoveObjectCommand(Ctx(sender), pieceId, target), sender))
            {
                pieceMovesRefused++;
                lastPieceRefusal = host.TrapFloorGame.HostRefusal ?? "The host refused that move.";
                PrivateNote("Pawn move refused by the game's move rules: " + lastPieceRefusal);
                return false;
            }

            bool moved;
            if (piece.PhysicalState != null && host.physicalAuthority != null && pawnView.PhysicalObject != null)
            {
                if (!host.physicalAuthority.Surfaces.TryResolveAuthoredLooseObject(target, sender, pawnView.PhysicalObject.PhysicalCollider, piece.IsUserLocked, out PhysicalObjectState placed))
                {
                    pieceMovesRefused++;
                    lastPieceRefusal = "That place is not on the table.";
                    return false;
                }

                // At rest where it was put (the resolver's place is Dynamic: the host's physics would move it again,
                // unpublished).
                PhysicalObjectState resting = pawnView.PhysicalObject.RestingOnTable(placed); // on what is under it, not 6 cm above it
                // P7 (s158) UNDO-POSE: the table's pose is part of the same accepted move. It is set before the commit, so
                // the Undo snapshot taken when the commit is accepted (and the game's landing check) see the pawn where it
                // now stands, not where it stood (live run s157: an Undo put a pawn back on a Key tile it had left).
                TabletopObjectState movedPiece = host.matchState.GetObject(pieceId);
                TabletopPose poseBefore = movedPiece.Pose;
                movedPiece.SetPose(new TabletopPose(target.Position, poseBefore.RotationDegrees, poseBefore.Layer, poseBefore.LocalOrder));
                moved = host.TableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(
                    new CommitPhysicalObjectCommand(host.CreateCommandContext(sender), pieceId, resting, piece.PhysicalRevision, null, AuthoritativeActionRecordMode.Transaction)).Succeeded;
                if (!moved) movedPiece.SetPose(poseBefore);
            }
            else
            {
                moved = host.TableAuthority.Submit<MoveObjectCommand, MoveObjectResult>(new MoveObjectCommand(Ctx(sender), pieceId, target)).Succeeded;
            }

            if (moved)
            {
                pieceMovesAccepted++;
                // The table's pose follows the accepted resting place (a physical commit records only the physical state):
                // the game's next move check and every guest read the pawn where it now stands.
                TabletopObjectState now = host.matchState.GetObject(pieceId);
                if (!now.Pose.Position.Equals(target.Position))
                    now.SetPose(new TabletopPose(target.Position, now.Pose.RotationDegrees, now.Pose.Layer, now.Pose.LocalOrder));
                pawnView.ApplyAcceptedState();
                lastPieceRefusal = null;
            }
            else pieceMovesRefused++;
            return moved;
        }

        private string lastPieceRefusal;

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
            // P7 (s155): while the host runs a guest's own checked decision (a popup answer, a choice), the card moves
            // that decision makes are that guest's, checked as that seat (its hand, its Console, its turn), never as
            // the host player's.
            Member me = ActingMember();
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
                case TrapFloorBuyFromShopCommand buy:
                    return TryRunHostOwnPurchase(me, self, buy, command, out result);
                default:
                    return false;
            }

            TResult captured = RefusedResults.Of<TResult>(); // PRIVACY-P6C: refused unless the command ran
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

        // PRIVACY-P6C: the host player's own Confirm Payment (the game's Shop flow on this computer) as the same Shop
        // purchase a guest sends: its own references for the payment cards, the same checks, then the game's command.
        private bool TryRunHostOwnPurchase<TCommand, TResult>(Member me, PlayerId self, TrapFloorBuyFromShopCommand buy, TCommand command, out TResult result) where TCommand : class
        {
            PrivateTransaction tx = new PrivateTransaction
            {
                Kind = PrivateTransactionKind.ShopBuy,
                RequestId = Guid.NewGuid(),
                Revision = privateHost.Revision,
                Definition = Guid.TryParse(buy.CardDefinitionStableId, out Guid definition) ? definition : Guid.Empty,
            };
            foreach (TabletopObjectId paid in buy.PaymentCardIds) tx.Items.Add(new PrivateTransactionItem { Card = privateHost.ReferenceFor(self, paid) });
            TResult captured = RefusedResults.Of<TResult>();
            PrivateRequestVerdict verdict = RunPrivateTransaction(me, tx, () =>
            {
                captured = host.TableAuthority.Submit<TCommand, TResult>(command);
                return ResultSucceeded(captured);
            }, out _, out _);
            if (verdict != PrivateRequestVerdict.Accepted && verdict != PrivateRequestVerdict.RuleRefused)
                host.ShowMessage($"Private session: the purchase was refused ({verdict}).");
            result = captured;
            return true;
        }

        // PRIVACY-P6C: THE pipeline for Confirm Payment (a Shop purchase) and Cancel Payment (staged cards back), whoever
        // asks. Who may ask: PrivateTransactionPolicy (seat, Full turn, one request id once, the revision the buyer
        // confirmed against, its own payment cards in its own Console). What is a legal purchase: the game's own
        // TrapFloorBuyFromShopUseCase (whose turn and phase, the exact price, side slots, unlocked cards, the
        // destination), all checked before anything changes. One accepted request: one Undo step, one revision, every
        // guest its own change set. <paramref name="detail"/>: the game's reason when its purchase refused (a TrapFloorShopError).
        private PrivateRequestVerdict RunPrivateTransaction(Member member, PrivateTransaction tx, Func<bool> runOwnCommand, out Guid requestId, out byte detail)
        {
            requestId = tx.RequestId;
            detail = 0;
            string actor = member.IsHost ? "the host's own seat" : member.Name;
            PlayerId sender = member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            if (privateHost == null) return PrivateRequestVerdict.NotSeated;
            string label = tx.Kind == PrivateTransactionKind.ShopBuy ? "Confirm Payment (Shop purchase)" : "Cancel Payment (staged cards back)";
            long revisionBefore = privateHost.Revision;
            string before = HostTableFingerprint();
            PrivateRequestVerdict verdict = PrivateTransactionPolicy.Check(host.matchState, new RoomSeats(this), privateHost, sender, tx, out PrivateResolvedTransaction resolved);
            CardDefinitionData bought = null;
            string why = verdict.ToString();
            // P8A: a card of this turn's Ability draw is bought out of the deck, whether or not the Shop rule is on.
            TabletopObjectId offerCard = tx.Kind == PrivateTransactionKind.ShopBuy
                ? host.TrapFloorGame.PeekAbilityOfferCard(sender, tx.Definition.ToString())
                : TabletopObjectId.Empty;
            if (verdict == PrivateRequestVerdict.Accepted && tx.Kind == PrivateTransactionKind.ShopBuy)
            {
                if (offerCard.IsEmpty && !host.TrapFloorGame.IsShopOpen()) { verdict = PrivateRequestVerdict.RuleRefused; why = "the Shop is closed in the rules in play"; }
                else if (!TryFindShopCard(tx.Definition, out bought)) { verdict = PrivateRequestVerdict.UnknownReference; why = "no such Ability in the Shop"; }
            }

            if (verdict != PrivateRequestVerdict.Accepted)
            {
                operationsRefused++;
                PrivateNote($"Refused {label} from {actor}: {why} ({PlayAssist.Name(PlayAssist.Level)}); host table {(before == HostTableFingerprint() ? "unchanged" : "CHANGED")}, revision {(privateHost.Revision == revisionBefore ? "unchanged" : "CHANGED")} ({privateHost.Revision}).");
                return verdict;
            }

            HostSceneState sceneBefore = CaptureHostScene();
            bool done = false;
            TrapFloorShopError shopError = TrapFloorShopError.None;
            int restored = 0;
            Dictionary<PlayerId, byte[]> updates = privateHost.Apply(() =>
            {
                bool passing = privateInterceptor.PassThrough;
                privateInterceptor.PassThrough = true;
                // One Undo step: a Cancel's several moves are folded into one batch; a purchase is ONE game command and
                // records its own step as the game does offline (live run, 2026-11-07: a batch around it ended on the
                // purchase's follow-up acceptance, which is not a completed transaction: "Only completed transactions may
                // enter Undo history", and the purchase was then published by the sweep instead of as this request).
                bool ownBatch = tx.Kind == PrivateTransactionKind.PaymentRestore && !host.undoBatchActive;
                if (ownBatch) host.SessionService.BeginUndoBatch();
                try
                {
                    if (tx.Kind == PrivateTransactionKind.PaymentRestore) done = RestoreStagedCards(sender, resolved, out restored);
                    else if (runOwnCommand != null) done = runOwnCommand();
                    else
                    {
                        TrapFloorBuyFromShopResult result = host.TableAuthority.Submit<TrapFloorBuyFromShopCommand, TrapFloorBuyFromShopResult>(
                            new TrapFloorBuyFromShopCommand(Ctx(sender), bought.StableId, TabletopObjectId.New(), resolved.RealCards, offerCard));
                        done = result.Succeeded;
                        shopError = result.Error;
                    }
                }
                finally
                {
                    if (ownBatch) host.SessionService.EndUndoBatch();
                    privateInterceptor.PassThrough = passing;
                }
            }, null, (Func<PrivateHudSnapshot>)CurrentHud);
            HostSceneFollow(sceneBefore);
            SendPublish(updates, label);
            if (!done)
            {
                operationsRefused++;
                detail = (byte)shopError;
                PrivateNote($"Refused {label} from {actor}: the game's own rules refused it ({(tx.Kind == PrivateTransactionKind.ShopBuy ? shopError.ToString() : "a card could not go back")}); "
                    + $"host table {(before == HostTableFingerprint() ? "unchanged" : "CHANGED")} (revision {revisionBefore} → {privateHost.Revision}, no card moved).");
                return PrivateRequestVerdict.RuleRefused;
            }

            operationsAccepted++;
            ForgetStaged(sender, resolved.RealCards); // P7: paid with, or put back: no longer staged
            if (tx.Kind == PrivateTransactionKind.ShopBuy) purchasesAccepted++;
            if (member.IsHost) hostOwnAccepted++;
            PrivateNote(tx.Kind == PrivateTransactionKind.ShopBuy
                ? $"Accepted {label} from {actor} ({PlayAssist.Name(PlayAssist.Level)}): {(bought != null ? bought.DisplayName : "an Ability")} for {tx.Items.Count} payment card(s); revision {revisionBefore} → {privateHost.Revision}; each guest computer here got its own change set."
                : $"Accepted {label} from {actor}: {restored} of {tx.Items.Count} card(s) put back; revision {revisionBefore} → {privateHost.Revision}, one change set each.");
            return PrivateRequestVerdict.Accepted;
        }

        private bool TryFindShopCard(Guid definition, out CardDefinitionData card)
        {
            card = null;
            GameDefinitionData game = host.trapFloorTemplate?.GameDefinition;
            if (game == null) return false;
            foreach (CardDefinitionData candidate in TrapFloorShop.Catalog(game))
                if (Guid.TryParse(candidate.StableId, out Guid id) && id == definition) { card = candidate; return true; }
            return false;
        }

        // Cancel Payment on the authority, as the game's own cancel does it (owner decision D): each staged card back to
        // its pile and place (or its spot), lowest place first, with its face. Checked already: the payer's own places.
        // ---------- P7 Gate 0: the host's own record of what a guest staged in its Console ----------
        // A guest's payment is staged by ordinary moves from its Hand into its own Console slots; the paying state itself
        // lives on the guest. The host records, from the moves it accepted, where each such card came from (Hand, place,
        // face), so that when that guest's computer goes away mid-payment the host puts the cards back itself: nothing is
        // lost, nothing is left half-staged, and the reconnected player finds its Hand as it was. A purchase or a Cancel
        // takes the cards off the record; so does any later move of the card out of that slot.

        private struct StagedOrigin
        {
            public TabletopObjectId Card;
            public ContainerId Slot;
            public ContainerId From;
            public int Index;
            public bool FaceUp;
            public bool Valid;
        }

        private readonly Dictionary<Guid, List<StagedOrigin>> hostStaged = new Dictionary<Guid, List<StagedOrigin>>();
        private int stagedRestoredOnLeave;

        private StagedOrigin StagingOriginFor(Member member, PlayerId sender, PrivateResolvedOperation resolved)
        {
            StagedOrigin origin = default;
            if (member.IsHost || resolved == null || resolved.Operation.Kind != PrivateOperationKind.Transfer || resolved.Operation.HasPose) return origin;
            RoomSeats seats = new RoomSeats(this);
            PrivateOperation op = resolved.Operation;
            if (op.Container != seats.HandOf(sender) || !seats.IsOwnConsoleSlot(sender, op.Destination)) return origin;
            if (!host.matchState.Containers.TryGetValue(op.Container, out ContainerState hand) || !host.matchState.Cards.TryGetValue(resolved.RealCard, out CardInstanceState card)) return origin;
            origin.Card = resolved.RealCard;
            origin.Slot = op.Destination;
            origin.From = op.Container;
            origin.Index = hand.IndexOf(resolved.RealCard);
            origin.FaceUp = card.Face == CardFace.FaceUp;
            origin.Valid = origin.Index >= 0;
            return origin;
        }

        private void NoteStaging(PlayerId sender, PrivateResolvedOperation resolved, StagedOrigin staging)
        {
            if (sender.IsEmpty || resolved == null) return;
            if (!hostStaged.TryGetValue(sender.Value, out List<StagedOrigin> list)) hostStaged[sender.Value] = list = new List<StagedOrigin>();
            // Any accepted move of a recorded card out of its slot ends its record (a manual return, a move elsewhere).
            if (!resolved.RealCard.IsEmpty)
                list.RemoveAll(o => o.Card == resolved.RealCard && (!host.matchState.Cards.TryGetValue(o.Card, out CardInstanceState c) || c.BaseState.ContainerId != o.Slot));
            if (staging.Valid && !list.Exists(o => o.Card == staging.Card)) list.Add(staging);
        }

        private void ForgetStaged(PlayerId sender, IReadOnlyList<TabletopObjectId> cards)
        {
            if (sender.IsEmpty || cards == null || !hostStaged.TryGetValue(sender.Value, out List<StagedOrigin> list)) return;
            foreach (TabletopObjectId id in cards) list.RemoveAll(o => o.Card == id);
        }

        // P7 (s155): how long a departed guest's staged payment waits for that same running copy to get back in (the
        // client's own reconnect tries last about 29 s: 1+2+3+5+8+10), before it is a confirmed departure.
        private const float StagedPaymentGraceSeconds = 40f;

        /// <summary>
        /// P7: a guest's computer went away (left, closed, or lost the connection). Nothing moves yet: the same running
        /// copy may get back in and finish its payment (it keeps its choice). It is put back once, when the departure is
        /// confirmed: it said goodbye, the wait runs out (TickPrivate), or another copy takes the seat back
        /// (PrivateMemberBack). A second leave notice (Bye, then the transport's own) changes nothing.
        /// </summary>
        private void PrivateMemberGone(Member member, bool saidGoodbye)
        {
            if (!privateHosting || privateHost == null || member == null || member.IsHost || member.Player == Guid.Empty) return;
            int stagedCount = hostStaged.TryGetValue(member.Player, out List<StagedOrigin> list) ? list.Count : 0;
            if (saidGoodbye)
            {
                // It left on purpose (Leave, or its window closed): nothing is coming back to finish the payment.
                PrivateNote($"{member.Name} said goodbye; cards it had staged in its Console: {stagedCount}.");
                PrivateConfirmDeparture(member, "it left the room");
                return;
            }

            if (stagedCount > 0 && member.StagedCleanupAt < 0f) member.StagedCleanupAt = Time.unscaledTime + StagedPaymentGraceSeconds;
            PrivateNote($"{member.Name} went away (its connection closed); cards it had staged in its Console: {stagedCount}"
                + (stagedCount > 0 ? $"; they go back in {StagedPaymentGraceSeconds:0} s unless the same copy of the game gets back in." : "."));
        }

        /// <summary>P7: a departed guest's seat is taken back. The same running copy finishes its own payment; any other copy
        /// (a restart) no longer holds it, so the staged cards go back first, before its package is sent.</summary>
        private void PrivateMemberBack(Member member, bool sameRunningCopy)
        {
            if (!privateHosting || privateHost == null || member == null || member.StagedCleanupAt < 0f) return;
            if (sameRunningCopy)
            {
                member.StagedCleanupAt = -1f;
                PrivateNote($"{member.Name} is back (the same copy of the game): its staged payment stays with it.");
                return;
            }

            PrivateConfirmDeparture(member, "a new copy of the game took the seat back");
        }

        private void TickPrivateDepartures()
        {
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.StagedCleanupAt >= 0f && !member.Connected && Time.unscaledTime >= member.StagedCleanupAt)
                    PrivateConfirmDeparture(member, $"it did not get back in within {StagedPaymentGraceSeconds:0} s");
            }
        }

        // Puts a departed guest's staged cards back where they came from, once (the record is cleared first).
        private void PrivateConfirmDeparture(Member member, string why)
        {
            member.StagedCleanupAt = -1f;
            if (!hostStaged.TryGetValue(member.Player, out List<StagedOrigin> list) || list.Count == 0) return;
            PrivateNote($"{member.Name}'s departure is confirmed ({why}): its staged payment is put back.");
            PlayerId player = new PlayerId(member.Player);
            PrivateTransaction tx = new PrivateTransaction { Kind = PrivateTransactionKind.PaymentRestore, RequestId = Guid.NewGuid(), Revision = privateHost.Revision };
            List<TabletopObjectId> real = new List<TabletopObjectId>();
            foreach (StagedOrigin o in list)
            {
                if (!host.matchState.Cards.TryGetValue(o.Card, out CardInstanceState card) || card.BaseState.ContainerId != o.Slot) continue; // already moved on
                tx.Items.Add(new PrivateTransactionItem { Container = o.From, Index = o.Index, FaceUp = o.FaceUp });
                real.Add(o.Card);
            }

            list.Clear();
            if (real.Count == 0) return;
            PrivateResolvedTransaction resolved = new PrivateResolvedTransaction { Transaction = tx };
            resolved.RealCards.AddRange(real);
            long before = privateHost.Revision;
            int restored = 0;
            BeginPrivateTransaction();
            bool passing = privateInterceptor.PassThrough;
            bool ownBatch = !host.undoBatchActive;
            if (ownBatch) host.SessionService.BeginUndoBatch();
            HostSceneState sceneBefore = CaptureHostScene();
            privateInterceptor.PassThrough = true;
            try
            {
                RestoreStagedCards(player, resolved, out restored);
            }
            finally
            {
                privateInterceptor.PassThrough = passing;
                if (ownBatch) host.SessionService.EndUndoBatch();
                HostSceneFollow(sceneBefore);
                privateTransactionSteps++;
                EndPrivateTransaction($"{member.Name}'s staged payment put back (the computer went away)");
            }

            stagedRestoredOnLeave += restored;
            PrivateNote($"{member.Name} went away with {real.Count} card(s) staged in its Console: {restored} put back where they came from (revision {before} → {privateHost.Revision}).");
        }

        private bool RestoreStagedCards(PlayerId sender, PrivateResolvedTransaction resolved, out int restored)
        {
            restored = 0;
            List<int> order = new List<int>();
            for (int i = 0; i < resolved.RealCards.Count; i++) order.Add(i);
            order.Sort((a, b) => resolved.Transaction.Items[a].Index.CompareTo(resolved.Transaction.Items[b].Index));
            bool all = true;
            foreach (int i in order)
            {
                PrivateTransactionItem item = resolved.Transaction.Items[i];
                TabletopObjectId real = resolved.RealCards[i];
                if (!host.matchState.Cards.TryGetValue(real, out CardInstanceState card)) { all = false; continue; }
                ContainerId from = card.BaseState.ContainerId;
                bool moved;
                if (item.HasPose)
                    moved = from.IsEmpty || host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(TransferCardCommand.ToTabletop(Ctx(sender), real, from, item.Pose)).Succeeded;
                else
                    moved = from == item.Container || host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(TransferCardCommand.ToContainer(Ctx(sender), real, from, item.Container)).Succeeded;
                if (!moved) { all = false; continue; }
                if (!item.HasPose && host.matchState.Containers.TryGetValue(item.Container, out ContainerState pile))
                {
                    int now = pile.IndexOf(real);
                    int wanted = Math.Min(item.Index, pile.Count - 1);
                    if (now >= 0 && now != wanted)
                        host.TableAuthority.Submit<ReorderContainerCommand, ReorderContainerResult>(new ReorderContainerCommand(Ctx(sender), item.Container, real, now, wanted));
                }

                CardFace face = item.FaceUp ? CardFace.FaceUp : CardFace.FaceDown;
                if (card.Face != face) host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(Ctx(sender), real, face));
                if (from != item.Container || item.HasPose) restored++;
            }

            return all;
        }

        // PRIVACY-P6C: a game step on the host that is several table commands (the game's own Cancel Payment) reaches
        // every guest once, as one revision, when it ends; the sweep waits for it.
        private int privateTransactionDepth;
        private int privateTransactionSteps;

        internal void BeginPrivateTransaction()
        {
            if (!privateHosting || privateHost == null) return;
            if (privateTransactionDepth++ == 0)
            {
                privateTransactionSteps = 0;
                privateTransactionTurn = CurrentTurnSnapshot();
            }
        }

        private PrivateTurnSnapshot privateTransactionTurn;

        internal void EndPrivateTransaction(string what)
        {
            if (privateTransactionDepth == 0) return;
            if (--privateTransactionDepth > 0) return;
            if (privateTransactionSteps == 0 || privateHost == null) return;
            long before = privateHost.Revision;
            PrivateTurnSnapshot now = CurrentTurnSnapshot();
            bool turnChanged = !SameTurn(privateTransactionTurn, now); // P7: a rule step can end a floor or knock a player out
            SendPublish(privateHost.PublishChanges(turnChanged ? now : null, CurrentHud()), what);
            if (turnChanged) turnTransitions++;
            if (turnChanged) RetireGuestPopups(now.Ended ? "the floor ended" : "the turn moved on", now.Ended ? null : now);
            PrivateNote($"{what}: {privateTransactionSteps} step(s) on the host's table, one revision for every guest ({before} → {privateHost.Revision}){(turnChanged ? "; the turn changed with it" : string.Empty)}.");
        }

        private static bool SameTurn(PrivateTurnSnapshot a, PrivateTurnSnapshot b) =>
            a != null && b != null && a.Round == b.Round && a.Phase == b.Phase && a.TurnOwner == b.TurnOwner && a.ActivePlayer == b.ActivePlayer && a.Ended == b.Ended;

        private int hostRuleSteps; // P7: the game's own rule commands run on the private host

        /// <summary>
        /// P7 (s153): on a private host, the game's own Trap Floor rule commands (reveal, Trap, Ability, Key, escape,
        /// prize, collapse, elimination, guidance, Blind roll...) that the host's cartridge issues are the existing rules
        /// running on the authority. They run once on the host's table and every guest gets one change set (its own
        /// projection, with the turn when it changed). The Shop purchase keeps its own transaction path (P6C). Guests
        /// never run these: their cartridge is empty and their requests reach the host as private operations.
        /// </summary>
        internal bool TryRunHostRuleCommand<TCommand, TResult>(TCommand command, out TResult result) where TCommand : class
        {
            result = default;
            if (!privateHosting || privateHost == null || command == null) return false;
            Type type = typeof(TCommand);
            if (type.Namespace != "ConsoleCards.Games.TrapFloor" || !type.Name.StartsWith("TrapFloor", StringComparison.Ordinal)) return false;
            if (command is TrapFloorBuyFromShopCommand) return false;
            BeginPrivateTransaction();
            HostSceneState sceneBefore = CaptureHostScene();
            privateInterceptor.PassThrough = true;
            try
            {
                result = host.TableAuthority.Submit<TCommand, TResult>(command);
            }
            finally
            {
                privateInterceptor.PassThrough = false;
                privateTransactionSteps++;
                hostRuleSteps++;
                HostSceneFollow(sceneBefore);
                EndPrivateTransaction("Rule step " + type.Name.Substring("TrapFloor".Length).Replace("Command", string.Empty));
            }

            return true;
        }

        private static bool ResultSucceeded(object result)
        {
            switch (result)
            {
                case TrapFloorBuyFromShopResult r: return r.Succeeded;
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
                case MsgPrivateShop:
                    GuestShopCatalog(payload);
                    break;
                case MsgPrivateAbilityOffer:
                    GuestAbilityOffer(payload);
                    break;
                case MsgPrivateTableRoles:
                    GuestTableRoles(payload);
                    break;
                case MsgPrivateRuleVote:
                    GuestRuleVote(payload);
                    break;
                case MsgPrivatePrompt:
                    GuestPrompt(payload);
                    break;
                case MsgPrivateSelection:
                    GuestSelection(payload);
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
            GuestPaymentFollow(); // PRIVACY-P6C: a payment staged before a resync continues with the new table
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
                int reusedBefore = host.privateViewsReused;
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
                int reused = host.privateViewsReused - reusedBefore;
                if (reused > 0) measure.What = $"incremental change set ({summary.Created.Count} new references: {reused} plain-back views rebound, {summary.Created.Count - reused} made)";
                EndSceneWork(measure);
                if (!shown)
                {
                    // The views could not follow even from this computer's own table: the scene is not left stale; this
                    // computer's own package is asked for again (counted; never a full table).
                    PrivateNote("The scene could not follow the change set: " + error + ". Asking the host for this computer's package (never a full table).");
                    AskForOwnPackage();
                    return;
                }
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
            GuestPaymentFollow();
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
                // P7 Gate 1: a pawn is moved on the host. While it is carried nothing is committed here (the view follows the
                // hand as a preview); where it is put down goes to the host, whose change set places it (or, refused, it
                // goes back to where this table still has it).
                case CommitPhysicalObjectCommand carried when IsPawn(carried.ObjectId) && carried.State != null
                    && (carried.State.Mode == PhysicalObjectMode.Held || carried.State.Mode == PhysicalObjectMode.Dynamic):
                    return true; // carried or still falling: only where it comes to rest is sent
                case CommitPhysicalObjectCommand put when IsPawn(put.ObjectId) && put.State != null:
                    op.Kind = PrivateOperationKind.MovePiece;
                    op.Card = put.ObjectId;
                    op.HasPose = true;
                    op.Pose = PoseOf(put);
                    // A resting piece may report itself again every frame: one request per place it comes to rest.
                    if (guestLastPiecePlace.TryGetValue(put.ObjectId, out TableCoordinate sentAt)
                        && Math.Abs(sentAt.X - op.Pose.Position.X) < 0.01d && Math.Abs(sentAt.Y - op.Pose.Position.Y) < 0.01d)
                        return true;
                    guestLastPiecePlace[put.ObjectId] = op.Pose.Position;
                    what = "pawn move (table gesture)";
                    guestPieceMoves[op.RequestId] = put.ObjectId;
                    break;
                // P7 Gate 1: a die thrown here. While it flies nothing is committed (the view shows the throw); where it
                // comes to rest and the face it shows go to the host, whose change set shows the accepted face.
                case CommitPhysicalObjectCommand flying when IsDie(flying.ObjectId) && flying.State != null
                    && (flying.State.Mode == PhysicalObjectMode.Held || flying.State.Mode == PhysicalObjectMode.Dynamic || !flying.SettledDieValue.HasValue):
                    return true;
                case CommitPhysicalObjectCommand landed when IsDie(landed.ObjectId) && landed.State != null:
                {
                    op.Kind = PrivateOperationKind.ThrowDie;
                    op.Card = landed.ObjectId;
                    op.HasPose = true;
                    op.Pose = PoseOf(landed);
                    op.A = landed.SettledDieValue.Value;
                    // A die that settles again where the table already has it, showing the table's face, is not a new throw
                    // (DICE-LOOP: the host's face shown here wakes the die once).
                    if (host.matchState.Dice.TryGetValue(landed.ObjectId, out var shownDie) && shownDie.CurrentValue == op.A
                        && guestLastPiecePlace.TryGetValue(landed.ObjectId, out TableCoordinate dieAt)
                        && Math.Abs(dieAt.X - op.Pose.Position.X) < 0.05d && Math.Abs(dieAt.Y - op.Pose.Position.Y) < 0.05d)
                        return true;
                    guestLastPiecePlace[landed.ObjectId] = op.Pose.Position;
                    guestPieceMoves[op.RequestId] = landed.ObjectId;
                    what = $"die throw ({op.A} here)";
                    break;
                }
                case MoveObjectCommand mv when IsPawn(mv.ObjectId):
                    op.Kind = PrivateOperationKind.MovePiece;
                    op.Card = mv.ObjectId;
                    op.HasPose = true;
                    op.Pose = mv.TargetPose;
                    what = "pawn move (table gesture)";
                    guestPieceMoves[op.RequestId] = mv.ObjectId;
                    break;
                default:
                    return false;
            }

            forwardedCommands++;
            SendPrivateRequest(PrivateOperationCodec.Write(op), what);
            return true;
        }

        private readonly Dictionary<Guid, TabletopObjectId> guestPieceMoves = new Dictionary<Guid, TabletopObjectId>();
        private readonly Dictionary<TabletopObjectId, TableCoordinate> guestLastPiecePlace = new Dictionary<TabletopObjectId, TableCoordinate>();

        // P8A (owner, 2026-11-08): in Full an Ability reaches a hand only by being bought (Draw 3 Abilities / Shop), so
        // the host refuses any draw or move from the Ability deck straight into a hand.
        private bool AbilityDrawRefusedInFull(ContainerId from, ContainerId to)
        {
            if (PlayAssist.Level != PlayAssistLevel.Full || host.trapFloorTemplate == null || from != host.trapFloorTemplate.AbilityDeckId) return false;
            if (host.matchState == null || !host.matchState.Containers.TryGetValue(to, out ContainerState toState) || toState.Kind != ContainerKind.Hand) return false;
            PrivateNote("Refused: in Full, Abilities are bought (Draw 3 Abilities), not drawn into a hand.");
            return true;
        }

        private bool IsPawn(TabletopObjectId id) => host.matchState != null && host.matchState.Pawns.ContainsKey(id);
        private bool IsDie(TabletopObjectId id) => host.matchState != null && host.matchState.Dice.ContainsKey(id);

        // Where a physical commit puts the piece on the table (the table's own coordinates, its turn on the table).
        private TabletopPose PoseOf(CommitPhysicalObjectCommand commit)
        {
            TabletopObjectState state = host.matchState.ContainsObject(commit.ObjectId) ? host.matchState.GetObject(commit.ObjectId) : null;
            TabletopPose basis = state != null ? state.Pose : TabletopPose.Default;
            if (commit.State == null || host.coordinateConverter == null) return basis;
            TableCoordinate at = host.coordinateConverter.ToTableCoordinate(new Vector3(commit.State.Position.X, commit.State.Position.Y, commit.State.Position.Z));
            return new TabletopPose(at, basis.RotationDegrees, basis.Layer, basis.LocalOrder);
        }

        // P7: the host refused a pawn move this table sent: the pawn goes back to where this table has it.
        private void GuestPieceVerdict(Guid requestId, PrivateRequestVerdict verdict)
        {
            if (!guestPieceMoves.TryGetValue(requestId, out TabletopObjectId piece)) return;
            guestPieceMoves.Remove(requestId);
            if (verdict == PrivateRequestVerdict.Accepted) return;
            guestLastPiecePlace.Remove(piece); // the same place may be asked for again
            if (host.matchState != null && host.matchState.ContainsObject(piece)) host.PresentAcceptedObject(piece);
            host.ShowMessage(verdict == PrivateRequestVerdict.RuleRefused ? "The host refused that move (the game's move rules)." : $"The host refused that move ({verdict}).");
        }

        // ---------- P7 Gate 2: the game's popups for a player on another computer ----------

        private sealed class PendingPrompt
        {
            public PlayerId Actor;
            public FocusedCardRevealModel Model;
            public PrivateTurnSnapshot Turn;
        }

        private readonly Dictionary<Guid, PendingPrompt> hostPrompts = new Dictionary<Guid, PendingPrompt>();
        private int promptsSent, promptsAnswered, promptsRefused;
        private Guid guestPromptId;

        /// <summary>Host: sends the popup to the computer that plays <paramref name="actor"/>. False when it is not connected (the popup then shows here).</summary>
        internal bool TrySendPrivatePrompt(PlayerId actor, FocusedCardRevealModel model)
        {
            if (!privateHosting || privateHost == null || model == null) return false;
            Member target = null;
            for (int i = 0; i < members.Count; i++)
                if (!members[i].IsHost && members[i].Connected && members[i].Connection >= 0 && members[i].Player == actor.Value) target = members[i];
            if (target == null) return false;
            // One popup at a time per player: a newer one replaces the one it was still showing.
            List<Guid> old = new List<Guid>();
            foreach (KeyValuePair<Guid, PendingPrompt> pair in hostPrompts) if (pair.Value.Actor == actor) old.Add(pair.Key);
            foreach (Guid id in old) hostPrompts.Remove(id);
            Guid promptId = Guid.NewGuid();
            hostPrompts[promptId] = new PendingPrompt { Actor = actor, Model = model, Turn = CurrentTurnSnapshot() };
            TabletopObjectId shownCard = privateHost.ReferenceFor(actor, model.CardId);
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                w.Write((byte)1);
                w.Write(promptId.ToByteArray());
                w.Write(shownCard.Value.ToByteArray());
                w.Write(model.HeaderTitle ?? string.Empty);
                w.Write(model.ResultState ?? string.Empty);
                w.Write(model.CardTitle ?? string.Empty);
                w.Write(model.CardSubtitle ?? string.Empty);
                w.Write(model.Body ?? string.Empty);
                w.Write(model.CompletionLabel ?? string.Empty);
                w.Write(model.Secondary != null ? model.SecondaryLabel ?? string.Empty : string.Empty);
                w.Flush();
                byte[] bytes = stream.ToArray();
                CheckSentBytes(bytes, actor, "game popup");
                SendPrivateBytes(target, MsgPrivatePrompt, bytes);
            }

            promptsSent++;
            PrivateNote($"Sent the game's popup \"{model.HeaderTitle}: {model.CardTitle}\" to {target.Name} (prompt {promptId.ToString().Substring(0, 8)}).");
            return true;
        }

        // P7 (s157): popups and choices the host's game sent that can no longer be answered (their floor ended, or the
        // table was replaced by a new floor, an Undo or a reset) are taken back from the guest that shows them: the
        // guest closes it and nothing is decided. An end-of-floor announcement (sent once the floor had ended) stays.
        // With a turn given (the turn moved on, live run s157: a prize popup stayed on the screen of a player who had
        // escaped): only what was asked of a player in an earlier turn who does not own the new one (the host refuses
        // their answer as stale anyway); a choice the new turn's owner was just asked stays.
        private bool StillAnswerable(PlayerId actor, PrivateTurnSnapshot asked, PrivateTurnSnapshot now) =>
            now != null && (asked == null || SameTurn(asked, now) || actor.Value == now.TurnOwner);

        private void RetireGuestPopups(string why, PrivateTurnSnapshot now = null)
        {
            if (!privateHosting) return;
            int retired = 0;
            foreach (KeyValuePair<Guid, PendingPrompt> pair in new List<KeyValuePair<Guid, PendingPrompt>>(hostPrompts))
            {
                if (pair.Value.Turn != null && pair.Value.Turn.Ended) continue;
                if (StillAnswerable(pair.Value.Actor, pair.Value.Turn, now)) continue;
                hostPrompts.Remove(pair.Key);
                SendRetire(pair.Value.Actor, MsgPrivatePrompt, pair.Key);
                retired++;
            }

            foreach (KeyValuePair<Guid, PendingSelection> pair in new List<KeyValuePair<Guid, PendingSelection>>(hostSelections))
            {
                if (pair.Value.Turn != null && pair.Value.Turn.Ended) continue;
                if (StillAnswerable(pair.Value.Actor, pair.Value.Turn, now)) continue;
                hostSelections.Remove(pair.Key);
                SendRetire(pair.Value.Actor, MsgPrivateSelection, pair.Key);
                retired++;
            }

            if (retired > 0) PrivateNote($"Took back {retired} unanswered guest popup(s): {why}.");
        }

        private void SendRetire(PlayerId actor, byte type, Guid id)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].IsHost || !members[i].Connected || members[i].Connection < 0 || members[i].Player != actor.Value) continue;
                byte[] payload = new byte[17];
                payload[0] = 2; // 2: take back (1: show)
                Array.Copy(id.ToByteArray(), 0, payload, 1, 16);
                SendPrivateBytes(members[i], type, payload);
            }
        }

        private void HostPromptAnswer(Member member, byte[] data)
        {
            // [type][16 prompt][1 choice: 0 main, 1 second]
            string verdict;
            PendingPrompt prompt = null;
            Guid promptId = Guid.Empty;
            if (data == null || data.Length != 18 || data[17] > 1) verdict = "Malformed";
            else
            {
                byte[] id = new byte[16];
                Array.Copy(data, 1, id, 0, 16);
                promptId = new Guid(id);
                if (!hostPrompts.TryGetValue(promptId, out prompt)) verdict = "Unknown or already answered";
                else if (member.Player != prompt.Actor.Value) verdict = "NotYourPopup";
                else if (!SameTurn(prompt.Turn, CurrentTurnSnapshot())) verdict = "Stale (the turn has moved on)";
                else if (data[17] == 1 && prompt.Model.Secondary == null) verdict = "NoSecondButton";
                else verdict = "Accepted";
            }

            if (verdict != "Accepted")
            {
                promptsRefused++;
                if (verdict.StartsWith("Stale", StringComparison.Ordinal)) hostPrompts.Remove(promptId);
                PrivateNote($"Refused a popup answer from {member.Name}: {verdict}.");
                return;
            }

            hostPrompts.Remove(promptId);
            promptsAnswered++;
            PrivateNote($"{member.Name} pressed \"{(data[17] == 1 ? prompt.Model.SecondaryLabel : prompt.Model.CompletionLabel)}\" on \"{prompt.Model.CardTitle}\": running it on the host's table.");
            hostActingFor = member.Player;
            try
            {
                if (data[17] == 1) prompt.Model.Secondary(); else prompt.Model.Completed();
            }
            finally
            {
                hostActingFor = Guid.Empty;
            }
        }

        private void GuestPrompt(byte[] bytes)
        {
            try
            {
                using (BinaryReader r = new BinaryReader(new MemoryStream(bytes, false)))
                {
                    byte version = r.ReadByte();
                    if (version == 2)
                    {
                        Guid retired = new Guid(r.ReadBytes(16));
                        if (retired != guestPromptId || retired == Guid.Empty) return;
                        guestPromptId = Guid.Empty;
                        host.runtimeUi?.CloseFocusedCardSelection();
                        PrivateNote("The host took back a popup that can no longer be answered (its floor ended or the table was replaced).");
                        return;
                    }

                    if (version != 1) return;
                    Guid promptId = new Guid(r.ReadBytes(16));
                    TabletopObjectId card = new TabletopObjectId(new Guid(r.ReadBytes(16)));
                    string header = r.ReadString(), state = r.ReadString(), title = r.ReadString(), subtitle = r.ReadString(), body = r.ReadString();
                    string main = r.ReadString(), second = r.ReadString();
                    guestPromptId = promptId;
                    if (host.runtimeUi == null || card.IsEmpty) return;
                    host.runtimeUi.ShowFocusedCardReveal(new FocusedCardRevealModel(
                        card, host.CurrentBoardCardBack(), GuestArt(card), header, state, title, subtitle, body, main,
                        () => GuestPromptAnswer(promptId, 0),
                        string.IsNullOrEmpty(second) ? null : second,
                        string.IsNullOrEmpty(second) ? null : (Action)(() => GuestPromptAnswer(promptId, 1)))
                    {
                        AlwaysAsk = true, // the host's level decided that this asks; this computer's own level does not hide it
                    });
                    PrivateNote($"The host's game asks: {header} {title} ({main}{(string.IsNullOrEmpty(second) ? string.Empty : " / " + second)}).");
                }
            }
            catch (Exception exception)
            {
                PrivateNote("A popup from the host could not be read: " + exception.Message);
            }
        }

        private void GuestPromptAnswer(Guid promptId, byte choice)
        {
            host.runtimeUi?.CloseFocusedCardSelection();
            if (promptId != guestPromptId || promptId == Guid.Empty) return;
            guestPromptId = Guid.Empty;
            BeginMessage(MsgPrivatePromptAnswer);
            writer.Write(promptId.ToByteArray());
            writer.Write(choice);
            SendToHost();
            PrivateNote($"Sent this computer's answer ({(choice == 0 ? "main" : "second")} button) to popup {promptId.ToString().Substring(0, 8)}.");
        }

        // P7 (s155): a card's front picture on this guest, only when this computer's own table shows what the card is
        // (a hidden card has the hidden definition here, so it gets no picture: nothing is learned from a popup's art).
        private Texture GuestArt(TabletopObjectId reference)
        {
            if (reference.IsEmpty || host.matchState == null || !host.matchState.Cards.TryGetValue(reference, out CardInstanceState card)) return null;
            if (card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) return null;
            if (host.TryGetCardLook(card.BaseState.DefinitionId, out ConsoleCards.Definitions.CardDefinition definition)) return host.FrontArt(definition);
            return host.TryGetAvatarLook(card.BaseState.DefinitionId, out ConsoleCards.Definitions.AvatarDefinition avatar) ? host.AvatarArt(avatar) : null; // P8A: Avatar draft
        }

        // ---------- P7 (s155): the game's card choices for a player on another computer ----------
        // Use an Ability now, which Reaction stops the waiting Trap, which drawn Ability to buy, which Hand cards pay a
        // prize or a purchase: the same choice, on that player's own computer. The cards are named by that computer's
        // own references; the picks come back and the game's own confirm runs here, checked by the game's own rule
        // (CanConfirm) and as that player's seat.

        private sealed class PendingSelection
        {
            public PlayerId Actor;
            public FocusedCardSelectionModel Model;
            public PrivateTurnSnapshot Turn;
            public List<TabletopObjectId> Real = new List<TabletopObjectId>();
        }

        private readonly Dictionary<Guid, PendingSelection> hostSelections = new Dictionary<Guid, PendingSelection>();
        private int selectionsSent, selectionsAnswered, selectionsRefused;
        private Guid guestSelectionId;
        private readonly List<TabletopObjectId> guestSelectionRefs = new List<TabletopObjectId>();

        /// <summary>Host: sends the choice to the computer that plays <paramref name="actor"/>. False when it is not connected
        /// or a card cannot be named for it (the choice then shows here, as before).</summary>
        // P8A: a choice the host's game asked of this player that is still open (not answered, not taken back).
        internal bool HasPendingPrivateSelection(PlayerId actor, string title)
        {
            foreach (PendingSelection pending in hostSelections.Values)
                if (pending.Actor == actor && pending.Model != null && string.Equals(pending.Model.Title, title, StringComparison.Ordinal)) return true;
            return false;
        }

        // P8A: a choice can reach this player's own computer now (the private table runs and that computer is connected).
        internal bool CanSendPrivateSelection(PlayerId actor)
        {
            if (!privateHosting || privateHost == null || privateHostPending) return false;
            for (int i = 0; i < members.Count; i++)
                if (!members[i].IsHost && members[i].Connected && members[i].Connection >= 0 && members[i].Player == actor.Value) return true;
            return false;
        }

        internal bool TrySendPrivateSelection(PlayerId actor, FocusedCardSelectionModel model)
        {
            if (!privateHosting || privateHost == null || model == null) return false;
            Member target = null;
            for (int i = 0; i < members.Count; i++)
                if (!members[i].IsHost && members[i].Connected && members[i].Connection >= 0 && members[i].Player == actor.Value) target = members[i];
            if (target == null) return false;
            PendingSelection pending = new PendingSelection { Actor = actor, Model = model, Turn = CurrentTurnSnapshot() };
            List<TabletopObjectId> refs = new List<TabletopObjectId>();
            foreach (FocusedCardOptionModel option in model.Options)
            {
                TabletopObjectId reference = privateHost.ReferenceFor(actor, option.CardId);
                if (reference.IsEmpty) return false;
                pending.Real.Add(option.CardId);
                refs.Add(reference);
            }

            List<Guid> old = new List<Guid>();
            foreach (KeyValuePair<Guid, PendingSelection> pair in hostSelections) if (pair.Value.Actor == actor) old.Add(pair.Key);
            foreach (Guid id in old) hostSelections.Remove(id); // one choice at a time per player: the newer one wins
            Guid selectionId = Guid.NewGuid();
            hostSelections[selectionId] = pending;
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                w.Write((byte)1);
                w.Write(selectionId.ToByteArray());
                w.Write(model.Title ?? string.Empty);
                w.Write(model.Instruction ?? string.Empty);
                w.Write(model.Status ?? string.Empty);
                w.Write(model.ConfirmLabel ?? string.Empty);
                w.Write(model.CancelLabel ?? string.Empty);
                w.Write(model.MaximumSelectionCount);
                w.Write(refs.Count);
                for (int i = 0; i < refs.Count; i++)
                {
                    FocusedCardOptionModel option = model.Options[i];
                    w.Write(refs[i].Value.ToByteArray());
                    w.Write(option.Title ?? string.Empty);
                    w.Write(option.Subtitle ?? string.Empty);
                    w.Write(option.Eligible);
                    w.Write(option.Selected);
                }

                w.Flush();
                byte[] bytes = stream.ToArray();
                CheckSentBytes(bytes, actor, "game choice");
                SendPrivateBytes(target, MsgPrivateSelection, bytes);
            }

            selectionsSent++;
            PrivateNote($"Sent the game's choice \"{model.Title}\" ({refs.Count} card(s)) to {target.Name} (choice {selectionId.ToString().Substring(0, 8)}).");
            return true;
        }

        private void HostSelectionAnswer(Member member, byte[] data)
        {
            // [type][16 id][1 closed][4 count][count × 4 index]
            string verdict;
            PendingSelection pending = null;
            Guid selectionId = Guid.Empty;
            List<TabletopObjectId> picked = new List<TabletopObjectId>();
            bool closed = false;
            try
            {
                if (data == null || data.Length < 1 + 16 + 1 + 4) throw new InvalidDataException("short");
                selectionId = new Guid(new ReadOnlySpan<byte>(data, 1, 16));
                closed = data[17] == 1;
                int count = BitConverter.ToInt32(data, 18);
                if (data[17] > 1 || count < 0 || count > 32 || data.Length != 22 + count * 4) throw new InvalidDataException("bad layout");
                if (!hostSelections.TryGetValue(selectionId, out pending)) verdict = "Unknown or already answered";
                else if (member.Player != pending.Actor.Value) verdict = "NotYourChoice";
                else if (!SameTurn(pending.Turn, CurrentTurnSnapshot())) verdict = "Stale (the turn has moved on)";
                else if (!closed && (count == 0 || count > pending.Model.MaximumSelectionCount)) verdict = "WrongCount";
                else
                {
                    verdict = "Accepted";
                    for (int i = 0; i < count && verdict == "Accepted"; i++)
                    {
                        int index = BitConverter.ToInt32(data, 22 + i * 4);
                        if (index < 0 || index >= pending.Real.Count || picked.Contains(pending.Real[index]) || !pending.Model.Options[index].Eligible) verdict = "NotAnOption";
                        else picked.Add(pending.Real[index]);
                    }

                    if (verdict == "Accepted" && !closed && !pending.Model.CanConfirm(picked)) verdict = "RuleRefused (the game's own check)";
                }
            }
            catch (Exception exception)
            {
                verdict = "Malformed (" + exception.Message + ")";
            }

            if (verdict != "Accepted")
            {
                selectionsRefused++;
                if (pending != null && verdict.StartsWith("Stale", StringComparison.Ordinal)) hostSelections.Remove(selectionId);
                PrivateNote($"Refused a choice answer from {member.Name}: {verdict}.");
                return;
            }

            hostSelections.Remove(selectionId);
            selectionsAnswered++;
            PrivateNote(closed
                ? $"{member.Name} closed \"{pending.Model.Title}\" ({(string.IsNullOrEmpty(pending.Model.CancelLabel) ? "Cancel" : pending.Model.CancelLabel)}): running it on the host's table."
                : $"{member.Name} chose {picked.Count} card(s) on \"{pending.Model.Title}\" ({pending.Model.ConfirmLabel}): running it on the host's table.");
            hostActingFor = member.Player;
            try
            {
                if (closed) pending.Model.Dismiss(); else pending.Model.Confirm(picked);
            }
            finally
            {
                hostActingFor = Guid.Empty;
            }
        }

        private void GuestSelection(byte[] bytes)
        {
            try
            {
                using (BinaryReader r = new BinaryReader(new MemoryStream(bytes, false)))
                {
                    byte version = r.ReadByte();
                    if (version == 2)
                    {
                        Guid retired = new Guid(r.ReadBytes(16));
                        if (retired != guestSelectionId || retired == Guid.Empty) return;
                        guestSelectionId = Guid.Empty;
                        host.runtimeUi?.CloseFocusedCardSelection();
                        PrivateNote("The host took back a choice that can no longer be made (its floor ended or the table was replaced).");
                        return;
                    }

                    if (version != 1) return;
                    Guid selectionId = new Guid(r.ReadBytes(16));
                    string title = r.ReadString(), instruction = r.ReadString(), status = r.ReadString(), confirm = r.ReadString(), cancel = r.ReadString();
                    int max = r.ReadInt32(), count = r.ReadInt32();
                    if (count < 0 || count > 32 || max < 1) return;
                    List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(count);
                    guestSelectionRefs.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        TabletopObjectId reference = new TabletopObjectId(new Guid(r.ReadBytes(16)));
                        string optionTitle = r.ReadString(), subtitle = r.ReadString();
                        bool eligible = r.ReadBoolean(), selected = r.ReadBoolean();
                        if (reference.IsEmpty || string.IsNullOrWhiteSpace(optionTitle)) return;
                        guestSelectionRefs.Add(reference);
                        options.Add(new FocusedCardOptionModel(reference, optionTitle, subtitle, GuestArt(reference), eligible, selected));
                    }

                    guestSelectionId = selectionId;
                    if (host.runtimeUi == null || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(confirm)) return;
                    host.runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                        title, instruction, status, confirm, options, max,
                        chosen => chosen.Count >= 1 && chosen.Count <= max, // the host checks the game's own rule
                        chosen => GuestSelectionAnswer(selectionId, false, chosen),
                        () => GuestSelectionAnswer(selectionId, true, null),
                        string.IsNullOrEmpty(cancel) ? null : cancel) { Spread = FocusedCardSelectionModel.IsSpreadTitle(title) });
                    PrivateNote($"The host's game asks you to choose: {title} ({count} card(s); {confirm}{(string.IsNullOrEmpty(cancel) ? string.Empty : " / " + cancel)}).");
                }
            }
            catch (Exception exception)
            {
                PrivateNote("A choice from the host could not be read: " + exception.Message);
            }
        }

        private void GuestSelectionAnswer(Guid selectionId, bool closed, IReadOnlyList<TabletopObjectId> chosen)
        {
            host.runtimeUi?.CloseFocusedCardSelection();
            if (selectionId != guestSelectionId || selectionId == Guid.Empty) return;
            guestSelectionId = Guid.Empty;
            List<int> indices = new List<int>();
            if (!closed && chosen != null)
                foreach (TabletopObjectId id in chosen) { int index = guestSelectionRefs.IndexOf(id); if (index >= 0 && !indices.Contains(index)) indices.Add(index); }
            BeginMessage(MsgPrivateSelectionAnswer);
            writer.Write(selectionId.ToByteArray());
            writer.Write((byte)(closed ? 1 : 0));
            writer.Write(indices.Count);
            foreach (int index in indices) writer.Write(index);
            SendToHost();
            PrivateNote($"Sent this computer's choice ({(closed ? "closed" : indices.Count + " card(s)")}) for {selectionId.ToString().Substring(0, 8)}.");
        }

        // ---------- P7 Gate 0: rule votes, one per player, on that player's own computer ----------

        private int ruleBallotsAccepted, ruleBallotsRefused;
        private byte[] lastRuleVoteBytes;
        private Guid guestRuleProposal;
        private bool guestRuleVoted;

        /// <summary>Host: a proposal opened, changed (someone agreed) or closed. Public text and who agreed; sent to every guest.</summary>
        internal void PrivateRuleVoteChanged(Guid proposal, bool open, string current, string preview, IReadOnlyList<PlayerId> voters, IReadOnlyList<bool> agreed, string message)
        {
            if (!privateHosting) return;
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                w.Write((byte)1); // version
                w.Write(open);
                w.Write(proposal.ToByteArray());
                w.Write(current ?? string.Empty);
                w.Write(preview ?? string.Empty);
                w.Write(message ?? string.Empty);
                w.Write(voters.Count);
                for (int i = 0; i < voters.Count; i++)
                {
                    w.Write(voters[i].Value.ToByteArray());
                    w.Write(host.PlayerLabel(voters[i]) ?? string.Empty);
                    w.Write(i < agreed.Count && agreed[i]);
                }

                w.Flush();
                lastRuleVoteBytes = stream.ToArray();
            }

            int sent = 0;
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.IsHost || !member.Connected || member.Connection < 0) continue;
                SendPrivateBytes(member, MsgPrivateRuleVote, lastRuleVoteBytes);
                sent++;
            }

            PrivateNote($"Rule proposal {proposal.ToString().Substring(0, 8)} {(open ? "open" : "closed")}: sent to {sent} guest(s) ({lastRuleVoteBytes.Length} bytes){(open ? string.Empty : ": " + message)}.");
        }

        private void HostRuleBallot(Member member, byte[] data)
        {
            // [type][16 proposal][1 agree]
            PlayerId voter = member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            string verdict;
            if (data == null || data.Length != 1 + 16 + 1 || data[17] > 1) verdict = "Malformed";
            else if (!new RoomSeats(this).IsSeated(voter)) verdict = "NotSeated";
            else
            {
                byte[] id = new byte[16];
                Array.Copy(data, 1, id, 0, 16);
                verdict = host.RulesCardPanel.ReceiveRemoteBallot(voter, new Guid(id), data[17] == 1);
            }

            if (verdict.StartsWith("Agreed", StringComparison.Ordinal) || verdict == "Rejected") ruleBallotsAccepted++;
            else ruleBallotsRefused++;
            PrivateNote($"Rule vote from {member.Name}: {verdict}.");
        }

        private void GuestRuleVote(byte[] bytes)
        {
            try
            {
                using (BinaryReader r = new BinaryReader(new MemoryStream(bytes, false)))
                {
                    if (r.ReadByte() != 1) return;
                    bool open = r.ReadBoolean();
                    Guid proposal = new Guid(r.ReadBytes(16));
                    string current = r.ReadString(), preview = r.ReadString(), message = r.ReadString();
                    int count = r.ReadInt32();
                    if (count < 0 || count > 8) return;
                    RuleProposalVoter[] voters = new RuleProposalVoter[count];
                    int agreedCount = 0;
                    int mine = -1;
                    for (int i = 0; i < count; i++)
                    {
                        Guid player = new Guid(r.ReadBytes(16));
                        string label = r.ReadString();
                        bool agreed = r.ReadBoolean();
                        if (agreed) agreedCount++;
                        if (player == myPlayer) mine = i;
                        voters[i] = new RuleProposalVoter(label, agreed, player == myPlayer && !agreed);
                    }

                    if (proposal != guestRuleProposal) guestRuleVoted = false;
                    guestRuleProposal = open ? proposal : Guid.Empty;
                    if (host.runtimeUi == null) return;
                    if (!open)
                    {
                        host.runtimeUi.CloseRuleProposal();
                        host.ruleProposalOpen = false;
                        if (!string.IsNullOrEmpty(message)) host.FeedbackLine(message, FeedbackSeverity.Info);
                        PrivateNote("Rule proposal closed by the host: " + message);
                        return;
                    }

                    RuleProposalModel model = new RuleProposalModel
                    {
                        Agreeing = true,
                        CurrentText = current,
                        PreviewText = preview,
                        Voters = voters,
                        Status = $"{agreedCount} of {count} agreed. Everyone must agree; one \"No\" keeps the rules."
                            + (mine < 0 ? " You are watching: no vote." : guestRuleVoted ? " Your vote is in." : string.Empty),
                    };
                    RuleProposalActions actions = new RuleProposalActions
                    {
                        Step = _ => { },
                        TextChanged = _ => { },
                        Propose = () => { },
                        Agree = index => { if (index == mine) GuestBallot(true); },
                        Reject = () => { if (mine >= 0) GuestBallot(false); else host.runtimeUi.CloseRuleProposal(); },
                    };
                    host.ruleProposalOpen = host.runtimeUi.ShowRuleProposal(model, actions);
                    PrivateNote($"Rule proposal {proposal.ToString().Substring(0, 8)} from the host: {agreedCount}/{count} agreed; this computer's player {(mine < 0 ? "is not a voter" : "votes here")}.");
                }
            }
            catch (Exception exception)
            {
                PrivateNote("A rule proposal could not be read: " + exception.Message);
            }
        }

        private void GuestBallot(bool agree)
        {
            if (guestRuleProposal == Guid.Empty || guestRuleVoted) return;
            guestRuleVoted = true;
            BeginMessage(MsgPrivateRuleBallot);
            writer.Write(guestRuleProposal.ToByteArray());
            writer.Write(agree);
            SendToHost();
            PrivateNote($"Sent this computer's vote ({(agree ? "agree" : "no")}) on proposal {guestRuleProposal.ToString().Substring(0, 8)}.");
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
                List<PopupActionOption> actions = new List<PopupActionOption>();
                if (GuestPaymentActive)
                {
                    // PRIVACY-P6C: the game's own payment buttons while this seat is paying.
                    actions.Add(new PopupActionOption("Confirm Payment", guestPayment.IsPaymentComplete && !guestConfirmSent, GuestConfirmPayment));
                    actions.Add(new PopupActionOption("Cancel Payment", true, () => GuestCancelPayment("Payment cancelled.")));
                }

                actions.Add(new PopupActionOption(privateTurn != null && privateTurn.Phase == (int)TrapFloorTurnPhase.FloorTurn ? "End Floor Turn" : "End Turn", mine, () => PrivateEndTurn()));
                actions.Add(new PopupActionOption("Undo my last", true, PrivateUndo));
                host.runtimeUi.ShowTrapFloorStatus(status, new FloorfallStatusModel(false, string.Empty, string.Empty, string.Empty), actions);
                RefreshGuestShopEntry();
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


        // ---------- PRIVACY-P6C: the Shop and Payment on a private guest ----------
        // The game's own flow, on this computer's own table: the Shop popup (the game's catalog, prices and cost
        // services, from the host's public Shop list), the card choice popup, the chosen cards placed in this seat's
        // Console with ordinary table moves (public, as in normal play), then Confirm Payment (one request: the host
        // runs the game's purchase) or Cancel Payment (one request: the staged cards go back). The choice itself stays
        // on this computer until Confirm, as in normal play.

        private PrivateShopCatalog privateShop;
        private GameDefinitionData privateShopDefinition;
        private PendingControllerPurchaseState guestPayment;
        private readonly Dictionary<TabletopObjectId, PrivateTransactionItem> guestPaymentOrigins = new Dictionary<TabletopObjectId, PrivateTransactionItem>();
        private string guestPickedDefinition = string.Empty;
        private Guid guestConfirmRequest = Guid.Empty;
        private Guid guestCancelRequest = Guid.Empty;
        private bool guestConfirmSent;
        private byte[] lastShopBuyBytes;

        private bool GuestPaymentActive => guestPayment != null && guestPayment.IsActive;

        private void GuestShopCatalog(byte[] bytes)
        {
            try
            {
                privateShop = (PrivateShopCatalog)ConsoleCards.Application.Authority.TableWire.Read(bytes, 0, bytes.Length, false);
                privateShopDefinition = privateShop?.ToDefinition();
            }
            catch (Exception exception)
            {
                privateShop = null;
                privateShopDefinition = null;
                PrivateNote("The Shop's list could not be read: " + exception.Message);
                return;
            }

            PrivateNote($"Received the Shop's public list: {bytes.Length} bytes, {privateShop.Entries.Count} Abilities, {privateShop.Controllers.Count} Controller Cards{(privateShopDefinition == null ? " (could not be read as game data)" : string.Empty)}.");
            RefreshGuestShopEntry();
        }

        internal void RefreshPrivateGuestShopEntry() => RefreshGuestShopEntry();

        private void RefreshGuestShopEntry()
        {
            if (!privateGuest || host.runtimeUi == null) return;
            bool open = privateHud != null && privateHud.ShopOpen && privateShopDefinition != null;
            host.runtimeUi.ShowShopEntry(open ? (Action)OpenGuestShop : null);
        }

        private bool GuestShopBuyingTurn() =>
            privateGuest && privateTurn != null && !privateTurn.Ended && privateTurn.Phase == (int)TrapFloorTurnPhase.PlayerTurn && privateTurn.ActivePlayer == myPlayer;

        private ContainerState GuestHand()
        {
            RecipientSeatLayout seat = OwnSeat();
            return seat != null && host.matchState != null && host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand) ? hand : null;
        }

        private void OpenGuestShop()
        {
            if (privateShopDefinition == null || privateHud == null || !privateHud.ShopOpen) { host.ShowMessage("The Shop is closed in the rules in play."); return; }
            if (GuestPaymentActive) { host.ShowMessage("Finish or cancel the current payment first."); return; }
            host.runtimeUi.ShowActionAbilityPurchase(BuildGuestShopOptions(), GuestPickShopItem, host.runtimeUi.CloseActionAbilityPurchase,
                GuestShopBuyingTurn() ? "You are buying." : "You can look; buying is on a player's own turn.", "SHOP", "ABILITIES");
        }

        // The game's own Shop list (TrapFloorShop.Catalog), its prices and what this seat's own Hand can pay.
        private List<ActionAbilityPurchaseOption> BuildGuestShopOptions()
        {
            List<ActionAbilityPurchaseOption> options = new List<ActionAbilityPurchaseOption>();
            ContainerState hand = GuestShopBuyingTurn() ? GuestHand() : null;
            ControllerInputCostEvaluator evaluator = new ControllerInputCostEvaluator();
            foreach (CardDefinitionData definition in TrapFloorShop.Catalog(privateShopDefinition))
            {
                bool forSale = TrapFloorShop.IsForSale(definition);
                string cost = forSale ? InputCostNotation.Format(definition.InputCost) : "not set yet";
                bool canPay = false;
                string affordability;
                if (!forSale) affordability = "Not for sale until the rules give it a price.";
                else if (hand == null) affordability = "Buying is on a player's own turn.";
                else
                {
                    ControllerInputCostEvaluation evaluation = evaluator.Evaluate(host.matchState, hand, definition.InputCost, privateShopDefinition);
                    canPay = evaluation.CanPay;
                    affordability = canPay ? "Your Hand can pay this." : $"Your Hand cannot pay {cost} yet.";
                }

                Texture artwork = null;
                if (Guid.TryParse(definition.StableId, out Guid id) && host.TryGetAuthoredCardDefinition(new ObjectDefinitionId(id), out CardDefinition authored)) artwork = host.FrontArt(authored);
                options.Add(new ActionAbilityPurchaseOption(definition.StableId,
                    guestDrawnCounts.TryGetValue(definition.StableId, out int drawnCount) && drawnCount > 1 ? $"{definition.DisplayName} ×{drawnCount}" : definition.DisplayName,
                    definition.Description, cost, artwork, canPay, affordability, forSale,
                    TrapFloorShop.IsAction(definition) ? "Action" : TrapFloorShop.IsReaction(definition) ? "Reaction" : string.Empty));
            }

            return options;
        }

        // ---------- P8A: Draw 3 Abilities for a guest ----------

        internal void GuestRequestAbilityDraw()
        {
            if (GuestPaymentActive) { host.ShowMessage("Finish or cancel the current payment first."); return; }
            BeginMessage(MsgPrivateAbilityDraw);
            SendToHost();
            PrivateNote("Asked the host for this turn's draw of 3 Abilities.");
        }

        // Host: the turn player on another computer asks for this turn's draw; the host draws (or reopens) it and sends
        // which Abilities lie there. Only in Full; in Free and Reminders players draw by hand.
        private void HostAbilityDraw(Member member)
        {
            PlayerId sender = member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            List<string> drawn = new List<string>();
            bool ok = host.TrapFloorGame.TryPrepareAbilityOfferFor(sender, out drawn); // P8A batch 2 (R1): every assist level
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(ok ? string.Join("\n", drawn) : string.Empty);
            SendPrivateBytes(member, MsgPrivateAbilityOffer, payload);
            PrivateNote(ok ? $"{member.Name}: this turn's Ability draw sent ({drawn.Count})." : $"{member.Name}: no Ability draw now (not their turn, a payment open, or the deck is empty).");
        }

        private readonly Dictionary<string, int> guestDrawnCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        private void GuestAbilityOffer(byte[] bytes)
        {
            string text = bytes == null ? string.Empty : System.Text.Encoding.UTF8.GetString(bytes);
            if (text.Length == 0 || privateShopDefinition == null)
            {
                host.ShowMessage("No Ability draw now: it is on your own turn, with no payment open.");
                return;
            }

            string[] ids = text.Split('\n');
            HashSet<string> drawn = new HashSet<string>(ids, StringComparer.Ordinal);
            guestDrawnCounts.Clear();
            foreach (string id in ids) guestDrawnCounts[id] = guestDrawnCounts.TryGetValue(id, out int n) ? n + 1 : 1;
            List<ActionAbilityPurchaseOption> options = new List<ActionAbilityPurchaseOption>();
            foreach (ActionAbilityPurchaseOption option in BuildGuestShopOptions())
            {
                if (drawn.Contains(option.CardDefinitionStableId)) options.Add(option);
            }

            guestDrawnCounts.Clear();

            host.runtimeUi.ShowActionAbilityPurchase(options, GuestPickShopItem, host.runtimeUi.CloseActionAbilityPurchase,
                "Pick one to buy. What you do not buy goes back; the deck is shuffled when your turn ends.", "ABILITY DRAW", "DRAWN");
        }

        private void GuestPickShopItem(string stableId)
        {
            if (GuestPaymentActive) { host.runtimeUi?.SetActionAbilityPurchaseStatus("Finish or cancel the current payment first."); return; }
            if (!GuestShopBuyingTurn()) { host.runtimeUi?.SetActionAbilityPurchaseStatus("Only the player whose turn it is can buy."); return; }
            if (!privateShopDefinition.TryGetCard(stableId, out CardDefinitionData bought) || !TrapFloorShop.IsForSale(bought))
            {
                host.runtimeUi?.SetActionAbilityPurchaseStatus("Not for sale until the rules give it a price.");
                return;
            }

            ContainerState hand = GuestHand();
            if (hand == null || !ControllerInputCardCatalog.TryCreate(privateShopDefinition, out ControllerInputCardCatalog catalog)) { host.runtimeUi?.SetActionAbilityPurchaseStatus("This price could not be read."); return; }
            guestPickedDefinition = stableId;
            List<FocusedCardOptionModel> cards = new List<FocusedCardOptionModel>(hand.Count);
            for (int i = 0; i < hand.Count; i++)
            {
                TabletopObjectId cardId = hand.GetObjectAt(i);
                if (!host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) continue;
                bool known = catalog.TryGetInput(card.BaseState.DefinitionId, out ControllerInput input);
                bool eligible = known && bought.InputCost.Accepts(input);
                CardDefinition authored = null;
                string name = host.TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out authored) ? authored.DisplayName : "Controller Card";
                cards.Add(new FocusedCardOptionModel(cardId, name, known ? input.ToString() : "Not eligible", host.FrontArt(authored), eligible));
            }

            bool canPay = new ControllerInputCostEvaluator().Evaluate(host.matchState, hand, bought.InputCost, privateShopDefinition).CanPay;
            host.runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                $"BUY {bought.DisplayName.ToUpperInvariant()} — HAND",
                $"Choose {InputCostNotation.Format(bought.InputCost)} from your Hand.",
                canPay ? "Pick the cards that pay the cost." : $"Missing: your Hand cannot pay {InputCostNotation.Format(bought.InputCost)}",
                "Confirm Cards",
                cards,
                bought.InputCost.CardCount,
                GuestPaymentReady,
                GuestConfirmCards,
                () => { guestPickedDefinition = string.Empty; host.runtimeUi?.CloseFocusedCardSelection(); host.ShowMessage("Payment cancelled."); }));
        }

        private bool GuestPaymentReady(IReadOnlyList<TabletopObjectId> selected)
        {
            ContainerState hand = GuestHand();
            if (hand == null || string.IsNullOrEmpty(guestPickedDefinition) || !privateShopDefinition.TryGetCard(guestPickedDefinition, out CardDefinitionData bought)) return false;
            for (int i = 0; i < selected.Count; i++) if (!hand.Contains(selected[i])) return false;
            return new ControllerInputCostEvaluator().EvaluateExact(host.matchState, selected, bought.InputCost, privateShopDefinition).CanPay;
        }

        // Confirm Cards: the payment begins on this computer (nothing is sent; the cards are placed next).
        private void GuestConfirmCards(IReadOnlyList<TabletopObjectId> selected)
        {
            if (!GuestPaymentReady(selected)) { host.ShowMessage("Choose exactly the cards the cost asks for."); return; }
            GuestBeginPayment(guestPickedDefinition, selected);
            guestPickedDefinition = string.Empty;
            host.runtimeUi?.CloseFocusedCardSelection();
        }

        private void GuestBeginPayment(string definition, IReadOnlyList<TabletopObjectId> selected)
        {
            if (guestPayment == null || guestPayment.MatchId != host.matchState.Id) guestPayment = new PendingControllerPurchaseState(host.matchState.Id);
            guestPayment.Begin(new PlayerId(myPlayer), definition, selected, PendingPurchaseSource.Shop);
            guestConfirmSent = false;
            // Owner decision D: where each chosen card was (its pile, place and face), to put it back on Cancel.
            guestPaymentOrigins.Clear();
            foreach (TabletopObjectId id in selected)
            {
                if (!host.matchState.Cards.TryGetValue(id, out CardInstanceState card)) continue;
                ContainerId at = card.BaseState.ContainerId;
                int index = !at.IsEmpty && host.matchState.Containers.TryGetValue(at, out ContainerState pile) ? pile.IndexOf(id) : -1;
                guestPaymentOrigins[id] = new PrivateTransactionItem { Card = id, Container = at, Index = Math.Max(0, index), HasPose = at.IsEmpty, Pose = card.BaseState.Pose, FaceUp = card.Face == CardFace.FaceUp };
            }

            if (host.pendingControllerPurchaseState == null) host.pendingControllerPurchaseState = guestPayment; // the Hand highlights the chosen cards
            PrivateNote($"Payment begun here: {selected.Count} chosen card(s) for {GuestShopName(definition)} (on this computer only until Confirm).");
            host.ShowMessage($"BUY {GuestShopName(definition).ToUpperInvariant()} — Place the chosen cards in your Console, then Confirm Payment");
            RenderPrivateHud();
        }

        private string GuestShopName(string definition) =>
            privateShopDefinition != null && privateShopDefinition.TryGetCard(definition, out CardDefinitionData card) ? card.DisplayName : "ABILITY";

        // After every change set: which chosen cards are now in this seat's payment slots (as the game's own payment
        // tracks Console insertions), a card with the same button standing in for a chosen one (BUY-1), and the
        // payment dropped when the turn has moved on (B01: its cards go back).
        private void GuestPaymentFollow()
        {
            if (!GuestPaymentActive) return;
            if (!GuestShopBuyingTurn()) { GuestCancelPayment("The turn moved on: the unfinished payment was cancelled."); return; }
            RecipientSeatLayout seat = OwnSeat();
            if (seat == null) return;
            PlayerId me = new PlayerId(myPlayer);
            ControllerInputCardCatalog.TryCreate(privateShopDefinition, out ControllerInputCardCatalog catalog);
            foreach (ContainerId slot in seat.SideSlots)
            {
                if (!host.matchState.Containers.TryGetValue(slot, out ContainerState pile)) continue;
                for (int i = 0; i < pile.Count; i++)
                {
                    TabletopObjectId id = pile.GetObjectAt(i);
                    bool chosen = false;
                    foreach (TabletopObjectId c in guestPayment.SelectedCardIds) if (c == id) chosen = true;
                    if (!chosen && catalog != null && host.matchState.Cards.TryGetValue(id, out CardInstanceState placed) && catalog.TryGetInput(placed.BaseState.DefinitionId, out ControllerInput input))
                        foreach (TabletopObjectId c in new List<TabletopObjectId>(guestPayment.SelectedCardIds))
                            if (!guestPayment.IsInserted(c) && host.matchState.Cards.TryGetValue(c, out CardInstanceState other) && catalog.TryGetInput(other.BaseState.DefinitionId, out ControllerInput wanted) && wanted == input)
                            {
                                if (guestPayment.ReplaceSelected(c, id) && guestPaymentOrigins.TryGetValue(c, out PrivateTransactionItem origin))
                                {
                                    guestPaymentOrigins.Remove(c);
                                    guestPaymentOrigins[id] = new PrivateTransactionItem { Card = id, Container = origin.Container, Index = origin.Index, HasPose = origin.HasPose, Pose = origin.Pose, FaceUp = origin.FaceUp };
                                }

                                break;
                            }

                    guestPayment.RecordConsoleInsertion(me, me, slot, id, seat.SideSlots);
                }
            }

            foreach (TabletopObjectId c in guestPayment.SelectedCardIds)
            {
                if (!guestPayment.IsInserted(c)) continue;
                ContainerId at = host.matchState.Cards.TryGetValue(c, out CardInstanceState card) ? card.BaseState.ContainerId : ContainerId.Empty;
                // Taken out of the payment slots again: no longer paid in (the slot it left is one of the seat's slots).
                if (!seat.SideSlots.Contains(at) && seat.SideSlots.Count > 0) guestPayment.RecordConsoleRemoval(me, me, seat.SideSlots[0], c, seat.SideSlots);
            }

            RenderPrivateHud();
        }

        // Confirm Payment: one request; the host runs the game's purchase once (a second Confirm with the same request
        // id is refused as Duplicate there).
        private void GuestConfirmPayment()
        {
            if (!GuestPaymentActive || !guestPayment.IsPaymentComplete) { host.ShowMessage("Place the chosen cards in your Console first."); return; }
            if (guestConfirmSent) { host.ShowMessage("The purchase is with the host already."); return; }
            if (!Guid.TryParse(guestPayment.PurchasedCardDefinitionStableId, out Guid definition)) return;
            PrivateTransaction tx = new PrivateTransaction { Kind = PrivateTransactionKind.ShopBuy, RequestId = Guid.NewGuid(), Revision = privateRevision, Definition = definition };
            foreach (TabletopObjectId id in guestPayment.SelectedCardIds) tx.Items.Add(new PrivateTransactionItem { Card = id });
            lastShopBuyBytes = PrivateTransactionCodec.Write(tx);
            guestConfirmRequest = tx.RequestId;
            guestConfirmSent = true;
            SendPrivateRequest(lastShopBuyBytes, $"Confirm Payment: buy {GuestShopName(guestPayment.PurchasedCardDefinitionStableId)} with {tx.Items.Count} card(s) (table shows revision {privateRevision})");
            RenderPrivateHud();
        }

        // Cancel Payment (also on End Turn and when the turn moves on): one request puts every staged card back where
        // it was; the payment ends here at once.
        private void GuestCancelPayment(string message)
        {
            if (!GuestPaymentActive) return;
            PrivateTransaction tx = new PrivateTransaction { Kind = PrivateTransactionKind.PaymentRestore, RequestId = Guid.NewGuid(), Revision = privateRevision };
            foreach (TabletopObjectId id in guestPayment.SelectedCardIds)
            {
                if (!guestPaymentOrigins.TryGetValue(id, out PrivateTransactionItem origin) || !host.matchState.Cards.TryGetValue(id, out CardInstanceState card)) continue;
                bool back = origin.HasPose ? card.BaseState.ContainerId.IsEmpty : card.BaseState.ContainerId == origin.Container;
                if (back && (card.Face == CardFace.FaceUp) == origin.FaceUp) continue;
                tx.Items.Add(origin);
            }

            guestPayment.Clear();
            guestPaymentOrigins.Clear();
            guestConfirmSent = false;
            if (ReferenceEquals(host.pendingControllerPurchaseState, guestPayment)) host.pendingControllerPurchaseState = null;
            if (tx.Items.Count > 0)
            {
                guestCancelRequest = tx.RequestId;
                SendPrivateRequest(PrivateTransactionCodec.Write(tx), $"Cancel Payment: {tx.Items.Count} staged card(s) back");
            }

            host.ShowMessage(tx.Items.Count > 0 ? $"{message} {tx.Items.Count} card(s) go back where they were." : message);
            PrivateNote($"Payment ended here: {message} ({tx.Items.Count} card(s) to put back).");
            RenderPrivateHud();
        }

        private void GuestTransactionVerdict(Guid id, PrivateRequestVerdict verdict, byte detail)
        {
            if (id == guestConfirmRequest && guestConfirmRequest != Guid.Empty)
            {
                if (verdict == PrivateRequestVerdict.Accepted)
                {
                    string name = GuestPaymentActive ? GuestShopName(guestPayment.PurchasedCardDefinitionStableId) : "the Ability";
                    guestPayment?.Clear();
                    guestPaymentOrigins.Clear();
                    if (ReferenceEquals(host.pendingControllerPurchaseState, guestPayment)) host.pendingControllerPurchaseState = null;
                    host.ShowMessage($"Bought {name} from the Shop.");
                }
                else
                {
                    guestConfirmSent = false; // the payment stays; fix it or cancel it
                    string reason = verdict == PrivateRequestVerdict.RuleRefused ? TrapFloorCartridge.PurchaseErrorText((TrapFloorShopError)detail) : verdict.ToString();
                    host.ShowMessage($"Could not buy it: {reason}");
                }

                RenderPrivateHud();
                return;
            }

            if (id == guestCancelRequest && guestCancelRequest != Guid.Empty && verdict != PrivateRequestVerdict.Accepted)
                host.ShowMessage($"The host could not put the staged cards back ({verdict}); they stay where they are.");
        }

        // ---------- PRIVACY-P6C dev controls (both sides): the game's own payment flow, driven from the panel ----------

        internal void PrivateShopStage()
        {
            if (privateHosting) { HostShopStage(); return; }
            if (privateShopDefinition == null) { PrivateNote("Shop: no Shop list here."); return; }
            if (GuestPaymentActive) { PrivateNote("Shop: a payment is already staged."); return; }
            if (privateHud == null || !privateHud.ShopOpen) { PrivateNote("Shop: the Shop is closed in the rules in play (as the Shop entry shows)."); return; }
            if (!GuestShopBuyingTurn()) { PrivateNote("Shop: not this seat's turn (buying is on a player's own turn)."); return; }
            ContainerState hand = GuestHand();
            if (hand == null) return;
            foreach (CardDefinitionData definition in TrapFloorShop.Catalog(privateShopDefinition))
            {
                if (!TrapFloorShop.IsForSale(definition)) continue;
                ControllerInputCostEvaluation evaluation = new ControllerInputCostEvaluator().Evaluate(host.matchState, hand, definition.InputCost, privateShopDefinition);
                if (!evaluation.CanPay) continue;
                GuestBeginPayment(definition.StableId, evaluation.PaymentCardIds);
                List<ContainerId> slots = FreeSideSlots(evaluation.PaymentCardIds.Count);
                if (slots.Count < evaluation.PaymentCardIds.Count) { PrivateNote("Shop: not enough free payment slots in the Console."); return; }
                for (int i = 0; i < evaluation.PaymentCardIds.Count; i++)
                    SendPrivateRequest(PrivateRequestCodec.Place(new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = evaluation.PaymentCardIds[i], Destination = slots[i] }), $"Stage payment card {i + 1}/{evaluation.PaymentCardIds.Count} into the Console");
                return;
            }

            PrivateNote("Shop: this Hand cannot pay for any Ability.");
        }

        internal void PrivateShopConfirm()
        {
            if (privateHosting) { host.TrapFloorGame.ConfirmPendingActionAbilityPurchase(); return; }
            GuestConfirmPayment();
        }

        internal void PrivateShopCancel()
        {
            if (privateHosting) { host.TrapFloorGame.CancelPendingActionAbilityPurchase(); return; }
            GuestCancelPayment("Payment cancelled.");
        }

        internal void PrivateShopConfirmAgain()
        {
            if (lastShopBuyBytes == null) { PrivateNote("Bad Confirm again: no Confirm sent yet."); return; }
            SendPrivateRequest(lastShopBuyBytes, "Confirm Payment AGAIN, same request id (expect Duplicate; nothing bought twice)");
        }

        // Pay with one card too few (or, with no payment staged, the first own Console card): expect RuleRefused (PaymentInvalid).
        internal void PrivateShopUnderpay()
        {
            List<TabletopObjectId> cards = new List<TabletopObjectId>();
            string definition = GuestPaymentActive ? guestPayment.PurchasedCardDefinitionStableId : FirstForSaleDefinition();
            if (GuestPaymentActive) for (int i = 0; i < guestPayment.SelectedCardIds.Count - 1; i++) cards.Add(guestPayment.SelectedCardIds[i]);
            if (cards.Count == 0) { TabletopObjectId first = FirstOwnConsoleCard(); if (!first.IsEmpty) cards.Add(PanelRef(first)); }
            if (cards.Count == 0 || !Guid.TryParse(definition, out Guid id)) { PrivateNote("Bad underpay: no card in this Console to pay with."); return; }
            PrivateTransaction tx = new PrivateTransaction { Kind = PrivateTransactionKind.ShopBuy, RequestId = Guid.NewGuid(), Revision = ShownRevision(), Definition = id };
            foreach (TabletopObjectId card in cards) tx.Items.Add(new PrivateTransactionItem { Card = card });
            SendPrivateRequest(PrivateTransactionCodec.Write(tx), $"Buy with {cards.Count} card(s), too few (expect RuleRefused: PaymentInvalid; nothing moves)");
        }

        // Pay with another seat's card (one in its Console, else a back in its Hand): expect InvalidSource.
        internal void PrivateShopOthersCards()
        {
            TabletopObjectId card = OtherSeatConsoleCard();
            if (card.IsEmpty) card = FirstCardInAnotherHand();
            string definition = FirstForSaleDefinition();
            if (card.IsEmpty || !Guid.TryParse(definition, out Guid id)) { PrivateNote("Bad pay with another seat's card: none found."); return; }
            PrivateTransaction tx = new PrivateTransaction { Kind = PrivateTransactionKind.ShopBuy, RequestId = Guid.NewGuid(), Revision = ShownRevision(), Definition = id };
            tx.Items.Add(new PrivateTransactionItem { Card = PanelRef(card) });
            SendPrivateRequest(PrivateTransactionCodec.Write(tx), "Buy with ANOTHER seat's card (expect InvalidSource or HiddenRealId; nothing moves)");
        }

        private long ShownRevision() => privateHosting ? (privateHost?.Revision ?? 0) : privateRevision;

        private string FirstForSaleDefinition()
        {
            GameDefinitionData game = privateHosting ? host.trapFloorTemplate?.GameDefinition : privateShopDefinition;
            if (game == null) return string.Empty;
            foreach (CardDefinitionData definition in TrapFloorShop.Catalog(game)) if (TrapFloorShop.IsForSale(definition)) return definition.StableId;
            return string.Empty;
        }

        private TabletopObjectId OtherSeatConsoleCard()
        {
            RecipientSeatLayout own = OwnSeat();
            RecipientPublicLayout layout = privateHosting ? privateHost?.Layout : host.privateViewLayout;
            if (layout == null) return TabletopObjectId.Empty;
            foreach (RecipientSeatLayout seat in layout.Seats)
            {
                if (own != null && seat.Seat == own.Seat) continue;
                foreach (ContainerId slot in seat.SideSlots)
                    if (host.matchState.Containers.TryGetValue(slot, out ContainerState pile) && pile.Count > 0) return pile.GetObjectAt(0);
            }

            return TabletopObjectId.Empty;
        }

        // The empty payment slots of this seat's Console, on its shown side first.
        private List<ContainerId> FreeSideSlots(int wanted)
        {
            List<ContainerId> free = new List<ContainerId>();
            RecipientSeatLayout seat = OwnSeat();
            if (seat == null) return free;
            foreach (ContainerId slot in seat.SideSlots)
                if (free.Count < wanted && host.matchState.Containers.TryGetValue(slot, out ContainerState pile) && pile.Count == 0) free.Add(slot);
            return free;
        }

        // The host player's own payment through the game's own Shop flow (TrapFloorCartridge): the purchase popup's
        // choice, then the chosen cards dragged into the Console (each through the private pipeline), then Confirm.
        private void HostShopStage()
        {
            TrapFloorCartridge game = host.TrapFloorGame;
            if (!game.IsShopBuyingTurn()) { PrivateNote("Shop: not the host seat's turn."); return; }
            if (!game.IsShopOpen()) { PrivateNote("Shop: the Shop is closed in the rules in play (as the Shop entry shows)."); return; }
            RecipientSeatLayout seat = OwnSeat();
            if (seat == null || !host.matchState.Containers.TryGetValue(seat.Hand, out ContainerState hand)) return;
            foreach (ActionAbilityPurchaseOption option in game.BuildActionAbilityPurchaseOptions())
            {
                if (!option.ForSale || !option.CanAfford || !host.trapFloorTemplate.GameDefinition.TryGetCard(option.CardDefinitionStableId, out CardDefinitionData definition)) continue;
                ControllerInputCostEvaluation evaluation = new ControllerInputCostEvaluator().Evaluate(host.matchState, hand, definition.InputCost, host.trapFloorTemplate.GameDefinition);
                if (!evaluation.CanPay) continue;
                List<ContainerId> slots = FreeSideSlots(evaluation.PaymentCardIds.Count);
                if (slots.Count < evaluation.PaymentCardIds.Count) { PrivateNote("Shop: not enough free payment slots in the Console."); return; }
                game.TryPurchaseActionAbilityFromPopup(option.CardDefinitionStableId);
                game.ConfirmFocusedPurchasePayment(evaluation.PaymentCardIds);
                PlayerId me = new PlayerId(HostMember().Player);
                for (int i = 0; i < evaluation.PaymentCardIds.Count; i++)
                {
                    TabletopObjectId card = evaluation.PaymentCardIds[i];
                    host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(TransferCardCommand.ToContainer(host.CreateCommandContext(me), card, seat.Hand, slots[i]));
                    if (host.matchState.Cards.TryGetValue(card, out CardInstanceState state) && state.Face != CardFace.FaceUp)
                        host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(host.CreateCommandContext(me), card, CardFace.FaceUp));
                    // A drag into the Console tells the game through the table's drop handler; a transfer made here
                    // does not, so the payment is told the same way a drop tells it (live run, 2026-11-07: the host's
                    // staged cards were not counted and Confirm said "Place the chosen cards in your Console first").
                    if (host.matchState.Cards.TryGetValue(card, out CardInstanceState placed) && placed.BaseState.ContainerId == slots[i])
                        game.HandlePendingPurchaseConsoleInteraction(new ConsoleCards.Core.Events.ConsoleCardInserted(
                            new ConsoleCards.Core.Events.DomainEventContext(host.matchState.Id, host.matchState.Revision),
                            me, card, placed.BaseState.DefinitionId, me, slots[i]));
                }

                PrivateNote($"Host Shop: staged {evaluation.PaymentCardIds.Count} card(s) for {definition.DisplayName} through the game's own payment flow.");
                return;
            }

            PrivateNote("Shop: the host seat's Hand cannot pay for any Ability.");
        }

        // PRIVACY-P6C dev control: the host's Assist level (Free → Reminders → Full), for the live Full-mode run.
        internal void PrivateCycleAssist()
        {
            PlayAssist.Level = (PlayAssistLevel)(((int)PlayAssist.Level + 1) % 3);
            PrivateNote($"Assist is now {PlayAssist.Name(PlayAssist.Level)} on this computer.");
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

        // P7 (s154): pawn and die requests from the panel. They are the same MovePiece / ThrowDie requests the table's own
        // drag and throw send (GuestForward), for a test that cannot rely on a mouse gesture reaching a 3D piece.
        internal void PrivatePawnStep(int dx, int dy, bool othersPawn = false)
        {
            RecipientSeatLayout seat = OwnSeat();
            RecipientPublicLayout layout = privateHosting ? privateHost?.Layout : host.privateViewLayout;
            TabletopObjectId pawn = default;
            if (!othersPawn && seat != null) pawn = seat.Pawn;
            if (othersPawn && layout != null)
                foreach (RecipientSeatLayout other in layout.Seats) if ((seat == null || other.Seat != seat.Seat) && !other.Pawn.IsEmpty) { pawn = other.Pawn; break; }
            if (pawn.IsEmpty || host.matchState == null || !host.matchState.ContainsObject(pawn)) { PrivateNote("Pawn step: no pawn."); return; }

            TableCoordinate from = host.matchState.GetObject(pawn).Pose.Position;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double ux = dx / len, uy = dy / len;
            double best = double.MaxValue;
            TableCoordinate to = from;
            foreach (var pair in host.matchState.Cards)
            {
                TabletopObjectState s = pair.Value.BaseState;
                if (!s.ContainerId.IsEmpty) continue;
                double ox = s.Pose.Position.X - from.X, oy = s.Pose.Position.Y - from.Y;
                double along = ox * ux + oy * uy;
                double across = Math.Abs(ox * uy - oy * ux);
                if (along < 0.2d || across > 0.35d * along || along >= best) continue;
                best = along;
                to = s.Pose.Position;
            }

            if (best == double.MaxValue) { PrivateNote("Pawn step: no tile that way."); return; }
            TabletopPose pose = host.matchState.GetObject(pawn).Pose;
            SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation
            {
                Kind = PrivateOperationKind.MovePiece, RequestId = Guid.NewGuid(), Card = pawn, HasPose = true,
                Pose = new TabletopPose(to, pose.RotationDegrees, pose.Layer, pose.LocalOrder),
            }), $"Pawn step ({dx},{dy}){(othersPawn ? " of ANOTHER seat's pawn (expect a refusal in Full)" : string.Empty)} to ({to.X:0.00},{to.Y:0.00})");
        }

        private int panelDieIndex;

        internal void PrivateThrowDie(int face = 0)
        {
            if (host.matchState == null || host.matchState.Dice.Count == 0) { PrivateNote("Throw: no die."); return; }
            int index = panelDieIndex++ % host.matchState.Dice.Count, at_ = 0;
            foreach (var pair in host.matchState.Dice)
            {
                if (at_++ != index) continue;
                TabletopPose at = pair.Value.BaseState.Pose;
                int shown = face > 0 ? face : (pair.Value.CurrentValue % pair.Value.SideCount) + 1; // a face other than the shown one
                SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation
                {
                    Kind = PrivateOperationKind.ThrowDie, RequestId = Guid.NewGuid(), Card = pair.Key, HasPose = true, A = shown,
                    Pose = new TabletopPose(new TableCoordinate(at.Position.X + 0.05d, at.Position.Y), at.RotationDegrees, at.Layer, at.LocalOrder),
                }), face > pair.Value.SideCount ? $"Throw die {index + 1} (d{pair.Value.SideCount}) claiming face {face} (expect a refusal)" : $"Throw die {index + 1} of {host.matchState.Dice.Count} (d{pair.Value.SideCount}, {shown} here)");
                return;
            }

            PrivateNote("Throw: no die.");
        }

        private void GuestVerdict(byte[] bytes)
        {
            if (bytes.Length < 17) return;
            byte[] id = new byte[16];
            Array.Copy(bytes, 0, id, 0, 16);
            PrivateRequestVerdict verdict = (PrivateRequestVerdict)bytes[16];
            byte detail = bytes.Length >= 18 ? bytes[17] : (byte)0;
            Guid requestId = new Guid(id);
            PrivateNote($"Host's answer to request {requestId.ToString().Substring(0, 8)}: {verdict}{(bytes.Length >= 18 && verdict == PrivateRequestVerdict.RuleRefused ? " (" + (TrapFloorShopError)detail + ")" : string.Empty)}.");
            host.ShowMessage($"Host: {verdict}.");
            GuestTransactionVerdict(requestId, verdict, detail);
            GuestPieceVerdict(requestId, verdict);
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
            host.ShowMessage(verdict == TrapFloorEndTurnVerdict.RuleRefused
                ? "Host: the floor must collapse first. Roll the two floor dice, then End Floor Turn."
                : $"Host: End Turn {verdict}.");
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
                    + $"own actions accepted {hostOwnAccepted}, refused {hostOwnRefused}; turn transitions {turnTransitions}; operations accepted {operationsAccepted}, refused {operationsRefused}; sweeps {sweeps} (piece re-settles {pieceResettles}); fallback packages {fallbackPackages}; "
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
            // PRIVACY-P6C: an unfinished payment ends with the turn (its staged cards go back first, as in normal play).
            if (GuestPaymentActive && !staleRound) GuestCancelPayment("Turn ended: the unfinished payment was cancelled.");
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

        // P7 (s157): an Ability this seat owns, lying loose beside its Console (where a bought or won Ability goes), into
        // this seat's first free shown Console slot: the same Transfer request the table's drag of that card sends. The
        // bad request picks another player's owned card (expect WrongSeat).
        internal void PrivateAbilityToConsole(bool others = false)
        {
            RecipientSeatLayout seat = OwnSeat();
            if (!InPrivateSession || seat == null || host.matchState == null) { PrivateNote("Ability: no seat of yours in the private table."); return; }
            PlayerId me = new PlayerId(myPlayer);
            TabletopObjectId found = TabletopObjectId.Empty;
            string name = string.Empty;
            foreach (CardInstanceState card in host.matchState.Cards.Values)
            {
                TabletopObjectState state = card.BaseState;
                if (!state.ContainerId.IsEmpty || state.OwnerPlayerId.IsEmpty || (state.OwnerPlayerId == me) == others) continue;
                if (state.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) continue;
                if (!host.TryGetCardLook(state.DefinitionId, out ConsoleCards.Definitions.CardDefinition definition)
                    || definition.ConsoleBehavior != ConsoleCardBehavior.ActivateOnInsert) continue;
                found = state.Id;
                name = definition.DisplayName;
                break;
            }

            if (found.IsEmpty)
            {
                PrivateNote("Ability: no owned Ability lying loose beside " + (others ? "another player's" : "your") + " Console (buy one in the Shop or take one from the floor first).");
                return;
            }

            ContainerId slot = OwnFreeShownSlot();
            if (slot.IsEmpty) { PrivateNote("Ability: no free Console slot."); return; }
            SendPrivateRequest(PrivateOperationCodec.Write(new PrivateOperation
            {
                Kind = PrivateOperationKind.Transfer, RequestId = Guid.NewGuid(), Card = PanelRef(found), Container = ContainerId.Empty, Destination = slot,
            }), others ? $"{name} from ANOTHER player (expect WrongSeat)" : $"{name} → Console");
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

        private void RaisePrivateConsoleInsertion(PlayerId sender, TabletopObjectId card, ContainerId slot)
        {
            if (card.IsEmpty || slot.IsEmpty || !host.matchState.Cards.TryGetValue(card, out CardInstanceState placed) || placed.BaseState.ContainerId != slot) return;
            host.HandleConsoleCardInteractionAccepted(new ConsoleCards.Core.Events.ConsoleCardInserted(
                new ConsoleCards.Core.Events.DomainEventContext(host.matchState.Id, host.matchState.Revision),
                sender, card, placed.BaseState.DefinitionId, sender, slot));
        }

        private void SendPrivateRequest(byte[] request, string what)
        {
            if (request[0] != PrivateRequestCodec.KindEndTurn) lastRequestBytes = request;
            lastRequestId = new Guid(new ReadOnlySpan<byte>(request, 1, 16));
            if (privateHosting)
            {
                // PRIVACY-P6A: the host's own panel goes through the same pipeline as a guest's request.
                PrivateNote($"Host's own {what} (request {lastRequestId.ToString().Substring(0, 8)}).");
                if (PrivateTransactionCodec.IsTransaction(request, 0))
                {
                    Member self = HostMember();
                    PrivateRequestVerdict tv = self != null && PrivateTransactionCodec.TryRead(request, 0, out PrivateTransaction tx) ? RunPrivateTransaction(self, tx, null, out _, out _) : PrivateRequestVerdict.UnknownReference;
                    if (tv != PrivateRequestVerdict.Accepted) host.ShowMessage($"Private session: {what} refused ({tv}).");
                    return;
                }

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
        internal void GuestRequestAbilityDraw() { }
        internal void ShowPrivateGuestHud() { }
        internal void BeginPrivateTransaction() { }
        internal void RefreshPrivateGuestShopEntry() { }
        internal void EndPrivateTransaction(string what) { }
        internal bool TryRunHostRuleCommand<TCommand, TResult>(TCommand command, out TResult result) where TCommand : class { result = default; return false; }
        internal void PrivateRuleVoteChanged(Guid proposal, bool open, string current, string preview, IReadOnlyList<PlayerId> voters, IReadOnlyList<bool> agreed, string message) { }
        private void PrivateMemberGone(Member member, bool saidGoodbye) { }
        private void PrivateMemberBack(Member member, bool sameRunningCopy) { }
        internal bool TrySendPrivatePrompt(PlayerId actor, FocusedCardRevealModel model) => false;
        internal bool TrySendPrivateSelection(PlayerId actor, FocusedCardSelectionModel model) => false;
        internal bool CanSendPrivateSelection(PlayerId actor) => false;
        internal bool HasPendingPrivateSelection(PlayerId actor, string title) => false;
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
            // P7: the game's own rule commands (the host's cartridge) run on the authority and are published.
            if (online.TryRunHostRuleCommand(command, out result)) return true;

            if (PoseOnly.Contains(typeof(TCommand).Name)) return false;
            int seen = refused.TryGetValue(typeof(TCommand), out int count) ? count : 0;
            refused[typeof(TCommand)] = seen + 1;
            if (seen == 0) Debug.LogWarning($"[PrivateSession] Refused the host's own {typeof(TCommand).Name}: no private request for it yet; it is not run on the host's table alone.");
            // PRIVACY-P6C: a refused command reports a refusal (a default result reads as Accepted).
            result = RefusedResults.Of<TResult>();
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
            // PRIVACY-P6C: the Shop and Payment (the game's own flow) and its bad requests.
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Shop: stage")) Online.PrivateShopStage();
            if (GUILayout.Button("Confirm Payment")) Online.PrivateShopConfirm();
            if (GUILayout.Button("Cancel Payment")) Online.PrivateShopCancel();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bad: Confirm again")) Online.PrivateShopConfirmAgain();
            if (GUILayout.Button("Bad: underpay")) Online.PrivateShopUnderpay();
            if (GUILayout.Button("Bad: pay w/ other's")) Online.PrivateShopOthersCards();
            if (Online.IsPrivateHost && GUILayout.Button("Assist: " + PlayAssist.Name(PlayAssist.Level))) Online.PrivateCycleAssist();
            GUILayout.EndHorizontal();
            // P7 (s154): pawn and die requests (the same ones the table's drag and throw send).
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pawn +X")) Online.PrivatePawnStep(1, 0);
            if (GUILayout.Button("Pawn -X")) Online.PrivatePawnStep(-1, 0);
            if (GUILayout.Button("Pawn +Y")) Online.PrivatePawnStep(0, 1);
            if (GUILayout.Button("Pawn -Y")) Online.PrivatePawnStep(0, -1);
            if (GUILayout.Button("Bad: diag")) Online.PrivatePawnStep(1, 1);
            if (GUILayout.Button("Bad: other's pawn")) Online.PrivatePawnStep(1, 0, true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Throw die")) Online.PrivateThrowDie();
            if (GUILayout.Button("Bad: die face 99")) Online.PrivateThrowDie(99);
            // P7 (s157): an owned Ability from this seat's own area into a free Console slot (the same Place request the
            // table's drag sends; the host's game then offers it to this seat only).
            if (GUILayout.Button("Ability → Console")) Online.PrivateAbilityToConsole();
            if (GUILayout.Button("Bad: other's Ability")) Online.PrivateAbilityToConsole(true);
            GUILayout.EndHorizontal();
        }

        private void OnGUI()
        {
            if (Online == null || !PrivateDevSession.Enabled || PrivateDevSession.PanelHidden) return;
            const float width = 560f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 12f, 12f, width, folded ? 80f : 620f), GUI.skin.box);
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
#if UNITY_EDITOR
                    // P7 (s159): the scenario seam in the panel, not an Editor menu. A native menu held open blocks the
                    // Editor's player loop, and the guests' connections time out. Setup only; the step onto the target is
                    // still made through the production request path (Pawn ±X/Y on the seat whose turn it is).
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Scenario:", GUILayout.Width(62f));
                    if (GUILayout.Button("→Key")) Online.ScenarioStage("Key");
                    if (GUILayout.Button("→Grand")) Online.ScenarioStage("Grand Exit");
                    if (GUILayout.Button("→Secret")) Online.ScenarioStage("Secret Exit");
                    if (GUILayout.Button("→Fall")) Online.ScenarioStage("Fall Trap");
                    if (GUILayout.Button("Report")) Online.ScenarioReport();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("→Slow")) Online.ScenarioStage("Slow Trap");
                    if (GUILayout.Button("→Spring")) Online.ScenarioStage("Spring Trap");
                    if (GUILayout.Button("→Crane")) Online.ScenarioStage("Crane Trap");
                    if (GUILayout.Button("→Blind")) Online.ScenarioStage("Blind Trap");
                    if (GUILayout.Button("→Sticky")) Online.ScenarioStage("Sticky Trap");
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Dice at:", GUILayout.Width(62f));
                    if (GUILayout.Button("Grand Exit")) Online.ScenarioAimDice("Grand Exit");
                    if (GUILayout.Button("Secret Exit")) Online.ScenarioAimDice("Secret Exit");
                    GUILayout.EndHorizontal();
#endif
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
