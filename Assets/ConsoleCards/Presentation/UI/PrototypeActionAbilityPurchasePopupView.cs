using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeActionAbilityPurchaseOption
    {
        public PrototypeActionAbilityPurchaseOption(
            string cardDefinitionStableId,
            string displayName,
            string description,
            string inputCost,
            bool canAfford,
            string affordabilityMessage)
        {
            if (string.IsNullOrWhiteSpace(cardDefinitionStableId))
                throw new ArgumentException("A purchase option requires a Card Definition ID.", nameof(cardDefinitionStableId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A purchase option requires a display name.", nameof(displayName));

            CardDefinitionStableId = cardDefinitionStableId;
            DisplayName = displayName;
            Description = description ?? string.Empty;
            InputCost = inputCost ?? string.Empty;
            CanAfford = canAfford;
            AffordabilityMessage = affordabilityMessage ?? string.Empty;
        }

        public string CardDefinitionStableId { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public string InputCost { get; }

        public bool CanAfford { get; }

        public string AffordabilityMessage { get; }
    }

    /// <summary>
    /// Prefab-authored catalog surface. Game-specific Presentation supplies authored definitions,
    /// affordability, and the authoritative purchase callback.
    /// </summary>
    public sealed class PrototypeActionAbilityPurchasePopupView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform catalogRowsRoot;
        [SerializeField] private ScrollRect catalogScrollRect;
        [SerializeField] private Text cardNameLabel;
        [SerializeField] private Text descriptionLabel;
        [SerializeField] private Text inputCostLabel;
        [SerializeField] private Text affordabilityLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private Text buyButtonLabel;
        [SerializeField] private Button closeButton;

        private readonly List<PrototypePopupActionRowView> catalogRows =
            new List<PrototypePopupActionRowView>();
        private IReadOnlyList<PrototypeActionAbilityPurchaseOption> options;
        private PrototypeActionAbilityPurchaseOption selectedOption;
        private IRuntimeUiService uiService;
        private Action<string> purchase;
        private Action dismiss;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null
                || panel == null
                || catalogRowsRoot == null
                || catalogScrollRect == null
                || cardNameLabel == null
                || descriptionLabel == null
                || inputCostLabel == null
                || affordabilityLabel == null
                || statusLabel == null
                || buyButton == null
                || buyButtonLabel == null
                || closeButton == null
                || uiService == null)
            {
                throw new InvalidOperationException(
                    "PrototypeActionAbilityPurchasePopupView requires its authored catalog, details, controls, and runtime UI service.");
            }
        }

        public void Bind(
            IReadOnlyList<PrototypeActionAbilityPurchaseOption> purchaseOptions,
            Action<string> purchaseSelected,
            Action dismissPopup,
            string statusMessage = "")
        {
            RequireAcquired();
            if (purchaseOptions == null) throw new ArgumentNullException(nameof(purchaseOptions));

            ValidateReferences();
            Unbind();
            options = purchaseOptions;
            purchase = purchaseSelected ?? throw new ArgumentNullException(nameof(purchaseSelected));
            dismiss = dismissPopup ?? throw new ArgumentNullException(nameof(dismissPopup));
            BindButton(dismissOverlayButton, dismissPopup);
            BindButton(closeButton, dismissPopup);
            BindButton(buyButton, BuySelected);
            buyButtonLabel.text = "Buy";
            BuildCatalogRows();
            statusLabel.text = statusMessage ?? string.Empty;
            panel.SetActive(true);

            if (options.Count > 0)
            {
                Select(options[0]);
            }
            else
            {
                ClearSelection("No authored Cards with a non-empty input cost are available.");
            }

            catalogScrollRect.verticalNormalizedPosition = 1f;
            ClearSelectedUiObject();
        }

        public override void Hide()
        {
            if (panel != null) panel.SetActive(false);
            base.Hide();
            ClearSelectedUiObject();
        }

        public void SetStatus(string message)
        {
            RequireAcquired();
            statusLabel.text = message ?? string.Empty;
        }

        public override void Unbind()
        {
            for (int i = catalogRows.Count - 1; i >= 0; i--)
            {
                PrototypePopupActionRowView row = catalogRows[i];
                if (row != null && uiService != null) uiService.Release(row);
            }

            catalogRows.Clear();
            RemoveListeners(dismissOverlayButton);
            RemoveListeners(buyButton);
            RemoveListeners(closeButton);
            options = null;
            selectedOption = null;
            purchase = null;
            dismiss = null;
            if (cardNameLabel != null) cardNameLabel.text = string.Empty;
            if (descriptionLabel != null) descriptionLabel.text = string.Empty;
            if (inputCostLabel != null) inputCostLabel.text = string.Empty;
            if (affordabilityLabel != null) affordabilityLabel.text = string.Empty;
            if (statusLabel != null) statusLabel.text = string.Empty;
            if (buyButtonLabel != null) buyButtonLabel.text = string.Empty;
        }

        private void BuildCatalogRows()
        {
            for (int i = 0; i < options.Count; i++)
            {
                PrototypeActionAbilityPurchaseOption option = options[i];
                PrototypePopupActionRowView row =
                    uiService.AcquirePooled<PrototypePopupActionRowView>(
                        PrototypeUiPrefabIds.PopupActionRow,
                        catalogRowsRoot);
                row.Bind(new PrototypePopupActionOption(
                    option.DisplayName,
                    true,
                    () => Select(option)));
                row.Show();
                catalogRows.Add(row);
            }
        }

        private void Select(PrototypeActionAbilityPurchaseOption option)
        {
            selectedOption = option ?? throw new ArgumentNullException(nameof(option));
            cardNameLabel.text = option.DisplayName;
            descriptionLabel.text = option.Description;
            inputCostLabel.text = $"Cost: {option.InputCost}";
            affordabilityLabel.text = option.AffordabilityMessage;
            // Affordability is informative; the authoritative service remains the final validator.
            buyButton.interactable = true;
        }

        private void ClearSelection(string message)
        {
            selectedOption = null;
            cardNameLabel.text = "No Purchasable Cards";
            descriptionLabel.text = message ?? string.Empty;
            inputCostLabel.text = string.Empty;
            affordabilityLabel.text = string.Empty;
            buyButton.interactable = false;
        }

        private void BuySelected()
        {
            if (selectedOption == null) return;
            purchase?.Invoke(selectedOption.CardDefinitionStableId);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                dismiss?.Invoke();
            }
        }

        private static void BindButton(Button button, Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback.Invoke);
        }

        private static void RemoveListeners(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void ClearSelectedUiObject()
        {
            EventSystem current = EventSystem.current;
            if (current != null
                && current.currentSelectedGameObject != null
                && current.currentSelectedGameObject.transform.IsChildOf(transform))
            {
                current.SetSelectedGameObject(null);
            }
        }
    }
}
