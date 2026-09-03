using System;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>Queries live, opted-in surface components; independent from model assets, hierarchy and camera motion.</summary>
    public sealed class PhysicalTabletopSurfaces : IPhysicalPlacementResolver
    {
        private readonly UnityEngine.Camera camera;
        private readonly TabletopCoordinateConverter converter;
        private bool missingSetupReported;
        public const float PlacementClearance = 0.06f;
        public const string MissingSurfaceMessage = "Physical tabletop placement is unavailable: no active, usable PhysicalTabletopSurface collider is registered. Add PhysicalTabletopSurface and an enabled, non-trigger Collider to the same GameObject on the Table or Board top. No model or composition reference is required.";

        public PhysicalTabletopSurfaces(UnityEngine.Camera camera, TabletopCoordinateConverter converter)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public static int CountUsableSurfaces(PhysicsScene physicsScene)
        {
            int count = 0;
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
                if (surface != null && surface.ParticipatesIn(physicsScene) && surface.TryGetCollider(out _, out _)) count++;
            return count;
        }

        public bool ValidateSetup()
        {
            PhysicsScene physicsScene = camera.gameObject.scene.GetPhysicsScene();
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
                if (surface != null && surface.ParticipatesIn(physicsScene)) surface.ReportConfigurationIssue();
            bool available = CountUsableSurfaces(physicsScene) > 0;
            if (!available && !missingSetupReported)
                Debug.LogError(MissingSurfaceMessage, camera);
            missingSetupReported = !available;
            return available;
        }

        public bool TryPointer(Vector2 screen, out RaycastHit hit) => TryRay(camera.ScreenPointToRay(screen), out hit);

        public bool TryAtLayout(TabletopPose pose, out RaycastHit hit)
        {
            Physics.SyncTransforms();
            Vector3 point = converter.ToWorldPosition(pose);
            float highest = point.y;
            PhysicsScene physicsScene = camera.gameObject.scene.GetPhysicsScene();
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
                if (surface != null && surface.ParticipatesIn(physicsScene) && surface.TryGetCollider(out Collider collider, out _))
                    highest = Mathf.Max(highest, collider.bounds.max.y);
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
            if (!ValidateSetup()) return false;
            float distance = float.PositiveInfinity;
            bool found = false;
            PhysicsScene physicsScene = camera.gameObject.scene.GetPhysicsScene();
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
            {
                if (surface == null || !surface.ParticipatesIn(physicsScene) || !surface.TryGetCollider(out Collider collider, out _)) continue;
                if (collider.Raycast(ray, out RaycastHit hit, float.MaxValue)
                    && Vector3.Dot(hit.normal, Vector3.up) > 0.1f && hit.distance < distance)
                { closest = hit; distance = hit.distance; found = true; }
            }
            return found;
        }
    }
}
