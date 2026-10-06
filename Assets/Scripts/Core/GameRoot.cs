using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Bootstraps the game after the (empty) Main scene loads and runs the top-level flow:
    /// title → night (title card, play, clock out) → shift report → morning chat → next night → ending.
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public static string[] Args => Environment.GetCommandLineArgs();

        public FirstPersonController Player { get; private set; }
        public CleaningController Cleaning { get; private set; }
        public ToolRig Rig { get; private set; }
        public PrototypeRoom Proto { get; private set; }
        public OfficeBuilder Office { get; private set; }
        public Interactor Interactor { get; private set; }
        public Hands Hands { get; private set; }
        public NightDirector Director { get; private set; }
        public bool InNight => Director != null && Director.Running;

        readonly HashSet<string> blockers = new();
        readonly HashSet<string> cursorBlockers = new();
        public bool Blocked => blockers.Count > 0;
        public string BlockerList => string.Join(",", blockers);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("GameRoot");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameRoot>();
        }

        public static string Arg(string name, int offset = 1)
        {
            var a = Args;
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + offset < a.Length ? a[i + offset] : null;
        }

        public static bool HasArg(string name) => Array.IndexOf(Args, name) >= 0;

        void Awake()
        {
            Layers.ApplyCollisionMatrix();
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            gameObject.AddComponent<GameInput>();
            PostFx.Create();
            AudioDirector.Create();

            bool proto = Arg("-ahScene") == "proto";
            if (proto)
            {
                Proto = PrototypeRoom.Build();
                Player = FirstPersonController.Create(Proto.Spawn, -20f);
            }
            else
            {
                Office = OfficeBuilder.Build();
                var spawn = Office.Anchor("ANCHOR_spawn");
                Player = FirstPersonController.Create(spawn ? spawn.position : Vector3.zero, spawn ? spawn.eulerAngles.y : 0f);
                AudioDirector.Instance.AttachOfficeSources(Office);
            }
            PostFx.ConfigureCamera(Player.Camera);
            Rig = ToolRig.Create(Player.Camera);
            Cleaning = Player.gameObject.AddComponent<CleaningController>();
            Cleaning.Player = Player;
            Cleaning.Rig = Rig;
            Hands = Player.gameObject.AddComponent<Hands>();
            Hands.Player = Player;
            Hands.Cleaning = Cleaning;
            UvTorch.Create(Player);
            Interactor = Player.gameObject.AddComponent<Interactor>();
            Interactor.Player = Player;
            Interactor.Cleaning = Cleaning;
            Player.gameObject.AddComponent<AimHighlight>();
            Player.Footstep += (kind, speed) =>
                Sfx.Play(kind == FloorKind.Carpet ? "step_carpet" : "step_tile", Player.transform.position, 0.3f + speed * 0.25f, kind == FloorKind.Vinyl ? 1.1f : 1f, 0.08f);

            var hud = Hud.Create(Cleaning);
            hud.SetClock("10:00", "PM   MON");
            Cleaning.SurfaceCompleted += srf =>
            {
                hud.Toast($"{srf.DisplayName}  ✓", "Spotless", ToolDefs.Accent(srf.Tool));
                hud.PulseDot();
                Rumble.Pulse(0.04f, 0.22f, 0.09f);
                Events.Raise(GameEvent.SurfaceCleaned, srf.Id);
            };
            Clipboard.Create();
            if (!proto)
            {
                RoomPhotos.Create();
                Director = NightDirector.Create(Office);
                Director.transform.SetParent(transform, false);
                ShiftHelper.Create(Director);
                PlaytestLog.Create(Director).transform.SetParent(transform, false);
            }
        }

        void Start()
        {
            if (HasArg("-ahCapture")) gameObject.AddComponent<CaptureDirector>();
            if (HasArg("-ahAutopilot")) gameObject.AddComponent<AutoPilot>();
            if (HasArg("-ahShowcase")) gameObject.AddComponent<Showcase>();
            if (HasArg("-ahTrailer")) gameObject.AddComponent<Trailer>();
            if (Proto != null) { LockCursor(true); return; }

            if (int.TryParse(Arg("-ahQuality"), out var quality)) Settings.Current.Quality = quality; // automation: measure a preset
            Settings.ApplyGraphics();
            int night = int.TryParse(Arg("-ahNight"), out var n) ? n : 0;
            if (HasArg("-ahFresh")) Story.State = new StoryState();
            else Story.State = StoryState.Load() ?? new StoryState();
            if (night > 0)
            {
                var snap = HasArg("-ahFresh") ? null : StoryState.LoadSnapshot(night);
                if (snap != null) Story.State = snap;
                StartNight(night);
            }
            else ToTitle();
        }

        /// <summary>Back to the title: the current night's office becomes the backdrop.</summary>
        public void ToTitle()
        {
            if (InNight && !Director.Paused) PlaytestLog.Log("quit_to_title", ("open", PlaytestLog.OpenTasks()));
            var saved = StoryState.Load();
            if (saved != null) Story.State = saved;
            int n = Mathf.Clamp(Story.State.Night, 1, NightDefs.Count);
            Director.Begin(n);
            Director.Pause(true);
            foreach (var r in Office.Rooms.Values) r.SetLights(false, true);
            TitleScreen.ShowTitle();
        }

        // =========================================================================================
        // Flow
        // =========================================================================================

        public void StartNight(int n)
        {
            Block(true, false);
            Hud.Instance.SetVisible(false);
            // The card covers the screen first; the (heavy) night setup happens behind it.
            TitleCard.Show(NightDefs.Get(n), () =>
            {
                Block(false, false);
                Hud.Instance.SetVisible(true);
                Director.Pause(false);
                LockCursor(true);
                PlaytestLog.NightStart(NightDefs.Get(n));
                if (n == 1) Tween.Delay(1.2f, () => Hud.Instance.Caption($"Your shift sheet is on the clipboard  ·  {(GameInput.UsingPad ? GameInput.PadGlyph("View") : Controls.Display(Act.Clipboard))}", 4f));
            });
            Tween.Delay(0.15f, () => { Director.Begin(n); Director.Pause(true); });
        }

        public void OnNightEnded(NightDef def, NightResult result)
        {
            PlaytestLog.NightEnd(result, PlaytestLog.OpenTasks());
            Block(true, false);
            Hud.Instance.SetVisible(false);
            ShiftReport.Show(def, result, Director, () =>
            {
                var chat = def.Chat.Where(c => c.When == null || SafeWhen(c, Story.State)).ToList();
                string day = $"{NightDefs.MorningAfter(def.Number)} morning";
                void next()
                {
                    Block(false, false);
                    if (def.Number < NightDefs.Count) StartNight(def.Number + 1);
                    else EndingScreen.Show(Endings.Resolve(Story.State));
                }
                if (chat.Count == 0) next();
                else ChatInterlude.Show(day, chat, next, def.Number < NightDefs.Count ? null : $"Press {GameInput.KeyTag("E")} to continue");
            });
        }

        static bool SafeWhen(ChatLine c, StoryState s)
        {
            try { return c.When(s); }
            catch (Exception e) { Debug.LogException(e); return false; }
        }

        // =========================================================================================
        // Input gating
        // =========================================================================================

        /// <summary>Block gameplay input while a UI owns the screen. cursor: free the mouse for UI.</summary>
        public void SetBlocked(string who, bool blocked, bool cursor = false)
        {
            if (blocked) { blockers.Add(who); if (cursor) cursorBlockers.Add(who); }
            else { blockers.Remove(who); cursorBlockers.Remove(who); }
            GameInput.GameplayEnabled = blockers.Count == 0;
            if (Player) { Player.LookLocked = blockers.Count > 0; Player.MoveLocked = blockers.Count > 0; }
            if (Interactor) Interactor.Locked = blockers.Count > 0;
            LockCursor(cursorBlockers.Count == 0);
        }

        void Block(bool on, bool cursor) => SetBlocked("flow", on, cursor);

        static void LockCursor(bool locked)
        {
            if (HasArg("-ahCapture") || HasArg("-ahAutopilot") || HasArg("-ahShowcase") || HasArg("-ahTrailer")) return;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // =========================================================================================
        // Per-frame: prompts and global keys
        // =========================================================================================

        void Update()
        {
            var hud = Hud.Instance;
            if (hud == null) return;
            var f = GameInput.Frame;
            if (GameInput.Menu.Pause && blockers.Count == 0 && InNight && !PauseMenu.IsOpen) PauseMenu.Show();
            if (blockers.Count == 0 && f.Clipboard && Clipboard.Instance && !Clipboard.Instance.Open && Director != null) Clipboard.Instance.Show();

            if (Hands != null && Hands.Holding != null)
            {
                hud.Prompt(("LMB", "Throw (hold)"), ("E", Hands.PromptText()), ("Q", "Drop"));
                return;
            }
            var t = Cleaning != null ? Cleaning.Target : null;
            var focus = Interactor != null ? Interactor.Focus : null;
            if (focus != null)
            {
                hud.Prompt(("E", focus.Prompt(Interactor)));
                return;
            }
            if (t != null && Cleaning.InReach && !Cleaning.Suspended)
            {
                string verb = !string.IsNullOrEmpty(t.Verb) ? t.Verb : t.Tool switch
                {
                    ToolKind.Vacuum => "Vacuum",
                    ToolKind.Squeegee => "Squeegee",
                    ToolKind.Mop => "Mop",
                    _ => t.HasGhost && t.Spec.GhostScrubbable ? (t.Done ? "Scrub the stain" : "Erase") : "Wipe",
                };
                if (t.Tool == ToolKind.Squeegee || t.Tool == ToolKind.Cloth && t.HasGhost && t.Done)
                    hud.Prompt(("RMB", "Spray"), ("LMB", verb));
                else if (t.Tool == ToolKind.Cloth)
                    hud.Prompt(("LMB", verb), ("RMB", "Spray"));
                else hud.Prompt(("LMB", verb));
            }
            else hud.Prompt();
        }
    }
}
