using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float OutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }
    }

    /// <summary>Tiny update-driven tweener (unscaled time by default, so menus animate while paused).</summary>
    public class Tween : MonoBehaviour
    {
        class Job
        {
            public float Duration, Elapsed, Delay;
            public Func<float, float> Ease;
            public Action<float> Step;
            public Action Done;
            public bool Scaled;
            public object Owner;
            public bool Cancelled;
        }

        static Tween instance;
        readonly List<Job> jobs = new();
        readonly List<Job> adding = new();

        static Tween Instance
        {
            get
            {
                if (instance) return instance;
                var go = new GameObject("Tween");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Tween>();
                return instance;
            }
        }

        /// <summary>Animate t from 0 to 1. Passing an owner cancels earlier tweens of the same owner.</summary>
        public static void Run(float duration, Action<float> step, Func<float, float> ease = null, Action done = null,
            float delay = 0f, object owner = null, bool scaled = false)
        {
            if (owner != null) Cancel(owner);
            var j = new Job { Duration = Mathf.Max(1e-4f, duration), Step = step, Ease = ease ?? Ease.OutCubic, Done = done, Delay = delay, Owner = owner, Scaled = scaled };
            Instance.adding.Add(j);
        }

        public static void Cancel(object owner)
        {
            if (instance == null || owner == null) return;
            foreach (var j in instance.jobs) if (ReferenceEquals(j.Owner, owner)) j.Cancelled = true;
            foreach (var j in instance.adding) if (ReferenceEquals(j.Owner, owner)) j.Cancelled = true;
        }

        public static void Delay(float seconds, Action done, bool scaled = false) => Run(seconds, _ => { }, Ease.Linear, done, 0f, null, scaled);

        void Update()
        {
            if (adding.Count > 0) { jobs.AddRange(adding); adding.Clear(); }
            float udt = GameTime.UnscaledDelta, sdt = Time.deltaTime;
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                var j = jobs[i];
                if (j.Cancelled) { jobs.RemoveAt(i); continue; }
                float dt = j.Scaled ? sdt : udt;
                if (j.Delay > 0f) { j.Delay -= dt; if (j.Delay > 0f) continue; }
                j.Elapsed += dt;
                float t = Mathf.Clamp01(j.Elapsed / j.Duration);
                try { j.Step?.Invoke(j.Ease(t)); }
                catch (Exception e) { Debug.LogException(e); j.Cancelled = true; }
                if (t >= 1f && !j.Cancelled)
                {
                    jobs.RemoveAt(i);
                    try { j.Done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                }
            }
        }
    }
}
