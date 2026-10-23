using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// SETUP-1 (owner, 2026-10-23: "the whole default setup ... the console mat, the board placement, the dice"):
    /// the game's default table setup, made from its Table Setup asset. Every place is absolute, in table units from
    /// the table's centre (x to the right of Player 1, y away from Player 1). The players' own saved table layout
    /// (LAYOUT-1) still wins over these places; anything it does not place comes from here.
    /// </summary>
    public sealed class TrapFloorTableSetup
    {
        private readonly TabletopPose[] consoles;

        /// <param name="consoles">One Console place per seat of the 4-player layout, in its seat order.</param>
        public TrapFloorTableSetup(
            double boardMatWidth,
            double boardMatDepth,
            TableCoordinate boardCenter,
            IReadOnlyList<TabletopPose> consoles,
            TabletopPose controllerDeck,
            TabletopPose abilityDeck,
            TabletopPose abilityDiscard,
            TableCoordinate xAxisDie,
            TableCoordinate yAxisDie,
            TableCoordinate directionDie,
            TrapFloorSeatPiecesLayout seatPieces)
        {
            if (double.IsNaN(boardMatWidth) || double.IsInfinity(boardMatWidth) || boardMatWidth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(boardMatWidth));
            if (double.IsNaN(boardMatDepth) || double.IsInfinity(boardMatDepth) || boardMatDepth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(boardMatDepth));
            if (consoles == null) throw new ArgumentNullException(nameof(consoles));
            this.consoles = new TabletopPose[consoles.Count];
            for (int i = 0; i < this.consoles.Length; i++) this.consoles[i] = consoles[i];
            BoardMatWidth = boardMatWidth;
            BoardMatDepth = boardMatDepth;
            BoardCenter = boardCenter;
            ControllerDeck = controllerDeck;
            AbilityDeck = abilityDeck;
            AbilityDiscard = abilityDiscard;
            XAxisDie = xAxisDie;
            YAxisDie = yAxisDie;
            DirectionDie = directionDie;
            SeatPieces = seatPieces ?? TrapFloorSeatPiecesLayout.Default;
        }

        public double BoardMatWidth { get; }
        public double BoardMatDepth { get; }
        public TableCoordinate BoardCenter { get; }
        public int ConsoleCount => consoles.Length;
        public TabletopPose ControllerDeck { get; }
        public TabletopPose AbilityDeck { get; }
        public TabletopPose AbilityDiscard { get; }
        public TableCoordinate XAxisDie { get; }
        public TableCoordinate YAxisDie { get; }

        /// <summary>The d4 the Blind trap rolls for its direction.</summary>
        public TableCoordinate DirectionDie { get; }

        public TrapFloorSeatPiecesLayout SeatPieces { get; }

        /// <summary>The Console place of the 4-player layout's seat <paramref name="standardSeatIndex"/>.</summary>
        public bool TryGetConsole(int standardSeatIndex, out TabletopPose pose)
        {
            bool found = standardSeatIndex >= 0 && standardSeatIndex < consoles.Length;
            pose = found ? consoles[standardSeatIndex] : default;
            return found;
        }
    }
}
