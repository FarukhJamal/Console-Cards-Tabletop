using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>REFUSAL-CORRECTION (s146): the reminder a computer showed for its own predicted collapse is taken down
    /// when the host's table does not hold that collapse.</summary>
    public sealed class CollapseHintTests
    {
        [Test]
        public void APredictedCollapseReminder_IsRemoved_WhenTheHostRefusedIt()
        {
            string predicted = TrapFloorCollapseHint.Text(4, 6); // this computer's own (refused) collapse
            // The host's table: its last resolved roll was (2,1), and that tile is a hole.
            Assert.IsFalse(TrapFloorCollapseHint.KeepAfterHostTable(predicted, true, 2, 1, true), "stale (4,6) reminder is taken down");
            Assert.IsTrue(TrapFloorCollapseHint.KeepAfterHostTable(TrapFloorCollapseHint.Text(2, 1), true, 2, 1, true), "the host's own collapse stays announced");
            Assert.IsFalse(TrapFloorCollapseHint.KeepAfterHostTable(predicted, true, 4, 6, false), "a roll whose tile is not a hole is not announced");
            Assert.IsFalse(TrapFloorCollapseHint.KeepAfterHostTable(predicted, false, 0, 0, false), "no roll on the host: nothing to announce");
            Assert.IsFalse(TrapFloorCollapseHint.KeepAfterHostTable(null, true, 2, 1, true));
        }
    }
}
