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
            // DealFloor(int floorIndex, optional...): reflection needs every parameter, so the optional ones get their
            // defaults (NEXTFLOOR-1, 2026-11-05; keepRun added later broke the fixed two-argument call).
            ParameterInfo[] parameters = deal.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = (int)index.GetValue(template) + 1;
            for (int i = 1; i < parameters.Length; i++)
                arguments[i] = parameters[i].Name == "keepRun" ? true : parameters[i].DefaultValue; // as Next floor does
            deal.Invoke(cartridge, arguments);
        }

        [MenuItem("Console Cards/Debug/Deal Next Trap Floor (Play mode)", true)]
        private static bool CanDeal() => UnityEngine.Application.isPlaying;
    }
}
