using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Authority
{
    /// <summary>Where this computer's table stands in online play (MP-2).</summary>
    public enum TableReplicationMode
    {
        /// <summary>Not online: every command goes straight to the local authority, exactly as before.</summary>
        Offline = 0,

        /// <summary>This computer hosts: it puts every command in one order and sends it to the others.</summary>
        Host = 1,

        /// <summary>This computer joined a host.</summary>
        Client = 2,
    }

    /// <summary>What happened to a command that arrived from another computer.</summary>
    public enum ReplayOutcome
    {
        Accepted = 0,
        Rejected = 1,

        /// <summary>This computer made it and already applied it (the host's copy of our own command).</summary>
        AlreadyApplied = 2,

        /// <summary>No handler for its type on this table (another game is on the table here).</summary>
        Unknown = 3,

        /// <summary>It was made for another table (one replaced since); it is left out.</summary>
        OtherTable = 4,
    }

    /// <summary>
    /// MP-2 (doc 10 §4, doc 30 A6): the table's command door, shared by several computers. Every computer runs every
    /// command on its own copy of the table with the same handlers (the local authority inside), so the views follow
    /// remote players' changes exactly as they follow local ones (R2).
    /// <list type="bullet">
    /// <item>A command made on this computer runs at once (no waiting for the network) and, when accepted, is handed
    /// to <see cref="LocalCommandAccepted"/> to be sent. Commands are handed on in the order they began, after the
    /// outermost one has finished, so a follow-up a game makes while a move is being accepted always goes after the
    /// move. The host sends every accepted command, its own and the ones it got, to everyone in one order.</item>
    /// <item>A command from another computer is replayed with <see cref="Replay"/>: its expected revision is set to
    /// this table's, and the command's random numbers and new IDs come from its command ID
    /// (<see cref="CommandScopedIdentitySource"/>, the random source reseeded), so every computer gets the same result.
    /// Commands made while replaying (a game reacting to the change) are not run: the computer that made the change
    /// sends those too.</item>
    /// <item>A command for a player who does not play on this computer is not made here.</item>
    /// </list>
    /// Offline it adds nothing: Submit is the local authority's Submit.
    /// </summary>
    public sealed class ReplicatedTableAuthority : ITableAuthority
    {
        private readonly LocalTableAuthority inner;
        private readonly Func<MatchState> currentMatch;
        private readonly Action<CommandId> beginCommand;
        private readonly Action endCommand;
        private readonly HashSet<CommandId> appliedHere = new HashSet<CommandId>();
        private readonly Dictionary<Type, FieldInfo> contextFields = new Dictionary<Type, FieldInfo>();
        // Commands made here, in the order they began (null: still running, or not accepted); handed on at depth 0.
        private readonly List<object> outgoing = new List<object>(8);
        private readonly List<int> slots = new List<int>(8);
        private static readonly FieldInfo PhysicalRevisionField = typeof(CommitPhysicalObjectCommand).GetField(
            "<ExpectedPhysicalRevision>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private int depth;
        private bool replaying;

        public ReplicatedTableAuthority(
            LocalTableAuthority inner,
            Func<MatchState> currentMatch,
            Action<CommandId> beginCommand,
            Action endCommand)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.currentMatch = currentMatch ?? throw new ArgumentNullException(nameof(currentMatch));
            this.beginCommand = beginCommand;
            this.endCommand = endCommand;
            inner.CommandAccepted += OnInnerAccepted; // first, before the views: see the class notes
        }

        public TableReplicationMode Mode { get; set; }

        /// <summary>Who plays on this computer (null: everyone, as offline).</summary>
        public Func<PlayerId, bool> IsLocalPlayer { get; set; }

        /// <summary>True while a command (made here or replayed) is running.</summary>
        public bool InsideCommand => depth > 0;

        /// <summary>True while a command from another computer is being replayed.</summary>
        public bool Replaying => replaying;

        /// <summary>Commands made here that the host has not sent back yet (client only).</summary>
        public int UnconfirmedCount => appliedHere.Count;

        /// <summary>The last error from <see cref="LocalCommandAccepted"/> (it never leaves Submit); the reader clears it.</summary>
        public Exception LastSendFailure { get; set; }

        /// <summary>A command made on this computer was accepted here; online it is sent on from here.</summary>
        public event Action<object> LocalCommandAccepted;

        public event Action<object> CommandAccepted
        {
            add => inner.CommandAccepted += value;
            remove => inner.CommandAccepted -= value;
        }

        public void Observe<TCommand, TResult>(Action<TCommand, TResult> listener) where TCommand : class
        {
            inner.Observe(listener);
        }

        public bool CanSubmit<TCommand>() where TCommand : class => inner.CanSubmit<TCommand>();

        public TResult Submit<TCommand, TResult>(TCommand command) where TCommand : class
        {
            if (Mode == TableReplicationMode.Offline) return inner.Submit<TCommand, TResult>(command);
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (replaying) return NotRun<TResult>(); // the computer that made the change sends this one too
            CommandContext context = ContextOf(command);
            if (IsLocalPlayer != null && !context.RequestedByPlayerId.IsEmpty && !IsLocalPlayer(context.RequestedByPlayerId))
            {
                return NotRun<TResult>(); // another computer plays that seat
            }

            slots.Add(outgoing.Count);
            outgoing.Add(null);
            depth++;
            beginCommand?.Invoke(context.Id);
            try
            {
                return inner.Submit<TCommand, TResult>(command);
            }
            finally
            {
                endCommand?.Invoke();
                depth--;
                slots.RemoveAt(slots.Count - 1);
                if (depth == 0) Flush();
            }
        }

        /// <summary>
        /// Runs a command that came from another computer. The caller asks for the whole table again when a command
        /// accepted elsewhere is rejected here (the tables went apart).
        /// </summary>
        public ReplayOutcome Replay(object command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            CommandContext context = ContextOf(command);
            if (!context.Id.IsEmpty && appliedHere.Remove(context.Id)) return ReplayOutcome.AlreadyApplied;
            MatchState match = currentMatch();
            if (match == null || (!context.MatchId.IsEmpty && context.MatchId != match.Id)) return ReplayOutcome.OtherTable;
            if (!inner.HasHandlerFor(command.GetType())) return ReplayOutcome.Unknown;
            Restamp(command, context, match);
            bool wasReplaying = replaying;
            replaying = true;
            depth++;
            beginCommand?.Invoke(context.Id);
            try
            {
                return inner.SubmitObject(command) ? ReplayOutcome.Accepted : ReplayOutcome.Rejected;
            }
            finally
            {
                endCommand?.Invoke();
                depth--;
                replaying = wasReplaying;
            }
        }

        /// <summary>A whole table arrived: commands made here before it are no longer waiting to come back.</summary>
        public void ForgetAppliedCommands()
        {
            appliedHere.Clear();
        }

        /// <summary>A command made here that will not come back from the host (it was sent as the whole table).</summary>
        public void ForgetCommand(object command)
        {
            if (command == null) return;
            CommandId id = ContextOf(command).Id;
            if (!id.IsEmpty) appliedHere.Remove(id);
        }

        /// <summary>The command's who / ID / revision part (every table command has one).</summary>
        public CommandContext ContextOf(object command)
        {
            if (command is ITabletopCommand tabletop) return tabletop.Context;
            FieldInfo field = ContextField(command.GetType());
            return field != null ? (CommandContext)field.GetValue(command) : default; // none: run here as it is
        }

        private void OnInnerAccepted(object command)
        {
            if (Mode == TableReplicationMode.Offline || replaying || slots.Count == 0) return;
            CommandId id = ContextOf(command).Id;
            if (Mode == TableReplicationMode.Client && !id.IsEmpty) appliedHere.Add(id);
            outgoing[slots[slots.Count - 1]] = command;
        }

        private void Flush()
        {
            if (outgoing.Count == 0) return;
            for (int i = 0; i < outgoing.Count; i++)
            {
                object command = outgoing[i];
                if (command == null) continue;
                try
                {
                    LocalCommandAccepted?.Invoke(command);
                }
                catch (Exception exception)
                {
                    // Never out of Submit (the move itself was made); the sender reads it and asks for the table again.
                    LastSendFailure = exception;
                }
            }

            outgoing.Clear();
        }

        // The command is checked against this table: its expected revision becomes this table's (the other computer
        // made it against its own copy, one or more changes behind or ahead), and a physics commit's expected
        // physical revision becomes the piece's here. Everything else is checked as usual.
        private void Restamp(object command, CommandContext context, MatchState match)
        {
            FieldInfo field = ContextField(command.GetType());
            if (field != null && !context.Id.IsEmpty && !context.RequestedByPlayerId.IsEmpty)
            {
                CommandContext here = new CommandContext(
                    context.Id,
                    match.Id,
                    context.RequestedByPlayerId,
                    context.ExpectedRevision.HasValue ? match.Revision : (long?)null);
                field.SetValue(command, here);
            }

            if (command is CommitPhysicalObjectCommand commit && PhysicalRevisionField != null && match.ContainsObject(commit.ObjectId))
            {
                TabletopObjectState state = match.GetObject(commit.ObjectId);
                PhysicalRevisionField.SetValue(command, state.PhysicalRevision);
            }
        }

        private FieldInfo ContextField(Type type)
        {
            if (contextFields.TryGetValue(type, out FieldInfo cached)) return cached;
            FieldInfo found = null;
            for (Type at = type; at != null && found == null; at = at.BaseType)
            {
                foreach (FieldInfo field in at.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(CommandContext))
                    {
                        found = field;
                        break;
                    }
                }
            }

            contextFields[type] = found;
            return found;
        }

        // The result of a command that is not run here: every field at its default, so "Succeeded" is false. One per
        // result type, made once (a game asking every frame for a seat played elsewhere allocates nothing).
        private readonly Dictionary<Type, object> notRun = new Dictionary<Type, object>();

        private TResult NotRun<TResult>()
        {
            if (typeof(TResult).IsValueType) return default;
            if (!notRun.TryGetValue(typeof(TResult), out object result))
            {
                result = FormatterServices.GetUninitializedObject(typeof(TResult));
                notRun.Add(typeof(TResult), result);
            }

            return (TResult)result;
        }
    }

    /// <summary>
    /// MP-2: new piece and pile IDs. Inside a command they are made from the command's ID and a counter, so every
    /// computer that runs the command makes the same IDs; outside one (setting up a table) they are new GUIDs, as before.
    /// </summary>
    public sealed class CommandScopedIdentitySource : ITabletopComponentIdentitySource
    {
        private readonly List<Guid> scopes = new List<Guid>(4);
        private readonly List<int> counters = new List<int>(4);
        private readonly byte[] bytes = new byte[16];

        public void Begin(Guid commandId)
        {
            scopes.Add(commandId);
            counters.Add(0);
        }

        public void End()
        {
            if (scopes.Count == 0) return;
            scopes.RemoveAt(scopes.Count - 1);
            counters.RemoveAt(counters.Count - 1);
        }

        /// <summary>True while that command (or one inside it) is running.</summary>
        public bool IsRunning(Guid commandId)
        {
            for (int i = 0; i < scopes.Count; i++)
            {
                if (scopes[i] == commandId) return true;
            }

            return false;
        }

        public TabletopObjectId NextObjectId() => new TabletopObjectId(Next());

        public ContainerId NextContainerId() => new ContainerId(Next());

        private Guid Next()
        {
            int top = scopes.Count - 1;
            if (top < 0 || scopes[top] == Guid.Empty) return Guid.NewGuid();
            int counter = ++counters[top];
            byte[] source = scopes[top].ToByteArray();
            ulong first = Mix(source, counter, 0x9E3779B97F4A7C15UL);
            ulong second = Mix(source, counter, 0xC2B2AE3D27D4EB4FUL);
            for (int i = 0; i < 8; i++)
            {
                bytes[i] = (byte)(first >> (i * 8));
                bytes[i + 8] = (byte)(second >> (i * 8));
            }

            return new Guid(bytes);
        }

        /// <summary>A seed for the command's random numbers (the same on every computer).</summary>
        public static int SeedOf(Guid commandId)
        {
            ulong mixed = Mix(commandId.ToByteArray(), 0, 0x94D049BB133111EBUL);
            return (int)(mixed ^ (mixed >> 32));
        }

        private static ulong Mix(byte[] source, int counter, ulong salt)
        {
            ulong hash = 14695981039346656037UL ^ salt;
            for (int i = 0; i < source.Length; i++)
            {
                hash ^= source[i];
                hash *= 1099511628211UL;
            }

            hash ^= (ulong)(uint)counter;
            hash *= 1099511628211UL;
            hash ^= hash >> 31;
            hash *= 0xBF58476D1CE4E5B9UL;
            hash ^= hash >> 29;
            return hash;
        }
    }
}
