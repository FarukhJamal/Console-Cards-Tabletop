using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// FLOORDECK-1 (owner, 2026-11-02: "floor decks … give them an option of adding more"): in Decks, a game with
    /// floors (Trap Floor) lists FLOOR 1, FLOOR 2 … under its own deck, each the cards that floor is dealt from (with
    /// copies) and its own back (CHANGE…), and + ADD FLOOR adds the next floor. A floor nobody changed is dealt from
    /// all the floor cards, as before; it becomes a deck of its own once opened and changed. The game keeps its tile
    /// mix (how many Traps, Keys, Exits…); a floor deck chooses which cards fill it.
    /// </summary>
    internal sealed partial class DeckEditorPresenter
    {
        internal const string FloorRowPrefix = "floor:";
        internal const string AddFloorRowId = "floor:add";
        private const int MinimumFloorSize = 3; // as the game: the floor shrinks one row and column a floor, down to 3×3

        // True when the game being edited has floor groups (content sets with the floor role).
        internal bool EditorGameHasFloors()
        {
            GameDefinition game = host.CardEditorPanel.editorGame;
            if (game == null) return false;
            for (int s = 0; s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                if (set != null && string.Equals(set.Role, "floor", StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        // The floors the board itself has (6×6 down to 3×3: 4), before any floor decks add more.
        private int BoardFloorCount()
        {
            GridDefinition grid = host.CardEditorPanel.editorGame != null ? host.CardEditorPanel.editorGame.PlayAreaDefinition : null;
            if (grid == null) return 1;
            return Math.Max(1, Math.Min(grid.Rows, grid.Columns) - MinimumFloorSize + 1);
        }

        private int FloorDeckCount()
        {
            int floors = 0;
            List<PlayerDeckRecord> decks = host.CardEditorPanel.editorDecks.decks;
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i] != null && decks[i].floor > floors) floors = decks[i].floor;
            }

            return floors;
        }

        internal PlayerDeckRecord FindFloorDeck(int floor)
        {
            List<PlayerDeckRecord> decks = host.CardEditorPanel.editorDecks.decks;
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i] != null && decks[i].floor == floor) return decks[i];
            }

            return null;
        }

        // FLOOR 1 … FLOOR n, then + ADD FLOOR, for a game with floors.
        internal void AddFloorRows(List<CardEditorRowModel> rows, List<DeckEditorCard> cards)
        {
            if (!EditorGameHasFloors()) return;
            int floors = Math.Max(BoardFloorCount(), FloorDeckCount());
            for (int floor = 1; floor <= floors; floor++)
            {
                PlayerDeckRecord deck = FindFloorDeck(floor);
                string extra = floor > BoardFloorCount() ? " · extra floor" : string.Empty;
                if (deck != null)
                {
                    rows.Add(DeckRow(deck.id, $"Floor {floor}", $"Floor deck{extra}", cards));
                }
                else
                {
                    rows.Add(new CardEditorRowModel(FloorRowPrefix + floor, $"Floor {floor}", "All floor cards (the game's)", null, false));
                }
            }

            rows.Add(new CardEditorRowModel(AddFloorRowId, "+ ADD FLOOR", $"Floor {floors + 1}: its own cards and back", null, false));
        }

        // A floor row or + ADD FLOOR was chosen; false for any other row.
        internal bool TrySelectFloorRow(string rowId)
        {
            if (string.IsNullOrEmpty(rowId) || !rowId.StartsWith(FloorRowPrefix, StringComparison.Ordinal)) return false;
            if (rowId == AddFloorRowId)
            {
                int next = Math.Max(BoardFloorCount(), FloorDeckCount()) + 1;
                PlayerDeckRecord added = MakeFloorDeck(next);
                deckEditorSelectedId = added.id;
                deckEditorChanged = true;
                ShowDeckEditor($"Added Floor {next}. It starts with every floor card; take out what this floor should not have, and give it its own back with CHANGE….");
                return true;
            }

            if (!int.TryParse(rowId.Substring(FloorRowPrefix.Length), out int floor) || floor < 1) return false;
            PlayerDeckRecord deck = FindFloorDeck(floor) ?? MakeFloorDeck(floor);
            deckEditorSelectedId = deck.id;
            ShowDeckEditor(string.Empty);
            return true;
        }

        // A floor deck holding every floor card with the game deck's copies (what the floor is dealt from by default).
        private PlayerDeckRecord MakeFloorDeck(int floor)
        {
            PlayerDeckRecord deck = new PlayerDeckRecord
            {
                id = PlayerCardsFile.NewCardId(),
                name = $"Floor {floor}",
                floor = floor,
            };
            List<DeckEditorCard> all = AllDeckEditorCards();
            for (int i = 0; i < all.Count; i++)
            {
                if (!IsFloorCard(all[i])) continue;
                int copies = DeckEditorCopies(CardEditorPresenter.GameDeckId, all[i]);
                if (copies > 0) deck.cards.Add(new PlayerDeckEntry { cardId = all[i].Id, count = copies });
            }

            host.CardEditorPanel.editorDecks.decks.Add(deck);
            return deck;
        }

        // A card of a floor group, or one of yours that works like one.
        internal bool IsFloorCard(DeckEditorCard card)
        {
            string id = card.GameCard != null ? card.GameCard.StableId : card.Record?.behavesLike;
            if (string.IsNullOrEmpty(id)) return false;
            GameDefinition game = host.CardEditorPanel.editorGame;
            for (int s = 0; game != null && s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                if (set == null || !string.Equals(set.Role, "floor", StringComparison.OrdinalIgnoreCase)) continue;
                for (int c = 0; c < set.Cards.Count; c++)
                {
                    if (set.Cards[c] != null && string.Equals(set.Cards[c].StableId, id, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }

        internal bool SelectedIsFloorDeck(out PlayerDeckRecord deck)
        {
            deck = FindEditorDeck(deckEditorSelectedId);
            return deck != null && deck.floor > 0;
        }

        internal string FloorDeckText(PlayerDeckRecord deck) =>
            deck.floor > BoardFloorCount()
                ? $"Floor {deck.floor} is an extra floor at the smallest size. It is dealt from these cards, with the game's mix of Traps, Keys and Exits. Delete deck removes this floor."
                : $"Floor {deck.floor} is dealt from these cards, with the game's mix of Traps, Keys and Exits (a kind with no cards here uses all of its cards). CHANGE… gives this floor its own back.";
    }
}
