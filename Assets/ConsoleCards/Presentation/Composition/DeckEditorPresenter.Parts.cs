using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.UI;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 63 (owner, 2026-11-21): "a deck is one single deck by default; splitting it into parts is an option the
    /// player turns on per deck". SPLIT INTO PARTS gives the selected deck Part 1 (its cards) and an empty Part 2;
    /// ADD PART adds another; a part is chosen, renamed (RENAME) and deleted (DELETE) like a deck, and its cards are
    /// its own. The deck's own cards stay the whole deck (every part added up). ONE DECK joins the parts again.
    /// Trap Floor's floor decks (stages) are not split.
    /// </summary>
    internal sealed partial class DeckEditorPresenter
    {
        internal const int MaximumDeckParts = 12;

        // The part with this ID and the deck it belongs to; null when the ID is not a part's.
        internal PlayerDeckPart FindEditorPart(string partId, out PlayerDeckRecord owner)
        {
            owner = null;
            PlayerDecksFile decks = host.CardEditorPanel.editorDecks;
            if (string.IsNullOrEmpty(partId) || partId == CardEditorPresenter.GameDeckId || decks == null) return null;
            for (int d = 0; d < decks.decks.Count; d++)
            {
                PlayerDeckRecord deck = decks.decks[d];
                PlayerDeckPart part = deck != null && deck.HasParts ? deck.FindPart(partId) : null;
                if (part == null) continue;
                owner = deck;
                return part;
            }

            return null;
        }

        internal static string PartName(PlayerDeckPart part) => string.IsNullOrWhiteSpace(part.name) ? "Part" : part.name;

        // A deck's parts as its children in the list.
        internal void AddPartRows(List<CardEditorRowModel> rows, PlayerDeckRecord deck, int depth)
        {
            if (!deck.HasParts) return;
            List<DeckEditorCard> cards = AllDeckEditorCards();
            for (int p = 0; p < deck.parts.Count; p++)
            {
                PlayerDeckPart part = deck.parts[p];
                if (part == null || string.IsNullOrWhiteSpace(part.id)) continue;
                rows.Add(DeckRow(part.id, PartName(part), "Part", cards, depth));
            }
        }

        // SPLIT INTO PARTS / ONE DECK on the selected deck (or the deck of the selected part).
        internal void ToggleEditorDeckParts()
        {
            PlayerDeckRecord deck = FindEditorDeck(deckEditorSelectedId);
            if (deck == null || deck.stage > 0)
            {
                ShowDeckEditor("Choose one of your decks first.");
                return;
            }

            if (deck.HasParts)
            {
                deck.SumParts();
                deck.parts.Clear();
                deckEditorSelectedId = deck.id;
                deckEditorChanged = true;
                ShowDeckEditor($"{DeckName(deck)} is one deck again, with every part's cards. UNDO brings the parts back.");
                return;
            }

            PlayerDeckPart first = new PlayerDeckPart { id = PlayerCardsFile.NewCardId(), name = "Part 1" };
            for (int i = 0; i < deck.cards.Count; i++)
            {
                PlayerDeckEntry entry = deck.cards[i];
                if (entry != null && entry.count > 0) first.cards.Add(new PlayerDeckEntry { cardId = entry.cardId, count = entry.count });
            }

            deck.parts.Add(first);
            deck.parts.Add(new PlayerDeckPart { id = PlayerCardsFile.NewCardId(), name = "Part 2" });
            deck.SumParts();
            deckEditorSelectedId = first.id;
            deckEditorChanged = true;
            ShowDeckEditor($"{DeckName(deck)} is now in parts: Part 1 has its cards, Part 2 is empty. Rename them with RENAME.");
        }

        internal void AddEditorDeckPart()
        {
            PlayerDeckRecord deck = FindEditorDeck(deckEditorSelectedId);
            if (deck == null || !deck.HasParts)
            {
                ShowDeckEditor("Split a deck into parts first (SPLIT INTO PARTS).");
                return;
            }

            if (deck.parts.Count >= MaximumDeckParts)
            {
                ShowDeckEditor($"A deck has at most {MaximumDeckParts} parts.");
                return;
            }

            PlayerDeckPart part = new PlayerDeckPart { id = PlayerCardsFile.NewCardId(), name = $"Part {deck.parts.Count + 1}" };
            deck.parts.Add(part);
            deckEditorSelectedId = part.id;
            deckEditorChanged = true;
            ShowDeckEditor($"Added {part.name} to {DeckName(deck)}. Use + to put cards in it.");
        }

        // DELETE on a part: the part and its cards go (a deck in parts keeps at least one).
        internal bool TryDeleteEditorPart()
        {
            PlayerDeckPart part = FindEditorPart(deckEditorSelectedId, out PlayerDeckRecord deck);
            if (part == null) return false;
            if (deck.parts.Count <= 1)
            {
                ShowDeckEditor("A deck in parts keeps one part at least; ONE DECK makes it a single deck again.");
                return true;
            }

            deck.parts.Remove(part);
            deck.SumParts();
            deckEditorSelectedId = deck.id;
            deckEditorChanged = true;
            ShowDeckEditor($"Deleted {PartName(part)} and its cards from {DeckName(deck)}. Undo changes brings it back.");
            return true;
        }
    }
}
