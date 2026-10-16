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
        private const int DeckNameMaximumLength = 32;

        private DeckEditorActions deckEditorActions;
        private string deckEditorSelectedId = GameDeckId;
        private bool deckEditorShowAll = true;
        private bool deckEditorChanged;
        private bool deckEditorCloseArmed;

        // A card of the chosen game: one of the game's own, or one of the player's.
        private readonly struct DeckEditorCard
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

        // The deck editor opens from the game selection menu (L2-4); the Table menu row stays hidden.
        private void RefreshDeckEditorEntry()
        {
            runtimeUi?.ShowDeckEditorEntry(null);
        }

        private void OpenDeckEditor()
        {
            EnsureContentArt();
            DropUnsavedNewCard();
            CollectEditorGames();
            if (editorGames.Count == 0)
            {
                ShowMessage("There are no games with decks to edit.");
                return;
            }

            GameDefinition start = editorGame != null && editorGames.Contains(editorGame) ? editorGame : editorGames[0];
            ContentArtService previous = UseEditorGame(start);
            deckEditorSelectedId = GameDeckId;
            deckEditorShowAll = true;
            deckEditorChanged = false;
            ShowDeckEditor(string.Empty);
            previous?.Dispose();
        }

        private void ShowDeckEditor(string status)
        {
            if (runtimeUi == null || editorGame == null) return;
            deckEditorCloseArmed = false;
            if (deckEditorActions == null)
            {
                deckEditorActions = new DeckEditorActions
                {
                    CycleGame = CycleDeckEditorGame,
                    SelectDeck = SelectEditorDeck,
                    NewDeck = NewDeckInEditor,
                    CopyDeck = CopyEditorDeck,
                    RenameDeck = RenameEditorDeck,
                    DeleteDeck = DeleteEditorDeck,
                    ToggleFilter = ToggleDeckEditorFilter,
                    ChangeCopies = ChangeDeckEditorCopies,
                    Undo = UndoDeckEditor,
                    Save = SaveDeckEditor,
                    Close = CloseDeckEditor,
                };
            }

            string gameName = editorGame.DisplayName ?? string.Empty;
            PlayerDeckRecord deck = FindEditorDeck(deckEditorSelectedId);
            if (deck == null) deckEditorSelectedId = GameDeckId;
            bool isGameDeck = deck == null;
            string deckName = isGameDeck ? $"{gameName} deck" : DeckName(deck);

            List<DeckEditorCard> cards = DeckEditorCards();
            List<DeckEditorCardRowModel> rows = new List<DeckEditorCardRowModel>(cards.Count);
            int total = 0;
            int different = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                int copies = DeckEditorCopies(deckEditorSelectedId, cards[i]);
                total += copies;
                if (copies > 0) different++;
                if (!deckEditorShowAll && copies == 0) continue;
                rows.Add(new DeckEditorCardRowModel(cards[i].Id, CardName(cards[i]), CardDetail(cards[i]), CardThumbnail(cards[i]), copies));
            }

            string text = status;
            if (string.IsNullOrEmpty(text))
            {
                text = deckEditorChanged
                    ? "Unsaved changes: Save keeps them, Undo changes drops them."
                    : isGameDeck
                        ? "The game's own deck: these copies are used in play from the next table you start."
                        : "Your deck. Games will use your decks in a later step (L2-5).";
            }

            DeckEditorModel model = new DeckEditorModel
            {
                GameName = gameName,
                CanChangeGame = editorGames.Count > 1,
                Decks = BuildDeckEditorDeckRows(gameName, cards),
                DeckName = deckName,
                CanRename = !isGameDeck,
                CanDelete = !isGameDeck,
                Summary = $"{deckName.ToUpperInvariant()} · {total} CARD{(total == 1 ? string.Empty : "S")} · {different} DIFFERENT",
                FilterText = deckEditorShowAll ? "Show: all cards" : "Show: this deck only",
                Cards = rows,
                HasChanges = deckEditorChanged,
                Status = text,
            };
            if (!runtimeUi.ShowDeckEditor(model, deckEditorActions))
            {
                ShowMessage("The deck editor is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        private List<CardEditorRowModel> BuildDeckEditorDeckRows(string gameName, List<DeckEditorCard> cards)
        {
            List<CardEditorRowModel> rows = new List<CardEditorRowModel>(1 + editorDecks.decks.Count)
            {
                DeckRow(GameDeckId, $"{gameName} deck", "The game's own deck", cards),
            };
            for (int d = 0; d < editorDecks.decks.Count; d++)
            {
                PlayerDeckRecord deck = editorDecks.decks[d];
                if (deck == null || string.IsNullOrWhiteSpace(deck.id)) continue;
                rows.Add(DeckRow(deck.id, DeckName(deck), "Your deck", cards));
            }

            return rows;
        }

        // A deck in the list: its name, size, and the picture of its first card.
        private CardEditorRowModel DeckRow(string deckId, string name, string kind, List<DeckEditorCard> cards)
        {
            int total = 0;
            Texture2D picture = null;
            for (int i = 0; i < cards.Count; i++)
            {
                int copies = DeckEditorCopies(deckId, cards[i]);
                total += copies;
                if (copies > 0 && picture == null) picture = CardThumbnail(cards[i]);
            }

            return new CardEditorRowModel(deckId, name, $"{kind} · {total} card{(total == 1 ? string.Empty : "s")}", picture,
                string.Equals(deckId, deckEditorSelectedId, StringComparison.OrdinalIgnoreCase));
        }

        // ---------- actions ----------

        private void CycleDeckEditorGame(int step)
        {
            if (editorGames.Count < 2) return;
            if (deckEditorChanged)
            {
                ShowDeckEditor("Save or undo your changes before choosing another game.");
                return;
            }

            int index = Math.Max(0, editorGames.IndexOf(editorGame));
            GameDefinition next = editorGames[((index + step) % editorGames.Count + editorGames.Count) % editorGames.Count];
            ContentArtService previous = UseEditorGame(next);
            deckEditorSelectedId = GameDeckId;
            ShowDeckEditor($"Now showing {next.DisplayName}'s decks.");
            previous?.Dispose();
        }

        private void SelectEditorDeck(string deckId)
        {
            deckEditorSelectedId = string.IsNullOrEmpty(deckId) ? GameDeckId : deckId;
            ShowDeckEditor(string.Empty);
        }

        private void NewDeckInEditor()
        {
            PlayerDeckRecord deck = new PlayerDeckRecord
            {
                id = PlayerCardsFile.NewCardId(),
                name = $"My deck {editorDecks.decks.Count + 1}",
            };
            editorDecks.decks.Add(deck);
            deckEditorSelectedId = deck.id;
            deckEditorShowAll = true;
            deckEditorChanged = true;
            ShowDeckEditor($"Added {deck.name}. Use + to put cards in it, then Save.");
        }

        // A new deck of yours with the same cards and copies as the selected deck (the game's deck included).
        private void CopyEditorDeck()
        {
            PlayerDeckRecord source = FindEditorDeck(deckEditorSelectedId);
            string sourceName = source != null ? DeckName(source) : $"{editorGame.DisplayName} deck";
            string name = $"Copy of {sourceName}";
            if (name.Length > DeckNameMaximumLength) name = name.Substring(0, DeckNameMaximumLength).TrimEnd();
            PlayerDeckRecord copy = new PlayerDeckRecord { id = PlayerCardsFile.NewCardId(), name = name };
            List<DeckEditorCard> cards = DeckEditorCards();
            for (int i = 0; i < cards.Count; i++)
            {
                int copies = DeckEditorCopies(deckEditorSelectedId, cards[i]);
                if (copies > 0) copy.cards.Add(new PlayerDeckEntry { cardId = cards[i].Id, count = copies });
            }

            editorDecks.decks.Add(copy);
            deckEditorSelectedId = copy.id;
            deckEditorChanged = true;
            ShowDeckEditor($"Made {copy.name}. Change it as you like, then Save.");
        }

        private void RenameEditorDeck(string typedName)
        {
            PlayerDeckRecord deck = FindEditorDeck(deckEditorSelectedId);
            if (deck == null) return;
            string name = (typedName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                ShowDeckEditor("Type a name for the deck first.");
                return;
            }

            if (name.Length > DeckNameMaximumLength)
            {
                ShowDeckEditor($"A deck name has at most {DeckNameMaximumLength} letters.");
                return;
            }

            deck.name = name;
            deckEditorChanged = true;
            ShowDeckEditor($"The deck is now called {name}. Save keeps the name.");
        }

        private void DeleteEditorDeck()
        {
            PlayerDeckRecord deck = FindEditorDeck(deckEditorSelectedId);
            if (deck == null) return;
            editorDecks.decks.Remove(deck);
            deckEditorSelectedId = GameDeckId;
            deckEditorChanged = true;
            ShowDeckEditor($"Deleted {DeckName(deck)}. Save makes it final; Undo changes brings it back.");
        }

        private void ToggleDeckEditorFilter()
        {
            deckEditorShowAll = !deckEditorShowAll;
            ShowDeckEditor(string.Empty);
        }

        private void ChangeDeckEditorCopies(string cardId, int step)
        {
            List<DeckEditorCard> cards = DeckEditorCards();
            for (int i = 0; i < cards.Count; i++)
            {
                if (!string.Equals(cards[i].Id, cardId, StringComparison.OrdinalIgnoreCase)) continue;
                int copies = Mathf.Clamp(DeckEditorCopies(deckEditorSelectedId, cards[i]) + step, 0, CardEditorMaximumCopies);
                SetDeckEditorCopies(deckEditorSelectedId, cards[i], copies);
                deckEditorChanged = true;
                break;
            }

            ShowDeckEditor(string.Empty);
        }

        private void UndoDeckEditor()
        {
            editorCards = EditorArt.Store.LoadCards(EditorArt.GameId);
            editorDecks = EditorArt.Store.LoadDecks(EditorArt.GameId);
            deckEditorChanged = false;
            ShowDeckEditor("Changes dropped; the decks are as they were saved.");
        }

        private void SaveDeckEditor()
        {
            bool cardsSaved = EditorArt.Store.SaveCards(EditorArt.GameId, editorCards);
            bool decksSaved = EditorArt.Store.SaveDecks(EditorArt.GameId, editorDecks);
            if (!cardsSaved || !decksSaved)
            {
                ShowDeckEditor("The decks could not be saved; the console says why.");
                return;
            }

            deckEditorChanged = false;
            ShowDeckEditor("Saved. The game's deck copies are used from the next table you start.");
        }

        // With unsaved changes the first Close only warns; a second Close drops them.
        private void CloseDeckEditor()
        {
            if (deckEditorChanged && !deckEditorCloseArmed)
            {
                ShowDeckEditor("You have unsaved changes. Save them, or press Close again to drop them.");
                deckEditorCloseArmed = true;
                return;
            }

            if (deckEditorChanged)
            {
                editorCards = EditorArt.Store.LoadCards(EditorArt.GameId);
                editorDecks = EditorArt.Store.LoadDecks(EditorArt.GameId);
            }

            deckEditorChanged = false;
            deckEditorCloseArmed = false;
            runtimeUi?.CloseDeckEditor();
            DisposeCardEditor();
            ReturnToGameSelect();
        }

        // ---------- copies ----------

        private List<DeckEditorCard> DeckEditorCards()
        {
            List<DeckEditorCard> cards = new List<DeckEditorCard>(editorGameCards.Count + editorCards.cards.Count);
            for (int i = 0; i < editorGameCards.Count; i++) cards.Add(new DeckEditorCard(editorGameCards[i], null));
            for (int i = 0; i < editorCards.cards.Count; i++)
            {
                PlayerCardRecord record = editorCards.cards[i];
                if (record != null && !string.IsNullOrWhiteSpace(record.id)) cards.Add(new DeckEditorCard(null, record));
            }

            return cards;
        }

        // The game's deck counts each card's copies in play; a deck of yours counts its own entries.
        private int DeckEditorCopies(string deckId, DeckEditorCard card)
        {
            if (deckId == GameDeckId)
            {
                if (card.Record != null) return Math.Max(0, card.Record.quantity);
                CardChangeRecord change = FindEditorChange(card.GameCard.StableId);
                return change != null && change.changeQuantity ? change.quantity : card.GameCard.Quantity;
            }

            PlayerDeckRecord deck = FindEditorDeck(deckId);
            return deck != null ? SavedDeckCopies(deck, card.Id) : 0;
        }

        private void SetDeckEditorCopies(string deckId, DeckEditorCard card, int copies)
        {
            if (deckId == GameDeckId)
            {
                if (card.Record != null)
                {
                    card.Record.quantity = copies;
                    return;
                }

                // A game card's copies are a change to that card; back at the game's own number, the change goes.
                CardChangeRecord change = FindEditorChange(card.GameCard.StableId);
                if (copies == card.GameCard.Quantity)
                {
                    if (change == null) return;
                    change.changeQuantity = false;
                    change.quantity = copies;
                    if (!change.HasAnyChange) editorCards.changes.Remove(change);
                    return;
                }

                if (change == null)
                {
                    change = new CardChangeRecord { cardId = card.GameCard.StableId };
                    editorCards.changes.Add(change);
                }

                change.changeQuantity = true;
                change.quantity = copies;
                return;
            }

            PlayerDeckRecord deck = FindEditorDeck(deckId);
            if (deck == null) return;
            deck.cards.RemoveAll(entry => entry != null && string.Equals(entry.cardId, card.Id, StringComparison.OrdinalIgnoreCase));
            if (copies > 0) deck.cards.Add(new PlayerDeckEntry { cardId = card.Id, count = copies });
        }

        // ---------- how a card shows ----------

        private string CardName(DeckEditorCard card)
        {
            if (card.Record != null) return string.IsNullOrWhiteSpace(card.Record.name) ? "(no name)" : card.Record.name;
            CardChangeRecord change = FindEditorChange(card.GameCard.StableId);
            return change != null && change.changeName ? change.name : card.GameCard.DisplayName;
        }

        private string CardDetail(DeckEditorCard card)
        {
            if (card.GameCard != null)
            {
                return $"{(FindEditorChange(card.GameCard.StableId) != null ? "Changed game card" : "Game card")} · {card.GameCard.Category}";
            }

            CardDefinition model = FindEditorGameCard(card.Record.behavesLike);
            string code = string.IsNullOrWhiteSpace(card.Record.code) ? string.Empty : $"{card.Record.code} · ";
            return $"{code}Your card · works like {(model != null ? model.DisplayName : "?")}";
        }

        private Texture2D CardThumbnail(DeckEditorCard card)
        {
            if (card.Record != null)
            {
                return DraftPicture(FrontKeyOf(card.Record), FindEditorGameCard(card.Record.behavesLike), true);
            }

            CardChangeRecord change = FindEditorChange(card.GameCard.StableId);
            string front = change != null && change.changeFrontArt ? change.frontArtKey : card.GameCard.FrontArtworkReference;
            return DraftPicture(front, card.GameCard, true);
        }

        private PlayerDeckRecord FindEditorDeck(string deckId)
        {
            if (string.IsNullOrEmpty(deckId) || deckId == GameDeckId || editorDecks == null) return null;
            for (int d = 0; d < editorDecks.decks.Count; d++)
            {
                PlayerDeckRecord deck = editorDecks.decks[d];
                if (deck != null && string.Equals(deck.id, deckId, StringComparison.OrdinalIgnoreCase)) return deck;
            }

            return null;
        }

        private static string DeckName(PlayerDeckRecord deck) => string.IsNullOrWhiteSpace(deck.name) ? "My deck" : deck.name;
    }
}
