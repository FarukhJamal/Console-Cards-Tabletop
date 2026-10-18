using System;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// The one door for every change to the table (doc 30 A6, gap 1; R1b). The table code builds a command (who asks,
    /// the command ID, the expected revision and the change) and submits it here instead of calling the use case
    /// itself. The local authority runs the matching use case at once; a network authority (later, only when the
    /// owner asks for multiplayer) sends the command to the host and returns the host's answer.
    /// </summary>
    public interface ITableAuthority
    {
        /// <summary>
        /// Every change the table accepted, after it is applied (R2): the command object itself, raised once per
        /// accepted command, whoever asked for it. The local authority raises it inside Submit, before Submit
        /// returns; a network authority will raise it when the host's accepted change arrives. Views follow the
        /// table from here, so a change made by another player shows the same way as one made on this computer.
        /// </summary>
        event Action<object> CommandAccepted;

        /// <summary>
        /// Listens to one command type with its result (R2): called after every accepted command of that type,
        /// right after <see cref="CommandAccepted"/>, with the command and what it made (new IDs and so on). Typed,
        /// so a listener costs no garbage per change.
        /// </summary>
        void Observe<TCommand, TResult>(Action<TCommand, TResult> listener) where TCommand : class;

        /// <summary>Runs (or sends) one change and returns its result. The command type picks the handler.</summary>
        TResult Submit<TCommand, TResult>(TCommand command) where TCommand : class;

        /// <summary>True when a handler for this command type is set up.</summary>
        bool CanSubmit<TCommand>() where TCommand : class;
    }
}
