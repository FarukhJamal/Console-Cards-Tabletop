using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Privacy;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P7 (s153): the private path's pawn move request (MovePiece) and the End Turn rule refusal. The pawn's move legality
    /// in Full is the game's own host move check (AUTH-MOVE, TrapFloorHostMoveRules, tested elsewhere); these tests pin
    /// the request bytes and the policy's part: only a loose piece, never a card or a die, a known object, a seated sender,
    /// once per request id. In-process (unit) only.
    /// </summary>
    public sealed class PrivateP7Tests
    {
        private static readonly byte[] Key = { 7, 7, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };

        private sealed class Seats : IPrivateSeats
        {
            public TrapFloorTestTable T;
            public bool IsSeated(PlayerId player) => Array.IndexOf(T.Players, player) >= 0;
            public bool IsTurnOf(PlayerId player) => IsSeated(player);
            public ContainerId DeckOf(PlayerId player) => T.SetupOf(player).ControllerDeckId;
            public ContainerId HandOf(PlayerId player) => T.SetupOf(player).HandContainerId;
            public bool IsOwnConsoleSlot(PlayerId player, ContainerId slot) => false;
            public IReadOnlyList<PlayerId> Seated => T.Players;
            public int DrawLimit(PlayerId player) => -1;
        }

        private static PrivateOperation Move(TabletopObjectId piece) => new PrivateOperation
        {
            Kind = PrivateOperationKind.MovePiece,
            RequestId = Guid.NewGuid(),
            Card = piece,
            HasPose = true,
            Pose = new TabletopPose(new TableCoordinate(1.25d, -0.5d), 90f, 0, 0),
        };

        [Test]
        public void MovePiece_Codec_RoundTrips_AndNeedsAPlace()
        {
            PrivateOperation op = Move(TabletopObjectId.New());
            byte[] wire = PrivateOperationCodec.Write(op);
            Assert.IsTrue(PrivateOperationCodec.IsOperation(wire, 0));
            Assert.IsTrue(PrivateOperationCodec.TryRead(wire, 0, out PrivateOperation read));
            Assert.AreEqual(PrivateOperationKind.MovePiece, read.Kind);
            Assert.AreEqual(op.Card, read.Card);
            Assert.AreEqual(op.Pose.Position, read.Pose.Position);

            op.HasPose = false;
            Assert.IsFalse(PrivateOperationCodec.TryRead(PrivateOperationCodec.Write(op), 0, out _), "a move names where");
            op.HasPose = true;
            op.Destination = new ContainerId(Guid.NewGuid());
            Assert.IsFalse(PrivateOperationCodec.TryRead(PrivateOperationCodec.Write(op), 0, out _), "a piece move names no container");
        }

        [Test]
        public void MovePiece_Policy_OnlyLoosePieces_NotCardsOrDice_KnownAndSeated_Once()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, 0x7A7A);
            Seats seats = new Seats { T = t };
            PrivateTableHost host = new PrivateTableHost(t.Match, seats, t.Template.CardLabels, Key);
            PlayerId mover = t.Players[0];
            TabletopObjectId pawn = t.SetupOf(mover).PawnId;
            Assert.IsTrue(t.Match.Pawns.ContainsKey(pawn), "the seat's pawn is a pawn of the table");

            PrivateOperation ok = Move(pawn);
            Assert.AreEqual(PrivateRequestVerdict.Accepted, PrivateOperationPolicy.Check(t.Match, seats, host, mover, ok, out PrivateResolvedOperation resolved));
            Assert.AreEqual(pawn, resolved.RealCard);
            Assert.AreEqual(PrivateRequestVerdict.Duplicate, PrivateOperationPolicy.Check(t.Match, seats, host, mover, ok, out _), "one request id, one move");

            TabletopObjectId someCard = default;
            foreach (TabletopObjectId id in t.Match.Cards.Keys) { someCard = id; break; }
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateOperationPolicy.Check(t.Match, seats, host, mover, Move(someCard), out _), "a card moves by Transfer, never as a piece");
            foreach (TabletopObjectId die in t.Match.Dice.Keys)
            {
                Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateOperationPolicy.Check(t.Match, seats, host, mover, Move(die), out _), "a die is thrown, not placed");
                break;
            }

            Assert.AreEqual(PrivateRequestVerdict.UnknownReference, PrivateOperationPolicy.Check(t.Match, seats, host, mover, Move(TabletopObjectId.New()), out _));
            PlayerId stranger = new PlayerId(Guid.NewGuid());
            Assert.AreEqual(PrivateRequestVerdict.NotSeated, PrivateOperationPolicy.Check(t.Match, seats, host, stranger, Move(pawn), out _));
        }

        [Test]
        public void ThrowDie_OnlyDice_AFaceOfThatDie_AnySeatedPlayer_Once()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, 0x7B7B);
            Seats seats = new Seats { T = t };
            PrivateTableHost host = new PrivateTableHost(t.Match, seats, t.Template.CardLabels, Key);
            PlayerId thrower = t.Players[1];
            TabletopObjectId die = default;
            int sides = 0;
            foreach (var pair in t.Match.Dice) { die = pair.Key; sides = pair.Value.SideCount; break; }
            Assert.IsFalse(die.IsEmpty, "the Trap Floor table has dice");

            PrivateOperation Throw(TabletopObjectId id, int face) => new PrivateOperation
            {
                Kind = PrivateOperationKind.ThrowDie, RequestId = Guid.NewGuid(), Card = id, HasPose = true, A = face,
                Pose = new TabletopPose(new TableCoordinate(0.5d, 0.5d), 0f, 0, 0),
            };

            PrivateOperation ok = Throw(die, 1);
            Assert.IsTrue(PrivateOperationCodec.TryRead(PrivateOperationCodec.Write(ok), 0, out PrivateOperation read));
            Assert.AreEqual(PrivateOperationKind.ThrowDie, read.Kind);
            Assert.AreEqual(1, read.A);
            Assert.AreEqual(PrivateRequestVerdict.Accepted, PrivateOperationPolicy.Check(t.Match, seats, host, thrower, read, out _));
            Assert.AreEqual(PrivateRequestVerdict.Duplicate, PrivateOperationPolicy.Check(t.Match, seats, host, thrower, read, out _), "one throw, one result");
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateOperationPolicy.Check(t.Match, seats, host, thrower, Throw(die, sides + 1), out _), "not a face of that die");
            Assert.IsFalse(PrivateOperationCodec.TryRead(PrivateOperationCodec.Write(Throw(die, 0)), 0, out _), "no face 0");
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateOperationPolicy.Check(t.Match, seats, host, thrower, Throw(t.SetupOf(thrower).PawnId, 1), out _), "a pawn is not thrown as a die");
            Assert.AreEqual(PrivateRequestVerdict.NotSeated, PrivateOperationPolicy.Check(t.Match, seats, host, new PlayerId(Guid.NewGuid()), Throw(die, 1), out _));
        }

        [Test]
        public void EndTurnVerdict_RuleRefused_IsItsOwnAnswer()
        {
            // The host's End Turn answer is one byte: the new rule refusal must not read as any earlier answer.
            Assert.AreEqual(8, (int)TrapFloorEndTurnVerdict.RuleRefused);
            Assert.AreNotEqual(TrapFloorEndTurnVerdict.Accepted, TrapFloorEndTurnVerdict.RuleRefused);
        }
    }
}
