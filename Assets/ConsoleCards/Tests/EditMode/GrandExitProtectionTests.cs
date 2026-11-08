using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P7 (s160), owner rule 2026-11-08 "Grand Exit protection and optional Secret Exit":
    /// - every floor holds a Grand Exit;
    /// - Floorfall never collapses it: a roll on it is rolled again, as a roll on a hole already is;
    /// - a Secret Exit is optional and may collapse.
    /// Unit, on the game's own authored content and the host's collapse use case.
    /// </summary>
    public sealed class GrandExitProtectionTests
    {
        // The floors the cartridge deals (TrapFloorCartridge.RecipeFor, owner 2026-11-04): 6×6 3 Keys Grand + Secret;
        // 5×5 2 Keys Grand (+ Secret in Free for all); 4×4 2 Keys Grand.
        private static TrapFloorFloorRecipe Recipe(int floorIndex, bool freeForAll) =>
            new TrapFloorFloorRecipe(
                3, floorIndex == 0 ? 3 : 2, floorIndex == 0 || (floorIndex == 1 && freeForAll), 1,
                1d, 1d, 1d, null, freeForAll ? "lasso" : null);

        private static void CountExits(TrapFloorTestTable t, out int grand, out int secret, out TrapFloorCoordinate grandAt,
            out TrapFloorCoordinate secretAt)
        {
            grand = 0;
            secret = 0;
            grandAt = default;
            secretAt = default;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in t.Template.FloorCardIds)
            {
                Assert.IsTrue(t.Template.TryGetFloorCardState(t.Match, pair.Value, out TrapFloorFloorCardState floor));
                if (floor.Content.Category != TrapFloorFloorContentCategory.SecretExit) continue;
                if (TrapFloorObjectiveUseCase.IsGrandExit(floor.Content)) { grand++; grandAt = pair.Key; }
                else { secret++; secretAt = pair.Key; }
            }
        }

        [Test]
        public void EveryFloorOfEveryMode_HasExactlyOneGrandExit_SecretOnlyWhereTheModeHasOne()
        {
            var game = TrapFloorTestTable.LoadGame();
            foreach (bool freeForAll in new[] { true, false })
            {
                for (int floor = 0; floor < 3; floor++)
                {
                    TrapFloorFloorRecipe recipe = Recipe(floor, freeForAll);
                    for (int seed = 1; seed <= 25; seed++)
                    {
                        TrapFloorTestTable t = TrapFloorTestTable.Create(3, seed, game, floor, recipe);
                        Assert.AreEqual(6 - floor, t.Template.GridColumns, "floor sizes 6×6, 5×5, 4×4");
                        CountExits(t, out int grand, out int secret, out _, out _);
                        string where = $"floor {floor + 1}, {(freeForAll ? "Free for all" : "Team")}, seed {seed}";
                        Assert.AreEqual(1, grand, $"the Grand Exit is required ({where})");
                        if (recipe.SecretExit) Assert.GreaterOrEqual(secret, 1, $"this floor has its Secret Exit ({where})");
                        else Assert.AreEqual(0, secret, $"no Secret Exit on this floor is valid ({where})");
                    }
                }
            }
        }

        // ---------- Floorfall on the host's collapse use case ----------

        private sealed class Floorfall
        {
            public TrapFloorTestTable T;
            public TrapFloorCollapseState State;
            public TrapFloorCollapseUseCase UseCase;
            public PlayerId Actor;
            private int moves;

            public static Floorfall Create(int floor, bool protects = true)
            {
                Floorfall f = new Floorfall { T = TrapFloorTestTable.Create(3, 11, null, floor, Recipe(floor, true)) };
                f.State = new TrapFloorCollapseState(f.T.Match.Id, f.T.Template.FloorCardIds.Count);
                f.UseCase = new TrapFloorCollapseUseCase(f.T.Template, f.State, f.T.Feed) { ProtectsGrandExit = protects };
                f.Actor = f.T.Players[0];
                return f;
            }

            private DieState Die(TabletopObjectId id)
            {
                Assert.IsTrue(T.Match.Dice.TryGetValue(id, out DieState die), "the floor dice are on the table");
                return die;
            }

            // A real throw as the host records it: a new resting pose and the host's value for each floor die.
            private void Throw(int x, int y)
            {
                moves++;
                foreach ((TabletopObjectId id, int value) in new[] { (T.Template.FloorfallXAxisDieId, x), (T.Template.FloorfallYAxisDieId, y) })
                {
                    DieState die = Die(id);
                    die.SetAcceptedRoll(new DieRoll(die.SideCount, value));
                    die.BaseState.SetPhysicalState(new PhysicalObjectState(
                        new PhysicalVector3(moves * 0.01f, 0f, 0f), new PhysicalRotation(0f, 0f, 0f, 1f),
                        new PhysicalVector3(0f, 0f, 0f), new PhysicalVector3(0f, 0f, 0f), PhysicalObjectMode.Sleeping, Actor));
                }
            }

            public void Begin()
            {
                // The floor dice lie on the table (not held) when the floor turn begins its collapse.
                foreach (TabletopObjectId id in new[] { T.Template.FloorfallXAxisDieId, T.Template.FloorfallYAxisDieId })
                {
                    DieState die = Die(id);
                    if (die.BaseState.PhysicalState != null) continue;
                    moves++;
                    die.BaseState.SetPhysicalState(new PhysicalObjectState(
                        new PhysicalVector3(moves * 0.01f, 0f, 0f), new PhysicalRotation(0f, 0f, 0f, 1f),
                        new PhysicalVector3(0f, 0f, 0f), new PhysicalVector3(0f, 0f, 0f), PhysicalObjectMode.Sleeping, Actor));
                }

                TrapFloorCollapseResult begun = UseCase.Begin(T.Match, new TrapFloorBeginCollapseCommand(T.Context(Actor)));
                Assert.IsTrue(begun.Succeeded, $"the collapse begins ({begun.Error})");
            }

            public TrapFloorCollapseResult Roll(TrapFloorCoordinate at)
            {
                Throw(at.X, at.Y);
                return UseCase.ResolveSettled(T.Match, new TrapFloorResolveCollapseCommand(T.Context(Actor)));
            }
        }

        [Test]
        public void Floorfall_OnTheGrandExit_IsRolledAgain_TheGrandExitStays_AnotherTileThenCollapses()
        {
            foreach (int floor in new[] { 0, 1, 2 })
            {
                Floorfall f = Floorfall.Create(floor);
                CountExits(f.T, out _, out _, out TrapFloorCoordinate grandAt, out _);
                Assert.IsTrue(f.T.Template.TryGetFloorCardId(grandAt, out TabletopObjectId grandId));
                f.Begin();

                for (int attempt = 0; attempt < 10; attempt++) // the dice may land on it again and again
                {
                    TrapFloorCollapseResult onGrand = f.Roll(grandAt);
                    Assert.IsTrue(onGrand.Succeeded, $"a roll on the Grand Exit is a roll, not an error ({onGrand.Error})");
                    Assert.IsTrue(onGrand.RerollRequired, "the dice are thrown again");
                    Assert.IsNull(onGrand.CollapsedFloor, "nothing collapsed");
                    Assert.IsFalse(f.State.IsCollapsed(grandId), "the Grand Exit never collapses");
                    Assert.IsTrue(f.State.IsCollapsePending, "the collapse is not used up by the protected tile");
                    Assert.AreEqual(0, f.State.CollapsedFloorCount);
                }

                TrapFloorCoordinate other = grandAt.X == 1 ? new TrapFloorCoordinate(2, grandAt.Y) : new TrapFloorCoordinate(1, grandAt.Y);
                Assert.IsTrue(f.T.Template.TryGetFloorCardId(other, out TabletopObjectId otherId));
                TrapFloorCollapseResult collapsed = f.Roll(other);
                Assert.IsTrue(collapsed.Succeeded && !collapsed.RerollRequired, $"floor {floor + 1}: another tile collapses");
                Assert.IsTrue(f.State.IsCollapsed(otherId));
                Assert.IsFalse(f.State.IsCollapsePending, "one collapse resolved");
                Assert.IsFalse(f.State.IsCollapsed(grandId));
            }
        }

        [Test]
        public void Floorfall_OnTheSecretExit_CollapsesIt()
        {
            Floorfall f = Floorfall.Create(0);
            CountExits(f.T, out _, out int secret, out _, out TrapFloorCoordinate secretAt);
            Assert.GreaterOrEqual(secret, 1, "the 6×6 floor has a Secret Exit");
            Assert.IsTrue(f.T.Template.TryGetFloorCardId(secretAt, out TabletopObjectId secretId));
            f.Begin();
            TrapFloorCollapseResult result = f.Roll(secretAt);
            Assert.IsTrue(result.Succeeded && !result.RerollRequired, "the Secret Exit is not protected");
            Assert.IsTrue(f.State.IsCollapsed(secretId));
        }

        [Test]
        public void Floorfall_WhenOnlyTheGrandExitIsLeft_HasNothingLeftToCollapse()
        {
            Floorfall f = Floorfall.Create(2);
            CountExits(f.T, out _, out _, out TrapFloorCoordinate grandAt, out _);
            Assert.IsFalse(f.UseCase.NothingLeftToCollapse);
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in f.T.Template.FloorCardIds)
            {
                if (pair.Key == grandAt) continue;
                f.Begin();
                TrapFloorCollapseResult r = f.Roll(pair.Key);
                Assert.IsTrue(r.Succeeded && !r.RerollRequired, $"{pair.Key} collapses");
            }

            Assert.AreEqual(1, f.State.UsableFloorCount, "only the Grand Exit stands");
            Assert.IsTrue(f.UseCase.NothingLeftToCollapse, "no floor turn owes a collapse that could never resolve");
        }
    }
}
