using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
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
    public sealed class TabletopInteractionRouterTests
    {
        private const int ObjectLayer = 8;
        private const int DropTargetLayer = 9;

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
        public void Constructor_WithValidDependencies_StartsWithNoActiveRoute()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.Router.HasActiveInteraction, Is.False);
        }

        [TestCase(ConstructorDependency.HitResolver)]
        [TestCase(ConstructorDependency.MoveCoordinator)]
        [TestCase(ConstructorDependency.ContainedCoordinator)]
        [TestCase(ConstructorDependency.SelectionState)]
        public void Constructor_WhenDependencyIsNull_Rejects(ConstructorDependency dependency)
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.Clear(dependency);

            Assert.Throws<ArgumentNullException>(() => dependencies.CreateRouter());
        }

        [Test]
        public void TabletopInteractionReleaseResult_FactoriesExposeApprovedShapes()
        {
            MoveInteractionReleaseResult move = MoveInteractionReleaseResult.ClickCompleted();
            ContainedCardDragReleaseResult contained = ContainedCardDragReleaseResult.ClickReleased();

            TabletopInteractionReleaseResult none = TabletopInteractionReleaseResult.NoActiveInteraction();
            TabletopInteractionReleaseResult moveResult = TabletopInteractionReleaseResult.FromMove(move);
            TabletopInteractionReleaseResult containedResult = TabletopInteractionReleaseResult.FromContainedCard(contained);

            Assert.That(none.Route, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(none.HadActiveInteraction, Is.False);
            Assert.That(none.MoveResult.HasValue, Is.False);
            Assert.That(none.ContainedCardResult.HasValue, Is.False);
            Assert.That(moveResult.Route, Is.EqualTo(TabletopInteractionRoute.TabletopMove));
            Assert.That(moveResult.HadActiveInteraction, Is.True);
            Assert.That(moveResult.MoveResult.Value, Is.EqualTo(move));
            Assert.That(moveResult.ContainedCardResult.HasValue, Is.False);
            Assert.That(containedResult.Route, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
            Assert.That(containedResult.HadActiveInteraction, Is.True);
            Assert.That(containedResult.MoveResult.HasValue, Is.False);
            Assert.That(containedResult.ContainedCardResult.Value, Is.EqualTo(contained));
        }

        [Test]
        public void TabletopInteractionReleaseResult_EqualityOperatorsHashAndToStringBehaveCorrectly()
        {
            TabletopInteractionReleaseResult first =
                TabletopInteractionReleaseResult.FromMove(MoveInteractionReleaseResult.ClickCompleted());
            TabletopInteractionReleaseResult second =
                TabletopInteractionReleaseResult.FromMove(MoveInteractionReleaseResult.ClickCompleted());
            TabletopInteractionReleaseResult different =
                TabletopInteractionReleaseResult.FromContainedCard(ContainedCardDragReleaseResult.ClickReleased());

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain(nameof(TabletopInteractionRoute.TabletopMove)));
        }

        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void TryBegin_WhenContainedCardIsHit_RoutesToContainedCardDrag(ContainerKind sourceKind)
        {
            RouterFixture fixture = CreateFixture(sourceKind);

            bool began = fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ContainedCardView));

            Assert.That(began, Is.True);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.True);
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.ContainedCardView));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [TestCase(ContainedBeginFailure.LocalLockConflict)]
        [TestCase(ContainedBeginFailure.UserLockedCard)]
        [TestCase(ContainedBeginFailure.MissingSourceLayout)]
        public void TryBegin_WhenContainedCardBeginFails_DoesNotFallbackAndPreservesSelection(
            ContainedBeginFailure failure)
        {
            RouterFixture fixture = CreateContainedFailureFixture(failure);
            fixture.SelectionState.Select(fixture.TabletopPawnView);

            bool began = fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ContainedCardView));

            Assert.That(began, Is.False);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.TabletopPawnView));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [TestCase(TabletopHitKind.Card)]
        [TestCase(TabletopHitKind.Pawn)]
        [TestCase(TabletopHitKind.Token)]
        public void TryBegin_WhenTabletopObjectIsHit_RoutesToTabletopMove(TabletopHitKind hitKind)
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);

            bool began = fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ViewFor(hitKind)));

            Assert.That(began, Is.True);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.TabletopMove));
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.True);
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.ViewFor(hitKind)));
        }

        [Test]
        public void TryBegin_WhenEmptySpaceIsPressed_DelegatesMoveEmptySpaceBehavior()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            fixture.SelectionState.Select(fixture.TabletopPawnView);
            fixture.SelectionState.SetHovered(fixture.TabletopPawnView);

            bool began = fixture.Router.TryBegin(fixture.ScreenPointForWorld(8f, 8f));

            Assert.That(began, Is.False);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.SelectionState.HasHoveredObject, Is.False);
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
        }

        [Test]
        public void TryBegin_WhenContainedCardTransformIsOutsideLayout_StillUsesContainerIdAuthority()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            fixture.ContainedCardView.transform.position = new Vector3(-5f, 0f, 4f);
            Physics.SyncTransforms();

            bool began = fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ContainedCardView));

            Assert.That(began, Is.True);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
        }

        [Test]
        public void TryBegin_WhenTabletopCardIsAtContainerLayoutPosition_RoutesToTabletopMove()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            fixture.TabletopCardView.transform.position = fixture.ContainedCardView.transform.position + new Vector3(3f, 0f, 0f);
            Physics.SyncTransforms();

            bool began = fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.TabletopCardView));

            Assert.That(began, Is.True);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.TabletopMove));
            Assert.That(fixture.TabletopCard.BaseState.ContainerId, Is.EqualTo(ContainerId.Empty));
        }

        [Test]
        public void TryBegin_WhenRouterAlreadyActive_RejectsNewBegin()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            Assert.That(fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.TabletopCardView)), Is.True);

            Assert.Throws<InvalidOperationException>(
                () => fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ContainedCardView)));

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.TabletopMove));
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.True);
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
        }

        [TestCase(TabletopInteractionRoute.TabletopMove)]
        [TestCase(TabletopInteractionRoute.ContainedCardDrag)]
        public void UpdatePointer_ForwardsOnlyToActiveRoute(TabletopInteractionRoute route)
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            BeginRoute(fixture, route);

            fixture.Router.UpdatePointer(fixture.ScreenPointForWorld(3f, -2f));

            if (route == TabletopInteractionRoute.TabletopMove)
            {
                Assert.That(fixture.MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
                Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
                Assert.That(fixture.TabletopCardView.IsPreviewing, Is.True);
                Assert.That(fixture.ContainedCardView.IsPreviewing, Is.False);
            }
            else
            {
                Assert.That(fixture.ContainedCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
                Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
                Assert.That(fixture.ContainedCardView.IsPreviewing, Is.True);
                Assert.That(fixture.TabletopCardView.IsPreviewing, Is.False);
            }
        }

        [Test]
        public void UpdatePointer_WithoutActiveRoute_Throws()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);

            Assert.Throws<InvalidOperationException>(
                () => fixture.Router.UpdatePointer(fixture.ScreenPointForWorld(1f, 1f)));
        }

        [Test]
        public void Release_WhenNoActiveRoute_ReturnsNoActiveInteraction()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);

            TabletopInteractionReleaseResult result = fixture.Router.Release(fixture.ScreenPointForWorld(1f, 1f));

            Assert.That(result.Route, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(result.HadActiveInteraction, Is.False);
            Assert.That(result.MoveResult.HasValue, Is.False);
            Assert.That(result.ContainedCardResult.HasValue, Is.False);
        }

        [Test]
        public void Release_FromTabletopRoute_WrapsMoveResultAndClearsRoute()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            Assert.That(fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.TabletopCardView)), Is.True);

            TabletopInteractionReleaseResult result = fixture.Router.Release(fixture.ScreenPointFor(fixture.TabletopCardView));

            Assert.That(result.Route, Is.EqualTo(TabletopInteractionRoute.TabletopMove));
            Assert.That(result.HadActiveInteraction, Is.True);
            Assert.That(result.MoveResult.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.ClickCompleted));
            Assert.That(result.ContainedCardResult.HasValue, Is.False);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
        }

        [Test]
        public void Release_FromContainedRoute_WrapsContainedResultAndClearsRoute()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            Assert.That(fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ContainedCardView)), Is.True);

            TabletopInteractionReleaseResult result = fixture.Router.Release(fixture.ScreenPointFor(fixture.ContainedCardView));

            Assert.That(result.Route, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
            Assert.That(result.HadActiveInteraction, Is.True);
            Assert.That(result.MoveResult.HasValue, Is.False);
            Assert.That(result.ContainedCardResult.Value.Status, Is.EqualTo(ContainedCardDragReleaseStatus.ClickReleased));
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
        }

        [Test]
        public void Release_FromContainedDragToContainer_TransfersThroughContainedCoordinatorOnce()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
            Assert.That(fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ContainedCardView)), Is.True);
            fixture.Router.UpdatePointer(fixture.ScreenPointForWorld(3f, -2f));

            TabletopInteractionReleaseResult result = fixture.Router.Release(fixture.ScreenPointFor(fixture.DestinationViewComponent));

            Assert.That(result.Route, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
            Assert.That(result.ContainedCardResult.Value.Status, Is.EqualTo(ContainedCardDragReleaseStatus.TransferAccepted));
            Assert.That(result.ContainedCardResult.Value.TransferAttempted, Is.True);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.SourceContainer.Contains(fixture.ContainedCard.BaseState.Id), Is.False);
            Assert.That(fixture.DestinationContainer.GetObjectAt(fixture.DestinationContainer.Count - 1), Is.EqualTo(fixture.ContainedCard.BaseState.Id));
            Assert.That(fixture.SourceLayout.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.DestinationLayout.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
        }

        [Test]
        public void Release_WhenRoutedCoordinatorThrows_ClearsRouteAndDoesNotInvokeOtherCoordinator()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            Assert.That(fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ContainedCardView)), Is.True);
            fixture.SourceViewComponent.SendMessage("Unbind");

            Assert.Throws<InvalidOperationException>(
                () => fixture.Router.Release(fixture.ScreenPointFor(fixture.ContainedCardView)));

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
        }

        [TestCase(TabletopInteractionRoute.TabletopMove)]
        [TestCase(TabletopInteractionRoute.ContainedCardDrag)]
        public void Cancel_ForActiveRoute_CleansSelectedLifecycleAndPreservesSelection(TabletopInteractionRoute route)
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            BeginRoute(fixture, route);
            TabletopObjectView selected = fixture.SelectionState.SelectedView;

            fixture.Router.Cancel();

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(selected));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void Cancel_WhenNoActiveRoute_IsSafeNoOp()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            fixture.SelectionState.Select(fixture.TabletopPawnView);

            fixture.Router.Cancel();

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.TabletopPawnView));
        }

        [TestCase(TabletopInteractionRoute.TabletopMove)]
        [TestCase(TabletopInteractionRoute.ContainedCardDrag)]
        public void Reset_ForActiveRoute_CleansBothCoordinatorsAndPreservesSelection(TabletopInteractionRoute route)
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            BeginRoute(fixture, route);
            fixture.SelectionState.Select(fixture.TabletopPawnView);

            fixture.Router.Reset();

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.TabletopPawnView));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void Reset_WhenInactiveContainedCoordinatorIsExternallyActive_CleansIt()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            Assert.That(fixture.ContainedCoordinator.TryBegin(fixture.ContainedCardView, fixture.ScreenPointFor(fixture.ContainedCardView)), Is.True);

            fixture.Router.Reset();

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.None));
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void StaticBoundaryScan_RouterAddsNoInputSceneSearchOrRuntimeMutation()
        {
            string[] files =
            {
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "TabletopInteractionRouter.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "TabletopInteractionReleaseResult.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "TabletopInteractionRoute.cs")
            };

            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                Assert.That(text, Does.Not.Contain("UnityEngine.InputSystem"));
                Assert.That(text, Does.Not.Contain("FindObjectOfType"));
                Assert.That(text, Does.Not.Contain("FindObjectsByType"));
                Assert.That(text, Does.Not.Contain("Camera.main"));
                Assert.That(text, Does.Not.Contain("SetContainer("));
                Assert.That(text, Does.Not.Contain("AdvanceRevision"));
                Assert.That(text, Does.Not.Contain("TransferCardCommand"));
                Assert.That(text, Does.Not.Contain("MoveObjectCommand"));
            }
        }

        private static void BeginRoute(
            RouterFixture fixture,
            TabletopInteractionRoute route)
        {
            switch (route)
            {
                case TabletopInteractionRoute.TabletopMove:
                    Assert.That(fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.TabletopCardView)), Is.True);
                    break;
                case TabletopInteractionRoute.ContainedCardDrag:
                    Assert.That(fixture.Router.TryBegin(fixture.ScreenPointFor(fixture.ContainedCardView)), Is.True);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, "Unsupported test route.");
            }
        }

        private RouterFixture CreateContainedFailureFixture(ContainedBeginFailure failure)
        {
            switch (failure)
            {
                case ContainedBeginFailure.LocalLockConflict:
                {
                    RouterFixture fixture = CreateFixture(ContainerKind.Deck);
                    fixture.LockService.Acquire(fixture.ContainedCard.BaseState.Id, InteractionOwnerId.New());
                    return fixture;
                }
                case ContainedBeginFailure.UserLockedCard:
                    return CreateFixture(ContainerKind.Deck, isContainedCardUserLocked: true);
                case ContainedBeginFailure.MissingSourceLayout:
                    return CreateFixture(ContainerKind.Deck, includeSourceLayoutInLookup: false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure), failure, "Unsupported failure.");
            }
        }

        private ConstructorDependencies CreateConstructorDependencies()
        {
            RouterFixture fixture = CreateFixture(ContainerKind.Deck);
            return new ConstructorDependencies
            {
                HitResolver = fixture.HitResolver,
                MoveCoordinator = fixture.MoveCoordinator,
                ContainedCoordinator = fixture.ContainedCoordinator,
                SelectionState = fixture.SelectionState
            };
        }

        private RouterFixture CreateFixture(
            ContainerKind sourceKind,
            ContainerKind destinationKind = ContainerKind.Hand,
            bool isContainedCardUserLocked = false,
            bool includeSourceLayoutInLookup = true)
        {
            Camera camera = CreateCamera();
            TabletopCoordinateConverter converter = CreateConverter();

            ContainerState sourceContainer = CreateContainer(sourceKind);
            ContainerState destinationContainer = CreateContainer(destinationKind);
            CardInstanceState containedCard = CreateCard(1, CreatePose(0d, 0d, 20f), isContainedCardUserLocked);
            CardInstanceState tabletopCard = CreateCard(2, CreatePose(3d, 0d, 15f));
            PawnState tabletopPawn = CreatePawn(3, CreatePose(5d, 0d, 0f));
            TokenState tabletopToken = CreateToken(4, CreatePose(7d, 0d, 0f));

            ContainerTransferService transferService = new ContainerTransferService();
            Assert.That(transferService.PlaceIntoContainer(containedCard.BaseState, sourceContainer).Succeeded, Is.True);

            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                0,
                new[] { containedCard, tabletopCard },
                new[] { tabletopPawn },
                new[] { tabletopToken },
                new[] { sourceContainer, destinationContainer },
                Array.Empty<SeatState>());

            CardView containedCardView = CreateCardView("Contained Card", containedCard, converter);
            CardView tabletopCardView = CreateCardView("Tabletop Card", tabletopCard, converter);
            PawnView tabletopPawnView = CreatePawnView("Tabletop Pawn", tabletopPawn, converter);
            TokenView tabletopTokenView = CreateTokenView("Tabletop Token", tabletopToken, converter);

            AddObjectCollider(containedCardView.gameObject);
            AddObjectCollider(tabletopCardView.gameObject);
            AddObjectCollider(tabletopPawnView.gameObject);
            AddObjectCollider(tabletopTokenView.gameObject);

            Component sourceViewComponent = CreateLayoutView(
                sourceContainer,
                converter,
                new[] { containedCardView },
                new Vector3(0f, 0f, 0f));
            Component destinationViewComponent = CreateLayoutView(
                destinationContainer,
                converter,
                new[] { containedCardView },
                new Vector3(-3f, 0f, -3f));
            CountingLayoutView sourceLayout = new CountingLayoutView((IContainerLayoutView)sourceViewComponent);
            CountingLayoutView destinationLayout = new CountingLayoutView((IContainerLayoutView)destinationViewComponent);

            AddDropTarget(sourceViewComponent);
            AddDropTarget(destinationViewComponent);

            TabletopSelectionState selectionState = new TabletopSelectionState();
            TabletopObjectHitResolver hitResolver = new TabletopObjectHitResolver(camera, LayerMaskFor(ObjectLayer), 100f);
            TabletopPointerProjector pointerProjector = new TabletopPointerProjector(camera, converter, 0f);
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            PlayerId requestedByPlayerId = PlayerId.New();
            InteractionOwnerId moveOwnerId = InteractionOwnerId.New();
            InteractionOwnerId containedOwnerId = InteractionOwnerId.New();

            TabletopMoveInteractionCoordinator moveCoordinator = new TabletopMoveInteractionCoordinator(
                match,
                requestedByPlayerId,
                moveOwnerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                new TabletopInteractionStateMachine(5f),
                new TabletopDragPreviewSession(),
                new MoveObjectUseCase());

            List<IContainerLayoutView> transferLayouts = new List<IContainerLayoutView>();
            if (includeSourceLayoutInLookup)
            {
                transferLayouts.Add(sourceLayout);
            }

            transferLayouts.Add(destinationLayout);

            CardTransferInteractionCoordinator transferCoordinator = new CardTransferInteractionCoordinator(
                match,
                requestedByPlayerId,
                containedOwnerId,
                lockService,
                new TransferCardUseCase(),
                transferLayouts);
            ContainerLayoutViewLookup layoutLookup = new ContainerLayoutViewLookup(transferLayouts);
            ContainedCardDragCoordinator containedCoordinator = new ContainedCardDragCoordinator(
                containedOwnerId,
                lockService,
                new TabletopInteractionStateMachine(5f),
                new TabletopDragPreviewSession(),
                pointerProjector,
                new CardDropTargetResolver(
                    camera,
                    pointerProjector,
                    LayerMaskFor(DropTargetLayer),
                    100f,
                    QueryTriggerInteraction.Collide),
                transferCoordinator,
                layoutLookup);

            TabletopInteractionRouter router = new TabletopInteractionRouter(
                hitResolver,
                moveCoordinator,
                containedCoordinator,
                selectionState);

            return new RouterFixture(
                router,
                hitResolver,
                moveCoordinator,
                containedCoordinator,
                selectionState,
                lockService,
                match,
                sourceContainer,
                destinationContainer,
                containedCard,
                tabletopCard,
                containedCardView,
                tabletopCardView,
                tabletopPawnView,
                tabletopTokenView,
                sourceLayout,
                destinationLayout,
                sourceViewComponent,
                destinationViewComponent,
                camera);
        }

        private Component CreateLayoutView(
            ContainerState container,
            TabletopCoordinateConverter converter,
            IReadOnlyList<CardView> cardViews,
            Vector3 anchorPosition)
        {
            switch (container.Kind)
            {
                case ContainerKind.Deck:
                {
                    DeckView view = CreateGameObject("DeckView").AddComponent<DeckView>();
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(anchorPosition.x, anchorPosition.z, 0f)), converter, cardViews);
                    return view;
                }
                case ContainerKind.Stack:
                {
                    StackView view = CreateGameObject("StackView").AddComponent<StackView>();
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(anchorPosition.x, anchorPosition.z, 0f)), converter, cardViews);
                    return view;
                }
                case ContainerKind.DiscardPile:
                {
                    DiscardPileView view = CreateGameObject("DiscardPileView").AddComponent<DiscardPileView>();
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(anchorPosition.x, anchorPosition.z, 0f)), converter, cardViews);
                    return view;
                }
                case ContainerKind.Hand:
                {
                    HandView view = CreateGameObject("HandView").AddComponent<HandView>();
                    Transform anchor = CreateGameObject("HandAnchor").transform;
                    anchor.position = anchorPosition;
                    view.Bind(container, anchor, converter, cardViews);
                    return view;
                }
                case ContainerKind.ConsoleSlot:
                {
                    ConsoleSlotView view = CreateGameObject("ConsoleSlotView").AddComponent<ConsoleSlotView>();
                    Transform anchor = CreateGameObject("SlotAnchor").transform;
                    anchor.position = anchorPosition;
                    view.Bind(container, anchor, converter, cardViews);
                    return view;
                }
                default:
                    throw new ArgumentException("Unsupported container kind for router tests.", nameof(container));
            }
        }

        private void AddDropTarget(Component viewComponent)
        {
            viewComponent.gameObject.layer = DropTargetLayer;
            BoxCollider collider = viewComponent.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 0.2f, 2f);
            TabletopContainerDropTarget dropTarget = viewComponent.gameObject.AddComponent<TabletopContainerDropTarget>();
            dropTarget.Configure((IContainerView)viewComponent, collider);
        }

        private CardView CreateCardView(
            string name,
            CardInstanceState card,
            TabletopCoordinateConverter converter)
        {
            CardView view = CreateGameObject(name).AddComponent<CardView>();
            view.Bind(card, converter);
            return view;
        }

        private PawnView CreatePawnView(
            string name,
            PawnState pawn,
            TabletopCoordinateConverter converter)
        {
            PawnView view = CreateGameObject(name).AddComponent<PawnView>();
            view.Bind(pawn, converter);
            return view;
        }

        private TokenView CreateTokenView(
            string name,
            TokenState token,
            TabletopCoordinateConverter converter)
        {
            TokenView view = CreateGameObject(name).AddComponent<TokenView>();
            view.Bind(token, converter);
            return view;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = CreateGameObject("Interaction Router Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 10f, 0f),
                Quaternion.Euler(90f, 0f, 0f));
            return camera;
        }

        private static BoxCollider AddObjectCollider(GameObject gameObject)
        {
            gameObject.layer = ObjectLayer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
            return collider;
        }

        private static ContainerState CreateContainer(ContainerKind kind)
        {
            return new ContainerState(ContainerId.New(), kind, SeatId.Empty, ObjectVisibility.Public, 0);
        }

        private static CardInstanceState CreateCard(
            int seed,
            TabletopPose pose,
            bool isUserLocked = false)
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    new TabletopObjectId(GuidFromSeed(seed)),
                    new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                    TabletopObjectKind.Card,
                    pose,
                    ContainerId.Empty,
                    PlayerId.Empty,
                    ObjectVisibility.Public,
                    isUserLocked),
                CardFace.FaceUp);
        }

        private static PawnState CreatePawn(int seed, TabletopPose pose)
        {
            return new PawnState(new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                TabletopObjectKind.Pawn,
                pose,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false));
        }

        private static TokenState CreateToken(int seed, TabletopPose pose)
        {
            return new TokenState(new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                TabletopObjectKind.Token,
                pose,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false));
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static TabletopPose CreatePose(
            double x,
            double y,
            float rotationDegrees)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, 0, 0);
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        public enum ConstructorDependency
        {
            HitResolver,
            MoveCoordinator,
            ContainedCoordinator,
            SelectionState
        }

        public enum ContainedBeginFailure
        {
            LocalLockConflict,
            UserLockedCard,
            MissingSourceLayout
        }

        public enum TabletopHitKind
        {
            Card,
            Pawn,
            Token
        }

        private sealed class ConstructorDependencies
        {
            public TabletopObjectHitResolver HitResolver { get; set; }

            public TabletopMoveInteractionCoordinator MoveCoordinator { get; set; }

            public ContainedCardDragCoordinator ContainedCoordinator { get; set; }

            public TabletopSelectionState SelectionState { get; set; }

            public TabletopInteractionRouter CreateRouter()
            {
                return new TabletopInteractionRouter(
                    HitResolver,
                    MoveCoordinator,
                    ContainedCoordinator,
                    SelectionState);
            }

            public void Clear(ConstructorDependency dependency)
            {
                switch (dependency)
                {
                    case ConstructorDependency.HitResolver:
                        HitResolver = null;
                        break;
                    case ConstructorDependency.MoveCoordinator:
                        MoveCoordinator = null;
                        break;
                    case ConstructorDependency.ContainedCoordinator:
                        ContainedCoordinator = null;
                        break;
                    case ConstructorDependency.SelectionState:
                        SelectionState = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dependency), dependency, "Unsupported dependency.");
                }
            }
        }

        private sealed class RouterFixture
        {
            public RouterFixture(
                TabletopInteractionRouter router,
                TabletopObjectHitResolver hitResolver,
                TabletopMoveInteractionCoordinator moveCoordinator,
                ContainedCardDragCoordinator containedCoordinator,
                TabletopSelectionState selectionState,
                LocalInteractionLockService lockService,
                MatchState match,
                ContainerState sourceContainer,
                ContainerState destinationContainer,
                CardInstanceState containedCard,
                CardInstanceState tabletopCard,
                CardView containedCardView,
                CardView tabletopCardView,
                PawnView tabletopPawnView,
                TokenView tabletopTokenView,
                CountingLayoutView sourceLayout,
                CountingLayoutView destinationLayout,
                Component sourceViewComponent,
                Component destinationViewComponent,
                Camera camera)
            {
                Router = router;
                HitResolver = hitResolver;
                MoveCoordinator = moveCoordinator;
                ContainedCoordinator = containedCoordinator;
                SelectionState = selectionState;
                LockService = lockService;
                Match = match;
                SourceContainer = sourceContainer;
                DestinationContainer = destinationContainer;
                ContainedCard = containedCard;
                TabletopCard = tabletopCard;
                ContainedCardView = containedCardView;
                TabletopCardView = tabletopCardView;
                TabletopPawnView = tabletopPawnView;
                TabletopTokenView = tabletopTokenView;
                SourceLayout = sourceLayout;
                DestinationLayout = destinationLayout;
                SourceViewComponent = sourceViewComponent;
                DestinationViewComponent = destinationViewComponent;
                Camera = camera;
            }

            public TabletopInteractionRouter Router { get; }

            public TabletopObjectHitResolver HitResolver { get; }

            public TabletopMoveInteractionCoordinator MoveCoordinator { get; }

            public ContainedCardDragCoordinator ContainedCoordinator { get; }

            public TabletopSelectionState SelectionState { get; }

            public LocalInteractionLockService LockService { get; }

            public MatchState Match { get; }

            public ContainerState SourceContainer { get; }

            public ContainerState DestinationContainer { get; }

            public CardInstanceState ContainedCard { get; }

            public CardInstanceState TabletopCard { get; }

            public CardView ContainedCardView { get; }

            public CardView TabletopCardView { get; }

            public PawnView TabletopPawnView { get; }

            public TokenView TabletopTokenView { get; }

            public CountingLayoutView SourceLayout { get; }

            public CountingLayoutView DestinationLayout { get; }

            public Component SourceViewComponent { get; }

            public Component DestinationViewComponent { get; }

            public Camera Camera { get; }

            public TabletopObjectView ViewFor(TabletopHitKind hitKind)
            {
                switch (hitKind)
                {
                    case TabletopHitKind.Card:
                        return TabletopCardView;
                    case TabletopHitKind.Pawn:
                        return TabletopPawnView;
                    case TabletopHitKind.Token:
                        return TabletopTokenView;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(hitKind), hitKind, "Unsupported hit kind.");
                }
            }

            public Vector2 ScreenPointFor(Component component)
            {
                return ScreenPointForWorld(component.transform.position.x, component.transform.position.z);
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

        private sealed class CountingLayoutView : IContainerLayoutView
        {
            private readonly IContainerLayoutView inner;

            public CountingLayoutView(IContainerLayoutView inner)
            {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public bool IsBound => inner.IsBound;

            public ContainerId ContainerId => inner.ContainerId;

            public ContainerState ContainerState => inner.ContainerState;

            public int ApplyCount { get; private set; }

            public void ApplyAcceptedLayout()
            {
                inner.ApplyAcceptedLayout();
                ApplyCount++;
            }
        }
    }
}
