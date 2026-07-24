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
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopInputRoutingOrderTests
    {
        private const int InteractionLayer = 8;
        private const float FloatTolerance = 0.0001f;
        private const double CoordinateTolerance = 0.00001d;
        private const float ScrollDelta = 100f;
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

            createdActionReferences.Clear();
            createdInputAssets.Clear();

            for (int i = 0; i < createdInputDevices.Count; i++)
            {
                if (createdInputDevices[i] != null)
                {
                    InputSystem.RemoveDevice(createdInputDevices[i]);
                }
            }

            createdInputDevices.Clear();
            createdGameObjects.Clear();
        }

        [Test]
        public void ObjectPressPlusScroll_WhenCoordinatorSetupOrderVaries_ProducesSameFinalOutcome()
        {
            OrderOutcome cameraFirst = RunScenario(FixtureVariant.CameraAdapterCreatedFirst, fixture =>
            {
                fixture.ApplySharedFrame(fixture.CreateObjectPressScrollFrame());
            });
            OrderOutcome objectFirst = RunScenario(FixtureVariant.ObjectAdapterCreatedFirst, fixture =>
            {
                fixture.ApplySharedFrame(fixture.CreateObjectPressScrollFrame());
            });

            AssertOutcomesEqual(cameraFirst, objectFirst);
            Assert.That(cameraFirst.CameraOrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(objectFirst.CameraOrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(cameraFirst.HasActiveInteraction, Is.True);
            Assert.That(objectFirst.HasActiveInteraction, Is.True);
            Assert.That(cameraFirst.MovePhase, Is.EqualTo(TabletopInteractionPhase.Pressed));
            Assert.That(objectFirst.MovePhase, Is.EqualTo(TabletopInteractionPhase.Pressed));
        }

        [Test]
        public void EmptySpaceClickPlusScroll_WhenCoordinatorSetupOrderVaries_ProducesSameFinalOutcome()
        {
            OrderOutcome cameraFirst = RunScenario(FixtureVariant.CameraAdapterCreatedFirst, fixture =>
            {
                fixture.SelectionState.Select(fixture.View);
                fixture.ApplySharedFrame(fixture.CreateEmptyPressScrollFrame());
            });
            OrderOutcome objectFirst = RunScenario(FixtureVariant.ObjectAdapterCreatedFirst, fixture =>
            {
                fixture.SelectionState.Select(fixture.View);
                fixture.ApplySharedFrame(fixture.CreateEmptyPressScrollFrame());
            });

            AssertOutcomesEqual(cameraFirst, objectFirst);
            Assert.That(cameraFirst.CameraOrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(objectFirst.CameraOrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(cameraFirst.HasSelection, Is.False);
            Assert.That(objectFirst.HasSelection, Is.False);
        }

        [Test]
        public void DraggingPlusScroll_WhenCoordinatorSetupOrderVaries_ProducesSameFinalOutcome()
        {
            OrderOutcome cameraFirst = RunScenario(FixtureVariant.CameraAdapterCreatedFirst, fixture =>
            {
                fixture.BeginDraggingObject();
                fixture.ApplySharedFrame(fixture.CreateDragScrollFrame());
            });
            OrderOutcome objectFirst = RunScenario(FixtureVariant.ObjectAdapterCreatedFirst, fixture =>
            {
                fixture.BeginDraggingObject();
                fixture.ApplySharedFrame(fixture.CreateDragScrollFrame());
            });

            AssertOutcomesEqual(cameraFirst, objectFirst);
            Assert.That(cameraFirst.CameraOrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(objectFirst.CameraOrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
        }

        [Test]
        public void StableSelectedObjectScroll_WhenCoordinatorSetupOrderVaries_ProducesSameFinalOutcome()
        {
            OrderOutcome cameraFirst = RunScenario(FixtureVariant.CameraAdapterCreatedFirst, fixture =>
            {
                fixture.SelectionState.Select(fixture.View);
                fixture.ApplySharedFrame(fixture.CreateStableSelectedScrollFrame());
            });
            OrderOutcome objectFirst = RunScenario(FixtureVariant.ObjectAdapterCreatedFirst, fixture =>
            {
                fixture.SelectionState.Select(fixture.View);
                fixture.ApplySharedFrame(fixture.CreateStableSelectedScrollFrame());
            });

            AssertOutcomesEqual(cameraFirst, objectFirst);
            Assert.That(cameraFirst.CameraOrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(objectFirst.CameraOrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
        }

        [Test]
        public void StableNoSelectionScroll_WhenCoordinatorSetupOrderVaries_ProducesSameFinalOutcome()
        {
            OrderOutcome cameraFirst = RunScenario(FixtureVariant.CameraAdapterCreatedFirst, fixture =>
            {
                fixture.ApplySharedFrame(fixture.CreateStableNoSelectionScrollFrame());
            });
            OrderOutcome objectFirst = RunScenario(FixtureVariant.ObjectAdapterCreatedFirst, fixture =>
            {
                fixture.ApplySharedFrame(fixture.CreateStableNoSelectionScrollFrame());
            });

            AssertOutcomesEqual(cameraFirst, objectFirst);
            Assert.That(cameraFirst.CameraOrthographicSize, Is.EqualTo(4f).Within(FloatTolerance));
            Assert.That(objectFirst.CameraOrthographicSize, Is.EqualTo(4f).Within(FloatTolerance));
        }

        [Test]
        public void Coordinator_WithValidAdapters_AttachesAdaptersAsExternalDrivers()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);

            Assert.That(fixture.CoordinatorComponent.IsInitialized, Is.True);
            Assert.That(fixture.CameraAdapter.IsExternallyDriven, Is.True);
            Assert.That(fixture.ObjectAdapter.IsExternallyDriven, Is.True);
        }

        [Test]
        public void Coordinator_WhenCameraAdapterIsMissing_DisablesAndLogs()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst, createCoordinator: false);
            LogAssert.Expect(LogType.Error, "TabletopInputFrameCoordinator requires a TabletopCameraInputAdapter reference.");

            TabletopInputFrameCoordinator coordinator = CreateCoordinator(null, fixture.ObjectAdapter);

            Assert.That(coordinator.enabled, Is.False);
            Assert.That(coordinator.IsInitialized, Is.False);
            Assert.That(fixture.ObjectAdapter.IsExternallyDriven, Is.False);
        }

        [Test]
        public void Coordinator_WhenObjectAdapterIsMissing_DisablesAndLogs()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst, createCoordinator: false);
            LogAssert.Expect(LogType.Error, "TabletopInputFrameCoordinator requires a TabletopObjectInputAdapter reference.");

            TabletopInputFrameCoordinator coordinator = CreateCoordinator(fixture.CameraAdapter, null);

            Assert.That(coordinator.enabled, Is.False);
            Assert.That(coordinator.IsInitialized, Is.False);
            Assert.That(fixture.CameraAdapter.IsExternallyDriven, Is.False);
        }

        [Test]
        public void AdapterExternalDriverBoundary_WhenSecondDriverAttaches_Throws()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);
            TabletopInputFrameCoordinator secondDriver = CreateInactiveCoordinator(fixture.CameraAdapter, fixture.ObjectAdapter);

            Assert.Throws<InvalidOperationException>(() => fixture.CameraAdapter.AttachExternalFrameDriver(secondDriver));
            Assert.Throws<InvalidOperationException>(() => fixture.ObjectAdapter.AttachExternalFrameDriver(secondDriver));
        }

        [Test]
        public void AdapterExternalDriverBoundary_WhenDifferentDriverDetaches_Throws()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);
            TabletopInputFrameCoordinator secondDriver = CreateInactiveCoordinator(fixture.CameraAdapter, fixture.ObjectAdapter);

            Assert.Throws<InvalidOperationException>(() => fixture.CameraAdapter.DetachExternalFrameDriver(secondDriver));
            Assert.Throws<InvalidOperationException>(() => fixture.ObjectAdapter.DetachExternalFrameDriver(secondDriver));
        }

        [Test]
        public void Coordinator_OnDisable_DetachesAdaptersAndRestoresStandalonePolling()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);
            Mouse mouse = CreateMouseDevice();
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, ScrollDelta) });
            InputSystem.Update();

            InvokeUpdate(fixture.CameraAdapter);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));

            fixture.CoordinatorComponent.enabled = false;
            Assert.That(fixture.CameraAdapter.IsExternallyDriven, Is.False);
            Assert.That(fixture.ObjectAdapter.IsExternallyDriven, Is.False);

            InvokeUpdate(fixture.CameraAdapter);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(4f).Within(FloatTolerance));
        }

        [Test]
        public void Coordinator_OnDestroy_DetachesAdaptersSafely()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);

            UnityObject.DestroyImmediate(fixture.CoordinatorComponent.gameObject);

            Assert.That(fixture.CameraAdapter.IsExternallyDriven, Is.False);
            Assert.That(fixture.ObjectAdapter.IsExternallyDriven, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenPointerTransitionHasKeyboardPan_SuppressesScrollButAppliesKeyboardPan()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);

            fixture.ApplySharedFrame(new TabletopInputFrame(
                new Vector2(1f, 0f),
                false,
                Vector2.zero,
                ScrollDelta,
                fixture.EmptyScreenPoint,
                true,
                false,
                false,
                false));

            AssertCoordinate(fixture.CameraController.State.FocusCoordinate, new TableCoordinate(5.0, 0.0), "Keyboard pan should remain active.");
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void ApplyInputFrame_WhenPointerTransitionHasDragPan_SuppressesScrollButAppliesDragPan()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);

            fixture.ApplySharedFrame(new TabletopInputFrame(
                Vector2.zero,
                true,
                new Vector2(10f, 0f),
                ScrollDelta,
                fixture.EmptyScreenPoint,
                true,
                false,
                false,
                false));

            AssertCoordinate(fixture.CameraController.State.FocusCoordinate, new TableCoordinate(-0.2, 0.0), "Drag pan should remain active.");
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void ApplyInputFrame_WhenPointerTransitionSuppressesScroll_DoesNotMutateRuntimeStateOrRevision()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);
            TabletopPose poseBefore = fixture.State.Pose;

            fixture.ApplySharedFrame(fixture.CreateEmptyPressScrollFrame());

            Assert.That(fixture.State.Pose, Is.EqualTo(poseBefore));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
        }

        [Test]
        public void ApplyInputFrame_WhenStableNoSelectionScrollOccurs_DoesNotMutateRuntimeStateOrRevision()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);
            TabletopPose poseBefore = fixture.State.Pose;

            fixture.ApplySharedFrame(fixture.CreateStableNoSelectionScrollFrame());

            Assert.That(fixture.State.Pose, Is.EqualTo(poseBefore));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(4f).Within(FloatTolerance));
        }

        [Test]
        public void Coordinator_DoesNotReferenceRotateFlipOrPlayerInput()
        {
            OrderFixture fixture = CreateOrderFixture(FixtureVariant.CameraAdapterCreatedFirst);

            Assert.That(fixture.CoordinatorComponent.GetComponent<PlayerInput>(), Is.Null);
            Assert.That(fixture.ObjectAdapter.PointAction, Is.Not.Null);
            Assert.That(fixture.ObjectAdapter.SelectAction, Is.Not.Null);
            Assert.That(fixture.ObjectAdapter.CancelAction, Is.Not.Null);
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.NegativeInfinity)]
        public void TabletopInputFrame_WhenVectorValueIsInvalid_Throws(float x, float y)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopInputFrame(
                new Vector2(x, y),
                false,
                Vector2.zero,
                0f,
                Vector2.zero,
                false,
                false,
                false,
                false));

            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopInputFrame(
                Vector2.zero,
                false,
                new Vector2(x, y),
                0f,
                Vector2.zero,
                false,
                false,
                false,
                false));

            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopInputFrame(
                Vector2.zero,
                false,
                Vector2.zero,
                0f,
                new Vector2(x, y),
                false,
                false,
                false,
                false));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TabletopInputFrame_WhenScrollValueIsInvalid_Throws(float scrollDelta)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopInputFrame(
                Vector2.zero,
                false,
                Vector2.zero,
                scrollDelta,
                Vector2.zero,
                false,
                false,
                false,
                false));
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void TabletopInputFrame_WhenPointerTransitionFlagIsSet_HasPointerTransitionIsTrue(
            bool selectPressedThisFrame,
            bool selectReleasedThisFrame,
            bool cancelPressedThisFrame)
        {
            TabletopInputFrame frame = new TabletopInputFrame(
                Vector2.zero,
                false,
                Vector2.zero,
                0f,
                Vector2.zero,
                selectPressedThisFrame,
                false,
                selectReleasedThisFrame,
                cancelPressedThisFrame);

            Assert.That(frame.HasPointerTransition, Is.True);
        }

        [Test]
        public void TabletopInputFrame_WhenNoPointerTransitionFlagsAreSet_HasPointerTransitionIsFalse()
        {
            TabletopInputFrame frame = new TabletopInputFrame(
                Vector2.zero,
                false,
                Vector2.zero,
                0f,
                Vector2.zero,
                false,
                true,
                false,
                false);

            Assert.That(frame.HasPointerTransition, Is.False);
        }

        private OrderOutcome RunScenario(FixtureVariant variant, Action<OrderFixture> execute)
        {
            OrderFixture fixture = CreateOrderFixture(variant);
            execute(fixture);
            return OrderOutcome.Capture(variant, fixture);
        }

        private OrderFixture CreateOrderFixture(FixtureVariant variant, bool createCoordinator = true)
        {
            TabletopCoordinateConverter converter = CreateConverter();
            UnityEngine.Camera targetCamera = CreateTopDownCamera("Routing Order Camera");
            CardView view = CreateCardView(converter, out TabletopObjectState state);
            AddBoxCollider(view.gameObject, InteractionLayer);
            Physics.SyncTransforms();

            CardInstanceState cardState = view.CardState;
            MatchState match = CreateMatch(0, new[] { cardState });
            TabletopSelectionState selectionState = new TabletopSelectionState();
            TabletopObjectHitResolver hitResolver = new TabletopObjectHitResolver(
                targetCamera,
                LayerMaskFor(InteractionLayer),
                25f);
            TabletopPointerProjector pointerProjector = new TabletopPointerProjector(targetCamera, converter, 0f);
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(5f);
            TabletopDragPreviewSession previewSession = new TabletopDragPreviewSession();
            TabletopMoveInteractionCoordinator moveCoordinator = new TabletopMoveInteractionCoordinator(
                match,
                PlayerId.New(),
                InteractionOwnerId.New(),
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                stateMachine,
                previewSession,
                new MoveObjectUseCase());
            TabletopInteractionInputRoutingPolicy routingPolicy = new TabletopInteractionInputRoutingPolicy(
                selectionState,
                moveCoordinator);

            TabletopCameraController cameraController = CreateInitializedCameraController(targetCamera);
            TabletopCameraInputAdapter cameraAdapter;
            TabletopObjectInputAdapter objectAdapter;
            if (variant == FixtureVariant.CameraAdapterCreatedFirst)
            {
                cameraAdapter = CreateInitializedCameraAdapter(cameraController);
                objectAdapter = CreateInitializedObjectAdapter(moveCoordinator);
            }
            else
            {
                objectAdapter = CreateInitializedObjectAdapter(moveCoordinator);
                cameraAdapter = CreateInitializedCameraAdapter(cameraController);
            }

            cameraAdapter.ConfigureScrollRoutingPolicy(routingPolicy);
            TabletopInputFrameCoordinator frameCoordinator = createCoordinator
                ? CreateCoordinator(cameraAdapter, objectAdapter)
                : null;

            return new OrderFixture(
                variant,
                cameraController,
                cameraAdapter,
                objectAdapter,
                frameCoordinator,
                routingPolicy,
                moveCoordinator,
                match,
                selectionState,
                lockService,
                view,
                state,
                cardState,
                targetCamera);
        }

        private TabletopInputFrameCoordinator CreateCoordinator(
            TabletopCameraInputAdapter cameraAdapter,
            TabletopObjectInputAdapter objectAdapter)
        {
            GameObject coordinatorObject = CreateGameObject("Tabletop Input Frame Coordinator");
            coordinatorObject.SetActive(false);
            TabletopInputFrameCoordinator coordinator = coordinatorObject.AddComponent<TabletopInputFrameCoordinator>();
            coordinator.cameraInputAdapter = cameraAdapter;
            coordinator.objectInputAdapter = objectAdapter;
            coordinatorObject.SetActive(true);
            return coordinator;
        }

        private TabletopInputFrameCoordinator CreateInactiveCoordinator(
            TabletopCameraInputAdapter cameraAdapter,
            TabletopObjectInputAdapter objectAdapter)
        {
            GameObject coordinatorObject = CreateGameObject("Inactive Tabletop Input Frame Coordinator");
            coordinatorObject.SetActive(false);
            TabletopInputFrameCoordinator coordinator = coordinatorObject.AddComponent<TabletopInputFrameCoordinator>();
            coordinator.cameraInputAdapter = cameraAdapter;
            coordinator.objectInputAdapter = objectAdapter;
            return coordinator;
        }

        private TabletopCameraController CreateInitializedCameraController(UnityEngine.Camera targetCamera)
        {
            Transform cameraRig = CreateGameObject("Routing Order Camera Rig").transform;
            GameObject controllerObject = CreateGameObject("Routing Order Camera Controller");
            controllerObject.SetActive(false);

            TabletopCameraController controller = controllerObject.AddComponent<TabletopCameraController>();
            controller.targetCamera = targetCamera;
            controller.cameraRig = cameraRig;

            controllerObject.SetActive(true);
            return controller;
        }

        private TabletopCameraInputAdapter CreateInitializedCameraAdapter(TabletopCameraController controller)
        {
            InputActionMap actionMap = CreateActionMap("RoutingOrderCamera");
            InputActionReference keyboardPanAction = CreateActionReference(actionMap, "KeyboardPan", InputActionType.Value, "Vector2");
            InputActionReference dragPanAction = CreateActionReference(actionMap, "DragPan", InputActionType.Button, "Button");
            InputActionReference pointerDeltaAction = CreateActionReference(actionMap, "PointerDelta", InputActionType.PassThrough, "Vector2");
            InputActionReference zoomAction = CreateActionReference(actionMap, "Zoom", InputActionType.PassThrough, "Axis");

            GameObject adapterObject = CreateGameObject("Routing Order Camera Input Adapter");
            adapterObject.SetActive(false);
            TabletopCameraInputAdapter adapter = adapterObject.AddComponent<TabletopCameraInputAdapter>();
            adapter.cameraController = controller;
            adapter.keyboardPanAction = keyboardPanAction;
            adapter.dragPanAction = dragPanAction;
            adapter.pointerDeltaAction = pointerDeltaAction;
            adapter.zoomAction = zoomAction;
            adapter.keyboardPanSpeed = 5f;
            adapter.dragPanUnitsPerPixel = 0.02f;
            adapter.zoomSensitivity = 0.01f;

            adapterObject.SetActive(true);
            return adapter;
        }

        private TabletopObjectInputAdapter CreateInitializedObjectAdapter(TabletopMoveInteractionCoordinator moveCoordinator)
        {
            InputActionMap actionMap = CreateActionMap("RoutingOrderObject");
            InputActionReference pointAction = CreateActionReference(actionMap, "Point", InputActionType.PassThrough, "Vector2");
            InputActionReference selectAction = CreateActionReference(actionMap, "Select", InputActionType.Button, "Button");
            InputActionReference cancelAction = CreateActionReference(actionMap, "Cancel", InputActionType.Button, "Button");

            GameObject adapterObject = CreateGameObject("Routing Order Object Input Adapter");
            adapterObject.SetActive(false);
            TabletopObjectInputAdapter adapter = adapterObject.AddComponent<TabletopObjectInputAdapter>();
            adapter.pointAction = pointAction;
            adapter.selectAction = selectAction;
            adapter.cancelAction = cancelAction;
            adapterObject.SetActive(true);
            adapter.Initialize(moveCoordinator);
            return adapter;
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

        private CardView CreateCardView(TabletopCoordinateConverter converter, out TabletopObjectState state)
        {
            CardView view = CreateView<CardView>();
            state = CreateBaseState(
                TabletopObjectKind.Card,
                1,
                TabletopPose.Default,
                false);
            view.Bind(new CardInstanceState(state, CardFace.FaceUp), converter);
            return view;
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
            }
        }

        private Mouse CreateMouseDevice()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            createdInputDevices.Add(mouse);
            return mouse;
        }

        private static void InvokeUpdate(MonoBehaviour behaviour)
        {
            MethodInfo updateMethod = behaviour.GetType().GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updateMethod, Is.Not.Null);
            updateMethod.Invoke(behaviour, null);
        }

        private static MatchState CreateMatch(long revision, CardInstanceState[] cards)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
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

        private static BoxCollider AddBoxCollider(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
            return collider;
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void AssertOutcomesEqual(OrderOutcome cameraFirst, OrderOutcome objectFirst)
        {
            Assert.That(cameraFirst.Variant, Is.EqualTo(FixtureVariant.CameraAdapterCreatedFirst));
            Assert.That(objectFirst.Variant, Is.EqualTo(FixtureVariant.ObjectAdapterCreatedFirst));

            Assert.That(
                cameraFirst.CameraOrthographicSize,
                Is.EqualTo(objectFirst.CameraOrthographicSize).Within(FloatTolerance),
                FormatDivergence("Camera orthographic size", cameraFirst, objectFirst));
            AssertCoordinate(cameraFirst.CameraFocus, objectFirst.CameraFocus, FormatDivergence("Camera focus", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.HasSelection,
                Is.EqualTo(objectFirst.HasSelection),
                FormatDivergence("Selection presence", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.SelectedObjectId,
                Is.EqualTo(objectFirst.SelectedObjectId),
                FormatDivergence("Selected object", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.HasActiveInteraction,
                Is.EqualTo(objectFirst.HasActiveInteraction),
                FormatDivergence("Active interaction", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.MovePhase,
                Is.EqualTo(objectFirst.MovePhase),
                FormatDivergence("Move-interaction phase", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.LockCount,
                Is.EqualTo(objectFirst.LockCount),
                FormatDivergence("Lock count", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.FinalScrollRoute,
                Is.EqualTo(objectFirst.FinalScrollRoute),
                FormatDivergence("Final scroll route", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.MatchRevision,
                Is.EqualTo(objectFirst.MatchRevision),
                FormatDivergence("Match revision", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.ObjectId,
                Is.EqualTo(objectFirst.ObjectId),
                FormatDivergence("Runtime object id", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.ObjectDefinitionId,
                Is.EqualTo(objectFirst.ObjectDefinitionId),
                FormatDivergence("Runtime object definition id", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.ObjectKind,
                Is.EqualTo(objectFirst.ObjectKind),
                FormatDivergence("Runtime object kind", cameraFirst, objectFirst));
            AssertPose(cameraFirst.ObjectPose, objectFirst.ObjectPose, FormatDivergence("Runtime object pose", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.ContainerId,
                Is.EqualTo(objectFirst.ContainerId),
                FormatDivergence("Runtime object container", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.OwnerPlayerId,
                Is.EqualTo(objectFirst.OwnerPlayerId),
                FormatDivergence("Runtime object owner", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.Visibility,
                Is.EqualTo(objectFirst.Visibility),
                FormatDivergence("Runtime object visibility", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.IsUserLocked,
                Is.EqualTo(objectFirst.IsUserLocked),
                FormatDivergence("Runtime object user lock", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.CardFace,
                Is.EqualTo(objectFirst.CardFace),
                FormatDivergence("Runtime card face", cameraFirst, objectFirst));
            Assert.That(
                cameraFirst.ViewIsPreviewing,
                Is.EqualTo(objectFirst.ViewIsPreviewing),
                FormatDivergence("View preview state", cameraFirst, objectFirst));
            AssertPose(cameraFirst.PreviewPose, objectFirst.PreviewPose, FormatDivergence("View preview pose", cameraFirst, objectFirst));
        }

        private static void AssertCoordinate(TableCoordinate actual, TableCoordinate expected, string message)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(CoordinateTolerance), message);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(CoordinateTolerance), message);
        }

        private static void AssertPose(TabletopPose actual, TabletopPose expected, string message)
        {
            AssertCoordinate(actual.Position, expected.Position, message);
            Assert.That(actual.RotationDegrees, Is.EqualTo(expected.RotationDegrees).Within(FloatTolerance), message);
            Assert.That(actual.Layer, Is.EqualTo(expected.Layer), message);
            Assert.That(actual.LocalOrder, Is.EqualTo(expected.LocalOrder), message);
        }

        private static string FormatDivergence(string field, OrderOutcome cameraFirst, OrderOutcome objectFirst)
        {
            return field
                + " diverged between same-frame input orders."
                + Environment.NewLine
                + "Camera-first: "
                + cameraFirst
                + Environment.NewLine
                + "Object-first: "
                + objectFirst;
        }

        private enum FixtureVariant
        {
            CameraAdapterCreatedFirst,
            ObjectAdapterCreatedFirst
        }

        private sealed class OrderFixture
        {
            public OrderFixture(
                FixtureVariant variant,
                TabletopCameraController cameraController,
                TabletopCameraInputAdapter cameraAdapter,
                TabletopObjectInputAdapter objectAdapter,
                TabletopInputFrameCoordinator coordinatorComponent,
                TabletopInteractionInputRoutingPolicy routingPolicy,
                TabletopMoveInteractionCoordinator moveCoordinator,
                MatchState match,
                TabletopSelectionState selectionState,
                LocalInteractionLockService lockService,
                CardView view,
                TabletopObjectState state,
                CardInstanceState cardState,
                UnityEngine.Camera pointerCamera)
            {
                Variant = variant;
                CameraController = cameraController;
                CameraAdapter = cameraAdapter;
                ObjectAdapter = objectAdapter;
                CoordinatorComponent = coordinatorComponent;
                RoutingPolicy = routingPolicy;
                MoveCoordinator = moveCoordinator;
                Match = match;
                SelectionState = selectionState;
                LockService = lockService;
                View = view;
                State = state;
                CardState = cardState;
                PointerCamera = pointerCamera;
                ObjectScreenPoint = ScreenPointForWorld(0f, 0f);
                EmptyScreenPoint = ScreenPointForWorld(7f, 7f);
                DragScreenPoint = ScreenPointForWorld(2f, 0f);
            }

            public FixtureVariant Variant { get; }

            public TabletopCameraController CameraController { get; }

            public TabletopCameraInputAdapter CameraAdapter { get; }

            public TabletopObjectInputAdapter ObjectAdapter { get; }

            public TabletopInputFrameCoordinator CoordinatorComponent { get; }

            public TabletopInteractionInputRoutingPolicy RoutingPolicy { get; }

            public TabletopMoveInteractionCoordinator MoveCoordinator { get; }

            public MatchState Match { get; }

            public TabletopSelectionState SelectionState { get; }

            public LocalInteractionLockService LockService { get; }

            public CardView View { get; }

            public TabletopObjectState State { get; }

            public CardInstanceState CardState { get; }

            public UnityEngine.Camera PointerCamera { get; }

            public Vector2 ObjectScreenPoint { get; }

            public Vector2 EmptyScreenPoint { get; }

            public Vector2 DragScreenPoint { get; }

            public void ApplySharedFrame(TabletopInputFrame frame)
            {
                CoordinatorComponent.ApplyInputFrame(frame, DeltaTime);
            }

            public TabletopInputFrame CreateObjectPressScrollFrame()
            {
                return CreateFrame(ObjectScreenPoint, selectPressedThisFrame: true);
            }

            public TabletopInputFrame CreateEmptyPressScrollFrame()
            {
                return CreateFrame(EmptyScreenPoint, selectPressedThisFrame: true);
            }

            public TabletopInputFrame CreateDragScrollFrame()
            {
                return CreateFrame(DragScreenPoint, selectHeld: true);
            }

            public TabletopInputFrame CreateStableSelectedScrollFrame()
            {
                return CreateFrame(ObjectScreenPoint);
            }

            public TabletopInputFrame CreateStableNoSelectionScrollFrame()
            {
                return CreateFrame(EmptyScreenPoint);
            }

            public void BeginDraggingObject()
            {
                ObjectAdapter.ApplyInputFrame(ObjectScreenPoint, true, false, false, false);
                ObjectAdapter.ApplyInputFrame(DragScreenPoint, false, true, false, false);
                Assert.That(MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
                Assert.That(RoutingPolicy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.Suppressed));
            }

            private static TabletopInputFrame CreateFrame(
                Vector2 screenPosition,
                bool selectPressedThisFrame = false,
                bool selectHeld = false,
                bool selectReleasedThisFrame = false,
                bool cancelPressedThisFrame = false)
            {
                return new TabletopInputFrame(
                    Vector2.zero,
                    false,
                    Vector2.zero,
                    ScrollDelta,
                    screenPosition,
                    selectPressedThisFrame,
                    selectHeld,
                    selectReleasedThisFrame,
                    cancelPressedThisFrame);
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

        private sealed class OrderOutcome
        {
            private OrderOutcome(
                FixtureVariant variant,
                float cameraOrthographicSize,
                TableCoordinate cameraFocus,
                bool hasSelection,
                TabletopObjectId selectedObjectId,
                bool hasActiveInteraction,
                TabletopInteractionPhase movePhase,
                int lockCount,
                TabletopScrollInputRoute finalScrollRoute,
                long matchRevision,
                TabletopObjectId objectId,
                ObjectDefinitionId objectDefinitionId,
                TabletopObjectKind objectKind,
                TabletopPose objectPose,
                ContainerId containerId,
                PlayerId ownerPlayerId,
                ObjectVisibility visibility,
                bool isUserLocked,
                CardFace cardFace,
                bool viewIsPreviewing,
                TabletopPose previewPose)
            {
                Variant = variant;
                CameraOrthographicSize = cameraOrthographicSize;
                CameraFocus = cameraFocus;
                HasSelection = hasSelection;
                SelectedObjectId = selectedObjectId;
                HasActiveInteraction = hasActiveInteraction;
                MovePhase = movePhase;
                LockCount = lockCount;
                FinalScrollRoute = finalScrollRoute;
                MatchRevision = matchRevision;
                ObjectId = objectId;
                ObjectDefinitionId = objectDefinitionId;
                ObjectKind = objectKind;
                ObjectPose = objectPose;
                ContainerId = containerId;
                OwnerPlayerId = ownerPlayerId;
                Visibility = visibility;
                IsUserLocked = isUserLocked;
                CardFace = cardFace;
                ViewIsPreviewing = viewIsPreviewing;
                PreviewPose = previewPose;
            }

            public FixtureVariant Variant { get; }

            public float CameraOrthographicSize { get; }

            public TableCoordinate CameraFocus { get; }

            public bool HasSelection { get; }

            public TabletopObjectId SelectedObjectId { get; }

            public bool HasActiveInteraction { get; }

            public TabletopInteractionPhase MovePhase { get; }

            public int LockCount { get; }

            public TabletopScrollInputRoute FinalScrollRoute { get; }

            public long MatchRevision { get; }

            public TabletopObjectId ObjectId { get; }

            public ObjectDefinitionId ObjectDefinitionId { get; }

            public TabletopObjectKind ObjectKind { get; }

            public TabletopPose ObjectPose { get; }

            public ContainerId ContainerId { get; }

            public PlayerId OwnerPlayerId { get; }

            public ObjectVisibility Visibility { get; }

            public bool IsUserLocked { get; }

            public CardFace CardFace { get; }

            public bool ViewIsPreviewing { get; }

            public TabletopPose PreviewPose { get; }

            public static OrderOutcome Capture(FixtureVariant variant, OrderFixture fixture)
            {
                TabletopObjectState state = fixture.State;
                return new OrderOutcome(
                    variant,
                    fixture.CameraController.State.OrthographicSize,
                    fixture.CameraController.State.FocusCoordinate,
                    fixture.SelectionState.HasSelection,
                    fixture.SelectionState.SelectedObjectId,
                    fixture.MoveCoordinator.HasActiveInteraction,
                    fixture.MoveCoordinator.Phase,
                    fixture.LockService.Count,
                    fixture.RoutingPolicy.ResolveScrollRoute(),
                    fixture.Match.Revision,
                    state.Id,
                    state.DefinitionId,
                    state.Kind,
                    state.Pose,
                    state.ContainerId,
                    state.OwnerPlayerId,
                    state.Visibility,
                    state.IsUserLocked,
                    fixture.CardState.Face,
                    fixture.View.IsPreviewing,
                    fixture.View.PreviewPose);
            }

            public override string ToString()
            {
                return $"Variant={Variant}, CameraSize={CameraOrthographicSize}, CameraFocus=({CameraFocus.X}, {CameraFocus.Y}), "
                    + $"HasSelection={HasSelection}, SelectedObjectId={SelectedObjectId}, HasActiveInteraction={HasActiveInteraction}, "
                    + $"MovePhase={MovePhase}, LockCount={LockCount}, FinalScrollRoute={FinalScrollRoute}, "
                    + $"MatchRevision={MatchRevision}, ObjectPose={FormatPose(ObjectPose)}, "
                    + $"ObjectKind={ObjectKind}, ContainerId={ContainerId}, OwnerPlayerId={OwnerPlayerId}, Visibility={Visibility}, "
                    + $"IsUserLocked={IsUserLocked}, CardFace={CardFace}, ViewIsPreviewing={ViewIsPreviewing}, PreviewPose={FormatPose(PreviewPose)}";
            }

            private static string FormatPose(TabletopPose pose)
            {
                return $"({pose.Position.X}, {pose.Position.Y}, rot={pose.RotationDegrees}, layer={pose.Layer}, order={pose.LocalOrder})";
            }
        }
    }
}
