using System;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// P-1 (owner, 2026-11-13, as in Tabletop Simulator): a seat's hand area, a marked zone on the table in front of
    /// the seat. A card dropped in it goes into that seat's Hand, through the same drop target and transfer command
    /// as any pile (so the move is replicated as before). The zone is a thin trigger on the drop-target layer plus a
    /// faint frame in the seat's colour. Moved only by the composition; nothing is allocated per frame.
    /// </summary>
    internal sealed class TableHandArea : MonoBehaviour, IContainerView
    {
        private const float FrameThickness = 0.03f;
        private const float FrameHeight = 0.006f;
        private const float ZoneHeight = 0.08f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Renderer[] bars = new Renderer[12];
        private Func<ContainerId, ContainerState> stateOf;
        private ContainerId containerId;
        private BoxCollider zone;
        private TabletopContainerDropTarget target;
        private MaterialPropertyBlock block;
        private Vector2 shownSize;
        private Color shownColour;
        private bool framed = true;
        private float shownHeight = -1f;
        private float shownBelow = -1f;

        public bool IsBound => !containerId.IsEmpty && ContainerState != null;

        public ContainerId ContainerId => containerId;

        public ContainerState ContainerState => stateOf != null && !containerId.IsEmpty ? stateOf(containerId) : null;

        internal static TableHandArea Create(Transform parent, string name, int layer, Material frameMaterial)
        {
            GameObject root = new GameObject(name);
            root.layer = layer;
            root.transform.SetParent(parent, false);
            TableHandArea area = root.AddComponent<TableHandArea>();
            area.zone = root.AddComponent<BoxCollider>();
            area.zone.isTrigger = true;
            area.target = root.AddComponent<TabletopContainerDropTarget>();
            area.block = new MaterialPropertyBlock();
            for (int i = 0; i < area.bars.Length; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Hand Area Frame {i}";
                Collider barCollider = bar.GetComponent<Collider>();
                if (barCollider != null) Destroy(barCollider);
                bar.layer = layer;
                bar.transform.SetParent(root.transform, false);
                Renderer barRenderer = bar.GetComponent<Renderer>();
                if (frameMaterial != null) barRenderer.sharedMaterial = frameMaterial;
                barRenderer.shadowCastingMode = ShadowCastingMode.Off;
                barRenderer.receiveShadows = false;
                area.bars[i] = barRenderer;
            }

            root.SetActive(false);
            return area;
        }

        /// <summary>Shows the zone for this hand at this place; size is its width and depth on the table.</summary>
        internal void Show(
            ContainerId hand,
            Func<ContainerId, ContainerState> states,
            Vector3 centre,
            Quaternion rotation,
            Vector2 size,
            Color colour,
            bool acceptsDrops,
            float height = ZoneHeight,
            float below = 0f)
        {
            bool rebind = hand != containerId || !ReferenceEquals(states, stateOf) || target.IsConfigured != acceptsDrops;
            containerId = hand;
            stateOf = states;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(centre, rotation);
            if (size != shownSize || !Mathf.Approximately(height, shownHeight) || !Mathf.Approximately(below, shownBelow))
            {
                shownSize = size;
                shownHeight = height;
                shownBelow = below;
                zone.center = new Vector3(0f, (height * 0.5f) - below, 0f);
                zone.size = new Vector3(size.x, height, size.y);
                // The box's twelve edges, drawn as thin bars (TTS: a thin outlined box while a card is held over it).
                float halfX = size.x * 0.5f;
                float halfZ = size.y * 0.5f;
                float low = -below;
                float high = height - below;
                float midY = (low + high) * 0.5f;
                for (int level = 0; level < 2; level++)
                {
                    float y = level == 0 ? low : high;
                    SetBar((level * 4) + 0, new Vector3(0f, y, halfZ), new Vector3(size.x, FrameThickness, FrameThickness));
                    SetBar((level * 4) + 1, new Vector3(0f, y, -halfZ), new Vector3(size.x, FrameThickness, FrameThickness));
                    SetBar((level * 4) + 2, new Vector3(-halfX, y, 0f), new Vector3(FrameThickness, FrameThickness, size.y));
                    SetBar((level * 4) + 3, new Vector3(halfX, y, 0f), new Vector3(FrameThickness, FrameThickness, size.y));
                }

                SetBar(8, new Vector3(-halfX, midY, -halfZ), new Vector3(FrameThickness, height, FrameThickness));
                SetBar(9, new Vector3(halfX, midY, -halfZ), new Vector3(FrameThickness, height, FrameThickness));
                SetBar(10, new Vector3(-halfX, midY, halfZ), new Vector3(FrameThickness, height, FrameThickness));
                SetBar(11, new Vector3(halfX, midY, halfZ), new Vector3(FrameThickness, height, FrameThickness));
            }

            if (colour != shownColour)
            {
                shownColour = colour;
                block.SetColor(BaseColorId, colour);
                block.SetColor(ColorId, colour);
                for (int i = 0; i < bars.Length; i++) bars[i].SetPropertyBlock(block);
            }

            // An area shown only (another player's hand outside Free) takes no drops: a card let go there stays on the table.
            if (zone.enabled != acceptsDrops) zone.enabled = acceptsDrops;
            if (rebind)
            {
                if (acceptsDrops && IsBound) target.Configure(this, zone);
                else target.ClearConfiguration();
            }
        }

        /// <summary>Owner, 2026-11-13: the frame is a hidden zone, lit only while the pointer is over it.</summary>
        internal void SetHighlighted(bool lit)
        {
            if (framed == lit) return;
            framed = lit;
            for (int i = 0; i < bars.Length; i++) bars[i].enabled = lit;
        }

        /// <summary>True when the ray passes through this zone's box (no physics, no allocation).</summary>
        internal bool Contains(Ray ray)
        {
            if (!gameObject.activeInHierarchy) return false;
            Vector3 origin = transform.InverseTransformPoint(ray.origin);
            Vector3 direction = transform.InverseTransformDirection(ray.direction);
            Vector3 min = zone.center - (zone.size * 0.5f);
            Vector3 max = zone.center + (zone.size * 0.5f);
            float near = 0f;
            float far = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = origin[axis];
                float d = direction[axis];
                if (Mathf.Abs(d) < 0.000001f)
                {
                    if (o < min[axis] || o > max[axis]) return false;
                    continue;
                }

                float a = (min[axis] - o) / d;
                float b = (max[axis] - o) / d;
                if (a > b) { float swap = a; a = b; b = swap; }
                near = Mathf.Max(near, a);
                far = Mathf.Min(far, b);
                if (near > far) return false;
            }

            return true;
        }

        internal void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void SetBar(int index, Vector3 localPosition, Vector3 localScale)
        {
            Transform bar = bars[index].transform;
            bar.localPosition = localPosition;
            bar.localRotation = Quaternion.identity;
            bar.localScale = localScale;
        }
    }
}
