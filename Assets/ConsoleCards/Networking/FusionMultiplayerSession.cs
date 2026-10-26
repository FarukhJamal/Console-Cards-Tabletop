using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConsoleCards.Application.Authority;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace ConsoleCards.Networking
{
    /// <summary>
    /// Online play through Photon Fusion 2 (owner's choice, 2026-10-26), behind <see cref="IMultiplayerSession"/>.
    /// Fusion is used for the cloud connection, the room list, joining and its message channel; the table itself is
    /// not made of Fusion objects. The host runs the table's own authority (doc 10, doc 30 A6). MP-0: connect to the
    /// room list (proves the App ID, the region and the network), and leave.
    /// </summary>
    internal sealed class FusionMultiplayerSession : IMultiplayerSession, INetworkRunnerCallbacks
    {
        private NetworkRunner runner;
        private MultiplayerSessionState state = MultiplayerSessionState.Offline;
        private string status = "Offline.";
        private string region = string.Empty;
        private int visibleRoomCount;

        public MultiplayerSessionState State => state;
        public string Status => status;
        public string Region => region;
        public int VisibleRoomCount => visibleRoomCount;
        public bool IsHost => runner != null && runner.IsRunning && runner.IsServer;

        public event Action Changed;

        public void ConnectToLobby()
        {
            if (state == MultiplayerSessionState.Connecting
                || state == MultiplayerSessionState.InLobby
                || state == MultiplayerSessionState.JoiningRoom
                || state == MultiplayerSessionState.InRoom)
            {
                return;
            }

            _ = ConnectToLobbyAsync();
        }

        public void Disconnect()
        {
            if (runner == null)
            {
                Set(MultiplayerSessionState.Offline, "Offline.");
                return;
            }

            NetworkRunner closing = runner;
            runner = null;
            region = string.Empty;
            visibleRoomCount = 0;
            Set(MultiplayerSessionState.Offline, "Offline.");
            if (closing != null) _ = closing.Shutdown();
        }

        private async Task ConnectToLobbyAsync()
        {
            Set(MultiplayerSessionState.Connecting, "Connecting…");

            // A runner is used once: Shutdown destroys it, so each connection makes a new one.
            GameObject host = new GameObject("Online Session (Fusion)");
            UnityEngine.Object.DontDestroyOnLoad(host);
            NetworkRunner created = host.AddComponent<NetworkRunner>();
            created.ProvideInput = false;
            created.AddCallbacks(this);
            runner = created;

            StartGameResult result;
            try
            {
                result = await created.JoinSessionLobby(SessionLobby.ClientServer);
            }
            catch (Exception exception)
            {
                if (runner == created) Fail("Could not connect: " + exception.Message);
                return;
            }

            if (runner != created) return; // left while connecting

            if (!result.Ok)
            {
                Fail("Could not connect: " + Describe(result.ShutdownReason, result.ErrorMessage));
                return;
            }

            region = created.LobbyInfo.IsValid ? created.LobbyInfo.Region ?? string.Empty : string.Empty;
            Set(MultiplayerSessionState.InLobby, DescribeLobby());
        }

        private void Fail(string message)
        {
            NetworkRunner failed = runner;
            runner = null;
            region = string.Empty;
            visibleRoomCount = 0;
            Set(MultiplayerSessionState.Failed, message);
            if (failed != null) _ = failed.Shutdown();
        }

        private void Set(MultiplayerSessionState newState, string newStatus)
        {
            state = newState;
            status = newStatus;
            Debug.Log("[Online] " + newStatus);
            Changed?.Invoke();
        }

        private string DescribeLobby()
        {
            string where = string.IsNullOrEmpty(region) ? string.Empty : " (" + region + ")";
            string rooms = visibleRoomCount == 1 ? "1 room open" : visibleRoomCount + " rooms open";
            return "Connected" + where + ". " + rooms + ".";
        }

        private static string Describe(ShutdownReason reason, string message)
        {
            switch (reason)
            {
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                    return "the App ID was not accepted (check Fusion > Realtime Settings).";
                case ShutdownReason.MaxCcuReached:
                    return "the server is full right now (player limit of the Photon plan).";
                case ShutdownReason.InvalidRegion:
                    return "the chosen server region does not exist.";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.OperationTimeout:
                    return "no answer from the server (check the internet connection).";
                default:
                    return string.IsNullOrEmpty(message) ? reason.ToString() : message;
            }
        }

        // ---------- Fusion callbacks ----------

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner sender, List<SessionInfo> sessionList)
        {
            if (sender != runner) return;
            visibleRoomCount = sessionList == null ? 0 : sessionList.Count;
            if (state == MultiplayerSessionState.InLobby) Set(MultiplayerSessionState.InLobby, DescribeLobby());
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner sender, ShutdownReason shutdownReason)
        {
            if (sender != runner) return; // a runner we closed ourselves
            runner = null;
            region = string.Empty;
            visibleRoomCount = 0;
            if (shutdownReason == ShutdownReason.Ok) Set(MultiplayerSessionState.Offline, "Offline.");
            else Set(MultiplayerSessionState.Failed, "Disconnected: " + Describe(shutdownReason, null));
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner sender, NetDisconnectReason reason)
        {
            if (sender != runner) return;
            Set(MultiplayerSessionState.Failed, "Disconnected from the host (" + reason + ").");
        }

        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner sender, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            if (sender != runner) return;
            Set(MultiplayerSessionState.Failed, "Could not reach the host (" + reason + ").");
        }

        // Used by later stages (rooms, the table's messages, rejoining); empty in MP-0.
        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner sender, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner sender, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner sender, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner sender, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner sender, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner sender, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner sender, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner sender, PlayerRef player, ReliableKey key, float progress) { }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner sender, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner sender, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner sender) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner sender, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner sender, HostMigrationToken hostMigrationToken) { }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner sender) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner sender) { }
    }
}
