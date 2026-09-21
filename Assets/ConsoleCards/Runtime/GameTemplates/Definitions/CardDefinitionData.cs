using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConsoleCards.GameTemplates.Definitions
{
    [Serializable]
    public sealed class CardDefinitionData
    {
        private readonly ReadOnlyCollection<string> tags;

        public CardDefinitionData(
            string stableId,
            string displayName,
            string category,
            string description,
            string frontArtworkReference,
            string backArtworkReference,
            int quantity,
            InputCostData inputCost,
            CardOrientation orientation,
            string preferredConsolePlacement,
            IEnumerable<string> tags)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Card ID is required.", nameof(stableId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Card name is required.", nameof(displayName));
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));

            StableId = stableId;
            DisplayName = displayName;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            FrontArtworkReference = frontArtworkReference ?? string.Empty;
            BackArtworkReference = backArtworkReference ?? string.Empty;
            Quantity = quantity;
            InputCost = inputCost ?? throw new ArgumentNullException(nameof(inputCost));
            Orientation = orientation;
            PreferredConsolePlacement = preferredConsolePlacement ?? string.Empty;
            this.tags = new ReadOnlyCollection<string>(new List<string>(tags ?? throw new ArgumentNullException(nameof(tags))));
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string Description { get; }
        public string FrontArtworkReference { get; }
        public string BackArtworkReference { get; }
        public int Quantity { get; }
        public InputCostData InputCost { get; }
        public CardOrientation Orientation { get; }
        public string PreferredConsolePlacement { get; }
        public IReadOnlyList<string> Tags => tags;
    }
}
