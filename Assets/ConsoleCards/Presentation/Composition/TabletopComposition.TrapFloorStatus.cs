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
    /// <summary>Trap Floor status texts and turn menu entries shown on the HUD. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        private void RefreshTrapFloorStatusUi()
        {
            if (runtimeUi == null || !IsInitialized)
            {
                return;
            }

            if (trapFloorObjectiveState != null && trapFloorTurnState != null)
            {
                // UX-1: the turn names the player's colour, and the strip's chip shows it.
                PlayerId turnPlayerId = trapFloorTurnState.IsCurrentFloorFailed
                    ? PlayerId.Empty
                    : trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                        ? trapFloorTurnState.ActivePlayerId
                        : trapFloorTurnState.FloorOperatorPlayerId;
                Color? turnColor = TryGetTrapFloorSeatIndex(turnPlayerId, out int turnSeatIndex)
                    ? PlayerSeatColor(turnSeatIndex)
                    : (Color?)null;
                string phase = trapFloorTurnState.IsCurrentFloorFailed
                    ? "ALL PLAYERS ELIMINATED"
                    : trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                        ? turnColor.HasValue
                            ? $"{FormatPlayerName(turnPlayerId).ToUpperInvariant()} ({PlayerColorName(turnSeatIndex)}) TURN{PlayerEffectChips(turnPlayerId)}"
                            : $"{FormatPlayerName(turnPlayerId).ToUpperInvariant()} TURN{PlayerEffectChips(turnPlayerId)}"
                        : "FLOOR TURN";
                string turnDetail = CurrentTrapFloorCollapseStatusText();
                if (!trapFloorTurnState.IsCurrentFloorFailed
                    && trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
                {
                    turnDetail += $"\nOperator: {FormatPlayerName(trapFloorTurnState.FloorOperatorPlayerId)}";
                }

                // The player list lives on the status card; the one-line turn strip keeps only the Keys.
                turnDetail += $"\n{CurrentTrapFloorPlayerStatesText()}";
                TrapFloorStatusModel turnStatus = new TrapFloorStatusModel(
                    $"ROUND {trapFloorTurnState.CurrentRound}",
                    phase,
                    CurrentTrapFloorObjectiveProgressText(),
                    turnDetail,
                    trapFloorTurnState.IsCurrentFloorFailed
                        ? "ALL PLAYERS ELIMINATED"
                        : trapFloorObjectiveState.IsWon ? "VICTORY" : string.Empty,
                    HintText(TrapFloorTurnGuidanceText()),
                    turnColor);
                runtimeUi.ShowTrapFloorStatus(
                    turnStatus,
                    BuildFloorfallStatusModel(),
                    AddHandTrayToggleAction(BuildTrapFloorTurnActions()));
                return;
            }

            if (trapFloorRoundState == null)
            {
                runtimeUi.HideTrapFloorStatus();
                return;
            }

            TrapFloorPendingFloormasterCard pendingCard = floormasterLifecycleState?.PendingCard;
            string detail = string.Empty;
            if (pendingCard != null)
            {
                detail =
                    $"Pending Trigger: {FormatPlayerName(pendingCard.SearchingPlayerId)} / {pendingCard.Category}\n"
                    + "Resolve the card's effect at the table, then continue.";
            }
            else if (trapFloorRoundState.Phase == TrapFloorRoundPhase.Floorfall)
            {
                detail =
                    $"Floorfalls performed this phase: {trapFloorRoundState.AcceptedFloorfallCount}\n"
                    + "Roll as many Floorfalls as your mode asks for.";
            }
            else if (trapFloorRoundState.IsScheduleCompleted)
            {
                detail = "All 10 rounds are played. Decide the result together.";
            }

            TrapFloorStatusModel status = new TrapFloorStatusModel(
                $"Round: {trapFloorRoundState.CurrentRoundNumber} / {TrapFloorRoundState.FinalRoundNumber}",
                $"Phase: {trapFloorRoundState.Phase}",
                $"Search + Trigger: {trapFloorRoundState.CompletedSearchTriggerCount} / "
                    + $"{trapFloorRoundState.ParticipatingPlayerIds.Count} Players complete",
                detail,
                $"Hand: {ContainerCount(handContainerId)}",
                HintText(TrapFloorActionHelpText()));

            FloorfallStatusModel floorfall = BuildFloorfallStatusModel();
            runtimeUi.ShowTrapFloorStatus(status, floorfall, AddHandTrayToggleAction(BuildTrapFloorAssistedActions()));
        }

        private FloorfallStatusModel BuildFloorfallStatusModel()
        {
            if (floorfallState == null || !floorfallState.CurrentTarget.HasValue)
            {
                return new FloorfallStatusModel(false, string.Empty, string.Empty, string.Empty);
            }

            TrapFloorFloorfallTarget target = floorfallState.CurrentTarget.Value;
            return new FloorfallStatusModel(
                true,
                $"Die 1 / X: {target.XAxisRoll.Value}   Die 2 / Y: {target.YAxisRoll.Value}",
                $"Coordinate: {target.Coordinate}",
                $"Current target: Floor Card {target.Coordinate}");
        }

        private List<PopupActionOption> BuildTrapFloorAssistedActions()
        {
            List<PopupActionOption> actions = new List<PopupActionOption>();
            switch (trapFloorRoundState.Phase)
            {
                case TrapFloorRoundPhase.Start:
                    actions.Add(new PopupActionOption(
                        trapFloorRoundState.CurrentRoundNumber == 1
                            ? "Begin Round / Continue to Search"
                            : "Movement done / Continue",
                        true,
                        () => CompleteTrapFloorStart()));
                    break;
                case TrapFloorRoundPhase.Search:
                    for (int i = 0; i < trapFloorRoundState.ParticipatingPlayerIds.Count; i++)
                    {
                        PlayerId playerId = trapFloorRoundState.ParticipatingPlayerIds[i];
                        if (trapFloorRoundState.HasCompletedSearchTrigger(playerId))
                        {
                            continue;
                        }

                        PlayerId searchingPlayerId = playerId;
                        actions.Add(new PopupActionOption(
                            $"Search - {FormatPlayerName(searchingPlayerId)}",
                            true,
                            () => SearchFloormasterDeck(searchingPlayerId)));
                    }

                    break;
                case TrapFloorRoundPhase.Trigger:
                    actions.Add(new PopupActionOption(
                        "Card effect done",
                        true,
                        () => CompletePendingFloormasterTrigger()));
                    break;
                case TrapFloorRoundPhase.Floorfall:
                    actions.Add(new PopupActionOption(
                        "Roll Floorfall",
                        true,
                        () => BeginPhysicalFloorfall()));
                    actions.Add(new PopupActionOption(
                        "Floorfall done",
                        trapFloorRoundState.AcceptedFloorfallCount > 0,
                        () => CompleteFloorfallPhase()));
                    break;
                case TrapFloorRoundPhase.End:
                    actions.Add(new PopupActionOption(
                        "End round",
                        true,
                        () => CompleteEndPhase()));
                    break;
            }

            return actions;
        }

        private List<PopupActionOption> BuildTrapFloorTurnActions()
        {
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (trapFloorTurnState == null)
            {
                return actions;
            }

            if (trapFloorTurnState.IsCurrentFloorFailed)
            {
                actions.Add(new PopupActionOption(
                    "End Floor Turn",
                    true,
                    AdvanceTrapFloorTurn));
                return actions;
            }

            if (trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                if (TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                    && !blindStatus.IsDirectionResolved)
                {
                    actions.Add(new PopupActionOption(
                        "Roll Blind Direction",
                        !physicalBlindDirectionPending,
                        () => BeginPhysicalBlindDirection()));
                }
                // Search and Careful Search are gone (doc 18 §15.2, G2d): landing on a tile flips it for free.
                bool purchasePending = pendingControllerPurchaseState != null
                    && pendingControllerPurchaseState.IsActive
                    && pendingControllerPurchaseState.PlayerId == trapFloorTurnState.ActivePlayerId;
                if (purchasePending)
                {
                    actions.Add(new PopupActionOption(
                        "Confirm Payment",
                        pendingControllerPurchaseState.IsPaymentComplete,
                        ConfirmPendingActionAbilityPurchase));
                    actions.Add(new PopupActionOption(
                        "Cancel Payment",
                        true,
                        CancelPendingActionAbilityPurchase));
                }
                else if (TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prizeTile))
                {
                    // The Buy Ability market is retired (doc 18 §15.4); prizes are taken from the floor.
                    actions.Add(new PopupActionOption(
                        TakePrizeLabel(prizeTile),
                        true,
                        BeginTakeFloorPrize));
                }
                // After a move the rules do not allow: the Undo button takes it back (owner, 2026-10-15: no Undo
                // entry here); the table may keep it instead (nothing is blocked).
                if (IsTooFarMoveUndoable())
                {
                    actions.Add(new PopupActionOption(
                        notAllowedLandingFloorCardId.IsEmpty ? "Keep move" : "Keep move (flip the tile)",
                        true,
                        KeepNotAllowedMove));
                }

                // F3: End Turn says when it will apply waiting Traps, and asks first.
                int waiting = PendingEndTurnEffects(trapFloorTurnState.ActivePlayerId, null);
                string applies = waiting > 0 ? $" (applies {waiting})" : string.Empty;
                actions.Add(new PopupActionOption(
                    "Skip Turn" + applies,
                    true,
                    () => RequestEndTurn(true)));
                actions.Add(new PopupActionOption(
                    "End Turn" + applies,
                    true,
                    () => RequestEndTurn(false)));
                return actions;
            }

            if (trapFloorCollapseState != null)
            {
                actions.Add(new PopupActionOption(
                    "Collapse Floor",
                    !trapFloorCollapseState.IsCollapsePending
                        && !trapFloorCollapseState.IsBoardExhausted,
                    () => BeginPhysicalFloorCollapse()));
            }

            actions.Add(new PopupActionOption(
                "End Floor Turn",
                true,
                AdvanceTrapFloorTurn));
            return actions;
        }

        private string TrapFloorTurnGuidanceText()
        {
            if (trapFloorTurnState.IsCurrentFloorFailed)
            {
                return "This round failed. You can still move pieces by hand; end the Floor Turn to continue.";
            }

            if (trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
            {
                return "Roll 2d6 to resolve Collapse\nEnd Floor Turn when complete";
            }

            if (pendingControllerPurchaseState != null
                && pendingControllerPurchaseState.IsActive
                && pendingControllerPurchaseState.PlayerId == trapFloorTurnState.ActivePlayerId)
            {
                if (pendingControllerPurchaseState.IsPaymentComplete)
                    return "PAYMENT READY — Confirm Payment";
                string abilityName = FindPurchaseDisplayName(
                    pendingControllerPurchaseState.PurchasedCardDefinitionStableId)
                    .ToUpperInvariant();
                return $"{PaymentVerb(pendingControllerPurchaseState)} {abilityName} — Place the chosen cards in your Console";
            }

            int handLimit = trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
            int moveTiles = MoveAllowance(out _);
            string standardGuidance =
                $"Draw up to {handLimit} if needed\nMove up to {moveTiles} tile{(moveTiles == 1 ? string.Empty : "s")} "
                + "in one straight line (they light while you hold your pawn); landing flips a tile. "
                + "Take a prize you stand on, then End Turn";
            bool isBlind = TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus);
            bool isSlow = TryGetActiveSlowStatus(out TrapFloorSlowStatusState slowStatus);
            bool isSticky = TryGetActiveStickyStatus(out _);
            string slowGuidance = isSlow
                ? $"{(slowStatus.MovementModifier > 0 ? "OVERCHARGED" : "SLOW")} — Movement {FormatSignedModifier(slowStatus.MovementModifier)} this round"
                : string.Empty;
            string movementGuidance = isSticky
                ? "STICKY — Stay in place this round"
                : string.Empty;
            if (isSlow)
                movementGuidance = string.IsNullOrEmpty(movementGuidance)
                    ? slowGuidance
                    : $"{movementGuidance}\n{slowGuidance}";
            // G2d: the old Search / Buy words are gone; the Shop is named only while it is open.
            string shopWord = IsShopOpen() ? "Shop / " : string.Empty;
            if (!isBlind)
            {
                string availableActions = isSticky
                    ? $"Draw up to {handLimit} if needed\nAction ability / {shopWord}Skip"
                    : standardGuidance;
                return string.IsNullOrEmpty(movementGuidance)
                    ? availableActions
                    : $"{movementGuidance}\n{availableActions}";
            }
            string directionGuidance = blindStatus.IsDirectionResolved
                ? $"BLIND DIRECTION: {blindStatus.MovementDirection.Value.ToString().ToUpperInvariant()}"
                : "BLIND — Roll d4 for movement direction";
            string blindActions = isSticky ? $"Action ability / {shopWord}Skip" : $"Move / Action ability / {shopWord}Skip";
            return string.IsNullOrEmpty(movementGuidance)
                ? $"BLIND — No draw this round\n{directionGuidance}\n{blindActions}"
                : $"BLIND — No draw this round\n{directionGuidance}\n{movementGuidance}\n{blindActions}";
        }

        private bool TryGetActiveBlindStatus(out TrapFloorBlindStatusState status)
        {
            if (trapFloorAbilityResolutionState != null
                && trapFloorTurnState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorAbilityResolutionState.TryGetBlindStatus(
                    trapFloorTurnState.ActivePlayerId,
                    trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        private bool TryGetActiveSlowStatus(out TrapFloorSlowStatusState status)
        {
            if (trapFloorAbilityResolutionState != null
                && trapFloorTurnState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorAbilityResolutionState.TryGetSlowStatus(
                    trapFloorTurnState.ActivePlayerId,
                    trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        private bool TryGetActiveStickyStatus(out TrapFloorStickyStatusState status)
        {
            if (trapFloorAbilityResolutionState != null
                && trapFloorTurnState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorAbilityResolutionState.TryGetStickyStatus(
                    trapFloorTurnState.ActivePlayerId,
                    trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        private static string FormatSignedModifier(int modifier) =>
            modifier > 0 ? $"+{modifier}" : modifier.ToString();

        private bool HasEliminatedTrapFloorPlayers()
        {
            for (int i = 0; i < trapFloorTurnState.PlayerOrder.Count; i++)
            {
                if (trapFloorTurnState.GetPlayerState(trapFloorTurnState.PlayerOrder[i])
                    == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    return true;
                }
            }

            return false;
        }

        private string CurrentStatusText()
        {
            if (activeSession != null
                && activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                if (Time.unscaledTime <= operationMessageUntil)
                {
                    return operationMessage;
                }

                return $"Empty Table | Objects {matchState?.ObjectCount ?? 0}";
            }

            string counts = $"Trap Floor | Floor Cards {CurrentFloorCardCount()}";
            string objective = CurrentTrapFloorObjectiveText();
            string activity = CurrentTrapFloorActivityText();
            string persistentStatus = string.IsNullOrEmpty(objective)
                ? counts
                : $"{counts}\n{objective}";
            if (!string.IsNullOrEmpty(activity))
            {
                persistentStatus = $"{persistentStatus}\n{activity}";
            }

            return Time.unscaledTime <= operationMessageUntil
                ? $"{operationMessage}\n{persistentStatus}"
                : persistentStatus;
        }

        private string CurrentTrapFloorActivityText()
        {
            if (trapFloorActivityFeed == null || trapFloorActivityFeed.Entries.Count == 0)
            {
                return string.Empty;
            }

            int firstIndex = Math.Max(0, trapFloorActivityFeed.Entries.Count - 2);
            string text = string.Empty;
            for (int i = firstIndex; i < trapFloorActivityFeed.Entries.Count; i++)
            {
                TrapFloorActivityEntry entry = trapFloorActivityFeed.Entries[i];
                string line;
                switch (entry.Kind)
                {
                    case TrapFloorActivityKind.RevealedFloorContent:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} revealed "
                            + $"{entry.ContentName} [{entry.ContentCategory}]";
                        break;
                    case TrapFloorActivityKind.SafelyRevealedTrap:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} revealed {entry.ContentName} safely";
                        break;
                    case TrapFloorActivityKind.ClaimedKey:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} claimed {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.WonGame:
                        line = "TRAP FLOOR VICTORY";
                        break;
                    case TrapFloorActivityKind.PlayerEscaped:
                        line = $"{FormatPlayerName(entry.ActorPlayerId).ToUpperInvariant()} ESCAPED";
                        break;
                    case TrapFloorActivityKind.TriggeredFloorfall:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} triggered Floorfall";
                        break;
                    case TrapFloorActivityKind.RolledFloorfall:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} rolled "
                            + $"{entry.XAxisResult}/{entry.YAxisResult} for Floor {entry.Coordinate}";
                        break;
                    case TrapFloorActivityKind.CollapsedFloor:
                        line = $"Floor {entry.Coordinate} collapsed";
                        break;
                    case TrapFloorActivityKind.SkippedTurn:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} skipped their turn";
                        break;
                    case TrapFloorActivityKind.PlayerEliminated:
                        line = entry.ContentDefinitionId.IsEmpty
                            ? $"{FormatPlayerName(entry.ActorPlayerId).ToUpperInvariant()} ELIMINATED"
                            : $"{FormatPlayerShortName(entry.ActorPlayerId)} was eliminated by {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.AllPlayersEliminated:
                        line = "ALL PLAYERS ELIMINATED";
                        break;
                    case TrapFloorActivityKind.PlayersReactivated:
                        line = "ALL PLAYERS REACTIVATED";
                        break;
                    case TrapFloorActivityKind.UsedDisarm:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Disarm on {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedShield:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Shield against {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedDodge:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Dodge on {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedRush:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} activated Rush";
                        break;
                    case TrapFloorActivityKind.UsedCheck:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} checked "
                            + $"{entry.ContentName} — Trap neutralized";
                        break;
                    case TrapFloorActivityKind.BlindApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Blind applies next Round";
                        break;
                    case TrapFloorActivityKind.BlindDirectionRolled:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} rolled "
                            + $"{entry.BlindDirectionResult} for Blind direction";
                        break;
                    case TrapFloorActivityKind.SlowApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Slow applies next Round";
                        break;
                    case TrapFloorActivityKind.StickyApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Sticky applies next Round";
                        break;
                    default:
                        line = string.Empty;
                        break;
                }

                text = string.IsNullOrEmpty(text) ? line : $"{text}\n{line}";
            }

            return text;
        }

        private string CurrentTrapFloorObjectiveText()
        {
            if (trapFloorObjectiveState == null)
            {
                return string.Empty;
            }

            string progress = CurrentTrapFloorObjectiveProgressText();
            if (trapFloorTurnState != null && trapFloorTurnState.IsCurrentFloorFailed)
            {
                return $"{progress} | ALL PLAYERS ELIMINATED";
            }
            return trapFloorObjectiveState.IsWon ? $"{progress} | VICTORY" : progress;
        }

        private string CurrentTrapFloorObjectiveProgressText()
        {
            string keys =
                $"KEYS {trapFloorObjectiveState.CollectedKeyCount} / {trapFloorObjectiveState.RequiredKeyCount}";
            return trapFloorObjectiveState.ModeBehavior == ModeBehavior.Survival
                ? $"{keys}\nESCAPED {trapFloorObjectiveState.EscapedPlayerCount} / "
                    + $"{activeSession.Request.ActivePlayerIds.Count}"
                : keys;
        }

        // F3: the effects on a player this round, as chips after their name on the turn strip.
        private string PlayerEffectChips(PlayerId playerId)
        {
            if (trapFloorAbilityResolutionState == null || trapFloorTurnState == null || playerId.IsEmpty)
            {
                return string.Empty;
            }

            int round = trapFloorTurnState.CurrentRound;
            string chips = string.Empty;
            if (trapFloorAbilityResolutionState.TryGetBlindStatus(playerId, round, out _)) chips += "  · BLIND";
            if (trapFloorAbilityResolutionState.TryGetSlowStatus(playerId, round, out TrapFloorSlowStatusState slow))
            {
                chips += slow.MovementModifier > 0 ? "  · OVERCHARGED" : "  · SLOW";
            }

            if (trapFloorAbilityResolutionState.TryGetStickyStatus(playerId, round, out _)) chips += "  · STUCK";
            int pending = PendingEndTurnEffects(playerId, null);
            if (pending > 0) chips += $"  · {pending} TRAP{(pending == 1 ? string.Empty : "S")} WAITING";
            return chips;
        }

        private string CurrentTrapFloorPlayerStatesText()
        {
            if (trapFloorTurnState == null) return string.Empty;

            string text = "PLAYERS ";
            for (int i = 0; i < trapFloorTurnState.PlayerOrder.Count; i++)
            {
                PlayerId playerId = trapFloorTurnState.PlayerOrder[i];
                string state = trapFloorObjectiveState != null
                    && trapFloorObjectiveState.IsPlayerEscaped(playerId)
                        ? "ESCAPED"
                        : trapFloorTurnState.GetPlayerState(playerId)
                            == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                            ? "ELIMINATED"
                            : "ACTIVE";
                if (trapFloorAbilityResolutionState != null
                    && trapFloorAbilityResolutionState.TryGetBlindStatus(
                        playerId,
                        trapFloorTurnState.CurrentRound,
                        out _))
                {
                    state += " / BLIND";
                }
                if (trapFloorAbilityResolutionState != null
                    && trapFloorAbilityResolutionState.TryGetSlowStatus(
                        playerId,
                        trapFloorTurnState.CurrentRound,
                        out TrapFloorSlowStatusState movementStatus))
                {
                    state += movementStatus.MovementModifier > 0 ? " / OVERCHARGED" : " / SLOW";
                }
                if (trapFloorAbilityResolutionState != null
                    && trapFloorAbilityResolutionState.TryGetStickyStatus(
                        playerId,
                        trapFloorTurnState.CurrentRound,
                        out _))
                {
                    state += " / STICKY";
                }
                if (i > 0) text += " | ";
                text += $"{FormatPlayerName(playerId)} {state}";
            }
            return text;
        }

        private string CurrentTrapFloorCollapseStatusText()
        {
            if (trapFloorCollapseState == null)
            {
                return string.Empty;
            }

            TrapFloorCollapseRollState roll = trapFloorCollapseState.LastRoll;
            if (trapFloorCollapseState.IsBoardExhausted)
            {
                return $"USABLE FLOORS 0 / {trapFloorCollapseState.TotalFloorCount} | BOARD EXHAUSTED";
            }

            if (trapFloorCollapseState.IsCollapsePending)
            {
                return roll != null && roll.RequiredReroll
                    ? $"FLOORFALL {roll.XAxisResult}/{roll.YAxisResult}: ALREADY A HOLE — REROLLING"
                    : "FLOORFALL: ROLLING 2d6";
            }

            string usable = $"USABLE FLOORS {trapFloorCollapseState.UsableFloorCount}"
                + $" / {trapFloorCollapseState.TotalFloorCount}";
            string collapse = roll == null
                ? usable
                : $"{usable} | LAST {roll.XAxisResult}/{roll.YAxisResult} → {roll.Coordinate}";
            if (trapFloorTemplate == null) return collapse;
            return $"{trapFloorTemplate.ActiveMode.DisplayName} · {trapFloorTemplate.ActiveMode.Behavior} game · "
                + $"{FormatCollapseSchedule(trapFloorTemplate.ActiveMode.Collapse.ScheduleKind)}\n{collapse}";
        }

        private static string FormatCollapseSchedule(CollapseScheduleKind schedule)
        {
            switch (schedule)
            {
                case CollapseScheduleKind.RoundBased:
                    return "floor collapses each round";
                case CollapseScheduleKind.RealTime:
                    return "floor collapses in real time";
                default:
                    return "no collapse";
            }
        }

        private void ApplyCollapsedFloorPresentation(TabletopObjectId floorCardId)
        {
            if (!matchState.Cards.TryGetValue(floorCardId, out CardInstanceState card)
                || !TryGetCardVisualReferences(floorCardId, out CardVisualReferences visualReferences))
            {
                return;
            }

            string label = labelsByCardId.TryGetValue(floorCardId, out string configuredLabel)
                ? configuredLabel
                : "FLOOR";
            ConfigureCardVisuals(visualReferences, card, label);
            visualReferences.SetCardContentVisible(true);
            visualReferences.CardView.ApplyAcceptedState();
            if (inspectedCardId == floorCardId)
            {
                CloseCardInspect();
            }
        }

        private void RefreshTrapFloorFloorCardPresentation(TabletopObjectId floorCardId)
        {
            if (!matchState.Cards.TryGetValue(floorCardId, out CardInstanceState card)
                || !TryGetCardVisualReferences(floorCardId, out CardVisualReferences visualReferences))
            {
                return;
            }

            string label = labelsByCardId.TryGetValue(floorCardId, out string configuredLabel)
                ? configuredLabel
                : "FLOOR";
            ConfigureCardVisuals(visualReferences, card, label);
            visualReferences.SetCardContentVisible(ShouldShowCardContent(card));
        }

        private int CurrentFloorCardCount()
        {
            if (trapFloorTemplate == null || matchState == null)
            {
                return 0;
            }

            int count = 0;
            foreach (TabletopObjectId objectId in trapFloorTemplate.FloorCardIds.Values)
            {
                if (matchState.Cards.ContainsKey(objectId))
                {
                    count++;
                }
            }

            return count;
        }

        private int ContainerCount(ContainerId containerId)
        {
            return matchState != null && matchState.Containers.TryGetValue(containerId, out ContainerState container)
                ? container.Count
                : 0;
        }

        private CardView FindCardView(TabletopObjectId objectId)
        {
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView candidate = cardViews[i];
                if (candidate != null && candidate.IsBound && candidate.ObjectId == objectId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Authoritative Card has no bound Presentation View.");
        }

        private string FormatPlayerName(PlayerId playerId)
        {
            if (trapFloorTemplate != null && matchState != null)
            {
                for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                    if (matchState.GetSeat(player.SeatId).OccupantPlayerId == playerId)
                    {
                        return $"Player {player.LayoutSeatIndex + 1}";
                    }
                }
            }

            return "Participating Player";
        }

        private string FormatPlayerShortName(PlayerId playerId)
        {
            if (trapFloorTemplate != null && matchState != null)
            {
                for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                    if (matchState.GetSeat(player.SeatId).OccupantPlayerId == playerId)
                    {
                        return $"P{player.LayoutSeatIndex + 1}";
                    }
                }
            }

            return "Player";
        }

        private int AvailableDrawableCount(ContainerId sourceDeckContainerId)
        {
            if (matchState == null
                || !matchState.Containers.TryGetValue(sourceDeckContainerId, out ContainerState deck)
                || !matchState.Containers.TryGetValue(DrawTargetHandFor(sourceDeckContainerId), out ContainerState hand))
            {
                return 0;
            }

            int handSpace = hand.Capacity == 0
                ? deck.Count
                : Math.Max(0, hand.Capacity - hand.Count);
            return Math.Min(deck.Count, handSpace);
        }

        private int AvailableControllerDeckCount(ContainerId sourceDeckContainerId)
        {
            return IsAssistedControllerDeck(sourceDeckContainerId)
                ? matchState.GetContainer(sourceDeckContainerId).Count
                : 0;
        }

        private TrapFloorPlayerSetupDefinition GetAssistedTrapFloorPlayerSetup()
        {
            if (trapFloorTemplate == null
                || trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                throw new InvalidOperationException(
                    "Controller assistance requires an active Trap Floor Player turn.");
            }

            if (TryGetTrapFloorPlayerSetup(
                    trapFloorTurnState.ActivePlayerId,
                    out TrapFloorPlayerSetupDefinition player))
                return player;

            throw new InvalidOperationException("The active Player has no authored Controller Hand setup.");
        }

        private bool TryGetTrapFloorPlayerSetup(
            PlayerId playerId,
            out TrapFloorPlayerSetupDefinition resolvedPlayer)
        {
            if (trapFloorTemplate != null && matchState != null && !playerId.IsEmpty)
            {
                for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                    if (matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                        && seat.OccupantPlayerId == playerId)
                    {
                        resolvedPlayer = player;
                        return true;
                    }
                }
            }

            resolvedPlayer = null;
            return false;
        }
    }
}
