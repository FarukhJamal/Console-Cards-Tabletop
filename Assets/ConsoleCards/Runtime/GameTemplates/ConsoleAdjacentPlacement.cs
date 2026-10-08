using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.GameTemplates
{
    /// <summary>Which side of a Console a piece is placed on (console-local -x or +x).</summary>
    public enum ConsoleSide
    {
        Left = -1,
        Right = 1
    }

    /// <summary>An axis-aligned rectangle in console-local space (+x right, +z toward the board).</summary>
    public readonly struct ConsoleLocalRect
    {
        public ConsoleLocalRect(double centerX, double centerZ, double width, double depth)
        {
            if (!IsFinite(centerX) || !IsFinite(centerZ))
                throw new ArgumentOutOfRangeException(nameof(centerX), "Console-local rectangle centre must be finite.");
            if (!IsFinite(width) || width <= 0d || !IsFinite(depth) || depth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(width), "Console-local rectangle size must be finite and above zero.");

            CenterX = centerX;
            CenterZ = centerZ;
            Width = width;
            Depth = depth;
        }

        public double CenterX { get; }
        public double CenterZ { get; }
        public double Width { get; }
        public double Depth { get; }
        public double MinX => CenterX - (Width * 0.5d);
        public double MaxX => CenterX + (Width * 0.5d);
        public double MinZ => CenterZ - (Depth * 0.5d);
        public double MaxZ => CenterZ + (Depth * 0.5d);

        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Parameters for placing pieces beside a Console. Gap and piece size are data here, not game constants.
    /// Standard: 0.2 gap, a 1.0 x 1.4 portrait card pile, at least 0.15 clearance and 0.1 table margin.
    /// </summary>
    public sealed class ConsoleAdjacentPlacementSettings
    {
        public static ConsoleAdjacentPlacementSettings Standard { get; } =
            new ConsoleAdjacentPlacementSettings(0.2d, 1.0d, 1.4d, 0.15d, 0.1d);

        public ConsoleAdjacentPlacementSettings(
            double gap,
            double pieceWidth,
            double pieceDepth,
            double minimumClearance,
            double minimumTableMargin)
        {
            if (!ConsoleLocalRect.IsFinite(gap) || gap < 0d)
                throw new ArgumentOutOfRangeException(nameof(gap));
            if (!ConsoleLocalRect.IsFinite(pieceWidth) || pieceWidth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(pieceWidth));
            if (!ConsoleLocalRect.IsFinite(pieceDepth) || pieceDepth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(pieceDepth));
            if (!ConsoleLocalRect.IsFinite(minimumClearance) || minimumClearance < 0d)
                throw new ArgumentOutOfRangeException(nameof(minimumClearance));
            if (!ConsoleLocalRect.IsFinite(minimumTableMargin) || minimumTableMargin < 0d)
                throw new ArgumentOutOfRangeException(nameof(minimumTableMargin));

            Gap = gap;
            PieceWidth = pieceWidth;
            PieceDepth = pieceDepth;
            MinimumClearance = minimumClearance;
            MinimumTableMargin = minimumTableMargin;
        }

        public double Gap { get; }
        public double PieceWidth { get; }
        public double PieceDepth { get; }
        public double MinimumClearance { get; }
        public double MinimumTableMargin { get; }
    }

    /// <summary>One rectangle in table space taking part in the placement check.</summary>
    public readonly struct ConsoleAdjacentPlacementItem
    {
        public ConsoleAdjacentPlacementItem(string label, int seatIndex, TabletopBounds bounds, bool checkTableMargin)
        {
            Label = string.IsNullOrWhiteSpace(label) ? throw new ArgumentException("Placement item label is required.", nameof(label)) : label;
            SeatIndex = seatIndex;
            Bounds = bounds;
            CheckTableMargin = checkTableMargin;
        }

        /// <summary>Items with the same seat index and label are parts of one shape and are not checked against each other.</summary>
        public string Label { get; }
        public int SeatIndex { get; }
        public TabletopBounds Bounds { get; }
        public bool CheckTableMargin { get; }
    }

    /// <summary>Result of <see cref="ConsoleAdjacentPlacement.Check"/>.</summary>
    public sealed class ConsoleAdjacentPlacementReport
    {
        internal ConsoleAdjacentPlacementReport(
            double minimumClearance,
            string closestFirst,
            string closestSecond,
            double minimumTableMargin,
            string leastMarginItem,
            bool passes)
        {
            MinimumClearance = minimumClearance;
            ClosestFirst = closestFirst;
            ClosestSecond = closestSecond;
            MinimumTableMargin = minimumTableMargin;
            LeastMarginItem = leastMarginItem;
            Passes = passes;
        }

        /// <summary>Smallest gap between any two checked shapes (negative when they overlap).</summary>
        public double MinimumClearance { get; }
        public string ClosestFirst { get; }
        public string ClosestSecond { get; }
        /// <summary>Smallest distance from a margin-checked item to the table edge; +infinity without table bounds.</summary>
        public double MinimumTableMargin { get; }
        public string LeastMarginItem { get; }
        public bool Passes { get; }

        public string Describe()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "closest pair '{0}' / '{1}' at {2:0.000}; least table margin '{3}' at {4:0.000}",
                ClosestFirst ?? "-",
                ClosestSecond ?? "-",
                MinimumClearance,
                LeastMarginItem ?? "-",
                MinimumTableMargin);
        }
    }

    /// <summary>
    /// Places pieces (decks, stacks, piles, staged cards) beside a Console from the Console's real shape:
    /// the mat plus every Card slot footprint of its layout. A piece goes outward from the Console (or from an
    /// inner piece on the same side) by the settings gap, within the z band it occupies. Without a layout the
    /// worst-case Console box (doc 19 section 4.2) is used. Pure data: no Unity, no game rules.
    /// </summary>
    public sealed class ConsoleAdjacentPlacement
    {
        /// <summary>Worst-case Console footprint with a 1.4 card in every edge slot (doc 19 section 4.2).</summary>
        public const double FallbackConsoleWidth = 7.3506d;
        public const double FallbackConsoleDepth = 4.9622d;

        private readonly ReadOnlyCollection<ConsoleLocalRect> consoleShape;

        public ConsoleAdjacentPlacement(ConsoleLayoutData consoleLayout, ConsoleAdjacentPlacementSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            List<ConsoleLocalRect> shape = new List<ConsoleLocalRect>();
            UsesFallbackShape = consoleLayout == null;
            if (consoleLayout == null)
            {
                shape.Add(new ConsoleLocalRect(0d, 0d, FallbackConsoleWidth, FallbackConsoleDepth));
            }
            else
            {
                shape.Add(new ConsoleLocalRect(0d, 0d, consoleLayout.MatWidth, consoleLayout.MatDepth));
                for (int i = 0; i < consoleLayout.CardSlots.Count; i++)
                {
                    ConsoleLayoutSlotData slot = consoleLayout.CardSlots[i];
                    shape.Add(new ConsoleLocalRect(slot.X, slot.Z, slot.FootprintWidth, slot.FootprintDepth));
                }
            }

            consoleShape = new ReadOnlyCollection<ConsoleLocalRect>(shape);
        }

        public ConsoleAdjacentPlacementSettings Settings { get; }

        /// <summary>
        /// True without a layout: the shape is the worst-case box, whose corners overlap neighbouring Consoles
        /// in a four-seat ring, so a placement check against it is not meaningful.
        /// </summary>
        public bool UsesFallbackShape { get; }

        /// <summary>The Console's shape in console-local space: the mat and its Card slot footprints.</summary>
        public IReadOnlyList<ConsoleLocalRect> ConsoleShape => consoleShape;

        /// <summary>
        /// One piece of the settings size centred at alongZ, the gap beyond the Console's outer edge on that side,
        /// or beyond an inner piece on the same side when the two share a z band.
        /// </summary>
        public ConsoleLocalRect PlaceBeside(ConsoleSide side, double alongZ, ConsoleLocalRect? innerPiece = null)
        {
            if (!ConsoleLocalRect.IsFinite(alongZ)) throw new ArgumentOutOfRangeException(nameof(alongZ));
            double halfDepth = Settings.PieceDepth * 0.5d;
            double edge = OuterEdge(side, alongZ - halfDepth, alongZ + halfDepth, innerPiece);
            double distance = edge + Settings.Gap + (Settings.PieceWidth * 0.5d);
            return new ConsoleLocalRect((int)side * distance, alongZ, Settings.PieceWidth, Settings.PieceDepth);
        }

        /// <summary>A row of pieces going outward along x, each the gap apart, the first placed as by PlaceBeside.</summary>
        public IReadOnlyList<ConsoleLocalRect> PlaceRow(
            ConsoleSide side,
            double alongZ,
            int count,
            ConsoleLocalRect? innerPiece = null)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            List<ConsoleLocalRect> row = new List<ConsoleLocalRect>(count);
            ConsoleLocalRect? previous = innerPiece;
            for (int i = 0; i < count; i++)
            {
                ConsoleLocalRect piece = PlaceBeside(side, alongZ, previous);
                row.Add(piece);
                previous = piece;
            }

            return row;
        }

        /// <summary>Table pose of a console-local rectangle's centre, keeping the Console's yaw.</summary>
        public static TabletopPose ToTablePose(TabletopPose consolePose, ConsoleLocalRect rect)
        {
            return new TabletopPose(
                ToTableCoordinate(consolePose, rect.CenterX, rect.CenterZ),
                consolePose.RotationDegrees,
                consolePose.Layer,
                consolePose.LocalOrder);
        }

        /// <summary>Axis-aligned table bounds of a console-local rectangle under the Console's pose.</summary>
        public static TabletopBounds ToTableBounds(TabletopPose consolePose, ConsoleLocalRect rect)
        {
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            for (int corner = 0; corner < 4; corner++)
            {
                double x = (corner & 1) == 0 ? rect.MinX : rect.MaxX;
                double z = (corner & 2) == 0 ? rect.MinZ : rect.MaxZ;
                TableCoordinate point = ToTableCoordinate(consolePose, x, z);
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new TabletopBounds(new TableCoordinate(minX, minY), new TableCoordinate(maxX, maxY));
        }

        /// <summary>
        /// Checks every pair of items (except parts of one shape) for clearance, and every margin-checked item for
        /// its distance to the table edge. Passes when clearance and margin meet the settings minimums.
        /// </summary>
        public static ConsoleAdjacentPlacementReport Check(
            IReadOnlyList<ConsoleAdjacentPlacementItem> items,
            TabletopBounds? tableBounds,
            ConsoleAdjacentPlacementSettings settings)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            double minimumClearance = double.PositiveInfinity;
            string closestFirst = null;
            string closestSecond = null;
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    ConsoleAdjacentPlacementItem first = items[i];
                    ConsoleAdjacentPlacementItem second = items[j];
                    if (first.SeatIndex == second.SeatIndex
                        && string.Equals(first.Label, second.Label, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    double clearance = Clearance(first.Bounds, second.Bounds);
                    if (clearance < minimumClearance)
                    {
                        minimumClearance = clearance;
                        closestFirst = Describe(first);
                        closestSecond = Describe(second);
                    }
                }
            }

            double minimumMargin = double.PositiveInfinity;
            string leastMarginItem = null;
            if (tableBounds.HasValue)
            {
                TabletopBounds table = tableBounds.Value;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!items[i].CheckTableMargin) continue;
                    TabletopBounds bounds = items[i].Bounds;
                    double margin = Math.Min(
                        Math.Min(bounds.Minimum.X - table.Minimum.X, table.Maximum.X - bounds.Maximum.X),
                        Math.Min(bounds.Minimum.Y - table.Minimum.Y, table.Maximum.Y - bounds.Maximum.Y));
                    if (margin < minimumMargin)
                    {
                        minimumMargin = margin;
                        leastMarginItem = Describe(items[i]);
                    }
                }
            }

            bool passes = minimumClearance >= settings.MinimumClearance
                && minimumMargin >= settings.MinimumTableMargin;
            return new ConsoleAdjacentPlacementReport(
                minimumClearance,
                closestFirst,
                closestSecond,
                minimumMargin,
                leastMarginItem,
                passes);
        }

        private double OuterEdge(ConsoleSide side, double minZ, double maxZ, ConsoleLocalRect? innerPiece)
        {
            double edge = 0d;
            for (int i = 0; i < consoleShape.Count; i++)
            {
                edge = Math.Max(edge, SideExtent(consoleShape[i], side, minZ, maxZ));
            }

            if (innerPiece.HasValue)
            {
                edge = Math.Max(edge, SideExtent(innerPiece.Value, side, minZ, maxZ));
            }

            return edge;
        }

        // How far a rectangle reaches toward the given side, when it overlaps the z band; 0 otherwise.
        private static double SideExtent(ConsoleLocalRect rect, ConsoleSide side, double minZ, double maxZ)
        {
            if (rect.MaxZ <= minZ || rect.MinZ >= maxZ) return 0d;
            return side == ConsoleSide.Right ? rect.MaxX : -rect.MinX;
        }

        // Console-local (x right, z toward the board) to table coordinates under the Console's pose.
        private static TableCoordinate ToTableCoordinate(TabletopPose consolePose, double localX, double localZ)
        {
            double radians = consolePose.RotationDegrees * (Math.PI / 180d);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new TableCoordinate(
                consolePose.Position.X + (cos * localX) + (sin * localZ),
                consolePose.Position.Y - (sin * localX) + (cos * localZ));
        }

        // Gap between two bounds; negative (the smaller overlap extent) when they overlap.
        private static double Clearance(TabletopBounds first, TabletopBounds second)
        {
            double gapX = Math.Max(first.Minimum.X, second.Minimum.X) - Math.Min(first.Maximum.X, second.Maximum.X);
            double gapY = Math.Max(first.Minimum.Y, second.Minimum.Y) - Math.Min(first.Maximum.Y, second.Maximum.Y);
            if (gapX <= 0d && gapY <= 0d)
            {
                return Math.Max(gapX, gapY);
            }

            return Math.Sqrt((Math.Max(0d, gapX) * Math.Max(0d, gapX)) + (Math.Max(0d, gapY) * Math.Max(0d, gapY)));
        }

        private static string Describe(ConsoleAdjacentPlacementItem item)
        {
            return item.SeatIndex < 0 ? item.Label : $"seat {item.SeatIndex + 1} {item.Label}";
        }
    }
}
