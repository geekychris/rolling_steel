#!/usr/bin/env python3
"""Regenerate the README / docs gallery (docs/images/*.png) from the built player.

    scripts/build.sh && python3 scripts/docshots.py [--keep WORKDIR]

Each picture is one or more in-game screenshots taken with the player's own
dev hooks (-shotat, -demo, -yaw, -killat, -players, -editat), then tiled and
captioned with Pillow. The player's save folder (best times, ghosts, course
files) is backed up first and restored afterwards, and the demo runs use a
throwaway course folder, so your own saves and edited courses come back as
they were. --reuse recomposes from the screenshots of an earlier --keep run.

Picking moments: the demo run takes a screenshot every second and the log says
when each course starts and clears ([level], [state]); tiles are picked at
fractions of each course's time, so they follow the courses if they change.
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "Builds" / "RollingSteel.app"
OUT = ROOT / "docs" / "images"
SAVES = Path.home() / "Library/Application Support/claude world/Rolling Steel"
W, H = 1280, 720
CAPTION_H = 24
DECOR = ["grove", "frost", "embers", "arches", "balloons", "monoliths"]

FONT = None
for cand in ("/System/Library/Fonts/Supplemental/Arial.ttf", "/System/Library/Fonts/Helvetica.ttc"):
    if os.path.exists(cand):
        FONT = ImageFont.truetype(cand, 12)
        break
FONT = FONT or ImageFont.load_default()


def binary():
    exe = next((APP / "Contents" / "MacOS").iterdir(), None)
    if exe is None:
        sys.exit(f"no build at {APP} - run scripts/build.sh first")
    return str(exe)


REUSE = False


def run(work, name, args, quit_after, own_courses=True):
    """Run the player once; returns (shot dir, log text)."""
    shots = work / name
    log = work / (name + ".log")
    if REUSE and log.exists():
        return shots, log.read_text(errors="replace")
    shutil.rmtree(shots, ignore_errors=True)
    shots.mkdir(parents=True)
    courses = work / (name + "-courses")
    shutil.rmtree(courses, ignore_errors=True)
    cmd = [binary(), "-screen-fullscreen", "0", "-screen-width", str(W), "-screen-height", str(H),
           "-mute", "-shots", str(shots), "-quitafter", str(quit_after), "-logFile", str(log)] + args
    if own_courses:
        cmd += ["-courses", str(courses)]
    print("  run", name, " ".join(args))
    subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=quit_after + 120)
    return shots, log.read_text(errors="replace")


def shots_at(times):
    return ["-shotat", ",".join(f"{t:g}" for t in times)]


def shot(shots, log, t):
    """The screenshot taken nearest to clock time t."""
    best = None
    for m in re.finditer(r"\[shot\] (\S+) at t=([\d.]+)", log):
        d = abs(float(m.group(2)) - t)
        if best is None or d < best[0]:
            best = (d, m.group(1))
    if best is None:
        sys.exit(f"no screenshots in {shots}")
    return Image.open(best[1]).convert("RGB")


def tile(img, w, h, caption=None):
    """img scaled to w x h; with a caption, a black strip is added below it."""
    t = img.resize((w, h), Image.LANCZOS)
    if caption is None:
        return t
    out = Image.new("RGB", (w, h + CAPTION_H), (0, 0, 0))
    out.paste(t, (0, 0))
    ImageDraw.Draw(out).text((9, h + 6), caption, fill=(255, 200, 80), font=FONT)
    return out


def grid(tiles, cols):
    tw, th = tiles[0].size
    rows = (len(tiles) + cols - 1) // cols
    out = Image.new("RGB", (tw * cols, th * rows))
    for i, t in enumerate(tiles):
        out.paste(t, ((i % cols) * tw, (i // cols) * th))
    return out


def save(img, name):
    img.save(OUT / name, optimize=True)
    print("  wrote", OUT / name, img.size)


def events(log):
    """([(course index, start t)], {state: [t, ...]}) from a player log."""
    t_now, levels, states = 0.0, [], {}
    for line in log.splitlines():
        m = re.search(r"\[shot\] \S+ at t=([\d.]+)", line)
        if m:
            t_now = float(m.group(1))
        m = re.match(r"\[level\] (\d+)/\d+ ", line)
        if m:
            levels.append((int(m.group(1)), t_now))
        m = re.match(r"\[state\] (\w+) t=([\d.]+)", line)
        if m:
            states.setdefault(m.group(1), []).append(float(m.group(2)))
    return levels, states


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--keep", help="work directory to keep (default: a temp dir, deleted)")
    ap.add_argument("--reuse", action="store_true", help="reuse the screenshots in --keep instead of running")
    a = ap.parse_args()
    global REUSE
    REUSE = a.reuse
    work = Path(a.keep) if a.keep else Path(tempfile.mkdtemp(prefix="rs-docshots-"))
    work.mkdir(parents=True, exist_ok=True)

    backup = work / "saves.backup"
    had_saves = SAVES.exists()
    if had_saves and not backup.exists():
        shutil.copytree(SAVES, backup, symlinks=True)
    # start from no best times, so the demo's first clear reads "NEW BEST"
    (SAVES / "progress.json").unlink(missing_ok=True)
    try:
        build_all(work)
    finally:
        shutil.rmtree(SAVES, ignore_errors=True)
        if had_saves:
            shutil.copytree(backup, SAVES, symlinks=True)
        if not a.keep:
            shutil.rmtree(work, ignore_errors=True)


def build_all(work):
    # 1. the demo bot plays all six courses (~3 minutes): a screenshot every second
    demo_s, demo_log = run(work, "demo", ["-demo"] + shots_at(range(1, 215)), 215)
    levels, states = events(demo_log)
    starts = [t for _, t in levels]
    clears = states.get("LevelClear", [])
    won = states.get("Won", [])
    names = re.findall(r"\[level\] \d+/\d+ (.+?) clock=", demo_log)
    if len(starts) < 6 or len(clears) < 6 or not won:
        sys.exit(f"demo run did not clear all six courses (starts={starts} clears={clears} won={won})")

    def mid(i, frac=0.5):
        return starts[i] + (clears[i] - starts[i]) * frac

    # 2. the title card (after the demo, so the course select shows best times)
    title_s, title_log = run(work, "title", shots_at([2, 12]), 13)

    # 3. the same corner from three camera angles
    views = []
    for yaw, cap in ((0, "default"), (45, "+45 deg  (E)"), (90, "+90 deg  (E E)")):
        s, lg = run(work, f"yaw{yaw}", ["-demo", "-yaw", str(yaw)] + shots_at([6.5]), 7.5)
        views.append(tile(shot(s, lg, 6.5), 640, 386, cap))

    # 4. a forced wipeout
    kill = 7.0
    wipe_t = [(0.08, "impact flash"), (0.30, "shatter"), (0.80, "debris"), (1.40, "back on course")]
    wipe_s, wipe_log = run(work, "wipeout", ["-demo", "-killat", str(kill)] + shots_at([kill + d for d, _ in wipe_t]), kill + 2.5)

    # 5. two players, split screen
    two_s, two_log = run(work, "two", ["-demo", "-players", "2"] + shots_at(range(1, 40)), 40)
    _, two_states = events(two_log)

    # 6. the course editor, opened over course 1 (on the normal course folder,
    #    so the path it shows is the real one)
    ed_s, ed_log = run(work, "editor", ["-autostart", "-editat", "1.5"] + shots_at([3]), 4, own_courses=False)

    # ---- compose -------------------------------------------------------
    save(shot(title_s, title_log, 2), "title.png")

    save(grid([tile(shot(demo_s, demo_log, mid(i, 0.45)), 640, 336, f"{i + 1}  {names[i]} - {DECOR[i]}")
               for i in range(6)], 3), "courses.png")

    save(grid(views, 3), "views.png")

    clear1 = clears[0]
    save(grid([tile(shot(title_s, title_log, 2), 640, 336, "title flyover  t=2s"),
               tile(shot(title_s, title_log, 12), 640, 336, "title flyover  t=12s"),
               tile(shot(demo_s, demo_log, clear1 + 1), 640, 336, "course clear  +1.0s"),
               tile(shot(demo_s, demo_log, clear1 + 3), 640, 336, "course clear  +3.0s")], 2), "flyover.png")

    save(grid([tile(shot(wipe_s, wipe_log, kill + d), 640, 386, f"+{d:.2f}s  {c}") for d, c in wipe_t], 2), "wipeout.png")

    save(grid([tile(shot(demo_s, demo_log, mid(0, 0.45)), 640, 336, "pillars - block the line, never kill"),
               tile(shot(demo_s, demo_log, mid(1, 0.35)), 640, 336, "fan and sweeper"),
               tile(shot(demo_s, demo_log, mid(2, 0.6)), 640, 336, "crusher - fatal underneath"),
               tile(shot(demo_s, demo_log, mid(2, 0.92)), 640, 336, "crumbling deck - keep moving")], 2), "obstacles.png")

    save(grid([tile(shot(title_s, title_log, 12), 640, 336, "title doubles as a course select"),
               tile(shot(demo_s, demo_log, clear1 + 1.5), 640, 336, "time, medal and a new best")], 2), "scoring.png")

    two_clear = (two_states.get("LevelClear") or two_states.get("Won") or [20.0])[0]
    save(grid([tile(shot(two_s, two_log, 6), 640, 336, "split screen, separate clocks"),
               tile(shot(two_s, two_log, two_clear + 1.5), 640, 336, "first to the pad takes the course")], 2), "twoplayer.png")

    save(shot(ed_s, ed_log, 3), "editor.png")
    save(shot(demo_s, demo_log, mid(5, 0.5)), "course6.png")
    save(shot(demo_s, demo_log, won[0] + 2), "won.png")


if __name__ == "__main__":
    main()
