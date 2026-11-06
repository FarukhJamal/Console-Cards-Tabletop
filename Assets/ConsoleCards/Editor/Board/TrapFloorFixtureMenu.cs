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

        [MenuItem(Root + "Describe Floor (Play mode)")]
        private static void Describe()
        {
            TabletopComposition table = Object.FindFirstObjectByType<TabletopComposition>();
            Debug.Log(TrapFloorDevFixtures.DescribeFloor(table));
        }

        [MenuItem(Root + "Describe Floor (Play mode)", true)]
        private static bool CanDescribe() => UnityEngine.Application.isPlaying;
    }
}
