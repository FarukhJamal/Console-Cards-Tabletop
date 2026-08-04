using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class CardDropTargetResolverTests
    {
        private const int DropTargetLayer = 9;
        private const int OtherLayer = 0;
        private const float Tolerance = 0.0001f;

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
        public void Constructor_WithValidConfiguration_StoresValues()
        {
            Camera camera = CreateCamera();
            TabletopPointerProjector projector = CreateProjector(camera);
            LayerMask layerMask = LayerMaskFor(DropTargetLayer);

            CardDropTargetResolver resolver = new CardDropTargetResolver(
                camera,
                projector,
                layerMask,
                25f,
                QueryTriggerInteraction.Collide);

            Assert.That(resolver.TargetCamera, Is.SameAs(camera));
            Assert.That(resolver.PointerProjector, Is.SameAs(projector));
            Assert.That(resolver.ContainerLayerMask.value, Is.EqualTo(layerMask.value));
            Assert.That(resolver.MaximumDistance, Is.EqualTo(25f).Within(Tolerance));
            Assert.That(resolver.QueryTriggerInteraction, Is.EqualTo(QueryTriggerInteraction.Collide));
            Assert.That(resolver.HitBufferCapacity, Is.GreaterThan(0));
        }

        [Test]
        public void Constructor_WhenCameraIsNull_Rejects()
        {
            Camera camera = CreateCamera();

            Assert.Throws<ArgumentNullException>(
                () => new CardDropTargetResolver(
                    null,
                    CreateProjector(camera),
                    LayerMaskFor(DropTargetLayer),
                    25f,
                    QueryTriggerInteraction.Collide));
        }

        [Test]
        public void Constructor_WhenCameraIsPerspective_Rejects()
        {
            Camera camera = CreateCamera();
            camera.orthographic = false;

            Assert.Throws<ArgumentException>(
                () => new CardDropTargetResolver(
                    camera,
                    CreateProjector(CreateCamera()),
                    LayerMaskFor(DropTargetLayer),
                    25f,
                    QueryTriggerInteraction.Collide));
        }

        [Test]
        public void Constructor_WhenProjectorIsNull_Rejects()
        {
            Camera camera = CreateCamera();

            Assert.Throws<ArgumentNullException>(
                () => new CardDropTargetResolver(
                    camera,
                    null,
                    LayerMaskFor(DropTargetLayer),
                    25f,
                    QueryTriggerInteraction.Collide));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenMaximumDistanceIsInvalid_Rejects(float maximumDistance)
        {
            Camera camera = CreateCamera();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CardDropTargetResolver(
                    camera,
                    CreateProjector(camera),
                    LayerMaskFor(DropTargetLayer),
                    maximumDistance,
                    QueryTriggerInteraction.Collide));
        }

        [Test]
        public void EmptyLayerMask_FallsBackToTabletopProjection()
        {
            Camera camera = CreateCamera();
            CardDropTargetResolver resolver = CreateResolver(camera, 0);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_NearestValidContainerTargetResolves()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture far = CreateBoundTarget(ContainerViewKind.Deck, new Vector3(0f, 0f, 0f), DropTargetLayer);
            BoundTargetFixture near = CreateBoundTarget(ContainerViewKind.Stack, new Vector3(0f, 2f, 0f), DropTargetLayer);
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Container));
            Assert.That(target.ContainerId, Is.EqualTo(near.Container.Id));
            Assert.That(target.ContainerId, Is.Not.EqualTo(far.Container.Id));
        }

        [TestCase(ContainerViewKind.Deck)]
        [TestCase(ContainerViewKind.Stack)]
        [TestCase(ContainerViewKind.DiscardPile)]
        [TestCase(ContainerViewKind.Hand)]
        [TestCase(ContainerViewKind.ConsoleSlot)]
        public void TryResolve_ResolvesEveryContainerViewKind(ContainerViewKind viewKind)
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(viewKind, Vector3.zero, DropTargetLayer);
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, fixture.DropTarget.transform.position), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Container));
            Assert.That(target.ContainerId, Is.EqualTo(fixture.Container.Id));
        }

        [Test]
        public void TryResolve_WithChildCollider_ResolvesParentTarget()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(ContainerViewKind.Hand, Vector3.zero, DropTargetLayer, useChildCollider: true);
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, fixture.Collider.transform.position), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.ContainerId, Is.EqualTo(fixture.Container.Id));
        }

        [Test]
        public void TryResolve_WhenColliderIsOnWrongLayer_FallsBackToTabletop()
        {
            Camera camera = CreateCamera();
            CreateBoundTarget(ContainerViewKind.Deck, Vector3.zero, OtherLayer);
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_WhenColliderIsDisabled_FallsBackToTabletop()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(ContainerViewKind.Deck, Vector3.zero, DropTargetLayer);
            fixture.Collider.enabled = false;
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_WhenTargetIsInactive_FallsBackToTabletop()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(ContainerViewKind.Stack, Vector3.zero, DropTargetLayer);
            Vector2 screenPosition = ScreenPointFor(camera, fixture.DropTarget.transform.position);
            fixture.DropTarget.gameObject.SetActive(false);
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(screenPosition, out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_WhenViewIsDisabled_FallsBackToTabletop()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(ContainerViewKind.DiscardPile, Vector3.zero, DropTargetLayer);
            fixture.ViewBehaviour.enabled = false;
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_WhenViewIsUnbound_FallsBackToTabletop()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(ContainerViewKind.Hand, Vector3.zero, DropTargetLayer);
            fixture.UnbindView();
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_WhenTargetIsUnconfigured_FallsBackToTabletop()
        {
            Camera camera = CreateCamera();
            GameObject gameObject = CreateGameObject("UnconfiguredTarget");
            gameObject.layer = DropTargetLayer;
            gameObject.transform.position = Vector3.zero;
            gameObject.AddComponent<BoxCollider>();
            gameObject.AddComponent<TabletopContainerDropTarget>();
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_WhenConfiguredContainerIdIsStale_FallsBackToTabletop()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(ContainerViewKind.Deck, Vector3.zero, DropTargetLayer);
            fixture.RebindViewToNewContainer();
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_WhenSourceContainerIsExcluded_FallsBackToTabletop()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(ContainerViewKind.ConsoleSlot, Vector3.zero, DropTargetLayer);
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(
                ScreenPointFor(camera, Vector3.zero),
                fixture.Container.Id,
                out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
        }

        [Test]
        public void TryResolve_WithMultiHit_SkipsNearestInvalidAndUsesNextValid()
        {
            Camera camera = CreateCamera();
            CreateUnconfiguredTarget(new Vector3(0f, 2f, 0f), DropTargetLayer);
            BoundTargetFixture valid = CreateBoundTarget(ContainerViewKind.Stack, Vector3.zero, DropTargetLayer);
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Container));
            Assert.That(target.ContainerId, Is.EqualTo(valid.Container.Id));
        }

        [Test]
        public void TryResolve_WithMultiHit_SkipsNearestExcludedAndUsesNextValid()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture excluded = CreateBoundTarget(ContainerViewKind.Deck, new Vector3(0f, 2f, 0f), DropTargetLayer);
            BoundTargetFixture valid = CreateBoundTarget(ContainerViewKind.Hand, Vector3.zero, DropTargetLayer);
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(
                ScreenPointFor(camera, Vector3.zero),
                excluded.Container.Id,
                out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Container));
            Assert.That(target.ContainerId, Is.EqualTo(valid.Container.Id));
        }

        [Test]
        public void TryResolve_EmptyTabletopArea_ResolvesTabletopPoseFromMathematicalPlane()
        {
            Camera camera = CreateCamera();
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(ScreenPointFor(camera, new Vector3(2f, 0f, -3f)), out CardDropTarget target);

            Assert.That(resolved, Is.True);
            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
            Assert.That(target.TabletopPose.Position.X, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(target.TabletopPose.Position.Y, Is.EqualTo(-3.0).Within(Tolerance));
            Assert.That(target.TabletopPose.RotationDegrees, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(target.TabletopPose.Layer, Is.EqualTo(0));
            Assert.That(target.TabletopPose.LocalOrder, Is.EqualTo(0));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(0f, float.NegativeInfinity)]
        public void TryResolve_WhenScreenPointIsNonFinite_Rejects(float x, float y)
        {
            Camera camera = CreateCamera();
            CardDropTargetResolver resolver = CreateResolver(camera);

            Assert.Throws<ArgumentOutOfRangeException>(() => resolver.TryResolve(new Vector2(x, y), out _));
        }

        [Test]
        public void TryResolve_WhenRayCannotHitTabletop_ReturnsFalseAndNone()
        {
            Camera camera = CreateCamera(new Vector3(0f, -10f, 0f), Quaternion.Euler(90f, 0f, 0f));
            CardDropTargetResolver resolver = CreateResolver(camera);

            bool resolved = resolver.TryResolve(new Vector2(100f, 100f), out CardDropTarget target);

            Assert.That(resolved, Is.False);
            Assert.That(target, Is.EqualTo(CardDropTarget.None()));
        }

        [Test]
        public void TryResolve_DoesNotMutateRuntimeStateViewLayoutOrMatchRevision()
        {
            Camera camera = CreateCamera();
            BoundTargetFixture fixture = CreateBoundTarget(ContainerViewKind.Deck, Vector3.zero, DropTargetLayer, cardCount: 1);
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.Empty,
                7,
                fixture.Cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                new[] { fixture.Container },
                Array.Empty<ConsoleCards.Core.Domain.Seats.SeatState>(),
                new[] { fixture.Placement });
            IReadOnlyList<TabletopObjectId> order = new List<TabletopObjectId>(fixture.Container.ObjectIds);
            TabletopPose placementPose = fixture.Placement.Pose;
            TabletopPose cardPose = fixture.Cards[0].BaseState.Pose;
            Vector3 cardPosition = fixture.CardViews[0].transform.position;
            CardDropTargetResolver resolver = CreateResolver(camera);

            resolver.TryResolve(ScreenPointFor(camera, Vector3.zero), out _);

            Assert.That(match.Revision, Is.EqualTo(7));
            Assert.That(fixture.Container.ObjectIds, Is.EqualTo(order));
            Assert.That(fixture.Placement.Pose, Is.EqualTo(placementPose));
            Assert.That(fixture.Cards[0].BaseState.Pose, Is.EqualTo(cardPose));
            AssertVector3(fixture.CardViews[0].transform.position, cardPosition);
        }

        [Test]
        public void ProductionSource_DoesNotUseForbiddenBoundaries()
        {
            string[] paths =
            {
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "CardDropTarget.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "CardDropTargetResolver.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "TabletopContainerDropTarget.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Views", "Containers", "IContainerView.cs")
            };

            string source = string.Join(Environment.NewLine, Array.ConvertAll(paths, File.ReadAllText));

            Assert.That(source, Does.Not.Contain("FindObjectOfType"));
            Assert.That(source, Does.Not.Contain("FindObjectsByType"));
            Assert.That(source, Does.Not.Contain("Camera.main"));
            Assert.That(source, Does.Not.Contain("UnityEngine.InputSystem"));
            Assert.That(source, Does.Not.Contain("TransferCardUseCase"));
            Assert.That(source, Does.Not.Contain("MoveObjectCommand"));
            Assert.That(source, Does.Not.Contain("SetContainer("));
            Assert.That(source, Does.Not.Contain("SetPose("));
            Assert.That(source, Does.Not.Contain("AdvanceRevision"));
        }

        [Test]
        public void HitBuffer_IsInstanceLocal()
        {
            Camera camera = CreateCamera();
            CardDropTargetResolver first = CreateResolver(camera);
            CardDropTargetResolver second = CreateResolver(camera);
            FieldInfo field = typeof(CardDropTargetResolver).GetField(
                "hitBuffer",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.GetValue(first), Is.Not.SameAs(field.GetValue(second)));
        }

        private BoundTargetFixture CreateBoundTarget(
            ContainerViewKind viewKind,
            Vector3 targetPosition,
            int layer,
            bool useChildCollider = false,
            int cardCount = 0)
        {
            BoundViewFixture viewFixture = CreateBoundView(viewKind, cardCount);
            GameObject targetObject = CreateGameObject($"{viewKind}DropTarget");
            targetObject.layer = layer;
            targetObject.transform.position = targetPosition;
            TabletopContainerDropTarget dropTarget = targetObject.AddComponent<TabletopContainerDropTarget>();
            BoxCollider collider;
            if (useChildCollider)
            {
                GameObject child = CreateGameObject($"{viewKind}ChildCollider");
                child.layer = layer;
                child.transform.SetParent(targetObject.transform, false);
                child.transform.localPosition = Vector3.zero;
                collider = child.AddComponent<BoxCollider>();
            }
            else
            {
                collider = targetObject.AddComponent<BoxCollider>();
            }

            collider.size = Vector3.one;
            dropTarget.Configure(viewFixture.ContainerView, collider);
            return new BoundTargetFixture(viewFixture, dropTarget, collider);
        }

        private void CreateUnconfiguredTarget(Vector3 targetPosition, int layer)
        {
            GameObject targetObject = CreateGameObject("UnconfiguredTarget");
            targetObject.layer = layer;
            targetObject.transform.position = targetPosition;
            BoxCollider collider = targetObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            targetObject.AddComponent<TabletopContainerDropTarget>();
        }

        private BoundViewFixture CreateBoundView(ContainerViewKind viewKind, int cardCount)
        {
            ContainerKind kind = ToContainerKind(viewKind);
            ContainerState container = new ContainerState(ContainerId.New(), kind, SeatId.Empty, ObjectVisibility.Public, 0);
            TabletopCoordinateConverter converter = CreateConverter();
            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<CardView> cardViews = new List<CardView>();
            ContainerTransferService transferService = new ContainerTransferService();
            for (int i = 0; i < cardCount; i++)
            {
                CardInstanceState card = CreateCard(i + 80);
                transferService.PlaceIntoContainer(card.BaseState, container);
                CardView cardView = CreateGameObject($"Card{i}").AddComponent<CardView>();
                cardView.Bind(card, converter);
                cards.Add(card);
                cardViews.Add(cardView);
            }

            GameObject viewObject = CreateGameObject($"{viewKind}View");
            switch (viewKind)
            {
                case ContainerViewKind.Deck:
                    DeckView deckView = viewObject.AddComponent<DeckView>();
                    ContainerPlacementState deckPlacement = new ContainerPlacementState(container.Id, CreatePose(0.0, 0.0));
                    deckView.Bind(container, deckPlacement, converter, cardViews);
                    return new BoundViewFixture(container, deckPlacement, cards, cardViews, deckView);

                case ContainerViewKind.Stack:
                    StackView stackView = viewObject.AddComponent<StackView>();
                    ContainerPlacementState stackPlacement = new ContainerPlacementState(container.Id, CreatePose(0.0, 0.0));
                    stackView.Bind(container, stackPlacement, converter, cardViews);
                    return new BoundViewFixture(container, stackPlacement, cards, cardViews, stackView);

                case ContainerViewKind.DiscardPile:
                    DiscardPileView discardPileView = viewObject.AddComponent<DiscardPileView>();
                    ContainerPlacementState discardPlacement = new ContainerPlacementState(container.Id, CreatePose(0.0, 0.0));
                    discardPileView.Bind(container, discardPlacement, converter, cardViews);
                    return new BoundViewFixture(container, discardPlacement, cards, cardViews, discardPileView);

                case ContainerViewKind.Hand:
                    HandView handView = viewObject.AddComponent<HandView>();
                    handView.Bind(container, CreateGameObject("HandAnchor").transform, converter, cardViews);
                    return new BoundViewFixture(container, null, cards, cardViews, handView);

                case ContainerViewKind.ConsoleSlot:
                    ConsoleSlotView slotView = viewObject.AddComponent<ConsoleSlotView>();
                    slotView.Bind(container, CreateGameObject("SlotAnchor").transform, converter, cardViews);
                    return new BoundViewFixture(container, null, cards, cardViews, slotView);

                default:
                    throw new ArgumentOutOfRangeException(nameof(viewKind), viewKind, "Unsupported test view kind.");
            }
        }

        private Camera CreateCamera()
        {
            return CreateCamera(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
        }

        private Camera CreateCamera(Vector3 position, Quaternion rotation)
        {
            GameObject cameraObject = CreateGameObject("DropTargetCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.SetPositionAndRotation(position, rotation);
            return camera;
        }

        private CardDropTargetResolver CreateResolver(Camera camera, int? layerMask = null)
        {
            return new CardDropTargetResolver(
                camera,
                CreateProjector(camera),
                layerMask ?? LayerMaskFor(DropTargetLayer),
                25f,
                QueryTriggerInteraction.Collide);
        }

        private static TabletopPointerProjector CreateProjector(Camera camera)
        {
            return new TabletopPointerProjector(camera, CreateConverter(), 0f);
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static Vector2 ScreenPointFor(Camera camera, Vector3 worldPosition)
        {
            Physics.SyncTransforms();
            Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
            Assert.That(IsFinite(screenPoint.x), Is.True);
            Assert.That(IsFinite(screenPoint.y), Is.True);
            return new Vector2(screenPoint.x, screenPoint.y);
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static TabletopPose CreatePose(double x, double y)
        {
            return new TabletopPose(new TableCoordinate(x, y), 0f, 0, 0);
        }

        private static ContainerKind ToContainerKind(ContainerViewKind viewKind)
        {
            switch (viewKind)
            {
                case ContainerViewKind.Deck:
                    return ContainerKind.Deck;
                case ContainerViewKind.Stack:
                    return ContainerKind.Stack;
                case ContainerViewKind.DiscardPile:
                    return ContainerKind.DiscardPile;
                case ContainerViewKind.Hand:
                    return ContainerKind.Hand;
                case ContainerViewKind.ConsoleSlot:
                    return ContainerKind.ConsoleSlot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(viewKind), viewKind, "Unsupported test view kind.");
            }
        }

        private static CardInstanceState CreateCard(int seed)
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    new TabletopObjectId(GuidFromSeed(seed)),
                    new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                    TabletopObjectKind.Card,
                    CreatePose(seed, seed + 1),
                    ContainerId.Empty,
                    PlayerId.Empty,
                    ObjectVisibility.Public,
                    false),
                CardFace.FaceUp);
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }

        public enum ContainerViewKind
        {
            Deck,
            Stack,
            DiscardPile,
            Hand,
            ConsoleSlot
        }

        private sealed class BoundViewFixture
        {
            public BoundViewFixture(
                ContainerState container,
                ContainerPlacementState placement,
                List<CardInstanceState> cards,
                List<CardView> cardViews,
                IContainerView containerView)
            {
                Container = container;
                Placement = placement;
                Cards = cards;
                CardViews = cardViews;
                ContainerView = containerView;
            }

            public ContainerState Container { get; private set; }

            public ContainerPlacementState Placement { get; private set; }

            public List<CardInstanceState> Cards { get; }

            public List<CardView> CardViews { get; }

            public IContainerView ContainerView { get; }

            public Behaviour ViewBehaviour => (Behaviour)ContainerView;

            public void UnbindView()
            {
                switch (ContainerView)
                {
                    case DeckView deckView:
                        deckView.Unbind();
                        break;
                    case StackView stackView:
                        stackView.Unbind();
                        break;
                    case DiscardPileView discardPileView:
                        discardPileView.Unbind();
                        break;
                    case HandView handView:
                        handView.Unbind();
                        break;
                    case ConsoleSlotView slotView:
                        slotView.Unbind();
                        break;
                }
            }

            public void RebindViewToNewContainer()
            {
                ContainerState newContainer = new ContainerState(
                    ContainerId.New(),
                    Container.Kind,
                    Container.OwnerSeatId,
                    Container.Visibility,
                    Container.Capacity);
                ContainerPlacementState newPlacement = Placement == null
                    ? null
                    : new ContainerPlacementState(newContainer.Id, Placement.Pose);
                TabletopCoordinateConverter converter = CreateConverter();

                switch (ContainerView)
                {
                    case DeckView deckView:
                        deckView.Bind(newContainer, newPlacement, converter, Array.Empty<CardView>());
                        break;
                    case StackView stackView:
                        stackView.Bind(newContainer, newPlacement, converter, Array.Empty<CardView>());
                        break;
                    case DiscardPileView discardPileView:
                        discardPileView.Bind(newContainer, newPlacement, converter, Array.Empty<CardView>());
                        break;
                    case HandView handView:
                        handView.Bind(newContainer, ((Component)handView).transform, converter, Array.Empty<CardView>());
                        break;
                    case ConsoleSlotView slotView:
                        slotView.Bind(newContainer, ((Component)slotView).transform, converter, Array.Empty<CardView>());
                        break;
                }

                Container = newContainer;
                Placement = newPlacement;
            }
        }

        private sealed class BoundTargetFixture
        {
            public BoundTargetFixture(
                BoundViewFixture viewFixture,
                TabletopContainerDropTarget dropTarget,
                BoxCollider collider)
            {
                ViewFixture = viewFixture;
                DropTarget = dropTarget;
                Collider = collider;
            }

            public BoundViewFixture ViewFixture { get; }

            public TabletopContainerDropTarget DropTarget { get; }

            public BoxCollider Collider { get; }

            public ContainerState Container => ViewFixture.Container;

            public ContainerPlacementState Placement => ViewFixture.Placement;

            public List<CardInstanceState> Cards => ViewFixture.Cards;

            public List<CardView> CardViews => ViewFixture.CardViews;

            public Behaviour ViewBehaviour => ViewFixture.ViewBehaviour;

            public void UnbindView()
            {
                ViewFixture.UnbindView();
            }

            public void RebindViewToNewContainer()
            {
                ViewFixture.RebindViewToNewContainer();
            }
        }
    }
}
