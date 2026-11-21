using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 63 T1 (owner, 2026-11-21: "use a template for creating any other game"): CREATE › MAKE A GAME › START FROM.
    /// &lt; &gt; choose an empty table, a built-in game (Trap Floor), one of the player's games or a template; MAKE A GAME
    /// then opens a NEW, unsaved game prefilled from it (SAVE OVER is off, SAVE AS NEW makes a separate game, the source
    /// is never changed). A built-in game is dealt as it normally starts, its table is captured, and the table is made
    /// again as an empty table with that layout. Host or local only (a room refuses, as SANDBOX does).
    /// </summary>
    internal sealed partial class GameSelectPresenter
    {
        private const string BuiltInSourcePrefix = "game:";

        // The chosen source: empty (an empty table), "game:" + a built-in game's entry id, or "my:" + a made game's id.
        private string makeGameFromId = string.Empty;
        private string makeGameFromName = string.Empty;

        // A START FROM copy is waiting to be laid out once the table is replaced (OnTableReplaced).
        private bool pendingDraft;

        // The words between START FROM's < >; a made game deleted meanwhile falls back to the empty table.
        private string MakeGameFromText(List<PlayerGameFile> mine)
        {
            if (makeGameFromId.StartsWith(PlayerGameEntryPrefix, StringComparison.Ordinal))
            {
                string id = makeGameFromId.Substring(PlayerGameEntryPrefix.Length);
                for (int i = 0; i < mine.Count; i++)
                {
                    if (string.Equals(mine[i].id, id, StringComparison.Ordinal)) return SourceName(mine[i]);
                }

                makeGameFromId = string.Empty;
            }

            return makeGameFromId.Length > 0 ? makeGameFromName : "Empty table";
        }

        private static string SourceName(PlayerGameFile game)
        {
            string name = string.IsNullOrWhiteSpace(game.name) ? "My game" : game.name;
            return game.isTemplate ? "Template: " + name : name;
        }

        // Empty table, each built-in game that can be played, the player's own games, then the templates.
        private void CycleMakeGameFrom(int step)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            List<string> ids = new List<string> { string.Empty };
            List<string> names = new List<string> { "Empty table" };
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Game == null || entries[i].Templates.Count == 0) continue;
                ids.Add(BuiltInSourcePrefix + entries[i].Id);
                names.Add(entries[i].Name);
            }

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    PlayerGameFile game = entries[i].PlayerGame;
                    if (game == null || game.isTemplate != (pass == 1)) continue;
                    ids.Add(entries[i].Id); // "my:" + the game's id
                    names.Add(SourceName(game));
                }
            }

            int index = Mathf.Max(0, ids.IndexOf(makeGameFromId));
            index = ((index + step) % ids.Count + ids.Count) % ids.Count;
            makeGameFromId = ids[index];
            makeGameFromName = names[index];
            ShowGameSelect(index == 0
                ? "MAKE A GAME starts on an empty table."
                : $"MAKE A GAME starts a new game from {names[index]}; {names[index]} is not changed.", entries);
        }

        // MAKE A GAME with a source chosen: true when it was handled here (started, or refused with a message).
        private bool StartFromChosenSource()
        {
            if (makeGameFromId.Length == 0) return false;
            if (host.HasOnline && host.Online.InRoom) return false; // SANDBOX's solo notice: Make a game is host or local only

            if (makeGameFromId.StartsWith(BuiltInSourcePrefix, StringComparison.Ordinal))
            {
                return StartFromBuiltInGame(makeGameFromId.Substring(BuiltInSourcePrefix.Length));
            }

            string id = makeGameFromId.StartsWith(PlayerGameEntryPrefix, StringComparison.Ordinal)
                ? makeGameFromId.Substring(PlayerGameEntryPrefix.Length)
                : makeGameFromId;
            PlayerGameFile source = host.PlayerGamesPanel.PlayerGames().Load(id);
            if (source == null)
            {
                makeGameFromId = string.Empty;
                ShowGameSelect("That game could not be found.");
                return true;
            }

            return StartDraft(PlayerGamesPresenter.DraftFrom(source));
        }

        // The built-in game dealt as it normally starts (its most players), captured, then laid out on an empty table.
        private bool StartFromBuiltInGame(string entryId)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, entryId);
            if (entry == null || entry.Game == null || entry.Templates.Count == 0)
            {
                makeGameFromId = string.Empty;
                ShowGameSelect("That game cannot be laid out here.", entries);
                return true;
            }

            if (!PlayerRange(entry, out int fewest, out int most))
            {
                fewest = entry.Game.MinimumPlayers;
                most = entry.Game.MaximumPlayers;
            }

            GameTemplateId templateId = entry.Templates[0];
            string name = entry.Name;
            string gameId = entry.Game.StableId ?? string.Empty;
            pendingDeckCards = null;
            pendingPlayerGame = null;
            pendingDraft = false;
            pendingOpenSave = false; // the game's own table opens nothing
            returnToGameSelect = false;
            host.runtimeUi?.CloseGameSelect();
            host.gameIntroWanted = false; // no camera intro: the table is made again at once
            if (!host.SessionService.TryReplaceTable(TabletopSessionSelection.FromGameTemplate(templateId), most))
            {
                pendingFrontEndPage = FrontEndPage.Create;
                ShowGameSelect($"{name} could not be laid out.");
                return true;
            }

            return StartDraft(host.PlayerGamesPanel.DraftFromLiveTable(name, gameId, fewest, most));
        }

        // An empty table with the copy laid out (no deal), then the save steps on it.
        private bool StartDraft(PlayerGameFile draft)
        {
            pendingDeckCards = null;
            pendingPlayerGame = draft;
            pendingPlayerGameCount = 0;
            pendingDraft = true;
            pendingOpenSave = true;
            returnToGameSelect = false;
            host.runtimeUi?.CloseGameSelect();
            if (host.SessionService.TryReplaceTable(TabletopSessionSelection.EmptyCustom, 0)) return true;
            pendingPlayerGame = null;
            pendingDraft = false;
            pendingOpenSave = false;
            pendingFrontEndPage = FrontEndPage.Create;
            ShowGameSelect("The new game's table could not be made.");
            return true;
        }
    }
}
