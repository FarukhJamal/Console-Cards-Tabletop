using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.Art
{
    /// <summary>Where a picture came from, in lookup order (doc 24): the first layer with the key wins.</summary>
    public enum ArtLayer
    {
        PlayerUpload = 0,
        GamePack = 1,
        BuiltIn = 2,
    }

    /// <summary>
    /// Art lookup and texture cache keyed by art key (doc 24, A2). Pictures are found by key in the player-upload
    /// layer, then the active game pack, then the built-in art, then the caller's own texture. Textures this
    /// library loaded from bytes are owned by it and destroyed when replaced, removed or cleared. Listeners hear
    /// <see cref="Changed"/> first (to re-apply art), then <see cref="TextureReleased"/> for any texture that is
    /// about to be destroyed (to drop materials built from it). Nothing here runs per frame.
    /// </summary>
    public sealed class ArtLibrary
    {
        private readonly Dictionary<string, Texture2D>[] layers =
        {
            new Dictionary<string, Texture2D>(StringComparer.Ordinal),
            new Dictionary<string, Texture2D>(StringComparer.Ordinal),
            new Dictionary<string, Texture2D>(StringComparer.Ordinal),
        };

        private readonly HashSet<Texture2D> owned = new HashSet<Texture2D>();
        private readonly List<Texture2D> pendingRelease = new List<Texture2D>();
        private int batchDepth;
        private bool batchChanged;

        /// <summary>A key changed (null: many keys, for example a whole layer was cleared).</summary>
        public event Action<string> Changed;

        /// <summary>A texture owned by the library is about to be destroyed.</summary>
        public event Action<Texture2D> TextureReleased;

        /// <summary>Goes up on every change, so callers can tell cheaply whether anything moved.</summary>
        public int Revision { get; private set; }

        /// <summary>
        /// Groups many changes (a whole pack) into one: listeners hear a single <see cref="Changed"/>(null) at the
        /// end, and textures replaced meanwhile are released only after that. Batches nest.
        /// </summary>
        public void BeginBatch()
        {
            batchDepth++;
        }

        public void EndBatch()
        {
            if (batchDepth == 0) return;
            batchDepth--;
            if (batchDepth > 0) return;
            if (batchChanged)
            {
                batchChanged = false;
                Changed?.Invoke(null);
            }

            for (int i = 0; i < pendingRelease.Count; i++)
            {
                ReleaseIfUnused(pendingRelease[i]);
            }

            pendingRelease.Clear();
        }

        /// <summary>Lower case, forward slashes, no leading slash or surrounding spaces. Empty for no key.</summary>
        public static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return key.Trim().Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        }

        /// <summary>The picture for a key, or <paramref name="fallback"/> when no layer has it.</summary>
        public Texture2D Resolve(string key, Texture2D fallback)
        {
            return TryResolve(key, out Texture2D texture, out _) ? texture : fallback;
        }

        public bool TryResolve(string key, out Texture2D texture, out ArtLayer layer)
        {
            string normalized = NormalizeKey(key);
            if (normalized.Length > 0)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i].TryGetValue(normalized, out texture) && texture != null)
                    {
                        layer = (ArtLayer)i;
                        return true;
                    }
                }
            }

            texture = null;
            layer = ArtLayer.BuiltIn;
            return false;
        }

        /// <summary>Built-in art from the game's definitions. Quiet: no change event, never owned.</summary>
        public void RegisterBuiltIn(string key, Texture2D texture)
        {
            string normalized = NormalizeKey(key);
            if (normalized.Length == 0 || texture == null)
            {
                return;
            }

            layers[(int)ArtLayer.BuiltIn][normalized] = texture;
        }

        public void ClearBuiltIn()
        {
            layers[(int)ArtLayer.BuiltIn].Clear();
            Revision++;
        }

        /// <summary>
        /// Puts a picture under a key in the upload or pack layer. <paramref name="takeOwnership"/> hands the
        /// texture to the library (it is destroyed when replaced or removed).
        /// </summary>
        public void Set(ArtLayer layer, string key, Texture2D texture, bool takeOwnership)
        {
            if (layer == ArtLayer.BuiltIn)
            {
                throw new ArgumentException("Built-in art is registered with RegisterBuiltIn.", nameof(layer));
            }

            string normalized = NormalizeKey(key);
            if (normalized.Length == 0)
            {
                throw new ArgumentException("An art key is required.", nameof(key));
            }

            if (texture == null)
            {
                Remove(layer, normalized);
                return;
            }

            Dictionary<string, Texture2D> map = layers[(int)layer];
            map.TryGetValue(normalized, out Texture2D previous);
            map[normalized] = texture;
            if (takeOwnership)
            {
                owned.Add(texture);
            }

            Revision++;
            Notify(normalized);
            if (previous != null && !ReferenceEquals(previous, texture))
            {
                ReleaseIfUnused(previous);
            }
        }

        public bool Remove(ArtLayer layer, string key)
        {
            if (layer == ArtLayer.BuiltIn)
            {
                return false;
            }

            string normalized = NormalizeKey(key);
            Dictionary<string, Texture2D> map = layers[(int)layer];
            if (!map.TryGetValue(normalized, out Texture2D previous))
            {
                return false;
            }

            map.Remove(normalized);
            Revision++;
            Notify(normalized);
            ReleaseIfUnused(previous);
            return true;
        }

        public void ClearLayer(ArtLayer layer)
        {
            if (layer == ArtLayer.BuiltIn)
            {
                ClearBuiltIn();
                return;
            }

            Dictionary<string, Texture2D> map = layers[(int)layer];
            if (map.Count == 0)
            {
                return;
            }

            List<Texture2D> previous = new List<Texture2D>(map.Values);
            map.Clear();
            Revision++;
            Notify(null);
            for (int i = 0; i < previous.Count; i++)
            {
                ReleaseIfUnused(previous[i]);
            }
        }

        /// <summary>
        /// Makes a texture from PNG or JPG bytes (for packs and uploads). Null when the bytes are not a picture.
        /// The caller passes it to <see cref="Set"/> with ownership, or destroys it.
        /// </summary>
        public static Texture2D LoadTexture(byte[] bytes, string key)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                name = NormalizeKey(key),
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return texture;
        }

        /// <summary>Destroys every owned texture and empties all layers.</summary>
        public void Dispose()
        {
            foreach (Texture2D texture in owned)
            {
                if (texture != null)
                {
                    TextureReleased?.Invoke(texture);
                    UnityEngine.Object.Destroy(texture);
                }
            }

            owned.Clear();
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i].Clear();
            }

            Changed = null;
            TextureReleased = null;
        }

        private void Notify(string key)
        {
            if (batchDepth > 0)
            {
                batchChanged = true;
                return;
            }

            Changed?.Invoke(key);
        }

        // An owned texture still used under another key or layer stays alive.
        private void ReleaseIfUnused(Texture2D texture)
        {
            if (texture == null || !owned.Contains(texture))
            {
                return;
            }

            if (batchDepth > 0)
            {
                if (!pendingRelease.Contains(texture)) pendingRelease.Add(texture);
                return;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                foreach (Texture2D used in layers[i].Values)
                {
                    if (ReferenceEquals(used, texture))
                    {
                        return;
                    }
                }
            }

            owned.Remove(texture);
            TextureReleased?.Invoke(texture);
            UnityEngine.Object.Destroy(texture);
        }
    }
}
