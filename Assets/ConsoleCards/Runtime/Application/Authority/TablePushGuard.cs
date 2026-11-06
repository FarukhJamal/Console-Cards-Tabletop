using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// AUTH-PUSH (2026-11-06, security milestone): what a joined computer's whole-table push may not change.
    /// <para>Every change to the table's pieces and piles (pawn and card moves, physics resting places, draws, deck order,
    /// flips, dice values, piles' contents, pieces made or deleted) is a command, which the host checks (sender seat,
    /// duplicate ids, the use case's own checks) before it runs. The whole-table push exists only for the game's rules
    /// state (turns, eliminations, Keys, statuses) and drawings, which are still worked out on the player's computer.</para>
    /// <para>So the host keeps its own pieces and piles and takes only the rules state and drawings from a push. This counts
    /// what a push tried to change, for the log and the correction. Pure, so it is unit tested. Compare two states made
    /// the same way (both restored from snapshots), so a field a snapshot does not keep never shows as a change.</para>
    /// </summary>
    public static class TablePushGuard
    {
        /// <summary>How many pieces or piles differ between the host's state and a pushed one (0: none).</summary>
        public static int CountPieceChanges(MatchState authority, MatchState pushed, List<string> notes = null, int maxNotes = 4)
        {
            if (authority == null || pushed == null) return 0;
            int changes = 0;
            changes += CompareObjects(authority.Cards, pushed.Cards, c => c.BaseState, (a, b) => a.Face == b.Face, "card", notes, maxNotes);
            changes += CompareObjects(authority.Pawns, pushed.Pawns, p => p.BaseState, null, "pawn", notes, maxNotes);
            changes += CompareObjects(authority.Tokens, pushed.Tokens, t => t.BaseState, null, "token", notes, maxNotes);
            changes += CompareObjects(authority.Dice, pushed.Dice, d => d.BaseState, (a, b) => a.CurrentValue == b.CurrentValue, "die", notes, maxNotes);
            changes += CompareContainers(authority.Containers, pushed.Containers, notes, maxNotes);
            return changes;
        }

        private static int CompareObjects<T>(
            IReadOnlyDictionary<TabletopObjectId, T> authority,
            IReadOnlyDictionary<TabletopObjectId, T> pushed,
            System.Func<T, TabletopObjectState> baseOf,
            System.Func<T, T, bool> sameExtra,
            string kind,
            List<string> notes,
            int maxNotes)
        {
            int changes = 0;
            foreach (KeyValuePair<TabletopObjectId, T> entry in authority)
            {
                if (!pushed.TryGetValue(entry.Key, out T other))
                {
                    changes++;
                    Note(notes, maxNotes, $"{kind} {Short(entry.Key)} removed");
                    continue;
                }

                if (!SameObject(baseOf(entry.Value), baseOf(other)) || (sameExtra != null && !sameExtra(entry.Value, other)))
                {
                    changes++;
                    Note(notes, maxNotes, $"{kind} {Short(entry.Key)} changed");
                }
            }

            foreach (TabletopObjectId id in pushed.Keys)
            {
                if (authority.ContainsKey(id)) continue;
                changes++;
                Note(notes, maxNotes, $"{kind} {Short(id)} added");
            }

            return changes;
        }

        private static int CompareContainers(
            IReadOnlyDictionary<ContainerId, ContainerState> authority,
            IReadOnlyDictionary<ContainerId, ContainerState> pushed,
            List<string> notes,
            int maxNotes)
        {
            int changes = 0;
            foreach (KeyValuePair<ContainerId, ContainerState> entry in authority)
            {
                if (!pushed.TryGetValue(entry.Key, out ContainerState other))
                {
                    changes++;
                    Note(notes, maxNotes, $"pile {entry.Value.Kind} removed");
                    continue;
                }

                IReadOnlyList<TabletopObjectId> a = entry.Value.ObjectIds;
                IReadOnlyList<TabletopObjectId> b = other.ObjectIds;
                bool same = a.Count == b.Count;
                for (int i = 0; same && i < a.Count; i++) same = a[i].Equals(b[i]);
                if (same) continue;
                changes++;
                Note(notes, maxNotes, $"pile {entry.Value.Kind} contents or order changed");
            }

            foreach (ContainerId id in pushed.Keys)
            {
                if (authority.ContainsKey(id)) continue;
                changes++;
                Note(notes, maxNotes, "pile added");
            }

            return changes;
        }

        private static bool SameObject(TabletopObjectState a, TabletopObjectState b)
        {
            if (a == null || b == null) return a == b;
            return a.Pose.Equals(b.Pose)
                && a.ContainerId.Equals(b.ContainerId)
                && a.OwnerPlayerId.Equals(b.OwnerPlayerId)
                && a.Visibility == b.Visibility
                && a.IsUserLocked == b.IsUserLocked
                && a.PhysicalRevision == b.PhysicalRevision
                && SamePhysical(a.PhysicalState, b.PhysicalState);
        }

        private static bool SamePhysical(PhysicalObjectState a, PhysicalObjectState b)
        {
            if (a == null || b == null) return a == b;
            return a.Mode == b.Mode
                && a.ControllingPlayerId.Equals(b.ControllingPlayerId)
                && a.Position.X.Equals(b.Position.X) && a.Position.Y.Equals(b.Position.Y) && a.Position.Z.Equals(b.Position.Z)
                && a.Rotation.X.Equals(b.Rotation.X) && a.Rotation.Y.Equals(b.Rotation.Y)
                && a.Rotation.Z.Equals(b.Rotation.Z) && a.Rotation.W.Equals(b.Rotation.W);
        }

        private static void Note(List<string> notes, int maxNotes, string text)
        {
            if (notes != null && notes.Count < maxNotes) notes.Add(text);
        }

        // Only the first characters of an id: enough to find it in a log, never a card's name or face.
        private static string Short(TabletopObjectId id) => id.Value.ToString("N").Substring(0, 8);
    }
}
