using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// BRAND-1 (owner, 2026-10-30): the game's own pictures. The CONSOLE CARDS plate is the main menu's title (and the
    /// intro's), and the main menu has its picture behind it (a table with the Console Cards box), darkened on the
    /// left where the menu is. The pictures are in Content/UI/Board.
    /// </summary>
    public static partial class BoardScreensBuilder
    {
        internal const string LogoPath = "Assets/ConsoleCards/Content/UI/Board/ConsoleCardsLogo.png";
        private const string MenuPicturePath = "Assets/ConsoleCards/Content/UI/Board/MenuBackground.jpg";
        private const string MenuShadePath = "Assets/ConsoleCards/Content/UI/Board/MenuShade.png";

        /// <summary>The CONSOLE CARDS plate as a sprite, or null when its picture is missing.</summary>
        internal static Sprite Logo()
        {
            if (!System.IO.File.Exists(LogoPath)) return null;
            UiKit.ConfigureTexture(LogoPath, Vector4.zero);
            return AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        }

        // A big picture as a sprite: compressed, at most 4096 px.
        private static Sprite Picture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // The main menu's picture in the front end's background (FrontEndView shows it on Home only). Added once.
        private static void MenuPicture()
        {
            string path = FrontEndBuilder.PrefabPath;
            if (!System.IO.File.Exists(path)) return;
            Sprite picture = Picture(MenuPicturePath);
            if (picture == null) return;
            if (System.IO.File.Exists(MenuShadePath)) UiKit.ConfigureTexture(MenuShadePath, Vector4.zero);
            Sprite shade = AssetDatabase.LoadAssetAtPath<Sprite>(MenuShadePath);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform background = root.transform.Find("Background");
                if (background == null || background.Find("Picture") != null) return;
                RectTransform rect = (RectTransform)new GameObject("Picture", typeof(RectTransform)).transform;
                rect.gameObject.layer = 5;
                rect.SetParent(background, false);
                UiKit.Stretch(rect);
                Image image = rect.gameObject.AddComponent<Image>();
                image.sprite = picture;
                image.raycastTarget = false;
                AspectRatioFitter fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // fills the screen, cropped not stretched
                fitter.aspectRatio = picture.rect.width / Mathf.Max(1f, picture.rect.height);
                if (shade != null)
                {
                    RectTransform shadeRect = (RectTransform)new GameObject("Shade", typeof(RectTransform)).transform;
                    shadeRect.gameObject.layer = 5;
                    shadeRect.SetParent(background, false);
                    UiKit.Stretch(shadeRect);
                    Image shadeImage = shadeRect.gameObject.AddComponent<Image>();
                    shadeImage.sprite = shade;
                    shadeImage.raycastTarget = false;
                    shadeRect.SetParent(rect, true); // shown and hidden with the picture
                    UiKit.Stretch(shadeRect);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
