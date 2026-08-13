using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    public enum TabletopComponentTargetKind
    {
        Object,
        Container,
        Console,
    }

    public readonly struct TabletopComponentTarget
    {
        private TabletopComponentTarget(
            TabletopComponentTargetKind kind,
            TabletopObjectId objectId,
            ContainerId containerId,
            ConsoleId consoleId)
        {
            Kind = kind;
            ObjectId = objectId;
            ContainerId = containerId;
            ConsoleId = consoleId;
        }

        public TabletopComponentTargetKind Kind { get; }
        public TabletopObjectId ObjectId { get; }
        public ContainerId ContainerId { get; }
        public ConsoleId ConsoleId { get; }

        public static TabletopComponentTarget ForObject(TabletopObjectId objectId)
        {
            return new TabletopComponentTarget(
                TabletopComponentTargetKind.Object,
                objectId,
                ContainerId.Empty,
                ConsoleId.Empty);
        }

        public static TabletopComponentTarget ForContainer(ContainerId containerId)
        {
            return new TabletopComponentTarget(
                TabletopComponentTargetKind.Container,
                TabletopObjectId.Empty,
                containerId,
                ConsoleId.Empty);
        }

        public static TabletopComponentTarget ForConsole(ConsoleId consoleId)
        {
            return new TabletopComponentTarget(
                TabletopComponentTargetKind.Console,
                TabletopObjectId.Empty,
                ContainerId.Empty,
                consoleId);
        }
    }

    public enum DeleteTabletopComponentError
    {
        None,
        MatchRequired,
        RequestRequired,
        MatchIdMismatch,
        RevisionConflict,
        ActorNotActive,
        TargetInvalid,
        TargetMissing,
        TemplateComponentProtected,
        ComponentKindUnsupported,
        ContainerNotEmpty,
        ConsoleNotEmpty,
        RevisionOverflow,
    }

    public sealed class DeleteTabletopComponentRequest
    {
        public DeleteTabletopComponentRequest(
            CommandContext context,
            TabletopComponentTarget target)
        {
            Context = context;
            Target = target;
        }

        public CommandContext Context { get; }
        public TabletopComponentTarget Target { get; }
    }

    public readonly struct DeleteTabletopComponentResult
    {
        private DeleteTabletopComponentResult(
            CommandResult commandResult,
            DeleteTabletopComponentError error,
            TabletopComponentTarget target,
            TabletopComponentKind componentKind,
            ContainerId previousContainerId)
        {
            CommandResult = commandResult;
            Error = error;
            Target = target;
            ComponentKind = componentKind;
            PreviousContainerId = previousContainerId;
        }

        public CommandResult CommandResult { get; }
        public DeleteTabletopComponentError Error { get; }
        public TabletopComponentTarget Target { get; }
        public TabletopComponentKind ComponentKind { get; }
        public ContainerId PreviousContainerId { get; }
        public bool Succeeded => CommandResult.Succeeded;
        public long Revision => CommandResult.Revision;

        internal static DeleteTabletopComponentResult Accepted(
            long revision,
            TabletopComponentTarget target,
            TabletopComponentKind componentKind,
            ContainerId previousContainerId)
        {
            return new DeleteTabletopComponentResult(
                CommandResult.Accepted(revision),
                DeleteTabletopComponentError.None,
                target,
                componentKind,
                previousContainerId);
        }

        internal static DeleteTabletopComponentResult Failure(
            CommandResultStatus status,
            DeleteTabletopComponentError error)
        {
            return new DeleteTabletopComponentResult(
                CommandResult.Failure(status),
                error,
                default,
                default,
                ContainerId.Empty);
        }
    }

    /// <summary>
    /// Actor-aware removal of one runtime-created generic tabletop Component.
    /// Game Template identities remain protected and non-empty Containers are rejected.
    /// </summary>
    public sealed class DeleteTabletopComponentUseCase
    {
        public DeleteTabletopComponentResult Execute(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            DeleteTabletopComponentRequest request)
        {
            DeleteTabletopComponentResult? failure = ValidateCommon(matchState, activePlayerIds, request);
            if (failure.HasValue)
            {
                return failure.Value;
            }

            switch (request.Target.Kind)
            {
                case TabletopComponentTargetKind.Object:
                    return DeleteObject(matchState, request.Target);
                case TabletopComponentTargetKind.Container:
                    return DeleteContainer(matchState, request.Target);
                case TabletopComponentTargetKind.Console:
                    return DeleteConsole(matchState, request.Target);
                default:
                    return DeleteTabletopComponentResult.Failure(
                        CommandResultStatus.Invalid,
                        DeleteTabletopComponentError.TargetInvalid);
            }
        }

        private static DeleteTabletopComponentResult DeleteObject(
            MatchState matchState,
            TabletopComponentTarget target)
        {
            if (target.ObjectId.IsEmpty || !matchState.ContainsObject(target.ObjectId))
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DeleteTabletopComponentError.TargetMissing);
            }

            if (matchState.IsTemplateObject(target.ObjectId))
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DeleteTabletopComponentError.TemplateComponentProtected);
            }

            TabletopObjectState objectState = matchState.GetObject(target.ObjectId);
            if (!TryMapObjectKind(objectState.Kind, out TabletopComponentKind componentKind))
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DeleteTabletopComponentError.ComponentKindUnsupported);
            }

            ContainerId previousContainerId = objectState.ContainerId;
            matchState.RemoveObject(target.ObjectId);
            return DeleteTabletopComponentResult.Accepted(
                matchState.AdvanceRevision(),
                target,
                componentKind,
                previousContainerId);
        }

        private static DeleteTabletopComponentResult DeleteContainer(
            MatchState matchState,
            TabletopComponentTarget target)
        {
            if (target.ContainerId.IsEmpty
                || !matchState.Containers.TryGetValue(target.ContainerId, out ContainerState container))
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DeleteTabletopComponentError.TargetMissing);
            }

            if (matchState.IsTemplateContainer(target.ContainerId))
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DeleteTabletopComponentError.TemplateComponentProtected);
            }

            TabletopComponentKind componentKind;
            switch (container.Kind)
            {
                case ContainerKind.Deck:
                    componentKind = TabletopComponentKind.Deck;
                    break;
                case ContainerKind.Stack:
                    componentKind = TabletopComponentKind.Stack;
                    break;
                default:
                    return DeleteTabletopComponentResult.Failure(
                        CommandResultStatus.Rejected,
                        DeleteTabletopComponentError.ComponentKindUnsupported);
            }

            if (container.Count != 0)
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DeleteTabletopComponentError.ContainerNotEmpty);
            }

            matchState.RemoveEmptyContainer(target.ContainerId);
            return DeleteTabletopComponentResult.Accepted(
                matchState.AdvanceRevision(),
                target,
                componentKind,
                ContainerId.Empty);
        }

        private static DeleteTabletopComponentResult DeleteConsole(
            MatchState matchState,
            TabletopComponentTarget target)
        {
            if (target.ConsoleId.IsEmpty
                || !matchState.PlacedConsoles.TryGetValue(target.ConsoleId, out PlacedConsoleState placedConsole))
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DeleteTabletopComponentError.TargetMissing);
            }

            for (int i = 0; i < placedConsole.Console.SlotContainerIds.Count; i++)
            {
                ContainerId slotId = placedConsole.Console.SlotContainerIds[i];
                if (!matchState.Containers.TryGetValue(slotId, out ContainerState slot)
                    || slot.Count != 0)
                {
                    return DeleteTabletopComponentResult.Failure(
                        CommandResultStatus.Rejected,
                        DeleteTabletopComponentError.ConsoleNotEmpty);
                }
            }

            matchState.RemoveEmptyPlacedConsole(target.ConsoleId);
            return DeleteTabletopComponentResult.Accepted(
                matchState.AdvanceRevision(),
                target,
                TabletopComponentKind.Console,
                ContainerId.Empty);
        }

        private static DeleteTabletopComponentResult? ValidateCommon(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            DeleteTabletopComponentRequest request)
        {
            if (matchState == null)
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    DeleteTabletopComponentError.MatchRequired);
            }

            if (request == null)
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    DeleteTabletopComponentError.RequestRequired);
            }

            if (request.Context.MatchId != matchState.Id)
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Invalid,
                    DeleteTabletopComponentError.MatchIdMismatch);
            }

            if (request.Context.ExpectedRevision.HasValue
                && request.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Conflict,
                    DeleteTabletopComponentError.RevisionConflict);
            }

            if (!Contains(activePlayerIds, request.Context.RequestedByPlayerId))
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Rejected,
                    DeleteTabletopComponentError.ActorNotActive);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return DeleteTabletopComponentResult.Failure(
                    CommandResultStatus.Conflict,
                    DeleteTabletopComponentError.RevisionOverflow);
            }

            return null;
        }

        private static bool TryMapObjectKind(
            TabletopObjectKind objectKind,
            out TabletopComponentKind componentKind)
        {
            switch (objectKind)
            {
                case TabletopObjectKind.Card:
                    componentKind = TabletopComponentKind.Card;
                    return true;
                case TabletopObjectKind.Pawn:
                    componentKind = TabletopComponentKind.Pawn;
                    return true;
                case TabletopObjectKind.Token:
                    componentKind = TabletopComponentKind.Token;
                    return true;
                case TabletopObjectKind.Die:
                    componentKind = TabletopComponentKind.Die;
                    return true;
                default:
                    componentKind = default;
                    return false;
            }
        }

        private static bool Contains(IReadOnlyList<PlayerId> players, PlayerId playerId)
        {
            if (players == null)
            {
                return false;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == playerId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
