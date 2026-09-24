using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Domain;

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
            IEnumerable<string> tags,
            ControllerInput? representedControllerInput = null,
            CardFace defaultFace = CardFace.FaceUp,
            string effectMetadata = "",
            string objectiveMetadata = "")
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
            if (!Enum.IsDefined(typeof(CardFace), defaultFace))
                throw new ArgumentOutOfRangeException(nameof(defaultFace));
            DefaultFace = defaultFace;
            PreferredConsolePlacement = preferredConsolePlacement ?? string.Empty;
            this.tags = new ReadOnlyCollection<string>(new List<string>(tags ?? throw new ArgumentNullException(nameof(tags))));
            if (representedControllerInput.HasValue
                && !Enum.IsDefined(typeof(ControllerInput), representedControllerInput.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(representedControllerInput));
            }

            RepresentedControllerInput = representedControllerInput;
            EffectMetadata = effectMetadata ?? string.Empty;
            ObjectiveMetadata = objectiveMetadata ?? string.Empty;
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
        public CardFace DefaultFace { get; }
        public string PreferredConsolePlacement { get; }
        public IReadOnlyList<string> Tags => tags;
        public ControllerInput? RepresentedControllerInput { get; }
        public string EffectMetadata { get; }
        public string ObjectiveMetadata { get; }
    }
}
