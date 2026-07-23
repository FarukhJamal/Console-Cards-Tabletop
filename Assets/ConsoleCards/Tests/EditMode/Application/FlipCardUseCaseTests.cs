using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class FlipCardUseCaseTests
    {
        public enum FlipFailureScenario
        {
            NullCommand,
            MatchIdMismatch,
            RevisionConflict,
            ObjectNotFound,
            PawnObject,
            TokenObject,
            ObjectUserLocked,
            RevisionOverflow
        }

        [Test]
        public void Execute_WhenMatchStateIsNull_ReturnsInvalidMatchRequired()
        {
            FlipCardUseCase useCase = new FlipCardUseCase();

            FlipCardResult result = useCase.Execute(null, CreateCommand(MatchId.New(), TabletopObjectId.New()));

            AssertFailure(result, CommandResultStatus.Invalid, FlipCardError.MatchRequired);
        }

        [Test]
        public void Execute_WhenCommandIsNull_ReturnsInvalidCommandRequired()
        {
            FlipFixture fixture = CreateFixture();
            FlipCardUseCase useCase = new FlipCardUseCase();

            FlipCardResult result = useCase.Execute(fixture.Match, null);

            AssertFailure(result, CommandResultStatus.Invalid, FlipCardError.CommandRequired);
        }

        [Test]
        public void Execute_WhenMatchIdMismatches_ReturnsInvalidMatchIdMismatch()
        {
            FlipFixture fixture = CreateFixture();

            FlipCardResult result = Execute(
                fixture,
                CreateCommand(MatchId.New(), fixture.TargetCard.BaseState.Id));

            AssertFailure(result, CommandResultStatus.Invalid, FlipCardError.MatchIdMismatch);
        }

        [Test]
        public void Execute_WhenExpectedRevisionMismatches_ReturnsConflictRevisionConflict()
        {
            FlipFixture fixture = CreateFixture(revision: 4);

            FlipCardResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetCard.BaseState.Id, expectedRevision: 5));

            AssertFailure(result, CommandResultStatus.Conflict, FlipCardError.RevisionConflict);
        }

        [Test]
        public void Execute_WhenObjectIsMissing_ReturnsRejectedObjectNotFound()
        {
            FlipFixture fixture = CreateFixture();

            FlipCardResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, TabletopObjectId.New()));

            AssertFailure(result, CommandResultStatus.Rejected, FlipCardError.ObjectNotFound);
        }

        [Test]
        public void Execute_WhenPawnObjectIsRequested_ReturnsRejectedObjectNotCard()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();

            FlipCardResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.Pawn.BaseState.Id));

            AssertFailure(result, CommandResultStatus.Rejected, FlipCardError.ObjectNotCard);
        }

        [Test]
        public void Execute_WhenTokenObjectIsRequested_ReturnsRejectedObjectNotCard()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();

            FlipCardResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.Token.BaseState.Id));

            AssertFailure(result, CommandResultStatus.Rejected, FlipCardError.ObjectNotCard);
        }

        [Test]
        public void Execute_WhenCardIsUserLocked_ReturnsRejectedObjectUserLocked()
        {
            FlipFixture fixture = CreateFixture(isUserLocked: true);

            FlipCardResult result = Execute(fixture);

            AssertFailure(result, CommandResultStatus.Rejected, FlipCardError.ObjectUserLocked);
        }

        [Test]
        public void Execute_WhenRevisionIsLongMaxValue_ReturnsConflictRevisionOverflow()
        {
            FlipFixture fixture = CreateFixture(revision: long.MaxValue);

            FlipCardResult result = Execute(fixture);

            AssertFailure(result, CommandResultStatus.Conflict, FlipCardError.RevisionOverflow);
        }

        [Test]
        public void Execute_WhenFaceUpCardTargetsFaceDown_UpdatesFace()
        {
            FlipFixture fixture = CreateFixture(initialFace: CardFace.FaceUp);

            FlipCardResult result = Execute(fixture, targetFace: CardFace.FaceDown);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetCard.Face, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void Execute_WhenFaceDownCardTargetsFaceUp_UpdatesFace()
        {
            FlipFixture fixture = CreateFixture(initialFace: CardFace.FaceDown);

            FlipCardResult result = Execute(fixture, targetFace: CardFace.FaceUp);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetCard.Face, Is.EqualTo(CardFace.FaceUp));
        }

        [Test]
        public void Execute_WhenSuccessful_UsesTargetFaceExactly()
        {
            FlipFixture fixture = CreateFixture(initialFace: CardFace.FaceDown);

            Execute(fixture, targetFace: CardFace.FaceDown);

            Assert.That(fixture.TargetCard.Face, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void Execute_WhenTargetFaceMatchesExistingFace_AcceptsAndAdvancesRevisionOnce()
        {
            FlipFixture fixture = CreateFixture(revision: 20, initialFace: CardFace.FaceUp);

            FlipCardResult result = Execute(fixture, targetFace: CardFace.FaceUp);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetCard.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.Match.Revision, Is.EqualTo(21));
        }

        [Test]
        public void Execute_WhenSuccessful_AdvancesMatchRevisionExactlyOnce()
        {
            FlipFixture fixture = CreateFixture(revision: 6);

            Execute(fixture);

            Assert.That(fixture.Match.Revision, Is.EqualTo(7));
        }

        [Test]
        public void Execute_WhenSuccessful_ReturnedRevisionEqualsMatchRevision()
        {
            FlipFixture fixture = CreateFixture(revision: 3);

            FlipCardResult result = Execute(fixture);

            Assert.That(result.Revision, Is.EqualTo(fixture.Match.Revision));
        }

        [Test]
        public void Execute_WhenExpectedRevisionIsNull_AcceptsCommand()
        {
            FlipFixture fixture = CreateFixture(revision: 9);

            FlipCardResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetCard.BaseState.Id, expectedRevision: null));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
        }

        [Test]
        public void Execute_WhenExpectedRevisionMatches_AcceptsCommand()
        {
            FlipFixture fixture = CreateFixture(revision: 9);

            FlipCardResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetCard.BaseState.Id, expectedRevision: 9));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
        }

        [TestCase(FlipFailureScenario.NullCommand)]
        [TestCase(FlipFailureScenario.MatchIdMismatch)]
        [TestCase(FlipFailureScenario.RevisionConflict)]
        [TestCase(FlipFailureScenario.ObjectNotFound)]
        [TestCase(FlipFailureScenario.PawnObject)]
        [TestCase(FlipFailureScenario.TokenObject)]
        [TestCase(FlipFailureScenario.ObjectUserLocked)]
        [TestCase(FlipFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_CardFaceAndRevisionRemainUnchanged(FlipFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            CardFace faceBefore = failure.TargetCard.Face;
            long revisionBefore = failure.Match.Revision;

            FlipCardResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(failure.TargetCard.Face, Is.EqualTo(faceBefore));
            Assert.That(failure.Match.Revision, Is.EqualTo(revisionBefore));
        }

        [TestCase(FlipFailureScenario.NullCommand)]
        [TestCase(FlipFailureScenario.MatchIdMismatch)]
        [TestCase(FlipFailureScenario.RevisionConflict)]
        [TestCase(FlipFailureScenario.ObjectNotFound)]
        [TestCase(FlipFailureScenario.PawnObject)]
        [TestCase(FlipFailureScenario.TokenObject)]
        [TestCase(FlipFailureScenario.ObjectUserLocked)]
        [TestCase(FlipFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_BaseObjectStateRemainsUnchanged(FlipFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            ObjectSnapshot targetBefore = ObjectSnapshot.Capture(failure.TargetCard.BaseState);

            failure.Execute();

            targetBefore.AssertMatches(failure.TargetCard.BaseState);
        }

        [TestCase(FlipFailureScenario.NullCommand)]
        [TestCase(FlipFailureScenario.MatchIdMismatch)]
        [TestCase(FlipFailureScenario.RevisionConflict)]
        [TestCase(FlipFailureScenario.ObjectNotFound)]
        [TestCase(FlipFailureScenario.PawnObject)]
        [TestCase(FlipFailureScenario.TokenObject)]
        [TestCase(FlipFailureScenario.ObjectUserLocked)]
        [TestCase(FlipFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_OtherObjectsRemainUnchanged(FlipFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            CardFace otherCardFaceBefore = failure.OtherCard.Face;
            ObjectSnapshot otherCardBefore = ObjectSnapshot.Capture(failure.OtherCard.BaseState);
            ObjectSnapshot pawnBefore = ObjectSnapshot.Capture(failure.Pawn.BaseState);
            ObjectSnapshot tokenBefore = ObjectSnapshot.Capture(failure.Token.BaseState);

            failure.Execute();

            Assert.That(failure.OtherCard.Face, Is.EqualTo(otherCardFaceBefore));
            otherCardBefore.AssertMatches(failure.OtherCard.BaseState);
            pawnBefore.AssertMatches(failure.Pawn.BaseState);
            tokenBefore.AssertMatches(failure.Token.BaseState);
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesBasePose()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();
            TabletopPose poseBefore = fixture.TargetCard.BaseState.Pose;

            Execute(fixture);

            Assert.That(fixture.TargetCard.BaseState.Pose, Is.EqualTo(poseBefore));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesContainerId()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();
            ContainerId containerBefore = fixture.TargetCard.BaseState.ContainerId;

            Execute(fixture);

            Assert.That(fixture.TargetCard.BaseState.ContainerId, Is.EqualTo(containerBefore));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesOwnerPlayerId()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();
            PlayerId ownerBefore = fixture.TargetCard.BaseState.OwnerPlayerId;

            Execute(fixture);

            Assert.That(fixture.TargetCard.BaseState.OwnerPlayerId, Is.EqualTo(ownerBefore));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesObjectVisibility()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();
            ObjectVisibility visibilityBefore = fixture.TargetCard.BaseState.Visibility;

            Execute(fixture);

            Assert.That(fixture.TargetCard.BaseState.Visibility, Is.EqualTo(visibilityBefore));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesUserLockState()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();
            bool userLockBefore = fixture.TargetCard.BaseState.IsUserLocked;

            Execute(fixture);

            Assert.That(fixture.TargetCard.BaseState.IsUserLocked, Is.EqualTo(userLockBefore));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesObjectIdAndDefinitionId()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();
            TabletopObjectId objectIdBefore = fixture.TargetCard.BaseState.Id;
            ObjectDefinitionId definitionIdBefore = fixture.TargetCard.BaseState.DefinitionId;

            Execute(fixture);

            Assert.That(fixture.TargetCard.BaseState.Id, Is.EqualTo(objectIdBefore));
            Assert.That(fixture.TargetCard.BaseState.DefinitionId, Is.EqualTo(definitionIdBefore));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesOtherCardsPawnsAndTokens()
        {
            FlipFixture fixture = CreateFixtureWithOtherObjects();
            CardFace otherCardFaceBefore = fixture.OtherCard.Face;
            ObjectSnapshot otherCardBefore = ObjectSnapshot.Capture(fixture.OtherCard.BaseState);
            ObjectSnapshot pawnBefore = ObjectSnapshot.Capture(fixture.Pawn.BaseState);
            ObjectSnapshot tokenBefore = ObjectSnapshot.Capture(fixture.Token.BaseState);

            Execute(fixture);

            Assert.That(fixture.OtherCard.Face, Is.EqualTo(otherCardFaceBefore));
            otherCardBefore.AssertMatches(fixture.OtherCard.BaseState);
            pawnBefore.AssertMatches(fixture.Pawn.BaseState);
            tokenBefore.AssertMatches(fixture.Token.BaseState);
        }

        [Test]
        public void Execute_DoesNotRequireCardView()
        {
            FlipFixture fixture = CreateFixture();

            FlipCardResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireSelectionState()
        {
            FlipFixture fixture = CreateFixture();

            FlipCardResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireLocalLockService()
        {
            FlipFixture fixture = CreateFixture();

            FlipCardResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireInputSystem()
        {
            FlipFixture fixture = CreateFixture();

            FlipCardResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireRendererOrMaterial()
        {
            FlipFixture fixture = CreateFixture();

            FlipCardResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_PreservesRequestedByPlayerIdWithoutInventingPermissionRule()
        {
            PlayerId requester = PlayerId.New();
            PlayerId owner = PlayerId.New();
            FlipFixture fixture = CreateFixtureWithOtherObjects(ownerPlayerId: owner);
            FlipCardCommand command = CreateCommand(
                fixture.Match.Id,
                fixture.TargetCard.BaseState.Id,
                requestedByPlayerId: requester);

            FlipCardResult result = Execute(fixture, command);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(command.Context.RequestedByPlayerId, Is.EqualTo(requester));
            Assert.That(fixture.TargetCard.BaseState.OwnerPlayerId, Is.EqualTo(owner));
        }

        private static FlipCardResult Execute(
            FlipFixture fixture,
            FlipCardCommand command = null,
            CardFace targetFace = CardFace.FaceUp)
        {
            FlipCardCommand actualCommand = command
                ?? CreateCommand(
                    fixture.Match.Id,
                    fixture.TargetCard.BaseState.Id,
                    targetFace,
                    fixture.Match.Revision);
            FlipCardUseCase useCase = new FlipCardUseCase();
            return useCase.Execute(fixture.Match, actualCommand);
        }

        private static void AssertFailure(
            FlipCardResult result,
            CommandResultStatus expectedStatus,
            FlipCardError expectedError)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        private static FailureFixture CreateFailureFixture(FlipFailureScenario scenario)
        {
            switch (scenario)
            {
                case FlipFailureScenario.NullCommand:
                {
                    FlipFixture fixture = CreateFixtureWithOtherObjects();
                    return new FailureFixture(fixture, null);
                }

                case FlipFailureScenario.MatchIdMismatch:
                {
                    FlipFixture fixture = CreateFixtureWithOtherObjects();
                    FlipCardCommand command = CreateCommand(MatchId.New(), fixture.TargetCard.BaseState.Id);
                    return new FailureFixture(fixture, command);
                }

                case FlipFailureScenario.RevisionConflict:
                {
                    FlipFixture fixture = CreateFixtureWithOtherObjects(revision: 3);
                    FlipCardCommand command = CreateCommand(
                        fixture.Match.Id,
                        fixture.TargetCard.BaseState.Id,
                        expectedRevision: 4);
                    return new FailureFixture(fixture, command);
                }

                case FlipFailureScenario.ObjectNotFound:
                {
                    FlipFixture fixture = CreateFixtureWithOtherObjects();
                    FlipCardCommand command = CreateCommand(fixture.Match.Id, TabletopObjectId.New());
                    return new FailureFixture(fixture, command);
                }

                case FlipFailureScenario.PawnObject:
                {
                    FlipFixture fixture = CreateFixtureWithOtherObjects();
                    FlipCardCommand command = CreateCommand(fixture.Match.Id, fixture.Pawn.BaseState.Id);
                    return new FailureFixture(fixture, command);
                }

                case FlipFailureScenario.TokenObject:
                {
                    FlipFixture fixture = CreateFixtureWithOtherObjects();
                    FlipCardCommand command = CreateCommand(fixture.Match.Id, fixture.Token.BaseState.Id);
                    return new FailureFixture(fixture, command);
                }

                case FlipFailureScenario.ObjectUserLocked:
                {
                    FlipFixture fixture = CreateFixtureWithOtherObjects(isUserLocked: true);
                    FlipCardCommand command = CreateCommand(fixture.Match.Id, fixture.TargetCard.BaseState.Id);
                    return new FailureFixture(fixture, command);
                }

                case FlipFailureScenario.RevisionOverflow:
                {
                    FlipFixture fixture = CreateFixtureWithOtherObjects(revision: long.MaxValue);
                    FlipCardCommand command = CreateCommand(
                        fixture.Match.Id,
                        fixture.TargetCard.BaseState.Id,
                        expectedRevision: long.MaxValue);
                    return new FailureFixture(fixture, command);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported failure scenario.");
            }
        }

        private static FlipFixture CreateFixture(
            long revision = 0,
            bool isUserLocked = false,
            CardFace initialFace = CardFace.FaceDown)
        {
            TabletopObjectState target = CreateObjectState(TabletopObjectKind.Card, isUserLocked: isUserLocked);
            CardInstanceState card = new CardInstanceState(target, initialFace);
            MatchState match = CreateMatch(revision: revision, cards: new[] { card });

            return new FlipFixture(match, card);
        }

        private static FlipFixture CreateFixtureWithOtherObjects(
            long revision = 0,
            bool isUserLocked = false,
            PlayerId? ownerPlayerId = null)
        {
            ContainerState container = new ContainerState(
                ContainerId.New(),
                ContainerKind.Generic,
                SeatId.Empty,
                ObjectVisibility.OwnerOnly,
                0);
            TabletopObjectState target = CreateObjectState(
                TabletopObjectKind.Card,
                initialPose: CreatePose(x: 4.5, y: -8.25, rotationDegrees: 10f, layer: 2, localOrder: 5),
                ownerPlayerId: ownerPlayerId ?? PlayerId.New(),
                visibility: ObjectVisibility.OwnerOnly,
                isUserLocked: isUserLocked);
            TabletopObjectState otherCardObject = CreateObjectState(
                TabletopObjectKind.Card,
                initialPose: CreatePose(x: -1.5, y: 2.25, rotationDegrees: 15f, layer: 1, localOrder: 4));
            TabletopObjectState pawnObject = CreateObjectState(
                TabletopObjectKind.Pawn,
                initialPose: CreatePose(x: -3.5, y: 4.75, rotationDegrees: 20f, layer: 3, localOrder: 6));
            TabletopObjectState tokenObject = CreateObjectState(
                TabletopObjectKind.Token,
                initialPose: CreatePose(x: 7.5, y: -1.75, rotationDegrees: -20f, layer: -1, localOrder: -6));

            ContainerTransferService transferService = new ContainerTransferService();
            transferService.PlaceIntoContainer(target, container);

            CardInstanceState targetCard = new CardInstanceState(target, CardFace.FaceUp);
            CardInstanceState otherCard = new CardInstanceState(otherCardObject, CardFace.FaceDown);
            PawnState pawn = new PawnState(pawnObject);
            TokenState token = new TokenState(tokenObject);
            MatchState match = CreateMatch(
                revision: revision,
                cards: new[] { targetCard, otherCard },
                pawns: new[] { pawn },
                tokens: new[] { token },
                containers: new[] { container });

            return new FlipFixture(match, targetCard, otherCard, pawn, token);
        }

        private static FlipCardCommand CreateCommand(
            MatchId matchId,
            TabletopObjectId objectId,
            CardFace targetFace = CardFace.FaceUp,
            long? expectedRevision = 0,
            PlayerId? requestedByPlayerId = null)
        {
            CommandContext context = new CommandContext(
                CommandId.New(),
                matchId,
                requestedByPlayerId ?? PlayerId.New(),
                expectedRevision);

            return new FlipCardCommand(context, objectId, targetFace);
        }

        private static MatchState CreateMatch(
            long revision = 0,
            CardInstanceState[] cards = null,
            PawnState[] pawns = null,
            TokenState[] tokens = null,
            ContainerState[] containers = null)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards ?? Array.Empty<CardInstanceState>(),
                pawns ?? Array.Empty<PawnState>(),
                tokens ?? Array.Empty<TokenState>(),
                containers ?? Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());
        }

        private static TabletopObjectState CreateObjectState(
            TabletopObjectKind kind,
            TabletopObjectId? id = null,
            ObjectDefinitionId? definitionId = null,
            TabletopPose? initialPose = null,
            ContainerId? containerId = null,
            PlayerId? ownerPlayerId = null,
            ObjectVisibility visibility = ObjectVisibility.Public,
            bool isUserLocked = false)
        {
            return new TabletopObjectState(
                id ?? TabletopObjectId.New(),
                definitionId ?? ObjectDefinitionId.New(),
                kind,
                initialPose ?? TabletopPose.Default,
                containerId ?? ContainerId.Empty,
                ownerPlayerId ?? PlayerId.Empty,
                visibility,
                isUserLocked);
        }

        private static TabletopPose CreatePose(
            double x = 1.0,
            double y = 2.0,
            float rotationDegrees = 30f,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, layer, localOrder);
        }

        private sealed class FlipFixture
        {
            public FlipFixture(
                MatchState match,
                CardInstanceState targetCard,
                CardInstanceState otherCard = null,
                PawnState pawn = null,
                TokenState token = null)
            {
                Match = match;
                TargetCard = targetCard;
                OtherCard = otherCard;
                Pawn = pawn;
                Token = token;
            }

            public MatchState Match { get; }

            public CardInstanceState TargetCard { get; }

            public CardInstanceState OtherCard { get; }

            public PawnState Pawn { get; }

            public TokenState Token { get; }
        }

        private sealed class FailureFixture
        {
            public FailureFixture(FlipFixture fixture, FlipCardCommand command)
            {
                Match = fixture.Match;
                TargetCard = fixture.TargetCard;
                OtherCard = fixture.OtherCard;
                Pawn = fixture.Pawn;
                Token = fixture.Token;
                Command = command;
            }

            public MatchState Match { get; }

            public CardInstanceState TargetCard { get; }

            public CardInstanceState OtherCard { get; }

            public PawnState Pawn { get; }

            public TokenState Token { get; }

            public FlipCardCommand Command { get; }

            public FlipCardResult Execute()
            {
                FlipCardUseCase useCase = new FlipCardUseCase();
                return useCase.Execute(Match, Command);
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
