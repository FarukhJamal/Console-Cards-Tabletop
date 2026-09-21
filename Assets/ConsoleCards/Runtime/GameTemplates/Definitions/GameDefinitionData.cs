using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConsoleCards.GameTemplates.Definitions
{
    [Serializable]
    public sealed class GameContentSetData
    {
        private readonly ReadOnlyCollection<string> cardDefinitionIds;

        public GameContentSetData(
            string stableId,
            string displayName,
            IEnumerable<string> cardDefinitionIds)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Content Set ID is required.", nameof(stableId));
            StableId = stableId;
            DisplayName = displayName ?? string.Empty;
            this.cardDefinitionIds = new ReadOnlyCollection<string>(
                new List<string>(cardDefinitionIds ?? throw new ArgumentNullException(nameof(cardDefinitionIds))));
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> CardDefinitionIds => cardDefinitionIds;
    }

    [Serializable]
    public sealed class ControllerConfigurationData
    {
        public ControllerConfigurationData(
            int maximumHandSize,
            bool drawToMaximumAtTurnStart,
            bool unusedCardsCarryOver,
            bool costsAreAllOrNothing,
            bool sharedPaymentAllowed)
        {
            if (maximumHandSize < 0) throw new ArgumentOutOfRangeException(nameof(maximumHandSize));
            MaximumHandSize = maximumHandSize;
            DrawToMaximumAtTurnStart = drawToMaximumAtTurnStart;
            UnusedCardsCarryOver = unusedCardsCarryOver;
            CostsAreAllOrNothing = costsAreAllOrNothing;
            SharedPaymentAllowed = sharedPaymentAllowed;
        }

        public int MaximumHandSize { get; }
        public bool DrawToMaximumAtTurnStart { get; }
        public bool UnusedCardsCarryOver { get; }
        public bool CostsAreAllOrNothing { get; }
        public bool SharedPaymentAllowed { get; }
    }

    [Serializable]
    public sealed class GameDefinitionData
    {
        private readonly ReadOnlyCollection<CardDefinitionData> cards;
        private readonly ReadOnlyCollection<GameContentSetData> contentSets;
        private readonly ReadOnlyCollection<AvatarDefinitionData> avatars;
        private readonly ReadOnlyCollection<ModeDefinitionData> modes;
        private readonly ReadOnlyCollection<ControllerInput> inputVocabulary;

        public GameDefinitionData(
            string stableId,
            string displayName,
            string manualRules,
            int minimumPlayers,
            int maximumPlayers,
            GridDefinitionData grid,
            IEnumerable<CardDefinitionData> cards,
            IEnumerable<GameContentSetData> contentSets,
            IEnumerable<AvatarDefinitionData> avatars,
            IEnumerable<ModeDefinitionData> modes,
            ConsoleConfigurationData console,
            IEnumerable<ControllerInput> inputVocabulary,
            ControllerConfigurationData controllerConfiguration,
            ControllerMappingKind controllerMappingKind,
            string presentationReference,
            string assistanceConfiguration)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Game ID is required.", nameof(stableId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Game name is required.", nameof(displayName));
            if (minimumPlayers < 1) throw new ArgumentOutOfRangeException(nameof(minimumPlayers));
            if (maximumPlayers < minimumPlayers) throw new ArgumentOutOfRangeException(nameof(maximumPlayers));

            StableId = stableId;
            DisplayName = displayName;
            ManualRules = manualRules ?? string.Empty;
            MinimumPlayers = minimumPlayers;
            MaximumPlayers = maximumPlayers;
            Grid = grid;
            this.cards = new ReadOnlyCollection<CardDefinitionData>(new List<CardDefinitionData>(cards ?? throw new ArgumentNullException(nameof(cards))));
            this.contentSets = new ReadOnlyCollection<GameContentSetData>(new List<GameContentSetData>(contentSets ?? throw new ArgumentNullException(nameof(contentSets))));
            this.avatars = new ReadOnlyCollection<AvatarDefinitionData>(new List<AvatarDefinitionData>(avatars ?? throw new ArgumentNullException(nameof(avatars))));
            this.modes = new ReadOnlyCollection<ModeDefinitionData>(new List<ModeDefinitionData>(modes ?? throw new ArgumentNullException(nameof(modes))));
            Console = console;
            this.inputVocabulary = new ReadOnlyCollection<ControllerInput>(new List<ControllerInput>(inputVocabulary ?? throw new ArgumentNullException(nameof(inputVocabulary))));
            ControllerConfiguration = controllerConfiguration;
            ControllerMappingKind = controllerMappingKind;
            PresentationReference = presentationReference ?? string.Empty;
            AssistanceConfiguration = assistanceConfiguration ?? string.Empty;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string ManualRules { get; }
        public int MinimumPlayers { get; }
        public int MaximumPlayers { get; }
        public GridDefinitionData Grid { get; }
        public IReadOnlyList<CardDefinitionData> Cards => cards;
        public IReadOnlyList<GameContentSetData> ContentSets => contentSets;
        public IReadOnlyList<AvatarDefinitionData> Avatars => avatars;
        public IReadOnlyList<ModeDefinitionData> Modes => modes;
        public ConsoleConfigurationData Console { get; }
        public IReadOnlyList<ControllerInput> InputVocabulary => inputVocabulary;
        public ControllerConfigurationData ControllerConfiguration { get; }
        public ControllerMappingKind ControllerMappingKind { get; }
        public string PresentationReference { get; }
        public string AssistanceConfiguration { get; }

        public bool TryGetCard(string stableId, out CardDefinitionData definition)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (string.Equals(cards[i].StableId, stableId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = cards[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
