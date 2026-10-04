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
                nav.selectOnUp = Nearest(a, live, Vector2.up);
                nav.selectOnDown = Nearest(a, live, Vector2.down);
                nav.selectOnLeft = Nearest(a, live, Vector2.left);
                nav.selectOnRight = Nearest(a, live, Vector2.right);
                a.navigation = nav;
            }
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
