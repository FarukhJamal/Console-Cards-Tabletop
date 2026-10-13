using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Announcement banner (doc 25, F1): one big moment at a time from a short queue, a quick pop and fade,
    /// click to skip, and an optional sound per kind. It never blocks the table: only the banner itself takes
    /// clicks. Layout, colours, timings and sounds are authored on the Final UI prefab.
    /// </summary>
    public sealed class FeedbackBannerView : ReusableUiView
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Image accent;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text subtitleLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private AudioSource audioSource;
        [Header("Timing")]
        [SerializeField, Min(0.2f)] private float showSeconds = 1.5f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.18f;
        [SerializeField, Range(0.5f, 1f)] private float popStartScale = 0.9f;
        [SerializeField, Range(1, 12)] private int queueCapacity = 6;
        [Header("Sounds (optional)")]
        [SerializeField] private AudioClip turnStartClip;
        [SerializeField] private AudioClip trapClip;
        [SerializeField] private AudioClip eliminatedClip;
        [SerializeField] private AudioClip rewardClip;
        [SerializeField] private AudioClip collapseClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private FeedbackBannerModel[] queue;
        private int head;
        private int count;
        private bool showing;
        private float shownAt;

        public void ValidateReferences()
        {
            if (group == null || panel == null || accent == null || titleLabel == null
                || subtitleLabel == null || skipButton == null)
            {
                throw new System.InvalidOperationException("FeedbackBannerView requires its authored references.");
            }
        }

        protected override void OnAcquire()
        {
            if (queue == null || queue.Length != queueCapacity) queue = new FeedbackBannerModel[queueCapacity];
            head = 0;
            count = 0;
            showing = false;
            skipButton.onClick.RemoveListener(Skip);
            skipButton.onClick.AddListener(Skip);
            SetVisible(0f, false);
        }

        public void Enqueue(FeedbackBannerModel model)
        {
            RequireAcquired();
            if (count == queue.Length)
            {
                head = (head + 1) % queue.Length; // drop the oldest waiting banner
                count--;
            }

            queue[(head + count) % queue.Length] = model;
            count++;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (!showing) ShowNext();
        }

        public void Skip()
        {
            if (showing) ShowNext();
        }

        public override void Unbind()
        {
            head = 0;
            count = 0;
            showing = false;
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
        }

        private void ShowNext()
        {
            if (count == 0)
            {
                showing = false;
                SetVisible(0f, false);
                gameObject.SetActive(false);
                return;
            }

            FeedbackBannerModel model = queue[head];
            head = (head + 1) % queue.Length;
            count--;
            titleLabel.text = model.Title;
            subtitleLabel.text = model.Subtitle;
            subtitleLabel.gameObject.SetActive(model.Subtitle.Length > 0);
            accent.color = model.Accent;
            showing = true;
            shownAt = Time.unscaledTime;
            SetVisible(0f, true);
            AudioClip clip = ClipFor(model.Kind);
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
        }

        private void Update()
        {
            if (!showing) return;
            float t = Time.unscaledTime - shownAt;
            if (t >= showSeconds)
            {
                ShowNext();
                return;
            }

            float fade = Mathf.Max(0.0001f, fadeSeconds);
            float alpha = Mathf.Min(Mathf.Clamp01(t / fade), Mathf.Clamp01((showSeconds - t) / fade));
            group.alpha = alpha;
            float pop = Mathf.Clamp01(t / (fade * 1.5f));
            float scale = Mathf.Lerp(popStartScale, 1f, 1f - ((1f - pop) * (1f - pop)));
            panel.localScale = new Vector3(scale, scale, 1f);
        }

        private void SetVisible(float alpha, bool clickable)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.blocksRaycasts = clickable;
            group.interactable = clickable;
        }

        private AudioClip ClipFor(FeedbackEventKind kind)
        {
            switch (kind)
            {
                case FeedbackEventKind.TurnStart:
                case FeedbackEventKind.RoundStart:
                    return turnStartClip;
                case FeedbackEventKind.Trap:
                    return trapClip;
                case FeedbackEventKind.Eliminated:
                    return eliminatedClip;
                case FeedbackEventKind.Reward:
                    return rewardClip;
                case FeedbackEventKind.Collapse:
                    return collapseClip;
                default:
                    return null;
            }
        }
    }
}
