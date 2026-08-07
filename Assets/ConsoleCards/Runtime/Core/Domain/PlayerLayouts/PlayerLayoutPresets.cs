using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.PlayerLayouts
{
    public static class PlayerLayoutPresets
    {
        public static PlayerLayoutDefinition StandardFourPlayer { get; } = CreateStandardFourPlayer();

        public static PlayerLayoutDefinition CompactFourPlayer { get; } = CreateCompactFourPlayer();

        public static PlayerLayoutDefinition EightPlayer { get; } = CreateEightPlayer();

        private static PlayerLayoutDefinition CreateStandardFourPlayer()
        {
            return new PlayerLayoutDefinition(
                new PlayerLayoutId(new Guid("3e93eec8-e4f1-4d97-b124-724e06128570")),
                "Standard 4-player",
                new[]
                {
                    CreateSeat(0, 0d, -4d, 0d, -1.86d, 0d, -3.86d, 0f),
                    CreateSeat(1, -6d, 0d, -3.2d, 0d, -5.8d, 0d, 90f),
                    CreateSeat(2, 0d, 4d, 0d, 1.86d, 0d, 3.86d, 180f),
                    CreateSeat(3, 6d, 0d, 3.2d, 0d, 5.8d, 0d, 270f),
                });
        }

        private static PlayerLayoutDefinition CreateCompactFourPlayer()
        {
            return new PlayerLayoutDefinition(
                new PlayerLayoutId(new Guid("dd17a66c-8561-42df-88bb-d817637e6cb6")),
                "Compact 4-player",
                new[]
                {
                    CreateSeat(0, 0d, -3.4d, 0d, -1.45d, 0d, -3.2d, 0f),
                    CreateSeat(1, -4.6d, 0d, -2.6d, 0d, -4.4d, 0d, 90f),
                    CreateSeat(2, 0d, 3.4d, 0d, 1.45d, 0d, 3.2d, 180f),
                    CreateSeat(3, 4.6d, 0d, 2.6d, 0d, 4.4d, 0d, 270f),
                });
        }

        private static PlayerLayoutDefinition CreateEightPlayer()
        {
            return new PlayerLayoutDefinition(
                new PlayerLayoutId(new Guid("eb43c57d-eb27-4cf9-b2a2-6bcdb19592f7")),
                "8-player",
                new[]
                {
                    CreateSeat(0, -3d, -4d, -3d, -1.86d, -3d, -3.86d, 0f),
                    CreateSeat(1, 3d, -4d, 3d, -1.86d, 3d, -3.86d, 0f),
                    CreateSeat(2, -6d, -1.7d, -3.2d, -1.7d, -5.8d, -1.7d, 90f),
                    CreateSeat(3, -6d, 1.7d, -3.2d, 1.7d, -5.8d, 1.7d, 90f),
                    CreateSeat(4, 3d, 4d, 3d, 1.86d, 3d, 3.86d, 180f),
                    CreateSeat(5, -3d, 4d, -3d, 1.86d, -3d, 3.86d, 180f),
                    CreateSeat(6, 6d, 1.7d, 3.2d, 1.7d, 5.8d, 1.7d, 270f),
                    CreateSeat(7, 6d, -1.7d, 3.2d, -1.7d, 5.8d, -1.7d, 270f),
                });
        }

        private static PlayerSeatLayoutEntry CreateSeat(
            int seatIndex,
            double playerZoneX,
            double playerZoneY,
            double handX,
            double handY,
            double consoleX,
            double consoleY,
            float facingRotationDegrees)
        {
            return new PlayerSeatLayoutEntry(
                seatIndex,
                CreatePose(playerZoneX, playerZoneY, facingRotationDegrees),
                CreatePose(handX, handY, facingRotationDegrees),
                CreatePose(consoleX, consoleY, facingRotationDegrees),
                facingRotationDegrees);
        }

        private static TabletopPose CreatePose(double x, double y, float rotationDegrees)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, 0, 0);
        }
    }
}
