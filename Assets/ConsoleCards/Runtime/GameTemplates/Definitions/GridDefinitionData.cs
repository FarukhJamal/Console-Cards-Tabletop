using System;

namespace ConsoleCards.GameTemplates.Definitions
{
    [Serializable]
    public sealed class GridDefinitionData
    {
        public GridDefinitionData(
            string stableId,
            int rows,
            int columns,
            double cellWidth,
            double cellHeight,
            double horizontalSpacing,
            double verticalSpacing,
            double originX,
            double originY,
            string layoutMetadata)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Grid ID is required.", nameof(stableId));
            if (rows < 1) throw new ArgumentOutOfRangeException(nameof(rows));
            if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns));
            if (cellWidth <= 0d) throw new ArgumentOutOfRangeException(nameof(cellWidth));
            if (cellHeight <= 0d) throw new ArgumentOutOfRangeException(nameof(cellHeight));
            if (horizontalSpacing < 0d) throw new ArgumentOutOfRangeException(nameof(horizontalSpacing));
            if (verticalSpacing < 0d) throw new ArgumentOutOfRangeException(nameof(verticalSpacing));

            StableId = stableId;
            Rows = rows;
            Columns = columns;
            CellWidth = cellWidth;
            CellHeight = cellHeight;
            HorizontalSpacing = horizontalSpacing;
            VerticalSpacing = verticalSpacing;
            OriginX = originX;
            OriginY = originY;
            LayoutMetadata = layoutMetadata ?? string.Empty;
        }

        public string StableId { get; }
        public int Rows { get; }
        public int Columns { get; }
        public double CellWidth { get; }
        public double CellHeight { get; }
        public double HorizontalSpacing { get; }
        public double VerticalSpacing { get; }
        public double OriginX { get; }
        public double OriginY { get; }
        public string LayoutMetadata { get; }
        public double ColumnPitch => CellWidth + HorizontalSpacing;
        public double RowPitch => CellHeight + VerticalSpacing;
        public int CellCount => checked(Rows * Columns);
    }
}
