using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Authority
{
    /// <summary>What the host decided about one streamed pose of a piece in a player's hand.</summary>
    public enum PoseRelayVerdict
    {
        Allowed = 0,

        /// <summary>The connection plays no seat (a watcher, or not identified).</summary>
        NoSeat = 1,

        /// <summary>No such piece on the host's table.</summary>
        UnknownObject = 2,

        /// <summary>The piece lies in a container (a Hand, a deck, a slot): it has no loose pose to stream.</summary>
        Contained = 3,

        /// <summary>The host's table has the piece not held (or with no physical state).</summary>
        NotHeld = 4,

        /// <summary>The piece is held, but by another player than the one this connection plays.</summary>
        NotController = 5,

        /// <summary>Not a finite position and rotation near the table.</summary>
        InvalidPose = 6,
    }

    /// <summary>
    /// P8A STAGE-A PHY-6 (owner, 2026-11-13: "fix sender authorization at the host boundary"): a pose streamed for a
    /// piece in someone's hand is used and passed on only when the connection it came on plays the seat whose player
    /// holds that piece on the host's own table. The sender's player comes from the connection (the host's member
    /// record), never from the message: a pose message carries only piece IDs and numbers. Pure and allocation-free,
    /// so it is checked per pose, ten times a second.
    /// </summary>
    public static class PoseRelayPolicy
    {
        /// <summary>Further than this from the table's centre (world units) is no pose on any table.</summary>
        public const float MaxDistance = 10000f;

        public static PoseRelayVerdict Check(
            MatchState hostTable,
            PlayerId connectionPlayer,
            TabletopObjectId objectId,
            float px, float py, float pz,
            float rx, float ry, float rz, float rw)
        {
            if (connectionPlayer.IsEmpty) return PoseRelayVerdict.NoSeat;
            if (!Sane(px) || !Sane(py) || !Sane(pz) || !Finite(rx) || !Finite(ry) || !Finite(rz) || !Finite(rw))
                return PoseRelayVerdict.InvalidPose;
            float norm = (rx * rx) + (ry * ry) + (rz * rz) + (rw * rw);
            if (norm < 0.5f || norm > 1.5f) return PoseRelayVerdict.InvalidPose; // not a rotation
            if (hostTable == null || objectId.IsEmpty || !hostTable.ContainsObject(objectId)) return PoseRelayVerdict.UnknownObject;
            TabletopObjectState piece = hostTable.GetObject(objectId);
            if (!piece.ContainerId.IsEmpty) return PoseRelayVerdict.Contained;
            PhysicalObjectState physical = piece.PhysicalState;
            if (physical == null || physical.Mode != PhysicalObjectMode.Held) return PoseRelayVerdict.NotHeld;
            return physical.ControllingPlayerId == connectionPlayer ? PoseRelayVerdict.Allowed : PoseRelayVerdict.NotController;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Sane(float value) => Finite(value) && Math.Abs(value) <= MaxDistance;
    }
}
