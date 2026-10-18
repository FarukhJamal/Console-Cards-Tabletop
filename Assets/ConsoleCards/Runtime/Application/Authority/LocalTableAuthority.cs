using System;
using System.Collections.Generic;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// The local table authority (R1b): each command type has one handler, set up once when the table is built,
    /// that calls the use case on this computer. Submitting looks the handler up and calls it; nothing is
    /// allocated by the door itself.
    /// </summary>
    public sealed class LocalTableAuthority : ITableAuthority
    {
        private readonly Dictionary<Type, object> handlers = new Dictionary<Type, object>(32);

        /// <summary>Sets the handler for one command type (a second call for the same type replaces it).</summary>
        public void Register<TCommand, TResult>(Func<TCommand, TResult> handler) where TCommand : class
        {
            handlers[typeof(TCommand)] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public TResult Submit<TCommand, TResult>(TCommand command) where TCommand : class
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!handlers.TryGetValue(typeof(TCommand), out object handler))
            {
                throw new InvalidOperationException($"No table handler is set up for {typeof(TCommand).Name}.");
            }

            if (!(handler is Func<TCommand, TResult> typed))
            {
                throw new InvalidOperationException(
                    $"The table handler for {typeof(TCommand).Name} does not return {typeof(TResult).Name}.");
            }

            return typed(command);
        }

        public bool CanSubmit<TCommand>() where TCommand : class
        {
            return handlers.ContainsKey(typeof(TCommand));
        }
    }
}
