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
