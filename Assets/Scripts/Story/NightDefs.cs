using System.Collections.Generic;
using UnityEngine;
using static AfterHours.GrimeStamp;

namespace AfterHours
{
    /// <summary>The seven nights: dirt, props, tasks, secrets, scripted beats and the morning chat.</summary>
    public static partial class NightDefs
    {
        static readonly Dictionary<int, NightDef> cache = new();

        public static NightDef Get(int n)
        {
            // Rebuilt on every request so conditions read the latest story state.
            NightDef d = n switch
            {
                1 => Night1(), 2 => Night2(), 3 => Night3(), 4 => Night4(), 5 => Night5(), 6 => Night6(), 7 => Night7(),
                _ => null,
            };
            if (d != null) cache[n] = d;
            return d;
        }

        public const int Count = 7;

        public static readonly string[] Days = { "", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Sunday", "Monday" };
        static readonly string[] Week = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        /// <summary>The calendar day after night <paramref name="n"/>'s shift (the morning chat's day).</summary>
        public static string MorningAfter(int n) => Week[(System.Array.IndexOf(Week, Days[n]) + 1) % 7];

        public static readonly string[] Titles = { "", "First Shift", "Glass", "The Whiteboard", "The Corner Office", "Pieces", "Prep", "Audit Eve" };

        // ---- shared helpers ----------------------------------------------------------------------

        static readonly Color CoffeeCol = new(0.33f, 0.19f, 0.09f);
        static readonly Color SodaCol = new(0.55f, 0.38f, 0.14f);
        static readonly Color EnergyCol = new(0.62f, 0.7f, 0.22f);
        static readonly Color MudCol = new(0.26f, 0.21f, 0.15f);

        static GrimeSpec Spec(int seed) => new GrimeSpec().WithSeed(seed);

        /// <summary>Floor uv helper for the bullpen carpet (world x,z → uv).</summary>
        static Vector2 BP(float x, float z) => new((x - 7.05f) / 10.9f, (z - 5.05f) / 10.9f);
        static Vector2 RC(float x, float z) => new((x - 7.1f) / 7.8f, (z - 0.1f) / 4.8f);
        static Vector2 BR(float x, float z) => new((x - 0.05f) / 6.9f, (z - 3.05f) / 5.9f);
        static Vector2 CF(float x, float z) => new((x - 0.05f) / 6.9f, (z - 9.05f) / 6.9f);
        static Vector2 OF(float x, float z) => new((x - 18.05f) / 6.9f, (z - 9.05f) / 6.9f);

        static bool Delivered(StoryState s, string doc, string to) => s.IsDelivered(doc, to);

        static void Monitors(NightDef n, params (string desk, string screen)[] screens)
        {
            foreach (var (desk, screen) in screens) n.Monitors[desk] = screen;
        }

        static void CommonCloset(NightDef n)
        {
            n.Spawns.Add(new SpawnDef { Prop = "paper_sheet", Anchor = "ANCHOR_corkboard_closet", Pos = new Vector3(0.3f, -0.05f, 0.012f), Pitch = 90, Roll = 180, Kind = SpawnKind.Decor });
        }

        // =========================================================================================
        // NIGHT 1 — Monday — First Shift
        // =========================================================================================

        static NightDef Night1()
        {
            var n = new NightDef { Number = 1, Day = "Monday", Title = "First Shift", Tagline = "Clean what they ask.", Rooms = new[] { "reception", "bullpen" } };

            n.Grime("desk_reception", Spec(11).Add(Dust(0.5f, 0.45f, 1f)).Add(Ring(0.3f, 0.45f, 0.042f)).Add(Ring(0.345f, 0.55f, 0.042f)).Add(Spill(0.72f, 0.5f, 0.05f, CoffeeCol)).Add(Crumbs(40, new Rect(0.5f, 0.1f, 0.2f, 0.8f))));
            n.Grime("coffeetable_reception", Spec(12).Add(Dust(0.45f, 0.4f, 1f)).Add(Ring(0.3f, 0.5f, 0.04f)).Add(Ring(0.66f, 0.38f, 0.045f)).Add(Crumbs(70, new Rect(0.45f, 0.3f, 0.3f, 0.4f))));
            n.Grime("desk_theo", Spec(13).Add(Dust(0.45f, 0.4f, 1f)).Add(Ring(0.24f, 0.33f)).Add(Ring(0.29f, 0.41f)).Add(Crumbs(40, new Rect(0.55f, 0.3f, 0.25f, 0.4f))));
            n.Grime("desk_priya", Spec(14).Add(Dust(0.4f, 0.35f, 1f)).Add(Spill(0.66f, 0.4f, 0.05f, EnergyCol)).Add(Ring(0.75f, 0.62f, 0.032f)).Add(Ring(0.71f, 0.3f, 0.032f)));
            n.Grime("desk_russ", Spec(15).Add(Dust(0.4f, 0.35f, 1f)).Add(Grease(0.45f, 0.5f, 0.11f)).Add(Crumbs(140, new Rect(0.25f, 0.25f, 0.5f, 0.5f))).Add(Ring(0.72f, 0.6f)).Add(Confetti(60, new Rect(0.2f, 0.2f, 0.6f, 0.6f))));
            n.Grime("floor_bullpen", Spec(16)
                .Add(Dust(0.15f, 0.25f, 1.2f))
                .Add(Trail(0.75f, 0.5f, BP(11.5f, 5.1f), BP(12.3f, 9.5f), BP(12.4f, 12.0f), BP(12.6f, 15.2f)))
                .Add(Trail(0.55f, 0.45f, BP(12.3f, 9.6f), BP(10.3f, 10.5f)))
                .Add(Trail(0.55f, 0.45f, BP(12.3f, 9.6f), BP(14.4f, 10.5f), BP(17.0f, 7.0f)))
                .Add(Confetti(1400, new Rect(BP(12.6f, 9.5f), new Vector2(0.27f, 0.32f))))
                .Add(Crumbs(220, new Rect(BP(12.8f, 10.0f), new Vector2(0.18f, 0.2f))))
                .Add(Spill(BP(13.3f, 10.4f).x, BP(13.3f, 10.4f).y, 0.22f, SodaCol, 1.4f)));
            n.Grime("rug_reception", Spec(17).Add(Dust(0.35f, 0.35f, 0.8f)).Add(Crumbs(160, new Rect(0.3f, 0.35f, 0.4f, 0.3f))), false);

            // Trash: the tail end of Russ's birthday.
            n.Trash("paper_cup", 8.62f, 0.44f, 2.1f, TrashKind.General, "Coffee cup", 20);
            n.Trash("soda_can", 8.25f, 0.02f, 1.05f, TrashKind.Recyclable, "Soda can", 0);
            n.Trash("paper_ball", 12.85f, 0.04f, 2.35f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_cup", 13.9f, 0.02f, 4.05f, TrashKind.General, "Coffee cup", 120);
            n.Trash("pizza_box", 14.35f, 0.76f, 12.45f, TrashKind.General, "Pizza box", 12);
            n.Trash("party_plate", 14.0f, 0.76f, 11.45f, TrashKind.General, "Paper plate");
            n.Trash("party_plate", 13.15f, 0.01f, 10.55f, TrashKind.General, "Paper plate", 40);
            n.Trash("party_hat", 15.6f, 0.02f, 10.85f, TrashKind.General, "Party hat", 0);
            n.Trash("energy_can", 10.78f, 0.76f, 12.6f, TrashKind.Recyclable, "Energy drink");
            n.Trash("soda_can", 12.6f, 0.02f, 12.95f, TrashKind.Recyclable, "Soda can");
            n.Trash("paper_ball", 9.55f, 0.04f, 10.65f, TrashKind.Paper, "Paper ball");
            n.Trash("paper_ball", 11.65f, 0.04f, 9.75f, TrashKind.Paper, "Paper ball");
            n.Trash("banana_peel", 14.95f, 0.02f, 10.15f, TrashKind.General, "Banana peel", 70);
            n.Evidence("theo_note", 10.78f, 0.04f, 10.55f);

            // Small resets (optional): Dana's stapler and Theo's binder.
            n.Reset("dana_stapler", "stapler", "Stapler", new Vector3(0.15f, 0f, 0.05f), 10, new Vector3(0.9f, -0.73f, 0.55f), 70, anchor: "ANCHOR_desk_dana");
            n.Reset("theo_photo", "photo_frame", "Photo frame", new Vector3(-0.35f, 0f, -0.28f), 160, new Vector3(-0.2f, 0.02f, 0.2f), 30, anchor: "ANCHOR_desk_theo");

            // Closet: Dana's welcome note and the BrightStar rules sheet.
            n.Spawns.Add(new SpawnDef { Prop = "sticky_note", Anchor = "ANCHOR_corkboard_closet", Pos = new Vector3(-0.25f, 0.08f, 0.012f), Pitch = 90, Roll = 180, Kind = SpawnKind.Readable, Doc = "dana_welcome", Id = "dana_welcome", Verb = "Read" });
            CommonCloset(n);

            Monitors(n, ("dana", "Textures/Screens/login_dana"), ("theo", "Textures/Screens/email_theo"), ("priya", "Textures/Screens/login_priya"), ("russ", "Textures/Screens/screensaver"));
            n.MonitorDocs["theo"] = "screen_theo_email";
            foreach (var c in new[] { "dana", "theo", "priya", "russ" }) n.ChairsOut.Add(c);

            n.Task("desk_reception", "Wipe the reception desk", TaskKind.Clean, "reception", "desk_reception")
             .Task("coffee_table", "Wipe the coffee table", TaskKind.Clean, "reception", "coffeetable_reception")
             .Task("trash", "Bin every bit of rubbish", TaskKind.Trash, "bullpen", "reception", "bullpen")
             .Task("desks", "Wipe Theo's, Priya's and Russ's desks", TaskKind.Clean, "bullpen", "desk_theo", "desk_priya", "desk_russ")
             .Task("vacuum", "Vacuum the bullpen carpet", TaskKind.Clean, "bullpen", "floor_bullpen")
             .Task("chairs", "Tuck in the chairs", TaskKind.Chairs, "bullpen", "reception", "bullpen")
             .Task("monitors", "Switch off the monitors", TaskKind.Monitors, "bullpen", "reception", "bullpen")
             .Task("lights", "Lights off in reception and the bullpen", TaskKind.Lights, "closet", "reception", "bullpen")
             .Optional("rug", "Vacuum the lobby rug", TaskKind.Clean, "reception", "rug_reception")
             .Optional("tidy", "Put things back where they belong", TaskKind.Reset, "bullpen", "reception", "bullpen");

            n.Secret("theo_note", "A crumpled note under Theo's desk")
             .Secret("screen_theo_email", "Theo's open email")
             .Secret("key_fc2", "A brass key nobody mentioned")
             .Secret("walt_note_1", "Walt's locker")
             .Secret("screen_remote", "Someone is logged in at 1 AM");

            n.Script = ctx =>
            {
                // Walt's locker.
                var locker = GameObject.Find("FURN_locker_walt");
                if (locker)
                {
                    var r = locker.GetComponent<Readable>() ?? locker.AddComponent<Readable>();
                    r.Doc = "walt_note_1";
                    r.Label = "Open W. Bremner's locker";
                    r.OnRead = () => Sfx.Play("drawer_open", locker.transform.position, 0.6f);
                }
                KeyUnderDesk(ctx, new Vector3(10.45f, 0.01f, 11.75f), "key_fc2", "has_key_fc2");
                RemoteSessionBeat(ctx, "walt");
            };

            n.Say("7:58 AM", "dana", "Morning all! ☀ New night cleaner started last night. My desk is SPARKLING ★", s => true, "♥")
             .Say("8:03 AM", "russ", "who vacuumed up my birthday confetti. that was ART")
             .Say("8:04 AM", "priya", "it was a fire hazard, Russ")
             .Say("8:31 AM", "theo", "who left this on my desk", s => Delivered(s, "theo_note", "theo"))
             .Say("8:32 AM", "priya", "left what?", s => Delivered(s, "theo_note", "theo"))
             .Say("8:33 AM", "theo", "nothing. never mind. sorry", s => Delivered(s, "theo_note", "theo"))
             .Say("8:47 AM", "priya", "Someone left me something interesting. Not saying who. Thanks, whoever.", s => Delivered(s, "theo_note", "priya"))
             .Say("9:02 AM", "marian", "Theo, my office please.", s => Delivered(s, "theo_note", "marian"))
             .Say("9:02 AM", "russ", "ooooooh", s => Delivered(s, "theo_note", "marian"))
             .Say("9:03 AM", "dana", "Russ.", s => Delivered(s, "theo_note", "marian"))
             .Say("8:15 AM", "dana", "Found a crumpled note on my keyboard?? Theo, can you come by reception when you're in", s => Delivered(s, "theo_note", "dana"))
             .Say("8:20 AM", "russ", "someone put THEO'S LOVE LETTER on my desk lmao", s => Delivered(s, "theo_note", "russ"))
             .Say("8:21 AM", "theo", "it's not a love letter. please give it back", s => Delivered(s, "theo_note", "russ"))
             .Say("8:50 AM", "theo", "did the cleaner skip my desk? there was paper under my chair. never mind", s => s.FateOf("theo_note") is Fate.Untouched or Fate.Seen)
             .Say("9:30 AM", "marian", "Reminder: Brightwater auditors arrive Tuesday the 15th. Desks tidy, files in order. — M.C.")
             .Say("9:31 AM", "russ", "✓✓✓")
             .Say("2:14 PM", "priya", "weird question. anyone else's machine wake up overnight? my VPN logs are noisy", s => s.Knows("logins"));
            return n;
        }

        // ---- shared beats ------------------------------------------------------------------------

        /// <summary>The vacuum clunks on a key hidden under a desk.</summary>
        static void KeyUnderDesk(NightContext ctx, Vector3 pos, string secret, string flag)
        {
            if (ctx.State.Has(flag)) return;
            var floor = ctx.Surface("floor_bullpen");
            bool found = false;
            ctx.Tick(dt =>
            {
                if (found || floor == null || !floor.gameObject.activeSelf) return;
                if (!floor.WorldToUv(pos, out var uv) || floor.RemainingAt(uv) > 0.35f) return;
                found = true;
                Sfx.Play("vacuum_clunk", pos, 0.9f);
                GameRoot.Instance.Player.Kick(1.2f);
                var key = ctx.Spawn(new SpawnDef { Prop = "key", Pos = pos + Vector3.up * 0.05f + Vector3.back * 0.25f, Kind = SpawnKind.Decor, Id = "key" });
                var rb = key.AddComponent<Rigidbody>();
                rb.mass = 0.02f;
                rb.linearVelocity = new Vector3(0.6f, 1.6f, -1.2f);
                key.layer = Layers.Prop;
                var k = key.AddComponent<KeyPickup>();
                k.Flag = flag;
                k.Secret = secret;
                ctx.Caption("[Clunk! Something was stuck under the desk]", 2.5f);
            });
        }

        /// <summary>When the bullpen goes dark, the empty desk's monitor wakes up on its own.</summary>
        static void RemoteSessionBeat(NightContext ctx, string desk)
        {
            bool fired = false;
            ctx.On(GameEvent.LightsChanged, room =>
            {
                if (fired || room != "bullpen" || ctx.Room("bullpen").LightsOn) return;
                if (ctx.Director.Elapsed < 60f) return;
                fired = true;
                ctx.Delay(1.2f, () =>
                {
                    var m = ctx.Monitor(desk);
                    if (m == null) return;
                    m.StoryOwned = true;
                    m.ScreenDoc = "screen_remote";
                    m.SetScreen("Textures/Screens/remote_session");
                    m.SetOn(true);
                    Sfx.Play("discover", m.transform.position, 0.7f);
                    ctx.Caption("[A monitor wakes up on the empty desk]", 3f);
                    ctx.Delay(45f, () => { if (m.On) { m.SetOn(false); ctx.Caption("[The screen goes dark. Session ended.]", 2.5f); } });
                });
            });
        }
    }

    /// <summary>A small key: picking it up puts it on your key ring.</summary>
    public class KeyPickup : MonoBehaviour, IInteractable
    {
        public string Flag, Secret;
        public string Prompt(Interactor who) => "Pick up · small brass key";
        public void Interact(Interactor who)
        {
            Story.State.Set(Flag);
            NightDirector.Instance?.FindSecret(Secret);
            Sfx.Play("can_clank", transform.position, 0.6f, 1.4f);
            Hud.Instance?.Toast("Small brass key", "The paper tag says FC-2. On your key ring now.", Ui.Accent, 3f);
            gameObject.SetActive(false);
        }
    }
}
