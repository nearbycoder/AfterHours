using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AfterHours
{
    public enum AudioBus { Sfx, Music, Ambience, Ui }

    /// <summary>
    /// Pooled one-shot and loop playback. Clips live in Resources/Audio; files named
    /// <c>name_1.wav, name_2.wav…</c> are variations of <c>name</c> picked at random.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx instance;
        readonly Dictionary<string, List<AudioClip>> banks = new();
        readonly List<AudioSource> pool = new();
        readonly Dictionary<string, int> lastPick = new();
        readonly Dictionary<string, float> lastPlayed = new();
        int next;

        public static float Duck = 1f;          // music ducking multiplier (set by AudioDirector)

        public static Sfx Instance
        {
            get
            {
                if (instance) return instance;
                var go = new GameObject("Sfx");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Sfx>();
                instance.Load();
                return instance;
            }
        }

        void Load()
        {
            var rx = new Regex(@"^(.*?)(_\d+)?$");
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio"))
            {
                var key = rx.Match(clip.name).Groups[1].Value;
                if (!banks.TryGetValue(key, out var list)) banks[key] = list = new List<AudioClip>();
                list.Add(clip);
            }
            for (int i = 0; i < 40; i++)
            {
                var go = new GameObject("voice" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                pool.Add(src);
            }
        }

        public static float BusVolume(AudioBus bus)
        {
            var s = Settings.Current;
            float v = s.MasterVolume;
            return bus switch
            {
                AudioBus.Music => v * s.MusicVolume,
                AudioBus.Ambience => v * s.AmbienceVolume,
                _ => v * s.SfxVolume,
            };
        }

        public static bool Has(string name) => Instance.banks.ContainsKey(name);

        public static AudioClip Pick(string name)
        {
            var self = Instance;
            if (!self.banks.TryGetValue(name, out var list) || list.Count == 0) return null;
            if (list.Count == 1) return list[0];
            self.lastPick.TryGetValue(name, out int last);
            int i = Random.Range(0, list.Count - 1);
            if (i >= last) i++;
            self.lastPick[name] = i;
            return list[i];
        }

        /// <summary>Play a one-shot. <paramref name="pos"/> null = 2D.</summary>
        public static AudioSource Play(string name, Vector3? pos = null, float volume = 1f, float pitch = 1f,
            float pitchJitter = 0.06f, AudioBus bus = AudioBus.Sfx, float minGap = 0.025f, float spatialRange = 14f)
        {
            var self = Instance;
            float now = Time.unscaledTime;
            if (self.lastPlayed.TryGetValue(name, out var t) && now - t < minGap) return null;
            var clip = Pick(name);
            if (clip == null) return null;
            self.lastPlayed[name] = now;
            var src = self.NextVoice();
            src.clip = clip;
            src.loop = false;
            src.volume = volume * BusVolume(bus);
            src.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            src.ignoreListenerPause = bus == AudioBus.Ui;
            if (pos.HasValue)
            {
                src.transform.position = pos.Value;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1.2f;
                src.maxDistance = spatialRange;
                src.dopplerLevel = 0f;
                src.spread = 40f;
            }
            else
            {
                src.spatialBlend = 0f;
            }
            src.Play();
            return src;
        }

        AudioSource NextVoice()
        {
            for (int k = 0; k < pool.Count; k++)
            {
                var s = pool[(next + k) % pool.Count];
                if (!s.isPlaying) { next = (next + k + 1) % pool.Count; return s; }
            }
            var steal = pool[next];
            next = (next + 1) % pool.Count;
            return steal;
        }

        /// <summary>Create a persistent loop voice whose volume and pitch glide to targets.</summary>
        public static LoopVoice Loop(string name, Transform parent = null, bool spatial = false, AudioBus bus = AudioBus.Sfx)
        {
            var go = new GameObject("loop_" + name);
            go.transform.SetParent(parent ? parent : Instance.transform, false);
            var v = go.AddComponent<LoopVoice>();
            v.Init(Pick(name), spatial, bus);
            return v;
        }
    }

    /// <summary>A looping source with smoothed volume and pitch.</summary>
    public class LoopVoice : MonoBehaviour
    {
        public AudioSource Source { get; private set; }
        public float TargetVolume, TargetPitch = 1f;
        public float Attack = 14f, Release = 7f, PitchGlide = 8f;
        public AudioBus Bus;
        float volume;

        public void Init(AudioClip clip, bool spatial, AudioBus bus)
        {
            Bus = bus;
            Source = gameObject.AddComponent<AudioSource>();
            Source.clip = clip;
            Source.loop = true;
            Source.playOnAwake = false;
            Source.volume = 0f;
            Source.spatialBlend = spatial ? 1f : 0f;
            Source.rolloffMode = AudioRolloffMode.Linear;
            Source.minDistance = 1f;
            Source.maxDistance = 10f;
            Source.dopplerLevel = 0f;
            if (clip != null)
            {
                Source.time = Random.Range(0f, clip.length * 0.9f);
                Source.Play();
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float k = TargetVolume > volume ? Attack : Release;
            volume = Mathf.Lerp(volume, TargetVolume, 1f - Mathf.Exp(-dt * k));
            Source.volume = volume * Sfx.BusVolume(Bus) * (Bus == AudioBus.Music ? Sfx.Duck : 1f);
            Source.pitch = Mathf.Lerp(Source.pitch, TargetPitch, 1f - Mathf.Exp(-dt * PitchGlide));
            if (!Source.isPlaying && Source.clip != null && Time.timeScale > 0f) Source.Play();
        }
    }
}
