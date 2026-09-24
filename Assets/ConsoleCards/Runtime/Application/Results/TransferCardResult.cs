using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Events;

namespace ConsoleCards.Application.Results
{
    public readonly struct TransferCardResult : IEquatable<TransferCardResult>
    {
        private static readonly IReadOnlyList<IConsoleCardInteraction> NoConsoleInteractions =
            Array.Empty<IConsoleCardInteraction>();
        private readonly IReadOnlyList<IConsoleCardInteraction> consoleInteractions;

        private TransferCardResult(
            CommandResult commandResult,
            TransferCardError error,
            IReadOnlyList<IConsoleCardInteraction> consoleInteractions)
        {
            CommandResult = commandResult;
            Error = error;
            this.consoleInteractions = consoleInteractions ?? NoConsoleInteractions;
        }

        public CommandResult CommandResult { get; }

        public TransferCardError Error { get; }

        public IReadOnlyList<IConsoleCardInteraction> ConsoleInteractions =>
            consoleInteractions ?? NoConsoleInteractions;

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static TransferCardResult Accepted(
            long revision,
            IEnumerable<IConsoleCardInteraction> consoleInteractions = null)
        {
            IReadOnlyList<IConsoleCardInteraction> copiedInteractions =
                CopyConsoleInteractions(consoleInteractions, revision);
            return new TransferCardResult(
                CommandResult.Accepted(revision),
                TransferCardError.None,
                copiedInteractions);
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

            return new TransferCardResult(
                CommandResult.Failure(status),
                error,
                NoConsoleInteractions);
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
            return $"CommandResult: {CommandResult}, Error: {Error}, ConsoleInteractions: {ConsoleInteractions.Count}";
        }

        public static bool operator ==(TransferCardResult left, TransferCardResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TransferCardResult left, TransferCardResult right)
        {
            return !left.Equals(right);
        }

        private static IReadOnlyList<IConsoleCardInteraction> CopyConsoleInteractions(
            IEnumerable<IConsoleCardInteraction> interactions,
            long revision)
        {
            if (interactions == null)
            {
                return NoConsoleInteractions;
            }

            List<IConsoleCardInteraction> copied = new List<IConsoleCardInteraction>();
            foreach (IConsoleCardInteraction interaction in interactions)
            {
                if (interaction == null)
                {
                    throw new ArgumentException("Console interactions cannot contain null entries.", nameof(interactions));
                }

                if (interaction.AcceptedRevision != revision)
                {
                    throw new ArgumentException(
                        "Console interaction revision must match the accepted transfer revision.",
                        nameof(interactions));
                }

                copied.Add(interaction);
            }

            return copied.Count == 0
                ? NoConsoleInteractions
                : new ReadOnlyCollection<IConsoleCardInteraction>(copied);
        }
    }
}
