#!/usr/bin/env bash
# Rebuild the Linux player and run a capture scenario:
#   Tools/build_and_capture.sh [scenario] [outdir] [--no-build] [extra player args...]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCEN="${1:-proto}"; shift || true
OUT="${1:-/tmp/ah-shots}"; shift || true
BUILD=1
if [ "${1:-}" = "--no-build" ]; then BUILD=0; shift; fi
mkdir -p "$ROOT/Logs"
if [ $BUILD = 1 ]; then
  if ! "$ROOT/Tools/unity.sh" build-linux > "$ROOT/Logs/build.log" 2>&1; then
    grep -E "error CS|Error|error:" "$ROOT/Logs/build.log" | head -30
    echo "BUILD FAILED"; exit 1
  fi
  grep -E "\[AfterHours\]" "$ROOT/Logs/build.log"
fi
EXTRA=(-ahProfile capture)
case "$SCEN" in
  proto) EXTRA+=(-ahScene proto) ;;
  night[1-7]) EXTRA+=(-ahFresh -ahNight "${SCEN#night}") ;;
esac
rm -rf "$OUT"; mkdir -p "$OUT"
timeout 900 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -ahCapture "$OUT" "$SCEN" "${EXTRA[@]}" "$@" > /dev/null 2>&1 || true
grep -E "\[Capture\]|\[Night\]|Exception|NullReference" "$OUT/player.log" | grep -v "shot /" || true
