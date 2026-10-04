#!/usr/bin/env bash
# Rebuild the Linux player and run a capture scenario: Tools/build_and_capture.sh [scenario] [outdir]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCEN="${1:-proto}"
OUT="${2:-/tmp/ah-shots}"
mkdir -p "$ROOT/Logs"
if ! "$ROOT/Tools/unity.sh" build-linux > "$ROOT/Logs/build.log" 2>&1; then
  grep -E "error CS|Error|error:" "$ROOT/Logs/build.log" | head -30
  echo "BUILD FAILED"; exit 1
fi
grep -E "\[AfterHours\]" "$ROOT/Logs/build.log"
rm -rf "$OUT"; mkdir -p "$OUT"
timeout 600 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -ahCapture "$OUT" "$SCEN" > /dev/null 2>&1 || true
grep -E "\[Capture\]|Exception|NullReference" "$OUT/player.log" | grep -v "shot /" || true
