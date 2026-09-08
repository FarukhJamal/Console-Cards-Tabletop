using System;
using System.Collections.Generic;
using ConsoleCards.Application.Random;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Randomness;
using ConsoleCards.GameTemplates;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Builds the current four-Player Trap Floor starting setup.
    /// Stable identities and layout are deterministic; hidden Floor content is assigned by the supplied authority random source.
    /// Two- and three-Player authored Seat mappings remain intentionally unresolved.
    /// </summary>
    public static class TrapFloorTemplateFactory
    {
        public const int MinimumPlayerCount = 2;
        public const int MaximumPlayerCount = 4;
        public const int PrototypePlayerCount = 4;
        public const int BoardAxisSize = 6;
        public const int FloorCardCount = 36;
        public const int ItemSlotCountPerPlayer = 3;
        public const int ConsoleSlotCountPerPlayer = 6;

        private const double FloorColumnSpacing = 0.72d;
        private const double FloorRowSpacing = 1.0d;
        private const double PlayerConsoleRadius = 6.1d;
        private const double PlayerHandRadius = 4.15d;
        private const double ControllerDeckOffset = 3.2d;
        private const double FloorfallDiceX = 3.45d;
        private const double FloorfallDiceY = 3.45d;
        private const double FloorfallDiceSpacing = 0.9d;
        private const float PrototypeCameraOrthographicSize = 7.35f;

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer()
        {
            return CreateStandardFourPlayer(new SystemRandomValueSource(0));
        }

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource)
        {
            if (randomValueSource == null)
            {
                throw new ArgumentNullException(nameof(randomValueSource));
            }

            PlayerLayoutDefinition playerLayout = PlayerLayoutPresets.StandardFourPlayer;
            GameTemplateId templateId = new GameTemplateId(CreateGuid(1, 1));
            PlayAreaId boardPlayAreaId = new PlayAreaId(CreateGuid(2, 1));

            ObjectDefinitionId avatarDefinitionId = new ObjectDefinitionId(CreateGuid(20, 5));
            ObjectDefinitionId ruleDefinitionId = new ObjectDefinitionId(CreateGuid(20, 6));
            ObjectDefinitionId modeDefinitionId = new ObjectDefinitionId(CreateGuid(20, 7));
            ObjectDefinitionId pawnDefinitionId = new ObjectDefinitionId(CreateGuid(20, 8));
            ObjectDefinitionId dieDefinitionId = new ObjectDefinitionId(CreateGuid(20, 10));

            IReadOnlyList<TrapFloorFloorContentDefinition> floorContentDefinitions =
                TrapFloorStage03ContentPool.CreateDefinitions();
            List<GameTemplateObjectDefinition> objectDefinitions = new List<GameTemplateObjectDefinition>
            {
                new GameTemplateObjectDefinition(avatarDefinitionId, TabletopObjectKind.Card, "Avatar Card"),
                new GameTemplateObjectDefinition(ruleDefinitionId, TabletopObjectKind.Card, "Rule Card"),
                new GameTemplateObjectDefinition(modeDefinitionId, TabletopObjectKind.Card, "Mode Card"),
                new GameTemplateObjectDefinition(pawnDefinitionId, TabletopObjectKind.Pawn, "Player Pawn"),
                new GameTemplateObjectDefinition(dieDefinitionId, TabletopObjectKind.Die, "Six-sided Die"),
            };
            for (int i = 0; i < floorContentDefinitions.Count; i++)
            {
                TrapFloorFloorContentDefinition definition = floorContentDefinitions[i];
                objectDefinitions.Add(new GameTemplateObjectDefinition(
                    definition.Id,
                    TabletopObjectKind.Card,
                    definition.DisplayName));
            }

            List<GameTemplateSeatDefinition> seats = new List<GameTemplateSeatDefinition>(PrototypePlayerCount);
            List<GameTemplateContainerDefinition> containers = new List<GameTemplateContainerDefinition>();
            List<GameTemplateObjectInstanceDefinition> objects = new List<GameTemplateObjectInstanceDefinition>();
            List<GameTemplateContainerMembership> memberships = new List<GameTemplateContainerMembership>();
            List<TrapFloorPlayerSetupDefinition> players = new List<TrapFloorPlayerSetupDefinition>(PrototypePlayerCount);
            Dictionary<TabletopObjectId, string> labels = new Dictionary<TabletopObjectId, string>();
            Dictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds =
                new Dictionary<TrapFloorCoordinate, TabletopObjectId>();

            CreateFloorBoard(
                floorContentDefinitions,
                randomValueSource,
                floorCardIds,
                labels,
                objects);

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
                floorCardIds,
                floorContentDefinitions,
                labels,
                players,
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
            IReadOnlyList<TrapFloorFloorContentDefinition> contentDefinitions,
            IRandomValueSource randomValueSource,
            IDictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds,
            IDictionary<TabletopObjectId, string> labels,
            ICollection<GameTemplateObjectInstanceDefinition> objects)
        {
            if (contentDefinitions == null || contentDefinitions.Count != FloorCardCount)
            {
                throw new ArgumentException("Trap Floor Board construction requires exactly 36 content definitions.");
            }

            List<TrapFloorFloorContentDefinition> shuffledContent =
                new List<TrapFloorFloorContentDefinition>(contentDefinitions);
            for (int i = shuffledContent.Count - 1; i > 0; i--)
            {
                int swapIndex = randomValueSource.NextInt(0, i + 1);
                TrapFloorFloorContentDefinition swap = shuffledContent[i];
                shuffledContent[i] = shuffledContent[swapIndex];
                shuffledContent[swapIndex] = swap;
            }

            int objectIndex = 0;
            for (int y = TrapFloorCoordinate.MinimumAxisValue; y <= TrapFloorCoordinate.MaximumAxisValue; y++)
            {
                for (int x = TrapFloorCoordinate.MinimumAxisValue; x <= TrapFloorCoordinate.MaximumAxisValue; x++)
                {
                    TrapFloorCoordinate coordinate = new TrapFloorCoordinate(x, y);
                    TabletopObjectId objectId = new TabletopObjectId(CreateGuid(30, ++objectIndex));
                    TrapFloorFloorContentDefinition content = shuffledContent[objectIndex - 1];
                    double tableX = (x - 3.5d) * FloorColumnSpacing;
                    double tableY = (y - 3.5d) * FloorRowSpacing;
                    objects.Add(new GameTemplateObjectInstanceDefinition(
                        objectId,
                        content.Id,
                        TabletopObjectKind.Card,
                        new TabletopPose(new TableCoordinate(tableX, tableY), 0f, 2, objectIndex),
                        SeatId.Empty,
                        ObjectVisibility.Public,
                        true,
                        CardFace.FaceDown));
                    floorCardIds.Add(coordinate, objectId);
                    labels.Add(objectId, content.DisplayName);
                }
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

            ContainerId[] consoleSlotIds =
            {
                mainSlotId,
                ruleSlotId,
                modeSlotId,
                itemSlotIds[0],
                itemSlotIds[1],
                itemSlotIds[2],
            };
            seats.Add(new GameTemplateSeatDefinition(
                seatId,
                seatIndex,
                handId,
                consoleSlotIds,
                GetConsolePose(layoutSeat)));
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
