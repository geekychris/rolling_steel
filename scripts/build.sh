#!/usr/bin/env bash
# Build the macOS player.
#   scripts/build.sh [output.app]
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/unity-env.sh"

OUT="${1:-$APP_DEFAULT}"
LOG="$REPO_ROOT/Logs/build.log"
mkdir -p "$(dirname "$OUT")" "$REPO_ROOT/Logs"

build_from() {
  "$UNITY_BIN" -batchmode -projectPath "$1" \
    -executeMethod CIBuild.BuildMacOS -buildOut "$OUT" -logFile "$LOG"
}

echo "unity:  $UNITY_BIN"
echo "output: $OUT"

if ! build_from "$REPO_ROOT"; then
  # Unity refuses to open a project that the editor already has locked.
  # Build from a synced clone so an open editor session is left alone.
  echo "direct build failed - the project is probably open in the Unity Editor."
  echo "retrying from a clone..."
  CLONE="$REPO_ROOT/.build-clone"
  mkdir -p "$CLONE"
  for d in Assets Packages ProjectSettings; do
    rsync -a --delete "$REPO_ROOT/$d/" "$CLONE/$d/"
  done
  if ! build_from "$CLONE"; then
    echo "build failed; see $LOG" >&2
    grep -E "error CS" "$LOG" | head -20 >&2 || true
    exit 1
  fi
fi

grep -E "\[CIBuild\] result=|BUILD_OK" "$LOG" | tail -2
