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

## Decisions needed from the owner

1. **Windows Build Support**: install it in Unity Hub (6000.6.2f1 → Add modules) if you want a
   Windows build. Until then only the build method and packaging are prepared.
2. **macOS distribution**: an unsigned, unnotarised `.app` is fine for a GitHub release, but players
   have to bypass Gatekeeper. Signing and notarisation need an Apple Developer account
   ($99/year). Ship unsigned with instructions, or skip macOS?
3. **Bundle ID**: `com.nearbycoder.afterhours` unless you prefer another.
4. **Licence**: the README says none has been chosen. That's outside this round, but it matters for
   a wider release.
