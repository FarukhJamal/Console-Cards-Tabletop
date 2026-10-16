using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.TableSurface
{
    /// <summary>
    /// Builds a table's meshes from its outline (doc 30, B2; T1), in the table root's own space (y up, top at 0):
    /// the top as a slab (playing area on top, sides and underside below) and the edge as a small cross-section
    /// swept around the outline. Playing-area texture coordinates come from table units, so a felt repeats instead of
    /// stretching when the table grows (B5). All lists are made once and reused: a rebuild makes no garbage.
    /// Convex outlines are filled with a fan; outlines with inward corners (the star, T2) by ear clipping.
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
        private readonly List<int> fill = new List<int>(1536);
        private readonly List<int> remaining = new List<int>(512);
        public const int MaximumOrnaments = 256;

        /// <summary>
        /// The top slab. With <paramref name="twoMaterials"/> the playing area is sub-mesh 0 and the sides and
        /// underside sub-mesh 1; otherwise everything is one sub-mesh (for the collider).
        /// <paramref name="textureSize"/> is the world size one texture repeat covers.
        /// </summary>
        public void BuildTop(TableOutline outline, float thickness, Vector2 textureSize, Mesh mesh, bool twoMaterials,
            bool centreFan = false)
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
            Fill(outline, centreFan);
            for (int i = 0; i + 2 < fill.Count; i += 3)
            {
                AddTriangle(top, centre + 1 + fill[i], centre + 1 + fill[i + 2], centre + 1 + fill[i + 1]);
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

            for (int i = 0; i + 2 < fill.Count; i += 3)
            {
                AddTriangle(body, under + 1 + fill[i], under + 1 + fill[i + 1], under + 1 + fill[i + 2]);
            }

            // Sides: straight down from the edge of the playing area.
            profile[0] = new Vector2(0f, 0f);
            profile[1] = new Vector2(0f, -thickness);
            Sweep(outline, 2, size.x, body);
            Finish(mesh, twoMaterials);
        }

        /// <summary>
        /// The edge (T2): a cross-section <paramref name="height"/> above the playing area and
        /// <paramref name="width"/> wide, just outside the outline, wrapping down over the slab's side to its
        /// underside. Lip: square; rounded rail: a rounded top; wooden rail: a bevelled top. Spikes and Neon stand on
        /// a lip. False (nothing built) for None.
        /// </summary>
        public bool BuildEdge(TableOutline outline, TableEdge kind, float height, float width, float thickness, float textureLength, Mesh mesh)
        {
            int count = 0;
            profile[count++] = new Vector2(0f, 0f);
            switch (kind)
            {
                case TableEdge.None:
                    return false;
                case TableEdge.RoundedRail:
                    profile[count++] = new Vector2(0f, 0.55f * height);
                    profile[count++] = new Vector2(0.07f * width, 0.8f * height);
                    profile[count++] = new Vector2(0.25f * width, 0.95f * height);
                    profile[count++] = new Vector2(0.5f * width, height);
                    profile[count++] = new Vector2(0.75f * width, 0.95f * height);
                    profile[count++] = new Vector2(0.93f * width, 0.8f * height);
                    profile[count++] = new Vector2(width, 0.55f * height);
                    break;
                case TableEdge.WoodenRail:
                    profile[count++] = new Vector2(0f, 0.65f * height);
                    profile[count++] = new Vector2(0.3f * width, height);
                    profile[count++] = new Vector2(0.85f * width, height);
                    profile[count++] = new Vector2(width, 0.85f * height);
                    break;
                default:
                    profile[count++] = new Vector2(0f, height);
                    profile[count++] = new Vector2(width, height);
                    break;
            }

            profile[count++] = new Vector2(width, -thickness);
            profile[count++] = new Vector2(0f, -thickness);
            Begin();
            Sweep(outline, count, Mathf.Max(0.01f, textureLength), playingAreaTriangles);
            Finish(mesh, false);
            return true;
        }

        /// <summary>The Neon edge's glowing strip (T2): a thin band along the middle of the lip's top.</summary>
        public void BuildNeonStrip(TableOutline outline, float height, float width, float textureLength, Mesh mesh)
        {
            float raise = Mathf.Max(0.01f, 0.25f * height);
            profile[0] = new Vector2(0.3f * width, height);
            profile[1] = new Vector2(0.3f * width, height + raise);
            profile[2] = new Vector2(0.7f * width, height + raise);
            profile[3] = new Vector2(0.7f * width, height);
            Begin();
            Sweep(outline, 4, Mathf.Max(0.01f, textureLength), playingAreaTriangles);
            Finish(mesh, false);
        }

        /// <summary>
        /// The Spikes edge's spikes (T2): small eight-sided cones standing on the middle of the lip, evenly spaced
        /// along the outline (at most <see cref="MaximumOrnaments"/>), merged into one mesh: one draw call and no
        /// work per frame.
        /// </summary>
        public void BuildSpikes(TableOutline outline, float height, float width, Mesh mesh)
        {
            Begin();
            float radius = 0.32f * width;
            float tall = 1.6f * width;
            float spacing = Mathf.Max(4f * width, outline.Perimeter / MaximumOrnaments);
            int spikes = Mathf.Clamp(Mathf.FloorToInt(outline.Perimeter / spacing), 3, MaximumOrnaments);
            spacing = outline.Perimeter / spikes;
            int segment = 0;
            float walked = 0f;
            int count = outline.Count;
            for (int s = 0; s < spikes; s++)
            {
                float target = (s + 0.5f) * spacing;
                Vector2 a = outline.Points[segment];
                Vector2 b = outline.Points[(segment + 1) % count];
                float length = (b - a).magnitude;
                while (walked + length < target && segment < count - 1)
                {
                    walked += length;
                    segment++;
                    a = outline.Points[segment];
                    b = outline.Points[(segment + 1) % count];
                    length = (b - a).magnitude;
                }

                Vector2 along = length > 1e-6f ? (b - a) / length : Vector2.right;
                Vector2 point = a + (along * Mathf.Clamp(target - walked, 0f, length));
                Vector2 outward = new Vector2(along.y, -along.x);
                Vector2 centre = point + (outward * (0.5f * width));
                AddCone(new Vector3(centre.x, height, centre.y), radius, tall, playingAreaTriangles);
            }

            Finish(mesh, false);
        }

        // An eight-sided cone with flat faces, its base centre at the given point.
        private void AddCone(Vector3 baseCentre, float radius, float tall, List<int> triangles)
        {
            const int sides = 8;
            Vector3 apex = baseCentre + (Vector3.up * tall);
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides;
                float a1 = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 p0 = baseCentre + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = baseCentre + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Vector3 normal = Vector3.Cross(apex - p0, p1 - p0).normalized;
                if (Vector3.Dot(normal, (p0 + p1) * 0.5f - baseCentre) < 0f) normal = -normal;
                int start = vertices.Count;
                AddVertex(p0, normal, new Vector2(0f, 0f));
                AddVertex(p1, normal, new Vector2(1f, 0f));
                AddVertex(apex, normal, new Vector2(0.5f, 1f));
                // Front side outward: Vector3.Cross(b - a, c - a) must point along the normal.
                if (Vector3.Dot(Vector3.Cross(p1 - p0, apex - p0), normal) > 0f) AddTriangle(triangles, start, start + 1, start + 2);
                else AddTriangle(triangles, start, start + 2, start + 1);
            }
        }

        // Triangles filling the outline, as outline indices, counter-clockwise seen from above (math orientation).
        // Index −1 is the centre. With centreFan: a fan from the centre, cheap enough for every frame of the shape
        // change (T3; every table shape can be seen whole from its centre). Convex: a fan from point 0. Otherwise
        // ear clipping, fine for the few corners a star has.
        private void Fill(TableOutline outline, bool centreFan)
        {
            fill.Clear();
            int count = outline.Count;
            if (centreFan)
            {
                for (int i = 0; i < count; i++)
                {
                    fill.Add(-1);
                    fill.Add(i);
                    fill.Add((i + 1) % count);
                }

                return;
            }

            if (outline.IsConvex)
            {
                for (int i = 1; i + 1 < count; i++)
                {
                    fill.Add(0);
                    fill.Add(i);
                    fill.Add(i + 1);
                }

                return;
            }

            remaining.Clear();
            for (int i = 0; i < count; i++) remaining.Add(i);
            int guard = count * count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int r = 0; r < remaining.Count; r++)
                {
                    int ia = remaining[(r + remaining.Count - 1) % remaining.Count];
                    int ib = remaining[r];
                    int ic = remaining[(r + 1) % remaining.Count];
                    Vector2 a = outline.Points[ia];
                    Vector2 b = outline.Points[ib];
                    Vector2 c = outline.Points[ic];
                    if (Cross(a, b, c) <= 1e-9f) continue; // an inward corner is not an ear
                    bool empty = true;
                    for (int k = 0; k < remaining.Count && empty; k++)
                    {
                        int ik = remaining[k];
                        if (ik == ia || ik == ib || ik == ic) continue;
                        if (Inside(outline.Points[ik], a, b, c)) empty = false;
                    }

                    if (!empty) continue;
                    fill.Add(ia);
                    fill.Add(ib);
                    fill.Add(ic);
                    remaining.RemoveAt(r);
                    clipped = true;
                    break;
                }

                if (!clipped) break;
            }

            if (remaining.Count == 3)
            {
                fill.Add(remaining[0]);
                fill.Add(remaining[1]);
                fill.Add(remaining[2]);
            }
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) => ((b.x - a.x) * (c.y - a.y)) - ((b.y - a.y) * (c.x - a.x));

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(a, b, p) >= 0f && Cross(b, c, p) >= 0f && Cross(c, a, p) >= 0f;

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
