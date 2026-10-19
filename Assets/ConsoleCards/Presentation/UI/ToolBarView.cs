using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>The table tools on the tool bar (TOOLS-1).</summary>
    public enum TableToolKind
    {
        Grab = 0,
        Select = 1,
        Ping = 2,
        Flick = 3,
        Draw = 4,
    }

    /// <summary>
    /// The tool bar (TOOLS-1), bottom right, in the Toolbox look: the table tools (Grab, Select, Ping, Flick, and
    /// Draw, which waits for T5), the keys strip that names the keys for what you are doing now (hideable, and off
    /// with the Hints switch), Help, the selection buttons (count, Group into deck, Clear) while cards are selected,
    /// and the selection box drawn while dragging with the Select tool. An authored Final UI prefab (Console Cards >
    /// UI > Create Missing Gameplay UI Prefabs); code only fills texts, colours and the box position.
    /// </summary>
    public sealed class ToolBarView : ReusableUiView
    {
        [Serializable]
        public sealed class ToolButton
        {
            public TableToolKind kind;
            public Button button;
            public Image fill;
            public Image icon;
            public Text label;
        }

        [SerializeField] private ToolButton[] tools = new ToolButton[0];
        [SerializeField] private Color idleFill = new Color(0.992f, 0.973f, 0.902f, 1f);
        [SerializeField] private Color activeFill = new Color(0.949f, 0.659f, 0.114f, 1f);
        [SerializeField] private Color idleIcon = new Color(0.114f, 0.114f, 0.122f, 1f);
        [SerializeField] private Color activeIcon = new Color(0.114f, 0.114f, 0.122f, 1f);
        [Header("Keys strip")]
        [SerializeField] private GameObject keysStrip;
        [SerializeField] private Text keysLabel;
        [SerializeField] private Button keysToggle;
        [SerializeField] private Button helpButton;
        [Header("Selection")]
        [SerializeField] private GameObject selectionActions;
        [SerializeField] private Text selectionCountLabel;
        [SerializeField] private Button groupButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private RectTransform selectionBox;

        private Action<TableToolKind> chooseTool;
        private Action toggleKeys;
        private Action openHelp;
        private Action groupSelection;
        private Action clearSelection;
        private string shownKeys;
        private bool keysWanted = true;
        private bool keysAllowed = true;
        private int shownCount = -1;
        private Canvas canvas;

        public void ValidateReferences()
        {
            if (tools == null || tools.Length == 0 || keysStrip == null || keysLabel == null || keysToggle == null
                || helpButton == null || selectionActions == null || selectionCountLabel == null || groupButton == null
                || clearButton == null || selectionBox == null)
            {
                throw new InvalidOperationException(
                    "ToolBarView is missing an authored reference. Run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI).");
            }
        }

        public void Bind(
            Action<TableToolKind> choose,
            Action toggleKeysStrip,
            Action help,
            Action group,
            Action clear)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            chooseTool = choose;
            toggleKeys = toggleKeysStrip;
            openHelp = help;
            groupSelection = group;
            clearSelection = clear;
            for (int i = 0; i < tools.Length; i++)
            {
                ToolButton tool = tools[i];
                if (tool?.button == null) continue;
                TableToolKind kind = tool.kind;
                tool.button.onClick.AddListener(() => chooseTool?.Invoke(kind));
            }

            keysToggle.onClick.AddListener(() => toggleKeys?.Invoke());
            helpButton.onClick.AddListener(() => openHelp?.Invoke());
            groupButton.onClick.AddListener(() => groupSelection?.Invoke());
            clearButton.onClick.AddListener(() => clearSelection?.Invoke());
            shownKeys = null;
            shownCount = -1;
            SetSelectionCount(0);
            HideSelectionBox();
        }

        /// <summary>Lights the chosen tool.</summary>
        public void SetActiveTool(TableToolKind kind)
        {
            for (int i = 0; i < tools.Length; i++)
            {
                ToolButton tool = tools[i];
                if (tool == null) continue;
                bool active = tool.kind == kind;
                if (tool.fill != null) tool.fill.color = active ? activeFill : idleFill;
                if (tool.icon != null) tool.icon.color = active ? activeIcon : idleIcon;
            }
        }

        /// <summary>Greys out a tool that is not ready yet (Draw until T5).</summary>
        public void SetToolEnabled(TableToolKind kind, bool enabled)
        {
            for (int i = 0; i < tools.Length; i++)
            {
                if (tools[i] != null && tools[i].kind == kind && tools[i].button != null) tools[i].button.interactable = enabled;
            }
        }

        /// <summary>The keys for what the player is doing now. Only writes when the text changed (no per-frame work).</summary>
        public void SetKeys(string text)
        {
            if (ReferenceEquals(text, shownKeys)) return;
            shownKeys = text;
            keysLabel.text = text ?? string.Empty;
        }

        /// <summary>The player's own show/hide choice for the keys strip.</summary>
        public void SetKeysWanted(bool wanted)
        {
            keysWanted = wanted;
            keysStrip.SetActive(keysWanted && keysAllowed);
        }

        /// <summary>The Hints switch: with Hints off the keys strip stays hidden.</summary>
        public void SetKeysAllowed(bool allowed)
        {
            keysAllowed = allowed;
            keysStrip.SetActive(keysWanted && keysAllowed);
        }

        /// <summary>Shows the selection buttons while cards are selected.</summary>
        public void SetSelectionCount(int count)
        {
            if (count == shownCount) return;
            shownCount = count;
            selectionActions.SetActive(count > 0);
            groupButton.interactable = count >= 2;
            selectionCountLabel.text = count == 1 ? "1 card selected" : count + " cards selected";
        }

        /// <summary>The selection box between two screen points (pixels).</summary>
        public void ShowSelectionBox(Vector2 screenA, Vector2 screenB)
        {
            RectTransform parent = selectionBox.parent as RectTransform;
            if (parent == null) return;
            if (canvas == null) canvas = selectionBox.GetComponentInParent<Canvas>();
            UnityEngine.Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenA, eventCamera, out Vector2 a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenB, eventCamera, out Vector2 b);
            selectionBox.anchorMin = selectionBox.anchorMax = new Vector2(0.5f, 0.5f);
            selectionBox.pivot = new Vector2(0.5f, 0.5f);
            selectionBox.anchoredPosition = (a + b) * 0.5f;
            selectionBox.sizeDelta = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
            if (!selectionBox.gameObject.activeSelf) selectionBox.gameObject.SetActive(true);
        }

        public void HideSelectionBox()
        {
            if (selectionBox != null && selectionBox.gameObject.activeSelf) selectionBox.gameObject.SetActive(false);
        }

        public override void Unbind()
        {
            for (int i = 0; tools != null && i < tools.Length; i++)
            {
                if (tools[i]?.button != null) tools[i].button.onClick.RemoveAllListeners();
            }

            if (keysToggle != null) keysToggle.onClick.RemoveAllListeners();
            if (helpButton != null) helpButton.onClick.RemoveAllListeners();
            if (groupButton != null) groupButton.onClick.RemoveAllListeners();
            if (clearButton != null) clearButton.onClick.RemoveAllListeners();
        }
    }
}
