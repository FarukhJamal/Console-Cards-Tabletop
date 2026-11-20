using System;
using System.Collections.Generic;

namespace ConsoleCards.Definitions
{
    /// <summary>RULES-SHEET (R1): one filled-in field of a game's rules sheet, by its key. An empty value: players decide.</summary>
    [Serializable]
    public sealed class SheetValue
    {
        public string key = string.Empty;
        public string value = string.Empty;

        public SheetValue()
        {
        }

        public SheetValue(string key, string value)
        {
            this.key = key ?? string.Empty;
            this.value = value ?? string.Empty;
        }
    }

    /// <summary>RULES-SHEET (R1): a section's written rules (one rule per line); only players follow them.</summary>
    [Serializable]
    public sealed class SheetText
    {
        public int section;
        public string text = string.Empty;

        public SheetText()
        {
        }

        public SheetText(int section, string text)
        {
            this.section = section;
            this.text = text ?? string.Empty;
        }
    }

    /// <summary>RULES-SHEET (R1): one field of the sheet: its section, key, kind, rulebook sentence and choices.</summary>
    public readonly struct SheetField
    {
        public SheetField(int section, string key, string label, RuleSettingKind kind, string text, params string[] choices)
        {
            Section = section;
            Key = key;
            Label = label;
            Kind = kind;
            Text = text;
            Choices = choices ?? Array.Empty<string>();
        }

        public int Section { get; }
        public string Key { get; }

        /// <summary>The field's name in the sheet editor.</summary>
        public string Label { get; }

        public RuleSettingKind Kind { get; }

        /// <summary>The rulebook sentence; {value} is the field's value.</summary>
        public string Text { get; }

        public IReadOnlyList<string> Choices { get; }
    }

    /// <summary>
    /// RULES-SHEET (R1; "Console Cards — Game Rules and Templates", 2026-11-18): every game a player makes has one
    /// rules sheet of ten sections. Each section has fields (values the game reads, and at the stricter assist levels
    /// checks or applies) and written text (shown in the rulebook and on the rules card; only players follow it). An
    /// empty field means "players decide" and is left out of the rules. The sheet is built into a
    /// <see cref="RuleSetDefinition"/> at runtime, so a made game's rules show, and later change by house rule, the
    /// same way Trap Floor's do, with the same {value} sentences.
    /// </summary>
    public static class RulesSheet
    {
        public const int Basics = 0;
        public const int Components = 1;
        public const int Decks = 2;
        public const int Setup = 3;
        public const int Turn = 4;
        public const int Limits = 5;
        public const int Actions = 6;
        public const int CardEffects = 7;
        public const int Scoring = 8;
        public const int HouseRules = 9;

        public static readonly string[] SectionHeadings =
        {
            "Basics", "Components", "Decks", "Setup", "Your turn", "Limits", "Actions", "Card effects",
            "Scoring and winning", "House rules",
        };

        public static readonly SheetField[] Fields =
        {
            new SheetField(Basics, "sheet.age", "Age guide", RuleSettingKind.Text, "For ages {value}."),
            new SheetField(Basics, "sheet.template", "Table", RuleSettingKind.Choice,
                "The table is {value}.", "fixed: pieces start in place and the board stays put", "open: players move anything freely"),
            new SheetField(Basics, "sheet.assist", "Assist levels allowed", RuleSettingKind.Choice,
                "Assist levels the host may pick: {value}.", "Free", "Free, Reminders", "Free, Reminders, Full"),

            new SheetField(Components, "sheet.mapper.grid", "Mapper grid", RuleSettingKind.Choice,
                "The mapper uses a {value} grid.", "hex", "square", "rectangle"),
            new SheetField(Components, "sheet.mapper.size", "Mapper size", RuleSettingKind.Text, "The mapper is {value}."),
            new SheetField(Components, "sheet.pawns", "Pawns per player", RuleSettingKind.Number, "Each player has {value} pawn(s)."),
            new SheetField(Components, "sheet.tokens", "Tokens", RuleSettingKind.Text, "Tokens: {value}."),
            new SheetField(Components, "sheet.dice", "Dice", RuleSettingKind.Text, "Dice: {value}."),
            new SheetField(Components, "sheet.zones", "Player zones", RuleSettingKind.Text, "Each player has {value}."),

            new SheetField(Decks, "sheet.deck.face", "Draw pile", RuleSettingKind.Choice,
                "The draw pile lies {value}.", "face down", "face up"),
            new SheetField(Decks, "sheet.deck.shuffle", "Shuffled at the start", RuleSettingKind.Choice,
                "Shuffle the draw pile at the start: {value}.", "yes", "no"),
            new SheetField(Decks, "sheet.deck.sees", "Who may look at the draw pile", RuleSettingKind.Choice,
                "Who may look at the draw pile: {value}.", "nobody", "everyone"),

            new SheetField(Setup, "sheet.setup.start", "Each player starts with", RuleSettingKind.Text, "Each player starts with {value}."),
            new SheetField(Setup, "sheet.first", "First player", RuleSettingKind.Choice,
                "The first player is {value}.", "chosen at random", "the youngest", "the game's creator", "the highest roll"),

            new SheetField(Turn, "sheet.turn.order", "Turn order", RuleSettingKind.Choice,
                "Turns go {value}.", "clockwise", "counter-clockwise", "at the same time"),
            new SheetField(Turn, "sheet.turn.phases", "Phases", RuleSettingKind.Text, "Each turn: {value}."),
            new SheetField(Turn, "sheet.turn.seconds", "Time limit (seconds)", RuleSettingKind.Number, "A turn may last {value} seconds."),

            new SheetField(Limits, "sheet.limit.draws", "Draws per turn", RuleSettingKind.Number, "Draw at most {value} card(s) a turn."),
            new SheetField(Limits, "sheet.limit.plays", "Cards played per turn", RuleSettingKind.Number, "Play at most {value} card(s) a turn."),
            new SheetField(Limits, "sheet.limit.actions", "Actions per turn", RuleSettingKind.Number, "Take at most {value} action(s) a turn."),
            new SheetField(Limits, "sheet.limit.hand", "Hand size", RuleSettingKind.Number, "Hold at most {value} cards."),
            new SheetField(Limits, "sheet.limit.hand.check", "Hand size checked", RuleSettingKind.Choice,
                "The hand size is checked {value}.", "at the end of your turn", "at any time"),
            new SheetField(Limits, "sheet.deck.empty", "When the draw pile runs out", RuleSettingKind.Choice,
                "When the draw pile runs out, {value}.", "shuffle the discard pile into a new draw pile", "the game ends", "nothing happens"),

            new SheetField(Actions, "sheet.actions", "Allowed actions", RuleSettingKind.Text, "On your turn you may: {value}."),

            new SheetField(CardEffects, "sheet.effects", "Card effects", RuleSettingKind.Text, "Card effects: {value}."),

            new SheetField(Scoring, "sheet.score", "Score kept with", RuleSettingKind.Choice,
                "Score is kept with {value}.", "nothing", "points", "tokens"),
            new SheetField(Scoring, "sheet.win", "Winner", RuleSettingKind.Choice,
                "The winner is {value}.", "the first to empty their hand", "the first to reach the target", "the last player left",
                "the first to reach the goal space"),
            new SheetField(Scoring, "sheet.win.target", "Target", RuleSettingKind.Number, "The target is {value}."),
            new SheetField(Scoring, "sheet.end", "The game ends when", RuleSettingKind.Text, "The game ends when {value}."),

            new SheetField(HouseRules, "sheet.house.fields", "Players may change", RuleSettingKind.Text,
                "Players may change at the table: {value}."),
            new SheetField(HouseRules, "sheet.house.vote", "A change needs", RuleSettingKind.Choice,
                "A change needs {value}.", "a vote", "the host's say"),
        };

        /// <summary>The field with this key, if the sheet has one.</summary>
        public static bool TryGetField(string key, out SheetField field)
        {
            for (int i = 0; i < Fields.Length; i++)
            {
                if (string.Equals(Fields[i].Key, key, StringComparison.Ordinal))
                {
                    field = Fields[i];
                    return true;
                }
            }

            field = default;
            return false;
        }

        /// <summary>The value filled in for <paramref name="key"/>, or empty (players decide).</summary>
        public static string ValueOf(IReadOnlyList<SheetValue> values, string key)
        {
            if (values == null) return string.Empty;
            for (int i = 0; i < values.Count; i++)
            {
                SheetValue value = values[i];
                if (value != null && string.Equals(value.key, key, StringComparison.Ordinal)) return value.value?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>True when any field is filled in or any section has written text.</summary>
        public static bool HasContent(IReadOnlyList<SheetValue> values, IReadOnlyList<SheetText> texts)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (values[i] != null && TryGetField(values[i].key, out _) && !string.IsNullOrWhiteSpace(values[i].value)) return true;
            }

            for (int i = 0; texts != null && i < texts.Count; i++)
            {
                if (texts[i] != null && !string.IsNullOrWhiteSpace(texts[i].text)) return true;
            }

            return false;
        }

        /// <summary>
        /// The sheet as a rule set (made at runtime, never saved as an asset; the caller destroys it when done).
        /// <paramref name="basics"/> are lines put first in Basics (players, play time), already written out. Each
        /// filled-in field is one rule with its sentence; each section's written text follows its fields, one rule
        /// per line. Sections with nothing in them are left out.
        /// </summary>
        public static RuleSetDefinition Build(
            string stableId,
            string displayName,
            string gameStableId,
            IReadOnlyList<string> basics,
            IReadOnlyList<SheetValue> values,
            IReadOnlyList<SheetText> texts)
        {
            List<RuleSection> sections = new List<RuleSection>(SectionHeadings.Length);
            for (int s = 0; s < SectionHeadings.Length; s++)
            {
                List<RuleEntry> entries = new List<RuleEntry>();
                if (s == Basics && basics != null)
                {
                    for (int i = 0; i < basics.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(basics[i]))
                        {
                            entries.Add(RuleEntry.Create("sheet.basics." + i, basics[i].Trim(), RuleSettingKind.None, string.Empty, string.Empty));
                        }
                    }
                }

                for (int f = 0; f < Fields.Length; f++)
                {
                    SheetField field = Fields[f];
                    if (field.Section != s) continue;
                    string value = ValueOf(values, field.Key);
                    if (value.Length == 0) continue; // players decide
                    entries.Add(RuleEntry.Create(field.Key, field.Text, field.Kind, field.Key, value, field.Choices));
                }

                for (int t = 0; texts != null && t < texts.Count; t++)
                {
                    SheetText text = texts[t];
                    if (text == null || text.section != s || string.IsNullOrWhiteSpace(text.text)) continue;
                    string[] lines = text.text.Split('\n');
                    for (int l = 0; l < lines.Length; l++)
                    {
                        string line = lines[l].Trim();
                        if (line.Length == 0) continue;
                        entries.Add(RuleEntry.Create("sheet.text." + s + "." + l, line, RuleSettingKind.None, string.Empty, string.Empty));
                    }
                }

                if (entries.Count > 0) sections.Add(RuleSection.Create(SectionHeadings[s], true, entries));
            }

            return sections.Count == 0 ? null : RuleSetDefinition.CreateRuntime(stableId, displayName, gameStableId, sections);
        }
    }
}
