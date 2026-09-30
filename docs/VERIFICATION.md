# Verification

A physics game can compile cleanly, render beautifully, and still contain a gap
nobody can jump. The player therefore ships with enough command-line surface to
play and screenshot itself, and `make verify` turns that into a pass/fail check.

```bash
make verify
```

```
--- progression ---
[level] 1/3 PRACTICE clock=75.0
[state] LevelClear t=15.2 falls=0 clock=59.8
[level] 2/3 BEGINNER clock=129.8
[state] LevelClear t=36.6 falls=0 clock=110.6
[level] 3/3 INTERMEDIATE clock=175.6
[state] LevelClear t=66.0 falls=3 clock=142.2
[state] Won t=68.2 falls=3 clock=142.2
--- falls: 3 ---
PASS - all three courses cleared
```

It exits non-zero if the run never reaches `Won`, so it works as a CI gate.

**Run it more than once.** Unity's physics is not frame-rate deterministic, so a
marginal course can pass one run and fail the next. A bug that only showed up on
a fresh clone (below) had already passed twice on an identical tree. Treat a
single green run as weak evidence; four in a row is reasonable.

```bash
for i in 1 2 3 4; do make verify || break; done
```

## Player flags

| Flag | Effect |
|------|--------|
| `-autostart` | skip the title card |
| `-demo` | the game plays itself (implies `-autostart`) |
| `-shots DIR` | write in-engine screenshots to `DIR` on a fixed schedule |
| `-quitafter SECS` | exit after this many seconds |
| `-yaw DEG` | initial camera yaw |
| `-dumpmusic DIR` | render every theme to a WAV and exit |
| `-mute` | start with music off |
| `-killat SECS` | force a wipeout, for capturing the death effects |
| `-shotat T1,T2,…` | override the screenshot schedule |
| `-courses DIR` | load courses from DIR (seeding it if empty) |
| `-players N` | start with one or two players, skipping the title |
| `-dumpmusic DIR` | render every theme to a WAV and exit |

```bash
scripts/run.sh -demo -quitafter 260            # full self-played run
scripts/run.sh -demo -shots ./shots -quitafter 75
scripts/run.sh -autostart -yaw 45
```

Screenshots come from `ScreenCapture.CaptureScreenshot`, so they contain the
game's framebuffer only — no desktop, no window chrome, and no screen-recording
permission needed.

## How the demo driver works

`GameDirector.DemoInput()` steers along the course centreline
(see [Course design](COURSE-DESIGN.md#the-centreline)) with a velocity-matching
controller rather than full throttle:

```csharp
Vector3 err = toWaypoint.normalized * CruiseSpeed - currentVelocity;
Vector3 wish = err.normalized;
```

That makes it brake into turns and on ice, which is what a human does. Pushing
at full force instead just pins the marble to the outside of every corner.

The resulting world-space direction is then converted **back** into
camera-relative stick input:

```csharp
return new Vector2(Vector3.Dot(wish, camRight), Vector3.Dot(wish, camForward));
```

so the bot drives correctly at any view angle — including while the player is
rotating the camera underneath it.

## Checking the music

The soundtrack is synthesised, so it can be rendered and measured rather than
listened to:

```bash
scripts/run.sh -batchmode -nographics -dumpmusic ./shots/music
```

Three things are worth measuring, and each has caught something:

- **Peak** — must stay below 1.0 or the clip will clatter. Render normalises to
  0.82 and soft-clips above that.
- **Envelope percentiles** — the 10th percentile of short-window RMS should be
  comfortably above zero. A track that is loud on average can still be silent
  half the time.
- **Pitch content** — FFT the melodic band (above the kick, say 220-1600 Hz), map
  the strongest peaks to note names, and check they belong to the key. Currently
  40/40 for three themes and 39/40 for the fourth.

That middle check earned its place immediately. Every theme measured a healthy
overall RMS while its 10th percentile was exactly zero — because **every melodic
voice was silent and all that was playing was drums**. The note envelope ramps
from zero over a 4 ms attack, and the "this note has decayed, stop rendering"
test ran before the attack finished, so it broke out of the loop on the first
sample of every note. Peak and RMS both looked fine; only the percentile and a
printed envelope showed the holes.

## Verifying the courses that ship

`make verify` seeds a throwaway directory with `-courses` and runs against that,
rather than against the player's own course folder. Without it, an editing
session would quietly change what the completability check is checking — the
harness would be testing your edits and reporting on the shipped courses.

## Capturing something that rarely happens

The demo driver currently clears all three courses without falling, which is good
for the courses and useless for testing the death effects. `-killat` forces a
wipeout at a chosen moment and `-shotat` puts the screenshots where they are
wanted, so the sequence can be captured deterministically:

```bash
scripts/run.sh -demo -killat 6.0 -shotat 6.08,6.3,6.8,7.4 -shots ./shots/death -quitafter 11
```

Worth remembering that `make shots` clears the output directory first, so a
hand-captured sequence wants its own folder or it will be deleted by the next run.

## Log lines

The player writes structured lines to its log, which is what makes failures
diagnosable rather than merely visible:

```
[level]   1/3 PRACTICE clock=75.0
[state]   LevelClear t=15.2 falls=0 clock=59.8
[death]   FELL OFF course=3 z=122.8 x=-7.0 y=-34.0 lastGround(z=119.9 x=-7.5 y=-10.5)
```

Deaths are reported in **course space**, so a cluster of identical coordinates
points straight at the offending metre of level geometry.

## What this actually caught

Seven real bugs, none of which produced a compile error or a visual glitch:

**1. Two unjumpable gaps.** Both course 2 and course 3 opened a gap straight off
a downhill ramp. The marble left the lip already moving downward and fell short
*even at maximum speed* — 2.94 m of flight for a 3.2 m gap. Fixed by
`CourseBuilder.Jump()`, which forces a flat launch lip. Full numbers in
[Course design](COURSE-DESIGN.md#jumps).

**2. Acid sitting on the only route.** Twelve consecutive `DISSOLVED` deaths at
`z=25.8, x=0.0` — the acid patch was dead centre on the line, and respawning put
the player back on the same approach. `Acid()` now splices a detour around
itself into the centreline.

**3. A respawn death loop.** Respawn originally rewound the marble's own position
history by 1.1 s. Near a gap that reinstates it *in mid-air over the thing that
killed it*, so it dies again, respawns identically, and burns the whole clock at
one spot. The giveaway was deaths alternating between two fixed coordinates.
Respawn now snaps to the recorded course centreline, which is always solid deck.

**4. A centreline waypoint over the void.** `Split()` placed its "drift onto the
left catwalk" waypoint relative to the split's own centre, assuming the preceding
deck reached that far across. Where the approach was a diagonal `Jog` still moving
sideways, it did not — the waypoint sat 1.5 m off the edge, over nothing. Because
respawn uses the centreline, a fall there respawned the player *into the void*,
which is unrecoverable.

This one is worth dwelling on: it passed `make verify` twice on the exact tree
that later failed, and was only caught when the repo was cloned fresh and
verified again. The flakiness was the tell.

Two fixes went in. `Split()` now lays a solid apron before the pit so the
sideways move is always over deck; and `LevelBuilder` no longer *trusts* the
centreline at all — it tests every densified waypoint against the block geometry
and records whether there is deck beneath it. Respawn only uses supported
waypoints, so waypoints spanning a `Jump()` gap remain available for steering
without ever becoming a respawn point.

**5. Respawning inside an acid pond.** The "is this waypoint safe?" test raycasts
for deck and deliberately ignores triggers — acid is a trigger, so a waypoint
sitting on top of a pond still counted as solid ground. Falling near one put the
marble back *in* the acid, which killed it instantly, which respawned it in the
same place. The tell was `lastGround` being identical to the death position.
Respawn now also rejects waypoints inside a hazard volume; course 3 has two.

**6. Hazard detours spliced in the wrong order.** Two acid patches on one run are
dodged on opposite sides. `Acid()` inserted its detour relative to the end of the
segment, so the detours came out in *declaration* order rather than the order the
marble meets them, sending the route back and forth across the deck. It now
projects each detour onto the current segment and inserts it by distance along.

**7. A split entry with nowhere to go.** The route stepped sideways onto a
2.2-wide catwalk at the exact z the pit opened, leaving no distance to drift
across. Two causes: the approach deck was narrower than the catwalks it fed, and
the previous segment's centre waypoint sat directly over the pit mouth.

The pattern across all five: none of them are visible in a screenshot, and all
five were found by reading coordinates out of a log. Three of the five were
respawn-related, which makes sense — respawn is the one system that moves the
player somewhere they did not drive to, so it is the one place a bad assumption
about the geometry goes unnoticed until it strands someone.

## Limits

- The bot follows a fixed line, so it exercises completability, not difficulty.
  A course it clears with 0 falls may still be unpleasant for a human, and one
  it struggles with may be fine.
- It does not test the camera controls, the title screen, or restart — those were
  checked by driving real keypresses into the window with AppleScript.
- `make verify` is macOS-only because it runs a macOS player.
- It checks the built-in courses only. A course you write yourself gets no such
  guarantee: run the game with `-demo -courses /path/to/yours` to put the bot on
  it, and watch the `[death]` lines.
