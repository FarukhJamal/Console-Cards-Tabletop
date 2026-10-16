using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.TableSurface
{
    /// <summary>
    /// Builds a table's meshes from its outline (doc 30, B2; T1), in the table root's own space (y up, top at 0):
    /// the top as a slab (playing area on top, sides and underside below) and the edge as a small cross-section
    /// swept around the outline. Playing-area texture coordinates come from table units, so a felt repeats instead of
    /// stretching when the table grows (B5). All lists are made once and reused: a rebuild makes no garbage.
    /// Convex shapes only (T1); the star's concave filling comes in T2.
    /// </summary>
    public sealed class TableMeshBuilder
    {
        public const int MaximumProfilePoints = 12;

        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Vector3> normals = new List<Vector3>(4096);
        private readonly List<Vector2> uvs = new List<Vector2>(4096);
        private readonly List<int> playingAreaTriangles = new List<int>(2048);
        private readonly List<int> bodyTriangles = new List<int>(8192);
        private readonly Vector2[] profile = new Vector2[MaximumProfilePoints];

        /// <summary>
        /// The top slab. With <paramref name="twoMaterials"/> the playing area is sub-mesh 0 and the sides and
        /// underside sub-mesh 1; otherwise everything is one sub-mesh (for the collider).
        /// <paramref name="textureSize"/> is the world size one texture repeat covers.
        /// </summary>
        public void BuildTop(TableOutline outline, float thickness, Vector2 textureSize, Mesh mesh, bool twoMaterials)
        {
            Begin();
            int count = outline.Count;
            Vector2 size = new Vector2(Mathf.Max(0.01f, textureSize.x), Mathf.Max(0.01f, textureSize.y));

            // Playing area: a fan from the centre, facing up.
            int centre = vertices.Count;
            AddVertex(Vector3.zero, Vector3.up, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < count; i++)
            {
                Vector2 p = outline.Points[i];
                AddVertex(new Vector3(p.x, 0f, p.y), Vector3.up, new Vector2((p.x / size.x) + 0.5f, (p.y / size.y) + 0.5f));
            }

            List<int> top = playingAreaTriangles;
            for (int i = 0; i < count; i++)
            {
                AddTriangle(top, centre, centre + 1 + ((i + 1) % count), centre + 1 + i);
            }

            // Underside: the same fan, facing down.
            List<int> body = twoMaterials ? bodyTriangles : playingAreaTriangles;
            int under = vertices.Count;
            AddVertex(new Vector3(0f, -thickness, 0f), Vector3.down, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < count; i++)
            {
                Vector2 p = outline.Points[i];
                AddVertex(new Vector3(p.x, -thickness, p.y), Vector3.down, new Vector2((p.x / size.x) + 0.5f, (p.y / size.y) + 0.5f));
            }

            for (int i = 0; i < count; i++)
            {
                AddTriangle(body, under, under + 1 + i, under + 1 + ((i + 1) % count));
            }

            // Sides: straight down from the edge of the playing area.
            profile[0] = new Vector2(0f, 0f);
            profile[1] = new Vector2(0f, -thickness);
            Sweep(outline, 2, size.x, body);
            Finish(mesh, twoMaterials);
        }

        /// <summary>
        /// The edge: a lip <paramref name="height"/> above the playing area and <paramref name="width"/> wide, just
        /// outside the outline, wrapping down over the slab's side to its underside.
        /// </summary>
        public void BuildLip(TableOutline outline, float height, float width, float thickness, float textureLength, Mesh mesh)
        {
            Begin();
            profile[0] = new Vector2(0f, 0f);
            profile[1] = new Vector2(0f, height);
            profile[2] = new Vector2(width, height);
            profile[3] = new Vector2(width, -thickness);
            profile[4] = new Vector2(0f, -thickness);
            Sweep(outline, 5, Mathf.Max(0.01f, textureLength), playingAreaTriangles);
            Finish(mesh, false);
        }

        // Sweeps the profile (x: out from the edge, y: up) around the outline. Each profile segment is one strip,
        // flat across and smooth along; the last column repeats the first so the texture wraps without a seam jump.
        private void Sweep(TableOutline outline, int profileCount, float textureLength, List<int> triangles)
        {
            int count = outline.Count;
            for (int s = 0; s + 1 < profileCount; s++)
            {
                Vector2 a = profile[s];
                Vector2 b = profile[s + 1];
                Vector2 across = new Vector2(-(b.y - a.y), b.x - a.x).normalized;
                float vA = 0f;
                float vB = (b - a).magnitude / textureLength;
                int start = vertices.Count;
                float along = 0f;
                for (int i = 0; i <= count; i++)
                {
                    int k = i % count;
                    if (i > 0) along += (outline.Points[k] - outline.Points[i - 1]).magnitude;
                    Vector2 point = outline.Points[k];
                    Vector2 outward = outline.Normal(k);
                    float mitre = outline.MitreScale(k);
                    Vector3 normal = new Vector3(outward.x * across.x, across.y, outward.y * across.x);
                    Vector2 pa = point + (outward * (a.x * mitre));
                    Vector2 pb = point + (outward * (b.x * mitre));
                    float u = along / textureLength;
                    AddVertex(new Vector3(pa.x, a.y, pa.y), normal, new Vector2(u, vA));
                    AddVertex(new Vector3(pb.x, b.y, pb.y), normal, new Vector2(u, vB));
                }

                for (int i = 0; i < count; i++)
                {
                    int ai = start + (2 * i);
                    int bi = ai + 1;
                    int an = ai + 2;
                    int bn = ai + 3;
                    AddTriangle(triangles, ai, an, bi);
                    AddTriangle(triangles, an, bn, bi);
                }
            }
        }

        private void Begin()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            playingAreaTriangles.Clear();
            bodyTriangles.Clear();
        }

        private void Finish(Mesh mesh, bool twoMaterials)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = twoMaterials ? 2 : 1;
            mesh.SetTriangles(playingAreaTriangles, 0, false);
            if (twoMaterials) mesh.SetTriangles(bodyTriangles, 1, false);
            mesh.RecalculateBounds();
        }

        private void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
