using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;

namespace ConsoleCards.Presentation.UI
{
    public enum RulesCardLineKind
    {
        Heading = 0,
        Rule = 1,
    }

    /// <summary>One line of the rules card: a section heading or a rule (doc 23, R1).</summary>
    public readonly struct RulesCardLine
    {
        public RulesCardLine(RulesCardLineKind kind, string number, string text, bool changed)
        {
            Kind = kind;
            Number = number ?? string.Empty;
            Text = text ?? string.Empty;
            Changed = changed;
        }

        public RulesCardLineKind Kind { get; }
        public string Number { get; }
        public string Text { get; }
        public bool Changed { get; }
    }

    /// <summary>What the rules card shows: the set's name and its lines for the current mode.</summary>
    public sealed class RulesCardModel
    {
        public RulesCardModel(string setName, bool isHouseRules, IReadOnlyList<RulesCardLine> lines)
        {
            SetName = string.IsNullOrWhiteSpace(setName) ? "Rules" : setName;
            IsHouseRules = isHouseRules;
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }

        public string SetName { get; }
        public bool IsHouseRules { get; }
        public IReadOnlyList<RulesCardLine> Lines { get; }

        /// <summary>
        /// Builds the card for one mode of a rule set: sections in order, rules filtered by mode, setting values in
        /// bold, and a mark on every line that differs from the set it is based on. Null when nothing applies.
        /// </summary>
        public static RulesCardModel FromRuleSet(RuleSetDefinition set, string modeStableId)
        {
            if (set == null)
            {
                return null;
            }

            List<RulesCardLine> lines = new List<RulesCardLine>();
            for (int s = 0; s < set.Sections.Count; s++)
            {
                RuleSection section = set.Sections[s];
                if (section == null)
                {
                    continue;
                }

                int headingIndex = lines.Count;
                int number = 0;
                for (int e = 0; e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry == null || !entry.AppliesToMode(modeStableId) || string.IsNullOrWhiteSpace(entry.Text))
                    {
                        continue;
                    }

                    if (lines.Count == headingIndex && !string.IsNullOrWhiteSpace(section.Heading))
                    {
                        lines.Add(new RulesCardLine(RulesCardLineKind.Heading, string.Empty, section.Heading, false));
                    }

                    number++;
                    lines.Add(new RulesCardLine(
                        RulesCardLineKind.Rule,
                        section.Numbered ? number.ToString() : "•",
                        entry.FormatText("<b>{0}</b>"),
                        RuleSetComparison.IsChanged(set, entry)));
                }
            }

            return lines.Count == 0 ? null : new RulesCardModel(set.DisplayName, !set.IsDefault, lines);
        }
    }
}
