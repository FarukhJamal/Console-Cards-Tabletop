using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>What a drawing on the table is (drawing tool).</summary>
    internal enum TableMarkKind
    {
        Pen = 0,
        Highlighter = 1,
        Label = 2,
    }

    /// <summary>
    /// One drawing on the table: a pen or highlighter line (its points), or a label (one point, its words and the way
    /// it faces). Points are world positions on the surface it was drawn on (the table, a mat, a board). Never changed
    /// once made, so an Undo step can keep it.
    /// </summary>
    internal sealed class TableMark
    {
        internal TableMark(TableMarkKind kind, Color colour, float width, Vector3[] points, string text, float yaw)
        {
            Kind = kind;
            Colour = colour;
            Width = width;
            Points = points ?? Array.Empty<Vector3>();
            Text = text ?? string.Empty;
            Yaw = yaw;
        }

        public TableMarkKind Kind { get; }
        public Color Colour { get; }

        /// <summary>The line's width, or a label's letter height (world units).</summary>
        public float Width { get; }

        public Vector3[] Points { get; }
        public string Text { get; }

        /// <summary>A label's turn on the table (degrees): it reads upright for whoever placed it.</summary>
        public float Yaw { get; }
    }

    /// <summary>
    /// The table's drawings at one moment. Never changed: drawing or erasing makes a new set, so the Undo history can
    /// keep each one as it was.
    /// </summary>
    internal sealed class TableMarkSet
    {
        public static readonly TableMarkSet Empty = new TableMarkSet(Array.Empty<TableMark>());

        private readonly TableMark[] marks;

        private TableMarkSet(TableMark[] marks)
        {
            this.marks = marks;
        }

        public int Count => marks.Length;

        public TableMark this[int index] => marks[index];

        public bool Contains(TableMark mark)
        {
            for (int i = 0; i < marks.Length; i++)
            {
                if (ReferenceEquals(marks[i], mark)) return true;
            }

            return false;
        }

        public TableMarkSet With(TableMark mark)
        {
            if (mark == null) return this;
            TableMark[] next = new TableMark[marks.Length + 1];
            Array.Copy(marks, next, marks.Length);
            next[marks.Length] = mark;
            return new TableMarkSet(next);
        }

        public TableMarkSet Without(TableMark mark)
        {
            int index = -1;
            for (int i = 0; i < marks.Length && index < 0; i++)
            {
                if (ReferenceEquals(marks[i], mark)) index = i;
            }

            if (index < 0) return this;
            if (marks.Length == 1) return Empty;
            TableMark[] next = new TableMark[marks.Length - 1];
            Array.Copy(marks, 0, next, 0, index);
            Array.Copy(marks, index + 1, next, index, marks.Length - index - 1);
            return new TableMarkSet(next);
        }

        public static TableMarkSet Of(List<TableMark> list)
        {
            return list == null || list.Count == 0 ? Empty : new TableMarkSet(list.ToArray());
        }
    }

    /// <summary>
    /// The drawing tool (client checklist item 5; owner's choices 2026-10-20): the Draw tool on the tool bar opens a
    /// panel with Pen, Highlighter (MARKER), Label and Eraser, eight colours (the first is your seat's colour) and
    /// Clear all. Lines lie on whatever is under the pen (the table, mats, game boards; never on cards, pawns, tokens,
    /// dice, piles or Consoles) and stay where they were drawn when pieces move. The eraser takes a whole line or
    /// label away at a touch. Each line, label, erase or Clear is one Undo step; the drawings are kept with the table
    /// (Undo, a rebuilt table) and saved in "Save table as a game"; a new or reset table starts clean.
    /// <para>The drawings are the table's own data, kept here and in each Undo step, not in the match (multiplayer
    /// sends them through the command door later). The lines and labels are copies of the Final World prefabs in the
    /// board feedback set (Console Cards > Feedback > Create Missing Board Feedback Prefabs). Nothing here runs per
    /// frame unless a line is being drawn or the eraser is rubbing, and that allocates nothing.</para>
    /// </summary>
    public sealed partial class TabletopComposition : ITabletopDrawHandler
    {
        private const int DrawModePen = 0;
        private const int DrawModeHighlighter = 1;
        private const int DrawModeLabel = 2;
        private const int DrawModeEraser = 3;
        private const int MaximumTableMarks = 400;
        private const int MaximumStrokePoints = 1024;
        private const float StrokePointSpacing = 0.04f; // table units between two points of a line
        private const float PenLift = 0.012f; // table units above the surface (pens over highlighters)
        private const float HighlighterLift = 0.008f;
        private const float LabelLift = 0.016f;
        private const float HighlighterAlpha = 0.45f;
        private const float LabelPrefabLetterHeight = 0.64f; // the label prefab's letter height at scale 1 (world)
        private const float MarkRayLength = 500f;

        // The swatches; the first is replaced by the seat's colour when the panel is shown.
        private static readonly Color[] DrawPalette =
        {
            new Color(0.878f, 0.325f, 0.122f, 1f),
            new Color(0.114f, 0.114f, 0.122f, 1f),
            new Color(0.98f, 0.97f, 0.93f, 1f),
            new Color(0.86f, 0.2f, 0.18f, 1f),
            new Color(0.95f, 0.66f, 0.11f, 1f),
            new Color(0.18f, 0.65f, 0.4f, 1f),
            new Color(0.2f, 0.45f, 0.85f, 1f),
            new Color(0.55f, 0.3f, 0.75f, 1f),
        };

        private readonly Color[] drawSwatches = new Color[8];
        private readonly Vector3[] strokePoints = new Vector3[MaximumStrokePoints];
        private readonly RaycastHit[] markHits = new RaycastHit[16];
        private readonly List<TableMark> shownMarks = new List<TableMark>(32);
        private readonly List<GameObject> shownMarkObjects = new List<GameObject>(32);
        private TableMarkSet tableMarks = TableMarkSet.Empty;
        private BoardFeedbackSet markSet;
        private bool markSetLooked;
        private GameObject markRoot;
        private int drawMode = DrawModePen;
        private int drawColour;
        private LineRenderer strokeLine;
        private int strokeCount;
        private bool strokeActive;
        private bool eraseActive;
        private int erasedCount;
        private bool labelPressActive;
        private string keysDraw;

        /// <summary>The drawings on the table now (kept in each Undo step).</summary>
        internal TableMarkSet TableMarks => tableMarks;

        // ---------- The Draw tool's readiness and panel ----------

        // The line and label prefabs (in the board feedback set) and the panel on the tool bar.
        private bool DrawToolReady()
        {
            ToolBarView bar = runtimeUi != null ? runtimeUi.ShownToolBar : null;
            return bar != null && bar.HasDrawPanel && EnsureMarkSet();
        }

        private bool EnsureMarkSet()
        {
            if (!markSetLooked)
            {
                markSetLooked = true;
                markSet = Resources.Load<BoardFeedbackSet>(BoardFeedbackSet.ResourcePath);
            }

            return markSet != null && markSet.TableMarkLine != null && markSet.TableMarkLabel != null;
        }

        // Called with the tool bar (RefreshToolBar): binds the panel's buttons and shows it while Draw is chosen.
        private void RefreshDrawPanel()
        {
            ToolBarView bar = runtimeUi != null ? runtimeUi.ShownToolBar : null;
            if (bar == null || !bar.HasDrawPanel) return;
            bar.BindDraw(ChooseDrawMode, ChooseDrawColour, ClearTableMarksByPlayer);
            ShowDrawPanel(activeTool == TableToolKind.Draw);
        }

        private void ShowDrawPanel(bool shown)
        {
            ToolBarView bar = runtimeUi != null ? runtimeUi.ShownToolBar : null;
            if (bar == null || !bar.HasDrawPanel) return;
            bar.ShowDraw(shown);
            if (!shown) return;
            bar.SetDrawMode(drawMode);
            int count = Mathf.Min(drawSwatches.Length, DrawPalette.Length);
            for (int i = 0; i < count; i++) drawSwatches[i] = DrawPalette[i];
            drawSwatches[0] = FeedbackColour(localPlayerId); // your seat's colour
            bar.SetDrawSwatches(drawSwatches, drawColour);
        }

        private void ChooseDrawMode(int mode)
        {
            if (mode < DrawModePen || mode > DrawModeEraser) return;
            CancelDraw();
            drawMode = mode;
            ShowDrawPanel(true);
            RefreshToolBarState();
            if (mode == DrawModeLabel) ShowMessage("Type the label's words in the box, then click the table where it goes.");
        }

        private void ChooseDrawColour(int colour)
        {
            if (colour < 0 || colour >= drawSwatches.Length) return;
            drawColour = colour;
            if (drawMode == DrawModeEraser) drawMode = DrawModePen; // a colour while erasing: back to the pen

            ShowDrawPanel(true);
            RefreshToolBarState();
        }

        private Color CurrentDrawColour(TableMarkKind kind)
        {
            Color colour = drawColour == 0 ? FeedbackColour(localPlayerId) : DrawPalette[Mathf.Clamp(drawColour, 0, DrawPalette.Length - 1)];
            if (kind == TableMarkKind.Highlighter) colour.a = HighlighterAlpha;
            return colour;
        }

        private float MarkUnit()
        {
            return FindClassicTable() ? classicFrame.WorldPerTableUnit : 1f;
        }

        // ---------- ITabletopDrawHandler ----------

        bool ITabletopDrawHandler.WantsDraw()
        {
            return IsInitialized
                && activeTool == TableToolKind.Draw
                && tableModeStart == null
                && (componentPlacementController == null || !componentPlacementController.IsActive)
                && EnsureMarkSet();
        }

        bool ITabletopDrawHandler.TryBeginDraw(Vector2 screenPosition)
        {
            ContextMenus.CloseContextMenu();
            switch (drawMode)
            {
                case DrawModeEraser:
                    eraseActive = true;
                    erasedCount = 0;
                    EraseAt(screenPosition);
                    return true;
                case DrawModeLabel:
                    labelPressActive = true;
                    return true;
                default:
                    if (tableMarks.Count >= MaximumTableMarks)
                    {
                        ShowMessage($"The table holds up to {MaximumTableMarks} drawings. Erase some or Clear all first.");
                        return true;
                    }

                    strokeActive = true;
                    strokeCount = 0;
                    AddStrokePoint(screenPosition);
                    return true;
            }
        }

        void ITabletopDrawHandler.UpdateDraw(Vector2 screenPosition)
        {
            if (strokeActive) AddStrokePoint(screenPosition);
            else if (eraseActive) EraseAt(screenPosition);
        }

        void ITabletopDrawHandler.ReleaseDraw(Vector2 screenPosition)
        {
            if (strokeActive)
            {
                FinishStroke();
            }
            else if (eraseActive)
            {
                FinishErase();
            }
            else if (labelPressActive)
            {
                labelPressActive = false;
                PlaceLabel(screenPosition);
            }
        }

        void ITabletopDrawHandler.CancelDraw()
        {
            CancelDraw();
        }

        // A line being drawn goes away; drawings already erased stay erased (one Undo step brings them back).
        private void CancelDraw()
        {
            if (strokeLine != null) Destroy(strokeLine.gameObject);
            strokeLine = null;
            strokeActive = false;
            strokeCount = 0;
            labelPressActive = false;
            if (eraseActive) FinishErase();
        }

        // ---------- Lines ----------

        private void AddStrokePoint(Vector2 screenPosition)
        {
            if (strokeCount >= MaximumStrokePoints || !TryMarkPoint(screenPosition, out Vector3 point, out Vector3 normal)) return;
            bool highlighter = drawMode == DrawModeHighlighter;
            float unit = MarkUnit();
            point += normal * ((highlighter ? HighlighterLift : PenLift) * unit);
            float spacing = StrokePointSpacing * unit;
            if (strokeCount > 0 && (point - strokePoints[strokeCount - 1]).sqrMagnitude < spacing * spacing) return;
            if (strokeLine == null)
            {
                TableMarkKind kind = highlighter ? TableMarkKind.Highlighter : TableMarkKind.Pen;
                strokeLine = NewMarkLine(kind, CurrentDrawColour(kind), LineWidth(kind));
                if (strokeLine == null)
                {
                    strokeActive = false;
                    return;
                }
            }

            strokePoints[strokeCount] = point;
            strokeCount++;
            strokeLine.positionCount = strokeCount;
            strokeLine.SetPosition(strokeCount - 1, point);
        }

        private float LineWidth(TableMarkKind kind)
        {
            float unit = MarkUnit();
            return (kind == TableMarkKind.Highlighter ? markSet.HighlighterWidth : markSet.PenWidth) * unit;
        }

        // The line is kept: one Undo step. A click without moving leaves a dot.
        private void FinishStroke()
        {
            strokeActive = false;
            if (strokeLine == null || strokeCount == 0)
            {
                if (strokeLine != null) Destroy(strokeLine.gameObject);
                strokeLine = null;
                strokeCount = 0;
                return;
            }

            if (strokeCount == 1)
            {
                strokePoints[1] = strokePoints[0] + new Vector3(0.002f, 0f, 0f);
                strokeCount = 2;
                strokeLine.positionCount = 2;
                strokeLine.SetPosition(1, strokePoints[1]);
            }

            Vector3[] points = new Vector3[strokeCount];
            Array.Copy(strokePoints, points, strokeCount);
            bool highlighter = drawMode == DrawModeHighlighter;
            TableMarkKind kind = highlighter ? TableMarkKind.Highlighter : TableMarkKind.Pen;
            TableMark mark = new TableMark(kind, strokeLine.startColor, strokeLine.widthMultiplier, points, null, 0f);
            tableMarks = tableMarks.With(mark);
            shownMarks.Add(mark);
            shownMarkObjects.Add(strokeLine.gameObject);
            strokeLine = null;
            strokeCount = 0;
            SessionService.RecordTableMarkStep(highlighter ? "highlighted on the table" : "drew a line");
        }

        // ---------- Labels ----------

        private void PlaceLabel(Vector2 screenPosition)
        {
            ToolBarView bar = runtimeUi != null ? runtimeUi.ShownToolBar : null;
            string words = bar != null ? bar.DrawLabelText : string.Empty;
            words = words != null ? words.Trim() : string.Empty;
            if (words.Length == 0)
            {
                ShowMessage("Type the label's words in the box first, then click the table.");
                return;
            }

            if (tableMarks.Count >= MaximumTableMarks)
            {
                ShowMessage($"The table holds up to {MaximumTableMarks} drawings. Erase some or Clear all first.");
                return;
            }

            if (!TryMarkPoint(screenPosition, out Vector3 point, out Vector3 normal))
            {
                ShowMessage("Click on the table (or a mat or board) to place the label.");
                return;
            }

            float unit = MarkUnit();
            point += normal * (LabelLift * unit);
            float yaw = cameraInputAdapter != null && cameraInputAdapter.CameraController != null
                ? cameraInputAdapter.CameraController.YawDegrees
                : 0f;
            TableMark mark = new TableMark(
                TableMarkKind.Label,
                CurrentDrawColour(TableMarkKind.Label),
                markSet.LabelHeight * unit,
                new[] { point },
                words,
                yaw);
            GameObject shown = NewMarkObject(mark);
            if (shown == null) return;
            tableMarks = tableMarks.With(mark);
            shownMarks.Add(mark);
            shownMarkObjects.Add(shown);
            SessionService.RecordTableMarkStep("placed a label");
        }

        // ---------- Erasing ----------

        // Every line or label the eraser touches goes, whole (owner's choice).
        private void EraseAt(Vector2 screenPosition)
        {
            if (shownMarks.Count == 0 || !TryMarkPoint(screenPosition, out Vector3 point, out _)) return;
            float radius = markSet.EraserRadius * MarkUnit();
            for (int i = shownMarks.Count - 1; i >= 0; i--)
            {
                TableMark mark = shownMarks[i];
                if (!MarkTouches(mark, point, radius)) continue;
                if (shownMarkObjects[i] != null) Destroy(shownMarkObjects[i]);
                shownMarks.RemoveAt(i);
                shownMarkObjects.RemoveAt(i);
                tableMarks = tableMarks.Without(mark);
                erasedCount++;
            }
        }

        private void FinishErase()
        {
            eraseActive = false;
            int erased = erasedCount;
            erasedCount = 0;
            if (erased == 0) return;
            SessionService.RecordTableMarkStep(erased == 1 ? "erased a drawing" : $"erased {erased} drawings");
        }

        // Flat distance (seen from above) from the eraser to the line or label.
        private static bool MarkTouches(TableMark mark, Vector3 point, float radius)
        {
            Vector3[] points = mark.Points;
            if (points.Length == 0) return false;
            if (mark.Kind == TableMarkKind.Label)
            {
                float reach = Mathf.Max(radius + (0.5f * mark.Width), 0.3f * mark.Width * mark.Text.Length);
                return FlatDistanceSquared(points[0], point) <= reach * reach;
            }

            float touch = radius + (0.5f * mark.Width);
            float touchSquared = touch * touch;
            if (points.Length == 1) return FlatDistanceSquared(points[0], point) <= touchSquared;
            for (int i = 1; i < points.Length; i++)
            {
                if (FlatSegmentDistanceSquared(points[i - 1], points[i], point) <= touchSquared) return true;
            }

            return false;
        }

        private static float FlatDistanceSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return (x * x) + (z * z);
        }

        private static float FlatSegmentDistanceSquared(Vector3 a, Vector3 b, Vector3 point)
        {
            float abX = b.x - a.x;
            float abZ = b.z - a.z;
            float lengthSquared = (abX * abX) + (abZ * abZ);
            float t = lengthSquared <= 1e-8f
                ? 0f
                : Mathf.Clamp01((((point.x - a.x) * abX) + ((point.z - a.z) * abZ)) / lengthSquared);
            float x = a.x + (abX * t) - point.x;
            float z = a.z + (abZ * t) - point.z;
            return (x * x) + (z * z);
        }

        // ---------- Clear all ----------

        private void ClearTableMarksByPlayer()
        {
            CancelDraw();
            if (tableMarks.Count == 0)
            {
                ShowMessage("Nothing is drawn on the table.");
                return;
            }

            int count = tableMarks.Count;
            ShowTableMarks(TableMarkSet.Empty);
            SessionService.RecordTableMarkStep("cleared the drawings");
            ShowMessage(count == 1 ? "Cleared 1 drawing (Undo brings it back)." : $"Cleared {count} drawings (Undo brings them back).");
        }

        // ---------- Where the pen touches ----------

        // The nearest surface under the pointer that can be drawn on: the table, a mat or a board, and a game's fixed
        // board tiles (anything else that is not a piece, a pile, a Console or the hand).
        private bool TryMarkPoint(Vector2 screenPosition, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            UnityCamera camera = targetCamera;
            if (camera == null) return false;
            Ray ray = camera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, markHits, MarkRayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = markHits[i];
                if (hit.distance >= nearest || !IsDrawSurface(hit.collider)) continue;
                nearest = hit.distance;
                point = hit.point;
                normal = hit.normal.y > 0.1f ? hit.normal : Vector3.up;
                found = true;
            }

            return found;
        }

        private bool IsDrawSurface(Collider collider)
        {
            if (collider == null) return false;
            TabletopObjectView piece = collider.GetComponentInParent<TabletopObjectView>();
            if (piece != null)
            {
                // A game's board tiles never move, so they are part of the board (Trap Floor's floor).
                return piece is CardView
                    && piece.IsBound
                    && piece.BoundState != null
                    && ActiveCartridge.IsFixedBoardPiece(piece.BoundState.Id);
            }

            return collider.GetComponentInParent<IContainerLayoutView>() == null
                && collider.GetComponentInParent<IContainerView>() == null
                && collider.GetComponentInParent<ConsoleView>() == null
                && collider.GetComponentInParent<PileBayView>() == null
                && collider.GetComponentInParent<HandTrayRig>() == null;
        }

        // ---------- What is shown ----------

        /// <summary>
        /// Shows exactly these drawings (Undo and Redo, a new or reset table, a game laid out). Drawings already shown
        /// stay; the others are made or taken away. Event-time only.
        /// </summary>
        internal void ShowTableMarks(TableMarkSet marks)
        {
            if (strokeLine != null) Destroy(strokeLine.gameObject);
            strokeLine = null;
            strokeActive = false;
            strokeCount = 0;
            eraseActive = false;
            erasedCount = 0;
            labelPressActive = false;
            tableMarks = marks ?? TableMarkSet.Empty;
            for (int i = shownMarks.Count - 1; i >= 0; i--)
            {
                if (tableMarks.Contains(shownMarks[i])) continue;
                if (shownMarkObjects[i] != null) Destroy(shownMarkObjects[i]);
                shownMarks.RemoveAt(i);
                shownMarkObjects.RemoveAt(i);
            }

            for (int i = 0; i < tableMarks.Count; i++)
            {
                TableMark mark = tableMarks[i];
                if (shownMarks.Contains(mark)) continue;
                GameObject shown = NewMarkObject(mark);
                if (shown == null) continue;
                shownMarks.Add(mark);
                shownMarkObjects.Add(shown);
            }
        }

        private GameObject NewMarkObject(TableMark mark)
        {
            if (mark.Kind == TableMarkKind.Label)
            {
                if (!EnsureMarkSet() || mark.Points.Length == 0) return null;
                TextMesh label = Instantiate(markSet.TableMarkLabel, MarkRoot());
                label.text = mark.Text;
                label.color = mark.Colour;
                label.transform.SetPositionAndRotation(mark.Points[0], Quaternion.Euler(90f, mark.Yaw, 0f));
                float scale = mark.Width / LabelPrefabLetterHeight;
                label.transform.localScale = new Vector3(scale, scale, scale);
                return label.gameObject;
            }

            LineRenderer line = NewMarkLine(mark.Kind, mark.Colour, mark.Width);
            if (line == null) return null;
            line.positionCount = mark.Points.Length;
            line.SetPositions(mark.Points);
            return line.gameObject;
        }

        // A flat line from the prefab, in this colour and width (a highlighter lies under pen lines).
        private LineRenderer NewMarkLine(TableMarkKind kind, Color colour, float width)
        {
            if (!EnsureMarkSet()) return null;
            LineRenderer line = Instantiate(markSet.TableMarkLine, MarkRoot());
            line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.positionCount = 0;
            line.widthMultiplier = width;
            line.startColor = colour;
            line.endColor = colour;
            line.sortingOrder = kind == TableMarkKind.Highlighter ? 0 : 1;
            return line;
        }

        private Transform MarkRoot()
        {
            if (markRoot == null)
            {
                markRoot = new GameObject("Table Drawings");
                SceneManager.MoveGameObjectToScene(markRoot, gameObject.scene);
            }

            return markRoot.transform;
        }

        // The table is going away for good (OnDestroy).
        private void DestroyTableMarks()
        {
            shownMarks.Clear();
            shownMarkObjects.Clear();
            tableMarks = TableMarkSet.Empty;
            strokeLine = null;
            if (markRoot != null) Destroy(markRoot);
            markRoot = null;
        }

        // ---------- Saving with the table ("Save table as a game") ----------

        /// <summary>Writes the drawings into a saved table: table coordinates and a height above the table's top.</summary>
        internal void SaveTableMarks(List<SavedMark> saved)
        {
            if (saved == null || coordinateConverter == null) return;
            Vector3 up = coordinateConverter.WorldUp;
            for (int i = 0; i < tableMarks.Count; i++)
            {
                TableMark mark = tableMarks[i];
                SavedMark entry = new SavedMark
                {
                    kind = mark.Kind == TableMarkKind.Label
                        ? SavedMark.Label
                        : mark.Kind == TableMarkKind.Highlighter ? SavedMark.Highlighter : SavedMark.Pen,
                    r = mark.Colour.r,
                    g = mark.Colour.g,
                    b = mark.Colour.b,
                    a = mark.Colour.a,
                    width = mark.Width / Mathf.Max(0.0001f, MarkUnit()),
                    text = mark.Text,
                    yaw = mark.Yaw,
                };
                for (int p = 0; p < mark.Points.Length; p++)
                {
                    Vector3 world = mark.Points[p];
                    TableCoordinate coordinate = coordinateConverter.ToTableCoordinate(world);
                    float height = Vector3.Dot(world - coordinateConverter.ToWorldPosition(coordinate), up);
                    entry.points.Add((float)coordinate.X);
                    entry.points.Add((float)coordinate.Y);
                    entry.points.Add(height);
                }

                saved.Add(entry);
            }
        }

        /// <summary>
        /// Puts a saved table's drawings on the table, as part of its start (no Undo step of their own). Drawings
        /// already there stay.
        /// </summary>
        internal void LoadTableMarks(List<SavedMark> saved)
        {
            if (saved == null || saved.Count == 0 || coordinateConverter == null) return;
            Vector3 up = coordinateConverter.WorldUp;
            float unit = MarkUnit();
            List<TableMark> marks = new List<TableMark>(tableMarks.Count + saved.Count);
            for (int i = 0; i < tableMarks.Count; i++) marks.Add(tableMarks[i]);
            for (int i = 0; i < saved.Count && marks.Count < MaximumTableMarks; i++)
            {
                SavedMark entry = saved[i];
                if (entry == null || entry.points == null || entry.points.Count < 3) continue;
                int count = Mathf.Min(entry.points.Count / 3, MaximumStrokePoints);
                Vector3[] points = new Vector3[count];
                for (int p = 0; p < count; p++)
                {
                    TableCoordinate coordinate = new TableCoordinate(entry.points[3 * p], entry.points[(3 * p) + 1]);
                    points[p] = coordinateConverter.ToWorldPosition(coordinate) + (up * entry.points[(3 * p) + 2]);
                }

                TableMarkKind kind = entry.kind == SavedMark.Label
                    ? TableMarkKind.Label
                    : entry.kind == SavedMark.Highlighter ? TableMarkKind.Highlighter : TableMarkKind.Pen;
                if (kind == TableMarkKind.Label && string.IsNullOrWhiteSpace(entry.text)) continue;
                marks.Add(new TableMark(
                    kind,
                    new Color(entry.r, entry.g, entry.b, entry.a),
                    Mathf.Max(0.001f, entry.width) * unit,
                    points,
                    entry.text,
                    entry.yaw));
            }

            ShowTableMarks(TableMarkSet.Of(marks));
            SessionService.FoldTableMarksIntoCurrentStep();
        }
    }
}
