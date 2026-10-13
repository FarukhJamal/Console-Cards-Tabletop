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

        public void Hide()
        {
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

            transform.localScale = baseScale * scale;
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
