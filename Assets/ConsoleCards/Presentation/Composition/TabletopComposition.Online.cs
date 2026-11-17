using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MP-2 (owner, 2026-10-27: "complete the multiplayer"): what online play needs from the table. The command door is
    /// shared (<see cref="ReplicatedTableAuthority"/>), new IDs and random numbers inside a command come from the
    /// command (<see cref="CommandScopedIdentitySource"/>), the physics of a piece runs on the computer of the player
    /// who moves it, and a whole table can be taken from (or given to) another computer through the same path Undo
    /// uses. The room, the messages and the seats are <see cref="OnlinePlayPresenter"/>'s. Offline nothing here changes
    /// anything: the door passes every command straight through, as before.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private OnlinePlayPresenter online;
        private ReplicatedTableAuthority replicatedAuthority;
        private readonly CommandScopedIdentitySource commandIds = new CommandScopedIdentitySource();
        private Func<PlayerId, bool> playerHereCheck;
        private Func<bool> initialPhysicsCheck;
        private Func<CommitPhysicalObjectCommand, CommitPhysicalObjectCommand> rulesDieResult;

        // FAIR-DICE: the host player's own throw of a die a rule reads (Full, online) gets the host result a guest's
        // throw gets: the same random source, the same roll lock in a turn and the same re-rolls by the rules.
        private CommitPhysicalObjectCommand HostPlayerRulesDieResult(CommitPhysicalObjectCommand commit)
        {
            if (online == null || !online.IsHosting || !online.TableShared) return null;
            return TrapFloorGame.TryHostRulesDie(commit, out CommitPhysicalObjectCommand hostResult) ? hostResult : null;
        }

        /// <summary>Online play: the room, its players and the shared table (made the first time it is needed).</summary>
        internal OnlinePlayPresenter Online => online ?? (online = new OnlinePlayPresenter(this));

        /// <summary>True when online play has been opened in this run (the presenter exists).</summary>
        internal bool HasOnline => online != null;

        /// <summary>True while this table is shared with other computers.</summary>
        internal bool IsOnlineTable => online != null && online.TableShared;

        /// <summary>AUTH-ET: on a joined computer the End Turn request goes to the host; false offline and on the host.</summary>
        internal bool TrySendEndTurnToHost(ConsoleCards.Games.TrapFloor.TrapFloorEndTurnRequest request) =>
            online != null && online.TrySendEndTurnRequest(request);

        /// <summary>P8A batch 4: the host runs a joined computer's End Turn right now (that player plays elsewhere).</summary>
        internal bool IsActingForAnotherComputer => online != null && online.IsActingForAnotherComputer;

        /// <summary>AUTH-DICE: a joined computer waits for the host's result of a rules die it threw (Full).</summary>
        internal bool RulesDiceAwaitingHost => online != null && online.TableShared && online.RulesDiceAwaitingHost;

        /// <summary>
        /// AUTH-DICE: the host sent back this computer's own die throw with its own result. The step already ran here, so
        /// the die is set to the host's face and resting place in place (same step, same revision) and shown again.
        /// </summary>
        internal void TakeHostDieResult(CommitPhysicalObjectCommand echoed)
        {
            if (matchState == null || echoed?.State == null || !echoed.SettledDieValue.HasValue) return;
            if (!matchState.Dice.TryGetValue(echoed.ObjectId, out DieState die) || !matchState.ContainsObject(echoed.ObjectId)) return;
            TabletopObjectState state = matchState.GetObject(echoed.ObjectId);
            // DICE-LOOP: only a different number is a correction (the host sends the thrower's own throw back unchanged
            // when it already shows the host's result); turning a die that shows the right number would wake it again.
            if (die.CurrentValue == echoed.SettledDieValue.Value) return;
            state.CorrectPhysicalState(echoed.State);
            die.SetAcceptedRoll(new DieRoll(die.SideCount, echoed.SettledDieValue.Value));
            PresentAcceptedObject(echoed.ObjectId);
            Debug.Log($"[Authority] The host's result for the die you threw: {echoed.SettledDieValue.Value}.");
        }

        /// <summary>The shared command door (made with the table's door).</summary>
        internal ReplicatedTableAuthority Replicated
        {
            get
            {
                if (replicatedAuthority == null) _ = TableAuthority;
                return replicatedAuthority;
            }
        }

        /// <summary>The shared command door, or null when the table's door has not been made yet.</summary>
        internal ReplicatedTableAuthority ReplicatedIfMade => replicatedAuthority;

        internal CommandScopedIdentitySource CommandIds => commandIds;

        /// <summary>
        /// MP-4 (owner, 2026-10-27): no longer used to hold back your content. In a room every computer deals from the
        /// host's cards, decks, pictures, house rules and layout: a joined computer reads the host's game saves, copied to
        /// it for the room (<see cref="ContentPackStore.RoomRoot"/>), so all of them deal the same table.
        /// </summary>
        internal bool UseStockContent => false;

        /// <summary>
        /// MP-4: the game saves in use changed (the room's arrived, or this computer left the room): pictures, saved
        /// layouts and the game's cards are read again from the store now in use, and the cards on the table redrawn.
        /// </summary>
        internal void ReloadContentStores()
        {
            if (contentArt != null)
            {
                contentArt.Library.Changed -= HandleArtChanged;
                contentArt.Library.TextureReleased -= HandleArtTextureReleased;
                contentArt.Dispose();
                contentArt = null;
            }

            TrapFloorGame.ForgetLayoutStore();
            if (TableGame == null) return;
            EnsureContentArt();
            try
            {
                SessionService.RegisterGameTemplates();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] The room's cards could not be read: {exception.Message}");
            }

            if (IsInitialized) RefreshAllCardArt();
        }

        // ---------- the door's command scope ----------

        // Online only (the door calls these only when the table is shared): the command's own IDs and random numbers.
        private void BeginCommandScope(CommandId id)
        {
            commandIds.Begin(id.Value);
            authoritativeRandomValueSource?.Reseed(CommandScopedIdentitySource.SeedOf(id.Value));
        }

        private void EndCommandScope()
        {
            commandIds.End();
            online?.OnCommandEnded();
        }

        // ---------- who plays here ----------

        // The physics authority asks before committing a piece: only the computer of the player who moves it does. A
        // new piece's first resting place is decided by the host alone (every computer would otherwise send one).
        private void ConnectPhysicsToOnline()
        {
            if (physicalAuthority == null) return;
            if (playerHereCheck == null) playerHereCheck = IsPlayerOnThisComputer;
            if (initialPhysicsCheck == null) initialPhysicsCheck = MayCommitFirstPhysicalState;
            physicalAuthority.IsLocalPlayer = playerHereCheck;
            physicalAuthority.MayCommitInitialState = initialPhysicsCheck;
            physicalAuthority.RulesDieResult = rulesDieResult ??= HostPlayerRulesDieResult; // FAIR-DICE
            physicalAuthority.IsOutOfPlay = outOfPlayCheck ??= IsPieceOutOfPlay; // P8A eliminated-pawn fix
        }

        private Func<TabletopObjectId, bool> outOfPlayCheck;

        // P8A eliminated-pawn fix (owner, 2026-11-13): a Trap Floor pawn whose player is out this floor rests still.
        // Not in Free (owner, 2026-11-13 16:39: "Free mode should be the same as Tabletop Simulator in terms of
        // permissions and interactions"): there every piece keeps its plain physics.
        private bool IsPieceOutOfPlay(TabletopObjectId id) =>
            trapFloorTemplate != null && !ConsoleCards.Presentation.UI.PlayAssist.IsFree && TrapFloorGame.IsEliminatedPawn(id);

        private bool MayCommitFirstPhysicalState() => online == null || !online.IsClientTable;

        /// <summary>The name a player plays under online (their profile name), else null.</summary>
        internal string OnlinePlayerName(PlayerId player) => online != null ? online.NameOf(player) : null;

        /// <summary>
        /// False (with the reason shown) when this computer may not change the whole table now: a joined player's
        /// Undo, Redo, Reset and new tables are the host's in online play.
        /// </summary>
        internal bool MayChangeWholeTable(string what)
        {
            if (online == null || !online.IsClientTable || online.ApplyingTable) return true;
            ShowMessage($"In online play the host does {what} for everyone.");
            return false;
        }

        // ---------- the table's shape ----------

        internal TableSpec CurrentTableSpec => tableSpec;

        /// <summary>The host's table shape, for this run only (your own saved table is not changed).</summary>
        internal void UseOnlineTableSpec(TableSpec spec)
        {
            if (spec == null || SameTable(spec, tableSpec) || !FindClassicTable()) return;
            TableSpec copy = spec.Copy();
            copy.Clamp();
            if (ShowTable(copy, out string error))
            {
                tableSpec = copy;
                return;
            }

            Debug.LogWarning($"[Online] The host's {TableShapeName(copy)} table could not be built here ({error}); this computer keeps its own.", this);
        }

        private static bool SameTable(TableSpec a, TableSpec b)
        {
            return b != null
                && a.shape == b.shape
                && string.Equals(a.modelId ?? string.Empty, b.modelId ?? string.Empty, StringComparison.Ordinal)
                && Mathf.Approximately(a.width, b.width)
                && Mathf.Approximately(a.depth, b.depth)
                && Mathf.Approximately(a.cornerRadius, b.cornerRadius)
                && a.edge == b.edge
                && Mathf.Approximately(a.edgeHeight, b.edgeHeight)
                && Mathf.Approximately(a.edgeWidth, b.edgeWidth)
                && Mathf.Approximately(a.thickness, b.thickness);
        }

        // ---------- a whole table from another computer ----------

        /// <summary>
        /// Puts another computer's table (the same data an Undo step keeps) on this table, through the path Undo uses:
        /// the views are built again, the camera stays. The Undo history starts from it. False (with the reason) when
        /// it could not be shown; the table before it is put back.
        /// </summary>
        internal bool RestoreOnlineSnapshot(SessionUndoSnapshot snapshot, long revision, out string error)
        {
            error = null;
            if (snapshot == null || activeSession == null)
            {
                error = "there is no table here yet";
                return false;
            }

            MatchState replacement;
            object restoredGameState;
            PendingControllerPurchaseState restoredPurchase;
            try
            {
                replacement = snapshot.Match.Restore(Math.Max(1L, revision));
                restoredGameState = snapshot.GameState?.Restore();
                // B01 (playtest): a payment in progress belongs to this computer's player. Another computer's table never
                // adds one here or wipes ours; it is checked again when it is confirmed.
                PendingControllerPurchaseState local = pendingControllerPurchaseState;
                restoredPurchase = local != null && local.IsActive && IsPlayerOnThisComputer(local.PlayerId) ? local : null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            rebuildingFromUndo = true;
            MatchState previousMatch = activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = SessionService.CaptureUndoSnapshot();
            try
            {
                Shutdown(true);
                activeSession.ReplaceCurrentMatch(replacement);
                pendingRestoredGameState = restoredGameState;
                pendingRestoredControllerPurchaseState = restoredPurchase;
                ShowTableMarks(snapshot.TableMarks);
                InitializeActiveSession(false);
                undoHistory.EstablishBaseline(SessionService.CaptureUndoSnapshot());
                if (runtimeUi != null && runtimeUi.IsGameSelectOpen) runtimeUi.KeepPanelsOnNextRefresh(); // the menu stays open
                SessionService.ShowActiveSessionUi();
                return true;
            }
            catch (Exception exception)
            {
                ShowTableMarks(previousSnapshot.TableMarks);
                RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Online table");
                Debug.LogError($"[Online] The table from the other computer could not be shown: {exception}", this);
                error = exception.Message;
                return false;
            }
            finally
            {
                pendingRestoredGameState = null;
                pendingRestoredControllerPurchaseState = null;
                rebuildingFromUndo = false;
                SessionService.RefreshUndoUi();
            }
        }

        // ---------- LIVE-HELD: cards another computer's player holds out of a deck, a hand or a Console ----------

        private const int MaxRemoteHeldCards = 8;
        private const float RemoteHeldTimeoutSeconds = 1.5f; // no news for this long: the card goes back to its place
        private const float RemoteHeldResponse = 18f; // how fast the card catches up with the pose it was sent (per s)

        private struct RemoteHeldCard
        {
            public PlayerId Player;
            public CardView View;
            public ContainerId Source;
            public Vector3 Position;
            public Quaternion Rotation;
            public float LastAt;
        }

        private readonly RemoteHeldCard[] remoteHeldCards = new RemoteHeldCard[MaxRemoteHeldCards];
        private int remoteHeldCount;

        /// <summary>
        /// LIVE-HELD (owner's online test, 2026-11-18): another computer's player carries a card out of a deck, a hand
        /// or a Console, or lets it go. The card is lifted out of its place here and follows the poses it is sent. It is
        /// shown as this computer already shows it in that container (another seat's hand: its back). Presentation
        /// only: its state stays where it is until the drop's own command moves it.
        /// </summary>
        internal void ShowRemoteHeldCard(PlayerId player, TabletopObjectId cardId, bool holding, Vector3 position, Quaternion rotation)
        {
            if (!IsInitialized) return;
            int index = -1;
            for (int i = 0; i < remoteHeldCount; i++)
            {
                if (remoteHeldCards[i].Player == player)
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0 && (!holding || remoteHeldCards[index].View == null || remoteHeldCards[index].View.ObjectId != cardId))
            {
                EndRemoteHeldCard(index, true);
                index = -1;
            }

            if (!holding
                || float.IsNaN(position.x) || float.IsInfinity(position.x)
                || float.IsNaN(position.y) || float.IsInfinity(position.y)
                || float.IsNaN(position.z) || float.IsInfinity(position.z)
                || float.IsNaN(rotation.w) || float.IsInfinity(rotation.w))
            {
                return;
            }

            rotation = rotation.normalized;
            if (index < 0)
            {
                if (remoteHeldCount >= MaxRemoteHeldCards
                    || !Registry.TryGetCardView(cardId, out CardView view)
                    || view.CardState == null
                    || view.CardState.BaseState.ContainerId.IsEmpty
                    || view.IsPreviewing
                    || ReferenceEquals(containedCardDragCoordinator?.ActiveCardView, view))
                {
                    return;
                }

                index = remoteHeldCount++;
                remoteHeldCards[index].Player = player;
                remoteHeldCards[index].View = view;
                remoteHeldCards[index].Source = view.CardState.BaseState.ContainerId;
            }

            remoteHeldCards[index].Position = position;
            remoteHeldCards[index].Rotation = rotation;
            remoteHeldCards[index].LastAt = Time.unscaledTime;
        }

        // Every frame: each card held elsewhere moves towards its last pose. A card that left its container (its drop
        // was accepted, or the table changed) is left to the table; one with no news for a while goes back.
        internal void TickRemoteHeldCards()
        {
            if (remoteHeldCount == 0) return;
            float now = Time.unscaledTime;
            float blend = 1f - Mathf.Exp(-RemoteHeldResponse * Time.unscaledDeltaTime);
            for (int i = remoteHeldCount - 1; i >= 0; i--)
            {
                CardView view = remoteHeldCards[i].View;
                if (view == null
                    || !view.IsBound
                    || view.CardState == null
                    || view.CardState.BaseState.ContainerId != remoteHeldCards[i].Source
                    || ReferenceEquals(containedCardDragCoordinator?.ActiveCardView, view))
                {
                    EndRemoteHeldCard(i, false);
                    continue;
                }

                if (now - remoteHeldCards[i].LastAt > RemoteHeldTimeoutSeconds || view.IsPreviewing)
                {
                    EndRemoteHeldCard(i, true);
                    continue;
                }

                Transform card = view.transform;
                view.ApplyContainerWorldPose(
                    Vector3.Lerp(card.position, remoteHeldCards[i].Position, blend),
                    Quaternion.Slerp(card.rotation, remoteHeldCards[i].Rotation, blend));
            }
        }

        private void EndRemoteHeldCard(int index, bool putBack)
        {
            CardView view = remoteHeldCards[index].View;
            ContainerId source = remoteHeldCards[index].Source;
            remoteHeldCount--;
            remoteHeldCards[index] = remoteHeldCards[remoteHeldCount];
            remoteHeldCards[remoteHeldCount] = default(RemoteHeldCard);
            if (!putBack
                || view == null
                || !view.IsBound
                || view.CardState == null
                || view.CardState.BaseState.ContainerId != source)
            {
                return;
            }

            // Back to its place: its container lays its cards out again (the face it showed there is kept).
            if (layoutViewLookup != null
                && layoutViewLookup.TryGet(source, out IContainerLayoutView layout)
                && layout != null
                && layout.IsBound)
            {
                layout.ApplyAcceptedLayout();
                return;
            }

            for (int i = 0; i < Registry.hiddenHandViews.Count; i++)
            {
                HiddenHandView hidden = Registry.hiddenHandViews[i];
                if (hidden != null && hidden.IsBound && hidden.ContainerId == source)
                {
                    hidden.ApplyAcceptedLayout();
                    return;
                }
            }
        }
    }
}
