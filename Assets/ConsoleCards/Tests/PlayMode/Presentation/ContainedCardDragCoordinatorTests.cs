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
    public sealed class ContainedCardDragCoordinatorTests
    {
        private const int DropTargetLayer = 9;
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
        public void Constructor_WithValidDependencies_StoresInitialLifecycle()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);

            Assert.That(fixture.Coordinator.InteractionOwnerId, Is.EqualTo(fixture.OwnerId));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
        }

        [TestCase(ConstructorDependency.LockService)]
        [TestCase(ConstructorDependency.StateMachine)]
        [TestCase(ConstructorDependency.PreviewSession)]
        [TestCase(ConstructorDependency.PointerProjector)]
        [TestCase(ConstructorDependency.DropTargetResolver)]
        [TestCase(ConstructorDependency.TransferCoordinator)]
        [TestCase(ConstructorDependency.LayoutLookup)]
        public void Constructor_WhenDependencyIsNull_Rejects(ConstructorDependency dependency)
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.Clear(dependency);

            Assert.Throws<ArgumentNullException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void Constructor_WhenOwnerIdIsEmpty_Rejects()
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.OwnerId = InteractionOwnerId.Empty;

            Assert.Throws<ArgumentException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void ContainedCardDragReleaseResult_FactoriesValidateCombinations()
        {
            AssertRelease(ContainedCardDragReleaseResult.ClickReleased(), ContainedCardDragReleaseStatus.ClickReleased, false, true, false);
            AssertRelease(ContainedCardDragReleaseResult.NoTarget(), ContainedCardDragReleaseStatus.NoTarget, false, true, false);
            AssertRelease(ContainedCardDragReleaseResult.SameSource(), ContainedCardDragReleaseStatus.SameSource, false, true, false);
            AssertRelease(ContainedCardDragReleaseResult.ProjectionFailed(), ContainedCardDragReleaseStatus.ProjectionFailed, false, false, false);
            AssertRelease(ContainedCardDragReleaseResult.Cancelled(), ContainedCardDragReleaseStatus.Cancelled, false, true, false);

            CardTransferInteractionResult accepted =
                CardTransferInteractionResult.TransferAccepted(TransferCardResult.Accepted(2));
            CardTransferInteractionResult rejected =
                CardTransferInteractionResult.TransferRejected(
                    TransferCardResult.Failure(CommandResultStatus.Rejected, TransferCardError.DestinationCapacityExceeded));

            AssertRelease(ContainedCardDragReleaseResult.TransferAccepted(accepted), ContainedCardDragReleaseStatus.TransferAccepted, true, true, true);
            AssertRelease(ContainedCardDragReleaseResult.TransferRejected(rejected), ContainedCardDragReleaseStatus.TransferRejected, true, false, true);
            Assert.Throws<ArgumentException>(() => ContainedCardDragReleaseResult.TransferAccepted(rejected));
            Assert.Throws<ArgumentException>(() => ContainedCardDragReleaseResult.TransferRejected(accepted));
            Assert.Throws<ArgumentException>(() => ContainedCardDragReleaseResult.FromTransferResult(CardTransferInteractionResult.NoTarget()));
        }

        [Test]
        public void ContainedCardDragReleaseResult_EqualityOperatorsHashAndToStringBehaveCorrectly()
        {
            ContainedCardDragReleaseResult first =
                ContainedCardDragReleaseResult.TransferAccepted(
                    CardTransferInteractionResult.TransferAccepted(TransferCardResult.Accepted(3)));
            ContainedCardDragReleaseResult second =
                ContainedCardDragReleaseResult.TransferAccepted(
                    CardTransferInteractionResult.TransferAccepted(TransferCardResult.Accepted(3)));
            ContainedCardDragReleaseResult different = ContainedCardDragReleaseResult.NoTarget();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain(nameof(ContainedCardDragReleaseStatus.TransferAccepted)));
        }

        [Test]
        public void TryBegin_WithValidContainedCard_BeginsPressedWithoutCommand()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand, revision: 4);

            bool began = fixture.Begin();

            Assert.That(began, Is.True);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
            Assert.That(fixture.Coordinator.ActiveCardView, Is.SameAs(fixture.ActiveCardView));
            Assert.That(fixture.Match.Revision, Is.EqualTo(4));
            Assert.That(fixture.ActiveCard.BaseState.ContainerId, Is.EqualTo(fixture.SourceContainer.Id));
            Assert.That(fixture.LockService.IsOwnedBy(fixture.ActiveCard.BaseState.Id, fixture.OwnerId), Is.True);
        }

        [TestCase(BeginRejectCase.TabletopCard)]
        [TestCase(BeginRejectCase.NullCard)]
        [TestCase(BeginRejectCase.DestroyedCard)]
        [TestCase(BeginRejectCase.DisabledCard)]
        [TestCase(BeginRejectCase.InactiveCard)]
        [TestCase(BeginRejectCase.UnboundCard)]
        [TestCase(BeginRejectCase.UserLockedCard)]
        [TestCase(BeginRejectCase.AlreadyPreviewing)]
        [TestCase(BeginRejectCase.MissingSourceLayout)]
        [TestCase(BeginRejectCase.LocalLockConflict)]
        [TestCase(BeginRejectCase.NonMatchOwnedCard)]
        public void TryBegin_WhenInvalid_ReturnsFalseWithoutLifecycleMutation(BeginRejectCase rejectCase)
        {
            DragFixture fixture = CreateBeginRejectFixture(rejectCase);
            StateSnapshot before = StateSnapshot.Capture(fixture);
            CardView cardView = rejectCase == BeginRejectCase.NullCard ? null : fixture.ActiveCardView;

            bool began = fixture.Coordinator.TryBegin(cardView, fixture.InitialScreenPoint);

            Assert.That(began, Is.False);
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            before.AssertMatches(fixture);
        }

        [Test]
        public void TryBegin_WhenSameOwnerAlreadyOwnsLock_BeginsAndPreservesLockAfterClickRelease()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
            fixture.LockService.Acquire(fixture.ActiveCard.BaseState.Id, fixture.OwnerId);

            Assert.That(fixture.Begin(), Is.True);
            ContainedCardDragReleaseResult result = fixture.Coordinator.Release(fixture.InitialScreenPoint);

            AssertRelease(result, ContainedCardDragReleaseStatus.ClickReleased, false, true, false);
            Assert.That(fixture.LockService.IsOwnedBy(fixture.ActiveCard.BaseState.Id, fixture.OwnerId), Is.True);
            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
        }

        [Test]
        public void UpdatePointer_BelowThreshold_RemainsPressedWithoutPreview()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
            fixture.Begin();

            fixture.Coordinator.UpdatePointer(fixture.InitialScreenPoint + new Vector2(1f, 0f));

            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void UpdatePointer_WhenThresholdCrosses_BeginsPreviewFromMathematicalProjection()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
            TabletopPose acceptedPose = fixture.ActiveCard.BaseState.Pose;
            fixture.Begin();

            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(4f, -3f));

            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
            Assert.That(fixture.PreviewSession.IsActive, Is.True);
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.True);
            AssertCoordinate(fixture.ActiveCardView.PreviewPose.Position, 4.0, -3.0);
            Assert.That(fixture.ActiveCardView.PreviewPose.RotationDegrees, Is.EqualTo(acceptedPose.RotationDegrees));
            Assert.That(fixture.ActiveCardView.PreviewPose.Layer, Is.EqualTo(acceptedPose.Layer));
            Assert.That(fixture.ActiveCardView.PreviewPose.LocalOrder, Is.EqualTo(acceptedPose.LocalOrder));
            Assert.That(fixture.ActiveCard.BaseState.Pose, Is.EqualTo(acceptedPose));
        }

        [Test]
        public void UpdatePointer_WhenRepeated_ReplacesPreviewWithoutRuntimeMutation()
        {
            DragFixture fixture = CreateDraggingFixture(ContainerKind.Deck, ContainerKind.Hand);
            TabletopPose acceptedPose = fixture.ActiveCard.BaseState.Pose;

            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(-2f, 5f));

            AssertCoordinate(fixture.ActiveCardView.PreviewPose.Position, -2.0, 5.0);
            Assert.That(fixture.ActiveCard.BaseState.Pose, Is.EqualTo(acceptedPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void UpdatePointer_WhenProjectionFails_CancelsAndRestoresSourceLayout()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
            fixture.Begin();
            fixture.Camera.transform.rotation = Quaternion.identity;

            fixture.Coordinator.UpdatePointer(new Vector2(20f, 20f));

            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.False);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.LockService.IsLocked(fixture.ActiveCard.BaseState.Id), Is.False);
        }

        [Test]
        public void Release_FromPressed_ReturnsClickReleaseAndRestoresSourceLayout()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand, revision: 6);
            fixture.Begin();

            ContainedCardDragReleaseResult result = fixture.Coordinator.Release(fixture.InitialScreenPoint);

            AssertRelease(result, ContainedCardDragReleaseStatus.ClickReleased, false, true, false);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Match.Revision, Is.EqualTo(6));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.LockService.IsLocked(fixture.ActiveCard.BaseState.Id), Is.False);
        }

        [TestCase(ContainerKind.Deck, ContainerKind.Hand)]
        [TestCase(ContainerKind.Hand, ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.Stack, ContainerKind.ConsoleSlot)]
        [TestCase(ContainerKind.ConsoleSlot, ContainerKind.Stack)]
        public void Release_ToContainer_TransfersExactlyOnceAndRefreshesLayouts(
            ContainerKind sourceKind,
            ContainerKind destinationKind)
        {
            DragFixture fixture = CreateDraggingFixture(sourceKind, destinationKind);

            ContainedCardDragReleaseResult result = fixture.Coordinator.Release(fixture.ScreenPointFor(fixture.DestinationViewComponent));

            AssertRelease(result, ContainedCardDragReleaseStatus.TransferAccepted, true, true, true);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.SourceContainer.Contains(fixture.ActiveCard.BaseState.Id), Is.False);
            Assert.That(fixture.DestinationContainer.GetObjectAt(fixture.DestinationContainer.Count - 1), Is.EqualTo(fixture.ActiveCard.BaseState.Id));
            Assert.That(fixture.ActiveCard.BaseState.ContainerId, Is.EqualTo(fixture.DestinationContainer.Id));
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.False);
            Assert.That(fixture.ActiveCardView.IsContainerLayoutApplied, Is.True);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.DestinationView.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.LockService.IsLocked(fixture.ActiveCard.BaseState.Id), Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Release_ToTabletop_TransfersOutAndPreservesAcceptedPoseFields(ContainerKind sourceKind)
        {
            DragFixture fixture = CreateDraggingFixture(sourceKind, ContainerKind.Hand);
            TabletopPose acceptedPose = fixture.ActiveCard.BaseState.Pose;
            Vector2 releasePoint = fixture.ScreenPointForWorld(7f, -6f);

            ContainedCardDragReleaseResult result = fixture.Coordinator.Release(releasePoint);

            AssertRelease(result, ContainedCardDragReleaseStatus.TransferAccepted, true, true, true);
            Assert.That(fixture.ActiveCard.BaseState.ContainerId, Is.EqualTo(ContainerId.Empty));
            AssertCoordinate(fixture.ActiveCard.BaseState.Pose.Position, 7.0, -6.0);
            Assert.That(fixture.ActiveCard.BaseState.Pose.RotationDegrees, Is.EqualTo(acceptedPose.RotationDegrees));
            Assert.That(fixture.ActiveCard.BaseState.Pose.Layer, Is.EqualTo(acceptedPose.Layer));
            Assert.That(fixture.ActiveCard.BaseState.Pose.LocalOrder, Is.EqualTo(acceptedPose.LocalOrder));
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.False);
            Assert.That(fixture.ActiveCardView.IsContainerLayoutApplied, Is.False);
            AssertWorldPose(fixture.ActiveCardView, fixture.ActiveCard.BaseState.Pose);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
        }

        [Test]
        public void Release_ToSameSourceContainer_CancelsWithoutTransfer()
        {
            DragFixture fixture = CreateDraggingFixture(ContainerKind.Deck, ContainerKind.Hand);
            StateSnapshot before = StateSnapshot.Capture(fixture);

            ContainedCardDragReleaseResult result = fixture.Coordinator.Release(fixture.ScreenPointFor(fixture.SourceViewComponent));

            AssertRelease(result, ContainedCardDragReleaseStatus.SameSource, false, true, false);
            before.AssertMatches(fixture, ignoreSourceApplyCount: true);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(before.SourceApplyCount + 1));
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void Release_WhenNoTarget_CancelsWithoutTransfer()
        {
            DragFixture fixture = CreateDraggingFixture(ContainerKind.Deck, ContainerKind.Hand);
            StateSnapshot before = StateSnapshot.Capture(fixture);
            fixture.Camera.transform.rotation = Quaternion.identity;

            ContainedCardDragReleaseResult result = fixture.Coordinator.Release(new Vector2(20f, 20f));

            AssertRelease(result, ContainedCardDragReleaseStatus.ProjectionFailed, false, false, false);
            before.AssertMatches(fixture, ignoreSourceApplyCount: true);
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.False);
            Assert.That(fixture.LockService.IsLocked(fixture.ActiveCard.BaseState.Id), Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void Release_WhenTransferRejected_RestoresSourceLayoutAndCleansUp()
        {
            DragFixture fixture = CreateDraggingFixture(
                ContainerKind.Deck,
                ContainerKind.ConsoleSlot,
                destinationCapacity: 1,
                destinationStartingCardCount: 1);
            StateSnapshot before = StateSnapshot.Capture(fixture);

            ContainedCardDragReleaseResult result = fixture.Coordinator.Release(fixture.ScreenPointFor(fixture.DestinationViewComponent));

            AssertRelease(result, ContainedCardDragReleaseStatus.TransferRejected, true, false, true);
            Assert.That(result.TransferResult.Value.TransferResult.Value.Error, Is.EqualTo(TransferCardError.DestinationCapacityExceeded));
            before.AssertMatches(fixture, ignoreSourceApplyCount: true);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(before.SourceApplyCount + 1));
            Assert.That(fixture.DestinationView.ApplyCount, Is.EqualTo(before.DestinationApplyCount));
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.False);
            Assert.That(fixture.LockService.IsLocked(fixture.ActiveCard.BaseState.Id), Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [TestCase(CleanupOperation.CancelPressed)]
        [TestCase(CleanupOperation.CancelDragging)]
        [TestCase(CleanupOperation.ResetPressed)]
        [TestCase(CleanupOperation.ResetDragging)]
        public void CancelAndReset_RestoreSourceLayoutWithoutRuntimeMutation(CleanupOperation operation)
        {
            DragFixture fixture = operation == CleanupOperation.CancelPressed || operation == CleanupOperation.ResetPressed
                ? CreateFixture(ContainerKind.Deck, ContainerKind.Hand)
                : CreateDraggingFixture(ContainerKind.Deck, ContainerKind.Hand);
            if (!fixture.Coordinator.HasActiveInteraction)
            {
                fixture.Begin();
            }

            StateSnapshot before = StateSnapshot.Capture(fixture);
            if (operation == CleanupOperation.CancelPressed || operation == CleanupOperation.CancelDragging)
            {
                fixture.Coordinator.Cancel();
            }
            else
            {
                fixture.Coordinator.Reset();
            }

            before.AssertMatches(fixture, ignoreSourceApplyCount: true);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(before.SourceApplyCount + 1));
            Assert.That(fixture.ActiveCardView.IsPreviewing, Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.LockService.IsLocked(fixture.ActiveCard.BaseState.Id), Is.False);
        }

        [Test]
        public void Reset_WithSameOwnerPreexistingLock_PreservesLock()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
            fixture.LockService.Acquire(fixture.ActiveCard.BaseState.Id, fixture.OwnerId);
            fixture.Begin();

            fixture.Coordinator.Reset();

            Assert.That(fixture.LockService.IsOwnedBy(fixture.ActiveCard.BaseState.Id, fixture.OwnerId), Is.True);
        }

        [Test]
        public void StaticBoundaries_DoNotAddInputSceneSearchOrRuntimeMutationShortcuts()
        {
            string[] files =
            {
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "ContainedCardDragCoordinator.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "ContainedCardDragReleaseResult.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "ContainedCardDragReleaseStatus.cs")
            };

            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                Assert.That(source, Does.Not.Contain("UnityEngine.InputSystem"));
                Assert.That(source, Does.Not.Contain("FindObjectOfType"));
                Assert.That(source, Does.Not.Contain("FindObjectsByType"));
                Assert.That(source, Does.Not.Contain("Camera.main"));
                Assert.That(source, Does.Not.Contain("SelectionState"));
                Assert.That(source, Does.Not.Contain("SetContainer("));
                Assert.That(source, Does.Not.Contain("AdvanceRevision"));
            }
        }

        private DragFixture CreateDraggingFixture(
            ContainerKind sourceKind,
            ContainerKind destinationKind,
            int destinationCapacity = 0,
            int destinationStartingCardCount = 0)
        {
            DragFixture fixture = CreateFixture(
                sourceKind,
                destinationKind,
                destinationCapacity: destinationCapacity,
                destinationStartingCardCount: destinationStartingCardCount);
            fixture.Begin();
            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(3f, 3f));
            return fixture;
        }

        private DragFixture CreateBeginRejectFixture(BeginRejectCase rejectCase)
        {
            DragFixture fixture = CreateFixture(
                ContainerKind.Deck,
                ContainerKind.Hand,
                includeSourceLayout: rejectCase != BeginRejectCase.MissingSourceLayout,
                includeActiveCardInMatch: rejectCase != BeginRejectCase.NonMatchOwnedCard,
                activeCardUserLocked: rejectCase == BeginRejectCase.UserLockedCard,
                activeCardStartsTabletop: rejectCase == BeginRejectCase.TabletopCard);

            switch (rejectCase)
            {
                case BeginRejectCase.DestroyedCard:
                    UnityObject.DestroyImmediate(fixture.ActiveCardView.gameObject);
                    break;
                case BeginRejectCase.DisabledCard:
                    fixture.ActiveCardView.enabled = false;
                    break;
                case BeginRejectCase.InactiveCard:
                    fixture.ActiveCardView.gameObject.SetActive(false);
                    break;
                case BeginRejectCase.UnboundCard:
                    fixture.ActiveCardView.Unbind();
                    break;
                case BeginRejectCase.AlreadyPreviewing:
                    fixture.ActiveCardView.ApplyPreviewPose(CreatePose(1.0, 2.0, 0f));
                    break;
                case BeginRejectCase.LocalLockConflict:
                    fixture.LockService.Acquire(fixture.ActiveCard.BaseState.Id, InteractionOwnerId.New());
                    break;
            }

            return fixture;
        }

        private DragFixture CreateFixture(
            ContainerKind sourceKind,
            ContainerKind destinationKind,
            long revision = 0,
            int destinationCapacity = 0,
            int destinationStartingCardCount = 0,
            bool includeSourceLayout = true,
            bool includeActiveCardInMatch = true,
            bool activeCardUserLocked = false,
            bool activeCardStartsTabletop = false)
        {
            Camera camera = CreateCamera();
            TabletopCoordinateConverter converter = CreateConverter();
            ContainerState source = CreateContainer(sourceKind);
            ContainerState destination = CreateContainer(destinationKind, destinationCapacity);
            CardInstanceState activeCard = CreateCard(1, CreatePose(1.0, 1.0, 30f, 2, 3), activeCardUserLocked);
            List<CardInstanceState> cards = new List<CardInstanceState>();
            if (!activeCardStartsTabletop && includeActiveCardInMatch)
            {
                new ContainerTransferService().PlaceIntoContainer(activeCard.BaseState, source);
            }
            else if (!activeCardStartsTabletop)
            {
                activeCard.BaseState.SetContainer(source.Id);
                CardInstanceState matchOwnedSourceMember = CreateCard(99, CreatePose(0.0, 0.0, 0f));
                new ContainerTransferService().PlaceIntoContainer(matchOwnedSourceMember.BaseState, source);
                cards.Add(matchOwnedSourceMember);
            }

            if (includeActiveCardInMatch)
            {
                cards.Add(activeCard);
            }

            for (int i = 0; i < destinationStartingCardCount; i++)
            {
                CardInstanceState destinationCard = CreateCard(10 + i, CreatePose(0.0, 0.0, 0f));
                new ContainerTransferService().PlaceIntoContainer(destinationCard.BaseState, destination);
                cards.Add(destinationCard);
            }

            List<ContainerState> containers = new List<ContainerState> { source, destination };
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                Array.Empty<SeatState>(),
                CreatePlacements(containers));

            List<CardView> cardViews = new List<CardView>();
            CardView activeCardView = CreateCardView("ActiveCardView", activeCard, converter);
            cardViews.Add(activeCardView);
            int firstAdditionalCardIndex = includeActiveCardInMatch ? 1 : 0;
            for (int i = firstAdditionalCardIndex; i < cards.Count; i++)
            {
                cardViews.Add(CreateCardView($"CardView{i}", cards[i], converter));
            }

            Component sourceViewComponent = CreateLayoutView(source, converter, cardViews, new Vector3(-2f, 0f, 0f));
            Component destinationViewComponent = CreateLayoutView(destination, converter, cardViews, new Vector3(4f, 0f, 0f));
            AddDropTarget(sourceViewComponent, DropTargetLayer);
            AddDropTarget(destinationViewComponent, DropTargetLayer);
            CountingLayoutView sourceView = new CountingLayoutView((IContainerLayoutView)sourceViewComponent);
            CountingLayoutView destinationView = new CountingLayoutView((IContainerLayoutView)destinationViewComponent);

            LocalInteractionLockService lockService = new LocalInteractionLockService();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            PlayerId playerId = PlayerId.New();
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(5f);
            TabletopDragPreviewSession previewSession = new TabletopDragPreviewSession();
            TabletopPointerProjector pointerProjector = new TabletopPointerProjector(camera, converter, 0f);
            CardDropTargetResolver dropTargetResolver = new CardDropTargetResolver(
                camera,
                pointerProjector,
                LayerMaskFor(DropTargetLayer),
                100f,
                QueryTriggerInteraction.Collide);
            List<IContainerLayoutView> transferViews = new List<IContainerLayoutView>();
            if (includeSourceLayout)
            {
                transferViews.Add(sourceView);
            }

            transferViews.Add(destinationView);
            CardTransferInteractionCoordinator transferCoordinator = new CardTransferInteractionCoordinator(
                match,
                playerId,
                ownerId,
                lockService,
                new TransferCardUseCase(),
                transferViews);
            ContainerLayoutViewLookup lookup = new ContainerLayoutViewLookup(transferViews);
            ContainedCardDragCoordinator coordinator = new ContainedCardDragCoordinator(
                ownerId,
                lockService,
                stateMachine,
                previewSession,
                pointerProjector,
                dropTargetResolver,
                transferCoordinator,
                lookup);

            return new DragFixture(
                coordinator,
                match,
                activeCard,
                activeCardView,
                source,
                destination,
                sourceView,
                destinationView,
                sourceViewComponent,
                destinationViewComponent,
                lockService,
                stateMachine,
                previewSession,
                camera,
                ownerId);
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
                    throw new ArgumentException("Unsupported test container kind.", nameof(container));
            }
        }

        private void AddDropTarget(Component viewComponent, int layer)
        {
            viewComponent.gameObject.layer = layer;
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

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = CreateGameObject("Contained Drag Camera");
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

        private ConstructorDependencies CreateConstructorDependencies()
        {
            DragFixture fixture = CreateFixture(ContainerKind.Deck, ContainerKind.Hand);
            return new ConstructorDependencies
            {
                OwnerId = fixture.OwnerId,
                LockService = fixture.LockService,
                StateMachine = new TabletopInteractionStateMachine(5f),
                PreviewSession = new TabletopDragPreviewSession(),
                PointerProjector = new TabletopPointerProjector(fixture.Camera, CreateConverter(), 0f),
                DropTargetResolver = new CardDropTargetResolver(
                    fixture.Camera,
                    new TabletopPointerProjector(fixture.Camera, CreateConverter(), 0f),
                    LayerMaskFor(DropTargetLayer),
                    100f,
                    QueryTriggerInteraction.Collide),
                TransferCoordinator = new CardTransferInteractionCoordinator(
                    fixture.Match,
                    PlayerId.New(),
                    fixture.OwnerId,
                    fixture.LockService,
                    new TransferCardUseCase(),
                    new IContainerLayoutView[] { fixture.SourceView, fixture.DestinationView }),
                LayoutLookup = new ContainerLayoutViewLookup(new IContainerLayoutView[] { fixture.SourceView, fixture.DestinationView })
            };
        }

        private static IReadOnlyList<ContainerPlacementState> CreatePlacements(IReadOnlyList<ContainerState> containers)
        {
            List<ContainerPlacementState> placements = new List<ContainerPlacementState>();
            for (int i = 0; i < containers.Count; i++)
            {
                if (containers[i].Kind == ContainerKind.Deck
                    || containers[i].Kind == ContainerKind.Stack
                    || containers[i].Kind == ContainerKind.DiscardPile)
                {
                    placements.Add(new ContainerPlacementState(containers[i].Id, CreatePose(i, i, 0f)));
                }
            }

            return placements;
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

        private static ContainerState CreateContainer(
            ContainerKind kind,
            int capacity = 0)
        {
            return new ContainerState(ContainerId.New(), kind, SeatId.Empty, ObjectVisibility.Public, capacity);
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static TabletopPose CreatePose(
            double x,
            double y,
            float rotation,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotation, layer, localOrder);
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void AssertRelease(
            ContainedCardDragReleaseResult result,
            ContainedCardDragReleaseStatus expectedStatus,
            bool expectedAttempted,
            bool expectedSucceeded,
            bool expectTransferResult)
        {
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.TransferAttempted, Is.EqualTo(expectedAttempted));
            Assert.That(result.Succeeded, Is.EqualTo(expectedSucceeded));
            Assert.That(result.TransferResult.HasValue, Is.EqualTo(expectTransferResult));
        }

        private static void AssertCoordinate(
            TableCoordinate coordinate,
            double expectedX,
            double expectedY)
        {
            Assert.That(coordinate.X, Is.EqualTo(expectedX).Within(0.00001d));
            Assert.That(coordinate.Y, Is.EqualTo(expectedY).Within(0.00001d));
        }

        private static void AssertWorldPose(CardView view, TabletopPose pose)
        {
            Assert.That(view.transform.position.x, Is.EqualTo((float)pose.Position.X).Within(Tolerance));
            Assert.That(view.transform.position.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(view.transform.position.z, Is.EqualTo((float)pose.Position.Y).Within(Tolerance));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, pose.RotationDegrees, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        public enum ConstructorDependency
        {
            LockService,
            StateMachine,
            PreviewSession,
            PointerProjector,
            DropTargetResolver,
            TransferCoordinator,
            LayoutLookup
        }

        public enum BeginRejectCase
        {
            TabletopCard,
            NullCard,
            DestroyedCard,
            DisabledCard,
            InactiveCard,
            UnboundCard,
            UserLockedCard,
            AlreadyPreviewing,
            MissingSourceLayout,
            LocalLockConflict,
            NonMatchOwnedCard
        }

        public enum CleanupOperation
        {
            CancelPressed,
            CancelDragging,
            ResetPressed,
            ResetDragging
        }

        private sealed class ConstructorDependencies
        {
            public InteractionOwnerId OwnerId { get; set; }

            public LocalInteractionLockService LockService { get; set; }

            public TabletopInteractionStateMachine StateMachine { get; set; }

            public TabletopDragPreviewSession PreviewSession { get; set; }

            public TabletopPointerProjector PointerProjector { get; set; }

            public CardDropTargetResolver DropTargetResolver { get; set; }

            public CardTransferInteractionCoordinator TransferCoordinator { get; set; }

            public ContainerLayoutViewLookup LayoutLookup { get; set; }

            public ContainedCardDragCoordinator CreateCoordinator()
            {
                return new ContainedCardDragCoordinator(
                    OwnerId,
                    LockService,
                    StateMachine,
                    PreviewSession,
                    PointerProjector,
                    DropTargetResolver,
                    TransferCoordinator,
                    LayoutLookup);
            }

            public void Clear(ConstructorDependency dependency)
            {
                switch (dependency)
                {
                    case ConstructorDependency.LockService:
                        LockService = null;
                        break;
                    case ConstructorDependency.StateMachine:
                        StateMachine = null;
                        break;
                    case ConstructorDependency.PreviewSession:
                        PreviewSession = null;
                        break;
                    case ConstructorDependency.PointerProjector:
                        PointerProjector = null;
                        break;
                    case ConstructorDependency.DropTargetResolver:
                        DropTargetResolver = null;
                        break;
                    case ConstructorDependency.TransferCoordinator:
                        TransferCoordinator = null;
                        break;
                    case ConstructorDependency.LayoutLookup:
                        LayoutLookup = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dependency), dependency, "Unsupported dependency.");
                }
            }
        }

        private sealed class DragFixture
        {
            public DragFixture(
                ContainedCardDragCoordinator coordinator,
                MatchState match,
                CardInstanceState activeCard,
                CardView activeCardView,
                ContainerState sourceContainer,
                ContainerState destinationContainer,
                CountingLayoutView sourceView,
                CountingLayoutView destinationView,
                Component sourceViewComponent,
                Component destinationViewComponent,
                LocalInteractionLockService lockService,
                TabletopInteractionStateMachine stateMachine,
                TabletopDragPreviewSession previewSession,
                Camera camera,
                InteractionOwnerId ownerId)
            {
                Coordinator = coordinator;
                Match = match;
                ActiveCard = activeCard;
                ActiveCardView = activeCardView;
                SourceContainer = sourceContainer;
                DestinationContainer = destinationContainer;
                SourceView = sourceView;
                DestinationView = destinationView;
                SourceViewComponent = sourceViewComponent;
                DestinationViewComponent = destinationViewComponent;
                LockService = lockService;
                StateMachine = stateMachine;
                PreviewSession = previewSession;
                Camera = camera;
                OwnerId = ownerId;
                InitialScreenPoint = ScreenPointFor(activeCardView);
            }

            public ContainedCardDragCoordinator Coordinator { get; }

            public MatchState Match { get; }

            public CardInstanceState ActiveCard { get; }

            public CardView ActiveCardView { get; }

            public ContainerState SourceContainer { get; }

            public ContainerState DestinationContainer { get; }

            public CountingLayoutView SourceView { get; }

            public CountingLayoutView DestinationView { get; }

            public Component SourceViewComponent { get; }

            public Component DestinationViewComponent { get; }

            public LocalInteractionLockService LockService { get; }

            public TabletopInteractionStateMachine StateMachine { get; }

            public TabletopDragPreviewSession PreviewSession { get; }

            public Camera Camera { get; }

            public InteractionOwnerId OwnerId { get; }

            public Vector2 InitialScreenPoint { get; }

            public bool Begin()
            {
                return Coordinator.TryBegin(ActiveCardView, InitialScreenPoint);
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
                this.inner = inner;
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

        private sealed class StateSnapshot
        {
            private readonly List<TabletopObjectId> sourceOrder;
            private readonly List<TabletopObjectId> destinationOrder;
            private readonly ContainerId cardContainerId;
            private readonly TabletopPose cardPose;
            private readonly long revision;

            private StateSnapshot(
                List<TabletopObjectId> sourceOrder,
                List<TabletopObjectId> destinationOrder,
                ContainerId cardContainerId,
                TabletopPose cardPose,
                long revision,
                int sourceApplyCount,
                int destinationApplyCount)
            {
                this.sourceOrder = sourceOrder;
                this.destinationOrder = destinationOrder;
                this.cardContainerId = cardContainerId;
                this.cardPose = cardPose;
                this.revision = revision;
                SourceApplyCount = sourceApplyCount;
                DestinationApplyCount = destinationApplyCount;
            }

            public int SourceApplyCount { get; }

            public int DestinationApplyCount { get; }

            public static StateSnapshot Capture(DragFixture fixture)
            {
                return new StateSnapshot(
                    new List<TabletopObjectId>(fixture.SourceContainer.ObjectIds),
                    new List<TabletopObjectId>(fixture.DestinationContainer.ObjectIds),
                    fixture.ActiveCard.BaseState.ContainerId,
                    fixture.ActiveCard.BaseState.Pose,
                    fixture.Match.Revision,
                    fixture.SourceView.ApplyCount,
                    fixture.DestinationView.ApplyCount);
            }

            public void AssertMatches(
                DragFixture fixture,
                bool ignoreSourceApplyCount = false,
                bool ignoreDestinationApplyCount = false)
            {
                Assert.That(fixture.SourceContainer.ObjectIds, Is.EqualTo(sourceOrder));
                Assert.That(fixture.DestinationContainer.ObjectIds, Is.EqualTo(destinationOrder));
                Assert.That(fixture.ActiveCard.BaseState.ContainerId, Is.EqualTo(cardContainerId));
                Assert.That(fixture.ActiveCard.BaseState.Pose, Is.EqualTo(cardPose));
                Assert.That(fixture.Match.Revision, Is.EqualTo(revision));
                if (!ignoreSourceApplyCount)
                {
                    Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(SourceApplyCount));
                }

                if (!ignoreDestinationApplyCount)
                {
                    Assert.That(fixture.DestinationView.ApplyCount, Is.EqualTo(DestinationApplyCount));
                }
            }
        }
    }
}
