using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct RotateObjectResult : IEquatable<RotateObjectResult>
    {
        private RotateObjectResult(CommandResult commandResult, RotateObjectError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public RotateObjectError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static RotateObjectResult Accepted(long revision)
        {
            return new RotateObjectResult(CommandResult.Accepted(revision), RotateObjectError.None);
        }

        public static RotateObjectResult Failure(CommandResultStatus status, RotateObjectError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Rotate object failure must use a non-Accepted status.", nameof(status));
            }

            if (error == RotateObjectError.None)
            {
                throw new ArgumentException("Rotate object failure must include an error.", nameof(error));
            }

            return new RotateObjectResult(CommandResult.Failure(status), error);
        }

        public bool Equals(RotateObjectResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is RotateObjectResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(RotateObjectResult left, RotateObjectResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RotateObjectResult left, RotateObjectResult right)
        {
            return !left.Equals(right);
        }
    }
}
