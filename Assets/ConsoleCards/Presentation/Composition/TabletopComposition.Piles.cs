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
    /// <summary>Decks, stacks and hands: shuffle, draw, split, merge, reorder and lay out the cards in a container. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        public ShuffleDeckResult ShuffleDeck(ContainerId targetDeckContainerId)
        {
            EnsureInitialized();
            if (!TryGetDeckPresentation(
                    targetDeckContainerId,
                    out DeckView targetDeckView,
                    out FixedContainerVisual targetDeckVisual))
            {
                ShowMessage("Shuffle rejected: Deck Presentation unavailable.");
                return ShuffleDeckResult.Failure(
                    CommandResultStatus.Rejected,
                    ShuffleDeckError.ContainerMissing);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(targetDeckContainerId);
            ShuffleDeckResult result = new ShuffleDeckUseCase(authoritativeRandomValueSource).Execute(
                matchState,
                new ShuffleDeckCommand(CreateCommandContext(), targetDeckContainerId));
            if (result.Succeeded)
            {
                targetDeckView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    settleDuration);
                presentationTransitions.Pulse(
                    targetDeckVisual.transform,
                    shuffleCompression,
                    feedbackDuration);
                ShowMessage("Deck shuffled.");
            }
            else
            {
                targetDeckView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Shuffle rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
            return result;
        }

        public ControllerInputHandDrawResult DrawUpToConfiguredHandLimit()
        {
            EnsureInitialized();
            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState capHand))
            {
                // Draw To Hand Limit fills the hand up to the player's hand cap; the game's own limit is
                // shown as advisory text in the DRAW popup.
                return DrawControllerCardsUpToComfortCap(player, capHand);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(player.ControllerDeckId, player.HandContainerId);
            ControllerInputHandDrawResult result = trapFloorTurnService.DrawForCurrentPlayer(
                matchState,
                CreateCommandContext(trapFloorTurnState.ActivePlayerId));
            ApplyLayout(player.ControllerDeckId);
            ApplyLayout(player.HandContainerId);
            presentationTransitions.AnimateCardsFromCurrentResults(
                transitionStarts,
                result.Succeeded ? handReflowDuration : returnDuration,
                0.035f);
            ShowMessage(result.Succeeded
                ? result.Changed
                    ? $"Drew {result.DrawnCount} Controller Card{(result.DrawnCount == 1 ? string.Empty : "s")}."
                    : "Controller Hand is already at its configured limit or the Deck is empty."
                : $"Controller draw rejected: {result.Error}.");
            return result;
        }

        private DrawCardsResult DrawCards(ContainerId sourceDeckContainerId, int count)
        {
            EnsureInitialized();
            int requestedCount = count;
            // H1: a draw from a player's own deck fills that player's hand, whoever's turn it is.
            ContainerId targetHandId = DrawTargetHandFor(sourceDeckContainerId);
            count = ClampToHandComfortCap(targetHandId, count);
            if (count <= 0)
            {
                ShowMessage(handComfortSettings.ReachedMessage + ".");
                return DrawCardsResult.Failure(
                    CommandResultStatus.Rejected,
                    DrawCardsError.DestinationCapacityExceeded);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(sourceDeckContainerId, targetHandId);
            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(CreateCommandContext(), sourceDeckContainerId, targetHandId, count));
            TryGetDeckPresentation(sourceDeckContainerId, out DeckView sourceDeckView, out _);
            if (result.Succeeded)
            {
                sourceDeckView?.ApplyAcceptedLayout();
                ApplyLayout(targetHandId);
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    handReflowDuration,
                    0.035f);
                string whose = targetHandId == handContainerId ? "Hand" : $"{HandOwnerName(targetHandId)}'s hand";
                if (targetHandId != handContainerId
                    && trapFloorTurnState != null
                    && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                    && !trapFloorTurnState.IsCurrentFloorFailed
                    && (!TryGetTrapFloorPlayerSetup(trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition activeSetup)
                        || activeSetup.HandContainerId != targetHandId))
                {
                    whose += " (not their turn)";
                }

                ShowMessage(count < requestedCount
                    ? $"{handComfortSettings.ReachedMessage}: drew {count} of {requestedCount}."
                    : $"Drew {count} card{(count == 1 ? string.Empty : "s")} to {whose}.");
            }
            else
            {
                sourceDeckView?.ApplyAcceptedLayout();
                ApplyLayout(targetHandId);
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Draw rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
            return result;
        }

        public ReorderContainerResult MoveSelectedHandCardLeft()
        {
            return MoveSelectedCardInContainer(handContainerId, -1);
        }

        public ReorderContainerResult MoveSelectedHandCardRight()
        {
            return MoveSelectedCardInContainer(handContainerId, 1);
        }

        public ReorderContainerResult MoveSelectedStackCardDown()
        {
            return MoveSelectedCardInSelectedStack(-1);
        }

        public ReorderContainerResult MoveSelectedStackCardUp()
        {
            return MoveSelectedCardInSelectedStack(1);
        }

        public SplitStackResult SplitSelectedOrPrimaryStack()
        {
            EnsureInitialized();
            if (!TryResolveSplitSource(out ContainerState source, out StackRuntimeView sourceView))
            {
                ShowMessage("Split unavailable.");
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall);
            }

            return SplitStack(source, sourceView);
        }

        private SplitStackResult SplitStack(ContainerState source, StackRuntimeView sourceView)
        {
            EnsureInitialized();
            if (source == null
                || sourceView == null
                || source.Kind != ContainerKind.Stack
                || source.Count < 2
                || !matchState.Containers.TryGetValue(source.Id, out ContainerState authoritativeSource)
                || !ReferenceEquals(authoritativeSource, source)
                || !stackViewsByContainerId.TryGetValue(source.Id, out StackRuntimeView authoritativeView)
                || !ReferenceEquals(authoritativeView, sourceView))
            {
                ShowMessage("Split unavailable.");
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall);
            }

            int firstMovedIndex = Math.Max(1, source.Count / 2);
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(source.Id);
            ContainerId newStackId = CreateDeterministicDynamicStackId(dynamicStackSequence++);
            TabletopPose sourcePose = sourceView.Placement.Pose;
            TabletopPose newPose = new TabletopPose(
                new TableCoordinate(sourcePose.Position.X + 1.4d, sourcePose.Position.Y + 0.9d),
                sourcePose.RotationDegrees,
                sourcePose.Layer,
                sourcePose.LocalOrder);

            SplitStackResult result = new SplitStackUseCase().Execute(
                matchState,
                new SplitStackCommand(
                    CreateCommandContext(),
                    source.Id,
                    newStackId,
                    new StackSplitSpecification(firstMovedIndex),
                    newPose));

            if (result.Succeeded)
            {
                StackRuntimeView newStackView = CreateStackRuntimeView(
                    $"Stack {stackViewsByContainerId.Count + 1}",
                    matchState.GetContainer(newStackId),
                    matchState.ContainerPlacements[newStackId],
                    true);
                stackViewsByContainerId.Add(newStackId, newStackView);
                primaryStackContainerId = newStackId;
                sourceView.View.ApplyAcceptedLayout();
                newStackView.View.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    settleDuration,
                    0.035f);
                presentationTransitions.Appear(newStackView.Root.transform, settleDuration);
                RebuildLayoutLookupAndRouter();
                ShowMessage("Stack split.");
            }
            else
            {
                sourceView.View.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Split rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
            return result;
        }

        private ReorderContainerResult MoveSelectedCardInSelectedStack(int delta)
        {
            EnsureInitialized();
            CardView selectedCard = SelectionState.SelectedView as CardView;
            if (selectedCard == null || selectedCard.CardState == null)
            {
                ShowMessage("Select a Stack card first.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            ContainerId containerId = selectedCard.CardState.BaseState.ContainerId;
            if (!stackViewsByContainerId.ContainsKey(containerId))
            {
                ShowMessage("Selected Card is not in a Stack.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ContainerMissing);
            }

            return MoveCardInContainer(selectedCard, containerId, delta);
        }

        private ReorderContainerResult MoveSelectedCardInContainer(ContainerId containerId, int delta)
        {
            EnsureInitialized();
            CardView selectedCard = SelectionState.SelectedView as CardView;
            if (selectedCard == null || selectedCard.CardState == null)
            {
                ShowMessage("Select a contained Card first.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            return MoveCardInContainer(selectedCard, containerId, delta);
        }

        private ReorderContainerResult MoveCardInContainer(
            CardView card,
            ContainerId containerId,
            int delta)
        {
            EnsureInitialized();
            if (card == null || card.CardState == null)
            {
                ShowMessage("Card is unavailable.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            if (card.CardState.BaseState.ContainerId != containerId)
            {
                ShowMessage("Card is not in that Container.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectContainerMismatch);
            }

            ContainerState container = matchState.GetContainer(containerId);
            int fromIndex = container.IndexOf(card.ObjectId);
            int toIndex = Mathf.Clamp(fromIndex + delta, 0, container.Count - 1);
            return ReorderCardInContainer(card, container, fromIndex, toIndex);
        }

        private ReorderContainerResult ReorderHandCardFromDrag(CardView card, int targetIndex)
        {
            EnsureInitialized();
            if (card == null
                || card.CardState == null
                || card.CardState.BaseState.ContainerId != handContainerId)
            {
                ShowMessage("Hand Card is unavailable.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectContainerMismatch);
            }

            ContainerState hand = matchState.GetContainer(handContainerId);
            int fromIndex = hand.IndexOf(card.ObjectId);
            int toIndex = Mathf.Clamp(targetIndex, 0, hand.Count - 1);
            return ReorderCardInContainer(card, hand, fromIndex, toIndex);
        }

        private ReorderContainerResult ReorderCardInContainer(
            CardView card,
            ContainerState container,
            int fromIndex,
            int toIndex)
        {
            ContainerId containerId = container.Id;
            if (fromIndex < 0)
            {
                ShowMessage("Card is not in that Container.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(containerId);
            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                matchState,
                new ReorderContainerCommand(
                    CreateCommandContext(),
                    containerId,
                    card.ObjectId,
                    fromIndex,
                    toIndex));

            if (result.Succeeded)
            {
                ApplyLayout(containerId);
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    containerId == handContainerId ? handReflowDuration : settleDuration);
                ShowMessage("Card reordered.");
            }
            else
            {
                ApplyLayout(containerId);
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Reorder rejected: {result.Error}.");
            }

            return result;
        }

        private MergeStacksResult MergeStacks(ContainerId sourceId, ContainerId destinationId)
        {
            EnsureInitialized();
            if (!stackViewsByContainerId.ContainsKey(sourceId)
                || !stackViewsByContainerId.ContainsKey(destinationId)
                || !matchState.Containers.ContainsKey(sourceId)
                || !matchState.Containers.ContainsKey(destinationId))
            {
                ShowMessage("Merge unavailable.");
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.SourceStackMissing);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(sourceId, destinationId);
            MergeStacksResult result = new MergeStacksUseCase().Execute(
                matchState,
                new MergeStacksCommand(CreateCommandContext(), sourceId, destinationId));
            if (result.Succeeded)
            {
                StackRuntimeView destinationView = stackViewsByContainerId[destinationId];
                RemoveStackRuntimeView(sourceId);
                destinationView.View.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    settleDuration,
                    0.04f);
                presentationTransitions.Pulse(destinationView.Root.transform, shuffleCompression, feedbackDuration);
                primaryStackContainerId = destinationId;
                ShowMessage("Stacks merged.");
            }
            else
            {
                stackViewsByContainerId[sourceId].View.ApplyAcceptedLayout();
                stackViewsByContainerId[destinationId].View.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Merge rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
            return result;
        }

        private bool TryResolveSplitSource(out ContainerState source, out StackRuntimeView sourceView)
        {
            source = null;
            sourceView = null;
            CardView selectedCard = SelectionState.SelectedView as CardView;
            if (selectedCard != null
                && selectedCard.CardState != null
                && stackViewsByContainerId.TryGetValue(selectedCard.CardState.BaseState.ContainerId, out sourceView)
                && matchState.Containers.TryGetValue(selectedCard.CardState.BaseState.ContainerId, out source)
                && source.Count >= 2)
            {
                return true;
            }

            if (!primaryStackContainerId.IsEmpty
                && stackViewsByContainerId.TryGetValue(primaryStackContainerId, out sourceView)
                && matchState.Containers.TryGetValue(primaryStackContainerId, out source)
                && source.Count >= 2)
            {
                return true;
            }

            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in stackViewsByContainerId)
            {
                if (matchState.Containers.TryGetValue(pair.Key, out source) && source.Count >= 2)
                {
                    sourceView = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private void RemoveStackRuntimeView(ContainerId containerId)
        {
            if (!stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackRuntimeView))
            {
                return;
            }

            SuspendInteractionDependenciesForRebuild();
            ReleaseStackView(containerId, stackRuntimeView);
            ResumeInteractionDependenciesAfterRebuild();
        }

        private void ApplyLayout(ContainerId containerId)
        {
            if (containerId.IsEmpty)
            {
                return;
            }

            if (containerId == handContainerId && handView != null)
            {
                handView.ApplyAcceptedLayout();
            }
            else if (stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackRuntimeView))
            {
                stackRuntimeView.View.ApplyAcceptedLayout();
            }
            else
            {
                for (int i = 0; i < hiddenHandViews.Count; i++)
                {
                    if (hiddenHandViews[i] != null && hiddenHandViews[i].ContainerId == containerId)
                    {
                        hiddenHandViews[i].ApplyAcceptedLayout();
                    }
                }

                for (int i = 0; i < controllerDeckViews.Count; i++)
                {
                    if (controllerDeckViews[i].ContainerId == containerId)
                    {
                        controllerDeckViews[i].ApplyAcceptedLayout();
                    }
                }

                if (TryGetRuntimeDiscardPile(containerId, out RuntimeDiscardPileInstance discardPile))
                {
                    discardPile.View.ApplyAcceptedLayout();
                }

                for (int i = 0; i < consoleSlotViews.Count; i++)
                {
                    if (consoleSlotViews[i].ContainerId == containerId)
                    {
                        consoleSlotViews[i].ApplyAcceptedLayout();
                    }
                }
            }

            RefreshCardContentVisibility();
        }

        private bool TryGetDeckPresentation(
            ContainerId containerId,
            out DeckView resolvedView,
            out FixedContainerVisual resolvedVisual)
        {
            for (int i = 0; i < runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance instance = runtimeDeckInstances[i];
                if (instance.ContainerId == containerId
                    && instance.View != null
                    && instance.View.IsBound
                    && instance.Visual != null)
                {
                    resolvedView = instance.View;
                    resolvedVisual = instance.Visual;
                    return true;
                }
            }

            resolvedView = null;
            resolvedVisual = null;
            return false;
        }

        // A transfer into a pile with an arrival face (a Discard Pile) changes the Card's face in the same
        // command; show the accepted face. Runs once per accepted transfer, not per frame.
        private void RefreshAcceptedCardFaces()
        {
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView view = cardViews[i];
                if (view != null
                    && view.IsBound
                    && view.CardState != null
                    && view.IsFacePresentationConfigured
                    && view.DisplayedFace != view.CardState.Face
                    && !IsInAnotherPlayersHand(view.CardState.BaseState.Id))
                {
                    view.ApplyAcceptedFacePresentation();
                }
            }
        }

        private void RefreshCardContentVisibility()
        {
            if (matchState == null)
            {
                return;
            }

            for (int i = 0; i < cardVisualReferences.Count; i++)
            {
                CardVisualReferences visualReferences = cardVisualReferences[i];
                if (visualReferences == null)
                {
                    continue;
                }

                CardView boundCardView = visualReferences.CardView;
                if (boundCardView == null
                    || !boundCardView.IsBound
                    || boundCardView.CardState == null)
                {
                    continue;
                }

                visualReferences.SetCardContentVisible(
                    ShouldShowCardContent(boundCardView.CardState));
            }
        }

        private bool ShouldShowCardContent(CardInstanceState card)
        {
            ContainerId containerId = card.BaseState.ContainerId;
            if (!containerId.IsEmpty
                && matchState.Containers.TryGetValue(containerId, out ContainerState hiddenHand)
                && IsHiddenHandContainer(hiddenHand))
            {
                return false;
            }

            if (containerId.IsEmpty
                || !matchState.Containers.TryGetValue(containerId, out ContainerState container)
                || !ShowsOnlyTopCardContent(container.Kind))
            {
                return true;
            }

            return container.Count > 0
                && container.ObjectIds[container.Count - 1] == card.BaseState.Id;
        }

        private static bool ShowsOnlyTopCardContent(ContainerKind kind)
        {
            return kind == ContainerKind.Deck
                || kind == ContainerKind.Stack
                || kind == ContainerKind.DiscardPile;
        }

        private IReadOnlyDictionary<Transform, TabletopTransformSnapshot> CaptureContainerCardTransforms(
            params ContainerId[] containerIds)
        {
            Dictionary<Transform, TabletopTransformSnapshot> starts =
                new Dictionary<Transform, TabletopTransformSnapshot>();
            for (int cardIndex = 0; cardIndex < cardViews.Count; cardIndex++)
            {
                CardView candidate = cardViews[cardIndex];
                if (candidate == null || candidate.CardState == null)
                {
                    continue;
                }

                ContainerId candidateContainerId = candidate.CardState.BaseState.ContainerId;
                for (int containerIndex = 0; containerIndex < containerIds.Length; containerIndex++)
                {
                    if (candidateContainerId != containerIds[containerIndex])
                    {
                        continue;
                    }

                    starts[candidate.transform] =
                        presentationTransitions.StopAndCapture(candidate.transform);
                    break;
                }
            }

            return starts;
        }
    }
}
