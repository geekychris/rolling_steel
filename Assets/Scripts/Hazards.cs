using UnityEngine;

namespace RollingSteel
{
    /// Rolling into acid dissolves the marble.
    public class AcidZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var marble = other.GetComponentInParent<MarbleController>();
            if (marble != null) GameDirector.Instance?.Kill(marble, "DISSOLVED");
        }
    }

    /// The finish pad.
    public class GoalPad : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var marble = other.GetComponentInParent<MarbleController>();
            if (marble != null) GameDirector.Instance?.ReachGoal(marble);
        }
    }

    /// Two flavours of nuisance:
    ///   Chaser   - a heavy steel marble that rolls at you and shoulders you off.
    ///   Wanderer - a green blob that slides across the deck; touching it is fatal.
    public class EnemyBall : MonoBehaviour
    {
        const float PatrolAmplitude = 3.2f;

        EnemySpec spec;
        Transform course;
        Vector3 home;
        Rigidbody rb;
        float phase;

        public void Init(EnemySpec s, Transform courseRoot)
        {
            spec = s;
            course = courseRoot;
            home = transform.position;
            rb = GetComponent<Rigidbody>();
        }

        public void ResetToHome()
        {
            phase = 0f;
            transform.position = home;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        void FixedUpdate()
        {
            var director = GameDirector.Instance;
            if (director == null || !director.AnyMarbleLive) return;

            // chase whoever is closest, which in two-player is a decision
            var marble = director.NearestMarble(transform.position);
            if (marble == null) return;

            if (spec.Kind == EnemyKind.Wanderer)
            {
                // slide back and forth across the course, independent of the player
                phase += Time.fixedDeltaTime * spec.Speed * 0.35f;
                Vector3 across = course.right;          // course-space +X in world
                rb.MovePosition(home + across * (Mathf.Sin(phase) * PatrolAmplitude));
                return;
            }

            // Chaser: wake up when the marble is close, otherwise settle back home.
            Vector3 target = marble.transform.position;
            float dist = Vector3.Distance(transform.position, target);
            Vector3 to = dist <= spec.Range ? target - transform.position : home - transform.position;
            to.y = 0f;

            if (to.sqrMagnitude > 0.04f)
                rb.AddForce(to.normalized * spec.Speed, ForceMode.Acceleration);

            // keep them from falling forever once they miss
            if (transform.position.y < director.KillY + 6f) ResetToHome();
        }

        void OnTriggerEnter(Collider other)
        {
            if (spec.Kind != EnemyKind.Wanderer) return;
            var marble = other.GetComponentInParent<MarbleController>();
            if (marble != null) GameDirector.Instance?.Kill(marble, "EATEN");
        }

        void OnCollisionEnter(Collision c)
        {
            if (spec.Kind != EnemyKind.Chaser) return;
            var marble = c.collider.GetComponentInParent<MarbleController>();
            if (marble == null) return;

            // a proper shove, so a hit near an edge actually costs you
            Vector3 push = marble.transform.position - transform.position;
            push.y = 0f;
            if (push.sqrMagnitude < 0.001f) return;
            marble.Body.AddForce(push.normalized * 5f, ForceMode.VelocityChange);
            Sfx.Play(Sfx.Clip.Clack);
        }
    }
}
