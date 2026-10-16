using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// In-simulator browser for replacing the current table with an Empty Table or a registered Game Template.
    /// </summary>
    public sealed class GameTemplatesPanelView : ReusableUiView
    {
        [SerializeField] private Button clearTableButton;
        [SerializeField] private Transform templateOptionsRoot;
        [SerializeField] private Text errorLabel;

        private readonly List<SessionTemplateButtonView> optionViews =
            new List<SessionTemplateButtonView>();
        private IRuntimeUiService uiManager;

        public void Initialize(IRuntimeUiService manager)
        {
            uiManager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void ValidateReferences()
        {
            if (clearTableButton == null)
            {
                throw new InvalidOperationException(
                    "GameTemplatesPanelView requires a Clear Table Button.");
            }

            if (templateOptionsRoot == null)
            {
                throw new InvalidOperationException(
                    "GameTemplatesPanelView requires a template-options root.");
            }

            if (uiManager == null)
            {
                throw new InvalidOperationException(
                    "GameTemplatesPanelView requires the runtime UI manager.");
            }

            if (errorLabel == null)
            {
                throw new InvalidOperationException(
                    "GameTemplatesPanelView requires an error label.");
            }
        }

        public void Bind(
            Action clearTable,
            IReadOnlyList<GameTemplateOption> templateOptions,
            string errorMessage)
        {
            RequireAcquired();
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
                GameTemplateOption option = templateOptions[i];
                SessionTemplateButtonView optionView =
                    uiManager.AcquirePooled<SessionTemplateButtonView>(
                        UiPrefabIds.GameTemplateRow,
                        templateOptionsRoot);
                optionView.name = $"GameTemplate_{i + 1}";
                optionView.Bind(option.DisplayName, option.Selected);
                optionView.Show();
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

        public override void Unbind()
        {
            if (clearTableButton != null)
            {
                clearTableButton.onClick.RemoveAllListeners();
            }

            for (int i = 0; i < optionViews.Count; i++)
            {
                SessionTemplateButtonView optionView = optionViews[i];
                if (optionView == null)
                {
                    continue;
                }

                uiManager.Release(optionView);
            }

            optionViews.Clear();
            SetError(string.Empty);
        }
    }

    public readonly struct GameTemplateOption
    {
        public GameTemplateOption(string displayName, Action selected)
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
}
