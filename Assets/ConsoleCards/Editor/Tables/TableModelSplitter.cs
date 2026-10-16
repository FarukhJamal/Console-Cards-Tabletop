using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.TableSurface;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConsoleCards.Editor.Tables
{
    /// <summary>
    /// Splits a table model into the three parts of a <see cref="TableModel"/> (doc 30, B8; T1b): playing area,
    /// sides and lower frame. The model itself is never changed: new mesh files and a prefab are written to
    /// Content/Tables/&lt;name&gt;/ and the prefab is added to Resources/TableModels.
    /// Pieces are sorted, not cut: each triangle goes to the part its centre falls in, so a model whose top, rim and
    /// legs are fused into one surface needs Blender. Texture coordinates and materials are kept as they are.
    /// </summary>
    public static class TableModelSplitter
    {
        public const string TablesFolder = "Assets/ConsoleCards/Content/Tables";
        public const string LibraryPath = "Assets/ConsoleCards/Content/Resources/TableModels.asset";

        private const float FlatNormal = 0.95f;

        /// <summary>The settings for one split.</summary>
        public sealed class Settings
        {
            public GameObject Source;
            public string DisplayName = "Classic";

            /// <summary>How far below the playing area the sides reach (world units); 0 picks 12 % of the height.</summary>
            public float SidesDepth;
        }

        private sealed class Part
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector4> Tangents = new List<Vector4>();
            public readonly List<Vector2> Uv0 = new List<Vector2>();
            public readonly List<Vector2> Uv1 = new List<Vector2>();
            public readonly List<Material> Materials = new List<Material>();
            public readonly List<List<int>> Triangles = new List<List<int>>();
            public readonly Dictionary<long, int> Remap = new Dictionary<long, int>();
            public int TriangleCount;

            public List<int> TrianglesFor(Material material)
            {
                int index = Materials.IndexOf(material);
                if (index < 0)
                {
                    Materials.Add(material);
                    Triangles.Add(new List<int>());
                    index = Materials.Count - 1;
                }

                return Triangles[index];
            }
        }

        // One source mesh with its data read once.
        private sealed class Source
        {
            public MeshFilter Filter;
            public Material[] Materials;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public List<Vector2> Uv0 = new List<Vector2>();
            public List<Vector2> Uv1 = new List<Vector2>();
            public Matrix4x4 ToWorld;
            public Matrix4x4 NormalToWorld;
            public bool IsPlayingArea;
        }

        /// <summary>Splits and saves. Returns the new model prefab, or null with <paramref name="report"/> saying why.</summary>
        public static TableModel Split(Settings settings, out string report)
        {
            StringBuilder log = new StringBuilder();
            if (settings?.Source == null)
            {
                report = "Pick the table model to split (a scene object or a prefab).";
                return null;
            }

            GameObject root = settings.Source;
            if (!root.scene.IsValid())
            {
                report = "Drag the table from the Hierarchy (a table in the open scene), not from the Project window. "
                    + "To split a prefab, drop it into the scene first.";
                return null;
            }

            Physics.SyncTransforms();
            List<Source> sources = ReadSources(root, log, out PhysicalTabletopSurface surface);
            if (sources == null)
            {
                report = log.ToString();
                return null;
            }

            // The playing area: the surface child (as on the classic table), or the largest flat, upward area.
            Vector3 up = Vector3.up;
            float topY;
            Vector3 centre;
            if (surface != null && TryTop(surface, out topY, out centre))
            {
                log.AppendLine($"Playing area: the surface object '{surface.name}'.");
            }
            else if (TryFindFlatTop(sources, out topY, out centre))
            {
                log.AppendLine("Playing area: the largest flat area facing up.");
            }
            else
            {
                report = log.AppendLine("No flat area facing up was found, so there is no playing area.").ToString();
                return null;
            }

            float bottomY = float.PositiveInfinity;
            Vector2 spanMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 spanMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Source source in sources)
            {
                foreach (Vector3 v in source.Vertices)
                {
                    Vector3 w = source.ToWorld.MultiplyPoint3x4(v);
                    bottomY = Mathf.Min(bottomY, w.y);
                    spanMin = Vector2.Min(spanMin, new Vector2(w.x, w.z));
                    spanMax = Vector2.Max(spanMax, new Vector2(w.x, w.z));
                }
            }

            float tableSpan = Mathf.Max(spanMax.x - spanMin.x, spanMax.y - spanMin.y);

            float depth = settings.SidesDepth > 0f ? settings.SidesDepth : Mathf.Max(0.05f, 0.12f * (topY - bottomY));
            log.AppendLine($"Top at height {topY:0.###}; sides reach {depth:0.###} below it; the model's foot is at {bottomY:0.###}.");

            Vector3 forward = Vector3.ProjectOnPlane(root.transform.forward, up);
            Quaternion facing = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward.normalized, up) : Quaternion.identity;
            Quaternion inverse = Quaternion.Inverse(facing);
            Vector3 pivot = new Vector3(centre.x, topY, centre.z);

            Part playingArea = new Part();
            Part sides = new Part();
            Part lowerFrame = new Part();

            // Legs: below the sides, the model falls apart into pieces (the cut separates legs from the rim or
            // apron they hang from). Each narrow piece is one leg, kept whole, so built shapes can move legs in.
            List<Part> legs = new List<Part>();
            Dictionary<long, Part> legByIsland = new Dictionary<long, Part>();
            for (int s = 0; s < sources.Count; s++)
            {
                Source source = sources[s];
                Mesh mesh = source.Filter.sharedMesh;
                int[] island = Islands(source, topY - depth, out Dictionary<int, Bounds> islandBounds);
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material material = source.Materials.Length == 0 ? null : source.Materials[Mathf.Min(sub, source.Materials.Length - 1)];
                    int[] triangles = mesh.GetTriangles(sub);
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        Vector3 a = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t]]);
                        Vector3 b = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t + 1]]);
                        Vector3 c = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t + 2]]);
                        Part part;
                        int piece = island[triangles[t]];
                        bool below = ((a.y + b.y + c.y) / 3f) < topY - depth;
                        bool isLeg = !source.IsPlayingArea && below
                            && islandBounds.TryGetValue(piece, out Bounds pieceBounds)
                            && Mathf.Max(pieceBounds.size.x, pieceBounds.size.z) < 0.35f * tableSpan;
                        if (source.IsPlayingArea) part = playingArea;
                        else if (isLeg) part = LegPart(legs, legByIsland, ((long)s << 32) | (uint)piece);
                        else if (((a.y + b.y + c.y) / 3f) >= topY - depth) part = sides;
                        else part = lowerFrame;

                        // Without a surface object, the flat triangles at the top's height are the playing area.
                        if (surface == null && part == sides && IsFlatAt(a, b, c, topY)) part = playingArea;

                        List<int> into = part.TrianglesFor(material);
                        into.Add(Vertex(part, source, s, triangles[t], pivot, inverse));
                        into.Add(Vertex(part, source, s, triangles[t + 1], pivot, inverse));
                        into.Add(Vertex(part, source, s, triangles[t + 2], pivot, inverse));
                        part.TriangleCount++;
                    }
                }
            }

            if (playingArea.TriangleCount == 0)
            {
                report = log.AppendLine("The playing area came out empty.").ToString();
                return null;
            }

            string safeName = SafeName(settings.DisplayName);
            string folder = $"{TablesFolder}/{safeName}";
            EnsureFolder(folder);
            Mesh playingAreaMesh = SaveMesh(playingArea, $"{folder}/{safeName}_PlayingArea.asset", $"{safeName} Playing Area");
            Mesh sidesMesh = sides.TriangleCount > 0 ? SaveMesh(sides, $"{folder}/{safeName}_Sides.asset", $"{safeName} Sides") : null;
            Mesh lowerFrameMesh = lowerFrame.TriangleCount > 0 ? SaveMesh(lowerFrame, $"{folder}/{safeName}_LowerFrame.asset", $"{safeName} Lower Frame") : null;
            Mesh[] legMeshes = new Mesh[legs.Count];
            Vector3[] legPivots = new Vector3[legs.Count];
            for (int i = 0; i < legs.Count; i++)
            {
                legPivots[i] = Repivot(legs[i]);
                legMeshes[i] = SaveMesh(legs[i], $"{folder}/{safeName}_Leg{i + 1}.asset", $"{safeName} Leg {i + 1}");
            }

            // Splitting again updates the existing prefab in place, so its parts keep their identity and anything
            // that refers to them (the scene's table, its surface and collider) stays connected.
            string prefabPath = $"{folder}/{safeName}.prefab";
            bool updating = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            GameObject prefabRoot = updating ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(safeName);
            try
            {
                TableModel model = prefabRoot.GetComponent<TableModel>();
                if (model == null) model = prefabRoot.AddComponent<TableModel>();
                Transform oldLowerFrame = prefabRoot.transform.Find(TableModel.LowerFrameName);
                if (oldLowerFrame != null)
                {
                    for (int i = oldLowerFrame.childCount - 1; i >= 0; i--)
                    {
                        if (oldLowerFrame.GetChild(i).name.StartsWith(TableModel.LegName + "_", StringComparison.Ordinal))
                        {
                            UnityEngine.Object.DestroyImmediate(oldLowerFrame.GetChild(i).gameObject);
                        }
                    }
                }

                MeshRenderer playingAreaRenderer = AddPart(prefabRoot, TableModel.PlayingAreaName, playingAreaMesh, playingArea);
                MeshRenderer sidesRenderer = AddPart(prefabRoot, TableModel.SidesName, sidesMesh, sides);
                MeshRenderer lowerFrameRenderer = AddPart(prefabRoot, TableModel.LowerFrameName, lowerFrameMesh, lowerFrame, legs.Count > 0);
                Transform[] legTransforms = new Transform[legs.Count];
                for (int i = 0; i < legs.Count; i++)
                {
                    MeshRenderer leg = AddPart(lowerFrameRenderer.gameObject, $"{TableModel.LegName}_{i + 1}", legMeshes[i], legs[i]);
                    leg.transform.localPosition = legPivots[i];
                    legTransforms[i] = leg.transform;
                }

                model.Configure(safeName.ToLowerInvariant(), string.IsNullOrWhiteSpace(settings.DisplayName) ? safeName : settings.DisplayName.Trim(), playingAreaRenderer, sidesRenderer, lowerFrameRenderer, null, legTransforms);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                TableModel savedModel = saved != null ? saved.GetComponent<TableModel>() : null;
                if (savedModel == null)
                {
                    report = log.AppendLine($"The prefab could not be saved at {prefabPath}.").ToString();
                    return null;
                }

                AddToLibrary(savedModel, log);
                log.AppendLine($"Playing area {playingArea.TriangleCount} triangles, sides {sides.TriangleCount}, lower frame {lowerFrame.TriangleCount}, legs {legs.Count}.");
                log.AppendLine($"Saved {prefabPath}.");
                Bounds area = playingAreaMesh.bounds;
                log.AppendLine($"Playing area size {area.size.x:0.##} × {area.size.z:0.##}.");
                report = log.ToString();
                return savedModel;
            }
            finally
            {
                if (updating) PrefabUtility.UnloadPrefabContents(prefabRoot);
                else UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static List<Source> ReadSources(GameObject root, StringBuilder log, out PhysicalTabletopSurface surface)
        {
            surface = null;
            foreach (PhysicalTabletopSurface candidate in root.GetComponentsInChildren<PhysicalTabletopSurface>(false))
            {
                if (candidate.GetComponent<MeshFilter>() != null && candidate.GetComponent<Collider>() != null)
                {
                    surface = candidate;
                    break;
                }
            }

            List<Source> sources = new List<Source>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Mesh mesh = filter.sharedMesh;
                if (renderer == null || !renderer.enabled || mesh == null) continue;
                Vector3[] vertices = mesh.vertices;
                if (vertices.Length == 0 && mesh.vertexCount > 0)
                {
                    log.AppendLine($"'{mesh.name}' cannot be read. Select its model file, tick Read/Write in the Model tab, Apply, and split again.");
                    return null;
                }

                Source source = new Source
                {
                    Filter = filter,
                    Materials = renderer.sharedMaterials,
                    Vertices = vertices,
                    Normals = mesh.normals,
                    Tangents = mesh.tangents,
                    ToWorld = filter.transform.localToWorldMatrix,
                    NormalToWorld = filter.transform.localToWorldMatrix.inverse.transpose,
                    IsPlayingArea = surface != null && filter.gameObject == surface.gameObject,
                };
                mesh.GetUVs(0, source.Uv0);
                mesh.GetUVs(1, source.Uv1);
                sources.Add(source);
            }

            if (sources.Count == 0)
            {
                log.AppendLine("The model has no visible meshes.");
                return null;
            }

            return sources;
        }

        private static bool TryTop(PhysicalTabletopSurface surface, out float topY, out Vector3 centre)
        {
            topY = 0f;
            centre = Vector3.zero;
            Collider collider = surface.GetComponent<Collider>();
            if (collider == null) return false;
            Bounds bounds = collider.bounds;
            if (bounds.size.sqrMagnitude <= 0f)
            {
                // A collider on an object outside a scene has no bounds: use the renderer's.
                Renderer renderer = surface.GetComponent<Renderer>();
                if (renderer == null) return false;
                bounds = renderer.bounds;
            }

            topY = bounds.max.y;
            centre = bounds.center;
            float reach = bounds.extents.magnitude + 1f;
            if (collider.Raycast(new Ray(bounds.center + (Vector3.up * reach), Vector3.down), out RaycastHit hit, reach * 2f))
            {
                topY = hit.point.y;
            }

            return true;
        }

        // The height with the most flat, upward-facing area, and the centre of that area.
        private static bool TryFindFlatTop(List<Source> sources, out float topY, out Vector3 centre)
        {
            Dictionary<int, float> areaByHeight = new Dictionary<int, float>();
            const float bucket = 0.01f;
            foreach (Source source in sources)
            {
                int[] triangles = source.Filter.sharedMesh.triangles;
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 a = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t]]);
                    Vector3 b = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t + 1]]);
                    Vector3 c = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t + 2]]);
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    if (cross.sqrMagnitude <= 0f || cross.normalized.y < FlatNormal) continue;
                    int key = Mathf.RoundToInt(((a.y + b.y + c.y) / 3f) / bucket);
                    areaByHeight.TryGetValue(key, out float area);
                    areaByHeight[key] = area + (0.5f * cross.magnitude);
                }
            }

            topY = 0f;
            centre = Vector3.zero;
            float best = 0f;
            foreach (KeyValuePair<int, float> pair in areaByHeight)
            {
                if (pair.Value > best)
                {
                    best = pair.Value;
                    topY = pair.Key * bucket;
                }
            }

            if (best <= 0f) return false;
            Bounds bounds = new Bounds();
            bool any = false;
            foreach (Source source in sources)
            {
                int[] triangles = source.Filter.sharedMesh.triangles;
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 a = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t]]);
                    Vector3 b = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t + 1]]);
                    Vector3 c = source.ToWorld.MultiplyPoint3x4(source.Vertices[triangles[t + 2]]);
                    if (!IsFlatAt(a, b, c, topY)) continue;
                    if (!any)
                    {
                        bounds = new Bounds(a, Vector3.zero);
                        any = true;
                    }

                    bounds.Encapsulate(a);
                    bounds.Encapsulate(b);
                    bounds.Encapsulate(c);
                }
            }

            centre = bounds.center;
            return any;
        }

        private static bool IsFlatAt(Vector3 a, Vector3 b, Vector3 c, float height)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            return cross.sqrMagnitude > 0f && cross.normalized.y >= FlatNormal
                && Mathf.Abs(((a.y + b.y + c.y) / 3f) - height) <= 0.02f;
        }

        // Copies one source vertex into the part once, moved into the model's own space (pivot at the playing area).
        private static int Vertex(Part part, Source source, int sourceIndex, int vertex, Vector3 pivot, Quaternion inverse)
        {
            long key = ((long)sourceIndex << 32) | (uint)vertex;
            if (part.Remap.TryGetValue(key, out int existing)) return existing;
            Vector3 world = source.ToWorld.MultiplyPoint3x4(source.Vertices[vertex]);
            part.Positions.Add(inverse * (world - pivot));
            Vector3 normal = source.Normals.Length > vertex
                ? inverse * source.NormalToWorld.MultiplyVector(source.Normals[vertex]).normalized
                : Vector3.up;
            part.Normals.Add(normal);
            if (source.Tangents.Length > vertex)
            {
                Vector4 t = source.Tangents[vertex];
                Vector3 tangent = inverse * source.ToWorld.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                part.Tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, t.w));
            }

            part.Uv0.Add(source.Uv0.Count > vertex ? source.Uv0[vertex] : Vector2.zero);
            part.Uv1.Add(source.Uv1.Count > vertex ? source.Uv1[vertex] : Vector2.zero);
            int index = part.Positions.Count - 1;
            part.Remap[key] = index;
            return index;
        }

        private static Mesh SaveMesh(Part part, string path, string name)
        {
            Mesh mesh = new Mesh { name = name };
            if (part.Positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(part.Positions);
            mesh.SetNormals(part.Normals);
            if (part.Tangents.Count == part.Positions.Count) mesh.SetTangents(part.Tangents);
            mesh.SetUVs(0, part.Uv0);
            mesh.SetUVs(1, part.Uv1);
            mesh.subMeshCount = part.Triangles.Count;
            for (int i = 0; i < part.Triangles.Count; i++) mesh.SetTriangles(part.Triangles[i], i, false);
            mesh.RecalculateBounds();

            // Splitting again keeps the same file (and every reference to it).
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Reuses the part's object when the prefab already has it (keeping its identity), otherwise makes it.
        private static MeshRenderer AddPart(GameObject root, string name, Mesh mesh, Part part, bool evenWhenEmpty = false)
        {
            Transform existing = root.transform.Find(name);
            if (mesh == null && !evenWhenEmpty)
            {
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                return null;
            }

            GameObject child = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null) child.transform.SetParent(root.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter == null) filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = part.Materials.ToArray();
            return renderer;
        }

        private static Part LegPart(List<Part> legs, Dictionary<long, Part> byIsland, long key)
        {
            if (byIsland.TryGetValue(key, out Part leg)) return leg;
            leg = new Part();
            byIsland[key] = leg;
            legs.Add(leg);
            return leg;
        }

        // Moves a leg's points so its own origin is the centre of its top; returns where that top sits in the model.
        private static Vector3 Repivot(Part leg)
        {
            Bounds bounds = new Bounds(leg.Positions[0], Vector3.zero);
            foreach (Vector3 p in leg.Positions) bounds.Encapsulate(p);
            Vector3 pivot = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            for (int i = 0; i < leg.Positions.Count; i++) leg.Positions[i] -= pivot;
            return pivot;
        }

        // The connected pieces of the part of a mesh below <paramref name="belowY"/> (by triangle centre): points
        // at the same place count as joined (texture seams split them). Returns each vertex's piece and the world
        // bounds of each piece (from the triangles below only).
        private static int[] Islands(Source source, float belowY, out Dictionary<int, Bounds> bounds)
        {
            int count = source.Vertices.Length;
            int[] parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;
            Dictionary<Vector3Int, int> samePlace = new Dictionary<Vector3Int, int>();
            Vector3[] world = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                world[i] = source.ToWorld.MultiplyPoint3x4(source.Vertices[i]);
                Vector3Int key = Vector3Int.RoundToInt(world[i] * 1000f);
                if (samePlace.TryGetValue(key, out int first)) Join(parent, i, first);
                else samePlace[key] = i;
            }

            int[] triangles = source.Filter.sharedMesh.triangles;
            bool[] used = new bool[count];
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                if (((world[triangles[t]].y + world[triangles[t + 1]].y + world[triangles[t + 2]].y) / 3f) >= belowY) continue;
                Join(parent, triangles[t], triangles[t + 1]);
                Join(parent, triangles[t], triangles[t + 2]);
                used[triangles[t]] = used[triangles[t + 1]] = used[triangles[t + 2]] = true;
            }

            bounds = new Dictionary<int, Bounds>();
            for (int i = 0; i < count; i++) parent[i] = Find(parent, i);
            for (int i = 0; i < count; i++)
            {
                if (!used[i]) continue;
                if (bounds.TryGetValue(parent[i], out Bounds b))
                {
                    b.Encapsulate(world[i]);
                    bounds[parent[i]] = b;
                }
                else
                {
                    bounds[parent[i]] = new Bounds(world[i], Vector3.zero);
                }
            }

            return parent;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        private static void Join(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb) parent[ra] = rb;
        }

        private static void AddToLibrary(TableModel model, StringBuilder log)
        {
            TableModelLibrary library = AssetDatabase.LoadAssetAtPath<TableModelLibrary>(LibraryPath);
            if (library == null)
            {
                EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
                library = ScriptableObject.CreateInstance<TableModelLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
                log.AppendLine($"Made the table model list {LibraryPath}.");
            }

            if (library.AddOrReplace(model))
            {
                EditorUtility.SetDirty(library);
                log.AppendLine("Added to the Table menu (Resources/TableModels).");
            }

            AssetDatabase.SaveAssets();
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        internal static string SafeName(string value)
        {
            StringBuilder name = new StringBuilder();
            foreach (char c in value ?? string.Empty)
            {
                if (char.IsLetterOrDigit(c)) name.Append(c);
                else if ((c == ' ' || c == '-' || c == '_') && name.Length > 0 && name[name.Length - 1] != '-') name.Append('-');
            }

            string result = name.ToString().Trim('-');
            return result.Length == 0 ? "Table" : result;
        }
    }
}
