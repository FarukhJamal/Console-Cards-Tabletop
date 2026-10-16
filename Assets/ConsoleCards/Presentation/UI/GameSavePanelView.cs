using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>What the "Save table as a game" panel shows (doc 29).</summary>
    public sealed class GameSaveModel
    {
        public string Title = "SAVE TABLE AS A GAME";
        public string Summary = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string RulesText = string.Empty;
        public int MinimumPlayers = 1;
        public int MaximumPlayers = 4;
        public bool CanSaveOver;
        public string SaveOverText = "Save over";
        public string Status = string.Empty;

        // The steps (L2-6b): 0 Deck, 1 Cards, 2 Save. The Deck and Cards steps show a title, a text, an optional
        // < choice > line and up to two action buttons (empty text hides one); the Save step shows the details.
        public const int DeckStep = 0;
        public const int CardsStep = 1;
        public const int SaveStep = 2;
        public int Step = SaveStep;
        public string StepTitle = string.Empty;
        public string StepText = string.Empty;
        public bool ShowPicker;
        public string PickText = string.Empty;
        public string ActionAText = string.Empty;
        public string ActionBText = string.Empty;
    }

    /// <summary>The words typed in the save panel, handed back with every action so nothing typed is lost.</summary>
    public readonly struct GameSaveTyped
    {
        public GameSaveTyped(string name, string description, string rulesText)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            RulesText = rulesText ?? string.Empty;
        }

        public string Name { get; }
        public string Description { get; }
        public string RulesText { get; }
    }

    /// <summary>The save panel's actions, supplied by the game.</summary>
    public sealed class GameSaveActions
    {
        public Action<bool, int, GameSaveTyped> ChangePlayers;
        public Action<GameSaveTyped> SaveAsNew;
        public Action<GameSaveTyped> SaveOver;
        public Action Close;
        public Action<int, GameSaveTyped> GoToStep;
        public Action<int, GameSaveTyped> CyclePick;
        public Action<GameSaveTyped> ActionA;
        public Action<GameSaveTyped> ActionB;
    }

    /// <summary>
    /// Save table as a game (doc 29): the game's name, players (fewest and most), description and written rules
    /// (a line starting with "#" is a heading), with what of the table will be saved. Save as a new game, or over
    /// the game this table was started from. An authored Final UI prefab.
    /// In steps (L2-6b, added to the prefab by Create Missing Gameplay UI Prefabs): 1 Deck, 2 Cards, 3 Save, with
    /// Back and Next. A prefab without the steps shows the Save step only.
    /// </summary>
    public sealed class GameSavePanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text summaryLabel;
        [SerializeField] private InputField nameInput;
        [SerializeField] private InputField descriptionInput;
        [SerializeField] private InputField rulesInput;
        [SerializeField] private Button fewerMinimumButton;
        [SerializeField] private Button moreMinimumButton;
        [SerializeField] private Text minimumLabel;
        [SerializeField] private Button fewerMaximumButton;
        [SerializeField] private Button moreMaximumButton;
        [SerializeField] private Text maximumLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button saveOverButton;
        [SerializeField] private Text saveOverLabel;
        [SerializeField] private Button saveAsNewButton;

        [Header("Steps (L2-6b; added by Create Missing Gameplay UI Prefabs)")]
        [SerializeField] private Button[] stepButtons;
        [SerializeField] private Text[] stepLabels;
        [SerializeField] private GameObject stepSection;
        [SerializeField] private Text stepTitleLabel;
        [SerializeField] private Text stepTextLabel;
        [SerializeField] private GameObject pickerRow;
        [SerializeField] private Button previousPickButton;
        [SerializeField] private Text pickLabel;
        [SerializeField] private Button nextPickButton;
        [SerializeField] private Button actionAButton;
        [SerializeField] private Text actionALabel;
        [SerializeField] private Button actionBButton;
        [SerializeField] private Text actionBLabel;
        [SerializeField] private GameObject detailsSection;
        [SerializeField] private Button backButton;
        [SerializeField] private Button nextButton;

        private GameSaveActions actions;
        private int step = GameSaveModel.SaveStep;

        private static readonly string[] StepNames = { "1 · Deck", "2 · Cards", "3 · Save" };

        /// <summary>True when the prefab has the steps (Deck, Cards, Save).</summary>
        public bool HasSteps =>
            stepButtons != null && stepButtons.Length == 3 && stepSection != null && stepTitleLabel != null
            && stepTextLabel != null && pickerRow != null && previousPickButton != null && pickLabel != null
            && nextPickButton != null && actionAButton != null && actionALabel != null && actionBButton != null
            && actionBLabel != null && detailsSection != null && backButton != null && nextButton != null;

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || closeButton == null || titleLabel == null || summaryLabel == null
                || nameInput == null || descriptionInput == null || rulesInput == null || fewerMinimumButton == null
                || moreMinimumButton == null || minimumLabel == null || fewerMaximumButton == null
                || moreMaximumButton == null || maximumLabel == null || statusLabel == null || cancelButton == null
                || saveOverButton == null || saveOverLabel == null || saveAsNewButton == null)
            {
                throw new InvalidOperationException("GameSavePanelView requires its authored references.");
            }
        }

        public void Bind(GameSaveModel model, GameSaveActions panelActions)
        {
            RequireAcquired();
            ValidateReferences();
            if (model == null) throw new ArgumentNullException(nameof(model));
            actions = panelActions ?? throw new ArgumentNullException(nameof(panelActions));
            RemoveListeners();

            titleLabel.text = model.Title;
            summaryLabel.text = model.Summary;
            nameInput.text = model.Name;
            descriptionInput.text = model.Description;
            rulesInput.text = model.RulesText;
            minimumLabel.text = model.MinimumPlayers.ToString();
            maximumLabel.text = model.MaximumPlayers.ToString();
            saveOverButton.gameObject.SetActive(model.CanSaveOver);
            saveOverLabel.text = model.SaveOverText;
            statusLabel.text = model.Status;
            BindSteps(model);

            dismissOverlayButton.onClick.AddListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
            cancelButton.onClick.AddListener(HandleClose);
            fewerMinimumButton.onClick.AddListener(HandleFewerMinimum);
            moreMinimumButton.onClick.AddListener(HandleMoreMinimum);
            fewerMaximumButton.onClick.AddListener(HandleFewerMaximum);
            moreMaximumButton.onClick.AddListener(HandleMoreMaximum);
            saveOverButton.onClick.AddListener(HandleSaveOver);
            saveAsNewButton.onClick.AddListener(HandleSaveAsNew);
            if (!HasSteps) return;
            for (int i = 0; i < stepButtons.Length; i++)
            {
                int target = i;
                stepButtons[i].onClick.AddListener(() => actions?.GoToStep?.Invoke(target, Typed()));
            }

            backButton.onClick.AddListener(() => actions?.GoToStep?.Invoke(step - 1, Typed()));
            nextButton.onClick.AddListener(() => actions?.GoToStep?.Invoke(step + 1, Typed()));
            previousPickButton.onClick.AddListener(() => actions?.CyclePick?.Invoke(-1, Typed()));
            nextPickButton.onClick.AddListener(() => actions?.CyclePick?.Invoke(1, Typed()));
            actionAButton.onClick.AddListener(() => actions?.ActionA?.Invoke(Typed()));
            actionBButton.onClick.AddListener(() => actions?.ActionB?.Invoke(Typed()));
        }

        // The Deck and Cards steps show the step section; the Save step shows the details and the save buttons.
        private void BindSteps(GameSaveModel model)
        {
            if (!HasSteps)
            {
                if (model.Step != GameSaveModel.SaveStep)
                {
                    statusLabel.text = "For the Deck and Cards steps, run Console Cards > UI > Create Missing Gameplay UI Prefabs.";
                }

                return;
            }

            step = Mathf.Clamp(model.Step, GameSaveModel.DeckStep, GameSaveModel.SaveStep);
            bool saving = step == GameSaveModel.SaveStep;
            for (int i = 0; i < stepButtons.Length; i++)
            {
                // The current step reads as "you are here": its button does not press.
                stepButtons[i].interactable = i != step;
                if (stepLabels != null && i < stepLabels.Length && stepLabels[i] != null) stepLabels[i].text = StepNames[i];
            }

            stepSection.SetActive(!saving);
            detailsSection.SetActive(saving);
            stepTitleLabel.text = model.StepTitle;
            stepTextLabel.text = model.StepText;
            pickerRow.SetActive(!saving && model.ShowPicker);
            pickLabel.text = model.PickText;
            actionAButton.gameObject.SetActive(!saving && !string.IsNullOrEmpty(model.ActionAText));
            actionALabel.text = model.ActionAText;
            actionBButton.gameObject.SetActive(!saving && !string.IsNullOrEmpty(model.ActionBText));
            actionBLabel.text = model.ActionBText;
            backButton.gameObject.SetActive(step > GameSaveModel.DeckStep);
            nextButton.gameObject.SetActive(!saving);
            saveAsNewButton.gameObject.SetActive(saving);
            saveOverButton.gameObject.SetActive(saving && model.CanSaveOver);
        }

        public override void Unbind()
        {
            RemoveListeners();
            actions = null;
        }

        private GameSaveTyped Typed() => new GameSaveTyped(nameInput.text, descriptionInput.text, rulesInput.text);

        private void RemoveListeners()
        {
            Clear(dismissOverlayButton);
            Clear(closeButton);
            Clear(cancelButton);
            Clear(fewerMinimumButton);
            Clear(moreMinimumButton);
            Clear(fewerMaximumButton);
            Clear(moreMaximumButton);
            Clear(saveOverButton);
            Clear(saveAsNewButton);
            for (int i = 0; stepButtons != null && i < stepButtons.Length; i++) Clear(stepButtons[i]);
            Clear(backButton);
            Clear(nextButton);
            Clear(previousPickButton);
            Clear(nextPickButton);
            Clear(actionAButton);
            Clear(actionBButton);
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HandleClose() => actions?.Close?.Invoke();
        private void HandleFewerMinimum() => actions?.ChangePlayers?.Invoke(true, -1, Typed());
        private void HandleMoreMinimum() => actions?.ChangePlayers?.Invoke(true, 1, Typed());
        private void HandleFewerMaximum() => actions?.ChangePlayers?.Invoke(false, -1, Typed());
        private void HandleMoreMaximum() => actions?.ChangePlayers?.Invoke(false, 1, Typed());
        private void HandleSaveOver() => actions?.SaveOver?.Invoke(Typed());
        private void HandleSaveAsNew() => actions?.SaveAsNew?.Invoke(Typed());
    }
}
