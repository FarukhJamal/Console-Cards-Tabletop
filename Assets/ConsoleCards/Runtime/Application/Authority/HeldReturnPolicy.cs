using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// P8A (owner, 2026-11-13 live test: "I tried to move another computer's pawn … the pawn was floating in the air,
    /// and the marker that shows where the pawn can move doesn't go"). Trace (host Editor log): the guest picked up the
    /// host's pawn; the host accepted the HOLD (a hold is never checked against the move rules), then refused every
    /// release and settle that followed ("Pawn move refused (Full, NotYourPawn)" ×12). The host's table, and every
    /// table it was sent to, kept the pawn HELD by the guest: no gravity (floating where the hand left it), the reach
    /// lights of a held turn pawn stayed on, and only the guest's computer could ever commit it again.
    /// <para>
    /// The host now remembers where a piece rested when a guest's hold of it was accepted. When the host refuses that
    /// guest's next commit of the piece while the host's table still has it held by that guest, the host puts it back
    /// where it rested, with one command requested in the holder's name (the only player the table lets end the hold),
    /// at rest, sent to every computer. A refused move thus never leaves a piece held. Nothing about who may move what
    /// changes: the refusal is the same as before; only its leftover hold is undone.
    /// </para>
    /// </summary>
    public sealed class HeldReturnPolicy
    {
        private readonly Dictionary<TabletopObjectId, PhysicalObjectState> restBeforeHold = new Dictionary<TabletopObjectId, PhysicalObjectState>();
        private MatchState rememberedFor;

        /// <summary>How many pieces were put back after a refused move (diagnostics).</summary>
        public int Returned { get; private set; }

        /// <summary>
        /// After the host accepted a commit: a new hold remembers the state before it; anything else that is not a hold
        /// forgets the piece (it is no longer held).
        /// </summary>
        public void NoteAccepted(MatchState match, TabletopObjectId objectId, PhysicalObjectState before, PhysicalObjectState after)
        {
            if (!ReferenceEquals(match, rememberedFor))
            {
                restBeforeHold.Clear(); // a table put back or replaced: what rested where is read from it again
                rememberedFor = match;
            }

            if (after == null || after.Mode != PhysicalObjectMode.Held)
            {
                restBeforeHold.Remove(objectId);
                return;
            }

            if (before != null && before.Mode != PhysicalObjectMode.Held) restBeforeHold[objectId] = before;
        }

        /// <summary>
        /// The host refused <paramref name="holder"/>'s commit of a piece. When the host's table still has it held by
        /// that same player and where it rested before is known, the command that puts it back there (at rest, no
        /// speed, no spin) in the holder's name. False otherwise (nothing is left held, or nothing is known).
        /// </summary>
        public bool TryBuildReturn(MatchState match, PlayerId holder, TabletopObjectId objectId, CommandId commandId,
            out CommitPhysicalObjectCommand putBack)
        {
            putBack = null;
            if (match == null || holder.IsEmpty || commandId.IsEmpty || !match.ContainsObject(objectId)) return false;
            if (!ReferenceEquals(match, rememberedFor) || !restBeforeHold.TryGetValue(objectId, out PhysicalObjectState rest)) return false;
            TabletopObjectState piece = match.GetObject(objectId);
            PhysicalObjectState now = piece.PhysicalState;
            if (now == null || now.Mode != PhysicalObjectMode.Held || now.ControllingPlayerId != holder || !piece.ContainerId.IsEmpty) return false;
            PhysicalObjectMode restMode = rest.Mode == PhysicalObjectMode.SleepingUnresolved || rest.Mode == PhysicalObjectMode.Sleeping
                ? rest.Mode
                : PhysicalObjectMode.Sleeping;
            if (piece.Kind == TabletopObjectKind.Die && restMode == PhysicalObjectMode.Sleeping) restMode = PhysicalObjectMode.SleepingUnresolved;
            putBack = new CommitPhysicalObjectCommand(
                new CommandContext(commandId, match.Id, holder, match.Revision),
                objectId,
                new PhysicalObjectState(rest.Position, rest.Rotation, default, default, restMode, holder),
                piece.PhysicalRevision);
            return true;
        }

        /// <summary>The put-back ran: the piece is no longer held.</summary>
        public void NoteReturned(TabletopObjectId objectId)
        {
            restBeforeHold.Remove(objectId);
            Returned++;
        }
    }
}
