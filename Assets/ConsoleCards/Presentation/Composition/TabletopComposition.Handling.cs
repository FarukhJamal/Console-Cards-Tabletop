using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 58, step 1: handling that Tabletop Simulator players expect.
    /// <list type="bullet">
    /// <item><b>Search</b> (a deck's or stack's menu): every card of the pile in a row; take any of them into your
    /// hand. The pile is shuffled afterwards (taking or not), as in TTS.</item>
    /// <item><b>Peek</b> (Alt + Shift over a face-down card or pile): you see the face; the table is told.</item>
    /// <item><b>Two digits draw</b>: over a deck, 1 then 2 within half a second draws 12.</item>
    /// </list>
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float DrawDigitWindow = 0.5f;
        private int pendingDrawCount;
        private ContainerId pendingDrawPile;
        private float pendingDrawAt = -1f;

        // ---------- Search ----------

        internal void AddSearchAction(List<PopupActionOption> actions, ContainerId pileId)
        {
            if (matchState == null || !matchState.Containers.TryGetValue(pileId, out ContainerState pile) || pile.Count == 0) return;
            if (handView == null || !handView.IsBound) return;
            // P8A (owner, 2026-11-08): in Full, Abilities are bought (Draw 3 Abilities), so the Ability deck has no Search.
            if (ActiveCartridge is TrapFloorCartridge && trapFloorTemplate != null && pileId == trapFloorTemplate.AbilityDeckId
                && ConsoleCards.Presentation.UI.PlayAssist.Level == ConsoleCards.Presentation.UI.PlayAssistLevel.Full) return;
            actions.Add(new PopupActionOption("Search", true, () =>
            {
                ContextMenus.CloseContextMenu();
                OpenSearch(pileId);
            }));
        }

        private void OpenSearch(ContainerId pileId)
        {
            if (runtimeUi == null || matchState == null || !matchState.Containers.TryGetValue(pileId, out ContainerState pile)) return;
            List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(pile.Count);
            for (int i = pile.Count - 1; i >= 0; i--) // the top card first
            {
                TabletopObjectId cardId = pile.GetObjectAt(i);
                if (!matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) continue;
                bool named = TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out CardDefinition definition);
                string title = named && !string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.DisplayName : "Card";
                options.Add(new FocusedCardOptionModel(
                    cardId,
                    title,
                    $"{pile.Count - i} from the top",
                    named ? FrontArt(definition) : null,
                    true));
            }

            if (options.Count == 0) return;
            runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                $"SEARCH · {PileName(pile.Kind, pileId).ToUpperInvariant()}",
                "Pick any cards to take into your hand. The pile is shuffled when you close this.",
                $"{options.Count} cards",
                "Take to hand",
                options,
                options.Count,
                ids => true,
                ids => FinishSearch(pileId, ids),
                () => FinishSearch(pileId, null),
                "Close"));
            runtimeUi.ShowTableNews("You are searching the pile", FeedbackColour(localPlayerId));
        }

        private void FinishSearch(ContainerId pileId, IReadOnlyList<TabletopObjectId> taken)
        {
            runtimeUi?.CloseFocusedCardSelection();
            if (matchState == null || !matchState.Containers.ContainsKey(pileId)) return;
            int moved = 0;
            SessionService.BeginUndoBatch();
            try
            {
                if (taken != null && transferCoordinator != null && handView != null && handView.IsBound)
                {
                    for (int i = 0; i < taken.Count; i++)
                    {
                        if (Registry.TryGetCardView(taken[i], out CardView card)
                            && transferCoordinator.Transfer(card, CardDropTarget.ForContainer(handView.ContainerId)).Succeeded)
                        {
                            moved++;
                        }
                    }
                }

                if (matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                    && pile.Count >= 2
                    && (pile.Kind == ContainerKind.Deck || pile.Kind == ContainerKind.Stack))
                {
                    ShuffleDeck(pileId);
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            ShowMessage(moved == 0 ? "Searched the pile and shuffled it." : $"Took {moved} card{(moved == 1 ? string.Empty : "s")} and shuffled the pile.");
        }

        // ---------- Peek ----------

        // Alt + Shift: the face of a face-down card (never a card in another player's hand).
        private bool PeekHeld()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.shiftKey.isPressed;
        }

        private bool MayPeekAt(TabletopObjectId cardId)
        {
            if (!matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) return false;
            ContainerId container = card.BaseState.ContainerId;
            if (container.IsEmpty) return true;
            return !matchState.Containers.TryGetValue(container, out ContainerState pile)
                || pile.Kind != ContainerKind.Hand
                || (handView != null && handView.ContainerId == container);
        }

        // ---------- Two-digit draws ----------

        // A digit over a deck: waits half a second for a second digit (1 then 2 draws 12).
        private void QueueDrawDigit(int digit, ContainerId pileId)
        {
            bool combine = pendingDrawAt >= 0f && pendingDrawPile == pileId && pendingDrawCount < 10;
            pendingDrawCount = combine ? (pendingDrawCount * 10) + digit : digit;
            pendingDrawPile = pileId;
            pendingDrawAt = Time.unscaledTime + DrawDigitWindow;
        }

        private void CompletePendingDraw()
        {
            if (pendingDrawAt < 0f || Time.unscaledTime < pendingDrawAt) return;
            pendingDrawAt = -1f;
            if (pendingDrawCount > 0 && matchState != null && matchState.Containers.ContainsKey(pendingDrawPile))
            {
                DrawCards(pendingDrawPile, pendingDrawCount);
            }

            pendingDrawCount = 0;
        }
    }
}
