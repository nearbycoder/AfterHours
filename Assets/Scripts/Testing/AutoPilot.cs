using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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

        /// <summary>
        /// Story route (<c>-ahRoute</c>): "audit" delivers everything to the auditor; "loose" keeps the
        /// red folder; "cleanbooks" bins it; "spotless" reads everything and keeps nothing.
        /// </summary>
        protected string route = "audit";
        bool Keeps => route != "spotless";
        /// <summary>"marian": hold everything until her office opens, then side with her.</summary>
        bool Hoards => route == "marian";
        bool HandsOut => Keeps && !Hoards;
        InspectChoice Take => Keeps ? InspectChoice.Keep : InspectChoice.Close;

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
            route = GameRoot.Arg("-ahRoute") ?? "audit";
            Debug.Log($"[AutoPilot] started, route {route}");
            // Test windows usually sit behind others, and Wayland throttles hidden windows' vsync
            // to a crawl (11-20 fps here). Run uncapped so timings measure the game, not the compositor.
            if (Time.captureFramerate == 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            yield return Wait(3f);
            Check(TitleScreen.Instance != null, "title screen shows on boot");
            yield return Shot("title");
            if (PadChecks) yield return PadTitle();
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
            string expected = route == "marian" ? "cleanbooks" : route;
            if (route == "marian")
            {
                var ending = Endings.Resolve(Story.State.Clone());
                Check(ending.Lines.Any(l => l.Contains("fifty dollars")), "taking Marian's money shows up in the epilogue");
            }
            Check(Story.State.Ending == expected, $"{route} route reaches the {expected} ending (got {Story.State.Ending}, score {Endings.AuditScore(Story.State)})");
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
            CheckOverlaps(n);
            if (n == 1 && PadChecks)
            {
                Clipboard.Instance.Show();
                yield return Wait(0.7f);
                yield return Shot("clipboard");
                Clipboard.Instance.Close();
                yield return Wait(0.4f);
                PauseMenu.Show();
                yield return Wait(0.6f);
                Check(PauseMenu.IsOpen, "the pause menu opens during a night");
                yield return Shot("pause");
                GameObject.Find("Btn_Restart this night")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
                yield return Wait(0.5f);
                Check(ChoiceMenu.IsOpen, "Restart this night asks first");
                yield return Shot("restart_confirm");
                ChoiceMenu.AutoPick = 99; // "Never mind" (clamped to the last option)
                yield return Wait(0.4f);
                PauseMenu.Instance.Close();
                yield return WaitUnblocked(3f);
                Check(!root.Blocked, "closing the pause menu hands control back");
            }
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
                    if (vpad != null && Keeps) yield return PadEvidenceKeep("theo_note");
                    else yield return Evidence("theo_note", Take);
                    yield return ReadMonitor("theo", "screen_theo_email", "Theo's email can be read");
                    if (vpad != null && HandsOut) yield return PadDeliver("theo_note", "priya");
                    else if (HandsOut) yield return Deliver("theo_note", "priya");
                    UnplugPad();
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
                    yield return Evidence("russ_slip", Take);
                    if (HandsOut) yield return Deliver("russ_slip", "priya");
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
                    yield return Evidence("theo_planner", Take);
                    if (HandsOut) yield return Deliver("theo_planner", "priya");
                    break;
                case 4:
                    {
                        var rub = GrimeSurface.All.FirstOrDefault(s => s.Id == "notepad_rub");
                        Check(rub != null, "Marian's notepad can be rubbed");
                        if (rub != null) yield return ShowClean(rub);
                        var pad = ctx.Get("notepad").GetComponent<ScriptedUse>();
                        yield return Approach(pad.transform.position, 0.8f);
                        yield return Inspect(() => pad.Interact(null), Take);
                        if (Keeps) Check(Story.State.FateOf("notepad_rubbing") == Fate.Kept, "the rubbing can be torn off and kept");
                        else Check(Story.State.FateOf("notepad_rubbing") is Fate.Untouched or Fate.Seen, "the rubbing can be read and left on the pad");
                        var fc2 = GameObject.Find("FURN_filing_fc2").GetComponent<ScriptedUse>();
                        yield return Approach(fc2.transform.position + Vector3.up * 0.8f);
                        fc2.Interact(null);
                        yield return Wait(0.5f);
                        yield return Evidence("northgate_invoices", Take);
                        var env = ctx.Get("envelope").GetComponent<ScriptedUse>();
                        yield return Approach(env.transform.position);
                        env.Interact(null);
                        yield return Wait(0.6f);
                        yield return Wait(ReadTime);
                        InspectView.AutoChoice = InspectChoice.Close;
                        yield return Wait(0.8f);
                        yield return Wait(MenuTime);
                        if (ChoiceMenu.IsOpen) ChoiceMenu.AutoPick = Hoards ? 0 : 1;
                        yield return Wait(0.6f);
                        if (Hoards) Check(Story.State.Has("took_money"), "the money can be taken");
                        else Check(Story.State.Has("left_money"), "the money can be left on the desk");
                        var bag = ctx.Furniture.Named.TryGetValue("shredder_office", out var sgo) ? sgo.GetComponent<ScriptedUse>() : null;
                        Check(bag != null, "the office shredder offers its bag");
                        if (bag != null) yield return Approach(bag.transform.position + Vector3.up * 0.6f);
                        bag?.Interact(null);
                        yield return Wait(0.6f);
                        yield return Wait(MenuTime);
                        if (ChoiceMenu.IsOpen) ChoiceMenu.AutoPick = Keeps ? 1 : 0;
                        yield return Wait(0.6f);
                        if (Keeps) Check(Story.State.Has("kept_shreds"), "the shredder bag can be kept");
                        else Check(Story.State.Has("shred_bag_done") && !Story.State.Has("kept_shreds"), "the shredder bag can go down the chute as asked");
                        yield return ReadMonitor("marian", "screen_marian_lock", "Marian's lock screen shows the 1 AM sign-in");
                        if (Hoards)
                        {
                            foreach (var d in new[] { "theo_note", "russ_slip", "theo_planner" }) yield return Deliver(d, "marian");
                            var chat = NightDefs.Get(4).Chat.Where(c => c.When == null || c.When(Story.State)).Select(c => c.Text).ToList();
                            Check(chat.Contains("Theo, my office please.") && chat.Any(t => t.StartsWith("Russ, a word please")),
                                "Marian reacts in the morning chat to what was left in her tray");
                        }
                    }
                    break;
                case 5:
                    {
                        var box = ctx.Get("shred_bag_box");
                        if (Keeps) Check(box != null, "the kept shredder bag waits on the closet table");
                        else Check(box == null, "no shredder bag on the closet table after sending it down the chute");
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
                        yield return Evidence("vpn_log", Take);
                        if (Hoards) yield return ShredAt("vpn_log", "bullpen");
                        yield return Evidence("theo_resignation", InspectChoice.Close);
                        var tile = FindObjectsByType<ScriptedUse>(FindObjectsSortMode.None).FirstOrDefault(s => s.name == "CeilingTile");
                        Check(tile != null, "Walt's ceiling tile is there with the UV torch");
                        if (tile != null)
                        {
                            yield return Approach(tile.transform.position, 0.6f);
                            tile.Interact(null);
                            yield return Wait(1.5f);
                            yield return Evidence("walt_letter", Take);
                        }
                        dir.SetClock(332.9f);
                        yield return Wait(1.2f);
                        yield return ReadReadable("printout");
                        // Priya only reaches out if you've helped her; otherwise the copier prints a test page.
                        if (HandsOut) Check(dir.HasSecret("priya_ally"), "Priya's 3:33 AM printout");
                        else Check(!dir.HasSecret("priya_ally"), "without your help, the 3:33 AM copier prints only a test page");
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
                        yield return Evidence("payment_ledger", Take);
                        var phone = ctx.Furniture.Named["phone_reception"].GetComponent<ScriptedUse>();
                        yield return Inspect(() => phone.Interact(null), InspectChoice.Close);
                        yield return ReadReadable("dana_doubt");
                        if (Hoards) yield return StickyNote("auditor");
                        else
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
                        var folderChoice = route switch { "cleanbooks" => InspectChoice.Toss, "spotless" => InspectChoice.Close, _ => InspectChoice.Keep };
                        InspectView.AutoChoice = folderChoice;
                        yield return Wait(0.8f);
                        var folder = Story.State.FateOf("red_folder");
                        switch (route)
                        {
                            case "cleanbooks": Check(folder == Fate.Trashed, "the red folder can be thrown away"); break;
                            case "spotless": Check(folder == Fate.Seen, "the red folder can be read and left"); break;
                            default: Check(folder == Fate.Kept, "the red folder can be pulled from the jam"); break;
                        }
                        yield return Evidence("flight_note", InspectChoice.Close);
                        if (Hoards) yield return Deliver("red_folder", "marian");
                        if (route == "audit")
                        {
                            yield return Deliver("red_folder", "auditor");
                            Check(Story.State.IsDelivered("red_folder", "auditor"), "the red folder reaches the auditor");
                        }
                        if (route == "spotless") Check(!Endings.Meddled(Story.State), "nothing has been kept, moved or binned");
                    }
                    break;
            }
        }

        // ---- virtual gamepad ------------------------------------------------------------------
        // A software Gamepad device fed real state events, so menus, prompts and choices are
        // exercised through the same Input System path a controller uses.

        Gamepad vpad;
        protected virtual bool PadChecks => true;

        IEnumerator Press(GamepadButton b)
        {
            InputSystem.QueueStateEvent(vpad, new GamepadState().WithButton(b));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(vpad, new GamepadState());
            yield return null; yield return null;
        }

        static string Selected => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            ? EventSystem.current.currentSelectedGameObject.name : "(none)";

        IEnumerator PadTitle()
        {
            vpad = InputSystem.AddDevice<Gamepad>("AutoPilotPad");
            yield return Press(GamepadButton.DpadDown);
            yield return Wait(0.3f);
            Check(GameInput.UsingPad, "touching the gamepad switches prompts to pad buttons");
            Check(Selected.StartsWith("Btn_"), $"the title menu gets a pad selection ({Selected})");
            for (int i = 0; i < 6 && Selected != "Btn_Settings"; i++) { yield return Press(GamepadButton.DpadDown); yield return Wait(0.15f); }
            Check(Selected == "Btn_Settings", "the d-pad walks the title menu to Settings");
            yield return Press(GamepadButton.South);
            yield return Wait(0.5f);
            Check(SettingsPanel.IsOpen, "pad A opens Settings");
            Check(Selected.StartsWith("Slider_"), $"Settings selects its first row for the pad ({Selected})");
            float before = Settings.Current.MouseSensitivity;
            yield return Press(GamepadButton.DpadRight);
            yield return Wait(0.2f);
            Check(Settings.Current.MouseSensitivity > before + 1e-3f, "d-pad right nudges the selected slider");
            yield return Press(GamepadButton.DpadLeft);
            yield return Wait(0.3f);
            yield return Shot("settings");
            yield return Press(GamepadButton.East);
            yield return Wait(0.4f);
            Check(!SettingsPanel.IsOpen, "pad B closes Settings");
            yield return Shot("pad_title");
            NightSelect.Show();
            yield return Wait(0.6f);
            Check(NightSelect.IsOpen && Selected.StartsWith("Night"), $"Night Select opens with a pad selection ({Selected})");
            yield return Shot("night_select");
            yield return Press(GamepadButton.East);
            yield return Wait(0.4f);
            Check(!NightSelect.IsOpen, "pad B closes Night Select");
            // With a save on disk, New Game asks first; the question must sit above the title.
            var newGame = GameObject.Find("Btn_New Game")?.GetComponent<UnityEngine.UI.Button>();
            if (newGame != null && StoryState.Load() != null)
            {
                newGame.onClick.Invoke();
                yield return Wait(0.5f);
                Check(ChoiceMenu.IsOpen, "New Game asks before overwriting a save");
                yield return Shot("new_game_confirm");
                yield return Press(GamepadButton.East);
                yield return Wait(0.4f);
                Check(!ChoiceMenu.IsOpen && TitleScreen.Instance != null, "pad B backs out of the New Game question");
            }
        }

        IEnumerator PadEvidenceKeep(string doc)
        {
            var ev = root.Director.Ctx.Get(doc)?.GetComponent<EvidenceItem>();
            Check(ev != null, $"evidence {doc} is in the office");
            if (ev == null) yield break;
            yield return Approach(ev.transform.position);
            ev.Interact(null);
            float t = 0;
            while (!InspectView.IsOpen && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.5f);
            yield return Shot("pad_inspect");
            yield return Press(GamepadButton.North);
            yield return Wait(0.5f);
            Check(Story.State.FateOf(doc) == Fate.Kept, $"pad Y keeps {doc}");
        }

        IEnumerator PadDeliver(string doc, string person)
        {
            var tray = root.Director.Furniture.Trays[person];
            int index = Story.State.Inventory.IndexOf(doc);
            yield return Approach(tray.transform.position);
            tray.Interact(null);
            float t = 0;
            while (!ChoiceMenu.IsOpen && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.4f);
            for (int i = 0; i < index; i++) { yield return Press(GamepadButton.DpadDown); yield return Wait(0.1f); }
            yield return Shot("pad_choice");
            yield return Press(GamepadButton.South);
            yield return Wait(0.5f);
            Check(Story.State.IsDelivered(doc, person), $"d-pad and A deliver {doc} to {person}");
        }

        void UnplugPad()
        {
            if (vpad == null) return;
            InputSystem.RemoveDevice(vpad);
            vpad = null;
            GameInput.UsingPad = false;
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

        bool auditorTrayShot;

        /// <summary>
        /// Small props shouldn't sit inside each other (a legal pad under a keyboard, a mug in a
        /// phone). Every pair of solid colliders under 0.8 m is tested for real penetration.
        /// </summary>
        void CheckOverlaps(int n)
        {
            Physics.SyncTransforms();
            var cols = FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && c.gameObject.layer != Layers.Player
                            && c.bounds.size.x < 0.8f && c.bounds.size.y < 0.8f && c.bounds.size.z < 0.8f)
                .ToList();
            int bad = 0;
            for (int i = 0; i < cols.Count; i++)
                for (int j = i + 1; j < cols.Count; j++)
                {
                    var a = cols[i]; var b = cols[j];
                    if (a.attachedRigidbody != null && a.attachedRigidbody == b.attachedRigidbody) continue;
                    if (a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform)) continue;
                    // Bins are built from overlapping wall colliders and are meant to hold things.
                    if (InBin(a.transform) || InBin(b.transform)) continue;
                    // Trays hold papers; sticky notes are stuck flat onto things.
                    if (Path(a.transform).Contains("tray_") || Path(b.transform).Contains("tray_")) continue;
                    if (a.name.Contains("sticky") || b.name.Contains("sticky") || a.name.Contains("thanks") || b.name.Contains("thanks")) continue;
                    if (!a.bounds.Intersects(b.bounds)) continue;
                    if (!Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out float d)) continue;
                    if (d < 0.01f) continue;
                    bad++;
                    Log($"overlap night {n}: {Path(a.transform)} {a.transform.position} <-> {Path(b.transform)} {b.transform.position} by {d * 100f:F1} cm");
                }
            Check(bad == 0, $"night {n}: no props sit inside each other ({bad} overlaps)");

            // Small props sunk into big furniture, walls or floors.
            var big = FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && c.gameObject.layer != Layers.Player
                            && c.gameObject.layer != Layers.Grime // dirt overlays are thin hit volumes, not furniture
                            && (c.bounds.size.x >= 0.8f || c.bounds.size.y >= 0.8f || c.bounds.size.z >= 0.8f))
                .ToList();
            int sunk = 0;
            foreach (var a in cols)
            {
                if (InBin(a.transform) || Path(a.transform).Contains("tray_") || a.name.Contains("sticky") || a.name.Contains("thanks")) continue;
                foreach (var b in big)
                {
                    if (a.attachedRigidbody != null && a.attachedRigidbody == b.attachedRigidbody) continue;
                    if (a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform)) continue;
                    if (!a.bounds.Intersects(b.bounds)) continue;
                    if (b.name.Contains("printer")) continue; // printouts sit in its output tray, inside the box collider
                    if (!Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out float d)) continue;
                    if (d < 0.015f) continue;
                    sunk++;
                    Log($"sunk night {n}: {Path(a.transform)} {a.transform.position} into {Path(b.transform)} by {d * 100f:F1} cm");
                }
            }
            Check(sunk == 0, $"night {n}: no props are sunk into furniture or walls ({sunk})");

            // Loose props should rest on something.
            int floating = 0;
            foreach (var h in FindObjectsByType<Holdable>(FindObjectsSortMode.None))
            {
                if (!h.gameObject.activeInHierarchy || h.Held || InBin(h.transform)) continue;
                var c = h.GetComponentInChildren<Collider>();
                if (c == null || !c.enabled) continue;
                var bottom = new Vector3(c.bounds.center.x, c.bounds.min.y + 0.005f, c.bounds.center.z);
                bool rests = Physics.Raycast(bottom, Vector3.down, out var hit, 0.04f, ~0, QueryTriggerInteraction.Ignore)
                             || Physics.CheckBox(c.bounds.center - Vector3.up * 0.02f, c.bounds.extents, Quaternion.identity, ~(1 << Layers.Player), QueryTriggerInteraction.Ignore)
                                && Physics.OverlapBox(c.bounds.center - Vector3.up * 0.02f, c.bounds.extents, Quaternion.identity, ~(1 << Layers.Player), QueryTriggerInteraction.Ignore).Any(o => o != c && !o.transform.IsChildOf(h.transform));
                if (rests) continue;
                floating++;
                Log($"floating night {n}: {Path(h.transform)} {h.transform.position}");
            }
            Check(floating == 0, $"night {n}: loose props rest on something ({floating} floating)");
        }

        static bool InBin(Transform t) { for (; t != null; t = t.parent) if (t.name.StartsWith("Bin_")) return true; return false; }

        static string Path(Transform t) => t.parent != null && t.parent.parent != null ? t.parent.name + "/" + t.name : t.name;

        IEnumerator ShredAt(string doc, string shredder)
        {
            var sh = root.Director.Furniture.Shredders[shredder];
            int index = Story.State.Inventory.IndexOf(doc);
            Check(index >= 0, $"{doc} is in your pocket to shred");
            if (index < 0) yield break;
            yield return Approach(sh.transform.position + Vector3.up * 0.6f);
            sh.Interact(null);
            float t = 0;
            while (!ChoiceMenu.IsOpen && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(MenuTime);
            ChoiceMenu.AutoPick = index;
            yield return Wait(1.6f);
            Check(Story.State.FateOf(doc) == Fate.Shredded, $"the {shredder} shredder destroys {doc}");
        }

        /// <summary>Tray → "Write a sticky note…" → first phrase: the nested menu must open and work.</summary>
        IEnumerator StickyNote(string person)
        {
            var tray = root.Director.Furniture.Trays[person];
            int notes = Story.State.Notes.Count;
            Check(Story.State.Phrases.Count > 0, "you have leads to write on a sticky note");
            yield return Approach(tray.transform.position);
            tray.Interact(null);
            float t = 0;
            while (!ChoiceMenu.IsOpen && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(MenuTime);
            ChoiceMenu.AutoPick = Story.State.Inventory.Count; // the row after the inventory
            yield return Wait(0.5f);
            Check(ChoiceMenu.IsOpen, "the sticky-note menu opens from the tray menu");
            yield return Shot("sticky_note_menu");
            ChoiceMenu.AutoPick = 0;
            yield return Wait(0.5f);
            Check(Story.State.Notes.Count == notes + 1 && Story.State.Notes[^1].To == person, $"a sticky note is left for {person}");
            Check(!ChoiceMenu.IsOpen && !root.Blocked, "after the note, the player can move again");
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
            if (person == "auditor" && !auditorTrayShot) { auditorTrayShot = true; yield return Wait(0.3f); yield return Shot("auditor_tray"); }
            ChoiceMenu.AutoPick = index;
            yield return Wait(0.5f);
            Check(Story.State.IsDelivered(doc, person), $"{doc} delivered to {person}");
        }
    }
}
