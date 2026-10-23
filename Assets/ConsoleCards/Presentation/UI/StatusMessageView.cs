using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class StatusMessageView : MonoBehaviour
    {
        [SerializeField] private Text messageLabel;
        [Tooltip("V5 (owner, 2026-10-24): the status line is not part of the table screen; on only for debugging.")]
        [SerializeField] private bool showOnTable;

        public void ValidateReferences()
        {
            if (messageLabel == null)
            {
                throw new InvalidOperationException("StatusMessageView requires a message label.");
            }
        }

        public void SetMessage(string message)
        {
            ValidateReferences();
            bool hasMessage = !string.IsNullOrWhiteSpace(message);
            messageLabel.text = hasMessage ? message : string.Empty;
            gameObject.SetActive(hasMessage && showOnTable);
        }
    }
}
