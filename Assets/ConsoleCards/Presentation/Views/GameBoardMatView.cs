using System.Collections.Generic;
using ConsoleCards.Core.Domain.Boards;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// Game board v2 (doc 22 §15): an orange-framed mat with a grid of card spaces. The frame, the surface collider
    /// and the spaces are laid out from a <see cref="BoardGrid"/>, so the grid can change size in the game while the
    /// physical mat stays the same. Spaces are copies of one authored space template, kept and reused (no new
    /// objects once a size has been shown). Presentation-only: the grid is Runtime State.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBoardMatView : MonoBehaviour
    {
        [SerializeField] private BoxCollider surface;
        [SerializeField] private Transform frame;
        [SerializeField] private Transform spacesRoot;
        [SerializeField] private GameObject spaceTemplate;
        [SerializeField, Min(0.005f)] private float thickness = 0.04f;

        private readonly List<GameObject> spaces = new List<GameObject>();
        private bool hasGrid;

        // T-1: the mat's own size and centre (mat-local); the spaces area with its frame unless SetMatSize says more.
        private Vector2 matSize;
        private Vector2 matCentre;

        // BOARD-1 (owner, 2026-10-23): a game's playmat picture (Trap Floor: the printed 35 x 24" board with its grid
        // taken out) lies on the mat, and the spaces become thin outlines in the printed grid's colour, laid out by
        // the grid so any number of columns and rows fits.
        private static readonly Color PictureOutlineColour = new Color(0.33f, 0.90f, 0.78f, 1f);
        private const float PictureOutlineWidth = 0.025f;
        private Texture2D picture;
        private Transform pictureQuad;
        private Material pictureMaterial;
        private Material outlineMaterial;
        private readonly List<Transform> outlineSpaces = new List<Transform>();

        public BoardGrid Grid { get; private set; }
        public bool HasGrid => hasGrid;
        public float Thickness => thickness;
        public BoxCollider Surface => surface;
        public bool IsComplete => surface != null && frame != null && spacesRoot != null && spaceTemplate != null;

        public void Apply(BoardGrid grid)
        {
            if (!grid.IsValid || !IsComplete) return;
            Grid = grid;
            hasGrid = true;
            frame.localRotation = Quaternion.identity;
            LayOutMat(new Vector2(grid.Width, grid.Depth), Vector2.zero);
            LayOutSpaces();
        }

        // The spaces for the grid: the authored space copies, or (with a playmat picture) outlines. Objects are made
        // only when the grid grows past what was shown before, then kept and reused.
        private void LayOutSpaces()
        {
            spaceTemplate.SetActive(false);
            BoardGrid grid = Grid;
            int count = grid.Columns * grid.Rows;
            bool outlines = picture != null;
            while (!outlines && spaces.Count < count)
            {
                GameObject space = Instantiate(spaceTemplate, spacesRoot, false);
                space.name = "Space";
                spaces.Add(space);
            }

            for (int i = 0; i < spaces.Count; i++)
            {
                GameObject space = spaces[i];
                bool used = !outlines && i < count;
                if (space.activeSelf != used) space.SetActive(used);
                if (!used) continue;
                grid.SpaceCentre(i % grid.Columns, i / grid.Columns, out float x, out float z);
                space.transform.localPosition = new Vector3(x, thickness + 0.001f, z);
                space.transform.localRotation = Quaternion.identity;
                space.transform.localScale = new Vector3(grid.SpaceWidth, 1f, grid.SpaceDepth);
            }

            while (outlines && outlineSpaces.Count < count) outlineSpaces.Add(CreateOutlineSpace());
            for (int i = 0; i < outlineSpaces.Count; i++)
            {
                Transform outline = outlineSpaces[i];
                bool used = outlines && i < count;
                if (outline.gameObject.activeSelf != used) outline.gameObject.SetActive(used);
                if (!used) continue;
                grid.SpaceCentre(i % grid.Columns, i / grid.Columns, out float x, out float z);
                outline.localPosition = new Vector3(x, thickness + 0.0015f, z);
                outline.localRotation = Quaternion.identity;
                LayOutOutline(outline, grid.SpaceWidth, grid.SpaceDepth);
            }
        }

        // One space outline: four thin bars round the space (near, far, left, right).
        private Transform CreateOutlineSpace()
        {
            if (outlineMaterial == null)
            {
                Renderer edge = FindSpaceRenderer("Edge");
                outlineMaterial = new Material(edge != null ? edge.sharedMaterial : frame.GetComponent<Renderer>().sharedMaterial)
                {
                    name = "Board Space Outline",
                };
                SetColour(outlineMaterial, PictureOutlineColour);
                if (outlineMaterial.HasProperty("_EmissionColor"))
                {
                    outlineMaterial.EnableKeyword("_EMISSION");
                    outlineMaterial.SetColor("_EmissionColor", PictureOutlineColour * 0.6f);
                }
            }

            Transform root = new GameObject("Space Outline").transform;
            root.SetParent(spacesRoot, false);
            for (int side = 0; side < 4; side++)
            {
                Transform bar = CreateFlatQuad($"Bar {side}", outlineMaterial).transform;
                bar.SetParent(root, false);
            }

            return root;
        }

        private static void LayOutOutline(Transform outline, float width, float depth)
        {
            float w = PictureOutlineWidth;
            for (int side = 0; side < outline.childCount && side < 4; side++)
            {
                Transform bar = outline.GetChild(side);
                bool across = side < 2; // near and far bars run across the space; left and right run along it
                float offset = across ? ((depth - w) * 0.5f * (side == 0 ? -1f : 1f)) : ((width - w) * 0.5f * (side == 2 ? -1f : 1f));
                bar.localPosition = across ? new Vector3(0f, 0f, offset) : new Vector3(offset, 0f, 0f);
                bar.localRotation = Quaternion.Euler(90f, 0f, 0f); // a flat quad facing up
                bar.localScale = across ? new Vector3(width, w, 1f) : new Vector3(w, depth, 1f);
            }
        }

        /// <summary>
        /// BOARD-1: draws <paramref name="texture"/> over the whole mat (null: the plain mat and the authored spaces).
        /// The picture follows the mat's size and centre.
        /// </summary>
        public void SetMatPicture(Texture2D texture)
        {
            if (!IsComplete) return;
            bool changed = !ReferenceEquals(picture, texture);
            picture = texture;
            if (picture != null && pictureQuad == null)
            {
                pictureMaterial = new Material(frame.GetComponent<Renderer>().sharedMaterial) { name = "Board Playmat" };
                pictureQuad = CreateFlatQuad("Playmat Picture", pictureMaterial).transform;
                pictureQuad.SetParent(transform, false);
            }

            if (pictureMaterial != null && changed)
            {
                pictureMaterial.SetTexture("_BaseMap", picture);
                pictureMaterial.SetTexture("_MainTex", picture);
                SetColour(pictureMaterial, Color.white);
                if (pictureMaterial.HasProperty("_Smoothness")) pictureMaterial.SetFloat("_Smoothness", 0.2f);
            }

            LayOutPicture();
            if (changed && hasGrid) LayOutSpaces();
        }

        private void LayOutPicture()
        {
            if (pictureQuad == null) return;
            bool shown = picture != null;
            if (pictureQuad.gameObject.activeSelf != shown) pictureQuad.gameObject.SetActive(shown);
            pictureQuad.localPosition = new Vector3(matCentre.x, thickness + 0.0005f, matCentre.y);
            pictureQuad.localRotation = Quaternion.Euler(90f, 0f, 0f); // picture top toward the mat's far side
            pictureQuad.localScale = new Vector3(matSize.x, matSize.y, 1f);
        }

        // A built-in quad without its collider (the mat's own surface takes the clicks), casting no shadow.
        private static GameObject CreateFlatQuad(string name, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return quad;
        }

        private Renderer FindSpaceRenderer(string name)
        {
            Renderer[] renderers = spaceTemplate.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].name == name) return renderers[i];
            }

            return null;
        }

        private static void SetColour(Material material, Color colour)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }

        private void OnDestroy()
        {
            if (pictureMaterial != null) Destroy(pictureMaterial);
            if (outlineMaterial != null) Destroy(outlineMaterial);
        }

        /// <summary>
        /// T-1 (owner, 2026-10-23: the play area is a 35 x 24" playmat): the mat is drawn this big (never smaller than
        /// its spaces with their frame), centred on <paramref name="localCentre"/> (mat-local x/z); the spaces stay
        /// where they are.
        /// </summary>
        public void SetMatSize(float width, float depth, Vector2 localCentre)
        {
            if (!hasGrid || !IsComplete) return;
            LayOutMat(new Vector2(Mathf.Max(width, Grid.Width), Mathf.Max(depth, Grid.Depth)), localCentre);
        }

        private void LayOutMat(Vector2 size, Vector2 centre)
        {
            matSize = size;
            matCentre = centre;
            frame.localPosition = new Vector3(centre.x, thickness * 0.5f, centre.y);
            frame.localScale = new Vector3(size.x, thickness, size.y);
            surface.center = new Vector3(centre.x, thickness * 0.5f, centre.y);
            surface.size = new Vector3(size.x, thickness, size.y);
            LayOutPicture();
        }

        public void SetSurfaceEnabled(bool enabled)
        {
            if (surface != null) surface.enabled = enabled;
        }

        /// <summary>The centre (on the mat top) of the space under a world point; false off the spaces area.</summary>
        public bool TryNearestSpace(Vector3 worldPoint, out Vector3 spaceCentre)
        {
            spaceCentre = Vector3.zero;
            if (!hasGrid) return false;
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            if (!Grid.TryNearestSpace(local.x, local.z, out int column, out int row)) return false;
            Grid.SpaceCentre(column, row, out float x, out float z);
            spaceCentre = transform.TransformPoint(new Vector3(x, thickness, z));
            return true;
        }

        /// <summary>True when a world point lies on the mat, frame included (for its right-click menu).</summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (!hasGrid) return false;
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x - matCentre.x) <= matSize.x * 0.5f && Mathf.Abs(local.z - matCentre.y) <= matSize.y * 0.5f;
        }
    }
}
