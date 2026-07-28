using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct ReorderContainerResult : IEquatable<ReorderContainerResult>
    {
        private ReorderContainerResult(CommandResult commandResult, ReorderContainerError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public ReorderContainerError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static ReorderContainerResult Accepted(long revision)
        {
            return new ReorderContainerResult(CommandResult.Accepted(revision), ReorderContainerError.None);
        }

        public static ReorderContainerResult Failure(CommandResultStatus status, ReorderContainerError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Reorder container failure must use a non-Accepted status.", nameof(status));
            }

            if (error == ReorderContainerError.None)
            {
                throw new ArgumentException("Reorder container failure must include an error.", nameof(error));
            }

            return new ReorderContainerResult(CommandResult.Failure(status), error);
        }

        public bool Equals(ReorderContainerResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is ReorderContainerResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(ReorderContainerResult left, ReorderContainerResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ReorderContainerResult left, ReorderContainerResult right)
        {
            return !left.Equals(right);
        }
    }
}
