using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Authority;
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
    /// <summary>
    /// The command door (R1b, doc 30 A6 gap 1): every change the table code makes goes through
    /// <see cref="TableAuthority"/> as a command. Here, the local authority is made once and each command type is
    /// given its use case. The handlers read the session's current fields (match, players, random source, surface
    /// query) when they run, so a new table needs no new set-up. Later a network authority can take its place.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private LocalTableAuthority tableAuthority;

        // Use cases that keep no state are made once: drags and physics submit often, and a new object per command
        // would be garbage every time a piece moves.
        private readonly MoveObjectUseCase moveObjectUseCase = new MoveObjectUseCase();
        private readonly CommitPhysicalObjectUseCase commitPhysicalObjectUseCase = new CommitPhysicalObjectUseCase();
        private readonly TransferCardUseCase transferCardUseCase = new TransferCardUseCase();
        private readonly TransferTokenUseCase transferTokenUseCase = new TransferTokenUseCase();
        private readonly RotateObjectUseCase rotateObjectUseCase = new RotateObjectUseCase();
        private readonly FlipCardUseCase flipCardUseCase = new FlipCardUseCase();

        internal ITableAuthority TableAuthority
        {
            get
            {
                if (tableAuthority == null) tableAuthority = CreateTableAuthority();
                return tableAuthority;
            }
        }

        private LocalTableAuthority CreateTableAuthority()
        {
            LocalTableAuthority authority = new LocalTableAuthority();

            // Pieces and physics: drags, drops, turns, flips and physics commits (R1b part 2).
            authority.Register<MoveObjectCommand, MoveObjectResult>(
                command => moveObjectUseCase.Execute(matchState, command));
            authority.Register<CommitPhysicalObjectCommand, CommandResult>(
                command => commitPhysicalObjectUseCase.Execute(matchState, activeSession.Request.ActivePlayerIds, command));
            authority.Register<TransferCardCommand, TransferCardResult>(
                command => transferCardUseCase.Execute(matchState, command));
            authority.Register<TransferTokenCommand, TransferTokenResult>(
                command => transferTokenUseCase.Execute(matchState, command));
            authority.Register<RotateObjectCommand, RotateObjectResult>(
                command => rotateObjectUseCase.Execute(matchState, command));
            authority.Register<FlipCardCommand, FlipCardResult>(
                command => flipCardUseCase.Execute(matchState, command));
            authority.Register<MoveContainerCommand, MoveContainerResult>(
                command => new MoveContainerUseCase(physicalSurfaceQuery.ResolveContainerSurfaceHeight).Execute(matchState, command));
            authority.Register<FlipConsoleCommand, CommandResult>(
                command => new FlipConsoleUseCase().Execute(matchState, command));

            // Decks, stacks and hands.
            authority.Register<ShuffleDeckCommand, ShuffleDeckResult>(
                command => new ShuffleDeckUseCase(authoritativeRandomValueSource).Execute(matchState, command));
            authority.Register<DrawCardsCommand, DrawCardsResult>(
                command => new DrawCardsUseCase().Execute(matchState, command));
            authority.Register<SplitStackCommand, SplitStackResult>(
                command => new SplitStackUseCase().Execute(matchState, command));
            authority.Register<ReorderContainerCommand, ReorderContainerResult>(
                command => new ReorderContainerUseCase().Execute(matchState, command));
            authority.Register<MergeStacksCommand, MergeStacksResult>(
                command => new MergeStacksUseCase().Execute(matchState, command));
            authority.Register<CollectHandIntoDeckRequest, CollectHandIntoDeckResult>(
                command => new CollectHandIntoDeckUseCase(componentIdentitySource, physicalSurfaceQuery.ResolveContainerSurfaceHeight)
                    .Execute(matchState, command));

            // The Toolbox: create, duplicate, populate and delete.
            authority.Register<CreateTabletopComponentRequest, CreateTabletopComponentResult>(
                command => componentCreationUseCase.Execute(matchState, activeSession.Request.ActivePlayerIds, command));
            authority.Register<CreateGenericCardBatchRequest, CreateGenericCardBatchResult>(
                command => cardBatchCreationUseCase.Execute(matchState, activeSession.Request.ActivePlayerIds, command));
            authority.Register<DuplicateTabletopComponentRequest, DuplicateTabletopComponentResult>(
                command => componentDuplicationUseCase.Execute(matchState, activeSession.Request.ActivePlayerIds, command));
            authority.Register<PopulateDeckRequest, PopulateDeckResult>(
                command => populateDeckUseCase.Execute(matchState, activeSession.Request.ActivePlayerIds, command));
            authority.Register<DuplicatePileRequest, CreateTabletopComponentResult>(
                command => componentCreationUseCase.DuplicatePile(matchState, activeSession.Request.ActivePlayerIds, command));
            authority.Register<DeleteTabletopComponentRequest, DeleteTabletopComponentResult>(
                command => componentDeletionUseCase.Execute(matchState, activeSession.Request.ActivePlayerIds, command));

            // Game board mats.
            authority.Register<CreateBoardCommand, CreateBoardResult>(CreateBoard);
            authority.Register<MoveBoardCommand, CommandResult>(
                command => boardUseCases.Move(matchState, command.Context, command.BoardId, command.Pose, command.SurfaceHeight));
            authority.Register<SetBoardGridCommand, CommandResult>(
                command => boardUseCases.SetGrid(matchState, command.Context, command.BoardId, command.Grid));
            authority.Register<DeleteBoardCommand, CommandResult>(
                command => boardUseCases.Delete(matchState, command.Context, command.BoardId));

            // Trap Floor.
            authority.Register<TrapFloorBeginCollapseCommand, TrapFloorCollapseResult>(
                command => trapFloorCollapseUseCase.Begin(matchState, command));
            authority.Register<TrapFloorResolveCollapseCommand, TrapFloorCollapseResult>(
                command => trapFloorCollapseUseCase.ResolveSettled(matchState, command));
            authority.Register<TrapFloorRevealFloorCommand, TrapFloorRevealFloorResult>(
                command => trapFloorRevealFloorUseCase.Execute(matchState, command));
            authority.Register<TrapFloorClaimKeyCommand, TrapFloorObjectiveResult>(
                command => trapFloorObjectiveUseCase.ClaimKey(matchState, command));
            authority.Register<TrapFloorAttemptEscapeCommand, TrapFloorObjectiveResult>(
                command => trapFloorObjectiveUseCase.AttemptEscape(matchState, command));
            authority.Register<TrapFloorTakePrizeCommand, TrapFloorTakePrizeResult>(
                command => trapFloorTakePrizeUseCase.Execute(matchState, command));
            authority.Register<TrapFloorBuyFromShopCommand, TrapFloorBuyFromShopResult>(
                command => trapFloorShopUseCase.Execute(matchState, command));
            return authority;
        }

        private CreateBoardResult CreateBoard(CreateBoardCommand command)
        {
            CommandResult result = boardUseCases.Create(
                matchState,
                command.Context,
                command.Pose,
                command.Grid,
                command.SurfaceHeight,
                command.Duplicate,
                out BoardId boardId);
            return new CreateBoardResult(result, boardId);
        }
    }
}
