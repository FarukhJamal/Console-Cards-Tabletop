using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Intro.Editor
{
    /// <summary>
    /// The intro room's own shapes (hexagon table top, rim, rings, frustums, planes), made as mesh assets with UVs in
    /// metres, so a texture's tiling is set once on its material and every surface shows it at the same size.
    /// </summary>
    internal static class IntroMeshes
    {
        public const string Folder = "Assets/ConsoleCards/Content/Intro/Meshes/";

        private sealed class Build
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            // A flat triangle whose front faces 'outward' (Unity: clockwise seen from the front).
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, outward) < 0f)
                {
                    (b, c) = (c, b);
                    (ub, uc) = (uc, ub);
                }

                Vector3 nn = outward.normalized;
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(nn); N.Add(nn); N.Add(nn);
                U.Add(ua); U.Add(ub); U.Add(uc);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward)
            {
                Tri(a, b, c, ua, ub, uc, outward);
                Tri(a, c, d, ua, uc, ud, outward);
            }

            // A quad with its own normals per corner (round shapes).
            public void SmoothQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                Vector3 outward = (na + nb + nc + nd).normalized;
                Vector3 n = Vector3.Cross(b - a, c - a);
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(na.normalized); N.Add(nb.normalized); N.Add(nc.normalized); N.Add(nd.normalized);
                U.Add(ua); U.Add(ub); U.Add(uc); U.Add(ud);
                if (Vector3.Dot(n, outward) >= 0f)
                {
                    T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
                }
                else
                {
                    T.Add(i); T.Add(i + 2); T.Add(i + 1); T.Add(i); T.Add(i + 3); T.Add(i + 2);
                }
            }

            public Mesh Save(string name)
            {
                Mesh mesh = new Mesh { name = name };
                if (V.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(V);
                mesh.SetNormals(N);
                mesh.SetUVs(0, U);
                mesh.SetTriangles(T, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                string path = Folder + name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
        }

        private static Vector3 Corner(float r, float a, float y) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);

        /// <summary>A flat rectangle from a corner along two edges, UVs in metres.</summary>
        public static Mesh Plane(string name, Vector3 corner, Vector3 edgeU, Vector3 edgeV, Vector3 outward)
        {
            Build b = new Build();
            float lu = edgeU.magnitude, lv = edgeV.magnitude;
            b.Quad(corner, corner + edgeU, corner + edgeU + edgeV, corner + edgeV,
                Vector2.zero, new Vector2(lu, 0f), new Vector2(lu, lv), new Vector2(0f, lv), outward);
            return b.Save(name);
        }

        /// <summary>An n-sided prism (hexagon with n = 6) from y0 to y1; top cap, optional bottom cap.</summary>
        public static Mesh Prism(string name, int sides, float radius, float y0, float y1, float a0, bool bottom = false,
            float uvScale = 1f, bool uvNormalised = false)
        {
            Build b = new Build();
            Vector2 Uv(Vector3 p) => uvNormalised
                ? new Vector2(p.x / (radius * 2f) + 0.5f, p.z / (radius * 2f) + 0.5f)
                : new Vector2(p.x, p.z) * uvScale;
            float s = 0f;
            for (int i = 0; i < sides; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / sides, a2 = a0 + (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = Corner(radius, a, y0), p1 = Corner(radius, a2, y0), p2 = Corner(radius, a2, y1), p3 = Corner(radius, a, y1);
                Vector3 mid = Corner(1f, (a + a2) * 0.5f, 0f);
                float len = Vector3.Distance(p0, p1);
                b.Quad(p0, p1, p2, p3, new Vector2(s, y0) * uvScale, new Vector2(s + len, y0) * uvScale,
                    new Vector2(s + len, y1) * uvScale, new Vector2(s, y1) * uvScale, mid);
                s += len;
                Vector3 c1 = new Vector3(0f, y1, 0f);
                b.Tri(c1, p3, p2, Uv(c1), Uv(p3), Uv(p2), Vector3.up);
                if (bottom)
                {
                    Vector3 c0 = new Vector3(0f, y0, 0f);
                    b.Tri(c0, p0, p1, Uv(c0), Uv(p0), Uv(p1), Vector3.down);
                }
            }

            return b.Save(name);
        }

        /// <summary>The table rim: a hexagon ring between two radii, closed on all four sides.</summary>
        public static Mesh Ring(string name, int sides, float rIn, float rOut, float y0, float y1, float a0)
        {
            Build b = new Build();
            float sIn = 0f, sOut = 0f;
            for (int i = 0; i < sides; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / sides, a2 = a0 + (i + 1) * Mathf.PI * 2f / sides;
                Vector3 mid = Corner(1f, (a + a2) * 0.5f, 0f);
                Vector3 i0 = Corner(rIn, a, y1), i1 = Corner(rIn, a2, y1), o0 = Corner(rOut, a, y1), o1 = Corner(rOut, a2, y1);
                b.Quad(i0, i1, o1, o0, Xz(i0), Xz(i1), Xz(o1), Xz(o0), Vector3.up);
                Vector3 bi0 = Corner(rIn, a, y0), bi1 = Corner(rIn, a2, y0), bo0 = Corner(rOut, a, y0), bo1 = Corner(rOut, a2, y0);
                b.Quad(bi0, bo0, bo1, bi1, Xz(bi0), Xz(bo0), Xz(bo1), Xz(bi1), Vector3.down);
                float lo = Vector3.Distance(bo0, bo1), li = Vector3.Distance(bi0, bi1);
                b.Quad(bo0, bo1, o1, o0, new Vector2(sOut, y0), new Vector2(sOut + lo, y0), new Vector2(sOut + lo, y1), new Vector2(sOut, y1), mid);
                b.Quad(bi0, bi1, i1, i0, new Vector2(sIn, y0), new Vector2(sIn + li, y0), new Vector2(sIn + li, y1), new Vector2(sIn, y1), -mid);
                sOut += lo;
                sIn += li;
            }

            return b.Save(name);
        }

        /// <summary>A flat ring (LED strips, inlays); faces up, or down with <paramref name="up"/> false.</summary>
        public static Mesh FlatRing(string name, int sides, float rIn, float rOut, float y, float a0, bool up = true)
        {
            Build b = new Build();
            for (int i = 0; i < sides; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / sides, a2 = a0 + (i + 1) * Mathf.PI * 2f / sides;
                Vector3 i0 = Corner(rIn, a, y), i1 = Corner(rIn, a2, y), o0 = Corner(rOut, a, y), o1 = Corner(rOut, a2, y);
                if (rIn <= 0.0001f) b.Tri(i0, o0, o1, Xz(i0), Xz(o0), Xz(o1), up ? Vector3.up : Vector3.down);
                else b.Quad(i0, i1, o1, o0, Xz(i0), Xz(i1), Xz(o1), Xz(o0), up ? Vector3.up : Vector3.down);
            }

            return b.Save(name);
        }

        /// <summary>A frustum (lamp shades, pots, pedestals); smooth sides for round ones.</summary>
        public static Mesh Frustum(string name, int sides, float r0, float r1, float y0, float y1, float a0, bool smooth,
            bool top, bool bottom)
        {
            Build b = new Build();
            float slope = (r0 - r1) / Mathf.Max(0.0001f, y1 - y0);
            for (int i = 0; i < sides; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / sides, a2 = a0 + (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = Corner(r0, a, y0), p1 = Corner(r0, a2, y0), p2 = Corner(r1, a2, y1), p3 = Corner(r1, a, y1);
                float u0 = (float)i / sides, u1 = (float)(i + 1) / sides;
                if (smooth)
                {
                    Vector3 n0 = new Vector3(Mathf.Cos(a), slope, Mathf.Sin(a)), n1 = new Vector3(Mathf.Cos(a2), slope, Mathf.Sin(a2));
                    b.SmoothQuad(p0, p1, p2, p3, n0, n1, n1, n0, new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f));
                }
                else
                {
                    b.Quad(p0, p1, p2, p3, new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f),
                        Corner(1f, (a + a2) * 0.5f, slope));
                }

                if (top && r1 > 0f)
                {
                    Vector3 c = new Vector3(0f, y1, 0f);
                    b.Tri(c, p3, p2, Xz(c), Xz(p3), Xz(p2), Vector3.up);
                }

                if (bottom && r0 > 0f)
                {
                    Vector3 c = new Vector3(0f, y0, 0f);
                    b.Tri(c, p0, p1, Xz(c), Xz(p0), Xz(p1), Vector3.down);
                }
            }

            return b.Save(name);
        }

        private static Vector2 Xz(Vector3 p) => new Vector2(p.x, p.z);
    }
}
