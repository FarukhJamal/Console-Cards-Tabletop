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

    /// <summary>Projects temporary, non-enforcing Dodge destination guidance onto Floor Cards.</summary>
    internal sealed class TrapFloorDodgeTargetPresenter
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly Color TargetColor = new Color(0.14f, 0.86f, 0.92f, 1f);

        private readonly Dictionary<TabletopObjectId, TargetRenderer[]> renderers =
            new Dictionary<TabletopObjectId, TargetRenderer[]>();
        private readonly List<TabletopObjectId> currentTargetIds = new List<TabletopObjectId>();

        public void Register(TabletopObjectId floorCardId, params Renderer[] floorRenderers)
        {
            if (floorCardId.IsEmpty)
                throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            if (floorRenderers == null || floorRenderers.Length == 0)
                throw new ArgumentException("Dodge requires at least one Floor renderer.", nameof(floorRenderers));
            if (renderers.ContainsKey(floorCardId))
                throw new InvalidOperationException("A Dodge target renderer is already registered.");

            TargetRenderer[] targets = new TargetRenderer[floorRenderers.Length];
            for (int i = 0; i < floorRenderers.Length; i++)
            {
                Renderer renderer = floorRenderers[i]
                    ?? throw new ArgumentException("Dodge Floor renderers cannot contain null.", nameof(floorRenderers));
                MaterialPropertyBlock baseline = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(baseline);
                targets[i] = new TargetRenderer(renderer, baseline);
            }
            renderers.Add(floorCardId, targets);
        }

        public void Show(IReadOnlyList<TabletopObjectId> floorCardIds)
        {
            if (floorCardIds == null) throw new ArgumentNullException(nameof(floorCardIds));
            ClearCurrent();
            for (int i = 0; i < floorCardIds.Count; i++)
            {
                TabletopObjectId floorCardId = floorCardIds[i];
                if (!renderers.TryGetValue(floorCardId, out TargetRenderer[] targets))
                {
                    throw new InvalidOperationException("Dodge target has no active Floor Card renderer.");
                }

                for (int rendererIndex = 0; rendererIndex < targets.Length; rendererIndex++)
                {
                    TargetRenderer target = targets[rendererIndex];
                    if (target.Renderer == null) continue;
                    MaterialPropertyBlock highlighted = new MaterialPropertyBlock();
                    target.Renderer.GetPropertyBlock(highlighted);
                    highlighted.SetColor(BaseColorProperty, TargetColor);
                    highlighted.SetColor(ColorProperty, TargetColor);
                    target.Renderer.SetPropertyBlock(highlighted);
                }
                currentTargetIds.Add(floorCardId);
            }
        }

        public void Clear()
        {
            ClearCurrent();
            renderers.Clear();
        }

        public void ClearCurrent()
        {
            for (int i = 0; i < currentTargetIds.Count; i++)
            {
                if (!renderers.TryGetValue(currentTargetIds[i], out TargetRenderer[] targets))
                {
                    continue;
                }

                for (int rendererIndex = 0; rendererIndex < targets.Length; rendererIndex++)
                {
                    TargetRenderer target = targets[rendererIndex];
                    if (target.Renderer != null)
                        target.Renderer.SetPropertyBlock(target.Baseline);
                }
            }
            currentTargetIds.Clear();
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
