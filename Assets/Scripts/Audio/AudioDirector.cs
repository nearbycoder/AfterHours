using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Ambience beds (HVAC, city, rain), per-room hums and the music player with ducking.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }
        LoopVoice hvac, city, rain, music, fridge;
        string currentTrack;
        AudioReverbZone reverb;

        public static AudioDirector Create()
        {
            var go = new GameObject("AudioDirector");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioDirector>();
            Instance.Build();
            return Instance;
        }

        void Build()
        {
            hvac = Sfx.Loop("amb_hvac", transform, false, AudioBus.Ambience);
            city = Sfx.Loop("amb_city", transform, false, AudioBus.Ambience);
            rain = Sfx.Loop("amb_rain", transform, false, AudioBus.Ambience);
            hvac.Attack = hvac.Release = 0.6f;
            city.Attack = city.Release = 0.6f;
            rain.Attack = rain.Release = 0.4f;
        }

        public void AttachOfficeSources(OfficeBuilder o)
        {
            // Fridge hum in the break room, fluorescent hum per lit room.
            fridge = Sfx.Loop("amb_fridge", null, true, AudioBus.Ambience);
            fridge.transform.position = new Vector3(5.25f, 1f, 3.7f);
            fridge.Source.maxDistance = 6f;
            fridge.TargetVolume = 0.35f;
            foreach (var r in o.Rooms.Values)
            {
                var hum = Sfx.Loop("fluoro_hum", null, true, AudioBus.Ambience);
                hum.transform.position = r.Bounds.center + Vector3.up * 1.2f;
                hum.Source.maxDistance = Mathf.Max(r.Bounds.extents.x, r.Bounds.extents.z) + 2f;
                hum.Source.minDistance = 1.5f;
                hum.Attack = 2f; hum.Release = 3f;
                r.Hum = hum;
            }
            reverb = gameObject.AddComponent<AudioReverbZone>();
            reverb.reverbPreset = AudioReverbPreset.Room;
            reverb.minDistance = 30f;
            reverb.maxDistance = 60f;
            transform.position = new Vector3(12, 1, 8);
        }

        public void SetNight(NightDef def)
        {
            hvac.TargetVolume = 0.32f;
            city.TargetVolume = def.Storm ? 0.12f : 0.22f;
            rain.TargetVolume = def.Storm ? 0.45f : 0f;
            // Calm for the first two nights, uneasy for the middle of the week, tense from the party on.
            PlayMusic(def.MusicIntensity > 0 ? "music_night_tense" : def.Number >= 3 ? "music_night_mid" : "music_night");
        }

        public void PlayMusic(string track, float volume = 0.5f)
        {
            if (currentTrack == track && music != null) { music.TargetVolume = volume; return; }
            if (music != null)
            {
                var old = music;
                old.TargetVolume = 0f;
                old.Release = 0.7f;
                Destroy(old.gameObject, 4f);
            }
            currentTrack = track;
            if (!Sfx.Has(track)) { music = null; return; }
            music = Sfx.Loop(track, transform, false, AudioBus.Music);
            music.Source.time = 0;
            music.Attack = 0.5f;
            music.TargetVolume = volume;
        }

        public void StopMusic()
        {
            if (music != null) music.TargetVolume = 0f;
            currentTrack = null;
        }

        public void SetAmbience(bool on)
        {
            hvac.TargetVolume = on ? 0.32f : 0f;
            city.TargetVolume = on ? 0.22f : 0f;
            if (!on) rain.TargetVolume = 0f;
        }
    }
}
