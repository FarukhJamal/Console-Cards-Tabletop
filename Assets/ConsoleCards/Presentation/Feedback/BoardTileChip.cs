using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>The three word chips of board 18 (owner, 2026-11-02: "it shouldn't be covering the card that much").</summary>
    public enum BoardChip
    {
        /// <summary>Over the turn piece's tile, sitting on its far edge: YOU, or the seat's colour.</summary>
        Turn = 0,

        /// <summary>Under the tile below a carried piece, hanging from its near edge: what dropping there costs.</summary>
        Steps = 1,

        /// <summary>Over the round's event tile, sitting on its far edge (Trap Floor: COLLAPSE).</summary>
        Event = 2,
    }

    /// <summary>
    /// CHIP-1: a small word chip drawn on the screen (so it reads at any zoom) and pinned to a tile's edge: it sits just
    /// past the edge, over the gap, so the card itself stays visible. One overlay canvas below every panel holds the
    /// chips; each chip follows its tile every frame (no allocation; the words change only when the caller passes other
    /// words).
    /// </summary>
    internal sealed class BoardTileChip : MonoBehaviour
    {
        private const float ReferenceHeight = 1080f;
        private const float LetterSize = 11f;      // at 1080 lines
        private const float ChipHeight = 16f;
        private const float SidePadding = 6f;
        private const float EdgeGap = 1f;          // pixels between the tile's edge and the chip
        private const float HalfTileDepth = 0.7f;  // table units: a floor tile is 1 wide, 1.4 deep
        private static Font chipFont;

        private RectTransform rect;
        private Image plate;
        private Text words;
        private string shownWords;
        private Vector3 centre;
        private bool below;
        private bool wanted;

        public GameObject Root => gameObject;

        /// <summary>The overlay canvas for the chips (sorted under the HUD and popups).</summary>
        public static Canvas CreateCanvas(Transform parent)
        {
            GameObject canvasObject = new GameObject("Board Chips", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -50;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        public static BoardTileChip Create(Canvas canvas, string name)
        {
            GameObject chip = new GameObject(name, typeof(RectTransform));
            chip.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)chip.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            Image plate = chip.AddComponent<Image>();
            plate.raycastTarget = false;

            GameObject label = new GameObject("Words", typeof(RectTransform));
            label.transform.SetParent(chip.transform, false);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text text = label.AddComponent<Text>();
            text.font = ChipFont();
            text.fontSize = Mathf.RoundToInt(LetterSize);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            BoardTileChip made = chip.AddComponent<BoardTileChip>();
            made.rect = rect;
            made.plate = plate;
            made.words = text;
            chip.SetActive(false);
            return made;
        }

        // The game's own Oxanium Bold when it is loaded (the HUD uses it), else Unity's built-in font.
        private static Font ChipFont()
        {
            if (chipFont != null) return chipFont;
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < fonts.Length; i++)
            {
                if (fonts[i] != null && fonts[i].name == "Oxanium-Bold")
                {
                    chipFont = fonts[i];
                    return chipFont;
                }
            }

            chipFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return chipFont;
        }

        /// <param name="tileCentre">The tile's centre in the world.</param>
        /// <param name="belowTile">True: hangs under the tile's near edge; false: sits on its far edge.</param>
        public void Show(Vector3 tileCentre, bool belowTile, string text, Color plateColour, Color ink)
        {
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }

            centre = tileCentre;
            below = belowTile;
            wanted = true;
            if (!ReferenceEquals(text, shownWords))
            {
                shownWords = text;
                words.text = text;
                float width = (text.Length * LetterSize * 0.68f) + (SidePadding * 2f);
                rect.sizeDelta = new Vector2(width, ChipHeight);
                rect.pivot = new Vector2(0.5f, belowTile ? 1f : 0f);
            }

            if (plate.color != plateColour) plate.color = plateColour;
            if (words.color != ink) words.color = ink;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Place();
        }

        public void Hide()
        {
            wanted = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (wanted) Place();
        }

        // Pinned to the tile's far (or near) edge as the camera looks, in canvas units.
        private void Place()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) return;
            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = camera.transform.up;
            forward.y = 0f;
            forward.Normalize();
            Vector3 edgePoint = centre + (forward * (below ? -HalfTileDepth : HalfTileDepth));
            Vector3 screen = camera.WorldToScreenPoint(edgePoint);
            bool visible = screen.z > 0f;
            if (words.enabled != visible)
            {
                words.enabled = visible;
                plate.enabled = visible;
            }

            float scale = ReferenceHeight / Mathf.Max(1f, Screen.height);
            rect.anchoredPosition = new Vector2(screen.x * scale, (screen.y * scale) + (below ? -EdgeGap : EdgeGap));
        }
    }
}
