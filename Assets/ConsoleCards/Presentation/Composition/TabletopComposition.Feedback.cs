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
    /// <summary>Drag feedback on containers, messages, banners, announcements and board markers. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        void IContainedCardDragFeedback.Begin(ContainerId sourceContainerId)
        {
            sourceFeedbackContainerId = sourceContainerId;
            ClearFeedback();
            if (feedbackTargetsByContainerId.TryGetValue(sourceContainerId, out ContainerFeedbackTarget target))
            {
                target.SetSource();
            }
        }

        void IContainedCardDragFeedback.Update(
            ContainerId sourceContainerId,
            CardDropTarget target,
            bool targetWouldAccept)
        {
            ClearFeedback();
            if (feedbackTargetsByContainerId.TryGetValue(sourceContainerId, out ContainerFeedbackTarget sourceTarget))
            {
                sourceTarget.SetSource();
            }

            if (target.Kind != CardDropTargetKind.Container)
            {
                return;
            }

            if (!feedbackTargetsByContainerId.TryGetValue(target.ContainerId, out ContainerFeedbackTarget feedbackTarget))
            {
                return;
            }

            if (target.ContainerId == sourceContainerId)
            {
                feedbackTarget.SetSource();
            }
            else if (targetWouldAccept)
            {
                feedbackTarget.SetValid();
            }
            else
            {
                feedbackTarget.SetInvalid();
            }
        }

        void IContainedCardDragFeedback.ShowRejected(ContainerId sourceContainerId, CardDropTarget target)
        {
            ClearFeedback();
            if (target.Kind == CardDropTargetKind.Container
                && feedbackTargetsByContainerId.TryGetValue(target.ContainerId, out ContainerFeedbackTarget feedbackTarget))
            {
                feedbackTarget.SetInvalid();
                feedbackHoldUntil = Time.unscaledTime + feedbackDuration;
            }

            ShowMessage("Transfer rejected.");
        }

        void IContainedCardDragFeedback.Clear()
        {
            ClearFeedback();
        }

        private void ClearFeedback()
        {
            feedbackHoldUntil = 0f;
            foreach (ContainerFeedbackTarget target in feedbackTargetsByContainerId.Values)
            {
                target.Clear();
            }
        }

        internal void ShowMessage(string message)
        {
            RecordHandoverLine(message);
            // With the message stack, messages go there and the status strip keeps only the standing status.
            if (playerFeedback
                && runtimeUi != null
                && runtimeUi.ShowFeedbackMessage(message, SeverityOf(message)))
            {
                operationMessageUntil = 0f;
                runtimeUi.SetStatusMessage(TrapFloorGame.CurrentStatusText());
                return;
            }

            operationMessage = message;
            operationMessageUntil = Time.unscaledTime + 2.5f;
            runtimeUi?.SetStatusMessage(TrapFloorGame.CurrentStatusText());
        }

        private static FeedbackSeverity SeverityOf(string message)
        {
            if (string.IsNullOrEmpty(message)) return FeedbackSeverity.Info;
            if (message.IndexOf("ELIMINATED", StringComparison.Ordinal) >= 0) return FeedbackSeverity.Loss;
            if (message.IndexOf("rejected", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("first", StringComparison.OrdinalIgnoreCase) >= 0)
                return FeedbackSeverity.Warning;
            return FeedbackSeverity.Info;
        }

        // Player feedback (doc 25, F1). Turns new activity-feed entries and turn changes into banners and
        // messages. A rebuilt or reset session (new or shorter feed) is taken as already announced.
        private void AnnounceTrapFloorFeedback()
        {
            if (!playerFeedback
                || runtimeUi == null
                || trapFloorActivityFeed == null
                || trapFloorTurnState == null
                || rebuildingFromUndo)
            {
                return;
            }

            IReadOnlyList<TrapFloorActivityEntry> entries = trapFloorActivityFeed.Entries;
            long newest = entries.Count > 0 ? entries[entries.Count - 1].Sequence : 0L;
            if (!ReferenceEquals(announcedFeed, trapFloorActivityFeed)
                || !ReferenceEquals(announcedTurnState, trapFloorTurnState)
                || newest < announcedSequence)
            {
                announcedFeed = trapFloorActivityFeed;
                announcedTurnState = trapFloorTurnState;
                announcedSequence = newest;
                RememberAnnouncedTurn();
                if (entries.Count == 0 || announceTurnAfterRebuild) AnnounceTurn(); // new game, or the hand moved on
                announceTurnAfterRebuild = false;
                return;
            }

            if (newest > announcedSequence)
            {
                int first = entries.Count;
                while (first > 0 && entries[first - 1].Sequence > announcedSequence) first--;
                for (int i = first; i < entries.Count; i++) AnnounceActivity(entries[i]);
                announcedSequence = newest;
            }

            if (trapFloorTurnState.CurrentRound != announcedRound
                || trapFloorTurnState.Phase != announcedPhase
                || trapFloorTurnState.ActivePlayerId != announcedActivePlayer)
            {
                RememberAnnouncedTurn();
                AnnounceTurn();
            }
        }

        private void RememberAnnouncedTurn()
        {
            announcedRound = trapFloorTurnState.CurrentRound;
            announcedPhase = trapFloorTurnState.Phase;
            announcedActivePlayer = trapFloorTurnState.ActivePlayerId;
        }

        private void AnnounceTurn()
        {
            if (trapFloorTurnState.IsCurrentFloorFailed) return;
            RulesCardPanel.RaiseHint(HintTriggers.TurnStart, trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn ? "player" : "floor");
            if (trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                PlayerId active = trapFloorTurnState.ActivePlayerId;
                string colour = TryGetTrapFloorSeatIndex(active, out int seat) ? $" ({PlayerColorName(seat)})" : string.Empty;
                runtimeUi.ShowFeedbackBanner(new FeedbackBannerModel(
                    FeedbackEventKind.TurnStart,
                    $"{TrapFloorGame.FormatPlayerName(active).ToUpperInvariant()}{colour} — YOUR TURN",
                    $"ROUND {trapFloorTurnState.CurrentRound}",
                    FeedbackColour(active)));
            }
            else if (trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
            {
                runtimeUi.ShowFeedbackBanner(new FeedbackBannerModel(
                    FeedbackEventKind.RoundStart,
                    "FLOOR TURN",
                    $"{TrapFloorGame.FormatPlayerName(trapFloorTurnState.FloorOperatorPlayerId)} rolls the floor",
                    new Color(0.878f, 0.325f, 0.122f)));
            }
        }

        private Color FeedbackColour(PlayerId player) =>
            !player.IsEmpty && TryGetTrapFloorSeatIndex(player, out int seat)
                ? PlayerSeatColor(seat)
                : new Color(0.878f, 0.325f, 0.122f);

        private void AnnounceActivity(TrapFloorActivityEntry entry)
        {
            PlayerId actor = entry.ActorPlayerId;
            string who = actor.IsEmpty ? "A player" : TrapFloorGame.FormatPlayerName(actor);
            Color colour = FeedbackColour(actor);
            string name = entry.ContentName ?? string.Empty;
            BoardFeedbackAt(entry, colour);
            RulesCardPanel.RaiseHint(HintTriggers.GameActivity, entry.Kind.ToString());
            if (entry.Kind == TrapFloorActivityKind.RevealedFloorContent)
                RulesCardPanel.RaiseHint(HintTriggers.ContentRevealed, entry.ContentCategory.ToString());
            switch (entry.Kind)
            {
                case TrapFloorActivityKind.RevealedFloorContent:
                    if (entry.ContentCategory == TrapFloorFloorContentCategory.Trap && !entry.TrapSuppressed)
                    {
                        Banner(FeedbackEventKind.Trap, $"TRAP! {name.ToUpperInvariant()}", $"{who} flipped it", colour);
                    }
                    else if (entry.ContentCategory == TrapFloorFloorContentCategory.Key
                        || entry.ContentCategory == TrapFloorFloorContentCategory.Entry
                        || entry.ContentCategory == TrapFloorFloorContentCategory.SecretExit)
                    {
                        Banner(FeedbackEventKind.Reward, $"FOUND: {name.ToUpperInvariant()}", who, colour);
                    }
                    else if (entry.ContentCategory != TrapFloorFloorContentCategory.Trap)
                    {
                        FeedbackLine($"{who} found {name}.", FeedbackSeverity.Gain);
                    }
                    break;
                case TrapFloorActivityKind.ClaimedKey:
                    Banner(FeedbackEventKind.Reward, "KEY CLAIMED", $"{who} — {name}", colour);
                    break;
                case TrapFloorActivityKind.CollapsedFloor:
                    Banner(FeedbackEventKind.Collapse, "FLOOR COLLAPSED",
                        entry.HasCoordinate ? $"Tile {entry.Coordinate} is now a hole" : string.Empty, colour);
                    break;
                case TrapFloorActivityKind.PlayerEliminated:
                    Banner(FeedbackEventKind.Eliminated, $"{who.ToUpperInvariant()} ELIMINATED",
                        "Out for the rest of this floor", colour);
                    break;
                case TrapFloorActivityKind.AllPlayersEliminated:
                    Banner(FeedbackEventKind.Eliminated, "EVERYONE IS OUT", "This floor has failed",
                        new Color(0.878f, 0.325f, 0.122f));
                    break;
                case TrapFloorActivityKind.PlayersReactivated:
                    FeedbackLine("Everyone is back in for the new round.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.PlayerEscaped:
                    Banner(FeedbackEventKind.Reward, $"{who.ToUpperInvariant()} ESCAPED", string.Empty, colour);
                    break;
                case TrapFloorActivityKind.WonGame:
                    Banner(FeedbackEventKind.Reward, "YOU WIN!", who, colour);
                    break;
                case TrapFloorActivityKind.UsedShield:
                    FeedbackLine($"{who} used Shield.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.UsedDodge:
                    FeedbackLine($"{who} used Dodge — move to a lit tile.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.UsedCheck:
                    FeedbackLine($"{who} used Check — the Trap is harmless.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.UsedDisarm:
                    FeedbackLine($"{who} used Disarm.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.UsedRush:
                    FeedbackLine($"{who} used Rush — move to a lit tile.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.BlindApplied:
                    FeedbackLine($"{who} will be BLIND next round.", FeedbackSeverity.Warning);
                    break;
                case TrapFloorActivityKind.SlowApplied:
                    FeedbackLine($"{who} will be SLOW next round.", FeedbackSeverity.Warning);
                    break;
                case TrapFloorActivityKind.StickyApplied:
                    FeedbackLine($"{who} is STUCK next round.", FeedbackSeverity.Warning);
                    break;
                case TrapFloorActivityKind.SkippedTurn:
                    FeedbackLine($"{who} skipped their turn.", FeedbackSeverity.Info);
                    break;
            }
        }

        // F2: a flash where a tile flipped, a burst and a glance where the floor collapsed, and short camera
        // kicks for a Trap or an elimination. The camera always returns to where it was.
        private void BoardFeedbackAt(TrapFloorActivityEntry entry, Color colour)
        {
            if (!EnsureBoardFeedback()) return;
            BoardFeedbackSet set = boardFeedback.Set;
            // Minimal feedback (F3) keeps the board markers but no camera nudges.
            TabletopCameraController cameraController = fullFeedback && cameraInputAdapter != null
                ? cameraInputAdapter.CameraController
                : null;
            CardView tileView = null;
            bool hasTile = !entry.FloorCardId.IsEmpty && Registry.TryGetCardView(entry.FloorCardId, out tileView);
            Vector3 tile = hasTile ? tileView.transform.position : Vector3.zero;
            switch (entry.Kind)
            {
                case TrapFloorActivityKind.RevealedFloorContent:
                    bool trap = entry.ContentCategory == TrapFloorFloorContentCategory.Trap && !entry.TrapSuppressed;
                    if (hasTile) boardFeedback.Flash(tile, trap ? new Color(0.94f, 0.28f, 0.24f, 1f) : colour);
                    if (trap) cameraController?.Kick(set.TrapKick, set.KickSeconds);
                    break;
                case TrapFloorActivityKind.CollapsedFloor:
                    if (hasTile)
                    {
                        boardFeedback.Burst(tile, new Color(0.878f, 0.325f, 0.122f, 1f));
                        cameraController?.Glance(tile, set.CollapseGlanceSeconds);
                    }

                    cameraController?.Kick(set.CollapseKick, set.KickSeconds);
                    break;
                case TrapFloorActivityKind.PlayerEliminated:
                case TrapFloorActivityKind.AllPlayersEliminated:
                    cameraController?.Kick(set.EliminatedKick, set.KickSeconds * 1.4f);
                    break;
            }
        }

        internal bool EnsureBoardFeedback()
        {
            if (boardFeedback != null) return true;
            if (boardFeedbackUnavailable) return false;
            BoardFeedbackSet set = Resources.Load<BoardFeedbackSet>(BoardFeedbackSet.ResourcePath);
            if (set == null)
            {
                boardFeedbackUnavailable = true;
                Debug.LogWarning(
                    "Board feedback is off: run Console Cards > Feedback > Create Missing Board Feedback Prefabs (Final World).");
                return false;
            }

            boardFeedback = new BoardFeedbackPresenter(set, transform);
            return true;
        }

        // F2 markers every frame: a ring on the active pawn, a warning on each waiting Trap, and the tiles a
        // move must reach (Spring, Crane, Dodge, Rush) with an arrow from the pawn when there is one target.
        private void RefreshBoardFeedback()
        {
            if (!playerFeedback
                || trapFloorTemplate == null
                || trapFloorTurnState == null
                || matchState == null)
            {
                boardFeedback?.HideAll();
                return;
            }

            if (!EnsureBoardFeedback()) return;
            bool playerTurn = !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn;
            PlayerId active = trapFloorTurnState.ActivePlayerId;
            Color colour = FeedbackColour(active);
            bool pawnFound = false;
            Vector3 pawnPosition = Vector3.zero;
            if (playerTurn && TrapFloorGame.TryGetTrapFloorPlayerSetup(active, out TrapFloorPlayerSetupDefinition setup))
            {
                for (int i = 0; i < Registry.pawnViews.Count; i++)
                {
                    PawnView pawnView = Registry.pawnViews[i];
                    if (pawnView != null && pawnView.IsBound && pawnView.ObjectId == setup.PawnId)
                    {
                        pawnPosition = pawnView.transform.position;
                        pawnFound = true;
                        break;
                    }
                }
            }

            boardFeedback.ShowActivePawn(pawnFound, pawnPosition, colour);
            boardFeedbackTraps.Clear();
            if (trapFloorAbilityResolutionState != null)
            {
                IReadOnlyList<TrapFloorTrapResolutionRecord> records = trapFloorAbilityResolutionState.TrapRecords;
                for (int i = 0; i < records.Count; i++)
                {
                    TrapFloorTrapResolutionRecord record = records[i];
                    if (record.IsPending
                        && record.Content.HasSupportedAssistedTrapEffect
                        && Registry.TryGetCardView(record.FloorCardId, out CardView trapView))
                    {
                        boardFeedbackTraps.Add(trapView.transform.position);
                    }
                }
            }

            boardFeedback.ShowTrapWarnings(boardFeedbackTraps);
            boardFeedbackTargets.Clear();
            IReadOnlyList<TabletopObjectId> targetIds = trapFloorAbilityResolutionState?.ActiveRushAssistance?.TargetFloorCardIds
                ?? trapFloorAbilityResolutionState?.ActiveDodgeAssistance?.TargetFloorCardIds;
            if (playerTurn && targetIds != null)
            {
                for (int i = 0; i < targetIds.Count; i++)
                {
                    if (Registry.TryGetCardView(targetIds[i], out CardView targetView))
                        boardFeedbackTargets.Add(targetView.transform.position);
                }
            }

            boardFeedback.ShowMoveTargets(boardFeedbackTargets, colour, pawnFound, pawnPosition);
        }

        private void Banner(FeedbackEventKind kind, string title, string subtitle, Color accent)
        {
            if (kind != FeedbackEventKind.TurnStart)
            {
                RecordHandoverLine(string.IsNullOrEmpty(subtitle) ? title : $"{title} — {subtitle}");
            }

            if (!fullFeedback && kind != FeedbackEventKind.TurnStart && kind != FeedbackEventKind.RoundStart)
            {
                runtimeUi.ShowFeedbackMessage(string.IsNullOrEmpty(subtitle) ? title : $"{title} — {subtitle}",
                    FeedbackSeverity.Info); // Minimal: a message instead of a banner
                return;
            }

            runtimeUi.ShowFeedbackBanner(new FeedbackBannerModel(kind, title, subtitle, accent));
        }
    }
}
