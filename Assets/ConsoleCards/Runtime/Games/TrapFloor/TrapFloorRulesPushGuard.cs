using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// AUTH-RULES-2 (owner, 2026-11-06: "remove the remaining client rules-state upload privileges"): what of a joined
    /// computer's pushed Trap Floor rules state the host takes. <b>Nothing.</b> Every rules step now reaches the host as a
    /// command it replays against its own table (Claim Key, Attempt Escape, Take / Collect Prize, Begin / Resolve
    /// Collapse, Reveal, Apply Trap, Launch Trap, Blind direction, Retrigger, Use Ability, Clear guidance, Eliminate) or
    /// through the host-run End Turn, so the host's own rules state is already the result. A push that differs is
    /// reported (it means a modified client, or a step not yet moved to a command) and the host's values are kept; the
    /// sender is then set right. The activity log is display only: the host's own log stands, a different pushed log is
    /// not counted as a refusal. Pure, so it is unit tested.
    /// </summary>
    public static class TrapFloorRulesPushGuard
    {
        /// <returns>The host's own snapshot (always). <paramref name="refused"/> lists what the push tried to change.</returns>
        public static TrapFloorSessionStateSnapshot Merge(
            TrapFloorSessionStateSnapshot hostSnapshot,
            TrapFloorSessionStateSnapshot pushedSnapshot,
            PlayerId sender,
            List<string> refused)
        {
            if (hostSnapshot == null) throw new ArgumentNullException(nameof(hostSnapshot));
            if (pushedSnapshot == null)
            {
                Note(refused, "no rules state");
                return hostSnapshot;
            }

            if (pushedSnapshot.MatchId != hostSnapshot.MatchId)
            {
                Note(refused, "rules state of another match");
                return hostSnapshot;
            }

            TrapFloorSessionState host = hostSnapshot.Restore();
            TrapFloorSessionState pushed = pushedSnapshot.Restore();
            CompareObjective(host.Objective, pushed.Objective, refused);
            CompareCollapse(host.Collapse, pushed.Collapse, refused);
            CompareEliminations(host.Turn, pushed.Turn, refused);
            CompareTurn(host.Turn, pushed.Turn, refused);
            CompareAbility(host.AbilityResolution, pushed.AbilityResolution, refused);
            return hostSnapshot;
        }

        // ---------- host only ----------

        private static void CompareObjective(TrapFloorObjectiveState host, TrapFloorObjectiveState pushed, List<string> refused)
        {
            if (host.CollectedKeyCount != pushed.CollectedKeyCount || !SameKeys(host, pushed))
                Note(refused, $"Keys ({pushed.CollectedKeyCount} pushed, {host.CollectedKeyCount} on the host)");
            if (!SameSet(host.EscapedPlayerIds, pushed.EscapedPlayerIds))
                Note(refused, $"escapes ({pushed.EscapedPlayerCount} pushed, {host.EscapedPlayerCount} on the host)");
            if (host.IsWon != pushed.IsWon || host.WinningPlayerId != pushed.WinningPlayerId || host.ExitFloorCardId != pushed.ExitFloorCardId)
                Note(refused, "floor won / winner");
            if (host.RequiredKeyCount != pushed.RequiredKeyCount || host.ModeBehavior != pushed.ModeBehavior)
                Note(refused, "Keys needed / mode");
        }

        private static bool SameKeys(TrapFloorObjectiveState host, TrapFloorObjectiveState pushed)
        {
            IReadOnlyList<TrapFloorCollectedKeyState> a = host.CollectedKeys;
            IReadOnlyList<TrapFloorCollectedKeyState> b = pushed.CollectedKeys;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < b.Count && !found; j++)
                {
                    found = a[i].FloorCardId == b[j].FloorCardId
                        && a[i].ClaimedByPlayerId == b[j].ClaimedByPlayerId
                        && a[i].IsUsed == b[j].IsUsed;
                }

                if (!found) return false;
            }

            return true;
        }

        private static void CompareCollapse(TrapFloorCollapseState host, TrapFloorCollapseState pushed, List<string> refused)
        {
            bool same = host.CollapsedFloorCount == pushed.CollapsedFloorCount
                && host.IsCollapsePending == pushed.IsCollapsePending
                && host.PendingActorPlayerId == pushed.PendingActorPlayerId
                && host.TotalFloorCount == pushed.TotalFloorCount;
            for (int i = 0; same && i < host.CollapsedFloors.Count; i++)
                same = pushed.IsCollapsed(host.CollapsedFloors[i].FloorCardId);
            if (!same)
                Note(refused, $"collapse ({pushed.CollapsedFloorCount} tiles pushed, {host.CollapsedFloorCount} on the host)");
        }

        private static void CompareTurn(TrapFloorTurnState host, TrapFloorTurnState pushed, List<string> refused)
        {
            TrapFloorTurnPosition a = host.CapturePosition();
            TrapFloorTurnPosition b = pushed.CapturePosition();
            bool same = a.Round == b.Round
                && a.Phase == b.Phase
                && a.ActivePlayerIndex == b.ActivePlayerIndex
                && a.FloorOperatorIndex == b.FloorOperatorIndex
                && a.IsCurrentFloorFailed == b.IsCurrentFloorFailed
                && a.FloorResumeIndex == b.FloorResumeIndex
                && SameOrder(host.PlayerOrder, pushed.PlayerOrder);
            if (!same)
                Note(refused, $"turn (pushed round {b.Round} {b.Phase} #{b.ActivePlayerIndex}, host round {a.Round} {a.Phase} #{a.ActivePlayerIndex})");
        }

        // ---------- eliminations and the per-player records ----------

        private static void CompareEliminations(TrapFloorTurnState host, TrapFloorTurnState pushed, List<string> refused)
        {
            HashSet<PlayerId> before = new HashSet<PlayerId>(host.CopyEliminatedPlayerIds());
            PlayerId[] pushedOut = pushed.CopyEliminatedPlayerIds();
            HashSet<PlayerId> pushedSet = new HashSet<PlayerId>(pushedOut);
            foreach (PlayerId player in before)
            {
                if (!pushedSet.Contains(player)) Note(refused, $"bringing {Short(player)} back in");
            }

            for (int i = 0; i < pushedOut.Length; i++)
            {
                if (!before.Contains(pushedOut[i])) Note(refused, $"elimination of {Short(pushedOut[i])}");
            }
        }

        private static void CompareAbility(TrapFloorAbilityResolutionState host, TrapFloorAbilityResolutionState pushed, List<string> refused)
        {
            TrapFloorTrapResolutionRecord[] a = host.CopyTrapRecords();
            TrapFloorTrapResolutionRecord[] b = pushed.CopyTrapRecords();
            bool same = a.Length == b.Length;
            for (int i = 0; same && i < a.Length; i++)
            {
                same = a[i].FloorCardId == b[i].FloorCardId
                    && a[i].AffectedPlayerId == b[i].AffectedPlayerId
                    && a[i].RevealedRevision == b[i].RevealedRevision
                    && a[i].Disposition == b[i].Disposition;
            }

            if (!same) Note(refused, $"Trap outcomes ({b.Length} pushed, {a.Length} on the host)");

            if (!SameSet(host.CopyAbilityActivations(), pushed.CopyAbilityActivations()))
                Note(refused, "Ability uses");

            TrapFloorDodgeAssistanceState hostDodge = host.CopyDodgeAssistance();
            TrapFloorDodgeAssistanceState pushedDodge = pushed.CopyDodgeAssistance();
            TrapFloorRushAssistanceState hostRush = host.CopyRushAssistance();
            TrapFloorRushAssistanceState pushedRush = pushed.CopyRushAssistance();
            if (!SameGuide(hostDodge?.PlayerId, pushedDodge?.PlayerId, hostDodge?.TargetFloorCardIds, pushedDodge?.TargetFloorCardIds)
                || !SameGuide(hostRush?.PlayerId, pushedRush?.PlayerId, hostRush?.TargetFloorCardIds, pushedRush?.TargetFloorCardIds))
                Note(refused, "move guidance (lit tiles)");

            if (!SameStatuses(host.CopyBlindStatuses(), pushed.CopyBlindStatuses(), s => s.PlayerId, s => s.AppliesInRound, s => s.DirectionRoll ?? 0))
                Note(refused, "Blind statuses");
            if (!SameStatuses(host.CopySlowStatuses(), pushed.CopySlowStatuses(), s => s.PlayerId, s => s.AppliesInRound, s => s.MovementModifier))
                Note(refused, "Slow statuses");
            if (!SameStatuses(host.CopyStickyStatuses(), pushed.CopyStickyStatuses(), s => s.PlayerId, s => s.AppliesInRound, s => 0))
                Note(refused, "Sticky statuses");
        }

        private static bool SameStatuses<T>(T[] a, T[] b, Func<T, PlayerId> player, Func<T, int> round, Func<T, int> detail)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < b.Length && !found; j++)
                    found = player(a[i]) == player(b[j]) && round(a[i]) == round(b[j]) && detail(a[i]) == detail(b[j]);
                if (!found) return false;
            }

            return true;
        }

        private static bool SameGuide(PlayerId? a, PlayerId? b, IReadOnlyList<TabletopObjectId> ta, IReadOnlyList<TabletopObjectId> tb)
        {
            if (a.HasValue != b.HasValue) return false;
            if (!a.HasValue) return true;
            if (a.Value != b.Value || ta.Count != tb.Count) return false;
            for (int i = 0; i < ta.Count; i++)
            {
                if (ta[i] != tb[i]) return false;
            }

            return true;
        }

        private static bool SameSet(TrapFloorAbilityActivationRecord[] a, TrapFloorAbilityActivationRecord[] b)
        {
            if (a.Length != b.Length) return false;
            HashSet<TrapFloorAbilityActivationRecord> set = new HashSet<TrapFloorAbilityActivationRecord>(a);
            for (int i = 0; i < b.Length; i++)
            {
                if (!set.Contains(b[i])) return false;
            }

            return true;
        }

        // ---------- helpers ----------

        private static bool SameSet(IReadOnlyList<PlayerId> a, IReadOnlyList<PlayerId> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < b.Count && !found; j++) found = a[i] == b[j];
                if (!found) return false;
            }

            return true;
        }

        private static bool SameOrder(IReadOnlyList<PlayerId> a, IReadOnlyList<PlayerId> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }

        private static void Note(List<string> notes, string text)
        {
            if (notes != null && notes.Count < 8) notes.Add(text);
        }

        // The first characters of a player's id: enough to find in a log.
        private static string Short(PlayerId player) => player.Value.ToString("N").Substring(0, 8);
    }
}
