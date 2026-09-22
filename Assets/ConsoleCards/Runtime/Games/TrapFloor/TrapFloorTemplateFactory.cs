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
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Builds the current authored four-Player Trap Floor starting setup. Two- and three-Player
    /// Seat layouts remain intentionally unresolved content work.
    /// </summary>
    public static class TrapFloorTemplateFactory
    {
        public const int PrototypePlayerCount = 4;
        public const string FloorContentSetId = "trap-floor-floor-pool";
        public const string AbilityContentSetId = "trap-floor-abilities";
        public const string ControllerInputContentSetId = "trap-floor-controller-inputs";

        private const double PlayerConsoleRadius = 6.1d;
        private const double PlayerHandRadius = 4.15d;
        private const double ControllerDeckOffset = 3.2d;
        private const double PurchasedAbilityAreaOffset = -4.45d;
        private const double StartingAbilityStagingSideOffset = -3.2d;
        private const double StartingAbilityStagingSpacing = 1.15d;
        private const double FloorfallDiceX = 3.45d;
        private const double FloorfallDiceY = 3.45d;
        private const double FloorfallDiceSpacing = 0.9d;
        private const float PrototypeCameraOrthographicSize = 7.35f;

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer()
        {
            throw MissingAuthoredDefinition();
        }

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(IRandomValueSource randomValueSource)
        {
            throw MissingAuthoredDefinition();
        }

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource,
            TrapFloorStage03Configuration stage03Configuration)
        {
            throw MissingAuthoredDefinition();
        }

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource,
            GameDefinitionData gameDefinition)
        {
            return CreateStandardFourPlayer(randomValueSource, gameDefinition, null);
        }

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource,
            GameDefinitionData gameDefinition,
            string selectedModeStableId)
        {
            if (randomValueSource == null) throw new ArgumentNullException(nameof(randomValueSource));
            if (gameDefinition == null) throw new ArgumentNullException(nameof(gameDefinition));

            ModeDefinitionData activeMode = ValidateAndResolveMode(gameDefinition, selectedModeStableId);
            Guid gameDefinitionId = ParseStableGuid(gameDefinition.StableId, "Game");
            GridDefinitionData grid = gameDefinition.Grid;
            IReadOnlyList<TrapFloorFloorContentDefinition> floorContent =
                ResolveFloorContent(gameDefinition, activeMode, grid);
            IReadOnlyList<CardDefinitionData> abilityDefinitions = ResolveAbilities(gameDefinition, activeMode);
            IReadOnlyList<CardDefinitionData> controllerInputCards = ResolveControllerInputCards(gameDefinition);
            ConsoleSlotDefinitionData mainSlot = ResolveConsoleSlot(gameDefinition.Console, "Main", 1);
            ConsoleSlotDefinitionData sideSlots = ResolveConsoleSlot(gameDefinition.Console, "Side", 1);
            if (mainSlot.PhysicalSlotCount != 1)
                throw new ArgumentException("Trap Floor Console requires exactly one authored Main Slot.", nameof(gameDefinition));
            PlayerLayoutDefinition playerLayout = PlayerLayoutPresets.StandardFourPlayer;
            GameTemplateId templateId = new GameTemplateId(
                string.Equals(activeMode.StableId, gameDefinition.DefaultModeStableId, StringComparison.OrdinalIgnoreCase)
                    ? gameDefinitionId
                    : CreateGuid(1, StableStringHash(activeMode.StableId)));
            PlayAreaId boardPlayAreaId = new PlayAreaId(CreateGuid(2, 1));
            ObjectDefinitionId avatarDefinitionId = ResolveAvatarDefinitionId(gameDefinition);
            ObjectDefinitionId pawnDefinitionId = new ObjectDefinitionId(CreateGuid(20, 8));
            ObjectDefinitionId dieDefinitionId = new ObjectDefinitionId(CreateGuid(20, 10));

            List<GameTemplateObjectDefinition> objectDefinitions = BuildObjectDefinitions(
                gameDefinition,
                avatarDefinitionId,
                pawnDefinitionId,
                dieDefinitionId);
            List<GameTemplateSeatDefinition> seats = new List<GameTemplateSeatDefinition>(PrototypePlayerCount);
            List<GameTemplateContainerDefinition> containers = new List<GameTemplateContainerDefinition>();
            List<GameTemplateObjectInstanceDefinition> objects = new List<GameTemplateObjectInstanceDefinition>();
            List<GameTemplateContainerMembership> memberships = new List<GameTemplateContainerMembership>();
            List<TrapFloorPlayerSetupDefinition> players = new List<TrapFloorPlayerSetupDefinition>(PrototypePlayerCount);
            Dictionary<TabletopObjectId, string> labels = new Dictionary<TabletopObjectId, string>();
            Dictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds =
                new Dictionary<TrapFloorCoordinate, TabletopObjectId>();

            CreateFloorBoard(floorContent, grid, randomValueSource, floorCardIds, labels, objects);

            TrapFloorCoordinate[] startingCorners =
            {
                new TrapFloorCoordinate(1, 1),
                new TrapFloorCoordinate(grid.Columns, 1),
                new TrapFloorCoordinate(grid.Columns, grid.Rows),
                new TrapFloorCoordinate(1, grid.Rows),
            };

            for (int seatIndex = 0; seatIndex < PrototypePlayerCount; seatIndex++)
            {
                playerLayout.TryGetSeat(seatIndex, out PlayerSeatLayoutEntry layoutSeat);
                CreatePlayerSetup(
                    seatIndex,
                    layoutSeat,
                    startingCorners[seatIndex],
                    avatarDefinitionId,
                    pawnDefinitionId,
                    grid,
                    mainSlot,
                    sideSlots,
                    controllerInputCards,
                    abilityDefinitions,
                    activeMode.StartingAbilityCount,
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

            TabletopBounds boardBounds = CreateBoardBounds(grid);
            GameTemplate template = new GameTemplate(
                templateId,
                GameTemplate.CurrentSchemaVersion,
                $"{gameDefinition.DisplayName} — {activeMode.DisplayName}",
                gameDefinition.ManualRules,
                playerLayout.Id,
                PrototypePlayerCount,
                seats,
                containers,
                objects,
                memberships,
                new[] { new GameTemplatePlayAreaDefinition(boardPlayAreaId, boardBounds, boardBounds) },
                new[]
                {
                    new GameTemplateCameraBookmarkDefinition(
                        "Trap Floor Tabletop",
                        boardBounds.Center,
                        ScaleCameraSize(grid)),
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
                gameDefinition,
                activeMode,
                template,
                catalog,
                playerLayout,
                boardPlayAreaId,
                floorCardIds,
                floorContent,
                labels,
                players,
                floorfallXAxisDieId,
                floorfallYAxisDieId);
        }

        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource,
            TrapFloorStage03Configuration stage03Configuration,
            GameDefinitionData gameDefinition)
        {
            if (stage03Configuration == null) throw new ArgumentNullException(nameof(stage03Configuration));
            TrapFloorTemplateDefinition template = CreateStandardFourPlayer(randomValueSource, gameDefinition);
            if (template.ActiveMode.RequiredKeyCount != stage03Configuration.RequiredKeyCount)
            {
                throw new ArgumentException(
                    "Legacy required-Key configuration does not match the authored active Mode.",
                    nameof(stage03Configuration));
            }

            return template;
        }

        private static InvalidOperationException MissingAuthoredDefinition()
        {
            return new InvalidOperationException(
                "Trap Floor Template construction requires an authored Game Definition. The former C# content fallback has been removed.");
        }

        private static ModeDefinitionData ValidateAndResolveMode(
            GameDefinitionData definition,
            string selectedModeStableId)
        {
            if (definition.Grid == null)
                throw new ArgumentException("Trap Floor requires an authored Grid Definition.", nameof(definition));
            if (definition.Console == null)
                throw new ArgumentException("Trap Floor requires an authored Console Configuration.", nameof(definition));
            if (definition.MinimumPlayers > PrototypePlayerCount || definition.MaximumPlayers < PrototypePlayerCount)
            {
                throw new ArgumentException(
                    "Trap Floor's authored Player range must include the current four-Player layout.",
                    nameof(definition));
            }

            string modeId = string.IsNullOrWhiteSpace(selectedModeStableId)
                ? definition.DefaultModeStableId
                : selectedModeStableId;
            if (string.IsNullOrWhiteSpace(modeId) || !definition.TryGetMode(modeId, out ModeDefinitionData mode))
            {
                throw new ArgumentException(
                    $"Trap Floor active Mode '{modeId}' is not present in the authored Game Definition.",
                    nameof(definition));
            }

            return mode;
        }

        private static IReadOnlyList<TrapFloorFloorContentDefinition> ResolveFloorContent(
            GameDefinitionData gameDefinition,
            ModeDefinitionData activeMode,
            GridDefinitionData grid)
        {
            if (!gameDefinition.TryGetContentSet(FloorContentSetId, out GameContentSetData contentSet))
            {
                throw new ArgumentException(
                    $"Trap Floor requires authored content set '{FloorContentSetId}'.",
                    nameof(gameDefinition));
            }

            List<TrapFloorFloorContentDefinition> expanded = new List<TrapFloorFloorContentDefinition>();
            int keyCount = 0;
            int exitCount = 0;
            for (int i = 0; i < contentSet.CardDefinitionIds.Count; i++)
            {
                string cardId = contentSet.CardDefinitionIds[i];
                if (!gameDefinition.TryGetCard(cardId, out CardDefinitionData card))
                {
                    throw new ArgumentException(
                        $"Trap Floor Floor content set references missing Card Definition '{cardId}'.",
                        nameof(gameDefinition));
                }

                TrapFloorFloorContentDefinition content = new TrapFloorFloorContentDefinition(card);
                for (int copyIndex = 0; copyIndex < card.Quantity; copyIndex++) expanded.Add(content);
                if (content.Category == TrapFloorFloorContentCategory.Key) keyCount += card.Quantity;
                if (content.Category == TrapFloorFloorContentCategory.SecretExit) exitCount += card.Quantity;
            }

            if (expanded.Count != grid.CellCount)
            {
                throw new ArgumentException(
                    $"Trap Floor Grid '{grid.StableId}' has {grid.CellCount} cells, but authored content set "
                    + $"'{FloorContentSetId}' produces {expanded.Count} Floor Cards. Configure matching Card quantities.",
                    nameof(gameDefinition));
            }

            if (activeMode.RequiredKeyCount > keyCount)
            {
                throw new ArgumentException(
                    $"Trap Floor Mode '{activeMode.DisplayName}' requires {activeMode.RequiredKeyCount} Keys, "
                    + $"but the authored Floor content produces {keyCount}.",
                    nameof(gameDefinition));
            }

            if (exitCount < 1)
            {
                throw new ArgumentException(
                    "Trap Floor's authored Floor content must produce at least one SecretExit Card.",
                    nameof(gameDefinition));
            }

            return expanded;
        }

        private static IReadOnlyList<CardDefinitionData> ResolveAbilities(
            GameDefinitionData gameDefinition,
            ModeDefinitionData activeMode)
        {
            if (!gameDefinition.TryGetContentSet(AbilityContentSetId, out GameContentSetData contentSet))
            {
                throw new ArgumentException(
                    $"Trap Floor requires authored content set '{AbilityContentSetId}'.",
                    nameof(gameDefinition));
            }

            List<CardDefinitionData> abilities = new List<CardDefinitionData>(contentSet.CardDefinitionIds.Count);
            for (int i = 0; i < contentSet.CardDefinitionIds.Count; i++)
            {
                if (!gameDefinition.TryGetCard(contentSet.CardDefinitionIds[i], out CardDefinitionData card))
                    throw new ArgumentException("Trap Floor Ability content references a missing Card Definition.", nameof(gameDefinition));
                if (!string.Equals(card.Category, "Ability", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(card.Category, "Action", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Starting Card '{card.DisplayName}' is not an Ability/Action.", nameof(gameDefinition));
                abilities.Add(card);
            }

            if (activeMode.StartingAbilityCount > abilities.Count)
            {
                throw new ArgumentException(
                    $"Trap Floor Mode '{activeMode.DisplayName}' requires {activeMode.StartingAbilityCount} starting Abilities, "
                    + $"but '{AbilityContentSetId}' contains {abilities.Count} definitions.",
                    nameof(gameDefinition));
            }

            return abilities;
        }

        private static IReadOnlyList<CardDefinitionData> ResolveControllerInputCards(
            GameDefinitionData gameDefinition)
        {
            if (!gameDefinition.TryGetContentSet(ControllerInputContentSetId, out GameContentSetData contentSet))
            {
                throw new ArgumentException(
                    $"Trap Floor requires authored content set '{ControllerInputContentSetId}'.",
                    nameof(gameDefinition));
            }

            List<CardDefinitionData> definitions = new List<CardDefinitionData>(contentSet.CardDefinitionIds.Count);
            HashSet<ControllerInput> representedInputs = new HashSet<ControllerInput>();
            int maximumQuantity = 0;
            for (int i = 0; i < contentSet.CardDefinitionIds.Count; i++)
            {
                if (!gameDefinition.TryGetCard(contentSet.CardDefinitionIds[i], out CardDefinitionData card))
                {
                    throw new ArgumentException(
                        "Trap Floor Controller-input content references a missing Card Definition.",
                        nameof(gameDefinition));
                }

                if (!card.RepresentedControllerInput.HasValue)
                {
                    throw new ArgumentException(
                        $"Controller Card '{card.DisplayName}' does not declare its represented input.",
                        nameof(gameDefinition));
                }

                bool vocabularyContainsInput = false;
                for (int vocabularyIndex = 0;
                     vocabularyIndex < gameDefinition.InputVocabulary.Count;
                     vocabularyIndex++)
                {
                    if (gameDefinition.InputVocabulary[vocabularyIndex] == card.RepresentedControllerInput.Value)
                    {
                        vocabularyContainsInput = true;
                        break;
                    }
                }

                if (!vocabularyContainsInput)
                {
                    throw new ArgumentException(
                        $"Controller Card '{card.DisplayName}' represents an input outside the Game vocabulary.",
                        nameof(gameDefinition));
                }

                if (!representedInputs.Add(card.RepresentedControllerInput.Value))
                {
                    throw new ArgumentException(
                        $"Trap Floor Controller-input content contains more than one definition for '{card.RepresentedControllerInput.Value}'.",
                        nameof(gameDefinition));
                }

                definitions.Add(card);
                maximumQuantity = Math.Max(maximumQuantity, card.Quantity);
            }

            for (int i = 0; i < gameDefinition.InputVocabulary.Count; i++)
            {
                if (!representedInputs.Contains(gameDefinition.InputVocabulary[i]))
                {
                    throw new ArgumentException(
                        $"Trap Floor Controller-input content is missing '{gameDefinition.InputVocabulary[i]}'.",
                        nameof(gameDefinition));
                }
            }

            List<CardDefinitionData> expanded = new List<CardDefinitionData>();
            for (int copyIndex = 0; copyIndex < maximumQuantity; copyIndex++)
            {
                for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
                {
                    CardDefinitionData card = definitions[definitionIndex];
                    if (copyIndex < card.Quantity) expanded.Add(card);
                }
            }

            return expanded;
        }

        private static ConsoleSlotDefinitionData ResolveConsoleSlot(
            ConsoleConfigurationData configuration,
            string role,
            int minimumCount)
        {
            for (int i = 0; i < configuration.Slots.Count; i++)
            {
                ConsoleSlotDefinitionData candidate = configuration.Slots[i];
                if (string.Equals(candidate.Role, role, StringComparison.OrdinalIgnoreCase))
                {
                    if (candidate.PhysicalSlotCount < minimumCount)
                        throw new ArgumentException($"Trap Floor Console role '{role}' has too few physical Slots.");
                    return candidate;
                }
            }

            throw new ArgumentException($"Trap Floor Console configuration is missing role '{role}'.");
        }

        private static ObjectDefinitionId ResolveAvatarDefinitionId(GameDefinitionData definition)
        {
            if (definition.Avatars.Count == 0)
                throw new ArgumentException("Trap Floor requires at least one authored Avatar Definition.", nameof(definition));
            return new ObjectDefinitionId(ParseStableGuid(definition.Avatars[0].StableId, "Avatar"));
        }

        private static List<GameTemplateObjectDefinition> BuildObjectDefinitions(
            GameDefinitionData definition,
            ObjectDefinitionId avatarDefinitionId,
            ObjectDefinitionId pawnDefinitionId,
            ObjectDefinitionId dieDefinitionId)
        {
            List<GameTemplateObjectDefinition> definitions = new List<GameTemplateObjectDefinition>
            {
                new GameTemplateObjectDefinition(avatarDefinitionId, TabletopObjectKind.Card, definition.Avatars[0].DisplayName),
                new GameTemplateObjectDefinition(pawnDefinitionId, TabletopObjectKind.Pawn, "Player Pawn"),
                new GameTemplateObjectDefinition(dieDefinitionId, TabletopObjectKind.Die, "Six-sided Die"),
            };
            HashSet<ObjectDefinitionId> seen = new HashSet<ObjectDefinitionId>
            {
                avatarDefinitionId,
                pawnDefinitionId,
                dieDefinitionId,
            };
            for (int i = 0; i < definition.Cards.Count; i++)
            {
                CardDefinitionData card = definition.Cards[i];
                ObjectDefinitionId id = new ObjectDefinitionId(ParseStableGuid(card.StableId, $"Card '{card.DisplayName}'"));
                if (seen.Add(id)) definitions.Add(new GameTemplateObjectDefinition(id, TabletopObjectKind.Card, card.DisplayName));
            }
            return definitions;
        }

        private static void CreateFloorBoard(
            IReadOnlyList<TrapFloorFloorContentDefinition> contentDefinitions,
            GridDefinitionData grid,
            IRandomValueSource randomValueSource,
            IDictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds,
            IDictionary<TabletopObjectId, string> labels,
            ICollection<GameTemplateObjectInstanceDefinition> objects)
        {
            List<TrapFloorFloorContentDefinition> shuffled = new List<TrapFloorFloorContentDefinition>(contentDefinitions);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int swapIndex = randomValueSource.NextInt(0, i + 1);
                TrapFloorFloorContentDefinition swap = shuffled[i];
                shuffled[i] = shuffled[swapIndex];
                shuffled[swapIndex] = swap;
            }

            int objectIndex = 0;
            for (int y = TrapFloorCoordinate.MinimumAxisValue; y <= grid.Rows; y++)
            {
                for (int x = TrapFloorCoordinate.MinimumAxisValue; x <= grid.Columns; x++)
                {
                    TrapFloorCoordinate coordinate = new TrapFloorCoordinate(x, y);
                    TabletopObjectId objectId = new TabletopObjectId(CreateGuid(30, ++objectIndex));
                    TrapFloorFloorContentDefinition content = shuffled[objectIndex - 1];
                    objects.Add(new GameTemplateObjectInstanceDefinition(
                        objectId,
                        content.Id,
                        TabletopObjectKind.Card,
                        CreateFloorPose(coordinate, grid, 2, objectIndex),
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
            ObjectDefinitionId pawnDefinitionId,
            GridDefinitionData grid,
            ConsoleSlotDefinitionData mainSlot,
            ConsoleSlotDefinitionData sideSlot,
            IReadOnlyList<CardDefinitionData> controllerInputCards,
            IReadOnlyList<CardDefinitionData> abilityDefinitions,
            int startingAbilityCount,
            ICollection<GameTemplateSeatDefinition> seats,
            ICollection<GameTemplateContainerDefinition> containers,
            ICollection<GameTemplateContainerMembership> memberships,
            ICollection<GameTemplateObjectInstanceDefinition> objects,
            IDictionary<TabletopObjectId, string> labels,
            ICollection<TrapFloorPlayerSetupDefinition> players)
        {
            int playerNumber = seatIndex + 1;
            int idBase = seatIndex * 20;
            SeatId seatId = new SeatId(CreateGuid(40, playerNumber));
            ContainerId handId = new ContainerId(CreateGuid(41, idBase + 1));
            ContainerId mainSlotId = new ContainerId(CreateGuid(41, idBase + 2));
            ContainerId[] sideSlotIds = new ContainerId[sideSlot.PhysicalSlotCount];
            for (int i = 0; i < sideSlotIds.Length; i++)
                sideSlotIds[i] = new ContainerId(CreateGuid(41, idBase + 3 + i));
            ContainerId controllerDeckId = new ContainerId(CreateGuid(41, idBase + 19));
            ContainerId actionAbilityAreaId = new ContainerId(CreateGuid(41, idBase + 18));

            List<ContainerId> consoleSlotIds = new List<ContainerId>(1 + sideSlotIds.Length) { mainSlotId };
            consoleSlotIds.AddRange(sideSlotIds);
            seats.Add(new GameTemplateSeatDefinition(
                seatId,
                seatIndex,
                handId,
                consoleSlotIds,
                GetConsolePose(layoutSeat)));
            // The authored maximum is an assisted draw target, not a physical capacity. Zero keeps
            // the Hand technically unbounded for freeform draws beyond that recommendation.
            containers.Add(CreateContainer(handId, ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, 0));
            containers.Add(CreateContainer(
                mainSlotId,
                ContainerKind.ConsoleSlot,
                seatId,
                ObjectVisibility.Public,
                mainSlot.MaximumCardsPerSlot));
            for (int i = 0; i < sideSlotIds.Length; i++)
            {
                containers.Add(CreateContainer(
                    sideSlotIds[i],
                    ContainerKind.ConsoleSlot,
                    seatId,
                    ObjectVisibility.Public,
                    sideSlot.MaximumCardsPerSlot));
            }

            containers.Add(new GameTemplateContainerDefinition(
                actionAbilityAreaId,
                ContainerKind.Stack,
                seatId,
                ObjectVisibility.Public,
                0,
                true,
                OffsetBesideConsole(GetConsolePose(layoutSeat), PurchasedAbilityAreaOffset)));
            containers.Add(new GameTemplateContainerDefinition(
                controllerDeckId,
                ContainerKind.Deck,
                seatId,
                ObjectVisibility.Public,
                0,
                true,
                OffsetBesideConsole(GetConsolePose(layoutSeat), ControllerDeckOffset)));

            TabletopObjectId avatarId = new TabletopObjectId(CreateGuid(42, playerNumber));
            TabletopObjectId pawnId = new TabletopObjectId(CreateGuid(45, playerNumber));
            TabletopPose consolePose = GetConsolePose(layoutSeat);
            objects.Add(CreatePlayerCard(avatarId, avatarDefinitionId, seatId, TabletopPose.Default));
            labels.Add(avatarId, $"P{playerNumber}\nAVATAR");
            objects.Add(new GameTemplateObjectInstanceDefinition(
                pawnId,
                pawnDefinitionId,
                TabletopObjectKind.Pawn,
                CreateFloorPose(startingCorner, grid, 6, playerNumber),
                seatId,
                ObjectVisibility.Public,
                false,
                CardFace.FaceUp));

            memberships.Add(new GameTemplateContainerMembership(mainSlotId, new[] { avatarId }));
            for (int i = 0; i < sideSlotIds.Length; i++)
            {
                memberships.Add(new GameTemplateContainerMembership(sideSlotIds[i], Array.Empty<TabletopObjectId>()));
            }

            for (int i = 0; i < startingAbilityCount; i++)
            {
                CardDefinitionData ability = abilityDefinitions[i];
                TabletopObjectId abilityId = new TabletopObjectId(CreateGuid(46, (seatIndex * 100) + i + 1));
                ObjectDefinitionId abilityDefinitionId = new ObjectDefinitionId(
                    ParseStableGuid(ability.StableId, $"Ability '{ability.DisplayName}'"));
                TabletopPose stagingPose = CreateStartingAbilityStagingPose(
                    consolePose,
                    i,
                    startingAbilityCount,
                    ability.Orientation);
                objects.Add(CreatePlayerCard(abilityId, abilityDefinitionId, seatId, stagingPose));
                labels.Add(abilityId, ability.DisplayName);
            }

            List<TabletopObjectId> controllerCardIds = new List<TabletopObjectId>(controllerInputCards.Count);
            for (int i = 0; i < controllerInputCards.Count; i++)
            {
                CardDefinitionData controllerCard = controllerInputCards[i];
                TabletopObjectId controllerCardId = new TabletopObjectId(
                    CreateGuid(47, (seatIndex * 10000) + i + 1));
                ObjectDefinitionId controllerDefinitionId = new ObjectDefinitionId(
                    ParseStableGuid(controllerCard.StableId, $"Controller Card '{controllerCard.DisplayName}'"));
                objects.Add(new GameTemplateObjectInstanceDefinition(
                    controllerCardId,
                    controllerDefinitionId,
                    TabletopObjectKind.Card,
                    TabletopPose.Default,
                    seatId,
                    ObjectVisibility.OwnerOnly,
                    false,
                    CardFace.FaceDown));
                controllerCardIds.Add(controllerCardId);
                labels.Add(controllerCardId, controllerCard.DisplayName);
            }

            memberships.Add(new GameTemplateContainerMembership(controllerDeckId, controllerCardIds));
            memberships.Add(new GameTemplateContainerMembership(actionAbilityAreaId, Array.Empty<TabletopObjectId>()));
            memberships.Add(new GameTemplateContainerMembership(handId, Array.Empty<TabletopObjectId>()));
            players.Add(new TrapFloorPlayerSetupDefinition(
                seatIndex,
                seatId,
                handId,
                mainSlotId,
                sideSlotIds,
                controllerDeckId,
                actionAbilityAreaId,
                avatarId,
                pawnId,
                startingCorner));
        }

        private static GameTemplateObjectInstanceDefinition CreatePlayerCard(
            TabletopObjectId id,
            ObjectDefinitionId definitionId,
            SeatId ownerSeatId,
            TabletopPose pose)
        {
            return new GameTemplateObjectInstanceDefinition(
                id,
                definitionId,
                TabletopObjectKind.Card,
                pose,
                ownerSeatId,
                ObjectVisibility.Public,
                false,
                CardFace.FaceUp);
        }

        private static TabletopPose CreateStartingAbilityStagingPose(
            TabletopPose consolePose,
            int abilityIndex,
            int abilityCount,
            CardOrientation orientation)
        {
            double radians = consolePose.RotationDegrees * (Math.PI / 180d);
            double centeredIndex = abilityIndex - ((abilityCount - 1d) * 0.5d);
            double rowOffset = centeredIndex * StartingAbilityStagingSpacing;
            float orientationOffset = orientation == CardOrientation.Landscape ? 90f : 0f;
            return new TabletopPose(
                new TableCoordinate(
                    consolePose.Position.X
                        + (Math.Cos(radians) * StartingAbilityStagingSideOffset)
                        + (Math.Sin(radians) * rowOffset),
                    consolePose.Position.Y
                        - (Math.Sin(radians) * StartingAbilityStagingSideOffset)
                        + (Math.Cos(radians) * rowOffset)),
                consolePose.RotationDegrees + orientationOffset,
                consolePose.Layer,
                abilityIndex);
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

        private static TabletopPose CreateFloorPose(
            TrapFloorCoordinate coordinate,
            GridDefinitionData grid,
            int layer,
            int localOrder)
        {
            if (coordinate.X > grid.Columns || coordinate.Y > grid.Rows)
                throw new ArgumentOutOfRangeException(nameof(coordinate), "Floor coordinate is outside the authored Grid.");
            return new TabletopPose(
                new TableCoordinate(
                    grid.OriginX + ((coordinate.X - ((grid.Columns + 1d) * 0.5d)) * grid.ColumnPitch),
                    grid.OriginY + ((coordinate.Y - ((grid.Rows + 1d) * 0.5d)) * grid.RowPitch)),
                0f,
                layer,
                localOrder);
        }

        private static TabletopBounds CreateBoardBounds(GridDefinitionData grid)
        {
            double halfWidth = (((grid.Columns - 1) * grid.ColumnPitch) + grid.CellWidth) * 0.5d;
            double halfHeight = (((grid.Rows - 1) * grid.RowPitch) + grid.CellHeight) * 0.5d;
            return new TabletopBounds(
                new TableCoordinate(grid.OriginX - halfWidth, grid.OriginY - halfHeight),
                new TableCoordinate(grid.OriginX + halfWidth, grid.OriginY + halfHeight));
        }

        private static float ScaleCameraSize(GridDefinitionData grid)
        {
            double width = ((grid.Columns - 1) * grid.ColumnPitch) + grid.CellWidth;
            double height = ((grid.Rows - 1) * grid.RowPitch) + grid.CellHeight;
            double scale = Math.Max(width / 4.32d, height / 6d);
            return (float)(PrototypeCameraOrthographicSize * scale);
        }

        public static TabletopPose GetConsolePose(PlayerSeatLayoutEntry layoutSeat)
        {
            if (layoutSeat == null) throw new ArgumentNullException(nameof(layoutSeat));
            return ProjectToRadius(layoutSeat.ConsoleAnchorPose, PlayerConsoleRadius);
        }

        public static TabletopPose GetHandPose(PlayerSeatLayoutEntry layoutSeat)
        {
            if (layoutSeat == null) throw new ArgumentNullException(nameof(layoutSeat));
            return ProjectToRadius(layoutSeat.HandAnchorPose, PlayerHandRadius);
        }

        private static TabletopPose ProjectToRadius(TabletopPose pose, double radius)
        {
            double sourceRadius = Math.Sqrt((pose.Position.X * pose.Position.X) + (pose.Position.Y * pose.Position.Y));
            if (sourceRadius <= 0d)
                throw new ArgumentException("Trap Floor player-area anchors must be offset from the Board center.", nameof(pose));
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

        private static Guid ParseStableGuid(string stableId, string label)
        {
            if (!Guid.TryParse(stableId, out Guid id))
                throw new ArgumentException($"{label} stable ID '{stableId}' must be a GUID.");
            return id;
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

        private static int StableStringHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
