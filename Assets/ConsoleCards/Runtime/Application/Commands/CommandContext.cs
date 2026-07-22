using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public readonly struct CommandContext : IEquatable<CommandContext>
    {
        public CommandContext(
            CommandId id,
            MatchId matchId,
            PlayerId requestedByPlayerId,
            long? expectedRevision)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Command ID cannot be empty.", nameof(id));
            }

            if (matchId.IsEmpty)
            {
                throw new ArgumentException("Match ID cannot be empty.", nameof(matchId));
            }

            if (requestedByPlayerId.IsEmpty)
            {
                throw new ArgumentException("Requested by Player ID cannot be empty.", nameof(requestedByPlayerId));
            }

            if (expectedRevision.HasValue && expectedRevision.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedRevision), "Expected revision cannot be below zero.");
            }

            Id = id;
            MatchId = matchId;
            RequestedByPlayerId = requestedByPlayerId;
            ExpectedRevision = expectedRevision;
        }

        public CommandId Id { get; }

        public MatchId MatchId { get; }

        public PlayerId RequestedByPlayerId { get; }

        public long? ExpectedRevision { get; }

        public bool Equals(CommandContext other)
        {
            return Id.Equals(other.Id)
                && MatchId.Equals(other.MatchId)
                && RequestedByPlayerId.Equals(other.RequestedByPlayerId)
                && ExpectedRevision == other.ExpectedRevision;
        }

        public override bool Equals(object obj)
        {
            return obj is CommandContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, MatchId, RequestedByPlayerId, ExpectedRevision);
        }

        public override string ToString()
        {
            return $"Id: {Id}, MatchId: {MatchId}, RequestedByPlayerId: {RequestedByPlayerId}, ExpectedRevision: {ExpectedRevision?.ToString() ?? "None"}";
        }

        public static bool operator ==(CommandContext left, CommandContext right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandContext left, CommandContext right)
        {
            return !left.Equals(right);
        }
    }
}
