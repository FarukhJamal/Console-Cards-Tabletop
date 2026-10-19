using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ConsoleCards.Definitions
{
    /// <summary>Where dealt cards go (DEAL-1).</summary>
    public enum DealDestination
    {
        Hand = 0,
        Table = 1
    }

    /// <summary>Which way up dealt cards lie on the table (DEAL-1). Cards in a hand are seen by their owner.</summary>
    public enum DealFace
    {
        Down = 0,
        Up = 1
    }

    /// <summary>When the game deals by itself (DEAL-1): only from the Deal action, or also when the game starts.</summary>
    public enum DealTiming
    {
        Manual = 0,
        Setup = 1
    }

    /// <summary>
    /// How a game deals cards (DEAL-1, doc 31 PL-DEAL): how many to each player (0: all of them, evenly), how many
    /// at a time, face, to the hand or the table, and when. Players are dealt in turn order starting left of the
    /// dealer. Written in a game's rules as typed settings (keys <c>deal.*</c>, so the rules card shows them and a
    /// house rule can change them, doc 23), or saved with a game a player made as a short text
    /// ("count=5;packet=1;face=down;to=hand;when=setup").
    /// </summary>
    [Serializable]
    public sealed class DealRule
    {
        public const string CountKey = "deal.count";
        public const string PacketKey = "deal.packet";
        public const string FaceKey = "deal.face";
        public const string ToKey = "deal.to";
        public const string WhenKey = "deal.when";
        public const string MaximumCount = "all";
        public const int MaximumEach = 52;
        public const int MaximumPacket = 5;

        public int CountEach = 5;
        public int Packet = 1;
        public DealFace Face = DealFace.Down;
        public DealDestination To = DealDestination.Hand;
        public DealTiming When = DealTiming.Manual;

        /// <summary>True when every card is dealt (evenly; the rest stay in the pile).</summary>
        public bool DealsAll => CountEach <= 0;

        public DealRule Copy() => new DealRule
        {
            CountEach = CountEach,
            Packet = Packet,
            Face = Face,
            To = To,
            When = When,
        };

        /// <summary>
        /// Reads the deal rule from a rule set's settings for a mode (house rules included). False when the rules
        /// have no <see cref="CountKey"/> setting: the game has no deal rule and the Deal action asks.
        /// </summary>
        public static bool TryRead(RuleSetDefinition rules, string modeStableId, out DealRule rule)
        {
            rule = null;
            if (rules == null) return false;
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int s = 0; s < rules.Sections.Count; s++)
            {
                RuleSection section = rules.Sections[s];
                for (int e = 0; section != null && e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry == null || !entry.HasSetting) continue;
                    string key = entry.SettingKey.Trim();
                    if (!key.StartsWith("deal.", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrEmpty(modeStableId) && !entry.AppliesToMode(modeStableId)) continue;
                    values[key] = entry.Value;
                }
            }

            if (!values.ContainsKey(CountKey)) return false;
            rule = FromValues(values);
            return true;
        }

        /// <summary>Reads the short text saved with a player's game; false (and null) when it is empty.</summary>
        public static bool TryParse(string notation, out DealRule rule)
        {
            rule = null;
            if (string.IsNullOrWhiteSpace(notation)) return false;
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] parts = notation.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                int equals = parts[i].IndexOf('=');
                if (equals <= 0) continue;
                values["deal." + parts[i].Substring(0, equals).Trim()] = parts[i].Substring(equals + 1).Trim();
            }

            if (!values.ContainsKey(CountKey)) return false;
            rule = FromValues(values);
            return true;
        }

        /// <summary>The short text saved with a player's game.</summary>
        public string ToNotation()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "count={0};packet={1};face={2};to={3};when={4}",
                DealsAll ? MaximumCount : CountEach.ToString(CultureInfo.InvariantCulture),
                Packet,
                Face == DealFace.Up ? "up" : "down",
                To == DealDestination.Table ? "table" : "hand",
                When == DealTiming.Setup ? "setup" : "manual");
        }

        private static DealRule FromValues(Dictionary<string, string> values)
        {
            DealRule rule = new DealRule();
            string count = Value(values, CountKey);
            rule.CountEach = string.Equals(count, MaximumCount, StringComparison.OrdinalIgnoreCase)
                ? 0
                : Clamp(ReadInt(count, 5), 1, MaximumEach);
            rule.Packet = Clamp(ReadInt(Value(values, PacketKey), 1), 1, MaximumPacket);
            rule.Face = string.Equals(Value(values, FaceKey), "up", StringComparison.OrdinalIgnoreCase) ? DealFace.Up : DealFace.Down;
            rule.To = string.Equals(Value(values, ToKey), "table", StringComparison.OrdinalIgnoreCase)
                ? DealDestination.Table
                : DealDestination.Hand;
            string when = Value(values, WhenKey);
            rule.When = string.Equals(when, "setup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(when, "start", StringComparison.OrdinalIgnoreCase)
                ? DealTiming.Setup
                : DealTiming.Manual;
            return rule;
        }

        /// <summary>"Deal 5 cards to each player, one at a time, face down on the table, when the game starts."</summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder("Deal ");
            text.Append(DealsAll ? "all the cards evenly" : CountEach == 1 ? "1 card" : $"{CountEach} cards");
            text.Append(" to each player, ");
            text.Append(Packet == 1 ? "one at a time" : $"{Packet} at a time");
            text.Append(To == DealDestination.Table
                ? Face == DealFace.Up ? ", face up on the table" : ", face down on the table"
                : ", into their hand");
            text.Append(When == DealTiming.Setup ? ", when the game starts." : ".");
            return text.ToString();
        }

        /// <summary>A short line for menus: "5 each, face down, to hands".</summary>
        public string Summary()
        {
            string count = DealsAll ? "all, evenly" : $"{CountEach} each";
            string where = To == DealDestination.Table ? (Face == DealFace.Up ? "face up on the table" : "face down on the table") : "to hands";
            string packets = Packet > 1 ? $", {Packet} at a time" : string.Empty;
            return $"{count}, {where}{packets}";
        }

        private static string Value(Dictionary<string, string> values, string key) =>
            values.TryGetValue(key, out string value) ? (value ?? string.Empty).Trim() : string.Empty;

        private static int ReadInt(string text, int fallback) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        private static int Clamp(int value, int minimum, int maximum) => value < minimum ? minimum : value > maximum ? maximum : value;
    }
}
