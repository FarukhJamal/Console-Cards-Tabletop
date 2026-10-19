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
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The right-click menus: which menu a click opens, what each menu offers and the small count and choice popups that follow from them.
    /// (R1d: moved out of TabletopComposition; the table reaches it as <c>ContextMenus</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class ContextMenuPresenter
    {
        private readonly TabletopComposition host;

        internal ContextMenuPresenter(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.ContextMenus.cs ----------

        internal void HandleSecondaryPointerPressed(Vector2 screenPosition)
        {
            if (!host.IsInitialized || (host.interactionRouter != null && host.interactionRouter.HasActiveInteraction))
            {
                return;
            }

            CloseContextMenu();
            if (host.hitResolver.TryResolve(screenPosition, out TabletopObjectView resolvedObjectView))
            {
                if (resolvedObjectView is DieView hitDie
                    && hitDie.IsBound
                    && hitDie.DieState != null)
                {
                    host.selectionState.Select(hitDie);
                    host.selectionPresenter.Refresh();
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
                    host.selectionState.Select(hitPawn);
                    host.selectionPresenter.Refresh();
                    OpenPawnContextMenu(screenPosition, hitPawn.ObjectId);
                    return;
                }

                if (resolvedObjectView is TokenView hitToken && hitToken.IsBound)
                {
                    host.selectionState.Select(hitToken);
                    host.selectionPresenter.Refresh();
                    OpenTokenContextMenu(screenPosition, hitToken.ObjectId);
                    return;
                }
            }

            if (!host.dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container
                || !host.matchState.Containers.TryGetValue(target.ContainerId, out ContainerState container))
            {
                // C3d: the mat itself, between its slots, opens the Console menu too.
                if (host.TryFindConsoleUnderPointer(screenPosition, out RuntimeConsoleInstance pointedConsole))
                {
                    OpenConsoleContextMenu(screenPosition, pointedConsole.SlotViews[0].ContainerId);
                }
                else if (host.TryFindBoardUnderPointer(screenPosition, out BoardId pointedBoard, out _))
                {
                    host.OpenBoardContextMenu(screenPosition, pointedBoard); // C3e
                }

                return;
            }

            if (container.Kind == ContainerKind.Deck
                && host.Registry.TryGetDeckPresentation(container.Id, out _, out _))
            {
                OpenContextMenu(
                    ContextMenuMode.Deck,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.Stack
                && host.Registry.stackViewsByContainerId.ContainsKey(container.Id))
            {
                OpenContextMenu(
                    ContextMenuMode.Stack,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.DiscardPile
                && host.Registry.TryGetRuntimeDiscardPile(container.Id, out _))
            {
                OpenContextMenu(
                    ContextMenuMode.DiscardPile,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.ConsoleSlot
                && host.TryResolveConsolePlacement(container.Id, out _, out _, out _))
            {
                OpenConsoleContextMenu(screenPosition, container.Id);
            }
        }

        internal bool CanBeginCameraOrbit(Vector2 screenPosition)
        {
            if (!host.IsInitialized
                || host.hitResolver == null
                || (host.interactionRouter != null && host.interactionRouter.HasActiveInteraction))
            {
                return false;
            }

            if (host.hitResolver.TryResolve(screenPosition, out _))
            {
                return false;
            }

            bool canOrbit = host.dropTargetResolver == null
                || !host.dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container;
            if (canOrbit)
            {
                CloseContextMenu();
            }

            return canOrbit;
        }

        internal bool TryOpenCardContextMenu(CardView hitCard, Vector2 screenPosition)
        {
            if (hitCard == null || !hitCard.IsBound || hitCard.CardState == null)
            {
                return false;
            }

            if (host.floormasterLifecycleState?.PendingCard != null
                && host.floormasterLifecycleState.PendingCard.CardId == hitCard.ObjectId)
            {
                host.selectionState.Select(hitCard);
                host.selectionPresenter.Refresh();
                OpenContextMenu(
                    ContextMenuMode.PendingFloormasterCard,
                    screenPosition,
                    hitCard.ObjectId,
                    ContainerId.Empty,
                    TabletopObjectId.Empty);
                return true;
            }

            if (host.trapFloorTemplate != null && host.trapFloorTemplate.IsFloorCard(hitCard.ObjectId))
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
                host.selectionState.Select(hitCard);
                host.selectionPresenter.Refresh();
                OpenContextMenu(
                    ContextMenuMode.TabletopCard,
                    screenPosition,
                    hitCard.ObjectId,
                    ContainerId.Empty,
                    TabletopObjectId.Empty);
                return true;
            }

            if (!host.matchState.Containers.TryGetValue(containerId, out ContainerState container))
            {
                return false;
            }

            if (container.Kind == ContainerKind.Deck
                && host.Registry.TryGetDeckPresentation(container.Id, out _, out _))
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
                && host.Registry.TryGetRuntimeDiscardPile(containerId, out _))
            {
                OpenContextMenu(
                    ContextMenuMode.DiscardPile,
                    screenPosition,
                    hitCard.ObjectId,
                    containerId,
                    TabletopObjectId.Empty);
                return true;
            }

            // A card in a stack opens the stack's menu, as a card in a deck opens the deck's (owner, 2026-10-18):
            // a stack made by dropping cards on each other works like any other pile.
            if (container.Kind == ContainerKind.Stack
                && host.Registry.stackViewsByContainerId.ContainsKey(containerId))
            {
                OpenContextMenu(
                    ContextMenuMode.Stack,
                    screenPosition,
                    hitCard.ObjectId,
                    containerId,
                    TabletopObjectId.Empty);
                return true;
            }

            host.selectionState.Select(hitCard);
            host.selectionPresenter.Refresh();
            OpenContextMenu(
                ContextMenuMode.ContainedCard,
                screenPosition,
                hitCard.ObjectId,
                containerId,
                TabletopObjectId.Empty);
            return true;
        }

        internal void OpenContextMenu(
            ContextMenuMode mode,
            Vector2 screenPosition,
            TabletopObjectId cardId,
            ContainerId containerId,
            TabletopObjectId dieId)
        {
            host.contextMenuAnchorScreenPosition = screenPosition;
            host.contextMenuCardId = cardId;
            host.contextMenuContainerId = containerId;
            host.contextMenuDieId = dieId;
            host.contextMenuPawnId = TabletopObjectId.Empty;
            host.contextMenuTokenId = TabletopObjectId.Empty;
            host.contextMenuConsoleId = ConsoleId.Empty;
            if (mode == ContextMenuMode.Deck)
            {
                host.selectedDrawCount = Mathf.Clamp(
                    host.selectedDrawCount,
                    1,
                    Math.Max(1, host.TrapFloorGame.AvailableDrawableCount(containerId)));
            }

            SetContextMenuMode(mode);
        }

        internal void OpenPawnContextMenu(Vector2 screenPosition, TabletopObjectId pawnId)
        {
            host.contextMenuAnchorScreenPosition = screenPosition;
            host.contextMenuCardId = TabletopObjectId.Empty;
            host.contextMenuContainerId = ContainerId.Empty;
            host.contextMenuDieId = TabletopObjectId.Empty;
            host.contextMenuPawnId = pawnId;
            host.contextMenuTokenId = TabletopObjectId.Empty;
            host.contextMenuConsoleId = ConsoleId.Empty;
            SetContextMenuMode(ContextMenuMode.Pawn);
        }

        internal void OpenTokenContextMenu(Vector2 screenPosition, TabletopObjectId tokenId)
        {
            host.contextMenuAnchorScreenPosition = screenPosition;
            host.contextMenuCardId = TabletopObjectId.Empty;
            host.contextMenuContainerId = ContainerId.Empty;
            host.contextMenuDieId = TabletopObjectId.Empty;
            host.contextMenuPawnId = TabletopObjectId.Empty;
            host.contextMenuTokenId = tokenId;
            host.contextMenuConsoleId = ConsoleId.Empty;
            SetContextMenuMode(ContextMenuMode.Token);
        }

        internal void OpenConsoleContextMenu(Vector2 screenPosition, ContainerId slotContainerId)
        {
            host.contextMenuAnchorScreenPosition = screenPosition;
            host.contextMenuCardId = TabletopObjectId.Empty;
            host.contextMenuContainerId = slotContainerId;
            host.contextMenuDieId = TabletopObjectId.Empty;
            host.contextMenuPawnId = TabletopObjectId.Empty;
            host.contextMenuTokenId = TabletopObjectId.Empty;
            host.contextMenuConsoleId = host.Registry.TryGetPlacedConsoleBySlot(slotContainerId, out ConsoleId consoleId)
                ? consoleId
                : ConsoleId.Empty;
            SetContextMenuMode(ContextMenuMode.Console);
        }

        internal void SetContextMenuMode(ContextMenuMode mode)
        {
            host.contextMenuMode = mode;
            if (mode == ContextMenuMode.None)
            {
                host.runtimeUi?.CloseTabletopPopup();
                return;
            }

            RenderOpenTabletopPopup();
        }

        internal void CloseContextMenu()
        {
            host.contextMenuMode = ContextMenuMode.None;
            host.contextMenuCardId = TabletopObjectId.Empty;
            host.contextMenuDieId = TabletopObjectId.Empty;
            host.contextMenuPawnId = TabletopObjectId.Empty;
            host.contextMenuTokenId = TabletopObjectId.Empty;
            host.contextMenuContainerId = ContainerId.Empty;
            host.contextMenuConsoleId = ConsoleId.Empty;
            host.contextMenuRenderedRevision = -1;
            host.runtimeUi?.CloseTabletopPopup();
        }

        internal void DismissPopupFromSecondary(Vector2 _)
        {
            CloseContextMenu();
        }

        internal void RefreshOpenTabletopPopup()
        {
            if (host.contextMenuMode == ContextMenuMode.None || !host.IsInitialized)
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
            // DEAL-1: the "how many players?" question after a game starts waits for an answer too.
            if (host.contextMenuMode != ContextMenuMode.Die
                && host.contextMenuMode != ContextMenuMode.DealPlayers
                && host.matchState != null && host.contextMenuRenderedRevision != host.matchState.Revision)
            {
                CloseContextMenu();
            }
        }

        internal void RenderOpenTabletopPopup()
        {
            if (host.runtimeUi == null || !IsContextMenuTargetAvailable())
            {
                CloseContextMenu();
                return;
            }

            switch (host.contextMenuMode)
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
                case ContextMenuMode.SplitPile:
                    host.ShowSplitPilePopup();
                    break;
                case ContextMenuMode.TabletopCard:
                    ShowTabletopCardContextMenu();
                    break;
                case ContextMenuMode.FloorCard:
                case ContextMenuMode.PendingFloormasterCard:
                    // CART-1a: the game's own menus come from the cartridge in play.
                    if (!host.ActiveCartridge.TryShowContextMenu(host.contextMenuMode))
                    {
                        CloseContextMenu();
                        return;
                    }

                    break;
                case ContextMenuMode.ContainedCard:
                    ShowContainedCardContextMenu();
                    break;
                case ContextMenuMode.Stack:
                    ShowStackContextMenu();
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
                case ContextMenuMode.ShuffleStyles:
                    host.ShowShuffleStylesMenu();
                    break;
                case ContextMenuMode.ShuffleDefault:
                    host.ShowShuffleDefaultMenu();
                    break;
                case ContextMenuMode.Flourishes:
                    host.ShowFlourishMenu();
                    break;
                case ContextMenuMode.DealForm:
                    host.ShowDealForm();
                    break;
                case ContextMenuMode.DealPlayers:
                    host.ShowDealPlayersMenu();
                    break;
                default:
                    CloseContextMenu();
                    return;
            }

            host.contextMenuRenderedRevision = host.matchState.Revision;
        }

        internal void ShowDeckContextMenu()
        {
            ContainerId targetDeckId = host.contextMenuContainerId;
            ContainerState targetDeck = host.matchState.GetContainer(targetDeckId);
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (!host.contextMenuCardId.IsEmpty)
            {
                AddInspectAction(actions, host.contextMenuCardId);
            }

            AddControllerDrawAction(actions, targetDeckId);
            if (host.HasLocalHand() && !host.TrapFloorGame.IsAssistedControllerDeck(targetDeckId) && targetDeck.Count > 0)
            {
                actions.Add(new PopupActionOption(
                    "Draw to Hand",
                    true,
                    () => host.OpenDrawToHandPopup(targetDeckId)));
            }

            if (targetDeck.Count == 0)
            {
                actions.Add(new PopupActionOption(
                    "Populate Deck",
                    true,
                    () =>
                    {
                        host.selectedQuantity = 1;
                        SetContextMenuMode(ContextMenuMode.PopulateDeck);
                    }));
            }

            host.AddShuffleActions(actions, targetDeckId); // SHUF-1: your style, Shuffle as..., Flourish...
            host.AddDealActions(actions, targetDeckId); // DEAL-1
            host.AddSplitIntoPilesAction(actions, targetDeckId);
            host.AddSpreadAction(actions, targetDeckId); // POL-4
            host.AddPileDuplicateAction(actions, targetDeckId);
            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => host.BeginContainerMove(targetDeckId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(targetDeckId));
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "DECK",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void ShowDiscardPileContextMenu()
        {
            ContainerId targetPileId = host.contextMenuContainerId;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (!host.contextMenuCardId.IsEmpty)
            {
                AddInspectAction(actions, host.contextMenuCardId);
            }

            host.AddSpreadAction(actions, targetPileId); // POL-4
            host.AddPileDuplicateAction(actions, targetPileId);
            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => host.BeginContainerMove(targetPileId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(targetPileId));
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "DISCARD PILE",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void AddDeleteActionIfRuntime(
            List<PopupActionOption> actions,
            TabletopComponentTarget target)
        {
            bool isTemplateComponent;
            switch (target.Kind)
            {
                case TabletopComponentTargetKind.Object:
                    isTemplateComponent = host.matchState.IsTemplateObject(target.ObjectId);
                    break;
                case TabletopComponentTargetKind.Container:
                    isTemplateComponent = host.matchState.IsTemplateContainer(target.ContainerId);
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

        internal string DuplicateActionLabel(
            TabletopObjectId sourceObjectId,
            TabletopComponentKind componentKind)
        {
            // A card is copied exactly: same card, same face (owner, 2026-10-18). Other pieces from a game become generic.
            return componentKind != TabletopComponentKind.Card && host.matchState.IsTemplateObject(sourceObjectId)
                ? $"Duplicate as Generic {componentKind}"
                : "Duplicate";
        }

        internal void DeleteComponent(TabletopComponentTarget target)
        {
            DeleteTabletopComponentResult result = host.TableAuthority.Submit<DeleteTabletopComponentRequest, DeleteTabletopComponentResult>(
                new DeleteTabletopComponentRequest(host.CreateCommandContext(), target));
            if (!result.Succeeded)
            {
                string detail = result.Error == DeleteTabletopComponentError.ContainerNotEmpty
                    ? "Container must be empty."
                    : result.Error == DeleteTabletopComponentError.ConsoleNotEmpty
                        ? "Every Console Slot must be empty."
                        : result.Error.ToString();
                host.ShowMessage($"Delete rejected: {detail}");
                return;
            }

            CloseContextMenu(); // R2: the view was removed by the table's view sync
            host.ShowMessage($"Deleted {result.ComponentKind}.");
        }

        internal void ShowControllerDrawPopup()
        {
            ContainerId targetDeckId = host.contextMenuContainerId;
            int availableCount = host.TrapFloorGame.AvailableControllerDeckCount(targetDeckId);
            ControllerConfigurationData configuration =
                host.trapFloorTemplate.GameDefinition.ControllerConfiguration;
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
                    $"Draw To Hand Limit ({host.handComfortSettings.MaxHandCards})",
                    availableCount > 0,
                    () => DrawUpToConfiguredHandLimitFromContext(targetDeckId)),
                new PopupActionOption(
                    "Custom",
                    availableCount > 0,
                    () =>
                    {
                        host.selectedDrawCount = Mathf.Clamp(
                            host.selectedDrawCount,
                            1,
                            Math.Max(1, host.TrapFloorGame.AvailableControllerDeckCount(targetDeckId)));
                        SetContextMenuMode(ContextMenuMode.CustomDrawCards);
                    }),
                new PopupActionOption(
                    host.handComfortSettings.CapLabel + "  -",
                    host.handComfortSettings.MaxHandCards > PlayerHandComfortSettings.MinimumMaxHandCards,
                    () => ChangeHandComfortCapFromDrawPopup(-1)),
                new PopupActionOption(
                    host.handComfortSettings.CapLabel + "  +",
                    true,
                    () => ChangeHandComfortCapFromDrawPopup(1)),
            };

            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
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

        internal void ChangeHandComfortCapFromDrawPopup(int delta)
        {
            host.handComfortSettings.MaxHandCards += delta;
            SetContextMenuMode(ContextMenuMode.DrawCards);
        }

        internal void ShowCustomControllerDrawPopup()
        {
            int availableCount = host.TrapFloorGame.AvailableControllerDeckCount(host.contextMenuContainerId);
            host.selectedDrawCount = availableCount > 0
                ? Mathf.Clamp(host.selectedDrawCount, 1, availableCount)
                : 0;
            host.runtimeUi.ShowDrawCountPopup(
                host.contextMenuAnchorScreenPosition,
                host.selectedDrawCount,
                availableCount,
                () => ChangeSelectedControllerDrawCount(-1),
                () => ChangeSelectedControllerDrawCount(1),
                ConfirmSelectedControllerDrawCount,
                CloseContextMenu,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void ChangeSelectedControllerDrawCount(int delta)
        {
            int availableCount = host.TrapFloorGame.AvailableControllerDeckCount(host.contextMenuContainerId);
            if (availableCount <= 0)
            {
                host.selectedDrawCount = 0;
                host.runtimeUi?.SetDrawCountPopupValue(0, 0);
                return;
            }

            host.selectedDrawCount = Mathf.Clamp(host.selectedDrawCount + delta, 1, availableCount);
            host.runtimeUi?.SetDrawCountPopupValue(host.selectedDrawCount, availableCount);
        }

        internal void ConfirmSelectedControllerDrawCount()
        {
            int availableCount = host.TrapFloorGame.AvailableControllerDeckCount(host.contextMenuContainerId);
            if (availableCount <= 0)
            {
                host.runtimeUi?.SetDrawCountPopupValue(0, 0);
                return;
            }

            DrawControllerCardsFromContext(
                host.contextMenuContainerId,
                Mathf.Clamp(host.selectedDrawCount, 1, availableCount));
        }

        internal void ShowPopulateDeckQuantityPopup()
        {
            ContainerState deck = host.matchState.GetContainer(host.contextMenuContainerId);
            int maximum = deck.Capacity > 0
                ? Math.Min(PopulateDeckUseCase.MaximumQuantity, deck.Capacity)
                : PopulateDeckUseCase.MaximumQuantity;
            host.selectedQuantity = Mathf.Clamp(host.selectedQuantity, 1, maximum);
            host.runtimeUi.ShowQuantityPopup(
                "POPULATE DECK",
                "Create generic blank Cards directly in this empty Deck.",
                "Populate",
                host.selectedQuantity,
                1,
                maximum,
                () => host.ChangeSelectedQuantity(-1, maximum),
                () => host.ChangeSelectedQuantity(1, maximum),
                ConfirmPopulateDeck,
                CloseContextMenu);
        }

        internal void ConfirmPopulateDeck()
        {
            ContainerId targetDeckId = host.contextMenuContainerId;
            if (!host.matchState.Containers.TryGetValue(targetDeckId, out ContainerState deck))
            {
                host.ShowMessage("Populate Deck rejected: DeckMissing.");
                CloseContextMenu();
                return;
            }

            int maximum = deck.Capacity > 0
                ? Math.Min(PopulateDeckUseCase.MaximumQuantity, deck.Capacity)
                : PopulateDeckUseCase.MaximumQuantity;
            int quantity = Mathf.Clamp(host.selectedQuantity, 1, maximum);
            PopulateDeckResult result = host.TableAuthority.Submit<PopulateDeckRequest, PopulateDeckResult>(
                new PopulateDeckRequest(host.CreateCommandContext(), targetDeckId, quantity));
            if (!result.Succeeded)
            {
                host.ShowMessage($"Populate Deck rejected: {result.Error}.");
                CloseContextMenu();
                return;
            }

            // R2: the new cards got their views from the table's view sync.
            host.ShowMessage($"Populated Deck with {quantity} generic Cards.");
            CloseContextMenu();
        }

        internal void ShowTabletopCardContextMenu()
        {
            TabletopObjectId targetCardId = host.contextMenuCardId;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            AddInspectAction(actions, targetCardId);
            actions.Add(new PopupActionOption(
                    "Flip",
                    true,
                    () => FlipContextCard(targetCardId)));
            actions.Add(new PopupActionOption(
                DuplicateActionLabel(targetCardId, TabletopComponentKind.Card),
                true,
                () => host.BeginDuplicatePlacement(targetCardId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetCardId));
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "CARD",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void FlipContextCard(TabletopObjectId targetCardId)
        {
            if (!host.Registry.TryGetCardView(targetCardId, out CardView targetCardView))
            {
                CloseContextMenu();
                return;
            }

            FlipInteractionResult result = host.flipCoordinator.Flip(targetCardView);
            host.ShowMessage(result.Succeeded ? "Card flipped." : $"Flip rejected: {result.Status}.");
            if (result.Succeeded)
            {
                CloseContextMenu();
            }
        }

        internal void ShowContainedCardContextMenu()
        {
            TabletopObjectId targetCardId = host.contextMenuCardId;
            ContainerState container = host.matchState.GetContainer(host.contextMenuContainerId);
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (!host.IsHiddenHandContainer(container))
            {
                AddInspectAction(actions, targetCardId);
            }

            if (container.Kind == ContainerKind.ConsoleSlot
                && host.TryResolveConsolePlacement(container.Id, out _, out _, out _))
            {
                ContainerId targetSlotContainerId = container.Id;
                actions.Add(new PopupActionOption(
                    "Move",
                    true,
                    () => host.BeginConsoleMove(targetSlotContainerId)));
            }

            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "CARD",
                $"In {FormatContainerKind(container.Kind)}",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal static string FormatContainerKind(ContainerKind kind)
        {
            return kind == ContainerKind.ConsoleSlot
                ? "Console Slot"
                : kind == ContainerKind.DiscardPile ? "Discard Pile" : kind.ToString();
        }

        internal void AddInspectAction(
            List<PopupActionOption> actions,
            TabletopObjectId targetCardId)
        {
            if (IsInAnotherPlayersHand(targetCardId)) return; // other hands stay private (H1b)
            actions.Add(new PopupActionOption(
                "Inspect",
                true,
                () => host.CardInspectPanel.OpenCardInspect(targetCardId)));
        }

        internal bool IsInAnotherPlayersHand(TabletopObjectId cardId)
        {
            return host.matchState != null
                && host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)
                && !card.BaseState.ContainerId.IsEmpty
                && host.matchState.Containers.TryGetValue(card.BaseState.ContainerId, out ContainerState container)
                && host.IsHiddenHandContainer(container);
        }

        internal void ShowStackContextMenu()
        {
            TryGetContextStack(out ContainerState stack, out StackRuntimeView stackView);
            ContainerId sourceStackId = stack.Id;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (!host.contextMenuCardId.IsEmpty)
            {
                AddInspectAction(actions, host.contextMenuCardId);
            }

            // The same pile actions as a deck (owner, 2026-10-18).
            if (host.HasLocalHand() && stack.Count > 0)
            {
                actions.Add(new PopupActionOption(
                    "Draw to Hand",
                    true,
                    () => host.OpenDrawToHandPopup(sourceStackId)));
            }

            if (stack.Count >= 2)
            {
                host.AddShuffleActions(actions, sourceStackId); // SHUF-1
            }

            host.AddDealActions(actions, sourceStackId); // DEAL-1

            if (stack.Count >= 2)
            {
                actions.Add(new PopupActionOption(
                    "Split Stack",
                    true,
                    () =>
                    {
                        SplitStackResult result = host.SplitStack(stack, stackView);
                        if (result.Succeeded)
                        {
                            CloseContextMenu();
                        }
                    }));
            }

            host.AddSplitIntoPilesAction(actions, sourceStackId);
            host.AddSpreadAction(actions, sourceStackId); // POL-4

            // Merging is done by hand (owner, 2026-10-18): press and hold the stack, carry it onto another stack.
            host.AddPileDuplicateAction(actions, sourceStackId);
            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => host.BeginContainerMove(sourceStackId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(sourceStackId));
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "STACK",
                $"{stack.Count} card{(stack.Count == 1 ? string.Empty : "s")}",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void ShowDieContextMenu()
        {
            TabletopObjectId targetDieId = host.contextMenuDieId;
            host.Registry.TryGetDieView(targetDieId, out DieView targetDieView);
            List<PopupActionOption> actions = new List<PopupActionOption>
            {
                new PopupActionOption(
                    "Roll",
                    true,
                    () => RollContextDie(targetDieId)),
                new PopupActionOption(
                    DuplicateActionLabel(targetDieId, TabletopComponentKind.Die),
                    true,
                    () => host.BeginDuplicatePlacement(targetDieId)),
            };
            host.AddDieNumberActions(actions, targetDieView);
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetDieId));
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "DIE",
                $"d{targetDieView.DieState.SideCount}: {targetDieView.DieState.CurrentValue}",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void ShowPawnContextMenu()
        {
            TabletopObjectId targetPawnId = host.contextMenuPawnId;
            List<PopupActionOption> actions = new List<PopupActionOption>
            {
                new PopupActionOption(
                    DuplicateActionLabel(targetPawnId, TabletopComponentKind.Pawn),
                    true,
                    () => host.BeginDuplicatePlacement(targetPawnId)),
            };
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetPawnId));
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "PAWN",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void ShowTokenContextMenu()
        {
            TabletopObjectId targetTokenId = host.contextMenuTokenId;
            List<PopupActionOption> actions = new List<PopupActionOption>
            {
                new PopupActionOption(
                    DuplicateActionLabel(targetTokenId, TabletopComponentKind.Token),
                    true,
                    () => host.BeginDuplicatePlacement(targetTokenId)),
            };
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetTokenId));
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "TOKEN",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void ShowConsoleContextMenu()
        {
            ConsoleId targetConsoleId = host.contextMenuConsoleId;
            ContainerId targetSlotContainerId = host.contextMenuContainerId;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            actions.Add(new PopupActionOption(
                "Move",
                true,
                () => host.BeginConsoleMove(targetSlotContainerId)));
            if (host.ConsoleMatHasBackSide())
            {
                actions.Add(new PopupActionOption(
                    "Flip (F)",
                    true,
                    () => host.FlipConsoleFromSlot(targetSlotContainerId)));
                actions.Add(new PopupActionOption(
                    "Duplicate (other side up)",
                    true,
                    () => host.BeginConsoleDuplicate(targetSlotContainerId)));
            }
            if (!targetConsoleId.IsEmpty)
            {
                AddDeleteActionIfRuntime(
                    actions,
                    TabletopComponentTarget.ForConsole(targetConsoleId));
            }

            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "CONSOLE",
                targetConsoleId.IsEmpty
                    ? string.Empty
                    : "Delete is available only while every Console Slot is empty.",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        internal void AddControllerDrawAction(
            List<PopupActionOption> actions,
            ContainerId targetDeckId)
        {
            if (!host.TrapFloorGame.IsAssistedControllerDeck(targetDeckId))
            {
                return;
            }

            actions.Add(new PopupActionOption(
                "Draw...",
                true,
                () => SetContextMenuMode(ContextMenuMode.DrawCards)));
        }

        internal void DrawControllerCardsFromContext(ContainerId targetDeckId, int requestedCount)
        {
            TrapFloorPlayerSetupDefinition player = host.TrapFloorGame.GetAssistedTrapFloorPlayerSetup();
            if (requestedCount <= 0
                || !host.TrapFloorGame.IsAssistedControllerDeck(targetDeckId)
                || host.contextMenuContainerId != targetDeckId)
            {
                CloseContextMenu();
                return;
            }

            int drawCount = Math.Min(requestedCount, host.TrapFloorGame.AvailableControllerDeckCount(targetDeckId));
            int deckLimitedCount = drawCount;
            drawCount = ClampToHandComfortCap(player.HandContainerId, drawCount);
            if (drawCount <= 0 && deckLimitedCount > 0)
            {
                host.ShowMessage(host.handComfortSettings.ReachedMessage + ".");
                CloseContextMenu();
                return;
            }

            if (drawCount <= 0)
            {
                host.ShowMessage("Controller Deck is empty.");
                CloseContextMenu();
                return;
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                host.CaptureContainerCardTransforms(targetDeckId, player.HandContainerId);
            DrawCardsResult result = host.TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(new DrawCardsCommand(
                    host.CreateCommandContext(host.trapFloorTurnState.ActivePlayerId),
                    targetDeckId,
                    player.HandContainerId,
                    drawCount));
            if (!result.Succeeded)
            {
                // Put the cards back where they were; an accepted draw was laid out by the table's view sync (R2).
                host.ApplyLayout(targetDeckId);
                host.ApplyLayout(player.HandContainerId);
            }

            host.presentationTransitions.AnimateCardsFromCurrentResults(
                transitionStarts,
                result.Succeeded ? host.handReflowDuration : host.returnDuration,
                result.Succeeded ? 0.035f : 0f);
            if (result.Succeeded)
            {
                host.ShowMessage(drawCount < deckLimitedCount
                    ? $"{host.handComfortSettings.ReachedMessage}: drew {drawCount} of {deckLimitedCount}."
                    : $"Drew {drawCount} Controller Card{(drawCount == 1 ? string.Empty : "s")}{host.ToHandSuffix(player.HandContainerId)}.");
                CloseContextMenu();
            }
            else
            {
                host.ShowMessage($"Controller draw rejected: {result.Error}.");
            }
        }

        internal void DrawUpToConfiguredHandLimitFromContext(ContainerId targetDeckId)
        {
            if (!host.TrapFloorGame.IsAssistedControllerDeck(targetDeckId)
                || host.contextMenuContainerId != targetDeckId)
            {
                CloseContextMenu();
                return;
            }

            ControllerInputHandDrawResult result = host.DrawUpToConfiguredHandLimit();
            if (result.Succeeded)
            {
                CloseContextMenu();
            }
        }

        internal ControllerInputHandDrawResult DrawControllerCardsUpToComfortCap(
            TrapFloorPlayerSetupDefinition player,
            ContainerState hand)
        {
            int drawCount = Math.Min(
                host.handComfortSettings.RemainingFor(hand),
                host.TrapFloorGame.AvailableControllerDeckCount(player.ControllerDeckId));
            if (drawCount <= 0)
            {
                host.ShowMessage(host.handComfortSettings.RemainingFor(hand) <= 0
                    ? host.handComfortSettings.ReachedMessage + "."
                    : "Controller Deck is empty.");
                return ControllerInputHandDrawResult.NoChange(host.matchState.Revision);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                host.CaptureContainerCardTransforms(player.ControllerDeckId, player.HandContainerId);
            DrawCardsResult result = host.TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(new DrawCardsCommand(
                    host.CreateCommandContext(host.trapFloorTurnState.ActivePlayerId),
                    player.ControllerDeckId,
                    player.HandContainerId,
                    drawCount));
            if (!result.Succeeded)
            {
                // Put the cards back where they were; an accepted draw was laid out by the table's view sync (R2).
                host.ApplyLayout(player.ControllerDeckId);
                host.ApplyLayout(player.HandContainerId);
            }

            host.presentationTransitions.AnimateCardsFromCurrentResults(
                transitionStarts,
                result.Succeeded ? host.handReflowDuration : host.returnDuration,
                result.Succeeded ? 0.035f : 0f);
            if (!result.Succeeded)
            {
                host.ShowMessage($"Controller draw rejected: {result.Error}.");
                return ControllerInputHandDrawResult.NoChange(host.matchState.Revision);
            }

            host.ShowMessage($"Drew {drawCount} Controller Card{(drawCount == 1 ? string.Empty : "s")}{host.ToHandSuffix(player.HandContainerId)} (hand cap {host.handComfortSettings.MaxHandCards}).");
            return ControllerInputHandDrawResult.Accepted(result.Revision, drawCount);
        }

        // Local comfort cap: limits only this player's own draws and drops into their own hand.
        internal int ClampToHandComfortCap(ContainerId handId, int requestedCount)
        {
            if (!host.matchState.Containers.TryGetValue(handId, out ContainerState hand))
            {
                return requestedCount;
            }

            return Math.Min(requestedCount, host.handComfortSettings.RemainingFor(hand));
        }

        internal bool IsOwnHandAtComfortCap(ContainerId containerId)
        {
            return host.handView != null
                && host.handView.IsBound
                && containerId == host.handView.ContainerId
                && host.matchState.Containers.TryGetValue(containerId, out ContainerState hand)
                && host.handComfortSettings.RemainingFor(hand) <= 0;
        }

        internal void ShowHandCapReached()
        {
            host.ShowMessage(host.handComfortSettings.ReachedMessage + ".");
        }

        internal void RollContextDie(TabletopObjectId targetDieId)
        {
            // Ignore callbacks retained from a closed/replaced menu; one accepted click launches once.
            if (host.contextMenuMode != ContextMenuMode.Die || host.contextMenuDieId != targetDieId) return;
            if (!host.Registry.TryGetDieView(targetDieId, out DieView targetDieView))
            {
                CloseContextMenu();
                return;
            }

            if (targetDieView.PhysicalObject == null || !targetDieView.PhysicalObject.Roll())
            {
                host.ShowMessage("Die Roll rejected: object is unavailable or controlled.");
                return;
            }
            host.ShowMessage("Rolling physically; result is accepted when the Die settles.");
            CloseContextMenu();
        }

        internal bool IsCurrentContextCardInContainer(ContainerId containerId)
        {
            return !host.contextMenuCardId.IsEmpty
                && host.matchState.Cards.TryGetValue(host.contextMenuCardId, out CardInstanceState card)
                && card.BaseState.ContainerId == containerId;
        }

        internal bool TryGetContextStack(out ContainerState stack, out StackRuntimeView stackView)
        {
            stack = null;
            stackView = null;
            return !host.contextMenuContainerId.IsEmpty
                && host.matchState.Containers.TryGetValue(host.contextMenuContainerId, out stack)
                && stack.Kind == ContainerKind.Stack
                && host.Registry.stackViewsByContainerId.TryGetValue(host.contextMenuContainerId, out stackView)
                && stackView.View != null
                && stackView.View.IsBound;
        }

        internal bool IsValidMergeDestination(
            ContainerId sourceId,
            ContainerId destinationId,
            StackRuntimeView destinationView)
        {
            return destinationId != sourceId
                && destinationView != null
                && destinationView.View != null
                && destinationView.View.IsBound
                && host.matchState.Containers.TryGetValue(destinationId, out ContainerState destination)
                && destination.Kind == ContainerKind.Stack;
        }

        internal bool IsContextMenuTargetAvailable()
        {
            if (host.matchState == null)
            {
                return false;
            }

            switch (host.contextMenuMode)
            {
                case ContextMenuMode.Deck:
                case ContextMenuMode.PopulateDeck:
                    return !host.contextMenuContainerId.IsEmpty
                        && host.matchState.Containers.TryGetValue(host.contextMenuContainerId, out ContainerState deck)
                        && deck.Kind == ContainerKind.Deck
                        && (host.contextMenuMode != ContextMenuMode.PopulateDeck || deck.Count == 0)
                        && (host.contextMenuCardId.IsEmpty
                            || (IsCurrentContextCardInContainer(host.contextMenuContainerId)
                                && host.Registry.TryGetCardView(host.contextMenuCardId, out _)))
                        && host.Registry.TryGetDeckPresentation(host.contextMenuContainerId, out _, out _);
                case ContextMenuMode.DrawCards:
                case ContextMenuMode.CustomDrawCards:
                    return host.TrapFloorGame.IsAssistedControllerDeck(host.contextMenuContainerId)
                        && host.Registry.TryGetDeckPresentation(host.contextMenuContainerId, out _, out _);
                case ContextMenuMode.TabletopCard:
                    return IsCurrentContextCardInContainer(ContainerId.Empty)
                        && host.Registry.TryGetCardView(host.contextMenuCardId, out _);
                case ContextMenuMode.FloorCard:
                    return host.trapFloorTemplate != null
                        && host.matchState.Cards.ContainsKey(host.contextMenuCardId)
                        && host.trapFloorTemplate.IsFloorCard(host.contextMenuCardId)
                        && host.Registry.TryGetCardView(host.contextMenuCardId, out _);
                case ContextMenuMode.PendingFloormasterCard:
                    return host.floormasterLifecycleState?.PendingCard != null
                        && host.floormasterLifecycleState.PendingCard.CardId == host.contextMenuCardId
                        && host.matchState.Cards.ContainsKey(host.contextMenuCardId)
                        && host.Registry.TryGetCardView(host.contextMenuCardId, out _);
                case ContextMenuMode.ContainedCard:
                    return IsCurrentContextCardInContainer(host.contextMenuContainerId)
                        && host.matchState.Containers.ContainsKey(host.contextMenuContainerId)
                        && host.Registry.TryGetCardView(host.contextMenuCardId, out _);
                case ContextMenuMode.Stack:
                    return TryGetContextStack(out _, out _)
                        && (host.contextMenuCardId.IsEmpty
                            || (IsCurrentContextCardInContainer(host.contextMenuContainerId)
                                && host.Registry.TryGetCardView(host.contextMenuCardId, out _)));
                case ContextMenuMode.SplitPile:
                    return host.CanSplitIntoPiles(host.contextMenuContainerId);
                case ContextMenuMode.ShuffleStyles:
                case ContextMenuMode.ShuffleDefault:
                case ContextMenuMode.Flourishes:
                    return host.CanShuffleInStyles(host.contextMenuContainerId);
                case ContextMenuMode.DealForm:
                    return host.CanDealFrom(host.contextMenuContainerId);
                case ContextMenuMode.DealPlayers:
                    return host.IsDealPlayersMenuAvailable();
                case ContextMenuMode.DiscardPile:
                    return !host.contextMenuContainerId.IsEmpty
                        && host.matchState.Containers.TryGetValue(host.contextMenuContainerId, out ContainerState discardPile)
                        && discardPile.Kind == ContainerKind.DiscardPile
                        && (host.contextMenuCardId.IsEmpty
                            || (IsCurrentContextCardInContainer(host.contextMenuContainerId)
                                && host.Registry.TryGetCardView(host.contextMenuCardId, out _)))
                        && host.Registry.TryGetRuntimeDiscardPile(host.contextMenuContainerId, out _);
                case ContextMenuMode.Die:
                    return !host.contextMenuDieId.IsEmpty
                        && host.matchState.Dice.ContainsKey(host.contextMenuDieId)
                        && host.Registry.TryGetDieView(host.contextMenuDieId, out _);
                case ContextMenuMode.Pawn:
                    return !host.contextMenuPawnId.IsEmpty
                        && host.matchState.Pawns.ContainsKey(host.contextMenuPawnId)
                        && host.Registry.TryGetPawnView(host.contextMenuPawnId, out _);
                case ContextMenuMode.Token:
                    return !host.contextMenuTokenId.IsEmpty
                        && host.matchState.Tokens.ContainsKey(host.contextMenuTokenId)
                        && host.Registry.TryGetTokenView(host.contextMenuTokenId, out _);
                case ContextMenuMode.Console:
                    return host.TryResolveConsolePlacement(
                        host.contextMenuContainerId,
                        out _,
                        out _,
                        out _);
                default:
                    return false;
            }
        }
    }
}
