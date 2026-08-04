using System;
using ConsoleCards.Application.Results;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct CardTransferInteractionResult : IEquatable<CardTransferInteractionResult>
    {
        private CardTransferInteractionResult(
            CardTransferInteractionStatus status,
            bool transferAttempted,
            bool succeeded,
            TransferCardResult? transferResult)
        {
            Status = status;
            TransferAttempted = transferAttempted;
            Succeeded = succeeded;
            TransferResult = transferResult;
        }

        public CardTransferInteractionStatus Status { get; }

        public bool TransferAttempted { get; }

        public bool Succeeded { get; }

        public TransferCardResult? TransferResult { get; }

        public static CardTransferInteractionResult NoTarget()
        {
            return NotAttempted(CardTransferInteractionStatus.NoTarget);
        }

        public static CardTransferInteractionResult CardUnavailable()
        {
            return NotAttempted(CardTransferInteractionStatus.CardUnavailable);
        }

        public static CardTransferInteractionResult CardNotTransferable()
        {
            return NotAttempted(CardTransferInteractionStatus.CardNotTransferable);
        }

        public static CardTransferInteractionResult SameLocation()
        {
            return NotAttempted(CardTransferInteractionStatus.SameLocation);
        }

        public static CardTransferInteractionResult SourceLayoutUnavailable()
        {
            return NotAttempted(CardTransferInteractionStatus.SourceLayoutUnavailable);
        }

        public static CardTransferInteractionResult DestinationLayoutUnavailable()
        {
            return NotAttempted(CardTransferInteractionStatus.DestinationLayoutUnavailable);
        }

        public static CardTransferInteractionResult LocalLockConflict()
        {
            return NotAttempted(CardTransferInteractionStatus.LocalLockConflict);
        }

        public static CardTransferInteractionResult FromTransferResult(TransferCardResult result)
        {
            return result.Succeeded
                ? TransferAccepted(result)
                : TransferRejected(result);
        }

        public static CardTransferInteractionResult TransferAccepted(TransferCardResult result)
        {
            if (!result.Succeeded)
            {
                throw new ArgumentException("Accepted transfer interaction requires an accepted TransferCardResult.", nameof(result));
            }

            return new CardTransferInteractionResult(
                CardTransferInteractionStatus.TransferAccepted,
                true,
                true,
                result);
        }

        public static CardTransferInteractionResult TransferRejected(TransferCardResult result)
        {
            if (result.Succeeded)
            {
                throw new ArgumentException("Rejected transfer interaction requires a failed TransferCardResult.", nameof(result));
            }

            return new CardTransferInteractionResult(
                CardTransferInteractionStatus.TransferRejected,
                true,
                false,
                result);
        }

        public bool Equals(CardTransferInteractionResult other)
        {
            return Status == other.Status
                && TransferAttempted == other.TransferAttempted
                && Succeeded == other.Succeeded
                && Nullable.Equals(TransferResult, other.TransferResult);
        }

        public override bool Equals(object obj)
        {
            return obj is CardTransferInteractionResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, TransferAttempted, Succeeded, TransferResult);
        }

        public override string ToString()
        {
            return $"Status: {Status}, TransferAttempted: {TransferAttempted}, Succeeded: {Succeeded}, TransferResult: {TransferResult?.ToString() ?? "None"}";
        }

        public static bool operator ==(CardTransferInteractionResult left, CardTransferInteractionResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CardTransferInteractionResult left, CardTransferInteractionResult right)
        {
            return !left.Equals(right);
        }

        private static CardTransferInteractionResult NotAttempted(CardTransferInteractionStatus status)
        {
            if (status == CardTransferInteractionStatus.TransferAccepted
                || status == CardTransferInteractionStatus.TransferRejected)
            {
                throw new ArgumentException("Transfer result statuses require a TransferCardResult.", nameof(status));
            }

            return new CardTransferInteractionResult(status, false, false, null);
        }
    }
}
