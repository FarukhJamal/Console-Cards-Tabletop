using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct MergeStacksResult : IEquatable<MergeStacksResult>
    {
        private MergeStacksResult(CommandResult commandResult, MergeStacksError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public MergeStacksError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static MergeStacksResult Accepted(long revision)
        {
            return new MergeStacksResult(CommandResult.Accepted(revision), MergeStacksError.None);
        }

        public static MergeStacksResult Failure(CommandResultStatus status, MergeStacksError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Merge stacks failure must use a non-Accepted status.", nameof(status));
            }

            if (error == MergeStacksError.None)
            {
                throw new ArgumentException("Merge stacks failure must include an error.", nameof(error));
            }

            return new MergeStacksResult(CommandResult.Failure(status), error);
        }

        public bool Equals(MergeStacksResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is MergeStacksResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(MergeStacksResult left, MergeStacksResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MergeStacksResult left, MergeStacksResult right)
        {
            return !left.Equals(right);
        }
    }
}
