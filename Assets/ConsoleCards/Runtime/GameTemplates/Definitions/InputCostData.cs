using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConsoleCards.GameTemplates.Definitions
{
    [Serializable]
    public readonly struct InputRequirementData
    {
        public InputRequirementData(ControllerInput input, int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Input = input;
            Count = count;
        }

        public ControllerInput Input { get; }

        public int Count { get; }
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
    }
}
