using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct SplitStackResult : IEquatable<SplitStackResult>
    {
        private SplitStackResult(CommandResult commandResult, SplitStackError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public SplitStackError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static SplitStackResult Accepted(long revision)
        {
            return new SplitStackResult(CommandResult.Accepted(revision), SplitStackError.None);
        }

        public static SplitStackResult Failure(CommandResultStatus status, SplitStackError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Split stack failure must use a non-Accepted status.", nameof(status));
            }

            if (error == SplitStackError.None)
            {
                throw new ArgumentException("Split stack failure must include an error.", nameof(error));
            }

            return new SplitStackResult(CommandResult.Failure(status), error);
        }

        public bool Equals(SplitStackResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is SplitStackResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(SplitStackResult left, SplitStackResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SplitStackResult left, SplitStackResult right)
        {
            return !left.Equals(right);
        }
    }
}
