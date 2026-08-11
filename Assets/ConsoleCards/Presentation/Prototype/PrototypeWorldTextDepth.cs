using System;
using UnityEngine;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>Applies the shared depth-tested world-text material to prototype TextMesh labels.</summary>
    internal static class PrototypeWorldTextDepth
    {
        private const string MaterialResourceName = "PrototypeWorldText";
        private static Material depthTestedMaterial;

        public static void Apply(TextMesh label)
        {
            if (label == null)
            {
                throw new ArgumentNullException(nameof(label));
            }

            Renderer renderer = label.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException("Prototype TextMesh requires a Renderer.");
            }

            Material sourceMaterial = renderer.sharedMaterial;
            Texture fontTexture = sourceMaterial != null ? sourceMaterial.mainTexture : null;
            if (fontTexture == null && label.font != null && label.font.material != null)
            {
                fontTexture = label.font.material.mainTexture;
            }

            if (depthTestedMaterial == null)
            {
                depthTestedMaterial = Resources.Load<Material>(MaterialResourceName);
                if (depthTestedMaterial == null)
                {
                    throw new InvalidOperationException(
                        $"Missing Resources/{MaterialResourceName} depth-tested world-text material.");
                }
            }

            renderer.sharedMaterial = depthTestedMaterial;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            if (fontTexture != null)
            {
                properties.SetTexture("_MainTex", fontTexture);
            }

            properties.SetColor("_Color", Color.white);
            renderer.SetPropertyBlock(properties);
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = 0;
        }
    }
}
