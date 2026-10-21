using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>
    /// PERSONA-2 (owner, 2026-10-21, with a picture: "Player 1", "Player 2"… panels standing along the table's edges):
    /// a seat's name tag, a glowing panel in the seat colour standing at the table edge behind the seat's Console,
    /// leaning back so it reads from above, with the player's name in it. Its pivot is the panel's bottom edge; its
    /// front (the readable side) faces -Z, so the table turns it with its back to the outside. A Final World prefab
    /// made by the Persona builder. Per-frame calls do not allocate.
    /// </summary>
    public sealed class PersonaTagView : MonoBehaviour
    {
        private static readonly int ColourId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [SerializeField] private Renderer panel;
        [SerializeField] private TextMesh nameLabel;

        private MaterialPropertyBlock block;
        private Color colour = Color.white;
        private float appliedGlow = -1f;

        public TextMesh NameLabel => nameLabel;

        public void Apply(string displayName, Color seatColour)
        {
            colour = seatColour;
            if (nameLabel != null)
            {
                nameLabel.text = displayName ?? string.Empty;
                nameLabel.color = Color.white;
            }

            appliedGlow = -1f;
        }

        public void Place(Vector3 position, Quaternion rotation, float scale)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (Mathf.Abs(transform.localScale.x - scale) > 0.0001f) transform.localScale = new Vector3(scale, scale, scale);
        }

        /// <summary>How brightly the panel glows (brighter on the player's turn).</summary>
        public void SetGlow(float glow)
        {
            if (panel == null || Mathf.Abs(glow - appliedGlow) < 0.005f) return;
            if (block == null) block = new MaterialPropertyBlock();
            appliedGlow = glow;
            panel.GetPropertyBlock(block);
            block.SetColor(ColourId, colour);
            block.SetFloat(IntensityId, glow);
            panel.SetPropertyBlock(block);
        }
    }
}
