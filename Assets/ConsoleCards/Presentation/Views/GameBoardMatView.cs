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

            spaceTemplate.SetActive(false);
            int count = grid.Columns * grid.Rows;
            while (spaces.Count < count)
            {
                GameObject space = Instantiate(spaceTemplate, spacesRoot, false);
                space.name = "Space";
                spaces.Add(space);
            }

            for (int i = 0; i < spaces.Count; i++)
            {
                GameObject space = spaces[i];
                bool used = i < count;
                if (space.activeSelf != used) space.SetActive(used);
                if (!used) continue;
                grid.SpaceCentre(i % grid.Columns, i / grid.Columns, out float x, out float z);
                space.transform.localPosition = new Vector3(x, thickness + 0.001f, z);
                space.transform.localRotation = Quaternion.identity;
                space.transform.localScale = new Vector3(grid.SpaceWidth, 1f, grid.SpaceDepth);
            }
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
