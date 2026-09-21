using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [Serializable]
    public sealed class GameContentSet
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private List<CardDefinition> cards = new List<CardDefinition>();

        public string StableId => stableId;
        public string DisplayName => displayName;
        public IReadOnlyList<CardDefinition> Cards => cards;

        internal GameContentSetData ToData()
        {
            List<string> cardIds = new List<string>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) throw new InvalidOperationException("Content Sets cannot contain null Cards.");
                cardIds.Add(cards[i].StableId);
            }

            return new GameContentSetData(stableId, displayName, cardIds);
        }
    }

    [Serializable]
    public sealed class ControllerConfiguration
    {
        [SerializeField, Min(0)] private int maximumHandSize;
        [SerializeField] private bool drawToMaximumAtTurnStart;
        [SerializeField] private bool unusedCardsCarryOver;
        [SerializeField] private bool costsAreAllOrNothing;
        [SerializeField] private bool sharedPaymentAllowed;

        internal ControllerConfigurationData ToData()
        {
            return new ControllerConfigurationData(
                maximumHandSize,
                drawToMaximumAtTurnStart,
                unusedCardsCarryOver,
                costsAreAllOrNothing,
                sharedPaymentAllowed);
        }
    }

    [CreateAssetMenu(fileName = "GameDefinition", menuName = "Console Cards/Definitions/Game")]
    public sealed class GameDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string manualRules;
        [SerializeField, Min(1)] private int minimumPlayers = 1;
        [SerializeField, Min(1)] private int maximumPlayers = 1;
        [SerializeField] private GridDefinition playAreaDefinition;
        [SerializeField] private List<GameContentSet> contentSets = new List<GameContentSet>();
        [SerializeField] private List<ModeDefinition> modes = new List<ModeDefinition>();
        [SerializeField] private string defaultModeStableId;
        [SerializeField] private List<AvatarDefinition> avatars = new List<AvatarDefinition>();
        [SerializeField] private ConsoleConfiguration consoleConfiguration;
        [SerializeField] private List<ControllerInput> inputVocabulary = new List<ControllerInput>();
        [SerializeField] private ControllerConfiguration controllerConfiguration = new ControllerConfiguration();
        [SerializeField] private ControllerMappingKind controllerMappingKind;
        [SerializeField] private string presentationReference;
        [SerializeField, TextArea] private string assistanceConfiguration;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public int MinimumPlayers => minimumPlayers;
        public int MaximumPlayers => maximumPlayers;
        public GridDefinition PlayAreaDefinition => playAreaDefinition;
        public IReadOnlyList<ModeDefinition> Modes => modes;
        public IReadOnlyList<AvatarDefinition> Avatars => avatars;
        public ConsoleConfiguration ConsoleConfiguration => consoleConfiguration;

        public bool TryGetCard(ObjectDefinitionId id, out CardDefinition definition)
        {
            for (int setIndex = 0; setIndex < contentSets.Count; setIndex++)
            {
                GameContentSet contentSet = contentSets[setIndex];
                if (contentSet == null) continue;
                for (int cardIndex = 0; cardIndex < contentSet.Cards.Count; cardIndex++)
                {
                    CardDefinition candidate = contentSet.Cards[cardIndex];
                    if (candidate != null
                        && candidate.TryGetObjectDefinitionId(out ObjectDefinitionId candidateId)
                        && candidateId == id)
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public GameDefinitionData ToData()
        {
            if (playAreaDefinition == null) throw new InvalidOperationException("Game Definition requires a Play Area/Grid Definition.");
            if (consoleConfiguration == null) throw new InvalidOperationException("Game Definition requires a Console Configuration.");

            List<CardDefinitionData> cardData = new List<CardDefinitionData>();
            HashSet<string> addedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<GameContentSetData> contentSetData = new List<GameContentSetData>(contentSets.Count);
            for (int setIndex = 0; setIndex < contentSets.Count; setIndex++)
            {
                GameContentSet contentSet = contentSets[setIndex];
                if (contentSet == null) throw new InvalidOperationException("Game content sets cannot contain null entries.");
                contentSetData.Add(contentSet.ToData());
                for (int cardIndex = 0; cardIndex < contentSet.Cards.Count; cardIndex++)
                {
                    CardDefinition card = contentSet.Cards[cardIndex];
                    if (card == null) throw new InvalidOperationException("Game content sets cannot contain null Cards.");
                    if (addedCardIds.Add(card.StableId))
                    {
                        cardData.Add(card.ToData());
                    }
                }
            }

            List<AvatarDefinitionData> avatarData = new List<AvatarDefinitionData>(avatars.Count);
            for (int i = 0; i < avatars.Count; i++)
            {
                if (avatars[i] == null) throw new InvalidOperationException("Game Avatars cannot contain null entries.");
                avatarData.Add(avatars[i].ToData());
            }

            List<ModeDefinitionData> modeData = new List<ModeDefinitionData>(modes.Count);
            for (int i = 0; i < modes.Count; i++)
            {
                if (modes[i] == null) throw new InvalidOperationException("Game Modes cannot contain null entries.");
                modeData.Add(modes[i].ToData());
            }

            return new GameDefinitionData(
                stableId,
                displayName,
                manualRules,
                minimumPlayers,
                maximumPlayers,
                playAreaDefinition.ToData(),
                cardData,
                contentSetData,
                avatarData,
                modeData,
                defaultModeStableId,
                consoleConfiguration.ToData(),
                inputVocabulary,
                controllerConfiguration.ToData(),
                controllerMappingKind,
                presentationReference,
                assistanceConfiguration);
        }
    }
}
