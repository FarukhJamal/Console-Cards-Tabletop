using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    /// <summary>
    /// Doc 58 step 1: locks or frees one loose piece (a card, pawn, token or die on the table). A locked piece cannot be
    /// picked up, pushed or turned until it is freed (Tabletop Simulator's L). Shared online like every command.
    /// </summary>
    public sealed class SetObjectLockedCommand : ITabletopCommand
    {
        public SetObjectLockedCommand(CommandContext context, TabletopObjectId objectId, bool locked)
        {
            Context = context;
            ObjectId = objectId;
            Locked = locked;
        }

        public CommandContext Context { get; }
        public TabletopObjectId ObjectId { get; }
        public bool Locked { get; }

        /// <summary>Applies the command to the table (the use case: no state of its own).</summary>
        public static CommandResult Execute(MatchState matchState, SetObjectLockedCommand command)
        {
            if (matchState == null || command == null) return CommandResult.Failure(CommandResultStatus.Invalid);
            if (command.Context.MatchId != matchState.Id) return CommandResult.Failure(CommandResultStatus.Invalid);
            if (command.Context.ExpectedRevision.HasValue && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return CommandResult.Failure(CommandResultStatus.Conflict);
            }

            if (command.ObjectId.IsEmpty || !matchState.ContainsObject(command.ObjectId))
            {
                return CommandResult.Failure(CommandResultStatus.Rejected);
            }

            TabletopObjectState state = matchState.GetObject(command.ObjectId);
            if (!state.ContainerId.IsEmpty || state.IsUserLocked == command.Locked || matchState.Revision == long.MaxValue)
            {
                return CommandResult.Failure(CommandResultStatus.Rejected);
            }

            state.SetUserLocked(command.Locked);
            long revision = matchState.AdvanceRevision(
                command.Context.Id, command.Context.RequestedByPlayerId, AuthoritativeActionKind.LockObject);
            return CommandResult.Accepted(revision);
        }
    }
}
