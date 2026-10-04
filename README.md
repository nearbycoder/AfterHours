# After Hours

> You're the new night cleaner at a small freight company on the 14th floor, and over seven
> nights the grime you scrub away shows what the day shift is hiding. You decide which evidence
> survives until morning.

A first-person cleaning game with a mystery underneath. Wipe desks, squeegee glass, vacuum,
mop, bin the rubbish and put the office back the way it was. Along the way you find what the
day shift left behind: crumpled notes, ghost writing under a whiteboard, finger-writing that
only shows through window foam, invisible-ink arrows under a UV torch, a notepad that gives
up its last page to a pencil rubbing, a bag of shredded invoices. Every piece of evidence is
yours to keep, put back, bin, shred, or deliver to someone's inbox tray, and the office
remembers what you did.

Built with Unity 6000.6.2f1 (URP). Every 3D model is generated in Blender 4.5 with `bpy`, and
every sound, music track and texture is synthesised by scripts in this repository.

- Design and technical plan: [`docs/PLAN.md`](docs/PLAN.md)
- The original brief: [`docs/BRIEF.md`](docs/BRIEF.md)

## Running it

```bash
Tools/play.sh                 # runs Builds/Linux/AfterHours.x86_64 windowed at 1600x900
Builds/Linux/AfterHours.x86_64  # or launch the player directly
```

`play.sh` passes `-force-wayland` when a Wayland session is present, because XWayland hangs at
start-up on the development machine.

## Controls

| Keyboard and mouse | Gamepad | Action |
|---|---|---|
| WASD, mouse | left stick, right stick | move, look |
| Left Shift | right bumper | brisk walk |
| C or Left Ctrl | left stick click | crouch (reach under desks) |
| Left mouse (hold) | right trigger | clean with the current tool; with something in hand, hold to charge a throw |
| Right mouse | left trigger | spray (squeegee and cloth) |
| E | A / south | interact: pick up, place, tuck a chair, switch, door, monitor, tray, read |
| Q | B / east | drop what you're holding |
| F | d-pad up | UV torch (from Night 2) |
| Tab | select | clipboard: tonight's tasks, secrets, leads and what's in your pocket |
| 1 to 4, mouse wheel | | pin a tool (otherwise the right tool is picked for the surface) |
| Esc | start | pause |

In the inspect view: **Tab** (pad **Y**) keeps a document, **E** (pad **A**) puts it back, **X**
(pad **X**) throws it away. On a monitor, **Q** (pad **X**) switches it off.

Menus, choices, the shred puzzle and the shift report work with W/S/A/D or the arrow keys, E or
Enter to confirm and Esc to go back; on a pad, the d-pad or left stick, **A** and **B**. Prompts
switch between keyboard keys and pad buttons depending on what you touched last.

## How it plays

- **A night** starts in the cleaning closet with your cart. The clipboard lists tonight's shift
  sheet, room by room. The wristwatch runs from 10 PM towards dawn; there's no fail timer.
- **Smart tools.** Look at a dirty surface and the right tool comes up: cloth for desks,
  squeegee for glass (spray first, then pull), vacuum for carpet, mop for hard floors. The
  reticle becomes a progress ring and surfaces finish themselves with a gleam once they're
  nearly clean.
- **Rubbish** goes in the matching bin: food and wrappers in black, cans and bottles in blue
  recycling, paper in either. Wrong bins bounce it back out. Throws charge while you hold the
  button and show an arc.
- **Putting things back.** Moved items have home spots; a ghost shows where something belongs
  and E snaps it home. Chairs tuck in and monitors switch off. Lights go off when you leave.
- **Evidence.** Story documents open in an inspect view. Keep them in your pocket, put them
  back, or throw them away; later, deliver them to someone's inbox tray, feed them to a
  shredder, or leave a sticky note made from the leads you've learned.
- **Suspicion.** In Marian's office, anything you move and don't return, and anything that goes
  missing, is noticed. It changes her notes and your epilogue, never your ability to finish.
- **Clocking out** at the punch clock ends the night with a shift report (grade S to C, secrets
  found, before-and-after polaroids of the rooms you cleaned), then the next morning's office
  chat, which reacts to what you did.

## Content

One office (Suite 1408: reception, bullpen, break room, conference room, Marian's corner office,
and the cleaning closet) that changes over seven nights:

1. **Monday, "First Shift"**: the tutorial night. A crumpled note under Theo's desk, a brass key
   the vacuum knocks loose, and a monitor that wakes up by itself at 1 AM.
2. **Tuesday, "Glass"**: squeegee and mop; foam on the break-room window shows finger-writing;
   Walt's locker gives you a UV torch.
3. **Wednesday, "The Whiteboard"**: erasing the brainstorm reveals the ghost diagram underneath.
4. **Thursday, "The Corner Office"**: a pencil rubbing, a locked cabinet, $50 in an envelope and a
   shredder bag Marian wants gone.
5. **Friday, "Pieces"**: the big post-party mess and a shred-strip reconstruction puzzle.
6. **Sunday, "Prep"**: the auditor's tray appears, plus boxes marked for destruction and a
   voicemail.
7. **Monday, "Audit Eve"**: a storm, flickering power, and the shredder in Marian's office jammed
   on a red folder.

There are four endings: **The Audit**, **Clean Books**, **Loose Threads** and the secret
**Spotless**, each with personal epilogue variations. Night Select replays any reached night from
its saved start state.

Menus: title (Continue, New Game, Night Select, Settings, Quit), pause (Resume, Shift sheet,
Settings, Restart this night, Quit to title) and settings (mouse sensitivity, FOV, four volume
sliders, render scale, invert Y, head bob, fullscreen, captions, reduce flashing). Progress and
settings save automatically.

## Project layout

```
Assets/
  Scripts/            runtime code, one assembly (AfterHours.asmdef)
    Cleaning/         grime surfaces (CPU masks, exact completion), brushes, tools
    Core/             GameRoot boot and flow, input, settings, tweening, events
    Interaction/      hands (pick up, throw), bins, home spots, doors, switches, story items
    Story/            night definitions, documents, endings, story state and saving, director
    World/            office builder (reads Office.fbx), furnisher, prop library
    UI/               HUD, clipboard, inspect view, menus, shift report, chat, ending
    FX/, Audio/       particles, post-processing, room photos; sound playback and music
    Player/           first-person controller, UV torch
    Testing/          CaptureDirector (scripted screenshots), AutoPilot (self-test)
  Editor/             BuildScript, ProjectSetup, import rules, EditMode tests
  Shaders/            Grime overlay, UV ink, skyline, particles
  Resources/          generated models, textures, audio, fonts
ArtSource/            Blender generators (office.py, props.py, furniture.py, tools.py) and .blend files
Tools/                build, play, capture and test scripts; texture and audio generators
docs/                 brief and plan
```

There's one scene; `GameRoot` builds the office from the FBX and runs everything from code.

## Rebuilding

The editor needs the old `libxml2.so.2` on this distro. `Tools/unity.sh` points
`LD_LIBRARY_PATH` at an extracted copy in `~/.local/share/ptt-unity-libs` (override with
`AH_UNITY_LIBS`), or install `libxml2-legacy`.

```bash
# Models (headless Blender), written to Assets/Resources/Models/
blender -b -P ArtSource/office.py -- [--render DIR]
blender -b -P ArtSource/props.py  -- [--render DIR] [--only a,b]

# Textures, sound effects and music (Python venv with numpy, scipy, Pillow)
python -m venv Tools/.venv && Tools/.venv/bin/pip install numpy scipy pillow
Tools/.venv/bin/python Tools/gen_textures.py
Tools/.venv/bin/python Tools/audio/build_sfx.py
Tools/.venv/bin/python Tools/audio/build_music.py

# Unity
Tools/unity.sh build-linux    # Builds/Linux/AfterHours.x86_64
Tools/unity.sh test           # EditMode tests -> Logs/test-results.xml
Tools/unity.sh                # open the project in the editor
```

## Verification

- **AutoPilot** (`Tools/autopilot.sh [outdir] [nightN|all] [route]`): the built player starts at the title
  and plays all seven nights through the real game components. It wipes surfaces with the real
  brush maths, drops rubbish into bins with physics, uses real mouse-and-key input for a wipe, a
  pick-up, a charged throw and the vacuum, reads and keeps evidence, delivers it to trays, solves
  the shred puzzle, switches the lights off, clocks out, and checks that the chosen route reaches
  its ending and returns to the title. The routes are `audit` (default; everything goes to the
  auditor), `loose` (keep the red folder), `cleanbooks` (bin it), `spotless` (read everything,
  keep nothing) and `marian` (hoard the evidence, hand it to Marian once her office opens, take
  her money, shred Priya's log, leave the auditor an anonymous sticky note, give Marian the red
  folder: Clean Books by a different road). Every route also plugs in a virtual gamepad and drives the title
  menu, Settings, the document reader and a tray choice with real pad button events. It saves
  screenshots and prints PASS/FAIL lines.
- **EditMode tests** (`Tools/unity.sh test`): an exhaustive search of the choice space proves all
  four endings are reachable, plus specific routes to each, data consistency between documents
  and nights, and grime pattern generation.
- **Captures** (`Tools/build_and_capture.sh`, `Tools/capture_all.sh`): scripted screenshot tours
  of each night.
- **Perf probe** (`Tools/play.sh -ahCapture DIR perf -ahNight 2 -ahFresh`): stands in the bullpen
  and times frames with vsync off, then with grime, post-processing, shadows, MSAA and render
  scale switched off one at a time.
- **Showcase** (`Tools/play.sh -ahShowcase DIR [nightN]`): plays the AutoPilot route on camera,
  walking up to things and holding documents open, and writes every frame (fixed 30 fps clock)
  plus the game's own audio to DIR for a gameplay video.

Latest results on the shipped build, all seven nights each: **audit 224, loose 220, cleanbooks
220, spotless 195, marian 215 checks passed, 0 failed**, each reaching its expected ending (the
checks include no props overlapping, sunk into furniture or floating, on every night); EditMode tests **13 of
13** passing.

Performance (1600x900, AMD Radeon 8060S iGPU, OpenGL Core, vsync off): the perf probe renders the
bullpen in about **3 ms a frame** (around 340 fps). Turning off any single feature saves under
0.5 ms. Across the AutoPilot's seven nights the median frame is 4-8 ms and the 95th percentile
6-14 ms. The worst frames, 60-140 ms, come from night set-up (grime patterns are generated while
the title card is up) and the AutoPilot's bulk brush maths. With vsync on, the game runs at the
monitor's refresh rate. One trap to know about: Wayland throttles vsync for windows that aren't
visible, so a test window left behind others runs at 11-20 fps. The AutoPilot therefore runs
uncapped.

The AutoPilot found and fixed these bugs in the game itself:

- Story objects from one night leaked into the next. The title screen's backdrop sets up a night
  in the background, so a new game's Night 1 could start with that night's note in Walt's locker,
  a stray pencil-rubbing surface on Marian's desk, and old UV marks. Night scripts now parent
  everything to the night root, and story behaviour added to permanent furniture is removed
  when a night ends.
- The camera-kick spring went unstable on long frames (night loads, screenshots) and could leave
  the camera upside down. It now sub-steps at a fixed rate and resets on teleport.
- Following Marian's instruction to send her shredder bag down the chute counted as tampering
  with evidence, which made the Spotless ending unreachable for a player who just did as they
  were told. Doing what you're asked no longer counts.
- Marian's reactions to things left in her tray (Theo's note, Russ's betting slip, Theo's
  planner) were attached to Nights 1-3, when her office is locked, so they could never appear.
  They now follow whichever night you actually reach her tray.
- Menus, choices and screens only read the keyboard, so a gamepad player got stuck at the first
  tray or document. All of them now take pad input.
- The story calendar contradicted itself: the epilogue had the auditor reading the tray on
  Monday morning, before Night 7's Monday shift, and a ledger entry was dated a week after it
  is found. The audit is now Tuesday the 15th, and every document, chat line, lock screen and
  epilogue agrees with that calendar. Clean Books no longer vindicates Walt.
- `Interstitial` screens (report, chat, title card, ending) destroyed their UI but never their own
  object. Leftovers piled up, and a static "a screen is open" flag raced between the chat closing
  and the next title card opening.
- The throw-arc preview was invisible (its shader faded everything past the first few
  centimetres). It now draws as a soft dashed line.
- Bins only caught items below the rim, so fast flat throws clipped the edge and bounced out.
  The catch zone now reaches slightly above the rim.

## Known limitations

- **Five routes, not every branch.** The AutoPilot plays one route to each ending plus a
  Marian-sided one. Other mixes (for example notes to Theo, or giving Dana things) are only covered
  by the EditMode sweep of the ending logic.
- **A monitor change can crash the game on Wayland.** During testing, a KDE display re-detection
  (a monitor powering down or reconnecting) crashed two running copies at the same instant inside
  Unity's Wayland window code (`wl_display_dispatch_queue_pending`). It's an engine/platform issue
  rather than game code, and `Tools/play.sh` uses the Wayland backend because XWayland hung at
  startup on this machine.
- **The AutoPilot takes shortcuts.** It teleports between rooms, triggers most interactions
  directly, and cleans most surfaces by calling the brush maths rather than moving the mouse.
  Real mouse-and-key input is exercised for one wipe, the pick-up, a charged throw and the
  vacuum. No human has played the full game, so pacing, difficulty and the 12-minute clock are
  untested with real players.
- **Audio was never heard.** Every sound and music track was checked numerically (level, crest
  factor, rhythm, loop seams), not by ear.
- **No physical gamepad was tested.** Pad support is verified with a virtual Input System gamepad
  (real state events, same code path), but not on hardware, and there is no rumble or remapping.
- **Performance was measured on one machine only** (32-core Strix Halo with its integrated GPU;
  numbers above). Lower-end hardware is untested, and there are no quality presets; the only
  graphics option is render scale in Settings.
- **The art is stylised and procedural.** Every model is built from code in Blender with chunky
  bevelled shapes and flat materials. It's cohesive, but it isn't hand-modelled or textured to a
  commercial standard.

## Credits and licences

Fonts are bundled with their licences in `Assets/Fonts-Licenses/`: Fira Sans, Caveat, Courier
Prime, Patrick Hand, Reenie Beanie (SIL OFL), Permanent Marker and Special Elite (Apache 2.0),
DejaVu Sans (Bitstream Vera licence). Everything else (models, textures, audio, music, code) was
made for this project.
