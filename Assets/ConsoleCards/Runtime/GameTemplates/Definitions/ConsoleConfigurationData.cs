using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConsoleCards.GameTemplates.Definitions
{
    [Serializable]
    public sealed class ConsoleSlotDefinitionData
    {
        private readonly ReadOnlyCollection<string> allowedCategories;

        public ConsoleSlotDefinitionData(
            string role,
            int physicalSlotCount,
            ConsoleStackingPolicy stackingPolicy,
            int maximumCardsPerSlot,
            IEnumerable<string> allowedCategories,
            CardOrientation orientation,
            string layoutMetadata)
        {
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Console Slot role is required.", nameof(role));
            if (physicalSlotCount < 1) throw new ArgumentOutOfRangeException(nameof(physicalSlotCount));
            if (maximumCardsPerSlot < 0) throw new ArgumentOutOfRangeException(nameof(maximumCardsPerSlot));

            Role = role;
            PhysicalSlotCount = physicalSlotCount;
            StackingPolicy = stackingPolicy;
            MaximumCardsPerSlot = maximumCardsPerSlot;
            this.allowedCategories = new ReadOnlyCollection<string>(new List<string>(allowedCategories ?? throw new ArgumentNullException(nameof(allowedCategories))));
            Orientation = orientation;
            LayoutMetadata = layoutMetadata ?? string.Empty;
        }

        public string Role { get; }
        public int PhysicalSlotCount { get; }
        public ConsoleStackingPolicy StackingPolicy { get; }
        public int MaximumCardsPerSlot { get; }
        public IReadOnlyList<string> AllowedCategories => allowedCategories;
        public CardOrientation Orientation { get; }
        public string LayoutMetadata { get; }
    }

    [Serializable]
    public sealed class ConsoleConfigurationData
    {
        private readonly ReadOnlyCollection<ConsoleSlotDefinitionData> slots;

        public ConsoleConfigurationData(
            string stableId,
            IEnumerable<ConsoleSlotDefinitionData> slots,
            string hudAreaMetadata,
            string statAreaMetadata,
            string componentAreaMetadata)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Console configuration ID is required.", nameof(stableId));
            StableId = stableId;
            this.slots = new ReadOnlyCollection<ConsoleSlotDefinitionData>(new List<ConsoleSlotDefinitionData>(slots ?? throw new ArgumentNullException(nameof(slots))));
            HudAreaMetadata = hudAreaMetadata ?? string.Empty;
            StatAreaMetadata = statAreaMetadata ?? string.Empty;
            ComponentAreaMetadata = componentAreaMetadata ?? string.Empty;
        }

        public string StableId { get; }
        public IReadOnlyList<ConsoleSlotDefinitionData> Slots => slots;
        public string HudAreaMetadata { get; }
        public string StatAreaMetadata { get; }
        public string ComponentAreaMetadata { get; }
    }
}
