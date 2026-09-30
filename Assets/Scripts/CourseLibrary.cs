using System.Collections.Generic;

namespace RollingSteel
{
    /// The built-in courses, as course-script source.
    ///
    /// They live as text rather than C# so that there is exactly one course
    /// format: what ships with the game is the same thing the in-game editor
    /// edits and writes back out. CourseStore copies these to disk on first run.
    public static class CourseLibrary
    {
        public static List<Level> All()
        {
            var levels = new List<Level>();
            foreach (var src in Sources) levels.Add(CourseScript.Build(src));
            return levels;
        }

        public static readonly string[] Sources =
            { Practice, Beginner, Intermediate, Aerial, Silly, Ultimate };

        // ---- 1 ------------------------------------------------------------
        public const string Practice = @"
name PRACTICE
medals gold=25 silver=35 bronze=50
time 75
width 10
decor 0
music 1

pad d=10 w=10 start
rails
run len=12 w=9
rails
hill len=16 drop=4 w=8 rails        # eased ramp, flat at both ends

curve r=15 a=60 drop=1.5 w=7 bank=12 rails
run len=11 w=8
pillar x=0 back=4 r=0.9 h=2.6       # first thing that is simply in the way
rails
curve r=13 a=-55 drop=1.5 w=7 bank=11 rails

run len=7 w=8
split len=15 side=3 pit=3.5         # pit down the middle

run len=9 w=8
rails
hill len=15 drop=5 w=7
boost x=0 back=9 w=4 d=5 push=24     # faster line, less say in what happens next
chaser x=0 back=4 range=18 speed=8
run len=12 w=6
rails
goal d=9
";

        // ---- 2 ------------------------------------------------------------
        public const string Beginner = @"
name BEGINNER
medals gold=36 silver=48 bronze=65
time 70
width 9
decor 1
music 2

pad d=8 w=9 start
rails
hill len=14 drop=3 w=7 rails

run len=10 w=6
acid w=3.2 d=3.2 x=-1.2 back=3
blob x=2 back=1 range=999 speed=5

curve r=14 a=70 drop=2 w=6 bank=16 rails
ice len=16 w=6
rails
curve r=17 a=-50 drop=2 w=6 bank=14 rails

run len=19 w=8
fan x=0 back=14 w=8 d=5 push=13     # shoves you sideways while you cross it
sweeper x=4 back=6 len=6 speed=80   # pivoted at the edge: no wide line here
hill len=12 drop=4 w=5
jump gap=2.6 drop=2.4               # flat lip, then carry speed
run len=9 w=7
rails

chaser x=-1.5 back=3 range=20 speed=9
curve r=12 a=-45 drop=1 w=6 bank=10 rails
split len=14 side=2.8 pit=4

run len=8 w=7
acid w=2.6 d=2.6 x=1.6 back=2.5
crumble len=7 w=7                   # keep moving
hill len=14 drop=5 w=6
chaser x=1 back=5 range=22 speed=10
curve r=14 a=55 drop=1.5 w=6 bank=12 rails
run len=11 w=6
rails
goal d=9
";

        // ---- 3 ------------------------------------------------------------
        public const string Intermediate = @"
name INTERMEDIATE
medals gold=42 silver=52 bronze=63
time 65
width 8
decor 2
music 3

pad d=7 w=8 start
rails
hill len=12 drop=3 w=6

run len=16 w=4.2                    # exposed bridge, no kerbs
blob x=0 back=6 range=999 speed=6

curve r=13 a=65 drop=2 w=5 bank=18  # banked, and nothing to catch you
run len=14 w=9
acid w=4.5 d=3 x=0 back=2

chicane r=15 a=40 drop=3 w=6 bank=14
ice len=16 w=5                      # ice with no kerbs
hill len=10 drop=5 w=4.5
jump gap=3 drop=3

run len=13 w=8
rails
crusher x=0 back=5 w=3 period=2.2 lift=4.5
chaser x=0 back=2 range=24 speed=11

curve r=11 a=-75 drop=2 w=4.6 bank=16
run len=6 w=13
split len=15 side=3.6 pit=4.5       # twin catwalks over a drop

run len=18 w=11                     # slalom: dodge right, then left
acid w=3 d=3 x=-3.4 back=11
acid w=3 d=3 x=3.4 back=3

run len=12 w=9
pillar x=-2.6 back=8 r=0.8 h=2.4
pillar x=2.6 back=3 r=0.8 h=2.4
crumble len=8 w=9

hill len=16 drop=7 w=5
chaser x=-1 back=6 range=26 speed=12
rough len=10 w=5                    # sand: kills your speed at the worst time
rails
goal d=9
";

        // ---- 4 ------------------------------------------------------------
        // Thin air. Long exposed spans, three jumps, and very little kerbing.
        public const string Aerial = @"
name AERIAL
medals gold=38 silver=47 bronze=58
time 60
width 8
decor 3
music 4

pad d=7 w=8 start
rails
hill len=12 drop=3 w=6

run len=18 w=4.4                    # first span, nothing either side
pillar x=0 back=6 r=0.7 h=2.2
curve r=14 a=55 drop=2 w=5 bank=16

run len=10 w=7
jump gap=2.8 drop=2.6
run len=9 w=6
rails
blob x=0 back=4 range=999 speed=6

curve r=12 a=-60 drop=2 w=5 bank=15
run len=16 w=4.6                    # second span
boost x=0 back=12 w=3.4 d=5 push=26
crumble len=7 w=5

run len=11 w=9
jump gap=3 drop=3
run len=10 w=8
rails
sweeper x=4.5 back=5 len=6 speed=95

chicane r=13 a=35 drop=3 w=6 bank=13
split len=14 side=3.2 pit=4
run len=9 w=8
chaser x=0 back=4 range=22 speed=10

hill len=15 drop=6 w=6
jump gap=2.8 drop=2.8
run len=11 w=7
rails
goal d=9
";

        // ---- 5 ------------------------------------------------------------
        // Everything moves. Crushers, fans and a sweeper, on generous deck.
        public const string Silly = @"
name SILLY
medals gold=34 silver=44 bronze=55
time 58
width 9
decor 4
music 5

pad d=7 w=9 start
rails
hill len=13 drop=3 w=8

run len=15 w=9
crusher x=0 back=5 w=3 period=1.9 lift=4.5
curve r=13 a=-65 drop=2 w=7 bank=15 rails

run len=17 w=9
fan x=0 back=12 w=9 d=5 push=17
fan x=0 back=4 w=9 d=5 push=-17     # and straight back the other way
run len=8 w=7
rails

ice len=14 w=7
rails
curve r=14 a=70 drop=2 w=7 bank=17 rails

run len=16 w=10
crusher x=-2.8 back=11 w=3 period=1.7 lift=4
crusher x=2.8 back=4 w=3 period=1.7 phase=0.5 lift=4
run len=9 w=8
rails

sweeper x=4.5 back=5 len=7 speed=110
crumble len=9 w=8
curve r=12 a=-55 drop=2.5 w=7 bank=14 rails

run len=12 w=9
acid w=3.4 d=3 x=-3 back=7
acid w=3.4 d=3 x=3 back=2
hill len=14 drop=5 w=7
chaser x=0 back=5 range=24 speed=11
run len=11 w=7
rails
goal d=9
";

        // ---- 6 ------------------------------------------------------------
        // All of it, with the clock at its meanest.
        public const string Ultimate = @"
name ULTIMATE
medals gold=40 silver=48 bronze=55
time 56
width 8
decor 5
music 6

pad d=6 w=8 start
rails
hill len=11 drop=3 w=6

run len=17 w=4.2                    # straight to the hard part
blob x=0 back=6 range=999 speed=7
curve r=11 a=70 drop=2.5 w=5 bank=18

run len=13 w=9
crusher x=0 back=5 w=3.2 period=1.6 lift=5
acid w=3 d=2.8 x=-3 back=1

ice len=16 w=5                      # unkerbed ice, straight into a jump
hill len=9 drop=4 w=4.6
jump gap=3 drop=3
run len=9 w=7
rails
chaser x=0 back=3 range=26 speed=12

curve r=10 a=-80 drop=2 w=4.6 bank=17
run len=14 w=10
fan x=0 back=9 w=10 d=6 push=19
pillar x=-3 back=3 r=0.8 h=2.4
pillar x=3 back=3 r=0.8 h=2.4

run len=7 w=13
split len=16 side=3.4 pit=5
run len=10 w=9
boost x=-3 back=6 w=3.4 d=5 push=28  # the quick way past the sweeper
sweeper x=5 back=5 len=7 speed=120

crumble len=10 w=9
chicane r=12 a=45 drop=3 w=6 bank=15
run len=13 w=10
acid w=3.2 d=3 x=-3.4 back=8
acid w=3.2 d=3 x=3.4 back=2
chaser x=0 back=6 range=28 speed=13

hill len=16 drop=7 w=5
rough len=9 w=5
rails
goal d=9
";
    }
}
