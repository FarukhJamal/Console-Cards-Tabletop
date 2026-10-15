using System.IO;
using ConsoleCards.Definitions;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Hints
{
    /// <summary>
    /// Creates the platform hint library (doc 23 §10) at Resources/PlatformHints with the starting tips about the
    /// table, the Toolbox and the components. Only what is missing is added: an existing library keeps every hint
    /// it has (edited text included); a starting hint whose ID is not in it yet is appended.
    /// </summary>
    [InitializeOnLoad]
    public static class PlatformHintsBuilder
    {
        private const string LibraryPath = "Assets/ConsoleCards/Content/Resources/PlatformHints.asset";

        private static readonly HintEntry[] StartingHints =
        {
            new HintEntry("platform.table-empty", HintTriggers.TableReady, "empty",
                "Open + TOOLBOX to put pieces on the table. The Base Box holds the platform's pieces (Console mats, cards, dice, boards, pawns); a Game Box holds one game's own pieces.",
                HintKind.Tip, 10, 2),
            new HintEntry("platform.table-game", HintTriggers.TableReady, "game",
                "The rules card on the left says how to play. Press Change on a rule to propose a house rule; every player agrees with their own button.",
                HintKind.Tip, 10, 2),
            new HintEntry("platform.place-console", HintTriggers.ToolboxPlacing, "Console",
                "A Console mat has two sides. Right-click it to flip it to the controller side or to Duplicate it. A card snaps to the slot with the same symbol.",
                HintKind.Tip, 5, 2),
            new HintEntry("platform.place-board", HintTriggers.ToolboxPlacing, "GameBoard",
                "Drop a board against another and their edges join. Right-click a board to change its grid or the size of its spaces; cards snap to the spaces.",
                HintKind.Tip, 5, 2),
            new HintEntry("platform.place-die", HintTriggers.ToolboxPlacing, "Die",
                "Drag a die and let go to roll it. Right-click a die to set it to a number; on a Console's D-pad arrow it snaps in place.",
                HintKind.Tip, 5, 2),
            new HintEntry("platform.place-deck", HintTriggers.ToolboxPlacing, "Deck",
                "Right-click a deck for its menu, for example Draw to Hand or Shuffle.",
                HintKind.Tip, 5, 2),
            new HintEntry("platform.drag-card", HintTriggers.CardDragged, string.Empty,
                "Drop a card near a slot, pile or board space and it snaps in. F flips a card; the wheel turns it.",
                HintKind.Tip, 3, 2),
            new HintEntry("platform.undo", HintTriggers.UndoAvailable, string.Empty,
                "Every step can be undone: Ctrl+Z undoes and Ctrl+Y redoes, or use the arrows on the top bar.",
                HintKind.Tip, 2, 2),
            new HintEntry("platform.draw", HintTriggers.ActionPrefix + "DrawCards", string.Empty,
                "Drawn cards go to your Hand at the bottom of the screen. H shows or hides it.",
                HintKind.Tip, 2, 2),
            new HintEntry("platform.flip-console", HintTriggers.ActionPrefix + "FlipConsole", string.Empty,
                "The back of the Console maps the controller: A, B, X and Y take cards, and a die on a D-pad arrow can be set to a number.",
                HintKind.Tip, 2, 2),
        };

        static PlatformHintsBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(LibraryPath)) CreateMissing();
            };
        }

        [MenuItem("Console Cards/Hints/Create Missing Platform Hints")]
        public static void CreateMissing()
        {
            HintLibraryDefinition library = AssetDatabase.LoadAssetAtPath<HintLibraryDefinition>(LibraryPath);
            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                library = ScriptableObject.CreateInstance<HintLibraryDefinition>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            SerializedObject so = new SerializedObject(library);
            SerializedProperty entries = so.FindProperty("entries");
            int added = 0;
            for (int i = 0; i < StartingHints.Length; i++)
            {
                HintEntry hint = StartingHints[i];
                if (Contains(entries, hint.StableId)) continue;
                int index = entries.arraySize;
                entries.arraySize++;
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("stableId").stringValue = hint.StableId;
                entry.FindPropertyRelative("trigger").stringValue = hint.Trigger;
                entry.FindPropertyRelative("context").stringValue = hint.Context;
                entry.FindPropertyRelative("text").stringValue = hint.Text;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)hint.Kind;
                entry.FindPropertyRelative("priority").intValue = hint.Priority;
                entry.FindPropertyRelative("maxShows").intValue = hint.MaxShows;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Hints] Platform hints at {LibraryPath}: {added} starting hint(s) added, the rest kept.");
        }

        private static bool Contains(SerializedProperty entries, string stableId)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("stableId").stringValue == stableId) return true;
            }

            return false;
        }
    }
}
