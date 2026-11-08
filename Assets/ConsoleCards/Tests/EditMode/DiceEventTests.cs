using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P7 (s158) DICE-EVENT: an accepted roll is a gameplay event even when the die ends showing the face it showed before,
    /// where it rested before; it resolves once with the host's result; a repeated throw before a rule read it gets the
    /// same result; a settle that was not a roll is never read. Unit.
    /// </summary>
    public sealed class DiceEventTests
    {
        private static readonly TabletopObjectId DieX = new TabletopObjectId(new Guid("d1ce0000-0000-4000-8000-000000000001"));

        [Test]
        public void RollGate_CountsTheRollItself_NotAChangeOfPose()
        {
            // Same face, same resting place: the roll was counted, so it is read.
            Assert.AreEqual(TrapFloorDieRollStep.Read, TrapFloorDiceRollGate.Next(true, PhysicalObjectMode.Sleeping));
            // No roll counted (physics settled it again, or a rebuilt table): never read, whatever the pose.
            Assert.AreEqual(TrapFloorDieRollStep.Wait, TrapFloorDiceRollGate.Next(false, PhysicalObjectMode.Sleeping));
            // Rolled but still moving: wait for it to rest.
            Assert.AreEqual(TrapFloorDieRollStep.Wait, TrapFloorDiceRollGate.Next(true, PhysicalObjectMode.Dynamic));
            Assert.AreEqual(TrapFloorDieRollStep.Wait, TrapFloorDiceRollGate.Next(true, PhysicalObjectMode.Held));
            // Rolled and cocked: ask again.
            Assert.AreEqual(TrapFloorDieRollStep.Cocked, TrapFloorDiceRollGate.Next(true, PhysicalObjectMode.SleepingUnresolved));
        }

        [Test]
        public void HostDice_SameTurnRepeatKeepsTheResult_ReadThenNewResult_NewTurnNewResult()
        {
            TrapFloorHostDice dice = new TrapFloorHostDice();
            int draws = 0;
            int[] sequence = { 3, 3, 5 }; // the second draw deliberately repeats the face the die already shows
            Func<int, int> roll = sides => sequence[draws++];

            int first = dice.Decide(DieX, 6, 100, null, roll);
            Assert.AreEqual(3, first);
            Assert.AreEqual(3, dice.Decide(DieX, 6, 100, null, roll), "thrown again before a rule read it: the same result");
            Assert.AreEqual(1, draws, "a repeated throw draws nothing new");

            dice.Consumed(DieX); // the collapse read it
            Assert.AreEqual(3, dice.Decide(DieX, 6, 100, null, roll), "a fresh draw may equal the face already shown");
            Assert.AreEqual(2, draws);

            Assert.AreEqual(5, dice.Decide(DieX, 6, 101, null, roll), "another turn draws again");
            Assert.AreEqual(3, draws);
        }

        [Test]
        public void HostDice_RerollAskedByTheRules_DrawsAgain()
        {
            TrapFloorHostDice dice = new TrapFloorHostDice();
            int draws = 0;
            int[] sequence = { 6, 2 };
            int first = dice.Decide(DieX, 6, 7, v => v > 5, sides => sequence[draws++]);
            Assert.AreEqual(6, first);
            Assert.AreEqual(2, dice.Decide(DieX, 6, 7, v => v > 5, sides => sequence[draws++]), "a value beyond a smaller floor is rolled again");
        }
    }
}
