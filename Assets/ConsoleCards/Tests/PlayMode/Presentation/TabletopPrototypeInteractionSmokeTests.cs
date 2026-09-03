using System.Collections;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Prototype;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopPrototypeInteractionSmokeTests
    {
        private const string ScenePath = "Assets/ConsoleCards/Presentation/Scenes/TabletopPrototype.unity";
        private const float DeltaTime = 1f;
        private const float Tolerance = 0.0001f;
        private const float ScrollDelta = 120f;
        private const float RotationStep = 15f;

        private SceneFixture fixture;
        private Scene loadedScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixture != null)
            {
                yield return fixture.Dispose();
                fixture = null;
            }
            else if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(loadedScene);
                while (unloadOperation != null && !unloadOperation.isDone)
                {
                    yield return null;
                }
            }

            loadedScene = default;

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Scene_SelectsObjectsAndUpdatesHighlights()
        {
            yield return LoadFixture();
            long revision = fixture.MatchRevision;

            fixture.Click(fixture.CardView);
            fixture.AssertSelection(fixture.CardView);
            fixture.AssertHighlights(card: true, pawn: false, token: false);
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));

            fixture.Click(fixture.PawnView);
            fixture.AssertSelection(fixture.PawnView);
            fixture.AssertHighlights(card: false, pawn: true, token: false);
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));

            fixture.Click(fixture.TokenView);
            fixture.AssertSelection(fixture.TokenView);
            fixture.AssertHighlights(card: false, pawn: false, token: true);
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));
        }

        [UnityTest]
        public IEnumerator Scene_EmptyClickClearsSelection()
        {
            yield return LoadFixture();
            fixture.Click(fixture.CardView);
            long revision = fixture.MatchRevision;

            fixture.ClickEmptyTable();

            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            fixture.AssertHighlights(card: false, pawn: false, token: false);
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));
        }

        [UnityTest]
        public IEnumerator Scene_SelectionHighlightChangesInSameSharedLogicalFrame()
        {
            yield return LoadFixture();

            fixture.Press(fixture.CardView);

            fixture.AssertSelection(fixture.CardView);
            fixture.AssertHighlights(card: true, pawn: false, token: false);
            Assert.That(fixture.MatchRevision, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Scene_SelectedViewsRemainCompositionSelectionStateObjects()
        {
            yield return LoadFixture();

            fixture.Click(fixture.PawnView);

            Assert.That(fixture.SelectionState, Is.SameAs(fixture.Composition.SelectionState));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.PawnView));
            Assert.That(fixture.PawnView.PawnState, Is.SameAs(fixture.Composition.PawnState));
            Assert.That(fixture.PawnView.BoundState, Is.SameAs(fixture.Composition.MatchState.GetObject(fixture.PawnView.ObjectId)));
        }

        [UnityTest]
        public IEnumerator Scene_DragBelowThresholdDoesNotPreview()
        {
            yield return LoadFixture();
            TabletopPose acceptedPose = fixture.CardState.BaseState.Pose;
            Vector3 acceptedPosition = fixture.CardView.transform.position;

            fixture.Press(fixture.CardView);
            fixture.HoldAt(fixture.ScreenPointFor(fixture.CardView) + new Vector2(1f, 0f));

            Assert.That(fixture.MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
            fixture.AssertVector3(fixture.CardView.transform.position, acceptedPosition);
            Assert.That(fixture.CardState.BaseState.Pose, Is.EqualTo(acceptedPose));
            Assert.That(fixture.MatchRevision, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Scene_DragPreviewDoesNotMutateRuntimeState()
        {
            yield return LoadFixture();
            StateSnapshot before = fixture.CaptureState();
            TableCoordinate target = new TableCoordinate(-1d, 1d);

            fixture.BeginDrag(fixture.CardView, target);

            Assert.That(fixture.MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
            Assert.That(fixture.PreviewSession.IsActive, Is.True);
            Assert.That(fixture.PreviewSession.ActiveView, Is.SameAs(fixture.CardView));
            fixture.AssertTablePosition(fixture.CardView.transform.position, target);
            fixture.AssertStateUnchanged(before);
            Assert.That(fixture.MatchRevision, Is.EqualTo(before.Revision));
        }

        [UnityTest]
        public IEnumerator Scene_RepeatedDragFramesReplacePreviewWithoutCommands()
        {
            yield return LoadFixture();
            StateSnapshot before = fixture.CaptureState();

            fixture.BeginDrag(fixture.CardView, new TableCoordinate(-1d, 1d));
            fixture.HoldAt(fixture.ScreenPointForTable(-0.5d, 1.5d));

            Assert.That(fixture.PreviewSession.IsActive, Is.True);
            fixture.AssertTablePosition(fixture.CardView.transform.position, new TableCoordinate(-0.5d, 1.5d));
            fixture.AssertStateUnchanged(before);
            Assert.That(fixture.MatchRevision, Is.EqualTo(before.Revision));
        }

        [UnityTest]
        public IEnumerator Scene_CardMoveIsAcceptedAndReconciled()
        {
            yield return LoadFixture();

            fixture.AssertAcceptedMove(fixture.CardView, new TableCoordinate(0d, 0d));
        }

        [UnityTest]
        public IEnumerator Scene_PawnMoveIsAcceptedAndReconciled()
        {
            yield return LoadFixture();

            fixture.AssertAcceptedMove(fixture.PawnView, new TableCoordinate(0.75d, 1d));
        }

        [UnityTest]
        public IEnumerator Scene_TokenMoveIsAcceptedAndReconciled()
        {
            yield return LoadFixture();

            fixture.AssertAcceptedMove(fixture.TokenView, new TableCoordinate(1.25d, -1d));
        }

        [UnityTest]
        public IEnumerator Scene_CancelPressedRestoresAcceptedState()
        {
            yield return LoadFixture();
            fixture.Click(fixture.CardView);
            StateSnapshot before = fixture.CaptureState();

            fixture.Press(fixture.CardView);
            fixture.CancelAt(fixture.ScreenPointFor(fixture.CardView));

            Assert.That(fixture.MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
            fixture.AssertStateUnchanged(before);
            fixture.AssertSelection(fixture.CardView);
            fixture.AssertHighlights(card: true, pawn: false, token: false);
        }

        [UnityTest]
        public IEnumerator Scene_CancelDraggingRestoresAcceptedState()
        {
            yield return LoadFixture();
            fixture.Click(fixture.CardView);
            StateSnapshot before = fixture.CaptureState();

            fixture.BeginDrag(fixture.CardView, new TableCoordinate(-1d, 1d));
            fixture.CancelAt(fixture.ScreenPointForTable(-1d, 1d));

            Assert.That(fixture.MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
            fixture.AssertVector3(fixture.CardView.transform.position, before.CardPosition);
            fixture.AssertStateUnchanged(before);
            fixture.AssertSelection(fixture.CardView);
            fixture.AssertHighlights(card: true, pawn: false, token: false);
        }

        [UnityTest]
        public IEnumerator Scene_ShutdownDuringActiveDragCleansCompositionGraph()
        {
            yield return LoadFixture();
            Vector3 cardScale = fixture.CardView.transform.localScale;
            Vector3 pawnScale = fixture.PawnView.transform.localScale;
            Vector3 tokenScale = fixture.TokenView.transform.localScale;
            CardInstanceState cardState = fixture.CardState;
            LocalInteractionLockService lockService = fixture.LockService;
            TabletopPose cardPose = cardState.BaseState.Pose;
            Vector3 acceptedCardPosition = fixture.CardView.transform.position;

            fixture.BeginDrag(fixture.CardView, new TableCoordinate(-1d, 1d));
            Assert.That(fixture.PreviewSession.IsActive, Is.True);

            fixture.Composition.Shutdown();
            Physics.SyncTransforms();

            Assert.That(fixture.Composition.IsInitialized, Is.False);
            Assert.That(fixture.FrameCoordinator.enabled, Is.False);
            Assert.That(fixture.CameraAdapter.IsExternallyDriven, Is.False);
            Assert.That(fixture.ObjectAdapter.IsExternallyDriven, Is.False);
            Assert.That(fixture.ObjectAdapter.IsInitialized, Is.False);
            Assert.That(fixture.CameraAdapter.HasScrollRoutingPolicy, Is.False);
            Assert.That(fixture.FrameCoordinator.HasSelectionPresenter, Is.False);
            fixture.AssertHighlights(card: false, pawn: false, token: false);
            Assert.That(fixture.CardSelectionVisual.IsConfigured, Is.False);
            Assert.That(fixture.PawnSelectionVisual.IsConfigured, Is.False);
            Assert.That(fixture.TokenSelectionVisual.IsConfigured, Is.False);
            Assert.That(fixture.CardView.IsBound, Is.False);
            Assert.That(fixture.PawnView.IsBound, Is.False);
            Assert.That(fixture.TokenView.IsBound, Is.False);
            Assert.That(lockService.Count, Is.EqualTo(0));
            Assert.That(fixture.CardObject, Is.Not.Null);
            fixture.AssertVector3(fixture.CardView.transform.localScale, cardScale);
            fixture.AssertVector3(fixture.PawnView.transform.localScale, pawnScale);
            fixture.AssertVector3(fixture.TokenView.transform.localScale, tokenScale);
            fixture.AssertVector3(fixture.CardView.transform.position, acceptedCardPosition);
            Assert.That(cardPose, Is.EqualTo(cardState.BaseState.Pose));
        }

        [UnityTest]
        public IEnumerator Scene_SelectedObjectScrollRotatesWithoutZoom()
        {
            yield return LoadFixture();

            fixture.AssertSelectedScrollRotates(fixture.CardView, fixture.CardState.BaseState, ScrollDelta, RotationStep);
            fixture.AssertSelectedScrollRotates(fixture.PawnView, fixture.PawnState.BaseState, -ScrollDelta, -RotationStep);
            fixture.AssertSelectedScrollRotates(fixture.TokenView, fixture.TokenState.BaseState, 1000f, RotationStep);
        }

        [UnityTest]
        public IEnumerator Scene_NoSelectionScrollZoomsWithoutRotation()
        {
            yield return LoadFixture();
            StateSnapshot before = fixture.CaptureState();
            float initialSize = fixture.MainCamera.orthographicSize;
            float expectedSize = initialSize - ScrollDelta * fixture.CameraAdapter.ZoomSensitivity;

            fixture.ApplyWheelFrame(ScrollDelta);

            Assert.That(fixture.MainCamera.orthographicSize, Is.EqualTo(expectedSize).Within(Tolerance));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            fixture.AssertObjectRotations(before);
            Assert.That(fixture.MatchRevision, Is.EqualTo(before.Revision));
        }

        [UnityTest]
        public IEnumerator Scene_DraggingScrollIsSuppressed()
        {
            yield return LoadFixture();
            fixture.BeginDrag(fixture.CardView, new TableCoordinate(-1d, 1d));
            float cameraSize = fixture.MainCamera.orthographicSize;
            float rotation = fixture.CardState.BaseState.Pose.RotationDegrees;
            long revision = fixture.MatchRevision;

            fixture.ApplyWheelFrame(ScrollDelta);

            Assert.That(fixture.MainCamera.orthographicSize, Is.EqualTo(cameraSize).Within(Tolerance));
            Assert.That(fixture.CardState.BaseState.Pose.RotationDegrees, Is.EqualTo(rotation).Within(Tolerance));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));
            Assert.That(fixture.PreviewSession.IsActive, Is.True);
        }

        [UnityTest]
        public IEnumerator Scene_PointerTransitionScrollSuppressesZoomAndRotation()
        {
            yield return LoadFixture();
            fixture.Click(fixture.CardView);
            float cameraSize = fixture.MainCamera.orthographicSize;
            float rotation = fixture.CardState.BaseState.Pose.RotationDegrees;
            long revision = fixture.MatchRevision;

            fixture.ApplyFrame(
                fixture.ScreenPointFor(fixture.CardView),
                selectPressedThisFrame: true,
                rotateDelta: ScrollDelta,
                scrollDelta: ScrollDelta);

            Assert.That(fixture.MainCamera.orthographicSize, Is.EqualTo(cameraSize).Within(Tolerance));
            Assert.That(fixture.CardState.BaseState.Pose.RotationDegrees, Is.EqualTo(rotation).Within(Tolerance));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));
        }

        [UnityTest]
        public IEnumerator Scene_UserLockedSelectionRejectsRotationWithoutZoom()
        {
            yield return LoadFixture();
            fixture.Click(fixture.CardView);
            fixture.CardState.BaseState.SetUserLocked(true);
            float cameraSize = fixture.MainCamera.orthographicSize;
            long revision = fixture.MatchRevision;

            fixture.ApplyWheelFrame(ScrollDelta);

            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.True);
            Assert.That(fixture.ObjectAdapter.LastRotationResult.Value.Status, Is.EqualTo(RotationInteractionStatus.ObjectUserLocked));
            Assert.That(fixture.MainCamera.orthographicSize, Is.EqualTo(cameraSize).Within(Tolerance));
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));
        }

        [UnityTest]
        public IEnumerator Scene_CardFlipUpdatesAuthoritativeFaceAndRoots()
        {
            yield return LoadFixture();
            fixture.Click(fixture.CardView);
            Vector3 position = fixture.CardView.transform.position;
            Quaternion rotation = fixture.CardView.transform.rotation;
            Vector3 scale = fixture.CardView.transform.localScale;
            long revision = fixture.MatchRevision;

            fixture.ApplyFrame(fixture.ScreenPointFor(fixture.CardView), flipPressedThisFrame: true);

            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.True);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.CardView.DisplayedFace, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.CardFaceUpRoot.activeSelf, Is.False);
            Assert.That(fixture.CardFaceDownRoot.activeSelf, Is.True);
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision + 1));
            fixture.AssertVector3(fixture.CardView.transform.position, position);
            Assert.That(Quaternion.Angle(fixture.CardView.transform.rotation, rotation), Is.EqualTo(0f).Within(Tolerance));
            fixture.AssertVector3(fixture.CardView.transform.localScale, scale);
            fixture.AssertSelection(fixture.CardView);
            fixture.AssertHighlights(card: true, pawn: false, token: false);

            fixture.ApplyFrame(fixture.ScreenPointFor(fixture.CardView), flipPressedThisFrame: true);

            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.CardView.DisplayedFace, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision + 2));
        }

        [UnityTest]
        public IEnumerator Scene_NonCardFlipIsRejected()
        {
            yield return LoadFixture();
            long revision = fixture.MatchRevision;

            fixture.Click(fixture.PawnView);
            fixture.ApplyFrame(fixture.ScreenPointFor(fixture.PawnView), flipPressedThisFrame: true);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.True);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Status, Is.EqualTo(FlipInteractionStatus.SelectionNotCard));
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));
            fixture.AssertSelection(fixture.PawnView);
            fixture.AssertHighlights(card: false, pawn: true, token: false);

            fixture.Click(fixture.TokenView);
            fixture.ApplyFrame(fixture.ScreenPointFor(fixture.TokenView), flipPressedThisFrame: true);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Status, Is.EqualTo(FlipInteractionStatus.SelectionNotCard));
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));
            fixture.AssertSelection(fixture.TokenView);
            fixture.AssertHighlights(card: false, pawn: false, token: true);
        }

        [UnityTest]
        public IEnumerator Scene_FlipWinsOverRotate()
        {
            yield return LoadFixture();
            fixture.Click(fixture.CardView);
            float cameraSize = fixture.MainCamera.orthographicSize;
            float rotation = fixture.CardState.BaseState.Pose.RotationDegrees;
            long revision = fixture.MatchRevision;

            fixture.ApplyFrame(
                fixture.ScreenPointFor(fixture.CardView),
                rotateDelta: ScrollDelta,
                flipPressedThisFrame: true,
                scrollDelta: ScrollDelta);

            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.CardState.BaseState.Pose.RotationDegrees, Is.EqualTo(rotation).Within(Tolerance));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
            Assert.That(fixture.MainCamera.orthographicSize, Is.EqualTo(cameraSize).Within(Tolerance));
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision + 1));
        }

        [UnityTest]
        public IEnumerator Scene_PointerTransitionFlipIsSuppressed()
        {
            yield return LoadFixture();
            fixture.Click(fixture.CardView);
            CardFace face = fixture.CardState.Face;
            long revision = fixture.MatchRevision;

            fixture.ApplyFrame(
                fixture.ScreenPointFor(fixture.CardView),
                selectPressedThisFrame: true,
                flipPressedThisFrame: true);

            Assert.That(fixture.CardState.Face, Is.EqualTo(face));
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
            Assert.That(fixture.MatchRevision, Is.EqualTo(revision));
        }

        [UnityTest]
        public IEnumerator Scene_ManualFaceRootInversionReappliesAuthoritativeFace()
        {
            yield return LoadFixture();
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));

            fixture.CardFaceUpRoot.SetActive(false);
            fixture.CardFaceDownRoot.SetActive(true);
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));

            fixture.CardView.ApplyAcceptedState();

            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.CardView.DisplayedFace, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.CardFaceUpRoot.activeSelf, Is.True);
            Assert.That(fixture.CardFaceDownRoot.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Scene_CameraPanDoesNotMutateObjects()
        {
            yield return LoadFixture();
            StateSnapshot before = fixture.CaptureState();
            TableCoordinate focus = fixture.CameraController.State.FocusCoordinate;

            fixture.ApplyFrame(
                fixture.EmptyScreenPoint,
                keyboardPan: new Vector2(1f, 0f),
                dragHeld: true,
                pointerDelta: new Vector2(10f, 0f));

            Assert.That(fixture.CameraController.State.FocusCoordinate, Is.Not.EqualTo(focus));
            fixture.AssertStateUnchanged(before);
            fixture.AssertAcceptedViewPositions(before);
        }

        [UnityTest]
        public IEnumerator Scene_SurfaceProxyIsVisualOnly()
        {
            yield return LoadFixture();
            StateSnapshot before = fixture.CaptureState();
            Vector3 cameraRigBefore = fixture.CameraRig.position;
            Assert.That(fixture.SurfaceProxy.GetComponentsInChildren<Collider>(true), Is.Empty);

            fixture.ApplyFrame(fixture.EmptyScreenPoint, keyboardPan: new Vector2(1f, 0f));
            yield return null;

            Assert.That(fixture.CameraRig.position, Is.Not.EqualTo(cameraRigBefore));
            Vector3 cameraRigAfterPan = fixture.CameraRig.position;
            fixture.SurfaceProxy.ApplyFollow();
            fixture.AssertVector3(fixture.CameraRig.position, cameraRigAfterPan);
            Assert.That(fixture.SurfaceProxy.SurfaceTransform.position.x, Is.EqualTo(fixture.CameraRig.position.x).Within(Tolerance));
            Assert.That(fixture.SurfaceProxy.SurfaceTransform.position.z, Is.EqualTo(fixture.CameraRig.position.z).Within(Tolerance));
            Assert.That(fixture.SurfaceProxy.SurfaceTransform.position.y, Is.EqualTo(fixture.SurfaceProxy.SurfaceHeight).Within(Tolerance));
            fixture.AssertAcceptedViewPositions(before);
            fixture.AssertStateUnchanged(before);
        }

        [UnityTest]
        public IEnumerator Scene_StateIdentityRemainsExactAfterAcceptedOperations()
        {
            yield return LoadFixture();
            CardInstanceState cardState = fixture.CardState;
            PawnState pawnState = fixture.PawnState;
            TokenState tokenState = fixture.TokenState;

            fixture.AssertAcceptedMove(fixture.CardView, new TableCoordinate(0d, 0d));
            fixture.Click(fixture.PawnView);
            fixture.ApplyWheelFrame(-ScrollDelta);
            fixture.Click(fixture.CardView);
            fixture.ApplyFrame(fixture.ScreenPointFor(fixture.CardView), flipPressedThisFrame: true);

            Assert.That(fixture.Composition.CardState, Is.SameAs(cardState));
            Assert.That(fixture.CardView.CardState, Is.SameAs(cardState));
            Assert.That(fixture.Composition.PawnState, Is.SameAs(pawnState));
            Assert.That(fixture.PawnView.PawnState, Is.SameAs(pawnState));
            Assert.That(fixture.Composition.TokenState, Is.SameAs(tokenState));
            Assert.That(fixture.TokenView.TokenState, Is.SameAs(tokenState));
            Assert.That(fixture.CardView.BoundState, Is.SameAs(fixture.Composition.MatchState.GetObject(cardState.BaseState.Id)));
            Assert.That(fixture.PawnView.BoundState, Is.SameAs(fixture.Composition.MatchState.GetObject(pawnState.BaseState.Id)));
            Assert.That(fixture.TokenView.BoundState, Is.SameAs(fixture.Composition.MatchState.GetObject(tokenState.BaseState.Id)));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
        }

        [UnityTest]
        public IEnumerator Scene_BoundaryComponentsAreAbsent()
        {
            yield return LoadFixture();

            Assert.That(fixture.GetComponentsInScene<PlayerInput>(), Is.Empty);
            Assert.That(fixture.FindMonoBehavioursContaining("EventSystem"), Is.EqualTo(0));
            Assert.That(fixture.GetComponentsInScene<Rigidbody>(), Is.Empty);
            Assert.That(fixture.SurfaceProxy.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(fixture.FindMonoBehavioursContaining("Network"), Is.EqualTo(0));
            Assert.That(fixture.FindMonoBehavioursContaining("ServiceLocator"), Is.EqualTo(0));
            Assert.That(fixture.FindMonoBehavioursContaining("ViewRegistry"), Is.EqualTo(0));
            Assert.That(fixture.FindMonoBehavioursContaining("PlayArea"), Is.EqualTo(0));
            Assert.That(fixture.FindMonoBehavioursContaining("GameTemplate"), Is.EqualTo(0));
        }

        private IEnumerator LoadFixture()
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(ScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0), "TabletopPrototype scene is not in build settings.");

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;

            Scene scene = SceneManager.GetSceneByBuildIndex(buildIndex);
            Assert.That(scene.IsValid(), Is.True);
            loadedScene = scene;
            fixture = new SceneFixture(scene);
            fixture.Composition.LoadGameTemplate(
                TrapFloorTemplateFactory.CreateStandardFourPlayer().Template.Id);
            fixture.AssertInitialized();
            Physics.SyncTransforms();
        }

        private sealed class SceneFixture
        {
            public SceneFixture(Scene scene)
            {
                Scene = scene;
                CameraRigObject = FindPath(scene, "CameraRig");
                MainCameraObject = FindPath(scene, "CameraRig/Main Camera");
                CompositionObject = FindPath(scene, "Interaction/PrototypeComposition");
                TabletopInputObject = FindPath(scene, "Interaction/TabletopInput");
                CardObject = FindPath(scene, "TabletopObjects/PrototypeCard");
                PawnObject = FindPath(scene, "TabletopObjects/PrototypePawn");
                TokenObject = FindPath(scene, "TabletopObjects/PrototypeToken");
                CardHighlightRoot = FindPath(scene, "TabletopObjects/PrototypeCard/SelectionHighlightRoot");
                PawnHighlightRoot = FindPath(scene, "TabletopObjects/PrototypePawn/SelectionHighlightRoot");
                TokenHighlightRoot = FindPath(scene, "TabletopObjects/PrototypeToken/SelectionHighlightRoot");
                CardFaceUpRoot = FindPath(scene, "TabletopObjects/PrototypeCard/FaceUpVisualRoot");
                CardFaceDownRoot = FindPath(scene, "TabletopObjects/PrototypeCard/FaceDownVisualRoot");
                SurfaceProxyObject = FindPath(scene, "Environment/TableSurfaceProxy");

                MainCamera = MainCameraObject.GetComponent<UnityEngine.Camera>();
                CameraRig = CameraRigObject.transform;
                CameraController = CameraRigObject.GetComponent<TabletopCameraController>();
                CameraAdapter = CameraRigObject.GetComponent<TabletopCameraInputAdapter>();
                Composition = CompositionObject.GetComponent<TabletopPrototypeComposition>();
                FrameCoordinator = TabletopInputObject.GetComponent<TabletopInputFrameCoordinator>();
                ObjectAdapter = TabletopInputObject.GetComponent<TabletopObjectInputAdapter>();
                CardView = CardObject.GetComponent<CardView>();
                PawnView = PawnObject.GetComponent<PawnView>();
                TokenView = TokenObject.GetComponent<TokenView>();
                CardSelectionVisual = CardObject.GetComponent<TabletopSelectionVisual>();
                PawnSelectionVisual = PawnObject.GetComponent<TabletopSelectionVisual>();
                TokenSelectionVisual = TokenObject.GetComponent<TabletopSelectionVisual>();
                CardCollider = CardObject.GetComponent<Collider>();
                PawnCollider = PawnObject.GetComponent<Collider>();
                TokenCollider = TokenObject.GetComponent<Collider>();
                SurfaceProxy = SurfaceProxyObject.GetComponent<TabletopSurfaceProxy>();
            }

            public Scene Scene { get; }

            public GameObject CameraRigObject { get; }

            public GameObject MainCameraObject { get; }

            public GameObject CompositionObject { get; }

            public GameObject TabletopInputObject { get; }

            public GameObject CardObject { get; }

            public GameObject PawnObject { get; }

            public GameObject TokenObject { get; }

            public GameObject CardHighlightRoot { get; }

            public GameObject PawnHighlightRoot { get; }

            public GameObject TokenHighlightRoot { get; }

            public GameObject CardFaceUpRoot { get; }

            public GameObject CardFaceDownRoot { get; }

            public GameObject SurfaceProxyObject { get; }

            public UnityEngine.Camera MainCamera { get; }

            public Transform CameraRig { get; }

            public TabletopCameraController CameraController { get; }

            public TabletopCameraInputAdapter CameraAdapter { get; }

            public TabletopPrototypeComposition Composition { get; }

            public TabletopInputFrameCoordinator FrameCoordinator { get; }

            public TabletopObjectInputAdapter ObjectAdapter { get; }

            public CardView CardView { get; }

            public PawnView PawnView { get; }

            public TokenView TokenView { get; }

            public TabletopSelectionVisual CardSelectionVisual { get; }

            public TabletopSelectionVisual PawnSelectionVisual { get; }

            public TabletopSelectionVisual TokenSelectionVisual { get; }

            public Collider CardCollider { get; }

            public Collider PawnCollider { get; }

            public Collider TokenCollider { get; }

            public TabletopSurfaceProxy SurfaceProxy { get; }

            public CardInstanceState CardState => Composition.CardState;

            public PawnState PawnState => Composition.PawnState;

            public TokenState TokenState => Composition.TokenState;

            public TabletopSelectionState SelectionState => Composition.SelectionState;

            public LocalInteractionLockService LockService => Composition.LockService;

            public TabletopDragPreviewSession PreviewSession => Composition.PreviewSession;

            public TabletopMoveInteractionCoordinator MoveCoordinator => Composition.MoveCoordinator;

            public long MatchRevision => Composition.MatchState.Revision;

            public Vector2 EmptyScreenPoint => ScreenPointForTable(0d, 4d);

            public IEnumerator Dispose()
            {
                if (Composition != null)
                {
                    Composition.Shutdown();
                }

                if (Scene.IsValid() && Scene.isLoaded)
                {
                    AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(Scene);
                    while (unloadOperation != null && !unloadOperation.isDone)
                    {
                        yield return null;
                    }
                }
            }

            public void AssertInitialized()
            {
                Assert.That(Composition.IsInitialized, Is.True);
                Assert.That(FrameCoordinator.enabled, Is.True);
                Assert.That(ObjectAdapter.IsInitialized, Is.True);
                Assert.That(CameraAdapter.IsExternallyDrivenBy(FrameCoordinator), Is.True);
                Assert.That(ObjectAdapter.IsExternallyDrivenBy(FrameCoordinator), Is.True);
                Assert.That(CameraAdapter.HasScrollRoutingPolicy, Is.True);
                Assert.That(Composition.SelectionPresenter, Is.Not.Null);
                Assert.That(FrameCoordinator.SelectionPresenter, Is.SameAs(Composition.SelectionPresenter));
                Assert.That(CardView.IsBound, Is.True);
                Assert.That(PawnView.IsBound, Is.True);
                Assert.That(TokenView.IsBound, Is.True);
                Assert.That(CardCollider.enabled, Is.True);
                Assert.That(PawnCollider.enabled, Is.True);
                Assert.That(TokenCollider.enabled, Is.True);
                Assert.That(MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
                Assert.That(PreviewSession.IsActive, Is.False);
                Assert.That(LockService.Count, Is.EqualTo(0));
            }

            public MoveInteractionReleaseResult? ApplyFrame(
                Vector2 screenPosition,
                bool selectPressedThisFrame = false,
                bool selectHeld = false,
                bool selectReleasedThisFrame = false,
                bool cancelPressedThisFrame = false,
                float rotateDelta = 0f,
                bool flipPressedThisFrame = false,
                float scrollDelta = 0f,
                Vector2 keyboardPan = default,
                bool dragHeld = false,
                Vector2 pointerDelta = default)
            {
                MoveInteractionReleaseResult? result = FrameCoordinator.ApplyInputFrame(
                    new TabletopInputFrame(
                        keyboardPan,
                        dragHeld,
                        pointerDelta,
                        scrollDelta,
                        screenPosition,
                        selectPressedThisFrame,
                        selectHeld,
                        selectReleasedThisFrame,
                        cancelPressedThisFrame,
                        rotateDelta,
                        flipPressedThisFrame),
                    DeltaTime);
                Physics.SyncTransforms();
                return result;
            }

            public void ApplyWheelFrame(float delta)
            {
                ApplyFrame(EmptyScreenPoint, rotateDelta: delta, scrollDelta: delta);
            }

            public void Press(TabletopObjectView view)
            {
                ApplyFrame(ScreenPointFor(view), selectPressedThisFrame: true);
            }

            public void HoldAt(Vector2 screenPosition)
            {
                ApplyFrame(screenPosition, selectHeld: true);
            }

            public MoveInteractionReleaseResult? ReleaseAt(Vector2 screenPosition)
            {
                return ApplyFrame(screenPosition, selectReleasedThisFrame: true);
            }

            public void CancelAt(Vector2 screenPosition)
            {
                ApplyFrame(screenPosition, cancelPressedThisFrame: true);
            }

            public MoveInteractionReleaseResult? Click(TabletopObjectView view)
            {
                Press(view);
                return ReleaseAt(ScreenPointFor(view));
            }

            public void ClickEmptyTable()
            {
                ApplyFrame(EmptyScreenPoint, selectPressedThisFrame: true);
                ApplyFrame(EmptyScreenPoint, selectReleasedThisFrame: true);
            }

            public void BeginDrag(TabletopObjectView view, TableCoordinate target)
            {
                Press(view);
                HoldAt(ScreenPointForTable(target.X, target.Y));
            }

            public void AssertAcceptedMove(TabletopObjectView view, TableCoordinate target)
            {
                StateSnapshot before = CaptureState();
                TabletopObjectState targetState = view.BoundState;
                TabletopPose originalPose = targetState.Pose;
                Vector3 originalScale = view.transform.localScale;

                Press(view);
                HoldAt(ScreenPointForTable(target.X, target.Y));
                MoveInteractionReleaseResult? releaseResult = ReleaseAt(ScreenPointForTable(target.X, target.Y));

                Assert.That(releaseResult.HasValue, Is.True);
                Assert.That(releaseResult.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
                AssertTableCoordinate(targetState.Pose.Position, target);
                Assert.That(targetState.Pose.RotationDegrees, Is.EqualTo(originalPose.RotationDegrees).Within(Tolerance));
                Assert.That(MatchRevision, Is.EqualTo(before.Revision + 1));
                AssertTablePosition(view.transform.position, target);
                Assert.That(PreviewSession.IsActive, Is.False);
                Assert.That(LockService.Count, Is.EqualTo(0));
                Assert.That(MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
                AssertSelection(view);
                AssertOnlyHighlightFor(view);
                AssertVector3(view.transform.localScale, originalScale);
                AssertOtherObjectsUnchanged(before, view);
            }

            public void AssertSelectedScrollRotates(
                TabletopObjectView view,
                TabletopObjectState state,
                float rawScrollDelta,
                float expectedRotationDelta)
            {
                Click(view);
                float cameraSize = MainCamera.orthographicSize;
                float previousRotation = state.Pose.RotationDegrees;
                long revision = MatchRevision;

                ApplyWheelFrame(rawScrollDelta);

                Assert.That(ObjectAdapter.LastRotationResult.HasValue, Is.True);
                Assert.That(ObjectAdapter.LastRotationResult.Value.Status, Is.EqualTo(RotationInteractionStatus.RotationAccepted));
                Assert.That(state.Pose.RotationDegrees, Is.EqualTo(previousRotation + expectedRotationDelta).Within(Tolerance));
                Assert.That(view.transform.rotation.eulerAngles.y, Is.EqualTo(NormalizeDegrees(previousRotation + expectedRotationDelta)).Within(Tolerance));
                Assert.That(MainCamera.orthographicSize, Is.EqualTo(cameraSize).Within(Tolerance));
                Assert.That(MatchRevision, Is.EqualTo(revision + 1));
                AssertSelection(view);
                AssertOnlyHighlightFor(view);
                Assert.That(LockService.Count, Is.EqualTo(0));
                Assert.That(PreviewSession.IsActive, Is.False);
            }

            public Vector2 ScreenPointFor(TabletopObjectView view)
            {
                Vector3 screenPoint = MainCamera.WorldToScreenPoint(view.transform.position);
                return new Vector2(screenPoint.x, screenPoint.y);
            }

            public Vector2 ScreenPointForTable(double x, double y)
            {
                Vector3 worldPosition = Composition.CoordinateConverter.ToWorldPosition(new TableCoordinate(x, y));
                Vector3 screenPoint = MainCamera.WorldToScreenPoint(worldPosition);
                return new Vector2(screenPoint.x, screenPoint.y);
            }

            public void AssertSelection(TabletopObjectView expected)
            {
                Assert.That(SelectionState.HasSelection, Is.True);
                Assert.That(SelectionState.SelectedView, Is.SameAs(expected));
            }

            public void AssertHighlights(bool card, bool pawn, bool token)
            {
                Assert.That(CardHighlightRoot.activeSelf, Is.EqualTo(card));
                Assert.That(PawnHighlightRoot.activeSelf, Is.EqualTo(pawn));
                Assert.That(TokenHighlightRoot.activeSelf, Is.EqualTo(token));
            }

            public void AssertOnlyHighlightFor(TabletopObjectView view)
            {
                AssertHighlights(
                    ReferenceEquals(view, CardView),
                    ReferenceEquals(view, PawnView),
                    ReferenceEquals(view, TokenView));
            }

            public StateSnapshot CaptureState()
            {
                return new StateSnapshot(
                    CardState.BaseState.Pose,
                    PawnState.BaseState.Pose,
                    TokenState.BaseState.Pose,
                    CardState.Face,
                    MatchRevision,
                    CardView.transform.position,
                    PawnView.transform.position,
                    TokenView.transform.position);
            }

            public void AssertStateUnchanged(StateSnapshot expected)
            {
                Assert.That(CardState.BaseState.Pose, Is.EqualTo(expected.CardPose));
                Assert.That(PawnState.BaseState.Pose, Is.EqualTo(expected.PawnPose));
                Assert.That(TokenState.BaseState.Pose, Is.EqualTo(expected.TokenPose));
                Assert.That(CardState.Face, Is.EqualTo(expected.CardFace));
                Assert.That(MatchRevision, Is.EqualTo(expected.Revision));
            }

            public void AssertObjectRotations(StateSnapshot expected)
            {
                Assert.That(CardState.BaseState.Pose.RotationDegrees, Is.EqualTo(expected.CardPose.RotationDegrees).Within(Tolerance));
                Assert.That(PawnState.BaseState.Pose.RotationDegrees, Is.EqualTo(expected.PawnPose.RotationDegrees).Within(Tolerance));
                Assert.That(TokenState.BaseState.Pose.RotationDegrees, Is.EqualTo(expected.TokenPose.RotationDegrees).Within(Tolerance));
            }

            public void AssertAcceptedViewPositions(StateSnapshot expected)
            {
                AssertVector3(CardView.transform.position, expected.CardPosition);
                AssertVector3(PawnView.transform.position, expected.PawnPosition);
                AssertVector3(TokenView.transform.position, expected.TokenPosition);
            }

            public void AssertOtherObjectsUnchanged(StateSnapshot expected, TabletopObjectView changedView)
            {
                if (!ReferenceEquals(changedView, CardView))
                {
                    Assert.That(CardState.BaseState.Pose, Is.EqualTo(expected.CardPose));
                    AssertVector3(CardView.transform.position, expected.CardPosition);
                }

                if (!ReferenceEquals(changedView, PawnView))
                {
                    Assert.That(PawnState.BaseState.Pose, Is.EqualTo(expected.PawnPose));
                    AssertVector3(PawnView.transform.position, expected.PawnPosition);
                }

                if (!ReferenceEquals(changedView, TokenView))
                {
                    Assert.That(TokenState.BaseState.Pose, Is.EqualTo(expected.TokenPose));
                    AssertVector3(TokenView.transform.position, expected.TokenPosition);
                }
            }

            public void AssertTablePosition(Vector3 worldPosition, TableCoordinate expected)
            {
                TableCoordinate actual = Composition.CoordinateConverter.ToTableCoordinate(worldPosition);
                AssertTableCoordinate(actual, expected);
            }

            public void AssertTableCoordinate(TableCoordinate actual, TableCoordinate expected)
            {
                Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance));
                Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance));
            }

            public void AssertVector3(Vector3 actual, Vector3 expected)
            {
                Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
                Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
                Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
            }

            public T[] GetComponentsInScene<T>() where T : Component
            {
                System.Collections.Generic.List<T> components = new System.Collections.Generic.List<T>();
                GameObject[] roots = Scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    components.AddRange(roots[i].GetComponentsInChildren<T>(true));
                }

                return components.ToArray();
            }

            public int FindMonoBehavioursContaining(string text)
            {
                int count = 0;
                MonoBehaviour[] behaviours = GetComponentsInScene<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour != null && behaviour.GetType().Name.Contains(text))
                    {
                        count++;
                    }
                }

                return count;
            }

            private static GameObject FindPath(Scene scene, string path)
            {
                string[] parts = path.Split('/');
                GameObject current = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].name == parts[0])
                    {
                        current = roots[i];
                        break;
                    }
                }

                Assert.That(current, Is.Not.Null, $"Missing scene root '{parts[0]}'.");
                for (int i = 1; i < parts.Length; i++)
                {
                    current = FindChild(current, parts[i]);
                }

                return current;
            }

            private static GameObject FindChild(GameObject parent, string name)
            {
                for (int i = 0; i < parent.transform.childCount; i++)
                {
                    Transform child = parent.transform.GetChild(i);
                    if (child.name == name)
                    {
                        return child.gameObject;
                    }
                }

                Assert.Fail($"Missing child '{name}' under '{parent.name}'.");
                return null;
            }

            private static float NormalizeDegrees(float degrees)
            {
                float normalized = degrees % 360f;
                return normalized < 0f ? normalized + 360f : normalized;
            }
        }

        private readonly struct StateSnapshot
        {
            public StateSnapshot(
                TabletopPose cardPose,
                TabletopPose pawnPose,
                TabletopPose tokenPose,
                CardFace cardFace,
                long revision,
                Vector3 cardPosition,
                Vector3 pawnPosition,
                Vector3 tokenPosition)
            {
                CardPose = cardPose;
                PawnPose = pawnPose;
                TokenPose = tokenPose;
                CardFace = cardFace;
                Revision = revision;
                CardPosition = cardPosition;
                PawnPosition = pawnPosition;
                TokenPosition = tokenPosition;
            }

            public TabletopPose CardPose { get; }

            public TabletopPose PawnPose { get; }

            public TabletopPose TokenPose { get; }

            public CardFace CardFace { get; }

            public long Revision { get; }

            public Vector3 CardPosition { get; }

            public Vector3 PawnPosition { get; }

            public Vector3 TokenPosition { get; }
        }
    }
}
