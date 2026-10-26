using System;
using System.Collections.Generic;
using System.Globalization;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Table mode (doc 30, B3; T3): the Table menu's "Table tools…" opens a side panel and turns the camera to the
    /// top view. Shape buttons, sizes (− / + with Shift for big steps, or typed in), corners,
    /// the edge and its size, and size handles on the table itself (drag; Alt for free sizes). A new shape
    /// grows out of the old one. Every change is one Undo step (up to 16) inside Table mode; Done keeps the table
    /// (saved in the profile), Cancel or Esc puts back the table Table mode started with.
    /// <para>Nothing falls off (owner, 2026-10-17: move the pieces in; 2026-10-18: every Toolbox piece): when the
    /// table gets smaller, every piece that would end up off it or right at its edge is moved in along the shortest
    /// line to the centre, as one Undo step of the game: cards, dice, pawns, tokens, decks, discard piles, stacks
    /// (with their pile bays), Consoles (with the cards in their slots) and game board mats (with the loose cards
    /// lying on them). Only another player's pile stops the table. While a game is on the table, the table never
    /// gets smaller than the classic playing area, so the game's own board always fits.</para>
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const int TableUndoLimit = 16;
        private const float ShapeChangeSeconds = 0.4f;
        private const int HandleSideRight = 0;
        private const int HandleSideLeft = 1;
        private const int HandleSideFar = 2;
        private const int HandleSideNear = 3;
        private const int HandleCornerOrRadius = 4;

        private static readonly TableShape[] PanelShapes =
        {
            TableShape.Classic, TableShape.Rectangle, TableShape.Square, TableShape.Round, TableShape.Oval, TableShape.Hexagon,
        };

        private readonly List<TableSpec> tableUndo = new List<TableSpec>(TableUndoLimit);
        private readonly List<TableSpec> tableRedo = new List<TableSpec>(TableUndoLimit);
        private readonly TablePanelModel tablePanelModel = new TablePanelModel();
        private readonly TableOutline holdOutline = new TableOutline(256);
        private readonly TableOutline nowOutline = new TableOutline(256);
        private readonly List<TabletopObjectView> loosePiecesToMove = new List<TabletopObjectView>(32);
        private readonly List<ContainerId> containersToMove = new List<ContainerId>(16);
        private readonly List<ContainerId> consolesToMove = new List<ContainerId>(8);
        private readonly List<Bounds> consoleBoundsToMove = new List<Bounds>(8);
        private readonly List<BoardId> boardsToMove = new List<BoardId>(8);
        private readonly List<Bounds> boardBoundsToMove = new List<Bounds>(8);
        private readonly List<TabletopObjectView> boardRiders = new List<TabletopObjectView>(32);
        private readonly List<int> boardRiderBoard = new List<int>(32);
        private TablePanelActions tablePanelActions;
        private Action openTableModeAction;
        private TableSpec tableModeStart;
        private TableSpec handleSpec;
        private int activeHandle = -1;

        private Action OpenTableModeAction
        {
            get
            {
                if (openTableModeAction == null) openTableModeAction = OpenTableMode;
                return openTableModeAction;
            }
        }

        // Owner, 2026-10-26: the table's shape, size and edge are made from CREATE on the front end; closing Table
        // tools (Done or Cancel) goes back there.
        private bool tableModeReturnsToFrontEnd;

        /// <summary>Opens Table tools from the front end; false when there is no table to change.</summary>
        internal bool OpenTableToolsFromFrontEnd()
        {
            OpenTableMode();
            tableModeReturnsToFrontEnd = tableModeStart != null;
            return tableModeReturnsToFrontEnd;
        }

        private void OpenTableMode()
        {
            if (runtimeUi == null || !FindClassicTable()) return;
            tableModeStart = tableSpec.Copy();
            tableUndo.Clear();
            tableRedo.Clear();
            handleSpec = null;
            activeHandle = -1;
            FrameWholeTable(); // camera fix (owner, 2026-10-20): the whole table from above, wherever the camera was
            RefreshTablePanel("Changes show on the table at once. Drag the dots to resize. Done keeps them; Cancel or Esc "
                + "puts the table back.");
        }

        private void RefreshTablePanel(string status)
        {
            if (runtimeUi == null) return;
            tablePanelActions ??= new TablePanelActions
            {
                PickShape = PanelPickShape,
                CycleModel = PanelCycleModel,
                Step = PanelStep,
                Type = PanelType,
                CycleEdge = PanelCycleEdge,
                Undo = PanelUndo,
                Redo = PanelRedo,
                Reset = PanelReset,
                Cancel = PanelCancel,
                Done = PanelDone,
                BeginHandle = BeginTableHandle,
                DragHandle = DragTableHandle,
                EndHandle = EndTableHandle,
            };

            TableSpec spec = handleSpec ?? tableSpec;
            TablePanelModel model = tablePanelModel;
            TableShape shape = spec.shape;
            bool built = IsBuiltShape(shape);
            model.SelectedShape = Array.IndexOf(PanelShapes, shape);
            model.ShowModels = NextCycleModel(-1) >= 0;
            model.ModelText = shape == TableShape.Model ? TableShapeName(spec) : "Choose…";
            SetField(model, TableField.Width, built, spec.width.ToString("0.0", CultureInfo.InvariantCulture));
            SetField(model, TableField.Depth, shape == TableShape.Rectangle || shape == TableShape.Oval,
                spec.depth.ToString("0.0", CultureInfo.InvariantCulture));
            SetField(model, TableField.Corners, shape == TableShape.Rectangle || shape == TableShape.Square,
                spec.cornerRadius.ToString("0.00", CultureInfo.InvariantCulture));
            SetField(model, TableField.Points, false, string.Empty); // T-3: the star's rows stay hidden (no star now)
            SetField(model, TableField.InnerSize, false, string.Empty);
            bool hasEdge = built && spec.edge != TableEdge.None;
            SetField(model, TableField.EdgeHeight, hasEdge, spec.edgeHeight.ToString("0.00", CultureInfo.InvariantCulture));
            SetField(model, TableField.EdgeWidth, hasEdge, spec.edgeWidth.ToString("0.00", CultureInfo.InvariantCulture));
            model.ShowEdge = built;
            model.EdgeText = TableSpec.EdgeLabel(spec.edge);
            model.CanUndo = tableUndo.Count > 0;
            model.CanRedo = tableRedo.Count > 0;
            model.Status = built || shape == TableShape.Classic || string.IsNullOrEmpty(status)
                ? status
                : $"{status} A model keeps its own size.";
            model.Camera = targetCamera;
            PlaceHandles(model, spec);
            if (!runtimeUi.ShowTablePanel(model, tablePanelActions))
            {
                ShowMessage("The Table panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        private static void SetField(TablePanelModel model, TableField field, bool show, string text)
        {
            model.ShowField[(int)field] = show;
            model.FieldText[(int)field] = text;
        }

        // ---------- one change: checked, shown, pieces moved in, one Undo step ----------

        private void ApplyTableModeChange(TableSpec candidate, string done)
        {
            candidate.Clamp();
            TableSpec previous = tableSpec;
            if (!TryChangeTable(candidate, out string note))
            {
                RefreshTablePanel(note);
                return;
            }

            tableUndo.Add(previous);
            if (tableUndo.Count > TableUndoLimit) tableUndo.RemoveAt(0);
            tableRedo.Clear();
            tableSpec = candidate;
            GrowFrom(previous, candidate);
            RefreshTablePanel(done + note);
        }

        /// <summary>
        /// Shows the table for <paramref name="to"/>, keeping everything on it: the game's room is kept, fixed pieces
        /// are moved in with everything else at the new edge (only another player's pile stops it). On success
        /// <see cref="tableSpec"/> becomes <paramref name="to"/> (the callers set it too). <paramref name="message"/> says why it stopped, or
        /// how many pieces were moved.
        /// </summary>
        private bool TryChangeTable(TableSpec to, out string message)
        {
            if (!PrepareTableChange(tableSpec, to, out string why))
            {
                message = why;
                return false;
            }

            if (!ShowTable(to, out string error))
            {
                message = $"Not changed: {error}.";
                return false;
            }

            int moved = MovePiecesIn();
            Vector3 sizeBefore = TryGetTableWorldBounds(out Bounds before) ? before.size : Vector3.zero;
            // The table is now this one (the callers set it too): the seats and the camera measure the new table, not
            // the one before (fix 2026-10-20: the seats used to follow one change behind).
            tableSpec = to;
            int seatsMoved = ArrangeSeatsAlongEdge(false); // CART-1c: a game's seats follow the edge
            ConfigureTabletopCameraFraming(); // the views 1–4 frame the table as it is now
            if (tableModeStart != null
                && TryGetTableWorldBounds(out Bounds after)
                && (after.size - sizeBefore).sqrMagnitude > 0.0001f)
            {
                FrameWholeTable(); // a bigger or smaller table: the top view follows it
            }

            message = moved > 0
                ? $" Moved {moved} {(moved == 1 ? "piece" : "pieces")} in from the edge (Undo puts {(moved == 1 ? "it" : "them")} back)."
                : string.Empty;
            if (seatsMoved > 0) message += $" {seatsMoved} seat{(seatsMoved == 1 ? " moves" : "s move")} to the new edge.";
            return true;
        }

        // The new shape grows out of the old one (built shapes only; models and Classic appear at once).
        private void GrowFrom(TableSpec previous, TableSpec next)
        {
            if (generatedTable == null || !generatedTable.gameObject.activeSelf || !IsBuiltShape(next.shape)) return;
            if (previous.shape == next.shape && previous.modelId == next.modelId) return;
            generatedTable.MorphFrom(HoldSpecFor(previous), classicFrame.WorldPerTableUnit, ShapeChangeSeconds);
        }

        // ---------- panel actions ----------

        private void PanelPickShape(int index)
        {
            if (index < 0 || index >= PanelShapes.Length || PanelShapes[index] == tableSpec.shape) return;
            TableShape shape = PanelShapes[index];
            TableSpec candidate = shape == TableShape.Classic
                ? new TableSpec { shape = TableShape.Classic }
                : TableSpec.DefaultFor(shape, classicFrame.ClassicSize.x, classicFrame.ClassicSize.y);
            KeepEdge(candidate);
            ApplyTableModeChange(candidate, $"{TableSpec.Label(shape)} table.");
        }

        private void PanelCycleModel(int direction)
        {
            LoadTableModels();
            List<int> choices = new List<int>();
            for (int i = NextCycleModel(-1); i >= 0; i = NextCycleModel(i)) choices.Add(i);
            if (choices.Count == 0) return;
            int current = tableSpec.shape == TableShape.Model
                ? choices.IndexOf(tableModels.IndexOf(FindTableModel(tableSpec.modelId)))
                : -1;
            int next = current < 0
                ? (direction > 0 ? 0 : choices.Count - 1)
                : (current + direction + choices.Count) % choices.Count;
            TableSpec candidate = ModelSpec(tableModels[choices[next]]);
            KeepEdge(candidate);
            ApplyTableModeChange(candidate, $"{TableShapeName(candidate)} table.{ModelFitNote()}");
        }

        private void PanelStep(TableField field, int direction, bool big)
        {
            TableSpec candidate = tableSpec.Copy();
            switch (field)
            {
                case TableField.Width:
                    candidate.width = Snap(candidate.width + (direction * (big ? 5f : 0.5f)), 0.5f);
                    break;
                case TableField.Depth:
                    candidate.depth = Snap(candidate.depth + (direction * (big ? 5f : 0.5f)), 0.5f);
                    break;
                case TableField.Corners:
                    candidate.cornerRadius = Snap(candidate.cornerRadius + (direction * (big ? 1f : 0.25f)), 0.25f);
                    break;
                case TableField.EdgeHeight:
                    candidate.edgeHeight = Snap(candidate.edgeHeight + (direction * (big ? 0.25f : 0.05f)), 0.05f);
                    break;
                case TableField.EdgeWidth:
                    candidate.edgeWidth = Snap(candidate.edgeWidth + (direction * (big ? 0.25f : 0.05f)), 0.05f);
                    break;
            }

            ApplyTableModeChange(candidate, string.Empty);
        }

        // A typed value is taken as it is (no snapping).
        private void PanelType(TableField field, string text)
        {
            string clean = (text ?? string.Empty).Replace('%', ' ').Replace(',', '.').Trim();
            if (!float.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                RefreshTablePanel("Type a number.");
                return;
            }

            TableSpec candidate = tableSpec.Copy();
            switch (field)
            {
                case TableField.Width: candidate.width = value; break;
                case TableField.Depth: candidate.depth = value; break;
                case TableField.Corners: candidate.cornerRadius = value; break;
                case TableField.EdgeHeight: candidate.edgeHeight = value; break;
                case TableField.EdgeWidth: candidate.edgeWidth = value; break;
            }

            ApplyTableModeChange(candidate, string.Empty);
        }

        private void PanelCycleEdge(int direction)
        {
            TableSpec candidate = tableSpec.Copy();
            // Six edges: going back one is going forward five.
            int steps = direction >= 0 ? 1 : 5;
            for (int i = 0; i < steps; i++) candidate.edge = TableSpec.NextEdge(candidate.edge);
            ApplyTableModeChange(candidate, $"Edge: {TableSpec.EdgeLabel(candidate.edge)}.");
        }

        private void PanelUndo() => StepHistory(tableUndo, tableRedo, "Undone.");

        private void PanelRedo() => StepHistory(tableRedo, tableUndo, "Redone.");

        private void StepHistory(List<TableSpec> from, List<TableSpec> to, string done)
        {
            if (from.Count == 0) return;
            TableSpec target = from[from.Count - 1];
            TableSpec previous = tableSpec;
            if (!TryChangeTable(target, out string note))
            {
                RefreshTablePanel(note);
                return;
            }

            from.RemoveAt(from.Count - 1);
            to.Add(previous);
            tableSpec = target;
            GrowFrom(previous, target);
            RefreshTablePanel(done + note);
        }

        private void PanelReset()
        {
            if (!IsBuiltShape(tableSpec.shape)) return;
            TableSpec candidate = TableSpec.DefaultFor(tableSpec.shape, classicFrame.ClassicSize.x, classicFrame.ClassicSize.y);
            KeepEdge(candidate);
            ApplyTableModeChange(candidate, "Size reset.");
        }

        private void PanelCancel()
        {
            handleSpec = null;
            activeHandle = -1;
            if (tableModeStart != null)
            {
                TableSpec previous = tableSpec;
                if (TryChangeTable(tableModeStart, out _) || ShowTable(tableModeStart, out _))
                {
                    tableSpec = tableModeStart;
                    GrowFrom(previous, tableSpec);
                }
            }

            CloseTableMode("Table mode closed; the table is as it was.");
        }

        private void PanelDone()
        {
            SaveFiles.WriteJson(TableSpecPath, tableSpec, TableArea);
            CloseTableMode($"{TableShapeName(tableSpec)} table kept.");
        }

        private void CloseTableMode(string message)
        {
            tableModeStart = null;
            handleSpec = null;
            activeHandle = -1;
            tableUndo.Clear();
            tableRedo.Clear();
            runtimeUi?.CloseTablePanel();
            TabletopCameraController cameraController = cameraInputAdapter?.CameraController;
            if (cameraController != null && !cameraController.ShowBoardView()) cameraController.ShowDefaultView();
            RefreshTableShapeEntry();
            ShowMessage(message);
            if (!tableModeReturnsToFrontEnd) return;
            tableModeReturnsToFrontEnd = false;
            GameSelectPanel.ReturnToGameSelect();
        }

        // LAYOUT-1: back to the play view after arranging the table (as Table mode does when it closes).
        internal void ShowPlayView()
        {
            TabletopCameraController cameraController = cameraInputAdapter?.CameraController;
            if (cameraController != null && !cameraController.ShowBoardView()) cameraController.ShowDefaultView();
        }

        // The whole table from above (Table mode), wherever the camera was panned or turned.
        internal void FrameWholeTable()
        {
            TabletopCameraController cameraController = cameraInputAdapter?.CameraController;
            if (cameraController == null) return;
            if (TryGetTableWorldBounds(out Bounds table)) cameraController.ShowTopDownView(table);
            else cameraController.ShowPreset(TabletopCameraPreset.TopDown);
        }

        // A new shape keeps the chosen edge and its size.
        private void KeepEdge(TableSpec candidate)
        {
            candidate.edge = tableSpec.edge;
            candidate.edgeHeight = tableSpec.edgeHeight;
            candidate.edgeWidth = tableSpec.edgeWidth;
        }

        private static float Snap(float value, float step) => Mathf.Round(value / step) * step;

        // ---------- size handles ----------

        // Where each handle sits for the shape (table-local flat points, lifted to the edge's top), and which show:
        // Rectangle four sides; Square one corner; Round one radius; Oval width and depth; Hexagon one corner.
        // Classic and models have no handles.
        private void PlaceHandles(TablePanelModel model, TableSpec spec)
        {
            for (int i = 0; i < TablePanelModel.HandleCount; i++) model.HandleShown[i] = false;
            if (!IsBuiltShape(spec.shape)) return;
            float unit = classicFrame.WorldPerTableUnit;
            float halfW = 0.5f * spec.width * unit;
            float halfD = 0.5f * spec.depth * unit;
            switch (spec.shape)
            {
                case TableShape.Rectangle:
                    SetHandle(model, HandleSideRight, new Vector2(halfW, 0f), spec);
                    SetHandle(model, HandleSideLeft, new Vector2(-halfW, 0f), spec);
                    SetHandle(model, HandleSideFar, new Vector2(0f, halfD), spec);
                    SetHandle(model, HandleSideNear, new Vector2(0f, -halfD), spec);
                    break;
                case TableShape.Square:
                    SetHandle(model, HandleCornerOrRadius, new Vector2(halfW, halfW), spec);
                    break;
                case TableShape.Round:
                    SetHandle(model, HandleCornerOrRadius, new Vector2(halfW, halfW) * 0.70710678f, spec);
                    break;
                case TableShape.Oval:
                    SetHandle(model, HandleSideRight, new Vector2(halfW, 0f), spec);
                    SetHandle(model, HandleSideFar, new Vector2(0f, halfD), spec);
                    break;
                case TableShape.Hexagon:
                    SetHandle(model, HandleCornerOrRadius, TableSpec.HexagonCorner(0) * halfW, spec);
                    break;
            }
        }

        private void SetHandle(TablePanelModel model, int index, Vector2 flat, TableSpec spec)
        {
            float lift = spec.edge == TableEdge.None ? 0.05f : spec.edgeHeight * classicFrame.WorldPerTableUnit;
            model.HandleShown[index] = true;
            model.HandleWorld[index] = classicFrame.Centre + (classicFrame.Rotation * new Vector3(flat.x, lift, flat.y));
        }

        private void BeginTableHandle(int index)
        {
            if (!IsBuiltShape(tableSpec.shape) || generatedTable == null) return;
            activeHandle = index;
            handleSpec = tableSpec.Copy();
        }

        // While dragging only the look follows (no collider change, so nothing on the table moves or falls);
        // the size snaps to 0.5 unless Alt is held.
        private void DragTableHandle(int index, Vector2 screen)
        {
            if (index != activeHandle || handleSpec == null || targetCamera == null) return;
            Ray ray = targetCamera.ScreenPointToRay(screen);
            // The plane the handles stand on (lifted to the edge's top), so the handle stays under the pointer from
            // any camera angle.
            float lift = handleSpec.edge == TableEdge.None ? 0.05f : handleSpec.edgeHeight * classicFrame.WorldPerTableUnit;
            Vector3 up = classicFrame.Rotation * Vector3.up;
            Plane top = new Plane(up, classicFrame.Centre + (up * lift));
            if (!top.Raycast(ray, out float distance)) return;
            Vector3 local = Quaternion.Inverse(classicFrame.Rotation) * (ray.GetPoint(distance) - classicFrame.Centre);
            Vector2 flat = new Vector2(local.x, local.z);
            float unit = classicFrame.WorldPerTableUnit;
            Keyboard keyboard = Keyboard.current;
            bool free = keyboard != null && keyboard.altKey.isPressed;
            TableSpec next = handleSpec.Copy();
            switch (index)
            {
                case HandleSideRight:
                case HandleSideLeft:
                    next.width = 2f * Mathf.Abs(flat.x) / unit;
                    break;
                case HandleSideFar:
                case HandleSideNear:
                    next.depth = 2f * Mathf.Abs(flat.y) / unit;
                    break;
                case HandleCornerOrRadius:
                    next.width = 2f * (next.shape == TableShape.Square
                        ? Mathf.Max(Mathf.Abs(flat.x), Mathf.Abs(flat.y))
                        : flat.magnitude) / unit;
                    break;
            }

            if (!free)
            {
                next.width = Snap(next.width, 0.5f);
                next.depth = Snap(next.depth, 0.5f);
            }

            next.Clamp();
            if (next.width == handleSpec.width && next.depth == handleSpec.depth) return;
            handleSpec = next;
            generatedTable.Preview(next, unit);
            RefreshTablePanel(DragText(next, index, free));
        }

        private static string DragText(TableSpec spec, int index, bool free)
        {
            string size = spec.shape == TableShape.Rectangle || spec.shape == TableShape.Oval
                ? $"{spec.width:0.0} × {spec.depth:0.0}"
                : $"{spec.width:0.0} across";
            return free ? $"{size} (free size)" : $"{size} · hold Alt for free sizes";
        }

        // On release the table is really built, pieces are moved in, and it becomes one Undo step; if something
        // fixed is in the way the table goes back to where the drag started.
        private void EndTableHandle(int index)
        {
            if (index != activeHandle) return;
            activeHandle = -1;
            TableSpec final = handleSpec;
            handleSpec = null;
            if (final == null) return;
            TableSpec previous = tableSpec;
            if (!TryChangeTable(final, out string note))
            {
                ShowTable(previous, out _);
                RefreshTablePanel(note);
                return;
            }

            tableUndo.Add(previous);
            if (tableUndo.Count > TableUndoLimit) tableUndo.RemoveAt(0);
            tableRedo.Clear();
            tableSpec = final;
            RefreshTablePanel(DragText(final, index, false).Split('·')[0].Trim() + "." + note);
        }

        // ---------- keeping the pieces on the table ----------

        // The outline a table stands for when checking pieces: a built shape itself; Classic and models their
        // playing area as a rectangle.
        private TableSpec HoldSpecFor(TableSpec spec)
        {
            if (IsBuiltShape(spec.shape)) return spec;
            Vector2 size = classicFrame.ClassicSize;
            if (spec.shape == TableShape.Model)
            {
                TableModel model = FindTableModel(spec.modelId);
                MeshFilter filter = model != null && model.PlayingArea != null ? model.PlayingArea.GetComponent<MeshFilter>() : null;
                if (filter != null && filter.sharedMesh != null)
                {
                    Vector3 area = filter.sharedMesh.bounds.size;
                    size = new Vector2(area.x, area.z) / classicFrame.WorldPerTableUnit;
                }
            }

            return new TableSpec { shape = TableShape.Rectangle, width = size.x, depth = size.y, cornerRadius = 0f };
        }

        // Checks a change from one table to another and notes which pieces to move in. False (with the reason)
        // when the game's room or a fixed piece would be lost. Event-time only (it looks the pieces up).
        private bool PrepareTableChange(TableSpec from, TableSpec to, out string why)
        {
            why = null;
            ClearPiecesToMove();
            float unit = classicFrame.WorldPerTableUnit;
            if (!holdOutline.Build(HoldSpecFor(to), unit, 1f))
            {
                why = "That shape and size do not make a usable table.";
                return false;
            }

            bool knowNow = nowOutline.Build(HoldSpecFor(from), unit, 1f);
            Quaternion inverse = Quaternion.Inverse(classicFrame.Rotation);
            Vector3 centre = classicFrame.Centre;
            if (to.shape != TableShape.Classic && activeSession != null
                && activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom)
            {
                Vector2 half = 0.49f * unit * classicFrame.ClassicSize;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 point = new Vector2(corner % 2 == 0 ? half.x : -half.x, corner < 2 ? half.y : -half.y);
                    if (!holdOutline.Contains(point))
                    {
                        why = $"This game needs at least the classic playing area ({classicFrame.ClassicSize.x:0.#} × "
                            + $"{classicFrame.ClassicSize.y:0.#}), so the table cannot get smaller here.";
                        return false;
                    }
                }
            }

            if (matchState == null || coordinateConverter == null) return true;

            // Consoles (the cards in their slots go with them).
            foreach (ConsoleView console in UnityEngine.Object.FindObjectsByType<ConsoleView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!console.IsBound || console.ConsoleState.SlotContainerIds.Count == 0) continue;
                Bounds bounds = AllBounds(console);
                if (!OnTableAndOffNew(bounds, inverse, centre, knowNow)) continue;
                consolesToMove.Add(console.ConsoleState.SlotContainerIds[0]);
                consoleBoundsToMove.Add(bounds);
            }

            // Board mats placed from the Toolbox (the game's own board sits inside the classic area).
            foreach (KeyValuePair<BoardId, GameBoardMatView> pair in boardViews)
            {
                if (pair.Value == null || !matchState.PlacedBoards.ContainsKey(pair.Key)) continue;
                Bounds bounds = AllBounds(pair.Value);
                if (!OnTableAndOffNew(bounds, inverse, centre, knowNow)) continue;
                boardsToMove.Add(pair.Key);
                boardBoundsToMove.Add(bounds);
            }

            // Loose pieces: those lying on a board that moves ride along with it; the rest move in on their own.
            foreach (TabletopObjectView piece in UnityEngine.Object.FindObjectsByType<TabletopObjectView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!piece.IsBound || piece.IsContainerLayoutApplied || piece.IsOverlayPresentation) continue;
                int board = BoardUnder(piece.transform.position);
                if (board >= 0)
                {
                    boardRiders.Add(piece);
                    boardRiderBoard.Add(board);
                }
                else if (OnTableAndOffNew(PieceBounds(piece), inverse, centre, knowNow))
                {
                    loosePiecesToMove.Add(piece);
                }
            }

            foreach (KeyValuePair<ContainerId, ContainerPlacementState> pair in matchState.ContainerPlacements)
            {
                if (!matchState.Containers.TryGetValue(pair.Key, out ContainerState container)
                    || (container.Kind != ContainerKind.Deck
                        && container.Kind != ContainerKind.Stack
                        && container.Kind != ContainerKind.DiscardPile))
                {
                    continue;
                }

                Vector3 world = coordinateConverter.ToWorldPosition(pair.Value.Pose);
                Bounds bounds = new Bounds(world, new Vector3(2f * unit, 0.5f * unit, 2.8f * unit));
                if (!OnTableAndOffNew(bounds, inverse, centre, knowNow)) continue;
                if (MoveContainerUseCase.IsMoveBlockedByOwner(matchState, container, localPlayerId))
                {
                    why = "Another player's pile is in the way near the edge, so the table stops here.";
                    return false;
                }

                containersToMove.Add(pair.Key);
            }

            return true;
        }

        // On the table shown now (pieces already off it are ignored, as is the screen hand far above), and either
        // off the new one or less than half its own width from the new edge.
        private bool OnTableAndOffNew(Bounds piece, Quaternion inverse, Vector3 centre, bool knowNow)
        {
            Vector3 local = inverse * (piece.center - centre);
            if (Mathf.Abs(local.y) > 3f * classicFrame.WorldPerTableUnit) return false;
            Vector2 flat = new Vector2(local.x, local.z);
            if (knowNow && !nowOutline.Contains(flat)) return false;
            float reach = 0.5f * Mathf.Max(piece.extents.x, piece.extents.z);
            return !holdOutline.Contains(flat) || holdOutline.DistanceToOutline(flat) < reach;
        }

        // Moves the noted pieces in along the line to the centre until they stand inside the new edge, as one Undo
        // step of the game. Returns how many moved. Call after the new table is shown (pieces land on it).
        private int MovePiecesIn()
        {
            if (loosePiecesToMove.Count == 0 && containersToMove.Count == 0 && consolesToMove.Count == 0
                && boardsToMove.Count == 0)
            {
                return 0;
            }

            int moved = 0;
            Quaternion inverse = Quaternion.Inverse(classicFrame.Rotation);
            SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < boardsToMove.Count; i++)
                {
                    Vector3 offset = Flat(PointInside(boardBoundsToMove[i], inverse) - boardBoundsToMove[i].center);
                    if (MoveBoardBy(boardsToMove[i], offset)) moved++;
                    for (int r = 0; r < boardRiders.Count; r++)
                    {
                        TabletopObjectView rider = boardRiders[r];
                        if (boardRiderBoard[r] != i || rider == null || !rider.IsBound) continue;
                        TabletopObjectState riderState = rider.BoundState;
                        TabletopPose riderPose = new TabletopPose(
                            coordinateConverter.ToTableCoordinate(rider.transform.position + offset),
                            riderState.Pose.RotationDegrees,
                            riderState.Pose.Layer,
                            riderState.Pose.LocalOrder);
                        if (MoveLoosePiece(rider, riderState, riderPose)) moved++;
                    }
                }

                for (int i = 0; i < consolesToMove.Count; i++)
                {
                    if (!TryResolveConsolePlacement(consolesToMove[i], out TabletopPose consolePose, out _, out _)) continue;
                    Vector3 offset = Flat(PointInside(consoleBoundsToMove[i], inverse) - consoleBoundsToMove[i].center);
                    Vector3 world = coordinateConverter.ToWorldPosition(consolePose) + offset;
                    TabletopPose pose = new TabletopPose(
                        coordinateConverter.ToTableCoordinate(world),
                        consolePose.RotationDegrees,
                        consolePose.Layer,
                        consolePose.LocalOrder);
                    if (CommitContainerMove(consolesToMove[i], pose)) moved++;
                }

                for (int i = 0; i < loosePiecesToMove.Count; i++)
                {
                    TabletopObjectView piece = loosePiecesToMove[i];
                    if (piece == null || !piece.IsBound) continue;
                    Bounds bounds = PieceBounds(piece);
                    TabletopObjectState state = piece.BoundState;
                    TabletopPose pose = new TabletopPose(
                        coordinateConverter.ToTableCoordinate(PointInside(bounds, inverse)),
                        state.Pose.RotationDegrees,
                        state.Pose.Layer,
                        state.Pose.LocalOrder);
                    if (MoveLoosePiece(piece, state, pose)) moved++;
                }

                for (int i = 0; i < containersToMove.Count; i++)
                {
                    if (!matchState.TryGetContainerPlacement(containersToMove[i], out ContainerPlacementState placement)) continue;
                    Vector3 world = coordinateConverter.ToWorldPosition(placement.Pose);
                    float unit = classicFrame.WorldPerTableUnit;
                    Bounds bounds = new Bounds(world, new Vector3(2f * unit, 0.5f * unit, 2.8f * unit));
                    TabletopPose pose = new TabletopPose(
                        coordinateConverter.ToTableCoordinate(PointInside(bounds, inverse)),
                        placement.Pose.RotationDegrees,
                        placement.Pose.Layer,
                        placement.Pose.LocalOrder);
                    if (CommitContainerMove(containersToMove[i], pose)) moved++;
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
                ClearPiecesToMove();
            }

            return moved;
        }

        // The world point a piece moves to: along the line to the centre, a little inside the new edge.
        private Vector3 PointInside(Bounds piece, Quaternion inverse)
        {
            Vector3 local = inverse * (piece.center - classicFrame.Centre);
            Vector2 flat = new Vector2(local.x, local.z);
            float reach = Mathf.Max(piece.extents.x, piece.extents.z) + (0.15f * classicFrame.WorldPerTableUnit);
            float allowed = Mathf.Max(0f, holdOutline.DistanceToEdge(flat) - reach);
            float distance = flat.magnitude;
            Vector2 inside = distance > allowed && distance > 1e-4f ? flat * (allowed / distance) : flat;
            return classicFrame.Centre + (classicFrame.Rotation * new Vector3(inside.x, 0f, inside.y));
        }

        // As the board-space placement does it: a loose piece through MoveObject, a physical one through
        // CommitPhysicalObject (resting on the new table top, keeping its turn).
        internal bool MoveLoosePiece(TabletopObjectView piece, TabletopObjectState state, TabletopPose pose)
        {
            bool accepted;
            if (piece.PhysicalObject == null)
            {
                accepted = TableAuthority.Submit<MoveObjectCommand, MoveObjectResult>(
                    new MoveObjectCommand(CreateCommandContext(), state.Id, pose)).Succeeded;
            }
            else
            {
                if (physicalAuthority == null || activeSession == null) return false;
                if (!physicalAuthority.Surfaces.TryResolveAuthoredLooseObject(
                        pose,
                        localPlayerId,
                        piece.PhysicalObject.PhysicalCollider,
                        false,
                        out PhysicalObjectState placed))
                {
                    return false;
                }

                Quaternion turn = piece.PhysicalObject.Body != null ? piece.PhysicalObject.Body.rotation : piece.transform.rotation;
                placed = placed.WithRotation(new PhysicalRotation(turn.x, turn.y, turn.z, turn.w));
                accepted = TableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(
                    new CommitPhysicalObjectCommand(
                        CreateCommandContext(),
                        state.Id,
                        placed,
                        state.PhysicalRevision,
                        null,
                        AuthoritativeActionRecordMode.Transaction)).Succeeded;
            }

            if (accepted) piece.ApplyAcceptedState();
            return accepted;
        }

        private static Bounds PieceBounds(Component piece)
        {
            Renderer renderer = piece.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.bounds : new Bounds(piece.transform.position, Vector3.zero);
        }

        private void ClearPiecesToMove()
        {
            loosePiecesToMove.Clear();
            containersToMove.Clear();
            consolesToMove.Clear();
            consoleBoundsToMove.Clear();
            boardsToMove.Clear();
            boardBoundsToMove.Clear();
            boardRiders.Clear();
            boardRiderBoard.Clear();
        }

        // The index (in boardsToMove) of the moving board a world point lies on, or -1.
        private int BoardUnder(Vector3 world)
        {
            for (int i = 0; i < boardsToMove.Count; i++)
            {
                if (boardViews.TryGetValue(boardsToMove[i], out GameBoardMatView view) && view != null && view.Contains(world))
                {
                    return i;
                }
            }

            return -1;
        }

        // Moves a placed board mat by a world offset, keeping its turn (as Move Board does, without its message).
        private bool MoveBoardBy(BoardId boardId, Vector3 offset)
        {
            if (!matchState.PlacedBoards.TryGetValue(boardId, out PlacedBoardState board)) return false;
            Vector3 world = coordinateConverter.ToWorldPosition(board.Pose) + offset;
            TabletopPose pose = new TabletopPose(
                coordinateConverter.ToTableCoordinate(world),
                board.Pose.RotationDegrees,
                board.Pose.Layer,
                board.Pose.LocalOrder);
            if (!TableAuthority.Submit<MoveBoardCommand, CommandResult>(
                    new MoveBoardCommand(CreateCommandContext(), boardId, pose, null)).Succeeded)
            {
                return false;
            }
            // R2: the mat was set in its new place by the table's view sync.

            Physics.SyncTransforms();
            return true;
        }

        private static Vector3 Flat(Vector3 offset)
        {
            return new Vector3(offset.x, 0f, offset.z);
        }

        // Every renderer of a piece together (a Console or board is made of several parts). Event-time only.
        private static Bounds AllBounds(Component piece)
        {
            Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(piece.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
