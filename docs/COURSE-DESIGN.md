# Course design

Courses live in `Assets/Scripts/CourseLibrary.cs` and are written against the
`CourseBuilder` DSL in `Assets/Scripts/Course.cs`.

## The cursor

`CourseBuilder` keeps a cursor at the centre of the **leading edge of the deck,
level with the deck's top surface**, plus a **heading**. Every call lays geometry
from the cursor and moves it along, so segments join up automatically and you
never type a coordinate. `Curve()` turns the heading, and everything after it is
laid out in the new direction.

```csharp
var b = new CourseBuilder("PRACTICE", timeBonus: 75f, startWidth: 10f);

b.Pad(10f, 10f, Surface.Start).Rails();   // starting platform, kerbed
b.Run(14f, 9f).Rails();                   // flat, 14 long, 9 wide
b.Slope(16f, 4f, 8f).Rails();             // 16 along, dropping 4
b.Jog(-6f, 12f, 6f);                      // shift 6 left while advancing 12
b.Split(16f, 3f, 3.5f);                   // two decks with a pit between
b.Goal();

return b.Done();
```

## Reference

| Call | Effect |
|------|--------|
| `Pad(depth, width, surface)` | platform from the cursor |
| `Run(len, width)` | flat segment |
| `Slope(len, drop, width)` | descending ramp |
| `Jog(dx, depth, width)` | diagonal shuffle sideways while advancing |
| `Ice(len, width)` / `Rough(len, width)` | low-friction / high-friction deck |
| `Split(len, sideWidth, gapWidth, apron)` | a solid apron, then two parallel decks either side of a pit |
| `Gap(len)` | advance without laying deck |
| `Step(dy)` | sheer drop with no connecting geometry |
| `Jump(gapLen, drop, lip)` | a flat lip, then a gap, then a step down |
| `Curve(radius, angle, drop, w, bank, rails)` | banked turn; positive angle turns right |
| `Hill(len, drop, w, rails)` | ramp whose incline eases in and out |
| `Chicane(radius, angle, drop, w, bank, rails)` | an S-bend, ending on the original heading |
| `Rails(left, right, h)` | kerbs along the segment just built |
| `Acid(w, depth, lateral, back, leadIn)` | acid patch on the segment just built |
| `Enemy(kind, lateral, back, range, speed)` | a chaser or a blob |
| `Goal(depth, width)` | the finish pad |

**`Rails()` only knows about the last *slab*.** Curves and hills take a `rails:`
argument instead; chaining `.Rails()` after one silently kerbs the wrong piece.

Omitting a `width` keeps the current one. Widths are in the same units as
everything else; the marble has a radius of 0.5, so a 3-wide catwalk gives you
2 units of usable room.

## Slabs or curves?

Both, on purpose. Slabs are chunky boxes with hard edges — they read well
isometrically and make a drop obvious at a glance. Swept pieces flow, which suits
turns and long descents.

A `Hill` is usually better than a `Slope` where a ramp meets flat deck: it starts
and ends at zero gradient, so there is no crease to catch the marble, and it
leaves the lip flat, which matters enormously for jumps (below). Use `Slope` when
you want the fold to be visible and deliberate.

`Curve` banks in and out with a sine ramp, so the ends always meet flat deck
level however hard the middle is banked. Bank is worth having: it holds the
marble through a turn that would otherwise throw it off the outside edge.

## Surfaces

`Normal`, `Rough` (sand), `Ice`, `Acid`, `Goal`, `Start`, `Rail`. Each maps to a
visual material and a physics material in `MatLib`. Friction uses
`PhysicsMaterialCombine.Multiply`, so the effective friction is the deck's times
the marble's.

## Jumps

**This is the trap worth reading before you design one.**

Gravity in this game is about 22 m/s² (world gravity plus 12 of extra downward
acceleration, which keeps the marble from feeling floaty). That makes airborne
time short, and it makes a gap much harder to clear than its length suggests.

Worse, if a gap opens directly off a downhill ramp, the marble leaves the lip
*already travelling downward*, and falls out of the air almost immediately.

The first version of these courses had two gaps built that way. Both were
impossible **even at maximum speed**:

| Gap | Ramp | Speed | Flight | Gap length | |
|-----|------|-------|--------|-----------|---|
| course 2 | 18.4° | 13.0 m/s (max) | 2.94 m | 3.2 m | falls short |
| course 3 | 26.6° | 13.0 m/s (max) | 2.77 m | 3.4 m | falls short |

`Jump()` exists to prevent exactly this. It lays a flat lip first, so the launch
is horizontal:

```csharp
b.Slope(12f, 4f, 5f);
b.Jump(gapLen: 2.6f, drop: 2.4f);   // 5-unit flat lip is the default
b.Run(9f, 7f).Rails();
```

With a flat lip the same gaps clear comfortably at 6 m/s — well below cruising
speed. If you change gravity, re-check every jump:

```
flight = v_horizontal * t,  where  t = (-v_down + sqrt(v_down² + 2·g·drop)) / g
```

## The centreline

Every call also records a point on the route. `LevelBuilder` densifies it to
roughly 2-unit spacing and hands it to `BuiltLevel.PathWorld`. It is used for:

- **respawning** — after a fall the marble returns to the centreline point just
  behind where it last had contact;
- **the demo driver** — see [Verification](VERIFICATION.md).

Two places need the centreline nudged away from the geometric middle, and both
are handled inside the builder:

- `Acid()` splices in a detour down whichever side of the deck has more room,
  because the patch is deliberately laid *on* the route;
- `Split()` lays a solid **apron** before the pit and drifts onto the left catwalk
  across it. The apron exists precisely so that sideways move happens over deck.
  Without it, the drift waypoint is placed relative to the split's centre, which
  the preceding segment may not reach — if that segment is a diagonal `Jog` still
  moving laterally, the waypoint lands over the void.

**The rule: every waypoint a player can respawn onto must have solid deck under
it and nothing lethal on it.** `LevelBuilder` enforces this rather than trusting
it: after building the geometry it raycasts down from each densified waypoint,
and separately checks whether the point sits inside an acid trigger. Both answers
fold into `BuiltLevel.PathSupported`. Respawn uses only safe waypoints, so a
waypoint spanning a `Jump()` gap, or one that clips the corner of an acid pond,
stays available for steering without ever becoming a respawn point.

Hazards on the same piece are spliced into the centreline **in travel order**,
not the order you declared them — `Acid()` projects its detour onto the current
segment to work out where it belongs. Declaring two acids back-to-front is
therefore safe, though writing them in the order the marble meets them still
reads better.

If you add a hazard type that blocks the route, give it the same treatment or
respawning will drop the player onto it.

## Difficulty

Time carries over between courses, so a course's `timeBonus` is a budget on top
of whatever the player saved, not a fresh start. Falling costs 3 seconds.

Rules of thumb that came out of tuning these three:

- 6-10 wide is comfortable; 4 is tense; below 3.5 is punishing at speed.
- Give a hazard room to be dodged. `Acid()` splices a detour around itself, but a
  detour needs *distance* to be driven: a patch 2 units into a 7-unit run cannot
  be avoided at speed however wide the deck is. `leadIn` defaults to 4 units;
  the flat piece has to be long enough to contain it.
- Ice wants kerbs unless you intend it to be the hard bit.
- Put sand where the player most wants to be fast, not where they are already slow.
- A chaser near a narrow section is far more dangerous than one on open deck —
  it does not kill you, it just makes the edge arrive sooner.

After changing a course, run `make verify` to confirm it is still completable.
