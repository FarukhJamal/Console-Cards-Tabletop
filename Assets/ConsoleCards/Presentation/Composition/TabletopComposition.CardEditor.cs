using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The card editor (doc 27 List 2, L2-2): New table… > Card editor… (the game selection menu, L2-4). Pick a game (every game in the library);
    /// its deck lists every card with its copies and the total, then the player's own cards. One card is edited at a
    /// time with a live preview, and the decks it is in (the game's deck and the player's decks) with copies in each.
    /// A game card's edits are saved as a change (Reset brings the game's card back); a new card works like a card of
    /// the same game. Everything is saved in the player's profile (doc 28) when the player presses Save, and used
    /// from the next table that starts.
    /// </summary>
    public sealed partial class TabletopComposition
    {

        // The card being edited. Nothing here is saved until Save.
        internal sealed class CardEditorDraft
        {
            public bool IsNew;
            public bool IsSaved;
            public string Id = string.Empty;
            public string Code = string.Empty;
            public string Name = string.Empty;
            public string Description = string.Empty;
            public int Copies = 1;
            public string Cost = string.Empty;
            public string WorksLikeId = string.Empty;
            // The art keys in use. Empty front on a new card: no picture (the blank base card).
            public string FrontKey = string.Empty;
            public string BackKey = string.Empty;
            // The icons on the card front (ICON-1), one per place (CardIconPlace order).
            public readonly ConsoleSymbol[] Icons = new ConsoleSymbol[CardDefinition.EdgeIconCount];
            // The frame variant ID (FRAME-1); empty: the game's default frame.
            public string Frame = string.Empty;
            // Copies in each of the player's decks, by deck ID (0 or missing: not in that deck).
            public readonly Dictionary<string, int> DeckCopies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
