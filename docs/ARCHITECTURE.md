# Architecture

## The one-asset rule

The project contains exactly one authored asset: `Assets/Scenes/Main.unity`, an
otherwise empty scene holding a single `GameDirector` component. Every piece of
geometry, every light, the camera, the HUD and all audio are created in code at
runtime.

That is a deliberate constraint, and it buys three things:

- the whole game regenerates from source — there is no hand-wired scene to drift
  out of sync with the code, and no binary asset to resolve merge conflicts in;
- courses are data, so adding one is a function call rather than an afternoon of
  dragging cubes;
- the game can be built, played and verified headlessly from a terminal.

The one thing that is *not* generated at runtime is materials. They are real
assets under `Assets/Resources/Mat/`, created by an editor script. See
[Materials and shader stripping](#materials-and-shader-stripping).

## Scripts

| Script | Role |
|--------|------|
| `Course.cs` | `CourseBuilder` — a cursor-based DSL for laying out a descent, plus the centreline it records |
| `CourseLibrary.cs` | the three built-in courses, as course-script source |
| `LevelBuilder.cs` | turns course data into colliders, renderers and hazards; meshes the swept pieces |
| `Decor.cs` | abstract floating scenery, themed per level |
| `DeathFx.cs` | the wipeout: physical shards, sparks and their cleanup |
| `Obstacles.cs` | sweepers, crushers, fan zones and crumbling deck |
| `CourseScript.cs` | the course file format: parse, render, and build |
| `CourseStore.cs` | where course files live; seeding and fallback |
| `CourseEditor.cs` | the in-game editor |
| `Progress.cs` | best times and medals, persisted as JSON |
| `Ghost.cs` | recording and playback of your best run |
| `Music.cs` | step sequencer and synth; the whole soundtrack |
| `MarbleController.cs` | rigidbody marble: camera-relative force, ground detection, speed cap |
| `IsoCamera.cs` | orthographic 3/4 chase camera with live yaw / tilt / zoom |
| `GameDirector.cs` | state machine, clock, deaths, level progression, CLI hooks |
| `Hazards.cs` | acid zones, the goal pad, steel chasers and green blobs |
| `Hud.cs` | IMGUI HUD |
| `Sfx.cs` | one-shot effects, also synthesised |
| `MatLib.cs` | material lookup plus physics materials (ice, sand, deck) |
| `Editor/ProjectSetup.cs` | generates materials, the scene and player settings |
| `Editor/CIBuild.cs` | headless macOS player build |

## Flow

```
GameDirector.Awake
  ├─ BuildRig        camera + IsoCamera + two directional lights + fog/ambient
  ├─ BuildMarble     sphere + Rigidbody + MarbleController + rolling audio
  ├─ Hud             added as a component on the director
  └─ LoadLevel(0)
        └─ LevelBuilder.Build(CourseLibrary.All()[0])
              ├─ a GameObject per Block (primitive cube, scaled + rotated)
              ├─ enemies as spheres with EnemyBall
              ├─ the course centreline, densified, in world space
              ├─ a per-waypoint "is this safe to respawn on?" flag
              └─ Decor.Scatter: themed floating scenery, no colliders
```

Each frame `GameDirector.Update` ticks the clock, runs the state machine
(`Title → Playing → Dying → LevelClear → … → Won / GameOver`) and feeds the
optional demo driver.

## Two kinds of track geometry

Slabs are `GameObject.CreatePrimitive(Cube)` with a scale and rotation — cheap,
and the hard edges are what make the isometric view readable.

Curves and eased hills are swept **ribbons**: a list of cross-sections, each with
a position, a frame and a width, meshed at build time. The frame comes from the
actual tangent through the point rather than the nominal heading, so the deck
stays perpendicular to travel however steep or banked it gets.

The mesh is built as four separate vertex groups — top, bottom, and the two sides
— plus end caps. Because the groups do not share vertices, `RecalculateNormals`
smooths *along* the sweep (so a curve reads as a curve) while leaving the edges
crisp. That is what lets a ribbon sit next to a chunky slab without the two
looking like they came from different games. Collision is a non-convex
`MeshCollider` over the same mesh.

### Making pieces link rather than abut

Three things were making joins visible, and all three are handled in the builder
rather than left to the course author:

- **Width stepped at the first cross-section.** A piece used its *target* width
  for every node, so a change from 9 to 8 appeared as a half-unit notch in each
  edge exactly at the seam. Widths now ease from the cursor's current width to
  the target across the piece. Straight runs are swept rather than boxed when
  their width changes, for the same reason.
- **Coincident end faces.** Two pieces that merely touch leave a hairline where
  their end faces fight. Every piece is now grown by a hair (0.06) at each end,
  so its cap sits buried inside its neighbour.
- **Gradient and bank.** `Hill` and `Curve` both use a smoothstep profile, whose
  derivative is zero at both ends, and `Curve` ramps its bank in and out on a
  sine. Pieces therefore always arrive and leave flat and level, so consecutive
  pieces are continuous in gradient and roll without the author matching anything
  up by hand. `Slope` deliberately keeps a constant gradient, for where the fold
  should be visible.

## Course space and the camera

Courses are authored with `+Z` running down-course, `+X` to the right and `-Y`
down — a plain, readable coordinate system to write level data in.

`LevelBuilder` then yaws the entire course 45° about Y. That is what puts the
course on the screen diagonal and produces the isometric read; without it the
slabs render as screen-aligned rectangles and the scene looks flat.

Because `MarbleController` applies force in the **camera's** basis rather than
the course's, that 45° is purely cosmetic. The player can rotate the view to any
angle and the controls rotate with it — "push up" always means "away from the
camera". This is why the camera is free to spin without breaking the game.

## Materials and shader stripping

Materials are created as assets by `ProjectSetup.cs` and loaded with
`Resources.Load<Material>("Mat/…")`, rather than built at runtime with
`new Material(Shader.Find("Standard"))`.

The reason is build-time shader stripping. A shader referenced only by a
`Shader.Find` string at runtime has nothing in the build graph pointing at it,
so it can be stripped and every surface comes out magenta in the player while
looking perfect in the editor. Anything under `Resources/` is force-included
along with its dependencies, which makes the Standard shader's presence a
property of the project rather than a thing to remember.

Physics materials have no shader dependency, so `MatLib` just makes those on the
fly. They use `PhysicsMaterialCombine.Multiply` for friction, so the marble's own
friction scales the deck's: ice (0.05) genuinely lets go, sand (1.1) bites.

## The marble

A sphere `Rigidbody` and nothing clever:

- input is converted to a world direction in the camera's basis and applied with
  `AddForce(..., ForceMode.Acceleration)`;
- extra downward acceleration (12 m/s² on top of gravity) stops it floating —
  note this makes jumps much harder than they look, see
  [Course design](COURSE-DESIGN.md#jumps);
- horizontal speed is capped, vertical is not, so falling stays fast;
- grounding comes from contact normals in `OnCollisionStay` (`normal.y > 0.45`),
  not a raycast, so ramps and kerbs work without special cases;
- `maxAngularVelocity` is raised to 60 — the default of 7 makes a rolling sphere
  look like it is skidding.

Rotation is never scripted. The marble rolls because friction between a sphere
collider and a deck makes it roll.

## Music

`Music.cs` is a small step sequencer (16 steps a bar, eight bars) feeding a
handful of synth voices. There are seven themes — a title track and one per
course, each in its own key and tempo, one of them in a major key because Silly
earns it: saw with a one-pole lowpass for the bass, variable-duty
pulse for the arpeggio, two-operator FM for the lead, and shaped noise for the
drums. Each theme renders into a `float[]`, is normalised and soft-clipped,
crossfaded at the loop point, and becomes an `AudioClip` — cached, so a theme
costs nothing after the first time its course is reached.

`-dumpmusic DIR` writes every theme as a WAV, which is how the soundtrack gets
checked without anyone having to listen to it. See
[Verification](VERIFICATION.md#checking-the-music).

## Courses as data

Courses are text, not code. `CourseScript` parses a file into a list of lines —
each a verb, some named numbers, some flags — and drives `CourseBuilder` with
them. `CourseLibrary` holds the built-ins as source strings, and `CourseStore`
seeds them to disk on first run so there is always something to edit.

That is what makes the in-game editor possible: editing is manipulating a list of
numbers and rebuilding, rather than anything that needs a scene or a serialiser.
A rebuild is a few milliseconds, so every keystroke can re-make the whole course.

Two properties fall out of it and are worth preserving:

- **What ships is what the editor edits.** There is one course format, so a
  built-in course and a hand-written one are the same kind of thing.
- **A bad course cannot take the game down.** A file that will not parse falls
  back to the built-in of the same index; an unknown verb is skipped with a
  warning; and an edit that will not build leaves the previous level standing.

`Level.Anchors` records the cursor position after each source line, which is how
the editor parks the marble at the piece you are editing.

## Times, medals and ghosts

`Progress` keeps best times in `progress.json`, keyed by course **name** rather
than index — so reordering the courses, or dropping a new one into the folder,
does not scramble anyone's records. Medal thresholds are declared by each course
in its own file, which keeps them editable and keeps the notion of "gold" a
property of the course rather than a constant in the code.

`Ghost` records the marble's position at a fixed 20 Hz and saves it whenever a
course best is beaten. Two details make it robust: samples are taken on the
**course clock** rather than wall time, so playback lines up with your own run
frame-for-frame; and they are stored in **course space** rather than world space,
so a ghost survives any change to how the course root is oriented.

## The cinematic orbit

`IsoCamera` has a second mode that takes over whenever nobody is driving: a slow
orbit around a focus point the director chooses. On the title card the focus
drifts along the course centreline, so it is a flyover of the whole course rather
than a turntable of one spot; on a cleared course it circles the finish pad.

The detail that matters is *what* gets smoothed. Smoothing the camera position
leaves it trailing its own rotation, and because the orbit radius is 90 units,
even a few degrees of lag throws the subject ten units off centre. The orbit
therefore eases the **focus point** and then places the camera exactly on the
circle around it. The focus is also lifted a few units above the subject, which
drops the subject into clear screen space below the banner.

The player's own yaw is never touched, so leaving the orbit eases back to
whatever angle they were playing at.

## The wipeout

Deaths are physical rather than a particle system: `DeathFx` spawns chunky shards
with rigidbodies that bounce off the deck, plus smaller collider-less sparks for
legibility at this camera distance. A `Fader` shrinks and removes each one, since
debris that lingers spends the rest of the run falling through the level.

Around that, `GameDirector` flashes the screen (tinted by cause of death), knocks
the camera, and drops `Time.timeScale` to 0.3, easing it back to 1 across the
death hold. Because time is scaled, the director's own timers — the state clock,
the screenshot schedule, the flash decay, the camera shake — all run on
`unscaledDeltaTime`; only gameplay is slowed.

Falls are detected by how far the marble has dropped *since it last had contact*
rather than by an absolute floor. The old absolute test fired 14 units below the
lowest deck, by which point the marble was far off screen and there was nothing
to see. It survives as a backstop.

## Unity 6 API notes

Unity 6 renamed several things this code relies on:

| Old | Current |
|-----|---------|
| `Rigidbody.velocity` | `Rigidbody.linearVelocity` |
| `Rigidbody.drag` / `angularDrag` | `linearDamping` / `angularDamping` |
| `PhysicMaterial` | `PhysicsMaterial` |
| `PhysicMaterialCombine` | `PhysicsMaterialCombine` |
| `FindObjectOfType` | `FindFirstObjectByType` / `FindObjectsByType` |

The old names are gone rather than merely deprecated, so this project will not
compile on Unity 2022 or earlier without changing them back.
