using System;
using System.Collections.Generic;
using System.Globalization;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// CREATE (owner, 2026-10-30: "in CREATE, make a game is not there"; design board 4 "CREATE hub"): MAKE A GAME lays
    /// out an empty table and opens the save steps (Deck · Cards · Deal · Save); YOUR GAMES lists the games this profile
    /// made, with PLAY, EDIT (the game's table with the save steps open) and Copy / Rename / Delete (asks twice).
    /// </summary>
    internal sealed partial class GameSelectPresenter
    {
        private bool pendingOpenSave;
        private string myGameDeleteArmed = string.Empty;

        internal void FillMyGames(GameSelectModel model)
        {
            List<PlayerGameFile> mine = host.PlayerGamesPanel.PlayerGames().List();
            mine.Sort((a, b) => string.CompareOrdinal(b.savedUtc ?? string.Empty, a.savedUtc ?? string.Empty));
            List<FrontEndMyGame> rows = new List<FrontEndMyGame>(mine.Count);
            for (int i = 0; i < mine.Count; i++)
            {
                PlayerGameFile game = mine[i];
                string players = game.minimumPlayers == game.maximumPlayers
                    ? $"{game.minimumPlayers} player{(game.minimumPlayers == 1 ? string.Empty : "s")}"
                    : $"{game.minimumPlayers}–{game.maximumPlayers} players";
                string changed = ChangedText(game.savedUtc);
                rows.Add(new FrontEndMyGame(
                    game.id,
                    string.IsNullOrWhiteSpace(game.name) ? "My game" : game.name,
                    changed.Length > 0 ? $"{players} · changed {changed}" : players,
                    game.id == myGameDeleteArmed));
            }

            model.MyGames = rows;
        }

        // "today", "yesterday", "3 days ago", or the date.
        private static string ChangedText(string savedUtc)
        {
            if (string.IsNullOrEmpty(savedUtc)
                || !DateTime.TryParse(savedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime saved))
            {
                return string.Empty;
            }

            int days = (int)(DateTime.Now.Date - saved.ToLocalTime().Date).TotalDays;
            if (days <= 0) return "today";
            if (days == 1) return "yesterday";
            if (days < 14) return $"{days} days ago";
            return saved.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        // MAKE A GAME: an empty table, then the save steps.
        private void MakeGameFromFrontEnd()
        {
            myGameDeleteArmed = string.Empty;
            pendingOpenSave = true;
            StartSandbox();
            if (host.runtimeUi != null && host.runtimeUi.IsGameSelectOpen) pendingOpenSave = false; // it did not start
        }

        private void PlayMyGame(string id)
        {
            myGameDeleteArmed = string.Empty;
            if (string.IsNullOrEmpty(id)) return;
            gameSelectEntryId = PlayerGameEntryPrefix + id;
            StartSelectedGame();
        }

        // EDIT: the game's table, laid out, with the save steps open (SAVE OVER keeps the same game).
        private void EditMyGame(string id)
        {
            pendingOpenSave = true;
            PlayMyGame(id);
            if (host.runtimeUi != null && host.runtimeUi.IsGameSelectOpen) pendingOpenSave = false;
        }

        private void CopyMyGame(string id)
        {
            myGameDeleteArmed = string.Empty;
            PlayerGameStore store = host.PlayerGamesPanel.PlayerGames();
            PlayerGameFile game = store.Load(id);
            if (game == null)
            {
                ShowGameSelect("That game could not be found.");
                return;
            }

            game.id = PlayerGameFile.NewId();
            string name = (string.IsNullOrWhiteSpace(game.name) ? "My game" : game.name) + " copy";
            game.name = name.Length > PlayerGameFile.MaximumNameLength ? name.Substring(0, PlayerGameFile.MaximumNameLength) : name;
            game.createdUtc = PlayerGameFile.Now();
            game.savedUtc = game.createdUtc;
            ShowGameSelect(store.Save(game, out string error) ? $"Copied as {game.name}." : $"Could not copy: {error}.");
        }

        private void RenameMyGame(string id, string newName)
        {
            myGameDeleteArmed = string.Empty;
            string name = (newName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                ShowGameSelect("A game needs a name.");
                return;
            }

            PlayerGameStore store = host.PlayerGamesPanel.PlayerGames();
            PlayerGameFile game = store.Load(id);
            if (game == null)
            {
                ShowGameSelect("That game could not be found.");
                return;
            }

            game.name = name.Length > PlayerGameFile.MaximumNameLength ? name.Substring(0, PlayerGameFile.MaximumNameLength) : name;
            game.savedUtc = PlayerGameFile.Now();
            ShowGameSelect(store.Save(game, out string error) ? $"Renamed to {game.name}." : $"Could not rename: {error}.");
        }

        // Asks twice: the first press arms the row ("DELETE?"), the second deletes.
        private void DeleteMyGame(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (myGameDeleteArmed != id)
            {
                myGameDeleteArmed = id;
                ShowGameSelect("Press DELETE again to delete it; this cannot be undone.");
                return;
            }

            myGameDeleteArmed = string.Empty;
            PlayerGameStore store = host.PlayerGamesPanel.PlayerGames();
            PlayerGameFile game = store.Load(id);
            string name = game != null && !string.IsNullOrWhiteSpace(game.name) ? game.name : "The game";
            if (!store.Delete(id, out string error))
            {
                ShowGameSelect($"{name} could not be deleted: {error}.");
                return;
            }

            if (host.PlayerGamesPanel.activePlayerGame != null && host.PlayerGamesPanel.activePlayerGame.id == id)
            {
                host.PlayerGamesPanel.activePlayerGame = null;
            }

            if (gameSelectEntryId == PlayerGameEntryPrefix + id) gameSelectEntryId = EmptyTableEntryId;
            ShowGameSelect($"Deleted {name}.");
        }

        /// <summary>After a table is made: MAKE A GAME or EDIT opens the save steps on it.</summary>
        internal void AfterTableReplaced()
        {
            if (!pendingOpenSave) return;
            pendingOpenSave = false;
            host.PlayerGamesPanel.OpenGameSave();
        }
    }
}
