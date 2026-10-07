#!/usr/bin/env bash
# Tests the game against a real window manager without touching the desktop you're using: it runs
# a private KWin on a virtual screen (its own D-Bus session and config folders; the game's saves
# and settings go there too), starts the built player inside it with a `-ahCapture` scenario, and
# drives KWin whenever the game's log says "ready".
#
#   Tools/wmtest.sh [outdir] [title|night|window|perf] [wayland|x11]
#   Tools/wmtest.sh outdir run [wayland|x11] -- <player args>
#     title   closes the window on the title (as the title bar's close button or Alt+F4 does):
#             the game must exit
#     night   closes it on Night 1: held behind the question; Never mind keeps the night paused
#             where it was; a second close asks again; Quit the game must end the process
#     window  started fullscreen, Settings switches Fullscreen off, on and off again; KWin
#             reports where the window is each time (it must fit on the screen)
#     perf    the perf probe on Night 2 (VSync on first, then uncapped) in a window the
#             compositor is showing, unlike the desktop's background windows
#     run     starts the player with the given arguments in a window (AH_W x AH_H, default
#             1600x900) and waits for it to exit (at most AH_RUN_TIMEOUT seconds, default 1800);
#             Tools/autopilot.sh uses it. AH_WM_XDG keeps the config folders (and so the game's
#             saves) somewhere other than outdir, for runs that share a profile
#     x11     runs the player on the private KWin's Xwayland instead of native Wayland
#   AH_SCREEN=WxH sets the virtual screen (default 1600x900; 1920x1080 for run).
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME="$ROOT/Builds/Linux/AfterHours.x86_64"
OUT="${1:-$ROOT/Recordings/wmtest}"
WHAT="${2:-night}"
BACKEND="${3:-wayland}"
shift $(( $# < 3 ? $# : 3 ))
[ "${1:-}" = "--" ] && shift
EXTRA=("$@")
SCREEN="${AH_SCREEN:-1600x900}"
[ "$WHAT" = run ] && SCREEN="${AH_SCREEN:-1920x1080}"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }

if [ -z "${AH_WMTEST_RUN:-}" ]; then
  mkdir -p "$OUT" && OUT="$(cd "$OUT" && pwd)"
  rm -rf "$OUT"; mkdir -p "$OUT"
  XDG="${AH_WM_XDG:-$OUT}"
  mkdir -p "$XDG"/{config,data,cache} && XDG="$(cd "$XDG" && pwd)"
  # Everything the nested KWin, the services its private bus starts and the game write goes under
  # $OUT (or AH_WM_XDG), and none of them can reach the desktop's displays.
  export XDG_CONFIG_HOME="$XDG/config" XDG_DATA_HOME="$XDG/data" XDG_CACHE_HOME="$XDG/cache"
  unset WAYLAND_DISPLAY DISPLAY
  # A private session bus: the nested KWin registers as org.kde.KWin there, not on the desktop's.
  # AH_WMTEST_RUN marks every process of this run, for the clean-up below.
  AH_WMTEST_RUN="$$-$RANDOM" exec dbus-run-session -- "$0" "$OUT" "$WHAT" "$BACKEND" -- "${EXTRA[@]}"
fi
# Services the private bus starts (portals, say) must talk to the private KWin: without
# WAYLAND_DISPLAY they'd try the default "wayland-0", the desktop's.
dbus-update-activation-environment WAYLAND_DISPLAY="ah-wmtest-$$" DISPLAY= 2> /dev/null
LOG="$OUT/player.log"
RESULT="$OUT/result.txt"
: > "$RESULT"
say() { echo "$*" | tee -a "$RESULT"; }

# KWin starts the game (so it gets the nested Wayland and Xwayland displays); the launcher records
# the game's PID before turning into the player.
case "$WHAT" in
  title) scen=(closetest) ;;
  night) scen=(closetest -ahNight 1 -ahFresh) ;;
  window) scen=(windowtest) ;;
  perf) scen=(perf -ahNight 2 -ahFresh) ;;
  run) scen=() ;;
  *) echo "unknown test $WHAT" >&2; exit 2 ;;
esac
win=(-screen-fullscreen 0 -screen-width 1280 -screen-height 720)
[ "$WHAT" = window ] && win=(-screen-fullscreen 1)
game_args=(-logFile "$LOG" -ahCapture "$OUT" "${scen[@]}" -ahProfile wmtest)
if [ "$WHAT" = run ]; then
  win=(-screen-fullscreen 0 -screen-width "${AH_W:-1600}" -screen-height "${AH_H:-900}")
  game_args=("${EXTRA[@]}")
fi
cat > "$OUT/launch.sh" <<EOF
#!/usr/bin/env bash
echo \$\$ > "$OUT/game.pid"
echo "WAYLAND_DISPLAY=\$WAYLAND_DISPLAY DISPLAY=\${DISPLAY:-}" > "$OUT/game.env"
args=(${win[*]} $(printf '%q ' "${game_args[@]}"))
[ "$BACKEND" = wayland ] && args+=(-force-wayland)
exec "$GAME" "\${args[@]}"
EOF
chmod +x "$OUT/launch.sh"
kw_args=(--virtual --socket "ah-wmtest-$$" --width "${SCREEN%x*}" --height "${SCREEN#*x}" --no-lockscreen --no-global-shortcuts --no-kactivities)
[ "$BACKEND" = x11 ] && kw_args+=(--xwayland)
kwin_wayland "${kw_args[@]}" "$OUT/launch.sh" > "$OUT/kwin.log" 2>&1 &
KWIN=$!

cleanup() {
  [ -f "$OUT/game.pid" ] && kill -0 "$(cat "$OUT/game.pid")" 2>/dev/null && kill "$(cat "$OUT/game.pid")"
  kill "$KWIN" 2>/dev/null
  wait "$KWIN" 2>/dev/null
  # Services the private bus started (portals, Xwayland's helpers) outlive it; stop this run's.
  local p
  for p in $(grep -lzx "AH_WMTEST_RUN=$AH_WMTEST_RUN" /proc/[0-9]*/environ 2>/dev/null | cut -d/ -f3); do
    [ "$p" != $$ ] && [ "$p" != "$PPID" ] && kill "$p" 2>/dev/null && echo "stopped leftover $p $(cat /proc/$p/comm 2>/dev/null)" >> "$OUT/cleanup.txt"
  done
}
trap cleanup EXIT

wait_for() { # pattern, seconds
  local end=$((SECONDS + $2))
  while [ $SECONDS -lt $end ]; do
    grep -q "$1" "$LOG" 2>/dev/null && return 0
    sleep 0.5
  done
  return 1
}
game_alive() { [ -f "$OUT/game.pid" ] && kill -0 "$(cat "$OUT/game.pid")" 2>/dev/null; }
wait_exit() { # seconds
  local end=$((SECONDS + $1))
  while [ $SECONDS -lt $end ]; do game_alive || return 0; sleep 0.25; done
  return 1
}

# Runs a KWin script; what it prints lands in the journal under the nested KWin's PID.
n_js=0
kwin_js() {
  n_js=$((n_js + 1))
  local js="$OUT/script$n_js.js" id
  printf '%s\n' "$1" > "$js"
  id=$(qdbus6 org.kde.KWin /Scripting org.kde.kwin.Scripting.loadScript "$js" "wmtest$n_js")
  qdbus6 org.kde.KWin "/Scripting/Script$id" org.kde.kwin.Script.run > /dev/null
  sleep 0.5
  qdbus6 org.kde.KWin /Scripting org.kde.kwin.Scripting.unloadScript "wmtest$n_js" > /dev/null
}
kwin_said() { journalctl --user _PID="$KWIN" -o cat 2>/dev/null | grep "wmtest:" ; }

close_window() { # what the title bar's close button does
  kwin_js 'for (const w of workspace.windowList()) if (w.normalWindow) { print("wmtest: closing " + w.caption + " (pid " + w.pid + ")"); w.closeWindow(); }'
  say "close sent from the window manager at $(date +%T)"
}

report_window() { # tag
  kwin_js "const s = workspace.virtualScreenSize; for (const w of workspace.windowList()) if (w.normalWindow) { const f = w.frameGeometry, c = w.clientGeometry; print('wmtest: $1 frame ' + f.x + ',' + f.y + ' ' + f.width + 'x' + f.height + ' client ' + c.x + ',' + c.y + ' ' + c.width + 'x' + c.height + ' screen ' + s.width + 'x' + s.height + ' fullScreen ' + w.fullScreen + ' maximized ' + (w.maximizeMode !== undefined ? w.maximizeMode : '?') + ' decorated ' + !w.noBorder); }"
  sleep 0.5
  local line; line=$(kwin_said | grep "wmtest: $1 " | tail -1)
  say "${line#wmtest: }"
  # Fits: the whole frame (title bar included) is on the screen.
  read -r fx fy fw fh sw sh < <(echo "$line" | sed -E 's/.*frame (-?[0-9.]+),(-?[0-9.]+) ([0-9.]+)x([0-9.]+) .* screen ([0-9]+)x([0-9]+).*/\1 \2 \3 \4 \5 \6/')
  if [ -n "${fx:-}" ] && awk -v x="$fx" -v y="$fy" -v w="$fw" -v h="$fh" -v W="$sw" -v H="$sh" 'BEGIN { exit !(x >= 0 && y >= 0 && x + w <= W + 0.5 && y + h <= H + 0.5) }'; then
    say "  fits on the screen"
  else
    say "  DOESN'T FIT on the screen"
  fi
}

say "wmtest: $WHAT, $BACKEND, screen $SCREEN, $(uptime | sed 's/.*load/load/')"
if [ "$WHAT" = run ]; then
  # Just the player: wait for it to start, then for it to finish.
  end=$((SECONDS + 60)); while [ $SECONDS -lt $end ] && ! game_alive; do sleep 0.5; done
  game_alive || { say "FAIL: the player never started"; exit 1; }
  cat "$OUT/game.env" | tee -a "$RESULT"
  if wait_exit "${AH_RUN_TIMEOUT:-1800}"; then say "player exited at $(date +%T)"; else say "FAIL: still running after ${AH_RUN_TIMEOUT:-1800} s, stopped"; exit 1; fi
  exit 0
fi
ready="ready 1"; [ "$WHAT" = perf ] && ready="scenario perf"
if ! wait_for "$ready" 120; then say "FAIL: the game never got ready"; exit 1; fi
cat "$OUT/game.env" | tee -a "$RESULT"
case "$WHAT" in
  title)
    close_window
    if wait_exit 10; then say "PASS: on the title the window close goes through (the game exited)"; else say "FAIL: the game is still running 10 s after the close"; fi
    ;;
  night)
    close_window
    if wait_for "closetest held" 20 && grep -q "closetest held: question True, pause menu True, time scale 0, night running True" "$LOG" && game_alive; then
      say "PASS: mid-night the close is held: the game runs on, paused, with the question showing"
    else
      say "FAIL: the close wasn't held ($(game_alive && echo running || echo exited))"
    fi
    if wait_for "closetest ready 2" 20 && grep -q "closetest never mind: question False, pause menu True, night running True, clock moved 0.00s" "$LOG"; then
      say "PASS: Never mind leaves the night paused where it was"
    else
      say "FAIL: Never mind didn't leave the night paused where it was"
    fi
    close_window
    if wait_for "closetest asked again: question True" 20; then say "PASS: a second close asks again"; else say "FAIL: a second close didn't ask"; fi
    if wait_exit 15; then say "PASS: Quit the game closes the game"; else say "FAIL: still running 15 s after Quit the game"; fi
    ;;
  window)
    report_window started
    for i in 2 3 4; do
      wait_for "windowtest ready $i" 30 || { say "FAIL: no step $i"; break; }
      report_window "step$i"
    done
    if grep -qE "ready 3: fullscreen again FullScreenWindow ([0-9]+x[0-9]+), display \1" "$LOG"; then
      say "PASS: fullscreen again renders at the screen's resolution"
    else
      say "FAIL: fullscreen again isn't at the screen's resolution"
    fi
    wait_exit 15
    ;;
  perf)
    wait_exit 120 || say "FAIL: the perf probe didn't finish"
    grep "\[Capture\] perf" "$LOG" | tee -a "$RESULT"
    ;;
esac
grep -E "\[Quit\]|\[Capture\] (closetest|windowtest)|Exception" "$LOG" | tee -a "$RESULT"
kwin_said | grep "closing" | tee -a "$RESULT"
! grep -qE "FAIL|DOESN'T FIT" "$RESULT"
