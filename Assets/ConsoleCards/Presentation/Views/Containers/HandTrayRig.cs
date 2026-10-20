using System;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;
using UnityEngine.Rendering;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Views.Containers
{
    public enum HandTrayFeedback
    {
        None,
        Source,
        Valid,
        Invalid,
    }

    /// <summary>
    /// Runtime child of the target camera that hosts the local hand as a tray at the bottom centre of the
    /// viewport. The tray anchor faces the camera (its up points at the camera, its forward up the screen),
    /// so cards laid on it face the owner at any camera pitch or yaw. Its scale keeps a constant on-screen
    /// size in both perspective (fixed depth) and orthographic (scaled by orthographic size) projection.
    /// The drop band is a trigger collider that the composition enables only while a card drag is active.
    /// Presentation only: it never writes authoritative state.
    /// </summary>
    public sealed class HandTrayRig : MonoBehaviour
    {
        // Camera-space depth of the tray plane; raised past the near clip plane when needed.
        public const float TrayDistance = 0.75f;
        // Card depth (on-screen height of one card) as a fraction of the viewport height.
        public const float CardScreenFraction = 0.24f;
        public const float BottomMarginFraction = 0.02f;
        public const float MaximumWidthFraction = 0.92f;
        // Fraction of a card's height that stays on screen while the tray is collapsed.
        public const float CollapsedVisibleFraction = 0.28f;
        // Margin around the card row, in unscaled tray units.
        public const float BandMargin = 0.12f;
        // Seconds to slide the whole tray off (or back onto) the bottom of the screen (seat switch, H1b).
        public const float ClearSeconds = 0.3f;
        private const float BandBehindCards = 0.02f;
        private const float FrameThickness = 0.03f;
        private const float FrameBehindCards = 0.01f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color SourceColor = new Color(0.35f, 0.6f, 1f, 1f);
        private static readonly Color ValidColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.3f, 0.3f, 1f);

        private UnityCamera targetCamera;
        private Transform anchor;
        private BoxCollider bandCollider;
        private TabletopContainerDropTarget dropTarget;
        private readonly Renderer[] frameRenderers = new Renderer[4];
        private MaterialPropertyBlock propertyBlock;
        private HandTrayFeedback feedback;
        private float scale = 1f;
        private float bandHalfWidth;
        private float bandLowerDepth;
        private float bandUpperDepth;
        private float bandDropTop;
        private bool clearTarget;
        private float clearAmount;

        public UnityCamera TargetCamera => targetCamera;

        public Transform Anchor => anchor;

        /// <summary>World size of one unscaled tray unit for the current frame.</summary>
        public float Scale => scale;

        public bool IsCollapsed { get; private set; }

        /// <summary>
        /// The share of a card's height, from its bottom edge, where a card from the table is dropped into the hand
        /// (the drop band). Dragging a hand card keeps the whole tray (<see cref="ContainsScreenPoint"/>).
        /// </summary>
        public float DropHeightFraction { get; set; } = 0.55f;

        /// <summary>True once a requested clear has slid the whole tray below the screen.</summary>
        public bool IsCleared => clearTarget && clearAmount >= 1f;

        public bool IsDropTargetConfigured => dropTarget != null && dropTarget.IsConfigured;

        public static HandTrayRig Create(UnityCamera camera, int dropTargetLayer, Material frameMaterial)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (frameMaterial == null)
            {
                throw new ArgumentNullException(nameof(frameMaterial));
            }

            GameObject root = new GameObject("Hand Tray Rig");
            root.transform.SetParent(camera.transform, false);
            HandTrayRig rig = root.AddComponent<HandTrayRig>();
            rig.Build(camera, dropTargetLayer, frameMaterial);
            return rig;
        }

        public void Activate()
        {
            gameObject.SetActive(true);
        }

        /// <summary>Teardown: clears and disables the drop band, hides feedback, and hides the rig.</summary>
        public void Deactivate()
        {
            ClearDropTarget();
            ShowFeedback(HandTrayFeedback.None);
            gameObject.SetActive(false);
        }

        public void SetCollapsed(bool collapsed)
        {
            IsCollapsed = collapsed;
        }

        /// <summary>
        /// Slides the whole tray (and every card on it) below the screen, or back. Used to empty the screen before
        /// the screen changes seat; a Presentation offset only.
        /// </summary>
        public void SetCleared(bool cleared, bool immediate = false)
        {
            clearTarget = cleared;
            if (immediate)
            {
                clearAmount = cleared ? 1f : 0f;
            }
        }

        private void Update()
        {
            float target = clearTarget ? 1f : 0f;
            if (clearAmount != target)
            {
                clearAmount = Mathf.MoveTowards(clearAmount, target, Time.unscaledDeltaTime / ClearSeconds);
            }
        }

        public void ConfigureDropTarget(IContainerView handView)
        {
            dropTarget.Configure(handView, bandCollider);
            SetDropTargetActive(false);
        }

        public void ClearDropTarget()
        {
            if (dropTarget == null)
            {
                return;
            }

            dropTarget.ClearConfiguration();
            dropTarget.enabled = false;
            bandCollider.enabled = false;
        }

        /// <summary>The band is hit-tested only while a card drag is in progress.</summary>
        public void SetDropTargetActive(bool active)
        {
            bool enable = active && dropTarget.IsConfigured && gameObject.activeInHierarchy;
            if (bandCollider.enabled != enable)
            {
                bandCollider.enabled = enable;
            }

            if (dropTarget.enabled != enable)
            {
                dropTarget.enabled = enable;
            }
        }

        public void ShowFeedback(HandTrayFeedback state)
        {
            if (propertyBlock == null)
            {
                return;
            }

            feedback = state;
            bool visible = state != HandTrayFeedback.None;
            Color color = state == HandTrayFeedback.Valid
                ? ValidColor
                : state == HandTrayFeedback.Invalid ? InvalidColor : SourceColor;
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            for (int i = 0; i < frameRenderers.Length; i++)
            {
                frameRenderers[i].SetPropertyBlock(propertyBlock);
                frameRenderers[i].enabled = visible;
            }

            if (visible)
            {
                LayoutFrame();
            }
        }

        /// <summary>
        /// Places the anchor for this frame. cardDepth and rowWidth are in unscaled card units;
        /// upperAllowance extends the band up the screen to cover lifted cards.
        /// </summary>
        public void UpdateFrame(float cardDepth, float rowWidth, float upperAllowance)
        {
            if (targetCamera == null)
            {
                return;
            }

            float distance = Mathf.Max(TrayDistance, targetCamera.nearClipPlane + 0.05f);
            float halfHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize
                : distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float viewportHeight = 2f * halfHeight;
            float viewportWidth = viewportHeight * targetCamera.aspect;
            float depth = Mathf.Max(0.0001f, cardDepth);
            float width = Mathf.Max(0.0001f, rowWidth + (2f * BandMargin));
            scale = Mathf.Min(
                CardScreenFraction * viewportHeight / depth,
                MaximumWidthFraction * viewportWidth / width);

            float scaledDepth = depth * scale;
            float y = -halfHeight + (BottomMarginFraction * viewportHeight) + (scaledDepth * 0.5f);
            if (IsCollapsed)
            {
                y -= scaledDepth * (1f - CollapsedVisibleFraction);
            }

            if (clearAmount > 0f)
            {
                // Eased slide to fully below the screen, lifted cards included.
                float eased = clearAmount * clearAmount * (3f - (2f * clearAmount));
                y -= eased * ((scaledDepth * 1.8f) + (BottomMarginFraction * viewportHeight));
            }

            anchor.localPosition = new Vector3(0f, y, distance);
            anchor.localScale = new Vector3(scale, scale, scale);

            bandHalfWidth = (rowWidth * 0.5f) + BandMargin;
            bandLowerDepth = (depth * 0.5f) + BandMargin;
            bandUpperDepth = (depth * 0.5f) + BandMargin + Mathf.Max(0f, upperAllowance);
            // Drops from the table land only on the lower part of the cards (all of the strip left on screen when
            // the tray is collapsed), so a card let go above the hand stays on the table.
            bandDropTop = IsCollapsed
                ? depth * 0.5f
                : (-depth * 0.5f) + (depth * Mathf.Clamp(DropHeightFraction, 0.2f, 1f));
            bandCollider.center = new Vector3(0f, -BandBehindCards, (bandDropTop - bandLowerDepth) * 0.5f);
            bandCollider.size = new Vector3(bandHalfWidth * 2f, 0.01f, bandLowerDepth + bandDropTop);

            if (feedback != HandTrayFeedback.None)
            {
                LayoutFrame();
            }
        }

        /// <summary>True when the screen point lies over the tray band (pure geometry, no physics).</summary>
        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            if (targetCamera == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            Ray ray = targetCamera.ScreenPointToRay(screenPosition);
            Plane plane = new Plane(anchor.up, anchor.position);
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 local = anchor.InverseTransformPoint(ray.GetPoint(enter));
            return Mathf.Abs(local.x) <= bandHalfWidth
                && local.z >= -bandLowerDepth
                && local.z <= bandUpperDepth;
        }

        private void Build(UnityCamera camera, int dropTargetLayer, Material frameMaterial)
        {
            targetCamera = camera;
            GameObject anchorObject = new GameObject("Tray Anchor");
            anchor = anchorObject.transform;
            anchor.SetParent(transform, false);
            // Camera-local: forward = screen up, up = toward the camera, right = screen right.
            anchor.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);

            GameObject band = new GameObject("Tray Drop Band");
            band.layer = dropTargetLayer;
            band.transform.SetParent(anchor, false);
            bandCollider = band.AddComponent<BoxCollider>();
            bandCollider.isTrigger = true;
            bandCollider.enabled = false;
            dropTarget = band.AddComponent<TabletopContainerDropTarget>();
            dropTarget.enabled = false;

            for (int i = 0; i < frameRenderers.Length; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Tray Frame {i}";
                Collider barCollider = bar.GetComponent<Collider>();
                if (barCollider != null)
                {
                    DestroyImmediate(barCollider);
                }

                bar.transform.SetParent(anchor, false);
                Renderer barRenderer = bar.GetComponent<Renderer>();
                barRenderer.sharedMaterial = frameMaterial;
                barRenderer.shadowCastingMode = ShadowCastingMode.Off;
                barRenderer.receiveShadows = false;
                barRenderer.enabled = false;
                frameRenderers[i] = barRenderer;
            }

            propertyBlock = new MaterialPropertyBlock();
            feedback = HandTrayFeedback.None;
        }

        private void LayoutFrame()
        {
            // The frame outlines where a drop lands (the drop band).
            float width = bandHalfWidth * 2f;
            float depth = bandLowerDepth + bandDropTop;
            float centerZ = (bandDropTop - bandLowerDepth) * 0.5f;
            SetBar(0, new Vector3(0f, -FrameBehindCards, bandDropTop), new Vector3(width, 0.005f, FrameThickness));
            SetBar(1, new Vector3(0f, -FrameBehindCards, -bandLowerDepth), new Vector3(width, 0.005f, FrameThickness));
            SetBar(2, new Vector3(-bandHalfWidth, -FrameBehindCards, centerZ), new Vector3(FrameThickness, 0.005f, depth));
            SetBar(3, new Vector3(bandHalfWidth, -FrameBehindCards, centerZ), new Vector3(FrameThickness, 0.005f, depth));
        }

        private void SetBar(int index, Vector3 localPosition, Vector3 localScale)
        {
            Transform bar = frameRenderers[index].transform;
            bar.localPosition = localPosition;
            bar.localRotation = Quaternion.identity;
            bar.localScale = localScale;
        }
    }
}
