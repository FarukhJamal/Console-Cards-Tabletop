using System;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A (owner, 2026-11-13 live test: another player's pawn left floating with its reach lights on after a refused
    /// move). Pure C#: the host accepts a guest's hold, refuses what follows, and puts the piece back where it rested;
    /// a second table replays the same put-back. The real use case runs both; TableStateHash compares them.
    /// </summary>
    public sealed class HeldReturnTests
    {
        private static readonly PhysicalRotation Upright = new PhysicalRotation(0f, 0f, 0f, 1f);

        private static PhysicalObjectState State(float x, float y, PhysicalObjectMode mode, PlayerId controller) =>
            new PhysicalObjectState(new PhysicalVector3(x, y, 0.5f), Upright, default, default, mode, controller);

        private static CommandResult Commit(TrapFloorTestTable t, PlayerId who, TabletopObjectId id, PhysicalObjectState state) =>
            new CommitPhysicalObjectUseCase().Execute(t.Match, t.Players, new CommitPhysicalObjectCommand(
                new CommandContext(CommandId.New(), t.Match.Id, who, t.Match.Revision), id, state, t.Match.GetObject(id).PhysicalRevision));

        private static CommandResult Run(TrapFloorTestTable t, CommitPhysicalObjectCommand command) =>
            new CommitPhysicalObjectUseCase().Execute(t.Match, t.Players, command);

        // The host's pawn rests; the guest's player picks it up (the host accepts the hold, as it always has).
        private static (TrapFloorTestTable Host, TrapFloorTestTable Guest, HeldReturnPolicy Policy, PlayerId Owner, PlayerId Other, TabletopObjectId Pawn) GuestHoldsHostsPawn()
        {
            TrapFloorTestTable host = TrapFloorTestTable.Create(2, 11), guest = TrapFloorTestTable.Create(2, 11);
            PlayerId owner = host.Players[0], other = host.Players[1];
            TabletopObjectId pawn = host.SetupOf(owner).PawnId;
            HeldReturnPolicy policy = new HeldReturnPolicy();
            foreach (TrapFloorTestTable t in new[] { host, guest })
            {
                t.Match.GetObject(pawn).SetPhysicalState(State(0.5f, 0.1f, PhysicalObjectMode.Sleeping, owner));
            }

            PhysicalObjectState before = host.Match.GetObject(pawn).PhysicalState;
            PhysicalObjectState held = State(2.25f, 0.9f, PhysicalObjectMode.Held, other); // lifted in the guest's hand
            Assert.IsTrue(Commit(host, other, pawn, held).Succeeded, "the host accepts the hold");
            policy.NoteAccepted(host.Match, pawn, before, held);
            Assert.IsTrue(Commit(guest, other, pawn, held).Succeeded);
            return (host, guest, policy, owner, other, pawn);
        }

        [Test]
        public void RefusedRelease_PutsTheHeldPawnBackWhereItRested_OnEveryTable()
        {
            var (host, guest, policy, owner, other, pawn) = GuestHoldsHostsPawn();
            // The release is refused (NotYourPawn): the host's table still has the pawn held by the guest.
            Assert.AreEqual(PhysicalObjectMode.Held, host.Match.GetObject(pawn).PhysicalState.Mode);

            Assert.IsTrue(policy.TryBuildReturn(host.Match, other, pawn, CommandId.New(), out CommitPhysicalObjectCommand putBack));
            Assert.IsTrue(Run(host, putBack).Succeeded, "the host's table takes the put-back");
            policy.NoteReturned(pawn);
            byte[] wire = TableWire.Write(putBack, false);
            Assert.IsTrue(Run(guest, (CommitPhysicalObjectCommand)TableWire.Read(wire, 0, wire.Length, false)).Succeeded, "and every other table");

            PhysicalObjectState rest = host.Match.GetObject(pawn).PhysicalState;
            Assert.AreEqual(PhysicalObjectMode.Sleeping, rest.Mode, "no longer held: gravity, no floating, no held-pawn lights");
            Assert.AreEqual(0.5f, rest.Position.X, "back where it rested");
            Assert.AreEqual(0.1f, rest.Position.Y, "on the floor, not where the hand lifted it");
            Assert.AreEqual(0f, rest.Velocity.X);
            Assert.AreEqual(TableStateHash.Of(host.Match), TableStateHash.Of(guest.Match), "the same record on host and guest");
            Assert.AreEqual(1, policy.Returned);

            Assert.IsFalse(policy.TryBuildReturn(host.Match, other, pawn, CommandId.New(), out _), "put back once: nothing is held now");
            Assert.IsTrue(Commit(host, owner, pawn, State(0.5f, 0.9f, PhysicalObjectMode.Held, owner)).Succeeded,
                "its owner can pick it up again (the table no longer has it in another player's hand)");
        }

        [Test]
        public void NothingIsPutBack_WhenTheRefusedPlayerDoesNotHoldIt()
        {
            var (host, _, policy, owner, other, pawn) = GuestHoldsHostsPawn();
            Assert.IsFalse(policy.TryBuildReturn(host.Match, owner, pawn, CommandId.New(), out _), "held by someone else");
            Assert.IsFalse(policy.TryBuildReturn(host.Match, PlayerId.Empty, pawn, CommandId.New(), out _));

            HeldReturnPolicy fresh = new HeldReturnPolicy();
            Assert.IsFalse(fresh.TryBuildReturn(host.Match, other, pawn, CommandId.New(), out _), "no remembered rest: nothing invented");

            TrapFloorTestTable replaced = TrapFloorTestTable.Create(2, 11);
            Assert.IsFalse(policy.TryBuildReturn(replaced.Match, other, pawn, CommandId.New(), out _), "a table put back or replaced forgets");
        }

        [Test]
        public void AnAcceptedRelease_ForgetsTheHold()
        {
            var (host, _, policy, _, other, pawn) = GuestHoldsHostsPawn();
            PhysicalObjectState before = host.Match.GetObject(pawn).PhysicalState;
            PhysicalObjectState dropped = State(1.5f, 0.1f, PhysicalObjectMode.Dynamic, other);
            Assert.IsTrue(Commit(host, other, pawn, dropped).Succeeded, "an allowed release");
            policy.NoteAccepted(host.Match, pawn, before, dropped);
            Assert.IsFalse(policy.TryBuildReturn(host.Match, other, pawn, CommandId.New(), out _), "an allowed move is never undone");
        }

        [Test]
        public void ARefusedHoldOfADie_IsPutBackUnresolved()
        {
            TrapFloorTestTable host = TrapFloorTestTable.Create(2, 11);
            PlayerId other = host.Players[1];
            TabletopObjectId die = host.Template.FloorfallXAxisDieId;
            host.Match.GetObject(die).SetPhysicalState(State(3f, 0.1f, PhysicalObjectMode.SleepingUnresolved, host.Players[0]));
            HeldReturnPolicy policy = new HeldReturnPolicy();
            PhysicalObjectState before = host.Match.GetObject(die).PhysicalState;
            PhysicalObjectState held = State(3f, 0.8f, PhysicalObjectMode.Held, other);
            Assert.IsTrue(Commit(host, other, die, held).Succeeded);
            policy.NoteAccepted(host.Match, die, before, held);
            Assert.IsTrue(policy.TryBuildReturn(host.Match, other, die, CommandId.New(), out CommitPhysicalObjectCommand putBack));
            Assert.AreEqual(PhysicalObjectMode.SleepingUnresolved, putBack.State.Mode, "a die at rest with no new value");
            Assert.IsTrue(Run(host, putBack).Succeeded, "the table accepts it");
        }
    }
}
