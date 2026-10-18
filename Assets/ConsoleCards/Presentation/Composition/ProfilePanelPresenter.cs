using System;
using System.Collections.Generic;
using System.Globalization;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine.InputSystem;
using UnityCamera = UnityEngine.Camera;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The player profile panel: who is playing, new and renamed profiles.
    /// (R1d: moved out of TabletopComposition; the table reaches it as <c>ProfilePanel</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class ProfilePanelPresenter
    {
        private readonly TabletopComposition host;

        internal ProfilePanelPresenter(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.Profiles.cs ----------

        // Once per run of the game: the start-up question is not asked again after switching profile.
        internal static bool profileOfferedThisRun;
        internal ProfilePanelActions profilePanelActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetProfileOffer() => profileOfferedThisRun = false;

        internal void RefreshProfileEntry()
        {
            if (host.runtimeUi == null) return;
            host.runtimeUi.ShowProfileEntry(host.EnsureProfile().Active.displayName, OpenProfilePanel);
        }

        internal void OfferProfileChoiceAtStart()
        {
            if (profileOfferedThisRun) return;
            profileOfferedThisRun = true;
            if (host.EnsureProfile().List().Count > 1)
            {
                ShowProfilePanel("Who is playing? Pick your profile, or close this to keep playing as "
                    + $"{host.profiles.Active.displayName}.", true);
            }
        }

        internal void OpenProfilePanel() => ShowProfilePanel(string.Empty, true);

        internal void ShowProfilePanel(string status, bool clearName)
        {
            if (host.runtimeUi == null) return;
            ProfileService service = host.EnsureProfile();
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
                    Close = CloseProfilePanel,
                };
            }

            string text = string.IsNullOrEmpty(status)
                ? "Everything you make is saved in your profile. Switching profile starts a fresh table."
                : status;
            if (!host.runtimeUi.ShowProfilePanel(new ProfilePanelModel(service.Active.displayName, rows, text),
                    profilePanelActions, clearName))
            {
                host.ShowMessage("The profile panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        internal void CloseProfilePanel()
        {
            host.runtimeUi?.CloseProfilePanel();
            host.GameSelectPanel.ReturnToGameSelect();
        }

        internal void PlayAsProfile(string profileId)
        {
            if (!host.EnsureProfile().Use(profileId, out string error))
            {
                ShowProfilePanel($"Could not switch: {error}.", false);
                return;
            }

            RestartForProfile();
        }

        internal void CreateProfile(string displayName)
        {
            if (host.EnsureProfile().Create(displayName, out string error) == null)
            {
                ShowProfilePanel(error, false);
                return;
            }

            RestartForProfile();
        }

        internal void RenameProfile(string displayName)
        {
            if (!host.EnsureProfile().RenameActive(displayName, out string error))
            {
                ShowProfilePanel(error, false);
                return;
            }

            RefreshProfileEntry();
            ShowProfilePanel($"This profile is now called {host.profiles.Active.displayName}.", true);
        }

        // Every store reads the profile when it starts, so the table starts again for the new profile: the scene is
        // loaded fresh (what is on the table now is not kept; the profile's own saves are).
        internal void RestartForProfile()
        {
            host.runtimeUi?.CloseProfilePanel();
            profileOfferedThisRun = true;
            Scene scene = host.gameObject.scene;
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }

#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            host.ShowMessage("Restart the game to play as the new profile.");
#endif
        }

        internal static string LastPlayedText(string lastUsedUtc)
        {
            if (!DateTime.TryParse(lastUsedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime when))
            {
                return "Not played yet";
            }

            return "Last played " + when.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
