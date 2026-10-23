using System;
using ConsoleCards.GameTemplates;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// SEAT-1: where the pieces beside every Console go, in table units from the Console (towardBoard: along its
    /// depth, more toward the board; outward: along its side, more away from it). Made from the game's Seat Pieces
    /// Layout asset; <see cref="Default"/> holds the built-in places.
    /// </summary>
    public sealed class TrapFloorSeatPiecesLayout
    {
        public static TrapFloorSeatPiecesLayout Default { get; } =
            new TrapFloorSeatPiecesLayout(ConsoleSide.Left, 0.9d, 0d, 0.2d, 0f, 0d, 0d, 0d, 0d);

        public TrapFloorSeatPiecesLayout(
            ConsoleSide startingAbilitiesSide,
            double startingAbilitiesTowardBoard,
            double startingAbilitiesOutward,
            double startingAbilitiesGap,
            float startingAbilitiesTurnDegrees,
            double handZoneTowardBoard,
            double handZoneOutward,
            double actionStackTowardBoard,
            double actionStackOutward)
        {
            if (startingAbilitiesSide != ConsoleSide.Left && startingAbilitiesSide != ConsoleSide.Right)
                throw new ArgumentOutOfRangeException(nameof(startingAbilitiesSide));
            Require(startingAbilitiesTowardBoard, nameof(startingAbilitiesTowardBoard));
            Require(startingAbilitiesOutward, nameof(startingAbilitiesOutward));
            Require(startingAbilitiesGap, nameof(startingAbilitiesGap));
            if (startingAbilitiesGap < 0d) throw new ArgumentOutOfRangeException(nameof(startingAbilitiesGap));
            Require(startingAbilitiesTurnDegrees, nameof(startingAbilitiesTurnDegrees));
            Require(handZoneTowardBoard, nameof(handZoneTowardBoard));
            Require(handZoneOutward, nameof(handZoneOutward));
            Require(actionStackTowardBoard, nameof(actionStackTowardBoard));
            Require(actionStackOutward, nameof(actionStackOutward));

            StartingAbilitiesSide = startingAbilitiesSide;
            StartingAbilitiesTowardBoard = startingAbilitiesTowardBoard;
            StartingAbilitiesOutward = startingAbilitiesOutward;
            StartingAbilitiesGap = startingAbilitiesGap;
            StartingAbilitiesTurnDegrees = startingAbilitiesTurnDegrees;
            HandZoneTowardBoard = handZoneTowardBoard;
            HandZoneOutward = handZoneOutward;
            ActionStackTowardBoard = actionStackTowardBoard;
            ActionStackOutward = actionStackOutward;
        }

        public ConsoleSide StartingAbilitiesSide { get; }
        public double StartingAbilitiesTowardBoard { get; }
        public double StartingAbilitiesOutward { get; }
        public double StartingAbilitiesGap { get; }
        public float StartingAbilitiesTurnDegrees { get; }
        public double HandZoneTowardBoard { get; }
        public double HandZoneOutward { get; }
        public double ActionStackTowardBoard { get; }
        public double ActionStackOutward { get; }

        private static void Require(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
