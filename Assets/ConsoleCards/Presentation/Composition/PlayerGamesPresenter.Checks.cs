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
    /// more than <see cref="MaximumMadeCards"/> cards made by the player; no Console Cards part (the console mat, a
    /// mapping mat or a controller deck); dealing more cards than the deck has; a sheet asking for pieces the table lacks.
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
                return "Use at least one Console Cards part: the console mat, a mapping mat or a controller deck.";
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

        // Owner, 2026-11-21: the Console Cards parts are the console mat (a console), a mapping mat (a board) and the
        // controller decks (their cards). A game box's cards (Trap Floor's) are the game's, not a part, and do not count.
        private bool UsesConsoleCardsPart(SavedTable table, List<ObjectDefinitionId> deckCards, HashSet<string> made)
        {
            if (table.consoles.Count > 0 || table.boards.Count > 0) return true;
            HashSet<string> controller = ControllerCardIds();
            for (int i = 0; i < deckCards.Count; i++)
            {
                if (controller.Contains(deckCards[i].Value.ToString("D"))) return true;
            }

            for (int p = 0; p < table.piles.Count; p++)
            {
                SavedPile pile = table.piles[p];
                for (int c = 0; pile != null && c < pile.cards.Count; c++)
                {
                    if (!string.IsNullOrEmpty(pile.cards[c]) && controller.Contains(pile.cards[c])) return true;
                }
            }

            for (int i = 0; i < table.pieces.Count; i++)
            {
                SavedPiece piece = table.pieces[i];
                if (piece != null && piece.kind == SavedPiece.Card && controller.Contains(piece.definitionId ?? string.Empty)) return true;
            }

            return false;
        }

        // The stable ids of every controller card (a card that represents a controller input) in the built-in games.
        private HashSet<string> ControllerCardIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<GameSelectEntry> entries = host.GameSelectPanel.GameSelectEntries();
            for (int e = 0; e < entries.Count; e++)
            {
                GameDefinition game = entries[e]?.Game;
                for (int s = 0; game != null && s < game.ContentSets.Count; s++)
                {
                    GameContentSet set = game.ContentSets[s];
                    for (int c = 0; set != null && c < set.Cards.Count; c++)
                    {
                        CardDefinition card = set.Cards[c];
                        if (card != null && card.RepresentedControllerInput.HasValue && !string.IsNullOrEmpty(card.StableId)) ids.Add(card.StableId);
                    }
                }
            }

            return ids;
        }

        // RULES-SHEET (R5): a template keeps no card the player made (piles lose them; such cards on the table go).
        private void StripMadeCards(SavedTable table)
        {
            HashSet<string> made = MadeCardIds(out _);
            if (made.Count == 0) return;
            for (int p = table.piles.Count - 1; p >= 0; p--)
            {
                SavedPile pile = table.piles[p];
                if (pile == null) continue;
                pile.cards.RemoveAll(made.Contains);
                if (pile.cards.Count == 0) table.piles.RemoveAt(p);
            }

            table.pieces.RemoveAll(piece => piece != null && piece.kind == SavedPiece.Card && made.Contains(piece.definitionId ?? string.Empty));
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
            AddLibraryCardIds(ids); // doc 63: "Your cards" are made cards too (kept in the profile, also in the Editor)
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

        private void AddLibraryCardIds(HashSet<string> ids)
        {
            try
            {
                PlayerCardsFile file = ContentPackStore.ForLibrary(host.EnsureProfile(), true).LoadCards(PlayerLibrary.Id);
                for (int i = 0; i < file.cards.Count; i++)
                {
                    if (file.cards[i] != null && !string.IsNullOrEmpty(file.cards[i].id)) ids.Add(file.cards[i].id);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Your games] Could not read Your cards: " + exception.Message);
            }
        }
    }
}
