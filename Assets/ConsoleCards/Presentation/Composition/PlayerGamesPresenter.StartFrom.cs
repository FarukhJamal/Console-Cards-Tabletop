using System.Collections.Generic;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 63 T1 (owner, 2026-11-21): START FROM. A made game or template is copied whole (table, table spec, layouts,
    /// deck, deal, players, rules sheet, minutes, cover; the player's cards are in the shared library, so its references
    /// stay valid). A built-in game's freshly dealt table is captured with its own pieces and seats' Consoles.
    /// The copy is a new game, not saved yet (<see cref="activeIsDraft"/>); templateId names its source.
    /// </summary>
    internal sealed partial class PlayerGamesPresenter
    {
        // The game on the table is a START FROM copy that is not saved yet: SAVE OVER is off, SAVE AS NEW makes it.
        internal bool activeIsDraft;

        // "<name> copy", at most the longest name a game may have.
        internal static string CopyName(string name)
        {
            string copy = (string.IsNullOrWhiteSpace(name) ? "My game" : name.Trim()) + " copy";
            return copy.Length > PlayerGameFile.MaximumNameLength ? copy.Substring(0, PlayerGameFile.MaximumNameLength) : copy;
        }

        // A new game with everything the source has (the source itself is not changed).
        internal static PlayerGameFile DraftFrom(PlayerGameFile source)
        {
            PlayerGameFile draft = JsonUtility.FromJson<PlayerGameFile>(JsonUtility.ToJson(source));
            draft.Normalised();
            draft.id = PlayerGameFile.NewId();
            draft.name = CopyName(source.name);
            draft.isTemplate = false;
            draft.templateId = source.id ?? string.Empty;
            draft.createdUtc = string.Empty;
            draft.savedUtc = string.Empty;
            return draft;
        }

        // A new game from the built-in game on the table now: its pieces where they lie, its players and table.
        internal PlayerGameFile DraftFromLiveTable(string gameName, string gameId, int fewest, int most)
        {
            int minimum = Mathf.Clamp(fewest, 1, PlayerGameFile.MaximumPlayers);
            int maximum = Mathf.Clamp(most, minimum, PlayerGameFile.MaximumPlayers);
            TableSpec spec = host.CurrentTableSpec;
            PlayerGameFile draft = new PlayerGameFile
            {
                id = PlayerGameFile.NewId(),
                name = CopyName(gameName),
                description = $"Started from {gameName}.",
                minimumPlayers = minimum,
                maximumPlayers = maximum,
                rulesText = "# How to play\n",
                table = CaptureTable(true),
                templateId = gameId ?? string.Empty,
                hasTable = spec != null,
                tableSpec = spec != null ? spec.Copy() : new TableSpec(),
            };
            return draft.Normalised();
        }

        // Each seat's Console mat as a Console on the table (CaptureTable with the game's pieces).
        private void AddSeatConsoles(List<SavedConsole> consoles)
        {
            foreach (SeatState seat in host.matchState.Seats.Values)
            {
                if (seat == null) continue;
                consoles.Add(new SavedConsole { showsBack = seat.Console != null && seat.Console.ShowsBack, pose = SavePose(seat.ConsolePose) });
            }
        }
    }
}
