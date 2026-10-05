using System.Collections.Generic;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// Shared rest-height rules: a component rests with the bottom of its own collider on a surface
    /// top. Stage H1 uses this for hand cards; P1a generalises it to other placed components.
    /// </summary>
    public static class ComponentRestHeight
    {
        /// <summary>Small gap that keeps a resting object from z-fighting with the surface below.</summary>
        public const float RestClearance = 0.002f;

        private static readonly List<Collider> ColliderBuffer = new List<Collider>();
        private static readonly List<Renderer> RendererBuffer = new List<Renderer>();

        /// <summary>
        /// Height to add to a surface top so the root rests on it: the distance from the root pivot down to
        /// its lowest body point, plus the root's authored <see cref="ComponentRestProfile"/> offset, plus
        /// <see cref="RestClearance"/>. Body points come from solid body colliders; when there are none, from
        /// mesh bounds. Triggers, objects inactive below the root, drop-target colliders, anything under a
        /// Console Slot, Drop Target or Card, and TextMesh labels never count. Collider and renderer enabled
        /// flags are ignored so a placement ghost measures the same as the placed piece. Yaw does not change
        /// the result. Call when a pose is applied or a ghost starts, not per frame.
        /// </summary>
        public static float RestLift(Transform root)
        {
            if (root == null)
            {
                return 0f;
            }

            float lowest = LowestBodyColliderPoint(root);
            if (float.IsPositiveInfinity(lowest))
            {
                lowest = LowestMeshPoint(root);
            }

            float lift = float.IsPositiveInfinity(lowest) ? 0f : root.position.y - lowest;
            if (root.TryGetComponent(out ComponentRestProfile profile))
            {
                lift += profile.RestOffset;
            }

            return lift + RestClearance;
        }

        /// <summary>
        /// Distance from the root pivot down to the bottom of the root's own solid collider, for an
        /// upright root. Returns 0 when the root has no solid collider.
        /// </summary>
        public static float PivotToBottom(Transform root)
        {
            if (root == null)
            {
                return 0f;
            }

            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box != null && !box.isTrigger)
            {
                float localBottom = box.center.y - (box.size.y * 0.5f);
                return -localBottom * root.lossyScale.y;
            }

            Collider collider = root.GetComponent<Collider>();
            if (collider != null && !collider.isTrigger && collider.enabled)
            {
                return root.position.y - collider.bounds.min.y;
            }

            return 0f;
        }

        /// <summary>
        /// Half the root's depth along its own forward axis, from its BoxCollider; the fallback is
        /// used when the root has none.
        /// </summary>
        public static float HalfDepth(Transform root, float fallback)
        {
            if (root == null)
            {
                return fallback;
            }

            BoxCollider box = root.GetComponent<BoxCollider>();
            return box != null
                ? box.size.z * 0.5f * root.lossyScale.z
                : fallback;
        }

        /// <summary>
        /// World height of the top of a renderer's mesh, from the mesh bounds so it is also valid
        /// while the renderer is hidden.
        /// </summary>
        public static float TopOf(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return renderer.bounds.max.y;
            }

            Bounds bounds = filter.sharedMesh.bounds;
            Matrix4x4 localToWorld = renderer.transform.localToWorldMatrix;
            float top = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                top = Mathf.Max(top, localToWorld.MultiplyPoint3x4(corner).y);
            }

            return top;
        }

        /// <summary>World height of the bottom of a renderer's mesh, from the mesh bounds (valid while hidden).</summary>
        public static float BottomOf(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return renderer.bounds.min.y;
            }

            Bounds bounds = filter.sharedMesh.bounds;
            return LowestCorner(renderer.transform.localToWorldMatrix, bounds.min, bounds.max);
        }

        private static float LowestBodyColliderPoint(Transform root)
        {
            float lowest = float.PositiveInfinity;
            root.GetComponentsInChildren(true, ColliderBuffer);
            for (int i = 0; i < ColliderBuffer.Count; i++)
            {
                Collider collider = ColliderBuffer[i];
                if (collider.isTrigger
                    || collider.TryGetComponent(out TabletopContainerDropTarget _)
                    || IsExcluded(root, collider.transform))
                {
                    continue;
                }

                lowest = Mathf.Min(lowest, ColliderBottom(collider));
            }

            ColliderBuffer.Clear();
            return lowest;
        }

        private static float LowestMeshPoint(Transform root)
        {
            float lowest = float.PositiveInfinity;
            root.GetComponentsInChildren(true, RendererBuffer);
            for (int i = 0; i < RendererBuffer.Count; i++)
            {
                Renderer renderer = RendererBuffer[i];
                if (renderer.TryGetComponent(out TextMesh _) || IsExcluded(root, renderer.transform))
                {
                    continue;
                }

                lowest = Mathf.Min(lowest, BottomOf(renderer));
            }

            RendererBuffer.Clear();
            return lowest;
        }

        // True when the object, or any parent below the root, is inactive or belongs to a Console Slot,
        // a Drop Target or a Card. The root itself is not checked, so a Card root measures its own body.
        private static bool IsExcluded(Transform root, Transform current)
        {
            while (current != null && current != root)
            {
                if (!current.gameObject.activeSelf
                    || current.TryGetComponent(out ConsoleSlotView _)
                    || current.TryGetComponent(out TabletopContainerDropTarget _)
                    || current.TryGetComponent(out CardView _))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static float ColliderBottom(Collider collider)
        {
            Transform owner = collider.transform;
            switch (collider)
            {
                case BoxCollider box:
                {
                    Vector3 half = box.size * 0.5f;
                    return LowestCorner(owner.localToWorldMatrix, box.center - half, box.center + half);
                }
                case SphereCollider sphere:
                {
                    Vector3 scale = Abs(owner.lossyScale);
                    float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    return owner.TransformPoint(sphere.center).y - radius;
                }
                case CapsuleCollider capsule:
                {
                    Vector3 scale = Abs(owner.lossyScale);
                    Vector3 axis;
                    float radiusScale;
                    if (capsule.direction == 0)
                    {
                        axis = Vector3.right;
                        radiusScale = Mathf.Max(scale.y, scale.z);
                    }
                    else if (capsule.direction == 1)
                    {
                        axis = Vector3.up;
                        radiusScale = Mathf.Max(scale.x, scale.z);
                    }
                    else
                    {
                        axis = Vector3.forward;
                        radiusScale = Mathf.Max(scale.x, scale.y);
                    }

                    float halfSpine = Mathf.Max(0f, (capsule.height * 0.5f) - capsule.radius);
                    float endA = owner.TransformPoint(capsule.center + (axis * halfSpine)).y;
                    float endB = owner.TransformPoint(capsule.center - (axis * halfSpine)).y;
                    return Mathf.Min(endA, endB) - (capsule.radius * radiusScale);
                }
                case MeshCollider meshCollider when meshCollider.sharedMesh != null:
                {
                    Bounds bounds = meshCollider.sharedMesh.bounds;
                    return LowestCorner(owner.localToWorldMatrix, bounds.min, bounds.max);
                }
                default:
                    return collider.enabled && collider.gameObject.activeInHierarchy
                        ? collider.bounds.min.y
                        : float.PositiveInfinity;
            }
        }

        private static float LowestCorner(Matrix4x4 localToWorld, Vector3 min, Vector3 max)
        {
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                lowest = Mathf.Min(lowest, localToWorld.MultiplyPoint3x4(corner).y);
            }

            return lowest;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
