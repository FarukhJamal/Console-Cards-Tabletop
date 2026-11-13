using ConsoleCards.Presentation.Composition;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Board
{
    /// <summary>
    /// DEV-FIXTURE (2026-11-06): Editor-only controls for reproducible Trap Floor test floors. This assembly is
    /// Editor-only, and the fixture itself is compiled only in the Editor, so no player build can reach either.
    /// Arm a seed, then start or deal the game (offline, or as the online host): that deal uses the seed once.
    /// Describe Floor prints the layout and the pawns' tiles to this Editor's Console only.
    /// </summary>
    internal static class TrapFloorFixtureMenu
    {
        private const string Root = "Console Cards/Debug/Fixtures/";

        [MenuItem(Root + "Arm Fixed Deal: Seed 1101")]
        private static void Arm1101() => TrapFloorDevFixtures.ArmDealSeed(1101);

        [MenuItem(Root + "Arm Fixed Deal: Seed 2202")]
        private static void Arm2202() => TrapFloorDevFixtures.ArmDealSeed(2202);

        [MenuItem(Root + "Arm Fixed Deal: Seed 3303")]
        private static void Arm3303() => TrapFloorDevFixtures.ArmDealSeed(3303);

        [MenuItem(Root + "Disarm Fixed Deal")]
        private static void Disarm()
        {
            TrapFloorDevFixtures.Disarm();
            Debug.Log("[Fixture] The next deal is random again.");
        }

        // P8A debug (owner, 2026-11-13): see every floor tile on this Editor's screen to play a floor through to a win.
        [MenuItem(Root + "Peek at Floor: On or Off (Play mode)")]
        private static void Peek() => Debug.Log(TrapFloorDevFixtures.ToggleFloorPeek(Object.FindFirstObjectByType<TabletopComposition>()));

        [MenuItem(Root + "Peek at Floor: On or Off (Play mode)", true)]
        private static bool CanPeek() => UnityEngine.Application.isPlaying;

        [MenuItem(Root + "Describe Floor (Play mode)")]
        private static void Describe()
        {
            TabletopComposition table = Object.FindFirstObjectByType<TabletopComposition>();
            Debug.Log(TrapFloorDevFixtures.DescribeFloor(table));
        }

        // P7 (s159) scenario seam: setup only; the step onto the target is made through the production request path.
        private const string Stage = "Console Cards/Debug/Scenarios/";

        private static void StageNextTo(string target) =>
            Debug.Log(TrapFloorDevFixtures.StageActivePawnNextTo(Object.FindFirstObjectByType<TabletopComposition>(), target));

        [MenuItem(Stage + "Stage turn pawn next to: Key")] private static void NextToKey() => StageNextTo("Key");
        [MenuItem(Stage + "Stage turn pawn next to: Grand Exit")] private static void NextToGrand() => StageNextTo("Grand Exit");
        [MenuItem(Stage + "Stage turn pawn next to: Secret Exit")] private static void NextToSecret() => StageNextTo("Secret Exit");
        [MenuItem(Stage + "Stage turn pawn next to: Fall Trap")] private static void NextToFall() => StageNextTo("Fall Trap");
        [MenuItem(Stage + "Stage turn pawn next to: Slow Trap")] private static void NextToSlow() => StageNextTo("Slow Trap");
        [MenuItem(Stage + "Stage turn pawn next to: Spring Trap")] private static void NextToSpring() => StageNextTo("Spring Trap");
        [MenuItem(Stage + "Stage turn pawn next to: Crane Trap")] private static void NextToCrane() => StageNextTo("Crane Trap");
        [MenuItem(Stage + "Stage turn pawn next to: Blind Trap")] private static void NextToBlind() => StageNextTo("Blind Trap");
        [MenuItem(Stage + "Stage turn pawn next to: Sticky Trap")] private static void NextToSticky() => StageNextTo("Sticky Trap");

        [MenuItem(Stage + "Report state")]
        private static void Report() => Debug.Log(TrapFloorDevFixtures.Report(Object.FindFirstObjectByType<TabletopComposition>()));

        [MenuItem(Stage + "Stage turn pawn next to: Key", true)] private static bool C1() => UnityEngine.Application.isPlaying;
        [MenuItem(Stage + "Stage turn pawn next to: Grand Exit", true)] private static bool C2() => UnityEngine.Application.isPlaying;
        [MenuItem(Stage + "Stage turn pawn next to: Secret Exit", true)] private static bool C3() => UnityEngine.Application.isPlaying;
        [MenuItem(Stage + "Stage turn pawn next to: Fall Trap", true)] private static bool C4() => UnityEngine.Application.isPlaying;
        [MenuItem(Stage + "Report state", true)] private static bool C5() => UnityEngine.Application.isPlaying;

        [MenuItem(Root + "Describe Floor (Play mode)", true)]
        private static bool CanDescribe() => UnityEngine.Application.isPlaying;
    }
}
