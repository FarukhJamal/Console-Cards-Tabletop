using UnityEngine;

namespace ConsoleCards.Presentation.Catalog
{
    /// <summary>
    /// The game board as a catalog component (doc 22, C3c): the root carries the board's physical surface (a
    /// BoxCollider sized to the play area at runtime) and names its visual child and the material its board
    /// renderers use. Made from the scene board by Console Cards > Toolbox > Create Game Board Prefab from Scene.
    /// </summary>
    public sealed class GameBoardView : MonoBehaviour
    {
        [SerializeField] private Collider surface;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Material visualMaterial;

        public Collider Surface => surface;
        public Transform VisualRoot => visualRoot;
        public Material VisualMaterial => visualMaterial;

        public bool IsComplete => surface != null && visualRoot != null && visualRoot.IsChildOf(surface.transform);
    }
}
