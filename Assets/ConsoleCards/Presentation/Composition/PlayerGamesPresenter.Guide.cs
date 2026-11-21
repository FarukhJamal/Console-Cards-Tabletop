namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 63 H1 (owner, 2026-11-22): the Make a game guide opens by itself the first time Make a game is opened, and
    /// GUIDE in its top bar plays it again. Finished or skipped is kept with the hints' seen marks (Profiles/&lt;id&gt;/
    /// hints/seen.json), on this computer only. It shows whatever the Assist and Hints settings are: it is asked for
    /// only once.
    /// </summary>
    internal sealed partial class PlayerGamesPresenter
    {
        internal const string MakeGameGuideId = "platform.guide.make-a-game";

        // True from opening Make a game until the next panel model is made.
        private bool openGuideOnce;

        private void AskForGuideIfNew()
        {
            host.RulesCardPanel.EnsureHints();
            openGuideOnce = host.hintService != null && !host.hintService.IsWalkthroughSeen(MakeGameGuideId);
        }

        // The panel's model asks for the guide once; a rebind after that keeps it as the player left it.
        private bool TakeGuideRequest()
        {
            bool open = openGuideOnce;
            openGuideOnce = false;
            return open;
        }

        private void GuideDone()
        {
            host.RulesCardPanel.EnsureHints();
            host.hintService?.MarkWalkthroughSeen(MakeGameGuideId);
        }
    }
}
