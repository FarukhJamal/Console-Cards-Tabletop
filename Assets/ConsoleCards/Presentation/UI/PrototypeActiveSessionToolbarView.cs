using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeActiveSessionToolbarView : MonoBehaviour
    {
        [SerializeField] private Text sessionTitleLabel;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button returnButton;

        public void ValidateReferences()
        {
            if (sessionTitleLabel == null || resetButton == null || returnButton == null)
            {
                throw new InvalidOperationException(
                    "PrototypeActiveSessionToolbarView requires its title, Reset Button, and Return Button references.");
            }
        }

        public void Bind(string sessionTitle, Action resetSession, Action returnToSessionEntry)
        {
            if (string.IsNullOrWhiteSpace(sessionTitle))
            {
                throw new ArgumentException("The active-session toolbar requires a title.", nameof(sessionTitle));
            }

            if (resetSession == null)
            {
                throw new ArgumentNullException(nameof(resetSession));
            }

            if (returnToSessionEntry == null)
            {
                throw new ArgumentNullException(nameof(returnToSessionEntry));
            }

            ValidateReferences();
            sessionTitleLabel.text = sessionTitle;
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(resetSession.Invoke);
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(returnToSessionEntry.Invoke);
        }

        public void Unbind()
        {
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
            }

            if (returnButton != null)
            {
                returnButton.onClick.RemoveAllListeners();
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
