using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public readonly struct PrototypeTrapFloorStatusModel
    {
        public PrototypeTrapFloorStatusModel(
            string round,
            string phase,
            string searchProgress,
            string detail,
            string containerCounts,
            string actionHelp)
        {
            Round = round ?? string.Empty;
            Phase = phase ?? string.Empty;
            SearchProgress = searchProgress ?? string.Empty;
            Detail = detail ?? string.Empty;
            ContainerCounts = containerCounts ?? string.Empty;
            ActionHelp = actionHelp ?? string.Empty;
        }

        public string Round { get; }

        public string Phase { get; }

        public string SearchProgress { get; }

        public string Detail { get; }

        public string ContainerCounts { get; }

        public string ActionHelp { get; }
    }

    public readonly struct PrototypeFloorfallStatusModel
    {
        public PrototypeFloorfallStatusModel(
            bool visible,
            string dice,
            string coordinate,
            string target)
        {
            Visible = visible;
            Dice = dice ?? string.Empty;
            Coordinate = coordinate ?? string.Empty;
            Target = target ?? string.Empty;
        }

        public bool Visible { get; }

        public string Dice { get; }

        public string Coordinate { get; }

        public string Target { get; }
    }

    public sealed class PrototypeTrapFloorHudView : ReusableUiView
    {
        [SerializeField] private Text roundLabel;
        [SerializeField] private Text phaseLabel;
        [SerializeField] private Text searchProgressLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Text containerCountsLabel;
        [SerializeField] private GameObject floorfallPanel;
        [SerializeField] private Text floorfallDiceLabel;
        [SerializeField] private Text floorfallCoordinateLabel;
        [SerializeField] private Text floorfallTargetLabel;
        [SerializeField] private Transform actionsRoot;
        [SerializeField] private GameObject actionsTitle;
        [SerializeField] private Text actionHelpLabel;

        private readonly List<PrototypePopupActionRowView> actionRows =
            new List<PrototypePopupActionRowView>();
        private IRuntimeUiService uiManager;

        public void Initialize(IRuntimeUiService manager)
        {
            uiManager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void ValidateReferences()
        {
            if (roundLabel == null
                || phaseLabel == null
                || searchProgressLabel == null
                || detailLabel == null
                || containerCountsLabel == null
                || floorfallPanel == null
                || floorfallDiceLabel == null
                || floorfallCoordinateLabel == null
                || floorfallTargetLabel == null
                || actionsRoot == null
                || actionsTitle == null
                || actionHelpLabel == null
                || uiManager == null)
            {
                throw new InvalidOperationException(
                    "PrototypeTrapFloorHudView requires its authored status, Floorfall, action references, and runtime UI manager.");
            }
        }

        public void Show(
            PrototypeTrapFloorStatusModel status,
            PrototypeFloorfallStatusModel floorfall,
            IReadOnlyList<PrototypePopupActionOption> actions)
        {
            RequireAcquired();
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            ValidateReferences();
            roundLabel.text = status.Round;
            phaseLabel.text = status.Phase;
            searchProgressLabel.text = status.SearchProgress;
            detailLabel.text = status.Detail;
            containerCountsLabel.text = status.ContainerCounts;
            roundLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.Round));
            phaseLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.Phase));
            searchProgressLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.SearchProgress));
            detailLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.Detail));
            containerCountsLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.ContainerCounts));
            actionHelpLabel.text = status.ActionHelp;
            actionHelpLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.ActionHelp));

            floorfallPanel.SetActive(floorfall.Visible);
            floorfallDiceLabel.text = floorfall.Dice;
            floorfallCoordinateLabel.text = floorfall.Coordinate;
            floorfallTargetLabel.text = floorfall.Target;
            actionsTitle.SetActive(actions.Count > 0);
            actionsRoot.gameObject.SetActive(actions.Count > 0);
            BindActions(actions);
            base.Show();
        }

        public void ShowObjective(
            string keyProgress,
            string collapseStatus,
            bool isWon,
            IReadOnlyList<PrototypePopupActionOption> actions)
        {
            RequireAcquired();
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            ValidateReferences();
            roundLabel.gameObject.SetActive(true);
            roundLabel.text = keyProgress ?? string.Empty;
            phaseLabel.gameObject.SetActive(isWon);
            phaseLabel.text = isWon ? "VICTORY" : string.Empty;
            searchProgressLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(collapseStatus));
            searchProgressLabel.text = collapseStatus ?? string.Empty;
            detailLabel.gameObject.SetActive(false);
            containerCountsLabel.gameObject.SetActive(false);
            floorfallPanel.SetActive(false);
            actionsTitle.SetActive(actions.Count > 0);
            actionsRoot.gameObject.SetActive(actions.Count > 0);
            actionHelpLabel.gameObject.SetActive(false);
            BindActions(actions);
            base.Show();
        }

        public override void Unbind()
        {
            UnbindActions();
            if (roundLabel != null) roundLabel.text = string.Empty;
            if (phaseLabel != null) phaseLabel.text = string.Empty;
            if (searchProgressLabel != null) searchProgressLabel.text = string.Empty;
            if (detailLabel != null) detailLabel.text = string.Empty;
            if (containerCountsLabel != null) containerCountsLabel.text = string.Empty;
            if (actionHelpLabel != null) actionHelpLabel.text = string.Empty;
            if (floorfallDiceLabel != null) floorfallDiceLabel.text = string.Empty;
            if (floorfallCoordinateLabel != null) floorfallCoordinateLabel.text = string.Empty;
            if (floorfallTargetLabel != null) floorfallTargetLabel.text = string.Empty;
        }

        private void BindActions(IReadOnlyList<PrototypePopupActionOption> actions)
        {
            while (actionRows.Count < actions.Count)
            {
                PrototypePopupActionRowView row =
                    uiManager.AcquirePooled<PrototypePopupActionRowView>(
                        PrototypeUiPrefabIds.PopupActionRow,
                        actionsRoot);
                row.ValidateReferences();
                actionRows.Add(row);
            }

            for (int i = 0; i < actionRows.Count; i++)
            {
                PrototypePopupActionRowView row = actionRows[i];
                if (i < actions.Count)
                {
                    row.Bind(actions[i]);
                    row.Show();
                }
                else
                {
                    uiManager.Release(row);
                    actionRows.RemoveAt(i);
                    i--;
                }
            }
        }

        private void UnbindActions()
        {
            for (int i = actionRows.Count - 1; i >= 0; i--)
            {
                if (actionRows[i] != null)
                {
                    uiManager.Release(actionRows[i]);
                }
            }

            actionRows.Clear();
        }

    }
}
