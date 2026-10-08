using System;
using ConsoleCards.GameTemplates;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConsoleCards.Presentation.Views.Containers
{
    public enum PileBayFeedback
    {
        None,
        Source,
        Valid,
        Invalid,
    }

    /// <summary>
    /// A pile's bay (doc 19 §12.1, doc 21 principle 17): a thin frame drawn flat on the table at the pile's
    /// footprint, with a slot mouth on the edge facing its owner's Console (the near edge when it has none) and
    /// a mark from the system set. Lives in each pile prefab ("Bay" child) and reads the shared PileBayStyle,
    /// so every bay in every game looks the same. Always shown; drop feedback tints and thickens the frame.
    /// Built from collider-less bars, so it never blocks pieces. Presentation only.
    /// </summary>
    public sealed class PileBayView : MonoBehaviour
    {
        private const int FrameBarCount = 8;
        private const int MarkBarCount = 3;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private PileBayStyle style;

        private readonly Renderer[] frameBars = new Renderer[FrameBarCount];
        private readonly Renderer[] markBars = new Renderer[MarkBarCount];
        private MaterialPropertyBlock propertyBlock;
        private Transform mouthTarget;
        private GameTemplateBayMark mark;
        private float halfWidth;
        private float halfDepth;
        private BayEdge mouthEdge;
        private PileBayFeedback feedback;
        private bool isBuilt;

        private enum BayEdge
        {
            Near,
            Far,
            Left,
            Right,
        }

        public PileBayStyle Style => style;

        public PileBayFeedback Feedback => feedback;

        public GameTemplateBayMark Mark => mark;

        /// <summary>
        /// Sizes the bay to the pile footprint (outer size, world units) and sets its mark. Builds the bars on
        /// the first call; later calls re-lay them out. Call once when the pile is created, not per frame.
        /// </summary>
        public void Configure(float footprintWidth, float footprintDepth, GameTemplateBayMark bayMark)
        {
            if (style == null)
            {
                throw new InvalidOperationException($"{name} requires a Pile Bay Style.");
            }

            style.Validate();
            if (!Enum.IsDefined(typeof(GameTemplateBayMark), bayMark))
            {
                throw new ArgumentOutOfRangeException(nameof(bayMark));
            }

            if (!isBuilt)
            {
                CreateBars(frameBars);
                CreateBars(markBars);
                propertyBlock = new MaterialPropertyBlock();
                feedback = PileBayFeedback.None;
                mouthEdge = ResolveMouthEdge();
                isBuilt = true;
            }

            transform.localPosition = new Vector3(0f, style.Lift, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            mark = bayMark;
            halfWidth = Mathf.Max(0.01f, footprintWidth) * 0.5f;
            halfDepth = Mathf.Max(0.01f, footprintDepth) * 0.5f;
            LayoutFrame();
            LayoutMark();
            ApplyColor();
        }

        /// <summary>The Console the mouth should face; null faces the near edge (local -z).</summary>
        public void SetMouthTarget(Transform target)
        {
            mouthTarget = target;
            RefreshMouthEdge();
        }

        public void ShowFeedback(PileBayFeedback state)
        {
            if (!isBuilt || state == feedback)
            {
                return;
            }

            feedback = state;
            ApplyColor();
            LayoutFrame();
        }

        private void LateUpdate()
        {
            if (isBuilt && mouthTarget != null)
            {
                RefreshMouthEdge();
            }
        }

        private void RefreshMouthEdge()
        {
            if (!isBuilt)
            {
                return;
            }

            BayEdge edge = ResolveMouthEdge();
            if (edge != mouthEdge)
            {
                mouthEdge = edge;
                LayoutFrame();
            }
        }

        private BayEdge ResolveMouthEdge()
        {
            if (mouthTarget == null)
            {
                return BayEdge.Near;
            }

            Vector3 local = transform.InverseTransformPoint(mouthTarget.position);
            if (Mathf.Abs(local.x) >= Mathf.Abs(local.z))
            {
                return local.x < 0f ? BayEdge.Left : BayEdge.Right;
            }

            return local.z < 0f ? BayEdge.Near : BayEdge.Far;
        }

        private void LayoutFrame()
        {
            float thickness = feedback == PileBayFeedback.None
                ? style.BarThickness
                : style.BarThickness * style.FeedbackThicknessScale;
            float opening = style.MouthHalfOpening;
            int bar = 0;
            for (int edge = 0; edge < 4; edge++)
            {
                GetEdge((BayEdge)edge, out Vector2 start, out Vector2 end, out Vector2 inward);
                if ((BayEdge)edge != mouthEdge)
                {
                    SetBar(frameBars[bar++], start, end, thickness);
                    continue;
                }

                Vector2 tangent = (end - start).normalized;
                Vector2 middle = (start + end) * 0.5f;
                Vector2 openA = middle - (tangent * opening);
                Vector2 openB = middle + (tangent * opening);
                Vector2 insetA = middle - (tangent * (opening - style.MouthSlant)) + (inward * style.MouthInset);
                Vector2 insetB = middle + (tangent * (opening - style.MouthSlant)) + (inward * style.MouthInset);
                SetBar(frameBars[bar++], start, openA, thickness);
                SetBar(frameBars[bar++], openB, end, thickness);
                SetBar(frameBars[bar++], openA, insetA, thickness);
                SetBar(frameBars[bar++], openB, insetB, thickness);
                SetBar(frameBars[bar++], insetA, insetB, thickness);
            }
        }

        // System marks in the middle of the bay, drawn from bars and pointing to local -z: Draw is a down
        // arrow, Discard a cross over a line. None hides the mark bars. A pile covers its mark while it has cards.
        private void LayoutMark()
        {
            float s = style.MarkScale;
            float t = style.MarkThickness;
            switch (mark)
            {
                case GameTemplateBayMark.Draw:
                    SetBar(markBars[0], new Vector2(0f, 0.22f) * s, new Vector2(0f, -0.2f) * s, t);
                    SetBar(markBars[1], new Vector2(-0.15f, -0.05f) * s, new Vector2(0f, -0.2f) * s, t);
                    SetBar(markBars[2], new Vector2(0.15f, -0.05f) * s, new Vector2(0f, -0.2f) * s, t);
                    SetMarkVisible(true);
                    break;
                case GameTemplateBayMark.Discard:
                    SetBar(markBars[0], new Vector2(-0.16f, 0.2f) * s, new Vector2(0.16f, -0.12f) * s, t);
                    SetBar(markBars[1], new Vector2(0.16f, 0.2f) * s, new Vector2(-0.16f, -0.12f) * s, t);
                    SetBar(markBars[2], new Vector2(-0.2f, -0.26f) * s, new Vector2(0.2f, -0.26f) * s, t);
                    SetMarkVisible(true);
                    break;
                default:
                    SetMarkVisible(false);
                    break;
            }
        }

        private void SetMarkVisible(bool visible)
        {
            for (int i = 0; i < markBars.Length; i++)
            {
                markBars[i].enabled = visible;
            }
        }

        private void ApplyColor()
        {
            Color color = feedback == PileBayFeedback.Valid
                ? style.ValidColor
                : feedback == PileBayFeedback.Invalid
                    ? style.InvalidColor
                    : feedback == PileBayFeedback.Source ? style.SourceColor : style.RestColor;
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            for (int i = 0; i < frameBars.Length; i++)
            {
                frameBars[i].SetPropertyBlock(propertyBlock);
            }

            for (int i = 0; i < markBars.Length; i++)
            {
                markBars[i].SetPropertyBlock(propertyBlock);
            }
        }

        private void GetEdge(BayEdge edge, out Vector2 start, out Vector2 end, out Vector2 inward)
        {
            switch (edge)
            {
                case BayEdge.Far:
                    start = new Vector2(-halfWidth, halfDepth);
                    end = new Vector2(halfWidth, halfDepth);
                    inward = new Vector2(0f, -1f);
                    return;
                case BayEdge.Left:
                    start = new Vector2(-halfWidth, -halfDepth);
                    end = new Vector2(-halfWidth, halfDepth);
                    inward = new Vector2(1f, 0f);
                    return;
                case BayEdge.Right:
                    start = new Vector2(halfWidth, -halfDepth);
                    end = new Vector2(halfWidth, halfDepth);
                    inward = new Vector2(-1f, 0f);
                    return;
                default:
                    start = new Vector2(-halfWidth, -halfDepth);
                    end = new Vector2(halfWidth, -halfDepth);
                    inward = new Vector2(0f, 1f);
                    return;
            }
        }

        // Places a flat bar from a to b (local x/z), extended by its thickness so corners close. The bar is
        // drawn inside the footprint edge, so the bay never reaches past the pile footprint.
        private void SetBar(Renderer bar, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            Transform barTransform = bar.transform;
            Vector2 middle = (a + b) * 0.5f;
            Vector2 inset = InsetTowardCentre(middle, thickness * 0.5f);
            barTransform.localPosition = new Vector3(inset.x, 0f, inset.y);
            barTransform.localRotation = length > 0f
                ? Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.y), Vector3.up)
                : Quaternion.identity;
            barTransform.localScale = new Vector3(thickness, style.BarHeight, length);
        }

        // Moves a point on an outer edge inward by the given amount, so a bar's outer face lies on the edge.
        private Vector2 InsetTowardCentre(Vector2 point, float amount)
        {
            float x = point.x;
            float y = point.y;
            if (Mathf.Abs(Mathf.Abs(x) - halfWidth) < 0.0001f)
            {
                x -= Mathf.Sign(x) * amount;
            }

            if (Mathf.Abs(Mathf.Abs(y) - halfDepth) < 0.0001f)
            {
                y -= Mathf.Sign(y) * amount;
            }

            return new Vector2(x, y);
        }

        private void CreateBars(Renderer[] bars)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Bay Bar {i}";
                bar.layer = gameObject.layer;
                Collider barCollider = bar.GetComponent<Collider>();
                if (barCollider != null)
                {
                    DestroyImmediate(barCollider);
                }

                bar.transform.SetParent(transform, false);
                Renderer barRenderer = bar.GetComponent<Renderer>();
                barRenderer.sharedMaterial = style.Material;
                barRenderer.shadowCastingMode = ShadowCastingMode.Off;
                barRenderer.receiveShadows = false;
                bars[i] = barRenderer;
            }
        }
    }
}
