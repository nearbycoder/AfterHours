#!/usr/bin/env bash
# Runs the built Linux player. XWayland hangs at startup on this machine, so use Unity's native
# Wayland backend when a Wayland session is available. Extra args are passed to the player, e.g.
#   Tools/play.sh -ahCapture /tmp/ah-shots proto
set -euo pipefail
GAME="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/Linux/AfterHours.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
args=(-screen-fullscreen 0 -screen-width 1600 -screen-height 900)
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
exec "$GAME" "${args[@]}" "$@"
