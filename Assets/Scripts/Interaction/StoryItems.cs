using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    /// <summary>A document you can take. Picking it up opens the inspect view instead of carrying it.</summary>
    public class EvidenceItem : Holdable
    {
        public string Doc;
        Vector3 homePos;
        Quaternion homeRot;

        void Start()
        {
            homePos = transform.position;
            homeRot = transform.rotation;
        }

        public override string Prompt(Interactor who)
        {
            if (Locked || (Hands.Instance != null && Hands.Instance.Holding != null)) return null;
            var d = Docs.Get(Doc);
            return d == null ? null : (d.Prop == "paper_ball" ? "Unfold · " : "Read · ") + (d.Prop == "paper_ball" ? "Crumpled paper" : d.Title);
        }

        public override void Interact(Interactor who)
        {
            var d = Docs.Get(Doc);
            if (d == null) return;
            Sfx.Play(d.Prop == "paper_ball" ? "paper_unfold" : "ui_page", null, 0.6f);
            Story.OnDocRead(d);
            InspectView.Show(d, InspectMode.Evidence, choice =>
            {
                switch (choice)
                {
                    case InspectChoice.Keep:
                        Story.State.SetFate(d.Id, Fate.Kept);
                        Events.Raise(GameEvent.EvidenceFate, d.Id);
                        Hud.Instance?.Toast("Kept · " + d.Title, "Deliver it to a tray, or shred it", Ui.Accent);
                        Sfx.Play("pickup", null, 0.6f);
                        gameObject.SetActive(false);
                        break;
                    case InspectChoice.Toss:
                        Story.State.SetFate(d.Id, Fate.Trashed);
                        Events.Raise(GameEvent.EvidenceFate, d.Id);
                        Sfx.Play("paper_crumple", null, 0.6f);
                        Sfx.Play("bin_thunk", null, 0.35f);
                        Hud.Instance?.Toast("Thrown away", d.Title, new Color(0.6f, 0.6f, 0.65f));
                        gameObject.SetActive(false);
                        break;
                    default:
                        if (Story.State.FateOf(d.Id) == Fate.Untouched) Story.State.SetFate(d.Id, Fate.Seen);
                        transform.SetPositionAndRotation(homePos, homeRot);
                        break;
                }
            });
        }
    }

    /// <summary>A note, card or screen you can read in place.</summary>
    public class Readable : MonoBehaviour, IInteractable
    {
        public string Doc;
        public string Verb = "Read";
        public string Label;
        public System.Action OnRead;

        public string Prompt(Interactor who)
        {
            var d = Docs.Get(Doc);
            if (d == null) return null;
            return !string.IsNullOrEmpty(Label) ? Label : $"{Verb} · {d.Title}";
        }

        public void Interact(Interactor who)
        {
            var d = Docs.Get(Doc);
            if (d == null) return;
            Sfx.Play(d.Style == DocStyle.Screen ? "ui_click" : "ui_page", null, 0.5f);
            Story.OnDocRead(d);
            OnRead?.Invoke();
            InspectView.Show(d, InspectMode.Read, null);
        }
    }

    /// <summary>
    /// A monitor that can show a screen (texture) and be switched off. Some screens are readable.
    /// </summary>
    public class MonitorScreen : MonoBehaviour, IInteractable
    {
        public string Id;
        public string ScreenDoc;
        public bool On;
        public bool StoryOwned;
        public bool CountsForTask;
        public Light Glow;
        Material screenMat;
        float flicker;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        public static readonly List<MonitorScreen> All = new();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init(string id, string screenTexture, bool on)
        {
            Id = id;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i].IsKeywordEnabled("_EMISSION")) continue;
                    if (mats[i].GetColor(EmissionColor).maxColorComponent > 0.5f) continue; // the little LED
                    screenMat = mats[i];
                }
            }
            var lg = new GameObject("ScreenGlow");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0, 0.38f, 0.25f);
            Glow = lg.AddComponent<Light>();
            Glow.type = LightType.Point;
            Glow.color = new Color(0.45f, 0.75f, 1f);
            Glow.range = 2.2f;
            Glow.intensity = 0.9f;
            Glow.shadows = LightShadows.None;
            SetScreen(screenTexture);
            SetOn(on, true);
        }

        public void SetScreen(string texturePath)
        {
            if (screenMat == null) return;
            var tex = string.IsNullOrEmpty(texturePath) ? null : Res.Texture(texturePath);
            screenMat.SetTexture(EmissionMap, tex);
            screenMat.SetTexture(BaseMap, tex);
            screenMat.SetColor("_BaseColor", tex ? new Color(0.05f, 0.05f, 0.05f) : Color.black);
        }

        public void SetOn(bool on, bool instant = false)
        {
            On = on;
            if (!instant)
            {
                Sfx.Play(on ? "ui_click" : "monitor_off", transform.position, 0.5f);
                flicker = on ? 0.35f : 0f;
            }
            Apply();
            if (!on && !instant) Events.Raise(GameEvent.MonitorOff, Id);
        }

        void Apply()
        {
            if (screenMat) screenMat.SetColor(EmissionColor, On ? Color.white * (flicker > 0 ? Random.Range(0.3f, 1.2f) : 1.15f) : Color.black);
            if (Glow) Glow.enabled = On;
        }

        void Update()
        {
            if (flicker <= 0f) return;
            flicker -= Time.deltaTime;
            Apply();
        }

        public string Prompt(Interactor who)
        {
            if (!On) return null;
            return string.IsNullOrEmpty(ScreenDoc) ? "Switch off monitor" : "Read screen";
        }

        public void Interact(Interactor who)
        {
            if (!string.IsNullOrEmpty(ScreenDoc))
            {
                var d = Docs.Get(ScreenDoc);
                Story.OnDocRead(d);
                InspectView.Show(d, InspectMode.Screen, c => { if (c == InspectChoice.SwitchOff) SetOn(false); });
                return;
            }
            SetOn(false);
        }
    }

    /// <summary>A person's inbox tray: leave kept evidence or a note for them.</summary>
    public class InboxTray : MonoBehaviour, IInteractable
    {
        public string Person;
        readonly List<GameObject> contents = new();
        public static readonly List<InboxTray> All = new();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Prompt(Interactor who)
        {
            if (Story.State.Inventory.Count == 0 && Story.State.Phrases.Count == 0) return null;
            return $"Leave something for {People.Short[Person]}";
        }

        public void Interact(Interactor who)
        {
            var options = new List<ChoiceMenu.Option>();
            foreach (var id in Story.State.Inventory.ToList())
            {
                var d = Docs.Get(id);
                if (d == null) continue;
                options.Add(new ChoiceMenu.Option(d.Title, d.Header, () => Deliver(d)));
            }
            if (Story.State.Phrases.Count > 0)
                options.Add(new ChoiceMenu.Option("Write a sticky note…", "Anonymous. In your handwriting.", ComposeNote));
            ChoiceMenu.Show($"{People.Name[Person]}'s tray", "Whatever you leave here, they find in the morning.", options);
        }

        void Deliver(DocDef d)
        {
            Story.State.SetFate(d.Id, Fate.Delivered, Person);
            Events.Raise(GameEvent.EvidenceFate, d.Id);
            AddPaper(d.Prop == "folder_red" ? "folder_red" : d.Prop == "binder" ? "binder" : d.Prop == "envelope" ? "envelope" : "folder_manila");
            Sfx.Play("ui_page", null, 0.6f);
            Hud.Instance?.Toast($"Left for {People.Short[Person]}", d.Title, Ui.Accent);
        }

        void ComposeNote()
        {
            var opts = Story.State.Phrases.Select(p => new ChoiceMenu.Option("\"" + Phrases.Text[p] + "\"", null, () =>
            {
                Story.State.Notes.Add(new NoteRecord { To = Person, Phrase = p, Night = Story.State.Night });
                AddPaper("sticky_note");
                Sfx.Play("pen_scratch", null, 0.6f);
                Hud.Instance?.Toast($"Note left for {People.Short[Person]}", Phrases.Text[p], Palette.Sticky);
                Events.Raise(GameEvent.NoteLeft, Person + ":" + p);
            })).ToList();
            ChoiceMenu.Show("Sticky note", "Pick what to write.", opts);
        }

        void AddPaper(string prop)
        {
            var go = PropLibrary.Spawn(prop, transform.position + Vector3.up * (0.09f + contents.Count * 0.012f), transform.rotation * Quaternion.Euler(0, Random.Range(-8f, 8f), 0), transform);
            contents.Add(go);
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        }
    }

    /// <summary>Paper shredder: feed kept documents in. Gone for good.</summary>
    public class Shredder : MonoBehaviour, IInteractable
    {
        public string Id;
        public bool Jammed;
        public System.Action OnUnjam;

        public string Prompt(Interactor who)
        {
            if (Jammed) return "Clear the jam";
            return Story.State.Inventory.Count == 0 ? null : "Shred something…";
        }

        public void Interact(Interactor who)
        {
            if (Jammed)
            {
                Jammed = false;
                Sfx.Play("drawer_open", transform.position, 0.6f);
                Fx.Burst(FxKind.Paper, transform.position + Vector3.up * 0.62f, Vector3.up, 20, Color.white, 1.2f, 0.8f);
                OnUnjam?.Invoke();
                return;
            }
            var options = Story.State.Inventory.Select(id => Docs.Get(id)).Where(d => d != null)
                .Select(d => new ChoiceMenu.Option(d.Title, d.Header, () => Shred(d))).ToList();
            ChoiceMenu.Show("Shredder", "There's no undo.", options);
        }

        void Shred(DocDef d)
        {
            Story.State.SetFate(d.Id, Fate.Shredded);
            Events.Raise(GameEvent.EvidenceFate, d.Id);
            var paper = PropLibrary.Spawn("paper_sheet", transform.position + Vector3.up * 0.7f, transform.rotation * Quaternion.Euler(90, 0, 0));
            foreach (var c in paper.GetComponentsInChildren<Collider>()) c.enabled = false;
            var start = paper.transform.position;
            Sfx.Play("shredder", transform.position, 0.8f);
            Tween.Run(1.4f, k =>
            {
                if (!paper) return;
                paper.transform.position = start + Vector3.down * (0.3f * k);
                paper.transform.localScale = new Vector3(1, 1, 1f - k);
            }, Ease.Linear, () => { if (paper) Destroy(paper); });
            for (int i = 0; i < 6; i++)
                Tween.Delay(0.15f + i * 0.2f, () => Fx.Burst(FxKind.Paper, transform.position + Vector3.up * 0.5f, Vector3.down, 6, Color.white, 0.4f, 0.4f, 0.1f));
            Hud.Instance?.Toast("Shredded", d.Title, new Color(0.6f, 0.6f, 0.65f));
        }
    }

    /// <summary>The time clock in the closet. Clocking out ends the night.</summary>
    public class PunchClock : MonoBehaviour, IInteractable
    {
        public string Prompt(Interactor who) => NightDirector.Instance != null && NightDirector.Instance.Running ? "Clock out" : null;
        public void Interact(Interactor who) => NightDirector.Instance?.RequestClockOut();
    }
}
