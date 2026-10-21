using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>One placed piece of a saved table layout (LAYOUT-1): its role and where it goes (table units).</summary>
    [Serializable]
    public sealed class TableLayoutPlaceRecord
    {
        public string role = string.Empty;
        public double x;
        public double y;
        public float rotation;
    }

    /// <summary>
    /// A table layout the players agreed (LAYOUT-1, owner 2026-10-21): where each piece of a game goes, for one player
    /// count. Roles: "seat:0".."seat:3" (each seat's Console), "pile:controller", "pile:ability",
    /// "pile:ability-discard", "die:x", "die:y" and "board" (the board's centre).
    /// </summary>
    [Serializable]
    public sealed class TableLayoutRecord
    {
        public const int CurrentFormatVersion = 1;
        public int formatVersion = CurrentFormatVersion;
        public string gameStableId = string.Empty;
        public int playerCount;
        public string savedUtc = string.Empty;
        public List<TableLayoutPlaceRecord> places = new List<TableLayoutPlaceRecord>();

        public void Set(string role, double x, double y, float rotation)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i] != null && string.Equals(places[i].role, role, StringComparison.Ordinal))
                {
                    places[i].x = x;
                    places[i].y = y;
                    places[i].rotation = rotation;
                    return;
                }
            }

            places.Add(new TableLayoutPlaceRecord { role = role, x = x, y = y, rotation = rotation });
        }

        public bool TryGet(string role, out TableLayoutPlaceRecord place)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i] != null && string.Equals(places[i].role, role, StringComparison.Ordinal))
                {
                    place = places[i];
                    return true;
                }
            }

            place = null;
            return false;
        }
    }

    /// <summary>
    /// Saved table layouts in the player's profile (LAYOUT-1): one JSON file per game and player count in
    /// <c>Profiles/&lt;profile&gt;/games/&lt;game&gt;/layouts/players-&lt;n&gt;.json</c>. Read when a table is dealt, never per frame.
    /// </summary>
    public sealed class TableLayoutStore
    {
        public const string LayoutsFolderName = "layouts";
        private readonly ContentPackStore contentStore;

        public TableLayoutStore(ContentPackStore contentStore)
        {
            this.contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        }

        public string LayoutPath(string gameId, int playerCount) =>
            Path.Combine(contentStore.GameFolder(gameId), LayoutsFolderName, $"players-{Math.Max(0, playerCount)}.json");

        /// <summary>The saved layout, or null when there is none or it cannot be read (a warning says why).</summary>
        public TableLayoutRecord TryLoad(string gameId, int playerCount)
        {
            string path = LayoutPath(gameId, playerCount);
            if (!File.Exists(path)) return null;
            try
            {
                TableLayoutRecord record = ConsoleCards.Presentation.Saving.SaveFiles.ReadJson<TableLayoutRecord>(path, "Layout");
                if (record == null || record.places == null || record.formatVersion > TableLayoutRecord.CurrentFormatVersion)
                {
                    Debug.LogWarning($"[Layout] Skipped {path}: not a table layout this version can read.");
                    return null;
                }

                return record;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Layout] Skipped {path}: {exception.Message}");
                return null;
            }
        }

        public bool TrySave(string gameId, TableLayoutRecord record, out string error)
        {
            error = string.Empty;
            try
            {
                return ConsoleCards.Presentation.Saving.SaveFiles.TryWriteJson(
                    LayoutPath(gameId, record.playerCount), record, out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>Back to the game's default layout: the saved file goes.</summary>
        public bool TryDelete(string gameId, int playerCount, out string error)
        {
            error = string.Empty;
            try
            {
                string path = LayoutPath(gameId, playerCount);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
