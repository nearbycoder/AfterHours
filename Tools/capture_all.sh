#!/usr/bin/env bash
# Run the per-night tour capture for every night (no rebuild): Tools/capture_all.sh [outdir]
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-/tmp/ah-tour}"
rm -rf "$OUT"; mkdir -p "$OUT"
for n in 1 2 3 4 5 6 7; do
  mkdir -p "$OUT/n$n"
  timeout 300 "$ROOT/Tools/play.sh" -logFile "$OUT/n$n/player.log" -ahCapture "$OUT/n$n" tour -ahProfile capture -ahFresh -ahNight $n > /dev/null 2>&1 || true
  grep -E "\[Capture\]|\[Night\]|Exception" "$OUT/n$n/player.log" | grep -v "shot /" | head -30
done
