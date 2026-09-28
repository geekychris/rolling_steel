#!/usr/bin/env bash
# Shared helper: locate the repo root and a Unity editor binary.
# Sourced by the other scripts; not meant to be run directly.
#
# Override the editor with:  UNITY=/path/to/Unity.app/Contents/MacOS/Unity
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

PINNED="$(sed -n 's/^m_EditorVersion: //p' "$REPO_ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r\n')"
HUB="${UNITY_HUB_EDITORS:-/Applications/Unity/Hub/Editor}"

if [[ -n "${UNITY:-}" ]]; then
  UNITY_BIN="$UNITY"
elif [[ -x "$HUB/$PINNED/Unity.app/Contents/MacOS/Unity" ]]; then
  UNITY_BIN="$HUB/$PINNED/Unity.app/Contents/MacOS/Unity"
else
  UNITY_BIN="$(ls -d "$HUB"/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1 || true)"
  if [[ -n "$UNITY_BIN" ]]; then
    echo "note: Unity $PINNED not installed; using $(basename "$(dirname "$(dirname "$(dirname "$UNITY_BIN")")")")" >&2
  fi
fi

if [[ -z "${UNITY_BIN:-}" || ! -x "$UNITY_BIN" ]]; then
  cat >&2 <<MSG
error: no Unity editor found.
  Looked for $PINNED under $HUB
  Install it via Unity Hub, or set UNITY=/path/to/Unity.app/Contents/MacOS/Unity
MSG
  exit 1
fi

APP_DEFAULT="$REPO_ROOT/Builds/RollingSteel.app"

# Path to the executable inside a built .app (its name follows productName).
app_binary() {
  ls "$1/Contents/MacOS/"* 2>/dev/null | head -1
}
