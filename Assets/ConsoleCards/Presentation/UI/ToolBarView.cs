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
        /// <summary>HUD-B board 7B: a drag moves the camera (Space + drag does too, with any tool).</summary>
        Pan = 5,
        /// <summary>HUD-B board 7B: a drag measures on the table.</summary>
        Measure = 6,
    }

    /// <summary>
    /// The tool bar (TOOLS-1), bottom right, in the Toolbox look: the table tools (Grab, Select, Ping, Flick, and
    /// Draw, with its panel of pen, highlighter, label, eraser, colours and Clear while it is chosen), the keys strip that names the keys for what you are doing now (hideable, and off
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

        /// <summary>V5 (owner, 2026-10-24): one key in its box and what it does, on the Controls bar.</summary>
        [Serializable]
        public sealed class KeyChip
        {
            public GameObject root;
            public Text key;
            public Text action;
            public GameObject divider;
        }

        private const string KeyOpen = "<b><color=#F2A81D>";
        private const string KeyClose = "</color></b>";

        [SerializeField] private ToolButton[] tools = new ToolButton[0];
        [SerializeField] private Color idleFill = new Color(0.992f, 0.973f, 0.902f, 1f);
        [SerializeField] private Color activeFill = new Color(0.949f, 0.659f, 0.114f, 1f);
        [SerializeField] private Color idleIcon = new Color(0.114f, 0.114f, 0.122f, 1f);
        [SerializeField] private Color activeIcon = new Color(0.114f, 0.114f, 0.122f, 1f);
        [Header("Keys strip")]
        [SerializeField] private GameObject keysStrip;
        [SerializeField] private Text keysLabel;
        [Tooltip("Optional (V5): the keys as chips, as many as fit keyChipsWidth; set by Apply Kit Look (UI-1).")]
        [SerializeField] private KeyChip[] keyChips = new KeyChip[0];
        [SerializeField] private float keyChipsWidth = 560f;
        [SerializeField] private float keyChipPadding = 40f;
        [Tooltip("Optional (V5): the Controls bar holding the keys; it hides with them.")]
        [SerializeField] private GameObject keysBar;
        [SerializeField] private Button keysToggle;
        [SerializeField] private Button helpButton;
        [Tooltip("Optional (owner, 2026-10-19): switches the hand tray between a fan and a straight row.")]
        [SerializeField] private Button handStyleButton;
        [SerializeField] private Text handStyleLabel;
        [Header("Selection")]
        [SerializeField] private GameObject selectionActions;
        [SerializeField] private Text selectionCountLabel;
        [SerializeField] private Button groupButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private RectTransform selectionBox;
        [Header("Draw tool (added by Create Missing Gameplay UI Prefabs; Draw stays greyed without it)")]
        [SerializeField] private GameObject drawActions;
        [Tooltip("Pen, Highlighter, Label, Eraser, in that order.")]
        [SerializeField] private Button[] drawModeButtons = new Button[0];
        [SerializeField] private Image[] drawModeFills = new Image[0];
        [Tooltip("The colour swatches; the first is your seat's colour.")]
        [SerializeField] private Button[] drawSwatchButtons = new Button[0];
        [SerializeField] private Image[] drawSwatchImages = new Image[0];
        [SerializeField] private InputField drawLabelInput;
        [SerializeField] private Button drawClearButton;
        [Tooltip("Optional: cycles the line style (Plain, Brush, Swirl, Wavy, Dashed, Dotted); its label shows the style.")]
        [SerializeField] private Button drawStyleButton;
        [SerializeField] private Text drawStyleLabel;

        private Action<TableToolKind> chooseTool;
        private Action toggleKeys;
        private Action openHelp;
        private Action groupSelection;
        private Action toggleHandStyle;
        private Action clearSelection;
        private string shownKeys;
        private bool keysWanted = true;
        private bool keysAllowed = true;
        private bool buttonsHidden;
        private int shownCount = -1;
        private Canvas canvas;
        private Action<int> chooseDrawMode;
        private Action<int> chooseDrawColour;
        private Action clearDrawing;
        private Action cycleDrawStyle;

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
            Action clear,
            Action handStyle = null)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            chooseTool = choose;
            toggleKeys = toggleKeysStrip;
            openHelp = help;
            groupSelection = group;
            clearSelection = clear;
            toggleHandStyle = handStyle;
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
            if (handStyleButton != null)
            {
                handStyleButton.gameObject.SetActive(handStyle != null);
                handStyleButton.onClick.AddListener(() => toggleHandStyle?.Invoke());
            }
            shownKeys = null;
            shownCount = -1;
            SetSelectionCount(0);
            HideSelectionBox();
        }

        /// <summary>True when the prefab has the Draw tool's panel (made by the UI builder).</summary>
        public bool HasDrawPanel => drawActions != null
            && drawModeButtons != null
            && drawModeButtons.Length >= 4
            && drawSwatchButtons != null
            && drawSwatchButtons.Length > 0
            && drawLabelInput != null
            && drawClearButton != null;

        /// <summary>How many colour swatches the panel has.</summary>
        public int DrawSwatchCount => drawSwatchButtons != null ? drawSwatchButtons.Length : 0;

        /// <summary>The words typed for the next label.</summary>
        public string DrawLabelText => drawLabelInput != null ? drawLabelInput.text : string.Empty;

        /// <summary>The Draw panel's buttons (after <see cref="Bind"/>): a drawing mode, a colour, Clear, the style.</summary>
        public void BindDraw(Action<int> chooseMode, Action<int> chooseColour, Action clear, Action cycleStyle = null)
        {
            if (!HasDrawPanel) return;
            chooseDrawMode = chooseMode;
            chooseDrawColour = chooseColour;
            clearDrawing = clear;
            cycleDrawStyle = cycleStyle;
            if (drawStyleButton != null) drawStyleButton.onClick.AddListener(() => cycleDrawStyle?.Invoke());
            for (int i = 0; i < drawModeButtons.Length; i++)
            {
                if (drawModeButtons[i] == null) continue;
                int mode = i;
                drawModeButtons[i].onClick.AddListener(() => chooseDrawMode?.Invoke(mode));
            }

            for (int i = 0; i < drawSwatchButtons.Length; i++)
            {
                if (drawSwatchButtons[i] == null) continue;
                int colour = i;
                drawSwatchButtons[i].onClick.AddListener(() => chooseDrawColour?.Invoke(colour));
            }

            drawClearButton.onClick.AddListener(() => clearDrawing?.Invoke());
        }

        /// <summary>Shows the Draw panel while the Draw tool is chosen.</summary>
        public void ShowDraw(bool shown)
        {
            if (drawActions != null && drawActions.activeSelf != shown) drawActions.SetActive(shown);
        }

        /// <summary>Lights the drawing mode (0 pen, 1 highlighter, 2 label, 3 eraser); the words box shows for labels.</summary>
        public void SetDrawMode(int mode)
        {
            for (int i = 0; drawModeFills != null && i < drawModeFills.Length; i++)
            {
                if (drawModeFills[i] != null) drawModeFills[i].color = i == mode ? activeFill : idleFill;
            }

            if (drawLabelInput != null) drawLabelInput.gameObject.SetActive(mode == 2);
        }

        /// <summary>The style button's name (PLAIN, BRUSH, SWIRL, WAVY, DASHED, DOTTED).</summary>
        public void SetDrawStyle(string name)
        {
            if (drawStyleLabel != null) drawStyleLabel.text = name;
        }

        /// <summary>The swatches' colours, and which one is chosen (it shows a little bigger).</summary>
        public void SetDrawSwatches(Color[] colours, int chosen)
        {
            for (int i = 0; drawSwatchImages != null && i < drawSwatchImages.Length; i++)
            {
                Image swatch = drawSwatchImages[i];
                if (swatch == null) continue;
                if (colours != null && i < colours.Length) swatch.color = colours[i];
                float scale = i == chosen ? 1.25f : 1f;
                swatch.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
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
                if (tool.label != null) tool.label.color = active ? activeIcon : idleIcon;
            }
        }

        /// <summary>The hand-style button's name: FAN when the tray is a fan, ROW when it is straight.</summary>
        public void SetHandStyle(bool fan)
        {
            if (handStyleLabel != null) handStyleLabel.text = fan ? "FAN" : "ROW";
        }

        /// <summary>Greys out a tool that cannot be used (Draw without its panel or its line prefabs).</summary>
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
            if (keyChips != null && keyChips.Length > 0) ShowKeyChips(text ?? string.Empty);
        }

        // The keys text is "<key> what it does" pairs; the chips show the first ones that fit the bar.
        private void ShowKeyChips(string text)
        {
            float used = 0f;
            int shown = 0;
            int at = 0;
            while (shown < keyChips.Length)
            {
                KeyChip chip = keyChips[shown];
                int open = text.IndexOf(KeyOpen, at, StringComparison.Ordinal);
                if (chip == null || chip.root == null || chip.key == null || chip.action == null || open < 0) break;
                int close = text.IndexOf(KeyClose, open, StringComparison.Ordinal);
                if (close < 0) break;
                int next = text.IndexOf(KeyOpen, close, StringComparison.Ordinal);
                int end = next < 0 ? text.Length : next;
                chip.key.text = text.Substring(open + KeyOpen.Length, close - open - KeyOpen.Length).ToUpperInvariant();
                chip.action.text = text.Substring(close + KeyClose.Length, end - close - KeyClose.Length).Trim();
                float width = chip.key.preferredWidth + chip.action.preferredWidth + keyChipPadding;
                if (shown > 0 && used + width > keyChipsWidth) break;
                used += width;
                chip.root.SetActive(true);
                if (chip.divider != null) chip.divider.SetActive(shown > 0);
                shown++;
                at = end;
            }

            for (int i = shown; i < keyChips.Length; i++)
            {
                if (keyChips[i] != null && keyChips[i].root != null) keyChips[i].root.SetActive(false);
            }
        }

        /// <summary>
        /// HUD-B: hides the tool buttons, the keys strip, Help and FAN / ROW (the new HUD's rail has the tools); the
        /// selection buttons, the selection box and the Draw panel still show here when they are needed.
        /// </summary>
        public void SetButtonsHidden(bool hidden) => SetButtonsHidden(hidden, Vector2.zero);

        /// <summary>As above; <paramref name="panelShift"/> moves the Draw panel and the selection buttons while hidden
        /// (HUD-B: clear of the turn's actions bottom right).</summary>
        public void SetButtonsHidden(bool hidden, Vector2 panelShift)
        {
            ShiftPanel(drawActions != null ? drawActions.transform as RectTransform : null, ref drawHome, ref drawHomeKnown,
                hidden ? panelShift : Vector2.zero);
            ShiftPanel(selectionActions != null ? selectionActions.transform as RectTransform : null, ref selectionHome,
                ref selectionHomeKnown, hidden ? panelShift : Vector2.zero);
            buttonsHidden = hidden;
            Transform toolsParent = SharedToolParent();
            for (int i = 0; i < tools.Length; i++)
            {
                if (tools[i]?.button != null && toolsParent == null) tools[i].button.gameObject.SetActive(!hidden);
            }

            if (toolsParent != null) toolsParent.gameObject.SetActive(!hidden);
            if (keysToggle != null) keysToggle.gameObject.SetActive(!hidden);
            if (helpButton != null) helpButton.gameObject.SetActive(!hidden);
            if (handStyleButton != null) handStyleButton.gameObject.SetActive(!hidden && toggleHandStyle != null);
            keysStrip.SetActive(keysWanted && keysAllowed && !hidden);
            if (keysBar != null) keysBar.SetActive(keysWanted && keysAllowed && !hidden);
        }

        private Vector2 drawHome;
        private bool drawHomeKnown;
        private Vector2 selectionHome;
        private bool selectionHomeKnown;

        private static void ShiftPanel(RectTransform panel, ref Vector2 home, ref bool known, Vector2 shift)
        {
            if (panel == null) return;
            if (!known)
            {
                home = panel.anchoredPosition;
                known = true;
            }

            panel.anchoredPosition = home + shift;
        }

        // The tool buttons' own bar, when they share one that holds nothing else this view still needs.
        private Transform SharedToolParent()
        {
            Transform parent = null;
            for (int i = 0; i < tools.Length; i++)
            {
                if (tools[i]?.button == null) continue;
                Transform candidate = tools[i].button.transform.parent;
                if (parent == null) parent = candidate;
                else if (candidate != parent) return null;
            }

            if (parent == null || parent == transform) return null;
            if (selectionActions != null && selectionActions.transform.IsChildOf(parent)) return null;
            if (drawActions != null && drawActions.transform.IsChildOf(parent)) return null;
            if (selectionBox != null && selectionBox.IsChildOf(parent)) return null;
            return parent;
        }

        /// <summary>The player's own show/hide choice for the keys strip.</summary>
        public void SetKeysWanted(bool wanted)
        {
            keysWanted = wanted;
            keysStrip.SetActive(keysWanted && keysAllowed && !buttonsHidden);
            if (keysBar != null) keysBar.SetActive(keysWanted && keysAllowed && !buttonsHidden);
        }

        /// <summary>The Hints switch: with Hints off the keys strip stays hidden.</summary>
        public void SetKeysAllowed(bool allowed)
        {
            keysAllowed = allowed;
            keysStrip.SetActive(keysWanted && keysAllowed && !buttonsHidden);
            if (keysBar != null) keysBar.SetActive(keysWanted && keysAllowed && !buttonsHidden);
        }

        /// <summary>Shows the selection buttons while pieces are selected (TOOLS-1b: any component but boards and Consoles).</summary>
        public void SetSelectionCount(int count)
        {
            if (count == shownCount) return;
            shownCount = count;
            selectionActions.SetActive(count > 0);
            groupButton.interactable = count >= 2;
            selectionCountLabel.text = count + " selected";
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
            if (handStyleButton != null) handStyleButton.onClick.RemoveAllListeners();
            for (int i = 0; drawModeButtons != null && i < drawModeButtons.Length; i++)
            {
                if (drawModeButtons[i] != null) drawModeButtons[i].onClick.RemoveAllListeners();
            }

            for (int i = 0; drawSwatchButtons != null && i < drawSwatchButtons.Length; i++)
            {
                if (drawSwatchButtons[i] != null) drawSwatchButtons[i].onClick.RemoveAllListeners();
            }

            if (drawClearButton != null) drawClearButton.onClick.RemoveAllListeners();
            if (drawStyleButton != null) drawStyleButton.onClick.RemoveAllListeners();
            cycleDrawStyle = null;
            chooseDrawMode = null;
            chooseDrawColour = null;
            clearDrawing = null;
        }
    }
}
