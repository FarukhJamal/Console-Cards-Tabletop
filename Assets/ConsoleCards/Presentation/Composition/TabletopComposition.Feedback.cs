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
                dragTargetRejects = true; // UX-1: the cursor shows "no" over a pile that will not take the card
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
            dragTargetRejects = false;
            foreach (ContainerFeedbackTarget target in feedbackTargetsByContainerId.Values)
            {
                target.Clear();
            }
        }

        internal void ShowMessage(string message)
        {
            RecordHandoverLine(message);
            // Owner, 2026-10-19: ordinary action messages no longer pop up (Feel Settings › Show Action Messages);
            // warnings still do.
            if (!Feel.ShowActionMessages && SeverityOf(message) == FeedbackSeverity.Info) return;
            // With the message stack, messages go there and the status strip keeps only the standing status.
            if (playerFeedback
                && runtimeUi != null
                && runtimeUi.ShowFeedbackMessage(message, SeverityOf(message)))
            {
                operationMessageUntil = 0f;
                runtimeUi.SetStatusMessage(ActiveCartridge.StatusText());
                return;
            }

            operationMessage = message;
            operationMessageUntil = Time.unscaledTime + 2.5f;
            runtimeUi?.SetStatusMessage(ActiveCartridge.StatusText());
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

        internal Color FeedbackColour(PlayerId player) =>
            !player.IsEmpty && TryGetSeatIndex(player, out int seat)
                ? PlayerSeatColor(seat)
                : new Color(0.878f, 0.325f, 0.122f);

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

        internal void Banner(FeedbackEventKind kind, string title, string subtitle, Color accent)
        {
            if (kind != FeedbackEventKind.TurnStart)
            {
                RecordHandoverLine(string.IsNullOrEmpty(subtitle) ? title : $"{title} — {subtitle}");
            }

            // Owner, 2026-10-24: no big banners in the middle of the screen (turns, eliminated, ...); the line above
            // goes to the Log, and the turn shows on the round bar and the player card.
        }
    }
}
