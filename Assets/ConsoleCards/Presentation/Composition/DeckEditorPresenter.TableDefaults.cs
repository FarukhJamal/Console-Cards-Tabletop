using System;
using System.Collections.Generic;
using System.Globalization;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.UI;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// STAGE-3 (owner, 2026-11-18; doc 19 §18 stage C): the game's default floor deck counts in CREATE › DECKS. A floor
    /// row (Floor 1 · 6×6 …) lists, above its cards, the rulebook's floor deck for that size per difficulty as three rows
    /// (Friends, Abilities, Fall Traps) with − and +. A change is saved with the decks (decks.json: in the game's files
    /// in the editor, the player's profile in a build) and replaces the rulebook's value at every table from the next
    /// one started; a house rule that changed the same line still wins. Event-time only.
    /// </summary>
    internal sealed partial class DeckEditorPresenter
    {
        internal const string CountRowPrefix = "count:";
        private static readonly string[] CountParts = { "Friends", "Abilities", "Fall Traps" };
        // A floor holds up to this many Keys and 2 Exits (rules v1.151126: 4 Keys in Free for all and Survival).
        private const int MostFloorKeys = TrapFloorCartridge.FreeForAllKeyCount;
        private const int MostFloorExits = 2;

        // The rulebook's floor deck lines for one floor size, in the rulebook's order (one per difficulty).
        private List<RuleEntry> FloorDeckEntries(int size)
        {
            List<RuleEntry> entries = new List<RuleEntry>(4);
            RuleSetDefinition rules = host.CardEditorPanel.editorGame != null ? host.CardEditorPanel.editorGame.DefaultRuleSet : null;
            if (rules == null) return entries;
            string key = RuleSettingKeys.FloorDeckPrefix + size.ToString(CultureInfo.InvariantCulture) + "x" + size.ToString(CultureInfo.InvariantCulture);
            for (int s = 0; s < rules.Sections.Count; s++)
            {
                RuleSection section = rules.Sections[s];
                for (int e = 0; section != null && e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry != null && string.Equals(entry.SettingKey, key, StringComparison.Ordinal)) entries.Add(entry);
                }
            }

            return entries;
        }

        private PlayerRuleValue GameValueOf(string entryId)
        {
            PlayerDecksFile decks = host.CardEditorPanel.editorDecks;
            if (decks.ruleValues == null) decks.ruleValues = new List<PlayerRuleValue>();
            for (int i = 0; i < decks.ruleValues.Count; i++)
            {
                PlayerRuleValue value = decks.ruleValues[i];
                if (value != null && string.Equals(value.entryId, entryId, StringComparison.Ordinal)) return value;
            }

            return null;
        }

        private string FloorDeckValue(RuleEntry entry)
        {
            PlayerRuleValue value = GameValueOf(entry.StableId);
            return value != null && RuleSettingKeys.TryParseFloorDeck(value.value, out _, out _, out _) ? value.value : entry.Value;
        }

        // "Easy", "Medium", "Hard (also Impossible)": from the line's modes (trap-floor-mode-easy …).
        private static string DifficultyName(RuleEntry entry)
        {
            if (entry.ModeStableIds == null || entry.ModeStableIds.Count == 0) return "Every difficulty";
            string first = ModeWord(entry.ModeStableIds[0]);
            if (entry.ModeStableIds.Count == 1) return first;
            List<string> others = new List<string>(entry.ModeStableIds.Count - 1);
            for (int i = 1; i < entry.ModeStableIds.Count; i++) others.Add(ModeWord(entry.ModeStableIds[i]));
            return $"{first} (also {string.Join(", ", others)})";
        }

        private static string ModeWord(string modeStableId)
        {
            string id = modeStableId ?? string.Empty;
            int dash = id.LastIndexOf('-');
            string word = dash >= 0 ? id.Substring(dash + 1) : id;
            return word.Length == 0 ? "Mode" : char.ToUpperInvariant(word[0]) + word.Substring(1);
        }

        // The count rows of a floor row, above its cards.
        internal void AddFloorCountRows(int floor, List<DeckEditorCardRowModel> rows)
        {
            int size = FirstFloorSize - (floor - 1);
            List<RuleEntry> entries = FloorDeckEntries(size);
            for (int i = 0; i < entries.Count; i++)
            {
                RuleEntry entry = entries[i];
                if (!RuleSettingKeys.TryParseFloorDeck(FloorDeckValue(entry), out int friends, out int abilities, out int fallTraps)) continue;
                int[] parts = { friends, abilities, fallTraps };
                bool changed = GameValueOf(entry.StableId) != null;
                string detail = changed
                    ? $"Game default · {size}×{size} · changed (rulebook {entry.Value})"
                    : $"Game default · {size}×{size} · as the rulebook";
                for (int p = 0; p < CountParts.Length; p++)
                {
                    rows.Add(new DeckEditorCardRowModel($"{CountRowPrefix}{entry.StableId}:{p}",
                        $"{DifficultyName(entry)} · {CountParts[p]}", detail, null, parts[p], true));
                }
            }
        }

        // − or + on a count row; false for any other row.
        internal bool TryChangeFloorCount(string rowId, int step)
        {
            if (string.IsNullOrEmpty(rowId) || !rowId.StartsWith(CountRowPrefix, StringComparison.Ordinal)) return false;
            string rest = rowId.Substring(CountRowPrefix.Length);
            int colon = rest.LastIndexOf(':');
            if (colon <= 0 || !int.TryParse(rest.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int part)
                || part < 0 || part >= CountParts.Length)
            {
                return true;
            }

            string entryId = rest.Substring(0, colon);
            RuleSetDefinition rules = host.CardEditorPanel.editorGame != null ? host.CardEditorPanel.editorGame.DefaultRuleSet : null;
            if (rules == null || !rules.TryGetEntry(entryId, out RuleEntry entry)
                || !RuleSettingKeys.TryParseFloorDeck(FloorDeckValue(entry), out int friends, out int abilities, out int fallTraps))
            {
                return true;
            }

            string sizeText = entry.SettingKey.Substring(RuleSettingKeys.FloorDeckPrefix.Length);
            int x = sizeText.IndexOf('x');
            if (x <= 0 || !int.TryParse(sizeText.Substring(0, x), NumberStyles.None, CultureInfo.InvariantCulture, out int size)) return true;
            int[] parts = { friends, abilities, fallTraps };
            parts[part] = Math.Max(0, parts[part] + step);
            int traps = size * size - MostFloorKeys - MostFloorExits - parts[0] - parts[1];
            string where = $"{DifficultyName(entry)} {size}×{size}";
            if (traps < parts[2])
            {
                ShowDeckEditor($"{where}: {parts[0]}/{parts[1]}/{parts[2]} does not fit. {size * size} tiles hold up to {MostFloorKeys} Keys "
                    + $"and {MostFloorExits} Exits, so Friends and Abilities leave {Math.Max(0, traps)} Traps, fewer than the {parts[2]} Fall Traps.");
                return true;
            }

            string value = $"{parts[0]}/{parts[1]}/{parts[2]}";
            PlayerRuleValue saved = GameValueOf(entryId);
            if (string.Equals(value, entry.Value, StringComparison.Ordinal))
            {
                if (saved != null) host.CardEditorPanel.editorDecks.ruleValues.Remove(saved); // back to the rulebook's
            }
            else if (saved != null) saved.value = value;
            else host.CardEditorPanel.editorDecks.ruleValues.Add(new PlayerRuleValue { entryId = entryId, value = value });

            deckEditorChanged = true;
            ShowDeckEditor($"{where} floor deck (Friends/Abilities/Fall Traps): {value}. Every table uses it from the next one you "
                + "start; a house rule on the rules card still wins.");
            return true;
        }
    }
}
