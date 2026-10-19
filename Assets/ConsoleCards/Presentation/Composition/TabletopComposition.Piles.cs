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
        // Shuffles a Deck or a Stack (a stack works like a deck, owner 2026-10-18) with the quick riffle.
        public ShuffleDeckResult ShuffleDeck(ContainerId targetDeckContainerId) =>
            ShuffleDeck(targetDeckContainerId, ShuffleStyle.Riffle);

        // SHUF-1: shuffles in a style (Cut cuts once instead). The order always comes from the table's random source;
        // the style only decides the animation and the sound.
        public ShuffleDeckResult ShuffleDeck(ContainerId targetDeckContainerId, ShuffleStyle style)
        {
            EnsureInitialized();
            bool isDeck = Registry.TryGetDeckPresentation(
                targetDeckContainerId,
                out DeckView targetDeckView,
                out FixedContainerVisual targetDeckVisual);
            StackRuntimeView targetStack = null;
            if (!isDeck && !Registry.stackViewsByContainerId.TryGetValue(targetDeckContainerId, out targetStack))
            {
                ShowMessage("Shuffle rejected: Deck Presentation unavailable.");
                return ShuffleDeckResult.Failure(
                    CommandResultStatus.Rejected,
                    ShuffleDeckError.ContainerMissing);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(targetDeckContainerId);
            ShuffleDeckResult result = TableAuthority.Submit<ShuffleDeckCommand, ShuffleDeckResult>(
                style == ShuffleStyle.Cut
                    ? ShuffleDeckCommand.Cut(CreateCommandContext(), targetDeckContainerId)
                    : new ShuffleDeckCommand(CreateCommandContext(), targetDeckContainerId));
            if (result.Succeeded)
            {
                // R2: the deck or stack was laid out again by the table's view sync when the shuffle was accepted.
                // SMOOTH-1 / SHUF-1: the cards move from the old order into the new one in the chosen style.
                Transform pile = isDeck ? targetDeckVisual.transform : targetStack.Root.transform;
                if (style == ShuffleStyle.Riffle)
                {
                    AnimateRiffle(transitionStarts, pile);
                    PlayShuffleSound();
                }
                else
                {
                    AnimateShuffleStyle(style, transitionStarts, pile);
                    PlayShuffleStyleSound(style);
                }

                string what = isDeck ? "Deck" : "Stack";
                ShowMessage(style == ShuffleStyle.Cut
                    ? $"{what} cut."
                    : style == ShuffleStyle.Riffle ? $"{what} shuffled." : $"{what} shuffled ({ShuffleStyleName(style)}).");
            }
            else
            {
                if (isDeck) targetDeckView.ApplyAcceptedLayout();
                else ApplyLayout(targetDeckContainerId);
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
            TrapFloorPlayerSetupDefinition player = TrapFloorGame.GetAssistedTrapFloorPlayerSetup();
            if (matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState capHand))
            {
                // Draw To Hand Limit fills the hand up to the player's hand cap; the game's own limit is
                // shown as advisory text in the DRAW popup.
                return ContextMenus.DrawControllerCardsUpToComfortCap(player, capHand);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(player.ControllerDeckId, player.HandContainerId);
            ControllerInputHandDrawResult result = trapFloorTurnService.DrawForCurrentPlayer(
                matchState,
                CreateCommandContext(trapFloorTurnState.ActivePlayerId));
            ApplyLayout(player.ControllerDeckId);
            ApplyLayout(player.HandContainerId);
            if (result.Succeeded) AnimateDrawnCards(transitionStarts); // SMOOTH-1
            else presentationTransitions.AnimateCardsFromCurrentResults(transitionStarts, returnDuration);
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
            count = ContextMenus.ClampToHandComfortCap(targetHandId, count);
            if (count <= 0)
            {
                ShowMessage(handComfortSettings.ReachedMessage + ".");
                return DrawCardsResult.Failure(
                    CommandResultStatus.Rejected,
                    DrawCardsError.DestinationCapacityExceeded);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(sourceDeckContainerId, targetHandId);
            DrawCardsResult result = TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(
                new DrawCardsCommand(CreateCommandContext(), sourceDeckContainerId, targetHandId, count));
            Registry.TryGetDeckPresentation(sourceDeckContainerId, out DeckView sourceDeckView, out _);
            if (result.Succeeded)
            {
                // R2: the deck (or stack) and the hand were laid out again by the table's view sync.
                AnimateDrawnCards(transitionStarts); // SMOOTH-1: one after another, along an arc
                string whose = targetHandId == handContainerId ? "Hand" : $"{HandOwnerName(targetHandId)}'s hand";
                if (targetHandId != handContainerId
                    && trapFloorTurnState != null
                    && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                    && !trapFloorTurnState.IsCurrentFloorFailed
                    && (!TrapFloorGame.TryGetTrapFloorPlayerSetup(trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition activeSetup)
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
                if (sourceDeckView != null) sourceDeckView.ApplyAcceptedLayout();
                else ApplyLayout(sourceDeckContainerId);
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

        internal SplitStackResult SplitStack(ContainerState source, StackRuntimeView sourceView)
        {
            EnsureInitialized();
            if (source == null
                || sourceView == null
                || source.Kind != ContainerKind.Stack
                || source.Count < 2
                || !matchState.Containers.TryGetValue(source.Id, out ContainerState authoritativeSource)
                || !ReferenceEquals(authoritativeSource, source)
                || !Registry.stackViewsByContainerId.TryGetValue(source.Id, out StackRuntimeView authoritativeView)
                || !ReferenceEquals(authoritativeView, sourceView))
            {
                ShowMessage("Split unavailable.");
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall);
            }

            int firstMovedIndex = Math.Max(1, source.Count / 2);
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(source.Id);
            ContainerId newStackId = Spawner.CreateDeterministicDynamicStackId(dynamicStackSequence++);
            TabletopPose sourcePose = sourceView.Placement.Pose;
            TabletopPose newPose = new TabletopPose(
                new TableCoordinate(sourcePose.Position.X + 1.4d, sourcePose.Position.Y + 0.9d),
                sourcePose.RotationDegrees,
                sourcePose.Layer,
                sourcePose.LocalOrder);

            SplitStackResult result = TableAuthority.Submit<SplitStackCommand, SplitStackResult>(new SplitStackCommand(
                    CreateCommandContext(),
                    source.Id,
                    newStackId,
                    new StackSplitSpecification(firstMovedIndex),
                    newPose));

            if (result.Succeeded)
            {
                // R2: the table's view sync made the new stack's view and laid both stacks out.
                StackRuntimeView newStackView = Registry.stackViewsByContainerId[newStackId];
                primaryStackContainerId = newStackId;
                AnimatePileCards(transitionStarts, true); // SMOOTH-1: the top half glides off card by card
                presentationTransitions.Appear(newStackView.Root.transform, Feel.PileMoveSeconds);
                if (matchState.GetContainer(newStackId).Count == 1) WatchStackForCollapse(newStackId); // a single card goes back to being a card
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
            if (!Registry.stackViewsByContainerId.ContainsKey(containerId))
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
            ReorderContainerResult result = TableAuthority.Submit<ReorderContainerCommand, ReorderContainerResult>(
                new ReorderContainerCommand(
                    CreateCommandContext(),
                    containerId,
                    card.ObjectId,
                    fromIndex,
                    toIndex));

            if (result.Succeeded)
            {
                // R2: laid out again by the table's view sync.
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

        internal MergeStacksResult MergeStacks(ContainerId sourceId, ContainerId destinationId)
        {
            EnsureInitialized();
            if (!Registry.stackViewsByContainerId.ContainsKey(sourceId)
                || !Registry.stackViewsByContainerId.ContainsKey(destinationId)
                || !matchState.Containers.ContainsKey(sourceId)
                || !matchState.Containers.ContainsKey(destinationId))
            {
                ShowMessage("Merge unavailable.");
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.SourceStackMissing);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(sourceId, destinationId);
            MergeStacksResult result = TableAuthority.Submit<MergeStacksCommand, MergeStacksResult>(
                new MergeStacksCommand(CreateCommandContext(), sourceId, destinationId));
            if (result.Succeeded)
            {
                // R2: the table's view sync removed the merged stack's view and laid the destination out.
                StackRuntimeView destinationView = Registry.stackViewsByContainerId[destinationId];
                AnimatePileCards(transitionStarts, false); // SMOOTH-1: the bottom card lands first
                presentationTransitions.Pulse(destinationView.Root.transform, shuffleCompression, feedbackDuration);
                primaryStackContainerId = destinationId;
                ShowMessage("Stacks merged.");
            }
            else
            {
                Registry.stackViewsByContainerId[sourceId].View.ApplyAcceptedLayout();
                Registry.stackViewsByContainerId[destinationId].View.ApplyAcceptedLayout();
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
                && Registry.stackViewsByContainerId.TryGetValue(selectedCard.CardState.BaseState.ContainerId, out sourceView)
                && matchState.Containers.TryGetValue(selectedCard.CardState.BaseState.ContainerId, out source)
                && source.Count >= 2)
            {
                return true;
            }

            if (!primaryStackContainerId.IsEmpty
                && Registry.stackViewsByContainerId.TryGetValue(primaryStackContainerId, out sourceView)
                && matchState.Containers.TryGetValue(primaryStackContainerId, out source)
                && source.Count >= 2)
            {
                return true;
            }

            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in Registry.stackViewsByContainerId)
            {
                if (matchState.Containers.TryGetValue(pair.Key, out source) && source.Count >= 2)
                {
                    sourceView = pair.Value;
                    return true;
                }
            }

            return false;
        }

        internal void RemoveStackRuntimeView(ContainerId containerId)
        {
            if (!Registry.stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackRuntimeView))
            {
                return;
            }

            SuspendInteractionDependenciesForRebuild();
            ReleaseStackView(containerId, stackRuntimeView);
            ResumeInteractionDependenciesAfterRebuild();
        }

        internal void ApplyLayout(ContainerId containerId)
        {
            if (containerId.IsEmpty)
            {
                return;
            }

            if (containerId == handContainerId && handView != null)
            {
                handView.ApplyAcceptedLayout();
            }
            else if (Registry.stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackRuntimeView))
            {
                stackRuntimeView.View.ApplyAcceptedLayout();
            }
            else
            {
                for (int i = 0; i < Registry.hiddenHandViews.Count; i++)
                {
                    if (Registry.hiddenHandViews[i] != null && Registry.hiddenHandViews[i].ContainerId == containerId)
                    {
                        Registry.hiddenHandViews[i].ApplyAcceptedLayout();
                    }
                }

                for (int i = 0; i < Registry.controllerDeckViews.Count; i++)
                {
                    if (Registry.controllerDeckViews[i].ContainerId == containerId)
                    {
                        Registry.controllerDeckViews[i].ApplyAcceptedLayout();
                    }
                }

                if (Registry.TryGetRuntimeDiscardPile(containerId, out RuntimeDiscardPileInstance discardPile))
                {
                    discardPile.View.ApplyAcceptedLayout();
                }

                for (int i = 0; i < Registry.consoleSlotViews.Count; i++)
                {
                    if (Registry.consoleSlotViews[i].ContainerId == containerId)
                    {
                        Registry.consoleSlotViews[i].ApplyAcceptedLayout();
                    }
                }
            }

            RefreshCardContentVisibility();
        }

        // A transfer into a pile with an arrival face (a Discard Pile) changes the Card's face in the same
        // command; show the accepted face. Runs once per accepted transfer, not per frame.
        internal void RefreshAcceptedCardFaces()
        {
            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView view = Registry.cardViews[i];
                if (view != null
                    && view.IsBound
                    && view.CardState != null
                    && view.IsFacePresentationConfigured
                    && view.DisplayedFace != view.CardState.Face
                    && !ContextMenus.IsInAnotherPlayersHand(view.CardState.BaseState.Id))
                {
                    view.ApplyAcceptedFacePresentation();
                }
            }
        }

        internal void RefreshCardContentVisibility()
        {
            if (matchState == null)
            {
                return;
            }

            for (int i = 0; i < Registry.cardVisualReferences.Count; i++)
            {
                CardVisualReferences visualReferences = Registry.cardVisualReferences[i];
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

        internal bool ShouldShowCardContent(CardInstanceState card)
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

        // ---------- SMOOTH-1: card motion between piles ----------

        // World units per table unit (about one card width), so the feel numbers can be given in card widths.
        private float WorldPerCardWidth()
        {
            if (coordinateConverter == null) return 1f;
            return Mathf.Max(
                0.0001f,
                Vector3.Distance(
                    coordinateConverter.ToWorldPosition(new TableCoordinate(0d, 0d)),
                    coordinateConverter.ToWorldPosition(new TableCoordinate(1d, 0d))));
        }

        // Split, Split into, merge: the cards glide one after another along a small arc.
        internal void AnimatePileCards(IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts, bool topFirst)
        {
            FeelSettings feel = Feel;
            presentationTransitions.AnimateCardsStaggered(
                starts,
                feel.PileMoveSeconds,
                feel.PileMoveArc * WorldPerCardWidth(),
                feel.PileMoveStagger,
                feel.PileMoveMaxStagger,
                topFirst);
        }

        // Draw: the top card flies to the hand first.
        private void AnimateDrawnCards(IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts)
        {
            FeelSettings feel = Feel;
            presentationTransitions.AnimateCardsStaggered(
                starts,
                feel.DrawSeconds,
                feel.DrawArc * WorldPerCardWidth(),
                feel.PileMoveStagger,
                feel.PileMoveMaxStagger,
                true);
        }

        // Shuffle: the halves slide apart along the pile's width and drop back interleaved.
        private void AnimateRiffle(IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts, Transform pile)
        {
            FeelSettings feel = Feel;
            float card = WorldPerCardWidth();
            Vector3 side = pile != null ? pile.right : Vector3.right;
            side.y = 0f;
            presentationTransitions.AnimateRiffle(
                starts,
                side,
                feel.ShuffleSpread * card,
                feel.ShuffleLift * card,
                feel.ShuffleTilt,
                feel.ShuffleSplitSeconds,
                feel.ShuffleDropSeconds,
                feel.ShuffleStagger,
                feel.ShuffleMaxStagger);
        }

        internal IReadOnlyDictionary<Transform, TabletopTransformSnapshot> CaptureContainerCardTransforms(
            params ContainerId[] containerIds)
        {
            Dictionary<Transform, TabletopTransformSnapshot> starts =
                new Dictionary<Transform, TabletopTransformSnapshot>();
            for (int cardIndex = 0; cardIndex < Registry.cardViews.Count; cardIndex++)
            {
                CardView candidate = Registry.cardViews[cardIndex];
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
