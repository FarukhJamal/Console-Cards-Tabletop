using System.Collections.Generic;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// COLLAPSE-ONCE (s143; replaces the s142 COLLAPSE-UNDO restriction). One logical floor turn resolves one collapse
    /// (COLLAPSE-3). The cartridge's own "collapse done" flag is cleared when the phase leaves the floor turn, and an Undo
    /// of End Floor Turn brings the phase back without it, which let a second tile collapse (live, s142). This ledger is
    /// kept outside the Undo history: it remembers, per floor-turn key (round, phase, seat index), how many holes the
    /// rules state had once that turn's collapse resolved. A collapse is refused while the rules state still holds those
    /// holes, so undoing End Floor Turn is harmless; undoing the collapse itself (where the level allows it) removes the
    /// hole, and the turn may collapse again. Pure, so it is unit tested.
    /// </summary>
    public sealed class TrapFloorFloorTurnLedger
    {
        private readonly Dictionary<long, int> holesAfterCollapse = new Dictionary<long, int>();

        /// <summary>The floor turn <paramref name="turnKey"/> resolved its collapse; the rules state now has <paramref name="holes"/> holes.</summary>
        public void RecordCollapse(long turnKey, int holes)
        {
            holesAfterCollapse[turnKey] = holes;
        }

        /// <summary>False when this floor turn already resolved a collapse that still stands.</summary>
        public bool MayCollapse(long turnKey, int holesNow)
        {
            return !holesAfterCollapse.TryGetValue(turnKey, out int recorded) || holesNow < recorded;
        }

        public void Clear() => holesAfterCollapse.Clear();

        /// <summary>Holes made by collapses (taken prizes leave holes too, but are not collapses).</summary>
        public static int CollapseHoles(TrapFloorCollapseState collapse)
        {
            if (collapse == null) return 0;
            int holes = 0;
            for (int i = 0; i < collapse.CollapsedFloors.Count; i++)
                if (!collapse.CollapsedFloors[i].IsTakenPrize) holes++;
            return holes;
        }
    }
}
