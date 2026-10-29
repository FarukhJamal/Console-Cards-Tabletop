using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// POP-2 (owner, 2026-10-29: "Starting floor 2 is too big"): the floor's end as a chip at the top centre, the size
    /// of the turn banner: a tag (FLOOR 1 WON), the next floor, a ring that counts down and then presses the main
    /// button, and a small way out. Esc stops the countdown and keeps the chip open; hovering pauses it. The table
    /// stays live. Authored prefab in Final UI/Notices.
    /// </summary>
    public sealed class TableMilestoneView : ReusableUiView, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Image toneBar;
        [SerializeField] private Image panelBorder;
        [SerializeField] private Image tagFill;
        [SerializeField] private Text tagLabel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text detailLabel;
        [Tooltip("The whole countdown (ring, back and number): shown only while counting.")]
        [SerializeField] private GameObject countdownRoot;
        [SerializeField] private Image countdownRing;
        [SerializeField] private Text countdownLabel;
        [SerializeField] private Text hintLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private Text secondaryLabel;
        [Header("Tone colours")]
        [SerializeField] private Color wonColour = new Color(0.184f, 0.831f, 0.420f, 1f);
        [SerializeField] private Color lostColour = new Color(1f, 0.231f, 0.231f, 1f);
        [SerializeField] private Color infoColour = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color darkTagText = new Color(0.055f, 0.141f, 0.086f, 1f);
        [SerializeField] private Color lightTagText = Color.white;

        private TableMilestoneModel model;
        private float countdownLeft;
        private bool counting;
        private bool hovered;
        private int shownSecond = -1;
        private Func<bool> paused;

        public void ValidateReferences()
        {
            if (panel == null || toneBar == null || panelBorder == null || tagFill == null || tagLabel == null
                || titleLabel == null || detailLabel == null || countdownRing == null || countdownLabel == null
                || hintLabel == null || primaryButton == null || primaryLabel == null || secondaryButton == null
                || secondaryLabel == null)
            {
                throw new InvalidOperationException(
                    "TableMilestoneView is missing an authored part. Run Console Cards > UI > Create Missing Table Notices (POP-2).");
            }
        }

        public void Bind(TableMilestoneModel milestone, Func<bool> pauseWhile = null)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            model = milestone ?? throw new ArgumentNullException(nameof(milestone));
            paused = pauseWhile;
            Color tone = model.Tone == TableNoticeTone.Won ? wonColour
                : model.Tone == TableNoticeTone.Danger ? lostColour
                : infoColour;
            toneBar.color = tone;
            panelBorder.color = tone;
            tagFill.color = tone;
            tagLabel.color = model.Tone == TableNoticeTone.Danger ? lightTagText : darkTagText;
            tagLabel.text = model.Tag.ToUpperInvariant();
            titleLabel.text = model.Title;
            detailLabel.text = model.Detail;
            detailLabel.gameObject.SetActive(model.Detail.Length > 0);
            primaryLabel.text = model.PrimaryLabel.ToUpperInvariant();
            primaryButton.onClick.AddListener(PressPrimary);
            bool hasSecondary = model.Secondary != null;
            secondaryButton.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                secondaryLabel.text = model.SecondaryLabel.ToUpperInvariant();
                secondaryButton.onClick.AddListener(PressSecondary);
            }

            counting = model.AutoContinueSeconds > 0f;
            countdownLeft = model.AutoContinueSeconds;
            hovered = false;
            shownSecond = -1;
            CountdownObject.SetActive(counting);
            hintLabel.gameObject.SetActive(counting);
            UpdateRing();
        }

        public override void Unbind()
        {
            if (primaryButton != null) primaryButton.onClick.RemoveAllListeners();
            if (secondaryButton != null) secondaryButton.onClick.RemoveAllListeners();
            model = null;
            paused = null;
            counting = false;
        }

        private GameObject CountdownObject => countdownRoot != null ? countdownRoot : countdownRing.gameObject;

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;

        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        private void Update()
        {
            if (model == null) return;
            if (counting && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                StopCountdown();
                return;
            }

            if (!counting || hovered || (paused != null && paused())) return;
            countdownLeft -= Time.unscaledDeltaTime;
            if (countdownLeft <= 0f)
            {
                counting = false;
                PressPrimary();
                return;
            }

            UpdateRing();
        }

        private void StopCountdown()
        {
            counting = false;
            CountdownObject.SetActive(false);
            hintLabel.gameObject.SetActive(false);
        }

        private void UpdateRing()
        {
            if (!counting || model == null) return;
            countdownRing.fillAmount = Mathf.Clamp01(countdownLeft / Mathf.Max(0.0001f, model.AutoContinueSeconds));
            int second = Mathf.CeilToInt(countdownLeft);
            if (second != shownSecond)
            {
                shownSecond = second;
                countdownLabel.text = second.ToString();
            }
        }

        private void PressPrimary()
        {
            counting = false;
            Action primary = model?.Primary;
            primary?.Invoke();
        }

        private void PressSecondary()
        {
            counting = false;
            Action secondary = model?.Secondary;
            secondary?.Invoke();
        }
    }
}
