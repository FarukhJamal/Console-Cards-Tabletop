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
    /// <summary>Drop targets, hidden hands and the interaction graph. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        private void ConfigureDropTargets()
        {
            for (int i = 0; i < runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance instance = runtimeDeckInstances[i];
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

            foreach (StackRuntimeView stackRuntimeView in stackViewsByContainerId.Values)
            {
                ConfigureStackDropTarget(stackRuntimeView);
            }

            for (int i = 0; i < consoleSlotViews.Count; i++)
            {
                ConfigureConsoleSlot(consoleSlotViews[i]);
            }

            ApplyAllConsoleSides();

            for (int i = 0; i < runtimeTokenContainerInstances.Count; i++)
            {
                RuntimeTokenContainerInstance instance = runtimeTokenContainerInstances[i];
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
            GameObject root = PrepareRuntimeRoot(new GameObject(name), name);
            HiddenHandView view = root.AddComponent<HiddenHandView>();
            GameObject labelRoot = new GameObject("Hidden Hand Count Label");
            labelRoot.layer = root.layer;
            labelRoot.transform.SetParent(root.transform, false);
            labelRoot.transform.localScale = Vector3.one * 0.34f;
            TextMesh label = labelRoot.AddComponent<TextMesh>();
            ConfigureWorldLabel(
                label,
                string.Empty,
                TrapFloorCoinAreaLabelCharacterSize,
                TrapFloorCoinAreaLabelFontSize);
            view.Bind(hand, zone, coordinateConverter, cardViews, label);
            return view;
        }

        private void ReleaseHiddenHandViews()
        {
            for (int i = 0; i < hiddenHandViews.Count; i++)
            {
                HiddenHandView hidden = hiddenHandViews[i];
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

            hiddenHandViews.Clear();
        }

        // Another seat's hand while this session has a local hand: shown face-down, contents not inspectable.
        private bool IsHiddenHandContainer(ContainerState container)
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
                cardSelectionVisuals,
                pawnSelectionVisuals,
                tokenSelectionVisuals,
                dieSelectionVisuals);
            componentPlacementController = new TabletopComponentPlacementController(
                pointerProjector,
                coordinateConverter,
                CommitToolboxPlacement,
                HandlePlacementRotationChanged);
            componentPlacementController.PhysicalSurfaces = physicalSurfaceQuery;
            inputFrameCoordinator.ConfigureComponentPlacement(componentPlacementController);
            componentPlacementInputConfiguredByComposition = true;
        }

        private void RebuildInteractionDependencies()
        {
            RegisterPhysicalViews();
            RebuildLayoutViewCollection();
            layoutViewLookup = new ContainerLayoutViewLookup(layoutViews);
            transferCoordinator = new CardTransferInteractionCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                lockService,
                new TransferCardUseCase(),
                layoutViews,
                cardViews,
                presentationTransitions,
                settleDuration,
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
                new MoveObjectUseCase(),
                dropTargetResolver,
                transferCoordinator,
                layoutViewLookup,
                this,
                magneticDistance,
                tokenDropTargetResolver,
                new TransferTokenUseCase(),
                tokenContainerViews);
            rotationCoordinator = new TabletopRotationCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                selectionState,
                lockService,
                new RotateObjectUseCase());
            flipCoordinator = new TabletopCardFlipCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                selectionState,
                lockService,
                new FlipCardUseCase());
            cardFlipTurns?.CompleteAll();
            flipCoordinator.Flipped = BeginCardFlipTurn;
            inputRoutingPolicy = new TabletopInteractionInputRoutingPolicy(selectionState, moveCoordinator);
            interactionRouter = new TabletopInteractionRouter(
                hitResolver,
                moveCoordinator,
                containedCardDragCoordinator,
                selectionState);
            interactionRouter.HandView = handView;
            hitResolver.HandPicker = handView;
            moveCoordinator.IsContainerAtComfortCap = IsOwnHandAtComfortCap;
            moveCoordinator.ComfortCapRejected = ShowHandCapReached;
            containedCardDragCoordinator.IsContainerAtComfortCap = IsOwnHandAtComfortCap;
            containedCardDragCoordinator.ComfortCapRejected = ShowHandCapReached;
            inputRoutingPolicy.ConfigureInteractionRouter(interactionRouter);
            cameraInputAdapter.ConfigureScrollRoutingPolicy(inputRoutingPolicy);
            cameraRoutingConfiguredByComposition = true;
            objectInputAdapter.Initialize(moveCoordinator, rotationCoordinator, flipCoordinator, inputRoutingPolicy);
            objectInputAdapter.ConfigureInteractionRouter(interactionRouter);
            objectAdapterInitializedByComposition = true;
        }

        private void RebuildLayoutLookupAndRouter()
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
            foreach (CardView view in cardViews) physicalAuthority?.Register(view);
            foreach (PawnView view in pawnViews) physicalAuthority?.Register(view);
            foreach (TokenView view in tokenViews) physicalAuthority?.Register(view);
            foreach (DieView view in dieViews) physicalAuthority?.Register(view);
        }

        private void RebuildLayoutViewCollection()
        {
            layoutViews.Clear();
            for (int i = 0; i < controllerDeckViews.Count; i++)
            {
                if (controllerDeckViews[i] != null && controllerDeckViews[i].IsBound)
                {
                    layoutViews.Add(controllerDeckViews[i]);
                }
            }

            if (handView != null && handView.IsBound)
            {
                layoutViews.Add(handView);
            }

            foreach (StackRuntimeView stackRuntimeView in stackViewsByContainerId.Values)
            {
                if (stackRuntimeView.View != null && stackRuntimeView.View.IsBound)
                {
                    layoutViews.Add(stackRuntimeView.View);
                }
            }

            for (int i = 0; i < runtimeDiscardPileInstances.Count; i++)
            {
                DiscardPileView runtimeDiscardView = runtimeDiscardPileInstances[i].View;
                if (runtimeDiscardView != null && runtimeDiscardView.IsBound)
                {
                    layoutViews.Add(runtimeDiscardView);
                }
            }

            for (int i = 0; i < consoleSlotViews.Count; i++)
            {
                if (consoleSlotViews[i] != null && consoleSlotViews[i].IsBound)
                {
                    layoutViews.Add(consoleSlotViews[i]);
                }
            }
        }
    }
}
