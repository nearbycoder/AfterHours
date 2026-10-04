using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AfterHours
{
    /// <summary>
    /// Before/after polaroids for the shift report: each room is photographed from a fixed
    /// viewpoint with its lights forced on, once when the night starts and once at clock-out.
    /// </summary>
    public class RoomPhotos : MonoBehaviour
    {
        public static RoomPhotos Instance { get; private set; }
        Camera cam;
        public readonly Dictionary<string, RenderTexture> Before = new(), After = new();

        static readonly Dictionary<string, (Vector3 pos, Vector3 look)> Views = new()
        {
            { "reception", (new Vector3(14.4f, 1.9f, 0.5f), new Vector3(9.5f, 0.6f, 3.6f)) },
            { "bullpen", (new Vector3(8.0f, 2.1f, 5.6f), new Vector3(13.2f, 0.4f, 12.0f)) },
            { "conference", (new Vector3(6.5f, 2.0f, 15.6f), new Vector3(2.4f, 0.6f, 10.0f)) },
            { "office", (new Vector3(18.5f, 2.0f, 15.6f), new Vector3(22.8f, 0.5f, 10.4f)) },
            { "breakroom", (new Vector3(6.6f, 2.0f, 8.7f), new Vector3(2.0f, 0.5f, 4.0f)) },
        };

        public static RoomPhotos Create()
        {
            var go = new GameObject("RoomPhotos");
            Instance = go.AddComponent<RoomPhotos>();
            Instance.cam = go.AddComponent<Camera>();
            Instance.cam.enabled = false;
            Instance.cam.fieldOfView = 62f;
            Instance.cam.nearClipPlane = 0.05f;
            Instance.cam.farClipPlane = 300f;
            PostFx.ConfigureCamera(Instance.cam);
            Instance.cam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            return Instance;
        }

        public void Snap(IEnumerable<string> rooms, bool before)
        {
            var store = before ? Before : After;
            var office = OfficeBuilder.Instance;
            if (office == null) return;
            foreach (var id in rooms)
            {
                if (!Views.TryGetValue(id, out var v) || !office.Rooms.TryGetValue(id, out var room)) continue;
                if (!store.TryGetValue(id, out var rt) || rt == null)
                {
                    rt = new RenderTexture(640, 400, 24, RenderTextureFormat.ARGB32) { name = "Photo_" + id, antiAliasing = 2 };
                    store[id] = rt;
                }
                bool was = room.LightsOn;
                room.SetLights(true, true);
                cam.transform.SetPositionAndRotation(v.pos, Quaternion.LookRotation(v.look - v.pos));
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                room.SetLights(was, true);
            }
        }

        public void Clear()
        {
            foreach (var rt in Before.Values) if (rt) rt.Release();
            foreach (var rt in After.Values) if (rt) rt.Release();
            Before.Clear();
            After.Clear();
        }
    }
}
