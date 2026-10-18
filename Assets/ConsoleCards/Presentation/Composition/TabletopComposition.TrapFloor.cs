using System;
using System.Collections.Generic;
using System.Text;
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
using ConsoleCards.Games.TrapFloor;
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
    /// <summary>Trap Floor flow: floorfall and collapse dice, turns, waiting Traps, landing flips, prizes, Keys and escape. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        public TrapFloorRoundActionResult CompleteTrapFloorStart()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("Round progression is available only in the active Trap Floor session.");
            }

            TrapFloorRoundActionResult result = trapFloorRoundOrchestrationService.CompleteStart(
                new TrapFloorRoundActionRequest(CreateCommandContext()));
            ShowMessage(result.Succeeded
                ? trapFloorRoundState.CurrentRoundNumber == 1
                    ? "Round 1 Start complete. Each participating Player may now Search once."
                    : $"Round {trapFloorRoundState.CurrentRoundNumber} started. Move your pawns by hand if the round asks for it."
                : $"Start progression rejected: {result.Error}.");
            return result;
        }

        public TrapFloorRoundFloorfallResult TriggerFloorfall()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null || floorfallTargetPresenter == null)
            {
                throw new InvalidOperationException("Floorfall is available only in the active Trap Floor session.");
            }

            TrapFloorRoundFloorfallResult result = trapFloorRoundOrchestrationService.RollFloorfall(
                new TrapFloorRoundActionRequest(CreateCommandContext(
                    physicalFloorfallActor.IsEmpty ? localPlayerId : physicalFloorfallActor)));
            if (!result.Succeeded)
            {
                ShowMessage($"Official Floorfall rejected: {result.Error}.");
                return result;
            }

            TrapFloorFloorfallTarget target = result.Target.Value;
            AnimateAcceptedDieResult(trapFloorTemplate.FloorfallXAxisDieId);
            AnimateAcceptedDieResult(trapFloorTemplate.FloorfallYAxisDieId);
            ShowFloorfallTarget(target.FloorCardId); // a board feedback marker (H3), not a tint on the tile
            ShowMessage(
                $"Floorfall {trapFloorRoundState.AcceptedFloorfallCount}: X {target.XAxisRoll.Value}, Y {target.YAxisRoll.Value} -> {target.Coordinate}.");
            return result;
        }

        private bool BeginPhysicalFloorfall()
        {
            if (physicalFloorfallPending || trapFloorRoundState == null
                || trapFloorRoundState.Phase != TrapFloorRoundPhase.Floorfall
                || !TryGetDieView(trapFloorTemplate.FloorfallXAxisDieId, out DieView x)
                || !TryGetDieView(trapFloorTemplate.FloorfallYAxisDieId, out DieView y)
                || x.PhysicalObject == null || y.PhysicalObject == null
                || x.PhysicalObject.IsHeld || y.PhysicalObject.IsHeld) return false;
            if (physicalFloorfallActor.IsEmpty) physicalFloorfallActor = localPlayerId;
            if (!x.PhysicalObject.Roll(physicalFloorfallActor) || !y.PhysicalObject.Roll(physicalFloorfallActor))
            { physicalFloorfallActor = PlayerId.Empty; return false; }
            physicalFloorfallPending = true;
            ShowMessage("Floorfall Dice rolling physically; waiting for both to settle.");
            return true;
        }

        private void CompletePhysicalFloorfallIfSettled()
        {
            if (!physicalFloorfallPending) return;
            if (trapFloorRoundState == null || trapFloorRoundState.Phase != TrapFloorRoundPhase.Floorfall)
            { physicalFloorfallPending = false; physicalFloorfallActor = PlayerId.Empty; return; }
            DieState x = matchState.Dice[trapFloorTemplate.FloorfallXAxisDieId];
            DieState y = matchState.Dice[trapFloorTemplate.FloorfallYAxisDieId];
            if (x.BaseState.PhysicalState?.Mode != PhysicalObjectMode.Sleeping
                || y.BaseState.PhysicalState?.Mode != PhysicalObjectMode.Sleeping) return;
            physicalFloorfallPending = false;
            if (floorfallService.IsProtectedPhysicalResult(new TrapFloorFloorfallContext(trapFloorRoundState.CurrentRoundNumber)))
            { BeginPhysicalFloorfall(); return; }
            TriggerFloorfall();
            physicalFloorfallActor = PlayerId.Empty;
        }

        private bool BeginPhysicalFloorCollapse()
        {
            if (physicalFloorCollapsePending
                || trapFloorCollapseState == null
                || trapFloorCollapseUseCase == null
                || trapFloorTurnState == null
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.FloorTurn)
            {
                return false;
            }

            if (trapFloorCollapseState.IsBoardExhausted)
            {
                ShowMessage("Collapse Floor rejected: the Board has no usable Floors remaining.");
                return false;
            }

            if (!TryGetOfficialFloorfallDieViews(out DieView xAxisDieView, out DieView yAxisDieView))
            {
                ShowMessage("Collapse Floor rejected: the two official physical d6 are unavailable.");
                return false;
            }

            PlayerId actor = trapFloorTurnState.FloorOperatorPlayerId;
            if (!LaunchPhysicalFloorCollapseDice(xAxisDieView, yAxisDieView, actor))
            {
                ShowMessage("Collapse Floor rejected: both official d6 must be loose and available.");
                return false;
            }

            TrapFloorCollapseResult begin = TableAuthority.Submit<TrapFloorBeginCollapseCommand, TrapFloorCollapseResult>(
                new TrapFloorBeginCollapseCommand(CreateCommandContext(actor)));
            if (!begin.Succeeded)
            {
                ShowMessage($"Collapse Floor rejected: {begin.Error}.");
                return false;
            }

            physicalFloorCollapsePending = true;
            physicalFloorCollapseActor = actor;
            RefreshTrapFloorStatusUi();
            ShowMessage(
                $"{FormatPlayerName(actor)} triggered Floorfall; rolling the two official d6 physically.");
            return true;
        }

        private void CompletePhysicalFloorCollapseIfSettled()
        {
            if (!physicalFloorCollapsePending)
            {
                return;
            }

            if (trapFloorCollapseState == null
                || trapFloorCollapseUseCase == null
                || !trapFloorCollapseState.IsCollapsePending
                || !TryGetOfficialFloorfallDieViews(out DieView xAxisDieView, out DieView yAxisDieView))
            {
                physicalFloorCollapsePending = false;
                physicalFloorCollapseActor = PlayerId.Empty;
                return;
            }

            DieState xAxisDie = matchState.Dice[trapFloorTemplate.FloorfallXAxisDieId];
            DieState yAxisDie = matchState.Dice[trapFloorTemplate.FloorfallYAxisDieId];
            if (xAxisDie.BaseState.PhysicalState?.Mode != PhysicalObjectMode.Sleeping
                || yAxisDie.BaseState.PhysicalState?.Mode != PhysicalObjectMode.Sleeping
                || (ReferenceEquals(
                        xAxisDie.BaseState.PhysicalState,
                        trapFloorCollapseState.LastResolvedXAxisPhysicalState)
                    && ReferenceEquals(
                        yAxisDie.BaseState.PhysicalState,
                        trapFloorCollapseState.LastResolvedYAxisPhysicalState)))
            {
                return;
            }

            TrapFloorCollapseResult result = TableAuthority.Submit<TrapFloorResolveCollapseCommand, TrapFloorCollapseResult>(
                new TrapFloorResolveCollapseCommand(
                    CreateCommandContext(physicalFloorCollapseActor)));
            if (!result.Succeeded)
            {
                physicalFloorCollapsePending = false;
                physicalFloorCollapseActor = PlayerId.Empty;
                ShowMessage($"Collapse Floor rejected: {result.Error}.");
                return;
            }

            if (result.RerollRequired)
            {
                ShowMessage(
                    $"Floor {result.Roll.Coordinate} is already collapsed; rerolling both official d6.");
                if (!LaunchPhysicalFloorCollapseDice(
                        xAxisDieView,
                        yAxisDieView,
                        physicalFloorCollapseActor))
                {
                    physicalFloorCollapsePending = false;
                    physicalFloorCollapseActor = PlayerId.Empty;
                    ShowMessage("Automatic Floorfall reroll could not launch both official d6.");
                }

                RefreshTrapFloorStatusUi();
                return;
            }

            physicalFloorCollapsePending = false;
            physicalFloorCollapseActor = PlayerId.Empty;
            ApplyCollapsedFloorPresentation(result.CollapsedFloor.FloorCardId);
            selectionState?.ClearAll();
            selectionPresenter?.Refresh();
            RefreshTrapFloorStatusUi();
            ShowMessage(
                $"Floorfall rolled {result.Roll.XAxisResult}/{result.Roll.YAxisResult}; "
                + $"Floor {result.Roll.Coordinate} collapsed permanently."
                + (result.BoardExhausted ? " The Board is exhausted." : string.Empty));
        }

        private bool BeginPhysicalBlindDirection()
        {
            if (physicalBlindDirectionPending
                || trapFloorAbilityResolutionService == null
                || !TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                || blindStatus.IsDirectionResolved)
            {
                ShowMessage("Roll Blind Direction is unavailable.");
                return false;
            }

            if (!TryGetAvailableBlindD4(out TabletopObjectId dieId, out DieView dieView))
            {
                ShowMessage("Roll Blind Direction requires an available loose physical d4.");
                return false;
            }

            PlayerId actor = trapFloorTurnState.ActivePlayerId;
            if (!dieView.PhysicalObject.Roll(actor, true))
            {
                ShowMessage("Roll Blind Direction rejected: the d4 is unavailable or controlled.");
                return false;
            }

            physicalBlindDirectionPending = true;
            physicalBlindDirectionActor = actor;
            physicalBlindDirectionDieId = dieId;
            ShowMessage("BLIND — rolling physical d4; waiting for it to settle.");
            return true;
        }

        private void CompletePhysicalBlindDirectionIfSettled()
        {
            if (!physicalBlindDirectionPending) return;
            if (matchState == null
                || trapFloorAbilityResolutionService == null
                || !TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                || blindStatus.IsDirectionResolved
                || physicalBlindDirectionActor != trapFloorTurnState.ActivePlayerId
                || !matchState.Dice.TryGetValue(physicalBlindDirectionDieId, out DieState die))
            {
                CancelPhysicalBlindDirection("Blind direction roll cancelled because its active status changed.");
                return;
            }

            PhysicalObjectState physicalState = die.BaseState.PhysicalState;
            if (physicalState == null
                || (physicalState.Mode != PhysicalObjectMode.Sleeping
                    && physicalState.Mode != PhysicalObjectMode.SleepingUnresolved))
            {
                return;
            }

            if (physicalState.Mode == PhysicalObjectMode.SleepingUnresolved)
            {
                if (TryGetDieView(physicalBlindDirectionDieId, out DieView unresolvedDieView)
                    && unresolvedDieView.PhysicalObject != null
                    && unresolvedDieView.PhysicalObject.Roll(physicalBlindDirectionActor, true))
                {
                    ShowMessage("Blind d4 is cocked; rolling it again.");
                    return;
                }
                CancelPhysicalBlindDirection("Blind direction roll could not resolve the cocked d4.");
                return;
            }

            TrapFloorBlindDirectionRollResult result =
                trapFloorAbilityResolutionService.ResolveBlindDirection(
                    matchState,
                    CreateCommandContext(physicalBlindDirectionActor),
                    physicalBlindDirectionDieId);
            if (!result.Succeeded)
            {
                CancelPhysicalBlindDirection($"Blind direction rejected: {result.Error}.");
                return;
            }

            physicalBlindDirectionPending = false;
            physicalBlindDirectionActor = PlayerId.Empty;
            physicalBlindDirectionDieId = TabletopObjectId.Empty;
            RefreshTrapFloorStatusUi();
            ShowMessage($"BLIND DIRECTION: {result.Direction.ToString().ToUpperInvariant()}");
        }

        private void CancelPhysicalBlindDirection(string message)
        {
            PlayerId actor = physicalBlindDirectionActor;
            physicalBlindDirectionPending = false;
            physicalBlindDirectionActor = PlayerId.Empty;
            physicalBlindDirectionDieId = TabletopObjectId.Empty;
            if (undoTransactionInProgress && matchState != null && !actor.IsEmpty)
            {
                matchState.AdvanceRevision(
                    CommandId.New(),
                    actor,
                    AuthoritativeActionKind.Unspecified,
                    AuthoritativeActionRecordMode.CancelTransaction);
            }
            ShowMessage(message);
        }

        private bool TryGetAvailableBlindD4(
            out TabletopObjectId dieId,
            out DieView dieView)
        {
            if (matchState != null)
            {
                foreach (KeyValuePair<TabletopObjectId, DieState> pair in matchState.Dice)
                {
                    if (pair.Value.SideCount == 4
                        && TryGetDieView(pair.Key, out DieView candidate)
                        && candidate.PhysicalObject != null
                        && candidate.PhysicalObject.OwnsLooseTransform
                        && !candidate.PhysicalObject.IsHeld)
                    {
                        dieId = pair.Key;
                        dieView = candidate;
                        return true;
                    }
                }
            }

            dieId = TabletopObjectId.Empty;
            dieView = null;
            return false;
        }

        private bool TryGetOfficialFloorfallDieViews(
            out DieView xAxisDieView,
            out DieView yAxisDieView)
        {
            xAxisDieView = null;
            yAxisDieView = null;
            return trapFloorTemplate != null
                && TryGetDieView(trapFloorTemplate.FloorfallXAxisDieId, out xAxisDieView)
                && TryGetDieView(trapFloorTemplate.FloorfallYAxisDieId, out yAxisDieView)
                && xAxisDieView.PhysicalObject != null
                && yAxisDieView.PhysicalObject != null
                && !xAxisDieView.PhysicalObject.IsHeld
                && !yAxisDieView.PhysicalObject.IsHeld
                && xAxisDieView.PhysicalObject.OwnsLooseTransform
                && yAxisDieView.PhysicalObject.OwnsLooseTransform
                && !xAxisDieView.BoundState.IsUserLocked
                && !yAxisDieView.BoundState.IsUserLocked;
        }

        private static bool LaunchPhysicalFloorCollapseDice(
            DieView xAxisDieView,
            DieView yAxisDieView,
            PlayerId actor)
        {
            return xAxisDieView.PhysicalObject.Roll(actor, true)
                && yAxisDieView.PhysicalObject.Roll(actor, true);
        }

        public TrapFloorRoundSearchResult SearchFloormasterDeck()
        {
            return SearchFloormasterDeck(localPlayerId);
        }

        public TrapFloorRoundSearchResult SearchFloormasterDeck(PlayerId searchingPlayerId)
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null || floormasterLifecycleState == null)
            {
                throw new InvalidOperationException("Floormaster Search is available only in the active Trap Floor session.");
            }

            TrapFloorRoundSearchResult result = trapFloorRoundOrchestrationService.Search(
                new TrapFloorFloormasterSearchRequest(CreateCommandContext(searchingPlayerId)));
            if (!result.Succeeded)
            {
                string detail = result.Error == TrapFloorRoundOrchestrationError.FloormasterLifecycleRejected
                    ? result.LifecycleError.ToString()
                    : result.Error.ToString();
                ShowMessage($"Floormaster Search rejected: {detail}.");
                return result;
            }

            ApplyLayout(trapFloorTemplate.FloormasterDeckId);
            if (result.ReshuffledDiscard)
            {
                ApplyLayout(trapFloorTemplate.FloormasterDiscardId);
            }

            CardView pendingCardView = FindCardView(result.PendingCard.CardId);
            pendingCardView.ApplyAcceptedState();
            RefreshCardContentVisibility();
            string reshuffleStatus = result.ReshuffledDiscard ? " Discard reshuffled first." : string.Empty;
            ShowMessage(
                $"{FormatPlayerName(result.PendingCard.SearchingPlayerId)} searched: {result.PendingCard.Category} pending.{reshuffleStatus}");
            return result;
        }

        public TrapFloorRoundTriggerResult CompletePendingFloormasterTrigger()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null || floormasterLifecycleState == null)
            {
                throw new InvalidOperationException("Floormaster Trigger completion is available only in the active Trap Floor session.");
            }

            TrapFloorPendingFloormasterCard pendingCard = floormasterLifecycleState.PendingCard;
            if (pendingCard == null)
            {
                ShowMessage("There is no searched card waiting to be resolved.");
                return TrapFloorRoundTriggerResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            TrapFloorRoundTriggerResult result = trapFloorRoundOrchestrationService.CompleteTrigger(
                new CompletePendingFloormasterCardRequest(CreateCommandContext(), pendingCard.CardId));
            if (!result.Succeeded)
            {
                string detail = result.Error == TrapFloorRoundOrchestrationError.FloormasterLifecycleRejected
                    ? result.LifecycleError.ToString()
                    : result.Error.ToString();
                ShowMessage($"The searched card could not be marked done: {detail}.");
                return result;
            }

            ApplyLayout(trapFloorTemplate.FloormasterDiscardId);
            RefreshCardContentVisibility();
            ShowMessage("Searched card discarded. Its effect is carried out by the players at the table.");
            return result;
        }

        public TrapFloorRoundActionResult CompleteFloorfallPhase()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("Floorfall phase progression is available only in the active Trap Floor session.");
            }

            TrapFloorRoundActionResult result = trapFloorRoundOrchestrationService.CompleteFloorfallPhase(
                new TrapFloorRoundActionRequest(CreateCommandContext()));
            ShowMessage(result.Succeeded
                ? "Floorfall done. Check together that your mode's Floorfalls were all rolled."
                : $"Floorfall phase completion rejected: {result.Error}.");
            return result;
        }

        public TrapFloorRoundActionResult CompleteEndPhase()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("End phase progression is available only in the active Trap Floor session.");
            }

            int completedRound = trapFloorRoundState.CurrentRoundNumber;
            TrapFloorRoundActionResult result = trapFloorRoundOrchestrationService.CompleteEnd(
                new TrapFloorRoundActionRequest(CreateCommandContext()));
            if (!result.Succeeded)
            {
                ShowMessage($"End completion rejected: {result.Error}.");
            }
            else if (trapFloorRoundState.IsScheduleCompleted)
            {
                ShowMessage("Round 10 schedule complete. Win/loss remains unresolved.");
            }
            else
            {
                ShowMessage($"Round {completedRound} End acknowledged. Round {trapFloorRoundState.CurrentRoundNumber} begins at Start.");
            }

            return result;
        }

        private void AnimateAcceptedDieResult(TabletopObjectId dieId)
        {
            for (int i = 0; i < dieViews.Count; i++)
            {
                DieView view = dieViews[i];
                if (view == null || !view.IsBound || view.ObjectId != dieId)
                {
                    continue;
                }

                view.ApplyAcceptedState();
                TabletopTransformSnapshot destination = presentationTransitions.Capture(view.transform);
                presentationTransitions.AnimateFromCurrentResult(
                    view.transform,
                    new TabletopTransformSnapshot(
                        destination.Position + (Vector3.up * 0.18f),
                        destination.Rotation * Quaternion.Euler(30f, 210f, 20f),
                        destination.LocalScale),
                    settleDuration,
                    0.12f);
                return;
            }
        }

        // F3: before a turn ends, list what End Turn will apply (waiting Traps, a launch move) and ask.
        private void RequestEndTurn(bool skipped)
        {
            if (trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || runtimeUi == null)
            {
                AdvanceTrapFloorTurn(skipped);
                return;
            }

            RevealWaitingLandingTile(true); // the tile under the pawn turns over first, then the turn ends
            PlayerId actor = trapFloorTurnState.ActivePlayerId;
            List<string> lines = new List<string>(4);
            if (PendingEndTurnEffects(actor, lines) == 0)
            {
                AdvanceTrapFloorTurn(skipped);
                return;
            }

            string body = $"{(skipped ? "Skipping" : "Ending")} {PlayerLabel(actor)}'s turn applies:\n• "
                + string.Join("\n• ", lines)
                + "\n\nPlay a Reaction (Dodge, Shield) first if you want to stop it.";
            Action close = () => runtimeUi?.CloseTabletopPopup();
            runtimeUi.ShowContextMenu(
                new Vector2((Screen.width * 0.5f) - 200f, Screen.height * 0.66f),
                skipped ? "SKIP TURN?" : "END TURN?",
                body,
                new[]
                {
                    new PopupActionOption(
                        skipped ? "Apply and Skip" : "Apply and End Turn",
                        true,
                        () =>
                        {
                            close();
                            AdvanceTrapFloorTurn(skipped);
                        }),
                    new PopupActionOption("Not yet", true, close),
                },
                close,
                _ => close());
        }

        // What End Turn would apply for this player now; fills lines when given. No game state changes.
        private int PendingEndTurnEffects(PlayerId player, List<string> lines)
        {
            if (trapFloorAbilityResolutionState == null || player.IsEmpty)
            {
                return 0;
            }

            int count = 0;
            if (HasPendingLaunchMove(player))
            {
                count++;
                lines?.Add("Your pawn moves onto the lit tile, and that tile flips.");
            }

            IReadOnlyList<TrapFloorTrapResolutionRecord> records = trapFloorAbilityResolutionState.TrapRecords;
            for (int i = 0; i < records.Count; i++)
            {
                TrapFloorTrapResolutionRecord record = records[i];
                if (!record.IsPending
                    || record.AffectedPlayerId != player
                    || !record.Content.HasSupportedAssistedTrapEffect)
                {
                    continue;
                }

                count++;
                lines?.Add($"{record.Content.DisplayName}: {TrapEffectText(record.Content.TrapEffect)}");
            }

            return count;
        }

        private static string TrapEffectText(TrapFloorTrapEffectCategory effect)
        {
            switch (effect)
            {
                case TrapFloorTrapEffectCategory.EliminateForCurrentRound: return "you are ELIMINATED this round.";
                case TrapFloorTrapEffectCategory.BlindNextRound: return "BLIND next round (no draw, random direction).";
                case TrapFloorTrapEffectCategory.SlowNextRound: return "SLOW next round (less movement).";
                case TrapFloorTrapEffectCategory.StickyNextRound: return "STUCK next round.";
                case TrapFloorTrapEffectCategory.SpringLaunch: return "your pawn is launched across the board (off the edge, you fall).";
                case TrapFloorTrapEffectCategory.CraneDrop: return "the Crane drops your pawn where the dice say (roll them first).";
                default: return "applied.";
            }
        }

        private void AdvanceTrapFloorTurn()
        {
            AdvanceTrapFloorTurn(false);
        }

        private void SkipTrapFloorTurn()
        {
            AdvanceTrapFloorTurn(true);
        }

        private void AdvanceTrapFloorTurn(bool skipped)
        {
            EnsureInitialized();
            if (trapFloorTurnState == null || trapFloorTurnService == null)
            {
                ShowMessage("Turn tracking is unavailable outside Trap Floor.");
                return;
            }
            bool wasFloorTurn = trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn;
            bool reactivatesPlayers = wasFloorTurn && HasEliminatedTrapFloorPlayers();
            PlayerId actor = trapFloorTurnState.IsCurrentFloorFailed
                ? trapFloorTurnState.PlayerOrder[0]
                : trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                ? trapFloorTurnState.ActivePlayerId
                : trapFloorTurnState.FloorOperatorPlayerId;
            TrapFloorTurnAdvanceResult result;
            string trapSummary = string.Empty;
            BeginUndoBatch();
            try
            {
                if (trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn)
                {
                    RevealWaitingLandingTile(true); // a tile still waiting turns over before the turn passes
                }

                // A Trap left waiting is applied before the turn passes (G2c); only a Reaction prevents it.
                if (!trapFloorTurnState.IsCurrentFloorFailed
                    && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                    && !ApplyPendingTrapsBeforeTurnEnds(actor, out trapSummary))
                {
                    return;
                }

                result = skipped
                    ? trapFloorTurnService.Skip(matchState, CreateCommandContext(actor))
                    : trapFloorTurnService.Advance(matchState, CreateCommandContext(actor));
            }
            finally
            {
                EndUndoBatch();
            }

            if (!result.Succeeded)
            {
                string action = skipped ? "Skip Turn" : "End Turn";
                ShowMessage($"{trapSummary}{action} rejected: {result.Error}.");
                return;
            }

            pendingLaunchPlayerId = PlayerId.Empty;
            pendingLaunchTargetId = TabletopObjectId.Empty;

            if (trapFloorAbilityResolutionService != null
                && trapFloorAbilityResolutionService.ClearMovementAssistance())
            {
                abilityTargetPresenter?.ClearCurrent();
                if (undoTrackedMatch == matchState && undoHistory.CurrentStateIndex >= 0)
                    undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
            }

            RefreshTrapFloorStatusUi();
            string skip = (skipped ? $"{FormatPlayerName(actor)} skipped. " : string.Empty) + trapSummary;
            if (wasFloorTurn)
            {
                string reactivated = reactivatesPlayers ? " All Players reactivated." : string.Empty;
                ShowMessage(
                    $"ROUND {trapFloorTurnState.CurrentRound} — "
                    + $"{FormatPlayerName(trapFloorTurnState.ActivePlayerId)} turn.{reactivated}");
            }
            else if (trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
            {
                ShowMessage(
                    $"{skip}Floor Turn — {FormatPlayerName(trapFloorTurnState.FloorOperatorPlayerId)} resolves the Floor.");
            }
            else
            {
                int handLimit = trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
                ShowMessage(
                    $"{skip}{FormatPlayerName(trapFloorTurnState.ActivePlayerId)} turn. "
                    + $"Draw up to {handLimit} if needed.");
            }
        }

        // Applies every Trap still waiting on the player, in order, before their turn passes (G2c).
        // Spring/Crane onto a real tile move the Pawn through the move/physical-commit command; a face-down
        // target flips at once and any Trap there is applied too. False keeps the turn (message shown).
        private bool ApplyPendingTrapsBeforeTurnEnds(PlayerId player, out string summary)
        {
            summary = string.Empty;
            if (trapFloorAbilityResolutionState == null
                || trapFloorAbilityResolutionService == null
                || trapFloorTurnService == null)
            {
                return true;
            }

            for (int guard = 0; guard < 64; guard++)
            {
                if (trapFloorTurnState.IsCurrentFloorFailed
                    || trapFloorTurnState.GetPlayerState(player)
                        == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    break;
                }

                if (HasPendingLaunchMove(player))
                {
                    TabletopObjectId target = pendingLaunchTargetId;
                    pendingLaunchPlayerId = PlayerId.Empty;
                    pendingLaunchTargetId = TabletopObjectId.Empty;
                    if (!TryAutoMovePawnToFloor(player, target, out TrapFloorCoordinate targetTile))
                    {
                        ShowMessage("A Trap still has to move your pawn, but the move was rejected. Move it by hand, then End Turn.");
                        return false;
                    }

                    if (trapFloorAbilityResolutionService.ClearMovementAssistance())
                        abilityTargetPresenter?.ClearCurrent();
                    summary += $"Pawn moved to {targetTile}. ";
                    if (TryLandingFlipNow(player, target, out string flipped))
                        summary += $"Flipped {flipped}. ";
                    continue;
                }

                if (!TryGetOldestPendingTrap(player, out TrapFloorTrapResolutionRecord trap))
                    break;

                if (trap.Content.IsLaunchTrap)
                {
                    TrapFloorLaunchResult launch = trapFloorAbilityResolutionService.ResolveLaunchTrap(
                        matchState,
                        CreateCommandContext(player),
                        trap.FloorCardId,
                        trapFloorTurnService);
                    if (!launch.Succeeded)
                    {
                        ShowMessage(launch.Error == TrapFloorLaunchError.DiceNotSettled
                            ? "CRANE — roll both dice and let them settle, then End Turn again."
                            : $"End Turn: {trap.Content.DisplayName} could not be applied ({launch.Error}).");
                        return false;
                    }

                    if (launch.IsEliminated)
                    {
                        summary += $"{trap.Content.DisplayName}: {FormatPlayerName(player).ToUpperInvariant()} ELIMINATED. ";
                        continue;
                    }

                    pendingLaunchPlayerId = player;
                    pendingLaunchTargetId = launch.TargetFloorCardId;
                    continue;
                }

                TrapFloorTurnAdvanceResult applied = trapFloorAbilityResolutionService.ResolveSupportedTrap(
                    matchState,
                    CreateCommandContext(player),
                    trap.FloorCardId,
                    trapFloorTurnService);
                if (!applied.Succeeded)
                {
                    ShowMessage($"End Turn: {trap.Content.DisplayName} could not be applied ({applied.Error}).");
                    return false;
                }

                summary += trap.Content.TrapEffect == TrapFloorTrapEffectCategory.EliminateForCurrentRound
                    ? $"{trap.Content.DisplayName}: {FormatPlayerName(player).ToUpperInvariant()} ELIMINATED. "
                    : $"{trap.Content.DisplayName} applied. ";
            }

            RefreshMovementAssistancePresentation();
            return true;
        }

        private bool HasPendingLaunchMove(PlayerId player)
        {
            TrapFloorDodgeAssistanceState assistance = trapFloorAbilityResolutionState.ActiveDodgeAssistance;
            return pendingLaunchPlayerId == player
                && !pendingLaunchTargetId.IsEmpty
                && assistance != null
                && assistance.PlayerId == player
                && assistance.TargetFloorCardIds.Count == 1
                && assistance.TargetFloorCardIds[0] == pendingLaunchTargetId;
        }

        private bool TryGetOldestPendingTrap(PlayerId player, out TrapFloorTrapResolutionRecord trap)
        {
            IReadOnlyList<TrapFloorTrapResolutionRecord> records = trapFloorAbilityResolutionState.TrapRecords;
            for (int i = 0; i < records.Count; i++)
            {
                TrapFloorTrapResolutionRecord candidate = records[i];
                if (candidate.IsPending
                    && candidate.AffectedPlayerId == player
                    && candidate.Content.HasSupportedAssistedTrapEffect)
                {
                    trap = candidate;
                    return true;
                }
            }

            trap = null;
            return false;
        }

        // Moves the player's Pawn onto a Floor tile through the authority: a physical Pawn by a physical
        // commit resolved on the table surface, otherwise by MoveObject. Never a direct transform write.
        private bool TryAutoMovePawnToFloor(
            PlayerId player,
            TabletopObjectId floorCardId,
            out TrapFloorCoordinate targetTile)
        {
            targetTile = default;
            if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                || !matchState.Pawns.TryGetValue(setup.PawnId, out PawnState pawn)
                || !matchState.Cards.TryGetValue(floorCardId, out CardInstanceState floor)
                || !trapFloorTemplate.TryGetFloorCardState(matchState, floorCardId, out TrapFloorFloorCardState floorState))
            {
                return false;
            }

            targetTile = floorState.Coordinate;
            TabletopPose currentPose = pawn.BaseState.Pose;
            TabletopPose targetPose = new TabletopPose(
                floor.BaseState.Pose.Position,
                currentPose.RotationDegrees,
                currentPose.Layer,
                currentPose.LocalOrder);
            PawnView pawnView = null;
            for (int i = 0; i < pawnViews.Count; i++)
            {
                if (pawnViews[i] != null && pawnViews[i].IsBound && pawnViews[i].ObjectId == pawn.BaseState.Id)
                {
                    pawnView = pawnViews[i];
                    break;
                }
            }

            // F4: remember where the pawn was seen, so it can hop from there to the tile.
            Transform hopVisual = pawnView != null ? pawnView.transform.Find("VisualRoot") : null;
            Vector3 hopStart = hopVisual != null
                ? hopVisual.position
                : pawnView != null ? pawnView.transform.position : Vector3.zero;
            TabletopTransformSnapshot hopRootStart = pawnView != null
                ? presentationTransitions.Capture(pawnView.transform)
                : default;
            bool moved;
            if (pawn.BaseState.PhysicalState != null
                && physicalAuthority != null
                && pawnView != null
                && pawnView.PhysicalObject != null)
            {
                if (!physicalAuthority.Surfaces.TryResolveAuthoredLooseObject(
                        targetPose,
                        player,
                        pawnView.PhysicalObject.PhysicalCollider,
                        pawn.BaseState.IsUserLocked,
                        out PhysicalObjectState placed))
                {
                    return false;
                }

                moved = TableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(
                    new CommitPhysicalObjectCommand(
                        CreateCommandContext(player),
                        pawn.BaseState.Id,
                        placed,
                        pawn.BaseState.PhysicalRevision,
                        null,
                        AuthoritativeActionRecordMode.Transaction)).Succeeded;
            }
            else
            {
                moved = TableAuthority.Submit<MoveObjectCommand, MoveObjectResult>(
                    new MoveObjectCommand(CreateCommandContext(player), pawn.BaseState.Id, targetPose)).Succeeded;
            }

            if (!moved) return false;
            pawnView?.ApplyAcceptedState();
            landingPawnTiles[pawn.BaseState.Id] = targetTile;
            StartPawnHop(pawnView, hopVisual, hopStart, hopRootStart);
            return true;
        }

        // F4: a pawn moved for the player hops from where it was to where it now stands. A physical pawn hops its
        // VisualRoot child only (the body is already placed); otherwise the whole pawn is animated by the
        // Presentation transitions. Never changes game state.
        private void StartPawnHop(
            PawnView pawnView,
            Transform visual,
            Vector3 startWorld,
            TabletopTransformSnapshot rootStart)
        {
            if (pawnView == null) return;
            if (visual == null)
            {
                presentationTransitions?.AnimateFromCurrentResult(pawnView.transform, rootStart, pawnHopSeconds, pawnHopHeight);
                return;
            }

            Vector3 restLocal = visual.localPosition;
            for (int i = pawnHops.Count - 1; i >= 0; i--)
            {
                if (pawnHops[i].Visual == visual)
                {
                    restLocal = pawnHops[i].RestLocal; // a hop already running keeps the true rest place
                    pawnHops.RemoveAt(i);
                }
            }

            pawnHops.Add(new PawnHop
            {
                Visual = visual,
                RestLocal = restLocal,
                StartWorld = startWorld,
                Started = Time.unscaledTime,
            });
        }

        private void LateUpdate()
        {
            cardFlipTurns?.Tick(Time.unscaledTime);
            if (pawnHops.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = pawnHops.Count - 1; i >= 0; i--)
            {
                PawnHop hop = pawnHops[i];
                if (hop.Visual == null)
                {
                    pawnHops.RemoveAt(i);
                    continue;
                }

                float t = Mathf.Clamp01((now - hop.Started) / Mathf.Max(0.05f, pawnHopSeconds));
                if (t >= 1f || hop.Visual.parent == null)
                {
                    hop.Visual.localPosition = hop.RestLocal;
                    pawnHops.RemoveAt(i);
                    continue;
                }

                Vector3 restWorld = hop.Visual.parent.TransformPoint(hop.RestLocal);
                float eased = t * t * (3f - (2f * t));
                hop.Visual.position = Vector3.Lerp(hop.StartWorld, restWorld, eased)
                    + (Vector3.up * (pawnHopHeight * 4f * t * (1f - t)));
            }
        }

        private void FinishPawnHops()
        {
            for (int i = 0; i < pawnHops.Count; i++)
            {
                if (pawnHops[i].Visual != null) pawnHops[i].Visual.localPosition = pawnHops[i].RestLocal;
            }

            pawnHops.Clear();
        }

        // Flips a face-down, usable target at once for the player (same Landing reveal as a hand move).
        private bool TryLandingFlipNow(PlayerId player, TabletopObjectId floorCardId, out string flippedName)
        {
            flippedName = string.Empty;
            if (trapFloorRevealFloorUseCase == null
                || !trapFloorTemplate.TryGetFloorCardState(matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                || floorCard.IsRevealed
                || trapFloorCollapseState.IsCollapsed(floorCardId))
            {
                return false;
            }

            TrapFloorRevealFloorResult result = TableAuthority.Submit<TrapFloorRevealFloorCommand, TrapFloorRevealFloorResult>(
                new TrapFloorRevealFloorCommand(
                    CreateCommandContext(player),
                    floorCardId,
                    TrapFloorSearchKind.Landing,
                    Array.Empty<TabletopObjectId>()));
            if (!result.Succeeded) return false;
            if (TryGetCardView(floorCardId, out CardView floorCardView))
                RevealFloorCardView(floorCardView);
            RefreshCardContentVisibility();
            activeFloorRevealActivity = result.RevealedActivity;
            flippedName = result.FloorCard.Content.DisplayName;
            return true;
        }

        private string TrapFloorActionHelpText()
        {
            switch (trapFloorRoundState.Phase)
            {
                case TrapFloorRoundPhase.Start:
                    return trapFloorRoundState.CurrentRoundNumber == 1
                        ? string.Empty
                        : "Move your pawns by hand as the round starts, then continue.";
                case TrapFloorRoundPhase.Search:
                    return "Choose the player who searches next.";
                case TrapFloorRoundPhase.Trigger:
                    return "Carry out the card's effect at the table, then mark it done.";
                case TrapFloorRoundPhase.Floorfall:
                    return "Roll as many Floorfalls as your mode asks for, then continue.";
                case TrapFloorRoundPhase.End:
                    return "Check together who survived and whether you won, then continue.";
                case TrapFloorRoundPhase.Completed:
                    return "All 10 rounds are played. Decide the result together.";
                default:
                    return string.Empty;
            }
        }

        private void ShowPendingFloormasterCardContextMenu()
        {
            TrapFloorPendingFloormasterCard pendingCard = floormasterLifecycleState.PendingCard;
            bool canComplete = trapFloorRoundState != null
                && trapFloorRoundState.Phase == TrapFloorRoundPhase.Trigger;
            TabletopObjectId targetCardId = pendingCard.CardId;
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "PENDING FLOORMASTER CARD",
                $"Category: {pendingCard.Category}\nCarry out its effect at the table, then mark it done.",
                new[]
                {
                    new PopupActionOption(
                        "Inspect",
                        true,
                        () => OpenCardInspect(targetCardId)),
                    new PopupActionOption(
                        "Card effect done",
                        canComplete,
                        () =>
                        {
                            TrapFloorRoundTriggerResult result = CompletePendingFloormasterTrigger();
                            if (result.Succeeded)
                            {
                                CloseContextMenu();
                            }
                        }),
                    new PopupActionOption(
                        "Duplicate as Generic Card",
                        true,
                        () => BeginDuplicatePlacement(targetCardId)),
                },
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowFloorCardContextMenu()
        {
            if (!trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    contextMenuCardId,
                    out TrapFloorFloorCardState floorCard))
            {
                CloseContextMenu();
                return;
            }

            TabletopObjectId targetCardId = contextMenuCardId;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            bool isCollapsed = trapFloorCollapseState != null
                && trapFloorCollapseState.IsCollapsed(targetCardId);
            if (floorCard.IsRevealed
                && floorCard.Content.HasSupportedAssistedTrapEffect
                && IsTrapFloorTrapPending(targetCardId))
            {
                actions.Add(new PopupActionOption(
                    "Resolve Trap",
                    CanResolveTrapFloorTrap(targetCardId),
                    () => ResolveTrapFloorTrap(targetCardId)));
            }
            else if (trapFloorObjectiveState != null && trapFloorObjectiveUseCase != null)
            {
                if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                    && !trapFloorObjectiveState.TryGetClaim(targetCardId, out _))
                {
                    actions.Add(new PopupActionOption(
                        "Claim Key",
                        !trapFloorObjectiveState.IsWon,
                        () => ClaimTrapFloorKey(targetCardId)));
                }
                else if (floorCard.Content.Category == TrapFloorFloorContentCategory.SecretExit)
                {
                    actions.Add(new PopupActionOption(
                        "Attempt Escape",
                        !trapFloorObjectiveState.IsWon,
                        () => AttemptTrapFloorEscape(targetCardId)));
                }
            }

            actions.Add(new PopupActionOption(
                "Inspect",
                true,
                () => OpenCardInspect(targetCardId)));
            actions.Add(new PopupActionOption(
                "Duplicate as Generic Card",
                true,
                () => BeginDuplicatePlacement(targetCardId)));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                $"FLOOR {floorCard.Coordinate}",
                isCollapsed
                    ? $"COLLAPSED HOLE — Floor {floorCard.Coordinate} is permanently unusable."
                    : floorCard.IsRevealed
                    ? FloorCardContextDescription(floorCard)
                    : "MYSTERY — content is hidden until the turn player's pawn lands on it.",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private string FloorCardContextDescription(TrapFloorFloorCardState floorCard)
        {
            string description =
                $"Revealed: {floorCard.Content.Category} — {floorCard.Content.DisplayName}";
            if (floorCard.Content.Category == TrapFloorFloorContentCategory.Trap)
            {
                if (WasTrapSafelyRevealed(floorCard.ObjectId))
                {
                    return $"{description}\nCAREFUL SEARCH — this reveal did not trigger the Trap; it remains a Trap.";
                }
                if (IsTrapFloorTrapNeutralized(floorCard.ObjectId))
                {
                    return $"{description}\nTRAP NEUTRALIZED — this Floor is safe from assisted Trap consequences.";
                }
                if (IsTrapFloorTrapResolved(floorCard.ObjectId))
                {
                    return $"{description}\nTRAP RESOLVED — its assisted consequence has already been applied or prevented.";
                }
                return $"{description}\n{TrapFloorTrapEffectDescription(floorCard.Content)}";
            }
            if (trapFloorObjectiveState == null)
            {
                return description;
            }

            if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key)
            {
                return trapFloorObjectiveState.TryGetClaim(
                        floorCard.ObjectId,
                        out TrapFloorCollectedKeyState claim)
                    ? $"{description}\nKEY STATUS: CLAIMED by {FormatPlayerName(claim.ClaimedByPlayerId)}."
                    : $"{description}\nKEY STATUS: UNCLAIMED — use Claim Key to collect it.";
            }

            if (floorCard.Content.Category == TrapFloorFloorContentCategory.SecretExit)
            {
                return $"{description}\nKEYS {trapFloorObjectiveState.CollectedKeyCount} / "
                    + trapFloorObjectiveState.RequiredKeyCount;
            }

            return description;
        }

        private bool CanResolveTrapFloorTrap(TabletopObjectId floorCardId)
        {
            return trapFloorTurnState != null
                && trapFloorTurnService != null
                && trapFloorAbilityResolutionService != null
                && trapFloorAbilityResolutionState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.IsPending
                && trap.AffectedPlayerId == trapFloorTurnState.ActivePlayerId;
        }

        private static string TrapFloorTrapEffectDescription(
            TrapFloorFloorContentDefinition content)
        {
            switch (content.TrapEffect)
            {
                case TrapFloorTrapEffectCategory.EliminateForCurrentRound:
                    return "Assisted consequence: eliminate the resolving Player for the current Round.";
                case TrapFloorTrapEffectCategory.BlindNextRound:
                    return "Assisted consequence: no Controller Card draw next Round; roll a d4 for movement direction.";
                case TrapFloorTrapEffectCategory.SlowNextRound:
                    return $"Assisted consequence: movement {FormatSignedModifier(content.MovementModifier.Value)} next Round.";
                case TrapFloorTrapEffectCategory.StickyNextRound:
                    return "Assisted consequence: stay in place during the next Round.";
                case TrapFloorTrapEffectCategory.SpringLaunch:
                    return $"Assisted consequence: launched 1 tile {content.LaunchDirection.ToString().ToLowerInvariant()}; off the grid or onto a hole, out for this floor.";
                case TrapFloorTrapEffectCategory.CraneDrop:
                    return "Assisted consequence: roll both dice (column, row) and move there; onto a hole, out for this floor.";
                default:
                    return "Manual consequence: follow the authored Trap text.";
            }
        }

        private void ResolveTrapFloorTrap(TabletopObjectId floorCardId)
        {
            if (!CanResolveTrapFloorTrap(floorCardId)
                || !trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    floorCardId,
                    out TrapFloorFloorCardState floorCard)
                || !floorCard.IsRevealed
                || !floorCard.Content.HasSupportedAssistedTrapEffect)
            {
                ShowMessage("Resolve Trap is unavailable for this Trap or assistance state.");
                return;
            }

            if (floorCard.Content.IsLaunchTrap)
            {
                ResolveTrapFloorLaunchTrap(floorCardId, floorCard.Content);
                return;
            }

            PlayerId affectedPlayerId = trapFloorTurnState.ActivePlayerId;
            TrapFloorTurnAdvanceResult result = trapFloorAbilityResolutionService.ResolveSupportedTrap(
                matchState,
                CreateCommandContext(affectedPlayerId),
                floorCardId,
                trapFloorTurnService);
            if (!result.Succeeded)
            {
                ShowMessage($"Resolve Trap rejected: {result.Error}.");
                return;
            }

            CloseContextMenu();
            CloseCardInspect();
            RefreshTrapFloorStatusUi();
            if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.BlindNextRound)
            {
                ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be BLIND during "
                    + $"Round {trapFloorTurnState.CurrentRound + 1}.");
            }
            else if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.SlowNextRound)
            {
                ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be SLOWED "
                    + $"during Round {trapFloorTurnState.CurrentRound + 1}.");
            }
            else if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.StickyNextRound)
            {
                ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be STICKY "
                    + $"during Round {trapFloorTurnState.CurrentRound + 1}.");
            }
            else
            {
                ShowMessage($"{FormatPlayerName(affectedPlayerId).ToUpperInvariant()} ELIMINATED");
            }
        }

        // Spring / Crane (G2b): eliminate, or highlight the tile the player must move their Pawn to.
        private void ResolveTrapFloorLaunchTrap(
            TabletopObjectId floorCardId,
            TrapFloorFloorContentDefinition content)
        {
            PlayerId affectedPlayerId = trapFloorTurnState.ActivePlayerId;
            bool spring = content.TrapEffect == TrapFloorTrapEffectCategory.SpringLaunch;
            string trapName = spring ? "SPRING" : "CRANE";
            TrapFloorLaunchResult result = trapFloorAbilityResolutionService.ResolveLaunchTrap(
                matchState,
                CreateCommandContext(affectedPlayerId),
                floorCardId,
                trapFloorTurnService);
            if (!result.Succeeded)
            {
                ShowMessage(result.Error == TrapFloorLaunchError.DiceNotSettled
                    ? "CRANE — roll both dice and let them settle, then choose Resolve Trap again."
                    : $"Resolve Trap rejected: {result.Error}.");
                return;
            }

            CloseContextMenu();
            CloseCardInspect();
            RefreshMovementAssistancePresentation();
            RefreshTrapFloorStatusUi();
            if (result.IsEliminated)
            {
                string where = result.TargetOnGrid
                    ? $"onto a hole at ({result.TargetX},{result.TargetY})"
                    : "off the floor";
                ShowMessage($"{trapName} — {FormatPlayerName(affectedPlayerId).ToUpperInvariant()} launched {where}. ELIMINATED");
                return;
            }

            pendingLaunchPlayerId = affectedPlayerId;
            pendingLaunchTargetId = result.TargetFloorCardId;
            ShowMessage(
                $"{trapName} — move your pawn to ({result.TargetX},{result.TargetY}). "
                + "A face-down tile there flips when you land. End Turn moves it for you.");
        }

        private void ShowFocusedSearchReveal(TrapFloorRevealFloorResult result)
        {
            if (runtimeUi == null
                || matchState == null
                || trapFloorTemplate == null
                || !matchState.Cards.TryGetValue(
                    result.FloorCard.ObjectId,
                    out CardInstanceState card)
                || card.Face != CardFace.FaceUp
                || !trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    result.FloorCard.ObjectId,
                    out TrapFloorFloorCardState revealedFloor)
                || !revealedFloor.IsRevealed)
            {
                ShowMessage("The tile was revealed, but its card could not be shown.");
                return;
            }

            // The tile's own picture is its content's art; its back is the round's floor back (A1).
            Texture backArtwork = CurrentBoardCardBack();
            Texture frontArtwork = null;
            if (TryGetAuthoredCardDefinition(revealedFloor.Content.Id, out CardDefinition contentDefinition))
            {
                frontArtwork = FrontArt(contentDefinition);
            }
            else if (TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out CardDefinition definition))
            {
                Texture2D definitionBack = BackArt(definition);
                backArtwork = definitionBack != null ? definitionBack : backArtwork;
                frontArtwork = FrontArt(definition);
            }

            TabletopObjectId revealedId = revealedFloor.ObjectId;
            bool trapWaiting = !result.TrapSuppressed
                && revealedFloor.Content.HasSupportedAssistedTrapEffect
                && IsTrapFloorTrapPending(revealedId)
                && CanResolveTrapFloorTrap(revealedId);
            string body = result.TrapSuppressed
                ? $"{revealedFloor.Content.DisplayText}\n\nREVEALED SAFELY"
                : trapWaiting
                ? $"{revealedFloor.Content.DisplayText}\n\nResolve it now, or return to play a Reaction first. "
                    + "End Turn applies it if you leave it."
                : revealedFloor.Content.DisplayText;
            const string header = "YOU LANDED ON";
            string state = $"{revealedFloor.Content.Category.ToString().ToUpperInvariant()} • REVEALED";
            FocusedCardRevealModel model = trapWaiting
                ? new FocusedCardRevealModel(
                    revealedId,
                    backArtwork,
                    frontArtwork,
                    header,
                    state,
                    revealedFloor.Content.DisplayName,
                    revealedFloor.Content.DisplayText,
                    body,
                    "Resolve Trap",
                    () =>
                    {
                        CloseFocusedSearchReveal();
                        ResolveTrapFloorTrap(revealedId);
                    },
                    "Return to Tabletop",
                    CloseFocusedSearchReveal)
                : new FocusedCardRevealModel(
                    revealedId,
                    backArtwork,
                    frontArtwork,
                    header,
                    state,
                    revealedFloor.Content.DisplayName,
                    revealedFloor.Content.DisplayText,
                    body,
                    "Return to Tabletop",
                    CloseFocusedSearchReveal);
            runtimeUi.ShowFocusedCardReveal(model);
        }

        private void CloseFocusedSearchReveal()
        {
            runtimeUi?.CloseFocusedCardSelection();
        }

        private void ReplaceCurrentUndoStateForPurchaseAssistance()
        {
            if (undoTrackedMatch != matchState || undoHistory.CurrentStateIndex < 0) return;
            undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
            RefreshUndoUi();
        }

        private void ClaimTrapFloorKey(TabletopObjectId floorCardId)
        {
            if (trapFloorObjectiveUseCase == null)
            {
                ShowMessage("Claim Key unavailable outside Trap Floor.");
                return;
            }

            TrapFloorObjectiveResult result = TableAuthority.Submit<TrapFloorClaimKeyCommand, TrapFloorObjectiveResult>(
                new TrapFloorClaimKeyCommand(CreateCommandContext(), floorCardId));
            if (!result.Succeeded)
            {
                ShowMessage($"Claim Key rejected: {result.Error}.");
                return;
            }

            bool keepInspectOpen = inspectedCardId == floorCardId;
            if (!keepInspectOpen)
            {
                CloseContextMenu();
            }

            RefreshTrapFloorFloorCardPresentation(floorCardId);
            RefreshTrapFloorStatusUi();
            if (keepInspectOpen)
            {
                RefreshCardInspectPopup();
            }

            ShowMessage(
                $"{FormatPlayerName(result.Activity.ActorPlayerId)} claimed "
                + $"{result.FloorCard.Content.DisplayName}. "
                + $"KEYS {result.CollectedKeyCount} / {result.RequiredKeyCount}.");
        }

        private void AttemptTrapFloorEscape(TabletopObjectId floorCardId)
        {
            if (trapFloorObjectiveUseCase == null)
            {
                ShowMessage("Attempt Escape unavailable outside Trap Floor.");
                return;
            }

            TrapFloorObjectiveResult result = TableAuthority.Submit<TrapFloorAttemptEscapeCommand, TrapFloorObjectiveResult>(
                new TrapFloorAttemptEscapeCommand(CreateCommandContext(), floorCardId));
            if (!result.Succeeded)
            {
                if (result.Error == TrapFloorObjectiveError.RequiredKeysMissing)
                {
                    ShowMessage(
                        $"Attempt Escape rejected: Need {result.RequiredKeyCount} Keys — "
                        + $"{result.CollectedKeyCount} claimed.");
                }
                else if (result.Error == TrapFloorObjectiveError.PlayerAlreadyEscaped)
                {
                    ShowMessage("Attempt Escape rejected: This Player has already escaped.");
                }
                else
                {
                    ShowMessage($"Attempt Escape rejected: {result.Error}.");
                }

                return;
            }

            if (result.IsWon)
            {
                ShowTrapFloorVictory(result);
            }
            else
            {
                CloseContextMenu();
            }
            RefreshTrapFloorStatusUi();
            string escapedPlayer = FormatPlayerName(result.EscapedActivity.ActorPlayerId);
            ShowMessage(result.IsWon
                ? $"{escapedPlayer} escaped. TRAP FLOOR VICTORY."
                : $"{escapedPlayer} escaped. ESCAPED {result.EscapedPlayerCount} / "
                    + $"{result.ParticipatingPlayerCount}.");
        }

        private void ShowTrapFloorVictory(TrapFloorObjectiveResult result)
        {
            Vector2 popupPosition = contextMenuAnchorScreenPosition;
            CloseContextMenu();
            Action closeVictory = () => runtimeUi?.CloseTabletopPopup();
            runtimeUi.ShowContextMenu(
                popupPosition,
                "TRAP FLOOR VICTORY",
                $"{FormatPlayerName(result.EscapedActivity.ActorPlayerId)} escaped through "
                    + $"Floor {result.FloorCard.Coordinate}.\n"
                    + $"{CurrentTrapFloorObjectiveProgressText()}",
                new[]
                {
                    new PopupActionOption("Close", true, closeVictory),
                },
                closeVictory,
                _ => closeVictory());
        }

        private void ShowTrapFloorReveal(TrapFloorRevealFloorResult result)
        {
            CloseContextMenu();
            CloseCardInspect();
            activeFloorRevealActivity = result.RevealedActivity;
            inspectedCardId = result.FloorCard.ObjectId;
            inspectedCardRenderedRevision = result.Revision;
            if (!TryBuildCardInspectModel(inspectedCardId, out CardInspectModel model))
            {
                activeFloorRevealActivity = null;
                inspectedCardId = TabletopObjectId.Empty;
                inspectedCardRenderedRevision = -1;
                return;
            }

            runtimeUi.ShowCardInspect(model, CloseCardInspect);
        }

        // G2a: the face-up Ability or Friend tile under the active Player's pawn, if it can still be taken.
        private bool TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prizeTile)
        {
            prizeTile = null;
            if (trapFloorTemplate == null
                || matchState == null
                || trapFloorTurnState == null
                || trapFloorCollapseState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || !TryGetTrapFloorPlayerSetup(
                    trapFloorTurnState.ActivePlayerId,
                    out TrapFloorPlayerSetupDefinition player)
                || !TryReadPawnTile(player.PawnId, out TrapFloorCoordinate tile)
                || !trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)
                || !trapFloorTemplate.TryGetFloorCardState(matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                || !floorCard.IsRevealed
                || trapFloorCollapseState.IsCollapsed(floorCardId)
                || !TrapFloorTakePrizeUseCase.IsPrize(floorCard.Content))
            {
                return false;
            }

            prizeTile = floorCard;
            return true;
        }

        private static string TakePrizeLabel(TrapFloorFloorCardState prizeTile)
        {
            InputCostData cost = TrapFloorTakePrizeUseCase.CostOf(prizeTile.Content);
            return cost.IsEmpty
                ? $"Take {prizeTile.Content.DisplayName}"
                : $"Take {prizeTile.Content.DisplayName} ({InputCostNotation.Format(cost)})";
        }

        // Take Ability / Take Friend (doc 18 §15.4). A free prize is taken at once; a cost opens the Hand picker
        // and then the usual Console payment, confirmed from the turn menu.
        private void BeginTakeFloorPrize()
        {
            CloseContextMenu();
            if (pendingControllerPurchaseState != null && pendingControllerPurchaseState.IsActive)
            {
                ShowMessage("Finish or cancel the current payment first.");
                return;
            }

            if (!TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prizeTile)
                || prizeTile.Content.AuthoredCard == null)
            {
                ShowMessage("Stand on a face-up Ability or Friend to take it.");
                return;
            }

            InputCostData cost = TrapFloorTakePrizeUseCase.CostOf(prizeTile.Content);
            if (cost.IsEmpty)
            {
                CompleteTakeFloorPrize(prizeTile.ObjectId, Array.Empty<TabletopObjectId>());
                RefreshTrapFloorStatusUi();
                return;
            }

            string cardDefinitionStableId = prizeTile.Content.AuthoredCard.StableId;
            if (!TryBuildPurchasePaymentOptions(
                    cardDefinitionStableId,
                    out CardDefinitionData prizeDefinition,
                    out List<PurchasePaymentCardOption> options,
                    out string missing))
            {
                ShowMessage($"{prizeTile.Content.DisplayName} cannot be taken: its cost could not be read.");
                return;
            }

            pendingPurchaseDefinitionStableId = cardDefinitionStableId;
            pendingPurchaseSource = PendingPurchaseSource.Table;
            List<FocusedCardOptionModel> cards =
                new List<FocusedCardOptionModel>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                PurchasePaymentCardOption option = options[i];
                cards.Add(new FocusedCardOptionModel(
                    option.CardId,
                    option.DisplayName,
                    option.Input.HasValue ? option.Input.Value.ToString() : "Not eligible",
                    option.Artwork,
                    option.Eligible));
            }

            string costText = InputCostNotation.Format(prizeDefinition.InputCost);
            runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                $"TAKE {prizeDefinition.DisplayName.ToUpperInvariant()} — HAND",
                $"Choose {costText} from your Hand.",
                string.IsNullOrEmpty(missing)
                    ? "Pick the cards that pay the cost."
                    : $"Missing: {missing}",
                "Confirm Cards",
                cards,
                prizeDefinition.InputCost.CardCount,
                IsPurchasePaymentReady,
                ConfirmFocusedPurchasePayment,
                CancelFocusedPurchaseSelection));
        }

        private void CompleteTakeFloorPrize(
            TabletopObjectId floorCardId,
            IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            if (trapFloorTakePrizeUseCase == null
                || trapFloorTurnState == null
                || !TryGetTrapFloorPlayerSetup(
                    trapFloorTurnState.ActivePlayerId,
                    out TrapFloorPlayerSetupDefinition taker)
                || !matchState.Pawns.TryGetValue(taker.PawnId, out PawnState takerPawn))
            {
                ShowMessage("Taking prizes is unavailable.");
                return;
            }

            TrapFloorTakePrizeResult result = TableAuthority.Submit<TrapFloorTakePrizeCommand, TrapFloorTakePrizeResult>(
                new TrapFloorTakePrizeCommand(
                    CreateCommandContext(trapFloorTurnState.ActivePlayerId),
                    floorCardId,
                    TabletopObjectId.New(),
                    paymentCardIds,
                    PawnTablePosition(takerPawn)));
            if (!result.Succeeded)
            {
                ShowMessage($"Take rejected: {result.Error}.");
                return;
            }

            if (pendingControllerPurchaseState != null && pendingControllerPurchaseState.IsActive)
            {
                pendingControllerPurchaseState.Clear();
            }

            pendingPurchaseDefinitionStableId = string.Empty;
            ReplaceCurrentUndoStateForPurchaseAssistance();

            string prizeName = result.FloorCard.Content.DisplayName;
            labelsByCardId[result.GrantedCardId] = prizeName;
            CardView grantedView = CreateCardView(
                matchState.Cards[result.GrantedCardId],
                prizeName,
                out TabletopSelectionVisual selectionVisual);
            cardViews.Add(grantedView);
            cardSelectionVisuals.Add(selectionVisual);
            physicalAuthority?.Register(grantedView);
            RefreshContainerCardViewSources();
            ApplyLayout(result.DestinationContainerId);
            RefreshSelectionPresenterAfterRuntimeProjection();
            presentationTransitions.Appear(grantedView.transform, settleDuration);
            ApplyCollapsedFloorPresentation(floorCardId);
            string takerName = HandOwnerName(taker.HandContainerId);
            ShowMessage(result.PlacedInHand
                ? $"Took {prizeName} into {takerName}'s hand. The tile is now a hole."
                : $"Took {prizeName}. {takerName}'s hand is full, so it is beside their Console. The tile is now a hole.");
        }

        // G2a: remembers the tile under every pawn, so only a real move onto a new tile flips it.
        private void SyncLandingPawnTiles()
        {
            landingPawnTiles.Clear();
            landingCheckPending = false;
            if (trapFloorTemplate == null || matchState == null) return;
            for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
            {
                TabletopObjectId pawnId = trapFloorTemplate.Players[i].PawnId;
                landingPawnTiles[pawnId] = TryReadPawnTile(pawnId, out TrapFloorCoordinate tile)
                    ? tile
                    : (TrapFloorCoordinate?)null;
            }

            // After Undo, Redo or a seat switch: a landed tile already triggered but still under a pawn keeps
            // waiting to turn over; its new view is held face down again. A tile Undo turned back is no longer waiting.
            TabletopObjectId waitingId = waitingLandingFloorCardId;
            waitingLandingFloorCardId = TabletopObjectId.Empty;
            waitingLandingActor = PlayerId.Empty;
            if (!waitingId.IsEmpty
                && trapFloorTurnState != null
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && landedFloorCards.Contains(waitingId)
                && trapFloorTemplate.TryGetFloorCardState(matchState, waitingId, out TrapFloorFloorCardState waitingCard)
                && waitingCard.IsRevealed
                && (trapFloorCollapseState == null || !trapFloorCollapseState.IsCollapsed(waitingId))
                && IsAnyPawnOnFloorCard(waitingId))
            {
                waitingLandingFloorCardId = waitingId;
                waitingLandingActor = trapFloorTurnState.ActivePlayerId;
                if (TryGetCardView(waitingId, out CardView waitingView)) waitingView.SetHoldFaceDown(true);
            }
        }

        // A landed tile still under a pawn shows face down on its (re)bound view until it turns over.
        private void HoldWaitingLandingTileView()
        {
            if (!waitingLandingFloorCardId.IsEmpty && TryGetCardView(waitingLandingFloorCardId, out CardView view))
            {
                view.SetHoldFaceDown(true);
            }
        }

        private void RevealFloorCardView(CardView floorCardView)
        {
            floorCardView.SetHoldFaceDown(false);
            floorCardView.ApplyAcceptedState();
            BeginCardFlipTurn(floorCardView); // the same turn-over as any card flip
        }

        private bool IsAnyPawnOnFloorCard(TabletopObjectId floorCardId)
        {
            for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
            {
                if (TryReadPawnTile(trapFloorTemplate.Players[i].PawnId, out TrapFloorCoordinate tile)
                    && trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId id)
                    && id == floorCardId)
                {
                    return true;
                }
            }

            return false;
        }

        // Physical part of a landing (owner, 2026-10-15): the tile, already triggered when the pawn landed, turns
        // over on the table once no pawn is on it. At the end of a turn a pawn still on it hops up while it turns
        // (the automatic flip).
        private void RevealWaitingLandingTile(bool turnEnds)
        {
            if (waitingLandingFloorCardId.IsEmpty || trapFloorTemplate == null || matchState == null)
            {
                return;
            }

            TabletopObjectId floorCardId = waitingLandingFloorCardId;
            if (!turnEnds && IsAnyPawnOnFloorCard(floorCardId)) return;
            waitingLandingFloorCardId = TabletopObjectId.Empty;
            waitingLandingActor = PlayerId.Empty;
            if (!TryGetCardView(floorCardId, out CardView floorCardView) || !floorCardView.HoldsFaceDown) return;
            if (turnEnds) HopPawnsOffFloorCard(floorCardId);
            RevealFloorCardView(floorCardView);
        }

        // Trigger part of a landing: the reveal runs at once for the player who landed (the "YOU LANDED ON" popup,
        // effects and messages), while the tile on the table stays face down until the pawn is off it.
        private void TriggerLandedTile(TabletopObjectId floorCardId, PlayerId actor)
        {
            if (trapFloorRevealFloorUseCase == null) return;
            TrapFloorRevealFloorResult result = TableAuthority.Submit<TrapFloorRevealFloorCommand, TrapFloorRevealFloorResult>(
                new TrapFloorRevealFloorCommand(
                    CreateCommandContext(actor),
                    floorCardId,
                    TrapFloorSearchKind.Landing,
                    Array.Empty<TabletopObjectId>()));
            if (!result.Succeeded)
            {
                ShowMessage($"Landing flip rejected: {result.Error}.");
                return;
            }

            bool waits = IsAnyPawnOnFloorCard(floorCardId);
            if (TryGetCardView(floorCardId, out CardView floorCardView))
            {
                if (waits) floorCardView.SetHoldFaceDown(true); // the card is revealed; the tile turns later
                else RevealFloorCardView(floorCardView);
            }

            if (waits)
            {
                waitingLandingFloorCardId = floorCardId;
                waitingLandingActor = actor;
            }

            RefreshCardContentVisibility();
            selectionPresenter?.Refresh();
            activeFloorRevealActivity = result.RevealedActivity;
            ShowFocusedSearchReveal(result);
            RefreshTrapFloorStatusUi();
            ShowMessage(
                $"{FormatPlayerShortName(result.RevealedActivity.ActorPlayerId)} landed on "
                + $"{result.FloorCard.Content.DisplayName}."
                + (waits ? " The tile turns over when the pawn moves off it, or when the turn ends." : string.Empty));
        }

        // The pawn on a tile turning at the end of a turn lifts and lands again in place (Presentation only).
        private void HopPawnsOffFloorCard(TabletopObjectId floorCardId)
        {
            for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
            {
                TabletopObjectId pawnId = trapFloorTemplate.Players[i].PawnId;
                if (!TryReadPawnTile(pawnId, out TrapFloorCoordinate tile)
                    || !trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId id)
                    || id != floorCardId)
                {
                    continue;
                }

                for (int v = 0; v < pawnViews.Count; v++)
                {
                    PawnView pawnView = pawnViews[v];
                    if (pawnView == null || !pawnView.IsBound || pawnView.ObjectId != pawnId) continue;
                    Transform hopVisual = pawnView.transform.Find("VisualRoot");
                    StartPawnHop(
                        pawnView,
                        hopVisual,
                        hopVisual != null ? hopVisual.position : pawnView.transform.position,
                        presentationTransitions.Capture(pawnView.transform));
                    break;
                }
            }
        }

        private bool IsHiddenUsableTile(TrapFloorCoordinate tile)
        {
            return trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)
                && trapFloorTemplate.TryGetFloorCardState(matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                && !floorCard.IsRevealed
                && (trapFloorCollapseState == null || !trapFloorCollapseState.IsCollapsed(floorCardId));
        }

        private bool TryReadPawnTile(TabletopObjectId pawnId, out TrapFloorCoordinate tile)
        {
            tile = default;
            return matchState != null
                && trapFloorTemplate != null
                && matchState.Pawns.TryGetValue(pawnId, out PawnState pawn)
                && trapFloorTemplate.TryGetFloorCoordinateAt(matchState, PawnTablePosition(pawn), out tile);
        }

        // Where a pawn stands on the table. A dragged pawn keeps its position in its physics state (world space);
        // its pose only holds the position it was placed at, so the physics state wins when there is one.
        private TableCoordinate PawnTablePosition(PawnState pawn)
        {
            PhysicalObjectState physical = pawn.BaseState.PhysicalState;
            if (physical != null && coordinateConverter != null)
            {
                return coordinateConverter.ToTableCoordinate(
                    new Vector3(physical.Position.X, physical.Position.Y, physical.Position.Z));
            }

            return pawn.BaseState.Pose.Position;
        }

        // Flip on landing (doc 18 §15.2): when the active Player's pawn has moved onto a new face-down tile, that
        // tile flips for free through the reveal use case. The flip amends the move's Undo step.
        private void CompletePendingLandingFlip()
        {
            if (!landingCheckPending || undoTransactionInProgress) return;
            landingCheckPending = false;
            if (trapFloorTemplate == null
                || matchState == null
                || trapFloorTurnState == null
                || trapFloorRevealFloorUseCase == null
                || trapFloorCollapseState == null)
            {
                return;
            }

            TabletopObjectId activePawnId = TabletopObjectId.Empty;
            if (!trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && TryGetTrapFloorPlayerSetup(trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
            {
                activePawnId = player.PawnId;
            }

            TrapFloorCoordinate? landedTile = null;
            TrapFloorCoordinate? landedFrom = null;
            bool otherPawnOnHiddenTile = false;
            bool otherPawnMoved = false;
            for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
            {
                TabletopObjectId pawnId = trapFloorTemplate.Players[i].PawnId;
                TrapFloorCoordinate? tile = TryReadPawnTile(pawnId, out TrapFloorCoordinate readTile)
                    ? readTile
                    : (TrapFloorCoordinate?)null;
                bool known = landingPawnTiles.TryGetValue(pawnId, out TrapFloorCoordinate? previous);
                if (known && tile.HasValue && !Nullable.Equals(previous, tile))
                {
                    if (pawnId == activePawnId)
                    {
                        landedTile = tile;
                        landedFrom = previous;
                    }
                    else
                    {
                        otherPawnMoved = true;
                        if (!activePawnId.IsEmpty && IsHiddenUsableTile(tile.Value)) otherPawnOnHiddenTile = true;
                    }
                }

                landingPawnTiles[pawnId] = tile;
            }

            // Owner, 2026-10-15 (H3): a landing the rules do not allow is a rule check, hints on or off. It does
            // not flip its tile; a warning says why and points at the Undo button; the turn menu offers Keep move.
            string moveProblem = null;
            if (landedTile.HasValue)
            {
                moveProblem = CheckActivePawnMove(landedFrom, landedTile.Value);
                if (moveProblem != null)
                {
                    FeedbackLine(
                        $"Move not allowed: {moveProblem} The tile does not flip. Press the Undo button to take the "
                        + "move back, or choose Keep move in the turn menu if the table agrees.",
                        FeedbackSeverity.Warning);
                    RefreshTrapFloorStatusUi();
                }

                RaiseHint(HintTriggers.PawnMoved, "own");
            }
            else if (otherPawnMoved)
            {
                RaiseHint(HintTriggers.PawnMoved, "other");
            }

            // The tile waiting from an earlier landing turns once no pawn stands on it.
            if (!waitingLandingFloorCardId.IsEmpty)
            {
                RevealWaitingLandingTile(false);
            }

            if (!landedTile.HasValue && otherPawnOnHiddenTile)
            {
                // UX-1 hint: moving another player's pawn never flips; say whose pawn does.
                string colour = TryGetTrapFloorSeatIndex(trapFloorTurnState.ActivePlayerId, out int seatIndex)
                    ? $" ({PlayerColorName(seatIndex).ToLowerInvariant()})"
                    : string.Empty;
                ShowMessage(
                    $"It's {FormatPlayerName(trapFloorTurnState.ActivePlayerId)}'s turn{colour}. "
                    + "Only their pawn flips tiles.");
                return;
            }

            if (!landedTile.HasValue
                || !trapFloorTemplate.TryGetFloorCardId(landedTile.Value, out TabletopObjectId floorCardId)
                || !trapFloorTemplate.TryGetFloorCardState(matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                || floorCard.IsRevealed
                || trapFloorCollapseState.IsCollapsed(floorCardId))
            {
                return;
            }

            if (moveProblem != null)
            {
                notAllowedLandingFloorCardId = floorCardId; // flips only if the table keeps the move
                RefreshTrapFloorStatusUi();
                return;
            }

            // The landing triggers now (popup and effects); the tile on the table turns once the pawn is off it
            // (aside or ahead), or when the turn ends.
            landedFloorCards.Add(floorCardId);
            TriggerLandedTile(floorCardId, trapFloorTurnState.ActivePlayerId);
        }

        private bool IsAssistedTrapFloorConsoleSlot(ContainerId slotContainerId)
        {
            if (trapFloorTemplate == null || slotContainerId.IsEmpty)
            {
                return false;
            }

            if (trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                return false;
            }

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (player.MainSlotContainerId == slotContainerId)
            {
                return true;
            }

            for (int i = 0; i < player.SideSlotContainerIds.Count; i++)
            {
                if (player.SideSlotContainerIds[i] == slotContainerId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAssistedControllerDeck(ContainerId containerId)
        {
            if (trapFloorTemplate == null || containerId.IsEmpty)
            {
                return false;
            }

            if (trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                return false;
            }

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            return player.ControllerDeckId == containerId
                && matchState.Containers.TryGetValue(containerId, out ContainerState container)
                && container.Kind == ContainerKind.Deck
                && container.OwnerSeatId == player.SeatId
                && matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                && seat.OccupantPlayerId == trapFloorTurnState.ActivePlayerId;
        }

        // The game's cards as played on this computer (doc 27 L2-1): the active pack's and your own card changes and
        // new cards applied to the built-in cards. Problems are skipped with a console warning, never stopping play.
        private GameContent BuildTrapFloorContent(bool useChosenDeck)
        {
            EnsureContentArt();
            GameContent content = new GameContent(trapFloorGameDefinition, contentArt.LoadPlayerCards(), contentArt.LoadPlayerDecks(),
                useChosenDeck ? ChosenTrapFloorDeckId() : null);
            for (int i = 0; i < content.Warnings.Count; i++)
            {
                Debug.LogWarning($"[Your cards] {content.Warnings[i]}", this);
            }

            if (content.ChangedCount > 0 || content.NewCards.Count > 0)
            {
                Debug.Log(
                    $"[Your cards] {trapFloorGameDefinition.DisplayName}: {content.ChangedCount} changed, {content.NewCards.Count} new"
                    + (content.PlayDeckName.Length > 0 ? $", playing with your deck '{content.PlayDeckName}'." : "."),
                    this);
            }

            return content;
        }

        private void HandleConsoleCardInteractionAccepted(IConsoleCardInteraction interaction)
        {
            if (interaction == null)
            {
                return;
            }

            ConsoleCardBehavior behavior = ConsoleCardBehavior.None;
            string cardName = "Card";
            if (TryGetAuthoredCardDefinition(interaction.CardDefinitionId, out CardDefinition definition))
            {
                behavior = definition.ConsoleBehavior;
                cardName = definition.DisplayName;
            }

            bool inserted = interaction is ConsoleCardInserted;
            string verb = inserted ? "inserted" : "removed";
            string direction = inserted ? "into" : "from";
            ShowMessage(
                $"{FormatPlayerName(interaction.ActorPlayerId)} {verb} {cardName} {direction} Console");

            try
            {
                ConsoleCardInteractionAccepted?.Invoke(interaction, behavior);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void HandleTrapFloorConsoleCardInteractionAccepted(
            IConsoleCardInteraction interaction,
            ConsoleCardBehavior behavior)
        {
            HandlePendingPurchaseConsoleInteraction(interaction);

            if (!(interaction is ConsoleCardInserted insertion)
                || behavior != ConsoleCardBehavior.ActivateOnInsert
                || trapFloorAbilityResolutionService == null
                || !TryGetAuthoredCardDefinition(
                    interaction.CardDefinitionId,
                    out CardDefinition definition))
            {
                return;
            }

            TrapFloorAbilityEffect effect =
                TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
            if (effect != TrapFloorAbilityEffect.Disarm
                && effect != TrapFloorAbilityEffect.Shield
                && effect != TrapFloorAbilityEffect.Dodge
                && effect != TrapFloorAbilityEffect.Rush
                && effect != TrapFloorAbilityEffect.Check)
            {
                return;
            }

            TrapFloorAbilityActivationResult result = trapFloorAbilityResolutionService.Activate(
                matchState,
                insertion,
                definition.ToData());
            if (!result.Succeeded)
            {
                ShowMessage(TrapFloorAbilityRejectionMessage(effect, result.Error));
                return;
            }

            if (undoTrackedMatch == matchState && undoHistory.CurrentStateIndex >= 0)
            {
                // The accepted transfer already owns this transaction. Replace its After snapshot
                // with the Trap Floor interpretation instead of creating a second history entry.
                undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                RefreshUndoUi();
            }

            CloseContextMenu();
            CloseCardInspect();
            RefreshTrapFloorStatusUi();
            if (effect == TrapFloorAbilityEffect.Dodge)
            {
                RefreshMovementAssistancePresentation();
                ShowMessage("DODGE — Move to a highlighted adjacent Floor");
            }
            else if (effect == TrapFloorAbilityEffect.Rush)
            {
                RefreshMovementAssistancePresentation();
                ShowMessage("RUSH — Move up to 3 spaces");
            }
            else if (effect == TrapFloorAbilityEffect.Check)
            {
                RefreshNeutralizedTrapPresentation();
                ShowMessage(
                    $"{FormatPlayerShortName(interaction.ActorPlayerId)} checked "
                    + $"{result.Trap.Content.DisplayName} — Trap neutralized");
            }
            else
            {
                string action = effect == TrapFloorAbilityEffect.Disarm
                    ? "used Disarm on"
                    : "used Shield against";
                ShowMessage(
                    $"{FormatPlayerShortName(interaction.ActorPlayerId)} {action} "
                    + result.Trap.Content.DisplayName);
            }
        }

        private void HandlePendingPurchaseConsoleInteraction(
            IConsoleCardInteraction interaction)
        {
            if (interaction == null
                || pendingControllerPurchaseState == null
                || !pendingControllerPurchaseState.IsActive
                || !TryGetTrapFloorPlayerSetup(
                    pendingControllerPurchaseState.PlayerId,
                    out TrapFloorPlayerSetupDefinition player))
            {
                return;
            }

            bool changed = interaction is ConsoleCardInserted
                ? pendingControllerPurchaseState.RecordConsoleInsertion(
                    interaction.ActorPlayerId,
                    interaction.ConsoleOwnerPlayerId,
                    interaction.SlotContainerId,
                    interaction.CardInstanceId,
                    player.SideSlotContainerIds)
                : interaction is ConsoleCardRemoved
                    && pendingControllerPurchaseState.RecordConsoleRemoval(
                        interaction.ActorPlayerId,
                        interaction.ConsoleOwnerPlayerId,
                        interaction.SlotContainerId,
                        interaction.CardInstanceId,
                        player.SideSlotContainerIds);
            if (!changed) return;

            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            string abilityName = FindPurchaseDisplayName(
                pendingControllerPurchaseState.PurchasedCardDefinitionStableId).ToUpperInvariant();
            ShowMessage(pendingControllerPurchaseState.IsPaymentComplete
                ? "PAYMENT READY — Confirm Purchase"
                : $"BUY {abilityName} — Place the selected Cards in your Console");
        }

        private static string TrapFloorAbilityRejectionMessage(
            TrapFloorAbilityEffect effect,
            TrapFloorAbilityActivationError error)
        {
            switch (error)
            {
                case TrapFloorAbilityActivationError.NoUnresolvedTrap:
                    return effect == TrapFloorAbilityEffect.Check
                        ? "Check unavailable — no unresolved Trap."
                        : $"{effect} has no revealed unresolved Trap for the active Player.";
                case TrapFloorAbilityActivationError.NoPendingTrapConsequence:
                    return "Shield has no pending assisted Trap consequence for the active Player.";
                case TrapFloorAbilityActivationError.AbilityAlreadyUsed:
                    return $"This {effect} Card has already activated.";
                case TrapFloorAbilityActivationError.ActorIsNotActivePlayer:
                    return $"{effect} assistance activates only for the active Player.";
                case TrapFloorAbilityActivationError.PawnFloorUnavailable:
                    return $"{effect} could not identify the active Player's current Floor.";
                case TrapFloorAbilityActivationError.NoValidDodgeDestination:
                    return "Dodge unavailable — no adjacent Floor.";
                case TrapFloorAbilityActivationError.NoValidRushDestination:
                    return "Rush unavailable — no reachable Floor.";
                default:
                    return $"{effect} assistance could not resolve ({error}).";
            }
        }

        private void RefreshMovementAssistancePresentation()
        {
            if (abilityTargetPresenter == null) return;
            IReadOnlyList<TabletopObjectId> targetFloorCardIds =
                trapFloorAbilityResolutionState?.ActiveRushAssistance?.TargetFloorCardIds
                ?? trapFloorAbilityResolutionState?.ActiveDodgeAssistance?.TargetFloorCardIds;
            if (targetFloorCardIds == null)
            {
                abilityTargetPresenter.ClearCurrent();
                return;
            }

            abilityTargetPresenter.Show(targetFloorCardIds);
        }

        private void RefreshNeutralizedTrapPresentation()
        {
            if (abilityTargetPresenter == null) return;
            List<TabletopObjectId> neutralizedFloorCardIds = new List<TabletopObjectId>();
            if (trapFloorAbilityResolutionState != null)
            {
                for (int i = 0; i < trapFloorAbilityResolutionState.TrapRecords.Count; i++)
                {
                    TrapFloorTrapResolutionRecord trap =
                        trapFloorAbilityResolutionState.TrapRecords[i];
                    if (trap.Disposition == TrapFloorTrapResolutionDisposition.Neutralized)
                        neutralizedFloorCardIds.Add(trap.FloorCardId);
                }
            }
            abilityTargetPresenter.ShowNeutralized(neutralizedFloorCardIds);
        }

        private bool IsTrapFloorTrapNeutralized(TabletopObjectId floorCardId)
        {
            return trapFloorAbilityResolutionState != null
                && trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.Disposition == TrapFloorTrapResolutionDisposition.Neutralized;
        }

        private bool WasTrapSafelyRevealed(TabletopObjectId floorCardId)
        {
            if (trapFloorAbilityResolutionState != null
                && trapFloorAbilityResolutionState.TryGetTrap(floorCardId, out _))
                return false;
            if (trapFloorActivityFeed == null) return false;
            for (int i = trapFloorActivityFeed.Entries.Count - 1; i >= 0; i--)
            {
                TrapFloorActivityEntry entry = trapFloorActivityFeed.Entries[i];
                if (entry.FloorCardId == floorCardId
                    && entry.Kind == TrapFloorActivityKind.SafelyRevealedTrap)
                    return true;
            }
            return false;
        }

        private bool IsTrapFloorTrapPending(TabletopObjectId floorCardId)
        {
            return trapFloorAbilityResolutionState != null
                && trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.IsPending;
        }

        private bool IsTrapFloorTrapResolved(TabletopObjectId floorCardId)
        {
            return trapFloorAbilityResolutionState != null
                && trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && !trap.IsPending;
        }

        // The cost line on inspect text, in card notation (C1). The active game data wins, so a cost set by the
        // rules shows; without a loaded game the card's authored cost is shown.
        private string FormatInputCost(CardDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            string cost;
            if (trapFloorTemplate != null
                && trapFloorTemplate.GameDefinition.TryGetCard(definition.StableId, out CardDefinitionData data))
            {
                cost = InputCostNotation.Format(data.InputCost);
            }
            else
            {
                cost = FormatAuthoredInputCost(definition.InputCost);
            }

            return string.IsNullOrEmpty(cost) ? string.Empty : $"\n\nCost: {cost}";
        }

        private static string FormatAuthoredInputCost(InputCostDefinition inputCost)
        {
            if (inputCost == null || inputCost.Requirements.Count == 0)
            {
                return string.Empty;
            }

            List<InputRequirementData> requirements = new List<InputRequirementData>(inputCost.Requirements.Count);
            for (int i = 0; i < inputCost.Requirements.Count; i++)
            {
                InputRequirement requirement = inputCost.Requirements[i];
                List<ControllerInput> inputs = new List<ControllerInput>(1 + requirement.AlsoAccepts.Count)
                {
                    requirement.Input,
                };
                inputs.AddRange(requirement.AlsoAccepts);
                requirements.Add(new InputRequirementData(inputs, requirement.Count));
            }

            return InputCostNotation.Format(new InputCostData(requirements));
        }
    }
}
