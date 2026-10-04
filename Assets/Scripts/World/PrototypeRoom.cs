using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// M1 greybox: one room with every cleaning verb (desk, carpet, tile, window, whiteboard)
    /// used to tune the core feel before the Blender office exists.
    /// </summary>
    public class PrototypeRoom : MonoBehaviour
    {
        public GrimeSurface Desk, Carpet, Tile, Window, Whiteboard, Counter;
        public Vector3 Spawn = new(1.5f, 0f, -2.2f);

        public static PrototypeRoom Build()
        {
            var root = new GameObject("PrototypeRoom").AddComponent<PrototypeRoom>();
            root.Make();
            return root;
        }

        void Make()
        {
            var t = transform;
            var wall = Res.Lit(Palette.Hex("C9C3B6"), 0.25f);
            var trim = Res.Lit(Palette.Hex("3A3F47"), 0.4f);
            var carpet = Res.Lit(Palette.Carpet, 0.05f);
            var tile = Res.Lit(Palette.Hex("B9B4A8"), 0.45f);
            var wood = Res.Lit(Palette.Oak, 0.45f);
            var metal = Res.Lit(Palette.Hex("5B6470"), 0.6f, 0.6f);
            var ceiling = Res.Lit(Palette.Hex("D9D7D0"), 0.15f);

            // Floors: carpet west half, tile east half.
            var c = Geo.Box(t, "Floor_Carpet", new Vector3(-2.25f, -0.05f, 0), new Vector3(4.5f, 0.1f, 7f), carpet);
            c.AddComponent<FloorSurface>().Kind = FloorKind.Carpet;
            var ti = Geo.Box(t, "Floor_Tile", new Vector3(2.25f, -0.05f, 0), new Vector3(4.5f, 0.1f, 7f), tile);
            ti.AddComponent<FloorSurface>().Kind = FloorKind.Tile;
            Geo.Box(t, "Ceiling", new Vector3(0, 2.85f, 0), new Vector3(9f, 0.1f, 7f), ceiling);

            // Walls (north has a window opening 3.0 x 1.6 starting at 0.8 m).
            Geo.Box(t, "Wall_S", new Vector3(0, 1.4f, -3.55f), new Vector3(9f, 2.8f, 0.1f), wall);
            Geo.Box(t, "Wall_E", new Vector3(4.55f, 1.4f, 0), new Vector3(0.1f, 2.8f, 7f), wall);
            Geo.Box(t, "Wall_W", new Vector3(-4.55f, 1.4f, 0), new Vector3(0.1f, 2.8f, 7f), wall);
            Geo.Box(t, "Wall_N_L", new Vector3(-2.75f, 1.4f, 3.55f), new Vector3(3.5f, 2.8f, 0.1f), wall);
            Geo.Box(t, "Wall_N_R", new Vector3(3.25f, 1.4f, 3.55f), new Vector3(2.5f, 2.8f, 0.1f), wall);
            Geo.Box(t, "Wall_N_Bot", new Vector3(0.5f, 0.4f, 3.55f), new Vector3(3.5f, 0.8f, 0.1f), wall);
            Geo.Box(t, "Wall_N_Top", new Vector3(0.5f, 2.6f, 3.55f), new Vector3(3.5f, 0.4f, 0.1f), wall);
            // Window frame + glass
            Geo.Box(t, "Sill", new Vector3(0.5f, 0.8f, 3.45f), new Vector3(3.6f, 0.05f, 0.25f), trim);
            Geo.Box(t, "Frame_T", new Vector3(0.5f, 2.4f, 3.5f), new Vector3(3.6f, 0.06f, 0.12f), trim);
            Geo.Box(t, "Frame_L", new Vector3(-1.25f, 1.6f, 3.5f), new Vector3(0.06f, 1.6f, 0.12f), trim);
            Geo.Box(t, "Frame_R", new Vector3(2.25f, 1.6f, 3.5f), new Vector3(0.06f, 1.6f, 0.12f), trim);
            var glass = Geo.Box(t, "Glass", new Vector3(0.5f, 1.6f, 3.52f), new Vector3(3.4f, 1.55f, 0.02f), Res.Glass(new Color(0.45f, 0.6f, 0.68f, 0.1f)), true, Layers.Glass);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Baseboards
            Geo.Box(t, "Base_S", new Vector3(0, 0.05f, -3.48f), new Vector3(9f, 0.1f, 0.03f), trim, false);
            Geo.Box(t, "Base_W", new Vector3(-4.48f, 0.05f, 0), new Vector3(0.03f, 0.1f, 7f), trim, false);

            // City outside the window.
            Skyline.Build(t, new Vector3(0.5f, 0f, 3.6f), 1);

            // Desk
            Geo.Box(t, "Desk_Top", new Vector3(-2.4f, 0.73f, 1.2f), new Vector3(1.6f, 0.04f, 0.8f), wood);
            Geo.Box(t, "Desk_LegL", new Vector3(-3.15f, 0.36f, 1.2f), new Vector3(0.05f, 0.71f, 0.7f), metal);
            Geo.Box(t, "Desk_LegR", new Vector3(-1.65f, 0.36f, 1.2f), new Vector3(0.05f, 0.71f, 0.7f), metal);
            Geo.Box(t, "Desk_Modesty", new Vector3(-2.4f, 0.45f, 1.57f), new Vector3(1.5f, 0.4f, 0.02f), metal);
            Geo.Box(t, "Monitor", new Vector3(-2.4f, 1.0f, 1.45f), new Vector3(0.55f, 0.33f, 0.03f), Res.Lit(Palette.Hex("15181C"), 0.7f));
            Geo.Box(t, "MonitorScreen", new Vector3(-2.4f, 1.0f, 1.433f), new Vector3(0.52f, 0.3f, 0.005f), Res.Emissive(Color.black, Palette.Monitor, 0.6f), false);
            Geo.Box(t, "MonitorStand", new Vector3(-2.4f, 0.8f, 1.47f), new Vector3(0.05f, 0.14f, 0.05f), metal);

            // Kitchen counter on the tile side.
            Geo.Box(t, "Counter_Base", new Vector3(3.9f, 0.45f, 0.5f), new Vector3(1.2f, 0.9f, 2.6f), Res.Lit(Palette.Hex("6E7B6A"), 0.3f));
            Geo.Box(t, "Counter_Top", new Vector3(3.85f, 0.92f, 0.5f), new Vector3(1.3f, 0.04f, 2.7f), Res.Lit(Palette.Hex("E6E1D6"), 0.55f));

            // Whiteboard on the west wall.
            Geo.Box(t, "WB_Frame", new Vector3(-4.48f, 1.5f, -1.2f), new Vector3(0.03f, 1.2f, 2.1f), Res.Lit(Palette.Hex("A7ADB4"), 0.7f, 0.8f));
            Geo.Box(t, "WB_Board", new Vector3(-4.46f, 1.5f, -1.2f), new Vector3(0.02f, 1.12f, 2.02f), Res.Lit(Color.white, 0.85f));
            Geo.Box(t, "WB_Tray", new Vector3(-4.42f, 0.92f, -1.2f), new Vector3(0.08f, 0.02f, 1.8f), Res.Lit(Palette.Hex("A7ADB4"), 0.7f, 0.8f));

            // Grime
            Desk = Geo.Grime(t, "desk", new Vector3(-2.4f, 0.75f, 1.2f), Vector3.up, Vector3.forward, new Vector2(1.6f, 0.8f), ToolKind.Cloth,
                new GrimeSpec().WithSeed(7)
                    .Add(GrimeStamp.Dust(0.5f, 0.45f, 1f))
                    .Add(GrimeStamp.Ring(0.22f, 0.35f, 0.045f))
                    .Add(GrimeStamp.Ring(0.27f, 0.42f, 0.044f))
                    .Add(GrimeStamp.Ring(0.78f, 0.3f, 0.05f))
                    .Add(GrimeStamp.Spill(0.6f, 0.6f, 0.07f, new Color(0.35f, 0.2f, 0.1f)))
                    .Add(GrimeStamp.Crumbs(60, new Rect(0.35f, 0.15f, 0.3f, 0.3f))), 160f);
            Desk.DisplayName = "Desk";

            Counter = Geo.Grime(t, "counter", new Vector3(3.85f, 0.94f, 0.5f), Vector3.up, Vector3.forward, new Vector2(1.3f, 2.7f), ToolKind.Cloth,
                new GrimeSpec().WithSeed(31)
                    .Add(GrimeStamp.Dust(0.4f, 0.4f))
                    .Add(GrimeStamp.Spill(0.4f, 0.3f, 0.12f, new Color(0.62f, 0.45f, 0.16f)))
                    .Add(GrimeStamp.Grease(0.6f, 0.7f, 0.1f))
                    .Add(GrimeStamp.Ring(0.3f, 0.8f, 0.04f))
                    .Add(GrimeStamp.Crumbs(80, new Rect(0.1f, 0.4f, 0.8f, 0.3f))), 120f);
            Counter.DisplayName = "Counter";

            Carpet = Geo.Grime(t, "carpet", new Vector3(-2.25f, 0.0f, 0), Vector3.up, Vector3.forward, new Vector2(4.5f, 7f), ToolKind.Vacuum,
                new GrimeSpec().WithSeed(3)
                    .Add(GrimeStamp.Dust(0.25f, 0.35f, 1.2f))
                    .Add(GrimeStamp.Trail(0.7f, 0.55f, new Vector2(0.95f, 0.1f), new Vector2(0.6f, 0.35f), new Vector2(0.45f, 0.62f), new Vector2(0.5f, 0.8f)))
                    .Add(GrimeStamp.Patch(0.5f, 0.66f, 0.55f, 0.6f))
                    .Add(GrimeStamp.Steps(0.9f, 0.05f, 0.45f, 0.72f, 0.75f))
                    .Add(GrimeStamp.Crumbs(260, new Rect(0.3f, 0.55f, 0.4f, 0.2f))), 56f);
            Carpet.DisplayName = "Carpet";

            Tile = Geo.Grime(t, "tile", new Vector3(2.25f, 0.0f, 0), Vector3.up, Vector3.forward, new Vector2(4.5f, 7f), ToolKind.Mop,
                new GrimeSpec().WithSeed(5)
                    .Add(GrimeStamp.Dust(0.2f, 0.3f, 1.2f))
                    .Add(GrimeStamp.Trail(0.6f, 0.45f, new Vector2(0.02f, 0.3f), new Vector2(0.5f, 0.38f), new Vector2(0.85f, 0.6f)))
                    .Add(GrimeStamp.Steps(0.05f, 0.3f, 0.9f, 0.55f, 0.95f))
                    .Add(GrimeStamp.Spill(0.55f, 0.75f, 0.28f, new Color(0.32f, 0.2f, 0.1f)))
                    .Add(GrimeStamp.Scuffs(25, new Rect(0.1f, 0.1f, 0.8f, 0.8f))), 56f);
            Tile.DisplayName = "Tile floor";

            Window = Geo.Grime(t, "window", new Vector3(0.5f, 1.6f, 3.505f), Vector3.back, Vector3.up, new Vector2(3.4f, 1.55f), ToolKind.Squeegee,
                new GrimeSpec { GhostTexture = "Textures/Grime/finger_walt", GhostMode = 1 }.WithSeed(9)
                    .Add(GrimeStamp.Haze(0.5f))
                    .Add(GrimeStamp.Smudge(0.25f, 0.4f, 0.14f))
                    .Add(GrimeStamp.Smudge(0.7f, 0.55f, 0.12f))
                    .Add(GrimeStamp.Prints(40, new Rect(0.1f, 0.15f, 0.8f, 0.5f)))
                    .Add(GrimeStamp.DripLines(18, new Rect(0.05f, 0.5f, 0.9f, 0.45f), new Color(0.7f, 0.72f, 0.7f))), 90f);
            Window.DisplayName = "Window";

            Whiteboard = Geo.Grime(t, "whiteboard", new Vector3(-4.448f, 1.5f, -1.2f), Vector3.right, Vector3.up, new Vector2(2.0f, 1.1f), ToolKind.Cloth,
                new GrimeSpec { GhostTexture = "Textures/Grime/wb_ghost", GhostMode = 0, GhostScrubbable = true }.WithSeed(11)
                    .Add(GrimeStamp.Image("Textures/Grime/wb_marker", 0.55f)), 150f);
            Whiteboard.DisplayName = "Whiteboard";
            Whiteboard.DirtSmoothness = 0.7f;

            // Lights: two ceiling panels (warm-white), monitor glow, moonlight.
            foreach (var x in new[] { -2.2f, 2.2f })
            {
                Geo.Box(t, "Panel", new Vector3(x, 2.79f, 0), new Vector3(1.2f, 0.03f, 0.6f), Res.Emissive(Color.white, Palette.Fluoro, 2.2f), false)
                    .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var l = Geo.SpotLight(t, new Vector3(x, 2.75f, 0), Vector3.down, Palette.Fluoro, 9f, 7f, 150f, true);
                l.shadowStrength = 0.6f;
            }
            Geo.PointLight(t, new Vector3(-2.4f, 1.0f, 1.25f), Palette.Monitor, 0.6f, 2.2f);
            var lamp = Geo.PointLight(t, new Vector3(3.7f, 1.6f, -1.2f), Palette.Tungsten, 1.6f, 4.5f);
            lamp.name = "WarmLamp";
        }
    }
}
