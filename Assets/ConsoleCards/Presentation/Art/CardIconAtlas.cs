using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Presentation.Art
{
    /// <summary>
    /// The card icon picture (ICON-1, doc 31 PL-ICONS): one texture of 3 × 3 cells in Resources/CardIcons. Cell 0 is
    /// the faint ring of an empty icon place (card editor only); cells 1-8 are the Console symbols in
    /// <see cref="ConsoleSymbol"/> order, each a dark round badge with a white symbol and a white rim.
    /// </summary>
    public static class CardIconAtlas
    {
        public const string ResourcePath = "CardIcons/CardIconAtlas";
        private const int Columns = 3;
        private const int CellCount = 9;

        private static Texture2D texture;
        private static bool warned;

        /// <summary>The icon picture, loaded once; null (with one warning) when it is missing.</summary>
        public static Texture2D Texture
        {
            get
            {
                if (texture == null)
                {
                    texture = Resources.Load<Texture2D>(ResourcePath);
                    if (texture == null && !warned)
                    {
                        warned = true;
                        Debug.LogWarning($"Card icons need the picture Resources/{ResourcePath}.png; icons are not shown.");
                    }
                }

                return texture;
            }
        }

        /// <summary>The UV rectangle of a symbol's cell; None is the empty ring.</summary>
        public static Rect Cell(ConsoleSymbol symbol)
        {
            int index = (int)symbol;
            if (index < 0 || index >= CellCount) index = 0;
            int column = index % Columns;
            int row = index / Columns;
            const float size = 1f / Columns;
            return new Rect(column * size, 1f - ((row + 1) * size), size, size);
        }
    }
}
