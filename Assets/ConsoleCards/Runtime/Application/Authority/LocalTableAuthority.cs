using System;
using System.Collections.Generic;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// The local table authority (R1b): each command type has one handler, set up once when the table is built,
    /// that calls the use case on this computer. Submitting looks the handler up and calls it; nothing is
    /// allocated by the door itself. Accepted commands are announced on <see cref="CommandAccepted"/> (R2).
    /// </summary>
    public sealed class LocalTableAuthority : ITableAuthority
    {
        private readonly Dictionary<Type, object> handlers = new Dictionary<Type, object>(32);
        private readonly Dictionary<Type, object> observers = new Dictionary<Type, object>(32);

        /// <inheritdoc />
        public event Action<object> CommandAccepted;

        /// <summary>Sets the handler for one command type (a second call for the same type replaces it).</summary>
        public void Register<TCommand, TResult>(Func<TCommand, TResult> handler) where TCommand : class
        {
            handlers[typeof(TCommand)] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Sets the handler for one command type and tells the table about every accepted one (R2):
        /// <paramref name="accepted"/> reads the result, and when it says yes, <see cref="CommandAccepted"/> is
        /// raised with the command before Submit returns. The wrapper is made once, here; a submit allocates nothing.
        /// </summary>
        public void Register<TCommand, TResult>(Func<TCommand, TResult> handler, Func<TResult, bool> accepted)
            where TCommand : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (accepted == null) throw new ArgumentNullException(nameof(accepted));
            ObserverSlot<TCommand, TResult> slot = SlotFor<TCommand, TResult>();
            handlers[typeof(TCommand)] = (Func<TCommand, TResult>)(command =>
            {
                TResult result = handler(command);
                if (accepted(result))
                {
                    CommandAccepted?.Invoke(command);
                    slot.Listeners?.Invoke(command, result);
                }

                return result;
            });
        }

        /// <inheritdoc />
        public void Observe<TCommand, TResult>(Action<TCommand, TResult> listener) where TCommand : class
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            SlotFor<TCommand, TResult>().Listeners += listener;
        }

        // One slot per command type, shared by Register and Observe in either order.
        private ObserverSlot<TCommand, TResult> SlotFor<TCommand, TResult>()
        {
            if (observers.TryGetValue(typeof(TCommand), out object existing))
            {
                if (existing is ObserverSlot<TCommand, TResult> typed) return typed;
                throw new InvalidOperationException(
                    $"{typeof(TCommand).Name} is observed with a result type other than {typeof(TResult).Name}.");
            }

            ObserverSlot<TCommand, TResult> slot = new ObserverSlot<TCommand, TResult>();
            observers[typeof(TCommand)] = slot;
            return slot;
        }

        private sealed class ObserverSlot<TCommand, TResult>
        {
            public Action<TCommand, TResult> Listeners;
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
