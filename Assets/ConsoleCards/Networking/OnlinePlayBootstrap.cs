using System.Threading.Tasks;
using ConsoleCards.Application.Authority;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ConsoleCards.Networking
{
    /// <summary>
    /// Tells the game that online play is in the build (MP-0): sets <see cref="MultiplayerSessions"/>'s factory when
    /// play starts, so the rest of the game never names Photon. In the Editor it also adds a connection check to the
    /// Console Cards menu (temporary, until the Lobby screen exists in MP-2).
    /// </summary>
    internal static class OnlinePlayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            FusionMultiplayerSession.Closing = false;
            quitAgreed = false;
            MultiplayerSessions.Reset();
            MultiplayerSessions.SetFactory(() => new FusionMultiplayerSession());
            // MP-2: leaving the game (or Play mode in the Editor) closes the connection; nothing reconnects after it.
            UnityEngine.Application.quitting -= LeaveOnQuit;
            UnityEngine.Application.quitting += LeaveOnQuit;
            UnityEngine.Application.wantsToQuit -= AskToQuit;
            UnityEngine.Application.wantsToQuit += AskToQuit;
        }

        // P7 (found live, 2026-11-08): when the window closes, Fusion's own NetworkRunner.OnApplicationQuit shuts the
        // runner down BEFORE Application.quitting reaches LeaveOnQuit, so the guest's goodbye was never sent and the
        // shutdown looked like a lost connection ("getting back into the room…") while the game was ending. Now the
        // first quit request in a room is held for a moment: say goodbye while the connection is still open, close
        // the connection on purpose, then quit. A killed process skips all of this (the host's transport timeout and
        // the departure grace cover that case).
        private static bool quitAgreed;
        private const int GoodbyeDelayMs = 600;

        private static bool AskToQuit()
        {
            if (quitAgreed) return true;
            IMultiplayerSession session = MultiplayerSessions.Current;
            if (session == null || (session.State != MultiplayerSessionState.InRoom
                && session.State != MultiplayerSessionState.Reconnecting))
            {
                quitAgreed = true;
                return true;
            }

            quitAgreed = true;
            MultiplayerSessions.RaiseLeaving();
            _ = QuitAfterGoodbye();
            return false;
        }

        private static async Task QuitAfterGoodbye()
        {
            try
            {
                await Task.Delay(GoodbyeDelayMs); // a few network ticks: the goodbye leaves before the runner closes
            }
            finally
            {
                FusionMultiplayerSession.Closing = true;
                MultiplayerSessions.Current?.Disconnect();
                UnityEngine.Application.Quit();
            }
        }

        private static void LeaveOnQuit()
        {
            MultiplayerSessions.RaiseLeaving(); // P7: a guest says goodbye first (sent while the connection is still open)
            FusionMultiplayerSession.Closing = true;
            MultiplayerSessions.Current?.Disconnect();
        }

#if UNITY_EDITOR
        // Stopping Play mode: this comes before the runner is destroyed (whose shutdown would otherwise look like a
        // lost connection and start getting back into the room, in edit mode).
        [InitializeOnLoadMethod]
        private static void WatchPlayMode()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) LeaveOnQuit();
        }

        private const string CheckMenu = "Console Cards/Online/Check Connection (Play mode)";
        private const string LeaveMenu = "Console Cards/Online/Leave (Play mode)";

        [MenuItem(CheckMenu)]
        private static void CheckConnection()
        {
            IMultiplayerSession session = MultiplayerSessions.Current;
            if (session == null)
            {
                Debug.LogWarning("[Online] Online play is not set up in this build.");
                return;
            }

            if (session.State == MultiplayerSessionState.InLobby)
            {
                Debug.Log("[Online] " + session.Status);
                return;
            }

            session.ConnectToLobby();
        }

        [MenuItem(CheckMenu, true)]
        private static bool CanCheckConnection() => UnityEngine.Application.isPlaying;

        [MenuItem(LeaveMenu)]
        private static void Leave() => MultiplayerSessions.Current?.Disconnect();

        [MenuItem(LeaveMenu, true)]
        private static bool CanLeave() => UnityEngine.Application.isPlaying;
#endif
    }
}
