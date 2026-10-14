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
        [Header("Fades (optional CanvasGroup; one is added to the root when missing)")]
        [SerializeField] private CanvasGroup group;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.3f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.5f;

        private Action confirm;
        private Action fadedOut;
        private float fadeFrom;
        private float fadeTo = 1f;
        private float fadeStarted;
        private float fadeSeconds;
        private bool fading;

        /// <summary>True while the cover is fully or partly on screen and not fading away.</summary>
        public bool IsCovering => IsAcquired && gameObject.activeSelf && fadeTo > 0f;

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
            confirmButton.interactable = true;
            fadedOut = null;
            StartFade(gameObject.activeSelf ? CurrentAlpha() : 0f, 1f, fadeInSeconds);
            if (handoverClip != null && audioSource != null) audioSource.PlayOneShot(handoverClip, volume);
        }

        /// <summary>Fades the cover away, then runs <paramref name="onFadedOut"/> (release it there).</summary>
        public void FadeOut(Action onFadedOut)
        {
            confirm = null;
            confirmButton.interactable = false;
            fadedOut = onFadedOut;
            StartFade(CurrentAlpha(), 0f, fadeOutSeconds);
        }

        public override void Unbind()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirm);
            confirm = null;
            fadedOut = null;
            fading = false;
            fadeTo = 1f;
        }

        protected override void OnAcquire()
        {
            if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
        }

        private void StartFade(float from, float to, float seconds)
        {
            fadeFrom = from;
            fadeTo = to;
            fadeSeconds = seconds;
            fadeStarted = Time.unscaledTime;
            fading = true;
            if (group != null)
            {
                group.alpha = from;
                group.blocksRaycasts = true; // the cover blocks the table until it is fully gone
            }
        }

        private float CurrentAlpha() => group != null ? group.alpha : 1f;

        private void Update()
        {
            if (fading && group != null)
            {
                float t = fadeSeconds <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - fadeStarted) / fadeSeconds);
                float eased = t * t * (3f - (2f * t));
                group.alpha = Mathf.Lerp(fadeFrom, fadeTo, eased);
                if (t >= 1f)
                {
                    fading = false;
                    if (fadeTo <= 0f)
                    {
                        group.blocksRaycasts = false;
                        Action done = fadedOut;
                        fadedOut = null;
                        done?.Invoke();
                        return;
                    }
                }
            }

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
