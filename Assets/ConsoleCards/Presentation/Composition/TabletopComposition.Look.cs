using System;
using System.Collections.Generic;
using System.Reflection;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// V1, V5, V6, V7 (owner, 2026-10-27): the table's light and look, made at start in code (no scene or asset edits).
    /// <list type="bullet">
    /// <item>V1: three light presets, Day, Evening and Arcade (the default, with the circuit skybox): the scene's key
    /// light (colour, strength, angle, soft shadows), a fill light without shadows, the ambient colours, a reflection
    /// probe drawn once per change, and a global post-processing volume (neutral tonemapping, low bloom, a light
    /// vignette, the preset's colour grade). The project's SSAO gives the contact shadows under pieces.</item>
    /// <item>V5: a quiet hum of the console around the table (made in code, looped; Room sound in SETTINGS).</item>
    /// <item>V6: each preset is its own colour grade; colour-blind seat colours in SETTINGS.</item>
    /// <item>V7: Low, Medium and High quality (picked for the computer on first run): anti-aliasing, shadows, SSAO,
    /// bloom, the probe, texture size and the frame cap.</item>
    /// </list>
    /// Nothing here runs per frame.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float RoomSoundVolume = 0.05f;

        private GameObject lookRoot;
        private VolumeProfile lookProfile;
        private Bloom lookBloom;
        private Vignette lookVignette;
        private ColorAdjustments lookColour;
        private WhiteBalance lookWhite;
        private Light keyLight;
        private Light fillLight;
        private ReflectionProbe lookProbe;
        private AudioSource roomSound;
        private AudioClip roomSoundClip;
        private ScriptableRendererFeature ambientOcclusion;
        private bool ambientOcclusionWasActive;
        private bool coloursPending;

        private struct LightLook
        {
            public Color Key;
            public float KeyStrength;
            public Vector3 KeyAngles;
            public Color Fill;
            public float FillStrength;
            public Color Sky;
            public Color Horizon;
            public Color Ground;
            public float Exposure;
            public float Contrast;
            public float Saturation;
            public float Temperature;
            public float Bloom;
            public float BloomThreshold;
            public float Vignette;
        }

        private static LightLook LookFor(LightPreset preset)
        {
            switch (preset)
            {
                case LightPreset.Day:
                    return new LightLook
                    {
                        Key = new Color(1f, 0.96f, 0.88f), KeyStrength = 1.15f, KeyAngles = new Vector3(52f, -30f, 0f),
                        Fill = new Color(0.75f, 0.85f, 1f), FillStrength = 0.35f,
                        Sky = new Color(0.55f, 0.6f, 0.68f), Horizon = new Color(0.42f, 0.44f, 0.46f), Ground = new Color(0.2f, 0.2f, 0.22f),
                        Exposure = 0f, Contrast = 5f, Saturation = 0f, Temperature = 0f,
                        Bloom = 0.15f, BloomThreshold = 1.1f, Vignette = 0.15f,
                    };
                case LightPreset.Evening:
                    return new LightLook
                    {
                        Key = new Color(1f, 0.78f, 0.55f), KeyStrength = 0.95f, KeyAngles = new Vector3(30f, -60f, 0f),
                        Fill = new Color(0.45f, 0.5f, 0.85f), FillStrength = 0.3f,
                        Sky = new Color(0.35f, 0.33f, 0.45f), Horizon = new Color(0.3f, 0.25f, 0.25f), Ground = new Color(0.12f, 0.1f, 0.1f),
                        Exposure = 0f, Contrast = 10f, Saturation = 5f, Temperature = 15f,
                        Bloom = 0.3f, BloomThreshold = 1f, Vignette = 0.25f,
                    };
                default:
                    return new LightLook
                    {
                        Key = new Color(0.85f, 0.95f, 1f), KeyStrength = 0.9f, KeyAngles = new Vector3(60f, 20f, 0f),
                        Fill = new Color(0.2f, 0.88f, 0.78f), FillStrength = 0.45f,
                        Sky = new Color(0.1f, 0.28f, 0.3f), Horizon = new Color(0.12f, 0.16f, 0.2f), Ground = new Color(0.05f, 0.06f, 0.08f),
                        Exposure = -0.1f, Contrast = 12f, Saturation = 10f, Temperature = -10f,
                        Bloom = 0.6f, BloomThreshold = 0.9f, Vignette = 0.3f,
                    };
            }
        }

        // ---------- making it ----------

        private void EnsureLook()
        {
            if (lookRoot != null) return;
            try
            {
                lookRoot = new GameObject("Table Look");
                keyLight = RenderSettings.sun;
                if (keyLight == null)
                {
                    Light[] lights = FindObjectsByType<Light>();
                    for (int i = 0; i < lights.Length && keyLight == null; i++)
                    {
                        if (lights[i].type == LightType.Directional && lights[i].shadows != LightShadows.None) keyLight = lights[i];
                    }
                }

                GameObject fill = new GameObject("Fill Light");
                fill.transform.SetParent(lookRoot.transform, false);
                fillLight = fill.AddComponent<Light>();
                fillLight.type = LightType.Directional;
                fillLight.shadows = LightShadows.None;

                lookProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                lookProfile.name = "Table Look (runtime)";
                Tonemapping tonemapping = lookProfile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.Neutral);
                lookBloom = lookProfile.Add<Bloom>(true);
                lookVignette = lookProfile.Add<Vignette>(true);
                lookColour = lookProfile.Add<ColorAdjustments>(true);
                lookWhite = lookProfile.Add<WhiteBalance>(true);
                Volume volume = lookRoot.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = lookProfile;

                GameObject probe = new GameObject("Reflection Probe");
                probe.transform.SetParent(lookRoot.transform, false);
                probe.transform.localPosition = new Vector3(0f, 2f, 0f);
                lookProbe = probe.AddComponent<ReflectionProbe>();
                lookProbe.mode = ReflectionProbeMode.Realtime;
                lookProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                lookProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                lookProbe.resolution = 128;
                lookProbe.size = new Vector3(120f, 40f, 120f);
                lookProbe.clearFlags = ReflectionProbeClearFlags.Skybox;

                roomSound = lookRoot.AddComponent<AudioSource>();
                roomSound.loop = true;
                roomSound.playOnAwake = false;
                roomSound.spatialBlend = 0f;
                roomSound.volume = RoomSoundVolume;

                ambientOcclusion = FindAmbientOcclusion();
                if (ambientOcclusion != null) ambientOcclusionWasActive = ambientOcclusion.isActive;
                ApplyLook();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Look] The table's light could not be set up: {exception.Message}", this);
            }
        }

        private void ReleaseLook()
        {
            if (ambientOcclusion != null) ambientOcclusion.SetActive(ambientOcclusionWasActive); // the project asset as it was
            ambientOcclusion = null;
            if (lookProfile != null) Destroy(lookProfile);
            if (roomSoundClip != null) Destroy(roomSoundClip);
            if (lookRoot != null) Destroy(lookRoot);
            lookProfile = null;
            roomSoundClip = null;
            lookRoot = null;
        }

        // The project's SSAO (a renderer feature): switched off on Low, back as it was when the table goes.
        private static ScriptableRendererFeature FindAmbientOcclusion()
        {
            try
            {
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)) return null;
                FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
                if (!(field?.GetValue(pipeline) is ScriptableRendererData[] renderers)) return null;
                for (int r = 0; r < renderers.Length; r++)
                {
                    List<ScriptableRendererFeature> features = renderers[r] != null ? renderers[r].rendererFeatures : null;
                    if (features == null) continue;
                    for (int f = 0; f < features.Count; f++)
                    {
                        if (features[f] != null && features[f].GetType().Name == "ScreenSpaceAmbientOcclusion") return features[f];
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Look] The contact shadows (SSAO) could not be found: {exception.Message}");
            }

            return null;
        }

        // ---------- applying it ----------

        internal void ApplyLook()
        {
            if (lookRoot == null) return;
            LightLook look = LookFor(LookSettings.Light);
            GraphicsQuality quality = LookSettings.Quality;
            bool low = quality == GraphicsQuality.Low;

            if (keyLight != null)
            {
                keyLight.color = look.Key;
                keyLight.intensity = look.KeyStrength;
                keyLight.transform.rotation = Quaternion.Euler(look.KeyAngles);
                keyLight.shadows = low ? LightShadows.Hard : LightShadows.Soft;
            }

            fillLight.color = look.Fill;
            fillLight.intensity = look.FillStrength;
            fillLight.transform.rotation = Quaternion.Euler(35f, look.KeyAngles.y + 160f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.Sky;
            RenderSettings.ambientEquatorColor = look.Horizon;
            RenderSettings.ambientGroundColor = look.Ground;

            lookColour.postExposure.Override(look.Exposure);
            lookColour.contrast.Override(look.Contrast);
            lookColour.saturation.Override(look.Saturation);
            lookWhite.temperature.Override(look.Temperature);
            lookBloom.active = !low;
            lookBloom.intensity.Override(look.Bloom);
            lookBloom.threshold.Override(look.BloomThreshold);
            lookVignette.intensity.Override(look.Vignette);

            if (targetCamera != null)
            {
                UniversalAdditionalCameraData cameraData = targetCamera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                targetCamera.allowMSAA = !low;
            }

            if (ambientOcclusion != null) ambientOcclusion.SetActive(!low && ambientOcclusionWasActive);
            QualitySettings.globalTextureMipmapLimit = low ? 1 : 0;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = quality == GraphicsQuality.High ? Mathf.Max(60, DisplayRate()) : 60;

            lookProbe.enabled = !low;
            if (!low) lookProbe.RenderProbe();
            ApplyRoomSound();
        }

        private static int DisplayRate()
        {
            double rate = Screen.currentResolution.refreshRateRatio.value;
            return rate > 1d ? (int)Math.Round(rate) : 60;
        }

        private void ApplyRoomSound()
        {
            if (roomSound == null) return;
            if (!LookSettings.RoomSound)
            {
                roomSound.Stop();
                return;
            }

            if (roomSoundClip == null) roomSoundClip = MakeRoomSound();
            roomSound.clip = roomSoundClip;
            if (!roomSound.isPlaying) roomSound.Play();
        }

        // Six seconds of a soft console hum (a low tone, its octave and soft filtered noise, gently breathing),
        // looping without a seam: every tone fits a whole number of cycles, and the noise's end fades into its start.
        private static AudioClip MakeRoomSound()
        {
            const int rate = 22050;
            const int seconds = 6;
            int count = rate * seconds;
            float[] samples = new float[count];
            System.Random random = new System.Random(20261027);
            float low = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.02f; // a soft low-pass
                float breathe = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                samples[i] = breathe * (0.35f * Mathf.Sin(2f * Mathf.PI * 55f * t)
                    + 0.12f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                    + 0.05f * Mathf.Sin(2f * Mathf.PI * 220f * t + 0.6f)
                    + 0.9f * low);
            }

            int fade = rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                int end = count - fade + i;
                samples[end] = samples[end] * (1f - w) + samples[i] * w;
            }

            AudioClip clip = AudioClip.Create("Room Sound", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ---------- the main menu's SETTINGS (owner, 2026-10-29) ----------

        // The table's SETTINGS menu no longer has these rows (they are on the main menu): a SessionBar that has them
        // hides them.
        internal void RefreshLookSettingsUi()
        {
            runtimeUi?.ShowLookSettings(null, null, null, null, null, null, null, null);
        }

        internal void NextLightPreset(int step)
        {
            LookSettings.Light = (LightPreset)Wrap((int)LookSettings.Light + step, 3);
            ApplyLook();
        }

        internal void NextQuality(int step)
        {
            LookSettings.Quality = (GraphicsQuality)Wrap((int)LookSettings.Quality + step, 3);
            ApplyLook();
        }

        internal void ToggleRoomSound()
        {
            LookSettings.RoomSound = !LookSettings.RoomSound;
            ApplyRoomSound();
        }

        // The seat colours are on the pieces, the HUD and the log: the table behind the main menu is made again when
        // the player goes back to it (TABLE), or a new table is made with them. The game itself does not change.
        internal void ToggleColourBlind()
        {
            LookSettings.ColourBlind = !LookSettings.ColourBlind;
            coloursPending = !coloursPending;
        }

        internal void ForgetPendingColours() => coloursPending = false;

        internal void ApplyPendingColours()
        {
            if (!coloursPending) return;
            coloursPending = false;
            if (!IsInitialized || activeSession == null || templateContext == null || seatSwitchPending || rebuildingFromUndo) return;
            if (RebuildPresentationFor(templateContext, PlayerId.Empty, false, "Colours"))
            {
                ShowMessage(LookSettings.ColourBlind
                    ? "Colour-blind colours: blue, vermilion, yellow and pink."
                    : "Standard colours: blue, red, green and yellow.");
            }
        }

        private static int Wrap(int value, int count) => ((value % count) + count) % count;
    }
}
