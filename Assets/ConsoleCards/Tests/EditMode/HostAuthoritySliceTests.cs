using System;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// AUTH-ET / AUTH-DRAW / AUTH-DUP (2026-11-06): the host's checks for the first authoritative actions. Pure tests on
    /// the seeded test table: who may end a turn, stale and repeated requests, terminal floors, duplicate commands and
    /// Controller draws into another player's hand.
    /// </summary>
    public sealed class HostAuthoritySliceTests
    {
        private static TrapFloorTurnService Turns(TrapFloorTestTable t) =>
            new TrapFloorTurnService(t.Template, t.Turn, new ControllerInputHandService(), t.Feed);

        [Test]
        public void EndTurn_FromTheTurnOwner_IsAcceptedOnce()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            TrapFloorEndTurnAuthority authority = new TrapFloorEndTurnAuthority();
            PlayerId owner = t.Turn.ActivePlayerId;
            TrapFloorEndTurnRequest request = TrapFloorEndTurnRequest.For(t.Turn, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.Accepted, authority.Check(t.Turn, owner, request));
            Assert.AreEqual(TrapFloorEndTurnVerdict.Duplicate, authority.Check(t.Turn, owner, request), "the same request ran twice");
        }

        [Test]
        public void EndTurn_FromAnotherSeat_IsRefused()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            TrapFloorEndTurnAuthority authority = new TrapFloorEndTurnAuthority();
            PlayerId other = t.Turn.PlayerOrder[1];
            // The request even names the real turn owner: the host goes by the connection, not by what the request says.
            TrapFloorEndTurnRequest request = TrapFloorEndTurnRequest.For(t.Turn, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.NotTurnOwner, authority.Check(t.Turn, other, request));
            Assert.AreEqual(TrapFloorEndTurnVerdict.NotSeated, authority.Check(t.Turn, PlayerId.Empty, request));
        }

        [Test]
        public void EndTurn_ForATurnThatAlreadyMovedOn_IsRefused()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            TrapFloorEndTurnAuthority authority = new TrapFloorEndTurnAuthority();
            PlayerId p1 = t.Turn.PlayerOrder[0];
            TrapFloorEndTurnRequest first = TrapFloorEndTurnRequest.For(t.Turn, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.Accepted, authority.Check(t.Turn, p1, first));
            Assert.IsTrue(Turns(t).Advance(t.Match, t.Context(p1)).Succeeded);
            // A second, new request made from the old view of the turn (a double press, a late resend).
            TrapFloorEndTurnRequest repeat = new TrapFloorEndTurnRequest(Guid.NewGuid(), first.Round, first.Phase, first.TurnOwner, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.NotTurnOwner, authority.Check(t.Turn, p1, repeat),
                "the old turn owner ended the next player's turn");
            PlayerId p2 = t.Turn.ActivePlayerId;
            TrapFloorEndTurnRequest stale = new TrapFloorEndTurnRequest(Guid.NewGuid(), first.Round + 1, first.Phase, p2, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.StaleTurn, authority.Check(t.Turn, p2, stale));
        }

        [Test]
        public void EndTurn_AfterTheFloorEnded_IsRefused()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            TrapFloorTurnService turns = Turns(t);
            PlayerId p1 = t.Turn.PlayerOrder[0], p2 = t.Turn.PlayerOrder[1];
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p2).Succeeded);
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p1).Succeeded);
            Assert.IsTrue(t.Turn.IsCurrentFloorFailed);
            TrapFloorEndTurnAuthority authority = new TrapFloorEndTurnAuthority();
            TrapFloorEndTurnRequest request = new TrapFloorEndTurnRequest(Guid.NewGuid(), t.Turn.CurrentRound, t.Turn.Phase, p1, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.FloorEnded, authority.Check(t.Turn, p1, request));

            TrapFloorTestTable won = TrapFloorTestTable.Create(2);
            TrapFloorEndTurnRequest live = TrapFloorEndTurnRequest.For(won.Turn, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.FloorEnded,
                new TrapFloorEndTurnAuthority().Check(won.Turn, won.Turn.ActivePlayerId, live, floorWon: true));
        }

        [Test]
        public void EndTurn_WithoutARequestId_IsRefused()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            TrapFloorEndTurnRequest request = new TrapFloorEndTurnRequest(Guid.Empty, t.Turn.CurrentRound, t.Turn.Phase, t.Turn.ActivePlayerId, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.MissingRequestId,
                new TrapFloorEndTurnAuthority().Check(t.Turn, t.Turn.ActivePlayerId, request));
        }

        [Test]
        public void Commands_RunOnceOnTheHost()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            PlayerId p1 = t.Players[0];
            TrapFloorPlayerSetupDefinition setup = t.SetupOf(p1);
            HostCommandPolicy policy = new HostCommandPolicy();
            DrawCardsCommand draw = new DrawCardsCommand(t.Context(p1), setup.ControllerDeckId, setup.HandContainerId, 2);
            Assert.AreEqual(HostCommandVerdict.Allowed, policy.Check(t.Match, p1, draw, _ => true));
            Assert.AreEqual(HostCommandVerdict.Duplicate, policy.Check(t.Match, p1, draw, _ => true));
        }

        [Test]
        public void ControllerDraw_IntoAnotherPlayersHand_IsRefused()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            PlayerId p1 = t.Players[0], p2 = t.Players[1];
            TrapFloorPlayerSetupDefinition own = t.SetupOf(p1), other = t.SetupOf(p2);
            Func<ContainerId, bool> controller = id => id == own.ControllerDeckId || id == other.ControllerDeckId;
            HostCommandPolicy policy = new HostCommandPolicy();
            DrawCardsCommand steal = new DrawCardsCommand(t.Context(p1), own.ControllerDeckId, other.HandContainerId, 3);
            Assert.AreEqual(HostCommandVerdict.DrawIntoAnotherHand, policy.Check(t.Match, p1, steal, controller));
            DrawCardsCommand mine = new DrawCardsCommand(t.Context(p1), own.ControllerDeckId, own.HandContainerId, 3);
            Assert.AreEqual(HostCommandVerdict.Allowed, policy.Check(t.Match, p1, mine, controller));
        }

        [Test]
        public void DrawFromAnUnguardedPile_KeepsTheTablesFreedom()
        {
            // A Manual deal from an ordinary pile into every hand is not a Controller draw: it stays allowed.
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            PlayerId p1 = t.Players[0], p2 = t.Players[1];
            DrawCardsCommand deal = new DrawCardsCommand(t.Context(p1), t.SetupOf(p1).ControllerDeckId, t.SetupOf(p2).HandContainerId, 1);
            Assert.AreEqual(HostCommandVerdict.Allowed, new HostCommandPolicy().Check(t.Match, p1, deal, _ => false));
        }
    }
}
