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
    /// <summary>
    /// Stacking that feels like Tabletop Simulator (owner, 2026-10-18):
    /// <list type="bullet">
    /// <item>Card on card: while a card is carried over a loose card, a stack outline lights up under that card;
    /// let go and the two become a stack at once (no waiting for the card to land): the cards glide together,
    /// a soft double tap plays and the new pile flashes. One Undo step.</item>
    /// <item>A card landing on a stack or deck gives a light tap and a short flash on that pile.</item>
    /// <item>A stack left with one card turns back into a loose card; an emptied stack goes away. This happens
    /// when cards leave a stack, so a stack placed empty from the Toolbox stays until it has been used. Game piles
    /// (made by a game template) are never removed. The tidy-up joins the Undo step that caused it.</item>
    /// </list>
    /// Polled from Update without allocations; pile counts are only rechecked after an accepted table action.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float CardStackOverlap = 0.7f;
        private const float CardStackReleaseWindow = 1f; // seconds a let-go card may take to come out of its pile
        private const float PileFlashSeconds = 0.35f;
        private const float CardSoundMinimumGap = 0.05f;
        private const float CardSoundVolume = 0.5f;
        private const int PileLandingMaximumCards = 3; // bigger jumps are deals and merges, not a card landing
        private const string CardStackSoundPath = "CardSounds/CardStack";
        private const string CardTapSoundPath = "CardSounds/CardTap";

        private readonly Dictionary<ContainerId, int> watchedPileCounts = new Dictionary<ContainerId, int>();
        private MatchState watchedPileMatch;
        internal bool pileCountCheckPending;

        private TabletopObjectId watchedDraggedCardId;
        private CardView stackHoverTarget;
        private GameObject stackHoverHint;
        private FixedContainerVisual stackHoverHintVisual;
        private TabletopObjectId releasedCardId;
        private CardView releasedOntoCard;
        private float releasedCardAt;

        private ContainerFeedbackTarget flashingPile;
        private ContainerId flashingPileId;
        private float pileFlashEndsAt;
        private ContainerId landingPileId;
        private bool landingIsNewStack;
        private float landingFeedbackAt = -1f;

        private AudioSource cardSoundSource;
        private AudioClip cardStackClip;
        private AudioClip cardTapClip;
        private bool cardSoundsLoaded;
        private float lastCardSoundAt = -1f;

        // Called every frame from Update (no allocations).
        private void UpdateCardStackDrops()
        {
            PlayDueLandingFeedback();
            UpdatePileFlash();
            if (!IsInitialized || matchState == null || rebuildingFromUndo)
            {
                ClearStackHover();
                return;
            }

            if (TryGetDraggedCard(out CardView dragged))
            {
                if (dragged.IsBound) watchedDraggedCardId = dragged.BoundState.Id;
                releasedCardId = TabletopObjectId.Empty;
                releasedOntoCard = null;
                SetStackHover(TryFindCardUnder(dragged, false, out CardView under) ? under : null);
                return;
            }

            if (!watchedDraggedCardId.IsEmpty)
            {
                // Let go: it stacks only when it was let go over a lit card.
                if (stackHoverTarget != null)
                {
                    releasedCardId = watchedDraggedCardId;
                    releasedOntoCard = stackHoverTarget;
                    releasedCardAt = Time.unscaledTime;
                }

                watchedDraggedCardId = TabletopObjectId.Empty;
                ClearStackHover();
            }

            if (!releasedCardId.IsEmpty)
            {
                TryCompleteReleasedStack();
                return;
            }

            UpdateWatchedPiles();
        }

        private void TryCompleteReleasedStack()
        {
            if (Time.unscaledTime - releasedCardAt > CardStackReleaseWindow)
            {
                ClearReleasedStack(); // it went into a pile or the hand instead
                return;
            }

            // Making a stack rebuilds the interaction graph, which is not allowed mid-gesture.
            if (interactionRouter != null && interactionRouter.HasActiveInteraction) return;
            if (!Registry.TryGetCardView(releasedCardId, out CardView top))
            {
                ClearReleasedStack();
                return;
            }

            if (!top.IsBound || !top.BoundState.ContainerId.IsEmpty) return; // still leaving its pile
            if (top.PhysicalObject != null && top.PhysicalObject.IsHeld) return;

            CardView bottom = releasedOntoCard;
            ClearReleasedStack();
            if (IsStackableLooseCard(top) && IsStackableLooseCard(bottom)) MakeStackFromCards(bottom, top);
        }

        private void ClearReleasedStack()
        {
            releasedCardId = TabletopObjectId.Empty;
            releasedOntoCard = null;
        }

        private bool IsStackableLooseCard(CardView card)
        {
            if (card == null || !card.IsBound || card.BoundState == null || card.IsOverlayPresentation) return false;
            TabletopObjectState state = card.BoundState;
            return state.ContainerId.IsEmpty
                && !state.IsUserLocked
                && (trapFloorTemplate == null || !trapFloorTemplate.IsFloorCard(state.Id)); // floor tiles never stack
        }

        private static Vector3 RestPosition(CardView card)
        {
            return card.PhysicalObject != null && card.PhysicalObject.Body != null
                ? card.PhysicalObject.Body.position
                : card.transform.position;
        }

        // Where a loose card actually lies on the table and which way it faces. A physical card's accepted pose is
        // where it was first placed, not where physics has since moved it, so the body is read instead.
        private TabletopPose RestPose(CardView card)
        {
            Vector3 position = RestPosition(card);
            Quaternion rotation = card.PhysicalObject != null && card.PhysicalObject.Body != null
                ? card.PhysicalObject.Body.rotation
                : card.transform.rotation;
            Quaternion tableFrame = coordinateConverter.ToWorldRotation(TabletopPose.Default);
            Vector3 forward = Quaternion.Inverse(tableFrame) * (rotation * Vector3.forward);
            float yaw = new Vector2(forward.x, forward.z).sqrMagnitude > 0.0001f
                ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
                : card.BoundState.Pose.RotationDegrees;
            return new TabletopPose(coordinateConverter.ToTableCoordinate(position), Mathf.Repeat(yaw, 360f), 0, 0);
        }

        // The loose card whose top the given card's centre lies over (nearest centre wins). While carrying, the
        // carried card is above everything, so the height test is only used for a card at rest.
        private bool TryFindCardUnder(CardView top, bool requireAbove, out CardView bottom)
        {
            bottom = null;
            if (top == null || !top.IsBound) return false;
            Vector3 position = RestPosition(top);
            float best = float.MaxValue;
            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView candidate = Registry.cardViews[i];
                if (candidate == null || ReferenceEquals(candidate, top) || !IsStackableLooseCard(candidate)) continue;
                if (candidate.PhysicalObject != null && candidate.PhysicalObject.IsHeld) continue;
                Renderer renderer = candidate.GetComponentInChildren<Renderer>();
                if (renderer == null) continue;
                Bounds bounds = renderer.bounds;
                float dx = Mathf.Abs(position.x - bounds.center.x);
                float dz = Mathf.Abs(position.z - bounds.center.z);
                if (dx > bounds.extents.x * CardStackOverlap || dz > bounds.extents.z * CardStackOverlap) continue;
                if (requireAbove && position.y < bounds.center.y) continue;
                float distance = (dx * dx) + (dz * dz);
                if (distance < best)
                {
                    best = distance;
                    bottom = candidate;
                }
            }

            return bottom != null;
        }

        // Under the card a carried card would stack on, the stack outline lights up green (the same look as a pile
        // that will take the card), so the player sees it will stack. One hint object, made once and reused.
        private void SetStackHover(CardView target)
        {
            if (ReferenceEquals(target, stackHoverTarget)) return;
            ClearStackHover();
            if (target == null) return;
            stackHoverTarget = target;
            EnsureStackHoverHint();
            Vector3 position = RestPosition(target);
            Renderer renderer = target.GetComponentInChildren<Renderer>();
            float surface = renderer != null ? renderer.bounds.min.y : position.y;
            stackHoverHint.transform.SetPositionAndRotation(
                new Vector3(position.x, surface, position.z),
                Quaternion.Euler(0f, target.transform.eulerAngles.y, 0f));
            stackHoverHint.SetActive(true);
            stackHoverHintVisual?.ShowValidTarget();
        }

        // The green stack outline, made once and shared by card-on-card stacking and carrying a stack.
        private void EnsureStackHoverHint()
        {
            if (stackHoverHint != null) return;
            stackHoverHint = CreateToolboxPlacementPreview(TabletopComponentKind.Stack, 0);
            stackHoverHint.name = "Stack Drop Hint";
            stackHoverHintVisual = stackHoverHint.GetComponent<FixedContainerVisual>();
            if (stackHoverHintVisual != null)
            {
                stackHoverHintVisual.SetBasePlateHidden(true); // only the outline
                if (stackHoverHintVisual.Label != null) stackHoverHintVisual.Label.gameObject.SetActive(false);
            }
        }

        private void ClearStackHover()
        {
            if (stackHoverTarget == null) return;
            stackHoverTarget = null;
            if (stackHoverHint != null) stackHoverHint.SetActive(false);
        }

        // A new stack where the bottom card lies; the bottom card goes in first, then the dropped card on top.
        // The cards glide into place (the transfer's own animation), then a double tap and a flash.
        private void MakeStackFromCards(CardView bottom, CardView top)
        {
            TabletopPose stackPose = RestPose(bottom);
            ContainerId stackId = ContainerId.Empty;
            bool stacked = false;
            SessionService.BeginUndoBatch();
            try
            {
                CreateTabletopComponentResult created = TableAuthority.Submit<CreateTabletopComponentRequest, CreateTabletopComponentResult>(
                    new CreateTabletopComponentRequest(
                        CreateCommandContext(),
                        TabletopComponentKind.Stack,
                        stackPose));
                if (!created.Succeeded)
                {
                    ShowMessage($"Stack rejected: {created.Error}.");
                    return;
                }

                stackId = created.ContainerId;
                // R2: the stack got its view from the table's view sync (which also rebuilt the transfer coordinator).
                CardDropTarget target = CardDropTarget.ForContainer(stackId);
                stacked = transferCoordinator != null
                    && transferCoordinator.Transfer(bottom, target).Succeeded
                    && transferCoordinator.Transfer(top, target).Succeeded;
                if (!stacked && matchState.Containers.TryGetValue(stackId, out ContainerState stack) && stack.Count == 0)
                {
                    // R2: an accepted delete removes the view through the table's view sync.
                    TableAuthority.Submit<DeleteTabletopComponentRequest, DeleteTabletopComponentResult>(
                        new DeleteTabletopComponentRequest(
                            CreateCommandContext(),
                            TabletopComponentTarget.ForContainer(stackId)));
                }

                if (!stacked) ShowMessage("Could not stack those cards.");
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            if (!stacked) return;
            if (matchState.Containers.TryGetValue(stackId, out ContainerState made))
            {
                watchedPileCounts[stackId] = made.Count; // the landing tap below is this stack's only sound
            }

            ScheduleLandingFeedback(stackId, true);
        }

        // ---------- Pile counts: landing taps and tidying up small stacks ----------

        // A split that leaves single cards in new stacks marks them, so they turn back into loose cards too.
        private void WatchStackForCollapse(ContainerId stackId)
        {
            watchedPileCounts[stackId] = int.MaxValue;
            pileCountCheckPending = true;
        }

        private void UpdateWatchedPiles()
        {
            if (!ReferenceEquals(watchedPileMatch, matchState))
            {
                // New table, Undo, Redo or Reset: start counting again without reacting to the difference.
                watchedPileCounts.Clear();
                watchedPileMatch = matchState;
                pileCountCheckPending = true;
            }

            if (!pileCountCheckPending || undoBatchActive) return;
            if (interactionRouter != null && interactionRouter.HasActiveInteraction) return;
            pileCountCheckPending = false;

            ContainerId collapseId = ContainerId.Empty;
            ContainerId landedOn = ContainerId.Empty;
            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in Registry.stackViewsByContainerId)
            {
                if (!matchState.Containers.TryGetValue(pair.Key, out ContainerState pile)) continue;
                int count = pile.Count;
                bool seen = watchedPileCounts.TryGetValue(pair.Key, out int previous);
                watchedPileCounts[pair.Key] = count;
                if (!seen) continue;
                if (count < previous && count <= 1)
                {
                    if (collapseId.IsEmpty && pile.Kind == ContainerKind.Stack && !matchState.IsTemplateContainer(pair.Key))
                    {
                        collapseId = pair.Key;
                    }
                }
                else if (count > previous && count - previous <= PileLandingMaximumCards)
                {
                    landedOn = pair.Key;
                }
            }

            for (int i = 0; i < Registry.runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance deck = Registry.runtimeDeckInstances[i];
                if (deck.View == null || !deck.View.IsBound
                    || !matchState.Containers.TryGetValue(deck.ContainerId, out ContainerState pile)) continue;
                int count = pile.Count;
                bool seen = watchedPileCounts.TryGetValue(deck.ContainerId, out int previous);
                watchedPileCounts[deck.ContainerId] = count;
                if (seen && count > previous && count - previous <= PileLandingMaximumCards)
                {
                    landedOn = deck.ContainerId;
                }
            }

            if (!collapseId.IsEmpty)
            {
                CollapseStack(collapseId);
                pileCountCheckPending = true; // look again next frame for any other stack
                return;
            }

            if (!landedOn.IsEmpty) ScheduleLandingFeedback(landedOn, false);
        }

        // One card left: it goes back on the table where the stack lay and the stack is removed. None left: the
        // stack is removed. Folded into the Undo step that emptied it.
        private void CollapseStack(ContainerId stackId)
        {
            watchedPileCounts.Remove(stackId);
            if (!Registry.stackViewsByContainerId.ContainsKey(stackId)
                || !matchState.Containers.TryGetValue(stackId, out ContainerState pile)
                || pile.Count > 1)
            {
                return;
            }

            SessionService.BeginUndoAmend();
            try
            {
                if (pile.Count == 1)
                {
                    if (transferCoordinator == null
                        || !matchState.TryGetContainerPlacement(stackId, out ContainerPlacementState placement)
                        || !Registry.TryGetCardView(pile.GetObjectAt(0), out CardView last))
                    {
                        return;
                    }

                    TabletopPose pose = placement.Pose;
                    if (!transferCoordinator.Transfer(
                            last,
                            CardDropTarget.ForTabletop(new TabletopPose(pose.Position, pose.RotationDegrees, 0, 0))).Succeeded)
                    {
                        return;
                    }
                }

                // R2: an accepted delete removes the view through the table's view sync.
                TableAuthority.Submit<DeleteTabletopComponentRequest, DeleteTabletopComponentResult>(
                    new DeleteTabletopComponentRequest(
                        CreateCommandContext(),
                        TabletopComponentTarget.ForContainer(stackId)));
            }
            finally
            {
                SessionService.EndUndoAmend();
            }
        }

        // ---------- Feel: flash and sound ----------

        // The tap and flash come when the card has glided into place (the transfer's settle animation), so the
        // sound lines up with the landing.
        private void ScheduleLandingFeedback(ContainerId pileId, bool newStack)
        {
            landingPileId = pileId;
            landingIsNewStack = newStack;
            landingFeedbackAt = Time.unscaledTime + settleDuration;
        }

        private void PlayDueLandingFeedback()
        {
            if (landingFeedbackAt < 0f || Time.unscaledTime < landingFeedbackAt) return;
            landingFeedbackAt = -1f;
            if (matchState == null || !matchState.Containers.ContainsKey(landingPileId)) return;
            PlayCardSound(landingIsNewStack);
            FlashPile(landingPileId);
        }

        // A short green flash on the pile, using the drop look the pile already has. The end of the drag that just
        // finished may clear pile feedback a moment later, so the flash is held each frame until it ends (setting
        // the same look again does nothing). A new drag takes the feedback over at once.
        private void FlashPile(ContainerId pileId)
        {
            if (interactionRouter != null && interactionRouter.HasActiveInteraction) return;
            if (!feedbackTargetsByContainerId.TryGetValue(pileId, out ContainerFeedbackTarget target)) return;
            if (flashingPile != null && !ReferenceEquals(flashingPile, target) && IsFlashTargetCurrent()) flashingPile.Clear();
            flashingPile = target;
            flashingPileId = pileId;
            pileFlashEndsAt = Time.unscaledTime + PileFlashSeconds;
            target.SetValid();
        }

        private void UpdatePileFlash()
        {
            if (flashingPile == null) return;
            if (!IsFlashTargetCurrent())
            {
                flashingPile = null; // the pile went away or the table was rebuilt
                return;
            }

            bool dragging = interactionRouter != null && interactionRouter.HasActiveInteraction;
            if (!dragging && Time.unscaledTime < pileFlashEndsAt)
            {
                flashingPile.SetValid();
                return;
            }

            if (!dragging) flashingPile.Clear();
            flashingPile = null;
        }

        private bool IsFlashTargetCurrent()
        {
            return feedbackTargetsByContainerId.TryGetValue(flashingPileId, out ContainerFeedbackTarget current)
                && ReferenceEquals(current, flashingPile);
        }

        private void PlayCardSound(bool stacked)
        {
            if (Time.unscaledTime - lastCardSoundAt < CardSoundMinimumGap) return;
            if (!cardSoundsLoaded)
            {
                cardSoundsLoaded = true; // loaded once; missing clips just mean silence
                cardStackClip = Resources.Load<AudioClip>(CardStackSoundPath);
                cardTapClip = Resources.Load<AudioClip>(CardTapSoundPath);
                if (!TryGetComponent(out cardSoundSource)) cardSoundSource = gameObject.AddComponent<AudioSource>();
                cardSoundSource.playOnAwake = false;
                cardSoundSource.spatialBlend = 0f;
            }

            AudioClip clip = stacked ? cardStackClip : cardTapClip;
            if (clip == null || cardSoundSource == null) return;
            lastCardSoundAt = Time.unscaledTime;
            cardSoundSource.pitch = UnityEngine.Random.Range(0.94f, 1.06f); // no two taps quite alike
            cardSoundSource.PlayOneShot(clip, CardSoundVolume);
        }
    }
}
