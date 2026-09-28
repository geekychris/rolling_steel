#!/usr/bin/env bash
# Capture in-engine screenshots (the player writes them itself, so you get the
# game window only - no desktop, no window chrome).
#   scripts/screenshots.sh [outdir]
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/unity-env.sh"

OUT="${1:-$REPO_ROOT/shots}"
rm -rf "$OUT"; mkdir -p "$OUT/title" "$OUT/play"

[[ -d "$APP_DEFAULT" ]] || "$REPO_ROOT/scripts/build.sh"
BIN="$(app_binary "$APP_DEFAULT")"

# title card: no -autostart, so it sits on the menu
"$BIN" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
       -shots "$OUT/title" -quitafter 2 -logFile "$REPO_ROOT/Logs/shots-title.log" >/dev/null 2>&1 || true

# a full self-played run, sampled across all three courses
"$BIN" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
       -demo -shots "$OUT/play" -quitafter 75 -logFile "$REPO_ROOT/Logs/shots-play.log" >/dev/null 2>&1 || true

echo "captured:"
ls "$OUT"/title "$OUT"/play
