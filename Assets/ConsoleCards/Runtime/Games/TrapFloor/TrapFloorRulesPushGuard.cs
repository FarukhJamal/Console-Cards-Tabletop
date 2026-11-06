using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// AUTH-RULES (2026-11-06): facts the host reads from its own table to judge a joined computer's rules-state change.
    /// Implemented by the game on the host; never filled from what the sender wrote.
    /// </summary>
    public interface ITrapFloorHostEvidence
    {
        /// <summary>On the host's table the player's pawn stands on a collapsed tile, an empty cell, or off the floor.</summary>
        bool StandsOnHoleOrOffFloor(PlayerId player);

        /// <summary>The "stuck without a Key" rule holds for the player on the host's table (no Key of theirs, none left).</summary>
        bool IsStuckWithoutKey(PlayerId player);

        /// <summary>The host's derived turn setting (Impossible after an escape: a floor turn after each player).</summary>
        bool FloorTurnAfterEachPlayer { get; }
    }

    /// <summary>
    /// AUTH-RULES (owner, 2026-11-06: "a guest must not directly overwrite authoritative rules state through whole-table
    /// uploads"): what of a joined computer's pushed Trap Floor rules state the host takes. Pure, so it is unit tested.
    /// <list type="bullet">
    /// <item><b>Host only (the pushed value is never taken):</b> Keys, escapes, floor won, the winner and the exit; collapsed
    /// tiles, the collapse in progress and its roll; the turn order, round, phase, turn owner, floor operator and floor
    /// failure. Each of these changes on the host only through a command it checked and ran (Claim Key, Attempt Escape,
    /// Take / Collect Prize, Begin / Resolve Collapse, Reveal) or through the host-run End Turn.</item>
    /// <item><b>Checked, then applied by the host:</b> an elimination is taken only when the sender eliminated itself, or
    /// the host's own table shows why (the pawn on a hole or off the floor, the player escaped, or stuck without a Key).
    /// The host applies it to its own turn state, so the turn moves on exactly as the rules move it; the pushed turn
    /// position is never used. A pushed table can never bring an eliminated player back.</item>
    /// <item><b>The sender's own records only:</b> Trap outcomes, Ability uses, Dodge / Rush guidance and Blind / Slow /
    /// Sticky statuses are taken only for the sender's own player; statuses only for this round or later.</item>
    /// <item><b>The activity log</b> is taken only when it adds lines after the host's own (display only).</item>
    /// </list>
    /// Remaining limit (documented in doc 10): a modified client can still change its <i>own</i> per-player records
    /// (clear its own waiting Trap, drop its own Slow / Sticky), eliminate itself, and add log lines.
    /// </summary>
    public static class TrapFloorRulesPushGuard
    {
        public static TrapFloorSessionStateSnapshot Merge(
            TrapFloorSessionStateSnapshot hostSnapshot,
            TrapFloorSessionStateSnapshot pushedSnapshot,
            PlayerId sender,
            ITrapFloorHostEvidence evidence,
            List<string> refused,
            List<PlayerId> acceptedEliminations = null)
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
            host.Turn.FloorTurnAfterEachPlayer = evidence != null && evidence.FloorTurnAfterEachPlayer;

            CompareObjective(host.Objective, pushed.Objective, refused);
            CompareCollapse(host.Collapse, pushed.Collapse, refused);
            MergeEliminations(host, pushed, sender, evidence, refused, acceptedEliminations);
            CompareTurn(host.Turn, pushed.Turn, refused);
            TrapFloorAbilityResolutionState ability = MergeAbility(host, pushed, sender, refused);
            TrapFloorActivityFeedState activity = MergeActivity(host.Activity, pushed.Activity, refused);
            return TrapFloorSessionStateSnapshot.Capture(activity, host.Objective, host.Collapse, host.Turn, ability);
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

        // ---------- checked, applied by the host ----------

        private static void MergeEliminations(
            TrapFloorSessionState host,
            TrapFloorSessionState pushed,
            PlayerId sender,
            ITrapFloorHostEvidence evidence,
            List<string> refused,
            List<PlayerId> accepted)
        {
            HashSet<PlayerId> before = new HashSet<PlayerId>(host.Turn.CopyEliminatedPlayerIds());
            PlayerId[] pushedOut = pushed.Turn.CopyEliminatedPlayerIds();
            HashSet<PlayerId> pushedSet = new HashSet<PlayerId>(pushedOut);
            foreach (PlayerId player in before)
            {
                if (!pushedSet.Contains(player)) Note(refused, $"bringing {Short(player)} back in");
            }

            for (int i = 0; i < pushedOut.Length; i++)
            {
                PlayerId player = pushedOut[i];
                if (before.Contains(player)) continue;
                if (!host.Turn.ContainsPlayer(player) || host.Turn.IsCurrentFloorFailed)
                {
                    Note(refused, $"elimination of {Short(player)} (not in play)");
                    continue;
                }

                bool allowed = player == sender
                    || host.Objective.IsPlayerEscaped(player)
                    || (evidence != null && (evidence.StandsOnHoleOrOffFloor(player) || evidence.IsStuckWithoutKey(player)));
                if (!allowed)
                {
                    Note(refused, $"elimination of {Short(player)} (the host's table shows no reason)");
                    continue;
                }

                host.Turn.MarkPlayerEliminatedForCurrentRound(player);
                accepted?.Add(player);
            }
        }

        // ---------- the sender's own records ----------

        private static TrapFloorAbilityResolutionState MergeAbility(
            TrapFloorSessionState hostState,
            TrapFloorSessionState pushedState,
            PlayerId sender,
            List<string> refused)
        {
            TrapFloorAbilityResolutionState host = hostState.AbilityResolution;
            TrapFloorAbilityResolutionState pushed = pushedState.AbilityResolution;
            int round = hostState.Turn.CurrentRound;

            // Trap outcomes: the host's records (a reveal is a command), with the sender's own outcomes and retriggers.
            List<TrapFloorTrapResolutionRecord> traps = new List<TrapFloorTrapResolutionRecord>(host.CopyTrapRecords());
            TrapFloorTrapResolutionRecord[] pushedTraps = pushed.CopyTrapRecords();
            bool othersTraps = false;
            for (int i = 0; i < pushedTraps.Length; i++)
            {
                TrapFloorTrapResolutionRecord record = pushedTraps[i];
                int index = IndexOf(traps, record);
                if (index >= 0)
                {
                    if (traps[index].Disposition == record.Disposition) continue;
                    if (record.AffectedPlayerId == sender) traps[index].SetDisposition(record.Disposition);
                    else othersTraps = true;
                }
                else if (record.AffectedPlayerId == sender)
                {
                    traps.Add(record.Copy());
                }
                else
                {
                    othersTraps = true;
                }
            }

            if (othersTraps) Note(refused, "another player's Trap outcome");

            // Ability uses: the host's, plus the sender's new ones.
            List<TrapFloorAbilityActivationRecord> activations = new List<TrapFloorAbilityActivationRecord>(host.CopyAbilityActivations());
            TrapFloorAbilityActivationRecord[] pushedActivations = pushed.CopyAbilityActivations();
            bool othersActivations = false;
            for (int i = 0; i < pushedActivations.Length; i++)
            {
                if (activations.Contains(pushedActivations[i])) continue;
                if (pushedActivations[i].PlayerId == sender) activations.Add(pushedActivations[i]);
                else othersActivations = true;
            }

            if (othersActivations) Note(refused, "another player's Ability use");

            // Dodge / Rush guidance: one at a time; the sender may set or clear only its own.
            TrapFloorDodgeAssistanceState dodge = host.CopyDodgeAssistance();
            TrapFloorRushAssistanceState rush = host.CopyRushAssistance();
            TrapFloorDodgeAssistanceState pushedDodge = pushed.CopyDodgeAssistance();
            TrapFloorRushAssistanceState pushedRush = pushed.CopyRushAssistance();
            bool mayChange = (dodge == null || dodge.PlayerId == sender)
                && (rush == null || rush.PlayerId == sender)
                && (pushedDodge == null || pushedDodge.PlayerId == sender)
                && (pushedRush == null || pushedRush.PlayerId == sender);
            if (mayChange)
            {
                dodge = pushedDodge;
                rush = pushedRush;
            }
            else if (!SameGuide(dodge?.PlayerId, pushedDodge?.PlayerId) || !SameGuide(rush?.PlayerId, pushedRush?.PlayerId))
            {
                Note(refused, "another player's move guidance");
            }

            // Statuses: other players' as on the host; the sender's own as pushed, for this round or later only.
            bool othersStatus = false;
            List<TrapFloorBlindStatusState> blind = Statuses(host.CopyBlindStatuses(), pushed.CopyBlindStatuses(), s => s.PlayerId, s => s.AppliesInRound, sender, round, ref othersStatus);
            List<TrapFloorSlowStatusState> slow = Statuses(host.CopySlowStatuses(), pushed.CopySlowStatuses(), s => s.PlayerId, s => s.AppliesInRound, sender, round, ref othersStatus);
            List<TrapFloorStickyStatusState> sticky = Statuses(host.CopyStickyStatuses(), pushed.CopyStickyStatuses(), s => s.PlayerId, s => s.AppliesInRound, sender, round, ref othersStatus);
            if (othersStatus) Note(refused, "another player's status");

            TrapFloorAbilityResolutionState merged = new TrapFloorAbilityResolutionState(host.MatchId);
            merged.Restore(traps, activations, dodge, rush, blind, slow, sticky);
            return merged;
        }

        private static List<T> Statuses<T>(
            T[] host,
            T[] pushed,
            Func<T, PlayerId> playerOf,
            Func<T, int> roundOf,
            PlayerId sender,
            int round,
            ref bool othersChanged)
        {
            List<T> result = new List<T>();
            int hostOthers = 0;
            for (int i = 0; i < host.Length; i++)
            {
                if (playerOf(host[i]) == sender) continue;
                result.Add(host[i]);
                hostOthers++;
            }

            int pushedOthers = 0;
            for (int i = 0; i < pushed.Length; i++)
            {
                if (playerOf(pushed[i]) != sender)
                {
                    pushedOthers++;
                    continue;
                }

                if (roundOf(pushed[i]) >= round) result.Add(pushed[i]);
            }

            if (hostOthers != pushedOthers) othersChanged = true;
            return result;
        }

        private static bool SameGuide(PlayerId? a, PlayerId? b) => a.HasValue == b.HasValue && (!a.HasValue || a.Value == b.Value);

        private static int IndexOf(List<TrapFloorTrapResolutionRecord> records, TrapFloorTrapResolutionRecord record)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].FloorCardId == record.FloorCardId
                    && records[i].AffectedPlayerId == record.AffectedPlayerId
                    && records[i].RevealedRevision == record.RevealedRevision)
                    return i;
            }

            return -1;
        }

        // ---------- the log ----------

        private static TrapFloorActivityFeedState MergeActivity(TrapFloorActivityFeedState host, TrapFloorActivityFeedState pushed, List<string> refused)
        {
            TrapFloorActivityFeedState result = new TrapFloorActivityFeedState(host.MatchId);
            IReadOnlyList<TrapFloorActivityEntry> a = host.Entries;
            IReadOnlyList<TrapFloorActivityEntry> b = pushed.Entries;
            bool extends = b.Count >= a.Count;
            for (int i = 0; extends && i < a.Count; i++)
            {
                extends = a[i].Sequence == b[i].Sequence
                    && a[i].AcceptedRevision == b[i].AcceptedRevision
                    && a[i].Kind == b[i].Kind
                    && a[i].ActorPlayerId == b[i].ActorPlayerId;
            }

            // Display only: a log that does not follow the host's is quietly not taken (not counted as a refusal).
            result.RestoreEntries(extends ? b : a);
            return result;
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
