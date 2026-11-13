using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A (owner, 2026-11-13: "a guest who has a Key lands on the Grand Exit and escapes, but the turn never passes to the
    /// next player"). Two identical tables (host, guest) run the guest's escape with the game's own services, in the
    /// order the guest's computer now sends it: the escape, the elimination that passes the turn, then the pawn set
    /// beside the board. The host's move check (TrapFloorHostMoveRules) is the one it runs on the guest's commit. Pure.
    /// </summary>
    public sealed class EscapeTurnPassTests
    {
        private sealed class Evidence : ITrapFloorEffectEvidence
        {
            public TrapFloorObjectiveState Objective;
            public bool IsSurvivalEndgame => false;
            public bool IsEscaped(PlayerId player) => Objective.IsPlayerEscaped(player); // read from the table, as HostEvidence does
            public bool StandsOnHoleOrOffFloor(PlayerId player) => false;
            public bool IsStuckWithoutKey(PlayerId player) => false;
            public bool CannotEscape(PlayerId player) => false;
            public bool StandsOnFloorCard(PlayerId player, TabletopObjectId floorCardId) => false;
        }

        private sealed class Side
        {
            public TrapFloorTestTable T;
            public TrapFloorObjectiveState Objective;
            public TrapFloorTurnService Turns;
            public TrapFloorEffectExecutor Exec;

            public static Side Create()
            {
                Side s = new Side { T = TrapFloorTestTable.Create(2, 11) };
                TrapFloorAbilityResolutionState ability = new TrapFloorAbilityResolutionState(s.T.Match.Id);
                TrapFloorCollapseState collapse = new TrapFloorCollapseState(s.T.Match.Id, s.T.Template.FloorCardIds.Count);
                s.Objective = new TrapFloorObjectiveState(s.T.Match.Id, 1);
                s.Turns = new TrapFloorTurnService(s.T.Template, s.T.Turn, new ControllerInputHandService(), s.T.Feed, ability);
                TrapFloorAbilityResolutionService service =
                    new TrapFloorAbilityResolutionService(ability, s.T.Turn, s.T.Feed, s.T.Template, collapse);
                s.Exec = new TrapFloorEffectExecutor(service, ability, s.Turns, s.T.Turn, s.T.Template, new Evidence { Objective = s.Objective }, id => null);
                return s;
            }

            public PlayerId TurnHolder =>
                T.Turn.Phase == TrapFloorTurnPhase.FloorTurn ? T.Turn.FloorOperatorPlayerId : T.Turn.ActivePlayerId;

            // What the escape command records on a table (TrapFloorObjectiveUseCase.AttemptEscape, winsFloor false).
            public void Escape(PlayerId player) => Objective.RecordEscape(TabletopObjectId.Empty, player, 2, winsFloor: false, countOnly: true);

            public bool EliminateEscaped(PlayerId player) =>
                Exec.Eliminate(T.Match, new TrapFloorEliminatePlayerCommand(T.Context(player), player, TrapFloorEliminationReason.Escaped)).Succeeded;
        }

        private static (Side Host, Side Guest, PlayerId HostPlayer, PlayerId GuestPlayer) GuestsTurn()
        {
            Side host = Side.Create(), guest = Side.Create();
            PlayerId hostPlayer = host.T.Players[0], guestPlayer = host.T.Players[1];
            foreach (Side side in new[] { host, guest })
            {
                Assert.AreEqual(hostPlayer, side.T.Turn.ActivePlayerId, "the host's player starts");
                Assert.IsTrue(side.Turns.Advance(side.T.Match, side.T.Context(hostPlayer)).Succeeded, "the host's player ends their turn");
                Assert.AreEqual(guestPlayer, side.T.Turn.ActivePlayerId, "now it is the guest's turn");
            }

            return (host, guest, hostPlayer, guestPlayer);
        }

        [Test]
        public void GuestEscapes_TheTurnPassesOnce_TheSameOnHostAndGuest()
        {
            var (host, guest, hostPlayer, guestPlayer) = GuestsTurn();
            int round = host.T.Turn.CurrentRound;

            foreach (Side side in new[] { guest, host }) // the guest runs it, the host replays it
            {
                side.Escape(guestPlayer);
                Assert.IsTrue(side.EliminateEscaped(guestPlayer), "an escaped player is out: the elimination has its reason on this table");
                Assert.AreEqual(TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound, side.T.Turn.GetPlayerState(guestPlayer));
                Assert.AreNotEqual(guestPlayer, side.TurnHolder, "the turn left the escaped guest");
                Assert.AreEqual(hostPlayer, side.TurnHolder, "the host's player (the only one still in) plays or runs the floor turn next");
                Assert.IsFalse(side.T.Turn.IsCurrentFloorFailed, "one player is still in: the floor goes on");
            }

            Assert.AreEqual(host.T.Turn.Phase, guest.T.Turn.Phase, "host and guest agree on the phase");
            Assert.AreEqual(host.T.Turn.CurrentRound, guest.T.Turn.CurrentRound);
            Assert.AreEqual(host.TurnHolder, guest.TurnHolder, "and on whose turn it is");
            Assert.AreEqual(TableStateHash.Of(host.T.Match), TableStateHash.Of(guest.T.Match), "the same table record");

            // Once: the same elimination again (a repeat, a replay) is refused and passes nothing.
            PlayerId holder = host.TurnHolder;
            TrapFloorTurnPhase phase = host.T.Turn.Phase;
            Assert.IsFalse(host.EliminateEscaped(guestPlayer), "a second elimination of the escaped player is refused");
            Assert.AreEqual(holder, host.TurnHolder, "no second turn pass");
            Assert.AreEqual(phase, host.T.Turn.Phase);
            Assert.GreaterOrEqual(host.T.Turn.CurrentRound, round);
        }

        [Test]
        public void EscapedPlayersPawn_SetBesideTheBoardByItsOwner_IsAccepted_ByTheHost()
        {
            TrapFloorHostMoveFacts SendAway(PlayerId owner, PlayerId sender, bool escaped, TrapFloorCoordinate? target) =>
                new TrapFloorHostMoveFacts
                {
                    FullRules = true,
                    Sender = sender,
                    PawnOwner = owner,
                    PawnOwnerOut = true, // escaped (and, now first, eliminated)
                    PawnOwnerEscaped = escaped,
                    IsPlayerTurn = true,
                    ActivePlayer = PlayerId.Empty,
                    Current = new TrapFloorCoordinate(3, 3), // the exit
                    TurnStart = new TrapFloorCoordinate(3, 3),
                    Target = target,
                    Columns = 6,
                    Rows = 6,
                    Walkable = (x, y) => true,
                    LitTiles = new List<TrapFloorCoordinate>(),
                };

            var (_, _, hostPlayer, guestPlayer) = GuestsTurn();
            Assert.AreEqual(TrapFloorHostMoveVerdict.Allowed,
                TrapFloorHostMoveRules.Check(SendAway(guestPlayer, guestPlayer, true, null)),
                "the escaped guest's own computer sets the pawn beside the board (off the floor): accepted, not refused as PlayerIsOut");
            Assert.AreEqual(TrapFloorHostMoveVerdict.PlayerIsOut,
                TrapFloorHostMoveRules.Check(SendAway(guestPlayer, guestPlayer, true, new TrapFloorCoordinate(2, 3))),
                "but it never comes back onto the floor this floor");
            Assert.AreEqual(TrapFloorHostMoveVerdict.PlayerIsOut,
                TrapFloorHostMoveRules.Check(SendAway(guestPlayer, guestPlayer, false, null)),
                "a player knocked out (not escaped) moves nothing");
            Assert.AreEqual(TrapFloorHostMoveVerdict.NotYourPawn,
                TrapFloorHostMoveRules.Check(SendAway(guestPlayer, hostPlayer, true, null)),
                "and nobody else moves the escaped player's pawn");
        }
    }
}
