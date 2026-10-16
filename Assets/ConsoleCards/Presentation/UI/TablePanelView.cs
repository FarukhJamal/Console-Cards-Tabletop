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
    /// </summary>
    public sealed class TablePanelView : ReusableUiView
    {
        [SerializeField] private Button closeButton;
        [Tooltip("Classic, Rectangle, Square, Round, Oval, Star — in that order.")]
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

        private TablePanelActions actions;

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

            for (int i = 0; i < shapeButtons.Length; i++)
            {
                int shape = i;
                // The chosen shape reads as "you are here": its button does not press.
                shapeButtons[i].interactable = i != model.SelectedShape;
                shapeButtons[i].onClick.AddListener(() => actions?.PickShape?.Invoke(shape));
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

            statusLabel.text = model.Status;
            undoButton.interactable = model.CanUndo;
            redoButton.interactable = model.CanRedo;
            closeButton.onClick.AddListener(HandleCancel);
            undoButton.onClick.AddListener(() => actions?.Undo?.Invoke());
            redoButton.onClick.AddListener(() => actions?.Redo?.Invoke());
            resetButton.onClick.AddListener(() => actions?.Reset?.Invoke());
            cancelButton.onClick.AddListener(HandleCancel);
            doneButton.onClick.AddListener(() => actions?.Done?.Invoke());
        }

        public override void Unbind()
        {
            RemoveListeners();
            actions = null;
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
    }

    /// <summary>The sizes Table mode can change (doc 30, B3; T3), in the order of the panel's rows.</summary>
    public enum TableField
    {
        Width = 0,
        Depth = 1,
        Corners = 2,
        Points = 3,
        InnerSize = 4,
        EdgeHeight = 5,
        EdgeWidth = 6,
    }

    /// <summary>What the Table mode panel shows.</summary>
    public sealed class TablePanelModel
    {
        public const int ShapeCount = 6;
        public const int FieldCount = 7;

        /// <summary>The chosen shape button (Classic, Rectangle, Square, Round, Oval, Star), or −1 for a model.</summary>
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
    }
}
