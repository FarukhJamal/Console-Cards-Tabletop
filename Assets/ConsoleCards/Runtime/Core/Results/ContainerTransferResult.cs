using System;

namespace ConsoleCards.Core.Results
{
    public readonly struct ContainerTransferResult : IEquatable<ContainerTransferResult>
    {
        private ContainerTransferResult(
            bool succeeded,
            ContainerTransferError error,
            int destinationIndex)
        {
            Succeeded = succeeded;
            Error = error;
            DestinationIndex = destinationIndex;
        }

        public bool Succeeded { get; }

        public ContainerTransferError Error { get; }

        public int DestinationIndex { get; }

        public static ContainerTransferResult Success(int destinationIndex)
        {
            return new ContainerTransferResult(true, ContainerTransferError.None, destinationIndex);
        }

        public static ContainerTransferResult Failure(ContainerTransferError error)
        {
            if (error == ContainerTransferError.None)
            {
                throw new ArgumentException("Failure must use an error other than None.", nameof(error));
            }

            return new ContainerTransferResult(false, error, -1);
        }

        public bool Equals(ContainerTransferResult other)
        {
            return Succeeded == other.Succeeded
                && Error == other.Error
                && DestinationIndex == other.DestinationIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is ContainerTransferResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Succeeded, Error, DestinationIndex);
        }

        public static bool operator ==(ContainerTransferResult left, ContainerTransferResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContainerTransferResult left, ContainerTransferResult right)
        {
            return !left.Equals(right);
        }
    }
}
