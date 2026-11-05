using System;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
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
        public const float CardScreenFraction = 0.28f; // owner, 2026-11-04: a bigger screen hand
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

        // QA Q11 (2026-11-03): a shown hand covered the player's own console (and the avatar just placed in it). It
        // now rests lower, with the top of each card showing, and rises as the pointer comes down to it or while a
        // card is carried.
        public const float RestingVisibleFraction = 0.3f; // owner, 2026-11-04: lower when not in focus
        private const float PeekResponse = 12f;
        private float peekAmount;

        /// <summary>Set by the hand while one of its cards is hovered, picked up or carried: the tray stays up.</summary>
        public bool HeldUp { get; set; }

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

        // HAND-1 (2026-11-06): the pointer's horizontal position, as a share of the screen width.
        private static bool PointerWithinColumn(float halfWidthFraction)
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || Screen.width <= 0) return false;
            float x = pointer.position.ReadValue().x / Screen.width;
            return Mathf.Abs(x - 0.5f) <= halfWidthFraction;
        }

        private bool raised;

        /// <summary>
        /// HAND-2 (2026-11-06): set while a card taken out of this hand is carried above the hand's top edge. The
        /// hand then rests (only its strip showing) until the card comes back down to that strip or is let go, so a
        /// raised hand no longer hides, and catches drops meant for, the player's own Console under it.
        /// </summary>
        public bool CarriedOut { get; set; }

        private static bool PointerBelow(float viewportFraction)
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || Screen.height <= 0) return false;
            Vector2 position = pointer.position.ReadValue();
            return position.y <= viewportFraction * Screen.height;
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
            else
            {
                // Up only while the pointer is over the hand's strip, as in TTS; carrying a piece does not raise it
                // (owner, 2026-11-05).
                // HAND-1 (2026-11-06): it used to rise whenever the pointer was in the bottom third of the screen,
                // anywhere across its width, and the raised cards then took the clicks meant for the table under
                // them (the Ability pile, beside the Console). Now it rises only when the pointer reaches the part of
                // the cards that shows while they rest, within the row's own width, and stays up while the pointer
                // is over the raised row.
                float stripTop = BottomMarginFraction + (scaledDepth / viewportHeight) + 0.04f;
                float restingTop = BottomMarginFraction + (RestingVisibleFraction * scaledDepth / viewportHeight) + 0.02f;
                float rowHalfWidth = (((Mathf.Max(0f, rowWidth) * 0.5f) + BandMargin) * scale) / Mathf.Max(0.0001f, viewportWidth);
                raised = HeldUp || (PointerWithinColumn(rowHalfWidth) && PointerBelow(raised && !CarriedOut ? stripTop : restingTop));
                bool up = raised;
                float blend = 1f - Mathf.Exp(-PeekResponse * Time.unscaledDeltaTime);
                peekAmount = Mathf.Lerp(peekAmount, up ? 0f : 1f, blend);
                y -= peekAmount * scaledDepth * (1f - RestingVisibleFraction);
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

        /// <summary>
        /// CONSOLE-1 (2026-11-06): true when the screen point lies over the hand's drop band, the lower part of the
        /// cards where a card from the table goes into the hand (pure geometry, no physics). A hand card that was
        /// carried out of the hand comes back only here: the full tray (<see cref="ContainsScreenPoint"/>) reaches up
        /// over the player's own Console, so a card carried to a Console slot was pulled back into the hand.
        /// </summary>
        public bool ContainsDropBandScreenPoint(Vector2 screenPosition)
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
                && local.z <= bandDropTop;
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
