using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    /// How a deck slab behaves underfoot (and which material it gets).
    public enum Surface { Normal, Rough, Ice, Acid, Goal, Start, Rail }

    public enum EnemyKind { Chaser, Wanderer }

    public struct Block
    {
        public Vector3 Center;      // course space
        public Vector3 Size;
        public Quaternion Rot;
        public Surface Surface;
    }

    public struct EnemySpec
    {
        public EnemyKind Kind;
        public Vector3 Pos;         // course space, centre of the sphere
        public float Range;         // how close the marble must be to wake it
        public float Speed;
    }

    public class Level
    {
        public string Name;
        public float TimeBonus;     // seconds added to the clock on entry
        public Vector3 Spawn;       // course space
        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<EnemySpec> Enemies = new List<EnemySpec>();
        /// Centreline of the route, in course space. Used by the demo driver and
        /// handy for anything else that needs to know where the course actually goes.
        public readonly List<Vector3> Path = new List<Vector3>();
    }

    /// Cursor-based course builder. The cursor sits at the centre of the leading
    /// edge of the deck, level with the deck's top surface, so every segment
    /// automatically joins the previous one.
    ///
    /// Course space: +Z runs away from the camera (down-course), +X is screen
    /// right, -Y is down. LevelBuilder yaws the whole course 45 degrees to get
    /// the classic isometric presentation.
    public class CourseBuilder
    {
        public const float Thickness = 1.2f;
        public const float MarbleRadius = 0.5f;

        public readonly Level Level = new Level();

        Vector3 cur;
        float width;

        // extent of the most recently built slab, so Rails() can hug it
        Vector3 lastA, lastB;
        float lastW;
        Quaternion lastRot = Quaternion.identity;

        public Vector3 Cursor => cur;
        public float Width => width;

        public CourseBuilder(string name, float timeBonus, float startWidth)
        {
            Level.Name = name;
            Level.TimeBonus = timeBonus;
            width = startWidth;
            cur = Vector3.zero;
            Level.Spawn = new Vector3(0f, MarbleRadius + 0.4f, 2.5f);
            Level.Path.Add(cur);
        }

        void Slab(Vector3 a, Vector3 b, float w, Surface s)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.001f) return;

            Quaternion rot = Quaternion.LookRotation(dir / len, Vector3.up);
            Vector3 up = rot * Vector3.up;

            // a and b are points on the *top* surface, so drop the box by half its depth
            Level.Blocks.Add(new Block
            {
                Center = (a + b) * 0.5f - up * (Thickness * 0.5f),
                Size = new Vector3(w, Thickness, len),
                Rot = rot,
                Surface = s,
            });

            lastA = a; lastB = b; lastW = w; lastRot = rot;
        }

        /// Build a slab from the cursor to cursor+delta and move the cursor there.
        public CourseBuilder Segment(Vector3 delta, float w, Surface s = Surface.Normal)
        {
            Slab(cur, cur + delta, w, s);
            cur += delta;
            width = w;
            Level.Path.Add(cur);
            return this;
        }

        public CourseBuilder Pad(float depth, float w, Surface s = Surface.Normal)
            => Segment(new Vector3(0f, 0f, depth), w, s);

        public CourseBuilder Run(float len, float w = -1f)
            => Segment(new Vector3(0f, 0f, len), w < 0f ? width : w);

        public CourseBuilder Ice(float len, float w = -1f)
            => Segment(new Vector3(0f, 0f, len), w < 0f ? width : w, Surface.Ice);

        public CourseBuilder Rough(float len, float w = -1f)
            => Segment(new Vector3(0f, 0f, len), w < 0f ? width : w, Surface.Rough);

        /// Descending ramp: travels `len` down-course while losing `drop` height.
        public CourseBuilder Slope(float len, float drop, float w = -1f)
            => Segment(new Vector3(0f, -drop, len), w < 0f ? width : w);

        /// Diagonal shuffle sideways while continuing down-course.
        public CourseBuilder Jog(float dx, float depth, float w = -1f)
            => Segment(new Vector3(dx, 0f, depth), w < 0f ? width : w);

        /// Open pit: advance the cursor without laying any deck.
        public CourseBuilder Gap(float len)
        {
            cur.z += len;
            Level.Path.Add(cur);
            return this;
        }

        /// Sheer step down with no connecting geometry.
        public CourseBuilder Step(float dy)
        {
            cur.y -= dy;
            Level.Path.Add(cur);
            return this;
        }

        /// A gap you have to carry speed across.
        ///
        /// Always lay a flat lip first: leaving straight off a downhill ramp
        /// gives the marble downward velocity at the lip, so it drops out of the
        /// air far sooner than the gap length suggests. With gravity at ~22 m/s2
        /// a 3.2m gap off an 18-degree ramp is unclearable even at top speed,
        /// which is exactly the trap this helper exists to prevent.
        public CourseBuilder Jump(float gapLen, float drop, float lip = 5f)
        {
            Run(lip);
            Gap(gapLen);
            Step(drop);
            return this;
        }

        /// Two parallel decks with a pit down the middle.
        public CourseBuilder Split(float len, float sideWidth, float gapWidth)
        {
            float off = (gapWidth + sideWidth) * 0.5f;
            Vector3 a = cur, b = cur + new Vector3(0f, 0f, len);
            Slab(a + Vector3.left * off, b + Vector3.left * off, sideWidth, Surface.Normal);
            Slab(a + Vector3.right * off, b + Vector3.right * off, sideWidth, Surface.Normal);

            cur = b;
            width = gapWidth + 2f * sideWidth;
            lastA = a; lastB = b; lastW = width; lastRot = Quaternion.identity;
            // Commit to the left deck *before* the pit opens up - stepping sideways
            // at the split entry gives the marble no room to drift across.
            // Drop the previous segment's centre point first: it sits at the split
            // entry, which is directly over the pit.
            int last = Level.Path.Count - 1;
            if (last >= 0 && Mathf.Abs(Level.Path[last].z - a.z) < 0.001f)
                Level.Path.RemoveAt(last);

            InsertPath(new Vector3(a.x - off, a.y, a.z - 4.5f));
            InsertPath(a + Vector3.left * off);
            InsertPath(b + Vector3.left * off);
            return this;
        }

        /// Low kerbs along the edges of the segment just built.
        public CourseBuilder Rails(bool left = true, bool right = true, float h = 0.7f)
        {
            const float rw = 0.35f;
            Vector3 mid = (lastA + lastB) * 0.5f;
            float len = (lastB - lastA).magnitude;
            Vector3 up = lastRot * Vector3.up;
            Vector3 side = lastRot * Vector3.right;

            for (int i = 0; i < 2; i++)
            {
                bool isLeft = i == 0;
                if (isLeft ? !left : !right) continue;
                float sgn = isLeft ? -1f : 1f;
                Level.Blocks.Add(new Block
                {
                    Center = mid + side * (sgn * (lastW + rw) * 0.5f) + up * (h * 0.5f),
                    Size = new Vector3(rw, h, len),
                    Rot = lastRot,
                    Surface = Surface.Rail,
                });
            }
            return this;
        }

        /// Keep the centreline sorted by z. The course only ever advances in z,
        /// so this is enough to splice a detour into the right place.
        void InsertPath(Vector3 p)
        {
            int i = Level.Path.Count;
            while (i > 0 && Level.Path[i - 1].z > p.z) i--;
            Level.Path.Insert(i, p);
        }

        /// Acid patch laid on top of the flat segment just built. The patch sits
        /// on the route on purpose - dodging it is the point - so the centreline
        /// gets a detour spliced in down whichever side of the deck has room.
        public CourseBuilder Acid(float w, float depth, float lateral = 0f, float back = 0f)
        {
            float zc = cur.z - (depth * 0.5f + back);
            Vector3 c = new Vector3(cur.x + lateral, cur.y + 0.06f, zc);

            Level.Blocks.Add(new Block
            {
                Center = c,
                Size = new Vector3(w, 0.12f, depth),
                Rot = Quaternion.identity,
                Surface = Surface.Acid,
            });

            float deckL = cur.x - width * 0.5f, deckR = cur.x + width * 0.5f;
            float acidL = c.x - w * 0.5f, acidR = c.x + w * 0.5f;
            float leftGap = acidL - deckL, rightGap = deckR - acidR;

            float bypassX = leftGap > rightGap
                ? deckL + leftGap * 0.5f
                : deckR - rightGap * 0.5f;

            InsertPath(new Vector3(bypassX, cur.y, zc - depth * 0.5f - 1.5f));
            InsertPath(new Vector3(bypassX, cur.y, zc + depth * 0.5f + 1.5f));
            return this;
        }

        public CourseBuilder Enemy(EnemyKind kind, float lateral, float back, float range = 16f, float speed = 9f)
        {
            Level.Enemies.Add(new EnemySpec
            {
                Kind = kind,
                Pos = cur + new Vector3(lateral, MarbleRadius + 0.3f, -back),
                Range = range,
                Speed = speed,
            });
            return this;
        }

        /// Final pad. Everything past here is scenery.
        public CourseBuilder Goal(float depth = 9f, float w = -1f)
        {
            Pad(depth, w < 0f ? Mathf.Max(width, 9f) : w, Surface.Goal);
            return this;
        }

        public Level Done() => Level;
    }
}
