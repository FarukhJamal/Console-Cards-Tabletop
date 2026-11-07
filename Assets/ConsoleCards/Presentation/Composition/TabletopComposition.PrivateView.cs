using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.GameTemplates.Privacy;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// How the private view drew its cards (PRIVACY-SCENE diagnostics). The cases stay apart, and each set counts
    /// cards, not drawing passes: a card styled twice is still one card.
    /// </summary>
    internal sealed class PrivateViewDiagnostics
    {
        public readonly HashSet<TabletopObjectId> HiddenBacks = new HashSet<TabletopObjectId>();       // A: legitimately hidden
        public readonly HashSet<TabletopObjectId> MissingDefinition = new HashSet<TabletopObjectId>(); // B: identity sent, not known here
        public readonly HashSet<TabletopObjectId> MissingFrontArt = new HashSet<TabletopObjectId>();   // C: known card, no picture
        public readonly HashSet<TabletopObjectId> PublicFaces = new HashSet<TabletopObjectId>();       // public cards with their own look
        public int MissingViews;                                                                        // D: a card of the table got no view
        public int ArtPasses;                                                                           // how many times a card was styled

        public void Note(HashSet<TabletopObjectId> set, TabletopObjectId id)
        {
            ArtPasses++;
            set.Add(id);
        }
    }

    /// <summary>
    /// PRIVACY-SCENE-2 (s147): while a private view is shown, every table command is refused here. The filtered copy
    /// is never changed by the legacy use cases, and nothing falls back to a full-state command. Results are the same
    /// "not run" results the replicated door gives (Succeeded is false).
    /// </summary>
    internal sealed class PrivateViewRefusingAuthority : ITableAuthority
    {
        private readonly Func<ITableAuthority> listeners;
        private readonly Dictionary<Type, object> notRun = new Dictionary<Type, object>();
        private readonly Dictionary<Type, int> refusedByType = new Dictionary<Type, int>();

        public string RefusedSummary()
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<Type, int> pair in refusedByType) parts.Add(pair.Key.Name + " " + pair.Value);
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        public PrivateViewRefusingAuthority(Func<ITableAuthority> listeners)
        {
            this.listeners = listeners;
        }

        public int Refused { get; private set; }
        public string LastRefused { get; private set; }

        public event Action<object> CommandAccepted
        {
            add => listeners().CommandAccepted += value;
            remove => listeners().CommandAccepted -= value;
        }

        public void Observe<TCommand, TResult>(Action<TCommand, TResult> listener) where TCommand : class =>
            listeners().Observe(listener);

        public bool CanSubmit<TCommand>() where TCommand : class => false;

        /// <summary>
        /// PRIVACY-P6B: set on a private guest. A table gesture it can describe (a card drag, a draw, a flip, a shuffle,
        /// a hand reorder) is sent to the host as a private request and true comes back: nothing runs here, the host's
        /// change set moves the cards. Everything else is still refused.
        /// </summary>
        public Func<object, bool> Forward;

        public int Forwarded { get; private set; }

        public TResult Submit<TCommand, TResult>(TCommand command) where TCommand : class
        {
            if (Forward != null && Forward(command))
            {
                Forwarded++;
                return NotRun<TResult>();
            }

            Refused++;
            LastRefused = typeof(TCommand).Name;
            int seen = refusedByType.TryGetValue(typeof(TCommand), out int count) ? count : 0;
            refusedByType[typeof(TCommand)] = seen + 1;
            // Once per command type (a settling piece asks every frame): the count is in the report.
            if (seen == 0) Debug.LogWarning($"[PrivateView] Refused {typeof(TCommand).Name}: the private view has no private request for it yet, and it never runs a table command on its copy.");
            return NotRun<TResult>();
        }

        private TResult NotRun<TResult>()
        {
            if (typeof(TResult).IsValueType) return default;
            if (!notRun.TryGetValue(typeof(TResult), out object result))
            {
                result = FormatterServices.GetUninitializedObject(typeof(TResult));
                notRun.Add(typeof(TResult), result);
            }

            return (TResult)result;
        }
    }

    public sealed partial class TabletopComposition
    {
        // PRIVACY-SCENE (s146/s147). The fields exist in every build; only the Editor entry below can turn the view on,
        // and TryReplaceTable turns it off again.
        internal bool privateViewActive;
        internal Texture2D privateViewCardBack;
        internal Texture2D privateViewBoardPicture;
        internal PrivateViewDiagnostics privateViewDiagnostics = new PrivateViewDiagnostics();
        // What the received package says (never the host's objects): the public table template and the card labels.
        internal RecipientPublicLayout privateViewLayout;
        internal readonly List<RecipientLabel> privateViewLabels = new List<RecipientLabel>();
        // The recipient's own identity (a client knows who it is; the package does not have to say).
        internal PlayerId privateViewRecipient;
        private readonly List<CartridgeSeat> privateViewSeats = new List<CartridgeSeat>(4);
        private PrivateViewRefusingAuthority privateViewAuthority;

        internal PrivateViewRefusingAuthority PrivateViewAuthority =>
            privateViewAuthority ??= new PrivateViewRefusingAuthority(() => replicatedAuthority);

        /// <summary>The seats the table builds its Consoles, hands and piles for: the game's, or the private view's public ones.</summary>
        internal IReadOnlyList<CartridgeSeat> TableSeats => privateViewActive ? privateViewSeats : ActiveCartridge.Seats;

        internal CartridgeSeat TableSeatAtLayoutIndex(int layoutSeatIndex)
        {
            if (!privateViewActive) return ActiveCartridge.Seats[layoutSeatIndex];
            for (int i = 0; i < privateViewSeats.Count; i++)
                if (privateViewSeats[i].LayoutSeatIndex == layoutSeatIndex) return privateViewSeats[i];
            throw new InvalidOperationException($"The private view has no seat {layoutSeatIndex + 1}.");
        }

        internal bool TryGetTableBoardGrid(out GridDefinitionData grid)
        {
            if (!privateViewActive) return ActiveCartridge.TryGetBoardGrid(out grid);
            grid = privateViewLayout?.Board?.Grid;
            return grid != null;
        }

        internal int TableBoardRemovedSpaces =>
            privateViewActive ? (privateViewLayout?.Board?.RemovedSpaces ?? 0) : ActiveCartridge.BoardRemovedSpaces;

        internal Vector2 TableBoardMatSize =>
            privateViewActive
                ? (privateViewLayout?.Board != null ? new Vector2(privateViewLayout.Board.MatWidth, privateViewLayout.Board.MatDepth) : Vector2.zero)
                : ActiveCartridge.BoardMatSize;

        // Inside the Empty Table build, before any piece view: the package's labels, then the board under the pieces.
        private void PreparePrivateViewTable()
        {
            for (int i = 0; i < privateViewLabels.Count; i++)
                labelsByCardId[privateViewLabels[i].Reference] = privateViewLabels[i].Text;

            RecipientBoardLayout board = privateViewLayout?.Board;
            if (board == null || board.CentralPlayArea.IsEmpty || !matchState.PlayAreas.ContainsKey(board.CentralPlayArea)) return;
            centralPlayAreaId = board.CentralPlayArea;
            ProjectTemplateBoardSurface(); // the game table's own board placement and mat (no cartridge involved)
        }

        // Inside the Empty Table build, after the piece views: each public seat's Console, hand and piles, built by the
        // game table's own builders from the public table template.
        private void BuildPrivateViewSeats()
        {
            privateViewSeats.Clear();
            handContainerId = ContainerId.Empty;
            localPlayerLayoutSeatIndex = -1;
            localSeatId = SeatId.Empty;
            if (privateViewLayout == null) return;
            List<RecipientSeatLayout> ordered = new List<RecipientSeatLayout>(privateViewLayout.Seats);
            ordered.Sort((a, b) => a.LayoutSeatIndex.CompareTo(b.LayoutSeatIndex));
            foreach (RecipientSeatLayout seat in ordered)
            {
                if (!matchState.Seats.TryGetValue(seat.Seat, out SeatState state)) continue;
                privateViewSeats.Add(new CartridgeSeat(seat.LayoutSeatIndex, seat.Seat, seat.Hand, seat.ControllerDeck, seat.ActionArea, seat.Pawn, state.ConsolePose));
                if (!privateViewRecipient.IsEmpty && state.OccupantPlayerId == privateViewRecipient)
                {
                    handContainerId = seat.Hand;
                    localPlayerLayoutSeatIndex = seat.LayoutSeatIndex;
                    localSeatId = seat.Seat;
                }
            }

            if (!privateViewRecipient.IsEmpty) localPlayerId = privateViewRecipient;
            BuildContainerViews();
            BindContainerViews();
            RefreshCardContentVisibility();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// PRIVACY-P4 (s148): the client half of a private view, shared by the Editor view and the networked private
        /// development session (Editor and development builds only). Builds this scene from one received package alone,
        /// as an Empty Table session with the public table template. The card back and the board picture are the
        /// game's public content on this computer.
        /// </summary>
        internal bool BuildPrivateRecipientScene(
            RecipientBootstrapPackage received,
            PlayerId recipient,
            Texture2D cardBack,
            Texture2D boardPicture,
            out RecipientClientTable client,
            out string error)
        {
            client = null;
            error = null;
            try
            {
                client = RecipientClientTable.Build(received); // checks the host's digest and the template
            }
            catch (Exception exception)
            {
                error = "The package was refused: " + exception.Message;
                return false;
            }

            if (!SessionService.TryReplaceTable(TabletopSessionSelection.EmptyCustom)) // also takes down a previous private view
            {
                error = "The Empty Table session could not be made: " + (gameTemplatesPanelError ?? "unknown");
                return false;
            }

            privateViewCardBack = cardBack;
            privateViewBoardPicture = boardPicture;
            privateViewDiagnostics = new PrivateViewDiagnostics();
            privateViewLayout = received.Layout;
            privateViewLabels.Clear();
            if (received.Labels != null) privateViewLabels.AddRange(received.Labels);
            privateViewRecipient = recipient;
            privateViewActive = true;
            GameTemplateInitialSnapshot shown = received.Table.ForSession(activeSession.CurrentMatch.Id, activeSession.CurrentMatch.GameTemplateId);
            if (!RestoreOnlineSnapshot(SessionUndoSnapshot.ForRecipientView(shown), Math.Max(1L, received.Table.Revision), out string restoreError))
            {
                privateViewActive = false;
                error = "The scene could not be built from the package: " + restoreError;
                return false;
            }

            return true;
        }

        /// <summary>
        /// PRIVACY-P4: after an update changed this computer's own table, the scene is built again from that table
        /// (the same builders; the views follow the table). Never from anything the host did not send.
        /// </summary>
        internal bool RebuildPrivateSceneFromOwnTable(out string error)
        {
            error = null;
            if (!privateViewActive || matchState == null)
            {
                error = "no private view";
                return false;
            }

            GameTemplateInitialSnapshot own = GameTemplateInitialSnapshot.Capture(matchState);
            if (RestoreOnlineSnapshot(SessionUndoSnapshot.ForRecipientView(own), Math.Max(1L, matchState.Revision + 1), out error)) return true;
            privateViewActive = false;
            return false;
        }

        /// <summary>
        /// PRIVACY-P5 (s149): the narrow scene update for one accepted card event, after it was applied to this
        /// computer's own table. Only the moved card's view is made again (its reference and identity may have changed:
        /// a back has no face to keep), and only its source and destination containers lay out again. False when the
        /// views cannot follow (the caller then builds the whole scene from this computer's own table).
        /// </summary>
        internal bool ApplyPrivateEventToScene(RecipientCardEvent ev, out string error)
        {
            error = null;
            if (!privateViewActive || matchState == null) { error = "no private view"; return false; }
            if (ev == null || !matchState.Cards.TryGetValue(ev.Reference, out CardInstanceState card)) { error = "the event's card is not in this table"; return false; }
            try
            {
                bool identified = !card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition);
                string keptLabel = null;
                if (identified && labelsByCardId.TryGetValue(ev.PreviousReference, out string label)) keptLabel = label;
                selectionState.ClearAll(); // as a deleted card's view is released (no selection names the old view)
                // The selection presenter keeps its own list of highlight visuals; it is taken down while all of them are
                // still configured (a released card's visual is cleared) and made again below from the registry.
                inputFrameCoordinator.ClearSelectionPresenter();
                // The old view is found by the id it is bound to: its card state has already left this table, so a
                // lookup through the table would miss it.
                if (HasRuntimeCardInstance(ev.PreviousReference)) ReleaseRuntimeCardInstance(ev.PreviousReference);
                if (!ev.Reference.Equals(ev.PreviousReference) && HasRuntimeCardInstance(ev.Reference)) ReleaseRuntimeCardInstance(ev.Reference);
                labelsByCardId.Remove(ev.Reference);
                if (keptLabel != null) labelsByCardId[ev.Reference] = keptLabel; // a back keeps no name

                CardView view = Spawner.CreateCardView(card, ResolveCardLabel(card), out var selectionVisual);
                Registry.cardViews.Add(view);
                Registry.cardSelectionVisuals.Add(selectionVisual);
                RefreshContainerCardViewSources();
                // Other seats' hands (drawn as backs) keep their own copy of the card views list and are not among the
                // layout views refreshed above: they get the new list too, or they would still hold the released view.
                for (int i = 0; i < Registry.hiddenHandViews.Count; i++)
                    if (Registry.hiddenHandViews[i] != null) Registry.hiddenHandViews[i].SetCardViews(Registry.cardViews);
                if (!ev.From.IsEmpty) ViewSync.SyncContainer(ev.From);
                if (!ev.To.IsEmpty && ev.To != ev.From) ViewSync.SyncContainer(ev.To);
                RefreshCardContentVisibility();
                RefreshAcceptedCardFaces();
                RefreshSelectionPresenterAfterRuntimeProjection();
                Physics.SyncTransforms();
            }
            catch (Exception exception)
            {
                string trace = exception.StackTrace ?? string.Empty;
                error = exception.GetType().Name + ": " + exception.Message + " at " + (trace.Length > 700 ? trace.Substring(0, 700) : trace);
                try
                {
                    // The views are made again by the caller; the selection presenter must not keep a released visual.
                    RefreshSelectionPresenterAfterRuntimeProjection();
                }
                catch (Exception again)
                {
                    error += " (selection presenter: " + again.Message + ")";
                }

                return false; // the caller builds the whole scene from this computer's own table
            }

            // The registry must match the table: one bound view per card, none for a card that is gone.
            int bound = 0;
            for (int i = 0; i < Registry.cardViews.Count; i++)
                if (Registry.cardViews[i] != null && Registry.cardViews[i].IsBound) bound++;
            if (bound != matchState.Cards.Count || !HasRuntimeCardInstance(ev.Reference))
            {
                error = $"card views {bound} for {matchState.Cards.Count} cards";
                return false;
            }

            return true;
        }

        /// <summary>
        /// PRIVACY-P6B (s151): the narrow scene update for one change set already applied to this computer's own table:
        /// the views of the cards it took out are released, the views of the cards it put in are made, pieces it moved
        /// are shown where they lie, and only the containers it touched lay out again. False when the views cannot
        /// follow (the caller then builds the whole scene from this computer's own table).
        /// </summary>
        internal bool ApplyPrivateChangesToScene(PrivateChangeSummary summary, out string error)
        {
            error = null;
            if (!privateViewActive || matchState == null) { error = "no private view"; return false; }
            try
            {
                selectionState.ClearAll();
                inputFrameCoordinator.ClearSelectionPresenter();
                Dictionary<TabletopObjectId, string> kept = new Dictionary<TabletopObjectId, string>();
                foreach (TabletopObjectId id in summary.Released)
                {
                    if (labelsByCardId.TryGetValue(id, out string label)) kept[id] = label;
                    if (HasRuntimeCardInstance(id)) ReleaseRuntimeCardInstance(id);
                    labelsByCardId.Remove(id);
                }

                foreach (TabletopObjectId id in summary.Created)
                {
                    if (!matchState.Cards.TryGetValue(id, out CardInstanceState card)) { error = "a made card is not in this table"; return false; }
                    if (HasRuntimeCardInstance(id)) ReleaseRuntimeCardInstance(id);
                    bool identified = !card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition);
                    if (identified && kept.TryGetValue(id, out string label)) labelsByCardId[id] = label; // a back keeps no name
                    CardView view = Spawner.CreateCardView(card, ResolveCardLabel(card), out var selectionVisual);
                    Registry.cardViews.Add(view);
                    Registry.cardSelectionVisuals.Add(selectionVisual);
                }

                RefreshContainerCardViewSources();
                for (int i = 0; i < Registry.hiddenHandViews.Count; i++)
                    if (Registry.hiddenHandViews[i] != null) Registry.hiddenHandViews[i].SetCardViews(Registry.cardViews);
                foreach (ContainerId container in summary.Containers) ViewSync.SyncContainer(container);
                foreach (TabletopObjectId id in summary.Created)
                    if (matchState.Cards.TryGetValue(id, out CardInstanceState card) && card.BaseState.ContainerId.IsEmpty) PresentAcceptedObject(id);
                foreach (TabletopObjectId id in summary.Moved) PresentAcceptedObject(id);
                RefreshCardContentVisibility();
                RefreshAcceptedCardFaces();
                RefreshSelectionPresenterAfterRuntimeProjection();
                Physics.SyncTransforms();
            }
            catch (Exception exception)
            {
                string trace = exception.StackTrace ?? string.Empty;
                error = exception.GetType().Name + ": " + exception.Message + " at " + (trace.Length > 700 ? trace.Substring(0, 700) : trace);
                try { RefreshSelectionPresenterAfterRuntimeProjection(); }
                catch (Exception again) { error += " (selection presenter: " + again.Message + ")"; }
                return false;
            }

            int bound = 0;
            for (int i = 0; i < Registry.cardViews.Count; i++)
                if (Registry.cardViews[i] != null && Registry.cardViews[i].IsBound) bound++;
            if (bound != matchState.Cards.Count)
            {
                error = $"card views {bound} for {matchState.Cards.Count} cards";
                return false;
            }

            return true;
        }

        private bool HasRuntimeCardInstance(TabletopObjectId id)
        {
            for (int i = 0; i < Registry.runtimeCardInstances.Count; i++)
            {
                CardView view = Registry.runtimeCardInstances[i].View;
                if (view != null && view.ObjectId == id) return true;
            }

            return false;
        }

        // The public table template, from the game table's public structure (its seats and its board). Host side.
        internal RecipientPublicLayout BuildPublicLayout()
        {
            RecipientPublicLayout layout = new RecipientPublicLayout();
            IReadOnlyList<CartridgeSeat> seats = ActiveCartridge.Seats;
            for (int i = 0; i < seats.Count; i++)
            {
                CartridgeSeat seat = seats[i];
                layout.Seats.Add(new RecipientSeatLayout
                {
                    LayoutSeatIndex = seat.LayoutSeatIndex,
                    Seat = seat.SeatId,
                    Hand = seat.HandContainerId,
                    ControllerDeck = seat.ControllerDeckId,
                    ActionArea = seat.ActionAreaContainerId,
                    Pawn = seat.PawnId,
                });
            }

            Vector2 mat = ActiveCartridge.BoardMatSize;
            layout.Board = new RecipientBoardLayout
            {
                CentralPlayArea = centralPlayAreaId,
                Grid = ActiveCartridge.TryGetBoardGrid(out GridDefinitionData grid) ? grid : null,
                RemovedSpaces = ActiveCartridge.BoardRemovedSpaces,
                MatWidth = mat.x,
                MatDepth = mat.y,
            };
            return layout;
        }

        internal static HashSet<string> HiddenOnlyNames(MatchState full, HashSet<TabletopObjectId> hidden, IReadOnlyDictionary<TabletopObjectId, string> labels)
        {
            HashSet<string> visible = new HashSet<string>(), hiddenNames = new HashSet<string>();
            foreach (KeyValuePair<TabletopObjectId, string> pair in labels)
            {
                if (string.IsNullOrEmpty(pair.Value) || !full.Cards.ContainsKey(pair.Key)) continue;
                if (pair.Value.Length < 3) continue; // one- and two-letter names (A, B, X, Y) match any bytes: not evidence
                if (hidden.Contains(pair.Key)) hiddenNames.Add(pair.Value);
                else visible.Add(pair.Value);
            }

            hiddenNames.ExceptWith(visible);
            return hiddenNames;
        }

        // Definitions only hidden cards have (a definition a visible card also has may appear legitimately).
        internal static HashSet<ObjectDefinitionId> HiddenOnlyDefinitions(MatchState full, HashSet<TabletopObjectId> hidden)
        {
            HashSet<ObjectDefinitionId> hiddenDefinitions = new HashSet<ObjectDefinitionId>(), visible = new HashSet<ObjectDefinitionId>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in full.Cards)
                (hidden.Contains(pair.Key) ? hiddenDefinitions : visible).Add(pair.Value.BaseState.DefinitionId);
            hiddenDefinitions.ExceptWith(visible);
            return hiddenDefinitions;
        }

        // Front pictures only hidden cards have (from the game's content; a picture a visible card also shows is excluded).
        internal HashSet<Texture> HiddenOnlyPictures(MatchState full, HashSet<TabletopObjectId> hidden, HashSet<ObjectDefinitionId> hiddenOnlyDefinitions)
        {
            HashSet<Texture> pictures = new HashSet<Texture>(), visible = new HashSet<Texture>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in full.Cards)
            {
                if (!TryGetCardLook(pair.Value.BaseState.DefinitionId, out CardDefinition definition)) continue;
                Texture2D front = FrontArt(definition);
                if (front == null) continue;
                if (hidden.Contains(pair.Key) && hiddenOnlyDefinitions.Contains(pair.Value.BaseState.DefinitionId)) pictures.Add(front);
                else visible.Add(front);
            }

            pictures.ExceptWith(visible);
            if (privateViewCardBack != null) pictures.Remove(privateViewCardBack);
            return pictures;
        }

        internal static bool Contains(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }

            return false;
        }

#endif

#if UNITY_EDITOR
        /// <summary>
        /// Development only (Editor, Play mode, an offline Trap Floor table first): this computer plays the host. The
        /// first call copies the running table into a simulated host (and lays the PRIVACY-SCENE-2 fixture on the
        /// copy); every call then builds the package <paramref name="seatIndex"/>'s player would receive (-1: a
        /// spectator), and rebuilds this scene from that package's bytes alone, as an Empty Table session with the
        /// public table template: no game cartridge, no Trap Floor rules, no seed. The scene is then inspected.
        /// Returns a report (no keys and no real hidden ids).
        /// </summary>
        public static string ShowPrivateRecipientViewForDevelopment(TabletopComposition host, int seatIndex)
        {
            if (host == null) return "[PrivateView] No table in the scene.";
            return host.ShowPrivateRecipientView(seatIndex);
        }

        private string ShowPrivateRecipientView(int seatIndex)
        {
            if (!IsInitialized || matchState == null) return "[PrivateView] Start a Trap Floor table first.";
            if (IsOnlineTable) return "[PrivateView] Refused: only on an offline table (it never touches the network).";
            if (!privateViewActive)
            {
                if (trapFloorTemplate == null || trapFloorTurnState == null) return "[PrivateView] Refused: the table is not a Trap Floor game.";
                PrivateViewSimulatedHost.Capture(this);
            }
            else if (PrivateViewSimulatedHost.Current == null)
            {
                return "[PrivateView] The simulated host is gone: open the main menu and start a Trap Floor table again.";
            }

            PrivateViewSimulatedHost simulated = PrivateViewSimulatedHost.Current;

            // ---- host side (the simulated host's copy of the full table) ----
            MatchState full = simulated.Match;
            PlayerId recipient = seatIndex >= 0 && seatIndex < simulated.Order.Count ? simulated.Order[seatIndex] : RecipientSnapshotProjection.Spectator;
            string who = recipient.IsEmpty ? "a spectator" : $"seat {seatIndex + 1}";
            RecipientSnapshotProjection.Classify(full, recipient, out HashSet<TabletopObjectId> hidden, out _);
            HashSet<string> hiddenOnlyNames = HiddenOnlyNames(full, hidden, simulated.Labels);
            HashSet<ObjectDefinitionId> hiddenOnlyDefinitions = HiddenOnlyDefinitions(full, hidden);
            HashSet<Texture> hiddenOnlyPictures = HiddenOnlyPictures(full, hidden, hiddenOnlyDefinitions);
            List<Guid> hiddenRealIds = new List<Guid>();
            foreach (TabletopObjectId id in hidden) hiddenRealIds.Add(id.Value);
            byte[] bytes = TableWire.Write(RecipientBootstrapPackage.Build(full, simulated.Handles, recipient, simulated.Labels, simulated.Layout), false);
            int idsOnWire = 0;
            foreach (Guid id in hiddenRealIds) if (Contains(bytes, id.ToByteArray())) idsOnWire++;
            int definitionsOnWire = 0;
            foreach (ObjectDefinitionId definition in hiddenOnlyDefinitions) if (Contains(bytes, definition.Value.ToByteArray())) definitionsOnWire++;
            int namesOnWire = 0;
            List<string> wireNames = new List<string>();
            foreach (string name in hiddenOnlyNames) if (Contains(bytes, Encoding.UTF8.GetBytes(name))) { namesOnWire++; wireNames.Add(name); }
            HashSet<string> publicLabelTexts = new HashSet<string>();
            foreach (KeyValuePair<TabletopObjectId, string> pair in simulated.Labels)
                if (!hidden.Contains(pair.Key) && full.Cards.ContainsKey(pair.Key) && !string.IsNullOrEmpty(pair.Value)) publicLabelTexts.Add(pair.Value);

            // ---- client side: everything below uses the received bytes (and the game's public content) only ----
            RecipientBootstrapPackage received = (RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false);
            int refusedBefore = PrivateViewAuthority.Refused;
            if (!BuildPrivateRecipientScene(received, recipient, simulated.CardBack, simulated.BoardPicture, out RecipientClientTable client, out string error))
                return "[PrivateView] " + error;

            ShowMessage($"Private view ({who}): built from that player's package only. Development view: nothing here is sent anywhere.");
            StringBuilder report = new StringBuilder();
            report.Append($"[PrivateView] Scene built for {who} from {bytes.Length} package bytes. ");
            report.Append($"Wire: hidden real ids {idsOnWire}/{hiddenRealIds.Count}, hidden-only definitions {definitionsOnWire}/{hiddenOnlyDefinitions.Count}, hidden-only names {namesOnWire}/{hiddenOnlyNames.Count}. ");
            InspectPrivateScene(report, client, simulated, hidden, hiddenOnlyDefinitions, hiddenOnlyPictures, hiddenOnlyNames, recipient);
            report.Append($"Commands refused while built: {PrivateViewAuthority.Refused - refusedBefore}{(PrivateViewAuthority.Refused > refusedBefore ? " (last: " + PrivateViewAuthority.LastRefused + ")" : string.Empty)}; refused since the first private view: {PrivateViewAuthority.RefusedSummary()}. ");
            report.Append($"Fixture on the host copy: {simulated.FixtureSummary}.");

            StringBuilder detail = new StringBuilder(report.ToString()).AppendLine();
            foreach (string name in wireNames)
            {
                string inside = null;
                foreach (string text in publicLabelTexts) if (text.IndexOf(name, StringComparison.Ordinal) >= 0) { inside = text; break; }
                detail.AppendLine($"wire match '{name.Replace('\n', ' ')}': {(inside != null ? "inside public label '" + inside.Replace('\n', ' ') + "'" : "NOT inside any public label")}");
            }

            WritePrivateViewReport(who, detail.ToString(), false);
            StartCoroutine(ScanPrivateSceneLater(who, hiddenRealIds, hiddenOnlyNames, hiddenOnlyDefinitions, hiddenOnlyPictures));
            return report.ToString();
        }

        // Immediately after the build: the client table, every card view, its state, its definition, its pictures,
        // its labels, the containers it is bound to and the drop targets; then the hands, Consoles and board.
        private void InspectPrivateScene(
            StringBuilder report,
            RecipientClientTable client,
            PrivateViewSimulatedHost simulated,
            HashSet<TabletopObjectId> hidden,
            HashSet<ObjectDefinitionId> hiddenOnlyDefinitions,
            HashSet<Texture> hiddenOnlyPictures,
            HashSet<string> hiddenOnlyNames,
            PlayerId recipient)
        {
            int hiddenCards = 0, hiddenFacePictures = 0, hiddenLabels = 0;
            foreach (ClientCardView card in client.Cards)
            {
                if (!Registry.TryGetCardView(card.Reference, out CardView view) || view == null)
                {
                    privateViewDiagnostics.MissingViews++;
                    continue;
                }

                if (!card.IsHidden) continue;
                hiddenCards++;
                if (CardInspectPanel.TryGetCardVisualReferences(card.Reference, out CardVisualReferences visuals))
                {
                    if (!string.IsNullOrEmpty(visuals.FrontLabel?.text) || !string.IsNullOrEmpty(visuals.BackLabel?.text)) hiddenLabels++;
                    Renderer face = visuals.FaceUpRenderer;
                    if (face != null && face.sharedMaterial != null && face.sharedMaterial.mainTexture != null
                        && face.sharedMaterial.mainTexture != privateViewCardBack) hiddenFacePictures++;
                }
            }

            // The scene's own table and views: never the host's objects, never a hidden real id or hidden-only definition.
            int realIdsInMatch = 0, hiddenDefinitionsInMatch = 0, hostObjectsInViews = 0, viewsOutsideMatch = 0, hiddenIdViews = 0, hiddenDefinitionViews = 0;
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in matchState.Cards)
            {
                if (hidden.Contains(pair.Key)) realIdsInMatch++;
                if (hiddenOnlyDefinitions.Contains(pair.Value.BaseState.DefinitionId)) hiddenDefinitionsInMatch++;
            }

            foreach (CardView view in Registry.cardViews)
            {
                if (view == null || !view.IsBound || view.CardState == null) continue;
                TabletopObjectId id = view.CardState.BaseState.Id;
                if (!matchState.Cards.TryGetValue(id, out CardInstanceState own) || !ReferenceEquals(own, view.CardState)) viewsOutsideMatch++;
                if (simulated.Match.Cards.TryGetValue(id, out CardInstanceState hostCard) && ReferenceEquals(hostCard, view.CardState)) hostObjectsInViews++;
                if (hidden.Contains(id)) hiddenIdViews++;
                if (hiddenOnlyDefinitions.Contains(view.CardState.BaseState.DefinitionId)) hiddenDefinitionViews++;
            }

            int hiddenLabelEntries = 0;
            foreach (KeyValuePair<TabletopObjectId, string> pair in labelsByCardId)
            {
                if (hidden.Contains(pair.Key)) hiddenLabelEntries++;
                else if (pair.Value != null && hiddenOnlyNames.Contains(pair.Value)) hiddenLabelEntries++;
            }

            int hiddenPicturesOnRenderers = CountHiddenPicturesOnRenderers(hiddenOnlyPictures, true);
            int hiddenPicturesAnywhere = CountHiddenPicturesOnRenderers(hiddenOnlyPictures);

            // Containers: every container the views are bound to is the scene's own, with the host's public count.
            int containerMismatch = 0;
            foreach (KeyValuePair<ContainerId, ContainerState> pair in matchState.Containers)
                if (!simulated.Match.Containers.TryGetValue(pair.Key, out ContainerState hostContainer) || hostContainer.Count != pair.Value.Count) containerMismatch++;

            // Hands: this recipient's own hand view lists the references the package gave, in the package's order.
            string ownHand = "none (spectator)";
            if (handView != null && !handContainerId.IsEmpty)
            {
                ContainerState hand = matchState.GetContainer(handContainerId);
                int known = 0;
                for (int i = 0; i < hand.Count; i++)
                    if (matchState.Cards.TryGetValue(hand.GetObjectAt(i), out CardInstanceState card)
                        && !card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) known++;
                ownHand = $"{hand.Count} cards ({known} with their faces), view bound to it: {handView.IsBound && handView.ContainerId == handContainerId}";
            }

            int otherHands = 0, otherHandCards = 0, otherHandFaces = 0;
            foreach (HiddenHandView other in Registry.hiddenHandViews)
            {
                if (other == null || !other.IsBound) continue;
                otherHands++;
                ContainerState hand = other.ContainerState;
                otherHandCards += hand.Count;
                for (int i = 0; i < hand.Count; i++)
                    if (matchState.Cards.TryGetValue(hand.GetObjectAt(i), out CardInstanceState card)
                        && !card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) otherHandFaces++;
            }

            // Consoles: the slots are the prefab's authored slots, bound to the seat's slot containers, with drop targets.
            int slots = 0, boundSlots = 0, slotTargets = 0, occupiedSlots = 0, faceDownSlotCards = 0, faceDownSlotIdentities = 0;
            foreach (ConsoleSlotView slot in Registry.consoleSlotViews)
            {
                slots++;
                if (slot == null || !slot.IsBound || !matchState.Containers.ContainsKey(slot.ContainerId)) continue;
                boundSlots++;
                if (Registry.consoleSlotVisualsByContainerId.TryGetValue(slot.ContainerId, out ConsoleSlotVisual slotVisual)
                    && slotVisual.DropTarget != null && slotVisual.DropTarget.enabled) slotTargets++;
                ContainerState container = slot.ContainerState;
                if (container.Count > 0) occupiedSlots++;
                for (int i = 0; i < container.Count; i++)
                {
                    if (!matchState.Cards.TryGetValue(container.GetObjectAt(i), out CardInstanceState card) || card.Face != CardFace.FaceDown) continue;
                    faceDownSlotCards++;
                    if (!card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) faceDownSlotIdentities++;
                }
            }

            int unboundSlotComponents = 0;
            foreach (ConsoleSlotView any in UnityEngine.Object.FindObjectsByType<ConsoleSlotView>(FindObjectsInactive.Exclude))
                if (!any.IsBound) unboundSlotComponents++;

            // Floor tiles: the revealed one carries its identity, the others are backs (the same for every recipient).
            int revealedTiles = 0, unrevealedTiles = 0;
            foreach (TabletopObjectId tile in simulated.FloorTiles)
            {
                if (!simulated.Match.Cards.TryGetValue(tile, out CardInstanceState hostTile)) continue;
                if (hostTile.Face == CardFace.FaceUp)
                {
                    if (matchState.Cards.TryGetValue(tile, out CardInstanceState shownTile) && shownTile.BaseState.DefinitionId.Equals(hostTile.BaseState.DefinitionId)) revealedTiles++;
                }
                else unrevealedTiles++;
            }

            report.Append($"Client table: {client.Cards.Count} cards ({hiddenCards} hidden); in the scene's match: hidden real ids {realIdsInMatch}, hidden-only definitions {hiddenDefinitionsInMatch}. ");
            report.Append($"Card views: hidden real ids {hiddenIdViews}, hidden-only definitions {hiddenDefinitionViews}, bound to the host's objects {hostObjectsInViews}, bound outside the scene's match {viewsOutsideMatch}; label entries for hidden cards {hiddenLabelEntries}; this scene's card renderers showing a hidden-only picture {hiddenPicturesOnRenderers} (scene-wide this frame {hiddenPicturesAnywhere}: the replaced views are destroyed at the end of the frame, see the scan below). ");
            report.Append($"Drawing: hidden backs {privateViewDiagnostics.HiddenBacks.Count} cards, public faces {privateViewDiagnostics.PublicFaces.Count}, unknown definition {privateViewDiagnostics.MissingDefinition.Count}, missing front picture {privateViewDiagnostics.MissingFrontArt.Count}, cards without a view {privateViewDiagnostics.MissingViews}, art passes {privateViewDiagnostics.ArtPasses}; hidden views with a face picture {hiddenFacePictures}, with a label {hiddenLabels}. ");
            report.Append($"Containers whose count differs from the host's {containerMismatch}. ");
            report.Append($"Own hand: {ownHand}. Other hands: {otherHands} views, {otherHandCards} cards, {otherHandFaces} with a face. ");
            report.Append($"Consoles: {Registry.playerConsoleViews.Count}; slots {slots}, bound {boundSlots}, with drop targets {slotTargets}, occupied {occupiedSlots}; face-down slot cards {faceDownSlotCards} ({faceDownSlotIdentities} with an identity); unbound slot components {unboundSlotComponents}. ");
            report.Append($"Seats {privateViewSeats.Count}; board mat {(templateBoardMat != null ? "yes" : "NO")}; floor tiles revealed with their identity {revealedTiles}, still face down {unrevealedTiles}. ");
            report.Append($"Card back picture: {(privateViewCardBack != null ? "the game's board card back" : "MISSING")}. ");
        }

        // currentOnly: only renderers of this scene's own card views (the host's views being replaced are destroyed at
        // the end of the frame, so right after the build they are still found by a scene-wide search).
        private int CountHiddenPicturesOnRenderers(HashSet<Texture> hiddenOnlyPictures, bool currentOnly = false)
        {
            if (hiddenOnlyPictures.Count == 0) return 0;
            HashSet<CardView> current = currentOnly ? new HashSet<CardView>(Registry.cardViews) : null;
            int found = 0;
            List<Material> materials = new List<Material>(4);
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (renderer == null || renderer.gameObject.scene != gameObject.scene) continue;
                if (current != null)
                {
                    CardView owner = renderer.GetComponentInParent<CardView>(true);
                    if (owner == null || !current.Contains(owner)) continue;
                }

                renderer.GetSharedMaterials(materials);
                foreach (Material material in materials)
                {
                    if (material == null) continue;
                    if (material.mainTexture != null && hiddenOnlyPictures.Contains(material.mainTexture)) found++;
                    else if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") is Texture baseMap && hiddenOnlyPictures.Contains(baseMap)) found++;
                }
            }

            return found;
        }

        private System.Collections.IEnumerator ScanPrivateSceneLater(
            string who,
            List<Guid> hiddenRealIds,
            HashSet<string> hiddenOnlyNames,
            HashSet<ObjectDefinitionId> hiddenOnlyDefinitions,
            HashSet<Texture> hiddenOnlyPictures)
        {
            yield return null;
            yield return null;
            int leakedNames = 0, leakedIds = 0, objects = 0, coincidences = 0;
            Dictionary<string, int> where = new Dictionary<string, int>();
            void Note(GameObject go, string hiddenName, string kind)
            {
                Transform root = go.transform;
                while (root.parent != null && root.parent.parent != null) root = root.parent;
                string key = kind + " '" + go.name + "' contains '" + hiddenName.Replace('\n', ' ') + "', under '" + root.name + "'";
                where[key] = where.TryGetValue(key, out int n) ? n + 1 : 1;
            }

            foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                objects++;
                string name = go.name;
                foreach (Guid id in hiddenRealIds)
                    if (name.IndexOf(id.ToString("N"), StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(id.ToString(), StringComparison.OrdinalIgnoreCase) >= 0) leakedIds++;
                // A hit on a card's own object counts as a leak; a hit on the room, the table or the UI (a hidden card
                // called "Left" against "Front Left Leg") is a name coincidence and is listed apart.
                bool onCard = go.GetComponentInParent<CardView>(true) != null;
                foreach (string hiddenName in hiddenOnlyNames)
                    if (name.IndexOf(hiddenName, StringComparison.Ordinal) >= 0)
                    {
                        if (onCard) leakedNames++; else coincidences++;
                        Note(go, hiddenName, onCard ? "CARD name" : "structural name");
                    }
                TextMesh text = go.GetComponent<TextMesh>();
                if (text != null && !string.IsNullOrEmpty(text.text))
                    foreach (string hiddenName in hiddenOnlyNames) if (text.text.IndexOf(hiddenName, StringComparison.Ordinal) >= 0) { leakedNames++; Note(go, hiddenName, "TextMesh"); }
            }

            // Components, not names: every card view in the scene (bound or not) and every renderer's pictures.
            int viewsWithHiddenDefinition = 0, viewsWithHiddenId = 0;
            HashSet<Guid> hiddenIds = new HashSet<Guid>(hiddenRealIds);
            foreach (CardView view in UnityEngine.Object.FindObjectsByType<CardView>(FindObjectsInactive.Include))
            {
                if (view == null || view.CardState == null) continue;
                if (hiddenIds.Contains(view.CardState.BaseState.Id.Value)) viewsWithHiddenId++;
                if (hiddenOnlyDefinitions.Contains(view.CardState.BaseState.DefinitionId)) viewsWithHiddenDefinition++;
            }

            int pictures = CountHiddenPicturesOnRenderers(hiddenOnlyPictures);
            string line = $"[PrivateView] Scene scan for {who} (2 frames later): {objects} objects; names or texts with a hidden real id {leakedIds}; card objects or texts with a hidden-only card name {leakedNames} (structural name coincidences {coincidences}, listed below); "
                + $"card view components holding a hidden real id {viewsWithHiddenId}, a hidden-only definition {viewsWithHiddenDefinition}; renderers showing a hidden-only picture {pictures}.";
            foreach (KeyValuePair<string, int> pair in where) line += System.Environment.NewLine + $"  hidden-only name in {pair.Key}: {pair.Value}";
            Debug.Log(line);
            WritePrivateViewReport(who, line, true);
        }

        private static void WritePrivateViewReport(string who, string text, bool append)
        {
            try
            {
                string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Logs"));
                System.IO.Directory.CreateDirectory(folder);
                string file = System.IO.Path.Combine(folder, "PrivateView-" + who.Replace(' ', '-') + ".txt");
                if (append) System.IO.File.AppendAllText(file, text + System.Environment.NewLine);
                else System.IO.File.WriteAllText(file, text + System.Environment.NewLine);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PrivateView] The report file could not be written: " + exception.Message);
            }
        }

        /// <summary>
        /// Editor only: the simulated host of the private view. It holds a copy of the full table (with the fixture
        /// laid on it), the turn order, the game's labels, the public table template and its handle table. The client
        /// scene never reads it: each view is built from the bytes of the package made from it. Same process, so the
        /// full table is still in this process's memory (doc 11 §19: no process-isolation claim).
        /// </summary>
        internal sealed class PrivateViewSimulatedHost
        {
            public static PrivateViewSimulatedHost Current { get; private set; }

            public MatchState Match;
            public List<PlayerId> Order;
            public Dictionary<TabletopObjectId, string> Labels;
            public RecipientPublicLayout Layout;
            public RecipientHandleTable Handles;
            public List<TabletopObjectId> FloorTiles;
            public Texture2D CardBack;
            public Texture2D BoardPicture;
            public string FixtureSummary;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void Forget() => Current = null;

            public static void Capture(TabletopComposition table)
            {
                PrivateViewSimulatedHost host = new PrivateViewSimulatedHost
                {
                    Match = GameTemplateInitialSnapshot.Capture(table.matchState).Restore(),
                    Order = new List<PlayerId>(table.trapFloorTurnState.PlayerOrder),
                    Labels = new Dictionary<TabletopObjectId, string>(),
                    Handles = new RecipientHandleTable(RecipientSnapshotProjection.NewSessionKey()),
                    FloorTiles = new List<TabletopObjectId>(table.trapFloorTemplate.FloorCardIds.Values),
                    CardBack = table.CurrentBoardCardBack(),
                    BoardPicture = table.ActiveCartridge.BoardMatPicture,
                };
                foreach (KeyValuePair<TabletopObjectId, string> label in table.trapFloorTemplate.CardLabels) host.Labels[label.Key] = label.Value;
                host.Layout = table.BuildPublicLayout();
                // The Console cards go in the first two slots on the Console's shown side after the first (a slot on
                // the other side holds its cards under the mat, out of sight).
                List<int> frontSlots = new List<int>();
                ConsoleLayoutData consoleLayout = table.GetConsoleLayout();
                for (int i = 1; i < consoleLayout.CardSlots.Count && frontSlots.Count < 2; i++)
                    if (consoleLayout.CardSlots[i].Side != ConsoleMatSide.Back) frontSlots.Add(i);
                RecipientViewFixture.Result fixture = RecipientViewFixture.Apply(host.Match, host.Layout.Seats, host.FloorTiles, 3, frontSlots.Count == 2 ? frontSlots : null);
                host.FixtureSummary = $"{fixture.HandCards} hand cards, Console cards {fixture.ConsoleFaceUp} face up and {fixture.ConsoleFaceDown} face down, "
                    + $"a loose card face up {(!fixture.LooseFaceUp.IsEmpty ? "yes" : "no")} and face down {(!fixture.LooseFaceDown.IsEmpty ? "yes" : "no")}, "
                    + $"revealed tile {(!fixture.RevealedTile.IsEmpty ? "yes" : "no")}, unrevealed tiles {fixture.UnrevealedTiles}"
                    + (fixture.Notes.Count > 0 ? " (" + string.Join("; ", fixture.Notes) + ")" : string.Empty);
                Current = host;
            }

        }
#endif
    }
}
