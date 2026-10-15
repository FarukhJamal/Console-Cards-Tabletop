using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Catalog;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Toolbox
{
    /// <summary>
    /// Fills the Controller box and each Game box with tiles that place real cards (doc 22, C3c):
    /// a Controller Deck of every card that stands for a controller input, one deck per content set of the box's
    /// game, and the game's Avatars. Entries that already exist (same name) are kept; nothing is removed. The
    /// Toolbox tiles are synced afterwards.
    /// </summary>
    public static class CatalogBoxFiller
    {
        [MenuItem("Console Cards/Toolbox/Fill Controller and Game Boxes")]
        public static void Fill()
        {
            ComponentLibrary library = LoadLibrary();
            if (library == null)
            {
                Debug.LogWarning("[Catalog] No Component Library found.");
                return;
            }

            if (!TryGetBaseEntry(library, ComponentCatalogKind.Deck, out ComponentCatalogEntry baseDeck)
                || !baseDeck.TryGetDefinition(ComponentDefinitionLink.BayStyleRole, out ScriptableObject bayStyle)
                || !TryGetBaseEntry(library, ComponentCatalogKind.Card, out ComponentCatalogEntry baseCard))
            {
                Debug.LogWarning("[Catalog] The Base Box needs its Card and Deck entries first.");
                return;
            }

            int added = 0;
            if (library.ControllerBox != null)
            {
                List<CardDefinition> controllerCards = CollectControllerCards(library);
                if (controllerCards.Count > 0)
                {
                    added += AddDeckEntry(library.ControllerBox, "Controller Deck",
                        $"The controller deck: {Total(controllerCards)} cards", baseDeck.Prefab, bayStyle, controllerCards, null);
                }
            }

            for (int i = 0; i < library.GameBoxes.Count; i++)
            {
                ComponentCatalog box = library.GameBoxes[i];
                GameDefinition game = box != null ? box.Game : null;
                if (game == null) continue;
                IReadOnlyList<GameContentSet> sets = game.ContentSets;
                for (int s = 0; sets != null && s < sets.Count; s++)
                {
                    GameContentSet set = sets[s];
                    List<CardDefinition> cards = CollectSet(set);
                    if (cards.Count == 0 || AllControllerInputs(cards)) continue; // controller cards live in the Controller box
                    string name = string.IsNullOrWhiteSpace(set.DisplayName) ? $"Set {s + 1}" : set.DisplayName;
                    added += AddDeckEntry(box, name, $"{Total(cards)} {game.DisplayName} cards", baseDeck.Prefab, bayStyle, cards, null);
                }

                List<AvatarDefinition> avatars = new List<AvatarDefinition>();
                for (int a = 0; game.Avatars != null && a < game.Avatars.Count; a++)
                {
                    if (game.Avatars[a] != null) avatars.Add(game.Avatars[a]);
                }

                if (avatars.Count > 0)
                {
                    added += AddDeckEntry(box, "Avatars", $"The {avatars.Count} {game.DisplayName} avatar cards",
                        baseDeck.Prefab, bayStyle, null, avatars);
                }
            }

            AssetDatabase.SaveAssets();
            try
            {
                library.Validate();
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"[Catalog] The library does not validate after filling: {exception.Message}");
                return;
            }

            Debug.Log($"[Catalog] Added {added} box tile(s). Syncing the Toolbox tiles.");
            ComponentToolboxBuilder.SyncFromLibrary();
        }

        private static int AddDeckEntry(
            ComponentCatalog catalog,
            string displayName,
            string description,
            GameObject prefab,
            ScriptableObject bayStyle,
            List<CardDefinition> cards,
            List<AvatarDefinition> avatars)
        {
            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("entries");
            int highestOrder = -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty existing = entries.GetArrayElementAtIndex(i);
                if (existing.FindPropertyRelative("displayName").stringValue == displayName) return 0;
                highestOrder = Math.Max(highestOrder, existing.FindPropertyRelative("toolboxOrder").intValue);
            }

            int index = entries.arraySize;
            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString();
            entry.FindPropertyRelative("displayName").stringValue = displayName;
            entry.FindPropertyRelative("description").stringValue = description;
            entry.FindPropertyRelative("kind").intValue = (int)ComponentCatalogKind.Deck;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("icon").objectReferenceValue = null;
            entry.FindPropertyRelative("showInToolbox").boolValue = true;
            entry.FindPropertyRelative("toolboxOrder").intValue = highestOrder + 1;
            SerializedProperty links = entry.FindPropertyRelative("definitions");
            links.arraySize = 0;
            AddLink(links, ComponentDefinitionLink.BayStyleRole, bayStyle);
            for (int i = 0; cards != null && i < cards.Count; i++) AddLink(links, ComponentDefinitionLink.CardRole, cards[i]);
            for (int i = 0; avatars != null && i < avatars.Count; i++) AddLink(links, ComponentDefinitionLink.CardRole, avatars[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return 1;
        }

        private static void AddLink(SerializedProperty links, string role, ScriptableObject definition)
        {
            int index = links.arraySize;
            links.arraySize++;
            SerializedProperty link = links.GetArrayElementAtIndex(index);
            link.FindPropertyRelative("role").stringValue = role;
            link.FindPropertyRelative("definition").objectReferenceValue = definition;
        }

        private static List<CardDefinition> CollectControllerCards(ComponentLibrary library)
        {
            List<CardDefinition> cards = new List<CardDefinition>();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < library.GameBoxes.Count; i++)
            {
                GameDefinition game = library.GameBoxes[i] != null ? library.GameBoxes[i].Game : null;
                if (game == null || game.ContentSets == null) continue;
                for (int s = 0; s < game.ContentSets.Count; s++)
                {
                    foreach (CardDefinition card in CollectSet(game.ContentSets[s]))
                    {
                        if (card.RepresentedControllerInput.HasValue && seen.Add(card.StableId)) cards.Add(card);
                    }
                }

                if (cards.Count > 0) break; // one game's controller cards make the deck
            }

            return cards;
        }

        private static List<CardDefinition> CollectSet(GameContentSet set)
        {
            List<CardDefinition> cards = new List<CardDefinition>();
            if (set == null || set.Cards == null) return cards;
            for (int i = 0; i < set.Cards.Count; i++)
            {
                if (set.Cards[i] != null && set.Cards[i].TryGetObjectDefinitionId(out _)) cards.Add(set.Cards[i]);
            }

            return cards;
        }

        private static bool AllControllerInputs(List<CardDefinition> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].RepresentedControllerInput.HasValue) return false;
            }

            return true;
        }

        private static int Total(List<CardDefinition> cards)
        {
            int total = 0;
            for (int i = 0; i < cards.Count; i++) total += Math.Max(1, cards[i].Quantity);
            return total;
        }

        private static bool TryGetBaseEntry(ComponentLibrary library, ComponentCatalogKind kind, out ComponentCatalogEntry entry)
        {
            entry = null;
            return library.BaseBox != null && library.BaseBox.TryGetFirst(kind, out entry);
        }

        private static ComponentLibrary LoadLibrary()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(ComponentLibrary));
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<ComponentLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }
    }
}
