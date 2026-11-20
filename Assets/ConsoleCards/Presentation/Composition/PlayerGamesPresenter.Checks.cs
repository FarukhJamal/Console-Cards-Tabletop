using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// RULES-SHEET (R3; "Console Cards — Game Rules and Templates", Save): Make a game refuses to save, with a message
    /// saying what to change, a game that breaks a client rule or whose rules sheet cannot work with its table:
    /// more than <see cref="MaximumMadeCards"/> cards made by the player; no Console Cards part (the console, a mapper
    /// or a Console Cards deck); dealing more cards than the deck has; a sheet asking for pieces the table lacks.
    /// The content check on uploaded cards (no sexual content) is the owner's open question 6 and is not done here.
    /// </summary>
    internal sealed partial class PlayerGamesPresenter
    {
        /// <summary>Client limit: the cards one player may make (upload) in the Card studio, in all games together.</summary>
        internal const int MaximumMadeCards = 400;

        // The first reason the game cannot be saved, or null.
        private string SaveCheck(SavedTable table, DeckChoice deck)
        {
            HashSet<string> made = MadeCardIds(out bool counted);
            if (counted && made.Count > MaximumMadeCards)
            {
                return $"You have made {made.Count} cards; the limit is {MaximumMadeCards}. Delete some in the Card studio.";
            }

            List<ObjectDefinitionId> deckCards = deck.Game != null ? SaveDeckCards(deck, out _, out _) : new List<ObjectDefinitionId>();
            if (!UsesConsoleCardsPart(table, deckCards, made))
            {
                return "Use at least one Console Cards part: the console, a mapper or a Console Cards deck (Toolbox).";
            }

            int players = Mathf.Max(saveMinimumPlayers, saveMaximumPlayers);
            if (saveDeal != null && !saveDeal.DealsAll && saveDeal.CountEach > 0 && deck.Game != null)
            {
                int needed = saveDeal.CountEach * players;
                if (needed > deckCards.Count)
                {
                    saveStep = GameSaveModel.DealStep;
                    return $"Dealing {saveDeal.CountEach} each to {players} players needs {needed} cards; {deck.Name} has {deckCards.Count}.";
                }
            }

            string pawns = RulesSheet.ValueOf(saveSheet, "sheet.pawns");
            if (int.TryParse(pawns, out int pawnsEach) && pawnsEach > 0)
            {
                int onTable = 0;
                for (int i = 0; i < table.pieces.Count; i++)
                {
                    if (table.pieces[i] != null && table.pieces[i].kind == SavedPiece.Pawn) onTable++;
                }

                if (onTable < pawnsEach * players)
                {
                    saveStep = GameSaveModel.RulesStep;
                    return $"The sheet gives {pawnsEach} pawn(s) each: {pawnsEach * players} for {players} players; the table has {onTable}.";
                }
            }

            if (RulesSheet.ValueOf(saveSheet, "sheet.mapper.grid").Length > 0 && table.boards.Count == 0)
            {
                saveStep = GameSaveModel.RulesStep;
                return "The rules sheet uses a mapper, but there is none on the table.";
            }

            if (string.Equals(RulesSheet.ValueOf(saveSheet, "sheet.win"), "the first to reach the target", StringComparison.Ordinal)
                && RulesSheet.ValueOf(saveSheet, "sheet.win.target").Length == 0)
            {
                saveStep = GameSaveModel.RulesStep;
                return "The winner is the first to reach the target, but the sheet's Target is empty.";
            }

            return null;
        }

        // A console or a mapper on the table, or any card that is not one the player made (a Console Cards deck's card).
        private static bool UsesConsoleCardsPart(SavedTable table, List<ObjectDefinitionId> deckCards, HashSet<string> made)
        {
            if (table.consoles.Count > 0 || table.boards.Count > 0) return true;
            for (int i = 0; i < deckCards.Count; i++)
            {
                if (!made.Contains(deckCards[i].Value.ToString("D"))) return true;
            }

            for (int p = 0; p < table.piles.Count; p++)
            {
                SavedPile pile = table.piles[p];
                for (int c = 0; pile != null && c < pile.cards.Count; c++)
                {
                    if (!string.IsNullOrEmpty(pile.cards[c]) && !made.Contains(pile.cards[c])) return true;
                }
            }

            for (int i = 0; i < table.pieces.Count; i++)
            {
                SavedPiece piece = table.pieces[i];
                if (piece != null && piece.kind == SavedPiece.Card && !string.IsNullOrEmpty(piece.definitionId) && !made.Contains(piece.definitionId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The ids of every card the player made, in all games. <paramref name="counted"/> is false in the Editor, where
        /// made cards are the project's own (game data) and the client limit does not apply to them.
        /// </summary>
        internal HashSet<string> MadeCardIds(out bool counted)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            counted = false;
            ContentPackStore store;
            try
            {
                store = ContentPackStore.ForOwnEdits(host.EnsureProfile());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Your games] Could not read your cards: " + exception.Message);
                return ids;
            }

            counted = !store.IsGameData;
            if (!Directory.Exists(store.RootFolder)) return ids;
            foreach (string folder in Directory.GetDirectories(store.RootFolder))
            {
                string path = Path.Combine(folder, PlayerCardsFile.FileName);
                if (!File.Exists(path)) continue;
                PlayerCardsFile file = SaveFiles.ReadJson<PlayerCardsFile>(path, "Your cards");
                for (int i = 0; file?.cards != null && i < file.cards.Count; i++)
                {
                    if (file.cards[i] != null && !string.IsNullOrEmpty(file.cards[i].id)) ids.Add(file.cards[i].id);
                }
            }

            return ids;
        }
    }
}
