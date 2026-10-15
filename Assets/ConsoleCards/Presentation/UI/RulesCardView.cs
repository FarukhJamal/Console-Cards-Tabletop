using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// The rules card (doc 23, R1): the active rule set's name and its lines for the current mode, in the left
    /// column. It only shows rules; nothing is enforced. The chevron folds it to its title band.
    /// </summary>
    public sealed class RulesCardView : ReusableUiView
    {
        [SerializeField] private Text setNameLabel;
        [SerializeField] private GameObject houseRulesBadge;
        [SerializeField] private Button collapseButton;
        [SerializeField] private RectTransform chevron;
        [SerializeField] private GameObject body;
        [SerializeField] private RectTransform linesRoot;
        [SerializeField] private ScrollRect scrollRect;
        [Header("House rules (doc 23, R3; optional, added by Create Missing Gameplay UI Prefabs)")]
        [SerializeField] private GameObject ruleSetRow;
        [SerializeField] private Button previousSetButton;
        [SerializeField] private Button nextSetButton;
        [SerializeField] private GameObject pendingRow;
        [SerializeField] private Text pendingLabel;
        [SerializeField] private Button restartButton;

        private readonly List<RulesCardLineView> lineViews = new List<RulesCardLineView>();
        private IRuntimeUiService uiService;
        private bool collapsed;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (setNameLabel == null || houseRulesBadge == null || collapseButton == null || chevron == null
                || body == null || linesRoot == null || scrollRect == null || uiService == null)
            {
                throw new InvalidOperationException(
                    "RulesCardView is missing an authored reference or the UI service. Run Console Cards > UI > Build Gameplay UI Prefabs.");
            }
        }

        public void Bind(RulesCardModel model)
        {
            RequireAcquired();
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            ValidateReferences();
            Unbind();
            setNameLabel.text = model.SetName;
            houseRulesBadge.SetActive(model.IsHouseRules);
            for (int i = 0; i < model.Lines.Count; i++)
            {
                RulesCardLine line = model.Lines[i];
                RulesCardLineView view = uiService.AcquirePooled<RulesCardLineView>(
                    line.Kind == RulesCardLineKind.Heading ? PrototypeUiPrefabIds.RulesHeading : PrototypeUiPrefabIds.RulesLine,
                    linesRoot);
                view.Bind(line, model.Actions?.ProposeChange);
                view.Show();
                lineViews.Add(view);
            }

            BindHouseRules(model);
            collapseButton.onClick.AddListener(ToggleCollapsed);
            ApplyCollapsed();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        public override void Unbind()
        {
            if (collapseButton != null)
            {
                collapseButton.onClick.RemoveAllListeners();
            }

            if (previousSetButton != null) previousSetButton.onClick.RemoveAllListeners();
            if (nextSetButton != null) nextSetButton.onClick.RemoveAllListeners();
            if (restartButton != null) restartButton.onClick.RemoveAllListeners();

            for (int i = lineViews.Count - 1; i >= 0; i--)
            {
                if (lineViews[i] != null && uiService != null)
                {
                    uiService.Release(lineViews[i]);
                }
            }

            lineViews.Clear();
        }

        // R3: pick another saved rule set (proposed to the table) and restart when settings changed.
        private void BindHouseRules(RulesCardModel model)
        {
            RulesCardActions actions = model.Actions;
            bool canCycle = actions?.CycleRuleSet != null && model.RuleSetCount > 1;
            if (ruleSetRow != null) ruleSetRow.SetActive(canCycle);
            if (canCycle && previousSetButton != null) previousSetButton.onClick.AddListener(() => actions.CycleRuleSet(-1));
            if (canCycle && nextSetButton != null) nextSetButton.onClick.AddListener(() => actions.CycleRuleSet(1));
            bool pending = !string.IsNullOrEmpty(model.PendingText);
            if (pendingRow != null) pendingRow.SetActive(pending);
            if (pendingLabel != null) pendingLabel.text = model.PendingText;
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(pending && actions?.Restart != null);
                if (pending && actions?.Restart != null) restartButton.onClick.AddListener(() => actions.Restart());
            }
        }

        private void ToggleCollapsed()
        {
            collapsed = !collapsed;
            ApplyCollapsed();
        }

        // The folded state is kept across tables and rebuilds; it is a view setting, not table state.
        private void ApplyCollapsed()
        {
            body.SetActive(!collapsed);
            chevron.localRotation = Quaternion.Euler(0f, 0f, collapsed ? 180f : 0f);
            if (transform is RectTransform rect)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rect);
            }
        }
    }
}
