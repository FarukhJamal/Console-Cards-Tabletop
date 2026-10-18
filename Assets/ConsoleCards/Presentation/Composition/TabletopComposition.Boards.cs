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
    /// <summary>Game board mats: placing, moving, joining, resizing and snapping cards to spaces. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        // ---- C3e: game board v2 (doc 22 §15) ----
        // Board mats are placed components (PlacedBoardState) with a grid of card spaces. Cards on a board stay loose
        // table cards: a card that comes to rest on a space is set on its centre (the magnetic grid). Boards join
        // edge to edge when placed or moved next to another. Trap Floor's own board is drawn as a mat too; its grid
        // can be resized as a table setting (kept by Undo, Redo and seat switches) without moving the floor tiles.

        private static GameBoardMatView FindBoardMatPrefab(ComponentLibrary library)
        {
            if (library == null) return null;
            IReadOnlyList<ComponentCatalog> catalogs = library.Catalogs;
            for (int c = 0; c < catalogs.Count; c++)
            {
                IReadOnlyList<ComponentCatalogEntry> entries = catalogs[c].Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    ComponentCatalogEntry entry = entries[i];
                    if (entry == null || entry.Kind != ComponentCatalogKind.GameBoard || entry.Prefab == null) continue;
                    GameBoardMatView view = entry.Prefab.GetComponent<GameBoardMatView>();
                    if (view != null && view.IsComplete) return view;
                }
            }

            return null;
        }

        private GameBoardMatView CreateBoardMatInstance(string name, BoardGrid grid)
        {
            GameBoardMatView view = Instantiate(boardMatPrefab);
            PrepareRuntimeRoot(view.gameObject, name);
            view.Apply(grid);
            return view;
        }

        private void ProjectPlacedBoards()
        {
            if (boardMatPrefab == null || matchState == null) return;
            foreach (PlacedBoardState board in matchState.PlacedBoards.Values)
            {
                if (boardViews.ContainsKey(board.Id)) continue;
                GameBoardMatView view = CreateBoardMatInstance("Board", board.Grid);
                ApplyConsolePose(view.transform, board.Pose, board.SurfaceHeight);
                boardViews.Add(board.Id, view);
            }
        }

        private void ReleaseBoardViews()
        {
            foreach (GameBoardMatView view in boardViews.Values)
            {
                if (view != null) DestroyRuntimeOwnedGameObject(view.gameObject);
            }

            boardViews.Clear();
            if (templateBoardMat != null) DestroyRuntimeOwnedGameObject(templateBoardMat.gameObject);
            templateBoardMat = null;
            cardsSetOnBoardSpaces.Clear();
        }

        // Trap Floor's board drawn as a v2 mat over its play area: the spaces are the floor grid's cells.
        private void ApplyTemplateBoardMat(BoxCollider boardCollider)
        {
            if (boardMatPrefab == null || trapFloorTemplate == null) return;
            GridDefinitionData floor = trapFloorTemplate.GameDefinition.Grid;
            BoardGrid grid = templateBoardGrid ?? new BoardGrid(
                floor.Columns,
                floor.Rows,
                (float)floor.CellWidth,
                (float)floor.CellHeight,
                (float)Math.Max(floor.HorizontalSpacing, 0d),
                BoardGrid.Standard.Frame);
            if (templateBoardMat == null) templateBoardMat = CreateBoardMatInstance("Trap Floor Board Mat", grid);
            else templateBoardMat.Apply(grid);
            templateBoardMat.SetSurfaceEnabled(false); // the board's own surface holds the floor tiles
            Transform board = boardCollider.transform;
            Vector3 top = board.TransformPoint(boardCollider.center + (Vector3.up * (boardCollider.size.y * 0.5f)));
            templateBoardMat.transform.SetPositionAndRotation(top - (Vector3.up * templateBoardMat.Thickness), board.rotation);
            if (gameBoardVisualRoot != null) gameBoardVisualRoot.gameObject.SetActive(false);
        }

        private bool TryFindBoardUnderPointer(Vector2 screenPosition, out BoardId boardId, out GameBoardMatView view)
        {
            boardId = BoardId.Empty;
            view = null;
            if (pointerProjector == null
                || coordinateConverter == null
                || !pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                return false;
            }

            Vector3 world = coordinateConverter.ToWorldPosition(new TabletopPose(coordinate, 0f, 0, 0));
            foreach (KeyValuePair<BoardId, GameBoardMatView> pair in boardViews)
            {
                if (pair.Value != null && pair.Value.gameObject.activeInHierarchy && pair.Value.Contains(world))
                {
                    boardId = pair.Key;
                    view = pair.Value;
                    return true;
                }
            }

            if (templateBoardMat != null && templateBoardMat.gameObject.activeInHierarchy && templateBoardMat.Contains(world))
            {
                view = templateBoardMat;
                return true;
            }

            return false;
        }

        // ---- Placing, moving and joining ----

        private GameObject CreateBoardPlacementPreview(BoardGrid grid)
        {
            GameBoardMatView preview = Instantiate(boardMatPrefab);
            preview.Apply(grid);
            GameObject root = preview.gameObject;
            PrepareRuntimeRoot(root, "Board Placement Preview");
            DisablePlacementPreviewInteraction(root);
            TintPlacementPreview(root);
            return root;
        }

        private void BeginBoardPlacement(BoardGrid grid, bool duplicate, float rotationDegrees)
        {
            EnsureInitialized();
            if (componentPlacementController == null || boardMatPrefab == null)
            {
                ShowMessage("Board placement is not available: run Console Cards > Board > Create Missing Board Mat.");
                return;
            }

            CloseContextMenu();
            GameObject previewRoot = CreateBoardPlacementPreview(grid);
            componentPlacementController.BeginCustomComponentPlacement(
                TabletopComponentKind.GameBoard,
                previewRoot,
                rotationDegrees,
                0,
                0,
                pose => CommitBoardCreate(pose, grid, duplicate));
            componentPlacementController.AdjustPose = pose => JoinBoardPose(pose, grid, BoardId.Empty);
            ShowPlacementHint(duplicate ? "Duplicate Board" : "Board Mat");
        }

        private bool CommitBoardCreate(TabletopPose pose, BoardGrid grid, bool duplicate)
        {
            CreateBoardResult created = TableAuthority.Submit<CreateBoardCommand, CreateBoardResult>(
                new CreateBoardCommand(CreateCommandContext(), pose, grid, null, duplicate));
            CommandResult result = created.Result;
            BoardId boardId = created.BoardId;
            if (!result.Succeeded)
            {
                ShowMessage($"Board rejected: {result.Status}.");
                return false;
            }

            ProjectPlacedBoards();
            if (boardViews.TryGetValue(boardId, out GameBoardMatView view))
            {
                presentationTransitions.Appear(view.transform, settleDuration);
            }

            Physics.SyncTransforms();
            ShowMessage($"Board placed: {grid.Columns} x {grid.Rows} spaces. Right-click it to change the grid.");
            return true;
        }

        private void BeginBoardMove(BoardId boardId)
        {
            EnsureInitialized();
            if (componentPlacementController == null
                || !matchState.PlacedBoards.TryGetValue(boardId, out PlacedBoardState board))
            {
                ShowMessage("Board move rejected: board unavailable.");
                return;
            }

            CloseContextMenu();
            componentPlacementController.Cancel();
            GameObject previewRoot = CreateBoardPlacementPreview(board.Grid);
            BoardGrid grid = board.Grid;
            componentPlacementController.BeginContainerMove(
                previewRoot,
                board.Pose,
                requested => CommitBoardMove(boardId, requested));
            componentPlacementController.AdjustPose = pose => JoinBoardPose(pose, grid, boardId);
            ShowPlacementHint("Move Board");
        }

        private bool CommitBoardMove(BoardId boardId, TabletopPose pose)
        {
            CommandResult result = TableAuthority.Submit<MoveBoardCommand, CommandResult>(
                new MoveBoardCommand(CreateCommandContext(), boardId, pose, null));
            if (!result.Succeeded)
            {
                ShowMessage($"Board move rejected: {result.Status}.");
                return false;
            }

            if (boardViews.TryGetValue(boardId, out GameBoardMatView view))
            {
                ApplyConsolePose(view.transform, matchState.PlacedBoards[boardId].Pose, null);
            }

            Physics.SyncTransforms();
            ShowMessage("Board moved. Cards on it stay where they are.");
            return true;
        }

        // Joins a board to the nearest edge of another board within reach: same turn, edges touching, the two
        // lined up at the centre or at one end. Otherwise the pose is kept.
        private TabletopPose JoinBoardPose(TabletopPose pose, BoardGrid grid, BoardId movingId)
        {
            double bestError = boardJoinDistance;
            TabletopPose best = pose;
            foreach (PlacedBoardState other in matchState.PlacedBoards.Values)
            {
                if (other.Id == movingId) continue;
                TryJoin(pose, grid, other.Pose, other.Grid, ref bestError, ref best);
            }

            if (templateBoardMat != null && templateBoardMat.HasGrid && matchState.PlayAreas.ContainsKey(centralPlayAreaId))
            {
                TabletopPose boardPose = new TabletopPose(matchState.GetPlayArea(centralPlayAreaId).Bounds.Center, 0f, 0, 0);
                TryJoin(pose, grid, boardPose, templateBoardMat.Grid, ref bestError, ref best);
            }

            return best;
        }

        private static void TryJoin(
            TabletopPose pose,
            BoardGrid grid,
            TabletopPose otherPose,
            BoardGrid otherGrid,
            ref double bestError,
            ref TabletopPose best)
        {
            float turn = Mathf.DeltaAngle(otherPose.RotationDegrees, pose.RotationDegrees);
            if (Mathf.Abs(turn) > 12f && Mathf.Abs(Mathf.Abs(turn) - 180f) > 12f) return;
            float yaw = Mathf.Abs(turn) <= 12f ? otherPose.RotationDegrees : otherPose.RotationDegrees + 180f;
            double radians = otherPose.RotationDegrees * (Math.PI / 180d);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double dx = pose.Position.X - otherPose.Position.X;
            double dy = pose.Position.Y - otherPose.Position.Y;
            double localX = (cos * dx) - (sin * dy);
            double localZ = (sin * dx) + (cos * dy);
            double alongX = (otherGrid.Width + grid.Width) * 0.5d;
            double alongZ = (otherGrid.Depth + grid.Depth) * 0.5d;
            double shiftX = (otherGrid.Width - grid.Width) * 0.5d;
            double shiftZ = (otherGrid.Depth - grid.Depth) * 0.5d;
            for (int side = 0; side < 4; side++)
            {
                for (int line = -1; line <= 1; line++)
                {
                    double targetX;
                    double targetZ;
                    if (side < 2)
                    {
                        targetX = side == 0 ? alongX : -alongX;
                        targetZ = line * shiftZ;
                    }
                    else
                    {
                        targetZ = side == 2 ? alongZ : -alongZ;
                        targetX = line * shiftX;
                    }

                    double ex = localX - targetX;
                    double ez = localZ - targetZ;
                    double error = Math.Sqrt((ex * ex) + (ez * ez));
                    if (error >= bestError) continue;
                    bestError = error;
                    best = new TabletopPose(
                        new TableCoordinate(
                            otherPose.Position.X + (cos * targetX) + (sin * targetZ),
                            otherPose.Position.Y - (sin * targetX) + (cos * targetZ)),
                        yaw,
                        pose.Layer,
                        pose.LocalOrder);
                }
            }
        }

        // ---- The board menu (right-click a board) ----

        private void OpenBoardContextMenu(Vector2 screenPosition, BoardId boardId)
        {
            CloseContextMenu();
            boardMenuScreenPosition = screenPosition;
            ShowBoardContextMenu(boardId);
        }

        private void ShowBoardContextMenu(BoardId boardId)
        {
            if (runtimeUi == null || !TryGetBoardGrid(boardId, out BoardGrid grid)) return;
            bool placed = !boardId.IsEmpty;
            bool cardSpaces = grid.SpaceWidth >= CardSpaceWidth - 0.01f;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (placed)
            {
                actions.Add(new PopupActionOption("Move", true, () => BeginBoardMove(boardId)));
            }

            actions.Add(new PopupActionOption("Columns +", grid.Columns < BoardGrid.MaximumColumns,
                () => ResizeBoard(boardId, grid.WithSize(grid.Columns + 1, grid.Rows))));
            actions.Add(new PopupActionOption("Columns -", grid.Columns > 1,
                () => ResizeBoard(boardId, grid.WithSize(grid.Columns - 1, grid.Rows))));
            actions.Add(new PopupActionOption("Rows +", grid.Rows < BoardGrid.MaximumRows,
                () => ResizeBoard(boardId, grid.WithSize(grid.Columns, grid.Rows + 1))));
            actions.Add(new PopupActionOption("Rows -", grid.Rows > 1,
                () => ResizeBoard(boardId, grid.WithSize(grid.Columns, grid.Rows - 1))));
            actions.Add(new PopupActionOption(
                cardSpaces ? "Spaces: small tiles" : "Spaces: card size",
                true,
                () => ResizeBoard(boardId, cardSpaces
                    ? grid.WithSpace(SmallSpaceWidth, SmallSpaceDepth, 0f)
                    : grid.WithSpace(CardSpaceWidth, CardSpaceDepth, CardSpaceGap))));
            if (placed)
            {
                actions.Add(new PopupActionOption("Duplicate", true, () =>
                    BeginBoardPlacement(grid, true, matchState.PlacedBoards[boardId].Pose.RotationDegrees)));
                actions.Add(new PopupActionOption("Delete", true, () => DeleteBoard(boardId)));
            }

            runtimeUi.ShowContextMenu(
                boardMenuScreenPosition,
                placed ? "BOARD" : "GAME BOARD",
                $"{grid.Columns} x {grid.Rows} spaces ({(cardSpaces ? "card size" : "small tiles")})."
                    + (placed ? " Cards on it stay where they are." : " The floor tiles do not move."),
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private bool TryGetBoardGrid(BoardId boardId, out BoardGrid grid)
        {
            if (!boardId.IsEmpty && matchState.PlacedBoards.TryGetValue(boardId, out PlacedBoardState board))
            {
                grid = board.Grid;
                return true;
            }

            if (boardId.IsEmpty && templateBoardMat != null && templateBoardMat.HasGrid)
            {
                grid = templateBoardMat.Grid;
                return true;
            }

            grid = default;
            return false;
        }

        private void ResizeBoard(BoardId boardId, BoardGrid grid)
        {
            if (boardId.IsEmpty)
            {
                templateBoardGrid = grid; // a table setting for the game's own board
                templateBoardMat?.Apply(grid);
                templateBoardMat?.SetSurfaceEnabled(false);
            }
            else
            {
                CommandResult result = TableAuthority.Submit<SetBoardGridCommand, CommandResult>(
                    new SetBoardGridCommand(CreateCommandContext(), boardId, grid));
                if (!result.Succeeded)
                {
                    ShowMessage($"Grid change rejected: {result.Status}.");
                    return;
                }

                if (boardViews.TryGetValue(boardId, out GameBoardMatView view))
                {
                    view.Apply(grid);
                    ApplyConsolePose(view.transform, matchState.PlacedBoards[boardId].Pose, null);
                }

                Physics.SyncTransforms();
            }

            ShowBoardContextMenu(boardId); // stays open for the next + or −
        }

        private void DeleteBoard(BoardId boardId)
        {
            CommandResult result = TableAuthority.Submit<DeleteBoardCommand, CommandResult>(
                new DeleteBoardCommand(CreateCommandContext(), boardId));
            CloseContextMenu();
            if (!result.Succeeded)
            {
                ShowMessage($"Delete rejected: {result.Status}.");
                return;
            }

            if (boardViews.TryGetValue(boardId, out GameBoardMatView view))
            {
                if (view != null) DestroyRuntimeOwnedGameObject(view.gameObject);
                boardViews.Remove(boardId);
            }

            Physics.SyncTransforms();
            ShowMessage("Board removed. Cards that were on it stay on the table.");
        }

        // ---- The magnetic grid ----

        // A loose card that comes to rest on a board space is set on the space's centre, turned square to the board.
        private void CompleteBoardCardSnaps()
        {
            if (!boardSnapCheckPending || !IsInitialized || matchState == null) return;
            boardSnapCheckPending = false;
            if (boardViews.Count == 0 && templateBoardMat == null) return;
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView card = cardViews[i];
                if (card == null || !card.IsBound || card.BoundState == null) continue;
                TabletopObjectState state = card.BoundState;
                if (!state.ContainerId.IsEmpty || state.IsUserLocked) continue;
                if (trapFloorTemplate != null && trapFloorTemplate.IsFloorCard(state.Id)) continue; // floor tiles never move
                if (card.PhysicalObject != null
                    && (card.PhysicalObject.IsHeld
                        || state.PhysicalState == null
                        || state.PhysicalState.Mode != PhysicalObjectMode.Sleeping))
                {
                    continue;
                }

                Vector3 position = card.PhysicalObject != null && card.PhysicalObject.Body != null
                    ? card.PhysicalObject.Body.position
                    : card.transform.position;
                if (!TryFindBoardSpace(position, out GameBoardMatView board, out Vector3 centre)) continue;
                float dx = centre.x - position.x;
                float dz = centre.z - position.z;
                if ((dx * dx) + (dz * dz) <= BoardSnapTolerance * BoardSnapTolerance) continue;
                if (cardsSetOnBoardSpaces.TryGetValue(state.Id, out Vector3 last)
                    && (last - centre).sqrMagnitude < 0.0001f)
                {
                    continue; // already set here once; it settled a little off, leave it
                }

                if (SetCardOnSpace(card, board, centre)) cardsSetOnBoardSpaces[state.Id] = centre;
            }
        }

        private bool TryFindBoardSpace(Vector3 position, out GameBoardMatView board, out Vector3 centre)
        {
            foreach (GameBoardMatView view in boardViews.Values)
            {
                if (view != null && view.gameObject.activeInHierarchy && view.TryNearestSpace(position, out centre))
                {
                    board = view;
                    return true;
                }
            }

            if (templateBoardMat != null && templateBoardMat.gameObject.activeInHierarchy
                && templateBoardMat.TryNearestSpace(position, out centre))
            {
                board = templateBoardMat;
                return true;
            }

            board = null;
            centre = Vector3.zero;
            return false;
        }

        private bool SetCardOnSpace(CardView card, GameBoardMatView board, Vector3 centre)
        {
            TabletopObjectState state = card.BoundState;
            TableCoordinate coordinate = coordinateConverter.ToTableCoordinate(centre);
            if (card.PhysicalObject == null)
            {
                float yaw = state.Pose.RotationDegrees;
                float boardYaw = board.transform.eulerAngles.y;
                float square = boardYaw + (Mathf.Round(Mathf.DeltaAngle(boardYaw, yaw) / 90f) * 90f);
                return TableAuthority.Submit<MoveObjectCommand, MoveObjectResult>(new MoveObjectCommand(
                        CreateCommandContext(),
                        state.Id,
                        new TabletopPose(coordinate, square, state.Pose.Layer, state.Pose.LocalOrder))).Succeeded
                    && ApplyAcceptedCard(card);
            }

            if (physicalAuthority == null || activeSession == null) return false;
            if (!physicalAuthority.Surfaces.TryResolveAuthoredLooseObject(
                    new TabletopPose(coordinate, 0f, 0, 0),
                    localPlayerId,
                    card.PhysicalObject.PhysicalCollider,
                    false,
                    out PhysicalObjectState placed))
            {
                return false;
            }

            Quaternion current = card.PhysicalObject.Body != null ? card.PhysicalObject.Body.rotation : card.transform.rotation;
            Vector3 forward = Vector3.ProjectOnPlane(current * Vector3.forward, Vector3.up);
            Vector3 boardForward = Vector3.ProjectOnPlane(board.transform.forward, Vector3.up);
            float angle = forward.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(boardForward, forward, Vector3.up) : 0f;
            float correction = (Mathf.Round(angle / 90f) * 90f) - angle;
            Quaternion squared = Quaternion.AngleAxis(correction, Vector3.up) * current;
            placed = placed.WithRotation(new PhysicalRotation(squared.x, squared.y, squared.z, squared.w));
            CommandResult result = TableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(
                new CommitPhysicalObjectCommand(
                    CreateCommandContext(),
                    state.Id,
                    placed,
                    state.PhysicalRevision,
                    null,
                    AuthoritativeActionRecordMode.None));
            return result.Succeeded && ApplyAcceptedCard(card);
        }

        private static bool ApplyAcceptedCard(CardView card)
        {
            card.ApplyAcceptedState();
            return true;
        }
    }
}
