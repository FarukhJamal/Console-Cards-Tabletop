using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>RULES-SHEET (R2): one field row of the Rules sheet panel.</summary>
    public readonly struct RulesSheetRow
    {
        public RulesSheetRow(string key, string label, RuleSettingKind kind, string value, string sentence)
        {
            Key = key;
            Label = label;
            Kind = kind;
            Value = value ?? string.Empty;
            Sentence = sentence ?? string.Empty;
        }

        public string Key { get; }
        public string Label { get; }
        public RuleSettingKind Kind { get; }

        /// <summary>The field's value; empty: players decide.</summary>
        public string Value { get; }

        /// <summary>The rulebook sentence this value makes.</summary>
        public string Sentence { get; }
    }

    public sealed class RulesSheetModel
    {
        public string Title = "RULES SHEET";
        public int Section;

        /// <summary>The section tabs' words, one per section (for example "Limits · 2").</summary>
        public IReadOnlyList<string> SectionTabs = Array.Empty<string>();
        public string SectionHint = string.Empty;
        public IReadOnlyList<RulesSheetRow> Rows = Array.Empty<RulesSheetRow>();
        public string SectionText = string.Empty;
        public string CopyText = string.Empty;
        public string Status = string.Empty;
    }

    public sealed class RulesSheetActions
    {
        /// <summary>Another section; the written text typed in the one being left comes along.</summary>
        public Action<int, string> PickSection;

        /// <summary>A field typed (or emptied with ×): its key and the new value.</summary>
        public Action<string, string> SetValue;

        /// <summary>A choice's &lt; &gt; or a number's – +: its key and -1 or +1.</summary>
        public Action<string, int> StepValue;

        public Action<string> SetSectionText;
        public Action<int> CycleCopySource;
        public Action CopyFrom;
        public Action ClearSection;

        /// <summary>Back to Make a game; the written text being typed comes along.</summary>
        public Action<string> Done;
    }

    /// <summary>
    /// RULES-SHEET (R2; owner, 2026-11-20: "Own panel"): the rules sheet of a game being made, opened from Make a
    /// game's RULES step. The ten sections are tabs down the left; the chosen section's fields are rows on the right
    /// (each with the sentence it puts in the rulebook), with the section's written rules under them. Copy from takes
    /// a whole sheet from another of your games. Nothing is saved here: Make a game's SAVE saves the sheet with the game.
    /// Built by Console Cards > UI > Create Missing Table HUD (HUD-B).
    /// </summary>
    public sealed class RulesSheetPanelView : ReusableUiView
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Button[] sectionButtons;
        [SerializeField] private Text[] sectionLabels;
        [SerializeField] private Image[] sectionPills;
        [SerializeField] private Text sectionTitleLabel;
        [SerializeField] private Text sectionHintLabel;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private RulesSheetRowView rowTemplate;
        [SerializeField] private float rowPitch = 90f;
        [SerializeField] private InputField sectionTextInput;
        [SerializeField] private Button previousCopyButton;
        [SerializeField] private Text copyLabel;
        [SerializeField] private Button nextCopyButton;
        [SerializeField] private Button copyButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button doneButton;
        [SerializeField] private Color pillOn = new Color(0.11f, 0.235f, 0.239f, 1f);
        [SerializeField] private Color pillOff = new Color(0f, 0f, 0f, 0f);

        private readonly List<RulesSheetRowView> rowPool = new List<RulesSheetRowView>();
        private RulesSheetActions actions;

        public void ValidateReferences()
        {
            if (titleLabel == null || sectionButtons == null || sectionButtons.Length != RulesSheet.SectionHeadings.Length
                || sectionLabels == null || sectionLabels.Length != sectionButtons.Length || sectionTitleLabel == null
                || rowsRoot == null || rowTemplate == null || sectionTextInput == null || statusLabel == null || doneButton == null)
            {
                throw new InvalidOperationException("RulesSheetPanelView requires its authored references.");
            }
        }

        public void Bind(RulesSheetModel model, RulesSheetActions sheetActions)
        {
            RequireAcquired();
            ValidateReferences();
            if (model == null) throw new ArgumentNullException(nameof(model));
            RemoveListeners();
            actions = sheetActions;
            titleLabel.text = model.Title;
            for (int i = 0; i < sectionButtons.Length; i++)
            {
                int section = i;
                if (sectionButtons[i] != null) sectionButtons[i].onClick.AddListener(() => actions?.PickSection?.Invoke(section, sectionTextInput.text));
                if (sectionLabels[i] != null) sectionLabels[i].text = i < model.SectionTabs.Count ? model.SectionTabs[i] : RulesSheet.SectionHeadings[i];
                if (sectionPills != null && i < sectionPills.Length && sectionPills[i] != null) sectionPills[i].color = i == model.Section ? pillOn : pillOff;
            }

            int now = Mathf.Clamp(model.Section, 0, RulesSheet.SectionHeadings.Length - 1);
            sectionTitleLabel.text = (now + 1) + " · " + RulesSheet.SectionHeadings[now].ToUpperInvariant();
            if (sectionHintLabel != null) sectionHintLabel.text = model.SectionHint;
            ShowRows(model.Rows);
            sectionTextInput.SetTextWithoutNotify(model.SectionText);
            sectionTextInput.onEndEdit.AddListener(text => actions?.SetSectionText?.Invoke(text));
            if (copyLabel != null) copyLabel.text = model.CopyText;
            if (previousCopyButton != null) previousCopyButton.onClick.AddListener(() => actions?.CycleCopySource?.Invoke(-1));
            if (nextCopyButton != null) nextCopyButton.onClick.AddListener(() => actions?.CycleCopySource?.Invoke(1));
            if (copyButton != null) copyButton.onClick.AddListener(() => actions?.CopyFrom?.Invoke());
            if (clearButton != null) clearButton.onClick.AddListener(() => actions?.ClearSection?.Invoke());
            statusLabel.text = model.Status ?? string.Empty;
            doneButton.onClick.AddListener(() => actions?.Done?.Invoke(sectionTextInput.text));
        }

        public override void Unbind()
        {
            RemoveListeners();
            actions = null;
        }

        private void ShowRows(IReadOnlyList<RulesSheetRow> rows)
        {
            rowTemplate.gameObject.SetActive(false);
            while (rowPool.Count < rows.Count) rowPool.Add(Instantiate(rowTemplate, rowsRoot, false));
            for (int i = 0; i < rowPool.Count; i++)
            {
                RulesSheetRowView row = rowPool[i];
                bool shown = i < rows.Count;
                if (row.gameObject.activeSelf != shown) row.gameObject.SetActive(shown);
                if (shown) row.Bind(i, rowPitch, rows[i], (key, value) => actions?.SetValue?.Invoke(key, value),
                    (key, step) => actions?.StepValue?.Invoke(key, step));
                else row.Unbind();
            }
        }

        private void RemoveListeners()
        {
            for (int i = 0; sectionButtons != null && i < sectionButtons.Length; i++)
            {
                if (sectionButtons[i] != null) sectionButtons[i].onClick.RemoveAllListeners();
            }

            if (sectionTextInput != null) sectionTextInput.onEndEdit.RemoveAllListeners();
            if (previousCopyButton != null) previousCopyButton.onClick.RemoveAllListeners();
            if (nextCopyButton != null) nextCopyButton.onClick.RemoveAllListeners();
            if (copyButton != null) copyButton.onClick.RemoveAllListeners();
            if (clearButton != null) clearButton.onClick.RemoveAllListeners();
            if (doneButton != null) doneButton.onClick.RemoveAllListeners();
            for (int i = 0; i < rowPool.Count; i++) rowPool[i].Unbind();
        }
    }
}
