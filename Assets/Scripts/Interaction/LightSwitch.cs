using UnityEngine;

namespace AfterHours
{
    /// <summary>Wall switch for a room's lights.</summary>
    public class LightSwitch : MonoBehaviour, IInteractable, IHandsFree
    {
        public Room Room;
        Transform toggle;

        public static LightSwitch Create(Transform anchor, Room room)
        {
            var go = new GameObject("Switch_" + room.Id);
            go.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            plate.transform.SetParent(go.transform, false);
            plate.transform.localScale = new Vector3(0.08f, 0.12f, 0.012f);
            plate.transform.localPosition = new Vector3(0, 0, 0.006f);
            plate.GetComponent<Renderer>().sharedMaterial = Res.Lit(Palette.Hex("EEECE6"), 0.5f);
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = "Toggle";
            Object.Destroy(t.GetComponent<Collider>());
            t.transform.SetParent(go.transform, false);
            t.transform.localScale = new Vector3(0.022f, 0.04f, 0.014f);
            t.transform.localPosition = new Vector3(0, 0, 0.016f);
            t.GetComponent<Renderer>().sharedMaterial = Res.Lit(Palette.Hex("F7F6F2"), 0.6f);
            // A tiny locator LED so switches are findable in the dark.
            var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(led.GetComponent<Collider>());
            led.transform.SetParent(go.transform, false);
            led.transform.localScale = new Vector3(0.008f, 0.008f, 0.004f);
            led.transform.localPosition = new Vector3(0, -0.045f, 0.013f);
            led.GetComponent<Renderer>().sharedMaterial = Res.Emissive(Color.black, new Color(1f, 0.55f, 0.15f), 3f);
            var s = go.AddComponent<LightSwitch>();
            s.Room = room;
            s.toggle = t.transform;
            // Larger invisible collider makes switches easy to hit.
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.16f, 0.2f, 0.05f);
            bc.center = new Vector3(0, 0, 0.02f);
            s.Sync();
            return s;
        }

        public string Prompt(Interactor who) => Room.LightsOn ? "Lights off" : "Lights on";

        public void Interact(Interactor who) => Toggle();

        public void Toggle()
        {
            Room.SetLights(!Room.LightsOn);
            Sfx.Play("light_switch", transform.position, 0.6f);
            if (Room.LightsOn) Sfx.Play("fluoro_on", transform.position + Vector3.up * 1.4f, 0.5f);
            Sync();
            Events.Raise(GameEvent.LightsChanged, Room.Id);
        }

        public void Sync()
        {
            if (toggle) toggle.localRotation = Quaternion.Euler(Room.LightsOn ? -18f : 18f, 0, 0);
        }
    }
}
