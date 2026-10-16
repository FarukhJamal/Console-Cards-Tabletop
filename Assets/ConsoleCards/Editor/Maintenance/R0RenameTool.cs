using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Maintenance
{
    /// <summary>
    /// R0 (doc 30 A5): gives the Prototype files their final names. The code already uses the final names; this
    /// renames the files, prefabs, materials, the scene and three folders inside Unity, so every reference (by the
    /// asset's ID) stays intact. Offered once the code compiles, and in Console Cards &gt; Maintenance &gt; Finish R0
    /// Renames. Safe to run again: finished steps are skipped. Delete this file once R0 is approved.
    /// </summary>
    [InitializeOnLoad]
    public static class R0RenameTool
    {
        private const string Root = "Assets/ConsoleCards/";
        private const string BasePrefabs = Root + "Content/Prefabs/Base";
        private const string OldScene = Root + "Presentation/Scenes/TabletopPrototype.unity";
        private const string NewScene = Root + "Presentation/Scenes/Tabletop.unity";
        private const string LaterKey = "ConsoleCards.R0.Later";

        // Old path (under Assets/ConsoleCards) and new file name, in the same folder. Files first, folders after.
        private static readonly string[,] Files =
        {
            { "Editor/TabletopPrototypeCompositionEditor.cs", "TabletopCompositionEditor.cs" },
            { "Presentation/Prototype/PrototypeCardVisualReferences.cs", "CardVisualReferences.cs" },
            { "Presentation/Prototype/PrototypeConsoleSlotVisual.cs", "ConsoleSlotVisual.cs" },
            { "Presentation/Prototype/PrototypeFixedContainerVisual.cs", "FixedContainerVisual.cs" },
            { "Presentation/Prototype/PrototypeSessionUndoSnapshot.cs", "SessionUndoSnapshot.cs" },
            { "Presentation/Prototype/PrototypeWorldTextDepth.cs", "WorldTextDepth.cs" },
            { "Presentation/Prototype/TabletopPrototypeComposition.cs", "TabletopComposition.cs" },
            { "Presentation/Prototype/TabletopPrototypeComposition.CardEditor.cs", "TabletopComposition.CardEditor.cs" },
            { "Presentation/Prototype/TabletopPrototypeComposition.DeckEditor.cs", "TabletopComposition.DeckEditor.cs" },
            { "Presentation/Prototype/TabletopPrototypeComposition.GameSelect.cs", "TabletopComposition.GameSelect.cs" },
            { "Presentation/Prototype/TabletopPrototypeComposition.PlayerGames.cs", "TabletopComposition.PlayerGames.cs" },
            { "Presentation/Prototype/TabletopPrototypeComposition.Profiles.cs", "TabletopComposition.Profiles.cs" },
            { "Presentation/Prototype/TabletopPrototypeComposition.TrapFloorHints.cs", "TabletopComposition.TrapFloorHints.cs" },
            { "Presentation/UI/PrototypeActionAbilityPurchasePopupView.cs", "ActionAbilityPurchasePopupView.cs" },
            { "Presentation/UI/PrototypeCardInspectView.cs", "CardInspectView.cs" },
            { "Presentation/UI/PrototypeComponentToolboxView.cs", "LegacyComponentToolboxView.cs" },
            { "Presentation/UI/PrototypeFocusedCardOptionView.cs", "FocusedCardOptionView.cs" },
            { "Presentation/UI/PrototypeFocusedCardSelectionView.cs", "FocusedCardSelectionView.cs" },
            { "Presentation/UI/PrototypeGameTemplatesPanelView.cs", "GameTemplatesPanelView.cs" },
            { "Presentation/UI/PrototypeInteractionGuide.cs", "InteractionGuideView.cs" },
            { "Presentation/UI/PrototypePopupActionRowView.cs", "PopupActionRowView.cs" },
            { "Presentation/UI/PrototypeQuantityPopupView.cs", "QuantityPopupView.cs" },
            { "Presentation/UI/PrototypeRuntimeUiController.cs", "TableUiController.cs" },
            { "Presentation/UI/PrototypeRuntimeUiRoot.cs", "TableUiRoot.cs" },
            { "Presentation/UI/PrototypeSessionTemplateButtonView.cs", "SessionTemplateButtonView.cs" },
            { "Presentation/UI/PrototypeStatusMessageView.cs", "StatusMessageView.cs" },
            { "Presentation/UI/PrototypeTabletopPopupView.cs", "TabletopPopupView.cs" },
            { "Presentation/UI/PrototypeTrapFloorHudView.cs", "TrapFloorHudView.cs" },
            { "Content/Prefabs/Prototype/PrototypeActionAbilityPurchasePopup.prefab", "ActionAbilityPurchasePopup.prefab" },
            { "Content/Prefabs/Prototype/PrototypeCard.prefab", "Card.prefab" },
            { "Content/Prefabs/Prototype/PrototypeCardInspectPopup.prefab", "CardInspectPopup.prefab" },
            { "Content/Prefabs/Prototype/PrototypeComponentToolbox.prefab", "ComponentToolbox.prefab" },
            { "Content/Prefabs/Prototype/PrototypeConsole.prefab", "Console.prefab" },
            { "Content/Prefabs/Prototype/PrototypeConsoleSlot.prefab", "ConsoleSlot.prefab" },
            { "Content/Prefabs/Prototype/PrototypeDeck.prefab", "Deck.prefab" },
            { "Content/Prefabs/Prototype/PrototypeDie.prefab", "Die.prefab" },
            { "Content/Prefabs/Prototype/PrototypeDiscardPile.prefab", "DiscardPile.prefab" },
            { "Content/Prefabs/Prototype/PrototypeHand.prefab", "Hand.prefab" },
            { "Content/Prefabs/Prototype/PrototypeInteractionGuide.prefab", "InteractionGuide.prefab" },
            { "Content/Prefabs/Prototype/PrototypePawn.prefab", "Pawn.prefab" },
            { "Content/Prefabs/Prototype/PrototypePopupActionRow.prefab", "PopupActionRow.prefab" },
            { "Content/Prefabs/Prototype/PrototypeQuantityPopup.prefab", "QuantityPopup.prefab" },
            { "Content/Prefabs/Prototype/PrototypeSessionTemplateButton.prefab", "SessionTemplateButton.prefab" },
            { "Content/Prefabs/Prototype/PrototypeStack.prefab", "Stack.prefab" },
            { "Content/Prefabs/Prototype/PrototypeTabletopPopup.prefab", "TabletopPopup.prefab" },
            { "Content/Prefabs/Prototype/PrototypeToken.prefab", "Token.prefab" },
            { "Content/Prefabs/Prototype/PrototypeTrapFloorHud.prefab", "TrapFloorHud.prefab" },
            { "Content/Prefabs/Prototype/PrototypeUiActionButton.prefab", "UiActionButton.prefab" },
            { "Content/Prefabs/Prototype/PrototypeRuntimeUiRoot.prefab", "TableUiRoot.prefab" },
            { "Content/Prefabs/Prototype/PrototypeUiPrefabCatalog.asset", "BaseUiPrefabCatalog.asset" },
            { "Content/Materials/Prototype/CardFaceDownPrototype.mat", "CardFaceDown.mat" },
            { "Content/Materials/Prototype/CardFaceUpPrototype.mat", "CardFaceUp.mat" },
            { "Content/Materials/Prototype/ConsoleAccentPrototype.mat", "ConsoleAccent.mat" },
            { "Content/Materials/Prototype/ConsoleBodyPrototype.mat", "ConsoleBody.mat" },
            { "Content/Materials/Prototype/ConsolePanelPrototype.mat", "ConsolePanel.mat" },
            { "Content/Materials/Prototype/ConsoleSlotPrototype.mat", "ConsoleSlot.mat" },
            { "Content/Materials/Prototype/ContainerInvalidTargetPrototype.mat", "ContainerInvalidTarget.mat" },
            { "Content/Materials/Prototype/ContainerSourceTargetPrototype.mat", "ContainerSourceTarget.mat" },
            { "Content/Materials/Prototype/ContainerValidTargetPrototype.mat", "ContainerValidTarget.mat" },
            { "Content/Materials/Prototype/DeckPrototype.mat", "Deck.mat" },
            { "Content/Materials/Prototype/DiscardPilePrototype.mat", "DiscardPile.mat" },
            { "Content/Materials/Prototype/HandPrototype.mat", "Hand.mat" },
            { "Content/Materials/Prototype/PawnPrototype.mat", "Pawn.mat" },
            { "Content/Materials/Prototype/SelectionHighlightPrototype.mat", "SelectionHighlight.mat" },
            { "Content/Materials/Prototype/StackPrototype.mat", "Stack.mat" },
            { "Content/Materials/Prototype/TokenPrototype.mat", "Token.mat" },
            { "Content/Materials/TableSurfacePrototype.mat", "TableSurface.mat" },
            { "Content/Resources/PrototypeWorldText.mat", "WorldText.mat" },
            { "Content/Resources/PrototypeWorldText.shader", "WorldText.shader" },
            { "Presentation/Scenes/TabletopPrototype.unity", "Tabletop.unity" },
        };

        private static readonly string[,] Folders =
        {
            { "Presentation/Prototype", "Composition" },
            { "Content/Prefabs/Prototype", "Base" },
            { "Content/Materials/Prototype", "Base" },
        };

        static R0RenameTool()
        {
            EditorApplication.delayCall += Offer;
        }

        private static void Offer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(LaterKey, false)) return;
            int pending = PendingCount();
            if (pending == 0) return;
            bool now = EditorUtility.DisplayDialog(
                "Console Cards - R0 renames",
                $"The code now uses the final names, but {pending} files and folders still have their Prototype names.\n\n"
                + "Rename them now? Every reference stays intact. Do not enter Play mode or save the scene before this is done.",
                "Rename now",
                "Later");
            if (now) Run();
            else SessionState.SetBool(LaterKey, true);
        }

        [MenuItem("Console Cards/Maintenance/Finish R0 Renames")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[R0] Leave Play mode first, then run Console Cards > Maintenance > Finish R0 Renames.");
                return;
            }

            int renamed = 0;
            List<string> problems = new List<string>();
            for (int i = 0; i < Files.GetLength(0); i++)
            {
                Rename(Root + Files[i, 0], Path.GetFileNameWithoutExtension(Files[i, 1]), Files[i, 1], false, ref renamed, problems);
            }

            for (int i = 0; i < Folders.GetLength(0); i++)
            {
                Rename(Root + Folders[i, 0], Folders[i, 1], Folders[i, 1], true, ref renamed, problems);
            }

            UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReimportRenamedScripts();
            List<string> unused = UnusedBasePrefabs();
            foreach (string problem in problems) Debug.LogWarning("[R0] " + problem);
            if (unused.Count > 0)
            {
                Debug.Log("[R0] Nothing in the project uses these (they can be deleted after a check):\n" + string.Join("\n", unused));
            }

            string summary = $"Renamed {renamed} files and folders."
                + (problems.Count > 0 ? $" {problems.Count} could not be renamed: see the Console." : string.Empty)
                + (unused.Count > 0 ? $" {unused.Count} old prefabs look unused: listed in the Console." : string.Empty);
            Debug.Log("[R0] " + summary);
            EditorUtility.DisplayDialog("Console Cards - R0 renames", summary, "OK");
        }

        // A renamed script keeps its old link to its class until Unity reads it again: a script whose first type
        // is not its view (for example a model class above it) then loads as "missing". Reading every renamed
        // script again repairs that; nothing in the files changes.
        [MenuItem("Console Cards/Maintenance/Repair R0 Script Links")]
        public static void ReimportRenamedScripts()
        {
            int count = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < Files.GetLength(0); i++)
                {
                    if (!Files[i, 1].EndsWith(".cs")) continue;
                    string path = NewPath(Root + Files[i, 0], Files[i, 1]);
                    if (!File.Exists(path)) continue;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[R0] Read {count} renamed scripts again so each one links to its class.");
        }

        // Writes to the Console, for every UI catalog entry, what Unity finds behind it: the prefab file, the scripts on
        // its root and the class each script is linked to. Used to find a link the renames broke.
        [MenuItem("Console Cards/Maintenance/Check UI Catalog Links")]
        public static void CheckUiCatalogLinks()
        {
            string[] catalogs = AssetDatabase.FindAssets("t:UiPrefabCatalog");
            foreach (string catalogGuid in catalogs)
            {
                string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuid);
                Object catalog = AssetDatabase.LoadMainAssetAtPath(catalogPath);
                SerializedObject so = new SerializedObject(catalog);
                SerializedProperty entries = so.FindProperty("entries");
                System.Text.StringBuilder report = new System.Text.StringBuilder();
                report.AppendLine($"[R0] Catalog {catalogPath}:");
                for (int i = 0; entries != null && i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    string id = entry.FindPropertyRelative("id").stringValue;
                    SerializedProperty prefab = entry.FindPropertyRelative("prefab");
                    Object value = prefab.objectReferenceValue;
                    if (value != null)
                    {
                        report.AppendLine($"  ok   {id} -> {AssetDatabase.GetAssetPath(value)} ({value.GetType().Name})");
                        continue;
                    }

                    report.AppendLine($"  NULL {id}");
                    // Find the prefab the entry should point at, from the YAML of the catalog file.
                    string yaml = File.ReadAllText(catalogPath);
                    int at = yaml.IndexOf("id: " + id, System.StringComparison.Ordinal);
                    int guidAt = at < 0 ? -1 : yaml.IndexOf("guid: ", at, System.StringComparison.Ordinal);
                    string guid = guidAt < 0 ? string.Empty : yaml.Substring(guidAt + 6, 32);
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    report.AppendLine($"       file in the catalog: {path} (guid {guid})");
                    GameObject root = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (root == null)
                    {
                        report.AppendLine("       the prefab does not load.");
                        continue;
                    }

                    foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>())
                    {
                        if (behaviour == null)
                        {
                            report.AppendLine("       a script on the root is MISSING (cannot be loaded).");
                            continue;
                        }

                        MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                        System.Type linked = script != null ? script.GetClass() : null;
                        report.AppendLine($"       script {AssetDatabase.GetAssetPath(script)} -> class {(linked != null ? linked.FullName : "NONE")}, component {behaviour.GetType().FullName}");
                    }
                }

                Debug.Log(report.ToString());
            }
        }

        // A file's new path: the renamed folders applied to its folder, then its new name.
        private static string NewPath(string oldPath, string newFileName)
        {
            string folder = Path.GetDirectoryName(oldPath).Replace('\\', '/');
            for (int i = 0; i < Folders.GetLength(0); i++)
            {
                string oldFolder = Root + Folders[i, 0];
                if (folder == oldFolder) folder = Path.GetDirectoryName(oldFolder).Replace('\\', '/') + "/" + Folders[i, 1];
            }

            return folder + "/" + newFileName;
        }

        private static int PendingCount()
        {
            int count = 0;
            for (int i = 0; i < Files.GetLength(0); i++) if (File.Exists(Root + Files[i, 0])) count++;
            for (int i = 0; i < Folders.GetLength(0); i++) if (Directory.Exists(Root + Folders[i, 0])) count++;
            return count;
        }

        private static void Rename(string oldPath, string newName, string newFileName, bool folder, ref int renamed, List<string> problems)
        {
            bool exists = folder ? Directory.Exists(oldPath) : File.Exists(oldPath);
            if (!exists) return;
            string newPath = Path.GetDirectoryName(oldPath).Replace('\\', '/') + "/" + newFileName;
            if (folder ? Directory.Exists(newPath) : File.Exists(newPath))
            {
                problems.Add($"{newPath} already exists, so {oldPath} was kept.");
                return;
            }

            string error = AssetDatabase.RenameAsset(oldPath, newName);
            if (string.IsNullOrEmpty(error)) renamed++;
            else problems.Add($"{oldPath}: {error}");
        }

        private static void UpdateBuildScenes()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool changed = false;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path != OldScene) continue;
                scenes[i] = new EditorBuildSettingsScene(NewScene, scenes[i].enabled);
                changed = true;
            }

            if (changed) EditorBuildSettings.scenes = scenes;
        }

        // The old prefabs nothing else uses (the scene, catalogs and other prefabs are checked).
        private static List<string> UnusedBasePrefabs()
        {
            List<string> unused = new List<string>();
            if (!Directory.Exists(BasePrefabs)) return unused;
            List<string> users = new List<string>();
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/") || path.StartsWith(BasePrefabs + "/")) continue;
                if (path.EndsWith(".unity") || path.EndsWith(".prefab") || path.EndsWith(".asset")) users.Add(path);
            }

            HashSet<string> used = new HashSet<string>(AssetDatabase.GetDependencies(users.ToArray(), true));
            foreach (string file in Directory.GetFiles(BasePrefabs))
            {
                string path = file.Replace('\\', '/');
                if (path.EndsWith(".meta")) continue;
                if (!used.Contains(path)) unused.Add(path);
            }

            unused.Sort();
            return unused;
        }
    }
}
