using System;
using System.Collections.Generic;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [Serializable]
    public sealed class ConsoleSlotDefinition
    {
        [SerializeField] private string role;
        [SerializeField, Min(1)] private int physicalSlotCount = 1;
        [SerializeField] private ConsoleStackingPolicy stackingPolicy;
        [SerializeField, Min(0)] private int maximumCardsPerSlot;
        [SerializeField] private List<string> allowedCategories = new List<string>();
        [SerializeField] private CardOrientation orientation;
        [SerializeField, TextArea] private string layoutMetadata;

        internal ConsoleSlotDefinitionData ToData()
        {
            return new ConsoleSlotDefinitionData(
                role,
                physicalSlotCount,
                stackingPolicy,
                maximumCardsPerSlot,
                allowedCategories,
                orientation,
                layoutMetadata);
        }
    }

    [CreateAssetMenu(fileName = "ConsoleConfiguration", menuName = "Console Cards/Definitions/Console Configuration")]
    public sealed class ConsoleConfiguration : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private List<ConsoleSlotDefinition> slots = new List<ConsoleSlotDefinition>();
        [SerializeField, TextArea] private string hudAreaMetadata;
        [SerializeField, TextArea] private string statAreaMetadata;
        [SerializeField, TextArea] private string componentAreaMetadata;

        public ConsoleConfigurationData ToData()
        {
            List<ConsoleSlotDefinitionData> slotData = new List<ConsoleSlotDefinitionData>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) throw new InvalidOperationException("Console Slots cannot contain null entries.");
                slotData.Add(slots[i].ToData());
            }

            return new ConsoleConfigurationData(
                stableId,
                slotData,
                hudAreaMetadata,
                statAreaMetadata,
                componentAreaMetadata);
        }
    }
}
