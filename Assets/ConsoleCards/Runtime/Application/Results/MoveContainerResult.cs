using System;

namespace ConsoleCards.Application.Results
{
    public enum MoveContainerError
    {
        None,
        MatchRequired,
        CommandRequired,
        MatchIdMismatch,
        RevisionConflict,
        ContainerNotFound,
        ContainerNotMovable,
        PlacementNotFound,
        RevisionOverflow,
        PhysicalSurfaceRequired,
    }

    public readonly struct MoveContainerResult : IEquatable<MoveContainerResult>
    {
        private MoveContainerResult(CommandResult commandResult, MoveContainerError error)
        {
            CommandResult = commandResult;
            Error = error;
        }

        public CommandResult CommandResult { get; }

        public MoveContainerError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public CommandResultStatus Status => CommandResult.Status;

        public long Revision => CommandResult.Revision;

        public static MoveContainerResult Accepted(long revision)
        {
            return new MoveContainerResult(CommandResult.Accepted(revision), MoveContainerError.None);
        }

        public static MoveContainerResult Failure(CommandResultStatus status, MoveContainerError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Move Container failure must use a non-Accepted status.", nameof(status));
            }

            if (error == MoveContainerError.None)
            {
                throw new ArgumentException("Move Container failure must include an error.", nameof(error));
            }

            return new MoveContainerResult(CommandResult.Failure(status), error);
        }

        public bool Equals(MoveContainerResult other)
        {
            return CommandResult.Equals(other.CommandResult) && Error == other.Error;
        }

        public override bool Equals(object obj) => obj is MoveContainerResult other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(CommandResult, Error);

        public static bool operator ==(MoveContainerResult left, MoveContainerResult right) => left.Equals(right);

        public static bool operator !=(MoveContainerResult left, MoveContainerResult right) => !left.Equals(right);
    }
}
