using UnityEngine;

namespace AfterHours
{
    public enum TrashKind { General, Recyclable, Paper }

    /// <summary>Loose rubbish. Goes in the right bin for credit.</summary>
    public class TrashItem : Holdable
    {
        public TrashKind Kind = TrashKind.General;
        public bool Binned { get; private set; }

        public override string Prompt(Interactor who)
        {
            var p = base.Prompt(who);
            return p == null ? null : $"Pick up · {DisplayName}";
        }

        public void MarkBinned() => Binned = true;
    }

    /// <summary>
    /// A bin with a trigger at its mouth. Accepts matching rubbish (with a satisfying drop), bounces
    /// wrong items back out with a hint.
    /// </summary>
    public class Bin : MonoBehaviour
    {
        public enum BinKind { Trash, Recycle }
        public BinKind Kind = BinKind.Trash;
        public string RoomId;
        public float Radius = 0.15f, Height = 0.36f;
        Transform model;
        float wobble;

        public static Bin Create(Transform anchor, BinKind kind, string roomId)
        {
            string prop = kind == BinKind.Recycle ? "bin_recycle" : roomId == "breakroom" ? "bin_break" : "bin_trash";
            var go = PropLibrary.Spawn(prop, anchor.position, anchor.rotation);
            go.name = "Bin_" + kind + "_" + roomId;
            var b = go.AddComponent<Bin>();
            b.Kind = kind;
            b.RoomId = roomId;
            b.model = go.transform;
            if (prop == "bin_break") { b.Radius = 0.17f; b.Height = 0.7f; }
            if (prop == "bin_recycle") { b.Radius = 0.17f; b.Height = 0.42f; }
            // Replace the solid box collider with walls + an open top so items fall in.
            Destroy(go.GetComponent<BoxCollider>());
            var floor = go.AddComponent<BoxCollider>();
            floor.center = new Vector3(0, 0.02f, 0);
            floor.size = new Vector3(b.Radius * 1.6f, 0.04f, b.Radius * 1.6f);
            for (int i = 0; i < 4; i++)
            {
                var w = new GameObject("wall" + i);
                w.transform.SetParent(go.transform, false);
                w.transform.localRotation = Quaternion.Euler(0, i * 90, 0);
                var wc = w.AddComponent<BoxCollider>();
                wc.center = new Vector3(0, b.Height / 2, b.Radius * 0.9f);
                wc.size = new Vector3(b.Radius * 2f, b.Height, 0.02f);
            }
            var trig = new GameObject("mouth");
            trig.layer = Layers.Trigger;
            trig.transform.SetParent(go.transform, false);
            var tc = trig.AddComponent<BoxCollider>();
            tc.isTrigger = true;
            tc.center = new Vector3(0, b.Height * 0.55f, 0);
            tc.size = new Vector3(b.Radius * 1.7f, b.Height * 0.9f, b.Radius * 1.7f);
            var rb = trig.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            trig.AddComponent<BinMouth>().Bin = b;
            return b;
        }

        public bool Accepts(TrashKind k) => Kind == BinKind.Trash ? k != TrashKind.Recyclable : k != TrashKind.General;

        public void Receive(TrashItem t)
        {
            if (t.Binned || t.Held || t.Locked) return;
            if (!Accepts(t.Kind))
            {
                Reject(t);
                return;
            }
            t.Locked = true;
            t.MarkBinned();
            bool longShot = t.InFlight && (t.ThrownFrom - transform.position).magnitude > 3.2f;
            t.Body.isKinematic = true;
            foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = false;
            var start = t.transform.position;
            var end = transform.position + Vector3.up * 0.08f;
            var s0 = t.transform.localScale;
            Tween.Run(0.28f, k =>
            {
                if (!t) return;
                t.transform.position = Vector3.Lerp(start, end, k);
                t.transform.localScale = s0 * Mathf.Lerp(1f, 0.6f, k);
            }, Ease.InCubic, () => { if (t) t.gameObject.SetActive(false); }, 0f, null, true);
            wobble = 1f;
            Sfx.Play(Kind == BinKind.Recycle && t.Kind == TrashKind.Recyclable ? "can_clank" : "bin_thunk", transform.position, 0.7f);
            if (t.Kind == TrashKind.Paper) Sfx.Play("paper_crumple", transform.position, 0.35f);
            Fx.Burst(FxKind.Puff, transform.position + Vector3.up * Height, Vector3.up, 6, new Color(0.8f, 0.8f, 0.8f, 0.25f), 0.4f, 0.8f, 0.05f);
            if (longShot)
            {
                Sfx.Play("swish", null, 0.6f);
                Hud.Instance?.Toast("Nice shot!", $"{(t.ThrownFrom - transform.position).magnitude:0.0} m", Ui.Accent, 1.6f);
                Fx.Burst(FxKind.Confetti, transform.position + Vector3.up * Height, Vector3.up, 24, Color.white, 2.2f, 0.7f);
            }
            Events.Raise(GameEvent.TrashBinned, t.Id);
        }

        void Reject(TrashItem t)
        {
            if (Time.time - lastReject < 0.5f) return;
            lastReject = Time.time;
            t.Body.isKinematic = false;
            var outward = (t.transform.position - transform.position);
            outward.y = 0;
            t.Body.linearVelocity = (outward.normalized * 1.4f + Vector3.up * 3.2f);
            Sfx.Play("wrong_bin", null, 0.5f, 1f, 0f);
            wobble = 0.6f;
            string hint = t.Kind == TrashKind.Recyclable ? "Cans and bottles go in the blue recycling bin" : "Food and wrappers go in the black bin";
            Hud.Instance?.Toast("Wrong bin", hint, Palette.EvidenceRed, 2f);
        }

        float lastReject;

        void Update()
        {
            if (wobble <= 0f) return;
            wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 2.5f);
            float a = Mathf.Sin(Time.time * 40f) * wobble * 4f;
            model.localRotation = Quaternion.Euler(a, model.localEulerAngles.y, a * 0.5f);
            if (wobble <= 0f) model.localRotation = Quaternion.Euler(0, model.localEulerAngles.y, 0);
        }
    }

    public class BinMouth : MonoBehaviour
    {
        public Bin Bin;

        void OnTriggerEnter(Collider other) => Check(other);
        void OnTriggerStay(Collider other) => Check(other);

        void Check(Collider other)
        {
            var t = other.GetComponentInParent<TrashItem>();
            if (t == null || t.Held || t.Binned) return;
            if (t.Body.linearVelocity.y > 0.5f) return; // still rising
            Bin.Receive(t);
        }
    }
}
