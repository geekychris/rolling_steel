using UnityEngine;

namespace RollingSteel
{
    /// Shrinks and removes a fragment. Debris that lingers is debris that ends up
    /// falling through the level for the rest of the run.
    public class Fader : MonoBehaviour
    {
        public float Life = 1.4f;

        float age;
        Vector3 born;

        void Start() { born = transform.localScale; }

        void Update()
        {
            age += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(age / Life);
            transform.localScale = born * (0.2f + 0.8f * k);
            if (age >= Life) Destroy(gameObject);
        }
    }

    /// The marble coming apart. Physical fragments rather than a particle system,
    /// because they bounce off the deck you just fell from, which sells it.
    public static class DeathFx
    {
        public static void Burst(Vector3 at, string material, int count, float force)
        {
            // Chunky shards that bounce, plus smaller bright sparks. The shards
            // alone read as a grey smudge at this camera distance.
            Spawn(at, material, count, 0.20f, 0.44f, force, 0.05f, true);
            Spawn(at, "Glow", count / 2, 0.08f, 0.16f, force * 1.5f, 0.01f, false);
        }

        static void Spawn(Vector3 at, string material, int count,
                          float minSize, float maxSize, float force, float mass, bool collide)
        {
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Shard";
                go.transform.position = at + Random.insideUnitSphere * 0.3f;
                go.transform.rotation = Random.rotation;
                go.transform.localScale = Vector3.one * Random.Range(minSize, maxSize);
                go.GetComponent<MeshRenderer>().sharedMaterial = MatLib.Get(material);

                var box = go.GetComponent<BoxCollider>();
                if (collide) box.sharedMaterial = MatLib.Marble;
                else box.enabled = false;         // sparks should not pile up on the deck

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.angularDamping = 0.05f;
                rb.linearVelocity = Random.onUnitSphere * Random.Range(force * 0.45f, force)
                                  + Vector3.up * Random.Range(force * 0.3f, force * 0.8f);
                rb.angularVelocity = Random.insideUnitSphere * 26f;

                go.AddComponent<Fader>().Life = collide ? Random.Range(1.2f, 2.0f)
                                                        : Random.Range(0.5f, 0.9f);
            }
        }
    }
}
