using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    public class BuiltLevel
    {
        public GameObject Root;
        public Vector3 SpawnWorld;
        public Vector3 GoalWorld;
        public float StartZ, GoalZ;             // course-space, for the progress bar
        public float KillY;
        public readonly List<EnemyBall> Enemies = new List<EnemyBall>();
        public readonly List<Vector3> PathWorld = new List<Vector3>();
    }

    public static class LevelBuilder
    {
        /// Courses are authored with +Z running down-course; yawing the whole
        /// thing 45 degrees lines course space up with the isometric camera, so
        /// "push up" on the stick really does send the marble down-course.
        public const float CourseYaw = 45f;

        public static BuiltLevel Build(Level lvl)
        {
            var built = new BuiltLevel();
            var root = new GameObject("Course_" + lvl.Name);
            root.transform.rotation = Quaternion.Euler(0f, CourseYaw, 0f);
            built.Root = root;

            float lowest = float.MaxValue;
            float goalZ = 0f;
            int normalIndex = 0;

            foreach (var blk in lvl.Blocks)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = blk.Surface.ToString();
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = blk.Center;
                go.transform.localRotation = blk.Rot;
                go.transform.localScale = blk.Size;

                string matName = MatLib.VisualName(blk.Surface);
                if (blk.Surface == Surface.Normal && (normalIndex++ & 1) == 1)
                    matName = "DeckAlt";          // banding makes the descent readable
                go.GetComponent<MeshRenderer>().sharedMaterial = MatLib.Get(matName);

                var col = go.GetComponent<BoxCollider>();
                if (blk.Surface == Surface.Acid)
                {
                    col.isTrigger = true;
                    go.AddComponent<AcidZone>();
                    go.GetComponent<MeshRenderer>().shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    col.sharedMaterial = MatLib.Physics(blk.Surface);
                }

                if (blk.Surface == Surface.Goal)
                {
                    goalZ = blk.Center.z;
                    built.GoalWorld = root.transform.TransformPoint(blk.Center + Vector3.up * 1.5f);
                    AddGoalTrigger(root.transform, blk);
                }

                lowest = Mathf.Min(lowest, blk.Center.y - blk.Size.y);
            }

            foreach (var e in lvl.Enemies)
                built.Enemies.Add(SpawnEnemy(e, root.transform));

            // Densify the centreline: with only segment endpoints, anything
            // following it cuts corners and clips the edge of narrow decks.
            var pts = lvl.Path;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0)
                {
                    Vector3 a = pts[i - 1], b = pts[i];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2f));
                    for (int k = 1; k < steps; k++)
                        built.PathWorld.Add(root.transform.TransformPoint(
                            Vector3.Lerp(a, b, (float)k / steps) + Vector3.up * 0.5f));
                }
                built.PathWorld.Add(root.transform.TransformPoint(pts[i] + Vector3.up * 0.5f));
            }

            built.SpawnWorld = root.transform.TransformPoint(lvl.Spawn);
            built.StartZ = lvl.Spawn.z;
            built.GoalZ = goalZ;
            built.KillY = lowest - 14f;
            return built;
        }

        static void AddGoalTrigger(Transform root, Block deck)
        {
            var trig = new GameObject("GoalTrigger");
            trig.transform.SetParent(root, false);
            trig.transform.localPosition = deck.Center + deck.Rot * Vector3.up * (deck.Size.y * 0.5f + 1.3f);
            trig.transform.localRotation = deck.Rot;

            var box = trig.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(deck.Size.x * 0.9f, 2.6f, deck.Size.z * 0.9f);
            trig.AddComponent<GoalPad>();
        }

        static EnemyBall SpawnEnemy(EnemySpec spec, Transform root)
        {
            bool lethal = spec.Kind == EnemyKind.Wanderer;
            float radius = lethal ? 0.55f : 0.7f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = spec.Kind.ToString();
            go.transform.position = root.TransformPoint(spec.Pos);
            go.transform.localScale = Vector3.one * (radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial =
                MatLib.Get(lethal ? "Blob" : "Steel");

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = lethal ? 1f : 3f;
            rb.linearDamping = lethal ? 0f : 0.2f;
            rb.angularDamping = 0.05f;
            rb.isKinematic = lethal;             // blobs glide, steel marbles roll
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = go.GetComponent<SphereCollider>();
            if (lethal) col.isTrigger = true;
            else col.sharedMaterial = MatLib.Physics(Surface.Normal);

            var ball = go.AddComponent<EnemyBall>();
            ball.Init(spec, root);
            return ball;
        }
    }
}
