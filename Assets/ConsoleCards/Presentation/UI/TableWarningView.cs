using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// POP-2 (owner, 2026-10-29: "warnings are usually in yellow … the undo button is too big"): one short yellow
    /// line at the top of the table, the head in bold, with the fix inside it as a small outlined button (UNDO) when
    /// the warning asks for Undo. It never blocks the table and fades by itself; ✕ closes it at once. A newer
    /// warning replaces it. The full line is still in the Log. Authored prefab in Final UI/Notices.
    /// </summary>
    public sealed class TableWarningView : ReusableUiView
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text messageLabel;
        [SerializeField] private Button fixButton;
        [SerializeField] private Text fixLabel;
        [SerializeField] private Button closeButton;
        [Header("Timing")]
        [SerializeField, Min(1f)] private float showSeconds = 6f;
        [Tooltip("A warning with a fix (UNDO) stays longer.")]
        [SerializeField, Min(1f)] private float showSecondsWithFix = 12f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.4f;
        [Header("Colours")]
        [SerializeField] private Color headColour = new Color(1f, 0.788f, 0.231f, 1f);

        private Action fix;
        private float shownAt;
        private float seconds;
        private bool showing;
        private string headHex;

        public void ValidateReferences()
        {
            if (group == null || messageLabel == null || fixButton == null || fixLabel == null || closeButton == null)
            {
                throw new InvalidOperationException(
                    "TableWarningView is missing an authored part. Run Console Cards > UI > Create Missing Table Notices (POP-2).");
            }
        }

        protected override void OnAcquire()
        {
            ValidateReferences();
            closeButton.onClick.RemoveListener(Dismiss);
            closeButton.onClick.AddListener(Dismiss);
            fixButton.onClick.RemoveListener(PressFix);
            fixButton.onClick.AddListener(PressFix);
            showing = false;
            group.alpha = 0f;
        }

        /// <summary>Shows a warning; with <paramref name="fixAction"/>, the small button runs it (Undo).</summary>
        public void ShowWarning(string message, string fixText, Action fixAction)
        {
            RequireAcquired();
            ValidateReferences();
            fix = fixAction;
            TableNoticeText.SplitWarning(message, fixAction != null, out string head, out string rest);
            if (headHex == null) headHex = ColorUtility.ToHtmlStringRGB(headColour);
            messageLabel.text = rest.Length > 0
                ? $"<b><color=#{headHex}>{head}</color></b>  ·  {rest}"
                : $"<b><color=#{headHex}>{head}</color></b>";
            fixButton.gameObject.SetActive(fixAction != null);
            fixLabel.text = string.IsNullOrWhiteSpace(fixText) ? "UNDO" : fixText.ToUpperInvariant();
            seconds = fixAction != null ? showSecondsWithFix : showSeconds;
            shownAt = Time.unscaledTime;
            showing = true;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Dismiss()
        {
            showing = false;
            fix = null;
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }

            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public override void Unbind()
        {
            fix = null;
            showing = false;
        }

        private void PressFix()
        {
            Action run = fix;
            Dismiss();
            run?.Invoke();
        }

        private void Update()
        {
            if (!showing) return;
            float left = seconds - (Time.unscaledTime - shownAt);
            if (left <= 0f)
            {
                Dismiss();
                return;
            }

            group.alpha = Mathf.Clamp01(left / Mathf.Max(0.0001f, fadeSeconds));
        }
    }
}
