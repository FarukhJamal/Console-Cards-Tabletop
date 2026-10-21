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
        private Transform tag;
        private Renderer tagPlate;
        private TextMesh tagText;

        /// <summary>The name tag's text, once it is made (the table gives it the depth-tested text material).</summary>
        public TextMesh NameTag => tagText;

        /// <summary>
        /// AVATAR-2 (owner, 2026-10-21: "name tags in front or on their head"): a plate in the seat colour with the
        /// player's name, over the picture or under it (made once, from the frame's own quad and material).
        /// </summary>
        public void ShowNameTag(string displayName, PersonaSet.NameTagPlace place, float letters, float gap)
        {
            if (place == PersonaSet.NameTagPlace.Off || frame == null)
            {
                if (tag != null && tag.gameObject.activeSelf) tag.gameObject.SetActive(false);
                return;
            }

            if (tag == null)
            {
                tag = new GameObject("NameTag").transform;
                tag.SetParent(transform, false);
                GameObject plate = new GameObject("Plate");
                plate.transform.SetParent(tag, false);
                plate.AddComponent<MeshFilter>().sharedMesh = frame.GetComponent<MeshFilter>().sharedMesh;
                MeshRenderer plateRenderer = plate.AddComponent<MeshRenderer>();
                plateRenderer.sharedMaterial = frame.sharedMaterial;
                plateRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tagPlate = plateRenderer;
                GameObject text = new GameObject("Name");
                text.transform.SetParent(tag, false);
                text.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                tagText = text.AddComponent<TextMesh>();
                tagText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tagText.fontSize = 64;
                tagText.fontStyle = FontStyle.Bold;
                tagText.anchor = TextAnchor.MiddleCenter;
                tagText.alignment = TextAlignment.Center;
                tagText.richText = false;
                MeshRenderer textRenderer = text.GetComponent<MeshRenderer>();
                if (tagText.font != null) textRenderer.sharedMaterial = tagText.font.material;
                textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // As wide as the frame; a long name gets smaller letters so it fits.
            Transform frameTransform = frame.transform;
            float width = frameTransform.localScale.x;
            string name = displayName ?? string.Empty;
            float fit = width * 0.9f / Mathf.Max(1f, name.Length * 0.62f * 6.4f);
            tagText.characterSize = Mathf.Min(letters, fit);
            tagText.text = name;
            tagText.color = Color.white;
            float height = tagText.characterSize * 6.4f * 1.6f;
            tagPlate.transform.localScale = new Vector3(width, height, 1f);
            float frameTop = frameTransform.localPosition.y + (frameTransform.localScale.y * 0.5f);
            float frameBottom = frameTransform.localPosition.y - (frameTransform.localScale.y * 0.5f);
            float y = place == PersonaSet.NameTagPlace.OverHead
                ? frameTop + gap + (height * 0.5f)
                : frameBottom - gap - (height * 0.5f);
            tag.localPosition = new Vector3(0f, y, 0f);
            if (!tag.gameObject.activeSelf) tag.gameObject.SetActive(true);
            appliedGlow = -1f;
        }

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
            if (tagPlate != null)
            {
                tagPlate.GetPropertyBlock(block);
                block.SetColor(BaseColorId, lit);
                tagPlate.SetPropertyBlock(block);
            }
        }
    }
}
