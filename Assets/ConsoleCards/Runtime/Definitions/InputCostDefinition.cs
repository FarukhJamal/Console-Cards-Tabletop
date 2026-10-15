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
        [Tooltip("Other inputs that also pay this part, as in A/B/X/Y on the card. Leave empty for a single input.")]
        [SerializeField] private List<ControllerInput> alsoAccepts = new List<ControllerInput>();
        [SerializeField, Min(1)] private int count = 1;

        public ControllerInput Input => input;
        public IReadOnlyList<ControllerInput> AlsoAccepts => alsoAccepts;
        public int Count => count;

        internal static InputRequirement FromData(InputRequirementData data)
        {
            InputRequirement requirement = new InputRequirement { input = data.Input, count = Math.Max(1, data.Count) };
            for (int i = 1; i < data.Inputs.Count; i++) requirement.alsoAccepts.Add(data.Inputs[i]);
            return requirement;
        }

        internal InputRequirementData ToData()
        {
            List<ControllerInput> inputs = new List<ControllerInput>(1 + alsoAccepts.Count) { input };
            inputs.AddRange(alsoAccepts);
            return new InputRequirementData(inputs, count);
        }
    }

    [Serializable]
    public sealed class InputCostDefinition
    {
        [SerializeField] private List<InputRequirement> requirements = new List<InputRequirement>();

        public IReadOnlyList<InputRequirement> Requirements => requirements;

        /// <summary>A cost made at runtime from cost data (player cards, doc 27 L2-1).</summary>
        internal static InputCostDefinition FromData(InputCostData data)
        {
            InputCostDefinition cost = new InputCostDefinition();
            if (data == null) return cost;
            for (int i = 0; i < data.Requirements.Count; i++) cost.requirements.Add(InputRequirement.FromData(data.Requirements[i]));
            return cost;
        }

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
