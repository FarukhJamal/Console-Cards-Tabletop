using ConsoleCards.Core.Domain;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// P8A eliminated-pawn fix (owner, 2026-11-13). When a loose piece is held still at its accepted place because it
    /// is out of play (Trap Floor: the pawn of a player eliminated on this floor). Only a pawn, only while it rests
    /// (an accepted Sleeping state), lies loose on the table and nobody is holding or moving it: a player who picks it
    /// up gets normal physics, and every other piece (active pawns, dice, cards) is never affected.
    /// </summary>
    public static class OutOfPlayRest
    {
        public static bool Wants(
            bool isPawn,
            bool outOfPlay,
            bool held,
            bool playerActionRunning,
            bool contained,
            PhysicalObjectState accepted)
        {
            return isPawn
                && outOfPlay
                && !held
                && !playerActionRunning
                && !contained
                && accepted != null
                && (accepted.Mode == PhysicalObjectMode.Sleeping || accepted.Mode == PhysicalObjectMode.SleepingUnresolved);
        }
    }
}
