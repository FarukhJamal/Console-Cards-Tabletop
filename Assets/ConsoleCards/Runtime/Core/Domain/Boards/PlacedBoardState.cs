using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Boards
{
    /// <summary>
    /// The grid of card spaces on a game board mat (doc 22 §15): columns × rows of spaces, the size of one space,
    /// the gap between spaces and the frame around them. Virtual only: the physical mat stays the same.
    /// </summary>
    public readonly struct BoardGrid : IEquatable<BoardGrid>
    {
        public const int MaximumColumns = 30;
        public const int MaximumRows = 30;

        public BoardGrid(int columns, int rows, float spaceWidth, float spaceDepth, float gap, float frame)
        {
            if (columns < 1 || columns > MaximumColumns) throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows < 1 || rows > MaximumRows) throw new ArgumentOutOfRangeException(nameof(rows));
            if (!Positive(spaceWidth)) throw new ArgumentOutOfRangeException(nameof(spaceWidth));
            if (!Positive(spaceDepth)) throw new ArgumentOutOfRangeException(nameof(spaceDepth));
            if (!NonNegative(gap)) throw new ArgumentOutOfRangeException(nameof(gap));
            if (!NonNegative(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            Columns = columns;
            Rows = rows;
            SpaceWidth = spaceWidth;
            SpaceDepth = spaceDepth;
            Gap = gap;
            Frame = frame;
        }

        /// <summary>The owner's reference mat: 10 × 5 card spaces.</summary>
        public static BoardGrid Standard => new BoardGrid(10, 5, 1.1f, 1.5f, 0.1f, 0.35f);

        public int Columns { get; }
        public int Rows { get; }
        public float SpaceWidth { get; }
        public float SpaceDepth { get; }
        public float Gap { get; }
        public float Frame { get; }
        public bool IsValid => Columns >= 1 && Rows >= 1 && SpaceWidth > 0f && SpaceDepth > 0f;

        /// <summary>Width of the spaces area (no frame).</summary>
        public float GridWidth => (Columns * SpaceWidth) + ((Columns - 1) * Gap);
        public float GridDepth => (Rows * SpaceDepth) + ((Rows - 1) * Gap);
        /// <summary>Outer size of the mat, frame included.</summary>
        public float Width => GridWidth + (2f * Frame);
        public float Depth => GridDepth + (2f * Frame);

        public BoardGrid WithSize(int columns, int rows) => new BoardGrid(columns, rows, SpaceWidth, SpaceDepth, Gap, Frame);
        public BoardGrid WithSpace(float width, float depth, float gap) => new BoardGrid(Columns, Rows, width, depth, gap, Frame);

        /// <summary>Board-local centre of space (column, row), both from 0; +x right, +z away from the near edge.</summary>
        public void SpaceCentre(int column, int row, out float x, out float z)
        {
            x = (-GridWidth * 0.5f) + (SpaceWidth * 0.5f) + (column * (SpaceWidth + Gap));
            z = (-GridDepth * 0.5f) + (SpaceDepth * 0.5f) + (row * (SpaceDepth + Gap));
        }

        /// <summary>The space nearest a board-local point, and whether the point lies on the spaces area.</summary>
        public bool TryNearestSpace(float x, float z, out int column, out int row)
        {
            float pitchX = SpaceWidth + Gap;
            float pitchZ = SpaceDepth + Gap;
            float u = (x + (GridWidth * 0.5f) - (SpaceWidth * 0.5f)) / pitchX;
            float v = (z + (GridDepth * 0.5f) - (SpaceDepth * 0.5f)) / pitchZ;
            column = (int)Math.Round(u);
            row = (int)Math.Round(v);
            bool inside = Math.Abs(x) <= GridWidth * 0.5f && Math.Abs(z) <= GridDepth * 0.5f;
            if (column < 0) column = 0;
            if (column >= Columns) column = Columns - 1;
            if (row < 0) row = 0;
            if (row >= Rows) row = Rows - 1;
            return inside;
        }

        public bool Equals(BoardGrid other)
        {
            return Columns == other.Columns && Rows == other.Rows && SpaceWidth.Equals(other.SpaceWidth)
                && SpaceDepth.Equals(other.SpaceDepth) && Gap.Equals(other.Gap) && Frame.Equals(other.Frame);
        }

        public override bool Equals(object obj) => obj is BoardGrid other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (Columns * 397) ^ Rows;
                hash = (hash * 397) ^ SpaceWidth.GetHashCode();
                hash = (hash * 397) ^ SpaceDepth.GetHashCode();
                return hash;
            }
        }

        private static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        private static bool NonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>
    /// A game board mat placed on the table (doc 22 §15): its pose and its grid. Cards on it stay loose table cards;
    /// the spaces are snap points, not containers.
    /// </summary>
    public sealed class PlacedBoardState
    {
        public PlacedBoardState(BoardId id, TabletopPose pose, BoardGrid grid, float? surfaceHeight = null)
        {
            if (id.IsEmpty) throw new ArgumentException("Board ID cannot be empty.", nameof(id));
            Id = id;
            SetPose(pose, surfaceHeight);
            SetGrid(grid);
        }

        public BoardId Id { get; }
        public TabletopPose Pose { get; private set; }
        public float? SurfaceHeight { get; private set; }
        public BoardGrid Grid { get; private set; }

        public void SetPose(TabletopPose pose, float? surfaceHeight = null)
        {
            if (double.IsNaN(pose.Position.X) || double.IsInfinity(pose.Position.X)
                || double.IsNaN(pose.Position.Y) || double.IsInfinity(pose.Position.Y)
                || float.IsNaN(pose.RotationDegrees) || float.IsInfinity(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(pose), "Board pose must be finite.");
            }

            if (surfaceHeight.HasValue && (float.IsNaN(surfaceHeight.Value) || float.IsInfinity(surfaceHeight.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(surfaceHeight));
            }

            Pose = pose;
            SurfaceHeight = surfaceHeight;
        }

        public void SetGrid(BoardGrid grid)
        {
            if (!grid.IsValid) throw new ArgumentException("Board grid is invalid.", nameof(grid));
            Grid = grid;
        }
    }
}
