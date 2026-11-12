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
            CartridgeSeat player = ActiveCartridge.GetAssistedSeat();
            if (matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState capHand))
            {
                // Draw To Hand Limit fills the hand up to the player's hand cap; the game's own limit is
                // shown as advisory text in the DRAW popup.
                return ContextMenus.DrawControllerCardsUpToComfortCap(player, capHand);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(player.ControllerDeckId, player.HandContainerId);
            ControllerInputHandDrawResult result = ActiveCartridge.DrawControllerHandForTurn(
                CreateCommandContext(ActiveCartridge.TurnPlayerId));
            ApplyLayout(player.ControllerDeckId);
            ApplyLayout(player.HandContainerId);
            if (result.Succeeded) AnimateDrawnCards(transitionStarts); // SMOOTH-1
            else presentationTransitions.AnimateCardsFromCurrentResults(transitionStarts, returnDuration);
            ShowMessage(result.Succeeded
                ? result.Changed
                    ? $"Drew {result.DrawnCount} Controller Card{(result.DrawnCount == 1 ? string.Empty : "s")}."
                    : "Controller Hand is already at its configured limit or the Deck is empty."
                : result.Error == ControllerInputHandDrawError.None
                ? "Only the computer that plays this seat can draw for it." // B08: run on another computer
                : $"Could not draw ({result.Error}).");
            return result;
        }

        // B08 (playtest: "draw requests are sometimes rejected"): why a draw was refused, in plain words. A draw for a
        // seat played on another computer is not run here and comes back with no error.
        internal static string DrawErrorText(DrawCardsError error)
        {
            switch (error)
            {
                case DrawCardsError.None: return "only the computer that plays this seat can draw for it.";
                case DrawCardsError.InsufficientCards: return "the deck does not have that many cards.";
                case DrawCardsError.DestinationCapacityExceeded: return "the hand is full.";
                case DrawCardsError.ObjectUserLocked: return "a card in the deck is locked.";
                case DrawCardsError.SourceContainerNotDeck: return "that pile is not a deck.";
                case DrawCardsError.RevisionConflict: return "the table changed at the same moment. Try again.";
                case DrawCardsError.InvalidCount: return "choose at least one card.";
                default: return $"the table refused it ({error}).";
            }
        }

        // P8A: the Ability deck in a room that plays Full, on the host (its own game) and on a private guest (the host's
        // public table roles): the same answer on every computer, so every player's menus behave the same.
        // P8A batch 2 (R1, owner 2026-11-12): Draw 3 and buying are in every assist level; only Full adds the strict
        // rules (no draw into the hand) and the automatic clean-up.
        internal bool IsAbilityDeck(ContainerId deckId)
        {
            if (deckId.IsEmpty) return false;
            if (HasOnline && Online.IsPrivateGuest) return deckId == Online.GuestAbilityDeckId;
            return ActiveCartridge is TrapFloorCartridge && trapFloorTemplate != null && deckId == trapFloorTemplate.AbilityDeckId;
        }

        internal bool IsFullAbilityDeck(ContainerId deckId)
        {
            if (deckId.IsEmpty) return false;
            if (HasOnline && Online.IsPrivateGuest) return deckId == Online.GuestAbilityDeckId && Online.GuestRoomFull;
            return ActiveCartridge is TrapFloorCartridge && trapFloorTemplate != null
                && deckId == trapFloorTemplate.AbilityDeckId && PlayAssist.Level == PlayAssistLevel.Full;
        }

        private DrawCardsResult DrawCards(ContainerId sourceDeckContainerId, int count)
        {
            EnsureInitialized();
            // P8A (owner, 2026-11-08): in Full, an Ability reaches a hand only by being bought.
            if (IsFullAbilityDeck(sourceDeckContainerId))
            {
                ShowMessage("Abilities are bought, not drawn: right-click the Ability deck → Draw 3 Abilities.");
                return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.InvalidCount);
            }

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
                    && ActiveCartridge.IsPlayerTurnOpen
                    && (!TryGetGameSeat(ActiveCartridge.TurnPlayerId, out CartridgeSeat activeSeat)
                        || activeSeat.HandContainerId != targetHandId))
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
                ShowMessage($"Could not draw: {DrawErrorText(result.Error)}");
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

        // P8A (owner, 2026-11-10: "the slide is not showing on the host"): a slide this composition runs itself. On the
        // computer that runs the table's physics a loose card is a live body that puts itself straight back where it
        // lies; for the slide it is held still (kinematic, its physics script paused) and let go where it ends.
        internal void SlideCards(IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts, float seconds, float stagger)
        {
            if (starts == null || starts.Count == 0) return;
            int index = 0;
            foreach (KeyValuePair<Transform, TabletopTransformSnapshot> pair in starts)
            {
                if (pair.Key == null) continue;
                StartCoroutine(SlideWhenPlaced(pair.Key, pair.Value, seconds, index * stagger));
                index++;
            }
        }

        // Owner, 2026-11-12 ("the slide shows on the guest, not on the host"). Two causes found by reading the code (the
        // [Slide] line below confirms which one it was on the owner's run):
        // 1. The physics piece was looked up with `GetComponent(...) ?? GetComponentInParent(...)`. In the Editor (the
        //    owner's host) a missing component comes back as Unity's stand-in object, which `??` does not treat as null,
        //    so the parent was never searched and the card took the transform slide, which its interpolated physics body
        //    overwrites every frame. A build returns a real null, which is why the guest build slid.
        // 2. The card was skipped when its view had not reached its new place yet in that frame.
        // Each card now finds its physics piece with Unity's own null test and glides once it stands where the table put
        // it (tried at once, then for a few frames). One [Slide] line says what happened.
        private System.Collections.IEnumerator SlideWhenPlaced(Transform view, TabletopTransformSnapshot from, float seconds, float delay)
        {
            PhysicalLooseObject physical = FindPhysicalPiece(view);
            for (int frame = 0; frame < 6 && view != null; frame++)
            {
                bool moved = (view.position - from.Position).sqrMagnitude >= 1e-6f || Quaternion.Angle(view.rotation, from.Rotation) >= 0.5f;
                if (physical != null && physical.OwnsLooseTransform)
                {
                    physical.GlideFrom(from.Position, from.Rotation, seconds + delay, 0.02f);
                    if (physical.IsGliding)
                    {
                        Debug.Log($"[Slide] {view.name}: glides as a physics piece (frame {frame}).");
                        yield break;
                    }
                }
                else if (moved)
                {
                    Debug.Log($"[Slide] {view.name}: slides (no physics piece of its own here, frame {frame}).");
                    yield return SlideCard(view, from, seconds, delay);
                    yield break;
                }

                yield return null;
            }

            if (view != null)
                Debug.Log($"[Slide] {view.name}: no slide (physics piece {(physical != null ? "found" : "not found")}, it did not move from where it started).");
        }

        private static PhysicalLooseObject FindPhysicalPiece(Transform view)
        {
            PhysicalLooseObject physical = view.GetComponent<PhysicalLooseObject>();
            if (physical == null) physical = view.GetComponentInParent<PhysicalLooseObject>();
            if (physical == null) physical = view.GetComponentInChildren<PhysicalLooseObject>();
            return physical;
        }

        private System.Collections.IEnumerator SlideCard(Transform view, TabletopTransformSnapshot from, float seconds, float delay)
        {
            Vector3 endPosition = view.position;
            Quaternion endRotation = view.rotation;
            Rigidbody body = view.GetComponent<Rigidbody>();
            PhysicalLooseObject physical = FindPhysicalPiece(view);
            bool wasKinematic = body != null && body.isKinematic;
            bool physicalWasOn = physical != null && physical.enabled;
            if (body != null) body.isKinematic = true;
            if (physical != null) physical.enabled = false;
            view.SetPositionAndRotation(from.Position, from.Rotation);
            float lift = Mathf.Max(0.05f, 0.25f * WorldPerCardWidth());
            float started = Time.unscaledTime + delay;
            while (view != null)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - started) / Mathf.Max(0.01f, seconds));
                float eased = t * t * (3f - (2f * t));
                view.SetPositionAndRotation(
                    Vector3.Lerp(from.Position, endPosition, eased) + (Vector3.up * (Mathf.Sin(eased * Mathf.PI) * lift)),
                    Quaternion.Slerp(from.Rotation, endRotation, eased));
                if (t >= 1f) break;
                yield return null;
            }

            if (view == null) yield break;
            view.SetPositionAndRotation(endPosition, endRotation);
            if (body != null)
            {
                body.position = endPosition;
                body.rotation = endRotation;
                body.isKinematic = wasKinematic;
            }

            if (physical != null) physical.enabled = physicalWasOn;
        }

        // P8A: a change made elsewhere (run by change) that moved this card: its view slides from where it was.
        internal void RunWithCardSlide(TabletopObjectId cardId, Func<bool> change)
        {
            if (cardId.IsEmpty || matchState == null)
            {
                change();
                return;
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts = CaptureCardTransforms(new[] { cardId });
            if (!change() || matchState == null) return;
            bool loose = !matchState.Cards.TryGetValue(cardId, out CardInstanceState card) || card.BaseState.ContainerId.IsEmpty;
            if (!loose) ViewSync.SyncContainer(card.BaseState.ContainerId);
            PresentAcceptedObject(cardId);
            RefreshAcceptedCardFaces();
            if (loose) SlideCards(starts, 0.45f, 0f); // a loose body: held still for the slide
            else presentationTransitions.AnimateCardsFromCurrentResults(starts, 0.45f, 0f); // into a pile or a hand
        }

        // P8A: where these cards' views are now (loose cards have no container to capture by).
        internal IReadOnlyDictionary<Transform, TabletopTransformSnapshot> CaptureCardTransforms(IEnumerable<TabletopObjectId> cardIds)
        {
            Dictionary<Transform, TabletopTransformSnapshot> starts = new Dictionary<Transform, TabletopTransformSnapshot>();
            foreach (TabletopObjectId cardId in cardIds)
                if (Registry.TryGetCardView(cardId, out CardView view) && view != null)
                    starts[view.transform] = presentationTransitions.StopAndCapture(view.transform);
            return starts;
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
