using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Views.Containers;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// R2 (doc 30 Part F): the table's views follow accepted changes. The table authority announces every accepted
    /// command with its result (<c>ITableAuthority.Observe</c>), whoever asked for it; this class brings the views in
    /// line with the new table state. The code that asked for a change keeps only what belongs to the person who did
    /// it: the animation from where things were, the pulse, the appear effect and the message.
    /// <list type="bullet">
    /// <item><b>Piles</b> (R2a): shuffle, draw, reorder, split and merge lay the touched piles out again; a split's
    /// new stack gets its view and a merged stack loses its view.</item>
    /// <item><b>What exists</b> (R2b): Toolbox create, duplicate, populate and delete, card batches, pile
    /// duplicates, boards (place, move, grid, remove), moved piles and Consoles, and the cards the Trap Floor Shop and
    /// prizes hand out get, change or lose their views here.</item>
    /// <item><b>Gestures</b> (R2b): a card or token carried, a piece moved, turned or flipped, a physics rest and a
    /// Console turned over are shown by the gesture or animation on the computer where they happen. When the player
    /// who did it plays on another computer (multiplayer, later), the view is set from the accepted state here.
    /// Today every seat plays on this computer, so this part waits for multiplayer.</item>
    /// <item><b>Trap Floor's own moments</b> (reveals, collapse, Keys, escape) stay with the cartridge, which animates
    /// them; they follow the cartridge contract (CART-1).</item>
    /// </list>
    /// Runs inside Submit, before it returns; nothing here allocates per frame.
    /// </summary>
    internal sealed class TableViewSync
    {
        private readonly TabletopComposition host;

        internal TableViewSync(TabletopComposition host)
        {
            this.host = host;
        }

        // Called once, when the table's command door is made.
        internal void Attach(ITableAuthority authority)
        {
            // Piles (R2a).
            authority.Observe<ShuffleDeckCommand, ShuffleDeckResult>(OnShuffled);
            authority.Observe<DrawCardsCommand, DrawCardsResult>(OnDrawn);
            authority.Observe<ReorderContainerCommand, ReorderContainerResult>(OnReordered);
            authority.Observe<SplitStackCommand, SplitStackResult>(OnSplit);
            authority.Observe<MergeStacksCommand, MergeStacksResult>(OnMerged);

            // What exists on the table (R2b).
            authority.Observe<CreateTabletopComponentRequest, CreateTabletopComponentResult>(OnCreated);
            authority.Observe<DuplicatePileRequest, CreateTabletopComponentResult>(OnPileDuplicated);
            authority.Observe<DuplicateTabletopComponentRequest, DuplicateTabletopComponentResult>(OnDuplicated);
            authority.Observe<CreateGenericCardBatchRequest, CreateGenericCardBatchResult>(OnCardBatchCreated);
            authority.Observe<PopulateDeckRequest, PopulateDeckResult>(OnDeckPopulated);
            authority.Observe<DeleteTabletopComponentRequest, DeleteTabletopComponentResult>(OnDeleted);
            authority.Observe<MoveContainerCommand, MoveContainerResult>(OnContainerMoved);
            authority.Observe<CreateBoardCommand, CreateBoardResult>(OnBoardCreated);
            authority.Observe<MoveBoardCommand, CommandResult>(OnBoardMoved);
            authority.Observe<SetBoardGridCommand, CommandResult>(OnBoardGridSet);
            authority.Observe<DeleteBoardCommand, CommandResult>(OnBoardDeleted);
            authority.Observe<TrapFloorBuyFromShopCommand, TrapFloorBuyFromShopResult>(OnShopCardBought);
            authority.Observe<TrapFloorTakePrizeCommand, TrapFloorTakePrizeResult>(OnPrizeTaken);

            // Gestures: shown here only when the player plays on another computer (R2b).
            authority.Observe<TransferCardCommand, TransferCardResult>(OnCardTransferred);
            authority.Observe<TransferTokenCommand, TransferTokenResult>(OnTokenTransferred);
            authority.Observe<MoveObjectCommand, MoveObjectResult>(OnObjectMoved);
            authority.Observe<RotateObjectCommand, RotateObjectResult>(OnObjectRotated);
            authority.Observe<FlipCardCommand, FlipCardResult>(OnCardFlipped);
            authority.Observe<CommitPhysicalObjectCommand, CommandResult>(OnPhysicalObjectCommitted);
            authority.Observe<FlipConsoleCommand, CommandResult>(OnConsoleFlipped);
        }

        private bool Ready => host.IsInitialized && host.matchState != null;

        // ---------- Piles ----------

        private void OnShuffled(ShuffleDeckCommand command, ShuffleDeckResult result)
        {
            if (!Ready) return;
            SyncContainer(command.DeckContainerId);
        }

        private void OnDrawn(DrawCardsCommand command, DrawCardsResult result)
        {
            if (!Ready) return;
            SyncContainer(command.SourceDeckContainerId);
            SyncContainer(command.DestinationContainerId);
        }

        private void OnReordered(ReorderContainerCommand command, ReorderContainerResult result)
        {
            if (!Ready) return;
            SyncContainer(command.ContainerId);
        }

        private void OnSplit(SplitStackCommand command, SplitStackResult result)
        {
            if (!Ready) return;
            EnsureStackView(command.NewStackContainerId);
            SyncContainer(command.SourceStackContainerId);
            SyncContainer(command.NewStackContainerId);
        }

        private void OnMerged(MergeStacksCommand command, MergeStacksResult result)
        {
            if (!Ready) return;
            RemoveEmptiedStackView(command.SourceStackContainerId);
            SyncContainer(command.DestinationStackContainerId);
        }

        // ---------- What exists ----------

        private void OnCreated(CreateTabletopComponentRequest command, CreateTabletopComponentResult result)
        {
            if (!Ready) return;
            host.PresentCreatedComponent(result);
        }

        private void OnPileDuplicated(DuplicatePileRequest command, CreateTabletopComponentResult result)
        {
            if (!Ready) return;
            host.PresentCreatedComponent(result);
        }

        private void OnDuplicated(DuplicateTabletopComponentRequest command, DuplicateTabletopComponentResult result)
        {
            if (!Ready) return;
            host.PresentCreatedComponent(result.CreationResult);
        }

        private void OnCardBatchCreated(CreateGenericCardBatchRequest command, CreateGenericCardBatchResult result)
        {
            if (!Ready) return;
            host.PresentCreatedCardBatch(result.CardIds);
        }

        private void OnDeckPopulated(PopulateDeckRequest command, PopulateDeckResult result)
        {
            if (!Ready) return;
            host.ProjectPopulatedDeckCards(command.DeckContainerId, result.CardIds);
        }

        private void OnDeleted(DeleteTabletopComponentRequest command, DeleteTabletopComponentResult result)
        {
            if (!Ready) return;
            host.ProjectDeletedComponent(result);
        }

        private void OnContainerMoved(MoveContainerCommand command, MoveContainerResult result)
        {
            if (!Ready) return;
            host.PresentMovedContainer(command.ContainerId);
        }

        private void OnBoardCreated(CreateBoardCommand command, CreateBoardResult result)
        {
            if (!Ready) return;
            host.PresentPlacedBoards();
        }

        private void OnBoardMoved(MoveBoardCommand command, CommandResult result)
        {
            if (!Ready) return;
            host.PresentBoardPose(command.BoardId, false);
        }

        private void OnBoardGridSet(SetBoardGridCommand command, CommandResult result)
        {
            if (!Ready) return;
            host.PresentBoardPose(command.BoardId, true);
        }

        private void OnBoardDeleted(DeleteBoardCommand command, CommandResult result)
        {
            if (!Ready) return;
            host.PresentDeletedBoard(command.BoardId);
        }

        private void OnShopCardBought(TrapFloorBuyFromShopCommand command, TrapFloorBuyFromShopResult result)
        {
            if (!Ready) return;
            host.TrapFloorGame.PresentGrantedCard(result.GrantedCardId, result.Card.DisplayName, result.DestinationContainerId);
        }

        private void OnPrizeTaken(TrapFloorTakePrizeCommand command, TrapFloorTakePrizeResult result)
        {
            if (!Ready) return;
            host.TrapFloorGame.PresentGrantedCard(
                result.GrantedCardId,
                result.FloorCard.Content.DisplayName,
                result.DestinationContainerId);
        }

        // ---------- Gestures (another computer's player only) ----------

        private bool ShownByGesture(CommandContext context)
        {
            return host.IsPlayerOnThisComputer(context.RequestedByPlayerId);
        }

        private void OnCardTransferred(TransferCardCommand command, TransferCardResult result)
        {
            if (!Ready || ShownByGesture(command.Context)) return;
            SyncContainer(command.ExpectedSourceContainerId);
            SyncContainer(command.DestinationContainerId);
            if (command.DestinationContainerId.IsEmpty) host.PresentAcceptedObject(command.CardObjectId);
            host.RefreshAcceptedCardFaces();
        }

        private void OnTokenTransferred(TransferTokenCommand command, TransferTokenResult result)
        {
            if (!Ready || ShownByGesture(command.Context)) return;
            host.PresentTokenContainer(command.ExpectedSourceContainerId);
            host.PresentTokenContainer(command.DestinationContainerId);
            if (command.DestinationContainerId.IsEmpty) host.PresentAcceptedObject(command.TokenObjectId);
        }

        private void OnObjectMoved(MoveObjectCommand command, MoveObjectResult result)
        {
            if (!Ready || ShownByGesture(command.Context)) return;
            host.PresentAcceptedObject(command.ObjectId);
        }

        private void OnObjectRotated(RotateObjectCommand command, RotateObjectResult result)
        {
            if (!Ready || ShownByGesture(command.Context)) return;
            host.PresentAcceptedObject(command.ObjectId);
        }

        private void OnCardFlipped(FlipCardCommand command, FlipCardResult result)
        {
            if (!Ready || ShownByGesture(command.Context)) return;
            host.PresentAcceptedObject(command.ObjectId);
            host.RefreshAcceptedCardFaces();
        }

        private void OnPhysicalObjectCommitted(CommitPhysicalObjectCommand command, CommandResult result)
        {
            if (!Ready || ShownByGesture(command.Context)) return;
            host.PresentAcceptedObject(command.ObjectId);
        }

        private void OnConsoleFlipped(FlipConsoleCommand command, CommandResult result)
        {
            if (!Ready || ShownByGesture(command.Context)) return;
            host.PresentConsoleSide(command.SlotContainerId);
        }

        // ---------- Shared ----------

        // Lays out the cards of one container from the accepted state: a deck through its deck view, every other
        // pile, hand and slot through the table's layout (which also refreshes what each card shows).
        internal void SyncContainer(ContainerId containerId)
        {
            if (containerId.IsEmpty || host.matchState == null || !host.matchState.Containers.ContainsKey(containerId))
            {
                return;
            }

            if (host.Registry.TryGetDeckPresentation(containerId, out DeckView deckView, out _) && deckView != null)
            {
                deckView.ApplyAcceptedLayout();
                host.RefreshCardContentVisibility();
                return;
            }

            host.ApplyLayout(containerId);
        }

        // A stack the change made (a split) gets its view, once.
        private void EnsureStackView(ContainerId containerId)
        {
            if (containerId.IsEmpty
                || host.Registry.stackViewsByContainerId.ContainsKey(containerId)
                || !host.matchState.Containers.TryGetValue(containerId, out ContainerState container)
                || container.Kind != ContainerKind.Stack
                || !host.matchState.ContainerPlacements.TryGetValue(containerId, out ContainerPlacementState placement))
            {
                return;
            }

            StackRuntimeView view = host.Spawner.CreateStackRuntimeView(
                $"Stack {host.Registry.stackViewsByContainerId.Count + 1}",
                container,
                placement,
                true);
            host.Registry.stackViewsByContainerId.Add(containerId, view);
            host.RebuildLayoutLookupAndRouter();
        }

        // A stack the change removed from the table (merged into another) loses its view.
        private void RemoveEmptiedStackView(ContainerId containerId)
        {
            if (containerId.IsEmpty || host.matchState.Containers.ContainsKey(containerId))
            {
                return;
            }

            host.RemoveStackRuntimeView(containerId);
        }
    }
}
