using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// STAGES-1 (owner, 2026-11-02: "the floor will have only different back and front graphics … think of it like
    /// stages: stage 1 with a kitchen floor which ends on 3×3, then the next stage a living room and so on"). In Decks,
    /// a game with floors (Trap Floor) lists its stages under its own deck: STAGE 1 · Kitchen, STAGE 2 · Living room …
    /// and + ADD STAGE. A stage holds no cards of its own (every stage plays the game's cards and rules); it has a
    /// name (RENAME), the back every floor tile shows (CHANGE…), and front pictures: IMPORT FRONTS… takes a folder of
    /// PNGs named like the cards ("Fall Trap.png"); a card without one keeps its normal front.
    /// </summary>
    internal sealed partial class DeckEditorPresenter
    {
        internal const string StageRowPrefix = "stage:";
        internal const string AddStageRowId = "stage:add";

        // True when the game being edited has stages: its definition allows them (GENRE-1) and it has floor groups
        // (content sets with the floor role).
        internal bool EditorGameHasFloors()
        {
            GameDefinition game = host.CardEditorPanel.editorGame;
            if (game == null || !game.AllowsStages) return false;
            for (int s = 0; s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                if (set != null && string.Equals(set.Role, "floor", StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private int StageCount()
        {
            int stages = 0;
            List<PlayerDeckRecord> decks = host.CardEditorPanel.editorDecks.decks;
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i] != null && decks[i].stage > stages) stages = decks[i].stage;
            }

            return stages;
        }

        internal PlayerDeckRecord FindStage(int stage)
        {
            List<PlayerDeckRecord> decks = host.CardEditorPanel.editorDecks.decks;
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i] != null && decks[i].stage == stage) return decks[i];
            }

            return null;
        }

        internal static string StageTitle(PlayerDeckRecord deck) =>
            string.IsNullOrWhiteSpace(deck.name) || deck.name == $"Stage {deck.stage}"
                ? $"Stage {deck.stage}"
                : $"Stage {deck.stage} · {deck.name}";

        // STAGE 1 … STAGE n, then + ADD STAGE, for a game with floors.
        internal void AddStageRows(List<CardEditorRowModel> rows)
        {
            if (!EditorGameHasFloors()) return;
            int stages = Math.Max(1, StageCount());
            for (int stage = 1; stage <= stages; stage++)
            {
                PlayerDeckRecord deck = FindStage(stage);
                if (deck != null)
                {
                    int fronts = deck.fronts != null ? deck.fronts.Count : 0;
                    rows.Add(new CardEditorRowModel(deck.id, StageTitle(deck),
                        $"6×6 to 3×3 · {fronts} picture{(fronts == 1 ? string.Empty : "s")}{(string.IsNullOrEmpty(deck.backArtKey) ? string.Empty : " · own back")}",
                        StageThumbnail(deck), string.Equals(deck.id, deckEditorSelectedId, StringComparison.OrdinalIgnoreCase), 1));
                }
                else
                {
                    rows.Add(new CardEditorRowModel(StageRowPrefix + stage, $"Stage {stage}", "6×6 to 3×3 · the game's own pictures", null, false, 1));
                }
            }

            rows.Add(new CardEditorRowModel(AddStageRowId, "+ ADD STAGE", $"Stage {stages + 1}: the floors again with a new look", null, false, 1));
        }

        private Texture2D StageThumbnail(PlayerDeckRecord deck)
        {
            if (deck.fronts != null && deck.fronts.Count > 0) return host.CardEditorPanel.DraftPicture(deck.fronts[0].frontArtKey, null, true);
            return string.IsNullOrEmpty(deck.backArtKey) ? null : host.CardEditorPanel.DraftPicture(deck.backArtKey, null, false);
        }

        // A stage row or + ADD STAGE was chosen; false for any other row.
        internal bool TrySelectStageRow(string rowId)
        {
            if (string.IsNullOrEmpty(rowId) || !rowId.StartsWith(StageRowPrefix, StringComparison.Ordinal)) return false;
            if (rowId == AddStageRowId)
            {
                if (FindStage(1) == null) MakeStage(1); // stage 1 is the game's look until it is changed
                PlayerDeckRecord added = MakeStage(Math.Max(1, StageCount()) + 1);
                deckEditorSelectedId = added.id;
                deckEditorChanged = true;
                ShowDeckEditor($"Added Stage {added.stage}. RENAME it (for example Living room), give it a back with CHANGE… and its pictures with IMPORT FRONTS….");
                return true;
            }

            if (!int.TryParse(rowId.Substring(StageRowPrefix.Length), out int stage) || stage < 1) return false;
            PlayerDeckRecord deck = FindStage(stage) ?? MakeStage(stage);
            deckEditorSelectedId = deck.id;
            ShowDeckEditor(string.Empty);
            return true;
        }

        private PlayerDeckRecord MakeStage(int stage)
        {
            PlayerDeckRecord deck = new PlayerDeckRecord
            {
                id = PlayerCardsFile.NewCardId(),
                name = $"Stage {stage}",
                stage = stage,
            };
            host.CardEditorPanel.editorDecks.decks.Add(deck);
            return deck;
        }

        // A card of a floor group, or one of yours that works like one.
        internal bool IsFloorCard(DeckEditorCard card)
        {
            string id = card.GameCard != null ? card.GameCard.StableId : card.Record?.behavesLike;
            if (string.IsNullOrEmpty(id)) return false;
            GameDefinition game = host.CardEditorPanel.editorGame;
            for (int s = 0; game != null && s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                if (set == null || !string.Equals(set.Role, "floor", StringComparison.OrdinalIgnoreCase)) continue;
                for (int c = 0; c < set.Cards.Count; c++)
                {
                    if (set.Cards[c] != null && string.Equals(set.Cards[c].StableId, id, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }

        internal bool SelectedIsStage(out PlayerDeckRecord deck)
        {
            deck = FindEditorDeck(deckEditorSelectedId);
            return deck != null && deck.stage > 0;
        }

        // The stage's picture for a card, else null (the card's normal front shows).
        internal Texture2D StageFront(PlayerDeckRecord stage, string cardId)
        {
            for (int i = 0; stage.fronts != null && i < stage.fronts.Count; i++)
            {
                PlayerDeckFront front = stage.fronts[i];
                if (front != null && string.Equals(front.cardId, cardId, StringComparison.OrdinalIgnoreCase))
                    return host.CardEditorPanel.DraftPicture(front.frontArtKey, null, true);
            }

            return null;
        }

        internal string StageText(PlayerDeckRecord deck)
        {
            int fronts = deck.fronts != null ? deck.fronts.Count : 0;
            return $"{StageTitle(deck)}: the floors from 6×6 to 3×3 with this look; the same cards and rules. {fronts} card"
                + $"{(fronts == 1 ? " has" : "s have")} a picture of this stage. IMPORT FRONTS… takes a folder of PNGs named like the cards; CHANGE… sets the tiles' back.";
        }

        // IMPORT FRONTS…: each picture in the folder whose name matches a floor card ("Fall Trap.png", "fall-trap.png")
        // becomes that card's front in this stage (cropped to the card's shape, a copy kept in the game's files).
        internal void ImportStageFronts()
        {
            if (!SelectedIsStage(out PlayerDeckRecord stage)) return;
            if (!PictureFilePicker.TryPickFolder($"Choose a folder of pictures for {StageTitle(stage)}", out string folder))
            {
                ShowDeckEditor(string.Empty);
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception exception)
            {
                ShowDeckEditor("That folder could not be read: " + exception.Message);
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            List<DeckEditorCard> cards = AllDeckEditorCards();
            CardEditorPresenter studio = host.CardEditorPanel;
            int done = 0;
            List<string> unmatched = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") continue;
                string name = CardEditorPresenter.MatchName(Path.GetFileNameWithoutExtension(files[i]));
                int matchIndex = -1;
                for (int c = 0; c < cards.Count && matchIndex < 0; c++)
                {
                    if (IsFloorCard(cards[c]) && CardEditorPresenter.MatchName(CardName(cards[c])) == name) matchIndex = c;
                }

                if (matchIndex < 0)
                {
                    unmatched.Add(Path.GetFileName(files[i]));
                    continue;
                }

                string cardId = cards[matchIndex].Id;
                string key = PlayerContent.DefaultArtKey(studio.editorArtFolder, $"stage{stage.stage}-{cardId}", true);
                if (!studio.StoreShapedPicture(key, files[i], false, out _)) continue;
                if (stage.fronts == null) stage.fronts = new List<PlayerDeckFront>();
                stage.fronts.RemoveAll(front => front != null && string.Equals(front.cardId, cardId, StringComparison.OrdinalIgnoreCase));
                stage.fronts.Add(new PlayerDeckFront { cardId = cardId, frontArtKey = key });
                done++;
            }

            deckEditorChanged = done > 0;
            string report = $"{done} picture{(done == 1 ? string.Empty : "s")} now on {StageTitle(stage)}'s cards.";
            if (unmatched.Count > 0)
            {
                report += $" No card named like: {string.Join(", ", unmatched.GetRange(0, Math.Min(6, unmatched.Count)))}"
                    + (unmatched.Count > 6 ? $" and {unmatched.Count - 6} more." : ".");
            }

            ShowDeckEditor(report);
        }
    }
}
