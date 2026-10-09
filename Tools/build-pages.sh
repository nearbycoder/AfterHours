#!/usr/bin/env bash
# Builds the browser version for GitHub Pages into Builds/Pages (gitignored): index.html at the
# root, Build/*.unityweb (Brotli, unpacked by the loader itself, so no server headers are needed),
# TemplateData/ and .nojekyll. Every path is relative, so the folder works under /AfterHours/.
#   Tools/build-pages.sh            build, then list the files and sizes
# The editor log goes to Logs/pages-build.log. Test the result with Tools/check-pages.mjs.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SITE="$ROOT/Builds/Pages"
LOG="$ROOT/Logs/pages-build.log"
mkdir -p "$ROOT/Logs"

rm -rf "$SITE"
echo "building the web player (log: $LOG)…"
start=$(date +%s)
if ! nice -n 10 "$ROOT/Tools/unity.sh" build-webgl > "$LOG" 2>&1; then
  grep -E "error|Error|\[AfterHours\]" "$LOG" | tail -n 30 >&2
  echo "build failed (see $LOG)" >&2
  exit 1
fi
grep -E "\[AfterHours\]" "$LOG" || true
[ -f "$SITE/index.html" ] || { echo "no index.html in $SITE" >&2; exit 1; }
touch "$SITE/.nojekyll"
echo "built in $(( $(date +%s) - start )) s"

# GitHub refuses files over 100 MB and warns over 50 MB.
status=0
while IFS= read -r -d '' f; do
  size=$(stat -c %s "$f")
  if [ "$size" -ge $((100 * 1024 * 1024)) ]; then echo "TOO BIG for GitHub (100 MB): ${f#"$SITE"/}" >&2; status=1
  elif [ "$size" -ge $((50 * 1024 * 1024)) ]; then echo "warning: over 50 MB: ${f#"$SITE"/}" >&2; fi
done < <(find "$SITE" -type f -print0)
(cd "$SITE" && find . -type f -printf '%s\t%p\n' | sort -rn | awk -F'\t' '{ printf "%8.2f MB  %s\n", $1 / 1048576, $2 }')
echo "total: $(du -sh --apparent-size "$SITE" | cut -f1)"
exit $status
