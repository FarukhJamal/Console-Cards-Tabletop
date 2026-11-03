using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Owner, 2026-11-03: "there should be a way of discarding your hand". Right-click a card in your hand: DISCARD
    /// puts it on the game's discard pile (Trap Floor: the Ability discard pile; any other table: its discard pile),
    /// DISCARD WHOLE HAND puts all of them there. One Undo step each; online it goes through the table like any move.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private readonly List<TabletopObjectId> handDiscardCards = new List<TabletopObjectId>(16);

        /// <summary>The pile cards from a hand are discarded to (false: this table has none).</summary>
        internal bool TryFindHandDiscardPile(out ContainerId discardId)
        {
            discardId = ContainerId.Empty;
            if (matchState == null) return false;
            if (trapFloorTemplate != null && matchState.Containers.ContainsKey(trapFloorTemplate.AbilityDiscardId))
            {
                discardId = trapFloorTemplate.AbilityDiscardId;
                return true;
            }

            foreach (KeyValuePair<ContainerId, ContainerState> pair in matchState.Containers)
            {
                if (pair.Value.Kind != ContainerKind.DiscardPile) continue;
                discardId = pair.Key;
                return true;
            }

            return false;
        }

        /// <summary>True when this container is the hand of the player at this screen.</summary>
        internal bool IsLocalHand(ContainerId containerId) =>
            handView != null && !containerId.IsEmpty && handView.ContainerId == containerId;

        /// <summary>Puts one card (or, with <paramref name="wholeHand"/>, every card) of the hand on the discard pile.</summary>
        internal void DiscardFromHand(ContainerId handId, TabletopObjectId cardId, bool wholeHand)
        {
            if (!TryFindHandDiscardPile(out ContainerId discardId)
                || !matchState.Containers.TryGetValue(handId, out ContainerState hand))
            {
                ShowMessage("This table has no discard pile.");
                return;
            }

            handDiscardCards.Clear();
            if (wholeHand)
            {
                for (int i = 0; i < hand.Count; i++) handDiscardCards.Add(hand.GetObjectAt(i));
            }
            else
            {
                handDiscardCards.Add(cardId);
            }

            int moved = 0;
            SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < handDiscardCards.Count; i++)
                {
                    TransferCardResult result = TableAuthority.Submit<TransferCardCommand, TransferCardResult>(
                        new TransferCardCommand(CreateCommandContext(), handDiscardCards[i], handId, discardId, null));
                    if (result.Succeeded) moved++;
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            ViewSync.SyncContainer(handId);
            ViewSync.SyncContainer(discardId);
            RefreshAcceptedCardFaces();
            ShowMessage(moved == 0 ? "Those cards could not be discarded."
                : moved == 1 ? "Discarded 1 card." : $"Discarded {moved} cards.");
        }
    }
}
