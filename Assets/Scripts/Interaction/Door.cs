using UnityEngine;

namespace AfterHours
{
    /// <summary>Hinged door: E toggles, it swings away from the player, and it can be locked.</summary>
    public class Door : MonoBehaviour, IInteractable
    {
        public string Id;
        public bool Locked;
        public string LockedMessage = "Locked";
        public float OpenAngle = 100f;
        public bool IsOpen { get; private set; }

        Quaternion closed;
        float angle, targetAngle, vel;
        float rattle;

        void Awake() => closed = transform.localRotation;

        public string Prompt(Interactor who) => Locked ? LockedMessage : IsOpen ? "Close" : "Open";

        public void Interact(Interactor who)
        {
            if (Locked)
            {
                rattle = 1f;
                Sfx.Play("door_close", transform.position, 0.25f, 1.6f);
                Hud.Instance?.Toast(LockedMessage, null, Palette.EvidenceRed, 1.4f);
                return;
            }
            SetOpen(!IsOpen, who != null ? who.Player.transform.position : (Vector3?)null);
        }

        public void SetOpen(bool open, Vector3? from = null, bool instant = false)
        {
            IsOpen = open;
            if (open)
            {
                // Swing away from whoever opens it.
                float sign = 1f;
                if (from.HasValue)
                {
                    var local = transform.parent ? transform.parent.InverseTransformPoint(from.Value) : from.Value;
                    var hingeLocal = transform.localPosition;
                    var closedDir = closed * Vector3.right;
                    var toPlayer = local - hingeLocal;
                    sign = Vector3.Cross(closedDir, toPlayer).y > 0 ? -1f : 1f;
                }
                targetAngle = OpenAngle * sign;
                if (!instant) Sfx.Play("door_open", transform.position, 0.55f);
            }
            else
            {
                targetAngle = 0f;
                if (!instant) Tween.Delay(0.45f, () => Sfx.Play("door_close", transform.position, 0.5f));
            }
            if (instant) { angle = targetAngle; Apply(); }
        }

        void Update()
        {
            // Spring toward the target with a slight overshoot.
            float k = 60f;
            vel += ((targetAngle - angle) * k - vel * 9f) * Time.deltaTime;
            angle += vel * Time.deltaTime;
            rattle = Mathf.MoveTowards(rattle, 0f, Time.deltaTime * 3f);
            Apply();
        }

        void Apply()
        {
            float shake = rattle > 0f ? Mathf.Sin(Time.time * 70f) * rattle * 1.2f : 0f;
            transform.localRotation = closed * Quaternion.Euler(0, angle + shake, 0);
        }
    }
}
