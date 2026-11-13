using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// P-2b (owner, 2026-11-13: "Free-mode floor: players can gather the floor cards, shuffle them and place them on the
    /// grid themselves; the game's default layout stays the start; in Free the floor tiles are fully interactable for
    /// every player and can be moved anywhere; Free dice only show their numbers; the players collapse tiles
    /// themselves"). In Free a floor tile is an ordinary card (not a fixed board piece): it can be picked up, stacked,
    /// selected, flipped and locked like any card, and its menu offers to free every tile at once or to gather the
    /// whole floor into one face-down deck to shuffle and lay out again (cards come to rest on the board's spaces).
    /// The table commands are the usual ones (unlock, transfer, group), so every computer follows and Undo takes each
    /// step back. Reminders and Full keep the floor fixed as before.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        /// <summary>P-2b: Free leaves the floor to the players.</summary>
        internal static bool FreeFloor => PlayAssist.IsFree;

        // The floor tile's menu in Free.
        internal void AddFreeFloorActions(List<PopupActionOption> actions, TabletopObjectId cardId)
        {
            if (!FreeFloor || host.trapFloorTemplate == null || !IsBoardTile(cardId)) return;
            int locked = CountLockedFloorTiles();
            actions.Add(new PopupActionOption(
                locked > 0 ? $"Free the floor tiles ({locked} locked)" : "Floor tiles are free to move",
                locked > 0,
                () =>
                {
                    host.ContextMenus.CloseContextMenu();
                    int freed = UnlockFloorTiles();
                    host.ShowMessage(freed > 0
                        ? $"{freed} floor tiles are free: move, stack or flip them as you like (Free)."
                        : "The floor tiles are already free.");
                }));
            actions.Add(new PopupActionOption("Gather the floor into a deck", true, () =>
            {
                host.ContextMenus.CloseContextMenu();
                GatherFloorIntoDeck();
            }));
        }

        private int CountLockedFloorTiles()
        {
            int locked = 0;
            foreach (CardInstanceState card in host.matchState.Cards.Values)
                if (card.BaseState.IsUserLocked && card.BaseState.ContainerId.IsEmpty && IsBoardTile(card.BaseState.Id)) locked++;
            return locked;
        }

        private int UnlockFloorTiles()
        {
            List<TabletopObjectId> tiles = new List<TabletopObjectId>();
            foreach (CardInstanceState card in host.matchState.Cards.Values)
                if (card.BaseState.IsUserLocked && card.BaseState.ContainerId.IsEmpty && IsBoardTile(card.BaseState.Id)) tiles.Add(card.BaseState.Id);
            if (tiles.Count == 0) return 0;
            int freed = 0;
            host.SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    CommandResult result = host.TableAuthority.Submit<SetObjectLockedCommand, CommandResult>(
                        new SetObjectLockedCommand(host.CreateCommandContext(), tiles[i], false));
                    if (result.Succeeded) freed++;
                }
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            Debug.Log($"[Floor] Free: {freed} floor tiles freed by hand.");
            return freed;
        }

        // Every floor tile still on the table, freed and grouped into one deck where the first tile lay (face down: the
        // group keeps each card's face, so turned tiles are turned back first).
        private void GatherFloorIntoDeck()
        {
            UnlockFloorTiles();
            List<CardView> tiles = new List<CardView>();
            foreach (CardInstanceState card in host.matchState.Cards.Values)
            {
                if (!card.BaseState.ContainerId.IsEmpty || !IsBoardTile(card.BaseState.Id)) continue;
                if (host.Registry.TryGetCardView(card.BaseState.Id, out CardView view) && view != null && view.IsBound) tiles.Add(view);
            }

            if (tiles.Count < 2)
            {
                host.ShowMessage("There are not enough floor tiles on the table to gather.");
                return;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].CardState == null || tiles[i].CardState.Face == CardFace.FaceDown) continue;
                host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(
                    new FlipCardCommand(host.CreateCommandContext(), tiles[i].ObjectId, CardFace.FaceDown));
            }

            host.GroupCardsIntoDeck(tiles);
            host.ShowMessage($"The floor ({tiles.Count} tiles) is one deck: shuffle it, then lay the tiles on the board's spaces.");
        }
    }
}
