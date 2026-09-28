#!/usr/bin/env bash
# Run the built player windowed. Builds first if there is nothing to run.
# Extra args are passed through to the player, e.g.  scripts/run.sh -autostart
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/unity-env.sh"

[[ -d "$APP_DEFAULT" ]] || "$REPO_ROOT/scripts/build.sh"

BIN="$(app_binary "$APP_DEFAULT")"
[[ -x "$BIN" ]] || { echo "no executable inside $APP_DEFAULT" >&2; exit 1; }

exec "$BIN" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 "$@"
