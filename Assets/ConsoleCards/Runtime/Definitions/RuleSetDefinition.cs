using System;
using System.Collections.Generic;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    /// <summary>The kind of typed setting a rule entry carries (doc 23). None means the rule is text only.</summary>
    public enum RuleSettingKind
    {
        None = 0,
        Number = 1,
        Toggle = 2,
        Choice = 3,
        Text = 4,
    }

    /// <summary>
    /// One rule as players read it, with an optional typed setting the game can read (doc 23, R1). The text may
    /// contain {value}, replaced by the setting's value. An entry can be limited to some modes (for example a
    /// goal line per difficulty); an empty mode list means every mode.
    /// </summary>
    [Serializable]
    public sealed class RuleEntry
    {
        public const string ValueToken = "{value}";

        [SerializeField] private string stableId;
        [SerializeField, TextArea(2, 6)] private string text;
        [SerializeField] private RuleSettingKind settingKind;
        [SerializeField] private string settingKey;
        [SerializeField] private string value;
        [Tooltip("Optional friendlier text shown on the rules card instead of the raw value (e.g. 'A, B, X or Y' for 'A,B,X,Y').")]
        [SerializeField] private string displayValue;
        [SerializeField] private List<string> modeStableIds = new List<string>();
        [Tooltip("Choice settings: the values players can pick from when they propose a change (doc 23, R3).")]
        [SerializeField] private List<string> choices = new List<string>();

        public string StableId => stableId;
        public string Text => text ?? string.Empty;
        public RuleSettingKind SettingKind => settingKind;
        public string SettingKey => settingKey ?? string.Empty;
        public string Value => value ?? string.Empty;
        public string DisplayValue => string.IsNullOrWhiteSpace(displayValue) ? Value : displayValue;
        public IReadOnlyList<string> ModeStableIds => modeStableIds;
        public IReadOnlyList<string> Choices => choices ?? (IReadOnlyList<string>)Array.Empty<string>();
        public bool HasSetting => settingKind != RuleSettingKind.None;

        public bool AppliesToMode(string modeStableId)
        {
            if (modeStableIds == null || modeStableIds.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < modeStableIds.Count; i++)
            {
                if (string.Equals(modeStableIds[i], modeStableId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A copy of this entry with a house-rule change (doc 23, R3): new text and/or value. A changed value drops
        /// the authored display value, which described the old value.
        /// </summary>
        public RuleEntry WithChange(string newText, string newValue)
        {
            bool valueChanged = newValue != null && !string.Equals(newValue, Value, StringComparison.Ordinal);
            return new RuleEntry
            {
                stableId = stableId,
                text = newText ?? text,
                settingKind = settingKind,
                settingKey = settingKey,
                value = newValue ?? value,
                displayValue = valueChanged ? string.Empty : displayValue,
                modeStableIds = new List<string>(modeStableIds ?? new List<string>()),
                choices = new List<string>(choices ?? new List<string>()),
            };
        }

        /// <summary>The text with {value} replaced; <paramref name="valueFormat"/> wraps the value (e.g. bold).</summary>
        public string FormatText(string valueFormat = "{0}")
        {
            string body = Text;
            if (!HasSetting || body.IndexOf(ValueToken, StringComparison.Ordinal) < 0)
            {
                return body;
            }

            return body.Replace(ValueToken, string.Format(valueFormat, DisplayValue));
        }
    }

    /// <summary>A titled group of rules (for example "Each round" or "Goal").</summary>
    [Serializable]
    public sealed class RuleSection
    {
        [SerializeField] private string heading;
        [SerializeField] private bool numbered = true;
        [SerializeField] private List<RuleEntry> entries = new List<RuleEntry>();

        /// <summary>A copy of this section with some entries replaced, by stable ID (doc 23, R3).</summary>
        public RuleSection WithChanges(IReadOnlyDictionary<string, RuleEntryChange> changes)
        {
            RuleSection copy = new RuleSection { heading = heading, numbered = numbered, entries = new List<RuleEntry>() };
            for (int i = 0; i < entries.Count; i++)
            {
                RuleEntry entry = entries[i];
                if (entry == null) continue;
                copy.entries.Add(changes != null && changes.TryGetValue(entry.StableId ?? string.Empty, out RuleEntryChange change)
                    ? entry.WithChange(change.Text, change.Value)
                    : entry.WithChange(null, null));
            }

            return copy;
        }

        public string Heading => heading ?? string.Empty;
        public bool Numbered => numbered;
        public IReadOnlyList<RuleEntry> Entries => entries;
    }

    /// <summary>
    /// A game's rules as data (doc 23). The set shipped in the game box is the default set and is never
    /// overwritten; a house-rule set names the set it is based on, and its changed lines are found by
    /// <see cref="RuleSetComparison"/>. R1 only shows rules; nothing here is enforced.
    /// </summary>
    [CreateAssetMenu(fileName = "RuleSet", menuName = "Console Cards/Definitions/Rule Set")]
    public sealed class RuleSetDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName = "Default";
        [SerializeField] private string gameStableId;
        [SerializeField] private RuleSetDefinition basedOn;
        [SerializeField] private List<RuleSection> sections = new List<RuleSection>();

        public string StableId => stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Rules" : displayName;
        public string GameStableId => gameStableId;
        public RuleSetDefinition BasedOn => basedOn;
        public bool IsDefault => basedOn == null;
        public IReadOnlyList<RuleSection> Sections => sections;

        /// <summary>The typed settings of this set, for <see cref="RuleSettingsApplication"/> (doc 23, R2a).</summary>
        public List<RuleSettingValue> CollectSettings()
        {
            List<RuleSettingValue> settings = new List<RuleSettingValue>();
            for (int s = 0; s < sections.Count; s++)
            {
                RuleSection section = sections[s];
                if (section == null)
                {
                    continue;
                }

                for (int e = 0; e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry != null && entry.HasSetting && !string.IsNullOrWhiteSpace(entry.SettingKey))
                    {
                        settings.Add(new RuleSettingValue(entry.SettingKey, entry.Value, entry.ModeStableIds));
                    }
                }
            }

            return settings;
        }

        /// <summary>
        /// A house-rule set (doc 23, R3): a copy of <paramref name="basedOn"/> with the given entries changed. It is
        /// made at runtime and saved on this computer, never as an asset; the default set is never changed.
        /// </summary>
        public static RuleSetDefinition CreateHouseRules(
            RuleSetDefinition basedOn,
            string stableId,
            string displayName,
            IReadOnlyDictionary<string, RuleEntryChange> changes)
        {
            if (basedOn == null) throw new ArgumentNullException(nameof(basedOn));
            RuleSetDefinition set = CreateInstance<RuleSetDefinition>();
            set.name = displayName;
            set.hideFlags = HideFlags.DontSave;
            set.stableId = stableId;
            set.displayName = displayName;
            set.gameStableId = basedOn.gameStableId;
            set.basedOn = basedOn;
            set.sections = new List<RuleSection>();
            for (int i = 0; i < basedOn.sections.Count; i++)
            {
                if (basedOn.sections[i] != null) set.sections.Add(basedOn.sections[i].WithChanges(changes));
            }

            return set;
        }

        public bool TryGetEntry(string entryStableId, out RuleEntry entry)
        {
            for (int s = 0; s < sections.Count; s++)
            {
                RuleSection section = sections[s];
                if (section == null)
                {
                    continue;
                }

                for (int e = 0; e < section.Entries.Count; e++)
                {
                    RuleEntry candidate = section.Entries[e];
                    if (candidate != null && string.Equals(candidate.StableId, entryStableId, StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }
    }

    /// <summary>One house-rule change to an entry: its new text and/or value (null keeps it).</summary>
    [Serializable]
    public readonly struct RuleEntryChange
    {
        public RuleEntryChange(string text, string value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }
        public string Value { get; }
    }

    /// <summary>Finds which rules of a set differ from the set it is based on (doc 23).</summary>
    public static class RuleSetComparison
    {
        /// <summary>
        /// True when the entry is new or its text or value differs from the same entry in the based-on set.
        /// Entries of a default set are never changed.
        /// </summary>
        public static bool IsChanged(RuleSetDefinition set, RuleEntry entry)
        {
            if (set == null || entry == null || set.BasedOn == null)
            {
                return false;
            }

            if (!set.BasedOn.TryGetEntry(entry.StableId, out RuleEntry original))
            {
                return true;
            }

            return !string.Equals(original.Text, entry.Text, StringComparison.Ordinal)
                || !string.Equals(original.Value, entry.Value, StringComparison.Ordinal)
                || original.SettingKind != entry.SettingKind;
        }
    }
}
