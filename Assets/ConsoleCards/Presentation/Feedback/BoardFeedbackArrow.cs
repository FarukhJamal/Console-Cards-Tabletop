using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>
    /// A dashed arc from a pawn to the tile it must move to (doc 25, F2), with an arrow head at the end.
    /// Points are written into a preallocated buffer; authored on the Final World prefab.
    /// </summary>
    public sealed class BoardFeedbackArrow : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField] private Transform head;
        [SerializeField] private Renderer headRenderer;
        [SerializeField, Range(4, 32)] private int segments = 16;
        [SerializeField] private float arcHeight = 0.6f;
        [SerializeField] private float lift = 0.05f;
        [SerializeField] private float dashScrollSpeed = 1.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private Vector3[] points;
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock lineProperties;

        public void Show(Vector3 from, Vector3 to, Color tint)
        {
            if (points == null || points.Length != segments + 1) points = new Vector3[segments + 1];
            from += Vector3.up * lift;
            to += Vector3.up * lift;
            for (int i = 0; i <= segments; i++)
            {
                float k = i / (float)segments;
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y += arcHeight * 4f * k * (1f - k);
                points[i] = p;
            }

            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startColor = tint;
            line.endColor = tint;
            if (head != null)
            {
                Vector3 direction = points[points.Length - 1] - points[points.Length - 2];
                direction.y = 0f;
                head.position = to;
                if (direction.sqrMagnitude > 1e-6f) head.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            }

            if (headRenderer != null)
            {
                if (properties == null) properties = new MaterialPropertyBlock();
                headRenderer.GetPropertyBlock(properties);
                properties.SetColor(BaseColorId, tint);
                properties.SetColor(ColorId, tint);
                headRenderer.SetPropertyBlock(properties);
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Update()
        {
            // Scrolls the dashes through a property block, never the shared material asset.
            if (line == null || dashScrollSpeed == 0f) return;
            if (lineProperties == null) lineProperties = new MaterialPropertyBlock();
            line.GetPropertyBlock(lineProperties);
            lineProperties.SetVector(BaseMapStId, new Vector4(1f, 1f, Mathf.Repeat(-Time.unscaledTime * dashScrollSpeed, 1f), 0f));
            line.SetPropertyBlock(lineProperties);
        }
    }
}
