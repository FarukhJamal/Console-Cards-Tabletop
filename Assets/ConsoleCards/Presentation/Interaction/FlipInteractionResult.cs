using System;
using ConsoleCards.Application.Results;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct FlipInteractionResult : IEquatable<FlipInteractionResult>
    {
        private FlipInteractionResult(
            FlipInteractionStatus status,
            bool flipAttempted,
            bool succeeded,
            FlipCardResult? flipResult)
        {
            Status = status;
            FlipAttempted = flipAttempted;
            Succeeded = succeeded;
            FlipResult = flipResult;
        }

        public FlipInteractionStatus Status { get; }

        public bool FlipAttempted { get; }

        public bool Succeeded { get; }

        public FlipCardResult? FlipResult { get; }

        public static FlipInteractionResult NoSelection()
        {
            return new FlipInteractionResult(
                FlipInteractionStatus.NoSelection,
                false,
                false,
                null);
        }

        public static FlipInteractionResult SelectionUnavailable()
        {
            return new FlipInteractionResult(
                FlipInteractionStatus.SelectionUnavailable,
                false,
                false,
                null);
        }

        public static FlipInteractionResult SelectionNotCard()
        {
            return new FlipInteractionResult(
                FlipInteractionStatus.SelectionNotCard,
                false,
                false,
                null);
        }

        public static FlipInteractionResult ObjectUserLocked()
        {
            return new FlipInteractionResult(
                FlipInteractionStatus.ObjectUserLocked,
                false,
                false,
                null);
        }

        public static FlipInteractionResult LocalLockConflict()
        {
            return new FlipInteractionResult(
                FlipInteractionStatus.LocalLockConflict,
                false,
                false,
                null);
        }

        public static FlipInteractionResult FromFlipResult(FlipCardResult result)
        {
            return new FlipInteractionResult(
                result.Succeeded
                    ? FlipInteractionStatus.FlipAccepted
                    : FlipInteractionStatus.FlipRejected,
                true,
                result.Succeeded,
                result);
        }

        public bool Equals(FlipInteractionResult other)
        {
            return Status == other.Status
                && FlipAttempted == other.FlipAttempted
                && Succeeded == other.Succeeded
                && Nullable.Equals(FlipResult, other.FlipResult);
        }

        public override bool Equals(object obj)
        {
            return obj is FlipInteractionResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, FlipAttempted, Succeeded, FlipResult);
        }

        public override string ToString()
        {
            return $"Status: {Status}, FlipAttempted: {FlipAttempted}, Succeeded: {Succeeded}, FlipResult: {FlipResult?.ToString() ?? "None"}";
        }

        public static bool operator ==(FlipInteractionResult left, FlipInteractionResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FlipInteractionResult left, FlipInteractionResult right)
        {
            return !left.Equals(right);
        }
    }
}
