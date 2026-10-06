using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>Shared helpers for full-screen interstitials.</summary>
    public abstract class Interstitial : MonoBehaviour
    {
        protected RectTransform root;
        protected CanvasGroup group;
        protected Action done;
        protected float age;          // seconds visible, robust to long loading frames
        static readonly HashSet<Interstitial> open = new();
        /// <summary>True while any interstitial is showing or still fading out.</summary>
        public static bool AnyOpen => open.Count > 0;

        protected virtual void LateUpdate() => age += Mathf.Min(GameTime.UnscaledDelta, 0.05f);

        protected void Setup(string name, int order, Color bg)
        {
            root = Ui.Layer(name, order);
            group = root.GetComponent<CanvasGroup>();
            var b = Ui.Image(root, "Bg", bg);
            Ui.Stretch(b.rectTransform);
            b.raycastTarget = true;
            group.alpha = 0;
            age = 0f;
            open.Add(this);
            Tween.Run(0.5f, k => group.alpha = k, Ease.OutCubic);
        }

        protected static bool Advance()
        {
            var mouse = Mouse.current;
            if (AutoAdvance) { AutoAdvance = false; return true; }
            return GameInput.Menu.Confirm || GameInput.Menu.Start
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        /// <summary>Automation: advance the current interstitial once.</summary>
        public static bool AutoAdvance;

        protected void Finish()
        {
            var cb = done;
            done = null;
            Tween.Run(0.45f, k => { if (group) group.alpha = 1 - k; }, Ease.InCubic, () =>
            {
                open.Remove(this);
                if (root) Destroy(root.gameObject);
                if (this) Destroy(gameObject);
            });
            cb?.Invoke();
        }

        protected virtual void OnDestroy()
        {
            open.Remove(this);
            if (root) Destroy(root.gameObject);
        }
    }

    // =============================================================================================
    // Night title card
    // =============================================================================================

    public class TitleCard : Interstitial
    {
        static readonly string[] Words = { "", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN" };
        float hold = 4.2f;

        public static void Show(NightDef def, Action onDone)
        {
            var go = new GameObject("TitleCard");
            var t = go.AddComponent<TitleCard>();
            t.done = onDone;
            t.Build(def);
        }

        void Build(NightDef def)
        {
            Setup("TitleCard", 80, new Color(0.012f, 0.016f, 0.03f, 1f));
            group.alpha = 1;
            var card = Ui.Panel(root, "PunchCard", Palette.Hex("EFE6CF"), 10);
            var rt = card.rectTransform;
            Ui.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(980, 420));
            rt.localRotation = Quaternion.Euler(0, 0, -1.2f);
            var stripe = Ui.Image(rt, "Stripe", Palette.Hex("D9C9A3"), Ui.Rounded(4));
            Ui.Place(stripe.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(940, 44), new Vector2(0.5f, 1));
            var hdr = Ui.Label(rt, "BRIGHTSTAR JANITORIAL  ·  TIME CARD  ·  MERIDIAN TOWER STE 1408", UiFont.Mono, 20, Palette.Hex("6B5A3A"), TextAlignmentOptions.Center);
            Ui.Place(hdr.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(940, 30), new Vector2(0.5f, 1));
            big = Ui.Label(rt, "", UiFont.Type, 96, Palette.Hex("1E2430"), TextAlignmentOptions.Center);
            Ui.Place(big.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(940, 120));
            full = "NIGHT " + Words[def.Number];
            Type(0);
            var sub = Ui.Label(rt, $"{def.Day.ToUpperInvariant()}  —  {def.Title}", UiFont.SansMedium, 34, Palette.Hex("3A3F4A"), TextAlignmentOptions.Center);
            Ui.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(940, 50));
            sub.alpha = 0;
            // Handwritten in the bottom-left, clear of the IN stamp on the right.
            var tag = Ui.Label(rt, def.Tagline, UiFont.Hand, 40, Palette.Hex("1E3A6E"), TextAlignmentOptions.Left);
            Ui.Place(tag.rectTransform, new Vector2(0, 0), new Vector2(60, 36), new Vector2(590, 60), new Vector2(0, 0));
            tag.enableAutoSizing = true;
            tag.fontSizeMin = 28;
            tag.fontSizeMax = 40;
            tag.alpha = 0;
            var stamp = Ui.Label(rt, "IN  10:02 PM", UiFont.Mono, 30, new Color(0.75f, 0.15f, 0.12f, 0.9f), TextAlignmentOptions.Center);
            Ui.Place(stamp.rectTransform, new Vector2(1, 0), new Vector2(-170, 96), new Vector2(260, 50), new Vector2(0.5f, 0.5f));
            stamp.alpha = 0;
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, 8);

            // The title types itself out in Update, then the stamp punches.
            Tween.Run(0.6f, k => { if (sub) sub.alpha = k; }, Ease.OutCubic, null, 1.2f);
            Tween.Run(0.8f, k => { if (tag) tag.alpha = k; }, Ease.OutCubic, null, 1.7f);
            Tween.Delay(2.3f, () =>
            {
                if (!stamp) return;
                Sfx.Play("punch_clock", null, 0.8f);
                stamp.alpha = 1;
                Tween.Run(0.25f, k => { if (stamp) stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, k); }, Ease.OutCubic);
                Tween.Run(0.3f, k => { if (rt) rt.anchoredPosition = new Vector2(Mathf.Sin(k * 40) * 6 * (1 - k), 10); }, Ease.Linear);
            });
        }

        TextMeshProUGUI big;
        string full;
        int typed;

        /// <summary>Show the first <paramref name="n"/> letters; the rest stay laid out but transparent.</summary>
        void Type(int n)
        {
            typed = n;
            big.text = full.Substring(0, n) + (n < full.Length ? "<alpha=#00>" + full.Substring(n) : "");
        }

        void Update()
        {
            int want = Mathf.Clamp(Mathf.FloorToInt((age - 0.35f) / 0.07f), 0, full.Length);
            while (typed < want)
            {
                Type(typed + 1);
                if (full[typed - 1] != ' ') Sfx.Play("ui_hover", null, 0.35f, 0.7f, 0.1f, AudioBus.Ui);
            }
            if (done != null && (age > hold || (age > 0.6f && Advance()))) Finish();
        }
    }

    // =============================================================================================
    // Shift report
    // =============================================================================================

    public class ShiftReport : Interstitial
    {
        public static void Show(NightDef def, NightResult result, NightDirector dir, Action onDone)
        {
            var go = new GameObject("ShiftReport");
            var s = go.AddComponent<ShiftReport>();
            s.done = onDone;
            s.Build(def, result, dir);
        }

        void Build(NightDef def, NightResult r, NightDirector dir)
        {
            Setup("ShiftReport", 70, new Color(0.02f, 0.025f, 0.04f, 0.94f));
            var board = Ui.Panel(root, "Clipboard", Palette.Hex("8A5A34"), 22);
            var brt = board.rectTransform;
            bool photos = RoomPhotos.Instance != null && RoomPhotos.Instance.After.Count > 0;
            Ui.Place(brt, new Vector2(0.5f, 0.5f), new Vector2(photos ? -330 : 0, -10), new Vector2(860, 940));
            if (photos) PolaroidWipe.Create(root, def);
            // Clipboard and polaroid need ~1700 units across; narrower screens (4:3 is 1440) shrink both.
            float canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            float fit = Mathf.Min(1f, (canvasW - 60f) / 1700f);
            if (fit < 1f)
                foreach (var part in new[] { brt, photos ? root.Find("Polaroid") as RectTransform : null })
                    if (part) { part.localScale *= fit; part.anchoredPosition *= fit; }
            brt.localRotation = Quaternion.Euler(0, 0, 1.2f);
            var clip = Ui.Panel(brt, "Clip", Palette.Hex("B9C0C7"), 10);
            Ui.Place(clip.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 26), new Vector2(260, 70), new Vector2(0.5f, 1));
            var paper = Ui.Panel(brt, "Paper", Ui.Paper, 6);
            Ui.Place(paper.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(780, 840));
            var p = paper.rectTransform;

            var title = Ui.Label(p, "SHIFT REPORT", UiFont.Type, 46, Ui.Ink, TextAlignmentOptions.TopLeft);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(50, -40), new Vector2(680, 60), new Vector2(0, 1));
            var sub = Ui.Label(p, $"Night {def.Number} · {def.Day} · {def.Title} · {Mathf.FloorToInt(r.Seconds / 60)}m {Mathf.FloorToInt(r.Seconds % 60):00}s on the clock", UiFont.Sans, 22, new Color(0.3f, 0.32f, 0.38f), TextAlignmentOptions.TopLeft);
            Ui.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(50, -100), new Vector2(700, 30), new Vector2(0, 1));

            float y = -160;
            int i = 0;
            foreach (var t in def.Tasks)
            {
                bool ok = dir.IsDone(t);
                var (d, n) = dir.Progress(t);
                string mark = ok ? "<color=#2E8B57>✔</color>" : "<color=#B03A2E>✗</color>";
                string extra = n > 1 && !ok ? $"  <size=80%><color=#888>{d}/{n}</color></size>" : "";
                var line = Ui.Label(p, $"{mark}  {t.Label}{(t.Optional ? "  <size=75%><color=#8A7A5A>bonus</color></size>" : "")}{extra}", UiFont.Hand, 34, Ui.Ink, TextAlignmentOptions.TopLeft);
                Ui.Place(line.rectTransform, new Vector2(0, 1), new Vector2(60, y), new Vector2(660, 44), new Vector2(0, 1));
                line.alpha = 0;
                int idx = i++;
                Tween.Run(0.3f, k => { if (line) line.alpha = k; }, Ease.OutCubic, null, 0.4f + idx * 0.12f);
                Tween.Delay(0.4f + idx * 0.12f, () => Sfx.Play("pen_scratch", null, 0.25f, 1.2f, 0.15f, AudioBus.Ui));
                y -= 46;
            }
            y -= 20;
            var sec = Ui.Label(p, $"Secrets found   <b>{r.Secrets} / {r.SecretsTotal}</b>", UiFont.SansMedium, 28, Palette.Hex("6A3FA0"), TextAlignmentOptions.TopLeft);
            Ui.Place(sec.rectTransform, new Vector2(0, 1), new Vector2(60, y), new Vector2(660, 40), new Vector2(0, 1));
            y -= 50;
            if (Story.State.Inventory.Count > 0)
            {
                var inv = Ui.Label(p, "In your locker: " + string.Join(", ", Story.State.Inventory.Select(id => Docs.Get(id)?.Title)), UiFont.Sans, 22, new Color(0.3f, 0.32f, 0.38f), TextAlignmentOptions.TopLeft);
                Ui.Place(inv.rectTransform, new Vector2(0, 1), new Vector2(60, y), new Vector2(660, 60), new Vector2(0, 1));
            }

            // Grade stamp
            var stamp = Ui.Label(p, r.Grade, UiFont.Marker, 220, new Color(0.72f, 0.12f, 0.1f, 0.85f), TextAlignmentOptions.Center);
            Ui.Place(stamp.rectTransform, new Vector2(1, 1), new Vector2(-150, -150), new Vector2(240, 240), new Vector2(0.5f, 0.5f));
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, -14);
            stamp.alpha = 0;
            var ring = Ui.Image(stamp.rectTransform, "Ring", new Color(0.72f, 0.12f, 0.1f, 0.7f), Ui.Ring(256, 0.04f));
            Ui.Stretch(ring.rectTransform, -10);
            float stampAt = 0.6f + def.Tasks.Count * 0.12f;
            Tween.Delay(stampAt, () =>
            {
                if (!stamp) return;
                stamp.alpha = 1;
                Sfx.Play("punch_clock", null, 0.7f, 1.3f, 0f, AudioBus.Ui);
                Tween.Run(0.22f, k => { if (stamp) stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k); }, Ease.InCubic);
                float bx = brt.anchoredPosition.x;
                Tween.Run(0.3f, k => { if (brt) brt.anchoredPosition = new Vector2(bx + Mathf.Sin(k * 50) * 8 * (1 - k), -10); }, Ease.Linear, null, 0.2f);
            });

            var hint = Ui.Label(root, $"Press {GameInput.MenuKeyTag("E")} to see what happened in the morning", UiFont.SansMedium, 22, Ui.TextDim, TextAlignmentOptions.Center);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1000, 40), new Vector2(0.5f, 0));
            AudioDirector.Instance?.PlayMusic("music_daylight", 0.4f);
        }

        void Update()
        {
            if (done != null && age > 1.2f && Advance()) Finish();
        }
    }

    /// <summary>Before/after polaroid: the after photo wipes across the before, like a squeegee.</summary>
    public class PolaroidWipe : MonoBehaviour
    {
        RectTransform frame, afterMask, line;
        RawImage before, after;
        TextMeshProUGUI caption;
        readonly List<string> rooms = new();
        int index = -1;
        float t;

        public static void Create(RectTransform parent, NightDef def)
        {
            var go = new GameObject("Polaroid", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PolaroidWipe>();
            p.Build(def);
        }

        void Build(NightDef def)
        {
            var photos = RoomPhotos.Instance;
            foreach (var r in def.Rooms) if (photos.Before.ContainsKey(r) && photos.After.ContainsKey(r)) rooms.Add(r);
            var rt = (RectTransform)transform;
            Ui.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(520, 30), new Vector2(700, 560));
            rt.localRotation = Quaternion.Euler(0, 0, 2.5f);
            var shadow = Ui.Panel(rt, "Shadow", new Color(0, 0, 0, 0.5f), 10);
            Ui.Stretch(shadow.rectTransform, -4);
            shadow.rectTransform.anchoredPosition = new Vector2(10, -12);
            var paper = Ui.Panel(rt, "Paper", Palette.Hex("F7F5EE"), 4);
            Ui.Stretch(paper.rectTransform);
            frame = Ui.Rect(rt, "Photo");
            Ui.Place(frame, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(640, 400), new Vector2(0.5f, 1));
            before = new GameObject("Before", typeof(RectTransform)).AddComponent<RawImage>();
            before.transform.SetParent(frame, false);
            Ui.Stretch(before.rectTransform);
            afterMask = Ui.Rect(frame, "AfterMask");
            afterMask.anchorMin = new Vector2(0, 0); afterMask.anchorMax = new Vector2(0, 1);
            afterMask.pivot = new Vector2(0, 0.5f);
            afterMask.sizeDelta = new Vector2(0, 0);
            afterMask.gameObject.AddComponent<RectMask2D>();
            after = new GameObject("After", typeof(RectTransform)).AddComponent<RawImage>();
            after.transform.SetParent(afterMask, false);
            after.rectTransform.anchorMin = after.rectTransform.anchorMax = new Vector2(0, 0.5f);
            after.rectTransform.pivot = new Vector2(0, 0.5f);
            after.rectTransform.sizeDelta = new Vector2(640, 400);
            var l = Ui.Image(frame, "Line", new Color(1f, 1f, 1f, 0.9f));
            line = l.rectTransform;
            line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(0, 1);
            line.sizeDelta = new Vector2(4, 0);
            caption = Ui.Label(rt, "", UiFont.Hand, 40, Ui.Ink, TextAlignmentOptions.Center);
            Ui.Place(caption.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(640, 90), new Vector2(0.5f, 0));
            Next();
        }

        void Next()
        {
            if (rooms.Count == 0) return;
            index = (index + 1) % rooms.Count;
            var id = rooms[index];
            before.texture = RoomPhotos.Instance.Before[id];
            after.texture = RoomPhotos.Instance.After[id];
            string name = OfficeBuilder.Instance.Rooms.TryGetValue(id, out var r) ? r.DisplayName : id;
            caption.text = $"{name}  <size=70%><color=#888>before → after</color></size>";
            t = 0f;
        }

        void Update()
        {
            if (rooms.Count == 0) return;
            t += GameTime.UnscaledDelta;
            float k = Mathf.Clamp01((t - 0.8f) / 1.1f);
            k = Ease.InOutCubic(k);
            afterMask.sizeDelta = new Vector2(640 * k, 0);
            line.anchoredPosition = new Vector2(640 * k, 0);
            line.gameObject.SetActive(k > 0.001f && k < 0.999f);
            if (t > 0.8f && t - GameTime.UnscaledDelta <= 0.8f) Sfx.Play("toss", null, 0.35f, 1.2f, 0.05f, AudioBus.Ui);
            if (t > 4.2f && rooms.Count > 1) Next();
        }
    }

    // =============================================================================================
    // Morning chat
    // =============================================================================================

    public class ChatInterlude : Interstitial
    {
        static readonly Dictionary<string, Color> Colors = new()
        {
            { "dana", Palette.Hex("E8A0BF") }, { "theo", Palette.Hex("7FB38A") }, { "priya", Palette.Hex("7FA8E8") },
            { "russ", Palette.Hex("F0A04B") }, { "marian", Palette.Hex("B04040") }, { "auditor", Palette.Hex("9AA4B0") },
            { "system", Palette.Hex("666C78") }, { "walt", Palette.Hex("C9A66B") },
        };

        RectTransform content;
        readonly List<ChatLine> lines = new();
        int shown;
        float nextAt;
        float y;
        TextMeshProUGUI typing, hint;
        bool finished;

        string doneHint = $"Press {GameInput.MenuKeyTag("E")} to clock in for the next night";

        public static void Show(string dayLabel, List<ChatLine> chat, Action onDone, string doneHint = null)
        {
            var go = new GameObject("ChatInterlude");
            var c = go.AddComponent<ChatInterlude>();
            c.done = onDone;
            if (doneHint != null) c.doneHint = doneHint;
            c.lines.AddRange(chat);
            c.Build(dayLabel);
        }

        void Build(string dayLabel)
        {
            Setup("Chat", 70, new Color(0.86f, 0.88f, 0.91f, 1f));
            var win = Ui.Panel(root, "Window", Color.white, 18);
            var w = win.rectTransform;
            Ui.Place(w, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 900));
            var side = Ui.Panel(w, "Side", Palette.Hex("2B2F3A"), 18);
            Ui.Place(side.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(270, 900), new Vector2(0, 0.5f));
            var org = Ui.Label(side.rectTransform, "Halvorsen Freight", UiFont.SansBold, 26, Color.white, TextAlignmentOptions.TopLeft);
            Ui.Place(org.rectTransform, new Vector2(0, 1), new Vector2(26, -30), new Vector2(230, 36), new Vector2(0, 1));
            string[] chans = { "# general", "# sales", "# finance", "# random" };
            for (int i = 0; i < chans.Length; i++)
            {
                var c = Ui.Label(side.rectTransform, chans[i], UiFont.SansMedium, 22, i == 0 ? Color.white : new Color(1, 1, 1, 0.5f), TextAlignmentOptions.TopLeft);
                Ui.Place(c.rectTransform, new Vector2(0, 1), new Vector2(26, -100 - i * 40), new Vector2(230, 30), new Vector2(0, 1));
            }
            var header = Ui.Label(w, $"# general  <size=70%><color=#8A90A0>{dayLabel}</color></size>", UiFont.SansBold, 30, Palette.Hex("1E2430"), TextAlignmentOptions.TopLeft);
            Ui.Place(header.rectTransform, new Vector2(0, 1), new Vector2(300, -26), new Vector2(840, 40), new Vector2(0, 1));
            var line = Ui.Image(w, "Rule", new Color(0, 0, 0, 0.08f));
            Ui.Place(line.rectTransform, new Vector2(0, 1), new Vector2(270, -80), new Vector2(910, 2), new Vector2(0, 1));
            var viewport = Ui.Rect(w, "Viewport");
            Ui.Place(viewport, new Vector2(0, 1), new Vector2(290, -90), new Vector2(870, 740), new Vector2(0, 1));
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Ui.Rect(viewport, "Content");
            Ui.Place(content, new Vector2(0, 1), Vector2.zero, new Vector2(870, 10), new Vector2(0, 1));
            typing = Ui.Label(w, "", UiFont.Sans, 20, new Color(0.4f, 0.42f, 0.48f), TextAlignmentOptions.TopLeft);
            Ui.Place(typing.rectTransform, new Vector2(0, 0), new Vector2(300, 30), new Vector2(800, 30), new Vector2(0, 0));
            hint = Ui.Label(root, "", UiFont.SansMedium, 22, new Color(0.25f, 0.28f, 0.35f), TextAlignmentOptions.Center);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1000, 40), new Vector2(0.5f, 0));
            nextAt = 1.0f;
        }

        void AddLine(ChatLine l)
        {
            var who = l.Who;
            var col = Colors.TryGetValue(who, out var c) ? c : Color.gray;
            var row = Ui.Rect(content, "Msg");
            var avatar = Ui.Image(row, "Avatar", col, Ui.Rounded(12));
            Ui.Place(avatar.rectTransform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(54, 54), new Vector2(0, 1));
            var init = Ui.Label(avatar.rectTransform, People.Short.TryGetValue(who, out var nm) ? nm.Substring(0, 1).ToUpperInvariant() : "?", UiFont.SansBold, 28, Color.white, TextAlignmentOptions.Center);
            Ui.Stretch(init.rectTransform);
            string name = People.Name.TryGetValue(who, out var full) ? full : who;
            var head = Ui.Label(row, $"<b>{name}</b>  <size=75%><color=#9AA0AE>{l.Time}</color></size>", UiFont.Sans, 22, Palette.Hex("1E2430"), TextAlignmentOptions.TopLeft);
            Ui.Place(head.rectTransform, new Vector2(0, 1), new Vector2(70, 0), new Vector2(780, 28), new Vector2(0, 1));
            var body = Ui.Label(row, l.Text, UiFont.Sans, 24, Palette.Hex("2A2F3A"), TextAlignmentOptions.TopLeft);
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0, 1);
            body.rectTransform.pivot = new Vector2(0, 1);
            body.rectTransform.anchoredPosition = new Vector2(70, -30);
            body.rectTransform.sizeDelta = new Vector2(780, 30);
            float h = Mathf.Max(30, body.GetPreferredValues(l.Text, 780, 0).y);
            body.rectTransform.sizeDelta = new Vector2(780, h);
            float rowH = 30 + h + 14;
            if (!string.IsNullOrEmpty(l.React))
            {
                var pill = Ui.Panel(row, "React", new Color(0.92f, 0.94f, 0.98f), 12);
                Ui.Place(pill.rectTransform, new Vector2(0, 1), new Vector2(70, -30 - h - 6), new Vector2(64, 30), new Vector2(0, 1));
                var e = Ui.Label(pill.rectTransform, l.React + " 2", UiFont.Sans, 18, Palette.Hex("44506A"), TextAlignmentOptions.Center);
                Ui.Stretch(e.rectTransform);
                rowH += 36;
                pill.rectTransform.localScale = Vector3.zero;
                Tween.Run(0.3f, k => { if (pill) pill.rectTransform.localScale = Vector3.one * k; }, Ease.OutBack, null, 0.5f);
            }
            row.anchorMin = row.anchorMax = new Vector2(0, 1);
            row.pivot = new Vector2(0, 1);
            row.sizeDelta = new Vector2(860, rowH);
            row.anchoredPosition = new Vector2(0, -y);
            y += rowH + 6;
            var cg = row.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            Tween.Run(0.3f, k => { if (cg) { cg.alpha = k; row.anchoredPosition = new Vector2(Mathf.Lerp(20, 0, k), row.anchoredPosition.y); } }, Ease.OutCubic);
            // Scroll so the newest message is visible.
            float overflow = Mathf.Max(0, y - 740);
            var from = content.anchoredPosition.y;
            Tween.Run(0.3f, k => { if (content) content.anchoredPosition = new Vector2(0, Mathf.Lerp(from, overflow, k)); }, Ease.OutCubic, owner: content);
            Sfx.Play("notify", null, 0.25f, 1f + UnityEngine.Random.Range(-0.05f, 0.05f), 0f, AudioBus.Ui);
        }

        void Update()
        {
            if (done == null) return;
            bool adv = age > 0.8f && Advance();
            if (finished)
            {
                if (adv) Finish();
                return;
            }
            if (shown < lines.Count)
            {
                var next = lines[shown];
                string who = People.Short.TryGetValue(next.Who, out var nm) ? nm : next.Who;
                typing.text = $"<i>{who} is typing…</i>";
                if (age >= nextAt || adv)
                {
                    AddLine(next);
                    shown++;
                    nextAt = age + Mathf.Clamp(0.5f + next.Text.Length * 0.018f, 0.7f, 2.2f);
                }
            }
            else
            {
                typing.text = "";
                finished = true;
                hint.text = doneHint;
            }
        }
    }
}
