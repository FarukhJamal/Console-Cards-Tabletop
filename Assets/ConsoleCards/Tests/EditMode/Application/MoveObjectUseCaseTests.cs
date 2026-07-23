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
    public sealed class MoveObjectUseCaseTests
    {
        public enum MoveFailureScenario
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
            MoveObjectUseCase useCase = new MoveObjectUseCase();

            MoveObjectResult result = useCase.Execute(null, CreateCommand(MatchId.New(), TabletopObjectId.New()));

            AssertFailure(result, CommandResultStatus.Invalid, MoveObjectError.MatchRequired);
        }

        [Test]
        public void Execute_WhenCommandIsNull_ReturnsInvalidCommandRequired()
        {
            MoveFixture fixture = CreateFixture();
            MoveObjectUseCase useCase = new MoveObjectUseCase();

            MoveObjectResult result = useCase.Execute(fixture.Match, null);

            AssertFailure(result, CommandResultStatus.Invalid, MoveObjectError.CommandRequired);
        }

        [Test]
        public void Execute_WhenMatchIdMismatches_ReturnsInvalidMatchIdMismatch()
        {
            MoveFixture fixture = CreateFixture();

            MoveObjectResult result = Execute(
                fixture,
                CreateCommand(MatchId.New(), fixture.TargetObject.Id));

            AssertFailure(result, CommandResultStatus.Invalid, MoveObjectError.MatchIdMismatch);
        }

        [Test]
        public void Execute_WhenExpectedRevisionMismatches_ReturnsConflictRevisionConflict()
        {
            MoveFixture fixture = CreateFixture(revision: 4);

            MoveObjectResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetObject.Id, expectedRevision: 5));

            AssertFailure(result, CommandResultStatus.Conflict, MoveObjectError.RevisionConflict);
        }

        [Test]
        public void Execute_WhenObjectIsMissing_ReturnsRejectedObjectNotFound()
        {
            MoveFixture fixture = CreateFixture();

            MoveObjectResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, TabletopObjectId.New()));

            AssertFailure(result, CommandResultStatus.Rejected, MoveObjectError.ObjectNotFound);
        }

        [Test]
        public void Execute_WhenObjectIsUserLocked_ReturnsRejectedObjectUserLocked()
        {
            MoveFixture fixture = CreateFixture(isUserLocked: true);

            MoveObjectResult result = Execute(fixture);

            AssertFailure(result, CommandResultStatus.Rejected, MoveObjectError.ObjectUserLocked);
        }

        [Test]
        public void Execute_WhenRevisionIsLongMaxValue_ReturnsConflictRevisionOverflow()
        {
            MoveFixture fixture = CreateFixture(revision: long.MaxValue);

            MoveObjectResult result = Execute(fixture);

            AssertFailure(result, CommandResultStatus.Conflict, MoveObjectError.RevisionOverflow);
        }

        [TestCase(TabletopObjectKind.Card)]
        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void Execute_WhenSuccessful_UpdatesBaseStatePose(TabletopObjectKind kind)
        {
            TabletopPose targetPose = CreatePose(x: 8.25, y: -6.5, rotationDegrees: 95f, layer: 3, localOrder: 11);
            MoveFixture fixture = CreateFixture(kind: kind);

            MoveObjectResult result = Execute(fixture, targetPose: targetPose);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetObject.Pose, Is.EqualTo(targetPose));
        }

        [Test]
        public void Execute_WhenSuccessful_UpdatesPosition()
        {
            TabletopPose targetPose = CreatePose(x: 12.5, y: -9.25);
            MoveFixture fixture = CreateFixture();

            Execute(fixture, targetPose: targetPose);

            Assert.That(fixture.TargetObject.Pose.Position, Is.EqualTo(targetPose.Position));
        }

        [Test]
        public void Execute_WhenSuccessful_UpdatesRotation()
        {
            TabletopPose targetPose = CreatePose(rotationDegrees: -450f);
            MoveFixture fixture = CreateFixture();

            Execute(fixture, targetPose: targetPose);

            Assert.That(fixture.TargetObject.Pose.RotationDegrees, Is.EqualTo(-450f));
        }

        [Test]
        public void Execute_WhenSuccessful_UpdatesLayer()
        {
            TabletopPose targetPose = CreatePose(layer: -4);
            MoveFixture fixture = CreateFixture();

            Execute(fixture, targetPose: targetPose);

            Assert.That(fixture.TargetObject.Pose.Layer, Is.EqualTo(-4));
        }

        [Test]
        public void Execute_WhenSuccessful_UpdatesLocalOrder()
        {
            TabletopPose targetPose = CreatePose(localOrder: -12);
            MoveFixture fixture = CreateFixture();

            Execute(fixture, targetPose: targetPose);

            Assert.That(fixture.TargetObject.Pose.LocalOrder, Is.EqualTo(-12));
        }

        [Test]
        public void Execute_WhenSuccessful_AdvancesMatchRevisionExactlyOnce()
        {
            MoveFixture fixture = CreateFixture(revision: 6);

            Execute(fixture);

            Assert.That(fixture.Match.Revision, Is.EqualTo(7));
        }

        [Test]
        public void Execute_WhenSuccessful_ReturnedRevisionEqualsMatchRevision()
        {
            MoveFixture fixture = CreateFixture(revision: 3);

            MoveObjectResult result = Execute(fixture);

            Assert.That(result.Revision, Is.EqualTo(fixture.Match.Revision));
        }

        [Test]
        public void Execute_WhenExpectedRevisionIsNull_AcceptsCommand()
        {
            MoveFixture fixture = CreateFixture(revision: 9);

            MoveObjectResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetObject.Id, expectedRevision: null));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
        }

        [Test]
        public void Execute_WhenExpectedRevisionMatches_AcceptsCommand()
        {
            MoveFixture fixture = CreateFixture(revision: 9);

            MoveObjectResult result = Execute(
                fixture,
                CreateCommand(fixture.Match.Id, fixture.TargetObject.Id, expectedRevision: 9));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Status, Is.EqualTo(CommandResultStatus.Accepted));
        }

        [Test]
        public void Execute_WhenMovingToExistingPose_AcceptsAndAdvancesRevisionOnce()
        {
            TabletopPose initialPose = CreatePose(x: 4.0, y: 5.0, rotationDegrees: 15f, layer: 1, localOrder: 2);
            MoveFixture fixture = CreateFixture(revision: 20, initialPose: initialPose);

            MoveObjectResult result = Execute(fixture, targetPose: initialPose);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.TargetObject.Pose, Is.EqualTo(initialPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(21));
        }

        [TestCase(MoveFailureScenario.NullCommand)]
        [TestCase(MoveFailureScenario.MatchIdMismatch)]
        [TestCase(MoveFailureScenario.RevisionConflict)]
        [TestCase(MoveFailureScenario.ObjectNotFound)]
        [TestCase(MoveFailureScenario.ObjectUserLocked)]
        [TestCase(MoveFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_TargetPoseAndRevisionRemainUnchanged(MoveFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            ObjectSnapshot targetBefore = ObjectSnapshot.Capture(failure.TargetObject);
            long revisionBefore = failure.Match.Revision;

            MoveObjectResult result = failure.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(failure.TargetObject.Pose, Is.EqualTo(targetBefore.Pose));
            Assert.That(failure.Match.Revision, Is.EqualTo(revisionBefore));
        }

        [TestCase(MoveFailureScenario.NullCommand)]
        [TestCase(MoveFailureScenario.MatchIdMismatch)]
        [TestCase(MoveFailureScenario.RevisionConflict)]
        [TestCase(MoveFailureScenario.ObjectNotFound)]
        [TestCase(MoveFailureScenario.ObjectUserLocked)]
        [TestCase(MoveFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_TargetNonPoseStateRemainsUnchanged(MoveFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            ObjectSnapshot targetBefore = ObjectSnapshot.Capture(failure.TargetObject);

            failure.Execute();

            targetBefore.AssertMatches(failure.TargetObject);
        }

        [TestCase(MoveFailureScenario.NullCommand)]
        [TestCase(MoveFailureScenario.MatchIdMismatch)]
        [TestCase(MoveFailureScenario.RevisionConflict)]
        [TestCase(MoveFailureScenario.ObjectNotFound)]
        [TestCase(MoveFailureScenario.ObjectUserLocked)]
        [TestCase(MoveFailureScenario.RevisionOverflow)]
        public void Execute_WhenFailureOccurs_OtherObjectRemainsUnchanged(MoveFailureScenario scenario)
        {
            FailureFixture failure = CreateFailureFixture(scenario);
            ObjectSnapshot otherBefore = ObjectSnapshot.Capture(failure.OtherObject);

            failure.Execute();

            otherBefore.AssertMatches(failure.OtherObject);
        }

        [Test]
        public void Execute_WhenSuccessful_DoesNotModifyNonPoseState()
        {
            MoveFixture fixture = CreateFixtureWithAssignedNonPoseState();
            ObjectSnapshot targetBefore = ObjectSnapshot.Capture(fixture.TargetObject);

            Execute(fixture);

            Assert.That(fixture.TargetObject.Pose, Is.Not.EqualTo(targetBefore.Pose));
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
            TabletopObjectState other = CreateObjectState(TabletopObjectKind.Pawn, initialPose: CreatePose(x: -2.0, y: 3.0));
            PawnState otherPawn = new PawnState(other);
            MatchState match = CreateMatch(cards: new[] { new CardInstanceState(target, CardFace.FaceDown) }, pawns: new[] { otherPawn });
            MoveFixture fixture = new MoveFixture(match, target);
            ObjectSnapshot otherBefore = ObjectSnapshot.Capture(other);

            Execute(fixture);

            otherBefore.AssertMatches(other);
        }

        [Test]
        public void Execute_DoesNotRequireView()
        {
            MoveFixture fixture = CreateFixture();

            MoveObjectResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireSelectionState()
        {
            MoveFixture fixture = CreateFixture();

            MoveObjectResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_DoesNotRequireLocalInteractionLockService()
        {
            MoveFixture fixture = CreateFixture();

            MoveObjectResult result = Execute(fixture);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Execute_PreservesRequestedByPlayerIdWithoutInventingPermissionRule()
        {
            PlayerId requester = PlayerId.New();
            PlayerId owner = PlayerId.New();
            MoveFixture fixture = CreateFixture(ownerPlayerId: owner);
            MoveObjectCommand command = CreateCommand(
                fixture.Match.Id,
                fixture.TargetObject.Id,
                requestedByPlayerId: requester);

            MoveObjectResult result = Execute(fixture, command);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(command.Context.RequestedByPlayerId, Is.EqualTo(requester));
            Assert.That(fixture.TargetObject.OwnerPlayerId, Is.EqualTo(owner));
        }

        private static MoveObjectResult Execute(
            MoveFixture fixture,
            MoveObjectCommand command = null,
            TabletopPose? targetPose = null)
        {
            MoveObjectCommand actualCommand = command
                ?? CreateCommand(
                    fixture.Match.Id,
                    fixture.TargetObject.Id,
                    targetPose ?? CreatePose(),
                    fixture.Match.Revision);
            MoveObjectUseCase useCase = new MoveObjectUseCase();
            return useCase.Execute(fixture.Match, actualCommand);
        }

        private static void AssertFailure(
            MoveObjectResult result,
            CommandResultStatus expectedStatus,
            MoveObjectError expectedError)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(result.Revision, Is.EqualTo(-1));
        }

        private static FailureFixture CreateFailureFixture(MoveFailureScenario scenario)
        {
            switch (scenario)
            {
                case MoveFailureScenario.NullCommand:
                {
                    MoveFixture fixture = CreateFixtureWithAssignedNonPoseState();
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, null);
                }

                case MoveFailureScenario.MatchIdMismatch:
                {
                    MoveFixture fixture = CreateFixtureWithAssignedNonPoseState();
                    MoveObjectCommand command = CreateCommand(MatchId.New(), fixture.TargetObject.Id);
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                case MoveFailureScenario.RevisionConflict:
                {
                    MoveFixture fixture = CreateFixtureWithAssignedNonPoseState(revision: 3);
                    MoveObjectCommand command = CreateCommand(
                        fixture.Match.Id,
                        fixture.TargetObject.Id,
                        expectedRevision: 4);
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                case MoveFailureScenario.ObjectNotFound:
                {
                    MoveFixture fixture = CreateFixtureWithAssignedNonPoseState();
                    MoveObjectCommand command = CreateCommand(fixture.Match.Id, TabletopObjectId.New());
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                case MoveFailureScenario.ObjectUserLocked:
                {
                    MoveFixture fixture = CreateFixtureWithAssignedNonPoseState(isUserLocked: true);
                    MoveObjectCommand command = CreateCommand(fixture.Match.Id, fixture.TargetObject.Id);
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                case MoveFailureScenario.RevisionOverflow:
                {
                    MoveFixture fixture = CreateFixtureWithAssignedNonPoseState(revision: long.MaxValue);
                    MoveObjectCommand command = CreateCommand(fixture.Match.Id, fixture.TargetObject.Id, expectedRevision: long.MaxValue);
                    return new FailureFixture(fixture.Match, fixture.TargetObject, fixture.OtherObject, command);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported failure scenario.");
            }
        }

        private static MoveFixture CreateFixture(
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

            return new MoveFixture(match, target);
        }

        private static MoveFixture CreateFixtureWithAssignedNonPoseState(
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
                ownerPlayerId: PlayerId.New(),
                visibility: ObjectVisibility.OwnerOnly,
                isUserLocked: isUserLocked);
            TabletopObjectState other = CreateObjectState(
                TabletopObjectKind.Pawn,
                initialPose: CreatePose(x: -3.5, y: 4.75, rotationDegrees: 10f, layer: 2, localOrder: 5));
            ContainerTransferService transferService = new ContainerTransferService();
            transferService.PlaceIntoContainer(target, container);
            MatchState match = CreateMatch(
                revision: revision,
                cards: new[] { new CardInstanceState(target, CardFace.FaceUp) },
                pawns: new[] { new PawnState(other) },
                containers: new[] { container });

            return new MoveFixture(match, target, other);
        }

        private static MoveObjectCommand CreateCommand(
            MatchId matchId,
            TabletopObjectId objectId,
            TabletopPose? targetPose = null,
            long? expectedRevision = 0,
            PlayerId? requestedByPlayerId = null)
        {
            CommandContext context = new CommandContext(
                CommandId.New(),
                matchId,
                requestedByPlayerId ?? PlayerId.New(),
                expectedRevision);

            return new MoveObjectCommand(context, objectId, targetPose ?? CreatePose());
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

        private sealed class MoveFixture
        {
            public MoveFixture(MatchState match, TabletopObjectState targetObject, TabletopObjectState otherObject = null)
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
                MoveObjectCommand command)
            {
                Match = match;
                TargetObject = targetObject;
                OtherObject = otherObject;
                Command = command;
            }

            public MatchState Match { get; }

            public TabletopObjectState TargetObject { get; }

            public TabletopObjectState OtherObject { get; }

            public MoveObjectCommand Command { get; }

            public MoveObjectResult Execute()
            {
                MoveObjectUseCase useCase = new MoveObjectUseCase();
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
