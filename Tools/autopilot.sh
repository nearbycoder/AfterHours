#!/usr/bin/env bash
# Self-test: the built game plays all seven nights from the title to the ending through real
# components, saves screenshots to ${1:-/tmp/ah-autopilot} and prints PASS/FAIL lines.
#   Tools/autopilot.sh [outdir] [nightN|all] [route] [profile]
#     nightN stops after that night (quick iteration)
#     route is audit (default), loose, cleanbooks, spotless or marian: which ending to play towards
#     profile is the save profile (default autopilot-<route>); records (best grades, endings seen)
#     carry over between runs that share one
# The game runs in a private KWin on a virtual screen (Tools/wmtest.sh run), so its window never
# appears on the desktop and its saves and settings go to <outdir>/wm/config (or, when a profile
# is named, Recordings/autopilot-profiles/<profile>, kept between runs). AH_DESKTOP=1 runs it on
# the desktop as before, with the saves under ~/.config/unity3d.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-/tmp/ah-autopilot}"
SCEN="${2:-all}"
ROUTE="${3:-audit}"
PROFILE="${4:-autopilot-$ROUTE}"
rm -rf "$OUT"; mkdir -p "$OUT" && OUT="$(cd "$OUT" && pwd)"
args=(-logFile "$OUT/player.log" -ahAutopilot "$OUT" "$SCEN" -ahRoute "$ROUTE" -ahProfile "$PROFILE")
if [ -z "${AH_DESKTOP:-}" ] && command -v kwin_wayland > /dev/null && command -v dbus-run-session > /dev/null; then
  xdg=""; [ -n "${4:-}" ] && xdg="$ROOT/Recordings/autopilot-profiles/$PROFILE"
  AH_WM_XDG="$xdg" AH_RUN_TIMEOUT=1800 "$ROOT/Tools/wmtest.sh" "$OUT/wm" run wayland -- "${args[@]}" > /dev/null 2>&1 || true
else
  timeout 1800 "$ROOT/Tools/play.sh" "${args[@]}" > /dev/null 2>&1 || true
fi
grep -E "\[AutoPilot\] (PASS|FAIL|done|started)|Exception" "$OUT/player.log"
! grep -qE "\[AutoPilot\] FAIL|Exception" "$OUT/player.log" && grep -q "\[AutoPilot\] done" "$OUT/player.log"
