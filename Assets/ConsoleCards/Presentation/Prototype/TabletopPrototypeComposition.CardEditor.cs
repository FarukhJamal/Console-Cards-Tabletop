using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>
    /// The card editor (doc 27 List 2, L2-2): New table… > Card editor… (the game selection menu, L2-4). Pick a game (every game in the library);
    /// its deck lists every card with its copies and the total, then the player's own cards. One card is edited at a
    /// time with a live preview, and the decks it is in (the game's deck and the player's decks) with copies in each.
    /// A game card's edits are saved as a change (Reset brings the game's card back); a new card works like a card of
    /// the same game. Everything is saved in the player's profile (doc 28) when the player presses Save, and used
    /// from the next table that starts.
    /// </summary>
    public sealed partial class TabletopPrototypeComposition
    {
        private const int CardEditorMaximumCopies = 20;
        private const int CardEditorMaximumCodeLength = 8;
        private const string CardEditorHint =
            "Saved cards are used from the next table you start. A cost set as a house rule on the rules card wins over the cost here.";
        // The deck ID the "In decks" list uses for the game's own deck (the card's copies in the game).
        private const string GameDeckId = "game";

        // The card being edited. Nothing here is saved until Save.
        private sealed class CardEditorDraft
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
            // Copies in each of the player's decks, by deck ID (0 or missing: not in that deck).
            public readonly Dictionary<string, int> DeckCopies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private readonly List<GameDefinition> editorGames = new List<GameDefinition>();
        private readonly List<CardDefinition> editorGameCards = new List<CardDefinition>();
        private GameDefinition editorGame;
        // Pictures for a game other than this table's (the table's own art service is used for the table's game).
        private ContentArtService editorOtherArt;
        private PlayerCardsFile editorCards;
        private PlayerDecksFile editorDecks;
        private CardEditorDraft editorDraft;
        private CardEditorActions cardEditorActions;
        private string editorArtFolder = string.Empty;

        // The card editor opens from the game selection menu (L2-4); the Table menu row stays hidden.
        private void RefreshCardEditorEntry()
        {
            runtimeUi?.ShowCardEditorEntry(null);
        }

        private ContentArtService EditorArt => editorOtherArt ?? contentArt;

        private void OpenCardEditor()
        {
            EnsureContentArt();
            CollectEditorGames();
            if (editorGames.Count == 0)
            {
                ShowMessage("There are no games with cards to edit.");
                return;
            }

            GameDefinition start = editorGame != null && editorGames.Contains(editorGame) ? editorGame : editorGames[0];
            ContentArtService previous = UseEditorGame(start);
            ShowCardEditor(string.Empty);
            previous?.Dispose();
        }

        // Every game the platform has: this table's game, then each game box in the component library (DLC games
        // arrive as more game boxes).
        private void CollectEditorGames()
        {
            editorGames.Clear();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (trapFloorGameDefinition != null && seen.Add(trapFloorGameDefinition.StableId ?? string.Empty))
            {
                editorGames.Add(trapFloorGameDefinition);
            }

            IReadOnlyList<ComponentCatalog> boxes = componentLibrary != null ? componentLibrary.GameBoxes : null;
            for (int i = 0; boxes != null && i < boxes.Count; i++)
            {
                GameDefinition game = boxes[i] != null ? boxes[i].Game : null;
                if (game != null && seen.Add(game.StableId ?? string.Empty)) editorGames.Add(game);
            }
        }

        // Loads one game's cards and decks from the profile. Returns the picture service no longer needed (dispose it
        // once the panel shows the new game).
        private ContentArtService UseEditorGame(GameDefinition game)
        {
            ContentArtService previous = editorOtherArt;
            editorOtherArt = null;
            if (!ReferenceEquals(game, trapFloorGameDefinition))
            {
                editorOtherArt = new ContentArtService(ContentPackStore.ForProfile(EnsureProfile()));
                editorOtherArt.UseGame(game);
            }

            editorGame = game;
            editorCards = EditorArt.Store.LoadCards(EditorArt.GameId);
            editorDecks = EditorArt.Store.LoadDecks(EditorArt.GameId);
            editorArtFolder = GameContent.ArtFolderOf(game);
            editorGameCards.Clear();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int s = 0; s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                for (int c = 0; set != null && c < set.Cards.Count; c++)
                {
                    CardDefinition card = set.Cards[c];
                    // Controller input cards are part of the controller, not of the game's deck.
                    if (card != null && card.RepresentedControllerInput == null && seen.Add(card.StableId)) editorGameCards.Add(card);
                }
            }

            editorDraft = editorGameCards.Count > 0 ? DraftForGameCard(editorGameCards[0]) : null;
            return previous;
        }

        private void CycleEditorGame(int step, CardEditorTyped typed)
        {
            if (editorGames.Count < 2) return;
            DropUnsavedNewCard();
            int index = Math.Max(0, editorGames.IndexOf(editorGame));
            GameDefinition next = editorGames[((index + step) % editorGames.Count + editorGames.Count) % editorGames.Count];
            ContentArtService previous = UseEditorGame(next);
            ShowCardEditor($"Now editing {next.DisplayName}'s cards.");
            previous?.Dispose();
        }

        // The table is going away: the picture service made for another game goes with it.
        private void DisposeCardEditor()
        {
            editorOtherArt?.Dispose();
            editorOtherArt = null;
        }

        private void ShowCardEditor(string status)
        {
            if (runtimeUi == null || editorCards == null) return;
            if (cardEditorActions == null)
            {
                cardEditorActions = new CardEditorActions
                {
                    CycleGame = CycleEditorGame,
                    Select = SelectEditorCard,
                    NewCard = NewEditorCard,
                    CycleWorksLike = CycleEditorWorksLike,
                    ToggleDeck = ToggleEditorDeck,
                    ChangeDeckCopies = ChangeEditorDeckCopies,
                    NewDeck = NewEditorDeck,
                    ChoosePicture = ChooseEditorPicture,
                    ClearPicture = ClearEditorPicture,
                    Save = SaveEditorCard,
                    Reset = ResetEditorCard,
                    Delete = DeleteEditorCard,
                    Close = CloseCardEditor,
                };
            }

            string gameName = editorGame.DisplayName ?? string.Empty;
            CardEditorModel model = new CardEditorModel
            {
                Title = "CARD EDITOR",
                GameName = gameName,
                CanChangeGame = editorGames.Count > 1,
                DeckSummary = EditorDeckSummary(gameName),
                Rows = BuildEditorRows(),
                Status = string.IsNullOrEmpty(status) ? CardEditorHint : status,
            };

            CardEditorDraft draft = editorDraft;
            if (draft != null)
            {
                CardDefinition model0 = FindEditorGameCard(draft.WorksLikeId);
                bool changed = !draft.IsNew && FindEditorChange(draft.Id) != null;
                model.Heading = draft.IsNew
                    ? (draft.IsSaved ? "YOUR CARD" : "NEW CARD (NOT SAVED YET)")
                    : (changed ? "GAME CARD (CHANGED)" : "GAME CARD");
                model.IdText = $"ID {draft.Id}";
                model.Code = draft.Code;
                model.CodeEditable = draft.IsNew;
                model.Name = draft.Name;
                model.Description = draft.Description;
                model.Cost = draft.Cost;
                model.WorksLike = model0 == null
                    ? "?"
                    : draft.IsNew ? $"{model0.DisplayName} ({model0.Category})" : $"Itself ({model0.Category})";
                model.CanChangeWorksLike = draft.IsNew && editorGameCards.Count > 1;
                model.Front = DraftPicture(draft.FrontKey, model0, true);
                model.Back = DraftPicture(draft.BackKey, model0, false);
                model.CanChoosePictures = PictureFilePicker.IsAvailable;
                model.CanClearFront = draft.IsNew ? draft.FrontKey.Length > 0 : !SameArtKey(draft.FrontKey, model0?.FrontArtworkReference);
                model.CanClearBack = !SameArtKey(draft.BackKey, model0?.BackArtworkReference);
                model.CanReset = true;
                model.ResetText = draft.IsNew ? "Undo edits" : "Reset to the game's card";
                model.CanDelete = draft.IsNew;
                model.Decks = BuildEditorDeckRows(draft, gameName);
            }

            if (!runtimeUi.ShowCardEditor(model, cardEditorActions))
            {
                ShowMessage("The card editor is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        private List<CardEditorRowModel> BuildEditorRows()
        {
            List<CardEditorRowModel> rows = new List<CardEditorRowModel>(editorGameCards.Count + editorCards.cards.Count + 1);
            for (int i = 0; i < editorGameCards.Count; i++)
            {
                CardDefinition card = editorGameCards[i];
                CardChangeRecord change = FindEditorChange(card.StableId);
                string name = change != null && change.changeName ? change.name : card.DisplayName;
                int copies = change != null && change.changeQuantity ? change.quantity : card.Quantity;
                string front = change != null && change.changeFrontArt ? change.frontArtKey : card.FrontArtworkReference;
                rows.Add(new CardEditorRowModel(
                    card.StableId,
                    name,
                    $"{(change != null ? "Changed" : "Game card")} · {card.Category} · {CopiesText(copies)}",
                    DraftPicture(front, card, true),
                    editorDraft != null && !editorDraft.IsNew && editorDraft.Id == card.StableId));
            }

            for (int i = 0; i < editorCards.cards.Count; i++)
            {
                PlayerCardRecord record = editorCards.cards[i];
                if (record == null) continue;
                CardDefinition model = FindEditorGameCard(record.behavesLike);
                string code = string.IsNullOrWhiteSpace(record.code) ? string.Empty : $"{record.code} · ";
                rows.Add(new CardEditorRowModel(
                    record.id,
                    string.IsNullOrWhiteSpace(record.name) ? "(no name)" : record.name,
                    $"{code}Your card · works like {(model != null ? model.DisplayName : "?")} · {CopiesText(record.quantity)}",
                    DraftPicture(FrontKeyOf(record), model, true),
                    editorDraft != null && editorDraft.IsNew && editorDraft.Id == record.id));
            }

            if (editorDraft != null && editorDraft.IsNew && !editorDraft.IsSaved)
            {
                rows.Add(new CardEditorRowModel(editorDraft.Id, string.IsNullOrWhiteSpace(editorDraft.Name) ? "New card" : editorDraft.Name,
                    "New card · not saved yet", DraftPicture(editorDraft.FrontKey, FindEditorGameCard(editorDraft.WorksLikeId), true), true));
            }

            return rows;
        }

        // ---------- actions ----------

        private void SelectEditorCard(string cardId)
        {
            DropUnsavedNewCard();
            CardDefinition card = FindEditorGameCard(cardId);
            if (card != null)
            {
                editorDraft = DraftForGameCard(card);
            }
            else
            {
                PlayerCardRecord record = FindEditorRecord(cardId);
                if (record != null) editorDraft = DraftForRecord(record);
            }

            ShowCardEditor(string.Empty);
        }

        private void NewEditorCard(CardEditorTyped typed)
        {
            DropUnsavedNewCard();
            CardDefinition model = editorDraft != null ? FindEditorGameCard(editorDraft.WorksLikeId) : null;
            if (model == null && editorGameCards.Count > 0) model = editorGameCards[0];
            if (model == null) return;
            editorDraft = new CardEditorDraft
            {
                IsNew = true,
                Id = PlayerCardsFile.NewCardId(),
                Name = "New card",
                Copies = 1,
                WorksLikeId = model.StableId,
                BackKey = model.BackArtworkReference ?? string.Empty,
            };
            ShowCardEditor("A new card. Give it a name, pick the card it works like and its pictures, then Save.");
        }

        // In a deck or not: the tick puts one copy in, or takes every copy out.
        private void ToggleEditorDeck(string deckId, CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            ApplyTyped(typed);
            SetDraftDeckCopies(deckId, DraftDeckCopies(deckId) > 0 ? 0 : 1);
        }

        private void ChangeEditorDeckCopies(string deckId, int step, CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            ApplyTyped(typed);
            SetDraftDeckCopies(deckId, DraftDeckCopies(deckId) + step);
        }

        private void SetDraftDeckCopies(string deckId, int copies)
        {
            copies = Mathf.Clamp(copies, 0, CardEditorMaximumCopies);
            if (deckId == GameDeckId)
            {
                editorDraft.Copies = copies;
                ShowCardEditor(copies == 0 ? "Out of the game's deck: the card is not used in play." : string.Empty);
                return;
            }

            editorDraft.DeckCopies[deckId] = copies;
            ShowCardEditor(string.Empty);
        }

        private int DraftDeckCopies(string deckId) =>
            deckId == GameDeckId
                ? editorDraft.Copies
                : editorDraft.DeckCopies.TryGetValue(deckId, out int copies) ? copies : 0;

        // A new deck of yours for this game; the card being edited is ticked into it (Save keeps it there).
        private void NewEditorDeck(CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            ApplyTyped(typed);
            PlayerDeckRecord deck = new PlayerDeckRecord
            {
                id = PlayerCardsFile.NewCardId(),
                name = $"My deck {editorDecks.decks.Count + 1}",
            };
            editorDecks.decks.Add(deck);
            EditorArt.Store.SaveDecks(EditorArt.GameId, editorDecks);
            editorDraft.DeckCopies[deck.id] = 1;
            ShowCardEditor($"Added {deck.name} with this card ticked in it. Save keeps the card there; decks are renamed in the deck editor.");
        }

        private void CycleEditorWorksLike(int step, CardEditorTyped typed)
        {
            if (editorDraft == null || !editorDraft.IsNew || editorGameCards.Count == 0) return;
            ApplyTyped(typed);
            int index = 0;
            for (int i = 0; i < editorGameCards.Count; i++)
            {
                if (editorGameCards[i].StableId == editorDraft.WorksLikeId) index = i;
            }

            CardDefinition previous = editorGameCards[index];
            CardDefinition next = editorGameCards[((index + step) % editorGameCards.Count + editorGameCards.Count) % editorGameCards.Count];
            editorDraft.WorksLikeId = next.StableId;
            if (SameArtKey(editorDraft.BackKey, previous.BackArtworkReference))
            {
                editorDraft.BackKey = next.BackArtworkReference ?? string.Empty; // the back follows the model unless chosen
            }

            ShowCardEditor($"Works like {next.DisplayName}: in play it does what {next.DisplayName} does.");
        }

        private void ChooseEditorPicture(bool front, CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            ApplyTyped(typed);
            if (!PictureFilePicker.TryPick(front ? "Choose the card's front picture" : "Choose the card's back picture", out string path))
            {
                ShowCardEditor(string.Empty);
                return;
            }

            string key = PlayerContent.DefaultArtKey(editorArtFolder, editorDraft.Id, front);
            if (!EditorArt.SetUpload(key, path, out string message))
            {
                ShowCardEditor(message);
                return;
            }

            if (front) editorDraft.FrontKey = key;
            else editorDraft.BackKey = key;
            ShowCardEditor("Picture copied into your profile. Press Save to keep it on the card.");
        }

        private void ClearEditorPicture(bool front, CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            ApplyTyped(typed);
            CardDefinition model = FindEditorGameCard(editorDraft.WorksLikeId);
            if (front)
            {
                editorDraft.FrontKey = editorDraft.IsNew ? string.Empty : model?.FrontArtworkReference ?? string.Empty;
            }
            else
            {
                editorDraft.BackKey = model?.BackArtworkReference ?? string.Empty;
            }

            ShowCardEditor(editorDraft.IsNew
                ? "Picture cleared. Press Save to keep the change."
                : "The game's picture is back. Press Save to keep the change.");
        }

        private void SaveEditorCard(CardEditorTyped typed)
        {
            CardEditorDraft draft = editorDraft;
            if (draft == null) return;
            ApplyTyped(typed);
            string name = draft.Name.Trim();
            if (name.Length == 0)
            {
                ShowCardEditor("Give the card a name first.");
                return;
            }

            if (!InputCostNotation.TryParse(draft.Cost, out InputCostData costData, out string costError))
            {
                ShowCardEditor($"The cost could not be read: {costError}. Write it like Up + A/B/X/Y.");
                return;
            }

            draft.Name = name;
            draft.Cost = InputCostNotation.Format(costData);
            draft.Copies = Mathf.Clamp(draft.Copies, 0, CardEditorMaximumCopies);
            if (draft.IsNew && !TryCleanCode(draft, out string codeError))
            {
                ShowCardEditor(codeError);
                return;
            }

            if (draft.IsNew) SaveNewCardRecord(draft);
            else SaveGameCardChange(draft);
            if (!EditorArt.Store.SaveCards(EditorArt.GameId, editorCards))
            {
                ShowCardEditor("Your cards could not be saved; the console says why.");
                return;
            }

            SaveDraftDecks(draft);

            draft.IsSaved = true;
            RemoveUnusedEditorPictures(draft.Id);
            ShowCardEditor($"Saved {draft.Name}. Start a new table (Table > New table…) to play with it.");
        }

        private void ResetEditorCard()
        {
            CardEditorDraft draft = editorDraft;
            if (draft == null) return;
            if (!draft.IsNew)
            {
                CardChangeRecord change = FindEditorChange(draft.Id);
                if (change != null)
                {
                    editorCards.changes.Remove(change);
                    EditorArt.Store.SaveCards(EditorArt.GameId, editorCards);
                }

                RemoveUnusedEditorPictures(draft.Id);
                CardDefinition card = FindEditorGameCard(draft.Id);
                editorDraft = card != null ? DraftForGameCard(card) : null;
                ShowCardEditor(change != null ? "Back to the game's own card." : "Edits undone.");
                return;
            }

            PlayerCardRecord record = FindEditorRecord(draft.Id);
            if (record != null)
            {
                editorDraft = DraftForRecord(record);
                ShowCardEditor("Edits undone.");
                return;
            }

            string worksLike = draft.WorksLikeId;
            DropUnsavedNewCard();
            CardDefinition model = FindEditorGameCard(worksLike);
            editorDraft = model != null ? DraftForGameCard(model) : null;
            ShowCardEditor("The new card was dropped.");
        }

        private void DeleteEditorCard()
        {
            CardEditorDraft draft = editorDraft;
            if (draft == null || !draft.IsNew) return;
            string name = string.IsNullOrWhiteSpace(draft.Name) ? "the card" : draft.Name;
            PlayerCardRecord record = FindEditorRecord(draft.Id);
            if (record != null)
            {
                editorCards.cards.Remove(record);
                EditorArt.Store.SaveCards(EditorArt.GameId, editorCards);
                RemoveFromDecks(draft.Id);
            }

            RemoveEditorPicture(PlayerContent.DefaultArtKey(editorArtFolder, draft.Id, true));
            RemoveEditorPicture(PlayerContent.DefaultArtKey(editorArtFolder, draft.Id, false));
            CardDefinition model = FindEditorGameCard(draft.WorksLikeId);
            editorDraft = model != null ? DraftForGameCard(model) : null;
            ShowCardEditor($"Deleted {name}.");
        }

        private void CloseCardEditor()
        {
            DropUnsavedNewCard();
            runtimeUi?.CloseCardEditor();
            DisposeCardEditor();
            ReturnToGameSelect();
        }

        // ---------- saving ----------

        private void SaveNewCardRecord(CardEditorDraft draft)
        {
            PlayerCardRecord record = FindEditorRecord(draft.Id);
            if (record == null)
            {
                record = new PlayerCardRecord { id = draft.Id };
                editorCards.cards.Add(record);
            }

            CardDefinition model = FindEditorGameCard(draft.WorksLikeId);
            record.code = draft.Code;
            record.name = draft.Name;
            record.description = draft.Description;
            record.behavesLike = draft.WorksLikeId;
            record.quantity = draft.Copies;
            record.cost = draft.Cost;
            record.frontArtKey = draft.FrontKey;
            record.backArtKey = SameArtKey(draft.BackKey, model?.BackArtworkReference) ? string.Empty : draft.BackKey;
        }

        // Only what differs from the game's card is kept; with nothing different the change is removed.
        private void SaveGameCardChange(CardEditorDraft draft)
        {
            CardDefinition card = FindEditorGameCard(draft.Id);
            if (card == null) return;
            CardChangeRecord change = new CardChangeRecord { cardId = card.StableId };
            if (!string.Equals(draft.Name, card.DisplayName, StringComparison.Ordinal))
            {
                change.changeName = true;
                change.name = draft.Name;
            }

            if (!string.Equals(draft.Description ?? string.Empty, card.Description ?? string.Empty, StringComparison.Ordinal))
            {
                change.changeDescription = true;
                change.description = draft.Description;
            }

            if (draft.Copies != card.Quantity)
            {
                change.changeQuantity = true;
                change.quantity = draft.Copies;
            }

            if (!string.Equals(draft.Cost, CostText(card), StringComparison.Ordinal))
            {
                change.changeCost = true;
                change.cost = draft.Cost;
            }

            if (!SameArtKey(draft.FrontKey, card.FrontArtworkReference))
            {
                change.changeFrontArt = true;
                change.frontArtKey = draft.FrontKey;
            }

            if (!SameArtKey(draft.BackKey, card.BackArtworkReference))
            {
                change.changeBackArt = true;
                change.backArtKey = draft.BackKey;
            }

            CardChangeRecord existing = FindEditorChange(card.StableId);
            if (existing != null) editorCards.changes.Remove(existing);
            if (change.HasAnyChange) editorCards.changes.Add(change);
        }

        private bool TryCleanCode(CardEditorDraft draft, out string error)
        {
            error = string.Empty;
            string code = draft.Code.Trim().ToUpperInvariant();
            if (code.Length > CardEditorMaximumCodeLength)
            {
                error = $"A short code has at most {CardEditorMaximumCodeLength} letters.";
                return false;
            }

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                if (!char.IsLetterOrDigit(c) && c != '-')
                {
                    error = "A short code uses letters, digits and '-' only.";
                    return false;
                }
            }

            for (int i = 0; code.Length > 0 && i < editorCards.cards.Count; i++)
            {
                PlayerCardRecord other = editorCards.cards[i];
                if (other != null && other.id != draft.Id && string.Equals(other.code, code, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"{other.name} already uses the code {code}.";
                    return false;
                }
            }

            draft.Code = code;
            return true;
        }

        private void RemoveFromDecks(string cardId)
        {
            bool changed = false;
            for (int d = 0; d < editorDecks.decks.Count; d++)
            {
                if (editorDecks.decks[d] == null) continue;
                changed |= editorDecks.decks[d].cards.RemoveAll(entry => entry != null && entry.cardId == cardId) > 0;
            }

            if (changed) EditorArt.Store.SaveDecks(EditorArt.GameId, editorDecks);
        }

        // The card's copies in each of your decks, as the draft says.
        private void SaveDraftDecks(CardEditorDraft draft)
        {
            bool changed = false;
            for (int d = 0; d < editorDecks.decks.Count; d++)
            {
                PlayerDeckRecord deck = editorDecks.decks[d];
                if (deck == null) continue;
                int saved = SavedDeckCopies(deck, draft.Id);
                int wanted = draft.DeckCopies.TryGetValue(deck.id, out int copies) ? copies : 0;
                if (saved == wanted) continue;
                deck.cards.RemoveAll(entry => entry != null && string.Equals(entry.cardId, draft.Id, StringComparison.OrdinalIgnoreCase));
                if (wanted > 0) deck.cards.Add(new PlayerDeckEntry { cardId = draft.Id, count = wanted });
                changed = true;
            }

            if (changed) EditorArt.Store.SaveDecks(EditorArt.GameId, editorDecks);
        }

        private static int SavedDeckCopies(PlayerDeckRecord deck, string cardId)
        {
            int copies = 0;
            for (int i = 0; i < deck.cards.Count; i++)
            {
                PlayerDeckEntry entry = deck.cards[i];
                if (entry != null && string.Equals(entry.cardId, cardId, StringComparison.OrdinalIgnoreCase)) copies += entry.count;
            }

            return copies;
        }

        // ---------- the game's deck and the "In decks" list ----------

        // "TRAP FLOOR DECK · 48 CARDS": every card's copies in the game, as edited now (yours included).
        private string EditorDeckSummary(string gameName)
        {
            int total = 0;
            int yours = 0;
            for (int i = 0; i < editorGameCards.Count; i++)
            {
                CardDefinition card = editorGameCards[i];
                if (editorDraft != null && !editorDraft.IsNew && editorDraft.Id == card.StableId)
                {
                    total += editorDraft.Copies;
                    continue;
                }

                CardChangeRecord change = FindEditorChange(card.StableId);
                total += change != null && change.changeQuantity ? change.quantity : card.Quantity;
            }

            for (int i = 0; i < editorCards.cards.Count; i++)
            {
                PlayerCardRecord record = editorCards.cards[i];
                if (record == null) continue;
                int copies = editorDraft != null && editorDraft.IsNew && editorDraft.Id == record.id ? editorDraft.Copies : record.quantity;
                total += copies;
                yours += copies;
            }

            if (editorDraft != null && editorDraft.IsNew && !editorDraft.IsSaved)
            {
                total += editorDraft.Copies;
                yours += editorDraft.Copies;
            }

            string own = yours > 0 ? $" ({yours} of yours)" : string.Empty;
            return $"{gameName.ToUpperInvariant()} DECK · {total} CARD{(total == 1 ? string.Empty : "S")}{own}";
        }

        private List<CardEditorDeckRowModel> BuildEditorDeckRows(CardEditorDraft draft, string gameName)
        {
            List<CardEditorDeckRowModel> rows = new List<CardEditorDeckRowModel>(1 + editorDecks.decks.Count)
            {
                new CardEditorDeckRowModel(GameDeckId, $"{gameName} deck", "The game's own deck: copies in play", draft.Copies),
            };
            for (int d = 0; d < editorDecks.decks.Count; d++)
            {
                PlayerDeckRecord deck = editorDecks.decks[d];
                if (deck == null || string.IsNullOrWhiteSpace(deck.id)) continue;
                int copies = draft.DeckCopies.TryGetValue(deck.id, out int value) ? value : 0;
                int size = 0;
                for (int i = 0; i < deck.cards.Count; i++) size += deck.cards[i] != null ? deck.cards[i].count : 0;
                size += copies - SavedDeckCopies(deck, draft.Id);
                rows.Add(new CardEditorDeckRowModel(deck.id, string.IsNullOrWhiteSpace(deck.name) ? "My deck" : deck.name,
                    $"Your deck · {size} card{(size == 1 ? string.Empty : "s")}", copies));
            }

            return rows;
        }

        private void FillDeckCopies(CardEditorDraft draft)
        {
            draft.DeckCopies.Clear();
            for (int d = 0; editorDecks != null && d < editorDecks.decks.Count; d++)
            {
                PlayerDeckRecord deck = editorDecks.decks[d];
                if (deck == null || string.IsNullOrWhiteSpace(deck.id)) continue;
                int copies = SavedDeckCopies(deck, draft.Id);
                if (copies > 0) draft.DeckCopies[deck.id] = copies;
            }
        }

        // A card's own pictures (its player art keys) that its saved card no longer uses are removed.
        private void RemoveUnusedEditorPictures(string cardId)
        {
            string front = PlayerContent.DefaultArtKey(editorArtFolder, cardId, true);
            string back = PlayerContent.DefaultArtKey(editorArtFolder, cardId, false);
            PlayerCardRecord record = FindEditorRecord(cardId);
            CardChangeRecord change = FindEditorChange(cardId);
            if (!SavedUses(record, change, front)) RemoveEditorPicture(front);
            if (!SavedUses(record, change, back)) RemoveEditorPicture(back);
        }

        private static bool SavedUses(PlayerCardRecord record, CardChangeRecord change, string key) =>
            (record != null && (SameArtKey(record.frontArtKey, key) || SameArtKey(record.backArtKey, key)))
            || (change != null && ((change.changeFrontArt && SameArtKey(change.frontArtKey, key))
                || (change.changeBackArt && SameArtKey(change.backArtKey, key))));

        private void RemoveEditorPicture(string key)
        {
            if (EditorArt.HasUpload(key)) EditorArt.RemoveUpload(key, out _);
        }

        private void DropUnsavedNewCard()
        {
            if (editorDraft != null && editorDraft.IsNew && !editorDraft.IsSaved)
            {
                RemoveUnusedEditorPictures(editorDraft.Id);
            }
        }

        // ---------- drafts and lookups ----------

        private CardEditorDraft DraftForGameCard(CardDefinition card)
        {
            CardChangeRecord change = FindEditorChange(card.StableId);
            CardEditorDraft draft = new CardEditorDraft
            {
                IsNew = false,
                IsSaved = true,
                Id = card.StableId,
                Name = change != null && change.changeName ? change.name : card.DisplayName,
                Description = (change != null && change.changeDescription ? change.description : card.Description) ?? string.Empty,
                Copies = change != null && change.changeQuantity ? change.quantity : card.Quantity,
                Cost = change != null && change.changeCost ? change.cost : CostText(card),
                WorksLikeId = card.StableId,
                FrontKey = change != null && change.changeFrontArt ? change.frontArtKey : card.FrontArtworkReference ?? string.Empty,
                BackKey = change != null && change.changeBackArt ? change.backArtKey : card.BackArtworkReference ?? string.Empty,
            };
            FillDeckCopies(draft);
            return draft;
        }

        private CardEditorDraft DraftForRecord(PlayerCardRecord record)
        {
            CardDefinition model = FindEditorGameCard(record.behavesLike);
            CardEditorDraft draft = new CardEditorDraft
            {
                IsNew = true,
                IsSaved = true,
                Id = record.id,
                Code = record.code ?? string.Empty,
                Name = record.name ?? string.Empty,
                Description = record.description ?? string.Empty,
                Copies = record.quantity,
                Cost = record.cost ?? string.Empty,
                WorksLikeId = model != null ? model.StableId : record.behavesLike ?? string.Empty,
                FrontKey = FrontKeyOf(record),
                BackKey = string.IsNullOrWhiteSpace(record.backArtKey)
                    ? model?.BackArtworkReference ?? string.Empty
                    : record.backArtKey,
            };
            FillDeckCopies(draft);
            return draft;
        }

        // A saved new card shows the picture under its own key (the same key the game uses for it, doc 24 L2-1).
        private string FrontKeyOf(PlayerCardRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.frontArtKey)) return record.frontArtKey;
            string key = PlayerContent.DefaultArtKey(editorArtFolder, record.id, true);
            return EditorArt.HasUpload(key) ? key : string.Empty;
        }

        private void ApplyTyped(CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            if (editorDraft.IsNew) editorDraft.Code = typed.Code.Trim();
            editorDraft.Name = typed.Name;
            editorDraft.Description = typed.Description;
            editorDraft.Cost = typed.Cost.Trim();
        }

        // The picture for a key: yours or a pack's by key, else the model card's own texture when the key is its own.
        private Texture2D DraftPicture(string key, CardDefinition model, bool front)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Texture2D own = null;
            if (model != null)
            {
                own = front
                    ? (SameArtKey(key, model.FrontArtworkReference) ? model.FrontArtwork : null)
                    : (SameArtKey(key, model.BackArtworkReference) ? model.BackArtwork : null);
            }

            return EditorArt.Resolve(key, own);
        }

        private CardDefinition FindEditorGameCard(string cardId)
        {
            for (int i = 0; i < editorGameCards.Count; i++)
            {
                if (string.Equals(editorGameCards[i].StableId, cardId, StringComparison.OrdinalIgnoreCase)) return editorGameCards[i];
            }

            return null;
        }

        private CardChangeRecord FindEditorChange(string cardId)
        {
            for (int i = 0; editorCards != null && i < editorCards.changes.Count; i++)
            {
                CardChangeRecord change = editorCards.changes[i];
                if (change != null && string.Equals(change.cardId, cardId, StringComparison.OrdinalIgnoreCase)) return change;
            }

            return null;
        }

        private PlayerCardRecord FindEditorRecord(string cardId)
        {
            for (int i = 0; editorCards != null && i < editorCards.cards.Count; i++)
            {
                PlayerCardRecord record = editorCards.cards[i];
                if (record != null && string.Equals(record.id, cardId, StringComparison.OrdinalIgnoreCase)) return record;
            }

            return null;
        }

        private static string CostText(CardDefinition card) => InputCostNotation.Format(card.ToData().InputCost);

        private static string CopiesText(int copies) => copies == 1 ? "1 copy" : $"{copies} copies";

        private static bool SameArtKey(string a, string b) =>
            string.Equals(ArtLibrary.NormalizeKey(a), ArtLibrary.NormalizeKey(b), StringComparison.Ordinal);
    }
}
