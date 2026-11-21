using ConsoleCards.Presentation.UI;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 63 M1 (owner, 2026-11-21): Make a game is a screen of its own; the table it saves is hidden under it.
    /// TEST ON TABLE hides the panel to try the game on the live table, and BACK TO GAME SETUP (or the game menu's
    /// Save table as a game) brings the panel back with everything set in it kept. SAVE still captures the table.
    /// </summary>
    internal sealed partial class PlayerGamesPresenter
    {
        // True while the panel is hidden for TEST ON TABLE.
        internal bool testingOnTable;

        private void TestOnTable(GameSaveTyped typed)
        {
            KeepTyped(typed); // the name, about and rules lines; the rest is kept here already
            host.runtimeUi?.CloseGameSave();
            testingOnTable = true;
            if (host.runtimeUi == null || !host.runtimeUi.ShowMakeGameTestBar(BackToGameSetup))
            {
                host.ShowMessage("Try your game here. Game menu › Save table as a game goes back to the setup.");
            }
        }

        internal void BackToGameSetup() => ShowGameSave(string.Empty); // ShowGameSave ends the test

        /// <summary>Ends a test without opening the panel (a new table, or the panel shown again).</summary>
        internal void EndTableTest()
        {
            if (!testingOnTable) return;
            testingOnTable = false;
            host.runtimeUi?.HideMakeGameTestBar();
        }
    }
}
