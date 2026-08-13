using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeSessionTemplateButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        public void ValidateReferences()
        {
            if (button == null)
            {
                throw new InvalidOperationException(
                    "PrototypeSessionTemplateButtonView requires a Button reference.");
            }

            if (label == null)
            {
                throw new InvalidOperationException(
                    "PrototypeSessionTemplateButtonView requires a label reference.");
            }
        }

        public void Bind(string displayName, Action selected)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A Session Entry option requires a display name.", nameof(displayName));
            }

            if (selected == null)
            {
                throw new ArgumentNullException(nameof(selected));
            }

            ValidateReferences();
            label.text = displayName;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(selected.Invoke);
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
