using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// 2026-11-05 deterministic elimination tests on the authoritative turn service (the one every computer replays).
    /// Only behaviour the code already defines: who is skipped, what a full knock-out does, that each event happens
    /// once. Winners and what losing a floor leads to (R3, R4, R6) are design questions and are not tested here.
    /// </summary>
    public sealed class EliminationTurnTests
    {
        private static TrapFloorTurnService Turns(TrapFloorTestTable t) =>
            new TrapFloorTurnService(t.Template, t.Turn, new ControllerInputHandService(), t.Feed);

        private static int Count(TrapFloorTestTable t, TrapFloorActivityKind kind)
        {
            int n = 0;
            IReadOnlyList<TrapFloorActivityEntry> entries = t.Feed.Entries;
            for (int i = 0; i < entries.Count; i++) if (entries[i].Kind == kind) n++;
            return n;
        }

        [Test]
        public void OnePlayerOut_IsSkipped_TheOthersPlayOn()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3);
            TrapFloorTurnService turns = Turns(t);
            PlayerId p1 = t.Turn.PlayerOrder[0], p2 = t.Turn.PlayerOrder[1], p3 = t.Turn.PlayerOrder[2];
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p2).Succeeded);
            Assert.AreEqual(p1, t.Turn.ActivePlayerId, "knocking out someone else must not end the turn");
            Assert.IsTrue(turns.Advance(t.Match, t.Context(p1)).Succeeded);
            Assert.AreEqual(p3, t.Turn.ActivePlayerId, "the player who is out was not skipped");
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound, t.Turn.GetPlayerState(p2));
            Assert.IsFalse(t.Turn.IsCurrentFloorFailed);
        }

        [Test]
        public void TheActivePlayerOut_PassesTheTurnAtOnce()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3);
            TrapFloorTurnService turns = Turns(t);
            PlayerId p1 = t.Turn.PlayerOrder[0], p2 = t.Turn.PlayerOrder[1];
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p1).Succeeded);
            Assert.AreEqual(p2, t.Turn.ActivePlayerId);
            Assert.AreEqual(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer, turns.Advance(t.Match, t.Context(p1)).Error,
                "a player who is out ended someone else's turn");
        }

        [Test]
        public void SeveralOut_TheRoundSkipsThem_AndTheyStayOutNextRound()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(4);
            TrapFloorTurnService turns = Turns(t);
            IReadOnlyList<PlayerId> order = t.Turn.PlayerOrder;
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(order[0]), order[1]).Succeeded);
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(order[0]), order[2]).Succeeded);
            Assert.IsTrue(turns.Advance(t.Match, t.Context(order[0])).Succeeded);
            Assert.AreEqual(order[3], t.Turn.ActivePlayerId);
            Assert.IsTrue(turns.Advance(t.Match, t.Context(order[3])).Succeeded);
            Assert.AreEqual(TrapFloorTurnPhase.FloorTurn, t.Turn.Phase);
            Assert.IsTrue(turns.Advance(t.Match, t.Context(t.Turn.FloorOperatorPlayerId)).Succeeded);
            Assert.AreEqual(2, t.Turn.CurrentRound);
            Assert.AreEqual(order[0], t.Turn.ActivePlayerId);
            Assert.IsTrue(turns.Advance(t.Match, t.Context(order[0])).Succeeded);
            Assert.AreEqual(order[3], t.Turn.ActivePlayerId, "a knocked-out player came back before the floor ended");
        }

        [Test]
        public void TheLastPlayerOut_FailsTheFloorOnce()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            TrapFloorTurnService turns = Turns(t);
            PlayerId p1 = t.Turn.PlayerOrder[0], p2 = t.Turn.PlayerOrder[1];
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p2).Succeeded);
            Assert.IsFalse(t.Turn.IsCurrentFloorFailed);
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p1).Succeeded);
            Assert.IsTrue(t.Turn.IsCurrentFloorFailed);
            Assert.AreEqual(TrapFloorTurnPhase.FloorTurn, t.Turn.Phase);
            Assert.AreEqual(1, Count(t, TrapFloorActivityKind.AllPlayersEliminated));
            Assert.AreEqual(2, Count(t, TrapFloorActivityKind.PlayerEliminated));
        }

        [Test]
        public void KnockingOutTheSamePlayerTwice_IsRefused_AndRecordedOnce()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3);
            TrapFloorTurnService turns = Turns(t);
            PlayerId p1 = t.Turn.PlayerOrder[0], p2 = t.Turn.PlayerOrder[1];
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p2).Succeeded);
            long revision = t.Match.Revision;
            Assert.AreEqual(TrapFloorTurnAdvanceError.PlayerAlreadyEliminatedForCurrentRound,
                turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p2).Error);
            Assert.AreEqual(revision, t.Match.Revision);
            Assert.AreEqual(1, Count(t, TrapFloorActivityKind.PlayerEliminated));
        }

        [Test]
        public void AfterTheFloorFails_NoPlayerTurnActionIsAccepted()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            TrapFloorTurnService turns = Turns(t);
            PlayerId p1 = t.Turn.PlayerOrder[0], p2 = t.Turn.PlayerOrder[1];
            turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), p1);
            turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p2), p2);
            Assert.IsTrue(t.Turn.IsCurrentFloorFailed);
            Assert.AreEqual(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer, turns.Skip(t.Match, t.Context(p1)).Error,
                "a player turn was skipped on a failed floor");
            Assert.AreEqual(TrapFloorTurnAdvanceError.PlayerIsNotParticipant,
                turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p1), PlayerId.New()).Error);
        }

        [Test]
        public void SomeoneOutsideTheGame_CannotEndATurn()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            Assert.AreEqual(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer, Turns(t).Advance(t.Match, t.Context(PlayerId.New())).Error);
        }

        [Test]
        public void TwoComputersReplayingTheKnockOuts_AgreeOnTheTurn()
        {
            TrapFloorTestTable host = TrapFloorTestTable.Create(3);
            TrapFloorTestTable guest = TrapFloorTestTable.Create(3, game: host.Game);
            foreach (TrapFloorTestTable t in new[] { host, guest })
            {
                TrapFloorTurnService turns = Turns(t);
                IReadOnlyList<PlayerId> order = t.Turn.PlayerOrder;
                turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(order[0]), order[0]);
                turns.Advance(t.Match, t.Context(order[1]));
                turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(order[2]), order[2]);
            }

            Assert.AreEqual(host.Turn.Phase, guest.Turn.Phase);
            Assert.AreEqual(host.Turn.ActivePlayerId, guest.Turn.ActivePlayerId);
            Assert.AreEqual(host.Turn.CurrentRound, guest.Turn.CurrentRound);
            Assert.AreEqual(host.Turn.IsCurrentFloorFailed, guest.Turn.IsCurrentFloorFailed);
            Assert.AreEqual(host.Match.Revision, guest.Match.Revision);
        }
    }
}
