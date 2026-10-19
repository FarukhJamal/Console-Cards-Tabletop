using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// POL-3 / POL-5: the table's information layer, over the table and under the menus; it never takes a click.
    /// A tooltip by the pointer (a piece's name, owner, a pile's card count), the card peek (Alt held over a card
    /// shows it large) and a few floating labels that rise from a point on the table and fade (a die's result).
    /// An authored Final UI prefab (Console Cards > UI > Create Missing Gameplay UI Prefabs); code only fills texts
    /// and positions. Nothing is allocated per frame.
    /// </summary>
    public sealed class TableOverlayView : ReusableUiView
    {
        [Header("Tooltip")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private Text tooltipTitle;
        [SerializeField] private Text tooltipDetail;
        [SerializeField] private Vector2 tooltipOffset = new Vector2(22f, -22f);
        [Header("Peek")]
        [SerializeField] private RectTransform peekPanel;
        [SerializeField] private Image peekSurface;
        [SerializeField] private Image peekArtwork;
        [SerializeField] private Text peekTitle;
        [SerializeField] private Text peekBody;
        [Header("Floating labels")]
        [SerializeField] private RectTransform[] floatLabels = new RectTransform[0];
        [SerializeField] private Text[] floatTexts = new Text[0];
        [SerializeField] private CanvasGroup[] floatGroups = new CanvasGroup[0];
        [SerializeField] private float floatRisePixels = 46f;

        private UnityEngine.Camera worldCamera;
        private Canvas canvas;
        private Vector3[] floatWorld = new Vector3[0];
        private float[] floatStart = new float[0];
        private float[] floatSeconds = new float[0];
        private int nextFloat;
        private string shownTooltipTitle;
        private string shownTooltipDetail;

        public void ValidateReferences()
        {
            if (tooltipPanel == null || tooltipTitle == null || tooltipDetail == null || peekPanel == null
                || peekSurface == null || peekArtwork == null || peekTitle == null || peekBody == null
                || floatLabels == null || floatTexts == null || floatGroups == null
                || floatLabels.Length != floatTexts.Length || floatLabels.Length != floatGroups.Length)
            {
                throw new InvalidOperationException(
                    "TableOverlayView is missing an authored reference. Run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI).");
            }
        }

        public void Bind(UnityEngine.Camera camera)
        {
            RequireAcquired();
            ValidateReferences();
            worldCamera = camera;
            if (floatWorld.Length != floatLabels.Length)
            {
                floatWorld = new Vector3[floatLabels.Length];
                floatStart = new float[floatLabels.Length];
                floatSeconds = new float[floatLabels.Length];
            }

            HideTooltip();
            HidePeek();
            for (int i = 0; i < floatLabels.Length; i++)
            {
                floatSeconds[i] = 0f;
                if (floatLabels[i] != null) floatLabels[i].gameObject.SetActive(false);
            }
        }

        /// <summary>The tooltip by the pointer. Texts are written only when they change.</summary>
        public void ShowTooltip(Vector2 screenPosition, string title, string detail)
        {
            if (!ReferenceEquals(title, shownTooltipTitle))
            {
                shownTooltipTitle = title;
                tooltipTitle.text = title ?? string.Empty;
            }

            if (!ReferenceEquals(detail, shownTooltipDetail))
            {
                shownTooltipDetail = detail;
                tooltipDetail.text = detail ?? string.Empty;
                tooltipDetail.gameObject.SetActive(!string.IsNullOrEmpty(detail));
            }

            // Left of the pointer near the right edge of the screen, below it otherwise.
            bool flip = screenPosition.x > Screen.width * 0.75f;
            tooltipPanel.pivot = new Vector2(flip ? 1f : 0f, 1f);
            Vector2 offset = new Vector2(flip ? -tooltipOffset.x : tooltipOffset.x, tooltipOffset.y);
            Place(tooltipPanel, screenPosition + offset);
            if (!tooltipPanel.gameObject.activeSelf) tooltipPanel.gameObject.SetActive(true);
        }

        public void HideTooltip()
        {
            if (tooltipPanel != null && tooltipPanel.gameObject.activeSelf) tooltipPanel.gameObject.SetActive(false);
        }

        /// <summary>The card shown large (Alt held over it): the side that lies up on the table.</summary>
        public void ShowPeek(string title, string body, Sprite artwork, Color surface, Color text)
        {
            peekTitle.text = title ?? string.Empty;
            peekTitle.color = text;
            peekBody.text = body ?? string.Empty;
            peekBody.color = text;
            peekSurface.color = surface;
            peekArtwork.sprite = artwork;
            peekArtwork.gameObject.SetActive(artwork != null);
            if (!peekPanel.gameObject.activeSelf) peekPanel.gameObject.SetActive(true);
        }

        public void HidePeek()
        {
            if (peekPanel != null && peekPanel.gameObject.activeSelf) peekPanel.gameObject.SetActive(false);
        }

        public bool IsPeekShown => peekPanel != null && peekPanel.gameObject.activeSelf;

        /// <summary>A label that rises from a point on the table and fades (oldest reused first).</summary>
        public void ShowFloat(Vector3 worldPosition, string text, Color colour, float seconds)
        {
            if (floatLabels.Length == 0 || seconds <= 0f) return;
            int i = nextFloat;
            nextFloat = (nextFloat + 1) % floatLabels.Length;
            floatWorld[i] = worldPosition;
            floatStart[i] = Time.unscaledTime;
            floatSeconds[i] = seconds;
            floatTexts[i].text = text ?? string.Empty;
            floatTexts[i].color = colour;
            floatGroups[i].alpha = 1f;
            floatLabels[i].gameObject.SetActive(true);
            UpdateFloat(i);
        }

        public override void Unbind()
        {
            worldCamera = null;
        }

        private void Update()
        {
            for (int i = 0; i < floatSeconds.Length; i++)
            {
                if (floatSeconds[i] > 0f) UpdateFloat(i);
            }
        }

        private void UpdateFloat(int i)
        {
            float t = (Time.unscaledTime - floatStart[i]) / floatSeconds[i];
            if (t >= 1f || worldCamera == null)
            {
                floatSeconds[i] = 0f;
                floatLabels[i].gameObject.SetActive(false);
                return;
            }

            Vector3 screen = worldCamera.WorldToScreenPoint(floatWorld[i]);
            if (screen.z <= 0f)
            {
                floatGroups[i].alpha = 0f;
                return;
            }

            float eased = 1f - ((1f - t) * (1f - t));
            Place(floatLabels[i], new Vector2(screen.x, screen.y + (eased * floatRisePixels)));
            floatGroups[i].alpha = t < 0.7f ? 1f : Mathf.Clamp01((1f - t) / 0.3f);
        }

        private void Place(RectTransform target, Vector2 screenPosition)
        {
            RectTransform parent = target.parent as RectTransform;
            if (parent == null) return;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            UnityEngine.Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, eventCamera, out Vector2 local))
            {
                target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
                target.anchoredPosition = local;
            }
        }
    }
}
