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
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopInteractionInputRoutingPolicyTests
    {
        private const int InteractionLayer = 8;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                if (createdGameObjects[i] != null)
                {
                    UnityObject.DestroyImmediate(createdGameObjects[i]);
                }
            }

            createdGameObjects.Clear();
        }

        [Test]
        public void Constructor_WithValidDependencies_StoresDependencies()
        {
            RoutingFixture fixture = CreateRoutingFixture();

            Assert.That(fixture.Policy.SelectionState, Is.SameAs(fixture.SelectionState));
            Assert.That(fixture.Policy.MoveCoordinator, Is.SameAs(fixture.MoveCoordinator));
        }

        [Test]
        public void Constructor_WhenSelectionStateIsNull_ThrowsArgumentNullException()
        {
            RoutingFixture fixture = CreateRoutingFixture();

            Assert.Throws<ArgumentNullException>(
                () => new TabletopInteractionInputRoutingPolicy(null, fixture.MoveCoordinator));
        }

        [Test]
        public void Constructor_WhenMoveCoordinatorIsNull_ThrowsArgumentNullException()
        {
            RoutingFixture fixture = CreateRoutingFixture();

            Assert.Throws<ArgumentNullException>(
                () => new TabletopInteractionInputRoutingPolicy(fixture.SelectionState, null));
        }

        [Test]
        public void ResolveScrollRoute_WhenNoSelection_ReturnsCameraZoom()
        {
            RoutingFixture fixture = CreateRoutingFixture(selectView: false);

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.CameraZoom));
        }

        [Test]
        public void ResolveScrollRoute_WhenCardIsSelected_ReturnsObjectRotation()
        {
            RoutingFixture fixture = CreateRoutingFixture(TabletopObjectKind.Card);

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.ObjectRotation));
        }

        [Test]
        public void ResolveScrollRoute_WhenPawnIsSelected_ReturnsObjectRotation()
        {
            RoutingFixture fixture = CreateRoutingFixture(TabletopObjectKind.Pawn);

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.ObjectRotation));
        }

        [Test]
        public void ResolveScrollRoute_WhenTokenIsSelected_ReturnsObjectRotation()
        {
            RoutingFixture fixture = CreateRoutingFixture(TabletopObjectKind.Token);

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.ObjectRotation));
        }

        [Test]
        public void ResolveScrollRoute_WhenSelectedObjectIsUserLocked_ReturnsObjectRotation()
        {
            RoutingFixture fixture = CreateRoutingFixture(isUserLocked: true);

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.ObjectRotation));
        }

        [Test]
        public void ResolveScrollRoute_WhenSameOwnerLockExists_ReturnsObjectRotation()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            fixture.LockService.Acquire(fixture.View.ObjectId, fixture.OwnerId);

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.ObjectRotation));
        }

        [Test]
        public void ResolveScrollRoute_WhenConflictingLockExists_ReturnsObjectRotation()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            fixture.LockService.Acquire(fixture.View.ObjectId, InteractionOwnerId.New());

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.ObjectRotation));
        }

        [Test]
        public void ResolveScrollRoute_WhenMovePressIsActive_ReturnsSuppressed()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            fixture.BeginPress();

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.Suppressed));
        }

        [Test]
        public void ResolveScrollRoute_WhenMoveDragIsActive_ReturnsSuppressed()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            fixture.BeginDrag();

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.Suppressed));
        }

        [Test]
        public void ResolveScrollRoute_WhenSelectedViewIsPreviewing_ReturnsSuppressed()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            fixture.View.ApplyPreviewPose(new TabletopPose(new TableCoordinate(3.0, 4.0), 10f, 0, 0));

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.Suppressed));
        }

        [Test]
        public void ResolveScrollRoute_WhenSelectedViewIsDestroyed_ClearsSelectionAndReturnsCameraZoom()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            UnityObject.DestroyImmediate(fixture.View.gameObject);

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.CameraZoom));
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
        }

        [Test]
        public void ResolveScrollRoute_WhenSelectedViewIsUnbound_ClearsSelectionAndReturnsCameraZoom()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            fixture.View.Unbind();

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.CameraZoom));
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
        }

        [Test]
        public void ResolveScrollRoute_WhenSelectedViewIsDisabled_ClearsSelectionAndReturnsCameraZoom()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            fixture.View.enabled = false;

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.CameraZoom));
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
        }

        [Test]
        public void ResolveScrollRoute_WhenSelectedViewIsInactive_ClearsSelectionAndReturnsCameraZoom()
        {
            RoutingFixture fixture = CreateRoutingFixture();
            fixture.View.gameObject.SetActive(false);

            Assert.That(fixture.Policy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.CameraZoom));
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
        }

        [Test]
        public void ResolveScrollRoute_DoesNotMutateRuntimeStateOrMatchRevision()
        {
            RoutingFixture fixture = CreateRoutingFixture(revision: 12);
            TabletopPose poseBefore = fixture.State.Pose;

            fixture.Policy.ResolveScrollRoute();

            Assert.That(fixture.State.Pose, Is.EqualTo(poseBefore));
            Assert.That(fixture.Match.Revision, Is.EqualTo(12));
        }

        private RoutingFixture CreateRoutingFixture(
            TabletopObjectKind selectedKind = TabletopObjectKind.Card,
            long revision = 0,
            bool selectView = true,
            bool isUserLocked = false)
        {
            Camera camera = CreateCamera();
            TabletopCoordinateConverter converter = CreateConverter();
            TabletopObjectView view = CreateBoundView(selectedKind, 1, isUserLocked, converter, out TabletopObjectState state);
            AddBoxCollider(view.gameObject, InteractionLayer);
            MatchState match = CreateMatch(selectedKind, state, revision);
            TabletopSelectionState selectionState = new TabletopSelectionState();
            if (selectView)
            {
                selectionState.Select(view);
            }

            TabletopObjectHitResolver hitResolver = new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), 25f);
            TabletopPointerProjector pointerProjector = new TabletopPointerProjector(camera, converter, 0f);
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(5f);
            TabletopDragPreviewSession previewSession = new TabletopDragPreviewSession();
            MoveObjectUseCase moveUseCase = new MoveObjectUseCase();
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
                moveUseCase);
            TabletopInteractionInputRoutingPolicy policy = new TabletopInteractionInputRoutingPolicy(
                selectionState,
                moveCoordinator);

            return new RoutingFixture(
                policy,
                moveCoordinator,
                match,
                view,
                state,
                selectionState,
                lockService,
                ownerId,
                camera);
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = CreateGameObject("Routing Camera");
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
            bool isUserLocked,
            TabletopCoordinateConverter converter,
            out TabletopObjectState state)
        {
            state = CreateBaseState(kind, seed, isUserLocked);
            switch (kind)
            {
                case TabletopObjectKind.Card:
                {
                    CardView view = CreateView<CardView>();
                    view.Bind(new CardInstanceState(state, CardFace.FaceUp), converter);
                    return view;
                }

                case TabletopObjectKind.Pawn:
                {
                    PawnView view = CreateView<PawnView>();
                    view.Bind(new PawnState(state), converter);
                    return view;
                }

                case TabletopObjectKind.Token:
                {
                    TokenView view = CreateView<TokenView>();
                    view.Bind(new TokenState(state), converter);
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

        private static MatchState CreateMatch(TabletopObjectKind kind, TabletopObjectState state, long revision)
        {
            switch (kind)
            {
                case TabletopObjectKind.Card:
                    return CreateMatch(revision, cards: new[] { new CardInstanceState(state, CardFace.FaceUp) });
                case TabletopObjectKind.Pawn:
                    return CreateMatch(revision, pawns: new[] { new PawnState(state) });
                case TabletopObjectKind.Token:
                    return CreateMatch(revision, tokens: new[] { new TokenState(state) });
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
            }
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
            bool isUserLocked)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                TabletopPose.Default,
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

        private sealed class RoutingFixture
        {
            public RoutingFixture(
                TabletopInteractionInputRoutingPolicy policy,
                TabletopMoveInteractionCoordinator moveCoordinator,
                MatchState match,
                TabletopObjectView view,
                TabletopObjectState state,
                TabletopSelectionState selectionState,
                LocalInteractionLockService lockService,
                InteractionOwnerId ownerId,
                Camera camera)
            {
                Policy = policy;
                MoveCoordinator = moveCoordinator;
                Match = match;
                View = view;
                State = state;
                SelectionState = selectionState;
                LockService = lockService;
                OwnerId = ownerId;
                Camera = camera;
            }

            public TabletopInteractionInputRoutingPolicy Policy { get; }

            public TabletopMoveInteractionCoordinator MoveCoordinator { get; }

            public MatchState Match { get; }

            public TabletopObjectView View { get; }

            public TabletopObjectState State { get; }

            public TabletopSelectionState SelectionState { get; }

            public LocalInteractionLockService LockService { get; }

            public InteractionOwnerId OwnerId { get; }

            public Camera Camera { get; }

            public void BeginPress()
            {
                Assert.That(MoveCoordinator.TryBeginPress(ScreenPointFor(View)), Is.True);
                Assert.That(MoveCoordinator.HasActiveInteraction, Is.True);
            }

            public void BeginDrag()
            {
                BeginPress();
                Assert.That(MoveCoordinator.UpdatePointer(ScreenPointForWorld(2f, 0f)), Is.True);
                Assert.That(MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
            }

            private Vector2 ScreenPointFor(TabletopObjectView view)
            {
                return ScreenPointForWorld(view.transform.position.x, view.transform.position.z);
            }

            private Vector2 ScreenPointForWorld(float x, float z)
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
