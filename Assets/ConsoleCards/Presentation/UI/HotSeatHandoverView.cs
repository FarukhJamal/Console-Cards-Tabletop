using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>What the hot-seat handover screen shows (doc 25, H2).</summary>
    public readonly struct HotSeatHandoverModel
    {
        public HotSeatHandoverModel(string title, string playerName, string summary, string confirmText, Color accent)
        {
            Title = title ?? string.Empty;
            PlayerName = playerName ?? string.Empty;
            Summary = summary ?? string.Empty;
            ConfirmText = string.IsNullOrEmpty(confirmText) ? "Show my hand" : confirmText;
            Accent = accent;
        }

        public string Title { get; }
        public string PlayerName { get; }
        public string Summary { get; }
        public string ConfirmText { get; }
        public Color Accent { get; }
    }

    /// <summary>
    /// Hot-seat handover (doc 25, H2): a full-screen cover that hides the table and every hand while the device
    /// passes to the next player. It names that player in their colour, lists what happened since they last had
    /// the screen, and shows their hand only when they confirm (button, Enter or Space). Layout, colours and the
    /// sound are authored on the Final UI prefab.
    /// </summary>
    public sealed class HotSeatHandoverView : ReusableUiView
    {
        [SerializeField] private Image cover;
        [SerializeField] private Image accentPlate;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text playerLabel;
        [SerializeField] private Text summaryLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmLabel;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip handoverClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private Action confirm;

        public void ValidateReferences()
        {
            if (cover == null || accentPlate == null || titleLabel == null || playerLabel == null
                || summaryLabel == null || confirmButton == null || confirmLabel == null)
            {
                throw new InvalidOperationException("HotSeatHandoverView requires its authored references.");
            }
        }

        public void Bind(HotSeatHandoverModel model, Action confirmAction)
        {
            RequireAcquired();
            ValidateReferences();
            confirm = confirmAction ?? throw new ArgumentNullException(nameof(confirmAction));
            titleLabel.text = model.Title;
            playerLabel.text = model.PlayerName;
            playerLabel.color = model.Accent;
            accentPlate.color = model.Accent;
            summaryLabel.text = model.Summary;
            summaryLabel.gameObject.SetActive(model.Summary.Length > 0);
            confirmLabel.text = model.ConfirmText;
            confirmButton.onClick.RemoveListener(HandleConfirm);
            confirmButton.onClick.AddListener(HandleConfirm);
            if (handoverClip != null && audioSource != null) audioSource.PlayOneShot(handoverClip, volume);
        }

        public override void Unbind()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirm);
            confirm = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (confirm != null
                && keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame))
            {
                HandleConfirm();
            }
        }

        private void HandleConfirm()
        {
            Action action = confirm;
            confirm = null; // one confirm per handover
            action?.Invoke();
        }
    }
}
