using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>What the Esc menu's rows do. A null action hides its row.</summary>
    public sealed class TablePauseActions
    {
        public Action Resume;
        public Action OpenRules;
        public Action OpenHowToPlay;
        public Action SaveAsGame;
        public Action OpenSettings;
        public Action NewTable;
        public Action ResetTable;
        public Action ClearTable;
        public Action MainMenu;
        public Action ExitGame;
        /// <summary>Settings › Ask before Reset: Reset and Clear need a second press.</summary>
        public bool AskBeforeReset = true;
    }

    /// <summary>
    /// MENU-1 (owner, 2026-10-30, design board 5 "In game · Esc menu"; doc 38): the Esc menu replaces the Table menu.
    /// A small card in the middle of the dimmed table: RESUME, RULES, HOW TO PLAY, SAVE TABLE AS A GAME, SETTINGS, the
    /// table rows (NEW TABLE, RESET, CLEAR) and MAIN MENU / EXIT GAME. Rows that undo work (Reset, Clear, Main menu, Exit)
    /// ask for a second press. Every size and colour is on the prefab (Final UI/Notices).
    /// </summary>
    public sealed class TablePauseMenuView : ReusableUiView
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text subtitleLabel;
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button rulesButton;
        [SerializeField] private Button howToPlayButton;
        [SerializeField] private Button saveGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject tableSection;
        [SerializeField] private Button newTableButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button exitButton;
        [Tooltip("How long a row that undoes work waits for its second press.")]
        [SerializeField] private float confirmSeconds = 3f;
        [SerializeField] private string confirmWords = "PRESS AGAIN";

        private TablePauseActions actions;
        private Button armed;
        private string armedText;
        private float armedUntil;
        private int ignoreKeysUntilFrame;

        public bool IsComplete =>
            titleLabel != null && subtitleLabel != null && backdropButton != null && resumeButton != null
            && rulesButton != null && howToPlayButton != null && saveGameButton != null && settingsButton != null
            && tableSection != null && newTableButton != null && resetButton != null && clearButton != null
            && mainMenuButton != null && exitButton != null;

        public void ValidateReferences()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("TablePauseMenuView requires its authored rows and labels.");
            }
        }

        public void Bind(string title, string subtitle, TablePauseActions pauseActions)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            actions = pauseActions ?? throw new ArgumentNullException(nameof(pauseActions));
            titleLabel.text = string.IsNullOrEmpty(title) ? "PAUSED" : title;
            subtitleLabel.text = subtitle ?? string.Empty;
            subtitleLabel.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            Wire(backdropButton, actions.Resume, false);
            Wire(resumeButton, actions.Resume, false);
            Wire(rulesButton, actions.OpenRules, false);
            Wire(howToPlayButton, actions.OpenHowToPlay, false);
            Wire(saveGameButton, actions.SaveAsGame, false);
            Wire(settingsButton, actions.OpenSettings, false);
            Wire(newTableButton, actions.NewTable, false);
            Wire(resetButton, actions.ResetTable, actions.AskBeforeReset);
            Wire(clearButton, actions.ClearTable, actions.AskBeforeReset);
            Wire(mainMenuButton, actions.MainMenu, true);
            Wire(exitButton, actions.ExitGame, true);
            tableSection.SetActive(actions.NewTable != null || actions.ResetTable != null || actions.ClearTable != null);
            backdropButton.gameObject.SetActive(true);
            ignoreKeysUntilFrame = Time.frameCount + 1; // the Esc that opened it does not close it
        }

        public override void Unbind()
        {
            Disarm();
            actions = null;
            Clear(backdropButton);
            Clear(resumeButton);
            Clear(rulesButton);
            Clear(howToPlayButton);
            Clear(saveGameButton);
            Clear(settingsButton);
            Clear(newTableButton);
            Clear(resetButton);
            Clear(clearButton);
            Clear(mainMenuButton);
            Clear(exitButton);
        }

        public override void Hide()
        {
            Disarm();
            base.Hide();
        }

        private void Update()
        {
            if (armed != null && Time.unscaledTime > armedUntil) Disarm();
            if (actions == null || Keyboard.current == null || Time.frameCount <= ignoreKeysUntilFrame) return;
            // Esc resumes; while the menu is open R and H are its RULES and HOW TO PLAY (board 5's key chips).
            if (Keyboard.current.escapeKey.wasPressedThisFrame) actions.Resume?.Invoke();
            else if (Keyboard.current.rKey.wasPressedThisFrame && actions.OpenRules != null) actions.OpenRules();
            else if (Keyboard.current.hKey.wasPressedThisFrame && actions.OpenHowToPlay != null) actions.OpenHowToPlay();
        }

        private void Wire(Button button, Action action, bool askTwice)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (button != backdropButton) button.gameObject.SetActive(action != null);
            if (action == null) return;
            if (askTwice) button.onClick.AddListener(() => PressTwice(button, action));
            else button.onClick.AddListener(() => action());
        }

        // The first press arms the row ("PRESS AGAIN"); a second press within confirmSeconds runs it.
        private void PressTwice(Button button, Action action)
        {
            if (armed == button)
            {
                Disarm();
                action();
                return;
            }

            Disarm();
            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                action();
                return;
            }

            armed = button;
            armedText = label.text;
            armedUntil = Time.unscaledTime + confirmSeconds;
            label.text = confirmWords;
        }

        private void Disarm()
        {
            if (armed == null) return;
            Text label = armed.GetComponentInChildren<Text>(true);
            if (label != null) label.text = armedText;
            armed = null;
            armedText = null;
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }
    }
}
