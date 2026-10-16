using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Feedback;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Shows the Floorfall collapse target (the tile the floor dice point at) with a board feedback marker
    /// (doc 23 H3). The tile's art is never recoloured: the trading-card model hides a tinted card colour.
    /// </summary>
    internal sealed class TrapFloorFloorfallTargetPresenter
    {
        public static readonly Color TargetColor = new Color(1f, 0.42f, 0.1f, 0.95f);

        private BoardFeedbackPresenter shownOn;

        public TabletopObjectId CurrentTargetId { get; private set; }

        public bool IsShowing => shownOn != null;

        /// <summary>Marks the target tile at its table position; replaces the previous target.</summary>
        public void Show(TabletopObjectId floorCardId, Vector3 tilePosition, BoardFeedbackPresenter feedback)
        {
            if (floorCardId.IsEmpty)
            {
                throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            }

            if (feedback == null)
            {
                Clear(); // board feedback is off (its set is missing); the Console already says so
                return;
            }

            if (shownOn != null && shownOn != feedback) shownOn.HideCollapseTarget();
            feedback.ShowCollapseTarget(tilePosition, TargetColor);
            shownOn = feedback;
            CurrentTargetId = floorCardId;
        }

        public void Clear()
        {
            shownOn?.HideCollapseTarget();
            shownOn = null;
            CurrentTargetId = TabletopObjectId.Empty;
        }
    }

    /// <summary>Projects temporary, non-enforcing Ability destination guidance onto Floor Cards.</summary>
    internal sealed class TrapFloorAbilityTargetPresenter
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly Color TargetColor = new Color(0.14f, 0.86f, 0.92f, 1f);
        private static readonly Color NeutralizedColor = new Color(0.32f, 0.58f, 0.48f, 1f);

        private readonly Dictionary<TabletopObjectId, TargetRenderer[]> renderers =
            new Dictionary<TabletopObjectId, TargetRenderer[]>();
        private readonly List<TabletopObjectId> currentTargetIds = new List<TabletopObjectId>();
        private readonly HashSet<TabletopObjectId> neutralizedFloorCardIds =
            new HashSet<TabletopObjectId>();

        public void Register(TabletopObjectId floorCardId, params Renderer[] floorRenderers)
        {
            if (floorCardId.IsEmpty)
                throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            if (floorRenderers == null || floorRenderers.Length == 0)
                throw new ArgumentException("Ability guidance requires at least one Floor renderer.", nameof(floorRenderers));
            if (renderers.ContainsKey(floorCardId))
                throw new InvalidOperationException("An Ability target renderer is already registered.");

            TargetRenderer[] targets = new TargetRenderer[floorRenderers.Length];
            for (int i = 0; i < floorRenderers.Length; i++)
            {
                Renderer renderer = floorRenderers[i]
                    ?? throw new ArgumentException("Ability Floor renderers cannot contain null.", nameof(floorRenderers));
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
                    throw new InvalidOperationException("Ability target has no active Floor Card renderer.");
                }

                ApplyColor(targets, TargetColor);
                currentTargetIds.Add(floorCardId);
            }
        }

        public void ShowNeutralized(IReadOnlyList<TabletopObjectId> floorCardIds)
        {
            if (floorCardIds == null) throw new ArgumentNullException(nameof(floorCardIds));

            foreach (TabletopObjectId floorCardId in neutralizedFloorCardIds)
            {
                if (!currentTargetIds.Contains(floorCardId)
                    && renderers.TryGetValue(floorCardId, out TargetRenderer[] targets))
                {
                    RestoreBaseline(targets);
                }
            }

            neutralizedFloorCardIds.Clear();
            for (int i = 0; i < floorCardIds.Count; i++)
            {
                TabletopObjectId floorCardId = floorCardIds[i];
                if (!floorCardId.IsEmpty) neutralizedFloorCardIds.Add(floorCardId);
            }

            foreach (TabletopObjectId floorCardId in neutralizedFloorCardIds)
            {
                if (!currentTargetIds.Contains(floorCardId)
                    && renderers.TryGetValue(floorCardId, out TargetRenderer[] targets))
                {
                    ApplyColor(targets, NeutralizedColor);
                }
            }
        }

        public void Clear()
        {
            ClearCurrent();
            foreach (TabletopObjectId floorCardId in neutralizedFloorCardIds)
            {
                if (renderers.TryGetValue(floorCardId, out TargetRenderer[] targets))
                    RestoreBaseline(targets);
            }
            neutralizedFloorCardIds.Clear();
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

                RestoreBaseline(targets);
                if (neutralizedFloorCardIds.Contains(currentTargetIds[i]))
                    ApplyColor(targets, NeutralizedColor);
            }
            currentTargetIds.Clear();
        }

        private static void ApplyColor(TargetRenderer[] targets, Color color)
        {
            for (int rendererIndex = 0; rendererIndex < targets.Length; rendererIndex++)
            {
                TargetRenderer target = targets[rendererIndex];
                if (target.Renderer == null) continue;
                MaterialPropertyBlock highlighted = new MaterialPropertyBlock();
                target.Renderer.GetPropertyBlock(highlighted);
                highlighted.SetColor(BaseColorProperty, color);
                highlighted.SetColor(ColorProperty, color);
                target.Renderer.SetPropertyBlock(highlighted);
            }
        }

        private static void RestoreBaseline(TargetRenderer[] targets)
        {
            for (int rendererIndex = 0; rendererIndex < targets.Length; rendererIndex++)
            {
                TargetRenderer target = targets[rendererIndex];
                if (target.Renderer != null)
                    target.Renderer.SetPropertyBlock(target.Baseline);
            }
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
