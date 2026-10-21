using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>
    /// AVATAR-1 (owner, 2026-10-21: "keep it simple and use avatars"): a seat's avatar, the player's profile picture
    /// standing upright just past the table's edge behind their Console, in a frame of the seat colour that glows
    /// brighter on their turn. Its pivot is the picture's bottom edge; its front (the picture) faces -Z, so the table
    /// turns it with its back to the outside. A Final World prefab made by the Persona builder. Per-frame calls do
    /// not allocate.
    /// </summary>
    public sealed class PersonaAvatarView : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer picture;
        [SerializeField] private Renderer frame;

        private MaterialPropertyBlock block;
        private Color colour = Color.white;
        private float appliedGlow = -1f;

        public void Apply(Texture2D avatar, Color seatColour)
        {
            if (block == null) block = new MaterialPropertyBlock();
            colour = seatColour;
            if (picture != null)
            {
                picture.enabled = avatar != null;
                if (avatar != null)
                {
                    picture.GetPropertyBlock(block);
                    block.SetTexture(BaseMapId, avatar);
                    picture.SetPropertyBlock(block);
                }
            }

            appliedGlow = -1f;
        }

        public void Place(Vector3 position, Quaternion rotation, float scale)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (Mathf.Abs(transform.localScale.x - scale) > 0.0001f) transform.localScale = new Vector3(scale, scale, scale);
        }

        /// <summary>How brightly the frame shows the seat colour (brighter on the player's turn).</summary>
        public void SetGlow(float glow)
        {
            if (frame == null || Mathf.Abs(glow - appliedGlow) < 0.005f) return;
            if (block == null) block = new MaterialPropertyBlock();
            appliedGlow = glow;
            frame.GetPropertyBlock(block);
            Color lit = colour * glow;
            lit.a = 1f;
            block.SetColor(BaseColorId, lit);
            frame.SetPropertyBlock(block);
        }
    }
}
