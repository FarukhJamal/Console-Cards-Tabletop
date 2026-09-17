using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>Unity-free copy of the mutable Match-scoped Stage-03 assistance state.</summary>
    public sealed class TrapFloorSessionStateSnapshot
    {
        private readonly TrapFloorActivityEntry[] activityEntries;
        private readonly TrapFloorCollectedKeyState[] collectedKeys;
        private readonly TrapFloorCollapsedFloorState[] collapsedFloors;
        private readonly int requiredKeyCount;
        private readonly int totalFloorCount;
        private readonly bool isWon;
        private readonly PlayerId winningPlayerId;
        private readonly TabletopObjectId exitFloorCardId;
        private readonly bool collapsePending;
        private readonly PlayerId pendingCollapseActor;
        private readonly TrapFloorCollapseRollState lastRoll;
        private readonly PhysicalObjectState lastXAxisState;
        private readonly PhysicalObjectState lastYAxisState;

        private TrapFloorSessionStateSnapshot(
            TrapFloorActivityFeedState activity,
            TrapFloorObjectiveState objective,
            TrapFloorCollapseState collapse)
        {
            MatchId = activity.MatchId;
            activityEntries = Copy(activity.Entries);
            collectedKeys = Copy(objective.CollectedKeys);
            collapsedFloors = Copy(collapse.CollapsedFloors);
            requiredKeyCount = objective.RequiredKeyCount;
            totalFloorCount = collapse.TotalFloorCount;
            isWon = objective.IsWon;
            winningPlayerId = objective.WinningPlayerId;
            exitFloorCardId = objective.ExitFloorCardId;
            collapsePending = collapse.IsCollapsePending;
            pendingCollapseActor = collapse.PendingActorPlayerId;
            lastRoll = collapse.LastRoll;
            lastXAxisState = collapse.LastResolvedXAxisPhysicalState;
            lastYAxisState = collapse.LastResolvedYAxisPhysicalState;
        }

        public MatchId MatchId { get; }

        public static TrapFloorSessionStateSnapshot Capture(
            TrapFloorActivityFeedState activity,
            TrapFloorObjectiveState objective,
            TrapFloorCollapseState collapse)
        {
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            if (objective == null) throw new ArgumentNullException(nameof(objective));
            if (collapse == null) throw new ArgumentNullException(nameof(collapse));
            if (activity.MatchId != objective.MatchId || activity.MatchId != collapse.MatchId)
                throw new ArgumentException("Trap Floor snapshot state must belong to one Match.");
            return new TrapFloorSessionStateSnapshot(activity, objective, collapse);
        }

        public TrapFloorSessionState Restore()
        {
            TrapFloorActivityFeedState activity = new TrapFloorActivityFeedState(MatchId);
            activity.RestoreEntries(activityEntries);
            TrapFloorObjectiveState objective = new TrapFloorObjectiveState(MatchId, requiredKeyCount);
            objective.Restore(collectedKeys, isWon, winningPlayerId, exitFloorCardId);
            TrapFloorCollapseState collapse = new TrapFloorCollapseState(MatchId, totalFloorCount);
            collapse.Restore(
                collapsedFloors,
                collapsePending,
                pendingCollapseActor,
                lastRoll,
                lastXAxisState,
                lastYAxisState);
            return new TrapFloorSessionState(activity, objective, collapse);
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            T[] copy = new T[source.Count];
            for (int i = 0; i < source.Count; i++) copy[i] = source[i];
            return copy;
        }
    }

    public sealed class TrapFloorSessionState
    {
        internal TrapFloorSessionState(
            TrapFloorActivityFeedState activity,
            TrapFloorObjectiveState objective,
            TrapFloorCollapseState collapse)
        {
            Activity = activity;
            Objective = objective;
            Collapse = collapse;
        }

        public TrapFloorActivityFeedState Activity { get; }
        public TrapFloorObjectiveState Objective { get; }
        public TrapFloorCollapseState Collapse { get; }
    }
}
