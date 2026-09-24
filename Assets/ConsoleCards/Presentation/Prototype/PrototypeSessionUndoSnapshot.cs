using System;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.GameTemplates;
using ConsoleCards.Games.TrapFloor;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>Authoritative session data only; contains no Unity object or Presentation reference.</summary>
    internal sealed class PrototypeSessionUndoSnapshot
    {
        private PrototypeSessionUndoSnapshot(
            GameTemplateInitialSnapshot match,
            TrapFloorSessionStateSnapshot trapFloor)
        {
            Match = match ?? throw new ArgumentNullException(nameof(match));
            TrapFloor = trapFloor;
        }

        public GameTemplateInitialSnapshot Match { get; }
        public TrapFloorSessionStateSnapshot TrapFloor { get; }

        public static PrototypeSessionUndoSnapshot Capture(
            MatchState match,
            TrapFloorActivityFeedState activity,
            TrapFloorObjectiveState objective,
            TrapFloorCollapseState collapse,
            TrapFloorTurnState turn,
            TrapFloorAbilityResolutionState abilityResolution)
        {
            TrapFloorSessionStateSnapshot trapFloor = null;
            if (activity != null || objective != null || collapse != null || turn != null || abilityResolution != null)
            {
                if (activity == null || objective == null || collapse == null || turn == null || abilityResolution == null)
                    throw new InvalidOperationException("Trap Floor Undo capture requires its complete Match-scoped state.");
                trapFloor = TrapFloorSessionStateSnapshot.Capture(
                    activity,
                    objective,
                    collapse,
                    turn,
                    abilityResolution);
            }

            return new PrototypeSessionUndoSnapshot(GameTemplateInitialSnapshot.Capture(match), trapFloor);
        }
    }
}
