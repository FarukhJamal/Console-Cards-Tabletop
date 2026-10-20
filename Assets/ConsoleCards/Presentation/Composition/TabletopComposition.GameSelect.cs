using System;
using System.Collections.Generic;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The game selection menu (doc 27 List 2, L2-4): Table menu > New table… opens it. It lists the Empty Table and
    /// every game the platform has (this table's game and each game box in the library; DLC games arrive as more
    /// boxes). For the chosen game it shows its picture, players, mode, the rules in play and its deck, who is
    /// playing, and the places to make the game yours: card art, the card editor and the deck editor (they open from
    /// here and come back here when closed). Start replaces the table, asking first when the table has content.
    /// The deck line chooses the deck to play with (L2-5): a game plays with its own deck or one of the player's
    /// decks for it (remembered per game in the profile); an Empty Table can start with any deck of the player's,
    /// placed on the table right after Start.
    /// </summary>
    public sealed partial class TabletopComposition
    {

        // A deck the menu offers: the game's own deck (empty ID), or one of the player's decks.
        internal sealed class DeckChoice
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public int Cards;
            public PlayerDeckRecord Deck;
            // The game whose cards the deck uses (doc 29, L2-6b).
            public GameDefinition Game;
        }

        internal sealed class GameSelectEntry
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public GameDefinition Game;
            // Doc 29: a game the player made (Game is then null).
            public PlayerGameFile PlayerGame;
            public readonly List<GameTemplateId> Templates = new List<GameTemplateId>();
            public readonly List<string> ModeNames = new List<string>();
        }

        // The table's own picture service for its game; a short-lived one for any other game.
        internal readonly struct ContentArtServiceScope : IDisposable
        {
            public ContentArtServiceScope(ContentArtService art, bool owned)
            {
                Art = art;
                Owned = owned;
            }

            public ContentArtService Art { get; }
            private bool Owned { get; }

            public void Dispose()
            {
                if (Owned) Art?.Dispose();
            }
        }
    }
}
