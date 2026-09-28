#!/usr/bin/env bash
# Regenerate the derived assets: materials, the single scene, player settings.
# Only needed after a clean checkout or if you delete Assets/Resources or the scene.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/unity-env.sh"

LOG="$REPO_ROOT/Logs/setup.log"
mkdir -p "$REPO_ROOT/Logs"

"$UNITY_BIN" -batchmode -projectPath "$REPO_ROOT" \
  -executeMethod ProjectSetup.Run -logFile "$LOG"

grep -E "SETUP_OK|error CS" "$LOG" | head -10
