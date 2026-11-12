using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A batch 4 (owner, 2026-11-12: Crane authority and the Trap lifecycle; CR-1..CR-4, RM-1). On the game's real rules
    /// services and seeded test tables (no presentation, no network transport except the table's own wire format):
    /// a Spring/Crane leaves its pawn move owed in the rules state (so a host that did not resolve it sees it, and a
    /// rebuilt or reconnected table keeps it) until the pawn reaches the tile; a Crane roll belongs to one occurrence; a
    /// Reminders skip is final; Sticky lasts exactly the next round and never stops End Turn.
    /// </summary>
    public sealed class TrapLifecycleTests
    {
        private sealed class Evidence : ITrapFloorEffectEvidence
        {
            public readonly Dictionary<PlayerId, TabletopObjectId> Standing = new Dictionary<PlayerId, TabletopObjectId>();
            public bool IsSurvivalEndgame { get; set; }
            public bool IsEscaped(PlayerId player) => false;
            public bool StandsOnHoleOrOffFloor(PlayerId player) => false;
            public bool IsStuckWithoutKey(PlayerId player) => false;
            public bool CannotEscape(PlayerId player) => false;
            public bool StandsOnFloorCard(PlayerId player, TabletopObjectId floorCardId) =>
                Standing.TryGetValue(player, out TabletopObjectId on) && on == floorCardId;
        }

        // One computer's table with the game's own services (a host and a guest are two of these from one seed).
        private sealed class Table
        {
            public TrapFloorTestTable T;
            public TrapFloorAbilityResolutionState Ability;
            public TrapFloorAbilityResolutionService Service;
            public TrapFloorCollapseState Collapse;
            public TrapFloorObjectiveState Objective;
            public TrapFloorTurnService Turns;
            public TrapFloorRevealFloorUseCase Reveal;
            public TrapFloorEffectExecutor Exec;
            public readonly Evidence Ev = new Evidence();

            public PlayerId Active => T.Turn.ActivePlayerId;

            public static Table Create(int seed)
            {
                Table t = new Table { T = TrapFloorTestTable.Create(3, seed) };
                t.Ability = new TrapFloorAbilityResolutionState(t.T.Match.Id);
                t.Collapse = new TrapFloorCollapseState(t.T.Match.Id, t.T.Template.FloorCardIds.Count);
                t.Objective = new TrapFloorObjectiveState(t.T.Match.Id, 1);
                t.Service = new TrapFloorAbilityResolutionService(t.Ability, t.T.Turn, t.T.Feed, t.T.Template, t.Collapse);
                t.Turns = new TrapFloorTurnService(t.T.Template, t.T.Turn, new ControllerInputHandService(), t.T.Feed, t.Ability);
                t.Reveal = new TrapFloorRevealFloorUseCase(t.T.Template, t.T.Feed, t.Collapse, t.Ability, t.T.Turn);
                t.Exec = new TrapFloorEffectExecutor(t.Service, t.Ability, t.Turns, t.T.Turn, t.T.Template, t.Ev, id => null);
                return t;
            }

            public TabletopObjectId FaceDown(Func<TrapFloorFloorContentDefinition, bool> match, TabletopObjectId except = default)
            {
                foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in T.Template.FloorCardIds)
                {
                    if (pair.Value == except) continue;
                    if (T.Template.TryGetFloorCardState(T.Match, pair.Value, out TrapFloorFloorCardState floor)
                        && !floor.IsRevealed && match(floor.Content))
                        return pair.Value;
                }

                return TabletopObjectId.Empty;
            }

            public TrapFloorCoordinate At(TabletopObjectId tile)
            {
                Assert.IsTrue(T.Template.TryGetFloorCardState(T.Match, tile, out TrapFloorFloorCardState floor));
                return floor.Coordinate;
            }

            public TrapFloorRevealFloorResult Land(PlayerId who, TabletopObjectId tile)
            {
                TrapFloorRevealFloorResult result = Reveal.Execute(T.Match, new TrapFloorRevealFloorCommand(T.Context(who), tile));
                Ev.Standing[who] = tile;
                return result;
            }

            // The two floor dice lie still showing (column, row), as after a settled roll.
            public void Dice(int column, int row)
            {
                DieState x = T.Match.Dice[T.Template.FloorfallXAxisDieId];
                DieState y = T.Match.Dice[T.Template.FloorfallYAxisDieId];
                if ((x.BaseState.PhysicalState != null && x.BaseState.PhysicalState.Mode != PhysicalObjectMode.Sleeping)
                    || (y.BaseState.PhysicalState != null && y.BaseState.PhysicalState.Mode != PhysicalObjectMode.Sleeping))
                    Assert.Inconclusive("the test table's floor dice are not at rest");
                x.SetAcceptedRoll(new DieRoll(x.SideCount, column));
                y.SetAcceptedRoll(new DieRoll(y.SideCount, row));
            }

            public TrapFloorLaunchResult Launch(PlayerId who, TabletopObjectId trap) =>
                Exec.LaunchTrap(T.Match, new TrapFloorLaunchTrapCommand(T.Context(who), trap));

            public TrapFloorTrapResolutionRecord Record(TabletopObjectId tile)
            {
                Assert.IsTrue(Ability.TryGetTrap(tile, out TrapFloorTrapResolutionRecord record), "a Trap record for that tile");
                return record;
            }

            public TrapFloorSessionStateSnapshot Snap() =>
                TrapFloorSessionStateSnapshot.Capture(T.Feed, Objective, Collapse, T.Turn, Ability);

            // Ends turns (and floor turns) until the given round's turn of that player begins. False if never.
            public bool AdvanceUntil(int round, PlayerId player)
            {
                for (int i = 0; i < 60; i++)
                {
                    if (T.Turn.Phase == TrapFloorTurnPhase.PlayerTurn && T.Turn.CurrentRound == round && T.Turn.ActivePlayerId == player)
                        return true;
                    PlayerId actor = T.Turn.Phase == TrapFloorTurnPhase.FloorTurn ? T.Turn.FloorOperatorPlayerId : T.Turn.ActivePlayerId;
                    Assert.IsTrue(Turns.Advance(T.Match, T.Context(actor)).Succeeded, $"End Turn in round {T.Turn.CurrentRound}");
                }

                return false;
            }
        }

        private static bool Crane(TrapFloorFloorContentDefinition c) =>
            c.Category == TrapFloorFloorContentCategory.Trap && c.TrapEffect == TrapFloorTrapEffectCategory.CraneDrop;

        private static bool Fall(TrapFloorFloorContentDefinition c) =>
            c.Category == TrapFloorFloorContentCategory.Trap && c.TrapEffect == TrapFloorTrapEffectCategory.EliminateForCurrentRound
            && c.HasSupportedAssistedTrapEffect;

        private static bool Sticky(TrapFloorFloorContentDefinition c) =>
            c.Category == TrapFloorFloorContentCategory.Trap && c.TrapEffect == TrapFloorTrapEffectCategory.StickyNextRound
            && c.HasSupportedAssistedTrapEffect;

        private static bool Ordinary(TrapFloorFloorContentDefinition c) => c.Category != TrapFloorFloorContentCategory.Trap;

        private static Table Dealing(Func<Table, bool> wanted, int from = 11)
        {
            for (int seed = from; seed < 260; seed++)
            {
                Table t = Table.Create(seed);
                if (wanted(t)) return t;
            }

            Assert.Inconclusive("no seed deals the tiles this test needs");
            return null;
        }

        private static int Seed(Func<Table, bool> wanted)
        {
            for (int seed = 11; seed < 260; seed++)
                if (wanted(Table.Create(seed))) return seed;
            Assert.Inconclusive("no seed deals the tiles this test needs");
            return -1;
        }

        private static bool CraneAndFall(Table t) => !t.FaceDown(Crane).IsEmpty && !t.FaceDown(Fall).IsEmpty;

        // ---------- the owed move (CR-1, CR-4) ----------

        [Test]
        public void Crane_ThenActiveFall_TheMoveIsOwedUntilTheLanding_OneElimination_OneTurnChange()
        {
            Table t = Dealing(CraneAndFall);
            TabletopObjectId crane = t.FaceDown(Crane), fall = t.FaceDown(Fall);
            PlayerId me = t.Active;
            Assert.IsTrue(t.Land(me, crane).Succeeded);
            TrapFloorCoordinate to = t.At(fall);
            t.Dice(to.X, to.Y);

            TrapFloorLaunchResult launch = t.Launch(me, crane);
            Assert.IsTrue(launch.Succeeded, launch.Error.ToString());
            Assert.AreEqual(fall, launch.TargetFloorCardId);
            TrapFloorTrapResolutionRecord craneRecord = t.Record(crane);
            Assert.IsTrue(craneRecord.IsMoveOwed, "the Crane is not done while the pawn has not moved");
            Assert.IsFalse(craneRecord.IsPending, "and it is not applied twice");
            Assert.IsTrue(t.Ability.TryGetOwedMove(me, out TrapFloorTrapResolutionRecord owed) && owed.LaunchTargetFloorCardId == fall);
            Assert.IsFalse(t.Launch(me, crane).Succeeded, "a second launch of the same Crane is refused");

            // The pawn arrives (by hand, or the forced move): the tile turns over, the guidance ends, the Crane is done.
            Assert.IsTrue(t.Land(me, fall).Succeeded, "the forced landing reveals the Fall Trap");
            Assert.IsTrue(t.Exec.ClearGuidance(t.T.Match, new TrapFloorClearGuidanceCommand(t.T.Context(me))).Succeeded);
            Assert.AreEqual(TrapFloorTrapResolutionDisposition.Resolved, t.Record(crane).Disposition, "the Crane occurrence is complete");
            Assert.IsFalse(t.Ability.TryGetOwedMove(me, out _));

            int myIndex = Array.IndexOf(Order(t), me);
            Assert.IsTrue(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(me), fall)).Succeeded);
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound, t.T.Turn.GetPlayerState(me));
            Assert.AreEqual(Order(t)[(myIndex + 1) % Order(t).Length], t.Active, "one turn change");
            Assert.IsFalse(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(me), fall)).Succeeded, "never twice");
        }

        [Test]
        public void Crane_ThenAFallTrapAlreadyTurnedOver_CompletesTheMove_AndNothingTriggersAgain()
        {
            Table t = Dealing(CraneAndFall);
            TabletopObjectId crane = t.FaceDown(Crane), fall = t.FaceDown(Fall);
            PlayerId first = t.Active;
            Assert.IsTrue(t.Land(first, fall).Succeeded);
            Assert.IsTrue(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(first), fall)).Succeeded);

            PlayerId second = t.Active;
            Assert.IsTrue(t.Land(second, crane).Succeeded);
            TrapFloorCoordinate to = t.At(fall);
            t.Dice(to.X, to.Y);
            Assert.IsTrue(t.Launch(second, crane).Succeeded);
            Assert.AreEqual(TrapFloorRevealFloorError.FloorAlreadyRevealed, t.Land(second, fall).Error, "a turned-over Trap is not revealed again");
            Assert.IsTrue(t.Exec.ClearGuidance(t.T.Match, new TrapFloorClearGuidanceCommand(t.T.Context(second))).Succeeded);
            Assert.AreEqual(TrapFloorTrapResolutionDisposition.Resolved, t.Record(crane).Disposition);
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.Active, t.T.Turn.GetPlayerState(second), "the inactive Fall does nothing");
            Assert.AreEqual(second, t.Active);
            Assert.IsFalse(t.Exec.ClearGuidance(t.T.Match, new TrapFloorClearGuidanceCommand(t.T.Context(second))).Succeeded, "idempotent");
        }

        [Test]
        public void GuestCrane_TheHostsOwnRulesHoldTheOwedMove_AndTheHostCompletesIt()
        {
            int seed = Seed(CraneAndFall);
            Table guest = Table.Create(seed);
            Table hostTable = Table.Create(seed); // the host's table: the same deal, the guest's commands replayed on it
            TabletopObjectId crane = guest.FaceDown(Crane), fall = guest.FaceDown(Fall);
            PlayerId player = guest.Active;
            Assert.AreEqual(player, hostTable.Active);
            TrapFloorCoordinate to = guest.At(fall);
            foreach (Table t in new[] { guest, hostTable })
            {
                Assert.IsTrue(t.Land(player, crane).Succeeded);
                t.Dice(to.X, to.Y); // AUTH-DICE: the host's faces, the same on both tables
                Assert.IsTrue(t.Launch(player, crane).Succeeded);
            }

            // Nothing from the guest's screen: the host reads the obligation from its own rules state.
            Assert.IsTrue(hostTable.Ability.TryGetOwedMove(player, out TrapFloorTrapResolutionRecord owed), "the host sees the guest's owed move");
            Assert.AreEqual(fall, owed.LaunchTargetFloorCardId);

            // The host's End Turn makes the move (its own landing reveal), then completes the occurrence once.
            Assert.IsTrue(hostTable.Land(player, fall).Succeeded);
            Assert.IsTrue(hostTable.Service.CompleteOwedMove(player, fall));
            Assert.IsFalse(hostTable.Service.CompleteOwedMove(player, fall), "a second completion changes nothing");
            Assert.IsTrue(hostTable.Ability.TryGetTrap(fall, out TrapFloorTrapResolutionRecord landed) && landed.IsPending,
                "the Fall Trap the Crane dropped the guest on waits for them on the host");
        }

        [Test]
        public void Spring_OwedMove_SurvivesTheWire_AndTheHostAgreesOnTheTarget()
        {
            int seed = -1;
            TabletopObjectId spring = TabletopObjectId.Empty;
            for (int s = 11; s < 260 && seed < 0; s++)
            {
                Table probe = Table.Create(s);
                foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in probe.T.Template.FloorCardIds)
                {
                    if (!probe.T.Template.TryGetFloorCardState(probe.T.Match, pair.Value, out TrapFloorFloorCardState floor)
                        || floor.Content.TrapEffect != TrapFloorTrapEffectCategory.SpringLaunch
                        || floor.Content.Category != TrapFloorFloorContentCategory.Trap)
                        continue;
                    TrapFloorCoordinate c = floor.Coordinate;
                    int x = c.X, y = c.Y;
                    switch (floor.Content.LaunchDirection)
                    {
                        case TrapFloorBlindMovementDirection.Up: y++; break;
                        case TrapFloorBlindMovementDirection.Down: y--; break;
                        case TrapFloorBlindMovementDirection.Left: x--; break;
                        case TrapFloorBlindMovementDirection.Right: x++; break;
                    }

                    if (probe.T.Template.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out _))
                    {
                        seed = s;
                        spring = pair.Value;
                        break;
                    }
                }
            }

            if (seed < 0) Assert.Inconclusive("no seed deals a Spring that lands on the floor");
            Table guest = Table.Create(seed);
            Table hostTable = Table.Create(seed);
            PlayerId player = guest.Active;
            Assert.IsTrue(guest.Land(player, spring).Succeeded);
            Assert.IsTrue(hostTable.Land(player, spring).Succeeded);
            TrapFloorLaunchResult a = guest.Launch(player, spring);
            TrapFloorLaunchResult b = hostTable.Launch(player, spring);
            Assert.IsTrue(a.Succeeded && b.Succeeded);
            Assert.AreEqual(a.TargetFloorCardId, b.TargetFloorCardId, "host and guest send the pawn to the same tile");

            // Reconnect / rebuild: the rules state goes over the wire and comes back with the owed move.
            TrapFloorSessionState restored = RoundTrip(hostTable.Snap()).Restore();
            Assert.IsTrue(restored.AbilityResolution.TryGetOwedMove(player, out TrapFloorTrapResolutionRecord owed), "kept across the wire");
            Assert.AreEqual(b.TargetFloorCardId, owed.LaunchTargetFloorCardId);
            Assert.AreEqual(TrapFloorTrapResolutionDisposition.MoveOwed, owed.Disposition);
        }

        // ---------- the Crane roll (CR-2, CR-3) ----------

        [Test]
        public void CraneRoll_CountsOnlyForTheOccurrenceItWasAskedFor()
        {
            Guid match = Guid.NewGuid();
            TabletopObjectId tile = TabletopObjectId.New();
            TrapFloorCraneOccurrence crane = new TrapFloorCraneOccurrence(match, tile, 40);
            TrapFloorCraneRollGate gate = new TrapFloorCraneRollGate();

            Assert.IsFalse(gate.Rolled(crane, 2, 3), "a roll nobody asked for is not the Crane's");
            Assert.IsFalse(gate.IsReadyFor(crane, 2, 3));
            gate.Ask(crane);
            Assert.IsFalse(gate.IsReadyFor(crane, 2, 3), "asked, not yet rolled: no stale faces are used");
            Assert.IsTrue(gate.Rolled(crane, 2, 3));
            Assert.IsTrue(gate.IsReadyFor(crane, 2, 3), "Reminders: Apply stays available after the roll");

            Assert.IsFalse(gate.IsReadyFor(crane, 5, 3), "Undo: the dice show other faces again: that roll is gone");
            Assert.IsFalse(gate.IsReadyFor(new TrapFloorCraneOccurrence(match, tile, 41), 2, 3), "Undo back past the reveal, another occurrence");
            Assert.IsFalse(gate.IsReadyFor(new TrapFloorCraneOccurrence(match, TabletopObjectId.New(), 40), 2, 3), "another floor's tile");
            Assert.IsFalse(gate.IsReadyFor(new TrapFloorCraneOccurrence(Guid.NewGuid(), tile, 40), 2, 3), "another match, same tile id");

            gate.Clear(); // the roll was used
            Assert.IsFalse(gate.IsReadyFor(crane, 2, 3), "one roll, one Crane");
            gate.Ask(crane);
            Assert.IsFalse(gate.IsReadyFor(crane, 2, 3), "a new ask needs a new roll, even with the same faces lying there");
        }

        [Test]
        public void RemindersCrane_RollThenApply_TheRollSurvivesATableRebuild_AndApplyRunsOnce()
        {
            Table t = Dealing(CraneAndFall);
            TabletopObjectId crane = t.FaceDown(Crane), fall = t.FaceDown(Fall);
            PlayerId me = t.Active;
            Assert.IsTrue(t.Land(me, crane).Succeeded);
            TrapFloorCraneRollGate gate = new TrapFloorCraneRollGate();
            TrapFloorCraneOccurrence occurrence = TrapFloorCraneOccurrence.For(t.T.Match.Id.Value, t.Record(crane));
            gate.Ask(occurrence);
            TrapFloorCoordinate to = t.At(fall);
            t.Dice(to.X, to.Y); // the player's own throw settles
            Assert.IsTrue(gate.Rolled(occurrence, to.X, to.Y));

            // The table is rebuilt from the wire (another computer's update): the same occurrence, still ready.
            TrapFloorSessionStateSnapshot back = RoundTrip(t.Snap());
            Assert.IsTrue(back.Restore().AbilityResolution.TryGetTrap(crane, out TrapFloorTrapResolutionRecord rebuilt));
            Assert.IsTrue(gate.IsReadyFor(TrapFloorCraneOccurrence.For(t.T.Match.Id.Value, rebuilt), to.X, to.Y));

            Assert.IsTrue(t.Launch(me, crane).Succeeded, "Apply");
            Assert.IsTrue(t.Record(crane).IsMoveOwed, "applied: the pawn now moves to the lit tile");
            Assert.IsFalse(t.Launch(me, crane).Succeeded, "Apply cannot run twice");
        }

        // ---------- Reminders skip (RM-1) and Sticky ----------

        [Test]
        public void Reminders_UnresolvedSticky_ExplicitSkip_IsFinal_NoLaterWarningOrLateApply()
        {
            Table t = Dealing(x => !x.FaceDown(Sticky).IsEmpty);
            TabletopObjectId sticky = t.FaceDown(Sticky);
            PlayerId me = t.Active;
            int round = t.T.Turn.CurrentRound;
            Assert.IsTrue(t.Land(me, sticky).Succeeded);
            Assert.IsTrue(t.Record(sticky).IsPending);

            Assert.AreEqual(1, t.Service.SkipUnresolved(me), "End Turn pressed through the Reminders warning");
            Assert.AreEqual(TrapFloorTrapResolutionDisposition.Skipped, t.Record(sticky).Disposition);
            Assert.AreEqual(0, t.Service.SkipUnresolved(me), "nothing is left to skip");
            Assert.IsFalse(AnyPending(t, me), "nothing pending: no warning on later turns");
            Assert.IsFalse(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(me), sticky)).Succeeded,
                "a skipped effect cannot be applied later");
            Assert.IsFalse(t.Ability.TryGetStickyStatus(me, round + 1, out _), "and nothing was scheduled");
            Assert.IsTrue(t.Turns.Advance(t.T.Match, t.T.Context(me)).Succeeded, "the turn ends normally");
        }

        [Test]
        public void Sticky_Applied_IsActiveOnlyNextRound_ExpiresAfter_AndNeverBlocksEndTurn()
        {
            Table t = Dealing(x => !x.FaceDown(Sticky).IsEmpty);
            TabletopObjectId sticky = t.FaceDown(Sticky);
            PlayerId me = t.Active;
            int round = t.T.Turn.CurrentRound;
            Assert.IsTrue(t.Land(me, sticky).Succeeded);
            Assert.IsTrue(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(me), sticky)).Succeeded, "Apply");
            Assert.IsFalse(AnyPending(t, me), "applied: no longer a pending effect");
            Assert.IsFalse(t.Ability.TryGetStickyStatus(me, round, out _), "not this round");
            Assert.IsTrue(t.Ability.TryGetStickyStatus(me, round + 1, out _), "scheduled for the next round");

            Assert.IsTrue(t.Turns.Advance(t.T.Match, t.T.Context(me)).Succeeded, "End Turn on the Sticky tile");
            Assert.IsTrue(t.AdvanceUntil(round + 1, me), "the next round's turn comes");
            Assert.IsTrue(t.Ability.TryGetStickyStatus(me, t.T.Turn.CurrentRound, out _), "stuck during that round");
            Assert.IsTrue(t.Turns.Advance(t.T.Match, t.T.Context(me)).Succeeded, "a stuck player still ends the turn");
            Assert.IsTrue(t.AdvanceUntil(round + 2, me));
            Assert.IsFalse(t.Ability.TryGetStickyStatus(me, round + 1, out _), "expired with its round");
            Assert.IsFalse(t.Ability.TryGetStickyStatus(me, round + 2, out _));
        }

        [Test]
        public void Reconnect_KeepsPendingOwedResolvedAndSkippedApart()
        {
            Table t = Dealing(x => CraneAndFall(x) && !x.FaceDown(Sticky).IsEmpty);
            TabletopObjectId crane = t.FaceDown(Crane), fall = t.FaceDown(Fall), sticky = t.FaceDown(Sticky);
            PlayerId me = t.Active;
            Assert.IsTrue(t.Land(me, sticky).Succeeded);
            Assert.AreEqual(1, t.Service.SkipUnresolved(me));
            Assert.IsTrue(t.Land(me, crane).Succeeded);
            TrapFloorCoordinate to = t.At(fall);
            t.Dice(to.X, to.Y);
            Assert.IsTrue(t.Launch(me, crane).Succeeded);

            TrapFloorSessionStateSnapshot back = RoundTrip(t.Snap());
            TrapFloorAbilityResolutionState restored = back.Restore().AbilityResolution;
            Assert.IsTrue(restored.TryGetTrap(sticky, out TrapFloorTrapResolutionRecord s) && s.Disposition == TrapFloorTrapResolutionDisposition.Skipped);
            Assert.IsTrue(restored.TryGetTrap(crane, out TrapFloorTrapResolutionRecord c) && c.IsMoveOwed && c.LaunchTargetFloorCardId == fall);
            Assert.IsNotNull(restored.ActiveDodgeAssistance, "the lit tile comes back too");
        }

        // Over the wire and back, as a reconnecting or rebuilt computer receives the table's rules state.
        private static TrapFloorSessionStateSnapshot RoundTrip(TrapFloorSessionStateSnapshot snapshot)
        {
            byte[] bytes = TableWire.Write(snapshot, true);
            object back = TableWire.Read(bytes, 0, bytes.Length, true);
            Assert.IsInstanceOf<TrapFloorSessionStateSnapshot>(back);
            return (TrapFloorSessionStateSnapshot)back;
        }

        private static bool AnyPending(Table t, PlayerId player)
        {
            for (int i = 0; i < t.Ability.TrapRecords.Count; i++)
                if (t.Ability.TrapRecords[i].AffectedPlayerId == player && t.Ability.TrapRecords[i].IsPending) return true;
            return false;
        }

        private static PlayerId[] Order(Table t)
        {
            PlayerId[] order = new PlayerId[t.T.Turn.PlayerOrder.Count];
            for (int i = 0; i < order.Length; i++) order[i] = t.T.Turn.PlayerOrder[i];
            return order;
        }
    }
}
