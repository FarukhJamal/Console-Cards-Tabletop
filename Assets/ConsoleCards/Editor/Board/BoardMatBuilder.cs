using System;
using System.IO;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Editor.Toolbox;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConsoleCards.Editor.Board
{
    /// <summary>
    /// Creates the game board v2 mat (doc 22 §15): the BoardMat prefab (frame, surface collider, one space template),
    /// its three materials and a Base Box entry "Board Mat" shown in the Toolbox. Only what is missing is made;
    /// nothing existing is overwritten. Restyle the prefab's materials freely afterwards.
    /// </summary>
    public static class BoardMatBuilder
    {
        public const string PrefabPath = "Assets/ConsoleCards/Content/Prefabs/Final/World/BoardMat.prefab";
        private const string MaterialFolder = "Assets/ConsoleCards/Content/Materials/Board";

        [MenuItem("Console Cards/Board/Create Missing Board Mat")]
        public static void CreateMissing()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool made = false;
            if (prefab == null)
            {
                prefab = BuildPrefab();
                made = prefab != null;
            }

            bool added = prefab != null && AddBaseBoxEntry(prefab);
            if (added) ComponentToolboxBuilder.SyncFromLibrary();
            Debug.Log($"[Board] Board Mat: prefab {(made ? "made" : "kept")}, Base Box entry {(added ? "added" : "kept")}.");
        }

        private static GameObject BuildPrefab()
        {
            Material frameMaterial = EnsureMaterial("BoardFrame", new Color(0.93f, 0.42f, 0.12f));
            Material edgeMaterial = EnsureMaterial("BoardSpaceEdge", new Color(0.88f, 0.2f, 0.08f));
            Material fillMaterial = EnsureMaterial("BoardSpaceFill", new Color(0.24f, 0.23f, 0.24f));
            if (frameMaterial == null || edgeMaterial == null || fillMaterial == null) return null;

            GameObject root = new GameObject("BoardMat");
            try
            {
                BoxCollider surface = root.AddComponent<BoxCollider>();
                root.AddComponent<PhysicalTabletopSurface>();
                GameBoardMatView view = root.AddComponent<GameBoardMatView>();

                GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "Frame";
                Object.DestroyImmediate(frame.GetComponent<Collider>());
                frame.transform.SetParent(root.transform, false);
                frame.GetComponent<MeshRenderer>().sharedMaterial = frameMaterial;

                GameObject spaces = new GameObject("Spaces");
                spaces.transform.SetParent(root.transform, false);
                GameObject template = new GameObject("SpaceTemplate");
                template.transform.SetParent(spaces.transform, false);
                AddQuad(template.transform, "Edge", 0f, Vector2.one, edgeMaterial);
                AddQuad(template.transform, "Fill", 0.0005f, new Vector2(0.9f, 0.93f), fillMaterial);
                template.SetActive(false);

                SerializedObject so = new SerializedObject(view);
                so.FindProperty("surface").objectReferenceValue = surface;
                so.FindProperty("frame").objectReferenceValue = frame.transform;
                so.FindProperty("spacesRoot").objectReferenceValue = spaces.transform;
                so.FindProperty("spaceTemplate").objectReferenceValue = template;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Size the frame and collider for the standard 10 × 5 mat; spaces are laid out in the game.
                BoardGrid grid = BoardGrid.Standard;
                frame.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                frame.transform.localScale = new Vector3(grid.Width, 0.04f, grid.Depth);
                surface.center = new Vector3(0f, 0.02f, 0f);
                surface.size = new Vector3(grid.Width, 0.04f, grid.Depth);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddQuad(Transform parent, string name, float height, Vector2 size, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, height, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material EnsureMaterial(string name, Color colour)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Board] The URP Lit shader was not found; the Board Mat was not made.");
                return null;
            }

            Material material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", colour);
            material.color = colour;
            material.SetFloat("_Smoothness", 0.2f);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static bool AddBaseBoxEntry(GameObject prefab)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(ComponentLibrary));
            ComponentLibrary library = guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<ComponentLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
            if (library == null || library.BaseBox == null)
            {
                Debug.LogWarning("[Board] No Component Library with a Base Box found.");
                return false;
            }

            foreach (ComponentCatalogEntry existing in library.BaseBox.Entries)
            {
                if (existing != null && existing.Prefab == prefab) return false;
            }

            SerializedObject so = new SerializedObject(library.BaseBox);
            SerializedProperty entries = so.FindProperty("entries");
            int index = entries.arraySize;
            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString();
            entry.FindPropertyRelative("displayName").stringValue = "Board Mat";
            entry.FindPropertyRelative("description").stringValue = "A board with card spaces; boards join edge to edge";
            entry.FindPropertyRelative("kind").intValue = (int)ComponentCatalogKind.GameBoard;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("icon").objectReferenceValue = null;
            entry.FindPropertyRelative("showInToolbox").boolValue = true;
            entry.FindPropertyRelative("toolboxOrder").intValue = 9;
            entry.FindPropertyRelative("definitions").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library.BaseBox);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
