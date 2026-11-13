using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A (owner, 2026-11-13: eliminated-pawn sync loop). Pure C#: two identical tables (host, guest) run the same
    /// elimination with the game's own turn service; the record hash is the table check's (TableStateHash); the
    /// host's answer to refused commits is RefusalReconciliation; the resting rule is OutOfPlayRest.
    /// </summary>
    public sealed class EliminatedPawnSyncTests
    {
        private sealed class Side
        {
            public TrapFloorTestTable T;
            public TrapFloorTurnService Turns;
            public TrapFloorAbilityResolutionState Ability;
            public TrapFloorCollapseState Collapse;
            public TrapFloorObjectiveState Objective;

            public static Side Create(int players = 2)
            {
                Side s = new Side { T = TrapFloorTestTable.Create(players, 11) };
                s.Ability = new TrapFloorAbilityResolutionState(s.T.Match.Id);
                s.Collapse = new TrapFloorCollapseState(s.T.Match.Id, s.T.Template.FloorCardIds.Count);
                s.Objective = new TrapFloorObjectiveState(s.T.Match.Id, 1);
                s.Turns = new TrapFloorTurnService(s.T.Template, s.T.Turn, new ControllerInputHandService(), s.T.Feed, s.Ability);
                return s;
            }

            public PlayerId Actor =>
                T.Turn.Phase == TrapFloorTurnPhase.FloorTurn ? T.Turn.FloorOperatorPlayerId : T.Turn.ActivePlayerId;

            public TabletopObjectId PawnOf(PlayerId player) => T.SetupOf(player).PawnId;

            public bool Eliminate(PlayerId player) =>
                Turns.MarkPlayerEliminatedForCurrentRound(T.Match, T.Context(Actor), player).Succeeded;
        }

        private static PhysicalObjectState Moving(PlayerId controller, float x) =>
            new PhysicalObjectState(new PhysicalVector3(x, 0.12f, 0.5f), new PhysicalRotation(0f, 0.3826834f, 0f, 0.9238795f),
                new PhysicalVector3(1.1f, -0.4f, 0.5f), new PhysicalVector3(0f, 8f, 1f), PhysicalObjectMode.Dynamic, controller);

        // Both pawns are moving on both tables (as when a floor tile went from under one of them mid-move).
        private static (Side Host, Side Guest) MovingTables(int players = 2)
        {
            Side host = Side.Create(players), guest = Side.Create(players);
            foreach (Side side in new[] { host, guest })
            {
                for (int i = 0; i < side.T.Players.Length; i++)
                {
                    PlayerId p = side.T.Players[i];
                    side.T.Match.GetObject(side.PawnOf(p)).SetPhysicalState(Moving(p, 0.5f + i));
                }
            }

            Assert.AreEqual(TableStateHash.Of(host.T.Match), TableStateHash.Of(guest.T.Match), "the tables start the same");
            return (host, guest);
        }

        private static Dictionary<TabletopObjectId, ulong> RecordsExcept(MatchState match, TabletopObjectId left)
        {
            Dictionary<TabletopObjectId, ulong> records = new Dictionary<TabletopObjectId, ulong>();
            void Add(TabletopObjectId id, object value)
            {
                if (id == left) return;
                byte[] bytes = TableWire.Write(value, false);
                records[id] = TableWire.Hash(bytes, 0, bytes.Length);
            }

            foreach (var item in match.Cards) Add(item.Key, item.Value);
            foreach (var item in match.Pawns) Add(item.Key, item.Value);
            foreach (var item in match.Dice) Add(item.Key, item.Value);
            foreach (var item in match.Tokens) Add(item.Key, item.Value);
            return records;
        }

        // ---------- the elimination puts the pawn to rest ----------

        [Test]
        public void EliminationWhileMoving_RestsThatPawnInPlace_TheSameOnHostAndGuest()
        {
            var (host, guest) = MovingTables();
            PlayerId outPlayer = host.T.Players[1];
            TabletopObjectId pawn = host.PawnOf(outPlayer);
            PhysicalObjectState before = host.T.Match.GetObject(pawn).PhysicalState;

            Assert.IsTrue(host.Eliminate(outPlayer), "the host eliminates");
            Assert.IsTrue(guest.Eliminate(outPlayer), "the guest replays it");

            PhysicalObjectState rested = host.T.Match.GetObject(pawn).PhysicalState;
            Assert.AreEqual(PhysicalObjectMode.Sleeping, rested.Mode, "no moving state is left to settle and commit");
            Assert.IsTrue(TrapFloorEliminatedPawnRest.IsAtRest(rested), "no speed, no spin");
            Assert.AreEqual(before.Position.X, rested.Position.X, "the accepted position is kept");
            Assert.AreEqual(before.Position.Y, rested.Position.Y);
            Assert.AreEqual(before.Position.Z, rested.Position.Z);
            Assert.AreEqual(before.Rotation.Y, rested.Rotation.Y, "and its turn");
            Assert.AreEqual(before.ControllingPlayerId, rested.ControllingPlayerId);
            Assert.AreEqual(TableStateHash.Of(host.T.Match), TableStateHash.Of(guest.T.Match),
                "host and guest hold the same record: nothing for a whole table to correct");
        }

        [Test]
        public void Elimination_LeavesEveryOtherLoosePiece_AndActivePawns_AsTheyWere()
        {
            var (host, _) = MovingTables(3);
            PlayerId outPlayer = host.T.Players[2];
            TabletopObjectId pawn = host.PawnOf(outPlayer);
            Dictionary<TabletopObjectId, ulong> before = RecordsExcept(host.T.Match, pawn);
            Assert.IsTrue(host.Eliminate(outPlayer));
            Dictionary<TabletopObjectId, ulong> after = RecordsExcept(host.T.Match, pawn);
            CollectionAssert.AreEquivalent(before, after, "cards, dice, tokens and the other pawns are untouched");

            PhysicalObjectState active = host.T.Match.GetObject(host.PawnOf(host.T.Players[0])).PhysicalState;
            Assert.AreEqual(PhysicalObjectMode.Dynamic, active.Mode, "an active pawn keeps its moving state");
            Assert.AreEqual(1.1f, active.Velocity.X, "and its speed");
        }

        [Test]
        public void Elimination_Repeated_ChangesNothing_AndTheTurnDoesNotAdvanceAgain()
        {
            var (host, _) = MovingTables(3);
            PlayerId outPlayer = host.T.Players[2];
            TabletopObjectId pawn = host.PawnOf(outPlayer);
            Assert.IsTrue(host.Eliminate(outPlayer));
            long revision = host.T.Match.Revision;
            long physicalRevision = host.T.Match.GetObject(pawn).PhysicalRevision;
            int round = host.T.Turn.CurrentRound;
            TrapFloorTurnPhase phase = host.T.Turn.Phase;
            PlayerId active = host.T.Turn.ActivePlayerId;
            ulong hash = TableStateHash.Of(host.T.Match);

            Assert.IsFalse(host.Eliminate(outPlayer), "a second elimination of the same player is refused");
            Assert.AreEqual(revision, host.T.Match.Revision, "no new revision");
            Assert.AreEqual(physicalRevision, host.T.Match.GetObject(pawn).PhysicalRevision, "the pawn is not rested again");
            Assert.AreEqual(round, host.T.Turn.CurrentRound);
            Assert.AreEqual(phase, host.T.Turn.Phase);
            Assert.AreEqual(active, host.T.Turn.ActivePlayerId, "no extra turn advance");
            Assert.AreEqual(hash, TableStateHash.Of(host.T.Match));
            Assert.IsFalse(TrapFloorEliminatedPawnRest.Rest(host.T.Match, pawn), "a pawn already at rest is left as it is");
        }

        [Test]
        public void Reconnect_RestoresTheEliminatedPawnAtRest_AndTheSurvivorsTurn()
        {
            var (host, _) = MovingTables();
            PlayerId survivor = host.T.Players[0];
            PlayerId outPlayer = host.T.Players[1];
            Assert.AreEqual(survivor, host.T.Turn.ActivePlayerId, "the survivor has the turn");
            Assert.IsTrue(host.Eliminate(outPlayer));
            Assert.AreEqual(survivor, host.T.Turn.ActivePlayerId, "eliminating the other player keeps the survivor's turn");

            // A reconnect (or any whole-table correction) carries the match and the game state.
            byte[] whole = TableWire.Write(host.T.Match, true);
            MatchState rejoined = (MatchState)TableWire.Read(whole, 0, whole.Length, true);
            PhysicalObjectState there = rejoined.GetObject(host.PawnOf(outPlayer)).PhysicalState;
            Assert.IsTrue(TrapFloorEliminatedPawnRest.IsAtRest(there), "the eliminated pawn arrives at rest");
            Assert.AreEqual(TableStateHash.Of(host.T.Match), TableStateHash.Of(rejoined));

            TrapFloorTurnState restored = TrapFloorSessionStateSnapshot
                .Capture(host.T.Feed, host.Objective, host.Collapse, host.T.Turn, host.Ability).Restore().Turn;
            Assert.AreEqual(survivor, restored.ActivePlayerId, "the survivor's turn survives the table's restore");
            Assert.AreEqual(TrapFloorTurnPhase.PlayerTurn, restored.Phase);
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound, restored.GetPlayerState(outPlayer));
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.Active, restored.GetPlayerState(survivor));
        }

        [Test]
        public void EliminatingTheActivePlayer_PassesTheTurnOnce_ToTheSurvivor()
        {
            var (host, _) = MovingTables();
            PlayerId first = host.T.Turn.ActivePlayerId;
            PlayerId other = host.T.Players[0] == first ? host.T.Players[1] : host.T.Players[0];
            Assert.IsTrue(host.Eliminate(first));
            PlayerId turnHolder = host.T.Turn.Phase == TrapFloorTurnPhase.FloorTurn ? host.T.Turn.FloorOperatorPlayerId : host.T.Turn.ActivePlayerId;
            Assert.AreEqual(other, turnHolder, "the survivor plays (or runs the floor turn) next");
            Assert.IsFalse(host.T.Turn.IsCurrentFloorFailed, "one player is still in");
        }

        // ---------- no refusal / whole-table loop ----------

        private static CommitPhysicalObjectCommand Settle(PlayerId who, AuthoritativeActionRecordMode mode, PhysicalObjectMode state = PhysicalObjectMode.Sleeping) =>
            new CommitPhysicalObjectCommand(
                new CommandContext(CommandId.New(), new MatchId(Guid.NewGuid()), who, 1),
                new TabletopObjectId(Guid.NewGuid()),
                new PhysicalObjectState(new PhysicalVector3(0f, 0.1f, 0f), new PhysicalRotation(0f, 0f, 0f, 1f), default, default, state, who),
                0, null, mode);

        [Test]
        public void RefusedPhysicsOnlySettles_AreNotAnsweredWithAWholeTableEachTime()
        {
            RefusalReconciliation host = new RefusalReconciliation();
            Guid guest = Guid.NewGuid();
            PlayerId player = new PlayerId(guest);
            int tables = 0;
            for (float t = 0f; t < 10f; t += 0.28f) // the live loop: one refused settle every ~0.28 s
            {
                CommitPhysicalObjectCommand settle = Settle(player, AuthoritativeActionRecordMode.None);
                Assert.IsTrue(RefusalReconciliation.IsPhysicsOnly(settle));
                Assert.IsFalse(RefusalReconciliation.NotifiesPlayer(settle), "no refusal notice for a piece settling on its own");
                if (host.SendsTable(guest, settle, t)) tables++;
            }

            Assert.LessOrEqual(tables, 2, "at most one whole table every 5 s (it was one per refusal: 36 here)");
            Assert.Greater(host.SuppressedTables, 30);
        }

        [Test]
        public void RefusedPlayerActions_StillGetTheNoticeAndTheHostsTable()
        {
            RefusalReconciliation host = new RefusalReconciliation();
            Guid guest = Guid.NewGuid();
            PlayerId player = new PlayerId(guest);
            foreach (CommitPhysicalObjectCommand action in new[]
                     {
                         Settle(player, AuthoritativeActionRecordMode.Transaction), // a release / a drop
                         Settle(player, AuthoritativeActionRecordMode.Intermediate), // a throw in flight
                         Settle(player, AuthoritativeActionRecordMode.None, PhysicalObjectMode.Held), // a hold
                     })
            {
                Assert.IsFalse(RefusalReconciliation.IsPhysicsOnly(action));
                Assert.IsTrue(RefusalReconciliation.NotifiesPlayer(action));
                Assert.IsTrue(host.SendsTable(guest, action, 1f), "every refused action is set right at once");
                Assert.IsTrue(host.SendsTable(guest, action, 1.1f));
            }

            Assert.IsTrue(host.SendsTable(guest, new object(), 1.2f), "any other refused command too");
            Assert.AreEqual(0, host.SuppressedTables);
        }

        // ---------- the out-of-play rest (presentation rule) ----------

        [Test]
        public void OutOfPlayRest_OnlyForAResting_EliminatedPawn_NobodyHolds()
        {
            PlayerId p = new PlayerId(Guid.NewGuid());
            PhysicalObjectState resting = new PhysicalObjectState(new PhysicalVector3(0f, 0.1f, 0f), new PhysicalRotation(0f, 0f, 0f, 1f), default, default, PhysicalObjectMode.Sleeping, p);
            PhysicalObjectState moving = Moving(p, 0f);
            Assert.IsTrue(OutOfPlayRest.Wants(true, true, false, false, false, resting), "held still, even over a hole");
            Assert.IsFalse(OutOfPlayRest.Wants(true, false, false, false, false, resting), "an active pawn keeps its physics");
            Assert.IsFalse(OutOfPlayRest.Wants(false, true, false, false, false, resting), "cards and dice keep their physics");
            Assert.IsFalse(OutOfPlayRest.Wants(true, true, true, false, false, resting), "picked up: normal physics");
            Assert.IsFalse(OutOfPlayRest.Wants(true, true, false, true, false, resting), "being moved by a player");
            Assert.IsFalse(OutOfPlayRest.Wants(true, true, false, false, false, moving), "a moving state settles first");
            Assert.IsFalse(OutOfPlayRest.Wants(true, true, false, false, true, resting), "in a container");
        }
    }
}
