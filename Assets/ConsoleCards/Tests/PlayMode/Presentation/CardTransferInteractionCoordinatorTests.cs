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
    public sealed class CardTransferInteractionCoordinatorTests
    {
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
        public void Constructor_WithValidDependencies_StoresExactInstances()
        {
            TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);

            Assert.That(fixture.Coordinator.MatchState, Is.SameAs(fixture.Match));
            Assert.That(fixture.Coordinator.RequestedByPlayerId, Is.EqualTo(fixture.RequestedByPlayerId));
            Assert.That(fixture.Coordinator.InteractionOwnerId, Is.EqualTo(fixture.OwnerId));
            Assert.That(fixture.Coordinator.LockService, Is.SameAs(fixture.LockService));
            Assert.That(fixture.Coordinator.TransferUseCase, Is.SameAs(fixture.TransferUseCase));
            Assert.That(fixture.Coordinator.LayoutViewLookup.TryGet(fixture.DestinationContainer.Id, out IContainerLayoutView view), Is.True);
            Assert.That(view, Is.SameAs(fixture.DestinationView));
        }

        [TestCase(ConstructorDependency.Match)]
        [TestCase(ConstructorDependency.LockService)]
        [TestCase(ConstructorDependency.TransferUseCase)]
        [TestCase(ConstructorDependency.LayoutViews)]
        public void Constructor_WhenDependencyIsNull_Rejects(ConstructorDependency dependency)
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.Clear(dependency);

            Assert.Throws<ArgumentNullException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void Constructor_WhenPlayerIdIsEmpty_Rejects()
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.RequestedByPlayerId = PlayerId.Empty;

            Assert.Throws<ArgumentException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void Constructor_WhenInteractionOwnerIdIsEmpty_Rejects()
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.OwnerId = InteractionOwnerId.Empty;

            Assert.Throws<ArgumentException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void ContainerLayoutViewLookup_ValidatesAndPreservesExactViews()
        {
            ContainerState firstContainer = CreateContainer(ContainerKind.Deck);
            ContainerState secondContainer = CreateContainer(ContainerKind.Hand);
            FakeLayoutView first = new FakeLayoutView(firstContainer);
            FakeLayoutView second = new FakeLayoutView(secondContainer);

            ContainerLayoutViewLookup lookup = new ContainerLayoutViewLookup(new IContainerLayoutView[] { first, second });

            Assert.That(lookup.TryGet(firstContainer.Id, out IContainerLayoutView firstResult), Is.True);
            Assert.That(firstResult, Is.SameAs(first));
            Assert.That(lookup.TryGet(secondContainer.Id, out IContainerLayoutView secondResult), Is.True);
            Assert.That(secondResult, Is.SameAs(second));
            Assert.That(lookup.TryGet(ContainerId.Empty, out _), Is.False);
        }

        [TestCase(LookupInvalidCase.NullCollection)]
        [TestCase(LookupInvalidCase.NullView)]
        [TestCase(LookupInvalidCase.UnboundView)]
        [TestCase(LookupInvalidCase.EmptyContainerId)]
        [TestCase(LookupInvalidCase.DuplicateContainerId)]
        public void ContainerLayoutViewLookup_WhenInvalid_Rejects(LookupInvalidCase invalidCase)
        {
            ContainerState container = CreateContainer(ContainerKind.Deck);
            FakeLayoutView bound = new FakeLayoutView(container);
            IReadOnlyList<IContainerLayoutView> views;

            switch (invalidCase)
            {
                case LookupInvalidCase.NullCollection:
                    views = null;
                    Assert.Throws<ArgumentNullException>(() => new ContainerLayoutViewLookup(views));
                    return;
                case LookupInvalidCase.NullView:
                    views = new IContainerLayoutView[] { null };
                    break;
                case LookupInvalidCase.UnboundView:
                    views = new IContainerLayoutView[] { new FakeLayoutView(container, isBound: false) };
                    break;
                case LookupInvalidCase.EmptyContainerId:
                    views = new IContainerLayoutView[] { new FakeLayoutView(container, containerIdOverride: ContainerId.Empty) };
                    break;
                case LookupInvalidCase.DuplicateContainerId:
                    views = new IContainerLayoutView[] { bound, new FakeLayoutView(container) };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, "Unsupported invalid lookup case.");
            }

            Assert.Throws<ArgumentException>(() => new ContainerLayoutViewLookup(views));
        }

        [Test]
        public void CardTransferInteractionResult_FactoriesMapApprovedStatuses()
        {
            AssertResult(CardTransferInteractionResult.NoTarget(), CardTransferInteractionStatus.NoTarget, false, false, false);
            AssertResult(CardTransferInteractionResult.CardUnavailable(), CardTransferInteractionStatus.CardUnavailable, false, false, false);
            AssertResult(CardTransferInteractionResult.CardNotTransferable(), CardTransferInteractionStatus.CardNotTransferable, false, false, false);
            AssertResult(CardTransferInteractionResult.SameLocation(), CardTransferInteractionStatus.SameLocation, false, false, false);
            AssertResult(CardTransferInteractionResult.SourceLayoutUnavailable(), CardTransferInteractionStatus.SourceLayoutUnavailable, false, false, false);
            AssertResult(CardTransferInteractionResult.DestinationLayoutUnavailable(), CardTransferInteractionStatus.DestinationLayoutUnavailable, false, false, false);
            AssertResult(CardTransferInteractionResult.LocalLockConflict(), CardTransferInteractionStatus.LocalLockConflict, false, false, false);
        }

        [Test]
        public void CardTransferInteractionResult_AcceptedAndRejectedFactoriesValidateTransferResult()
        {
            TransferCardResult accepted = TransferCardResult.Accepted(5);
            TransferCardResult rejected = TransferCardResult.Failure(
                CommandResultStatus.Rejected,
                TransferCardError.DestinationCapacityExceeded);

            AssertResult(CardTransferInteractionResult.TransferAccepted(accepted), CardTransferInteractionStatus.TransferAccepted, true, true, true);
            AssertResult(CardTransferInteractionResult.TransferRejected(rejected), CardTransferInteractionStatus.TransferRejected, true, false, true);
            Assert.Throws<ArgumentException>(() => CardTransferInteractionResult.TransferAccepted(rejected));
            Assert.Throws<ArgumentException>(() => CardTransferInteractionResult.TransferRejected(accepted));
        }

        [Test]
        public void CardTransferInteractionResult_EqualityHashCodeOperatorsAndToStringBehaveCorrectly()
        {
            CardTransferInteractionResult first =
                CardTransferInteractionResult.FromTransferResult(TransferCardResult.Accepted(7));
            CardTransferInteractionResult second =
                CardTransferInteractionResult.FromTransferResult(TransferCardResult.Accepted(7));
            CardTransferInteractionResult different = CardTransferInteractionResult.NoTarget();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain(nameof(CardTransferInteractionStatus.TransferAccepted)));
        }

        [TestCase(NoCommandCase.NullCardView)]
        [TestCase(NoCommandCase.DestroyedCardView)]
        [TestCase(NoCommandCase.DisabledCardView)]
        [TestCase(NoCommandCase.InactiveCardView)]
        [TestCase(NoCommandCase.UnboundCardView)]
        [TestCase(NoCommandCase.NonMatchOwnedCard)]
        [TestCase(NoCommandCase.UserLockedCard)]
        [TestCase(NoCommandCase.NoneTarget)]
        [TestCase(NoCommandCase.SameContainer)]
        [TestCase(NoCommandCase.TabletopToTabletop)]
        [TestCase(NoCommandCase.MissingSourceLayout)]
        [TestCase(NoCommandCase.MissingDestinationLayout)]
        [TestCase(NoCommandCase.LocalLockConflict)]
        public void Transfer_NoCommandOutcomes_DoNotMutateOrAttemptTransfer(NoCommandCase noCommandCase)
        {
            TransferFixture fixture = CreateNoCommandFixture(noCommandCase);
            StateSnapshot before = StateSnapshot.Capture(fixture);

            CardTransferInteractionResult result = fixture.Coordinator.Transfer(
                noCommandCase == NoCommandCase.NullCardView ? null : fixture.CardView,
                fixture.Target);

            Assert.That(result.TransferAttempted, Is.False);
            Assert.That(result.TransferResult.HasValue, Is.False);
            Assert.That(result.Status, Is.EqualTo(ExpectedStatus(noCommandCase)));
            before.AssertMatches(fixture);
        }

        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Transfer_TabletopToContainer_AcceptsAndRefreshesDestinationLayout(ContainerKind destinationKind)
        {
            TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, destinationKind);
            TabletopPose cardPose = fixture.Card.BaseState.Pose;

            CardTransferInteractionResult result = fixture.Coordinator.Transfer(
                fixture.CardView,
                CardDropTarget.ForContainer(fixture.DestinationContainer.Id));

            AssertResult(result, CardTransferInteractionStatus.TransferAccepted, true, true, true);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.DestinationContainer.GetObjectAt(fixture.DestinationContainer.Count - 1), Is.EqualTo(fixture.Card.BaseState.Id));
            Assert.That(fixture.Card.BaseState.ContainerId, Is.EqualTo(fixture.DestinationContainer.Id));
            Assert.That(fixture.Card.BaseState.Pose, Is.EqualTo(cardPose));
            Assert.That(fixture.CardView.IsContainerLayoutApplied, Is.True);
            Assert.That(fixture.DestinationView.ApplyCount, Is.EqualTo(2));
            Assert.That(fixture.LockService.IsLocked(fixture.Card.BaseState.Id), Is.False);
        }

        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void Transfer_ContainerToTabletop_AcceptsRefreshesSourceAndPreservesPoseFields(ContainerKind sourceKind)
        {
            TransferFixture fixture = CreateFixture(SourceLocation.Container, ContainerKind.Hand, sourceKind);
            TabletopPose acceptedPose = fixture.Card.BaseState.Pose;
            CardDropTarget target = CardDropTarget.ForTabletop(CreatePose(9.0, -4.0, 0f));

            CardTransferInteractionResult result = fixture.Coordinator.Transfer(fixture.CardView, target);

            AssertResult(result, CardTransferInteractionStatus.TransferAccepted, true, true, true);
            Assert.That(fixture.SourceContainer.Contains(fixture.Card.BaseState.Id), Is.False);
            Assert.That(fixture.Card.BaseState.ContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(fixture.Card.BaseState.Pose.Position, Is.EqualTo(target.TabletopPose.Position));
            Assert.That(fixture.Card.BaseState.Pose.RotationDegrees, Is.EqualTo(acceptedPose.RotationDegrees));
            Assert.That(fixture.Card.BaseState.Pose.Layer, Is.EqualTo(acceptedPose.Layer));
            Assert.That(fixture.Card.BaseState.Pose.LocalOrder, Is.EqualTo(acceptedPose.LocalOrder));
            Assert.That(fixture.CardView.IsContainerLayoutApplied, Is.False);
            AssertWorldPose(fixture.CardView, fixture.Card.BaseState.Pose);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(2));
            Assert.That(fixture.LockService.IsLocked(fixture.Card.BaseState.Id), Is.False);
        }

        [TestCase(ContainerKind.Deck, ContainerKind.Hand)]
        [TestCase(ContainerKind.Hand, ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.Stack, ContainerKind.ConsoleSlot)]
        [TestCase(ContainerKind.ConsoleSlot, ContainerKind.Stack)]
        [TestCase(ContainerKind.DiscardPile, ContainerKind.Deck)]
        public void Transfer_ContainerToContainer_AcceptsAndRefreshesOnlyAffectedLayouts(
            ContainerKind sourceKind,
            ContainerKind destinationKind)
        {
            TransferFixture fixture = CreateFixture(SourceLocation.Container, destinationKind, sourceKind);
            TabletopPose cardPose = fixture.Card.BaseState.Pose;
            int sourceApplyBefore = fixture.SourceView.ApplyCount;
            int destinationApplyBefore = fixture.DestinationView.ApplyCount;

            CardTransferInteractionResult result = fixture.Coordinator.Transfer(
                fixture.CardView,
                CardDropTarget.ForContainer(fixture.DestinationContainer.Id));

            AssertResult(result, CardTransferInteractionStatus.TransferAccepted, true, true, true);
            Assert.That(fixture.SourceContainer.Contains(fixture.Card.BaseState.Id), Is.False);
            Assert.That(fixture.DestinationContainer.GetObjectAt(fixture.DestinationContainer.Count - 1), Is.EqualTo(fixture.Card.BaseState.Id));
            Assert.That(fixture.Card.BaseState.ContainerId, Is.EqualTo(fixture.DestinationContainer.Id));
            Assert.That(fixture.Card.BaseState.Pose, Is.EqualTo(cardPose));
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(sourceApplyBefore + 1));
            Assert.That(fixture.DestinationView.ApplyCount, Is.EqualTo(destinationApplyBefore + 1));
            Assert.That(fixture.CardView.IsContainerLayoutApplied, Is.True);
        }

        [Test]
        public void Transfer_WhenUseCaseRejects_ReappliesSourceLayoutAndReleasesLock()
        {
            TransferFixture fixture = CreateFixture(
                SourceLocation.Container,
                ContainerKind.ConsoleSlot,
                ContainerKind.Deck,
                destinationCapacity: 1,
                destinationStartingCardCount: 1);
            StateSnapshot before = StateSnapshot.Capture(fixture);

            CardTransferInteractionResult result = fixture.Coordinator.Transfer(
                fixture.CardView,
                CardDropTarget.ForContainer(fixture.DestinationContainer.Id));

            AssertResult(result, CardTransferInteractionStatus.TransferRejected, true, false, true);
            Assert.That(result.TransferResult.Value.Error, Is.EqualTo(TransferCardError.DestinationCapacityExceeded));
            before.AssertMatches(fixture, ignoreSourceApplyCount: true);
            Assert.That(fixture.SourceView.ApplyCount, Is.EqualTo(before.SourceApplyCount + 1));
            Assert.That(fixture.DestinationView.ApplyCount, Is.EqualTo(before.DestinationApplyCount));
            Assert.That(fixture.LockService.IsLocked(fixture.Card.BaseState.Id), Is.False);
        }

        [Test]
        public void Transfer_WhenSourceIsTabletopAndRejected_ReconcilesAcceptedPose()
        {
            TransferFixture fixture = CreateFixture(
                SourceLocation.Tabletop,
                ContainerKind.ConsoleSlot,
                destinationCapacity: 1,
                destinationStartingCardCount: 1);
            TabletopPose acceptedPose = fixture.Card.BaseState.Pose;
            fixture.CardView.transform.SetPositionAndRotation(
                new Vector3(8f, 2f, 8f),
                Quaternion.Euler(0f, 90f, 0f));

            CardTransferInteractionResult result = fixture.Coordinator.Transfer(
                fixture.CardView,
                CardDropTarget.ForContainer(fixture.DestinationContainer.Id));

            AssertResult(result, CardTransferInteractionStatus.TransferRejected, true, false, true);
            Assert.That(fixture.CardView.IsContainerLayoutApplied, Is.False);
            AssertWorldPose(fixture.CardView, acceptedPose);
        }

        [Test]
        public void Transfer_WhenSameOwnerAlreadyOwnsLock_PreservesLockAfterAcceptedTransfer()
        {
            TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
            fixture.LockService.Acquire(fixture.Card.BaseState.Id, fixture.OwnerId);

            CardTransferInteractionResult result = fixture.Coordinator.Transfer(
                fixture.CardView,
                CardDropTarget.ForContainer(fixture.DestinationContainer.Id));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.LockService.IsOwnedBy(fixture.Card.BaseState.Id, fixture.OwnerId), Is.True);
            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
        }

        [Test]
        public void Transfer_WhenAcceptedReconciliationThrows_DoesNotRollbackRuntimeStateAndReleasesLock()
        {
            TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
            ThrowingLayoutView throwingDestination = new ThrowingLayoutView(fixture.DestinationContainer);
            fixture.ReplaceCoordinatorViews(new IContainerLayoutView[] { throwingDestination });

            Assert.Throws<InvalidOperationException>(
                () => fixture.Coordinator.Transfer(
                    fixture.CardView,
                    CardDropTarget.ForContainer(fixture.DestinationContainer.Id)));

            Assert.That(fixture.Card.BaseState.ContainerId, Is.EqualTo(fixture.DestinationContainer.Id));
            Assert.That(fixture.DestinationContainer.Contains(fixture.Card.BaseState.Id), Is.True);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.LockService.IsLocked(fixture.Card.BaseState.Id), Is.False);
        }

        [Test]
        public void Transfer_DoesNotMutateSelectionHighlightOrOtherRuntimeState()
        {
            TransferFixture fixture = CreateFixture(SourceLocation.Container, ContainerKind.Hand, ContainerKind.Deck, includeOtherCard: true);
            TabletopSelectionState selection = new TabletopSelectionState();
            selection.Select(fixture.CardView);
            ObjectSnapshot otherCardBefore = ObjectSnapshot.Capture(fixture.OtherCard.BaseState);

            fixture.Coordinator.Transfer(
                fixture.CardView,
                CardDropTarget.ForContainer(fixture.DestinationContainer.Id));

            Assert.That(selection.SelectedView, Is.SameAs(fixture.CardView));
            Assert.That(selection.HasSelection, Is.True);
            otherCardBefore.AssertMatches(fixture.OtherCard.BaseState);
        }

        [Test]
        public void ProductionSource_DoesNotUseForbiddenBoundaries()
        {
            string[] paths =
            {
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "CardTransferInteractionCoordinator.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Interaction", "ContainerLayoutViewLookup.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Views", "Containers", "IContainerLayoutView.cs")
            };

            string source = string.Join(Environment.NewLine, Array.ConvertAll(paths, File.ReadAllText));

            Assert.That(source, Does.Not.Contain("FindObjectOfType"));
            Assert.That(source, Does.Not.Contain("FindObjectsByType"));
            Assert.That(source, Does.Not.Contain("Camera.main"));
            Assert.That(source, Does.Not.Contain("UnityEngine.InputSystem"));
            Assert.That(source, Does.Not.Contain("Update("));
            Assert.That(source, Does.Not.Contain("SetContainer("));
            Assert.That(source, Does.Not.Contain("SetPose("));
            Assert.That(source, Does.Not.Contain("AdvanceRevision"));
        }

        private TransferFixture CreateNoCommandFixture(NoCommandCase noCommandCase)
        {
            switch (noCommandCase)
            {
                case NoCommandCase.NullCardView:
                    return CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
                case NoCommandCase.DestroyedCardView:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
                    UnityObject.DestroyImmediate(fixture.CardView.gameObject);
                    return fixture;
                }
                case NoCommandCase.DisabledCardView:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
                    fixture.CardView.enabled = false;
                    return fixture;
                }
                case NoCommandCase.InactiveCardView:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
                    fixture.CardView.gameObject.SetActive(false);
                    return fixture;
                }
                case NoCommandCase.UnboundCardView:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
                    fixture.CardView.Unbind();
                    return fixture;
                }
                case NoCommandCase.NonMatchOwnedCard:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
                    CardInstanceState detachedCard = CreateCard(999, CreatePose(1.0, 1.0, 0f));
                    fixture.CardView.Bind(detachedCard, CreateConverter());
                    return fixture;
                }
                case NoCommandCase.UserLockedCard:
                    return CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck, isUserLocked: true);
                case NoCommandCase.NoneTarget:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Container, ContainerKind.Hand, ContainerKind.Deck);
                    fixture.Target = CardDropTarget.None();
                    return fixture;
                }
                case NoCommandCase.SameContainer:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Container, ContainerKind.Hand, ContainerKind.Deck);
                    fixture.Target = CardDropTarget.ForContainer(fixture.SourceContainer.Id);
                    return fixture;
                }
                case NoCommandCase.TabletopToTabletop:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
                    fixture.Target = CardDropTarget.ForTabletop(CreatePose(2.0, 2.0, 0f));
                    return fixture;
                }
                case NoCommandCase.MissingSourceLayout:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Container, ContainerKind.Hand, ContainerKind.Deck);
                    fixture.ReplaceCoordinatorViews(new IContainerLayoutView[] { fixture.DestinationView });
                    return fixture;
                }
                case NoCommandCase.MissingDestinationLayout:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Container, ContainerKind.Hand, ContainerKind.Deck);
                    fixture.ReplaceCoordinatorViews(new IContainerLayoutView[] { fixture.SourceView });
                    return fixture;
                }
                case NoCommandCase.LocalLockConflict:
                {
                    TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
                    fixture.LockService.Acquire(fixture.Card.BaseState.Id, InteractionOwnerId.New());
                    return fixture;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(noCommandCase), noCommandCase, "Unsupported no-command case.");
            }
        }

        private TransferFixture CreateFixture(
            SourceLocation sourceLocation,
            ContainerKind destinationKind,
            ContainerKind sourceKind = ContainerKind.Deck,
            int destinationCapacity = 0,
            int destinationStartingCardCount = 1,
            bool isUserLocked = false,
            bool includeOtherCard = false)
        {
            TabletopCoordinateConverter converter = CreateConverter();
            ContainerTransferService transferService = new ContainerTransferService();
            CardInstanceState card = CreateCard(1, CreatePose(3.0, 4.0, 25f, 2, 3), isUserLocked);
            CardView cardView = CreateCardView("TransferCard", card, converter);
            CardInstanceState destinationStartingCard = CreateCard(20, CreatePose(20.0, 21.0, 0f));
            CardView destinationStartingCardView = CreateCardView("DestinationStartingCard", destinationStartingCard, converter);
            CardInstanceState otherCard = includeOtherCard ? CreateCard(40, CreatePose(-1.0, -2.0, 0f)) : null;
            CardView otherCardView = includeOtherCard ? CreateCardView("OtherCard", otherCard, converter) : null;

            ContainerState sourceContainer = sourceLocation == SourceLocation.Container
                ? CreateContainer(sourceKind)
                : null;
            ContainerState destinationContainer = CreateContainer(destinationKind, destinationCapacity);
            List<CardInstanceState> cards = new List<CardInstanceState> { card, destinationStartingCard };
            List<ContainerState> containers = new List<ContainerState> { destinationContainer };
            List<IContainerLayoutView> layoutViews = new List<IContainerLayoutView>();

            if (sourceContainer != null)
            {
                transferService.PlaceIntoContainer(card.BaseState, sourceContainer);
                containers.Add(sourceContainer);
            }

            for (int i = 0; i < destinationStartingCardCount; i++)
            {
                if (i == 0)
                {
                    transferService.PlaceIntoContainer(destinationStartingCard.BaseState, destinationContainer);
                    continue;
                }

                CardInstanceState extraCard = CreateCard(21 + i, CreatePose(30.0 + i, 31.0 + i, 0f));
                transferService.PlaceIntoContainer(extraCard.BaseState, destinationContainer);
                cards.Add(extraCard);
            }

            if (otherCard != null)
            {
                cards.Add(otherCard);
            }

            CountingLayoutView sourceView = null;
            if (sourceContainer != null)
            {
                List<CardView> sourceCardViews = new List<CardView> { cardView };
                if (otherCardView != null)
                {
                    sourceCardViews.Add(otherCardView);
                }

                sourceView = CreateLayoutView(sourceContainer, sourceCardViews, converter);
                layoutViews.Add(sourceView);
            }

            CountingLayoutView destinationView = CreateLayoutView(
                destinationContainer,
                new List<CardView> { destinationStartingCardView, cardView },
                converter);
            layoutViews.Add(destinationView);

            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.Empty,
                0,
                cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                containers,
                Array.Empty<SeatState>(),
                CreatePlacements(containers));

            LocalInteractionLockService lockService = new LocalInteractionLockService();
            TransferCardUseCase transferUseCase = new TransferCardUseCase();
            PlayerId requestedByPlayerId = PlayerId.New();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            CardTransferInteractionCoordinator coordinator = new CardTransferInteractionCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                lockService,
                transferUseCase,
                layoutViews);

            return new TransferFixture(
                coordinator,
                match,
                card,
                cardView,
                sourceContainer,
                destinationContainer,
                sourceView,
                destinationView,
                lockService,
                transferUseCase,
                requestedByPlayerId,
                ownerId,
                CardDropTarget.ForContainer(destinationContainer.Id),
                otherCard);
        }

        private CountingLayoutView CreateLayoutView(
            ContainerState container,
            IReadOnlyList<CardView> cardViews,
            TabletopCoordinateConverter converter)
        {
            switch (container.Kind)
            {
                case ContainerKind.Deck:
                {
                    DeckView view = CreateGameObject("DeckView").AddComponent<DeckView>();
                    view.CardThicknessOffset = 0.1f;
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(0.0, 0.0, 0f)), converter, cardViews);
                    return new CountingLayoutView(view);
                }
                case ContainerKind.Stack:
                {
                    StackView view = CreateGameObject("StackView").AddComponent<StackView>();
                    view.VerticalOffset = 0.1f;
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(1.0, 0.0, 0f)), converter, cardViews);
                    return new CountingLayoutView(view);
                }
                case ContainerKind.DiscardPile:
                {
                    DiscardPileView view = CreateGameObject("DiscardPileView").AddComponent<DiscardPileView>();
                    view.VerticalOffset = 0.1f;
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(2.0, 0.0, 0f)), converter, cardViews);
                    return new CountingLayoutView(view);
                }
                case ContainerKind.Hand:
                {
                    HandView view = CreateGameObject("HandView").AddComponent<HandView>();
                    view.HorizontalSpacing = 1f;
                    view.Bind(container, CreateGameObject("HandAnchor").transform, converter, cardViews);
                    return new CountingLayoutView(view);
                }
                case ContainerKind.ConsoleSlot:
                {
                    ConsoleSlotView view = CreateGameObject("ConsoleSlotView").AddComponent<ConsoleSlotView>();
                    view.VerticalOffset = 0.1f;
                    view.Bind(container, CreateGameObject("SlotAnchor").transform, converter, cardViews);
                    return new CountingLayoutView(view);
                }
                default:
                    throw new ArgumentException("Test layout View requires a supported collection kind.", nameof(container));
            }
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

        private ConstructorDependencies CreateConstructorDependencies()
        {
            TransferFixture fixture = CreateFixture(SourceLocation.Tabletop, ContainerKind.Deck);
            return new ConstructorDependencies
            {
                Match = fixture.Match,
                RequestedByPlayerId = fixture.RequestedByPlayerId,
                OwnerId = fixture.OwnerId,
                LockService = fixture.LockService,
                TransferUseCase = fixture.TransferUseCase,
                LayoutViews = new IContainerLayoutView[] { fixture.DestinationView }
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

        private static CardTransferInteractionStatus ExpectedStatus(NoCommandCase noCommandCase)
        {
            switch (noCommandCase)
            {
                case NoCommandCase.NullCardView:
                case NoCommandCase.DestroyedCardView:
                case NoCommandCase.DisabledCardView:
                case NoCommandCase.InactiveCardView:
                case NoCommandCase.UnboundCardView:
                case NoCommandCase.NonMatchOwnedCard:
                    return CardTransferInteractionStatus.CardUnavailable;
                case NoCommandCase.UserLockedCard:
                    return CardTransferInteractionStatus.CardNotTransferable;
                case NoCommandCase.NoneTarget:
                    return CardTransferInteractionStatus.NoTarget;
                case NoCommandCase.SameContainer:
                case NoCommandCase.TabletopToTabletop:
                    return CardTransferInteractionStatus.SameLocation;
                case NoCommandCase.MissingSourceLayout:
                    return CardTransferInteractionStatus.SourceLayoutUnavailable;
                case NoCommandCase.MissingDestinationLayout:
                    return CardTransferInteractionStatus.DestinationLayoutUnavailable;
                case NoCommandCase.LocalLockConflict:
                    return CardTransferInteractionStatus.LocalLockConflict;
                default:
                    throw new ArgumentOutOfRangeException(nameof(noCommandCase), noCommandCase, "Unsupported no-command case.");
            }
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

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void AssertResult(
            CardTransferInteractionResult result,
            CardTransferInteractionStatus expectedStatus,
            bool expectedAttempted,
            bool expectedSucceeded,
            bool expectTransferResult)
        {
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.TransferAttempted, Is.EqualTo(expectedAttempted));
            Assert.That(result.Succeeded, Is.EqualTo(expectedSucceeded));
            Assert.That(result.TransferResult.HasValue, Is.EqualTo(expectTransferResult));
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
            Match,
            LockService,
            TransferUseCase,
            LayoutViews
        }

        public enum LookupInvalidCase
        {
            NullCollection,
            NullView,
            UnboundView,
            EmptyContainerId,
            DuplicateContainerId
        }

        public enum NoCommandCase
        {
            NullCardView,
            DestroyedCardView,
            DisabledCardView,
            InactiveCardView,
            UnboundCardView,
            NonMatchOwnedCard,
            UserLockedCard,
            NoneTarget,
            SameContainer,
            TabletopToTabletop,
            MissingSourceLayout,
            MissingDestinationLayout,
            LocalLockConflict
        }

        public enum SourceLocation
        {
            Tabletop,
            Container
        }

        private sealed class ConstructorDependencies
        {
            public MatchState Match { get; set; }

            public PlayerId RequestedByPlayerId { get; set; }

            public InteractionOwnerId OwnerId { get; set; }

            public LocalInteractionLockService LockService { get; set; }

            public TransferCardUseCase TransferUseCase { get; set; }

            public IReadOnlyList<IContainerLayoutView> LayoutViews { get; set; }

            public CardTransferInteractionCoordinator CreateCoordinator()
            {
                return new CardTransferInteractionCoordinator(
                    Match,
                    RequestedByPlayerId,
                    OwnerId,
                    LockService,
                    TransferUseCase,
                    LayoutViews);
            }

            public void Clear(ConstructorDependency dependency)
            {
                switch (dependency)
                {
                    case ConstructorDependency.Match:
                        Match = null;
                        break;
                    case ConstructorDependency.LockService:
                        LockService = null;
                        break;
                    case ConstructorDependency.TransferUseCase:
                        TransferUseCase = null;
                        break;
                    case ConstructorDependency.LayoutViews:
                        LayoutViews = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dependency), dependency, "Unsupported dependency.");
                }
            }
        }

        private sealed class TransferFixture
        {
            public TransferFixture(
                CardTransferInteractionCoordinator coordinator,
                MatchState match,
                CardInstanceState card,
                CardView cardView,
                ContainerState sourceContainer,
                ContainerState destinationContainer,
                CountingLayoutView sourceView,
                CountingLayoutView destinationView,
                LocalInteractionLockService lockService,
                TransferCardUseCase transferUseCase,
                PlayerId requestedByPlayerId,
                InteractionOwnerId ownerId,
                CardDropTarget target,
                CardInstanceState otherCard)
            {
                Coordinator = coordinator;
                Match = match;
                Card = card;
                CardView = cardView;
                SourceContainer = sourceContainer;
                DestinationContainer = destinationContainer;
                SourceView = sourceView;
                DestinationView = destinationView;
                LockService = lockService;
                TransferUseCase = transferUseCase;
                RequestedByPlayerId = requestedByPlayerId;
                OwnerId = ownerId;
                Target = target;
                OtherCard = otherCard;
            }

            public CardTransferInteractionCoordinator Coordinator { get; private set; }

            public MatchState Match { get; }

            public CardInstanceState Card { get; }

            public CardView CardView { get; }

            public ContainerState SourceContainer { get; }

            public ContainerState DestinationContainer { get; }

            public CountingLayoutView SourceView { get; }

            public CountingLayoutView DestinationView { get; }

            public LocalInteractionLockService LockService { get; }

            public TransferCardUseCase TransferUseCase { get; }

            public PlayerId RequestedByPlayerId { get; }

            public InteractionOwnerId OwnerId { get; }

            public CardDropTarget Target { get; set; }

            public CardInstanceState OtherCard { get; }

            public void ReplaceCoordinatorViews(IReadOnlyList<IContainerLayoutView> layoutViews)
            {
                Coordinator = new CardTransferInteractionCoordinator(
                    Match,
                    RequestedByPlayerId,
                    OwnerId,
                    LockService,
                    TransferUseCase,
                    layoutViews);
            }
        }

        private sealed class CountingLayoutView : IContainerLayoutView
        {
            private readonly IContainerLayoutView inner;

            public CountingLayoutView(IContainerLayoutView inner)
            {
                this.inner = inner;
                ApplyCount = 1;
            }

            public bool IsBound => inner.IsBound;

            public ContainerId ContainerId => inner.ContainerId;

            public ContainerState ContainerState => inner.ContainerState;

            public int ApplyCount { get; private set; }

            public void SetCardViews(IReadOnlyList<CardView> cardViews)
            {
                inner.SetCardViews(cardViews);
            }

            public void ApplyAcceptedLayout()
            {
                inner.ApplyAcceptedLayout();
                ApplyCount++;
            }
        }

        private sealed class FakeLayoutView : IContainerLayoutView
        {
            public FakeLayoutView(
                ContainerState containerState,
                bool isBound = true,
                ContainerId? containerIdOverride = null)
            {
                IsBound = isBound;
                ContainerState = containerState;
                ContainerId = containerIdOverride ?? containerState.Id;
            }

            public bool IsBound { get; }

            public ContainerId ContainerId { get; }

            public ContainerState ContainerState { get; }

            public void SetCardViews(IReadOnlyList<CardView> cardViews)
            {
            }

            public void ApplyAcceptedLayout()
            {
            }
        }

        private sealed class ThrowingLayoutView : IContainerLayoutView
        {
            public ThrowingLayoutView(ContainerState containerState)
            {
                ContainerState = containerState;
            }

            public bool IsBound => true;

            public ContainerId ContainerId => ContainerState.Id;

            public ContainerState ContainerState { get; }

            public void SetCardViews(IReadOnlyList<CardView> cardViews)
            {
            }

            public void ApplyAcceptedLayout()
            {
                throw new InvalidOperationException("Injected layout failure.");
            }
        }

        private sealed class StateSnapshot
        {
            private readonly List<TabletopObjectId> sourceOrder;
            private readonly List<TabletopObjectId> destinationOrder;
            private readonly ObjectSnapshot cardSnapshot;
            private readonly Vector3 cardPosition;
            private readonly Quaternion cardRotation;
            private readonly long revision;

            private StateSnapshot(
                List<TabletopObjectId> sourceOrder,
                List<TabletopObjectId> destinationOrder,
                ObjectSnapshot cardSnapshot,
                Vector3 cardPosition,
                Quaternion cardRotation,
                long revision,
                int sourceApplyCount,
                int destinationApplyCount)
            {
                this.sourceOrder = sourceOrder;
                this.destinationOrder = destinationOrder;
                this.cardSnapshot = cardSnapshot;
                this.cardPosition = cardPosition;
                this.cardRotation = cardRotation;
                this.revision = revision;
                SourceApplyCount = sourceApplyCount;
                DestinationApplyCount = destinationApplyCount;
            }

            public int SourceApplyCount { get; }

            public int DestinationApplyCount { get; }

            public static StateSnapshot Capture(TransferFixture fixture)
            {
                return new StateSnapshot(
                    fixture.SourceContainer == null
                        ? null
                        : new List<TabletopObjectId>(fixture.SourceContainer.ObjectIds),
                    fixture.DestinationContainer == null
                        ? null
                        : new List<TabletopObjectId>(fixture.DestinationContainer.ObjectIds),
                    ObjectSnapshot.Capture(fixture.Card.BaseState),
                    fixture.CardView == null ? Vector3.zero : fixture.CardView.transform.position,
                    fixture.CardView == null ? Quaternion.identity : fixture.CardView.transform.rotation,
                    fixture.Match.Revision,
                    fixture.SourceView?.ApplyCount ?? 0,
                    fixture.DestinationView?.ApplyCount ?? 0);
            }

            public void AssertMatches(
                TransferFixture fixture,
                bool ignoreSourceApplyCount = false,
                bool ignoreDestinationApplyCount = false)
            {
                if (sourceOrder != null)
                {
                    Assert.That(fixture.SourceContainer.ObjectIds, Is.EqualTo(sourceOrder));
                }

                if (destinationOrder != null)
                {
                    Assert.That(fixture.DestinationContainer.ObjectIds, Is.EqualTo(destinationOrder));
                }

                cardSnapshot.AssertMatches(fixture.Card.BaseState);
                Assert.That(fixture.Match.Revision, Is.EqualTo(revision));
                if (fixture.CardView != null)
                {
                    Assert.That(fixture.CardView.transform.position.x, Is.EqualTo(cardPosition.x).Within(Tolerance));
                    Assert.That(fixture.CardView.transform.position.y, Is.EqualTo(cardPosition.y).Within(Tolerance));
                    Assert.That(fixture.CardView.transform.position.z, Is.EqualTo(cardPosition.z).Within(Tolerance));
                    Assert.That(Quaternion.Angle(cardRotation, fixture.CardView.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
                }

                if (!ignoreSourceApplyCount)
                {
                    Assert.That(fixture.SourceView?.ApplyCount ?? 0, Is.EqualTo(SourceApplyCount));
                }

                if (!ignoreDestinationApplyCount)
                {
                    Assert.That(fixture.DestinationView?.ApplyCount ?? 0, Is.EqualTo(DestinationApplyCount));
                }
            }
        }

        private sealed class ObjectSnapshot
        {
            private ObjectSnapshot(
                TabletopObjectId id,
                ObjectDefinitionId definitionId,
                TabletopPose pose,
                ContainerId containerId,
                PlayerId ownerPlayerId,
                ObjectVisibility visibility,
                bool isUserLocked)
            {
                Id = id;
                DefinitionId = definitionId;
                Pose = pose;
                ContainerId = containerId;
                OwnerPlayerId = ownerPlayerId;
                Visibility = visibility;
                IsUserLocked = isUserLocked;
            }

            public TabletopObjectId Id { get; }

            public ObjectDefinitionId DefinitionId { get; }

            public TabletopPose Pose { get; }

            public ContainerId ContainerId { get; }

            public PlayerId OwnerPlayerId { get; }

            public ObjectVisibility Visibility { get; }

            public bool IsUserLocked { get; }

            public static ObjectSnapshot Capture(TabletopObjectState state)
            {
                return new ObjectSnapshot(
                    state.Id,
                    state.DefinitionId,
                    state.Pose,
                    state.ContainerId,
                    state.OwnerPlayerId,
                    state.Visibility,
                    state.IsUserLocked);
            }

            public void AssertMatches(TabletopObjectState state)
            {
                Assert.That(state.Id, Is.EqualTo(Id));
                Assert.That(state.DefinitionId, Is.EqualTo(DefinitionId));
                Assert.That(state.Pose, Is.EqualTo(Pose));
                Assert.That(state.ContainerId, Is.EqualTo(ContainerId));
                Assert.That(state.OwnerPlayerId, Is.EqualTo(OwnerPlayerId));
                Assert.That(state.Visibility, Is.EqualTo(Visibility));
                Assert.That(state.IsUserLocked, Is.EqualTo(IsUserLocked));
            }
        }
    }
}
