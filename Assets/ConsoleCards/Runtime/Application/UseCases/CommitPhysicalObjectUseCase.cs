using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    /// <summary>Authority-side surface query, implemented by the local/host collision world, never by a client.</summary>
    public interface IPhysicalPlacementResolver
    {
        bool TryResolve(TabletopPose layoutPose, PlayerId actor, out PhysicalObjectState state,
            TabletopComponentKind kind = TabletopComponentKind.Card);
    }

    public sealed class CommitPhysicalObjectCommand : ITabletopCommand
    {
        public CommitPhysicalObjectCommand(CommandContext context, TabletopObjectId objectId,
            PhysicalObjectState state, long expectedPhysicalRevision, int? settledDieValue = null)
        {
            Context = context; ObjectId = objectId; State = state; ExpectedPhysicalRevision = expectedPhysicalRevision;
            SettledDieValue = settledDieValue;
        }
        public CommandContext Context { get; }
        public TabletopObjectId ObjectId { get; }
        public PhysicalObjectState State { get; }
        public long ExpectedPhysicalRevision { get; }
        public int? SettledDieValue { get; }
    }

    /// <summary>Accepts outcomes only from the authority's simulation adapter. No surface check on release/settlement.</summary>
    public sealed class CommitPhysicalObjectUseCase
    {
        public CommandResult Execute(MatchState match, IReadOnlyList<PlayerId> actors, CommitPhysicalObjectCommand command)
        {
            if (match == null || command == null || command.State == null || command.Context.Id.IsEmpty
                || command.Context.MatchId != match.Id || !command.Context.ExpectedRevision.HasValue)
                return CommandResult.Failure(CommandResultStatus.Invalid);
            if (match.HasPhysicalCommand(command.Context.Id)
                || command.Context.ExpectedRevision.Value != match.Revision || match.Revision == long.MaxValue)
                return CommandResult.Failure(CommandResultStatus.Conflict);
            bool active = false;
            if (actors != null) foreach (PlayerId actor in actors) active |= actor == command.Context.RequestedByPlayerId;
            if (!active || command.State.ControllingPlayerId != command.Context.RequestedByPlayerId)
                return CommandResult.Failure(CommandResultStatus.Rejected);
            if (!match.ContainsObject(command.ObjectId)) return CommandResult.Failure(CommandResultStatus.Rejected);
            TabletopObjectState obj = match.GetObject(command.ObjectId);
            if (!obj.ContainerId.IsEmpty || (obj.IsUserLocked && obj.PhysicalState != null)
                || obj.PhysicalRevision != command.ExpectedPhysicalRevision || obj.PhysicalRevision == long.MaxValue)
                return CommandResult.Failure(CommandResultStatus.Conflict);
            if (obj.PhysicalState != null && obj.PhysicalState.Mode == PhysicalObjectMode.Held
                && obj.PhysicalState.ControllingPlayerId != command.Context.RequestedByPlayerId)
                return CommandResult.Failure(CommandResultStatus.Conflict);
            DieState die = null;
            if (command.State.Mode == PhysicalObjectMode.SleepingUnresolved && obj.Kind != TabletopObjectKind.Die)
                return CommandResult.Failure(CommandResultStatus.Invalid);
            if (command.SettledDieValue.HasValue)
            {
                if (command.State.Mode != PhysicalObjectMode.Sleeping
                    || !match.Dice.TryGetValue(obj.Id, out die)
                    || command.SettledDieValue.Value < 1 || command.SettledDieValue.Value > die.SideCount)
                    return CommandResult.Failure(CommandResultStatus.Invalid);
            }
            else if (obj.Kind == TabletopObjectKind.Die && command.State.Mode == PhysicalObjectMode.Sleeping)
                return CommandResult.Failure(CommandResultStatus.Invalid);
            obj.SetPhysicalState(command.State);
            if (die != null) die.SetAcceptedRoll(new DieRoll(die.SideCount, command.SettledDieValue.Value));
            match.RecordPhysicalCommand(command.Context.Id);
            return CommandResult.Accepted(match.AdvanceRevision());
        }
    }
}
