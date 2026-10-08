using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>
    /// How menus open and close (round 12): a short fade with a slight rise in scale on the way in,
    /// a quicker fade on the way out, like the clipboard and the questions. Closing still takes
    /// effect at once: the layer stops taking input, leaves navigation and loses its logic in that
    /// frame, and only its picture fades before it's destroyed.
    /// </summary>
    public static class MenuFade
    {
        public const float InTime = 0.2f, OutTime = 0.15f, StartScale = 0.97f;
        /// <summary>The name a closing layer gets, so nothing looks it up as the open menu.</summary>
        public const string Closing = " (closing)";

        /// <summary>Fade a menu layer (from <see cref="Ui.Layer"/>) in.</summary>
        public static void In(RectTransform layer)
        {
            var g = layer.GetComponent<CanvasGroup>();
            if (g == null) return;
            g.alpha = 0f;
            layer.localScale = Vector3.one * StartScale;
            layer.gameObject.AddComponent<MenuFadeIn>().Group = g;
        }

        /// <summary>Close a menu layer now, and let its picture fade out.</summary>
        public static void Out(RectTransform layer)
        {
            if (!layer) return;
            var g = layer.GetComponent<CanvasGroup>();
            layer.name += Closing;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.transform.IsChildOf(layer))
                es.SetSelectedGameObject(null);
            // Everything but the picture stops: the menu's own logic, its buttons and rows, focus and scrolling.
            foreach (var b in layer.GetComponentsInChildren<MonoBehaviour>(true))
                if (b is not Graphic && b is not IClipper && b is not ILayoutController && b is not ILayoutElement) b.enabled = false;
            if (g == null) { Object.Destroy(layer.gameObject); return; }
            g.interactable = false;
            g.blocksRaycasts = false;
            float a0 = g.alpha, s0 = layer.localScale.x;
            Tween.Run(OutTime, k =>
            {
                if (!layer) return;
                g.alpha = a0 * (1f - k);
                layer.localScale = Vector3.one * Mathf.Lerp(s0, 0.985f, k);
            }, Ease.InCubic, () => { if (layer) Object.Destroy(layer.gameObject); });
        }
    }

    /// <summary>
    /// The fade in, step by step. A step never covers more than 1/30 s: the frame that builds a
    /// menu can be long, and would otherwise skip most of the fade.
    /// </summary>
    public class MenuFadeIn : MonoBehaviour
    {
        public CanvasGroup Group;
        float t;

        void Update()
        {
            t += Mathf.Min(GameTime.UnscaledDelta, 1f / 30f);
            float k = Ease.OutCubic(Mathf.Clamp01(t / MenuFade.InTime));
            if (Group) Group.alpha = k;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(MenuFade.StartScale, 1f, k);
            if (k >= 1f) Destroy(this);
        }
    }
}
