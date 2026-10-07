using UnityEngine;

namespace AfterHours
{
    /// <summary>Anything the player can point at and press E on.</summary>
    public interface IInteractable
    {
        /// <summary>Prompt label, or null when not currently usable.</summary>
        string Prompt(Interactor who);
        void Interact(Interactor who);
        Transform transform { get; }
    }

    /// <summary>
    /// An interactable that works with your hands full (a door, a light switch, a chair): with
    /// something held, E on it uses it rather than placing or dropping what you're holding.
    /// </summary>
    public interface IHandsFree { }

    /// <summary>
    /// Finds the interactable under the reticle (with a little aim assist), shows its prompt and
    /// handles E. Cleaning has priority on LMB; interaction is always E. With something held only
    /// <see cref="IHandsFree"/> things are found; otherwise E belongs to <see cref="Hands"/>.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class Interactor : MonoBehaviour
    {
        public FirstPersonController Player;
        public CleaningController Cleaning;
        public IInteractable Focus { get; private set; }
        public float Reach = 2.3f;
        public bool Locked { get; set; }

        public static Interactor Instance { get; private set; }
        /// <summary>Something held, and the reticle on a thing E uses anyway (a door, a switch, a chair).</summary>
        public bool HandsFreeFocus => Focus is IHandsFree && Hands.Instance != null && Hands.Instance.Holding != null;

        void Awake() => Instance = this;

        void Update()
        {
            Focus = null;
            if (Locked || Player == null) return;
            bool holding = Hands.Instance != null && Hands.Instance.Holding != null;
            var cam = Player.Camera.transform;
            var ray = new Ray(cam.position, cam.forward);
            if (Physics.Raycast(ray, out var hit, Reach, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                Focus = hit.collider.GetComponentInParent<IInteractable>();
            if (Focus == null)
            {
                // Aim assist: small sphere cast for tiny objects (switches, papers).
                if (Physics.SphereCast(ray, 0.07f, out var sh, Reach, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                    Focus = sh.collider.GetComponentInParent<IInteractable>();
            }
            if (Focus != null && (Focus.Prompt(this) == null || holding && Focus is not IHandsFree)) Focus = null;
            if (Focus != null && GameInput.Frame.Interact) Focus.Interact(this);
        }
    }
}
