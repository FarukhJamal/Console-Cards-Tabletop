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

    /// <summary>
    /// Projects temporary, non-enforcing guidance onto Floor Cards: Ability destinations (cyan), neutralized Traps
    /// and, while the active player holds their pawn, the tiles in reach (R4 hints). Ability destinations win over
    /// the reach, and the reach over neutralized Traps.
    /// </summary>
    internal sealed class TrapFloorAbilityTargetPresenter
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly Color TargetColor = new Color(0.14f, 0.86f, 0.92f, 1f);
        private static readonly Color NeutralizedColor = new Color(0.32f, 0.58f, 0.48f, 1f);
        private static readonly Color ReachColor = new Color(0.62f, 0.9f, 0.38f, 1f);
        private readonly HashSet<TabletopObjectId> reachFloorCardIds = new HashSet<TabletopObjectId>();

        private readonly Dictionary<TabletopObjectId, TargetRenderer[]> renderers =
            new Dictionary<TabletopObjectId, TargetRenderer[]>();
        private readonly List<TabletopObjectId> currentTargetIds = new List<TabletopObjectId>();
        private readonly HashSet<TabletopObjectId> neutralizedFloorCardIds =
            new HashSet<TabletopObjectId>();

        // Scratch lists for colouring each material slot of a renderer (no allocation per call).
        private static readonly List<Material> slotMaterials = new List<Material>(4);
        private static MaterialPropertyBlock colourBlock;

        /// <summary>
        /// Puts a tile's own colours back (the game re-applies its art tint). Without it the colours captured at
        /// registration are restored.
        /// </summary>
        public Action<TabletopObjectId> Restore { get; set; }

        /// <summary>True while Ability destinations are shown.</summary>
        public bool HasCurrent => currentTargetIds.Count > 0;

        /// <summary>Lights the tiles in reach (R4 hints); replaces any reach shown before.</summary>
        public void ShowReach(IReadOnlyList<TabletopObjectId> floorCardIds)
        {
            if (floorCardIds == null) throw new ArgumentNullException(nameof(floorCardIds));
            ClearReach();
            for (int i = 0; i < floorCardIds.Count; i++)
            {
                TabletopObjectId floorCardId = floorCardIds[i];
                if (floorCardId.IsEmpty || !renderers.TryGetValue(floorCardId, out TargetRenderer[] targets)) continue;
                reachFloorCardIds.Add(floorCardId);
                if (!currentTargetIds.Contains(floorCardId)) ApplyColor(targets, ReachColor);
            }
        }

        public void ClearReach()
        {
            foreach (TabletopObjectId floorCardId in reachFloorCardIds)
            {
                if (currentTargetIds.Contains(floorCardId)
                    || !renderers.TryGetValue(floorCardId, out TargetRenderer[] targets)) continue;
                RestoreTile(floorCardId, targets);
                if (neutralizedFloorCardIds.Contains(floorCardId)) ApplyColor(targets, NeutralizedColor);
            }

            reachFloorCardIds.Clear();
        }

        /// <summary>
        /// Registers a tile's plain plates (tinted as a whole) and the renderers of the supplied card model (tinted per
        /// material slot, as its face and back slots carry their own tints).
        /// </summary>
        public void Register(TabletopObjectId floorCardId, Renderer[] floorRenderers, IReadOnlyList<Renderer> modelRenderers)
        {
            Register(floorCardId, floorRenderers);
            if (modelRenderers == null || modelRenderers.Count == 0) return;
            TargetRenderer[] plates = renderers[floorCardId];
            TargetRenderer[] all = new TargetRenderer[plates.Length + modelRenderers.Count];
            Array.Copy(plates, all, plates.Length);
            for (int i = 0; i < modelRenderers.Count; i++)
            {
                Renderer renderer = modelRenderers[i]
                    ?? throw new ArgumentException("Ability model renderers cannot contain null.", nameof(modelRenderers));
                MaterialPropertyBlock baseline = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(baseline);
                all[plates.Length + i] = new TargetRenderer(renderer, baseline, true);
            }

            renderers[floorCardId] = all;
        }

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
                    && !reachFloorCardIds.Contains(floorCardId)
                    && renderers.TryGetValue(floorCardId, out TargetRenderer[] targets))
                {
                    RestoreTile(floorCardId, targets);
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
                    && !reachFloorCardIds.Contains(floorCardId)
                    && renderers.TryGetValue(floorCardId, out TargetRenderer[] targets))
                {
                    ApplyColor(targets, NeutralizedColor);
                }
            }
        }

        public void Clear()
        {
            // The table is being taken down: put the captured colours back without asking the game.
            Action<TabletopObjectId> restore = Restore;
            Restore = null;
            ClearReach();
            ClearCurrent();
            Restore = restore;
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

                RestoreTile(currentTargetIds[i], targets);
                if (reachFloorCardIds.Contains(currentTargetIds[i]))
                    ApplyColor(targets, ReachColor);
                else if (neutralizedFloorCardIds.Contains(currentTargetIds[i]))
                    ApplyColor(targets, NeutralizedColor);
            }
            currentTargetIds.Clear();
        }

        // Tints the whole renderer and each of its material slots: the supplied card model tints its face and back
        // slots separately, and a slot's own tint would otherwise hide the guidance colour.
        private static void ApplyColor(TargetRenderer[] targets, Color color)
        {
            if (colourBlock == null) colourBlock = new MaterialPropertyBlock();
            for (int rendererIndex = 0; rendererIndex < targets.Length; rendererIndex++)
            {
                TargetRenderer target = targets[rendererIndex];
                if (target.Renderer == null) continue;
                if (!target.PerSlot)
                {
                    target.Renderer.GetPropertyBlock(colourBlock);
                    colourBlock.SetColor(BaseColorProperty, color);
                    colourBlock.SetColor(ColorProperty, color);
                    target.Renderer.SetPropertyBlock(colourBlock);
                    colourBlock.Clear();
                    continue;
                }

                target.Renderer.GetSharedMaterials(slotMaterials);
                for (int slot = 0; slot < slotMaterials.Count; slot++)
                {
                    target.Renderer.GetPropertyBlock(colourBlock, slot);
                    colourBlock.SetColor(BaseColorProperty, color);
                    colourBlock.SetColor(ColorProperty, color);
                    target.Renderer.SetPropertyBlock(colourBlock, slot);
                    colourBlock.Clear();
                }
            }

            slotMaterials.Clear();
        }

        private void RestoreTile(TabletopObjectId floorCardId, TargetRenderer[] targets)
        {
            if (Restore != null)
            {
                Restore(floorCardId);
                return;
            }

            RestoreBaseline(targets);
        }

        private static void RestoreBaseline(TargetRenderer[] targets)
        {
            for (int rendererIndex = 0; rendererIndex < targets.Length; rendererIndex++)
            {
                TargetRenderer target = targets[rendererIndex];
                if (target.Renderer == null) continue;
                if (!target.PerSlot)
                {
                    target.Renderer.SetPropertyBlock(target.Baseline);
                    continue;
                }

                // Model slots: drop the guidance colour; the game re-tints them on its next refresh.
                target.Renderer.GetSharedMaterials(slotMaterials);
                for (int slot = 0; slot < slotMaterials.Count; slot++) target.Renderer.SetPropertyBlock(null, slot);
            }

            slotMaterials.Clear();
        }

        private sealed class TargetRenderer
        {
            public TargetRenderer(Renderer renderer, MaterialPropertyBlock baseline, bool perSlot = false)
            {
                Renderer = renderer;
                Baseline = baseline;
                PerSlot = perSlot;
            }

            public Renderer Renderer { get; }
            public MaterialPropertyBlock Baseline { get; }
            public bool PerSlot { get; }
        }
    }
}
