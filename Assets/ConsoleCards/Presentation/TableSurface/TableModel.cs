using System;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;

namespace ConsoleCards.Presentation.TableSurface
{
    /// <summary>
    /// A table model (doc 30, B8; T1b): any table, the client's own included, made of three separate parts —
    /// the playing area (where pieces land), the sides (rim) and the lower frame (legs, base). Sits on the prefab
    /// root, which is the centre of the playing area at its top surface (Y up, Z forward, front is −Z, scale 1).
    /// Made with Console Cards > Table > Split Table Model and checked with Check Table Model. At runtime the game
    /// turns the playing area into the placement surface; each part keeps its own material for the texture tools.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableModel : MonoBehaviour
    {
        public const string PlayingAreaName = "PlayingArea";
        public const string SidesName = "Sides";
        public const string LowerFrameName = "LowerFrame";
        public const string ColliderName = "PlayingArea_Collider";
        public const string LegName = "Leg";

        [SerializeField, Tooltip("A short, unique name used in saves (letters, digits, '-').")]
        private string modelId = string.Empty;
        [SerializeField, Tooltip("The name shown in the Table menu.")]
        private string displayName = string.Empty;
        [SerializeField, Tooltip("The flat middle where pieces land. Flat, facing up, its top at the root's height.")]
        private MeshRenderer playingArea;
        [SerializeField, Tooltip("The rim and everything around the playing area.")]
        private MeshRenderer sides;
        [SerializeField, Tooltip("Legs, frame or pedestal: everything below the top. Decoration only.")]
        private MeshRenderer lowerFrame;
        [SerializeField, Tooltip("Each leg as its own piece under LowerFrame, its origin at the centre of its top. Built shapes move legs in so they stay under the table.")]
        private Transform[] legs = Array.Empty<Transform>();
        [SerializeField, Tooltip("Optional simpler mesh for the playing area's collider. Empty: the playing area's own mesh.")]
        private Mesh colliderMesh;
        [SerializeField, Tooltip("How the model may be resized (sizes come with the Table tools, T3).")]
        private TableResizeMode resizeMode = TableResizeMode.Fixed;
        [SerializeField, Min(0.1f)] private float minimumScale = 1f;
        [SerializeField, Min(0.1f)] private float maximumScale = 1f;

        public string ModelId => modelId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? modelId : displayName;
        public MeshRenderer PlayingArea => playingArea;
        public MeshRenderer Sides => sides;
        public MeshRenderer LowerFrame => lowerFrame;
        public Mesh ColliderMesh => colliderMesh;
        public Transform[] Legs => legs ?? Array.Empty<Transform>();
        public TableResizeMode ResizeMode => resizeMode;
        public float MinimumScale => minimumScale;
        public float MaximumScale => Mathf.Max(minimumScale, maximumScale);

        /// <summary>Fills the model in one go (used by the editor tools that make table models).</summary>
        public void Configure(
            string id,
            string shownName,
            MeshRenderer playingAreaPart,
            MeshRenderer sidesPart,
            MeshRenderer lowerFramePart,
            Mesh simplerCollider,
            Transform[] legPieces = null)
        {
            legs = legPieces ?? Array.Empty<Transform>();
            modelId = id ?? string.Empty;
            displayName = shownName ?? string.Empty;
            playingArea = playingAreaPart;
            sides = sidesPart;
            lowerFrame = lowerFramePart;
            colliderMesh = simplerCollider;
        }

        /// <summary>
        /// Makes the playing area the placement surface and the Template Layout Origin with the classic frame, so
        /// games land in the same places. The collider gets its mesh before the surface turns on. False with a
        /// reason when the model cannot be used.
        /// </summary>
        internal bool TryMakeSurface(Transform frame, float localUnitsPerTableUnit, out string error)
        {
            error = null;
            if (playingArea == null)
            {
                error = $"the model has no {PlayingAreaName} part";
                return false;
            }

            MeshFilter filter = playingArea.GetComponent<MeshFilter>();
            Mesh mesh = colliderMesh != null ? colliderMesh : filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                error = $"the {PlayingAreaName} part has no mesh";
                return false;
            }

            GameObject part = playingArea.gameObject;
            if (part.GetComponents<Collider>().Length > 1)
            {
                error = $"the {PlayingAreaName} part has more than one collider; leave colliders to the game";
                return false;
            }

            MeshCollider meshCollider = part.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                if (part.GetComponent<Collider>() != null)
                {
                    error = $"the {PlayingAreaName} part has a collider of its own; leave colliders to the game";
                    return false;
                }

                meshCollider = part.AddComponent<MeshCollider>();
            }

            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            if (meshCollider.sharedMesh == null)
            {
                error = $"the {PlayingAreaName} collider could not be made";
                return false;
            }

            PhysicalTabletopSurface surface = part.GetComponent<PhysicalTabletopSurface>();
            if (surface == null) surface = part.AddComponent<PhysicalTabletopSurface>();
            surface.ConfigureTemplateOrigin(true, frame, localUnitsPerTableUnit);
            if (!surface.enabled) surface.enabled = true;
            return true;
        }
    }

    /// <summary>How a table model may be resized (doc 30, B8). The sizes themselves come with the Table tools (T3).</summary>
    public enum TableResizeMode
    {
        /// <summary>The model's own size.</summary>
        Fixed = 0,

        /// <summary>Scales evenly between the minimum and maximum scale.</summary>
        Uniform = 1,

        /// <summary>Rectangles and squares: corners stay, straight parts stretch (needs the sides split into pieces).</summary>
        Stretch = 2,
    }
}
