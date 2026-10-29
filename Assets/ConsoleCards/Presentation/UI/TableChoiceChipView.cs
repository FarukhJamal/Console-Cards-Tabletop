using System;
using ConsoleCards.Core.Identifiers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// POP-2 (owner approved "Popups v2", 2026-10-29): a reveal shown where it happened. The card lifts above its
    /// tile, a dotted leader ties it to the tile, and a small choice chip sits beside it (tag, name, one short text,
    /// the way out and the main button). The table stays live: only the chip itself takes clicks. When the tile
    /// leaves the screen the chip parks bottom right ("1 choice open") with the same buttons.
    /// It shows the same <see cref="FocusedCardRevealModel"/> the old centre popup used, so games are unchanged.
    /// Layout, sizes and colours are authored on the prefab (Final UI/Notices); nothing is allocated per frame.
    /// </summary>
    public sealed class TableChoiceChipView : ReusableUiView
    {
        [Header("Parts")]
        [SerializeField] private RectTransform chip;
        [SerializeField] private Image chipBorder;
        [SerializeField] private RectTransform card;
        [SerializeField] private Image cardGlow;
        [SerializeField] private RawImage cardPicture;
        [SerializeField] private RectTransform leader;
        [SerializeField] private Image leaderImage;
        [SerializeField] private Image tagFill;
        [SerializeField] private Text tagLabel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private GameObject dockLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private Text secondaryLabel;

        [Header("Placing (canvas units)")]
        [Tooltip("Where the lifted card's centre sits, from the tile's point on the screen.")]
        [SerializeField] private Vector2 cardOffset = new Vector2(0f, 96f);
        [Tooltip("Gap between the lifted card and the chip beside it.")]
        [SerializeField] private float chipGap = 14f;
        [Tooltip("A tile closer than this to the screen edge counts as off screen: the chip parks.")]
        [SerializeField] private float screenMargin = 24f;
        [Tooltip("Where the parked chip sits, from the bottom-right corner.")]
        [SerializeField] private Vector2 dockOffset = new Vector2(-24f, 132f);
        [SerializeField] private float cardTilt = -4f;

        [Header("Tone colours")]
        [SerializeField] private Color infoColour = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color dangerColour = new Color(1f, 0.231f, 0.231f, 1f);
        [SerializeField] private Color cautionColour = new Color(1f, 0.698f, 0.118f, 1f);
        [SerializeField] private Color rewardColour = new Color(0.910f, 0.769f, 0.290f, 1f);
        [SerializeField] private Color abilityColour = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color friendColour = new Color(0.184f, 0.831f, 0.420f, 1f);
        [SerializeField] private Color darkTagText = new Color(0.063f, 0.125f, 0.129f, 1f);
        [SerializeField] private Color lightTagText = Color.white;

        private FocusedCardRevealModel model;
        private TableAnchorResolver resolver;
        private UnityEngine.Camera worldCamera;
        private Canvas canvas;
        private RectTransform area;
        private bool docked;
        private bool placedOnce;
        private int boundFrame;
        private Func<bool> enterBlocked;

        public TabletopObjectId CardId => model != null ? model.CardId : TabletopObjectId.Empty;

        public void ValidateReferences()
        {
            if (chip == null || chipBorder == null || card == null || cardGlow == null || cardPicture == null
                || leader == null || leaderImage == null || tagFill == null || tagLabel == null || titleLabel == null
                || bodyLabel == null || dockLabel == null || primaryButton == null || primaryLabel == null
                || secondaryButton == null || secondaryLabel == null)
            {
                throw new InvalidOperationException(
                    "TableChoiceChipView is missing an authored part. Run Console Cards > UI > Create Missing Table Notices (POP-2).");
            }
        }

        public void Bind(
            FocusedCardRevealModel reveal,
            UnityEngine.Camera camera,
            TableAnchorResolver anchors,
            Func<bool> blockEnter = null)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            model = reveal ?? throw new ArgumentNullException(nameof(reveal));
            worldCamera = camera;
            resolver = anchors;
            enterBlocked = blockEnter;
            boundFrame = Time.frameCount;
            area = transform as RectTransform;

            Color tone = ToneColour(TableNoticeText.ToneOf(model.HeaderTitle, model.ResultState));
            string tag = TableNoticeText.Tag(model.ResultState);
            tagLabel.text = tag.Length > 0 ? tag : TableNoticeText.Tag(model.HeaderTitle);
            tagFill.color = tone;
            tagLabel.color = IsDark(tone) ? lightTagText : darkTagText;
            chipBorder.color = tone;
            cardGlow.color = tone;
            leaderImage.color = tone;
            titleLabel.text = model.CardTitle.Length > 0 ? model.CardTitle : model.HeaderTitle;
            bodyLabel.text = ShortBody(model);
            bodyLabel.gameObject.SetActive(bodyLabel.text.Length > 0);

            cardPicture.texture = model.FrontArtwork != null ? model.FrontArtwork : model.BackArtwork;
            cardPicture.gameObject.SetActive(cardPicture.texture != null);
            card.localEulerAngles = new Vector3(0f, 0f, cardTilt);

            primaryLabel.text = model.CompletionLabel.ToUpperInvariant();
            primaryButton.onClick.AddListener(PressPrimary);
            bool hasSecondary = model.Secondary != null;
            secondaryButton.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                secondaryLabel.text = model.SecondaryLabel.ToUpperInvariant();
                secondaryButton.onClick.AddListener(PressSecondary);
            }

            placedOnce = false;
            docked = false;
            Place();
        }

        public override void Unbind()
        {
            if (primaryButton != null) primaryButton.onClick.RemoveAllListeners();
            if (secondaryButton != null) secondaryButton.onClick.RemoveAllListeners();
            model = null;
            resolver = null;
            worldCamera = null;
            enterBlocked = null;
            if (cardPicture != null) cardPicture.texture = null;
        }

        private void LateUpdate()
        {
            if (model == null) return;
            Place();
            // Enter presses the main button: not in the frame the chip appeared (that Enter belonged to something else),
            // not while the player types or a chip button has focus, and not while a modal panel is open.
            if (Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                && Time.frameCount != boundFrame
                && !UiHasFocus()
                && (enterBlocked == null || !enterBlocked()))
            {
                PressPrimary();
            }
        }

        private void Place()
        {
            if (area == null) return;
            bool onScreen = false;
            Vector2 tileScreen = default;
            if (resolver != null && worldCamera != null && resolver(model.CardId, out Vector3 world))
            {
                Vector3 screen = worldCamera.WorldToScreenPoint(world);
                tileScreen = new Vector2(screen.x, screen.y);
                onScreen = screen.z > 0f
                    && screen.x > screenMargin && screen.x < Screen.width - screenMargin
                    && screen.y > screenMargin && screen.y < Screen.height - screenMargin;
            }

            if (docked == onScreen || !placedOnce)
            {
                docked = !onScreen;
                card.gameObject.SetActive(onScreen);
                leader.gameObject.SetActive(onScreen);
                dockLabel.SetActive(!onScreen);
                placedOnce = true;
            }

            if (!onScreen)
            {
                chip.anchorMin = chip.anchorMax = new Vector2(1f, 0f);
                chip.pivot = new Vector2(1f, 0f);
                chip.anchoredPosition = dockOffset;
                return;
            }

            if (!ToLocal(tileScreen, out Vector2 tileLocal)) return;
            Vector2 cardCentre = tileLocal + cardOffset;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = cardCentre;

            // The leader: from the tile up to the card's lower edge.
            Vector2 cardBottom = cardCentre - new Vector2(0f, card.rect.height * 0.5f);
            Vector2 span = cardBottom - tileLocal;
            float length = span.magnitude;
            leader.anchorMin = leader.anchorMax = new Vector2(0.5f, 0.5f);
            leader.pivot = new Vector2(0.5f, 0f);
            leader.anchoredPosition = tileLocal;
            leader.sizeDelta = new Vector2(leader.sizeDelta.x, length);
            leader.localEulerAngles = new Vector3(0f, 0f, length > 0.01f ? Vector2.SignedAngle(Vector2.up, span) : 0f);

            // The chip beside the card, on the side with room; kept inside the screen.
            float halfWidth = area.rect.width * 0.5f;
            float halfHeight = area.rect.height * 0.5f;
            float chipWidth = chip.rect.width;
            float chipHeight = chip.rect.height;
            float rightX = cardCentre.x + (card.rect.width * 0.5f) + chipGap;
            bool flip = rightX + chipWidth > halfWidth - screenMargin;
            float x = flip ? cardCentre.x - (card.rect.width * 0.5f) - chipGap - chipWidth : rightX;
            float top = cardCentre.y + (card.rect.height * 0.5f);
            float y = Mathf.Clamp(top, -halfHeight + chipHeight + screenMargin, halfHeight - screenMargin);
            chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.5f);
            chip.pivot = new Vector2(0f, 1f);
            chip.anchoredPosition = new Vector2(Mathf.Max(x, -halfWidth + screenMargin), y);
        }

        private bool ToLocal(Vector2 screenPosition, out Vector2 local)
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            UnityEngine.Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPosition, eventCamera, out local);
        }

        private void PressPrimary()
        {
            Action completed = model?.Completed;
            completed?.Invoke();
        }

        private void PressSecondary()
        {
            Action secondary = model?.Secondary;
            secondary?.Invoke();
        }

        // The chip shows the reveal's words after the card's own text (the picture carries that): the timing, the
        // cost, what to do. A one-paragraph body is shown as it is.
        private static string ShortBody(FocusedCardRevealModel reveal)
        {
            string body = reveal.Body ?? string.Empty;
            int split = body.IndexOf("\n\n", StringComparison.Ordinal);
            string rest = split >= 0 ? body.Substring(split + 2) : body;
            return rest.Replace("\n\n", " ").Replace('\n', ' ').Trim();
        }

        private Color ToneColour(TableNoticeTone tone)
        {
            switch (tone)
            {
                case TableNoticeTone.Danger: return dangerColour;
                case TableNoticeTone.Caution: return cautionColour;
                case TableNoticeTone.Reward: return rewardColour;
                case TableNoticeTone.Ability: return abilityColour;
                case TableNoticeTone.Friend: return friendColour;
                default: return infoColour;
            }
        }

        private static bool IsDark(Color colour) =>
            (colour.r * 0.299f) + (colour.g * 0.587f) + (colour.b * 0.114f) < 0.55f;

        // A text box has focus, or one of the chip's own buttons (its Submit would press it a second time).
        private bool UiHasFocus()
        {
            EventSystem current = EventSystem.current;
            GameObject selected = current != null ? current.currentSelectedGameObject : null;
            return selected != null
                && (selected.transform.IsChildOf(transform) || selected.GetComponent<InputField>() != null);
        }
    }
}
