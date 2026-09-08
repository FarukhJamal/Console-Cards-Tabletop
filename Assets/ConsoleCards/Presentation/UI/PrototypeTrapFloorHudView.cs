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

    public sealed class PrototypeTrapFloorHudView : MonoBehaviour
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
        [SerializeField] private PrototypePopupActionRowView actionRowPrefab;

        private readonly List<PrototypePopupActionRowView> actionRows =
            new List<PrototypePopupActionRowView>();
        private float expandedHeight;

        private const float ObjectivePanelHeight = 116f;

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
                || actionRowPrefab == null)
            {
                throw new InvalidOperationException(
                    "PrototypeTrapFloorHudView requires its authored status, Floorfall, action, and row-prefab references.");
            }

            actionRowPrefab.ValidateReferences();
        }

        public void Show(
            PrototypeTrapFloorStatusModel status,
            PrototypeFloorfallStatusModel floorfall,
            IReadOnlyList<PrototypePopupActionOption> actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            ValidateReferences();
            gameObject.SetActive(true);
            SetCompact(false);
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
        }

        public void ShowObjective(string keyProgress, bool isWon)
        {
            ValidateReferences();
            gameObject.SetActive(true);
            SetCompact(true);
            roundLabel.gameObject.SetActive(true);
            roundLabel.text = keyProgress ?? string.Empty;
            phaseLabel.gameObject.SetActive(isWon);
            phaseLabel.text = isWon ? "VICTORY" : string.Empty;
            searchProgressLabel.gameObject.SetActive(false);
            detailLabel.gameObject.SetActive(false);
            containerCountsLabel.gameObject.SetActive(false);
            floorfallPanel.SetActive(false);
            actionsTitle.SetActive(false);
            actionsRoot.gameObject.SetActive(false);
            actionHelpLabel.gameObject.SetActive(false);
            UnbindActions();
        }

        public void Hide()
        {
            UnbindActions();
            gameObject.SetActive(false);
        }

        private void BindActions(IReadOnlyList<PrototypePopupActionOption> actions)
        {
            while (actionRows.Count < actions.Count)
            {
                PrototypePopupActionRowView row = Instantiate(actionRowPrefab, actionsRoot, false);
                row.name = actionRowPrefab.name;
                row.ValidateReferences();
                actionRows.Add(row);
            }

            for (int i = 0; i < actionRows.Count; i++)
            {
                PrototypePopupActionRowView row = actionRows[i];
                if (i < actions.Count)
                {
                    row.gameObject.SetActive(true);
                    row.Bind(actions[i]);
                }
                else
                {
                    row.Unbind();
                    row.gameObject.SetActive(false);
                }
            }
        }

        private void UnbindActions()
        {
            for (int i = 0; i < actionRows.Count; i++)
            {
                if (actionRows[i] != null)
                {
                    actionRows[i].Unbind();
                }
            }
        }

        private void SetCompact(bool compact)
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            if (expandedHeight <= 0f)
            {
                expandedHeight = rectTransform.sizeDelta.y;
            }

            Vector2 size = rectTransform.sizeDelta;
            size.y = compact ? ObjectivePanelHeight : expandedHeight;
            rectTransform.sizeDelta = size;
        }

        private void OnDestroy()
        {
            UnbindActions();
        }
    }
}
