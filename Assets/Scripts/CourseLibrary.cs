using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    /// The three hand-authored descents. Each is laid out with CourseBuilder in
    /// course space; LevelBuilder turns them into geometry.
    public static class CourseLibrary
    {
        public static List<Level> All()
        {
            return new List<Level> { Practice(), Beginner(), Intermediate() };
        }

        // ---- 1. Practice ------------------------------------------------
        // Wide, railed, one pit and one chaser. Teaches momentum.
        static Level Practice()
        {
            var b = new CourseBuilder("PRACTICE", 75f, 10f);

            b.Pad(10f, 10f, Surface.Start).Rails();
            b.Run(14f, 9f).Rails();
            b.Slope(16f, 4f, 8f).Rails();
            b.Run(10f, 7f).Rails(left: true, right: false);
            b.Jog(-6f, 12f, 6f);
            b.Run(8f, 10f);                         // wide enough to line up on either side

            b.Split(16f, 3f, 3.5f);                 // pit down the middle

            b.Run(9f, 8f).Rails();
            b.Slope(15f, 5f, 7f);
            b.Enemy(EnemyKind.Chaser, lateral: 0f, back: 4f, range: 18f, speed: 8f);
            b.Run(13f, 6f).Rails();
            b.Goal();

            return b.Done();
        }

        // ---- 2. Beginner ------------------------------------------------
        // Narrower, adds ice, acid and a jump. Two chasers.
        static Level Beginner()
        {
            var b = new CourseBuilder("BEGINNER", 70f, 9f);

            b.Pad(8f, 9f, Surface.Start).Rails();
            b.Slope(14f, 3f, 7f).Rails();

            b.Run(10f, 6f);
            b.Acid(w: 3.2f, depth: 3.2f, lateral: -1.2f, back: 3f);
            b.Enemy(EnemyKind.Wanderer, lateral: 2f, back: 1f, range: 999f, speed: 5f);

            b.Jog(7f, 13f, 5.5f);
            b.Ice(16f, 6f).Rails();                 // kerbed both sides; course 3's ice is not

            b.Slope(12f, 4f, 5f);
            b.Jump(2.6f, 2.4f);                     // flat lip, then carry speed across
            b.Run(9f, 7f).Rails();

            b.Enemy(EnemyKind.Chaser, lateral: -1.5f, back: 3f, range: 20f, speed: 9f);
            b.Jog(-8f, 14f, 9f);
            b.Split(14f, 2.8f, 4f);

            b.Run(8f, 7f);
            b.Acid(w: 2.6f, depth: 2.6f, lateral: 1.6f, back: 2.5f);
            b.Slope(14f, 5f, 6f);
            b.Enemy(EnemyKind.Chaser, lateral: 1f, back: 5f, range: 22f, speed: 10f);
            b.Run(11f, 6f).Rails();
            b.Goal();

            return b.Done();
        }

        // ---- 3. Intermediate --------------------------------------------
        // Long exposed bridges, a steep ice drop, three enemies. Unforgiving.
        static Level Intermediate()
        {
            var b = new CourseBuilder("INTERMEDIATE", 65f, 8f);

            b.Pad(7f, 8f, Surface.Start).Rails();
            b.Slope(12f, 3f, 6f);

            b.Run(16f, 4.2f);                       // first exposed bridge, no kerbs
            b.Enemy(EnemyKind.Wanderer, lateral: 0f, back: 6f, range: 999f, speed: 6f);

            b.Jog(9f, 12f, 4f);
            b.Run(7f, 9f);
            b.Acid(w: 4.5f, depth: 3f, lateral: 0f, back: 2f);

            b.Ice(18f, 5f);                         // ice with nothing to stop you
            b.Slope(10f, 5f, 4.5f);
            b.Jump(3f, 3f);

            b.Run(8f, 7f).Rails();
            b.Enemy(EnemyKind.Chaser, lateral: 0f, back: 3f, range: 24f, speed: 11f);

            b.Jog(-11f, 15f, 4.4f);                 // long diagonal catwalk
            b.Run(6f, 13f);
            b.Split(15f, 3.6f, 4.5f);               // twin catwalks over a drop

            b.Run(9f, 10f);                         // slalom: dodge right, then left
            b.Acid(w: 3.2f, depth: 3f, lateral: -3f, back: 2f);
            b.Acid(w: 3.2f, depth: 3f, lateral: 3f, back: 6f);

            b.Slope(16f, 7f, 5f);
            b.Enemy(EnemyKind.Chaser, lateral: -1f, back: 6f, range: 26f, speed: 12f);
            b.Rough(10f, 5f).Rails();               // sand: kills your speed at the worst time
            b.Goal();

            return b.Done();
        }
    }
}
