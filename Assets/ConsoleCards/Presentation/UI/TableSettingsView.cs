using System;
using ConsoleCards.Presentation.UI.Toolbox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>The in-game Settings values and what each switch does. A null action hides its row.</summary>
    public sealed class TableSettingsModel
    {
        public bool HintsOn;
        public Action<bool> SetHints;
        public bool TipsOn;
        public Action<bool> SetTips;
        public bool FeedbackOn;
        public Action<bool> SetFeedback;
        public string SeatText = string.Empty;
        public Action NextSeat;
        public bool HotSeatOn;
        public Action<bool> SetHotSeat;
        public bool HandsOnTableOn;
        public Action<bool> SetHandsOnTable;
        public bool HandShown;
        public Action<bool> SetHandShown;
        /// <summary>BACK: to the Esc menu when opened from it; null closes Settings.</summary>
        public Action Back;
        public Action Close;

        // Board 6 (optional rows; a null action hides its row).
        public bool RulesCardOn;
        public Action<bool> SetRulesCard;
        public bool AskBeforeReset = true;
        public Action<bool> SetAskBeforeReset;
        /// <summary>Tips while placing: 0 first games, 1 always, 2 off.</summary>
        public int TipsMode;
        public Action<int> SetTipsMode;
        public string LightText = string.Empty;
        public Action<int> CycleLight;
        public string QualityText = string.Empty;
        public Action<int> CycleQuality;
        public bool ColourBlindOn;
        public Action<bool> SetColourBlind;
        public bool RoomSoundOn;
        public Action<bool> SetRoomSound;
        public bool HandFanOn;
        public Action<bool> SetHandFan;
        public string ProfileText = string.Empty;
    }

    /// <summary>
    /// MENU-1 (owner, 2026-10-30, design board 6 "Settings"): the in-game Settings panel in the middle of the dimmed
    /// table, with tabs GAME (Hints, Tips, Full feedback), TABLE (Seat, Hot-seat, Hands on table, Show hand) and
    /// CONTROLS (the keys). Changes apply at once. Light, colours, quality and sound stay on the main menu's SETTINGS.
    /// </summary>
    public sealed partial class TableSettingsView : ReusableUiView
    {
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] tabButtons = Array.Empty<Button>();
        [SerializeField] private Image[] tabFills = Array.Empty<Image>();
        [SerializeField] private Text[] tabLabels = Array.Empty<Text>();
        [SerializeField] private GameObject[] pages = Array.Empty<GameObject>();
        [SerializeField] private Color tabOn = new Color(0.949f, 0.463f, 0.180f, 1f);
        [SerializeField] private Color tabOff = new Color(0.133f, 0.145f, 0.153f, 1f);
        [SerializeField] private Color tabOnWords = new Color(0.102f, 0.071f, 0.024f, 1f);
        [SerializeField] private Color tabOffWords = new Color(0.788f, 0.816f, 0.824f, 1f);

        [Header("GAME")]
        [SerializeField] private ToolboxSwitch hintsSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch tipsSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch feedbackSwitch = new ToolboxSwitch();

        [Header("TABLE")]
        [SerializeField] private GameObject seatRow;
        [SerializeField] private Button seatButton;
        [SerializeField] private Text seatLabel;
        [SerializeField] private ToolboxSwitch hotSeatSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch handsOnTableSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch handShownSwitch = new ToolboxSwitch();
        [SerializeField] private GameObject tableEmptyNote;

        private TableSettingsModel model;
        private int page;
        private int ignoreKeysUntilFrame;

        public bool IsComplete =>
            backdropButton != null && backButton != null && closeButton != null && tabButtons.Length > 0
            && tabButtons.Length == pages.Length && tabFills.Length == tabButtons.Length
            && tabLabels.Length == tabButtons.Length && hintsSwitch.IsComplete && tipsSwitch.IsComplete
            && feedbackSwitch.IsComplete && seatRow != null && seatButton != null && seatLabel != null
            && hotSeatSwitch.IsComplete && handsOnTableSwitch.IsComplete && handShownSwitch.IsComplete
            && tableEmptyNote != null;

        public void ValidateReferences()
        {
            if (!IsComplete) throw new InvalidOperationException("TableSettingsView requires its authored tabs and rows.");
        }

        /// <summary>Shows the values; keeps the open tab unless <paramref name="firstTab"/>.</summary>
        public void Bind(TableSettingsModel settings, bool firstTab)
        {
            RequireAcquired();
            ValidateReferences();
            model = settings ?? throw new ArgumentNullException(nameof(settings));
            Clear(backdropButton);
            Clear(backButton);
            Clear(closeButton);
            backdropButton.onClick.AddListener(PressClose);
            closeButton.onClick.AddListener(PressClose);
            backButton.onClick.AddListener(PressBack);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                Clear(tabButtons[i]);
                tabButtons[i].onClick.AddListener(() => ShowPage(index));
            }

            BindSwitch(hintsSwitch, model.HintsOn, model.SetHints, FlipHints);
            BindSwitch(tipsSwitch, model.TipsOn, model.SetTips, FlipTips);
            BindSwitch(feedbackSwitch, model.FeedbackOn, model.SetFeedback, FlipFeedback);
            BindSwitch(hotSeatSwitch, model.HotSeatOn, model.SetHotSeat, FlipHotSeat);
            BindSwitch(handsOnTableSwitch, model.HandsOnTableOn, model.SetHandsOnTable, FlipHandsOnTable);
            BindSwitch(handShownSwitch, model.HandShown, model.SetHandShown, FlipHandShown);
            Clear(seatButton);
            seatRow.SetActive(model.NextSeat != null);
            seatLabel.text = model.SeatText ?? string.Empty;
            if (model.NextSeat != null) seatButton.onClick.AddListener(() => model?.NextSeat?.Invoke());
            tableEmptyNote.SetActive(model.NextSeat == null && model.SetHotSeat == null && model.SetHandsOnTable == null
                && model.SetHandShown == null);
            BindBoard(model);
            ShowPage(firstTab ? 0 : Mathf.Clamp(page, 0, pages.Length - 1));
            if (firstTab) ignoreKeysUntilFrame = Time.frameCount + 1;
        }

        public override void Unbind()
        {
            model = null;
            Clear(backdropButton);
            Clear(backButton);
            Clear(closeButton);
            for (int i = 0; i < tabButtons.Length; i++) Clear(tabButtons[i]);
            Clear(seatButton);
            if (hintsSwitch.IsComplete) Clear(hintsSwitch.Button);
            if (tipsSwitch.IsComplete) Clear(tipsSwitch.Button);
            if (feedbackSwitch.IsComplete) Clear(feedbackSwitch.Button);
            if (hotSeatSwitch.IsComplete) Clear(hotSeatSwitch.Button);
            if (handsOnTableSwitch.IsComplete) Clear(handsOnTableSwitch.Button);
            if (handShownSwitch.IsComplete) Clear(handShownSwitch.Button);
            UnbindBoard();
        }

        private void Update()
        {
            if (model == null || Keyboard.current == null || Time.frameCount <= ignoreKeysUntilFrame) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame) PressBack();
        }

        private void ShowPage(int index)
        {
            page = index;
            for (int i = 0; i < pages.Length; i++)
            {
                bool on = i == index;
                if (pages[i] != null) pages[i].SetActive(on);
                if (tabFills[i] != null) tabFills[i].color = on ? tabOn : tabOff;
                if (tabLabels[i] != null) tabLabels[i].color = on ? tabOnWords : tabOffWords;
                if (i < tabMarks.Length && tabMarks[i] != null) tabMarks[i].SetActive(on);
            }
        }

        private static void BindSwitch(ToolboxSwitch row, bool on, Action<bool> set, UnityEngine.Events.UnityAction flip)
        {
            Clear(row.Button);
            row.Row.SetActive(set != null);
            row.Show(on);
            if (set != null) row.Button.onClick.AddListener(flip);
        }

        private void FlipHints() => Flip(hintsSwitch, ref model.HintsOn, model.SetHints);
        private void FlipTips() => Flip(tipsSwitch, ref model.TipsOn, model.SetTips);
        private void FlipFeedback() => Flip(feedbackSwitch, ref model.FeedbackOn, model.SetFeedback);
        private void FlipHotSeat() => Flip(hotSeatSwitch, ref model.HotSeatOn, model.SetHotSeat);
        private void FlipHandsOnTable() => Flip(handsOnTableSwitch, ref model.HandsOnTableOn, model.SetHandsOnTable);
        private void FlipHandShown() => Flip(handShownSwitch, ref model.HandShown, model.SetHandShown);

        private static void Flip(ToolboxSwitch row, ref bool on, Action<bool> set)
        {
            if (set == null) return;
            on = !on;
            row.Show(on);
            set(on);
        }

        private void PressBack()
        {
            if (model == null) return;
            Action back = model.Back ?? model.Close;
            back?.Invoke();
        }

        private void PressClose()
        {
            model?.Close?.Invoke();
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }
    }
}
