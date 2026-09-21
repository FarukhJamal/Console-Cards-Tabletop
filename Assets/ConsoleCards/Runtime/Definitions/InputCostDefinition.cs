using System;
using System.Collections.Generic;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [Serializable]
    public sealed class InputRequirement
    {
        [SerializeField] private ControllerInput input;
        [SerializeField, Min(1)] private int count = 1;

        public ControllerInput Input => input;
        public int Count => count;

        internal InputRequirementData ToData()
        {
            return new InputRequirementData(input, count);
        }
    }

    [Serializable]
    public sealed class InputCostDefinition
    {
        [SerializeField] private List<InputRequirement> requirements = new List<InputRequirement>();

        public IReadOnlyList<InputRequirement> Requirements => requirements;

        internal InputCostData ToData()
        {
            List<InputRequirementData> data = new List<InputRequirementData>(requirements.Count);
            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i] == null)
                {
                    throw new InvalidOperationException("Input costs cannot contain null requirements.");
                }

                data.Add(requirements[i].ToData());
            }

            return new InputCostData(data);
        }
    }
}
