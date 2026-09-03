using System;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>Explicitly injected authored colliders, independent from decorative geometry and camera motion.</summary>
    public sealed class PhysicalTabletopSurfaces : IPhysicalPlacementResolver
    {
        private readonly Collider[] surfaces;
        private readonly UnityEngine.Camera camera;
        private readonly TabletopCoordinateConverter converter;
        public const float PlacementClearance = 0.06f;

        public PhysicalTabletopSurfaces(UnityEngine.Camera camera, TabletopCoordinateConverter converter, Collider[] surfaces)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
            this.surfaces = (Collider[])(surfaces ?? throw new ArgumentNullException(nameof(surfaces))).Clone();
            if (surfaces.Length == 0) throw new ArgumentException("Physical placement requires authored Table/Board colliders.");
            foreach (Collider surface in surfaces)
                if (surface == null || surface.isTrigger || surface.attachedRigidbody != null)
                    throw new ArgumentException("Surface colliders must be assigned, static, and non-trigger.");
        }

        public bool TryPointer(Vector2 screen, out RaycastHit hit) => TryRay(camera.ScreenPointToRay(screen), out hit);

        public bool TryAtLayout(TabletopPose pose, out RaycastHit hit)
        {
            Vector3 point = converter.ToWorldPosition(pose);
            float highest = point.y;
            foreach (Collider surface in surfaces)
                if (surface.enabled && surface.gameObject.activeInHierarchy) highest = Mathf.Max(highest, surface.bounds.max.y);
            point.y = highest + 2f;
            return TryRay(new Ray(point, Vector3.down), out hit);
        }

        public bool TryResolve(TabletopPose layoutPose, PlayerId actor, out PhysicalObjectState state,
            TabletopComponentKind kind = TabletopComponentKind.Card)
        {
            if (!TryAtLayout(layoutPose, out RaycastHit hit)) { state = null; return false; }
            state = PhysicalLooseObject.State(hit.point + Vector3.up * (kind == TabletopComponentKind.Die ? 0.6f : PlacementClearance),
                converter.ToWorldRotation(layoutPose), Vector3.zero, Vector3.zero, PhysicalObjectMode.Dynamic, actor);
            return true;
        }

        public TableCoordinate Coordinate(Vector3 worldPosition) => converter.ToTableCoordinate(worldPosition);

        private bool TryRay(Ray ray, out RaycastHit closest)
        {
            Physics.SyncTransforms();
            closest = default;
            float distance = float.PositiveInfinity;
            bool found = false;
            foreach (Collider surface in surfaces)
            {
                if (!surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                if (surface.Raycast(ray, out RaycastHit hit, float.MaxValue)
                    && Vector3.Dot(hit.normal, Vector3.up) > 0.1f && hit.distance < distance)
                { closest = hit; distance = hit.distance; found = true; }
            }
            return found;
        }
    }
}
