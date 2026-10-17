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
using ConsoleCards.Games.TrapFloor;
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
    /// <summary>Right-click menus for cards, piles, dice, pawns, tokens and Consoles. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        private void HandleSecondaryPointerPressed(Vector2 screenPosition)
        {
            if (!IsInitialized || (interactionRouter != null && interactionRouter.HasActiveInteraction))
            {
                return;
            }

            CloseContextMenu();
            if (hitResolver.TryResolve(screenPosition, out TabletopObjectView resolvedObjectView))
            {
                if (resolvedObjectView is DieView hitDie
                    && hitDie.IsBound
                    && hitDie.DieState != null)
                {
                    selectionState.Select(hitDie);
                    selectionPresenter.Refresh();
                    OpenContextMenu(
                        ContextMenuMode.Die,
                        screenPosition,
                        TabletopObjectId.Empty,
                        ContainerId.Empty,
                        hitDie.ObjectId);
                    return;
                }

                if (resolvedObjectView is CardView hitCard
                    && TryOpenCardContextMenu(hitCard, screenPosition))
                {
                    return;
                }

                if (resolvedObjectView is PawnView hitPawn && hitPawn.IsBound)
                {
                    selectionState.Select(hitPawn);
                    selectionPresenter.Refresh();
                    OpenPawnContextMenu(screenPosition, hitPawn.ObjectId);
                    return;
                }

                if (resolvedObjectView is TokenView hitToken && hitToken.IsBound)
                {
                    selectionState.Select(hitToken);
                    selectionPresenter.Refresh();
                    OpenTokenContextMenu(screenPosition, hitToken.ObjectId);
                    return;
                }
            }

            if (!dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container
                || !matchState.Containers.TryGetValue(target.ContainerId, out ContainerState container))
            {
                // C3d: the mat itself, between its slots, opens the Console menu too.
                if (TryFindConsoleUnderPointer(screenPosition, out RuntimeConsoleInstance pointedConsole))
                {
                    OpenConsoleContextMenu(screenPosition, pointedConsole.SlotViews[0].ContainerId);
                }
                else if (TryFindBoardUnderPointer(screenPosition, out BoardId pointedBoard, out _))
                {
                    OpenBoardContextMenu(screenPosition, pointedBoard); // C3e
                }

                return;
            }

            if (container.Kind == ContainerKind.Deck
                && TryGetDeckPresentation(container.Id, out _, out _))
            {
                OpenContextMenu(
                    ContextMenuMode.Deck,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.Stack
                && stackViewsByContainerId.ContainsKey(container.Id))
            {
                OpenContextMenu(
                    ContextMenuMode.Stack,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.DiscardPile
                && TryGetRuntimeDiscardPile(container.Id, out _))
            {
                OpenContextMenu(
                    ContextMenuMode.DiscardPile,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.ConsoleSlot
                && TryResolveConsolePlacement(container.Id, out _, out _, out _))
            {
                OpenConsoleContextMenu(screenPosition, container.Id);
            }
        }

        private bool CanBeginCameraOrbit(Vector2 screenPosition)
        {
            if (!IsInitialized
                || hitResolver == null
                || (interactionRouter != null && interactionRouter.HasActiveInteraction))
            {
                return false;
            }

            if (hitResolver.TryResolve(screenPosition, out _))
            {
                return false;
            }

            bool canOrbit = dropTargetResolver == null
                || !dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container;
            if (canOrbit)
            {
                CloseContextMenu();
            }

            return canOrbit;
        }

        private bool TryOpenCardContextMenu(CardView hitCard, Vector2 screenPosition)
        {
            if (hitCard == null || !hitCard.IsBound || hitCard.CardState == null)
            {
                return false;
            }

            if (floormasterLifecycleState?.PendingCard != null
                && floormasterLifecycleState.PendingCard.CardId == hitCard.ObjectId)
            {
                selectionState.Select(hitCard);
                selectionPresenter.Refresh();
                OpenContextMenu(
                    ContextMenuMode.PendingFloormasterCard,
                    screenPosition,
                    hitCard.ObjectId,
                    ContainerId.Empty,
                    TabletopObjectId.Empty);
                return true;
            }

            if (trapFloorTemplate != null && trapFloorTemplate.IsFloorCard(hitCard.ObjectId))
            {
                OpenContextMenu(
                    ContextMenuMode.FloorCard,
                    screenPosition,
                    hitCard.ObjectId,
                    ContainerId.Empty,
                    TabletopObjectId.Empty);
                return true;
            }

            ContainerId containerId = hitCard.CardState.BaseState.ContainerId;
            if (containerId.IsEmpty)
            {
                selectionState.Select(hitCard);
                selectionPresenter.Refresh();
                OpenContextMenu(
                    ContextMenuMode.TabletopCard,
                    screenPosition,
                    hitCard.ObjectId,
                    ContainerId.Empty,
                    TabletopObjectId.Empty);
                return true;
            }

            if (!matchState.Containers.TryGetValue(containerId, out ContainerState container))
            {
                return false;
            }

            if (container.Kind == ContainerKind.Deck
                && TryGetDeckPresentation(container.Id, out _, out _))
            {
                OpenContextMenu(
                    ContextMenuMode.Deck,
                    screenPosition,
                    hitCard.ObjectId,
                    containerId,
                    TabletopObjectId.Empty);
                return true;
            }

            if (container.Kind == ContainerKind.DiscardPile
                && TryGetRuntimeDiscardPile(containerId, out _))
            {
                OpenContextMenu(
                    ContextMenuMode.DiscardPile,
                    screenPosition,
                    hitCard.ObjectId,
                    containerId,
                    TabletopObjectId.Empty);
                return true;
            }

            if (container.Kind == ContainerKind.Stack
                && stackViewsByContainerId.ContainsKey(containerId))
            {
                selectionState.Select(hitCard);
                selectionPresenter.Refresh();
                OpenContextMenu(
                    ContextMenuMode.StackCard,
                    screenPosition,
                    hitCard.ObjectId,
                    containerId,
                    TabletopObjectId.Empty);
                return true;
            }

            selectionState.Select(hitCard);
            selectionPresenter.Refresh();
            OpenContextMenu(
                ContextMenuMode.ContainedCard,
                screenPosition,
                hitCard.ObjectId,
                containerId,
                TabletopObjectId.Empty);
            return true;
        }

        private void OpenContextMenu(
            ContextMenuMode mode,
            Vector2 screenPosition,
            TabletopObjectId cardId,
            ContainerId containerId,
            TabletopObjectId dieId)
        {
            contextMenuAnchorScreenPosition = screenPosition;
            contextMenuCardId = cardId;
            contextMenuContainerId = containerId;
            contextMenuDieId = dieId;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            if (mode == ContextMenuMode.Deck)
            {
                selectedDrawCount = Mathf.Clamp(
                    selectedDrawCount,
                    1,
                    Math.Max(1, AvailableDrawableCount(containerId)));
            }

            SetContextMenuMode(mode);
        }

        private void OpenPawnContextMenu(Vector2 screenPosition, TabletopObjectId pawnId)
        {
            contextMenuAnchorScreenPosition = screenPosition;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuContainerId = ContainerId.Empty;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = pawnId;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            SetContextMenuMode(ContextMenuMode.Pawn);
        }

        private void OpenTokenContextMenu(Vector2 screenPosition, TabletopObjectId tokenId)
        {
            contextMenuAnchorScreenPosition = screenPosition;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuContainerId = ContainerId.Empty;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = tokenId;
            contextMenuConsoleId = ConsoleId.Empty;
            SetContextMenuMode(ContextMenuMode.Token);
        }

        private void OpenConsoleContextMenu(Vector2 screenPosition, ContainerId slotContainerId)
        {
            contextMenuAnchorScreenPosition = screenPosition;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuContainerId = slotContainerId;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuConsoleId = TryGetPlacedConsoleBySlot(slotContainerId, out ConsoleId consoleId)
                ? consoleId
                : ConsoleId.Empty;
            SetContextMenuMode(ContextMenuMode.Console);
        }

        private void SetContextMenuMode(ContextMenuMode mode)
        {
            contextMenuMode = mode;
            if (mode == ContextMenuMode.None)
            {
                runtimeUi?.CloseTabletopPopup();
                return;
            }

            RenderOpenTabletopPopup();
        }

        private void CloseContextMenu()
        {
            contextMenuMode = ContextMenuMode.None;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuContainerId = ContainerId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            contextMenuRenderedRevision = -1;
            runtimeUi?.CloseTabletopPopup();
        }

        private void DismissPopupFromSecondary(Vector2 _)
        {
            CloseContextMenu();
        }

        private void RefreshOpenTabletopPopup()
        {
            if (contextMenuMode == ContextMenuMode.None || !IsInitialized)
            {
                return;
            }

            if (!IsContextMenuTargetAvailable())
            {
                CloseContextMenu();
                return;
            }

            // Physics checkpoints advance the Match even while a uGUI Button owns a press.
            // Keep the Die menu and its Button instance alive until click/acceptance; Roll validates
            // current state through the physical authority. Other menus retain their existing policy.
            if (contextMenuMode != ContextMenuMode.Die
                && matchState != null && contextMenuRenderedRevision != matchState.Revision)
            {
                CloseContextMenu();
            }
        }

        private void RenderOpenTabletopPopup()
        {
            if (runtimeUi == null || !IsContextMenuTargetAvailable())
            {
                CloseContextMenu();
                return;
            }

            switch (contextMenuMode)
            {
                case ContextMenuMode.Deck:
                    ShowDeckContextMenu();
                    break;
                case ContextMenuMode.DrawCards:
                    ShowControllerDrawPopup();
                    break;
                case ContextMenuMode.CustomDrawCards:
                    ShowCustomControllerDrawPopup();
                    break;
                case ContextMenuMode.PopulateDeck:
                    ShowPopulateDeckQuantityPopup();
                    break;
                case ContextMenuMode.TabletopCard:
                    ShowTabletopCardContextMenu();
                    break;
                case ContextMenuMode.FloorCard:
                    ShowFloorCardContextMenu();
                    break;
                case ContextMenuMode.PendingFloormasterCard:
                    ShowPendingFloormasterCardContextMenu();
                    break;
                case ContextMenuMode.StackCard:
                    ShowStackCardContextMenu();
                    break;
                case ContextMenuMode.ContainedCard:
                    ShowContainedCardContextMenu();
                    break;
                case ContextMenuMode.Stack:
                    ShowStackContextMenu();
                    break;
                case ContextMenuMode.MergeDestination:
                    ShowMergeDestinationPopup();
                    break;
                case ContextMenuMode.Die:
                    ShowDieContextMenu();
                    break;
                case ContextMenuMode.Pawn:
                    ShowPawnContextMenu();
                    break;
                case ContextMenuMode.Token:
                    ShowTokenContextMenu();
                    break;
                case ContextMenuMode.Console:
                    ShowConsoleContextMenu();
                    break;
                case ContextMenuMode.DiscardPile:
                    ShowDiscardPileContextMenu();
                    break;
                default:
                    CloseContextMenu();
                    return;
            }

            contextMenuRenderedRevision = matchState.Revision;
        }

        private void ShowDeckContextMenu()
        {
            ContainerId targetDeckId = contextMenuContainerId;
            ContainerState targetDeck = matchState.GetContainer(targetDeckId);
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (!contextMenuCardId.IsEmpty)
            {
                AddInspectAction(actions, contextMenuCardId);
            }

            AddControllerDrawAction(actions, targetDeckId);
            if (HasLocalHand() && !IsAssistedControllerDeck(targetDeckId) && targetDeck.Count > 0)
            {
                actions.Add(new PopupActionOption(
                    "Draw to Hand",
                    true,
                    () => OpenDrawToHandPopup(targetDeckId)));
            }

            if (targetDeck.Count == 0)
            {
                actions.Add(new PopupActionOption(
                    "Populate Deck",
                    true,
                    () =>
                    {
                        selectedQuantity = 1;
                        SetContextMenuMode(ContextMenuMode.PopulateDeck);
                    }));
            }

            actions.Add(new PopupActionOption(
                "Shuffle",
                true,
                () =>
                {
                    ShuffleDeckResult result = ShuffleDeck(targetDeckId);
                    if (result.Succeeded)
                    {
                        CloseContextMenu();
                    }
                }));
            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => BeginContainerMove(targetDeckId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(targetDeckId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DECK",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowDiscardPileContextMenu()
        {
            ContainerId targetPileId = contextMenuContainerId;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (!contextMenuCardId.IsEmpty)
            {
                AddInspectAction(actions, contextMenuCardId);
            }

            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => BeginContainerMove(targetPileId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(targetPileId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DISCARD PILE",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void AddDeleteActionIfRuntime(
            List<PopupActionOption> actions,
            TabletopComponentTarget target)
        {
            bool isTemplateComponent;
            switch (target.Kind)
            {
                case TabletopComponentTargetKind.Object:
                    isTemplateComponent = matchState.IsTemplateObject(target.ObjectId);
                    break;
                case TabletopComponentTargetKind.Container:
                    isTemplateComponent = matchState.IsTemplateContainer(target.ContainerId);
                    break;
                case TabletopComponentTargetKind.Console:
                    isTemplateComponent = false;
                    break;
                default:
                    return;
            }

            if (isTemplateComponent)
            {
                return;
            }

            actions.Add(new PopupActionOption(
                "Delete",
                true,
                () => DeleteComponent(target)));
        }

        private string DuplicateActionLabel(
            TabletopObjectId sourceObjectId,
            TabletopComponentKind componentKind)
        {
            return matchState.IsTemplateObject(sourceObjectId)
                ? $"Duplicate as Generic {componentKind}"
                : "Duplicate";
        }

        private void DeleteComponent(TabletopComponentTarget target)
        {
            DeleteTabletopComponentResult result = componentDeletionUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new DeleteTabletopComponentRequest(CreateCommandContext(), target));
            if (!result.Succeeded)
            {
                string detail = result.Error == DeleteTabletopComponentError.ContainerNotEmpty
                    ? "Container must be empty."
                    : result.Error == DeleteTabletopComponentError.ConsoleNotEmpty
                        ? "Every Console Slot must be empty."
                        : result.Error.ToString();
                ShowMessage($"Delete rejected: {detail}");
                return;
            }

            CloseContextMenu();
            ProjectDeletedComponent(result);
            ShowMessage($"Deleted {result.ComponentKind}.");
        }

        private void ShowControllerDrawPopup()
        {
            ContainerId targetDeckId = contextMenuContainerId;
            int availableCount = AvailableControllerDeckCount(targetDeckId);
            ControllerConfigurationData configuration =
                trapFloorTemplate.GameDefinition.ControllerConfiguration;
            List<PopupActionOption> actions = new List<PopupActionOption>
            {
                new PopupActionOption(
                    "Draw 1",
                    availableCount > 0,
                    () => DrawControllerCardsFromContext(targetDeckId, 1)),
                new PopupActionOption(
                    "Draw 5",
                    availableCount > 0,
                    () => DrawControllerCardsFromContext(targetDeckId, 5)),
                new PopupActionOption(
                    $"Draw To Hand Limit ({handComfortSettings.MaxHandCards})",
                    availableCount > 0,
                    () => DrawUpToConfiguredHandLimitFromContext(targetDeckId)),
                new PopupActionOption(
                    "Custom",
                    availableCount > 0,
                    () =>
                    {
                        selectedDrawCount = Mathf.Clamp(
                            selectedDrawCount,
                            1,
                            Math.Max(1, AvailableControllerDeckCount(targetDeckId)));
                        SetContextMenuMode(ContextMenuMode.CustomDrawCards);
                    }),
                new PopupActionOption(
                    handComfortSettings.CapLabel + "  -",
                    handComfortSettings.MaxHandCards > PlayerHandComfortSettings.MinimumMaxHandCards,
                    () => ChangeHandComfortCapFromDrawPopup(-1)),
                new PopupActionOption(
                    handComfortSettings.CapLabel + "  +",
                    true,
                    () => ChangeHandComfortCapFromDrawPopup(1)),
            };

            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DRAW",
                (availableCount == 1
                    ? "1 Card remaining in Controller Deck."
                    : $"{availableCount} Cards remaining in Controller Deck.")
                    + (configuration != null
                        ? $"\nGame hand limit: {configuration.MaximumHandSize} (advisory)."
                        : string.Empty),
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ChangeHandComfortCapFromDrawPopup(int delta)
        {
            handComfortSettings.MaxHandCards += delta;
            SetContextMenuMode(ContextMenuMode.DrawCards);
        }

        private void ShowCustomControllerDrawPopup()
        {
            int availableCount = AvailableControllerDeckCount(contextMenuContainerId);
            selectedDrawCount = availableCount > 0
                ? Mathf.Clamp(selectedDrawCount, 1, availableCount)
                : 0;
            runtimeUi.ShowDrawCountPopup(
                contextMenuAnchorScreenPosition,
                selectedDrawCount,
                availableCount,
                () => ChangeSelectedControllerDrawCount(-1),
                () => ChangeSelectedControllerDrawCount(1),
                ConfirmSelectedControllerDrawCount,
                CloseContextMenu,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ChangeSelectedControllerDrawCount(int delta)
        {
            int availableCount = AvailableControllerDeckCount(contextMenuContainerId);
            if (availableCount <= 0)
            {
                selectedDrawCount = 0;
                runtimeUi?.SetDrawCountPopupValue(0, 0);
                return;
            }

            selectedDrawCount = Mathf.Clamp(selectedDrawCount + delta, 1, availableCount);
            runtimeUi?.SetDrawCountPopupValue(selectedDrawCount, availableCount);
        }

        private void ConfirmSelectedControllerDrawCount()
        {
            int availableCount = AvailableControllerDeckCount(contextMenuContainerId);
            if (availableCount <= 0)
            {
                runtimeUi?.SetDrawCountPopupValue(0, 0);
                return;
            }

            DrawControllerCardsFromContext(
                contextMenuContainerId,
                Mathf.Clamp(selectedDrawCount, 1, availableCount));
        }

        private void ShowPopulateDeckQuantityPopup()
        {
            ContainerState deck = matchState.GetContainer(contextMenuContainerId);
            int maximum = deck.Capacity > 0
                ? Math.Min(PopulateDeckUseCase.MaximumQuantity, deck.Capacity)
                : PopulateDeckUseCase.MaximumQuantity;
            selectedQuantity = Mathf.Clamp(selectedQuantity, 1, maximum);
            runtimeUi.ShowQuantityPopup(
                "POPULATE DECK",
                "Create generic blank Cards directly in this empty Deck.",
                "Populate",
                selectedQuantity,
                1,
                maximum,
                () => ChangeSelectedQuantity(-1, maximum),
                () => ChangeSelectedQuantity(1, maximum),
                ConfirmPopulateDeck,
                CloseContextMenu);
        }

        private void ConfirmPopulateDeck()
        {
            ContainerId targetDeckId = contextMenuContainerId;
            if (!matchState.Containers.TryGetValue(targetDeckId, out ContainerState deck))
            {
                ShowMessage("Populate Deck rejected: DeckMissing.");
                CloseContextMenu();
                return;
            }

            int maximum = deck.Capacity > 0
                ? Math.Min(PopulateDeckUseCase.MaximumQuantity, deck.Capacity)
                : PopulateDeckUseCase.MaximumQuantity;
            int quantity = Mathf.Clamp(selectedQuantity, 1, maximum);
            PopulateDeckResult result = populateDeckUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new PopulateDeckRequest(CreateCommandContext(), targetDeckId, quantity));
            if (!result.Succeeded)
            {
                ShowMessage($"Populate Deck rejected: {result.Error}.");
                CloseContextMenu();
                return;
            }

            ProjectPopulatedDeckCards(targetDeckId, result.CardIds);
            ShowMessage($"Populated Deck with {quantity} generic Cards.");
            CloseContextMenu();
        }

        private void ShowTabletopCardContextMenu()
        {
            TabletopObjectId targetCardId = contextMenuCardId;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            AddInspectAction(actions, targetCardId);
            actions.Add(new PopupActionOption(
                    "Flip",
                    true,
                    () => FlipContextCard(targetCardId)));
            actions.Add(new PopupActionOption(
                DuplicateActionLabel(targetCardId, TabletopComponentKind.Card),
                true,
                () => BeginDuplicatePlacement(targetCardId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetCardId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "CARD",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void FlipContextCard(TabletopObjectId targetCardId)
        {
            if (!TryGetCardView(targetCardId, out CardView targetCardView))
            {
                CloseContextMenu();
                return;
            }

            FlipInteractionResult result = flipCoordinator.Flip(targetCardView);
            ShowMessage(result.Succeeded ? "Card flipped." : $"Flip rejected: {result.Status}.");
            if (result.Succeeded)
            {
                CloseContextMenu();
            }
        }

        private void ShowStackCardContextMenu()
        {
            TabletopObjectId targetCardId = contextMenuCardId;
            ContainerId targetStackId = contextMenuContainerId;
            ContainerState stack = matchState.GetContainer(targetStackId);
            int index = stack.IndexOf(targetCardId);
            List<PopupActionOption> actions = new List<PopupActionOption>();
            AddInspectAction(actions, targetCardId);
            if (index < stack.Count - 1)
            {
                actions.Add(new PopupActionOption(
                    "Move Up",
                    true,
                    () => ReorderContextStackCard(targetCardId, targetStackId, 1)));
            }

            if (index > 0)
            {
                actions.Add(new PopupActionOption(
                    "Move Down",
                    true,
                    () => ReorderContextStackCard(targetCardId, targetStackId, -1)));
            }

            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => BeginContainerMove(targetStackId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetCardId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "CARD",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ReorderContextStackCard(
            TabletopObjectId targetCardId,
            ContainerId targetStackId,
            int offset)
        {
            if (!TryGetCardView(targetCardId, out CardView targetCardView)
                || targetCardView.CardState.BaseState.ContainerId != targetStackId)
            {
                CloseContextMenu();
                return;
            }

            ReorderContainerResult result = MoveCardInContainer(targetCardView, targetStackId, offset);
            if (result.Succeeded)
            {
                CloseContextMenu();
            }
        }

        private void ShowContainedCardContextMenu()
        {
            TabletopObjectId targetCardId = contextMenuCardId;
            ContainerState container = matchState.GetContainer(contextMenuContainerId);
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (!IsHiddenHandContainer(container))
            {
                AddInspectAction(actions, targetCardId);
            }

            if (container.Kind == ContainerKind.ConsoleSlot
                && TryResolveConsolePlacement(container.Id, out _, out _, out _))
            {
                ContainerId targetSlotContainerId = container.Id;
                actions.Add(new PopupActionOption(
                    "Move",
                    true,
                    () => BeginConsoleMove(targetSlotContainerId)));
            }

            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "CARD",
                $"In {FormatContainerKind(container.Kind)}",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private static string FormatContainerKind(ContainerKind kind)
        {
            return kind == ContainerKind.ConsoleSlot
                ? "Console Slot"
                : kind == ContainerKind.DiscardPile ? "Discard Pile" : kind.ToString();
        }

        private void AddInspectAction(
            List<PopupActionOption> actions,
            TabletopObjectId targetCardId)
        {
            if (IsInAnotherPlayersHand(targetCardId)) return; // other hands stay private (H1b)
            actions.Add(new PopupActionOption(
                "Inspect",
                true,
                () => OpenCardInspect(targetCardId)));
        }

        private bool IsInAnotherPlayersHand(TabletopObjectId cardId)
        {
            return matchState != null
                && matchState.Cards.TryGetValue(cardId, out CardInstanceState card)
                && !card.BaseState.ContainerId.IsEmpty
                && matchState.Containers.TryGetValue(card.BaseState.ContainerId, out ContainerState container)
                && IsHiddenHandContainer(container);
        }

        private void ShowStackContextMenu()
        {
            TryGetContextStack(out ContainerState stack, out StackRuntimeView stackView);
            ContainerId sourceStackId = stack.Id;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (stack.Count >= 2)
            {
                actions.Add(new PopupActionOption(
                    "Split Stack",
                    true,
                    () =>
                    {
                        SplitStackResult result = SplitStack(stack, stackView);
                        if (result.Succeeded)
                        {
                            CloseContextMenu();
                        }
                    }));
            }

            if (HasValidMergeDestination(sourceStackId))
            {
                actions.Add(new PopupActionOption(
                    "Merge Into...",
                    true,
                    () => SetContextMenuMode(ContextMenuMode.MergeDestination)));
            }

            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => BeginContainerMove(sourceStackId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(sourceStackId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "STACK",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowDieContextMenu()
        {
            TabletopObjectId targetDieId = contextMenuDieId;
            TryGetDieView(targetDieId, out DieView targetDieView);
            List<PopupActionOption> actions = new List<PopupActionOption>
            {
                new PopupActionOption(
                    "Roll",
                    true,
                    () => RollContextDie(targetDieId)),
                new PopupActionOption(
                    DuplicateActionLabel(targetDieId, TabletopComponentKind.Die),
                    true,
                    () => BeginDuplicatePlacement(targetDieId)),
            };
            AddDieNumberActions(actions, targetDieView);
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetDieId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DIE",
                $"d{targetDieView.DieState.SideCount}: {targetDieView.DieState.CurrentValue}",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowPawnContextMenu()
        {
            TabletopObjectId targetPawnId = contextMenuPawnId;
            List<PopupActionOption> actions = new List<PopupActionOption>
            {
                new PopupActionOption(
                    DuplicateActionLabel(targetPawnId, TabletopComponentKind.Pawn),
                    true,
                    () => BeginDuplicatePlacement(targetPawnId)),
            };
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetPawnId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "PAWN",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowTokenContextMenu()
        {
            TabletopObjectId targetTokenId = contextMenuTokenId;
            List<PopupActionOption> actions = new List<PopupActionOption>
            {
                new PopupActionOption(
                    DuplicateActionLabel(targetTokenId, TabletopComponentKind.Token),
                    true,
                    () => BeginDuplicatePlacement(targetTokenId)),
            };
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetTokenId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "TOKEN",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowConsoleContextMenu()
        {
            ConsoleId targetConsoleId = contextMenuConsoleId;
            ContainerId targetSlotContainerId = contextMenuContainerId;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => BeginConsoleMove(targetSlotContainerId)));
            if (ConsoleMatHasBackSide())
            {
                actions.Add(new PopupActionOption(
                    "Flip (F)",
                    true,
                    () => FlipConsoleFromSlot(targetSlotContainerId)));
                actions.Add(new PopupActionOption(
                    "Duplicate (other side up)",
                    true,
                    () => BeginConsoleDuplicate(targetSlotContainerId)));
            }
            if (!targetConsoleId.IsEmpty)
            {
                AddDeleteActionIfRuntime(
                    actions,
                    TabletopComponentTarget.ForConsole(targetConsoleId));
            }

            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "CONSOLE",
                targetConsoleId.IsEmpty
                    ? string.Empty
                    : "Delete is available only while every Console Slot is empty.",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void AddControllerDrawAction(
            List<PopupActionOption> actions,
            ContainerId targetDeckId)
        {
            if (!IsAssistedControllerDeck(targetDeckId))
            {
                return;
            }

            actions.Add(new PopupActionOption(
                "Draw...",
                true,
                () => SetContextMenuMode(ContextMenuMode.DrawCards)));
        }

        private void DrawControllerCardsFromContext(ContainerId targetDeckId, int requestedCount)
        {
            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (requestedCount <= 0
                || !IsAssistedControllerDeck(targetDeckId)
                || contextMenuContainerId != targetDeckId)
            {
                CloseContextMenu();
                return;
            }

            int drawCount = Math.Min(requestedCount, AvailableControllerDeckCount(targetDeckId));
            int deckLimitedCount = drawCount;
            drawCount = ClampToHandComfortCap(player.HandContainerId, drawCount);
            if (drawCount <= 0 && deckLimitedCount > 0)
            {
                ShowMessage(handComfortSettings.ReachedMessage + ".");
                CloseContextMenu();
                return;
            }

            if (drawCount <= 0)
            {
                ShowMessage("Controller Deck is empty.");
                CloseContextMenu();
                return;
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(targetDeckId, player.HandContainerId);
            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(
                    CreateCommandContext(trapFloorTurnState.ActivePlayerId),
                    targetDeckId,
                    player.HandContainerId,
                    drawCount));
            ApplyLayout(targetDeckId);
            ApplyLayout(player.HandContainerId);
            presentationTransitions.AnimateCardsFromCurrentResults(
                transitionStarts,
                result.Succeeded ? handReflowDuration : returnDuration,
                result.Succeeded ? 0.035f : 0f);
            if (result.Succeeded)
            {
                ShowMessage(drawCount < deckLimitedCount
                    ? $"{handComfortSettings.ReachedMessage}: drew {drawCount} of {deckLimitedCount}."
                    : $"Drew {drawCount} Controller Card{(drawCount == 1 ? string.Empty : "s")}{ToHandSuffix(player.HandContainerId)}.");
                CloseContextMenu();
            }
            else
            {
                ShowMessage($"Controller draw rejected: {result.Error}.");
            }
        }

        private void DrawUpToConfiguredHandLimitFromContext(ContainerId targetDeckId)
        {
            if (!IsAssistedControllerDeck(targetDeckId)
                || contextMenuContainerId != targetDeckId)
            {
                CloseContextMenu();
                return;
            }

            ControllerInputHandDrawResult result = DrawUpToConfiguredHandLimit();
            if (result.Succeeded)
            {
                CloseContextMenu();
            }
        }

        private ControllerInputHandDrawResult DrawControllerCardsUpToComfortCap(
            TrapFloorPlayerSetupDefinition player,
            ContainerState hand)
        {
            int drawCount = Math.Min(
                handComfortSettings.RemainingFor(hand),
                AvailableControllerDeckCount(player.ControllerDeckId));
            if (drawCount <= 0)
            {
                ShowMessage(handComfortSettings.RemainingFor(hand) <= 0
                    ? handComfortSettings.ReachedMessage + "."
                    : "Controller Deck is empty.");
                return ControllerInputHandDrawResult.NoChange(matchState.Revision);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(player.ControllerDeckId, player.HandContainerId);
            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(
                    CreateCommandContext(trapFloorTurnState.ActivePlayerId),
                    player.ControllerDeckId,
                    player.HandContainerId,
                    drawCount));
            ApplyLayout(player.ControllerDeckId);
            ApplyLayout(player.HandContainerId);
            presentationTransitions.AnimateCardsFromCurrentResults(
                transitionStarts,
                result.Succeeded ? handReflowDuration : returnDuration,
                result.Succeeded ? 0.035f : 0f);
            if (!result.Succeeded)
            {
                ShowMessage($"Controller draw rejected: {result.Error}.");
                return ControllerInputHandDrawResult.NoChange(matchState.Revision);
            }

            ShowMessage($"Drew {drawCount} Controller Card{(drawCount == 1 ? string.Empty : "s")}{ToHandSuffix(player.HandContainerId)} (hand cap {handComfortSettings.MaxHandCards}).");
            return ControllerInputHandDrawResult.Accepted(result.Revision, drawCount);
        }

        // Local comfort cap: limits only this player's own draws and drops into their own hand.
        private int ClampToHandComfortCap(ContainerId handId, int requestedCount)
        {
            if (!matchState.Containers.TryGetValue(handId, out ContainerState hand))
            {
                return requestedCount;
            }

            return Math.Min(requestedCount, handComfortSettings.RemainingFor(hand));
        }

        private bool IsOwnHandAtComfortCap(ContainerId containerId)
        {
            return handView != null
                && handView.IsBound
                && containerId == handView.ContainerId
                && matchState.Containers.TryGetValue(containerId, out ContainerState hand)
                && handComfortSettings.RemainingFor(hand) <= 0;
        }

        private void ShowHandCapReached()
        {
            ShowMessage(handComfortSettings.ReachedMessage + ".");
        }

        private void RollContextDie(TabletopObjectId targetDieId)
        {
            // Ignore callbacks retained from a closed/replaced menu; one accepted click launches once.
            if (contextMenuMode != ContextMenuMode.Die || contextMenuDieId != targetDieId) return;
            if (!TryGetDieView(targetDieId, out DieView targetDieView))
            {
                CloseContextMenu();
                return;
            }

            if (targetDieView.PhysicalObject == null || !targetDieView.PhysicalObject.Roll())
            {
                ShowMessage("Die Roll rejected: object is unavailable or controlled.");
                return;
            }
            ShowMessage("Rolling physically; result is accepted when the Die settles.");
            CloseContextMenu();
        }

        private void ShowMergeDestinationPopup()
        {
            TryGetContextStack(out ContainerState source, out _);
            ContainerId sourceStackId = source.Id;
            List<PopupActionOption> destinations = new List<PopupActionOption>();
            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in stackViewsByContainerId)
            {
                if (!IsValidMergeDestination(sourceStackId, pair.Key, pair.Value))
                {
                    continue;
                }

                ContainerId destinationStackId = pair.Key;
                string label = pair.Value.Visual != null && pair.Value.Visual.Label != null
                    ? pair.Value.Visual.Label.text
                    : "Stack";
                destinations.Add(new PopupActionOption(
                    label,
                    true,
                    () =>
                    {
                        MergeStacksResult result = MergeStacks(sourceStackId, destinationStackId);
                        if (result.Succeeded)
                        {
                            CloseContextMenu();
                        }
                    }));
            }

            runtimeUi.ShowMergeDestinationPopup(
                contextMenuAnchorScreenPosition,
                destinations,
                () => SetContextMenuMode(ContextMenuMode.Stack),
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private bool IsCurrentContextCardInContainer(ContainerId containerId)
        {
            return !contextMenuCardId.IsEmpty
                && matchState.Cards.TryGetValue(contextMenuCardId, out CardInstanceState card)
                && card.BaseState.ContainerId == containerId;
        }

        private bool TryGetContextStack(out ContainerState stack, out StackRuntimeView stackView)
        {
            stack = null;
            stackView = null;
            return !contextMenuContainerId.IsEmpty
                && matchState.Containers.TryGetValue(contextMenuContainerId, out stack)
                && stack.Kind == ContainerKind.Stack
                && stackViewsByContainerId.TryGetValue(contextMenuContainerId, out stackView)
                && stackView.View != null
                && stackView.View.IsBound;
        }

        private bool HasValidMergeDestination(ContainerId sourceId)
        {
            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in stackViewsByContainerId)
            {
                if (IsValidMergeDestination(sourceId, pair.Key, pair.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsValidMergeDestination(
            ContainerId sourceId,
            ContainerId destinationId,
            StackRuntimeView destinationView)
        {
            return destinationId != sourceId
                && destinationView != null
                && destinationView.View != null
                && destinationView.View.IsBound
                && matchState.Containers.TryGetValue(destinationId, out ContainerState destination)
                && destination.Kind == ContainerKind.Stack;
        }

        private bool IsContextMenuTargetAvailable()
        {
            if (matchState == null)
            {
                return false;
            }

            switch (contextMenuMode)
            {
                case ContextMenuMode.Deck:
                case ContextMenuMode.PopulateDeck:
                    return !contextMenuContainerId.IsEmpty
                        && matchState.Containers.TryGetValue(contextMenuContainerId, out ContainerState deck)
                        && deck.Kind == ContainerKind.Deck
                        && (contextMenuMode != ContextMenuMode.PopulateDeck || deck.Count == 0)
                        && (contextMenuCardId.IsEmpty
                            || (IsCurrentContextCardInContainer(contextMenuContainerId)
                                && TryGetCardView(contextMenuCardId, out _)))
                        && TryGetDeckPresentation(contextMenuContainerId, out _, out _);
                case ContextMenuMode.DrawCards:
                case ContextMenuMode.CustomDrawCards:
                    return IsAssistedControllerDeck(contextMenuContainerId)
                        && TryGetDeckPresentation(contextMenuContainerId, out _, out _);
                case ContextMenuMode.TabletopCard:
                    return IsCurrentContextCardInContainer(ContainerId.Empty)
                        && TryGetCardView(contextMenuCardId, out _);
                case ContextMenuMode.FloorCard:
                    return trapFloorTemplate != null
                        && matchState.Cards.ContainsKey(contextMenuCardId)
                        && trapFloorTemplate.IsFloorCard(contextMenuCardId)
                        && TryGetCardView(contextMenuCardId, out _);
                case ContextMenuMode.PendingFloormasterCard:
                    return floormasterLifecycleState?.PendingCard != null
                        && floormasterLifecycleState.PendingCard.CardId == contextMenuCardId
                        && matchState.Cards.ContainsKey(contextMenuCardId)
                        && TryGetCardView(contextMenuCardId, out _);
                case ContextMenuMode.StackCard:
                    return IsCurrentContextCardInContainer(contextMenuContainerId)
                        && matchState.Containers.TryGetValue(contextMenuContainerId, out ContainerState stackCardContainer)
                        && stackCardContainer.Kind == ContainerKind.Stack
                        && TryGetCardView(contextMenuCardId, out _);
                case ContextMenuMode.ContainedCard:
                    return IsCurrentContextCardInContainer(contextMenuContainerId)
                        && matchState.Containers.ContainsKey(contextMenuContainerId)
                        && TryGetCardView(contextMenuCardId, out _);
                case ContextMenuMode.Stack:
                case ContextMenuMode.MergeDestination:
                    return TryGetContextStack(out _, out _);
                case ContextMenuMode.DiscardPile:
                    return !contextMenuContainerId.IsEmpty
                        && matchState.Containers.TryGetValue(contextMenuContainerId, out ContainerState discardPile)
                        && discardPile.Kind == ContainerKind.DiscardPile
                        && (contextMenuCardId.IsEmpty
                            || (IsCurrentContextCardInContainer(contextMenuContainerId)
                                && TryGetCardView(contextMenuCardId, out _)))
                        && TryGetRuntimeDiscardPile(contextMenuContainerId, out _);
                case ContextMenuMode.Die:
                    return !contextMenuDieId.IsEmpty
                        && matchState.Dice.ContainsKey(contextMenuDieId)
                        && TryGetDieView(contextMenuDieId, out _);
                case ContextMenuMode.Pawn:
                    return !contextMenuPawnId.IsEmpty
                        && matchState.Pawns.ContainsKey(contextMenuPawnId)
                        && TryGetPawnView(contextMenuPawnId, out _);
                case ContextMenuMode.Token:
                    return !contextMenuTokenId.IsEmpty
                        && matchState.Tokens.ContainsKey(contextMenuTokenId)
                        && TryGetTokenView(contextMenuTokenId, out _);
                case ContextMenuMode.Console:
                    return TryResolveConsolePlacement(
                        contextMenuContainerId,
                        out _,
                        out _,
                        out _);
                default:
                    return false;
            }
        }
    }
}
