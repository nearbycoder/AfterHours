#!/usr/bin/env bash
# Films the trailer footage from the built player at 1920x1080 (fixed 30 fps game clock):
#   Tools/trailer/record.sh [outdir] [reel...]       reels: title n1 n2 n3 n4 n5 n6 n7 (default: all)
#
# 1. If there's no story state yet (or AH_FRESH_STATE=1), the AutoPilot plays the "audit" route once
#    under the trailer-src profile, so every night has a real save snapshot to start from.
# 2. Each reel then runs from its own copy of those saves and films its clips into outdir/<clip>/.
# Then build the video with Tools/trailer/make_trailer.py.
#
# Every run is in a private KWin on a virtual screen (Tools/wmtest.sh run), so no window appears on
# the desktop, and the saves and settings live in outdir/xdg, never under ~/.config/unity3d.
# AH_QUALITY picks the Graphics fidelity step to film at (default 3, Ultra: the fixed clock means
# the frame rate can't drop on film). AH_DESKTOP=1 runs on the desktop with the real saves, as before.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/Recordings/trailer}")"
shift || true
REELS=("$@")
[ ${#REELS[@]} -eq 0 ] && REELS=(title n1 n2 n3 n4 n5 n6 n7)
QUALITY="${AH_QUALITY:-3}"
mkdir -p "$OUT"
if [ -n "${AH_DESKTOP:-}" ]; then
  DATA="${XDG_CONFIG_HOME:-$HOME/.config}/unity3d/After Hours Team/After Hours"
else
  XDG="$OUT/xdg"
  mkdir -p "$XDG"
  DATA="$XDG/config/unity3d/After Hours Team/After Hours"
fi

# play <name> <timeout> <player args...>: one run of the player, logged to outdir/<name>.log.
play() {
  local name="$1" secs="$2"; shift 2
  if [ -n "${AH_DESKTOP:-}" ]; then
    AH_W=1920 AH_H=1080 timeout "$secs" "$ROOT/Tools/play.sh" -logFile "$OUT/$name.log" "$@" > /dev/null 2>&1 || true
  else
    AH_WM_XDG="$XDG" AH_W=1920 AH_H=1080 AH_SCREEN=1920x1200 AH_RUN_TIMEOUT="$secs" \
      "$ROOT/Tools/wmtest.sh" "$OUT/wm-$name" run wayland -- -logFile "$OUT/$name.log" "$@" > /dev/null 2>&1 || true
  fi
}

if [ ! -f "$DATA/profile_trailer-src/night7_start.json" ] || [ -n "${AH_FRESH_STATE:-}" ]; then
  echo "== story state: AutoPilot audit route (trailer-src profile)"
  rm -rf "$DATA/profile_trailer-src"
  play autopilot 1800 -ahAutopilot "$OUT/autopilot" all -ahRoute audit -ahProfile trailer-src
  grep -E "\[AutoPilot\] (FAIL|done)" "$OUT/autopilot.log"
fi

for reel in "${REELS[@]}"; do
  echo "== reel $reel"
  rm -rf "$DATA/profile_trailer-$reel"
  cp -r "$DATA/profile_trailer-src" "$DATA/profile_trailer-$reel"
  play "$reel" 1200 -ahTrailer "$OUT" "$reel" -ahProfile "trailer-$reel" -ahQuality "$QUALITY"
  grep -E "\[Trailer\]|\[AutoPilot\] FAIL|Exception" "$OUT/$reel.log"
done
