using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates
{
    /// <summary>
    /// Session-local authoritative snapshot history. The first state is the active session baseline.
    /// Each immutable accepted After snapshot becomes the already-established Before boundary for the
    /// next serialized top-level action; removing the latest transaction exposes that exact Before state.
    /// </summary>
    public sealed class ActiveSessionUndoHistory<TSnapshot> where TSnapshot : class
    {
        private readonly List<TSnapshot> states = new List<TSnapshot>();
        private readonly List<ActiveSessionUndoTransaction> transactions =
            new List<ActiveSessionUndoTransaction>();

        public bool CanUndo => transactions.Count > 0;
        public int TransactionCount => transactions.Count;
        public int CurrentStateIndex => states.Count == 0 ? -1 : states.Count - 1;
        public TSnapshot CurrentState =>
            states.Count > 0
                ? states[states.Count - 1]
                : throw new InvalidOperationException("Undo history requires State 0 before reading the current state.");
        public ActiveSessionUndoTransaction NextUndo =>
            CanUndo ? transactions[transactions.Count - 1] : null;

        public void EstablishBaseline(TSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            states.Clear();
            transactions.Clear();
            states.Add(state);
        }

        public void RecordAccepted(
            TSnapshot beforeState,
            TSnapshot acceptedState,
            AuthoritativeActionAcceptance acceptance,
            string description)
        {
            if (beforeState == null) throw new ArgumentNullException(nameof(beforeState));
            if (acceptedState == null) throw new ArgumentNullException(nameof(acceptedState));
            if (states.Count == 0) throw new InvalidOperationException("Undo history requires State 0 before recording actions.");
            if (!ReferenceEquals(states[states.Count - 1], beforeState))
                throw new InvalidOperationException("The Undo transaction Before state is not the current authoritative history state.");
            if (acceptance.RecordMode != AuthoritativeActionRecordMode.Transaction)
                throw new ArgumentException("Only completed transactions may enter Undo history.", nameof(acceptance));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Undo description is required.", nameof(description));

            states.Add(acceptedState);
            transactions.Add(new ActiveSessionUndoTransaction(
                acceptance.CommandId,
                acceptance.ActorPlayerId,
                acceptance.Kind,
                acceptance.Revision,
                description));
        }

        public bool TryUndo(out TSnapshot previousState, out ActiveSessionUndoTransaction transaction)
        {
            if (!TryPeekUndo(out previousState, out transaction))
            {
                return false;
            }

            CommitUndo();
            return true;
        }

        public bool TryPeekUndo(out TSnapshot previousState, out ActiveSessionUndoTransaction transaction)
        {
            if (!CanUndo)
            {
                previousState = null;
                transaction = null;
                return false;
            }

            transaction = transactions[transactions.Count - 1];
            previousState = states[states.Count - 2];
            return true;
        }

        public void CommitUndo()
        {
            if (!CanUndo) throw new InvalidOperationException("Undo history is already at State 0.");
            transactions.RemoveAt(transactions.Count - 1);
            states.RemoveAt(states.Count - 1);
        }

        public void ReplaceCurrentState(TSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (states.Count == 0) throw new InvalidOperationException("Undo history requires State 0 before replacing current state.");
            states[states.Count - 1] = state;
        }
    }

    public sealed class ActiveSessionUndoTransaction
    {
        internal ActiveSessionUndoTransaction(
            CommandId commandId,
            PlayerId actorPlayerId,
            AuthoritativeActionKind kind,
            long acceptedRevision,
            string description)
        {
            CommandId = commandId;
            ActorPlayerId = actorPlayerId;
            Kind = kind;
            AcceptedRevision = acceptedRevision;
            Description = description;
        }

        public CommandId CommandId { get; }
        public PlayerId ActorPlayerId { get; }
        public AuthoritativeActionKind Kind { get; }
        public long AcceptedRevision { get; }
        public string Description { get; }
    }
}
