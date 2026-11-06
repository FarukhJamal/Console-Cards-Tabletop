using ConsoleCards.Presentation.Composition;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Board
{
    /// <summary>
    /// PRIVACY-SCENE (s146/s147), development only: the first use copies the running offline Trap Floor table into a
    /// simulated host (with the PRIVACY-SCENE-2 fixture); each use rebuilds the scene from one recipient's seed-free
    /// package and inspects it. Switching recipient takes the previous view down first. Editor menu only; nothing here exists in a player build.
    /// </summary>
    internal static class PrivacyViewMenu
    {
        private const string Root = "Console Cards/Debug/Privacy/";

        [MenuItem(Root + "Seed-free view as seat 1 (Play mode)")] private static void Seat1() => Show(0);
        [MenuItem(Root + "Seed-free view as seat 2 (Play mode)")] private static void Seat2() => Show(1);
        [MenuItem(Root + "Seed-free view as seat 3 (Play mode)")] private static void Seat3() => Show(2);
        [MenuItem(Root + "Seed-free view as a spectator (Play mode)")] private static void Spectator() => Show(-1);

        [MenuItem(Root + "Seed-free view as seat 1 (Play mode)", true)]
        [MenuItem(Root + "Seed-free view as seat 2 (Play mode)", true)]
        [MenuItem(Root + "Seed-free view as seat 3 (Play mode)", true)]
        [MenuItem(Root + "Seed-free view as a spectator (Play mode)", true)]
        private static bool CanShow() => UnityEngine.Application.isPlaying;

        private static void Show(int seat)
        {
            TabletopComposition table = Object.FindAnyObjectByType<TabletopComposition>();
            Debug.Log(TabletopComposition.ShowPrivateRecipientViewForDevelopment(table, seat));
        }
    }
}
