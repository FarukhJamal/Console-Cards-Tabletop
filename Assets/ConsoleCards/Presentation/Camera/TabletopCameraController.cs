using System;
using System.Runtime.CompilerServices;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

[assembly: InternalsVisibleTo("ConsoleCards.Tests.PlayMode")]

namespace ConsoleCards.Presentation.Camera
{
    /// <summary>
    /// Applies local Tabletop Camera state to an explicitly assigned Camera and rig.
    /// </summary>
    public sealed class TabletopCameraController : MonoBehaviour
    {
        [SerializeField] internal UnityCamera targetCamera;
        [SerializeField] internal Transform cameraRig;
        [SerializeField] internal float worldUnitsPerTableUnit = 1f;
        [SerializeField] internal float cameraHeight = 10f;
        [SerializeField] internal float minimumOrthographicSize = 2f;
        [SerializeField] internal float maximumOrthographicSize = 20f;
        [SerializeField] internal float initialOrthographicSize = 5f;

        private TabletopCoordinateConverter coordinateConverter;
        private bool isInitialized;

        public TabletopCameraState State { get; private set; }

        public UnityCamera TargetCamera => targetCamera;

        public Transform CameraRig => cameraRig;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            State = new TabletopCameraState(
                TableCoordinate.Zero,
                initialOrthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            coordinateConverter = new TabletopCoordinateConverter(worldUnitsPerTableUnit, 0f, 0f, 0f);
            isInitialized = true;

            ApplyState();
        }

        public void Pan(double deltaX, double deltaY)
        {
            EnsureInitialized();

            State.Pan(deltaX, deltaY);
            ApplyState();
        }

        public void Zoom(float delta)
        {
            EnsureInitialized();

            State.Zoom(delta);
            ApplyState();
        }

        public void Focus(TableCoordinate coordinate)
        {
            EnsureInitialized();

            State.SetFocus(coordinate);
            ApplyState();
        }

        public void Focus(TableCoordinate coordinate, float orthographicSize)
        {
            EnsureInitialized();

            State.SetFocus(coordinate, orthographicSize);
            ApplyState();
        }

        public void ApplyState()
        {
            EnsureInitialized();

            Vector3 convertedFocus = coordinateConverter.ToWorldPosition(State.FocusCoordinate);
            cameraRig.position = new Vector3(convertedFocus.x, cameraHeight, convertedFocus.z);
            targetCamera.orthographicSize = State.OrthographicSize;
        }

        private bool ValidateConfiguration()
        {
            if (targetCamera == null)
            {
                LogConfigurationError("TabletopCameraController requires a target Camera reference.");
                return false;
            }

            if (cameraRig == null)
            {
                LogConfigurationError("TabletopCameraController requires a CameraRig Transform reference.");
                return false;
            }

            if (!targetCamera.orthographic)
            {
                LogConfigurationError("TabletopCameraController requires an orthographic target Camera.");
                return false;
            }

            if (!IsFinite(worldUnitsPerTableUnit) || worldUnitsPerTableUnit <= 0f)
            {
                LogConfigurationError("TabletopCameraController requires finite worldUnitsPerTableUnit greater than zero.");
                return false;
            }

            if (!IsFinite(cameraHeight))
            {
                LogConfigurationError("TabletopCameraController requires finite cameraHeight.");
                return false;
            }

            if (!IsFinite(minimumOrthographicSize) || minimumOrthographicSize <= 0f)
            {
                LogConfigurationError("TabletopCameraController requires finite minimumOrthographicSize greater than zero.");
                return false;
            }

            if (!IsFinite(maximumOrthographicSize) || maximumOrthographicSize < minimumOrthographicSize)
            {
                LogConfigurationError("TabletopCameraController requires finite maximumOrthographicSize greater than or equal to minimumOrthographicSize.");
                return false;
            }

            if (!IsFinite(initialOrthographicSize))
            {
                LogConfigurationError("TabletopCameraController requires finite initialOrthographicSize.");
                return false;
            }

            return true;
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("TabletopCameraController has not been successfully initialized.");
            }
        }

        private void LogConfigurationError(string message)
        {
            Debug.LogError(message, this);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
