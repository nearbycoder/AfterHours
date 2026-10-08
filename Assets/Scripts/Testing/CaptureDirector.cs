using System.Collections;
using System.Linq;
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
        protected string dir;
        protected string scenario;
        int shot;
        protected ScriptedInput input;
        protected GameRoot root;

        protected virtual string ArgName => "-ahCapture";

        void Start()
        {
            dir = GameRoot.Arg(ArgName) ?? "/tmp/ah-capture";
            scenario = GameRoot.Arg(ArgName, 2) ?? "proto";
            if (scenario.StartsWith("-")) scenario = "proto";
            Directory.CreateDirectory(dir);
            // Unattended windows lose focus to whatever else is on the desktop, and by default the
            // Input System then ignores devices (the virtual keyboard and pads the checks drive).
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            input = new ScriptedInput();
            GameInput.Override = input;
            root = GameRoot.Instance;
            StartCoroutine(Run());
        }

        protected virtual IEnumerator Run()
        {
            Log($"scenario {scenario}, screen {Screen.width}x{Screen.height}");
            yield return Wait(1.5f);
            switch (scenario)
            {
                case "proto": yield return Proto(); break;
                case "night1": yield return Night1(); break;
                case "tour": yield return NightTour(); break;
                case "perf": yield return PerfProbe(); break;
                case "fidelity": yield return FidelityProbe(); break;
                case "monitors": yield return Monitors(); break;
                case "menus": yield return Menus(); break;
                case "closetest": yield return CloseTest(); break;
                case "windowtest": yield return WindowTest(); break;
                case var s when s.StartsWith("look:"): yield return LookAt(s.Substring(5)); break;
                default: yield return OfficeTour(); break;
            }
            Log("done");
            yield return Wait(0.3f);
            Application.Quit();
        }

        /// <summary>
        /// Title, Settings and Night Select at whatever window size the player was started with
        /// (<c>AH_W</c>/<c>AH_H</c> in Tools/play.sh), at each text size, plus a layout check on
        /// Settings, both controls pages and the brightness page: no row overlaps another or the
        /// buttons, and nothing leaves the screen. Rows in a scrolling list (the larger text sizes)
        /// are checked against each other, and the list itself against the rest.
        /// </summary>
        IEnumerator Menus()
        {
            yield return Wait(2f);
            Rect ScreenRect(RectTransform rt)
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            }
            string size = $"{Screen.width}x{Screen.height}";
            // Every row, plus the panel's notes (the controls page's message under its rows).
            void Layout(string what, string layer, string text)
            {
                var panel = Ui.Canvas.transform.Find(layer);
                var all = panel ? panel.GetComponentsInChildren<UnityEngine.UI.Selectable>().Select(x => (RectTransform)x.transform).ToList() : new System.Collections.Generic.List<RectTransform>();
                var listed = all.Where(r => r.GetComponentInParent<UnityEngine.UI.RectMask2D>() != null).ToList();
                var rows = all.Except(listed).ToList();
                if (panel) rows.AddRange(panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>()
                    .Where(t => t.transform.parent.name == "Panel" && t.text.Length > 60).Select(t => t.rectTransform));
                if (panel) rows.AddRange(panel.GetComponentsInChildren<UnityEngine.UI.RectMask2D>().Select(m => (RectTransform)m.transform));
                int overlaps = 0, off = 0;
                void Pairs(System.Collections.Generic.List<RectTransform> set, bool onScreen)
                {
                    var rects = set.Select(r => (r.name, rect: ScreenRect(r))).ToList();
                    for (int i = 0; i < rects.Count; i++)
                    {
                        var a = rects[i].rect;
                        if (onScreen && (a.xMin < 0 || a.yMin < 0 || a.xMax > Screen.width || a.yMax > Screen.height)) { off++; Log($"layout: {rects[i].name} leaves the screen ({a})"); }
                        for (int j = i + 1; j < rects.Count; j++)
                            if (a.Overlaps(rects[j].rect)) { overlaps++; Log($"layout: {rects[i].name} overlaps {rects[j].name}"); }
                    }
                }
                Pairs(rows, true);
                Pairs(listed, false);
                Log($"layout {size} {text}: {what} has {rows.Count + listed.Count} rows ({listed.Count} in a scrolling list), {overlaps} overlaps, {off} off screen -> {(overlaps == 0 && off == 0 && rows.Count > 0 ? "OK" : "BAD")}");
            }
            for (int ts = 0; ts <= 2; ts++)
            {
                Settings.Current.TextSize = ts;
                string text = Settings.TextSizes[ts].ToLowerInvariant();
                string tag = ts == 0 ? size : $"{size}_{text}";
                yield return Wait(0.4f);
                yield return Shot($"title_{tag}");
                SettingsPanel.Show();
                yield return Wait(0.8f);
                Layout("settings", "Settings", text);
                yield return Shot($"settings_{tag}");
                ControlsPanel.Show(ControlsPanel.KeyboardPage);
                yield return Wait(0.6f);
                Layout("controls (keyboard)", "Controls", text);
                yield return Shot($"controls_{tag}");
                FindAnyObjectByType<ControlsPanel>()?.SwitchTo(ControlsPanel.PadPage);
                yield return Wait(0.6f);
                Layout("controls (controller)", "Controls", text);
                yield return Shot($"controls_pad_{tag}");
                FindAnyObjectByType<ControlsPanel>()?.SendMessage("Close");
                yield return Wait(0.3f);
                BrightnessPanel.Show();
                yield return Wait(0.6f);
                Layout("brightness", "Brightness", text);
                yield return Shot($"brightness_{tag}");
                FindAnyObjectByType<BrightnessPanel>()?.SendMessage("Close");
                yield return Wait(0.3f);
                FindAnyObjectByType<SettingsPanel>()?.SendMessage("Close");
                yield return Wait(0.5f);
                NightSelect.Show();
                yield return Wait(0.8f);
                yield return Shot($"night_select_{tag}");
                FindAnyObjectByType<NightSelect>()?.SendMessage("Close");
                yield return Wait(0.4f);
            }
            Settings.Current.TextSize = 0;
            Settings.Save();
        }

        /// <summary>
        /// The other half of <c>Tools/closetest.sh</c>, which closes this window from the window
        /// manager (as the title bar's close button does) when the log says "ready". On the title
        /// the close must go through. Mid-night (<c>-ahNight 1</c>) it must be held behind the
        /// question; Never mind keeps the night paused where it was, and the second close is
        /// answered with Quit the game, which must close the game.
        /// </summary>
        IEnumerator CloseTest()
        {
            GameRoot.QuitAsks = true; // as for a player
            if (!GameRoot.HasArg("-ahNight"))
            {
                yield return Wait(2f);
                Log("closetest ready 1: on the title");
                yield return Wait(30f);
                Log("closetest FAIL: still running 30 s after the title's window was closed");
                yield break;
            }
            var dir = root.Director;
            yield return WaitUnblocked(40f);
            yield return Wait(1.5f);
            Log($"closetest ready 1: Night {dir.Def.Number} running {dir.Running} at {dir.Elapsed:F1}s");
            for (float t = 0; !ChoiceMenu.IsOpen && t < 30f; t += GameTime.UnscaledDelta) yield return null;
            float at = dir.Elapsed;
            Log($"closetest held: question {ChoiceMenu.IsOpen}, pause menu {PauseMenu.IsOpen}, time scale {Time.timeScale}, night running {dir.Running} at {at:F1}s");
            if (!ChoiceMenu.IsOpen) { Log("closetest FAIL: the close wasn't held"); yield break; }
            yield return Shot("close_question");
            ChoiceMenu.AutoPick = 1; // Never mind
            yield return Wait(2f);
            Log($"closetest never mind: question {ChoiceMenu.IsOpen}, pause menu {PauseMenu.IsOpen}, night running {dir.Running}, clock moved {Mathf.Abs(dir.Elapsed - at):F2}s");
            Log("closetest ready 2: closing again");
            for (float t = 0; !ChoiceMenu.IsOpen && t < 30f; t += GameTime.UnscaledDelta) yield return null;
            Log($"closetest asked again: question {ChoiceMenu.IsOpen}, pause menu {PauseMenu.IsOpen}; answering Quit the game");
            ChoiceMenu.AutoPick = 0; // Quit the game
            yield return Wait(30f);
            Log("closetest FAIL: still running 30 s after Quit the game");
        }

        /// <summary>
        /// The other half of <c>Tools/wmtest.sh window</c>: started fullscreen, switches Fullscreen
        /// off and back on as Settings does, saying "ready" at each step so the script can ask
        /// the window manager where the window is.
        /// </summary>
        IEnumerator WindowTest()
        {
            yield return Wait(2f);
            string Now() => $"{Screen.fullScreenMode} {Screen.width}x{Screen.height}, display {Display.main.systemWidth}x{Display.main.systemHeight}";
            Log($"windowtest ready 1: started {Now()}");
            yield return Wait(4f);
            Settings.ApplyWindowMode(false);
            yield return Wait(3f);
            Log($"windowtest ready 2: windowed {Now()}");
            yield return Wait(4f);
            Settings.ApplyWindowMode(true);
            yield return Wait(3f);
            Log($"windowtest ready 3: fullscreen again {Now()}");
            yield return Wait(4f);
            Settings.ApplyWindowMode(false);
            yield return Wait(3f);
            Log($"windowtest ready 4: windowed again {Now()}");
            yield return Wait(4f);
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

        /// <summary>
        /// Frame-time probe: stand in the bullpen and time frames with features switched off one at a
        /// time, to tell CPU cost from GPU cost. Run with <c>-ahCapture dir perf -ahNight 2 -ahFresh</c>.
        /// </summary>
        IEnumerator PerfProbe()
        {
            yield return WaitUnblocked(30f);
            root.Player.Teleport(new Vector3(8.2f, 0, 6.0f), 40f, 6f);
            yield return Wait(2f);
            var main = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Main Thread", 120);
            var render = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Render Thread", 120);
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var camData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(root.Player.Camera);
            Log($"perf: {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}, vsync {QualitySettings.vSyncCount}, target {Application.targetFrameRate}, lights {FindObjectsByType<Light>(FindObjectsSortMode.None).Length}, grime {GrimeSurface.All.Count}");

            IEnumerator Measure(string label)
            {
                yield return Wait(0.5f);
                var ms = new System.Collections.Generic.List<float>();
                double mainSum = 0, renderSum = 0; int n = 0;
                for (float t = 0; t < 3f; t += GameTime.UnscaledDelta)
                {
                    yield return null;
                    ms.Add(GameTime.UnscaledDelta * 1000f);
                    if (main.Valid && main.LastValue > 0) { mainSum += main.LastValue / 1e6; renderSum += render.Valid ? render.LastValue / 1e6 : 0; n++; }
                }
                ms.Sort();
                Log($"perf [{label}] median {ms[ms.Count / 2]:F1} ms ({1000f / ms[ms.Count / 2]:F0} fps), p95 {ms[(int)(ms.Count * 0.95f)]:F1} ms" +
                    (n > 0 ? $", main thread {mainSum / n:F1} ms, render thread {renderSum / n:F1} ms" : ", thread timings unavailable"));
            }

            yield return Measure("baseline");
            // Everything below runs uncapped so the numbers are the real cost, not the compositor's pace.
            int vs = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return Measure("vsync off");
            var grime = GrimeSurface.All.ToList();
            foreach (var g in grime) g.enabled = false;
            yield return Measure("grime scripts off");
            foreach (var g in grime) g.enabled = true;
            camData.renderPostProcessing = false;
            yield return Measure("post off");
            camData.renderPostProcessing = true;
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var shadows = lights.Select(l => l.shadows).ToArray();
            foreach (var l in lights) l.shadows = LightShadows.None;
            yield return Measure("shadows off");
            for (int i = 0; i < lights.Length; i++) lights[i].shadows = shadows[i];
            if (urp != null)
            {
                float rs = urp.renderScale;
                urp.renderScale = 0.5f;
                yield return Measure("render scale 0.5");
                urp.renderScale = rs;
                int msaa = urp.msaaSampleCount;
                urp.msaaSampleCount = 1;
                yield return Measure("msaa off");
                urp.msaaSampleCount = msaa;
            }
            var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Log("perf cameras: " + string.Join(", ", cams.Select(c => $"{c.name}({(c.enabled ? "on" : "off")})")));
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.gameObject.layer == Layers.Grime) r.enabled = false;
            yield return Measure("grime renderers off");
            QualitySettings.vSyncCount = vs;
            main.Dispose(); render.Dispose();
        }

        /// <summary>The machine's load average (1, 5 and 15 minutes), for the logs next to a timing.</summary>
        protected static string LoadAverage()
        {
            string load, gpu = "";
            try { load = "load " + string.Join(" ", File.ReadAllText("/proc/loadavg").Split(' ').Take(3)); }
            catch { load = "load ?"; }
            // The GPU is shared with whatever else the machine runs; how busy it is (this game included).
            try { gpu = ", GPU busy " + File.ReadAllText("/sys/class/drm/card1/device/gpu_busy_percent").Trim() + "%"; }
            catch { }
            return load + gpu;
        }

        /// <summary>
        /// Graphics Fidelity, step by step: at each of four views (rooms lit, HUD hidden, the world
        /// frozen so every step sees the same frame), every step gets a screenshot and an uncapped
        /// frame-time measurement (median and 95th percentile over 2.5 s, three rounds taking turns, the middle round's figures), with what the engine
        /// reports for the step and the machine's load. Run with <c>-ahCapture dir fidelity -ahNight 2 -ahFresh</c>.
        /// </summary>
        IEnumerator FidelityProbe()
        {
            yield return WaitUnblocked(30f);
            var s = Settings.Current;
            int q0 = s.Quality;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            foreach (var r in root.Office.Rooms.Values) r.SetLights(true, true);
            Hud.Instance?.SetVisible(false);
            root.Rig.Hidden = true;
            Log($"fidelity: {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}, {LoadAverage()}");
            var views = new (string name, Vector3 stand, Vector3 look)[]
            {
                ("bullpen", new Vector3(8.2f, 0f, 6.0f), new Vector3(13.5f, 0.9f, 12.5f)),
                ("breakroom", new Vector3(6.3f, 0f, 3.7f), new Vector3(1.5f, 0.9f, 8.0f)),
                ("reception", new Vector3(14.3f, 0f, 0.7f), new Vector3(9.5f, 0.6f, 3.6f)),
                ("conference", new Vector3(6.3f, 0f, 15.3f), new Vector3(1.0f, 1.1f, 10.5f)),
            };
            var totals = new float[GraphicsQuality.Names.Length];
            var gpuTotals = new float[GraphicsQuality.Names.Length];
            const int Rounds = 3; // the steps take turns, so a change in the machine's load hits them all alike
            foreach (var (name, stand, look) in views)
            {
                Time.timeScale = 1f;
                root.Player.Teleport(stand, 0f, 0f);
                yield return null;
                LookAtNow(look);
                yield return Wait(1f);
                var medians = new System.Collections.Generic.List<float>[GraphicsQuality.Names.Length];
                var p95s = new System.Collections.Generic.List<float>[GraphicsQuality.Names.Length];
                var gpus = new System.Collections.Generic.List<float>[GraphicsQuality.Names.Length];
                var cpus = new System.Collections.Generic.List<float>[GraphicsQuality.Names.Length];
                var p10s = new System.Collections.Generic.List<float>[GraphicsQuality.Names.Length];
                for (int q = 0; q < medians.Length; q++) { medians[q] = new(); p95s[q] = new(); p10s[q] = new(); gpus[q] = new(); cpus[q] = new(); }
                for (int round = 0; round < Rounds; round++)
                for (int q = 0; q < GraphicsQuality.Names.Length; q++)
                {
                    Time.timeScale = 1f;
                    s.Quality = q;
                    Settings.ApplyGraphics();
                    float settle = 0f;
                    for (; settle < 8f && !RoomProbes.Settled; settle += GameTime.UnscaledDelta) yield return null;
                    if (q == 3 && round == 0) Log($"fidelity probes [{name}] {(RoomProbes.Settled ? "settled" : "NOT settled")} after {settle:F1} s, {RoomProbes.Renders} renders: {RoomProbes.Status()}");
                    yield return Wait(1f);
                    Time.timeScale = 0f; // the same frame for every step: nothing moves while it's measured and shot
                    yield return Wait(0.4f);
                    if (name == views[0].name && round == 0) Log($"fidelity step {GraphicsQuality.Describe()}");
                    var ms = new System.Collections.Generic.List<float>();
                    var gpu = new System.Collections.Generic.List<float>();
                    var cpu = new System.Collections.Generic.List<float>();
                    var timing = new FrameTiming[1];
                    for (float t = 0; t < 2.5f; t += GameTime.UnscaledDelta)
                    {
                        yield return null;
                        ms.Add(GameTime.UnscaledDelta * 1000f);
                        FrameTimingManager.CaptureFrameTimings();
                        if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                        {
                            if (timing[0].gpuFrameTime > 0) gpu.Add((float)timing[0].gpuFrameTime);
                            if (timing[0].cpuMainThreadFrameTime > 0) cpu.Add((float)timing[0].cpuMainThreadFrameTime);
                        }
                    }
                    ms.Sort(); gpu.Sort(); cpu.Sort();
                    medians[q].Add(ms[ms.Count / 2]);
                    p95s[q].Add(ms[(int)(ms.Count * 0.95f)]);
                    p10s[q].Add(ms[(int)(ms.Count * 0.10f)]);
                    float g = gpu.Count > 0 ? gpu[gpu.Count / 2] : 0f, c = cpu.Count > 0 ? cpu[cpu.Count / 2] : 0f;
                    gpus[q].Add(g); cpus[q].Add(c);
                    Log($"fidelity round {round + 1} [{name}] [{GraphicsQuality.Names[q]}] median {ms[ms.Count / 2]:F2} ms (GPU {g:F2} ms, main thread {c:F2} ms), {ms.Count} frames, {LoadAverage()}");
                    if (round == 0) yield return Shot($"fidelity_{name}_{q}_{GraphicsQuality.Names[q].ToLowerInvariant()}");
                }
                for (int q = 0; q < medians.Length; q++)
                {
                    medians[q].Sort(); p95s[q].Sort(); p10s[q].Sort(); gpus[q].Sort(); cpus[q].Sort();
                    float median = medians[q][Rounds / 2], p95 = p95s[q][Rounds / 2];
                    totals[q] += median;
                    gpuTotals[q] += gpus[q][Rounds / 2];
                    Log($"fidelity [{name}] [{GraphicsQuality.Names[q]}] median {median:F2} ms ({1000f / median:F0} fps), p95 {p95:F2} ms, p10 {p10s[q][Rounds / 2]:F2} ms, GPU {gpus[q][Rounds / 2]:F2} ms, main thread {cpus[q][Rounds / 2]:F2} ms, middle of {Rounds} rounds, {LoadAverage()}");
                }
            }
            for (int q = 0; q < totals.Length; q++)
                Log($"fidelity mean of the views' medians [{GraphicsQuality.Names[q]}] {totals[q] / views.Length:F2} ms, GPU {gpuTotals[q] / views.Length:F2} ms");
            Time.timeScale = 1f;
            s.Quality = q0;
            Settings.ApplyGraphics();
        }

        /// <summary>Every monitor photographed straight on, lit room, to check screen mapping.</summary>
        IEnumerator Monitors()
        {
            yield return WaitUnblocked(30f);
            foreach (var r in root.Office.Rooms.Values) r.SetLights(true, true);
            foreach (var kv in root.Director.Furniture.Monitors)
            {
                var m = kv.Value;
                if (!m.On) m.SetOn(true, true);
                var screenPos = m.transform.position + Vector3.up * 0.38f;
                // The screen faces away from the stand: the stand sits behind the panel (-forward).
                var front = m.transform.forward;
                var stand = screenPos + front * 0.85f;
                stand.y = 0;
                root.Player.Teleport(stand, Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg, 0f);
                yield return Aim(screenPos, 0.1f);
                yield return Wait(0.5f);
                Log($"monitor {kv.Key}: screen {m.ScreenDoc}, forward {front}");
                yield return Shot("monitor_" + kv.Key);
            }
        }

        /// <summary>Photograph a world point from four sides at arm's length: <c>look:x,y,z</c>.</summary>
        IEnumerator LookAt(string xyz)
        {
            var p = xyz.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var target = new Vector3(p[0], p[1], p[2]);
            yield return WaitUnblocked(30f);
            foreach (var r in root.Office.Rooms.Values) r.SetLights(true, true);
            int i = 0;
            foreach (var dir in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
            {
                var stand = target + dir * 0.9f; stand.y = 0;
                root.Player.Teleport(stand, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg, 0f);
                yield return Aim(target, 0.1f);
                yield return Wait(0.4f);
                yield return Shot($"look_{i++}");
            }
        }

        IEnumerator OfficeTour()
        {
            var o = root.Office;
            var player = root.Player;
            foreach (var kv in o.Surfaces)
                if (kv.Value.gameObject.activeSelf)
                    Log($"surface {kv.Key} tool={kv.Value.Tool} size={kv.Value.Size} pos={kv.Value.transform.position} up={kv.Value.transform.up} fwd={kv.Value.transform.forward}");
            foreach (var kv in o.Rooms) Log($"room {kv.Key} bounds={kv.Value.Bounds} lights={kv.Value.Lights.Count} panels={kv.Value.Panels.Count}");
            Log($"anchors {o.Anchors.Count}, doors {o.Doors.Count}, switches {o.Switches.Count}");
            yield return Wait(1f);
            yield return Shot("closet_dark");
            o.Rooms["closet"].SetLights(true, true);
            yield return Wait(0.5f);
            yield return Shot("closet_lit");

            (string room, Vector3 pos, float yaw, float pitch)[] views =
            {
                ("reception", new Vector3(14.3f, 0, 0.7f), -55f, 8f),
                ("bullpen", new Vector3(8.2f, 0, 6.2f), 35f, 6f),
                ("conference", new Vector3(6.3f, 0, 15.3f), -140f, 10f),
                ("office", new Vector3(18.7f, 0, 15.2f), 140f, 10f),
                ("breakroom", new Vector3(6.4f, 0, 8.6f), -140f, 12f),
            };
            foreach (var v in views)
            {
                player.Teleport(v.pos, v.yaw, v.pitch);
                yield return Wait(0.6f);
                yield return Shot(v.room + "_dark");
                o.Rooms[v.room].SetLights(true, true);
                yield return Wait(0.4f);
                yield return Shot(v.room + "_lit");
            }
            // Whiteboard reveal in situ
            var wb = o.Surfaces["whiteboard_conf"];
            player.Teleport(new Vector3(2.6f, 0, 10.4f), 180f, 5f);
            yield return Wait(0.4f);
            yield return Sweep(wb, new Rect(0.04f, 0.06f, 0.92f, 0.88f), 6, 1.0f, true, true, false);
            yield return Sweep(wb, new Rect(0.04f, 0.06f, 0.92f, 0.88f), 6, 1.0f, true, true, false);
            yield return Aim(wb.UvToWorld(new Vector2(0.5f, 0.5f)), 0.3f);
            yield return Wait(1.0f);
            yield return Shot("whiteboard_reveal");
            // Vacuum a lane of the bullpen.
            var fl = o.Surfaces["floor_bullpen"];
            player.Teleport(new Vector3(12.0f, 0, 6.2f), 0f, 35f);
            yield return Sweep(fl, new Rect(0.3f, 0.12f, 0.25f, 0.2f), 5, 0.9f, false, true, false, "bullpen_vacuum", 3);
            yield return Aim(fl.UvToWorld(new Vector2(0.43f, 0.25f)), 0.3f);
            yield return Shot("bullpen_stripes");
        }

        IEnumerator Night1()
        {
            var o = root.Office;
            var player = root.Player;
            var dir = root.Director;
            yield return Wait(1.6f);
            yield return Shot("titlecard");
            yield return WaitUnblocked();
            yield return Wait(0.5f);
            yield return Shot("closet_start");

            // Dana's note on the corkboard
            var note = GameObject.Find("dana_welcome");
            yield return UseAt(note.transform.position, 1.1f, new Vector3(16.5f, 0, 2.5f));
            yield return Wait(0.8f);
            yield return Shot("read_dana_note");
            InspectView.AutoChoice = InspectChoice.Close;
            yield return Wait(0.6f);

            // Walt's locker
            var locker = GameObject.Find("FURN_locker_walt");
            yield return UseAt(locker.transform.position + Vector3.up * 1.2f + Vector3.left * 0.26f, 1.1f, new Vector3(16f, 0, 3.6f));
            yield return Wait(0.8f);
            yield return Shot("read_walt_note");
            InspectView.AutoChoice = InspectChoice.Close;
            yield return Wait(0.6f);

            // Clipboard
            input.ClipboardOnce = true;
            yield return Wait(0.8f);
            yield return Shot("clipboard");
            Clipboard.Instance.Close();
            yield return Wait(0.5f);

            // Reception: lights on, wipe the desk
            player.Teleport(new Vector3(14.2f, 0, 2.0f), -90f, 10f);
            yield return UseAt(o.Switches["reception"].transform.position, 0.9f, new Vector3(13f, 0, 2.2f));
            yield return Wait(1.2f);
            player.Teleport(new Vector3(12.6f, 0, 1.0f), -40f, 8f);
            yield return Wait(0.3f);
            yield return Shot("reception_lit");
            var desk = o.Surfaces["desk_reception"];
            player.Teleport(new Vector3(11.5f, 0, 2.3f), 0f, 30f);
            yield return Sweep(desk, new Rect(0.03f, 0.15f, 0.94f, 0.7f), 2, 2.2f, true, true, false, "reception_wipe", 0);
            yield return Sweep(desk, new Rect(0.03f, 0.15f, 0.94f, 0.7f), 2, 2.2f, true, true, false);
            Log($"reception desk {desk.Completion:P0} done={desk.Done}");

            // Throw a coffee cup into the bin
            var cup = FindTrashNear(new Vector3(13.9f, 0, 4.05f));
            if (cup)
            {
                yield return UseAt(cup.transform.position, 1.0f, new Vector3(13.5f, 0, 2.5f));
                yield return Wait(0.3f);
                var bin = o.Anchor("BIN_trash_reception_1").position;
                player.Teleport(bin + new Vector3(-3.6f, 0, -2.0f), 0, 0);
                yield return Aim(bin + Vector3.up * 1.1f, 0.4f);
                input.Use = true;
                yield return Wait(0.5f);
                yield return Shot("throw_arc");
                input.Use = false;
                yield return Wait(1.6f);
                Log($"cup binned={cup.Binned} held={root.Hands.Holding != null}");
                yield return Shot("after_throw");
            }

            // Bullpen: lights, the crumpled note
            yield return UseAt(o.Switches["bullpen"].transform.position, 0.9f, new Vector3(13.6f, 0, 6.5f));
            yield return Wait(1.2f);
            player.Teleport(new Vector3(8.0f, 0, 6.0f), 40f, 6f);
            yield return Wait(0.3f);
            yield return Shot("bullpen_lit");
            var theoNote = GameObject.Find("theo_note");
            yield return UseAt(theoNote.transform.position, 0.9f, new Vector3(10.8f, 0, 9.5f));
            yield return Wait(0.9f);
            yield return Shot("inspect_theo_note");
            InspectView.AutoChoice = InspectChoice.Keep;
            yield return Wait(1.2f);
            Log($"theo_note fate after keep={Story.State.FateOf("theo_note")}");

            // Vacuum under Theo's desk
            var floor = o.Surfaces["floor_bullpen"];
            player.Teleport(new Vector3(10.45f, 0, 10.05f), 0f, 55f);
            input.Crouch = true;
            yield return Wait(0.5f);
            for (int i = 0; i < 4; i++)
            {
                yield return SweepWorld(floor, new Vector3(9.95f, 0, 11.3f + i * 0.2f), new Vector3(10.9f, 0, 11.3f + i * 0.2f), 0.7f);
                yield return SweepWorld(floor, new Vector3(10.9f, 0, 11.4f + i * 0.2f), new Vector3(9.95f, 0, 11.4f + i * 0.2f), 0.7f);
            }
            yield return Wait(0.3f);
            yield return Shot("vacuum_key");
            input.Crouch = false;
            yield return Wait(0.6f);
            var key = GameObject.Find("key");
            if (key) { yield return UseAt(key.transform.position, 0.8f, new Vector3(10.4f, 0, 10f)); yield return Wait(0.5f); }
            Log($"key found={Story.State.Has("has_key_fc2")}");

            // Deliver the note to Priya
            var tray = root.Director.Furniture.Trays["priya"];
            yield return UseAt(tray.transform.position + Vector3.up * 0.06f, 0.8f, new Vector3(9.0f, 0, 13.4f));
            yield return Wait(0.6f);
            yield return Shot("tray_menu");
            ChoiceMenu.AutoPick = 0;
            yield return Wait(1f);
            Log($"theo_note fate={Story.State.FateOf("theo_note")} to={Story.State.DeliveredTo("theo_note")}");

            // Finish everything else quickly, then lights off for the end beat.
            foreach (var s in o.Surfaces.Values) if (s.gameObject.activeSelf) s.ForceComplete();
            foreach (var t in Object.FindObjectsByType<TrashItem>(FindObjectsSortMode.None))
                if (!t.Binned && t.gameObject.activeSelf) { t.MarkBinned(); t.gameObject.SetActive(false); }
            foreach (var c in dir.Furniture.Chairs.Values) c.Tuck(true);
            foreach (var m in dir.Furniture.Monitors.Values) if (m.On && m.CountsForTask) m.SetOn(false, true);
            yield return Wait(1f);
            o.Rooms["reception"].SetLights(false, true);
            yield return UseAt(o.Switches["bullpen"].transform.position, 0.9f, new Vector3(13.6f, 0, 6.5f));
            player.Teleport(new Vector3(13.4f, 0, 8.6f), 10f, 8f);
            yield return Wait(2.5f);
            yield return Aim(dir.Furniture.Monitors["walt"].transform.position + Vector3.up * 0.35f, 0.5f);
            yield return Wait(0.5f);
            yield return Shot("remote_session");
            foreach (var t in dir.Def.Tasks) Log($"task {t.Id}: {dir.Progress(t)} done={dir.IsDone(t)}");

            // Clock out
            player.Teleport(new Vector3(16.4f, 0, 2.5f), -90f, 0f);
            dir.RequestClockOut();
            yield return Wait(3.5f);
            yield return Shot("shift_report");
            Interstitial.AutoAdvance = true;
            yield return Wait(9f);
            yield return Shot("morning_chat");
            Interstitial.AutoAdvance = true;
            yield return Wait(2.5f);
            yield return Shot("night2_card");
        }

        /// <summary>For -ahNight N: photograph each unlocked room with lights on and log the night setup.</summary>
        IEnumerator NightTour()
        {
            var o = root.Office;
            var dir = root.Director;
            yield return WaitUnblocked();
            yield return Wait(0.5f);
            int n = dir.Def.Number;
            Log($"night {n} spawns={dir.Ctx.Spawned.Count} trash={Object.FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Length} evidence={Object.FindObjectsByType<EvidenceItem>(FindObjectsSortMode.None).Length} readables={Object.FindObjectsByType<Readable>(FindObjectsSortMode.None).Length} resets={Resettable.All.Count}");
            foreach (var t in dir.Def.Tasks) Log($"task {t.Id}: {dir.Progress(t)}");
            yield return Shot($"n{n}_closet");
            (string room, Vector3 pos, float yaw, float pitch)[] views =
            {
                ("reception", new Vector3(14.3f, 0, 0.7f), -55f, 12f),
                ("bullpen", new Vector3(8.2f, 0, 6.2f), 35f, 12f),
                ("conference", new Vector3(6.3f, 0, 15.3f), -140f, 14f),
                ("office", new Vector3(18.7f, 0, 15.2f), 140f, 14f),
                ("breakroom", new Vector3(6.4f, 0, 8.6f), -140f, 16f),
            };
            foreach (var v in views)
            {
                if (!dir.Def.Rooms.Contains(v.room)) continue;
                root.Player.Teleport(v.pos, v.yaw, v.pitch);
                o.Rooms[v.room].SetLights(true, true);
                yield return Wait(0.6f);
                yield return Shot($"n{n}_{v.room}");
            }
            if (Story.State.Has("has_uv_torch") || n >= 2)
            {
                Story.State.Set("has_uv_torch");
                root.Player.Teleport(new Vector3(9.5f, 0, 8.6f), -90f, 2f);
                o.Rooms["bullpen"].SetLights(false, true);
                input.TorchOnce = true;
                yield return Wait(0.8f);
                yield return Shot($"n{n}_uv");
                input.TorchOnce = true;
            }
        }

        TrashItem FindTrashNear(Vector3 p)
        {
            TrashItem best = null;
            float bd = 1.5f;
            foreach (var t in Object.FindObjectsByType<TrashItem>(FindObjectsSortMode.None))
            {
                float d = (t.transform.position - p).magnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        /// <summary>Stand within reach of target (horizontal distance d), then look at it and press E.</summary>
        protected IEnumerator UseAt(Vector3 target, float d = 1.2f, Vector3? from = null)
        {
            var p = root.Player.transform.position;
            var dir = (from ?? p) - target;
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.back;
            var stand = target + dir.normalized * d;
            stand.y = 0;
            root.Player.Teleport(stand, root.Player.Yaw, 0);
            yield return null;
            yield return LookAndUse(target, 0.35f);
        }

        protected IEnumerator LookAndUse(Vector3 target, float aimTime)
        {
            InspectView.AutoChoice = null;
            ChoiceMenu.AutoPick = -1;
            Interstitial.AutoAdvance = false;
            if (root.Blocked) Log("blocked before use: " + root.BlockerList);
            yield return Aim(target, aimTime);
            yield return null;
            input.InteractOnce = true;
            yield return null;
            yield return null;
        }

        protected IEnumerator SweepWorld(GrimeSurface s, Vector3 a, Vector3 b, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                var p = Vector3.Lerp(a, b, t / seconds);
                LookAtNow(new Vector3(p.x, s.transform.position.y, p.z));
                input.Use = true;
                yield return null;
            }
            input.Use = false;
        }

        // ---- helpers -------------------------------------------------------------------------

        protected void Log(string msg) => Debug.Log($"[Capture] {msg}  (t={Time.time:F1})");

        protected IEnumerator WaitUnblocked(float timeout = 20f)
        {
            float t = 0;
            while ((Interstitial.AnyOpen || root.Blocked) && t < timeout) { t += GameTime.UnscaledDelta; yield return null; }
            if (root.Blocked) Log("still blocked by " + root.BlockerList);
        }

        protected IEnumerator Wait(float s)
        {
            if (Time.captureDeltaTime > 0f)
            {
                for (float t = 0; t < s; t += GameTime.UnscaledDelta) yield return null;
                yield break;
            }
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
