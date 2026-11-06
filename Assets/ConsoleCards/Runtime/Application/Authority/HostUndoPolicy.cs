using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Authority
{
    public enum HostUndoVerdict
    {
        Allowed = 0,
        /// <summary>This request id already ran (a repeat or a replayed message).</summary>
        Duplicate = 1,
        MissingRequestId = 2,
        NotSeated = 3,
        /// <summary>The host's history has nothing to take back.</summary>
        NothingToUndo = 4,
        /// <summary>The latest change is another player's.</summary>
        NotYourChange = 5,
        /// <summary>The latest change has no player (a game step): only the host takes it back.</summary>
        NoPlayer = 6,
        /// <summary>The latest change is not the one the request named (it was already taken back, or something
        /// happened since): a double press never takes back a second change.</summary>
        NotTheNamedChange = 7,
        /// <summary>
        /// AUTH-UNDO-2 (owner, 2026-11-06): the latest change is a rules result in Full (a host-rolled die a rule reads,
        /// a Trap applied, a tile turned over, a collapse, an elimination). Taking it back would let a player throw again
        /// for a better result or step out of a Trap: in Full it stands.
        /// </summary>
        RulesResult = 8,
    }

    /// <summary>
    /// UNDO-AUTH (owner, 2026-11-06: "for the initial MVP, use host-authorized Undo only"): the host's check of a joined
    /// computer's Undo or Redo before it runs it on the host's own history. The sender's seat comes from the connection.
    /// A request names the change it means (the command id of that change on the sender's table); the host runs it only
    /// when that very change is next in the host's history and was made by the sender's seat. A change with no player
    /// (a game step) is the host's to take back. Each request id runs once. Pure, so it is unit tested.
    /// </summary>
    public sealed class HostUndoPolicy
    {
        public const int Remembered = 512;

        private readonly HashSet<Guid> seen = new HashSet<Guid>();
        private readonly Queue<Guid> order = new Queue<Guid>();

        /// <param name="hasNext">The host's history has a change to take back (Undo) or to make again (Redo).</param>
        /// <param name="nextCommandId">That change's command id on the host.</param>
        /// <param name="nextActor">The player who made it (empty: a game step).</param>
        /// <param name="nextIsRulesResult">Full only: that change is a rules result the host marked (see RulesResult).</param>
        public HostUndoVerdict Check(
            PlayerId sender,
            Guid requestId,
            CommandId namedChange,
            bool hasNext,
            CommandId nextCommandId,
            PlayerId nextActor,
            bool nextIsRulesResult = false)
        {
            if (requestId == Guid.Empty) return HostUndoVerdict.MissingRequestId;
            if (seen.Contains(requestId)) return HostUndoVerdict.Duplicate;
            Remember(requestId); // a request is used once, whether it is run or refused
            if (sender.IsEmpty) return HostUndoVerdict.NotSeated;
            if (!hasNext) return HostUndoVerdict.NothingToUndo;
            if (nextActor.IsEmpty) return HostUndoVerdict.NoPlayer;
            if (nextActor != sender) return HostUndoVerdict.NotYourChange;
            if (namedChange.IsEmpty || namedChange != nextCommandId) return HostUndoVerdict.NotTheNamedChange;
            if (nextIsRulesResult) return HostUndoVerdict.RulesResult;
            return HostUndoVerdict.Allowed;
        }

        public void Clear()
        {
            seen.Clear();
            order.Clear();
        }

        public static string Describe(HostUndoVerdict verdict, bool redo)
        {
            string what = redo ? "redo" : "undo";
            switch (verdict)
            {
                case HostUndoVerdict.NotSeated: return $"Only players with a seat can {what}.";
                case HostUndoVerdict.NothingToUndo: return redo ? "There is nothing to redo on the host's table." : "There is nothing to undo on the host's table.";
                case HostUndoVerdict.NotYourChange: return $"The latest change is another player's: only they can {what} it.";
                case HostUndoVerdict.NoPlayer: return $"The latest change was a game step: only the host can {what} it.";
                case HostUndoVerdict.NotTheNamedChange: return $"That change was already taken back or something happened since: nothing more to {what}.";
                case HostUndoVerdict.MissingRequestId: return $"The {what} request was not understood.";
                case HostUndoVerdict.RulesResult: return $"That was a rules result (a die the rules read, a Trap, a turned tile, a collapse or an elimination): in Full it stands and cannot be taken back.";
                default: return null;
            }
        }

        private void Remember(Guid id)
        {
            seen.Add(id);
            order.Enqueue(id);
            while (order.Count > Remembered) seen.Remove(order.Dequeue());
        }
    }
}
