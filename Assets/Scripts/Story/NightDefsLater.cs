using System.Linq;
using UnityEngine;
using static AfterHours.GrimeStamp;

namespace AfterHours
{
    public static partial class NightDefs
    {
        // ---- shared helpers for nights 2–7 -----------------------------------------------------

        static GrimeSpec DeskDirt(int seed, int rings = 2, bool crumbs = true)
        {
            var s = Spec(seed).Add(Dust(0.45f, 0.4f, 1f));
            var rng = new Rng(seed);
            for (int i = 0; i < rings; i++) s.Add(Ring(rng.Range(0.15f, 0.85f), rng.Range(0.2f, 0.8f), rng.Range(0.035f, 0.05f)));
            if (crumbs) s.Add(Crumbs(rng.Range(20, 70), new Rect(rng.Range(0.2f, 0.6f), 0.2f, 0.3f, 0.5f)));
            return s;
        }

        static GrimeSpec WindowDirt(int seed, float haze = 0.45f, int prints = 25)
        {
            return Spec(seed).Add(Haze(haze)).Add(Smudge(0.3f, 0.45f, 0.13f)).Add(Smudge(0.7f, 0.35f, 0.12f))
                .Add(Prints(prints, new Rect(0.15f, 0.2f, 0.7f, 0.5f))).Add(DripLines(12, new Rect(0.05f, 0.5f, 0.9f, 0.45f), new Color(0.7f, 0.72f, 0.7f)));
        }

        /// <summary>A sticky note stuck on a desk's monitor, readable.</summary>
        static SpawnDef MonitorSticky(NightDef n, string desk, string doc, System.Func<StoryState, bool> when)
        {
            var s = new SpawnDef { Prop = "sticky_note", Anchor = "ANCHOR_desk_" + desk, Pos = new Vector3(-0.2f, 0.47f, -0.205f), Pitch = 90, Roll = 180, Kind = SpawnKind.Readable, Doc = doc, Id = doc, When = when };
            n.Spawns.Add(s);
            return s;
        }

        static SpawnDef CorkNote(NightDef n, string doc, float x, float y, System.Func<StoryState, bool> when = null, string prop = "sticky_note")
        {
            var s = new SpawnDef { Prop = prop, Anchor = "ANCHOR_corkboard_closet", Pos = new Vector3(x, y, 0.012f), Pitch = 90, Roll = 180, Kind = SpawnKind.Readable, Doc = doc, Id = doc, When = when };
            n.Spawns.Add(s);
            return s;
        }

        static void Mugs(NightDef n, params Vector3[] where)
        {
            string[] mugs = { "mug_white", "mug_red", "mug_teal", "mug_yellow", "mug_white", "mug_teal" };
            for (int i = 0; i < where.Length; i++)
                n.Spawns.Add(new SpawnDef { Id = "mug" + i, Prop = mugs[i % mugs.Length], Name = "Mug", Pos = where[i], Yaw = i * 47, Kind = SpawnKind.Reset, Group = "dishrack", HasHome = true });
        }

        static void AllMonitorsOn(NightDef n)
        {
            Monitors(n, ("dana", "Textures/Screens/login_dana"), ("theo", "Textures/Screens/screensaver"), ("priya", "Textures/Screens/login_priya"), ("russ", "Textures/Screens/screensaver"));
        }

        /// <summary>Marian's desk things become movable, starting at home. Moved and not returned = suspicion.</summary>
        static void MarianDeskItems(NightContext ctx)
        {
            foreach (var name in new[] { "marian_orchid", "marian_plaque", "marian_phone" })
            {
                if (!ctx.Furniture.Named.TryGetValue(name, out var go) || go == null) continue;
                go.layer = Layers.Prop;
                var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.mass = 0.5f;
                if (!go.GetComponent<ImpactSound>()) go.AddComponent<ImpactSound>();
                var h = go.AddComponent<Holdable>();
                h.Id = name;
                h.DisplayName = name == "marian_orchid" ? "Orchid" : name == "marian_plaque" ? "Award" : "Desk phone";
                var r = go.AddComponent<Resettable>();
                r.Id = name;
                r.Required = false;
                r.SetHome(go.transform.position, go.transform.rotation);
                r.ForceHome();
            }
        }

        static int MarianDisturbed(NightContext ctx) =>
            Resettable.All.Count(r => r.Id != null && r.Id.StartsWith("marian_") && !r.AtHome);

        static void UvArrow(NightContext ctx, string tex, Vector3 pos, float yaw, float size = 0.45f, string secret = null, string caption = null)
        {
            UvMark.Create(ctx.Director.transform, tex, pos, Quaternion.Euler(0, yaw, 0), Vector2.one * size, secret, caption);
        }

        static void LeftLocker(NightContext ctx, string doc, string label, System.Action onRead = null)
        {
            var locker = GameObject.Find("FURN_locker_walt");
            if (!locker) return;
            var r = locker.GetComponent<Readable>() ?? locker.AddComponent<Readable>();
            r.Doc = doc;
            r.Label = label;
            r.OnRead = () => { Sfx.Play("drawer_open", locker.transform.position, 0.6f); onRead?.Invoke(); };
        }

        // =========================================================================================
        // NIGHT 2 — Tuesday — Glass
        // =========================================================================================

        static NightDef Night2()
        {
            var n = new NightDef { Number = 2, Day = "Tuesday", Title = "Glass", Tagline = "Read what they don't.", Rooms = new[] { "reception", "bullpen", "breakroom" } };

            n.Grime("glass_doors", WindowDirt(21, 0.5f, 40));
            n.Grime("win_break_1", WindowDirt(22));
            n.Grime("win_break_2", new GrimeSpec { GhostTexture = "Textures/Grime/finger_walt", GhostMode = 1 }.WithSeed(23).Add(Haze(0.55f)).Add(Smudge(0.25f, 0.3f, 0.12f)).Add(DripLines(10, new Rect(0.05f, 0.5f, 0.9f, 0.45f), new Color(0.7f, 0.72f, 0.7f))), false);
            n.Grime("floor_break", Spec(24).Add(Dust(0.2f, 0.3f, 1.2f))
                .Add(Steps(BR(6.8f, 6.85f).x, BR(6.8f, 6.85f).y, BR(1.5f, 4.0f).x, BR(1.5f, 4.0f).y, 0.85f))
                .Add(Spill(BR(3.0f, 4.4f).x, BR(3.0f, 4.4f).y, 0.32f, CoffeeCol))
                .Add(Scuffs(30, new Rect(0.1f, 0.1f, 0.8f, 0.8f))));
            n.Grime("counter_break", Spec(25).Add(Dust(0.3f, 0.3f, 1f)).Add(Spill(0.3f, 0.5f, 0.1f, CoffeeCol)).Add(Grease(0.7f, 0.45f, 0.09f)).Add(Ring(0.55f, 0.6f)).Add(Crumbs(90, new Rect(0.1f, 0.2f, 0.8f, 0.6f))));
            n.Grime("table_break", Spec(26).Add(Dust(0.35f, 0.3f, 1f)).Add(Ring(0.3f, 0.4f)).Add(Ring(0.65f, 0.7f)).Add(Spill(0.5f, 0.3f, 0.07f, SodaCol)).Add(Crumbs(70, new Rect(0.3f, 0.3f, 0.4f, 0.4f))));
            n.Grime("fridge_break", Spec(27).Add(Prints(40, new Rect(0.75f, 0.2f, 0.2f, 0.6f))).Add(Smudge(0.85f, 0.5f, 0.1f)).Add(Dust(0.15f, 0.2f, 0.5f)), false);
            n.Grime("desk_reception", DeskDirt(28, 1));
            n.Grime("floor_reception", Spec(29).Add(Dust(0.15f, 0.22f, 1.2f)).Add(Steps(RC(11.2f, 0.2f).x, RC(11.2f, 0.2f).y, RC(12.2f, 4.7f).x, RC(12.2f, 4.7f).y, 0.9f)).Add(Steps(RC(11.4f, 0.2f).x, RC(11.4f, 0.2f).y, RC(8.4f, 2.2f).x, RC(8.4f, 2.2f).y, 0.7f)), false);
            n.Grime("desk_theo", DeskDirt(30, 1), false);
            n.Grime("desk_russ", DeskDirt(31, 3), false);

            // Break room mess
            n.Trash("soda_can", 3.0f, 0.76f, 6.3f, TrashKind.Recyclable, "Soda can");
            n.Trash("soda_can", 2.2f, 0.02f, 7.6f, TrashKind.Recyclable, "Soda can");
            n.Trash("paper_cup", 3.6f, 0.76f, 6.85f, TrashKind.General, "Coffee cup");
            n.Trash("paper_cup", 1.1f, 0.94f, 3.5f, TrashKind.General, "Coffee cup");
            n.Trash("takeout_box", 3.2f, 0.76f, 6.55f, TrashKind.General, "Takeout box", 30);
            n.Trash("banana_peel", 5.9f, 0.02f, 5.2f, TrashKind.General, "Banana peel");
            n.Trash("water_bottle", 0.6f, 0.02f, 8.3f, TrashKind.Recyclable, "Water bottle", 90);
            n.Trash("paper_ball", 4.6f, 0.04f, 7.9f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_cup", 13.5f, 0.02f, 4.3f, TrashKind.General, "Coffee cup");
            n.Trash("paper_ball", 9.9f, 0.04f, 10.6f, TrashKind.Paper, "Paper ball");
            n.Evidence("russ_slip", 6.25f, 0.01f, 4.35f, 25);

            Mugs(n, new Vector3(2.8f, 0.76f, 6.9f), new Vector3(3.75f, 0.76f, 6.35f), new Vector3(0.6f, 0.94f, 3.45f), new Vector3(4.2f, 0.94f, 3.5f), new Vector3(10.9f, 0.76f, 11.65f));

            // Break room corkboard and fridge
            n.Spawns.Add(new SpawnDef { Prop = "paper_sheet", Anchor = "ANCHOR_corkboard_break", Pos = new Vector3(0.2f, 0.05f, 0.012f), Pitch = 90, Roll = 180, Kind = SpawnKind.Readable, Doc = "walt_card", Id = "walt_card" });
            n.Spawns.Add(new SpawnDef { Prop = "sticky_note", Pos = new Vector3(5.45f, 1.45f, 3.86f), Pitch = 90, Roll = 180, Kind = SpawnKind.Readable, Doc = "fridge_note", Id = "fridge_note" });

            // Consequences of night 1
            MonitorSticky(n, "theo", "theo_thanks", s => Delivered(s, "theo_note", "theo"));
            MonitorSticky(n, "priya", "priya_thanks", s => Delivered(s, "theo_note", "priya") || s.NoteTo("priya", null));
            MonitorSticky(n, "theo", "marian_seeme", s => Delivered(s, "theo_note", "marian"));
            n.Trash("paper_ball", 10.6f, 0.04f, 10.8f, TrashKind.Paper, "Paper ball", 0, s => Delivered(s, "theo_note", "marian"));
            n.Trash("paper_ball", 9.7f, 0.04f, 11.0f, TrashKind.Paper, "Paper ball", 0, s => Delivered(s, "theo_note", "marian"));
            CommonCloset(n);

            AllMonitorsOn(n);
            foreach (var c in new[] { "break_1", "break_2", "break_3", "dana", "russ" }) n.ChairsOut.Add(c);

            n.Task("glass_doors", "Squeegee the glass doors", TaskKind.Clean, "reception", "glass_doors")
             .Task("desk", "Wipe the reception desk", TaskKind.Clean, "reception", "desk_reception")
             .Task("mop", "Mop the break room floor", TaskKind.Clean, "breakroom", "floor_break")
             .Task("counter", "Wipe the counter and the table", TaskKind.Clean, "breakroom", "counter_break", "table_break")
             .Task("mugs", "Mugs in the dish rack", TaskKind.Reset, "breakroom", "breakroom", "reception", "bullpen")
             .Task("window", "Wash the break room window", TaskKind.Clean, "breakroom", "win_break_1")
             .Task("trash", "Bin the rubbish", TaskKind.Trash, "breakroom", "breakroom", "reception", "bullpen")
             .Task("chairs", "Tuck in the chairs", TaskKind.Chairs, "breakroom", "breakroom", "reception", "bullpen")
             .Task("monitors", "Switch off the monitors", TaskKind.Monitors, "bullpen", "reception", "bullpen")
             .Task("lights", "Lights off everywhere", TaskKind.Lights, "closet", "reception", "bullpen", "breakroom")
             .Optional("window2", "Wash the second window", TaskKind.Clean, "breakroom", "win_break_2")
             .Optional("fridge", "Wipe the fridge door", TaskKind.Clean, "breakroom", "fridge_break")
             .Optional("floor_rec", "Mop the reception floor", TaskKind.Clean, "reception", "floor_reception")
             .Optional("desks", "Quick wipe of Theo's and Russ's desks", TaskKind.Clean, "bullpen", "desk_theo", "desk_russ");

            n.Secret("window_message", "Writing in the foam")
             .Secret("russ_slip", "Russ owes somebody money")
             .Secret("walt_card", "Walt's farewell card")
             .Secret("walt_note_2", "Walt's UV torch")
             .Secret("uv_arrows", "Marks only the torch can see");

            n.Script = ctx =>
            {
                LeftLocker(ctx, "walt_note_2", "Open W. Bremner's locker", () =>
                {
                    if (ctx.State.Has("has_uv_torch")) return;
                    ctx.State.Set("has_uv_torch");
                    ctx.Delay(0.3f, () => ctx.Toast("Walt's UV torch", "Press F to switch it on. Missed spots glow.", new Color(0.6f, 0.45f, 1f), 4f));
                });
                // The writing shows through the foam.
                var win = ctx.Surface("win_break_2");
                bool revealed = false;
                ctx.Tick(dt =>
                {
                    if (revealed || win == null || !win.gameObject.activeSelf) return;
                    if (win.GhostFoamCoverage() > 0.35f)
                    {
                        revealed = true;
                        Story.OnDocRead(Docs.Get("window_message"));
                        ctx.Caption("[Letters show through the foam: WALT DIDN'T TAKE IT]", 4f);
                        PostFx.Instance?.Pulse(1f);
                    }
                });
                // Walt's UV arrows in the bullpen lead toward the conference room.
                UvArrow(ctx, "arrow", new Vector3(7.08f, 1.2f, 8.2f), -90, 0.4f, "uv_arrows", "[Glowing arrows on the wall. Walt's handwriting?]");
                UvArrow(ctx, "arrow", new Vector3(7.08f, 1.2f, 9.6f), -90, 0.4f);
                UvArrow(ctx, "question", new Vector3(7.06f, 1.5f, 11.8f), -90, 0.5f);
                // 3 AM: the elevator arrives. Nobody gets out.
                bool dinged = false;
                ctx.Tick(dt =>
                {
                    if (dinged || ctx.Director.ClockMinutes < 300f) return;
                    dinged = true;
                    Sfx.Play("elevator_ding", new Vector3(11f, 1.5f, -3.2f), 1f, 1f, 0f, AudioBus.Sfx, 0f, 30f);
                    ctx.Caption("[The elevator dings. The doors open on an empty car.]", 3.5f);
                    if (ctx.Room("lobby") is { } lobby) { lobby.SetLights(true); ctx.Delay(6f, () => lobby.SetLights(false)); }
                });
            };
            n.End = ctx =>
            {
                var win = ctx.Surface("win_break_2");
                if (ctx.Director.HasSecret("window_message") && win != null && !win.Done) ctx.State.Set("window_left");
                if (ctx.Director.Def.Tasks.First(t => t.Id == "mugs") is var mt && ctx.Director.IsDone(mt)) ctx.State.Set("mugs_done");
            };

            n.Say("7:52 AM", "dana", "WHO WASHED ALL THE MUGS ♥♥♥", s => s.Has("mugs_done"), "♥")
             .Say("8:05 AM", "dana", "Did someone write on the break room window?? \"Walt didn't take it\" ... that's not funny", s => s.Has("window_left"))
             .Say("8:07 AM", "priya", "or it's true", s => s.Has("window_left"))
             .Say("8:20 AM", "marian", "Facilities has been called. This is unprofessional.", s => s.Has("window_left"))
             .Say("8:41 AM", "russ", "found my own betting slip in my tray?? thanks mystery cleaner. i'm cutting back I SWEAR", s => Delivered(s, "russ_slip", "russ"))
             .Say("9:10 AM", "marian", "Russ, a word please. Bring your expense reports.", s => Delivered(s, "russ_slip", "marian"))
             .Say("9:11 AM", "russ", "???????", s => Delivered(s, "russ_slip", "marian"))
             .Say("9:15 AM", "dana", "Russ honey, are you okay? Found something in my tray. Let's get coffee.", s => Delivered(s, "russ_slip", "dana"))
             .Say("9:30 AM", "priya", "Russ, I'm not your accountant. But I found your debts in my tray, so: maybe stop.", s => Delivered(s, "russ_slip", "priya"))
             .Say("8:02 AM", "theo", "working from home today", s => Delivered(s, "theo_note", "marian"))
             .Say("10:12 AM", "russ", "the new cleaner put my mug in a RACK. nobody puts my mug in a rack")
             .Say("10:13 AM", "priya", "the break room smells like actual lemons now. I'm not complaining")
             .Say("11:47 AM", "dana", "Elevator 2 is acting up again, it went to 14 at 3am with nobody in it lol", s => true);
            return n;
        }

        // =========================================================================================
        // NIGHT 3 — Wednesday — The Whiteboard
        // =========================================================================================

        static NightDef Night3()
        {
            var n = new NightDef { Number = 3, Day = "Wednesday", Title = "The Whiteboard", Tagline = "Some marks don't wipe off.", Rooms = new[] { "reception", "bullpen", "conference", "breakroom" } };

            n.Grime("whiteboard_conf", new GrimeSpec { GhostTexture = "Textures/Grime/wb_ghost", GhostMode = 0, GhostScrubbable = true }.WithSeed(31).Add(Image("Textures/Grime/wb_marker", 0.55f)));
            n.Grime("table_conf", Spec(32).Add(Dust(0.35f, 0.3f, 1f)).Add(Ring(0.15f, 0.35f)).Add(Ring(0.2f, 0.42f)).Add(Ring(0.45f, 0.62f)).Add(Ring(0.72f, 0.3f)).Add(Ring(0.86f, 0.6f)).Add(Grease(0.55f, 0.45f, 0.12f)).Add(Crumbs(120, new Rect(0.3f, 0.3f, 0.5f, 0.4f))));
            n.Grime("glasswall_conf", Spec(33).Add(Haze(0.4f)).Add(Prints(50, new Rect(0.1f, 0.35f, 0.8f, 0.35f))).Add(Smudge(0.2f, 0.45f, 0.15f)).Add(Smudge(0.6f, 0.4f, 0.12f)).Add(Smudge(0.8f, 0.5f, 0.13f)));
            n.Grime("floor_conf", Spec(34).Add(Dust(0.15f, 0.25f, 1.2f)).Add(Crumbs(320, new Rect(CF(1.8f, 11.5f), new Vector2(0.55f, 0.3f)))).Add(Trail(0.6f, 0.45f, CF(6.8f, 10.5f), CF(4.5f, 11.2f), CF(1.5f, 11.4f))));
            n.Grime("desk_theo", DeskDirt(35, 2));
            n.Grime("desk_priya", DeskDirt(36, 2));
            n.Grime("desk_russ", DeskDirt(37, 3));
            n.Grime("desk_walt", DeskDirt(38, 1, false));
            n.Grime("floor_bullpen", Spec(39).Add(Dust(0.12f, 0.22f, 1.2f)).Add(Trail(0.7f, 0.45f, BP(11.5f, 5.1f), BP(12.0f, 9.0f), BP(7.4f, 10.5f))), false);
            n.Grime("desk_reception", DeskDirt(40, 1), false);

            // The late meeting
            n.Trash("pizza_box", 2.4f, 0.775f, 12.55f, TrashKind.General, "Pizza box", 15);
            n.Trash("pizza_box", 4.9f, 0.775f, 12.9f, TrashKind.General, "Pizza box", -20);
            n.Trash("soda_can", 2.0f, 0.775f, 12.1f, TrashKind.Recyclable, "Soda can");
            n.Trash("soda_can", 3.9f, 0.775f, 13.1f, TrashKind.Recyclable, "Soda can");
            n.Trash("soda_can", 5.6f, 0.02f, 11.0f, TrashKind.Recyclable, "Soda can");
            n.Trash("water_bottle", 3.2f, 0.775f, 12.15f, TrashKind.Recyclable, "Water bottle", 80);
            n.Trash("party_plate", 4.3f, 0.775f, 12.3f, TrashKind.General, "Paper plate");
            n.Trash("party_plate", 1.5f, 0.02f, 13.8f, TrashKind.General, "Paper plate");
            n.Trash("paper_ball", 6.0f, 0.04f, 14.5f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_ball", 1.2f, 0.04f, 10.6f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_cup", 11.6f, 0.76f, 12.6f, TrashKind.General, "Coffee cup");
            n.Trash("energy_can", 10.7f, 0.76f, 12.55f, TrashKind.Recyclable, "Energy drink");
            n.Evidence("theo_planner", 3.0f, 0.005f, 12.9f, 35);
            n.Readable("audit_agenda", "paper_sheet", 3.6f, 0.756f, 12.75f, 8);
            Mugs(n, new Vector3(4.6f, 0.775f, 12.4f), new Vector3(2.9f, 0.775f, 13.0f));

            // Consequences of night 2
            n.Readable("marian_memo", "paper_sheet", 12.2f, 0.756f, 3.75f, -10, "Read", s => s.Has("window_left"));
            n.Decor("card_box", 14.8f, 0.0f, 13.3f, 20, s => Delivered(s, "russ_slip", "marian"));
            CommonCloset(n);

            AllMonitorsOn(n);
            n.Monitors["walt"] = null;
            for (int i = 1; i <= 6; i++) n.ChairsOut.Add("conf_" + i);
            n.ChairsOut.Add("theo");
            n.ChairsOut.Add("priya");

            n.Task("whiteboard", "Erase the whiteboard", TaskKind.Clean, "conference", "whiteboard_conf")
             .Task("table", "Wipe the conference table", TaskKind.Clean, "conference", "table_conf")
             .Task("glass", "Squeegee the glass wall", TaskKind.Clean, "conference", "glasswall_conf")
             .Task("vacuum", "Vacuum the conference carpet", TaskKind.Clean, "conference", "floor_conf")
             .Task("chairs", "Tuck in the chairs", TaskKind.Chairs, "conference", "conference", "bullpen")
             .Task("desks", "Wipe the four bullpen desks", TaskKind.Clean, "bullpen", "desk_theo", "desk_priya", "desk_russ", "desk_walt")
             .Task("trash", "Bin the rubbish", TaskKind.Trash, "conference", "conference", "bullpen", "reception")
             .Task("mugs", "Mugs back to the break room rack", TaskKind.Reset, "breakroom", "breakroom", "conference")
             .Task("monitors", "Switch off the monitors", TaskKind.Monitors, "bullpen", "reception", "bullpen")
             .Task("lights", "Lights off everywhere", TaskKind.Lights, "closet", "reception", "bullpen", "conference", "breakroom")
             .Optional("bullpen_floor", "Vacuum the bullpen", TaskKind.Clean, "bullpen", "floor_bullpen")
             .Optional("rec_desk", "Wipe the reception desk", TaskKind.Clean, "reception", "desk_reception");

            n.Secret("whiteboard_ghost", "What was under the marker")
             .Secret("audit_agenda", "The audit is on Monday")
             .Secret("theo_planner", "Theo's planner page")
             .Secret("uv_w", "Walt was here too")
             .Secret("marian_shredder", "Someone in the locked office");

            n.Script = ctx =>
            {
                var wb = ctx.Surface("whiteboard_conf");
                if (wb != null)
                {
                    wb.GhostRevealed += _ =>
                    {
                        Story.OnDocRead(Docs.Get("whiteboard_ghost"));
                        Sfx.Play("discover", wb.transform.position, 0.9f);
                        PostFx.Instance?.Pulse(1.2f);
                        GameRoot.Instance.Player.FovPunch = -4f;
                        ctx.Caption("[Under the marker, older writing that won't wipe off]", 4f);
                    };
                    wb.GhostScrubbed += _ => ctx.State.Set("ghost_scrubbed");
                }
                UvArrow(ctx, "w", new Vector3(3.85f, 2.13f, 9.11f), 0, 0.22f, "uv_w", "[A small glowing W on the frame]");
                // Someone is in Marian's office.
                bool heard = false;
                ctx.Tick(dt =>
                {
                    if (heard || ctx.Director.Elapsed < 75f) return;
                    var p = GameRoot.Instance.Player.transform.position;
                    if (p.x < 13f || p.z < 7f) return;
                    heard = true;
                    Sfx.Play("shredder", new Vector3(23.4f, 0.6f, 9.8f), 0.9f, 1f, 0f, AudioBus.Sfx, 0f, 16f);
                    ctx.Delay(2.3f, () => Sfx.Play("shredder", new Vector3(23.4f, 0.6f, 9.8f), 0.9f, 0.95f, 0f, AudioBus.Sfx, 0f, 16f));
                    ctx.Caption("[A shredder runs behind Marian's locked door]", 3.5f);
                    if (ctx.Office.Doors.TryGetValue("office", out var door)) door.LockedMessage = "Locked · someone's moving around inside";
                    ctx.Secret("marian_shredder");
                    ctx.Delay(40f, () => { if (ctx.Office.Doors.TryGetValue("office", out var d)) d.LockedMessage = "Locked · Marian's office"; });
                });
            };
            n.End = ctx =>
            {
                var wb = ctx.Surface("whiteboard_conf");
                if (wb != null && wb.GhostWasRevealed && !wb.GhostWasScrubbed) { ctx.State.Set("ghost_left"); ctx.State.Suspicion += 1; }
            };

            n.Say("9:02 AM", "priya", "who drew a conspiracy board on the conference whiteboard lol", s => s.Has("ghost_left"))
             .Say("9:03 AM", "russ", "\"who approves these??\" ... who DOES approve these", s => s.Has("ghost_left"))
             .Say("9:05 AM", "marian", "Dana, call facilities. The board is stained. Meeting moved to my office.", s => s.Has("ghost_left"))
             .Say("9:06 AM", "theo", "...", s => s.Has("ghost_left"))
             .Say("8:40 AM", "dana", "Conference room looks BRAND NEW. That whiteboard was so stained!", s => s.Has("ghost_scrubbed"))
             .Say("8:41 AM", "marian", "Thank you, Dana.", s => s.Has("ghost_scrubbed"))
             .Say("8:30 AM", "theo", "found a page from my planner on my desk. thank you whoever. please. stop.", s => Delivered(s, "theo_planner", "theo"))
             .Say("8:47 AM", "priya", "someone keeps sending me Theo's planner pages. ok. ok ok ok.", s => Delivered(s, "theo_planner", "priya"))
             .Say("9:15 AM", "marian", "Theo, bring your planner to my office.", s => Delivered(s, "theo_planner", "marian"))
             .Say("9:20 AM", "dana", "Theo you left a page from your planner at reception, hon", s => Delivered(s, "theo_planner", "dana"))
             .Say("9:44 AM", "russ", "the pizza boxes are GONE. respect to the night shift")
             .Say("1:10 PM", "dana", "Reminder: conference room is booked ALL of next week for the auditors.");
            return n;
        }

        // =========================================================================================
        // NIGHT 4 — Thursday — The Corner Office
        // =========================================================================================

        static NightDef Night4()
        {
            var n = new NightDef { Number = 4, Day = "Thursday", Title = "The Corner Office", Tagline = "Put everything back exactly where it was.", Rooms = new[] { "reception", "bullpen", "office", "breakroom", "conference" } };

            n.Grime("desk_marian", Spec(41).Add(Dust(0.5f, 0.45f, 1f)).Add(Ring(0.2f, 0.55f, 0.04f)).Add(Ring(0.75f, 0.35f, 0.045f)));
            for (int i = 1; i <= 4; i++) n.Grime("shelf_office_" + i, Spec(41 + i).Add(Dust(0.85f, 0.75f, 0.6f)));
            n.Grime("win_office_n1", WindowDirt(46, 0.4f));
            n.Grime("win_office_n2", WindowDirt(47, 0.45f));
            n.Grime("win_office_e1", WindowDirt(48, 0.4f), false);
            n.Grime("floor_office", Spec(49).Add(Dust(0.2f, 0.28f, 1.3f)).Add(Steps(OF(18.3f, 10.45f).x, OF(18.3f, 10.45f).y, OF(23.0f, 12.6f).x, OF(23.0f, 12.6f).y, 0.75f)).Add(Spill(OF(24.0f, 14.6f).x, OF(24.0f, 14.6f).y, 0.12f, MudCol)));
            n.Grime("desk_theo", DeskDirt(50, 1), false);
            n.Grime("desk_priya", DeskDirt(51, 2), false);
            n.Grime("desk_reception", DeskDirt(52, 2));
            n.Grime("floor_bullpen", Spec(53).Add(Dust(0.15f, 0.22f, 1.2f)).Add(Trail(0.7f, 0.5f, BP(11.5f, 5.1f), BP(12.3f, 9.5f), BP(17.9f, 10.5f))).Add(Crumbs(120, new Rect(BP(9.5f, 10f), new Vector2(0.2f, 0.15f)))));

            // Orchid soil spill and office trash
            n.Trash("paper_ball", 23.1f, 0.04f, 11.95f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_ball", 22.8f, 0.04f, 10.5f, TrashKind.Paper, "Paper ball");
            n.Trash("water_bottle", 24.3f, 0.73f, 13.1f, TrashKind.Recyclable, "Water bottle", 90);
            n.Trash("paper_cup", 12.9f, 0.02f, 4.2f, TrashKind.General, "Coffee cup");
            n.Trash("soda_can", 15.2f, 0.02f, 9.0f, TrashKind.Recyclable, "Soda can");
            n.Trash("paper_ball", 10.4f, 0.04f, 10.3f, TrashKind.Paper, "Paper ball");
            n.Trash("takeout_box", 14.6f, 0.76f, 12.6f, TrashKind.General, "Takeout box");
            CorkNote(n, "dana_office_key", -0.25f, 0.08f);
            CommonCloset(n);

            AllMonitorsOn(n);
            n.Monitors["marian"] = "Textures/Screens/marian_lock";
            n.MonitorDocs["marian"] = "screen_marian_lock";
            n.ChairsOut.Add("marian");
            n.ChairsOut.Add("guest_1");
            n.ChairsOut.Add("russ");

            n.Task("shelves", "Dust Marian's bookshelves", TaskKind.Clean, "office", "shelf_office_1", "shelf_office_2", "shelf_office_3", "shelf_office_4")
             .Task("desk", "Wipe Marian's desk", TaskKind.Clean, "office", "desk_marian")
             .Task("windows", "Squeegee the office windows", TaskKind.Clean, "office", "win_office_n1", "win_office_n2")
             .Task("floor", "Mop the office floor", TaskKind.Clean, "office", "floor_office")
             .Task("shredder", "Empty Marian's shredder", TaskKind.Flag, "office", "shred_bag_done")
             .Task("vacuum", "Vacuum the bullpen", TaskKind.Clean, "bullpen", "floor_bullpen")
             .Task("rec", "Wipe the reception desk", TaskKind.Clean, "reception", "desk_reception")
             .Task("trash", "Bin the rubbish", TaskKind.Trash, "office", "office", "bullpen", "reception")
             .Task("chairs", "Tuck in the chairs", TaskKind.Chairs, "office", "office", "bullpen")
             .Task("monitors", "Switch off the monitors", TaskKind.Monitors, "bullpen", "reception", "bullpen", "office")
             .Task("lights", "Lights off everywhere", TaskKind.Lights, "closet", "reception", "bullpen", "office")
             .Optional("east", "The east window too", TaskKind.Clean, "office", "win_office_e1")
             .Optional("desks", "Wipe Theo's and Priya's desks", TaskKind.Clean, "bullpen", "desk_theo", "desk_priya");

            n.Secret("notepad_rubbing", "Pressed into a legal pad")
             .Secret("northgate_invoices", "Cabinet FC-2")
             .Secret("marian_envelope", "A tip for your discretion")
             .Secret("screen_marian_lock", "Marian signs in at 1 AM")
             .Secret("shred_bag", "What she wants gone");

            n.Script = ctx =>
            {
                MarianDeskItems(ctx);
                var desk = ctx.Office.Anchor("ANCHOR_desk_marian");

                // Legal pad: rub a pencil over the indentations.
                var padPos = desk.TransformPoint(new Vector3(0.1f, 0.0f, 0.25f));
                var pad = ctx.Spawn(new SpawnDef { Prop = "notepad", Pos = padPos, Yaw = desk.eulerAngles.y + 8, Kind = SpawnKind.Decor, Id = "notepad" });
                var rub = Geo.Grime(ctx.Director.transform, "notepad_rub", padPos + Vector3.up * 0.0125f, Vector3.up, pad.transform.forward, new Vector2(0.2f, 0.27f), ToolKind.Cloth,
                    new GrimeSpec { GhostTexture = "Textures/Grime/notepad_rubbing", GhostMode = 0 }.WithSeed(4), 160f);
                rub.RevealOnly = true;
                rub.Verb = "Shade with a pencil";
                rub.DisplayName = "Legal pad";
                rub.Required = false;
                rub.FoamStrength = 0f;
                rub.Build();
                GameRoot.Instance.Cleaning.Hook(rub);
                bool revealed = false;
                rub.Completed += _ => { revealed = true; Story.OnDocRead(Docs.Get("notepad_blank")); ctx.Caption("[Words come up white through the graphite]", 3f); };
                ScriptedUse.Attach(pad, () => ctx.State.FateOf("notepad_rubbing") is Fate.Kept or Fate.Delivered or Fate.Shredded or Fate.Trashed ? null : revealed ? "Tear off the page" : "Look closer · legal pad", () =>
                {
                    if (!revealed) { InspectView.Show(Docs.Get("notepad_blank"), InspectMode.Read, null); return; }
                    var d = Docs.Get("notepad_rubbing");
                    Story.OnDocRead(d);
                    InspectView.Show(d, InspectMode.Evidence, c =>
                    {
                        if (c == InspectChoice.Keep) { ctx.State.SetFate(d.Id, Fate.Kept); ctx.Toast("Kept · " + d.Title, null, Ui.Accent); rub.gameObject.SetActive(false); }
                        else if (c == InspectChoice.Toss) { ctx.State.SetFate(d.Id, Fate.Trashed); rub.gameObject.SetActive(false); }
                    });
                });

                // FC-2
                var fc2 = GameObject.Find("FURN_filing_fc2");
                bool opened = false;
                if (fc2)
                    ScriptedUse.Attach(fc2, () => opened ? null : ctx.State.Has("has_key_fc2") ? "Unlock FC-2 with the brass key" : "Locked · FC-2", () =>
                    {
                        if (!ctx.State.Has("has_key_fc2")) { Sfx.Play("door_close", fc2.transform.position, 0.3f, 1.8f); ctx.Toast("Locked", "The tag on the cabinet says FC-2.", Palette.EvidenceRed, 2f); return; }
                        opened = true;
                        Sfx.Play("drawer_open", fc2.transform.position, 0.8f);
                        ctx.Spawn(new SpawnDef { Prop = "folder_manila", Pos = fc2.transform.position + new Vector3(-0.05f, 0.72f, 0), Yaw = 80, Kind = SpawnKind.Evidence, Doc = "northgate_invoices", Id = "northgate_invoices" })
                           .GetComponent<EvidenceItem>().Body.isKinematic = true;
                        ctx.Caption("[The top drawer slides open. One folder inside.]", 2.5f);
                    });

                // The envelope with $50
                var envPos = desk.TransformPoint(new Vector3(-0.35f, 0.0f, 0.2f));
                var env = ctx.Spawn(new SpawnDef { Prop = "envelope", Pos = envPos + Vector3.up * 0.004f, Yaw = desk.eulerAngles.y - 12, Kind = SpawnKind.Decor, Id = "envelope" });
                bool envDone = false;
                ScriptedUse.Attach(env, () => envDone ? null : "Read · envelope", () =>
                {
                    var d = Docs.Get("marian_envelope");
                    Story.OnDocRead(d);
                    InspectView.Show(d, InspectMode.Read, _ => ChoiceMenu.Show("Fifty dollars", "Nobody would know.", new System.Collections.Generic.List<ChoiceMenu.Option>
                    {
                        new("Take the money", "It's a tip. Probably.", () => { envDone = true; ctx.State.Set("took_money"); env.SetActive(false); Sfx.Play("paper_crumple", null, 0.4f); }),
                        new("Leave it on the desk", "Exactly where it was.", () => { envDone = true; ctx.State.Set("left_money"); }),
                    }));
                });

                // The shredder bag
                if (ctx.Furniture.Shredders.TryGetValue("office", out var shred))
                {
                    var bagGo = shred.gameObject;
                    var old = bagGo.GetComponent<Shredder>();
                    ScriptedUse.Attach(bagGo, () => ctx.State.Has("shred_bag_done") ? (ctx.State.Inventory.Count > 0 ? null : null) : "Empty the shredder bag", () =>
                    {
                        Story.OnDocRead(Docs.Get("shred_bag"));
                        Sfx.Play("paper_crumple", bagGo.transform.position, 0.6f);
                        ChoiceMenu.Show("Shredder bag", "Marian wants it straight down the chute.", new System.Collections.Generic.List<ChoiceMenu.Option>
                        {
                            new("Down the chute", "As asked. Gone by morning.", () => { ctx.State.Set("shred_bag_done"); ctx.State.SetFate("shred_bag", Fate.Trashed); Sfx.Play("bin_thunk", null, 0.6f); }),
                            new("Keep it on your cart", "Walt had tape. So do you.", () => { ctx.State.Set("shred_bag_done"); ctx.State.Set("kept_shreds"); ctx.State.SetFate("shred_bag", Fate.Kept); ctx.Toast("Shredder bag kept", "It's in your closet now.", Ui.Accent); }),
                        });
                    });
                    if (old) Object.Destroy(old);
                }
            };
            n.End = ctx =>
            {
                int moved = MarianDisturbed(ctx);
                int taken = new[] { "northgate_invoices", "notepad_rubbing" }.Count(id => ctx.State.FateOf(id) is Fate.Kept or Fate.Delivered or Fate.Shredded or Fate.Trashed);
                ctx.State.Suspicion += moved + taken;
                if (ctx.State.Has("shred_bag_done") && !ctx.State.Has("kept_shreds")) { } else if (!ctx.State.Has("shred_bag_done")) ctx.State.Suspicion += 1;
                Debug.Log($"[Night] suspicion now {ctx.State.Suspicion} (moved {moved}, taken {taken})");
            };

            n.Say("8:55 AM", "marian", "Thank you to whoever deep-cleaned my office. Everything is exactly where I left it.", s => s.Suspicion < 2)
             .Say("8:55 AM", "marian", "Someone has been through my desk. Dana, who has access to my office at night?", s => s.Suspicion >= 2)
             .Say("8:57 AM", "dana", "Only the cleaning agency, Marian… I'll call them.", s => s.Suspicion >= 2)
             .Say("9:30 AM", "theo", "has anyone seen the vendor folder from FC-2? asking for… me", s => s.FateOf("northgate_invoices") is Fate.Kept or Fate.Delivered or Fate.Shredded or Fate.Trashed)
             .Say("9:31 AM", "marian", "Theo. My office.", s => Delivered(s, "northgate_invoices", "marian"))
             .Say("10:02 AM", "priya", "rebooted FIN01. nobody should be on it overnight anyway. right?", s => s.Knows("logins"))
             .Say("11:15 AM", "russ", "FRIDAY DRINKS TOMORROW. bring snacks. I'm bringing the good confetti")
             .Say("11:16 AM", "dana", "Russ no", react: "★");
            return n;
        }

        // =========================================================================================
        // NIGHT 5 — Friday — Pieces
        // =========================================================================================

        static NightDef Night5()
        {
            var n = new NightDef { Number = 5, Day = "Friday", Title = "Pieces", Tagline = "After the party.", Rooms = new[] { "reception", "bullpen", "breakroom", "conference", "office" }, MusicIntensity = 1 };

            n.Grime("floor_bullpen", Spec(61).Add(Dust(0.15f, 0.25f, 1.2f))
                .Add(Confetti(2600, new Rect(BP(9.0f, 8.0f), new Vector2(0.55f, 0.55f))))
                .Add(Spill(BP(12.5f, 10.2f).x, BP(12.5f, 10.2f).y, 0.25f, SodaCol, 1.4f))
                .Add(Spill(BP(15.8f, 9.4f).x, BP(15.8f, 9.4f).y, 0.18f, new Color(0.5f, 0.12f, 0.15f), 1.5f))
                .Add(Trail(0.8f, 0.5f, BP(11.5f, 5.1f), BP(12.2f, 10f), BP(14.5f, 13.5f))));
            n.Grime("floor_break", Spec(62).Add(Dust(0.2f, 0.3f, 1.2f)).Add(Spill(BR(2.5f, 5.5f).x, BR(2.5f, 5.5f).y, 0.4f, SodaCol, 1.6f)).Add(Steps(BR(6.8f, 6.9f).x, BR(6.8f, 6.9f).y, BR(2f, 4f).x, BR(2f, 4f).y, 0.9f)).Add(Confetti(500, new Rect(0.2f, 0.3f, 0.6f, 0.5f))));
            n.Grime("counter_break", Spec(63).Add(Spill(0.25f, 0.5f, 0.12f, SodaCol)).Add(Ring(0.5f, 0.4f)).Add(Ring(0.55f, 0.6f)).Add(Crumbs(150, new Rect(0.1f, 0.2f, 0.8f, 0.6f))).Add(Confetti(80, new Rect(0.1f, 0.1f, 0.8f, 0.8f))));
            n.Grime("table_break", DeskDirt(64, 4));
            n.Grime("table_conf", DeskDirt(65, 3));
            n.Grime("desk_russ", DeskDirt(66, 3).Add(Confetti(120, new Rect(0.1f, 0.1f, 0.8f, 0.8f))));
            n.Grime("desk_walt", DeskDirt(67, 2).Add(Grease(0.5f, 0.5f, 0.1f)));
            n.Grime("desk_reception", DeskDirt(68, 2));
            n.Grime("coffeetable_reception", DeskDirt(69, 3));
            n.Grime("printer_top", Spec(70).Add(Dust(0.5f, 0.5f, 1f)).Add(Ring(0.5f, 0.5f, 0.04f)), false);

            // Party aftermath: lots of rubbish
            var rng = new Rng(555);
            (Vector3 c, float r, int count)[] piles = { (new Vector3(12.8f, 0, 10.0f), 2.2f, 10), (new Vector3(3.3f, 0, 6.3f), 1.6f, 6), (new Vector3(9.0f, 0, 2.6f), 1.2f, 3) };
            string[] kinds = { "paper_cup", "soda_can", "party_plate", "party_hat", "paper_ball", "water_bottle", "energy_can", "paper_cup" };
            foreach (var (c, r, count) in piles)
                for (int i = 0; i < count; i++)
                {
                    var off = rng.InsideUnitCircle() * r;
                    string k = kinds[rng.Range(0, kinds.Length)];
                    var tk = k is "soda_can" or "water_bottle" or "energy_can" ? TrashKind.Recyclable : k == "paper_ball" ? TrashKind.Paper : TrashKind.General;
                    n.Trash(k, c.x + off.x, 0.03f, c.z + off.y, tk, k switch { "paper_cup" => "Cup", "soda_can" => "Soda can", "party_plate" => "Paper plate", "party_hat" => "Party hat", "water_bottle" => "Bottle", "energy_can" => "Energy drink", _ => "Paper ball" }, rng.Range(0f, 360f));
                }
            n.Trash("pizza_box", 14.4f, 0.76f, 11.55f, TrashKind.General, "Pizza box");
            n.Trash("pizza_box", 3.3f, 0.76f, 6.6f, TrashKind.General, "Pizza box", 30);
            Mugs(n, new Vector3(13.9f, 0.76f, 12.3f), new Vector3(2.7f, 0.76f, 6.2f), new Vector3(9.0f, 0.43f, 2.3f), new Vector3(4.0f, 0.775f, 12.8f));

            // Story
            n.Evidence("vpn_log", 17.15f, 0.735f, 6.3f, 90);
            n.Evidence("theo_resignation", 11.2f, 0.04f, 11.0f);
            n.Spawns.Add(new SpawnDef { Prop = "sticky_note", Pos = new Vector3(15.92f, 1.55f, 1.9f), Pitch = 90, Yaw = -90, Roll = 180, Kind = SpawnKind.Readable, Doc = "marian_warning", Id = "marian_warning", When = s => s.Suspicion >= 2 });
            n.Decor("card_box", 14.9f, 0.0f, 13.2f, 20, s => Delivered(s, "russ_slip", "marian"));
            CommonCloset(n);

            AllMonitorsOn(n);
            foreach (var c in new[] { "theo", "priya", "russ", "walt", "dana", "break_1", "break_2", "break_3", "break_4", "conf_2", "conf_5" }) n.ChairsOut.Add(c);

            n.Task("vacuum", "Vacuum the party out of the bullpen", TaskKind.Clean, "bullpen", "floor_bullpen")
             .Task("mop", "Mop the sticky break room floor", TaskKind.Clean, "breakroom", "floor_break")
             .Task("counter", "Wipe the counter and the table", TaskKind.Clean, "breakroom", "counter_break", "table_break")
             .Task("desks", "Wipe Russ's and the hot desk", TaskKind.Clean, "bullpen", "desk_russ", "desk_walt")
             .Task("conf", "Wipe the conference table", TaskKind.Clean, "conference", "table_conf")
             .Task("rec", "Wipe reception: desk and coffee table", TaskKind.Clean, "reception", "desk_reception", "coffeetable_reception")
             .Task("trash", "Bin every last cup", TaskKind.Trash, "bullpen", "bullpen", "breakroom", "reception", "conference")
             .Task("mugs", "Mugs in the dish rack", TaskKind.Reset, "breakroom", "breakroom", "bullpen", "reception", "conference")
             .Task("chairs", "Tuck in the chairs", TaskKind.Chairs, "bullpen", "bullpen", "breakroom", "reception", "conference")
             .Task("monitors", "Switch off the monitors", TaskKind.Monitors, "bullpen", "reception", "bullpen")
             .Task("lights", "Lights off everywhere", TaskKind.Lights, "closet", "reception", "bullpen", "breakroom", "conference")
             .Optional("copier", "Wipe the copier", TaskKind.Clean, "bullpen", "printer_top");

            n.Secret("reconstructed_invoice", "Taped back together")
             .Secret("vpn_log", "Priya's printout")
             .Secret("theo_resignation", "Theo's draft")
             .Secret("walt_letter", "Above the ceiling tile")
             .Secret("priya_ally", "3:33 AM");

            n.Script = ctx =>
            {
                // The shred bag on the closet table.
                if (ctx.State.Has("kept_shreds") && ctx.State.FateOf("reconstructed_invoice") == Fate.Untouched)
                {
                    var t = ctx.Office.Anchor("ANCHOR_closet_table");
                    var bag = ctx.Spawn(new SpawnDef { Prop = "card_box", Pos = t.position + new Vector3(0.05f, 0, 0), Kind = SpawnKind.Decor, Id = "shred_bag_box" });
                    bag.transform.localScale = Vector3.one * 0.7f;
                    bool solved = false;
                    ScriptedUse.Attach(bag, () => solved ? null : "Tape the strips back together", () => ShredPuzzle.Show(() =>
                    {
                        solved = true;
                        var sheet = ctx.Spawn(new SpawnDef { Prop = "paper_sheet", Pos = t.position + new Vector3(-0.15f, 0.005f, -0.05f), Yaw = 20, Kind = SpawnKind.Evidence, Doc = "reconstructed_invoice", Id = "reconstructed_invoice" });
                        sheet.GetComponent<EvidenceItem>().Body.isKinematic = true;
                        sheet.GetComponent<EvidenceItem>().Interact(null);
                    }));
                }
                // Walt's UV trail ends at the closet ceiling.
                if (ctx.State.Has("has_uv_torch") && ctx.State.FateOf("walt_letter") == Fate.Untouched)
                {
                    UvArrow(ctx, "look_up", new Vector3(17.9f, 2.0f, 2.3f), 90, 0.55f, null, "[LOOK UP, in Walt's glowing handwriting]");
                    UvArrow(ctx, "in_here", new Vector3(16.2f, 2.875f, 3.0f), 0, 0.5f);
                    var tile = ctx.Office.Anchor("ANCHOR_ceiling_tile_closet");
                    var go = new GameObject("CeilingTile");
                    go.transform.position = tile.position + Vector3.down * 0.05f;
                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(0.6f, 0.08f, 0.6f);
                    bool done = false;
                    ScriptedUse.Attach(go, () => done ? null : "Push up the ceiling tile", () =>
                    {
                        done = true;
                        Sfx.Play("drawer_open", go.transform.position, 0.6f, 0.7f);
                        Fx.Burst(FxKind.Dust, go.transform.position, Vector3.down, 30, new Color(0.7f, 0.68f, 0.62f, 0.5f), 0.6f, 0.8f, 0.3f);
                        var env = ctx.Spawn(new SpawnDef { Prop = "envelope", Pos = go.transform.position + Vector3.down * 0.1f, Kind = SpawnKind.Evidence, Doc = "walt_letter", Id = "walt_letter" });
                        var ev = env.GetComponent<EvidenceItem>();
                        ev.Body.isKinematic = false;
                        ctx.Caption("[An envelope drops out of the dark]", 2.5f);
                    });
                }
                // 3:33 AM: the copier prints by itself.
                bool printed = false;
                ctx.Tick(dt =>
                {
                    if (printed || ctx.Director.ClockMinutes < 333f) return;
                    printed = true;
                    bool ally = Endings.KeyDocs.Concat(new[] { "russ_slip" }).Any(d => ctx.State.IsDelivered(d, "priya")) || ctx.State.AnyNoteTo("priya");
                    Sfx.Play("shredder", new Vector3(17.45f, 0.9f, 6.3f), 0.5f, 1.6f, 0f, AudioBus.Sfx, 0f, 18f);
                    var page = ctx.Spawn(new SpawnDef { Prop = "paper_sheet", Pos = new Vector3(17.15f, 0.76f, 6.0f), Yaw = 90, Kind = SpawnKind.Readable, Doc = ally ? "priya_ally" : "test_page", Id = "printout" });
                    ctx.Caption("[The copier wakes up and prints a single page]", 3f);
                });
            };
            n.End = ctx =>
            {
                if (ctx.Director.HasSecret("priya_ally")) ctx.State.Set("priya_contact");
            };

            n.Say("10:21 AM", "russ", "best friday drinks EVER. who cleaned up?? the office looks like nothing happened")
             .Say("10:22 AM", "dana", "The night shift is a saint. I'm buying them a plant.")
             .Say("10:40 AM", "priya", "for the record I printed nothing at 3:33 AM. my printer did. I'm choosing not to think about it", s => s.Has("priya_contact"))
             .Say("11:05 AM", "theo", "I'm still here. for now.", s => s.FateOf("theo_resignation") is Fate.Delivered or Fate.Kept)
             .Say("11:06 AM", "dana", "Theo ♥", s => s.FateOf("theo_resignation") is Fate.Delivered or Fate.Kept)
             .Say("2:30 PM", "marian", "Auditors on Monday. I'll be in over the weekend to prepare. Please don't come in. — M.C.")
             .Say("2:31 PM", "russ", "who comes in on weekends lol");
            return n;
        }

        // =========================================================================================
        // NIGHT 6 — Sunday — Prep
        // =========================================================================================

        static NightDef Night6()
        {
            var n = new NightDef { Number = 6, Day = "Sunday", Title = "Prep", Tagline = "Everything goes out with the morning pickup.", Rooms = new[] { "reception", "bullpen", "breakroom", "conference", "office" }, MusicIntensity = 1 };

            n.Grime("table_conf", Spec(71).Add(Dust(0.55f, 0.5f, 1f)).Add(Ring(0.3f, 0.5f)).Add(Ring(0.7f, 0.45f)));
            n.Grime("glasswall_conf", Spec(72).Add(Haze(0.35f)).Add(Prints(30, new Rect(0.1f, 0.35f, 0.8f, 0.35f))));
            n.Grime("floor_conf", Spec(73).Add(Dust(0.2f, 0.28f, 1.2f)).Add(Trail(0.6f, 0.45f, CF(6.8f, 10.5f), CF(3.5f, 11.4f))));
            n.Grime("whiteboard_conf", Spec(74).Add(Dust(0.3f, 0.35f, 1f)).Add(Smudge(0.3f, 0.5f, 0.2f)).Add(Smudge(0.7f, 0.4f, 0.18f)), false);
            n.Grime("glass_doors", WindowDirt(75, 0.45f, 30));
            n.Grime("floor_reception", Spec(76).Add(Dust(0.2f, 0.3f, 1.2f)).Add(Steps(RC(11.2f, 0.2f).x, RC(11.2f, 0.2f).y, RC(13.0f, 4.6f).x, RC(13.0f, 4.6f).y, 0.9f)).Add(Steps(RC(11.0f, 0.2f).x, RC(11.0f, 0.2f).y, RC(8.0f, 4.6f).x, RC(8.0f, 4.6f).y, 0.9f)));
            n.Grime("desk_marian", Spec(77).Add(Dust(0.4f, 0.4f, 1f)).Add(Ring(0.45f, 0.5f)));
            n.Grime("floor_office", Spec(78).Add(Steps(OF(18.3f, 10.45f).x, OF(18.3f, 10.45f).y, OF(24.0f, 10.3f).x, OF(24.0f, 10.3f).y, 0.85f)).Add(Steps(OF(24.0f, 10.3f).x, OF(24.0f, 10.3f).y, OF(23.0f, 12.6f).x, OF(23.0f, 12.6f).y, 0.85f)));
            n.Grime("desk_theo", DeskDirt(79, 1), false);
            n.Grime("counter_break", DeskDirt(80, 2), false);

            n.Trash("paper_ball", 23.4f, 0.04f, 10.2f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_ball", 24.0f, 0.04f, 11.0f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_ball", 22.9f, 0.04f, 13.9f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_cup", 21.4f, 0.775f, 12.2f, TrashKind.General, "Coffee cup");
            n.Trash("paper_cup", 13.4f, 0.02f, 4.3f, TrashKind.General, "Coffee cup");
            n.Trash("water_bottle", 5.2f, 0.775f, 13.0f, TrashKind.Recyclable, "Water bottle");

            // Water glasses for the auditors: set at each place.
            float[] gx = { 2.3f, 3.5f, 4.7f, 2.3f, 3.5f, 4.7f };
            float[] gz = { 12.15f, 12.15f, 12.15f, 13.05f, 13.05f, 13.05f };
            for (int i = 0; i < 6; i++)
            {
                var start = new Vector3(0.4f + (i % 3) * 0.12f, 0.73f, 12.2f + (i / 3) * 0.14f);
                n.Spawns.Add(new SpawnDef { Id = "glass" + i, Prop = "water_glass", Name = "Water glass", Home = new Vector3(gx[i], 0.752f, gz[i]), HasHome = true, Pos = start, Kind = SpawnKind.Reset, Group = "glasses" });
            }

            // Archive boxes for the morning pickup
            n.Decor("archive_box", 13.9f, 0.0f, 1.0f, 10, null, "archive_1");
            n.Decor("archive_box", 13.9f, 0.27f, 1.0f, -5, null, "archive_2");
            n.Decor("archive_box", 13.4f, 0.0f, 0.75f, 30, null, "archive_3");
            n.Spawns.Add(new SpawnDef { Prop = "sticky_note", Anchor = "ANCHOR_desk_dana", Pos = new Vector3(-0.4f, 0.03f, 0.13f), Kind = SpawnKind.Readable, Doc = "dana_doubt", Id = "dana_doubt" });
            n.Decor("card_box", 10.6f, 0.742f, 11.65f, 15, s => Delivered(s, "theo_note", "marian") || Delivered(s, "theo_planner", "marian"));
            CommonCloset(n);

            Monitors(n, ("dana", "Textures/Screens/login_dana"), ("priya", "Textures/Screens/login_priya"), ("marian", "Textures/Screens/marian_lock"));
            n.MonitorDocs["marian"] = "screen_marian_lock";
            for (int i = 1; i <= 6; i++) n.ChairsOut.Add("conf_" + i);
            n.ChairsOut.Add("marian");

            n.Task("table", "Polish the conference table", TaskKind.Clean, "conference", "table_conf")
             .Task("glasses", "Set a water glass at every place", TaskKind.Reset, "conference", "conference")
             .Task("chairs", "Tuck in the chairs", TaskKind.Chairs, "conference", "conference", "office")
             .Task("glasswall", "Squeegee the glass wall", TaskKind.Clean, "conference", "glasswall_conf")
             .Task("vacuum", "Vacuum the conference room", TaskKind.Clean, "conference", "floor_conf")
             .Task("doors", "Squeegee the entrance doors", TaskKind.Clean, "reception", "glass_doors")
             .Task("mop", "Mop reception", TaskKind.Clean, "reception", "floor_reception")
             .Task("office", "Marian's desk and floor", TaskKind.Clean, "office", "desk_marian", "floor_office")
             .Task("trash", "Bin the rubbish", TaskKind.Trash, "office", "office", "reception", "conference")
             .Task("monitors", "Switch off the monitors", TaskKind.Monitors, "bullpen", "reception", "bullpen", "office")
             .Task("lights", "Lights off everywhere", TaskKind.Lights, "closet", "reception", "bullpen", "conference", "office")
             .Optional("board", "Wipe down the whiteboard", TaskKind.Clean, "conference", "whiteboard_conf")
             .Optional("extra", "Theo's desk and the counter", TaskKind.Clean, "bullpen", "desk_theo", "counter_break");

            n.Secret("payment_ledger", "Inside a box marked DESTROY")
             .Secret("voicemail", "One new message")
             .Secret("dana_doubt", "Dana has doubts")
             .Secret("priya_forward", "Priya's hand-off");

            n.Script = ctx =>
            {
                ctx.Furniture.AddAuditorTray();
                float[] sx = { 2.3f, 3.5f, 4.7f, 2.3f, 3.5f, 4.7f };
                float[] sz = { 12.15f, 12.15f, 12.15f, 13.05f, 13.05f, 13.05f };
                for (int i = 0; i < 6; i++) Resettable.AddSlot("glasses", new Vector3(sx[i], 0.752f, sz[i]), Quaternion.identity);
                if (Endings.PriyaForwards(ctx.State))
                {
                    var tray = ctx.Furniture.Trays["auditor"];
                    var note = ctx.Spawn(new SpawnDef { Prop = "sticky_note", Pos = tray.transform.position + new Vector3(0.05f, 0.1f, 0.02f), Kind = SpawnKind.Readable, Doc = "priya_forward", Id = "priya_forward" });
                }
                // The archive box: look inside.
                var box = ctx.Get("archive_1");
                bool looked = false;
                if (box)
                    ScriptedUse.Attach(box, () => ctx.State.FateOf("payment_ledger") != Fate.Untouched && looked ? null : "Look inside · ARCHIVE — DESTROY", () =>
                    {
                        if (!looked)
                        {
                            looked = true;
                            Sfx.Play("drawer_open", box.transform.position, 0.6f, 1.3f);
                            InspectView.Show(Docs.Get("archive_label"), InspectMode.Read, _ =>
                            {
                                var b = ctx.Spawn(new SpawnDef { Prop = "binder", Pos = box.transform.position + new Vector3(-0.35f, 0.27f, 0.2f), Yaw = 0, Pitch = 90, Kind = SpawnKind.Evidence, Doc = "payment_ledger", Id = "payment_ledger" });
                                b.GetComponent<EvidenceItem>().Body.isKinematic = true;
                                ctx.Caption("[Under the junk, a ledger binder]", 2.5f);
                            });
                        }
                    });
                // Voicemail on the reception phone.
                if (ctx.Furniture.Named.TryGetValue("phone_reception", out var phone))
                {
                    bool played = false;
                    ScriptedUse.Attach(phone, () => played ? null : "Play voicemail (1 new)", () =>
                    {
                        played = true;
                        Sfx.Play("ui_click", phone.transform.position, 0.6f);
                        InspectView.Show(Docs.Get("voicemail"), InspectMode.Read, null);
                        Story.OnDocRead(Docs.Get("voicemail"));
                    });
                }
            };

            n.Say("9:00 AM", "dana", "Brightwater arrives at 9 tomorrow. Conference room is ready and GORGEOUS.")
             .Say("9:12 AM", "marian", "Who moved the archive boxes? They were to be collected.", s => s.FateOf("payment_ledger") is Fate.Kept or Fate.Delivered or Fate.Shredded)
             .Say("9:30 AM", "priya", "@Erin Sato welcome! I left something in your tray. Ask me anything.", s => Endings.PriyaForwards(s))
             .Say("9:31 AM", "auditor", "Thank you, Priya. Noted.", s => Endings.PriyaForwards(s))
             .Say("11:59 PM", "marian", "I'll be in early tomorrow.");
            return n;
        }

        // =========================================================================================
        // NIGHT 7 — Monday — Audit Day
        // =========================================================================================

        static NightDef Night7()
        {
            var n = new NightDef { Number = 7, Day = "Monday", Title = "Audit Day", Tagline = "Decide what survives.", Rooms = new[] { "reception", "bullpen", "breakroom", "conference", "office" }, Storm = true, MusicIntensity = 1 };

            n.Grime("floor_office", Spec(81).Add(Dust(0.2f, 0.3f, 1.2f)).Add(Steps(OF(18.3f, 10.45f).x, OF(18.3f, 10.45f).y, OF(23.4f, 9.7f).x, OF(23.4f, 9.7f).y, 0.95f)).Add(Spill(OF(22.6f, 11.4f).x, OF(22.6f, 11.4f).y, 0.2f, CoffeeCol)).Add(Confetti(300, new Rect(OF(22.8f, 9.4f), new Vector2(0.18f, 0.12f)))));
            n.Grime("desk_marian", DeskDirt(82, 3));
            n.Grime("floor_reception", Spec(83).Add(Steps(RC(11.2f, 0.2f).x, RC(11.2f, 0.2f).y, RC(13.0f, 4.8f).x, RC(13.0f, 4.8f).y, 0.95f)).Add(Spill(RC(11.2f, 0.6f).x, RC(11.2f, 0.6f).y, 0.4f, MudCol, 1.2f)));
            n.Grime("glass_doors", WindowDirt(84, 0.5f, 40).Add(DripLines(30, new Rect(0.05f, 0.1f, 0.9f, 0.85f), new Color(0.6f, 0.62f, 0.62f))));
            n.Grime("table_conf", DeskDirt(85, 2));
            n.Grime("desk_reception", DeskDirt(86, 2));
            n.Grime("floor_bullpen", Spec(87).Add(Dust(0.15f, 0.25f, 1.2f)).Add(Trail(0.7f, 0.5f, BP(11.5f, 5.1f), BP(12.3f, 9.5f), BP(17.9f, 10.5f))).Add(Steps(BP(11.5f, 5.2f).x, BP(11.5f, 5.2f).y, BP(17.8f, 10.4f).x, BP(17.8f, 10.4f).y, 0.8f)));
            n.Grime("win_bullpen_2", WindowDirt(88, 0.5f), false);

            // Shred strips everywhere in the office
            var rng = new Rng(777);
            for (int i = 0; i < 9; i++)
            {
                var off = rng.InsideUnitCircle() * 1.6f;
                n.Trash("paper_ball", 22.8f + off.x, 0.04f, 11.0f + off.y * 0.8f, TrashKind.Paper, "Shredded paper");
            }
            n.Trash("paper_cup", 22.3f, 0.775f, 13.2f, TrashKind.General, "Coffee cup");
            n.Trash("paper_cup", 12.2f, 1.13f, 3.4f, TrashKind.General, "Coffee cup");
            n.Evidence("flight_note", 23.25f, 0.04f, 11.75f, 40);
            n.Decor("archive_box", 13.9f, 0.0f, 1.0f, 10, s => s.FateOf("payment_ledger") == Fate.Untouched);
            CommonCloset(n);

            Monitors(n, ("marian", "Textures/Screens/remote_session"), ("dana", "Textures/Screens/login_dana"));
            n.MonitorDocs["marian"] = "screen_remote";
            foreach (var c in new[] { "marian", "guest_1", "guest_2", "theo" }) n.ChairsOut.Add(c);

            n.Task("jam", "Clear the jammed shredder in Marian's office", TaskKind.Flag, "office", "jam_cleared")
             .Task("office_floor", "Clean up Marian's office floor", TaskKind.Clean, "office", "floor_office")
             .Task("office_desk", "Wipe Marian's desk", TaskKind.Clean, "office", "desk_marian")
             .Task("rec_floor", "Mop the muddy entrance", TaskKind.Clean, "reception", "floor_reception")
             .Task("doors", "Squeegee the rain off the doors", TaskKind.Clean, "reception", "glass_doors")
             .Task("conf", "Wipe the conference table", TaskKind.Clean, "conference", "table_conf")
             .Task("vacuum", "Vacuum the bullpen", TaskKind.Clean, "bullpen", "floor_bullpen")
             .Task("trash", "Bin the rubbish", TaskKind.Trash, "office", "office", "reception")
             .Task("chairs", "Tuck in the chairs", TaskKind.Chairs, "office", "office", "bullpen")
             .Task("lights", "Lights off everywhere", TaskKind.Lights, "closet", "reception", "bullpen", "office", "conference")
             .Optional("rec_desk", "Wipe the reception desk", TaskKind.Clean, "reception", "desk_reception")
             .Optional("window", "One last window", TaskKind.Clean, "bullpen", "win_bullpen_2");

            n.Secret("red_folder", "The red folder")
             .Secret("flight_note", "One way")
             .Secret("screen_remote", "She's still logged in");

            n.Script = ctx =>
            {
                ctx.Furniture.AddAuditorTray();
                if (ctx.Office.Rooms.TryGetValue("office", out var office)) office.SetLights(true, true);
                if (ctx.Office.Doors.TryGetValue("office", out var door)) door.SetOpen(true, new Vector3(16f, 0, 10.5f), true);
                // The red folder, jammed in the shredder.
                if (ctx.Furniture.Shredders.TryGetValue("office", out var shred))
                {
                    shred.Jammed = true;
                    var stuck = ctx.Spawn(new SpawnDef { Prop = "folder_red", Pos = shred.transform.position + new Vector3(0, 0.66f, 0.02f), Pitch = -70, Kind = SpawnKind.Decor, Id = "red_stuck" });
                    foreach (var c in stuck.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                    shred.OnUnjam = () =>
                    {
                        ctx.State.Set("jam_cleared");
                        stuck.SetActive(false);
                        var f = ctx.Spawn(new SpawnDef { Prop = "folder_red", Pos = shred.transform.position + new Vector3(0.3f, 0.66f, 0.3f), Yaw = 30, Kind = SpawnKind.Evidence, Doc = "red_folder", Id = "red_folder" });
                        var ev = f.GetComponent<EvidenceItem>();
                        ev.Body.isKinematic = true;
                        ctx.Caption("[The jam pulls free: a red folder, half-chewed]", 3f);
                        ctx.Delay(0.6f, () => ev.Interact(null));
                    };
                }
                // The storm: thunder and the lights stutter.
                float next = 25f;
                ctx.Tick(dt =>
                {
                    next -= dt;
                    if (next > 0) return;
                    next = Random.Range(35f, 70f);
                    Sfx.Play("thunder", null, 0.8f, Random.Range(0.85f, 1.1f), 0f, AudioBus.Ambience);
                    PostFx.Instance?.Flicker(1f);
                    foreach (var r in ctx.Office.Rooms.Values)
                        if (r.LightsOn) { r.SetLights(false, true); var rr = r; ctx.Delay(Random.Range(0.08f, 0.3f), () => rr.SetLights(true, true)); }
                });
            };
            n.End = ctx => { };
            return n;
        }
    }
}
