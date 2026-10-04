using UnityEngine;

namespace AfterHours
{
    /// <summary>A loose object you can pick up, carry, place, drop and throw.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Holdable : MonoBehaviour, IInteractable
    {
        public string Id;
        public string DisplayName = "Item";
        public bool Held { get; internal set; }
        public Rigidbody Body { get; private set; }
        public Vector3 ThrownFrom { get; internal set; }
        public bool InFlight { get; internal set; }
        public float HoldDistance = 0.55f;
        public bool Locked;            // e.g. while being binned

        void Awake() => Body = GetComponent<Rigidbody>();

        public virtual string Prompt(Interactor who)
        {
            if (Locked || Hands.Instance == null || Hands.Instance.Holding != null) return null;
            return "Pick up · " + DisplayName;
        }

        public virtual void Interact(Interactor who)
        {
            if (Hands.Instance != null) Hands.Instance.Grab(this);
        }

        /// <summary>Called when released by the hands (placed, dropped or thrown).</summary>
        public virtual void OnReleased(bool thrown) { }

        /// <summary>Called when grabbed.</summary>
        public virtual void OnGrabbed() { }

        void OnCollisionEnter(Collision c)
        {
            if (InFlight && c.relativeVelocity.magnitude < 3f) InFlight = false;
        }
    }
}
