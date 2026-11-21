using System;
using System.Collections.Generic;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
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
    /// MAKE-B, board 13 (owner, 2026-10-30): what the Make a game screen shows besides the steps: ABOUT (how long a
    /// game takes), which steps are done (a green mark), and WHAT GETS SAVED (the table, its pieces, decks and deal).
    /// </summary>
    internal sealed partial class PlayerGamesPresenter
    {
        internal int saveMinutes = 20;
        private static readonly int[] MinuteChoices = { 5, 10, 15, 20, 30, 45, 60, 90, 120 };

        internal void CycleSaveMinutes(int step, GameSaveTyped typed)
        {
            KeepTyped(typed);
            int at = Array.IndexOf(MinuteChoices, saveMinutes);
            if (at < 0) at = 3;
            at = Mathf.Clamp(at + step, 0, MinuteChoices.Length - 1);
            saveMinutes = MinuteChoices[at];
            ShowGameSave(string.Empty);
        }

        internal void FillBoardParts(GameSaveModel model, SavedTable table, DeckChoice deck, bool deckOnTable)
        {
            model.Minutes = saveMinutes;
            model.SheetPreview = SheetPreviewLines(); // RULES-SHEET (R2)
            bool hasDeck = deck.Game != null;
            List<ObjectDefinitionId> deckCardIds = hasDeck ? SaveDeckCards(deck, out _, out _) : null;
            int deckCards = deckCardIds != null ? deckCardIds.Count : 0;
            model.StepsDone = new[]
            {
                hasDeck ? deckOnTable : table.Count > 0, // Deck: the game's deck is on the table (or it needs none)
                hasDeck && deckCards > 0, // Cards: its deck has cards
                saveDeal != null, // Deal: it deals
                RulesSheet.HasContent(saveSheet, saveSheetText), // Rules: its rules sheet has something in it
                false,
            };

            int piles = 0;
            int pileCards = 0;
            for (int i = 0; i < table.piles.Count; i++)
            {
                if (table.piles[i] == null) continue;
                piles++;
                pileCards += table.piles[i].cards.Count;
            }

            List<GameSaveRow> rows = new List<GameSaveRow>(5)
            {
                new GameSaveRow("Table", host.CurrentTableShapeName),
                new GameSaveRow("Pieces on it", table.Count == 0
                    ? "none yet"
                    : $"{CountText(table.pieces.Count + table.boards.Count + table.consoles.Count, "piece")}, where they sit"),
                new GameSaveRow("Decks", hasDeck
                    ? $"{deck.Name} · {CountText(deckCards, "card")}" + (piles > 0 ? $" + {CountText(piles, "pile")}" : string.Empty)
                    : piles > 0 ? $"{CountText(piles, "pile")} ({CountText(pileCards, "card")})" : "none"),
                new GameSaveRow("Deal", saveDeal == null
                    ? "no dealing"
                    : saveDeal.DealsAll ? "all the cards, evenly" : $"{CountText(saveDeal.CountEach, "card")} to each "
                        + (saveDeal.To == DealDestination.Hand ? "hand" : "player on the table")),
                new GameSaveRow("Players", saveMinimumPlayers == saveMaximumPlayers
                    ? CountText(saveMinimumPlayers, "player")
                    : $"{saveMinimumPlayers}–{saveMaximumPlayers} players"),
            };
            FillLayoutRow(model, rows); // doc 63 G2
            FillCover(model, rows, deckCardIds); // doc 63 G1
            model.SavedRows = rows;
        }

        // ---------- doc 63 G1 (owner, 2026-11-21): the game's cover picture ----------

        // The picture file the game has (in the "Your cards" uploads), a picture chosen now (kept when the game is
        // saved), and that picture read once for the preview.
        internal string saveCoverPicture = string.Empty;
        private string saveCoverSource = string.Empty;
        private Texture2D saveCoverPreview;
        private Texture2D savedCoverPreview; // the game's own file, read once while the panel is open

        private void FillCover(GameSaveModel model, List<GameSaveRow> rows, List<ObjectDefinitionId> deckCardIds)
        {
            model.CanChoosePicture = PictureFilePicker.IsAvailable;
            Texture2D picture = saveCoverPreview;
            if (picture == null && !string.IsNullOrEmpty(saveCoverPicture))
            {
                if (savedCoverPreview == null) savedCoverPreview = host.GameSelectPanel.LoadMadeCover(saveCoverPicture);
                picture = savedCoverPreview;
            }

            string line;
            if (picture != null)
            {
                line = $"your picture · {picture.width} × {picture.height}";
            }
            else
            {
                picture = DeckFront(deckCardIds);
                line = picture != null ? "the deck's first card" : "none";
            }

            model.CoverPicture = picture;
            rows.Add(new GameSaveRow("Cover", line));
        }

        private Texture2D DeckFront(List<ObjectDefinitionId> deckCardIds)
        {
            if (deckCardIds == null || deckCardIds.Count == 0) return null;
            return host.TryGetCardLook(deckCardIds[0], out CardDefinition card) ? host.FrontArt(card) : null;
        }

        // PICTURE…: a PNG or JPG for the cover, checked and shown now, copied into the profile when the game is saved.
        internal void ChooseSaveCover(GameSaveTyped typed)
        {
            KeepTyped(typed);
            if (!PictureFilePicker.IsAvailable)
            {
                ShowGameSave("Choosing a picture works in the Unity editor and the Windows game.");
                return;
            }

            if (!PictureFilePicker.TryPick("Choose your game's cover picture", out string path))
            {
                ShowGameSave(string.Empty);
                return;
            }

            Texture2D picture = null;
            string problem = null;
            try
            {
                string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                System.IO.FileInfo file = new System.IO.FileInfo(path);
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") problem = "only PNG or JPG pictures can be used";
                else if (!file.Exists) problem = "the picture was not found";
                else if (file.Length > ContentPackStore.MaximumPictureBytes) problem = "the picture is larger than 16 MB";
                else picture = ArtLibrary.LoadTexture(System.IO.File.ReadAllBytes(path), "your-game-cover");
                if (problem == null && picture == null) problem = "the picture could not be read";
            }
            catch (Exception exception)
            {
                problem = exception.Message;
            }

            if (problem != null)
            {
                ShowGameSave($"That picture can't be the cover: {problem}.");
                return;
            }

            ForgetChosenCover();
            saveCoverPreview = picture;
            saveCoverSource = path;
            ShowGameSave($"Cover: OK · {picture.width} × {picture.height}. It is kept when you SAVE.");
        }

        // On SAVE: a picture chosen now is copied into the profile under the game's ID (games/your-cards/uploads/).
        internal bool KeepChosenCover(PlayerGameFile game, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(saveCoverSource)) return true;
            ContentPackStore store = ContentPackStore.ForLibrary(host.EnsureProfile(), true);
            if (!store.TryStoreUpload(PlayerLibrary.Id, "game-cover-" + game.id, saveCoverSource, out string fileName, out error)) return false;
            game.coverPicture = fileName;
            saveCoverPicture = fileName;
            return true;
        }

        internal void ForgetChosenCover()
        {
            if (saveCoverPreview != null) UnityEngine.Object.Destroy(saveCoverPreview);
            if (savedCoverPreview != null) UnityEngine.Object.Destroy(savedCoverPreview);
            saveCoverPreview = null;
            savedCoverPreview = null;
            saveCoverSource = string.Empty;
        }
    }
}
