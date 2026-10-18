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
    /// <summary>
    /// R2: the view work <see cref="TableViewSync"/> asks for when a change is accepted. It touches views of every
    /// kind (cards, piles, boards, Consoles), so it lives with the composition; the sync decides when.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        // One computer: every seat plays here (hot-seat). With multiplayer this becomes "the seats this computer
        // plays"; the view sync then shows other computers' gestures from the accepted state.
        internal bool IsPlayerOnThisComputer(PlayerId playerId)
        {
            return true;
        }

        // A Toolbox piece, deck, stack, discard pile or Console was made (placed, duplicated, a card-on-card stack,
        // a pile duplicate). Card views come first: a new pile view binds every member card.
        internal void PresentCreatedComponent(CreateTabletopComponentResult result)
        {
            bool madeCards = false;
            for (int i = 0; i < result.CardIds.Count; i++)
            {
                TabletopObjectId cardId = result.CardIds[i];
                if (Registry.TryGetCardView(cardId, out _) || !matchState.Cards.TryGetValue(cardId, out CardInstanceState card))
                {
                    continue;
                }

                CardView view = Spawner.CreateCardView(card, ResolveCardLabel(card), out TabletopSelectionVisual selectionVisual);
                Registry.cardViews.Add(view);
                Registry.cardSelectionVisuals.Add(selectionVisual);
                madeCards = true;
            }

            if (madeCards) RefreshContainerCardViewSources();
            ProjectCreatedToolboxComponent(result);
            if (madeCards) RefreshContainerCardViewSources();
            RefreshCardContentVisibility();
            Physics.SyncTransforms();
        }

        internal void PresentCreatedCardBatch(IReadOnlyList<TabletopObjectId> cardIds)
        {
            ProjectCreatedCardBatch(cardIds);
        }

        // A deck, stack, discard pile or Console moved (Move, Table mode, a carried stack).
        internal void PresentMovedContainer(ContainerId containerId)
        {
            if (matchState.Containers.TryGetValue(containerId, out ContainerState container)
                && container.Kind == ContainerKind.ConsoleSlot)
            {
                ApplyConsolePlacement(containerId);
            }
            else
            {
                ViewSync.SyncContainer(containerId);
            }

            Physics.SyncTransforms();
        }

        internal void PresentPlacedBoards()
        {
            ProjectPlacedBoards();
            Physics.SyncTransforms();
        }

        // A board mat moved, or its grid changed (regrid: the mat is redrawn first).
        internal void PresentBoardPose(BoardId boardId, bool regrid)
        {
            if (!boardViews.TryGetValue(boardId, out GameBoardMatView view) || view == null
                || !matchState.PlacedBoards.TryGetValue(boardId, out PlacedBoardState board))
            {
                return;
            }

            if (regrid) view.Apply(board.Grid);
            ApplyConsolePose(view.transform, board.Pose, null);
            Physics.SyncTransforms();
        }

        internal void PresentDeletedBoard(BoardId boardId)
        {
            if (!boardViews.TryGetValue(boardId, out GameBoardMatView view)) return;
            if (view != null) DestroyRuntimeOwnedGameObject(view.gameObject);
            boardViews.Remove(boardId);
            Physics.SyncTransforms();
        }

        // A loose piece's view set from its accepted state (another computer's gesture).
        internal void PresentAcceptedObject(TabletopObjectId objectId)
        {
            if (objectId.IsEmpty) return;
            if (Registry.TryGetCardView(objectId, out CardView card)) card.ApplyAcceptedState();
            else if (Registry.TryGetPawnView(objectId, out PawnView pawn)) pawn.ApplyAcceptedState();
            else if (Registry.TryGetTokenView(objectId, out TokenView token)) token.ApplyAcceptedState();
            else if (Registry.TryGetDieView(objectId, out DieView die)) die.ApplyAcceptedState();
        }

        internal void PresentTokenContainer(ContainerId containerId)
        {
            if (!containerId.IsEmpty) ApplyTokenContainerLayout(containerId);
        }

        internal void PresentConsoleSide(ContainerId slotContainerId)
        {
            RuntimeConsoleInstance instance = FindConsoleInstanceBySlot(slotContainerId);
            if (instance != null) ApplyConsoleSide(instance);
        }
    }
}
