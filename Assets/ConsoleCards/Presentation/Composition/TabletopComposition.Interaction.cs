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
    /// <summary>Drop targets, hidden hands and the interaction graph. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        private void ConfigureDropTargets()
        {
            for (int i = 0; i < Registry.runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance instance = Registry.runtimeDeckInstances[i];
                ConfigureFixedContainer(instance.Visual, instance.View);
            }

            if (handView != null && !handView.IsTrayMode)
            {
                // Hands on table (F3): the table plate is the hand's drop target, as for any fixed container.
                localHandVisual.SetBasePlateHidden(false);
                localHandVisual.Label.gameObject.SetActive(false);
                ConfigureFixedContainer(localHandVisual, handView);
            }
            else
            {
                ConfigureHandTrayDropTarget();
            }

            foreach (StackRuntimeView stackRuntimeView in Registry.stackViewsByContainerId.Values)
            {
                ConfigureStackDropTarget(stackRuntimeView);
            }

            for (int i = 0; i < Registry.consoleSlotViews.Count; i++)
            {
                ConfigureConsoleSlot(Registry.consoleSlotViews[i]);
            }

            ApplyAllConsoleSides();

            for (int i = 0; i < Registry.runtimeTokenContainerInstances.Count; i++)
            {
                RuntimeTokenContainerInstance instance = Registry.runtimeTokenContainerInstances[i];
                instance.DropTarget.Configure(instance.View, instance.TargetCollider);
                instance.DropTarget.enabled = true;
                instance.TargetCollider.enabled = true;
            }
        }

        // The table hand plate stays bound for its HandView but is hidden and never a drop target;
        // the camera tray band receives hand drops and shows the hand feedback instead.
        private void ConfigureHandTrayDropTarget()
        {
            localHandVisual.DropTarget.ClearConfiguration();
            localHandVisual.DropTarget.enabled = false;
            localHandVisual.TargetCollider.enabled = false;
            localHandVisual.SetBasePlateHidden(true);
            localHandVisual.ClearFeedback();
            localHandVisual.Label.gameObject.SetActive(false);
            handTrayRig.ConfigureDropTarget(handView);
            feedbackTargetsByContainerId[handView.ContainerId] = new ContainerFeedbackTarget(handTrayRig);
        }

        private HiddenHandView CreateHiddenHandView(
            string name,
            ContainerState hand,
            ContainerPlacementState zone)
        {
            GameObject root = Spawner.PrepareRuntimeRoot(new GameObject(name), name);
            HiddenHandView view = root.AddComponent<HiddenHandView>();
            GameObject labelRoot = new GameObject("Hidden Hand Count Label");
            labelRoot.layer = root.layer;
            labelRoot.transform.SetParent(root.transform, false);
            labelRoot.transform.localScale = Vector3.one * 0.34f;
            TextMesh label = labelRoot.AddComponent<TextMesh>();
            ConfigureWorldLabel(
                label,
                string.Empty,
                CoinAreaLabelCharacterSize,
                CoinAreaLabelFontSize);
            view.Bind(hand, zone, coordinateConverter, Registry.cardViews, label);
            return view;
        }

        private void ReleaseHiddenHandViews()
        {
            for (int i = 0; i < Registry.hiddenHandViews.Count; i++)
            {
                HiddenHandView hidden = Registry.hiddenHandViews[i];
                if (hidden == null)
                {
                    continue;
                }

                if (hidden.IsBound)
                {
                    hidden.Unbind();
                }

                Destroy(hidden.gameObject);
            }

            Registry.hiddenHandViews.Clear();
        }

        // Another seat's hand while this session has a local hand: shown face-down, contents not inspectable.
        internal bool IsHiddenHandContainer(ContainerState container)
        {
            return container != null
                && container.Kind == ContainerKind.Hand
                && !handContainerId.IsEmpty
                && container.Id != handContainerId;
        }

        private void BuildInteractionGraph()
        {
            selectionState = new TabletopSelectionState();
            hitResolver = new TabletopObjectHitResolver(targetCamera, interactionLayerMask, maximumHitDistance);
            hitResolver.IsHidden = IsUnderConsoleMat;
            pointerProjector = new TabletopPointerProjector(
                targetCamera,
                coordinateConverter,
                coordinateConverter.ToWorldPosition(new TableCoordinate(0d, 0d)).y);
            lockService = new LocalInteractionLockService();
            interactionStateMachine = new TabletopInteractionStateMachine(dragThresholdPixels);
            previewSession = new TabletopDragPreviewSession(
                presentationTransitions,
                pickupLift,
                dragLift,
                pickupResponseDuration,
                dragFollowSmoothing,
                settleDuration,
                returnDuration);
            dropTargetResolver = new CardDropTargetResolver(
                targetCamera,
                pointerProjector,
                interactionLayerMask,
                maximumHitDistance,
                QueryTriggerInteraction.Collide);
            dropTargetResolver.PhysicalSurfaces = physicalSurfaceQuery;
            dropTargetResolver.Magnet = this;
            tokenDropTargetResolver = new TokenDropTargetResolver(
                targetCamera,
                interactionLayerMask,
                maximumHitDistance);
            RebuildInteractionDependencies();
            selectionPresenter = new TabletopSelectionPresenter(
                selectionState,
                Registry.cardSelectionVisuals,
                Registry.pawnSelectionVisuals,
                Registry.tokenSelectionVisuals,
                Registry.dieSelectionVisuals);
            componentPlacementController = new TabletopComponentPlacementController(
                pointerProjector,
                coordinateConverter,
                CommitToolboxPlacement,
                HandlePlacementRotationChanged);
            componentPlacementController.PhysicalSurfaces = physicalSurfaceQuery;
            componentPlacementController.Placed = PlayPlacementSound; // owner, 2026-10-19: placement has a sound
            inputFrameCoordinator.ConfigureComponentPlacement(componentPlacementController);
            componentPlacementInputConfiguredByComposition = true;
        }

        private void RebuildInteractionDependencies()
        {
            RegisterPhysicalViews();
            RebuildLayoutViewCollection();
            layoutViewLookup = new ContainerLayoutViewLookup(Registry.layoutViews);
            transferCoordinator = new CardTransferInteractionCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                lockService,
                TableAuthority,
                Registry.layoutViews,
                Registry.cardViews,
                presentationTransitions,
                Mathf.Max(settleDuration, Feel.DropSettleSeconds), // POL-4: drops slide into a pile, not snap
                returnDuration,
                handReflowDuration,
                HandleConsoleCardInteractionAccepted);
            containedCardDragCoordinator = handView != null
                ? new ContainedCardDragCoordinator(
                    interactionOwnerId,
                    lockService,
                    interactionStateMachine,
                    previewSession,
                    pointerProjector,
                    dropTargetResolver,
                    transferCoordinator,
                    layoutViewLookup,
                    this,
                    magneticDistance,
                    handView,
                    ReorderHandCardFromDrag)
                : new ContainedCardDragCoordinator(
                    interactionOwnerId,
                    lockService,
                    interactionStateMachine,
                    previewSession,
                    pointerProjector,
                    dropTargetResolver,
                    transferCoordinator,
                    layoutViewLookup,
                    this,
                    magneticDistance);
            moveCoordinator = new TabletopMoveInteractionCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                interactionStateMachine,
                previewSession,
                TableAuthority,
                dropTargetResolver,
                transferCoordinator,
                layoutViewLookup,
                this,
                magneticDistance,
                tokenDropTargetResolver,
                Registry.tokenContainerViews);
            rotationCoordinator = new TabletopRotationCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                selectionState,
                lockService,
                TableAuthority);
            flipCoordinator = new TabletopCardFlipCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                selectionState,
                lockService,
                TableAuthority);
            cardFlipTurns?.CompleteAll();
            flipCoordinator.Flipped = BeginCardFlipTurn;
            inputRoutingPolicy = new TabletopInteractionInputRoutingPolicy(selectionState, moveCoordinator);
            interactionRouter = new TabletopInteractionRouter(
                hitResolver,
                moveCoordinator,
                containedCardDragCoordinator,
                selectionState);
            interactionRouter.HandView = handView;
            interactionRouter.CarryHandler = this; // press and hold a stack to carry it whole (owner, 2026-10-18)
            interactionRouter.FlickHandler = this; // UX-1: Shift-press a loose piece, pull back, let go
            interactionRouter.SelectHandler = this; // TOOLS-1: the Select tool, or Ctrl held: click or box-select cards
            interactionRouter.LongPressSeconds = Feel.StackHoldSeconds; // POL-0: from Resources/FeelSettings
            interactionRouter.LogMissedPresses = Feel.LogMissedClicks;
            inputFrameCoordinator.LogMissedPresses = Feel.LogMissedClicks;
            hitResolver.HandPicker = handView;
            moveCoordinator.IsContainerAtComfortCap = ContextMenus.IsOwnHandAtComfortCap;
            moveCoordinator.ComfortCapRejected = ContextMenus.ShowHandCapReached;
            containedCardDragCoordinator.IsContainerAtComfortCap = ContextMenus.IsOwnHandAtComfortCap;
            containedCardDragCoordinator.ComfortCapRejected = ContextMenus.ShowHandCapReached;
            inputRoutingPolicy.ConfigureInteractionRouter(interactionRouter);
            cameraInputAdapter.ConfigureScrollRoutingPolicy(inputRoutingPolicy);
            cameraRoutingConfiguredByComposition = true;
            objectInputAdapter.Initialize(moveCoordinator, rotationCoordinator, flipCoordinator, inputRoutingPolicy);
            objectInputAdapter.ConfigureInteractionRouter(interactionRouter);
            objectAdapterInitializedByComposition = true;
        }

        internal void RebuildLayoutLookupAndRouter()
        {
            SuspendInteractionDependenciesForRebuild();
            ResumeInteractionDependenciesAfterRebuild();
        }

        private void SuspendInteractionDependenciesForRebuild()
        {
            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                throw new InvalidOperationException("Cannot rebuild M3 interaction graph during an active interaction.");
            }

            // The state machine is shared and outlives the rebuild, and the new coordinators require it to be Idle.
            // A rebuild can run while the pointer merely rests over a piece (e.g. a card-on-card stack forming under
            // the cursor); drop that hover first, or the rebuild stops half way and nothing on the table responds.
            if (interactionStateMachine != null && interactionStateMachine.Phase == TabletopInteractionPhase.Hovering)
            {
                interactionStateMachine.ClearHoveredObject();
            }

            // POL-0 diagnostics: a click in the same frame as a rebuild can be lost.
            if (Feel.LogMissedClicks && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Debug.Log("[ConsoleCards Input] A click came in the same frame as the table rebuilt its controls; it may be lost.");
            }

            if (frameCoordinatorEnabledByComposition)
            {
                inputFrameCoordinator.enabled = false;
                frameCoordinatorEnabledByComposition = false;
            }

            if (objectAdapterInitializedByComposition)
            {
                objectInputAdapter.Shutdown();
                objectAdapterInitializedByComposition = false;
            }

            if (cameraRoutingConfiguredByComposition)
            {
                cameraInputAdapter.ClearScrollRoutingPolicy();
                cameraRoutingConfiguredByComposition = false;
            }

            inputRoutingPolicy?.ClearInteractionRouter();
            layoutViewLookup = null;
            transferCoordinator = null;
            containedCardDragCoordinator = null;
            moveCoordinator = null;
            rotationCoordinator = null;
            flipCoordinator = null;
            inputRoutingPolicy = null;
            interactionRouter = null;
        }

        private void ResumeInteractionDependenciesAfterRebuild()
        {
            RebuildInteractionDependencies();
            inputFrameCoordinator.enabled = true;
            frameCoordinatorEnabledByComposition = true;
            selectionPresenter.Refresh();
        }

        private void RegisterPhysicalViews()
        {
            foreach (CardView view in Registry.cardViews) physicalAuthority?.Register(view);
            foreach (PawnView view in Registry.pawnViews) physicalAuthority?.Register(view);
            foreach (TokenView view in Registry.tokenViews) physicalAuthority?.Register(view);
            foreach (DieView view in Registry.dieViews) physicalAuthority?.Register(view);
        }

        private void RebuildLayoutViewCollection()
        {
            Registry.layoutViews.Clear();
            for (int i = 0; i < Registry.controllerDeckViews.Count; i++)
            {
                if (Registry.controllerDeckViews[i] != null && Registry.controllerDeckViews[i].IsBound)
                {
                    Registry.layoutViews.Add(Registry.controllerDeckViews[i]);
                }
            }

            if (handView != null && handView.IsBound)
            {
                Registry.layoutViews.Add(handView);
            }

            foreach (StackRuntimeView stackRuntimeView in Registry.stackViewsByContainerId.Values)
            {
                if (stackRuntimeView.View != null && stackRuntimeView.View.IsBound)
                {
                    Registry.layoutViews.Add(stackRuntimeView.View);
                }
            }

            for (int i = 0; i < Registry.runtimeDiscardPileInstances.Count; i++)
            {
                DiscardPileView runtimeDiscardView = Registry.runtimeDiscardPileInstances[i].View;
                if (runtimeDiscardView != null && runtimeDiscardView.IsBound)
                {
                    Registry.layoutViews.Add(runtimeDiscardView);
                }
            }

            for (int i = 0; i < Registry.consoleSlotViews.Count; i++)
            {
                if (Registry.consoleSlotViews[i] != null && Registry.consoleSlotViews[i].IsBound)
                {
                    Registry.layoutViews.Add(Registry.consoleSlotViews[i]);
                }
            }
        }
    }
}
