# Build this game in Unity: After Hours

The designer framing for this whole batch: each game should have **one mechanic that feels great immediately**, a premise you can explain in a sentence, and enough depth to make players say "one more run." It should feel like a complete indie game, and the first version must stay manageable in scope. The real test: does a five-minute play make someone ask to play again?

## The concept (from the designer)
**Clean an office while learning its secrets, and decide which evidence survives.**

Vacuum, wash windows, sort trash, and reset desks. Environmental clues reveal an unfolding workplace mystery. Your cleaning choices affect the next shift: shred a document, leave a note, or put something where the right person will find it.

- **The fun:** Satisfying cleaning paired with curiosity about what happened.
- **The polish:** Crisp before-and-after transformations, expressive clutter, and late-night ambience.
- **Buildable first version:** One office with five rooms that changes over seven nights.
- **Trailer moment:** Clean a whiteboard and discover a message written beneath the marker.

The cleaning has to feel satisfying on its own. Paint-away dirt masks, a vacuum trail, squeegee streaks, and audio feedback should all be nailed in the prototype.

## How to work (applies to every game in this batch)

You're one of eight sessions building a separate game at the same time. Your game lives only in this project directory. Don't touch anything outside it.

**Goal:** a polished, complete-feeling indie game at AAA quality: juicy feedback, cohesive art direction, real audio, smooth UX, and no placeholder look in the final result. Treat the "buildable first version" scope above as the content target, and make the quality bar high.

**Tooling on this machine**
- Unity **6000.6.2f1** (licensed, Personal) at `/home/nearby/Unity/Hub/Editor/6000.6.2f1/Editor/Unity`. Use URP. The `unity` CLI is installed (`unity --help`, `unity commands`). It can create projects, drive a running Editor, enter play mode, capture the game, and build. Use it.
- Blender **4.5 LTS** is on PATH as `blender`. Do **all 3D modeling in Blender**, scripted with `bpy` (for example `blender -b -P ArtSource/build_assets.py`). Keep `.blend` sources and generator scripts in `ArtSource/` and export FBX/GLB into `Assets/`. Render-check models from Blender and look at the renders before importing.
- Audio: there are no stock libraries. Synthesize SFX and music procedurally (for example Python and numpy to WAV, layered and enveloped). Don't use copyrighted assets.
- Reference projects from the same developer (read only, don't modify): `/home/nearby/Sites/packthetrunk` (Unity 6.6 URP puzzle game) and `/home/nearby/Sites/peggle` (Unity plus a Blender `ArtSource/` pipeline). Skim them for working pipeline patterns.

**Process**
1. **Plan first.** Write `docs/PLAN.md`. It should be a thorough game design and technical plan: one-sentence pitch, design pillars, core loop, mechanics in detail, every level/case/day/object enumerated with its purpose, difficulty curve, narrative beats, art direction (palette, shapes, lighting, camera), audio direction, UI/UX and controls, game feel and juice list, code architecture, asset list (Blender models), milestone plan, risks, and a "5-minute prototype test" describing what must make a player ask to play again. Then **go straight into building** without waiting for approval.
2. **Prototype the core mechanic first** and make it feel great before you produce content. Capture play-mode screenshots, look at them, and iterate.
3. Build out the full content scope, then polish: animation, particles, post-processing, transitions, audio mix, menus (title, pause, settings, level select), save/progress, and an onboarding flow that teaches without walls of text.
4. Verify by actually running the game. Use screenshots and captures, check the console for errors, and fix them. Whenever possible, add automated validation (for example a solver or test that proves every puzzle is solvable).
5. `git init` at the start, use a good Unity `.gitignore`, and commit at each milestone. Don't create GitHub repos or push.
6. Finish with a `README.md` (pitch, controls, rules, content, project layout, how to rebuild assets) and a Linux standalone build in `Builds/`.

**Shared-machine etiquette:** Seven other Unity and Blender sessions run on this machine at the same time (32 cores, 109 GB RAM). Prefer batch mode and headless Blender, close Editors you open when you finish with them, and never kill processes you didn't start.

Report honestly. If something isn't done, doesn't work, or is still placeholder, say so plainly in your updates and in the README.
