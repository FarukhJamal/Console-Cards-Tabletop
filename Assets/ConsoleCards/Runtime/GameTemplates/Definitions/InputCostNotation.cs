using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ConsoleCards.GameTemplates.Definitions
{
    /// <summary>
    /// The written form of an input cost, as printed on the cards (doc 23 §5, C1). Parts are joined with "+";
    /// "/" lists inputs where any one pays; "x2" repeats a part. Examples: "Up + A/B/X/Y",
    /// "Left/Right + A/B/X/Y", "A + B + X + Y", "Right x2 + A". An empty text is an empty cost.
    /// </summary>
    public static class InputCostNotation
    {
        public static string Format(InputCostData cost)
        {
            if (cost == null || cost.IsEmpty)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < cost.Requirements.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" + ");
                }

                AppendRequirement(builder, cost.Requirements[i]);
            }

            return builder.ToString();
        }

        public static string Format(InputRequirementData requirement)
        {
            StringBuilder builder = new StringBuilder();
            AppendRequirement(builder, requirement);
            return builder.ToString();
        }

        public static bool TryParse(string text, out InputCostData cost, out string error)
        {
            cost = null;
            error = string.Empty;
            List<InputRequirementData> requirements = new List<InputRequirementData>();
            string trimmed = text == null ? string.Empty : text.Trim();
            if (trimmed.Length == 0)
            {
                cost = new InputCostData(requirements);
                return true;
            }

            string[] parts = trimmed.Split('+');
            for (int p = 0; p < parts.Length; p++)
            {
                string part = parts[p].Trim();
                int count = 1;
                int marker = FindCountMarker(part);
                if (marker >= 0)
                {
                    string countText = part.Substring(marker + 1).Trim();
                    if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1)
                    {
                        error = $"'{parts[p].Trim()}' has an unreadable count";
                        return false;
                    }

                    part = part.Substring(0, marker).Trim();
                }

                if (part.Length == 0)
                {
                    error = "a part of the cost is empty";
                    return false;
                }

                string[] names = part.Split('/');
                List<ControllerInput> inputs = new List<ControllerInput>(names.Length);
                for (int n = 0; n < names.Length; n++)
                {
                    string name = names[n].Trim();
                    if (name.Length == 0
                        || !Enum.TryParse(name, true, out ControllerInput input)
                        || !Enum.IsDefined(typeof(ControllerInput), input)
                        || int.TryParse(name, out _))
                    {
                        error = $"'{name}' is not a Controller input (Up, Down, Left, Right, A, B, X, Y)";
                        return false;
                    }

                    inputs.Add(input);
                }

                requirements.Add(new InputRequirementData(inputs, count));
            }

            cost = new InputCostData(requirements);
            return true;
        }

        private static void AppendRequirement(StringBuilder builder, InputRequirementData requirement)
        {
            IReadOnlyList<ControllerInput> inputs = requirement.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('/');
                }

                builder.Append(inputs[i].ToString());
            }

            if (requirement.Count > 1)
            {
                builder.Append(" x").Append(requirement.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        // The "x" or "×" before a trailing count, as in "Right x2"; -1 when the part has no count.
        private static int FindCountMarker(string part)
        {
            for (int i = part.Length - 1; i > 0; i--)
            {
                char c = part[i];
                if (char.IsDigit(c) || char.IsWhiteSpace(c))
                {
                    continue;
                }

                if ((c == 'x' || c == 'X' || c == '×') && i < part.Length - 1 && HasDigit(part, i + 1)
                    && (char.IsWhiteSpace(part[i - 1]) || c == '×'))
                {
                    return i;
                }

                return -1;
            }

            return -1;
        }

        private static bool HasDigit(string text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
