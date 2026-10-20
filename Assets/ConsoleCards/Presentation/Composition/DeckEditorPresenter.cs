using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The deck editor panel: building and changing decks.
    /// (R1d: moved out of TabletopComposition; the table reaches it as <c>DeckEditorPanel</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class DeckEditorPresenter
    {
        private readonly TabletopComposition host;

        internal DeckEditorPresenter(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.DeckEditor.cs ----------

        internal const int DeckNameMaximumLength = 32;

        internal DeckEditorActions deckEditorActions;
        internal string deckEditorSelectedId = CardEditorPresenter.GameDeckId;
        internal bool deckEditorShowAll = true;
        internal bool deckEditorChanged;
        internal bool deckEditorCloseArmed;

        // The deck editor opens from the game selection menu (L2-4) and, so decks can be looked at without starting
        // a new table, straight from the Table menu row "Deck editor…" (2026-10-18, owner request).
        internal void RefreshDeckEditorEntry()
        {
            host.runtimeUi?.ShowDeckEditorEntry(OpenDeckEditorFromTableMenu);
        }

        // From the Table menu: Close just closes (no return to the game selection menu or a Save table as a game step).
        internal void OpenDeckEditorFromTableMenu()
        {
            host.GameSelectPanel.returnToGameSelect = false;
            host.PlayerGamesPanel.returnToGameSave = false;
            OpenDeckEditor();
        }

        internal void OpenDeckEditor()
        {
            host.EnsureContentArt();
            host.CardEditorPanel.DropUnsavedNewCard();
            host.CardEditorPanel.CollectEditorGames();
            if (host.CardEditorPanel.editorGames.Count == 0)
            {
                host.ShowMessage("There are no games with decks to edit.");
                return;
            }

            GameDefinition start = host.CardEditorPanel.editorGame != null && host.CardEditorPanel.editorGames.Contains(host.CardEditorPanel.editorGame) ? host.CardEditorPanel.editorGame : host.CardEditorPanel.editorGames[0];
            ContentArtService previous = host.CardEditorPanel.UseEditorGame(start);
            deckEditorSelectedId = CardEditorPresenter.GameDeckId;
            deckEditorShowAll = true;
            deckEditorChanged = false;
            ShowDeckEditor(string.Empty);
            previous?.Dispose();
        }

        internal void ShowDeckEditor(string status)
        {
            if (host.runtimeUi == null || host.CardEditorPanel.editorGame == null) return;
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

            string gameName = host.CardEditorPanel.editorGame.DisplayName ?? string.Empty;
            PlayerDeckRecord deck = FindEditorDeck(deckEditorSelectedId);
            if (deck == null) deckEditorSelectedId = CardEditorPresenter.GameDeckId;
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
                CanChangeGame = host.CardEditorPanel.editorGames.Count > 1,
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
            if (!host.runtimeUi.ShowDeckEditor(model, deckEditorActions))
            {
                host.ShowMessage("The deck editor is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        internal List<CardEditorRowModel> BuildDeckEditorDeckRows(string gameName, List<DeckEditorCard> cards)
        {
            List<CardEditorRowModel> rows = new List<CardEditorRowModel>(1 + host.CardEditorPanel.editorDecks.decks.Count)
            {
                DeckRow(CardEditorPresenter.GameDeckId, $"{gameName} deck", "The game's own deck", cards),
            };
            for (int d = 0; d < host.CardEditorPanel.editorDecks.decks.Count; d++)
            {
                PlayerDeckRecord deck = host.CardEditorPanel.editorDecks.decks[d];
                if (deck == null || string.IsNullOrWhiteSpace(deck.id)) continue;
                rows.Add(DeckRow(deck.id, DeckName(deck), "Your deck", cards));
            }

            return rows;
        }

        // A deck in the list: its name, size, and the picture of its first card.
        internal CardEditorRowModel DeckRow(string deckId, string name, string kind, List<DeckEditorCard> cards)
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

        internal void CycleDeckEditorGame(int step)
        {
            if (host.CardEditorPanel.editorGames.Count < 2) return;
            if (deckEditorChanged)
            {
                ShowDeckEditor("Save or undo your changes before choosing another game.");
                return;
            }

            int index = Math.Max(0, host.CardEditorPanel.editorGames.IndexOf(host.CardEditorPanel.editorGame));
            GameDefinition next = host.CardEditorPanel.editorGames[((index + step) % host.CardEditorPanel.editorGames.Count + host.CardEditorPanel.editorGames.Count) % host.CardEditorPanel.editorGames.Count];
            ContentArtService previous = host.CardEditorPanel.UseEditorGame(next);
            deckEditorSelectedId = CardEditorPresenter.GameDeckId;
            ShowDeckEditor($"Now showing {next.DisplayName}'s decks.");
            previous?.Dispose();
        }

        internal void SelectEditorDeck(string deckId)
        {
            deckEditorSelectedId = string.IsNullOrEmpty(deckId) ? CardEditorPresenter.GameDeckId : deckId;
            ShowDeckEditor(string.Empty);
        }

        internal void NewDeckInEditor()
        {
            PlayerDeckRecord deck = new PlayerDeckRecord
            {
                id = PlayerCardsFile.NewCardId(),
                name = $"My deck {host.CardEditorPanel.editorDecks.decks.Count + 1}",
            };
            host.CardEditorPanel.editorDecks.decks.Add(deck);
            deckEditorSelectedId = deck.id;
            deckEditorShowAll = true;
            deckEditorChanged = true;
            ShowDeckEditor($"Added {deck.name}. Use + to put cards in it, then Save.");
        }

        // A new deck of yours with the same cards and copies as the selected deck (the game's deck included).
        internal void CopyEditorDeck()
        {
            PlayerDeckRecord source = FindEditorDeck(deckEditorSelectedId);
            string sourceName = source != null ? DeckName(source) : $"{host.CardEditorPanel.editorGame.DisplayName} deck";
            string name = $"Copy of {sourceName}";
            if (name.Length > DeckNameMaximumLength) name = name.Substring(0, DeckNameMaximumLength).TrimEnd();
            PlayerDeckRecord copy = new PlayerDeckRecord { id = PlayerCardsFile.NewCardId(), name = name };
            List<DeckEditorCard> cards = DeckEditorCards();
            for (int i = 0; i < cards.Count; i++)
            {
                int copies = DeckEditorCopies(deckEditorSelectedId, cards[i]);
                if (copies > 0) copy.cards.Add(new PlayerDeckEntry { cardId = cards[i].Id, count = copies });
            }

            host.CardEditorPanel.editorDecks.decks.Add(copy);
            deckEditorSelectedId = copy.id;
            deckEditorChanged = true;
            ShowDeckEditor($"Made {copy.name}. Change it as you like, then Save.");
        }

        internal void RenameEditorDeck(string typedName)
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

        internal void DeleteEditorDeck()
        {
            PlayerDeckRecord deck = FindEditorDeck(deckEditorSelectedId);
            if (deck == null) return;
            host.CardEditorPanel.editorDecks.decks.Remove(deck);
            deckEditorSelectedId = CardEditorPresenter.GameDeckId;
            deckEditorChanged = true;
            ShowDeckEditor($"Deleted {DeckName(deck)}. Save makes it final; Undo changes brings it back.");
        }

        internal void ToggleDeckEditorFilter()
        {
            deckEditorShowAll = !deckEditorShowAll;
            ShowDeckEditor(string.Empty);
        }

        internal void ChangeDeckEditorCopies(string cardId, int step)
        {
            List<DeckEditorCard> cards = DeckEditorCards();
            for (int i = 0; i < cards.Count; i++)
            {
                if (!string.Equals(cards[i].Id, cardId, StringComparison.OrdinalIgnoreCase)) continue;
                int copies = Mathf.Clamp(DeckEditorCopies(deckEditorSelectedId, cards[i]) + step, 0, CardEditorPresenter.CardEditorMaximumCopies);
                SetDeckEditorCopies(deckEditorSelectedId, cards[i], copies);
                deckEditorChanged = true;
                break;
            }

            ShowDeckEditor(string.Empty);
        }

        internal void UndoDeckEditor()
        {
            host.CardEditorPanel.editorCards = host.CardEditorPanel.EditorArt.Store.LoadCards(host.CardEditorPanel.EditorArt.GameId);
            host.CardEditorPanel.editorDecks = host.CardEditorPanel.EditorArt.Store.LoadDecks(host.CardEditorPanel.EditorArt.GameId);
            deckEditorChanged = false;
            ShowDeckEditor("Changes dropped; the decks are as they were saved.");
        }

        internal void SaveDeckEditor()
        {
            bool cardsSaved = host.CardEditorPanel.EditorArt.Store.SaveCards(host.CardEditorPanel.EditorArt.GameId, host.CardEditorPanel.editorCards);
            bool decksSaved = host.CardEditorPanel.EditorArt.Store.SaveDecks(host.CardEditorPanel.EditorArt.GameId, host.CardEditorPanel.editorDecks);
            if (!cardsSaved || !decksSaved)
            {
                ShowDeckEditor("The decks could not be saved; the console says why.");
                return;
            }

            deckEditorChanged = false;
            ShowDeckEditor("Saved. The game's deck copies are used from the next table you start.");
        }

        // With unsaved changes the first Close only warns; a second Close drops them.
        internal void CloseDeckEditor()
        {
            if (deckEditorChanged && !deckEditorCloseArmed)
            {
                ShowDeckEditor("You have unsaved changes. Save them, or press Close again to drop them.");
                deckEditorCloseArmed = true;
                return;
            }

            if (deckEditorChanged)
            {
                host.CardEditorPanel.editorCards = host.CardEditorPanel.EditorArt.Store.LoadCards(host.CardEditorPanel.EditorArt.GameId);
                host.CardEditorPanel.editorDecks = host.CardEditorPanel.EditorArt.Store.LoadDecks(host.CardEditorPanel.EditorArt.GameId);
            }

            deckEditorChanged = false;
            deckEditorCloseArmed = false;
            host.runtimeUi?.CloseDeckEditor();
            host.CardEditorPanel.DisposeCardEditor();
            host.GameSelectPanel.ReturnToGameSelect();
        }

        // ---------- copies ----------

        internal List<DeckEditorCard> DeckEditorCards()
        {
            List<DeckEditorCard> cards = new List<DeckEditorCard>(host.CardEditorPanel.editorGameCards.Count + host.CardEditorPanel.editorCards.cards.Count);
            for (int i = 0; i < host.CardEditorPanel.editorGameCards.Count; i++) cards.Add(new DeckEditorCard(host.CardEditorPanel.editorGameCards[i], null));
            for (int i = 0; i < host.CardEditorPanel.editorCards.cards.Count; i++)
            {
                PlayerCardRecord record = host.CardEditorPanel.editorCards.cards[i];
                if (record != null && !string.IsNullOrWhiteSpace(record.id)) cards.Add(new DeckEditorCard(null, record));
            }

            return cards;
        }

        // The game's deck counts each card's copies in play; a deck of yours counts its own entries.
        internal int DeckEditorCopies(string deckId, DeckEditorCard card)
        {
            if (deckId == CardEditorPresenter.GameDeckId)
            {
                if (card.Record != null) return Math.Max(0, card.Record.quantity);
                CardChangeRecord change = host.CardEditorPanel.FindEditorChange(card.GameCard.StableId);
                return change != null && change.changeQuantity ? change.quantity : card.GameCard.Quantity;
            }

            PlayerDeckRecord deck = FindEditorDeck(deckId);
            return deck != null ? CardEditorPresenter.SavedDeckCopies(deck, card.Id) : 0;
        }

        internal void SetDeckEditorCopies(string deckId, DeckEditorCard card, int copies)
        {
            if (deckId == CardEditorPresenter.GameDeckId)
            {
                if (card.Record != null)
                {
                    card.Record.quantity = copies;
                    return;
                }

                // A game card's copies are a change to that card; back at the game's own number, the change goes.
                CardChangeRecord change = host.CardEditorPanel.FindEditorChange(card.GameCard.StableId);
                if (copies == card.GameCard.Quantity)
                {
                    if (change == null) return;
                    change.changeQuantity = false;
                    change.quantity = copies;
                    if (!change.HasAnyChange) host.CardEditorPanel.editorCards.changes.Remove(change);
                    return;
                }

                if (change == null)
                {
                    change = new CardChangeRecord { cardId = card.GameCard.StableId };
                    host.CardEditorPanel.editorCards.changes.Add(change);
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

        internal string CardName(DeckEditorCard card)
        {
            if (card.Record != null) return string.IsNullOrWhiteSpace(card.Record.name) ? "(no name)" : card.Record.name;
            CardChangeRecord change = host.CardEditorPanel.FindEditorChange(card.GameCard.StableId);
            return change != null && change.changeName ? change.name : card.GameCard.DisplayName;
        }

        internal string CardDetail(DeckEditorCard card)
        {
            if (card.GameCard != null)
            {
                return $"{(host.CardEditorPanel.FindEditorChange(card.GameCard.StableId) != null ? "Changed game card" : "Game card")} · {card.GameCard.Category}";
            }

            CardDefinition model = host.CardEditorPanel.FindEditorGameCard(card.Record.behavesLike);
            string code = string.IsNullOrWhiteSpace(card.Record.code) ? string.Empty : $"{card.Record.code} · ";
            return $"{code}Your card · works like {(model != null ? model.DisplayName : "?")}";
        }

        internal Texture2D CardThumbnail(DeckEditorCard card)
        {
            if (card.Record != null)
            {
                return host.CardEditorPanel.DraftPicture(host.CardEditorPanel.FrontKeyOf(card.Record), host.CardEditorPanel.FindEditorGameCard(card.Record.behavesLike), true);
            }

            CardChangeRecord change = host.CardEditorPanel.FindEditorChange(card.GameCard.StableId);
            string front = change != null && change.changeFrontArt ? change.frontArtKey : card.GameCard.FrontArtworkReference;
            return host.CardEditorPanel.DraftPicture(front, card.GameCard, true);
        }

        internal PlayerDeckRecord FindEditorDeck(string deckId)
        {
            if (string.IsNullOrEmpty(deckId) || deckId == CardEditorPresenter.GameDeckId || host.CardEditorPanel.editorDecks == null) return null;
            for (int d = 0; d < host.CardEditorPanel.editorDecks.decks.Count; d++)
            {
                PlayerDeckRecord deck = host.CardEditorPanel.editorDecks.decks[d];
                if (deck != null && string.Equals(deck.id, deckId, StringComparison.OrdinalIgnoreCase)) return deck;
            }

            return null;
        }

        internal static string DeckName(PlayerDeckRecord deck) => string.IsNullOrWhiteSpace(deck.name) ? "My deck" : deck.name;
    }
}
