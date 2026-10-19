using ConsoleCards.Presentation.Settings;
using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>POL-8: what a piece is made of, for its impact sound.</summary>
    public enum TableSoundMaterial
    {
        Card = 0,
        Wood = 1,
        Chip = 2,
        Die = 3,
        Soft = 4,
    }

    /// <summary>
    /// POL-8, the sound pass: one place that plays the table's sounds. A few voices (each with its own pitch, so
    /// sounds playing together do not bend each other), a set of sounds per material picked at random with a small
    /// random pitch, volume from the impact speed, and quieter sounds when many play at once. One looping voice for
    /// the flick's charge. The sounds are WAV files made for the game in Resources/TableSounds (and CardSounds) and
    /// can be replaced there with the same names. Nothing is allocated while playing.
    /// </summary>
    public sealed class TableSoundPlayer
    {
        private const int VoiceCount = 8;
        private const int RecentCount = 16;
        private const float CrowdWindowSeconds = 0.12f;
        private const float MaterialMinimumGap = 0.035f;

        private static readonly string[][] MaterialClipPaths =
        {
            new[] { "CardSounds/CardTap" },
            new[] { "TableSounds/Wood1", "TableSounds/Wood2" },
            new[] { "TableSounds/Chip1", "TableSounds/Chip2" },
            new[] { "TableSounds/Die1", "TableSounds/Die2", "TableSounds/Die3" },
            new[] { "TableSounds/Soft1" },
        };

        private readonly GameObject root;
        private readonly AudioSource[] voices = new AudioSource[VoiceCount];
        private readonly AudioSource loop;
        private readonly AudioClip[][] materialClips;
        private readonly float[] lastMaterialTime;
        private readonly float[] recent = new float[RecentCount];
        private int recentIndex;
        private int nextVoice;

        public TableSoundPlayer(Transform parent)
        {
            root = new GameObject("Table Sounds");
            root.transform.SetParent(parent, false);
            for (int i = 0; i < VoiceCount; i++)
            {
                voices[i] = MakeSource(root);
            }

            loop = MakeSource(root);
            loop.loop = true;
            materialClips = new AudioClip[MaterialClipPaths.Length][];
            lastMaterialTime = new float[MaterialClipPaths.Length];
            for (int m = 0; m < MaterialClipPaths.Length; m++)
            {
                string[] paths = MaterialClipPaths[m];
                AudioClip[] loaded = new AudioClip[paths.Length];
                for (int c = 0; c < paths.Length; c++) loaded[c] = Resources.Load<AudioClip>(paths[c]);
                materialClips[m] = loaded;
                lastMaterialTime[m] = -1f;
            }

            for (int i = 0; i < RecentCount; i++) recent[i] = -1f;
        }

        /// <summary>A sound at this volume (0..1, before the master volume) with a small random pitch.</summary>
        public void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            FeelSettings feel = FeelSettings.Current;
            float jitter = feel.PitchJitter;
            float finalVolume = volume * feel.MasterVolume * CrowdFactor(feel.CrowdDamping);
            if (finalVolume <= 0.001f) return;
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % VoiceCount;
            voice.pitch = pitch * Random.Range(1f - jitter, 1f + jitter);
            voice.PlayOneShot(clip, Mathf.Clamp01(finalVolume));
            Remember();
        }

        /// <summary>A piece of this material hit something at this speed (world units a second).</summary>
        public void PlayImpact(TableSoundMaterial material, float speed)
        {
            FeelSettings feel = FeelSettings.Current;
            if (speed < feel.ImpactMinSpeed) return;
            int m = (int)material;
            if (m < 0 || m >= materialClips.Length) return;
            float now = Time.unscaledTime;
            if (now - lastMaterialTime[m] < MaterialMinimumGap) return;
            AudioClip[] set = materialClips[m];
            AudioClip clip = set.Length > 0 ? set[Random.Range(0, set.Length)] : null;
            if (clip == null) return;
            lastMaterialTime[m] = now;
            float strength = Mathf.InverseLerp(feel.ImpactMinSpeed, Mathf.Max(feel.ImpactMinSpeed + 0.01f, feel.ImpactFullSpeed), speed);
            // Harder hits are louder and a touch higher.
            Play(clip, feel.ImpactVolume * Mathf.Lerp(0.18f, 1f, strength), Mathf.Lerp(0.94f, 1.06f, strength));
        }

        public void StartLoop(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            if (loop.clip != clip) loop.clip = clip;
            SetLoop(volume, pitch);
            if (!loop.isPlaying) loop.Play();
        }

        public void SetLoop(float volume, float pitch)
        {
            loop.volume = Mathf.Clamp01(volume * FeelSettings.Current.MasterVolume);
            loop.pitch = pitch;
        }

        public void StopLoop()
        {
            if (loop != null && loop.isPlaying) loop.Stop();
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        // Each sound already playing in the last moment makes the next one quieter.
        private float CrowdFactor(float damping)
        {
            float now = Time.unscaledTime;
            int busy = 0;
            for (int i = 0; i < RecentCount; i++)
            {
                if (recent[i] >= 0f && now - recent[i] < CrowdWindowSeconds) busy++;
            }

            return busy <= 1 ? 1f : Mathf.Pow(damping, busy - 1);
        }

        private void Remember()
        {
            recent[recentIndex] = Time.unscaledTime;
            recentIndex = (recentIndex + 1) % RecentCount;
        }

        private static AudioSource MakeSource(GameObject owner)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
