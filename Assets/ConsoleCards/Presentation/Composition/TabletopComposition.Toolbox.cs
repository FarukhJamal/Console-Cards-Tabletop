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
    /// <summary>The Toolbox: placing pieces and real cards, duplicates, moving containers and Consoles, projecting new pieces. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        private void BeginToolboxPlacement(
            TabletopComponentKind componentKind,
            int dieSideCount = 0)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            ContextMenus.CloseContextMenu();
            GameObject previewRoot = CreateToolboxPlacementPreview(componentKind, dieSideCount);
            float rotation = localSeatLayout != null
                ? localSeatLayout.PlayerZonePose.RotationDegrees
                : 0f;
            componentPlacementController.Begin(
                componentKind,
                dieSideCount,
                previewRoot,
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride);
            ShowPlacementHint(
                componentKind == TabletopComponentKind.Die
                    ? $"d{dieSideCount}"
                    : ToolboxComponentLabel(componentKind));
        }

        private static string ToolboxComponentLabel(TabletopComponentKind componentKind)
        {
            return componentKind == TabletopComponentKind.DiscardPile ? "Discard Pile" : componentKind.ToString();
        }

        // A Toolbox pile takes its arrival face from the system pile style (doc 22, C2b): a Discard Pile turns
        // arriving Cards face down; a Deck or Stack keeps their face.
        internal static ContainerArrivalFace ToolboxArrivalFace(TabletopComponentKind componentKind)
        {
            switch (componentKind)
            {
                case TabletopComponentKind.Deck:
                    return GameTemplatePileStyle.DefaultFor(ContainerKind.Deck).ArrivalFace;
                case TabletopComponentKind.Stack:
                    return GameTemplatePileStyle.DefaultFor(ContainerKind.Stack).ArrivalFace;
                case TabletopComponentKind.DiscardPile:
                    return GameTemplatePileStyle.DefaultFor(ContainerKind.DiscardPile).ArrivalFace;
                default:
                    return ContainerArrivalFace.Unchanged;
            }
        }

        // A Toolbox tile was picked (doc 22, C2a). Cards: 1 places a loose card, 2 or more place one Deck of that
        // many face-down cards. Dice: the value is the chosen size. Everything else places its component.
        internal void PlaceCatalogEntry(ComponentCatalogEntry entry, int value)
        {
            EnsureInitialized();
            ContextMenus.CloseContextMenu();
            RulesCardPanel.RaiseHint(HintTriggers.ToolboxPlacing, entry.Kind.ToString());
            toolboxPlacementIcon = entry.Icon;
            if (TryCollectRealCards(entry, value, out List<ObjectDefinitionId> realCards))
            {
                BeginRealCardsPlacement(entry.DisplayName, realCards); // C3c: box tiles place real cards
                KeepToolboxPlacing(false);
                return;
            }

            switch (entry.Kind)
            {
                case ComponentCatalogKind.Card:
                    if (value <= 1)
                    {
                        BeginCardBatchPlacement(1);
                    }
                    else
                    {
                        BeginDeckOfCardsPlacement(Mathf.Min(value, CreateTabletopComponentUseCase.MaximumDeckCardCount));
                    }

                    break;
                case ComponentCatalogKind.Deck:
                    BeginToolboxPlacement(TabletopComponentKind.Deck);
                    break;
                case ComponentCatalogKind.Stack:
                    BeginToolboxPlacement(TabletopComponentKind.Stack);
                    break;
                case ComponentCatalogKind.DiscardPile:
                    BeginToolboxPlacement(TabletopComponentKind.DiscardPile);
                    break;
                case ComponentCatalogKind.Pawn:
                    BeginToolboxPlacement(TabletopComponentKind.Pawn);
                    break;
                case ComponentCatalogKind.Token:
                    BeginToolboxPlacement(TabletopComponentKind.Token);
                    break;
                case ComponentCatalogKind.Console:
                    BeginToolboxPlacement(TabletopComponentKind.Console);
                    break;
                case ComponentCatalogKind.Die:
                    BeginToolboxPlacement(TabletopComponentKind.Die, value);
                    break;
                case ComponentCatalogKind.GameBoard when entry.Prefab != null && entry.Prefab.GetComponent<GameBoardMatView>() != null:
                    BeginBoardPlacement(
                        BoardGrid.Standard,
                        false,
                        localSeatLayout != null ? localSeatLayout.PlayerZonePose.RotationDegrees : 0f);
                    KeepToolboxPlacing(true);
                    return;
                default:
                    toolboxPlacementIcon = null;
                    ShowMessage($"{entry.DisplayName} cannot be placed from the Toolbox yet.");
                    return;
            }

            KeepToolboxPlacing(false);
        }

        // L2-6b: a Toolbox piece (and a duplicate) is placed again and again until Esc or right-click. Each next
        // piece takes the next spawn order; a board keeps its own.
        private void KeepToolboxPlacing(bool board)
        {
            if (componentPlacementController == null || !componentPlacementController.IsActive) return;
            componentPlacementController.KeepPlacing(board ? (Func<int>)null : () => toolboxSpawnSequence * ToolboxPhysicalOrderStride);
            if (!string.IsNullOrWhiteSpace(toolboxPlacementSubject)) ShowPlacementHint($"{toolboxPlacementSubject} · Esc stops");
        }

        // C3c: an entry naming real cards (role "Card"): a Card entry places that card (2 or more: a deck of
        // copies); any other entry places a deck of all its cards, each as often as its quantity.
        private static bool TryCollectRealCards(ComponentCatalogEntry entry, int value, out List<ObjectDefinitionId> cards)
        {
            cards = null;
            List<ObjectDefinitionId> kinds = new List<ObjectDefinitionId>();
            List<int> counts = new List<int>();
            IReadOnlyList<ComponentDefinitionLink> links = entry.Definitions;
            for (int i = 0; links != null && i < links.Count; i++)
            {
                ComponentDefinitionLink link = links[i];
                if (link == null || link.Role != ComponentDefinitionLink.CardRole) continue;
                if (link.Definition is CardDefinition card && card.TryGetObjectDefinitionId(out ObjectDefinitionId cardId))
                {
                    kinds.Add(cardId);
                    counts.Add(Math.Max(1, card.Quantity));
                }
                else if (link.Definition is AvatarDefinition avatar && Guid.TryParse(avatar.StableId, out Guid avatarGuid))
                {
                    kinds.Add(new ObjectDefinitionId(avatarGuid));
                    counts.Add(1);
                }
            }

            if (kinds.Count == 0) return false;
            cards = new List<ObjectDefinitionId>();
            if (entry.Kind == ComponentCatalogKind.Card)
            {
                for (int i = 0; i < Math.Max(1, value); i++) cards.Add(kinds[0]);
            }
            else
            {
                for (int i = 0; i < kinds.Count; i++)
                {
                    for (int c = 0; c < counts[i]; c++) cards.Add(kinds[i]);
                }
            }

            int maximum = CreateTabletopComponentUseCase.MaximumDeckCardCount;
            if (cards.Count > maximum)
            {
                Debug.LogWarning($"[Toolbox] {entry.DisplayName} has {cards.Count} cards; a deck holds {maximum}.");
                cards.RemoveRange(maximum, cards.Count - maximum);
            }

            return true;
        }

        internal void BeginRealCardsPlacement(string subject, List<ObjectDefinitionId> cards)
        {
            bool single = cards.Count == 1;
            GameObject previewRoot = single
                ? CreateCardBatchPlacementPreview(1)
                : CreateDeckOfCardsPlacementPreview(cards.Count);
            float rotation = localSeatLayout != null
                ? localSeatLayout.PlayerZonePose.RotationDegrees
                : 0f;
            componentPlacementController.BeginCustomComponentPlacement(
                single ? TabletopComponentKind.Card : TabletopComponentKind.Deck,
                previewRoot,
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitRealCardsPlacement(subject, cards, pose));
            ShowPlacementHint(single ? subject : $"{subject} ({cards.Count} cards)");
        }

        // One command: the real card, or the Deck with its real face-down cards, so one Undo removes it. A saved
        // table (doc 29) also lays out cards with their face, decks of one card and the deck's arrival style, quietly.
        internal bool CommitRealCardsPlacement(
            string subject,
            List<ObjectDefinitionId> cards,
            TabletopPose requestedPose,
            CardFace face = CardFace.FaceUp,
            ContainerArrivalFace arrivalFace = ContainerArrivalFace.Unchanged,
            bool forceDeck = false,
            bool quiet = false)
        {
            bool single = cards.Count == 1 && !forceDeck;
            CreateTabletopComponentResult result = TableAuthority.Submit<CreateTabletopComponentRequest, CreateTabletopComponentResult>(
                new CreateTabletopComponentRequest(
                    CreateCommandContext(),
                    single ? TabletopComponentKind.Card : TabletopComponentKind.Deck,
                    requestedPose,
                    0,
                    single ? face : CardFace.FaceUp,
                    AuthoritativeActionKind.CreateComponent,
                    single ? 0 : cards.Count,
                    single ? ContainerArrivalFace.Unchanged : arrivalFace,
                    cards));
            if (!result.Succeeded)
            {
                ShowMessage($"Add {subject} rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            PlayerGamesPanel.lastPlacedContainerId = result.ContainerId; // doc 29: the game's deck is found by it
            // R2: the cards and the deck got their views from the table's view sync when the change was accepted.
            if (!quiet) ShowMessage(single ? $"Added {subject}." : $"Added {subject}: {cards.Count} cards.");
            return true;
        }

        private void BeginDeckOfCardsPlacement(int quantity)
        {
            GameObject previewRoot = CreateDeckOfCardsPlacementPreview(quantity);
            float rotation = localSeatLayout != null
                ? localSeatLayout.PlayerZonePose.RotationDegrees
                : 0f;
            componentPlacementController.BeginCustomComponentPlacement(
                TabletopComponentKind.Deck,
                previewRoot,
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitDeckOfCardsPlacement(quantity, pose));
            ShowPlacementHint($"Deck of {quantity} cards");
        }

        // One command: the Deck and its face-down cards, so one Undo removes both.
        private bool CommitDeckOfCardsPlacement(int quantity, TabletopPose requestedPose)
        {
            CreateTabletopComponentResult result = TableAuthority.Submit<CreateTabletopComponentRequest, CreateTabletopComponentResult>(
                new CreateTabletopComponentRequest(
                    CreateCommandContext(),
                    TabletopComponentKind.Deck,
                    requestedPose,
                    0,
                    CardFace.FaceUp,
                    AuthoritativeActionKind.CreateComponent,
                    quantity));
            if (!result.Succeeded)
            {
                ShowMessage($"Add Deck rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            // R2: the deck and its cards got their views from the table's view sync.
            ShowMessage($"Added a deck of {quantity} cards.");
            return true;
        }

        // The Deck ghost (its bay with the Draw mark) with up to 12 card ghosts stacked inside it.
        internal GameObject CreateDeckOfCardsPlacementPreview(int quantity)
        {
            GameObject previewRoot = CreateToolboxPlacementPreview(TabletopComponentKind.Deck, 0);
            int shown = Mathf.Min(quantity, 12);
            for (int i = 0; i < shown; i++)
            {
                CardVisualReferences preview = Instantiate(catalogCardPrefab, previewRoot.transform, false);
                preview.ValidateReferences();
                preview.FrontLabel.gameObject.SetActive(false);
                preview.BackLabel.gameObject.SetActive(false);
                preview.transform.localPosition = new Vector3(0f, 0.03f + (i * 0.02f), 0f);
                preview.transform.localRotation = Quaternion.identity;
            }

            DisablePlacementPreviewInteraction(previewRoot);
            TintPlacementPreview(previewRoot);
            return previewRoot;
        }

        private void OpenCardQuantityPopup()
        {
            EnsureInitialized();
            ContextMenus.CloseContextMenu();
            selectedQuantity = Mathf.Clamp(
                selectedQuantity,
                1,
                GenericCardBatchLayout.MaximumLooseBatchQuantity);
            runtimeUi.ShowQuantityPopup(
                "CREATE CARDS",
                "Choose how many generic Cards to place as one visible batch.",
                "Place Cards",
                selectedQuantity,
                1,
                GenericCardBatchLayout.MaximumLooseBatchQuantity,
                () => ChangeSelectedQuantity(-1, GenericCardBatchLayout.MaximumLooseBatchQuantity),
                () => ChangeSelectedQuantity(1, GenericCardBatchLayout.MaximumLooseBatchQuantity),
                ConfirmCardQuantity,
                ContextMenus.CloseContextMenu);
        }

        internal void ChangeSelectedQuantity(int delta, int maximum)
        {
            selectedQuantity = Mathf.Clamp(selectedQuantity + delta, 1, maximum);
            runtimeUi?.SetQuantityPopupValue(selectedQuantity, 1, maximum);
        }

        private void ConfirmCardQuantity()
        {
            int quantity = Mathf.Clamp(
                selectedQuantity,
                1,
                GenericCardBatchLayout.MaximumLooseBatchQuantity);
            runtimeUi?.CloseTabletopPopup();
            BeginCardBatchPlacement(quantity);
        }

        private void BeginCardBatchPlacement(int quantity)
        {
            GameObject previewRoot = CreateCardBatchPlacementPreview(quantity);
            float rotation = localSeatLayout != null
                ? localSeatLayout.PlayerZonePose.RotationDegrees
                : 0f;
            componentPlacementController.BeginCustomComponentPlacement(
                TabletopComponentKind.Card,
                previewRoot,
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitCardBatchPlacement(quantity, pose));
            componentPlacementController.PhysicalQuantity = quantity;
            ShowPlacementHint(quantity == 1 ? "Card" : $"{quantity} Cards");
        }

        private bool CommitCardBatchPlacement(int quantity, TabletopPose requestedPose)
        {
            CreateGenericCardBatchResult result = TableAuthority.Submit<CreateGenericCardBatchRequest, CreateGenericCardBatchResult>(
                new CreateGenericCardBatchRequest(
                    CreateCommandContext(),
                    quantity,
                    requestedPose));
            if (!result.Succeeded)
            {
                ShowMessage($"Add Cards rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            // R2: the cards got their views from the table's view sync.
            ShowMessage(quantity == 1 ? "Added Card." : $"Added {quantity} Cards.");
            return true;
        }

        public CreateTabletopComponentResult AddToolboxComponent(
            TabletopComponentKind componentKind,
            int dieSideCount = 0)
        {
            EnsureInitialized();
            return AddToolboxComponentAtPose(
                componentKind,
                dieSideCount,
                CreateNextToolboxSpawnPose());
        }

        internal CreateTabletopComponentResult AddToolboxComponentAtPose(
            TabletopComponentKind componentKind,
            int dieSideCount,
            TabletopPose requestedPose,
            bool consoleShowsBack = false,
            bool quiet = false)
        {
            CreateTabletopComponentResult result = TableAuthority.Submit<CreateTabletopComponentRequest, CreateTabletopComponentResult>(
                new CreateTabletopComponentRequest(
                    CreateCommandContext(),
                    componentKind,
                    requestedPose,
                    dieSideCount,
                    containerArrivalFace: ToolboxArrivalFace(componentKind),
                    consoleShowsBack: consoleShowsBack));
            if (!result.Succeeded)
            {
                ShowMessage($"Add {ToolboxComponentLabel(componentKind)} rejected: {result.Error}.");
                return result;
            }

            toolboxSpawnSequence++;
            // R2: the piece got its view from the table's view sync.
            if (!quiet) ShowMessage(componentKind == TabletopComponentKind.Die
                ? $"Added d{dieSideCount}."
                : $"Added {ToolboxComponentLabel(componentKind)}.");
            return result;
        }

        private bool CommitToolboxPlacement(
            TabletopComponentKind componentKind,
            int dieSideCount,
            TabletopPose requestedPose)
        {
            return AddToolboxComponentAtPose(componentKind, dieSideCount, requestedPose).Succeeded;
        }

        internal void BeginDuplicatePlacement(TabletopObjectId sourceObjectId)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            if (sourceObjectId.IsEmpty || !matchState.ContainsObject(sourceObjectId))
            {
                ShowMessage("Duplicate rejected: SourceMissing.");
                ContextMenus.CloseContextMenu();
                return;
            }

            TabletopObjectState sourceState = matchState.GetObject(sourceObjectId);
            if (!sourceState.ContainerId.IsEmpty)
            {
                ShowMessage("Duplicate rejected: SourceMustBeLoose.");
                ContextMenus.CloseContextMenu();
                return;
            }

            if (!TryResolveDuplicatedComponent(
                    sourceObjectId,
                    sourceState,
                    out TabletopComponentKind componentKind,
                    out int dieSideCount))
            {
                ShowMessage("Duplicate rejected: SourceKindUnsupported.");
                ContextMenus.CloseContextMenu();
                return;
            }

            GameObject previewRoot = CreateToolboxPlacementPreview(componentKind, dieSideCount);
            ContextMenus.CloseContextMenu();
            componentPlacementController.BeginCustomComponentPlacement(
                componentKind,
                previewRoot,
                sourceState.Pose.RotationDegrees,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitDuplicatePlacement(sourceObjectId, pose));
            string subject = componentKind == TabletopComponentKind.Die
                ? $"Duplicate d{dieSideCount}"
                : $"Duplicate {componentKind}";
            ShowPlacementHint(subject);
            KeepToolboxPlacing(false);
        }

        private bool CommitDuplicatePlacement(
            TabletopObjectId sourceObjectId,
            TabletopPose requestedPose)
        {
            DuplicateTabletopComponentResult result = TableAuthority.Submit<DuplicateTabletopComponentRequest, DuplicateTabletopComponentResult>(
                new DuplicateTabletopComponentRequest(
                    CreateCommandContext(),
                    sourceObjectId,
                    requestedPose));
            if (!result.Succeeded)
            {
                ShowMessage($"Duplicate rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            // R2: the copy got its view from the table's view sync.
            ShowMessage($"Duplicated the {ToolboxComponentLabel(result.CreationResult.ComponentKind)}.");
            return true;
        }

        private bool TryResolveDuplicatedComponent(
            TabletopObjectId sourceObjectId,
            TabletopObjectState sourceState,
            out TabletopComponentKind componentKind,
            out int dieSideCount)
        {
            dieSideCount = 0;
            switch (sourceState.Kind)
            {
                case TabletopObjectKind.Card:
                    componentKind = TabletopComponentKind.Card;
                    return true;
                case TabletopObjectKind.Pawn:
                    componentKind = TabletopComponentKind.Pawn;
                    return true;
                case TabletopObjectKind.Token:
                    componentKind = TabletopComponentKind.Token;
                    return true;
                case TabletopObjectKind.Die:
                    componentKind = TabletopComponentKind.Die;
                    dieSideCount = matchState.Dice[sourceObjectId].SideCount;
                    return true;
                default:
                    componentKind = default;
                    return false;
            }
        }

        internal void BeginContainerMove(ContainerId containerId)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            if (!matchState.Containers.TryGetValue(containerId, out ContainerState container)
                || !matchState.TryGetContainerPlacement(containerId, out ContainerPlacementState placement)
                || (container.Kind != ContainerKind.Deck
                    && container.Kind != ContainerKind.Stack
                    && container.Kind != ContainerKind.DiscardPile))
            {
                ShowMessage("Container move rejected: Container unavailable.");
                return;
            }

            if (MoveContainerUseCase.IsMoveBlockedByOwner(matchState, container, localPlayerId))
            {
                ShowMessage($"Container move rejected: {MoveContainerError.NotContainerOwner}.");
                return;
            }

            TabletopComponentKind previewKind = container.Kind == ContainerKind.Deck
                ? TabletopComponentKind.Deck
                : container.Kind == ContainerKind.DiscardPile
                    ? TabletopComponentKind.DiscardPile
                    : TabletopComponentKind.Stack;
            if (!TryResolveContainerMoveRoot(containerId, container.Kind, out GameObject sourceRoot))
            {
                ShowMessage("Container move rejected: bound View unavailable.");
                return;
            }

            componentPlacementController.Cancel();
            GameObject previewRoot = CreateToolboxPlacementPreview(previewKind, 0);
            List<GameObjectActivationSnapshot> sourceVisibility = HideContainerMoveSource(
                sourceRoot,
                new[] { containerId });
            ContextMenus.CloseContextMenu();
            try
            {
                componentPlacementController.BeginContainerMove(
                    previewRoot,
                    placement.Pose,
                    pose => CommitContainerMove(containerId, pose),
                    committed => CompleteContainerMovePresentation(
                        containerId,
                        false,
                        committed,
                        sourceVisibility));
            }
            catch
            {
                RestoreMoveSourceVisibility(sourceVisibility);
                previewRoot.SetActive(false);
                Destroy(previewRoot);
                throw;
            }

            ShowPlacementHint($"Move {ToolboxComponentLabel(previewKind)}");
            ShowMessage($"Move {ToolboxComponentLabel(previewKind)}: left-click to confirm, right-click or Escape to cancel.");
        }

        private bool CommitContainerMove(ContainerId containerId, TabletopPose requestedPose)
        {
            MoveContainerResult result = TableAuthority.Submit<MoveContainerCommand, MoveContainerResult>(
                new MoveContainerCommand(CreateCommandContext(), containerId, requestedPose));
            if (!result.Succeeded)
            {
                ShowMessage($"Container move rejected: {result.Error}.");
                return false;
            }

            // R2: the pile or Console was set in its new place by the table's view sync.
            ShowMessage("Container moved.");
            return true;
        }

        internal void BeginConsoleMove(ContainerId slotContainerId)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            if (!TryResolveConsolePlacement(
                    slotContainerId,
                    out TabletopPose pose,
                    out _,
                    out ConsoleView sourceView))
            {
                ShowMessage("Console move rejected: Console unavailable.");
                return;
            }

            componentPlacementController.Cancel();
            GameObject previewRoot = CreateToolboxPlacementPreview(TabletopComponentKind.Console, 0);
            List<GameObjectActivationSnapshot> sourceVisibility = HideContainerMoveSource(
                sourceView.gameObject,
                sourceView.ConsoleState.SlotContainerIds);
            ContextMenus.CloseContextMenu();
            try
            {
                componentPlacementController.BeginContainerMove(
                    previewRoot,
                    pose,
                    requestedPose => CommitContainerMove(slotContainerId, requestedPose),
                    committed => CompleteContainerMovePresentation(
                        slotContainerId,
                        true,
                        committed,
                        sourceVisibility));
            }
            catch
            {
                RestoreMoveSourceVisibility(sourceVisibility);
                previewRoot.SetActive(false);
                Destroy(previewRoot);
                throw;
            }

            ShowPlacementHint("Move Console");
            ShowMessage("Move Console: left-click to confirm, right-click or Escape to cancel.");
        }

        private bool TryResolveContainerMoveRoot(
            ContainerId containerId,
            ContainerKind containerKind,
            out GameObject sourceRoot)
        {
            if (containerKind == ContainerKind.Deck
                && Registry.TryGetDeckPresentation(containerId, out _, out FixedContainerVisual deckVisual))
            {
                sourceRoot = deckVisual.gameObject;
                return true;
            }

            if (containerKind == ContainerKind.Stack
                && Registry.stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackView)
                && stackView.Root != null)
            {
                sourceRoot = stackView.Root;
                return true;
            }

            if (containerKind == ContainerKind.DiscardPile
                && Registry.TryGetRuntimeDiscardPile(containerId, out RuntimeDiscardPileInstance discardPile)
                && discardPile.Root != null)
            {
                sourceRoot = discardPile.Root;
                return true;
            }

            sourceRoot = null;
            return false;
        }

        private List<GameObjectActivationSnapshot> HideContainerMoveSource(
            GameObject sourceRoot,
            IReadOnlyList<ContainerId> memberContainerIds)
        {
            List<GameObjectActivationSnapshot> snapshots = new List<GameObjectActivationSnapshot>();
            HashSet<GameObject> capturedObjects = new HashSet<GameObject>();
            CaptureActivation(sourceRoot, snapshots, capturedObjects);
            for (int containerIndex = 0; containerIndex < memberContainerIds.Count; containerIndex++)
            {
                if (!matchState.Containers.TryGetValue(
                        memberContainerIds[containerIndex],
                        out ContainerState memberContainer))
                {
                    continue;
                }

                for (int objectIndex = 0; objectIndex < memberContainer.ObjectIds.Count; objectIndex++)
                {
                    if (Registry.TryGetCardView(memberContainer.ObjectIds[objectIndex], out CardView card))
                    {
                        CaptureActivation(card.gameObject, snapshots, capturedObjects);
                    }
                }
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                snapshots[i].Target.SetActive(false);
            }

            return snapshots;
        }

        private void CompleteContainerMovePresentation(
            ContainerId authoritativeId,
            bool isConsole,
            bool committed,
            IReadOnlyList<GameObjectActivationSnapshot> sourceVisibility)
        {
            try
            {
                if (!committed && matchState != null)
                {
                    if (isConsole)
                    {
                        ApplyConsolePlacement(authoritativeId);
                    }
                    else
                    {
                        ApplyLayout(authoritativeId);
                    }
                }
            }
            finally
            {
                RestoreMoveSourceVisibility(sourceVisibility);
                Physics.SyncTransforms();
            }
        }

        private static void CaptureActivation(
            GameObject target,
            ICollection<GameObjectActivationSnapshot> snapshots,
            ISet<GameObject> capturedObjects)
        {
            if (target != null && capturedObjects.Add(target))
            {
                snapshots.Add(new GameObjectActivationSnapshot(target, target.activeSelf));
            }
        }

        private static void RestoreMoveSourceVisibility(
            IReadOnlyList<GameObjectActivationSnapshot> snapshots)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                GameObjectActivationSnapshot snapshot = snapshots[i];
                if (snapshot.Target != null)
                {
                    snapshot.Target.SetActive(snapshot.WasActive);
                }
            }
        }

        private TabletopPose CreateNextToolboxSpawnPose()
        {
            return CreateToolboxSpawnPose(toolboxSpawnSequence);
        }

        // The first quick-spawn grid cell with no placed Deck, Stack, Discard Pile or Console in it (H-E): a pile
        // made outside the Toolbox (the hand turned off) never lands on another, even after a rebuild.
        private TabletopPose CreateFreeToolboxSpawnPose()
        {
            ConsoleAdjacentPlacementSettings footprint = ConsoleAdjacentPlacementSettings.Standard;
            double cellWidth = footprint.BayWidth + footprint.MinimumClearance;
            double cellDepth = footprint.BayDepth + footprint.MinimumClearance;
            for (int sequence = 0; sequence < 12; sequence++)
            {
                TabletopPose candidate = CreateToolboxSpawnPose(sequence);
                bool occupied = false;
                foreach (ContainerPlacementState placement in matchState.ContainerPlacements.Values)
                {
                    if (!placement.HasExtent
                        && Math.Abs(placement.Pose.Position.X - candidate.Position.X) < cellWidth
                        && Math.Abs(placement.Pose.Position.Y - candidate.Position.Y) < cellDepth)
                    {
                        occupied = true;
                        break;
                    }
                }

                foreach (PlacedConsoleState console in matchState.PlacedConsoles.Values)
                {
                    if (Math.Abs(console.Pose.Position.X - candidate.Position.X) < cellWidth * 2d
                        && Math.Abs(console.Pose.Position.Y - candidate.Position.Y) < cellDepth * 1.5d)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                {
                    return candidate;
                }
            }

            return CreateToolboxSpawnPose(toolboxSpawnSequence);
        }

        private TabletopPose CreateToolboxSpawnPose(int sequence)
        {
            // Grid cells are the largest catalog footprint, a pile's bay, plus the placement clearance, so
            // quick-spawned piles never overlap (doc 22, C2b).
            ConsoleAdjacentPlacementSettings footprint = ConsoleAdjacentPlacementSettings.Standard;
            double cellWidth = footprint.BayWidth + footprint.MinimumClearance;
            double cellDepth = footprint.BayDepth + footprint.MinimumClearance;
            double baseX = -2.4d;
            double baseY = -2.4d;
            double columnX = cellWidth;
            double columnY = 0d;
            double rowX = 0d;
            double rowY = cellDepth;
            float rotation = 0f;
            if (localSeatLayout != null)
            {
                TabletopPose playerZonePose = localSeatLayout.PlayerZonePose;
                double x = playerZonePose.Position.X;
                double y = playerZonePose.Position.Y;
                double magnitude = Math.Sqrt((x * x) + (y * y));
                if (magnitude > 0.0001d)
                {
                    double radialX = x / magnitude;
                    double radialY = y / magnitude;
                    baseX = x - (radialX * 0.25d);
                    baseY = y - (radialY * 0.25d);
                    columnX = -radialY * cellWidth;
                    columnY = radialX * cellWidth;
                    // Rows step toward the table centre so the larger cells stay on the table.
                    rowX = -radialX * cellDepth;
                    rowY = -radialY * cellDepth;
                }
                else
                {
                    baseX = x;
                    baseY = y;
                }

                rotation = playerZonePose.RotationDegrees;
            }

            int column = sequence % 4;
            int row = (sequence / 4) % 3;
            return new TabletopPose(
                new TableCoordinate(
                    baseX + (column * columnX) + (row * rowX),
                    baseY + (column * columnY) + (row * rowY)),
                rotation,
                0,
                sequence * ToolboxPhysicalOrderStride);
        }

        private GameObject CreateToolboxPlacementPreview(
            TabletopComponentKind componentKind,
            int dieSideCount)
        {
            GameObject previewRoot;
            switch (componentKind)
            {
                case TabletopComponentKind.Card:
                {
                    CardVisualReferences preview = Instantiate(catalogCardPrefab);
                    preview.ValidateReferences();
                    preview.AlignFaceLabelsToSurface(tabletopLocalOrderHeight);
                    ConfigureWorldLabel(
                        preview.FrontLabel,
                        "CARD",
                        CardLabelCharacterSize,
                        CardLabelFontSize);
                    ConfigureWorldLabel(
                        preview.BackLabel,
                        preview.BackLabel.text,
                        CardBackLabelCharacterSize,
                        CardLabelFontSize);
                    preview.SetBackLabelHidden(HidesTableVisual(TableVisualHide.CardPlaceholderLabels));
                    ApplyLabelRendererHide(preview.FrontLabel, TableVisualHide.CardFaceLabels);
                    ApplyLabelRendererHide(preview.BackLabel, TableVisualHide.CardFaceLabels);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Deck:
                {
                    FixedContainerVisual preview = Instantiate(catalogDeckPrefab);
                    preview.ValidateReferences();
                    ConfigureContainerLabel(preview.Label, "DECK");
                    preview.Label.gameObject.SetActive(!HidesTableVisual(TableVisualHide.ContainerLabels));
                    ConfigureBayFootprint(preview, GameTemplatePileStyle.DefaultFor(ContainerKind.Deck).BayMark);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Stack:
                {
                    FixedContainerVisual preview = Instantiate(catalogStackPrefab);
                    preview.ValidateReferences();
                    ConfigureContainerLabel(preview.Label, "STACK");
                    preview.Label.gameObject.SetActive(!HidesTableVisual(TableVisualHide.ContainerLabels));
                    ConfigureBayFootprint(preview, GameTemplatePileStyle.DefaultFor(ContainerKind.Stack).BayMark);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.DiscardPile:
                {
                    FixedContainerVisual preview = Instantiate(catalogDiscardPilePrefab);
                    preview.ValidateReferences();
                    ConfigureContainerLabel(preview.Label, "DISCARD");
                    preview.Label.gameObject.SetActive(!HidesTableVisual(TableVisualHide.ContainerLabels));
                    ConfigureBayFootprint(preview, GameTemplatePileStyle.DefaultFor(ContainerKind.DiscardPile).BayMark);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Pawn:
                    previewRoot = Instantiate(catalogPawnPrefab).gameObject;
                    break;
                case TabletopComponentKind.Token:
                    previewRoot = Instantiate(catalogTokenPrefab).gameObject;
                    break;
                case TabletopComponentKind.Die:
                {
                    if (!ToolboxComponentDefinitions.IsSupportedDieSideCount(dieSideCount))
                    {
                        throw new ArgumentOutOfRangeException(nameof(dieSideCount));
                    }

                    DieView preview = Instantiate(catalogDiePrefab);
                    preview.ConfigurePhysicalShape(dieSideCount);
                    ConfigureWorldLabel(preview.ResultLabel, $"d{dieSideCount}\n1", 0.18f, 64);
                    ApplyLabelRendererHide(preview.ResultLabel, TableVisualHide.DieResultLabel);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Console:
                {
                    ConsoleView preview = Instantiate(catalogConsolePrefab);
                    previewRoot = preview.gameObject;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(componentKind));
            }

            Spawner.PrepareRuntimeRoot(previewRoot, $"{componentKind} Placement Preview");
            DisablePlacementPreviewInteraction(previewRoot);
            TintPlacementPreview(previewRoot);
            return previewRoot;
        }

        private GameObject CreateCardBatchPlacementPreview(int quantity)
        {
            GameObject previewRoot = new GameObject("Card Batch Placement Preview");
            Spawner.PrepareRuntimeRoot(previewRoot, previewRoot.name);
            TabletopPose origin = TabletopPose.Default;
            for (int i = 0; i < quantity; i++)
            {
                CardVisualReferences preview = Instantiate(catalogCardPrefab, previewRoot.transform, false);
                preview.ValidateReferences();
                preview.AlignFaceLabelsToSurface(tabletopLocalOrderHeight);
                ConfigureWorldLabel(
                    preview.FrontLabel,
                    "CARD",
                    CardLabelCharacterSize,
                    CardLabelFontSize);
                ConfigureWorldLabel(
                    preview.BackLabel,
                    preview.BackLabel.text,
                    CardBackLabelCharacterSize,
                    CardLabelFontSize);
                preview.SetBackLabelHidden(HidesTableVisual(TableVisualHide.CardPlaceholderLabels));
                ApplyLabelRendererHide(preview.FrontLabel, TableVisualHide.CardFaceLabels);
                ApplyLabelRendererHide(preview.BackLabel, TableVisualHide.CardFaceLabels);
                TabletopPose offsetPose = GenericCardBatchLayout.ResolvePose(origin, i, quantity, i);
                preview.transform.localPosition = new Vector3(
                    (float)(offsetPose.Position.X * worldUnitsPerTableUnit),
                    i * tabletopLocalOrderHeight,
                    (float)(offsetPose.Position.Y * worldUnitsPerTableUnit));
                preview.transform.localRotation = Quaternion.identity;
            }

            DisablePlacementPreviewInteraction(previewRoot);
            TintPlacementPreview(previewRoot);
            return previewRoot;
        }

        private static void DisablePlacementPreviewInteraction(GameObject previewRoot)
        {
            Collider[] colliders = previewRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            TabletopContainerDropTarget[] dropTargets =
                previewRoot.GetComponentsInChildren<TabletopContainerDropTarget>(true);
            for (int i = 0; i < dropTargets.Length; i++)
            {
                dropTargets[i].enabled = false;
            }

            TabletopObjectView[] objectViews = previewRoot.GetComponentsInChildren<TabletopObjectView>(true);
            for (int i = 0; i < objectViews.Length; i++)
            {
                objectViews[i].enabled = false;
            }

            TabletopSelectionVisual[] selectionVisuals =
                previewRoot.GetComponentsInChildren<TabletopSelectionVisual>(true);
            for (int i = 0; i < selectionVisuals.Length; i++)
            {
                if (selectionVisuals[i].IsConfigured)
                {
                    selectionVisuals[i].SetSelected(false);
                }

                selectionVisuals[i].enabled = false;
            }
        }

        private static void TintPlacementPreview(GameObject previewRoot)
        {
            Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            Color previewColor = new Color(0.42f, 0.82f, 1f, 0.72f);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer.GetComponent<TextMesh>() != null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", previewColor);
                properties.SetColor("_Color", previewColor);
                renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }

        private void ProjectCreatedToolboxComponent(CreateTabletopComponentResult result)
        {
            Transform appearedTransform;
            bool layoutCollectionChanged = false;
            switch (result.ComponentKind)
            {
                case TabletopComponentKind.Card:
                {
                    // The view sync (R2) may already have made the card's view with the rest of the new cards.
                    if (!Registry.TryGetCardView(result.ObjectId, out CardView view))
                    {
                        CardInstanceState card = matchState.Cards[result.ObjectId];
                        view = Spawner.CreateCardView(card, ResolveCardLabel(card), out TabletopSelectionVisual selectionVisual);
                        Registry.cardViews.Add(view);
                        Registry.cardSelectionVisuals.Add(selectionVisual);
                        RefreshContainerCardViewSources();
                    }

                    appearedTransform = view.transform;
                    break;
                }
                case TabletopComponentKind.Deck:
                    appearedTransform = ProjectToolboxDeck(result.ContainerId);
                    layoutCollectionChanged = true;
                    break;
                case TabletopComponentKind.Stack:
                    appearedTransform = ProjectToolboxStack(result.ContainerId, "STACK");
                    layoutCollectionChanged = true;
                    break;
                case TabletopComponentKind.DiscardPile:
                    appearedTransform = ProjectDiscardPile(result.ContainerId);
                    layoutCollectionChanged = true;
                    break;
                case TabletopComponentKind.Pawn:
                {
                    PawnView view = Spawner.CreatePawnView(
                        matchState.Pawns[result.ObjectId],
                        out TabletopSelectionVisual selectionVisual);
                    Registry.pawnViews.Add(view);
                    Registry.pawnSelectionVisuals.Add(selectionVisual);
                    appearedTransform = view.transform;
                    break;
                }
                case TabletopComponentKind.Token:
                {
                    TokenView view = Spawner.CreateTokenView(
                        matchState.Tokens[result.ObjectId],
                        out TabletopSelectionVisual selectionVisual,
                        1f);
                    Registry.tokenViews.Add(view);
                    Registry.tokenSelectionVisuals.Add(selectionVisual);
                    appearedTransform = view.transform;
                    break;
                }
                case TabletopComponentKind.Die:
                {
                    DieState die = matchState.Dice[result.ObjectId];
                    DieView view = Spawner.CreateDieView(
                        die,
                        $"d{die.SideCount}",
                        out TabletopSelectionVisual selectionVisual);
                    Registry.dieViews.Add(view);
                    Registry.dieSelectionVisuals.Add(selectionVisual);
                    appearedTransform = view.transform;
                    break;
                }
                case TabletopComponentKind.Console:
                    appearedTransform = ProjectToolboxConsole(matchState.PlacedConsoles[result.ConsoleId]);
                    layoutCollectionChanged = true;
                    break;
                default:
                    throw new InvalidOperationException("Accepted toolbox component kind is unsupported by Presentation.");
            }

            if (layoutCollectionChanged)
            {
                RebuildLayoutLookupAndRouter();
            }

            RefreshSelectionPresenterAfterRuntimeProjection();
            Physics.SyncTransforms();
            presentationTransitions.Appear(appearedTransform, settleDuration);
            Physics.SyncTransforms();
        }

        // The Presentation of one placed Deck, Stack, Discard Pile or Console the Toolbox (or a split) made. Used
        // when it is placed and again when Undo or Redo rebuilds the table (doc 22, C2b). No interaction
        // rebuild or appear animation here; the callers do that.
        private Transform ProjectToolboxDeck(ContainerId containerId)
        {
            RuntimeDeckInstance instance = Spawner.CreateRuntimeDeckInstance(
                "Toolbox Deck",
                "DECK",
                containerId,
                true);
            Registry.runtimeDeckInstances.Add(instance);
            Registry.controllerDeckViews.Add(instance.View);
            GameTemplatePileStyle toolboxDeckStyle = GameTemplatePileStyle.DefaultFor(ContainerKind.Deck);
            instance.View.ConfigureTableRest(true, toolboxDeckStyle.MaximumPileHeight);
            ConfigurePileBay(instance.Visual, toolboxDeckStyle, SeatId.Empty);
            instance.View.Bind(
                matchState.GetContainer(containerId),
                matchState.ContainerPlacements[containerId],
                coordinateConverter,
                Registry.cardViews);
            ConfigureFixedContainer(instance.Visual, instance.View);
            return instance.Root.transform;
        }

        private Transform ProjectToolboxStack(ContainerId containerId, string name)
        {
            ContainerState container = matchState.GetContainer(containerId);
            ContainerPlacementState placement = matchState.ContainerPlacements[containerId];
            StackRuntimeView stack = Spawner.CreateStackRuntimeView(name, container, placement, true);
            Registry.stackViewsByContainerId.Add(containerId, stack);
            return stack.Root.transform;
        }

        private Transform ProjectToolboxConsole(PlacedConsoleState placedConsole)
        {
            RuntimeConsoleInstance instance = Spawner.CreateRuntimeConsoleInstance(
                "Toolbox Console",
                placedConsole);
            Registry.runtimeConsoleInstances.Add(instance);
            Registry.playerConsoleViews.Add(instance.View);
            BindConsole(
                instance.View,
                placedConsole.Console,
                instance.SlotViews,
                instance.SlotVisuals);
            for (int i = 0; i < instance.SlotViews.Length; i++)
            {
                ConfigureConsoleSlot(instance.SlotViews[i]);
            }

            ApplyConsoleSide(instance);
            return instance.Root.transform;
        }

        // Every Discard Pile, from the Toolbox or declared by a template, is the catalog prefab resting on the
        // table in its bay with the Discard mark; a template pile uses its declared style (doc 22, C2b).
        private Transform ProjectDiscardPile(ContainerId containerId)
        {
            ContainerState container = matchState.GetContainer(containerId);
            GameTemplatePileStyle style = ResolveTemplatePileStyle(containerId)
                ?? GameTemplatePileStyle.DefaultFor(ContainerKind.DiscardPile);
            FixedContainerVisual visual = Instantiate(catalogDiscardPilePrefab);
            GameObject root = Spawner.PrepareRuntimeRoot(
                visual.gameObject,
                matchState.IsTemplateContainer(containerId) ? "Template Discard Pile" : "Toolbox Discard Pile");
            visual.ValidateReferences();
            DiscardPileView view = visual.GetView<DiscardPileView>();
            ConfigureContainerLabel(visual.Label, "DISCARD");
            visual.ClearFeedback();
            ApplyFixedContainerVisualHide(visual, true);
            view.ConfigureTableRest(true, style.MaximumPileHeight);
            ConfigurePileBay(visual, style, container.OwnerSeatId);
            view.Bind(
                container,
                matchState.ContainerPlacements[containerId],
                coordinateConverter,
                Registry.cardViews);
            ConfigureFixedContainer(visual, view);
            Registry.runtimeDiscardPileInstances.Add(new RuntimeDiscardPileInstance(root, visual, view, containerId));
            return root.transform;
        }

        // Undo and Redo rebuild the table from the restored Match. Template pieces are rebuilt by the session
        // build; this recreates every other placed Deck, Stack and Console (from the Toolbox or a split) and every
        // Discard Pile, template or not, so nothing the Match still holds disappears from the table.
        private void ProjectUnprojectedPlacedComponents()
        {
            ProjectPlacedBoards();
            List<ContainerId> containerIds = new List<ContainerId>(matchState.ContainerPlacements.Keys);
            for (int i = 0; i < containerIds.Count; i++)
            {
                ContainerId containerId = containerIds[i];
                if (!matchState.Containers.TryGetValue(containerId, out ContainerState container)
                    || containerId == handContainerId)
                {
                    continue;
                }

                switch (container.Kind)
                {
                    case ContainerKind.Deck:
                        if (!matchState.IsTemplateContainer(containerId)
                            && !Registry.TryGetDeckPresentation(containerId, out _, out _))
                        {
                            ProjectToolboxDeck(containerId);
                        }

                        break;
                    case ContainerKind.Stack:
                        if (!matchState.IsTemplateContainer(containerId)
                            && !Registry.stackViewsByContainerId.ContainsKey(containerId))
                        {
                            ProjectToolboxStack(containerId, "STACK");
                        }

                        break;
                    case ContainerKind.DiscardPile:
                        if (!Registry.TryGetRuntimeDiscardPile(containerId, out _))
                        {
                            ProjectDiscardPile(containerId);
                        }

                        break;
                }
            }

            foreach (PlacedConsoleState placedConsole in matchState.PlacedConsoles.Values)
            {
                bool projected = false;
                for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
                {
                    if (Registry.runtimeConsoleInstances[i].ConsoleId == placedConsole.Id)
                    {
                        projected = true;
                        break;
                    }
                }

                if (!projected)
                {
                    ProjectToolboxConsole(placedConsole);
                }
            }

            AssignPileBayMouthTargets();
        }

        private void ReleaseRuntimeDiscardPileInstance(int index)
        {
            RuntimeDiscardPileInstance instance = Registry.runtimeDiscardPileInstances[index];
            GameObject root = instance.Root;
            FixedContainerVisual visual = instance.Visual;
            DiscardPileView view = instance.View;
            DisableRuntimeInteraction(root);
            if (visual != null)
            {
                visual.DropTarget.ClearConfiguration();
                visual.DropTarget.enabled = false;
                visual.TargetCollider.enabled = false;
                visual.ClearFeedback();
            }

            if (view != null && view.IsBound)
            {
                view.Unbind();
            }

            Registry.layoutViews.Remove(view);
            feedbackTargetsByContainerId.Remove(instance.ContainerId);
            Registry.runtimeDiscardPileInstances.RemoveAt(index);
            instance.ClearReferences();
            DestroyRuntimeOwnedGameObject(root);
        }

        private void ReleaseRuntimeDiscardPileInstance(ContainerId containerId)
        {
            for (int i = Registry.runtimeDiscardPileInstances.Count - 1; i >= 0; i--)
            {
                if (Registry.runtimeDiscardPileInstances[i].ContainerId == containerId)
                {
                    ReleaseRuntimeDiscardPileInstance(i);
                    return;
                }
            }
        }

        private void ReleaseRuntimeDiscardPileInstances()
        {
            while (Registry.runtimeDiscardPileInstances.Count > 0)
            {
                ReleaseRuntimeDiscardPileInstance(Registry.runtimeDiscardPileInstances.Count - 1);
            }
        }

        // ---------- Empty Table hand (doc 22, H-E) ----------

        internal bool HasLocalHand()
        {
            return handView != null && handView.IsBound && !handContainerId.IsEmpty;
        }

        // The Empty Table's Hand from the catalog Hand entry with the camera tray, when the Hand is on. An Undo
        // that puts cards back into a Hand that is off turns it back on, so cards are never hidden.
        private void BuildEmptyTableHand()
        {
            emptyTableHandContainerId = ContainerId.Empty;
            foreach (ContainerState container in matchState.Containers.Values)
            {
                if (container.Kind == ContainerKind.Hand)
                {
                    emptyTableHandContainerId = container.Id;
                    break;
                }
            }

            if (emptyTableHandContainerId.IsEmpty)
            {
                return;
            }

            ContainerState hand = matchState.GetContainer(emptyTableHandContainerId);
            if (!emptyTableHandEnabled && hand.Count > 0)
            {
                emptyTableHandEnabled = true;
            }

            if (!emptyTableHandEnabled)
            {
                return;
            }

            handContainerId = emptyTableHandContainerId;
            localHandVisual = Spawner.CreateLocalHandVisual();
            handView = localHandVisual.GetView<HandView>();
            handView.ConfigurePresentation(presentationTransitions, localHandVisual.FeedbackRenderer);
            if (handTrayRig == null)
            {
                handTrayRig = HandTrayRig.Create(
                    targetCamera,
                    localHandVisual.TargetCollider.gameObject.layer,
                    localHandVisual.FeedbackRenderer.sharedMaterial);
            }

            handTrayRig.Activate();
            handView.ConfigureTray(handTrayRig);
            handView.Bind(hand, localHandVisual.LayoutAnchor, coordinateConverter, Registry.cardViews);
            ConfigureContainerLabel(localHandVisual.Label, "HAND");
            ConfigureHandTrayDropTarget();
            RefreshCardContentVisibility();
        }

        // The Toolbox footer's Hand switch. Off: the Hand's cards go to the table as one face-down Deck (one Undo
        // step); the table is then rebuilt without the Hand. On: the table is rebuilt with it.
        internal void SetEmptyTableHandEnabled(bool enabled)
        {
            if (!IsInitialized
                || activeSession == null
                || activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom
                || emptyTableHandContainerId.IsEmpty
                || enabled == emptyTableHandEnabled)
            {
                return;
            }

            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                ShowMessage("Finish the current move first.");
                SessionService.ShowActiveSessionUi();
                return;
            }

            ContextMenus.CloseContextMenu();
            componentPlacementController?.Cancel();
            string message = enabled ? "Hand on." : "Hand off.";
            ContainerState hand = matchState.GetContainer(emptyTableHandContainerId);
            if (!enabled && hand.Count > 0)
            {
                int count = hand.Count;
                CollectHandIntoDeckResult result = TableAuthority.Submit<CollectHandIntoDeckRequest, CollectHandIntoDeckResult>(
                    new CollectHandIntoDeckRequest(
                            CreateCommandContext(),
                            emptyTableHandContainerId,
                            CreateFreeToolboxSpawnPose()));
                if (!result.Succeeded)
                {
                    ShowMessage($"Hand off rejected: {result.Error}.");
                    SessionService.ShowActiveSessionUi();
                    return;
                }

                message = count == 1
                    ? "Hand off: your card went back to the table as a face-down deck. Undo brings it back."
                    : $"Hand off: your {count} cards went back to the table as a face-down deck. Undo brings them back.";
            }

            emptyTableHandEnabled = enabled;
            RebuildActiveTablePresentation();
            ShowMessage(message);
            runtimeUi?.OpenComponentToolbox();
        }

        // Rebuilds the table from the current Match, as Undo does, keeping the camera and the Undo history.
        private void RebuildActiveTablePresentation()
        {
            rebuildingFromUndo = true;
            try
            {
                Shutdown(true);
                InitializeActiveSession(false);
                SessionService.ShowActiveSessionUi();
            }
            finally
            {
                rebuildingFromUndo = false;
                SessionService.RefreshUndoUi();
            }
        }

        // Deck right-click "Draw to Hand": a count, then the draw goes from the top of the deck into the local Hand.
        internal void OpenDrawToHandPopup(ContainerId deckId)
        {
            ContextMenus.CloseContextMenu();
            if (!HasLocalHand() || !matchState.Containers.TryGetValue(deckId, out ContainerState deck) || deck.Count == 0)
            {
                ShowMessage("Draw to Hand is unavailable.");
                return;
            }

            int maximum = Math.Max(1, deck.Count);
            selectedQuantity = Mathf.Clamp(selectedQuantity, 1, maximum);
            runtimeUi.ShowQuantityPopup(
                "DRAW TO HAND",
                $"Draw from the top of this {(deck.Kind == ContainerKind.Stack ? "stack" : "deck")} into your hand. {deck.Count} card{(deck.Count == 1 ? string.Empty : "s")} left.",
                "Draw",
                selectedQuantity,
                1,
                maximum,
                () => ChangeSelectedQuantity(-1, maximum),
                () => ChangeSelectedQuantity(1, maximum),
                () =>
                {
                    int count = Mathf.Clamp(selectedQuantity, 1, maximum);
                    runtimeUi?.CloseTabletopPopup();
                    DrawCards(deckId, count);
                },
                ContextMenus.CloseContextMenu);
        }

        private void ProjectCreatedCardBatch(IReadOnlyList<TabletopObjectId> cardIds)
        {
            List<Transform> appearedTransforms = new List<Transform>(cardIds.Count);
            for (int i = 0; i < cardIds.Count; i++)
            {
                CardInstanceState card = matchState.Cards[cardIds[i]];
                CardView view = Spawner.CreateCardView(card, "CARD", out TabletopSelectionVisual selectionVisual);
                Registry.cardViews.Add(view);
                Registry.cardSelectionVisuals.Add(selectionVisual);
                appearedTransforms.Add(view.transform);
            }

            RefreshContainerCardViewSources();
            RefreshSelectionPresenterAfterRuntimeProjection();
            Physics.SyncTransforms();
            for (int i = 0; i < appearedTransforms.Count; i++)
            {
                presentationTransitions.Appear(appearedTransforms[i], settleDuration);
            }

            Physics.SyncTransforms();
        }

        internal void ProjectPopulatedDeckCards(
            ContainerId deckContainerId,
            IReadOnlyList<TabletopObjectId> cardIds)
        {
            for (int i = 0; i < cardIds.Count; i++)
            {
                CardInstanceState card = matchState.Cards[cardIds[i]];
                CardView view = Spawner.CreateCardView(card, "CARD", out TabletopSelectionVisual selectionVisual);
                Registry.cardViews.Add(view);
                Registry.cardSelectionVisuals.Add(selectionVisual);
            }

            RefreshContainerCardViewSources();
            if (!Registry.TryGetDeckPresentation(deckContainerId, out DeckView deck, out _))
            {
                throw new InvalidOperationException("Populated Deck has no Presentation binding.");
            }

            deck.ApplyAcceptedLayout();
            RefreshSelectionPresenterAfterRuntimeProjection();
            RefreshCardContentVisibility();
            Physics.SyncTransforms();
        }

        internal void ProjectDeletedComponent(DeleteTabletopComponentResult result)
        {
            if (result.ComponentKind == TabletopComponentKind.Card
                && inspectedCardId == result.Target.ObjectId)
            {
                CardInspectPanel.CloseCardInspect();
            }

            selectionState.ClearAll();
            switch (result.ComponentKind)
            {
                case TabletopComponentKind.Card:
                    ReleaseRuntimeCardInstance(result.Target.ObjectId);
                    RefreshContainerCardViewSources();
                    if (!result.PreviousContainerId.IsEmpty
                        && matchState.Containers.ContainsKey(result.PreviousContainerId))
                    {
                        ApplyLayout(result.PreviousContainerId);
                    }
                    break;
                case TabletopComponentKind.Pawn:
                    ReleaseRuntimeObjectInstance(
                        result.Target.ObjectId,
                        Registry.runtimePawnInstances,
                        Registry.pawnViews,
                        Registry.pawnSelectionVisuals);
                    break;
                case TabletopComponentKind.Token:
                    ReleaseRuntimeObjectInstance(
                        result.Target.ObjectId,
                        Registry.runtimeTokenInstances,
                        Registry.tokenViews,
                        Registry.tokenSelectionVisuals);
                    if (!result.PreviousContainerId.IsEmpty)
                    {
                        ApplyTokenContainerLayout(result.PreviousContainerId);
                    }
                    break;
                case TabletopComponentKind.Die:
                    ReleaseRuntimeObjectInstance(
                        result.Target.ObjectId,
                        Registry.runtimeDieInstances,
                        Registry.dieViews,
                        Registry.dieSelectionVisuals);
                    break;
                case TabletopComponentKind.Deck:
                    SuspendInteractionDependenciesForRebuild();
                    ReleaseRuntimeDeckInstance(result.Target.ContainerId);
                    ResumeInteractionDependenciesAfterRebuild();
                    break;
                case TabletopComponentKind.DiscardPile:
                    SuspendInteractionDependenciesForRebuild();
                    ReleaseRuntimeDiscardPileInstance(result.Target.ContainerId);
                    ResumeInteractionDependenciesAfterRebuild();
                    break;
                case TabletopComponentKind.Stack:
                    RemoveStackRuntimeView(result.Target.ContainerId);
                    if (primaryStackContainerId == result.Target.ContainerId)
                    {
                        primaryStackContainerId = ContainerId.Empty;
                    }

                    if (sourceFeedbackContainerId == result.Target.ContainerId)
                    {
                        sourceFeedbackContainerId = ContainerId.Empty;
                    }
                    break;
                case TabletopComponentKind.Console:
                    SuspendInteractionDependenciesForRebuild();
                    ReleaseRuntimeConsoleInstance(result.Target.ConsoleId);
                    ResumeInteractionDependenciesAfterRebuild();
                    break;
                default:
                    throw new InvalidOperationException("Deleted Component kind has no Presentation removal path.");
            }

            RefreshSelectionPresenterAfterRuntimeProjection();
            RefreshCardContentVisibility();
            Physics.SyncTransforms();
        }

        private void ApplyTokenContainerLayout(ContainerId containerId)
        {
            for (int i = 0; i < Registry.tokenContainerViews.Count; i++)
            {
                TokenContainerView view = Registry.tokenContainerViews[i];
                if (view != null && view.IsBound && view.ContainerId == containerId)
                {
                    view.ApplyAcceptedLayout();
                    return;
                }
            }
        }

        internal void RefreshContainerCardViewSources()
        {
            for (int i = 0; i < Registry.layoutViews.Count; i++)
            {
                IContainerLayoutView layoutView = Registry.layoutViews[i];
                if (layoutView != null && layoutView.IsBound)
                {
                    layoutView.SetCardViews(Registry.cardViews);
                }
            }
        }

        internal void RefreshSelectionPresenterAfterRuntimeProjection()
        {
            RegisterPhysicalViews();
            inputFrameCoordinator.ClearSelectionPresenter();
            selectionPresenter = new TabletopSelectionPresenter(
                selectionState,
                Registry.cardSelectionVisuals,
                Registry.pawnSelectionVisuals,
                Registry.tokenSelectionVisuals,
                Registry.dieSelectionVisuals);
            inputFrameCoordinator.ConfigureSelectionPresenter(selectionPresenter);
            selectionPresenter.Refresh();
        }

        private void RefreshToolboxPlacementUi()
        {
            if (!toolboxPlacementHintActive)
            {
                return;
            }

            if (componentPlacementController != null && componentPlacementController.IsActive)
            {
                return;
            }

            toolboxPlacementHintActive = false;
            toolboxPlacementSubject = null;
            toolboxPlacementIcon = null;
            runtimeUi?.ClearPlacementHint();
        }

        internal void ShowPlacementHint(string subject)
        {
            toolboxPlacementHintActive = true;
            toolboxPlacementSubject = subject;
            runtimeUi?.ShowPlacementHint(
                subject,
                componentPlacementController?.RotationDegrees ?? 0f,
                toolboxPlacementIcon);
        }

        private void HandlePlacementRotationChanged(float rotationDegrees)
        {
            if (!toolboxPlacementHintActive || string.IsNullOrWhiteSpace(toolboxPlacementSubject))
            {
                return;
            }

            runtimeUi?.ShowPlacementHint(toolboxPlacementSubject, rotationDegrees, toolboxPlacementIcon);
        }
    }
}
