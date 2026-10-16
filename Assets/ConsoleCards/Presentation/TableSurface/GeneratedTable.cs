using System;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;

namespace ConsoleCards.Presentation.TableSurface
{
    /// <summary>
    /// A table built from a <see cref="TableSpec"/> at runtime (doc 30, B2; T1): the top (playing area, sides and
    /// underside) with its surface collider, and the edge. It stands where the classic table's playing area is and
    /// uses the classic table's frame, so Game Templates, the Empty Table and saved positions land in the same
    /// places. Rebuilt only when the spec changes, never per frame; the meshes are made once and reused (B5).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedTable : MonoBehaviour
    {
        // Points about this far apart (world units) on the drawn outline and on the simpler collider outline (B5).
        private const float VisualSpacing = 0.25f;
        private const float ColliderSpacing = 1f;
        private const int ColliderPointCap = 128;

        private readonly TableOutline outline = new TableOutline();
        private readonly TableOutline colliderOutline = new TableOutline(ColliderPointCap);
        private readonly TableMeshBuilder builder = new TableMeshBuilder();
        private readonly Material[] topMaterials = new Material[2];

        // The sides, underside and edge: one plain copy of the classic body material, made once, without its
        // picture. The classic model's texture is laid out for that model only, so it would look scrambled here.
        private static readonly Color BodyColour = new Color(0.106f, 0.106f, 0.114f, 1f); // #1B1B1D (doc 30, C1)
        private Material bodyMaterial;

        private GameObject top;
        private GameObject edge;
        private MeshFilter topFilter;
        private MeshRenderer topRenderer;
        private MeshCollider topCollider;
        private PhysicalTabletopSurface surface;
        private MeshFilter edgeFilter;
        private MeshRenderer edgeRenderer;
        private Mesh topMesh;
        private Mesh colliderMesh;
        private Mesh edgeMesh;

        /// <summary>The spec this table was last built from (a copy), or null before the first build.</summary>
        public TableSpec Spec { get; private set; }

        /// <summary>
        /// Makes an empty, inactive table under <paramref name="parent"/>. It stays inactive (and is not a placement
        /// surface) until <see cref="TryBuild"/> succeeds.
        /// </summary>
        public static GeneratedTable Create(Transform parent, int topLayer, int edgeLayer)
        {
            GameObject root = new GameObject("Generated Table");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            GeneratedTable table = root.AddComponent<GeneratedTable>();
            table.top = new GameObject("Top");
            table.top.layer = topLayer;
            table.top.transform.SetParent(root.transform, false);
            table.topFilter = table.top.AddComponent<MeshFilter>();
            table.topRenderer = table.top.AddComponent<MeshRenderer>();
            table.topCollider = table.top.AddComponent<MeshCollider>();
            table.surface = table.top.AddComponent<PhysicalTabletopSurface>();
            // Off until its collider has a mesh: a surface that turns on without one reports a setup error.
            table.surface.enabled = false;
            table.edge = new GameObject("Edge");
            table.edge.layer = edgeLayer;
            table.edge.transform.SetParent(root.transform, false);
            table.edgeFilter = table.edge.AddComponent<MeshFilter>();
            table.edgeRenderer = table.edge.AddComponent<MeshRenderer>();
            table.topMesh = new Mesh { name = "Generated Table Top" };
            table.colliderMesh = new Mesh { name = "Generated Table Collider" };
            table.edgeMesh = new Mesh { name = "Generated Table Edge" };
            table.topFilter.sharedMesh = table.topMesh;
            table.edgeFilter.sharedMesh = table.edgeMesh;
            return table;
        }

        /// <summary>
        /// Builds (or rebuilds) the table for <paramref name="spec"/> at <paramref name="frame"/> and turns it on.
        /// On a broken spec nothing changes, the last good table stays, and <paramref name="error"/> says why.
        /// </summary>
        public bool TryBuild(TableSpec spec, in TableFrame frame, Material playingArea, Material body, out string error)
        {
            error = null;
            if (spec == null || spec.shape == TableShape.Classic)
            {
                error = "the Classic table is the scene's own table";
                return false;
            }

            TableSpec clean = spec.Copy();
            clean.Clamp();
            float unit = frame.WorldPerTableUnit;
            if (!outline.Build(clean, unit, VisualSpacing) || !colliderOutline.Build(clean, unit, ColliderSpacing))
            {
                // Put the outlines back to the last good table so Contains keeps answering for it.
                if (Spec != null)
                {
                    outline.Build(Spec, unit, VisualSpacing);
                    colliderOutline.Build(Spec, unit, ColliderSpacing);
                }

                error = "that shape and size do not make a usable table";
                return false;
            }

            transform.SetPositionAndRotation(frame.Centre, frame.Rotation);
            transform.localScale = Vector3.one;
            Vector2 textureSize = frame.ClassicSize * unit;
            float thickness = clean.thickness * unit;

            builder.BuildTop(outline, thickness, textureSize, topMesh, true);
            builder.BuildTop(colliderOutline, thickness, textureSize, colliderMesh, false);
            Material sides = BodyMaterial(body != null ? body : playingArea);
            topMaterials[0] = playingArea;
            topMaterials[1] = sides;
            topRenderer.sharedMaterials = topMaterials;

            bool hasEdge = clean.edge == TableEdge.Lip;
            if (hasEdge)
            {
                builder.BuildLip(outline, clean.edgeHeight * unit, clean.edgeWidth * unit, thickness, textureSize.x, edgeMesh);
                edgeRenderer.sharedMaterial = sides;
            }

            edge.SetActive(hasEdge);

            // The collider takes its mesh while active (reassigning makes physics take the new shape); only then is
            // the surface turned on, so it never starts without a mesh.
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            topCollider.sharedMesh = null;
            topCollider.sharedMesh = colliderMesh;
            if (topCollider.sharedMesh == null)
            {
                surface.enabled = false;
                Hide();
                error = $"the table's collider could not be made ({colliderMesh.vertexCount} points)";
                return false;
            }

            surface.ConfigureTemplateOrigin(true, frame.Frame, frame.LocalUnitsPerTableUnit);
            if (!surface.enabled) surface.enabled = true;
            Spec = clean;
            return true;
        }

        /// <summary>Turns the table off: it stops drawing and stops being a placement surface.</summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>How far the edge is from the table's centre in a flat direction of the table's own space.</summary>
        public float EdgeDistance(Vector2 localDirection) => Spec == null ? 0f : outline.DistanceToEdge(localDirection);

        /// <summary>True when the world point lies over the playing area (height ignored).</summary>
        public bool Contains(Vector3 world)
        {
            if (Spec == null) return false;
            Vector3 local = transform.InverseTransformPoint(world);
            return outline.Contains(new Vector2(local.x, local.z));
        }

        /// <summary>The world point itself when it is over the playing area, otherwise the nearest point on its edge.</summary>
        public Vector3 ClosestPoint(Vector3 world)
        {
            if (Spec == null) return world;
            Vector3 local = transform.InverseTransformPoint(world);
            Vector2 flat = outline.ClosestPoint(new Vector2(local.x, local.z));
            return transform.TransformPoint(new Vector3(flat.x, local.y, flat.y));
        }

        private void OnDestroy()
        {
            DestroyMesh(topMesh);
            DestroyMesh(colliderMesh);
            DestroyMesh(edgeMesh);
            if (bodyMaterial != null) Destroy(bodyMaterial);
        }

        // Made once from the classic body material: same shader and lighting, no picture, the theme's black.
        private Material BodyMaterial(Material template)
        {
            if (bodyMaterial != null || template == null) return bodyMaterial != null ? bodyMaterial : template;
            bodyMaterial = new Material(template) { name = "Generated Table Body" };
            if (bodyMaterial.HasProperty("_BaseMap")) bodyMaterial.SetTexture("_BaseMap", null);
            if (bodyMaterial.HasProperty("_MainTex")) bodyMaterial.SetTexture("_MainTex", null);
            if (bodyMaterial.HasProperty("_BumpMap")) bodyMaterial.SetTexture("_BumpMap", null);
            if (bodyMaterial.HasProperty("_BaseColor")) bodyMaterial.SetColor("_BaseColor", BodyColour);
            if (bodyMaterial.HasProperty("_Color")) bodyMaterial.SetColor("_Color", BodyColour);
            return bodyMaterial;
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh != null) Destroy(mesh);
        }
    }

    /// <summary>
    /// Where a generated table stands and how big a table unit is, taken from the classic table (T1): the centre of
    /// its playing area at the top's height, its facing, its frame and its playing-area size in table units.
    /// </summary>
    public readonly struct TableFrame
    {
        public TableFrame(Vector3 centre, Quaternion rotation, Transform frame, float localUnitsPerTableUnit,
            float worldPerTableUnit, Vector2 classicSize)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            Centre = centre;
            Rotation = rotation;
            Frame = frame;
            LocalUnitsPerTableUnit = localUnitsPerTableUnit;
            WorldPerTableUnit = worldPerTableUnit;
            ClassicSize = classicSize;
        }

        public Vector3 Centre { get; }
        public Quaternion Rotation { get; }
        public Transform Frame { get; }
        public float LocalUnitsPerTableUnit { get; }
        public float WorldPerTableUnit { get; }

        /// <summary>The classic playing area's width and depth in table units.</summary>
        public Vector2 ClassicSize { get; }
    }
}
