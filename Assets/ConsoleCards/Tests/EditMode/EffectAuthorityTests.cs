using System;
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
    /// 2026-11-06, host-executed effects and dice (AUTH-EFFECTS, AUTH-DICE, AUTH-UNDO-2). The effect commands run on the
    /// game's real services and a seeded test table, exactly as the host replays them; requests are forged the way a
    /// modified client could send them. Pure; no network.
    /// </summary>
    public sealed class EffectAuthorityTests
    {
        private sealed class Evidence : ITrapFloorEffectEvidence
        {
            public readonly HashSet<PlayerId> Escaped = new HashSet<PlayerId>();
            public readonly HashSet<PlayerId> OnHole = new HashSet<PlayerId>();
            public readonly HashSet<PlayerId> Stuck = new HashSet<PlayerId>();
            public readonly Dictionary<PlayerId, TabletopObjectId> Standing = new Dictionary<PlayerId, TabletopObjectId>();
            public bool IsSurvivalEndgame { get; set; }
            public bool IsEscaped(PlayerId player) => Escaped.Contains(player);
            public bool StandsOnHoleOrOffFloor(PlayerId player) => OnHole.Contains(player);
            public bool IsStuckWithoutKey(PlayerId player) => Stuck.Contains(player);
            public bool StandsOnFloorCard(PlayerId player, TabletopObjectId floorCardId) =>
                Standing.TryGetValue(player, out TabletopObjectId on) && on == floorCardId;
        }

        // The host's table, with the game's own services on it.
        private sealed class Host
        {
            public TrapFloorTestTable T;
            public TrapFloorAbilityResolutionState Ability;
            public TrapFloorCollapseState Collapse;
            public TrapFloorObjectiveState Objective;
            public TrapFloorTurnService Turns;
            public TrapFloorAbilityResolutionService Service;
            public TrapFloorRevealFloorUseCase Reveal;
            public Evidence Ev = new Evidence();
            public TrapFloorEffectExecutor Exec;

            public static Host Create(int players = 3, int seed = 11)
            {
                Host h = new Host { T = TrapFloorTestTable.Create(players, seed) };
                h.Ability = new TrapFloorAbilityResolutionState(h.T.Match.Id);
                h.Collapse = new TrapFloorCollapseState(h.T.Match.Id, h.T.Template.FloorCardIds.Count);
                h.Objective = new TrapFloorObjectiveState(h.T.Match.Id, 1);
                h.Service = new TrapFloorAbilityResolutionService(h.Ability, h.T.Turn, h.T.Feed, h.T.Template, h.Collapse);
                h.Turns = new TrapFloorTurnService(h.T.Template, h.T.Turn, new ControllerInputHandService(), h.T.Feed, h.Ability);
                h.Reveal = new TrapFloorRevealFloorUseCase(h.T.Template, h.T.Feed, h.Collapse, h.Ability, h.T.Turn);
                h.Exec = new TrapFloorEffectExecutor(h.Service, h.Ability, h.Turns, h.T.Turn, h.T.Template, h.Ev, id => null);
                return h;
            }

            public PlayerId Active => T.Turn.ActivePlayerId;

            public CommandContext Ctx(PlayerId who) => T.Context(who);

            public TrapFloorSessionStateSnapshot Snap() =>
                TrapFloorSessionStateSnapshot.Capture(T.Feed, Objective, Collapse, T.Turn, Ability);

            /// <summary>A face-down tile whose content matches, or Empty.</summary>
            public TabletopObjectId Tile(Func<TrapFloorFloorContentDefinition, bool> match)
            {
                foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in T.Template.FloorCardIds)
                {
                    if (T.Template.TryGetFloorCardState(T.Match, pair.Value, out TrapFloorFloorCardState floor)
                        && !floor.IsRevealed
                        && match(floor.Content))
                        return pair.Value;
                }

                return TabletopObjectId.Empty;
            }

            // The active player lands on the tile: the landing reveal (a command the host already checks).
            public void Land(TabletopObjectId tile)
            {
                TrapFloorRevealFloorResult result = Reveal.Execute(T.Match, new TrapFloorRevealFloorCommand(Ctx(Active), tile));
                Assert.IsTrue(result.Succeeded, $"the landing reveal was refused ({result.Error})");
                Ev.Standing[Active] = tile;
            }
        }

        private static bool AppliedTrap(TrapFloorFloorContentDefinition c) =>
            c.Category == TrapFloorFloorContentCategory.Trap && c.HasSupportedAssistedTrapEffect && !c.IsLaunchTrap;

        private static bool StatusTrap(TrapFloorFloorContentDefinition c) =>
            AppliedTrap(c) && c.TrapEffect != TrapFloorTrapEffectCategory.EliminateForCurrentRound;

        // A seeded table that deals the kind of tile asked for (the deal is random per seed, the same on every computer).
        private static Host WithTile(Func<TrapFloorFloorContentDefinition, bool> kind, out TabletopObjectId tile)
        {
            for (int seed = 11; seed < 90; seed++)
            {
                Host h = Host.Create(3, seed);
                tile = h.Tile(kind);
                if (!tile.IsEmpty) return h;
            }

            Assert.Fail("no seed between 11 and 89 deals that tile");
            tile = TabletopObjectId.Empty;
            return null;
        }

        // ---------- Trap effects ----------

        [Test]
        public void ApplyTrap_ByTheTurnPlayer_RunsOnce_ASecondRequestDoesNothing()
        {
            Host h = WithTile(StatusTrap, out TabletopObjectId tile);
            h.Land(tile);
            PlayerId me = h.Active;
            Assert.IsTrue(h.Exec.ApplyTrap(h.T.Match, new TrapFloorApplyTrapCommand(h.Ctx(me), tile)).Succeeded);
            int statuses = h.Ability.BlindStatuses.Count + h.Ability.SlowStatuses.Count + h.Ability.StickyStatuses.Count;
            Assert.AreEqual(1, statuses, "the host scheduled the Trap's own status");
            Assert.IsFalse(h.Exec.ApplyTrap(h.T.Match, new TrapFloorApplyTrapCommand(h.Ctx(me), tile)).Succeeded,
                "a Trap is applied once");
            Assert.AreEqual(statuses, h.Ability.BlindStatuses.Count + h.Ability.SlowStatuses.Count + h.Ability.StickyStatuses.Count);
        }

        [Test]
        public void ApplyTrap_SentByAnotherPlayer_IsRefused_TheTrapStillWaits()
        {
            Host h = WithTile(AppliedTrap, out TabletopObjectId tile);
            h.Land(tile);
            PlayerId other = h.T.Turn.PlayerOrder[1];
            TrapFloorTurnAdvanceResult result = h.Exec.ApplyTrap(h.T.Match, new TrapFloorApplyTrapCommand(h.Ctx(other), tile));
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(h.Ability.TrapRecords[0].IsPending);
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.Active, h.T.Turn.GetPlayerState(h.Active), "nobody was knocked out by it");
        }

        [Test]
        public void ForgedEffect_OnATileNoTrapWaitsOn_IsRefused()
        {
            Host h = WithTile(AppliedTrap, out TabletopObjectId tile); // not landed on: nothing waits
            Assert.IsFalse(h.Exec.ApplyTrap(h.T.Match, new TrapFloorApplyTrapCommand(h.Ctx(h.Active), tile)).Succeeded);
            Assert.AreEqual(0, h.Ability.SlowStatuses.Count + h.Ability.BlindStatuses.Count + h.Ability.StickyStatuses.Count);
        }

        [Test]
        public void FallTrap_Applied_KnocksOutOnlyTheTurnPlayer_Once()
        {
            Host h = WithTile(c => AppliedTrap(c) && c.TrapEffect == TrapFloorTrapEffectCategory.EliminateForCurrentRound, out TabletopObjectId tile);
            h.Land(tile);
            PlayerId me = h.Active;
            Assert.IsTrue(h.Exec.ApplyTrap(h.T.Match, new TrapFloorApplyTrapCommand(h.Ctx(me), tile)).Succeeded);
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound, h.T.Turn.GetPlayerState(me));
            Assert.AreNotEqual(me, h.T.Turn.ActivePlayerId, "the turn moved on as the rules say");
            Assert.IsFalse(h.Exec.ApplyTrap(h.T.Match, new TrapFloorApplyTrapCommand(h.Ctx(me), tile)).Succeeded);
        }

        [Test]
        public void BlindRoll_WithoutABlindStatus_IsRefused()
        {
            Host h = Host.Create();
            TrapFloorBlindDirectionRollResult result = h.Exec.RollBlindDirection(
                h.T.Match, new TrapFloorRollBlindDirectionCommand(h.Ctx(h.Active), new TabletopObjectId(Guid.NewGuid())));
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(TrapFloorBlindDirectionRollError.NoActiveBlindStatus, result.Error);
        }

        [Test]
        public void Retrigger_OutsideTheSurvivalEndgame_OrOffTheTile_IsRefused()
        {
            Host h = WithTile(AppliedTrap, out TabletopObjectId tile);
            h.Land(tile);
            h.Ability.TrapRecords[0].SetDisposition(TrapFloorTrapResolutionDisposition.Resolved);
            TrapFloorRetriggerTrapCommand again = new TrapFloorRetriggerTrapCommand(h.Ctx(h.Active), tile);
            Assert.AreEqual(TrapFloorEffectError.NotAllowedNow, h.Exec.RetriggerTrap(h.T.Match, again).Error);
            h.Ev.IsSurvivalEndgame = true;
            h.Ev.Standing.Clear();
            Assert.AreEqual(TrapFloorEffectError.NotOnThatTile, h.Exec.RetriggerTrap(h.T.Match, again).Error);
        }

        // ---------- Abilities and guidance ----------

        [Test]
        public void UseAbility_ForACardThatIsNotInThatSlot_IsRefused()
        {
            Host h = Host.Create();
            TrapFloorPlayerSetupDefinition setup = h.T.SetupOf(h.Active);
            TrapFloorAbilityActivationResult result = h.Exec.UseAbility(
                h.T.Match, new TrapFloorUseAbilityCommand(h.Ctx(h.Active), new TabletopObjectId(Guid.NewGuid()), setup.MainSlotContainerId));
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, h.Ability.CopyAbilityActivations().Length);
        }

        [Test]
        public void ClearGuidance_WithNothingLit_OrOffTheLitTile_IsRefused()
        {
            Host h = Host.Create();
            Assert.AreEqual(TrapFloorEffectError.NotAllowedNow,
                h.Exec.ClearGuidance(h.T.Match, new TrapFloorClearGuidanceCommand(h.Ctx(h.Active))).Error);
        }

        // ---------- Elimination ----------

        [Test]
        public void SelfElimination_WithoutAReasonOnTheHostsTable_IsRefused()
        {
            Host h = Host.Create();
            PlayerId me = h.Active;
            foreach (TrapFloorEliminationReason reason in new[]
                     { TrapFloorEliminationReason.None, TrapFloorEliminationReason.Fell, TrapFloorEliminationReason.Escaped, TrapFloorEliminationReason.StuckWithoutKey })
            {
                Assert.IsFalse(h.Exec.Eliminate(h.T.Match, new TrapFloorEliminatePlayerCommand(h.Ctx(me), me, reason)).Succeeded, reason.ToString());
            }

            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.Active, h.T.Turn.GetPlayerState(me));
        }

        [Test]
        public void Elimination_WithTheHostsReason_IsApplied_Once()
        {
            Host h = Host.Create();
            PlayerId me = h.Active, other = h.T.Turn.PlayerOrder[2];
            h.Ev.OnHole.Add(other); // the collapse dropped seat 3 on the host's table
            Assert.IsTrue(h.Exec.Eliminate(h.T.Match, new TrapFloorEliminatePlayerCommand(h.Ctx(me), other, TrapFloorEliminationReason.Fell)).Succeeded);
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound, h.T.Turn.GetPlayerState(other));
            Assert.AreEqual(me, h.T.Turn.ActivePlayerId, "knocking out another player does not end the turn");
            Assert.IsFalse(h.Exec.Eliminate(h.T.Match, new TrapFloorEliminatePlayerCommand(h.Ctx(me), other, TrapFloorEliminationReason.Fell)).Succeeded,
                "once only");
            h.Ev.Escaped.Add(me);
            Assert.IsTrue(h.Exec.Eliminate(h.T.Match, new TrapFloorEliminatePlayerCommand(h.Ctx(me), me, TrapFloorEliminationReason.Escaped)).Succeeded);
        }

        [Test]
        public void EffectCommand_SentTwice_RunsOnceOnTheHost()
        {
            Host h = Host.Create();
            HostCommandPolicy policy = new HostCommandPolicy();
            TrapFloorApplyTrapCommand command = new TrapFloorApplyTrapCommand(h.Ctx(h.Active), new TabletopObjectId(Guid.NewGuid()));
            Assert.AreEqual(HostCommandVerdict.Allowed, policy.Check(h.T.Match, h.Active, command, null));
            Assert.AreEqual(HostCommandVerdict.Duplicate, policy.Check(h.T.Match, h.Active, command, null));
        }

        // ---------- AUTH-DICE ----------

        [Test]
        public void HostDice_AThrowAgainBeforeARuleReadsIt_KeepsTheHostsResult()
        {
            TrapFloorHostDice dice = new TrapFloorHostDice();
            TabletopObjectId die = new TabletopObjectId(Guid.NewGuid());
            int calls = 0;
            Func<int, int> roll = sides => { calls++; return calls == 1 ? 2 : 5; };
            Assert.AreEqual(2, dice.Decide(die, 6, 7, null, roll));
            Assert.AreEqual(2, dice.Decide(die, 6, 7, null, roll), "no fishing for a better face");
            Assert.AreEqual(1, calls);
            dice.Consumed(die);
            Assert.AreEqual(5, dice.Decide(die, 6, 7, null, roll), "a rule read the last one: a new result");
        }

        [Test]
        public void HostDice_ANewTurn_OrARerollTheRulesAskFor_GetsANewResult()
        {
            TrapFloorHostDice dice = new TrapFloorHostDice();
            TabletopObjectId die = new TabletopObjectId(Guid.NewGuid());
            Queue<int> faces = new Queue<int>(new[] { 6, 3, 4 });
            Func<int, int> roll = sides => faces.Dequeue();
            Assert.AreEqual(6, dice.Decide(die, 6, 1, v => v > 5, roll));
            Assert.AreEqual(3, dice.Decide(die, 6, 1, v => v > 5, roll), "6 is beyond this floor: the rules ask for a re-roll");
            Assert.AreEqual(4, dice.Decide(die, 6, 2, v => v > 5, roll), "the turn moved on");
        }

        [Test]
        public void HostDice_AResultOutOfRange_IsNeverUsed()
        {
            TrapFloorHostDice dice = new TrapFloorHostDice();
            Assert.Throws<InvalidOperationException>(() => dice.Decide(new TabletopObjectId(Guid.NewGuid()), 4, 1, null, s => 9));
        }

        // ---------- AUTH-UNDO-2 ----------

        [Test]
        public void UndoEligibility_ATurnedTile_AndAnAppliedTrap_AreRulesResults()
        {
            Host h = WithTile(StatusTrap, out TabletopObjectId tile);
            TrapFloorSessionStateSnapshot before = h.Snap();
            h.Land(tile);
            TrapFloorSessionStateSnapshot afterReveal = h.Snap();
            Assert.IsTrue(TrapFloorUndoEligibility.IsRulesResult(before, afterReveal), "a tile turned over");
            Assert.IsTrue(h.Exec.ApplyTrap(h.T.Match, new TrapFloorApplyTrapCommand(h.Ctx(h.Active), tile)).Succeeded);
            Assert.IsTrue(TrapFloorUndoEligibility.IsRulesResult(afterReveal, h.Snap()), "a Trap applied");
        }

        // FAIR-UNDO (s141): an escape is batched with its elimination, so the step that escapes is a rules result and
        // cannot be taken back in Full: undoing it cannot hand the floor's win back after later effects relied on it.
        [Test]
        public void UndoEligibility_AnEscapeStep_IsARulesResult()
        {
            Host h = Host.Create();
            PlayerId me = h.Active;
            TrapFloorSessionStateSnapshot before = h.Snap();
            h.Ev.Escaped.Add(me);
            Assert.IsTrue(h.Exec.Eliminate(h.T.Match, new TrapFloorEliminatePlayerCommand(h.Ctx(me), me, TrapFloorEliminationReason.Escaped)).Succeeded);
            Assert.IsTrue(TrapFloorUndoEligibility.IsRulesResult(before, h.Snap()), "the escape's elimination");
        }

        // FAIR-UNDO (s141): the rules-result verdict does not depend on who asks (the host player's MayUndo applies the
        // same IsRulesResultStep test locally; this pins that the policy has no seat-specific exception).
        [Test]
        public void RulesResultVerdict_DoesNotDependOnTheRequester()
        {
            HostUndoPolicy policy = new HostUndoPolicy();
            PlayerId host = new PlayerId(Guid.NewGuid()), guest = new PlayerId(Guid.NewGuid());
            CommandId step = CommandId.New();
            Assert.AreEqual(policy.Check(guest, Guid.NewGuid(), step, true, step, guest, true),
                policy.Check(host, Guid.NewGuid(), step, true, step, host, true));
            Assert.AreEqual(HostUndoVerdict.RulesResult, policy.Check(host, Guid.NewGuid(), step, true, step, host, true));
        }

        [Test]
        public void UndoEligibility_NothingRulesDriven_IsNotARulesResult()
        {
            Host h = Host.Create();
            TrapFloorSessionStateSnapshot before = h.Snap();
            h.Ability.RecordAbilityActivation(new TabletopObjectId(Guid.NewGuid()), h.Active, 1); // a choice, not a result
            Assert.IsFalse(TrapFloorUndoEligibility.IsRulesResult(before, before));
            Assert.IsFalse(TrapFloorUndoEligibility.IsRulesResult(before, h.Snap()));
        }

        [Test]
        public void HostUndo_OfARulesResult_IsRefused_OwnPlainStepsStillUndo()
        {
            HostUndoPolicy policy = new HostUndoPolicy();
            PlayerId me = new PlayerId(Guid.NewGuid());
            CommandId step = CommandId.New();
            Assert.AreEqual(HostUndoVerdict.RulesResult, policy.Check(me, Guid.NewGuid(), step, true, step, me, true));
            Assert.AreEqual(HostUndoVerdict.Allowed, policy.Check(me, Guid.NewGuid(), step, true, step, me, false));
            Assert.IsNotNull(HostUndoPolicy.Describe(HostUndoVerdict.RulesResult, false));
        }
    }
}
