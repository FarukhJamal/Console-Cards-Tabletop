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
            frame.localPosition = new Vector3(0f, thickness * 0.5f, 0f);
            frame.localRotation = Quaternion.identity;
            frame.localScale = new Vector3(grid.Width, thickness, grid.Depth);
            surface.center = new Vector3(0f, thickness * 0.5f, 0f);
            surface.size = new Vector3(grid.Width, thickness, grid.Depth);

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
            return Mathf.Abs(local.x) <= Grid.Width * 0.5f && Mathf.Abs(local.z) <= Grid.Depth * 0.5f;
        }
    }
}
