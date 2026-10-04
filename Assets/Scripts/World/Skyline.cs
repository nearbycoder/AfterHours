using UnityEngine;

namespace AfterHours
{
    /// <summary>Procedural city seen through the windows: tower blocks plus a gradient sky dome.</summary>
    public static class Skyline
    {
        public static Transform Build(Transform parent, Vector3 origin, int seed, float width = 120f, Vector3? facing = null)
        {
            var root = new GameObject("Skyline").transform;
            root.SetParent(parent, false);
            root.localPosition = origin;
            root.localRotation = Quaternion.LookRotation(facing ?? Vector3.forward);
            var rng = new Rng(seed);
            var tmpl = Res.Material("AH_Skyline");

            // Sky backdrop
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(sky.GetComponent<Collider>());
            sky.name = "Sky";
            sky.transform.SetParent(root, false);
            sky.transform.localPosition = new Vector3(0, 20f, 150f);
            sky.transform.localScale = new Vector3(width * 3f, 140f, 1);
            var skyMat = new Material(tmpl);
            skyMat.SetFloat("_Mode", 1);
            sky.GetComponent<Renderer>().sharedMaterial = skyMat;
            sky.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Three rows of towers, nearer ones taller and more detailed.
            for (int row = 0; row < 3; row++)
            {
                float z = 30f + row * 35f;
                float x = -width * 0.5f - row * 20f;
                while (x < width * 0.5f + row * 20f)
                {
                    float w = rng.Range(8f, 18f);
                    float d = rng.Range(8f, 16f);
                    float h = rng.Range(12f, 60f) * (row == 0 ? 0.8f : 1.1f) + (rng.Value > 0.85f ? 40f : 0f);
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(b.GetComponent<Collider>());
                    b.name = "Tower";
                    b.transform.SetParent(root, false);
                    // The office is on the 14th floor (~45 m up): towers start far below.
                    b.transform.localPosition = new Vector3(x + w * 0.5f, h * 0.5f - 46f, z + rng.Range(-4f, 4f));
                    b.transform.localScale = new Vector3(w, h + 46f, d);
                    var m = new Material(tmpl);
                    m.SetFloat("_Seed", rng.Range(0f, 100f));
                    m.SetColor("_Body", Color.Lerp(new Color(0.03f, 0.04f, 0.07f), new Color(0.06f, 0.07f, 0.1f), rng.Value) * (1f - row * 0.2f));
                    m.SetVector("_WindowSize", new Vector4(rng.Range(1.3f, 2.2f), rng.Range(2.8f, 3.6f), 0, 0));
                    m.SetFloat("_LitFraction", rng.Range(0.12f, 0.38f));
                    var r = b.GetComponent<Renderer>();
                    r.sharedMaterial = m;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    // Red aviation light on tall roofs.
                    if (h > 45f)
                    {
                        var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        Object.Destroy(beacon.GetComponent<Collider>());
                        beacon.transform.SetParent(root, false);
                        beacon.transform.localPosition = new Vector3(x + w * 0.5f, h - 46f + 0.6f, b.transform.localPosition.z);
                        beacon.transform.localScale = Vector3.one * 0.7f;
                        beacon.GetComponent<Renderer>().sharedMaterial = Res.Emissive(Color.red, new Color(1f, 0.1f, 0.05f), 6f);
                        beacon.AddComponent<Blinker>().Period = rng.Range(1.6f, 2.4f);
                    }
                    x += w + rng.Range(1f, 6f);
                }
            }
            return root;
        }
    }

    /// <summary>Blinks a renderer on and off (aviation beacons, standby LEDs).</summary>
    public class Blinker : MonoBehaviour
    {
        public float Period = 2f, Duty = 0.18f;
        Renderer r;
        float offset;
        void Awake() { r = GetComponent<Renderer>(); offset = Random.value * 10f; }
        void Update() { if (r) r.enabled = Mathf.Repeat(Time.time + offset, Period) < Period * Duty; }
    }
}
