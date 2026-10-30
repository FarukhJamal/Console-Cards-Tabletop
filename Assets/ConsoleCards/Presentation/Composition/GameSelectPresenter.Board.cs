using System;
using System.Collections.Generic;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.GameTemplates.ControllerInputs;
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
    /// Boards 1–3 of the menu flow (owner, 2026-10-30: "everything the same as the reference"): what Home, the Library
    /// and the Game sheet show besides the usual choices. Home's card: the game table behind the front end (CONTINUE)
    /// or the game chosen last (PLAY AGAIN). The Library: a tile per game and per game you made (the Empty Table is
    /// SANDBOX). The Game sheet: a line about the game and its first rules (from the game's rules).
    /// </summary>
    internal sealed partial class GameSelectPresenter
    {
        private readonly Dictionary<string, string> taglines = new Dictionary<string, string>(StringComparer.Ordinal);

        private void FillBoardScreens(GameSelectModel model, List<GameSelectEntry> entries, GameSelectEntry chosen)
        {
            if (model.Features != null && model.Features.Count > 0 && chosen.Game != null)
            {
                string[] lines = new string[model.Features.Count];
                for (int i = 0; i < lines.Length; i++) lines[i] = model.Features[i].Line;
                taglines[chosen.Id] = string.Join(" ", lines);
            }

            List<FrontEndLibraryTile> tiles = new List<FrontEndLibraryTile>(entries.Count + ComingSoon.Length);
            bool soonAdded = false;
            for (int i = 0; i < entries.Count; i++)
            {
                // Board 2: our games first, then the ones coming soon, then the games you made.
                if (!soonAdded && entries[i].PlayerGame != null)
                {
                    AddComingSoon(tiles);
                    soonAdded = true;
                }

                GameSelectEntry entry = entries[i];
                if (entry.Game == null && entry.PlayerGame == null) continue; // the Empty Table is SANDBOX
                bool mine = entry.PlayerGame != null;
                string players = PlayersOf(entry);
                string line = mine
                    ? $"Made in CREATE · {players}."
                    : taglines.TryGetValue(entry.Id, out string tagline) ? tagline : GameDetail(entry);
                tiles.Add(new FrontEndLibraryTile(
                    entry.Id,
                    entry.Name,
                    line,
                    mine ? "YOUR GAME" : players.ToUpperInvariant(),
                    mine,
                    mine ? null : GamePicture(entry.Game),
                    entry.Id == chosen.Id));
            }

            if (!soonAdded) AddComingSoon(tiles);
            model.LibraryTiles = tiles;
            model.Tagline = chosen.PlayerGame != null
                ? string.IsNullOrWhiteSpace(chosen.PlayerGame.description) ? "A game you made in CREATE." : chosen.PlayerGame.description
                : taglines.TryGetValue(chosen.Id, out string chosenLine) ? chosenLine : GameDetail(chosen);
            model.HowToPlay = HowToPlayLines(chosen);
            FillContinue(model, entries, chosen);
            gameSelectActions.PlayAgain = StartWithChosenColours;
        }

        // Owner, 2026-10-30 (board 2): the games that are not out yet, dimmed with COMING SOON.
        private static readonly string[] ComingSoon =
        {
            "Tower Clash", "Build higher. Block opponents. Be the last builder.",
            "Color Control", "Flip tiles. Control zones. Outplay your friends.",
        };

        private static void AddComingSoon(List<FrontEndLibraryTile> tiles)
        {
            for (int i = 0; i + 1 < ComingSoon.Length; i += 2)
            {
                tiles.Add(new FrontEndLibraryTile("soon:" + ComingSoon[i], ComingSoon[i], ComingSoon[i + 1], "COMING SOON",
                    false, null, false, true));
            }
        }

        private string PlayersOf(GameSelectEntry entry)
        {
            string detail = GameDetail(entry);
            if (entry.PlayerGame != null)
            {
                int cut = detail.IndexOf(" · ", StringComparison.Ordinal);
                return cut >= 0 ? detail.Substring(cut + 3) : detail;
            }

            int dot = detail.IndexOf(" · ", StringComparison.Ordinal);
            return dot >= 0 ? detail.Substring(0, dot) : detail;
        }

        // The first four rules of the game's own rules (a game you made: its first written lines).
        private static IReadOnlyList<string> HowToPlayLines(GameSelectEntry entry)
        {
            List<string> lines = new List<string>(4);
            if (entry.PlayerGame != null)
            {
                string[] written = (entry.PlayerGame.rulesText ?? string.Empty).Split('\n');
                for (int i = 0; i < written.Length && lines.Count < 4; i++)
                {
                    string line = written[i].Trim();
                    if (line.Length > 0 && line[0] != '#') lines.Add(FitText.Concise(line, 54));
                }

                return lines;
            }

            RulesCardModel rules = entry.Game != null ? RulesCardModel.FromRuleSet(entry.Game.DefaultRuleSet, null) : null;
            if (rules == null) return lines;
            for (int i = 0; i < rules.Lines.Count && lines.Count < 4; i++)
            {
                if (rules.Lines[i].Kind == RulesCardLineKind.Rule) lines.Add(FitText.Concise(rules.Lines[i].Text, 54));
            }

            return lines;
        }

        // Home's card: the game table behind the front end, or the game chosen last.
        private void FillContinue(GameSelectModel model, List<GameSelectEntry> entries, GameSelectEntry chosen)
        {
            bool inProgress = host.activeSession != null
                && host.activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom
                && host.activeSession.Template != null;
            model.ContinueInProgress = inProgress;
            if (inProgress)
            {
                string displayName = host.activeSession.Template.DisplayName ?? string.Empty;
                int separator = displayName.IndexOf(" — ", StringComparison.Ordinal);
                string name = separator < 0 ? displayName : displayName.Substring(0, separator);
                string mode = separator < 0 ? string.Empty : displayName.Substring(separator + 3);
                int seats = host.ActiveCartridge != null ? host.ActiveCartridge.Seats.Count : 0;
                model.ContinueShown = true;
                model.ContinueName = name.ToUpperInvariant();
                model.ContinueLine = seats > 0
                    ? (mode.Length > 0 ? $"{seats} players · {mode}" : $"{seats} players")
                    : mode;
                model.ContinueSubLine = "The table as you left it";
                GameSelectEntry playing = null;
                for (int i = 0; i < entries.Count && playing == null; i++)
                {
                    if (entries[i].Templates.Contains(host.activeSession.Selection.GameTemplateId)) playing = entries[i];
                }

                model.ContinuePicture = playing != null ? GamePicture(playing.Game) : null;
                return;
            }

            bool game = chosen.Game != null || chosen.PlayerGame != null;
            model.ContinueShown = game;
            if (!game) return;
            model.ContinueName = (chosen.Name ?? string.Empty).ToUpperInvariant();
            model.ContinueLine = model.ShowMode && model.ModeName.Length > 0
                ? $"{model.PlayersText} · {model.ModeName}"
                : model.PlayersText;
            model.ContinueSubLine = string.Empty;
            model.ContinuePicture = chosen.Game != null ? GamePicture(chosen.Game) : null;
        }
    }
}
