using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class PhysicalObjectAuthorityTests
    {
        private MatchState match;
        private PlayerId actor;
        private TabletopObjectState obj;
        private CommitPhysicalObjectUseCase useCase;

        [SetUp]
        public void SetUp()
        {
            actor = PlayerId.New();
            match = new MatchState(MatchId.New(), GameTemplateId.Empty, 0, Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(), Array.Empty<TokenState>(), Array.Empty<ContainerState>(), Array.Empty<SeatState>());
            obj = new TabletopObjectState(TabletopObjectId.New(), ObjectDefinitionId.New(), TabletopObjectKind.Die,
                TabletopPose.Default, ContainerId.Empty, actor, ObjectVisibility.Public, false);
            match.AddUncontainedDie(new DieState(obj, 6, 1));
            useCase = new CommitPhysicalObjectUseCase();
        }

        private PhysicalObjectState State(PhysicalObjectMode mode, float y = -20f) =>
            new PhysicalObjectState(new PhysicalVector3(50f, y, 70f), new PhysicalRotation(0.2f, 0.3f, 0.4f, 0.5f),
                default, default, mode, actor);
        private CommitPhysicalObjectCommand Command(PhysicalObjectState state, int? value = null, CommandId? id = null) =>
            new CommitPhysicalObjectCommand(new CommandContext(id ?? CommandId.New(), match.Id, actor, match.Revision),
                obj.Id, state, obj.PhysicalRevision, value);

        [Test]
        public void OffTableSettlement_CommitsFullPoseAndDieValueOnce_WithoutChangingLayout()
        {
            PhysicalObjectState state = State(PhysicalObjectMode.Sleeping);
            TabletopPose layout = obj.Pose;
            Assert.That(useCase.Execute(match, new[] { actor }, Command(state, 6)).Succeeded, Is.True);
            Assert.That(obj.PhysicalState, Is.SameAs(state));
            Assert.That(obj.Pose, Is.EqualTo(layout));
            Assert.That(match.Dice[obj.Id].CurrentValue, Is.EqualTo(6));
            Assert.That(match.Revision, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateCommand_WithFreshRevision_IsRejected()
        {
            CommandId id = CommandId.New();
            Assert.That(useCase.Execute(match, new[] { actor }, Command(State(PhysicalObjectMode.Dynamic), null, id)).Succeeded, Is.True);
            Assert.That(useCase.Execute(match, new[] { actor }, Command(State(PhysicalObjectMode.Sleeping), 2, id)).Succeeded, Is.False);
            Assert.That(match.Revision, Is.EqualTo(1));
            Assert.That(match.Dice[obj.Id].CurrentValue, Is.EqualTo(1));
        }

        [Test]
        public void StaleSimulationAndInactiveActor_CannotCommit()
        {
            CommitPhysicalObjectCommand stale = Command(State(PhysicalObjectMode.Sleeping), 4);
            Assert.That(useCase.Execute(match, Array.Empty<PlayerId>(), stale).Succeeded, Is.False);
            Assert.That(useCase.Execute(match, new[] { actor }, Command(State(PhysicalObjectMode.Dynamic))).Succeeded, Is.True);
            Assert.That(useCase.Execute(match, new[] { actor }, stale).Succeeded, Is.False);
        }

        [TestCase(0)]
        [TestCase(7)]
        public void InvalidFace_LeavesPoseValueAndRevisionUnchanged(int value)
        {
            Assert.That(useCase.Execute(match, new[] { actor }, Command(State(PhysicalObjectMode.Sleeping), value)).Succeeded, Is.False);
            Assert.That(obj.PhysicalState, Is.Null);
            Assert.That(match.Revision, Is.Zero);
        }

        [Test]
        public void Containment_ClearsLooseState_AndRejectsSimulation()
        {
            useCase.Execute(match, new[] { actor }, Command(State(PhysicalObjectMode.Dynamic)));
            obj.SetContainer(ContainerId.New());
            Assert.That(obj.PhysicalState, Is.Null);
            Assert.That(useCase.Execute(match, new[] { actor }, Command(State(PhysicalObjectMode.Dynamic))).Succeeded, Is.False);
        }

        [Test]
        public void Snapshot_PreservesPhysicalPoseAndRevision()
        {
            useCase.Execute(match, new[] { actor }, Command(State(PhysicalObjectMode.Sleeping), 5));
            MatchState restored = GameTemplateInitialSnapshot.Capture(match).Restore();
            Assert.That(restored.GetObject(obj.Id).PhysicalState.Position.Y, Is.EqualTo(-20f));
            Assert.That(restored.GetObject(obj.Id).PhysicalRevision, Is.EqualTo(obj.PhysicalRevision));
            Assert.That(restored.Dice[obj.Id].CurrentValue, Is.EqualTo(5));
        }

        [Test]
        public void BatchWithMissingSurface_DoesNotPartiallyCreateCards()
        {
            CreateGenericCardBatchUseCase batch = new CreateGenericCardBatchUseCase(new GuidTabletopComponentIdentitySource(), new MissingSurface());
            CreateGenericCardBatchResult result = batch.Execute(match, new[] { actor }, new CreateGenericCardBatchRequest(
                new CommandContext(CommandId.New(), match.Id, actor, match.Revision), 4, TabletopPose.Default));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(match.Cards.Count, Is.Zero);
            Assert.That(match.Revision, Is.Zero);
        }

        [TestCase(TabletopComponentKind.Card)] [TestCase(TabletopComponentKind.Pawn)]
        [TestCase(TabletopComponentKind.Token)] [TestCase(TabletopComponentKind.Die)]
        public void LooseCreationWithoutSurface_DoesNotCommit(TabletopComponentKind kind)
        {
            CreateTabletopComponentUseCase create = new CreateTabletopComponentUseCase(
                new GuidTabletopComponentIdentitySource(), new MissingSurface());
            CreateTabletopComponentResult result = create.Execute(match, new[] { actor }, new CreateTabletopComponentRequest(
                new CommandContext(CommandId.New(), match.Id, actor, match.Revision), kind, TabletopPose.Default,
                kind == TabletopComponentKind.Die ? 6 : 0));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(CreateTabletopComponentError.PhysicalSurfaceRequired));
            Assert.That(match.ObjectCount, Is.EqualTo(1));
            Assert.That(match.Revision, Is.Zero);
        }

        private sealed class MissingSurface : IPhysicalPlacementResolver
        {
            public bool TryResolve(TabletopPose pose, PlayerId actor, out PhysicalObjectState state,
                TabletopComponentKind kind = TabletopComponentKind.Card)
            { state = null; return false; }
        }
    }
}
