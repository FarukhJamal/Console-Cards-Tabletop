using System;
using System.Collections.Generic;
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
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopInputFrameSelectionPresentationTests
    {
        private const int InteractionLayer = 8;
        private const float FloatTolerance = 0.0001f;
        private const float DeltaTime = 1f;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();
        private readonly List<InputActionAsset> createdInputAssets = new List<InputActionAsset>();
        private readonly List<InputActionReference> createdActionReferences = new List<InputActionReference>();

        [TearDown]
        public void TearDown()
        {
            ShutdownRuntimeComponents();

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
            createdGameObjects.Clear();
        }

        [Test]
        public void Coordinator_BeginsWithoutSelectionPresenter()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: false);

            Assert.That(fixture.FrameCoordinator.HasSelectionPresenter, Is.False);
            Assert.That(fixture.FrameCoordinator.SelectionPresenter, Is.Null);
        }

        [Test]
        public void ConfigureSelectionPresenter_WhenValid_StoresPresenterWithoutImmediateMutation()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: false);
            fixture.ConfigurePresenter();

            Assert.That(fixture.FrameCoordinator.HasSelectionPresenter, Is.True);
            Assert.That(fixture.FrameCoordinator.SelectionPresenter, Is.SameAs(fixture.Presenter));
            fixture.AssertAllHighlightsInactive();
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void ConfigureSelectionPresenter_WhenInvalid_RejectsNullAndDuplicatePresenter()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);

            Assert.Throws<ArgumentNullException>(() => fixture.FrameCoordinator.ConfigureSelectionPresenter(null));
            Assert.Throws<InvalidOperationException>(
                () => fixture.FrameCoordinator.ConfigureSelectionPresenter(fixture.Presenter));
        }

        [Test]
        public void ClearSelectionPresenter_DisablesHighlightsAndIsIdempotent()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);
            fixture.SelectionState.Select(fixture.CardView);
            fixture.Presenter.Refresh();
            Assert.That(fixture.CardHighlight.activeSelf, Is.True);

            fixture.FrameCoordinator.ClearSelectionPresenter();
            fixture.FrameCoordinator.ClearSelectionPresenter();

            Assert.That(fixture.FrameCoordinator.HasSelectionPresenter, Is.False);
            Assert.That(fixture.FrameCoordinator.SelectionPresenter, Is.Null);
            fixture.AssertAllHighlightsInactive();
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
        }

        [Test]
        public void ApplyInputFrame_WhenNoPresenterConfigured_PreservesInputBehaviorWithoutHighlightMutation()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: false);

            fixture.ApplyFrame(fixture.CreatePressFrame(fixture.CardView));

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.True);
            fixture.AssertAllHighlightsInactive();
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void ApplyInputFrame_WithPresenter_RefreshesSelectionExactlyOnce()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);
            DisableViewWhenEnabled disableViewWhenEnabled =
                fixture.CardHighlight.AddComponent<DisableViewWhenEnabled>();
            disableViewWhenEnabled.TargetView = fixture.CardView;

            fixture.ApplyFrame(fixture.CreatePressFrame(fixture.CardView));

            Assert.That(disableViewWhenEnabled.EnableCount, Is.EqualTo(1));
            Assert.That(fixture.CardHighlight.activeSelf, Is.True);
            Assert.That(fixture.CardView.enabled, Is.False);
        }

        [TestCase(TabletopObjectKind.Card)]
        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void ApplyInputFrame_WhenObjectIsPressed_HighlightsSelectionInSameFrame(TabletopObjectKind kind)
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);
            TabletopObjectView view = fixture.ViewFor(kind);

            fixture.ApplyFrame(fixture.CreatePressFrame(view));

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(view));
            fixture.AssertOnlyHighlightActive(kind);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void ApplyInputFrame_WhenEmptySpaceIsPressed_ClearsSelectionAndHighlightInSameFrame()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);
            fixture.SelectionState.Select(fixture.CardView);
            fixture.Presenter.Refresh();

            fixture.ApplyFrame(fixture.CreateEmptyPressFrame());

            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            fixture.AssertAllHighlightsInactive();
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void ApplyInputFrame_WhenSelectionIsReplaced_UpdatesHighlightInSameFrame()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);
            fixture.SelectionState.Select(fixture.CardView);
            fixture.Presenter.Refresh();

            fixture.ApplyFrame(fixture.CreatePressFrame(fixture.PawnView));

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.PawnView));
            fixture.AssertOnlyHighlightActive(TabletopObjectKind.Pawn);
        }

        [Test]
        public void ApplyInputFrame_WhenPointerTransitionHasScroll_PreservesPanAndSuppressesZoom()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);

            fixture.ApplyFrame(new TabletopInputFrame(
                new Vector2(1f, 0f),
                false,
                Vector2.zero,
                100f,
                fixture.EmptyScreenPoint,
                true,
                false,
                false,
                false,
                100f,
                false));

            Assert.That(fixture.CameraController.State.FocusCoordinate.X, Is.EqualTo(5d).Within(0.00001d));
            Assert.That(fixture.CameraController.State.FocusCoordinate.Y, Is.EqualTo(0d).Within(0.00001d));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            fixture.AssertAllHighlightsInactive();
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [TestCase(DiscreteAction.Rotate)]
        [TestCase(DiscreteAction.Flip)]
        public void ApplyInputFrame_WhenPresenterConfigured_PreservesDiscreteObjectInputBehavior(DiscreteAction action)
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);
            fixture.SelectionState.Select(fixture.CardView);

            fixture.ApplyFrame(action == DiscreteAction.Rotate
                ? fixture.CreateStableScrollFrame(fixture.CardView)
                : fixture.CreateFlipFrame(fixture.CardView));

            fixture.AssertOnlyHighlightActive(TabletopObjectKind.Card);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));

            if (action == DiscreteAction.Rotate)
            {
                Assert.That(fixture.CardState.BaseState.Pose.RotationDegrees, Is.EqualTo(15f).Within(FloatTolerance));
                Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.True);
                Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            }
            else
            {
                Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceDown));
                Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.True);
                Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            }
        }

        [Test]
        public void ApplyInputFrame_WhenOnlyHighlightRefreshOccurs_DoesNotMutateRuntimeState()
        {
            FrameFixture fixture = CreateFixture(configurePresenter: true);
            fixture.SelectionState.Select(fixture.TokenView);
            TabletopPose cardPose = fixture.CardState.BaseState.Pose;
            TabletopPose pawnPose = fixture.PawnState.BaseState.Pose;
            TabletopPose tokenPose = fixture.TokenState.BaseState.Pose;

            fixture.ApplyFrame(fixture.CreateStableNoInputFrame(fixture.TokenView));

            fixture.AssertOnlyHighlightActive(TabletopObjectKind.Token);
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.CardState.BaseState.Pose, Is.EqualTo(cardPose));
            Assert.That(fixture.PawnState.BaseState.Pose, Is.EqualTo(pawnPose));
            Assert.That(fixture.TokenState.BaseState.Pose, Is.EqualTo(tokenPose));
        }

        private FrameFixture CreateFixture(bool configurePresenter)
        {
            InputGraph inputGraph = CreateCompleteInputGraph();

            GameObject cameraRigObject = CreateGameObject("Frame Selection Camera Rig");
            cameraRigObject.SetActive(false);
            GameObject cameraObject = CreateGameObject("Frame Selection Camera");
            cameraObject.transform.SetParent(cameraRigObject.transform, false);
            Camera targetCamera = cameraObject.AddComponent<Camera>();
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = 5f;
            targetCamera.nearClipPlane = 0.1f;
            targetCamera.farClipPlane = 100f;
            targetCamera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            TabletopCameraController cameraController = cameraRigObject.AddComponent<TabletopCameraController>();
            cameraController.targetCamera = targetCamera;
            cameraController.cameraRig = cameraRigObject.transform;
            cameraController.initialOrthographicSize = 5f;
            cameraController.minimumOrthographicSize = 2f;
            cameraController.maximumOrthographicSize = 20f;
            cameraController.worldUnitsPerTableUnit = 1f;
            cameraController.cameraHeight = 10f;
            cameraRigObject.SetActive(true);

            TabletopCameraInputAdapter cameraAdapter = CreateCameraAdapter(cameraController, inputGraph.CameraActions);
            TabletopObjectInputAdapter objectAdapter = CreateObjectAdapter(inputGraph.ObjectActions);

            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
            CardInstanceState cardState = new CardInstanceState(
                CreateBaseState(TabletopObjectKind.Card, 10, new TabletopPose(new TableCoordinate(-2d, 0d), 0f, 0, 0)),
                CardFace.FaceUp);
            PawnState pawnState = new PawnState(
                CreateBaseState(TabletopObjectKind.Pawn, 11, new TabletopPose(new TableCoordinate(0d, 0d), 0f, 0, 0)));
            TokenState tokenState = new TokenState(
                CreateBaseState(TabletopObjectKind.Token, 12, new TabletopPose(new TableCoordinate(2d, 0d), 0f, 0, 0)));
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.Empty,
                0,
                new[] { cardState },
                new[] { pawnState },
                new[] { tokenState },
                Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());

            CardView cardView = CreateView<CardView>("Frame Selection Card");
            PawnView pawnView = CreateView<PawnView>("Frame Selection Pawn");
            TokenView tokenView = CreateView<TokenView>("Frame Selection Token");
            AddBoxCollider(cardView.gameObject, InteractionLayer);
            AddBoxCollider(pawnView.gameObject, InteractionLayer);
            AddBoxCollider(tokenView.gameObject, InteractionLayer);
            cardView.Bind(cardState, converter);
            pawnView.Bind(pawnState, converter);
            tokenView.Bind(tokenState, converter);

            TabletopSelectionVisual cardVisual = cardView.gameObject.AddComponent<TabletopSelectionVisual>();
            TabletopSelectionVisual pawnVisual = pawnView.gameObject.AddComponent<TabletopSelectionVisual>();
            TabletopSelectionVisual tokenVisual = tokenView.gameObject.AddComponent<TabletopSelectionVisual>();
            GameObject cardHighlight = CreateChild(cardView.gameObject, "Card Highlight");
            GameObject pawnHighlight = CreateChild(pawnView.gameObject, "Pawn Highlight");
            GameObject tokenHighlight = CreateChild(tokenView.gameObject, "Token Highlight");
            cardVisual.Configure(cardView, cardHighlight);
            pawnVisual.Configure(pawnView, pawnHighlight);
            tokenVisual.Configure(tokenView, tokenHighlight);

            TabletopSelectionState selectionState = new TabletopSelectionState();
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            TabletopMoveInteractionCoordinator moveCoordinator = new TabletopMoveInteractionCoordinator(
                match,
                PlayerId.New(),
                InteractionOwnerId.New(),
                selectionState,
                new TabletopObjectHitResolver(targetCamera, LayerMaskFor(InteractionLayer), 100f),
                new TabletopPointerProjector(targetCamera, converter, 0f),
                lockService,
                new TabletopInteractionStateMachine(8f),
                new TabletopDragPreviewSession(),
                new MoveObjectUseCase());
            TabletopInteractionInputRoutingPolicy routingPolicy =
                new TabletopInteractionInputRoutingPolicy(selectionState, moveCoordinator);
            cameraAdapter.ConfigureScrollRoutingPolicy(routingPolicy);
            objectAdapter.Initialize(
                moveCoordinator,
                new TabletopRotationCoordinator(
                    match,
                    moveCoordinator.RequestedByPlayerId,
                    moveCoordinator.InteractionOwnerId,
                    selectionState,
                    lockService,
                    new RotateObjectUseCase()),
                new TabletopCardFlipCoordinator(
                    match,
                    moveCoordinator.RequestedByPlayerId,
                    moveCoordinator.InteractionOwnerId,
                    selectionState,
                    lockService,
                    new FlipCardUseCase()),
                routingPolicy);

            TabletopInputFrameCoordinator frameCoordinator = CreateFrameCoordinator(cameraAdapter, objectAdapter);
            TabletopSelectionPresenter presenter = new TabletopSelectionPresenter(
                selectionState,
                cardVisual,
                pawnVisual,
                tokenVisual);

            if (configurePresenter)
            {
                frameCoordinator.ConfigureSelectionPresenter(presenter);
            }

            return new FrameFixture(
                match,
                cardState,
                pawnState,
                tokenState,
                cardView,
                pawnView,
                tokenView,
                cardHighlight,
                pawnHighlight,
                tokenHighlight,
                selectionState,
                moveCoordinator,
                cameraController,
                objectAdapter,
                frameCoordinator,
                presenter,
                targetCamera);
        }

        private TabletopCameraInputAdapter CreateCameraAdapter(
            TabletopCameraController cameraController,
            CameraActions cameraActions)
        {
            GameObject adapterObject = CreateGameObject("Frame Selection Camera Adapter");
            adapterObject.SetActive(false);
            TabletopCameraInputAdapter adapter = adapterObject.AddComponent<TabletopCameraInputAdapter>();
            adapter.cameraController = cameraController;
            adapter.keyboardPanAction = cameraActions.KeyboardPan;
            adapter.dragPanAction = cameraActions.DragPan;
            adapter.pointerDeltaAction = cameraActions.PointerDelta;
            adapter.zoomAction = cameraActions.Zoom;
            adapterObject.SetActive(true);
            return adapter;
        }

        private TabletopObjectInputAdapter CreateObjectAdapter(ObjectActions objectActions)
        {
            GameObject adapterObject = CreateGameObject("Frame Selection Object Adapter");
            adapterObject.SetActive(false);
            TabletopObjectInputAdapter adapter = adapterObject.AddComponent<TabletopObjectInputAdapter>();
            adapter.pointAction = objectActions.Point;
            adapter.selectAction = objectActions.Select;
            adapter.cancelAction = objectActions.Cancel;
            adapter.rotateAction = objectActions.Rotate;
            adapter.flipAction = objectActions.Flip;
            adapter.rotationStepDegrees = 15f;
            adapterObject.SetActive(true);
            return adapter;
        }

        private TabletopInputFrameCoordinator CreateFrameCoordinator(
            TabletopCameraInputAdapter cameraAdapter,
            TabletopObjectInputAdapter objectAdapter)
        {
            GameObject coordinatorObject = CreateGameObject("Frame Selection Coordinator");
            coordinatorObject.SetActive(false);
            TabletopInputFrameCoordinator coordinator = coordinatorObject.AddComponent<TabletopInputFrameCoordinator>();
            coordinator.cameraInputAdapter = cameraAdapter;
            coordinator.objectInputAdapter = objectAdapter;
            coordinatorObject.SetActive(true);
            return coordinator;
        }

        private T CreateView<T>(string name)
            where T : TabletopObjectView
        {
            GameObject gameObject = CreateGameObject(name);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateChild(GameObject parent, string name)
        {
            GameObject child = CreateGameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private InputActionReference CreateActionReference(
            InputActionMap actionMap,
            string actionName,
            InputActionType actionType,
            string expectedControlType)
        {
            InputAction action = actionMap.AddAction(actionName, actionType, expectedControlLayout: expectedControlType);
            AddRequiredBinding(action, actionName);
            InputActionReference actionReference = InputActionReference.Create(action);
            createdActionReferences.Add(actionReference);
            return actionReference;
        }

        private static void AddRequiredBinding(InputAction action, string actionName)
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
                    action.AddBinding("<Pointer>/delta");
                    break;
                case "Zoom":
                case "Rotate":
                    action.AddBinding("<Mouse>/scroll/y");
                    break;
                case "Point":
                    action.AddBinding("<Pointer>/position");
                    break;
                case "Select":
                    action.AddBinding("<Mouse>/leftButton");
                    break;
                case "Cancel":
                    action.AddBinding("<Keyboard>/escape");
                    break;
                case "Flip":
                    action.AddBinding("<Keyboard>/f");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(actionName), actionName, "Unsupported input action.");
            }
        }

        private InputGraph CreateCompleteInputGraph()
        {
            InputActionAsset inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            createdInputAssets.Add(inputActionAsset);
            InputActionMap cameraMap = inputActionAsset.AddActionMap("FrameSelectionCamera");
            InputActionMap objectMap = inputActionAsset.AddActionMap("FrameSelectionObject");

            CameraActions cameraActions = new CameraActions(
                CreateActionReference(cameraMap, "KeyboardPan", InputActionType.Value, "Vector2"),
                CreateActionReference(cameraMap, "DragPan", InputActionType.Button, "Button"),
                CreateActionReference(cameraMap, "PointerDelta", InputActionType.PassThrough, "Vector2"),
                CreateActionReference(cameraMap, "Zoom", InputActionType.PassThrough, "Axis"));
            ObjectActions objectActions = new ObjectActions(
                CreateActionReference(objectMap, "Point", InputActionType.PassThrough, "Vector2"),
                CreateActionReference(objectMap, "Select", InputActionType.Button, "Button"),
                CreateActionReference(objectMap, "Cancel", InputActionType.Button, "Button"),
                CreateActionReference(objectMap, "Rotate", InputActionType.PassThrough, "Axis"),
                CreateActionReference(objectMap, "Flip", InputActionType.Button, "Button"));

            AssertInputGraphDisabled(inputActionAsset);

            return new InputGraph(inputActionAsset, cameraActions, objectActions);
        }

        private static void AssertInputGraphDisabled(InputActionAsset inputActionAsset)
        {
            foreach (InputActionMap actionMap in inputActionAsset.actionMaps)
            {
                Assert.That(actionMap.enabled, Is.False);

                foreach (InputAction action in actionMap.actions)
                {
                    Assert.That(action.enabled, Is.False);
                }
            }
        }

        private void ShutdownRuntimeComponents()
        {
            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                GameObject gameObject = createdGameObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                TabletopInputFrameCoordinator frameCoordinator = gameObject.GetComponent<TabletopInputFrameCoordinator>();
                if (frameCoordinator != null)
                {
                    frameCoordinator.enabled = false;
                }

                TabletopObjectInputAdapter objectInputAdapter = gameObject.GetComponent<TabletopObjectInputAdapter>();
                if (objectInputAdapter != null)
                {
                    objectInputAdapter.Shutdown();
                }

                TabletopCameraInputAdapter cameraInputAdapter = gameObject.GetComponent<TabletopCameraInputAdapter>();
                if (cameraInputAdapter != null)
                {
                    cameraInputAdapter.enabled = false;
                }
            }
        }

        private static TabletopObjectState CreateBaseState(
            TabletopObjectKind kind,
            int seed,
            TabletopPose pose)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                pose,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }

        private static BoxCollider AddBoxCollider(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
            return collider;
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        public enum DiscreteAction
        {
            Rotate,
            Flip
        }

        private readonly struct InputGraph
        {
            public InputGraph(
                InputActionAsset asset,
                CameraActions cameraActions,
                ObjectActions objectActions)
            {
                Asset = asset;
                CameraActions = cameraActions;
                ObjectActions = objectActions;
            }

            public InputActionAsset Asset { get; }

            public CameraActions CameraActions { get; }

            public ObjectActions ObjectActions { get; }
        }

        private readonly struct CameraActions
        {
            public CameraActions(
                InputActionReference keyboardPan,
                InputActionReference dragPan,
                InputActionReference pointerDelta,
                InputActionReference zoom)
            {
                KeyboardPan = keyboardPan;
                DragPan = dragPan;
                PointerDelta = pointerDelta;
                Zoom = zoom;
            }

            public InputActionReference KeyboardPan { get; }

            public InputActionReference DragPan { get; }

            public InputActionReference PointerDelta { get; }

            public InputActionReference Zoom { get; }
        }

        private readonly struct ObjectActions
        {
            public ObjectActions(
                InputActionReference point,
                InputActionReference select,
                InputActionReference cancel,
                InputActionReference rotate,
                InputActionReference flip)
            {
                Point = point;
                Select = select;
                Cancel = cancel;
                Rotate = rotate;
                Flip = flip;
            }

            public InputActionReference Point { get; }

            public InputActionReference Select { get; }

            public InputActionReference Cancel { get; }

            public InputActionReference Rotate { get; }

            public InputActionReference Flip { get; }
        }

        private sealed class FrameFixture
        {
            public FrameFixture(
                MatchState match,
                CardInstanceState cardState,
                PawnState pawnState,
                TokenState tokenState,
                CardView cardView,
                PawnView pawnView,
                TokenView tokenView,
                GameObject cardHighlight,
                GameObject pawnHighlight,
                GameObject tokenHighlight,
                TabletopSelectionState selectionState,
                TabletopMoveInteractionCoordinator moveCoordinator,
                TabletopCameraController cameraController,
                TabletopObjectInputAdapter objectAdapter,
                TabletopInputFrameCoordinator frameCoordinator,
                TabletopSelectionPresenter presenter,
                Camera targetCamera)
            {
                Match = match;
                CardState = cardState;
                PawnState = pawnState;
                TokenState = tokenState;
                CardView = cardView;
                PawnView = pawnView;
                TokenView = tokenView;
                CardHighlight = cardHighlight;
                PawnHighlight = pawnHighlight;
                TokenHighlight = tokenHighlight;
                SelectionState = selectionState;
                MoveCoordinator = moveCoordinator;
                CameraController = cameraController;
                ObjectAdapter = objectAdapter;
                FrameCoordinator = frameCoordinator;
                Presenter = presenter;
                TargetCamera = targetCamera;
                EmptyScreenPoint = ScreenPointForWorld(7f, 7f);
            }

            public MatchState Match { get; }

            public CardInstanceState CardState { get; }

            public PawnState PawnState { get; }

            public TokenState TokenState { get; }

            public CardView CardView { get; }

            public PawnView PawnView { get; }

            public TokenView TokenView { get; }

            public GameObject CardHighlight { get; }

            public GameObject PawnHighlight { get; }

            public GameObject TokenHighlight { get; }

            public TabletopSelectionState SelectionState { get; }

            public TabletopMoveInteractionCoordinator MoveCoordinator { get; }

            public TabletopCameraController CameraController { get; }

            public TabletopObjectInputAdapter ObjectAdapter { get; }

            public TabletopInputFrameCoordinator FrameCoordinator { get; }

            public TabletopSelectionPresenter Presenter { get; }

            public Camera TargetCamera { get; }

            public Vector2 EmptyScreenPoint { get; }

            public void ConfigurePresenter()
            {
                FrameCoordinator.ConfigureSelectionPresenter(Presenter);
            }

            public void ApplyFrame(TabletopInputFrame frame)
            {
                FrameCoordinator.ApplyInputFrame(frame, DeltaTime);
            }

            public TabletopInputFrame CreatePressFrame(TabletopObjectView view)
            {
                return CreateFrame(ScreenPointFor(view), selectPressedThisFrame: true);
            }

            public TabletopInputFrame CreateEmptyPressFrame()
            {
                return CreateFrame(EmptyScreenPoint, selectPressedThisFrame: true);
            }

            public TabletopInputFrame CreateStableNoInputFrame(TabletopObjectView view)
            {
                return CreateFrame(ScreenPointFor(view));
            }

            public TabletopInputFrame CreateStableScrollFrame(TabletopObjectView view)
            {
                return CreateFrame(ScreenPointFor(view), scrollDelta: 100f, rotateDelta: 100f);
            }

            public TabletopInputFrame CreateFlipFrame(TabletopObjectView view)
            {
                return CreateFrame(ScreenPointFor(view), flipPressedThisFrame: true);
            }

            public TabletopObjectView ViewFor(TabletopObjectKind kind)
            {
                switch (kind)
                {
                    case TabletopObjectKind.Card:
                        return CardView;
                    case TabletopObjectKind.Pawn:
                        return PawnView;
                    case TabletopObjectKind.Token:
                        return TokenView;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
                }
            }

            public void AssertAllHighlightsInactive()
            {
                Assert.That(CardHighlight.activeSelf, Is.False);
                Assert.That(PawnHighlight.activeSelf, Is.False);
                Assert.That(TokenHighlight.activeSelf, Is.False);
            }

            public void AssertOnlyHighlightActive(TabletopObjectKind kind)
            {
                Assert.That(CardHighlight.activeSelf, Is.EqualTo(kind == TabletopObjectKind.Card));
                Assert.That(PawnHighlight.activeSelf, Is.EqualTo(kind == TabletopObjectKind.Pawn));
                Assert.That(TokenHighlight.activeSelf, Is.EqualTo(kind == TabletopObjectKind.Token));
            }

            private TabletopInputFrame CreateFrame(
                Vector2 screenPosition,
                bool selectPressedThisFrame = false,
                float scrollDelta = 0f,
                float rotateDelta = 0f,
                bool flipPressedThisFrame = false)
            {
                return new TabletopInputFrame(
                    Vector2.zero,
                    false,
                    Vector2.zero,
                    scrollDelta,
                    screenPosition,
                    selectPressedThisFrame,
                    false,
                    false,
                    false,
                    rotateDelta,
                    flipPressedThisFrame);
            }

            private Vector2 ScreenPointFor(TabletopObjectView view)
            {
                return ScreenPointForWorld(view.transform.position.x, view.transform.position.z);
            }

            private Vector2 ScreenPointForWorld(float x, float z)
            {
                Physics.SyncTransforms();
                Vector3 screenPoint = TargetCamera.WorldToScreenPoint(new Vector3(x, 0f, z));
                Assert.That(float.IsNaN(screenPoint.x), Is.False);
                Assert.That(float.IsNaN(screenPoint.y), Is.False);
                return new Vector2(screenPoint.x, screenPoint.y);
            }
        }

        private sealed class DisableViewWhenEnabled : MonoBehaviour
        {
            public TabletopObjectView TargetView { get; set; }

            public int EnableCount { get; private set; }

            private void OnEnable()
            {
                EnableCount++;

                if (TargetView != null)
                {
                    TargetView.enabled = false;
                }
            }
        }
    }
}
