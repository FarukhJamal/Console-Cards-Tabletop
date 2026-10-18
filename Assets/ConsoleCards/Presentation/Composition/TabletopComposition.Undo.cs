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
    /// <summary>Reset, Undo and Redo: whole-table snapshots, batches of steps and their labels. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        public void ResetTable()
        {
            EnsureInitialized();
            MatchState previousMatch = activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                // Reset returns the table to its start, Empty Table Hand switched on included (H-E).
                emptyTableHandEnabled = true;
                Shutdown(true);
                InitializeActiveSession(true);
                ShowActiveSessionUi();
            }
            catch (Exception exception)
            {
                RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Reset");
                Debug.LogError($"Reset Presentation rebuild failed: {exception.Message}", this);
                throw;
            }

            RefreshTrapFloorStatusUi();
        }

        public bool UndoLatestAction()
        {
            return UndoLatestAction(localPlayerId);
        }

        public bool UndoLatestAction(PlayerId requestingPlayerId)
        {
            if (!IsActiveSessionPlayer(requestingPlayerId)
                || !CanUndoCurrentAction()
                || !undoHistory.TryPeekUndo(
                    out SessionUndoSnapshot snapshot,
                    out ActiveSessionUndoTransaction transaction))
            {
                return false;
            }

            Debug.Log(
                $"[Undo] Selecting state index {undoHistory.CurrentStateIndex - 1} "
                + $"from {undoHistory.TransactionCount} transactions: {transaction.Description}; "
                + $"objects={snapshot.Match.ObjectCount}; ids={FormatUndoObjectIds(snapshot.Match)}.",
                this);

            long undoRevision;
            try
            {
                undoRevision = checked(matchState.Revision + 1L);
            }
            catch (OverflowException)
            {
                ShowMessage("Undo rejected: Match revision cannot advance.");
                return false;
            }

            MatchState replacement;
            TrapFloorSessionState restoredTrapFloor = null;
            PendingControllerPurchaseState restoredPurchase = null;
            try
            {
                replacement = snapshot.Match.Restore(undoRevision);
                restoredTrapFloor = snapshot.TrapFloor?.Restore();
                restoredPurchase = snapshot.PendingControllerPurchase?.Restore();
            }
            catch (Exception exception)
            {
                ShowMessage($"Undo rejected: {exception.Message}");
                return false;
            }

            rebuildingFromUndo = true;
            MatchState previousMatch = activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                Shutdown(true);
                activeSession.ReplaceCurrentMatch(replacement);
                pendingRestoredTrapFloorState = restoredTrapFloor;
                pendingRestoredControllerPurchaseState = restoredPurchase;
                InitializeActiveSession(false);
                undoHistory.CommitUndo();
                undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                Debug.Log(
                    $"[Undo] Restored state index {undoHistory.CurrentStateIndex}; "
                    + $"history states={undoHistory.CurrentStateIndex}/{undoHistory.TransactionCount}; "
                    + $"objects={matchState.ObjectCount}.",
                    this);
                ShowActiveSessionUi();
                ShowMessage($"Undid {transaction.Description}.");
                return true;
            }
            catch (Exception exception)
            {
                RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Undo");
                Debug.LogError($"Undo Presentation rebuild failed: {exception.Message}", this);
                throw;
            }
            finally
            {
                pendingRestoredTrapFloorState = null;
                pendingRestoredControllerPurchaseState = null;
                rebuildingFromUndo = false;
                RefreshUndoUi();
            }
        }

        public bool RedoLatestAction()
        {
            return RedoLatestAction(localPlayerId);
        }

        public bool RedoLatestAction(PlayerId requestingPlayerId)
        {
            if (!IsActiveSessionPlayer(requestingPlayerId)
                || !CanRedoCurrentAction()
                || !undoHistory.TryPeekRedo(
                    out SessionUndoSnapshot snapshot,
                    out ActiveSessionUndoTransaction transaction))
            {
                return false;
            }

            long redoRevision;
            try
            {
                redoRevision = checked(matchState.Revision + 1L);
            }
            catch (OverflowException)
            {
                ShowMessage("Redo rejected: Match revision cannot advance.");
                return false;
            }

            MatchState replacement;
            TrapFloorSessionState restoredTrapFloor = null;
            PendingControllerPurchaseState restoredPurchase = null;
            try
            {
                replacement = snapshot.Match.Restore(redoRevision);
                restoredTrapFloor = snapshot.TrapFloor?.Restore();
                restoredPurchase = snapshot.PendingControllerPurchase?.Restore();
            }
            catch (Exception exception)
            {
                ShowMessage($"Redo rejected: {exception.Message}");
                return false;
            }

            rebuildingFromUndo = true;
            MatchState previousMatch = activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                Shutdown(true);
                activeSession.ReplaceCurrentMatch(replacement);
                pendingRestoredTrapFloorState = restoredTrapFloor;
                pendingRestoredControllerPurchaseState = restoredPurchase;
                InitializeActiveSession(false);
                undoHistory.CommitRedo();
                undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                ShowActiveSessionUi();
                ShowMessage($"Redid {transaction.Description}.");
                return true;
            }
            catch (Exception exception)
            {
                RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Redo");
                Debug.LogError($"Redo Presentation rebuild failed: {exception.Message}", this);
                throw;
            }
            finally
            {
                pendingRestoredTrapFloorState = null;
                pendingRestoredControllerPurchaseState = null;
                rebuildingFromUndo = false;
                RefreshUndoUi();
            }
        }

        private bool IsActiveSessionPlayer(PlayerId playerId)
        {
            if (playerId.IsEmpty || activeSession == null) return false;
            IReadOnlyList<PlayerId> players = activeSession.Request.ActivePlayerIds;
            for (int i = 0; i < players.Count; i++)
                if (players[i] == playerId) return true;
            return false;
        }

        private bool HandleUndoShortcut()
        {
            Keyboard keyboard = Keyboard.current;
            if (!IsInitialized
                || keyboard == null
                || !keyboard.zKey.wasPressedThisFrame
                || !(keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
                || keyboard.leftShiftKey.isPressed
                || keyboard.rightShiftKey.isPressed)
            {
                return false;
            }

            UndoLatestAction();
            return true;
        }

        private bool HandleRedoShortcut()
        {
            Keyboard keyboard = Keyboard.current;
            if (!IsInitialized || keyboard == null)
            {
                return false;
            }

            bool controlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool shiftPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            bool redoPressed = keyboard.yKey.wasPressedThisFrame
                || (shiftPressed && keyboard.zKey.wasPressedThisFrame);
            if (!controlPressed || !redoPressed)
            {
                return false;
            }

            RedoLatestAction();
            return true;
        }

        private bool CanUndoCurrentAction()
        {
            return IsInitialized
                && !rebuildingFromUndo
                && !undoTransactionInProgress
                && undoHistory.CanUndo;
        }

        private bool CanRedoCurrentAction()
        {
            return IsInitialized
                && !rebuildingFromUndo
                && !undoTransactionInProgress
                && undoHistory.CanRedo;
        }

        private void BeginUndoTrackingForCurrentMatch()
        {
            StopUndoTrackingCurrentMatch();
            undoTrackedMatch = matchState;
            undoTrackedMatch.AuthoritativeActionAccepted += HandleAuthoritativeActionAccepted;
            undoTransactionInProgress = false;
            if (!rebuildingFromUndo)
            {
                undoHistory.EstablishBaseline(CaptureUndoSnapshot());
            }
            RefreshUndoUi();
        }

        private void StopUndoTrackingCurrentMatch()
        {
            if (undoTrackedMatch != null)
            {
                undoTrackedMatch.AuthoritativeActionAccepted -= HandleAuthoritativeActionAccepted;
                undoTrackedMatch = null;
            }
            undoTransactionInProgress = false;
        }

        private void HandleAuthoritativeActionAccepted(AuthoritativeActionAcceptance acceptance)
        {
            pileCountCheckPending = true; // landing taps and small-stack tidy-up (TabletopComposition.Stacking.cs)

            if (acceptance.Kind == AuthoritativeActionKind.TransferCard
                || acceptance.Kind == AuthoritativeActionKind.DrawCards)
            {
                RefreshAcceptedCardFaces(); // a Hand turns arriving cards face up (H1b)
            }

            if ((acceptance.Kind == AuthoritativeActionKind.MoveObject
                    || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
                && IsGuidedMoveShown())
            {
                // R4: a move made while an Ability or a Trap shows where to go is not a regular move.
                EnsureMoveTurn();
                guidedMoveThisTurn = true;
            }

            if ((acceptance.Kind == AuthoritativeActionKind.MoveObject
                    || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
                && trapFloorAbilityResolutionService != null
                && trapFloorAbilityResolutionService.ClearMovementAssistanceIfPawnMoved(matchState))
            {
                abilityTargetPresenter?.ClearCurrent();
            }

            if ((acceptance.Kind == AuthoritativeActionKind.MoveObject
                    || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
                && trapFloorTemplate != null)
            {
                landingCheckPending = true;
            }

            if (acceptance.Kind == AuthoritativeActionKind.MoveObject
                || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
            {
                consoleDieCheckPending = true; // C3d: a die may have come to rest on a Console D-pad arrow
                boardSnapCheckPending = true; // C3e: a card may have come to rest on a board space
            }

            if (undoBatchActive)
            {
                undoBatchLastAcceptance = acceptance;
                return;
            }

            switch (acceptance.RecordMode)
            {
                case AuthoritativeActionRecordMode.Intermediate:
                    undoTransactionInProgress = true;
                    RefreshUndoUi();
                    break;
                case AuthoritativeActionRecordMode.Transaction:
                    SessionUndoSnapshot beforeState = undoHistory.CurrentState;
                    SessionUndoSnapshot afterState = CaptureUndoSnapshot();
                    undoHistory.RecordAccepted(
                        beforeState,
                        afterState,
                        acceptance,
                        DescribeUndoAction(acceptance));
                    Debug.Log(
                        $"[Undo] Recorded state index {undoHistory.CurrentStateIndex}; "
                        + $"transactions={undoHistory.TransactionCount}; "
                        + $"action={DescribeUndoAction(acceptance)}; "
                        + $"beforeObjects={beforeState.Match.ObjectCount}; "
                        + $"afterObjects={afterState.Match.ObjectCount}; "
                        + $"afterIds={FormatUndoObjectIds(afterState.Match)}.",
                        this);
                    undoTransactionInProgress = false;
                    RefreshUndoUi();
                    RaiseHint(ActionHintTrigger(acceptance.Kind));
                    RaiseHint(HintTriggers.UndoAvailable);
                    break;
                case AuthoritativeActionRecordMode.CancelTransaction:
                    undoTransactionInProgress = false;
                    undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                    RefreshUndoUi();
                    break;
                default:
                    if (!undoTransactionInProgress)
                    {
                        undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                        RefreshUndoUi();
                    }
                    break;
            }
        }

        private void BeginUndoBatch()
        {
            undoBatchActive = undoTrackedMatch == matchState && undoHistory.CurrentStateIndex >= 0;
            undoBatchBefore = undoBatchActive ? undoHistory.CurrentState : null;
            undoBatchLastAcceptance = null;
        }

        private void EndUndoBatch()
        {
            if (!undoBatchActive) return;
            undoBatchActive = false;
            if (undoBatchLastAcceptance.HasValue && undoTrackedMatch == matchState)
            {
                AuthoritativeActionAcceptance last = undoBatchLastAcceptance.Value;
                undoHistory.RecordAccepted(
                    undoBatchBefore,
                    CaptureUndoSnapshot(),
                    last,
                    DescribeUndoAction(last));
                undoTransactionInProgress = false;
                RefreshUndoUi();
            }

            undoBatchBefore = null;
            undoBatchLastAcceptance = null;
        }

        // Folds follow-up tidying (a stack left with one card turned back into a card) into the step the player
        // just made, so Undo never brings back the in-between state (a stack of one). When that step is still
        // being recorded (the card taken off is still falling), nothing is replaced: the step is recorded when the
        // card lands, with the tidy-up in it. Snapshots are never taken of a card in mid-air.
        private void BeginUndoAmend()
        {
            BeginUndoBatch();
        }

        private void EndUndoAmend()
        {
            if (!undoBatchActive) return;
            undoBatchActive = false;
            if (undoBatchLastAcceptance.HasValue && undoTrackedMatch == matchState && !undoTransactionInProgress)
            {
                undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                RefreshUndoUi();
            }

            undoBatchBefore = null;
            undoBatchLastAcceptance = null;
        }

        private SessionUndoSnapshot CaptureUndoSnapshot()
        {
            return SessionUndoSnapshot.Capture(
                matchState,
                trapFloorActivityFeed,
                trapFloorObjectiveState,
                trapFloorCollapseState,
                trapFloorTurnState,
                trapFloorAbilityResolutionState,
                pendingControllerPurchaseState);
        }

        private static string FormatUndoObjectIds(GameTemplateInitialSnapshot snapshot)
        {
            IReadOnlyList<TabletopObjectId> ids = snapshot.CopyObjectIds();
            if (ids.Count == 0) return "<none>";
            string result = ids[0].ToString();
            for (int i = 1; i < ids.Count; i++) result += $",{ids[i]}";
            return result;
        }

        private string DescribeUndoAction(AuthoritativeActionAcceptance acceptance)
        {
            string actor = FormatUndoActor(acceptance.ActorPlayerId);
            switch (acceptance.Kind)
            {
                case AuthoritativeActionKind.MoveObject: return $"{actor} moved an object";
                case AuthoritativeActionKind.MoveContainer: return $"{actor} moved a Container";
                case AuthoritativeActionKind.RotateObject: return $"{actor} rotated an object";
                case AuthoritativeActionKind.FlipCard: return $"{actor} flipped a Card";
                case AuthoritativeActionKind.TransferCard: return $"{actor} transferred a Card";
                case AuthoritativeActionKind.TransferToken: return $"{actor} transferred a Token";
                case AuthoritativeActionKind.ShuffleDeck: return $"{actor} shuffled a Deck";
                case AuthoritativeActionKind.DrawCards: return $"{actor} drew Cards";
                case AuthoritativeActionKind.ReorderContainer: return $"{actor} reordered a Container";
                case AuthoritativeActionKind.MergeStacks: return $"{actor} merged Stacks";
                case AuthoritativeActionKind.SplitStack: return $"{actor} split a Stack";
                case AuthoritativeActionKind.CreateComponent: return $"{actor} created a Component";
                case AuthoritativeActionKind.DuplicateComponent: return $"{actor} duplicated a Component";
                case AuthoritativeActionKind.DeleteComponent: return $"{actor} deleted a Component";
                case AuthoritativeActionKind.PopulateDeck: return $"{actor} populated a Deck";
                case AuthoritativeActionKind.PhysicalObjectSettled: return $"{actor} moved a physical object";
                case AuthoritativeActionKind.TrapFloorReveal: return $"{actor} searched a Floor";
                case AuthoritativeActionKind.TrapFloorClaimKey: return $"{actor} claimed a Key";
                case AuthoritativeActionKind.TrapFloorAttemptEscape: return $"{actor} attempted Escape";
                case AuthoritativeActionKind.TrapFloorCollapse: return $"{actor} collapsed a Floor";
                case AuthoritativeActionKind.PurchaseActionOrAbility: return $"{actor} bought or took an Ability";
                case AuthoritativeActionKind.FlipConsole: return $"{actor} turned a Console over";
                case AuthoritativeActionKind.MoveBoard: return $"{actor} moved a Board";
                case AuthoritativeActionKind.ResizeBoardGrid: return $"{actor} changed a Board's grid";
                default:
                    return trapFloorTurnState == null
                        ? $"{actor} performed a table action"
                        : $"{actor} advanced the Trap Floor turn";
            }
        }

        private string FormatUndoActor(PlayerId playerId)
        {
            if (activeSession != null)
            {
                IReadOnlyList<PlayerId> players = activeSession.Request.ActivePlayerIds;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] == playerId) return $"P{i + 1}";
                }
            }
            return "Player";
        }

        private void RefreshUndoUi()
        {
            ActiveSessionUndoTransaction nextUndo = undoHistory.NextUndo;
            runtimeUi?.SetUndoState(
                CanUndoCurrentAction(),
                nextUndo == null ? "Undo" : $"Undo: {nextUndo.Description}");
            ActiveSessionUndoTransaction nextRedo = undoHistory.NextRedo;
            runtimeUi?.SetRedoState(
                CanRedoCurrentAction(),
                nextRedo == null ? "Redo" : $"Redo: {nextRedo.Description}");
        }
    }
}
