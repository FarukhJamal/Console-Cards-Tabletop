using ConsoleCards.Presentation.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConsoleCards.Presentation.Views.Containers
{
    /// <summary>
    /// The local hand's zone on the table, as in Tabletop Simulator: no model, only a trigger drop target the
    /// size of the zone's extent and a thin outline. The composition enables the drop target only while a
    /// card drag is in progress; the outline then shows when the pointer is near the zone and highlights
    /// while the pointer is inside it. Presentation only: the zone pose comes from the hand's placement.
    /// </summary>
    public sealed class HandZoneView : MonoBehaviour
    {
        public const float ProximityDistance = 1.5f;
        private const float ColliderHeight = 0.06f;
        private const float OutlineThickness = 0.04f;
        private const float OutlineHeight = 0.004f;
        private const float OutlineLift = 0.003f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color NearColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color InsideColor = new Color(0.2f, 0.9f, 1f, 1f);
        private static readonly Color FullColor = new Color(0.95f, 0.3f, 0.3f, 1f);

        private readonly Renderer[] outline = new Renderer[4];
        private BoxCollider zoneCollider;
        private TabletopContainerDropTarget dropTarget;
        private MaterialPropertyBlock propertyBlock;
        private ZoneHighlight highlight;
        private float width = 1f;
        private float depth = 1f;

        private enum ZoneHighlight
        {
            None,
            Near,
            Inside,
            Full,
        }

        public float Width => width;

        public float Depth => depth;

        public static HandZoneView Create(string name, int dropTargetLayer, Material outlineMaterial)
        {
            GameObject root = new GameObject(name);
            root.layer = dropTargetLayer;
            HandZoneView view = root.AddComponent<HandZoneView>();
            view.zoneCollider = root.AddComponent<BoxCollider>();
            view.zoneCollider.isTrigger = true;
            view.zoneCollider.enabled = false;
            view.dropTarget = root.AddComponent<TabletopContainerDropTarget>();
            view.dropTarget.enabled = false;
            CreateOutline(root.transform, outlineMaterial, view.outline);
            view.propertyBlock = new MaterialPropertyBlock();
            view.highlight = ZoneHighlight.None;
            for (int i = 0; i < view.outline.Length; i++)
            {
                view.outline[i].enabled = false;
            }

            return view;
        }

        /// <summary>Outline-only ghost (no colliders) used as the Move Hand Zone placement preview.</summary>
        public static GameObject CreateOutlineGhost(string name, float zoneWidth, float zoneDepth, Material outlineMaterial)
        {
            GameObject root = new GameObject(name);
            Renderer[] bars = new Renderer[4];
            CreateOutline(root.transform, outlineMaterial, bars);
            LayoutOutline(bars, Mathf.Max(0.01f, zoneWidth), Mathf.Max(0.01f, zoneDepth));
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, InsideColor);
            block.SetColor(ColorId, InsideColor);
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i].SetPropertyBlock(block);
                bars[i].enabled = true;
            }

            return root;
        }

        public void ConfigureDropTarget(IContainerView handView)
        {
            dropTarget.Configure(handView, zoneCollider);
            SetDropTargetActive(false);
        }

        public void ClearDropTarget()
        {
            dropTarget.ClearConfiguration();
            dropTarget.enabled = false;
            zoneCollider.enabled = false;
            SetHighlight(ZoneHighlight.None);
        }

        /// <summary>The zone is hit-tested only while a card drag is in progress.</summary>
        public void SetDropTargetActive(bool active)
        {
            bool enable = active && dropTarget.IsConfigured && gameObject.activeInHierarchy;
            if (zoneCollider.enabled != enable)
            {
                zoneCollider.enabled = enable;
            }

            if (dropTarget.enabled != enable)
            {
                dropTarget.enabled = enable;
            }
        }

        public void ApplyPlacement(Vector3 worldPosition, Quaternion worldRotation, float zoneWidth, float zoneDepth)
        {
            width = Mathf.Max(0.01f, zoneWidth);
            depth = Mathf.Max(0.01f, zoneDepth);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            zoneCollider.center = new Vector3(0f, ColliderHeight * 0.5f, 0f);
            zoneCollider.size = new Vector3(width, ColliderHeight, depth);
            LayoutOutline(outline, width, depth);
        }

        /// <summary>
        /// Called each frame by the composition. The outline shows only while dragging with the pointer within
        /// ProximityDistance of the zone; inside the zone it is highlighted, or red when the hand is full.
        /// </summary>
        public void UpdateOutline(bool dragging, bool hasPointer, Vector3 pointerWorldPosition, bool handFull)
        {
            if (!dragging || !hasPointer)
            {
                SetHighlight(ZoneHighlight.None);
                return;
            }

            Vector3 local = transform.InverseTransformPoint(pointerWorldPosition);
            float outsideX = Mathf.Max(0f, Mathf.Abs(local.x) - (width * 0.5f));
            float outsideZ = Mathf.Max(0f, Mathf.Abs(local.z) - (depth * 0.5f));
            if (outsideX <= 0f && outsideZ <= 0f)
            {
                SetHighlight(handFull ? ZoneHighlight.Full : ZoneHighlight.Inside);
            }
            else if ((outsideX * outsideX) + (outsideZ * outsideZ) <= ProximityDistance * ProximityDistance)
            {
                SetHighlight(ZoneHighlight.Near);
            }
            else
            {
                SetHighlight(ZoneHighlight.None);
            }
        }

        private void SetHighlight(ZoneHighlight next)
        {
            if (next == highlight || propertyBlock == null)
            {
                return;
            }

            highlight = next;
            bool visible = next != ZoneHighlight.None;
            Color color = next == ZoneHighlight.Inside
                ? InsideColor
                : next == ZoneHighlight.Full ? FullColor : NearColor;
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            for (int i = 0; i < outline.Length; i++)
            {
                outline[i].SetPropertyBlock(propertyBlock);
                outline[i].enabled = visible;
            }
        }

        private static void CreateOutline(Transform parent, Material material, Renderer[] bars)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Zone Outline {i}";
                Collider barCollider = bar.GetComponent<Collider>();
                if (barCollider != null)
                {
                    DestroyImmediate(barCollider);
                }

                bar.transform.SetParent(parent, false);
                Renderer barRenderer = bar.GetComponent<Renderer>();
                barRenderer.sharedMaterial = material;
                barRenderer.shadowCastingMode = ShadowCastingMode.Off;
                barRenderer.receiveShadows = false;
                bars[i] = barRenderer;
            }
        }

        private static void LayoutOutline(Renderer[] bars, float zoneWidth, float zoneDepth)
        {
            float halfWidth = zoneWidth * 0.5f;
            float halfDepth = zoneDepth * 0.5f;
            SetBar(bars[0], new Vector3(0f, OutlineLift, halfDepth), new Vector3(zoneWidth + OutlineThickness, OutlineHeight, OutlineThickness));
            SetBar(bars[1], new Vector3(0f, OutlineLift, -halfDepth), new Vector3(zoneWidth + OutlineThickness, OutlineHeight, OutlineThickness));
            SetBar(bars[2], new Vector3(-halfWidth, OutlineLift, 0f), new Vector3(OutlineThickness, OutlineHeight, zoneDepth));
            SetBar(bars[3], new Vector3(halfWidth, OutlineLift, 0f), new Vector3(OutlineThickness, OutlineHeight, zoneDepth));
        }

        private static void SetBar(Renderer bar, Vector3 localPosition, Vector3 localScale)
        {
            Transform barTransform = bar.transform;
            barTransform.localPosition = localPosition;
            barTransform.localRotation = Quaternion.identity;
            barTransform.localScale = localScale;
        }
    }
}
