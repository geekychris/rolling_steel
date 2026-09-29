using UnityEngine;

namespace RollingSteel
{
    /// A bar rotating about a post. Kinematic, so it shoves the marble rather
    /// than being shoved by it, and the shove is what gets you - it is never
    /// lethal on its own, only in where it puts you.
    public class SweeperArm : MonoBehaviour
    {
        public float DegreesPerSecond = 70f;

        Rigidbody rb;
        float angle;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            angle = transform.localEulerAngles.y;
        }

        void FixedUpdate()
        {
            angle += DegreesPerSecond * Time.fixedDeltaTime;
            rb.MoveRotation(transform.parent != null
                ? transform.parent.rotation * Quaternion.Euler(0f, angle, 0f)
                : Quaternion.Euler(0f, angle, 0f));
        }

        void OnCollisionEnter(Collision c)
        {
            var marble = c.collider.GetComponentInParent<MarbleController>();
            if (marble == null) return;

            Vector3 push = marble.transform.position - transform.position;
            push.y = 0f;
            if (push.sqrMagnitude < 0.001f) return;

            marble.Body.AddForce(push.normalized * 7f, ForceMode.VelocityChange);
            Sfx.Play(Sfx.Clip.Clack);
        }
    }

    /// A block that lifts and slams down on a cycle. Fatal underneath while it
    /// is coming down; the rest of the time it is simply in the way.
    public class CrusherBlock : MonoBehaviour
    {
        public float Period = 2.4f;
        public float Lift = 4.5f;
        public float Phase;
        public float HalfWidth = 1.5f;

        Rigidbody rb;
        Vector3 down;      // resting position, sitting on the deck
        float clock;
        bool slammed;

        public void Init(Vector3 restPosition)
        {
            down = restPosition;
            rb = GetComponent<Rigidbody>();
            clock = Phase * Period;
        }

        void FixedUpdate()
        {
            clock += Time.fixedDeltaTime;
            float t = Mathf.Repeat(clock, Period) / Period;

            // slow lift for three quarters of the cycle, then a fast drop
            float h;
            if (t < 0.75f) h = Mathf.SmoothStep(0f, 1f, t / 0.75f);
            else h = 1f - Mathf.Pow((t - 0.75f) / 0.25f, 2f);

            rb.MovePosition(down + Vector3.up * (Lift * h));

            bool low = h < 0.12f;
            if (low && !slammed)
            {
                slammed = true;
                Crush();
            }
            else if (!low) slammed = false;
        }

        void Crush()
        {
            var director = GameDirector.Instance;
            if (director == null || !director.MarbleIsLive) return;

            Vector3 d = director.Marble.transform.position - down;
            if (Mathf.Abs(d.y) < 1.6f && new Vector2(d.x, d.z).magnitude < HalfWidth + 0.4f)
                director.KillMarble("CRUSHED");
            else
                Sfx.Play(Sfx.Clip.Thud);
        }
    }

    /// A zone that shoves the marble sideways while it is inside it.
    public class FanZone : MonoBehaviour
    {
        public Vector3 Push = Vector3.right;
        public float Power = 16f;

        void OnTriggerStay(Collider other)
        {
            var marble = other.GetComponentInParent<MarbleController>();
            if (marble == null || marble.Frozen) return;
            marble.Body.AddForce(Push.normalized * Power, ForceMode.Acceleration);
        }
    }

    /// Deck that drops away shortly after you touch it, then comes back. The
    /// delay is the whole point: you can cross it, but not stop on it.
    public class CrumbleTile : MonoBehaviour
    {
        public float Delay = 0.55f;
        public float Regrow = 3.5f;

        Vector3 home;
        Quaternion homeRot;
        Rigidbody rb;
        Renderer view;
        Collider box;
        float timer;
        enum State { Solid, Falling, Gone }
        State state = State.Solid;

        void Awake()
        {
            home = transform.localPosition;
            homeRot = transform.localRotation;
            view = GetComponent<Renderer>();
            box = GetComponent<Collider>();
        }

        void OnCollisionEnter(Collision c)
        {
            if (state != State.Solid) return;
            if (c.collider.GetComponentInParent<MarbleController>() == null) return;
            timer = Delay;
            state = State.Falling;
        }

        void Update()
        {
            switch (state)
            {
                case State.Falling:
                    timer -= Time.deltaTime;
                    // shiver, so it is obvious the floor is about to leave
                    transform.localPosition = home + new Vector3(
                        Mathf.Sin(Time.time * 60f) * 0.05f, 0f, Mathf.Cos(Time.time * 55f) * 0.05f);
                    if (timer <= 0f) Drop();
                    break;

                case State.Gone:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) Restore();
                    break;
            }
        }

        void Drop()
        {
            state = State.Gone;
            timer = Regrow;

            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 6f;
            rb.angularVelocity = Random.insideUnitSphere * 1.5f;
            Sfx.Play(Sfx.Clip.Thud);
        }

        void Restore()
        {
            state = State.Solid;
            if (rb != null) { Destroy(rb); rb = null; }
            transform.localPosition = home;
            transform.localRotation = homeRot;
            if (view != null) view.enabled = true;
            if (box != null) box.enabled = true;
        }
    }
}
