using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates
{
    /// <summary>
    /// Session-local authoritative snapshot history. The first state is the active session baseline.
    /// Each immutable accepted After snapshot becomes the already-established Before boundary for the
    /// next serialized top-level action. A cursor traverses exact states without rerunning gameplay logic.
    /// </summary>
    public sealed class ActiveSessionUndoHistory<TSnapshot> where TSnapshot : class
    {
        private readonly List<TSnapshot> states = new List<TSnapshot>();
        private readonly List<ActiveSessionUndoTransaction> transactions =
            new List<ActiveSessionUndoTransaction>();
        private int currentStateIndex = -1;

        public bool CanUndo => currentStateIndex > 0;
        public bool CanRedo => currentStateIndex >= 0 && currentStateIndex < transactions.Count;
        public int TransactionCount => transactions.Count;
        public int CurrentStateIndex => currentStateIndex;
        public TSnapshot CurrentState =>
            currentStateIndex >= 0
                ? states[currentStateIndex]
                : throw new InvalidOperationException("Undo history requires State 0 before reading the current state.");
        public ActiveSessionUndoTransaction NextUndo =>
            CanUndo ? transactions[currentStateIndex - 1] : null;
        public ActiveSessionUndoTransaction NextRedo =>
            CanRedo ? transactions[currentStateIndex] : null;

        public void EstablishBaseline(TSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            states.Clear();
            transactions.Clear();
            states.Add(state);
            currentStateIndex = 0;
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
            if (!ReferenceEquals(states[currentStateIndex], beforeState))
                throw new InvalidOperationException("The Undo transaction Before state is not the current authoritative history state.");
            if (acceptance.RecordMode != AuthoritativeActionRecordMode.Transaction)
                throw new ArgumentException("Only completed transactions may enter Undo history.", nameof(acceptance));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Undo description is required.", nameof(description));

            if (CanRedo)
            {
                transactions.RemoveRange(currentStateIndex, transactions.Count - currentStateIndex);
                states.RemoveRange(currentStateIndex + 1, states.Count - currentStateIndex - 1);
            }

            states.Add(acceptedState);
            transactions.Add(new ActiveSessionUndoTransaction(
                acceptance.CommandId,
                acceptance.ActorPlayerId,
                acceptance.Kind,
                acceptance.Revision,
                description));
            currentStateIndex++;
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

            transaction = transactions[currentStateIndex - 1];
            previousState = states[currentStateIndex - 1];
            return true;
        }

        public void CommitUndo()
        {
            if (!CanUndo) throw new InvalidOperationException("Undo history is already at State 0.");
            currentStateIndex--;
        }

        public bool TryPeekRedo(out TSnapshot nextState, out ActiveSessionUndoTransaction transaction)
        {
            if (!CanRedo)
            {
                nextState = null;
                transaction = null;
                return false;
            }

            transaction = transactions[currentStateIndex];
            nextState = states[currentStateIndex + 1];
            return true;
        }

        public void CommitRedo()
        {
            if (!CanRedo) throw new InvalidOperationException("Redo history is already at its latest state.");
            currentStateIndex++;
        }

        public void ReplaceCurrentState(TSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (states.Count == 0) throw new InvalidOperationException("Undo history requires State 0 before replacing current state.");
            states[currentStateIndex] = state;
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
