using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>
    /// One board feedback marker (doc 25, F2): a pulsing ring, outline or warning, or a one-shot flash.
    /// It only animates its own transform and colour; nothing it does changes game state. Timing, size and
    /// motion are authored on the Final World prefab.
    /// </summary>
    public sealed class BoardFeedbackMarker : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [Header("Looping")]
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseScale = 0.08f;
        [SerializeField, Range(0f, 1f)] private float pulseAlpha = 0.35f;
        [SerializeField] private float bobHeight;
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float spinDegreesPerSecond;
        [Header("One shot (flash / burst)")]
        [SerializeField] private bool oneShot;
        [SerializeField, Min(0.05f)] private float oneShotSeconds = 0.6f;
        [SerializeField] private float oneShotGrow = 1.8f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock properties;
        private Vector3 basePosition;
        private Vector3 baseScale = Vector3.one;
        private Color color = Color.white;
        private float startedAt;
        private bool scaleCaptured;
        private float lifetime; // > 0: a timed show (the table ping) that fades out at the end
        private float size = 1f; // POL-3: a marker sized to the piece (1 = as authored, card sized)

        private const float TimedFadeSeconds = 0.5f;

        public bool IsShowing => gameObject.activeSelf;

        public void Show(Vector3 position, Color tint)
        {
            if (!scaleCaptured)
            {
                baseScale = transform.localScale;
                scaleCaptured = true;
            }

            basePosition = position;
            color = tint;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                startedAt = Time.unscaledTime;
            }
            else if (oneShot)
            {
                startedAt = Time.unscaledTime;
            }

            Animate();
        }

        /// <summary>
        /// A looping marker shown for a while (UX-1, the table ping): it pulses as authored, fades out over its last
        /// half second and hides itself. Showing it again restarts it.
        /// </summary>
        public void ShowFor(Vector3 position, Color tint, float seconds)
        {
            lifetime = Mathf.Max(0.1f, seconds);
            if (gameObject.activeSelf) startedAt = Time.unscaledTime;
            Show(position, tint);
            startedAt = Time.unscaledTime;
        }

        /// <summary>TOOLS-1: shown turned to a yaw (degrees about up), as for the outline around a selected card.</summary>
        public void ShowAt(Vector3 position, float yawDegrees, Color tint)
        {
            ShowAt(position, yawDegrees, tint, 1f);
        }

        /// <summary>POL-3: shown turned to a yaw and scaled to a piece (1 = card sized, as authored).</summary>
        public void ShowAt(Vector3 position, float yawDegrees, Color tint, float sizeMultiplier)
        {
            size = Mathf.Max(0.05f, sizeMultiplier);
            transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            Show(position, tint);
        }

        public void Hide()
        {
            lifetime = 0f;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Update()
        {
            Animate();
        }

        private void Animate()
        {
            float t = Time.unscaledTime - startedAt;
            float alpha = color.a;
            float scale = 1f;
            if (oneShot)
            {
                float k = Mathf.Clamp01(t / oneShotSeconds);
                if (k >= 1f)
                {
                    Hide();
                    return;
                }

                scale = Mathf.Lerp(1f, oneShotGrow, 1f - ((1f - k) * (1f - k)));
                alpha *= 1f - k;
            }
            else
            {
                float wave = Mathf.Sin(t * pulseSpeed);
                scale = 1f + (wave * pulseScale);
                alpha *= 1f - (pulseAlpha * 0.5f * (1f - wave));
            }

            if (lifetime > 0f)
            {
                if (t >= lifetime)
                {
                    Hide();
                    return;
                }

                alpha *= Mathf.Clamp01((lifetime - t) / TimedFadeSeconds);
            }

            transform.localScale = baseScale * (scale * size);
            Vector3 position = basePosition;
            if (bobHeight != 0f) position += Vector3.up * (bobHeight * (0.5f + (0.5f * Mathf.Sin(t * bobSpeed))));
            transform.position = position;
            if (spinDegreesPerSecond != 0f) transform.Rotate(0f, spinDegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.World);
            ApplyColour(new Color(color.r, color.g, color.b, alpha));
        }

        private void ApplyColour(Color tint)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null) continue;
                target.GetPropertyBlock(properties);
                properties.SetColor(BaseColorId, tint);
                properties.SetColor(ColorId, tint);
                target.SetPropertyBlock(properties);
            }
        }
    }
}
