using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    /// <summary>Places a game board mat (doc 22 §15); <see cref="Duplicate"/> records it as a duplicate.</summary>
    public sealed class CreateBoardCommand : ITabletopCommand
    {
        public CreateBoardCommand(CommandContext context, TabletopPose pose, BoardGrid grid, float? surfaceHeight, bool duplicate)
        {
            Context = context;
            Pose = pose;
            Grid = grid;
            SurfaceHeight = surfaceHeight;
            Duplicate = duplicate;
        }

        public CommandContext Context { get; }
        public TabletopPose Pose { get; }
        public BoardGrid Grid { get; }
        public float? SurfaceHeight { get; }
        public bool Duplicate { get; }
    }

    /// <summary>The result of <see cref="CreateBoardCommand"/>: the command result and the new board's ID.</summary>
    public readonly struct CreateBoardResult
    {
        public CreateBoardResult(CommandResult result, BoardId boardId)
        {
            Result = result;
            BoardId = boardId;
        }

        public CommandResult Result { get; }
        public BoardId BoardId { get; }
        public bool Succeeded => Result.Succeeded;
    }

    /// <summary>Moves a placed game board mat.</summary>
    public sealed class MoveBoardCommand : ITabletopCommand
    {
        public MoveBoardCommand(CommandContext context, BoardId boardId, TabletopPose pose, float? surfaceHeight)
        {
            Context = context;
            BoardId = boardId;
            Pose = pose;
            SurfaceHeight = surfaceHeight;
        }

        public CommandContext Context { get; }
        public BoardId BoardId { get; }
        public TabletopPose Pose { get; }
        public float? SurfaceHeight { get; }
    }

    /// <summary>Changes the grid of card spaces on a placed game board mat.</summary>
    public sealed class SetBoardGridCommand : ITabletopCommand
    {
        public SetBoardGridCommand(CommandContext context, BoardId boardId, BoardGrid grid)
        {
            Context = context;
            BoardId = boardId;
            Grid = grid;
        }

        public CommandContext Context { get; }
        public BoardId BoardId { get; }
        public BoardGrid Grid { get; }
    }

    /// <summary>Removes a placed game board mat.</summary>
    public sealed class DeleteBoardCommand : ITabletopCommand
    {
        public DeleteBoardCommand(CommandContext context, BoardId boardId)
        {
            Context = context;
            BoardId = boardId;
        }

        public CommandContext Context { get; }
        public BoardId BoardId { get; }
    }
}
