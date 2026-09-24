using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>Unity-free copy of the mutable Match-scoped Stage-03 assistance state.</summary>
    public sealed class TrapFloorSessionStateSnapshot
    {
        private readonly TrapFloorActivityEntry[] activityEntries;
        private readonly TrapFloorCollectedKeyState[] collectedKeys;
        private readonly TrapFloorCollapsedFloorState[] collapsedFloors;
        private readonly int requiredKeyCount;
        private readonly ModeBehavior modeBehavior;
        private readonly PlayerId[] escapedPlayerIds;
        private readonly int totalFloorCount;
        private readonly bool isWon;
        private readonly PlayerId winningPlayerId;
        private readonly TabletopObjectId exitFloorCardId;
        private readonly bool collapsePending;
        private readonly PlayerId pendingCollapseActor;
        private readonly TrapFloorCollapseRollState lastRoll;
        private readonly PhysicalObjectState lastXAxisState;
        private readonly PhysicalObjectState lastYAxisState;
        private readonly PlayerId[] turnPlayerOrder;
        private readonly int turnRound;
        private readonly TrapFloorTurnPhase turnPhase;
        private readonly int activeTurnPlayerIndex;
        private readonly int floorOperatorIndex;
        private readonly PlayerId[] eliminatedTurnPlayerIds;
        private readonly bool isCurrentFloorFailed;
        private readonly TrapFloorTrapResolutionRecord[] trapResolutionRecords;
        private readonly TrapFloorAbilityActivationRecord[] abilityActivations;
        private readonly TrapFloorDodgeAssistanceState dodgeAssistance;

        private TrapFloorSessionStateSnapshot(
            TrapFloorActivityFeedState activity,
            TrapFloorObjectiveState objective,
            TrapFloorCollapseState collapse,
            TrapFloorTurnState turn,
            TrapFloorAbilityResolutionState abilityResolution)
        {
            MatchId = activity.MatchId;
            activityEntries = Copy(activity.Entries);
            collectedKeys = Copy(objective.CollectedKeys);
            collapsedFloors = Copy(collapse.CollapsedFloors);
            requiredKeyCount = objective.RequiredKeyCount;
            modeBehavior = objective.ModeBehavior;
            escapedPlayerIds = Copy(objective.EscapedPlayerIds);
            totalFloorCount = collapse.TotalFloorCount;
            isWon = objective.IsWon;
            winningPlayerId = objective.WinningPlayerId;
            exitFloorCardId = objective.ExitFloorCardId;
            collapsePending = collapse.IsCollapsePending;
            pendingCollapseActor = collapse.PendingActorPlayerId;
            lastRoll = collapse.LastRoll;
            lastXAxisState = collapse.LastResolvedXAxisPhysicalState;
            lastYAxisState = collapse.LastResolvedYAxisPhysicalState;
            turnPlayerOrder = Copy(turn.PlayerOrder);
            TrapFloorTurnPosition turnPosition = turn.CapturePosition();
            turnRound = turnPosition.Round;
            turnPhase = turnPosition.Phase;
            activeTurnPlayerIndex = turnPosition.ActivePlayerIndex;
            floorOperatorIndex = turnPosition.FloorOperatorIndex;
            eliminatedTurnPlayerIds = turn.CopyEliminatedPlayerIds();
            isCurrentFloorFailed = turn.IsCurrentFloorFailed;
            trapResolutionRecords = abilityResolution.CopyTrapRecords();
            abilityActivations = abilityResolution.CopyAbilityActivations();
            dodgeAssistance = abilityResolution.CopyDodgeAssistance();
        }

        public MatchId MatchId { get; }

        public static TrapFloorSessionStateSnapshot Capture(
            TrapFloorActivityFeedState activity,
            TrapFloorObjectiveState objective,
            TrapFloorCollapseState collapse,
            TrapFloorTurnState turn,
            TrapFloorAbilityResolutionState abilityResolution)
        {
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            if (objective == null) throw new ArgumentNullException(nameof(objective));
            if (collapse == null) throw new ArgumentNullException(nameof(collapse));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            if (abilityResolution == null) throw new ArgumentNullException(nameof(abilityResolution));
            if (activity.MatchId != objective.MatchId
                || activity.MatchId != collapse.MatchId
                || activity.MatchId != turn.MatchId
                || activity.MatchId != abilityResolution.MatchId)
                throw new ArgumentException("Trap Floor snapshot state must belong to one Match.");
            return new TrapFloorSessionStateSnapshot(activity, objective, collapse, turn, abilityResolution);
        }

        public TrapFloorSessionState Restore()
        {
            TrapFloorActivityFeedState activity = new TrapFloorActivityFeedState(MatchId);
            activity.RestoreEntries(activityEntries);
            TrapFloorObjectiveState objective = new TrapFloorObjectiveState(
                MatchId,
                requiredKeyCount,
                modeBehavior);
            objective.Restore(
                collectedKeys,
                escapedPlayerIds,
                isWon,
                winningPlayerId,
                exitFloorCardId);
            TrapFloorCollapseState collapse = new TrapFloorCollapseState(MatchId, totalFloorCount);
            collapse.Restore(
                collapsedFloors,
                collapsePending,
                pendingCollapseActor,
                lastRoll,
                lastXAxisState,
                lastYAxisState);
            TrapFloorTurnState turn = new TrapFloorTurnState(
                MatchId,
                turnPlayerOrder,
                turnRound,
                turnPhase,
                activeTurnPlayerIndex,
                floorOperatorIndex,
                eliminatedTurnPlayerIds,
                isCurrentFloorFailed);
            TrapFloorAbilityResolutionState abilityResolution =
                new TrapFloorAbilityResolutionState(MatchId);
            abilityResolution.Restore(trapResolutionRecords, abilityActivations, dodgeAssistance);
            return new TrapFloorSessionState(activity, objective, collapse, turn, abilityResolution);
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
            TrapFloorCollapseState collapse,
            TrapFloorTurnState turn,
            TrapFloorAbilityResolutionState abilityResolution)
        {
            Activity = activity;
            Objective = objective;
            Collapse = collapse;
            Turn = turn;
            AbilityResolution = abilityResolution;
        }

        public TrapFloorActivityFeedState Activity { get; }
        public TrapFloorObjectiveState Objective { get; }
        public TrapFloorCollapseState Collapse { get; }
        public TrapFloorTurnState Turn { get; }
        public TrapFloorAbilityResolutionState AbilityResolution { get; }
    }
}
