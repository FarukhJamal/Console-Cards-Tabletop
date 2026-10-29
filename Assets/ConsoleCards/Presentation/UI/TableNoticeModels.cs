using System;
using System.Text;
using ConsoleCards.Core.Identifiers;
using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// POP-2 (owner, 2026-10-29; doc 38 "Popups v2"): finds where a piece lies on the table, so a notice can sit
    /// beside it. False when the piece has no view on the table now.
    /// </summary>
    public delegate bool TableAnchorResolver(TabletopObjectId objectId, out Vector3 worldPosition);

    /// <summary>The colour family of a table notice (POP-2). Each view maps a tone to its authored colours.</summary>
    public enum TableNoticeTone
    {
        Info = 0,
        Danger = 1,
        Caution = 2,
        Reward = 3,
        Ability = 4,
        Friend = 5,
        Won = 6,
    }

    /// <summary>
    /// The floor's end as a small chip at the top centre (POP-2, owner 2026-10-29: "Starting floor 2 is too big"): a tag
    /// (FLOOR 1 WON), the next floor, an optional countdown that presses the main button by itself, and a way out.
    /// </summary>
    public sealed class TableMilestoneModel
    {
        public TableMilestoneModel(
            string tag,
            TableNoticeTone tone,
            string title,
            string detail,
            string primaryLabel,
            Action primary,
            string secondaryLabel,
            Action secondary,
            float autoContinueSeconds)
        {
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("A milestone needs a tag.", nameof(tag));
            if (string.IsNullOrWhiteSpace(primaryLabel)) throw new ArgumentException("A milestone needs a main button.", nameof(primaryLabel));
            Tag = tag;
            Tone = tone;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            PrimaryLabel = primaryLabel;
            Primary = primary ?? throw new ArgumentNullException(nameof(primary));
            SecondaryLabel = secondaryLabel ?? string.Empty;
            Secondary = secondary;
            AutoContinueSeconds = Mathf.Max(0f, autoContinueSeconds);
        }

        public string Tag { get; }
        public TableNoticeTone Tone { get; }
        public string Title { get; }
        public string Detail { get; }
        public string PrimaryLabel { get; }
        public Action Primary { get; }
        public string SecondaryLabel { get; }
        public Action Secondary { get; }

        /// <summary>Seconds before the main button presses itself; 0: it waits for the player.</summary>
        public float AutoContinueSeconds { get; }
    }

    /// <summary>
    /// Text helpers for the POP-2 notices: the short tag of a reveal, its tone, and a warning split into a bold
    /// head and a short rest. Games keep writing their messages as before; these only shape them for the chips.
    /// </summary>
    public static class TableNoticeText
    {
        /// <summary>"TRAP • NOW" → "TRAP · NOW".</summary>
        public static string Tag(string resultState)
        {
            if (string.IsNullOrWhiteSpace(resultState)) return string.Empty;
            return resultState.Replace('•', '·').Trim().ToUpperInvariant();
        }

        /// <summary>The tone of a reveal from its words (a Trap now is danger, a Key a reward, and so on).</summary>
        public static TableNoticeTone ToneOf(string headerTitle, string resultState)
        {
            string state = (resultState ?? string.Empty).ToUpperInvariant();
            string header = (headerTitle ?? string.Empty).ToUpperInvariant();
            if (header.Contains("COLLAPSE")) return TableNoticeTone.Danger;
            if (state.Contains("TRAP"))
            {
                return state.Contains("NEXT ROUND") ? TableNoticeTone.Caution : TableNoticeTone.Danger;
            }

            if (state.Contains("KEY")) return TableNoticeTone.Reward;
            if (state.Contains("FRIEND")) return TableNoticeTone.Friend;
            if (state.Contains("ABILITY")) return TableNoticeTone.Ability;
            return TableNoticeTone.Info;
        }

        /// <summary>
        /// True when the warning tells the player to press Undo ("Press Undo …", "Press the Undo button …", "… or Undo
        /// the roll"); not for Undo's own messages ("Undo rejected: …").
        /// </summary>
        public static bool MentionsUndo(string message)
        {
            if (string.IsNullOrEmpty(message) || message.StartsWith("Undo", StringComparison.Ordinal)) return false;
            return message.IndexOf("Press Undo", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("Press the Undo", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("or Undo", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Splits a warning for the yellow chip: the head is the words before the first ':' (or the first sentence),
        /// the rest is what follows. With an Undo button on the chip, sentences that only say "press Undo" are
        /// dropped (the full line stays in the Log).
        /// </summary>
        public static void SplitWarning(string message, bool undoButtonShown, out string head, out string rest)
        {
            head = string.Empty;
            rest = string.Empty;
            if (string.IsNullOrWhiteSpace(message)) return;
            string text = message.Trim();
            int colon = text.IndexOf(':');
            int stop = text.IndexOf(". ", StringComparison.Ordinal);
            if (colon > 0 && colon < 48 && (stop < 0 || colon < stop))
            {
                head = text.Substring(0, colon).Trim();
                rest = text.Substring(colon + 1).Trim();
            }
            else if (stop > 0 && stop < 64)
            {
                head = text.Substring(0, stop).Trim();
                rest = text.Substring(stop + 1).Trim();
            }
            else
            {
                head = text;
            }

            if (undoButtonShown) rest = DropUndoSentences(rest);
        }

        private static string DropUndoSentences(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder kept = new StringBuilder(text.Length);
            int start = 0;
            while (start < text.Length)
            {
                int end = text.IndexOf(". ", start, StringComparison.Ordinal);
                int next = end < 0 ? text.Length : end + 1;
                string sentence = text.Substring(start, next - start).Trim();
                bool undoOnly = sentence.IndexOf("Undo", StringComparison.Ordinal) >= 0;
                if (undoOnly && sentence.IndexOf("Keep move", StringComparison.Ordinal) >= 0)
                {
                    sentence = "Or choose Keep move in the turn menu.";
                    undoOnly = false;
                }

                if (!undoOnly && sentence.Length > 0)
                {
                    if (kept.Length > 0) kept.Append(' ');
                    kept.Append(sentence);
                }

                start = next;
            }

            return kept.ToString();
        }
    }
}
