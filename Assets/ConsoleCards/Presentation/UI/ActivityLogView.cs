using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// "What happened" log (doc 25, F4): a foldable list of the latest table events, newest first, so nothing a
    /// player missed is lost. A tab (or L) opens and closes it. Layout and styling are authored on the Final UI
    /// prefab; the game only supplies the text when the log changes.
    /// </summary>
    public sealed class ActivityLogView : ReusableUiView
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleLabel;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text body;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private string collapsedText = "WHAT HAPPENED  (L)";
        [SerializeField] private string expandedText = "HIDE  (L)";

        private bool expanded;

        public bool IsExpanded => expanded;

        public void ValidateReferences()
        {
            if (toggleButton == null || toggleLabel == null || panel == null || body == null)
            {
                throw new InvalidOperationException("ActivityLogView requires its authored references.");
            }
        }

        protected override void OnAcquire()
        {
            ValidateReferences();
            toggleButton.onClick.RemoveListener(Toggle);
            toggleButton.onClick.AddListener(Toggle);
            SetExpanded(false);
        }

        /// <summary>Replaces the log text (newest first). Call when the log changes, not every frame.</summary>
        public void SetText(string text)
        {
            RequireAcquired();
            body.text = text ?? string.Empty;
            if (expanded && scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        public void SetExpanded(bool open)
        {
            expanded = open;
            panel.SetActive(open);
            toggleLabel.text = open ? expandedText : collapsedText;
            if (open && scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        public void Toggle() => SetExpanded(!expanded);

        public override void Unbind()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null
                && keyboard.lKey.wasPressedThisFrame
                && !keyboard.leftCtrlKey.isPressed
                && !keyboard.rightCtrlKey.isPressed)
            {
                Toggle();
            }
        }
    }
}
