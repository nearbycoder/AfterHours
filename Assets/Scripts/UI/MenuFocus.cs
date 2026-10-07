using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>
    /// Gamepad (and arrow-key) navigation for a uGUI menu: wires explicit up/down/left/right links
    /// between its items from their on-screen positions, so focus never wanders into a menu
    /// underneath, and selects the first item whenever the pad is in use and nothing in this menu
    /// is selected. The most recently opened menu owns focus.
    /// </summary>
    public class MenuFocus : MonoBehaviour
    {
        static readonly List<MenuFocus> open = new();
        readonly List<Selectable> items = new();
        bool wired;

        public static MenuFocus Attach(GameObject host, IEnumerable<Selectable> selectables)
        {
            var f = host.AddComponent<MenuFocus>();
            f.items.AddRange(selectables.Where(s => s != null));
            return f;
        }

        /// <summary>Every selectable under <paramref name="host"/>, in hierarchy order.</summary>
        public static MenuFocus AttachAll(GameObject host) => Attach(host, host.GetComponentsInChildren<Selectable>(true));

        /// <summary>The menu in front (the most recently opened), if any.</summary>
        public static MenuFocus Top => open.Count > 0 ? open[^1] : null;

        /// <summary>
        /// Rows of a scrolling list, walked in order by up and down (their on-screen positions
        /// say little while most of them are scrolled out of view). The first row goes up to
        /// <see cref="Before"/> (or wraps to <see cref="After"/>), the last goes down to
        /// <see cref="After"/>, and those come back to the list.
        /// </summary>
        public List<Selectable> Chain;
        public List<Selectable> Before = new(), After = new();

        void OnEnable() => open.Add(this);
        void OnDisable() => open.Remove(this);

        void Update()
        {
            if (!wired)
            {
                Canvas.ForceUpdateCanvases();
                Wire();
                wired = true;
            }
            if (open.Count == 0 || open[^1] != this || !GameInput.UsingPad) return;
            var es = EventSystem.current;
            if (es == null) return;
            var cur = es.currentSelectedGameObject;
            if (cur != null && cur.activeInHierarchy && items.Any(i => i && i.gameObject == cur)) return;
            var first = items.FirstOrDefault(i => i && i.isActiveAndEnabled && i.interactable);
            if (first) es.SetSelectedGameObject(first.gameObject);
        }

        void Wire()
        {
            var live = items.Where(i => i).ToList();
            foreach (var a in live)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = Nearest(a, live, Vector2.up) ?? Wrap(a, live, -1);
                nav.selectOnDown = Nearest(a, live, Vector2.down) ?? Wrap(a, live, +1);
                // Left and right adjust a slider or choice row, so they don't also move focus.
                bool adjusts = a.GetComponent<SliderNav>() || a.GetComponent<ChoiceNav>();
                nav.selectOnLeft = adjusts ? null : Nearest(a, live, Vector2.left);
                nav.selectOnRight = adjusts ? null : Nearest(a, live, Vector2.right);
                a.navigation = nav;
            }
            if (Chain == null || Chain.Count == 0) return;
            var chain = Chain.Where(c => c).ToList();
            Selectable up0 = Before.FirstOrDefault(b => b) ?? After.FirstOrDefault(b => b), down1 = After.FirstOrDefault(b => b);
            for (int i = 0; i < chain.Count; i++)
            {
                var nav = chain[i].navigation;
                nav.selectOnUp = i > 0 ? chain[i - 1] : up0;
                nav.selectOnDown = i < chain.Count - 1 ? chain[i + 1] : down1;
                chain[i].navigation = nav;
            }
            foreach (var b in Before.Where(b => b)) { var nav = b.navigation; nav.selectOnDown = chain[0]; b.navigation = nav; }
            foreach (var a in After.Where(a => a))
            {
                var nav = a.navigation;
                nav.selectOnUp = chain[^1];
                if (Before.Count == 0) nav.selectOnDown = chain[0];
                a.navigation = nav;
            }
        }

        /// <summary>
        /// In a menu laid out in columns: down off the bottom of one column goes to the top of the
        /// next one to the right, and up off the top goes to the bottom of the one to the left.
        /// </summary>
        static Selectable Wrap(Selectable from, List<Selectable> all, int dir)
        {
            Vector2 p = ((RectTransform)from.transform).position;
            float w = ((RectTransform)from.transform).rect.width * from.transform.lossyScale.x;
            var col = all.Where(s => s != from)
                .Select(s => (s, pos: (Vector2)((RectTransform)s.transform).position))
                .Where(x => dir > 0 ? x.pos.x > p.x + w * 0.5f : x.pos.x < p.x - w * 0.5f)
                .ToList();
            if (col.Count == 0) return null;
            // The nearest column in that direction, then its top (or bottom) item.
            float colX = dir > 0 ? col.Min(x => x.pos.x) : col.Max(x => x.pos.x);
            var inCol = col.Where(x => Mathf.Abs(x.pos.x - colX) < w * 0.5f).ToList();
            return (dir > 0 ? inCol.OrderByDescending(x => x.pos.y) : inCol.OrderBy(x => x.pos.y)).First().s;
        }

        static Selectable Nearest(Selectable from, List<Selectable> all, Vector2 dir)
        {
            Vector2 p = ((RectTransform)from.transform).position;
            Selectable best = null;
            float bestScore = float.MaxValue;
            foreach (var s in all)
            {
                if (s == from) continue;
                Vector2 d = (Vector2)((RectTransform)s.transform).position - p;
                float along = Vector2.Dot(d, dir);
                if (along <= 1f) continue;
                float across = Mathf.Abs(Vector2.Dot(d, new Vector2(-dir.y, dir.x)));
                if (across > along * 1.5f) continue; // roughly in that direction
                float score = along + across * 2f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }
    }

    /// <summary>Lets d-pad / arrow left and right nudge a slider row while it is selected.</summary>
    public class SliderNav : MonoBehaviour, IMoveHandler
    {
        public System.Func<float> Get;
        public System.Action<float> Set;

        public void OnMove(AxisEventData e)
        {
            if (e.moveDir != MoveDirection.Left && e.moveDir != MoveDirection.Right) return;
            float v = Mathf.Clamp01(Get() + (e.moveDir == MoveDirection.Right ? 0.05f : -0.05f));
            Set(v);
            Sfx.Play("ui_hover", null, 0.18f, 0.8f + v * 0.6f, 0f, AudioBus.Ui);
        }
    }

    /// <summary>Lets d-pad / arrow left and right step a choice row while it is selected.</summary>
    public class ChoiceNav : MonoBehaviour, IMoveHandler
    {
        public System.Action<int> Step;

        public void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) Step(-1);
            else if (e.moveDir == MoveDirection.Right) Step(1);
        }
    }

    /// <summary>Soft highlight behind a row while it is selected with the pad or keys.</summary>
    public class SelectGlow : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public Graphic Target;
        public Color On = new(1f, 0.78f, 0.34f, 0.16f);
        public Color Off = new(1, 1, 1, 0f);
        bool selected;

        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;

        void Update()
        {
            if (!Target) return;
            Target.color = Color.Lerp(Target.color, selected && GameInput.UsingPad ? On : Off, 1f - Mathf.Exp(-GameTime.UnscaledDelta * 14f));
        }
    }
}
