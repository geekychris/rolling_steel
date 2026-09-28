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
| `CourseLibrary.cs` | the three courses, written against that DSL |
| `LevelBuilder.cs` | turns course data into colliders, renderers and hazards; meshes the swept pieces |
| `Decor.cs` | abstract floating scenery, themed per level |
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
handful of synth voices: saw with a one-pole lowpass for the bass, variable-duty
pulse for the arpeggio, two-operator FM for the lead, and shaped noise for the
drums. Each theme renders into a `float[]`, is normalised and soft-clipped,
crossfaded at the loop point, and becomes an `AudioClip` — cached, so a theme
costs nothing after the first time its course is reached.

`-dumpmusic DIR` writes every theme as a WAV, which is how the soundtrack gets
checked without anyone having to listen to it. See
[Verification](VERIFICATION.md#checking-the-music).

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
