using System;
using System.Collections.Generic;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopObjectInputAdapterTests
    {
        private const int InteractionLayer = 8;
        private const double CoordinateTolerance = 0.00001d;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();
        private readonly List<InputActionAsset> createdInputAssets = new List<InputActionAsset>();
        private readonly List<InputActionReference> createdActionReferences = new List<InputActionReference>();

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
            createdGameObjects.Clear();
        }

        [Test]
        public void Awake_WithValidActionConfiguration_AcceptsConfiguration()
        {
            AdapterFixture fixture = CreateAdapterFixture();

            Assert.That(fixture.Adapter.enabled, Is.True);
            Assert.That(fixture.Adapter.HasValidActionConfiguration, Is.True);
            Assert.That(fixture.Adapter.IsInitialized, Is.False);
            Assert.That(fixture.Adapter.PointAction, Is.SameAs(fixture.PointReference));
            Assert.That(fixture.Adapter.SelectAction, Is.SameAs(fixture.SelectReference));
            Assert.That(fixture.Adapter.CancelAction, Is.SameAs(fixture.CancelReference));
        }

        [TestCase(RequiredActionReference.Point, "TabletopObjectInputAdapter requires a Point InputActionReference.")]
        [TestCase(RequiredActionReference.Select, "TabletopObjectInputAdapter requires a Select InputActionReference.")]
        [TestCase(RequiredActionReference.Cancel, "TabletopObjectInputAdapter requires a Cancel InputActionReference.")]
        public void Awake_WhenRequiredReferenceIsMissing_DisablesAdapter(
            RequiredActionReference missingReference,
            string expectedMessage)
        {
            LogAssert.Expect(LogType.Error, expectedMessage);

            AdapterFixture fixture = CreateAdapterFixture(missingReference: missingReference);

            Assert.That(fixture.Adapter.enabled, Is.False);
            Assert.That(fixture.Adapter.HasValidActionConfiguration, Is.False);
            Assert.That(fixture.Adapter.IsInitialized, Is.False);
        }

        [Test]
        public void Awake_WhenReferenceHasNoAction_DisablesAdapter()
        {
            LogAssert.Expect(LogType.Error, "TabletopObjectInputAdapter requires the Point InputActionReference to resolve to an InputAction.");

            AdapterFixture fixture = CreateAdapterFixture(pointReferenceOverride: CreateReferenceWithoutAction());

            Assert.That(fixture.Adapter.enabled, Is.False);
            Assert.That(fixture.Adapter.HasValidActionConfiguration, Is.False);
        }

        [Test]
        public void Awake_WhenPointControlTypeIsWrong_DisablesAdapter()
        {
            LogAssert.Expect(LogType.Error, "TabletopObjectInputAdapter requires the Point action expected control type to be Vector2.");

            InputActionMap actionMap = CreateActionMap();
            InputActionReference pointReference = CreateActionReference(actionMap, "Point", InputActionType.PassThrough, "Axis");

            AdapterFixture fixture = CreateAdapterFixture(pointReferenceOverride: pointReference);

            Assert.That(fixture.Adapter.enabled, Is.False);
            Assert.That(fixture.Adapter.HasValidActionConfiguration, Is.False);
        }

        [Test]
        public void Awake_WhenSelectIsNotButton_DisablesAdapter()
        {
            LogAssert.Expect(LogType.Error, "TabletopObjectInputAdapter requires the Select action to be a Button.");

            InputActionMap actionMap = CreateActionMap();
            InputActionReference selectReference = CreateActionReference(actionMap, "Select", InputActionType.Value, "Vector2");

            AdapterFixture fixture = CreateAdapterFixture(selectReferenceOverride: selectReference);

            Assert.That(fixture.Adapter.enabled, Is.False);
        }

        [Test]
        public void Awake_WhenCancelIsNotButton_DisablesAdapter()
        {
            LogAssert.Expect(LogType.Error, "TabletopObjectInputAdapter requires the Cancel action to be a Button.");

            InputActionMap actionMap = CreateActionMap();
            InputActionReference cancelReference = CreateActionReference(actionMap, "Cancel", InputActionType.PassThrough, "Button");

            AdapterFixture fixture = CreateAdapterFixture(cancelReferenceOverride: cancelReference);

            Assert.That(fixture.Adapter.enabled, Is.False);
        }

        [Test]
        public void Initialize_WithValidCoordinator_StoresCoordinatorAndMarksInitialized()
        {
            AdapterFixture fixture = CreateAdapterFixture();

            fixture.Adapter.Initialize(fixture.Coordinator);

            Assert.That(fixture.Adapter.IsInitialized, Is.True);
            Assert.That(fixture.Adapter.Coordinator, Is.SameAs(fixture.Coordinator));
        }

        [Test]
        public void Initialize_WhenCoordinatorIsNull_Throws()
        {
            AdapterFixture fixture = CreateAdapterFixture();

            Assert.Throws<ArgumentNullException>(() => fixture.Adapter.Initialize(null));
        }

        [Test]
        public void Initialize_WhenConfigurationIsInvalid_Throws()
        {
            LogAssert.Expect(LogType.Error, "TabletopObjectInputAdapter requires a Point InputActionReference.");
            AdapterFixture fixture = CreateAdapterFixture(missingReference: RequiredActionReference.Point);

            Assert.Throws<InvalidOperationException>(() => fixture.Adapter.Initialize(fixture.Coordinator));
        }

        [Test]
        public void Initialize_WhenAlreadyInitialized_Throws()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            Assert.Throws<InvalidOperationException>(() => fixture.Adapter.Initialize(fixture.Coordinator));
        }

        [Test]
        public void Initialize_WhenCoordinatorHasActiveInteraction_Throws()
        {
            AdapterFixture fixture = CreateAdapterFixture();
            fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View));

            Assert.Throws<ArgumentException>(() => fixture.Adapter.Initialize(fixture.Coordinator));
        }

        [Test]
        public void Initialize_ClearsLastReleaseResult()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointFor(fixture.View), true, false, true, false);
            Assert.That(fixture.Adapter.LastReleaseResult.HasValue, Is.True);

            fixture.Adapter.Shutdown();
            fixture.Adapter.Initialize(fixture.Coordinator);

            Assert.That(fixture.Adapter.LastReleaseResult.HasValue, Is.False);
        }

        [Test]
        public void OnEnable_AfterInitialization_EnablesAssignedActions()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            Assert.That(fixture.PointReference.action.enabled, Is.True);
            Assert.That(fixture.SelectReference.action.enabled, Is.True);
            Assert.That(fixture.CancelReference.action.enabled, Is.True);
        }

        [Test]
        public void OnEnable_BeforeInitialization_DoesNotEnableActions()
        {
            AdapterFixture fixture = CreateAdapterFixture();

            Assert.That(fixture.PointReference.action.enabled, Is.False);
            Assert.That(fixture.SelectReference.action.enabled, Is.False);
            Assert.That(fixture.CancelReference.action.enabled, Is.False);
        }

        [Test]
        public void OnDisable_DisablesActionsEnabledByAdapter()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            fixture.Adapter.enabled = false;

            Assert.That(fixture.PointReference.action.enabled, Is.False);
            Assert.That(fixture.SelectReference.action.enabled, Is.False);
            Assert.That(fixture.CancelReference.action.enabled, Is.False);
        }

        [Test]
        public void OnDisable_PreservesExternallyEnabledActions()
        {
            AdapterFixture fixture = CreateAdapterFixture(enableActionsBeforeAdapter: true);
            fixture.Adapter.Initialize(fixture.Coordinator);

            fixture.Adapter.enabled = false;

            Assert.That(fixture.PointReference.action.enabled, Is.True);
            Assert.That(fixture.SelectReference.action.enabled, Is.True);
            Assert.That(fixture.CancelReference.action.enabled, Is.True);
        }

        [Test]
        public void OnDisable_WithDuplicateActionReferences_HandlesActionOnce()
        {
            InputActionMap actionMap = CreateActionMap();
            InputActionReference pointReference = CreateActionReference(actionMap, "Point", InputActionType.PassThrough, "Vector2");
            InputActionReference sharedButtonReference = CreateActionReference(actionMap, "SharedButton", InputActionType.Button, "Button");
            AdapterFixture fixture = CreateAdapterFixture(
                pointReferenceOverride: pointReference,
                selectReferenceOverride: sharedButtonReference,
                cancelReferenceOverride: sharedButtonReference);
            fixture.Adapter.Initialize(fixture.Coordinator);

            fixture.Adapter.enabled = false;

            Assert.That(pointReference.action.enabled, Is.False);
            Assert.That(sharedButtonReference.action.enabled, Is.False);
        }

        [Test]
        public void Shutdown_WhenCalledRepeatedly_IsIdempotent()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            Assert.DoesNotThrow(() =>
            {
                fixture.Adapter.Shutdown();
                fixture.Adapter.Shutdown();
            });

            Assert.That(fixture.Adapter.IsInitialized, Is.False);
            Assert.That(fixture.Adapter.Coordinator, Is.Null);
        }

        [Test]
        public void Shutdown_WhenInteractionIsActive_ResetsCoordinatorInteraction()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointFor(fixture.View), true, false, false, false);

            fixture.Adapter.Shutdown();

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void Shutdown_WhenInteractionIsActive_ReleasesCoordinatorLocks()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointFor(fixture.View), true, false, false, false);

            fixture.Adapter.Shutdown();

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnDestroy_WhenInitialized_CleansActiveInteractionSafely()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointFor(fixture.View), true, false, false, false);

            UnityObject.DestroyImmediate(fixture.Adapter.gameObject);

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.NegativeInfinity)]
        public void ApplyInputFrame_WhenScreenPositionIsInvalid_ThrowsWithoutMutation(float x, float y)
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => fixture.Adapter.ApplyInputFrame(new Vector2(x, y), true, false, false, false));

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void ApplyInputFrame_WhenNotInitialized_Throws()
        {
            AdapterFixture fixture = CreateAdapterFixture();

            Assert.Throws<InvalidOperationException>(
                () => fixture.Adapter.ApplyInputFrame(fixture.ScreenPointFor(fixture.View), true, false, false, false));
        }

        [Test]
        public void ApplyInputFrame_WhenPressHitsObject_BeginsInteraction()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointFor(fixture.View), true, false, false, false);

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.True);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
        }

        [Test]
        public void ApplyInputFrame_WhenPressHitsEmptySpace_DoesNotBeginInteraction()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.SelectionState.Select(fixture.View);

            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointForWorld(7f, 7f), true, false, false, false);

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenHeldBelowThreshold_RemainsPressed()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginPressThroughAdapter();

            fixture.Adapter.ApplyInputFrame(fixture.PressScreenPoint + new Vector2(1f, 0f), false, true, false, false);

            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenHeldCrossesThreshold_BeginsDragPreview()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginPressThroughAdapter();

            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointForWorld(2f, 0f), false, true, false, false);

            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
            Assert.That(fixture.PreviewSession.IsActive, Is.True);
            Assert.That(fixture.View.IsPreviewing, Is.True);
        }

        [Test]
        public void ApplyInputFrame_WhenHeldRepeatedly_UpdatesPreview()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginDragThroughAdapter();

            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointForWorld(-3f, 4f), false, true, false, false);

            AssertCoordinate(fixture.View.PreviewPose.Position, -3.0, 4.0);
        }

        [Test]
        public void ApplyInputFrame_WhenReleaseBeforeDrag_ReturnsClickCompleted()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginPressThroughAdapter();

            MoveInteractionReleaseResult? result = fixture.Adapter.ApplyInputFrame(
                fixture.PressScreenPoint,
                false,
                false,
                true,
                false);

            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.ClickCompleted));
        }

        [Test]
        public void ApplyInputFrame_WhenReleaseAfterDrag_ReturnsMoveAccepted()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginDragThroughAdapter();

            MoveInteractionReleaseResult? result = fixture.Adapter.ApplyInputFrame(
                fixture.ScreenPointForWorld(4f, -2f),
                false,
                false,
                true,
                false);

            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
            AssertCoordinate(fixture.State.Pose.Position, 4.0, -2.0);
        }

        [Test]
        public void ApplyInputFrame_WhenReleaseOccurs_StoresLastReleaseResult()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginPressThroughAdapter();

            MoveInteractionReleaseResult? result = fixture.Adapter.ApplyInputFrame(
                fixture.PressScreenPoint,
                false,
                false,
                true,
                false);

            Assert.That(fixture.Adapter.LastReleaseResult.HasValue, Is.True);
            Assert.That(fixture.Adapter.LastReleaseResult.Value, Is.EqualTo(result.Value));
        }

        [Test]
        public void ApplyInputFrame_WhenReleaseAndHeldAreBothTrue_ReleaseHasPriority()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginDragThroughAdapter();

            MoveInteractionReleaseResult? result = fixture.Adapter.ApplyInputFrame(
                fixture.ScreenPointForWorld(4f, -2f),
                false,
                true,
                true,
                false);

            Assert.That(result.HasValue, Is.True);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenCancelFromPressed_CleansInteraction()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginPressThroughAdapter();

            MoveInteractionReleaseResult? result = fixture.Adapter.ApplyInputFrame(
                fixture.PressScreenPoint,
                false,
                true,
                false,
                true);

            Assert.That(result.HasValue, Is.False);
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyInputFrame_WhenCancelFromDragging_RestoresAcceptedState()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            TabletopPose acceptedPose = fixture.State.Pose;
            fixture.BeginDragThroughAdapter();

            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointForWorld(5f, 5f), false, true, false, true);

            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
            Assert.That(fixture.View.IsPreviewing, Is.False);
            AssertWorldPose(fixture.View, acceptedPose);
        }

        [Test]
        public void ApplyInputFrame_WhenCancelAndReleaseAreBothTrue_CancelHasPriority()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            fixture.BeginDragThroughAdapter();
            long revision = fixture.Match.Revision;

            MoveInteractionReleaseResult? result = fixture.Adapter.ApplyInputFrame(
                fixture.ScreenPointForWorld(4f, -2f),
                false,
                true,
                true,
                true);

            Assert.That(result.HasValue, Is.False);
            Assert.That(fixture.Adapter.LastReleaseResult.HasValue, Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(revision));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void ApplyInputFrame_WhenCancelWithoutActiveInteraction_IsSafe()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            MoveInteractionReleaseResult? result = fixture.Adapter.ApplyInputFrame(
                fixture.PressScreenPoint,
                false,
                false,
                false,
                true);

            Assert.That(result.HasValue, Is.False);
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
        }

        [Test]
        public void ApplyInputFrame_WhenPressAndReleaseSameFrame_ProducesClickCompletion()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            MoveInteractionReleaseResult? result = fixture.Adapter.ApplyInputFrame(
                fixture.PressScreenPoint,
                true,
                false,
                true,
                false);

            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.ClickCompleted));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
        }

        [Test]
        public void ApplyInputFrame_DoesNotMutateRuntimeStateBeforeCoordinatorAcceptance()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();
            TabletopPose acceptedPose = fixture.State.Pose;

            fixture.BeginDragThroughAdapter();
            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointForWorld(5f, 5f), false, true, false, false);

            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
        }

        [Test]
        public void ApplyInputFrame_DoesNotAdvanceRevisionDuringHeldPreviewFrames()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture(revision: 12);

            fixture.BeginDragThroughAdapter();
            fixture.Adapter.ApplyInputFrame(fixture.ScreenPointForWorld(5f, 5f), false, true, false, false);

            Assert.That(fixture.Match.Revision, Is.EqualTo(12));
        }

        [Test]
        public void Adapter_DoesNotUsePlayerInput()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            Assert.That(fixture.Adapter.GetComponent<PlayerInput>(), Is.Null);
        }

        [Test]
        public void Adapter_DoesNotRequireTabletopPrototypeScene()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            Assert.That(fixture.Coordinator, Is.Not.Null);
            Assert.That(fixture.Adapter.gameObject.scene.IsValid(), Is.True);
        }

        [Test]
        public void Adapter_DoesNotReferenceRotateOrFlipActions()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            Assert.That(fixture.Adapter.PointAction, Is.SameAs(fixture.PointReference));
            Assert.That(fixture.Adapter.SelectAction, Is.SameAs(fixture.SelectReference));
            Assert.That(fixture.Adapter.CancelAction, Is.SameAs(fixture.CancelReference));
        }

        [Test]
        public void Adapter_DelegatesThroughCoordinatorWithoutOwningStateServices()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            fixture.Adapter.ApplyInputFrame(fixture.PressScreenPoint, true, false, false, false);

            Assert.That(fixture.Coordinator.ActiveView, Is.SameAs(fixture.View));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [Test]
        public void Adapter_HasNoCameraOrSurfaceProxyDependency()
        {
            AdapterFixture fixture = CreateInitializedAdapterFixture();

            Assert.That(fixture.Adapter.GetComponent<Camera>(), Is.Null);
            Assert.That(fixture.Adapter.GetComponent<ConsoleCards.Presentation.TableSurface.TabletopSurfaceProxy>(), Is.Null);
        }

        private AdapterFixture CreateInitializedAdapterFixture(
            long revision = 0,
            bool enableActionsBeforeAdapter = false)
        {
            AdapterFixture fixture = CreateAdapterFixture(revision: revision, enableActionsBeforeAdapter: enableActionsBeforeAdapter);
            fixture.Adapter.Initialize(fixture.Coordinator);
            return fixture;
        }

        private AdapterFixture CreateAdapterFixture(
            long revision = 0,
            RequiredActionReference? missingReference = null,
            InputActionReference pointReferenceOverride = null,
            InputActionReference selectReferenceOverride = null,
            InputActionReference cancelReferenceOverride = null,
            bool enableActionsBeforeAdapter = false)
        {
            InputActionMap actionMap = CreateActionMap();
            InputActionReference createdPointReference = CreateActionReference(actionMap, "Point", InputActionType.PassThrough, "Vector2");
            InputActionReference createdSelectReference = CreateActionReference(actionMap, "Select", InputActionType.Button, "Button");
            InputActionReference createdCancelReference = CreateActionReference(actionMap, "Cancel", InputActionType.Button, "Button");

            InputActionReference assignedPointReference = missingReference == RequiredActionReference.Point
                ? null
                : pointReferenceOverride ?? createdPointReference;
            InputActionReference assignedSelectReference = missingReference == RequiredActionReference.Select
                ? null
                : selectReferenceOverride ?? createdSelectReference;
            InputActionReference assignedCancelReference = missingReference == RequiredActionReference.Cancel
                ? null
                : cancelReferenceOverride ?? createdCancelReference;

            if (enableActionsBeforeAdapter)
            {
                assignedPointReference.action.Enable();
                assignedSelectReference.action.Enable();
                assignedCancelReference.action.Enable();
            }

            CoordinatorFixture coordinatorFixture = CreateCoordinatorFixture(revision);
            GameObject adapterObject = CreateGameObject("Tabletop Object Input Adapter");
            adapterObject.SetActive(false);
            TabletopObjectInputAdapter adapter = adapterObject.AddComponent<TabletopObjectInputAdapter>();
            adapter.pointAction = assignedPointReference;
            adapter.selectAction = assignedSelectReference;
            adapter.cancelAction = assignedCancelReference;
            adapterObject.SetActive(true);

            return new AdapterFixture(
                adapter,
                assignedPointReference,
                assignedSelectReference,
                assignedCancelReference,
                coordinatorFixture);
        }

        private CoordinatorFixture CreateCoordinatorFixture(long revision)
        {
            Camera camera = CreateCamera();
            TabletopCoordinateConverter converter = CreateConverter();
            TabletopObjectView view = CreateBoundView(TabletopObjectKind.Card, 1, TabletopPose.Default, false, out TabletopObjectState state);
            AddBoxCollider(view.gameObject, InteractionLayer);

            MatchState match = CreateMatch(revision, cards: new[] { new CardInstanceState(state, CardFace.FaceUp) });
            TabletopSelectionState selectionState = new TabletopSelectionState();
            TabletopObjectHitResolver hitResolver = new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), 25f);
            TabletopPointerProjector pointerProjector = new TabletopPointerProjector(camera, converter, 0f);
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(5f);
            TabletopDragPreviewSession previewSession = new TabletopDragPreviewSession();
            MoveObjectUseCase moveUseCase = new MoveObjectUseCase();
            PlayerId requestedByPlayerId = PlayerId.New();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            TabletopMoveInteractionCoordinator coordinator = new TabletopMoveInteractionCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                stateMachine,
                previewSession,
                moveUseCase);

            return new CoordinatorFixture(
                coordinator,
                camera,
                match,
                view,
                state,
                selectionState,
                lockService,
                stateMachine,
                previewSession,
                requestedByPlayerId,
                ownerId);
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = CreateGameObject("Object Input Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
            return camera;
        }

        private TabletopObjectView CreateBoundView(
            TabletopObjectKind kind,
            int seed,
            TabletopPose pose,
            bool isUserLocked,
            out TabletopObjectState state)
        {
            switch (kind)
            {
                case TabletopObjectKind.Card:
                {
                    CardView view = CreateView<CardView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked);
                    view.Bind(new CardInstanceState(state, CardFace.FaceUp), CreateConverter());
                    return view;
                }

                case TabletopObjectKind.Pawn:
                {
                    PawnView view = CreateView<PawnView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked);
                    view.Bind(new PawnState(state), CreateConverter());
                    return view;
                }

                case TabletopObjectKind.Token:
                {
                    TokenView view = CreateView<TokenView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked);
                    view.Bind(new TokenState(state), CreateConverter());
                    return view;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
            }
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

        private InputActionMap CreateActionMap()
        {
            InputActionAsset inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            createdInputAssets.Add(inputActionAsset);

            return inputActionAsset.AddActionMap("TabletopObject");
        }

        private InputActionReference CreateActionReference(
            InputActionMap actionMap,
            string actionName,
            InputActionType actionType,
            string expectedControlType)
        {
            InputAction action = actionMap.AddAction(actionName, actionType, expectedControlLayout: expectedControlType);
            InputActionReference actionReference = InputActionReference.Create(action);
            createdActionReferences.Add(actionReference);
            return actionReference;
        }

        private InputActionReference CreateReferenceWithoutAction()
        {
            InputActionReference actionReference = ScriptableObject.CreateInstance<InputActionReference>();
            createdActionReferences.Add(actionReference);
            return actionReference;
        }

        private static BoxCollider AddBoxCollider(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
            return collider;
        }

        private static MatchState CreateMatch(
            long revision,
            CardInstanceState[] cards = null,
            PawnState[] pawns = null,
            TokenState[] tokens = null)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards ?? Array.Empty<CardInstanceState>(),
                pawns ?? Array.Empty<PawnState>(),
                tokens ?? Array.Empty<TokenState>(),
                Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());
        }

        private static TabletopObjectState CreateBaseState(
            TabletopObjectKind kind,
            int seed,
            TabletopPose pose,
            bool isUserLocked = false)
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

        private static void AssertWorldPose(TabletopObjectView view, TabletopPose pose)
        {
            Assert.That(view.transform.position.x, Is.EqualTo((float)pose.Position.X).Within(0.0001f));
            Assert.That(view.transform.position.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(view.transform.position.z, Is.EqualTo((float)pose.Position.Y).Within(0.0001f));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, pose.RotationDegrees, 0f), view.transform.rotation), Is.EqualTo(0f).Within(0.0001f));
        }

        private static void AssertCoordinate(TableCoordinate actual, double expectedX, double expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(CoordinateTolerance));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(CoordinateTolerance));
        }

        public enum RequiredActionReference
        {
            Point,
            Select,
            Cancel
        }

        private sealed class AdapterFixture
        {
            private readonly CoordinatorFixture coordinatorFixture;

            public AdapterFixture(
                TabletopObjectInputAdapter adapter,
                InputActionReference pointReference,
                InputActionReference selectReference,
                InputActionReference cancelReference,
                CoordinatorFixture coordinatorFixture)
            {
                Adapter = adapter;
                PointReference = pointReference;
                SelectReference = selectReference;
                CancelReference = cancelReference;
                this.coordinatorFixture = coordinatorFixture;
            }

            public TabletopObjectInputAdapter Adapter { get; }

            public InputActionReference PointReference { get; }

            public InputActionReference SelectReference { get; }

            public InputActionReference CancelReference { get; }

            public TabletopMoveInteractionCoordinator Coordinator => coordinatorFixture.Coordinator;

            public MatchState Match => coordinatorFixture.Match;

            public TabletopObjectView View => coordinatorFixture.View;

            public TabletopObjectState State => coordinatorFixture.State;

            public TabletopSelectionState SelectionState => coordinatorFixture.SelectionState;

            public LocalInteractionLockService LockService => coordinatorFixture.LockService;

            public TabletopInteractionStateMachine StateMachine => coordinatorFixture.StateMachine;

            public TabletopDragPreviewSession PreviewSession => coordinatorFixture.PreviewSession;

            public Vector2 PressScreenPoint => coordinatorFixture.PressScreenPoint;

            public void BeginPressThroughAdapter()
            {
                Adapter.ApplyInputFrame(PressScreenPoint, true, false, false, false);
                Assert.That(Coordinator.HasActiveInteraction, Is.True);
            }

            public void BeginDragThroughAdapter()
            {
                BeginPressThroughAdapter();
                Adapter.ApplyInputFrame(ScreenPointForWorld(2f, 0f), false, true, false, false);
                Assert.That(PreviewSession.IsActive, Is.True);
            }

            public Vector2 ScreenPointFor(TabletopObjectView view)
            {
                return coordinatorFixture.ScreenPointFor(view);
            }

            public Vector2 ScreenPointForWorld(float x, float z)
            {
                return coordinatorFixture.ScreenPointForWorld(x, z);
            }
        }

        private sealed class CoordinatorFixture
        {
            public CoordinatorFixture(
                TabletopMoveInteractionCoordinator coordinator,
                Camera camera,
                MatchState match,
                TabletopObjectView view,
                TabletopObjectState state,
                TabletopSelectionState selectionState,
                LocalInteractionLockService lockService,
                TabletopInteractionStateMachine stateMachine,
                TabletopDragPreviewSession previewSession,
                PlayerId requestedByPlayerId,
                InteractionOwnerId ownerId)
            {
                Coordinator = coordinator;
                Camera = camera;
                Match = match;
                View = view;
                State = state;
                SelectionState = selectionState;
                LockService = lockService;
                StateMachine = stateMachine;
                PreviewSession = previewSession;
                RequestedByPlayerId = requestedByPlayerId;
                OwnerId = ownerId;
                PressScreenPoint = ScreenPointFor(view);
            }

            public TabletopMoveInteractionCoordinator Coordinator { get; }

            public Camera Camera { get; }

            public MatchState Match { get; }

            public TabletopObjectView View { get; }

            public TabletopObjectState State { get; }

            public TabletopSelectionState SelectionState { get; }

            public LocalInteractionLockService LockService { get; }

            public TabletopInteractionStateMachine StateMachine { get; }

            public TabletopDragPreviewSession PreviewSession { get; }

            public PlayerId RequestedByPlayerId { get; }

            public InteractionOwnerId OwnerId { get; }

            public Vector2 PressScreenPoint { get; }

            public Vector2 ScreenPointFor(TabletopObjectView view)
            {
                return ScreenPointForWorld(view.transform.position.x, view.transform.position.z);
            }

            public Vector2 ScreenPointForWorld(float x, float z)
            {
                Physics.SyncTransforms();
                Vector3 screenPoint = Camera.WorldToScreenPoint(new Vector3(x, 0f, z));
                Assert.That(float.IsFinite(screenPoint.x), Is.True);
                Assert.That(float.IsFinite(screenPoint.y), Is.True);
                return new Vector2(screenPoint.x, screenPoint.y);
            }
        }
    }
}
