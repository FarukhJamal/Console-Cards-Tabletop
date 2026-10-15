using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>
    /// The card editor (doc 27 List 2, L2-2): Table menu > Card editor… lists the game's cards and the player's own,
    /// and edits one at a time with a live preview. A game card's edits are saved as a change (Reset brings the
    /// game's card back); a new card works like a game card the player picks. Everything is saved in the player's
    /// profile (doc 28) when the player presses Save, and used from the next table that starts.
    /// </summary>
    public sealed partial class TabletopPrototypeComposition
    {
        private const int CardEditorMaximumCopies = 20;
        private const int CardEditorMaximumCodeLength = 8;
        private const string CardEditorHint =
            "Saved cards are used from the next table you start. A cost set as a house rule on the rules card wins over the cost here.";

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
        }

        private readonly List<CardDefinition> editorGameCards = new List<CardDefinition>();
        private PlayerCardsFile editorCards;
        private CardEditorDraft editorDraft;
        private CardEditorActions cardEditorActions;
        private string editorArtFolder = string.Empty;

        private void RefreshCardEditorEntry()
        {
            runtimeUi?.ShowCardEditorEntry(trapFloorGameDefinition != null ? (Action)OpenCardEditor : null);
        }

        private void OpenCardEditor()
        {
            EnsureContentArt();
            if (!contentArt.HasGame)
            {
                ShowMessage("There are no game cards to edit here.");
                return;
            }

            editorCards = contentArt.Store.LoadCards(contentArt.GameId);
            editorArtFolder = GameContent.ArtFolderOf(trapFloorGameDefinition);
            editorGameCards.Clear();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int s = 0; s < trapFloorGameDefinition.ContentSets.Count; s++)
            {
                GameContentSet set = trapFloorGameDefinition.ContentSets[s];
                for (int c = 0; set != null && c < set.Cards.Count; c++)
                {
                    CardDefinition card = set.Cards[c];
                    // Controller input cards are part of the controller, not cards to change.
                    if (card != null && card.RepresentedControllerInput == null && seen.Add(card.StableId)) editorGameCards.Add(card);
                }
            }

            editorDraft = editorGameCards.Count > 0 ? DraftForGameCard(editorGameCards[0]) : null;
            ShowCardEditor(string.Empty);
        }

        private void ShowCardEditor(string status)
        {
            if (runtimeUi == null || editorCards == null) return;
            if (cardEditorActions == null)
            {
                cardEditorActions = new CardEditorActions
                {
                    Select = SelectEditorCard,
                    NewCard = NewEditorCard,
                    ChangeCopies = ChangeEditorCopies,
                    CycleWorksLike = CycleEditorWorksLike,
                    ChoosePicture = ChooseEditorPicture,
                    ClearPicture = ClearEditorPicture,
                    Save = SaveEditorCard,
                    Reset = ResetEditorCard,
                    Delete = DeleteEditorCard,
                    Close = CloseCardEditor,
                };
            }

            CardEditorModel model = new CardEditorModel
            {
                Title = $"CARD EDITOR · {(trapFloorGameDefinition.DisplayName ?? string.Empty).ToUpperInvariant()}",
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
                model.Copies = draft.Copies;
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

        private void ChangeEditorCopies(int step, CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            ApplyTyped(typed);
            editorDraft.Copies = Mathf.Clamp(editorDraft.Copies + step, 0, CardEditorMaximumCopies);
            ShowCardEditor(editorDraft.Copies == 0 ? "With 0 copies the card is not used in play." : string.Empty);
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
            if (!contentArt.SetUpload(key, path, out string message))
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
            if (!contentArt.Store.SaveCards(contentArt.GameId, editorCards))
            {
                ShowCardEditor("Your cards could not be saved; the console says why.");
                return;
            }

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
                    contentArt.Store.SaveCards(contentArt.GameId, editorCards);
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
                contentArt.Store.SaveCards(contentArt.GameId, editorCards);
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
            PlayerDecksFile decks = contentArt.Store.LoadDecks(contentArt.GameId);
            bool changed = false;
            for (int d = 0; d < decks.decks.Count; d++)
            {
                if (decks.decks[d] == null) continue;
                changed |= decks.decks[d].cards.RemoveAll(entry => entry != null && entry.cardId == cardId) > 0;
            }

            if (changed) contentArt.Store.SaveDecks(contentArt.GameId, decks);
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
            if (contentArt.HasUpload(key)) contentArt.RemoveUpload(key, out _);
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
            return new CardEditorDraft
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
        }

        private CardEditorDraft DraftForRecord(PlayerCardRecord record)
        {
            CardDefinition model = FindEditorGameCard(record.behavesLike);
            return new CardEditorDraft
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
        }

        // A saved new card shows the picture under its own key (the same key the game uses for it, doc 24 L2-1).
        private string FrontKeyOf(PlayerCardRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.frontArtKey)) return record.frontArtKey;
            string key = PlayerContent.DefaultArtKey(editorArtFolder, record.id, true);
            return contentArt.HasUpload(key) ? key : string.Empty;
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

            return Art(key, own);
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
