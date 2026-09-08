using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;

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
        private readonly ReadOnlyCollection<TrapFloorPlayerSetupDefinition> players;
        private readonly ReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory> emptyFloormasterCategories;
        private readonly ReadOnlyCollection<TabletopObjectId> emptyObjectIds;

        internal TrapFloorTemplateDefinition(
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
            this.floorContentDefinitions = new ReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloorContentDefinition>(
                IndexFloorContentDefinitions(floorContentDefinitions));
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

        public int MinimumPlayerCount => TrapFloorTemplateFactory.MinimumPlayerCount;

        public int MaximumPlayerCount => TrapFloorTemplateFactory.MaximumPlayerCount;

        public GameTemplateContentCatalog ContentCatalog { get; }

        public PlayerLayoutDefinition PlayerLayout { get; }

        public PlayAreaId BoardPlayAreaId { get; }

        public IReadOnlyDictionary<TrapFloorCoordinate, TabletopObjectId> FloorCardIds => floorCardIds;

        public IReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloorContentDefinition> FloorContentDefinitions =>
            floorContentDefinitions;

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
            return floorCardIds.TryGetValue(coordinate, out objectId);
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
                if (definition == null || indexed.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        "Floor content definitions must be non-null and have unique stable IDs.",
                        nameof(definitions));
                }

                indexed.Add(definition.Id, definition);
            }

            return indexed;
        }

        private void ValidateFloorContentAssignments()
        {
            if (floorCardIds.Count != TrapFloorTemplateFactory.FloorCardCount)
            {
                throw new ArgumentException("Trap Floor requires exactly 36 coordinate-mapped Floor Cards.");
            }

            if (floorContentDefinitions.Count != TrapFloorStage03ContentPool.TotalCount)
            {
                throw new ArgumentException("Trap Floor requires the complete 36-entry Stage-03 content pool.");
            }

            Dictionary<TrapFloorFloorContentCategory, int> categoryCounts =
                new Dictionary<TrapFloorFloorContentCategory, int>();
            foreach (TrapFloorFloorContentDefinition definition in floorContentDefinitions.Values)
            {
                categoryCounts.TryGetValue(definition.Category, out int count);
                categoryCounts[definition.Category] = count + 1;
            }

            RequireCategoryCount(categoryCounts, TrapFloorFloorContentCategory.Trap, TrapFloorStage03ContentPool.TrapCount);
            RequireCategoryCount(categoryCounts, TrapFloorFloorContentCategory.Friend, TrapFloorStage03ContentPool.FriendCount);
            RequireCategoryCount(categoryCounts, TrapFloorFloorContentCategory.Key, TrapFloorStage03ContentPool.KeyCount);
            RequireCategoryCount(
                categoryCounts,
                TrapFloorFloorContentCategory.SecretExit,
                TrapFloorStage03ContentPool.SecretExitCount);
            RequireCategoryCount(categoryCounts, TrapFloorFloorContentCategory.Entry, TrapFloorStage03ContentPool.EntryCount);
            RequireCategoryCount(categoryCounts, TrapFloorFloorContentCategory.Ability, TrapFloorStage03ContentPool.AbilityCount);

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

            HashSet<ObjectDefinitionId> acceptedAssignments = new HashSet<ObjectDefinitionId>();
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in floorCardIds)
            {
                if (!templateObjects.TryGetValue(pair.Value, out GameTemplateObjectInstanceDefinition instance)
                    || instance.Kind != TabletopObjectKind.Card
                    || instance.InitialCardFace != CardFace.FaceDown
                    || !floorContentDefinitions.ContainsKey(instance.DefinitionId)
                    || !acceptedAssignments.Add(instance.DefinitionId))
                {
                    throw new ArgumentException(
                        "Every Floor coordinate requires one unrevealed Card with one unique Stage-03 content assignment.");
                }

                if (!cardLabels.ContainsKey(pair.Value))
                {
                    throw new ArgumentException("Every Floor Card requires a content display label.");
                }
            }

            if (acceptedAssignments.Count != floorContentDefinitions.Count)
            {
                throw new ArgumentException("Every Stage-03 content definition must be assigned exactly once.");
            }
        }

        private static void RequireCategoryCount(
            IReadOnlyDictionary<TrapFloorFloorContentCategory, int> counts,
            TrapFloorFloorContentCategory category,
            int expected)
        {
            counts.TryGetValue(category, out int actual);
            if (actual != expected)
            {
                throw new ArgumentException(
                    $"Stage-03 {category} content count must be {expected}, but was {actual}.");
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
            ContainerId ruleSlotContainerId,
            ContainerId modeSlotContainerId,
            IEnumerable<ContainerId> itemSlotContainerIds,
            ContainerId controllerDeckId,
            TabletopObjectId avatarCardId,
            TabletopObjectId ruleCardId,
            TabletopObjectId modeCardId,
            TabletopObjectId pawnId,
            TrapFloorCoordinate startingCorner)
        {
            LayoutSeatIndex = layoutSeatIndex;
            SeatId = seatId;
            HandContainerId = handContainerId;
            MainSlotContainerId = mainSlotContainerId;
            RuleSlotContainerId = ruleSlotContainerId;
            ModeSlotContainerId = modeSlotContainerId;
            ItemSlotContainerIds = new ReadOnlyCollection<ContainerId>(
                new List<ContainerId>(itemSlotContainerIds));
            ControllerDeckId = controllerDeckId;
            AvatarCardId = avatarCardId;
            RuleCardId = ruleCardId;
            ModeCardId = modeCardId;
            PawnId = pawnId;
            StartingCorner = startingCorner;
        }

        public int LayoutSeatIndex { get; }
        public SeatId SeatId { get; }
        public ContainerId HandContainerId { get; }
        public ContainerId MainSlotContainerId { get; }
        public ContainerId RuleSlotContainerId { get; }
        public ContainerId ModeSlotContainerId { get; }
        public IReadOnlyList<ContainerId> ItemSlotContainerIds { get; }
        public ContainerId ControllerDeckId { get; }
        public ContainerId CoinStorageContainerId => ContainerId.Empty;
        public TabletopPose CoinStoragePose => TabletopPose.Default;
        public TabletopObjectId AvatarCardId { get; }
        public TabletopObjectId RuleCardId { get; }
        public TabletopObjectId ModeCardId { get; }
        public TabletopObjectId PawnId { get; }
        public TrapFloorCoordinate StartingCorner { get; }
    }
}
