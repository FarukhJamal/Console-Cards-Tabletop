using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Boards
{
    /// <summary>
    /// The grid of card spaces on a game board mat (doc 22 §15): columns × rows of spaces, the size of one space,
    /// the gap between spaces and the frame around them. Virtual only: the physical mat stays the same.
    /// Hex (doc 62, owner 2026-11-21): the spaces are regular hexagons, points to the near and far sides, as tall as
    /// <see cref="SpaceDepth"/>; every other row is shifted half a space to the right. A space keeps its (column, row).
    /// </summary>
    public readonly struct BoardGrid : IEquatable<BoardGrid>
    {
        public const int MaximumColumns = 30;
        public const int MaximumRows = 30;

        /// <summary>A regular hexagon's width (flat side to flat side) for its height (point to point): √3 / 2.</summary>
        public const float HexWidthShare = 0.8660254f;

        public BoardGrid(int columns, int rows, float spaceWidth, float spaceDepth, float gap, float frame, bool hex = false)
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
            Hex = hex;
        }

        /// <summary>The owner's reference mat: 10 × 5 card spaces.</summary>
        public static BoardGrid Standard => new BoardGrid(10, 5, 1.1f, 1.5f, 0.1f, 0.35f);

        public int Columns { get; }
        public int Rows { get; }
        public float SpaceWidth { get; }
        public float SpaceDepth { get; }
        public float Gap { get; }
        public float Frame { get; }
        /// <summary>Hexagon spaces (doc 62) instead of rectangles.</summary>
        public bool Hex { get; }
        public bool IsValid => Columns >= 1 && Rows >= 1 && SpaceWidth > 0f && SpaceDepth > 0f;

        /// <summary>A hex space's width (flat side to flat side); a rectangle space's width otherwise.</summary>
        public float CellWidth => Hex ? SpaceDepth * HexWidthShare : SpaceWidth;
        private float PitchX => CellWidth + Gap;
        // Hex rows overlap by a quarter of a space: the next row's points sit in the gaps of this one.
        private float PitchZ => Hex ? (0.75f * SpaceDepth) + (Gap * HexWidthShare) : SpaceDepth + Gap;
        private bool Shifted => Hex && Rows > 1;

        /// <summary>Width of the spaces area (no frame).</summary>
        public float GridWidth => (Columns * CellWidth) + ((Columns - 1) * Gap) + (Shifted ? 0.5f * PitchX : 0f);
        public float GridDepth => SpaceDepth + ((Rows - 1) * PitchZ);
        /// <summary>Outer size of the mat, frame included.</summary>
        public float Width => GridWidth + (2f * Frame);
        public float Depth => GridDepth + (2f * Frame);

        public BoardGrid WithSize(int columns, int rows) => new BoardGrid(columns, rows, SpaceWidth, SpaceDepth, Gap, Frame, Hex);
        public BoardGrid WithSpace(float width, float depth, float gap) => new BoardGrid(Columns, Rows, width, depth, gap, Frame, Hex);
        public BoardGrid WithHex(bool hex) => new BoardGrid(Columns, Rows, SpaceWidth, SpaceDepth, Gap, Frame, hex);

        /// <summary>Board-local centre of space (column, row), both from 0; +x right, +z away from the near edge.</summary>
        public void SpaceCentre(int column, int row, out float x, out float z)
        {
            x = (-GridWidth * 0.5f) + (CellWidth * 0.5f) + (column * PitchX) + (Shifted && (row & 1) == 1 ? 0.5f * PitchX : 0f);
            z = (-GridDepth * 0.5f) + (SpaceDepth * 0.5f) + (row * PitchZ);
        }

        /// <summary>The space nearest a board-local point, and whether the point lies on the spaces area.</summary>
        public bool TryNearestSpace(float x, float z, out int column, out int row)
        {
            bool inside = Math.Abs(x) <= GridWidth * 0.5f && Math.Abs(z) <= GridDepth * 0.5f;
            float v = (z + (GridDepth * 0.5f) - (SpaceDepth * 0.5f)) / PitchZ;
            row = Clamp((int)Math.Round(v), Rows);
            column = NearestColumn(x, row);
            if (!Hex) return inside;

            // Hex rows overlap: the nearest centre among this row and the rows on either side.
            SpaceCentre(column, row, out float cx, out float cz);
            float best = Squared(x - cx, z - cz);
            for (int r = row - 1; r <= row + 1; r += 2)
            {
                if (r < 0 || r >= Rows) continue;
                int c = NearestColumn(x, r);
                SpaceCentre(c, r, out cx, out cz);
                float distance = Squared(x - cx, z - cz);
                if (distance >= best) continue;
                best = distance;
                row = r;
                column = c;
            }

            return inside;
        }

        private int NearestColumn(float x, int row)
        {
            float shift = Shifted && (row & 1) == 1 ? 0.5f * PitchX : 0f;
            float u = (x + (GridWidth * 0.5f) - (CellWidth * 0.5f) - shift) / PitchX;
            return Clamp((int)Math.Round(u), Columns);
        }

        private static int Clamp(int value, int count) => value < 0 ? 0 : value >= count ? count - 1 : value;

        private static float Squared(float x, float z) => (x * x) + (z * z);

        public bool Equals(BoardGrid other)
        {
            return Columns == other.Columns && Rows == other.Rows && SpaceWidth.Equals(other.SpaceWidth)
                && SpaceDepth.Equals(other.SpaceDepth) && Gap.Equals(other.Gap) && Frame.Equals(other.Frame)
                && Hex == other.Hex;
        }

        public override bool Equals(object obj) => obj is BoardGrid other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (Columns * 397) ^ Rows;
                hash = (hash * 397) ^ SpaceWidth.GetHashCode();
                hash = (hash * 397) ^ SpaceDepth.GetHashCode();
                return Hex ? hash ^ 0x5bd1e995 : hash;
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
