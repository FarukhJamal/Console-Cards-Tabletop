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
    }
}
