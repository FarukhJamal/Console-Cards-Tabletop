using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Events
{
    public readonly struct DomainEventContext : IEquatable<DomainEventContext>
    {
        public DomainEventContext(MatchId matchId, long revision)
        {
            if (matchId.IsEmpty)
            {
                throw new ArgumentException("Match ID cannot be empty.", nameof(matchId));
            }

            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), "Revision cannot be below zero.");
            }

            MatchId = matchId;
            Revision = revision;
        }

        public MatchId MatchId { get; }

        public long Revision { get; }

        public bool Equals(DomainEventContext other)
        {
            return MatchId.Equals(other.MatchId)
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is DomainEventContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MatchId, Revision);
        }

        public override string ToString()
        {
            return $"MatchId: {MatchId}, Revision: {Revision}";
        }

        public static bool operator ==(DomainEventContext left, DomainEventContext right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DomainEventContext left, DomainEventContext right)
        {
            return !left.Equals(right);
        }
    }
}
