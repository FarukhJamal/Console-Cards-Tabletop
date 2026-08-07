using System;
using ConsoleCards.Core.Coordinates;

namespace ConsoleCards.Core.Domain.PlayerLayouts
{
    public sealed class PlayerSeatLayoutEntry
    {
        public PlayerSeatLayoutEntry(
            int seatIndex,
            TabletopPose playerZonePose,
            TabletopPose handAnchorPose,
            TabletopPose consoleAnchorPose,
            float facingRotationDegrees)
        {
            if (seatIndex < 0 || seatIndex >= PlayerLayoutDefinition.MaximumSeatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(seatIndex));
            }

            ValidateFinite(playerZonePose, nameof(playerZonePose));
            ValidateFinite(handAnchorPose, nameof(handAnchorPose));
            ValidateFinite(consoleAnchorPose, nameof(consoleAnchorPose));
            if (!IsFinite(facingRotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(facingRotationDegrees));
            }

            SeatIndex = seatIndex;
            PlayerZonePose = playerZonePose;
            HandAnchorPose = handAnchorPose;
            ConsoleAnchorPose = consoleAnchorPose;
            FacingRotationDegrees = facingRotationDegrees;
        }

        public int SeatIndex { get; }

        public TabletopPose PlayerZonePose { get; }

        public TabletopPose HandAnchorPose { get; }

        public TabletopPose ConsoleAnchorPose { get; }

        public float FacingRotationDegrees { get; }

        private static void ValidateFinite(TabletopPose pose, string parameterName)
        {
            if (!IsFinite(pose.Position.X)
                || !IsFinite(pose.Position.Y)
                || !IsFinite(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Player layout poses must be finite.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
