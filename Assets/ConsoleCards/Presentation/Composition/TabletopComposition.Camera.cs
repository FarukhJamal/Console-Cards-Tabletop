using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>Camera framing and authored poses. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        private void ConfigureTabletopCameraFraming()
        {
            if (cameraInputAdapter == null
                || cameraInputAdapter.CameraController == null
                || coordinateConverter == null)
            {
                return;
            }

            Bounds? localBounds = null;
            // The hand is a camera tray; it is not part of the framed table area.

            if (consoleView != null && consoleView.gameObject.activeInHierarchy)
            {
                EncapsulateBounds(
                    ref localBounds,
                    CreatePresentationBounds(consoleView.transform, 0.75f));
            }

            Bounds? sharedBounds = null;
            if (matchState != null && !centralPlayAreaId.IsEmpty)
            {
                PlayAreaState playArea = matchState.GetPlayArea(centralPlayAreaId);
                sharedBounds = CreateWorldBounds(playArea.Bounds);
            }

            Bounds? surroundingPlayerBounds = null;
            if (playerLayout != null)
            {
                for (int i = 0; i < playerLayout.Seats.Count; i++)
                {
                    PlayerSeatLayoutEntry seat = playerLayout.Seats[i];
                    EncapsulateBounds(
                        ref surroundingPlayerBounds,
                        coordinateConverter.ToWorldPosition(seat.PlayerZonePose.Position));
                    EncapsulateBounds(
                        ref surroundingPlayerBounds,
                        coordinateConverter.ToWorldPosition(seat.HandAnchorPose.Position));
                    EncapsulateBounds(
                        ref surroundingPlayerBounds,
                        coordinateConverter.ToWorldPosition(seat.ConsoleAnchorPose.Position));
                }
            }

            for (int i = 0; i < Registry.playerConsoleViews.Count; i++)
            {
                ConsoleView playerConsole = Registry.playerConsoleViews[i];
                if (playerConsole != null && playerConsole.gameObject.activeInHierarchy)
                {
                    EncapsulateBounds(
                        ref surroundingPlayerBounds,
                        CreatePresentationBounds(playerConsole.transform, 0.5f));
                }
            }

            Bounds? mappingBounds = null;
            if (sceneControllerMappingArea != null)
            {
                mappingBounds = CreatePresentationBounds(sceneControllerMappingArea, 0.5f);
            }

            float seatYawDegrees = 0f;
            Vector3? localSeatAnchor = null;
            if (localSeatLayout != null)
            {
                Quaternion seatRotation = coordinateConverter.ToWorldRotation(
                    new TabletopPose(
                        TableCoordinate.Zero,
                        localSeatLayout.FacingRotationDegrees,
                        0,
                        0));
                seatYawDegrees = seatRotation.eulerAngles.y;
                localSeatAnchor = coordinateConverter.ToWorldPosition(
                    localSeatLayout.PlayerZonePose.Position);
            }

            cameraInputAdapter.CameraController.ConfigureFramingTargets(
                localBounds,
                sharedBounds,
                seatYawDegrees,
                mappingBounds,
                sceneControllerMappingArea != null
                    ? sceneControllerMappingArea.gameObject
                    : null,
                localSeatAnchor,
                surroundingPlayerBounds);
        }

        private static void EncapsulateBounds(ref Bounds? aggregate, Bounds addition)
        {
            if (aggregate.HasValue)
            {
                Bounds expanded = aggregate.Value;
                expanded.Encapsulate(addition);
                aggregate = expanded;
                return;
            }

            aggregate = addition;
        }

        private static void EncapsulateBounds(ref Bounds? aggregate, Vector3 point)
        {
            if (aggregate.HasValue)
            {
                Bounds expanded = aggregate.Value;
                expanded.Encapsulate(point);
                aggregate = expanded;
                return;
            }

            aggregate = new Bounds(point, Vector3.zero);
        }

        private Bounds CreateWorldBounds(TabletopBounds bounds)
        {
            Vector3 minimum = coordinateConverter.ToWorldPosition(bounds.Minimum);
            Vector3 maximum = coordinateConverter.ToWorldPosition(bounds.Maximum);
            Bounds worldBounds = new Bounds(minimum, Vector3.zero);
            worldBounds.Encapsulate(maximum);
            Vector3 size = worldBounds.size;
            size.y = Mathf.Max(size.y, 0.1f);
            worldBounds.size = size;
            return worldBounds;
        }

        private static Bounds CreatePresentationBounds(Transform root, float minimumPlanarExtent)
        {
            bool found = false;
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            Vector3 extents = bounds.extents;
            extents.x = Mathf.Max(extents.x, minimumPlanarExtent);
            extents.y = Mathf.Max(extents.y, 0.05f);
            extents.z = Mathf.Max(extents.z, minimumPlanarExtent);
            bounds.extents = extents;
            return bounds;
        }

        private void ProjectTemplateCameraBookmark()
        {
            if (cameraInputAdapter.CameraController.ShowInitialGameView())
            {
                return;
            }

            IReadOnlyList<GameTemplateCameraBookmarkDefinition> bookmarks =
                templateContext.Session.CameraBookmarks;
            if (bookmarks.Count == 0)
            {
                return;
            }

            GameTemplateCameraBookmarkDefinition bookmark = bookmarks[0];
            cameraInputAdapter.CameraController.TransitionTo(
                new TabletopCameraBookmark(
                    bookmark.Name,
                    bookmark.FocusCoordinate,
                    bookmark.OrthographicSize));
        }

        internal void ApplyAuthoredPose(Transform target, TabletopPose pose)
        {
            target.SetPositionAndRotation(
                coordinateConverter.ToWorldPosition(pose),
                coordinateConverter.ToWorldRotation(pose));
        }

        internal void ApplyConsolePose(
            Transform target,
            TabletopPose pose,
            float? surfaceHeight)
        {
            Vector3 worldPosition = coordinateConverter.ToWorldPosition(pose);
            if (surfaceHeight.HasValue)
            {
                worldPosition.y = surfaceHeight.Value;
            }

            // Rest the Console's lowest body point on the surface (P1a); the authoritative pose and
            // surface height are unchanged.
            worldPosition.y += ComponentRestHeight.RestLift(target);
            target.SetPositionAndRotation(
                worldPosition,
                coordinateConverter.ToWorldRotation(pose));
        }
    }
}
