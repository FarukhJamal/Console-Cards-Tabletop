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
        public RulesCardLine(RulesCardLineKind kind, string number, string text, bool changed, string entryStableId = null)
        {
            Kind = kind;
            Number = number ?? string.Empty;
            Text = text ?? string.Empty;
            Changed = changed;
            EntryStableId = entryStableId ?? string.Empty;
        }

        public RulesCardLineKind Kind { get; }
        public string Number { get; }
        public string Text { get; }
        public bool Changed { get; }
        /// <summary>The rule's stable ID, for "Propose change" (doc 23, R3). Empty for headings.</summary>
        public string EntryStableId { get; }
    }

    /// <summary>What players can do from the rules card (doc 23, R3). Null members hide their controls.</summary>
    public sealed class RulesCardActions
    {
        /// <summary>Opens the proposal for one rule, by its stable ID.</summary>
        public Action<string> ProposeChange;
        /// <summary>Proposes switching to the next saved rule set (+1) or the previous one (-1).</summary>
        public Action<int> CycleRuleSet;
        /// <summary>Restarts the game so changed settings take effect.</summary>
        public Action Restart;
    }

    /// <summary>What the rules card shows: the set's name and its lines for the current mode.</summary>
    public sealed class RulesCardModel
    {
        public RulesCardModel(string setName, bool isHouseRules, IReadOnlyList<RulesCardLine> lines)
        {
            SetName = string.IsNullOrWhiteSpace(setName) ? "Rules" : setName;
            IsHouseRules = isHouseRules;
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            PendingText = string.Empty;
        }

        /// <summary>Optional R3 controls; null shows the card read-only, as in R1.</summary>
        public RulesCardActions Actions { get; private set; }
        /// <summary>How many rule sets can be picked (default plus saved house rules).</summary>
        public int RuleSetCount { get; private set; } = 1;
        /// <summary>Shown when the rules differ from the ones the running game was set up with; empty hides it.</summary>
        public string PendingText { get; private set; }

        public RulesCardModel WithActions(RulesCardActions actions, int ruleSetCount, string pendingText)
        {
            Actions = actions;
            RuleSetCount = Math.Max(1, ruleSetCount);
            PendingText = pendingText ?? string.Empty;
            return this;
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
                        RuleSetComparison.IsChanged(set, entry),
                        entry.StableId));
                }
            }

            return lines.Count == 0 ? null : new RulesCardModel(set.DisplayName, !set.IsDefault, lines);
        }
    }
}
