using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Owner, 2026-10-28: the title intro. A gaming room with the table and the retro console; hovering the console
    /// lights it and lifts the cartridge, clicking it (or Enter / Space / the pad's A or Start) pushes the cartridge
    /// in, the camera dives into the slot and the screen powers on like a CRT onto the main menu, which is already
    /// open underneath. Built by Console Cards > UI > Build Title Intro; the art layers come from
    /// Content/UI/Intro/ and can be swapped in the prefab. Every art layer except the room is optional.
    /// </summary>
    public sealed class TitleIntroView : ReusableUiView
    {
        [Header("Art layers (inside Stage/Frame, all the size of the frame)")]
        [SerializeField] private RectTransform stage;
        [SerializeField] private RectTransform frame;
        [SerializeField] private RectTransform roomLayer;
        [SerializeField] private RectTransform consoleLayer;
        [SerializeField] private RectTransform cartridgeLayer;
        [SerializeField] private Image glow;
        [SerializeField] private RectTransform focus;
        [SerializeField] private RectTransform motes;

        [Header("Overlay")]
        [SerializeField] private CanvasGroup hud;
        [SerializeField] private CanvasGroup prompt;
        [SerializeField] private RectTransform promptLift;
        [SerializeField] private Text promptLabel;
        [SerializeField] private RectTransform promptLine;
        [SerializeField] private Image vignette;
        [SerializeField] private Image streaks;
        [SerializeField] private Image flash;
        [SerializeField] private RectTransform shutterTop;
        [SerializeField] private RectTransform shutterBottom;
        [SerializeField] private Image seam;
        [SerializeField] private Image scanlines;
        [SerializeField] private Image catcher; // clear, under the art: takes clicks so the menu under it gets none
        [SerializeField] private Image fade; // black, over everything: the fade in from black
        [SerializeField] private Button exitButton;

        [Header("Sound")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip insertClip;
        [SerializeField] private AudioClip powerClip;

        [Header("Feel")]
        [SerializeField] private Color accent = new Color(0.024f, 0.937f, 0.941f, 1f); // kit cyan #06EFF0
        [SerializeField] private Color idleLabel = new Color(0.91f, 0.925f, 0.929f, 1f); // kit words #E8ECED
        [SerializeField, Range(0f, 40f)] private float parallaxPixels = 14f;
        [SerializeField, Range(0.5f, 4f)] private float fadeInSeconds = 1.6f;
        [SerializeField, Range(0.4f, 3f)] private float diveSeconds = 1.35f;
        [SerializeField, Range(2f, 20f)] private float diveScale = 9f;
        [SerializeField, Range(0f, 60f)] private float cartridgeTravel = 26f;
        [SerializeField] private bool playOncePerRun = true;

        /// <summary>True while the intro covers the screen (the front end leaves its keys alone).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>True once the intro has played in this run (a profile switch reloads the scene).</summary>
        public static bool HasPlayed { get; private set; }

        private Action finished;
        private bool hovered;
        private bool started;
        private bool hurry;
        private float hoverAmount;
        private float glowBase = 0.35f;
        private Vector2 parallax;
        private Vector2 cartridgeHome;
        private Vector2 consoleHome;
        private Vector2 roomHome;
        private Coroutine running;
        private RectTransform[] moteDots = Array.Empty<RectTransform>();
        private Image[] moteImages = Array.Empty<Image>();
        private Vector4[] moteSeeds = Array.Empty<Vector4>();

        public bool ShouldPlay => !(playOncePerRun && HasPlayed);

        // Play mode without a domain reload keeps statics: each Play starts with the intro again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false;
            HasPlayed = false;
        }

        /// <summary>Plays the intro; <paramref name="done"/> runs when the main menu is fully shown.</summary>
        public void Bind(Action done)
        {
            RequireAcquired();
            finished = done;
            started = false;
            hurry = false;
            hovered = false;
            hoverAmount = 0f;
            HasPlayed = true;
            IsOpen = true;
            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(Quit);
            }

            Canvas.ForceUpdateCanvases(); // the frame's aspect fitter places the focus point before the first frame
            ResetLook();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FadeIn());
        }

        public override void Unbind()
        {
            if (exitButton != null) exitButton.onClick.RemoveAllListeners();
            finished = null;
            IsOpen = false;
        }

        public override void Hide()
        {
            IsOpen = false;
            base.Hide();
        }

        // ---------- Called by the hotspot and the prompt (TitleIntroTarget) ----------

        public void PointerEnter()
        {
            if (started) return;
            if (!hovered) Play(hoverClip, 0.35f);
            hovered = true;
        }

        public void PointerExit() => hovered = false;

        public void Activate()
        {
            if (started)
            {
                hurry = true; // a second click skips ahead
                return;
            }

            started = true;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Dive());
        }

        // ---------- Idle ----------

        private void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            ReadInput();
            if (started) return;

            hoverAmount = Mathf.MoveTowards(hoverAmount, hovered ? 1f : 0f, dt * 5f);
            float ease = Smooth(hoverAmount);

            // The room leans a little with the mouse (each layer a bit more, so the console floats over the room).
            Vector2 target = Vector2.zero;
            Mouse mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = mouse.position.ReadValue();
                target = new Vector2(p.x / Screen.width * 2f - 1f, p.y / Screen.height * 2f - 1f);
                target = Vector2.ClampMagnitude(target, 1.2f);
            }

            parallax = Vector2.Lerp(parallax, target, 1f - Mathf.Exp(-dt * 3f));
            Offset(roomLayer, roomHome, parallax * -parallaxPixels * 0.6f);
            Offset(consoleLayer, consoleHome, parallax * -parallaxPixels);
            Vector2 lift = new Vector2(0f, cartridgeTravel * 0.35f * ease);
            Offset(cartridgeLayer, cartridgeHome + lift, parallax * -parallaxPixels);

            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.7f);
            SetAlpha(glow, Mathf.Lerp(glowBase * (0.75f + 0.25f * breathe), 0.95f, ease));
            if (glow != null) glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, ease);

            if (promptLift != null) promptLift.anchoredPosition = new Vector2(0f, 8f * ease + 3f * breathe);
            if (promptLabel != null) promptLabel.color = Color.Lerp(idleLabel, accent, ease);
            if (promptLine != null) promptLine.localScale = new Vector3(Mathf.Lerp(0.55f, 1f, ease), 1f, 1f);

            AnimateMotes(dt);
        }

        private void ReadInput()
        {
            if (!UnityEngine.Application.isFocused) return;
            Keyboard keyboard = Keyboard.current;
            bool pressed = keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame);
            Gamepad pad = Gamepad.current;
            pressed |= pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
            if (started && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) pressed = true;
            if (pressed) Activate();
        }

        // ---------- Sequences ----------

        private IEnumerator FadeIn()
        {
            SetAlpha(fade, 1f);
            SetGroup(hud, 0f);
            SetGroup(prompt, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / fadeInSeconds;
                float e = Smooth(Mathf.Clamp01(t));
                // The black lifts first, then the camera settles from a slow push back.
                SetAlpha(fade, 1f - e);
                SetStage(Mathf.Lerp(1.08f, 1f, OutCubic(Mathf.Clamp01(t))), 0f);
                SetGroup(hud, Mathf.Clamp01((t - 0.55f) / 0.45f));
                SetGroup(prompt, Mathf.Clamp01((t - 0.7f) / 0.3f));
                yield return null;
            }

            SetAlpha(fade, 0f);
            SetStage(1f, 0f);
            SetGroup(hud, 1f);
            SetGroup(prompt, 1f);
            running = null;
        }

        private IEnumerator Dive()
        {
            Play(insertClip, 0.9f);
            SetAlpha(fade, 0f); // clicked during the fade in: no waiting for it
            float startScale = stage != null ? stage.localScale.x : 1f;
            float startGlow = glow != null ? glow.color.a : 0f;

            // 1. The cartridge clicks into the slot, the power light comes on, the words step aside.
            float t = 0f;
            float liftNow = cartridgeLayer != null ? cartridgeLayer.anchoredPosition.y - cartridgeHome.y : 0f;
            while (t < 1f)
            {
                t += Step(0.32f);
                float e = Mathf.Clamp01(t);
                SetGroup(hud, 1f - e);
                SetGroup(prompt, 1f - e);
                if (cartridgeLayer != null)
                {
                    float y = Mathf.Lerp(liftNow, -cartridgeTravel, InBack(e));
                    cartridgeLayer.anchoredPosition = new Vector2(cartridgeLayer.anchoredPosition.x, cartridgeHome.y + y);
                }

                SetAlpha(glow, Mathf.Lerp(startGlow, 1f, e));
                yield return null;
            }

            // 2. A short beat with the light on.
            Play(powerClip, 0.8f);
            for (float hold = 0f; hold < 0.18f && !hurry; hold += Time.unscaledDeltaTime) yield return null;

            // 3. The dive: the camera rushes into the slot, the room smears outward, light fills the screen.
            Vector2 parallaxAtStart = parallax;
            t = 0f;
            while (t < 1f)
            {
                t += Step(diveSeconds);
                float e = Mathf.Clamp01(t);
                float zoom = Mathf.Lerp(startScale, diveScale, Mathf.Lerp(InCubic(e), InExpo(e), 0.6f));
                float center = Smooth(Mathf.Clamp01(e * 1.6f));
                parallax = Vector2.Lerp(parallaxAtStart, Vector2.zero, center);
                Offset(roomLayer, roomHome, parallax * -parallaxPixels * 0.6f);
                Offset(consoleLayer, consoleHome, parallax * -parallaxPixels);
                SetStage(zoom, center);
                SetAlpha(streaks, Mathf.Clamp01((e - 0.35f) / 0.4f) * 0.75f);
                if (streaks != null) streaks.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.9f, e);
                SetAlpha(vignette, Mathf.Lerp(0.85f, 1f, e));
                SetAlpha(flash, InCubic(Mathf.Clamp01((e - 0.62f) / 0.38f)));
                yield return null;
            }

            // 4. Power on: the shutters close behind the light, the room goes, the light becomes one bright line,
            //    then the picture opens from the middle onto the main menu.
            if (stage != null) stage.gameObject.SetActive(false);
            SetAlpha(streaks, 0f);
            SetAlpha(vignette, 0f);
            SetShutters(0f);
            SetAlpha(seam, 1f);
            SetAlpha(scanlines, 0.45f);
            t = 0f;
            while (t < 1f)
            {
                t += Step(0.14f);
                SetAlpha(flash, 1f - Mathf.Clamp01(t));
                yield return null;
            }

            SetAlpha(flash, 0f);
            t = 0f;
            while (t < 1f)
            {
                t += Step(0.55f);
                float e = Mathf.Clamp01(t);
                SetShutters(OutCubic(e));
                if (seam != null) seam.rectTransform.localScale = new Vector3(Mathf.Lerp(1f, 1.2f, e), Mathf.Lerp(1f, 0.2f, e), 1f);
                SetAlpha(seam, 1f - Smooth(e));
                SetAlpha(scanlines, Mathf.Lerp(0.45f, 0.12f, e));
                yield return null;
            }

            // The scanlines fade off the menu, the way a warm tube settles.
            if (catcher != null) catcher.raycastTarget = false; // the menu is usable while the lines fade
            t = 0f;
            while (t < 1f)
            {
                t += Step(0.5f);
                SetAlpha(scanlines, Mathf.Lerp(0.12f, 0f, Mathf.Clamp01(t)));
                yield return null;
            }

            running = null;
            IsOpen = false;
            Action done = finished;
            finished = null;
            done?.Invoke(); // the owner releases (hides) the intro
        }

        // ---------- Helpers ----------

        private void ResetLook()
        {
            if (stage != null)
            {
                stage.gameObject.SetActive(true);
                stage.localScale = Vector3.one;
                stage.anchoredPosition = Vector2.zero;
            }

            roomHome = Home(roomLayer);
            consoleHome = Home(consoleLayer);
            cartridgeHome = Home(cartridgeLayer);
            SetAlpha(glow, glowBase);
            SetAlpha(streaks, 0f);
            SetAlpha(flash, 0f);
            SetAlpha(seam, 0f);
            SetAlpha(scanlines, 0f);
            SetAlpha(vignette, 0.85f);
            if (seam != null) seam.rectTransform.localScale = Vector3.one;
            SetShutters(1f);
            if (catcher != null) catcher.raycastTarget = true;
            SetAlpha(catcher, 0f);
            SetAlpha(fade, 1f);
            BuildMotes();
        }

        private static Vector2 Home(RectTransform rect)
        {
            // Layers are stretched over the frame; their resting place is the middle.
            if (rect != null) rect.anchoredPosition = Vector2.zero;
            return Vector2.zero;
        }

        /// <summary>Scales the stage around the focus point; <paramref name="toCenter"/> 0..1 slides it to the middle.</summary>
        private void SetStage(float scale, float toCenter)
        {
            if (stage == null) return;
            Vector2 p = Vector2.zero;
            if (focus != null)
            {
                // The focus point in the stage's own space (the stage is stretched with its pivot in the middle);
                // it does not change while the stage scales or moves, so the point stays put on screen.
                Vector3 local = stage.InverseTransformPoint(focus.position);
                p = new Vector2(local.x, local.y);
            }

            stage.localScale = new Vector3(scale, scale, 1f);
            stage.anchoredPosition = -p * scale + p * (1f - toCenter);
        }

        private void SetShutters(float open)
        {
            // open 0: both halves meet in the middle (black screen); 1: both are off screen.
            if (shutterTop != null)
            {
                shutterTop.anchorMin = new Vector2(0f, Mathf.Lerp(0.5f, 1f, open));
                shutterTop.anchorMax = new Vector2(1f, 1f + open * 0.02f);
                shutterTop.gameObject.SetActive(open < 1f);
            }

            if (shutterBottom != null)
            {
                shutterBottom.anchorMin = new Vector2(0f, -open * 0.02f);
                shutterBottom.anchorMax = new Vector2(1f, Mathf.Lerp(0.5f, 0f, open));
                shutterBottom.gameObject.SetActive(open < 1f);
            }
        }

        private void BuildMotes()
        {
            if (motes == null) return;
            int count = motes.childCount;
            if (moteDots.Length != count)
            {
                moteDots = new RectTransform[count];
                moteImages = new Image[count];
                moteSeeds = new Vector4[count];
                var random = new System.Random(1985); // the year of the console in the picture; any seed works
                for (int i = 0; i < count; i++)
                {
                    moteDots[i] = motes.GetChild(i) as RectTransform;
                    moteImages[i] = moteDots[i] != null ? moteDots[i].GetComponent<Image>() : null;
                    moteSeeds[i] = new Vector4(
                        (float)random.NextDouble(),
                        (float)random.NextDouble(),
                        0.15f + (float)random.NextDouble() * 0.5f,
                        (float)random.NextDouble() * 6.28f);
                }
            }
        }

        private void AnimateMotes(float dt)
        {
            if (motes == null || moteDots.Length == 0) return;
            Rect area = motes.rect;
            float time = Time.unscaledTime;
            for (int i = 0; i < moteDots.Length; i++)
            {
                RectTransform dot = moteDots[i];
                if (dot == null) continue;
                Vector4 s = moteSeeds[i];
                float y = Mathf.Repeat(s.y + time * s.z * 0.04f, 1f);
                float x = s.x + Mathf.Sin(time * 0.3f + s.w) * 0.02f;
                dot.anchoredPosition = new Vector2(
                    (x - 0.5f) * area.width + parallax.x * -parallaxPixels * 1.6f,
                    (y - 0.5f) * area.height);
                Image image = moteImages[i];
                if (image != null)
                {
                    float fade = Mathf.Sin(y * Mathf.PI); // in at the bottom, out at the top
                    SetAlpha(image, fade * (0.25f + 0.25f * Mathf.Sin(time * 2f + s.w)));
                }
            }
        }

        private float Step(float seconds) => Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds) * (hurry ? 3f : 1f);

        private void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

        private static void Quit()
        {
            UnityEngine.Application.Quit();
#if UNITY_EDITOR
            Debug.Log("Title intro: EXIT quits the built game (the Editor keeps running).");
#endif
        }

        private static void Offset(RectTransform rect, Vector2 home, Vector2 offset)
        {
            if (rect != null) rect.anchoredPosition = home + offset;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        private static void SetGroup(CanvasGroup group, float alpha)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.interactable = alpha > 0.5f;
            group.blocksRaycasts = alpha > 0.5f;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float InCubic(float t) => t * t * t;
        private static float InExpo(float t) => t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);

        private static float InBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        protected override void OnDestroy()
        {
            IsOpen = false;
            base.OnDestroy();
        }
    }
}
