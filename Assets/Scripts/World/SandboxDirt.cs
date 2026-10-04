using UnityEngine;

namespace AfterHours
{
    /// <summary>Temporary dirt layout used while building the office (replaced by night data).</summary>
    public static class SandboxDirt
    {
        public static void Apply(OfficeBuilder o)
        {
            Set(o, "desk_theo", new GrimeSpec().WithSeed(2).Add(GrimeStamp.Dust(0.5f, 0.45f, 1f)).Add(GrimeStamp.Ring(0.25f, 0.3f)).Add(GrimeStamp.Ring(0.3f, 0.36f)).Add(GrimeStamp.Crumbs(50, new Rect(0.5f, 0.3f, 0.3f, 0.3f))));
            Set(o, "desk_reception", new GrimeSpec().WithSeed(3).Add(GrimeStamp.Dust(0.5f, 0.4f, 1f)).Add(GrimeStamp.Ring(0.4f, 0.5f, 0.04f)).Add(GrimeStamp.Spill(0.7f, 0.5f, 0.06f, new Color(0.35f, 0.2f, 0.1f))));
            Set(o, "floor_bullpen", new GrimeSpec().WithSeed(4).Add(GrimeStamp.Dust(0.2f, 0.3f, 1.2f))
                .Add(GrimeStamp.Trail(0.8f, 0.55f, new Vector2(0.42f, 0.0f), new Vector2(0.4f, 0.35f), new Vector2(0.35f, 0.55f)))
                .Add(GrimeStamp.Patch(0.3f, 0.6f, 0.9f, 0.5f)).Add(GrimeStamp.Crumbs(300, new Rect(0.2f, 0.45f, 0.5f, 0.3f))));
            Set(o, "floor_reception", new GrimeSpec().WithSeed(5).Add(GrimeStamp.Dust(0.15f, 0.25f, 1.2f)).Add(GrimeStamp.Steps(0.55f, 0.02f, 0.62f, 0.95f, 0.85f)).Add(GrimeStamp.Spill(0.3f, 0.3f, 0.2f, new Color(0.3f, 0.22f, 0.12f))));
            Set(o, "glass_doors", new GrimeSpec().WithSeed(6).Add(GrimeStamp.Haze(0.45f)).Add(GrimeStamp.Prints(30, new Rect(0.3f, 0.35f, 0.4f, 0.25f))).Add(GrimeStamp.Smudge(0.5f, 0.5f, 0.15f)));
            Set(o, "win_bullpen_2", new GrimeSpec { GhostTexture = "Textures/Grime/finger_walt", GhostMode = 1 }.WithSeed(7).Add(GrimeStamp.Haze(0.5f)).Add(GrimeStamp.Smudge(0.3f, 0.4f)).Add(GrimeStamp.DripLines(14, new Rect(0.05f, 0.5f, 0.9f, 0.45f), new Color(0.7f, 0.72f, 0.7f))));
            Set(o, "whiteboard_conf", new GrimeSpec { GhostTexture = "Textures/Grime/wb_ghost", GhostMode = 0, GhostScrubbable = true }.WithSeed(8).Add(GrimeStamp.Image("Textures/Grime/wb_marker", 0.55f)));
            Set(o, "table_conf", new GrimeSpec().WithSeed(9).Add(GrimeStamp.Dust(0.4f, 0.35f, 1f)).Add(GrimeStamp.Ring(0.2f, 0.4f)).Add(GrimeStamp.Ring(0.7f, 0.6f)).Add(GrimeStamp.Ring(0.75f, 0.35f)));
            Set(o, "counter_break", new GrimeSpec().WithSeed(10).Add(GrimeStamp.Spill(0.3f, 0.4f, 0.12f, new Color(0.62f, 0.45f, 0.16f))).Add(GrimeStamp.Grease(0.7f, 0.5f, 0.12f)).Add(GrimeStamp.Crumbs(80, new Rect(0.1f, 0.2f, 0.8f, 0.6f))));
            Set(o, "floor_break", new GrimeSpec().WithSeed(11).Add(GrimeStamp.Dust(0.2f, 0.3f, 1.2f)).Add(GrimeStamp.Steps(0.95f, 0.5f, 0.2f, 0.2f, 0.9f)).Add(GrimeStamp.Spill(0.45f, 0.25f, 0.3f, new Color(0.32f, 0.2f, 0.1f))));
        }

        static void Set(OfficeBuilder o, string id, GrimeSpec spec)
        {
            if (!o.Surfaces.TryGetValue(id, out var g)) { Debug.LogWarning("[Sandbox] no surface " + id); return; }
            g.Spec = spec;
            g.gameObject.SetActive(true);
            g.Build();
        }
    }
}
