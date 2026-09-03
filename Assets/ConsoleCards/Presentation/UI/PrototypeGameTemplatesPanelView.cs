using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public readonly struct PrototypeGameTemplateOption
    {
        public PrototypeGameTemplateOption(string displayName, Action selected)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A Game Template option requires a display name.",
                    nameof(displayName));
            }

            DisplayName = displayName;
            Selected = selected ?? throw new ArgumentNullException(nameof(selected));
        }

        public string DisplayName { get; }

        public Action Selected { get; }
    }

    /// <summary>
    /// In-simulator browser for replacing the current table with an Empty Table or a registered Game Template.
    /// </summary>
    public sealed class PrototypeGameTemplatesPanelView : MonoBehaviour
    {
        [SerializeField] private Button clearTableButton;
        [SerializeField] private Transform templateOptionsRoot;
        [SerializeField] private PrototypeSessionTemplateButtonView templateOptionPrefab;
        [SerializeField] private Text errorLabel;

        private readonly List<PrototypeSessionTemplateButtonView> optionViews =
            new List<PrototypeSessionTemplateButtonView>();

        public void ValidateReferences()
        {
            if (clearTableButton == null)
            {
                throw new InvalidOperationException(
                    "PrototypeGameTemplatesPanelView requires a Clear Table Button.");
            }

            if (templateOptionsRoot == null)
            {
                throw new InvalidOperationException(
                    "PrototypeGameTemplatesPanelView requires a template-options root.");
            }

            if (templateOptionPrefab == null || templateOptionPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "PrototypeGameTemplatesPanelView requires an authored template-option prefab asset.");
            }

            templateOptionPrefab.ValidateReferences();
            if (errorLabel == null)
            {
                throw new InvalidOperationException(
                    "PrototypeGameTemplatesPanelView requires an error label.");
            }
        }

        public void Bind(
            Action clearTable,
            IReadOnlyList<PrototypeGameTemplateOption> templateOptions,
            string errorMessage)
        {
            if (clearTable == null)
            {
                throw new ArgumentNullException(nameof(clearTable));
            }

            if (templateOptions == null)
            {
                throw new ArgumentNullException(nameof(templateOptions));
            }

            ValidateReferences();
            Unbind();
            clearTableButton.onClick.AddListener(clearTable.Invoke);
            for (int i = 0; i < templateOptions.Count; i++)
            {
                PrototypeGameTemplateOption option = templateOptions[i];
                PrototypeSessionTemplateButtonView optionView = Instantiate(
                    templateOptionPrefab,
                    templateOptionsRoot,
                    false);
                optionView.name = $"GameTemplate_{i + 1}";
                optionView.Bind(option.DisplayName, option.Selected);
                optionViews.Add(optionView);
            }

            SetError(errorMessage);
        }

        public void SetError(string errorMessage)
        {
            if (errorLabel == null)
            {
                return;
            }

            bool hasError = !string.IsNullOrWhiteSpace(errorMessage);
            errorLabel.text = hasError ? errorMessage : string.Empty;
            errorLabel.gameObject.SetActive(hasError);
        }

        public void Unbind()
        {
            if (clearTableButton != null)
            {
                clearTableButton.onClick.RemoveAllListeners();
            }

            for (int i = 0; i < optionViews.Count; i++)
            {
                PrototypeSessionTemplateButtonView optionView = optionViews[i];
                if (optionView == null)
                {
                    continue;
                }

                optionView.Unbind();
                optionView.gameObject.SetActive(false);
                Destroy(optionView.gameObject);
            }

            optionViews.Clear();
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
