using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PopupActionRowView : ReusableUiView
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        public void ValidateReferences()
        {
            if (button == null || label == null)
            {
                throw new InvalidOperationException(
                    "PopupActionRowView requires its authored Button and label references.");
            }
        }

        public void Bind(PopupActionOption option)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            label.text = option.Label;
            button.interactable = option.Enabled;
            button.onClick.AddListener(option.Selected.Invoke);
        }

        public override void Unbind()
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }

            if (label != null)
            {
                label.text = string.Empty;
            }
        }
    }

    public readonly struct PopupActionOption
    {
        public PopupActionOption(string label, bool enabled, Action selected)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("A popup action requires a label.", nameof(label));
            }

            Label = label;
            Enabled = enabled;
            Selected = selected ?? throw new ArgumentNullException(nameof(selected));
        }

        public string Label { get; }

        public bool Enabled { get; }

        public Action Selected { get; }
    }
}
