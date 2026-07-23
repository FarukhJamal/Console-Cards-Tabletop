using System;
using ConsoleCards.Application.Results;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct RotationInteractionResult : IEquatable<RotationInteractionResult>
    {
        private RotationInteractionResult(
            RotationInteractionStatus status,
            bool rotationAttempted,
            bool succeeded,
            RotateObjectResult? rotateResult)
        {
            Status = status;
            RotationAttempted = rotationAttempted;
            Succeeded = succeeded;
            RotateResult = rotateResult;
        }

        public RotationInteractionStatus Status { get; }

        public bool RotationAttempted { get; }

        public bool Succeeded { get; }

        public RotateObjectResult? RotateResult { get; }

        public static RotationInteractionResult NoSelection()
        {
            return new RotationInteractionResult(
                RotationInteractionStatus.NoSelection,
                false,
                false,
                null);
        }

        public static RotationInteractionResult SelectionUnavailable()
        {
            return new RotationInteractionResult(
                RotationInteractionStatus.SelectionUnavailable,
                false,
                false,
                null);
        }

        public static RotationInteractionResult NoRotationRequested()
        {
            return new RotationInteractionResult(
                RotationInteractionStatus.NoRotationRequested,
                false,
                false,
                null);
        }

        public static RotationInteractionResult ObjectUserLocked()
        {
            return new RotationInteractionResult(
                RotationInteractionStatus.ObjectUserLocked,
                false,
                false,
                null);
        }

        public static RotationInteractionResult LocalLockConflict()
        {
            return new RotationInteractionResult(
                RotationInteractionStatus.LocalLockConflict,
                false,
                false,
                null);
        }

        public static RotationInteractionResult FromRotateResult(RotateObjectResult result)
        {
            return new RotationInteractionResult(
                result.Succeeded
                    ? RotationInteractionStatus.RotationAccepted
                    : RotationInteractionStatus.RotationRejected,
                true,
                result.Succeeded,
                result);
        }

        public bool Equals(RotationInteractionResult other)
        {
            return Status == other.Status
                && RotationAttempted == other.RotationAttempted
                && Succeeded == other.Succeeded
                && Nullable.Equals(RotateResult, other.RotateResult);
        }

        public override bool Equals(object obj)
        {
            return obj is RotationInteractionResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, RotationAttempted, Succeeded, RotateResult);
        }

        public override string ToString()
        {
            return $"Status: {Status}, RotationAttempted: {RotationAttempted}, Succeeded: {Succeeded}, RotateResult: {RotateResult?.ToString() ?? "None"}";
        }

        public static bool operator ==(RotationInteractionResult left, RotationInteractionResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RotationInteractionResult left, RotationInteractionResult right)
        {
            return !left.Equals(right);
        }
    }
}
