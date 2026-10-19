using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    /// <summary>
    /// One look of a game's card frame (FRAME-1, doc 31 PL-FRAMES): its colours, and optionally its own frame picture.
    /// A card picks a variant by <see cref="Id"/> (<see cref="CardDefinition.FrameVariant"/>); empty uses the first.
    /// </summary>
    [Serializable]
    public sealed class CardFrameVariant
    {
        [Tooltip("Never changes once cards use it; saved with the cards that pick this variant.")]
        [SerializeField] private string id = "default";
        [SerializeField] private string displayName = "Default";
        [Tooltip("Off: no border and no tabs; the icons sit inside the card as small badges.")]
        [SerializeField] private bool showFrame = true;
        [Tooltip("Tint of the frame picture (white parts take this colour exactly).")]
        [SerializeField] private Color frameColor = new Color(0.25f, 0.21f, 0.2f, 1f);
        [Tooltip("Tint of the icon tabs.")]
        [SerializeField] private Color tabColor = new Color(0.25f, 0.21f, 0.2f, 1f);
        [Tooltip("Optional: this variant's own frame picture. Empty uses the style's.")]
        [SerializeField] private Texture2D frameTexture;

        public CardFrameVariant()
        {
        }

        public CardFrameVariant(string id, string displayName, bool showFrame, Color frameColor, Color tabColor)
        {
            this.id = id ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.showFrame = showFrame;
            this.frameColor = frameColor;
            this.tabColor = tabColor;
        }

        public string Id => id ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public bool ShowFrame => showFrame;
        public Color FrameColor => frameColor;
        public Color TabColor => tabColor;
        public Texture2D FrameTexture => frameTexture;
    }

    /// <summary>
    /// A game's card frame (FRAME-1, doc 31 PL-FRAMES): the border the platform draws on every card front, the tab
    /// shapes that hold the icons (PL-ICONS) and stick out past the card edge, and the frame variants a card can pick.
    /// The pictures are white or grey so each variant tints them; replace them (same layout) to restyle every card
    /// of the game. A game without its own style uses <see cref="Default"/> (Resources/CardFrames).
    /// </summary>
    [CreateAssetMenu(fileName = "CardFrameStyle", menuName = "Console Cards/Definitions/Card Frame Style")]
    public sealed class CardFrameStyle : ScriptableObject
    {
        public const string DefaultResourcePath = "CardFrames/DefaultCardFrameStyle";

        [SerializeField] private string stableId;
        [SerializeField] private string displayName;

        [Header("Pictures (white or grey; each variant tints them)")]
        [Tooltip("The border over the whole card front, with a clear middle where the card's picture shows. Top of "
            + "the picture is the top of the card.")]
        [SerializeField] private Texture2D frameTexture;
        [Tooltip("A side tab pointing up: its base along the bottom of the picture. Turned for the other sides.")]
        [SerializeField] private Texture2D sideTabTexture;
        [Tooltip("A corner tab for the top right corner: the card's corner at the picture's bottom left part. Turned "
            + "for the other corners.")]
        [SerializeField] private Texture2D cornerTabTexture;
        [Tooltip("Optional: another symbol picture (3 × 3 cells, as Resources/CardIcons/CardIconAtlas). Empty uses "
            + "the shared symbols, the same in every game.")]
        [SerializeField] private Texture2D symbolAtlas;

        [Header("Sizes, as parts of the card's shorter side")]
        [SerializeField, Range(0.05f, 1f)] private float sideTabWidth = 0.36f;
        [SerializeField, Range(0.05f, 0.6f)] private float sideTabHeight = 0.2f;
        [Tooltip("How much of a side tab sticks out past the card edge (0 none, 1 all of it).")]
        [SerializeField, Range(0f, 1f)] private float sideTabOutside = 0.55f;
        [SerializeField, Range(0.05f, 0.6f)] private float cornerTabSize = 0.26f;
        [Tooltip("How much of a corner tab sticks out past the card corner, each way.")]
        [SerializeField, Range(0f, 1f)] private float cornerTabOutside = 0.3f;
        [SerializeField, Range(0.05f, 0.4f)] private float symbolSize = 0.15f;

        [Header("Variants (the first is the game's default)")]
        [SerializeField] private List<CardFrameVariant> variants = new List<CardFrameVariant> { new CardFrameVariant() };

        private static CardFrameStyle defaultStyle;
        private static bool defaultLooked;

        public string StableId => stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Texture2D FrameTexture => frameTexture;
        public Texture2D SideTabTexture => sideTabTexture;
        public Texture2D CornerTabTexture => cornerTabTexture;
        public Texture2D SymbolAtlas => symbolAtlas;
        public float SideTabWidth => sideTabWidth;
        public float SideTabHeight => sideTabHeight;
        public float SideTabOutside => sideTabOutside;
        public float CornerTabSize => cornerTabSize;
        public float CornerTabOutside => cornerTabOutside;
        public float SymbolSize => symbolSize;
        public IReadOnlyList<CardFrameVariant> Variants => variants;

        /// <summary>The variant with this ID, else the first (the game's default); null when there are none.</summary>
        public CardFrameVariant Variant(string variantId)
        {
            if (variants == null || variants.Count == 0) return null;
            int index = IndexOf(variantId);
            return variants[index >= 0 ? index : 0];
        }

        /// <summary>The index of the variant with this ID, or -1.</summary>
        public int IndexOf(string variantId)
        {
            if (string.IsNullOrEmpty(variantId) || variants == null) return -1;
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i] != null && string.Equals(variants[i].Id, variantId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The frame picture of a variant (its own, else the style's).</summary>
        public Texture2D FrameTextureOf(CardFrameVariant variant) =>
            variant != null && variant.FrameTexture != null ? variant.FrameTexture : frameTexture;

        /// <summary>
        /// The platform's frame style (Resources/CardFrames/DefaultCardFrameStyle, made by Console Cards > Cards >
        /// Create Missing Card Frame Style); null until it exists.
        /// </summary>
        public static CardFrameStyle Default
        {
            get
            {
                if (defaultStyle == null && !defaultLooked)
                {
                    defaultLooked = true;
                    defaultStyle = Resources.Load<CardFrameStyle>(DefaultResourcePath);
                }

                return defaultStyle;
            }
        }

        /// <summary>Looks for the default style again (after the builder made it).</summary>
        public static void ForgetDefault()
        {
            defaultStyle = null;
            defaultLooked = false;
        }
    }
}
