# The course editor

Press **F1** in game. The course you are on opens as a list of lines; every
change rebuilds it immediately and parks the marble at the line you are editing,
so you are always looking at what you just changed.

![The in-game course editor](images/editor.png)

## Keys

| Key | Does |
|-----|------|
| `Up` / `Down` | select a line |
| `Tab` | select a field within the line (`Shift+Tab` goes back) |
| `Left` / `Right` | change the selected value |
| `Shift` + `Left`/`Right` | ×10 steps |
| `Alt` + `Left`/`Right` | ×0.1 steps |
| `Space` | toggle the line's flag (`rails`, `start`) |
| `I` | insert a line — `,` / `.` pick the verb, `Enter` confirms |
| `D` | duplicate the selected line |
| `Del` / `Backspace` | delete the selected line |
| `S` | save to disk |
| `L` | reload from disk, discarding unsaved changes |
| `F1` / `Esc` | close, and play on from wherever the marble is parked |

The camera keys (`Q`/`E`/`Z`/`X`, wheel) keep working while the editor is open,
so you can look around the piece you are editing.

A course that will not build is ignored rather than applied, so a half-finished
edit never leaves you with no level.

## Where courses live

On first run the built-in courses are written to

```
~/Library/Application Support/claude world/Rolling Steel/Courses/
```

as `1.course` … `6.course`. The editor saves back there. Only missing files are
written, so a build that adds courses adds their files without touching edits you
have already made. You can edit
them in any text editor instead and press `L` in game to reload, or point the
game somewhere else entirely:

```bash
scripts/run.sh -courses /path/to/my/courses
```

Any `*.course` file in that directory is loaded, sorted by filename — so adding
`7.course` adds a seventh course. A file that fails to parse falls back to the
built-in course of the same index, and unknown verbs are skipped with a warning
rather than taking the game down.

## The file format

One line per piece. A verb, then named numbers, then bare flags. `#` starts a
comment, and comments survive a round trip through the editor.

```
name PRACTICE
time 75
width 10
decor 0
music 1

pad d=10 w=10 start
rails
run len=12 w=9
hill len=16 drop=4 w=8 rails        # eased ramp, flat at both ends
curve r=15 a=60 drop=1.5 w=7 bank=12 rails
pillar x=0 back=4 r=0.9 h=2.6
split len=15 side=3 pit=3.5
chaser x=0 back=4 range=18 speed=8
goal d=9
```

### Header

| Verb | Meaning |
|------|---------|
| `name` | shown on the HUD |
| `time` | seconds added to the clock on entering this course |
| `width` | starting deck width |
| `decor` | scenery theme: 0 grove, 1 frost, 2 embers, 3 arches, 4 balloons, 5 monoliths |
| `music` | theme index: 0 title, 1-6 the course themes |
| `medals` | `gold`, `silver`, `bronze` target times in seconds |

### Track

| Verb | Fields |
|------|--------|
| `pad` | `d` depth, `w` width, flag `start` |
| `run` / `ice` / `rough` | `len`, `w` |
| `crumble` | `len`, `w`, `tile` — deck that drops away after you touch it |
| `slope` | `len`, `drop`, `w` — constant gradient, visible fold |
| `hill` | `len`, `drop`, `w`, flag `rails` — eased gradient, no crease |
| `curve` | `r` radius, `a` angle (+ right), `drop`, `w`, `bank`, flag `rails` |
| `chicane` | as `curve`; an S-bend ending on the original heading |
| `jog` | `dx` sideways, `d` down-course, `w` |
| `gap` | `len` — no deck |
| `step` | `dy` — sheer drop, no connecting geometry |
| `jump` | `gap`, `drop`, `lip` — flat lip, then the gap |
| `split` | `len`, `side`, `pit`, `apron` — twin catwalks over a pit |
| `rails` | `l`, `r` (0 or 1), `h` — kerbs on the piece just laid |
| `goal` | `d`, `w` |

### Hazards

| Verb | Fields |
|------|--------|
| `acid` | `w`, `d`, `x` lateral, `back`, `lead` |
| `pillar` | `x`, `back`, `r`, `h` — static post; blocks, never kills |
| `sweeper` | `x`, `back`, `len`, `speed` deg/s, `h`, `phase` — rotating arm |
| `crusher` | `x`, `back`, `w`, `period`, `phase`, `lift` — slams down; fatal underneath |
| `fan` | `x`, `back`, `w`, `d`, `push` — shoves you sideways; negative `push` flips it |
| `boost` | `x`, `back`, `w`, `d`, `push` — shoves you down-course |
| `chaser` | `x`, `back`, `range`, `speed` — steel marble |
| `blob` | `x`, `back`, `range`, `speed` — green blob; fatal on contact |

`x` is offset across the deck and `back` is distance back from the end of the
piece the hazard is attached to, so hazards are written after the piece they sit
on.

## Things worth knowing when authoring

- **Hazards must have room to be dodged.** `acid`, `pillar` and `crusher` splice
  a detour into the route automatically, but a detour needs distance to drive:
  a pillar 2 units into a 7-unit run cannot be avoided at speed however wide the
  deck is. Give the piece length.
- **Sweepers and fans cannot be routed around**, because the danger moves. Put a
  sweeper's pivot at the deck edge if you want the middle line to stay clear.
- **`rails` applies to the piece just laid**, whichever kind it was.
- Everything else about joins, widths and gradients is handled for you — see
  [Course design](COURSE-DESIGN.md).
