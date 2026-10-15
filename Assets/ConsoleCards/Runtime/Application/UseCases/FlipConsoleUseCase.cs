using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    /// <summary>Turns a double-sided Console mat over (doc 22 §14). The Console is addressed by any of its Slots.</summary>
    public sealed class FlipConsoleCommand : ITabletopCommand
    {
        public FlipConsoleCommand(CommandContext context, ContainerId slotContainerId, bool showBack)
        {
            Context = context;
            SlotContainerId = slotContainerId;
            ShowBack = showBack;
        }

        public CommandContext Context { get; }
        public ContainerId SlotContainerId { get; }
        /// <summary>The side to show afterwards: true = back side up.</summary>
        public bool ShowBack { get; }
    }

    /// <summary>
    /// Sets which side of a Console mat faces up. Cards stay in their Slots; only the side facing up can be
    /// used. Anyone may flip a Console, as anyone may move one. Slot membership is not changed.
    /// </summary>
    public sealed class FlipConsoleUseCase
    {
        public CommandResult Execute(MatchState matchState, FlipConsoleCommand command)
        {
            if (matchState == null || command == null || command.Context.MatchId != matchState.Id)
            {
                return CommandResult.Failure(CommandResultStatus.Invalid);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return CommandResult.Failure(CommandResultStatus.Conflict);
            }

            if (!matchState.Containers.TryGetValue(command.SlotContainerId, out ContainerState slot)
                || slot.Kind != ContainerKind.ConsoleSlot
                || !TryResolveConsole(matchState, command.SlotContainerId, out ConsoleState console))
            {
                return CommandResult.Failure(CommandResultStatus.Rejected);
            }

            if (console.ShowsBack == command.ShowBack)
            {
                return CommandResult.Failure(CommandResultStatus.Rejected);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return CommandResult.Failure(CommandResultStatus.Conflict);
            }

            console.SetShowsBack(command.ShowBack);
            return CommandResult.Accepted(matchState.AdvanceRevision(
                command.Context.Id,
                command.Context.RequestedByPlayerId,
                AuthoritativeActionKind.FlipConsole));
        }

        public static bool TryResolveConsole(MatchState matchState, ContainerId slotContainerId, out ConsoleState console)
        {
            foreach (SeatState seat in matchState.Seats.Values)
            {
                if (seat.Console.ContainsSlot(slotContainerId))
                {
                    console = seat.Console;
                    return true;
                }
            }

            foreach (PlacedConsoleState placed in matchState.PlacedConsoles.Values)
            {
                if (placed.Console.ContainsSlot(slotContainerId))
                {
                    console = placed.Console;
                    return true;
                }
            }

            console = null;
            return false;
        }
    }
}
