using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly ReadOnlyCollection<TrapFloorPlayerSetupDefinition> players;
        private readonly ReadOnlyCollection<TabletopObjectId> coinTokenIds;

        internal TrapFloorTemplateDefinition(
            GameTemplate template,
            GameTemplateContentCatalog contentCatalog,
            PlayerLayoutDefinition playerLayout,
            PlayAreaId boardPlayAreaId,
            ContainerId floormasterDeckId,
            ContainerId floormasterDiscardId,
            ContainerId sharedCoinSupplyId,
            IDictionary<TrapFloorCoordinate, TabletopObjectId> floorCardIds,
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
            SharedCoinSupplyId = sharedCoinSupplyId;
            FloorfallXAxisDieId = floorfallXAxisDieId;
            FloorfallYAxisDieId = floorfallYAxisDieId;
            this.floorCardIds = new ReadOnlyDictionary<TrapFloorCoordinate, TabletopObjectId>(
                new Dictionary<TrapFloorCoordinate, TabletopObjectId>(floorCardIds));
            this.floorCoordinates = new ReadOnlyDictionary<TabletopObjectId, TrapFloorCoordinate>(
                ReverseFloorCoordinates(floorCardIds));
            this.cardLabels = new ReadOnlyDictionary<TabletopObjectId, string>(
                new Dictionary<TabletopObjectId, string>(cardLabels));
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

        public ContainerId SharedCoinSupplyId { get; }

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
        public TabletopObjectId AvatarCardId { get; }
        public TabletopObjectId RuleCardId { get; }
        public TabletopObjectId ModeCardId { get; }
        public TabletopObjectId PawnId { get; }
        public TrapFloorCoordinate StartingCorner { get; }
    }
}
