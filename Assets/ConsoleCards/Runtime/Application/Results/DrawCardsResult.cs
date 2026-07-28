using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct DrawCardsResult : IEquatable<DrawCardsResult>
    {
        private DrawCardsResult(CommandResult commandResult, DrawCardsError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public DrawCardsError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static DrawCardsResult Accepted(long revision)
        {
            return new DrawCardsResult(CommandResult.Accepted(revision), DrawCardsError.None);
        }

        public static DrawCardsResult Failure(CommandResultStatus status, DrawCardsError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Draw cards failure must use a non-Accepted status.", nameof(status));
            }

            if (error == DrawCardsError.None)
            {
                throw new ArgumentException("Draw cards failure must include an error.", nameof(error));
            }

            return new DrawCardsResult(CommandResult.Failure(status), error);
        }

        public bool Equals(DrawCardsResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is DrawCardsResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(DrawCardsResult left, DrawCardsResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DrawCardsResult left, DrawCardsResult right)
        {
            return !left.Equals(right);
        }
    }
}
