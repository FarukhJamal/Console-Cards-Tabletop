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

        public static TabletopCoordinateConverter CreateTemplateLayoutConverter(
            UnityEngine.Camera camera,
            float worldUnitsPerTableUnit,
            float layerHeight,
            float localOrderHeight)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            Physics.SyncTransforms();
            PhysicsScene physicsScene = camera.gameObject.scene.GetPhysicsScene();
            PhysicalTabletopSurface selected = null;
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
            {
                if (surface == null
                    || !surface.ParticipatesIn(physicsScene)
                    || !surface.IsTemplateLayoutOrigin)
                {
                    continue;
                }

                if (selected != null)
                {
                    throw new InvalidOperationException(
                        "Game Template projection requires exactly one active PhysicalTabletopSurface marked as the Template Layout Origin.");
                }

                selected = surface;
            }

            if (selected == null)
            {
                throw new InvalidOperationException(
                    "Game Template projection requires one active physical Table surface marked as the Template Layout Origin.");
            }

            if (!selected.TryCreateTemplateCoordinateConverter(
                    worldUnitsPerTableUnit,
                    layerHeight,
                    localOrderHeight,
                    out TabletopCoordinateConverter converter,
                    out string issue))
            {
                throw new InvalidOperationException($"Game Template physical Table projection is invalid: {issue}");
            }

            return converter;
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
            const float layoutRayDistance = 1000f;
            return TryRay(
                new Ray(point + (converter.WorldUp * layoutRayDistance), -converter.WorldUp),
                out hit,
                converter.WorldUp,
                layoutRayDistance * 2f);
        }

        public bool TryResolve(TabletopPose layoutPose, PlayerId actor, out PhysicalObjectState state,
            TabletopComponentKind kind = TabletopComponentKind.Card)
        {
            if (!TryAtLayout(layoutPose, out RaycastHit hit)) { state = null; return false; }
            state = PhysicalLooseObject.State(hit.point + hit.normal.normalized * (kind == TabletopComponentKind.Die ? 0.6f : PlacementClearance),
                converter.ToWorldRotation(layoutPose), Vector3.zero, Vector3.zero, PhysicalObjectMode.Dynamic, actor);
            return true;
        }

        public bool TryResolveAuthoredLooseObject(
            TabletopPose layoutPose,
            PlayerId actor,
            Collider physicalCollider,
            bool isUserLocked,
            out PhysicalObjectState state)
        {
            if (physicalCollider == null)
            {
                throw new ArgumentNullException(nameof(physicalCollider));
            }

            if (!TryAtLayout(layoutPose, out RaycastHit hit))
            {
                state = null;
                return false;
            }

            Vector3 normal = hit.normal.normalized;
            Bounds bounds = physicalCollider.bounds;
            Vector3 centerOffset = bounds.center - physicalCollider.transform.position;
            float projectedExtent = Vector3.Dot(Abs(normal), bounds.extents);
            float restingRootOffset = Mathf.Max(0f, projectedExtent - Vector3.Dot(centerOffset, normal));
            float settlingClearance = isUserLocked ? 0f : PlacementClearance;
            state = PhysicalLooseObject.State(
                hit.point + (normal * (restingRootOffset + settlingClearance)),
                converter.ToWorldRotation(layoutPose),
                Vector3.zero,
                Vector3.zero,
                isUserLocked ? PhysicalObjectMode.Sleeping : PhysicalObjectMode.Dynamic,
                actor);
            return true;
        }

        public TableCoordinate Coordinate(Vector3 worldPosition) => converter.ToTableCoordinate(worldPosition);

        // Non-physical Containers sit on the surface; loose-object spawn clearance does not apply.
        public float? ResolveContainerSurfaceHeight(TabletopPose pose) =>
            TryAtLayout(pose, out RaycastHit hit) ? hit.point.y : (float?)null;

        private bool TryRay(Ray ray, out RaycastHit closest)
        {
            return TryRay(ray, out closest, Vector3.up, float.MaxValue);
        }

        private bool TryRay(Ray ray, out RaycastHit closest, Vector3 expectedUp, float maximumDistance)
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
                if (collider.Raycast(ray, out RaycastHit hit, maximumDistance)
                    && Vector3.Dot(hit.normal, expectedUp) > 0.1f && hit.distance < distance)
                { closest = hit; distance = hit.distance; found = true; }
            }
            return found;
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }
}
