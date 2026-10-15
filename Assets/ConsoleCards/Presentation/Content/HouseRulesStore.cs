using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using UnityEngine;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>One saved change of a house-rule set: the rule's stable ID and its new text and value.</summary>
    [Serializable]
    public sealed class HouseRuleChangeRecord
    {
        public string entryId = string.Empty;
        public bool changesText;
        public string text = string.Empty;
        public bool changesValue;
        public string value = string.Empty;
    }

    /// <summary>A saved house-rule set (doc 23, R3): based on a game's default rules, with its changed rules.</summary>
    [Serializable]
    public sealed class HouseRuleSetRecord
    {
        public const int CurrentFormatVersion = 1;
        public int formatVersion = CurrentFormatVersion;
        public string stableId = string.Empty;
        public string displayName = string.Empty;
        public string gameStableId = string.Empty;
        public string basedOnStableId = string.Empty;
        public string createdUtc = string.Empty;
        public List<HouseRuleChangeRecord> changes = new List<HouseRuleChangeRecord>();

        public Dictionary<string, RuleEntryChange> ToChanges()
        {
            Dictionary<string, RuleEntryChange> result = new Dictionary<string, RuleEntryChange>(StringComparer.Ordinal);
            for (int i = 0; i < changes.Count; i++)
            {
                HouseRuleChangeRecord change = changes[i];
                if (change == null || string.IsNullOrWhiteSpace(change.entryId)) continue;
                result[change.entryId] = new RuleEntryChange(
                    change.changesText ? change.text : null,
                    change.changesValue ? change.value : null);
            }

            return result;
        }

        public static HouseRuleSetRecord FromChanges(
            string stableId,
            string displayName,
            RuleSetDefinition basedOn,
            IReadOnlyDictionary<string, RuleEntryChange> changes)
        {
            HouseRuleSetRecord record = new HouseRuleSetRecord
            {
                stableId = stableId,
                displayName = displayName,
                gameStableId = basedOn.GameStableId ?? string.Empty,
                basedOnStableId = basedOn.StableId ?? string.Empty,
                createdUtc = DateTime.UtcNow.ToString("o"),
            };
            foreach (KeyValuePair<string, RuleEntryChange> pair in changes)
            {
                record.changes.Add(new HouseRuleChangeRecord
                {
                    entryId = pair.Key,
                    changesText = pair.Value.Text != null,
                    text = pair.Value.Text ?? string.Empty,
                    changesValue = pair.Value.Value != null,
                    value = pair.Value.Value ?? string.Empty,
                });
            }

            return record;
        }
    }

    /// <summary>
    /// House-rule sets saved in the player's profile (doc 23 R3, doc 28): one JSON file per set in
    /// <c>Profiles/&lt;profile&gt;/games/&lt;game&gt;/rules/</c>, next to the game's packs. Plain files a player can copy to
    /// share; they are read at start, never per frame.
    /// </summary>
    public sealed class HouseRulesStore
    {
        public const string RulesFolderName = "rules";
        private readonly ContentPackStore contentStore;

        public HouseRulesStore(ContentPackStore contentStore)
        {
            this.contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        }

        public string RulesFolder(string gameId) => Path.Combine(contentStore.GameFolder(gameId), RulesFolderName);

        /// <summary>Every readable saved set for the game, oldest first. Unreadable files are skipped with a warning.</summary>
        public List<HouseRuleSetRecord> Load(string gameId)
        {
            List<HouseRuleSetRecord> records = new List<HouseRuleSetRecord>();
            string folder = RulesFolder(gameId);
            if (!Directory.Exists(folder)) return records;
            string[] files = Directory.GetFiles(folder, "*.json");
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    HouseRuleSetRecord record = ConsoleCards.Presentation.Saving.SaveFiles.ReadJson<HouseRuleSetRecord>(files[i], "Rules");
                    if (record == null || string.IsNullOrWhiteSpace(record.stableId) || record.formatVersion > HouseRuleSetRecord.CurrentFormatVersion)
                    {
                        Debug.LogWarning($"[Rules] Skipped {files[i]}: not a house-rule set this version can read.");
                        continue;
                    }

                    records.Add(record);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Rules] Skipped {files[i]}: {exception.Message}");
                }
            }

            records.Sort((a, b) => string.CompareOrdinal(a.createdUtc, b.createdUtc));
            return records;
        }

        public bool TrySave(string gameId, HouseRuleSetRecord record, out string error)
        {
            error = string.Empty;
            try
            {
                string path = Path.Combine(RulesFolder(gameId), ContentPackStore.SafeName(record.stableId, "rules") + ".json");
                return ConsoleCards.Presentation.Saving.SaveFiles.TryWriteJson(path, record, out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
