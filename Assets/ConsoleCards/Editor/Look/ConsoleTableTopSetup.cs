using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Editor.Look
{
    /// <summary>
    /// Console Cards > Look > Use Console Table Top (owner, 2026-10-27, from the V5 reference): the grey console-plastic
    /// top (ConsoleTableTop.png: diagonal bands, speaker-grille dimples near the edges) and the orange rim.
    /// Makes (or resets) two plain URP Lit materials in Content/Environment and puts them on the Classic table prefab:
    /// ConsoleTableTop on PlayingArea and ConsoleTableEdge on Sides (the rim); legs and lower frame are not touched.
    /// Every built table (rectangle, round, hexagon …) copies the top at start and uses the rim material for its edge
    /// (GeneratedTable.EdgeMaterial), so both materials set every table. Change the orange or the gloss on
    /// ConsoleTableEdge in the Inspector. Console Cards > Look > Use Old Table Top puts the old materials back.
    /// LOOK-2c: the materials are made fresh from the Lit shader (not copied from the model's materials) and the
    /// picture is imported before it is used, so the top is never left blank.
    /// </summary>
    public static class ConsoleTableTopSetup
    {
        public const string TexturePath = "Assets/ConsoleCards/Content/Environment/ConsoleTableTop.png";
        public const string MaterialPath = "Assets/ConsoleCards/Content/Environment/ConsoleTableTop.mat";
        public const string EdgeMaterialPath = "Assets/ConsoleCards/Content/Environment/ConsoleTableEdge.mat";
        public const string OldMaterialPath = "Assets/3D Models/Square Table/Materials/TableMat.mat";
        public const string OldEdgeMaterialPath = "Assets/3D Models/Square Table/Materials/model.mat";
        public const string PrefabPath = "Assets/ConsoleCards/Content/Tables/Classic/Classic.prefab";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string PlayingAreaName = "PlayingArea"; // TableModel.PlayingAreaName
        private const string SidesName = "Sides"; // the Classic table's rim
        private const string Title = "Console Table Top";

        private static readonly Color EdgeColour = new Color(0.929f, 0.380f, 0.004f, 1f); // #ED6101 (V5 orange)

        [MenuItem("Console Cards/Look/Use Console Table Top")]
        public static void Use()
        {
            Shader lit = Shader.Find(LitShaderName);
            if (lit == null)
            {
                EditorUtility.DisplayDialog(Title, $"The {LitShaderName} shader was not found.", "OK");
                return;
            }

            Texture2D texture = ImportPicture();
            if (texture == null)
            {
                EditorUtility.DisplayDialog(Title, $"The table picture could not be loaded: {TexturePath}.", "OK");
                return;
            }

            Material top = PlainLit(MaterialPath, "ConsoleTableTop", lit);
            top.SetTexture("_BaseMap", texture);
            top.SetTextureScale("_BaseMap", Vector2.one);
            top.SetTextureOffset("_BaseMap", Vector2.zero);
            top.SetColor("_BaseColor", Color.white);
            top.SetFloat("_Smoothness", 0.35f); // satin plastic
            Save(top);

            Material edge = PlainLit(EdgeMaterialPath, "ConsoleTableEdge", lit);
            edge.SetColor("_BaseColor", EdgeColour);
            edge.SetFloat("_Smoothness", 0.45f); // glossier plastic rim
            Save(edge);

            if (!SetPart(PlayingAreaName, top) || !SetPart(SidesName, edge)) return;
            int swapped = SwapInOpenScenes(AssetDatabase.LoadAssetAtPath<Material>(OldMaterialPath), top);
            Debug.Log($"[{Title}] Classic PlayingArea uses {MaterialPath} and Sides {EdgeMaterialPath}"
                + (swapped > 0 ? $"; {swapped} table surface(s) in the open scene too (save the scene)." : ".")
                + " Press Play: built tables take both.");
        }

        [MenuItem("Console Cards/Look/Use Old Table Top")]
        public static void UseOld()
        {
            Material old = AssetDatabase.LoadAssetAtPath<Material>(OldMaterialPath);
            Material oldEdge = AssetDatabase.LoadAssetAtPath<Material>(OldEdgeMaterialPath);
            if (old == null || oldEdge == null)
            {
                EditorUtility.DisplayDialog(Title, $"The old materials are missing: {OldMaterialPath}, {OldEdgeMaterialPath}.", "OK");
                return;
            }

            if (!SetPart(PlayingAreaName, old) || !SetPart(SidesName, oldEdge)) return;
            int swapped = SwapInOpenScenes(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath), old);
            Debug.Log($"[{Title}] The Classic table uses its old materials again"
                + (swapped > 0 ? $"; {swapped} table surface(s) in the open scene too (save the scene)." : "."));
        }

        // A repeating surface seen at low angles: wraps both ways, mipmaps with sharp anisotropic filtering. Imported
        // at once (not in the background), then loaded.
        private static Texture2D ImportPicture()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            settings.maxTextureSize = 4096;
            importer.SetPlatformTextureSettings(settings);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        // The material at that path (made when missing), reset to plain Lit: opaque, metallic workflow, no keywords,
        // no pictures and no glow left over from earlier versions. Its GUID stays, so references to it keep working.
        private static Material PlainLit(string path, string name, Shader lit)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = lit;
            material.shaderKeywords = new string[0];
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            foreach (string map in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_EmissionMap", "_ParallaxMap" })
            {
                if (material.HasProperty(map)) material.SetTexture(map, null);
            }

            if (material.HasProperty("_WorkflowMode")) material.SetFloat("_WorkflowMode", 1f); // metallic
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f); // opaque
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            return material;
        }

        private static void Save(Material material)
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        // Changes only that one part's renderer material in the Classic prefab; nothing else in it.
        private static bool SetPart(string partName, Material material)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                EditorUtility.DisplayDialog(Title, $"The Classic table prefab is missing: {PrefabPath}.", "OK");
                return false;
            }

            try
            {
                Transform part = FindChild(root.transform, partName);
                MeshRenderer renderer = part != null ? part.GetComponent<MeshRenderer>() : null;
                if (renderer == null)
                {
                    EditorUtility.DisplayDialog(Title, $"The Classic table has no {partName} with a renderer.", "OK");
                    return false;
                }

                if (renderer.sharedMaterial == material) return true;
                renderer.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The scene's own one-piece table surface (used only when the Classic model cannot be placed) follows too.
        private static int SwapInOpenScenes(Material from, Material to)
        {
            if (from == null || to == null || from == to) return 0;
            int swapped = 0;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Material[] materials = renderer.sharedMaterials;
                        bool changed = false;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            if (materials[i] != from) continue;
                            materials[i] = to;
                            changed = true;
                        }

                        if (!changed) continue;
                        Undo.RecordObject(renderer, Title);
                        renderer.sharedMaterials = materials;
                        if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                        EditorSceneManager.MarkSceneDirty(scene);
                        swapped++;
                    }
                }
            }

            return swapped;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChild(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
