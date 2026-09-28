using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    /// The three hand-authored descents, laid out with CourseBuilder.
    ///
    /// The chunky slabs are still the backbone - they read well isometrically and
    /// make the drops legible. Curves and eased hills are used where the course
    /// should flow: banked turns, and ramps that ease in and out instead of
    /// meeting flat deck at a hard crease.
    ///
    /// Note that Curve() and Hill() take `rails:` rather than being followed by
    /// .Rails(), which only knows about the last *slab*.
    public static class CourseLibrary
    {
        public static List<Level> All()
        {
            return new List<Level> { Practice(), Beginner(), Intermediate() };
        }

        // ---- 1. Practice ------------------------------------------------
        // Wide, kerbed, gentle banked turns. Teaches momentum.
        static Level Practice()
        {
            var b = new CourseBuilder("PRACTICE", 75f, 10f).Theme(decor: 0, music: 1);

            b.Pad(10f, 10f, Surface.Start).Rails();
            b.Run(12f, 9f).Rails();
            b.Hill(16f, 4f, 8f, rails: true);                       // eased ramp

            b.Curve(15f, 60f, 1.5f, 7f, bank: 12f, rails: true);    // banked right
            b.Run(8f, 7f).Rails();
            b.Curve(13f, -55f, 1.5f, 7f, bank: 11f, rails: true);   // and back left

            b.Run(7f, 8f);
            b.Split(15f, 3f, 3.5f);                                 // pit down the middle

            b.Run(9f, 8f).Rails();
            b.Hill(15f, 5f, 7f);
            b.Enemy(EnemyKind.Chaser, lateral: 0f, back: 4f, range: 18f, speed: 8f);
            b.Run(12f, 6f).Rails();
            b.Goal();

            return b.Done();
        }

        // ---- 2. Beginner ------------------------------------------------
        // Ice through a banked turn, acid, a jump. Two chasers.
        static Level Beginner()
        {
            var b = new CourseBuilder("BEGINNER", 70f, 9f).Theme(decor: 1, music: 2);

            b.Pad(8f, 9f, Surface.Start).Rails();
            b.Hill(14f, 3f, 7f, rails: true);

            b.Run(10f, 6f);
            b.Acid(w: 3.2f, depth: 3.2f, lateral: -1.2f, back: 3f);
            b.Enemy(EnemyKind.Wanderer, lateral: 2f, back: 1f, range: 999f, speed: 5f);

            b.Curve(14f, 70f, 2f, 6f, bank: 16f, rails: true);
            b.Ice(16f, 6f).Rails();                                 // kerbed; course 3's ice is not
            b.Curve(17f, -50f, 2f, 6f, bank: 14f, rails: true);

            b.Hill(12f, 4f, 5f);
            b.Jump(2.6f, 2.4f);                                     // flat lip, then carry speed
            b.Run(9f, 7f).Rails();

            b.Enemy(EnemyKind.Chaser, lateral: -1.5f, back: 3f, range: 20f, speed: 9f);
            b.Curve(12f, -45f, 1f, 6f, bank: 10f, rails: true);
            b.Split(14f, 2.8f, 4f);

            b.Run(8f, 7f);
            b.Acid(w: 2.6f, depth: 2.6f, lateral: 1.6f, back: 2.5f);
            b.Hill(14f, 5f, 6f);
            b.Enemy(EnemyKind.Chaser, lateral: 1f, back: 5f, range: 22f, speed: 10f);
            b.Curve(14f, 55f, 1.5f, 6f, bank: 12f, rails: true);
            b.Run(11f, 6f).Rails();
            b.Goal();

            return b.Done();
        }

        // ---- 3. Intermediate --------------------------------------------
        // Exposed bridges, unkerbed banked turns, an S-bend, three enemies.
        static Level Intermediate()
        {
            var b = new CourseBuilder("INTERMEDIATE", 65f, 8f).Theme(decor: 2, music: 3);

            b.Pad(7f, 8f, Surface.Start).Rails();
            b.Hill(12f, 3f, 6f);

            b.Run(16f, 4.2f);                                       // exposed bridge, no kerbs
            b.Enemy(EnemyKind.Wanderer, lateral: 0f, back: 6f, range: 999f, speed: 6f);

            b.Curve(13f, 65f, 2f, 5f, bank: 18f);                   // banked, and nothing to catch you
            b.Run(14f, 9f);
            b.Acid(w: 4.5f, depth: 3f, lateral: 0f, back: 2f);

            b.Chicane(15f, 40f, 3f, 6f, bank: 14f);                 // S-bend
            b.Ice(16f, 5f);                                         // ice with no kerbs
            b.Hill(10f, 5f, 4.5f);
            b.Jump(3f, 3f);

            b.Run(8f, 7f).Rails();
            b.Enemy(EnemyKind.Chaser, lateral: 0f, back: 3f, range: 24f, speed: 11f);

            b.Curve(11f, -75f, 2f, 4.6f, bank: 16f);                // tight and narrow
            b.Run(6f, 13f);
            b.Split(15f, 3.6f, 4.5f);                               // twin catwalks over a drop

            b.Run(18f, 11f);                                        // slalom: dodge right, then left
            b.Acid(w: 3.0f, depth: 3f, lateral: -3.4f, back: 11f);
            b.Acid(w: 3.0f, depth: 3f, lateral: 3.4f, back: 3f);

            b.Hill(16f, 7f, 5f);
            b.Enemy(EnemyKind.Chaser, lateral: -1f, back: 6f, range: 26f, speed: 12f);
            b.Rough(10f, 5f).Rails();                               // sand: kills your speed at the worst time
            b.Goal();

            return b.Done();
        }
    }
}
