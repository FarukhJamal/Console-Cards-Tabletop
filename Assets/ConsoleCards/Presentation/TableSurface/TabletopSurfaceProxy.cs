using System;
using UnityEngine;

namespace ConsoleCards.Presentation.TableSurface
{
    /// <summary>
    /// Local Presentation coverage proxy for the Table Surface.
    /// It may follow the Camera in X/Z, but it does not represent or move
    /// the logical tabletop. Future patterned or textured surface visuals
    /// must remain anchored to Tabletop-space or world-space X/Z coordinates.
    /// </summary>
    public sealed class TabletopSurfaceProxy : MonoBehaviour
    {
        [SerializeField] internal Transform trackedTransform;
        [SerializeField] internal Transform surfaceTransform;
        [SerializeField] internal float surfaceHeight = 0f;

        public bool IsInitialized { get; private set; }

        public Transform TrackedTransform => trackedTransform;

        public Transform SurfaceTransform => surfaceTransform;

        public float SurfaceHeight => surfaceHeight;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                IsInitialized = false;
                enabled = false;
                return;
            }

            IsInitialized = true;
            ApplyFollow();
        }

        private void LateUpdate()
        {
            if (IsInitialized)
            {
                ApplyFollow();
            }
        }

        public void ApplyFollow()
        {
            EnsureInitialized();

            ApplyFollowPosition(trackedTransform.position);
        }

        internal void ApplyFollowPosition(Vector3 trackedPosition)
        {
            EnsureInitialized();
            ValidateTrackedPosition(trackedPosition);

            surfaceTransform.position = new Vector3(trackedPosition.x, surfaceHeight, trackedPosition.z);
        }

        private bool ValidateConfiguration()
        {
            if (trackedTransform == null)
            {
                LogConfigurationError("TabletopSurfaceProxy requires a tracked Transform reference.");
                return false;
            }

            if (surfaceTransform == null)
            {
                LogConfigurationError("TabletopSurfaceProxy requires a surface Transform reference.");
                return false;
            }

            if (!IsFinite(surfaceHeight))
            {
                LogConfigurationError("TabletopSurfaceProxy requires finite surfaceHeight.");
                return false;
            }

            return true;
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("TabletopSurfaceProxy has not been successfully initialized.");
            }
        }

        private static void ValidateTrackedPosition(Vector3 trackedPosition)
        {
            ValidateFinite(trackedPosition.x, nameof(trackedPosition));
            ValidateFinite(trackedPosition.y, nameof(trackedPosition));
            ValidateFinite(trackedPosition.z, nameof(trackedPosition));
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
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
