#!/usr/bin/env bash
# Films the trailer footage from the built player at 1920x1080 (fixed 30 fps game clock):
#   Tools/trailer/record.sh [outdir] [reel...]       reels: title n1 n2 n3 n4 n5 n6 n7 (default: all)
#
# 1. If there's no story state yet (or AH_FRESH_STATE=1), the AutoPilot plays the "audit" route once
#    under the trailer-src profile, so every night has a real save snapshot to start from.
# 2. Each reel then runs from its own copy of those saves and films its clips into outdir/<clip>/.
# Then build the video with Tools/trailer/make_trailer.py.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/Recordings/trailer}")"
shift || true
REELS=("$@")
[ ${#REELS[@]} -eq 0 ] && REELS=(title n1 n2 n3 n4 n5 n6 n7)
DATA="${XDG_CONFIG_HOME:-$HOME/.config}/unity3d/After Hours Team/After Hours"
mkdir -p "$OUT"

if [ ! -f "$DATA/profile_trailer-src/night7_start.json" ] || [ -n "${AH_FRESH_STATE:-}" ]; then
  echo "== story state: AutoPilot audit route (trailer-src profile)"
  rm -rf "$DATA/profile_trailer-src"
  timeout 1800 "$ROOT/Tools/play.sh" -logFile "$OUT/autopilot.log" -ahAutopilot "$OUT/autopilot" all -ahRoute audit -ahProfile trailer-src > /dev/null 2>&1 || true
  grep -E "\[AutoPilot\] (FAIL|done)" "$OUT/autopilot.log"
fi

for reel in "${REELS[@]}"; do
  echo "== reel $reel"
  rm -rf "$DATA/profile_trailer-$reel"
  cp -r "$DATA/profile_trailer-src" "$DATA/profile_trailer-$reel"
  AH_W=1920 AH_H=1080 timeout 1200 "$ROOT/Tools/play.sh" -logFile "$OUT/$reel.log" -ahTrailer "$OUT" "$reel" -ahProfile "trailer-$reel" > /dev/null 2>&1 || true
  grep -E "\[Trailer\]|\[AutoPilot\] FAIL|Exception" "$OUT/$reel.log"
done
