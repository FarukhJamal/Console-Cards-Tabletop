using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Approved Trap Floor setup/content surrounding one generic Game Template.
    /// It contains no turn, Floorfall, card-effect, economy, or movement rules.
    /// </summary>
    public sealed class TrapFloorTemplateDefinition
    {
        private readonly ReadOnlyDictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds;
        private readonly ReadOnlyDictionary<TabletopObjectId, TrapFloorCoordinate> floorCoordinates;
        private readonly ReadOnlyDictionary<TabletopObjectId, string> cardLabels;
        private readonly ReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory> floormasterCategoryDefinitions;
        private readonly ReadOnlyDictionary<TabletopObjectId, TrapFloorFloormasterCardCategory> floormasterCardCategories;
        private readonly ReadOnlyCollection<TabletopObjectId> floormasterCardIds;
        private readonly ReadOnlyCollection<TrapFloorPlayerSetupDefinition> players;
        private readonly ReadOnlyCollection<TabletopObjectId> coinTokenIds;

        internal TrapFloorTemplateDefinition(
            GameTemplate template,
            GameTemplateContentCatalog contentCatalog,
            PlayerLayoutDefinition playerLayout,
            PlayAreaId boardPlayAreaId,
            ContainerId floormasterDeckId,
            ContainerId floormasterDiscardId,
            TabletopPose floormasterRevealPose,
            ContainerId sharedCoinSupplyId,
            TabletopPose sharedCoinSupplyPose,
            IDictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds,
            IDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory> floormasterCategoryDefinitions,
            IEnumerable<TabletopObjectId> floormasterCardIds,
            IDictionary<TabletopObjectId, string> cardLabels,
            IEnumerable<TrapFloorPlayerSetupDefinition> players,
            IEnumerable<TabletopObjectId> coinTokenIds,
            TabletopObjectId floorfallXAxisDieId,
            TabletopObjectId floorfallYAxisDieId)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            ContentCatalog = contentCatalog ?? throw new ArgumentNullException(nameof(contentCatalog));
            PlayerLayout = playerLayout ?? throw new ArgumentNullException(nameof(playerLayout));
            BoardPlayAreaId = boardPlayAreaId;
            FloormasterDeckId = floormasterDeckId;
            FloormasterDiscardId = floormasterDiscardId;
            FloormasterRevealPose = floormasterRevealPose;
            SharedCoinSupplyId = sharedCoinSupplyId;
            SharedCoinSupplyPose = sharedCoinSupplyPose;
            FloorfallXAxisDieId = floorfallXAxisDieId;
            FloorfallYAxisDieId = floorfallYAxisDieId;
            this.floorCardIds = new ReadOnlyDictionary<TrapFloorCoordinate, TabletopObjectId>(
                new Dictionary<TrapFloorCoordinate, TabletopObjectId>(floorCardIds));
            this.floorCoordinates = new ReadOnlyDictionary<TabletopObjectId, TrapFloorCoordinate>(
                ReverseFloorCoordinates(floorCardIds));
            this.cardLabels = new ReadOnlyDictionary<TabletopObjectId, string>(
                new Dictionary<TabletopObjectId, string>(cardLabels));
            this.floormasterCategoryDefinitions = new ReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory>(
                new Dictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory>(floormasterCategoryDefinitions));
            this.floormasterCardIds = new ReadOnlyCollection<TabletopObjectId>(
                new List<TabletopObjectId>(floormasterCardIds));
            this.floormasterCardCategories = new ReadOnlyDictionary<TabletopObjectId, TrapFloorFloormasterCardCategory>(
                BuildFloormasterCardCategories(Template, this.floormasterCardIds, this.floormasterCategoryDefinitions));
            this.players = new ReadOnlyCollection<TrapFloorPlayerSetupDefinition>(
                new List<TrapFloorPlayerSetupDefinition>(players));
            this.coinTokenIds = new ReadOnlyCollection<TabletopObjectId>(
                new List<TabletopObjectId>(coinTokenIds));

            if (this.floorCardIds.Count != TrapFloorTemplateFactory.FloorCardCount)
            {
                throw new ArgumentException("Trap Floor requires exactly 36 coordinate-mapped Floor Cards.", nameof(floorCardIds));
            }

            if (this.coinTokenIds.Count != TrapFloorTemplateFactory.SharedCoinCount)
            {
                throw new ArgumentException("Trap Floor requires exactly 50 shared coin Tokens.", nameof(coinTokenIds));
            }

            ValidateFloormasterContent();

            if (FloorfallXAxisDieId.IsEmpty
                || FloorfallYAxisDieId.IsEmpty
                || FloorfallXAxisDieId == FloorfallYAxisDieId)
            {
                throw new ArgumentException("Trap Floor requires two distinct official Floorfall Die IDs.");
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
                    throw new ArgumentException("Trap Floor official Floorfall objects must be generic d6 Dice.");
                }

                foundXAxisDie |= instance.Id == FloorfallXAxisDieId;
                foundYAxisDie |= instance.Id == FloorfallYAxisDieId;
            }

            if (!foundXAxisDie || !foundYAxisDie)
            {
                throw new ArgumentException("Trap Floor Template is missing an associated Floorfall Die.");
            }
        }

        public GameTemplate Template { get; }

        public int MinimumPlayerCount => TrapFloorTemplateFactory.MinimumPlayerCount;

        public int MaximumPlayerCount => TrapFloorTemplateFactory.MaximumPlayerCount;

        public GameTemplateContentCatalog ContentCatalog { get; }

        public PlayerLayoutDefinition PlayerLayout { get; }

        public PlayAreaId BoardPlayAreaId { get; }

        public ContainerId FloormasterDeckId { get; }

        public ContainerId FloormasterDiscardId { get; }

        public TabletopPose FloormasterRevealPose { get; }

        public IReadOnlyList<TabletopObjectId> FloormasterCardIds => floormasterCardIds;

        public IReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory> FloormasterCategoryDefinitions =>
            floormasterCategoryDefinitions;

        public ContainerId SharedCoinSupplyId { get; }

        public TabletopPose SharedCoinSupplyPose { get; }

        public TabletopObjectId FloorfallXAxisDieId { get; }

        public TabletopObjectId FloorfallYAxisDieId { get; }

        public IReadOnlyDictionary<TrapFloorCoordinate, TabletopObjectId> FloorCardIds => floorCardIds;

        public IReadOnlyDictionary<TabletopObjectId, string> CardLabels => cardLabels;

        public IReadOnlyList<TrapFloorPlayerSetupDefinition> Players => players;

        public IReadOnlyList<TabletopObjectId> CoinTokenIds => coinTokenIds;

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

        public bool IsOfficialFloormasterCard(TabletopObjectId objectId)
        {
            return floormasterCardCategories.ContainsKey(objectId);
        }

        public bool TryGetFloormasterCardCategory(
            TabletopObjectId objectId,
            out TrapFloorFloormasterCardCategory category)
        {
            return floormasterCardCategories.TryGetValue(objectId, out category);
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
                if (reversed.ContainsKey(pair.Value))
                {
                    throw new ArgumentException("A Floor Card cannot occupy more than one Board coordinate.", nameof(coordinates));
                }

                reversed.Add(pair.Value, pair.Key);
            }

            return reversed;
        }

        private static Dictionary<TabletopObjectId, TrapFloorFloormasterCardCategory> BuildFloormasterCardCategories(
            GameTemplate template,
            IEnumerable<TabletopObjectId> officialCardIds,
            IReadOnlyDictionary<ObjectDefinitionId, TrapFloorFloormasterCardCategory> categoryDefinitions)
        {
            Dictionary<TabletopObjectId, ObjectDefinitionId> definitionsByObjectId =
                new Dictionary<TabletopObjectId, ObjectDefinitionId>();
            for (int i = 0; i < template.Objects.Count; i++)
            {
                GameTemplateObjectInstanceDefinition instance = template.Objects[i];
                definitionsByObjectId.Add(instance.Id, instance.DefinitionId);
            }

            Dictionary<TabletopObjectId, TrapFloorFloormasterCardCategory> categories =
                new Dictionary<TabletopObjectId, TrapFloorFloormasterCardCategory>();
            foreach (TabletopObjectId objectId in officialCardIds)
            {
                if (objectId.IsEmpty || categories.ContainsKey(objectId))
                {
                    throw new ArgumentException("Official Floormaster Card IDs must be non-empty and unique.", nameof(officialCardIds));
                }

                if (!definitionsByObjectId.TryGetValue(objectId, out ObjectDefinitionId definitionId)
                    || !categoryDefinitions.TryGetValue(definitionId, out TrapFloorFloormasterCardCategory category))
                {
                    throw new ArgumentException(
                        "Every official Floormaster Card must reference an explicit Trap Floor category definition.",
                        nameof(officialCardIds));
                }

                categories.Add(objectId, category);
            }

            return categories;
        }

        private void ValidateFloormasterContent()
        {
            if (FloormasterDeckId.IsEmpty
                || FloormasterDiscardId.IsEmpty
                || FloormasterDeckId == FloormasterDiscardId)
            {
                throw new ArgumentException("Trap Floor requires distinct official Floormaster Deck and discard IDs.");
            }

            if (floormasterCardIds.Count != TrapFloorTemplateFactory.FloormasterCardCount)
            {
                throw new ArgumentException("Trap Floor requires exactly 36 official Floormaster Cards.");
            }

            int trapCount = 0;
            int coinCount = 0;
            int itemCount = 0;
            foreach (TrapFloorFloormasterCardCategory category in floormasterCardCategories.Values)
            {
                switch (category)
                {
                    case TrapFloorFloormasterCardCategory.Trap:
                        trapCount++;
                        break;
                    case TrapFloorFloormasterCardCategory.Coin:
                        coinCount++;
                        break;
                    case TrapFloorFloormasterCardCategory.Item:
                        itemCount++;
                        break;
                    default:
                        throw new ArgumentException("Trap Floor Floormaster content contains an unsupported category.");
                }
            }

            if (trapCount != TrapFloorTemplateFactory.FloormasterTrapCardCount
                || coinCount != TrapFloorTemplateFactory.FloormasterCoinCardCount
                || itemCount != TrapFloorTemplateFactory.FloormasterItemCardCount)
            {
                throw new ArgumentException("Trap Floor Floormaster content must preserve the approved 14 Trap / 14 Coin / 8 Item composition.");
            }

            GameTemplateContainerMembership deckMembership = null;
            GameTemplateContainerMembership discardMembership = null;
            for (int i = 0; i < Template.Memberships.Count; i++)
            {
                GameTemplateContainerMembership membership = Template.Memberships[i];
                if (membership.ContainerId == FloormasterDeckId)
                {
                    deckMembership = membership;
                }
                else if (membership.ContainerId == FloormasterDiscardId)
                {
                    discardMembership = membership;
                }
            }

            if (deckMembership == null
                || discardMembership == null
                || discardMembership.OrderedObjectIds.Count != 0
                || deckMembership.OrderedObjectIds.Count != floormasterCardIds.Count)
            {
                throw new ArgumentException("Trap Floor Floormaster starting membership must use the official Deck with an empty official discard.");
            }

            for (int i = 0; i < floormasterCardIds.Count; i++)
            {
                if (deckMembership.OrderedObjectIds[i] != floormasterCardIds[i])
                {
                    throw new ArgumentException("Trap Floor Floormaster starting order must match the Template-authored official Card order.");
                }
            }
        }
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
            ContainerId coinStorageContainerId,
            TabletopPose coinStoragePose,
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
            CoinStorageContainerId = coinStorageContainerId;
            CoinStoragePose = coinStoragePose;
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
        public ContainerId CoinStorageContainerId { get; }
        public TabletopPose CoinStoragePose { get; }
        public TabletopObjectId AvatarCardId { get; }
        public TabletopObjectId RuleCardId { get; }
        public TabletopObjectId ModeCardId { get; }
        public TabletopObjectId PawnId { get; }
        public TrapFloorCoordinate StartingCorner { get; }
    }
}
