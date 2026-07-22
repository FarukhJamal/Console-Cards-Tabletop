using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct CommandResult : IEquatable<CommandResult>
    {
        private CommandResult(CommandResultStatus status, bool succeeded, long revision)
        {
            Status = status;
            Succeeded = succeeded;
            Revision = revision;
        }

        public CommandResultStatus Status { get; }

        public bool Succeeded { get; }

        public long Revision { get; }

        public static CommandResult Accepted(long revision)
        {
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), "Revision cannot be below zero.");
            }

            return new CommandResult(CommandResultStatus.Accepted, true, revision);
        }

        public static CommandResult Failure(CommandResultStatus status)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Failure must use a non-Accepted status.", nameof(status));
            }

            return new CommandResult(status, false, -1);
        }

        public bool Equals(CommandResult other)
        {
            return Status == other.Status
                && Succeeded == other.Succeeded
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is CommandResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, Succeeded, Revision);
        }

        public override string ToString()
        {
            return $"Status: {Status}, Succeeded: {Succeeded}, Revision: {Revision}";
        }

        public static bool operator ==(CommandResult left, CommandResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandResult left, CommandResult right)
        {
            return !left.Equals(right);
        }
    }
}
