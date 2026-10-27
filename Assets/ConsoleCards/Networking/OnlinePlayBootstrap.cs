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
            MultiplayerSessions.Reset();
            MultiplayerSessions.SetFactory(() => new FusionMultiplayerSession());
            // MP-2: leaving the game (or Play mode in the Editor) closes the connection; nothing reconnects after it.
            UnityEngine.Application.quitting -= LeaveOnQuit;
            UnityEngine.Application.quitting += LeaveOnQuit;
        }

        private static void LeaveOnQuit()
        {
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
