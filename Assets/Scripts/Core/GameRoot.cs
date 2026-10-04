using System;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Bootstraps the game after the (empty) Main scene loads. Everything is built from code and
    /// Resources, so there is no scene wiring to break.
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public static string[] Args => Environment.GetCommandLineArgs();

        public FirstPersonController Player { get; private set; }
        public CleaningController Cleaning { get; private set; }
        public ToolRig Rig { get; private set; }
        public PrototypeRoom Proto { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("GameRoot");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameRoot>();
        }

        void Update()
        {
            var hud = Hud.Instance;
            if (hud == null || Cleaning == null) return;
            var t = Cleaning.Target;
            if (t != null && Cleaning.InReach && !Cleaning.Suspended)
            {
                string verb = t.Tool switch
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

            Proto = PrototypeRoom.Build();
            Player = FirstPersonController.Create(Proto.Spawn, -20f);
            PostFx.ConfigureCamera(Player.Camera);
            Rig = ToolRig.Create(Player.Camera);
            Cleaning = Player.gameObject.AddComponent<CleaningController>();
            Cleaning.Player = Player;
            Cleaning.Rig = Rig;
            Player.Footstep += (kind, speed) =>
                Sfx.Play(kind == FloorKind.Carpet ? "step_carpet" : "step_tile", Player.transform.position, 0.35f + speed * 0.25f, 1f, 0.08f);

            var hud = Hud.Create(Cleaning);
            hud.SetClock("10:00", "PM   MON");
            Cleaning.SurfaceCompleted += srf =>
            {
                hud.Toast($"{srf.DisplayName}  ✓", "Spotless", ToolDefs.Accent(srf.Tool));
                hud.PulseDot();
            };

            if (HasArg("-ahCapture")) gameObject.AddComponent<CaptureDirector>();
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
