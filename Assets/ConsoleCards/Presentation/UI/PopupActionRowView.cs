using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PopupActionRowView : ReusableUiView
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        // POP-2 (owner approved Popups v2, 2026-10-29): Delete asks again in place, on its own row (no dialog).
        [SerializeField] private Color confirmFill = new Color(0.478f, 0.118f, 0.086f, 1f);
        [SerializeField] private Color confirmText = new Color(1f, 0.890f, 0.875f, 1f);
        [SerializeField, Min(0.5f)] private float confirmSeconds = 3f;

        private PopupActionOption option;
        private bool bound;
        private bool armed;
        private float armedAt;
        private Color idleFill;
        private Color idleText;

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
            FitText.Shrink(label); // MP-P: a long label shrinks to fit its button instead of running past it
            label.text = option.Label;
            button.interactable = option.Enabled;
            this.option = option;
            bound = true;
            button.onClick.AddListener(Press);
        }

        private void Press()
        {
            if (!bound) return;
            if (!option.Label.StartsWith("Delete", StringComparison.Ordinal))
            {
                option.Selected.Invoke();
                return;
            }

            if (!armed)
            {
                armed = true;
                armedAt = Time.unscaledTime;
                if (button.targetGraphic != null)
                {
                    idleFill = button.targetGraphic.color;
                    button.targetGraphic.color = confirmFill;
                }

                idleText = label.color;
                label.color = confirmText;
                label.text = $"{option.Label}? Click again";
                return;
            }

            Disarm();
            option.Selected.Invoke();
        }

        private void Disarm()
        {
            if (!armed) return;
            armed = false;
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = idleFill;
            if (label != null)
            {
                label.color = idleText;
                if (bound) label.text = option.Label;
            }
        }

        private void Update()
        {
            if (armed && Time.unscaledTime - armedAt > confirmSeconds) Disarm();
        }

        public override void Unbind()
        {
            Disarm();
            bound = false;
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
