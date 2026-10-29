using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConsoleCards.Intro
{
    /// <summary>
    /// Owner, 2026-10-29 (doc 37): the title intro scene. The gaming room drifts slowly; hovering the console lights it
    /// and lifts ENTER; clicking it (or Enter / Space / the pad's A or Start) runs a light across its buttons, powers
    /// the screen up, and dives the camera into the screen. At the white flash the table scene, loaded in the
    /// background meanwhile, takes over and <see cref="IntroPowerOn"/> opens the picture onto the main menu.
    /// Console Cards &gt; Intro &gt; Build Intro Scene makes the scene and sets every reference below.
    /// </summary>
    public sealed class IntroDirector : MonoBehaviour
    {
        private enum Phase
        {
            FadeIn,
            Idle,
            Boot,
            Dive,
            Done,
        }

        [Header("Scene")]
        [SerializeField] private Camera shotCamera;
        [SerializeField] private Collider consoleHit;
        [SerializeField] private Transform screenFocus;
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Light consoleLight;
        [SerializeField] private Light sweepLight;
        [SerializeField] private Vector3 sweepFrom;
        [SerializeField] private Vector3 sweepTo;
        [SerializeField] private Light[] pulseLights = Array.Empty<Light>();
        [SerializeField] private Volume volume;

        [Header("Shot")]
        [SerializeField] private Vector3 restPosition = new Vector3(0f, 2.02f, 3.3f);
        [SerializeField] private Vector3 restTarget = new Vector3(0f, 0.78f, -0.22f);
        [SerializeField, Range(20f, 70f)] private float restFov = 42f;
        [SerializeField, Range(10f, 60f)] private float diveFov = 30f;
        [SerializeField, Range(0.5f, 4f)] private float fadeSeconds = 1.4f;
        [SerializeField, Range(0.2f, 2f)] private float bootSeconds = 0.75f;
        [SerializeField, Range(0.6f, 4f)] private float diveSeconds = 1.6f;
        [SerializeField, Range(0f, 0.3f)] private float sway = 0.06f;
        [SerializeField, Range(0f, 0.4f)] private float parallax = 0.12f;

        [Header("Console screen (emission strength)")]
        [SerializeField] private float screenIdle = 0.35f;
        [SerializeField] private float screenHover = 0.9f;
        [SerializeField] private float screenBoot = 1.5f;
        [SerializeField] private float consoleLightIdle = 0.25f;
        [SerializeField] private float consoleLightHover = 1.6f;

        [Header("Words")]
        [SerializeField] private RectTransform prompt;
        [SerializeField] private CanvasGroup promptGroup;
        [SerializeField] private RectTransform promptLift;
        [SerializeField] private Text promptWord;
        [SerializeField] private RectTransform promptLine;
        [SerializeField] private CanvasGroup hud;
        [SerializeField] private Button exitButton;
        [SerializeField] private Color accent = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color idleWord = new Color(0.91f, 0.925f, 0.929f, 1f);

        [Header("Sound")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip bootClip;

        [Header("Next")]
        [SerializeField] private string nextScene = "Tabletop";

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private Phase phase = Phase.FadeIn;
        private float phaseStart;
        private float hover;
        private bool hot;
        private bool hurry;
        private Vector2 mouse = new Vector2(0.5f, 0.5f);
        private Vector2 lean;
        private Vector3 fromPosition;
        private Vector3 fromTarget;
        private float[] pulseBase = Array.Empty<float>();
        private MaterialPropertyBlock screenBlock;
        private IntroPowerOn layer;
        private AsyncOperation load;
        private DepthOfField depthOfField;
        private ChromaticAberration chromatic;
        private LensDistortion lens;
        private MotionBlur motionBlur;
        private float ignoreClicksUntil;

        private void Start()
        {
            layer = IntroPowerOn.Create();
            layer.Fade = 1f;
            phase = Phase.FadeIn;
            phaseStart = Time.unscaledTime;
            screenBlock = new MaterialPropertyBlock();
            pulseBase = new float[pulseLights.Length];
            for (int i = 0; i < pulseLights.Length; i++) pulseBase[i] = pulseLights[i] != null ? pulseLights[i].intensity : 0f;
            if (sweepLight != null) sweepLight.intensity = 0f;
            if (exitButton != null) exitButton.onClick.AddListener(Quit);
            if (volume != null && volume.profile != null)
            {
                VolumeProfile profile = volume.profile; // a copy for this run; the asset is not changed
                profile.TryGet(out depthOfField);
                profile.TryGet(out chromatic);
                profile.TryGet(out lens);
                profile.TryGet(out motionBlur);
            }

            PlaceCamera(restPosition, restTarget, restFov);
            SetScreen(screenIdle);
            StartLoading();
        }

        // The table scene loads while the room is shown, so the switch at the flash is quick.
        private void StartLoading()
        {
            if (string.IsNullOrEmpty(nextScene) || !UnityEngine.Application.CanStreamedLevelBeLoaded(nextScene))
            {
                Debug.LogWarning($"Intro: the scene '{nextScene}' is not in Build Settings, so the intro cannot open it.");
                return;
            }

            UnityEngine.Application.backgroundLoadingPriority = ThreadPriority.Low;
            load = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
            if (load != null) load.allowSceneActivation = false;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = now - phaseStart;
            ReadPointer();
            PulseLights(now);

            switch (phase)
            {
                case Phase.FadeIn:
                    layer.Fade = 1f - Smooth(Mathf.Clamp01(t / fadeSeconds));
                    Idle(now, dt, 1f - OutCubic(Mathf.Clamp01(t / (fadeSeconds + 0.2f))));
                    SetGroup(promptGroup, Mathf.Clamp01((t - fadeSeconds * 0.7f) / 0.6f));
                    SetGroup(hud, Mathf.Clamp01((t - fadeSeconds * 0.5f) / 0.6f));
                    if (t >= fadeSeconds + 0.2f) Enter(Phase.Idle);
                    break;
                case Phase.Idle:
                    Idle(now, dt, 0f);
                    break;
                case Phase.Boot:
                    Boot(t);
                    break;
                case Phase.Dive:
                    Dive(t);
                    break;
            }

            PlacePrompt();
            ReadKeys();
        }

        // ---------- idle ----------

        private void Idle(float now, float dt, float push)
        {
            hot = phase == Phase.Idle && PointerOnConsole();
            hover = Mathf.MoveTowards(hover, hot ? 1f : 0f, dt * 5f);
            float e = Smooth(hover);
            Vector2 target = (mouse - new Vector2(0.5f, 0.5f)) * 2f;
            lean = Vector2.Lerp(lean, target, 1f - Mathf.Exp(-dt * 2.5f));
            float drift = Mathf.Sin(now * 0.23f) * sway;
            Vector3 position = restPosition + new Vector3(drift - lean.x * parallax, -lean.y * parallax * 0.5f + push * 0.25f, push);
            Vector3 target3 = restTarget + new Vector3(-lean.x * parallax * 0.25f, 0f, 0f);
            PlaceCamera(position, target3, restFov);

            SetScreen(Mathf.Lerp(screenIdle, screenHover, e) + Mathf.Sin(now * 2.1f) * 0.03f);
            if (consoleLight != null) consoleLight.intensity = Mathf.Lerp(consoleLightIdle, consoleLightHover, e);
            if (promptWord != null) promptWord.color = Color.Lerp(idleWord, accent, e);
            if (promptLine != null) promptLine.localScale = new Vector3(Mathf.Lerp(0.55f, 1f, e), 1f, 1f);
            if (promptLift != null) promptLift.anchoredPosition = new Vector2(0f, 8f * e + 2f * Mathf.Sin(now * 1.7f));
        }

        public void Activate()
        {
            if (phase == Phase.Boot || phase == Phase.Dive)
            {
                hurry = true; // a second press skips ahead
                return;
            }

            if (phase != Phase.Idle && phase != Phase.FadeIn) return;
            layer.Fade = 0f;
            Play(bootClip, 0.8f);
            UnityEngine.Application.backgroundLoadingPriority = ThreadPriority.High;
            Enter(Phase.Boot);
        }

        // ---------- boot: a light runs across the buttons, the screen powers up ----------

        private void Boot(float t)
        {
            float k = Mathf.Clamp01(t / (bootSeconds / Speed));
            SetGroup(promptGroup, 1f - k);
            SetGroup(hud, 1f - k);
            if (sweepLight != null)
            {
                sweepLight.transform.position = Vector3.Lerp(sweepFrom, sweepTo, Smooth(k));
                sweepLight.intensity = Mathf.Sin(k * Mathf.PI) * 3f;
            }

            if (consoleLight != null) consoleLight.intensity = Mathf.Lerp(consoleLightHover, consoleLightHover * 1.4f, k);
            float flicker = k > 0.4f ? Mathf.Sin(Time.unscaledTime * 60f) * 0.08f : 0f;
            SetScreen(Mathf.Lerp(screenHover, screenBoot, Smooth(k)) + flicker);
            if (k >= 1f)
            {
                fromPosition = shotCamera.transform.position;
                fromTarget = shotCamera.transform.position + shotCamera.transform.forward * Vector3.Distance(restPosition, restTarget);
                if (depthOfField != null) depthOfField.active = false;
                Enter(Phase.Dive);
            }
        }

        // ---------- dive into the screen ----------

        private void Dive(float t)
        {
            float k = Mathf.Clamp01(t / (diveSeconds / Speed));
            float e = InOutCubic(k);
            Vector3 screen = screenFocus != null ? screenFocus.position : restTarget;
            Vector3 end = screen + new Vector3(0f, 0.06f, 0.035f);
            Vector3 c1 = fromPosition + new Vector3(0f, 0.25f, -0.9f);
            Vector3 c2 = screen + new Vector3(0f, 0.65f, 0.35f);
            Vector3 position = Bezier(fromPosition, c1, c2, end, e);
            Vector3 target = Vector3.Lerp(fromTarget, screen, Smooth(Mathf.Clamp01(k * 1.4f)));
            PlaceCamera(position, target, Mathf.Lerp(restFov, diveFov, e));

            float late = Mathf.Clamp01((k - 0.55f) / 0.45f);
            if (chromatic != null) chromatic.intensity.value = late * late * 0.9f;
            if (lens != null) lens.intensity.value = -0.4f * late * late * late;
            if (motionBlur != null) motionBlur.intensity.value = 0.6f * late;
            SetScreen(screenBoot + 0.4f * e + Mathf.Pow(Mathf.Clamp01((k - 0.72f) / 0.28f), 3f) * 6f);
            layer.Flash = Mathf.Pow(Mathf.Clamp01((k - 0.8f) / 0.2f), 3f);

            if (k >= 1f)
            {
                Enter(Phase.Done);
                layer.Flash = 1f;
                if (load != null)
                {
                    layer.RevealAfter(load);
                }
                else
                {
                    Debug.LogWarning("Intro: nothing to open after the dive (the table scene was not loading).");
                    layer.RevealAfter(null);
                }
            }
        }

        // ---------- helpers ----------

        private float Speed => hurry ? 3f : 1f;

        private void Enter(Phase next)
        {
            phase = next;
            phaseStart = Time.unscaledTime;
        }

        private void ReadPointer()
        {
            Mouse m = Mouse.current;
            if (m == null || Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 p = m.position.ReadValue();
            mouse = new Vector2(p.x / Screen.width, 1f - p.y / Screen.height);
            if (m.leftButton.wasPressedThisFrame && Time.unscaledTime > ignoreClicksUntil)
            {
                bool overButton = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overButton && (phase == Phase.Boot || phase == Phase.Dive)) Activate();
                else if (!overButton && PointerOnConsole()) Activate();
            }
        }

        private void ReadKeys()
        {
            if (!UnityEngine.Application.isFocused) return;
            Keyboard k = Keyboard.current;
            bool pressed = k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame);
            if (k != null && k.escapeKey.wasPressedThisFrame && (phase == Phase.Boot || phase == Phase.Dive)) pressed = true;
            Gamepad pad = Gamepad.current;
            pressed |= pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
            if (pressed) Activate();
        }

        private bool PointerOnConsole()
        {
            Mouse m = Mouse.current;
            if (m == null || shotCamera == null) return false;
            Vector2 p = m.position.ReadValue();
            if (prompt != null && promptGroup != null && promptGroup.alpha > 0.5f
                && RectTransformUtility.RectangleContainsScreenPoint(prompt, p, null))
            {
                return true;
            }

            return consoleHit != null && consoleHit.Raycast(shotCamera.ScreenPointToRay(p), out _, 50f);
        }

        private void PlacePrompt()
        {
            if (prompt == null || shotCamera == null || screenFocus == null) return;
            Vector3 anchor = screenFocus.position + Vector3.up * 0.38f;
            Vector3 s = shotCamera.WorldToScreenPoint(anchor);
            prompt.position = new Vector3(s.x, s.y, 0f);
        }

        private void PlaceCamera(Vector3 position, Vector3 target, float fov)
        {
            if (shotCamera == null) return;
            shotCamera.transform.position = position;
            Vector3 forward = target - position;
            if (forward.sqrMagnitude > 1e-8f) shotCamera.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            float aspect = shotCamera.aspect;
            shotCamera.fieldOfView = aspect < 1f ? Mathf.Min(fov * 1.6f, fov / aspect * 0.9f) : fov; // phones: the table still fits
        }

        private void SetScreen(float strength)
        {
            if (screenRenderer == null) return;
            screenRenderer.GetPropertyBlock(screenBlock);
            screenBlock.SetColor(EmissionColor, Color.white * Mathf.Max(0f, strength));
            screenRenderer.SetPropertyBlock(screenBlock);
        }

        private void PulseLights(float now)
        {
            float pulse = 0.92f + 0.08f * Mathf.Sin(now * 1.3f);
            for (int i = 0; i < pulseLights.Length; i++)
            {
                if (pulseLights[i] != null) pulseLights[i].intensity = pulseBase[i] * pulse;
            }
        }

        private void Play(AudioClip clip, float volumeScale)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volumeScale);
        }

        private static void SetGroup(CanvasGroup group, float alpha)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.interactable = alpha > 0.5f;
            group.blocksRaycasts = alpha > 0.5f;
        }

        private void Quit()
        {
            ignoreClicksUntil = Time.unscaledTime + 0.3f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        private void OnDestroy()
        {
            if (exitButton != null) exitButton.onClick.RemoveListener(Quit);
            UnityEngine.Application.backgroundLoadingPriority = ThreadPriority.Normal;
        }
    }
}
