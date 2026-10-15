using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>How a proposed change is edited (doc 23, R3).</summary>
    public enum RuleProposalEditKind
    {
        /// <summary>Nothing to edit: the proposal is shown for agreement (for example switching rule sets).</summary>
        None = 0,
        /// <summary>The value steps with - and + (numbers, on/off, choices).</summary>
        Step = 1,
        /// <summary>The value or the rule text is typed.</summary>
        Text = 2,
    }

    /// <summary>One player's agreement button.</summary>
    public readonly struct RuleProposalVoter
    {
        public RuleProposalVoter(string label, bool agreed)
        {
            Label = label ?? string.Empty;
            Agreed = agreed;
        }

        public string Label { get; }
        public bool Agreed { get; }
    }

    /// <summary>What the rule proposal panel shows (doc 23, R3).</summary>
    public sealed class RuleProposalModel
    {
        public string Title = "CHANGE A RULE";
        public string CurrentText = string.Empty;
        /// <summary>False while the proposer edits the change; true while the players agree.</summary>
        public bool Agreeing;
        public RuleProposalEditKind EditKind;
        public string ValueText = string.Empty;
        public string EditText = string.Empty;
        public string PreviewText = string.Empty;
        public string Status = string.Empty;
        public IReadOnlyList<RuleProposalVoter> Voters = Array.Empty<RuleProposalVoter>();
    }

    /// <summary>The rule proposal panel's actions, supplied by the game.</summary>
    public sealed class RuleProposalActions
    {
        public Action<int> Step;
        public Action<string> TextChanged;
        public Action Propose;
        public Action<int> Agree;
        public Action Reject;
    }

    /// <summary>
    /// House rules (doc 23, R3): a player proposes a change to one rule, then every player at the table agrees with
    /// their own button (one "No" cancels). An authored Final UI prefab; nothing is built at runtime.
    /// </summary>
    public sealed class RuleProposalPanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text currentLabel;
        [SerializeField] private GameObject stepRow;
        [SerializeField] private Button decrementButton;
        [SerializeField] private Button incrementButton;
        [SerializeField] private Text valueLabel;
        [SerializeField] private InputField textInput;
        [SerializeField] private Text previewLabel;
        [SerializeField] private GameObject agreeRow;
        [SerializeField] private Button[] agreeButtons = Array.Empty<Button>();
        [SerializeField] private Text[] agreeLabels = Array.Empty<Text>();
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button proposeButton;
        [SerializeField] private Button rejectButton;
        [SerializeField] private Text rejectLabel;

        private RuleProposalActions actions;

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || titleLabel == null || currentLabel == null || stepRow == null
                || decrementButton == null || incrementButton == null || valueLabel == null || textInput == null
                || previewLabel == null || agreeRow == null || agreeButtons == null || agreeButtons.Length == 0
                || agreeLabels == null || agreeLabels.Length != agreeButtons.Length || statusLabel == null
                || proposeButton == null || rejectButton == null || rejectLabel == null)
            {
                throw new InvalidOperationException(
                    "RuleProposalPanelView is missing an authored reference. Run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI).");
            }
        }

        public void Bind(RuleProposalModel model, RuleProposalActions panelActions)
        {
            RequireAcquired();
            ValidateReferences();
            RemoveListeners();
            actions = panelActions ?? throw new ArgumentNullException(nameof(panelActions));
            titleLabel.text = model.Title;
            currentLabel.text = model.CurrentText;
            previewLabel.text = model.PreviewText;
            statusLabel.text = model.Status;
            bool editing = !model.Agreeing;
            stepRow.SetActive(editing && model.EditKind == RuleProposalEditKind.Step);
            valueLabel.text = model.ValueText;
            textInput.gameObject.SetActive(editing && model.EditKind == RuleProposalEditKind.Text);
            if (editing && model.EditKind == RuleProposalEditKind.Text && textInput.text != model.EditText)
            {
                textInput.SetTextWithoutNotify(model.EditText);
            }

            proposeButton.gameObject.SetActive(editing);
            rejectLabel.text = editing ? "Cancel" : "No, keep the rule";
            agreeRow.SetActive(model.Agreeing);
            for (int i = 0; i < agreeButtons.Length; i++)
            {
                bool used = model.Agreeing && i < model.Voters.Count;
                agreeButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                RuleProposalVoter voter = model.Voters[i];
                agreeLabels[i].text = voter.Agreed ? $"{voter.Label}: agreed" : $"{voter.Label}: agree";
                agreeButtons[i].interactable = !voter.Agreed;
                int index = i;
                agreeButtons[i].onClick.AddListener(() => actions?.Agree?.Invoke(index));
            }

            decrementButton.onClick.AddListener(() => actions?.Step?.Invoke(-1));
            incrementButton.onClick.AddListener(() => actions?.Step?.Invoke(1));
            textInput.onValueChanged.AddListener(value => actions?.TextChanged?.Invoke(value));
            proposeButton.onClick.AddListener(() => actions?.Propose?.Invoke());
            rejectButton.onClick.AddListener(() => actions?.Reject?.Invoke());
            dismissOverlayButton.onClick.AddListener(() => actions?.Reject?.Invoke());
        }

        public override void Unbind()
        {
            RemoveListeners();
            actions = null;
        }

        private void RemoveListeners()
        {
            if (decrementButton != null) decrementButton.onClick.RemoveAllListeners();
            if (incrementButton != null) incrementButton.onClick.RemoveAllListeners();
            if (textInput != null) textInput.onValueChanged.RemoveAllListeners();
            if (proposeButton != null) proposeButton.onClick.RemoveAllListeners();
            if (rejectButton != null) rejectButton.onClick.RemoveAllListeners();
            if (dismissOverlayButton != null) dismissOverlayButton.onClick.RemoveAllListeners();
            if (agreeButtons == null) return;
            for (int i = 0; i < agreeButtons.Length; i++)
            {
                if (agreeButtons[i] != null) agreeButtons[i].onClick.RemoveAllListeners();
            }
        }
    }
}
