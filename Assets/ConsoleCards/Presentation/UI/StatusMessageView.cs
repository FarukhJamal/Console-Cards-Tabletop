using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class StatusMessageView : MonoBehaviour
    {
        [SerializeField] private Text messageLabel;

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
            gameObject.SetActive(hasMessage);
        }
    }
}
