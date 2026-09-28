using UnityEngine;

namespace RollingSteel
{
    /// Orthographic chase camera in the flat 3/4 view the arcade cabinet used,
    /// but the angle is the player's to change: yaw, tilt and zoom are all live.
    /// MarbleController steers relative to this transform, so rotating the view
    /// rotates the controls with it and "push up" always means "away from me".
    public class IsoCamera : MonoBehaviour
    {
        public Transform Target;

        public float Yaw = 0f;          // desired; the camera eases toward it
        public float Pitch = 35f;
        public float Size = 13f;
        public float Distance = 90f;    // orthographic, so this is just "far enough back"
        public float Smooth = 0.16f;
        /// Set while the marble is falling to its death: keep framing the course
        /// it fell from instead of following it down into the dark.
        public bool Hold;
        public float LookAhead = 0.35f;

        const float YawSpeed = 100f;
        const float PitchSpeed = 45f;
        const float ZoomKeySpeed = 14f;
        const float MinPitch = 15f, MaxPitch = 80f;
        const float MinSize = 7f, MaxSize = 28f;
        const float SnapStep = 15f;     // release the key and settle on a tidy angle
        const float StepAngle = 45f;    // a tap turns exactly one isometric facet
        const float HoldDelay = 0.35f;  // keep holding and it spins freely instead

        Camera cam;
        Vector3 vel;
        float yawNow, pitchNow, sizeNow;
        float shake;
        float qHold, eHold;
        bool wasSpinning;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;

            yawNow = Yaw;
            pitchNow = Pitch;
            sizeNow = Size;
            cam.orthographicSize = sizeNow;
            transform.rotation = Quaternion.Euler(pitchNow, yawNow, 0f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // A tap steps one 45-degree facet; holding it down spins freely and
            // settles on a tidy angle when released.
            if (Input.GetKeyDown(KeyCode.E)) { Yaw += StepAngle; eHold = 0f; }
            if (Input.GetKeyDown(KeyCode.Q)) { Yaw -= StepAngle; qHold = 0f; }

            bool spinning = false;
            if (Input.GetKey(KeyCode.E))
            {
                eHold += dt;
                if (eHold > HoldDelay) { Yaw += YawSpeed * dt; spinning = true; }
            }
            else eHold = 0f;

            if (Input.GetKey(KeyCode.Q))
            {
                qHold += dt;
                if (qHold > HoldDelay) { Yaw -= YawSpeed * dt; spinning = true; }
            }
            else qHold = 0f;

            if (wasSpinning && !spinning) Yaw = Mathf.Round(Yaw / SnapStep) * SnapStep;
            wasSpinning = spinning;

            float tilt = (Input.GetKey(KeyCode.X) ? 1f : 0f) - (Input.GetKey(KeyCode.Z) ? 1f : 0f);
            if (Mathf.Abs(tilt) > 0f)
                Pitch = Mathf.Clamp(Pitch + tilt * PitchSpeed * dt, MinPitch, MaxPitch);

            float zoom = -Input.mouseScrollDelta.y * 1.2f;
            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) zoom += ZoomKeySpeed * dt;
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) zoom -= ZoomKeySpeed * dt;
            if (Mathf.Abs(zoom) > 0f) Size = Mathf.Clamp(Size + zoom, MinSize, MaxSize);
        }

        Quaternion Rig => Quaternion.Euler(pitchNow, yawNow, 0f);

        Vector3 Desired()
        {
            Vector3 focus = Target.position;

            var rb = Target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 v = rb.linearVelocity;
                focus += new Vector3(v.x, 0f, v.z) * LookAhead;
            }

            return focus - Rig * Vector3.forward * Distance;
        }

        void LateUpdate()
        {
            if (Target == null) return;

            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            if (Hold)
            {
                yawNow = Mathf.LerpAngle(yawNow, Yaw, k);
                pitchNow = Mathf.Lerp(pitchNow, Pitch, k);
                sizeNow = Mathf.Lerp(sizeNow, Size, k);
                cam.orthographicSize = sizeNow;
                transform.rotation = Rig;
                transform.position += ShakeOffset();
                return;
            }

            yawNow = Mathf.LerpAngle(yawNow, Yaw, k);
            pitchNow = Mathf.Lerp(pitchNow, Pitch, k);
            sizeNow = Mathf.Lerp(sizeNow, Size, k);
            cam.orthographicSize = sizeNow;

            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref vel, Smooth);
            transform.rotation = Rig;
            transform.position += ShakeOffset();
        }

        Vector3 ShakeOffset()
        {
            if (shake <= 0.001f) return Vector3.zero;
            Vector3 o = Random.insideUnitSphere * shake;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 3.5f);
            return o;
        }

        /// A knock, for deaths. Decays on unscaled time so it still reads during
        /// the slow-motion beat.
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        /// Jump straight to the framing with no easing (level load, respawn).
        public void Snap()
        {
            if (Target == null) return;
            yawNow = Yaw;
            pitchNow = Pitch;
            sizeNow = Size;
            cam.orthographicSize = sizeNow;
            vel = Vector3.zero;
            transform.position = Desired();
            transform.rotation = Rig;
        }
    }
}
