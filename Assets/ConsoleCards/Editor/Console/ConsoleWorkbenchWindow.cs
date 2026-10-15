using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConsoleCards.Editor.ConsoleMat
{
    /// <summary>
    /// Console Workbench (doc 22 §14): a separate window showing the real Console mat prefab from above, with test
    /// cards and dice. Flip the mat, move and resize slots and their colliders, drop test pieces to check how they
    /// snap (symbols, magnet distance, die spots), check the four-seat spacing, compare sizes, and apply the layout
    /// to the Console prefab. Editor only; it never changes the game while it runs.
    /// </summary>
    public sealed class ConsoleWorkbenchWindow : EditorWindow
    {
        private const float PanelWidth = 320f;
        private const float CardWidth = 1f;
        private const float CardDepth = 1.4f;
        private const float DieSize = 0.6f;
        private const float PieceHeight = 0.045f;
        private const float HandleRadius = 6f;
        private const string BaseBoxPath = "Assets/ConsoleCards/Content/Catalogs/BaseBox.asset";

        private static readonly Color CardSlotColour = new Color(0.33f, 0.94f, 0.86f);
        private static readonly Color DieSpotColour = new Color(1f, 0.85f, 0.2f);
        private static readonly Color SelectedColour = new Color(1f, 0.55f, 0.15f);
        private static readonly Color AcceptColour = new Color(0.35f, 0.95f, 0.35f);
        private static readonly Color RefuseColour = new Color(0.95f, 0.3f, 0.3f);

        [SerializeField] private GameObject consolePrefab;
        [SerializeField] private float magnetDistance = 1.1f;
        [SerializeField] private float dieSpotRadius = 0.45f;
        [SerializeField] private float avatarCardScale = 1.486f;
        [SerializeField] private float floorCardScale = 0.62f;
        [SerializeField] private bool showBack;
        [SerializeField] private bool showColliders = true;
        [SerializeField] private bool showCardOutlines = true;
        [SerializeField] private bool showCubeSpots;
        [SerializeField] private bool applyOnRelease = true;
        [SerializeField] private float zoom = 3.4f;
        [SerializeField] private Vector2 pan;

        private PreviewRenderUtility preview;
        private GameObject matInstance;
        private GameObject cardPrefab;
        private ConsoleLayoutDefinition layout;
        private readonly List<TestPiece> pieces = new List<TestPiece>();
        private readonly List<string> checkResults = new List<string>();
        private Vector2 panelScroll;
        private int selectedSlot = -1;
        private int selectedPiece = -1;
        private DragMode drag;
        private Vector2 dragOffset;
        private Vector2 lastMouse;
        private string snapStatus = string.Empty;

        private enum DragMode { None, MoveSlot, ResizeSlot, MovePiece, Pan }

        private enum PieceKind { Card, AvatarCard, Die }

        private sealed class TestPiece
        {
            public PieceKind Kind;
            public GameObject Instance;
            public Vector2 Position;
            public ConsoleSymbol Symbol;
            public bool Landscape;
            public int Value = 1;
            public string SnappedKey = string.Empty;
            public ConsoleMatSide Side;
        }

        [MenuItem("Console Cards/Console/Console Workbench")]
        public static void Open()
        {
            ConsoleWorkbenchWindow window = GetWindow<ConsoleWorkbenchWindow>("Console Workbench");
            window.minSize = new Vector2(900f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            if (consolePrefab == null) consolePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConsoleMatUpgradeTool.PrefabPath);
            Undo.undoRedoPerformed += OnUndoRedo;
            BuildPreview();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            DestroyPreview();
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        // ---- Preview scene ----

        private void BuildPreview()
        {
            DestroyPreview();
            preview = new PreviewRenderUtility();
            preview.camera.orthographic = true;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(55f, 25f, 0f);
            preview.ambientColor = new Color(0.45f, 0.45f, 0.45f);

            ConsoleLayoutBinding binding = consolePrefab != null ? consolePrefab.GetComponent<ConsoleLayoutBinding>() : null;
            layout = binding != null ? binding.Layout : null;
            if (consolePrefab != null)
            {
                matInstance = Object.Instantiate(consolePrefab);
                matInstance.hideFlags = HideFlags.HideAndDontSave;
                matInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                preview.AddSingleGO(matInstance);
                ShowSide(showBack);
            }

            cardPrefab = FindCatalogPrefab(ComponentCatalogKind.Card);
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].Instance = CreatePieceInstance(pieces[i]);
            }
        }

        private void DestroyPreview()
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Instance != null) Object.DestroyImmediate(pieces[i].Instance);
                pieces[i].Instance = null;
            }

            if (matInstance != null) Object.DestroyImmediate(matInstance);
            matInstance = null;
            preview?.Cleanup();
            preview = null;
        }

        private static GameObject FindCatalogPrefab(ComponentCatalogKind kind)
        {
            ComponentCatalog baseBox = AssetDatabase.LoadAssetAtPath<ComponentCatalog>(BaseBoxPath);
            return baseBox != null && baseBox.TryGetFirst(kind, out ComponentCatalogEntry entry) ? entry.Prefab : null;
        }

        private GameObject CreatePieceInstance(TestPiece piece)
        {
            if (preview == null) return null;
            GameObject instance;
            if (piece.Kind == PieceKind.Die || cardPrefab == null)
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(instance.GetComponent<Collider>());
                instance.transform.localScale = piece.Kind == PieceKind.Die
                    ? Vector3.one * DieSize
                    : new Vector3(CardWidth, 0.02f, CardDepth);
            }
            else
            {
                instance = Object.Instantiate(cardPrefab);
                if (piece.Kind == PieceKind.AvatarCard)
                {
                    instance.transform.localScale = new Vector3(avatarCardScale, 1f, avatarCardScale);
                }
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            preview.AddSingleGO(instance);
            PlacePiece(piece, instance);
            return instance;
        }

        private void PlacePiece(TestPiece piece, GameObject instance)
        {
            if (instance == null) return;
            float height = piece.Kind == PieceKind.Die ? (DieSize * 0.5f) + 0.01f : PieceHeight;
            instance.transform.SetPositionAndRotation(
                new Vector3(piece.Position.x, height, piece.Position.y),
                Quaternion.Euler(0f, piece.Landscape ? 90f : 0f, 0f));
            bool hidden = !string.IsNullOrEmpty(piece.SnappedKey) && (piece.Side == ConsoleMatSide.Back) != showBack;
            instance.SetActive(!hidden);
        }

        private void ShowSide(bool back)
        {
            showBack = back;
            ConsoleMatFaces faces = matInstance != null ? matInstance.GetComponent<ConsoleMatFaces>() : null;
            if (faces != null && faces.TurnPivot != null)
            {
                faces.TurnPivot.localRotation = back ? Quaternion.AngleAxis(180f, Vector3.right) : Quaternion.identity;
            }

            for (int i = 0; i < pieces.Count; i++) PlacePiece(pieces[i], pieces[i].Instance);
            selectedSlot = -1;
        }

        // ---- GUI ----

        private void OnGUI()
        {
            Rect panel = new Rect(0f, 0f, PanelWidth, position.height);
            Rect canvas = new Rect(PanelWidth, 0f, position.width - PanelWidth, position.height);
            DrawCanvas(canvas);
            GUILayout.BeginArea(panel, EditorStyles.helpBox);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            DrawPanel();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPanel()
        {
            EditorGUILayout.LabelField("Console Workbench", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            consolePrefab = (GameObject)EditorGUILayout.ObjectField("Console prefab", consolePrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) BuildPreview();
            if (layout == null)
            {
                EditorGUILayout.HelpBox("The Console prefab needs a Console Layout Binding with a layout.", MessageType.Warning);
                return;
            }

            EditorGUILayout.ObjectField("Layout", layout, typeof(ConsoleLayoutDefinition), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mat", EditorStyles.boldLabel);
            if (GUILayout.Button(showBack ? "Flip (showing the back: controller)" : "Flip (showing the front: symbols)", GUILayout.Height(26f)))
            {
                ShowSide(!showBack);
            }

            showColliders = EditorGUILayout.Toggle("Show slot colliders", showColliders);
            showCardOutlines = EditorGUILayout.Toggle("Show card outlines", showCardOutlines);
            showCubeSpots = EditorGUILayout.Toggle("Show cube spots", showCubeSpots);
            zoom = EditorGUILayout.Slider("Zoom (half height)", zoom, 1f, 12f);
            if (GUILayout.Button("Frame the mat"))
            {
                pan = Vector2.zero;
                zoom = 3.4f;
            }

            DrawSelectedSlot();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add a slot (at the view centre, this side)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Card slot")) AddSlot(ConsoleLayoutSlotKind.Card);
                if (GUILayout.Button("Die spot")) AddSlot(ConsoleLayoutSlotKind.Die);
            }

            DrawTestPieces();
            DrawSnapSettings();
            DrawChecks();
            DrawSizes();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            applyOnRelease = EditorGUILayout.Toggle("Apply to prefab on release", applyOnRelease);
            if (GUILayout.Button("Apply layout to Console prefab", GUILayout.Height(26f))) ApplyToPrefab(true);
        }

        private void DrawSelectedSlot()
        {
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty slots = so.FindProperty("slots");
            if (selectedSlot < 0 || selectedSlot >= slots.arraySize) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected slot", EditorStyles.boldLabel);
            SerializedProperty slot = slots.GetArrayElementAtIndex(selectedSlot);
            EditorGUI.BeginChangeCheck();
            DrawField(slot, "key", "Name");
            DrawField(slot, "kind", "Kind");
            DrawField(slot, "side", "Side");
            DrawField(slot, "symbol", "Symbol");
            DrawField(slot, "x", "X (right)");
            DrawField(slot, "z", "Z (up the picture)");
            DrawField(slot, "footprintWidth", "Collider width");
            DrawField(slot, "footprintDepth", "Collider depth");
            DrawField(slot, "defaultOrientation", "Card orientation");
            DrawField(slot, "defaultCapacity", "Capacity (0 = any)");
            bool changed = EditorGUI.EndChangeCheck();
            if (slot.FindPropertyRelative("kind").intValue == (int)ConsoleLayoutSlotKind.Card && GUILayout.Button("Fit collider to one card"))
            {
                bool landscape = slot.FindPropertyRelative("defaultOrientation").intValue == (int)ConsoleSlotOrientation.Landscape;
                slot.FindPropertyRelative("footprintWidth").floatValue = (landscape ? CardDepth : CardWidth) + 0.2f;
                slot.FindPropertyRelative("footprintDepth").floatValue = (landscape ? CardWidth : CardDepth) + 0.2f;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                if (applyOnRelease) ApplyToPrefab(false);
            }
        }

        private static void DrawField(SerializedProperty slot, string name, string label)
        {
            SerializedProperty property = slot.FindPropertyRelative(name);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private void DrawTestPieces()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test pieces (drag them on the mat)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Card")) AddPiece(PieceKind.Card);
                if (GUILayout.Button("Avatar card")) AddPiece(PieceKind.AvatarCard);
                if (GUILayout.Button("Die")) AddPiece(PieceKind.Die);
            }

            if (selectedPiece >= 0 && selectedPiece < pieces.Count)
            {
                TestPiece piece = pieces[selectedPiece];
                EditorGUILayout.LabelField($"Selected: {piece.Kind}" + (string.IsNullOrEmpty(piece.SnappedKey) ? " (on the table)" : $" in {piece.SnappedKey}"));
                EditorGUI.BeginChangeCheck();
                if (piece.Kind == PieceKind.Die)
                {
                    piece.Value = EditorGUILayout.IntSlider("Shows", piece.Value, 1, 6);
                }
                else
                {
                    piece.Symbol = (ConsoleSymbol)EditorGUILayout.EnumPopup("Card symbol", piece.Symbol);
                    piece.Landscape = EditorGUILayout.Toggle("Landscape", piece.Landscape);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    SnapPiece(piece);
                    PlacePiece(piece, piece.Instance);
                }

                if (GUILayout.Button("Remove this piece")) RemovePiece(selectedPiece);
            }

            if (pieces.Count > 0 && GUILayout.Button("Remove all pieces"))
            {
                while (pieces.Count > 0) RemovePiece(pieces.Count - 1);
            }

            if (!string.IsNullOrEmpty(snapStatus)) EditorGUILayout.HelpBox(snapStatus, MessageType.None);
        }

        private void DrawSnapSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Snapping (match the composition's values)", EditorStyles.boldLabel);
            magnetDistance = EditorGUILayout.FloatField("Symbol magnet distance", magnetDistance);
            dieSpotRadius = EditorGUILayout.FloatField("Die spot radius", dieSpotRadius);
        }

        private void DrawChecks()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);
            if (GUILayout.Button("Run checks")) RunChecks();
            for (int i = 0; i < checkResults.Count; i++)
            {
                bool problem = checkResults[i].StartsWith("✗", StringComparison.Ordinal);
                EditorGUILayout.HelpBox(checkResults[i], problem ? MessageType.Warning : MessageType.Info);
            }
        }

        private void DrawSizes()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sizes on the table (1 unit = 1 world unit)", EditorStyles.boldLabel);
            avatarCardScale = EditorGUILayout.FloatField("Avatar card scale", avatarCardScale);
            floorCardScale = EditorGUILayout.FloatField("Floor tile card scale", floorCardScale);
            EditorGUILayout.LabelField($"Console mat: {MatWidth():0.###} × {MatDepth():0.###}");
            EditorGUILayout.LabelField($"Card: {CardWidth:0.###} × {CardDepth:0.###}");
            EditorGUILayout.LabelField($"Avatar card: {CardWidth * avatarCardScale:0.###} × {CardDepth * avatarCardScale:0.###}");
            EditorGUILayout.LabelField($"Floor tile card: {CardWidth * floorCardScale:0.###} × {CardDepth * floorCardScale:0.###}");
            EditorGUILayout.LabelField($"Die: {DieSize:0.###}");
        }

        private float MatWidth()
        {
            SerializedObject so = new SerializedObject(layout);
            return so.FindProperty("matWidth").floatValue;
        }

        private float MatDepth()
        {
            SerializedObject so = new SerializedObject(layout);
            return so.FindProperty("matDepth").floatValue;
        }

        // ---- Canvas ----

        private void DrawCanvas(Rect canvas)
        {
            Event e = Event.current;
            if (e.type == EventType.Repaint && preview != null)
            {
                preview.camera.orthographicSize = zoom;
                preview.camera.transform.SetPositionAndRotation(new Vector3(pan.x, 20f, pan.y), Quaternion.Euler(90f, 0f, 0f));
                preview.BeginPreview(canvas, GUIStyle.none);
                preview.Render(true);
                Texture texture = preview.EndPreview();
                GUI.DrawTexture(canvas, texture, ScaleMode.StretchToFill, false);
            }

            if (layout == null) return;
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty slots = so.FindProperty("slots");
            if (e.type == EventType.Repaint) DrawOverlay(canvas, slots);
            HandleInput(canvas, so, slots);
            GUI.Label(new Rect(canvas.x + 8f, canvas.y + 6f, 520f, 20f),
                (showBack ? "BACK (controller)" : "FRONT (symbols)")
                + "  ·  drag a slot to move, its corner dot to resize, right-drag to pan, scroll to zoom",
                EditorStyles.whiteMiniLabel);
        }

        private void DrawOverlay(Rect canvas, SerializedProperty slots)
        {
            TestPiece dragged = drag == DragMode.MovePiece && selectedPiece >= 0 && selectedPiece < pieces.Count ? pieces[selectedPiece] : null;
            string targetKey = string.Empty;
            bool targetRefused = false;
            if (dragged != null) PredictSnap(dragged, out targetKey, out targetRefused, out _);

            Handles.BeginGUI();
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                ConsoleLayoutSlotKind kind = (ConsoleLayoutSlotKind)slot.FindPropertyRelative("kind").intValue;
                if (!IsShown(slot, kind)) continue;
                Rect rect = SlotRect(canvas, slot);
                string key = slot.FindPropertyRelative("key").stringValue;
                Color line = kind == ConsoleLayoutSlotKind.Die ? DieSpotColour : CardSlotColour;
                if (kind == ConsoleLayoutSlotKind.Cube) line = new Color(0.7f, 0.7f, 0.7f);
                if (i == selectedSlot) line = SelectedColour;
                if (key == targetKey) line = targetRefused ? RefuseColour : AcceptColour;
                if (showColliders || i == selectedSlot || key == targetKey)
                {
                    Handles.DrawSolidRectangleWithOutline(rect, new Color(line.r, line.g, line.b, 0.14f), line);
                }

                if (showCardOutlines && kind == ConsoleLayoutSlotKind.Card)
                {
                    bool landscape = slot.FindPropertyRelative("defaultOrientation").intValue == (int)ConsoleSlotOrientation.Landscape;
                    Vector2 centre = rect.center;
                    float ppu = PixelsPerUnit(canvas);
                    Vector2 half = new Vector2((landscape ? CardDepth : CardWidth) * 0.5f * ppu, (landscape ? CardWidth : CardDepth) * 0.5f * ppu);
                    Handles.color = new Color(1f, 1f, 1f, 0.75f);
                    Vector3 a = new Vector3(centre.x - half.x, centre.y - half.y);
                    Vector3 b = new Vector3(centre.x + half.x, centre.y - half.y);
                    Vector3 c = new Vector3(centre.x + half.x, centre.y + half.y);
                    Vector3 d = new Vector3(centre.x - half.x, centre.y + half.y);
                    Handles.DrawDottedLines(new[] { a, b, b, c, c, d, d, a }, 3f);
                }

                ConsoleSymbol symbol = (ConsoleSymbol)slot.FindPropertyRelative("symbol").intValue;
                if (kind != ConsoleLayoutSlotKind.Cube)
                {
                    GUI.Label(new Rect(rect.x + 2f, rect.y + 1f, 160f, 16f),
                        key + (symbol != ConsoleSymbol.None ? $" ({symbol})" : string.Empty), EditorStyles.whiteMiniLabel);
                }

                if (i == selectedSlot)
                {
                    Handles.color = SelectedColour;
                    Handles.DrawSolidDisc(new Vector3(rect.xMax, rect.yMax), Vector3.forward, HandleRadius);
                }
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                TestPiece piece = pieces[i];
                if (piece.Instance != null && !piece.Instance.activeSelf) continue;
                Vector2 gui = ToGui(canvas, piece.Position);
                string text = piece.Kind == PieceKind.Die
                    ? piece.Value.ToString()
                    : piece.Symbol != ConsoleSymbol.None ? piece.Symbol.ToString() : string.Empty;
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = i == selectedPiece ? SelectedColour : Color.black } };
                GUI.Label(new Rect(gui.x - 40f, gui.y - 9f, 80f, 18f), text, style);
            }

            Handles.EndGUI();
        }

        private bool IsShown(SerializedProperty slot, ConsoleLayoutSlotKind kind)
        {
            if (kind == ConsoleLayoutSlotKind.Cube && !showCubeSpots) return false;
            bool back = slot.FindPropertyRelative("side").intValue == (int)ConsoleMatSide.Back;
            return back == showBack;
        }

        private void HandleInput(Rect canvas, SerializedObject so, SerializedProperty slots)
        {
            Event e = Event.current;
            if (!canvas.Contains(e.mousePosition) && drag == DragMode.None) return;
            Vector2 world = ToWorld(canvas, e.mousePosition);
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    zoom = Mathf.Clamp(zoom * (1f + (e.delta.y * 0.04f)), 1f, 12f);
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseDown:
                    lastMouse = e.mousePosition;
                    if (e.button == 1 || e.button == 2)
                    {
                        drag = DragMode.Pan;
                        e.Use();
                        break;
                    }

                    if (e.button != 0) break;
                    BeginLeftDrag(canvas, slots, world, e.mousePosition);
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseDrag:
                    ContinueDrag(canvas, so, slots, world, e.mousePosition);
                    lastMouse = e.mousePosition;
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseUp:
                    EndDrag();
                    e.Use();
                    Repaint();
                    break;
            }
        }

        private void BeginLeftDrag(Rect canvas, SerializedProperty slots, Vector2 world, Vector2 mouse)
        {
            Undo.IncrementCurrentGroup();
            if (selectedSlot >= 0 && selectedSlot < slots.arraySize)
            {
                Rect selected = SlotRect(canvas, slots.GetArrayElementAtIndex(selectedSlot));
                if (Vector2.Distance(mouse, new Vector2(selected.xMax, selected.yMax)) <= HandleRadius + 3f)
                {
                    drag = DragMode.ResizeSlot;
                    return;
                }
            }

            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                TestPiece piece = pieces[i];
                if (piece.Instance != null && !piece.Instance.activeSelf) continue;
                Vector2 half = PieceHalfSize(piece);
                if (Mathf.Abs(world.x - piece.Position.x) <= half.x && Mathf.Abs(world.y - piece.Position.y) <= half.y)
                {
                    selectedPiece = i;
                    selectedSlot = -1;
                    drag = DragMode.MovePiece;
                    dragOffset = piece.Position - world;
                    piece.SnappedKey = string.Empty;
                    return;
                }
            }

            int best = -1;
            float bestArea = float.MaxValue;
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                ConsoleLayoutSlotKind kind = (ConsoleLayoutSlotKind)slot.FindPropertyRelative("kind").intValue;
                if (!IsShown(slot, kind) || kind == ConsoleLayoutSlotKind.Cube) continue;
                SlotGeometry(slot, out Vector2 centre, out Vector2 size);
                if (Mathf.Abs(world.x - centre.x) > size.x * 0.5f || Mathf.Abs(world.y - centre.y) > size.y * 0.5f) continue;
                float area = size.x * size.y;
                if (area < bestArea)
                {
                    best = i;
                    bestArea = area;
                }
            }

            selectedPiece = -1;
            selectedSlot = best;
            if (best >= 0)
            {
                SlotGeometry(slots.GetArrayElementAtIndex(best), out Vector2 centre, out _);
                dragOffset = centre - world;
                drag = DragMode.MoveSlot;
            }
        }

        private void ContinueDrag(Rect canvas, SerializedObject so, SerializedProperty slots, Vector2 world, Vector2 mouse)
        {
            switch (drag)
            {
                case DragMode.Pan:
                    pan -= new Vector2(mouse.x - lastMouse.x, -(mouse.y - lastMouse.y)) / PixelsPerUnit(canvas);
                    break;
                case DragMode.MovePiece:
                    if (selectedPiece >= 0 && selectedPiece < pieces.Count)
                    {
                        pieces[selectedPiece].Position = world + dragOffset;
                        PlacePiece(pieces[selectedPiece], pieces[selectedPiece].Instance);
                    }

                    break;
                case DragMode.MoveSlot:
                    if (selectedSlot >= 0 && selectedSlot < slots.arraySize)
                    {
                        SerializedProperty slot = slots.GetArrayElementAtIndex(selectedSlot);
                        Vector2 centre = world + dragOffset;
                        slot.FindPropertyRelative("x").floatValue = Round(centre.x);
                        slot.FindPropertyRelative("z").floatValue = Round(centre.y);
                        so.ApplyModifiedProperties();
                    }

                    break;
                case DragMode.ResizeSlot:
                    if (selectedSlot >= 0 && selectedSlot < slots.arraySize)
                    {
                        SerializedProperty slot = slots.GetArrayElementAtIndex(selectedSlot);
                        SlotGeometry(slot, out Vector2 centre, out _);
                        slot.FindPropertyRelative("footprintWidth").floatValue = Mathf.Max(0.1f, Round(Mathf.Abs(world.x - centre.x) * 2f));
                        slot.FindPropertyRelative("footprintDepth").floatValue = Mathf.Max(0.1f, Round(Mathf.Abs(world.y - centre.y) * 2f));
                        so.ApplyModifiedProperties();
                    }

                    break;
            }
        }

        private void EndDrag()
        {
            DragMode ended = drag;
            drag = DragMode.None;
            if (ended == DragMode.MovePiece && selectedPiece >= 0 && selectedPiece < pieces.Count)
            {
                SnapPiece(pieces[selectedPiece]);
                PlacePiece(pieces[selectedPiece], pieces[selectedPiece].Instance);
            }
            else if ((ended == DragMode.MoveSlot || ended == DragMode.ResizeSlot) && applyOnRelease)
            {
                ApplyToPrefab(false);
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        // ---- Snapping (the same rules as the table, doc 22 §14) ----

        // Card: the slot under it takes it unless its symbol differs; a card with a symbol is pulled into the nearest
        // slot with that symbol within the magnet distance. Die: the nearest die spot within the radius.
        private bool PredictSnap(TestPiece piece, out string key, out bool refused, out SerializedProperty target)
        {
            key = string.Empty;
            refused = false;
            target = null;
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty slots = so.FindProperty("slots");
            SerializedProperty under = null;
            SerializedProperty nearest = null;
            float nearestDistance = piece.Kind == PieceKind.Die ? dieSpotRadius : magnetDistance;
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                ConsoleLayoutSlotKind kind = (ConsoleLayoutSlotKind)slot.FindPropertyRelative("kind").intValue;
                bool back = slot.FindPropertyRelative("side").intValue == (int)ConsoleMatSide.Back;
                if (back != showBack) continue;
                SlotGeometry(slot, out Vector2 centre, out Vector2 size);
                float distance = Vector2.Distance(centre, piece.Position);
                if (piece.Kind == PieceKind.Die)
                {
                    if (kind != ConsoleLayoutSlotKind.Die || distance > nearestDistance) continue;
                    nearest = slot;
                    nearestDistance = distance;
                    continue;
                }

                if (kind != ConsoleLayoutSlotKind.Card) continue;
                bool inside = Mathf.Abs(piece.Position.x - centre.x) <= size.x * 0.5f && Mathf.Abs(piece.Position.y - centre.y) <= size.y * 0.5f;
                if (inside && (under == null || size.x * size.y < AreaOf(under))) under = slot;
                ConsoleSymbol symbol = (ConsoleSymbol)slot.FindPropertyRelative("symbol").intValue;
                if (piece.Symbol != ConsoleSymbol.None && symbol == piece.Symbol && distance < nearestDistance)
                {
                    nearest = slot;
                    nearestDistance = distance;
                }
            }

            if (piece.Kind == PieceKind.Die)
            {
                target = nearest;
            }
            else if (under != null)
            {
                ConsoleSymbol underSymbol = (ConsoleSymbol)under.FindPropertyRelative("symbol").intValue;
                bool fits = piece.Symbol == ConsoleSymbol.None || underSymbol == ConsoleSymbol.None || underSymbol == piece.Symbol;
                target = fits ? under : nearest;
                if (!fits && nearest == null)
                {
                    key = under.FindPropertyRelative("key").stringValue;
                    refused = true;
                    return false;
                }
            }
            else
            {
                target = nearest;
            }

            if (target == null) return false;
            key = target.FindPropertyRelative("key").stringValue;
            return true;
        }

        private void SnapPiece(TestPiece piece)
        {
            if (!PredictSnap(piece, out string key, out bool refused, out SerializedProperty target))
            {
                piece.SnappedKey = string.Empty;
                snapStatus = refused
                    ? $"Refused by {key}: its symbol is not the card's ({piece.Symbol}). The card stays on the table."
                    : piece.Kind == PieceKind.Die
                        ? "No D-pad spot within reach: the die stays on the table."
                        : "No slot here: the card stays on the table.";
                return;
            }

            SlotGeometry(target, out Vector2 centre, out _);
            piece.Position = centre;
            piece.SnappedKey = key;
            piece.Side = (ConsoleMatSide)target.FindPropertyRelative("side").intValue;
            if (piece.Kind != PieceKind.Die)
            {
                piece.Landscape = target.FindPropertyRelative("defaultOrientation").intValue == (int)ConsoleSlotOrientation.Landscape;
            }

            ConsoleSymbol symbol = (ConsoleSymbol)target.FindPropertyRelative("symbol").intValue;
            snapStatus = piece.Kind == PieceKind.Die
                ? $"The die is set on {key}; in the game its number picker opens (now {piece.Value})."
                : $"Snapped into {key}" + (symbol != ConsoleSymbol.None ? $" ({symbol})." : ".");
        }

        private static float AreaOf(SerializedProperty slot)
        {
            return slot.FindPropertyRelative("footprintWidth").floatValue * slot.FindPropertyRelative("footprintDepth").floatValue;
        }

        // ---- Checks ----

        private void RunChecks()
        {
            checkResults.Clear();
            ConsoleLayoutData data;
            try
            {
                data = layout.ToData();
            }
            catch (Exception exception)
            {
                checkResults.Add($"✗ The layout is not valid: {exception.Message}");
                return;
            }

            // Overlapping colliders on the same side would make a drop land in the wrong slot.
            int overlaps = 0;
            for (int i = 0; i < data.CardSlots.Count; i++)
            {
                for (int j = i + 1; j < data.CardSlots.Count; j++)
                {
                    ConsoleLayoutSlotData a = data.CardSlots[i];
                    ConsoleLayoutSlotData b = data.CardSlots[j];
                    if (a.Side != b.Side) continue;
                    bool apart = Mathf.Abs(a.X - b.X) >= (a.FootprintWidth + b.FootprintWidth) * 0.5f
                        || Mathf.Abs(a.Z - b.Z) >= (a.FootprintDepth + b.FootprintDepth) * 0.5f;
                    if (apart) continue;
                    overlaps++;
                    checkResults.Add($"✗ Colliders overlap: {a.Key} and {b.Key} ({a.Side}).");
                }
            }

            if (overlaps == 0) checkResults.Add("✓ No slot colliders overlap on either side.");

            // The same seat spacing check Trap Floor runs at start (four players around the board).
            ConsoleAdjacentPlacement placement = new ConsoleAdjacentPlacement(data, ConsoleAdjacentPlacementSettings.Standard);
            PlayerLayoutDefinition seats = PlayerLayoutPresets.StandardFourPlayer;
            List<ConsoleAdjacentPlacementItem> items = new List<ConsoleAdjacentPlacementItem>();
            for (int seatIndex = 0; seatIndex < 4; seatIndex++)
            {
                if (!seats.TryGetSeat(seatIndex, out PlayerSeatLayoutEntry seat)) continue;
                TabletopPose pose = TrapFloorTemplateFactory.GetConsolePose(seat);
                for (int i = 0; i < placement.ConsoleShape.Count; i++)
                {
                    items.Add(new ConsoleAdjacentPlacementItem("console", seatIndex, ConsoleAdjacentPlacement.ToTableBounds(pose, placement.ConsoleShape[i]), false));
                }
            }

            ConsoleAdjacentPlacementReport report = ConsoleAdjacentPlacement.Check(items, null, placement.Settings);
            checkResults.Add(report.Passes
                ? $"✓ Four-seat spacing: closest Consoles are {report.MinimumClearance:0.###} apart (needs {placement.Settings.MinimumClearance})."
                : $"✗ Four-seat spacing: {report.ClosestFirst} and {report.ClosestSecond} are {report.MinimumClearance:0.###} apart; "
                    + $"Trap Floor needs {placement.Settings.MinimumClearance}. Make the corner or edge slots smaller or move them in.");

            int back = 0;
            int dice = 0;
            for (int i = 0; i < data.Slots.Count; i++)
            {
                if (data.Slots[i].Side == ConsoleMatSide.Back && data.Slots[i].Kind == ConsoleLayoutSlotKind.Card) back++;
                if (data.Slots[i].Kind == ConsoleLayoutSlotKind.Die) dice++;
            }

            checkResults.Add($"✓ {data.CardSlots.Count} card slots ({data.CardSlots.Count - back} front, {back} back) and {dice} die spots.");
            if (!PrefabMatchesLayout(data, out string mismatch)) checkResults.Add($"✗ The Console prefab does not match the layout: {mismatch} Press Apply.");
            else checkResults.Add("✓ The Console prefab matches the layout.");
        }

        private bool PrefabMatchesLayout(ConsoleLayoutData data, out string mismatch)
        {
            mismatch = string.Empty;
            if (consolePrefab == null) return true;
            ConsoleSlotView[] slotViews = consolePrefab.GetComponentsInChildren<ConsoleSlotView>(true);
            Array.Sort(slotViews, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            if (slotViews.Length != data.CardSlots.Count)
            {
                mismatch = $"it has {slotViews.Length} slots, the layout {data.CardSlots.Count}.";
                return false;
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                ConsoleLayoutSlotData slot = data.CardSlots[i];
                Vector3 local = slotViews[i].transform.localPosition;
                BoxCollider collider = slotViews[i].GetComponent<BoxCollider>();
                if (Mathf.Abs(local.x - slot.X) > 0.001f || Mathf.Abs(local.z - slot.Z) > 0.001f
                    || (collider != null && (Mathf.Abs(collider.size.x - slot.FootprintWidth) > 0.001f || Mathf.Abs(collider.size.z - slot.FootprintDepth) > 0.001f)))
                {
                    mismatch = $"slot {slot.Key} is in a different place or size.";
                    return false;
                }
            }

            return true;
        }

        // ---- Layout edits ----

        private void AddSlot(ConsoleLayoutSlotKind kind)
        {
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty slots = so.FindProperty("slots");
            int maxOrdinal = 0;
            HashSet<string> keys = new HashSet<string>();
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                maxOrdinal = Mathf.Max(maxOrdinal, slot.FindPropertyRelative("ordinal").intValue);
                keys.Add(slot.FindPropertyRelative("key").stringValue);
            }

            string prefix = kind == ConsoleLayoutSlotKind.Die ? "Die" : "Slot";
            int number = 1;
            while (keys.Contains(prefix + number)) number++;
            slots.arraySize++;
            SerializedProperty added = slots.GetArrayElementAtIndex(slots.arraySize - 1);
            added.FindPropertyRelative("ordinal").intValue = maxOrdinal + 1;
            added.FindPropertyRelative("key").stringValue = prefix + number;
            added.FindPropertyRelative("kind").intValue = (int)kind;
            added.FindPropertyRelative("group").stringValue = kind == ConsoleLayoutSlotKind.Die ? "DPad" : "Added";
            added.FindPropertyRelative("x").floatValue = Round(pan.x);
            added.FindPropertyRelative("z").floatValue = Round(pan.y);
            added.FindPropertyRelative("footprintWidth").floatValue = kind == ConsoleLayoutSlotKind.Die ? DieSize : CardWidth + 0.4f;
            added.FindPropertyRelative("footprintDepth").floatValue = kind == ConsoleLayoutSlotKind.Die ? DieSize : CardDepth;
            added.FindPropertyRelative("defaultCapacity").intValue = 0;
            added.FindPropertyRelative("marker").boolValue = false;
            added.FindPropertyRelative("defaultOrientation").intValue = kind == ConsoleLayoutSlotKind.Card
                ? (int)ConsoleSlotOrientation.Portrait
                : (int)ConsoleSlotOrientation.None;
            added.FindPropertyRelative("side").intValue = showBack ? (int)ConsoleMatSide.Back : (int)ConsoleMatSide.Front;
            added.FindPropertyRelative("symbol").intValue = (int)ConsoleSymbol.None;
            so.ApplyModifiedProperties();
            selectedSlot = slots.arraySize - 1;
            if (kind == ConsoleLayoutSlotKind.Card)
            {
                // A new card slot needs its slot object on the prefab: the menu tool adds missing slots.
                ConsoleMatUpgradeTool.MakeDoubleSided();
                BuildPreview();
            }
        }

        private void ApplyToPrefab(bool report)
        {
            if (consolePrefab == null) return;
            ConsoleLayoutData data;
            try
            {
                data = layout.ToData();
            }
            catch (Exception exception)
            {
                if (report) Debug.LogWarning($"[Console] The layout is not valid yet: {exception.Message}");
                return;
            }

            string path = AssetDatabase.GetAssetPath(consolePrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int changed;
            try
            {
                changed = ConsoleMatUpgradeTool.SyncSlotsToLayout(root, data, false);
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            if (report) Debug.Log($"[Console] Layout applied to {consolePrefab.name}: {changed} slots moved or resized.");
        }

        // ---- Test pieces ----

        private void AddPiece(PieceKind kind)
        {
            TestPiece piece = new TestPiece { Kind = kind, Position = pan + new Vector2(0f, -(MatDepth() * 0.5f) - 1.2f) };
            piece.Instance = CreatePieceInstance(piece);
            pieces.Add(piece);
            selectedPiece = pieces.Count - 1;
            selectedSlot = -1;
            snapStatus = "Drag the piece onto the mat to see where it snaps.";
        }

        private void RemovePiece(int index)
        {
            if (pieces[index].Instance != null) Object.DestroyImmediate(pieces[index].Instance);
            pieces.RemoveAt(index);
            selectedPiece = -1;
        }

        private Vector2 PieceHalfSize(TestPiece piece)
        {
            if (piece.Kind == PieceKind.Die) return Vector2.one * (DieSize * 0.5f);
            float scale = piece.Kind == PieceKind.AvatarCard ? avatarCardScale : 1f;
            Vector2 half = new Vector2(CardWidth * 0.5f * scale, CardDepth * 0.5f * scale);
            return piece.Landscape ? new Vector2(half.y, half.x) : half;
        }

        // ---- Geometry ----

        private static void SlotGeometry(SerializedProperty slot, out Vector2 centre, out Vector2 size)
        {
            centre = new Vector2(slot.FindPropertyRelative("x").floatValue, slot.FindPropertyRelative("z").floatValue);
            size = new Vector2(slot.FindPropertyRelative("footprintWidth").floatValue, slot.FindPropertyRelative("footprintDepth").floatValue);
        }

        private Rect SlotRect(Rect canvas, SerializedProperty slot)
        {
            SlotGeometry(slot, out Vector2 centre, out Vector2 size);
            float ppu = PixelsPerUnit(canvas);
            Vector2 gui = ToGui(canvas, centre);
            return new Rect(gui.x - (size.x * 0.5f * ppu), gui.y - (size.y * 0.5f * ppu), size.x * ppu, size.y * ppu);
        }

        private float PixelsPerUnit(Rect canvas)
        {
            return canvas.height / (2f * zoom);
        }

        private Vector2 ToGui(Rect canvas, Vector2 world)
        {
            float ppu = PixelsPerUnit(canvas);
            return new Vector2(canvas.center.x + ((world.x - pan.x) * ppu), canvas.center.y - ((world.y - pan.y) * ppu));
        }

        private Vector2 ToWorld(Rect canvas, Vector2 gui)
        {
            float ppu = PixelsPerUnit(canvas);
            return new Vector2(pan.x + ((gui.x - canvas.center.x) / ppu), pan.y - ((gui.y - canvas.center.y) / ppu));
        }

        private static float Round(float value)
        {
            return Mathf.Round(value * 1000f) / 1000f;
        }
    }
}
