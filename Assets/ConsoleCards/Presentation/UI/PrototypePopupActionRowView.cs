using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public readonly struct PrototypePopupActionOption
    {
        public PrototypePopupActionOption(string label, bool enabled, Action selected)
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

    public sealed class PrototypePopupActionRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        public void ValidateReferences()
        {
            if (button == null || label == null)
            {
                throw new InvalidOperationException(
                    "PrototypePopupActionRowView requires its authored Button and label references.");
            }
        }

        public void Bind(PrototypePopupActionOption option)
        {
            ValidateReferences();
            Unbind();
            label.text = option.Label;
            button.interactable = option.Enabled;
            button.onClick.AddListener(option.Selected.Invoke);
        }

        public void Unbind()
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
