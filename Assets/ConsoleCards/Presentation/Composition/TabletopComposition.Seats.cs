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
    /// <summary>Seats on this screen, hot-seat handover and the activity log. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        // Seats (H1b): this screen plays one seat. The screen hand is that seat's hand and every other hand lies
        // face down at its owner's table edge. Switching rebuilds the Presentation for the new seat through the
        // same path Undo uses (the camera turns to that seat); game state and the Undo history are not changed.
        private bool SwitchScreenSeat(PlayerId target, bool announceTurn)
        {
            string blocked = !IsInitialized ? "the table is not ready"
                : rebuildingFromUndo ? "the table is rebuilding"
                : activeSession == null ? "there is no active session"
                : templateContext == null || templateContext.TrapFloorTemplate == null
                    ? "this table has no seats"
                : target.IsEmpty ? "no player was chosen"
                : target == localPlayerId ? "this screen already plays that seat"
                : null;
            if (blocked != null)
            {
                SeatSwitchFailed(target, blocked);
                return false;
            }

            TemplateContext next;
            try
            {
                next = CreateTrapFloorTemplateContext(
                    templateContext.Session,
                    templateContext.TrapFloorTemplate,
                    target);
            }
            catch (InvalidOperationException exception)
            {
                handSeatSyncFailedFor = target; // not retried every frame
                SeatSwitchFailed(target, exception.Message);
                return false;
            }

            RememberSeatLeaving(localPlayerId);
            return RebuildPresentationFor(next, target, announceTurn, "Seat switch");
        }

        // Rebuilds the whole Presentation for a template context (a seat, or new view settings) through the path
        // Undo uses. Game state and the Undo history do not change.
        private bool RebuildPresentationFor(
            TemplateContext next,
            PlayerId failedFor,
            bool announceTurn,
            string operationName)
        {
            TemplateContext previousContext = templateContext;
            MatchState previousMatch = activeSession.CurrentMatch;
            SessionUndoSnapshot current = CaptureUndoSnapshot();
            rebuildingFromUndo = true;
            try
            {
                Shutdown(true);
                templateContext = next;
                pendingRestoredTrapFloorState = current.TrapFloor?.Restore();
                pendingRestoredControllerPurchaseState = current.PendingControllerPurchase?.Restore();
                InitializeActiveSession(false);
                undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                ShowActiveSessionUi();
                announceTurnAfterRebuild = announceTurn;
                return true;
            }
            catch (Exception exception)
            {
                templateContext = previousContext;
                if (!failedFor.IsEmpty) handSeatSyncFailedFor = failedFor;
                RestorePresentationAfterFailedRebuild(previousMatch, current, operationName);
                Debug.LogError($"[Seat] {operationName}: Presentation rebuild failed: {exception}", this);
                ShowMessage($"{operationName} failed: {exception.Message}");
                return false;
            }
            finally
            {
                pendingRestoredTrapFloorState = null;
                pendingRestoredControllerPurchaseState = null;
                rebuildingFromUndo = false;
                RefreshUndoUi();
            }
        }

        // Table menu: Hands on table (F3). Rebuilds the table view so the hand moves between tray and table.
        private void SetHandsOnTable(bool on)
        {
            bool before = handsOnTable;
            handsOnTable = on;
            if (!IsInitialized || activeSession == null || templateContext?.TrapFloorTemplate == null) return;
            if (seatSwitchPending || rebuildingFromUndo)
            {
                handsOnTable = before;
                ShowMessage("The table is busy; try again in a moment.");
                ShowActiveSessionUi();
                return;
            }

            if (!RebuildPresentationFor(templateContext, PlayerId.Empty, false, "Hands on table"))
            {
                handsOnTable = before;
                return;
            }

            ShowMessage(on
                ? "Hands on table: your hand lies face up at your table edge."
                : "Your hand is back on screen.");
        }

        private void SetFullFeedback(bool on)
        {
            fullFeedback = on;
            ShowMessage(on
                ? "Full feedback: banners, sounds and camera nudges."
                : "Minimal feedback: the turn banner, messages and board markers.");
        }

        // Table menu Seat row: this screen plays the next seated player (not in hot-seat mode).
        private void NextScreenSeat()
        {
            Debug.Log("[Seat] Seat row clicked.", this);
            if (!IsInitialized || trapFloorTemplate == null || matchState == null)
            {
                SeatSwitchFailed(PlayerId.Empty, "this table has no seats");
                return;
            }

            if (hotSeatMode)
            {
                ShowMessage("Hot-seat is on: the screen follows the turn.");
                return;
            }

            int count = trapFloorTemplate.Players.Count;
            int localIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (matchState.GetSeat(trapFloorTemplate.Players[i].SeatId).OccupantPlayerId == localPlayerId)
                {
                    localIndex = i;
                    break;
                }
            }

            for (int step = 1; step < count; step++)
            {
                PlayerId candidate = matchState.GetSeat(
                    trapFloorTemplate.Players[(localIndex + step + count) % count].SeatId).OccupantPlayerId;
                if (candidate.IsEmpty || candidate == localPlayerId) continue;
                BeginSeatSwitch(candidate, false);
                return;
            }

            ShowMessage("There is no other seated player.");
        }

        // Slides the screen hand away, then (next frames) switches the screen to the target seat.
        private void SeatSwitchFailed(PlayerId target, string reason)
        {
            Debug.LogWarning($"[Seat] Could not switch this screen to {PlayerLabel(target)}: {reason}.", this);
            ShowMessage($"Could not switch seat: {reason}.");
        }

        private bool BeginSeatSwitch(PlayerId target, bool fromHandover)
        {
            if (seatSwitchPending)
            {
                Debug.LogWarning("[Seat] A seat switch is already running.", this);
                return false;
            }

            if (target.IsEmpty || target == localPlayerId)
            {
                SeatSwitchFailed(target, target.IsEmpty ? "no player was chosen" : "this screen already plays that seat");
                return false;
            }

            Debug.Log($"[Seat] Switching this screen from {PlayerLabel(localPlayerId)} to {PlayerLabel(target)}.", this);
            seatSwitchPending = true;
            seatSwitchTarget = target;
            seatSwitchFromHandover = fromHandover;
            seatSwitchDeadline = Time.unscaledTime + HandTrayRig.ClearSeconds + 0.3f;
            seatRevealAt = -1f;
            handTrayRig?.SetCleared(true);
            return true;
        }

        private void CompleteSeatSwitch()
        {
            seatSwitchPending = false;
            PlayerId target = seatSwitchTarget;
            bool fromHandover = seatSwitchFromHandover;
            if (fromHandover
                && (trapFloorTurnState == null || trapFloorTurnState.ActivePlayerId != target))
            {
                handTrayRig?.SetCleared(false);
                runtimeUi?.FadeOutHotSeatHandover();
                return; // the turn moved meanwhile; hot-seat opens a new handover if needed
            }

            string gained = fromHandover ? HandGainText(target) : string.Empty;
            bool switched = SwitchScreenSeat(target, fromHandover);
            if (fromHandover) runtimeUi?.FadeOutHotSeatHandover(); // the cover lifts while the camera travels
            if (!switched)
            {
                handTrayRig?.SetCleared(false);
                return;
            }

            MoveCameraToScreenSeat();
            seatRevealAt = Time.unscaledTime + (seatSwitchCameraSeconds * 0.65f);
            if (fromHandover)
            {
                if (gained.Length > 0) ShowMessage(gained.TrimStart());
            }
            else
            {
                ShowMessage($"This screen now plays {PlayerLabel(target)}.");
            }
        }

        // The camera travels to the seat this screen now plays (its console and the board, from behind it).
        private void MoveCameraToScreenSeat()
        {
            TabletopCameraController cameraController = cameraInputAdapter != null ? cameraInputAdapter.CameraController : null;
            if (cameraController == null) return;
            bool moved = cameraController.ShowPreset(seatSwitchCameraView, seatSwitchCameraSeconds)
                || cameraController.ShowMidView()
                || cameraController.ShowCloseView();
            if (!moved) ProjectTrapFloorCameraBookmark();
            Debug.Log(
                $"[Seat] Screen seat is now {PlayerLabel(localPlayerId)} (layout seat {localPlayerLayoutSeatIndex + 1}, "
                + $"facing {(localSeatLayout != null ? localSeatLayout.FacingRotationDegrees : 0f)}°); camera view "
                + (moved ? seatSwitchCameraView.ToString() : "bookmark") + ".",
                this);
        }

        private void SetHotSeatMode(bool on)
        {
            hotSeatMode = on;
            handSeatSyncFailedFor = PlayerId.Empty;
            ShowMessage(on
                ? "Hot-seat on: at each turn the screen passes to that player after a handover."
                : "Hot-seat off: this screen keeps its seat.");
        }

        // H2: opens the handover when the turn belongs to someone else, keeps it on the right player, and closes
        // it when it is no longer needed (mode off, the turn came back, or a Floor turn).
        private void UpdateHotSeat()
        {
            if (seatSwitchPending) return;
            if (!hotSeatMode || !ScreenHandBehindTurn())
            {
                if (hotSeatHandoverOpen) CloseHotSeatHandover();
                return;
            }

            PlayerId active = trapFloorTurnState.ActivePlayerId;
            if (hotSeatHandoverOpen && hotSeatHandoverFor == active) return;
            OpenHotSeatHandover(active);
        }

        private void OpenHotSeatHandover(PlayerId player)
        {
            hotSeatHandoverFor = player;
            runtimeUi?.ClearActiveSessionTransientUi();
            string label = PlayerLabel(player);
            HotSeatHandoverModel model = new HotSeatHandoverModel(
                "PASS THE SCREEN",
                label.ToUpperInvariant(),
                BuildHandoverSummary(player),
                $"I'm {FormatPlayerName(player)} — show my hand",
                FeedbackColour(player));
            if (runtimeUi != null && runtimeUi.ShowHotSeatHandover(model, ConfirmHotSeatHandover))
            {
                hotSeatHandoverOpen = true;
                handTrayRig?.SetCleared(true); // the previous hand slides away as the cover fades in
                return;
            }

            // No handover prefab: pass the screen straight away so the mode still works.
            hotSeatHandoverOpen = false;
            ShowMessage($"Screen passing to {label}.");
            BeginSeatSwitch(player, true);
        }

        private void ConfirmHotSeatHandover()
        {
            PlayerId target = hotSeatHandoverFor;
            hotSeatHandoverOpen = false; // the cover stays up until the new seat is ready, then fades
            if (!IsInitialized
                || trapFloorTurnState == null
                || trapFloorTurnState.ActivePlayerId != target)
            {
                // The turn moved while the cover was up: lift it; the next frame opens a new handover if needed.
                runtimeUi?.FadeOutHotSeatHandover();
                handTrayRig?.SetCleared(false);
                return;
            }

            if (!BeginSeatSwitch(target, true))
            {
                runtimeUi?.FadeOutHotSeatHandover();
                handTrayRig?.SetCleared(false);
            }
        }

        private void CloseHotSeatHandover()
        {
            hotSeatHandoverOpen = false;
            runtimeUi?.FadeOutHotSeatHandover();
            handTrayRig?.SetCleared(false);
        }

        // What happened since this player last had the screen: the latest lines of the shared log, and their hand.
        private string BuildHandoverSummary(PlayerId player)
        {
            handoverSummary.Clear();
            bool seenBefore = seatLeftAtLog.TryGetValue(player, out long from);
            if (!seenBefore) from = Math.Max(0L, handoverLogCount - 4);
            long start = Math.Max(Math.Max(from, handoverLogCount - HandoverSummaryLines), handoverLogCount - HandoverLogSize);
            if (handoverLogCount > from)
            {
                handoverSummary.Append(seenBefore ? "Since your last turn:" : "Lately:");
                if (start > from) handoverSummary.Append("\n• …");
                for (long i = start; i < handoverLogCount; i++)
                {
                    handoverSummary.Append("\n• ").Append(handoverLog[i % HandoverLogSize]);
                }
            }
            else
            {
                handoverSummary.Append(seenBefore ? "Nothing new since your last turn." : "Your first turn on this screen.");
            }

            string gained = HandGainText(player);
            if (gained.Length > 0) handoverSummary.Append("\n\n").Append(gained.TrimStart());
            return handoverSummary.ToString();
        }

        // " Your hand: +2 cards since your last turn." or empty.
        private string HandGainText(PlayerId player)
        {
            if (!seatLeftHandCount.TryGetValue(player, out int before)) return string.Empty;
            int gained = PlayerHandCount(player) - before;
            return gained > 0
                ? $" Your hand: +{gained} card{(gained == 1 ? string.Empty : "s")} since your last turn."
                : string.Empty;
        }

        private int PlayerHandCount(PlayerId player)
        {
            return TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                && matchState.Containers.TryGetValue(setup.HandContainerId, out ContainerState hand)
                ? hand.Count
                : 0;
        }

        private void RememberSeatLeaving(PlayerId player)
        {
            if (player.IsEmpty) return;
            seatLeftAtLog[player] = handoverLogCount;
            seatLeftHandCount[player] = PlayerHandCount(player);
        }

        // Every message and banner goes into a short shared log the handover summarises.
        private void RecordHandoverLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            handoverLog[handoverLogCount % HandoverLogSize] = line;
            handoverLogRound[handoverLogCount % HandoverLogSize] = trapFloorTurnState != null ? trapFloorTurnState.CurrentRound : 0;
            handoverLogCount++;
        }

        // F4: the "What happened" text, newest first, with the round each line happened in. Built only when the
        // log changes.
        private void RefreshActivityLog()
        {
            shownActivityLogCount = handoverLogCount;
            if (activityLogUnavailable || runtimeUi == null) return;
            activityLogText.Clear();
            long oldest = Math.Max(0L, handoverLogCount - Math.Min(ActivityLogLines, HandoverLogSize));
            for (long i = handoverLogCount - 1; i >= oldest; i--)
            {
                int slot = (int)(i % HandoverLogSize);
                if (activityLogText.Length > 0) activityLogText.Append('\n');
                int round = handoverLogRound[slot];
                if (round > 0) activityLogText.Append("<color=#e0531f><b>R").Append(round).Append("</b></color>  ");
                activityLogText.Append(handoverLog[slot]);
            }

            if (activityLogText.Length == 0) activityLogText.Append("Nothing yet.");
            if (!runtimeUi.ShowActivityLog(activityLogText.ToString()))
            {
                activityLogUnavailable = true;
            }
        }

        private void FeedbackLine(string message, FeedbackSeverity severity)
        {
            RecordHandoverLine(message);
            runtimeUi.ShowFeedbackMessage(message, severity);
        }

        private string PlayerLabel(PlayerId player)
        {
            string colour = TryGetTrapFloorSeatIndex(player, out int seat) ? $" ({PlayerColorName(seat)})" : string.Empty;
            return $"{FormatPlayerName(player)}{colour}";
        }

        // True while a Player turn belongs to someone whose hand is not the screen hand.
        private bool ScreenHandBehindTurn()
        {
            return trapFloorTurnState != null
                && templateContext != null
                && templateContext.TrapFloorTemplate != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && !trapFloorTurnState.ActivePlayerId.IsEmpty
                && trapFloorTurnState.ActivePlayerId != localPlayerId
                && trapFloorTurnState.ActivePlayerId != handSeatSyncFailedFor;
        }

        // The hand a draw from this deck fills: the deck owner's hand (a Console's Controller Deck belongs to
        // its seat), otherwise the screen hand.
        private ContainerId DrawTargetHandFor(ContainerId deckId)
        {
            if (trapFloorTemplate != null
                && matchState != null
                && matchState.Containers.TryGetValue(deckId, out ContainerState deck))
            {
                for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                    if (player.ControllerDeckId == deckId
                        || (!deck.OwnerSeatId.IsEmpty && deck.OwnerSeatId == player.SeatId))
                    {
                        return player.HandContainerId;
                    }
                }
            }

            return handContainerId;
        }

        // " to Player 2 (RED)'s hand" when the hand is not this screen's hand; empty otherwise.
        private string ToHandSuffix(ContainerId handId)
        {
            return handId == handContainerId ? string.Empty : $" to {HandOwnerName(handId)}'s hand";
        }

        private string HandOwnerName(ContainerId handId)
        {
            if (trapFloorTemplate != null && matchState != null)
            {
                for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                    if (player.HandContainerId == handId)
                    {
                        PlayerId owner = matchState.GetSeat(player.SeatId).OccupantPlayerId;
                        string colour = TryGetTrapFloorSeatIndex(owner, out int seat) ? $" ({PlayerColorName(seat)})" : string.Empty;
                        return $"{FormatPlayerName(owner)}{colour}";
                    }
                }
            }

            return "the";
        }
    }
}
