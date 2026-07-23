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
    public sealed class RotateObjectUseCaseTests
    {
        public enum RotateFailureScenario
        {
            NullCommand,
            MatchIdMismatch,
            RevisionConflict,
            ObjectNotFound,
            ObjectUserLocked,
            RevisionOverflow
        }

        [Test]
        public void Execute_WhenMatchStateIsNull_ReturnsInvalidMatchRequired()
        {
            RotateObjectUseCase useCase = new RotateObjectUseCase();

            RotateObjectResult result = useCase.Execute(null, CreateCommand(MatchId.New(), TabletopObjectId.New()));

            AssertFailure(result, CommandResultStatus.Invalid, RotateObjectError.MatchRequired);
        }

        [Test]
        public void Execute_WhenCommandIsNull_ReturnsInvalidCommandRequired()
        {
            RotateFixture fixture = CreateFixture();
            RotateObjectUseCase useCase = new RotateObjectUseCase();

            RotateObjectResult result = useCase.Execute(fixture.Match, null);

            AssertFailure(result, CommandResultStatus.Invalid, RotateObjectError.CommandRequired);
        }

        [Test]
        public void Execute_WhenMatchIdMismatches_ReturnsInvalidMatchIdMismatch()
        {
            RotateFixture fixture = CreateFixture();

            RotateObjectResult result = Execute(
                fixture,
                CreateCommand(MatchId.New(), fixture.TargetObject.Id));

            AssertFailure(result, CommandResultStatus.Invalid, RotateObjectError.MatchIdMismatch);
        }

        [Test]
        public void Execute_WhenExpectedRevisionMismatches_ReturnsConflictRevisionConflict()
        {
            RotateFixture fixture = CreateFixture(revision: 4);

            RotateObjectResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetObject.Id, expectedRevision: 5));

            AssertFailure(result, CommandResultStatus.Conflict, RotateObjectError.RevisionConflict);
        }

        [Test]
        public void Execute_WhenObjectIsMissing_ReturnsRejectedObjectNotFound()
        {
            RotateFixture fixture = CreateFixture();

            RotateObjectResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, TabletopObjectId.New()));

            AssertFailure(result, CommandResultStatus.Rejected, RotateObjectError.ObjectNotFound);
        }

        [Test]
        public void Execute_WhenObjectIsUserLocked_ReturnsRejectedObjectUserLocked()
        {
            RotateFixture fixture = CreateFixture(isUserLocked: true);

            RotateObjectResult result = Execute(fixture);

            AssertFailure(result, CommandResultStatus.Rejected, RotateObjectError.ObjectUserLocked);
        }

        [Test]
        public void Execute_WhenRevisionIsLongMaxValue_ReturnsConflictRevisionOverflow()
        {
            RotateFixture fixture = CreateFixture(revision: long.MaxValue);

            RotateObjectResult result = Execute(fixture);

            AssertFailure(result, CommandResultStatus.Conflict, RotateObjectError.RevisionOverflow);
        }

        [TestCase(TabletopObjectKind.Card)]
        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void Execute_WhenSuccessful_UpdatesObjectRotation(TabletopObjectKind kind)
        {
            RotateFixture fixture = CreateFixture(kind: kind);

            RotateObjectResult result = Execute(fixture, targetRotationDegrees: 95f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetObject.Pose.RotationDegrees, Is.EqualTo(95f));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesPosition()
        {
            TabletopPose initialPose = CreatePose(x: 12.5, y: -9.25, rotationDegrees: 10f);
            RotateFixture fixture = CreateFixture(initialPose: initialPose);

            Execute(fixture, targetRotationDegrees: 180f);

            Assert.That(fixture.TargetObject.Pose.Position, Is.EqualTo(initialPose.Position));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesLayer()
        {
            TabletopPose initialPose = CreatePose(rotationDegrees: 10f, layer: -4);
            RotateFixture fixture = CreateFixture(initialPose: initialPose);

            Execute(fixture, targetRotationDegrees: 180f);

            Assert.That(fixture.TargetObject.Pose.Layer, Is.EqualTo(-4));
        }

        [Test]
        public void Execute_WhenSuccessful_PreservesLocalOrder()
        {
            TabletopPose initialPose = CreatePose(rotationDegrees: 10f, localOrder: -12);
            RotateFixture fixture = CreateFixture(initialPose: initialPose);

            Execute(fixture, targetRotationDegrees: 180f);

            Assert.That(fixture.TargetObject.Pose.LocalOrder, Is.EqualTo(-12));
        }

        [Test]
        public void Execute_WhenRotationIsNegative_PreservesExactly()
        {
            RotateFixture fixture = CreateFixture();

            Execute(fixture, targetRotationDegrees: -45f);

            Assert.That(fixture.TargetObject.Pose.RotationDegrees, Is.EqualTo(-45f));
        }

        [Test]
        public void Execute_WhenRotationIsAbove360_PreservesExactly()
        {
            RotateFixture fixture = CreateFixture();

            Execute(fixture, targetRotationDegrees: 765f);

            Assert.That(fixture.TargetObject.Pose.RotationDegrees, Is.EqualTo(765f));
        }

        [Test]
        public void Execute_WhenRotationIsBelowNegative360_PreservesExactly()
        {
            RotateFixture fixture = CreateFixture();

            Execute(fixture, targetRotationDegrees: -765f);

            Assert.That(fixture.TargetObject.Pose.RotationDegrees, Is.EqualTo(-765f));
        }

        [Test]
        public void Execute_WhenExpectedRevisionMatches_AcceptsCommand()
        {
            RotateFixture fixture = CreateFixture(revision: 9);

            RotateObjectResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetObject.Id, expectedRevision: 9));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
        }

        [Test]
        public void Execute_WhenExpectedRevisionIsNull_AcceptsCommand()
        {
            RotateFixture fixture = CreateFixture(revision: 9);

            RotateObjectResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetObject.Id, expectedRevision: null));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
        }

        [Test]
        public void Execute_WhenSuccessful_AdvancesMatchRevisionExactlyOnce()
        {
            RotateFixture fixture = CreateFixture(revision: 6);

            Execute(fixture);

            Assert.That(fixture.Match.Revision, Is.EqualTo(7));
        }

        [Test]
        public void Execute_WhenSuccessful_ReturnedRevisionEqualsMatchRevision()
        {
            RotateFixture fixture = CreateFixture(revision: 3);

            RotateObjectResult result = Execute(fixture);

            Assert.That(result.Revision, Is.EqualTo(fixture.Match.Revision));
        }

        [Test]
        public void Execute_WhenRotatingToCurrentRotation_AcceptsAndAdvancesRevisionOnce()
        {
            TabletopPose initialPose = CreatePose(rotationDegrees: 15f);
            RotateFixture fixture = CreateFixture(revision: 20, initialPose: initialPose);

            RotateObjectResult result = Execute(fixture, targetRotationDegrees: 15f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetObject.Pose, Is.EqualTo(initialPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(21));
        }

        [TestCase(RotateFailureScenario.NullCommand)]
        [TestCase(RotateFailureScenario.MatchIdMismatch)]
        [TestCase(RotateFailureScenario.RevisionConflict)]
        [TestCase(RotateFailureScenario.ObjectNotFound)]
        [TestCase(RotateFailureScenario.ObjectUserLocked)]
        [TestCase(RotateFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_PoseAndRevisionRemainUnchanged(RotateFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            ObjectSnapshot targetBefore = ObjectSnapshot.Capture(failure.TargetObject);
            long revisionBefore = failure.Match.Revision;

            RotateObjectResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(failure.TargetObject.Pose, Is.EqualTo(targetBefore.Pose));
            Assert.That(failure.Match.Revision, Is.EqualTo(revisionBefore));
        }

        [TestCase(RotateFailureScenario.NullCommand)]
        [TestCase(RotateFailureScenario.MatchIdMismatch)]
        [TestCase(RotateFailureScenario.RevisionConflict)]
        [TestCase(RotateFailureScenario.ObjectNotFound)]
        [TestCase(RotateFailureScenario.ObjectUserLocked)]
        [TestCase(RotateFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_TargetNonPoseStateRemainsUnchanged(RotateFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            ObjectSnapshot targetBefore = ObjectSnapshot.Capture(failure.TargetObject);

            failure.Execute();

            targetBefore.AssertMatches(failure.TargetObject);
        }

        [TestCase(RotateFailureScenario.NullCommand)]
        [TestCase(RotateFailureScenario.MatchIdMismatch)]
        [TestCase(RotateFailureScenario.RevisionConflict)]
        [TestCase(RotateFailureScenario.ObjectNotFound)]
        [TestCase(RotateFailureScenario.ObjectUserLocked)]
        [TestCase(RotateFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_OtherObjectRemainsUnchanged(RotateFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            ObjectSnapshot otherBefore = ObjectSnapshot.Capture(failure.OtherObject);

            failure.Execute();

            otherBefore.AssertMatches(failure.OtherObject);
        }

        [Test]
        public void Execute_WhenSuccessful_DoesNotModifyNonRotationState()
        {
            RotateFixture fixture = CreateFixtureWithAssignedNonPoseState();
            ObjectSnapshot targetBefore = ObjectSnapshot.Capture(fixture.TargetObject);

            Execute(fixture, targetRotationDegrees: 135f);

            Assert.That(fixture.TargetObject.Pose.RotationDegrees, Is.EqualTo(135f));
            Assert.That(fixture.TargetObject.Pose.Position, Is.EqualTo(targetBefore.Pose.Position));
            Assert.That(fixture.TargetObject.Pose.Layer, Is.EqualTo(targetBefore.Pose.Layer));
            Assert.That(fixture.TargetObject.Pose.LocalOrder, Is.EqualTo(targetBefore.Pose.LocalOrder));
            Assert.That(fixture.TargetObject.Id, Is.EqualTo(targetBefore.Id));
            Assert.That(fixture.TargetObject.DefinitionId, Is.EqualTo(targetBefore.DefinitionId));
            Assert.That(fixture.TargetObject.ContainerId, Is.EqualTo(targetBefore.ContainerId));
            Assert.That(fixture.TargetObject.OwnerPlayerId, Is.EqualTo(targetBefore.OwnerPlayerId));
            Assert.That(fixture.TargetObject.Visibility, Is.EqualTo(targetBefore.Visibility));
            Assert.That(fixture.TargetObject.IsUserLocked, Is.EqualTo(targetBefore.IsUserLocked));
        }

        [Test]
        public void Execute_WhenSuccessful_DoesNotModifyOtherObjectsInMatch()
        {
            TabletopObjectState target = CreateObjectState(TabletopObjectKind.Card);
            TabletopObjectState other = CreateObjectState(TabletopObjectKind.Pawn, initialPose: CreatePose(x: -2.0, y: 3.0, rotationDegrees: 10f));
            PawnState otherPawn = new PawnState(other);
            MatchState match = CreateMatch(cards: new[] { new CardInstanceState(target, CardFace.FaceDown) }, pawns: new[] { otherPawn });
            RotateFixture fixture = new RotateFixture(match, target);
            ObjectSnapshot otherBefore = ObjectSnapshot.Capture(other);

            Execute(fixture, targetRotationDegrees: 270f);

            otherBefore.AssertMatches(other);
        }

        [Test]
        public void Execute_DoesNotRequireView()
        {
            RotateFixture fixture = CreateFixture();

            RotateObjectResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireSelectionState()
        {
            RotateFixture fixture = CreateFixture();

            RotateObjectResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireLocalInteractionLockService()
        {
            RotateFixture fixture = CreateFixture();

            RotateObjectResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireInputSystem()
        {
            RotateFixture fixture = CreateFixture();

            RotateObjectResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_PreservesRequestedByPlayerIdWithoutInventingPermissionRule()
        {
            PlayerId requester = PlayerId.New();
            PlayerId owner = PlayerId.New();
            RotateFixture fixture = CreateFixture(ownerPlayerId: owner);
            RotateObjectCommand command = CreateCommand(
                fixture.Match.Id,
                fixture.TargetObject.Id,
                requestedByPlayerId: requester);

            RotateObjectResult result = Execute(fixture, command);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(command.Context.RequestedByPlayerId, Is.EqualTo(requester));
            Assert.That(fixture.TargetObject.OwnerPlayerId, Is.EqualTo(owner));
        }

        private static RotateObjectResult Execute(
            RotateFixture fixture,
            RotateObjectCommand command = null,
            float targetRotationDegrees = 90f)
        {
            RotateObjectCommand actualCommand = command
                ?? CreateCommand(
                    fixture.Match.Id,
                    fixture.TargetObject.Id,
                    targetRotationDegrees,
                    fixture.Match.Revision);
            RotateObjectUseCase useCase = new RotateObjectUseCase();
            return useCase.Execute(fixture.Match, actualCommand);
        }

        private static void AssertFailure(
            RotateObjectResult result,
            CommandResultStatus expectedStatus,
            RotateObjectError expectedError)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        private static FailureFixture CreateFailureFixture(RotateFailureScenario scenario)
        {
            switch (scenario)
            {
                case RotateFailureScenario.NullCommand:
                {
                    RotateFixture fixture = CreateFixtureWithAssignedNonPoseState();
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, null);
                }

                case RotateFailureScenario.MatchIdMismatch:
                {
                    RotateFixture fixture = CreateFixtureWithAssignedNonPoseState();
                    RotateObjectCommand command = CreateCommand(MatchId.New(), fixture.TargetObject.Id);
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                case RotateFailureScenario.RevisionConflict:
                {
                    RotateFixture fixture = CreateFixtureWithAssignedNonPoseState(revision: 3);
                    RotateObjectCommand command = CreateCommand(
                        fixture.Match.Id,
                        fixture.TargetObject.Id,
                        expectedRevision: 4);
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                case RotateFailureScenario.ObjectNotFound:
                {
                    RotateFixture fixture = CreateFixtureWithAssignedNonPoseState();
                    RotateObjectCommand command = CreateCommand(fixture.Match.Id, TabletopObjectId.New());
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                case RotateFailureScenario.ObjectUserLocked:
                {
                    RotateFixture fixture = CreateFixtureWithAssignedNonPoseState(isUserLocked: true);
                    RotateObjectCommand command = CreateCommand(fixture.Match.Id, fixture.TargetObject.Id);
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                case RotateFailureScenario.RevisionOverflow:
                {
                    RotateFixture fixture = CreateFixtureWithAssignedNonPoseState(revision: long.MaxValue);
                    RotateObjectCommand command = CreateCommand(fixture.Match.Id, fixture.TargetObject.Id, expectedRevision: long.MaxValue);
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported failure scenario.");
            }
        }

        private static RotateFixture CreateFixture(
            TabletopObjectKind kind = TabletopObjectKind.Card,
            long revision = 0,
            bool isUserLocked = false,
            TabletopPose? initialPose = null,
            PlayerId? ownerPlayerId = null)
        {
            TabletopObjectState target = CreateObjectState(
                kind,
                initialPose: initialPose,
                ownerPlayerId: ownerPlayerId,
                isUserLocked: isUserLocked);

            MatchState match;
            switch (kind)
            {
                case TabletopObjectKind.Card:
                    match = CreateMatch(revision: revision, cards: new[] { new CardInstanceState(target, CardFace.FaceDown) });
                    break;

                case TabletopObjectKind.Pawn:
                    match = CreateMatch(revision: revision, pawns: new[] { new PawnState(target) });
                    break;

                case TabletopObjectKind.Token:
                    match = CreateMatch(revision: revision, tokens: new[] { new TokenState(target) });
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported tabletop object kind.");
            }

            return new RotateFixture(match, target);
        }

        private static RotateFixture CreateFixtureWithAssignedNonPoseState(
            long revision = 0,
            bool isUserLocked = false)
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
                ownerPlayerId: PlayerId.New(),
                visibility: ObjectVisibility.OwnerOnly,
                isUserLocked: isUserLocked);
            TabletopObjectState other = CreateObjectState(
                TabletopObjectKind.Pawn,
                initialPose: CreatePose(x: -3.5, y: 4.75, rotationDegrees: 20f, layer: 3, localOrder: 6));
            ContainerTransferService transferService = new ContainerTransferService();
            transferService.PlaceIntoContainer(target, container);
            MatchState match = CreateMatch(
                revision: revision,
                cards: new[] { new CardInstanceState(target, CardFace.FaceUp) },
                pawns: new[] { new PawnState(other) },
                containers: new[] { container });

            return new RotateFixture(match, target, other);
        }

        private static RotateObjectCommand CreateCommand(
            MatchId matchId,
            TabletopObjectId objectId,
            float targetRotationDegrees = 90f,
            long? expectedRevision = 0,
            PlayerId? requestedByPlayerId = null)
        {
            CommandContext context = new CommandContext(
                CommandId.New(),
                matchId,
                requestedByPlayerId ?? PlayerId.New(),
                expectedRevision);

            return new RotateObjectCommand(context, objectId, targetRotationDegrees);
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

        private sealed class RotateFixture
        {
            public RotateFixture(MatchState match, TabletopObjectState targetObject, TabletopObjectState otherObject = null)
            {
                Match = match;
                TargetObject = targetObject;
                OtherObject = otherObject;
            }

            public MatchState Match { get; }

            public TabletopObjectState TargetObject { get; }

            public TabletopObjectState OtherObject { get; }
        }

        private sealed class FailureFixture
        {
            public FailureFixture(
                MatchState match,
                TabletopObjectState targetObject,
                TabletopObjectState otherObject,
                RotateObjectCommand command)
            {
                Match = match;
                TargetObject = targetObject;
                OtherObject = otherObject;
                Command = command;
            }

            public MatchState Match { get; }

            public TabletopObjectState TargetObject { get; }

            public TabletopObjectState OtherObject { get; }

            public RotateObjectCommand Command { get; }

            public RotateObjectResult Execute()
            {
                RotateObjectUseCase useCase = new RotateObjectUseCase();
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
