using System;
using System.Collections.Generic;
using System.Reflection;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopDiscreteObjectInputRoutingTests
    {
        private const int InteractionLayer = 8;
        private const float FloatTolerance = 0.0001f;
        private const double CoordinateTolerance = 0.00001d;
        private const float DefaultScrollDelta = 120f;
        private const float DeltaTime = 1f;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();
        private readonly List<InputActionAsset> createdInputAssets = new List<InputActionAsset>();
        private readonly List<InputActionReference> createdActionReferences = new List<InputActionReference>();
        private readonly List<InputDevice> createdInputDevices = new List<InputDevice>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdInputAssets.Count; i++)
            {
                if (createdInputAssets[i] != null)
                {
                    createdInputAssets[i].Disable();
                }
            }

            for (int i = 0; i < createdActionReferences.Count; i++)
            {
                if (createdActionReferences[i] != null)
                {
                    UnityObject.DestroyImmediate(createdActionReferences[i]);
                }
            }

            for (int i = 0; i < createdInputAssets.Count; i++)
            {
                if (createdInputAssets[i] != null)
                {
                    UnityObject.DestroyImmediate(createdInputAssets[i]);
                }
            }

            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                if (createdGameObjects[i] != null)
                {
                    UnityObject.DestroyImmediate(createdGameObjects[i]);
                }
            }

            for (int i = 0; i < createdInputDevices.Count; i++)
            {
                if (createdInputDevices[i] != null)
                {
                    InputSystem.RemoveDevice(createdInputDevices[i]);
                }
            }

            createdActionReferences.Clear();
            createdInputAssets.Clear();
            createdGameObjects.Clear();
            createdInputDevices.Clear();
        }

        [TestCase(TabletopObjectKind.Card)]
        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void ApplyInputFrame_WhenStableSelectedObjectScrolls_RotatesOnceAndSuppressesCameraZoom(
            TabletopObjectKind kind)
        {
            RoutingFixture fixture = CreateFixture(kind);

            fixture.ApplySharedFrame(fixture.CreateFrame(rotateDelta: DefaultScrollDelta, scrollDelta: DefaultScrollDelta));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(15f).Within(FloatTolerance));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.True);
            Assert.That(fixture.ObjectAdapter.LastRotationResult.Value.Status, Is.EqualTo(RotationInteractionStatus.RotationAccepted));
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [TestCase(120f, 15f)]
        [TestCase(1f, 15f)]
        [TestCase(-120f, -15f)]
        public void ApplyInputFrame_WhenStableSelectedObjectScrolls_UsesOneSignedConfiguredStep(
            float rawRotateDelta,
            float expectedRotation)
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card, rotationStepDegrees: 15f);

            fixture.ApplySharedFrame(fixture.CreateFrame(rotateDelta: rawRotateDelta, scrollDelta: rawRotateDelta));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(expectedRotation).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
        }

        [Test]
        public void ApplyInputFrame_WhenNoSelectionScrolls_RoutesToCameraZoomWithoutRotationResult()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card, selectView: false);
            float initialOrthographicSize = fixture.CameraController.State.OrthographicSize;
            float expectedOrthographicSize = initialOrthographicSize
                - DefaultScrollDelta * fixture.CameraAdapter.ZoomSensitivity;

            fixture.ApplySharedFrame(fixture.CreateFrame(rotateDelta: DefaultScrollDelta, scrollDelta: DefaultScrollDelta));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(expectedOrthographicSize).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenSelectedObjectIsUserLocked_StoresRejectedRotationAndSuppressesCameraZoom()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card, isUserLocked: true);

            fixture.ApplySharedFrame(fixture.CreateFrame(rotateDelta: DefaultScrollDelta, scrollDelta: DefaultScrollDelta));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.Value.Status, Is.EqualTo(RotationInteractionStatus.ObjectUserLocked));
        }

        [Test]
        public void ApplyInputFrame_WhenSelectedObjectHasLocalLockConflict_StoresRejectedRotationAndSuppressesCameraZoom()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);
            fixture.LockService.Acquire(fixture.View.ObjectId, InteractionOwnerId.New());

            fixture.ApplySharedFrame(fixture.CreateFrame(rotateDelta: DefaultScrollDelta, scrollDelta: DefaultScrollDelta));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.Value.Status, Is.EqualTo(RotationInteractionStatus.LocalLockConflict));
            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
        }

        [Test]
        public void ApplyInputFrame_WhenMoveInteractionIsActive_SuppressesRotateFlipAndCameraZoom()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);
            fixture.BeginDraggingObject();

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.DragScreenPoint,
                selectHeld: true,
                rotateDelta: DefaultScrollDelta,
                scrollDelta: DefaultScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenSelectedViewIsPreviewing_SuppressesRotateFlipAndCameraZoom()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);
            fixture.View.ApplyPreviewPose(CreatePose(rotationDegrees: 45f));

            fixture.ApplySharedFrame(fixture.CreateFrame(rotateDelta: DefaultScrollDelta, scrollDelta: DefaultScrollDelta, flipPressedThisFrame: true));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [TestCase(CardFace.FaceUp, CardFace.FaceDown)]
        [TestCase(CardFace.FaceDown, CardFace.FaceUp)]
        public void ApplyInputFrame_WhenStableSelectedCardFlips_FlipsOnceAndDoesNotMoveCamera(
            CardFace initialFace,
            CardFace expectedFace)
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card, initialFace: initialFace);

            fixture.ApplySharedFrame(fixture.CreateFrame(flipPressedThisFrame: true));

            Assert.That(fixture.CardState.Face, Is.EqualTo(expectedFace));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
        }

        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void ApplyInputFrame_WhenSelectedNonCardFlips_StoresSelectionNotCardWithoutRevision(
            TabletopObjectKind kind)
        {
            RoutingFixture fixture = CreateFixture(kind);

            fixture.ApplySharedFrame(fixture.CreateFrame(flipPressedThisFrame: true));

            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Status, Is.EqualTo(FlipInteractionStatus.SelectionNotCard));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenPointerTransitionIncludesRotateFlip_SuppressesDiscreteActionsAndScroll()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                selectPressedThisFrame: true,
                rotateDelta: DefaultScrollDelta,
                scrollDelta: DefaultScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenFlipAndRotateOccurInStableFrame_FlipWinsAndRevisionAdvancesOnce()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                rotateDelta: DefaultScrollDelta,
                scrollDelta: DefaultScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenCancelOccurs_SuppressesMoveRotateAndFlip()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);
            fixture.ObjectAdapter.ApplyInputFrame(fixture.ObjectScreenPoint, true, false, false, false);
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.True);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                cancelPressedThisFrame: true,
                rotateDelta: DefaultScrollDelta,
                scrollDelta: DefaultScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenReleaseHeldAndRotateOccur_ReleaseWinsAndDiscreteActionsAreSuppressed()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);
            fixture.BeginDraggingObject();

            MoveInteractionReleaseResult? releaseResult = fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.ReleaseScreenPoint,
                selectHeld: true,
                selectReleasedThisFrame: true,
                rotateDelta: DefaultScrollDelta,
                scrollDelta: DefaultScrollDelta));

            Assert.That(releaseResult.HasValue, Is.True);
            Assert.That(releaseResult.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
            AssertCoordinate(fixture.State.Pose.Position, 4.0, -2.0);
            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenPointerTransitionHasKeyboardPan_SuppressesDiscreteActionsButAppliesKeyboardPan()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                keyboardPan: new Vector2(1f, 0f),
                selectPressedThisFrame: true,
                rotateDelta: DefaultScrollDelta,
                scrollDelta: DefaultScrollDelta,
                flipPressedThisFrame: true));

            AssertCoordinate(fixture.CameraController.State.FocusCoordinate, 5.0, 0.0);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenPointerTransitionHasDragPan_SuppressesDiscreteActionsButAppliesDragPan()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                dragHeld: true,
                pointerDelta: new Vector2(10f, 0f),
                selectPressedThisFrame: true,
                rotateDelta: DefaultScrollDelta,
                scrollDelta: DefaultScrollDelta,
                flipPressedThisFrame: true));

            AssertCoordinate(fixture.CameraController.State.FocusCoordinate, -0.2, 0.0);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void Update_WhenAdapterPollsStandalone_DoesNotExecuteRotateOrFlip()
        {
            RoutingFixture fixture = CreateFixture(TabletopObjectKind.Card, createFrameCoordinator: false);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            createdInputDevices.Add(mouse);
            createdInputDevices.Add(keyboard);

            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, DefaultScrollDelta) });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            InputSystem.Update();

            InvokeUpdate(fixture.ObjectAdapter);

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenSetupOrderVaries_StableSelectedScrollProducesIdenticalOutcome()
        {
            RoutingFixture cameraFirst = CreateFixture(TabletopObjectKind.Card, setupOrder: FixtureSetupOrder.CameraAdapterFirst);
            RoutingFixture objectFirst = CreateFixture(TabletopObjectKind.Card, setupOrder: FixtureSetupOrder.ObjectAdapterFirst);

            cameraFirst.ApplySharedFrame(cameraFirst.CreateFrame(rotateDelta: DefaultScrollDelta, scrollDelta: DefaultScrollDelta));
            objectFirst.ApplySharedFrame(objectFirst.CreateFrame(rotateDelta: DefaultScrollDelta, scrollDelta: DefaultScrollDelta));

            Assert.That(cameraFirst.CameraController.State.OrthographicSize, Is.EqualTo(objectFirst.CameraController.State.OrthographicSize).Within(FloatTolerance));
            AssertCoordinate(cameraFirst.CameraController.State.FocusCoordinate, objectFirst.CameraController.State.FocusCoordinate);
            Assert.That(cameraFirst.State.Pose, Is.EqualTo(objectFirst.State.Pose));
            Assert.That(cameraFirst.Match.Revision, Is.EqualTo(objectFirst.Match.Revision));
            Assert.That(cameraFirst.ObjectAdapter.LastRotationResult.Value.Status, Is.EqualTo(objectFirst.ObjectAdapter.LastRotationResult.Value.Status));
        }

        [Test]
        public void TabletopInputFrame_WhenRotateDeltaIsInvalid_ThrowsBeforeFrameExists()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopInputFrame(
                Vector2.zero,
                false,
                Vector2.zero,
                0f,
                Vector2.zero,
                false,
                false,
                false,
                false,
                float.NaN,
                false));
        }

        [TestCase(1f, false)]
        [TestCase(0f, true)]
        public void TabletopInputFrame_WhenDiscreteObjectActionIsPresent_ReportsIt(
            float rotateDelta,
            bool flipPressedThisFrame)
        {
            TabletopInputFrame frame = new TabletopInputFrame(
                Vector2.zero,
                false,
                Vector2.zero,
                0f,
                Vector2.zero,
                false,
                false,
                false,
                false,
                rotateDelta,
                flipPressedThisFrame);

            Assert.That(frame.HasDiscreteObjectAction, Is.True);
        }

        private RoutingFixture CreateFixture(
            TabletopObjectKind kind,
            bool selectView = true,
            bool isUserLocked = false,
            CardFace initialFace = CardFace.FaceUp,
            float rotationStepDegrees = 15f,
            FixtureSetupOrder setupOrder = FixtureSetupOrder.CameraAdapterFirst,
            bool createFrameCoordinator = true)
        {
            TabletopCoordinateConverter converter = CreateConverter();
            UnityEngine.Camera pointerCamera = CreateTopDownCamera("Discrete Routing Camera");
            TabletopObjectView view = CreateBoundView(
                kind,
                converter,
                isUserLocked,
                initialFace,
                out TabletopObjectState state,
                out CardInstanceState cardState,
                out PawnState pawnState,
                out TokenState tokenState);
            AddBoxCollider(view.gameObject, InteractionLayer);
            Physics.SyncTransforms();

            MatchState match = CreateMatch(0, cardState, pawnState, tokenState);
            TabletopSelectionState selectionState = new TabletopSelectionState();
            if (selectView)
            {
                selectionState.Select(view);
            }

            TabletopObjectHitResolver hitResolver = new TabletopObjectHitResolver(
                pointerCamera,
                LayerMaskFor(InteractionLayer),
                25f);
            TabletopPointerProjector pointerProjector = new TabletopPointerProjector(
                pointerCamera,
                converter,
                0f);
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(5f);
            TabletopDragPreviewSession previewSession = new TabletopDragPreviewSession();
            PlayerId requestedByPlayerId = PlayerId.New();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            TabletopMoveInteractionCoordinator moveCoordinator = new TabletopMoveInteractionCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                stateMachine,
                previewSession,
                new MoveObjectUseCase());
            TabletopRotationCoordinator rotationCoordinator = new TabletopRotationCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                selectionState,
                lockService,
                new RotateObjectUseCase());
            TabletopCardFlipCoordinator flipCoordinator = new TabletopCardFlipCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                selectionState,
                lockService,
                new FlipCardUseCase());
            TabletopInteractionInputRoutingPolicy routingPolicy = new TabletopInteractionInputRoutingPolicy(
                selectionState,
                moveCoordinator);

            TabletopCameraController cameraController = CreateInitializedCameraController(pointerCamera);
            TabletopCameraInputAdapter cameraAdapter;
            TabletopObjectInputAdapter objectAdapter;
            if (setupOrder == FixtureSetupOrder.CameraAdapterFirst)
            {
                cameraAdapter = CreateInitializedCameraAdapter(cameraController, routingPolicy);
                objectAdapter = CreateInitializedObjectAdapter(
                    moveCoordinator,
                    rotationCoordinator,
                    flipCoordinator,
                    routingPolicy,
                    rotationStepDegrees);
            }
            else
            {
                objectAdapter = CreateInitializedObjectAdapter(
                    moveCoordinator,
                    rotationCoordinator,
                    flipCoordinator,
                    routingPolicy,
                    rotationStepDegrees);
                cameraAdapter = CreateInitializedCameraAdapter(cameraController, routingPolicy);
            }

            TabletopInputFrameCoordinator frameCoordinator = createFrameCoordinator
                ? CreateFrameCoordinator(cameraAdapter, objectAdapter)
                : null;

            return new RoutingFixture(
                cameraController,
                cameraAdapter,
                objectAdapter,
                frameCoordinator,
                moveCoordinator,
                routingPolicy,
                lockService,
                match,
                selectionState,
                view,
                state,
                cardState,
                pointerCamera);
        }

        private TabletopInputFrameCoordinator CreateFrameCoordinator(
            TabletopCameraInputAdapter cameraAdapter,
            TabletopObjectInputAdapter objectAdapter)
        {
            GameObject coordinatorObject = CreateGameObject("Discrete Routing Frame Coordinator");
            coordinatorObject.SetActive(false);
            TabletopInputFrameCoordinator coordinator = coordinatorObject.AddComponent<TabletopInputFrameCoordinator>();
            coordinator.cameraInputAdapter = cameraAdapter;
            coordinator.objectInputAdapter = objectAdapter;
            coordinatorObject.SetActive(true);
            return coordinator;
        }

        private TabletopCameraController CreateInitializedCameraController(UnityEngine.Camera targetCamera)
        {
            Transform cameraRig = CreateGameObject("Discrete Routing Camera Rig").transform;
            GameObject controllerObject = CreateGameObject("Discrete Routing Camera Controller");
            controllerObject.SetActive(false);
            TabletopCameraController controller = controllerObject.AddComponent<TabletopCameraController>();
            controller.targetCamera = targetCamera;
            controller.cameraRig = cameraRig;
            controllerObject.SetActive(true);
            return controller;
        }

        private TabletopCameraInputAdapter CreateInitializedCameraAdapter(
            TabletopCameraController cameraController,
            TabletopInteractionInputRoutingPolicy routingPolicy)
        {
            InputActionMap actionMap = CreateActionMap("DiscreteRoutingCamera");
            InputActionReference keyboardPanAction = CreateActionReference(actionMap, "KeyboardPan", InputActionType.Value, "Vector2");
            InputActionReference dragPanAction = CreateActionReference(actionMap, "DragPan", InputActionType.Button, "Button");
            InputActionReference pointerDeltaAction = CreateActionReference(actionMap, "PointerDelta", InputActionType.PassThrough, "Vector2");
            InputActionReference zoomAction = CreateActionReference(actionMap, "Zoom", InputActionType.PassThrough, "Axis");

            GameObject adapterObject = CreateGameObject("Discrete Routing Camera Adapter");
            adapterObject.SetActive(false);
            TabletopCameraInputAdapter adapter = adapterObject.AddComponent<TabletopCameraInputAdapter>();
            adapter.cameraController = cameraController;
            adapter.keyboardPanAction = keyboardPanAction;
            adapter.dragPanAction = dragPanAction;
            adapter.pointerDeltaAction = pointerDeltaAction;
            adapter.zoomAction = zoomAction;
            adapter.keyboardPanSpeed = 5f;
            adapter.dragPanUnitsPerPixel = 0.02f;
            adapter.zoomSensitivity = 0.01f;
            adapterObject.SetActive(true);
            adapter.ConfigureScrollRoutingPolicy(routingPolicy);
            return adapter;
        }

        private TabletopObjectInputAdapter CreateInitializedObjectAdapter(
            TabletopMoveInteractionCoordinator moveCoordinator,
            TabletopRotationCoordinator rotationCoordinator,
            TabletopCardFlipCoordinator flipCoordinator,
            TabletopInteractionInputRoutingPolicy routingPolicy,
            float rotationStepDegrees)
        {
            InputActionMap actionMap = CreateActionMap("DiscreteRoutingObject");
            InputActionReference pointAction = CreateActionReference(actionMap, "Point", InputActionType.PassThrough, "Vector2");
            InputActionReference selectAction = CreateActionReference(actionMap, "Select", InputActionType.Button, "Button");
            InputActionReference cancelAction = CreateActionReference(actionMap, "Cancel", InputActionType.Button, "Button");
            InputActionReference rotateAction = CreateActionReference(actionMap, "Rotate", InputActionType.PassThrough, "Axis");
            InputActionReference flipAction = CreateActionReference(actionMap, "Flip", InputActionType.Button, "Button");

            GameObject adapterObject = CreateGameObject("Discrete Routing Object Adapter");
            adapterObject.SetActive(false);
            TabletopObjectInputAdapter adapter = adapterObject.AddComponent<TabletopObjectInputAdapter>();
            adapter.pointAction = pointAction;
            adapter.selectAction = selectAction;
            adapter.cancelAction = cancelAction;
            adapter.rotateAction = rotateAction;
            adapter.flipAction = flipAction;
            adapter.rotationStepDegrees = rotationStepDegrees;
            adapterObject.SetActive(true);
            adapter.Initialize(
                moveCoordinator,
                rotationCoordinator,
                flipCoordinator,
                routingPolicy);
            return adapter;
        }

        private TabletopObjectView CreateBoundView(
            TabletopObjectKind kind,
            TabletopCoordinateConverter converter,
            bool isUserLocked,
            CardFace initialFace,
            out TabletopObjectState state,
            out CardInstanceState cardState,
            out PawnState pawnState,
            out TokenState tokenState)
        {
            cardState = null;
            pawnState = null;
            tokenState = null;
            state = CreateBaseState(kind, 1, TabletopPose.Default, isUserLocked);
            switch (kind)
            {
                case TabletopObjectKind.Card:
                {
                    CardView view = CreateView<CardView>();
                    cardState = new CardInstanceState(state, initialFace);
                    view.Bind(cardState, converter);
                    return view;
                }

                case TabletopObjectKind.Pawn:
                {
                    PawnView view = CreateView<PawnView>();
                    pawnState = new PawnState(state);
                    view.Bind(pawnState, converter);
                    return view;
                }

                case TabletopObjectKind.Token:
                {
                    TokenView view = CreateView<TokenView>();
                    tokenState = new TokenState(state);
                    view.Bind(tokenState, converter);
                    return view;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
            }
        }

        private UnityEngine.Camera CreateTopDownCamera(string name)
        {
            GameObject cameraObject = CreateGameObject(name);
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
            return camera;
        }

        private T CreateView<T>()
            where T : TabletopObjectView
        {
            GameObject gameObject = CreateGameObject(typeof(T).Name);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private InputActionMap CreateActionMap(string name)
        {
            InputActionAsset inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            createdInputAssets.Add(inputActionAsset);
            return inputActionAsset.AddActionMap(name);
        }

        private InputActionReference CreateActionReference(
            InputActionMap actionMap,
            string actionName,
            InputActionType actionType,
            string expectedControlType)
        {
            InputAction action = actionMap.AddAction(actionName, actionType, expectedControlLayout: expectedControlType);
            AddBinding(action, actionName);
            InputActionReference actionReference = InputActionReference.Create(action);
            createdActionReferences.Add(actionReference);
            return actionReference;
        }

        private static void AddBinding(InputAction action, string actionName)
        {
            switch (actionName)
            {
                case "KeyboardPan":
                    action.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/w")
                        .With("Down", "<Keyboard>/s")
                        .With("Left", "<Keyboard>/a")
                        .With("Right", "<Keyboard>/d");
                    break;
                case "DragPan":
                    action.AddBinding("<Mouse>/middleButton");
                    break;
                case "PointerDelta":
                    action.AddBinding("<Mouse>/delta");
                    break;
                case "Zoom":
                    action.AddBinding("<Mouse>/scroll/y");
                    break;
                case "Point":
                    action.AddBinding("<Mouse>/position");
                    break;
                case "Select":
                    action.AddBinding("<Mouse>/leftButton");
                    break;
                case "Cancel":
                    action.AddBinding("<Keyboard>/escape");
                    break;
                case "Rotate":
                    action.AddBinding("<Mouse>/scroll/y");
                    break;
                case "Flip":
                    action.AddBinding("<Keyboard>/f");
                    break;
            }
        }

        private static MatchState CreateMatch(
            long revision,
            CardInstanceState cardState,
            PawnState pawnState,
            TokenState tokenState)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cardState == null ? Array.Empty<CardInstanceState>() : new[] { cardState },
                pawnState == null ? Array.Empty<PawnState>() : new[] { pawnState },
                tokenState == null ? Array.Empty<TokenState>() : new[] { tokenState },
                Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());
        }

        private static TabletopObjectState CreateBaseState(
            TabletopObjectKind kind,
            int seed,
            TabletopPose pose,
            bool isUserLocked)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                pose,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                isUserLocked);
        }

        private static TabletopPose CreatePose(
            double x = 0.0,
            double y = 0.0,
            float rotationDegrees = 0f)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, 0, 0);
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static BoxCollider AddBoxCollider(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
            return collider;
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void InvokeUpdate(MonoBehaviour behaviour)
        {
            MethodInfo updateMethod = behaviour.GetType().GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updateMethod, Is.Not.Null);
            updateMethod.Invoke(behaviour, null);
        }

        private static void AssertCoordinate(TableCoordinate actual, double expectedX, double expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(CoordinateTolerance));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(CoordinateTolerance));
        }

        private static void AssertCoordinate(TableCoordinate actual, TableCoordinate expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(CoordinateTolerance));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(CoordinateTolerance));
        }

        private enum FixtureSetupOrder
        {
            CameraAdapterFirst,
            ObjectAdapterFirst
        }

        private sealed class RoutingFixture
        {
            public RoutingFixture(
                TabletopCameraController cameraController,
                TabletopCameraInputAdapter cameraAdapter,
                TabletopObjectInputAdapter objectAdapter,
                TabletopInputFrameCoordinator frameCoordinator,
                TabletopMoveInteractionCoordinator moveCoordinator,
                TabletopInteractionInputRoutingPolicy routingPolicy,
                LocalInteractionLockService lockService,
                MatchState match,
                TabletopSelectionState selectionState,
                TabletopObjectView view,
                TabletopObjectState state,
                CardInstanceState cardState,
                UnityEngine.Camera pointerCamera)
            {
                CameraController = cameraController;
                CameraAdapter = cameraAdapter;
                ObjectAdapter = objectAdapter;
                FrameCoordinator = frameCoordinator;
                MoveCoordinator = moveCoordinator;
                RoutingPolicy = routingPolicy;
                LockService = lockService;
                Match = match;
                SelectionState = selectionState;
                View = view;
                State = state;
                CardState = cardState;
                PointerCamera = pointerCamera;
                ObjectScreenPoint = ScreenPointForWorld(0f, 0f);
                DragScreenPoint = ScreenPointForWorld(2f, 0f);
                ReleaseScreenPoint = ScreenPointForWorld(4f, -2f);
            }

            public TabletopCameraController CameraController { get; }

            public TabletopCameraInputAdapter CameraAdapter { get; }

            public TabletopObjectInputAdapter ObjectAdapter { get; }

            public TabletopInputFrameCoordinator FrameCoordinator { get; }

            public TabletopMoveInteractionCoordinator MoveCoordinator { get; }

            public TabletopInteractionInputRoutingPolicy RoutingPolicy { get; }

            public LocalInteractionLockService LockService { get; }

            public MatchState Match { get; }

            public TabletopSelectionState SelectionState { get; }

            public TabletopObjectView View { get; }

            public TabletopObjectState State { get; }

            public CardInstanceState CardState { get; }

            public UnityEngine.Camera PointerCamera { get; }

            public Vector2 ObjectScreenPoint { get; }

            public Vector2 DragScreenPoint { get; }

            public Vector2 ReleaseScreenPoint { get; }

            public MoveInteractionReleaseResult? ApplySharedFrame(TabletopInputFrame frame)
            {
                return FrameCoordinator.ApplyInputFrame(frame, DeltaTime);
            }

            public void BeginDraggingObject()
            {
                ObjectAdapter.ApplyInputFrame(ObjectScreenPoint, true, false, false, false);
                ObjectAdapter.ApplyInputFrame(DragScreenPoint, false, true, false, false);
                Assert.That(MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
            }

            public TabletopInputFrame CreateFrame(
                Vector2? keyboardPan = null,
                bool dragHeld = false,
                Vector2? pointerDelta = null,
                float scrollDelta = 0f,
                Vector2? screenPosition = null,
                bool selectPressedThisFrame = false,
                bool selectHeld = false,
                bool selectReleasedThisFrame = false,
                bool cancelPressedThisFrame = false,
                float rotateDelta = 0f,
                bool flipPressedThisFrame = false)
            {
                return new TabletopInputFrame(
                    keyboardPan ?? Vector2.zero,
                    dragHeld,
                    pointerDelta ?? Vector2.zero,
                    scrollDelta,
                    screenPosition ?? ObjectScreenPoint,
                    selectPressedThisFrame,
                    selectHeld,
                    selectReleasedThisFrame,
                    cancelPressedThisFrame,
                    rotateDelta,
                    flipPressedThisFrame);
            }

            private Vector2 ScreenPointForWorld(float x, float z)
            {
                Physics.SyncTransforms();
                Vector3 screenPoint = PointerCamera.WorldToScreenPoint(new Vector3(x, 0f, z));
                Assert.That(float.IsFinite(screenPoint.x), Is.True);
                Assert.That(float.IsFinite(screenPoint.y), Is.True);
                return new Vector2(screenPoint.x, screenPoint.y);
            }
        }
    }
}
