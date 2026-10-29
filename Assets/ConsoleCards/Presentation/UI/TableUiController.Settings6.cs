using System;
using ConsoleCards.Presentation.Settings;
using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// MENU-1 to design board 6 (owner, 2026-10-30): what the Settings page adds to the table's settings: Show the
    /// rules card, Ask before Reset (kept on this computer), Tips while placing (FIRST GAMES turns the tips off after
    /// the first five games; ALWAYS; OFF), the look (Light, Quality, colour-blind seats; the hand as a fan), the room
    /// sound and who is playing. The look is this computer's (LookSettings); colour-blind seats apply from the next
    /// table, as on the main menu.
    /// </summary>
    public sealed partial class TableUiController
    {
        private const string AskBeforeResetPref = "ConsoleCards.AskBeforeReset";
        private const string TipsModePref = "ConsoleCards.TipsMode";
        private const string GamesStartedPref = "ConsoleCards.GamesStarted";
        private const int FirstGames = 5;

        private bool recRulesCard;
        private Action<bool> rawSetRulesCard;
        private Action<bool> setRulesCardFromSettings;
        private bool recHandFan = true;
        private Action<bool> setHandFanFromSettings;
        private Action<bool> setAskResetFromSettings;
        private Action<int> setTipsModeFromSettings;
        private Action<int> lookLight;
        private Action<int> lookQuality;
        private Action lookColourBlind;
        private Action lookRoomSound;
        private Action<int> cycleLightFromSettings;
        private Action<int> cycleQualityFromSettings;
        private Action<bool> setColourBlindFromSettings;
        private Action<bool> setRoomSoundFromSettings;
        private string playingAs = string.Empty;
        private string lastCountedGame = string.Empty;

        private static bool AskBeforeReset => PlayerPrefs.GetInt(AskBeforeResetPref, 1) != 0;

        /// <summary>The look and sound settings' actions (from the table: they apply the change to the room).</summary>
        public void ShowLookActions(Action<int> nextLight, Action<int> nextQuality, Action toggleColourBlind,
            Action toggleRoomSound, string profileName)
        {
            lookLight = nextLight;
            lookQuality = nextQuality;
            lookColourBlind = toggleColourBlind;
            lookRoomSound = toggleRoomSound;
            playingAs = profileName ?? string.Empty;
            RefreshSettingsView();
        }

        /// <summary>Counts a game table once (for Tips while placing › FIRST GAMES).</summary>
        public void CountGameStarted(string gameKey)
        {
            if (string.IsNullOrEmpty(gameKey) || gameKey == lastCountedGame) return;
            lastCountedGame = gameKey;
            PlayerPrefs.SetInt(GamesStartedPref, PlayerPrefs.GetInt(GamesStartedPref, 0) + 1);
            EndFirstGamesTips();
        }

        private void RememberRulesCard(bool available, bool on, Action<bool> set)
        {
            recRulesCard = on;
            rawSetRulesCard = available ? set : null;
            RefreshSettingsView();
        }

        // FIRST GAMES: after the first few games the tips go off by themselves.
        private void EndFirstGamesTips()
        {
            if (!recTips || rawSetTips == null) return;
            if (PlayerPrefs.GetInt(TipsModePref, 0) != 0 || PlayerPrefs.GetInt(GamesStartedPref, 0) < FirstGames) return;
            recTips = false;
            rawSetTips(false);
        }

        private void FillBoardSettings(TableSettingsModel model)
        {
            model.RulesCardOn = recRulesCard;
            model.SetRulesCard = rawSetRulesCard != null ? setRulesCardFromSettings ??= SetRulesCardFromSettings : null;
            model.AskBeforeReset = AskBeforeReset;
            model.SetAskBeforeReset = setAskResetFromSettings ??= on =>
            {
                PlayerPrefs.SetInt(AskBeforeResetPref, on ? 1 : 0);
                PlayerPrefs.Save();
            };
            model.TipsMode = !recTips ? 2 : Mathf.Clamp(PlayerPrefs.GetInt(TipsModePref, 0), 0, 1);
            model.SetTipsMode = rawSetTips != null ? setTipsModeFromSettings ??= SetTipsMode : null;
            model.LightText = LookSettings.LightName(LookSettings.Light);
            model.CycleLight = lookLight != null ? cycleLightFromSettings ??= step =>
            {
                lookLight?.Invoke(step);
                RefreshSettingsView();
            } : null;
            model.QualityText = LookSettings.QualityName(LookSettings.Quality);
            model.CycleQuality = lookQuality != null ? cycleQualityFromSettings ??= step =>
            {
                lookQuality?.Invoke(step);
                RefreshSettingsView();
            } : null;
            model.ColourBlindOn = LookSettings.ColourBlind;
            model.SetColourBlind = lookColourBlind != null ? setColourBlindFromSettings ??= on =>
            {
                if (on != LookSettings.ColourBlind) lookColourBlind?.Invoke();
            } : null;
            model.RoomSoundOn = LookSettings.RoomSound;
            model.SetRoomSound = lookRoomSound != null ? setRoomSoundFromSettings ??= on =>
            {
                if (on != LookSettings.RoomSound) lookRoomSound?.Invoke();
            } : null;
            model.HandFanOn = recHandFan;
            model.SetHandFan = hudHandStyle != null ? setHandFanFromSettings ??= SetHandFanFromSettings : null;
            model.ProfileText = string.IsNullOrEmpty(playingAs)
                ? "Profiles are picked on the main menu (PLAY)."
                : $"Playing as <b>{playingAs}</b> on this computer. Profiles are picked and made on the main menu (PLAY).";
        }

        private void SetRulesCardFromSettings(bool on)
        {
            recRulesCard = on;
            rawSetRulesCard?.Invoke(on);
        }

        private void SetHandFanFromSettings(bool fan)
        {
            if (fan != recHandFan) hudHandStyle?.Invoke(); // the hand's own toggle (it reports back through SetHandStyle)
            recHandFan = fan;
        }

        private void SetTipsMode(int mode)
        {
            if (mode < 2) PlayerPrefs.SetInt(TipsModePref, mode);
            PlayerPrefs.Save();
            bool on = mode < 2;
            recTips = on;
            rawSetTips?.Invoke(on);
        }
    }
}
