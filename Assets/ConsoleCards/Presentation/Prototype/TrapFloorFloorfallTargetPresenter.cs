using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using UnityEngine;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>
    /// Projects Floorfall target state through a renderer property override independent of selection.
    /// </summary>
    internal sealed class TrapFloorFloorfallTargetPresenter
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly Color TargetColor = new Color(1f, 0.22f, 0.06f, 1f);

        private readonly Dictionary<TabletopObjectId, TargetRenderer> renderers =
            new Dictionary<TabletopObjectId, TargetRenderer>();

        private TabletopObjectId currentTargetId;

        public void Register(TabletopObjectId floorCardId, Renderer renderer)
        {
            if (floorCardId.IsEmpty)
            {
                throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            }

            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            if (renderers.ContainsKey(floorCardId))
            {
                throw new InvalidOperationException("A Floor Card target renderer is already registered.");
            }

            MaterialPropertyBlock baseline = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(baseline);
            renderers.Add(floorCardId, new TargetRenderer(renderer, baseline));
        }

        public void Show(TabletopObjectId floorCardId)
        {
            ClearCurrent();

            if (!renderers.TryGetValue(floorCardId, out TargetRenderer target)
                || target.Renderer == null)
            {
                throw new InvalidOperationException("Floorfall target has no active Floor Card renderer.");
            }

            MaterialPropertyBlock highlighted = new MaterialPropertyBlock();
            target.Renderer.GetPropertyBlock(highlighted);
            highlighted.SetColor(BaseColorProperty, TargetColor);
            highlighted.SetColor(ColorProperty, TargetColor);
            target.Renderer.SetPropertyBlock(highlighted);
            currentTargetId = floorCardId;
        }

        public void Clear()
        {
            ClearCurrent();
            renderers.Clear();
        }

        private void ClearCurrent()
        {
            if (!currentTargetId.IsEmpty
                && renderers.TryGetValue(currentTargetId, out TargetRenderer current)
                && current.Renderer != null)
            {
                current.Renderer.SetPropertyBlock(current.Baseline);
            }

            currentTargetId = TabletopObjectId.Empty;
        }

        private sealed class TargetRenderer
        {
            public TargetRenderer(Renderer renderer, MaterialPropertyBlock baseline)
            {
                Renderer = renderer;
                Baseline = baseline;
            }

            public Renderer Renderer { get; }

            public MaterialPropertyBlock Baseline { get; }
        }
    }
}
