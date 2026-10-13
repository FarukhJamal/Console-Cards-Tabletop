using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Message stack (doc 25, F1): the last few messages, newest at the bottom, coloured by severity, each
    /// fading after a few seconds. The rows are authored in the Final UI prefab and reused; nothing is built
    /// at runtime.
    /// </summary>
    public sealed class FeedbackMessageStackView : ReusableUiView
    {
        [SerializeField] private GameObject[] rows = new GameObject[0];
        [SerializeField] private Image[] rowFills = new Image[0];
        [SerializeField] private Text[] rowLabels = new Text[0];
        [SerializeField] private CanvasGroup[] rowGroups = new CanvasGroup[0];
        [Header("Timing")]
        [SerializeField, Min(0.5f)] private float showSeconds = 4f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;
        [Header("Row colours")]
        [SerializeField] private Color infoFill = new Color(0.114f, 0.114f, 0.122f, 0.92f);
        [SerializeField] private Color warningFill = new Color(0.949f, 0.659f, 0.114f, 0.95f);
        [SerializeField] private Color lossFill = new Color(0.878f, 0.325f, 0.122f, 0.95f);
        [SerializeField] private Color gainFill = new Color(0.184f, 0.647f, 0.541f, 0.95f);
        [SerializeField] private Color infoText = new Color(0.992f, 0.973f, 0.902f, 1f);
        [SerializeField] private Color alertText = new Color(0.114f, 0.114f, 0.122f, 1f);

        private string[] texts;
        private FeedbackSeverity[] severities;
        private float[] shownAt;
        private int used;

        public void ValidateReferences()
        {
            if (rows == null || rows.Length == 0
                || rowFills.Length != rows.Length
                || rowLabels.Length != rows.Length
                || rowGroups.Length != rows.Length)
            {
                throw new System.InvalidOperationException("FeedbackMessageStackView requires matching authored rows.");
            }
        }

        protected override void OnAcquire()
        {
            int n = rows.Length;
            if (texts == null || texts.Length != n)
            {
                texts = new string[n];
                severities = new FeedbackSeverity[n];
                shownAt = new float[n];
            }

            used = 0;
            for (int i = 0; i < n; i++) rows[i].SetActive(false);
        }

        /// <summary>Adds a message as the newest row (row 0); older ones move up, the oldest drops off.</summary>
        public void Push(string message, FeedbackSeverity severity)
        {
            RequireAcquired();
            if (string.IsNullOrWhiteSpace(message)) return;
            int n = rows.Length;
            for (int i = Mathf.Min(used, n - 1); i > 0; i--)
            {
                texts[i] = texts[i - 1];
                severities[i] = severities[i - 1];
                shownAt[i] = shownAt[i - 1];
            }

            texts[0] = message;
            severities[0] = severity;
            shownAt[0] = Time.unscaledTime;
            used = Mathf.Min(used + 1, n);
            for (int i = 0; i < n; i++) ApplyRow(i);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public override void Unbind()
        {
            used = 0;
        }

        private void ApplyRow(int i)
        {
            bool active = i < used;
            if (rows[i].activeSelf != active) rows[i].SetActive(active);
            if (!active) return;
            rowLabels[i].text = texts[i];
            FeedbackSeverity severity = severities[i];
            rowFills[i].color = severity == FeedbackSeverity.Warning ? warningFill
                : severity == FeedbackSeverity.Loss ? lossFill
                : severity == FeedbackSeverity.Gain ? gainFill
                : infoFill;
            rowLabels[i].color = severity == FeedbackSeverity.Info ? infoText : alertText;
        }

        private void Update()
        {
            if (used == 0) return;
            float now = Time.unscaledTime;
            float fade = Mathf.Max(0.0001f, fadeSeconds);
            for (int i = used - 1; i >= 0; i--)
            {
                float left = showSeconds - (now - shownAt[i]);
                if (left <= 0f)
                {
                    used = i; // rows are newest-first, so everything from here up has expired
                    rows[i].SetActive(false);
                    continue;
                }

                rowGroups[i].alpha = Mathf.Clamp01(left / fade);
            }
        }
    }
}
