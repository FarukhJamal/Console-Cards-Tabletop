using System;
using ConsoleCards.Presentation.UI.Toolbox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// The session bar (UI-1): the table's game chip, Undo, Redo and the Table menu, top left. An authored
    /// prefab built by Console Cards > UI > Build Session Bar Prefab; at runtime it only binds clicks and
    /// switches what is shown. The bar moves beside the Toolbox panel while that panel is open.
    /// </summary>
    public sealed class SessionBarView : ReusableUiView
    {
        public const string BuildHint = "Run Console Cards > UI > Build Session Bar Prefab.";

        [Header("Bar")]
        [SerializeField] private RectTransform bar;
        [SerializeField] private float barLeft;
        [SerializeField] private float barLeftBesidePanel;
        [SerializeField] private Image chipFill;
        [SerializeField] private GameObject chipGlyph;
        [SerializeField] private Text chipTitle;
        [SerializeField] private Text chipSubtitle;
        [SerializeField] private Color gameChipColor = Color.red;
        [SerializeField] private Color tableChipColor = Color.black;
        [SerializeField] private Button undoButton;
        [SerializeField] private Image undoIcon;
        [SerializeField] private SessionBarHoverTarget undoHover;
        [SerializeField] private Button redoButton;
        [SerializeField] private Image redoIcon;
        [SerializeField] private SessionBarHoverTarget redoHover;
        [SerializeField] private Button tableButton;
        [SerializeField] private Color iconColor = Color.black;
        [SerializeField] private float disabledIconAlpha = 0.35f;

        [Header("Hover tip")]
        [SerializeField] private RectTransform tip;
        [SerializeField] private Text tipLabel;
        [SerializeField] private float tipGap = 10f;

        [Header("Table menu")]
        [SerializeField] private Button menuBlocker;
        [SerializeField] private RectTransform menu;
        [SerializeField] private float menuGap = 12f;
        [SerializeField] private Button newTableButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button clearTableButton;

        [Header("Table settings (doc 23, R1)")]
        [SerializeField] private ToolboxSwitch rulesSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch hintsSwitch = new ToolboxSwitch();
        [SerializeField] private Button helpButton;

        private readonly Vector3[] corners = new Vector3[4];
        private Action undo;
        private Action redo;
        private Action newTable;
        private Action resetTable;
        private Action clearTable;
        private Action beforeMenuOpen;
        private string undoLabel = "Undo";
        private string redoLabel = "Redo";
        private RectTransform tipOwner;
        private Action<bool> setRules;
        private Action<bool> setHints;
        private Action openHelp;
        private bool rulesOn;
        private bool hintsOn;

        public bool IsMenuOpen => menu != null && menu.gameObject.activeSelf;

        public void ValidateReferences()
        {
            if (bar == null || chipFill == null || chipGlyph == null || chipTitle == null || chipSubtitle == null
                || undoButton == null || undoIcon == null || undoHover == null
                || redoButton == null || redoIcon == null || redoHover == null
                || tableButton == null || tip == null || tipLabel == null
                || menuBlocker == null || menu == null
                || newTableButton == null || resetButton == null || clearTableButton == null
                || rulesSwitch == null || !rulesSwitch.IsComplete || hintsSwitch == null || !hintsSwitch.IsComplete
                || helpButton == null)
            {
                throw new InvalidOperationException("SessionBarView is missing an authored reference. " + BuildHint);
            }
        }

        /// <summary>
        /// Shows the bar for the current table. <paramref name="isGame"/> picks the game chip (orange, card glyph,
        /// mode line) or the plain table chip. <paramref name="beforeMenuOpen"/> runs before the Table menu opens.
        /// </summary>
        public void Bind(
            string title,
            string subtitle,
            bool isGame,
            Action undoAction,
            Action redoAction,
            Action newTableAction,
            Action resetAction,
            Action clearTableAction,
            Action beforeMenuOpenAction)
        {
            RequireAcquired();
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("The session bar requires a title.", nameof(title));
            }

            undo = undoAction ?? throw new ArgumentNullException(nameof(undoAction));
            redo = redoAction ?? throw new ArgumentNullException(nameof(redoAction));
            newTable = newTableAction ?? throw new ArgumentNullException(nameof(newTableAction));
            resetTable = resetAction ?? throw new ArgumentNullException(nameof(resetAction));
            clearTable = clearTableAction ?? throw new ArgumentNullException(nameof(clearTableAction));
            ValidateReferences();
            RemoveListeners();
            beforeMenuOpen = beforeMenuOpenAction;

            chipFill.color = isGame ? gameChipColor : tableChipColor;
            chipGlyph.SetActive(isGame);
            chipTitle.text = title;
            bool hasSubtitle = isGame && !string.IsNullOrWhiteSpace(subtitle);
            chipSubtitle.gameObject.SetActive(hasSubtitle);
            chipSubtitle.text = hasSubtitle ? subtitle : string.Empty;

            undoButton.onClick.AddListener(HandleUndo);
            redoButton.onClick.AddListener(HandleRedo);
            tableButton.onClick.AddListener(ToggleMenu);
            menuBlocker.onClick.AddListener(CloseMenu);
            newTableButton.onClick.AddListener(HandleNewTable);
            resetButton.onClick.AddListener(HandleReset);
            clearTableButton.onClick.AddListener(HandleClearTable);
            undoHover.SetListener(HandleUndoHover);
            redoHover.SetListener(HandleRedoHover);

            SetShiftedForPanel(false);
            CloseMenu();
            HideTip();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
        }

        /// <summary>
        /// Binds the Table menu's view settings (doc 23, R1): the Rules card switch (shown only when the table has
        /// rules), the Hints switch and Help. These are the viewer's settings, not table state.
        /// </summary>
        public void BindTableSettings(
            bool showRulesSwitch,
            bool rulesCardOn,
            Action<bool> setRulesCard,
            bool hintsEnabled,
            Action<bool> setHintsEnabled,
            Action openHelpAction)
        {
            RequireAcquired();
            ValidateReferences();
            setRules = setRulesCard;
            setHints = setHintsEnabled ?? throw new ArgumentNullException(nameof(setHintsEnabled));
            openHelp = openHelpAction ?? throw new ArgumentNullException(nameof(openHelpAction));
            rulesOn = rulesCardOn;
            hintsOn = hintsEnabled;
            rulesSwitch.Row.SetActive(showRulesSwitch && setRules != null);
            rulesSwitch.Show(rulesOn);
            hintsSwitch.Show(hintsOn);
            Clear(rulesSwitch.Button);
            Clear(hintsSwitch.Button);
            Clear(helpButton);
            rulesSwitch.Button.onClick.AddListener(FlipRules);
            hintsSwitch.Button.onClick.AddListener(FlipHints);
            helpButton.onClick.AddListener(HandleHelp);
        }

        public void SetUndoState(bool enabled, string label)
        {
            undoLabel = string.IsNullOrWhiteSpace(label) ? "Undo" : label;
            SetButtonState(undoButton, undoIcon, enabled);
            RefreshTip(undoButton, undoLabel);
        }

        public void SetRedoState(bool enabled, string label)
        {
            redoLabel = string.IsNullOrWhiteSpace(label) ? "Redo" : label;
            SetButtonState(redoButton, redoIcon, enabled);
            RefreshTip(redoButton, redoLabel);
        }

        /// <summary>Moves the bar beside the open Toolbox panel, or back to the corner.</summary>
        public void SetShiftedForPanel(bool shifted)
        {
            if (bar == null)
            {
                return;
            }

            Vector2 position = bar.anchoredPosition;
            position.x = shifted ? barLeftBesidePanel : barLeft;
            bar.anchoredPosition = position;
            if (shifted)
            {
                CloseMenu();
            }

            HideTip();
        }

        public void OpenMenu()
        {
            if (menu == null || IsMenuOpen)
            {
                return;
            }

            beforeMenuOpen?.Invoke();
            HideTip();
            PlaceBelow(menu, (RectTransform)tableButton.transform, menuGap);
            menuBlocker.gameObject.SetActive(true);
            menu.gameObject.SetActive(true);
        }

        public void CloseMenu()
        {
            if (menu != null)
            {
                menu.gameObject.SetActive(false);
            }

            if (menuBlocker != null)
            {
                menuBlocker.gameObject.SetActive(false);
            }
        }

        public override void Unbind()
        {
            RemoveListeners();
            if (undoHover != null)
            {
                undoHover.SetListener(null);
            }

            if (redoHover != null)
            {
                redoHover.SetListener(null);
            }

            CloseMenu();
            HideTip();
            undo = null;
            redo = null;
            setRules = null;
            setHints = null;
            openHelp = null;
            newTable = null;
            resetTable = null;
            clearTable = null;
            beforeMenuOpen = null;
        }

        private void Update()
        {
            if (IsMenuOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMenu();
            }
        }

        private void ToggleMenu()
        {
            if (IsMenuOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        private void HandleUndo()
        {
            CloseMenu();
            undo?.Invoke();
        }

        private void HandleRedo()
        {
            CloseMenu();
            redo?.Invoke();
        }

        private void HandleNewTable()
        {
            CloseMenu();
            newTable?.Invoke();
        }

        private void HandleReset()
        {
            CloseMenu();
            resetTable?.Invoke();
        }

        private void HandleClearTable()
        {
            CloseMenu();
            clearTable?.Invoke();
        }

        private void FlipRules()
        {
            if (setRules == null)
            {
                return;
            }

            rulesOn = !rulesOn;
            rulesSwitch.Show(rulesOn);
            setRules(rulesOn);
        }

        private void FlipHints()
        {
            if (setHints == null)
            {
                return;
            }

            hintsOn = !hintsOn;
            hintsSwitch.Show(hintsOn);
            setHints(hintsOn);
        }

        private void HandleHelp()
        {
            CloseMenu();
            openHelp?.Invoke();
        }

        private void HandleUndoHover(bool hovered) => SetTip(undoButton, undoLabel, hovered);

        private void HandleRedoHover(bool hovered) => SetTip(redoButton, redoLabel, hovered);

        private void SetTip(Button owner, string label, bool hovered)
        {
            RectTransform ownerRect = (RectTransform)owner.transform;
            if (!hovered)
            {
                if (tipOwner == ownerRect)
                {
                    HideTip();
                }

                return;
            }

            if (IsMenuOpen)
            {
                return;
            }

            tipOwner = ownerRect;
            tipLabel.text = label;
            tip.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tip);
            PlaceBelow(tip, ownerRect, tipGap);
        }

        private void RefreshTip(Button owner, string label)
        {
            if (tip != null && tip.gameObject.activeSelf && tipOwner == (RectTransform)owner.transform)
            {
                tipLabel.text = label;
            }
        }

        private void HideTip()
        {
            tipOwner = null;
            if (tip != null)
            {
                tip.gameObject.SetActive(false);
            }
        }

        // Puts the target's top-left corner under the anchor's bottom-left corner (both pivot top left).
        private void PlaceBelow(RectTransform target, RectTransform anchor, float gap)
        {
            anchor.GetWorldCorners(corners);
            target.position = corners[0];
            Vector2 position = target.anchoredPosition;
            position.y -= gap;
            target.anchoredPosition = position;
        }

        private void SetButtonState(Button button, Image icon, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = enabled;
            if (icon != null)
            {
                Color color = iconColor;
                color.a = enabled ? iconColor.a : disabledIconAlpha;
                icon.color = color;
            }
        }

        private void RemoveListeners()
        {
            Clear(undoButton);
            Clear(redoButton);
            Clear(tableButton);
            Clear(menuBlocker);
            Clear(newTableButton);
            Clear(resetButton);
            Clear(clearTableButton);
            Clear(rulesSwitch?.Button);
            Clear(hintsSwitch?.Button);
            Clear(helpButton);
        }

        private static void Clear(Button button)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }
    }
}
