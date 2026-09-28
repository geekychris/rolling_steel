using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    /// How a deck surface behaves underfoot (and which material it gets).
    public enum Surface { Normal, Rough, Ice, Acid, Goal, Start, Rail }

    public enum EnemyKind { Chaser, Wanderer }

    /// A chunky axis-aligned-ish slab. Still the backbone of the courses.
    public struct Block
    {
        public Vector3 Center;      // course space
        public Vector3 Size;
        public Quaternion Rot;
        public Surface Surface;
    }

    /// One cross-section of a swept track piece.
    public struct RibbonNode
    {
        public Vector3 P;           // centre of the top surface
        public Quaternion Rot;      // local forward = travel, local up = surface normal
        public float Width;
    }

    /// A swept track piece: curves and eased ramps, meshed rather than boxed.
    public class Ribbon
    {
        public readonly List<RibbonNode> Nodes = new List<RibbonNode>();
        public Surface Surface;
        public float Thickness;
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
        public int DecorTheme;      // which set of floating scenery to scatter
        public int MusicTheme;      // which synthesised track to play

        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<Ribbon> Ribbons = new List<Ribbon>();
        public readonly List<EnemySpec> Enemies = new List<EnemySpec>();

        /// Centreline of the route, in course space and in travel order. Used by
        /// respawn and by the demo driver.
        public readonly List<Vector3> Path = new List<Vector3>();
    }

    /// Cursor-based course builder. The cursor sits at the centre of the leading
    /// edge of the deck, level with its top surface, and carries a heading, so
    /// every piece joins the previous one and curves can turn the course.
    ///
    /// Local frame: +Z is down-course, +X is to the cursor's right, -Y is down.
    /// LevelBuilder yaws the whole course 45 degrees for the isometric look.
    public class CourseBuilder
    {
        public const float Thickness = 1.2f;
        public const float MarbleRadius = 0.5f;
        /// Pieces are grown very slightly at each end so they bury themselves in
        /// their neighbours. Two pieces that merely touch leave a visible hairline
        /// where the coincident end faces fight.
        const float Overlap = 0.06f;

        public readonly Level Level = new Level();

        Vector3 cur;
        float heading;              // degrees; 0 = +Z
        float width;

        // start of the piece currently being decorated, so Acid() can splice its
        // detour into the centreline in travel order rather than call order
        Vector3 segStart;
        int segStartIdx;

        // the most recently built piece, so Rails() can kerb whichever it was
        Vector3 lastA, lastB;
        float lastW;
        Quaternion lastRot = Quaternion.identity;
        Ribbon lastRibbon;
        bool lastIsRibbon;

        public Vector3 Cursor => cur;
        public float Width => width;
        public float Heading => heading;

        Quaternion Frame => Quaternion.AngleAxis(heading, Vector3.up);
        Vector3 Fwd => Frame * Vector3.forward;
        Vector3 Right => Frame * Vector3.right;

        public CourseBuilder(string name, float timeBonus, float startWidth)
        {
            Level.Name = name;
            Level.TimeBonus = timeBonus;
            width = startWidth;
            cur = Vector3.zero;
            Level.Spawn = new Vector3(0f, MarbleRadius + 0.4f, 2.5f);
            Level.Path.Add(cur);
        }

        public CourseBuilder Theme(int decor, int music)
        {
            Level.DecorTheme = decor;
            Level.MusicTheme = music;
            return this;
        }

        // ---- slabs -------------------------------------------------------

        void Slab(Vector3 a, Vector3 b, float w, Surface s)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.001f) return;

            Quaternion rot = Quaternion.LookRotation(dir / len, Vector3.up);
            Vector3 up = rot * Vector3.up;

            // a and b sit on the *top* surface, so drop the box by half its depth
            Level.Blocks.Add(new Block
            {
                Center = (a + b) * 0.5f - up * (Thickness * 0.5f),
                Size = new Vector3(w, Thickness, len + 2f * Overlap),
                Rot = rot,
                Surface = s,
            });

            lastA = a; lastB = b; lastW = w; lastRot = rot;
            lastIsRibbon = false;
        }

        /// Build a slab from the cursor along `localDelta` (cursor frame) and
        /// move the cursor there.
        public CourseBuilder Segment(Vector3 localDelta, float w, Surface s = Surface.Normal)
        {
            Vector3 delta = Frame * localDelta;
            segStart = cur;
            segStartIdx = Level.Path.Count - 1;

            // A width change on a straight is just as visible as one on a curve.
            // Sweep it instead of boxing it - but only for running surfaces: the
            // start and goal pads must stay slabs, because the goal trigger is
            // attached to the block.
            bool taperable = s == Surface.Normal || s == Surface.Ice || s == Surface.Rough;
            if (taperable && Mathf.Abs(w - width) > 0.01f)
            {
                int steps = Mathf.Max(4, Mathf.CeilToInt(delta.magnitude / 2f));
                var pts = new List<Vector3>(steps + 1);
                var rolls = new List<float>(steps + 1);
                var ws = new List<float>(steps + 1);
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    pts.Add(cur + delta * t);
                    rolls.Add(0f);
                    ws.Add(Mathf.Lerp(width, w, Ease(t)));
                }
                Sweep(pts, rolls, ws, s, Thickness);
            }
            else
            {
                Slab(cur, cur + delta, w, s);
            }

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

        /// Straight descending ramp with a constant incline (the chunky look).
        public CourseBuilder Slope(float len, float drop, float w = -1f)
            => Segment(new Vector3(0f, -drop, len), w < 0f ? width : w);

        /// Diagonal shuffle sideways while continuing down-course.
        public CourseBuilder Jog(float dx, float depth, float w = -1f)
            => Segment(new Vector3(dx, 0f, depth), w < 0f ? width : w);

        /// Open pit: advance without laying any deck.
        public CourseBuilder Gap(float len)
        {
            cur += Fwd * len;
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

        // ---- swept pieces ------------------------------------------------

        static float Ease(float t) => t * t * (3f - 2f * t);

        /// Build a swept ribbon through `pts`, rolled by `rolls` (degrees), and
        /// take the frame from the actual tangent so the deck stays perpendicular
        /// to the direction of travel however steep it gets.
        Ribbon Sweep(List<Vector3> pts, List<float> rolls, List<float> widths, Surface s, float thickness)
        {
            // extend a touch past each end, so the caps sit inside the neighbours
            int last = pts.Count - 1;
            Vector3 head = (pts[0] - pts[1]).normalized * Overlap;
            Vector3 tail = (pts[last] - pts[last - 1]).normalized * Overlap;
            pts.Insert(0, pts[0] + head); rolls.Insert(0, rolls[0]); widths.Insert(0, widths[0]);
            pts.Add(pts[pts.Count - 1] + tail); rolls.Add(rolls[rolls.Count - 1]); widths.Add(widths[widths.Count - 1]);

            var rib = new Ribbon { Surface = s, Thickness = thickness };

            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 prev = pts[Mathf.Max(0, i - 1)];
                Vector3 next = pts[Mathf.Min(pts.Count - 1, i + 1)];
                Vector3 tangent = next - prev;
                if (tangent.sqrMagnitude < 1e-6f) tangent = Fwd;

                Quaternion frame = Quaternion.LookRotation(tangent.normalized, Vector3.up)
                                 * Quaternion.AngleAxis(rolls[i], Vector3.forward);

                rib.Nodes.Add(new RibbonNode { P = pts[i], Rot = frame, Width = widths[i] });
            }

            Level.Ribbons.Add(rib);
            lastRibbon = rib;
            lastIsRibbon = true;
            return rib;
        }

        /// Raised lips down both edges of a ribbon.
        void RibbonKerbs(Ribbon rib, float h, bool left = true, bool right = true)
        {
            const float kw = 0.4f;
            for (int side = -1; side <= 1; side += 2)
            {
                if (side < 0 ? !left : !right) continue;
                var kerb = new Ribbon { Surface = Surface.Rail, Thickness = h };
                foreach (var n in rib.Nodes)
                {
                    kerb.Nodes.Add(new RibbonNode
                    {
                        P = n.P + n.Rot * new Vector3(side * (n.Width + kw) * 0.5f, h, 0f),
                        Rot = n.Rot,
                        Width = kw,
                    });
                }
                Level.Ribbons.Add(kerb);
            }
        }

        /// A banked horizontal curve. Positive angle turns right, negative left.
        /// The bank eases in and out, so the ends still meet flat deck cleanly.
        public CourseBuilder Curve(float radius, float angleDeg, float drop = 0f,
                                   float w = -1f, float bank = 0f, bool rails = false,
                                   Surface s = Surface.Normal)
        {
            float wid = w < 0f ? width : w;
            float w0 = width;
            float sign = Mathf.Sign(angleDeg);
            float sweep = Mathf.Abs(angleDeg);
            if (sweep < 0.01f || radius <= 0.01f) return this;

            Vector3 centre = cur + Right * (radius * sign);
            Vector3 radial = cur - centre;
            radial.y = 0f;

            int steps = Mathf.Max(8, Mathf.CeilToInt(sweep / 4f));
            var pts = new List<Vector3>(steps + 1);
            var rolls = new List<float>(steps + 1);
            var ws = new List<float>(steps + 1);

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 p = centre + Quaternion.AngleAxis(sweep * t * sign, Vector3.up) * radial;
                p.y = cur.y - drop * Ease(t);
                pts.Add(p);

                // ramp the bank in and out so the joins stay level
                rolls.Add(-bank * sign * Mathf.Sin(t * Mathf.PI));

                // ease the width across too - a step at the first node is a
                // notch in the edge of the track exactly where two pieces meet
                ws.Add(Mathf.Lerp(w0, wid, Ease(t)));
            }

            var rib = Sweep(pts, rolls, ws, s, Thickness);
            if (rails) RibbonKerbs(rib, 0.7f);

            for (int i = 1; i < pts.Count; i++) Level.Path.Add(pts[i]);

            cur = pts[pts.Count - 1];
            heading += angleDeg;
            width = wid;
            return this;
        }

        /// A straight ramp whose incline varies: flat at the top, steepest in the
        /// middle, flat at the bottom. Reads much better than a constant slope
        /// where it meets level deck, and is far kinder to jump physics.
        public CourseBuilder Hill(float len, float drop, float w = -1f,
                                  bool rails = false, Surface s = Surface.Normal)
        {
            float wid = w < 0f ? width : w;
            float w0 = width;
            int steps = Mathf.Max(8, Mathf.CeilToInt(len / 1.5f));

            var pts = new List<Vector3>(steps + 1);
            var rolls = new List<float>(steps + 1);
            var ws = new List<float>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 p = cur + Fwd * (len * t);
                p.y = cur.y - drop * Ease(t);
                pts.Add(p);
                rolls.Add(0f);
                ws.Add(Mathf.Lerp(w0, wid, Ease(t)));
            }

            var rib = Sweep(pts, rolls, ws, s, Thickness);
            if (rails) RibbonKerbs(rib, 0.7f);

            for (int i = 1; i < pts.Count; i++) Level.Path.Add(pts[i]);

            cur = pts[pts.Count - 1];
            width = wid;
            return this;
        }

        /// An S-bend: two opposed curves, ending on the original heading.
        public CourseBuilder Chicane(float radius, float angleDeg, float drop = 0f,
                                     float w = -1f, float bank = 0f, bool rails = false)
        {
            Curve(radius, angleDeg, drop * 0.5f, w, bank, rails);
            Curve(radius, -angleDeg, drop * 0.5f, w, bank, rails);
            return this;
        }

        // ---- structure ---------------------------------------------------

        /// Two parallel decks with a pit down the middle.
        ///
        /// Lays a solid apron first. The route has to move sideways onto one of
        /// the catwalks, and that move has to happen over deck. If the preceding
        /// segment is a diagonal jog it may not reach that far across, which puts
        /// the centreline - and therefore respawns - out over the void.
        public CourseBuilder Split(float len, float sideWidth, float gapWidth, float apron = 5f)
        {
            float full = gapWidth + 2f * sideWidth;
            Pad(apron, full);

            float off = (gapWidth + sideWidth) * 0.5f;
            Vector3 a = cur, b = cur + Fwd * len;
            Slab(a - Right * off, b - Right * off, sideWidth, Surface.Normal);
            Slab(a + Right * off, b + Right * off, sideWidth, Surface.Normal);

            cur = b;
            width = full;
            lastA = a; lastB = b; lastW = full; lastRot = Frame;

            // The apron's own centre point sits at the pit mouth. Replace it with a
            // drift across the apron, then a run down the left catwalk.
            Level.Path.RemoveAt(Level.Path.Count - 1);
            Level.Path.Add(a - Right * off - Fwd * (apron * 0.55f));
            Level.Path.Add(a - Right * off);
            Level.Path.Add(b - Right * off);
            return this;
        }

        /// Low kerbs along the edges of the slab just built.
        public CourseBuilder Rails(bool left = true, bool right = true, float h = 0.7f)
        {
            if (lastIsRibbon && lastRibbon != null)
            {
                RibbonKerbs(lastRibbon, h, left, right);
                return this;
            }

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

        /// Splice a detour point into the current segment, ordered by how far
        /// along that segment it sits. Two hazards on one run are declared in
        /// whatever order reads best, but the route has to visit them in the
        /// order the marble will actually meet them.
        void InsertAlong(Vector3 p)
        {
            Vector3 fwd = Fwd;
            float key = Vector3.Dot(p - segStart, fwd);
            int i = segStartIdx + 1;
            while (i < Level.Path.Count - 1 && Vector3.Dot(Level.Path[i] - segStart, fwd) <= key) i++;
            Level.Path.Insert(i, p);
        }

        /// Acid patch laid on top of the flat piece just built. The patch sits on
        /// the route on purpose - dodging it is the point - so the centreline gets
        /// a detour spliced in down whichever side of the deck has room.
        public CourseBuilder Acid(float w, float depth, float lateral = 0f, float back = 0f,
                                  float leadIn = 4f)
        {
            float backZ = depth * 0.5f + back;
            Vector3 c = cur + Frame * new Vector3(lateral, 0.06f, -backZ);

            Level.Blocks.Add(new Block
            {
                Center = c,
                Size = new Vector3(w, 0.12f, depth),
                Rot = Frame,
                Surface = Surface.Acid,
            });

            float deckL = -width * 0.5f, deckR = width * 0.5f;
            float acidL = lateral - w * 0.5f, acidR = lateral + w * 0.5f;
            float leftGap = acidL - deckL, rightGap = deckR - acidR;
            float bypass = leftGap > rightGap ? deckL + leftGap * 0.5f : deckR - rightGap * 0.5f;

            // The detour has to start far enough back that the marble can
            // actually get across before it reaches the patch.
            InsertAlong(cur + Frame * new Vector3(bypass, 0f, -(backZ + depth * 0.5f + leadIn)));
            InsertAlong(cur + Frame * new Vector3(bypass, 0f, -(backZ - depth * 0.5f - 1.5f)));
            return this;
        }

        public CourseBuilder Enemy(EnemyKind kind, float lateral, float back, float range = 16f, float speed = 9f)
        {
            Level.Enemies.Add(new EnemySpec
            {
                Kind = kind,
                Pos = cur + Frame * new Vector3(lateral, MarbleRadius + 0.3f, -back),
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
