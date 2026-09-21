using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Trap Floor setup/content surrounding one generic Game Template.
    /// It contains no Search, reveal, card-effect, economy, or movement rules.
    /// </summary>
    public sealed class TrapFloorTemplateDefinition
    {
        private readonly ReadOnlyDictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds;
        private readonly ReadOnlyDictionary<TabletopObjectId, TrapFloorCoordinate> floorCoordinates;
        private readonly ReadOnlyDictionary<TabletopObjectId, string> cardLabels;
        private readonly ReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloorContentDefinition> floorContentDefinitions;
        private readonly int floorContentInstanceCount;
        private readonly ReadOnlyCollection<TrapFloorPlayerSetupDefinition> players;
        private readonly ReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory> emptyFloormasterCategories;
        private readonly ReadOnlyCollection<TabletopObjectId> emptyObjectIds;

        internal TrapFloorTemplateDefinition(
            GameDefinitionData gameDefinition,
            ModeDefinitionData activeMode,
            GameTemplate template,
            GameTemplateContentCatalog contentCatalog,
            PlayerLayoutDefinition playerLayout,
            PlayAreaId boardPlayAreaId,
            IDictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds,
            IEnumerable<TrapFloorFloorContentDefinition> floorContentDefinitions,
            IDictionary<TabletopObjectId, string> cardLabels,
            IEnumerable<TrapFloorPlayerSetupDefinition> players,
            TabletopObjectId floorfallXAxisDieId,
            TabletopObjectId floorfallYAxisDieId)
        {
            GameDefinition = gameDefinition ?? throw new ArgumentNullException(nameof(gameDefinition));
            ActiveMode = activeMode ?? throw new ArgumentNullException(nameof(activeMode));
            Template = template ?? throw new ArgumentNullException(nameof(template));
            ContentCatalog = contentCatalog ?? throw new ArgumentNullException(nameof(contentCatalog));
            PlayerLayout = playerLayout ?? throw new ArgumentNullException(nameof(playerLayout));
            BoardPlayAreaId = boardPlayAreaId;
            FloorfallXAxisDieId = floorfallXAxisDieId;
            FloorfallYAxisDieId = floorfallYAxisDieId;
            this.floorCardIds = new ReadOnlyDictionary<TrapFloorCoordinate, TabletopObjectId>(
                new Dictionary<TrapFloorCoordinate, TabletopObjectId>(
                    floorCardIds ?? throw new ArgumentNullException(nameof(floorCardIds))));
            this.floorCoordinates = new ReadOnlyDictionary<TabletopObjectId, TrapFloorCoordinate>(
                ReverseFloorCoordinates(this.floorCardIds));
            this.cardLabels = new ReadOnlyDictionary<TabletopObjectId, string>(
                new Dictionary<TabletopObjectId, string>(
                    cardLabels ?? throw new ArgumentNullException(nameof(cardLabels))));
            List<TrapFloorFloorContentDefinition> floorContentInstances = new List<TrapFloorFloorContentDefinition>(
                floorContentDefinitions ?? throw new ArgumentNullException(nameof(floorContentDefinitions)));
            floorContentInstanceCount = floorContentInstances.Count;
            this.floorContentDefinitions = new ReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloorContentDefinition>(
                IndexFloorContentDefinitions(floorContentInstances));
            Stage03Configuration = new TrapFloorStage03Configuration(ActiveMode.RequiredKeyCount);
            this.players = new ReadOnlyCollection<TrapFloorPlayerSetupDefinition>(
                new List<TrapFloorPlayerSetupDefinition>(
                    players ?? throw new ArgumentNullException(nameof(players))));
            emptyFloormasterCategories =
                new ReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory>(
                    new Dictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory>());
            emptyObjectIds = new ReadOnlyCollection<TabletopObjectId>(new List<TabletopObjectId>());

            ValidateFloorContentAssignments();
            ValidateOfficialDice();
        }

        public GameTemplate Template { get; }

        public GameDefinitionData GameDefinition { get; }

        public ModeDefinitionData ActiveMode { get; }

        public GridDefinitionData Grid => GameDefinition.Grid;

        public int GridRows => Grid.Rows;

        public int GridColumns => Grid.Columns;

        public int MinimumPlayerCount => GameDefinition.MinimumPlayers;

        public int MaximumPlayerCount => GameDefinition.MaximumPlayers;

        public GameTemplateContentCatalog ContentCatalog { get; }

        public PlayerLayoutDefinition PlayerLayout { get; }

        public PlayAreaId BoardPlayAreaId { get; }

        public IReadOnlyDictionary<TrapFloorCoordinate, TabletopObjectId> FloorCardIds => floorCardIds;

        public IReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloorContentDefinition> FloorContentDefinitions =>
            floorContentDefinitions;

        public TrapFloorStage03Configuration Stage03Configuration { get; }

        public IReadOnlyDictionary<TabletopObjectId, string> CardLabels => cardLabels;

        public IReadOnlyList<TrapFloorPlayerSetupDefinition> Players => players;

        public TabletopObjectId FloorfallXAxisDieId { get; }

        public TabletopObjectId FloorfallYAxisDieId { get; }

        // Compatibility-only empty accessors keep the retired lifecycle types buildable. The current
        // Trap Floor Template does not construct or register any of these Containers or Objects.
        public ContainerId FloormasterDeckId => ContainerId.Empty;

        public ContainerId FloormasterDiscardId => ContainerId.Empty;

        public TabletopPose FloormasterRevealPose => TabletopPose.Default;

        public IReadOnlyList<TabletopObjectId> FloormasterCardIds => emptyObjectIds;

        public IReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory> FloormasterCategoryDefinitions =>
            emptyFloormasterCategories;

        public ContainerId SharedCoinSupplyId => ContainerId.Empty;

        public TabletopPose SharedCoinSupplyPose => TabletopPose.Default;

        public IReadOnlyList<TabletopObjectId> CoinTokenIds => emptyObjectIds;

        public bool TryGetFloorCardId(TrapFloorCoordinate coordinate, out TabletopObjectId objectId)
        {
            if (!IsCoordinateInGrid(coordinate))
            {
                objectId = TabletopObjectId.Empty;
                return false;
            }

            return floorCardIds.TryGetValue(coordinate, out objectId);
        }

        public bool IsCoordinateInGrid(TrapFloorCoordinate coordinate)
        {
            return coordinate.X <= GridColumns && coordinate.Y <= GridRows;
        }

        public bool TryGetFloorCoordinate(TabletopObjectId objectId, out TrapFloorCoordinate coordinate)
        {
            return floorCoordinates.TryGetValue(objectId, out coordinate);
        }

        public bool IsFloorCard(TabletopObjectId objectId)
        {
            return floorCoordinates.ContainsKey(objectId);
        }

        public bool TryGetFloorCardState(
            MatchState matchState,
            TabletopObjectId objectId,
            out TrapFloorFloorCardState floorCardState)
        {
            if (matchState == null
                || matchState.GameTemplateId != Template.Id
                || !floorCoordinates.TryGetValue(objectId, out TrapFloorCoordinate coordinate)
                || !matchState.Cards.TryGetValue(objectId, out CardInstanceState card)
                || !floorContentDefinitions.TryGetValue(
                    card.BaseState.DefinitionId,
                    out TrapFloorFloorContentDefinition content))
            {
                floorCardState = null;
                return false;
            }

            floorCardState = new TrapFloorFloorCardState(
                objectId,
                coordinate,
                content,
                card.Face == CardFace.FaceUp);
            return true;
        }

        public bool IsOfficialFloormasterCard(TabletopObjectId objectId)
        {
            return false;
        }

        public bool TryGetFloormasterCardCategory(
            TabletopObjectId objectId,
            out TrapFloorFloormasterCardCategory category)
        {
            category = default;
            return false;
        }

        public GameTemplateMatchBuildResult TryCreateMatch(
            IReadOnlyList<PlayerId> activePlayerIds,
            MatchId matchId)
        {
            return new GameTemplateMatchFactory().TryCreate(
                Template,
                ContentCatalog,
                activePlayerIds,
                matchId);
        }

        private static Dictionary<TabletopObjectId, TrapFloorCoordinate> ReverseFloorCoordinates(
            IEnumerable<KeyValuePair<TrapFloorCoordinate, TabletopObjectId>> coordinates)
        {
            Dictionary<TabletopObjectId, TrapFloorCoordinate> reversed =
                new Dictionary<TabletopObjectId, TrapFloorCoordinate>();
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in coordinates)
            {
                if (pair.Value.IsEmpty || reversed.ContainsKey(pair.Value))
                {
                    throw new ArgumentException(
                        "Floor Card IDs must be non-empty and cannot occupy more than one Board coordinate.",
                        nameof(coordinates));
                }

                reversed.Add(pair.Value, pair.Key);
            }

            return reversed;
        }

        private static Dictionary<ObjectDefinitionId, TrapFloorFloorContentDefinition> IndexFloorContentDefinitions(
            IEnumerable<TrapFloorFloorContentDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            Dictionary<ObjectDefinitionId, TrapFloorFloorContentDefinition> indexed =
                new Dictionary<ObjectDefinitionId, TrapFloorFloorContentDefinition>();
            foreach (TrapFloorFloorContentDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Floor content definitions must be non-null.",
                        nameof(definitions));
                }

                if (!indexed.ContainsKey(definition.Id)) indexed.Add(definition.Id, definition);
            }

            return indexed;
        }

        private void ValidateFloorContentAssignments()
        {
            if (floorCardIds.Count != Grid.CellCount)
            {
                throw new ArgumentException(
                    $"Trap Floor Grid requires {Grid.CellCount} coordinate-mapped Floor Cards, but found {floorCardIds.Count}.");
            }

            int configuredKeyCount = 0;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in floorCardIds)
            {
                GameTemplateObjectInstanceDefinition instance = null;
                for (int i = 0; i < Template.Objects.Count; i++)
                {
                    if (Template.Objects[i].Id == pair.Value)
                    {
                        instance = Template.Objects[i];
                        break;
                    }
                }

                if (instance != null
                    && floorContentDefinitions.TryGetValue(instance.DefinitionId, out TrapFloorFloorContentDefinition definition)
                    && definition.Category == TrapFloorFloorContentCategory.Key)
                    configuredKeyCount++;
            }

            if (floorContentInstanceCount != Grid.CellCount)
            {
                throw new ArgumentException(
                    $"Authored Trap Floor content produces {floorContentInstanceCount} instances for a {Grid.CellCount}-cell Grid.");
            }

            if (ActiveMode.RequiredKeyCount > configuredKeyCount)
            {
                throw new ArgumentException(
                    "Trap Floor active Mode requires more Keys than the authored Floor content produces.");
            }

            Dictionary<TabletopObjectId, GameTemplateObjectInstanceDefinition> templateObjects =
                new Dictionary<TabletopObjectId, GameTemplateObjectInstanceDefinition>();
            for (int i = 0; i < Template.Objects.Count; i++)
            {
                GameTemplateObjectInstanceDefinition instance = Template.Objects[i];
                if (!templateObjects.ContainsKey(instance.Id))
                {
                    templateObjects.Add(instance.Id, instance);
                }
            }

            int acceptedAssignments = 0;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in floorCardIds)
            {
                if (!IsCoordinateInGrid(pair.Key)
                    || !templateObjects.TryGetValue(pair.Value, out GameTemplateObjectInstanceDefinition instance)
                    || instance.Kind != TabletopObjectKind.Card
                    || instance.InitialCardFace != CardFace.FaceDown
                    || !floorContentDefinitions.ContainsKey(instance.DefinitionId))
                {
                    throw new ArgumentException(
                        "Every authored Grid coordinate requires one unrevealed Card from the configured Floor content set.");
                }

                acceptedAssignments++;

                if (!cardLabels.ContainsKey(pair.Value))
                {
                    throw new ArgumentException("Every Floor Card requires a content display label.");
                }
            }

            if (acceptedAssignments != Grid.CellCount)
            {
                throw new ArgumentException("Every authored Grid cell must receive exactly one Floor content instance.");
            }
        }

        private void ValidateOfficialDice()
        {
            if (FloorfallXAxisDieId.IsEmpty
                || FloorfallYAxisDieId.IsEmpty
                || FloorfallXAxisDieId == FloorfallYAxisDieId)
            {
                throw new ArgumentException("Trap Floor requires two distinct official d6 IDs.");
            }

            bool foundXAxisDie = false;
            bool foundYAxisDie = false;
            for (int i = 0; i < Template.Objects.Count; i++)
            {
                GameTemplateObjectInstanceDefinition instance = Template.Objects[i];
                if (instance.Id != FloorfallXAxisDieId && instance.Id != FloorfallYAxisDieId)
                {
                    continue;
                }

                if (instance.Kind != TabletopObjectKind.Die
                    || instance.DieSideCount != TrapFloorFloorfallService.DieSideCount)
                {
                    throw new ArgumentException("Trap Floor official setup objects must be generic d6 Dice.");
                }

                foundXAxisDie |= instance.Id == FloorfallXAxisDieId;
                foundYAxisDie |= instance.Id == FloorfallYAxisDieId;
            }

            if (!foundXAxisDie || !foundYAxisDie)
            {
                throw new ArgumentException("Trap Floor Template is missing an associated official d6.");
            }
        }
    }

    public sealed class TrapFloorFloorCardState
    {
        internal TrapFloorFloorCardState(
            TabletopObjectId objectId,
            TrapFloorCoordinate coordinate,
            TrapFloorFloorContentDefinition content,
            bool isRevealed)
        {
            ObjectId = objectId;
            Coordinate = coordinate;
            Content = content ?? throw new ArgumentNullException(nameof(content));
            IsRevealed = isRevealed;
        }

        public TabletopObjectId ObjectId { get; }

        public TrapFloorCoordinate Coordinate { get; }

        public TrapFloorFloorContentDefinition Content { get; }

        public bool IsRevealed { get; }
    }

    public sealed class TrapFloorPlayerSetupDefinition
    {
        internal TrapFloorPlayerSetupDefinition(
            int layoutSeatIndex,
            SeatId seatId,
            ContainerId handContainerId,
            ContainerId mainSlotContainerId,
            IEnumerable<ContainerId> sideSlotContainerIds,
            ContainerId controllerDeckId,
            TabletopObjectId avatarCardId,
            TabletopObjectId pawnId,
            TrapFloorCoordinate startingCorner)
        {
            LayoutSeatIndex = layoutSeatIndex;
            SeatId = seatId;
            HandContainerId = handContainerId;
            MainSlotContainerId = mainSlotContainerId;
            SideSlotContainerIds = new ReadOnlyCollection<ContainerId>(
                new List<ContainerId>(sideSlotContainerIds));
            ControllerDeckId = controllerDeckId;
            AvatarCardId = avatarCardId;
            PawnId = pawnId;
            StartingCorner = startingCorner;
        }

        public int LayoutSeatIndex { get; }
        public SeatId SeatId { get; }
        public ContainerId HandContainerId { get; }
        public ContainerId MainSlotContainerId { get; }
        public IReadOnlyList<ContainerId> SideSlotContainerIds { get; }
        public ContainerId RuleSlotContainerId => ContainerId.Empty;
        public ContainerId ModeSlotContainerId => ContainerId.Empty;
        public IReadOnlyList<ContainerId> ItemSlotContainerIds => SideSlotContainerIds;
        public ContainerId ControllerDeckId { get; }
        public ContainerId CoinStorageContainerId => ContainerId.Empty;
        public TabletopPose CoinStoragePose => TabletopPose.Default;
        public TabletopObjectId AvatarCardId { get; }
        public TabletopObjectId RuleCardId => TabletopObjectId.Empty;
        public TabletopObjectId ModeCardId => TabletopObjectId.Empty;
        public TabletopObjectId PawnId { get; }
        public TrapFloorCoordinate StartingCorner { get; }
    }
}
