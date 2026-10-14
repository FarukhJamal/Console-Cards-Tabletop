using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    /// <summary>Physical size of a card. Large is a hero/avatar-size card (doc 24); presentation sets the scale.</summary>
    public enum CardSizeClass
    {
        Standard = 0,
        Large = 1
    }

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
        [SerializeField] private CardFace defaultFace = CardFace.FaceUp;
        [SerializeField] private string preferredConsolePlacement;
        [SerializeField] private ConsoleCardBehavior consoleBehavior;
        [SerializeField] private List<string> tags = new List<string>();
        [SerializeField, TextArea] private string effectMetadata;
        [SerializeField, TextArea] private string objectiveMetadata;
        [SerializeField] private bool representsControllerInput;
        [SerializeField] private ControllerInput representedControllerInput;
        [Tooltip("Large draws the card at hero/avatar size. Most cards are Standard.")]
        [SerializeField] private CardSizeClass sizeClass;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public string Category => category;
        public string Description => description;
        public Texture2D FrontArtwork => frontArtwork;
        public Texture2D BackArtwork => backArtwork;
        public string FrontArtworkReference => frontArtworkReference;
        public string BackArtworkReference => backArtworkReference;
        public int Quantity => quantity;
        public InputCostDefinition InputCost => inputCost;
        public CardOrientation Orientation => orientation;
        public CardFace DefaultFace => defaultFace;
        public string PreferredConsolePlacement => preferredConsolePlacement;
        public ConsoleCardBehavior ConsoleBehavior => consoleBehavior;
        public IReadOnlyList<string> Tags => tags;
        public string EffectMetadata => effectMetadata;
        public string ObjectiveMetadata => objectiveMetadata;
        public CardSizeClass SizeClass => sizeClass;
        public ControllerInput? RepresentedControllerInput =>
            representsControllerInput ? representedControllerInput : (ControllerInput?)null;

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
                tags,
                RepresentedControllerInput,
                defaultFace,
                effectMetadata,
                objectiveMetadata,
                consoleBehavior);
        }
    }
}
