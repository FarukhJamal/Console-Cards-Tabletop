using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// MP-P (owner, 2026-10-27: "the hints are too big… button text"): keeps words inside their box. Shrink lets a
    /// label get smaller (down to 60 % of its size) instead of running past its button or panel; Concise shortens a
    /// long hint to its first sentence (no brackets), and cuts it at a word if it is still too long.
    /// </summary>
    public static class FitText
    {
        private static readonly StringBuilder Builder = new StringBuilder(256);

        public static void Shrink(Text label)
        {
            if (label == null || label.resizeTextForBestFit) return;
            int size = Mathf.Max(1, label.fontSize);
            label.resizeTextMaxSize = size;
            label.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.6f));
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
        }

        public static string Concise(string text, int maxCharacters)
        {
            if (string.IsNullOrEmpty(text) || Visible(text) <= maxCharacters) return text ?? string.Empty;
            Builder.Clear();
            int depth = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') inTag = true;
                if (inTag)
                {
                    Builder.Append(c);
                    if (c == '>') inTag = false;
                    continue;
                }

                if (c == '(') { depth++; continue; }
                if (c == ')') { if (depth > 0) depth--; continue; }
                if (depth > 0) continue;
                Builder.Append(c);
                bool sentenceEnd = (c == '.' || c == '!' || c == '?')
                    && (i + 1 >= text.Length || text[i + 1] == ' ' || text[i + 1] == '\n');
                if (sentenceEnd && Visible(Builder) >= 12) break;
            }

            string sentence = Builder.ToString().Replace("  ", " ").Replace(" .", ".").Trim();
            if (Visible(sentence) <= maxCharacters) return sentence;
            int cut = sentence.LastIndexOf(' ', Mathf.Min(sentence.Length - 1, maxCharacters));
            if (cut <= 0) cut = Mathf.Min(sentence.Length, maxCharacters);
            return sentence.Substring(0, cut).TrimEnd(',', ';', ':', ' ') + "…";
        }

        private static int Visible(string text)
        {
            int count = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag) count++;
            }

            return count;
        }

        private static int Visible(StringBuilder text)
        {
            int count = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag) count++;
            }

            return count;
        }
    }
}
