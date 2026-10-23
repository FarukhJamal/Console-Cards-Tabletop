using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Content
{
    /// <summary>
    /// Card pictures stay sharp on the table (owner, 2026-10-23: "everything looks pixelated, low quality"). Every
    /// texture (not sprites) in the card art folders imports at up to 4096 px with high-quality compression,
    /// trilinear filtering, 8x anisotropic filtering and slightly sharper mipmaps, so a card seen small or at an angle
    /// keeps its detail. It applies whenever a picture is imported: reimport the folders once (right-click the folder >
    /// Reimport) to update the pictures already there.
    /// </summary>
    internal sealed class CardArtImportSettings : AssetPostprocessor
    {
        private const int CardArtMaximumSize = 4096;
        private const int CardArtAnisoLevel = 8;
        private const float CardArtMipMapBias = -0.5f;

        private static readonly string[] CardArtFolders =
        {
            "Assets/ConsoleCards/Content/Art/",
            "Assets/3D Models/CARD/",
        };

        private void OnPreprocessTexture()
        {
            if (!IsCardArt(assetPath) || !(assetImporter is TextureImporter importer)) return;
            if (importer.textureType != TextureImporterType.Default) return; // UI sprites keep their own settings

            importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, CardArtMaximumSize);
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.mipMapBias = CardArtMipMapBias;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = CardArtAnisoLevel;
        }

        private static bool IsCardArt(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string normalised = path.Replace('\\', '/');
            for (int i = 0; i < CardArtFolders.Length; i++)
            {
                if (normalised.StartsWith(CardArtFolders[i], System.StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
