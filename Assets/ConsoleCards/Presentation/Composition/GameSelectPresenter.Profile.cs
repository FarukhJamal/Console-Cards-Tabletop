using System;
using System.Collections.Generic;
using System.Globalization;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// PROFILE-2 (owner, 2026-10-31: "I like the design ... implement that design"): board 19 "Player profile v2" and
    /// board 19b "Profile chip + switch player". The players on this computer with their avatars and colours, the
    /// player playing now (name, colour, avatar, stats), a new player, and removing a player (asked again in place).
    /// Every change is saved at once.
    /// </summary>
    internal sealed partial class GameSelectPresenter
    {
        private bool profileDeleteArmed;
        private bool profileSaved = true;

        private void FillProfileBoard(GameSelectModel model)
        {
            ProfileService service = host.EnsureProfile();
            PersonaSet personas = host.PersonaSetForProfile;
            List<ProfileInfo> saved = service.List();
            List<FrontEndProfile> cards = new List<FrontEndProfile>(saved.Count);
            for (int i = 0; i < saved.Count; i++)
            {
                ProfileInfo profile = saved[i];
                bool active = string.Equals(profile.id, service.Active.id, StringComparison.OrdinalIgnoreCase);
                Texture avatar = personas != null && personas.AvatarCount > 0 ? personas.Avatar(profile.avatar) : null;
                FrontEndProfile card = new FrontEndProfile(profile.id, profile.displayName,
                    active ? "Playing now" : PlayedLine(profile), avatar, ProfileColourOf(profile), active);
                if (active) cards.Insert(0, card);
                else cards.Add(card);
            }

            model.ProfileCards = cards;
            Color[] colours = new Color[SeatColourCount];
            for (int i = 0; i < colours.Length; i++) colours[i] = ColourByIndex(i);
            model.ProfileColours = colours;
            model.ProfileColour = service.Active.colour >= 0 && service.Active.colour < colours.Length ? service.Active.colour : 0;
            int count = personas != null ? personas.AvatarCount : 0;
            Texture[] choices = new Texture[count];
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                choices[i] = personas.Avatar(i);
                names[i] = personas.AvatarName(i);
            }

            model.AvatarChoices = choices;
            model.AvatarChoiceNames = names;
            model.StatGames = service.Active.gamesPlayed.ToString(CultureInfo.InvariantCulture);
            model.StatWins = service.Active.gamesWon.ToString(CultureInfo.InvariantCulture);
            model.StatLastGame = string.IsNullOrEmpty(service.Active.lastGame) ? "-" : service.Active.lastGame.ToUpperInvariant();
            model.StatYourGames = (model.MyGames != null ? model.MyGames.Count : 0).ToString(CultureInfo.InvariantCulture);
            model.CanDeleteProfile = saved.Count > 1;
            model.ProfileDeleteArmed = profileDeleteArmed && model.CanDeleteProfile;
            model.ProfileSaved = profileSaved;
            gameSelectActions.RenameProfile = RenameFromFrontEnd;
            gameSelectActions.PickProfileColour = PickProfileColour;
            gameSelectActions.PickAvatar = PickAvatarFromFrontEnd;
            gameSelectActions.NewProfile = NewProfileFromFrontEnd;
            gameSelectActions.DeleteProfile = DeleteProfileFromFrontEnd;
        }

        // A profile's colour, or the first seat colour when none was picked.
        private static Color ProfileColourOf(ProfileInfo profile) =>
            ColourByIndex(profile.colour >= 0 && profile.colour < SeatColourCount ? profile.colour : 0);

        // "Trap Floor · 3 days ago" / "Not played yet".
        private static string PlayedLine(ProfileInfo profile)
        {
            if (profile.gamesPlayed <= 0 && string.IsNullOrEmpty(profile.lastGame)) return "Not played yet";
            string when = WhenText(profile.lastUsedUtc);
            return string.IsNullOrEmpty(profile.lastGame) ? when : $"{profile.lastGame} · {when}";
        }

        private static string WhenText(string utc)
        {
            if (!DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime when)) return "a while ago";
            TimeSpan ago = DateTime.UtcNow - when.ToUniversalTime();
            if (ago.TotalMinutes < 2) return "just now";
            if (ago.TotalHours < 1) return $"{(int)ago.TotalMinutes} min ago";
            if (ago.TotalDays < 1) return $"{(int)ago.TotalHours} h ago";
            if (ago.TotalDays < 2) return "yesterday";
            return $"{(int)ago.TotalDays} days ago";
        }

        private void RenameFromFrontEnd(string name)
        {
            profileDeleteArmed = false;
            ProfileService service = host.EnsureProfile();
            if (string.Equals((name ?? string.Empty).Trim(), service.Active.displayName, StringComparison.Ordinal)) return;
            profileSaved = service.RenameActive(name, out string error);
            host.ProfilePanel.RefreshProfileEntry();
            ShowGameSelect(profileSaved ? string.Empty : error);
        }

        private void PickProfileColour(int colour)
        {
            profileDeleteArmed = false;
            if (colour < 0 || colour >= SeatColourCount) return;
            ProfileService service = host.EnsureProfile();
            profileSaved = service.SaveColour(colour, out string error);
            PutColourOnFirstSeat(colour);
            ShowGameSelect(profileSaved ? string.Empty : $"Could not save your colour: {error}.");
        }

        // The player's colour goes to the first seat (the seat it had takes the first seat's colour).
        private void PutColourOnFirstSeat(int colour)
        {
            int[] order = FrontEndColours();
            int at = Array.IndexOf(order, colour);
            if (at <= 0) return;
            order[at] = order[0];
            order[0] = colour;
        }

        private void PickAvatarFromFrontEnd(int avatar)
        {
            profileDeleteArmed = false;
            PersonaSet personas = host.PersonaSetForProfile;
            if (personas == null || avatar < 0 || avatar >= personas.AvatarCount) return;
            profileSaved = host.EnsureProfile().SaveAvatar(avatar, out string error);
            ShowGameSelect(profileSaved ? string.Empty : $"Could not save your avatar: {error}.");
        }

        // NEW PLAYER: a player called "Player N" (rename them in NAME); the table starts again for them.
        private void NewProfileFromFrontEnd()
        {
            profileDeleteArmed = false;
            ProfileService service = host.EnsureProfile();
            int n = service.List().Count + 1;
            string name = $"Player {n}";
            while (!service.Store.TryCleanName(name, null, out _, out _) && n < 99) name = $"Player {++n}";
            if (service.Create(name, out string error) == null)
            {
                ShowGameSelect(error);
                return;
            }

            pendingFrontEndPage = FrontEndPage.Profile;
            host.runtimeUi?.CloseGameSelect();
            host.ProfilePanel.RestartForProfile();
        }

        // DELETE PLAYER asks again in place; the second press switches to the player used most recently and takes this one
        // off the computer (its folder is kept in Profiles/.removed).
        private void DeleteProfileFromFrontEnd()
        {
            ProfileService service = host.EnsureProfile();
            List<ProfileInfo> saved = service.List();
            ProfileInfo other = null;
            for (int i = 0; i < saved.Count && other == null; i++)
            {
                if (!string.Equals(saved[i].id, service.Active.id, StringComparison.OrdinalIgnoreCase)) other = saved[i];
            }

            if (other == null)
            {
                ShowGameSelect("The only player on this computer cannot be removed.");
                return;
            }

            if (!profileDeleteArmed)
            {
                profileDeleteArmed = true;
                ShowGameSelect(string.Empty);
                return;
            }

            profileDeleteArmed = false;
            string removed = service.Active.id;
            string removedName = service.Active.displayName;
            if (!service.Use(other.id, out string error) || !service.Remove(removed, out error))
            {
                ShowGameSelect($"Could not remove {removedName}: {error}.");
                return;
            }

            pendingFrontEndPage = FrontEndPage.Profile;
            host.runtimeUi?.CloseGameSelect();
            host.ProfilePanel.RestartForProfile();
        }
    }
}
