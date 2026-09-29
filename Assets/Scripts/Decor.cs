using UnityEngine;

namespace RollingSteel
{
    /// Slow drift for background scenery. Purely cosmetic - decor never has a
    /// collider, so none of this can touch the marble.
    public class FloatBob : MonoBehaviour
    {
        public float Amplitude = 0.6f;
        public float Speed = 0.45f;
        public float Spin = 7f;
        public float Phase;

        Vector3 home;
        bool ready;

        void Start() { home = transform.localPosition; ready = true; }

        void Update()
        {
            if (!ready) return;
            transform.localPosition = home + Vector3.up * (Mathf.Sin(Time.time * Speed + Phase) * Amplitude);
            transform.Rotate(Vector3.up, Spin * Time.deltaTime, Space.Self);
        }
    }

    /// Abstract floating scenery: a handful of pieces off to the sides of the
    /// course, drifting. Deliberately sparse - it is there to give the void a
    /// sense of depth and to make the three courses feel like different places,
    /// not to decorate the track itself.
    public static class Decor
    {
        const int PerLevel = 12;

        public static void Scatter(Level lvl, BuiltLevel built, Transform root)
        {
            var path = lvl.Path;
            if (path.Count < 4) return;

            // seeded, so a course looks the same every run (and in screenshots)
            var rng = new System.Random(lvl.Name.GetHashCode() ^ (lvl.DecorTheme * 7919));

            for (int i = 0; i < PerLevel; i++)
            {
                int idx = Mathf.Clamp(
                    Mathf.RoundToInt((i + 0.5f) / PerLevel * (path.Count - 1)), 1, path.Count - 2);

                Vector3 anchor = path[idx];
                Vector3 tangent = path[idx + 1] - path[idx - 1];
                tangent.y = 0f;
                if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.forward;
                tangent.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, tangent);

                float sgn = rng.Next(2) == 0 ? -1f : 1f;
                Vector3 pos = anchor
                            + side * (sgn * Rand(rng, 22f, 42f))
                            + tangent * Rand(rng, -7f, 7f)
                            + Vector3.up * Rand(rng, -24f, 2f);

                var piece = Build(lvl.DecorTheme, root, pos, rng);

                var bob = piece.AddComponent<FloatBob>();
                bob.Phase = Rand(rng, 0f, 6.28f);
                bob.Amplitude = Rand(rng, 0.3f, 1.1f);
                bob.Speed = Rand(rng, 0.2f, 0.55f);
                bob.Spin = Rand(rng, -11f, 11f);
            }
        }

        static float Rand(System.Random r, float lo, float hi) => lo + (float)r.NextDouble() * (hi - lo);

        static GameObject Build(int theme, Transform root, Vector3 pos, System.Random rng)
        {
            var host = new GameObject("Decor");
            host.transform.SetParent(root, false);
            host.transform.localPosition = pos;
            host.transform.localRotation = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f);

            float s = Rand(rng, 0.9f, 2.2f);
            host.transform.localScale = Vector3.one * s;

            switch (theme)
            {
                case 1: if (rng.Next(3) == 0) Orb(host.transform, rng); else Crystal(host.transform, rng); break;
                case 2: if (rng.Next(3) == 0) Shard(host.transform, rng); else Spire(host.transform, rng); break;
                case 3: if (rng.Next(2) == 0) Ring(host.transform, rng); else Arch(host.transform, rng); break;
                case 4: if (rng.Next(2) == 0) Balloon(host.transform, rng); else Candy(host.transform, rng); break;
                case 5: if (rng.Next(3) == 0) Shard(host.transform, rng); else Monolith(host.transform, rng); break;
                default: if (rng.Next(4) == 0) Island(host.transform, rng); else Tree(host.transform, rng); break;
            }
            return host;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 p, Vector3 scale,
                               Quaternion rot, string material)
        {
            var go = GameObject.CreatePrimitive(type);

            // scenery must never be in the way
            var col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Object.Destroy(col); }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = p;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = MatLib.Get(material);
            return go;
        }

        // ---- theme 0: a grove ------------------------------------------

        static void Tree(Transform t, System.Random rng)
        {
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f),
                 new Vector3(0.35f, 1.6f, 0.35f), Quaternion.identity, "Bark");

            int tiers = rng.Next(2, 4);
            float y = 1.6f;
            float w = Rand(rng, 2.2f, 3.0f);
            for (int i = 0; i < tiers; i++)
            {
                Part(t, PrimitiveType.Cube, new Vector3(0f, y, 0f),
                     new Vector3(w, w * 0.55f, w),
                     Quaternion.Euler(0f, 45f + i * 22f, 0f), "Leaf");
                y += w * 0.5f;
                w *= 0.68f;
            }
        }

        static void Island(Transform t, System.Random rng)
        {
            Part(t, PrimitiveType.Cube, Vector3.zero,
                 new Vector3(Rand(rng, 4f, 7f), 0.9f, Rand(rng, 4f, 7f)),
                 Quaternion.Euler(0f, Rand(rng, 0f, 90f), 0f), "Stone");
            Part(t, PrimitiveType.Cube, new Vector3(0f, 1.1f, 0f),
                 new Vector3(1.6f, 1.4f, 1.6f), Quaternion.Euler(0f, 30f, 0f), "Leaf");
        }

        // ---- theme 1: frost --------------------------------------------

        static void Crystal(Transform t, System.Random rng)
        {
            int shards = rng.Next(2, 4);
            for (int i = 0; i < shards; i++)
            {
                Part(t, PrimitiveType.Cube,
                     new Vector3(Rand(rng, -0.8f, 0.8f), Rand(rng, -0.4f, 1.2f), Rand(rng, -0.8f, 0.8f)),
                     new Vector3(Rand(rng, 0.5f, 0.9f), Rand(rng, 2.4f, 4.5f), Rand(rng, 0.5f, 0.9f)),
                     Quaternion.Euler(Rand(rng, -28f, 28f), Rand(rng, 0f, 90f), Rand(rng, -28f, 28f)),
                     "Crystal");
            }
        }

        static void Orb(Transform t, System.Random rng)
        {
            Part(t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * Rand(rng, 1.6f, 2.6f),
                 Quaternion.identity, "Crystal");
            Part(t, PrimitiveType.Cylinder, Vector3.zero,
                 new Vector3(3.2f, 0.06f, 3.2f),
                 Quaternion.Euler(Rand(rng, -25f, 25f), 0f, Rand(rng, -25f, 25f)), "Glow");
        }

        // ---- theme 3: aerial -------------------------------------------

        static void Arch(Transform t, System.Random rng)
        {
            float span = Rand(rng, 3.5f, 6f), h = Rand(rng, 3f, 5f);
            for (int side = -1; side <= 1; side += 2)
                Part(t, PrimitiveType.Cube, new Vector3(side * span * 0.5f, h * 0.5f, 0f),
                     new Vector3(0.5f, h, 0.5f), Quaternion.identity, "Pale");
            Part(t, PrimitiveType.Cube, new Vector3(0f, h + 0.3f, 0f),
                 new Vector3(span + 0.5f, 0.6f, 0.7f), Quaternion.identity, "Pale");
        }

        static void Ring(Transform t, System.Random rng)
        {
            float r = Rand(rng, 2.4f, 4.2f);
            var rot = Quaternion.Euler(Rand(rng, 60f, 120f), Rand(rng, 0f, 360f), 0f);
            Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(r, 0.12f, r), rot, "Crystal");
            Part(t, PrimitiveType.Cylinder, Vector3.zero,
                 new Vector3(r * 0.72f, 0.16f, r * 0.72f), rot, "Pale");
        }

        // ---- theme 4: silly --------------------------------------------

        static void Candy(Transform t, System.Random rng)
        {
            string[] mats = { "Candy", "Glow", "Leaf", "Crystal" };
            float y = 0f;
            int n = rng.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                float w = Rand(rng, 1.2f, 2.6f);
                Part(t, PrimitiveType.Cube, new Vector3(Rand(rng, -0.5f, 0.5f), y, Rand(rng, -0.5f, 0.5f)),
                     Vector3.one * w, Quaternion.Euler(Rand(rng, -30f, 30f), Rand(rng, 0f, 90f), Rand(rng, -30f, 30f)),
                     mats[rng.Next(mats.Length)]);
                y += w * 0.85f;
            }
        }

        static void Balloon(Transform t, System.Random rng)
        {
            float r = Rand(rng, 1.6f, 2.8f);
            Part(t, PrimitiveType.Sphere, Vector3.up * r, Vector3.one * r,
                 Quaternion.identity, rng.Next(2) == 0 ? "Candy" : "Glow");
            Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, r * 0.9f, 0.1f),
                 Quaternion.identity, "Pale");
        }

        // ---- theme 5: monoliths ----------------------------------------

        static void Monolith(Transform t, System.Random rng)
        {
            float h = Rand(rng, 5f, 11f), w = Rand(rng, 1.4f, 2.6f);
            Part(t, PrimitiveType.Cube, Vector3.up * (h * 0.5f), new Vector3(w, h, w * 0.7f),
                 Quaternion.Euler(0f, Rand(rng, 0f, 90f), Rand(rng, -6f, 6f)), "Stone");
            Part(t, PrimitiveType.Cube, Vector3.up * (h * 0.78f),
                 new Vector3(w * 1.05f, h * 0.06f, w * 0.75f),
                 Quaternion.Euler(0f, Rand(rng, 0f, 90f), 0f), "Danger");
        }

        // ---- theme 2: embers -------------------------------------------

        static void Spire(Transform t, System.Random rng)
        {
            float y = 0f, w = Rand(rng, 1.8f, 2.6f);
            int tiers = rng.Next(3, 6);
            for (int i = 0; i < tiers; i++)
            {
                float h = Rand(rng, 1.0f, 2.0f);
                Part(t, PrimitiveType.Cube, new Vector3(0f, y + h * 0.5f, 0f),
                     new Vector3(w, h, w), Quaternion.Euler(0f, i * 18f, 0f), "Stone");
                y += h;
                w *= 0.74f;
            }
            Part(t, PrimitiveType.Cube, new Vector3(0f, y + 0.6f, 0f),
                 new Vector3(w, 1.2f, w), Quaternion.Euler(0f, 45f, 0f), "Glow");
        }

        static void Shard(Transform t, System.Random rng)
        {
            Part(t, PrimitiveType.Cube, Vector3.zero,
                 new Vector3(Rand(rng, 0.5f, 1.1f), Rand(rng, 4f, 8f), Rand(rng, 0.5f, 1.1f)),
                 Quaternion.Euler(Rand(rng, -50f, 50f), Rand(rng, 0f, 360f), Rand(rng, -50f, 50f)),
                 "Stone");
        }
    }
}
