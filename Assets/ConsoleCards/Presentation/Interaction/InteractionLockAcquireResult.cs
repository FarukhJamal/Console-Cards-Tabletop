using System;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct InteractionLockAcquireResult : IEquatable<InteractionLockAcquireResult>
    {
        private InteractionLockAcquireResult(
            InteractionLockAcquireStatus status,
            InteractionOwnerId currentOwnerId)
        {
            Status = status;
            CurrentOwnerId = currentOwnerId;
        }

        public InteractionLockAcquireStatus Status { get; }

        public bool Succeeded => Status == InteractionLockAcquireStatus.Acquired
            || Status == InteractionLockAcquireStatus.AlreadyOwned;

        public InteractionOwnerId CurrentOwnerId { get; }

        public static InteractionLockAcquireResult Acquired(InteractionOwnerId ownerId)
        {
            ValidateOwnerId(ownerId, nameof(ownerId));
            return new InteractionLockAcquireResult(InteractionLockAcquireStatus.Acquired, ownerId);
        }

        public static InteractionLockAcquireResult AlreadyOwned(InteractionOwnerId ownerId)
        {
            ValidateOwnerId(ownerId, nameof(ownerId));
            return new InteractionLockAcquireResult(InteractionLockAcquireStatus.AlreadyOwned, ownerId);
        }

        public static InteractionLockAcquireResult Conflict(InteractionOwnerId currentOwnerId)
        {
            ValidateOwnerId(currentOwnerId, nameof(currentOwnerId));
            return new InteractionLockAcquireResult(InteractionLockAcquireStatus.Conflict, currentOwnerId);
        }

        public bool Equals(InteractionLockAcquireResult other)
        {
            return Status == other.Status
                && CurrentOwnerId.Equals(other.CurrentOwnerId);
        }

        public override bool Equals(object obj)
        {
            return obj is InteractionLockAcquireResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, CurrentOwnerId);
        }

        public override string ToString()
        {
            return $"Status: {Status}, Succeeded: {Succeeded}, CurrentOwnerId: {CurrentOwnerId}";
        }

        public static bool operator ==(InteractionLockAcquireResult left, InteractionLockAcquireResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InteractionLockAcquireResult left, InteractionLockAcquireResult right)
        {
            return !left.Equals(right);
        }

        private static void ValidateOwnerId(InteractionOwnerId ownerId, string parameterName)
        {
            if (ownerId.IsEmpty)
            {
                throw new ArgumentException("Interaction owner ID cannot be empty.", parameterName);
            }
        }
    }
}
