using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Intro
{
    /// <summary>
    /// The screen-wide layer of the title intro (doc 37). It fades the room in from black, takes the white flash at the
    /// end of the dive, survives the switch to the table scene, and then powers the picture on like an old TV: one
    /// bright line, the two black halves opening from the middle onto the main menu, the scan lines fading away.
    /// Everything is made in code; it needs no art.
    /// </summary>
    public sealed class IntroPowerOn : MonoBehaviour
    {
        private Image fade;
        private Image flash;
        private RectTransform shutterTop;
        private RectTransform shutterBottom;
        private Image seam;
        private Image scanlines;
        private Texture2D seamTexture;
        private Texture2D scanTexture;
        private bool revealing;

        public static IntroPowerOn Create()
        {
            GameObject go = new GameObject("Intro Power-On");
            DontDestroyOnLoad(go);
            IntroPowerOn layer = go.AddComponent<IntroPowerOn>();
            layer.Build();
            return layer;
        }

        /// <summary>Black over everything, 1 = black.</summary>
        public float Fade
        {
            set => SetAlpha(fade, value);
        }

        /// <summary>The white-cyan light at the end of the dive, 1 = full.</summary>
        public float Flash
        {
            set => SetAlpha(flash, value);
        }

        /// <summary>
        /// Lets <paramref name="load"/> finish (the table scene), waits for its first frames (the main menu opens in its
        /// Start), then opens the picture. The layer removes itself at the end.
        /// </summary>
        public void RevealAfter(AsyncOperation load)
        {
            if (revealing) return;
            revealing = true;
            StartCoroutine(Reveal(load));
        }

        private IEnumerator Reveal(AsyncOperation load)
        {
            SetAlpha(flash, 1f);
            SetShutters(0f);
            SetAlpha(seam, 1f);
            SetAlpha(scanlines, 0.45f);
            if (load != null)
            {
                load.allowSceneActivation = true;
                while (!load.isDone) yield return null;
            }

            // The table builds itself and opens the main menu in its first frame; a few frames hide that work.
            for (int i = 0; i < 3; i++) yield return null;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.14f;
                SetAlpha(flash, 1f - Mathf.Clamp01(t));
                yield return null;
            }

            SetAlpha(flash, 0f);
            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.55f;
                float e = Mathf.Clamp01(t);
                SetShutters(1f - Mathf.Pow(1f - e, 3f));
                float s = e * e * (3f - 2f * e);
                seam.rectTransform.localScale = new Vector3(1f + 0.2f * e, 1f - 0.8f * e, 1f);
                SetAlpha(seam, 1f - s);
                SetAlpha(scanlines, Mathf.Lerp(0.45f, 0.12f, e));
                yield return null;
            }

            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.5f;
                SetAlpha(scanlines, Mathf.Lerp(0.12f, 0f, Mathf.Clamp01(t)));
                yield return null;
            }

            Destroy(gameObject);
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767; // over the table's own UI while it starts

            seamTexture = new Texture2D(64, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Intro Seam" };
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    float dx = Mathf.Abs(x - 31.5f) / 32f;
                    float dy = Mathf.Abs(y - 3.5f) / 4f;
                    float a = Mathf.Clamp01(1f - dy) * Mathf.Clamp01(1f - dx * dx);
                    seamTexture.SetPixel(x, y, new Color(0.85f + 0.15f * a, 1f, 1f, a));
                }
            }

            seamTexture.Apply();
            scanTexture = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Intro Scanlines" };
            scanTexture.SetPixel(0, 0, Color.clear);
            scanTexture.SetPixel(0, 1, Color.clear);
            scanTexture.SetPixel(0, 2, Color.black);
            scanTexture.SetPixel(0, 3, Color.black);
            scanTexture.Apply();

            shutterTop = Full("Shutter Top", Color.black).rectTransform;
            shutterBottom = Full("Shutter Bottom", Color.black).rectTransform;
            seam = Full("Seam", Color.white);
            seam.sprite = Sprite.Create(seamTexture, new Rect(0, 0, 64, 8), new Vector2(0.5f, 0.5f));
            RectTransform seamRect = seam.rectTransform;
            seamRect.anchorMin = new Vector2(-0.1f, 0.5f);
            seamRect.anchorMax = new Vector2(1.1f, 0.5f);
            seamRect.sizeDelta = new Vector2(0f, 70f);
            flash = Full("Flash", new Color(0.92f, 1f, 1f, 1f));
            scanlines = Full("Scanlines", Color.white);
            scanlines.sprite = Sprite.Create(scanTexture, new Rect(0, 0, 1, 4), new Vector2(0.5f, 0.5f), 1f);
            scanlines.type = Image.Type.Tiled;
            fade = Full("Fade", Color.black);

            SetAlpha(flash, 0f);
            SetAlpha(seam, 0f);
            SetAlpha(scanlines, 0f);
            SetAlpha(fade, 1f);
            SetShutters(1f);
        }

        private Image Full(string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // 0: the halves meet in the middle (black); 1: both are off screen.
        private void SetShutters(float open)
        {
            shutterTop.anchorMin = new Vector2(0f, Mathf.Lerp(0.5f, 1f, open));
            shutterTop.anchorMax = new Vector2(1f, 1.02f);
            shutterBottom.anchorMin = new Vector2(0f, -0.02f);
            shutterBottom.anchorMax = new Vector2(1f, Mathf.Lerp(0.5f, 0f, open));
            shutterTop.gameObject.SetActive(open < 1f);
            shutterBottom.gameObject.SetActive(open < 1f);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            Color c = graphic.color;
            c.a = Mathf.Clamp01(alpha);
            graphic.color = c;
            graphic.enabled = c.a > 0.001f;
        }

        private void OnDestroy()
        {
            if (seamTexture != null) Destroy(seamTexture);
            if (scanTexture != null) Destroy(scanTexture);
        }
    }
}
