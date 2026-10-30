namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Owner, 2026-10-29: the main menu's SETTINGS screen (FE_Settings): Light, Colours, Quality and Room sound, each
    /// with &lt; &gt;. They are kept on this computer and used at every table (they are no longer in the table's menu).
    /// </summary>
    public sealed partial class FrontEndView
    {
        private const string SettingsRows = "Pages/Settings/Panel/";

        private void OpenSettings()
        {
            // Board 6 (owner, 2026-10-30): the same Settings page as in a game (GAME, LOOK, CONTROLS, SOUND,
            // PROFILES); BACK comes back here.
            if (actions?.OpenBoardSettings != null && actions.OpenBoardSettings()) return;
            // A front end made before FE_Settings: SETTINGS opens the player profile, as before.
            if (pages[(int)FrontEndPage.Settings] == null) actions?.OpenProfile?.Invoke();
            else Go(FrontEndPage.Settings);
        }

        private void BindSettingsButtons()
        {
            Arrows(SettingsRows + "Light", step => actions?.CycleLight?.Invoke(step));
            Arrows(SettingsRows + "Colours", step => actions?.CycleColours?.Invoke(step));
            Arrows(SettingsRows + "Quality", step => actions?.CycleQuality?.Invoke(step));
            Arrows(SettingsRows + "RoomSound", step => actions?.CycleRoomSound?.Invoke(step));
            On("Pages/Settings/Back", () => Go(FrontEndPage.Home));
        }

        // The settings rows have their arrows straight on the row (Previous, Next), not in a Picker box.
        private void Arrows(string row, System.Action<int> cycle)
        {
            On(row + "/Previous", () => cycle(-1));
            On(row + "/Next", () => cycle(1));
        }

        private void RenderSettings()
        {
            SetText(SettingsRows + "Light/Value", model.LightText);
            SetText(SettingsRows + "Colours/Value", model.ColoursText);
            SetText(SettingsRows + "Quality/Value", model.QualityText);
            SetText(SettingsRows + "RoomSound/Value", model.RoomSoundText);
        }
    }
}
