using System;

namespace ConsoleCards.Application.Results
{
    public readonly struct TransferCardResult : IEquatable<TransferCardResult>
    {
        private TransferCardResult(CommandResult commandResult, TransferCardError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public TransferCardError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static TransferCardResult Accepted(long revision)
        {
            return new TransferCardResult(CommandResult.Accepted(revision), TransferCardError.None);
        }

        public static TransferCardResult Failure(CommandResultStatus status, TransferCardError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Transfer card failure must use a non-Accepted status.", nameof(status));
            }

            if (error == TransferCardError.None)
            {
                throw new ArgumentException("Transfer card failure must include an error.", nameof(error));
            }

            return new TransferCardResult(CommandResult.Failure(status), error);
        }

        public bool Equals(TransferCardResult other)
        {
            return CommandResult.Equals(other.CommandResult)
                && Error == other.Error;
        }

        public override bool Equals(object obj)
        {
            return obj is TransferCardResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandResult, Error);
        }

        public override string ToString()
        {
            return $"CommandResult: {CommandResult}, Error: {Error}";
        }

        public static bool operator ==(TransferCardResult left, TransferCardResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TransferCardResult left, TransferCardResult right)
        {
            return !left.Equals(right);
        }
    }
}
