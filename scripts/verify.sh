#!/usr/bin/env bash
# Completability check. The player drives itself along each course centreline
# and we assert it reaches the win state. Exits non-zero if it does not.
#   scripts/verify.sh [seconds]
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/unity-env.sh"

TIMEOUT="${1:-120}"
LOG="$REPO_ROOT/Logs/verify.log"
mkdir -p "$REPO_ROOT/Logs"

[[ -d "$APP_DEFAULT" ]] || "$REPO_ROOT/scripts/build.sh"
BIN="$(app_binary "$APP_DEFAULT")"

echo "running headless playthrough (up to ${TIMEOUT}s)..."
"$BIN" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
       -demo -quitafter "$TIMEOUT" -logFile "$LOG" >/dev/null 2>&1 || true

echo "--- progression ---"
grep -E "\[level\]|\[state\]" "$LOG" || true
echo "--- falls: $(grep -c '\[death\]' "$LOG" || true) ---"

if grep -q '\[state\] Won' "$LOG"; then
  echo "PASS - all three courses cleared"
else
  echo "FAIL - never reached the win state; see $LOG" >&2
  grep -E '\[death\]' "$LOG" | head -10 >&2 || true
  exit 1
fi
