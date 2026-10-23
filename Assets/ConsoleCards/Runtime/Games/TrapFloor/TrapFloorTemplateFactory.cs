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
    /// Builds the authored Trap Floor starting setup for 2, 3 or 4 Players (PLACE-1, owner 2026-10-21): the seats
    /// used are picked from the standard four-seat layout by the platform rule (<see cref="PlayerLayoutSeatRules"/>),
    /// facing the board and spread out round the table.
    /// </summary>
    public static class TrapFloorTemplateFactory
    {
        public const int StandardPlayerCount = 4;
        /// <summary>
        /// Content sets with this role are the floor groups (doc 18 §15.6). Each draws its count at random, and
        /// together they must fill the Grid.
        /// </summary>
        public const string FloorContentRole = "floor";
        public const string ControllerInputContentSetId = "trap-floor-controller-inputs";

        /// <summary>FLOORS-1 (owner, 2026-10-21): the smallest floor; each floor is one row and one column smaller.</summary>
        public const int MinimumFloorSize = 3;

        private const double PlayerConsoleRadius = 6.1d;

        /// <summary>
        /// T-1 (owner, 2026-10-23): the floor lies on a 35 x 24" playmat. One table unit is 2.5", a card's width, so a
        /// 2.5 x 3.5" card is 1 x 1.4.
        /// </summary>
        public const double PlayMatWidth = 14d;
        public const double PlayMatDepth = 9.6d;

        // Each Console stands this far out from the playmat's edge (to its middle: half a Console's depth with its
        // card slots, 2.48, plus a gap).
        private const double ConsoleOutsidePlayMat = 2.7d;

        // T-2: how far a seat reaches from its Console's middle: across (the Console with its piles, hand and starting
        // Abilities beside it) and deep (the Console with its card slots); and the table's margin past the pieces.
        private const double SeatReachAcross = 7.5d;
        private const double SeatReachDeep = 2.6d;
        private const double TableEdgeMargin = 0.5d;
        // Trap Floor's placement choices beside each Console (console-local along-z). Positions come from
        // ConsoleAdjacentPlacement with its Standard settings (gap, pile size, clearance and margin rules).
        // Right side: Controller Deck, then the Hand zone; left side: Action stack, then the staging row (the Avatar's starting Abilities).
        private const double ControllerDeckAlongZ = 0d;
        // Each seat's Controller Deck rests in a bay marked Draw (default maximum pile height).
        private static readonly GameTemplatePileStyle ControllerDeckPileStyle =
            new GameTemplatePileStyle(GameTemplateBayMark.Draw);
        // T-1: on the playmat, left of the 6x6 floor (full-size tiles) below the Ability piles, clear of the Consoles
        // (collapsed tiles are set aside on the right).
        private const double FloorfallDiceX = -5.45d;
        private const double FloorfallDiceY = -3.6d;
        private const double FloorfallDiceSpacing = 0.9d;
        // The Blind trap's direction die (doc 18; TrapFloorAbilityResolution requires a d4).
        private const int BlindDieSideCount = 4;
        private const float DefaultCameraOrthographicSize = 7.35f;
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
            return CreateStandardFourPlayer(randomValueSource, gameDefinition, selectedModeStableId, consoleLayout, 0);
        }

        /// <summary>How many floors a game has: its authored grid down to <see cref="MinimumFloorSize"/>.</summary>
        public static int FloorCountFor(GridDefinitionData grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            return Math.Max(1, Math.Min(grid.Rows, grid.Columns) - MinimumFloorSize + 1);
        }

        /// <summary>
        /// FLOORS-1 (owner, 2026-10-21): one floor of the game. Floor 0 is the authored grid; each later floor is
        /// one row and one column smaller and is dealt fresh, with the same tile mix scaled down (both Exits and
        /// at least the mode's Key count always stay).
        /// </summary>
        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource,
            GameDefinitionData gameDefinition,
            string selectedModeStableId,
            ConsoleLayoutData consoleLayout,
            int floorIndex)
        {
            return CreateStandardFourPlayer(randomValueSource, gameDefinition, selectedModeStableId, consoleLayout, floorIndex, null);
        }

        /// <summary>
        /// LAYOUT-1 (owner, 2026-10-21): as above, dealt with the players' saved table layout (null: the default
        /// places). The layout places each seat's Console (its hand zone and staging row follow it), the shared piles,
        /// the floor dice and the board's centre; a smaller floor shrinks around that centre.
        /// </summary>
        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource,
            GameDefinitionData gameDefinition,
            string selectedModeStableId,
            ConsoleLayoutData consoleLayout,
            int floorIndex,
            TrapFloorTableLayout tableLayout)
        {
            return CreateStandardFourPlayer(
                randomValueSource, gameDefinition, selectedModeStableId, consoleLayout, floorIndex, tableLayout, StandardPlayerCount);
        }

        /// <summary>
        /// PLACE-1 (owner, 2026-10-21): as above, for <paramref name="playerCount"/> players (2, 3 or 4 within the
        /// game's authored range). Only those seats are dealt; the shared Controller deck and the Ability deck hold
        /// one set per player. SEAT-1: <paramref name="seatPiecesLayout"/> places the pieces beside every Console
        /// (null: the built-in places).
        /// </summary>
        public static TrapFloorTemplateDefinition CreateStandardFourPlayer(
            IRandomValueSource randomValueSource,
            GameDefinitionData gameDefinition,
            string selectedModeStableId,
            ConsoleLayoutData consoleLayout,
            int floorIndex,
            TrapFloorTableLayout tableLayout,
            int playerCount,
            TrapFloorSeatPiecesLayout seatPiecesLayout = null)
        {
            if (randomValueSource == null) throw new ArgumentNullException(nameof(randomValueSource));
            if (gameDefinition == null) throw new ArgumentNullException(nameof(gameDefinition));
            int floorCount = FloorCountFor(gameDefinition.Grid);
            if (floorIndex < 0 || floorIndex >= floorCount) throw new ArgumentOutOfRangeException(nameof(floorIndex));
            if (floorIndex > 0 || (tableLayout != null && tableLayout.BoardCenter.HasValue))
            {
                gameDefinition = WithFloorGrid(gameDefinition, floorIndex, tableLayout?.BoardCenter);
            }

            ModeDefinitionData activeMode = ValidateAndResolveMode(gameDefinition, selectedModeStableId, playerCount);
            Guid gameDefinitionId = ParseStableGuid(gameDefinition.StableId, "Game");
            GridDefinitionData grid = gameDefinition.Grid;
            IReadOnlyList<TrapFloorFloorContentDefinition> floorContent =
                ResolveFloorContent(gameDefinition, activeMode, grid, randomValueSource, floorIndex > 0);
            IReadOnlyList<TrapFloorAvatarSetup> avatars = ResolveAvatars(gameDefinition);
            IReadOnlyList<CardDefinitionData> controllerInputCards = ResolveControllerInputCards(gameDefinition);
            ConsoleSlotDefinitionData mainSlot = ResolveConsoleSlot(gameDefinition.Console, "Main", 1);
            ConsoleSlotDefinitionData sideSlots = ResolveConsoleSlot(gameDefinition.Console, "Side", 1);
            if (mainSlot.PhysicalSlotCount != 1)
                throw new ArgumentException("Trap Floor Console requires exactly one authored Main Slot.", nameof(gameDefinition));
            PlayerLayoutDefinition playerLayout =
                PlayerLayoutSeatRules.ForPlayerCount(PlayerLayoutPresets.StandardFourPlayer, playerCount);
            float? mainSlotYawOffset = ResolveMainSlotYawOffset(consoleLayout);
            IReadOnlyList<int> extraConsoleSlotCapacities =
                ResolveExtraConsoleSlotCapacities(consoleLayout, sideSlots.PhysicalSlotCount);
            ConsoleAdjacentPlacement adjacentPlacement = new ConsoleAdjacentPlacement(
                consoleLayout,
                ConsoleAdjacentPlacementSettings.Standard);
            TrapFloorSeatPieces seatPieces = CreateSeatPieces(
                adjacentPlacement,
                MaximumStartingAbilityCount(avatars, playerCount),
                seatPiecesLayout ?? TrapFloorSeatPiecesLayout.Default);
            GameTemplateId templateId = new GameTemplateId(
                string.Equals(activeMode.StableId, gameDefinition.DefaultModeStableId, StringComparison.OrdinalIgnoreCase)
                    ? gameDefinitionId
                    : CreateGuid(1, StableStringHash(activeMode.StableId)));
            PlayAreaId boardPlayAreaId = new PlayAreaId(CreateGuid(2, 1));
            ObjectDefinitionId pawnDefinitionId = new ObjectDefinitionId(CreateGuid(20, 8));
            ObjectDefinitionId dieDefinitionId = new ObjectDefinitionId(CreateGuid(20, 10));
            ObjectDefinitionId blindDieDefinitionId = new ObjectDefinitionId(CreateGuid(20, 11));

            List<GameTemplateObjectDefinition> objectDefinitions = BuildObjectDefinitions(
                gameDefinition,
                avatars,
                pawnDefinitionId,
                dieDefinitionId,
                blindDieDefinitionId);
            List<GameTemplateSeatDefinition> seats = new List<GameTemplateSeatDefinition>(playerCount);
            List<GameTemplateContainerDefinition> containers = new List<GameTemplateContainerDefinition>();
            List<GameTemplateObjectInstanceDefinition> objects = new List<GameTemplateObjectInstanceDefinition>();
            List<GameTemplateContainerMembership> memberships = new List<GameTemplateContainerMembership>();
            List<TrapFloorPlayerSetupDefinition> players = new List<TrapFloorPlayerSetupDefinition>(playerCount);
            Dictionary<TabletopObjectId, string> labels = new Dictionary<TabletopObjectId, string>();
            Dictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds =
                new Dictionary<TrapFloorCoordinate, TabletopObjectId>();

            CreateFloorBoard(floorContent, grid, randomValueSource, floorCardIds, labels, objects);

            TrapFloorCoordinate[] startingCorners = AssignStartingCorners(playerLayout, grid, playerCount);

            // TABLE-1 (owner, 2026-10-21, as in the playtest): one Controller deck for everyone beside the board.
            ContainerId controllerDeckId = CreateSharedControllerDeck(
                controllerInputCards, playerCount, grid, randomValueSource, containers, memberships, objects, labels,
                tableLayout?.ControllerDeck);

            for (int seatIndex = 0; seatIndex < playerCount; seatIndex++)
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
                    controllerDeckId,
                    tableLayout?.SeatConsole(seatIndex),
                    seats,
                    containers,
                    memberships,
                    objects,
                    labels,
                    players,
                    mainSlotYawOffset,
                    seatPieces,
                    extraConsoleSlotCapacities);
            }

            TabletopObjectId floorfallXAxisDieId = new TabletopObjectId(CreateGuid(60, 1));
            TabletopObjectId floorfallYAxisDieId = new TabletopObjectId(CreateGuid(60, 2));
            TableCoordinate xAxisDie = tableLayout?.DieX?.Position
                ?? new TableCoordinate(FloorfallDiceX - (FloorfallDiceSpacing * 0.5d), FloorfallDiceY);
            TableCoordinate yAxisDie = tableLayout?.DieY?.Position
                ?? new TableCoordinate(FloorfallDiceX + (FloorfallDiceSpacing * 0.5d), FloorfallDiceY);
            objects.Add(CreateFloorfallDie(floorfallXAxisDieId, dieDefinitionId, xAxisDie.X, xAxisDie.Y));
            objects.Add(CreateFloorfallDie(floorfallYAxisDieId, dieDefinitionId, yAxisDie.X, yAxisDie.Y));
            // D4-1 (owner, 2026-10-21): the d4 the Blind trap rolls for its direction lies beside the floor dice
            // (next to the Y die, so it follows a saved table layout too).
            objects.Add(CreateFloorfallDie(
                new TabletopObjectId(CreateGuid(60, 3)),
                blindDieDefinitionId,
                yAxisDie.X + FloorfallDiceSpacing,
                yAxisDie.Y,
                BlindDieSideCount));

            ContainerId abilityDeckId = CreateAbilityDeck(
                gameDefinition, playerCount, grid, randomValueSource, containers, memberships, objects, labels,
                tableLayout?.AbilityDeck);
            ContainerId abilityDiscardId = CreateAbilityDiscard(grid, containers, memberships, tableLayout?.AbilityDiscard);

            TabletopBounds boardBounds = CreateBoardBounds(grid);
            // A saved layout is the players' own choice of places: the default seat check does not apply to it.
            if (!adjacentPlacement.UsesFallbackShape && (tableLayout == null || !tableLayout.HasSeatPlaces))
            {
                CheckSeatPlacement(adjacentPlacement, seatPieces, playerLayout, boardBounds);
            }
            GameTemplate template = new GameTemplate(
                templateId,
                GameTemplate.CurrentSchemaVersion,
                floorIndex == 0
                    ? $"{gameDefinition.DisplayName} — {activeMode.DisplayName}"
                    : $"{gameDefinition.DisplayName} — {activeMode.DisplayName} — Floor {floorIndex + 1}",
                gameDefinition.ManualRules,
                playerLayout.Id,
                playerCount,
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
                CatalogLayouts(playerLayout));

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
                floorfallYAxisDieId,
                floorIndex,
                floorCount,
                abilityDeckId,
                abilityDiscardId,
                tableLayout != null);
        }

        // TABLE-1 (owner, 2026-10-21): the Ability discard pile, just above the Ability deck. Used Abilities go there;
        // when the Ability deck runs out, the discard is shuffled into it.
        private static ContainerId CreateAbilityDiscard(
            GridDefinitionData grid,
            List<GameTemplateContainerDefinition> containers,
            List<GameTemplateContainerMembership> memberships,
            TabletopPose? placed = null)
        {
            ContainerId discardId = new ContainerId(CreateGuid(71, 3));
            TabletopPose deckPose = AbilityDeckPose(grid);
            containers.Add(new GameTemplateContainerDefinition(
                discardId,
                ContainerKind.DiscardPile,
                SeatId.Empty,
                ObjectVisibility.Public,
                0,
                true,
                placed ?? new TabletopPose(
                    new TableCoordinate(deckPose.Position.X, deckPose.Position.Y + AbilityDiscardAboveDeck),
                    0f,
                    0,
                    0)));
            memberships.Add(new GameTemplateContainerMembership(discardId, Array.Empty<TabletopObjectId>()));
            return discardId;
        }

        // TABLE-1 (owner, 2026-10-21): every player's set of Controller Cards in one shuffled, face-down deck off the
        // board's bottom-right corner (the Ability piles are beside its left side). Paid cards go straight back into it.
        // Owner, 2026-10-21 (layout shown in a screenshot): the Ability deck beside the board's left side, level with
        // its middle, with the discard pile above it.
        private const double AbilityDiscardAboveDeck = 2.0d;
        private const double AbilityPilesClearanceX = 1.4d;

        private static ContainerId CreateSharedControllerDeck(
            IReadOnlyList<CardDefinitionData> controllerInputCards,
            int playerCount,
            GridDefinitionData grid,
            IRandomValueSource randomValueSource,
            List<GameTemplateContainerDefinition> containers,
            List<GameTemplateContainerMembership> memberships,
            List<GameTemplateObjectInstanceDefinition> objects,
            Dictionary<TabletopObjectId, string> labels,
            TabletopPose? placed = null)
        {
            ContainerId deckId = new ContainerId(CreateGuid(71, 2));
            TabletopBounds board = CreateBoardBounds(grid);
            containers.Add(new GameTemplateContainerDefinition(
                deckId,
                ContainerKind.Deck,
                SeatId.Empty,
                ObjectVisibility.Public,
                0,
                true,
                placed ?? new TabletopPose(
                    new TableCoordinate(board.Maximum.X + AbilityDeckClearanceX, board.Minimum.Y - AbilityDeckClearanceY),
                    0f,
                    0,
                    0),
                pileStyle: ControllerDeckPileStyle));

            List<TabletopObjectId> cardIds = new List<TabletopObjectId>(controllerInputCards.Count * playerCount);
            for (int seatIndex = 0; seatIndex < playerCount; seatIndex++)
            {
                for (int i = 0; i < controllerInputCards.Count; i++)
                {
                    CardDefinitionData controllerCard = controllerInputCards[i];
                    TabletopObjectId controllerCardId = new TabletopObjectId(CreateGuid(47, (seatIndex * 10000) + i + 1));
                    objects.Add(new GameTemplateObjectInstanceDefinition(
                        controllerCardId,
                        new ObjectDefinitionId(
                            ParseStableGuid(controllerCard.StableId, $"Controller Card '{controllerCard.DisplayName}'")),
                        TabletopObjectKind.Card,
                        TabletopPose.Default,
                        SeatId.Empty,
                        ObjectVisibility.Public,
                        false,
                        CardFace.FaceDown));
                    cardIds.Add(controllerCardId);
                    labels.Add(controllerCardId, controllerCard.DisplayName);
                }
            }

            // Shuffled on every deal.
            for (int i = cardIds.Count - 1; i > 0; i--)
            {
                int pick = randomValueSource.NextInt(0, i + 1);
                TabletopObjectId swap = cardIds[i];
                cardIds[i] = cardIds[pick];
                cardIds[pick] = swap;
            }

            memberships.Add(new GameTemplateContainerMembership(deckId, cardIds));
            return deckId;
        }

        // ABIL-DECK (owner, 2026-10-21): buying is from the table. A shared, shuffled, face-down Ability deck beside
        // the board holds every Ability that has a price, one copy per player. Empty when no Ability has a price yet.
        // It sits beside the board's left side, measured from the floor, so it stays clear of the board's frame on every
        // floor size. The clearances below place the shared Controller deck off the board's bottom-right corner.
        private const double AbilityDeckClearanceX = 1.85d; // board frame, a gap and half a deck bay, with room to spare
        private const double AbilityDeckClearanceY = 0.9d;

        private static ContainerId CreateAbilityDeck(
            GameDefinitionData gameDefinition,
            int playerCount,
            GridDefinitionData grid,
            IRandomValueSource randomValueSource,
            List<GameTemplateContainerDefinition> containers,
            List<GameTemplateContainerMembership> memberships,
            List<GameTemplateObjectInstanceDefinition> objects,
            Dictionary<TabletopObjectId, string> labels,
            TabletopPose? placed = null)
        {
            List<CardDefinitionData> forSale = new List<CardDefinitionData>();
            List<CardDefinitionData> catalog = TrapFloorShop.Catalog(gameDefinition);
            for (int i = 0; i < catalog.Count; i++)
            {
                if (TrapFloorShop.IsForSale(catalog[i])) forSale.Add(catalog[i]);
            }

            if (forSale.Count == 0) return ContainerId.Empty;
            List<CardDefinitionData> copies = new List<CardDefinitionData>(forSale.Count * playerCount);
            for (int copy = 0; copy < playerCount; copy++) copies.AddRange(forSale);
            IReadOnlyList<CardDefinitionData> order = Shuffled(copies, randomValueSource);

            ContainerId deckId = new ContainerId(CreateGuid(71, 1));
            containers.Add(new GameTemplateContainerDefinition(
                deckId,
                ContainerKind.Deck,
                SeatId.Empty,
                ObjectVisibility.Public,
                0,
                true,
                placed ?? AbilityDeckPose(grid),
                pileStyle: ControllerDeckPileStyle));
            List<TabletopObjectId> cardIds = new List<TabletopObjectId>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                CardDefinitionData card = order[i];
                TabletopObjectId cardId = new TabletopObjectId(CreateGuid(72, i + 1));
                objects.Add(new GameTemplateObjectInstanceDefinition(
                    cardId,
                    new ObjectDefinitionId(ParseStableGuid(card.StableId, $"Ability '{card.DisplayName}'")),
                    TabletopObjectKind.Card,
                    TabletopPose.Default,
                    SeatId.Empty,
                    ObjectVisibility.Public,
                    false,
                    CardFace.FaceDown));
                cardIds.Add(cardId);
                labels.Add(cardId, card.DisplayName);
            }

            memberships.Add(new GameTemplateContainerMembership(deckId, cardIds));
            return deckId;
        }

        private static TabletopPose AbilityDeckPose(GridDefinitionData grid)
        {
            TabletopBounds board = CreateBoardBounds(grid);
            return new TabletopPose(
                new TableCoordinate(
                    board.Minimum.X - AbilityPilesClearanceX,
                    board.Center.Y),
                0f,
                0,
                0);
        }

        // The game data with the floor's smaller grid (same cell size and centre); everything else is unchanged.
        private static GameDefinitionData WithFloorGrid(GameDefinitionData data, int floorIndex, TableCoordinate? center = null)
        {
            GridDefinitionData grid = data.Grid;
            GridDefinitionData floorGrid = new GridDefinitionData(
                grid.StableId,
                grid.Rows - floorIndex,
                grid.Columns - floorIndex,
                grid.CellWidth,
                grid.CellHeight,
                grid.HorizontalSpacing,
                grid.VerticalSpacing,
                center.HasValue ? center.Value.X : grid.OriginX, // LAYOUT-1: the board where the players put it
                center.HasValue ? center.Value.Y : grid.OriginY,
                grid.LayoutMetadata);
            return new GameDefinitionData(
                data.StableId,
                data.DisplayName,
                data.ManualRules,
                data.MinimumPlayers,
                data.MaximumPlayers,
                floorGrid,
                data.Cards,
                data.ContentSets,
                data.Avatars,
                data.Modes,
                data.DefaultModeStableId,
                data.Console,
                data.InputVocabulary,
                data.ControllerConfiguration,
                data.ControllerMappingKind,
                data.PresentationReference,
                data.AssistanceConfiguration,
                data.RuleSettings);
        }

        private static IReadOnlyList<CardDefinitionData> Shuffled(
            IReadOnlyList<CardDefinitionData> cards,
            IRandomValueSource randomValueSource)
        {
            List<CardDefinitionData> shuffled = new List<CardDefinitionData>(cards);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int pick = randomValueSource.NextInt(0, i + 1);
                CardDefinitionData swap = shuffled[i];
                shuffled[i] = shuffled[pick];
                shuffled[pick] = swap;
            }

            return shuffled;
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

        /// <summary>PLACE-1: the fewest players this game can be dealt for (the authored minimum, at least 1).</summary>
        public static int MinimumPlayerCount(GameDefinitionData definition) =>
            Math.Max(1, Math.Min(definition?.MinimumPlayers ?? StandardPlayerCount, StandardPlayerCount));

        /// <summary>PLACE-1: the most players this game can be dealt for (the authored maximum, at most the seats).</summary>
        public static int MaximumPlayerCount(GameDefinitionData definition) =>
            Math.Max(MinimumPlayerCount(definition), Math.Min(definition?.MaximumPlayers ?? StandardPlayerCount, StandardPlayerCount));

        // The template's own layout first (the validator looks it up), then the built-in presets.
        private static PlayerLayoutDefinition[] CatalogLayouts(PlayerLayoutDefinition playerLayout)
        {
            return playerLayout == PlayerLayoutPresets.StandardFourPlayer
                ? new[] { PlayerLayoutPresets.StandardFourPlayer, PlayerLayoutPresets.CompactFourPlayer, PlayerLayoutPresets.EightPlayer }
                : new[] { playerLayout, PlayerLayoutPresets.StandardFourPlayer, PlayerLayoutPresets.CompactFourPlayer, PlayerLayoutPresets.EightPlayer };
        }

        private static InvalidOperationException MissingAuthoredDefinition()
        {
            return new InvalidOperationException(
                "Trap Floor Template construction requires an authored Game Definition. The former C# content fallback has been removed.");
        }

        private static ModeDefinitionData ValidateAndResolveMode(
            GameDefinitionData definition,
            string selectedModeStableId,
            int playerCount)
        {
            if (definition.Grid == null)
                throw new ArgumentException("Trap Floor requires an authored Grid Definition.", nameof(definition));
            if (definition.Console == null)
                throw new ArgumentException("Trap Floor requires an authored Console Configuration.", nameof(definition));
            if (playerCount < MinimumPlayerCount(definition) || playerCount > MaximumPlayerCount(definition))
            {
                throw new ArgumentException(
                    $"Trap Floor cannot be dealt for {playerCount} Players: the authored range is {definition.MinimumPlayers}-{definition.MaximumPlayers} and the layout has {StandardPlayerCount} seats.",
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
            IRandomValueSource randomValueSource,
            bool scaleToGrid)
        {
            List<TrapFloorFloorContentDefinition> drawn = new List<TrapFloorFloorContentDefinition>(grid.CellCount);
            List<FloorGroupDraw> groups = new List<FloorGroupDraw>();
            for (int setIndex = 0; setIndex < gameDefinition.ContentSets.Count; setIndex++)
            {
                GameContentSetData contentSet = gameDefinition.ContentSets[setIndex];
                if (!string.Equals(contentSet.Role, FloorContentRole, StringComparison.OrdinalIgnoreCase)) continue;
                List<TrapFloorFloorContentDefinition> pool = ExpandFloorGroup(gameDefinition, contentSet);
                groups.Add(new FloorGroupDraw(
                    contentSet,
                    pool,
                    ResolveFloorDrawCount(gameDefinition, activeMode, contentSet, pool.Count)));
            }

            if (scaleToGrid) ScaleFloorDrawCounts(groups, grid.CellCount, activeMode.RequiredKeyCount);
            for (int i = 0; i < groups.Count; i++)
            {
                DrawFloorGroup(groups[i].ContentSet, groups[i].Pool, groups[i].Count, randomValueSource, drawn);
            }

            int groupCount = groups.Count;
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

        private enum FloorGroupKind
        {
            Other = 0,
            Keys = 1,
            Exits = 2,
        }

        private sealed class FloorGroupDraw
        {
            public FloorGroupDraw(GameContentSetData contentSet, List<TrapFloorFloorContentDefinition> pool, int count)
            {
                ContentSet = contentSet;
                Pool = pool;
                Count = count;
                BaseCount = count;
                Kind = KindOf(pool);
            }

            public GameContentSetData ContentSet { get; }
            public List<TrapFloorFloorContentDefinition> Pool { get; }
            public int Count { get; set; }
            public int BaseCount { get; }
            public FloorGroupKind Kind { get; }
            public int MaximumCount => ContentSet.AllowRepeats ? int.MaxValue : Pool.Count;

            private static FloorGroupKind KindOf(List<TrapFloorFloorContentDefinition> pool)
            {
                if (pool.Count == 0) return FloorGroupKind.Other;
                TrapFloorFloorContentCategory first = pool[0].Category;
                for (int i = 1; i < pool.Count; i++)
                {
                    if (pool[i].Category != first) return FloorGroupKind.Other;
                }

                return first == TrapFloorFloorContentCategory.Key
                    ? FloorGroupKind.Keys
                    : first == TrapFloorFloorContentCategory.SecretExit
                        ? FloorGroupKind.Exits
                        : FloorGroupKind.Other;
            }
        }

        // FLOORS-1 (owner, 2026-10-21: "same mix, scaled"): a smaller floor keeps every Exit, scales the Keys but
        // never below the mode's Key count, and shares the remaining cells among the other groups in proportion
        // (largest remainder), so the counts add up to the floor's cells.
        private static void ScaleFloorDrawCounts(List<FloorGroupDraw> groups, int cellCount, int requiredKeys)
        {
            int baseTotal = 0;
            for (int i = 0; i < groups.Count; i++) baseTotal += groups[i].BaseCount;
            if (baseTotal == 0 || baseTotal == cellCount) return;
            double ratio = cellCount / (double)baseTotal;

            int fixedTotal = 0;
            int keys = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                FloorGroupDraw group = groups[i];
                if (group.Kind == FloorGroupKind.Exits)
                {
                    fixedTotal += group.Count;
                }
                else if (group.Kind == FloorGroupKind.Keys)
                {
                    group.Count = Math.Min(group.BaseCount, (int)Math.Round(group.BaseCount * ratio, MidpointRounding.AwayFromZero));
                    keys += group.Count;
                }
            }

            for (int i = 0; i < groups.Count && keys < requiredKeys; i++)
            {
                FloorGroupDraw group = groups[i];
                while (group.Kind == FloorGroupKind.Keys && keys < requiredKeys && group.Count < group.MaximumCount)
                {
                    group.Count++;
                    keys++;
                }
            }

            fixedTotal += keys;
            int remaining = cellCount - fixedTotal;
            int otherBase = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Kind == FloorGroupKind.Other) otherBase += groups[i].BaseCount;
            }

            if (remaining < 0 || (remaining > 0 && otherBase == 0))
            {
                throw new ArgumentException(
                    $"A floor of {cellCount} tiles cannot hold the Exits and Keys ({fixedTotal}) with the other tiles in proportion.");
            }

            double[] leftOver = new double[groups.Count];
            int assigned = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                FloorGroupDraw group = groups[i];
                leftOver[i] = -1d;
                if (group.Kind != FloorGroupKind.Other) continue;
                double exact = otherBase == 0 ? 0d : remaining * (group.BaseCount / (double)otherBase);
                group.Count = Math.Min(group.MaximumCount, (int)Math.Floor(exact));
                leftOver[i] = exact - group.Count;
                assigned += group.Count;
            }

            while (assigned < remaining)
            {
                int best = -1;
                for (int i = 0; i < groups.Count; i++)
                {
                    FloorGroupDraw group = groups[i];
                    if (group.Kind != FloorGroupKind.Other || group.Count >= group.MaximumCount) continue;
                    if (best < 0
                        || leftOver[i] > leftOver[best]
                        || (leftOver[i] == leftOver[best] && group.BaseCount > groups[best].BaseCount))
                    {
                        best = i;
                    }
                }

                if (best < 0)
                {
                    throw new ArgumentException($"The floor groups cannot fill a floor of {cellCount} tiles.");
                }

                groups[best].Count++;
                leftOver[best] = -1d;
                assigned++;
            }
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

        private static int MaximumStartingAbilityCount(IReadOnlyList<TrapFloorAvatarSetup> avatars, int playerCount)
        {
            int maximum = 0;
            for (int seatIndex = 0; seatIndex < playerCount; seatIndex++)
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

        // Doc 22 §14: the layout can have more Card slots than Trap Floor's authored Main and Side slots (the mat's
        // corner slots and the back's A/B/X/Y). Every Console has every slot, so those follow the Side slots in
        // layout order. The rules do not say where on the Console a card goes (owner, 2026-10-15): every slot but
        // Main is an item slot. Without a layout there are none.
        private static IReadOnlyList<int> ResolveExtraConsoleSlotCapacities(ConsoleLayoutData consoleLayout, int sideSlotCount)
        {
            if (consoleLayout == null) return Array.Empty<int>();
            int used = 1 + sideSlotCount;
            int extraCount = consoleLayout.CardSlots.Count - used;
            if (extraCount <= 0) return Array.Empty<int>();
            int[] capacities = new int[extraCount];
            for (int i = 0; i < extraCount; i++) capacities[i] = consoleLayout.CardSlotCapacities[used + i];
            return capacities;
        }

        private static List<ContainerId> ConcatSlotIds(ContainerId[] first, ContainerId[] second)
        {
            List<ContainerId> ids = new List<ContainerId>(first.Length + second.Length);
            ids.AddRange(first);
            ids.AddRange(second);
            return ids;
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
            ObjectDefinitionId dieDefinitionId,
            ObjectDefinitionId blindDieDefinitionId)
        {
            List<GameTemplateObjectDefinition> definitions = new List<GameTemplateObjectDefinition>
            {
                new GameTemplateObjectDefinition(pawnDefinitionId, TabletopObjectKind.Pawn, "Player Pawn"),
                new GameTemplateObjectDefinition(dieDefinitionId, TabletopObjectKind.Die, "Six-sided Die"),
                new GameTemplateObjectDefinition(blindDieDefinitionId, TabletopObjectKind.Die, "Four-sided Die"),
            };
            HashSet<ObjectDefinitionId> seen = new HashSet<ObjectDefinitionId>
            {
                pawnDefinitionId,
                dieDefinitionId,
                blindDieDefinitionId,
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
            ContainerId controllerDeckId,
            TabletopPose? consolePoseOverride,
            ICollection<GameTemplateSeatDefinition> seats,
            ICollection<GameTemplateContainerDefinition> containers,
            ICollection<GameTemplateContainerMembership> memberships,
            ICollection<GameTemplateObjectInstanceDefinition> objects,
            IDictionary<TabletopObjectId, string> labels,
            ICollection<TrapFloorPlayerSetupDefinition> players,
            float? mainSlotYawOffset,
            TrapFloorSeatPieces seatPieces,
            IReadOnlyList<int> extraConsoleSlotCapacities)
        {
            // LAYOUT-1: the seat's Console where the saved layout puts it; its hand zone and staging row follow it.
            TabletopPose seatConsolePose = consolePoseOverride ?? GetConsolePose(layoutSeat);
            int playerNumber = seatIndex + 1;
            int idBase = seatIndex * 20;
            SeatId seatId = new SeatId(CreateGuid(40, playerNumber));
            ContainerId handId = new ContainerId(CreateGuid(41, idBase + 1));
            ContainerId mainSlotId = new ContainerId(CreateGuid(41, idBase + 2));
            ContainerId[] sideSlotIds = new ContainerId[sideSlot.PhysicalSlotCount];
            for (int i = 0; i < sideSlotIds.Length; i++)
                sideSlotIds[i] = new ContainerId(CreateGuid(41, idBase + 3 + i));

            ContainerId[] extraSlotIds = new ContainerId[extraConsoleSlotCapacities.Count];
            for (int i = 0; i < extraSlotIds.Length; i++)
                extraSlotIds[i] = new ContainerId(CreateGuid(48, (seatIndex * 32) + i + 1));

            List<ContainerId> consoleSlotIds = new List<ContainerId>(1 + sideSlotIds.Length + extraSlotIds.Length) { mainSlotId };
            consoleSlotIds.AddRange(sideSlotIds);
            consoleSlotIds.AddRange(extraSlotIds);
            seats.Add(new GameTemplateSeatDefinition(
                seatId,
                seatIndex,
                handId,
                consoleSlotIds,
                seatConsolePose));
            // The authored maximum is an assisted draw target, not a physical capacity. Zero keeps
            // the Hand technically unbounded for freeform draws beyond that recommendation.
            containers.Add(new GameTemplateContainerDefinition(
                handId,
                ContainerKind.Hand,
                seatId,
                ObjectVisibility.OwnerOnly,
                0,
                true,
                ConsoleAdjacentPlacement.ToTablePose(seatConsolePose, seatPieces.HandZone),
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

            for (int i = 0; i < extraSlotIds.Length; i++)
            {
                containers.Add(CreateContainer(
                    extraSlotIds[i],
                    ContainerKind.ConsoleSlot,
                    seatId,
                    ObjectVisibility.Public,
                    extraConsoleSlotCapacities[i]));
            }

            // TABLE-1: no Controller deck or Action stack per seat. A bought Ability that does not fit in the Hand
            // is placed loose where the stack was, beside the Console.
            TabletopPose abilityOverflowPose =
                ConsoleAdjacentPlacement.ToTablePose(seatConsolePose, seatPieces.ActionStack);

            TabletopObjectId avatarId = new TabletopObjectId(CreateGuid(42, playerNumber));
            TabletopObjectId pawnId = new TabletopObjectId(CreateGuid(45, playerNumber));
            TabletopPose consolePose = seatConsolePose;
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

            for (int i = 0; i < extraSlotIds.Length; i++)
            {
                memberships.Add(new GameTemplateContainerMembership(extraSlotIds[i], Array.Empty<TabletopObjectId>()));
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
                    ability.Orientation,
                    seatPieces.StagingTurnDegrees);
                objects.Add(CreatePlayerCard(abilityId, abilityDefinitionId, seatId, stagingPose));
                labels.Add(abilityId, ability.DisplayName);
            }

            memberships.Add(new GameTemplateContainerMembership(handId, Array.Empty<TabletopObjectId>()));
            players.Add(new TrapFloorPlayerSetupDefinition(
                seatIndex,
                seatId,
                handId,
                mainSlotId,
                ConcatSlotIds(sideSlotIds, extraSlotIds),
                controllerDeckId,
                ContainerId.Empty,
                avatarId,
                pawnId,
                startingCorner,
                abilityOverflowPose));
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
            CardOrientation orientation,
            float turnDegrees)
        {
            TabletopPose centre = ConsoleAdjacentPlacement.ToTablePose(consolePose, stagingRect);
            float orientationOffset = orientation == CardOrientation.Landscape ? 90f : 0f;
            return new TabletopPose(
                centre.Position,
                consolePose.RotationDegrees + orientationOffset + turnDegrees,
                consolePose.Layer,
                abilityIndex);
        }

        // The pieces beside every Console, in console-local space; the same for every seat. SEAT-1: the hand zone,
        // the action stack and the starting Abilities follow the seat pieces layout (z toward the board, x outward).
        private static TrapFloorSeatPieces CreateSeatPieces(
            ConsoleAdjacentPlacement placement,
            int stagingCount,
            TrapFloorSeatPiecesLayout layout)
        {
            // Piles sit in bays (bay footprint); the hand zone and staged cards use the card footprint.
            ConsoleLocalRect controllerDeck = placement.PlaceBeside(ConsoleSide.Right, ControllerDeckAlongZ, null, true);
            ConsoleLocalRect handZone = MoveOutward(
                placement.PlaceBeside(ConsoleSide.Right, layout.HandZoneTowardBoard, controllerDeck),
                ConsoleSide.Right,
                layout.HandZoneOutward);
            ConsoleLocalRect actionStack = MoveOutward(
                placement.PlaceBeside(ConsoleSide.Left, layout.ActionStackTowardBoard, null, true),
                ConsoleSide.Left,
                layout.ActionStackOutward);
            // A row going outward past the pile on its side, each card the layout's gap from the one before.
            ConsoleSide side = layout.StartingAbilitiesSide;
            ConsoleLocalRect innerPiece = side == ConsoleSide.Left ? actionStack : handZone;
            List<ConsoleLocalRect> staging = new List<ConsoleLocalRect>(stagingCount);
            for (int i = 0; i < stagingCount; i++)
            {
                staging.Add(i == 0
                    ? MoveOutward(
                        placement.PlaceBeside(side, layout.StartingAbilitiesTowardBoard, innerPiece),
                        side,
                        layout.StartingAbilitiesOutward)
                    : MoveOutward(staging[i - 1], side, staging[i - 1].Width + layout.StartingAbilitiesGap));
            }

            return new TrapFloorSeatPieces(
                controllerDeck,
                handZone,
                actionStack,
                staging,
                layout.StartingAbilitiesTurnDegrees);
        }

        private static ConsoleLocalRect MoveOutward(ConsoleLocalRect rect, ConsoleSide side, double outward)
        {
            return outward == 0d
                ? rect
                : new ConsoleLocalRect(rect.CenterX + ((int)side * outward), rect.CenterZ, rect.Width, rect.Depth);
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
            for (int seatIndex = 0; seatIndex < playerLayout.OccupiedSeatCount; seatIndex++)
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

            // T-2: no fixed table edge here; the table grows to hold every seat when the game starts.
            ConsoleAdjacentPlacementReport report = ConsoleAdjacentPlacement.Check(
                items,
                null,
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
                IReadOnlyList<ConsoleLocalRect> staging,
                float stagingTurnDegrees)
            {
                ControllerDeck = controllerDeck;
                HandZone = handZone;
                ActionStack = actionStack;
                Staging = staging;
                StagingTurnDegrees = stagingTurnDegrees;
            }

            public ConsoleLocalRect ControllerDeck { get; }
            public ConsoleLocalRect HandZone { get; }
            public ConsoleLocalRect ActionStack { get; }
            public IReadOnlyList<ConsoleLocalRect> Staging { get; }
            public float StagingTurnDegrees { get; }
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
            double tableY,
            int sideCount = TrapFloorFloorfallService.DieSideCount)
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
                sideCount,
                1);
        }

        // Each pawn starts on a corner on its own side of the board: of the two corners on the seat's side, the one
        // to the player's left as they face the board. Worked out from where each seat sits, so any layout works;
        // a seat never takes a corner another seat already has (more seats than corners then share).
        private static TrapFloorCoordinate[] AssignStartingCorners(
            PlayerLayoutDefinition playerLayout,
            GridDefinitionData grid,
            int seatCount)
        {
            TrapFloorCoordinate[] corners =
            {
                new TrapFloorCoordinate(1, 1),
                new TrapFloorCoordinate(grid.Columns, 1),
                new TrapFloorCoordinate(grid.Columns, grid.Rows),
                new TrapFloorCoordinate(1, grid.Rows),
            };
            double[] cornerX = { -1d, 1d, 1d, -1d };
            double[] cornerY = { -1d, -1d, 1d, 1d };
            bool[] taken = new bool[corners.Length];
            TrapFloorCoordinate[] result = new TrapFloorCoordinate[seatCount];
            for (int seat = 0; seat < seatCount; seat++)
            {
                double outX = 0d;
                double outY = -1d;
                if (playerLayout.TryGetSeat(seat, out PlayerSeatLayoutEntry entry))
                {
                    double dx = entry.PlayerZonePose.Position.X - grid.OriginX;
                    double dy = entry.PlayerZonePose.Position.Y - grid.OriginY;
                    double length = Math.Sqrt((dx * dx) + (dy * dy));
                    if (length > 1e-6d)
                    {
                        outX = dx / length;
                        outY = dy / length;
                    }
                }

                // The player's left as they face the board centre.
                double leftX = outY;
                double leftY = -outX;
                int best = -1;
                double bestScore = double.NegativeInfinity;
                for (int c = 0; c < corners.Length; c++)
                {
                    if (taken[c]) continue;
                    double score = (cornerX[c] * outX) + (cornerY[c] * outY)
                        + (0.5d * ((cornerX[c] * leftX) + (cornerY[c] * leftY)));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }

                if (best < 0)
                {
                    best = seat % corners.Length;
                }
                else
                {
                    taken[best] = true;
                }

                result[seat] = corners[best];
            }

            return result;
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
            return (float)(DefaultCameraOrthographicSize * scale);
        }

        public static TabletopPose GetConsolePose(PlayerSeatLayoutEntry layoutSeat)
        {
            if (layoutSeat == null) throw new ArgumentNullException(nameof(layoutSeat));
            // T-1: each Console stands outside the playmat (the circle grows only along the seat's direction).
            TabletopPose anchor = layoutSeat.ConsoleAnchorPose;
            return ProjectToRadius(anchor, Math.Max(PlayerConsoleRadius, PlayMatEdgeDistance(anchor.Position) + ConsoleOutsidePlayMat));
        }

        /// <summary>
        /// T-2 (owner, 2026-10-23: "resize the table to a default point where the maximum number of players can be
        /// seated"): half the table's width and depth (around the board's centre) that the playmat and every seat of
        /// the 4-player layout need, with a margin.
        /// </summary>
        public static void TableHalfSizeForMostPlayers(out double halfWidth, out double halfDepth)
        {
            halfWidth = PlayMatWidth * 0.5d;
            halfDepth = PlayMatDepth * 0.5d;
            PlayerLayoutDefinition layout = PlayerLayoutPresets.StandardFourPlayer;
            for (int i = 0; i < layout.OccupiedSeatCount; i++)
            {
                if (!layout.TryGetSeat(i, out PlayerSeatLayoutEntry seat)) continue;
                TabletopPose pose = GetConsolePose(seat);
                double radians = pose.RotationDegrees * (Math.PI / 180d);
                double cos = Math.Abs(Math.Cos(radians));
                double sin = Math.Abs(Math.Sin(radians));
                halfWidth = Math.Max(halfWidth, Math.Abs(pose.Position.X) + (cos * SeatReachAcross) + (sin * SeatReachDeep));
                halfDepth = Math.Max(halfDepth, Math.Abs(pose.Position.Y) + (sin * SeatReachAcross) + (cos * SeatReachDeep));
            }

            halfWidth += TableEdgeMargin;
            halfDepth += TableEdgeMargin;
        }

        // From the board's centre to the playmat's edge, in the direction of the point.
        private static double PlayMatEdgeDistance(TableCoordinate point)
        {
            double length = Math.Sqrt((point.X * point.X) + (point.Y * point.Y));
            if (length <= 0d) return 0d;
            double dx = Math.Abs(point.X) / length;
            double dy = Math.Abs(point.Y) / length;
            double toSide = dx > 1e-9d ? (PlayMatWidth * 0.5d) / dx : double.PositiveInfinity;
            double toEnd = dy > 1e-9d ? (PlayMatDepth * 0.5d) / dy : double.PositiveInfinity;
            return Math.Min(toSide, toEnd);
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
