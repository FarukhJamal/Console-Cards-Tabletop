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
            Spawner.PrepareRuntimeRoot(view.gameObject, name);
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

        // A game's board (Trap Floor's floor) drawn as a v2 mat over its play area: the spaces are the game grid's
        // cells (CART-1b: the cartridge gives the grid).
        private void ApplyTemplateBoardMat(BoxCollider boardCollider)
        {
            if (boardMatPrefab == null || !ActiveCartridge.TryGetBoardGrid(out GridDefinitionData floor)) return;
            // MAT-2 (owner, 2026-10-22: "board 6x6 visually and cards on 5x5"): the board keeps the first floor's spaces;
            // a smaller floor's tiles lie on them from the first row and column. BOARD-1 (owner, 2026-10-23): the spaces
            // fill the game's playmat like the printed board (Trap Floor: 10 x 6), and the board menu's Columns and
            // Rows (+ / -) set them exactly; the floor's tiles always land on spaces.
            int removed = Math.Max(0, ActiveCartridge.BoardRemovedSpaces);
            int floorColumns = floor.Columns + removed;
            int floorRows = floor.Rows + removed;
            Vector2 playMat = ActiveCartridge.BoardMatSize;
            float spaceWidth = (float)floor.CellWidth;
            float spaceDepth = (float)floor.CellHeight;
            float gap = (float)Math.Max(floor.HorizontalSpacing, 0d);
            BoardGrid grid = templateBoardGrid ?? new BoardGrid(
                playMat.x > 0f ? SpacesFitting(playMat.x, spaceWidth, gap, floorColumns) : floorColumns,
                playMat.y > 0f ? SpacesFitting(playMat.y, spaceDepth, gap, floorRows) : floorRows,
                spaceWidth,
                spaceDepth,
                gap,
                playMat.x > 0f ? PlayMatSpaceFrame : BoardGrid.Standard.Frame);
            if (templateBoardMat == null) templateBoardMat = CreateBoardMatInstance($"{ActiveCartridge.DisplayName} Board Mat", grid);
            else templateBoardMat.Apply(grid);
            templateBoardMat.SetSurfaceEnabled(false); // the board's own surface holds the floor tiles
            Transform board = boardCollider.transform;
            Vector3 top = board.TransformPoint(boardCollider.center + (Vector3.up * (boardCollider.size.y * 0.5f)));
            // BOARD-2 (owner, 2026-10-30: "the grid is misplaced" after the floor shrank): the spaces and the playmat stay
            // where the first floor put them. A floor with an odd number of spaces taken out lies half a space toward
            // its first column and row (TrapFloorTemplateFactory), so its tiles land on spaces; the mat moves back by
            // that half space to the first floor's centre. Only spaces set from the board menu with the other odd/even
            // move by a further half space.
            float half = (removed % 2) * 0.5f;
            float extraColumns = Mathf.Repeat((grid.Columns - floor.Columns - removed) * 0.5f, 1f);
            float extraRows = Mathf.Repeat((grid.Rows - floor.Rows - removed) * 0.5f, 1f);
            Vector3 extra = (coordinateConverter.WorldXAxisPerTableUnit * (float)(extraColumns * floor.ColumnPitch))
                + (coordinateConverter.WorldYAxisPerTableUnit * (float)(extraRows * floor.RowPitch));
            Vector3 shift = extra
                + (coordinateConverter.WorldXAxisPerTableUnit * (float)(half * floor.ColumnPitch))
                + (coordinateConverter.WorldYAxisPerTableUnit * (float)(half * floor.RowPitch));
            templateBoardMat.transform.SetPositionAndRotation(
                top + shift - (Vector3.up * templateBoardMat.Thickness),
                board.rotation);
            // T-1: the game's playmat (Trap Floor: 35 x 24") stays centred on the first floor while only extra moves
            // the spaces. Where the mat reaches past the board, its own surface holds what is put there.
            if (playMat.x > 0f && playMat.y > 0f)
            {
                Vector3 back = templateBoardMat.transform.InverseTransformVector(-extra);
                templateBoardMat.SetMatSize(playMat.x, playMat.y, new Vector2(back.x, back.z));
                if (playMat.x > grid.Width || playMat.y > grid.Depth) templateBoardMat.SetSurfaceEnabled(true);
            }

            templateBoardMat.SetMatPicture(ActiveCartridge.BoardMatPicture);
            if (gameBoardVisualRoot != null) gameBoardVisualRoot.gameObject.SetActive(false);
        }

        // BOARD-1: how many spaces of this size fit along the playmat (a margin inside each edge), at least the floor's,
        // with the floor's odd/even so the two stay centred on one another.
        private const float PlayMatSpaceMargin = 1f;

        // On a playmat the spaces need only a thin frame (the picture is the board's border).
        private const float PlayMatSpaceFrame = 0.1f;

        private static int SpacesFitting(float matLength, float space, float gap, int atLeast)
        {
            int fit = Mathf.FloorToInt((matLength - (2f * PlayMatSpaceMargin) + gap) / Mathf.Max(0.01f, space + gap));
            if (fit > atLeast && (fit - atLeast) % 2 != 0) fit--;
            return Mathf.Max(atLeast, fit);
        }

        internal bool TryFindBoardUnderPointer(Vector2 screenPosition, out BoardId boardId, out GameBoardMatView view)
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
            Spawner.PrepareRuntimeRoot(root, "Board Placement Preview");
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

            ContextMenus.CloseContextMenu();
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

        internal bool CommitBoardCreate(TabletopPose pose, BoardGrid grid, bool duplicate)
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

            // R2: the mat got its view from the table's view sync.
            if (boardViews.TryGetValue(boardId, out GameBoardMatView view))
            {
                presentationTransitions.Appear(view.transform, settleDuration);
            }

            Physics.SyncTransforms();
            ShowMessage($"Board placed: {grid.Columns} x {grid.Rows} spaces. Right-click it to change the grid.");
            return true;
        }

        // LAYOUT-1 (owner, 2026-10-21): a preview of the game's own board (Trap Floor's floor) to place while arranging the
        // table. The board itself cannot move during play; commit gets the new centre and the table is dealt again.
        internal bool BeginTemplateBoardPlacement(Func<TabletopPose, bool> commit)
        {
            EnsureInitialized();
            if (componentPlacementController == null
                || boardMatPrefab == null
                || templateBoardMat == null
                || !templateBoardMat.HasGrid
                || !matchState.PlayAreas.ContainsKey(centralPlayAreaId))
            {
                ShowMessage("The board cannot be placed here.");
                return false;
            }

            ContextMenus.CloseContextMenu();
            componentPlacementController.Cancel();
            GameObject previewRoot = CreateBoardPlacementPreview(templateBoardMat.Grid);
            componentPlacementController.BeginContainerMove(
                previewRoot,
                new TabletopPose(matchState.GetPlayArea(centralPlayAreaId).Bounds.Center, 0f, 0, 0),
                commit);
            ShowPlacementHint("Move Board");
            ShowMessage("Move board: left-click to place it, right-click or Escape to cancel.");
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

            ContextMenus.CloseContextMenu();
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

            // R2: the mat was set in its new place by the table's view sync.
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

        internal void OpenBoardContextMenu(Vector2 screenPosition, BoardId boardId)
        {
            ContextMenus.CloseContextMenu();
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
                ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
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
                if (gameBoardPhysicalSurface is BoxCollider boardCollider)
                {
                    ApplyTemplateBoardMat(boardCollider); // BOARD-1: laid out again (spaces on the tiles, the playmat)
                }
                else
                {
                    templateBoardMat?.Apply(grid);
                    templateBoardMat?.SetSurfaceEnabled(false);
                }
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

                // R2: the mat was redrawn with its new grid by the table's view sync.
            }

            ShowBoardContextMenu(boardId); // stays open for the next + or −
        }

        private void DeleteBoard(BoardId boardId)
        {
            CommandResult result = TableAuthority.Submit<DeleteBoardCommand, CommandResult>(
                new DeleteBoardCommand(CreateCommandContext(), boardId));
            ContextMenus.CloseContextMenu();
            if (!result.Succeeded)
            {
                ShowMessage($"Delete rejected: {result.Status}.");
                return;
            }

            // R2: the mat's view was removed by the table's view sync.
            ShowMessage("Board removed. Cards that were on it stay on the table.");
        }

        // ---- The magnetic grid ----

        // A loose card that comes to rest on a board space is set on the space's centre, turned square to the board.
        private void CompleteBoardCardSnaps()
        {
            if (!boardSnapCheckPending || !IsInitialized || matchState == null) return;
            boardSnapCheckPending = false;
            if (boardViews.Count == 0 && templateBoardMat == null)
            {
                SnapRestingPieces(); // POL-2: pieces still settle on the cards they stand on
                return;
            }

            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView card = Registry.cardViews[i];
                if (card == null || !card.IsBound || card.BoundState == null) continue;
                TabletopObjectState state = card.BoundState;
                if (!state.ContainerId.IsEmpty || state.IsUserLocked) continue;
                if (ActiveCartridge.IsFixedBoardPiece(state.Id)) continue; // a game's board tiles never move
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

            SnapRestingPieces(); // POL-2: pawns and tokens settle on the centre of a board space or a card
        }

        // ---- POL-2: piece snap ----

        private readonly RaycastHit[] pieceSnapHits = new RaycastHit[8];

        private void SnapRestingPieces()
        {
            if (!Feel.PieceSnap) return;
            for (int i = 0; i < Registry.pawnViews.Count; i++) TrySnapPiece(Registry.pawnViews[i]);
            for (int i = 0; i < Registry.tokenViews.Count; i++) TrySnapPiece(Registry.tokenViews[i]);
        }

        // A pawn or token at rest on a board space (or on a loose card) is set on its centre, once.
        private void TrySnapPiece(TabletopObjectView piece)
        {
            if (piece == null || !piece.IsBound || piece.BoundState == null || piece.PhysicalObject == null) return;
            TabletopObjectState state = piece.BoundState;
            if (!state.ContainerId.IsEmpty || state.IsUserLocked) return;
            PhysicalLooseObject body = piece.PhysicalObject;
            if (body.IsHeld || body.IsGliding || body.Body == null
                || state.PhysicalState == null || state.PhysicalState.Mode != PhysicalObjectMode.Sleeping)
            {
                return;
            }

            Vector3 position = body.Body.position;
            if (!TryFindPieceSpot(piece, position, out Vector3 centre)) return;
            float dx = centre.x - position.x;
            float dz = centre.z - position.z;
            if ((dx * dx) + (dz * dz) <= BoardSnapTolerance * BoardSnapTolerance) return;
            if (cardsSetOnBoardSpaces.TryGetValue(state.Id, out Vector3 last) && (last - centre).sqrMagnitude < 0.0001f) return;
            if (IsSpotTaken(piece, centre)) return; // someone already stands there: leave it where it is

            PhysicalObjectState current = state.PhysicalState;
            Quaternion fromRotation = body.Body.rotation;
            PhysicalObjectState placed = new PhysicalObjectState(
                new PhysicalVector3(centre.x, current.Position.Y, centre.z),
                current.Rotation,
                new PhysicalVector3(0f, 0f, 0f),
                new PhysicalVector3(0f, 0f, 0f),
                PhysicalObjectMode.Sleeping,
                current.ControllingPlayerId);
            CommandResult result = TableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(
                new CommitPhysicalObjectCommand(
                    CreateCommandContext(),
                    state.Id,
                    placed,
                    state.PhysicalRevision,
                    null,
                    AuthoritativeActionRecordMode.None));
            if (!result.Succeeded) return;
            cardsSetOnBoardSpaces[state.Id] = centre;
            piece.ApplyAcceptedState();
            body.GlideFrom(position, fromRotation, Feel.SnapGlideSeconds, Feel.SnapOvershoot);
        }

        // The board space under the piece, or else the centre of a loose card it stands on.
        private bool TryFindPieceSpot(TabletopObjectView piece, Vector3 position, out Vector3 centre)
        {
            if (TryFindBoardSpace(position, out _, out centre)) return true;
            int count = Physics.RaycastNonAlloc(
                new Ray(position + (Vector3.up * 0.05f), Vector3.down),
                pieceSnapHits,
                2f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            CardView under = null;
            for (int i = 0; i < count; i++)
            {
                Rigidbody hitBody = pieceSnapHits[i].rigidbody;
                if (hitBody == null || hitBody == piece.PhysicalObject.Body) continue;
                if (!hitBody.TryGetComponent(out CardView card) || !card.IsBound || card.BoundState == null) continue;
                if (!card.BoundState.ContainerId.IsEmpty || pieceSnapHits[i].distance >= best) continue;
                best = pieceSnapHits[i].distance;
                under = card;
            }

            if (under == null)
            {
                centre = Vector3.zero;
                return false;
            }

            centre = RestPosition(under);
            return true;
        }

        private bool IsSpotTaken(TabletopObjectView piece, Vector3 centre)
        {
            return IsSpotTakenIn(Registry.pawnViews, piece, centre) || IsSpotTakenIn(Registry.tokenViews, piece, centre);
        }

        private static bool IsSpotTakenIn<TView>(List<TView> views, TabletopObjectView piece, Vector3 centre)
            where TView : TabletopObjectView
        {
            for (int i = 0; i < views.Count; i++)
            {
                TView other = views[i];
                if (other == null || ReferenceEquals(other, piece) || !other.IsBound || other.BoundState == null) continue;
                if (!other.BoundState.ContainerId.IsEmpty) continue;
                Vector3 p = other.PhysicalObject != null && other.PhysicalObject.Body != null
                    ? other.PhysicalObject.Body.position
                    : other.transform.position;
                float dx = p.x - centre.x;
                float dz = p.z - centre.z;
                if ((dx * dx) + (dz * dz) <= BoardSnapTolerance * BoardSnapTolerance * 4f) return true;
            }

            return false;
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
            Vector3 fromPosition = card.PhysicalObject.Body != null ? card.PhysicalObject.Body.position : card.transform.position;
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
            if (!result.Succeeded || !ApplyAcceptedCard(card)) return false;
            // POL-2: it glides onto the space with a small spring instead of jumping there.
            card.PhysicalObject.GlideFrom(fromPosition, current, Feel.SnapGlideSeconds, Feel.SnapOvershoot);
            return true;
        }

        private static bool ApplyAcceptedCard(CardView card)
        {
            card.ApplyAcceptedState();
            return true;
        }
    }
}
