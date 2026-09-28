# Building

## Prerequisites

- **macOS** (Apple Silicon or Intel)
- **Unity 6000.6.3f1** with the **macOS Build Support** module

The project pins its editor version in `ProjectSettings/ProjectVersion.txt`.
The scripts prefer that exact version and fall back to the newest installed
editor with a warning, so a nearby 6.x usually works — Unity will offer to
upgrade the project on first open.

To use a specific editor without installing it where the scripts look:

```bash
UNITY=/path/to/Unity.app/Contents/MacOS/Unity make build
```

### Scripting backend

Builds use the **Mono** backend, pinned in `Assets/Editor/CIBuild.cs`, because
that is available with a stock macOS Build Support install. If you want IL2CPP
(smaller, faster, required for App Store distribution) install the
`mac-il2cpp` module via Unity Hub and change `ScriptingImplementation.Mono2x`
to `ScriptingImplementation.IL2CPP`.

## Commands

```bash
make build     # -> Builds/RollingSteel.app
make run       # build if needed, then launch windowed at 1280x720
make verify    # headless playthrough; fails if a course is not completable
make shots     # in-engine screenshots into shots/
make setup     # regenerate materials, scene, player settings
make clean     # remove Builds/, Logs/, Library/, Temp/, shots/
```

Each target is a thin wrapper around `scripts/*.sh`, which you can call directly:

```bash
scripts/build.sh                      # default output path
scripts/build.sh /tmp/Test.app        # custom output path
scripts/run.sh -autostart             # extra args go through to the player
scripts/verify.sh 180                 # allow a longer headless run
```

## First build

A clean checkout has no `Library/`, so the first build imports every asset and
resolves packages from the Unity registry (needs network). Expect a couple of
minutes. Later builds reuse `Library/` and take seconds.

## Regenerating derived assets

`Assets/Resources/Mat/*.mat`, `Assets/Scenes/Main.unity` and the player settings
are produced by `Assets/Editor/ProjectSetup.cs`. They are committed, so you do
not normally need this — but if you delete them, or want to change the palette
in one place:

```bash
make setup
```

## Troubleshooting

### "direct build failed - the project is probably open in the Unity Editor"

Unity refuses to open a project another editor instance has locked. `build.sh`
detects this and automatically retries from a synced clone in `.build-clone/`,
leaving your editor session untouched. This is expected and the build still
succeeds; closing the editor removes the extra step.

### The build reports success but the app will not launch

Check the architecture matches your machine:

```bash
lipo -archs Builds/RollingSteel.app/Contents/MacOS/*
```

### Where are the logs?

- `Logs/build.log` — the batchmode build (compile errors appear as `error CS…`)
- `Logs/verify.log` — the headless playthrough
- Runtime logs from a normally launched player go to
  `~/Library/Logs/claude world/Rolling Steel/Player.log`

## Building for other platforms

`CIBuild.BuildMacOS` targets `StandaloneOSX` only. Other targets need the
matching Unity module installed; the build method itself is a few lines and
`BuildTarget` is the only thing that has to change. Nothing in the game is
macOS-specific — it uses the legacy Input Manager and the built-in render
pipeline, both cross-platform.
