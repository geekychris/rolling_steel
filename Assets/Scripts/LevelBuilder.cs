using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    public class BuiltLevel
    {
        public GameObject Root;
        public Vector3 SpawnWorld;
        public Vector3 GoalWorld;
        public float KillY;

        public readonly List<EnemyBall> Enemies = new List<EnemyBall>();
        public readonly List<Vector3> PathWorld = new List<Vector3>();
        /// Parallel to PathWorld: is there solid deck under this point?
        public readonly List<bool> PathSupported = new List<bool>();
        /// Parallel to PathWorld: distance along the route, normalised 0..1.
        /// Curves mean course-space Z is no longer a usable progress measure.
        public readonly List<float> PathProgress = new List<float>();
    }

    public static class LevelBuilder
    {
        /// Courses are authored with +Z running down-course; yawing the whole
        /// thing 45 degrees lines course space up with the isometric camera.
        public const float CourseYaw = 45f;

        public static BuiltLevel Build(Level lvl)
        {
            var built = new BuiltLevel();
            var root = new GameObject("Course_" + lvl.Name);
            root.transform.rotation = Quaternion.Euler(0f, CourseYaw, 0f);
            built.Root = root;

            float lowest = float.MaxValue;
            int normalIndex = 0;

            foreach (var blk in lvl.Blocks)
            {
                BuildSlab(blk, root.transform, ref normalIndex, built);
                lowest = Mathf.Min(lowest, blk.Center.y - blk.Size.y);
            }

            foreach (var rib in lvl.Ribbons)
            {
                BuildRibbon(rib, root.transform, ref normalIndex);
                foreach (var n in rib.Nodes) lowest = Mathf.Min(lowest, n.P.y - rib.Thickness * 2f);
            }

            foreach (var e in lvl.Enemies)
                built.Enemies.Add(SpawnEnemy(e, root.transform));

            BuildPath(lvl, root.transform, built);
            Decor.Scatter(lvl, built, root.transform);

            built.SpawnWorld = root.transform.TransformPoint(lvl.Spawn);
            built.KillY = lowest - 14f;
            return built;
        }

        // ---- chunky slabs ------------------------------------------------

        static void BuildSlab(Block blk, Transform root, ref int normalIndex, BuiltLevel built)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = blk.Surface.ToString();
            go.transform.SetParent(root, false);
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
                built.GoalWorld = root.TransformPoint(blk.Center + Vector3.up * 1.5f);
                AddGoalTrigger(root, blk);
            }
        }

        // ---- swept ribbons -----------------------------------------------

        /// Builds the mesh for a curve or eased ramp. Top, bottom and the two
        /// sides are separate vertex groups, so RecalculateNormals smooths along
        /// the sweep (curves read as curves) while keeping the edges crisp - which
        /// is what lets these sit next to the chunky slabs without clashing.
        static void BuildRibbon(Ribbon rib, Transform root, ref int normalIndex)
        {
            int n = rib.Nodes.Count;
            if (n < 2) return;

            var topL = new Vector3[n]; var topR = new Vector3[n];
            var botL = new Vector3[n]; var botR = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                var node = rib.Nodes[i];
                Vector3 right = node.Rot * Vector3.right;
                Vector3 up = node.Rot * Vector3.up;
                float h = node.Width * 0.5f;

                topL[i] = node.P - right * h;
                topR[i] = node.P + right * h;
                botL[i] = topL[i] - up * rib.Thickness;
                botR[i] = topR[i] - up * rib.Thickness;
            }

            var verts = new List<Vector3>();
            var tris = new List<int>();

            Strip(verts, tris, topL, topR);   // top   (+up)
            Strip(verts, tris, botR, botL);   // bottom
            Strip(verts, tris, botL, topL);   // left side
            Strip(verts, tris, topR, botR);   // right side
            Cap(verts, tris, topL[0], topR[0], botL[0], botR[0], false);
            Cap(verts, tris, topL[n - 1], topR[n - 1], botL[n - 1], botR[n - 1], true);

            var mesh = new Mesh { name = "Ribbon_" + rib.Surface };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Ribbon_" + rib.Surface);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            string matName = MatLib.VisualName(rib.Surface);
            if (rib.Surface == Surface.Normal && (normalIndex++ & 1) == 1) matName = "DeckAlt";
            go.AddComponent<MeshRenderer>().sharedMaterial = MatLib.Get(matName);

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.sharedMaterial = MatLib.Physics(rib.Surface);
        }

        /// Quads between two edge curves. Winding assumes travel along +Z with
        /// `a` on the left, which gives outward-facing normals.
        static void Strip(List<Vector3> verts, List<int> tris, Vector3[] a, Vector3[] b)
        {
            int start = verts.Count;
            for (int i = 0; i < a.Length; i++) { verts.Add(a[i]); verts.Add(b[i]); }

            for (int i = 0; i < a.Length - 1; i++)
            {
                int i0 = start + i * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
        }

        static void Cap(List<Vector3> verts, List<int> tris,
                        Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br, bool flip)
        {
            int s = verts.Count;
            verts.Add(tl); verts.Add(tr); verts.Add(bl); verts.Add(br);
            if (!flip)
            {
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                tris.Add(s + 1); tris.Add(s + 3); tris.Add(s + 2);
            }
            else
            {
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                tris.Add(s + 1); tris.Add(s + 2); tris.Add(s + 3);
            }
        }

        // ---- centreline ---------------------------------------------------

        static void BuildPath(Level lvl, Transform root, BuiltLevel built)
        {
            // Densify: with only segment endpoints, anything following the route
            // cuts corners and clips the edge of narrow decks.
            var dense = new List<Vector3>();
            var pts = lvl.Path;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0)
                {
                    Vector3 a = pts[i - 1], b = pts[i];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2f));
                    for (int k = 1; k < steps; k++)
                        dense.Add(Vector3.Lerp(a, b, (float)k / steps));
                }
                dense.Add(pts[i]);
            }

            foreach (var p in dense)
                built.PathWorld.Add(root.TransformPoint(p + Vector3.up * 0.5f));

            // Colliders exist but the physics scene has not stepped yet.
            Physics.SyncTransforms();

            int supported = 0, hazardous = 0;
            foreach (var w in built.PathWorld)
            {
                // deck underneath?
                bool hit = Physics.Raycast(w + Vector3.up * 1.0f, Vector3.down, 4f,
                                           ~0, QueryTriggerInteraction.Ignore);

                // ...and is it somewhere it is safe to be put down? Respawning
                // inside an acid pond kills instantly, which respawns you in the
                // same place: a loop that eats the whole clock.
                bool lethal = hit && InsideHazard(w);
                if (lethal) hazardous++;

                built.PathSupported.Add(hit && !lethal);
                if (hit && !lethal) supported++;
            }

            // cumulative distance, normalised, for the HUD progress bar
            float total = 0f;
            var run = new List<float> { 0f };
            for (int i = 1; i < built.PathWorld.Count; i++)
            {
                total += Vector3.Distance(built.PathWorld[i - 1], built.PathWorld[i]);
                run.Add(total);
            }
            foreach (var d in run) built.PathProgress.Add(total > 0.01f ? d / total : 0f);

            Debug.Log($"[path] {built.PathWorld.Count} waypoints, {supported} safe to respawn on " +
                      $"({hazardous} rejected as hazardous), length {total:0}m");
        }

        /// Is this point inside something that would kill the marble on contact?
        static bool InsideHazard(Vector3 world)
        {
            var hits = Physics.OverlapSphere(world, 0.8f, ~0, QueryTriggerInteraction.Collide);
            foreach (var c in hits)
                if (c.GetComponent<AcidZone>() != null) return true;
            return false;
        }

        // ---- props --------------------------------------------------------

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
            go.GetComponent<MeshRenderer>().sharedMaterial = MatLib.Get(lethal ? "Blob" : "Steel");

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
