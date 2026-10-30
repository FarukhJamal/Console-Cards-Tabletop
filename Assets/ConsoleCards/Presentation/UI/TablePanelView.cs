using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Table mode (doc 30, B3; T3): shape buttons, sizes with − / + and number boxes, the edge, Undo and Redo, and
    /// Cancel or Done. A side panel, so the table stays in view while it changes. An authored Final UI prefab
    /// (Create Missing Gameplay UI Prefabs); nothing is built at runtime. Esc cancels, like the Cancel button.
    /// Size handles (optional, added to older prefabs by the same menu) sit over the table's edge and follow the
    /// camera; dragging one resizes the table.
    /// </summary>
    public sealed class TablePanelView : ReusableUiView
    {
        [SerializeField] private Button closeButton;
        [Tooltip("Classic, Rectangle, Square, Round, Oval, Hexagon — in that order.")]
        [SerializeField] private Button[] shapeButtons = Array.Empty<Button>();
        [SerializeField] private GameObject modelRow;
        [SerializeField] private Button previousModelButton;
        [SerializeField] private Text modelLabel;
        [SerializeField] private Button nextModelButton;
        [Tooltip("One row per TableField, in its order: Width, Depth, Corners, Points, Inner size, Edge height, Edge width.")]
        [SerializeField] private GameObject[] fieldRows = Array.Empty<GameObject>();
        [SerializeField] private Button[] fieldMinusButtons = Array.Empty<Button>();
        [SerializeField] private Button[] fieldPlusButtons = Array.Empty<Button>();
        [SerializeField] private InputField[] fieldInputs = Array.Empty<InputField>();
        [SerializeField] private GameObject edgeRow;
        [SerializeField] private Button previousEdgeButton;
        [SerializeField] private Text edgeLabel;
        [SerializeField] private Button nextEdgeButton;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button redoButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button doneButton;

        [Header("Size handles (T3; optional, added by Create Missing Gameplay UI Prefabs)")]
        [SerializeField] private RectTransform handlesRoot;
        [Tooltip("Six handles: +X side, −X side, +Z side, −Z side, corner or radius, star inner corner.")]
        [SerializeField] private TableHandleView[] handles = Array.Empty<TableHandleView>();

        [Header("Board 12 (optional)")]
        [Tooltip("The shape tiles' fills (same order as the shape buttons): the chosen one teal, the others grey.")]
        [SerializeField] private Image[] shapeFills = Array.Empty<Image>();
        [SerializeField] private Image[] shapeIcons = Array.Empty<Image>();
        [SerializeField] private Color shapeOn = new Color(0.110f, 0.235f, 0.239f, 1f);
        [SerializeField] private Color shapeOff = new Color(0.161f, 0.180f, 0.184f, 1f);
        [SerializeField] private Color iconOn = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color iconOff = new Color(0.420f, 0.455f, 0.467f, 1f);
        [Tooltip("Board 12: while the panel is open the table camera draws only inside this part of the screen (x, y from "
            + "the bottom left, width, height; 0 to 1). Zero size: the whole screen, as before.")]
        [SerializeField] private Rect previewViewport = new Rect(0f, 0f, 0f, 0f);

        // Board 12 (2026-10-30): TOP swatches, Fit the players, SAVE AS NEW TABLE. Optional.
        [SerializeField] private GameObject lookRoot;
        [SerializeField] private Button[] topButtons = Array.Empty<Button>();
        [SerializeField] private GameObject[] topRings = Array.Empty<GameObject>();
        [SerializeField] private Button fitButton;
        [SerializeField] private Image fitTrack;
        [SerializeField] private RectTransform fitKnob;
        [SerializeField] private Button saveAsButton;
        [SerializeField] private Color fitOn = new Color(0.075f, 0.541f, 0.549f, 1f);
        [SerializeField] private Color fitOff = new Color(0.259f, 0.271f, 0.278f, 1f);

        private UnityEngine.Camera framedCamera;
        private Rect framedCameraRect;

        private TablePanelActions actions;
        private TablePanelModel shown;
        private Canvas canvas;
        private Action<int> beginHandle;
        private Action<int, Vector2> dragHandle;
        private Action<int> endHandle;

        /// <summary>True when the prefab has the size handles.</summary>
        public bool HasHandles => handlesRoot != null && handles != null && handles.Length == TablePanelModel.HandleCount;

        public void ValidateReferences()
        {
            int fields = TablePanelModel.FieldCount;
            if (closeButton == null || shapeButtons == null || shapeButtons.Length != TablePanelModel.ShapeCount
                || modelRow == null || previousModelButton == null || modelLabel == null || nextModelButton == null
                || fieldRows == null || fieldRows.Length != fields || fieldMinusButtons == null
                || fieldMinusButtons.Length != fields || fieldPlusButtons == null || fieldPlusButtons.Length != fields
                || fieldInputs == null || fieldInputs.Length != fields || edgeRow == null || previousEdgeButton == null
                || edgeLabel == null || nextEdgeButton == null || statusLabel == null || undoButton == null
                || redoButton == null || resetButton == null || cancelButton == null || doneButton == null)
            {
                throw new InvalidOperationException("TablePanelView requires its authored references.");
            }
        }

        /// <summary>Shows the panel; call again with a new model to refresh it.</summary>
        public void Bind(TablePanelModel model, TablePanelActions panelActions)
        {
            RequireAcquired();
            ValidateReferences();
            if (model == null) throw new ArgumentNullException(nameof(model));
            actions = panelActions ?? throw new ArgumentNullException(nameof(panelActions));
            RemoveListeners();

            // LAYOUT-1: the Arrange table panel shows no shape row and may hide Undo/Redo and rename its buttons.
            if (shapeButtons.Length > 0 && shapeButtons[0] != null && shapeButtons[0].transform.parent != null)
            {
                shapeButtons[0].transform.parent.gameObject.SetActive(model.ShowShapes);
            }

            undoButton.gameObject.SetActive(model.ShowHistory);
            redoButton.gameObject.SetActive(model.ShowHistory);
            SetLabel(resetButton, model.ResetText, ref resetLabelText);
            SetLabel(cancelButton, model.CancelText, ref cancelLabelText);
            SetLabel(doneButton, model.DoneText, ref doneLabelText);

            for (int i = 0; i < shapeButtons.Length; i++)
            {
                int shape = i;
                // The chosen shape reads as "you are here": its button does not press.
                shapeButtons[i].interactable = i != model.SelectedShape;
                shapeButtons[i].onClick.AddListener(() => actions?.PickShape?.Invoke(shape));
                if (i < shapeFills.Length && shapeFills[i] != null) shapeFills[i].color = i == model.SelectedShape ? shapeOn : shapeOff;
                if (i < shapeIcons.Length && shapeIcons[i] != null) shapeIcons[i].color = i == model.SelectedShape ? iconOn : iconOff;
            }

            modelRow.SetActive(model.ShowModels);
            modelLabel.text = model.ModelText;
            previousModelButton.onClick.AddListener(() => actions?.CycleModel?.Invoke(-1));
            nextModelButton.onClick.AddListener(() => actions?.CycleModel?.Invoke(1));

            for (int i = 0; i < fieldRows.Length; i++)
            {
                TableField field = (TableField)i;
                fieldRows[i].SetActive(model.ShowField[i]);
                fieldInputs[i].text = model.FieldText[i];
                fieldMinusButtons[i].onClick.AddListener(() => actions?.Step?.Invoke(field, -1, BigStep()));
                fieldPlusButtons[i].onClick.AddListener(() => actions?.Step?.Invoke(field, 1, BigStep()));
                fieldInputs[i].onEndEdit.AddListener(text => actions?.Type?.Invoke(field, text));
            }

            edgeRow.SetActive(model.ShowEdge);
            edgeLabel.text = model.EdgeText;
            previousEdgeButton.onClick.AddListener(() => actions?.CycleEdge?.Invoke(-1));
            nextEdgeButton.onClick.AddListener(() => actions?.CycleEdge?.Invoke(1));

            BindLook(model);
            statusLabel.text = model.Status;
            undoButton.interactable = model.CanUndo;
            redoButton.interactable = model.CanRedo;
            closeButton.onClick.AddListener(HandleCancel);
            undoButton.onClick.AddListener(() => actions?.Undo?.Invoke());
            redoButton.onClick.AddListener(() => actions?.Redo?.Invoke());
            resetButton.onClick.AddListener(() => actions?.Reset?.Invoke());
            cancelButton.onClick.AddListener(HandleCancel);
            doneButton.onClick.AddListener(() => actions?.Done?.Invoke());

            shown = model;
            FrameCamera(model.Camera);
            if (!HasHandles) return;
            if (beginHandle == null)
            {
                beginHandle = i => actions?.BeginHandle?.Invoke(i);
                dragHandle = (i, screen) => actions?.DragHandle?.Invoke(i, screen);
                endHandle = i => actions?.EndHandle?.Invoke(i);
            }

            for (int i = 0; i < handles.Length; i++)
            {
                if (handles[i] == null) continue;
                handles[i].Bind(i, beginHandle, dragHandle, endHandle);
                handles[i].gameObject.SetActive(model.HandleShown[i]);
            }

            PlaceHandles();
        }

        private void LateUpdate() => PlaceHandles();

        // Keeps each handle over its point of the table as the camera moves. No allocation.
        private void PlaceHandles()
        {
            if (shown == null || !HasHandles || shown.Camera == null) return;
            if (canvas == null) canvas = handlesRoot.GetComponentInParent<Canvas>();
            UnityEngine.Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            for (int i = 0; i < handles.Length; i++)
            {
                TableHandleView handle = handles[i];
                if (handle == null || !shown.HandleShown[i]) continue;
                Vector3 screen = shown.Camera.WorldToScreenPoint(shown.HandleWorld[i]);
                bool visible = screen.z > 0f;
                if (handle.gameObject.activeSelf != visible) handle.gameObject.SetActive(visible);
                if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(handlesRoot, screen, uiCamera, out Vector2 local))
                {
                    handle.Rect.localPosition = local;
                }
            }
        }

        // Board 12: the table is seen "from above" inside the panel's frame.
        private void FrameCamera(UnityEngine.Camera camera)
        {
            if (previewViewport.width <= 0f || previewViewport.height <= 0f || camera == null) return;
            if (framedCamera != camera)
            {
                UnframeCamera();
                framedCamera = camera;
                framedCameraRect = camera.rect;
            }

            camera.rect = previewViewport;
        }

        private void UnframeCamera()
        {
            if (framedCamera != null) framedCamera.rect = framedCameraRect;
            framedCamera = null;
        }

        private void OnDisable() => UnframeCamera();

        private void BindLook(TablePanelModel model)
        {
            if (lookRoot != null && lookRoot.activeSelf != model.ShowLook) lookRoot.SetActive(model.ShowLook);
            for (int i = 0; i < topButtons.Length; i++)
            {
                if (topButtons[i] == null) continue;
                int top = i;
                topButtons[i].onClick.RemoveAllListeners();
                topButtons[i].onClick.AddListener(() => actions?.PickTop?.Invoke(top));
                if (i < topRings.Length && topRings[i] != null && topRings[i].activeSelf != (i == model.Top)) topRings[i].SetActive(i == model.Top);
            }

            if (fitButton != null)
            {
                fitButton.onClick.RemoveAllListeners();
                fitButton.onClick.AddListener(() => actions?.ToggleFitPlayers?.Invoke());
            }

            if (fitTrack != null) fitTrack.color = model.FitPlayers ? fitOn : fitOff;
            if (fitKnob != null)
            {
                fitKnob.anchorMin = fitKnob.anchorMax = new Vector2(model.FitPlayers ? 1f : 0f, 0.5f);
                fitKnob.pivot = new Vector2(model.FitPlayers ? 1f : 0f, 0.5f);
                fitKnob.anchoredPosition = new Vector2(model.FitPlayers ? -4.5f : 4.5f, 0f);
            }

            if (saveAsButton != null)
            {
                saveAsButton.onClick.RemoveAllListeners();
                saveAsButton.onClick.AddListener(() => actions?.SaveAsNew?.Invoke());
            }
        }

        public override void Unbind()
        {
            UnframeCamera();
            RemoveListeners();
            for (int i = 0; handles != null && i < handles.Length; i++)
            {
                if (handles[i] != null) handles[i].Unbind();
            }

            actions = null;
            shown = null;
        }

        // Esc leaves Table mode without keeping the changes (doc 30, B3). Reads a key state: no allocation.
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (actions != null && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) HandleCancel();
        }

        // Shift + − / + takes bigger steps.
        private static bool BigStep()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private void HandleCancel() => actions?.Cancel?.Invoke();

        private void RemoveListeners()
        {
            Clear(closeButton);
            for (int i = 0; shapeButtons != null && i < shapeButtons.Length; i++) Clear(shapeButtons[i]);
            Clear(previousModelButton);
            Clear(nextModelButton);
            for (int i = 0; fieldMinusButtons != null && i < fieldMinusButtons.Length; i++) Clear(fieldMinusButtons[i]);
            for (int i = 0; fieldPlusButtons != null && i < fieldPlusButtons.Length; i++) Clear(fieldPlusButtons[i]);
            for (int i = 0; fieldInputs != null && i < fieldInputs.Length; i++)
            {
                if (fieldInputs[i] != null) fieldInputs[i].onEndEdit.RemoveAllListeners();
            }

            Clear(previousEdgeButton);
            Clear(nextEdgeButton);
            Clear(undoButton);
            Clear(redoButton);
            Clear(resetButton);
            Clear(cancelButton);
            Clear(doneButton);
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private string resetLabelText;
        private string cancelLabelText;
        private string doneLabelText;

        // A button's words: the model's, or the prefab's own when the model gives none (remembered the first time).
        private static void SetLabel(Button button, string text, ref string original)
        {
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label == null) return;
            if (original == null) original = label.text;
            label.text = string.IsNullOrEmpty(text) ? original : text;
        }
    }

    /// <summary>The sizes Table mode can change (doc 30, B3; T3), in the order of the panel's rows.</summary>
    public enum TableField
    {
        Width = 0,
        Depth = 1,
        Corners = 2,
        Points = 3, // the star's; unused since T-3 (kept so the panel's rows keep their order)
        InnerSize = 4,
        EdgeHeight = 5,
        EdgeWidth = 6,
    }

    /// <summary>What the Table mode panel shows.</summary>
    public sealed class TablePanelModel
    {
        public const int ShapeCount = 6;
        public const int FieldCount = 7;
        public const int HandleCount = 6;

        /// <summary>The chosen shape button (Classic, Rectangle, Square, Round, Oval, Hexagon), or −1 for a model.</summary>
        public int SelectedShape;
        public bool ShowModels;
        public string ModelText = string.Empty;
        public readonly bool[] ShowField = new bool[FieldCount];
        public readonly string[] FieldText = new string[FieldCount];
        public bool ShowEdge;
        public string EdgeText = string.Empty;
        public bool CanUndo;
        public bool CanRedo;
        public string Status = string.Empty;

        /// <summary>LAYOUT-1: false hides the shape buttons (the Arrange table panel).</summary>
        public bool ShowShapes = true;

        /// <summary>LAYOUT-1: false hides Undo and Redo.</summary>
        public bool ShowHistory = true;

        /// <summary>Board 12: TOP (0 the table's own), Fit the players and SAVE AS NEW TABLE; false hides them.</summary>
        public bool ShowLook;
        public int Top;
        public bool FitPlayers = true;

        /// <summary>LAYOUT-1: the Reset, Cancel and Done buttons' words; empty keeps the prefab's.</summary>
        public string ResetText = string.Empty;
        public string CancelText = string.Empty;
        public string DoneText = string.Empty;

        /// <summary>The camera the table is seen through, for placing the handles.</summary>
        public UnityEngine.Camera Camera;
        public readonly bool[] HandleShown = new bool[HandleCount];
        public readonly Vector3[] HandleWorld = new Vector3[HandleCount];
    }

    /// <summary>The Table mode panel's actions, supplied by the game.</summary>
    public sealed class TablePanelActions
    {
        public Action<int> PickShape;
        public Action<int> CycleModel;

        /// <summary>A field, the direction (−1 or 1) and whether it is a big step (Shift held).</summary>
        public Action<TableField, int, bool> Step;
        public Action<TableField, string> Type;
        public Action<int> CycleEdge;
        public Action Undo;
        public Action Redo;
        public Action Reset;
        public Action Cancel;
        public Action Done;

        /// <summary>A size handle (its index) starts being dragged, moves to a screen point, and is let go.</summary>
        public Action<int> PickTop;
        public Action ToggleFitPlayers;
        public Action SaveAsNew;

        public Action<int> BeginHandle;
        public Action<int, Vector2> DragHandle;
        public Action<int> EndHandle;
    }
}
