using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// P8A (owner, 2026-11-13 QA: "I tried to move a defeated player's pawn … it's being rejected, which is fine. But
    /// when you do that, the hand cards on the table and in front of you start glitching"). Trace (host Editor log):
    /// every refused commit of that drag (the release and each checkpoint after it, 4–5 per drop) was answered with the
    /// host's WHOLE table, and a whole table rebuilds the joined computer's entire table (every loose card and piece is
    /// made again). The refused move only changed one piece there, so the host now sends that one piece's record (its
    /// physical state and physical revision, which is what the table check compares) and the joined computer puts it
    /// back. Nothing else on its table is touched. A loose piece only: no card identity, no Hand contents.
    /// </summary>
    public sealed class PieceCorrection
    {
        public PieceCorrection(TabletopObjectId objectId, PhysicalObjectState state, long physicalRevision)
        {
            ObjectId = objectId;
            State = state;
            PhysicalRevision = physicalRevision;
        }

        public TabletopObjectId ObjectId { get; }

        public PhysicalObjectState State { get; }

        public long PhysicalRevision { get; }

        /// <summary>The host's record of a loose piece; false for a piece in a container or with no physical state.</summary>
        public static bool TryCapture(MatchState hostTable, TabletopObjectId objectId, out PieceCorrection correction)
        {
            correction = null;
            if (hostTable == null || !hostTable.ContainsObject(objectId)) return false;
            TabletopObjectState piece = hostTable.GetObject(objectId);
            if (!piece.ContainerId.IsEmpty || piece.PhysicalState == null) return false;
            correction = new PieceCorrection(objectId, piece.PhysicalState, piece.PhysicalRevision);
            return true;
        }

        /// <summary>
        /// A joined computer takes it. False (nothing changed) when the piece is not loose here: then only the whole
        /// table can set it right.
        /// </summary>
        public bool ApplyTo(MatchState table)
        {
            if (table == null || State == null || PhysicalRevision < 0 || !table.ContainsObject(ObjectId)) return false;
            TabletopObjectState piece = table.GetObject(ObjectId);
            if (!piece.ContainerId.IsEmpty) return false;
            piece.CorrectPhysicalState(State, PhysicalRevision);
            return true;
        }
    }
}
