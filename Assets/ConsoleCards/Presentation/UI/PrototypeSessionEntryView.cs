using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public readonly struct PrototypeSessionTemplateOption
    {
        public PrototypeSessionTemplateOption(string displayName, Action selected)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A Session Entry option requires a display name.", nameof(displayName));
            }

            DisplayName = displayName;
            Selected = selected ?? throw new ArgumentNullException(nameof(selected));
        }

        public string DisplayName { get; }

        public Action Selected { get; }
    }

    public sealed class PrototypeSessionEntryView : MonoBehaviour
    {
        [SerializeField] private Button emptyTableButton;
        [SerializeField] private Transform templateOptionsRoot;
        [SerializeField] private PrototypeSessionTemplateButtonView templateOptionPrefab;
        [SerializeField] private Text errorLabel;

        private readonly List<PrototypeSessionTemplateButtonView> optionViews =
            new List<PrototypeSessionTemplateButtonView>();

        public void ValidateReferences()
        {
            if (emptyTableButton == null)
            {
                throw new InvalidOperationException("PrototypeSessionEntryView requires an Empty Table Button.");
            }

            if (templateOptionsRoot == null)
            {
                throw new InvalidOperationException("PrototypeSessionEntryView requires a template-options root.");
            }

            if (templateOptionPrefab == null || templateOptionPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "PrototypeSessionEntryView requires an authored template-option prefab asset.");
            }

            templateOptionPrefab.ValidateReferences();
            if (errorLabel == null)
            {
                throw new InvalidOperationException("PrototypeSessionEntryView requires an error label.");
            }
        }

        public void Bind(
            Action selectEmptyTable,
            IReadOnlyList<PrototypeSessionTemplateOption> templateOptions,
            string errorMessage)
        {
            if (selectEmptyTable == null)
            {
                throw new ArgumentNullException(nameof(selectEmptyTable));
            }

            if (templateOptions == null)
            {
                throw new ArgumentNullException(nameof(templateOptions));
            }

            ValidateReferences();
            emptyTableButton.onClick.RemoveAllListeners();
            emptyTableButton.onClick.AddListener(selectEmptyTable.Invoke);
            RebuildTemplateOptions(templateOptions);
            SetError(errorMessage);
        }

        public void SetError(string errorMessage)
        {
            bool hasError = !string.IsNullOrWhiteSpace(errorMessage);
            errorLabel.text = hasError
                ? $"Session could not start: {errorMessage}"
                : string.Empty;
            errorLabel.gameObject.SetActive(hasError);
        }

        public void Unbind()
        {
            if (emptyTableButton != null)
            {
                emptyTableButton.onClick.RemoveAllListeners();
            }

            ClearTemplateOptions();
        }

        private void RebuildTemplateOptions(IReadOnlyList<PrototypeSessionTemplateOption> templateOptions)
        {
            ClearTemplateOptions();
            for (int i = 0; i < templateOptions.Count; i++)
            {
                PrototypeSessionTemplateOption option = templateOptions[i];
                PrototypeSessionTemplateButtonView optionView = Instantiate(
                    templateOptionPrefab,
                    templateOptionsRoot,
                    false);
                optionView.Bind(option.DisplayName, option.Selected);
                optionViews.Add(optionView);
            }
        }

        private void ClearTemplateOptions()
        {
            for (int i = optionViews.Count - 1; i >= 0; i--)
            {
                PrototypeSessionTemplateButtonView optionView = optionViews[i];
                if (optionView == null)
                {
                    continue;
                }

                optionView.Unbind();
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(optionView.gameObject);
                }
                else
                {
                    DestroyImmediate(optionView.gameObject);
                }
            }

            optionViews.Clear();
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
