using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The deck editor (doc 27 List 2, L2-3): New table… > Deck editor… (the game selection menu, L2-4). Pick a game; its decks are the game's own deck
    /// (every card with its copies, the copies used in play) and the player's decks for that game. Decks are made,
    /// copied, renamed and deleted here, and the selected deck's cards get their copies with − and +. Everything is
    /// kept in memory until Save, which writes the profile (cards.json for the game's deck copies, decks.json for
    /// the player's decks; doc 28). It shares the card editor's game list, picture service and saved files.
    /// </summary>
    public sealed partial class TabletopComposition
    {

        // A card of the chosen game: one of the game's own, or one of the player's.
        internal readonly struct DeckEditorCard
        {
            public DeckEditorCard(CardDefinition gameCard, PlayerCardRecord record)
            {
                GameCard = gameCard;
                Record = record;
            }

            public CardDefinition GameCard { get; }
            public PlayerCardRecord Record { get; }
            public string Id => GameCard != null ? GameCard.StableId : Record.id;
        }
    }
}
