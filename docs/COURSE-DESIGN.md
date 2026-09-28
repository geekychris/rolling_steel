# Course design

Courses live in `Assets/Scripts/CourseLibrary.cs` and are written against the
`CourseBuilder` DSL in `Assets/Scripts/Course.cs`.

## The cursor

`CourseBuilder` keeps a cursor at the centre of the **leading edge of the deck,
level with the deck's top surface**. Every call lays geometry from the cursor and
moves it along, so segments join up automatically and you never type a
coordinate.

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
| `Split(len, sideWidth, gapWidth)` | two parallel decks either side of a pit |
| `Gap(len)` | advance without laying deck |
| `Step(dy)` | sheer drop with no connecting geometry |
| `Jump(gapLen, drop, lip)` | a flat lip, then a gap, then a step down |
| `Rails(left, right, h)` | kerbs along the segment just built |
| `Acid(w, depth, lateral, back)` | acid patch on the segment just built |
| `Enemy(kind, lateral, back, range, speed)` | a chaser or a blob |
| `Goal(depth, width)` | the finish pad |

Omitting a `width` keeps the current one. Widths are in the same units as
everything else; the marble has a radius of 0.5, so a 3-wide catwalk gives you
2 units of usable room.

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
- `Split()` commits to the left deck 4.5 units *before* the pit opens, and drops
  the previous segment's centre point — which sits directly over the pit mouth.

If you add a hazard type that blocks the route, give it the same treatment or
respawning will drop the player onto it.

## Difficulty

Time carries over between courses, so a course's `timeBonus` is a budget on top
of whatever the player saved, not a fresh start. Falling costs 3 seconds.

Rules of thumb that came out of tuning these three:

- 6-10 wide is comfortable; 4 is tense; below 3.5 is punishing at speed.
- Ice wants kerbs unless you intend it to be the hard bit.
- Put sand where the player most wants to be fast, not where they are already slow.
- A chaser near a narrow section is far more dangerous than one on open deck —
  it does not kill you, it just makes the edge arrive sooner.

After changing a course, run `make verify` to confirm it is still completable.
