using System;
using System.Collections.Generic;

namespace ConsoleCards.Application.Authority
{
    /// <summary>Where this computer is in online play (MP-0, doc 10 §14).</summary>
    public enum MultiplayerSessionState
    {
        Offline = 0,
        Connecting = 1,
        InLobby = 2,
        JoiningRoom = 3,
        InRoom = 4,
        Reconnecting = 5,
        Failed = 6,
    }

    /// <summary>One room in the public room list (MP-2).</summary>
    public sealed class OnlineRoomListing
    {
        /// <summary>The room's code: what Join uses ("asia-K7PQ2X").</summary>
        public string Code = string.Empty;

        /// <summary>The name the host gave it.</summary>
        public string Title = string.Empty;

        /// <summary>The game the host has chosen (empty before one is chosen).</summary>
        public string Game = string.Empty;

        public int Players;
        public int MaxPlayers;
        public bool IsOpen;
    }

    /// <summary>How a new room is made (MP-2; owner, 2026-10-26: public rooms in the list, private ones by code).</summary>
    public sealed class OnlineRoomOptions
    {
        public string Title = string.Empty;
        public bool IsPrivate;
        public int MaxPlayers = 8;
    }

    /// <summary>
    /// Online play, without naming the networking product (doc 10 §14; the owner chose Photon Fusion, 2026-10-26).
    /// The product's code lives in its own assembly (ConsoleCards.Networking) and is the only code that sees its types.
    /// MP-0: connect and leave. MP-2: the room list, making and joining rooms by code, and messages between the
    /// computers in a room (bytes; what they mean is the table's business, not the product's). A connection is a
    /// number that stays the same while that computer stays in the room.
    /// </summary>
    public interface IMultiplayerSession
    {
        MultiplayerSessionState State { get; }

        /// <summary>What is happening, in plain words for the player ("Connected (asia). 3 rooms open.").</summary>
        string Status { get; }

        /// <summary>The server region in use, empty until connected.</summary>
        string Region { get; }

        /// <summary>Rooms in the room list (public rooms only).</summary>
        int VisibleRoomCount { get; }

        /// <summary>The public rooms in this region (updated while in the lobby).</summary>
        IReadOnlyList<OnlineRoomListing> Rooms { get; }

        /// <summary>True when this computer hosts the room.</summary>
        bool IsHost { get; }

        /// <summary>The room's code while in a room (give it to friends), else empty.</summary>
        string RoomCode { get; }

        /// <summary>The room's name while in a room, else empty.</summary>
        string RoomTitle { get; }

        /// <summary>True when the room is private (not in the list; joined by its code).</summary>
        bool RoomIsPrivate { get; }

        /// <summary>This computer's connection in the room (-1 when not in one).</summary>
        int LocalConnection { get; }

        /// <summary>Raised on the main thread whenever <see cref="State"/>, <see cref="Status"/> or the room list changes.</summary>
        event Action Changed;

        /// <summary>Host only: another computer came into the room.</summary>
        event Action<int> ConnectionJoined;

        /// <summary>Host only: a computer left the room (or lost its connection).</summary>
        event Action<int> ConnectionLeft;

        /// <summary>A message from another computer in the room: who sent it, and its bytes (a copy, kept by the reader).</summary>
        event Action<int, byte[]> MessageReceived;

        /// <summary>Connects to the cloud and the room list (nothing happens when already connected).</summary>
        void ConnectToLobby();

        /// <summary>Makes a room and hosts it.</summary>
        void CreateRoom(OnlineRoomOptions options);

        /// <summary>Joins a room by its code (from the list or from a friend).</summary>
        void JoinRoom(string code);

        /// <summary>Leaves the room and goes back to the room list.</summary>
        void LeaveRoom();

        /// <summary>Host only: what the room list shows about the room (the game chosen; open or full).</summary>
        void UpdateRoom(string game, bool open);

        /// <summary>Client: sends bytes to the host. Host: does nothing.</summary>
        void SendToHost(byte[] data, int offset, int count);

        /// <summary>Host: sends bytes to one computer in the room.</summary>
        void SendTo(int connection, byte[] data, int offset, int count);

        /// <summary>Host: closes one computer's connection (a room that is full, a different game version).</summary>
        void Disconnect(int connection);

        /// <summary>Leaves any room and the cloud.</summary>
        void Disconnect();
    }

    /// <summary>
    /// The one place the game finds online play. The networking assembly sets the factory when the game starts; the
    /// rest of the game only asks for <see cref="Current"/> and never names the product.
    /// </summary>
    public static class MultiplayerSessions
    {
        private static Func<IMultiplayerSession> factory;
        private static IMultiplayerSession current;

        /// <summary>True when a networking product is in the build.</summary>
        public static bool IsAvailable => factory != null;

        /// <summary>The session, made the first time it is asked for (null when no product is in the build).</summary>
        public static IMultiplayerSession Current
        {
            get
            {
                if (current == null && factory != null) current = factory();
                return current;
            }
        }

        /// <summary>Called once by the networking assembly at start-up.</summary>
        public static void SetFactory(Func<IMultiplayerSession> create)
        {
            factory = create;
        }

        /// <summary>Forgets the session (play mode starting again in the Editor).</summary>
        public static void Reset()
        {
            current?.Disconnect();
            current = null;
            factory = null;
        }
    }
}
