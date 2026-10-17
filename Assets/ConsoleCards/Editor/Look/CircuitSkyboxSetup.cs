using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConsoleCards.Editor.Look
{
    /// <summary>
    /// Console Cards > Look > Use Circuit Skybox (doc 30, C0 quick win Q2; client, 2026-10-15): replaces the grey
    /// background with a dark teal circuit board whose faint bright-teal data lines fade out above and below.
    /// Sets the picture's import settings, makes (or updates) the panoramic skybox material, puts it in the open
    /// scene's Lighting settings and switches the scene's cameras to draw the skybox. Undo puts the cameras back;
    /// save the scene afterwards.
    /// </summary>
    public static class CircuitSkyboxSetup
    {
        public const string TexturePath = "Assets/ConsoleCards/Content/Environment/CircuitSkybox.png";
        public const string MaterialPath = "Assets/ConsoleCards/Content/Environment/CircuitSkybox.mat";
        private const string Title = "Use Circuit Skybox";

        [MenuItem("Console Cards/Look/Use Circuit Skybox")]
        public static void Use()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog(Title, $"The skybox picture is missing: {TexturePath}.", "OK");
                return;
            }

            // A 360° picture: wraps left to right, no seams top and bottom, no mipmap blur at the horizon.
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                EditorUtility.DisplayDialog(Title, "Unity's Skybox/Panoramic shader was not found.", "OK");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CircuitSkybox" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = shader;
            material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Mapping")) material.SetFloat("_Mapping", 1f); // latitude-longitude layout
            if (material.HasProperty("_ImageType")) material.SetFloat("_ImageType", 0f); // 360 degrees
            if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", 1f);
            if (material.HasProperty("_Rotation")) material.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            RenderSettings.skybox = material;
            DynamicGI.UpdateEnvironment();
            int cameras = 0;
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (EditorUtility.IsPersistent(camera) || camera.targetTexture != null) continue;
                if (camera.clearFlags == CameraClearFlags.Skybox) continue;
                Undo.RecordObject(camera, Title);
                camera.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(camera);
                cameras++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                Title,
                "The circuit skybox is on.\n\n"
                + (cameras > 0 ? $"• {cameras} camera(s) now draw the skybox.\n" : string.Empty)
                + "• Save the scene (Ctrl+S).\n"
                + "• To try another picture, replace CircuitSkybox.png and run this again.",
                "OK");
        }
    }
}
