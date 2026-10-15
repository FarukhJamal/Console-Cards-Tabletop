using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using UnityEngine;

namespace ConsoleCards.Presentation.Hints
{
    /// <summary>
    /// The one place hints come from (doc 23 §10). Libraries of hints (the platform's now; each game's from its
    /// rules in H2) are matched against events the table raises; the best hint the current level allows and that
    /// has not been shown too often is returned for the hint strip. Plain C#; no per-frame work.
    /// </summary>
    public sealed class HintService
    {
        private readonly List<HintLibraryDefinition> libraries = new List<HintLibraryDefinition>();
        private readonly List<HintEntry> gameHints = new List<HintEntry>();
        private readonly HintSeenStore seen;

        public HintService(HintSeenStore seenStore)
        {
            seen = seenStore ?? throw new ArgumentNullException(nameof(seenStore));
        }

        public HintLevel Level { get; set; } = HintLevel.Learning;

        public void AddLibrary(HintLibraryDefinition library)
        {
            if (library != null && !libraries.Contains(library)) libraries.Add(library);
        }

        /// <summary>The current game's hints, made from its rules (H2); replaces the previous game's.</summary>
        public void SetGameHints(IReadOnlyList<HintEntry> hints)
        {
            gameHints.Clear();
            if (hints == null) return;
            for (int i = 0; i < hints.Count; i++)
            {
                if (hints[i] != null) gameHints.Add(hints[i]);
            }
        }

        /// <summary>The best hint for an event, or false when none may show. Nothing is recorded until it shows.</summary>
        public bool TryFind(string trigger, string context, out HintEntry hint)
        {
            hint = null;
            if (Level == HintLevel.Off || string.IsNullOrEmpty(trigger)) return false;
            for (int l = 0; l < libraries.Count; l++) Consider(libraries[l].Entries, trigger, context, ref hint);
            Consider(gameHints, trigger, context, ref hint);
            return hint != null;
        }

        private void Consider(IReadOnlyList<HintEntry> entries, string trigger, string context, ref HintEntry best)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                HintEntry entry = entries[i];
                if (entry == null
                    || string.IsNullOrEmpty(entry.StableId)
                    || !entry.Matches(trigger, context)
                    || (entry.Kind == HintKind.Tip && Level != HintLevel.Learning)
                    || !seen.CanShow(entry.StableId, entry.MaxShows)
                    || (best != null && entry.Priority <= best.Priority))
                {
                    continue;
                }

                best = entry;
            }
        }

        public void MarkShown(HintEntry hint)
        {
            if (hint != null) seen.RecordShown(hint.StableId);
        }

        /// <summary>"Got it": the hint never shows again on this computer.</summary>
        public void MarkDismissed(HintEntry hint)
        {
            if (hint != null) seen.RecordDismissed(hint.StableId);
        }

        /// <summary>Shows every tip again from the start (the Table menu's Tips switch, turned off and on, does not).</summary>
        public void ResetSeen() => seen.Clear();
    }

    /// <summary>Which hints were shown or dismissed, kept on this computer in one small JSON file.</summary>
    public sealed class HintSeenStore
    {
        [Serializable]
        private sealed class Record
        {
            public string id = string.Empty;
            public int shown;
            public bool dismissed;
        }

        [Serializable]
        private sealed class FileData
        {
            public int formatVersion = 1;
            public List<Record> hints = new List<Record>();
        }

        private readonly string path;
        private readonly Dictionary<string, Record> records = new Dictionary<string, Record>(StringComparer.Ordinal);

        public HintSeenStore(string filePath)
        {
            path = filePath;
            Load();
        }

        public static HintSeenStore CreateDefault() =>
            new HintSeenStore(Path.Combine(UnityEngine.Application.persistentDataPath, "Hints", "seen.json"));

        public bool CanShow(string id, int maxShows)
        {
            if (!records.TryGetValue(id, out Record record)) return true;
            return !record.dismissed && (maxShows <= 0 || record.shown < maxShows);
        }

        public void RecordShown(string id)
        {
            Get(id).shown++;
            Save();
        }

        public void RecordDismissed(string id)
        {
            Get(id).dismissed = true;
            Save();
        }

        public void Clear()
        {
            records.Clear();
            Save();
        }

        private Record Get(string id)
        {
            if (!records.TryGetValue(id, out Record record))
            {
                record = new Record { id = id };
                records.Add(id, record);
            }

            return record;
        }

        private void Load()
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                FileData data = JsonUtility.FromJson<FileData>(File.ReadAllText(path));
                if (data?.hints == null) return;
                for (int i = 0; i < data.hints.Count; i++)
                {
                    Record record = data.hints[i];
                    if (record != null && !string.IsNullOrEmpty(record.id)) records[record.id] = record;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Hints] Could not read {path}: {exception.Message}. Hints start fresh.");
            }
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                FileData data = new FileData();
                foreach (Record record in records.Values) data.hints.Add(record);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Hints] Could not save {path}: {exception.Message}.");
            }
        }
    }
}
