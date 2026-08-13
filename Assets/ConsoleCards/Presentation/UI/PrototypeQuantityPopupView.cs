using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeQuantityPopupView : MonoBehaviour
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text descriptionLabel;
        [SerializeField] private Text countLabel;
        [SerializeField] private Text rangeLabel;
        [SerializeField] private Button decrementButton;
        [SerializeField] private Button incrementButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmButtonLabel;
        [SerializeField] private Button cancelButton;

        private Action dismiss;

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null
                || panel == null
                || titleLabel == null
                || descriptionLabel == null
                || countLabel == null
                || rangeLabel == null
                || decrementButton == null
                || incrementButton == null
                || confirmButton == null
                || confirmButtonLabel == null
                || cancelButton == null)
            {
                throw new InvalidOperationException(
                    "PrototypeQuantityPopupView requires its authored overlay, panel, labels, and controls.");
            }
        }

        public void Show(
            string title,
            string description,
            string confirmText,
            int quantity,
            int minimum,
            int maximum,
            Action decrement,
            Action increment,
            Action confirm,
            Action dismissPopup)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Quantity popup requires a title.", nameof(title));
            }

            if (minimum < 1 || maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            ValidateReferences();
            Close();
            dismiss = dismissPopup ?? throw new ArgumentNullException(nameof(dismissPopup));
            titleLabel.text = title;
            descriptionLabel.text = description ?? string.Empty;
            confirmButtonLabel.text = string.IsNullOrWhiteSpace(confirmText) ? "Confirm" : confirmText;
            BindButton(dismissOverlayButton, dismissPopup);
            BindButton(decrementButton, decrement);
            BindButton(incrementButton, increment);
            BindButton(confirmButton, confirm);
            BindButton(cancelButton, dismissPopup);
            SetQuantity(quantity, minimum, maximum);
            panel.SetActive(true);
            gameObject.SetActive(true);
            ClearSelectedUiObject();
        }

        public void SetQuantity(int quantity, int minimum, int maximum)
        {
            int clamped = Mathf.Clamp(quantity, minimum, maximum);
            countLabel.text = clamped.ToString();
            rangeLabel.text = $"Allowed: {minimum} - {maximum}";
            decrementButton.interactable = clamped > minimum;
            incrementButton.interactable = clamped < maximum;
            confirmButton.interactable = clamped >= minimum && clamped <= maximum;
        }

        public void Close()
        {
            RemoveListeners(dismissOverlayButton);
            RemoveListeners(decrementButton);
            RemoveListeners(incrementButton);
            RemoveListeners(confirmButton);
            RemoveListeners(cancelButton);
            dismiss = null;
            if (panel != null)
            {
                panel.SetActive(false);
            }

            gameObject.SetActive(false);
            ClearSelectedUiObject();
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
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback.Invoke);
        }

        private static void RemoveListeners(Button button)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
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

        private void OnDestroy()
        {
            RemoveListeners(dismissOverlayButton);
            RemoveListeners(decrementButton);
            RemoveListeners(incrementButton);
            RemoveListeners(confirmButton);
            RemoveListeners(cancelButton);
            dismiss = null;
        }
    }
}
