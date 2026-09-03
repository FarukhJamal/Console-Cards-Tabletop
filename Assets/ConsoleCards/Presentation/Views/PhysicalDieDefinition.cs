using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    [Serializable]
    public sealed class PhysicalDieFace
    {
        public int value;
        public Vector3 outwardNormal;
        public float planeDistance = 0.3f;
    }

    /// <summary>Immutable authored face/value and collision-shape definition. No triangle/name based result inference.</summary>
    [CreateAssetMenu(menuName = "Console Cards/Physical Die Definition")]
    public sealed class PhysicalDieDefinition : ScriptableObject
    {
        [SerializeField] private int sideCount;
        [Tooltip("d4 reads the upward apex opposite the supporting face; other variants read the upward face.")]
        [SerializeField] private bool readOppositeSupportingFace;
        [SerializeField] private PhysicalDieFace[] faces;
        [Header("Supplied Visual")]
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private float visualSize = 0.6f;
        public int SideCount => sideCount;

        public bool TryRead(Quaternion rotation, out int value)
        {
            float best = -2f, next = -2f;
            value = 0;
            foreach (PhysicalDieFace face in faces)
            {
                Vector3 axis = face.outwardNormal.normalized * (readOppositeSupportingFace ? -1f : 1f);
                float dot = Vector3.Dot(rotation * axis, Vector3.up);
                if (dot > best) { next = best; best = dot; value = face.value; }
                else next = Mathf.Max(next, dot);
            }
            return best >= 0.9f && best - next >= 0.08f;
        }

        public void Build(Transform root, MeshFilter prototypeBodyMesh)
        {
            if (faces == null || faces.Length != sideCount) throw new InvalidOperationException("Die face count mismatch.");
            HashSet<int> values = new HashSet<int>();
            foreach (PhysicalDieFace face in faces)
                if (face.value < 1 || face.value > sideCount || !values.Add(face.value)
                    || face.outwardNormal.sqrMagnitude < 0.99f || face.planeDistance <= 0f)
                    throw new InvalidOperationException("Invalid authored physical Die mapping.");

            if (root == null) throw new ArgumentNullException(nameof(root));
            if (prototypeBodyMesh == null) throw new ArgumentNullException(nameof(prototypeBodyMesh));
            if (visualPrefab == null) throw new InvalidOperationException($"Physical d{sideCount} requires its supplied visual prefab.");
            if (!IsFinitePositive(visualSize)) throw new InvalidOperationException($"Physical d{sideCount} visual size is invalid.");

            GameObject visual = Instantiate(visualPrefab, root, false);
            visual.name = $"PhysicalD{sideCount}Visual";
            SetLayerRecursively(visual.transform, root.gameObject.layer);

            MeshFilter visualMeshFilter = visual.GetComponent<MeshFilter>();
            MeshRenderer visualRenderer = visual.GetComponent<MeshRenderer>();
            MeshCollider visualCollider = visual.GetComponent<MeshCollider>();
            if (visualMeshFilter == null || visualMeshFilter.sharedMesh == null || visualRenderer == null)
            {
                throw new InvalidOperationException($"Physical d{sideCount} visual prefab requires a root MeshFilter and MeshRenderer.");
            }

            if (visualCollider == null || visualCollider.sharedMesh == null)
            {
                throw new InvalidOperationException($"Physical d{sideCount} visual prefab requires its authored MeshCollider.");
            }

            Bounds meshBounds = visualMeshFilter.sharedMesh.bounds;
            float largestDimension = Mathf.Max(meshBounds.size.x, meshBounds.size.y, meshBounds.size.z);
            if (!IsFinitePositive(largestDimension))
            {
                throw new InvalidOperationException($"Physical d{sideCount} visual mesh has invalid bounds.");
            }

            float uniformScale = visualSize / largestDimension;
            visual.transform.localPosition = -meshBounds.center * uniformScale;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * uniformScale;

            visualCollider.convex = true;
            visualCollider.isTrigger = false;
            visualCollider.enabled = true;

            foreach (Rigidbody duplicateBody in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                duplicateBody.isKinematic = true;
                duplicateBody.detectCollisions = false;
                Destroy(duplicateBody);
            }

            foreach (MonoBehaviour importedBehaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            {
                importedBehaviour.enabled = false;
                Destroy(importedBehaviour);
            }

            Collider[] importedColliders = visual.GetComponentsInChildren<Collider>(true);
            foreach (Collider importedCollider in importedColliders)
            {
                if (ReferenceEquals(importedCollider, visualCollider)) continue;
                importedCollider.enabled = false;
                Destroy(importedCollider);
            }

            prototypeBodyMesh.gameObject.SetActive(false);
            Collider oldCollider = root.GetComponent<Collider>();
            if (oldCollider != null)
            {
                oldCollider.enabled = false;
                Destroy(oldCollider);
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
