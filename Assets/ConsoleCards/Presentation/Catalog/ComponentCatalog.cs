using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Prototype;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Catalog
{
    /// <summary>What a catalog entry is; decides which prefab parts and linked definitions it must have.</summary>
    public enum ComponentCatalogKind
    {
        Card = 0,
        Deck = 1,
        Stack = 2,
        DiscardPile = 3,
        Hand = 4,
        Pawn = 5,
        Token = 6,
        Die = 7,
        Console = 8,
        GameBoard = 9,
        ControllerMappingBoard = 10,
        ControllerBox = 11,
        Other = 12,
    }

    /// <summary>Which shelf a catalog belongs to: a product box (Base, Controller, a Game) or the environment.</summary>
    public enum ComponentCatalogCategory
    {
        BaseBox = 0,
        ControllerBox = 1,
        GameBox = 2,
        Environment = 3,
    }

    /// <summary>A named relationship from an entry to a definition asset (for example Console → Layout).</summary>
    [Serializable]
    public sealed class ComponentDefinitionLink
    {
        public const string LayoutRole = "Layout";
        public const string BayStyleRole = "BayStyle";
        public const string ShapeRole = "Shape";
        public const string GameRole = "Game";

        [SerializeField] private string role;
        [SerializeField] private ScriptableObject definition;

        public string Role => role;

        public ScriptableObject Definition => definition;
    }

    /// <summary>
    /// One component in a catalog: the stable ID Runtime uses, its 3D prefab on the table, its UI face (icon) and
    /// its linked definitions. The same entry is used whether a template places the component or the Toolbox does.
    /// </summary>
    [Serializable]
    public sealed class ComponentCatalogEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private ComponentCatalogKind kind;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool showInToolbox = true;
        [SerializeField] private int toolboxOrder;
        [SerializeField] private List<ComponentDefinitionLink> definitions = new List<ComponentDefinitionLink>();

        public string Id => id;

        public string DisplayName => displayName;

        /// <summary>One short line for the UI face (the Toolbox tile hint).</summary>
        public string Description => description ?? string.Empty;

        public ComponentCatalogKind Kind => kind;

        public GameObject Prefab => prefab;

        public Sprite Icon => icon;

        public bool ShowInToolbox => showInToolbox;

        public int ToolboxOrder => toolboxOrder;

        public IReadOnlyList<ComponentDefinitionLink> Definitions => definitions;

        public bool TryGetDefinition<TDefinition>(string role, out TDefinition definition)
            where TDefinition : ScriptableObject
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                ComponentDefinitionLink link = definitions[i];
                if (link != null
                    && string.Equals(link.Role, role, StringComparison.Ordinal)
                    && link.Definition is TDefinition typed)
                {
                    definition = typed;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public TComponent GetPrefabComponent<TComponent>() where TComponent : Component
        {
            TComponent component = prefab != null ? prefab.GetComponent<TComponent>() : null;
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"Catalog entry '{displayName}' ({id}) prefab requires a {typeof(TComponent).Name} on its root.");
            }

            return component;
        }
    }

    /// <summary>
    /// A shelf of components for one product box (Base box, Controller box, a Game box) or the environment
    /// (doc 22). Every component and its definitions are registered here, never wired one by one. A Game box
    /// names its game; catalogs are gathered by the ComponentLibrary.
    /// </summary>
    [CreateAssetMenu(fileName = "ComponentCatalog", menuName = "Console Cards/Components/Component Catalog")]
    public sealed class ComponentCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId;
        [SerializeField] private string displayName;
        [SerializeField] private ComponentCatalogCategory category;
        [SerializeField] private GameDefinition game;
        [SerializeField] private List<ComponentCatalogEntry> entries = new List<ComponentCatalogEntry>();

        public string CatalogId => catalogId;

        public string DisplayName => displayName;

        public ComponentCatalogCategory Category => category;

        /// <summary>The game a Game box belongs to; null for every other category.</summary>
        public GameDefinition Game => game;

        public IReadOnlyList<ComponentCatalogEntry> Entries => entries;

        /// <summary>Checks IDs, prefabs and kind-specific parts and links. Call once at initialisation.</summary>
        public void Validate(ISet<string> knownIds)
        {
            if (knownIds == null)
            {
                throw new ArgumentNullException(nameof(knownIds));
            }

            if (!IsGuid(catalogId) || string.IsNullOrWhiteSpace(displayName))
            {
                throw new InvalidOperationException($"Component catalog '{name}' requires a GUID ID and a display name.");
            }

            if (category != ComponentCatalogCategory.GameBox && game != null)
            {
                throw new InvalidOperationException($"Component catalog '{displayName}' names a game but is not a Game box.");
            }

            if (category == ComponentCatalogCategory.GameBox && game == null && entries.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Game box '{displayName}' has components, so it must name its game.");
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ComponentCatalogEntry entry = entries[i];
                if (entry == null || !IsGuid(entry.Id) || string.IsNullOrWhiteSpace(entry.DisplayName))
                {
                    throw new InvalidOperationException(
                        $"Component catalog '{displayName}' entry {i} requires a GUID ID and a display name.");
                }

                if (!knownIds.Add(entry.Id))
                {
                    throw new InvalidOperationException(
                        $"Component catalog '{displayName}' entry '{entry.DisplayName}' repeats ID {entry.Id}.");
                }

                if (entry.Prefab == null || entry.Prefab.scene.IsValid())
                {
                    throw new InvalidOperationException(
                        $"Component catalog '{displayName}' entry '{entry.DisplayName}' requires a prefab asset.");
                }

                for (int link = 0; link < entry.Definitions.Count; link++)
                {
                    ComponentDefinitionLink definitionLink = entry.Definitions[link];
                    if (definitionLink == null
                        || string.IsNullOrWhiteSpace(definitionLink.Role)
                        || definitionLink.Definition == null)
                    {
                        throw new InvalidOperationException(
                            $"Component catalog '{displayName}' entry '{entry.DisplayName}' link {link} requires a role and a definition.");
                    }
                }

                ValidateKind(entry);
            }
        }

        public bool TryGetFirst(ComponentCatalogKind kind, out ComponentCatalogEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Kind == kind)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        private void ValidateKind(ComponentCatalogEntry entry)
        {
            switch (entry.Kind)
            {
                case ComponentCatalogKind.Deck:
                    ValidatePile<DeckView>(entry);
                    break;
                case ComponentCatalogKind.Stack:
                    ValidatePile<StackView>(entry);
                    break;
                case ComponentCatalogKind.DiscardPile:
                    ValidatePile<DiscardPileView>(entry);
                    break;
                case ComponentCatalogKind.Console:
                {
                    ConsoleView consoleView = entry.GetPrefabComponent<ConsoleView>();
                    if (!entry.TryGetDefinition(ComponentDefinitionLink.LayoutRole, out ConsoleLayoutDefinition layout))
                    {
                        Fail(entry, "requires a Layout link to a Console Layout Definition");
                    }

                    ConsoleLayoutBinding binding = consoleView.GetComponent<ConsoleLayoutBinding>();
                    if (binding != null && binding.Layout != layout)
                    {
                        Fail(entry, "links a Layout that differs from its prefab's Console Layout Binding");
                    }

                    break;
                }
                case ComponentCatalogKind.Die:
                {
                    entry.GetPrefabComponent<DieView>();
                    if (!entry.TryGetDefinition(ComponentDefinitionLink.ShapeRole, out PhysicalDieDefinition _))
                    {
                        Fail(entry, "requires at least one Shape link to a Physical Die Definition");
                    }

                    break;
                }
            }
        }

        // A pile prefab: its view, the fixed-container visual with a bay, and a BayStyle link matching the bay.
        private void ValidatePile<TView>(ComponentCatalogEntry entry) where TView : MonoBehaviour, IContainerView
        {
            entry.GetPrefabComponent<TView>();
            PrototypeFixedContainerVisual visual = entry.GetPrefabComponent<PrototypeFixedContainerVisual>();
            if (visual.Bay == null)
            {
                Fail(entry, "requires a Bay (Pile Bay View) on its fixed-container visual");
            }

            if (!entry.TryGetDefinition(ComponentDefinitionLink.BayStyleRole, out PileBayStyle bayStyle)
                || visual.Bay.Style != bayStyle)
            {
                Fail(entry, "requires a BayStyle link equal to its bay's Pile Bay Style");
            }
        }

        private void Fail(ComponentCatalogEntry entry, string problem)
        {
            throw new InvalidOperationException(
                $"Component catalog '{displayName}' entry '{entry.DisplayName}' {problem}.");
        }

        private static bool IsGuid(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out Guid parsed) && parsed != Guid.Empty;
        }
    }
}
