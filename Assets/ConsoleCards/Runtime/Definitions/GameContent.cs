using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConsoleCards.Definitions
{
    /// <summary>A deck as players see it: a preset from the game box (read-only) or one of their own.</summary>
    public sealed class GameDeckInfo
    {
        public GameDeckInfo(string id, string name, bool isPreset, int cardCount, int drawCount, IReadOnlyList<string> cardIds)
        {
            Id = id ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(name) ? Id : name;
            IsPreset = isPreset;
            CardCount = cardCount;
            DrawCount = drawCount;
            CardIds = cardIds ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>True for the game's own card groups (built-in content sets); they cannot be edited.</summary>
        public bool IsPreset { get; }

        /// <summary>Cards in the deck, each counted by its quantity (or its count in a player deck).</summary>
        public int CardCount { get; }

        /// <summary>How many a setup draws from a preset (0: all of them); always 0 for a player deck.</summary>
        public int DrawCount { get; }

        public IReadOnlyList<string> CardIds { get; }
    }

    /// <summary>
    /// A game's cards as they are played on this computer (doc 27 List 2, L2-1): the game's built-in cards with the
    /// player's changes applied, plus the player's new cards. A new card joins every card group its model card is
    /// in, so it can turn up in play. Built once per table start; the built-in assets are never changed.
    /// </summary>
    public sealed class GameContent
    {
        private readonly GameDefinition definition;
        private readonly Dictionary<string, CardDefinition> changedCards =
            new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CardDefinition> newCards = new List<CardDefinition>();
        private readonly Dictionary<string, string> newCardModel =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CardDefinition> allCards = new List<CardDefinition>();
        private readonly List<GameDeckInfo> decks = new List<GameDeckInfo>();
        private readonly List<string> warnings = new List<string>();

        /// <param name="playDeckId">
        /// One of the player's decks to play with (doc 27 L2-5): each card's copies in play become its copies in that
        /// deck, and a card not in the deck is not used. Empty: the game's own deck.
        /// </param>
        public GameContent(GameDefinition definition, PlayerCardsFile cards, PlayerDecksFile decks, string playDeckId = null)
        {
            PlayDeckId = playDeckId ?? string.Empty;
            this.definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            PlayerCards = (cards ?? new PlayerCardsFile()).Normalised();
            PlayerDecks = (decks ?? new PlayerDecksFile()).Normalised();
            Build();
        }

        public GameDefinition Definition => definition;
        public PlayerCardsFile PlayerCards { get; }

        /// <summary>The player's deck this game plays with; empty for the game's own deck.</summary>
        public string PlayDeckId { get; }

        /// <summary>The name of the deck played with, or empty for the game's own deck (or a deck that was not found).</summary>
        public string PlayDeckName { get; private set; } = string.Empty;
        public PlayerDecksFile PlayerDecks { get; }

        /// <summary>Every card of the game as played here: built-in (changed or not), then the player's new cards.</summary>
        public IReadOnlyList<CardDefinition> Cards => allCards;

        /// <summary>The player's new cards only.</summary>
        public IReadOnlyList<CardDefinition> NewCards => newCards;

        /// <summary>Preset decks (the game's card groups), then the player's decks.</summary>
        public IReadOnlyList<GameDeckInfo> Decks => decks;

        /// <summary>What could not be used (an unknown model card, an unreadable cost, a clashing ID…).</summary>
        public IReadOnlyList<string> Warnings => warnings;

        /// <summary>How many of the game's own cards the player changed.</summary>
        public int ChangedCount => changedCards.Count;

        public bool IsChanged(string cardId) => changedCards.ContainsKey(cardId ?? string.Empty);

        public bool IsNewCard(string cardId) => newCardModel.ContainsKey(cardId ?? string.Empty);

        /// <summary>The card as played here: a changed copy, a new card, or the built-in asset.</summary>
        public bool TryGetCard(ObjectDefinitionId id, out CardDefinition card)
        {
            for (int i = 0; i < allCards.Count; i++)
            {
                if (allCards[i].TryGetObjectDefinitionId(out ObjectDefinitionId candidate) && candidate == id)
                {
                    card = allCards[i];
                    return true;
                }
            }

            card = null;
            return false;
        }

        /// <summary>
        /// The game data with the player's cards applied, then the rule set's settings on top: a house-rule cost set on
        /// the rules card wins over a cost changed in the card editor.
        /// </summary>
        public GameDefinitionData ToData(RuleSetDefinition ruleSet) => definition.ToData(ruleSet, this);

        internal CardDefinition Resolve(CardDefinition builtIn) =>
            builtIn != null && changedCards.TryGetValue(builtIn.StableId ?? string.Empty, out CardDefinition changed)
                ? changed
                : builtIn;

        /// <summary>The player's new cards that join a card group: those whose model card is in it.</summary>
        internal void AddNewCardIdsFor(GameContentSet set, List<string> cardIds)
        {
            for (int i = 0; i < newCards.Count; i++)
            {
                string model = newCardModel[newCards[i].StableId];
                if (Contains(set, model)) cardIds.Add(newCards[i].StableId);
            }
        }

        /// <summary>Destroys the cards made at runtime (call when the table unloads).</summary>
        public void Destroy()
        {
            foreach (CardDefinition card in changedCards.Values) DestroyRuntime(card);
            for (int i = 0; i < newCards.Count; i++) DestroyRuntime(newCards[i]);
            changedCards.Clear();
            newCards.Clear();
            allCards.Clear();
        }

        private void Build()
        {
            Dictionary<string, CardDefinition> builtIn = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
            List<CardDefinition> builtInOrder = new List<CardDefinition>();
            for (int s = 0; s < definition.ContentSets.Count; s++)
            {
                GameContentSet set = definition.ContentSets[s];
                if (set == null) continue;
                for (int c = 0; c < set.Cards.Count; c++)
                {
                    CardDefinition card = set.Cards[c];
                    if (card != null && !string.IsNullOrEmpty(card.StableId) && !builtIn.ContainsKey(card.StableId))
                    {
                        builtIn.Add(card.StableId, card);
                        builtInOrder.Add(card);
                    }
                }
            }

            ApplyChanges(builtIn);
            AddNewCards(builtIn);
            ApplyPlayDeck(builtInOrder);
            for (int i = 0; i < builtInOrder.Count; i++) allCards.Add(Resolve(builtInOrder[i]));
            allCards.AddRange(newCards);
            BuildDecks();
        }

        // Playing with one of the player's decks: every card's copies become its copies in that deck (0 when absent).
        // Controller input cards belong to the controller and keep theirs.
        private void ApplyPlayDeck(List<CardDefinition> builtInOrder)
        {
            if (string.IsNullOrWhiteSpace(PlayDeckId)) return;
            PlayerDeckRecord deck = null;
            for (int d = 0; d < PlayerDecks.decks.Count; d++)
            {
                if (PlayerDecks.decks[d] != null && string.Equals(PlayerDecks.decks[d].id, PlayDeckId, StringComparison.OrdinalIgnoreCase))
                {
                    deck = PlayerDecks.decks[d];
                }
            }

            if (deck == null)
            {
                warnings.Add($"The deck chosen to play with ({PlayDeckId}) was not found; the game's own deck is used.");
                return;
            }

            PlayDeckName = string.IsNullOrWhiteSpace(deck.name) ? "My deck" : deck.name;
            Dictionary<string, int> copies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < deck.cards.Count; i++)
            {
                PlayerDeckEntry entry = deck.cards[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.cardId) || entry.count <= 0) continue;
                copies.TryGetValue(entry.cardId, out int count);
                copies[entry.cardId] = count + entry.count;
            }

            for (int i = 0; i < builtInOrder.Count; i++)
            {
                CardDefinition current = Resolve(builtInOrder[i]);
                if (current.RepresentedControllerInput != null) continue;
                copies.TryGetValue(current.StableId, out int wanted);
                if (current.Quantity == wanted) continue;
                CardDefinition counted = WithQuantity(current, wanted);
                if (changedCards.TryGetValue(current.StableId, out CardDefinition old)) DestroyRuntime(old);
                changedCards[current.StableId] = counted;
            }

            for (int i = 0; i < newCards.Count; i++)
            {
                copies.TryGetValue(newCards[i].StableId, out int wanted);
                if (newCards[i].Quantity == wanted) continue;
                CardDefinition counted = WithQuantity(newCards[i], wanted);
                DestroyRuntime(newCards[i]);
                newCards[i] = counted;
            }
        }

        private static CardDefinition WithQuantity(CardDefinition card, int quantity) =>
            CardDefinition.CreateRuntime(card, card.StableId, card.DisplayName, card.Description, quantity, null,
                card.FrontArtworkReference, card.BackArtworkReference); // icons come from the card itself

        private void ApplyChanges(Dictionary<string, CardDefinition> builtIn)
        {
            for (int i = 0; i < PlayerCards.changes.Count; i++)
            {
                CardChangeRecord change = PlayerCards.changes[i];
                if (change == null || !change.HasAnyChange) continue;
                if (!builtIn.TryGetValue(change.cardId ?? string.Empty, out CardDefinition model))
                {
                    warnings.Add($"A change names the card '{change.cardId}', which this game does not have.");
                    continue;
                }

                if (changedCards.ContainsKey(model.StableId)) continue;
                InputCostDefinition cost = null;
                if (change.changeCost && !TryReadCost(change.cost, model.DisplayName, out cost)) cost = null;
                changedCards.Add(model.StableId, CardDefinition.CreateRuntime(
                    model,
                    model.StableId,
                    change.changeName && !string.IsNullOrWhiteSpace(change.name) ? change.name.Trim() : model.DisplayName,
                    change.changeDescription ? change.description : model.Description,
                    change.changeQuantity ? change.quantity : model.Quantity,
                    cost,
                    change.changeFrontArt && !string.IsNullOrWhiteSpace(change.frontArtKey) ? change.frontArtKey.Trim() : model.FrontArtworkReference,
                    change.changeBackArt && !string.IsNullOrWhiteSpace(change.backArtKey) ? change.backArtKey.Trim() : model.BackArtworkReference,
                    change.changeIcons ? ReadIcons(change.icons) : null,
                    change.changeFrame ? (change.frame ?? string.Empty).Trim() : null));
            }
        }

        // Saved icon text into 8 places; empty text is 8 Nones (a new card starts without icons, a change clears them).
        private static ConsoleSymbol[] ReadIcons(string text)
        {
            CardDefinition.TryParseEdgeSymbols(text, out ConsoleSymbol[] icons);
            return icons;
        }

        private void AddNewCards(Dictionary<string, CardDefinition> builtIn)
        {
            string artFolder = ArtFolderOf(definition);
            for (int i = 0; i < PlayerCards.cards.Count; i++)
            {
                PlayerCardRecord record = PlayerCards.cards[i];
                if (record == null) continue;
                string id = (record.id ?? string.Empty).Trim();
                if (!Guid.TryParse(id, out _))
                {
                    warnings.Add($"The card '{record.name}' has no valid ID ('{record.id}') and is skipped.");
                    continue;
                }

                if (builtIn.ContainsKey(id) || newCardModel.ContainsKey(id))
                {
                    warnings.Add($"The card '{record.name}' uses the ID {id}, which another card already has; it is skipped.");
                    continue;
                }

                if (!builtIn.TryGetValue(record.behavesLike ?? string.Empty, out CardDefinition model))
                {
                    warnings.Add($"The card '{record.name}' behaves like '{record.behavesLike}', which this game does not have; it is skipped.");
                    continue;
                }

                InputCostDefinition cost = null;
                if (!string.IsNullOrWhiteSpace(record.cost) && !TryReadCost(record.cost, record.name, out cost)) cost = null;
                string name = string.IsNullOrWhiteSpace(record.name) ? $"New {model.DisplayName}" : record.name.Trim();
                CardDefinition card = CardDefinition.CreateRuntime(
                    model,
                    id,
                    name,
                    record.description,
                    record.quantity,
                    cost,
                    string.IsNullOrWhiteSpace(record.frontArtKey) ? PlayerContent.DefaultArtKey(artFolder, id, true) : record.frontArtKey.Trim(),
                    string.IsNullOrWhiteSpace(record.backArtKey) ? model.BackArtworkReference : record.backArtKey.Trim(),
                    ReadIcons(record.icons),
                    (record.frame ?? string.Empty).Trim());
                newCards.Add(card);
                newCardModel.Add(id, model.StableId);
            }
        }

        private void BuildDecks()
        {
            for (int s = 0; s < definition.ContentSets.Count; s++)
            {
                GameContentSet set = definition.ContentSets[s];
                if (set == null) continue;
                List<string> ids = new List<string>();
                int count = 0;
                for (int c = 0; c < set.Cards.Count; c++)
                {
                    CardDefinition card = Resolve(set.Cards[c]);
                    if (card == null) continue;
                    ids.Add(card.StableId);
                    count += card.Quantity;
                }

                int before = ids.Count;
                AddNewCardIdsFor(set, ids);
                for (int i = before; i < ids.Count; i++) count += QuantityOf(ids[i]);
                decks.Add(new GameDeckInfo(set.StableId, set.DisplayName, true, count, set.DrawCount, ids));
            }

            for (int d = 0; d < PlayerDecks.decks.Count; d++)
            {
                PlayerDeckRecord deck = PlayerDecks.decks[d];
                if (deck == null || string.IsNullOrWhiteSpace(deck.id)) continue;
                List<string> ids = new List<string>();
                int count = 0;
                for (int i = 0; i < deck.cards.Count; i++)
                {
                    PlayerDeckEntry entry = deck.cards[i];
                    if (entry == null || entry.count <= 0) continue;
                    if (!HasCard(entry.cardId))
                    {
                        warnings.Add($"The deck '{deck.name}' lists the card '{entry.cardId}', which this game does not have.");
                        continue;
                    }

                    ids.Add(entry.cardId);
                    count += entry.count;
                }

                decks.Add(new GameDeckInfo(deck.id, deck.name, false, count, 0, ids));
            }
        }

        private bool HasCard(string id)
        {
            for (int i = 0; i < allCards.Count; i++)
            {
                if (string.Equals(allCards[i].StableId, id, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private int QuantityOf(string id)
        {
            for (int i = 0; i < newCards.Count; i++)
            {
                if (string.Equals(newCards[i].StableId, id, StringComparison.OrdinalIgnoreCase)) return newCards[i].Quantity;
            }

            return 0;
        }

        private bool TryReadCost(string text, string cardName, out InputCostDefinition cost)
        {
            cost = null;
            if (!InputCostNotation.TryParse(text ?? string.Empty, out InputCostData data, out string error))
            {
                warnings.Add($"The cost '{text}' of '{cardName}' could not be read ({error}); the model card's cost is used.");
                return false;
            }

            cost = InputCostDefinition.FromData(data);
            return true;
        }

        /// <summary>The first part of the game's own art keys (doc 24), for example "trap-floor".</summary>
        public static string ArtFolderOf(GameDefinition definition)
        {
            if (definition == null) return "game";
            for (int s = 0; s < definition.ContentSets.Count; s++)
            {
                GameContentSet set = definition.ContentSets[s];
                if (set == null) continue;
                for (int c = 0; c < set.Cards.Count; c++)
                {
                    string key = set.Cards[c] != null ? set.Cards[c].FrontArtworkReference : null;
                    int slash = string.IsNullOrEmpty(key) ? -1 : key.IndexOf('/');
                    if (slash > 0) return key.Substring(0, slash).ToLowerInvariant();
                }
            }

            return (definition.StableId ?? "game").ToLowerInvariant();
        }

        private static bool Contains(GameContentSet set, string cardId)
        {
            for (int c = 0; c < set.Cards.Count; c++)
            {
                if (set.Cards[c] != null && string.Equals(set.Cards[c].StableId, cardId, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static void DestroyRuntime(CardDefinition card)
        {
            if (card == null) return;
            if (UnityEngine.Application.isPlaying) Object.Destroy(card);
            else Object.DestroyImmediate(card);
        }
    }
}
