using System;
using System.Collections.Generic;
using System.Globalization;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>
    /// The player profile panel (doc 28, P2): the Table menu's "Profile: name…" row opens it; at game start it opens
    /// by itself when there is more than one profile. Playing as another profile, or making a new one, starts the
    /// table again from the start, so every store (pictures, packs, cards, decks, rules, hints) reads that profile.
    /// </summary>
    public sealed partial class TabletopPrototypeComposition
    {
        // Once per run of the game: the start-up question is not asked again after switching profile.
        private static bool profileOfferedThisRun;
        private ProfilePanelActions profilePanelActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetProfileOffer() => profileOfferedThisRun = false;

        private void RefreshProfileEntry()
        {
            if (runtimeUi == null) return;
            runtimeUi.ShowProfileEntry(EnsureProfile().Active.displayName, OpenProfilePanel);
        }

        private void OfferProfileChoiceAtStart()
        {
            if (profileOfferedThisRun) return;
            profileOfferedThisRun = true;
            if (EnsureProfile().List().Count > 1)
            {
                ShowProfilePanel("Who is playing? Pick your profile, or close this to keep playing as "
                    + $"{profiles.Active.displayName}.", true);
            }
        }

        private void OpenProfilePanel() => ShowProfilePanel(string.Empty, true);

        private void ShowProfilePanel(string status, bool clearName)
        {
            if (runtimeUi == null) return;
            ProfileService service = EnsureProfile();
            List<ProfileInfo> saved = service.List();
            List<ProfileRowModel> rows = new List<ProfileRowModel>(saved.Count);
            for (int i = 0; i < saved.Count; i++)
            {
                bool isActive = string.Equals(saved[i].id, service.Active.id, StringComparison.OrdinalIgnoreCase);
                rows.Add(new ProfileRowModel(
                    saved[i].id,
                    saved[i].displayName,
                    isActive ? "Playing now" : LastPlayedText(saved[i].lastUsedUtc),
                    isActive));
            }

            if (profilePanelActions == null)
            {
                profilePanelActions = new ProfilePanelActions
                {
                    PlayAs = PlayAsProfile,
                    Create = CreateProfile,
                    Rename = RenameProfile,
                    OpenFolder = OpenProfilesFolder,
                    Close = () => runtimeUi?.CloseProfilePanel(),
                };
            }

            string text = string.IsNullOrEmpty(status)
                ? "Everything you make is saved in your profile. Switching profile starts a fresh table."
                : status;
            if (!runtimeUi.ShowProfilePanel(new ProfilePanelModel(service.Active.displayName, rows, text),
                    profilePanelActions, clearName))
            {
                ShowMessage("The profile panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        private void PlayAsProfile(string profileId)
        {
            if (!EnsureProfile().Use(profileId, out string error))
            {
                ShowProfilePanel($"Could not switch: {error}.", false);
                return;
            }

            RestartForProfile();
        }

        private void CreateProfile(string displayName)
        {
            if (EnsureProfile().Create(displayName, out string error) == null)
            {
                ShowProfilePanel(error, false);
                return;
            }

            RestartForProfile();
        }

        private void RenameProfile(string displayName)
        {
            if (!EnsureProfile().RenameActive(displayName, out string error))
            {
                ShowProfilePanel(error, false);
                return;
            }

            RefreshProfileEntry();
            ShowProfilePanel($"This profile is now called {profiles.Active.displayName}.", true);
        }

        private void OpenProfilesFolder()
        {
            string folder = EnsureProfile().Store.RootFolder;
            try
            {
                System.IO.Directory.CreateDirectory(folder);
                UnityEngine.Application.OpenURL(new Uri(folder).AbsoluteUri);
                ShowProfilePanel($"Profiles folder: {folder}", false);
            }
            catch (Exception exception)
            {
                ShowProfilePanel($"Could not open {folder}: {exception.Message}", false);
            }
        }

        // Every store reads the profile when it starts, so the table starts again for the new profile: the scene is
        // loaded fresh (what is on the table now is not kept; the profile's own saves are).
        private void RestartForProfile()
        {
            runtimeUi?.CloseProfilePanel();
            profileOfferedThisRun = true;
            Scene scene = gameObject.scene;
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }

#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            ShowMessage("Restart the game to play as the new profile.");
#endif
        }

        private static string LastPlayedText(string lastUsedUtc)
        {
            if (!DateTime.TryParse(lastUsedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime when))
            {
                return "Not played yet";
            }

            return "Last played " + when.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
