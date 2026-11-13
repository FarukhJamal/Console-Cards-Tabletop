using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A (owner QA, 2026-11-13: hand cards glitched while a refused pawn drag was set right). Pure C#: the guest's
    /// table took its own drag (hold, release, checkpoints) that the host refused; the host's record of that one piece
    /// sets it right over the wire, and the two tables' record hashes (the table check's) agree again, without a whole
    /// table. Nothing else on the guest's table changes.
    /// </summary>
    public sealed class PieceCorrectionTests
    {
        private static readonly PhysicalRotation Upright = new PhysicalRotation(0f, 0f, 0f, 1f);

        private static PhysicalObjectState State(float x, float y, PhysicalObjectMode mode, PlayerId controller) =>
            new PhysicalObjectState(new PhysicalVector3(x, y, 0.5f), Upright, default, default, mode, controller);

        private static void Commit(TrapFloorTestTable t, PlayerId who, TabletopObjectId id, PhysicalObjectState state) =>
            Assert.IsTrue(new CommitPhysicalObjectUseCase().Execute(t.Match, t.Players, new CommitPhysicalObjectCommand(
                new CommandContext(CommandId.New(), t.Match.Id, who, t.Match.Revision), id, state, t.Match.GetObject(id).PhysicalRevision)).Succeeded);

        private static Dictionary<TabletopObjectId, ulong> Records(MatchState match, TabletopObjectId except)
        {
            Dictionary<TabletopObjectId, ulong> all = new Dictionary<TabletopObjectId, ulong>();
            void Add(TabletopObjectId id, object value)
            {
                if (id == except) return;
                byte[] bytes = TableWire.Write(value, false);
                all[id] = TableWire.Hash(bytes, 0, bytes.Length);
            }

            foreach (var item in match.Cards) Add(item.Key, item.Value);
            foreach (var item in match.Pawns) Add(item.Key, item.Value);
            foreach (var item in match.Dice) Add(item.Key, item.Value);
            return all;
        }

        [Test]
        public void ARefusedDrag_IsSetRightWithThatPieceAlone_AndTheTablesAgreeAgain()
        {
            TrapFloorTestTable host = TrapFloorTestTable.Create(2, 11), guest = TrapFloorTestTable.Create(2, 11);
            PlayerId owner = host.Players[0], dragger = host.Players[1];
            TabletopObjectId pawn = host.SetupOf(owner).PawnId;
            foreach (TrapFloorTestTable t in new[] { host, guest })
                t.Match.GetObject(pawn).SetPhysicalState(State(0.5f, 0.1f, PhysicalObjectMode.Sleeping, owner));
            Assert.AreEqual(TableStateHash.Of(host.Match), TableStateHash.Of(guest.Match));

            // The guest's own table takes its whole drag; the host refused all of it (its record did not change).
            Commit(guest, dragger, pawn, State(0.5f, 0.9f, PhysicalObjectMode.Held, dragger));
            Commit(guest, dragger, pawn, State(-4f, 0.6f, PhysicalObjectMode.Dynamic, dragger));
            Commit(guest, dragger, pawn, State(-4.2f, 0.1f, PhysicalObjectMode.Dynamic, dragger));
            Assert.AreNotEqual(TableStateHash.Of(host.Match), TableStateHash.Of(guest.Match), "the guest's table differs");
            Dictionary<TabletopObjectId, ulong> othersBefore = Records(guest.Match, pawn);

            Assert.IsTrue(PieceCorrection.TryCapture(host.Match, pawn, out PieceCorrection correction));
            byte[] wire = TableWire.Write(correction, false);
            PieceCorrection received = (PieceCorrection)TableWire.Read(wire, 0, wire.Length, false);
            Assert.IsTrue(received.ApplyTo(guest.Match), "the guest takes the host's record of the piece");

            Assert.AreEqual(TableStateHash.Of(host.Match), TableStateHash.Of(guest.Match),
                "the same records again: the table check asks for no whole table");
            PhysicalObjectState there = guest.Match.GetObject(pawn).PhysicalState;
            Assert.AreEqual(PhysicalObjectMode.Sleeping, there.Mode, "back at rest where the host has it");
            Assert.AreEqual(0.5f, there.Position.X);
            Assert.AreEqual(host.Match.GetObject(pawn).PhysicalRevision, guest.Match.GetObject(pawn).PhysicalRevision);
            CollectionAssert.AreEquivalent(othersBefore, Records(guest.Match, pawn), "every other card, pawn and die is untouched");

            Assert.IsTrue(received.ApplyTo(guest.Match), "taking it again changes nothing");
            Assert.AreEqual(TableStateHash.Of(host.Match), TableStateHash.Of(guest.Match));
        }

        [Test]
        public void APieceInAContainer_IsNotCorrectedThisWay()
        {
            TrapFloorTestTable host = TrapFloorTestTable.Create(2, 11);
            TabletopObjectId contained = TabletopObjectId.Empty;
            foreach (var card in host.Match.Cards)
            {
                if (!card.Value.BaseState.ContainerId.IsEmpty) { contained = card.Key; break; }
            }

            Assert.IsFalse(contained.IsEmpty);
            Assert.IsFalse(PieceCorrection.TryCapture(host.Match, contained, out _), "a card in a pile or Hand: the whole table sets it right");
            Assert.IsFalse(PieceCorrection.TryCapture(host.Match, new TabletopObjectId(Guid.NewGuid()), out _));
        }
    }
}
