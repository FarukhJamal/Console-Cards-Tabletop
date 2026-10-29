using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// MAKE-B (owner, 2026-10-30; design board 13 "Make a game · Save step"): the rules one line each (× removes one,
    /// Enter adds one, drag ⋮⋮ to reorder; a line starting with "#" is a heading), HOW THE RULES CARD WILL LOOK, WHAT
    /// GETS SAVED, ABOUT (how long a game takes) and a mark on each step that is done. All optional: a panel without
    /// these parts works as before. The rules field itself stays the text that is saved; the rows write into it.
    /// </summary>
    public sealed partial class GameSavePanelView
    {
        [Header("Board 13 (optional)")]
        [SerializeField] private Button minutesButton;
        [SerializeField] private Text minutesLabel;
        [SerializeField] private RectTransform ruleRows;
        [SerializeField] private SaveRuleRow ruleRowTemplate;
        [SerializeField] private float ruleRowPitch = 69f;
        [SerializeField] private InputField newRuleInput;
        [SerializeField] private Text previewTitle;
        [SerializeField] private RectTransform previewRows;
        [Tooltip("A row of the rules card preview: a Plate (hidden on a heading) and its Text.")]
        [SerializeField] private GameObject previewRowTemplate;
        [SerializeField] private float previewRowPitch = 63f;
        [SerializeField] private RectTransform savedRows;
        [SerializeField] private GameObject savedRowTemplate;
        [SerializeField] private float savedRowPitch = 40f;
        [SerializeField] private GameObject[] stepDoneMarks = System.Array.Empty<GameObject>();
        [Tooltip("The number circle of each step (hidden when the step is done and its mark shows).")]
        [SerializeField] private GameObject[] stepNumberMarks = System.Array.Empty<GameObject>();
        [Tooltip("Board 13: the steps read DECK, CARDS, DEAL, SAVE (their numbers are in the circles), and BACK is not on SAVE.")]
        [SerializeField] private bool boardSteps;
        [SerializeField] private Image[] stepPills = System.Array.Empty<Image>();
        [SerializeField] private Color stepDoneColor = new Color(0.184f, 0.831f, 0.420f, 1f);
        [SerializeField] private Color stepNowColor = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color stepIdleColor = new Color(0.608f, 0.643f, 0.655f, 1f);
        [SerializeField] private GameObject warningBox;

        private static readonly string[] BoardStepNames = { "DECK", "CARDS", "DEAL", "SAVE" };

        private readonly List<SaveRuleRow> ruleRowPool = new List<SaveRuleRow>();
        private readonly List<string> rules = new List<string>();
        private readonly List<GameObject> previewPool = new List<GameObject>();
        private readonly List<GameObject> savedPool = new List<GameObject>();
        private readonly StringBuilder rulesText = new StringBuilder(512);
        private string gameName = string.Empty;

        private bool HasRuleList => ruleRows != null && ruleRowTemplate != null && newRuleInput != null;

        private void BindBoard(GameSaveModel model)
        {
            gameName = model.Name ?? string.Empty;
            if (minutesButton != null && minutesLabel != null)
            {
                minutesLabel.text = $"{model.Minutes} min";
                minutesButton.onClick.RemoveAllListeners();
                minutesButton.onClick.AddListener(() => actions?.CycleMinutes?.Invoke(1, Typed()));
            }

            ShowStepMarks(model);
            if (boardSteps && backButton != null && model.Step == GameSaveModel.SaveStep) backButton.gameObject.SetActive(false);
            ShowSavedRows(model);
            if (warningBox != null) warningBox.SetActive(!string.IsNullOrEmpty(model.Status));
            if (nameInput != null)
            {
                nameInput.onValueChanged.RemoveListener(NameTyped);
                nameInput.onValueChanged.AddListener(NameTyped);
            }

            if (!HasRuleList)
            {
                ShowPreview();
                return;
            }

            ruleRowTemplate.gameObject.SetActive(false);
            rules.Clear();
            string[] lines = (model.RulesText ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length > 0) rules.Add(line);
            }

            newRuleInput.onEndEdit.RemoveAllListeners();
            newRuleInput.onEndEdit.AddListener(AddRule);
            newRuleInput.SetTextWithoutNotify(string.Empty);
            ShowRules();
        }

        private void UnbindBoard()
        {
            if (minutesButton != null) minutesButton.onClick.RemoveAllListeners();
            if (newRuleInput != null) newRuleInput.onEndEdit.RemoveAllListeners();
            if (nameInput != null) nameInput.onValueChanged.RemoveListener(NameTyped);
            for (int i = 0; i < ruleRowPool.Count; i++) ruleRowPool[i].Unbind();
        }

        private void NameTyped(string text)
        {
            gameName = text ?? string.Empty;
            ShowPreview();
        }

        // ---------- the rules, one line each ----------

        private void ShowRules()
        {
            while (ruleRowPool.Count < rules.Count)
            {
                SaveRuleRow row = Instantiate(ruleRowTemplate, ruleRows, false);
                row.name = "Rule" + ruleRowPool.Count;
                ruleRowPool.Add(row);
            }

            for (int i = 0; i < ruleRowPool.Count; i++)
            {
                SaveRuleRow row = ruleRowPool[i];
                bool shown = i < rules.Count;
                if (row.gameObject.activeSelf != shown) row.gameObject.SetActive(shown);
                if (!shown) continue;
                row.Bind(i, rules[i], ruleRowPitch, EditRule, RemoveRule, MoveRule);
            }

            // The "Type a rule and press Enter…" line goes under the last rule.
            RectTransform input = (RectTransform)newRuleInput.transform;
            input.anchoredPosition = new Vector2(input.anchoredPosition.x, -rules.Count * ruleRowPitch);
            WriteRules();
        }

        private void AddRule(string text)
        {
            string line = (text ?? string.Empty).Trim();
            if (line.Length == 0) return;
            rules.Add(line);
            newRuleInput.SetTextWithoutNotify(string.Empty);
            ShowRules();
            newRuleInput.ActivateInputField(); // ready for the next rule
        }

        private void EditRule(int index, string text)
        {
            if (index < 0 || index >= rules.Count) return;
            string line = (text ?? string.Empty).Trim();
            if (line.Length == 0) rules.RemoveAt(index);
            else rules[index] = line;
            ShowRules();
        }

        private void RemoveRule(int index)
        {
            if (index < 0 || index >= rules.Count) return;
            rules.RemoveAt(index);
            ShowRules();
        }

        private void MoveRule(int from, int to)
        {
            if (from < 0 || from >= rules.Count) return;
            to = Mathf.Clamp(to, 0, rules.Count - 1);
            if (to != from)
            {
                string line = rules[from];
                rules.RemoveAt(from);
                rules.Insert(to, line);
            }

            ShowRules();
        }

        // The rules field is what is saved (and handed back with every action), so the rows write into it.
        private void WriteRules()
        {
            rulesText.Length = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                if (i > 0) rulesText.Append('\n');
                rulesText.Append(rules[i]);
            }

            rulesInput.SetTextWithoutNotify(rulesText.ToString());
            ShowPreview();
        }

        // ---------- HOW THE RULES CARD WILL LOOK ----------

        private void ShowPreview()
        {
            if (previewRows == null || previewRowTemplate == null) return;
            if (previewTitle != null)
            {
                previewTitle.text = string.IsNullOrWhiteSpace(gameName) ? "RULES" : "RULES · " + gameName.Trim().ToUpperInvariant();
            }

            previewRowTemplate.SetActive(false);
            List<string> lines = rules;
            if (!HasRuleList)
            {
                lines = new List<string>();
                foreach (string line in (rulesInput.text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
                {
                    if (line.Trim().Length > 0) lines.Add(line.Trim());
                }
            }

            float height = previewRows.rect.height;
            int fits = Mathf.Max(1, Mathf.FloorToInt(height / previewRowPitch));
            int shown = Mathf.Min(lines.Count, fits);
            while (previewPool.Count < shown) previewPool.Add(Instantiate(previewRowTemplate, previewRows, false));
            for (int i = 0; i < previewPool.Count; i++)
            {
                GameObject row = previewPool[i];
                bool on = i < shown;
                if (row.activeSelf != on) row.SetActive(on);
                if (!on) continue;
                string line = lines[i];
                bool heading = line.StartsWith("#", System.StringComparison.Ordinal);
                Text text = row.transform.Find("Text")?.GetComponent<Text>();
                if (text != null)
                {
                    text.text = heading
                        ? line.TrimStart('#').Trim().ToUpperInvariant()
                        : "<color=#f2762e>›</color>  " + line;
                    text.fontStyle = heading ? FontStyle.Bold : FontStyle.Normal;
                }

                RectTransform rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -i * previewRowPitch);
                Transform plate = row.transform.Find("Plate");
                if (plate != null) plate.gameObject.SetActive(!heading);
            }
        }

        // ---------- WHAT GETS SAVED, the step marks ----------

        private void ShowSavedRows(GameSaveModel model)
        {
            if (savedRows == null || savedRowTemplate == null) return;
            savedRowTemplate.SetActive(false);
            IReadOnlyList<GameSaveRow> rows = model.SavedRows;
            while (savedPool.Count < rows.Count) savedPool.Add(Instantiate(savedRowTemplate, savedRows, false));
            for (int i = 0; i < savedPool.Count; i++)
            {
                GameObject row = savedPool[i];
                bool on = i < rows.Count;
                if (row.activeSelf != on) row.SetActive(on);
                if (!on) continue;
                RectTransform rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -i * savedRowPitch);
                Text label = row.transform.Find("Label")?.GetComponent<Text>();
                Text value = row.transform.Find("Value")?.GetComponent<Text>();
                if (label != null) label.text = rows[i].Label;
                if (value != null) value.text = rows[i].Value;
            }
        }

        private void ShowStepMarks(GameSaveModel model)
        {
            int now = Mathf.Clamp(model.Step, GameSaveModel.DeckStep, GameSaveModel.SaveStep);
            for (int i = 0; i < GameSaveModel.StepCount; i++)
            {
                bool done = model.StepsDone != null && i < model.StepsDone.Length && model.StepsDone[i] && i != now;
                if (i < stepDoneMarks.Length && stepDoneMarks[i] != null) stepDoneMarks[i].SetActive(done);
                if (i < stepNumberMarks.Length && stepNumberMarks[i] != null) stepNumberMarks[i].SetActive(!done);
                Color colour = i == now ? stepNowColor : done ? stepDoneColor : stepIdleColor;
                if (stepLabels != null && i < stepLabels.Length && stepLabels[i] != null)
                {
                    stepLabels[i].color = colour;
                    if (boardSteps) stepLabels[i].text = BoardStepNames[i];
                }
                if (i < stepPills.Length && stepPills[i] != null) stepPills[i].enabled = i == now;
            }
        }
    }
}
