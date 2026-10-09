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
                view.Bind(line);
                view.Show();
                lineViews.Add(view);
            }

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

            for (int i = lineViews.Count - 1; i >= 0; i--)
            {
                if (lineViews[i] != null && uiService != null)
                {
                    uiService.Release(lineViews[i]);
                }
            }

            lineViews.Clear();
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
