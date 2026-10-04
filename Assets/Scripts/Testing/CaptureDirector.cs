using System.Collections;
using System.IO;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Scripted screenshot sessions for verifying the look and feel from a built player:
    /// <c>-ahCapture &lt;dir&gt; [scenario]</c>. Drives the real tools through ScriptedInput,
    /// logs numbers with a [Capture] prefix, then quits.
    /// </summary>
    public class CaptureDirector : MonoBehaviour
    {
        string dir;
        string scenario;
        int shot;
        protected ScriptedInput input;
        protected GameRoot root;

        void Start()
        {
            dir = GameRoot.Arg("-ahCapture") ?? "/tmp/ah-capture";
            scenario = GameRoot.Arg("-ahCapture", 2) ?? "proto";
            if (scenario.StartsWith("-")) scenario = "proto";
            Directory.CreateDirectory(dir);
            input = new ScriptedInput();
            GameInput.Override = input;
            root = GameRoot.Instance;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Log($"scenario {scenario}, screen {Screen.width}x{Screen.height}");
            yield return Wait(1.5f);
            switch (scenario)
            {
                default: yield return Proto(); break;
            }
            Log("done");
            yield return Wait(0.3f);
            Application.Quit();
        }

        IEnumerator Proto()
        {
            var p = root.Proto;
            var player = root.Player;

            player.Teleport(new Vector3(1.6f, 0, -2.8f), -35f, 8f);
            yield return Wait(1f);
            yield return Shot("overview");

            // Desk
            player.Teleport(new Vector3(-2.4f, 0, 0.2f), 0f, 35f);
            yield return Aim(p.Desk.UvToWorld(new Vector2(0.5f, 0.5f)), 0.6f);
            yield return Shot("desk_dirty");
            float t0 = Time.time;
            yield return Sweep(p.Desk, new Rect(0.05f, 0.08f, 0.9f, 0.84f), 4, 1.1f, true, true, false, "desk_scrub", 1);
            Log($"desk completion after 4 passes ({Time.time - t0:F1}s) {p.Desk.Completion:P0} done={p.Desk.Done}");
            yield return Sweep(p.Desk, new Rect(0.05f, 0.08f, 0.9f, 0.84f), 4, 1.1f, true, true, false);
            Log($"desk completion after 8 passes ({Time.time - t0:F1}s) {p.Desk.Completion:P0} done={p.Desk.Done}");
            yield return Aim(p.Desk.UvToWorld(new Vector2(0.5f, 0.5f)), 0.2f);
            yield return Wait(0.35f);
            yield return Shot("desk_gleam");
            yield return Wait(1.0f);
            yield return Shot("desk_clean");

            // Carpet
            player.Teleport(new Vector3(-2.25f, 0, -2.0f), 0f, 40f);
            yield return Aim(p.Carpet.UvToWorld(new Vector2(0.5f, 0.35f)), 0.4f);
            yield return Shot("carpet_dirty");
            yield return Sweep(p.Carpet, new Rect(0.18f, 0.24f, 0.64f, 0.26f), 6, 1.0f, false, true, false, "carpet_vacuum", 3);
            Log($"carpet completion {p.Carpet.Completion:P1}");
            yield return Aim(p.Carpet.UvToWorld(new Vector2(0.5f, 0.38f)), 0.4f);
            yield return Wait(0.5f);
            yield return Shot("carpet_stripes");

            // Window: spray, see the finger writing, squeegee.
            player.Teleport(new Vector3(0.5f, 0, 2.0f), 0f, -2f);
            yield return Aim(p.Window.UvToWorld(new Vector2(0.5f, 0.5f)), 0.4f);
            yield return Shot("window_dirty");
            yield return Sweep(p.Window, new Rect(0.08f, 0.15f, 0.84f, 0.7f), 4, 0.8f, true, false, true);
            Log($"window foam over writing {p.Window.GhostFoamCoverage():P0}");
            yield return Aim(p.Window.UvToWorld(new Vector2(0.5f, 0.5f)), 0.3f);
            yield return Shot("window_foam");
            yield return Sweep(p.Window, new Rect(0.04f, 0.08f, 0.92f, 0.84f), 5, 0.9f, true, true, false, "window_squeegee", 2);
            Log($"window completion {p.Window.Completion:P1} done={p.Window.Done}");
            yield return Aim(p.Window.UvToWorld(new Vector2(0.5f, 0.5f)), 0.3f);
            yield return Wait(0.6f);
            yield return Shot("window_clean");

            // Whiteboard: erase and reveal the ghost.
            player.Teleport(new Vector3(-3.25f, 0, -1.2f), -90f, 5f);
            yield return Aim(p.Whiteboard.UvToWorld(new Vector2(0.5f, 0.5f)), 0.4f);
            yield return Shot("whiteboard_marker");
            yield return Sweep(p.Whiteboard, new Rect(0.04f, 0.06f, 0.92f, 0.88f), 6, 1.0f, true, true, false, "whiteboard_erase", 2);
            Log($"whiteboard completion {p.Whiteboard.Completion:P1}, ghost reveal {p.Whiteboard.GhostReveal:P0}");
            yield return Sweep(p.Whiteboard, new Rect(0.04f, 0.06f, 0.92f, 0.88f), 6, 1.0f, true, true, false);
            Log($"whiteboard completion {p.Whiteboard.Completion:P1}, ghost reveal {p.Whiteboard.GhostReveal:P0}");
            yield return Aim(p.Whiteboard.UvToWorld(new Vector2(0.5f, 0.5f)), 0.3f);
            yield return Wait(1.2f);
            yield return Shot("whiteboard_ghost");

            // Mop the tile.
            player.Teleport(new Vector3(2.25f, 0, -2.4f), 0f, 42f);
            yield return Aim(p.Tile.UvToWorld(new Vector2(0.5f, 0.3f)), 0.4f);
            yield return Shot("tile_dirty");
            yield return Sweep(p.Tile, new Rect(0.15f, 0.16f, 0.7f, 0.28f), 5, 1.0f, false, true, false, "tile_mop", 3);
            Log($"tile completion {p.Tile.Completion:P1}");
            yield return Aim(p.Tile.UvToWorld(new Vector2(0.5f, 0.3f)), 0.2f);
            yield return Shot("tile_wet");
            yield return Wait(3f);
            yield return Shot("tile_dry");
        }

        // ---- helpers -------------------------------------------------------------------------

        protected void Log(string msg) => Debug.Log("[Capture] " + msg);

        protected IEnumerator Wait(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        protected IEnumerator Shot(string name)
        {
            var path = Path.Combine(dir, $"{shot++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Log("shot " + path);
        }

        protected void LookAtNow(Vector3 world)
        {
            var player = root.Player;
            var cam = player.Camera.transform.position;
            var d = (world - cam).normalized;
            player.Yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            player.Pitch = -Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        protected IEnumerator Aim(Vector3 world, float seconds)
        {
            var player = root.Player;
            float y0 = player.Yaw, p0 = player.Pitch;
            var cam = player.Camera.transform.position;
            var d = (world - cam).normalized;
            float y1 = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, p1 = -Mathf.Asin(d.y) * Mathf.Rad2Deg;
            y1 = y0 + Mathf.DeltaAngle(y0, y1);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / seconds);
                player.Yaw = Mathf.Lerp(y0, y1, k);
                player.Pitch = Mathf.Lerp(p0, p1, k);
                yield return null;
            }
            player.Yaw = y1; player.Pitch = p1;
        }

        /// <summary>Boustrophedon sweep over a UV rect while holding Use / Spray.</summary>
        protected IEnumerator Sweep(GrimeSurface s, Rect r, int passes, float secondsPerPass, bool horizontal, bool use, bool spray,
            string shotName = null, int shotAtPass = -1)
        {
            yield return Aim(s.UvToWorld(horizontal ? new Vector2(r.xMin, r.yMax) : new Vector2(r.xMin, r.yMin)), 0.25f);
            for (int k = 0; k < passes; k++)
            {
                float lane = passes == 1 ? 0.5f : k / (float)(passes - 1);
                bool forward = k % 2 == 0;
                for (float t = 0; t < secondsPerPass; t += Time.deltaTime)
                {
                    float a = Mathf.SmoothStep(0, 1, t / secondsPerPass);
                    if (!forward) a = 1 - a;
                    var uv = horizontal
                        ? new Vector2(Mathf.Lerp(r.xMin, r.xMax, a), Mathf.Lerp(r.yMax, r.yMin, lane))
                        : new Vector2(Mathf.Lerp(r.xMin, r.xMax, lane), Mathf.Lerp(r.yMin, r.yMax, a));
                    LookAtNow(s.UvToWorld(uv));
                    input.Use = use;
                    input.Spray = spray;
                    yield return null;
                    if (shotName != null && k == shotAtPass && t < secondsPerPass * 0.5f && t + Time.deltaTime >= secondsPerPass * 0.5f)
                        yield return Shot(shotName);
                }
            }
            input.Use = false;
            input.Spray = false;
            yield return null;
        }
    }
}
