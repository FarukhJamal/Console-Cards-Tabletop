using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates
{
    /// <summary>
    /// Minimum local content resolver for M4.1. It resolves stable object and Player Layout IDs only.
    /// </summary>
    public sealed class GameTemplateContentCatalog
    {
        private readonly ReadOnlyDictionary<ObjectDefinitionId, GameTemplateObjectDefinition> objectDefinitions;
        private readonly ReadOnlyDictionary<PlayerLayoutId, PlayerLayoutDefinition> playerLayouts;

        public GameTemplateContentCatalog(
            IEnumerable<GameTemplateObjectDefinition> objectDefinitions,
            IEnumerable<PlayerLayoutDefinition> playerLayouts)
        {
            this.objectDefinitions = new ReadOnlyDictionary<ObjectDefinitionId, GameTemplateObjectDefinition>(
                IndexObjectDefinitions(objectDefinitions));
            this.playerLayouts = new ReadOnlyDictionary<PlayerLayoutId, PlayerLayoutDefinition>(
                IndexPlayerLayouts(playerLayouts));
        }

        public IReadOnlyDictionary<ObjectDefinitionId, GameTemplateObjectDefinition> ObjectDefinitions =>
            objectDefinitions;

        public IReadOnlyDictionary<PlayerLayoutId, PlayerLayoutDefinition> PlayerLayouts => playerLayouts;

        public bool TryResolveObjectDefinition(
            ObjectDefinitionId id,
            out GameTemplateObjectDefinition definition)
        {
            return objectDefinitions.TryGetValue(id, out definition);
        }

        public bool TryResolvePlayerLayout(
            PlayerLayoutId id,
            out PlayerLayoutDefinition definition)
        {
            return playerLayouts.TryGetValue(id, out definition);
        }

        private static Dictionary<ObjectDefinitionId, GameTemplateObjectDefinition> IndexObjectDefinitions(
            IEnumerable<GameTemplateObjectDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            Dictionary<ObjectDefinitionId, GameTemplateObjectDefinition> indexed =
                new Dictionary<ObjectDefinitionId, GameTemplateObjectDefinition>();
            foreach (GameTemplateObjectDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Object definitions cannot contain null entries.", nameof(definitions));
                }

                if (definition.Id.IsEmpty)
                {
                    throw new ArgumentException("Object definition IDs cannot be empty.", nameof(definitions));
                }

                if (indexed.ContainsKey(definition.Id))
                {
                    throw new ArgumentException("Object definition IDs must be unique.", nameof(definitions));
                }

                indexed.Add(definition.Id, definition);
            }

            return indexed;
        }

        private static Dictionary<PlayerLayoutId, PlayerLayoutDefinition> IndexPlayerLayouts(
            IEnumerable<PlayerLayoutDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            Dictionary<PlayerLayoutId, PlayerLayoutDefinition> indexed =
                new Dictionary<PlayerLayoutId, PlayerLayoutDefinition>();
            foreach (PlayerLayoutDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Player Layout definitions cannot contain null entries.", nameof(definitions));
                }

                if (indexed.ContainsKey(definition.Id))
                {
                    throw new ArgumentException("Player Layout IDs must be unique.", nameof(definitions));
                }

                indexed.Add(definition.Id, definition);
            }

            return indexed;
        }
    }

    public sealed class GameTemplateObjectDefinition
    {
        public GameTemplateObjectDefinition(
            ObjectDefinitionId id,
            TabletopObjectKind kind,
            string displayName)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Object definition ID cannot be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Object definition display name cannot be empty.", nameof(displayName));
            }

            Id = id;
            Kind = kind;
            DisplayName = displayName;
        }

        public ObjectDefinitionId Id { get; }

        public TabletopObjectKind Kind { get; }

        public string DisplayName { get; }
    }
}
