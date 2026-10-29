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
            bool hasDeck = deck.Game != null;
            int deckCards = hasDeck ? SaveDeckCards(deck, out _, out _).Count : 0;
            model.StepsDone = new[]
            {
                hasDeck ? deckOnTable : table.Count > 0, // Deck: the game's deck is on the table (or it needs none)
                hasDeck && deckCards > 0, // Cards: its deck has cards
                saveDeal != null, // Deal: it deals
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
            model.SavedRows = rows;
        }
    }
}
