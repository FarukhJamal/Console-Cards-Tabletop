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
    /// <summary>
    /// Carrying a whole stack (owner, 2026-10-18): press and hold a card in a stack without moving and the whole
    /// stack lifts and follows the pointer (a quick drag still takes only the top card). Over another stack, that
    /// stack's outline lights green; let go there and the carried cards are merged onto it. Let go anywhere else and
    /// the stack is put down there. Escape puts it back. Replaces the old "Merge Into…" menu entry.
    /// The carry moves only the existing transforms (no allocations per frame); the merge or move is one command,
    /// committed on the next frame so the table's interaction graph is never rebuilt mid-gesture.
    /// </summary>
    public sealed partial class TabletopComposition : ITabletopCarryHandler
    {
        // POL-0: from Resources/FeelSettings.
        private float StackCarryLift => Feel.StackCarryLift;          // how high the carried stack rides (world units)
        private float StackCarryMergeReach => Feel.StackMergeReach;   // table units between stack centres that count as "on it"

        private readonly List<Transform> carriedCards = new List<Transform>(64);
        private readonly List<Vector3> carriedCardStarts = new List<Vector3>(64);
        private ContainerId carriedStackId;
        private Transform carriedRoot;
        private Vector3 carriedRootStart;
        private Vector3 carryGrabWorld;
        private Vector3 carryHorizontalOffset;
        // POL-4: what is shown follows the pointer smoothly, lifts with an ease and leans toward the merge target.
        private Vector3 carryShownOffset;
        private float carryStartedAt;
        private ContainerId carryTargetId;
        private bool carryDropPending;
        private ContainerId carryDropTargetId;
        private TabletopPose carryDropPose;
        // Owner, 2026-11-03 (doc 58, "as in TTS, anything on anything makes a deck"): a pile carried onto a single
        // loose card takes that card in (the card goes to the bottom, the pile lies where the card lay).
        private CardView carryTargetCard;
        private CardView carryDropTargetCard;
        private readonly List<TabletopObjectId> carryMoveIds = new List<TabletopObjectId>(64);

        internal bool IsCarryingStack => !carriedStackId.IsEmpty;

        bool ITabletopCarryHandler.CanCarry(CardView pressedCard)
        {
            return CanCarryStackOf(pressedCard, out _, out _);
        }

        bool ITabletopCarryHandler.TryBeginCarry(CardView pressedCard, Vector2 screenPosition)
        {
            if (!CanCarryStackOf(pressedCard, out ContainerId stackId, out Transform pileRoot)
                || pointerProjector == null
                || !pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate grab))
            {
                return false;
            }

            ContextMenus.CloseContextMenu();
            carriedStackId = stackId;
            carriedRoot = pileRoot;
            carriedRootStart = carriedRoot != null ? carriedRoot.position : Vector3.zero;
            carriedCards.Clear();
            carriedCardStarts.Clear();
            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView card = Registry.cardViews[i];
                if (card == null || card.CardState == null || card.CardState.BaseState.ContainerId != stackId) continue;
                presentationTransitions.Stop(card.transform, true);
                carriedCards.Add(card.transform);
                carriedCardStarts.Add(card.transform.position);
            }

            carryGrabWorld = coordinateConverter.ToWorldPosition(grab);
            carryHorizontalOffset = Vector3.zero;
            carryShownOffset = Vector3.zero;
            carryStartedAt = Time.unscaledTime;
            carryTargetId = ContainerId.Empty;
            ApplyCarryOffset();
            PlayCardSound(false);
            return true;
        }

        void ITabletopCarryHandler.UpdateCarry(Vector2 screenPosition)
        {
            if (carriedStackId.IsEmpty) return;
            if (matchState == null || !matchState.Containers.ContainsKey(carriedStackId))
            {
                ((ITabletopCarryHandler)this).CancelCarry();
                return;
            }

            if (pointerProjector == null || !pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate point))
            {
                return;
            }

            Vector3 offset = coordinateConverter.ToWorldPosition(point) - carryGrabWorld;
            offset.y = 0f;
            carryHorizontalOffset = offset;
            ContainerId pile = FindCarryMergeTarget();
            SetCarryTarget(pile, pile.IsEmpty ? FindCarryTargetCard() : null); // POL-4: the shown stack follows in UpdateStackCarryMotion
        }

        void ITabletopCarryHandler.ReleaseCarry(Vector2 screenPosition)
        {
            if (carriedStackId.IsEmpty) return;
            if (matchState == null || !matchState.TryGetContainerPlacement(carriedStackId, out ContainerPlacementState placement))
            {
                ((ITabletopCarryHandler)this).CancelCarry();
                return;
            }

            // Where the stack's centre was carried to, keeping its turn.
            TabletopPose start = placement.Pose;
            Vector3 world = coordinateConverter.ToWorldPosition(start) + carryHorizontalOffset;
            carryDropPose = new TabletopPose(
                coordinateConverter.ToTableCoordinate(world),
                start.RotationDegrees,
                start.Layer,
                start.LocalOrder);
            carryDropTargetId = carryTargetId;
            carryDropTargetCard = carryTargetCard;
            carryDropPending = true;
            HideCarryTarget();
        }

        void ITabletopCarryHandler.CancelCarry()
        {
            if (carriedStackId.IsEmpty) return;
            ContainerId stackId = carriedStackId;
            HideCarryTarget();
            ClearCarryState();
            if (matchState != null && matchState.Containers.ContainsKey(stackId)) ViewSync.SyncContainer(stackId);
            Physics.SyncTransforms();
        }

        // Called from Update: the carried stack is merged or put down once the gesture has fully ended.
        private void CompletePendingStackCarry()
        {
            if (!carryDropPending) return;
            if (interactionRouter != null && interactionRouter.HasActiveInteraction) return;
            carryDropPending = false;
            ContainerId stackId = carriedStackId;
            ContainerId targetId = carryDropTargetId;
            CardView targetCard = carryDropTargetCard;
            carryDropTargetCard = null;
            TabletopPose pose = carryDropPose;
            ClearCarryState();
            if (matchState == null || !matchState.Containers.ContainsKey(stackId)) return;

            if (!targetId.IsEmpty && matchState.Containers.ContainsKey(targetId))
            {
                bool bothStacks = matchState.Containers[stackId].Kind == ContainerKind.Stack
                    && matchState.Containers[targetId].Kind == ContainerKind.Stack;
                // MergeStacks animates the cards from where they were carried onto the stack below; any other pile
                // (a deck, a discard pile) takes the carried cards one by one, bottom first, so their order stays.
                bool merged = bothStacks ? MergeStacks(stackId, targetId).Succeeded : PourPileInto(stackId, targetId);
                if (merged) ScheduleLandingFeedback(targetId, true);
                Physics.SyncTransforms();
                return;
            }

            if (targetCard != null && TakeCardUnderPile(stackId, targetCard, pose))
            {
                ScheduleLandingFeedback(stackId, true);
                Physics.SyncTransforms();
                return;
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts = CaptureContainerCardTransforms(stackId);
            bool moved = CommitContainerMove(stackId, pose);
            if (!moved) ViewSync.SyncContainer(stackId);
            presentationTransitions.AnimateCardsFromCurrentResults(starts, moved ? Feel.DropSettleSeconds : returnDuration);
            if (moved) ScheduleLandingFeedback(stackId, false);
            Physics.SyncTransforms();
        }

        // Owner, 2026-11-03 ("like Tabletop Simulator: hold on the deck and it all moves together"): decks and discard
        // piles are carried whole by a hold too; only stacks merge, a deck or discard pile is put down where it is let go.
        private bool CanCarryStackOf(CardView pressedCard, out ContainerId stackId, out Transform pileRoot)
        {
            stackId = ContainerId.Empty;
            pileRoot = null;
            if (!IsInitialized || matchState == null || carryDropPending || !carriedStackId.IsEmpty
                || pressedCard == null || pressedCard.CardState == null)
            {
                return false;
            }

            stackId = pressedCard.CardState.BaseState.ContainerId;
            if (!matchState.Containers.TryGetValue(stackId, out ContainerState pile)
                || pile.Count == 0
                || MoveContainerUseCase.IsMoveBlockedByOwner(matchState, pile, localPlayerId))
            {
                return false;
            }

            switch (pile.Kind)
            {
                case ContainerKind.Stack:
                    if (!Registry.stackViewsByContainerId.TryGetValue(stackId, out StackRuntimeView stackView) || stackView == null)
                    {
                        return false;
                    }

                    pileRoot = stackView.Root != null ? stackView.Root.transform : null;
                    return true;
                case ContainerKind.Deck:
                    for (int i = 0; i < Registry.runtimeDeckInstances.Count; i++)
                    {
                        RuntimeDeckInstance deck = Registry.runtimeDeckInstances[i];
                        if (deck.ContainerId != stackId || deck.Root == null) continue;
                        pileRoot = deck.Root.transform;
                        return true;
                    }

                    return false;
                case ContainerKind.DiscardPile:
                    if (!Registry.TryGetRuntimeDiscardPile(stackId, out RuntimeDiscardPileInstance discard)) return false;
                    pileRoot = discard.Root != null ? discard.Root.transform : null;
                    return true;
                default:
                    return false;
            }
        }

        // POL-4, every frame while a stack is carried: it follows the pointer smoothly and, over a stack it will merge
        // onto, is pulled part of the way toward it.
        private void UpdateStackCarryMotion()
        {
            if (carriedStackId.IsEmpty || carryDropPending) return;
            Vector3 desired = carryHorizontalOffset;
            float pull = Feel.StackMagnetPull;
            if (pull > 0f
                && !carryTargetId.IsEmpty
                && matchState != null
                && matchState.TryGetContainerPlacement(carryTargetId, out ContainerPlacementState target)
                && matchState.TryGetContainerPlacement(carriedStackId, out ContainerPlacementState carried))
            {
                Vector3 toTarget = coordinateConverter.ToWorldPosition(target.Pose)
                    - (coordinateConverter.ToWorldPosition(carried.Pose) + carryHorizontalOffset);
                toTarget.y = 0f;
                desired += toTarget * pull;
            }

            float follow = 1f - Mathf.Exp(-Feel.StackFollow * Mathf.Max(0f, Time.unscaledDeltaTime));
            carryShownOffset = Vector3.Lerp(carryShownOffset, desired, follow);
            ApplyCarryOffset();
        }

        private void ApplyCarryOffset()
        {
            float liftSeconds = Feel.StackLiftSeconds;
            float lift = liftSeconds <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - carryStartedAt) / liftSeconds);
            lift = 1f - ((1f - lift) * (1f - lift)); // fast up, eased at the top
            Vector3 offset = carryShownOffset + (Vector3.up * (StackCarryLift * lift));
            if (carriedRoot != null) carriedRoot.position = carriedRootStart + offset;
            for (int i = 0; i < carriedCards.Count; i++)
            {
                if (carriedCards[i] != null) carriedCards[i].position = carriedCardStarts[i] + offset;
            }
        }

        // The nearest other pile (stack, deck or discard pile) whose centre is within reach of the carried pile's
        // centre (doc 58: every pile takes another, as in TTS).
        private ContainerId FindCarryMergeTarget()
        {
            if (!matchState.TryGetContainerPlacement(carriedStackId, out ContainerPlacementState placement)
                || !matchState.Containers.ContainsKey(carriedStackId))
            {
                return ContainerId.Empty;
            }

            Vector3 carriedCentre = coordinateConverter.ToWorldPosition(placement.Pose) + carryHorizontalOffset;
            TableCoordinate centre = coordinateConverter.ToTableCoordinate(carriedCentre);
            ContainerId best = ContainerId.Empty;
            double bestDistance = StackCarryMergeReach * StackCarryMergeReach;
            foreach (KeyValuePair<ContainerId, ContainerState> pair in matchState.Containers)
            {
                ContainerKind kind = pair.Value.Kind;
                if (pair.Key == carriedStackId
                    || (kind != ContainerKind.Stack && kind != ContainerKind.Deck && kind != ContainerKind.DiscardPile)
                    || !IsSelectablePile(pair.Key)
                    || !matchState.TryGetContainerPlacement(pair.Key, out ContainerPlacementState other))
                {
                    continue;
                }

                double dx = other.Pose.Position.X - centre.X;
                double dy = other.Pose.Position.Y - centre.Y;
                double distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pair.Key;
                }
            }

            return best;
        }

        // The single loose card under the carried pile's centre (not one of the pile's own cards).
        private CardView FindCarryTargetCard()
        {
            if (!matchState.TryGetContainerPlacement(carriedStackId, out ContainerPlacementState placement)) return null;
            Vector3 carriedCentre = coordinateConverter.ToWorldPosition(placement.Pose) + carryHorizontalOffset;
            TableCoordinate centre = coordinateConverter.ToTableCoordinate(carriedCentre);
            CardView best = null;
            double bestDistance = StackCarryMergeReach * StackCarryMergeReach;
            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView card = Registry.cardViews[i];
                if (!IsSelectableLoosePiece(card)) continue;
                TableCoordinate at = coordinateConverter.ToTableCoordinate(PieceRestPosition(card));
                double dx = at.X - centre.X;
                double dy = at.Y - centre.Y;
                double distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = card;
                }
            }

            return best;
        }

        // A deck or discard pile under the carried pile takes its cards, bottom first (one Undo step).
        private bool PourPileInto(ContainerId sourceId, ContainerId targetId)
        {
            if (transferCoordinator == null || !matchState.Containers.TryGetValue(sourceId, out ContainerState source)) return false;
            carryMoveIds.Clear();
            for (int i = 0; i < source.Count; i++) carryMoveIds.Add(source.GetObjectAt(i));
            int moved = 0;
            SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < carryMoveIds.Count; i++)
                {
                    if (Registry.TryGetCardView(carryMoveIds[i], out CardView card)
                        && transferCoordinator.Transfer(card, CardDropTarget.ForContainer(targetId)).Succeeded)
                    {
                        moved++;
                    }
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            carryMoveIds.Clear();
            ViewSync.SyncContainer(targetId);
            if (matchState.Containers.ContainsKey(sourceId)) ViewSync.SyncContainer(sourceId);
            return moved > 0;
        }

        // A pile let go over a single card: it goes where the card lay and the card joins it at the bottom.
        private bool TakeCardUnderPile(ContainerId pileId, CardView card, TabletopPose carriedPose)
        {
            if (transferCoordinator == null || card == null || !card.IsBound || card.BoundState == null) return false;
            TabletopPose cardPose = RestPose(card);
            TabletopPose pose = new TabletopPose(cardPose.Position, carriedPose.RotationDegrees, carriedPose.Layer, carriedPose.LocalOrder);
            bool joined = false;
            SessionService.BeginUndoBatch();
            try
            {
                IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts = CaptureContainerCardTransforms(pileId);
                if (!CommitContainerMove(pileId, pose)) ViewSync.SyncContainer(pileId);
                presentationTransitions.AnimateCardsFromCurrentResults(starts, Feel.DropSettleSeconds);
                if (transferCoordinator.Transfer(card, CardDropTarget.ForContainer(pileId)).Succeeded
                    && matchState.Containers.TryGetValue(pileId, out ContainerState pile))
                {
                    joined = true;
                    int from = pile.IndexOf(card.ObjectId);
                    if (from > 0) ReorderCardInContainer(card, pile, from, 0);
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            if (joined) ShowMessage("The card joined the deck.");
            return joined;
        }

        // The pile or card the carried one would join shows the green outline.
        private void SetCarryTarget(ContainerId targetId, CardView targetCard)
        {
            if (targetId == carryTargetId && ReferenceEquals(targetCard, carryTargetCard)) return;
            carryTargetId = targetId;
            carryTargetCard = targetCard;
            Vector3 position;
            float yaw;
            if (!targetId.IsEmpty && TryGetPileWorldPose(targetId, out position, out yaw))
            {
                if (Registry.stackViewsByContainerId.TryGetValue(targetId, out StackRuntimeView targetStack) && targetStack.Root != null)
                {
                    position = targetStack.Root.transform.position;
                }
            }
            else if (targetCard != null)
            {
                position = PieceRestPosition(targetCard);
                yaw = targetCard.transform.eulerAngles.y;
            }
            else
            {
                HideCarryTarget();
                return;
            }

            EnsureStackHoverHint();
            stackHoverHint.transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, yaw, 0f));
            stackHoverHint.SetActive(true);
            stackHoverHintVisual?.ShowValidTarget();
        }

        private void HideCarryTarget()
        {
            carryTargetId = ContainerId.Empty;
            carryTargetCard = null;
            if (stackHoverTarget == null && stackHoverHint != null) stackHoverHint.SetActive(false);
        }

        private void ClearCarryState()
        {
            // The pile's own frame goes back to where it lay; the table's view sync then sets it where the pile now is.
            if (carriedRoot != null) carriedRoot.position = carriedRootStart;
            carriedStackId = ContainerId.Empty;
            carriedRoot = null;
            carriedCards.Clear();
            carriedCardStarts.Clear();
            carryHorizontalOffset = Vector3.zero;
            carryShownOffset = Vector3.zero;
            carryTargetId = ContainerId.Empty;
            carryTargetCard = null;
        }
    }
}
