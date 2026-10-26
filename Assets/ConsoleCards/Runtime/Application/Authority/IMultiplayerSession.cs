using System;

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

    /// <summary>
    /// Online play, without naming the networking product (doc 10 §14; the owner chose Photon Fusion, 2026-10-26).
    /// The product's code lives in its own assembly (ConsoleCards.Networking) and is the only code that sees its
    /// types. MP-0: connect to the room list and leave. Rooms, the table's messages and rejoining are added by the
    /// later stages (MP-2, MP-3, MP-5).
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

        /// <summary>True when this computer hosts the room (its table authority is the table's).</summary>
        bool IsHost { get; }

        /// <summary>Raised on the main thread whenever <see cref="State"/>, <see cref="Status"/> or the room list changes.</summary>
        event Action Changed;

        /// <summary>Connects to the Photon cloud and the room list (nothing happens when already connected).</summary>
        void ConnectToLobby();

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
