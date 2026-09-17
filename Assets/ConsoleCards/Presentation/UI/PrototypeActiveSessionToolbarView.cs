using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeActiveSessionToolbarView : MonoBehaviour
    {
        [SerializeField] private Text sessionTitleLabel;
        [SerializeField] private Button undoButton;
        [SerializeField] private Text undoButtonLabel;
        [SerializeField] private Button redoButton;
        [SerializeField] private Text redoButtonLabel;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button returnButton;

        public void ValidateReferences()
        {
            if (sessionTitleLabel == null || undoButton == null || undoButtonLabel == null
                || redoButton == null || redoButtonLabel == null
                || resetButton == null || returnButton == null)
            {
                throw new InvalidOperationException(
                    "PrototypeActiveSessionToolbarView requires its title, Undo, Redo, Reset, and Games / Templates references.");
            }
        }

        public void Bind(
            string sessionTitle,
            Action undo,
            Action redo,
            Action resetSession,
            Action openGameTemplates)
        {
            if (string.IsNullOrWhiteSpace(sessionTitle))
            {
                throw new ArgumentException("The active-session toolbar requires a title.", nameof(sessionTitle));
            }

            if (resetSession == null)
            {
                throw new ArgumentNullException(nameof(resetSession));
            }

            if (undo == null)
            {
                throw new ArgumentNullException(nameof(undo));
            }

            if (redo == null)
            {
                throw new ArgumentNullException(nameof(redo));
            }

            if (openGameTemplates == null)
            {
                throw new ArgumentNullException(nameof(openGameTemplates));
            }

            ValidateReferences();
            sessionTitleLabel.text = sessionTitle;
            undoButton.onClick.RemoveAllListeners();
            undoButton.onClick.AddListener(undo.Invoke);
            redoButton.onClick.RemoveAllListeners();
            redoButton.onClick.AddListener(redo.Invoke);
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(resetSession.Invoke);
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(openGameTemplates.Invoke);
        }

        public void SetUndoState(bool enabled, string label)
        {
            ValidateReferences();
            undoButton.interactable = enabled;
            undoButtonLabel.text = string.IsNullOrWhiteSpace(label) ? "Undo" : label;
        }

        public void SetRedoState(bool enabled, string label)
        {
            ValidateReferences();
            redoButton.interactable = enabled;
            redoButtonLabel.text = string.IsNullOrWhiteSpace(label) ? "Redo" : label;
        }

        public void Unbind()
        {
            if (undoButton != null)
            {
                undoButton.onClick.RemoveAllListeners();
                undoButton.interactable = false;
            }

            if (redoButton != null)
            {
                redoButton.onClick.RemoveAllListeners();
                redoButton.interactable = false;
            }

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
