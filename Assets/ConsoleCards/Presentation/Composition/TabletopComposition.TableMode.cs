using System;
using System.Collections.Generic;
using System.Globalization;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Table mode (doc 30, B3; T3): the Table menu's "Table tools…" opens a side panel and turns the camera to the
    /// top view. Shape buttons, sizes (− / + with Shift for big steps, or typed in), corners, star points and inner
    /// size, the edge and its size. Every change shows at once and is one Undo step (up to 16) inside Table mode;
    /// Done keeps the table (saved in the profile), Cancel or Esc puts back the table Table mode started with.
    /// A built table never shrinks past a piece standing on it, nor below the classic playing area while a game is
    /// on the table, so nothing falls off and every game layout still fits.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const int TableUndoLimit = 16;
        private static readonly TableShape[] PanelShapes =
        {
            TableShape.Classic, TableShape.Rectangle, TableShape.Square, TableShape.Round, TableShape.Oval, TableShape.Star,
        };

        private readonly List<TableSpec> tableUndo = new List<TableSpec>(TableUndoLimit);
        private readonly List<TableSpec> tableRedo = new List<TableSpec>(TableUndoLimit);
        private readonly TablePanelModel tablePanelModel = new TablePanelModel();
        private readonly TableOutline holdOutline = new TableOutline(256);
        private readonly List<Bounds> tablePieces = new List<Bounds>(64);
        private TablePanelActions tablePanelActions;
        private Action openTableModeAction;
        private TableSpec tableModeStart;

        private Action OpenTableModeAction
        {
            get
            {
                if (openTableModeAction == null) openTableModeAction = OpenTableMode;
                return openTableModeAction;
            }
        }

        private void OpenTableMode()
        {
            if (runtimeUi == null || !FindClassicTable()) return;
            tableModeStart = tableSpec.Copy();
            tableUndo.Clear();
            tableRedo.Clear();
            cameraInputAdapter?.CameraController?.ShowPreset(TabletopCameraPreset.TopDown);
            RefreshTablePanel("Changes show on the table at once. Done keeps them; Cancel or Esc puts the table back.");
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
            };

            TablePanelModel model = tablePanelModel;
            TableShape shape = tableSpec.shape;
            bool built = IsBuiltShape(shape);
            model.SelectedShape = Array.IndexOf(PanelShapes, shape);
            model.ShowModels = NextCycleModel(-1) >= 0;
            model.ModelText = shape == TableShape.Model ? TableShapeName(tableSpec) : "Choose…";
            SetField(model, TableField.Width, built, tableSpec.width.ToString("0.0", CultureInfo.InvariantCulture));
            SetField(model, TableField.Depth, shape == TableShape.Rectangle || shape == TableShape.Oval,
                tableSpec.depth.ToString("0.0", CultureInfo.InvariantCulture));
            SetField(model, TableField.Corners, shape == TableShape.Rectangle || shape == TableShape.Square,
                tableSpec.cornerRadius.ToString("0.00", CultureInfo.InvariantCulture));
            SetField(model, TableField.Points, shape == TableShape.Star, tableSpec.starPoints.ToString(CultureInfo.InvariantCulture));
            SetField(model, TableField.InnerSize, shape == TableShape.Star,
                Mathf.RoundToInt(tableSpec.starInner * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            bool hasEdge = built && tableSpec.edge != TableEdge.None;
            SetField(model, TableField.EdgeHeight, hasEdge, tableSpec.edgeHeight.ToString("0.00", CultureInfo.InvariantCulture));
            SetField(model, TableField.EdgeWidth, hasEdge, tableSpec.edgeWidth.ToString("0.00", CultureInfo.InvariantCulture));
            model.ShowEdge = built;
            model.EdgeText = TableSpec.EdgeLabel(tableSpec.edge);
            model.CanUndo = tableUndo.Count > 0;
            model.CanRedo = tableRedo.Count > 0;
            model.Status = built || shape == TableShape.Classic || string.IsNullOrEmpty(status)
                ? status
                : $"{status} A model keeps its own size.";
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

        // One change: checked (pieces and the game's room), shown at once, and one Undo step.
        private void ApplyTableModeChange(TableSpec candidate, string done)
        {
            candidate.Clamp();
            if (IsBuiltShape(candidate.shape) && !TableHolds(candidate, out string why))
            {
                RefreshTablePanel(why);
                return;
            }

            if (!ShowTable(candidate, out string error))
            {
                RefreshTablePanel($"Not changed: {error}.");
                return;
            }

            tableUndo.Add(tableSpec);
            if (tableUndo.Count > TableUndoLimit) tableUndo.RemoveAt(0);
            tableRedo.Clear();
            tableSpec = candidate;
            RefreshTablePanel(done);
        }

        private void PanelPickShape(int index)
        {
            if (index < 0 || index >= PanelShapes.Length || PanelShapes[index] == tableSpec.shape) return;
            TableShape shape = PanelShapes[index];
            TableSpec candidate = shape == TableShape.Classic
                ? new TableSpec { shape = TableShape.Classic }
                : TableSpec.DefaultFor(shape, classicFrame.ClassicSize.x, classicFrame.ClassicSize.y);
            KeepEdgeAndStar(candidate);
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
            KeepEdgeAndStar(candidate);
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
                case TableField.Points:
                    candidate.starPoints += direction;
                    break;
                case TableField.InnerSize:
                    candidate.starInner = Snap(candidate.starInner + (direction * (big ? 0.1f : 0.05f)), 0.05f);
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

        // A typed value is taken as it is (no snapping). Inner size may be typed as a percentage.
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
                case TableField.Points: candidate.starPoints = Mathf.RoundToInt(value); break;
                case TableField.InnerSize: candidate.starInner = value > 1f ? value / 100f : value; break;
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
            TableSpec previous = from[from.Count - 1];
            if (!ShowTable(previous, out string error))
            {
                RefreshTablePanel($"Not changed: {error}.");
                return;
            }

            from.RemoveAt(from.Count - 1);
            to.Add(tableSpec);
            tableSpec = previous;
            RefreshTablePanel(done);
        }

        private void PanelReset()
        {
            if (!IsBuiltShape(tableSpec.shape)) return;
            TableSpec candidate = TableSpec.DefaultFor(tableSpec.shape, classicFrame.ClassicSize.x, classicFrame.ClassicSize.y);
            KeepEdgeAndStar(candidate);
            ApplyTableModeChange(candidate, "Size reset.");
        }

        private void PanelCancel()
        {
            if (tableModeStart != null && ShowTable(tableModeStart, out _)) tableSpec = tableModeStart;
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
            tableUndo.Clear();
            tableRedo.Clear();
            runtimeUi?.CloseTablePanel();
            cameraInputAdapter?.CameraController?.ShowDefaultView();
            RefreshTableShapeEntry();
            ShowMessage(message);
        }

        // A new shape keeps the chosen edge, its size and the star settings.
        private void KeepEdgeAndStar(TableSpec candidate)
        {
            candidate.edge = tableSpec.edge;
            candidate.edgeHeight = tableSpec.edgeHeight;
            candidate.edgeWidth = tableSpec.edgeWidth;
            candidate.starPoints = tableSpec.starPoints;
            candidate.starInner = tableSpec.starInner;
        }

        private static float Snap(float value, float step) => Mathf.Round(value / step) * step;

        // True when the built table for the spec still holds every piece standing on the table now, and, while a
        // game is on the table, the whole classic playing area its layout was made for. Event-time only (it looks
        // the pieces up), never per frame.
        private bool TableHolds(TableSpec spec, out string why)
        {
            why = null;
            float unit = classicFrame.WorldPerTableUnit;
            if (!holdOutline.Build(spec, unit, 1f))
            {
                why = "That shape and size do not make a usable table.";
                return false;
            }

            Quaternion inverse = Quaternion.Inverse(classicFrame.Rotation);
            Vector3 centre = classicFrame.Centre;
            if (activeSession != null && activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom)
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

            CollectTablePieces(tablePieces);
            for (int i = 0; i < tablePieces.Count; i++)
            {
                Bounds piece = tablePieces[i];
                Vector3 local = inverse * (piece.center - centre);
                if (Mathf.Abs(local.y) > 3f * unit || !OnTableNow(piece.center, local)) continue;
                Vector2 flat = new Vector2(local.x, local.z);
                float reach = 0.5f * Mathf.Max(piece.extents.x, piece.extents.z);
                if (!holdOutline.Contains(flat) || holdOutline.DistanceToOutline(flat) < reach)
                {
                    why = "A piece is in the way near the edge, so the table stops here. Move it in, then try again.";
                    return false;
                }
            }

            return true;
        }

        // On the table shown now: the built table's outline, or else the classic playing area.
        private bool OnTableNow(Vector3 world, Vector3 local)
        {
            if (generatedTable != null && generatedTable.gameObject.activeSelf) return generatedTable.Contains(world);
            Vector2 half = 0.5f * classicFrame.WorldPerTableUnit * classicFrame.ClassicSize;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y;
        }

        private static void CollectTablePieces(List<Bounds> into)
        {
            into.Clear();
            AddTablePieces<TabletopObjectView>(into);
            AddTablePieces<DeckView>(into);
            AddTablePieces<DiscardPileView>(into);
            AddTablePieces<StackView>(into);
            AddTablePieces<ConsoleView>(into);
            AddTablePieces<PileBayView>(into);
            AddTablePieces<GameBoardMatView>(into);
        }

        private static void AddTablePieces<T>(List<Bounds> into) where T : Component
        {
            foreach (T piece in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Renderer renderer = piece.GetComponentInChildren<Renderer>();
                into.Add(renderer != null ? renderer.bounds : new Bounds(piece.transform.position, Vector3.zero));
            }
        }
    }
}
