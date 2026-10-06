using System;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Smart-tool cleaning: aims from the camera, picks the tool the surface needs, sweeps the
    /// brush between frames and drives every bit of feedback (rig animation, particles, loops,
    /// completion dings).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CleaningController : MonoBehaviour
    {
        public FirstPersonController Player;
        public ToolRig Rig;
        public ToolKind Equipped { get; private set; } = ToolKind.Cloth;
        public ToolKind Pinned { get; private set; } = ToolKind.None;
        public GrimeSurface Target { get; private set; }
        public Vector3 ContactPoint { get; private set; }
        public Vector3 ContactNormal { get; private set; } = Vector3.up;
        public bool InReach { get; private set; }
        public bool Cleaning { get; private set; }
        public float Intensity { get; private set; }       // 0..1 smoothed "how much is happening"
        public bool Suspended { get; set; }                  // hands busy (holding an item, inspecting)

        /// <summary>Raised when any surface is finished by the player.</summary>
        public event Action<GrimeSurface> SurfaceCompleted;
        public event Action<ToolKind> ToolChanged;

        GrimeSurface strokeSurface;
        Vector2 lastUv;
        float dwell, comboTimer;
        int comboStep;
        LoopVoice clothLoop, vacuumLoop, suckLoop, squeakLoop, mopLoop, sprayLoop;
        float removedSmoothed;
        float particleBudget;
        Vector2 strokeDirSurface;
        bool vacuumOn;

        static readonly int[] Ladder = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };

        void Start()
        {
            clothLoop = Sfx.Loop("cloth_loop", transform);
            vacuumLoop = Sfx.Loop("vacuum_loop", transform);
            suckLoop = Sfx.Loop("vacuum_suck", transform);
            squeakLoop = Sfx.Loop("squeegee_loop", transform);
            mopLoop = Sfx.Loop("mop_loop", transform);
            sprayLoop = Sfx.Loop("spray_loop", transform);
            foreach (var s in GrimeSurface.All) Hook(s);
        }

        public void HookAll()
        {
            foreach (var s in GrimeSurface.All) Hook(s);
        }

        public void Hook(GrimeSurface s)
        {
            s.Completed -= OnSurfaceCompleted;
            s.Completed += OnSurfaceCompleted;
        }

        public void Pin(ToolKind k)
        {
            Pinned = Pinned == k ? ToolKind.None : k;
            if (Pinned != ToolKind.None) Equip(Pinned);
        }

        static readonly ToolKind[] CycleOrder = { ToolKind.None, ToolKind.Cloth, ToolKind.Vacuum, ToolKind.Squeegee, ToolKind.Mop };

        /// <summary>
        /// Wheel or pad d-pad: step the pinned tool through automatic, cloth, vacuum, squeegee and
        /// mop (automatic lets the tool follow the surface again).
        /// </summary>
        public void Cycle(int dir)
        {
            int i = System.Array.IndexOf(CycleOrder, Pinned);
            var next = CycleOrder[((i < 0 ? 0 : i) + (dir > 0 ? 1 : CycleOrder.Length - 1)) % CycleOrder.Length];
            Pinned = next;
            if (next != ToolKind.None) Equip(next);
            else Sfx.Play("tool_swap", null, 0.35f, 1.2f);
            Hud.Instance?.ToolNote(next == ToolKind.None ? "Automatic tool" : ToolDefs.Name(next) + "  ·  pinned");
        }

        void Equip(ToolKind k)
        {
            if (Equipped == k) return;
            Equipped = k;
            Rig.Show(k);
            Sfx.Play("tool_swap", null, 0.5f);
            ToolChanged?.Invoke(k);
        }

        void Update()
        {
            var input = GameInput.Frame;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (input.ToolSlot > 0)
                Pin(input.ToolSlot switch { 1 => ToolKind.Cloth, 2 => ToolKind.Vacuum, 3 => ToolKind.Squeegee, _ => ToolKind.Mop });
            if (!Suspended && (Mathf.Abs(input.Scroll) > 0.1f || input.ToolCycle != 0))
                Cycle(input.ToolCycle != 0 ? input.ToolCycle : input.Scroll > 0 ? -1 : 1);

            Aim();

            // Smart tool: when not mid-stroke, the tool follows the surface you look at.
            if (!Suspended && Pinned == ToolKind.None && !input.Use && Target != null && Target.Tool != Equipped)
            {
                dwell += dt;
                if (dwell > 0.12f) Equip(Target.Tool);
            }
            else dwell = 0f;
            if (!Suspended && Pinned == ToolKind.None && input.UseDown && Target != null) Equip(Target.Tool);

            float removed = 0f;
            Cleaning = false;
            bool spraying = false;
            bool matches = Target != null && Target.Tool == Equipped && InReach && !Suspended;

            if (matches && input.Spray && (Equipped == ToolKind.Squeegee || Equipped == ToolKind.Cloth))
            {
                spraying = true;
                if (Target.WorldToUv(ContactPoint, out var suv))
                {
                    Target.Spray(suv, Equipped == ToolKind.Squeegee ? 0.26f : 0.16f, dt * (Equipped == ToolKind.Squeegee ? 14f : 8f));
                    if (Rig.SprayPuff(dt)) Fx.Burst(FxKind.Foam, ContactPoint + ContactNormal * 0.05f, ContactNormal, 3, new Color(0.9f, 0.95f, 1f, 0.8f), 0.25f, 1f, 0.08f);
                }
            }

            if (matches && input.Use && !spraying)
            {
                if (Target.WorldToUv(ContactPoint, out var uv))
                {
                    if (strokeSurface != Target) { strokeSurface = Target; lastUv = uv; }
                    var deltaM = new Vector2((uv.x - lastUv.x) * Target.Size.x, (uv.y - lastUv.y) * Target.Size.y);
                    float speed = deltaM.magnitude / dt;
                    if (deltaM.sqrMagnitude > 1e-8f) strokeDirSurface = Vector2.Lerp(strokeDirSurface, deltaM.normalized, 0.35f);
                    var brush = ToolDefs.For(Equipped);
                    removed = Target.Stroke(lastUv, uv, brush, dt, speed);
                    if (Equipped == ToolKind.Cloth && Target.HasGhost && Target.Done)
                        removed += Target.ScrubGhost(uv, brush.Radius, dt * 2.2f * Mathf.Clamp(speed / 0.5f, 0.3f, 1.6f));
                    lastUv = uv;
                    Cleaning = true;
                    Rig.Scrub(speed, Target.transform.TransformDirection(new Vector3(strokeDirSurface.x, 0, strokeDirSurface.y)));
                }
            }
            else strokeSurface = null;

            // Feedback
            removedSmoothed = Mathf.Lerp(removedSmoothed, removed / Mathf.Max(dt, 1e-4f), 1f - Mathf.Exp(-dt * 10f));
            float speedNow = Cleaning ? Rig.ScrubSpeed : 0f;
            Intensity = Mathf.Lerp(Intensity, Cleaning ? Mathf.Clamp01(0.35f + speedNow * 0.8f) : 0f, 1f - Mathf.Exp(-dt * 12f));
            Emit(removed, dt);
            UpdateLoops(input, spraying, speedNow);
            Rig.Tick(this, dt);

            comboTimer -= dt;
        }

        void Aim()
        {
            Target = null;
            InReach = false;
            var cam = Player.Camera.transform;
            var ray = new Ray(cam.position, cam.forward);
            if (Physics.Raycast(ray, out var hit, 4.2f, Layers.SolidMask | Layers.GrimeMask, QueryTriggerInteraction.Ignore))
            {
                var g = hit.collider.GetComponent<GrimeSurface>();
                if (g != null && !g.Done || g != null && g.HasGhost && g.Spec.GhostScrubbable && !g.GhostWasScrubbed)
                {
                    // Contact is on the overlay plane itself.
                    var plane = new Plane(g.Normal, g.transform.position);
                    plane.Raycast(ray, out float enter);
                    var p = ray.GetPoint(enter);
                    Target = g;
                    ContactPoint = p;
                    ContactNormal = g.Normal;
                    InReach = enter <= ToolDefs.Reach(g.Tool);
                    if (g.Tool is ToolKind.Vacuum or ToolKind.Mop)
                    {
                        var flat = p - Player.transform.position; flat.y = 0;
                        InReach &= flat.magnitude < 2.6f;
                    }
                    return;
                }
                ContactPoint = hit.point;
                ContactNormal = hit.normal;
            }
            else
            {
                ContactPoint = ray.GetPoint(3f);
                ContactNormal = -ray.direction;
            }
        }

        float popTimer;

        void Emit(float removed, float dt)
        {
            if (!Cleaning) return;
            if (Equipped == ToolKind.Vacuum && removed > 0.00015f)
            {
                // Debris rattling up the wand.
                popTimer -= dt;
                if (popTimer <= 0f)
                {
                    popTimer = UnityEngine.Random.Range(0.04f, 0.13f) / Mathf.Clamp(removed * 600f, 0.6f, 2.5f);
                    Sfx.Play("suck_pop", ContactPoint, UnityEngine.Random.Range(0.12f, 0.3f), UnityEngine.Random.Range(0.8f, 1.4f), 0f, AudioBus.Sfx, 0.03f);
                }
            }
            particleBudget += Mathf.Min(removed * 900f, 40f) * dt * 60f * 0.02f + (Rig.ScrubSpeed > 0.2f ? dt * 6f : 0f);
            int n = Mathf.FloorToInt(particleBudget);
            if (n <= 0) return;
            particleBudget -= n;
            var p = ContactPoint + ContactNormal * 0.01f;
            switch (Equipped)
            {
                case ToolKind.Cloth:
                    Fx.Burst(FxKind.Foam, p, ContactNormal, n, new Color(0.92f, 0.95f, 1f, 0.85f), 0.45f, 1.1f, 0.06f);
                    if (removed > 0.0005f) Fx.Burst(FxKind.Dust, p, ContactNormal, Mathf.Max(1, n / 3), new Color(0.6f, 0.55f, 0.48f, 0.35f), 0.2f, 1f, 0.06f);
                    break;
                case ToolKind.Vacuum:
                    // Dust gets pulled up into the nozzle.
                    for (int i = 0; i < n; i++)
                    {
                        var off = UnityEngine.Random.insideUnitSphere * 0.28f; off.y = 0;
                        Fx.Burst(FxKind.Dust, p + off, -off * 3f + Vector3.up * 0.4f, 1, new Color(0.55f, 0.52f, 0.47f, 0.4f), 1.2f, 0.1f);
                    }
                    break;
                case ToolKind.Squeegee:
                    Fx.Burst(FxKind.Drops, p - Vector3.up * 0.05f, Vector3.down + ContactNormal * 0.4f, n, new Color(0.8f, 0.9f, 1f, 0.8f), 0.6f, 0.4f, 0.12f);
                    break;
                case ToolKind.Mop:
                    Fx.Burst(FxKind.Splash, p, Vector3.up, n, new Color(0.75f, 0.85f, 0.95f, 0.55f), 0.9f, 0.8f, 0.2f);
                    break;
            }
        }

        void UpdateLoops(InputFrame input, bool spraying, float speed)
        {
            float effort = Mathf.Clamp01(removedSmoothed * 18f);
            bool cloth = Cleaning && Equipped == ToolKind.Cloth;
            clothLoop.TargetVolume = cloth ? Mathf.Clamp01(0.15f + speed * 0.9f) * (0.55f + 0.45f * effort) : 0f;
            clothLoop.TargetPitch = 0.85f + Mathf.Clamp(speed, 0, 1.5f) * 0.35f;

            bool wantVac = Equipped == ToolKind.Vacuum && input.Use && !Suspended;
            if (wantVac != vacuumOn)
            {
                vacuumOn = wantVac;
                Sfx.Play(wantVac ? "vacuum_start" : "vacuum_stop", null, 0.55f);
            }
            vacuumLoop.TargetVolume = vacuumOn ? 0.42f : 0f;
            vacuumLoop.TargetPitch = vacuumOn ? 1f - effort * 0.06f + (Cleaning ? 0.02f : 0f) : 0.6f;
            vacuumLoop.Attack = 3f; vacuumLoop.Release = 5f; vacuumLoop.PitchGlide = 3f;
            suckLoop.TargetVolume = vacuumOn && Cleaning ? 0.12f + effort * 0.45f : 0f;
            suckLoop.TargetPitch = 0.9f + effort * 0.25f;

            bool sq = Cleaning && Equipped == ToolKind.Squeegee;
            squeakLoop.TargetVolume = sq ? Mathf.Clamp01(speed * 0.7f) * 0.55f : 0f;
            squeakLoop.TargetPitch = 0.75f + Mathf.Clamp(speed, 0, 2f) * 0.35f;

            bool mop = Cleaning && Equipped == ToolKind.Mop;
            mopLoop.TargetVolume = mop ? Mathf.Clamp01(0.2f + speed * 0.6f) * 0.6f : 0f;
            mopLoop.TargetPitch = 0.9f + Mathf.Clamp(speed, 0, 1.5f) * 0.15f;

            sprayLoop.TargetVolume = spraying ? 0.45f : 0f;
            sprayLoop.Attack = 30f; sprayLoop.Release = 18f;
        }

        void OnSurfaceCompleted(GrimeSurface s)
        {
            comboStep = comboTimer > 0f ? Mathf.Min(comboStep + 1, Ladder.Length - 1) : 0;
            comboTimer = 5f;
            float pitch = Mathf.Pow(2f, Ladder[comboStep] / 12f);
            Sfx.Play("clean_ding", null, 0.55f, pitch, 0f);
            Sfx.Play("sparkle", s.transform.position, 0.45f, 1f, 0.1f);
            var t = s.transform;
            Fx.SparkleRect(t.position, t.right, t.forward, s.Size, t.up, Mathf.Clamp(Mathf.RoundToInt(s.Size.x * s.Size.y * 24f), 10, 80));
            Player.FovPunch = -1.2f;
            SurfaceCompleted?.Invoke(s);
        }
    }
}
