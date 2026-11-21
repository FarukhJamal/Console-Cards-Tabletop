using System.Collections.Generic;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 63 G2 (owner, 2026-11-21: "a game for 8 players should be set up on a proper table"): a made game may keep
    /// its own table and layout for some player counts. SAVE with LAYOUT FOR set to a count stores the table as that
    /// count's layout and keeps the default; ALL PLAYER COUNTS saves the default as before; CLEAR removes the chosen
    /// count's layout from the saved game at once. Starting the game with that many players uses it.
    /// </summary>
    internal sealed partial class PlayerGamesPresenter
    {
        // The count SAVE writes a layout for (0: the default, for every count).
        internal int saveLayoutPlayers;

        // The count whose own layout was laid out on this table (0: the default), so SAVE OVER edits that layout.
        internal int activeLayoutPlayers;

        // The chosen count, when it is still in the game's player range (else 0: the default).
        internal int EffectiveLayoutPlayers()
        {
            int fewest = Mathf.Clamp(saveMinimumPlayers, 1, PlayerGameFile.MaximumPlayers);
            int most = Mathf.Clamp(saveMaximumPlayers, fewest, PlayerGameFile.MaximumPlayers);
            return saveLayoutPlayers >= fewest && saveLayoutPlayers <= most ? saveLayoutPlayers : 0;
        }

        // LAYOUT FOR ‹ ›: all player counts, then each count from the fewest to the most.
        internal void CycleSaveLayout(int step, GameSaveTyped typed)
        {
            KeepTyped(typed);
            int fewest = Mathf.Clamp(saveMinimumPlayers, 1, PlayerGameFile.MaximumPlayers);
            int most = Mathf.Clamp(saveMaximumPlayers, fewest, PlayerGameFile.MaximumPlayers);
            int span = most - fewest + 2;
            int players = EffectiveLayoutPlayers();
            int index = players == 0 ? 0 : players - fewest + 1;
            index = (((index + step) % span) + span) % span;
            saveLayoutPlayers = index == 0 ? 0 : fewest + index - 1;
            ShowGameSave(string.Empty);
        }

        // CLEAR: the chosen count's layout is taken out of the saved game now; that count uses the default again.
        internal void ClearSaveLayout(GameSaveTyped typed)
        {
            KeepTyped(typed);
            int players = EffectiveLayoutPlayers();
            PlayerGameFile game = activePlayerGame;
            if (players == 0 || game == null || game.LayoutFor(players) == null)
            {
                ShowGameSave("Choose a player count that has its own layout to clear it.");
                return;
            }

            PlayerGameStore store = PlayerGames();
            PlayerGameFile saved = store.Load(game.id);
            if (saved == null)
            {
                ShowGameSave("This game is not saved in your profile, so there is no layout to clear.");
                return;
            }

            RemoveLayout(saved.layouts, players);
            if (!store.Save(saved, out string error))
            {
                ShowGameSave($"The layout could not be cleared: {error}.");
                return;
            }

            RemoveLayout(game.layouts, players);
            if (activeLayoutPlayers == players) activeLayoutPlayers = 0;
            saveLayoutPlayers = 0; // back to ALL, so the Layouts row shows what is left (not a preview of saving this count again)
            ShowGameSave($"Cleared the layout for {CountText(players, "player")}: it now starts on the default layout.");
        }

        private static void RemoveLayout(List<SavedCountLayout> layouts, int players)
        {
            for (int i = (layouts != null ? layouts.Count : 0) - 1; i >= 0; i--)
            {
                if (layouts[i] == null || layouts[i].players == players) layouts.RemoveAt(i);
            }
        }

        // The LAYOUT FOR line and CLEAR, and the WHAT GETS SAVED row ("default + 6, 8 players").
        internal void FillLayoutRow(GameSaveModel model, List<GameSaveRow> rows)
        {
            int players = EffectiveLayoutPlayers();
            model.LayoutText = players > 0 ? CountText(players, "player") : "All player counts";
            model.CanClearLayout = players > 0 && activePlayerGame != null && !activeIsDraft && activePlayerGame.LayoutFor(players) != null; // doc 63 T1
            List<int> counts = new List<int>();
            List<SavedCountLayout> layouts = activePlayerGame != null ? activePlayerGame.layouts : null;
            for (int i = 0; layouts != null && i < layouts.Count; i++)
            {
                if (layouts[i] != null && layouts[i].table != null && !counts.Contains(layouts[i].players)) counts.Add(layouts[i].players);
            }

            if (players > 0 && !counts.Contains(players)) counts.Add(players);
            counts.Sort();
            rows.Add(new GameSaveRow("Layouts", counts.Count == 0 ? "one, for every count" : $"default + {string.Join(", ", counts)} players"));
        }

        // The SAVE notice: 7 or 8 players fit best on the hexagon table (the table is not changed here).
        internal string TableHint()
        {
            int players = EffectiveLayoutPlayers();
            if (players == 0) players = saveMaximumPlayers;
            TableSpec spec = host.CurrentTableSpec;
            if (players < 7 || (spec != null && spec.shape == TableShape.Hexagon)) return null;
            return $"{players} players fit best on the hexagon table: CREATE › TABLE.";
        }

        // On SAVE: the game's layouts (the ones in its player range; template true strips the cards you made from each;
        // doc 63 T1 (owner, 2026-11-21): SAVE passes false, so a template keeps them).
        // With a count chosen, the table becomes that count's layout and the default stays as the game had it.
        internal bool ApplySaveLayout(PlayerGameFile game, bool template, out string problem)
        {
            problem = null;
            PlayerGameFile from = activePlayerGame;
            int players = EffectiveLayoutPlayers();
            if (players > 0 && from == null)
            {
                problem = "Save the game for ALL PLAYER COUNTS first; then add a layout for another count.";
                return false;
            }

            List<SavedCountLayout> layouts = new List<SavedCountLayout>();
            for (int i = 0; from != null && from.layouts != null && i < from.layouts.Count; i++)
            {
                SavedCountLayout layout = from.layouts[i];
                if (layout == null || layout.table == null || layout.players == players
                    || layout.players < game.minimumPlayers || layout.players > game.maximumPlayers)
                {
                    continue;
                }

                layouts.Add(new SavedCountLayout
                {
                    players = layout.players,
                    hasTable = layout.hasTable,
                    tableSpec = layout.tableSpec != null ? layout.tableSpec.Copy() : new TableSpec(),
                    table = CopyTable(layout.table, template),
                    hasDeckPose = layout.hasDeckPose && game.deck.IsSet,
                    deckPose = CopyPose(layout.deckPose),
                });
            }

            if (players > 0)
            {
                layouts.Add(new SavedCountLayout
                {
                    players = players,
                    hasTable = game.hasTable,
                    tableSpec = game.tableSpec != null ? game.tableSpec.Copy() : new TableSpec(),
                    table = game.table,
                    hasDeckPose = game.deck.IsSet,
                    deckPose = CopyPose(game.deck.pose),
                });
                game.table = CopyTable(from.table, template);
                game.hasTable = from.hasTable;
                game.tableSpec = from.tableSpec != null ? from.tableSpec.Copy() : new TableSpec();
                if (game.deck.IsSet && from.deck != null && from.deck.IsSet) game.deck.pose = CopyPose(from.deck.pose);
            }

            layouts.Sort((a, b) => a.players.CompareTo(b.players));
            game.layouts = layouts;
            return true;
        }

        private SavedTable CopyTable(SavedTable table, bool template)
        {
            SavedTable copy = table != null ? JsonUtility.FromJson<SavedTable>(JsonUtility.ToJson(table)) : null;
            if (copy == null) copy = new SavedTable();
            copy.Normalised();
            if (template) StripMadeCards(copy);
            return copy;
        }

        private static SavedPose CopyPose(SavedPose pose) => pose == null
            ? new SavedPose()
            : new SavedPose { x = pose.x, y = pose.y, rotation = pose.rotation, layer = pose.layer, order = pose.order };
    }
}
