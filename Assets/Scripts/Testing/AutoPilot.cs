using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Self-test: <c>-ahAutopilot &lt;dir&gt;</c>. Starts at the title, plays all seven nights through the
    /// real components (inspect choices, trays, shredders, punch clock, light switches, the real
    /// brush maths on every dirty surface, physics drops into bins, some real mouse-and-key input)
    /// along the "audit" story route, checks every night completes and the ending resolves, saves
    /// screenshots and logs PASS/FAIL lines with an [AutoPilot] prefix.
    /// </summary>
    public class AutoPilot : CaptureDirector
    {
        protected override string ArgName => "-ahAutopilot";
        int passes, fails;

        // Frame times while a night is being played (real-time runs only; the showcase fixes the step).
        readonly System.Collections.Generic.List<float> frameTimes = new();
        bool sampling;

        void Update()
        {
            if (sampling && Time.captureFramerate == 0) frameTimes.Add(Time.unscaledDeltaTime * 1000f);
        }

        void LogFrameStats(int n)
        {
            if (frameTimes.Count < 30) return;
            var sorted = frameTimes.OrderBy(x => x).ToList();
            float P(float q) => sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
            Debug.Log($"[Perf] night {n}: {sorted.Count} frames, avg {1000f / sorted.Average():F0} fps, " +
                      $"median {P(0.5f):F1} ms, p95 {P(0.95f):F1} ms, p99 {P(0.99f):F1} ms, worst {sorted[^1]:F0} ms");
        }

        void Check(bool ok, string what)
        {
            if (ok) passes++; else fails++;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {what}  (t={Time.time:F1})");
        }

        protected override IEnumerator Run()
        {
            Debug.Log("[AutoPilot] started");
            // Test windows usually sit behind others, and Wayland throttles hidden windows' vsync
            // to a crawl (11-20 fps here). Run uncapped so timings measure the game, not the compositor.
            if (Time.captureFramerate == 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            yield return Wait(3f);
            Check(TitleScreen.Instance != null, "title screen shows on boot");
            yield return Shot("title");
            StoryState.DeleteAll();
            Story.State = new StoryState();
            TitleScreen.Instance?.Begin(1);
            int last = scenario.StartsWith("night") && int.TryParse(scenario.Substring(5), out var only) ? only : NightDefs.Count;
            for (int n = 1; n <= last; n++)
            {
                yield return PlayNight(n);
            }
            if (last < NightDefs.Count)
            {
                Debug.Log($"[AutoPilot] done: {passes} passed, {fails} failed (stopped after night {last})");
                Application.Quit();
                yield break;
            }
            // Ending
            float t = 0;
            while (FindAnyObjectByType<EndingScreen>() == null && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            Check(FindAnyObjectByType<EndingScreen>() != null, "ending screen appears after night 7");
            yield return Wait(8f);
            yield return Shot("ending");
            Check(Story.State.Ending == "audit", $"audit route reaches the audit ending (got {Story.State.Ending}, score {Endings.AuditScore(Story.State)})");
            for (int i = 0; i < 8; i++) { Interstitial.AutoAdvance = true; yield return Wait(0.4f); }
            yield return Wait(2f);
            Check(TitleScreen.Instance != null, "ending returns to the title");
            yield return Shot("back_to_title");
            Debug.Log($"[AutoPilot] done: {passes} passed, {fails} failed");
            yield return Wait(0.5f);
            Application.Quit();
        }

        // =========================================================================================

        /// <summary>Hook for subclasses (the showcase recorder) at fixed points of each night.</summary>
        protected virtual IEnumerator Beat(string phase, int n) { yield break; }
        /// <summary>Hook: get within reach of <paramref name="target"/> and look at it before using it.</summary>
        protected virtual IEnumerator Approach(Vector3 target, float reach = 1.1f) { yield break; }
        /// <summary>Hook: clean a surface on camera. By default the brush maths runs directly.</summary>
        protected virtual IEnumerator ShowClean(GrimeSurface s) => CleanSurface(s);
        /// <summary>Hook: foam a window on camera before the route finishes the spray directly.</summary>
        protected virtual IEnumerator ShowSpray(GrimeSurface s) { yield break; }
        protected virtual float ReadTime => 0.35f;
        protected virtual float MenuTime => 0.3f;

        IEnumerator PlayNight(int n)
        {
            yield return WaitUnblocked(40f);
            yield return Beat("start", n);
            var dir = root.Director;
            Check(dir.Def != null && dir.Def.Number == n && dir.Running && !dir.Paused, $"night {n} starts and runs");
            float tilt = Vector3.Angle(root.Player.Camera.transform.up, Vector3.up);
            Check(tilt < 60f, $"night {n}: the camera starts upright (tilt {tilt:F0}°)");
            yield return Wait(0.5f);
            yield return Shot($"n{n}_start");
            float start = Time.realtimeSinceStartup;
            frameTimes.Clear();
            sampling = true;
            foreach (var room in dir.Def.Rooms)
                if (root.Office.Rooms.TryGetValue(room, out var r) && !r.LightsOn && root.Office.Switches.TryGetValue(room, out var sw)) sw.Toggle();

            yield return Route(n);
            yield return Beat("afterRoute", n);
            yield return CompleteTasks(n);

            // The remote-session beat ignores lights going off in the first minute of the night.
            if (n == 1) for (float t = 0; dir.Elapsed < 62f && t < 90f; t += Time.unscaledDeltaTime) yield return null;
            yield return Beat("lockup", n);
            // Lock up: lights off through the real switches.
            foreach (var room in dir.Def.Rooms.Concat(new[] { "closet" }))
                if (root.Office.Rooms.TryGetValue(room, out var r) && r.LightsOn && root.Office.Switches.TryGetValue(room, out var sw)) sw.Toggle();
            yield return Wait(1.5f);
            yield return Beat("dark", n);
            if (n == 1) yield return ReadMonitor("walt", "screen_remote", "the 1 AM remote session appears when the bullpen goes dark");
            foreach (var room in dir.Def.Rooms)
                if (root.Office.Rooms.TryGetValue(room, out var r) && r.LightsOn && root.Office.Switches.TryGetValue(room, out var sw)) sw.Toggle();
            yield return Wait(0.6f);
            foreach (var t in dir.Def.Tasks) Log($"night {n} task {t.Id}: {dir.Progress(t)} done={dir.IsDone(t)}{(t.Optional ? " (optional)" : "")}");
            var remaining = dir.RequiredRemaining().ToList();
            Check(remaining.Count == 0, $"night {n}: every required task completed" + (remaining.Count > 0 ? " (left: " + string.Join(", ", remaining.Select(x => x.Id)) + ")" : ""));
            Log($"night {n}: secrets {dir.SecretsFoundCount}/{dir.Def.Secrets.Count}, {Time.realtimeSinceStartup - start:F0}s");

            sampling = false;
            LogFrameStats(n);

            // Clock out at the punch clock.
            yield return Beat("clockout", n);
            root.Player.Teleport(new Vector3(16.0f, 0, 2.5f), -90f, 0);
            var clock = FindAnyObjectByType<PunchClock>();
            yield return Approach(clock.transform.position, 0.9f);
            clock.Interact(null);
            yield return Wait(0.6f);
            if (ChoiceMenu.IsOpen) { yield return Wait(MenuTime); ChoiceMenu.AutoPick = 0; }
            float w = 0;
            while (!Interstitial.AnyOpen && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
            Check(Story.State.ResultFor(n) != null, $"night {n}: clocking out records a result (grade {Story.State.ResultFor(n)?.Grade})");
            yield return Wait(3.2f);
            yield return Shot($"n{n}_report");
            yield return Beat("report", n);
            Interstitial.AutoAdvance = true;
            yield return Wait(9f);
            yield return Shot($"n{n}_chat");
            for (int i = 0; i < 40 && FindAnyObjectByType<ChatInterlude>() != null; i++) { Interstitial.AutoAdvance = true; yield return Wait(0.25f); }
            Interstitial.AutoAdvance = false;
            yield return Beat("end", n);
            yield return Wait(1.0f);
        }

        // ---- the story route ("audit") --------------------------------------------------------

        IEnumerator Route(int n)
        {
            var dir = root.Director;
            var ctx = dir.Ctx;
            switch (n)
            {
                case 1:
                    yield return ReadReadable("dana_welcome");
                    yield return ReadLocker("walt_note_1");
                    yield return RealInputNight1();
                    yield return Evidence("theo_note", InspectChoice.Keep);
                    yield return ReadMonitor("theo", "screen_theo_email", "Theo's email can be read");
                    yield return Deliver("theo_note", "priya");
                    break;
                case 2:
                    yield return ReadLocker("walt_note_2");
                    Check(Story.State.Has("has_uv_torch"), "Walt's locker gives the UV torch");
                    yield return ReadReadable("walt_card");
                    yield return ReadReadable("fridge_note");
                    {
                        var win = ctx.Surface("win_break_2");
                        yield return ShowSpray(win);
                        for (float u = 0.1f; u <= 0.9f; u += 0.08f)
                            for (float v = 0.2f; v <= 0.8f; v += 0.1f) win.Spray(new Vector2(u, v), 0.25f, 1f);
                        yield return Wait(0.4f);
                        Check(dir.HasSecret("window_message"), "foam reveals the finger writing");
                        root.Player.Teleport(new Vector3(1.6f, 0, 7.2f), -90f, 0f);
                        yield return Aim(win.transform.position, 0.3f);
                        yield return Wait(0.3f);
                        yield return Shot("n2_window_writing");
                    }
                    yield return Evidence("russ_slip", InspectChoice.Keep);
                    yield return Deliver("russ_slip", "priya");
                    // UV arrows
                    root.Player.Teleport(new Vector3(9.0f, 0, 8.4f), -90f, 0f);
                    if (!UvTorch.Instance.On) input.TorchOnce = true;
                    yield return Aim(new Vector3(7.08f, 1.2f, 8.2f), 0.3f);
                    yield return Wait(0.6f);
                    yield return Shot("n2_uv");
                    Check(dir.HasSecret("uv_arrows"), "the UV torch reveals Walt's arrows");
                    input.TorchOnce = true;
                    yield return Wait(0.2f);
                    break;
                case 3:
                    {
                        var wb = ctx.Surface("whiteboard_conf");
                        yield return ShowClean(wb);
                        Check(wb.GhostWasRevealed && dir.HasSecret("whiteboard_ghost"), "erasing the whiteboard reveals the ghost writing");
                        root.Player.Teleport(new Vector3(2.6f, 0, 11.0f), 180f, 4f);
                        yield return Wait(0.8f);
                        yield return Shot("n3_whiteboard_ghost");
                    }
                    yield return ReadReadable("audit_agenda");
                    yield return Evidence("theo_planner", InspectChoice.Keep);
                    yield return Deliver("theo_planner", "priya");
                    break;
                case 4:
                    {
                        var rub = GrimeSurface.All.FirstOrDefault(s => s.Id == "notepad_rub");
                        Check(rub != null, "Marian's notepad can be rubbed");
                        if (rub != null) yield return ShowClean(rub);
                        var pad = ctx.Get("notepad").GetComponent<ScriptedUse>();
                        yield return Approach(pad.transform.position, 0.8f);
                        yield return Inspect(() => pad.Interact(null), InspectChoice.Keep);
                        Check(Story.State.FateOf("notepad_rubbing") == Fate.Kept, "the rubbing can be torn off and kept");
                        var fc2 = GameObject.Find("FURN_filing_fc2").GetComponent<ScriptedUse>();
                        yield return Approach(fc2.transform.position + Vector3.up * 0.8f);
                        fc2.Interact(null);
                        yield return Wait(0.5f);
                        yield return Evidence("northgate_invoices", InspectChoice.Keep);
                        var env = ctx.Get("envelope").GetComponent<ScriptedUse>();
                        yield return Approach(env.transform.position);
                        env.Interact(null);
                        yield return Wait(0.6f);
                        yield return Wait(ReadTime);
                        InspectView.AutoChoice = InspectChoice.Close;
                        yield return Wait(0.8f);
                        yield return Wait(MenuTime);
                        if (ChoiceMenu.IsOpen) ChoiceMenu.AutoPick = 1;
                        yield return Wait(0.6f);
                        Check(Story.State.Has("left_money"), "the money can be left on the desk");
                        var bag = ctx.Furniture.Named.TryGetValue("shredder_office", out var sgo) ? sgo.GetComponent<ScriptedUse>() : null;
                        Check(bag != null, "the office shredder offers its bag");
                        if (bag != null) yield return Approach(bag.transform.position + Vector3.up * 0.6f);
                        bag?.Interact(null);
                        yield return Wait(0.6f);
                        yield return Wait(MenuTime);
                        if (ChoiceMenu.IsOpen) ChoiceMenu.AutoPick = 1;
                        yield return Wait(0.6f);
                        Check(Story.State.Has("kept_shreds"), "the shredder bag can be kept");
                        yield return ReadMonitor("marian", "screen_marian_lock", "Marian's lock screen shows the 1 AM sign-in");
                    }
                    break;
                case 5:
                    {
                        var box = ctx.Get("shred_bag_box");
                        Check(box != null, "the kept shredder bag waits on the closet table");
                        if (box != null)
                        {
                            yield return Approach(box.transform.position);
                            box.GetComponent<ScriptedUse>().Interact(null);
                            yield return Wait(1.0f);
                            yield return Wait(ReadTime);
                            yield return Shot("n5_shred_puzzle");
                            ShredPuzzle.AutoSolve = true;
                            float t = 0;
                            while (!InspectView.IsOpen && t < 6f) { t += Time.unscaledDeltaTime; yield return null; }
                            InspectView.AutoChoice = InspectChoice.Keep;
                            yield return Wait(0.8f);
                            Check(Story.State.FateOf("reconstructed_invoice") == Fate.Kept, "the shred puzzle rebuilds the invoice");
                        }
                        yield return Evidence("vpn_log", InspectChoice.Keep);
                        yield return Evidence("theo_resignation", InspectChoice.Close);
                        var tile = FindObjectsByType<ScriptedUse>(FindObjectsSortMode.None).FirstOrDefault(s => s.name == "CeilingTile");
                        Check(tile != null, "Walt's ceiling tile is there with the UV torch");
                        if (tile != null)
                        {
                            yield return Approach(tile.transform.position, 0.6f);
                            tile.Interact(null);
                            yield return Wait(1.5f);
                            yield return Evidence("walt_letter", InspectChoice.Keep);
                        }
                        dir.SetClock(332.9f);
                        yield return Wait(1.2f);
                        yield return ReadReadable("printout");
                        Check(dir.HasSecret("priya_ally"), "Priya's 3:33 AM printout");
                    }
                    break;
                case 6:
                    {
                        Check(ctx.Furniture.Trays.ContainsKey("auditor"), "the auditor's tray appears on night 6");
                        var box = ctx.Get("archive_1").GetComponent<ScriptedUse>();
                        box.Interact(null);
                        yield return Wait(0.6f);
                        InspectView.AutoChoice = InspectChoice.Close;
                        yield return Wait(0.8f);
                        yield return Evidence("payment_ledger", InspectChoice.Keep);
                        var phone = ctx.Furniture.Named["phone_reception"].GetComponent<ScriptedUse>();
                        yield return Inspect(() => phone.Interact(null), InspectChoice.Close);
                        yield return ReadReadable("dana_doubt");
                        foreach (var id in Story.State.Inventory.ToList())
                            if (Docs.Get(id)?.Key == true) yield return Deliver(id, "auditor");
                        Log("auditor score now " + Endings.AuditScore(Story.State));
                    }
                    break;
                case 7:
                    {
                        var shred = ctx.Furniture.Shredders["office"];
                        Check(shred.Jammed, "the office shredder is jammed on night 7");
                        root.Player.Teleport(new Vector3(23.0f, 0, 10.8f), 180f, 30f);
                        yield return Aim(shred.transform.position + Vector3.up * 0.6f, 0.3f);
                        yield return Shot("n7_jam");
                        shred.Interact(null);
                        float t = 0;
                        while (!InspectView.IsOpen && t < 4f) { t += Time.unscaledDeltaTime; yield return null; }
                        yield return Wait(0.6f);
                        yield return Shot("n7_red_folder");
                        InspectView.AutoChoice = InspectChoice.Keep;
                        yield return Wait(0.8f);
                        Check(Story.State.FateOf("red_folder") == Fate.Kept, "the red folder can be pulled from the jam");
                        yield return Evidence("flight_note", InspectChoice.Close);
                        yield return Deliver("red_folder", "auditor");
                        Check(Story.State.IsDelivered("red_folder", "auditor"), "the red folder reaches the auditor");
                    }
                    break;
            }
        }

        // ---- real input on night 1 -------------------------------------------------------------

        IEnumerator RealInputNight1()
        {
            var o = root.Office;
            // Wipe the reception desk with the mouse.
            var desk = o.Surfaces["desk_reception"];
            root.Player.Teleport(new Vector3(11.5f, 0, 2.3f), 0f, 30f);
            yield return Sweep(desk, new Rect(0.03f, 0.15f, 0.94f, 0.7f), 2, 2.2f, true, true, false);
            yield return Sweep(desk, new Rect(0.03f, 0.15f, 0.94f, 0.7f), 2, 2.2f, true, true, false);
            yield return Sweep(desk, new Rect(0.03f, 0.2f, 0.94f, 0.6f), 2, 2.2f, true, true, false);
            Check(desk.Completion > 0.6f, $"real input: wiping the reception desk cleans it ({desk.Completion:P0})");
            yield return Shot("n1_wiped_desk");
            // Pick up a cup with E and throw it into the bin with LMB.
            var cup = FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(t => t.Kind == TrashKind.General && !t.Binned)
                .OrderBy(t => (t.transform.position - new Vector3(13.9f, 0, 4.05f)).sqrMagnitude).FirstOrDefault();
            if (cup != null)
            {
                yield return UseAt(cup.transform.position, 1.0f, new Vector3(13.4f, 0, 2.4f));
                yield return Wait(0.4f);
                Check(root.Hands.Holding == cup, "real input: E picks up a cup");
                var anchor = o.Anchor("BIN_trash_reception_1").position;
                var bin = root.Director.Furniture.Bins.OrderBy(b => (b.transform.position - anchor).sqrMagnitude).First();
                var spot = ThrowSpot(bin, 3.6f) ?? ThrowSpot(bin, 2.8f) ?? ThrowSpot(bin, 2.2f);
                Check(spot.HasValue, "a clear throwing spot exists near the reception bin");
                root.Player.Teleport(spot ?? anchor + new Vector3(-2.2f, 0, 0), 0, 0);
                yield return Aim(bin.transform.position + Vector3.up * bin.Height, 0.4f);
                // Hold LMB to full charge (charge clamps at 1, so the throw doesn't depend on frame
                // timing), keep the solved pitch while the held cup settles, then release.
                input.Use = true;
                for (float t = 0; t < 0.9f; t += Time.deltaTime)
                {
                    if (SolvePitch(cup.transform.position, bin, 1f) is float p) root.Player.Pitch = p;
                    yield return null;
                }
                var arcLine = GameObject.Find("ThrowArc")?.GetComponent<LineRenderer>();
                Check(arcLine != null && arcLine.positionCount > 4, $"charging a throw draws the arc preview ({arcLine?.positionCount} points)");
                yield return Shot("n1_throw_arc");
                for (int f = 0; f < 20; f++)
                {
                    if (SolvePitch(cup.transform.position, bin, 1f) is float p) root.Player.Pitch = p;
                    yield return null;
                }
                input.Use = false;
                float solved = root.Player.Pitch;
                yield return null;
                Log($"throw: pitch {solved:F1} from {cup.transform.position} v={cup.Body.linearVelocity} bin {bin.transform.position} h={bin.Height}");
                float closest = float.MaxValue;
                for (float t = 0; t < 2f && !cup.Binned; t += Time.deltaTime)
                {
                    var d = cup.transform.position - bin.transform.position;
                    if (d.y < bin.Height + 0.1f) closest = Mathf.Min(closest, new Vector2(d.x, d.z).magnitude);
                    yield return null;
                }
                Log($"throw: closest {closest:F2} m at mouth height, cup ends at {cup.transform.position}");
                Check(cup.Binned, "real input: a charged throw lands the cup in the bin");
                if (!cup.Binned && root.Hands.Holding == cup) { input.DropOnce = true; yield return Wait(0.3f); }
            }
            // Vacuum under Theo's desk until the key clunks out.
            var floor = o.Surfaces["floor_bullpen"];
            root.Player.Teleport(new Vector3(10.45f, 0, 10.05f), 0f, 55f);
            input.Crouch = true;
            yield return Wait(0.5f);
            for (int i = 0; i < 4 && GameObject.Find("key") == null; i++)
            {
                yield return SweepWorld(floor, new Vector3(9.95f, 0, 11.3f + i * 0.2f), new Vector3(10.9f, 0, 11.3f + i * 0.2f), 0.7f);
                yield return SweepWorld(floor, new Vector3(10.9f, 0, 11.4f + i * 0.2f), new Vector3(9.95f, 0, 11.4f + i * 0.2f), 0.7f);
            }
            input.Crouch = false;
            var key = GameObject.Find("key");
            Check(key != null, "real input: vacuuming under Theo's desk knocks a key loose");
            if (key == null)
            {
                for (float u = 0; u < 1f; u += 0.02f) floor.Stroke(floor.WorldToUv(new Vector3(10.45f, 0, 11.75f), out var uv) ? uv : Vector2.zero, uv, ToolDefs.Vacuum, 0.1f, 0);
                yield return Wait(0.3f);
                key = GameObject.Find("key");
            }
            yield return Wait(1.0f);
            if (key != null) key.GetComponent<KeyPickup>().Interact(null);
            Check(Story.State.Has("has_key_fc2"), "the FC-2 key goes on the key ring");
        }

        // ---- task completion through real components -------------------------------------------

        IEnumerator CompleteTasks(int n)
        {
            var dir = root.Director;
            // Surfaces: the real brush maths, stroke by stroke.
            foreach (var t in dir.Def.Tasks.Where(t => t.Kind == TaskKind.Clean))
                foreach (var id in t.Targets)
                    if (root.Office.Surfaces.TryGetValue(id, out var s) && s.gameObject.activeSelf && !s.Done && (!t.Optional || id != "win_break_2"))
                        yield return CleanSurface(s);
            // Trash: drop every item into a matching bin and let physics do the rest.
            var bins = dir.Furniture.Bins;
            var items = FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(x => x.gameObject.activeInHierarchy && !x.Binned).ToList();
            int i = 0;
            foreach (var item in items)
            {
                var bin = bins.Where(b => b.Accepts(item.Kind)).OrderBy(b => (b.transform.position - item.transform.position).sqrMagnitude).FirstOrDefault();
                if (bin == null) { Check(false, $"night {n}: no bin accepts {item.Kind}"); continue; }
                if (root.Hands.Holding == item) { input.DropOnce = true; yield return null; }
                var drop = bin.transform.position + Vector3.up * (bin.Height + 0.3f) + new Vector3((i % 3 - 1) * 0.03f, i % 5 * 0.15f, 0);
                item.Body.isKinematic = false;
                item.Body.position = drop;
                item.transform.position = drop;
                item.Body.linearVelocity = Vector3.down * 0.5f;
                item.Body.WakeUp();
                i++;
                yield return Wait(0.15f);
            }
            yield return Wait(2.5f);
            int missed = 0;
            foreach (var item in items)
                if (item && !item.Binned)
                {
                    missed++;
                    var b = FindNearestBin(item);
                    Log($"night {n}: {item.Id} ({item.Kind}) missed; at {item.transform.position}, kinematic={item.Body.isKinematic} locked={item.Locked}, bin {b?.name} at {b?.transform.position}");
                    b?.Receive(item);
                }
            Check(missed == 0, $"night {n}: {items.Count} pieces of rubbish fall into their bins ({missed} needed a nudge)");
            // Things back where they belong.
            foreach (var r in Resettable.All.ToList())
                if (r.Required && !r.AtHome) { r.SnapFirstFree(); yield return Wait(0.05f); }
            yield return Wait(0.6f);
            foreach (var c in dir.Furniture.Chairs.Values) c.Tuck();
            foreach (var m in dir.Furniture.Monitors.Values) if (m.On && m.CountsForTask) m.SetOn(false);
            yield return Wait(1.0f);
        }

        Bin FindNearestBin(TrashItem item) => root.Director.Furniture.Bins.Where(b => b.Accepts(item.Kind))
            .OrderBy(b => (b.transform.position - item.transform.position).sqrMagnitude).FirstOrDefault();

        /// <summary>A standing spot <paramref name="range"/> from the bin with room for the player and a clear view of its mouth.</summary>
        static Vector3? ThrowSpot(Bin bin, float range)
        {
            var b = bin.transform.position;
            var mouth = b + Vector3.up * (bin.Height + 0.25f);
            for (int a = 0; a < 24; a++)
            {
                float ang = a * 15f * Mathf.Deg2Rad;
                var pos = b + new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * range;
                var eye = pos + Vector3.up * FirstPersonController.StandEye;
                if (Physics.CheckCapsule(pos + Vector3.up * 0.35f, pos + Vector3.up * 1.45f, 0.32f, Layers.WalkMask, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, 0.7f, Layers.WalkMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(eye, mouth, Layers.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(eye, b + Vector3.up * 1.6f, Layers.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                return pos;
            }
            return null;
        }

        /// <summary>Camera pitch for which a throw at charge <paramref name="c"/> from <paramref name="from"/> drops into the bin (same maths as <see cref="Hands"/>).</summary>
        float? SolvePitch(Vector3 from, Bin bin, float c)
        {
            var target = bin.transform.position;
            float targetY = target.y + bin.Height;
            float yaw = root.Player.Yaw;
            float best = float.MaxValue, bestPitch = 0;
            for (float pitch = -45f; pitch <= 70f; pitch += 0.1f)
            {
                var f = Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
                var v = f * Mathf.Lerp(3.2f, 10f, c * c) + Vector3.up * Mathf.Lerp(0.8f, 1.6f, c);
                float g = -Physics.gravity.y, dy = from.y - targetY;
                float disc = v.y * v.y + 2f * g * dy;
                if (disc < 0) continue;
                float t = (v.y + Mathf.Sqrt(disc)) / g;
                var land = from + new Vector3(v.x, 0, v.z) * t;
                float miss = new Vector2(land.x - target.x, land.z - target.z).magnitude;
                if (miss < best) { best = miss; bestPitch = pitch; }
            }
            return best < 0.12f ? bestPitch : null;
        }

        /// <summary>Clean a surface with its tool's real brush: boustrophedon strokes until it's done.</summary>
        IEnumerator CleanSurface(GrimeSurface s)
        {
            var brush = ToolDefs.For(s.Tool);
            float r = brush.Radius;
            float lane = r * 1.1f;
            float step = Mathf.Min(0.05f, r * 0.4f);
            int calls = 0;
            for (int pass = 0; pass < 6 && !s.Done; pass++)
            {
                if (s.Tool == ToolKind.Squeegee || s.Tool == ToolKind.Cloth)
                    for (float x = 0; x <= s.Size.x; x += 0.2f)
                        for (float y = 0; y <= s.Size.y; y += 0.2f)
                            s.Spray(new Vector2(x / s.Size.x, y / s.Size.y), 0.25f, 1f);
                int row = 0;
                for (float y = lane * 0.5f + (pass % 2) * lane * 0.5f; y < s.Size.y + lane * 0.5f && !s.Done; y += lane, row++)
                {
                    float vy = Mathf.Clamp01(y / s.Size.y);
                    Vector2 prev = new(row % 2 == 0 ? 0f : 1f, vy);
                    for (float x = step; x <= s.Size.x + step && !s.Done; x += step)
                    {
                        float ux = Mathf.Clamp01(x / s.Size.x);
                        var cur = new Vector2(row % 2 == 0 ? ux : 1f - ux, vy);
                        s.Stroke(prev, cur, brush, step / 0.6f, 0.6f);
                        if (s.HasGhost && s.Spec.GhostScrubbable && s.Done) break;
                        prev = cur;
                        if (++calls % 300 == 0) yield return null;
                    }
                }
            }
            if (s.Spec != null && s.Spec.GhostScrubbable) { }
            Check(s.Done, $"surface {s.Id} can be cleaned with the {s.Tool} ({s.Completion:P0})");
            yield return null;
        }

        // ---- small helpers -----------------------------------------------------------------------

        IEnumerator Inspect(Action open, InspectChoice choice)
        {
            open();
            float t = 0;
            while (!InspectView.IsOpen && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(ReadTime);
            InspectView.AutoChoice = choice;
            t = 0;
            while (InspectView.IsOpen && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.2f);
        }

        IEnumerator Evidence(string doc, InspectChoice choice)
        {
            var go = root.Director.Ctx.Get(doc);
            var ev = go != null ? go.GetComponent<EvidenceItem>() : null;
            Check(ev != null, $"evidence {doc} is in the office");
            if (ev == null) yield break;
            yield return Approach(ev.transform.position);
            yield return Inspect(() => ev.Interact(null), choice);
            if (choice == InspectChoice.Keep) Check(Story.State.FateOf(doc) == Fate.Kept, $"evidence {doc} can be kept");
            else Check(Story.State.FateOf(doc) != Fate.Untouched, $"evidence {doc} can be read");
        }

        IEnumerator ReadReadable(string doc)
        {
            var r = FindObjectsByType<Readable>(FindObjectsSortMode.None).FirstOrDefault(x => x.Doc == doc || x.name == doc);
            Check(r != null, $"readable {doc} is placed");
            if (r == null) yield break;
            yield return Approach(r.transform.position);
            yield return Inspect(() => r.Interact(null), InspectChoice.Close);
        }

        IEnumerator ReadLocker(string doc)
        {
            var locker = GameObject.Find("FURN_locker_walt")?.GetComponent<Readable>();
            Check(locker != null && locker.Doc == doc, $"Walt's locker holds {doc}");
            if (locker == null) yield break;
            yield return Approach(locker.transform.position + Vector3.up * 1.3f, 1.0f);
            yield return Inspect(() => locker.Interact(null), InspectChoice.Close);
        }

        IEnumerator ReadMonitor(string desk, string doc, string what)
        {
            var m = root.Director.Furniture.Monitors.TryGetValue(desk, out var mm) ? mm : null;
            Check(m != null && m.On && m.ScreenDoc == doc, what);
            if (m == null || !m.On || m.ScreenDoc != doc) yield break;
            yield return Approach(m.transform.position + Vector3.up * 0.2f, 0.9f);
            yield return Inspect(() => m.Interact(null), InspectChoice.SwitchOff);
        }

        IEnumerator Deliver(string doc, string person)
        {
            var trays = root.Director.Furniture.Trays;
            Check(trays.ContainsKey(person), $"{person}'s tray exists");
            if (!trays.ContainsKey(person)) yield break;
            int index = Story.State.Inventory.IndexOf(doc);
            Check(index >= 0, $"{doc} is in your pocket to deliver");
            if (index < 0) yield break;
            yield return Approach(trays[person].transform.position);
            trays[person].Interact(null);
            float t = 0;
            while (!ChoiceMenu.IsOpen && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(MenuTime);
            ChoiceMenu.AutoPick = index;
            yield return Wait(0.5f);
            Check(Story.State.IsDelivered(doc, person), $"{doc} delivered to {person}");
        }
    }
}
