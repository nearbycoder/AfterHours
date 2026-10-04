#!/usr/bin/env bash
# Self-test: the built game plays all seven nights from the title to the ending through real
# components, saves screenshots to ${1:-/tmp/ah-autopilot} and prints PASS/FAIL lines.
#   Tools/autopilot.sh [outdir] [nightN]    nightN stops after that night (quick iteration)
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-/tmp/ah-autopilot}"
SCEN="${2:-all}"
rm -rf "$OUT"; mkdir -p "$OUT"
timeout 1800 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -ahAutopilot "$OUT" "$SCEN" -ahProfile autopilot > /dev/null 2>&1 || true
grep -E "\[AutoPilot\] (PASS|FAIL|done|started)|Exception" "$OUT/player.log"
! grep -qE "\[AutoPilot\] FAIL|Exception" "$OUT/player.log" && grep -q "\[AutoPilot\] done" "$OUT/player.log"
