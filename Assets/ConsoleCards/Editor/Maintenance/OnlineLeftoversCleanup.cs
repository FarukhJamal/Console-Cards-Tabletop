using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Editor.Maintenance
{
    /// <summary>
    /// MP-2 (found testing online, 2026-10-27): leaving Play mode while connected lets the networking package finish
    /// its disconnect after the scene is back in edit mode, and it leaves its helper objects (ConnectionHandler,
    /// RegionHandler) in the open scene, where a save would keep them. For a few seconds after Play mode ends, such
    /// objects are removed again (only objects carrying those helper scripts, never anything else).
    /// </summary>
    [InitializeOnLoad]
    internal static class OnlineLeftoversCleanup
    {
        private const double WatchSeconds = 8.0;
        private static readonly string[] HelperScripts = { "ConnectionHandler", "RegionHandler" };
        private static double watchUntil;

        static OnlineLeftoversCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            watchUntil = EditorApplication.timeSinceStartup + WatchSeconds;
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
        }

        private static void Watch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup > watchUntil)
            {
                EditorApplication.update -= Watch;
                return;
            }

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (IsHelper(root)) Object.DestroyImmediate(root);
                }
            }
        }

        private static bool IsHelper(GameObject root)
        {
            if (root == null || root.transform.childCount > 0) return false;
            for (int i = 0; i < HelperScripts.Length; i++)
            {
                if (root.GetComponent(HelperScripts[i]) != null) return true;
            }

            return false;
        }
    }
}
