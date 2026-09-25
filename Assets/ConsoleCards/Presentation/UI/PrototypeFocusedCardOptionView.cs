using System;
using System.Collections;
using ConsoleCards.Core.Identifiers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Generic, prefab-authored representation of one authoritative Card identity in a
    /// focused card-selection interaction. It owns only hover/selection/reveal presentation.
    /// </summary>
    public sealed class PrototypeFocusedCardOptionView : ReusableUiView,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private RectTransform cardTransform;
        [SerializeField] private Image surface;
        [SerializeField] private RawImage artwork;
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private LayoutElement layoutElement;

        private static readonly Color EligibleSurface = new Color(0.16f, 0.25f, 0.36f, 1f);
        private static readonly Color SelectedSurface = new Color(0.20f, 0.62f, 0.78f, 1f);
        private static readonly Color IneligibleSurface = new Color(0.075f, 0.09f, 0.12f, 0.92f);
        private static readonly Color EligibleArtwork = Color.white;
        private static readonly Color IneligibleArtwork = new Color(0.30f, 0.30f, 0.30f, 0.72f);

        private TabletopObjectId cardId;
        private bool eligible;
        private bool selected;
        private bool hovered;
        private Vector2 restingPosition;
        private float restingRotation;
        private Action<TabletopObjectId> selectedCallback;
        private Coroutine revealRoutine;

        public TabletopObjectId CardId => cardId;

        public void ValidateReferences()
        {
            if (cardTransform == null
                || surface == null
                || artwork == null
                || button == null
                || label == null
                || layoutElement == null)
            {
                throw new InvalidOperationException(
                    "PrototypeFocusedCardOptionView requires its authored Card surface, artwork, label, Button, and layout references.");
            }
        }

        public void Bind(PrototypeFocusedCardOptionModel model, Action<TabletopObjectId> choose)
        {
            RequireAcquired();
            if (model == null) throw new ArgumentNullException(nameof(model));
            ValidateReferences();
            Unbind();

            cardId = model.CardId;
            eligible = model.Eligible;
            selected = model.Selected;
            selectedCallback = choose ?? throw new ArgumentNullException(nameof(choose));
            artwork.texture = model.Artwork;
            artwork.enabled = model.Artwork != null;
            label.text = string.IsNullOrWhiteSpace(model.Subtitle)
                ? model.Title
                : $"{model.Title}\n<size=12>{model.Subtitle}</size>";
            layoutElement.preferredWidth = 148f;
            layoutElement.preferredHeight = 214f;
            button.interactable = eligible;
            button.onClick.AddListener(Choose);
            ApplyVisualState(immediate: true);
        }

        public void SetFanPose(Vector2 anchoredPosition, float rotationDegrees, bool animateEntry)
        {
            restingPosition = anchoredPosition;
            restingRotation = rotationDegrees;
            cardTransform.sizeDelta = new Vector2(
                layoutElement.preferredWidth,
                layoutElement.preferredHeight);
            cardTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            if (animateEntry)
            {
                cardTransform.anchoredPosition = anchoredPosition + new Vector2(0f, -44f);
                cardTransform.localScale = Vector3.one * 0.94f;
            }
            else
            {
                ApplyVisualState(immediate: true);
            }
        }

        public void SetSelected(bool value)
        {
            selected = value;
            ApplyVisualState(immediate: false);
        }

        public void ShowReveal(
            Texture backArtwork,
            Texture frontArtwork,
            string frontTitle,
            string frontSubtitle,
            Action completed)
        {
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            eligible = false;
            selected = false;
            hovered = false;
            button.interactable = false;
            surface.color = EligibleSurface;
            artwork.texture = backArtwork;
            artwork.enabled = backArtwork != null;
            artwork.color = Color.white;
            label.text = "MYSTERY";
            label.color = Color.white;
            revealRoutine = StartCoroutine(RevealSequence(
                frontArtwork,
                frontTitle,
                frontSubtitle,
                completed));
        }

        public override void Unbind()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }

            cardId = TabletopObjectId.Empty;
            eligible = false;
            selected = false;
            hovered = false;
            restingPosition = Vector2.zero;
            restingRotation = 0f;
            selectedCallback = null;
            if (artwork != null)
            {
                artwork.texture = null;
                artwork.enabled = false;
                artwork.color = Color.white;
            }
            if (label != null) label.text = string.Empty;
            if (cardTransform != null)
            {
                cardTransform.localScale = Vector3.one;
                cardTransform.localRotation = Quaternion.identity;
            }
        }

        public void OnPointerEnter(PointerEventData _)
        {
            if (!IsAcquired || !eligible) return;
            hovered = true;
            cardTransform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData _)
        {
            if (!IsAcquired) return;
            hovered = false;
        }

        private void Update()
        {
            if (!IsAcquired || revealRoutine != null || cardTransform == null) return;
            Vector2 targetPosition = restingPosition + new Vector2(0f, selected ? 28f : hovered ? 13f : 0f);
            float targetScale = selected ? 1.08f : hovered ? 1.035f : 1f;
            cardTransform.anchoredPosition = Vector2.Lerp(
                cardTransform.anchoredPosition,
                targetPosition,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            cardTransform.localScale = Vector3.Lerp(
                cardTransform.localScale,
                Vector3.one * targetScale,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            cardTransform.localRotation = Quaternion.Lerp(
                cardTransform.localRotation,
                Quaternion.Euler(0f, 0f, restingRotation),
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }

        private void Choose()
        {
            if (eligible) selectedCallback?.Invoke(cardId);
        }

        private void ApplyVisualState(bool immediate)
        {
            surface.color = !eligible ? IneligibleSurface : selected ? SelectedSurface : EligibleSurface;
            artwork.color = eligible ? EligibleArtwork : IneligibleArtwork;
            label.color = eligible ? Color.white : new Color(0.50f, 0.54f, 0.60f, 1f);
            if (selected || hovered) cardTransform.SetAsLastSibling();
            if (!immediate) return;
            cardTransform.anchoredPosition = restingPosition + new Vector2(0f, selected ? 28f : 0f);
            cardTransform.localScale = Vector3.one * (selected ? 1.08f : 1f);
        }

        private IEnumerator RevealSequence(
            Texture frontArtwork,
            string frontTitle,
            string frontSubtitle,
            Action completed)
        {
            const float raiseDuration = 0.12f;
            const float halfFlipDuration = 0.16f;
            Vector2 start = restingPosition;
            Vector2 raised = restingPosition + new Vector2(0f, 34f);
            yield return Animate(raiseDuration, value =>
            {
                cardTransform.anchoredPosition = Vector2.Lerp(start, raised, value);
                cardTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.12f, value);
            });
            yield return Animate(halfFlipDuration, value =>
            {
                cardTransform.localRotation = Quaternion.Euler(
                    0f,
                    Mathf.Lerp(0f, 90f, value),
                    restingRotation);
            });

            artwork.texture = frontArtwork;
            artwork.enabled = frontArtwork != null;
            label.text = string.IsNullOrWhiteSpace(frontSubtitle)
                ? frontTitle
                : $"{frontTitle}\n<size=12>{frontSubtitle}</size>";

            yield return Animate(halfFlipDuration, value =>
            {
                cardTransform.localRotation = Quaternion.Euler(
                    0f,
                    Mathf.Lerp(-90f, 0f, value),
                    restingRotation);
            });
            yield return Animate(0.12f, value =>
            {
                cardTransform.anchoredPosition = Vector2.Lerp(raised, restingPosition, value);
                cardTransform.localScale = Vector3.Lerp(Vector3.one * 1.12f, Vector3.one, value);
            });
            revealRoutine = null;
            completed?.Invoke();
        }

        private static IEnumerator Animate(float duration, Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            apply(1f);
        }
    }
}
