using System;
using UnityEngine;

namespace AfterHours
{
    public enum FloorKind { Tile, Carpet, Vinyl }

    /// <summary>Marks a collider's walking sound.</summary>
    public class FloorSurface : MonoBehaviour
    {
        public FloorKind Kind = FloorKind.Tile;
    }

    /// <summary>
    /// First-person body: CharacterController movement, smoothed mouse look, crouch, head bob and
    /// footstep events. The camera lives on <see cref="Head"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class FirstPersonController : MonoBehaviour
    {
        public const float StandHeight = 1.78f, CrouchHeight = 1.15f;
        public const float StandEye = 1.62f, CrouchEye = 1.02f;

        public float WalkSpeed = 3.4f, BriskSpeed = 5.2f, CrouchSpeed = 1.8f;
        public Transform Head { get; private set; }
        public Camera Camera { get; private set; }
        public CharacterController Body { get; private set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public bool Crouching { get; private set; }
        public Vector3 Velocity => velocity;
        public bool LookLocked { get; set; }
        public bool MoveLocked { get; set; }

        /// <summary>Fired on each footfall with the floor under the player.</summary>
        public event Action<FloorKind, float> Footstep;

        Vector3 velocity;
        float eye = StandEye, bobPhase, strideDist, verticalVel;
        Vector2 lookVel;
        float kickPitch, kickVel;

        public static FirstPersonController Create(Vector3 position, float yaw)
        {
            var go = new GameObject("Player") { layer = Layers.Player };
            go.transform.position = position;
            var body = go.AddComponent<CharacterController>();
            body.height = StandHeight;
            body.radius = 0.3f;
            body.center = new Vector3(0, StandHeight * 0.5f, 0);
            body.stepOffset = 0.3f;
            body.slopeLimit = 50f;
            body.skinWidth = 0.03f;
            var fpc = go.AddComponent<FirstPersonController>();
            fpc.Body = body;
            fpc.Yaw = yaw;

            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0, StandEye, 0);
            fpc.Head = head;
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(head, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = Settings.Current.Fov;
            camGo.AddComponent<AudioListener>();
            fpc.Camera = cam;
            return fpc;
        }

        /// <summary>Small camera kick (throws, impacts, discoveries).</summary>
        public void Kick(float degrees) => kickVel -= degrees * 18f;

        public void Teleport(Vector3 pos, float yaw, float pitch = 0f)
        {
            Body.enabled = false;
            transform.position = pos;
            Body.enabled = true;
            Yaw = yaw; Pitch = pitch; velocity = Vector3.zero; verticalVel = 0;
            ApplyRotation();
        }

        void Update()
        {
            var input = GameInput.Frame;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!LookLocked)
            {
                // Light smoothing keeps raw mouse jitter out of the camera without feeling floaty.
                lookVel = Vector2.Lerp(lookVel, input.Look, 1f - Mathf.Exp(-dt * 38f));
                Yaw += lookVel.x;
                Pitch = Mathf.Clamp(Pitch - lookVel.y, -86f, 86f);
            }

            // Crouch
            bool wantCrouch = input.Crouch && !MoveLocked;
            if (!wantCrouch && Crouching)
            {
                // Only stand up if there is headroom.
                var top = transform.position + Vector3.up * (CrouchHeight + 0.05f);
                if (Physics.SphereCast(top, 0.28f, Vector3.up, out _, StandHeight - CrouchHeight, Layers.WalkMask, QueryTriggerInteraction.Ignore))
                    wantCrouch = true;
            }
            Crouching = wantCrouch;
            float targetH = Crouching ? CrouchHeight : StandHeight;
            Body.height = Mathf.MoveTowards(Body.height, targetH, dt * 4f);
            Body.center = new Vector3(0, Body.height * 0.5f, 0);
            eye = Mathf.Lerp(eye, Crouching ? CrouchEye : StandEye, 1f - Mathf.Exp(-dt * 12f));

            // Move
            var move = MoveLocked ? Vector2.zero : Vector2.ClampMagnitude(input.Move, 1f);
            float speed = Crouching ? CrouchSpeed : input.Sprint ? BriskSpeed : WalkSpeed;
            var fwd = Quaternion.Euler(0, Yaw, 0);
            var wish = fwd * new Vector3(move.x, 0, move.y) * speed;
            float accel = wish.sqrMagnitude > velocity.sqrMagnitude ? 14f : 10f;
            velocity = Vector3.Lerp(velocity, wish, 1f - Mathf.Exp(-dt * accel));

            verticalVel = Body.isGrounded ? -2f : verticalVel - 20f * dt;
            Body.Move((velocity + Vector3.up * verticalVel) * dt);

            // Head bob + footsteps
            float planar = new Vector2(Body.velocity.x, Body.velocity.z).magnitude;
            if (Body.isGrounded && planar > 0.3f)
            {
                float stride = Crouching ? 0.5f : input.Sprint ? 0.82f : 0.68f;
                bobPhase += planar * dt / stride * Mathf.PI;
                strideDist += planar * dt;
                if (strideDist >= stride)
                {
                    strideDist -= stride;
                    Footstep?.Invoke(FloorUnderfoot(), Mathf.Clamp01(planar / BriskSpeed));
                }
            }
            else
            {
                bobPhase = Mathf.Lerp(bobPhase, Mathf.Round(bobPhase / Mathf.PI) * Mathf.PI, dt * 6f);
            }
            float bobAmt = Settings.Current.HeadBob ? Mathf.Clamp01(planar / WalkSpeed) : 0f;
            var bob = new Vector3(Mathf.Cos(bobPhase) * 0.012f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.026f - 0.013f, 0) * bobAmt;

            // Camera kick spring
            kickVel += (-kickPitch * 140f - kickVel * 16f) * dt;
            kickPitch += kickVel * dt;

            Head.localPosition = new Vector3(0, eye, 0) + bob;
            ApplyRotation();
            Camera.fieldOfView = Mathf.Lerp(Camera.fieldOfView, Settings.Current.Fov + FovPunch, 1f - Mathf.Exp(-dt * 10f));
            FovPunch = Mathf.Lerp(FovPunch, 0f, 1f - Mathf.Exp(-dt * 3f));
        }

        public float FovPunch { get; set; }

        void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0, Yaw, 0);
            Head.localRotation = Quaternion.Euler(Pitch + kickPitch, 0, 0);
        }

        public FloorKind FloorUnderfoot()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, Layers.WalkMask, QueryTriggerInteraction.Ignore))
            {
                var fs = hit.collider.GetComponentInParent<FloorSurface>();
                if (fs) return fs.Kind;
            }
            return FloorKind.Tile;
        }
    }
}
