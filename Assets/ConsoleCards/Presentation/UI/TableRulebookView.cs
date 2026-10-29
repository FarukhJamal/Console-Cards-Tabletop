using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// HUD-B (owner, 2026-10-29: "we are not showing rules on the UI anymore … it will open a book"): the rules as a
    /// two-page book over the dimmed table. Each section of the rule set is a page; the tabs on the page edge jump to
    /// a section (a tab with a house rule is marked); ‹ › turn two pages; search finds any rule. A rule the table
    /// changed carries a HOUSE mark, and CHANGE proposes a house rule as the rules card did (doc 23, R3). The rule set
    /// arrows and the restart line work as on the rules card. Layout and colours are on the prefab
    /// (Final UI/Notices/TableRulebook); rows and tabs are templates cloned once and kept.
    /// </summary>
    public sealed class TableRulebookView : ReusableUiView
    {
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;
        [Header("Pages")]
        [SerializeField] private Text leftHeading;
        [SerializeField] private RectTransform leftLines;
        [SerializeField] private Text leftPageNumber;
        [SerializeField] private Text rightHeading;
        [SerializeField] private RectTransform rightLines;
        [SerializeField] private Text rightPageNumber;
        [SerializeField] private RulebookLineView lineTemplate;
        [Header("Tabs")]
        [SerializeField] private RectTransform tabsRoot;
        [SerializeField] private Button tabTemplate;
        [SerializeField] private Color tabIdleFill = new Color(0.839f, 0.812f, 0.741f, 1f);
        [SerializeField] private Color tabActiveFill = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color tabHouseFill = new Color(0.949f, 0.788f, 0.659f, 1f);
        [SerializeField] private Color tabIdleWords = new Color(0.227f, 0.259f, 0.271f, 1f);
        [SerializeField] private Color tabActiveWords = new Color(0.063f, 0.125f, 0.129f, 1f);
        [Header("Footer")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Text pageCountLabel;
        [SerializeField] private InputField searchField;
        [SerializeField] private GameObject ruleSetRow;
        [SerializeField] private Button previousSetButton;
        [SerializeField] private Button nextSetButton;
        [SerializeField] private Text ruleSetLabel;
        [SerializeField] private GameObject pendingRow;
        [SerializeField] private Text pendingLabel;
        [SerializeField] private Button restartButton;

        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

        private sealed class Section
        {
            public string Heading;
            public readonly List<RulesCardLine> Rules = new List<RulesCardLine>();
            public bool HasHouseRule;
        }

        private readonly List<Section> sections = new List<Section>();
        private readonly List<RulebookLineView> leftRows = new List<RulebookLineView>();
        private readonly List<RulebookLineView> rightRows = new List<RulebookLineView>();
        private readonly List<Button> tabs = new List<Button>();
        private readonly List<Text> tabLabels = new List<Text>();
        private readonly List<RulesCardLine> found = new List<RulesCardLine>();
        private RulesCardModel model;
        private Action close;
        private int spread; // the left page's section index (even)
        private string query = string.Empty;

        public void ValidateReferences()
        {
            if (backdropButton == null || closeButton == null || titleLabel == null || leftHeading == null
                || leftLines == null || leftPageNumber == null || rightHeading == null || rightLines == null
                || rightPageNumber == null || lineTemplate == null || !lineTemplate.IsComplete || tabsRoot == null
                || tabTemplate == null || previousButton == null || nextButton == null || pageCountLabel == null
                || searchField == null || ruleSetRow == null || previousSetButton == null || nextSetButton == null
                || ruleSetLabel == null || pendingRow == null || pendingLabel == null || restartButton == null)
            {
                throw new InvalidOperationException(
                    "TableRulebookView is missing an authored part. Run Console Cards > UI > Create Missing Table HUD (HUD-B).");
            }
        }

        public void Bind(RulesCardModel rules, Action closeBook)
        {
            RequireAcquired();
            ValidateReferences();
            RemoveListeners();
            model = rules ?? throw new ArgumentNullException(nameof(rules));
            close = closeBook;
            lineTemplate.gameObject.SetActive(false);
            tabTemplate.gameObject.SetActive(false);
            BuildSections();
            titleLabel.text = string.IsNullOrEmpty(model.GameName)
                ? "RULEBOOK"
                : "RULEBOOK · " + model.GameName.ToUpperInvariant();

            backdropButton.onClick.AddListener(Close);
            closeButton.onClick.AddListener(Close);
            previousButton.onClick.AddListener(() => Turn(-2));
            nextButton.onClick.AddListener(() => Turn(2));
            searchField.onValueChanged.AddListener(Search);
            RulesCardActions ruleActions = model.Actions;
            bool canCycle = ruleActions?.CycleRuleSet != null && model.RuleSetCount > 1;
            ruleSetRow.SetActive(true);
            previousSetButton.gameObject.SetActive(canCycle);
            nextSetButton.gameObject.SetActive(canCycle);
            if (canCycle)
            {
                previousSetButton.onClick.AddListener(() => ruleActions.CycleRuleSet(-1));
                nextSetButton.onClick.AddListener(() => ruleActions.CycleRuleSet(1));
            }

            ruleSetLabel.text = model.IsHouseRules ? $"{model.SetName}  ·  HOUSE RULES" : model.SetName;
            bool pending = !string.IsNullOrEmpty(model.PendingText);
            pendingRow.SetActive(pending);
            pendingLabel.text = model.PendingText;
            restartButton.gameObject.SetActive(pending && ruleActions?.Restart != null);
            if (ruleActions?.Restart != null) restartButton.onClick.AddListener(() => ruleActions.Restart());

            BuildTabs();
            if (searchField.text.Length > 0) searchField.SetTextWithoutNotify(string.Empty);
            query = string.Empty;
            spread = EvenSpread(spread);
            Render();
        }

        /// <summary>The rules changed while the book is open (a house rule was agreed): show them, keep the page.</summary>
        public void Refresh(RulesCardModel rules)
        {
            if (rules == null || !IsAcquired) return;
            Bind(rules, close); // keeps the open pages (Bind keeps the spread when it still fits)
        }

        public override void Unbind()
        {
            RemoveListeners();
            model = null;
            close = null;
        }

        private void Update()
        {
            if (model == null || Keyboard.current == null || IsCovered()) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (searchField.isFocused) return;
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.pageDownKey.wasPressedThisFrame) Turn(2);
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.pageUpKey.wasPressedThisFrame) Turn(-2);
        }

        // Another panel (a rule proposal) open over the book: its keys are its own.
        private bool IsCovered()
        {
            Transform parent = transform.parent;
            if (parent == null) return false;
            for (int i = transform.GetSiblingIndex() + 1; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).gameObject.activeSelf) return true;
            }

            return false;
        }

        private void Close()
        {
            Action closeBook = close;
            closeBook?.Invoke();
        }

        // The left page of a spread is always an even section, inside the book.
        private int EvenSpread(int index)
        {
            int last = Mathf.Max(0, sections.Count - 1);
            int clamped = Mathf.Clamp(index, 0, last);
            return clamped - (clamped % 2);
        }

        private void Turn(int pages)
        {
            if (query.Length > 0) return;
            int next = spread + pages;
            if (next < 0 || next >= sections.Count) return;
            spread = next;
            Render();
        }

        private void Search(string text)
        {
            query = (text ?? string.Empty).Trim();
            Render();
        }

        private void BuildSections()
        {
            sections.Clear();
            Section current = null;
            for (int i = 0; i < model.Lines.Count; i++)
            {
                RulesCardLine line = model.Lines[i];
                if (line.Kind == RulesCardLineKind.Heading)
                {
                    current = new Section { Heading = Tags.Replace(line.Text, string.Empty) };
                    sections.Add(current);
                    continue;
                }

                if (current == null)
                {
                    current = new Section { Heading = "Rules" };
                    sections.Add(current);
                }

                current.Rules.Add(line);
                if (line.Changed) current.HasHouseRule = true;
            }
        }

        private void BuildTabs()
        {
            while (tabs.Count < sections.Count)
            {
                Button tab = Instantiate(tabTemplate, tabsRoot, false);
                tab.name = "Tab" + tabs.Count;
                int index = tabs.Count;
                tab.onClick.AddListener(() => JumpTo(index));
                tabs.Add(tab);
                tabLabels.Add(tab.GetComponentInChildren<Text>(true));
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                bool shown = i < sections.Count;
                tabs[i].gameObject.SetActive(shown);
                if (shown && tabLabels[i] != null)
                {
                    string heading = sections[i].Heading.ToUpperInvariant();
                    tabLabels[i].text = sections[i].HasHouseRule ? heading + " •" : heading;
                }
            }
        }

        private void JumpTo(int section)
        {
            if (section < 0 || section >= sections.Count) return;
            if (query.Length > 0) searchField.text = string.Empty; // leaves search; Search() renders
            spread = section - (section % 2);
            Render();
        }

        private void Render()
        {
            if (model == null) return;
            if (query.Length > 0)
            {
                found.Clear();
                for (int s = 0; s < sections.Count; s++)
                {
                    List<RulesCardLine> rules = sections[s].Rules;
                    for (int r = 0; r < rules.Count; r++)
                    {
                        if (Tags.Replace(rules[r].Text, string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found.Add(rules[r]);
                        }
                    }
                }

                int half = (found.Count + 1) / 2;
                leftHeading.text = found.Count == 0 ? $"Nothing for “{query}”" : $"Found {found.Count}";
                rightHeading.text = string.Empty;
                FillPage(leftRows, leftLines, found, 0, half);
                FillPage(rightRows, rightLines, found, half, found.Count - half);
                leftPageNumber.text = string.Empty;
                rightPageNumber.text = string.Empty;
                pageCountLabel.text = "Search";
                previousButton.interactable = false;
                nextButton.interactable = false;
                MarkTabs(-1);
                return;
            }

            ShowSection(spread, leftHeading, leftRows, leftLines, leftPageNumber);
            ShowSection(spread + 1, rightHeading, rightRows, rightLines, rightPageNumber);
            int last = Mathf.Min(spread + 2, sections.Count);
            pageCountLabel.text = sections.Count == 0 ? "No rules" : $"Pages {spread + 1}–{last} of {sections.Count}";
            previousButton.interactable = spread > 0;
            nextButton.interactable = spread + 2 < sections.Count;
            MarkTabs(spread);
        }

        private void ShowSection(int index, Text heading, List<RulebookLineView> rows, RectTransform root, Text pageNumber)
        {
            if (index < 0 || index >= sections.Count)
            {
                heading.text = string.Empty;
                pageNumber.text = string.Empty;
                FillPage(rows, root, null, 0, 0);
                return;
            }

            Section section = sections[index];
            heading.text = section.Heading;
            pageNumber.text = (index + 1).ToString();
            FillPage(rows, root, section.Rules, 0, section.Rules.Count);
        }

        private void FillPage(List<RulebookLineView> rows, RectTransform root, List<RulesCardLine> lines, int start, int count)
        {
            while (rows.Count < count)
            {
                RulebookLineView row = Instantiate(lineTemplate, root, false);
                row.name = "Rule" + rows.Count;
                rows.Add(row);
            }

            Action<string> propose = model.Actions?.ProposeChange;
            for (int i = 0; i < rows.Count; i++)
            {
                bool shown = i < count;
                if (rows[i].gameObject.activeSelf != shown) rows[i].gameObject.SetActive(shown);
                if (shown) rows[i].Bind(lines[start + i], propose);
            }
        }

        private void MarkTabs(int leftSection)
        {
            for (int i = 0; i < tabs.Count && i < sections.Count; i++)
            {
                bool open = leftSection >= 0 && (i == leftSection || i == leftSection + 1);
                Image fill = tabs[i].targetGraphic as Image;
                if (fill != null) fill.color = open ? tabActiveFill : sections[i].HasHouseRule ? tabHouseFill : tabIdleFill;
                if (tabLabels[i] != null) tabLabels[i].color = open ? tabActiveWords : tabIdleWords;
            }
        }

        private void RemoveListeners()
        {
            Button[] buttons =
            {
                backdropButton, closeButton, previousButton, nextButton, previousSetButton, nextSetButton, restartButton,
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null) buttons[i].onClick.RemoveAllListeners();
            }

            if (searchField != null) searchField.onValueChanged.RemoveAllListeners();
        }
    }
}
