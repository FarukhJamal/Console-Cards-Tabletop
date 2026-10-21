using System;

namespace ConsoleCards.Presentation.Saving
{
    /// <summary>
    /// A player profile (doc 28): who is playing. Everything a player makes or chooses (cards, decks, rules, pictures,
    /// packs, hints seen) is saved in the profile's folder. Saved as profile.json in that folder.
    /// </summary>
    [Serializable]
    public sealed class ProfileInfo
    {
        public const string FileName = "profile.json";
        public const int CurrentFormatVersion = 1;
        public const int MaximumNameLength = 32;

        public int formatVersion = CurrentFormatVersion;

        /// <summary>Made when the profile is created; also its folder name. Never changes.</summary>
        public string id = string.Empty;

        /// <summary>The name players see, for example "Player 1". Can be changed.</summary>
        public string displayName = string.Empty;

        /// <summary>UTC times as round-trip text ("o").</summary>
        public string createdUtc = string.Empty;
        public string lastUsedUtc = string.Empty;

        /// <summary>AVATAR-1: the player's avatar picture at the table (an index into the Persona set's avatars).</summary>
        public int avatar;

        /// <summary>PERSONA-2: the 3D character (kept; the characters are off since AVATAR-1).</summary>
        public PersonaLook persona = new PersonaLook();

        /// <summary>PERSONA-1: experience from play; the level is worked out from it (filled by a later stage).</summary>
        public int experience;
    }

    /// <summary>
    /// PERSONA-2 (owner, 2026-10-21: "a proper lite customization"): a player's character, one choice per slot, each
    /// an index into the Persona set's choices for that slot (any number wraps round them, so an old or edited file
    /// still shows a character). Saved in the profile.
    /// </summary>
    [Serializable]
    public sealed class PersonaLook
    {
        public const int SlotCount = 12;
        public const int Body = 0;
        public const int Build = 1;
        public const int Skin = 2;
        public const int Eyes = 3;
        public const int Hair = 4;
        public const int HairColour = 5;
        public const int Beard = 6;
        public const int Outfit = 7;
        public const int Top = 8;
        public const int Bottom = 9;
        public const int Shoes = 10;
        public const int Emblem = 11;

        public int body;
        public int build = 1;
        public int skin = 1;
        public int eyes;
        public int hair = 1;
        public int hairColour = 1;
        public int beard;
        public int outfit;
        public int top = 2;
        public int bottom = 7;
        public int shoes = 8;
        public int emblem;

        public int Get(int slot)
        {
            switch (slot)
            {
                case Body: return body;
                case Build: return build;
                case Skin: return skin;
                case Eyes: return eyes;
                case Hair: return hair;
                case HairColour: return hairColour;
                case Beard: return beard;
                case Outfit: return outfit;
                case Top: return top;
                case Bottom: return bottom;
                case Shoes: return shoes;
                default: return emblem;
            }
        }

        public void Set(int slot, int value)
        {
            switch (slot)
            {
                case Body: body = value; break;
                case Build: build = value; break;
                case Skin: skin = value; break;
                case Eyes: eyes = value; break;
                case Hair: hair = value; break;
                case HairColour: hairColour = value; break;
                case Beard: beard = value; break;
                case Outfit: outfit = value; break;
                case Top: top = value; break;
                case Bottom: bottom = value; break;
                case Shoes: shoes = value; break;
                default: emblem = value; break;
            }
        }

        public bool SameAs(PersonaLook other)
        {
            if (other == null) return false;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (Get(slot) != other.Get(slot)) return false;
            }

            return true;
        }

        public void CopyTo(PersonaLook other)
        {
            for (int slot = 0; slot < SlotCount; slot++) other.Set(slot, Get(slot));
        }

        public PersonaLook Copy()
        {
            PersonaLook copy = new PersonaLook();
            CopyTo(copy);
            return copy;
        }

        /// <summary>A different character for each seat that has no profile of its own (other seats on this screen).</summary>
        public static PersonaLook ForSeat(int seatIndex)
        {
            int n = Math.Max(0, seatIndex);
            return new PersonaLook
            {
                body = n % 2,
                build = (n + 1) % 3,
                skin = (n * 2) + 1,
                eyes = n,
                hair = 1 + (n * 2),
                hairColour = n * 3,
                beard = n == 2 ? 1 : 0,
                outfit = n,
                top = (n * 3) + 1,
                bottom = 7 - n,
                shoes = n + 8,
                emblem = n,
            };
        }
    }

    /// <summary>The profiles folder's own file (profiles.json): which profile was used last.</summary>
    [Serializable]
    public sealed class ProfileIndex
    {
        public const string FileName = "profiles.json";

        public int formatVersion = 1;
        public string lastProfileId = string.Empty;
    }
}
