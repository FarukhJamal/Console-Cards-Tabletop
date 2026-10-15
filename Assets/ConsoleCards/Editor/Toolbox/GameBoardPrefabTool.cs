using System;
using System.IO;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Prototype;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConsoleCards.Editor.Toolbox
{
    /// <summary>
    /// Saves the scene's game board, exactly as authored, as the catalog's Game Board prefab (doc 22, C3c) and adds
    /// it to the Base Box (not shown in the Toolbox). An existing prefab is never overwritten.
    /// </summary>
    public static class GameBoardPrefabTool
    {
        public const string PrefabPath = "Assets/ConsoleCards/Content/Prefabs/Final/World/GameBoard.prefab";

        [MenuItem("Console Cards/Toolbox/Create Game Board Prefab from Scene")]
        public static void CreateFromScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                TabletopPrototypeComposition composition = Object.FindFirstObjectByType<TabletopPrototypeComposition>();
                if (composition == null)
                {
                    Debug.LogWarning("[Board] Open the table scene first (it has the composition and the board).");
                    return;
                }

                SerializedObject fields = new SerializedObject(composition);
                Collider surface = fields.FindProperty("gameBoardPhysicalSurface").objectReferenceValue as Collider;
                Transform visual = fields.FindProperty("gameBoardVisualRoot").objectReferenceValue as Transform;
                Material material = fields.FindProperty("gameBoardVisualMaterial").objectReferenceValue as Material;
                if (surface == null || visual == null || !visual.IsChildOf(surface.transform))
                {
                    Debug.LogWarning("[Board] The composition has no board surface with its visual as a child.");
                    return;
                }

                string visualPath = AnimationUtility.CalculateTransformPath(visual, surface.transform);
                GameObject copy = Object.Instantiate(surface.gameObject);
                try
                {
                    copy.name = "GameBoard";
                    copy.SetActive(true);
                    copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    Transform copiedVisual = string.IsNullOrEmpty(visualPath) ? copy.transform : copy.transform.Find(visualPath);
                    GameBoardView view = copy.AddComponent<GameBoardView>();
                    SerializedObject so = new SerializedObject(view);
                    so.FindProperty("surface").objectReferenceValue = copy.GetComponent(surface.GetType());
                    so.FindProperty("visualRoot").objectReferenceValue = copiedVisual;
                    so.FindProperty("visualMaterial").objectReferenceValue = material;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                    prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(copy);
                }

                Debug.Log($"[Board] Saved the scene board as {PrefabPath}.");
            }
            else
            {
                Debug.Log($"[Board] Kept {PrefabPath} (already exists; edit it there).");
            }

            AddBaseBoxEntry(prefab);
        }

        private static void AddBaseBoxEntry(GameObject prefab)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(ComponentLibrary));
            ComponentLibrary library = guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<ComponentLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
            if (library == null || library.BaseBox == null)
            {
                Debug.LogWarning("[Board] No Component Library with a Base Box found.");
                return;
            }

            if (library.BaseBox.TryGetFirst(ComponentCatalogKind.GameBoard, out _))
            {
                Debug.Log("[Board] The Base Box already has a Game Board entry.");
                return;
            }

            SerializedObject so = new SerializedObject(library.BaseBox);
            SerializedProperty entries = so.FindProperty("entries");
            int index = entries.arraySize;
            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString();
            entry.FindPropertyRelative("displayName").stringValue = "Game Board";
            entry.FindPropertyRelative("description").stringValue = "The game's board";
            entry.FindPropertyRelative("kind").intValue = (int)ComponentCatalogKind.GameBoard;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("icon").objectReferenceValue = null;
            entry.FindPropertyRelative("showInToolbox").boolValue = false;
            entry.FindPropertyRelative("toolboxOrder").intValue = 100;
            entry.FindPropertyRelative("definitions").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library.BaseBox);
            AssetDatabase.SaveAssets();
            Debug.Log("[Board] Added the Game Board entry to the Base Box (not shown in the Toolbox).");
        }
    }
}
