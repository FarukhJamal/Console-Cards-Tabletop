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

        public void Build(Transform root, MeshFilter bodyMesh, TextMesh labelTemplate)
        {
            if (faces == null || faces.Length != sideCount) throw new InvalidOperationException("Die face count mismatch.");
            HashSet<int> values = new HashSet<int>();
            foreach (PhysicalDieFace face in faces)
                if (face.value < 1 || face.value > sideCount || !values.Add(face.value)
                    || face.outwardNormal.sqrMagnitude < 0.99f || face.planeDistance <= 0f)
                    throw new InvalidOperationException("Invalid authored physical Die mapping.");
            List<Vector3> points = new List<Vector3>();
            for (int a = 0; a < faces.Length; a++)
            for (int b = a + 1; b < faces.Length; b++)
            for (int c = b + 1; c < faces.Length; c++)
            {
                Vector3 n1 = faces[a].outwardNormal.normalized, n2 = faces[b].outwardNormal.normalized,
                    n3 = faces[c].outwardNormal.normalized;
                float determinant = Vector3.Dot(n1, Vector3.Cross(n2, n3));
                if (Mathf.Abs(determinant) < 0.00001f) continue;
                Vector3 point = (Vector3.Cross(n2, n3) * faces[a].planeDistance
                    + Vector3.Cross(n3, n1) * faces[b].planeDistance
                    + Vector3.Cross(n1, n2) * faces[c].planeDistance) / determinant;
                bool inside = true;
                foreach (PhysicalDieFace face in faces)
                    if (Vector3.Dot(point, face.outwardNormal.normalized) > face.planeDistance + 0.0001f) inside = false;
                if (inside && !points.Exists(p => (p - point).sqrMagnitude < 0.000001f)) points.Add(point);
            }
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            foreach (PhysicalDieFace face in faces)
            {
                Vector3 normal = face.outwardNormal.normalized;
                List<Vector3> polygon = points.FindAll(p => Mathf.Abs(Vector3.Dot(p, normal) - face.planeDistance) < 0.001f);
                if (polygon.Count < 3) throw new InvalidOperationException("An authored Die face has no collision polygon.");
                Vector3 center = Vector3.zero;
                foreach (Vector3 p in polygon) center += p;
                center /= polygon.Count;
                Vector3 u = (polygon[0] - center).normalized;
                Vector3 v = Vector3.Cross(normal, u);
                polygon.Sort((left, right) => Mathf.Atan2(Vector3.Dot(left - center, v), Vector3.Dot(left - center, u))
                    .CompareTo(Mathf.Atan2(Vector3.Dot(right - center, v), Vector3.Dot(right - center, u))));
                int start = vertices.Count;
                vertices.AddRange(polygon);
                for (int i = 1; i < polygon.Count - 1; i++)
                { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
                TextMesh label = Instantiate(labelTemplate, root);
                label.gameObject.SetActive(true);
                label.text = face.value.ToString();
                label.transform.localPosition = readOppositeSupportingFace
                    ? -normal * (face.planeDistance * 3f + 0.004f) : center + normal * 0.004f;
                label.transform.localRotation = Quaternion.LookRotation(readOppositeSupportingFace ? normal : -normal, v);
                label.transform.localScale = Vector3.one * (sideCount >= 12 ? 0.09f : 0.14f);
            }
            Mesh mesh = new Mesh { name = "Authored physical die convex shape" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            bodyMesh.transform.localPosition = Vector3.zero;
            bodyMesh.transform.localRotation = Quaternion.identity;
            bodyMesh.transform.localScale = Vector3.one;
            bodyMesh.sharedMesh = mesh;
            Collider oldCollider = root.GetComponent<Collider>();
            oldCollider.enabled = false;
            Destroy(oldCollider);
            MeshCollider collider = root.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
        }
    }
}
