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
        [Tooltip("Console mat symbol (doc 22 §14). A card with a symbol fits only a Console slot with the same symbol "
            + "and is pulled into it like a magnet. None fits any slot.")]
        [SerializeField] private ConsoleSymbol consoleSymbol;

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
        public ConsoleSymbol ConsoleSymbol => consoleSymbol;
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

        /// <summary>
        /// A card made at runtime from a model card (doc 27 List 2, L2-1): the blank base card with new words, count,
        /// cost and pictures, keeping everything that decides how the card works (category, tags, effect, Console
        /// behaviour, size, symbol) from <paramref name="model"/>. Pictures are found by art key; a picture texture is
        /// kept only while its key is the model's own. Not saved as an asset; destroy it when the game unloads.
        /// </summary>
        public static CardDefinition CreateRuntime(
            CardDefinition model,
            string stableId,
            string displayName,
            string description,
            int quantity,
            InputCostDefinition cost,
            string frontArtworkReference,
            string backArtworkReference)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("A card needs an ID.", nameof(stableId));
            CardDefinition card = CreateInstance<CardDefinition>();
            card.hideFlags = HideFlags.DontSave;
            card.stableId = stableId;
            card.name = string.IsNullOrWhiteSpace(displayName) ? model.name : displayName;
            card.displayName = string.IsNullOrWhiteSpace(displayName) ? model.displayName : displayName;
            card.category = model.category;
            card.description = description ?? string.Empty;
            card.frontArtworkReference = frontArtworkReference ?? string.Empty;
            card.backArtworkReference = backArtworkReference ?? string.Empty;
            card.frontArtwork = string.Equals(card.frontArtworkReference, model.frontArtworkReference, StringComparison.OrdinalIgnoreCase)
                ? model.frontArtwork
                : null;
            card.backArtwork = string.Equals(card.backArtworkReference, model.backArtworkReference, StringComparison.OrdinalIgnoreCase)
                ? model.backArtwork
                : null;
            card.quantity = Math.Max(0, quantity);
            card.inputCost = cost ?? model.inputCost;
            card.orientation = model.orientation;
            card.defaultFace = model.defaultFace;
            card.preferredConsolePlacement = model.preferredConsolePlacement;
            card.consoleBehavior = model.consoleBehavior;
            card.tags = new List<string>(model.tags);
            if (cost != null && cost.Requirements.Count > 0) card.tags.Remove("cost-pending");
            card.effectMetadata = model.effectMetadata;
            card.objectiveMetadata = model.objectiveMetadata;
            card.representsControllerInput = model.representsControllerInput;
            card.representedControllerInput = model.representedControllerInput;
            card.sizeClass = model.sizeClass;
            card.consoleSymbol = model.consoleSymbol;
            return card;
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
