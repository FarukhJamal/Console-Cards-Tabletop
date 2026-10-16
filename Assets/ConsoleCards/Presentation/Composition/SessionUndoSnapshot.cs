using System;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>Authoritative session data only; contains no Unity object or Presentation reference.</summary>
    internal sealed class SessionUndoSnapshot
    {
        private SessionUndoSnapshot(
            GameTemplateInitialSnapshot match,
            TrapFloorSessionStateSnapshot trapFloor,
            PendingControllerPurchaseStateSnapshot pendingControllerPurchase)
        {
            Match = match ?? throw new ArgumentNullException(nameof(match));
            TrapFloor = trapFloor;
            PendingControllerPurchase = pendingControllerPurchase;
        }

        public GameTemplateInitialSnapshot Match { get; }
        public TrapFloorSessionStateSnapshot TrapFloor { get; }
        public PendingControllerPurchaseStateSnapshot PendingControllerPurchase { get; }

        public static SessionUndoSnapshot Capture(
            MatchState match,
            TrapFloorActivityFeedState activity,
            TrapFloorObjectiveState objective,
            TrapFloorCollapseState collapse,
            TrapFloorTurnState turn,
            TrapFloorAbilityResolutionState abilityResolution,
            PendingControllerPurchaseState pendingControllerPurchase)
        {
            TrapFloorSessionStateSnapshot trapFloor = null;
            if (activity != null
                || objective != null
                || collapse != null
                || turn != null
                || abilityResolution != null)
            {
                if (activity == null
                    || objective == null
                    || collapse == null
                    || turn == null
                    || abilityResolution == null)
                    throw new InvalidOperationException("Trap Floor Undo capture requires its complete Match-scoped state.");
                trapFloor = TrapFloorSessionStateSnapshot.Capture(
                    activity,
                    objective,
                    collapse,
                    turn,
                    abilityResolution);
            }

            PendingControllerPurchaseStateSnapshot purchase = pendingControllerPurchase == null
                ? null
                : PendingControllerPurchaseStateSnapshot.Capture(pendingControllerPurchase);
            return new SessionUndoSnapshot(
                GameTemplateInitialSnapshot.Capture(match),
                trapFloor,
                purchase);
        }
    }
}
