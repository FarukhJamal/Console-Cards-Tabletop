using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    /// <summary>
    /// Game board mats (doc 22 §15): place, move, resize the grid and remove. Each is one authoritative command
    /// (one Undo step). Cards lying on a board are loose table cards and are never changed by these commands.
    /// Anyone may change a board, as anyone may move a Console.
    /// </summary>
    public sealed class GameBoardUseCases
    {
        public CommandResult Create(
            MatchState matchState,
            CommandContext context,
            TabletopPose pose,
            BoardGrid grid,
            float? surfaceHeight,
            bool duplicate,
            out BoardId boardId)
        {
            boardId = BoardId.Empty;
            if (!IsValid(matchState, context, out CommandResult failure)) return failure;
            if (!grid.IsValid) return CommandResult.Failure(CommandResultStatus.Invalid);
            // MP-2: the board's ID comes from the command's ID, so every computer that runs this command makes the same
            // board (one board per command; a command ID is never used twice).
            BoardId id = context.Id.IsEmpty ? BoardId.New() : new BoardId(context.Id.Value);
            matchState.AddPlacedBoard(new PlacedBoardState(id, pose, grid, surfaceHeight));
            boardId = id;
            return CommandResult.Accepted(matchState.AdvanceRevision(
                context.Id,
                context.RequestedByPlayerId,
                duplicate ? AuthoritativeActionKind.DuplicateComponent : AuthoritativeActionKind.CreateComponent));
        }

        public CommandResult Move(MatchState matchState, CommandContext context, BoardId boardId, TabletopPose pose, float? surfaceHeight)
        {
            if (!IsValid(matchState, context, out CommandResult failure)) return failure;
            if (!matchState.PlacedBoards.TryGetValue(boardId, out PlacedBoardState board)) return CommandResult.Failure(CommandResultStatus.Rejected);
            board.SetPose(pose, surfaceHeight);
            return CommandResult.Accepted(matchState.AdvanceRevision(context.Id, context.RequestedByPlayerId, AuthoritativeActionKind.MoveBoard));
        }

        public CommandResult SetGrid(MatchState matchState, CommandContext context, BoardId boardId, BoardGrid grid)
        {
            if (!IsValid(matchState, context, out CommandResult failure)) return failure;
            if (!grid.IsValid || !matchState.PlacedBoards.TryGetValue(boardId, out PlacedBoardState board))
                return CommandResult.Failure(CommandResultStatus.Rejected);
            if (board.Grid.Equals(grid)) return CommandResult.Failure(CommandResultStatus.Rejected);
            board.SetGrid(grid);
            return CommandResult.Accepted(matchState.AdvanceRevision(context.Id, context.RequestedByPlayerId, AuthoritativeActionKind.ResizeBoardGrid));
        }

        public CommandResult Delete(MatchState matchState, CommandContext context, BoardId boardId)
        {
            if (!IsValid(matchState, context, out CommandResult failure)) return failure;
            if (!matchState.PlacedBoards.ContainsKey(boardId)) return CommandResult.Failure(CommandResultStatus.Rejected);
            matchState.RemovePlacedBoard(boardId);
            return CommandResult.Accepted(matchState.AdvanceRevision(context.Id, context.RequestedByPlayerId, AuthoritativeActionKind.DeleteComponent));
        }

        private static bool IsValid(MatchState matchState, CommandContext context, out CommandResult failure)
        {
            if (matchState == null || context.MatchId != matchState.Id)
            {
                failure = CommandResult.Failure(CommandResultStatus.Invalid);
                return false;
            }

            if ((context.ExpectedRevision.HasValue && context.ExpectedRevision.Value != matchState.Revision)
                || matchState.Revision == long.MaxValue)
            {
                failure = CommandResult.Failure(CommandResultStatus.Conflict);
                return false;
            }

            failure = default;
            return true;
        }
    }
}
