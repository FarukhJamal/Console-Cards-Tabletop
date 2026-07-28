using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct ShuffleDeckResult : IEquatable<ShuffleDeckResult>
    {
        private ShuffleDeckResult(CommandResult commandResult, ShuffleDeckError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public ShuffleDeckError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static ShuffleDeckResult Accepted(long revision)
        {
            return new ShuffleDeckResult(CommandResult.Accepted(revision), ShuffleDeckError.None);
        }

        public static ShuffleDeckResult Failure(CommandResultStatus status, ShuffleDeckError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Shuffle deck failure must use a non-Accepted status.", nameof(status));
            }

            if (error == ShuffleDeckError.None)
            {
                throw new ArgumentException("Shuffle deck failure must include an error.", nameof(error));
            }

            return new ShuffleDeckResult(CommandResult.Failure(status), error);
        }

        public bool Equals(ShuffleDeckResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is ShuffleDeckResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(ShuffleDeckResult left, ShuffleDeckResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ShuffleDeckResult left, ShuffleDeckResult right)
        {
            return !left.Equals(right);
        }
    }
}
