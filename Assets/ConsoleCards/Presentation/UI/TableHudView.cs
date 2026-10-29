using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>What the HUD-B buttons do. Null members hide their buttons.</summary>
    public sealed class TableHudActions
    {
        public Action OpenMenu;
        public Action OpenSettings;
        public Action Undo;
        public Action Redo;
        public Action<TableToolKind> ChooseTool;
        public Action OpenRulebook;
        public Action OpenToolbox;
        public Action ToggleLog;
        public Action OpenHelp;
        public Action OpenShop;
        public Action ToggleHandStyle;
        /// <summary>True while the log is on screen at all (open or folded): then L is the log's own key.</summary>
        public Func<bool> IsLogShown;
        /// <summary>Board 7B: true while a menu, panel or popup is open; then B and Enter are not the HUD's.</summary>
        public Func<bool> KeysBlocked;
    }

    /// <summary>
    /// HUD-B (owner, 2026-10-29: "minimise the UI so we look at the table"; colour B approved): the table's HUD on the
    /// screen edges only.
    /// <list type="bullet">
    /// <item>Top left: the logo (opens the Table menu), ROUND n in orange, whose turn in white and the turn pips.</item>
    /// <item>Under it: the teal hint bar with a dark-teal tag (it turns orange at the end of a round).</item>
    /// <item>Right edge: the rail. Tools, Undo and Redo, Shop, Log, Help, Settings, Menu. The chosen tool is orange
    /// and a hovered one teal.</item>
    /// <item>Bottom left: the Rulebook and Toolbox buttons and the turn player's chip (keys, status).</item>
    /// <item>Bottom right: END TURN in orange, the turn's other actions as small chips above it.</item>
    /// </list>
    /// It replaces the look of the session bar, the Trap Floor turn strip and player card, the rules card, the hint
    /// strip and the tool bar's buttons; their menus and panels (Table menu, Settings, Toolbox panel, Draw panel, Log,
    /// Help) keep working and are opened from here. Layout and colours are on the prefab (Final UI/Notices/TableHud).
    /// </summary>
    public sealed class TableHudView : ReusableUiView
    {
        [Serializable]
        public sealed class RailTool
        {
            public TableToolKind kind;
            public Button button;
            public Image fill;
            public Image icon;
        }

        [Header("Status strip")]
        [SerializeField] private Button logoButton;
        [SerializeField] private GameObject roundGroup;
        [SerializeField] private Text roundWord;
        [SerializeField] private Text roundNumber;
        [SerializeField] private Text turnLabel;
        [SerializeField] private Image[] pips = new Image[0];
        [SerializeField] private Text gameLabel;
        [SerializeField] private float activePipScale = 1.45f;

        [Header("Hint bar")]
        [SerializeField] private GameObject hintBar;
        [SerializeField] private Image hintFill;
        [SerializeField] private Image hintTagFill;
        [SerializeField] private Text hintTag;
        [SerializeField] private Text hintText;
        [SerializeField] private Button hintGotIt;
        [SerializeField] private Button hintClose;
        [Tooltip("Board 7B (optional): the hint is one line, one step at a time; this shows which step (1/3).")]
        [SerializeField] private Text hintCount;
        [Tooltip("Board 7B (optional): a press on the hint shows its next step.")]
        [SerializeField] private Button hintNext;

        [Header("Rail")]
        [SerializeField] private RailTool[] tools = new RailTool[0];
        [SerializeField] private Button undoButton;
        [SerializeField] private Image undoIcon;
        [SerializeField] private Button redoButton;
        [SerializeField] private Image redoIcon;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button logButton;
        [SerializeField] private Button helpButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button menuButton;
        [Tooltip("Optional: FAN / ROW for the hand tray (the tool bar's button is hidden under this HUD).")]
        [SerializeField] private Button handStyleButton;

        [Header("Bottom left")]
        [SerializeField] private Button rulebookButton;
        [SerializeField] private Button toolboxButton;
        [SerializeField] private GameObject playerChip;
        [SerializeField] private Image playerDot;
        [SerializeField] private Text playerText;
        [Tooltip("Board 7B (optional): the chip's edge, in the player's colour.")]
        [SerializeField] private Graphic playerEdge;
        [Tooltip("Board 7B (optional): the player's state (OVERCHARGED +1) as a yellow tag.")]
        [SerializeField] private GameObject playerStatusTag;
        [SerializeField] private Text playerStatusText;

        [Header("Bottom right: the turn's actions")]
        [SerializeField] private RectTransform actionsRoot;
        [Tooltip("A small chip for each action that is not the main one (cloned at run time, kept).")]
        [SerializeField] private Button actionTemplate;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryLabel;
        [Tooltip("Actions whose label starts with this are the big main button.")]
        [SerializeField] private string primaryActionPrefix = "End";

        [Header("Colours (colour B)")]
        [SerializeField] private Color orange = new Color(0.949f, 0.463f, 0.180f, 1f);
        [SerializeField] private Color hintTeal = new Color(0.075f, 0.541f, 0.549f, 1f);
        [SerializeField] private Color hintTagTeal = new Color(0.039f, 0.208f, 0.216f, 1f);
        [SerializeField] private Color hintTagWords = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color roundEndTag = new Color(0.102f, 0.071f, 0.024f, 1f);
        [SerializeField] private Color railIdleFill = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color railActiveFill = new Color(0.949f, 0.463f, 0.180f, 1f);
        [SerializeField] private Color railIdleIcon = new Color(0.788f, 0.816f, 0.824f, 1f);
        [SerializeField] private Color railActiveIcon = new Color(0.102f, 0.071f, 0.024f, 1f);
        [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.35f;

        private readonly List<Button> actionChips = new List<Button>();
        private readonly List<Text> actionChipLabels = new List<Text>();
        private readonly List<PopupActionOption> secondaryActions = new List<PopupActionOption>();
        private readonly Dictionary<Image, Vector3> pipScales = new Dictionary<Image, Vector3>();
        private TableHudActions actions;
        private PopupActionOption primaryAction;
        private bool hasPrimary;
        private bool tipShown;
        private string statusHint = string.Empty;
        private bool roundEnded;
        private Action tipGotIt;
        private Action tipLater;
        private string tipKind = string.Empty;
        private string tipText = string.Empty;
        private bool[] toolEnabled = new bool[0];
        private bool toolsShown = true;

        public bool IsTipShown => tipShown;

        public void ValidateReferences()
        {
            if (logoButton == null || roundGroup == null || roundWord == null || roundNumber == null || turnLabel == null
                || gameLabel == null || hintBar == null || hintFill == null || hintTagFill == null || hintTag == null
                || hintText == null || hintGotIt == null || hintClose == null || tools == null || undoButton == null
                || redoButton == null || shopButton == null || logButton == null || helpButton == null
                || settingsButton == null || menuButton == null || rulebookButton == null || toolboxButton == null
                || playerChip == null || playerDot == null || playerText == null || actionsRoot == null
                || actionTemplate == null || primaryButton == null || primaryLabel == null)
            {
                throw new InvalidOperationException(
                    "TableHudView is missing an authored part. Run Console Cards > UI > Create Missing Table HUD (HUD-B).");
            }
        }

        public void Bind(TableHudActions hudActions, string gameTitle)
        {
            RequireAcquired();
            ValidateReferences();
            RemoveListeners();
            actions = hudActions ?? new TableHudActions();
            Wire(logoButton, actions.OpenMenu);
            Wire(menuButton, actions.OpenMenu);
            Wire(settingsButton, actions.OpenSettings);
            Wire(undoButton, actions.Undo);
            Wire(redoButton, actions.Redo);
            Wire(rulebookButton, actions.OpenRulebook);
            Wire(toolboxButton, actions.OpenToolbox);
            Wire(logButton, actions.ToggleLog);
            Wire(helpButton, actions.OpenHelp);
            if (handStyleButton != null)
            {
                Wire(handStyleButton, actions.ToggleHandStyle);
                handStyleButton.gameObject.SetActive(false); // shown by SetHandStyleVisible when the table has a hand tray
            }
            // The Shop comes and goes with its rule (doc 18 §15.4): SetShopVisible shows it, the action is read on press.
            shopButton.onClick.AddListener(() => actions?.OpenShop?.Invoke());
            shopButton.gameObject.SetActive(actions.OpenShop != null);
            for (int i = 0; i < tools.Length; i++)
            {
                RailTool tool = tools[i];
                if (tool?.button == null) continue;
                TableToolKind kind = tool.kind;
                tool.button.gameObject.SetActive(actions.ChooseTool != null && toolsShown);
                tool.button.onClick.AddListener(() => actions?.ChooseTool?.Invoke(kind));
            }

            if (toolEnabled.Length != tools.Length) toolEnabled = new bool[tools.Length];
            for (int i = 0; i < toolEnabled.Length; i++) toolEnabled[i] = true;

            hintGotIt.onClick.AddListener(PressGotIt);
            hintClose.onClick.AddListener(PressLater);
            if (hintNext != null) hintNext.onClick.AddListener(NextHintStep);
            primaryButton.onClick.AddListener(PressPrimary);
            actionTemplate.gameObject.SetActive(false);
            gameLabel.text = gameTitle ?? string.Empty;
            ClearStatus();
            SetActiveTool(TableToolKind.Grab);
        }

        public override void Unbind()
        {
            ForgetClosedHint();
            RemoveListeners();
            actions = null;
            tipGotIt = null;
            tipLater = null;
            tipShown = false;
            hasPrimary = false;
        }

        // ---------- the status ----------

        /// <summary>The game's turn: round, whose turn, the pips, the hint line, the turn player's chip, the actions.</summary>
        public void ShowStatus(
            TrapFloorStatusModel status,
            FloorfallStatusModel floorfall,
            IReadOnlyList<PopupActionOption> turnActions)
        {
            RequireAcquired();
            SplitRound(status.Round, out string word, out string number);
            roundGroup.SetActive(word.Length > 0 || number.Length > 0);
            roundWord.text = word;
            roundNumber.text = number;
            roundEnded = status.Phase.IndexOf("FLOOR TURN", StringComparison.OrdinalIgnoreCase) >= 0;
            turnLabel.text = roundEnded ? "END OF ROUND · ANYONE ROLLS" : status.Phase;
            turnLabel.gameObject.SetActive(turnLabel.text.Length > 0);
            gameLabel.gameObject.SetActive(false);
            ShowPips(status.Players);
            ShowPlayer(status.Card, status.PlayerColor);

            string hint = status.ActionHelp;
            if (floorfall.Visible)
            {
                string dice = string.IsNullOrEmpty(floorfall.Dice) ? string.Empty : floorfall.Dice;
                string target = string.IsNullOrEmpty(floorfall.Target) ? floorfall.Coordinate : floorfall.Target;
                string roll = (dice + " " + target).Trim();
                if (roll.Length > 0) hint = hint.Length > 0 ? $"{roll} · {hint}" : roll;
            }

            statusHint = hint ?? string.Empty;
            // A closed hint comes back on the next turn or round, even with the same words.
            if (dismissedHint.Length > 0 && (turnLabel.text != dismissedTurn || roundNumber.text != dismissedRound))
            {
                dismissedHint = string.Empty;
            }

            ShowActions(turnActions);
            RefreshHint();
        }

        /// <summary>The objective view (Keys, collapse, victory) from the older Trap Floor flow.</summary>
        public void ShowObjective(string keyProgress, string collapseStatus, bool isWon, IReadOnlyList<PopupActionOption> turnActions)
        {
            RequireAcquired();
            roundGroup.SetActive(false);
            roundEnded = false;
            turnLabel.text = isWon ? "VICTORY" : (keyProgress ?? string.Empty);
            turnLabel.gameObject.SetActive(turnLabel.text.Length > 0);
            gameLabel.gameObject.SetActive(false);
            ShowPips(null);
            ShowPlayer(null, null);
            statusHint = collapseStatus ?? string.Empty;
            ShowActions(turnActions);
            RefreshHint();
        }

        /// <summary>No game turn (an Empty Table): the strip shows the table's name, no turn and no actions.</summary>
        public void ClearStatus()
        {
            roundGroup.SetActive(false);
            roundEnded = false;
            turnLabel.gameObject.SetActive(false);
            gameLabel.gameObject.SetActive(gameLabel.text.Length > 0);
            ShowPips(null);
            ShowPlayer(null, null);
            statusHint = string.Empty;
            ForgetClosedHint();
            ShowActions(null);
            RefreshHint();
        }

        public void SetGameTitle(string title)
        {
            gameLabel.text = title ?? string.Empty;
            if (!roundGroup.activeSelf && !turnLabel.gameObject.activeSelf)
            {
                gameLabel.gameObject.SetActive(gameLabel.text.Length > 0);
            }
        }

        // ---------- tips (doc 23 §10) ----------

        /// <summary>A tip in the hint bar, with GOT IT (never again) and × (later). It shows over the turn's hint.</summary>
        public void ShowTip(string kind, string text, Action gotIt, Action later)
        {
            tipKind = string.IsNullOrEmpty(kind) ? "TIP" : kind.ToUpperInvariant();
            tipText = FitText.Concise(text, 110);
            tipGotIt = gotIt;
            tipLater = later;
            tipShown = true;
            RefreshHint();
        }

        public void HideTip()
        {
            tipShown = false;
            tipGotIt = null;
            tipLater = null;
            RefreshHint();
        }

        /// <summary>
        /// Hides the hint bar while something else uses its place (the Toolbox panel, which has its own guidance and
        /// sits under the HUD's layer); the hint and any tip come back when it is shown again.
        /// </summary>
        public void SetHintHidden(bool hidden)
        {
            if (hintHidden == hidden) return;
            hintHidden = hidden;
            RefreshHint();
        }

        private bool hintHidden;

        private void RefreshHint()
        {
            string tag;
            string text;
            if (tipShown)
            {
                tag = tipKind;
                text = tipText;
            }
            else if (hintCount != null)
            {
                // Board 7B: one step of the turn's hint at a time, its tag naming the step.
                SplitHintSteps();
                int step = hintSteps.Count > 0 ? Mathf.Clamp(hintStep, 0, hintSteps.Count - 1) : 0;
                text = hintSteps.Count > 0 ? hintSteps[step] : string.Empty;
                tag = roundEnded ? "ROUND END" : StepTag(text);
            }
            else
            {
                tag = roundEnded ? "ROUND END" : "HINT";
                text = statusHint;
            }

            // Owner, 2026-10-30: "you can't close the hints": × hides the turn's hint until the next one.
            bool dismissed = !tipShown && dismissedHint.Length > 0;
            bool shown = text.Length > 0 && !hintHidden && !dismissed;
            if (hintBar.activeSelf != shown) hintBar.SetActive(shown);
            if (!shown) return;
            hintTag.text = tag;
            hintText.text = text;
            hintFill.color = roundEnded && !tipShown ? orange : hintTeal;
            hintTagFill.color = roundEnded && !tipShown ? roundEndTag : hintTagTeal;
            hintTag.color = roundEnded && !tipShown ? orange : hintTagWords;
            hintGotIt.gameObject.SetActive(tipShown && tipGotIt != null);
            hintClose.gameObject.SetActive(true);
            if (hintCount != null)
            {
                bool counted = !tipShown && hintSteps.Count > 1;
                if (hintCount.gameObject.activeSelf != counted) hintCount.gameObject.SetActive(counted);
                if (counted) hintCount.text = $"{Mathf.Clamp(hintStep, 0, hintSteps.Count - 1) + 1}/{hintSteps.Count}";
            }

            if (hintNext != null) hintNext.interactable = !tipShown && hintSteps.Count > 1;
        }

        // ---------- the one-line hint's steps (board 7B) ----------

        private readonly List<string> hintSteps = new List<string>();
        private string splitHint;
        private int hintStep;

        // The turn's hint, one step per line, each made short (brackets dropped, one sentence, ~64 letters).
        private void SplitHintSteps()
        {
            if (string.Equals(splitHint, statusHint, StringComparison.Ordinal)) return;
            splitHint = statusHint;
            hintStep = 0;
            hintSteps.Clear();
            if (string.IsNullOrEmpty(statusHint)) return;
            string[] lines = statusHint.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = FitText.Concise(WithoutBrackets(lines[i]), 64).Trim().TrimEnd('.');
                if (line.Length > 0) hintSteps.Add(line);
            }
        }

        // "Draw up to 10 if needed (hover the deck)" → "Draw up to 10 if needed".
        private static string WithoutBrackets(string line)
        {
            int open = line.IndexOf('(');
            while (open >= 0)
            {
                int close = line.IndexOf(')', open);
                if (close < 0) break;
                line = line.Remove(open, close - open + 1);
                open = line.IndexOf('(');
            }

            return line.Replace("  ", " ").Replace(" ;", ";").Replace(" ,", ",").Replace(" .", ".").Trim();
        }

        private void NextHintStep()
        {
            if (tipShown || hintSteps.Count < 2) return;
            hintStep = (hintStep + 1) % hintSteps.Count;
            RefreshHint();
        }

        // DRAW PHASE, MOVE PHASE…: named by the step's first word.
        private static string StepTag(string step)
        {
            if (string.IsNullOrEmpty(step)) return "HINT";
            int end = 0;
            while (end < step.Length && char.IsLetter(step[end])) end++;
            string verb = end > 0 ? step.Substring(0, end).ToLowerInvariant() : string.Empty;
            switch (verb)
            {
                case "draw": return "DRAW PHASE";
                case "move": return "MOVE PHASE";
                case "roll": return "ROLL PHASE";
                case "buy": return "SHOP PHASE";
                case "play": return "PLAY PHASE";
                case "deal": return "DEAL PHASE";
                case "take":
                case "claim":
                case "collect": return "PRIZE";
                case "end": return "END TURN";
                default: return "YOUR TURN";
            }
        }

        private void PressGotIt()
        {
            Action gotIt = tipGotIt;
            HideTip();
            gotIt?.Invoke();
        }

        private string dismissedHint = string.Empty;

        private void ForgetClosedHint()
        {
            dismissedHint = string.Empty;
            dismissedTurn = string.Empty;
            dismissedRound = string.Empty;
        }
        private string dismissedTurn = string.Empty;
        private string dismissedRound = string.Empty;

        private void PressLater()
        {
            if (!tipShown)
            {
                dismissedHint = statusHint;
                dismissedTurn = turnLabel.text;
                dismissedRound = roundNumber.text;
                RefreshHint();
                return;
            }

            Action later = tipLater;
            HideTip();
            later?.Invoke();
        }

        // ---------- rail ----------

        public void SetActiveTool(TableToolKind kind)
        {
            for (int i = 0; i < tools.Length; i++)
            {
                RailTool tool = tools[i];
                if (tool == null) continue;
                bool active = tool.kind == kind;
                if (tool.fill != null) tool.fill.color = active ? railActiveFill : railIdleFill;
                if (tool.icon != null)
                {
                    Color c = active ? railActiveIcon : railIdleIcon;
                    bool enabled = i >= toolEnabled.Length || toolEnabled[i];
                    tool.icon.color = new Color(c.r, c.g, c.b, enabled ? c.a : disabledAlpha);
                }
            }
        }

        public void SetHandStyleVisible(bool visible)
        {
            if (handStyleButton != null && handStyleButton.gameObject.activeSelf != visible)
            {
                handStyleButton.gameObject.SetActive(visible);
            }
        }

        /// <summary>The rail's tools show only when the table has its tool bar behind them.</summary>
        public void SetToolsVisible(bool visible)
        {
            toolsShown = visible;
            for (int i = 0; i < tools.Length; i++)
            {
                if (tools[i]?.button != null) tools[i].button.gameObject.SetActive(visible);
            }
        }

        public void SetToolEnabled(TableToolKind kind, bool enabled)
        {
            for (int i = 0; i < tools.Length; i++)
            {
                RailTool tool = tools[i];
                if (tool?.button == null || tool.kind != kind) continue;
                if (i < toolEnabled.Length) toolEnabled[i] = enabled;
                tool.button.interactable = enabled;
                if (tool.icon != null)
                {
                    bool active = tool.fill != null && tool.fill.color == railActiveFill;
                    Color c = active ? railActiveIcon : railIdleIcon;
                    tool.icon.color = new Color(c.r, c.g, c.b, enabled ? c.a : disabledAlpha);
                }
            }
        }

        public void SetUndoEnabled(bool enabled) => SetIconButton(undoButton, undoIcon, enabled);

        public void SetRedoEnabled(bool enabled) => SetIconButton(redoButton, redoIcon, enabled);

        public void SetShopVisible(bool visible)
        {
            if (shopButton.gameObject.activeSelf != visible) shopButton.gameObject.SetActive(visible);
        }

        public void SetRulebookAvailable(bool available)
        {
            rulebookButton.interactable = available;
        }

        // ---------- the turn's actions ----------

        private void ShowActions(IReadOnlyList<PopupActionOption> turnActions)
        {
            hasPrimary = false;
            secondaryActions.Clear();
            if (turnActions != null)
            {
                for (int i = 0; i < turnActions.Count; i++)
                {
                    PopupActionOption action = turnActions[i];
                    if (!hasPrimary && IsPrimary(action))
                    {
                        primaryAction = action;
                        hasPrimary = true;
                    }
                    else
                    {
                        secondaryActions.Add(action);
                    }
                }
            }

            primaryButton.gameObject.SetActive(hasPrimary);
            if (hasPrimary)
            {
                primaryLabel.text = primaryAction.Label.ToUpperInvariant();
                primaryButton.interactable = primaryAction.Enabled;
            }

            while (actionChips.Count < secondaryActions.Count)
            {
                Button chip = Instantiate(actionTemplate, actionsRoot, false);
                chip.name = "Action" + actionChips.Count;
                int index = actionChips.Count;
                chip.onClick.AddListener(() => PressSecondary(index));
                actionChips.Add(chip);
                actionChipLabels.Add(chip.GetComponentInChildren<Text>(true));
            }

            for (int i = 0; i < actionChips.Count; i++)
            {
                bool shown = i < secondaryActions.Count;
                if (actionChips[i].gameObject.activeSelf != shown) actionChips[i].gameObject.SetActive(shown);
                if (!shown) continue;
                if (actionChipLabels[i] != null) actionChipLabels[i].text = secondaryActions[i].Label.ToUpperInvariant();
                actionChips[i].interactable = secondaryActions[i].Enabled;
            }

            actionsRoot.gameObject.SetActive(secondaryActions.Count > 0);
        }

        private bool IsPrimary(PopupActionOption action) =>
            !string.IsNullOrEmpty(primaryActionPrefix)
            && action.Label.StartsWith(primaryActionPrefix, StringComparison.OrdinalIgnoreCase);

        private void PressPrimary()
        {
            if (hasPrimary && primaryAction.Enabled) primaryAction.Selected.Invoke();
        }

        private void PressSecondary(int index)
        {
            if (index < 0 || index >= secondaryActions.Count) return;
            PopupActionOption action = secondaryActions[index];
            if (action.Enabled) action.Selected.Invoke();
        }

        // ---------- players ----------

        private void ShowPips(IReadOnlyList<TrapFloorHudPlayer> players)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                Image pip = pips[i];
                if (pip == null) continue;
                bool shown = players != null && i < players.Count;
                if (pip.gameObject.activeSelf != shown) pip.gameObject.SetActive(shown);
                if (!shown) continue;
                if (!pipScales.TryGetValue(pip, out Vector3 normal))
                {
                    normal = pip.rectTransform.localScale;
                    pipScales[pip] = normal;
                }

                TrapFloorHudPlayer player = players[i];
                Color colour = player.Color;
                pip.color = player.Active ? colour : new Color(colour.r, colour.g, colour.b, 0.45f);
                pip.rectTransform.localScale = player.Active ? normal * activePipScale : normal;
                // Board 7B: the turn player's pip has a ring in its colour (a "Ring" behind it, when the prefab has one).
                Transform ring = pip.transform.Find("Ring");
                if (ring != null)
                {
                    if (ring.gameObject.activeSelf != player.Active) ring.gameObject.SetActive(player.Active);
                    Image ringImage = ring.GetComponent<Image>();
                    if (ringImage != null) ringImage.color = colour;
                }
            }
        }

        private void ShowPlayer(TrapFloorPlayerCardModel card, Color? colour)
        {
            bool shown = card != null;
            if (playerChip.activeSelf != shown) playerChip.SetActive(shown);
            if (!shown) return;
            playerDot.color = colour ?? Color.white;
            if (playerEdge != null) playerEdge.color = colour ?? Color.white;
            if (playerStatusTag != null)
            {
                // Board 7B: NAME · Keys 1/3, and the state as its own yellow tag.
                string line = $"<b>{card.Title}</b>";
                if (!string.IsNullOrEmpty(card.Keys))
                {
                    line += $"  <color=#9BA4A7>· Keys</color>  <b>{card.Keys.Replace(" / ", "/")}</b>";
                }

                playerText.text = line;
                bool status = !string.IsNullOrEmpty(card.Status);
                if (playerStatusTag.activeSelf != status) playerStatusTag.SetActive(status);
                if (status && playerStatusText != null) playerStatusText.text = card.Status.ToUpperInvariant();
                return;
            }

            string text = card.Title;
            if (!string.IsNullOrEmpty(card.Avatar)) text += $"  ·  {card.Avatar}";
            if (!string.IsNullOrEmpty(card.Keys)) text += $"  ·  Keys {card.Keys}";
            if (!string.IsNullOrEmpty(card.Status))
            {
                text += $"  ·  <b><color=#{ColorUtility.ToHtmlStringRGB(orange)}>{card.Status.ToUpperInvariant()}</color></b>";
            }

            playerText.text = text;
        }

        // L opens the log while it is hidden (an open log handles L itself). LateUpdate, so the log shown here does
        // not also read the same key press in its own Update this frame.
        private void LateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (actions == null || keyboard == null) return;
            bool l = keyboard.lKey.wasPressedThisFrame;
            bool b = keyboard.bKey.wasPressedThisFrame;
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if (!l && !b && !enter) return;
            if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) return;
            EventSystem current = EventSystem.current;
            GameObject selected = current != null ? current.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponent<InputField>() != null) return;
            if (l && actions.ToggleLog != null && (actions.IsLogShown == null || !actions.IsLogShown())) actions.ToggleLog();
            // Board 7B: B opens the Toolbox, Enter is END TURN (not while a menu, panel or popup is open).
            if ((!b && !enter) || (actions.KeysBlocked != null && actions.KeysBlocked())) return;
            if (b && actions.OpenToolbox != null && toolboxButton.gameObject.activeInHierarchy) actions.OpenToolbox();
            if (enter && primaryButton.gameObject.activeInHierarchy && primaryButton.interactable) PressPrimary();
        }

        // ---------- helpers ----------

        // "ROUND 3" → "ROUND" and "3"; anything else stays whole in the word.
        private static void SplitRound(string round, out string word, out string number)
        {
            word = string.Empty;
            number = string.Empty;
            if (string.IsNullOrWhiteSpace(round)) return;
            string text = round.Trim();
            int colon = text.IndexOf(':');
            if (colon > 0)
            {
                word = text.Substring(0, colon).Trim().ToUpperInvariant();
                number = text.Substring(colon + 1).Trim();
                return;
            }

            int space = text.LastIndexOf(' ');
            if (space > 0 && int.TryParse(text.Substring(space + 1), out _))
            {
                word = text.Substring(0, space).ToUpperInvariant();
                number = text.Substring(space + 1);
                return;
            }

            word = text.ToUpperInvariant();
        }

        private void SetIconButton(Button button, Image icon, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            if (icon != null)
            {
                Color c = icon.color;
                icon.color = new Color(c.r, c.g, c.b, enabled ? 1f : disabledAlpha);
            }
        }

        private static void Wire(Button button, Action action)
        {
            if (button == null) return;
            button.gameObject.SetActive(action != null);
            if (action != null) button.onClick.AddListener(() => action());
        }

        private void RemoveListeners()
        {
            Button[] buttons =
            {
                logoButton, menuButton, settingsButton, undoButton, redoButton, rulebookButton, toolboxButton,
                logButton, helpButton, shopButton, hintGotIt, hintClose, primaryButton, handStyleButton, hintNext,
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null) buttons[i].onClick.RemoveAllListeners();
            }

            for (int i = 0; tools != null && i < tools.Length; i++)
            {
                if (tools[i]?.button != null) tools[i].button.onClick.RemoveAllListeners();
            }
        }
    }
}
