#!/bin/sh
# Starts After Hours. In a Wayland session it uses Unity's native Wayland backend, because the
# player can hang at start-up under XWayland. Set AH_X11=1 to force X11 (XWayland) instead.
cd "$(dirname "$0")" || exit 1
if [ -n "${WAYLAND_DISPLAY:-}" ] && [ -z "${AH_X11:-}" ]; then
  exec ./AfterHours.x86_64 -force-wayland "$@"
fi
exec ./AfterHours.x86_64 "$@"
