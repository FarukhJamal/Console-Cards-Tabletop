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

        private readonly Renderer[] bars = new Renderer[4];
        private Func<ContainerId, ContainerState> stateOf;
        private ContainerId containerId;
        private BoxCollider zone;
        private TabletopContainerDropTarget target;
        private MaterialPropertyBlock block;
        private Vector2 shownSize;
        private Color shownColour;
        private bool framed = true;

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
            bool acceptsDrops)
        {
            bool rebind = hand != containerId || !ReferenceEquals(states, stateOf) || target.IsConfigured != acceptsDrops;
            containerId = hand;
            stateOf = states;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(centre, rotation);
            if (size != shownSize)
            {
                shownSize = size;
                zone.center = new Vector3(0f, ZoneHeight * 0.5f, 0f);
                zone.size = new Vector3(size.x, ZoneHeight, size.y);
                float halfX = size.x * 0.5f;
                float halfZ = size.y * 0.5f;
                SetBar(0, new Vector3(0f, FrameHeight, halfZ), new Vector3(size.x, FrameHeight, FrameThickness));
                SetBar(1, new Vector3(0f, FrameHeight, -halfZ), new Vector3(size.x, FrameHeight, FrameThickness));
                SetBar(2, new Vector3(-halfX, FrameHeight, 0f), new Vector3(FrameThickness, FrameHeight, size.y));
                SetBar(3, new Vector3(halfX, FrameHeight, 0f), new Vector3(FrameThickness, FrameHeight, size.y));
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

        /// <summary>True when the ray meets the table inside this zone (plane test, no physics, no allocation).</summary>
        internal bool Contains(Ray ray)
        {
            if (!gameObject.activeInHierarchy) return false;
            Vector3 origin = transform.InverseTransformPoint(ray.origin);
            Vector3 direction = transform.InverseTransformDirection(ray.direction);
            if (Mathf.Abs(direction.y) < 0.0001f) return false;
            float along = -origin.y / direction.y;
            if (along < 0f) return false;
            Vector3 hit = origin + (direction * along);
            return Mathf.Abs(hit.x) <= shownSize.x * 0.5f && Mathf.Abs(hit.z) <= shownSize.y * 0.5f;
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
