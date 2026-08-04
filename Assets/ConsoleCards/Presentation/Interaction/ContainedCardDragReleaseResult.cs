using System;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct ContainedCardDragReleaseResult : IEquatable<ContainedCardDragReleaseResult>
    {
        private ContainedCardDragReleaseResult(
            ContainedCardDragReleaseStatus status,
            bool transferAttempted,
            bool succeeded,
            CardTransferInteractionResult? transferResult)
        {
            Status = status;
            TransferAttempted = transferAttempted;
            Succeeded = succeeded;
            TransferResult = transferResult;
        }

        public ContainedCardDragReleaseStatus Status { get; }

        public bool TransferAttempted { get; }

        public bool Succeeded { get; }

        public CardTransferInteractionResult? TransferResult { get; }

        public static ContainedCardDragReleaseResult ClickReleased()
        {
            return NotAttempted(ContainedCardDragReleaseStatus.ClickReleased, true);
        }

        public static ContainedCardDragReleaseResult NoTarget()
        {
            return NotAttempted(ContainedCardDragReleaseStatus.NoTarget, true);
        }

        public static ContainedCardDragReleaseResult SameSource()
        {
            return NotAttempted(ContainedCardDragReleaseStatus.SameSource, true);
        }

        public static ContainedCardDragReleaseResult ProjectionFailed()
        {
            return NotAttempted(ContainedCardDragReleaseStatus.ProjectionFailed, false);
        }

        public static ContainedCardDragReleaseResult Cancelled()
        {
            return NotAttempted(ContainedCardDragReleaseStatus.Cancelled, true);
        }

        public static ContainedCardDragReleaseResult FromTransferResult(CardTransferInteractionResult transferResult)
        {
            if (!transferResult.TransferAttempted)
            {
                throw new ArgumentException("Contained drag transfer result requires an attempted transfer.", nameof(transferResult));
            }

            return transferResult.Succeeded
                ? TransferAccepted(transferResult)
                : TransferRejected(transferResult);
        }

        public static ContainedCardDragReleaseResult TransferAccepted(CardTransferInteractionResult transferResult)
        {
            if (!transferResult.TransferAttempted || !transferResult.Succeeded)
            {
                throw new ArgumentException("Accepted contained drag release requires an accepted transfer result.", nameof(transferResult));
            }

            return new ContainedCardDragReleaseResult(
                ContainedCardDragReleaseStatus.TransferAccepted,
                true,
                true,
                transferResult);
        }

        public static ContainedCardDragReleaseResult TransferRejected(CardTransferInteractionResult transferResult)
        {
            if (!transferResult.TransferAttempted || transferResult.Succeeded)
            {
                throw new ArgumentException("Rejected contained drag release requires a failed transfer result.", nameof(transferResult));
            }

            return new ContainedCardDragReleaseResult(
                ContainedCardDragReleaseStatus.TransferRejected,
                true,
                false,
                transferResult);
        }

        public bool Equals(ContainedCardDragReleaseResult other)
        {
            return Status == other.Status
                && TransferAttempted == other.TransferAttempted
                && Succeeded == other.Succeeded
                && Nullable.Equals(TransferResult, other.TransferResult);
        }

        public override bool Equals(object obj)
        {
            return obj is ContainedCardDragReleaseResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, TransferAttempted, Succeeded, TransferResult);
        }

        public override string ToString()
        {
            return $"Status: {Status}, TransferAttempted: {TransferAttempted}, Succeeded: {Succeeded}, TransferResult: {TransferResult?.ToString() ?? "None"}";
        }

        public static bool operator ==(ContainedCardDragReleaseResult left, ContainedCardDragReleaseResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContainedCardDragReleaseResult left, ContainedCardDragReleaseResult right)
        {
            return !left.Equals(right);
        }

        private static ContainedCardDragReleaseResult NotAttempted(
            ContainedCardDragReleaseStatus status,
            bool succeeded)
        {
            if (status == ContainedCardDragReleaseStatus.TransferAccepted
                || status == ContainedCardDragReleaseStatus.TransferRejected)
            {
                throw new ArgumentException("Transfer statuses require a transfer result.", nameof(status));
            }

            return new ContainedCardDragReleaseResult(status, false, succeeded, null);
        }
    }
}
