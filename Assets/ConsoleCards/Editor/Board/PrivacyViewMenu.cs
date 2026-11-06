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

        // ---------- PRIVACY-P4 (s148): the networked private development session ----------

        private const string SessionItem = Root + "Private development session (online)";

        [MenuItem(SessionItem)]
        private static void ToggleSession()
        {
            bool on = !PrivateDevSession.Enabled;
            PrivateDevSession.SetEditorEnabled(on);
            Menu.SetChecked(SessionItem, on);
            Debug.Log(on
                ? "[PrivateSession] Private development session ON for this Editor: rooms you host or join use the private protocol. Normal rooms refuse this copy."
                : "[PrivateSession] Private development session OFF: normal online play.");
        }

        [MenuItem(SessionItem, true)]
        private static bool ToggleSessionCheck()
        {
            Menu.SetChecked(SessionItem, PrivateDevSession.Enabled);
            return true;
        }

        /// <summary>
        /// A development build for the guest computer, with the flag file that switches the private development session
        /// on for it. A release build has no private-session code at all.
        /// </summary>
        [MenuItem(Root + "Build private-session guest (development build)")]
        private static void BuildGuest() => BuildGuestInto("Builds/MLP/PD1");

        // A second guest copy in its own folder has its own profiles (its own seat key), for a host + two guests run.
        [MenuItem(Root + "Build second private-session guest (development build)")]
        private static void BuildSecondGuest() => BuildGuestInto("Builds/MLP/PD2");

        [MenuItem(Root + "Build second private-session guest (development build)", true)]
        private static bool CanBuildSecondGuest() => !UnityEngine.Application.isPlaying;

        private static void BuildGuestInto(string folder)
        {
            string exe = System.IO.Path.Combine(folder, "Console Cards.exe");
            System.Collections.Generic.List<string> scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes) if (scene.enabled) scenes.Add(scene.path);
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, PrivateDevSession.FlagFileName),
                    "Development only: this copy plays the private development session (doc 10 §57). Delete this file for normal online play.");
            }

            Debug.Log($"[PrivateSession] Guest build: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalTime.TotalSeconds:F0} s → {exe}");
        }

        [MenuItem(Root + "Build private-session guest (development build)", true)]
        private static bool CanBuildGuest() => !UnityEngine.Application.isPlaying;

        private static void Show(int seat)
        {
            TabletopComposition table = Object.FindAnyObjectByType<TabletopComposition>();
            Debug.Log(TabletopComposition.ShowPrivateRecipientViewForDevelopment(table, seat));
        }
    }
}
