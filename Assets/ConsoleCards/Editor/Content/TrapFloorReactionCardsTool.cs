using ConsoleCards.Definitions;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Content
{
    /// <summary>
    /// Owner, 2026-11-04 (TRAP CARDS rules): two new Reaction cards, Insurance and Lasso. The owner writes their costs
    /// and full text later; until then they have no cost, so they are floor Abilities (blue cards) taken for free when a
    /// player lands on them, and they are listed with the game's Abilities. Run once:
    /// Console Cards > Trap Floor > Add Insurance and Lasso. Existing cards are left as they are.
    /// </summary>
    public static class TrapFloorReactionCardsTool
    {
        private const string Folder = "Assets/ConsoleCards/Content/Definitions/TrapFloor";
        private const string GamePath = Folder + "/TrapFloorGameDefinition.asset";

        [MenuItem("Console Cards/Trap Floor/Add Insurance and Lasso")]
        public static void AddReactionCards()
        {
            GameDefinition game = AssetDatabase.LoadAssetAtPath<GameDefinition>(GamePath);
            if (game == null)
            {
                Debug.LogWarning($"[Trap Floor] {GamePath} was not found.");
                return;
            }

            CardDefinition insurance = MakeCard(
                Folder + "/AbilityInsurance.asset",
                "54460015-4f4f-4000-8000-0015000000a1",
                "Insurance",
                "Reaction. Saves you when you would fall into a collapsed tile. (Cost and full text to come.)",
                "insurance");
            CardDefinition lasso = MakeCard(
                Folder + "/AbilityLasso.asset",
                "54460015-4f4f-4000-8000-0015000000a2",
                "Lasso",
                "Reaction. (Effect and cost to come.) Not used in Free for all.",
                "lasso");

            SerializedObject gameObject = new SerializedObject(game);
            SerializedProperty sets = gameObject.FindProperty("contentSets");
            int added = 0;
            for (int i = 0; i < sets.arraySize; i++)
            {
                SerializedProperty set = sets.GetArrayElementAtIndex(i);
                string id = set.FindPropertyRelative("stableId").stringValue;
                if (id != "trap-floor-abilities" && id != "trap-floor-floor-abilities") continue;
                SerializedProperty cards = set.FindPropertyRelative("cards");
                added += AddOnce(cards, insurance);
                added += AddOnce(cards, lasso);
            }

            gameObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(game);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Trap Floor] Insurance and Lasso are ready ({added} new place(s) in the game's card sets).");
        }

        private static int AddOnce(SerializedProperty cards, CardDefinition card)
        {
            for (int i = 0; i < cards.arraySize; i++)
            {
                if (cards.GetArrayElementAtIndex(i).objectReferenceValue == card) return 0;
            }

            cards.InsertArrayElementAtIndex(cards.arraySize);
            cards.GetArrayElementAtIndex(cards.arraySize - 1).objectReferenceValue = card;
            return 1;
        }

        private static CardDefinition MakeCard(string path, string stableId, string name, string text, string tag)
        {
            CardDefinition existing = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            if (existing != null) return existing;
            CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
            card.name = "Ability" + name;
            SerializedObject data = new SerializedObject(card);
            data.FindProperty("stableId").stringValue = stableId;
            data.FindProperty("displayName").stringValue = name;
            data.FindProperty("category").stringValue = "Ability";
            data.FindProperty("description").stringValue = text;
            data.FindProperty("quantity").intValue = 1;
            SerializedProperty placement = data.FindProperty("preferredConsolePlacement");
            if (placement != null) placement.stringValue = "Side";
            SerializedProperty tags = data.FindProperty("tags");
            string[] words = { "trap-floor", "ability", "reaction", tag, "cost-pending" };
            tags.arraySize = words.Length;
            for (int i = 0; i < words.Length; i++) tags.GetArrayElementAtIndex(i).stringValue = words[i];
            data.FindProperty("effectMetadata").stringValue = "trap-floor-ability-" + tag;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(card, path);
            return card;
        }
    }
}
