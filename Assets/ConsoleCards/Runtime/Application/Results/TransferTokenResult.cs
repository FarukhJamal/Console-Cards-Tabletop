using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct TransferTokenResult : IEquatable<TransferTokenResult>
    {
        private TransferTokenResult(CommandResult commandResult, TransferTokenError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public TransferTokenError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static TransferTokenResult Accepted(long revision)
        {
            return new TransferTokenResult(CommandResult.Accepted(revision), TransferTokenError.None);
        }

        public static TransferTokenResult Failure(CommandResultStatus status, TransferTokenError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Transfer Token failure must use a non-Accepted status.", nameof(status));
            }

            if (error == TransferTokenError.None)
            {
                throw new ArgumentException("Transfer Token failure must include an error.", nameof(error));
            }

            return new TransferTokenResult(CommandResult.Failure(status), error);
        }

        public bool Equals(TransferTokenResult other)
        {
            return CommandResult.Equals(other.CommandResult) && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is TransferTokenResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(TransferTokenResult left, TransferTokenResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TransferTokenResult left, TransferTokenResult right)
        {
            return !left.Equals(right);
        }
    }
}
