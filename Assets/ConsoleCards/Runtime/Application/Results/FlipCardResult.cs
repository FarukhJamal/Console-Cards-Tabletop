using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct FlipCardResult : IEquatable<FlipCardResult>
    {
        private FlipCardResult(CommandResult commandResult, FlipCardError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public FlipCardError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static FlipCardResult Accepted(long revision)
        {
            return new FlipCardResult(CommandResult.Accepted(revision), FlipCardError.None);
        }

        public static FlipCardResult Failure(CommandResultStatus status, FlipCardError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Flip card failure must use a non-Accepted status.", nameof(status));
            }

            if (error == FlipCardError.None)
            {
                throw new ArgumentException("Flip card failure must include an error.", nameof(error));
            }

            return new FlipCardResult(CommandResult.Failure(status), error);
        }

        public bool Equals(FlipCardResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is FlipCardResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(FlipCardResult left, FlipCardResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FlipCardResult left, FlipCardResult right)
        {
            return !left.Equals(right);
        }
    }
}
