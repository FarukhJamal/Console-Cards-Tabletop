using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// AUTH-PUSH (2026-11-06): a joined computer's pushed table cannot change pieces or piles; the host keeps its own.
    /// The pushed table is made the way the network makes it (a snapshot restored on the other side).
    /// </summary>
    public sealed class TablePushGuardTests
    {
        private static MatchState Copy(MatchState match) =>
            GameTemplateInitialSnapshot.Capture(match).Restore(match.Revision);

        [Test]
        public void AnUnchangedTable_HasNoPieceChanges()
        {
            TrapFloorTestTable table = TrapFloorTestTable.Create(3);
            Assert.AreEqual(0, TablePushGuard.CountPieceChanges(Copy(table.Match), Copy(table.Match)));
        }

        [Test]
        public void AMovedPawn_IsCounted()
        {
            TrapFloorTestTable table = TrapFloorTestTable.Create(2);
            MatchState pushed = Copy(table.Match);
            foreach (PawnState pawn in pushed.Pawns.Values)
            {
                TabletopPose pose = pawn.BaseState.Pose;
                pawn.BaseState.SetPose(new TabletopPose(
                    new TableCoordinate(pose.Position.X + 3d, pose.Position.Y), pose.RotationDegrees, pose.Layer, pose.LocalOrder));
                break;
            }

            List<string> notes = new List<string>();
            Assert.AreEqual(1, TablePushGuard.CountPieceChanges(Copy(table.Match), pushed, notes));
            StringAssert.Contains("pawn", notes[0]);
        }

        [Test]
        public void AReorderedDeck_AndAFlippedCard_AreCounted()
        {
            TrapFloorTestTable table = TrapFloorTestTable.Create(2);
            MatchState pushed = Copy(table.Match);
            ContainerState deck = null;
            foreach (ContainerState container in pushed.Containers.Values)
            {
                if (container.Kind == ContainerKind.Deck && container.Count >= 2) { deck = container; break; }
            }

            Assert.IsNotNull(deck, "the test table has a deck");
            List<TabletopObjectId> order = new List<TabletopObjectId>(deck.ObjectIds);
            order.Reverse();
            deck.ReplaceOrder(order);
            Assert.AreEqual(1, TablePushGuard.CountPieceChanges(Copy(table.Match), pushed), "deck order");

            foreach (CardInstanceState card in pushed.Cards.Values)
            {
                if (!card.BaseState.ContainerId.IsEmpty) continue;
                card.SetFace(card.Face == CardFace.FaceUp ? CardFace.FaceDown : CardFace.FaceUp);
                break;
            }

            Assert.AreEqual(2, TablePushGuard.CountPieceChanges(Copy(table.Match), pushed), "deck order + a flip");
        }
    }
}
