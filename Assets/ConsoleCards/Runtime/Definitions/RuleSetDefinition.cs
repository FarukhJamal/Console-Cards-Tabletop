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

        public string StableId => stableId;
        public string Text => text ?? string.Empty;
        public RuleSettingKind SettingKind => settingKind;
        public string SettingKey => settingKey ?? string.Empty;
        public string Value => value ?? string.Empty;
        public string DisplayValue => string.IsNullOrWhiteSpace(displayValue) ? Value : displayValue;
        public IReadOnlyList<string> ModeStableIds => modeStableIds;
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
