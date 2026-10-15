using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ConsoleCards.Presentation.Saving
{
    /// <summary>
    /// The profiles folder (doc 28): one folder per profile, next to the game. In Unity that is
    /// &lt;project&gt;/Profiles (outside Assets, so Unity does not import it); in a build it is a Profiles folder beside
    /// the game's .exe. Files only; which profile is active is <see cref="ProfileService"/>'s job.
    /// </summary>
    public sealed class ProfileStore
    {
        public const string FolderName = "Profiles";

        public ProfileStore(string rootFolder)
        {
            RootFolder = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));
        }

        public string RootFolder { get; }

        /// <summary>
        /// The store next to the game. If that folder cannot be written (for example a build installed in a protected
        /// folder), the game's own data folder is used instead, with a warning.
        /// </summary>
        public static ProfileStore CreateDefault()
        {
            string beside = BesideTheGame();
            if (SaveFiles.CanWriteIn(beside)) return new ProfileStore(beside);
            string fallback = Path.Combine(UnityEngine.Application.persistentDataPath, FolderName);
            Debug.LogWarning($"[Profiles] {beside} cannot be written; profiles are saved in {fallback} instead.");
            return new ProfileStore(fallback);
        }

        /// <summary>&lt;project&gt;/Profiles in Unity; &lt;game folder&gt;/Profiles in a build.</summary>
        public static string BesideTheGame() =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", FolderName));

        public string ProfileFolder(string profileId) => Path.Combine(RootFolder, SaveFiles.SafeName(profileId, "profile"));

        /// <summary>Every readable profile, most recently used first. Folders without a readable profile.json are skipped.</summary>
        public List<ProfileInfo> List()
        {
            List<ProfileInfo> profiles = new List<ProfileInfo>();
            if (!Directory.Exists(RootFolder)) return profiles;
            foreach (string folder in Directory.GetDirectories(RootFolder))
            {
                ProfileInfo profile = SaveFiles.ReadJson<ProfileInfo>(Path.Combine(folder, ProfileInfo.FileName), "Profiles");
                if (profile == null) continue;
                if (profile.formatVersion > ProfileInfo.CurrentFormatVersion)
                {
                    Debug.LogWarning($"[Profiles] {folder} was made by a newer version; skipped.");
                    continue;
                }

                // The folder is the profile's identity, whatever the file says.
                profile.id = Path.GetFileName(folder);
                if (string.IsNullOrWhiteSpace(profile.displayName)) profile.displayName = profile.id;
                profiles.Add(profile);
            }

            profiles.Sort((a, b) => string.CompareOrdinal(b.lastUsedUtc ?? string.Empty, a.lastUsedUtc ?? string.Empty));
            return profiles;
        }

        public ProfileInfo Find(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return null;
            List<ProfileInfo> profiles = List();
            for (int i = 0; i < profiles.Count; i++)
            {
                if (string.Equals(profiles[i].id, profileId, StringComparison.OrdinalIgnoreCase)) return profiles[i];
            }

            return null;
        }

        /// <summary>
        /// A usable profile name: trimmed, 1 to <see cref="ProfileInfo.MaximumNameLength"/> letters, and not the name of
        /// another profile (<paramref name="exceptProfileId"/> is the profile being renamed).
        /// </summary>
        public bool TryCleanName(string displayName, string exceptProfileId, out string name, out string error)
        {
            name = (displayName ?? string.Empty).Trim();
            error = string.Empty;
            if (name.Length == 0)
            {
                error = "Type a name first.";
                return false;
            }

            if (name.Length > ProfileInfo.MaximumNameLength)
            {
                error = $"A profile name has at most {ProfileInfo.MaximumNameLength} letters.";
                return false;
            }

            List<ProfileInfo> profiles = List();
            for (int i = 0; i < profiles.Count; i++)
            {
                if (string.Equals(profiles[i].displayName, name, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(profiles[i].id, exceptProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"There is already a profile called {profiles[i].displayName}.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>A new profile with its folder. Null with a reason when the name is not usable or saving fails.</summary>
        public ProfileInfo Create(string displayName, out string error)
        {
            if (!TryCleanName(displayName, null, out string name, out error)) return null;
            string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            ProfileInfo profile = new ProfileInfo
            {
                id = SaveFiles.SafeName(name, "profile") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                displayName = name,
                createdUtc = now,
                lastUsedUtc = now,
            };
            return Save(profile, out error) ? profile : null;
        }

        public bool Save(ProfileInfo profile, out string error) =>
            SaveFiles.TryWriteJson(Path.Combine(ProfileFolder(profile.id), ProfileInfo.FileName), profile, out error);

        public ProfileIndex LoadIndex() =>
            SaveFiles.ReadJson<ProfileIndex>(Path.Combine(RootFolder, ProfileIndex.FileName), "Profiles") ?? new ProfileIndex();

        public void SaveIndex(ProfileIndex index) =>
            SaveFiles.WriteJson(Path.Combine(RootFolder, ProfileIndex.FileName), index, "Profiles");
    }
}
