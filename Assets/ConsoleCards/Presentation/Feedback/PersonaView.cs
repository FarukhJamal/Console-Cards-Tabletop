using System;
using ConsoleCards.Presentation.Saving;
using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>
    /// PERSONA-2 (owner, 2026-10-21): one player's character (Universal Base Characters, CC0): both bodies, each with
    /// every hairstyle and the beard, one of each shown as the player chose. Full colour, sitting behind its seat
    /// facing the board, with an edge light in the seat colour, the Console-symbol emblem on its chest and the
    /// player's name over its head. Its front is +Z. A Final World prefab made by the Persona builder; the table and
    /// the Persona panel's preview place, dress and light it. Per-frame calls do not allocate.
    /// </summary>
    public sealed class PersonaView : MonoBehaviour
    {
        [Serializable]
        public sealed class PersonaBody
        {
            public string name = "Body";
            [Tooltip("Scaled sideways for the build (holds the model).")]
            public Transform root;
            [Tooltip("The imported character (its humanoid Avatar is posed).")]
            public Transform model;
            public Avatar avatar;
            public Transform head;
            public Renderer body;
            public Renderer eyes;
            public Renderer brows;
            [Tooltip("Every hairstyle, in the order of the view's hair names.")]
            public GameObject[] hairs = new GameObject[0];
            public GameObject beard;
            public Renderer emblem;
        }

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ColourModeId = Shader.PropertyToID("_ColourMode");
        private static readonly int UseOutfitId = Shader.PropertyToID("_UseOutfit");
        private static readonly int TopId = Shader.PropertyToID("_TopColour");
        private static readonly int BottomId = Shader.PropertyToID("_BottomColour");
        private static readonly int ShoeId = Shader.PropertyToID("_ShoeColour");
        private static readonly int PatternId = Shader.PropertyToID("_Pattern");
        private static readonly int RimColourId = Shader.PropertyToID("_RimColour");
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int HoloColourId = Shader.PropertyToID("_Color");
        private static readonly int HoloIntensityId = Shader.PropertyToID("_Intensity");

        private static int[] poseMuscles;

        [SerializeField] private PersonaBody[] bodies = new PersonaBody[0];
        [Tooltip("The hairstyles' names, in order (Hair_Long → Long).")]
        [SerializeField] private string[] hairNames = new string[0];
        [SerializeField] private TextMesh nameLabel;

        private MaterialPropertyBlock block;
        private Renderer[] glowing;
        private int shownBody = -1;
        private Color seatColour;
        private float appliedGlow = -1f;
        private float appliedFade = -1f;
        private float posedKey = float.NaN;
        private bool[] facingChecked;

        public TextMesh NameLabel => nameLabel;
        public int BodyCount => bodies != null ? bodies.Length : 0;
        public string BodyName(int index) => BodyCount > 0 ? bodies[PersonaSet.Wrap(index, BodyCount)].name : "Body";

        /// <summary>The hairstyle choices: none, then every hairstyle.</summary>
        public int HairChoiceCount => (hairNames != null ? hairNames.Length : 0) + 1;

        public string HairName(int choice)
        {
            int count = HairChoiceCount;
            int wrapped = PersonaSet.Wrap(choice, count);
            return wrapped == 0 ? "None" : hairNames[wrapped - 1];
        }

        /// <summary>Dresses the character as the look says, in the seat colour, with the name over its head.</summary>
        public void Apply(PersonaLook look, PersonaSet set, Color colour, string displayName)
        {
            if (look == null || set == null || BodyCount == 0) return;
            EnsureBlock();
            seatColour = colour;
            int bodyIndex = PersonaSet.Wrap(look.body, BodyCount);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null && bodies[i].root != null && bodies[i].root.gameObject.activeSelf != (i == bodyIndex))
                {
                    bodies[i].root.gameObject.SetActive(i == bodyIndex);
                }
            }

            PersonaBody body = bodies[bodyIndex];
            EnsureFacing(bodyIndex);
            if (shownBody != bodyIndex)
            {
                shownBody = bodyIndex;
                glowing = null;
                posedKey = float.NaN;
            }

            float width = set.BuildWidth(look.build);
            if (body.root != null) body.root.localScale = new Vector3(width, 1f, width);

            int hairChoice = PersonaSet.Wrap(look.hair, HairChoiceCount);
            for (int i = 0; body.hairs != null && i < body.hairs.Length; i++)
            {
                if (body.hairs[i] != null) body.hairs[i].SetActive(i == hairChoice - 1);
            }

            if (body.beard != null) body.beard.SetActive(PersonaSet.Wrap(look.beard, 2) == 1);

            PersonaSet.SkinTone skin = set.Skin(look.skin);
            Texture2D skinPicture = set.SkinPicture(bodyIndex, skin.dark);
            Color hairColour = set.HairColour(look.hairColour).colour;
            SetSurface(body.body, skinPicture, skin.tint, 0f, true, set, look);
            SetSurface(body.eyes, null, set.Eye(look.eyes).colour, 1f, false, set, look);
            SetSurface(body.brows, null, hairColour, 1f, false, set, look);
            for (int i = 0; body.hairs != null && i < body.hairs.Length; i++)
            {
                if (body.hairs[i] != null) SetSurfaces(body.hairs[i], hairColour, set, look);
            }

            if (body.beard != null) SetSurfaces(body.beard, hairColour, set, look);
            if (body.emblem != null)
            {
                Texture2D emblem = set.Emblem(look.emblem);
                body.emblem.enabled = emblem != null;
                if (emblem != null)
                {
                    body.emblem.GetPropertyBlock(block);
                    block.SetTexture(MainTexId, emblem);
                    block.SetColor(HoloColourId, colour);
                    block.SetFloat(HoloIntensityId, 1.2f);
                    body.emblem.SetPropertyBlock(block);
                }
            }

            if (nameLabel != null)
            {
                nameLabel.text = displayName ?? string.Empty;
                nameLabel.color = colour;
                if (nameLabel.gameObject.activeSelf != set.NameOverHead) nameLabel.gameObject.SetActive(set.NameOverHead);
            }

            posedKey = float.NaN;
            Pose(set);
            appliedGlow = -1f;
            appliedFade = -1f;
        }

        /// <summary>Sits the character down (or stands it) as the set says; done again only when the pose changes.</summary>
        public void Pose(PersonaSet set) => Pose(set, set != null && set.Seated);

        /// <summary>Sits (or stands) the character with the set's pose values; done again only when the pose changes.</summary>
        public void Pose(PersonaSet set, bool seated)
        {
            if (shownBody < 0 || set == null) return;
            PersonaBody body = bodies[shownBody];
            float key = (seated ? 1f : 0f) + (set.UpperLegForward * 3.1f) + (set.KneeBend * 5.3f) + (set.ArmDown * 7.7f)
                + (set.ArmForward * 11.3f) + (set.ElbowBend * 13.7f);
            if (key == posedKey) return;
            posedKey = key;
            if (body.avatar != null && body.avatar.isHuman && body.model != null)
            {
                EnsureMuscles();
                HumanPoseHandler handler = new HumanPoseHandler(body.avatar, body.model);
                HumanPose pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                for (int i = 0; i < pose.muscles.Length; i++) pose.muscles[i] = 0f;
                if (seated)
                {
                    SetMuscle(ref pose, 0, set.UpperLegForward);
                    SetMuscle(ref pose, 1, set.UpperLegForward);
                    SetMuscle(ref pose, 2, set.KneeBend);
                    SetMuscle(ref pose, 3, set.KneeBend);
                }

                SetMuscle(ref pose, 4, set.ArmDown);
                SetMuscle(ref pose, 5, set.ArmDown);
                SetMuscle(ref pose, 6, set.ArmForward);
                SetMuscle(ref pose, 7, set.ArmForward);
                SetMuscle(ref pose, 8, set.ElbowBend);
                SetMuscle(ref pose, 9, set.ElbowBend);
                handler.SetHumanPose(ref pose);
                handler.Dispose();
            }

            PlaceName(body);
        }

        /// <summary>Stands the character at its place, facing the board, at its size.</summary>
        public void Place(Vector3 position, Quaternion rotation, float scale)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (Mathf.Abs(transform.localScale.x - scale) > 0.0001f) transform.localScale = new Vector3(scale, scale, scale);
        }

        /// <summary>The seat-colour edge light (brighter on the player's turn) and how far it is faded (1: shown).</summary>
        public void SetGlow(float strength, float fade)
        {
            if (Mathf.Abs(strength - appliedGlow) < 0.005f && Mathf.Abs(fade - appliedFade) < 0.005f) return;
            EnsureBlock();
            EnsureGlowing();
            appliedGlow = strength;
            appliedFade = fade;
            for (int i = 0; i < glowing.Length; i++)
            {
                Renderer renderer = glowing[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor(RimColourId, seatColour);
                block.SetFloat(RimStrengthId, strength);
                block.SetFloat(FadeId, fade);
                renderer.SetPropertyBlock(block);
            }

            if (nameLabel != null)
            {
                Color colour = seatColour;
                colour.a = Mathf.Clamp01(fade);
                nameLabel.color = colour;
            }
        }

        private void SetSurfaces(GameObject part, Color tint, PersonaSet set, PersonaLook look)
        {
            Renderer renderer = part.GetComponentInChildren<Renderer>(true);
            if (renderer != null) SetSurface(renderer, null, tint, 1f, false, set, look);
        }

        private void SetSurface(Renderer renderer, Texture2D picture, Color tint, float colourMode, bool outfit, PersonaSet set, PersonaLook look)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            if (picture != null) block.SetTexture(MainTexId, picture);
            block.SetColor(TintId, tint);
            block.SetFloat(ColourModeId, colourMode);
            block.SetFloat(UseOutfitId, outfit ? 1f : 0f);
            if (outfit)
            {
                block.SetColor(TopId, set.Cloth(look.top).colour);
                block.SetColor(BottomId, set.Cloth(look.bottom).colour);
                block.SetColor(ShoeId, set.Cloth(look.shoes).colour);
                block.SetFloat(PatternId, PersonaSet.Wrap(look.outfit, Math.Max(1, set.OutfitCount)));
            }

            block.SetColor(RimColourId, seatColour);
            renderer.SetPropertyBlock(block);
        }

        // Owner, 2026-10-21 ("wrong direction"): the character must face the board, its front along +Z. Which way the
        // model really looks is read from its feet (the toes point forward), before it is posed; a model looking the
        // other way is turned round, and the chest emblem is put on the real front. Once per body.
        private void EnsureFacing(int bodyIndex)
        {
            if (facingChecked == null || facingChecked.Length != bodies.Length) facingChecked = new bool[bodies.Length];
            if (facingChecked[bodyIndex]) return;
            facingChecked[bodyIndex] = true;
            PersonaBody body = bodies[bodyIndex];
            if (body.model == null || body.root == null) return;
            Transform foot = null;
            Transform toe = null;
            Transform chest = null;
            foreach (Transform bone in body.model.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "foot_l") foot = bone;
                else if (bone.name == "ball_l") toe = bone;
                else if (bone.name == "spine_03") chest = bone;
            }

            if (foot != null && toe != null)
            {
                Vector3 forward = body.root.InverseTransformDirection(toe.position - foot.position);
                if (forward.z < 0f) body.model.Rotate(0f, 180f, 0f, Space.Self);
            }

            if (body.emblem != null && chest != null)
            {
                Vector3 local = body.root.InverseTransformPoint(chest.position) + new Vector3(0f, 0.05f, 0.17f);
                body.emblem.transform.position = body.root.TransformPoint(local);
                body.emblem.transform.rotation = body.root.rotation * Quaternion.Euler(0f, 180f, 0f);
            }
        }

        // The name floats over the head, readable from the board side (the side the character faces).
        private void PlaceName(PersonaBody body)
        {
            if (nameLabel == null || body.head == null) return;
            Vector3 head = transform.InverseTransformPoint(body.head.position);
            nameLabel.transform.localPosition = head + new Vector3(0f, 0.42f, 0f);
            nameLabel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private void EnsureBlock()
        {
            if (block == null) block = new MaterialPropertyBlock();
        }

        // The shown body's lit renderers (not the emblem or the name), gathered once per body.
        private void EnsureGlowing()
        {
            if (glowing != null) return;
            if (shownBody < 0)
            {
                glowing = new Renderer[0];
                return;
            }

            PersonaBody body = bodies[shownBody];
            Renderer[] all = body.root != null ? body.root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            Renderer nameRenderer = nameLabel != null ? nameLabel.GetComponent<Renderer>() : null;
            int count = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != body.emblem && all[i] != nameRenderer) count++;
            glowing = new Renderer[count];
            int next = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != body.emblem && all[i] != nameRenderer) glowing[next++] = all[i];
        }

        private static void EnsureMuscles()
        {
            if (poseMuscles != null) return;
            string[] wanted =
            {
                "Left Upper Leg Front-Back", "Right Upper Leg Front-Back",
                "Left Lower Leg Stretch", "Right Lower Leg Stretch",
                "Left Arm Down-Up", "Right Arm Down-Up",
                "Left Arm Front-Back", "Right Arm Front-Back",
                "Left Forearm Stretch", "Right Forearm Stretch",
            };
            poseMuscles = new int[wanted.Length];
            string[] names = HumanTrait.MuscleName;
            for (int w = 0; w < wanted.Length; w++)
            {
                poseMuscles[w] = Array.IndexOf(names, wanted[w]);
            }
        }

        private static void SetMuscle(ref HumanPose pose, int which, float value)
        {
            int index = poseMuscles[which];
            if (index >= 0 && index < pose.muscles.Length) pose.muscles[index] = Mathf.Clamp(value, -1f, 1f);
        }
    }
}
