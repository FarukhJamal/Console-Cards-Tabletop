using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConsoleCards.GameTemplates.Definitions
{
    /// <summary>
    /// One part of an input cost: <see cref="Count"/> Controller Cards, each showing any one of
    /// <see cref="Inputs"/> (doc 18 §9, C1). A single input is the common case; "A/B/X/Y" lists several.
    /// </summary>
    [Serializable]
    public readonly struct InputRequirementData
    {
        private readonly ControllerInput[] inputs;

        public InputRequirementData(ControllerInput input, int count)
            : this(new[] { input }, count)
        {
        }

        public InputRequirementData(IReadOnlyList<ControllerInput> inputs, int count)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            List<ControllerInput> distinct = new List<ControllerInput>(inputs.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                if (!Enum.IsDefined(typeof(ControllerInput), inputs[i]))
                {
                    throw new ArgumentOutOfRangeException(nameof(inputs));
                }

                if (!distinct.Contains(inputs[i]))
                {
                    distinct.Add(inputs[i]);
                }
            }

            if (distinct.Count == 0)
            {
                throw new ArgumentException("A cost requirement needs at least one input.", nameof(inputs));
            }

            this.inputs = distinct.ToArray();
            Count = count;
        }

        /// <summary>The first accepted input; the only one when the requirement is not a choice.</summary>
        public ControllerInput Input => inputs == null ? default : inputs[0];

        /// <summary>Every input that pays this requirement, in authored order.</summary>
        public IReadOnlyList<ControllerInput> Inputs => inputs ?? Array.Empty<ControllerInput>();

        public int Count { get; }

        /// <summary>True when more than one input pays this requirement ("A/B/X/Y").</summary>
        public bool IsChoice => inputs != null && inputs.Length > 1;

        public bool Accepts(ControllerInput input)
        {
            if (inputs == null)
            {
                return false;
            }

            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] == input)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class InputCostData
    {
        private readonly ReadOnlyCollection<InputRequirementData> requirements;

        public InputCostData(IEnumerable<InputRequirementData> requirements)
        {
            this.requirements = new ReadOnlyCollection<InputRequirementData>(
                new List<InputRequirementData>(requirements ?? throw new ArgumentNullException(nameof(requirements))));
        }

        public IReadOnlyList<InputRequirementData> Requirements => requirements;

        public bool IsEmpty => requirements.Count == 0;

        /// <summary>Total Controller Cards the cost takes.</summary>
        public int CardCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < requirements.Count; i++)
                {
                    count = checked(count + requirements[i].Count);
                }

                return count;
            }
        }

        /// <summary>True when some part of the cost can be paid with this input.</summary>
        public bool Accepts(ControllerInput input)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i].Accepts(input))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
