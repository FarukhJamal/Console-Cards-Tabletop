using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using UnityEngine;

namespace ConsoleCards.Presentation.Catalog
{
    /// <summary>
    /// The whole component shelf (doc 22): the Base box, the Controller box, one Game box per game and the
    /// environment. The scene references only this asset; adding a game means adding its Game box here.
    /// </summary>
    [CreateAssetMenu(fileName = "ComponentLibrary", menuName = "Console Cards/Components/Component Library")]
    public sealed class ComponentLibrary : ScriptableObject
    {
        [SerializeField] private ComponentCatalog baseBox;
        [SerializeField] private ComponentCatalog controllerBox;
        [SerializeField] private List<ComponentCatalog> gameBoxes = new List<ComponentCatalog>();
        [SerializeField] private ComponentCatalog environment;

        private readonly List<ComponentCatalog> catalogs = new List<ComponentCatalog>();

        public ComponentCatalog BaseBox => baseBox;

        public ComponentCatalog ControllerBox => controllerBox;

        public IReadOnlyList<ComponentCatalog> GameBoxes => gameBoxes;

        /// <summary>Optional until the environment shelf exists.</summary>
        public ComponentCatalog Environment => environment;

        /// <summary>Every catalog in shelf order: Base, Controller, the Game boxes, then the environment.</summary>
        public IReadOnlyList<ComponentCatalog> Catalogs => catalogs;

        /// <summary>
        /// Checks the shelf shape (each slot holds a catalog of its category, each game has one box) and every
        /// catalog, with IDs unique across the whole library. Call once at initialisation.
        /// </summary>
        public void Validate()
        {
            catalogs.Clear();
            RequireCategory(baseBox, ComponentCatalogCategory.BaseBox, nameof(baseBox));
            RequireCategory(controllerBox, ComponentCatalogCategory.ControllerBox, nameof(controllerBox));
            catalogs.Add(baseBox);
            catalogs.Add(controllerBox);
            HashSet<GameDefinition> games = new HashSet<GameDefinition>();
            for (int i = 0; i < gameBoxes.Count; i++)
            {
                ComponentCatalog gameBox = gameBoxes[i];
                RequireCategory(gameBox, ComponentCatalogCategory.GameBox, $"{nameof(gameBoxes)}[{i}]");
                if (gameBox.Game != null && !games.Add(gameBox.Game))
                {
                    throw new InvalidOperationException(
                        $"Component library '{name}' has two Game boxes for {gameBox.Game.DisplayName}.");
                }

                if (catalogs.Contains(gameBox))
                {
                    throw new InvalidOperationException($"Component library '{name}' lists {gameBox.DisplayName} twice.");
                }

                catalogs.Add(gameBox);
            }

            if (environment != null)
            {
                RequireCategory(environment, ComponentCatalogCategory.Environment, nameof(environment));
                catalogs.Add(environment);
            }

            HashSet<string> knownIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> catalogIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalogs.Count; i++)
            {
                catalogs[i].Validate(knownIds);
                if (!catalogIds.Add(catalogs[i].CatalogId))
                {
                    throw new InvalidOperationException(
                        $"Component library '{name}' has two catalogs with ID {catalogs[i].CatalogId}.");
                }
            }
        }

        /// <summary>The Game box of a game, if the library has one.</summary>
        public bool TryGetGameBox(GameDefinition game, out ComponentCatalog gameBox)
        {
            for (int i = 0; i < gameBoxes.Count; i++)
            {
                if (gameBoxes[i] != null && game != null && gameBoxes[i].Game == game)
                {
                    gameBox = gameBoxes[i];
                    return true;
                }
            }

            gameBox = null;
            return false;
        }

        private void RequireCategory(ComponentCatalog catalog, ComponentCatalogCategory category, string slot)
        {
            if (catalog == null)
            {
                throw new InvalidOperationException($"Component library '{name}' requires {slot}.");
            }

            if (catalog.Category != category)
            {
                throw new InvalidOperationException(
                    $"Component library '{name}' {slot} holds '{catalog.DisplayName}', which is not a {category} catalog.");
            }
        }
    }
}
