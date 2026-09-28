using UnityEngine;

namespace RollingSteel
{
    /// The marble. Input pushes it around; it is never teleported by the player,
    /// so all the character comes out of momentum, friction and the slope it is
    /// sitting on - which is the whole point of a game like this.
    [RequireComponent(typeof(Rigidbody))]
    public class MarbleController : MonoBehaviour
    {
        public float Accel = 34f;
        public float MaxSpeed = 13f;
        public float AirControl = 0.3f;
        public float ExtraGravity = 12f;      // on top of world gravity; stops it floating

        public Rigidbody Body { get; private set; }
        public bool Grounded { get; private set; }
        public bool Frozen { get; set; }
        /// Where the marble was last actually resting on something.
        public Vector3 LastGrounded { get; private set; }

        /// Scripted input, used by -demo so the game can play itself for a capture.
        public Vector2 ScriptedInput;
        public bool UseScriptedInput;

        Transform cam;
        MeshRenderer view;
        bool groundedThisStep;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            view = GetComponent<MeshRenderer>();
            Body.mass = 1f;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.05f;
            Body.maxAngularVelocity = 60f;    // default 7 makes it look like it's skidding
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Bind(Camera c) => cam = c.transform;

        public void SetVisible(bool visible) { if (view != null) view.enabled = visible; }

        void FixedUpdate()
        {
            Grounded = groundedThisStep;
            groundedThisStep = false;

            if (Frozen)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
                return;
            }

            Vector2 raw = UseScriptedInput
                ? ScriptedInput
                : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (cam != null && raw.sqrMagnitude > 0.0001f)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

                Vector3 wish = right * raw.x + fwd * raw.y;
                if (wish.sqrMagnitude > 1f) wish.Normalize();

                Body.AddForce(wish * (Accel * (Grounded ? 1f : AirControl)), ForceMode.Acceleration);
            }

            Body.AddForce(Vector3.down * ExtraGravity, ForceMode.Acceleration);

            // cap ground speed only; falling is allowed to be as fast as it likes
            Vector3 v = Body.linearVelocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (flat.magnitude > MaxSpeed)
            {
                flat = flat.normalized * MaxSpeed;
                Body.linearVelocity = new Vector3(flat.x, v.y, flat.z);
            }

            RecordSafeSpot();
        }

        void RecordSafeSpot()
        {
            if (Grounded) LastGrounded = transform.position;
        }

        public void Teleport(Vector3 pos)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            transform.position = pos;
            Body.position = pos;
            LastGrounded = pos;
        }

        public float Speed => new Vector3(Body.linearVelocity.x, 0f, Body.linearVelocity.z).magnitude;

        void OnCollisionStay(Collision c)
        {
            for (int i = 0; i < c.contactCount; i++)
                if (c.GetContact(i).normal.y > 0.45f) { groundedThisStep = true; return; }
        }
    }
}
