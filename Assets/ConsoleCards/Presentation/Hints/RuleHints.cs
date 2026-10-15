using System.Collections.Generic;
using ConsoleCards.Definitions;

namespace ConsoleCards.Presentation.Hints
{
    /// <summary>
    /// Game hints made from the rules (doc 23 §10, H2): every rule line with a hint tag becomes a tip with the
    /// rule's own words and current value, so a house rule changes its hint too. A hint keeps the rule's ID, so
    /// "Got it" on a rule holds across house-rule sets. Works for any game; no game code needed.
    /// </summary>
    public static class RuleHints
    {
        public const int ShowsPerRule = 2;

        public static List<HintEntry> Collect(RuleSetDefinition rules, string modeStableId, string gameStableId)
        {
            List<HintEntry> hints = new List<HintEntry>();
            if (rules == null) return hints;
            for (int s = 0; s < rules.Sections.Count; s++)
            {
                RuleSection section = rules.Sections[s];
                if (section == null) continue;
                for (int e = 0; e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry == null || !entry.HasHint || !entry.AppliesToMode(modeStableId)) continue;
                    hints.Add(new HintEntry(
                        $"rule.{gameStableId}.{entry.StableId}",
                        entry.HintTrigger,
                        entry.HintContext,
                        entry.HintText,
                        HintKind.Tip,
                        entry.HintPriority,
                        ShowsPerRule));
                }
            }

            return hints;
        }

        /// <summary>
        /// The hint helpers the rules in play name (H3), each once. A rule may name a helper with or without a
        /// hint trigger of its own.
        /// </summary>
        public static List<string> CollectHelpers(RuleSetDefinition rules, string modeStableId)
        {
            List<string> helperIds = new List<string>();
            if (rules == null) return helperIds;
            for (int s = 0; s < rules.Sections.Count; s++)
            {
                RuleSection section = rules.Sections[s];
                if (section == null) continue;
                for (int e = 0; e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry == null || !entry.AppliesToMode(modeStableId)) continue;
                    string helper = entry.HintHelper;
                    if (helper.Length > 0 && !helperIds.Contains(helper)) helperIds.Add(helper);
                }
            }

            return helperIds;
        }
    }
}
