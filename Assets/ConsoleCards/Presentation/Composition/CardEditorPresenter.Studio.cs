using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Card studio (board 10), the parts added 2026-10-30 (owner: "the one you not built, I want the card studio in
    /// it"): card size, crop or fit for a picture of another shape, DUPLICATE, IMPORT A FOLDER OF PNGs and UNDO
    /// back to the card as it was when chosen. Every change is saved at once (auto-save).
    /// </summary>
    internal sealed partial class CardEditorPresenter
    {
        // A card is 5 : 7 (2.5 x 3.5"), large (avatar) cards too.
        internal const float CardAspect = 5f / 7f;
        private const string SourceSuffix = "-source";
        private const int MaximumImportReport = 6;

        // UNDO: the cards and decks as they were when this card was chosen.
        internal string editorUndoCards = string.Empty;
        internal string editorUndoDecks = string.Empty;
        internal string editorUndoCardId = string.Empty;

        internal void TakeEditorUndo()
        {
            if (editorCards == null || editorDecks == null) return;
            editorUndoCards = JsonUtility.ToJson(editorCards);
            editorUndoDecks = JsonUtility.ToJson(editorDecks);
            editorUndoCardId = editorDraft != null ? editorDraft.Id : string.Empty;
        }

        internal void UndoEditorCard()
        {
            if (editorUndoCards.Length == 0 || editorUndoDecks.Length == 0) return;
            editorCards = JsonUtility.FromJson<PlayerCardsFile>(editorUndoCards).Normalised();
            editorDecks = JsonUtility.FromJson<PlayerDecksFile>(editorUndoDecks).Normalised();
            bool saved = EditorArt.Store.SaveCards(EditorArt.GameId, editorCards)
                & EditorArt.Store.SaveDecks(EditorArt.GameId, editorDecks);
            CardDefinition card = FindEditorGameCard(editorUndoCardId);
            PlayerCardRecord record = FindEditorRecord(editorUndoCardId);
            editorDraft = card != null ? DraftForGameCard(card)
                : record != null ? DraftForRecord(record)
                : editorGameCards.Count > 0 ? DraftForGameCard(editorGameCards[0]) : null;
            editorAutoSaved = saved;
            ShowCardEditor(saved
                ? "Undone: the card is as it was when you chose it."
                : "Your cards could not be saved; the console says why.");
        }

        // ---------- card size and picture shape ----------

        internal static bool ModelIsLarge(CardDefinition model) => model != null && model.SizeClass == CardSizeClass.Large;

        internal void SetEditorSize(bool large, CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            ApplyTyped(typed);
            editorDraft.Large = large;
            ShowCardEditor(large
                ? "Large: the card plays at avatar size."
                : "Standard: the card plays at the normal card size.");
        }

        // Crop to fill or fit with a border. The player's own pictures are shaped again from the picture as chosen.
        internal void SetEditorFit(bool fit, CardEditorTyped typed)
        {
            if (editorDraft == null) return;
            ApplyTyped(typed);
            editorDraft.FitPicture = fit;
            int reshaped = 0;
            if (ReshapeFromSource(editorDraft.FrontKey, fit)) reshaped++;
            if (ReshapeFromSource(editorDraft.BackKey, fit)) reshaped++;
            string how = fit ? "Fit: a picture of another shape gets a border." : "Crop to fill: a picture of another shape is cropped.";
            ShowCardEditor(reshaped > 0 ? how + " The picture was shaped again." : how);
        }

        private bool ReshapeFromSource(string key, bool fit)
        {
            if (string.IsNullOrEmpty(key) || !EditorArt.HasUpload(key)) return false;
            if (!EditorArt.TryGetUploadPath(key + SourceSuffix, out string source)) return false;
            if (!CardPictureShaper.TryShape(source, CardAspect, fit, "reshaped", out string shaped, out _, out _)) return false;
            return EditorArt.SetUpload(key, shaped, out _);
        }

        // A chosen picture: the file as chosen is kept (to shape again later), the card uses its card-shaped copy.
        internal bool StoreShapedPicture(string key, string path, bool fit, out string message)
        {
            if (!CardPictureShaper.TryShape(path, CardAspect, fit, "chosen", out string shaped, out bool reshaped, out string error))
            {
                message = $"That picture could not be read: {error}.";
                return false;
            }

            if (!EditorArt.SetUpload(key, shaped, out message)) return false;
            if (reshaped) EditorArt.SetUpload(key + SourceSuffix, path, out _);
            else if (EditorArt.HasUpload(key + SourceSuffix)) EditorArt.RemoveUpload(key + SourceSuffix, out _);
            message = reshaped
                ? (fit ? "Picture saved on the card, fitted with a border (it is another shape)." : "Picture saved on the card, cropped to fill (it is another shape).")
                : "Picture saved on the card (a copy is kept in your profile).";
            return true;
        }

        internal void RemoveEditorPictureSource(string key)
        {
            if (!string.IsNullOrEmpty(key) && EditorArt.HasUpload(key + SourceSuffix)) EditorArt.RemoveUpload(key + SourceSuffix, out _);
        }

        // ---------- DUPLICATE ----------

        internal void DuplicateEditorCard(CardEditorTyped typed)
        {
            CardEditorDraft from = editorDraft;
            if (from == null) return;
            ApplyTyped(typed);
            if (!IsUntouchedNewCard(from)) TrySaveEditorDraft(from, out _);
            string name = "Copy of " + (string.IsNullOrWhiteSpace(from.Name) ? "card" : from.Name.Trim());
            if (name.Length > 40) name = name.Substring(0, 40).TrimEnd();
            CardEditorDraft copy = new CardEditorDraft
            {
                IsNew = true,
                Id = PlayerCardsFile.NewCardId(),
                Name = name,
                Description = from.Description,
                Copies = 1,
                Cost = from.Cost,
                WorksLikeId = from.WorksLikeId,
                Frame = from.Frame,
                Large = from.Large,
                FitPicture = from.FitPicture,
            };
            CopyIcons(from.Icons, copy.Icons);
            copy.FrontKey = CopyPicture(from.FrontKey, copy.Id, true);
            copy.BackKey = CopyPicture(from.BackKey, copy.Id, false);
            editorDraft = copy;
            editorAutoSaved = TrySaveEditorDraft(copy, out string problem);
            TakeEditorUndo();
            ShowCardEditor(editorAutoSaved ? $"Made {copy.Name}: change it as you like." : "Not saved yet: " + problem);
        }

        // The player's own picture is copied to the new card's own key; a game picture is shared by key.
        private string CopyPicture(string key, string newCardId, bool front)
        {
            if (string.IsNullOrEmpty(key) || !EditorArt.HasUpload(key) || !EditorArt.TryGetUploadPath(key, out string path)) return key ?? string.Empty;
            string newKey = PlayerContent.DefaultArtKey(editorArtFolder, newCardId, front);
            if (!EditorArt.SetUpload(newKey, path, out _)) return key;
            if (EditorArt.TryGetUploadPath(key + SourceSuffix, out string source)) EditorArt.SetUpload(newKey + SourceSuffix, source, out _);
            return newKey;
        }

        // ---------- IMPORT A FOLDER OF PNGs ----------

        // Each picture goes to the card with its name ("Golden Key.png", "golden-key.png"); "<name> back.png" is its
        // back. Each card is saved at once; pictures that match no card are listed.
        internal void ImportEditorFolder(CardEditorTyped typed)
        {
            if (editorDraft != null)
            {
                ApplyTyped(typed);
                if (!IsUntouchedNewCard(editorDraft)) TrySaveEditorDraft(editorDraft, out _);
            }

            if (!PictureFilePicker.TryPickFolder("Choose a folder of card pictures", out string folder))
            {
                ShowCardEditor(string.Empty);
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception exception)
            {
                ShowCardEditor("That folder could not be read: " + exception.Message);
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            string keepId = editorDraft != null ? editorDraft.Id : string.Empty;
            int done = 0;
            List<string> unmatched = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") continue;
                string stem = Path.GetFileNameWithoutExtension(files[i]);
                bool back = TryCutBack(ref stem);
                CardEditorDraft draft = DraftForImportName(MatchName(stem));
                if (draft == null)
                {
                    unmatched.Add(Path.GetFileName(files[i]));
                    continue;
                }

                string key = PlayerContent.DefaultArtKey(editorArtFolder, draft.Id, !back);
                if (!StoreShapedPicture(key, files[i], draft.FitPicture, out _)) continue;
                if (back) draft.BackKey = key;
                else draft.FrontKey = key;
                if (TrySaveEditorDraft(draft, out _)) done++;
            }

            CardDefinition card = FindEditorGameCard(keepId);
            PlayerCardRecord record = FindEditorRecord(keepId);
            if (card != null) editorDraft = DraftForGameCard(card);
            else if (record != null) editorDraft = DraftForRecord(record);
            editorAutoSaved = done > 0;
            TakeEditorUndo();
            ShowCardEditor(ImportReport(done, unmatched));
        }

        private static bool TryCutBack(ref string stem)
        {
            string lower = stem.ToLowerInvariant();
            string[] endings = { " back", "-back", "_back", ".back" };
            for (int i = 0; i < endings.Length; i++)
            {
                if (!lower.EndsWith(endings[i], StringComparison.Ordinal)) continue;
                stem = stem.Substring(0, stem.Length - endings[i].Length);
                return true;
            }

            return false;
        }

        // Letters and digits only, lower case: "Golden Key", "golden-key" and "golden_key" are the same name.
        internal static string MatchName(string words)
        {
            StringBuilder name = new StringBuilder(words?.Length ?? 0);
            for (int i = 0; words != null && i < words.Length; i++)
            {
                if (char.IsLetterOrDigit(words[i])) name.Append(char.ToLowerInvariant(words[i]));
            }

            return name.ToString();
        }

        // The card (game card by its shown name, or one of yours by name or code) a picture's name matches.
        private CardEditorDraft DraftForImportName(string name)
        {
            if (name.Length == 0) return null;
            for (int i = 0; i < editorCards.cards.Count; i++)
            {
                PlayerCardRecord record = editorCards.cards[i];
                if (record == null) continue;
                if (MatchName(record.name) == name || (!string.IsNullOrEmpty(record.code) && MatchName(record.code) == name))
                {
                    return DraftForRecord(record);
                }
            }

            for (int i = 0; i < editorGameCards.Count; i++)
            {
                CardDefinition card = editorGameCards[i];
                CardChangeRecord change = FindEditorChange(card.StableId);
                string shown = change != null && change.changeName ? change.name : card.DisplayName;
                if (MatchName(shown) == name || MatchName(card.DisplayName) == name) return DraftForGameCard(card);
            }

            return null;
        }

        private static string ImportReport(int done, List<string> unmatched)
        {
            StringBuilder words = new StringBuilder(160);
            words.Append(done == 0 ? "No pictures were added." : $"Added {done} picture{(done == 1 ? string.Empty : "s")} and saved the cards.");
            if (unmatched.Count == 0) return words.ToString();
            words.Append(" No card is called ");
            for (int i = 0; i < unmatched.Count && i < MaximumImportReport; i++)
            {
                if (i > 0) words.Append(", ");
                words.Append(unmatched[i]);
            }

            if (unmatched.Count > MaximumImportReport) words.Append($" and {unmatched.Count - MaximumImportReport} more");
            words.Append(". Name a file like its card (add \"back\" for a back).");
            return words.ToString();
        }
    }
}
