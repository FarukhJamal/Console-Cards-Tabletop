using System;
using System.IO;
using UnityEngine;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>
    /// Card studio (board 10, owner 2026-10-30): a card picture is never stretched. A picture of another shape is made
    /// the card's shape when it is chosen: cropped to fill (the middle is kept) or fitted with a border. The shaped
    /// copy is written to the temporary folder and then stored in the profile like any chosen picture.
    /// </summary>
    public static class CardPictureShaper
    {
        /// <summary>A picture within this of the card's width : height is already the card's shape.</summary>
        public const float ShapeTolerance = 0.02f;

        private static readonly Color32 BorderColour = new Color32(16, 19, 20, 255);

        /// <summary>
        /// The picture at <paramref name="sourcePath"/> in the card's shape (<paramref name="aspect"/> = width / height).
        /// <paramref name="shapedPath"/> is the source itself when it already has the shape, else a new PNG.
        /// </summary>
        public static bool TryShape(string sourcePath, float aspect, bool fit, string name, out string shapedPath,
            out bool reshaped, out string error)
        {
            shapedPath = sourcePath;
            reshaped = false;
            error = string.Empty;
            Texture2D picture = null;
            Texture2D shaped = null;
            try
            {
                picture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!picture.LoadImage(File.ReadAllBytes(sourcePath)))
                {
                    error = "the file is not a PNG or JPG picture";
                    return false;
                }

                int width = picture.width;
                int height = picture.height;
                if (aspect <= 0f || height <= 0 || Mathf.Abs(((float)width / height) - aspect) <= ShapeTolerance) return true;

                shaped = fit ? Fit(picture, aspect) : Crop(picture, aspect);
                string folder = Path.Combine(UnityEngine.Application.temporaryCachePath, "card-pictures");
                Directory.CreateDirectory(folder);
                shapedPath = Path.Combine(folder, (string.IsNullOrEmpty(name) ? "picture" : name) + ".png");
                File.WriteAllBytes(shapedPath, shaped.EncodeToPNG());
                reshaped = true;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                shapedPath = sourcePath;
                return false;
            }
            finally
            {
                if (picture != null) UnityEngine.Object.Destroy(picture);
                if (shaped != null) UnityEngine.Object.Destroy(shaped);
            }
        }

        // The middle of the picture in the card's shape.
        private static Texture2D Crop(Texture2D picture, float aspect)
        {
            int width = picture.width;
            int height = picture.height;
            int cropWidth = width;
            int cropHeight = height;
            if ((float)width / height > aspect) cropWidth = Mathf.Max(1, Mathf.RoundToInt(height * aspect));
            else cropHeight = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
            Texture2D shaped = new Texture2D(cropWidth, cropHeight, TextureFormat.RGBA32, false);
            shaped.SetPixels(picture.GetPixels((width - cropWidth) / 2, (height - cropHeight) / 2, cropWidth, cropHeight));
            shaped.Apply(false);
            return shaped;
        }

        // The whole picture in the middle of a card-shaped page, the rest a dark border.
        private static Texture2D Fit(Texture2D picture, float aspect)
        {
            int width = picture.width;
            int height = picture.height;
            int pageWidth = width;
            int pageHeight = height;
            if ((float)width / height > aspect) pageHeight = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
            else pageWidth = Mathf.Max(1, Mathf.RoundToInt(height * aspect));
            Texture2D shaped = new Texture2D(pageWidth, pageHeight, TextureFormat.RGBA32, false);
            Color32[] page = new Color32[pageWidth * pageHeight];
            for (int i = 0; i < page.Length; i++) page[i] = BorderColour;
            shaped.SetPixels32(page);
            shaped.SetPixels((pageWidth - width) / 2, (pageHeight - height) / 2, width, height, picture.GetPixels());
            shaped.Apply(false);
            return shaped;
        }
    }
}
