using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// P8A (owner, 2026-11-13: "targeted eliminated pawn physics fix"). Live finding (v2.3): a pawn whose player was
    /// eliminated kept its last accepted MOVING physical state. The guest's physics settled it and committed the rest,
    /// the host refused that (PlayerIsOut) and sent its whole table, which set the pawn moving again from the old
    /// state: an endless commit / refusal / whole-table loop, seen as a pawn moving and spinning on its own.
    /// <para>
    /// The elimination itself (TrapFloorTurnService.MarkPlayerEliminatedForCurrentRound, a replicated command) now
    /// puts that pawn to rest where its accepted state has it: same position and turn, no speed, no spin, Sleeping,
    /// the same controlling player. Every computer that runs or replays the elimination writes the same record, a
    /// whole table or a reconnect carries it, and a pawn already at rest is left exactly as it is (no new revision), so
    /// running it again changes nothing. A pawn held in a hand is put down too (its player is out). The table pose is
    /// not changed, and nothing about the elimination's visuals.
    /// </para>
    /// </summary>
    public static class TrapFloorEliminatedPawnRest
    {
        /// <summary>True when the pawn's accepted state was changed to rest; false when it already rested (or has none).</summary>
        public static bool Rest(MatchState matchState, TabletopObjectId pawnId)
        {
            if (matchState == null || pawnId.IsEmpty || !matchState.Pawns.TryGetValue(pawnId, out PawnState pawn)) return false;
            TabletopObjectState piece = pawn.BaseState;
            PhysicalObjectState physical = piece.PhysicalState;
            if (physical == null || !piece.ContainerId.IsEmpty) return false;
            if (IsAtRest(physical)) return false;
            piece.SetPhysicalState(new PhysicalObjectState(
                physical.Position,
                physical.Rotation,
                default,
                default,
                PhysicalObjectMode.Sleeping,
                physical.ControllingPlayerId));
            return true;
        }

        /// <summary>Sleeping with no speed and no spin.</summary>
        public static bool IsAtRest(PhysicalObjectState physical) =>
            physical != null
            && physical.Mode == PhysicalObjectMode.Sleeping
            && IsZero(physical.Velocity)
            && IsZero(physical.AngularVelocity);

        private static bool IsZero(PhysicalVector3 v) => v.X == 0f && v.Y == 0f && v.Z == 0f;
    }
}
