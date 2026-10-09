using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// <summary>
        /// Content sets with this role are the floor groups (doc 18 §15.6). Each draws its count at random, and
        /// together they must fill the Grid.
        /// </summary>
        public const string FloorContentRole = "floor";
        public const string ControllerInputContentSetId = "trap-floor-controller-inputs";

        private const double PlayerConsoleRadius = 6.1d;
        // Trap Floor's placement choices beside each Console (console-local along-z). Positions come from
        // ConsoleAdjacentPlacement with its Standard settings (gap, pile size, clearance and margin rules).
        // Right side: Controller Deck, then the Hand zone; left side: Action stack, then the staging row (the Avatar's starting Abilities).
        private const double ControllerDeckAlongZ = 0d;
        private const double HandZoneAlongZ = 0d;
        private const double ActionStackAlongZ = 0d;
        private const double StartingAbilityStagingAlongZ = -1.1d;
        // Each seat's Controller Deck rests in a bay marked Draw (default maximum pile height).
        private static readonly GameTemplatePileStyle ControllerDeckPileStyle =
            new GameTemplatePileStyle(GameTemplateBayMark.Draw);
        private const double FloorfallDiceX = 3.45d;
        private const double FloorfallDiceY = 3.45d;
        private const double FloorfallDiceSpacing = 0.9d;
        private const float PrototypeCameraOrthographicSize = 7.35f;
        private const string MainConsoleSlotKey = "Main";

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
            return CreateStandardFourPlayer(randomValueSource, gameDefinition, selectedModeStableId, null);
        }

        /// <summary>
        /// As above, with the Console layout: each avatar enters the Main slot in Main's layout default
        /// orientation relative to its Console. Without a layout the avatar keeps yaw 0 (previous behaviour).
        /// </summary>
        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource,
            GameDefinitionData gameDefinition,
            string selectedModeStableId,
            ConsoleLayoutData consoleLayout)
        {
            if (randomValueSource == null) throw new ArgumentNullException(nameof(randomValueSource));
            if (gameDefinition == null) throw new ArgumentNullException(nameof(gameDefinition));

            ModeDefinitionData activeMode = ValidateAndResolveMode(gameDefinition, selectedModeStableId);
            Guid gameDefinitionId = ParseStableGuid(gameDefinition.StableId, "Game");
            GridDefinitionData grid = gameDefinition.Grid;
            IReadOnlyList<TrapFloorFloorContentDefinition> floorContent =
                ResolveFloorContent(gameDefinition, activeMode, grid, randomValueSource);
            IReadOnlyList<TrapFloorAvatarSetup> avatars = ResolveAvatars(gameDefinition);
            IReadOnlyList<CardDefinitionData> controllerInputCards = ResolveControllerInputCards(gameDefinition);
            ConsoleSlotDefinitionData mainSlot = ResolveConsoleSlot(gameDefinition.Console, "Main", 1);
            ConsoleSlotDefinitionData sideSlots = ResolveConsoleSlot(gameDefinition.Console, "Side", 1);
            if (mainSlot.PhysicalSlotCount != 1)
                throw new ArgumentException("Trap Floor Console requires exactly one authored Main Slot.", nameof(gameDefinition));
            PlayerLayoutDefinition playerLayout = PlayerLayoutPresets.StandardFourPlayer;
            float? mainSlotYawOffset = ResolveMainSlotYawOffset(consoleLayout);
            ConsoleAdjacentPlacement adjacentPlacement = new ConsoleAdjacentPlacement(
                consoleLayout,
                ConsoleAdjacentPlacementSettings.Standard);
            TrapFloorSeatPieces seatPieces = CreateSeatPieces(adjacentPlacement, MaximumStartingAbilityCount(avatars));
            GameTemplateId templateId = new GameTemplateId(
                string.Equals(activeMode.StableId, gameDefinition.DefaultModeStableId, StringComparison.OrdinalIgnoreCase)
                    ? gameDefinitionId
                    : CreateGuid(1, StableStringHash(activeMode.StableId)));
            PlayAreaId boardPlayAreaId = new PlayAreaId(CreateGuid(2, 1));
            ObjectDefinitionId pawnDefinitionId = new ObjectDefinitionId(CreateGuid(20, 8));
            ObjectDefinitionId dieDefinitionId = new ObjectDefinitionId(CreateGuid(20, 10));

            List<GameTemplateObjectDefinition> objectDefinitions = BuildObjectDefinitions(
                gameDefinition,
                avatars,
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
                    AvatarForSeat(avatars, seatIndex),
                    pawnDefinitionId,
                    grid,
                    mainSlot,
                    sideSlots,
                    controllerInputCards,
                    seats,
                    containers,
                    memberships,
                    objects,
                    labels,
                    players,
                    mainSlotYawOffset,
                    seatPieces);
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
            if (!adjacentPlacement.UsesFallbackShape)
            {
                CheckSeatPlacement(adjacentPlacement, seatPieces, playerLayout, boardBounds);
            }
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

        // Draws every floor group (content sets with the floor role) at random, then checks the whole floor.
        // A floor.count.<set> rule setting overrides a group's authored count. The floor.placement setting
        // ("pattern", doc 18 §15.7) is not read yet; until G2 every floor is placed at random.
        private static IReadOnlyList<TrapFloorFloorContentDefinition> ResolveFloorContent(
            GameDefinitionData gameDefinition,
            ModeDefinitionData activeMode,
            GridDefinitionData grid,
            IRandomValueSource randomValueSource)
        {
            List<TrapFloorFloorContentDefinition> drawn = new List<TrapFloorFloorContentDefinition>(grid.CellCount);
            int groupCount = 0;
            for (int setIndex = 0; setIndex < gameDefinition.ContentSets.Count; setIndex++)
            {
                GameContentSetData contentSet = gameDefinition.ContentSets[setIndex];
                if (!string.Equals(contentSet.Role, FloorContentRole, StringComparison.OrdinalIgnoreCase)) continue;
                groupCount++;
                List<TrapFloorFloorContentDefinition> pool = ExpandFloorGroup(gameDefinition, contentSet);
                int drawCount = ResolveFloorDrawCount(gameDefinition, activeMode, contentSet, pool.Count);
                DrawFloorGroup(contentSet, pool, drawCount, randomValueSource, drawn);
            }

            if (groupCount == 0)
            {
                throw new ArgumentException(
                    $"Trap Floor requires at least one authored content set with role '{FloorContentRole}'.",
                    nameof(gameDefinition));
            }

            int keyCount = 0;
            int exitCount = 0;
            for (int i = 0; i < drawn.Count; i++)
            {
                if (drawn[i].Category == TrapFloorFloorContentCategory.Key) keyCount++;
                if (drawn[i].Category == TrapFloorFloorContentCategory.SecretExit) exitCount++;
            }

            if (drawn.Count != grid.CellCount)
            {
                throw new ArgumentException(
                    $"Trap Floor Grid '{grid.StableId}' has {grid.CellCount} cells, but the floor groups draw "
                    + $"{drawn.Count} Floor Cards. Change the groups' draw counts (or the floor.count rules) so they add up to {grid.CellCount}.",
                    nameof(gameDefinition));
            }

            if (activeMode.RequiredKeyCount > keyCount)
            {
                throw new ArgumentException(
                    $"Trap Floor Mode '{activeMode.DisplayName}' requires {activeMode.RequiredKeyCount} Keys, "
                    + $"but the floor groups draw {keyCount}.",
                    nameof(gameDefinition));
            }

            if (exitCount < 1)
            {
                throw new ArgumentException(
                    "Trap Floor's floor groups must draw at least one SecretExit Card.",
                    nameof(gameDefinition));
            }

            return drawn;
        }

        // The group's pool: every card once per copy (its quantity).
        private static List<TrapFloorFloorContentDefinition> ExpandFloorGroup(
            GameDefinitionData gameDefinition,
            GameContentSetData contentSet)
        {
            List<TrapFloorFloorContentDefinition> pool = new List<TrapFloorFloorContentDefinition>();
            for (int i = 0; i < contentSet.CardDefinitionIds.Count; i++)
            {
                string cardId = contentSet.CardDefinitionIds[i];
                if (!gameDefinition.TryGetCard(cardId, out CardDefinitionData card))
                {
                    throw new ArgumentException(
                        $"Trap Floor floor group '{contentSet.StableId}' references missing Card Definition '{cardId}'.",
                        nameof(gameDefinition));
                }

                TrapFloorFloorContentDefinition content = new TrapFloorFloorContentDefinition(card);
                for (int copyIndex = 0; copyIndex < card.Quantity; copyIndex++) pool.Add(content);
            }

            return pool;
        }

        private static int ResolveFloorDrawCount(
            GameDefinitionData gameDefinition,
            ModeDefinitionData activeMode,
            GameContentSetData contentSet,
            int poolCount)
        {
            int count = contentSet.DrawCount > 0 ? contentSet.DrawCount : poolCount;
            if (gameDefinition.TryGetRuleSetting(
                    RuleSettingKeys.FloorCountPrefix + contentSet.StableId,
                    activeMode.StableId,
                    out string ruled)
                && int.TryParse(ruled, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ruledCount)
                && ruledCount >= 0)
            {
                count = ruledCount;
            }

            return count;
        }

        // Without repeats the group is partly shuffled and its first cards taken; with repeats every draw picks
        // from the whole pool again.
        private static void DrawFloorGroup(
            GameContentSetData contentSet,
            List<TrapFloorFloorContentDefinition> pool,
            int drawCount,
            IRandomValueSource randomValueSource,
            List<TrapFloorFloorContentDefinition> drawn)
        {
            if (drawCount == 0) return;
            if (pool.Count == 0)
            {
                throw new ArgumentException(
                    $"Trap Floor floor group '{contentSet.StableId}' has no cards to draw {drawCount} from.");
            }

            if (contentSet.AllowRepeats)
            {
                for (int i = 0; i < drawCount; i++) drawn.Add(pool[randomValueSource.NextInt(0, pool.Count)]);
                return;
            }

            if (drawCount > pool.Count)
            {
                throw new ArgumentException(
                    $"Trap Floor floor group '{contentSet.StableId}' holds {pool.Count} cards, so it cannot draw "
                    + $"{drawCount} without repeats.");
            }

            for (int i = 0; i < drawCount; i++)
            {
                int pick = randomValueSource.NextInt(i, pool.Count);
                TrapFloorFloorContentDefinition swap = pool[i];
                pool[i] = pool[pick];
                pool[pick] = swap;
                drawn.Add(pool[i]);
            }
        }

        // Every Avatar with its starting Abilities (doc 18 §15.9); the pairing is data on each Avatar.
        private static IReadOnlyList<TrapFloorAvatarSetup> ResolveAvatars(GameDefinitionData definition)
        {
            if (definition.Avatars.Count == 0)
                throw new ArgumentException("Trap Floor requires at least one authored Avatar Definition.", nameof(definition));

            List<TrapFloorAvatarSetup> avatars = new List<TrapFloorAvatarSetup>(definition.Avatars.Count);
            for (int i = 0; i < definition.Avatars.Count; i++)
            {
                AvatarDefinitionData avatar = definition.Avatars[i];
                List<CardDefinitionData> abilities = new List<CardDefinitionData>(avatar.StartingAbilityIds.Count);
                for (int abilityIndex = 0; abilityIndex < avatar.StartingAbilityIds.Count; abilityIndex++)
                {
                    string abilityId = avatar.StartingAbilityIds[abilityIndex];
                    if (!definition.TryGetCard(abilityId, out CardDefinitionData card))
                    {
                        throw new ArgumentException(
                            $"Avatar '{avatar.DisplayName}' starts with Card '{abilityId}', which is in none of the game's content sets.",
                            nameof(definition));
                    }

                    if (!string.Equals(card.Category, "Ability", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(card.Category, "Action", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"Starting Card '{card.DisplayName}' is not an Ability/Action.", nameof(definition));
                    abilities.Add(card);
                }

                avatars.Add(new TrapFloorAvatarSetup(
                    new ObjectDefinitionId(ParseStableGuid(avatar.StableId, $"Avatar '{avatar.DisplayName}'")),
                    avatar.DisplayName,
                    abilities));
            }

            return avatars;
        }

        // Until the start flow lets players choose (doc 18 §15.9), seats take the Avatars in authored order.
        private static TrapFloorAvatarSetup AvatarForSeat(IReadOnlyList<TrapFloorAvatarSetup> avatars, int seatIndex)
        {
            return avatars[seatIndex % avatars.Count];
        }

        private static int MaximumStartingAbilityCount(IReadOnlyList<TrapFloorAvatarSetup> avatars)
        {
            int maximum = 0;
            for (int seatIndex = 0; seatIndex < PrototypePlayerCount; seatIndex++)
                maximum = Math.Max(maximum, AvatarForSeat(avatars, seatIndex).StartingAbilities.Count);
            return maximum;
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

        // Main's layout default orientation as a yaw offset from the Console (Landscape = +90, as for
        // landscape abilities). Null without a layout, which keeps the avatar at yaw 0.
        private static float? ResolveMainSlotYawOffset(ConsoleLayoutData consoleLayout)
        {
            if (consoleLayout == null) return null;
            if (!consoleLayout.TryGetSlot(MainConsoleSlotKey, out ConsoleLayoutSlotData mainSlot)
                || mainSlot.Kind != ConsoleLayoutSlotKind.Card)
                throw new ArgumentException("The Console layout has no Card slot keyed 'Main'.", nameof(consoleLayout));
            return mainSlot.DefaultOrientation == ConsoleSlotOrientation.Landscape ? 90f : 0f;
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

        private static List<GameTemplateObjectDefinition> BuildObjectDefinitions(
            GameDefinitionData definition,
            IReadOnlyList<TrapFloorAvatarSetup> avatars,
            ObjectDefinitionId pawnDefinitionId,
            ObjectDefinitionId dieDefinitionId)
        {
            List<GameTemplateObjectDefinition> definitions = new List<GameTemplateObjectDefinition>
            {
                new GameTemplateObjectDefinition(pawnDefinitionId, TabletopObjectKind.Pawn, "Player Pawn"),
                new GameTemplateObjectDefinition(dieDefinitionId, TabletopObjectKind.Die, "Six-sided Die"),
            };
            HashSet<ObjectDefinitionId> seen = new HashSet<ObjectDefinitionId>
            {
                pawnDefinitionId,
                dieDefinitionId,
            };
            // Every Avatar needs its own Card definition. An authored ID that matches the Pawn, the Die or another
            // Avatar would make the Avatar card resolve to the wrong kind, so it is reported by name here.
            for (int i = 0; i < avatars.Count; i++)
            {
                if (!seen.Add(avatars[i].DefinitionId))
                {
                    throw new ArgumentException(
                        $"Avatar '{avatars[i].DisplayName}' uses stable ID '{avatars[i].DefinitionId}', which is already used by "
                        + "the Player Pawn, the Die or another Avatar. Give the Avatar a unique stable ID.",
                        nameof(definition));
                }

                definitions.Add(new GameTemplateObjectDefinition(avatars[i].DefinitionId, TabletopObjectKind.Card, avatars[i].DisplayName));
            }
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
            TrapFloorAvatarSetup avatar,
            ObjectDefinitionId pawnDefinitionId,
            GridDefinitionData grid,
            ConsoleSlotDefinitionData mainSlot,
            ConsoleSlotDefinitionData sideSlot,
            IReadOnlyList<CardDefinitionData> controllerInputCards,
            ICollection<GameTemplateSeatDefinition> seats,
            ICollection<GameTemplateContainerDefinition> containers,
            ICollection<GameTemplateContainerMembership> memberships,
            ICollection<GameTemplateObjectInstanceDefinition> objects,
            IDictionary<TabletopObjectId, string> labels,
            ICollection<TrapFloorPlayerSetupDefinition> players,
            float? mainSlotYawOffset,
            TrapFloorSeatPieces seatPieces)
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
            containers.Add(new GameTemplateContainerDefinition(
                handId,
                ContainerKind.Hand,
                seatId,
                ObjectVisibility.OwnerOnly,
                0,
                true,
                ConsoleAdjacentPlacement.ToTablePose(GetConsolePose(layoutSeat), seatPieces.HandZone),
                (float)seatPieces.HandZone.Width,
                (float)seatPieces.HandZone.Depth));
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
                ConsoleAdjacentPlacement.ToTablePose(GetConsolePose(layoutSeat), seatPieces.ActionStack)));
            containers.Add(new GameTemplateContainerDefinition(
                controllerDeckId,
                ContainerKind.Deck,
                seatId,
                ObjectVisibility.Public,
                0,
                true,
                ConsoleAdjacentPlacement.ToTablePose(GetConsolePose(layoutSeat), seatPieces.ControllerDeck),
                pileStyle: ControllerDeckPileStyle));

            TabletopObjectId avatarId = new TabletopObjectId(CreateGuid(42, playerNumber));
            TabletopObjectId pawnId = new TabletopObjectId(CreateGuid(45, playerNumber));
            TabletopPose consolePose = GetConsolePose(layoutSeat);
            TabletopPose avatarPose = mainSlotYawOffset.HasValue
                ? new TabletopPose(TableCoordinate.Zero, consolePose.RotationDegrees + mainSlotYawOffset.Value, 0, 0)
                : TabletopPose.Default;
            objects.Add(CreatePlayerCard(avatarId, avatar.DefinitionId, seatId, avatarPose));
            labels.Add(avatarId, $"P{playerNumber}\n{avatar.DisplayName}");
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

            for (int i = 0; i < avatar.StartingAbilities.Count; i++)
            {
                CardDefinitionData ability = avatar.StartingAbilities[i];
                TabletopObjectId abilityId = new TabletopObjectId(CreateGuid(46, (seatIndex * 100) + i + 1));
                ObjectDefinitionId abilityDefinitionId = new ObjectDefinitionId(
                    ParseStableGuid(ability.StableId, $"Ability '{ability.DisplayName}'"));
                TabletopPose stagingPose = CreateStartingAbilityStagingPose(
                    consolePose,
                    seatPieces.Staging[i],
                    i,
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
            ConsoleLocalRect stagingRect,
            int abilityIndex,
            CardOrientation orientation)
        {
            TabletopPose centre = ConsoleAdjacentPlacement.ToTablePose(consolePose, stagingRect);
            float orientationOffset = orientation == CardOrientation.Landscape ? 90f : 0f;
            return new TabletopPose(
                centre.Position,
                consolePose.RotationDegrees + orientationOffset,
                consolePose.Layer,
                abilityIndex);
        }

        // The pieces beside every Console, in console-local space; the same for every seat.
        private static TrapFloorSeatPieces CreateSeatPieces(ConsoleAdjacentPlacement placement, int stagingCount)
        {
            // Piles sit in bays (bay footprint); the hand zone and staged cards use the card footprint.
            ConsoleLocalRect controllerDeck = placement.PlaceBeside(ConsoleSide.Right, ControllerDeckAlongZ, null, true);
            ConsoleLocalRect handZone = placement.PlaceBeside(ConsoleSide.Right, HandZoneAlongZ, controllerDeck);
            ConsoleLocalRect actionStack = placement.PlaceBeside(ConsoleSide.Left, ActionStackAlongZ, null, true);
            IReadOnlyList<ConsoleLocalRect> staging = placement.PlaceRow(
                ConsoleSide.Left,
                StartingAbilityStagingAlongZ,
                stagingCount,
                actionStack);
            return new TrapFloorSeatPieces(controllerDeck, handZone, actionStack, staging);
        }

        // Build-time check: every seat's Console shape and pieces, the board and the table surface.
        private static void CheckSeatPlacement(
            ConsoleAdjacentPlacement placement,
            TrapFloorSeatPieces pieces,
            PlayerLayoutDefinition playerLayout,
            TabletopBounds boardBounds)
        {
            List<ConsoleAdjacentPlacementItem> items = new List<ConsoleAdjacentPlacementItem>
            {
                new ConsoleAdjacentPlacementItem("board", -1, boardBounds, false),
            };
            for (int seatIndex = 0; seatIndex < PrototypePlayerCount; seatIndex++)
            {
                playerLayout.TryGetSeat(seatIndex, out PlayerSeatLayoutEntry layoutSeat);
                TabletopPose consolePose = GetConsolePose(layoutSeat);
                for (int i = 0; i < placement.ConsoleShape.Count; i++)
                {
                    items.Add(new ConsoleAdjacentPlacementItem(
                        "console",
                        seatIndex,
                        ConsoleAdjacentPlacement.ToTableBounds(consolePose, placement.ConsoleShape[i]),
                        false));
                }

                AddPiece(items, "controller deck", seatIndex, consolePose, pieces.ControllerDeck);
                AddPiece(items, "hand zone", seatIndex, consolePose, pieces.HandZone);
                AddPiece(items, "action stack", seatIndex, consolePose, pieces.ActionStack);
                for (int i = 0; i < pieces.Staging.Count; i++)
                {
                    AddPiece(items, $"staging {i + 1}", seatIndex, consolePose, pieces.Staging[i]);
                }
            }

            ConsoleAdjacentPlacementReport report = ConsoleAdjacentPlacement.Check(
                items,
                playerLayout.TableBounds,
                placement.Settings);
            if (!report.Passes)
            {
                throw new InvalidOperationException($"Trap Floor seat placement check failed: {report.Describe()}.");
            }
        }

        private static void AddPiece(
            List<ConsoleAdjacentPlacementItem> items,
            string label,
            int seatIndex,
            TabletopPose consolePose,
            ConsoleLocalRect rect)
        {
            items.Add(new ConsoleAdjacentPlacementItem(
                label,
                seatIndex,
                ConsoleAdjacentPlacement.ToTableBounds(consolePose, rect),
                true));
        }

        private sealed class TrapFloorSeatPieces
        {
            public TrapFloorSeatPieces(
                ConsoleLocalRect controllerDeck,
                ConsoleLocalRect handZone,
                ConsoleLocalRect actionStack,
                IReadOnlyList<ConsoleLocalRect> staging)
            {
                ControllerDeck = controllerDeck;
                HandZone = handZone;
                ActionStack = actionStack;
                Staging = staging;
            }

            public ConsoleLocalRect ControllerDeck { get; }
            public ConsoleLocalRect HandZone { get; }
            public ConsoleLocalRect ActionStack { get; }
            public IReadOnlyList<ConsoleLocalRect> Staging { get; }
        }

        private sealed class TrapFloorAvatarSetup
        {
            public TrapFloorAvatarSetup(
                ObjectDefinitionId definitionId,
                string displayName,
                IReadOnlyList<CardDefinitionData> startingAbilities)
            {
                DefinitionId = definitionId;
                DisplayName = displayName;
                StartingAbilities = startingAbilities;
            }

            public ObjectDefinitionId DefinitionId { get; }
            public string DisplayName { get; }
            public IReadOnlyList<CardDefinitionData> StartingAbilities { get; }
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
