using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [CreateAssetMenu(fileName = "CardDefinition", menuName = "Console Cards/Definitions/Card")]
    public sealed class CardDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private string category;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Texture2D frontArtwork;
        [SerializeField] private string frontArtworkReference;
        [SerializeField] private Texture2D backArtwork;
        [SerializeField] private string backArtworkReference;
        [SerializeField, Min(0)] private int quantity = 1;
        [SerializeField] private InputCostDefinition inputCost = new InputCostDefinition();
        [SerializeField] private CardOrientation orientation;
        [SerializeField] private string preferredConsolePlacement;
        [SerializeField] private List<string> tags = new List<string>();

        public string StableId => stableId;
        public string DisplayName => displayName;
        public string Category => category;
        public string Description => description;
        public Texture2D FrontArtwork => frontArtwork;
        public Texture2D BackArtwork => backArtwork;
        public int Quantity => quantity;
        public InputCostDefinition InputCost => inputCost;
        public CardOrientation Orientation => orientation;
        public string PreferredConsolePlacement => preferredConsolePlacement;
        public IReadOnlyList<string> Tags => tags;

        public bool TryGetObjectDefinitionId(out ObjectDefinitionId id)
        {
            if (Guid.TryParse(stableId, out Guid value))
            {
                id = new ObjectDefinitionId(value);
                return true;
            }

            id = ObjectDefinitionId.Empty;
            return false;
        }

        public CardDefinitionData ToData()
        {
            return new CardDefinitionData(
                stableId,
                displayName,
                category,
                description,
                frontArtworkReference,
                backArtworkReference,
                quantity,
                inputCost.ToData(),
                orientation,
                preferredConsolePlacement,
                tags);
        }
    }
}
