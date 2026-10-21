using System;
using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>
    /// PERSONA-2 (owner, 2026-10-21): the player characters and what can be chosen for them. The character prefab
    /// (both bodies with every hairstyle, from the Universal Base Characters pack), the skin pictures, the colour
    /// lists for skin, eyes, hair and clothes, the outfit patterns, the chest emblems, where a character sits at the
    /// table and how it glows. Lives at Resources/PersonaSet so the game finds it without a scene reference; made by
    /// Console Cards > Feedback > Create Missing Persona Prefabs (Final World). Edit it freely: names and colours
    /// here are what players see in the Persona panel.
    /// </summary>
    [CreateAssetMenu(fileName = "PersonaSet", menuName = "Console Cards/Feedback/Persona Set")]
    public sealed class PersonaSet : ScriptableObject
    {
        public const string ResourcePath = "PersonaSet";

        /// <summary>AVATAR-2: where the name tag sits on the avatar.</summary>
        public enum NameTagPlace
        {
            Off = 0,
            OverHead = 1,
            InFront = 2,
        }

        [Serializable]
        public struct NamedColour
        {
            public string name;
            public Color colour;

            public NamedColour(string name, Color colour)
            {
                this.name = name;
                this.colour = colour;
            }
        }

        [Serializable]
        public struct SkinTone
        {
            public string name;
            [Tooltip("Use the body's darker skin picture.")]
            public bool dark;
            [Tooltip("Multiplied with the skin picture.")]
            public Color tint;

            public SkinTone(string name, bool dark, Color tint)
            {
                this.name = name;
                this.dark = dark;
                this.tint = tint;
            }
        }

        [Header("Avatars (AVATAR-1, owner 2026-10-21: \"keep it simple and use avatars\")")]
        [Tooltip("The profile pictures players choose from (the file name after Avatar_ is the name shown).")]
        [SerializeField] private Texture2D[] avatars = new Texture2D[0];
        [SerializeField] private PersonaAvatarView avatarStand;
        [Tooltip("Where the avatar stands, from the table's edge behind the Console (table units; negative: on the table).")]
        [SerializeField] private float avatarFromEdge = 1.2f;
        [Tooltip("How far the avatar leans back from upright (degrees).")]
        [SerializeField] private float avatarLean = 12f;
        [Tooltip("How high the avatar's bottom edge is above the table top (table units).")]
        [SerializeField] private float avatarLift = 0.3f;
        [SerializeField] private float avatarGlowIdle = 0.75f;
        [SerializeField] private float avatarGlowTurn = 1.6f;
        [Tooltip("AVATAR-2: the player's name tag on their avatar: over its head, or in front (under the picture).")]
        [SerializeField] private NameTagPlace avatarNameTag = NameTagPlace.OverHead;
        [Tooltip("The name tag's letter size (table units).")]
        [SerializeField] private float nameTagLetters = 0.11f;
        [Tooltip("Room between the picture's frame and the name tag (table units).")]
        [SerializeField] private float nameTagGap = 0.15f;
        [Tooltip("Write each player's name along the table's rim in front of their seat, in the seat colour.")]
        [SerializeField] private bool nameOnRim = true;
        [Tooltip("How far in from the table's edge the rim name lies (table units).")]
        [SerializeField] private float rimNameInset = 0.35f;
        [Tooltip("The rim name's letter size.")]
        [SerializeField] private float rimNameSize = 0.09f;

        [Header("Switched off (kept for later)")]
        [Tooltip("PERSONA-2: the 3D characters behind each seat.")]
        [SerializeField] private bool showCharacters;
        [Tooltip("PERSONA-2: the Persona panel (the 3D character customizer) from the Profile panel.")]
        [SerializeField] private bool characterCustomizer;
        [Tooltip("The leaning name panels at the table edge.")]
        [SerializeField] private bool showSeatTags;

        [Header("Character (Final World prefab)")]
        [Tooltip("Both bodies, each with every hairstyle and the beard; one body and one of each is shown.")]
        [SerializeField] private PersonaView character;
        [Tooltip("Each body's skin pictures, in the prefab's body order: light, then dark.")]
        [SerializeField] private Texture2D[] lightSkin = new Texture2D[0];
        [SerializeField] private Texture2D[] darkSkin = new Texture2D[0];

        [Header("Choices")]
        [SerializeField] private string[] builds = { "Slim", "Regular", "Broad" };
        [Tooltip("How wide each build is (1: as made).")]
        [SerializeField] private float[] buildWidths = { 0.9f, 1f, 1.12f };
        [SerializeField] private SkinTone[] skinTones =
        {
            new SkinTone("Porcelain", false, new Color(1f, 0.96f, 0.95f)),
            new SkinTone("Fair", false, Color.white),
            new SkinTone("Warm", false, new Color(1f, 0.88f, 0.78f)),
            new SkinTone("Olive", false, new Color(0.9f, 0.82f, 0.68f)),
            new SkinTone("Brown", true, Color.white),
            new SkinTone("Deep", true, new Color(0.72f, 0.66f, 0.62f)),
        };
        [SerializeField] private NamedColour[] eyeColours =
        {
            new NamedColour("Brown", new Color(0.45f, 0.28f, 0.15f)),
            new NamedColour("Blue", new Color(0.25f, 0.5f, 0.95f)),
            new NamedColour("Green", new Color(0.25f, 0.65f, 0.35f)),
            new NamedColour("Grey", new Color(0.6f, 0.65f, 0.7f)),
            new NamedColour("Amber", new Color(0.85f, 0.6f, 0.2f)),
            new NamedColour("Violet", new Color(0.6f, 0.35f, 0.9f)),
        };
        [SerializeField] private NamedColour[] hairColours =
        {
            new NamedColour("Black", new Color(0.12f, 0.11f, 0.11f)),
            new NamedColour("Dark Brown", new Color(0.3f, 0.19f, 0.12f)),
            new NamedColour("Brown", new Color(0.52f, 0.33f, 0.19f)),
            new NamedColour("Auburn", new Color(0.6f, 0.24f, 0.12f)),
            new NamedColour("Blonde", new Color(0.92f, 0.78f, 0.5f)),
            new NamedColour("Silver", new Color(0.82f, 0.84f, 0.88f)),
            new NamedColour("Neon Blue", new Color(0.2f, 0.6f, 1f)),
            new NamedColour("Hot Pink", new Color(1f, 0.3f, 0.65f)),
        };
        [SerializeField] private string[] outfits = { "Plain", "Two-tone", "Stripes", "Circuit" };
        [SerializeField] private NamedColour[] clothColours =
        {
            new NamedColour("Charcoal", new Color(0.16f, 0.17f, 0.2f)),
            new NamedColour("White", new Color(0.92f, 0.92f, 0.9f)),
            new NamedColour("Console Orange", new Color(0.93f, 0.4f, 0.13f)),
            new NamedColour("Red", new Color(0.82f, 0.18f, 0.18f)),
            new NamedColour("Yellow", new Color(0.95f, 0.78f, 0.2f)),
            new NamedColour("Green", new Color(0.25f, 0.62f, 0.32f)),
            new NamedColour("Teal", new Color(0.12f, 0.6f, 0.62f)),
            new NamedColour("Navy", new Color(0.13f, 0.2f, 0.42f)),
            new NamedColour("Denim", new Color(0.28f, 0.4f, 0.62f)),
            new NamedColour("Purple", new Color(0.45f, 0.25f, 0.62f)),
            new NamedColour("Pink", new Color(0.95f, 0.55f, 0.72f)),
            new NamedColour("Khaki", new Color(0.66f, 0.6f, 0.44f)),
        };
        [Tooltip("The chest emblem pictures (the Console's symbols).")]
        [SerializeField] private Texture2D[] emblems = new Texture2D[0];

        [Header("At the table (sizes in metres of the character; one metre is Character Size table units)")]
        [Tooltip("How many table units one metre of the character is (a card is about 2 units; a real person would be about 30).")]
        [SerializeField] private float characterSize = 11f;
        [Tooltip("How far past the table's edge the character sits, behind its Console (metres).")]
        [SerializeField] private float pastEdgeMetres = 0.45f;
        [Tooltip("Sideways from the Console's middle (metres; positive: the player's left).")]
        [SerializeField] private float sidewaysMetres = 0f;
        [Tooltip("How far below the table top the character's feet are (metres): about 1.2 sitting, 0.75 standing.")]
        [SerializeField] private float feetBelowTableMetres = 1.2f;
        [Tooltip("Sit the characters down (off: they stand).")]
        [SerializeField] private bool seated = true;
        [Tooltip("The sitting pose as humanoid muscle values (-1 to 1). Change them in Play mode to tune the pose.")]
        [SerializeField] private float upperLegForward = 0.6f;
        [SerializeField] private float kneeBend = -0.9f;
        [SerializeField] private float armDown = -0.35f;
        [SerializeField] private float armForward = 0.45f;
        [SerializeField] private float elbowBend = 0.1f;

        [Tooltip("Show the name over each character's head too (the seat tags show it already).")]
        [SerializeField] private bool nameOverHead;

        [Header("Fading near the camera (world units)")]
        [Tooltip("A character this close to the camera is hidden (your own seat, seen from behind it).")]
        [SerializeField] private float hideCloserThan = 7f;
        [Tooltip("From this far away it shows fully.")]
        [SerializeField] private float showFullFrom = 15f;

        [Header("Seat tags (a name panel at the table edge behind each seat)")]
        [SerializeField] private PersonaTagView seatTag;
        [Tooltip("Where the tag stands, from the table's edge (table units; negative: on the table).")]
        [SerializeField] private float tagFromEdge = 0.2f;
        [Tooltip("How far the tag leans back from upright, so it reads from above (degrees).")]
        [SerializeField] private float tagLean = 55f;
        [SerializeField] private float tagGlowIdle = 0.7f;
        [SerializeField] private float tagGlowTurn = 1.5f;

        [Header("Seat glow (an edge light in the seat colour)")]
        [SerializeField] private float edgeGlowIdle = 0.6f;
        [SerializeField] private float edgeGlowTurn = 1.6f;
        [Tooltip("How fast the glow breathes on its player's turn.")]
        [SerializeField] private float glowPulseSpeed = 1.6f;
        [SerializeField] private float glowPulseAmount = 0.25f;

        public PersonaView Character => character;
        public int AvatarCount => avatars != null ? avatars.Length : 0;
        public Texture2D Avatar(int index) => AvatarCount > 0 ? avatars[Wrap(index, avatars.Length)] : null;
        public string AvatarName(int index)
        {
            Texture2D picture = Avatar(index);
            if (picture == null) return "None";
            string name = picture.name;
            return name.StartsWith("Avatar_", StringComparison.Ordinal) ? name.Substring(7) : name;
        }

        public PersonaAvatarView AvatarStand => avatarStand;
        public float AvatarFromEdge => avatarFromEdge;
        public float AvatarLean => avatarLean;
        public float AvatarLift => avatarLift;
        public float AvatarGlowIdle => avatarGlowIdle;
        public float AvatarGlowTurn => avatarGlowTurn;
        public bool NameOnRim => nameOnRim;
        public NameTagPlace AvatarNameTag => avatarNameTag;
        public float NameTagLetters => nameTagLetters;
        public float NameTagGap => nameTagGap;
        public float RimNameInset => rimNameInset;
        public float RimNameSize => rimNameSize;
        public bool ShowCharacters => showCharacters;
        public bool CharacterCustomizer => characterCustomizer;
        public bool ShowSeatTags => showSeatTags;
        public float CharacterScale => Mathf.Max(0.01f, characterSize);
        public bool NameOverHead => nameOverHead;
        public PersonaTagView SeatTag => seatTag;
        public float TagFromEdge => tagFromEdge;
        public float TagLean => tagLean;
        public float TagGlowIdle => tagGlowIdle;
        public float TagGlowTurn => tagGlowTurn;
        public float PastEdgeMetres => pastEdgeMetres;
        public float SidewaysMetres => sidewaysMetres;
        public float FeetBelowTableMetres => feetBelowTableMetres;
        public bool Seated => seated;
        public float UpperLegForward => upperLegForward;
        public float KneeBend => kneeBend;
        public float ArmDown => armDown;
        public float ArmForward => armForward;
        public float ElbowBend => elbowBend;
        public float FadeNear => hideCloserThan;
        public float FadeFar => Mathf.Max(hideCloserThan + 0.01f, showFullFrom);
        public float IdleGlow => edgeGlowIdle;
        public float TurnGlow => edgeGlowTurn;
        public float TurnPulseSpeed => glowPulseSpeed;
        public float TurnPulseAmount => glowPulseAmount;

        public int BuildCount => Count(builds);
        public int SkinCount => skinTones != null ? skinTones.Length : 0;
        public int EyeCount => eyeColours != null ? eyeColours.Length : 0;
        public int HairColourCount => hairColours != null ? hairColours.Length : 0;
        public int OutfitCount => Count(outfits);
        public int ClothColourCount => clothColours != null ? clothColours.Length : 0;
        public int EmblemCount => emblems != null ? emblems.Length : 0;

        public string BuildName(int index) => Pick(builds, index) ?? "Regular";
        public float BuildWidth(int index) => buildWidths != null && buildWidths.Length > 0 ? buildWidths[Wrap(index, buildWidths.Length)] : 1f;
        public SkinTone Skin(int index) => SkinCount > 0 ? skinTones[Wrap(index, skinTones.Length)] : new SkinTone("Skin", false, Color.white);
        public NamedColour Eye(int index) => EyeCount > 0 ? eyeColours[Wrap(index, eyeColours.Length)] : new NamedColour("Eyes", Color.white);
        public NamedColour HairColour(int index) => HairColourCount > 0 ? hairColours[Wrap(index, hairColours.Length)] : new NamedColour("Hair", Color.white);
        public string OutfitName(int index) => Pick(outfits, index) ?? "Plain";
        public NamedColour Cloth(int index) => ClothColourCount > 0 ? clothColours[Wrap(index, clothColours.Length)] : new NamedColour("Cloth", Color.grey);

        public Texture2D Emblem(int index) => EmblemCount > 0 ? emblems[Wrap(index, emblems.Length)] : null;

        /// <summary>A body's skin picture, light or dark (null: the picture the body came with).</summary>
        public Texture2D SkinPicture(int body, bool dark)
        {
            Texture2D[] pictures = dark ? darkSkin : lightSkin;
            return pictures != null && body >= 0 && body < pictures.Length ? pictures[body] : null;
        }

        public static int Wrap(int index, int count) => count <= 0 ? 0 : ((index % count) + count) % count;

        private static int Count(string[] names) => names != null ? names.Length : 0;

        private static string Pick(string[] names, int index) => names != null && names.Length > 0 ? names[Wrap(index, names.Length)] : null;
    }
}
