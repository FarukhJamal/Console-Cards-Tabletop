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
    /// switches what is shown. Every element (bar, tips, Table menu) is placed in the prefab; nothing is
    /// positioned at runtime.
    /// </summary>
    public sealed class SessionBarView : ReusableUiView
    {
        public const string BuildHint = "Run Console Cards > UI > Build Session Bar Prefab.";

        [Header("Bar")]
        [SerializeField] private RectTransform bar;
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

        [Header("Hover tips (each placed under its button in the prefab)")]
        [SerializeField] private GameObject undoTip;
        [SerializeField] private Text undoTipLabel;
        [SerializeField] private GameObject redoTip;
        [SerializeField] private Text redoTipLabel;

        [Header("Table menu")]
        [SerializeField] private Button menuBlocker;
        [SerializeField] private RectTransform menu;
        [SerializeField] private Button newTableButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button clearTableButton;

        [Header("Table settings (doc 23, R1)")]
        [SerializeField] private ToolboxSwitch rulesSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch hintsSwitch = new ToolboxSwitch();
        [SerializeField] private Button helpButton;
        [Tooltip("Optional (doc 23 §10): tips about the table and the game. Added by Add Missing Table Menu Rows.")]
        [SerializeField] private ToolboxSwitch tipsSwitch = new ToolboxSwitch();

        [Header("Play (doc 18 §15.11; optional, added by Add Missing Table Menu Rows)")]
        [SerializeField] private Button seatButton;
        [SerializeField] private Text seatLabel;
        [SerializeField] private ToolboxSwitch hotSeatSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch handsOnTableSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch fullFeedbackSwitch = new ToolboxSwitch();
        [SerializeField] private Button cardArtButton;

        [Header("Card editor (L2-2; optional, added by Add Missing Table Menu Rows)")]
        [SerializeField] private Button cardEditorButton;

        [Header("Save table as a game (doc 29; optional, added by Add Missing Table Menu Rows)")]
        [SerializeField] private Button saveGameButton;

        [Header("Deck editor (L2-3; optional, added by Add Missing Table Menu Rows)")]
        [SerializeField] private Button deckEditorButton;

        [Header("Profile (doc 28; optional, added by Add Missing Table Menu Rows)")]
        [Tooltip("The Table menu row \"Profile: name…\" that opens the profile panel.")]
        [SerializeField] private Button profileButton;
        [SerializeField] private Text profileLabel;

        [Header("Table shape (T1; optional, added by Add Missing Table Menu Rows)")]
        [Tooltip("The Table menu row \"Table: shape\": each press changes the table to the next shape.")]
        [SerializeField] private Button tableShapeButton;
        [SerializeField] private Text tableShapeLabel;

        [Header("Table edge (T2; optional, added by Add Missing Table Menu Rows)")]
        [Tooltip("The Table menu row \"Edge: kind\": each press changes the built table's edge to the next kind.")]
        [SerializeField] private Button tableEdgeButton;
        [SerializeField] private Text tableEdgeLabel;

        [Header("Table tools (T3; optional, added by Add Missing Table Menu Rows)")]
        [Tooltip("The Table menu row \"Table tools…\" that opens Table mode.")]
        [SerializeField] private Button tableToolsButton;

        [Header("Shop (doc 18 §15.4; optional, added by Add Missing Table Menu Rows)")]
        [Tooltip("The Shop entry on the bar: the shop icon and the word Shop, between Redo and Table.")]
        [SerializeField] private Button shopButton;

        private Action undo;
        private Action redo;
        private Action newTable;
        private Action resetTable;
        private Action clearTable;
        private Action beforeMenuOpen;

        // The Table menu scrolls with the mouse wheel when it is taller than the room below the bar (owner,
        // 2026-10-20: the lower rows, Table shape and Table tools, were off the screen). The menu itself slides up;
        // nothing is built at runtime.
        private const float MenuScrollStep = 60f;
        private const float MenuScreenMargin = 12f;
        private readonly Vector3[] menuCorners = new Vector3[4];
        private Canvas menuCanvas;
        private Vector2 menuHome;
        private bool menuHomeKnown;
        private float menuScroll;
        private string undoLabel = "Undo";
        private string redoLabel = "Redo";
        private Action<bool> setRules;
        private Action<bool> setHints;
        private Action openHelp;
        private bool rulesOn;
        private bool hintsOn;
        private Action<bool> setTips;
        private bool tipsOn;
        private Action nextSeat;
        private Action<bool> setHotSeat;
        private bool hotSeatOn;
        private Action<bool> setHandsOnTable;
        private Action<bool> setFullFeedback;
        private bool handsOnTableOn;
        private bool fullFeedbackOn;
        private Action openCardArt;
        private Action openProfile;
        private Func<string> cycleTableShape;
        private Func<string> cycleTableEdge;
        private Action openTableTools;
        private Action openCardEditor;
        private Action openDeckEditor;
        private Action openSaveGame;
        private Action openShop;

        public bool IsMenuOpen => menu != null && menu.gameObject.activeSelf;

        public void ValidateReferences()
        {
            if (bar == null || chipFill == null || chipGlyph == null || chipTitle == null || chipSubtitle == null
                || undoButton == null || undoIcon == null || undoHover == null
                || redoButton == null || redoIcon == null || redoHover == null
                || tableButton == null || undoTip == null || undoTipLabel == null
                || redoTip == null || redoTipLabel == null
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

        /// <summary>
        /// Binds the optional play rows: Seat (which player this screen plays; click for the next seat) and the
        /// Hot-seat switch. Rows are hidden when <paramref name="available"/> is false. Returns false when the
        /// prefab does not have the rows.
        /// </summary>
        public bool BindPlaySettings(
            bool available,
            string seatText,
            Action nextSeatAction,
            bool hotSeatEnabled,
            Action<bool> setHotSeatEnabled)
        {
            RequireAcquired();
            bool hasRows = seatButton != null && seatLabel != null && hotSeatSwitch != null && hotSeatSwitch.IsComplete;
            if (!hasRows)
            {
                return false;
            }

            Clear(seatButton);
            Clear(hotSeatSwitch.Button);
            nextSeat = available ? nextSeatAction : null;
            setHotSeat = available ? setHotSeatEnabled : null;
            hotSeatOn = hotSeatEnabled;
            seatButton.gameObject.SetActive(available && nextSeat != null);
            hotSeatSwitch.Row.SetActive(available && setHotSeat != null);
            seatLabel.text = seatText ?? string.Empty;
            seatButton.interactable = !hotSeatOn;
            hotSeatSwitch.Show(hotSeatOn);
            seatButton.onClick.AddListener(HandleNextSeat);
            hotSeatSwitch.Button.onClick.AddListener(FlipHotSeat);
            return true;
        }

        /// <summary>
        /// Binds the optional view rows (doc 25, F3): Hands on table (your hand on your table edge instead of the
        /// screen tray; tables with seats only) and Feedback (Full, or Minimal: messages and board markers only).
        /// Returns false when the prefab does not have the rows.
        /// </summary>
        public bool BindViewSettings(
            bool handsOnTableAvailable,
            bool handsOnTable,
            Action<bool> setHandsOnTableAction,
            bool fullFeedback,
            Action<bool> setFullFeedbackAction)
        {
            RequireAcquired();
            bool hasRows = handsOnTableSwitch != null && handsOnTableSwitch.IsComplete
                && fullFeedbackSwitch != null && fullFeedbackSwitch.IsComplete;
            if (!hasRows)
            {
                return false;
            }

            Clear(handsOnTableSwitch.Button);
            Clear(fullFeedbackSwitch.Button);
            setHandsOnTable = handsOnTableAvailable ? setHandsOnTableAction : null;
            setFullFeedback = setFullFeedbackAction;
            handsOnTableOn = handsOnTable;
            fullFeedbackOn = fullFeedback;
            handsOnTableSwitch.Row.SetActive(setHandsOnTable != null);
            fullFeedbackSwitch.Row.SetActive(setFullFeedback != null);
            handsOnTableSwitch.Show(handsOnTableOn);
            fullFeedbackSwitch.Show(fullFeedbackOn);
            handsOnTableSwitch.Button.onClick.AddListener(FlipHandsOnTable);
            fullFeedbackSwitch.Button.onClick.AddListener(FlipFullFeedback);
            return true;
        }

        /// <summary>
        /// Binds the optional Tips switch (doc 23 §10): on, the hint strip teaches the table and the game; off, only
        /// reminders show. The row shows only while Hints are on. False when the prefab lacks the row.
        /// </summary>
        public bool BindTips(bool tipsEnabled, Action<bool> setTipsAction)
        {
            RequireAcquired();
            if (tipsSwitch == null || !tipsSwitch.IsComplete) return false;
            Clear(tipsSwitch.Button);
            setTips = setTipsAction;
            tipsOn = tipsEnabled;
            tipsSwitch.Row.SetActive(setTips != null && hintsOn);
            tipsSwitch.Show(tipsOn);
            tipsSwitch.Button.onClick.AddListener(FlipTips);
            return true;
        }

        /// <summary>Binds the optional "Card art…" row (doc 24, A3/A4); null hides it. False when the prefab lacks it.</summary>
        public bool BindCardArt(Action openCardArtAction)
        {
            RequireAcquired();
            if (cardArtButton == null)
            {
                return false;
            }

            Clear(cardArtButton);
            openCardArt = openCardArtAction;
            cardArtButton.gameObject.SetActive(openCardArt != null);
            cardArtButton.onClick.AddListener(HandleCardArt);
            return true;
        }

        /// <summary>Binds the optional "Card editor…" row (L2-2); null hides it. False when the prefab lacks it.</summary>
        public bool BindCardEditor(Action openCardEditorAction)
        {
            RequireAcquired();
            if (cardEditorButton == null)
            {
                return false;
            }

            Clear(cardEditorButton);
            openCardEditor = openCardEditorAction;
            cardEditorButton.gameObject.SetActive(openCardEditor != null);
            cardEditorButton.onClick.AddListener(HandleCardEditor);
            return true;
        }

        /// <summary>Binds the optional "Save table as a game…" row (doc 29); null hides it. False when the prefab lacks it.</summary>
        public bool BindSaveGame(Action openSaveGameAction)
        {
            RequireAcquired();
            if (saveGameButton == null)
            {
                return false;
            }

            Clear(saveGameButton);
            openSaveGame = openSaveGameAction;
            saveGameButton.gameObject.SetActive(openSaveGame != null);
            saveGameButton.onClick.AddListener(HandleSaveGame);
            return true;
        }

        /// <summary>Binds the optional "Deck editor…" row (L2-3); null hides it. False when the prefab lacks it.</summary>
        public bool BindDeckEditor(Action openDeckEditorAction)
        {
            RequireAcquired();
            if (deckEditorButton == null)
            {
                return false;
            }

            Clear(deckEditorButton);
            openDeckEditor = openDeckEditorAction;
            deckEditorButton.gameObject.SetActive(openDeckEditor != null);
            deckEditorButton.onClick.AddListener(HandleDeckEditor);
            return true;
        }

        /// <summary>
        /// Binds the optional "Profile: name…" row (doc 28, P2); null hides it. False when the prefab lacks it.
        /// </summary>
        public bool BindProfile(string profileName, Action openProfileAction)
        {
            RequireAcquired();
            if (profileButton == null)
            {
                return false;
            }

            Clear(profileButton);
            openProfile = openProfileAction;
            if (profileLabel != null) profileLabel.text = $"Profile: {profileName}…";
            profileButton.gameObject.SetActive(openProfile != null);
            profileButton.onClick.AddListener(HandleProfile);
            return true;
        }

        /// <summary>
        /// Binds the optional "Table: shape" row (T1); null hides it. A press cycles the shape and shows the text the
        /// action returns; the menu stays open so the shapes can be stepped through. False when the prefab lacks it.
        /// </summary>
        public bool BindTableShape(string label, Func<string> cycleTableShapeAction)
        {
            RequireAcquired();
            if (tableShapeButton == null)
            {
                return false;
            }

            Clear(tableShapeButton);
            cycleTableShape = cycleTableShapeAction;
            if (tableShapeLabel != null) tableShapeLabel.text = label;
            tableShapeButton.gameObject.SetActive(cycleTableShape != null);
            tableShapeButton.onClick.AddListener(HandleTableShape);
            return true;
        }

        /// <summary>
        /// Binds the optional "Edge: kind" row (T2); null hides it. A press cycles the edge and shows the text the
        /// action returns; the menu stays open. False when the prefab lacks it.
        /// </summary>
        public bool BindTableEdge(string label, Func<string> cycleTableEdgeAction)
        {
            RequireAcquired();
            if (tableEdgeButton == null)
            {
                return false;
            }

            Clear(tableEdgeButton);
            cycleTableEdge = cycleTableEdgeAction;
            if (tableEdgeLabel != null) tableEdgeLabel.text = label;
            tableEdgeButton.gameObject.SetActive(cycleTableEdge != null);
            tableEdgeButton.onClick.AddListener(HandleTableEdge);
            return true;
        }

        /// <summary>Binds the optional "Table tools…" row (T3); null hides it. False when the prefab lacks it.</summary>
        public bool BindTableTools(Action openTableToolsAction)
        {
            RequireAcquired();
            if (tableToolsButton == null)
            {
                return false;
            }

            Clear(tableToolsButton);
            openTableTools = openTableToolsAction;
            tableToolsButton.gameObject.SetActive(openTableTools != null);
            tableToolsButton.onClick.AddListener(HandleTableTools);
            return true;
        }

        /// <summary>
        /// Binds the optional Shop entry on the bar (doc 18 §15.4): shown while <paramref name="openShopAction"/> is
        /// set (a game whose Shop is open), hidden for null. False when the prefab lacks it.
        /// </summary>
        public bool BindShop(Action openShopAction)
        {
            RequireAcquired();
            if (shopButton == null)
            {
                return false;
            }

            Clear(shopButton);
            openShop = openShopAction;
            bool show = openShop != null;
            if (shopButton.gameObject.activeSelf != show)
            {
                shopButton.gameObject.SetActive(show);
                LayoutRebuilder.MarkLayoutForRebuild(bar);
            }

            shopButton.onClick.AddListener(HandleShop);
            return true;
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

        /// <summary>The Toolbox panel opened or closed: the menu closes when it opens (the bar keeps its place).</summary>
        public void SetShiftedForPanel(bool shifted)
        {
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
            menuBlocker.gameObject.SetActive(true);
            if (!menuHomeKnown)
            {
                menuHome = menu.anchoredPosition;
                menuHomeKnown = true;
            }

            menuScroll = 0f; // each time it opens at the top
            menu.anchoredPosition = menuHome;
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
            setTips = null;
            nextSeat = null;
            setHotSeat = null;
            setHandsOnTable = null;
            setFullFeedback = null;
            openCardArt = null;
            openProfile = null;
            cycleTableShape = null;
            cycleTableEdge = null;
            openTableTools = null;
            openCardEditor = null;
            openDeckEditor = null;
            openSaveGame = null;
            openShop = null;
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
                return;
            }

            if (IsMenuOpen) ScrollMenu();
        }

        // Mouse wheel (or Up / Down keys) while the Table menu is open: slides the menu so every row can be reached,
        // never further than its last row. Polled each frame; no allocation.
        private void ScrollMenu()
        {
            float maximum = MenuScrollRange();
            float step = 0f;
            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (wheel > 0.01f) step = -MenuScrollStep;
                else if (wheel < -0.01f) step = MenuScrollStep;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.downArrowKey.wasPressedThisFrame) step = MenuScrollStep;
                else if (Keyboard.current.upArrowKey.wasPressedThisFrame) step = -MenuScrollStep;
            }

            float next = Mathf.Clamp(menuScroll + step, 0f, maximum);
            if (Mathf.Approximately(next, menuScroll)) return;
            menuScroll = next;
            menu.anchoredPosition = menuHome + new Vector2(0f, menuScroll);
        }

        // How far the menu may slide up: its height less the room from its top (at home) to the screen's bottom.
        private float MenuScrollRange()
        {
            if (menuCanvas == null) menuCanvas = menu.GetComponentInParent<Canvas>();
            if (menuCanvas == null) return 0f;
            Canvas root = menuCanvas.rootCanvas;
            UnityEngine.Camera eye = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            float scale = Mathf.Max(0.0001f, root.scaleFactor);
            menu.GetWorldCorners(menuCorners);
            float topNow = RectTransformUtility.WorldToScreenPoint(eye, menuCorners[1]).y;
            float topAtHome = topNow - (menuScroll * scale);
            float room = (topAtHome - MenuScreenMargin) / scale;
            return Mathf.Max(0f, menu.rect.height - room);
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
            if (tipsSwitch != null && tipsSwitch.IsComplete) tipsSwitch.Row.SetActive(setTips != null && hintsOn);
            setHints(hintsOn);
        }

        private void FlipTips()
        {
            if (setTips == null) return;
            tipsOn = !tipsOn;
            tipsSwitch.Show(tipsOn);
            setTips(tipsOn);
        }

        private void HandleNextSeat()
        {
            if (nextSeat == null || hotSeatOn)
            {
                Debug.Log(hotSeatOn
                    ? "[Seat] The Seat row is off while Hot-seat is on."
                    : "[Seat] The Seat row has no action bound (not a table with seats).");
                return;
            }

            Action action = nextSeat;
            CloseMenu();
            action();
        }

        private void FlipHotSeat()
        {
            if (setHotSeat == null)
            {
                return;
            }

            hotSeatOn = !hotSeatOn;
            hotSeatSwitch.Show(hotSeatOn);
            seatButton.interactable = !hotSeatOn;
            setHotSeat(hotSeatOn);
        }

        private void FlipHandsOnTable()
        {
            if (setHandsOnTable == null)
            {
                return;
            }

            handsOnTableOn = !handsOnTableOn;
            handsOnTableSwitch.Show(handsOnTableOn);
            Action<bool> action = setHandsOnTable;
            CloseMenu(); // the table rebuilds for the new hand place
            action(handsOnTableOn);
        }

        private void FlipFullFeedback()
        {
            if (setFullFeedback == null)
            {
                return;
            }

            fullFeedbackOn = !fullFeedbackOn;
            fullFeedbackSwitch.Show(fullFeedbackOn);
            setFullFeedback(fullFeedbackOn);
        }

        private void HandleShop()
        {
            Action action = openShop;
            CloseMenu();
            action?.Invoke();
        }

        private void HandleCardArt()
        {
            Action action = openCardArt;
            CloseMenu();
            action?.Invoke();
        }

        private void HandleCardEditor()
        {
            Action action = openCardEditor;
            CloseMenu();
            action?.Invoke();
        }

        private void HandleSaveGame()
        {
            Action action = openSaveGame;
            CloseMenu();
            action?.Invoke();
        }

        private void HandleDeckEditor()
        {
            Action action = openDeckEditor;
            CloseMenu();
            action?.Invoke();
        }

        private void HandleProfile()
        {
            Action action = openProfile;
            CloseMenu();
            action?.Invoke();
        }

        private void HandleTableShape()
        {
            string label = cycleTableShape?.Invoke();
            if (tableShapeLabel != null && label != null) tableShapeLabel.text = label;
        }

        private void HandleTableTools()
        {
            Action action = openTableTools;
            CloseMenu();
            action?.Invoke();
        }

        private void HandleTableEdge()
        {
            string label = cycleTableEdge?.Invoke();
            if (tableEdgeLabel != null && label != null) tableEdgeLabel.text = label;
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
            GameObject tip = owner == undoButton ? undoTip : redoTip;
            Text tipLabel = owner == undoButton ? undoTipLabel : redoTipLabel;
            if (tip == null || tipLabel == null)
            {
                return;
            }

            if (!hovered || IsMenuOpen)
            {
                tip.SetActive(false);
                return;
            }

            tipLabel.text = label;
            tip.SetActive(true);
        }

        private void RefreshTip(Button owner, string label)
        {
            Text tipLabel = owner == undoButton ? undoTipLabel : redoTipLabel;
            if (tipLabel != null)
            {
                tipLabel.text = label;
            }
        }

        private void HideTip()
        {
            if (undoTip != null)
            {
                undoTip.SetActive(false);
            }

            if (redoTip != null)
            {
                redoTip.SetActive(false);
            }
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
            Clear(tipsSwitch?.Button);
            Clear(helpButton);
            Clear(seatButton);
            Clear(hotSeatSwitch?.Button);
            Clear(handsOnTableSwitch?.Button);
            Clear(fullFeedbackSwitch?.Button);
            Clear(cardArtButton);
            Clear(profileButton);
            Clear(tableShapeButton);
            Clear(tableEdgeButton);
            Clear(tableToolsButton);
            Clear(cardEditorButton);
            Clear(deckEditorButton);
            Clear(saveGameButton);
            Clear(shopButton);
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
