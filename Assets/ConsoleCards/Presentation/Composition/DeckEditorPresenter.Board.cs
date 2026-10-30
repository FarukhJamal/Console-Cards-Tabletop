using System.Collections.Generic;
using System.Text.RegularExpressions;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Decks (board 11), the parts added 2026-10-30: the size range over the deck ("Size 32 / 30–40") and BACK OF
    /// CARDS with CHANGE… (one picture becomes the back of every card in the deck).
    /// </summary>
    internal sealed partial class DeckEditorPresenter
    {
        private static readonly Regex RangeNumbers = new Regex(@"\d+");
        private const int MaximumRange = 999;

        internal void FillDeckBoard(DeckEditorModel model, PlayerDeckRecord deck, List<DeckEditorCard> cards)
        {
            PlayerDecksFile decks = host.CardEditorPanel.editorDecks;
            model.RangeMinimum = deck != null ? deck.minimumCards : decks.gameDeckMinimum;
            model.RangeMaximum = deck != null ? deck.maximumCards : decks.gameDeckMaximum;
            model.CanChangeBack = PictureFilePicker.IsAvailable;
            for (int i = 0; i < cards.Count; i++)
            {
                if (DeckEditorCopies(deckEditorSelectedId, cards[i]) <= 0) continue;
                model.BackPicture = CardBack(cards[i]);
                if (model.BackPicture != null) break;
            }
        }

        internal Texture2D CardBack(DeckEditorCard card)
        {
            CardEditorPresenter studio = host.CardEditorPanel;
            if (card.Record != null)
            {
                CardDefinition model = studio.FindEditorGameCard(card.Record.behavesLike);
                string key = string.IsNullOrWhiteSpace(card.Record.backArtKey) ? model?.BackArtworkReference : card.Record.backArtKey;
                return studio.DraftPicture(key, model, false);
            }

            CardChangeRecord change = studio.FindEditorChange(card.GameCard.StableId);
            string back = change != null && change.changeBackArt ? change.backArtKey : card.GameCard.BackArtworkReference;
            return studio.DraftPicture(back, card.GameCard, false);
        }

        // "30-40", "30–40" or "30 to 40"; one number is an exact size; nothing clears the range.
        internal void SetDeckEditorRange(string typed)
        {
            MatchCollection numbers = RangeNumbers.Matches(typed ?? string.Empty);
            int minimum = 0;
            int maximum = 0;
            if (numbers.Count > 0)
            {
                minimum = Mathf.Clamp(int.Parse(numbers[0].Value), 0, MaximumRange);
                maximum = numbers.Count > 1 ? Mathf.Clamp(int.Parse(numbers[1].Value), 0, MaximumRange) : minimum;
                if (maximum < minimum) (minimum, maximum) = (maximum, minimum);
            }

            PlayerDeckRecord deck = FindEditorDeck(deckEditorSelectedId);
            PlayerDecksFile decks = host.CardEditorPanel.editorDecks;
            int oldMinimum = deck != null ? deck.minimumCards : decks.gameDeckMinimum;
            int oldMaximum = deck != null ? deck.maximumCards : decks.gameDeckMaximum;
            if (oldMinimum == minimum && oldMaximum == maximum)
            {
                ShowDeckEditor(string.Empty);
                return;
            }

            if (deck != null)
            {
                deck.minimumCards = minimum;
                deck.maximumCards = maximum;
            }
            else
            {
                decks.gameDeckMinimum = minimum;
                decks.gameDeckMaximum = maximum;
            }

            deckEditorChanged = true;
            ShowDeckEditor(maximum == 0
                ? "No size range for this deck."
                : minimum == maximum ? $"This deck should have {minimum} cards." : $"This deck should have {minimum} to {maximum} cards.");
        }

        // One picture for the back of every card in the deck (each card keeps it in any other deck too).
        internal void ChangeDeckEditorBack()
        {
            CardEditorPresenter studio = host.CardEditorPanel;
            if (!PictureFilePicker.TryPick("Choose the back of the cards", out string path))
            {
                ShowDeckEditor(string.Empty);
                return;
            }

            string key = PlayerContent.DefaultArtKey(studio.editorArtFolder, "deck-" + deckEditorSelectedId, false);
            if (!studio.StoreShapedPicture(key, path, false, out string message))
            {
                ShowDeckEditor(message);
                return;
            }

            int changed = 0;
            List<DeckEditorCard> cards = DeckEditorCards();
            for (int i = 0; i < cards.Count; i++)
            {
                if (DeckEditorCopies(deckEditorSelectedId, cards[i]) <= 0) continue;
                if (cards[i].Record != null)
                {
                    cards[i].Record.backArtKey = key;
                }
                else
                {
                    CardChangeRecord change = studio.FindEditorChange(cards[i].GameCard.StableId);
                    if (change == null)
                    {
                        change = new CardChangeRecord { cardId = cards[i].GameCard.StableId };
                        studio.editorCards.changes.Add(change);
                    }

                    change.changeBackArt = true;
                    change.backArtKey = key;
                }

                changed++;
            }

            deckEditorChanged = true;
            ShowDeckEditor(changed == 0
                ? "The deck has no cards yet: add some, then change their back."
                : $"Your picture is now the back of the {changed} card{(changed == 1 ? string.Empty : "s")} in this deck.");
        }
    }
}
