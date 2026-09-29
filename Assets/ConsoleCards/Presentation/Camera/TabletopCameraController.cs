using System;
using System.Runtime.CompilerServices;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

[assembly: InternalsVisibleTo("ConsoleCards.Tests.PlayMode")]

namespace ConsoleCards.Presentation.Camera
{
    public enum TabletopCameraPreset
    {
        Close,
        Mid,
        Board,
        TopDown,
    }

    [Serializable]
    public sealed class TabletopCameraPresetTuning
    {
        [SerializeField] internal float pitchDegrees = 72f;
        [SerializeField] internal float yawOffsetDegrees;
        [SerializeField] internal float framingPadding = 0.65f;
        [SerializeField] internal float distanceMultiplier = 1f;
        [SerializeField] internal bool includeLocalPlayerArea = true;
        [SerializeField] internal bool includeBoardArea;
        [SerializeField] internal bool showControllerMappingArea;

        public float PitchDegrees => pitchDegrees;
        public float YawOffsetDegrees => yawOffsetDegrees;
        public float FramingPadding => framingPadding;
        public float DistanceMultiplier => distanceMultiplier;
        public bool IncludeLocalPlayerArea => includeLocalPlayerArea;
        public bool IncludeBoardArea => includeBoardArea;
        public bool ShowControllerMappingArea => showControllerMappingArea;
    }

    /// <summary>
    /// Owns the local, Presentation-only Tabletop Camera pose. Presets provide reusable
    /// framing starts; free navigation always remains available afterward.
    /// </summary>
    public sealed class TabletopCameraController : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] internal UnityCamera targetCamera;
        [SerializeField] internal Transform cameraRig;
        [SerializeField] internal float worldUnitsPerTableUnit = 1f;
        [SerializeField] internal float cameraHeight = 10f;
        [SerializeField] internal float initialDistance = 16f;
        [SerializeField] internal float initialPitchDegrees = 78f;
        [SerializeField] internal float initialYawDegrees;
        [SerializeField] internal float tabletopHeight;
        [SerializeField, Range(1f, 179f)] internal float perspectiveFieldOfView = 50f;
        [SerializeField] internal bool topDownUsesOrthographic = true;

        [Header("Zoom / Distance")]
        [SerializeField] internal float minimumOrthographicSize = 2f;
        [SerializeField] internal float maximumOrthographicSize = 20f;
        [SerializeField] internal float initialOrthographicSize = 5f;
        [SerializeField] internal float minimumDistance = 3f;
        [SerializeField] internal float maximumDistance = 32f;
        [SerializeField] internal float zoomSpeed = 22.5f;

        [Header("Navigation")]
        [SerializeField] internal float orbitDegreesPerPixel = 0.18f;
        [SerializeField] internal float panDistanceScale = 0.085f;
        [SerializeField] internal float minimumPitchDegrees = 32f;
        [SerializeField] internal float maximumPitchDegrees = 89.5f;
        [SerializeField] internal float motionSmoothing = 0.09f;
        [SerializeField] internal float presetTransitionDuration = 0.32f;
        [SerializeField] internal float initialGameFramingDistanceMultiplier = 0.85f;

        [Header("Authored Views")]
        [SerializeField] internal TabletopCameraPresetTuning closeView = new TabletopCameraPresetTuning
        {
            pitchDegrees = 78f,
            framingPadding = 0.55f,
            distanceMultiplier = 0.9f,
            includeLocalPlayerArea = true,
            includeBoardArea = false,
            showControllerMappingArea = true,
        };
        [SerializeField] internal TabletopCameraPresetTuning midView = new TabletopCameraPresetTuning
        {
            pitchDegrees = 66f,
            framingPadding = 0.8f,
            distanceMultiplier = 1f,
            includeLocalPlayerArea = true,
            includeBoardArea = true,
            showControllerMappingArea = false,
        };
        [SerializeField] internal TabletopCameraPresetTuning boardView = new TabletopCameraPresetTuning
        {
            pitchDegrees = 72f,
            framingPadding = 1f,
            distanceMultiplier = 1.08f,
            includeLocalPlayerArea = false,
            includeBoardArea = true,
            showControllerMappingArea = false,
        };
        [SerializeField] internal TabletopCameraPresetTuning topDownView = new TabletopCameraPresetTuning
        {
            pitchDegrees = 89.5f,
            framingPadding = 0.9f,
            distanceMultiplier = 1f,
            includeLocalPlayerArea = false,
            includeBoardArea = true,
            showControllerMappingArea = false,
        };
        private GameObject controllerMappingAreaRoot;

        private TabletopCoordinateConverter coordinateConverter;
        private bool isInitialized;
        private bool hasLocalPlayerBounds;
        private bool hasBoardBounds;
        private bool hasRelevantPlayerBounds;
        private bool hasControllerMappingBounds;
        private bool hasLocalSeatAnchor;
        private Bounds localPlayerBounds;
        private Bounds boardBounds;
        private Bounds relevantPlayerBounds;
        private Bounds controllerMappingBounds;
        private Vector3 localSeatAnchor;
        private float localSeatYawDegrees;

        private Vector3 currentPivot;
        private Vector3 targetPivot;
        private float currentYawDegrees;
        private float targetYawDegrees;
        private float currentPitchDegrees;
        private float targetPitchDegrees;
        private float currentDistance;
        private float targetDistance;
        private float currentOrthographicSize;
        private float targetOrthographicSize;
        private Vector3 pivotVelocity;
        private float yawVelocity;
        private float pitchVelocity;
        private float distanceVelocity;
        private float orthographicSizeVelocity;

        private bool transitionActive;
        private float transitionElapsed;
        private float transitionDuration;
        private Vector3 transitionStartPivot;
        private float transitionStartYaw;
        private float transitionStartPitch;
        private float transitionStartDistance;
        private float transitionStartOrthographicSize;
        private bool usesOrthographicProjection;

        public TabletopCameraState State { get; private set; }
        public UnityCamera TargetCamera => targetCamera;
        public Transform CameraRig => cameraRig;
        public float YawDegrees => targetYawDegrees;
        public float PitchDegrees => targetPitchDegrees;
        public float Distance => targetDistance;
        public bool HasFramingTargets => hasLocalPlayerBounds
            || hasBoardBounds
            || hasRelevantPlayerBounds
            || hasLocalSeatAnchor;
        public bool IsInitialized => isInitialized;

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
            coordinateConverter = new TabletopCoordinateConverter(worldUnitsPerTableUnit, tabletopHeight, 0f, 0f);
            currentPivot = targetPivot = coordinateConverter.ToWorldPosition(State.FocusCoordinate);
            currentYawDegrees = targetYawDegrees = NormalizeYaw(initialYawDegrees);
            currentPitchDegrees = targetPitchDegrees = ClampPitch(initialPitchDegrees);
            currentDistance = targetDistance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
            currentOrthographicSize = targetOrthographicSize = State.OrthographicSize;
            usesOrthographicProjection = false;
            isInitialized = true;

            ApplyCurrentPose();
        }

        private void LateUpdate()
        {
            if (!isInitialized)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            if (transitionActive)
            {
                transitionElapsed += deltaTime;
                float progress = transitionDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(transitionElapsed / transitionDuration);
                float eased = progress * progress * (3f - (2f * progress));
                currentPivot = Vector3.Lerp(transitionStartPivot, targetPivot, eased);
                currentYawDegrees = Mathf.LerpAngle(transitionStartYaw, targetYawDegrees, eased);
                currentPitchDegrees = Mathf.Lerp(transitionStartPitch, targetPitchDegrees, eased);
                currentDistance = Mathf.Lerp(transitionStartDistance, targetDistance, eased);
                currentOrthographicSize = Mathf.Lerp(
                    transitionStartOrthographicSize,
                    targetOrthographicSize,
                    eased);
                if (progress >= 1f)
                {
                    transitionActive = false;
                    ResetSmoothingVelocities();
                }
            }
            else
            {
                float smoothTime = Mathf.Max(0.0001f, motionSmoothing);
                currentPivot = Vector3.SmoothDamp(
                    currentPivot,
                    targetPivot,
                    ref pivotVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
                currentYawDegrees = Mathf.SmoothDampAngle(
                    currentYawDegrees,
                    targetYawDegrees,
                    ref yawVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
                currentPitchDegrees = Mathf.SmoothDampAngle(
                    currentPitchDegrees,
                    targetPitchDegrees,
                    ref pitchVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
                currentDistance = Mathf.SmoothDamp(
                    currentDistance,
                    targetDistance,
                    ref distanceVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
                currentOrthographicSize = Mathf.SmoothDamp(
                    currentOrthographicSize,
                    targetOrthographicSize,
                    ref orthographicSizeVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }

            ApplyCurrentPose();
        }

        public void Pan(double deltaX, double deltaY)
        {
            EnsureInitialized();
            State.Pan(deltaX, deltaY);
            currentPivot = targetPivot = coordinateConverter.ToWorldPosition(State.FocusCoordinate);
            CancelTransition();
            ApplyCurrentPose();
        }

        public void Zoom(float delta)
        {
            EnsureInitialized();
            State.Zoom(delta);
            currentOrthographicSize = targetOrthographicSize = State.OrthographicSize;
            currentDistance = targetDistance = DistanceForOrthographicSize(State.OrthographicSize);
            CancelTransition();
            ApplyCurrentPose();
        }

        public void Focus(TableCoordinate coordinate)
        {
            EnsureInitialized();
            State.SetFocus(coordinate);
            currentPivot = targetPivot = coordinateConverter.ToWorldPosition(coordinate);
            CancelTransition();
            ApplyCurrentPose();
        }

        public void Focus(TableCoordinate coordinate, float orthographicSize)
        {
            EnsureInitialized();
            State.SetFocus(coordinate, orthographicSize);
            currentPivot = targetPivot = coordinateConverter.ToWorldPosition(coordinate);
            currentOrthographicSize = targetOrthographicSize = State.OrthographicSize;
            currentDistance = targetDistance = DistanceForOrthographicSize(State.OrthographicSize);
            CancelTransition();
            ApplyCurrentPose();
        }

        public void PanFromScreen(Vector2 screenDelta, bool keyboardInput)
        {
            EnsureInitialized();
            ValidateFinite(screenDelta, nameof(screenDelta));
            InterruptTransitionForNavigation();

            Quaternion orientation = Quaternion.Euler(targetPitchDegrees, targetYawDegrees, 0f);
            Vector3 right = Vector3.ProjectOnPlane(orientation * Vector3.right, Vector3.up).normalized;
            Vector3 up = Vector3.ProjectOnPlane(orientation * Vector3.up, Vector3.up).normalized;
            if (up.sqrMagnitude <= 0.0001f)
            {
                up = Vector3.ProjectOnPlane(orientation * Vector3.forward, Vector3.up).normalized;
            }

            float distanceScale = Mathf.Max(0.1f, targetDistance * panDistanceScale);
            Vector3 worldDelta = keyboardInput
                ? ((right * screenDelta.x) + (up * screenDelta.y)) * distanceScale
                : ((right * -screenDelta.x) + (up * -screenDelta.y)) * distanceScale;
            SetTargetPivot(targetPivot + worldDelta);
        }

        public void Orbit(Vector2 pointerDelta)
        {
            EnsureInitialized();
            ValidateFinite(pointerDelta, nameof(pointerDelta));
            InterruptTransitionForNavigation();
            SwitchProjection(false, true);
            targetYawDegrees = NormalizeYaw(targetYawDegrees + (pointerDelta.x * orbitDegreesPerPixel));
            targetPitchDegrees = ClampPitch(targetPitchDegrees - (pointerDelta.y * orbitDegreesPerPixel));
        }

        public void ZoomAtScreenPoint(Vector2 screenPosition, float inputDelta)
        {
            EnsureInitialized();
            ValidateFinite(screenPosition, nameof(screenPosition));
            ValidateFinite(inputDelta, nameof(inputDelta));
            if (Mathf.Approximately(inputDelta, 0f))
            {
                return;
            }

            InterruptTransitionForNavigation();
            float previousZoom = usesOrthographicProjection
                ? targetOrthographicSize
                : targetDistance;
            float nextZoom = usesOrthographicProjection
                ? Mathf.Clamp(
                    previousZoom + (inputDelta * zoomSpeed),
                    minimumOrthographicSize,
                    maximumOrthographicSize)
                : Mathf.Clamp(
                    previousZoom + (inputDelta * zoomSpeed),
                    minimumDistance,
                    maximumDistance);
            if (Mathf.Approximately(previousZoom, nextZoom))
            {
                return;
            }

            if (usesOrthographicProjection)
            {
                targetOrthographicSize = nextZoom;
            }
            else
            {
                targetDistance = nextZoom;
                targetOrthographicSize = Mathf.Clamp(
                    PerspectiveHalfHeight(nextZoom),
                    minimumOrthographicSize,
                    maximumOrthographicSize);
            }

            State.SetFocus(coordinateConverter.ToTableCoordinate(targetPivot));
            State.SetOrthographicSize(targetOrthographicSize);
        }

        public void ConfigureFramingTargets(
            Bounds? localBounds,
            Bounds? sharedBoardBounds,
            float seatFacingYawDegrees,
            Bounds? mappingAreaBounds = null,
            GameObject mappingAreaRoot = null,
            Vector3? seatAnchor = null,
            Bounds? surroundingPlayerBounds = null)
        {
            EnsureInitialized();
            if (localBounds.HasValue)
            {
                ValidateBounds(localBounds.Value, nameof(localBounds));
            }

            if (sharedBoardBounds.HasValue)
            {
                ValidateBounds(sharedBoardBounds.Value, nameof(sharedBoardBounds));
            }

            ValidateFinite(seatFacingYawDegrees, nameof(seatFacingYawDegrees));
            if (mappingAreaBounds.HasValue)
            {
                ValidateBounds(mappingAreaBounds.Value, nameof(mappingAreaBounds));
            }

            if (seatAnchor.HasValue && !IsFinite(seatAnchor.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(seatAnchor));
            }

            if (surroundingPlayerBounds.HasValue)
            {
                ValidateBounds(surroundingPlayerBounds.Value, nameof(surroundingPlayerBounds));
            }

            localPlayerBounds = localBounds.GetValueOrDefault();
            boardBounds = sharedBoardBounds.GetValueOrDefault();
            relevantPlayerBounds = surroundingPlayerBounds.GetValueOrDefault();
            controllerMappingBounds = mappingAreaBounds.GetValueOrDefault();
            localSeatAnchor = seatAnchor.GetValueOrDefault();
            hasLocalPlayerBounds = localBounds.HasValue;
            hasBoardBounds = sharedBoardBounds.HasValue;
            hasRelevantPlayerBounds = surroundingPlayerBounds.HasValue;
            hasControllerMappingBounds = mappingAreaBounds.HasValue;
            hasLocalSeatAnchor = seatAnchor.HasValue;
            localSeatYawDegrees = NormalizeYaw(seatFacingYawDegrees);
            controllerMappingAreaRoot = mappingAreaRoot;
        }

        public void ClearFramingTargets()
        {
            hasLocalPlayerBounds = false;
            hasBoardBounds = false;
            hasRelevantPlayerBounds = false;
            hasControllerMappingBounds = false;
            hasLocalSeatAnchor = false;
            localPlayerBounds = default;
            boardBounds = default;
            relevantPlayerBounds = default;
            controllerMappingBounds = default;
            localSeatAnchor = default;
            localSeatYawDegrees = 0f;
            if (controllerMappingAreaRoot != null)
            {
                controllerMappingAreaRoot.SetActive(false);
            }
            controllerMappingAreaRoot = null;

        }

        public bool ShowPreset(TabletopCameraPreset preset, bool immediate = false)
        {
            return ShowPreset(preset, immediate, 1f);
        }

        private bool ShowPreset(
            TabletopCameraPreset preset,
            bool immediate,
            float framingDistanceMultiplier)
        {
            EnsureInitialized();
            ValidateFinite(framingDistanceMultiplier, nameof(framingDistanceMultiplier));
            if (framingDistanceMultiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(framingDistanceMultiplier));
            }

            TabletopCameraPresetTuning tuning = ResolvePreset(preset);
            if (!TryResolvePresetBounds(
                    preset,
                    tuning,
                    out Bounds framingBounds,
                    out _,
                    out _))
            {
                return false;
            }

            float pitch = ClampPitch(tuning.PitchDegrees);
            float yaw = NormalizeYaw(localSeatYawDegrees + tuning.YawOffsetDegrees);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float size = CalculateOrthographicSize(
                framingBounds,
                rotation,
                tuning.FramingPadding);
            bool useOrthographicProjection = preset == TabletopCameraPreset.TopDown
                && topDownUsesOrthographic;
            float fittedDistance = useOrthographicProjection
                ? DistanceForOrthographicSize(size)
                : CalculatePerspectiveDistance(
                    framingBounds,
                    rotation,
                    tuning.FramingPadding);
            float distance = Mathf.Clamp(
                fittedDistance
                    * Mathf.Max(0.01f, tuning.DistanceMultiplier)
                    * framingDistanceMultiplier,
                minimumDistance,
                maximumDistance);
            Vector3 pivot = framingBounds.center;
            pivot.y = tabletopHeight;

            SetPresetTarget(
                pivot,
                yaw,
                pitch,
                distance,
                size,
                useOrthographicProjection,
                immediate);
            if (controllerMappingAreaRoot != null)
            {
                controllerMappingAreaRoot.SetActive(
                    tuning.ShowControllerMappingArea && hasControllerMappingBounds);
            }

            return true;
        }

        public bool ShowCloseView(bool immediate = false)
        {
            return ShowPreset(TabletopCameraPreset.Close, immediate);
        }

        public bool ShowMidView(bool immediate = false)
        {
            return ShowPreset(TabletopCameraPreset.Mid, immediate);
        }

        public bool ShowBoardView(bool immediate = false)
        {
            return ShowPreset(TabletopCameraPreset.Board, immediate);
        }

        public bool ShowTopDownView(bool immediate = false)
        {
            return ShowPreset(TabletopCameraPreset.TopDown, immediate);
        }

        public bool ShowInitialGameView(bool immediate = false)
        {
            return ShowPreset(
                TabletopCameraPreset.Board,
                immediate,
                initialGameFramingDistanceMultiplier);
        }

        public void ShowDefaultView(bool immediate = false)
        {
            EnsureInitialized();
            SetPresetTarget(
                coordinateConverter.ToWorldPosition(TableCoordinate.Zero),
                initialYawDegrees,
                initialPitchDegrees,
                initialDistance,
                initialOrthographicSize,
                false,
                immediate);
        }

        public bool ReturnToLocalPlayer(bool immediate = false) => ShowCloseView(immediate);

        public TabletopCameraBookmark CaptureBookmark(string name)
        {
            EnsureInitialized();
            return new TabletopCameraBookmark(name, State.FocusCoordinate, State.OrthographicSize);
        }

        public void Focus(TabletopCameraBookmark bookmark)
        {
            EnsureInitialized();
            Focus(bookmark.FocusCoordinate, bookmark.OrthographicSize);
        }

        public void TransitionTo(TabletopCameraBookmark bookmark)
        {
            EnsureInitialized();
            Vector3 pivot = coordinateConverter.ToWorldPosition(bookmark.FocusCoordinate);
            float size = Mathf.Clamp(
                bookmark.OrthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            SetPresetTarget(
                pivot,
                targetYawDegrees,
                targetPitchDegrees,
                DistanceForOrthographicSize(size),
                size,
                false,
                false);
        }

        public void ApplyState()
        {
            EnsureInitialized();
            currentPivot = targetPivot = coordinateConverter.ToWorldPosition(State.FocusCoordinate);
            currentOrthographicSize = targetOrthographicSize = State.OrthographicSize;
            currentDistance = targetDistance = DistanceForOrthographicSize(State.OrthographicSize);
            CancelTransition();
            ApplyCurrentPose();
        }

        private void SetPresetTarget(
            Vector3 pivot,
            float yaw,
            float pitch,
            float distance,
            float orthographicSize,
            bool useOrthographicProjection,
            bool immediate)
        {
            SwitchProjection(useOrthographicProjection, true);
            targetPivot = SanitizePivot(pivot);
            targetYawDegrees = NormalizeYaw(yaw);
            targetPitchDegrees = ClampPitch(pitch);
            targetDistance = Mathf.Clamp(distance, minimumDistance, maximumDistance);
            targetOrthographicSize = Mathf.Clamp(
                orthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            State.SetFocus(coordinateConverter.ToTableCoordinate(targetPivot));
            State.SetOrthographicSize(targetOrthographicSize);

            if (immediate || presetTransitionDuration <= 0f)
            {
                currentPivot = targetPivot;
                currentYawDegrees = targetYawDegrees;
                currentPitchDegrees = targetPitchDegrees;
                currentDistance = targetDistance;
                currentOrthographicSize = targetOrthographicSize;
                CancelTransition();
                ApplyCurrentPose();
                return;
            }

            transitionStartPivot = currentPivot;
            transitionStartYaw = currentYawDegrees;
            transitionStartPitch = currentPitchDegrees;
            transitionStartDistance = currentDistance;
            transitionStartOrthographicSize = currentOrthographicSize;
            transitionElapsed = 0f;
            transitionDuration = presetTransitionDuration;
            transitionActive = true;
            ResetSmoothingVelocities();
        }

        private bool TryResolvePresetBounds(
            TabletopCameraPreset preset,
            TabletopCameraPresetTuning tuning,
            out Bounds resolvedBounds,
            out string source,
            out string failureReason)
        {
            resolvedBounds = default;
            source = string.Empty;
            failureReason = string.Empty;

            switch (preset)
            {
                case TabletopCameraPreset.Close:
                    if (hasLocalPlayerBounds)
                    {
                        resolvedBounds = localPlayerBounds;
                        source = "local Hand/Console";
                    }
                    else if (hasLocalSeatAnchor)
                    {
                        resolvedBounds = CreateAnchorBounds(localSeatAnchor);
                        source = "local Seat anchor fallback";
                    }
                    else
                    {
                        failureReason = "no local Hand, Console, or Seat anchor";
                        return false;
                    }

                    EncapsulateMappingAreaIfRequested(tuning, ref resolvedBounds, ref source);
                    return true;

                case TabletopCameraPreset.Mid:
                    bool hasMidBounds = false;
                    if (hasLocalPlayerBounds)
                    {
                        resolvedBounds = localPlayerBounds;
                        source = "local Hand/Console";
                        hasMidBounds = true;
                    }

                    if (hasBoardBounds)
                    {
                        Encapsulate(ref resolvedBounds, ref hasMidBounds, boardBounds);
                        source = AppendSource(source, "Board/play area");
                    }
                    else if (hasMidBounds)
                    {
                        source = AppendSource(source, "authored Mid fallback");
                    }

                    if (!hasLocalPlayerBounds && hasLocalSeatAnchor)
                    {
                        Encapsulate(
                            ref resolvedBounds,
                            ref hasMidBounds,
                            CreateAnchorBounds(localSeatAnchor));
                        source = AppendSource(source, "local Seat anchor fallback");
                    }

                    if (!hasMidBounds)
                    {
                        failureReason = "no local Hand/Console, local Seat anchor, or Board/play-area bounds";
                        return false;
                    }

                    EncapsulateMappingAreaIfRequested(tuning, ref resolvedBounds, ref source);
                    return true;

                case TabletopCameraPreset.Board:
                    if (!hasBoardBounds)
                    {
                        failureReason = "no active Board/play-area bounds";
                        return false;
                    }

                    resolvedBounds = boardBounds;
                    source = "Board/play area";
                    if (hasRelevantPlayerBounds)
                    {
                        resolvedBounds.Encapsulate(relevantPlayerBounds);
                        source = AppendSource(source, "relevant Player areas");
                    }

                    return true;

                case TabletopCameraPreset.TopDown:
                    if (hasBoardBounds)
                    {
                        resolvedBounds = boardBounds;
                        source = "Board/play area";
                    }
                    else
                    {
                        resolvedBounds = CreateAnchorBounds(targetPivot);
                        source = "current Camera pivot fallback";
                    }

                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(preset));
            }
        }

        private void EncapsulateMappingAreaIfRequested(
            TabletopCameraPresetTuning tuning,
            ref Bounds resolvedBounds,
            ref string source)
        {
            if (tuning.ShowControllerMappingArea && hasControllerMappingBounds)
            {
                resolvedBounds.Encapsulate(controllerMappingBounds);
                source = AppendSource(source, "Controller Mapping area");
            }
        }

        private Bounds CreateAnchorBounds(Vector3 anchor)
        {
            Vector3 center = SanitizePivot(anchor);
            return new Bounds(center, new Vector3(0f, 0.1f, 0f));
        }

        private static void Encapsulate(
            ref Bounds aggregate,
            ref bool hasAggregate,
            Bounds addition)
        {
            if (hasAggregate)
            {
                aggregate.Encapsulate(addition);
                return;
            }

            aggregate = addition;
            hasAggregate = true;
        }

        private static string AppendSource(string current, string addition)
        {
            return string.IsNullOrEmpty(current)
                ? addition
                : $"{current} + {addition}";
        }

        private float CalculateOrthographicSize(Bounds bounds, Quaternion rotation, float padding)
        {
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 extents = bounds.extents;
            float projectedHalfWidth =
                Mathf.Abs(right.x) * extents.x
                + Mathf.Abs(right.y) * extents.y
                + Mathf.Abs(right.z) * extents.z;
            float projectedHalfHeight =
                Mathf.Abs(up.x) * extents.x
                + Mathf.Abs(up.y) * extents.y
                + Mathf.Abs(up.z) * extents.z;
            float aspect = targetCamera.aspect > 0f ? targetCamera.aspect : 1f;
            float required = Mathf.Max(projectedHalfHeight, projectedHalfWidth / aspect)
                + Mathf.Max(0f, padding);
            return Mathf.Clamp(required, minimumOrthographicSize, maximumOrthographicSize);
        }

        private float CalculatePerspectiveDistance(
            Bounds bounds,
            Quaternion rotation,
            float padding)
        {
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 extents = bounds.extents;
            float projectedHalfWidth = ProjectExtent(extents, right) + Mathf.Max(0f, padding);
            float projectedHalfHeight = ProjectExtent(extents, up) + Mathf.Max(0f, padding);
            float projectedHalfDepth = ProjectExtent(extents, forward);
            float verticalTangent = Mathf.Tan(perspectiveFieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = targetCamera.aspect > 0f ? targetCamera.aspect : 1f;
            float horizontalTangent = verticalTangent * aspect;
            float framingDistance = Mathf.Max(
                projectedHalfHeight / verticalTangent,
                projectedHalfWidth / horizontalTangent);
            return projectedHalfDepth + framingDistance;
        }

        private static float ProjectExtent(Vector3 extents, Vector3 axis)
        {
            return Mathf.Abs(axis.x) * extents.x
                + Mathf.Abs(axis.y) * extents.y
                + Mathf.Abs(axis.z) * extents.z;
        }

        private void SetTargetPivot(Vector3 pivot)
        {
            targetPivot = SanitizePivot(pivot);
            State.SetFocus(coordinateConverter.ToTableCoordinate(targetPivot));
        }

        private Vector3 SanitizePivot(Vector3 pivot)
        {
            if (!IsFinite(pivot))
            {
                return new Vector3(0f, tabletopHeight, 0f);
            }

            pivot.y = tabletopHeight;
            return pivot;
        }

        private void ApplyCurrentPose()
        {
            Quaternion rotation = Quaternion.Euler(
                ClampPitch(currentPitchDegrees),
                NormalizeYaw(currentYawDegrees),
                0f);
            cameraRig.SetPositionAndRotation(currentPivot, rotation);
            targetCamera.transform.localPosition = Vector3.back
                * Mathf.Clamp(currentDistance, minimumDistance, maximumDistance);
            targetCamera.transform.localRotation = Quaternion.identity;
            targetCamera.orthographic = usesOrthographicProjection;
            targetCamera.fieldOfView = perspectiveFieldOfView;
            targetCamera.orthographicSize = Mathf.Clamp(
                currentOrthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
        }

        private void SwitchProjection(bool useOrthographicProjection, bool preserveVisibleScale)
        {
            if (usesOrthographicProjection == useOrthographicProjection)
            {
                return;
            }

            if (preserveVisibleScale)
            {
                if (useOrthographicProjection)
                {
                    currentOrthographicSize = Mathf.Clamp(
                        PerspectiveHalfHeight(currentDistance),
                        minimumOrthographicSize,
                        maximumOrthographicSize);
                    targetOrthographicSize = currentOrthographicSize;
                }
                else
                {
                    currentDistance = Mathf.Clamp(
                        PerspectiveDistanceForHalfHeight(currentOrthographicSize),
                        minimumDistance,
                        maximumDistance);
                    targetDistance = currentDistance;
                }
            }

            usesOrthographicProjection = useOrthographicProjection;
            ApplyCurrentPose();
        }

        private float PerspectiveHalfHeight(float distance)
        {
            return Mathf.Max(
                0.01f,
                distance * Mathf.Tan(perspectiveFieldOfView * 0.5f * Mathf.Deg2Rad));
        }

        private float PerspectiveDistanceForHalfHeight(float halfHeight)
        {
            return halfHeight / Mathf.Tan(perspectiveFieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        private void InterruptTransitionForNavigation()
        {
            if (!transitionActive)
            {
                return;
            }

            targetPivot = currentPivot;
            targetYawDegrees = currentYawDegrees;
            targetPitchDegrees = currentPitchDegrees;
            targetDistance = currentDistance;
            targetOrthographicSize = currentOrthographicSize;
            State.SetFocus(coordinateConverter.ToTableCoordinate(targetPivot));
            State.SetOrthographicSize(targetOrthographicSize);
            CancelTransition();
        }

        private void CancelTransition()
        {
            transitionActive = false;
            transitionElapsed = 0f;
            transitionDuration = 0f;
            ResetSmoothingVelocities();
        }

        private void ResetSmoothingVelocities()
        {
            pivotVelocity = Vector3.zero;
            yawVelocity = 0f;
            pitchVelocity = 0f;
            distanceVelocity = 0f;
            orthographicSizeVelocity = 0f;
        }

        private float DistanceForOrthographicSize(float orthographicSize)
        {
            float authoredInitialDistance = Mathf.Clamp(
                initialDistance,
                minimumDistance,
                maximumDistance);
            float authoredInitialSize = Mathf.Clamp(
                initialOrthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            if (orthographicSize <= authoredInitialSize)
            {
                if (authoredInitialSize - minimumOrthographicSize <= Mathf.Epsilon)
                {
                    return authoredInitialDistance;
                }

                return Mathf.Lerp(
                    minimumDistance,
                    authoredInitialDistance,
                    Mathf.InverseLerp(
                        minimumOrthographicSize,
                        authoredInitialSize,
                        orthographicSize));
            }

            if (maximumOrthographicSize - authoredInitialSize <= Mathf.Epsilon)
            {
                return authoredInitialDistance;
            }

            return Mathf.Lerp(
                authoredInitialDistance,
                maximumDistance,
                Mathf.InverseLerp(
                    authoredInitialSize,
                    maximumOrthographicSize,
                    orthographicSize));
        }

        private TabletopCameraPresetTuning ResolvePreset(TabletopCameraPreset preset)
        {
            switch (preset)
            {
                case TabletopCameraPreset.Close: return closeView;
                case TabletopCameraPreset.Mid: return midView;
                case TabletopCameraPreset.Board: return boardView;
                case TabletopCameraPreset.TopDown: return topDownView;
                default: throw new ArgumentOutOfRangeException(nameof(preset));
            }
        }

        private float ClampPitch(float value)
        {
            return Mathf.Clamp(value, minimumPitchDegrees, maximumPitchDegrees);
        }

        private static float NormalizeYaw(float value)
        {
            return Mathf.Repeat(value, 360f);
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

            if (!IsFinite(minimumDistance)
                || !IsFinite(maximumDistance)
                || minimumDistance <= 0f
                || maximumDistance < minimumDistance)
            {
                LogConfigurationError("TabletopCameraController requires a valid positive Camera distance range.");
                return false;
            }

            if (!IsFinite(initialDistance)
                || initialDistance <= 0f
                || !IsFinite(zoomSpeed)
                || zoomSpeed < 0f
                || !IsFinite(orbitDegreesPerPixel)
                || orbitDegreesPerPixel < 0f
                || !IsFinite(panDistanceScale)
                || panDistanceScale < 0f
                || !IsFinite(motionSmoothing)
                || motionSmoothing < 0f
                || !IsFinite(presetTransitionDuration)
                || presetTransitionDuration < 0f
                || !IsFinite(initialGameFramingDistanceMultiplier)
                || initialGameFramingDistanceMultiplier <= 0f
                || !IsFinite(tabletopHeight)
                || !IsFinite(perspectiveFieldOfView)
                || perspectiveFieldOfView <= 1f
                || perspectiveFieldOfView >= 179f)
            {
                LogConfigurationError("TabletopCameraController navigation tuning must be finite and non-negative.");
                return false;
            }

            if (!IsFinite(minimumPitchDegrees)
                || !IsFinite(maximumPitchDegrees)
                || minimumPitchDegrees <= 0f
                || maximumPitchDegrees >= 90f
                || maximumPitchDegrees < minimumPitchDegrees)
            {
                LogConfigurationError("TabletopCameraController requires pitch limits above zero and below ninety degrees.");
                return false;
            }

            return ValidatePreset(closeView, nameof(closeView))
                && ValidatePreset(midView, nameof(midView))
                && ValidatePreset(boardView, nameof(boardView))
                && ValidatePreset(topDownView, nameof(topDownView));
        }

        private bool ValidatePreset(TabletopCameraPresetTuning preset, string fieldName)
        {
            if (preset == null
                || !IsFinite(preset.PitchDegrees)
                || !IsFinite(preset.YawOffsetDegrees)
                || !IsFinite(preset.FramingPadding)
                || preset.FramingPadding < 0f
                || !IsFinite(preset.DistanceMultiplier)
                || preset.DistanceMultiplier <= 0f)
            {
                LogConfigurationError($"TabletopCameraController {fieldName} tuning is invalid.");
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

        private static void ValidateBounds(Bounds bounds, string parameterName)
        {
            if (!IsFinite(bounds.center)
                || !IsFinite(bounds.size)
                || bounds.size.x < 0f
                || bounds.size.y < 0f
                || bounds.size.z < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateFinite(Vector2 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
