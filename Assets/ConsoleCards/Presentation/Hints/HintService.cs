using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using UnityEngine;

namespace ConsoleCards.Presentation.Hints
{
    /// <summary>
    /// The one place hints come from (doc 23 §10). Libraries of hints (the platform's; each game's from its
    /// rules, H2) and the game's hint helpers named by the rules in play (H3) are matched against events the
    /// table raises; the best hint the current level allows and that has not been shown too often is returned
    /// for the hint strip. Plain C#; the only per-frame work is <see cref="TickHelpers"/>, which allocates nothing.
    /// </summary>
    public sealed class HintService
    {
        private readonly List<HintLibraryDefinition> libraries = new List<HintLibraryDefinition>();
        private readonly List<HintEntry> gameHints = new List<HintEntry>();
        private readonly List<IGameHintHelper> helpers = new List<IGameHintHelper>();
        private readonly List<IGameHintHelper> helpersInPlay = new List<IGameHintHelper>();
        private readonly HashSet<string> missingHelpersWarned = new HashSet<string>(StringComparer.Ordinal);
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

        /// <summary>
        /// A game's hint helper (H3). Added once; it runs only while a rule in play names it
        /// (<see cref="SetHelpersInPlay"/>).
        /// </summary>
        public void AddHelper(IGameHintHelper helper)
        {
            if (helper == null || string.IsNullOrEmpty(helper.HelperId) || FindHelper(helper.HelperId) != null) return;
            helpers.Add(helper);
        }

        /// <summary>
        /// The helpers the rules in play name (their hintHelper fields). Helpers no longer named stop and hide
        /// their markers; a name with no helper is reported once in the Console.
        /// </summary>
        public void SetHelpersInPlay(IReadOnlyList<string> helperIds)
        {
            for (int i = helpersInPlay.Count - 1; i >= 0; i--)
            {
                if (Contains(helperIds, helpersInPlay[i].HelperId)) continue;
                helpersInPlay[i].Stop();
                helpersInPlay.RemoveAt(i);
            }

            if (helperIds == null) return;
            for (int i = 0; i < helperIds.Count; i++)
            {
                IGameHintHelper helper = FindHelper(helperIds[i]);
                if (helper == null)
                {
                    if (!string.IsNullOrEmpty(helperIds[i]) && missingHelpersWarned.Add(helperIds[i]))
                        Debug.LogWarning($"[Hints] A rule names the hint helper \"{helperIds[i]}\", but this game has no such helper.");
                    continue;
                }

                if (!helpersInPlay.Contains(helper)) helpersInPlay.Add(helper);
            }
        }

        public bool IsHelperInPlay(string helperId)
        {
            for (int i = 0; i < helpersInPlay.Count; i++)
            {
                if (string.Equals(helpersInPlay[i].HelperId, helperId, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>Once a frame: the helpers in play show or hide their markers for the current level.</summary>
        public void TickHelpers()
        {
            for (int i = 0; i < helpersInPlay.Count; i++) helpersInPlay[i].Tick(Level);
        }

        /// <summary>The best hint for an event, or false when none may show. Nothing is recorded until it shows.</summary>
        public bool TryFind(string trigger, string context, out HintEntry hint)
        {
            hint = null;
            if (Level == HintLevel.Off || string.IsNullOrEmpty(trigger)) return false;
            for (int l = 0; l < libraries.Count; l++) Consider(libraries[l].Entries, trigger, context, ref hint);
            Consider(gameHints, trigger, context, ref hint);
            // Helpers answer for themselves (they already know the event matches).
            for (int i = 0; i < helpersInPlay.Count; i++)
            {
                HintEntry answer = helpersInPlay[i].Explain(trigger, context, Level);
                if (answer != null && CanConsider(answer, hint)) hint = answer;
            }

            return hint != null;
        }

        private void Consider(IReadOnlyList<HintEntry> entries, string trigger, string context, ref HintEntry best)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                HintEntry entry = entries[i];
                if (entry == null || !entry.Matches(trigger, context) || !CanConsider(entry, best)) continue;
                best = entry;
            }
        }

        private bool CanConsider(HintEntry entry, HintEntry best)
        {
            return !string.IsNullOrEmpty(entry.StableId)
                && (entry.Kind != HintKind.Tip || Level == HintLevel.Learning)
                && seen.CanShow(entry.StableId, entry.MaxShows)
                && (best == null || entry.Priority > best.Priority);
        }

        /// <summary>
        /// A reminder with no show limit explains the table right now (for example why a move is not allowed);
        /// it is not counted, and "Got it" only closes it.
        /// </summary>
        public static bool IsEveryTimeReminder(HintEntry hint) =>
            hint != null && hint.Kind == HintKind.Reminder && hint.MaxShows == 0;

        public void MarkShown(HintEntry hint)
        {
            if (hint != null && !IsEveryTimeReminder(hint)) seen.RecordShown(hint.StableId);
        }

        /// <summary>"Got it": the hint never shows again on this computer (an every-time reminder only closes).</summary>
        public void MarkDismissed(HintEntry hint)
        {
            if (hint != null && !IsEveryTimeReminder(hint)) seen.RecordDismissed(hint.StableId);
        }

        private IGameHintHelper FindHelper(string helperId)
        {
            for (int i = 0; i < helpers.Count; i++)
            {
                if (string.Equals(helpers[i].HelperId, helperId, StringComparison.Ordinal)) return helpers[i];
            }

            return null;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal)) return true;
            }

            return false;
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
