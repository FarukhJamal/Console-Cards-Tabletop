using System.Reflection;
using ConsoleCards.Presentation.Composition;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Board
{
    /// <summary>Play-mode helper: deals Trap Floor's next (smaller) floor at once, to check the board's spaces.</summary>
    internal static class TrapFloorDebugMenu
    {
        [MenuItem("Console Cards/Debug/Deal Next Trap Floor (Play mode)")]
        private static void DealNextFloor()
        {
            TabletopComposition table = Object.FindFirstObjectByType<TabletopComposition>();
            if (table == null) { Debug.Log("[Debug] No table in play."); return; }
            const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            object cartridge = typeof(TabletopComposition).GetField("activeCartridge", any)?.GetValue(table);
            object template = typeof(TabletopComposition).GetField("trapFloorTemplate", any)?.GetValue(table);
            MethodInfo deal = cartridge?.GetType().GetMethod("DealFloor", any);
            PropertyInfo index = template?.GetType().GetProperty("FloorIndex");
            if (deal == null || index == null) { Debug.Log("[Debug] Not a Trap Floor table."); return; }
            deal.Invoke(cartridge, new object[] { (int)index.GetValue(template) + 1 });
        }

        [MenuItem("Console Cards/Debug/Deal Next Trap Floor (Play mode)", true)]
        private static bool CanDeal() => UnityEngine.Application.isPlaying;
    }
}
