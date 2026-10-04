using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Gameplay video: <c>-ahShowcase &lt;dir&gt;</c>. Plays the AutoPilot route at a fixed 30 fps game clock,
    /// walking up to things and cleaning on camera during the highlight nights, and writes every recorded
    /// frame to <c>dir/frames/NNNNNN.jpg</c> plus the mixed game audio to <c>dir/audio.wav</c>.
    /// Nights and stretches that aren't recorded run unfilmed so the story still reaches its ending.
    /// </summary>
    public class Showcase : AutoPilot
    {
        protected override string ArgName => "-ahShowcase";
        const int Fps = 30;
        static readonly HashSet<int> Filmed = new() { 1, 2, 3, 4, 5, 7 };

        bool rec;
        int frame;
        string framesDir;
        readonly List<Task> pending = new();
        readonly List<float> audio = new();
        int channels = 2;
        bool audioOk;

        protected override bool PadChecks => false;
        // Every epilogue line (one per 3.2 s) and the stats row, then a beat to read them.
        protected override float EndingTime => 27f;
        protected override float ReadTime => rec ? 2.4f : 0.35f;
        protected override float MenuTime => rec ? 1.3f : 0.3f;

        protected override IEnumerator Run()
        {
            framesDir = Path.Combine(dir, "frames");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);
            QualitySettings.vSyncCount = 0;
            Time.captureFramerate = Fps;
            channels = AudioSettings.speakerMode switch
            {
                AudioSpeakerMode.Mono => 1, AudioSpeakerMode.Quad => 4, AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6, AudioSpeakerMode.Mode7point1 => 8, _ => 2,
            };
            audioOk = AudioRenderer.Start();
            var layer = Ui.Layer("ShowcaseCut", 35);
            layer.GetComponent<CanvasGroup>().blocksRaycasts = false;
            cut = Ui.Image(layer, "Black", new Color(0.01f, 0.012f, 0.02f, 0f));
            Ui.Stretch(cut.rectTransform);
            FirstPersonController.Teleported += d => { if (rec && d > 0.3f) Dip(); };
            Debug.Log($"[Showcase] {Screen.width}x{Screen.height}, audio renderer {(audioOk ? "on" : "unavailable")}, {AudioSettings.outputSampleRate} Hz x{channels}");
            StartCoroutine(Recorder());
            StartCoroutine(StopAfterEnding());
            rec = true;
            yield return base.Run();
        }

        UnityEngine.UI.Image cut;

        /// <summary>Instant moves read as a cut: come up from black instead of jumping.</summary>
        void Dip()
        {
            Ui.SetAlpha(cut, 1f);
            Tween.Run(0.5f, k => Ui.SetAlpha(cut, 1f - k), Ease.InOutSine, null, 0.05f, cut);
        }

        IEnumerator StopAfterEnding()
        {
            while (FindAnyObjectByType<EndingScreen>() == null) yield return null;
            while (FindAnyObjectByType<EndingScreen>() != null) yield return null;
            yield return Wait(4f);
            rec = false;
            Debug.Log("[Showcase] stopped filming after the ending");
        }

        IEnumerator Recorder()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (audioOk)
                {
                    int n = AudioRenderer.GetSampleCountForCaptureFrame();
                    if (n > 0)
                    {
                        var buf = new NativeArray<float>(n * channels, Allocator.Temp);
                        AudioRenderer.Render(buf);
                        if (rec) audio.AddRange(buf);
                        buf.Dispose();
                    }
                }
                if (!rec) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                var raw = tex.GetRawTextureData();
                var fmt = tex.graphicsFormat;
                uint w = (uint)tex.width, h = (uint)tex.height;
                Destroy(tex);
                var path = Path.Combine(framesDir, $"{frame++:000000}.jpg");
                pending.Add(Task.Run(() => File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, w, h, 0, 92))));
                if (pending.Count > 12) { pending[0].Wait(); pending.RemoveAt(0); }
            }
        }

        void OnApplicationQuit()
        {
            Task.WaitAll(pending.ToArray());
            if (audioOk) AudioRenderer.Stop();
            WriteWav(Path.Combine(dir, "audio.wav"));
            Debug.Log($"[Showcase] wrote {frame} frames ({frame / (float)Fps:F1}s), {audio.Count / channels} audio frames");
        }

        void WriteWav(string path)
        {
            int rate = AudioSettings.outputSampleRate;
            using var w = new BinaryWriter(File.Create(path));
            int bytes = audio.Count * 4;
            w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)3); w.Write((short)channels);
            w.Write(rate); w.Write(rate * channels * 4); w.Write((short)(channels * 4)); w.Write((short)32);
            w.Write("data".ToCharArray()); w.Write(bytes);
            foreach (var s in audio) w.Write(s);
        }

        // ---- what gets filmed ------------------------------------------------------------------

        protected override IEnumerator Beat(string phase, int n)
        {
            var dir = root.Director;
            switch (phase)
            {
                case "start":
                    rec = Filmed.Contains(n);
                    if (rec) yield return Wait(1.2f);
                    break;
                case "afterRoute":
                    if (rec && n == 2) yield return Night2Chores();
                    if (rec) yield return Wait(0.6f);
                    rec = false;
                    break;
                case "lockup":
                    if (n == 1)
                    {
                        rec = true;
                        root.Player.Teleport(new Vector3(13.4f, 0, 8.6f), 10f, 8f);
                        yield return Aim(dir.Furniture.Monitors["walt"].transform.position + Vector3.up * 0.35f, 0.5f);
                        yield return Wait(0.8f);
                    }
                    break;
                case "dark":
                    if (rec) yield return Wait(2.0f);
                    break;
                case "clockout":
                    rec = n == 1 || n == 7;
                    break;
                case "report":
                    if (rec) yield return Wait(3.5f);
                    break;
                case "end":
                    rec = Filmed.Contains(n + 1) || n == 7;
                    break;
            }
        }

        IEnumerator Night2Chores()
        {
            var ctx = root.Director.Ctx;
            var floor = ctx.Surface("floor_break");
            if (floor != null && !floor.Done)
            {
                var c = floor.UvToWorld(new Vector2(0.5f, 0.45f));
                yield return Approach(c, 1.6f);
                yield return Sweep(floor, new Rect(0.3f, 0.3f, 0.35f, 0.3f), 5, 0.9f, true, true, false);
            }
            var win = ctx.Surface("win_break_1");
            if (win != null && !win.Done) yield return ShowClean(win);
        }

        protected override IEnumerator ShowSpray(GrimeSurface s)
        {
            if (!rec) yield break;
            yield return Approach(s.UvToWorld(new Vector2(0.5f, 0.5f)), 1.4f);
            yield return Sweep(s, new Rect(0.08f, 0.2f, 0.84f, 0.6f), 4, 0.8f, true, false, true);
            yield return Aim(s.UvToWorld(new Vector2(0.5f, 0.5f)), 0.3f);
            yield return Wait(1.5f);
        }

        protected override IEnumerator ShowClean(GrimeSurface s)
        {
            if (rec)
            {
                bool vertical = Mathf.Abs(s.transform.up.y) < 0.5f;
                if (s.Id == "whiteboard_conf") { root.Player.Teleport(new Vector3(2.6f, 0, 10.4f), 180f, 5f); yield return null; }
                else yield return Approach(s.UvToWorld(new Vector2(0.5f, 0.5f)), vertical ? 1.2f : 0.75f);
                var r = new Rect(0.04f, 0.06f, 0.92f, 0.88f);
                if (s.Tool == ToolKind.Squeegee) yield return Sweep(s, r, 4, 0.7f, true, false, true);
                // One squeegee pass shows the idea; the rest finishes off camera.
                int passes = s.Tool == ToolKind.Squeegee ? 1 : 2;
                for (int i = 0; i < passes && !s.Done; i++)
                    yield return Sweep(s, r, vertical ? 6 : 4, vertical ? 0.9f : 0.7f, true, true, false);
                yield return Aim(s.UvToWorld(new Vector2(0.5f, 0.5f)), 0.3f);
                yield return Wait(1.6f);
            }
            if (!s.Done) yield return base.ShowClean(s);
        }

        // ---- walking up to things --------------------------------------------------------------

        protected override IEnumerator Approach(Vector3 target, float reach = 1.1f)
        {
            if (!rec) yield break;
            var player = root.Player;
            var p = player.transform.position;
            var flat = new Vector3(target.x, 0, target.z);
            var away = p - flat; away.y = 0;
            if (away.magnitude <= reach + 0.35f && Visible(p, target)) { yield return Aim(target, 0.4f); yield break; }

            Vector3? best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                foreach (float d in new[] { reach, reach + 0.3f, reach - 0.25f })
                {
                    var stand = flat + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Max(0.5f, d);
                    if (!Free(stand) || !Visible(stand, target)) continue;
                    float score = (stand - p).magnitude + Mathf.Abs(d - reach) * 2f;
                    if (score < bestScore) { bestScore = score; best = stand; }
                }
            }
            if (best == null) { yield return Aim(target, 0.4f); yield break; }
            var goal = best.Value;
            var path = goal - p;
            bool walkable = path.magnitude < 7f && !Physics.CapsuleCast(p + Vector3.up * 0.45f, p + Vector3.up * 1.5f, 0.28f,
                path.normalized, path.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (walkable) yield return WalkTo(goal);
            if ((player.transform.position - goal).magnitude > 0.35f)
            {
                var look = target - goal;
                player.Teleport(goal, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 10f);
                yield return null;
            }
            yield return Aim(target, 0.45f);
        }

        IEnumerator WalkTo(Vector3 goal)
        {
            var player = root.Player;
            float limit = (goal - player.transform.position).magnitude / 1.2f + 1.5f;
            for (float t = 0; t < limit; t += Time.deltaTime)
            {
                var d = goal - player.transform.position; d.y = 0;
                if (d.magnitude < 0.2f) break;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float diff = Mathf.DeltaAngle(player.Yaw, yaw);
                player.Yaw = Mathf.MoveTowardsAngle(player.Yaw, yaw, 260f * Time.deltaTime);
                player.Pitch = Mathf.MoveTowards(player.Pitch, 8f, 60f * Time.deltaTime);
                input.Move = Mathf.Abs(diff) < 35f ? new Vector2(0, Mathf.Clamp01(d.magnitude / 0.6f) * 0.5f + 0.5f) : Vector2.zero;
                yield return null;
            }
            input.Move = Vector2.zero;
            yield return null;
        }

        static bool Free(Vector3 stand) =>
            !Physics.CheckCapsule(stand + Vector3.up * 0.4f, stand + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && Physics.Raycast(stand + Vector3.up * 0.3f, Vector3.down, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        static bool Visible(Vector3 stand, Vector3 target)
        {
            var eye = new Vector3(stand.x, 1.62f, stand.z);
            var d = target - eye;
            return !Physics.Raycast(eye, d.normalized, out var hit, d.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                   || (hit.point - target).magnitude < 0.35f;
        }
    }
}
