using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A (owner, 2026-11-12: "the Crane launched a pawn onto a Fall Trap; it did not die and the turn moved on"). A
    /// forced landing (where a Crane or Spring drops the pawn) is the same landing as any other on the host's rules: the
    /// landing reveal command, then the Trap's own effect. These pin that on the game's real services and a seeded test
    /// table (no presentation, no network): an active Fall Trap knocks the player out once and the turn passes once; a
    /// Fall Trap already turned over does nothing; an ordinary tile changes nothing.
    /// </summary>
    public sealed class ChainedLandingTests
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

        private sealed class Table
        {
            public TrapFloorTestTable T;
            public TrapFloorAbilityResolutionState Ability;
            public TrapFloorRevealFloorUseCase Reveal;
            public TrapFloorEffectExecutor Exec;
            public readonly Evidence Ev = new Evidence();

            public PlayerId Active => T.Turn.ActivePlayerId;

            public static Table Create(int seed)
            {
                Table t = new Table { T = TrapFloorTestTable.Create(3, seed) };
                t.Ability = new TrapFloorAbilityResolutionState(t.T.Match.Id);
                TrapFloorCollapseState collapse = new TrapFloorCollapseState(t.T.Match.Id, t.T.Template.FloorCardIds.Count);
                TrapFloorAbilityResolutionService service = new TrapFloorAbilityResolutionService(t.Ability, t.T.Turn, t.T.Feed, t.T.Template, collapse);
                TrapFloorTurnService turns = new TrapFloorTurnService(t.T.Template, t.T.Turn, new ControllerInputHandService(), t.T.Feed, t.Ability);
                t.Reveal = new TrapFloorRevealFloorUseCase(t.T.Template, t.T.Feed, collapse, t.Ability, t.T.Turn);
                t.Exec = new TrapFloorEffectExecutor(service, t.Ability, turns, t.T.Turn, t.T.Template, t.Ev, id => null);
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

            public TrapFloorRevealFloorResult Land(PlayerId who, TabletopObjectId tile)
            {
                TrapFloorRevealFloorResult result = Reveal.Execute(T.Match, new TrapFloorRevealFloorCommand(T.Context(who), tile));
                if (result.Succeeded) Ev.Standing[who] = tile;
                return result;
            }

            public int PendingFor(PlayerId who, TabletopObjectId tile)
            {
                int n = 0;
                for (int i = 0; i < Ability.TrapRecords.Count; i++)
                    if (Ability.TrapRecords[i].AffectedPlayerId == who && Ability.TrapRecords[i].FloorCardId == tile && Ability.TrapRecords[i].IsPending) n++;
                return n;
            }
        }

        private static bool Crane(TrapFloorFloorContentDefinition c) =>
            c.Category == TrapFloorFloorContentCategory.Trap && c.TrapEffect == TrapFloorTrapEffectCategory.CraneDrop;

        private static bool Fall(TrapFloorFloorContentDefinition c) =>
            c.Category == TrapFloorFloorContentCategory.Trap && c.TrapEffect == TrapFloorTrapEffectCategory.EliminateForCurrentRound
            && c.HasSupportedAssistedTrapEffect;

        private static bool Ordinary(TrapFloorFloorContentDefinition c) => c.Category != TrapFloorFloorContentCategory.Trap;

        // A seeded table that deals a Crane and a Fall Trap (and an ordinary tile).
        private static Table Dealt(out TabletopObjectId crane, out TabletopObjectId fall, out TabletopObjectId plain)
        {
            for (int seed = 11; seed < 200; seed++)
            {
                Table t = Table.Create(seed);
                crane = t.FaceDown(Crane);
                fall = t.FaceDown(Fall);
                plain = t.FaceDown(Ordinary);
                if (!crane.IsEmpty && !fall.IsEmpty && !plain.IsEmpty) return t;
            }

            Assert.Inconclusive("no seed between 11 and 199 deals a Crane, a Fall Trap and an ordinary tile");
            crane = fall = plain = TabletopObjectId.Empty;
            return null;
        }

        [Test]
        public void Crane_ThenAnActiveFallTrap_RevealsIt_KnocksThePlayerOutOnce_TheTurnPassesOnce()
        {
            Table t = Dealt(out TabletopObjectId crane, out TabletopObjectId fall, out _);
            PlayerId me = t.Active;
            int myIndex = IndexOf(t.T.Turn.PlayerOrder, me);
            Assert.IsTrue(t.Land(me, crane).Succeeded, "the Crane landing");

            // The Crane drops the pawn on the Fall Trap: the same landing reveal, in the same turn.
            TrapFloorRevealFloorResult forced = t.Land(me, fall);
            Assert.IsTrue(forced.Succeeded, $"the forced landing reveals the Fall Trap ({forced.Error})");
            Assert.AreEqual(1, t.PendingFor(me, fall), "the Fall Trap waits on the player who was dropped on it");

            Assert.IsTrue(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(me), fall)).Succeeded, "its effect runs");
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound, t.T.Turn.GetPlayerState(me), "out");
            PlayerId next = t.T.Turn.PlayerOrder[(myIndex + 1) % t.T.Turn.PlayerOrder.Count];
            Assert.AreEqual(next, t.T.Turn.ActivePlayerId, "the turn passed once, to the next player");

            long revision = t.T.Match.Revision;
            Assert.IsFalse(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(me), fall)).Succeeded, "never twice");
            Assert.AreEqual(next, t.T.Turn.ActivePlayerId, "no second turn change");
            Assert.AreEqual(revision, t.T.Match.Revision);
        }

        [Test]
        public void Crane_ThenAFallTrapAlreadyTurnedOver_DoesNothing()
        {
            Table t = Dealt(out TabletopObjectId crane, out TabletopObjectId fall, out _);
            PlayerId first = t.Active;
            Assert.IsTrue(t.Land(first, fall).Succeeded);
            Assert.IsTrue(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(first), fall)).Succeeded, "the first player falls");

            PlayerId second = t.Active;
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(t.Land(second, crane).Succeeded, "the second player lands on the Crane");
            TrapFloorRevealFloorResult forced = t.Land(second, fall);
            Assert.IsFalse(forced.Succeeded);
            Assert.AreEqual(TrapFloorRevealFloorError.FloorAlreadyRevealed, forced.Error, "a turned-over Trap is not revealed again");
            Assert.AreEqual(0, t.PendingFor(second, fall), "nothing waits on the second player");
            Assert.IsFalse(t.Exec.ApplyTrap(t.T.Match, new TrapFloorApplyTrapCommand(t.T.Context(second), fall)).Succeeded, "and it does not act");
            Assert.AreEqual(TrapFloorCurrentFloorPlayerState.Active, t.T.Turn.GetPlayerState(second));
            Assert.AreEqual(second, t.T.Turn.ActivePlayerId, "still their turn");
        }

        [Test]
        public void Crane_ThenAnOrdinaryTile_RevealsIt_AndNobodyIsOut()
        {
            Table t = Dealt(out TabletopObjectId crane, out _, out TabletopObjectId plain);
            PlayerId me = t.Active;
            Assert.IsTrue(t.Land(me, crane).Succeeded);
            Assert.IsTrue(t.Land(me, plain).Succeeded, "the forced landing reveals the tile");
            Assert.AreEqual(0, t.PendingFor(me, plain), "no Trap waits there");
            foreach (PlayerId p in t.T.Players) Assert.AreEqual(TrapFloorCurrentFloorPlayerState.Active, t.T.Turn.GetPlayerState(p));
            Assert.AreEqual(me, t.T.Turn.ActivePlayerId);
        }

        private static int IndexOf(IReadOnlyList<PlayerId> order, PlayerId player)
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i] == player) return i;
            return -1;
        }
    }
}
