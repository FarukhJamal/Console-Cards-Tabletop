using ConsoleCards.Core.Domain.Containers;
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
    /// A pile's bay: a thin frame drawn flat on the table around a plate-less pile, with a slot mouth on the
    /// edge facing its owner's Console (the near edge when it has none) and an optional system mark. One
    /// platform look for every game (doc 21). A fixed pile (declared by the template) always shows its bay; a
    /// free pile shows it only while empty. Drop feedback tints the frame. Built from collider-less bars, so it
    /// never blocks pieces. Presentation only: it never writes authoritative state.
    /// </summary>
    public sealed class PileBayView : MonoBehaviour
    {
        // Margin around the card footprint, and the bar sizes, in world units.
        public const float FootprintMargin = 0.08f;
        private const float BarThickness = 0.035f;
        private const float FeedbackThicknessScale = 1.7f;
        private const float BarHeight = 0.004f;
        private const float Lift = 0.004f;
        private const float MouthHalfOpening = 0.18f;
        private const float MouthInset = 0.07f;
        private const float MouthSlant = 0.05f;
        private const float MarkThickness = 0.04f;
        private const int FrameBarCount = 8;
        private const int MarkBarCount = 3;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color RestColor = new Color(0.55f, 0.66f, 0.63f, 1f);
        private static readonly Color SourceColor = new Color(0.45f, 0.62f, 1f, 1f);
        private static readonly Color ValidColor = new Color(0.45f, 0.95f, 0.6f, 1f);
        private static readonly Color InvalidColor = new Color(0.75f, 0.22f, 0.18f, 1f);

        private readonly Renderer[] frameBars = new Renderer[FrameBarCount];
        private readonly Renderer[] markBars = new Renderer[MarkBarCount];
        private MaterialPropertyBlock propertyBlock;
        private IContainerView pileView;
        private Transform mouthTarget;
        private GameTemplateBayMark mark;
        private bool fixedSpot;
        private float halfWidth;
        private float halfDepth;
        private BayEdge mouthEdge;
        private PileBayFeedback feedback;
        private bool visible;

        private enum BayEdge
        {
            Near,
            Far,
            Left,
            Right,
        }

        public PileBayFeedback Feedback => feedback;

        public bool IsFixedSpot => fixedSpot;

        /// <summary>
        /// Creates a bay under the pile root. pileView may be null (a placement preview), which always shows.
        /// footprintWidth and footprintDepth are the card footprint in world units (the margin is added here).
        /// </summary>
        public static PileBayView Create(
            Transform pileRoot,
            Material material,
            float footprintWidth,
            float footprintDepth,
            GameTemplateBayMark bayMark,
            bool isFixedSpot,
            IContainerView view)
        {
            GameObject root = new GameObject("Pile Bay");
            root.transform.SetParent(pileRoot, false);
            root.transform.localPosition = new Vector3(0f, Lift, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            PileBayView bay = root.AddComponent<PileBayView>();
            bay.Build(material, footprintWidth, footprintDepth, bayMark, isFixedSpot, view);
            return bay;
        }

        /// <summary>The Console the mouth should face; null faces the near edge (local -z).</summary>
        public void SetMouthTarget(Transform target)
        {
            mouthTarget = target;
            BayEdge edge = ResolveMouthEdge();
            if (edge != mouthEdge)
            {
                mouthEdge = edge;
                LayoutFrame();
            }
        }

        public void ShowFeedback(PileBayFeedback state)
        {
            if (state == feedback || propertyBlock == null)
            {
                return;
            }

            feedback = state;
            ApplyColor();
            LayoutFrame();
            RefreshVisibility();
        }

        private void Build(
            Material material,
            float footprintWidth,
            float footprintDepth,
            GameTemplateBayMark bayMark,
            bool isFixedSpot,
            IContainerView view)
        {
            mark = bayMark;
            fixedSpot = isFixedSpot;
            pileView = view;
            halfWidth = (Mathf.Max(0.01f, footprintWidth) * 0.5f) + FootprintMargin;
            halfDepth = (Mathf.Max(0.01f, footprintDepth) * 0.5f) + FootprintMargin;
            CreateBars(material, frameBars);
            CreateBars(material, markBars);
            propertyBlock = new MaterialPropertyBlock();
            feedback = PileBayFeedback.None;
            mouthEdge = BayEdge.Near;
            LayoutFrame();
            LayoutMark();
            ApplyColor();
            visible = true;
            RefreshVisibility();
        }

        private void LateUpdate()
        {
            if (mouthTarget != null)
            {
                BayEdge edge = ResolveMouthEdge();
                if (edge != mouthEdge)
                {
                    mouthEdge = edge;
                    LayoutFrame();
                }
            }

            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            bool show = fixedSpot
                || feedback != PileBayFeedback.None
                || pileView == null
                || !pileView.IsBound
                || IsEmpty(pileView.ContainerState);
            if (show == visible)
            {
                return;
            }

            visible = show;
            for (int i = 0; i < frameBars.Length; i++)
            {
                frameBars[i].enabled = show;
            }

            bool showMark = show && mark != GameTemplateBayMark.None;
            for (int i = 0; i < markBars.Length; i++)
            {
                markBars[i].enabled = showMark && markBars[i].transform.localScale.z > 0f;
            }
        }

        private static bool IsEmpty(ContainerState container)
        {
            return container == null || container.Count == 0;
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
                ? BarThickness
                : BarThickness * FeedbackThicknessScale;
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
                Vector2 openA = middle - (tangent * MouthHalfOpening);
                Vector2 openB = middle + (tangent * MouthHalfOpening);
                Vector2 insetA = middle - (tangent * (MouthHalfOpening - MouthSlant)) + (inward * MouthInset);
                Vector2 insetB = middle + (tangent * (MouthHalfOpening - MouthSlant)) + (inward * MouthInset);
                SetBar(frameBars[bar++], start, openA, thickness);
                SetBar(frameBars[bar++], openB, end, thickness);
                SetBar(frameBars[bar++], openA, insetA, thickness);
                SetBar(frameBars[bar++], openB, insetB, thickness);
                SetBar(frameBars[bar++], insetA, insetB, thickness);
            }
        }

        // System marks, drawn from bars and pointing to local -z: Draw is a down arrow, Discard a cross over
        // a line. None hides the mark bars.
        private void LayoutMark()
        {
            switch (mark)
            {
                case GameTemplateBayMark.Draw:
                    SetBar(markBars[0], new Vector2(0f, 0.22f), new Vector2(0f, -0.2f), MarkThickness);
                    SetBar(markBars[1], new Vector2(-0.15f, -0.05f), new Vector2(0f, -0.2f), MarkThickness);
                    SetBar(markBars[2], new Vector2(0.15f, -0.05f), new Vector2(0f, -0.2f), MarkThickness);
                    break;
                case GameTemplateBayMark.Discard:
                    SetBar(markBars[0], new Vector2(-0.16f, 0.2f), new Vector2(0.16f, -0.12f), MarkThickness);
                    SetBar(markBars[1], new Vector2(0.16f, 0.2f), new Vector2(-0.16f, -0.12f), MarkThickness);
                    SetBar(markBars[2], new Vector2(-0.2f, -0.26f), new Vector2(0.2f, -0.26f), MarkThickness);
                    break;
                default:
                    for (int i = 0; i < markBars.Length; i++)
                    {
                        markBars[i].transform.localScale = Vector3.zero;
                        markBars[i].enabled = false;
                    }

                    break;
            }
        }

        private void ApplyColor()
        {
            Color color = feedback == PileBayFeedback.Valid
                ? ValidColor
                : feedback == PileBayFeedback.Invalid
                    ? InvalidColor
                    : feedback == PileBayFeedback.Source ? SourceColor : RestColor;
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

        // Places a flat bar from a to b (local x/z), extended by its thickness so corners close.
        private static void SetBar(Renderer bar, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            Transform barTransform = bar.transform;
            Vector2 middle = (a + b) * 0.5f;
            barTransform.localPosition = new Vector3(middle.x, 0f, middle.y);
            barTransform.localRotation = length > 0f
                ? Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.y), Vector3.up)
                : Quaternion.identity;
            barTransform.localScale = new Vector3(thickness, BarHeight, length + thickness);
        }

        private void CreateBars(Material material, Renderer[] bars)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Bay Bar {i}";
                Collider barCollider = bar.GetComponent<Collider>();
                if (barCollider != null)
                {
                    DestroyImmediate(barCollider);
                }

                bar.transform.SetParent(transform, false);
                Renderer barRenderer = bar.GetComponent<Renderer>();
                barRenderer.sharedMaterial = material;
                barRenderer.shadowCastingMode = ShadowCastingMode.Off;
                barRenderer.receiveShadows = false;
                bars[i] = barRenderer;
            }
        }
    }
}
