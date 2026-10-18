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
        /// <summary>Runs (or sends) one change and returns its result. The command type picks the handler.</summary>
        TResult Submit<TCommand, TResult>(TCommand command) where TCommand : class;

        /// <summary>True when a handler for this command type is set up.</summary>
        bool CanSubmit<TCommand>() where TCommand : class;
    }
}
