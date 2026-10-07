# After Hours: improvement plan (round 1)

This was written on 6 October 2026, two days after v0.1.0 shipped. It ranks what would most improve
the game for a real player, based on the code, a fresh baseline run of the build and its automated
checks, and the README's own known issues. Nothing here is implemented yet. The last section is
the proposed scope for the next round.

## Baseline (branch `improvements`, from `main` at 7e07dc5)

| Check | Result |
|---|---|
| `Tools/unity.sh test` (EditMode) | **13/13 passed** (18 s, includes the ending-reachability search) |
| `Tools/unity.sh build-linux` | **Succeeded**, 182 MB, 0 errors |
| `Tools/autopilot.sh … all audit` | **224 passed, 0 failed**, all seven nights to the audit ending (about 5 min of game time; the other four routes weren't re-run for the baseline) |
| Perf probe (`-ahCapture … perf -ahNight 2`) | VSync off: **4.0 ms** median (p95 6.7 ms) at 1600×900; shadows off 2.6 ms, post off 3.4 ms. VSync on: **91 ms (11 fps)**, see below. Load average was about 57 from other sessions, so the numbers are noisy |
| macOS feasibility build | `-buildTarget OSXUniversal` **succeeded** in 4 min (155 MB `.app`), but the binary is **arm64 only**, and the bundle ID is the template's. Linux rebuilt cleanly afterwards; no tracked files changed |

I read through the autopilot's screenshots: title, settings, Night Select, Night 1 start, the
clipboard, a wipe, the throw arc, inspecting with the pad, the shift report and the morning chat.
Things I saw there and in the code:

- **Settings layout bug.** At 1600×900 the **Done** button covers the last row ("Reduce flashing
  and flicker") in `SettingsPanel` (`Assets/Scripts/UI/TitleScreen.cs`). There is no room for
  another option without changing the layout.
- **A dead setting.** `Settings.Quality` (0–2) is saved but nothing reads it. Render scale is the
  only graphics option, and the URP asset is set up for high-end: soft shadows, 8 shadowed
  additional lights in a 4096 atlas, SSAO, high-quality bloom and MSAA (`ProjectSetup.cs`,
  `PostFx.cs`).
- **Night 1 can trap a stuck player.** On Night 1 you can't clock out until every required task is
  done (`NightDirector.RequestClockOut`), and a task like "Bin every bit of rubbish 0/13" covers two
  rooms. The clipboard shows a count but not where the rest is. The UV torch, the only finder, comes
  on Night 2. Thrown rubbish has no recovery (`Holdable` and `TrashItem` have no out-of-bounds or
  unreachable check). One can lodged somewhere odd means you can't finish the tutorial night.
- **Night Select erases records.** Replaying a night loads that night's start-of-night snapshot as
  the live state (`GameRoot.Start`, `StoryState.LoadSnapshot`). The grades and secrets for later
  nights then disappear from Night Select, and replaying overwrites the previous result. Nothing
  records which endings you've seen, which undercuts the "four endings, replay from Night Select"
  pitch.
- **Saves aren't atomic.** `StoryState.Save`, `SaveSnapshot` and `Settings.Save` call
  `File.WriteAllText` directly. If the game is killed during a write, the save is truncated,
  `Load()` returns null and Continue silently disappears.
- **Music repetition.** Every music track is a single 12–16-bar loop of 41–60 s
  (`Tools/audio/build_music.py`). Nights 1–4 share `music_night` (53 s) and Nights 5–7 share
  `music_night_tense` (60 s). At the planned 5–8 minutes a night, the same loop plays about 30
  times over a full playthrough. Loudness is consistent (−18 to −20 LUFS integrated), so the
  problem is variety, not level.
- **Gamepad gaps.** No way to pin or cycle tools on the pad (only keys 1–4 and the wheel), no
  separate stick sensitivity (the stick reuses the mouse slider at a fixed 160°/s), and no rumble.
  No physical pad is attached to this machine, so pad behaviour can still only be exercised with
  the virtual pad.
- **Release packaging.** The Standalone bundle identifier is still the URP template's
  `com.Unity-Technologies.com.unity.template.urp-blank`. That matters for a macOS `.app`. No script
  makes the release zip; v0.1.0's zip was made by hand. `-force-wayland` appears only in
  `Tools/play.sh` and the README, not in anything a player runs.
- **VSync under native Wayland.** The perf probe's VSync-on baseline ran at 91 ms a frame, while
  the same scene with VSync off ran at 4 ms. The likeliest cause is KDE throttling frame callbacks
  for a window that isn't visible or focused (the probe ran unattended), but I couldn't confirm
  it. If it also happens in a visible window, it's the most serious bug here. The game forces
  `vSyncCount = 1` (`GameRoot.Awake`) and has no way to turn it off.
- **No app icon.** `m_BuildTargetIcons` is empty, so Linux, macOS and Windows builds all get Unity's
  default icon.
- Small things: the shift report's "Press E to see what happened in the morning" uses a plain
  letter where the rest of the UI uses keycaps. The Night 1 "shift sheet is on the clipboard"
  caption shows through at the clipboard's edge. There's no highlight on the object under the
  reticle, only the prompt, which matters in the darker rooms.

## Ranked list

Impact means for a real player. Effort: S is under half a day, M is about a day, L is several days.

| # | Improvement | Impact | Effort | Risk |
|---|---|---|---|---|
| 1 | **Never stuck on the last item**: per-room "what's left" on the shift sheet, a glint on remaining items when you look stuck, recovery for rubbish that ends up out of reach | High: the most likely way a first-time player gets stuck or quits, on the tutorial night | S–M | Low |
| 2 | **Settings v2**: check the Wayland VSync result, fix the overlap, a graphics quality preset that actually works (shadows, SSAO, bloom, MSAA), VSync or frame cap, separate stick sensitivity, vibration toggle | High for anyone not on a strong GPU, and needed before shipping to more platforms | M | Low–Med (layout, URP asset tweaks at runtime) |
| 3 | **macOS build and release packaging**: `BuildMac` (truly Universal), an app icon, a real bundle ID, a packaging script for Linux and macOS zips, a Linux launcher that picks Wayland, and a ready `BuildWindows` once the module is installed | High reach: the blog promises three platforms and the release has one | M | Med: can't be run on a Mac here; unsigned app needs a Gatekeeper workaround |
| 4 | **Records that survive replays**: profile-level best grade and secrets per night, endings seen (x/4) on the title and Night Select, atomic save writes | Med–High: makes replay and the four endings visible and rewarding; removes a data-loss path | S–M | Low |
| 5 | **Longer, varied music**: arranged night tracks with sections and variation (about 3 min), a separate calm track for Nights 3–4 | Med: 53 s loops repeat 30× a playthrough | M | Med: tuned by numbers; nobody can sign it off by ear here |
| 6 | **Playtest kit**: an opt-in local session log (time per task, time without progress, clipboard opens, hints shown, grade) and a `docs/PLAYTEST.md` script for the owner's first testers | High for the owner (the main open question is pacing and clue difficulty) but indirect for players | S | Low |
| 7 | Focus highlight: a soft rim or tint on the interactable under the reticle | Med: readability in dark rooms | S–M | Low (shader or property-block tint) |
| 8 | Pad tool cycling (d-pad left/right), light rumble on throw/clunk/finish, keyboard glyphs from the active layout (AZERTY shows the right letters) | Med for pad and non-US players | S | Med: rumble can't be felt or verified here, and Unity's Linux pad backend may ignore it |
| 9 | Small polish: keycaps in the report and chat hints, caption hidden under the clipboard, best-of grade on Night Select cards | Low–Med | S | Low |
| 10 | Key and button remapping | Med (accessibility) | L | Med: the input layer reads devices directly (`GameInput.ReadDevices`), so this means moving to Input System actions |
| 11 | Windows build | High reach | S once possible | **Blocked**: Windows Build Support isn't installed; the owner must add it in Unity Hub |
| 12 | WebGL build | Potentially large reach | L | High: Forward+, many render-texture masks, 182 MB of content, SSAO and filesystem saves; not a good fit for this game as it stands. Revisit only as a Night 1 demo |
| 13 | Hand-modelled or textured art | Med | L | Out of scope for scripted generation; stays a known limitation |

Not proposed: content additions (an eighth night, new rooms). Seven nights and four endings are
already a complete arc. Until someone has played it, polishing what exists is worth more than
adding more.

## Proposed scope for this round

Five items, in order. If time runs short, item 5 goes first.

### 1. Never stuck on the last item

- The shift sheet lists where unfinished work is, per task ("2 left · bullpen, reception").
- If the night has gone 60 s without progress while required tasks remain, or the player opens the
  shift sheet with everything nearly done, the remaining targets glint (a sparkle and a soft ping
  on rubbish, untucked chairs, monitors left on and unfinished surfaces). It never draws a waypoint
  or an arrow. Reduce flashing tones the glint down.
- Rubbish or a held item that falls below the floor, leaves the office bounds, or comes to rest
  higher than the player can reach is returned to the floor near the closest bin, with a caption.

**Acceptance:** an autopilot check throws a can onto an unreachable spot (and out of the world),
and it comes back within 5 s and can still be binned. A check idles 60 s on Night 1 with rubbish
left and sees the glint fire on exactly the unfinished targets. The existing five routes still
pass. **Verify:** `Tools/autopilot.sh` for all routes, plus a screenshot of the shift sheet's
per-room lines and of the glint.

### 2. Settings v2

- The settings panel gets a layout that fits all rows at 1280×720, 1600×900, 1920×1080 and 16:10
  (two columns or a scrolling list, keeping pad and keyboard navigation).
- **Graphics quality** Low, Medium or High, applied at runtime and saved. Low: no SSAO, hard shadows
  on the main light only, a smaller shadow atlas, no MSAA, and simple bloom. Medium sits between.
  High is today's look. Wire up the existing `Settings.Quality` field.
- **VSync** on or off, plus a frame cap (30, 60, 120, unlimited). Before that, check whether the
  91 ms VSync result also happens in a visible, focused window. If it does, fix that first.
- **Stick look sensitivity** separate from the mouse slider, and a **Vibration** toggle (rumble
  itself is item 8 and stays optional).

**Acceptance:** no row overlaps another or the Done button at those four sizes. Each preset
changes measured frame time in the perf probe in the expected direction, and Low is visibly
simpler but still reads (lit rooms, grime visible). Settings round-trip through `settings.json`.
**Verify:** screenshots at each size via `AH_W`/`AH_H`, perf probe runs per preset, and an
EditMode test for settings defaults and migration (old files without the new fields keep their
values).

### 3. macOS build and release packaging

- `BuildScript.BuildMac` with the architecture set explicitly to Intel + Apple Silicon (the
  baseline CLI build came out arm64-only), `Tools/unity.sh build-mac`, a bundle identifier such as
  `com.nearbycoder.afterhours`, and a generated app icon (from a scripted render, like the other
  assets) for every platform. The company and product names stay the
  same, so Linux save paths don't move.
- `BuildScript.BuildWindows` and `Tools/unity.sh build-windows`, which fail with a clear message
  until Windows Build Support is installed.
- `Tools/package.py` makes the release zips (keeping Unix permissions, and the `.app` executable
  bit) and a launcher, `AfterHours.sh`, for Linux. The launcher passes `-force-wayland` when a
  Wayland session is detected and falls back to X11 otherwise.
- README: platform badges, download steps per platform, and the honest status (macOS built but
  untested on hardware, unsigned, so first launch needs right-click → Open or `xattr -dr
  com.apple.quarantine`).

**Acceptance:** the macOS build succeeds in batch mode; the `.app` has the right bundle ID,
version and icon in `Info.plist`; `file` reports a universal Mach-O binary; the Linux build and
autopilot still pass after switching targets back. **Verify:** the build logs, `plutil`-style
inspection of `Info.plist` (Python's `plistlib`), `file` on the binary, and the full Linux
autopilot afterwards. **Can't be verified here:** that the Mac build launches and plays. That
needs the owner or a tester on a Mac.

### 4. Records that survive replays

- A profile-level `records.json` (next to the save) holds the best grade and the most secrets per
  night, and the endings seen. Night Select replays only add to it, never remove.
- Night Select cards show the best grade and best secrets. The title or Night Select shows "Endings
  2 / 4", with unseen endings shown as unnamed silhouettes, so no spoilers.
- Save, snapshot, settings and records writes go to a temp file and then replace the old one.

**Acceptance:** replaying Night 2 from Night Select after finishing the game keeps the Night 3–7
cards and the endings count. A save interrupted mid-write leaves the previous save loadable.
**Verify:** EditMode tests (merging records, atomic write with a simulated failure) and an
autopilot sequence that finishes two routes in one profile and checks "Endings 2 / 4" plus the
best-of grades on the Night Select screenshot.

### 5. Longer, varied night music

- `build_music.py` renders night tracks as arranged pieces of about 3 minutes (intro, A, B,
  breakdown and A′ sections with melody and drum variations) instead of one 16-bar loop. A third
  track, a "calm but uneasy" variation, covers Nights 3–4, so each part of the week sounds
  different.

**Acceptance:** each night track is at least 150 s with no identical bar-length segment repeated
back to back (checked by a correlation scan in the script). Integrated loudness stays within ±1 LU
of the current tracks. The loop point is seamless (no step at the wrap; the existing tail-wrap is
kept). **Verify:** the script's own numeric checks and the ebur128 measurements. **Can't be
verified here:** whether it sounds good. Listening is still needed, as for all the audio.

### Deferred, and why

- **Playtest kit (6)** is cheap and I'd like to do it next. It's held back only because items 1–4
  change the experience testers would be logging.
- **Rumble and pad tool cycling (8)** wait until a physical pad can be tested; the vibration toggle
  in item 2 lays the groundwork.
- **Remapping (10)** needs the input layer moved to Input System actions; it's worth its own round.
- **Windows (11)** needs the owner to install Windows Build Support in Unity Hub. After that it's
  about an hour, using the packaging from item 3.

## Round 1 outcome (6 October 2026)

All five items shipped on `improvements`, one commit each, plus this update. Verification is
listed per item. Screenshots are in [`docs/media/improvements/`](media/improvements/).

| # | Item | Status | How it was verified |
|---|---|---|---|
| 1 | Never stuck on the last item | Done | New Night 1 AutoPilot checks: a can lost out of the world, on a 2.4 m ledge and in a sealed crate comes back each time; a can on the open floor and one under a desk stay put; after 60 s idle the glint marks exactly the 35 unfinished things. All five routes pass. Screenshots of the glint and the per-room lines, and of Night 2's longer sheet still fitting the paper. |
| 2 | Settings v2 | Done | The `menus` capture finds 18 controls, 0 overlaps and 0 off screen at 1280×720, 1600×900, 1920×1080, 1680×1050 and 1440×1080. Pad checks walk from the left column into the right, step the preset with the d-pad and send a rumble pulse to the virtual pad. Perf probe (Night 2, 1600×900, VSync off, load average about 33): High 4.9 ms, Medium 2.9 ms, Low 2.7 ms median. High and Low screenshots compared. An EditMode test loads an old settings file with the new fields missing. |
| 3 | macOS build and packaging | Done, but the Mac app hasn't been run | `Tools/unity.sh build-mac` succeeds; `file` reports a universal Mach-O (x86_64 + arm64); Info.plist has `com.nearbycoder.afterhours` and the games category; the `.icns` holds the generated icon. `build-windows` stops with a clear message (module missing). `Tools/package.py` zips keep exec bits and leave out do-not-ship folders; the unpacked Linux zip starts through `AfterHours.sh` on the Wayland backend. **Not verified:** launching on a Mac. |
| 4 | Records that survive replays | Done | 8 new EditMode tests (atomic write and backup, a half-written temp file, a truncated save falling back to the backup, best-of merging, endings counted once, seeding from an old save, old settings files). The AutoPilot replays Night 2 after the ending and checks Night 7's best and the ending are still listed; `audit` then `spotless` in one profile show "Endings 2 / 4". |
| 5 | Longer night music | Done (numbers only) | `build_music.py --check`: night tracks are 160 s, 169 s (new Nights 3–4 track) and 165 s, against 53 s and 60 s before. Mean correlation between neighbouring 4-bar blocks is 0.20, 0.25 and 0.42 (0.37 and 0.72 before), with no near-repeats (max 0.70). Loop seams match the old ones; loudness is −18.3, −19.0 and −19.6 LUFS (old −18.3 and −19.8). The other tracks render bit-identical. **Not verified:** whether it sounds good. |

Final run on the last build: all five routes, 0 failed and no crashes (audit 243 checks, spotless 214, loose 239, cleanbooks 239, marian 234); EditMode tests 21/21.

What came up along the way:

- One unattended AutoPilot run crashed inside Unity's Wayland event dispatch
  (`wl_display_dispatch_queue_pending` in `UnityPlayer.so`, from the core dump), during the
  60-second idle. It's the same family as the known Wayland crash in the README. A re-run of the
  same build passed, and it wasn't seen again.
- The VSync question from the baseline is still open. The AutoPilot's own comments already put
  the 11 fps down to KDE throttling a window that isn't in front. Players can now turn VSync off
  or set a frame-rate limit, but whether a visible window holds 60 fps with VSync on wasn't
  checked.
- The trailer was not re-cut, so it still uses the old 53 s and 60 s music loops.
- The version is still 0.1.0 in ProjectSettings, so packaged zips are named v0.1.0. Bumping it
  is part of a release, which is the owner's call.

Still open from the ranked list: the playtest kit (6), focus highlight (7), pad tool cycling and
keyboard-layout glyphs (8), remapping (10), Windows (11, blocked on the module), WebGL (12).

## Round 2 scope (6 October 2026)

Round 1 is merged. This round takes the next items from the ranked list that change how the game
feels to a real player and that can be checked here: focus highlight (7), remapping (10), pad
tool cycling and layout-aware glyphs (8), and the playtest kit (6). It also picks up the
in-text hints round 1 noticed ("Press E to…" as a plain letter). Windows, signing, releases and
the trailer stay with the owner.

### R2-1. Highlight what you're aiming at

Today the only sign that something can be used is the prompt at the bottom of the screen, and in
dark rooms a small switch or a paper ball is easy to miss. The interactable under the reticle
(and anything held near its home spot) gets a soft warm rim that pulses gently: a second
renderer sharing the object's meshes with an additive fresnel shader. It's on by default, with a
toggle under Accessibility.

**Acceptance:** looking at a pickup, a light switch, a chair or a door highlights exactly that
object; looking away or picking it up removes the highlight within a frame; the toggle turns it
off. **Verify:** AutoPilot checks on Night 1 (aim at a cup and a switch, check a highlight
exists on that object only, look away, check it's gone, toggle off, check none); screenshots in
a dark room; all five routes.

### R2-2. Remappable keyboard and mouse controls

The input layer reads fixed keys (`GameInput.ReadDevices`). Bindings become data: each action
(move ×4, brisk walk, crouch, clean, spray, interact, drop, UV torch, clipboard, throw away
document) maps to a keyboard key or mouse button, saved in settings. A **Controls** panel (from
Settings) lists them; pick one and press the new key. A key already in use swaps with the
other action, Esc cancels, and there's a "Reset to defaults" button. Prompts, keycaps and in-text
hints ("Press E to see what happened in the morning") show the bound key by its name on the
current keyboard layout, so AZERTY players see the right letters too. Menus follow the movement
and interact keys as well as the arrows and Enter. Pad buttons stay fixed (see R2-3).

**Acceptance:** rebinding Interact to F through the panel makes F use things and E do nothing
(E now runs the torch, swapped); prompts show "F"; the binding survives a restart; reset brings
back the defaults; an old settings file without bindings gets the defaults. **Verify:** EditMode
tests (defaults, swap on conflict, save round trip, old file); an AutoPilot sequence that
rebinds through the real panel by sending a key press to a virtual keyboard, then presses F on a
light switch with device input live; screenshots of the panel and a prompt; all five routes.

### R2-3. Gamepad tool cycling and controller glyphs

On a pad there's no way to pin a tool (keyboard has 1–4 and the wheel). D-pad left and right
cycle the pinned tool, like the wheel. Prompts show PlayStation symbols (✕ ○ △ □, L2/R2) when
the active pad is a DualShock or DualSense, and Xbox letters otherwise.

**Acceptance:** with a virtual pad, d-pad right pins the next tool and left the previous one;
with a virtual DualShock 4 the interact prompt shows ✕ and with a generic pad it shows A.
**Verify:** AutoPilot checks with virtual `Gamepad` and `DualShock4GamepadHID` devices; a
screenshot of a PlayStation prompt. Still no physical pad, so this is virtual devices only.

### R2-4. Playtest kit

The biggest open question is pacing and clue difficulty with real players. An opt-in **playtest
log** (a Settings toggle, or `-playtest` on the command line) writes one JSON-lines file per
session, locally only: nights started and finished with times, each task ticked and secret
found with timestamps, evidence choices, glints (stuck moments), recovered items, clipboard
opens, pauses, and quitting mid-night with what was left. `Tools/playtest_report.py` turns a
folder of logs into a per-night table (time to finish, stuck moments, secrets found, the task
that finished last). `docs/PLAYTEST.md` is a short script for the owner's first testers: what to
send, what to watch for, and the questions to ask afterwards.

**Acceptance:** with the log on, a full AutoPilot route writes a log with all seven nights,
their tasks and secrets; the report script reads it and prints a sensible table; with the log
off, nothing is written. **Verify:** the AutoPilot runs one route with `-playtest` and checks
the file; the report script runs on it; EditMode test for the event format.

## Round 2 results (6 October 2026)

All four items shipped on `improvements-2`, one commit each. Screenshots and a sample report are in
[`docs/media/improvements/round2/`](media/improvements/round2/). Final build: **all five routes
pass, 0 failed, no crashes** (audit 269 checks, spotless 240, loose 265, cleanbooks 265, marian
260); EditMode tests 30/30. The intermediate commits were also compiled and tested on their own
(R2-1: 21/21, R2-2: 27/27).

| # | Item | Status | How it was verified |
|---|---|---|---|
| R2-1 | Aim highlight | Done | AutoPilot: looking at a paper ball highlights exactly that object; looking away and turning the setting off remove it; a switch in the dark closet lights up. Screenshots. |
| R2-2 | Keyboard and mouse remapping | Done | 6 EditMode tests (defaults, swap on conflict, reserved keys, stored changes and reset, save round trip, a round-1 settings file). AutoPilot rebinds Interact through the real page by pressing F on a virtual keyboard; then, reading the device itself, E no longer uses a light switch, the prompt shows F and F uses it; the binding is saved; reset restores E. Screenshots of the page, the rebind and the prompt. **Not verified:** a real AZERTY keyboard (names come from Unity's layout-aware key names). |
| R2-3 | Pad tool cycling and glyphs | Done (virtual pads) | AutoPilot with a virtual pad: d-pad right steps Cloth, Vacuum, Squeegee, Mop, automatic; left steps back. A generic pad shows A; a virtual DualShock 4 shows ✕, △ and R2, and the switch prompt shows a ✕ cap. **Not verified:** a physical pad. |
| R2-4 | Playtest kit | Done | 3 EditMode tests for the line format. AutoPilot: the log stays empty while off, then the run's file has valid JSON lines, a night end for each night in order, all 28 secrets found, the ending, clipboard opens, pauses, glints and recovered items. `Tools/playtest_report.py` read it ([sample](media/improvements/round2/r2-4-report-autopilot.md)). The Settings layout check passes at five window sizes with the new row. **Not verified:** use by a real tester. |

Things noticed along the way:

- The glyph and remapping changes also fixed the in-text hints round 1 noticed: "Press E to…",
  the UV torch toast and the Night 1 clipboard caption are now key caps that follow the bindings
  and the pad.
- The mouse wheel now uses the same cycle as the d-pad, so it can also step back to the
  automatic tool. Before, once you pinned a tool with the wheel, only pressing its number key
  again unpinned it.
- Tool keys and the wheel are ignored while a menu has the screen. Before, pressing 1–4 with
  the clipboard or a document open could pin a tool, since those don't stop time the way the
  pause menu does.
- A first `/tmp` check found 14 GB of `/tmp/ah*` scratch from the original build sessions; it
  was removed. This round kept everything under `Recordings/` and `Logs/`.

Still open from the ranked list: Windows (blocked on the module), WebGL, and art. Remapping pad
buttons is still not possible.

## Round 3 scope (6 October 2026)

Rounds 1 and 2 are merged. What's left on the ranked list is blocked (Windows), a poor fit
(WebGL) or out of reach for scripted work (art), apart from controller remapping. So this round
also takes new items, found by reading the code and the AutoPilot's screenshots from a fresh
baseline run, with one question in mind: what gets in the way of a first-time player, or
of one who can't use the default controls?

**Baseline (branch `improvements-3`, from `main` at 403bf87).** The Linux build succeeds (187 MB,
0 errors). An `audit` route run failed its real-input checks on Night 1 (rebinding with a
virtual keyboard, the pad's d-pad tool cycling, pad Y and A on documents) about 100 s in, right
after the 60-second idle, although device input had worked earlier in the same run. The
likeliest cause is that the unattended window lost focus to another session's window. The
Input System then ignores device input, and on this shared desktop that can happen at any
time. Round 2's runs presumably passed because the window kept focus. Item R3-0 fixes the test
setup before anything else.

### R3-0. AutoPilot input that doesn't depend on window focus

Automated runs (`-ahAutopilot`, `-ahCapture` and the showcase and trailer recorders) tell the
Input System to keep reading devices when the window isn't focused. Players are unaffected.

**Acceptance:** the `audit` route passes with the window unfocused. **Verify:** all five routes,
run while other sessions' windows are in front.

### R3-1. Case file: read documents again

A mystery game where you can't re-read the clues. Today the clipboard lists what's in your pocket
by title only, and once a note is put back, binned or delivered, it's gone, so the evidence for
a decision lives only in the player's memory. The clipboard gets a second page, the **case
file**: every document you've read this playthrough, grouped by night, with what became of it
(in your pocket, put back, left for Dana, shredded, thrown away, read on a screen or a wall).
Pick one to read it again in the inspect view, which only offers Close and changes nothing:
no secrets, leads or choices. A/D, the arrows or the d-pad flip between the shift sheet and the
case file; W/S, the arrows, the d-pad or the mouse wheel move through the list; E, Enter or pad
A reads. (The clipboard keeps the mouse captured, as it does today, so there's no pointer.) The story save keeps the list, so Night Select replays show what you had read by then.
Saves from before this round build the list from their evidence records.

**Acceptance:** after Night 1 the case file lists exactly the documents read, with the right fate
for each; opening one shows that document read-only, and closing it returns to the list with the
fate, secrets and leads unchanged; it works with keys and pad; Tab or Esc close the clipboard
from either page, and closing a document doesn't also close the clipboard. **Verify:** EditMode
tests (recording reads, fate labels, building the list for an old save); AutoPilot checks on
Night 2 that open the case file and read a document with pad A and with E; screenshots.

### R3-2. Hold or toggle for crouch and brisk walk

Holding C to stay under a desk, or a stick click and a bumper on a pad, is tiring and hard
for some players. Two settings, **Crouch: Hold / Toggle** and **Brisk walk: Hold / Toggle**,
go on the controls page and apply to keys and pad alike. Toggle crouch keeps the existing
headroom check (you can't stand up under a desk). Toggle brisk walk ends when you stop moving.
Defaults stay Hold.

**Acceptance:** with Toggle, one press crouches and the next stands; brisk walk stays on until
you stop; Hold behaves as before; the settings are saved; an older settings file gets Hold.
**Verify:** EditMode tests (the toggle logic, an old settings file); AutoPilot presses C on a
virtual keyboard and the stick click on a virtual pad with each mode; all five routes.

### R3-3. Controller button remapping

The deferred half of round 2's remapping. The controls page gets a **Controller** tab beside
**Keyboard and mouse**. Interact, drop, brisk walk, crouch, clean, spray, UV torch and the
clipboard can go on any face button, shoulder, trigger, stick click, d-pad up or down, or View.
A button already in use swaps over. Start (pause) and d-pad left and right (tools) stay
fixed, and so do A, B, X and Y inside menus and documents (like Esc and Enter on the keyboard).
In-game prompts show the bound button, as a PlayStation symbol on a DualShock or DualSense.

**Acceptance:** binding Interact to X through the page makes pad X use a light switch and A no
longer does; the prompt shows X (□ on a DualShock); the binding is saved and reset restores
it; Start and the d-pad's tool buttons are refused; an older settings file gets the defaults.
**Verify:** EditMode tests (defaults, swap, reserved buttons, round trip, old file); AutoPilot
rebinds through the real page with a virtual pad, then presses X on a switch; screenshots.
Still virtual pads only.

### R3-4. Pause when the window loses focus or the controller disconnects

The game keeps running in the background (it has to for capture), so alt-tabbing mid-night
leaves the clock running and the music playing, and the click that brings the window back
reaches the game as a left-click. Unplugging or losing a wireless pad leaves the player
standing in the dark. During a night, losing focus or losing the pad in use now opens the pause
menu, as long as nothing else is open apart from the clipboard (which closes). Documents
and choices stay up: they are already waiting for an answer. Automated runs keep running.

**Acceptance:** with nothing open, a focus loss or the pad being removed opens the pause menu;
with a document open, nothing changes; the title and menus are unaffected. **Verify:** AutoPilot
calls the focus handler and removes a virtual pad, checking each case. Losing focus to a real
window manager can't be tested unattended, so that path is only covered through the handler.

### R3-5. Small polish

- Dana's welcome note says "(Tab)" in its text, whatever the clipboard is bound to and even on a
  pad. Document text gets key tokens that follow the bindings and the pad, like the HUD's.
- The Night 1 "shift sheet is on the clipboard" caption shows through between the clipboard and its
  side note (visible in the baseline's `07_clipboard.png`). Opening the clipboard clears it.

**Acceptance and verify:** screenshots of the note with keys and with a pad, and of the clipboard
with no caption behind it; an AutoPilot check that the note's text shows the bound key.

Out of scope this round: saving mid-night (quitting mid-night restarts that night, five to eight
minutes at most; saving grime masks and every prop's state is a large change), larger text
(needs a layout pass over every screen), and everything left with the owner.

## Round 3 results (6 October 2026)

All six items shipped on `improvements-3`, one commit each after the scope commit. Screenshots
are in [`docs/media/improvements/round3/`](media/improvements/round3/). Final build: **all five
routes pass, 0 failed, no crashes** (audit 305 checks, loose 301, cleanbooks 301, spotless 276,
marian 296; round 2 ended at 269, 265, 265, 240 and 260). EditMode tests 43/43. Every item was
also built and run on its own (through Night 1 or Night 2) before its commit. The Settings
layout check still finds 0 overlaps and nothing off screen at 1280×720 and 1440×1080. The real
save and settings files under `~/.config/unity3d` were checksummed before and after the session
and are unchanged (all automated runs use `-ahProfile` folders).

| # | Item | Status | How it was verified |
|---|---|---|---|
| R3-0 | Focus-independent test input | Done | The baseline `audit` run had 8 failures (261 passed). With the fix, the run logs `window focused: False` and every real-input check passes; all five final routes ran while other sessions' windows were in front. This confirms the cause. |
| R3-1 | Case file | Done | 4 EditMode tests (reads recorded once with their night, fate labels, an old save seeded from evidence, a new save not re-seeded). AutoPilot on Night 2, every route: d-pad right turns the page; the list matches what was read on Night 1, with Theo's note shown as the route left it (left for Priya, kept, or left where it was); pad A reads the first entry, pad B closes it with the case file still open; fates, leads, secrets and the list are unchanged; the down arrow and E read the next one; Esc closes the document, then the clipboard, without also pausing. Screenshots. |
| R3-2 | Hold or toggle | Done | 5 EditMode tests (hold, toggle, presses ignored in menus, brisk walk ends when you stop, an old settings file). AutoPilot sets both to Toggle through the real page, then: a tap of C crouches and the next stands; the same with the pad's stick click; a tap of Shift while walking stays on and ends when movement stops; back on Hold, crouching lasts only while C is down. |
| R3-3 | Controller remapping | Done (virtual pads) | 4 EditMode tests (defaults and labels, swapping, fixed buttons refused, save, reset and a round-2 file). AutoPilot: the Controller tab lists the pad actions; picking Interact and pressing X binds it; Start cancels and can't be bound; the binding is saved; then, reading the pad itself, A no longer uses a light switch, the prompt shows X, X uses it, a DualShock shows □, and reset restores A. **Not verified:** a physical pad. |
| R3-4 | Pause on focus or pad loss | Done | AutoPilot on Night 2: the focus handler opens the pause menu with nothing open; with the clipboard open it closes and pauses; with a document open nothing changes; removing the virtual pad in use pauses; removing one nobody is using doesn't. The pause is logged in the playtest log as `auto_pause` (the report script ignores it). **Not verified:** focus taken by a real window manager, or a real wireless pad dropping out. |
| R3-5 | Small polish | Done | AutoPilot: no caption shows behind the open clipboard (the Night 1 "…iew" peeking out is gone, before and after in the screenshots); Dana's note reads "(Tab)" on the keyboard and "(View)" on a pad. |

Things fixed along the way:

- Closing the last overlay with Esc or Tab could also open the pause menu or reopen the
  clipboard on the same frame, depending on update order. Now the closing key goes no further.
- The clipboard's pocket said "Empty." while also listing the FC-2 key.
- Menu hints ("Press E to continue", the shred puzzle) keep showing the fixed menu buttons on a
  pad, while in-game prompts follow the pad bindings.

Known limits:

- With pad remapping, the clipboard can be put on d-pad down, which also moves the case file's
  selection; scrolling down then closes the clipboard. It's an odd choice of button, so it's
  allowed rather than refused.
- The case file has no pointer: the clipboard keeps the mouse captured, as before, so the wheel
  moves the selection and E reads.
- Reading a document again only shows it; the game doesn't comment on it.

Deferred: saving mid-night (a large change to grime masks and every prop's state; nights are five
to eight minutes), larger text (a layout pass over every screen), and still WebGL, Windows and
the art.

## Round 4 scope (6 October 2026)

Round 3 is merged. Apart from items that are blocked (Windows), a poor fit (WebGL) or not
scripted work (art), the ranked list is used up. So, like round 3, this round reads the code, the
settings and the screens with one question in mind: what would a first-time player on their own
screen, with their own hands, run into? A night is spent in a dark office, yet there's no
brightness setting. Holding the clean button is the whole game, and it can't be toggled. A grade
of A gives no reason why it isn't S. And one mis-click on "Quit to title" throws the night away
without asking.

**Baseline (branch `improvements-4`, from `main` at b6420b3).** The Linux build succeeds (187 MB,
0 errors), and an `audit` AutoPilot run passes (results below, under Round 4 results).

### R4-1. Brightness

Monitors and rooms differ, and most of a night is spent in the dark before the lights go on.
A **Brightness** slider goes under Display. It lifts or darkens the image through the
post-processing stack (a gamma offset, so dark areas lift more than lit ones), and the menus and
HUD aren't affected. The first time the game starts, a short brightness page appears over the
dark office on the title screen ("turn it up until you can make out the desks"), with the same
slider and a Done button. It's skipped by automated runs and never shown again once closed. It
can be opened again from Settings.

**Acceptance:** the slider changes the game image in the expected direction and leaves the UI
alone; the value is saved; an older settings file gets the default (no change in look) and sees
the page once. **Verify:** an EditMode test for the old settings file; AutoPilot checks that open
the page, move the slider with the keyboard and the pad and check the applied gamma; screenshots
at the lowest, default and highest settings, with mean luminance measured on them.

### R4-2. Hold or toggle for clean and spray

Holding the left mouse button, or the right trigger, is how every surface gets cleaned: minutes
at a time, seven nights running. Like crouch and brisk walk in round 3, **Clean and spray:
Hold / Toggle** goes on the controls page and applies to keys, mouse and pad. With Toggle, one
press starts cleaning (or spraying) and the next stops it. Picking something up, dropping it, or
opening any menu, document or the clipboard switches it off. A throw is charged with one press
and thrown with the next. Default stays Hold.

**Acceptance:** with Toggle, a tap starts cleaning a surface and it keeps getting cleaner with
the button up; a second tap stops it; opening the clipboard stops it; a throw charges on one tap
and flies on the next; Hold behaves as before; the setting is saved and an old settings file
gets Hold. **Verify:** EditMode tests (the latch, an old settings file); AutoPilot clicks a
virtual mouse once on a dirty surface and checks its completion keeps rising, then clicks again
and checks it stops; the same with the pad's trigger; all five routes.

### R4-3. The shift report says why

The report stamps S, A, B or C but never says what the grade is made of: the shift sheet (60%),
the bonus tasks (15%) and how clean every surface is (25%). An S needs every required task and a
score of 97%. The report gets one line with those three parts ("Shift sheet 6/6 · bonus 1/2 ·
surfaces 94% clean") and, below S, one short hint at what would have lifted it ("For an S: the
bonus tasks, and leave fewer surfaces half done").

**Acceptance:** the line matches the numbers the grade was computed from; an S shows no hint;
below S the hint names the parts that fell short. **Verify:** an EditMode test for the grade and
hint logic (the grading moves into a pure function the report and the night share); AutoPilot
checks the line on each night's report; screenshots of an S and a lower grade.

### R4-4. Larger HUD text

Prompts, captions, toasts and the label under the reticle are sized for a 1080p monitor at desk
distance. On a laptop at 720p or a TV across the room, 18–22 point text is small. A **Text
size** setting (Normal, Large, Largest) under Accessibility scales them, and the hints under
documents, by 1, 1.25 or 1.5. Menus, the clipboard and documents themselves keep their layout
(their text is already 28–46 points on paper sized to the screen).

**Acceptance:** at Largest, the prompt, caption, toasts and reticle label are 1.5× and stay on
screen at 1280×720 and 4:3; Normal looks as before; the setting is saved and an old file gets
Normal. **Verify:** AutoPilot sets each size and checks the HUD elements' on-screen rectangles
stay inside the screen and don't overlap the watch; screenshots at 1280×720.

### R4-5. Don't lose a night by accident

"Quit to title" in the pause menu drops the night in progress straight away, and Continue then
starts that night again from 10 PM. It now asks first ("Tonight starts over from the beginning
next time"), as Restart already does. The Settings page also gets the room it needs for the new
rows (the playtest log moves to the left column).

**Acceptance:** choosing Quit to title shows the question; Back keeps the night; confirming goes
to the title. The Settings layout check still finds no overlaps at five window sizes.
**Verify:** AutoPilot (open the question, back out with the night still running, the clock not
reset); the existing `menus` capture's layout check.

Out of scope again: saving mid-night (a night is five to eight minutes; saving every grime mask
and prop is a large change), a full layout pass for larger menu text, and the items left with
the owner. Hints for missed secrets were considered (where to look, on the report); they need a
room for every one of the 32 secrets and a decision on how much to give away, so they're noted
for the owner rather than built.

## Decisions needed from the owner

Settled by the orchestrator for this round: Windows skipped (module not installed; build entry
point ready); macOS built locally, unsigned, bundle id `com.nearbycoder.afterhours`, not
published; no web build; licence, releases, tags and signing left to the owner. Still open:

1. **Windows Build Support**: install it in Unity Hub (6000.6.2f1 → Add modules), then
   `Tools/unity.sh build-windows && python3 Tools/package.py windows`.
2. **macOS distribution**: whether to publish the unsigned universal zip with the Gatekeeper
   steps from its README, or pay for an Apple Developer account to sign and notarise it. Either
   way, someone should launch it on a real Mac first.
3. **Version and release**: ProjectSettings still says 0.1.0. A new release means bumping it,
   rebuilding and running `Tools/package.py`, then publishing (and maybe re-cutting the trailer
   with the new music).
4. **Licence**: none has been chosen yet.
5. **Playtests**: the kit is ready (`docs/PLAYTEST.md`). Choosing testers, and handing them a build
   (an unreleased one, or a new release), is the owner's call.
6. **A physical controller**: rumble, PlayStation symbols, pad remapping and the pause on a pad
   dropping out have only met virtual devices. Ten minutes with a real Xbox and DualSense pad
   would settle it.
