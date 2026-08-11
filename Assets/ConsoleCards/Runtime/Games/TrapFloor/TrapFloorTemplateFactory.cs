using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Builds the approved deterministic four-Player Trap Floor starting setup.
    /// Two- and three-Player authored Seat mappings remain intentionally unresolved.
    /// </summary>
    public static class TrapFloorTemplateFactory
    {
        public const int MinimumPlayerCount = 2;
        public const int MaximumPlayerCount = 4;
        public const int PrototypePlayerCount = 4;
        public const int BoardAxisSize = 6;
        public const int FloorCardCount = 36;
        public const int FloormasterTrapCardCount = 14;
        public const int FloormasterCoinCardCount = 14;
        public const int FloormasterItemCardCount = 8;
        public const int FloormasterCardCount = 36;
        public const int SharedCoinCount = 50;
        public const int ItemSlotCountPerPlayer = 3;
        public const int ConsoleSlotCountPerPlayer = 6;

        private const double FloorColumnSpacing = 0.72d;
        private const double FloorRowSpacing = 1.0d;
        private const double PlayerConsoleRadius = 6.1d;
        private const double PlayerHandRadius = 4.15d;
        private const double ControllerDeckOffset = 3.2d;
        private const double FloormasterSupportX = 3.2d;
        private const double FloormasterDeckY = 1.15d;
        private const double FloormasterDiscardY = -1.15d;
        private const double FloormasterRevealX = 4.7d;
        private const double FloormasterRevealY = 0d;
        private const double SharedCoinSupplyX = -4.5d;
        private const double SharedCoinSupplyY = -1.08d;
        private const double SharedCoinSpacing = 0.24d;
        private const double FloorfallDiceX = 3.45d;
        private const double FloorfallDiceY = 3.45d;
        private const double FloorfallDiceSpacing = 0.9d;
        private const float PrototypeCameraOrthographicSize = 7.35f;

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer()
        {
            PlayerLayoutDefinition playerLayout = PlayerLayoutPresets.StandardFourPlayer;
            GameTemplateId templateId = new GameTemplateId(CreateGuid(1, 1));
            PlayAreaId boardPlayAreaId = new PlayAreaId(CreateGuid(2, 1));
            ContainerId floormasterDeckId = new ContainerId(CreateGuid(10, 1));
            ContainerId floormasterDiscardId = new ContainerId(CreateGuid(10, 2));
            ContainerId sharedCoinSupplyId = new ContainerId(CreateGuid(10, 3));

            ObjectDefinitionId floorDefinitionId = new ObjectDefinitionId(CreateGuid(20, 1));
            ObjectDefinitionId floormasterTrapDefinitionId = new ObjectDefinitionId(CreateGuid(20, 2));
            ObjectDefinitionId floormasterCoinDefinitionId = new ObjectDefinitionId(CreateGuid(20, 3));
            ObjectDefinitionId floormasterItemDefinitionId = new ObjectDefinitionId(CreateGuid(20, 4));
            ObjectDefinitionId avatarDefinitionId = new ObjectDefinitionId(CreateGuid(20, 5));
            ObjectDefinitionId ruleDefinitionId = new ObjectDefinitionId(CreateGuid(20, 6));
            ObjectDefinitionId modeDefinitionId = new ObjectDefinitionId(CreateGuid(20, 7));
            ObjectDefinitionId pawnDefinitionId = new ObjectDefinitionId(CreateGuid(20, 8));
            ObjectDefinitionId coinDefinitionId = new ObjectDefinitionId(CreateGuid(20, 9));
            ObjectDefinitionId dieDefinitionId = new ObjectDefinitionId(CreateGuid(20, 10));

            List<GameTemplateObjectDefinition> objectDefinitions = new List<GameTemplateObjectDefinition>
            {
                new GameTemplateObjectDefinition(floorDefinitionId, TabletopObjectKind.Card, "Floor Card"),
                new GameTemplateObjectDefinition(floormasterTrapDefinitionId, TabletopObjectKind.Card, "Floormaster Trap Card"),
                new GameTemplateObjectDefinition(floormasterCoinDefinitionId, TabletopObjectKind.Card, "Floormaster Coin Card"),
                new GameTemplateObjectDefinition(floormasterItemDefinitionId, TabletopObjectKind.Card, "Floormaster Item Card"),
                new GameTemplateObjectDefinition(avatarDefinitionId, TabletopObjectKind.Card, "Avatar Card"),
                new GameTemplateObjectDefinition(ruleDefinitionId, TabletopObjectKind.Card, "Rule Card"),
                new GameTemplateObjectDefinition(modeDefinitionId, TabletopObjectKind.Card, "Mode Card"),
                new GameTemplateObjectDefinition(pawnDefinitionId, TabletopObjectKind.Pawn, "Player Pawn"),
                new GameTemplateObjectDefinition(coinDefinitionId, TabletopObjectKind.Token, "Wooden Coin Cube"),
                new GameTemplateObjectDefinition(dieDefinitionId, TabletopObjectKind.Die, "Six-sided Die"),
            };

            List<GameTemplateSeatDefinition> seats = new List<GameTemplateSeatDefinition>(PrototypePlayerCount);
            List<GameTemplateContainerDefinition> containers = new List<GameTemplateContainerDefinition>();
            List<GameTemplateObjectInstanceDefinition> objects = new List<GameTemplateObjectInstanceDefinition>();
            List<GameTemplateContainerMembership> memberships = new List<GameTemplateContainerMembership>();
            List<TrapFloorPlayerSetupDefinition> players = new List<TrapFloorPlayerSetupDefinition>(PrototypePlayerCount);
            Dictionary<TabletopObjectId, string> labels = new Dictionary<TabletopObjectId, string>();
            Dictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds =
                new Dictionary<TrapFloorCoordinate, TabletopObjectId>();

            CreateFloorBoard(floorDefinitionId, floorCardIds, labels, objects);

            List<TabletopObjectId> floormasterCardIds = new List<TabletopObjectId>(FloormasterCardCount);
            CreateFloormasterCards(
                floormasterTrapDefinitionId,
                floormasterCoinDefinitionId,
                floormasterItemDefinitionId,
                floormasterCardIds,
                labels,
                objects);
            Dictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory> floormasterCategories =
                new Dictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory>
                {
                    { floormasterTrapDefinitionId, TrapFloorFloormasterCardCategory.Trap },
                    { floormasterCoinDefinitionId, TrapFloorFloormasterCardCategory.Coin },
                    { floormasterItemDefinitionId, TrapFloorFloormasterCardCategory.Item },
                };

            containers.Add(CreatePlacedContainer(
                floormasterDeckId,
                ContainerKind.Deck,
                new TabletopPose(
                    new TableCoordinate(FloormasterSupportX, FloormasterDeckY),
                    0f,
                    0,
                    0)));
            containers.Add(CreatePlacedContainer(
                floormasterDiscardId,
                ContainerKind.DiscardPile,
                new TabletopPose(
                    new TableCoordinate(FloormasterSupportX, FloormasterDiscardY),
                    0f,
                    0,
                    0)));
            containers.Add(CreateContainer(
                sharedCoinSupplyId,
                ContainerKind.Generic,
                SeatId.Empty,
                ObjectVisibility.Public,
                SharedCoinCount));
            memberships.Add(new GameTemplateContainerMembership(floormasterDeckId, floormasterCardIds));
            memberships.Add(new GameTemplateContainerMembership(
                floormasterDiscardId,
                Array.Empty<TabletopObjectId>()));

            TrapFloorCoordinate[] startingCorners =
            {
                new TrapFloorCoordinate(1, 1),
                new TrapFloorCoordinate(6, 1),
                new TrapFloorCoordinate(6, 6),
                new TrapFloorCoordinate(1, 6),
            };

            for (int seatIndex = 0; seatIndex < PrototypePlayerCount; seatIndex++)
            {
                playerLayout.TryGetSeat(seatIndex, out PlayerSeatLayoutEntry layoutSeat);
                CreatePlayerSetup(
                    seatIndex,
                    layoutSeat,
                    startingCorners[seatIndex],
                    avatarDefinitionId,
                    ruleDefinitionId,
                    modeDefinitionId,
                    pawnDefinitionId,
                    seats,
                    containers,
                    memberships,
                    objects,
                    labels,
                    players);
            }

            List<TabletopObjectId> coinTokenIds = new List<TabletopObjectId>(SharedCoinCount);
            for (int i = 0; i < SharedCoinCount; i++)
            {
                TabletopObjectId tokenId = new TabletopObjectId(CreateGuid(50, i + 1));
                int column = i % 5;
                int row = i / 5;
                objects.Add(new GameTemplateObjectInstanceDefinition(
                    tokenId,
                    coinDefinitionId,
                    TabletopObjectKind.Token,
                    new TabletopPose(
                        new TableCoordinate(
                            SharedCoinSupplyX + (column * SharedCoinSpacing),
                            SharedCoinSupplyY + (row * SharedCoinSpacing)),
                        0f,
                        0,
                        i),
                    SeatId.Empty,
                    ObjectVisibility.Public,
                    true,
                    CardFace.FaceUp));
                coinTokenIds.Add(tokenId);
            }

            memberships.Add(new GameTemplateContainerMembership(sharedCoinSupplyId, coinTokenIds));

            TabletopObjectId floorfallXAxisDieId = new TabletopObjectId(CreateGuid(60, 1));
            TabletopObjectId floorfallYAxisDieId = new TabletopObjectId(CreateGuid(60, 2));
            objects.Add(CreateFloorfallDie(
                floorfallXAxisDieId,
                dieDefinitionId,
                FloorfallDiceX - (FloorfallDiceSpacing * 0.5d),
                FloorfallDiceY));
            objects.Add(CreateFloorfallDie(
                floorfallYAxisDieId,
                dieDefinitionId,
                FloorfallDiceX + (FloorfallDiceSpacing * 0.5d),
                FloorfallDiceY));

            TabletopBounds boardBounds = new TabletopBounds(
                new TableCoordinate(-2.35d, -3.1d),
                new TableCoordinate(2.35d, 3.1d));
            TabletopBounds boardFocus = new TabletopBounds(
                new TableCoordinate(-2.15d, -2.85d),
                new TableCoordinate(2.15d, 2.85d));
            GameTemplate template = new GameTemplate(
                templateId,
                GameTemplate.CurrentSchemaVersion,
                "Trap Floor",
                "Approved four-Player Trap Floor starting setup. Gameplay rules are supplied separately.",
                playerLayout.Id,
                PrototypePlayerCount,
                seats,
                containers,
                objects,
                memberships,
                new[]
                {
                    new GameTemplatePlayAreaDefinition(boardPlayAreaId, boardBounds, boardFocus),
                },
                new[]
                {
                    new GameTemplateCameraBookmarkDefinition(
                        "Trap Floor Tabletop",
                        boardFocus.Center,
                        PrototypeCameraOrthographicSize),
                });
            GameTemplateContentCatalog catalog = new GameTemplateContentCatalog(
                objectDefinitions,
                new[]
                {
                    PlayerLayoutPresets.StandardFourPlayer,
                    PlayerLayoutPresets.CompactFourPlayer,
                    PlayerLayoutPresets.EightPlayer,
                });

            return new TrapFloorTemplateDefinition(
                template,
                catalog,
                playerLayout,
                boardPlayAreaId,
                floormasterDeckId,
                floormasterDiscardId,
                new TabletopPose(
                    new TableCoordinate(FloormasterRevealX, FloormasterRevealY),
                    0f,
                    4,
                    0),
                sharedCoinSupplyId,
                floorCardIds,
                floormasterCategories,
                floormasterCardIds,
                labels,
                players,
                coinTokenIds,
                floorfallXAxisDieId,
                floorfallYAxisDieId);
        }

        private static GameTemplateObjectInstanceDefinition CreateFloorfallDie(
            TabletopObjectId objectId,
            ObjectDefinitionId definitionId,
            double tableX,
            double tableY)
        {
            return new GameTemplateObjectInstanceDefinition(
                objectId,
                definitionId,
                TabletopObjectKind.Die,
                new TabletopPose(new TableCoordinate(tableX, tableY), 0f, 0, 0),
                SeatId.Empty,
                ObjectVisibility.Public,
                false,
                CardFace.FaceUp,
                TrapFloorFloorfallService.DieSideCount,
                1);
        }

        private static void CreateFloorBoard(
            ObjectDefinitionId definitionId,
            IDictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds,
            IDictionary<TabletopObjectId, string> labels,
            ICollection<GameTemplateObjectInstanceDefinition> objects)
        {
            int objectIndex = 0;
            for (int y = TrapFloorCoordinate.MinimumAxisValue; y <= TrapFloorCoordinate.MaximumAxisValue; y++)
            {
                for (int x = TrapFloorCoordinate.MinimumAxisValue; x <= TrapFloorCoordinate.MaximumAxisValue; x++)
                {
                    TrapFloorCoordinate coordinate = new TrapFloorCoordinate(x, y);
                    TabletopObjectId objectId = new TabletopObjectId(CreateGuid(30, ++objectIndex));
                    double tableX = (x - 3.5d) * FloorColumnSpacing;
                    double tableY = (y - 3.5d) * FloorRowSpacing;
                    objects.Add(new GameTemplateObjectInstanceDefinition(
                        objectId,
                        definitionId,
                        TabletopObjectKind.Card,
                        new TabletopPose(new TableCoordinate(tableX, tableY), 0f, 2, objectIndex),
                        SeatId.Empty,
                        ObjectVisibility.Public,
                        true,
                        CardFace.FaceUp));
                    floorCardIds.Add(coordinate, objectId);
                    labels.Add(objectId, $"{x},{y}");
                }
            }
        }

        private static void CreateFloormasterCards(
            ObjectDefinitionId trapDefinitionId,
            ObjectDefinitionId coinDefinitionId,
            ObjectDefinitionId itemDefinitionId,
            ICollection<TabletopObjectId> orderedCardIds,
            IDictionary<TabletopObjectId, string> labels,
            ICollection<GameTemplateObjectInstanceDefinition> objects)
        {
            for (int i = 0; i < FloormasterCardCount; i++)
            {
                ObjectDefinitionId definitionId;
                string label;
                if (i < FloormasterTrapCardCount)
                {
                    definitionId = trapDefinitionId;
                    label = "TRAP";
                }
                else if (i < FloormasterTrapCardCount + FloormasterCoinCardCount)
                {
                    definitionId = coinDefinitionId;
                    label = "COIN";
                }
                else
                {
                    definitionId = itemDefinitionId;
                    label = "ITEM";
                }

                TabletopObjectId objectId = new TabletopObjectId(CreateGuid(31, i + 1));
                objects.Add(new GameTemplateObjectInstanceDefinition(
                    objectId,
                    definitionId,
                    TabletopObjectKind.Card,
                    new TabletopPose(
                        new TableCoordinate(FloormasterSupportX, FloormasterDeckY),
                        0f,
                        0,
                        i),
                    SeatId.Empty,
                    ObjectVisibility.Public,
                    false,
                    CardFace.FaceDown));
                orderedCardIds.Add(objectId);
                labels.Add(objectId, label);
            }
        }

        private static void CreatePlayerSetup(
            int seatIndex,
            PlayerSeatLayoutEntry layoutSeat,
            TrapFloorCoordinate startingCorner,
            ObjectDefinitionId avatarDefinitionId,
            ObjectDefinitionId ruleDefinitionId,
            ObjectDefinitionId modeDefinitionId,
            ObjectDefinitionId pawnDefinitionId,
            ICollection<GameTemplateSeatDefinition> seats,
            ICollection<GameTemplateContainerDefinition> containers,
            ICollection<GameTemplateContainerMembership> memberships,
            ICollection<GameTemplateObjectInstanceDefinition> objects,
            IDictionary<TabletopObjectId, string> labels,
            ICollection<TrapFloorPlayerSetupDefinition> players)
        {
            int playerNumber = seatIndex + 1;
            SeatId seatId = new SeatId(CreateGuid(40, playerNumber));
            ContainerId handId = new ContainerId(CreateGuid(41, (seatIndex * 10) + 1));
            ContainerId mainSlotId = new ContainerId(CreateGuid(41, (seatIndex * 10) + 2));
            ContainerId ruleSlotId = new ContainerId(CreateGuid(41, (seatIndex * 10) + 3));
            ContainerId modeSlotId = new ContainerId(CreateGuid(41, (seatIndex * 10) + 4));
            ContainerId[] itemSlotIds =
            {
                new ContainerId(CreateGuid(41, (seatIndex * 10) + 5)),
                new ContainerId(CreateGuid(41, (seatIndex * 10) + 6)),
                new ContainerId(CreateGuid(41, (seatIndex * 10) + 7)),
            };
            ContainerId controllerDeckId = new ContainerId(CreateGuid(41, (seatIndex * 10) + 8));
            ContainerId coinStorageId = new ContainerId(CreateGuid(41, (seatIndex * 10) + 9));

            ContainerId[] consoleSlotIds =
            {
                mainSlotId,
                ruleSlotId,
                modeSlotId,
                itemSlotIds[0],
                itemSlotIds[1],
                itemSlotIds[2],
            };
            seats.Add(new GameTemplateSeatDefinition(seatId, seatIndex, handId, consoleSlotIds));
            containers.Add(CreateContainer(handId, ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, 10));
            for (int i = 0; i < consoleSlotIds.Length; i++)
            {
                containers.Add(CreateContainer(
                    consoleSlotIds[i],
                    ContainerKind.ConsoleSlot,
                    seatId,
                    ObjectVisibility.Public,
                    1));
            }

            TabletopPose controllerDeckPose = OffsetBesideConsole(
                GetConsolePose(layoutSeat),
                ControllerDeckOffset);
            containers.Add(new GameTemplateContainerDefinition(
                controllerDeckId,
                ContainerKind.Deck,
                seatId,
                ObjectVisibility.Public,
                0,
                true,
                controllerDeckPose));
            containers.Add(CreateContainer(
                coinStorageId,
                ContainerKind.Generic,
                seatId,
                ObjectVisibility.Public,
                SharedCoinCount));

            TabletopObjectId avatarId = new TabletopObjectId(CreateGuid(42, playerNumber));
            TabletopObjectId ruleId = new TabletopObjectId(CreateGuid(43, playerNumber));
            TabletopObjectId modeId = new TabletopObjectId(CreateGuid(44, playerNumber));
            TabletopObjectId pawnId = new TabletopObjectId(CreateGuid(45, playerNumber));
            objects.Add(CreatePlayerCard(avatarId, avatarDefinitionId, seatId));
            objects.Add(CreatePlayerCard(ruleId, ruleDefinitionId, seatId));
            objects.Add(CreatePlayerCard(modeId, modeDefinitionId, seatId));
            labels.Add(avatarId, $"P{playerNumber}\nAVATAR");
            labels.Add(ruleId, $"P{playerNumber}\nRULE");
            labels.Add(modeId, $"P{playerNumber}\nMODE");

            TabletopPose pawnPose = CreateFloorPose(startingCorner, 6, playerNumber);
            objects.Add(new GameTemplateObjectInstanceDefinition(
                pawnId,
                pawnDefinitionId,
                TabletopObjectKind.Pawn,
                pawnPose,
                seatId,
                ObjectVisibility.Public,
                false,
                CardFace.FaceUp));

            memberships.Add(new GameTemplateContainerMembership(mainSlotId, new[] { avatarId }));
            memberships.Add(new GameTemplateContainerMembership(ruleSlotId, new[] { ruleId }));
            memberships.Add(new GameTemplateContainerMembership(modeSlotId, new[] { modeId }));
            for (int i = 0; i < itemSlotIds.Length; i++)
            {
                memberships.Add(new GameTemplateContainerMembership(itemSlotIds[i], Array.Empty<TabletopObjectId>()));
            }

            memberships.Add(new GameTemplateContainerMembership(controllerDeckId, Array.Empty<TabletopObjectId>()));
            memberships.Add(new GameTemplateContainerMembership(coinStorageId, Array.Empty<TabletopObjectId>()));
            memberships.Add(new GameTemplateContainerMembership(handId, Array.Empty<TabletopObjectId>()));

            players.Add(new TrapFloorPlayerSetupDefinition(
                seatIndex,
                seatId,
                handId,
                mainSlotId,
                ruleSlotId,
                modeSlotId,
                itemSlotIds,
                controllerDeckId,
                coinStorageId,
                avatarId,
                ruleId,
                modeId,
                pawnId,
                startingCorner));
        }

        private static GameTemplateObjectInstanceDefinition CreatePlayerCard(
            TabletopObjectId id,
            ObjectDefinitionId definitionId,
            SeatId ownerSeatId)
        {
            return new GameTemplateObjectInstanceDefinition(
                id,
                definitionId,
                TabletopObjectKind.Card,
                TabletopPose.Default,
                ownerSeatId,
                ObjectVisibility.Public,
                false,
                CardFace.FaceUp);
        }

        private static TabletopPose CreateFloorPose(
            TrapFloorCoordinate coordinate,
            int layer,
            int localOrder)
        {
            return new TabletopPose(
                new TableCoordinate(
                    (coordinate.X - 3.5d) * FloorColumnSpacing,
                    (coordinate.Y - 3.5d) * FloorRowSpacing),
                0f,
                layer,
                localOrder);
        }

        public static TabletopPose GetConsolePose(PlayerSeatLayoutEntry layoutSeat)
        {
            if (layoutSeat == null)
            {
                throw new ArgumentNullException(nameof(layoutSeat));
            }

            return ProjectToRadius(layoutSeat.ConsoleAnchorPose, PlayerConsoleRadius);
        }

        public static TabletopPose GetHandPose(PlayerSeatLayoutEntry layoutSeat)
        {
            if (layoutSeat == null)
            {
                throw new ArgumentNullException(nameof(layoutSeat));
            }

            return ProjectToRadius(layoutSeat.HandAnchorPose, PlayerHandRadius);
        }

        private static TabletopPose ProjectToRadius(TabletopPose pose, double radius)
        {
            double sourceRadius = Math.Sqrt(
                (pose.Position.X * pose.Position.X)
                + (pose.Position.Y * pose.Position.Y));
            if (sourceRadius <= 0d)
            {
                throw new ArgumentException(
                    "Trap Floor player-area anchors must be offset from the Board center.",
                    nameof(pose));
            }

            double scale = radius / sourceRadius;
            return new TabletopPose(
                new TableCoordinate(pose.Position.X * scale, pose.Position.Y * scale),
                pose.RotationDegrees,
                pose.Layer,
                pose.LocalOrder);
        }

        private static TabletopPose OffsetBesideConsole(TabletopPose consolePose, double distance)
        {
            double radians = consolePose.RotationDegrees * (Math.PI / 180d);
            return new TabletopPose(
                new TableCoordinate(
                    consolePose.Position.X + (Math.Cos(radians) * distance),
                    consolePose.Position.Y - (Math.Sin(radians) * distance)),
                consolePose.RotationDegrees,
                consolePose.Layer,
                consolePose.LocalOrder);
        }

        private static GameTemplateContainerDefinition CreateContainer(
            ContainerId id,
            ContainerKind kind,
            SeatId ownerSeatId,
            ObjectVisibility visibility,
            int capacity)
        {
            return new GameTemplateContainerDefinition(
                id,
                kind,
                ownerSeatId,
                visibility,
                capacity,
                false,
                TabletopPose.Default);
        }

        private static GameTemplateContainerDefinition CreatePlacedContainer(
            ContainerId id,
            ContainerKind kind,
            TabletopPose pose)
        {
            return new GameTemplateContainerDefinition(
                id,
                kind,
                SeatId.Empty,
                ObjectVisibility.Public,
                0,
                true,
                pose);
        }

        private static Guid CreateGuid(int category, int index)
        {
            return new Guid(
                unchecked((int)0x54460000) + category,
                unchecked((short)0x4f4f),
                unchecked((short)0x4000),
                0x80,
                0x00,
                (byte)(category >> 8),
                (byte)category,
                (byte)(index >> 24),
                (byte)(index >> 16),
                (byte)(index >> 8),
                (byte)index);
        }
    }
}
