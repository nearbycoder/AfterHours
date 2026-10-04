using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Trailer footage: <c>-ahTrailer &lt;dir&gt; &lt;reel&gt;</c>, where reel is <c>title</c> or <c>n1</c>…<c>n7</c>.
    /// Each reel starts from the save snapshot of that night (run the AutoPilot with the same
    /// <c>-ahProfile</c> first) and films short scripted clips. Every clip is written to
    /// <c>dir/&lt;clip&gt;/NNNNNN.jpg</c> at a fixed 30 fps game clock, with the game's own audio (music
    /// muted, it's laid in by <c>Tools/trailer/make_trailer.py</c>) in <c>dir/&lt;clip&gt;/audio.wav</c>.
    /// </summary>
    public class Trailer : AutoPilot
    {
        protected override string ArgName => "-ahTrailer";
        protected override bool PadChecks => false;
        const int Fps = 30;

        bool rec;
        string clipDir;
        int frame;
        readonly List<Task> pending = new();
        readonly List<float> audio = new();
        int channels = 2;
        bool audioOk;

        protected override IEnumerator Run()
        {
            QualitySettings.vSyncCount = 0;
            Time.captureFramerate = Fps;
            channels = AudioSettings.speakerMode switch
            {
                AudioSpeakerMode.Mono => 1, AudioSpeakerMode.Quad => 4, AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6, AudioSpeakerMode.Mode7point1 => 8, _ => 2,
            };
            audioOk = AudioRenderer.Start();
            // The trailer gets its music bed in the edit, so the game's own music stays out of the clips.
            Settings.Current.MusicVolume = 0f;
            Settings.Current.HeadBob = false;
            Settings.NotifyChanged();
            StartCoroutine(Recorder());
            Debug.Log($"[Trailer] reel {scenario}, {Screen.width}x{Screen.height}, audio {(audioOk ? "on" : "unavailable")} {AudioSettings.outputSampleRate} Hz x{channels}");

            yield return Wait(2.5f);
            if (scenario == "title") yield return TitleReel();
            else if (scenario.Length == 2 && scenario[0] == 'n' && int.TryParse(scenario.Substring(1), out int n)) yield return NightReel(n);
            else Debug.LogError("[Trailer] unknown reel " + scenario);

            Debug.Log("[Trailer] done");
            yield return Wait(0.3f);
            Application.Quit();
        }

        // ---- recording ---------------------------------------------------------------------------

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
                var path = Path.Combine(clipDir, $"{frame++:000000}.jpg");
                pending.Add(Task.Run(() => File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, w, h, 0, 95))));
                if (pending.Count > 16) { pending[0].Wait(); pending.RemoveAt(0); }
            }
        }

        /// <summary>Film <paramref name="body"/> as one clip.</summary>
        IEnumerator Film(string name, IEnumerator body)
        {
            clipDir = Path.Combine(dir, name);
            if (Directory.Exists(clipDir)) Directory.Delete(clipDir, true);
            Directory.CreateDirectory(clipDir);
            frame = 0;
            audio.Clear();
            // The body's first step (usually a teleport) reaches the camera a frame later, so start
            // filming on the next frame.
            bool done = false;
            StartCoroutine(Then(body, () => done = true));
            yield return null;
            rec = true;
            while (!done) yield return null;
            // One more frame so the last state is on film, then stop.
            yield return null;
            rec = false;
            Task.WaitAll(pending.ToArray());
            pending.Clear();
            WriteWav(Path.Combine(clipDir, "audio.wav"));
            Debug.Log($"[Trailer] clip {name}: {frame} frames ({frame / (float)Fps:F1}s)");
        }

        static IEnumerator Then(IEnumerator body, System.Action after)
        {
            yield return body;
            after();
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

        // ---- camera helpers ----------------------------------------------------------------------

        FirstPersonController P => root.Player;

        void Cam(Vector3 pos, float yaw, float pitch) => P.Teleport(pos, yaw, pitch);

        /// <summary>Glide the camera (no footsteps or bob) between two framings.</summary>
        IEnumerator Dolly(Vector3 a, float yawA, float pitchA, Vector3 b, float yawB, float pitchB, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / seconds);
                P.Teleport(Vector3.Lerp(a, b, k), Mathf.LerpAngle(yawA, yawB, k), Mathf.Lerp(pitchA, pitchB, k));
                yield return null;
            }
            P.Teleport(b, yawB, pitchB);
        }

        /// <summary>Stand about <paramref name="d"/> metres from <paramref name="target"/>, preferably on the side of <paramref name="from"/>, already looking at it.</summary>
        void StandAt(Vector3 target, float d, Vector3 from)
        {
            P.Teleport(SpotFacing(target, d, from), 0, 0);
            LookAtNow(target);
        }

        /// <summary>A free spot within reach of target with a clear view of it, nearest the preferred side.</summary>
        static Vector3 SpotFacing(Vector3 target, float d, Vector3 from)
        {
            var flat = new Vector3(target.x, 0, target.z);
            var pref = from - flat; pref.y = 0;
            if (pref.sqrMagnitude < 1e-4f) pref = Vector3.back;
            var want = flat + pref.normalized * d;
            if (Free(want) && Visible(want, target)) return want;
            Vector3? best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10f * Mathf.Deg2Rad;
                foreach (float r in new[] { d, d + 0.25f, d - 0.2f, d + 0.5f })
                {
                    var stand = flat + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Max(0.45f, r);
                    if (!Free(stand) || !Visible(stand, target)) continue;
                    float score = (stand - want).magnitude + Mathf.Abs(r - d) * 1.5f;
                    if (score < bestScore) { bestScore = score; best = stand; }
                }
            }
            return best ?? want;
        }

        static bool Free(Vector3 stand) =>
            !Physics.CheckCapsule(stand + Vector3.up * 0.4f, stand + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && Physics.Raycast(stand + Vector3.up * 0.3f, Vector3.down, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        static bool Visible(Vector3 stand, Vector3 target)
        {
            var eye = new Vector3(stand.x, FirstPersonController.StandEye, stand.z);
            var dv = target - eye;
            return !Physics.Raycast(eye, dv.normalized, out var hit, dv.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                   || (hit.point - target).magnitude < 0.3f;
        }

        /// <summary>Yaw and pitch that look from a standing spot at a point.</summary>
        static (float yaw, float pitch) LookFrom(Vector3 stand, Vector3 target)
        {
            var d = target - new Vector3(stand.x, FirstPersonController.StandEye, stand.z);
            return (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Asin(d.normalized.y) * Mathf.Rad2Deg);
        }

        /// <summary>A free standing spot in front of a vertical surface (either side), facing it.</summary>
        void FaceSurface(GrimeSurface s, float d, Vector2 uv)
        {
            var c = s.UvToWorld(uv);
            var n = s.transform.up; n.y = 0; n.Normalize();
            foreach (var sign in new[] { 1f, -1f })
            {
                var stand = c + n * d * sign; stand.y = 0;
                if (Physics.CheckCapsule(stand + Vector3.up * 0.4f, stand + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                P.Teleport(stand, 0, 0);
                LookAtNow(c);
                return;
            }
            P.Teleport(c + n * d, 0, 0);
            LookAtNow(c);
        }

        void Lights(bool on, params string[] rooms)
        {
            foreach (var r in rooms.Length > 0 ? rooms : NightDirector.AllRooms)
                if (root.Office.Rooms.TryGetValue(r, out var room) && room.LightsOn != on)
                {
                    if (root.Office.Switches.TryGetValue(r, out var sw)) sw.Toggle(); else room.SetLights(on, true);
                }
        }

        IEnumerator UntilInspect(float timeout = 4f)
        {
            for (float t = 0; !InspectView.IsOpen && t < timeout; t += GameTime.UnscaledDelta) yield return null;
        }

        IEnumerator UntilChoice(float timeout = 4f)
        {
            for (float t = 0; !ChoiceMenu.IsOpen && t < timeout; t += GameTime.UnscaledDelta) yield return null;
        }

        IEnumerator UntilClosed(float timeout = 4f)
        {
            for (float t = 0; (InspectView.IsOpen || ChoiceMenu.IsOpen) && t < timeout; t += GameTime.UnscaledDelta) yield return null;
        }

        /// <summary>Clean on camera for a while, then let the brush maths finish (gleam, ding, toast).</summary>
        IEnumerator CleanOnCamera(GrimeSurface s, Rect r, int passes, float perPass, bool horizontal, int rounds = 1)
        {
            for (int i = 0; i < rounds && !s.Done; i++) yield return Sweep(s, r, passes, perPass, horizontal, true, false);
            if (!s.Done) yield return CleanSurface(s);
        }

        // ---- title reel --------------------------------------------------------------------------

        IEnumerator TitleReel()
        {
            Story.State = StoryState.Load() ?? Story.State;
            // A fresh title so the logo animates in on camera.
            yield return RebuildTitle();
            var ui = TitleScreen.Instance.GetComponent<CanvasGroup>();
            yield return Film("title_drift", Hold(14f, () => ui.alpha = 0f));
            yield return RebuildTitle();
            yield return Film("title_menu", Wait(4.5f));
            yield return Film("title_nightselect", NightSelectClip());
            yield return Film("title_settings", SettingsClip());

            // The four endings, from the audit route's final state and three variations of it.
            var final = Story.State.Clone();
            var variants = new (string id, StoryState s)[]
            {
                ("audit", final.Clone()),
                ("cleanbooks", Variant(final, st => st.SetFate("red_folder", Fate.Shredded))),
                ("loose", Variant(final, st => st.SetFate("red_folder", Fate.Kept))),
                ("spotless", new StoryState { Night = 8 }),
            };
            foreach (var (id, s) in variants)
            {
                var e = Endings.Resolve(s);
                Check(e.Id == id, $"crafted state resolves to {id} (got {e.Id})");
                Story.State = s;
                yield return Film("ending_" + id, EndingClip(e, id == "audit" ? 12f : 5.5f));
            }
            Story.State = final;
            Story.State.Save();
        }

        static StoryState Variant(StoryState s, System.Action<StoryState> change)
        {
            var c = s.Clone();
            change(c);
            return c;
        }

        IEnumerator RebuildTitle()
        {
            if (TitleScreen.Instance) Destroy(TitleScreen.Instance.gameObject);
            yield return null;
            TitleScreen.ShowTitle();
            yield return null;
        }

        IEnumerator Hold(float seconds, System.Action each)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) { each(); yield return null; }
        }

        IEnumerator NightSelectClip()
        {
            NightSelect.Show();
            yield return Wait(4f);
            Click("Btn_Back");
            yield return Wait(0.6f);
        }

        IEnumerator SettingsClip()
        {
            SettingsPanel.Show();
            yield return Wait(3.5f);
            Settings.Current.MusicVolume = 0f;
            Click("Btn_Done");
            Settings.Current.MusicVolume = 0f;
            Settings.NotifyChanged();
            yield return Wait(0.6f);
        }

        static void Click(string name) => GameObject.Find(name)?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();

        IEnumerator EndingClip(EndingDef e, float seconds)
        {
            EndingScreen.Show(e);
            yield return Wait(seconds);
            for (int i = 0; i < 10 && FindAnyObjectByType<EndingScreen>() != null; i++) { Interstitial.AutoAdvance = true; yield return Wait(0.3f); }
            Interstitial.AutoAdvance = false;
            yield return Wait(1.2f);
        }

        // ---- night reels -------------------------------------------------------------------------

        IEnumerator NightReel(int n)
        {
            var snap = StoryState.LoadSnapshot(n);
            Check(snap != null, $"night {n} snapshot exists (run the AutoPilot with this profile first)");
            Story.State = snap ?? new StoryState();
            // The night's punch card, then the closet.
            yield return Film($"n{n}_card", CardClip(n));
            var dir = root.Director;
            Lights(true, dir.Def.Rooms.ToArray());
            yield return Wait(0.8f);
            switch (n)
            {
                case 1: yield return Night1(); break;
                case 2: yield return Night2(); break;
                case 3: yield return Night3(); break;
                case 4: yield return Night4(); break;
                case 5: yield return Night5(); break;
                case 6: yield return Night6(); break;
                case 7: yield return Night7(); break;
            }
        }

        IEnumerator CardClip(int n)
        {
            TitleScreen.Instance?.Begin(n);
            yield return Wait(1.0f);
            yield return WaitUnblocked(30f);
            yield return Wait(1.6f);
        }

        // Night 1: the cleaning verbs, the first secret, the remote session, the shift report.
        IEnumerator Night1()
        {
            var o = root.Office;
            var dir = root.Director;
            yield return Film("n1_clipboard", ClipboardClip());

            // Reception: lights off again so the switch can bring them up on camera.
            Lights(false, "reception");
            yield return Wait(0.6f);
            yield return Film("n1_lights", LightsClip("reception", new Vector3(13.2f, 0, 2.0f), new Vector3(12.0f, 1.0f, 1.0f)));

            // From Dana's side of the counter, looking down at the coffee rings.
            var desk = o.Surfaces["desk_reception"];
            var rings = desk.UvToWorld(new Vector2(0.36f, 0.5f));
            StandAt(rings, 0.8f, rings + new Vector3(0, 0, 1.5f));
            yield return Wait(0.4f);
            Log($"wipe from {P.transform.position} at {rings}, desk size {desk.Size}");
            yield return Film("n1_wipe", Seq(Wait(0.8f), CleanOnCamera(desk, new Rect(0.2f, 0.25f, 0.32f, 0.5f), 5, 0.75f, true), AimHold(rings, 0.4f, 2.4f)));

            yield return ThrowPrep();
            yield return Film("n1_throw", ThrowClip());

            var floor = o.Surfaces["floor_bullpen"];
            Cam(new Vector3(12.0f, 0, 6.2f), 0f, 35f);
            yield return Aim(floor.UvToWorld(new Vector2(0.42f, 0.2f)), 0.1f);
            yield return Wait(0.3f);
            yield return Film("n1_vacuum", Seq(Wait(0.4f), Sweep(floor, new Rect(0.3f, 0.12f, 0.25f, 0.2f), 5, 0.85f, false, true, false), AimHold(floor.UvToWorld(new Vector2(0.43f, 0.24f)), 0.4f, 1.4f)));

            yield return Film("n1_key", KeyClip());
            yield return Film("n1_note", NoteClip());
            yield return Film("n1_putback", PutBackClip());
            yield return Film("n1_tray", TrayClip("theo_note", "priya", new Vector3(9.0f, 0, 13.4f)));

            // Everything else, quickly and off camera.
            yield return CompleteTasks(1);
            for (float t = 0; dir.Elapsed < 63f && t < 120f; t += GameTime.UnscaledDelta) yield return null;
            yield return Film("n1_remote", RemoteClip());
            yield return Film("n1_report", ReportClip(1));
        }

        IEnumerator Seq(params IEnumerator[] steps) { foreach (var s in steps) yield return s; }

        IEnumerator AimHold(Vector3 at, float aim, float hold)
        {
            yield return Aim(at, aim);
            yield return Wait(hold);
        }

        IEnumerator ClipboardClip()
        {
            Cam(new Vector3(16.3f, 0, 2.6f), -120f, 4f);
            yield return Wait(0.5f);
            input.ClipboardOnce = true;
            yield return Wait(3.2f);
            Clipboard.Instance.Close();
            yield return Wait(0.6f);
        }

        IEnumerator LightsClip(string room, Vector3 stand, Vector3 reveal)
        {
            var sw = root.Office.Switches[room];
            StandAt(sw.transform.position, 0.85f, stand);
            yield return Wait(0.7f);
            input.InteractOnce = true;
            yield return Wait(0.5f);
            yield return Aim(reveal, 1.4f);
            yield return Wait(1.2f);
        }

        TrashItem cup;
        Bin throwBin;

        /// <summary>Off camera: pick up a cup and take it to a throwing spot a few metres from the bin.</summary>
        IEnumerator ThrowPrep()
        {
            var o = root.Office;
            cup = FindObjectsByType<TrashItem>(FindObjectsSortMode.None).Where(t => t.Kind == TrashKind.General && !t.Binned && t.gameObject.activeInHierarchy)
                .OrderBy(t => (t.transform.position - new Vector3(13.9f, 0, 4.05f)).sqrMagnitude).FirstOrDefault();
            if (cup == null) { Check(false, "a cup to throw"); yield break; }
            StandAt(cup.transform.position, 1.0f, new Vector3(13.4f, 0, 2.4f));
            yield return Wait(0.3f);
            input.InteractOnce = true;
            yield return Wait(0.5f);
            var anchor = o.Anchor("BIN_trash_reception_1").position;
            throwBin = root.Director.Furniture.Bins.OrderBy(b => (b.transform.position - anchor).sqrMagnitude).First();
            var spot = ThrowSpot(throwBin, 3.8f) ?? ThrowSpot(throwBin, 3.2f) ?? ThrowSpot(throwBin, 2.6f);
            P.Teleport(spot ?? anchor + new Vector3(-2.2f, 0, 0), P.Yaw, 0);
            yield return null;
            LookAtNow(throwBin.transform.position + Vector3.up * (throwBin.Height + 0.6f));
            P.Yaw += 25f;
            yield return Wait(0.6f);
        }

        IEnumerator ThrowClip()
        {
            if (cup == null || throwBin == null) yield break;
            var bin = throwBin;
            yield return Wait(0.4f);
            yield return Aim(bin.transform.position + Vector3.up * bin.Height, 0.7f);
            input.Use = true;
            for (float t = 0; t < 1.0f; t += Time.deltaTime)
            {
                if (SolvePitch(cup.transform.position, bin, 1f) is float p) P.Pitch = Mathf.MoveTowards(P.Pitch, p, 40f * Time.deltaTime);
                yield return null;
            }
            for (int f = 0; f < 12; f++)
            {
                if (SolvePitch(cup.transform.position, bin, 1f) is float p) P.Pitch = p;
                yield return null;
            }
            input.Use = false;
            for (float t = 0; t < 2f && !cup.Binned; t += Time.deltaTime) yield return null;
            Check(cup.Binned, "trailer: the throw lands");
            yield return Wait(1.8f);
        }

        IEnumerator KeyClip()
        {
            var floor = root.Office.Surfaces["floor_bullpen"];
            Cam(new Vector3(10.45f, 0, 10.05f), 0f, 55f);
            input.Crouch = true;
            yield return Wait(0.6f);
            for (int i = 0; i < 4 && GameObject.Find("key") == null; i++)
            {
                yield return SweepWorld(floor, new Vector3(9.95f, 0, 11.3f + i * 0.2f), new Vector3(10.9f, 0, 11.3f + i * 0.2f), 0.6f);
                if (GameObject.Find("key") != null) break;
                yield return SweepWorld(floor, new Vector3(10.9f, 0, 11.4f + i * 0.2f), new Vector3(9.95f, 0, 11.4f + i * 0.2f), 0.6f);
            }
            input.Use = false;
            yield return Wait(0.7f);
            var key = GameObject.Find("key");
            if (key != null) yield return Aim(key.transform.position, 0.5f);
            yield return Wait(0.6f);
            input.Crouch = false;
            if (key != null) key.GetComponent<KeyPickup>().Interact(null);
            yield return Wait(1.6f);
        }

        IEnumerator NoteClip()
        {
            var note = root.Director.Ctx.Get("theo_note");
            if (note == null) { Check(false, "Theo's note is there"); yield break; }
            StandAt(note.transform.position, 0.9f, new Vector3(10.8f, 0, 9.4f));
            P.Pitch = 20f;
            yield return Wait(0.4f);
            yield return Aim(note.transform.position, 0.5f);
            yield return Wait(0.5f);
            input.InteractOnce = true;
            yield return UntilInspect();
            yield return Wait(3.0f);
            InspectView.AutoChoice = InspectChoice.Keep;
            yield return Wait(1.6f);
        }

        IEnumerator PutBackClip()
        {
            var photo = root.Director.Ctx.Get("theo_photo");
            var res = photo != null ? photo.GetComponent<Resettable>() : null;
            if (res == null) { Check(false, "Theo's photo frame is out of place"); yield break; }
            StandAt(photo.transform.position, 0.95f, new Vector3(10.6f, 0, 9.6f));
            yield return Wait(0.5f);
            yield return Aim(photo.transform.position, 0.3f);
            input.InteractOnce = true;
            yield return Wait(0.8f);
            yield return Aim(res.HomePos + Vector3.up * 0.05f, 0.8f);
            yield return Wait(0.9f);
            input.InteractOnce = true;
            yield return Wait(1.0f);
            // Tuck Theo's chair in.
            var chair = root.Director.Furniture.Chairs.TryGetValue("theo", out var c) ? c : null;
            if (chair != null && !chair.Tucked)
            {
                yield return Aim(chair.transform.position + Vector3.up * 0.6f, 0.6f);
                yield return Wait(0.3f);
                input.InteractOnce = true;
                yield return Wait(1.3f);
            }
        }

        IEnumerator TrayClip(string doc, string person, Vector3 from)
        {
            var tray = root.Director.Furniture.Trays[person];
            int index = Story.State.Inventory.IndexOf(doc);
            if (index < 0) { Check(false, $"{doc} in the pocket"); yield break; }
            StandAt(tray.transform.position, 0.95f, from);
            yield return Wait(0.6f);
            tray.Interact(null);
            yield return UntilChoice();
            yield return Wait(2.2f);
            ChoiceMenu.AutoPick = index;
            yield return Wait(1.8f);
            Check(Story.State.IsDelivered(doc, person), $"trailer: {doc} delivered to {person}");
        }

        IEnumerator RemoteClip()
        {
            var dir = root.Director;
            var m = dir.Furniture.Monitors["walt"];
            Cam(new Vector3(13.4f, 0, 8.6f), 10f, 8f);
            LookAtNow(m.transform.position + Vector3.up * 0.3f);
            yield return Wait(1.2f);
            // Lock up: every switch, closet last.
            foreach (var r in dir.Def.Rooms.Concat(new[] { "closet" }))
                if (root.Office.Rooms.TryGetValue(r, out var room) && room.LightsOn && root.Office.Switches.TryGetValue(r, out var sw)) { sw.Toggle(); yield return Wait(0.12f); }
            yield return Wait(2.6f);
            Check(m.On && m.ScreenDoc == "screen_remote", "trailer: the remote session wakes the monitor");
            // Walk up to it.
            var screen = m.transform.position + Vector3.up * 0.35f;
            var stand = screen + m.transform.forward * 1.0f; stand.y = 0;
            var (yaw, pitch) = LookFrom(stand, screen);
            yield return Dolly(P.transform.position, P.Yaw, P.Pitch, stand, yaw, pitch, 2.6f);
            yield return Wait(1.6f);
        }

        IEnumerator ReportClip(int n)
        {
            var clock = FindAnyObjectByType<PunchClock>();
            StandAt(clock.transform.position, 0.9f, new Vector3(16.0f, 0, 2.5f));
            yield return Wait(0.6f);
            clock.Interact(null);
            yield return Wait(0.6f);
            if (ChoiceMenu.IsOpen) ChoiceMenu.AutoPick = 0;
            for (float t = 0; !Interstitial.AnyOpen && t < 10f; t += GameTime.UnscaledDelta) yield return null;
            yield return Wait(9.5f);
            Interstitial.AutoAdvance = true;
            yield return Wait(8.5f);
        }

        // Night 2: foam and squeegee, mop, UV torch.
        IEnumerator Night2()
        {
            var ctx = root.Director.Ctx;
            var win = ctx.Surface("win_break_2");
            FaceSurface(win, 1.35f, new Vector2(0.5f, 0.5f));
            yield return Wait(0.5f);
            yield return Film("n2_foam", Seq(Wait(0.5f),
                Sweep(win, new Rect(0.08f, 0.18f, 0.84f, 0.64f), 4, 0.8f, true, false, true),
                AimHold(win.UvToWorld(new Vector2(0.5f, 0.5f)), 0.4f, 2.2f)));
            yield return Film("n2_squeegee", Seq(
                Sweep(win, new Rect(0.04f, 0.08f, 0.92f, 0.84f), 4, 0.75f, false, true, false),
                FinishOnCamera(win), AimHold(win.UvToWorld(new Vector2(0.5f, 0.5f)), 0.3f, 1.8f)));

            var floor = ctx.Surface("floor_break");
            if (floor != null)
            {
                // The coffee spill by the table (world 3.0, 4.4) and the footprints across it.
                var spill = floor.UvToWorld(new Vector2(0.43f, 0.23f));
                StandAt(spill, 1.6f, spill + new Vector3(1.3f, 0, 1.0f));
                yield return Wait(0.4f);
                yield return Film("n2_mop", Seq(Wait(0.5f), Sweep(floor, new Rect(0.3f, 0.1f, 0.27f, 0.26f), 6, 0.8f, true, true, false),
                    AimHold(spill, 0.5f, 1.8f)));
            }

            MugPrep();
            yield return Wait(0.3f);
            yield return Film("n2_mugs", MugClip());

            // Walt's locker hands over the torch later in the night; the trailer skips ahead.
            Story.State.Set("has_uv_torch");
            Lights(false);
            yield return Wait(1.2f);
            Cam(new Vector3(9.4f, 0, 8.5f), -95f, 2f);
            yield return Wait(0.4f);
            yield return Film("n2_uv", UvClip());
        }

        IEnumerator FinishOnCamera(GrimeSurface s)
        {
            if (!s.Done) yield return CleanSurface(s);
        }

        Resettable mug;

        /// <summary>Off camera: stand by a stray mug.</summary>
        void MugPrep()
        {
            mug = Resettable.All.Where(r => r.Group == "dishrack" && !r.AtHome && r.gameObject.activeInHierarchy)
                .OrderBy(r => (r.transform.position - new Vector3(2.5f, 0, 5.5f)).sqrMagnitude).FirstOrDefault();
            if (mug != null) StandAt(mug.transform.position, 0.9f, mug.transform.position + new Vector3(0.6f, 0, -0.6f));
        }

        IEnumerator MugClip()
        {
            if (mug == null) { Check(false, "a mug to put away"); yield break; }
            yield return Wait(0.4f);
            yield return Aim(mug.transform.position, 0.3f);
            input.InteractOnce = true;
            yield return Wait(0.6f);
            var home = mug.HomeAnchor;
            var stand = SpotFacing(home, 0.85f, P.transform.position);
            var (yaw, pitch) = LookFrom(stand, home);
            yield return Dolly(P.transform.position, P.Yaw, P.Pitch, stand, yaw, pitch, 1.6f);
            yield return Wait(0.8f);
            input.InteractOnce = true;
            yield return Wait(1.4f);
            Check(mug.AtHome, "trailer: the mug goes in the rack");
        }

        IEnumerator UvClip()
        {
            yield return Wait(0.5f);
            input.TorchOnce = true;
            yield return Wait(0.8f);
            yield return Aim(new Vector3(7.08f, 1.2f, 8.2f), 1.0f);
            yield return Wait(1.0f);
            yield return Aim(new Vector3(7.08f, 1.15f, 9.6f), 1.6f);
            yield return Wait(1.0f);
            input.TorchOnce = true;
            yield return Wait(0.3f);
        }

        // Night 3: the whiteboard (the trailer moment).
        IEnumerator Night3()
        {
            var wb = root.Director.Ctx.Surface("whiteboard_conf");
            Cam(new Vector3(2.6f, 0, 10.4f), 180f, 5f);
            yield return Aim(wb.UvToWorld(new Vector2(0.5f, 0.5f)), 0.1f);
            yield return Wait(0.6f);
            yield return Film("n3_whiteboard", Seq(Wait(1.2f),
                Sweep(wb, new Rect(0.04f, 0.06f, 0.92f, 0.88f), 6, 0.8f, true, true, false),
                Sweep(wb, new Rect(0.04f, 0.06f, 0.92f, 0.88f), 6, 0.8f, true, true, false),
                FinishOnCamera(wb), AimHold(wb.UvToWorld(new Vector2(0.5f, 0.5f)), 0.5f, 1.0f),
                Dolly(P.transform.position, P.Yaw, P.Pitch, new Vector3(2.6f, 0, 10.75f), 180f, 4f, 3.0f), Wait(1.5f)));
            Check(wb.GhostWasRevealed, "trailer: the ghost diagram is revealed");
            // A wide of the conference room for the montage.
            Cam(new Vector3(6.3f, 0, 15.3f), -140f, 12f);
            yield return Wait(0.4f);
            yield return Film("n3_conference", Dolly(new Vector3(6.3f, 0, 15.3f), -140f, 12f, new Vector3(5.6f, 0, 14.4f), -150f, 9f, 4f));
        }

        // Night 4: Marian's office.
        IEnumerator Night4()
        {
            var ctx = root.Director.Ctx;
            Lights(true, "office");
            Cam(new Vector3(18.7f, 0, 15.2f), 140f, 12f);
            yield return Wait(0.5f);
            yield return Film("n4_office", Dolly(new Vector3(18.7f, 0, 15.2f), 140f, 12f, new Vector3(19.4f, 0, 14.4f), 125f, 14f, 4f));

            var rub = GrimeSurface.All.FirstOrDefault(s => s.Id == "notepad_rub");
            var pad = ctx.Get("notepad")?.GetComponent<ScriptedUse>();
            if (rub != null)
            {
                var c = rub.UvToWorld(new Vector2(0.5f, 0.5f));
                StandAt(c, 0.7f, P.transform.position);
                yield return Wait(0.4f);
                yield return Film("n4_rubbing", Seq(Wait(0.4f), CleanOnCamera(rub, new Rect(0.08f, 0.1f, 0.84f, 0.8f), 5, 0.7f, true), Wait(1.2f),
                    pad != null ? Inspected(() => pad.Interact(null), InspectChoice.Keep, 3.2f) : Wait(0.1f)));
            }

            var env = ctx.Get("envelope")?.GetComponent<ScriptedUse>();
            if (env != null)
            {
                StandAt(env.transform.position, 0.8f, P.transform.position);
                yield return Wait(0.3f);
                yield return Film("n4_envelope", EnvelopeClip(env));
            }

            var bag = ctx.Furniture.Named.TryGetValue("shredder_office", out var sgo) ? sgo.GetComponent<ScriptedUse>() : null;
            if (bag != null)
            {
                StandAt(bag.transform.position + Vector3.up * 0.6f, 1.0f, P.transform.position);
                yield return Wait(0.3f);
                yield return Film("n4_bag", Seq(Wait(0.4f), Chosen(() => bag.Interact(null), 1, 2.4f)));
            }
        }

        IEnumerator Inspected(System.Action open, InspectChoice choice, float hold)
        {
            open();
            yield return UntilInspect();
            yield return Wait(hold);
            InspectView.AutoChoice = choice;
            yield return Wait(1.2f);
        }

        IEnumerator Chosen(System.Action open, int pick, float hold)
        {
            open();
            yield return UntilChoice();
            yield return Wait(hold);
            ChoiceMenu.AutoPick = pick;
            yield return Wait(1.2f);
        }

        IEnumerator EnvelopeClip(ScriptedUse env)
        {
            yield return Wait(0.4f);
            env.Interact(null);
            yield return UntilInspect();
            yield return Wait(2.6f);
            InspectView.AutoChoice = InspectChoice.Close;
            yield return UntilChoice();
            yield return Wait(2.2f);
            ChoiceMenu.AutoPick = 1; // leave it on the desk
            yield return Wait(1.2f);
        }

        // Night 5: the party mess and the shred puzzle.
        IEnumerator Night5()
        {
            var ctx = root.Director.Ctx;
            Cam(new Vector3(8.2f, 0, 6.2f), 35f, 14f);
            yield return Wait(0.6f);
            yield return Film("n5_party", Dolly(new Vector3(8.2f, 0, 6.2f), 35f, 14f, new Vector3(9.4f, 0, 7.4f), 50f, 18f, 4.5f));
            Cam(new Vector3(6.3f, 0, 15.3f), -140f, 16f);
            yield return Wait(0.5f);
            yield return Film("n5_party_conf", Dolly(new Vector3(6.3f, 0, 15.3f), -140f, 16f, new Vector3(5.5f, 0, 14.6f), -130f, 20f, 3.5f));

            var box = ctx.Get("shred_bag_box");
            if (box == null) { Check(false, "the kept shredder bag is on the closet table"); yield break; }
            StandAt(box.transform.position, 0.9f, new Vector3(16.2f, 0, 2.6f));
            yield return Wait(0.4f);
            yield return Film("n5_puzzle", PuzzleClip(box));
        }

        IEnumerator PuzzleClip(GameObject box)
        {
            yield return Wait(0.5f);
            box.GetComponent<ScriptedUse>().Interact(null);
            for (float t = 0; !ShredPuzzle.IsOpen && t < 4f; t += GameTime.UnscaledDelta) yield return null;
            yield return Wait(1.6f);
            // Swap strips into order through the real buttons, one move at a time.
            var strips = Enumerable.Range(0, 32).Select(i => GameObject.Find("Strip" + i)).TakeWhile(g => g != null).ToList();
            int count = strips.Count;
            for (int slot = 0; slot < count && ShredPuzzle.IsOpen; slot++)
            {
                var bySlot = strips.OrderBy(g => ((RectTransform)g.transform).anchoredPosition.x).ToList();
                if (bySlot[slot] == strips[slot]) continue;
                bySlot[slot].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return Wait(0.32f);
                strips[slot].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return Wait(0.42f);
            }
            for (float t = 0; ShredPuzzle.IsOpen && t < 5f; t += GameTime.UnscaledDelta) yield return null;
            yield return UntilInspect();
            yield return Wait(2.8f);
            InspectView.AutoChoice = InspectChoice.Keep;
            yield return Wait(1.2f);
            Check(Story.State.FateOf("reconstructed_invoice") == Fate.Kept, "trailer: the shred puzzle rebuilds the invoice");
        }

        // Night 6: archive boxes, the voicemail, the auditor's tray.
        IEnumerator Night6()
        {
            var ctx = root.Director.Ctx;
            var box = ctx.Get("archive_1");
            if (box != null)
            {
                StandAt(box.transform.position + Vector3.up * 0.3f, 1.4f, new Vector3(13.6f, 0, 3.2f));
                yield return Wait(0.4f);
                yield return Film("n6_archive", Seq(Wait(1.0f), Inspected(() => box.GetComponent<ScriptedUse>().Interact(null), InspectChoice.Close, 2.6f), Wait(0.4f)));
            }
            if (ctx.Furniture.Named.TryGetValue("phone_reception", out var phone))
            {
                StandAt(phone.transform.position, 0.8f, new Vector3(11.8f, 0, 2.6f));
                yield return Wait(0.4f);
                yield return Film("n6_voicemail", Seq(Wait(0.6f), Inspected(() => phone.GetComponent<ScriptedUse>().Interact(null), InspectChoice.Close, 3.4f)));
            }
            // Pick up the ledger so the auditor's tray has something new to take.
            var ledger = ctx.Get("payment_ledger")?.GetComponent<EvidenceItem>();
            if (ledger != null) { ledger.Interact(null); yield return UntilInspect(); yield return Wait(0.3f); InspectView.AutoChoice = InspectChoice.Keep; yield return Wait(0.8f); }
            var key = Story.State.Inventory.FirstOrDefault(id => Docs.Get(id)?.Key == true);
            if (key != null && ctx.Furniture.Trays.ContainsKey("auditor"))
                yield return Film("n6_auditor", TrayClip(key, "auditor", new Vector3(4.0f, 0, 11.5f)));
        }

        // Night 7: storm, the jammed shredder, the red folder, the ending.
        IEnumerator Night7()
        {
            var ctx = root.Director.Ctx;
            // Facing the bullpen windows and the city in the rain.
            var win = ctx.Surface("win_bullpen_2");
            if (win != null) FaceSurface(win, 3.2f, new Vector2(0.5f, 0.55f)); else Cam(new Vector3(9.0f, 0, 7.0f), 90f, 2f);
            P.Pitch = -2f;
            yield return Wait(0.5f);
            yield return Film("n7_storm", StormClip());

            var shred = ctx.Furniture.Shredders["office"];
            Cam(new Vector3(23.0f, 0, 10.8f), 180f, 30f);
            yield return Aim(shred.transform.position + Vector3.up * 0.6f, 0.1f);
            yield return Wait(0.4f);
            yield return Film("n7_jam", Seq(Wait(1.2f), Inspected(() => shred.Interact(null), InspectChoice.Keep, 3.4f)));
            Check(Story.State.FateOf("red_folder") == Fate.Kept, "trailer: the red folder is pulled from the jam");

            yield return Film("n7_folder_tray", TrayClip("red_folder", "auditor", new Vector3(4.0f, 0, 11.5f)));

            // Finish the night and film the ending.
            yield return CompleteTasks(7);
            Lights(false);
            yield return Wait(1.0f);
            yield return Film("n7_end", EndOfGameClip());
        }

        /// <summary>Thunder: the power stutters twice, the picture flickers.</summary>
        IEnumerator StormClip()
        {
            yield return Wait(1.4f);
            Sfx.Play("thunder", null, 0.95f, 0.92f, 0f, AudioBus.Ambience);
            PostFx.Instance?.Flicker(1f);
            var lit = root.Office.Rooms.Values.Where(r => r.LightsOn).ToList();
            foreach (var (on, hold) in new[] { (false, 0.12f), (true, 0.2f), (false, 0.45f), (true, 0f) })
            {
                foreach (var r in lit) r.SetLights(on, true);
                yield return Wait(hold);
            }
            yield return Wait(2.6f);
        }

        IEnumerator EndOfGameClip()
        {
            var clock = FindAnyObjectByType<PunchClock>();
            StandAt(clock.transform.position, 0.9f, new Vector3(16.0f, 0, 2.5f));
            yield return Wait(0.4f);
            clock.Interact(null);
            yield return Wait(0.6f);
            if (ChoiceMenu.IsOpen) ChoiceMenu.AutoPick = 0;
            for (float t = 0; !Interstitial.AnyOpen && t < 10f; t += GameTime.UnscaledDelta) yield return null;
            yield return Wait(6.5f);
            Interstitial.AutoAdvance = true;          // report → chat
            yield return Wait(5.5f);
            for (int i = 0; i < 40 && FindAnyObjectByType<ChatInterlude>() != null; i++) { Interstitial.AutoAdvance = true; yield return Wait(0.25f); }
            for (float t = 0; FindAnyObjectByType<EndingScreen>() == null && t < 20f; t += GameTime.UnscaledDelta) yield return null;
            yield return Wait(16f);
        }
    }
}
