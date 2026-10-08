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
            if (sampling && Time.captureFramerate == 0) frameTimes.Add(GameTime.UnscaledDelta * 1000f);
        }

        // The line under the watch, sampled while a night's route is played: it must name the room
        // the player has stood in for at least one sample (a teleport mid-frame isn't a mistake).
        readonly System.Collections.Generic.HashSet<string> roomsNamed = new(), roomsAll = new();
        int roomSamples, roomWrong;
        string roomPrev, roomWrongNote;
        float roomSampleT;

        void LateUpdate()
        {
            if (!sampling || Hud.Instance == null || root == null || root.Director == null || !root.Director.Running) return;
            if ((roomSampleT -= GameTime.UnscaledDelta) > 0f) return;
            roomSampleT = 0.25f;
            var here = Room.At(root.Player.transform.position + Vector3.up * 0.2f);
            string id = here != null ? here.Id : null;
            if (id != null && id == roomPrev)
            {
                roomSamples++;
                roomsNamed.Add(id);
                string want = here.DisplayName.ToUpperInvariant();
                if (Hud.Instance.RoomShown != id || Hud.Instance.RoomLine.text != want)
                {
                    roomWrong++;
                    roomWrongNote ??= $"in {id} it read \"{Hud.Instance.RoomLine.text}\"";
                }
            }
            roomPrev = id;
        }

        void LogFrameStats(int n)
        {
            if (frameTimes.Count < 30) return;
            var sorted = frameTimes.OrderBy(x => x).ToList();
            float P(float q) => sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
            Debug.Log($"[Perf] night {n}: {sorted.Count} frames, avg {1000f / sorted.Average():F0} fps, " +
                      $"median {P(0.5f):F1} ms, p95 {P(0.95f):F1} ms, p99 {P(0.99f):F1} ms, worst {sorted[^1]:F0} ms");
        }

        protected void Check(bool ok, string what)
        {
            if (ok) passes++; else fails++;
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {what}  (t={Time.time:F1})");
        }

        protected override IEnumerator Run()
        {
            route = GameRoot.Arg("-ahRoute") ?? "audit";
            Debug.Log($"[AutoPilot] started, route {route}");
            // The playtest log stays off through the title (nothing may be written), then records the run.
            bool logWasOn = Settings.Current.PlaytestLog;
            Settings.Current.PlaytestLog = false;
            // Test windows usually sit behind others, and Wayland throttles hidden windows' vsync
            // to a crawl (11-20 fps here). Run uncapped so timings measure the game, not the compositor.
            if (Time.captureFramerate == 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            yield return Wait(3f);
            Check(TitleScreen.Instance != null, "title screen shows on boot");
            yield return Shot("title");
            if (PadChecks) yield return PadTitle();
            bool padWas = GameInput.UsingPad; // the keyboard checks leave the keyboard as the last device touched
            if (PadChecks) yield return KeyboardTitleChecks();
            StoryState.DeleteAll();
            Story.State = new StoryState();
            if (PadChecks) Check(PlaytestLog.CurrentFile == null, $"with the playtest log off nothing is written (it was {(logWasOn ? "on" : "off")} in this profile's settings)");
            Settings.Current.PlaytestLog = true;
            if (PadChecks)
            {
                // Enter with nothing selected starts the night, as it always has.
                EventSystem.current?.SetSelectedGameObject(null);
                yield return TitleKey(UnityEngine.InputSystem.Key.Enter);
                yield return Wait(1f);
                Check(TitleScreen.Instance == null, "Enter on the title with nothing selected starts the night");
                if (TitleScreen.Instance != null) TitleScreen.Instance.Begin(1);
                if (titleKb != null) { InputSystem.RemoveDevice(titleKb); titleKb = null; }
                GameInput.UsingPad = padWas; // later checks expect the pad they last touched
            }
            else TitleScreen.Instance?.Begin(1);
            int last = scenario.StartsWith("night") && int.TryParse(scenario.Substring(5), out var only) ? only : NightDefs.Count;
            for (int n = 1; n <= last; n++)
            {
                yield return PlayNight(n);
            }
            var unnamed = NightDirector.AllRooms.Where(r => !roomsAll.Contains(r)).ToList();
            if (last == NightDefs.Count) Check(unnamed.Count == 0, $"over the week the line under the watch named every room ({string.Join(", ", roomsAll)}){(unnamed.Count > 0 ? "; never " + string.Join(", ", unnamed) : "")}");
            if (last < NightDefs.Count)
            {
                Debug.Log($"[AutoPilot] done: {passes} passed, {fails} failed (stopped after night {last})");
                Application.Quit();
                yield break;
            }
            // Ending
            float t = 0;
            while (FindAnyObjectByType<EndingScreen>() == null && t < 20f) { t += GameTime.UnscaledDelta; yield return null; }
            Check(FindAnyObjectByType<EndingScreen>() != null, "ending screen appears after night 7");
            yield return Wait(EndingTime);
            yield return Shot("ending");
            if (PadChecks) EndingTextChecks();
            string expected = route == "marian" ? "cleanbooks" : route;
            if (route == "marian")
            {
                var ending = Endings.Resolve(Story.State.Clone());
                Check(ending.Lines.Any(l => l.Contains("fifty dollars")), "taking Marian's money shows up in the epilogue");
            }
            Check(Story.State.Ending == expected, $"{route} route reaches the {expected} ending (got {Story.State.Ending}, score {Endings.AuditScore(Story.State)})");
            for (int i = 0; i < 8; i++)
            {
                Interstitial.AutoAdvance = true;
                yield return Wait(0.4f);
                // What the ending showed once every line and the stats were in (read again from the title later).
                var es = FindAnyObjectByType<EndingScreen>();
                if (es && es.AllShown) endingSeen = System.Text.RegularExpressions.Regex.Replace(es.Shown, "<[^>]+>", "");
            }
            yield return Wait(2f);
            Check(TitleScreen.Instance != null, "ending returns to the title");
            yield return Shot("back_to_title");
            if (PadChecks)
            {
                // The title was built at Largest; it follows the change back to Normal.
                var quitBtn = (RectTransform)TitleScreen.Instance.transform.Find("Menu/Btn_Quit");
                float before = quitBtn ? ScreenRect(quitBtn).height * 1080f / Screen.height : 0f;
                Settings.Current.TextSize = 0;
                yield return Wait(0.3f);
                float after = quitBtn ? ScreenRect(quitBtn).height * 1080f / Screen.height : 0f;
                Check(Mathf.Abs(before - 96f) < 2f && Mathf.Abs(after - 64f) < 2f, $"the title follows a change of text size ({before:F0} → {after:F0} units a button)");
            }
            int secretsFound = Story.State.Results.Sum(r => r.Secrets);
            if (PadChecks) yield return TitleCaseFileChecks(true, null);
            if (PadChecks) yield return RecordsChecks(expected);
            if (PadChecks) PlaytestChecks(expected, secretsFound);
            Debug.Log($"[AutoPilot] done: {passes} passed, {fails} failed");
            yield return Wait(0.5f);
            Application.Quit();
        }

        string endingSeen;

        [Serializable] class LogLine { public float t; public int night; public float nt; public string type; public string id; public string grade; }

        /// <summary>The playtest log of this run: valid lines, every night started and finished, the ending.</summary>
        void PlaytestChecks(string ending, int secretsFound)
        {
            var file = PlaytestLog.CurrentFile;
            Check(file != null && System.IO.File.Exists(file), $"the playtest log was written ({file})");
            if (file == null || !System.IO.File.Exists(file)) return;
            var lines = System.IO.File.ReadAllLines(file);
            var parsed = lines.Select(l => { try { return JsonUtility.FromJson<LogLine>(l); } catch { return null; } }).ToList();
            Check(parsed.All(p => p != null && !string.IsNullOrEmpty(p.type)), $"every playtest log line is JSON with a type ({lines.Length} lines)");
            int Count(string type) => parsed.Count(p => p?.type == type);
            var ends = parsed.Where(p => p?.type == "night_end").Select(p => p.night).ToList();
            Check(string.Join(",", ends) == "1,2,3,4,5,6,7", $"the log has a night_end for each night in order ({string.Join(",", ends)})");
            Check(Count("night_start") >= 7 && Count("task_done") >= 7 * 5 && Count("surface_done") > 0, $"the log has night starts ({Count("night_start")}), ticked tasks ({Count("task_done")}) and cleaned surfaces ({Count("surface_done")})");
            Check(Count("secret") == secretsFound, $"the log has every secret found ({Count("secret")} of {secretsFound})");
            Check(parsed.Any(p => p?.type == "ending" && p.id == ending) && Count("clipboard") > 0 && Count("pause") > 0 && Count("stuck_glint") > 0 && Count("item_recovered") > 0,
                $"the log has the ending, clipboard opens ({Count("clipboard")}), pauses ({Count("pause")}), stuck glints ({Count("stuck_glint")}) and recovered items ({Count("item_recovered")})");
        }

        /// <summary>Records outlive Night Select replays: the ending and every night's best stay listed.</summary>
        IEnumerator RecordsChecks(string ending)
        {
            var rec = Records.Current;
            Check(rec.HasEnding(ending), $"the {ending} ending is recorded (endings in this profile: {string.Join(", ", rec.Endings)})");
            Check(Enumerable.Range(1, NightDefs.Count).All(n => rec.Best(n) != null), "every night has a best result on record");
            // Cards are looked up inside Night Select: the running night's root is also called "Night<n>".
            Transform Card(int n) => FindAnyObjectByType<NightSelect>()?.transform.Find("Night" + n);
            string StatsOf(int n) => Card(n)?.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t => t.text).FirstOrDefault(t => t.Contains("secrets"));
            NightSelect.Show();
            yield return Wait(0.8f);
            var card = FindAnyObjectByType<NightSelect>()?.transform.Find("Endings")?.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text ?? "";
            Check(card.Contains($"{rec.Endings.Count} / {Endings.Ids.Length}"), $"Night Select counts the endings found ({rec.Endings.Count} / {Endings.Ids.Length})");
            Check(StatsOf(7) != null, $"Night Select shows night 7's best ({StatsOf(7)})");
            yield return Shot("night_select_records");
            yield return NightSelectTextChecks();
            // Replay night 2: the live save rolls back to that night's start...
            Card(2)?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            yield return WaitUnblocked(40f);
            Check(root.Director.Def?.Number == 2 && Story.State.ResultFor(7) == null, "replaying night 2 rolls the save back to its start");
            root.ToTitle();
            yield return Wait(1.5f);
            // ...but the records don't.
            NightSelect.Show();
            yield return Wait(0.8f);
            Check(StatsOf(7) != null && Records.Current.HasEnding(ending), "after a replay, Night Select still shows night 7's best and the ending");
            yield return Shot("night_select_after_replay");
            FindAnyObjectByType<NightSelect>()?.SendMessage("Close");
            yield return Wait(0.4f);
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
        /// <summary>How long to watch the ending before clicking through it.</summary>
        protected virtual float EndingTime => 8f;
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
                yield return Wait(1.0f); // past the moment the "shift sheet is on the clipboard" caption is due
                Check(!Hud.Instance.CaptionShowing, "no caption shows behind the open clipboard");
                Clipboard.Instance.Close();
                yield return Wait(0.4f);
                Hud.Instance.Caption("[A caption that's still up when the game is paused]", 4f);
                yield return Wait(0.4f);
                PauseMenu.Show();
                yield return Wait(0.6f);
                Check(PauseMenu.IsOpen, "the pause menu opens during a night");
                Check(!Hud.Instance.CaptionShowing, "a caption doesn't stay on screen under the pause menu");
                yield return Shot("pause");
                yield return PauseCardChecks();
                yield return PauseTextChecks();
                yield return SettingsLargeChecks();
                GameObject.Find("Btn_Restart this night")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
                yield return Wait(0.5f);
                Check(ChoiceMenu.IsOpen, "Restart this night asks first");
                yield return Shot("restart_confirm");
                ChoiceMenu.AutoPick = 99; // "Never mind" (clamped to the last option)
                yield return Wait(0.4f);
                yield return BrightnessChecks();
                yield return QuitChecks();
                if (PauseMenu.IsOpen) PauseMenu.Instance.Close();
                yield return WaitUnblocked(3f);
                Check(!root.Blocked, "closing the pause menu hands control back");
                yield return TextSizeChecks();
                yield return ReadingTextChecks();
                yield return CloseAskChecks();
            }
            if (n > 1 && PadChecks)
            {
                // Every night's sheet, to check long ones still fit the paper.
                Clipboard.Instance.Show();
                yield return Wait(0.7f);
                yield return Shot($"n{n}_clipboard");
                Clipboard.Instance.Close();
                yield return Wait(0.4f);
                if (n == 2) yield return CaseFileChecks();
                if (n == 2) yield return ChatAgainChecks();
                if (n == 2) yield return ClipboardLargeChecks();
                if (n == 3) // the torch turns up during night 2
                {
                    PauseMenu.Show();
                    yield return Wait(0.5f);
                    string card = PauseMenu.ControlsText();
                    Check(card.Contains("UV torch") && card.Contains($"<b>{Controls.Display(Act.Torch)}</b>"), $"night 3: with the torch, the pause menu's controls list it on {Controls.Display(Act.Torch)}");
                    PauseMenu.Instance.Close();
                    yield return WaitUnblocked(3f);
                    yield return ReplayAskChecks();
                }
                if (n == 2) yield return AutoPauseChecks();
                if (n == 2) yield return SlowFrameChecks();
            }
            float start = Time.realtimeSinceStartup;
            frameTimes.Clear();
            roomsNamed.Clear(); roomSamples = roomWrong = 0; roomPrev = roomWrongNote = null;
            sampling = true;
            foreach (var room in dir.Def.Rooms)
                if (root.Office.Rooms.TryGetValue(room, out var r) && !r.LightsOn && root.Office.Switches.TryGetValue(room, out var sw)) sw.Toggle();

            yield return Route(n);
            yield return Beat("afterRoute", n);
            yield return CompleteTasks(n);

            // The remote-session beat ignores lights going off in the first minute of the night.
            if (n == 1) for (float t = 0; dir.Elapsed < 62f && t < 90f; t += GameTime.UnscaledDelta) yield return null;
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
            roomsAll.UnionWith(roomsNamed);
            Check(roomSamples > 20 && roomWrong == 0 && roomsNamed.Count > 0,
                $"night {n}: the line under the watch names the room you're in ({roomSamples} samples in {roomsNamed.Count} rooms: {string.Join(", ", roomsNamed)}; {roomWrong} wrong{(roomWrongNote != null ? ", first " + roomWrongNote : "")})");

            // Nights 1 and 2 end at the largest text size: the report and the morning chat grow.
            bool large = PadChecks && n <= 2;
            if (large) Settings.Current.TextSize = 2;
            // Night 2's report is also checked with Handwriting set Plain (its task list is handwritten).
            if (large && n == 2) Settings.Current.PlainHandwriting = true;
            // Clock out at the punch clock.
            yield return Beat("clockout", n);
            root.Player.Teleport(new Vector3(16.0f, 0, 2.5f), -90f, 0);
            var clock = FindAnyObjectByType<PunchClock>();
            yield return Approach(clock.transform.position, 0.9f);
            clock.Interact(null);
            yield return Wait(0.6f);
            if (ChoiceMenu.IsOpen) { yield return Wait(MenuTime); ChoiceMenu.AutoPick = 0; }
            float w = 0;
            while (!Interstitial.AnyOpen && w < 10f) { w += GameTime.UnscaledDelta; yield return null; }
            Check(Story.State.ResultFor(n) != null, $"night {n}: clocking out records a result (grade {Story.State.ResultFor(n)?.Grade})");
            yield return Wait(3.2f);
            yield return Shot($"n{n}_report");
            ReportChecks(n);
            if (large) ReportTextChecks(n);
            if (large && n == 2)
            {
                var lines = Ui.Canvas.transform.Find("ShiftReport")?.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Where(l => l.text.Contains("✔") || l.text.Contains("✗") || l.name == "GradeHint").ToList();
                Check(lines != null && lines.Count >= dir.Def.Tasks.Count && lines.All(l => l.font.name.StartsWith("FiraSans-Regular")),
                    $"night 2 with Handwriting Plain: the report's {lines?.Count} handwritten lines are in Fira Sans ({lines?.FirstOrDefault()?.font.name})");
                yield return Shot("n2_report_plain");
            }
            yield return Beat("report", n);
            // The ending is read at the largest text size (its epilogue grows). Night 7 has no
            // morning chat, so the ending is built as soon as the report goes.
            if (PadChecks && n == NightDefs.Count) Settings.Current.TextSize = 2;
            Interstitial.AutoAdvance = true;
            yield return Wait(9f);
            yield return Shot($"n{n}_chat");
            if (large) yield return ChatChecks(n);
            for (int i = 0; i < 40 && FindAnyObjectByType<ChatInterlude>() != null; i++)
            {
                // What the morning showed, for reading it again from the case file the next night.
                var live = FindAnyObjectByType<ChatInterlude>();
                if (live && !live.Review) morningTexts[n] = live.Content.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Where(t => t.name == "Message").Select(t => t.text).ToList();
                Interstitial.AutoAdvance = true;
                yield return Wait(0.25f);
            }
            Interstitial.AutoAdvance = false;
            if (large) Settings.Current.TextSize = 0;
            Settings.Current.PlainHandwriting = false;
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
                    if (PadChecks) yield return ShiftHelperChecks();
                    if (PadChecks) yield return HighlightChecks();
                    if (PadChecks) yield return CameraMotionChecks();
                    if (PadChecks) yield return RemapChecks();
                    if (PadChecks && vpad != null) yield return ToggleChecks();
                    if (PadChecks && vpad != null) yield return PadToolChecks();
                    if (PadChecks && vpad != null) yield return PadRemapChecks();
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
                            while (!InspectView.IsOpen && t < 6f) { t += GameTime.UnscaledDelta; yield return null; }
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
                        while (!InspectView.IsOpen && t < 4f) { t += GameTime.UnscaledDelta; yield return null; }
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
            // Down off the bottom of the left column carries on at the top of the right one.
            for (int i = 0; i < 14 && Selected != "Choice_Graphics quality"; i++) { yield return Press(GamepadButton.DpadDown); yield return Wait(0.12f); }
            Check(Selected == "Choice_Graphics quality", $"the d-pad walks from the left column of Settings into the right ({Selected})");
            int quality = Settings.Current.Quality;
            yield return Press(GamepadButton.DpadRight);
            yield return Wait(0.3f);
            Check(Settings.Current.Quality != quality, $"d-pad right steps the graphics preset ({GraphicsQuality.Names[quality]} → {GraphicsQuality.Names[GraphicsQuality.Level]})");
            yield return Shot("settings_quality");
            yield return Press(GamepadButton.DpadLeft);
            yield return Wait(0.3f);
            Check(Settings.Current.Quality == quality && Selected == "Choice_Graphics quality", "d-pad left steps it back without leaving the row");
            int sent = Rumble.Sent;
            Rumble.Pulse(0.2f, 0.3f, 0.1f);
            Check(Rumble.Sent == sent + 1, "a rumble pulse goes to the (virtual) pad while it's in use");
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

        Keyboard titleKb;

        IEnumerator TitleKey(UnityEngine.InputSystem.Key k)
        {
            titleKb ??= InputSystem.AddDevice<Keyboard>("AutoPilotKeyboardTitle");
            titleKb.MakeCurrent();
            InputSystem.QueueStateEvent(titleKb, new KeyboardState(k));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(titleKb, new KeyboardState());
            yield return Wait(0.3f);
        }

        /// <summary>
        /// Round 10: on the title, the arrow keys walk the menu and Enter presses the button you're
        /// on (Settings, Night Select, New Game with a save on disk) without also starting the night.
        /// </summary>
        IEnumerator KeyboardTitleChecks()
        {
            var title = TitleScreen.Instance;
            if (title == null) { Check(false, "the title is up for the keyboard checks"); yield break; }
            var es = EventSystem.current;
            var first = title.transform.Find("Menu").GetComponentsInChildren<UnityEngine.UI.Selectable>().FirstOrDefault();
            bool hadSave = StoryState.Load() != null;
            foreach (var button in new[] { "Btn_Settings", "Btn_Night Select", "Btn_New Game" })
            {
                if (button == "Btn_New Game" && !hadSave) continue; // with no save it just starts Night 1
                es.SetSelectedGameObject(first.gameObject);
                yield return Wait(0.1f);
                for (int i = 0; i < 8 && Selected != button; i++) yield return TitleKey(UnityEngine.InputSystem.Key.DownArrow);
                bool walked = Selected == button;
                yield return TitleKey(UnityEngine.InputSystem.Key.Enter);
                yield return Wait(0.8f); // past the title's fade, if it had started the night
                bool opened = button switch { "Btn_Settings" => SettingsPanel.IsOpen, "Btn_Night Select" => NightSelect.IsOpen, _ => ChoiceMenu.IsOpen };
                bool stayed = TitleScreen.Instance == title && title.isActiveAndEnabled && root.Director.Paused;
                Check(walked && opened && stayed, $"on the title, the arrow keys reach {button.Substring(4)} and Enter opens it without starting the night (selected {Selected}, opened {opened}, title still up {stayed})");
                if (button == "Btn_Settings") yield return Shot("title_enter_settings");
                // Esc backs out (Never mind, for New Game's question).
                yield return TitleKey(UnityEngine.InputSystem.Key.Escape);
                yield return Wait(0.4f);
                Check(!SettingsPanel.IsOpen && !NightSelect.IsOpen && !ChoiceMenu.IsOpen && TitleScreen.Instance == title, $"Esc closes it again ({button.Substring(4)})");
                if (TitleScreen.Instance != title) yield break;
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
            while (!InspectView.IsOpen && t < 3f) { t += GameTime.UnscaledDelta; yield return null; }
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
            while (!ChoiceMenu.IsOpen && t < 3f) { t += GameTime.UnscaledDelta; yield return null; }
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
                yield return BinLabelChecks(cup);
                yield return HandsFullChecks(cup);
                var anchor = o.Anchor("BIN_trash_reception_1").position;
                var bin = root.Director.Furniture.Bins.OrderBy(b => (b.transform.position - anchor).sqrMagnitude).First();
                var spot = ThrowSpot(bin, 3.6f) ?? ThrowSpot(bin, 2.8f) ?? ThrowSpot(bin, 2.2f);
                Check(spot.HasValue, "a clear throwing spot exists near the reception bin");
                root.Player.Teleport(spot ?? anchor + new Vector3(-2.2f, 0, 0), 0, 0);
                yield return Aim(bin.transform.position + Vector3.up * bin.Height, 0.4f);
                yield return Wait(0.2f);
                Check(root.Hands.AimedBin == bin && HudLabel() == "BLACK BIN  ✓", $"aiming the cup at the black bin, the label says it takes it (\"{HudLabel()}\")");
                // Solve once for the aim at which the cup, settled where the hands hold it at full
                // charge (16 cm right of the camera), lands in the bin. The old check re-solved the
                // pitch every frame from wherever the cup was, ignoring that offset: it often found
                // no solution and threw at whatever pitch the look at the bin had left.
                var camT = root.Player.Camera.transform;
                var aim = SolveHeldAim(cup, camT.position, root.Player.Yaw, bin, 1f);
                Check(aim.HasValue, $"the throw has a solution from this spot (pitch {aim?.pitch.ToString("F1") ?? "none"}°, yaw {aim?.yaw.ToString("F1") ?? "-"}° against {root.Player.Yaw:F1}° looking at the bin)");
                if (aim.HasValue) { root.Player.Pitch = aim.Value.pitch; root.Player.Yaw = aim.Value.yaw; }
                // Hold LMB to full charge (charge clamps at 1, so the throw doesn't depend on frame
                // timing) at that pitch, let the held cup settle, then release.
                input.Use = true;
                for (float t = 0; t < 0.9f; t += Time.deltaTime) yield return null;
                var arcLine = GameObject.Find("ThrowArc")?.GetComponent<LineRenderer>();
                Check(arcLine != null && arcLine.positionCount > 4, $"charging a throw draws the arc preview ({arcLine?.positionCount} points)");
                yield return Shot("n1_throw_arc");
                var expect = Hands.HoldTarget(cup, camT.position, Quaternion.Euler(root.Player.Pitch, root.Player.Yaw, 0), 1f);
                for (float t = 0; t < 1.5f && ((cup.transform.position - expect).magnitude > 0.003f || cup.Body.linearVelocity.sqrMagnitude > 1e-4f); t += Time.deltaTime)
                    yield return null;
                float settle = (cup.transform.position - expect).magnitude;
                input.Use = false;
                float solved = root.Player.Pitch;
                yield return null;
                Log($"throw: pitch {solved:F1} from {cup.transform.position} (expected {expect}, {settle * 100f:F1} cm off) v={cup.Body.linearVelocity} bin {bin.transform.position} h={bin.Height}, {1f / Mathf.Max(1e-4f, Time.smoothDeltaTime):F0} fps");
                Check(settle < 0.02f, $"the held cup settles where the solve expected it ({settle * 100f:F1} cm off)");
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

        static string HudLabel() => System.Text.RegularExpressions.Regex.Replace(Hud.Instance.TargetLabel.text, "<[^>]+>", "");

        /// <summary>
        /// With rubbish in hand the label under the reticle names the bin it goes in, and a bin
        /// under the reticle says whether it takes it (round 8). Ends where it started.
        /// </summary>
        IEnumerator BinLabelChecks(TrashItem item)
        {
            var player = root.Player;
            var (pos, yaw, pitch) = (player.transform.position, player.Yaw, player.Pitch);
            player.Pitch = -80f; // at the ceiling: no bin
            yield return Wait(0.5f);
            string where = item.Kind == TrashKind.Recyclable ? "BLUE RECYCLING" : item.Kind == TrashKind.General ? "BLACK BIN" : "EITHER BIN";
            Check(root.Hands.AimedBin == null && HudLabel() == $"{item.DisplayName.ToUpperInvariant()}  ·  {where}" && Hud.Instance.TargetLabel.alpha > 0.5f,
                $"holding the {item.DisplayName}, the label under the reticle says which bin it goes in (\"{HudLabel()}\")");
            player.Pitch = 10f;
            yield return Wait(0.4f);
            yield return Shot("n1_bin_label_held");
            var wrong = root.Director.Furniture.Bins.Where(b => !b.Accepts(item.Kind)).OrderBy(b => (b.transform.position - pos).sqrMagnitude).FirstOrDefault();
            var spot = wrong != null ? ThrowSpot(wrong, 1.8f) ?? ThrowSpot(wrong, 2.4f) : null;
            Check(spot.HasValue, "a spot in view of a bin that doesn't take it");
            if (spot.HasValue)
            {
                player.Teleport(spot.Value, 0, 0);
                yield return Aim(wrong.transform.position + Vector3.up * wrong.Height * 0.6f, 0.4f);
                yield return Wait(0.3f);
                string expect = item.Kind == TrashKind.Recyclable ? "BLACK BIN  ✗  ·  THIS ONE GOES IN BLUE" : "BLUE RECYCLING  ✗  ·  THIS ONE GOES IN BLACK";
                Check(root.Hands.AimedBin == wrong && HudLabel() == expect, $"aiming at the wrong bin, the label says so and which one it goes in (\"{HudLabel()}\")");
                yield return Shot("n1_bin_label_wrong");
            }
            player.Teleport(pos, yaw, pitch);
            yield return Wait(0.3f);
            Check(root.Hands.Holding == item, "still holding it afterwards");
        }

        /// <summary>
        /// With something in hand, E still uses a light switch, a door or a chair, and the cup stays
        /// held (round 9); E on the floor offers Place, and with nothing to stand it on the prompt
        /// offers only Q. Pressed on a virtual keyboard. Ends where it started, still holding it.
        /// </summary>
        /// <summary>
        /// Contrast of the label under the reticle against what's behind it, from a screen capture:
        /// its brightest letters (98th percentile of relative luminance in the text's bounds)
        /// against the middle of that area (mostly the gaps between letters). WCAG ratio.
        /// </summary>
        IEnumerator MeasureLabel(Action<float> got)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var r = TextRect(Hud.Instance.TargetLabel);
            var lum = new System.Collections.Generic.List<float>();
            int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin), 0, tex.width - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(r.xMax), 0, tex.width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin), 0, tex.height - 1), y1 = Mathf.Clamp(Mathf.CeilToInt(r.yMax), 0, tex.height - 1);
            var px = tex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            foreach (var c in px)
                lum.Add(0.2126f * Mathf.GammaToLinearSpace(c.r) + 0.7152f * Mathf.GammaToLinearSpace(c.g) + 0.0722f * Mathf.GammaToLinearSpace(c.b));
            Destroy(tex);
            lum.Sort();
            float P(float q) => lum.Count == 0 ? 0f : lum[Mathf.Clamp(Mathf.RoundToInt(q * (lum.Count - 1)), 0, lum.Count - 1)];
            got(lum.Count < 50 ? 0f : (P(0.98f) + 0.05f) / (P(0.5f) + 0.05f));
        }

        /// <summary>Round 10: the label reads at least 4.5:1 here; measured again without its backing and shadow (as before).</summary>
        IEnumerator LabelContrastChecks(string where)
        {
            var label = Hud.Instance.TargetLabel;
            var plate = Ui.Canvas.transform.Find("HUD/TargetLabelPlate")?.GetComponent<UnityEngine.UI.Image>();
            float with = 0f, without = 0f;
            yield return MeasureLabel(v => with = v);
            if (plate) plate.enabled = false;
            label.fontMaterial.DisableKeyword("UNDERLAY_ON");
            yield return null; yield return null;
            yield return MeasureLabel(v => without = v);
            yield return Shot("n1_label_without_backing");
            if (plate) plate.enabled = true;
            label.fontMaterial.EnableKeyword("UNDERLAY_ON");
            yield return null;
            Check(plate != null && with >= 4.5f, $"the label under the reticle (\"{HudLabel()}\") reads at {with:F1}:1 against {where} (at least 4.5:1; {without:F1}:1 without its backing and shadow, as before round 10)");
        }

        IEnumerator HandsFullChecks(Holdable item)
        {
            var player = root.Player;
            var (pos, yaw, pitch) = (player.transform.position, player.Yaw, player.Pitch);
            var hl = AimHighlight.Instance;
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotKeyboardHands");
            vkb.MakeCurrent();
            IEnumerator Key(UnityEngine.InputSystem.Key k)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(k));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return Wait(0.4f);
            }
            string Prompt() => Hud.Instance.PromptSignature ?? "";
            var scripted = GameInput.Override;
            GameInput.Override = null;

            // The reception light switch.
            var room = root.Office.Rooms["reception"];
            var sw = root.Office.Switches["reception"];
            var toRoom = room.Bounds.center - sw.transform.position; toRoom.y = 0;
            var stand = sw.transform.position + toRoom.normalized * 0.9f; stand.y = 0;
            player.Teleport(stand, 0, 0);
            yield return Aim(sw.transform.position, 0.3f);
            yield return Wait(0.3f);
            bool lit = room.LightsOn;
            string want = lit ? "Lights off" : "Lights on";
            Check(Interactor.Instance.Focus == (IInteractable)sw && Interactor.Instance.HandsFreeFocus && hl != null && hl.Target == sw.transform,
                $"holding the {item.DisplayName.ToLowerInvariant()}, the switch under the reticle is found and highlighted (focus {(Interactor.Instance.Focus as Component)?.name ?? "none"})");
            Check(Prompt().Contains("|E" + want + "|"), $"the prompt offers E: {want} as well as the throw and Q (\"{Prompt()}\")");
            yield return Shot("n1_hands_full_switch");
            yield return LabelContrastChecks("the reception light switch, lights " + (room.LightsOn ? "on" : "off"));
            yield return Key(UnityEngine.InputSystem.Key.E);
            Check(room.LightsOn != lit && root.Hands.Holding == item, $"E flips the switch with the cup in hand, and the cup stays held (lights {(room.LightsOn ? "on" : "off")}, holding {root.Hands.Holding?.name ?? "nothing"})");
            yield return Key(UnityEngine.InputSystem.Key.E);
            Check(room.LightsOn == lit && root.Hands.Holding == item, "E again flips it back, still holding");

            // The nearest door that opens.
            var door = root.Office.Doors.Values.Where(d => d != null && !d.Locked && d.gameObject.activeInHierarchy)
                .OrderBy(d => (d.transform.position - pos).sqrMagnitude).FirstOrDefault();
            Check(door != null, "a door to try");
            if (door != null)
            {
                var col = door.GetComponentInChildren<Collider>();
                var mid = col != null ? col.bounds.center : door.transform.position + Vector3.up;
                var away = pos - mid; away.y = 0;
                // Stand square in front of it (across its thin side), on the side the cup came from.
                var size = col != null ? col.bounds.size : Vector3.one;
                var normal = size.x < size.z ? Vector3.right : Vector3.forward;
                if (Vector3.Dot(normal, away) < 0) normal = -normal;
                var at = mid + normal * 1.3f; at.y = 0;
                player.Teleport(at, 0, 0);
                yield return Aim(new Vector3(mid.x, 1.1f, mid.z), 0.3f);
                yield return Wait(0.3f);
                bool open = door.IsOpen;
                Check(Interactor.Instance.Focus == (IInteractable)door && Prompt().Contains("|E" + (open ? "Close" : "Open") + "|"),
                    $"holding it, a door under the reticle offers E: {(open ? "Close" : "Open")} (\"{Prompt()}\", focus {(Interactor.Instance.Focus as Component)?.name ?? "none"})");
                yield return Key(UnityEngine.InputSystem.Key.E);
                Check(door.IsOpen != open && root.Hands.Holding == item, $"E {(open ? "closes" : "opens")} the door ({door.Id}) and the cup stays held");
                yield return Wait(0.8f); // the swing
                yield return Shot("n1_hands_full_door");
                // Look at where the door is now, as a player would, and close it again.
                yield return Aim(col != null ? col.bounds.center : mid, 0.3f);
                yield return Wait(0.3f);
                Check(Interactor.Instance.Focus == (IInteractable)door && Prompt().Contains("|E" + (open ? "Open" : "Close") + "|"),
                    $"aimed at the swung door, E offers to {(open ? "open" : "close")} it again (\"{Prompt()}\")");
                yield return Key(UnityEngine.InputSystem.Key.E);
                Check(door.IsOpen == open && root.Hands.Holding == item, "E again puts the door back, still holding");
                yield return Wait(0.6f);
            }

            // A chair left out from its desk.
            var chair = Chair.All.Where(c => !c.Tucked).OrderBy(c => (c.transform.position - pos).sqrMagnitude).FirstOrDefault();
            Check(chair != null, "a chair that's out from its desk");
            if (chair != null)
            {
                var (p0, r0) = (chair.transform.position, chair.transform.rotation);
                var out_ = p0 - chair.HomePos; out_.y = 0;
                if (out_.sqrMagnitude < 0.01f) out_ = Vector3.back;
                var at = p0 + out_.normalized * 1.0f; at.y = 0;
                player.Teleport(at, 0, 0);
                yield return Aim(p0 + Vector3.up * 0.55f, 0.3f);
                yield return Wait(0.3f);
                Check(Interactor.Instance.Focus == (IInteractable)chair && Prompt().Contains("|ETuck in chair|"),
                    $"holding it, a chair under the reticle offers E: Tuck in chair (\"{Prompt()}\", focus {(Interactor.Instance.Focus as Component)?.name ?? "none"})");
                yield return Key(UnityEngine.InputSystem.Key.E);
                yield return Wait(0.4f);
                Check(chair.Tucked && root.Hands.Holding == item, "E tucks the chair in and the cup stays held");
                chair.Untuck(p0, r0); // as the night left it: the route tucks it in later
            }

            // Nothing to stand it on: only the throw and Q; the floor: Place.
            player.Teleport(pos, yaw, -80f);
            yield return Wait(0.4f);
            Check(!Prompt().Contains("|E") && Prompt().Contains("|QDrop"), $"aimed at the ceiling, E isn't offered (it would only drop it, like Q) (\"{Prompt()}\")");
            player.Pitch = 60f;
            yield return Wait(0.4f);
            Check(Prompt().Contains("|EPlace") || Prompt().Contains("|EPut back"), $"aimed at the floor, E places it (\"{Prompt()}\")");

            GameInput.Override = scripted;
            InputSystem.RemoveDevice(vkb);
            player.Teleport(pos, yaw, pitch);
            yield return Wait(0.3f);
            Check(root.Hands.Holding == item, "still holding it afterwards");
        }

        // ---- running slowly (night 2) -------------------------------------------------------------

        /// <summary>
        /// Running slowly (round 9): at full speed nothing is offered; with frames held to 20 a
        /// second the game offers the next lower preset once its warm-up and window have passed;
        /// lowering applies and saves it; still slow, it offers the next step; Keep means it never
        /// asks again. Both offers go in the playtest log. Puts every setting back afterwards.
        /// </summary>
        IEnumerator SlowFrameChecks()
        {
            var s = Settings.Current;
            var fw = FrameWatch.Instance;
            Check(fw != null && !FrameWatch.Watching, "the frame watch is there, and off in automated runs");
            if (fw == null) yield break;
            var (q0, scale0, vs0, fr0) = (s.Quality, s.RenderScale, QualitySettings.vSyncCount, Application.targetFrameRate);
            var file = System.IO.Path.Combine(StoryState.Dir, "settings.json");
            int offers0 = fw.Offers;
            IEnumerator Play(float seconds, Func<bool> until = null)
            {
                for (float t = 0; t < seconds && (until == null || !until()); t += GameTime.UnscaledDelta) yield return null;
            }
            FrameWatch.Watching = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return Play(SlowFrames.Warmup + SlowFrames.Window + 3f, () => ChoiceMenu.IsOpen);
            Check(!ChoiceMenu.IsOpen && fw.Offers == offers0, $"at full speed nothing is offered ({1f / Mathf.Max(1e-4f, Time.smoothDeltaTime):F0} fps, median {fw.Frames.MedianMs:F1} ms, {Load()})");
            if (ChoiceMenu.IsOpen) { ChoiceMenu.AutoPick = 99; yield return Wait(0.4f); }

            // 20 frames a second: the preset one step down.
            Application.targetFrameRate = 20;
            float waited = 0f;
            yield return Play(SlowFrames.Warmup + SlowFrames.Window + 10f, () => { waited += GameTime.UnscaledDelta; return ChoiceMenu.IsOpen; });
            Check(ChoiceMenu.IsOpen && fw.Offers == offers0 + 1 && fw.OfferedFps >= 15 && fw.OfferedFps <= 22,
                $"at 20 fps the game offers a lower setting after {waited:F0} s, naming the rate on screen ({fw.OfferedFps} fps; median over the window {fw.Frames.MedianMs:F0} ms)");
            yield return Wait(0.5f);
            yield return Shot("n2_running_slowly");
            ChoiceMenu.AutoPick = 0;
            yield return Wait(0.5f);
            Check(s.Quality == q0 - 1 && System.IO.File.ReadAllText(file).Contains($"\"Quality\": {q0 - 1}"),
                $"choosing it lowers the preset to {GraphicsQuality.Names[Mathf.Max(0, q0 - 1)]} and saves it (now {GraphicsQuality.Names[s.Quality]})");
            Check(!root.Blocked, "and play carries on");

            // Still slow: the next step; Keep means never again.
            waited = 0f;
            yield return Play(SlowFrames.Warmup + SlowFrames.Window + 10f, () => { waited += GameTime.UnscaledDelta; return ChoiceMenu.IsOpen; });
            Check(ChoiceMenu.IsOpen && fw.Offers == offers0 + 2, $"still slow, it offers the next step after {waited:F0} s");
            ChoiceMenu.AutoPick = 1;
            yield return Wait(0.5f);
            Check(s.SlowFramesDeclined && s.Quality == q0 - 1 && System.IO.File.ReadAllText(file).Contains("\"SlowFramesDeclined\": true"),
                "Keep leaves the settings as they are and is saved");
            yield return Play(SlowFrames.Warmup + SlowFrames.Window + 3f, () => ChoiceMenu.IsOpen);
            Check(!ChoiceMenu.IsOpen && fw.Offers == offers0 + 2, "after Keep it doesn't ask again, however slow");
            var log = PlaytestLog.CurrentFile != null && System.IO.File.Exists(PlaytestLog.CurrentFile) ? System.IO.File.ReadAllText(PlaytestLog.CurrentFile) : "";
            Check(System.Text.RegularExpressions.Regex.Matches(log, "\"type\":\"slow_frames\"").Count == 2 && log.Contains("\"choice\":\"lower\"") && log.Contains("\"choice\":\"keep\""),
                "the playtest log has both offers and both choices");

            FrameWatch.Watching = false;
            QualitySettings.vSyncCount = vs0;
            Application.targetFrameRate = fr0;
            s.Quality = q0;
            s.RenderScale = scale0;
            s.SlowFramesDeclined = false;
            Settings.ApplyGraphics();
            Settings.Save();
            yield return Wait(0.5f);
        }

        static string Load()
        {
            try { return "load " + System.IO.File.ReadAllText("/proc/loadavg").Split(' ')[0]; } catch { return "load ?"; }
        }

        // ---- pad tool cycling and controller glyphs (night 1) ------------------------------------

        IEnumerator PadToolChecks()
        {
            var cc = root.Cleaning;
            var scripted = GameInput.Override;
            GameInput.Override = null; // read the pad itself
            vpad.MakeCurrent();
            root.Player.Teleport(new Vector3(12.2f, 0, 7.0f), 0f, 35f);
            yield return Wait(0.2f);
            var seen = new System.Collections.Generic.List<ToolKind>();
            for (int i = 0; i < 5; i++) { yield return Press(GamepadButton.DpadRight); yield return Wait(0.15f); seen.Add(cc.Pinned); if (i == 1) yield return Shot("n1_pad_tool_cycle"); }
            Check(string.Join(",", seen) == "Cloth,Vacuum,Squeegee,Mop,None", $"d-pad right steps through the tools and back to automatic ({string.Join(", ", seen)})");
            yield return Press(GamepadButton.DpadLeft);
            yield return Wait(0.15f);
            Check(cc.Pinned == ToolKind.Mop, $"d-pad left steps back ({cc.Pinned})");
            yield return Press(GamepadButton.DpadRight);
            yield return Wait(0.15f);
            Check(GameInput.UsingPad && GameInput.Glyph("E") == "A", $"a generic pad shows Xbox letters (interact: {GameInput.Glyph("E")})");

            // A DualShock 4: PlayStation symbols.
            var ds = InputSystem.AddDevice<UnityEngine.InputSystem.DualShock.DualShock4GamepadHID>("AutoPilotDS4");
            ds.MakeCurrent();
            // UsingPad is already on from the generic pad; the glyphs follow whichever pad is current.
            yield return Wait(0.2f);
            Check(Gamepad.current == ds && GameInput.Glyph("E") == "✕" && GameInput.Glyph("TAB") == "△" && GameInput.Glyph("LMB") == "R2",
                $"a DualShock shows PlayStation symbols (interact {GameInput.Glyph("E")}, keep {GameInput.Glyph("TAB")}, clean {GameInput.Glyph("LMB")})");
            var sw = root.Office.Switches["reception"];
            var room = root.Office.Rooms["reception"];
            var toRoom = room.Bounds.center - sw.transform.position; toRoom.y = 0;
            var stand = sw.transform.position + toRoom.normalized * 0.9f; stand.y = 0;
            root.Player.Teleport(stand, 0, 0);
            yield return Aim(sw.transform.position, 0.3f);
            yield return Wait(0.3f);
            Check(GameObject.Find("Key_✕") != null, "the prompt shows the ✕ button");
            yield return Shot("n1_playstation_prompt");
            InputSystem.RemoveDevice(ds);
            vpad.MakeCurrent();
            GameInput.Override = scripted;
            yield return Wait(0.2f);
        }

        // ---- the case file (start of night 2) ----------------------------------------------------

        IEnumerator CaseFileChecks()
        {
            var cb = Clipboard.Instance;
            var pad = InputSystem.AddDevice<Gamepad>("AutoPilotCasePad");
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotCaseKeyboard");
            IEnumerator PadPress(GamepadButton b)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return Wait(0.35f);
            }
            IEnumerator Key(UnityEngine.InputSystem.Key k)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(k));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return Wait(0.35f);
            }
            pad.MakeCurrent();
            cb.Show();
            yield return Wait(0.5f);
            yield return PadPress(GamepadButton.DpadRight);
            Check(cb.Open && cb.Page == Clipboard.CasePage, "d-pad right turns the clipboard to the case file");
            var night1 = Story.State.Read.Where(r => r.Night == 1).Select(r => r.Id).ToList();
            var expected = new[] { "dana_welcome", "walt_note_1", "theo_note", "screen_theo_email", "screen_remote" };
            Check(expected.All(night1.Contains) && cb.CaseEntries.Where(e => !e.StartsWith(Clipboard.ChatPrefix)).SequenceEqual(Story.State.Read.Select(r => r.Id)),
                $"the case file lists what was read on night 1 ({string.Join(", ", cb.CaseEntries)})");
            string theoFate = Story.State.FateLabel("theo_note", true);
            string wantFate = HandsOut ? "left for Priya" : Keeps ? "kept" : "left where it was";
            var text = GameObject.Find("CaseFile")?.GetComponent<TMPro.TextMeshProUGUI>()?.text ?? "";
            Check(theoFate == wantFate && text.Contains(wantFate), $"Theo's note is listed as {wantFate} ({theoFate})");
            yield return Shot("n2_case_file");

            // Pad A reads the first entry again; nothing in the story changes.
            string fate = JsonUtility.ToJson(Story.State.Evidence);
            int phrases = Story.State.Phrases.Count, secrets = root.Director.SecretsFoundCount, reads = Story.State.Read.Count;
            string first = cb.CaseEntries.FirstOrDefault();
            yield return PadPress(GamepadButton.South);
            yield return Wait(0.3f);
            Check(InspectView.IsOpen && InspectView.CurrentDoc == first, $"pad A reads {first} again ({InspectView.CurrentDoc})");
            string Body() => GameObject.Find("Inspect")?.transform.Find("Card/Body")?.GetComponent<TMPro.TextMeshProUGUI>()?.text ?? "";
            Check(first != "dana_welcome" || Body().Contains("(View)"), $"on a pad, Dana's note names the View button for the clipboard ({(Body().Contains("(View)") ? "View" : "missing")})");
            yield return Shot("n2_case_file_read");
            yield return PadPress(GamepadButton.East);
            Check(!InspectView.IsOpen && cb.Open && cb.Page == Clipboard.CasePage, "pad B closes the document and leaves the case file open");
            Check(JsonUtility.ToJson(Story.State.Evidence) == fate && Story.State.Phrases.Count == phrases && root.Director.SecretsFoundCount == secrets && Story.State.Read.Count == reads,
                "reading again changes no fates, leads, secrets or the list");

            // The keyboard: down, E reads the second entry, Esc closes it, Esc closes the clipboard.
            vkb.MakeCurrent();
            yield return Key(UnityEngine.InputSystem.Key.DownArrow);
            Check(cb.Selected == 1, $"the down arrow moves to the next entry ({cb.Selected})");
            yield return Key(UnityEngine.InputSystem.Key.E);
            yield return Wait(0.3f);
            Check(InspectView.IsOpen && InspectView.CurrentDoc == cb.CaseEntries.ElementAtOrDefault(1), $"E reads it ({InspectView.CurrentDoc})");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            Check(!InspectView.IsOpen && cb.Open, "Esc closes the document but not the clipboard");
            yield return Key(UnityEngine.InputSystem.Key.UpArrow);
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            yield return Wait(0.3f);
            Check(InspectView.CurrentDoc != "dana_welcome" || Body().Contains("(Tab)"), "with the keyboard, Dana's note names the clipboard key (Tab)");
            yield return Shot("n2_case_file_dana_keys");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            yield return Key(UnityEngine.InputSystem.Key.A);
            Check(cb.Page == Clipboard.SheetPage, "A turns back to the shift sheet");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            Check(!cb.Open && !PauseMenu.IsOpen, "Esc then closes the clipboard (and doesn't also pause)");
            InputSystem.RemoveDevice(vkb);
            InputSystem.RemoveDevice(pad);
            GameInput.UsingPad = false;
            yield return WaitUnblocked(3f);
        }

        // ---- pausing on focus loss and pad loss (night 2) -----------------------------------------

        IEnumerator AutoPauseChecks()
        {
            GameRoot.AutoPause = true;
            IEnumerator Unpause()
            {
                PauseMenu.Instance?.Close();
                yield return WaitUnblocked(3f);
            }
            // Losing focus with nothing open pauses (called through the handler: the window
            // manager can't be made to take focus away on cue).
            root.SendMessage("OnApplicationFocus", false);
            yield return Wait(0.3f);
            Check(PauseMenu.IsOpen && Time.timeScale == 0f, "losing window focus mid-night opens the pause menu");
            yield return Shot("n2_focus_pause");
            yield return Unpause();
            // With the clipboard up, it closes and the pause menu opens.
            Clipboard.Instance.Show();
            yield return Wait(0.4f);
            root.SendMessage("OnApplicationFocus", false);
            yield return Wait(0.3f);
            Check(PauseMenu.IsOpen && !Clipboard.Instance.Open, "with the clipboard open, losing focus closes it and pauses");
            yield return Unpause();
            // A document waiting for an answer stays as it is.
            InspectView.Show(Docs.Get("dana_welcome"), InspectMode.Read, null, reread: true);
            yield return Wait(0.4f);
            root.SendMessage("OnApplicationFocus", false);
            yield return Wait(0.3f);
            Check(!PauseMenu.IsOpen && InspectView.IsOpen, "with a document open, losing focus changes nothing");
            InspectView.AutoChoice = InspectChoice.Close;
            yield return WaitUnblocked(3f);
            // The pad in use goes away: pause.
            var pad = InputSystem.AddDevice<Gamepad>("AutoPilotUnplugPad");
            pad.MakeCurrent();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
            yield return Wait(0.2f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Wait(0.2f);
            Check(GameInput.UsingPad, "the pad is in use");
            InputSystem.RemoveDevice(pad);
            yield return Wait(0.3f);
            Check(PauseMenu.IsOpen, "unplugging the pad in use mid-night opens the pause menu");
            GameInput.UsingPad = false;
            yield return Unpause();
            // A pad that isn't being used (keyboard last) can go without a pause.
            var idle = InputSystem.AddDevice<Gamepad>("AutoPilotIdlePad");
            yield return Wait(0.2f);
            GameInput.UsingPad = false;
            InputSystem.RemoveDevice(idle);
            yield return Wait(0.3f);
            Check(!PauseMenu.IsOpen, "unplugging a pad nobody is using doesn't pause");
            GameRoot.AutoPause = false;
            yield return WaitUnblocked(3f);
        }

        // ---- rebinding pad buttons (night 1) ------------------------------------------------------

        IEnumerator PadRemapChecks()
        {
            vpad.MakeCurrent();
            yield return Press(GamepadButton.DpadDown);   // the pad is in use
            UnityEngine.UI.Button Btn(string prefix) => FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name.StartsWith(prefix));
            SettingsPanel.Show();
            yield return Wait(0.4f);
            Btn("Btn_Keyboard")?.onClick.Invoke();
            yield return Wait(0.4f);
            Btn("Tab_Controller")?.onClick.Invoke();
            yield return Wait(0.5f);
            Check(ControlsPanel.IsOpen && ControlsPanel.Page == ControlsPanel.PadPage && GameObject.Find("Bind_Interact") != null, "the controls page has a Controller tab with the pad's actions");
            yield return Shot("controls_pad");
            GameObject.Find("Bind_Interact")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            yield return Wait(0.2f);
            Check(ControlsPanel.Listening == Act.Interact, "picking Interact waits for a pad button");
            yield return Press(GamepadButton.West);
            yield return Wait(0.4f);
            Check(Controls.PadPathOf(Settings.Current, Act.Interact) == "<Gamepad>/buttonWest" && ControlsPanel.Listening == null,
                $"pressing X binds Interact to X ({Controls.PadName(Controls.PadPathOf(Settings.Current, Act.Interact))})");
            yield return Shot("controls_pad_rebound");
            // Start stays fixed: it cancels.
            GameObject.Find("Bind_Drop")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            yield return Wait(0.2f);
            yield return Press(GamepadButton.Start);
            yield return Wait(0.4f);
            Check(ControlsPanel.Listening == null && Controls.PadPathOf(Settings.Current, Act.Drop) == "<Gamepad>/buttonEast", "Start cancels and can't be bound");
            FindAnyObjectByType<ControlsPanel>()?.SendMessage("Close");
            yield return Wait(0.3f);
            FindAnyObjectByType<SettingsPanel>()?.SendMessage("Close");
            yield return WaitUnblocked(3f);
            var file = System.IO.Path.Combine(StoryState.Dir, "settings.json");
            Check(System.IO.File.Exists(file) && System.IO.File.ReadAllText(file).Contains("<Gamepad>/buttonWest"), "the pad binding is saved");

            // Play with the pad itself: X uses a light switch, A no longer does.
            var room = root.Office.Rooms["reception"];
            var sw = root.Office.Switches["reception"];
            var toRoom = room.Bounds.center - sw.transform.position; toRoom.y = 0;
            var stand = sw.transform.position + toRoom.normalized * 0.9f; stand.y = 0;
            root.Player.Teleport(stand, 0, 0);
            yield return Aim(sw.transform.position, 0.3f);
            yield return Wait(0.2f);
            bool lit = room.LightsOn;
            var scripted = GameInput.Override;
            GameInput.Override = null;
            yield return Press(GamepadButton.South);
            yield return Wait(0.3f);
            Check(room.LightsOn == lit, "after rebinding, pad A no longer uses the switch");
            Check(GameObject.Find("Key_X") != null && GameInput.Glyph("E") == "X", $"the prompt shows the X button ({GameInput.Glyph("E")})");
            yield return Shot("n1_pad_prompt_rebound");
            yield return Press(GamepadButton.West);
            yield return Wait(0.3f);
            Check(room.LightsOn != lit, "pad X uses the switch");
            var ds = InputSystem.AddDevice<UnityEngine.InputSystem.DualShock.DualShock4GamepadHID>("AutoPilotDS4Remap");
            ds.MakeCurrent();
            yield return Wait(0.2f);
            Check(GameInput.Glyph("E") == "□", $"on a DualShock the rebound prompt shows □ ({GameInput.Glyph("E")})");
            InputSystem.RemoveDevice(ds);
            vpad.MakeCurrent();
            GameInput.Override = scripted;
            if (room.LightsOn != lit) sw.Toggle();
            Controls.ResetPad(Settings.Current);
            Settings.Save();
            Check(Controls.PadPathOf(Settings.Current, Act.Interact) == "<Gamepad>/buttonSouth" && GameInput.Glyph("E") == "A", "reset brings back the pad's defaults");
            yield return Wait(0.2f);
        }

        // ---- hold or toggle (night 1) -------------------------------------------------------------

        IEnumerator ToggleChecks()
        {
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotToggleKeyboard");
            vkb.MakeCurrent();
            IEnumerator Keys(params UnityEngine.InputSystem.Key[] keys)
            {
                var st = new KeyboardState();
                foreach (var k in keys) st.Set(k, true);
                InputSystem.QueueStateEvent(vkb, st);
                yield return Wait(0.25f);
            }
            var player = root.Player;
            var s = Settings.Current;
            // Switch crouch to Toggle through the real page.
            SettingsPanel.Show();
            yield return Wait(0.4f);
            FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name.StartsWith("Btn_Keyboard"))?.onClick.Invoke();
            yield return Wait(0.4f);
            GameObject.Find("Choice_Crouch mode")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            GameObject.Find("Choice_Brisk walk mode")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            GameObject.Find("Choice_Clean and spray mode")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            yield return Wait(0.3f);
            Check(s.ToggleCrouch && s.ToggleSprint && s.ToggleUse, "the controls page switches crouch, brisk walk and clean and spray to Toggle");
            yield return Shot("controls_toggle");
            FindAnyObjectByType<ControlsPanel>()?.SendMessage("Close");
            yield return Wait(0.2f);
            FindAnyObjectByType<SettingsPanel>()?.SendMessage("Close");
            yield return WaitUnblocked(3f);
            root.Player.Teleport(new Vector3(12.2f, 0, 7.0f), 0f, 0f);
            yield return Wait(0.3f);
            var scripted = GameInput.Override;
            GameInput.Override = null;

            // Toggle: a tap crouches, the next tap stands.
            yield return Keys(UnityEngine.InputSystem.Key.C);
            yield return Keys();
            yield return Wait(0.3f);
            bool down = player.Crouching;
            yield return Keys(UnityEngine.InputSystem.Key.C);
            yield return Keys();
            yield return Wait(0.3f);
            Check(down && !player.Crouching, $"toggle crouch: one tap of C crouches, the next stands ({down}, {player.Crouching})");
            // On the pad: the left stick click.
            vpad.MakeCurrent();
            InputSystem.QueueStateEvent(vpad, new GamepadState().WithButton(GamepadButton.LeftStick));
            yield return Wait(0.2f);
            InputSystem.QueueStateEvent(vpad, new GamepadState());
            yield return Wait(0.4f);
            Check(player.Crouching, "toggle crouch: a tap of the pad's stick click crouches");
            InputSystem.QueueStateEvent(vpad, new GamepadState().WithButton(GamepadButton.LeftStick));
            yield return Wait(0.2f);
            InputSystem.QueueStateEvent(vpad, new GamepadState());
            yield return Wait(0.4f);
            Check(!player.Crouching, "toggle crouch: the next tap stands");
            vkb.MakeCurrent();
            // Toggle brisk walk: a tap of Shift while walking stays on until you stop.
            yield return Keys(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift);
            yield return Keys(UnityEngine.InputSystem.Key.W);
            yield return Wait(0.2f);
            bool brisk = GameInput.Frame.Sprint;
            yield return Keys();
            yield return Wait(0.1f);
            Check(brisk && !GameInput.Frame.Sprint, $"toggle brisk walk: stays on after letting go of Shift, ends when you stop ({brisk}, {GameInput.Frame.Sprint})");

            // Hold (the default) again: crouch only while C is down.
            s.ToggleCrouch = s.ToggleSprint = false;
            yield return Keys(UnityEngine.InputSystem.Key.C);
            yield return Wait(0.3f);
            down = player.Crouching;
            yield return Keys();
            yield return Wait(0.4f);
            Check(down && !player.Crouching, "hold crouch: crouched while C is held, up when it's let go");
            yield return ToggleCleanChecks();
            Settings.Save();
            GameInput.Override = scripted;
            InputSystem.RemoveDevice(vkb);
            vpad.MakeCurrent();
            root.Player.Teleport(root.Player.transform.position, 0f, 0f);
            yield return Wait(0.2f);
        }

        /// <summary>
        /// Toggled clean, through a virtual mouse and the pad: a tap starts cleaning the carpet and it
        /// keeps getting cleaner with the button up; the next tap stops; the clipboard stops it; a
        /// held item charges on one tap and flies on the next. Ends back on Hold. Device input is live.
        /// </summary>
        IEnumerator ToggleCleanChecks()
        {
            var s = Settings.Current;
            // The dirtiest spot of an unfinished surface that can be reached.
            GrimeSurface floor = null;
            Vector3 spot = default, stand = default;
            foreach (var id in new[] { "desk_russ", "desk_priya", "desk_theo", "coffeetable_reception", "floor_bullpen" })
            {
                if (!root.Office.Surfaces.TryGetValue(id, out var g) || !g.gameObject.activeSelf || g.Done) continue;
                foreach (var p in g.DirtiestSpots(3))
                    if (StandFor(g, p) is Vector3 at) { floor = g; spot = p; stand = at; break; }
                if (floor != null) break;
            }
            Check(floor != null, $"toggle clean: a dirty surface within reach ({floor?.Id} at {spot})");
            if (floor == null) { s.ToggleUse = false; yield break; }
            var vmouse = InputSystem.AddDevice<Mouse>("AutoPilotToggleMouse");
            vmouse.MakeCurrent();
            IEnumerator Click(bool pad = false)
            {
                if (pad) InputSystem.QueueStateEvent(vpad, new GamepadState { rightTrigger = 1f });
                else InputSystem.QueueStateEvent(vmouse, new MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                yield return Wait(0.12f);
                if (pad) InputSystem.QueueStateEvent(vpad, new GamepadState());
                else InputSystem.QueueStateEvent(vmouse, new MouseState());
                yield return Wait(0.12f);
            }
            // Scrub around the spot for a while with the button up (the aim moves; nothing is held).
            var side = Vector3.Cross(Vector3.up, (spot - stand).normalized);
            IEnumerator Scrub(float seconds)
            {
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    LookAtNow(spot + side * Mathf.Sin(t * 6f) * 0.18f);
                    yield return null;
                }
            }
            var look = spot - stand;
            root.Player.Teleport(stand, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 30f);
            yield return Wait(0.4f);
            yield return Scrub(0.8f);
            Check(root.Cleaning.Target == floor && root.Cleaning.InReach, $"toggle clean: aiming at {floor.Id} ({root.Cleaning.Target?.Id}, in reach {root.Cleaning.InReach})");
            float c0 = floor.Completion;
            yield return Click();
            yield return Scrub(1.6f);
            float c1 = floor.Completion;
            Check(GameInput.Frame.Use && c1 > c0 + 0.002f, $"toggle clean: one tap of the mouse button and {floor.Id} keeps getting cleaner with it up ({c0:P1} → {c1:P1}, {root.Cleaning.Equipped})");
            yield return Click();
            yield return Scrub(0.2f);
            float c2 = floor.Completion;
            yield return Scrub(1.0f);
            float c3 = floor.Completion;
            Check(!GameInput.Frame.Use && c3 - c2 < 0.0005f, $"toggle clean: the next tap stops it ({c2:P2} → {c3:P2})");
            // The pad's trigger does the same.
            vpad.MakeCurrent();
            yield return Click(pad: true);
            yield return Scrub(0.5f);
            bool padOn = GameInput.Frame.Use;
            yield return Click(pad: true);
            yield return Wait(0.1f);
            Check(padOn && !GameInput.Frame.Use, $"toggle clean: a tap of the right trigger starts it and the next stops it ({padOn}, {GameInput.Frame.Use})");
            // A menu switches it off.
            vmouse.MakeCurrent();
            yield return Click();
            bool on = GameInput.Frame.Use;
            Clipboard.Instance.Show();
            yield return Wait(0.5f);
            Clipboard.Instance.Close();
            yield return WaitUnblocked(2f);
            yield return Wait(0.2f);
            Check(on && !GameInput.Frame.Use, "toggle clean: opening the clipboard switches it off");
            // A throw: one tap charges, the next throws.
            var item = FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(t => !t.Binned)
                .OrderBy(t => (t.transform.position - root.Player.transform.position).sqrMagnitude).FirstOrDefault();
            if (item != null)
            {
                var (itemPos, itemRot) = (item.transform.position, item.transform.rotation);
                root.Hands.Grab(item);
                yield return Wait(0.4f);
                root.Player.Pitch = -10f;
                yield return Click();
                yield return Wait(0.4f);
                bool charging = root.Hands.Charging && root.Hands.Holding == item;
                yield return Click();
                yield return Wait(0.2f);
                Check(charging && root.Hands.Holding == null, $"toggle throw: one tap charges ({charging}), the next throws ({root.Hands.Holding == null})");
                yield return Wait(1.5f);
                PutBack(item, itemPos, itemRot);
            }
            else Check(false, "toggle throw: some rubbish to throw");
            s.ToggleUse = false;
            GameInput.ReleaseUse();
            InputSystem.RemoveDevice(vmouse);
        }

        /// <summary>
        /// HUD text size: at each size the prompt (holding something, so it's the longest one), a
        /// long caption and a toast stay on screen and clear of the watch, and Largest is 1.5x Normal.
        /// </summary>
        IEnumerator TextSizeChecks()
        {
            var s = Settings.Current;
            var hud = Ui.Canvas.transform.Find("HUD");
            var item = FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(t => !t.Binned)
                .OrderBy(t => (t.transform.position - root.Player.transform.position).sqrMagnitude).FirstOrDefault();
            var (itemPos, itemRot) = item != null ? (item.transform.position, item.transform.rotation) : default;
            if (item != null) root.Hands.Grab(item);
            float W = Screen.width, H = Screen.height;
            Rect Corners(Vector3[] c) => Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y));
            Rect Bounds(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return Corners(c); }
            Rect TextBounds(TMPro.TextMeshProUGUI t)
            {
                var b = t.textBounds;
                return Corners(new[] { t.rectTransform.TransformPoint(b.min), t.rectTransform.TransformPoint(b.max) });
            }
            Rect Union(System.Collections.Generic.IEnumerable<Rect> rs)
            {
                var list = rs.ToList();
                return list.Count == 0 ? Rect.zero : Rect.MinMaxRect(list.Min(r => r.xMin), list.Min(r => r.yMin), list.Max(r => r.xMax), list.Max(r => r.yMax));
            }
            bool OnScreen(Rect r) => r.width > 0 && r.xMin >= -1 && r.yMin >= -1 && r.xMax <= W + 1 && r.yMax <= H + 1;
            var watch = Bounds((RectTransform)hud.Find("Watch"));
            float promptH0 = 0;
            for (int size = 0; size <= 2; size++)
            {
                s.TextSize = size;
                const string Long = "[The vacuum clunks against something wedged under the desk, and somewhere down the hall a phone rings twice, then stops]";
                Hud.Instance.Toast("✓ Wipe Theo's, Priya's and Russ's desks", "Bonus", Ui.Good, 3f);
                yield return Wait(0.4f);
                // Night 1 has its own opening captions; show ours until it's the one on screen.
                TMPro.TextMeshProUGUI captionLabel = null;
                for (int tries = 0; tries < 8 && captionLabel?.text != Long; tries++)
                {
                    Hud.Instance.Caption(Long, 6f);
                    yield return Wait(0.5f);
                    captionLabel = hud.Find("Caption").GetComponent<TMPro.TextMeshProUGUI>();
                }
                var prompt = Union(hud.Find("Prompt").Cast<RectTransform>().Select(Bounds));
                var caption = TextBounds(captionLabel);
                Check(captionLabel.text == Long, "the HUD text check measures its own caption");
                var toasts = Ui.Canvas.transform.Find("Toasts/Toasts").Cast<RectTransform>().Select(Bounds).ToList();
                if (size == 0) promptH0 = prompt.height;
                bool ok = OnScreen(prompt) && OnScreen(caption) && toasts.Count > 0 && toasts.All(OnScreen)
                          && !toasts.Any(t => t.Overlaps(watch)) && !caption.Overlaps(prompt);
                Check(ok, $"HUD text {Settings.TextSizes[size]} at {W}x{H}: the prompt ({prompt.width:F0}x{prompt.height:F0} px), a long caption ({caption.width:F0}x{caption.height:F0}) and {toasts.Count} toast(s) stay on screen, apart and clear of the watch");
                // The room's name under the watch: on screen, below the watch, clear of the toasts, at the text size.
                var roomLabel = Hud.Instance.RoomLine;
                var roomRect = TextBounds(roomLabel);
                bool roomOk = !string.IsNullOrEmpty(roomLabel.text) && OnScreen(roomRect) && roomRect.yMax <= watch.yMin + 1 && !toasts.Any(t => t.Overlaps(roomRect))
                              && Mathf.Abs(roomLabel.rectTransform.lossyScale.y / Ui.Canvas.transform.lossyScale.y - Settings.TextScale) < 0.01f;
                Check(roomOk, $"HUD text {Settings.TextSizes[size]} at {W}x{H}: the room line (\"{roomLabel.text}\", {roomRect.width:F0}x{roomRect.height:F0} px) is on screen under the watch and clear of the toasts");
                if (size == 2) Check(Mathf.Abs(prompt.height / Mathf.Max(1f, promptH0) - 1.5f) < 0.05f, $"HUD text Largest is 1.5x Normal (prompt {promptH0:F0} → {prompt.height:F0} px)");
                yield return Shot($"hud_text_{Settings.TextSizes[size].ToLowerInvariant()}");
                yield return Wait(2.6f); // let the toasts go
            }
            s.TextSize = 0;
            input.DropOnce = true;
            yield return Wait(0.4f);
            PutBack(item, itemPos, itemRot); // later checks expect it where the night left it
        }

        // ---- text size for what you read (round 5) ----------------------------------------------

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y));
        }

        static Rect TextRect(TMPro.TextMeshProUGUI t)
        {
            var b = t.textBounds;
            var a = t.rectTransform.TransformPoint(b.min);
            var z = t.rectTransform.TransformPoint(b.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, z.x), Mathf.Min(a.y, z.y), Mathf.Max(a.x, z.x), Mathf.Max(a.y, z.y));
        }

        static bool Inside(Rect inner, Rect outer, float slack = 2f) =>
            inner.xMin >= outer.xMin - slack && inner.yMin >= outer.yMin - slack && inner.xMax <= outer.xMax + slack && inner.yMax <= outer.yMax + slack;

        static Rect ScreenArea => new(0, 0, Screen.width, Screen.height);

        /// <summary>
        /// Every document at Normal and Largest: text on the paper, paper on the screen, text at
        /// least its Normal size; then a short and a long choice panel.
        /// </summary>
        IEnumerator ReadingTextChecks()
        {
            var s = Settings.Current;
            var docs = Docs.All.Values.ToList();
            var normal = new System.Collections.Generic.Dictionary<string, (float font, Vector2 card)>();
            var writtenFont = new System.Collections.Generic.Dictionary<string, string>();
            float W = Screen.width, H = Screen.height;
            // As written, then with Handwriting set Plain (round 10): the same fit, in the plain font.
            foreach (bool plain in new[] { false, true })
            foreach (int size in new[] { 0, 2 })
            {
                s.TextSize = size;
                s.PlainHandwriting = plain;
                if (size == 0) normal.Clear();
                int plainOk = 0, hand = 0;
                int ok = 0, bigger = 0, full = 0;
                var bad = new System.Collections.Generic.List<string>();
                foreach (var d in docs)
                {
                    InspectView.Show(d, InspectMode.Read, null, reread: true);
                    yield return Wait(0.55f);
                    var view = GameObject.Find("Inspect").transform;
                    var card = (RectTransform)view.Find("Card");
                    var body = view.Find("Card/Body").GetComponent<TMPro.TextMeshProUGUI>();
                    var shot = view.Find("Card/Screen").GetComponent<UnityEngine.UI.Image>();
                    body.ForceMeshUpdate();
                    var paper = ScreenRect(card);
                    bool fits = Inside(paper, ScreenArea);
                    if (body.enabled) fits &= !body.isTextOverflowing && (string.IsNullOrWhiteSpace(body.text) || Inside(TextRect(body), paper));
                    if (size == 0) normal[d.Id] = (body.fontSize, card.sizeDelta);
                    else if (normal.TryGetValue(d.Id, out var n0))
                    {
                        float ratio = body.enabled ? body.fontSize / n0.font : card.sizeDelta.x / n0.card.x;
                        fits &= ratio >= 0.999f;
                        if (ratio > 1.01f) bigger++;
                        if (ratio > 1.49f) full++;
                    }
                    if (body.enabled)
                    {
                        string fontName = body.font != null ? body.font.name : "";
                        if (!plain) writtenFont[d.Id] = fontName;
                        else if (writtenFont.TryGetValue(d.Id, out var was))
                        {
                            bool handwritten = was.StartsWith("Caveat");
                            if (handwritten) hand++;
                            if (handwritten ? fontName.StartsWith("FiraSans-Regular") : fontName == was) plainOk++;
                            else bad.Add($"{d.Id} (in {fontName}, as written {was})");
                        }
                    }
                    if (fits) ok++; else bad.Add($"{d.Id} ({(body.enabled ? (body.isTextOverflowing ? "overflows" : "off the paper") : "picture")}, paper {paper.width:F0}x{paper.height:F0})");
                    InspectView.AutoChoice = InspectChoice.Close;
                    yield return Wait(0.3f);
                }
                if (plain) Check(hand > 0 && plainOk == docs.Count(d => writtenFont.ContainsKey(d.Id)), $"Handwriting Plain at {Settings.TextSizes[size]}: the {hand} handwritten documents are in Fira Sans and the others keep their fonts ({plainOk} right)");
                Check(ok == docs.Count, $"documents{(plain ? " with Handwriting Plain" : "")} at {Settings.TextSizes[size]}, {W}x{H}: {ok}/{docs.Count} keep their text on the paper and the paper on screen{(size > 0 ? $", none smaller than Normal; {bigger} larger, {full} at 1.5x" : "")}{(bad.Count > 0 ? " — " + string.Join("; ", bad) : "")}");
                if (size == 2) Check(full >= docs.Count / 2, $"at Largest most documents' text is 1.5x ({full}/{docs.Count}){(plain ? " with Handwriting Plain" : "")}");
                if (plain && size == 0)
                {
                    // Theo's note in the plain lettering, and the shift sheet with it.
                    InspectView.Show(Docs.Get("theo_note") ?? docs[0], InspectMode.Read, null, reread: true);
                    yield return Wait(0.6f);
                    yield return Shot("plain_document");
                    InspectView.AutoChoice = InspectChoice.Close;
                    yield return Wait(0.4f);
                    Clipboard.Instance.Show();
                    yield return Wait(0.7f);
                    var sheet = Clipboard.Instance.GetComponentsInChildren<TMPro.TextMeshProUGUI>().FirstOrDefault(t => t.name == "Sheet");
                    Check(sheet != null && sheet.font.name.StartsWith("FiraSans-Regular") && !sheet.isTextOverflowing && Inside(TextRect(sheet), ScreenRect((RectTransform)sheet.transform.parent)),
                        $"Handwriting Plain: the shift sheet is in Fira Sans ({sheet?.font.name}, {sheet?.fontSize:F0}) and stays on the paper");
                    yield return Shot("plain_clipboard");
                    Clipboard.Instance.Close();
                    yield return Wait(0.4f);
                    s.PlainHandwriting = false;
                    Clipboard.Instance.Show();
                    yield return Wait(0.7f);
                    Check(sheet != null && sheet.font.name.StartsWith("Caveat"), $"switched back to As written, the shift sheet is in Caveat again ({sheet?.font.name})");
                    Clipboard.Instance.Close();
                    yield return Wait(0.4f);
                    s.PlainHandwriting = true;
                }
            }
            s.PlainHandwriting = false;
            // One document at Largest for the screenshot: Theo's crumpled note.
            InspectView.Show(Docs.Get("theo_note") ?? docs[0], InspectMode.Read, null, reread: true);
            yield return Wait(0.6f);
            yield return Shot("largest_document");
            InspectView.AutoChoice = InspectChoice.Close;
            yield return Wait(0.4f);

            // Choices: a short list grows 1.5x; a long one (every lead, as at a tray's sticky note) still fits.
            foreach (int count in new[] { 2, 10 })
            {
                var opts = Enumerable.Range(1, count).Select(i => new ChoiceMenu.Option($"Option {i}", i % 2 == 0 ? "A line under it" : null, null)).ToList();
                ChoiceMenu.Show("A question at the largest text size", "Whatever you leave here, they find in the morning.", opts);
                yield return Wait(0.5f);
                var panel = (RectTransform)Ui.Canvas.transform.Find("Choice/Panel");
                var r = ScreenRect(panel);
                bool want15 = count == 2;
                Check(Inside(r, ScreenArea) && (!want15 || Mathf.Abs(ChoiceMenu.Scale - 1.5f) < 0.01f) && ChoiceMenu.Scale >= 1f,
                    $"a choice with {count + 1} options at Largest is {ChoiceMenu.Scale:F2}x and on screen ({r.width:F0}x{r.height:F0} px of {W}x{H})");
                if (want15) yield return Shot("largest_choice");
                ChoiceMenu.AutoPick = 99;
                yield return Wait(0.4f);
            }
            s.TextSize = 0;
            yield return WaitUnblocked(3f);
        }

        /// <summary>Where one part (page) of a paged text draws on screen: its visible characters' bounds.</summary>
        static Rect PartRect(TMPro.TextMeshProUGUI t, int page)
        {
            var info = t.textInfo;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible || c.pageNumber != page) continue;
                foreach (var v in new[] { c.bottomLeft, c.topRight })
                {
                    var w = t.rectTransform.TransformPoint(v);
                    x0 = Mathf.Min(x0, w.x); y0 = Mathf.Min(y0, w.y); x1 = Mathf.Max(x1, w.x); y1 = Mathf.Max(y1, w.y);
                }
            }
            return x0 > x1 ? Rect.zero : Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>
        /// The clipboard at Large and Largest (night 2, the longest sheet): one wide sheet with three
        /// pages, text at the setting's scale, long pages in parts that stay on the paper; Normal
        /// laid out as before.
        /// </summary>
        IEnumerator ClipboardLargeChecks()
        {
            var cb = Clipboard.Instance;
            var s = Settings.Current;
            var pad = InputSystem.AddDevice<Gamepad>("AutoPilotClipPad");
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotClipKeyboard");
            IEnumerator PadPress(GamepadButton b)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return Wait(0.35f);
            }
            IEnumerator Key(UnityEngine.InputSystem.Key k)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(k));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return Wait(0.35f);
            }
            var clip = Ui.Canvas.transform.Find("Clipboard");
            var board = (RectTransform)clip.Find("Board");
            var paperRt = (RectTransform)clip.Find("Board/Paper");
            var note = clip.Find("Side");
            TMPro.TextMeshProUGUI Label(string name) => clip.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).First(t => t.name == name);
            float W = Screen.width, H = Screen.height;

            vkb.MakeCurrent();
            s.TextSize = 0;
            cb.Show();
            yield return Wait(0.6f);
            Check(!cb.Large && board.sizeDelta == new Vector2(760, 920) && Mathf.Abs(board.anchoredPosition.x + 170f) < 0.5f && note.gameObject.activeSelf
                  && Label("Sheet").enableAutoSizing && Mathf.Approximately(Label("Notes").fontSize, 22f),
                "at Normal the clipboard is laid out as before: the sheet, and the side note beside it");
            cb.Close();
            yield return Wait(0.4f);

            for (int size = 1; size <= 2; size++)
            {
                s.TextSize = size;
                float k = Settings.TextScale;
                cb.Show(); // as Tab does (play keys come from the AutoPilot's own input; menu keys below are real)
                yield return Wait(0.7f);
                var sheet = Label("Sheet");
                var paper = ScreenRect(paperRt);
                Check(cb.Open && cb.Large && cb.Page == Clipboard.SheetPage && !note.gameObject.activeSelf && Inside(ScreenRect(board), ScreenArea, 30f) && Inside(paper, ScreenArea),
                    $"{Settings.TextSizes[size]} at {W}x{H}: the clipboard opens as one wide sheet ({paper.width:F0}x{paper.height:F0} px), the side note folded into it");
                Check(Mathf.Abs(sheet.fontSize - 33f * k) < 0.5f && Mathf.Abs(Label("Header").fontSize - 34f * k) < 0.5f && Mathf.Abs(Label("PageHint").fontSize - 20f * k) < 0.5f,
                    $"{Settings.TextSizes[size]}: the sheet's text is {k}x Normal's (tasks {sheet.fontSize:F1}, header {Label("Header").fontSize:F1}, footer {Label("PageHint").fontSize:F1})");
                // Every part of the sheet stays on the paper, and the parts hold every task.
                int parts = cb.Parts, onPaper = 0;
                var tasksSeen = new System.Collections.Generic.HashSet<string>();
                var def = root.Director.Def;
                for (int part = 0; part < parts; part++)
                {
                    if (part > 0) yield return Key(UnityEngine.InputSystem.Key.S);
                    sheet.ForceMeshUpdate();
                    var r = PartRect(sheet, part);
                    if (cb.Part == part && r.width > 0 && Inside(r, paper)) onPaper++;
                    var info = sheet.textInfo;
                    var visible = new System.Text.StringBuilder();
                    for (int i = 0; i < info.characterCount; i++) if (info.characterInfo[i].pageNumber == part) visible.Append(info.characterInfo[i].character);
                    foreach (var t in def.Tasks) if (visible.ToString().Contains(t.Label)) tasksSeen.Add(t.Id);
                    if (size == 2 && part == 0) yield return Shot("n2_clipboard_largest_sheet");
                    if (size == 2 && part == 1) yield return Shot("n2_clipboard_largest_sheet_part2");
                }
                Check(onPaper == parts && tasksSeen.Count == def.Tasks.Count,
                    $"{Settings.TextSizes[size]}: the sheet is in {parts} part(s) reached with S, each on the paper, holding all {tasksSeen.Count}/{def.Tasks.Count} tasks");
                if (parts > 1)
                {
                    yield return Key(UnityEngine.InputSystem.Key.S);
                    Check(cb.Part == 0, "S on the last part goes back to the top");
                    yield return Key(UnityEngine.InputSystem.Key.S);
                    yield return Key(UnityEngine.InputSystem.Key.W);
                    Check(cb.Part == 0, "W goes back a part");
                }
                // Pages: D to the notes, D to the case file, A back; then the pad.
                yield return Key(UnityEngine.InputSystem.Key.D);
                var notes = Label("Notes");
                notes.ForceMeshUpdate();
                bool notesOk = cb.Page == Clipboard.NotesPage && notes.enabled && notes.text.Contains("SECRETS") && Mathf.Abs(notes.fontSize - 22f * k) < 0.5f
                               && Enumerable.Range(0, cb.Parts).All(p => { var r = PartRect(notes, p); return r.width > 0 && Inside(r, paper); });
                Check(notesOk, $"{Settings.TextSizes[size]}: D turns to the notes (secrets, leads, pocket) at {notes.fontSize:F1}, on the paper in {cb.Parts} part(s)");
                if (size == 2) yield return Shot("n2_clipboard_largest_notes");
                yield return Key(UnityEngine.InputSystem.Key.D);
                var files = Label("CaseFile");
                files.ForceMeshUpdate();
                Check(cb.Page == Clipboard.CasePage && files.enabled && !files.isTextOverflowing && Inside(TextRect(files), paper) && cb.CaseEntries.Count > 0,
                    $"{Settings.TextSizes[size]}: D again turns to the case file, which fits the paper ({cb.CaseEntries.Count} documents)");
                if (size == 2)
                {
                    yield return Shot("n2_clipboard_largest_case_file");
                    yield return Key(UnityEngine.InputSystem.Key.E);
                    yield return Wait(0.4f);
                    Check(InspectView.IsOpen && InspectView.CurrentDoc == cb.CaseEntries[0], $"E reads the first case-file entry again ({InspectView.CurrentDoc})");
                    yield return Key(UnityEngine.InputSystem.Key.Escape);
                    Check(!InspectView.IsOpen && cb.Open, "Esc closes it and leaves the clipboard open");
                }
                yield return Key(UnityEngine.InputSystem.Key.A);
                yield return Key(UnityEngine.InputSystem.Key.A);
                yield return Key(UnityEngine.InputSystem.Key.A);
                Check(cb.Page == Clipboard.SheetPage, "A turns back through the notes to the shift sheet and stops there");
                pad.MakeCurrent();
                yield return PadPress(GamepadButton.DpadRight);
                bool padNotes = cb.Page == Clipboard.NotesPage;
                yield return PadPress(GamepadButton.DpadLeft);
                Check(padNotes && cb.Page == Clipboard.SheetPage, "the d-pad turns the pages too");
                if (cb.Parts > 1)
                {
                    yield return PadPress(GamepadButton.DpadDown);
                    Check(cb.Part == 1, "d-pad down shows the next part");
                }
                vkb.MakeCurrent();
                GameInput.UsingPad = false;
                yield return Key(UnityEngine.InputSystem.Key.Escape);
                Check(!cb.Open && !PauseMenu.IsOpen, "Esc closes the clipboard");
            }
            s.TextSize = 0;
            cb.Show();
            yield return Wait(0.6f);
            Check(!cb.Large && note.gameObject.activeSelf && board.sizeDelta == new Vector2(760, 920), "back at Normal, the side note returns");
            cb.Close();
            InputSystem.RemoveDevice(vkb);
            InputSystem.RemoveDevice(pad);
            GameInput.UsingPad = false;
            yield return WaitUnblocked(3f);
        }

        // ---- the morning chats again, and replaying an earlier night (round 6) ----------------------

        /// <summary>Each morning's messages as the chat showed them.</summary>
        readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<string>> morningTexts = new();

        static string StoryKey() => JsonUtility.ToJson(Story.State.Evidence) + JsonUtility.ToJson(new Wrap { Read = Story.State.Read, Chats = Story.State.Chats, Phrases = Story.State.Phrases, Inventory = Story.State.Inventory });
        [Serializable] class Wrap { public System.Collections.Generic.List<ReadRecord> Read; public System.Collections.Generic.List<ChatRecord> Chats; public System.Collections.Generic.List<string> Phrases, Inventory; }

        /// <summary>Night 2: Tuesday morning's chat is in the case file; reading it shows that morning, from the first message, and changes nothing.</summary>
        IEnumerator ChatAgainChecks()
        {
            var cb = Clipboard.Instance;
            var pad = InputSystem.AddDevice<Gamepad>("AutoPilotChatPad");
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotChatKeyboard");
            IEnumerator PadPress(GamepadButton b)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return Wait(0.35f);
            }
            IEnumerator Key(UnityEngine.InputSystem.Key k)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(k));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return Wait(0.35f);
            }
            pad.MakeCurrent();
            cb.Show(Clipboard.CasePage);
            yield return Wait(0.5f);
            string entry = Clipboard.ChatPrefix + "1";
            int idx = cb.CaseEntries.IndexOf(entry);
            int lastDoc = cb.CaseEntries.FindLastIndex(e => Story.State.Read.Any(r => r.Night == 1 && r.Id == e));
            Check(idx >= 0 && idx == lastDoc + 1, $"the case file lists Tuesday morning's chat after night 1's documents (entry {idx}, last document {lastDoc})");
            for (int i = 0; i < 30 && cb.Selected < idx; i++) yield return PadPress(GamepadButton.DpadDown);
            var list = GameObject.Find("CaseFile")?.GetComponent<TMPro.TextMeshProUGUI>()?.text ?? "";
            Check(cb.Selected == idx && list.Contains("Tuesday morning · #general"), $"the d-pad reaches it, listed as Tuesday morning · #general ({cb.Selected})");
            yield return Shot("n2_case_file_chat");
            string story = StoryKey();
            yield return PadPress(GamepadButton.South);
            yield return Wait(0.4f);
            var chat = FindAnyObjectByType<ChatInterlude>();
            var texts = chat ? chat.Content.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Where(t => t.name == "Message").Select(t => t.text).ToList() : new System.Collections.Generic.List<string>();
            morningTexts.TryGetValue(1, out var want);
            Check(ChatInterlude.ReviewOpen && chat != null && chat.Review && want != null && texts.SequenceEqual(want),
                $"pad A opens Tuesday morning's chat with the {texts.Count} messages shown that morning ({want?.Count ?? 0})");
            var view = chat ? (RectTransform)chat.Content.parent : null;
            var firstRow = chat && chat.Content.childCount > 0 ? (RectTransform)chat.Content.GetChild(0) : null;
            Check(chat && chat.Scroll == 0f && firstRow && Inside(ScreenRect(firstRow), ScreenRect(view), 2f), "it opens at the first message");
            yield return Shot("n2_chat_again");
            if (chat && chat.MaxScroll > 1f)
            {
                yield return PadPress(GamepadButton.DpadDown);
                Check(chat.Scroll > 0f, $"d-pad down scrolls it ({chat.Scroll:F0} of {chat.MaxScroll:F0} units)");
            }
            else Log($"the chat fits without scrolling ({texts.Count} messages)");
            yield return PadPress(GamepadButton.East);
            yield return Wait(0.3f);
            Check(!ChatInterlude.ReviewOpen && cb.Open && cb.Page == Clipboard.CasePage && cb.Selected == idx && StoryKey() == story,
                "pad B closes it, back on the case file, and nothing in the story changed");
            vkb.MakeCurrent();
            yield return Key(UnityEngine.InputSystem.Key.E);
            yield return Wait(0.3f);
            Check(ChatInterlude.ReviewOpen, "E opens it again");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            yield return Wait(0.3f);
            Check(!ChatInterlude.ReviewOpen && cb.Open && !PauseMenu.IsOpen, "Esc closes it, the clipboard stays open and nothing pauses");
            // At Largest the morning is longer than the window: S scrolls it on, W back to the top.
            Settings.Current.TextSize = 2;
            yield return Key(UnityEngine.InputSystem.Key.E);
            yield return Wait(0.3f);
            chat = FindAnyObjectByType<ChatInterlude>();
            float max = chat && chat.Review ? chat.MaxScroll : 0f;
            yield return Key(UnityEngine.InputSystem.Key.S);
            yield return Wait(0.3f);
            float down = chat ? chat.Scroll : 0f;
            yield return Shot("n2_chat_again_largest");
            for (int i = 0; i < 8 && chat && chat.Scroll > 0f; i++) yield return Key(UnityEngine.InputSystem.Key.W);
            Check(chat && chat.Review && max > 0f && down > 0f && chat.Scroll == 0f,
                $"at Largest it scrolls: S moves on ({down:F0} of {max:F0} units), W comes back to the first message");
            yield return Key(UnityEngine.InputSystem.Key.Tab);
            yield return Wait(0.3f);
            Settings.Current.TextSize = 0;
            Check(!ChatInterlude.ReviewOpen && cb.Open, "Tab closes it too, leaving the clipboard open");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            Check(!cb.Open && !PauseMenu.IsOpen, "Esc then closes the clipboard");
            InputSystem.RemoveDevice(vkb);
            InputSystem.RemoveDevice(pad);
            GameInput.UsingPad = false;
            yield return WaitUnblocked(3f);
        }

        /// <summary>
        /// Night 3, from the title: an earlier night in Night Select asks first, backing out keeps
        /// the story on night 3, a confirmed replay moves Continue, and Night 3 in Night Select puts
        /// it back.
        /// </summary>
        IEnumerator ReplayAskChecks()
        {
            var dir = root.Director;
            int results = Story.State.Results.Count, chats = Story.State.Chats.Count;
            Transform Card(int n) => FindAnyObjectByType<NightSelect>()?.transform.Find("Night" + n);
            void Pick(int n) => Card(n)?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            // The clipboard's case file tonight, less tonight's own reads, is what the save holds.
            var cb = Clipboard.Instance;
            cb.Show(Clipboard.CasePage);
            yield return Wait(0.4f);
            var tonight = Story.State.Read.Where(r => r.Night == dir.Def.Number).Select(r => r.Id).ToList();
            var clipboardList = cb.CaseEntries.Where(e => !tonight.Contains(e)).ToList();
            cb.Close();
            yield return WaitUnblocked(3f);
            root.ToTitle();
            yield return Wait(1.5f);
            Check(TitleScreen.Instance != null && GameObject.Find("Btn_Continue  ·  Night 3") != null, "mid-story, the title offers Continue on Night 3");
            yield return TitleCaseFileChecks(false, clipboardList);
            NightSelect.Show();
            yield return Wait(0.6f);
            vpad = InputSystem.AddDevice<Gamepad>("AutoPilotReplayPad"); // night 1's pad was unplugged
            vpad.MakeCurrent();
            GameInput.UsingPad = true;
            Pick(1);
            yield return Wait(0.5f);
            Check(ChoiceMenu.IsOpen && NightSelect.IsOpen, "picking an earlier night mid-story asks first");
            yield return Shot("replay_confirm");
            yield return Press(GamepadButton.East);
            yield return Wait(0.4f);
            Check(!ChoiceMenu.IsOpen && NightSelect.IsOpen && StoryState.Load()?.Night == 3, "pad B backs out, with Night Select open and the save still on night 3");
            // Pad A on Never mind: the d-pad also moves Night Select's own selection behind the question.
            Pick(1);
            yield return Wait(0.5f);
            yield return Press(GamepadButton.DpadDown);
            yield return Wait(0.2f);
            yield return Press(GamepadButton.South);
            yield return Wait(0.8f);
            Check(!ChoiceMenu.IsOpen && NightSelect.IsOpen && TitleScreen.Instance != null && StoryState.Load()?.Night == 3,
                "pad A on Never mind doesn't also pick the card behind the question");
            // Confirmed: night 2 starts, and Continue moves to it.
            Pick(2);
            yield return Wait(0.5f);
            ChoiceMenu.AutoPick = 0;
            yield return WaitUnblocked(40f);
            Check(dir.Def?.Number == 2 && dir.Running && StoryState.Load()?.Night == 2, "confirming replays night 2 and Continue moves to it");
            root.ToTitle();
            yield return Wait(1.5f);
            Check(GameObject.Find("Btn_Continue  ·  Night 2") != null, "the title now offers Continue on Night 2");
            // Night 3 is still there, and picking it (later than the save) doesn't ask.
            NightSelect.Show();
            yield return Wait(0.6f);
            Pick(3);
            yield return Wait(0.5f);
            Check(!ChoiceMenu.IsOpen, "a later night doesn't ask");
            yield return WaitUnblocked(40f);
            Check(dir.Def?.Number == 3 && dir.Running && Story.State.Night == 3 && Story.State.Results.Count == results && Story.State.Chats.Count == chats,
                $"Night Select puts the story back at the start of night 3 ({Story.State.Results.Count} results, {Story.State.Chats.Count} chats kept)");
            GameInput.UsingPad = false;
            InputSystem.RemoveDevice(vpad);
            vpad = null;
        }

        // ---- the case file from the title (round 7) ------------------------------------------------

        /// <summary>
        /// The title's Case file: the save's list (the clipboard's, mid-story), documents and a
        /// morning read again over the menu with pad and keys, the save untouched, Esc back to the
        /// title. Mid-story it's also walked at Largest and the six-button title is laid out at
        /// each size; after the ending, Night 7's documents are listed.
        /// </summary>
        IEnumerator TitleCaseFileChecks(bool ended, System.Collections.Generic.List<string> clipboardList)
        {
            string when = ended ? "after the ending" : "mid-story";
            Interstitial.AutoAdvance = false; // left pending by clicking through the ending; it would close the chat read again
            var pad = InputSystem.AddDevice<Gamepad>("AutoPilotCaseFilePad");
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotCaseFileKeyboard");
            var padBefore = vpad;
            vpad = pad; // Press() and ListChecks drive this pad
            IEnumerator PadPress(GamepadButton b) { yield return Press(b); yield return Wait(0.35f); }
            IEnumerator Key(UnityEngine.InputSystem.Key k)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(k));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return Wait(0.35f);
            }
            string savePath = System.IO.Path.Combine(StoryState.Dir, "save.json");
            byte[] SaveBytes() => System.IO.File.Exists(savePath) ? System.IO.File.ReadAllBytes(savePath) : new byte[0];
            var before = SaveBytes();
            var saved = StoryState.Load();
            var want = CaseFile.Entries(saved).Select(e => e.id).ToList();
            var title = TitleScreen.Instance;
            var btn = title ? title.transform.Find("Menu/Btn_Case file") : null;
            Check(btn != null && want.Count > 0, $"{when}, the title offers the case file ({want.Count} entries in the save)");
            if (clipboardList != null)
                Check(want.SequenceEqual(clipboardList), $"{when}, the save's case file is the clipboard's from tonight, less tonight's reads ({want.Count} and {clipboardList.Count} entries)");
            if (ended)
            {
                var nights = CaseFile.Entries(saved).Select(e => e.night).ToList();
                Check(want.Count > 1 && want[0] == CaseFile.EndingId && nights[1] == 7 && nights.Skip(1).Distinct().Count() >= 6,
                    $"after the ending it starts with the ending, then night 7's documents ({nights.Count(n => n == 7)}), and covers {nights.Skip(1).Distinct().Count()} nights");
            }
            if (btn == null) { vpad = padBefore; InputSystem.RemoveDevice(pad); InputSystem.RemoveDevice(vkb); yield break; }
            if (!ended) yield return TitleTextChecks(); // six buttons now (Continue and Case file)

            bool padWas = GameInput.UsingPad;
            pad.MakeCurrent();
            GameInput.UsingPad = true;
            EventSystem.current.SetSelectedGameObject(btn.gameObject);
            yield return Wait(0.2f);
            yield return PadPress(GamepadButton.South);
            yield return Wait(0.4f);
            Check(CaseFilePanel.IsOpen && CaseFilePanel.Entries.SequenceEqual(want) && Selected == "Entry_" + want[0],
                $"pad A opens it: the save's {want.Count} entries in order, the first selected ({Selected})");
            yield return Shot(ended ? "title_case_file_ended" : "title_case_file");

            if (ended) yield return EndingAgainChecks(saved, before, PadPress, Key);

            // A document, read again over the list.
            string doc = want.FirstOrDefault(e => CaseFile.ChatNight(e) < 0 && e != CaseFile.EndingId);
            int docAt = want.IndexOf(doc);
            for (int i = 0; i < docAt; i++) yield return PadPress(GamepadButton.DpadDown);
            Check(Selected == "Entry_" + doc, $"the d-pad reaches {doc} ({Selected})");
            yield return PadPress(GamepadButton.South);
            yield return Wait(0.3f);
            var inspect = Ui.Canvas.transform.Find("Inspect")?.GetComponent<Canvas>();
            var panelCanvas = Ui.Canvas.transform.Find("CaseFile")?.GetComponent<Canvas>();
            Check(InspectView.IsOpen && InspectView.CurrentDoc == doc && inspect && panelCanvas && inspect.sortingOrder > panelCanvas.sortingOrder,
                $"pad A reads {doc} again, drawn above the list ({inspect?.sortingOrder} over {panelCanvas?.sortingOrder})");
            yield return Shot(ended ? "title_case_file_read_ended" : "title_case_file_read");
            yield return PadPress(GamepadButton.East);
            Check(!InspectView.IsOpen && CaseFilePanel.IsOpen && Selected == "Entry_" + doc, "pad B closes it, back on the list with the same entry selected");
            yield return PadPress(GamepadButton.South);
            yield return Wait(0.3f);
            yield return PadPress(GamepadButton.South);
            yield return Wait(0.4f);
            Check(!InspectView.IsOpen && CaseFilePanel.IsOpen, "pad A closes it too, without opening the entry again");

            // A morning's chat, with the keyboard.
            string chat = want.FirstOrDefault(e => CaseFile.ChatNight(e) >= 0);
            if (chat != null)
            {
                vkb.MakeCurrent();
                GameInput.UsingPad = false;
                int at = want.IndexOf(chat), from = want.IndexOf(doc);
                for (int i = from; i < at; i++) yield return Key(UnityEngine.InputSystem.Key.DownArrow);
                Check(Selected == "Entry_" + chat, $"the down arrow reaches {chat} ({Selected})");
                yield return Key(UnityEngine.InputSystem.Key.Enter);
                yield return Wait(0.4f);
                var live = FindAnyObjectByType<ChatInterlude>();
                int night = CaseFile.ChatNight(chat);
                var texts = live ? live.Content.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Where(t => t.name == "Message").Select(t => t.text).ToList() : new System.Collections.Generic.List<string>();
                bool known = morningTexts.TryGetValue(night, out var shown);
                Check(ChatInterlude.ReviewOpen && live && live.Review && texts.Count > 0 && (!known || texts.SequenceEqual(shown)),
                    $"Enter opens {CaseFile.MorningLabel(night)}'s chat ({texts.Count} messages{(known ? $", as shown that morning: {shown.Count}" : "")})");
                yield return Shot(ended ? "title_case_file_chat_ended" : "title_case_file_chat");
                yield return Key(UnityEngine.InputSystem.Key.Escape);
                yield return Wait(0.3f);
                Check(!ChatInterlude.ReviewOpen && CaseFilePanel.IsOpen && TitleScreen.Instance != null, "Esc closes the chat, leaving the list open");
            }
            else Log("case file: no morning chat in this save");

            if (!ended)
            {
                // At Largest: rows at 1.5×, the d-pad walks every one in view and on to Back.
                FindAnyObjectByType<CaseFilePanel>()?.SendMessage("Close");
                yield return Wait(0.3f);
                Settings.Current.TextSize = 2;
                pad.MakeCurrent();
                GameInput.UsingPad = true;
                CaseFilePanel.Show(StoryState.Load());
                yield return Wait(0.6f);
                yield return ListChecks("CaseFile", () => CaseFilePanel.List, "the case file", "Btn_Back", 3, false);
                var listView = CaseFilePanel.List ? (RectTransform)CaseFilePanel.List.transform : null;
                var heading = listView ? (RectTransform)listView.Find("Content").GetChild(0) : null;
                Check(heading && Inside(ScreenRect(heading), ScreenRect(listView), 2f), "back on the first entry, its night's heading is in view too");
                yield return Shot("title_case_file_largest");
                FindAnyObjectByType<CaseFilePanel>()?.SendMessage("Close");
                yield return Wait(0.3f);
                Settings.Current.TextSize = 0;
                CaseFilePanel.Show(StoryState.Load());
                yield return Wait(0.5f);
            }

            vkb.MakeCurrent();
            GameInput.UsingPad = false;
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            yield return Wait(1f);
            Check(!CaseFilePanel.IsOpen && TitleScreen.Instance != null && !SettingsPanel.IsOpen && !NightSelect.IsOpen,
                "Esc closes the case file, back on the title without starting a night");
            var after = SaveBytes();
            Check(after.Length > 0 && after.SequenceEqual(before), $"reading from the title leaves the save as it was ({after.Length} bytes)");
            GameInput.UsingPad = padWas;
            vpad = padBefore;
            InputSystem.RemoveDevice(pad);
            InputSystem.RemoveDevice(vkb);
        }

        /// <summary>
        /// The ending read again from the title's case file (round 8): the same headline,
        /// epilogue and stats as when it was reached, over the list; pad B and Esc close it back
        /// to the list, with the save unchanged.
        /// </summary>
        IEnumerator EndingAgainChecks(StoryState saved, byte[] before, Func<GamepadButton, IEnumerator> padPress, Func<UnityEngine.InputSystem.Key, IEnumerator> key)
        {
            Check(Selected == "Entry_" + CaseFile.EndingId, $"the ending is the first entry, under its own heading ({Selected})");
            yield return padPress(GamepadButton.South);
            yield return Wait(0.6f);
            var screen = FindObjectsByType<EndingScreen>(FindObjectsSortMode.None).FirstOrDefault(x => x.Review);
            var layer = Ui.Canvas.transform.Find("Ending")?.GetComponent<Canvas>();
            var panelCanvas = Ui.Canvas.transform.Find("CaseFile")?.GetComponent<Canvas>();
            string shown = screen ? System.Text.RegularExpressions.Regex.Replace(screen.Shown, "<[^>]+>", "") : "";
            Check(EndingScreen.ReviewOpen && screen && endingSeen != null && shown == endingSeen && layer && panelCanvas && layer.sortingOrder > panelCanvas.sortingOrder,
                $"pad A reads the ending again: the same headline, {saved.Results.Count} nights' stats and epilogue as when it was reached ({shown.Split('\n').Length} lines), above the list");
            yield return Shot("title_case_file_ending");
            yield return padPress(GamepadButton.East);
            yield return Wait(0.6f);
            Check(!EndingScreen.ReviewOpen && CaseFilePanel.IsOpen && Selected == "Entry_" + CaseFile.EndingId && !FindObjectsByType<EndingScreen>(FindObjectsSortMode.None).Any(),
                "pad B closes it, back on the list with the ending selected");
            yield return padPress(GamepadButton.South);
            yield return Wait(0.6f);
            yield return key(UnityEngine.InputSystem.Key.Escape);
            yield return Wait(0.6f);
            Check(!EndingScreen.ReviewOpen && CaseFilePanel.IsOpen && TitleScreen.Instance != null && Selected == "Entry_" + CaseFile.EndingId,
                "Esc closes it too, leaving the list open on the ending");
        }

        // ---- the menus at larger text sizes (round 6) ----------------------------------------------

        static float Units(float px) => px * 1080f / Screen.height;
        /// <summary>The scale the pause menu should reach at Largest: 1.5 where the screen is wide enough, less on 4:3.</summary>
        static bool Wide => Screen.width / (float)Screen.height > 1.5f;

        /// <summary>The pause menu at each text size: buttons and card grow, stay on screen and apart; then the brightness page.</summary>
        IEnumerator PauseTextChecks()
        {
            var s = Settings.Current;
            var pause = Ui.Canvas.transform.Find("Pause");
            var card = (RectTransform)pause.Find("ControlsCard");
            var list = card.Find("ControlsList").GetComponent<TMPro.TextMeshProUGUI>();
            var menu = (RectTransform)pause.Find("Menu");
            var btn = (RectTransform)menu.Find("Btn_Shift sheet");
            var sub = pause.Find("Subtitle")?.GetComponent<TMPro.TextMeshProUGUI>();
            for (int size = 0; size <= 2; size++)
            {
                s.TextSize = size;
                yield return Wait(0.4f);
                list.ForceMeshUpdate();
                float k = Settings.TextScale, km = PauseMenu.Instance.MenuScale, kc = PauseMenu.Instance.CardScale;
                var cr = ScreenRect(card);
                var mr = ScreenRect(menu);
                float h = Units(ScreenRect(btn).height);
                bool sizeOk = size == 0 ? km == 1f && kc == 1f : km >= (Wide ? k - 0.01f : 1.2f) && kc >= 1f;
                Check(sizeOk && Mathf.Abs(h - 64f * km) < 1.5f && Inside(cr, ScreenArea) && Inside(mr, ScreenArea) && !cr.Overlaps(mr)
                      && Inside(TextRect(list), cr) && (sub == null || !TextRect(sub).Overlaps(cr)),
                    $"pause menu at {Settings.TextSizes[size]}, {Screen.width}x{Screen.height}: buttons {km:F2}x ({h:F0} units), controls card {kc:F2}x, both on screen and apart, the card's text inside");
                if (size > 0) yield return Shot($"pause_text_{Settings.TextSizes[size].ToLowerInvariant()}");
            }
            BrightnessPanel.Show();
            yield return Wait(0.5f);
            var panel = (RectTransform)Ui.Canvas.transform.Find("Brightness/Panel");
            float scale = panel ? panel.localScale.x : 0f, want = BrightnessPanel.ScaleFor(Screen.width * 1080f / Screen.height, 1.5f);
            Check(panel && Inside(ScreenRect(panel), ScreenArea) && scale > 1.1f && Mathf.Abs(scale - want) < 0.01f && ScreenRect(panel).width <= Screen.width * 0.61f,
                $"the brightness page at Largest is {scale:F2}x, on screen and no wider than 60% of it");
            yield return Shot("brightness_largest");
            FindAnyObjectByType<BrightnessPanel>()?.SendMessage("Close");
            yield return Wait(0.3f);
            s.TextSize = 0;
            yield return Wait(0.3f);
        }

        /// <summary>Settings and both controls pages at Largest: one column that scrolls, walked with the pad and keys; switching text size relays the page.</summary>
        IEnumerator SettingsLargeChecks()
        {
            var s = Settings.Current;
            bool wasPad = GameInput.UsingPad;
            s.TextSize = 2;
            GameObject.Find("Btn_Settings")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            yield return Wait(0.6f);
            vpad.MakeCurrent();
            GameInput.UsingPad = true;
            yield return ListChecks("Settings", () => SettingsPanel.List, "Settings");
            yield return Shot("settings_largest");
            // Text size from the pad, on its own row: the page is laid out again around the same row.
            var es = EventSystem.current;
            for (int i = 0; i < 40 && Selected != "Choice_Text size"; i++) { yield return Press(GamepadButton.DpadDown); yield return Wait(0.1f); }
            RectTransform Row() => es.currentSelectedGameObject ? (RectTransform)es.currentSelectedGameObject.transform : null;
            bool InView()
            {
                var list = SettingsPanel.List;
                return list != null && Row() != null && Inside(ScreenRect(Row()), ScreenRect((RectTransform)list.transform), 2f);
            }
            IEnumerator Settle() { for (float t = 0; t < 1f && SettingsPanel.List != null && !SettingsPanel.List.Settled; t += GameTime.UnscaledDelta) yield return null; }
            yield return Press(GamepadButton.DpadLeft);
            yield return Wait(0.3f);
            yield return Settle();
            float h1 = Row() ? Units(ScreenRect(Row()).height) : 0f;
            Check(s.TextSize == 1 && SettingsPanel.List != null && Selected == "Choice_Text size" && Mathf.Abs(h1 - 70f) < 2.5f && InView(),
                $"d-pad left on Text size relays Settings at Large around the same row ({Selected}, {h1:F0} units, in view {InView()})");
            yield return Press(GamepadButton.DpadLeft);
            yield return Wait(0.4f);
            bool twoCols = Ui.Canvas.transform.Find("Settings/Panel/Right") != null;
            Check(s.TextSize == 0 && SettingsPanel.List == null && twoCols && Selected == "Choice_Text size",
                $"and again at Normal: two columns, the same row selected ({Selected})");
            yield return Press(GamepadButton.DpadRight);
            yield return Wait(0.3f);
            yield return Press(GamepadButton.DpadRight);
            yield return Wait(0.3f);
            yield return Settle();
            Check(s.TextSize == 2 && SettingsPanel.List != null && Selected == "Choice_Text size" && InView(), $"d-pad right twice is back at Largest with the row in view ({Selected})");
            // The wheel scrolls the list.
            var mouse = InputSystem.AddDevice<Mouse>("AutoPilotWheelMouse");
            var list0 = SettingsPanel.List;
            float before = list0.Scroll;
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120f) });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Wait(0.5f);
            Check(list0.Scroll < before - 50f, $"the mouse wheel scrolls the list up ({before:F0} → {list0.Scroll:F0} units)");
            InputSystem.RemoveDevice(mouse);
            vpad.MakeCurrent();
            GameInput.UsingPad = true;
            // The controls pages.
            FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name.StartsWith("Btn_Keyboard, mouse"))?.onClick.Invoke();
            yield return Wait(0.6f);
            yield return ListChecks("Controls", () => ControlsPanel.List, "the keyboard controls page");
            yield return Shot("controls_largest");
            // The arrow keys walk it too, keeping the row in view.
            var rows = ControlsPanel.List.transform.Find("Content").GetComponentsInChildren<UnityEngine.UI.Selectable>().ToList();
            es.SetSelectedGameObject(rows[0].gameObject);
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotListKeyboard");
            vkb.MakeCurrent();
            for (int i = 0; i < 6; i++)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(UnityEngine.InputSystem.Key.DownArrow));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return Wait(0.15f);
            }
            for (float t = 0; t < 1f && !ControlsPanel.List.Settled; t += GameTime.UnscaledDelta) yield return null;
            var sel = Row();
            Check(sel != null && rows.IndexOf(sel.GetComponent<UnityEngine.UI.Selectable>()) == 6 && Inside(ScreenRect(sel), ScreenRect((RectTransform)ControlsPanel.List.transform), 2f),
                $"the down arrow walks the controls list too, the row in view ({Selected})");
            InputSystem.RemoveDevice(vkb);
            vpad.MakeCurrent();
            GameInput.UsingPad = true;
            FindAnyObjectByType<ControlsPanel>()?.SwitchTo(ControlsPanel.PadPage);
            yield return Wait(0.6f);
            yield return ListChecks("Controls", () => ControlsPanel.List, "the controller page");
            FindAnyObjectByType<ControlsPanel>()?.SendMessage("Close");
            yield return Wait(0.3f);
            s.TextSize = 0; // before Settings saves on closing: the profile's later runs start at Normal
            FindAnyObjectByType<SettingsPanel>()?.SendMessage("Close");
            yield return Wait(0.3f);
            GameInput.UsingPad = wasPad;
            yield return Wait(0.3f);
            Check(PauseMenu.IsOpen && !SettingsPanel.IsOpen, "back in the pause menu after the Settings checks");
        }

        /// <summary>
        /// A scrolling list at Largest: rows 1.5x, on screen and apart from each other and the
        /// buttons; the d-pad walks every row from the first, each in view, and on to Done.
        /// </summary>
        IEnumerator ListChecks(string layer, Func<ScrollFollow> getList, string what, string after = "Btn_Done", int minRows = 6, bool mustScroll = true)
        {
            var list = getList();
            if (list == null) { Check(false, $"{what} at Largest is a scrolling list"); yield break; }
            var view = (RectTransform)list.transform;
            var panel = (RectTransform)Ui.Canvas.transform.Find(layer + "/Panel");
            var rows = view.Find("Content").GetComponentsInChildren<UnityEngine.UI.Selectable>().ToList();
            var others = panel.GetComponentsInChildren<UnityEngine.UI.Selectable>().Where(x => !rows.Contains(x)).ToList();
            var vr = ScreenRect(view);
            int overlaps = 0;
            for (int i = 0; i < rows.Count; i++)
                for (int j = i + 1; j < rows.Count; j++)
                    if (ScreenRect((RectTransform)rows[i].transform).Overlaps(ScreenRect((RectTransform)rows[j].transform))) overlaps++;
            var heights = rows.Select(r => Units(ScreenRect((RectTransform)r.transform).height)).ToList();
            Check(rows.Count >= minRows && heights.All(h => Mathf.Abs(h - 84f) < 2.5f) && overlaps == 0 && Inside(ScreenRect(panel), ScreenArea) && Inside(vr, ScreenRect(panel))
                  && others.All(o => !ScreenRect((RectTransform)o.transform).Overlaps(vr)) && (!mustScroll || list.MaxScroll > 0f),
                $"{what} at Largest, {Screen.width}x{Screen.height}: {rows.Count} rows in one {(list.MaxScroll > 0f ? "scrolling " : "")}list ({heights.Min():F0}–{heights.Max():F0} units, Normal 56), {overlaps} overlaps, clear of {others.Count} buttons, on screen");
            var es = EventSystem.current;
            es.SetSelectedGameObject(rows[0].gameObject);
            yield return Wait(0.3f);
            var seen = new System.Collections.Generic.HashSet<string>();
            int outOfView = 0;
            for (int i = 0; i < rows.Count + 2; i++)
            {
                for (float t = 0; t < 1f && !list.Settled; t += GameTime.UnscaledDelta) yield return null;
                var cur = es.currentSelectedGameObject;
                var row = cur ? cur.GetComponent<UnityEngine.UI.Selectable>() : null;
                if (row == null || !rows.Contains(row)) break;
                seen.Add(cur.name);
                if (!Inside(ScreenRect((RectTransform)cur.transform), vr, 2f)) { outOfView++; Log($"list: {cur.name} out of view"); }
                yield return Press(GamepadButton.DpadDown);
                yield return Wait(0.1f);
            }
            Check(seen.Count == rows.Count && outOfView == 0 && Selected == after,
                $"{what}: the d-pad walks all {rows.Count} rows ({seen.Count} seen, {outOfView} out of view) and on to {after.Replace("Btn_", "")} ({Selected})");
            es.SetSelectedGameObject(rows[0].gameObject);
            yield return Wait(0.4f);
        }

        /// <summary>The title menu at each text size: buttons grow from the bottom, clear of the tagline and the footer.</summary>
        IEnumerator TitleTextChecks()
        {
            var s = Settings.Current;
            var title = TitleScreen.Instance.transform;
            var menu = (RectTransform)title.Find("Menu");
            var tag = title.Find("Tagline").GetComponent<TMPro.TextMeshProUGUI>();
            var foot = title.Find("Footer").GetComponent<TMPro.TextMeshProUGUI>();
            var quit = (RectTransform)menu.Find("Btn_Quit");
            for (int size = 0; size <= 2; size++)
            {
                s.TextSize = size;
                yield return Wait(0.3f);
                foot.ForceMeshUpdate();
                float k = Settings.TextScale, h = Units(ScreenRect(quit).height);
                var mr = ScreenRect(menu);
                var tr = TextRect(tag);
                var fr = TextRect(foot);
                // Five buttons grow by the full text size; a sixth (Case file) at Largest grows as far as there's room.
                int buttons = menu.childCount;
                float km = TitleScreen.Instance.MenuScale;
                bool scaleOk = buttons <= 5 ? Mathf.Abs(km - k) < 0.01f : km >= Mathf.Min(k, 1.3f) - 0.01f && km <= k + 0.01f;
                Check(scaleOk && Mathf.Abs(h - 64f * km) < 1.5f && Inside(mr, ScreenArea) && Inside(fr, ScreenArea) && mr.yMax < tr.yMin && mr.yMin > fr.yMax,
                    $"title at {Settings.TextSizes[size]}, {Screen.width}x{Screen.height}, {buttons} buttons: buttons {h:F0} units ({64f * km:F0} expected, {km:F2}×), on screen, below the tagline ({Units(tr.yMin - mr.yMax):F0} units clear) and above the footer ({Units(mr.yMin - fr.yMax):F0} clear)");
                if (size == 2) yield return Shot("title_largest");
            }
            s.TextSize = 0;
            yield return Wait(0.3f);
        }

        /// <summary>Night Select at each text size: every card's writing stays on its card; the grade line grows.</summary>
        IEnumerator NightSelectTextChecks()
        {
            var s = Settings.Current;
            FindAnyObjectByType<NightSelect>()?.SendMessage("Close");
            yield return Wait(0.3f);
            float stats0 = 0f;
            for (int pass = 0; pass <= 3; pass++)
            {
                int size = Mathf.Min(pass, 2);
                bool plain = pass == 3; // Largest again with Handwriting set Plain
                s.TextSize = size;
                s.PlainHandwriting = plain;
                NightSelect.Show();
                yield return Wait(0.6f);
                var ns = FindAnyObjectByType<NightSelect>().transform;
                var cards = Enumerable.Range(1, NightDefs.Count).Select(n => (RectTransform)ns.Find("Night" + n)).Append((RectTransform)ns.Find("Endings")).ToList();
                var off = new System.Collections.Generic.List<string>();
                foreach (var c in cards)
                    foreach (var t in c.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                    {
                        t.ForceMeshUpdate();
                        if (!string.IsNullOrEmpty(t.text) && !Inside(TextRect(t), ScreenRect(c), 3f)) off.Add($"{c.name}/{t.name}");
                    }
                var stats = cards[0].Find("Stats")?.GetComponent<TMPro.TextMeshProUGUI>();
                float fs = stats ? stats.fontSize : 0f;
                if (size == 0) stats0 = fs;
                var sub = ns.Find("Subtitle").GetComponent<TMPro.TextMeshProUGUI>();
                var hands = cards.SelectMany(c => c.GetComponentsInChildren<TMPro.TextMeshProUGUI>()).Where(t => t.name == "Label" || t.name == "List").ToList();
                bool fonts = hands.Count > 0 && hands.All(t => t.font.name.StartsWith(plain ? "FiraSans-Regular" : "Caveat"));
                Check(off.Count == 0 && fonts && Inside(TextRect(sub), ScreenArea) && (size == 0 || fs > stats0 + 0.5f),
                    $"Night Select at {Settings.TextSizes[size]}{(plain ? " with Handwriting Plain" : "")} ({hands.Count} handwritten labels in {hands.FirstOrDefault()?.font.name}): every card's writing stays on its card{(off.Count > 0 ? " (off: " + string.Join(", ", off) + ")" : "")}, grade line {fs:F0} (Normal {stats0:F0})");
                if (size == 2) yield return Shot(plain ? "night_select_largest_plain" : "night_select_largest");
                ns.SendMessage("Close");
                yield return Wait(0.3f);
            }
            s.TextSize = 0;
            s.PlainHandwriting = false;
            NightSelect.Show();
            yield return Wait(0.6f);
        }

        /// <summary>The ending at Largest: the epilogue grows as far as all its lines fit above the stats.</summary>
        void EndingTextChecks()
        {
            var ending = FindAnyObjectByType<EndingScreen>();
            var labels = ending ? Ui.Canvas.transform.Find("Ending").GetComponentsInChildren<TMPro.TextMeshProUGUI>(true) : new TMPro.TextMeshProUGUI[0];
            var epi = labels.FirstOrDefault(l => l.name == "Epilogue");
            var stats = labels.FirstOrDefault(l => l.name == "Stats");
            if (!epi || !stats) { Check(false, "the ending's epilogue and stats are there"); return; }
            var def = Endings.Resolve(Story.State.Clone());
            float need = epi.GetPreferredValues(string.Join("\n", def.Lines), epi.rectTransform.rect.width, 0f).y;
            var er = ScreenRect(epi.rectTransform);
            Check(epi.fontSize > 30.5f && need <= epi.rectTransform.rect.height + 1f && Inside(er, ScreenArea) && er.yMin >= ScreenRect(stats.rectTransform).yMax - 1f,
                $"the ending at Largest: the epilogue is {epi.fontSize:F0} (Normal 30) and all {def.Lines.Count} lines fit its box ({need:F0} of {epi.rectTransform.rect.height:F0} units), above the stats");
        }

        /// <summary>
        /// The pause menu's controls card (night 1, menu open): the bound keys, a rebind showing at
        /// once, pad buttons and PlayStation symbols, no torch before night 2, clear of the menu.
        /// </summary>
        IEnumerator PauseCardChecks()
        {
            var s = Settings.Current;
            var pause = Ui.Canvas.transform.Find("Pause");
            var card = (RectTransform)pause.Find("ControlsCard");
            var list = card.Find("ControlsList").GetComponent<TMPro.TextMeshProUGUI>();
            var menu = (RectTransform)pause.Find("Menu");
            string Row(string what)
            {
                var line = list.text.Split('\n').FirstOrDefault(l => l.Contains(what)) ?? "";
                int a = line.IndexOf("<b>"), b = line.IndexOf("</b>");
                return a >= 0 && b > a ? line.Substring(a + 3, b - a - 3) : null;
            }
            bool wasPad = GameInput.UsingPad;
            GameInput.UsingPad = false;
            yield return Wait(0.4f);
            list.ForceMeshUpdate();
            var cr = ScreenRect(card);
            Check(Inside(cr, ScreenArea) && !cr.Overlaps(ScreenRect(menu)) && !list.isTextOverflowing && Inside(TextRect(list), cr),
                $"the pause menu's controls card is on screen at {Screen.width}x{Screen.height}, clear of the menu, with its text inside");
            Check(Row("Use, pick up") == Controls.Display(Act.Interact) && Row("Clean") == Controls.Display(Act.Use) && Row("Clipboard") == Controls.Display(Act.Clipboard) && Row("UV torch") == null,
                $"the card shows the bound keys (interact {Row("Use, pick up")}, clean {Row("Clean")}, clipboard {Row("Clipboard")}) and no torch on night 1");
            Controls.Set(s, Act.Interact, "<Keyboard>/f");
            yield return Wait(0.4f);
            Check(Row("Use, pick up") == "F", $"after rebinding Interact to F the card shows F ({Row("Use, pick up")})");
            Controls.Set(s, Act.Interact, "<Keyboard>/e");
            Check(Controls.Display(Act.Interact) == "E" && Controls.Display(Act.Torch) == "F", "and the binding is put back");
            var pad = InputSystem.AddDevice<Gamepad>("AutoPilotCardPad");
            pad.MakeCurrent();
            GameInput.UsingPad = true;
            yield return Wait(0.4f);
            string padRow = Row("Use, pick up");
            var ds = InputSystem.AddDevice<UnityEngine.InputSystem.DualShock.DualShock4GamepadHID>("AutoPilotCardDS4");
            ds.MakeCurrent();
            GameInput.UsingPad = true;
            yield return Wait(0.4f);
            string psRow = Row("Use, pick up");
            Check(padRow == "A" && psRow == "✕" && Row("Pick a tool") == "◀ ▶", $"with a pad the card shows its buttons (interact {padRow}; on a DualShock {psRow})");
            yield return Shot("pause_card_playstation");
            InputSystem.RemoveDevice(ds);
            InputSystem.RemoveDevice(pad);
            GameInput.UsingPad = wasPad; // later checks carry on with the device they had
            yield return Wait(0.3f);
        }

        /// <summary>At Largest, the report's grey lines grow and everything stays on the paper.</summary>
        void ReportTextChecks(int n)
        {
            var paper = Ui.Canvas.transform.Find("ShiftReport")?.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Paper");
            if (!paper) { Check(false, $"night {n}: the report's paper is there"); return; }
            var labels = paper.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Where(l => l.enabled && !string.IsNullOrEmpty(l.text)).ToList();
            var pr = ScreenRect(paper);
            var off = labels.Where(l => !Inside(TextRect(l), pr, 3f)).Select(l => l.name).ToList();
            var grey = labels.Where(l => l.name == "NightLine" || l.name == "GradeBreakdown").ToList();
            Check(off.Count == 0 && grey.Count == 2 && grey.All(l => l.fontSize > 22.5f) && Inside(pr, ScreenArea),
                $"night {n} at Largest: the report's text stays on the paper{(off.Count > 0 ? " (off: " + string.Join(", ", off) + ")" : "")} and its grey lines grow ({string.Join(", ", grey.Select(l => $"{l.name} {l.fontSize:F0}"))}, Normal 22)");
            var report = Ui.Canvas.transform.Find("ShiftReport");
            var hint = report.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).FirstOrDefault(l => l.name == "Hint");
            var board = (RectTransform)report.Find("Clipboard");
            var ring = paper.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Ring");
            var night = labels.FirstOrDefault(l => l.name == "NightLine");
            var hr = hint ? TextRect(hint) : Rect.zero;
            Check(hint && Inside(hr, ScreenArea) && !hr.Overlaps(ScreenRect(board)) && (ring == null || night == null || !TextRect(night).Overlaps(ScreenRect(ring))),
                $"night {n} at Largest: the hint to go on is on screen and clear of the clipboard, and the night line clear of the grade stamp");
        }

        /// <summary>At Largest, the chat's messages are 1.5x; scrolled back, new ones wait; it scrolls back to the first.</summary>
        IEnumerator ChatChecks(int n)
        {
            var chat = FindAnyObjectByType<ChatInterlude>();
            if (chat == null) { Log($"night {n}: no morning chat"); yield break; }
            Interstitial.AutoAdvance = false;
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotChatKeyboard");
            vkb.MakeCurrent();
            IEnumerator Key(UnityEngine.InputSystem.Key k)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(k));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return Wait(0.3f);
            }
            var msg = chat.Content.GetComponentsInChildren<TMPro.TextMeshProUGUI>().FirstOrDefault(t => t.name == "Message");
            Check(msg != null && Mathf.Abs(msg.fontSize - 36f) < 0.5f, $"night {n} at Largest: chat messages are 1.5x ({msg?.fontSize:F0}, Normal 24)");
            // Bring messages one at a time until they overflow the window.
            for (int i = 0; i < 40 && !chat.Finished && chat.MaxScroll <= 0f; i++) { Interstitial.AutoAdvance = true; yield return Wait(0.35f); }
            if (chat.MaxScroll > 0f && !chat.Finished)
            {
                yield return Key(UnityEngine.InputSystem.Key.UpArrow);
                int shown = chat.Shown;
                yield return Wait(3f); // longer than the slowest message
                Check(!chat.AtBottom && chat.Shown == shown, $"night {n}: scrolled back, the next message waits ({shown} of {chat.Count} shown, still {chat.Shown} after 3 s)");
                for (int i = 0; i < 20 && !chat.AtBottom; i++) yield return Key(UnityEngine.InputSystem.Key.DownArrow);
                Check(chat.AtBottom, "the down arrow scrolls back to the newest message");
            }
            for (int i = 0; i < 40 && !chat.Finished; i++) { Interstitial.AutoAdvance = true; yield return Wait(0.35f); }
            yield return Wait(0.4f);
            float max = chat.MaxScroll;
            if (max > 0f)
            {
                for (int i = 0; i < 30 && chat.Scroll > 0.5f; i++) yield return Key(i % 2 == 0 ? UnityEngine.InputSystem.Key.UpArrow : UnityEngine.InputSystem.Key.W);
                yield return Wait(0.4f);
                var first = (RectTransform)chat.Content.GetChild(0);
                var view = ScreenRect((RectTransform)chat.Content.parent);
                Check(chat.Scroll < 0.5f && Inside(ScreenRect(first), view, 3f), $"night {n}: W and the up arrow scroll the chat back to its first message ({chat.Count} messages, {max:F0} units more than fit)");
                yield return Shot($"n{n}_chat_scrolled_back");
            }
            else Log($"night {n}: the whole chat fits at Largest ({chat.Count} messages)");
            InputSystem.RemoveDevice(vkb);
        }

        static void PutBack(Holdable item, Vector3 pos, Quaternion rot)
        {
            if (item == null || item.Held) return;
            item.Body.linearVelocity = Vector3.zero;
            item.Body.angularVelocity = Vector3.zero;
            item.transform.SetPositionAndRotation(pos, rot);
            item.Body.position = pos;
            item.Body.rotation = rot;
        }

        /// <summary>
        /// Somewhere to stand within reach of <paramref name="target"/> on <paramref name="surface"/>:
        /// clear of furniture, with a line of sight to that surface. Null if there isn't one.
        /// </summary>
        static Vector3? StandFor(GrimeSurface surface, Vector3 target)
        {
            var flat = new Vector3(target.x, 0, target.z);
            foreach (float d in new[] { 0.9f, 1.2f, 0.7f })
                for (int i = 0; i < 24; i++)
                {
                    float a = i * 15f * Mathf.Deg2Rad;
                    var stand = flat + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * d;
                    if (Physics.CheckCapsule(stand + Vector3.up * 0.4f, stand + Vector3.up * 1.6f, 0.3f, Layers.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                    var eye = stand + Vector3.up * 1.55f;
                    if (!Physics.Raycast(eye, (target - eye).normalized, out var hit, 3.5f, Layers.SolidMask | Layers.GrimeMask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.GetComponent<GrimeSurface>() == surface) return stand;
                }
            return null;
        }

        /// <summary>The shift report explains the grade with the numbers it was computed from, on the paper.</summary>
        void ReportChecks(int n)
        {
            var parts = root.Director.LastGrade;
            string grade = Story.State.ResultFor(n)?.Grade;
            var paper = Ui.Canvas.transform.Find("ShiftReport")?.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Paper");
            var labels = paper ? paper.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true) : new TMPro.TextMeshProUGUI[0];
            string Text(string name) => labels.FirstOrDefault(l => l.name == name)?.text;
            Check(grade == Grading.Grade(parts) && Text("GradeBreakdown") == Grading.Summary(parts),
                $"night {n}: the report explains grade {grade} ({Text("GradeBreakdown")})");
            Check((grade == "S") == (Text("GradeHint") == null),
                $"night {n}: a hint shows only below S ({Text("GradeHint") ?? "none"})");
            if (!paper) return;
            var corners = new Vector3[4];
            paper.GetWorldCorners(corners);
            float bottom = corners[0].y;
            var low = labels.Where(l => l.enabled && !string.IsNullOrEmpty(l.text)).Select(l =>
            {
                var c = new Vector3[4];
                l.rectTransform.GetWorldCorners(c);
                return (l, y: Mathf.Min(c[0].y, c[3].y));
            }).OrderBy(x => x.y).FirstOrDefault();
            Check(low.l == null || low.y >= bottom - 2f, $"night {n}: every line of the report sits on the paper (lowest: {low.l?.name} at {low.y - bottom:F0} px above its edge)");
        }

        /// <summary>
        /// Quit to title asks first: backing out keeps the night, confirming goes to the title,
        /// and Continue starts the night again. Ends with Night 1 running from the start.
        /// </summary>
        IEnumerator QuitChecks()
        {
            var dir = root.Director;
            float elapsed = dir.Elapsed;
            var quit = GameObject.Find("Btn_Quit to title")?.GetComponent<UnityEngine.UI.Button>();
            quit?.onClick.Invoke();
            yield return Wait(0.5f);
            Check(ChoiceMenu.IsOpen && PauseMenu.IsOpen && TitleScreen.Instance == null, "Quit to title asks first");
            yield return Shot("quit_confirm");
            vpad.MakeCurrent();
            yield return Press(GamepadButton.East);
            yield return Wait(0.4f);
            Check(!ChoiceMenu.IsOpen && PauseMenu.IsOpen && TitleScreen.Instance == null && dir.Running && dir.Def.Number == 1 && dir.Elapsed >= elapsed,
                $"pad B backs out with the night still running ({dir.Elapsed:F1}s in)");
            quit?.onClick.Invoke();
            yield return Wait(0.5f);
            ChoiceMenu.AutoPick = 0; // "Quit to title"
            yield return Wait(1.5f);
            Check(TitleScreen.Instance != null && !PauseMenu.IsOpen, "confirming goes to the title");
            bool canContinue = GameObject.Find("Btn_Continue  ·  Night 1") != null;
            Check(canContinue, "the title offers Continue on Night 1");
            yield return TitleTextChecks();
            TitleScreen.Instance?.Begin(1);
            yield return WaitUnblocked(40f);
            Check(dir.Running && dir.Def.Number == 1 && dir.Elapsed < 10f, $"Continue starts Night 1 again from the beginning ({dir.Elapsed:F1}s in)");
        }

        // ---- closing the game mid-night (round 7) --------------------------------------------------

        /// <summary>
        /// A quit request mid-night (Application.Quit goes through the same request as a window
        /// close) is held: the clipboard closes, the night pauses and a question asks; Esc leaves
        /// it paused; confirming quits. On the title nothing is held.
        /// </summary>
        IEnumerator CloseAskChecks()
        {
            var dir = root.Director;
            bool padWas = GameInput.UsingPad;
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotQuitKeyboard");
            int quits = 0;
            var realQuit = GameRoot.QuitNow;
            GameRoot.QuitNow = () => quits++;
            GameRoot.QuitAsks = true;
            Clipboard.Instance.Show();
            yield return Wait(0.5f);
            Application.Quit(); // held: the run goes on
            yield return Wait(0.6f);
            float elapsed = dir.Elapsed;
            Check(ChoiceMenu.IsOpen && PauseMenu.IsOpen && !Clipboard.Instance.Open && dir.Running && dir.Def.Number == 1 && Time.timeScale == 0f,
                "closing the game mid-night asks first: the clipboard closes and the night pauses behind the question");
            yield return Shot("quit_game_confirm");
            vkb.MakeCurrent();
            InputSystem.QueueStateEvent(vkb, new KeyboardState(UnityEngine.InputSystem.Key.Escape));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(vkb, new KeyboardState());
            yield return Wait(0.5f);
            Check(!ChoiceMenu.IsOpen && PauseMenu.IsOpen && dir.Running && Mathf.Abs(dir.Elapsed - elapsed) < 0.05f && quits == 0,
                $"Esc on the question leaves the night paused where it was ({dir.Elapsed:F1}s in) and the game running");
            Application.Quit();
            yield return Wait(0.6f);
            Check(ChoiceMenu.IsOpen && PauseMenu.IsOpen, "asked again over the pause menu");
            ChoiceMenu.AutoPick = 0; // "Quit the game"
            yield return Wait(0.5f);
            Check(quits == 1 && root.WantsToQuit(), $"confirming quits ({quits} quit), and the request then goes through");
            PauseMenu.Instance?.Close();
            root.ToTitle();
            yield return Wait(1.5f);
            Check(TitleScreen.Instance != null && root.WantsToQuit() && !ChoiceMenu.IsOpen, "on the title a quit request isn't held");
            GameRoot.QuitAsks = false;
            GameRoot.QuitNow = realQuit;
            InputSystem.RemoveDevice(vkb);
            GameInput.UsingPad = padWas;
            TitleScreen.Instance?.Begin(1);
            yield return WaitUnblocked(40f);
            Check(dir.Running && dir.Def.Number == 1 && !root.Blocked, "Continue starts Night 1 again");
        }

        // ---- brightness (night 1, from the pause menu) ---------------------------------------------

        IEnumerator BrightnessChecks()
        {
            var s = Settings.Current;
            Check(!BrightnessPanel.IsOpen, "automated runs aren't offered the first-launch brightness page");
            GameObject.Find("Btn_Settings")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            yield return Wait(0.4f);
            FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name.StartsWith("Btn_Brightness"))?.onClick.Invoke();
            yield return Wait(0.6f);
            CanvasGroup Layer(string name) => Ui.Canvas.transform.Find(name)?.GetComponent<CanvasGroup>();
            var settingsLayer = Layer("Settings");
            var pauseLayer = Layer("Pause");
            Check(BrightnessPanel.IsOpen && settingsLayer != null && settingsLayer.alpha < 0.01f && pauseLayer != null && pauseLayer.alpha < 0.01f,
                "Settings opens the brightness page, with the pause menu and Settings hidden behind it");
            s.Brightness = 0.5f;
            yield return Wait(0.3f);
            Check(Mathf.Abs(PostFx.Instance.AppliedGamma) < 1e-4f, $"at the default the image is as designed (gamma offset {PostFx.Instance.AppliedGamma:F3})");
            yield return Shot("brightness_default");
            vpad.MakeCurrent();
            yield return Wait(0.2f);
            Check(Selected == "Slider_Brightness", $"the page selects its slider for the pad ({Selected})");
            for (int i = 0; i < 10; i++) { yield return Press(GamepadButton.DpadRight); yield return Wait(0.05f); }
            yield return Wait(0.3f);
            Check(s.Brightness > 0.95f && PostFx.Instance.AppliedGamma > 0.3f, $"d-pad right turns the brightness up ({BrightnessPanel.Format(s.Brightness)}, gamma offset {PostFx.Instance.AppliedGamma:F2})");
            yield return Shot("brightness_high");
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotBrightnessKeyboard");
            vkb.MakeCurrent();
            for (int i = 0; i < 20; i++)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(UnityEngine.InputSystem.Key.LeftArrow));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return null; yield return null;
                yield return Wait(0.05f);
            }
            yield return Wait(0.3f);
            Check(s.Brightness < 0.05f && PostFx.Instance.AppliedGamma < -0.1f, $"the left arrow turns it down ({BrightnessPanel.Format(s.Brightness)}, gamma offset {PostFx.Instance.AppliedGamma:F2})");
            yield return Shot("brightness_low");
            InputSystem.RemoveDevice(vkb);
            GameObject.Find("Btn_Default")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            yield return Wait(0.3f);
            Check(Mathf.Approximately(s.Brightness, 0.5f) && Mathf.Abs(PostFx.Instance.AppliedGamma) < 1e-4f, "Default puts it back");
            vpad.MakeCurrent();
            yield return Press(GamepadButton.East);
            yield return Wait(0.4f);
            Check(!BrightnessPanel.IsOpen && SettingsPanel.IsOpen && settingsLayer.alpha > 0.99f && pauseLayer.alpha > 0.99f,
                "pad B closes the page and leaves Settings and the pause menu showing");
            Check(s.BrightnessChecked, "closing the page marks it as seen, so it isn't offered at launch again");
            FindAnyObjectByType<SettingsPanel>()?.SendMessage("Close");
            yield return Wait(0.3f);
            Check(PauseMenu.IsOpen, "the pause menu is still open after Settings closes");
        }

        // ---- rebinding keys (night 1) -------------------------------------------------------------

        IEnumerator RemapChecks()
        {
            Log($"window focused: {Application.isFocused}, input background behaviour: {InputSystem.settings.backgroundBehavior}");
            var vkb = InputSystem.AddDevice<Keyboard>("AutoPilotKeyboard");
            vkb.MakeCurrent();
            IEnumerator Key(UnityEngine.InputSystem.Key k)
            {
                InputSystem.QueueStateEvent(vkb, new KeyboardState(k));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(vkb, new KeyboardState());
                yield return Wait(0.3f);
            }
            // Through the real panel: Settings -> Keyboard and mouse -> Interact -> press F.
            SettingsPanel.Show();
            yield return Wait(0.5f);
            FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name.StartsWith("Btn_Keyboard"))?.onClick.Invoke();
            yield return Wait(0.5f);
            Check(ControlsPanel.IsOpen, "Settings opens the keyboard and mouse controls");
            yield return Shot("controls");
            GameObject.Find("Bind_Interact")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            yield return Wait(0.2f);
            Check(ControlsPanel.Listening == Act.Interact, "picking Interact waits for a key");
            yield return Shot("controls_listening");
            yield return Key(UnityEngine.InputSystem.Key.F);
            Check(Controls.PathOf(Settings.Current, Act.Interact) == "<Keyboard>/f" && Controls.PathOf(Settings.Current, Act.Torch) == "<Keyboard>/e",
                $"pressing F binds Interact to F and the torch swaps to E ({Controls.Display(Act.Interact)}, {Controls.Display(Act.Torch)})");
            yield return Shot("controls_rebound");
            FindAnyObjectByType<ControlsPanel>()?.SendMessage("Close");
            yield return Wait(0.3f);
            Check(SettingsPanel.IsOpen, "closing the controls leaves Settings open");
            FindAnyObjectByType<SettingsPanel>()?.SendMessage("Close");
            yield return Wait(0.4f);
            var file = System.IO.Path.Combine(StoryState.Dir, "settings.json");
            Check(System.IO.File.Exists(file) && System.IO.File.ReadAllText(file).Contains("<Keyboard>/f"), "the new binding is saved");

            // Play with the device itself: F uses a light switch, E no longer does.
            var room = root.Office.Rooms["reception"];
            var sw = root.Office.Switches["reception"];
            var toRoom = room.Bounds.center - sw.transform.position; toRoom.y = 0;
            var stand = sw.transform.position + toRoom.normalized * 0.9f; stand.y = 0;
            root.Player.Teleport(stand, 0, 0);
            yield return Aim(sw.transform.position, 0.3f);
            yield return Wait(0.2f);
            bool lit = room.LightsOn;
            var scripted = GameInput.Override;
            GameInput.Override = null;
            yield return Key(UnityEngine.InputSystem.Key.E);
            Check(room.LightsOn == lit, "after rebinding, E no longer uses the switch");
            Check(GameObject.Find("Key_F") != null, "the prompt shows the new key (F)");
            yield return Shot("n1_prompt_rebound");
            yield return Key(UnityEngine.InputSystem.Key.F);
            Check(room.LightsOn != lit, "F uses the switch");
            GameInput.Override = scripted;
            if (room.LightsOn != lit) sw.Toggle();
            Controls.ResetAll(Settings.Current);
            Settings.Save();
            Check(Controls.PathOf(Settings.Current, Act.Interact) == "<Keyboard>/e", "reset brings back the default bindings");
            InputSystem.RemoveDevice(vkb);
            yield return Wait(0.2f);
        }

        // ---- the aim highlight (night 1) ---------------------------------------------------------

        IEnumerator HighlightChecks()
        {
            var hl = AimHighlight.Instance;
            Check(hl != null, "the aim highlight is running");
            if (hl == null) yield break;
            bool OnlyOn(Transform t) => hl.Target == t && hl.ShellCount > 0
                && t.GetComponentsInChildren<MeshRenderer>().Count(r => r.name == "Highlight") == hl.ShellCount;
            // Standing in front of something and looking at it: that thing, and nothing else, glows.
            var cup = FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(t => !t.Binned && t.gameObject.activeInHierarchy)
                .OrderBy(t => (t.transform.position - new Vector3(12.5f, 0, 8f)).sqrMagnitude).FirstOrDefault();
            if (cup != null)
            {
                var stand = cup.transform.position + new Vector3(0, 0, -1.0f); stand.y = 0;
                root.Player.Teleport(stand, 0, 0);
                yield return Aim(cup.transform.position, 0.3f);
                yield return Wait(0.3f);
                Check(OnlyOn(cup.transform), $"looking at the {cup.DisplayName.ToLowerInvariant()} highlights it ({hl.ShellCount} shells, target {hl.Target?.name})");
                // The tool in hand would hide a thing on the floor; lower it for the picture.
                root.Rig.Hidden = true;
                yield return Wait(0.3f);
                yield return Shot("n1_highlight_pickup");
                root.Rig.Hidden = false;
                yield return Aim(cup.transform.position + Vector3.up * 3f, 0.25f);
                yield return Wait(0.1f);
                Check(hl.Target == null && hl.ShellCount == 0, "looking away removes the highlight");
                Settings.Current.AimHighlight = false;
                yield return Aim(cup.transform.position, 0.25f);
                yield return Wait(0.2f);
                Check(hl.Target == null && hl.ShellCount == 0, "with the highlight off in Settings nothing glows");
                Settings.Current.AimHighlight = true;
            }
            // A light switch in the dark closet.
            var sw = root.Office.Switches["closet"];
            if (sw != null)
            {
                var closet = root.Office.Rooms["closet"];
                bool was = closet.LightsOn;
                if (was) sw.Toggle();
                var toRoom = closet.Bounds.center - sw.transform.position; toRoom.y = 0;
                var p = sw.transform.position + toRoom.normalized * 0.9f; p.y = 0;
                root.Player.Teleport(p, 0, 0);
                yield return Aim(sw.transform.position, 0.3f);
                yield return Wait(0.3f);
                Check(OnlyOn(sw.transform), $"looking at a light switch highlights it ({hl.ShellCount} shells)");
                yield return Shot("n1_highlight_switch");
                if (was && !closet.LightsOn) sw.Toggle();
            }
        }

        // ---- camera motion (night 1) --------------------------------------------------------------

        /// <summary>
        /// Camera motion off in Settings (round 9) stops the camera kick a throw or a bump gives
        /// and the field-of-view punch of a reveal, as well as the head bob; on, both happen.
        /// </summary>
        IEnumerator CameraMotionChecks()
        {
            var p = root.Player;
            var s = Settings.Current;
            bool was = s.HeadBob;
            IEnumerator Measure(Action<float, float> got)
            {
                float kick = 0f, fov = 0f;
                p.Kick(1.8f); // a full-charge throw
                p.FovPunch = -4f; // the whiteboard's reveal
                for (float t = 0; t < 0.5f; t += Time.deltaTime)
                {
                    kick = Mathf.Max(kick, Mathf.Abs(p.KickPitch));
                    fov = Mathf.Max(fov, s.Fov - p.Camera.fieldOfView);
                    yield return null;
                }
                got(kick, fov);
                yield return Wait(1.5f);
            }
            float k0 = 0, f0 = 0, k1 = 0, f1 = 0;
            s.HeadBob = false;
            yield return Wait(0.3f);
            yield return Measure((k, f) => { k0 = k; f0 = f; });
            Check(k0 < 0.01f && f0 < 0.05f, $"with Camera motion off, a throw's kick and a reveal's punch leave the camera still (kick {k0:F2}°, punch {f0:F2}°)");
            s.HeadBob = true;
            yield return Measure((k, f) => { k1 = k; f1 = f; });
            Check(k1 > 0.5f && f1 > 1f, $"with it on, they move it (kick {k1:F2}°, punch {f1:F2}°)");
            s.HeadBob = was;
        }

        // ---- never stuck on the last item (night 1) ---------------------------------------------

        IEnumerator ShiftHelperChecks()
        {
            var helper = ShiftHelper.Instance;
            var dir = root.Director;
            Check(helper != null, "the shift helper is running");
            if (helper == null) yield break;
            var cans = FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(t => !t.Binned && t.gameObject.activeInHierarchy).ToList();
            var can = cans.FirstOrDefault(t => t.Kind == TrashKind.Recyclable) ?? cans.FirstOrDefault();
            if (can == null) { Check(false, "night 1 has rubbish left for the shift helper checks"); yield break; }
            root.Player.Teleport(new Vector3(12.2f, 0, 6.2f), 0f, 10f);
            yield return Wait(0.3f);

            IEnumerator Lose(string what, Vector3 at, bool expectRecovered, float wait)
            {
                int before = helper.Recovered;
                can.Body.isKinematic = false;
                can.Body.linearVelocity = Vector3.zero;
                can.Body.position = at;
                can.transform.position = at;
                can.Body.WakeUp();
                helper.Watch(can);
                for (float t = 0; t < wait && helper.Recovered == before; t += Time.deltaTime) yield return null;
                bool recovered = helper.Recovered > before;
                if (expectRecovered)
                    Check(recovered && can.transform.position.y > -0.1f && ShiftHelper.Reachable(can),
                        $"lost rubbish comes back within {wait:F0}s: {what} (now at {can.transform.position})");
                else
                    Check(!recovered, $"rubbish within reach is left alone: {what}");
            }

            // Out of the world, on a ledge above reach, and sealed in a crate on the floor.
            yield return Lose("fell out of the world", new Vector3(12.5f, -3f, 8f), true, 5f);
            yield return Wait(0.5f);
            var test = new GameObject("ShiftHelperTest").transform;
            GameObject Slab(Vector3 c, Vector3 size)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.transform.SetParent(test, false);
                g.transform.position = c;
                g.transform.localScale = size;
                return g;
            }
            Slab(new Vector3(12.5f, 2.35f, 7.5f), new Vector3(0.5f, 0.06f, 0.5f));
            yield return Lose("resting on a ledge at 2.4 m", new Vector3(12.5f, 2.5f, 7.5f), true, 5f);
            Destroy(test.gameObject);
            yield return Wait(0.3f);
            test = new GameObject("ShiftHelperTest").transform;
            var crate = new Vector3(13.0f, 0f, 7.0f);
            Slab(crate + new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.04f, 0.5f));
            Slab(crate + new Vector3(0.25f, 0.25f, 0), new Vector3(0.04f, 0.5f, 0.5f));
            Slab(crate + new Vector3(-0.25f, 0.25f, 0), new Vector3(0.04f, 0.5f, 0.5f));
            Slab(crate + new Vector3(0, 0.25f, 0.25f), new Vector3(0.5f, 0.5f, 0.04f));
            Slab(crate + new Vector3(0, 0.25f, -0.25f), new Vector3(0.5f, 0.5f, 0.04f));
            yield return Wait(0.1f);
            yield return Lose("sealed in a crate", crate + new Vector3(0, 0.12f, 0), true, 5f);
            Destroy(test.gameObject);
            yield return Wait(0.3f);
            // No false alarms: the open floor and under a desk (reachable crouching) stay put.
            yield return Lose("on the open floor", new Vector3(11.6f, 0.15f, 7.4f), false, 4f);
            yield return Lose("under Theo's desk", new Vector3(10.6f, 0.1f, 11.6f), false, 4f);

            // Idle for a minute with rubbish left: everything unfinished glints.
            root.Player.Teleport(new Vector3(12.6f, 0, 6.4f), 0f, 22f);
            var remaining = dir.RequiredRemaining().SelectMany(t => dir.Remaining(t)).Select(r => r.pos).ToList();
            int glints = helper.GlintCount;
            float waited = 0;
            while (helper.GlintCount == glints && waited < helper.StuckAfter + 15f) { waited += Time.deltaTime; yield return null; }
            Check(helper.GlintCount > glints && helper.LastGlintIdle >= helper.StuckAfter, $"after {helper.StuckAfter:F0}s without progress the leftovers glint ({helper.LastGlintIdle:F0}s idle)");
            yield return Wait(0.9f);
            yield return Shot("n1_glint");
            var binned = FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(t => t.Binned).Select(t => t.transform.position).ToList();
            bool exact = helper.LastGlint.Count == remaining.Count
                         && helper.LastGlint.All(p => remaining.Any(r => (r - p).sqrMagnitude < 0.04f))
                         && !helper.LastGlint.Any(p => binned.Any(b => (b - p).sqrMagnitude < 0.01f));
            Check(exact, $"the glint marks exactly the unfinished things ({helper.LastGlint.Count} points, {remaining.Count} expected)");
            // It says where the chimes come from (the nearest few, their rooms nearest first), once.
            var me = root.Player.transform.position;
            var chimed = helper.LastGlint.OrderBy(p => (p - me).sqrMagnitude).Take(ShiftHelper.Chimes).ToList();
            string where = ShiftHelper.WhereCaption(chimed.Count, chimed.Select(p => Room.At(p + Vector3.up * 0.2f)?.DisplayName).ToList());
            var captionText = Ui.Canvas.transform.Find("HUD/Caption")?.GetComponent<TMPro.TextMeshProUGUI>();
            Check(helper.LastWhere == where && where.StartsWith("[") && Hud.Instance.CaptionShowing && captionText != null && captionText.text == where,
                $"the glint's caption says where the chimes are: \"{captionText?.text}\" (expected \"{where}\" from {string.Join(", ", chimed.Select(p => Room.At(p + Vector3.up * 0.2f)?.Id ?? "?"))})");
            yield return Shot("n1_glint_where");
            int glints2 = helper.GlintCount;
            for (float t2 = 0; helper.GlintCount == glints2 && t2 < helper.RepeatEvery + 5f; t2 += Time.deltaTime) yield return null;
            Check(helper.GlintCount > glints2 && helper.LastWhere == null, $"the repeat glint {helper.RepeatEvery:F0}s later doesn't say it again (caption {(helper.LastWhere ?? "none")})");
            Clipboard.Instance.Show();
            yield return Wait(0.7f);
            var sheet = Clipboard.Instance.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t => t.text).FirstOrDefault(t => t.Contains("Bin every bit"));
            Check(sheet != null && sheet.Contains("· bullpen") , "the shift sheet says which rooms still have rubbish");
            yield return Shot("n1_clipboard_where");
            Clipboard.Instance.Close();
            yield return Wait(0.4f);
        }

        // ---- task completion through real components -------------------------------------------

        protected IEnumerator CompleteTasks(int n)
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
        protected static Vector3? ThrowSpot(Bin bin, float range)
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

        /// <summary>
        /// Camera pitch and yaw for which a held item, settled where the hands hold it at charge
        /// <paramref name="c"/> for that aim (a little right of and below the camera), is thrown into
        /// the bin (same maths as <see cref="Hands"/>). Yaw is searched near <paramref name="yaw0"/>.
        /// </summary>
        protected (float pitch, float yaw)? SolveHeldAim(Holdable h, Vector3 camPos, float yaw0, Bin bin, float c)
        {
            var target = bin.transform.position;
            float targetY = target.y + bin.Height, g = -Physics.gravity.y;
            float best = float.MaxValue, bestPitch = 0, bestYaw = yaw0;
            for (float yaw = yaw0 - 10f; yaw <= yaw0 + 10f; yaw += 0.1f)
                for (float pitch = -45f; pitch <= 70f; pitch += 0.1f)
                {
                    var rot = Quaternion.Euler(pitch, yaw, 0);
                    var from = Hands.HoldTarget(h, camPos, rot, c);
                    var v = rot * Vector3.forward * Mathf.Lerp(3.2f, 10f, c * c) + Vector3.up * Mathf.Lerp(0.8f, 1.6f, c);
                    float dy = from.y - targetY, disc = v.y * v.y + 2f * g * dy;
                    if (disc < 0) continue;
                    float t = (v.y + Mathf.Sqrt(disc)) / g;
                    var land = from + new Vector3(v.x, 0, v.z) * t;
                    float miss = new Vector2(land.x - target.x, land.z - target.z).magnitude;
                    if (miss < best) { best = miss; bestPitch = pitch; bestYaw = yaw; }
                }
            return best < 0.05f ? (bestPitch, bestYaw) : null;
        }

        /// <summary>Camera pitch for which a throw at charge <paramref name="c"/> from <paramref name="from"/> drops into the bin (same maths as <see cref="Hands"/>).</summary>
        protected float? SolvePitch(Vector3 from, Bin bin, float c)
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
        protected IEnumerator CleanSurface(GrimeSurface s)
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

        protected IEnumerator Inspect(Action open, InspectChoice choice)
        {
            open();
            float t = 0;
            while (!InspectView.IsOpen && t < 3f) { t += GameTime.UnscaledDelta; yield return null; }
            yield return Wait(ReadTime);
            InspectView.AutoChoice = choice;
            t = 0;
            while (InspectView.IsOpen && t < 3f) { t += GameTime.UnscaledDelta; yield return null; }
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
            while (!ChoiceMenu.IsOpen && t < 3f) { t += GameTime.UnscaledDelta; yield return null; }
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
            while (!ChoiceMenu.IsOpen && t < 3f) { t += GameTime.UnscaledDelta; yield return null; }
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

        protected IEnumerator Deliver(string doc, string person)
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
            while (!ChoiceMenu.IsOpen && t < 3f) { t += GameTime.UnscaledDelta; yield return null; }
            yield return Wait(MenuTime);
            if (person == "auditor" && !auditorTrayShot) { auditorTrayShot = true; yield return Wait(0.3f); yield return Shot("auditor_tray"); }
            ChoiceMenu.AutoPick = index;
            yield return Wait(0.5f);
            Check(Story.State.IsDelivered(doc, person), $"{doc} delivered to {person}");
        }
    }
}
