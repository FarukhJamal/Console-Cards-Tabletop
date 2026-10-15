using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ConsoleCards.Presentation.Saving
{
    /// <summary>
    /// The active player profile (doc 28) and where its saves live. The single entry point for saving: every store
    /// asks this service for its folder, and nothing else decides where files go.
    /// <code>
    /// Profiles/
    ///   profiles.json                 the last profile used
    ///   &lt;profile&gt;/
    ///     profile.json                name and dates
    ///     hints/seen.json             hints shown or dismissed
    ///     games/&lt;game id&gt;/
    ///       cards.json                your new cards and changes to the game's cards
    ///       decks.json                your decks
    ///       rules/*.json              your house-rule sets
    ///       uploads/                  your pictures (and uploads.json)
    ///       packs/&lt;pack&gt;/            packs you added
    ///       settings.json             the active pack
    /// </code>
    /// </summary>
    public sealed class ProfileService
    {
        public const string DefaultProfileName = "Player 1";
        public const string GamesFolderName = "games";

        public ProfileService(ProfileStore store)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public static ProfileService CreateDefault() => new ProfileService(ProfileStore.CreateDefault());

        public ProfileStore Store { get; }

        /// <summary>The profile in use; null until <see cref="EnsureActive"/> or <see cref="Use"/>.</summary>
        public ProfileInfo Active { get; private set; }

        public bool HasActive => Active != null;

        /// <summary>Raised after another profile becomes active. Stores built for the old profile must be rebuilt.</summary>
        public event Action<ProfileInfo> Changed;

        public string ActiveFolder => Store.ProfileFolder(RequireActive().id);

        /// <summary>Per-game saves of the active profile: games/&lt;game id&gt;/ (see the class summary).</summary>
        public string GamesFolder => Path.Combine(ActiveFolder, GamesFolderName);

        public string HintsSeenFile => Path.Combine(ActiveFolder, "hints", "seen.json");

        public List<ProfileInfo> List() => Store.List();

        /// <summary>
        /// Makes sure a profile is active: the one used last, else the most recent, else a new "Player 1" (which also
        /// brings in saves from before profiles existed, once).
        /// </summary>
        public ProfileInfo EnsureActive()
        {
            if (Active != null) return Active;
            ProfileInfo profile = Store.Find(Store.LoadIndex().lastProfileId);
            if (profile == null)
            {
                List<ProfileInfo> profiles = Store.List();
                profile = profiles.Count > 0 ? profiles[0] : null;
            }

            if (profile == null)
            {
                profile = Store.Create(DefaultProfileName, out string error);
                if (profile == null)
                {
                    // Play never stops over saving: an unsaved profile works for this session.
                    Debug.LogWarning($"[Profiles] Could not create a profile in {Store.RootFolder}: {error}");
                    profile = new ProfileInfo { id = "player-1", displayName = DefaultProfileName };
                }
                else
                {
                    BringInOldSaves(profile);
                }
            }

            SetActive(profile, false);
            return Active;
        }

        /// <summary>Switches to another saved profile. False with a reason when it is not found.</summary>
        public bool Use(string profileId, out string error)
        {
            ProfileInfo profile = Store.Find(profileId);
            if (profile == null)
            {
                error = $"no profile '{profileId}'";
                return false;
            }

            error = string.Empty;
            bool changed = Active == null || !string.Equals(Active.id, profile.id, StringComparison.OrdinalIgnoreCase);
            SetActive(profile, changed);
            return true;
        }

        /// <summary>Creates a profile and switches to it. Null with a reason when the name is not usable.</summary>
        public ProfileInfo Create(string displayName, out string error)
        {
            ProfileInfo profile = Store.Create(displayName, out error);
            if (profile != null) SetActive(profile, true);
            return profile;
        }

        private void SetActive(ProfileInfo profile, bool raise)
        {
            profile.lastUsedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            if (!Store.Save(profile, out string error))
            {
                Debug.LogWarning($"[Profiles] Could not save {profile.displayName}: {error}");
            }

            Store.SaveIndex(new ProfileIndex { lastProfileId = profile.id });
            Active = profile;
            Debug.Log($"[Profiles] Playing as '{profile.displayName}' ({Store.ProfileFolder(profile.id)}).");
            if (raise) Changed?.Invoke(profile);
        }

        // Before profiles, pictures, packs, house rules and hints were saved in the game's data folder. The first
        // profile gets a copy of them; the old files are left where they are.
        private void BringInOldSaves(ProfileInfo profile)
        {
            try
            {
                string oldRoot = UnityEngine.Application.persistentDataPath;
                string folder = Store.ProfileFolder(profile.id);
                SaveFiles.CopyFolderKeepingExisting(Path.Combine(oldRoot, "Content"), Path.Combine(folder, GamesFolderName));
                SaveFiles.CopyFolderKeepingExisting(Path.Combine(oldRoot, "Hints"), Path.Combine(folder, "hints"));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Profiles] Older saves could not be copied into '{profile.displayName}': {exception.Message}");
            }
        }

        private ProfileInfo RequireActive() =>
            Active ?? throw new InvalidOperationException("No profile is active; call EnsureActive first.");
    }
}
