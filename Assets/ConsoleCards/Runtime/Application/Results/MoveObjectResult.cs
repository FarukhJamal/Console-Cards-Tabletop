using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct MoveObjectResult : IEquatable<MoveObjectResult>
    {
        private MoveObjectResult(CommandResult commandResult, MoveObjectError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public MoveObjectError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static MoveObjectResult Accepted(long revision)
        {
            return new MoveObjectResult(CommandResult.Accepted(revision), MoveObjectError.None);
        }

        public static MoveObjectResult Failure(CommandResultStatus status, MoveObjectError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Move object failure must use a non-Accepted status.", nameof(status));
            }

            if (error == MoveObjectError.None)
            {
                throw new ArgumentException("Move object failure must include an error.", nameof(error));
            }

            return new MoveObjectResult(CommandResult.Failure(status), error);
        }

        public bool Equals(MoveObjectResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is MoveObjectResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(MoveObjectResult left, MoveObjectResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MoveObjectResult left, MoveObjectResult right)
        {
            return !left.Equals(right);
        }
    }
}
