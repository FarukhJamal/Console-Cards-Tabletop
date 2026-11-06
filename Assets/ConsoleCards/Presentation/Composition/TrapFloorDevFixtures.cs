#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Random;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// DEV-FIXTURE (2026-11-06, "stop relying on random floor layouts"): reproducible Trap Floor deals for testing Keys,
    /// exits and eliminations live.
    /// <list type="bullet">
    /// <item>Editor only: the whole file is inside <c>#if UNITY_EDITOR</c>, so no player build (development builds
    /// included) contains it. It is driven from the Editor menu Console Cards › Debug › Fixtures.</item>
    /// <item>It changes nothing but the deal's seed. The deal itself is the game's normal path (the same template
    /// factory, the game's own authored content and rules, the room's seed message), so a fixed seed gives the same
    /// layout for the same game, difficulty, win mode, floor and player count, on every computer in the room.</item>
    /// <item>Online, only the host's deal reads it (the seed then travels in the normal table setup), so the joined
    /// computers deal the same floor.</item>
    /// <item>The floor description names face-down tiles. It goes to the Editor's own Console only; it is never sent
    /// to another computer.</item>
    /// </list>
    /// </summary>
    public static class TrapFloorDevFixtures
    {
        private static int pendingSeed;
        private static bool armed;

        /// <summary>True while the next deal will use <see cref="PendingSeed"/>.</summary>
        public static bool IsArmed => armed;

        public static int PendingSeed => pendingSeed;

        /// <summary>The next deal made on this computer (offline, or as the online host) uses this seed, once.</summary>
        public static void ArmDealSeed(int seed)
        {
            pendingSeed = seed;
            armed = true;
            Debug.Log($"[Fixture] The next deal uses seed {seed} (Editor only).");
        }

        public static void Disarm()
        {
            armed = false;
        }

        /// <summary>Takes the armed seed for this deal (online host path). False when none is armed.</summary>
        internal static bool TryTakeDealSeed(out int seed)
        {
            seed = pendingSeed;
            if (!armed) return false;
            armed = false;
            Debug.Log($"[Fixture] Dealing with fixed seed {seed}.");
            return true;
        }

        /// <summary>Offline deals: the armed seed starts the table's random numbers for this deal.</summary>
        internal static void ApplyOfflineSeed(TabletopComposition host)
        {
            if (host == null || !TryTakeDealSeed(out int seed)) return;
            if (host.authoritativeRandomValueSource == null) host.authoritativeRandomValueSource = new SystemRandomValueSource(seed);
            else host.authoritativeRandomValueSource.Reseed(seed);
        }

        /// <summary>The floor in play on this computer as a grid (top row first), with the pawns' tiles.</summary>
        public static string DescribeFloor(TabletopComposition host)
        {
            if (host == null || host.trapFloorTemplate == null) return "[Fixture] No Trap Floor table in play.";
            Dictionary<TabletopObjectId, string> pawns = new Dictionary<TabletopObjectId, string>();
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            StringBuilder players = new StringBuilder();
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = template.Players[i];
                string where = host.TrapFloorGame != null && host.TrapFloorGame.TryReadPawnTile(player.PawnId, out TrapFloorCoordinate tile)
                    ? tile.ToString()
                    : "off the floor";
                players.Append($"\n  seat {i + 1}: pawn at {where}");
            }

            return $"{DescribeTemplate(template)}{players}";
        }

        /// <summary>A floor's layout, top row first: each cell is "(x,y) name". Pure, so it is unit tested.</summary>
        public static string DescribeTemplate(TrapFloorTemplateDefinition template)
        {
            if (template == null) return "[Fixture] No floor.";
            StringBuilder text = new StringBuilder();
            text.Append($"[Fixture] Floor {template.FloorIndex + 1} of {template.FloorCount}, {template.GridColumns}×{template.GridRows}, "
                + $"mode {template.ActiveMode.StableId}, Keys needed {template.FloorRequiredKeyCount}:");
            for (int y = template.GridRows; y >= 1; y--)
            {
                text.Append("\n ");
                for (int x = 1; x <= template.GridColumns; x++)
                {
                    TrapFloorCoordinate cell = new TrapFloorCoordinate(x, y);
                    string name = template.TryGetFloorCardId(cell, out TabletopObjectId id)
                        && template.CardLabels.TryGetValue(id, out string label)
                            ? label
                            : "-";
                    text.Append($" ({x},{y}) {Short(name)} |");
                }
            }

            return text.ToString();
        }

        /// <summary>Where the floor's cards of one kind lie (for example "Key"), as cells.</summary>
        public static List<TrapFloorCoordinate> CellsNamed(TrapFloorTemplateDefinition template, string name)
        {
            List<TrapFloorCoordinate> cells = new List<TrapFloorCoordinate>();
            if (template == null || string.IsNullOrEmpty(name)) return cells;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> cell in template.FloorCardIds)
            {
                if (template.CardLabels.TryGetValue(cell.Value, out string label)
                    && label.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cells.Add(cell.Key);
                }
            }

            cells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return cells;
        }

        private static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return "-";
            return name.Length <= 16 ? name : name.Substring(0, 16);
        }
    }
}
#endif
