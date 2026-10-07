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

        /// <summary>Run by the AutoPilot, a capture, the showcase or the trailer recorder.</summary>
        public static bool Automated => HasArg("-ahCapture") || HasArg("-ahAutopilot") || HasArg("-ahShowcase") || HasArg("-ahTrailer");

        /// <summary>
        /// Pause the night when the window loses focus or the pad in use goes away. Off for
        /// automated runs, whose windows are rarely in front; the AutoPilot turns it on to test it.
        /// </summary>
        public static bool AutoPause = !Automated;

        /// <summary>
        /// Ask before the game closes in the middle of a night (the window's close button, Alt+F4):
        /// a night is only saved when it ends. Off for automated runs; the AutoPilot turns it on to test it.
        /// </summary>
        public static bool QuitAsks = !Automated;
        /// <summary>How the game quits once the player has said so (automation can watch it instead).</summary>
        public static Action QuitNow = Application.Quit;
        bool quitConfirmed;

        /// <summary>Quitting would lose a night being played (not the title, a report, a chat or the ending).</summary>
        public static bool QuitLosesNight(bool asks, bool confirmed, bool inNight, bool paused, bool onTitle) =>
            asks && !confirmed && inNight && !paused && !onTitle;

        void Awake()
        {
            Layers.ApplyCollisionMatrix();
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            gameObject.AddComponent<GameInput>();
            gameObject.AddComponent<FrameWatch>();
            Application.wantsToQuit += WantsToQuit;
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

        void OnDestroy() => Application.wantsToQuit -= WantsToQuit;

        /// <summary>Unity asks before quitting (a window close, Alt+F4, Application.Quit): hold it if a night would be lost.</summary>
        public bool WantsToQuit()
        {
            if (!QuitLosesNight(QuitAsks, quitConfirmed, InNight, Director != null && Director.Paused, TitleScreen.Instance != null))
            {
                Debug.Log("[Quit] request goes through");
                return true;
            }
            Debug.Log("[Quit] request held: tonight would be lost, asking first");
            AskBeforeQuit();
            return false;
        }

        /// <summary>The night pauses (the clipboard closes first) and the same kind of question as Quit to title asks.</summary>
        void AskBeforeQuit()
        {
            PlaytestLog.Log("quit_asked");
            if (blockers.Count == 1 && blockers.Contains("clipboard") && Clipboard.Instance) Clipboard.Instance.Close();
            if (blockers.Count == 0 && !PauseMenu.IsOpen) PauseMenu.Show();
            ChoiceMenu.Show("Quit the game?", "Tonight starts over from 10 PM next time. Earlier nights are saved.", new List<ChoiceMenu.Option>
            {
                new("Quit the game", null, () => { quitConfirmed = true; QuitNow(); }),
            });
        }

        void OnEnable() => UnityEngine.InputSystem.InputSystem.onDeviceChange += OnDeviceChange;
        void OnDisable() => UnityEngine.InputSystem.InputSystem.onDeviceChange -= OnDeviceChange;

        void OnApplicationFocus(bool focus)
        {
            if (!focus) AutoPauseNow("focus");
        }

        void OnDeviceChange(UnityEngine.InputSystem.InputDevice device, UnityEngine.InputSystem.InputDeviceChange change)
        {
            if (device is not UnityEngine.InputSystem.Gamepad) return;
            if (change is not (UnityEngine.InputSystem.InputDeviceChange.Removed or UnityEngine.InputSystem.InputDeviceChange.Disconnected)) return;
            if (GameInput.UsingPad) AutoPauseNow("pad");
        }

        /// <summary>
        /// Open the pause menu if a night is being played and nothing but the clipboard is up (it
        /// closes). Documents, choices and menus stay as they are: they're already waiting.
        /// </summary>
        public void AutoPauseNow(string why)
        {
            if (!AutoPause || !InNight || Director.Paused || PauseMenu.IsOpen) return;
            if (blockers.Count == 1 && blockers.Contains("clipboard") && Clipboard.Instance) Clipboard.Instance.Close();
            if (blockers.Count > 0) return;
            PlaytestLog.Log("auto_pause", ("why", why));
            PauseMenu.Show();
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
            else
            {
                ToTitle();
                if (BrightnessPanel.OfferOnLaunch(Settings.Current, Automated)) BrightnessPanel.Show();
            }
        }

        /// <summary>Back to the title: the current night's office becomes the backdrop.</summary>
        public void ToTitle()
        {
            quitConfirmed = false;
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
                if (n == 1) Tween.Delay(1.2f, () => { if (!(Clipboard.Instance && Clipboard.Instance.Open)) Hud.Instance.Caption($"Your shift sheet is on the clipboard  ·  {GameInput.ActGlyph(Act.Clipboard)}", 4f); });
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
                var chat = (Story.State.ChatFor(def.Number)?.Lines ?? def.MorningChat(Story.State)).Where(i => i >= 0 && i < def.Chat.Count).Select(i => def.Chat[i]).ToList();
                string day = $"{NightDefs.MorningAfter(def.Number)} morning";
                void next()
                {
                    Block(false, false);
                    if (def.Number < NightDefs.Count) StartNight(def.Number + 1);
                    else EndingScreen.Show(Endings.Resolve(Story.State));
                }
                if (chat.Count == 0) next();
                else ChatInterlude.Show(day, chat, next, def.Number < NightDefs.Count ? null : $"Press {GameInput.MenuKeyTag("E")} to continue");
            });
        }

        // =========================================================================================
        // Input gating
        // =========================================================================================

        /// <summary>Block gameplay input while a UI owns the screen. cursor: free the mouse for UI.</summary>
        public void SetBlocked(string who, bool blocked, bool cursor = false)
        {
            if (blocked) { blockers.Add(who); if (cursor) cursorBlockers.Add(who); }
            else
            {
                // The key that closed the last overlay (Esc, Tab) mustn't also pause or reopen the clipboard.
                if (blockers.Remove(who) && blockers.Count == 0) unblockedFrame = Time.frameCount;
                cursorBlockers.Remove(who);
            }
            GameInput.GameplayEnabled = blockers.Count == 0;
            if (Player) { Player.LookLocked = blockers.Count > 0; Player.MoveLocked = blockers.Count > 0; }
            if (Interactor) Interactor.Locked = blockers.Count > 0;
            LockCursor(cursorBlockers.Count == 0);
        }

        int unblockedFrame = -1;

        void Block(bool on, bool cursor) => SetBlocked("flow", on, cursor);

        static void LockCursor(bool locked)
        {
            if (Automated) return;
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
            bool fresh = Time.frameCount != unblockedFrame;
            if (GameInput.Menu.Pause && blockers.Count == 0 && fresh && InNight && !PauseMenu.IsOpen) PauseMenu.Show();
            if (blockers.Count == 0 && fresh && f.Clipboard && Clipboard.Instance && !Clipboard.Instance.Open && Director != null) Clipboard.Instance.Show();

            if (Hands != null && Hands.Holding != null)
            {
                // Toggled: one press starts the charge and the next throws.
                string throwText = !Settings.Current.ToggleUse ? "Throw (hold)" : Hands.Charging ? "Throw" : "Aim a throw";
                // E uses a door, switch or chair under the reticle; otherwise it places what's held.
                var use = Interactor != null && Interactor.HandsFreeFocus ? Interactor.Focus.Prompt(Interactor) : Hands.PromptText();
                if (use != null) hud.Prompt(("LMB", throwText), ("E", use), ("Q", "Drop"));
                else hud.Prompt(("LMB", throwText), ("Q", "Drop"));
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
