using System;
using ConsoleCards.Application.Results;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct MoveInteractionReleaseResult : IEquatable<MoveInteractionReleaseResult>
    {
        private MoveInteractionReleaseResult(
            MoveInteractionReleaseStatus status,
            bool movementAttempted,
            bool succeeded,
            MoveObjectResult? moveResult)
        {
            Status = status;
            MovementAttempted = movementAttempted;
            Succeeded = succeeded;
            MoveResult = moveResult;
        }

        public MoveInteractionReleaseStatus Status { get; }

        public bool MovementAttempted { get; }

        public bool Succeeded { get; }

        public MoveObjectResult? MoveResult { get; }

        public static MoveInteractionReleaseResult ClickCompleted()
        {
            return new MoveInteractionReleaseResult(
                MoveInteractionReleaseStatus.ClickCompleted,
                false,
                true,
                null);
        }

        public static MoveInteractionReleaseResult ProjectionFailed()
        {
            return new MoveInteractionReleaseResult(
                MoveInteractionReleaseStatus.ProjectionFailed,
                false,
                false,
                null);
        }

        public static MoveInteractionReleaseResult FromMoveResult(MoveObjectResult result)
        {
            return new MoveInteractionReleaseResult(
                result.Succeeded
                    ? MoveInteractionReleaseStatus.MoveAccepted
                    : MoveInteractionReleaseStatus.MoveRejected,
                true,
                result.Succeeded,
                result);
        }

        public bool Equals(MoveInteractionReleaseResult other)
        {
            return Status == other.Status
                && MovementAttempted == other.MovementAttempted
                && Succeeded == other.Succeeded
                && Nullable.Equals(MoveResult, other.MoveResult);
        }

        public override bool Equals(object obj)
        {
            return obj is MoveInteractionReleaseResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, MovementAttempted, Succeeded, MoveResult);
        }

        public override string ToString()
        {
            return $"Status: {Status}, MovementAttempted: {MovementAttempted}, Succeeded: {Succeeded}, MoveResult: {MoveResult?.ToString() ?? "None"}";
        }

        public static bool operator ==(MoveInteractionReleaseResult left, MoveInteractionReleaseResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MoveInteractionReleaseResult left, MoveInteractionReleaseResult right)
        {
            return !left.Equals(right);
        }
    }
}
