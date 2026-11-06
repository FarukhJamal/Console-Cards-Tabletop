namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// REFUSAL-CORRECTION (s145/s146): the collapse reminder's text, and whether a reminder already on screen still
    /// matches the host's table after this computer took it (for example after the host refused a collapse that this
    /// computer had predicted). Pure, so it is unit tested.
    /// </summary>
    public static class TrapFloorCollapseHint
    {
        public static string Text(int column, int row) =>
            $"The floor dice rolled {column} and {row}: column {column}, row {row} is a hole now. Moves cannot cross holes.";

        /// <summary>True only when the shown reminder names the host's last roll and that roll's tile really is a hole.</summary>
        public static bool KeepAfterHostTable(string shownText, bool hasLastRoll, int column, int row, bool lastRollCollapsed)
        {
            if (string.IsNullOrEmpty(shownText) || !hasLastRoll || !lastRollCollapsed) return false;
            return shownText == Text(column, row);
        }
    }
}
