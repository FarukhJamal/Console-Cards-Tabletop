using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConsoleCards.Application.Authority;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;

namespace ConsoleCards.Networking
{
    /// <summary>
    /// Online play through Photon Fusion 2 (owner's choice, 2026-10-26), behind <see cref="IMultiplayerSession"/>.
    /// Fusion is used for the cloud connection, the room list, joining and its message channel; the table itself is
    /// not made of Fusion objects. The host runs the table's own authority (doc 10, doc 30 A6).
    /// MP-0: connect to the room list, and leave. MP-2: rooms (Host mode: the host is one player's computer, through
    /// Photon's relay when a direct connection is not possible), public ones in the list and private ones by code,
    /// messages, and a lost connection: a client tries to get back into its room on its own (Reconnecting) for about
    /// half a minute; the room's table code then gives it its seat back.
    /// Rooms are kept apart by game version (AppVersion): a computer with another build never sees or joins them.
    /// </summary>
    internal sealed class FusionMultiplayerSession : IMultiplayerSession, INetworkRunnerCallbacks
    {
        private const string TitleKey = "t";
        private const string GameKey = "g";
        private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 6;
        private const int ProtocolVersion = 1;
        private static readonly float[] RetrySeconds = { 1f, 2f, 3f, 5f, 8f, 10f };

        private NetworkRunner runner;
        private MultiplayerSessionState state = MultiplayerSessionState.Offline;
        private string status = "Offline.";
        private string region = string.Empty;
        private string roomCode = string.Empty;
        private string roomTitle = string.Empty;
        private bool roomPrivate;
        private int roomMax = 8;
        private string roomGame = string.Empty;
        private bool hosting;
        private int reliableCounter;
        private int attempt; // which try this is, to stop a stale one
        private readonly List<OnlineRoomListing> rooms = new List<OnlineRoomListing>();
        private static readonly System.Random CodeRandom = new System.Random();

        /// <summary>Set while the game (or Play mode in the Editor) is ending: nothing connects or reconnects then.</summary>
        internal static bool Closing;

        private static bool Stopped => Closing || !UnityEngine.Application.isPlaying;

        public MultiplayerSessionState State => state;
        public string Status => status;
        public string Region => region;
        public int VisibleRoomCount => rooms.Count;
        public IReadOnlyList<OnlineRoomListing> Rooms => rooms;
        public bool IsHost => runner != null && runner.IsRunning && runner.IsServer;
        public string RoomCode => state == MultiplayerSessionState.InRoom || state == MultiplayerSessionState.Reconnecting ? roomCode : string.Empty;
        public string RoomTitle => RoomCode.Length > 0 ? roomTitle : string.Empty;
        public bool RoomIsPrivate => roomPrivate;

        public int LocalConnection =>
            runner != null && runner.IsRunning && state == MultiplayerSessionState.InRoom ? runner.LocalPlayer.RawEncoded : -1;

        public event Action Changed;
        public event Action<int> ConnectionJoined;
        public event Action<int> ConnectionLeft;
        public event Action<int, byte[]> MessageReceived;

        // ---------- the room list ----------

        public void ConnectToLobby()
        {
            if (state == MultiplayerSessionState.Connecting
                || state == MultiplayerSessionState.InLobby
                || state == MultiplayerSessionState.JoiningRoom
                || state == MultiplayerSessionState.InRoom
                || state == MultiplayerSessionState.Reconnecting)
            {
                return;
            }

            _ = ConnectToLobbyAsync(++attempt);
        }

        private async Task ConnectToLobbyAsync(int tryNumber)
        {
            if (Stopped) return; // never while the game ends or in edit mode (leaving Play mode)
            Set(MultiplayerSessionState.Connecting, "Connecting…");
            await CloseRunner();
            if (tryNumber != attempt) return;
            NetworkRunner created = NewRunner();
            StartGameResult result;
            try
            {
                result = await created.JoinSessionLobby(SessionLobby.ClientServer, null, null, Settings(null));
            }
            catch (Exception exception)
            {
                if (runner == created) Fail("Could not connect: " + exception.Message);
                return;
            }

            if (runner != created || tryNumber != attempt) return; // left or moved on meanwhile
            if (!result.Ok)
            {
                Fail("Could not connect: " + Describe(result.ShutdownReason, result.ErrorMessage));
                return;
            }

            region = created.LobbyInfo.IsValid ? created.LobbyInfo.Region ?? string.Empty : string.Empty;
            hosting = false;
            Set(MultiplayerSessionState.InLobby, DescribeLobby());
        }

        // ---------- rooms ----------

        public void CreateRoom(OnlineRoomOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (state != MultiplayerSessionState.InLobby)
            {
                Set(state, "Connect first, then make a room.");
                return;
            }

            roomTitle = Clean(options.Title, 32);
            if (roomTitle.Length == 0) roomTitle = "Console Cards table";
            roomPrivate = options.IsPrivate;
            roomMax = Mathf.Clamp(options.MaxPlayers, 2, 8);
            roomGame = string.Empty;
            roomCode = NewCode(region);
            hosting = true;
            _ = EnterRoomAsync(++attempt, GameMode.Host, false);
        }

        public void JoinRoom(string code)
        {
            if (state != MultiplayerSessionState.InLobby && state != MultiplayerSessionState.Failed)
            {
                Set(state, "Leave this room first.");
                return;
            }

            string parsed = ParseCode(code, region);
            if (parsed == null)
            {
                Set(state, "That is not a room code (it looks like eu-K7PQ2X).");
                return;
            }

            roomCode = parsed;
            roomTitle = string.Empty;
            roomPrivate = false;
            hosting = false;
            _ = EnterRoomAsync(++attempt, GameMode.Client, false);
        }

        public void LeaveRoom()
        {
            if (state != MultiplayerSessionState.InRoom
                && state != MultiplayerSessionState.JoiningRoom
                && state != MultiplayerSessionState.Reconnecting)
            {
                return;
            }

            attempt++;
            roomCode = string.Empty;
            hosting = false;
            _ = BackToLobbyAsync(attempt, "You left the room.");
        }

        public void UpdateRoom(string game, bool open)
        {
            roomGame = Clean(game, 32);
            if (!IsHost || runner.SessionInfo == null || !runner.SessionInfo.IsValid) return;
            try
            {
                runner.SessionInfo.IsOpen = open;
                runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
                {
                    { TitleKey, roomTitle },
                    { GameKey, roomGame },
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Online] The room list could not be updated: " + exception.Message);
            }
        }

        private async Task EnterRoomAsync(int tryNumber, GameMode mode, bool again)
        {
            if (Stopped) return; // never while the game ends or in edit mode (leaving Play mode)
            Set(again ? MultiplayerSessionState.Reconnecting : MultiplayerSessionState.JoiningRoom,
                again ? "Connection lost: getting back into the room…" : mode == GameMode.Host ? "Making the room…" : "Joining " + roomCode + "…");
            await CloseRunner();
            if (tryNumber != attempt || Stopped) return;

            string roomRegion = RegionOf(roomCode);
            NetworkRunner created = NewRunner();
            StartGameArgs args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = roomCode,
                CustomPhotonAppSettings = Settings(roomRegion),
            };
            if (mode == GameMode.Host)
            {
                args.PlayerCount = roomMax;
                args.IsVisible = !roomPrivate;
                args.IsOpen = true;
                args.SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { TitleKey, roomTitle },
                    { GameKey, roomGame },
                };
            }

            StartGameResult result;
            try
            {
                result = await created.StartGame(args);
            }
            catch (Exception exception)
            {
                if (runner == created && tryNumber == attempt) await RoomFailed(tryNumber, mode, again, "Could not reach the room: " + exception.Message, ShutdownReason.Error);
                return;
            }

            if (runner != created || tryNumber != attempt) return;
            if (!result.Ok)
            {
                await RoomFailed(tryNumber, mode, again, Describe(result.ShutdownReason, result.ErrorMessage), result.ShutdownReason);
                return;
            }

            region = string.IsNullOrEmpty(roomRegion) ? region : roomRegion;
            SessionInfo info = created.SessionInfo;
            if (info != null && info.IsValid)
            {
                roomPrivate = !info.IsVisible;
                roomMax = info.MaxPlayers;
                roomTitle = Property(info, TitleKey, roomTitle);
            }

            retry = 0;
            bool tookOver = takingOver && mode == GameMode.Host;
            takingOver = false;
            newHostUntil = 0f;
            Set(MultiplayerSessionState.InRoom, tookOver
                ? "The host left: you host the room now."
                : mode == GameMode.Host
                ? $"Room {roomCode} is open{(roomPrivate ? " (private: share the code)" : string.Empty)}."
                : again ? "Back in the room." : $"In room {roomCode}.");
        }

        private int retry;

        // MP-5 (owner, 2026-10-27: "if the host leaves, another player takes over"): while a new host is expected, a
        // room that is gone is tried again; the player taking over hosts the same code (or joins it when the old room
        // is still there: only its own connection had dropped).
        private float newHostUntil;
        private bool takingOver;
        private const float TakeOverRetrySeconds = 1.5f;

        public void TakeOverRoom()
        {
            if (string.IsNullOrEmpty(roomCode) || Stopped) return;
            takingOver = true;
            newHostUntil = Time.realtimeSinceStartup + 30f;
            hosting = true;
            _ = EnterRoomAsync(++attempt, GameMode.Host, true);
        }

        public void ExpectNewHost(float seconds)
        {
            newHostUntil = Mathf.Max(newHostUntil, Time.realtimeSinceStartup + Mathf.Max(0f, seconds));
        }

        // A client that lost its room tries again a few times; a room that is gone (the host left) ends the tries.
        private async Task RoomFailed(int tryNumber, GameMode mode, bool again, string why, ShutdownReason reason)
        {
            bool gone = reason == ShutdownReason.GameNotFound || reason == ShutdownReason.GameClosed
                || reason == ShutdownReason.GameIsFull || reason == ShutdownReason.IncompatibleConfiguration
                || reason == ShutdownReason.GameIdAlreadyExists;
            if (again && Time.realtimeSinceStartup < newHostUntil && reason != ShutdownReason.GameIsFull
                && reason != ShutdownReason.IncompatibleConfiguration)
            {
                // MP-5: the room is changing hosts. The player taking over hosts it once the old room has gone, and
                // joins it if it is still there; the others try to join until the new host has opened it.
                GameMode next = !takingOver ? GameMode.Client
                    : reason == ShutdownReason.GameIdAlreadyExists ? GameMode.Client
                    : reason == ShutdownReason.GameNotFound || reason == ShutdownReason.GameClosed ? GameMode.Host
                    : mode;
                Set(MultiplayerSessionState.Reconnecting, takingOver
                    ? "The host left: you are taking over the room…"
                    : "The host left: waiting for the next host…");
                await Task.Delay(TimeSpan.FromSeconds(TakeOverRetrySeconds));
                if (tryNumber != attempt || Stopped) return;
                hosting = next == GameMode.Host;
                _ = EnterRoomAsync(++attempt, next, true);
                return;
            }

            takingOver = false;
            if (mode == GameMode.Client && again && !gone && retry < RetrySeconds.Length)
            {
                float wait = RetrySeconds[retry++];
                Set(MultiplayerSessionState.Reconnecting, $"Connection lost: trying again in {wait:0} s…");
                await Task.Delay(TimeSpan.FromSeconds(wait));
                if (tryNumber != attempt || Stopped) return;
                _ = EnterRoomAsync(++attempt, GameMode.Client, true);
                return;
            }

            retry = 0;
            roomCode = string.Empty;
            hosting = false;
            string message = again && gone ? "The room has closed (the host left)." : (mode == GameMode.Host ? "Could not make the room: " : "Could not join: ") + why;
            await BackToLobbyAsync(tryNumber, message);
        }

        private async Task BackToLobbyAsync(int tryNumber, string message)
        {
            await CloseRunner();
            if (tryNumber != attempt) return;
            Set(MultiplayerSessionState.Offline, message);
            lastMessage = message;
            _ = ConnectToLobbyAsync(++attempt);
        }

        private string lastMessage;

        // ---------- messages ----------

        public void SendToHost(byte[] data, int offset, int count)
        {
            if (runner == null || !runner.IsRunning || runner.IsServer || state != MultiplayerSessionState.InRoom) return;
            runner.SendReliableDataToServer(NextKey(), new ReadOnlySpan<byte>(data, offset, count));
        }

        public void SendTo(int connection, byte[] data, int offset, int count)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer) return;
            PlayerRef player = PlayerRef.FromEncoded(connection);
            if (player == runner.LocalPlayer) return;
            runner.SendReliableDataToPlayer(player, NextKey(), new ReadOnlySpan<byte>(data, offset, count));
        }

        public void Disconnect(int connection)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer) return;
            PlayerRef player = PlayerRef.FromEncoded(connection);
            if (player != runner.LocalPlayer) runner.Disconnect(player, null);
        }

        private ReliableKey NextKey() => ReliableKey.FromInts(++reliableCounter, ProtocolVersion, 0, 0);

        // ---------- leaving ----------

        public void Disconnect()
        {
            attempt++;
            roomCode = string.Empty;
            hosting = false;
            rooms.Clear();
            region = string.Empty;
            NetworkRunner closing = runner;
            runner = null;
            Set(MultiplayerSessionState.Offline, "Offline.");
            if (closing != null) _ = closing.Shutdown();
        }

        // ---------- runners ----------

        private NetworkRunner NewRunner()
        {
            // A runner is used once: Shutdown destroys it, so each connection makes a new one.
            GameObject holder = new GameObject("Online Session (Fusion)");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            NetworkRunner created = holder.AddComponent<NetworkRunner>();
            created.ProvideInput = false;
            created.AddCallbacks(this);
            runner = created;
            return created;
        }

        private async Task CloseRunner()
        {
            NetworkRunner closing = runner;
            runner = null;
            rooms.Clear();
            if (closing == null) return;
            try
            {
                await closing.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Online] Closing the connection: " + exception.Message);
            }
        }

        // The project's Photon settings, with the build's version (other builds never meet) and the room's region.
        private static FusionAppSettings Settings(string fixedRegion)
        {
            FusionAppSettings settings = PhotonAppSettings.Global.AppSettings.GetCopy();
            settings.AppVersion = UnityEngine.Application.version + "-cc" + ProtocolVersion;
            if (!string.IsNullOrEmpty(fixedRegion)) settings.FixedRegion = fixedRegion;
            return settings;
        }

        private void Fail(string message)
        {
            NetworkRunner failed = runner;
            runner = null;
            region = string.Empty;
            rooms.Clear();
            Set(MultiplayerSessionState.Failed, message);
            if (failed != null) _ = failed.Shutdown();
        }

        private void Set(MultiplayerSessionState newState, string newStatus)
        {
            bool changed = state != newState || status != newStatus;
            state = newState;
            status = newStatus;
            if (changed) Debug.Log("[Online] " + newStatus);
            Raise(Changed);
        }

        // Listeners run inside Fusion's callbacks and our own async steps, where an exception would vanish silently: each
        // one is caught and logged so a problem in the game's code shows in the Console.
        private static void Raise(Action listeners)
        {
            if (listeners == null) return;
            foreach (Delegate listener in listeners.GetInvocationList())
            {
                try
                {
                    ((Action)listener)();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void Raise<T>(Action<T> listeners, T value)
        {
            if (listeners == null) return;
            foreach (Delegate listener in listeners.GetInvocationList())
            {
                try
                {
                    ((Action<T>)listener)(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private string DescribeLobby()
        {
            string where = string.IsNullOrEmpty(region) ? string.Empty : " (" + region + ")";
            string list = rooms.Count == 1 ? "1 room open" : rooms.Count + " rooms open";
            string before = string.IsNullOrEmpty(lastMessage) ? string.Empty : lastMessage + " ";
            lastMessage = null;
            return before + "Connected" + where + ". " + list + ".";
        }

        // ---------- codes ----------

        private static string NewCode(string region)
        {
            char[] letters = new char[CodeLength];
            lock (CodeRandom)
            {
                for (int i = 0; i < letters.Length; i++) letters[i] = CodeLetters[CodeRandom.Next(CodeLetters.Length)];
            }

            string where = string.IsNullOrEmpty(region) ? "eu" : region;
            return where + "-" + new string(letters);
        }

        // "eu-k7pq2x", " EU-K7PQ2X ", "K7PQ2X" (this region) -> "eu-K7PQ2X". Null when it cannot be a code.
        internal static string ParseCode(string text, string currentRegion)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string trimmed = text.Trim().Replace(" ", string.Empty);
            int dash = trimmed.IndexOf('-');
            string where = dash > 0 ? trimmed.Substring(0, dash).ToLowerInvariant() : currentRegion;
            string letters = (dash > 0 ? trimmed.Substring(dash + 1) : trimmed).ToUpperInvariant();
            if (string.IsNullOrEmpty(where) || letters.Length != CodeLength) return null;
            for (int i = 0; i < where.Length; i++)
            {
                if (!char.IsLetter(where[i])) return null;
            }

            for (int i = 0; i < letters.Length; i++)
            {
                if (CodeLetters.IndexOf(letters[i]) < 0) return null;
            }

            return where + "-" + letters;
        }

        private static string RegionOf(string code)
        {
            int dash = code.IndexOf('-');
            return dash > 0 ? code.Substring(0, dash) : null;
        }

        private static string Clean(string text, int length)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string trimmed = text.Trim();
            return trimmed.Length > length ? trimmed.Substring(0, length) : trimmed;
        }

        private static string Property(SessionInfo info, string key, string fallback)
        {
            if (info.Properties != null && info.Properties.TryGetValue(key, out SessionProperty value) && value != null)
            {
                object text = value.PropertyValue;
                if (text is string words) return words;
            }

            return fallback;
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
                    return "the room's server region does not exist.";
                case ShutdownReason.GameNotFound:
                    return "there is no room with that code (or it has closed).";
                case ShutdownReason.GameClosed:
                    return "the room is closed.";
                case ShutdownReason.GameIsFull:
                    return "the room is full.";
                case ShutdownReason.GameIdAlreadyExists:
                    return "a room with that code already exists; try again.";
                case ShutdownReason.IncompatibleConfiguration:
                    return "the room was made by a different version of the game.";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.OperationTimeout:
                    return "no answer from the server (check the internet connection).";
                case ShutdownReason.DisconnectedByPluginLogic:
                    return "the host closed the connection.";
                default:
                    return string.IsNullOrEmpty(message) ? reason.ToString() : message;
            }
        }

        // ---------- Fusion callbacks ----------

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner sender, List<SessionInfo> sessionList)
        {
            if (sender != runner) return;
            rooms.Clear();
            for (int i = 0; sessionList != null && i < sessionList.Count; i++)
            {
                SessionInfo info = sessionList[i];
                if (info == null || !info.IsValid || !info.IsVisible) continue;
                rooms.Add(new OnlineRoomListing
                {
                    Code = info.Name,
                    Title = Property(info, TitleKey, info.Name),
                    Game = Property(info, GameKey, string.Empty),
                    Players = info.PlayerCount,
                    MaxPlayers = info.MaxPlayers,
                    IsOpen = info.IsOpen && info.PlayerCount < info.MaxPlayers,
                });
            }

            if (state == MultiplayerSessionState.InLobby) Set(MultiplayerSessionState.InLobby, DescribeLobby());
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner sender, ShutdownReason shutdownReason)
        {
            if (sender != runner) return; // a runner we closed ourselves
            if (state == MultiplayerSessionState.JoiningRoom || state == MultiplayerSessionState.Reconnecting
                || state == MultiplayerSessionState.Connecting)
            {
                // Still starting (found testing, 2026-10-27: a reconnect to a room the host had closed ended as
                // "Disconnected"): the start's own result says what happened (RoomFailed: "the room has closed").
                return;
            }

            runner = null;
            rooms.Clear();
            if (Stopped)
            {
                // The game (or Play mode) is ending and took the runner with it: no getting back in.
                roomCode = string.Empty;
                hosting = false;
                region = string.Empty;
                Set(MultiplayerSessionState.Offline, "Offline.");
                return;
            }

            if (state == MultiplayerSessionState.InRoom && !hosting && !string.IsNullOrEmpty(roomCode))
            {
                // The connection to the host broke: get back in (the same seat waits for us).
                retry = 0;
                _ = EnterRoomAsync(++attempt, GameMode.Client, true);
                return;
            }

            if (state == MultiplayerSessionState.InRoom && hosting)
            {
                roomCode = string.Empty;
                hosting = false;
                _ = BackToLobbyAsync(++attempt, "Your room closed: " + Describe(shutdownReason, null));
                return;
            }

            region = string.Empty;
            if (shutdownReason == ShutdownReason.Ok) Set(MultiplayerSessionState.Offline, "Offline.");
            else Set(MultiplayerSessionState.Failed, "Disconnected: " + Describe(shutdownReason, null));
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner sender, NetDisconnectReason reason)
        {
            if (sender != runner) return;
            Debug.Log("[Online] Disconnected from the host (" + reason + ").");
            // OnShutdown follows and decides what to do (a client tries to get back in).
        }

        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner sender, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            if (sender != runner) return;
            Debug.Log("[Online] Could not reach the host (" + reason + ").");
        }

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner sender, PlayerRef player)
        {
            if (sender != runner || !sender.IsServer || player == sender.LocalPlayer) return;
            Raise(ConnectionJoined, player.RawEncoded);
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner sender, PlayerRef player)
        {
            if (sender != runner || !sender.IsServer || player == sender.LocalPlayer) return;
            Raise(ConnectionLeft, player.RawEncoded);
        }

        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner sender, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            if (sender != runner) return;
            byte[] copy = data.ToArray();
            Action<int, byte[]> listeners = MessageReceived;
            if (listeners == null) return;
            int from = sender.IsServer ? player.RawEncoded : -1;
            foreach (Delegate listener in listeners.GetInvocationList())
            {
                try
                {
                    ((Action<int, byte[]>)listener)(from, copy);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner sender, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // Accepted (the default): the table code checks the game version and the seat in its Hello.
        }

        // Not used: the table is not made of Fusion objects (no input, no scene, no host migration in v1).
        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner sender, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner sender, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner sender, SimulationMessagePtr message) { }
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
