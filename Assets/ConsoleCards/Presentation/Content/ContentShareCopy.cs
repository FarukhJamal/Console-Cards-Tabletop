using System;
using System.IO;
using UnityEngine;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>
    /// SHARE-COPY (owner, 2026-11-27): the picture a host sends to a room is a smaller "share copy" of its own: a
    /// high-quality JPG when every pixel is fully opaque, else the original PNG; its size is rounded down to a
    /// multiple of 4 (cropping at most 3 pixels from the right and bottom) so the guest can compress it for the
    /// graphics card. The copy is made once and kept next to the original as "&lt;file&gt;.share" in the host's own
    /// profile (never in the room's memory), made again only when the original is newer. Main thread only.
    /// </summary>
    public static class ContentShareCopy
    {
        public const string Suffix = ".share";
        private const int JpgQuality = 90;

        public static bool IsPicture(string path)
        {
            string extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }

        public static bool IsShareFile(string path) =>
            path != null && path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The file to send for a picture: its share copy (made now when it is missing or older than the picture), or
        /// the picture itself when no copy can be made. Pictures held for a room are already share copies.
        /// </summary>
        public static string Prepare(string picturePath)
        {
            if (!IsPicture(picturePath) || ContentIO.IsRoomPath(picturePath)) return picturePath;
            string share = picturePath + Suffix;
            try
            {
                if (File.Exists(share) && File.GetLastWriteTimeUtc(share) > File.GetLastWriteTimeUtc(picturePath)) return share;
                byte[] made = Make(File.ReadAllBytes(picturePath));
                if (made == null) return picturePath;
                File.WriteAllBytes(share, made);
                return share;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Content] A smaller copy of a picture to share could not be made: {exception.Message}");
                return picturePath;
            }
        }

        /// <summary>
        /// A picture was just stored in the host's own profile: its old share copy goes and the new one is made now, so it
        /// is ready before a room starts. The picture's time is set to now, because a copied file keeps the time of the
        /// file it came from, which could be older than the share copy it replaces.
        /// </summary>
        public static void AfterStore(string storedPath, bool makeCopy)
        {
            try
            {
                File.SetLastWriteTimeUtc(storedPath, DateTime.UtcNow);
                Delete(storedPath);
                if (makeCopy) Prepare(storedPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Content] A smaller copy of a picture to share could not be made: {exception.Message}");
            }
        }

        /// <summary>The picture's share copy goes (the picture was deleted or replaced).</summary>
        public static void Delete(string picturePath)
        {
            try
            {
                string share = picturePath + Suffix;
                if (File.Exists(share)) File.Delete(share);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Content] A smaller copy of a picture could not be removed: {exception.Message}");
            }
        }

        // The bytes to send for a picture; null when the bytes are not a picture.
        private static byte[] Make(byte[] original)
        {
            Texture2D picture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!picture.LoadImage(original, false)) return null;
                int width = picture.width;
                int height = picture.height;
                int newWidth = width >= 4 ? width - (width % 4) : width;
                int newHeight = height >= 4 ? height - (height % 4) : height;
                bool crop = newWidth != width || newHeight != height;
                Color32[] all = picture.GetPixels32();
                Color32[] pixels = all;
                if (crop)
                {
                    // Rows run from the bottom up: dropping the bottom rows is dropping rows from the start.
                    pixels = new Color32[newWidth * newHeight];
                    int firstRow = height - newHeight;
                    for (int row = 0; row < newHeight; row++)
                        Array.Copy(all, (firstRow + row) * width, pixels, row * newWidth, newWidth);
                }

                bool opaque = true;
                for (int i = 0; i < pixels.Length && opaque; i++) opaque = pixels[i].a == 255;
                if (!opaque && !crop) return original; // the original PNG as it is
                Texture2D shaped = picture;
                try
                {
                    if (crop)
                    {
                        shaped = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
                        shaped.SetPixels32(pixels);
                        shaped.Apply(false);
                    }

                    return opaque ? shaped.EncodeToJPG(JpgQuality) : shaped.EncodeToPNG();
                }
                finally
                {
                    if (!ReferenceEquals(shaped, picture)) Discard(shaped);
                }
            }
            finally
            {
                Discard(picture);
            }
        }

        private static void Discard(Texture2D texture)
        {
            if (texture == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
