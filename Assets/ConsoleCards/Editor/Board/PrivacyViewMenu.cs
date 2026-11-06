using ConsoleCards.Presentation.Composition;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Board
{
    /// <summary>
    /// PRIVACY-SCENE (s146), development only: rebuilds the running offline Trap Floor table from one recipient's
    /// seed-free package and inspects the scene. Editor menu only; nothing here exists in a player build.
    /// </summary>
    internal static class PrivacyViewMenu
    {
        private const string Root = "Console Cards/Debug/Privacy/";

        [MenuItem(Root + "Seed-free view as seat 1 (Play mode)")] private static void Seat1() => Show(0);
        [MenuItem(Root + "Seed-free view as seat 2 (Play mode)")] private static void Seat2() => Show(1);
        [MenuItem(Root + "Seed-free view as a spectator (Play mode)")] private static void Spectator() => Show(-1);

        [MenuItem(Root + "Seed-free view as seat 1 (Play mode)", true)]
        [MenuItem(Root + "Seed-free view as seat 2 (Play mode)", true)]
        [MenuItem(Root + "Seed-free view as a spectator (Play mode)", true)]
        private static bool CanShow() => UnityEngine.Application.isPlaying;

        private static void Show(int seat)
        {
            TabletopComposition table = Object.FindFirstObjectByType<TabletopComposition>();
            Debug.Log(TabletopComposition.ShowPrivateRecipientViewForDevelopment(table, seat));
        }
    }
}
