# After Hours: Design and Technical Plan

> **Pitch:** You're the new night cleaner at a small freight company on the 14th floor, and over
> seven nights the grime you scrub away shows what the day shift is hiding. You decide which
> evidence survives until morning.

This plan is the working contract for the build. It is written before any code. Where the build
deviates from it, the README records the deviation.

---

## 1. Design pillars

1. **Cleaning is the toy.** Every stroke has to feel good with no story at all. Wiping, vacuuming,
   squeegeeing and mopping each get a distinct brush, sound, particle effect and finish moment. A
   surface that hits 100% *pings*, gleams and checks itself off.
2. **Grime hides things.** Clues come out *because* you clean: erasing a whiteboard, spraying foam
   on a window, vacuuming under a desk, shading a notepad with a pencil. Curiosity and tidiness
   pull the same way.
3. **Your hands decide.** Any document you pick up can be put back, shredded, kept, or put on
   someone's desk. The office remembers, and the next night shows what your choice did.
4. **Late-night intimacy.** It's a dark office lit by city glow, monitor light and the lamps you
   switch on. The HVAC hums and the fluorescents buzz. Cozy, a little lonely, a little noir.
5. **Respect the player's time.** The game has no fail state, no walls of text, and no
   pixel-hunting. A UV torch shows missed grime. A shift is about 5 to 7 minutes.

## 2. Core loop

```
 Clock in (janitor closet: corkboard notes, shift sheet)
   → walk the floor, switch on lights
   → clean rooms (wipe / vacuum / squeegee / mop / bin / reset)
        ↳ cleaning reveals clues → inspect → choose: put back · shred · keep · deliver · leave note
   → lights off, clock out
 → Shift report (grade, before/after polaroids, secrets found)
 → Day interlude: the office chat reacts to what you did
 → next night: the office has changed (new mess, consequences, new rooms)
```

The **"one more night" hook**: every night ends on a small unexplained beat, and every morning
chat shows a consequence of your choices. Seven nights take about 45 minutes. Four endings and
per-night secret counts give a reason to replay from **Night Select**.

## 3. Mechanics in detail

### 3.1 Movement and camera
- First person, `CharacterController`, walk at 3.4 m/s, hold Shift for a brisk walk at 5.2 m/s,
  hold C/Ctrl to crouch (to vacuum under desks).
- Mouse look has light smoothing, a subtle head bob (can be turned off) and footsteps that change
  with the floor (carpet, tile or vinyl).
- The view model (the tool in your hand) sways with look and movement and plays a procedural scrub
  animation that follows your stroke direction.

### 3.2 Smart tools
You don't manage an inventory. The tool comes up to match what you aim at. Number keys and the
mouse wheel can still pin a tool by hand.

| Tool | Surfaces | Brush | Feel |
|---|---|---|---|
| **Cloth and spray** | desks, counters, tables, shelves, whiteboard, monitors, microwave | soft round, removal scales with how fast you scrub | the cloth wipe sound follows speed, foam flecks, stubborn stains take two passes |
| **Vacuum** | carpet | round nozzle on the floor | motor loop, pitch rises while it sucks, debris flies into the nozzle with pops, leaves **vacuum stripes** (carpet nap shading by stroke direction) |
| **Squeegee and spray** | glass (doors, windows, glass walls) | RMB sprays foam, LMB drags a wide blade | squeak pitch follows speed, overlapping strokes clear edge **streaks**, water drips run down |
| **Mop** | tile and vinyl floors | wide ellipse | slosh and swish, leaves a wet sheen that dries over about 6 s |
| **Hands** | objects | — | pick up, inspect, throw, place |

Removal rule: `remove = strength × brushFalloff × dt × scrubFactor / toughness`. `scrubFactor`
rewards movement (cloth and mop), so holding still is slow and scrubbing is fast. Vacuum and
squeegee work at a constant rate. **Auto-finish**: at 94% or more, the last specks fade over
0.35 s with a gleam sweep, a sparkle burst and a "ding". The pitch of the ding climbs if you finish
surfaces in quick succession.

### 3.3 Grime surfaces (the core tech)
- Every cleanable is a **grime overlay**: a thin quad or box face laid over real geometry in the
  Blender layout (planes named `GRIME_<id>`). It has its own 2D space, so painting doesn't depend on
  the models' UVs.
- Each overlay owns a `RenderTexture` **mask**:
  - R: dirt remaining (0 to 1)
  - G: vacuum nap (0.5 neutral, lighter or darker by stroke direction)
  - B: wetness or foam (decays)
  - A: reveal accumulator (secret layers)
- Each overlay also owns a CPU-generated **pattern** (`Texture2D` RGBA): the dirt colour and initial
  amount, composed from stamps (dust noise, coffee rings, footprints, scuffs, smudges, haze,
  marker strokes, grease, spills).
- A small **CPU mirror** (¼ resolution) of R takes the same brush stamps. It gives instant
  completion percentages with no GPU readback.
- Optional **secret layers**: *ghost text* is revealed where dirt was erased (the whiteboard),
  *foam-resist text* shows as clear letters in foam (window finger-writing), and a *rubbing* layer
  is revealed by shading (the notepad).
- **UV torch** (F): inside the torch cone, remaining grime fluoresces cyan-violet, and Walt's
  invisible UV-marker notes appear. It makes completionism painless and carries a story thread.

### 3.4 Trash, sorting and throwing
- Loose trash has physics (paper balls, cups, cans, takeout boxes, pizza boxes, banana peels,
  receipts, sticky notes, party plates). Press E to pick up. LMB throws, and holding LMB charges
  the throw (with a faint arc preview).
- Bins: **Trash** (black), **Recycling** (blue: cans, bottles, clean paper) and **Shred** (the
  shredder machines: paper only). A correct bin gives a satisfying thunk or rustle, the lid flaps,
  and a "+" toast appears. A long-distance make adds a *swish* and "Nice shot". A wrong bin gives a
  soft "hmm" and the item bounces back out, so you never lose items.
- Crumpled paper can be **unfolded** (inspect) before you bin it. Some of it is evidence.

### 3.5 Resetting desks
- Displaced objects (mugs, staplers, frames, keyboards, chairs, binders, plants) have **home
  spots**. When you hold one within 0.6 m of its home, a ghost outline appears. Releasing it snaps
  the object home with a click, a squash and a dust puff, as in *A Little to the Left*.
- Chairs: press E to tuck them in, and they glide home with a squeak.
- Monitors: press E to switch them off. Read them first; emails are clues.
- Mugs go into the break-room dishwasher rack (home spots in the rack).
- Light switches: rooms start dark. Switching them on gives a flicker-and-tink, and switching them
  off at the end is part of locking up.

### 3.6 Evidence and choices
Some objects are **evidence** (data-driven documents). Picking one up opens the **Inspect view**:
the paper fills the screen, the background goes to depth of field, and the text is readable (in a
handwriting, typewriter or print font). From the inspect view:

- **Put back** (E): leave it where it was. It stays found but untouched.
- **Keep** (Tab): it goes on your clipboard. You can carry it to a destination later, and you can
  carry several.
- **Shred**: carry it to a shredder (or a shred bag) and feed it in. The pages feed with a
  grinding animation, strips fall into the bin, and it's gone.
- **Deliver**: put it in a person's **inbox tray** (Dana, Theo, Priya, Russ, Marian, and the
  auditor from Night 6). That person finds it in the morning.
- **Leave a note**: the sticky-note pad on your cart. You write a note by choosing one of the
  **phrases you've learned** (clue phrases unlock as you find things, for example "Northgate isn't
  real", "Walt didn't take the laptop", "Check the March wires", "Someone logs in at 1 AM") and
  stick it on a person's monitor. It's a small deduction layer: the right phrase to the right
  person moves the story.

Every evidence item's final **fate** is stored: untouched, back in place, kept, shredded, delivered
to someone, or noted. The night scripts read these flags.

### 3.7 Suspicion (cleaning skill affects the story)
In Marian's office, objects you moved and didn't return to their home spots, plus documents that
are missing, raise **suspicion** when you clock out. Perfect resets cover your tracks. Suspicion
changes Marian's notes, the threats, where she hides the red folder, and your personal epilogue
(kept on or let go). It never blocks progress.

### 3.8 Shift clock and grading
- A wristwatch HUD runs from 10:00 PM to 5:45 AM over about 12 minutes of real time and then holds.
  The city outside the windows slowly blues toward dawn. There's no fail timer.
- The night ends when you clock out at the punch clock in the closet. Clocking out needs the
  shift-sheet tasks done (they're listed on the clipboard). From Night 2 on there's an "Leave
  early anyway?" option that costs grade.
- **Shift grade** (S, A, B, C) comes from required tasks, optional extras (bonus surfaces) and how
  thoroughly you cleaned. **Secrets found** is x/y per night. Both are saved per night.

## 4. Content: the office

One floor (Suite 1408, Meridian Tower). Five rooms plus a closet hub and a hallway.

| Room | Size | Key objects | Cleaning | Story role |
|---|---|---|---|---|
| **Reception** | 7×6 m | reception desk (Dana), glass entrance doors, waiting sofa and coffee table, logo wall, monstera, coat rack, water cooler, Dana's inbox tray | wipe desk and coffee table, squeegee glass doors, vacuum rug, mop tile entry, bins | Dana's notes, the voicemail phone |
| **Bullpen** | 10×8 m | 4 desks (Theo, Priya, Russ, empty "Walt's old hot desk"), printer/copier, shredder, filing cabinets, windows to the city | wipe desks, vacuum carpet, bins, tuck chairs, switch off monitors, reset desk items | the core cast's desks and trays |
| **Conference room** | 6×5 m | long table, 6 chairs, **whiteboard**, wall TV, credenza, glass wall facing the bullpen | erase whiteboard, wipe table, squeegee glass wall, vacuum, tuck chairs | trailer moment, audit-prep agenda, the auditor's desk on Nights 6 and 7 |
| **Corner office** | 5×5 m | executive desk, leather chair, bookshelf, filing cabinet FC-2, orchid, floor-to-ceiling window, guest chairs, shredder, notepad | dust shelves, wipe desk, squeegee window, vacuum, reset every object exactly | Marian's office, suspicion zone |
| **Break room** | 6×5 m | counter, sink, dishwasher, fridge, microwave, coffee machine, table and 4 chairs, bins, corkboard, window | wipe microwave and counter, mop tile, mugs to dishwasher, bins, squeegee window | gossip, Walt's farewell card |
| Janitor closet (hub) | 2.5×2.5 m | punch clock, cart, corkboard, Walt's locker, shelves | — | clock in and out, notes, puzzles |
| Hallway | — | elevator doors, exit sign, fire extinguisher, the "ARCHIVE — DESTROY" boxes (Nights 6 and 7) | mop | connects the rooms |

## 5. Cast

- **You.** An unnamed cleaner from *BrightStar Janitorial*, badge 0417. You're never seen and
  never speak.
- **Dana Whitfield**, office manager (reception). Warm and chatty. Leaves you notes and candy.
  Loyal to the company until she isn't.
- **Theo Marsh**, junior accountant (bullpen). Anxious, signs what he's told, keeps succulents and
  colour-coded pens. The would-be scapegoat.
- **Priya Anand**, IT and operations (bullpen). Sharp and sarcastic, with stickers and energy
  drinks. She noticed the 1 AM logins.
- **Russ Kettering**, sales (bullpen). Loud and messy, plays golf, eats at his desk, bets on games.
  The red herring.
- **Marian Cole**, Director of Finance (corner office). Immaculate, keeps orchids. She is paying a
  fake vendor, *Northgate Supply Co.*, and routing the money to herself.
- **Walt Bremner**, the previous night cleaner (61). Fired for "stealing a laptop" after he taped
  shredded invoices back together. He leaves UV-marker notes for whoever comes next.
- **Erin Sato**, external auditor from Brightwater & Co. She arrives for the audit, and her
  temporary desk is set up on Night 6.

## 6. The seven nights

Each night lists its rooms, new mechanic, required tasks, secrets, choices and the beat it ends
on. *Conditional* lines depend on earlier flags.

### Night 1, Monday: "First Shift" (tutorial, about 5 minutes)
- **Rooms:** Reception, Bullpen. **Introduces:** look and move, cloth, picking up and throwing,
  bins, vacuum, chairs, monitors, light switches, clock out.
- **Tasks:** wipe the reception desk; empty trash (8 items, Russ's birthday leftovers); wipe 3
  desks; vacuum the bullpen (confetti); tuck 4 chairs; monitors off; lights off; clock out.
- **Secrets (3):** Theo's crumpled note *"I can't keep signing these without POs. —T"* (E1).
  Vacuuming under Theo's desk jams on a small brass key tagged **FC-2** (clunk, spit out). Walt's
  note in his locker: *"Whoever you are: the red folder. Don't let them see you read it. —W"*.
- **Choice:** what happens to E1.
- **End beat:** as you switch off the last bullpen light, the monitor in the locked corner office
  wakes up by itself: `mcole — remote session — 01:12`.

### Night 2, Tuesday: "Glass"
- **Rooms:** + Break room. **Introduces:** squeegee and spray, mop, UV torch (from Walt's locker).
- **Tasks:** squeegee the reception doors and the break-room window; mop the break room (spilled
  coffee, footprints); mugs to the dishwasher (5); wipe the microwave and counter; bins; vacuum the
  reception rug.
- **Secrets (4):** foam on the break-room window shows finger-writing, **"WALT DIDN'T TAKE IT"**.
  Russ's betting slip in the bin (*"owe Benny 3,200 by Fri"*), a red herring (E-R). The farewell
  card for Walt, unsigned, with Marian's sticky note *"Do not circulate."* UV arrows on the
  hallway wall point at the conference room.
- **Choices:** erase the finger-writing or leave a clear patch where everyone will see it. Fate of
  the betting slip. Note phrases unlocked.
- **Conditional:** E1 delivered to Theo gives Theo's thank-you post-it on his monitor. To Marian:
  Theo's desk has a "Please see me — M.C." post-it, and Theo looks wrecked. To Priya: her
  "who left this?" message in chat.
- **End beat:** the elevator dings at 3 AM. Nobody comes out.

### Night 3, Wednesday: "The Whiteboard" (trailer moment)
- **Rooms:** + Conference room. **Introduces:** whiteboard erasing, ghost layers, glass wall.
- **Tasks:** erase the whiteboard; wipe table rings; tuck 6 chairs; pizza boxes and cans; squeegee
  the glass wall; vacuum; the usual bullpen pass.
- **Secrets (4):** erasing the Q3 "synergy" brainstorm shows a **ghost diagram** underneath:
  *NORTHGATE SUPPLY → ??? → "who approves these??" → M.C.* (with Priya's arrow). The audit-prep
  agenda: *"Brightwater audit — Monday the 14th."* Theo's planner page listing "sign-off w/ M.C."
  dates (E2). A UV "W" on the whiteboard frame.
- **Choice:** scrub the ghost away (needs a second, harder spray-and-scrub pass) or leave it for
  the 9 AM meeting.
- **Conditional:** if the window message was left, Dana's chat has a "creepy prank" thread and
  Marian sends an all-staff "unprofessional" email that you find on the monitors.
- **End beat:** Marian's office light is on under the door when you arrive. When you leave, it's
  off.

### Night 4, Thursday: "The Corner Office"
- **Rooms:** + Corner office (Dana leaves you the key: "Marian wants a deep clean before the
  auditors"). **Introduces:** exact resets, suspicion, pencil rubbing, FC-2 cabinet, shredder bag.
- **Tasks:** dust the bookshelf; wipe the desk; squeegee the window; vacuum; empty the shredder bin
  (into the chute or the cart); reset desk items exactly.
- **Secrets (5):** pencil rubbing on Marian's notepad gives *"Northgate — wire 48,500 — acct
  ••7731 — Fri"* (E3). The FC-2 cabinet holds Northgate invoices initialled "T.M." (E4). The
  shredder bag: dump it down the chute (destroyed) or put it on your cart (kept for Night 5). An
  envelope with $50 and *"For your discretion. Shred bag straight down the chute, please. —M.C."*
  Her monitor shows the remote-session log again.
- **Choices:** the bag; the invoices; take or return the money (taking it = complicit flag).
- **End beat:** a post-it appears on the punch clock: *"Thank you for being thorough. —M."*
  (exact wording depends on suspicion).

### Night 5, Friday: "Pieces" (the big mess)
- **Rooms:** everything, after Friday drinks. **Introduces:** the shred reconstruction puzzle.
- **Tasks:** the heaviest night. Confetti, plates and cups everywhere, sticky spills, a toppled
  plant, chairs scattered.
- **Secrets (5):** if you kept the shred bag, Walt's tape on the closet table lets you
  reconstruct the strips (a slide-the-strips mini-puzzle) into invoice NG-0412, *"remit to
  ••7731, approved M. Cole"* (E5). Priya's server-log printout left in the copier tray:
  `mcole VPN 01:12, 01:47, 02:03…` (E6). Theo's resignation draft in the bin. Walt's UV trail
  leads to a ceiling tile in the closet with his envelope of copies (E7).
- **Conditional:** betting slip to Marian means Russ has been "suspended pending review" and his
  desk is half packed. High suspicion: Marian's note on the closet door, *"Things have been moved
  in my office. I will be speaking to your agency."*
- **End beat:** the copier prints a single page on its own: *"I KNOW SOMEONE IS HELPING. —P"*
  (only if you've delivered anything to Priya; otherwise it prints "TEST PAGE").

### Night 6, Sunday: "Prep"
- **Rooms:** everything. **Introduces:** the auditor's tray (a legitimate destination).
- **Tasks:** prep the conference room for the audit (polish the table, set 6 water glasses on
  their spots, tuck chairs), the full floor pass, and the hallway "ARCHIVE — DESTROY" boxes taken
  down the chute or left.
- **Secrets (3):** the archive boxes contain the Northgate payment ledger (E8). Theo's packed box
  holds his "insurance" photocopies (if E1 went well). Dana's note: *"Something's off. I don't
  want to know. Do I?"*
- **End beat:** a voicemail light blinks on the reception phone. Play it: Marian, quiet, *"…it all
  goes out with the morning pickup. All of it."*

### Night 7, Monday: "Audit Day"
- **Rooms:** everything, with a storm outside and flickering power. **Introduces:** the final
  choice.
- **Tasks:** the full clean before the auditors arrive at 9 AM.
- **The red folder:** Marian came in tonight. Her office light is on and the shredder is
  **jammed** on the red folder (E9), half fed. Clearing the jam (scrub, pull) frees it. Final
  choice: auditor tray, shredder, back to her desk, or your locker.
- **End:** clock out, then *"Monday, 9:04 AM"*. Chat plus epilogue cards.

### Endings (computed by `EndingResolver`, all four are proven reachable by tests)
1. **"The Audit"** (justice): the red folder is in the auditor's tray **and** at least 2 other key
   pieces (E1, E3 to E8) reached the auditor, either directly or via Priya, who forwards
   everything she was given. Marian is escorted out, Theo is cleared, and Walt gets a letter of
   apology (plus his postcard, if his copies were delivered).
2. **"Clean Books"** (scapegoat): the red folder was shredded or returned to Marian, or fewer than
   2 key pieces reached the auditor and E1 went to Marian. Theo takes the fall. You get a bonus
   envelope and a "Senior Night Custodian" pin.
3. **"Loose Threads"** (partial): some evidence reached the auditor but not enough. Marian
   "resigns to pursue other opportunities", nothing is proven, and Walt's name stays muddy.
4. **"Spotless"** (secret): you never moved a single piece of evidence. Nothing changes. *"The
   office has never looked better."*

Personal epilogue variants: kept on or let go (suspicion), Walt's thanks, the $50.

## 7. Difficulty and pacing curve

| Night | Rooms | Mess volume | New verbs | Secret difficulty | Target time |
|---|---|---|---|---|---|
| 1 | 2 | light | wipe, throw, vacuum, chairs | obvious | 5 min |
| 2 | 3 | light+ | squeegee, mop, UV | spray to see | 5–6 |
| 3 | 4 | medium | ghost layers | requires cleaning | 6 |
| 4 | 5 | medium | exact reset, rubbing | requires tools and keys | 6–7 |
| 5 | 5 | **heavy** | reconstruction | combination | 7–8 |
| 6 | 5 | medium | auditor tray | hidden (UV) | 6 |
| 7 | 5 | medium, with storm | jam, final choice | dramatic | 6–7 |

Variety comes from rotating which rooms are messy and adding "party", "late meeting" and "storm"
themes. Required tasks never ask for 100%; each surface needs its auto-finish threshold.

## 8. Art direction

- **Look: cozy noir, stylised realism.** Furniture is chunky and softly bevelled, with clean
  silhouettes and no fussy detail. Colours read clearly in low light.
- **Palette:**
  - night ambience: ink blue `#141C2B`, `#1E2B40`, `#2C4366`
  - city glow: teal `#3FA7B5`
  - tungsten lamps: `#FFB45E`
  - fluorescents: `#E4F2FF`
  - monitor cyan: `#5FE3FF`
  - evidence red: `#D9483B`
  - sticky-note yellow: `#FFD54A`
  - carpet slate blue: `#3B4A5E`
  - wood: warm oak `#A0703F`
  - dirt: coffee `#4A2F1D`, dust `#8A8170`, grime `#3A3530`
- **Lighting:** URP Forward+. A cool moonlight and city directional light comes through the
  windows. Switchable fluorescent panels (emissive mesh plus a soft spot), warm desk lamps,
  monitor point lights, a red exit sign, and the skyline's emissive windows. Before you switch the
  lights on, rooms are dark but readable.
- **Post-processing:** ACES tonemapping; colour grading with cool shadows and warm highlights;
  bloom for lamps and monitors; SSAO; a soft vignette; light film grain; depth of field only while
  inspecting; chromatic aberration only in the power-flicker beats.
- **Dirt reads:** brown coffee rings with dark edges, grey dust film, dark footprints, colourful
  marker, white glass haze, rainbow confetti. Clean surfaces gain a subtle specular gleam, so
  "clean" looks *better* than default.
- **Camera:** 72° FOV first person. The view-model tool sits lower right and is lit by scene
  lights.
- **UI:** diegetic paper. The clipboard, polaroids, sticky notes, the punch card and the chat
  window all sit on a dark translucent backdrop. Fonts: Fira Sans (UI), a handwriting font (notes)
  and a typewriter or mono font (documents), all OFL with licences included.

## 9. Audio direction (all synthesised in Python/numpy)

- **Ambience beds:** HVAC hum (brown noise, low-passed, with 60 Hz harmonics), distant city
  traffic, rain on glass (Night 7), fridge hum (break room, 3D), fluorescent buzz (3D, per light),
  server whine near the copier, a ticking clock (conference room).
- **Music:** lo-fi night jazz. FM electric-piano chords, a soft sub bass, brushed noise hats, vinyl
  crackle, 72 BPM. Tracks: *Title*, *Night (calm)*, *Night (tense)* (Nights 5–7), *Daylight*
  (chat interlude), *Ending (warm)* and *Ending (cold)*. Stingers: discovery, room complete, night
  complete. The music ducks while you inspect documents.
- **SFX (with 3–5 variations each, pitch-randomised):**
  - cleaning: cloth wipe loop, spray psst, squeegee squeak (pitch by speed), vacuum motor loop
    and suck pops, mop slosh and swish, surface-complete ding (pitch ladder)
  - trash and handling: paper crumple, unfold, bin thunks (plastic and metal), can clank, swish,
    pickup and drop for paper, ceramic, plastic and metal
  - office: shredder grind, footsteps (carpet, tile), door open and close, light switch,
    fluorescent flicker-tink, drawer slide, chair roll and squeak, monitor off blip, keyboard
    clack, phone beep, elevator ding, punch clock ka-chunk, page flip, pen scratch, UI hover and
    click, notification pop

## 10. UI, UX and controls

| Input | Action |
|---|---|
| WASD and mouse | move and look |
| Shift / C (Ctrl) | brisk walk / crouch |
| LMB (hold) | clean with the smart tool / throw a held item (hold to charge) |
| RMB | spray (squeegee and cloth) |
| E | interact: pick up, put back, tuck chair, switch, door, monitor, tray |
| Q | drop held item |
| F | UV torch |
| Tab | clipboard (tasks per room, evidence, phrases, notes) |
| 1–4, wheel | pin a tool (otherwise it's chosen automatically) |
| Esc | pause |

- **HUD:** a centre dot that morphs into a hand, a tool icon, or a progress ring for the surface
  under the reticle. One context prompt at the bottom ("E Pick up · Crumpled note"). A wristwatch
  clock top-right. Toasts for checks ("Reception desk ✓", "Bullpen — spotless").
- **Onboarding (no walls of text):** Dana's corkboard note is the brief, three lines. Prompts
  appear contextually only the first time each verb is possible and fade once you've done it
  twice. Dirty objects shimmer faintly in Night 1 only.
- **Menus:**
  - Title: a slow camera drift through the dark office, with Continue, New Game, Night Select,
    Settings and Quit.
  - Pause: Resume, Clipboard, Settings, Return to Title.
  - Settings: mouse sensitivity, invert Y, FOV, head bob, master, music, SFX and ambience volume,
    fullscreen, resolution scale, quality, subtitles and captions.
  - Night Select: polaroid per night with grade and secrets x/y, replayable from that night's
    saved start state.
- **Transitions:** fade to black with the punch-clock "ka-chunk". Night title cards
  (*Night 3 — Wednesday*) are typewritten onto a punch card.
- **Save:** JSON in `persistentDataPath`. A snapshot of the story state is stored at the start of
  each night, so Night Select replays from that point. Settings are saved separately.

## 11. Game feel and juice list

- [ ] dirt fades under the brush, with foam flecks and dust puffs per stroke
- [ ] auto-finish gleam sweep, sparkle burst and pitch-laddered ding
- [ ] vacuum: nozzle glow, debris pulled in on curves, stripes left behind, the motor whine bends
  with load, a clunk when the key jams
- [ ] squeegee: squeak pitch by speed, drips, streak lines, foam bubbles
- [ ] mop: wet sheen that dries, slosh
- [ ] throw arc preview, bin lid flap, a "swish" on long shots, screen-space "+1"
- [ ] home-spot ghost outline, snap squash and stretch, click
- [ ] chairs glide and settle
- [ ] lights: flicker-on, warm-up, the fluorescent buzz comes up
- [ ] tool view model: raise and lower on swap, sway, scrub follows the stroke
- [ ] camera: tiny kick on throw, head dip on crouch, FOV punch on discovery
- [ ] discovery: music stinger, vignette pulse, slow-motion text reveal
- [ ] room complete: chime, lights pulse, task toast
- [ ] shift report: polaroids slide in, stamp slam on the grade, before/after slider
- [ ] chat interlude: typing indicators, emoji reactions pop
- [ ] UI: hover lifts and clicks, everything eased (no linear tweens)

## 12. Code architecture

```
Assets/
  Scripts/
    Core/        GameRoot (bootstrap, state machine), Save, Settings, Tween, Events, Rng
    Player/      FirstPersonController, PlayerLook, Interactor, Viewmodel, Footsteps
    Cleaning/    GrimeSurface, GrimePattern (stamps), GrimeBrush, ToolBelt, ToolDefs,
                 DebrisField (vacuum debris), CleanStats
    Interaction/ Pickup, Throwable, TrashItem, Bin, HomeSpot/Resettable, Chair, Door, LightSwitch,
                 MonitorScreen, InboxTray, Shredder, Drawer, PunchClock, UvMark
    Story/       StoryState (flags, fates, phrases), EvidenceDef/Docs, NightDefs (7 nights, data),
                 NightDirector (applies conditional content), EndingResolver, ChatScripts
    World/       OfficeBuilder (FBX → components by name), RoomVolume, Lighting, Skyline, DayCycle
    UI/          UiKit (runtime uGUI), Hud, Prompts, Clipboard, InspectView, NoteComposer,
                 ShiftReport, ChatInterlude, Menus (Title/Pause/Settings/NightSelect), Ending
    Audio/       AudioDirector (music stems, ducking), Sfx (pooled, variations), AmbienceZones
    FX/          Particles, GleamSweep, ScreenFx (post volume weights)
    Testing/     AutoPilot (-ahAutopilot), Capture (-ahCapture shots)
  Shaders/       Grime.shader (lit overlay), GrimeBrush.shader (mask stamp), Skyline.shader, UvMark
  Editor/        BuildScript, ImportSettings (FBX/Audio), MaterialSetup, ProjectSetup (URP, renderer)
  Resources/     Models/, Audio/, Fonts/, Textures/
  Tests/         EditMode: EndingResolver reachability, night data integrity, pattern stats
```

- **Bootstrap:** a `RuntimeInitializeOnLoadMethod` creates `GameRoot`. The `Main` scene holds
  only a camera, light and volume. The office is instantiated from `Resources/Models/Office.fbx`,
  and `OfficeBuilder` attaches behaviour by object-name conventions (`GRIME_`, `ANCHOR_`, `LIGHT_`,
  `DOOR_`, `SWITCH_`, `TRAY_`, `BIN_`, `CHAIR_`, `MONITOR_`, `ROOM_`, `HOME_`, `COL_`). That makes
  Blender the level editor.
- **Data-driven nights:** `NightDefs.cs` holds C# object initialisers (type-safe, no parser).
  Each night is a list of `Spawn` entries (prop, anchor, offset, condition), `GrimeSpec`s per
  surface (stamps, seed, toughness), tasks, secrets, notes and documents, and a `Chat` script
  with conditions.
- **Conditions:** small predicates over `StoryState`, for example
  `Fate("E1") == Delivered("theo")`, `Flag("kept_shreds")` or `Suspicion >= 2`.
- **Testability:** `StoryState` and `EndingResolver` are pure C# and tested in EditMode. A test
  enumerates the choice space to prove all 4 endings are reachable and that every night's
  required tasks reference real objects.
- **AutoPilot:** launched with `-ahAutopilot <dir>`, the built player plays every night with real
  tool strokes, checks tasks complete, makes a scripted set of choices, saves screenshots, and
  prints PASS/FAIL.

## 13. Asset list (Blender, `ArtSource/`)

All assets come from Python generator scripts (`ArtSource/*.py`) run with `blender -b`. They're
exported as FBX into `Assets/Resources/Models/`, with a `.blend` saved per set. Materials are named
by convention (`col_RRGGBB`, `mat_wood`, `mat_carpet`, `mat_tile`, `mat_glass`, `glow_RRGGBB`,
`screen_*`) and remapped to URP materials on import.

- **Architecture (`office.py`):** floors per room (carpet, tile, vinyl), walls with window cut-outs,
  ceiling with grid tiles and light-panel housings, door frames and doors (wood, glass), glass
  walls and doors, window frames and mullions, baseboards, columns, elevator doors, and all
  anchors, grime planes and room volumes. The skyline is a set of low-poly tower blocks.
- **Furniture (`furniture.py`):** office desk, executive desk, reception desk, conference table,
  office chair, executive chair, guest chair, sofa, coffee table, bookshelf, filing cabinet (2- and
  4-drawer), credenza, kitchen counter with sink, fridge, table and chairs, coat rack, lockers,
  shelving, janitor cart.
- **Equipment (`equipment.py`):** monitor, keyboard, mouse, desk lamp, desk phone, laptop,
  printer/copier, shredder, microwave, coffee machine, water cooler, wall TV, whiteboard, punch
  clock, wall clock, exit sign, light switch, ceiling light panel, inbox tray, fire extinguisher.
- **Props (`props.py`):** mugs (3 styles), paper cup, soda can, energy drink, water bottle, paper
  ball, sheets of paper, folder (manila, red), binder, sticky-note pad, stapler, pen cup, photo
  frame, award plaque, succulent, monstera, snake plant, orchid, golf bag, pizza box, takeout box,
  party plate, banana peel, party hat, envelope, key, shred strips, archive box, water glass.
- **Tools (`tools.py`):** cloth and spray bottle, vacuum (upright, hand-held view model), squeegee
  and spray, mop and bucket, UV torch.
- **Review:** each script renders an Eevee contact sheet (`ArtSource/renders/*.png`), and I look at
  it before importing.

**Procedural textures** (`Tools/gen_textures.py`, numpy and PIL): carpet tile, floor tile, oak
grain, ceiling tile, fabric, brushed metal, whiteboard marker art, ghost text, finger-writing
masks, document paper, dirt stamps (rings, footprints, smudges, splats), confetti, UV glyphs.

## 14. Milestones

| # | Milestone | Exit criteria |
|---|---|---|
| M0 | Setup | project, git, pipeline, scripts for building, playing and capturing; a screenshot from a build |
| M1 | **Core prototype** | greybox room; FPS controller; cloth, vacuum (stripes and debris), squeegee (foam, streaks), mop; completion, auto-finish, particles and placeholder synth audio; tuned from screenshots |
| M2 | Office and art | Blender office and props; lighting and post look in Unity; skyline |
| M3 | Interaction | pickup and throw, bins, home spots, chairs, doors, switches, monitors, inspect view, trays, shredder, notes |
| M4 | Story | 7 nights of data, conditions, NightDirector, chat interludes, shift report, endings, save, night select |
| M5 | UI and onboarding | title, pause, settings, clipboard, prompts, transitions |
| M6 | Audio | full synthesised SFX set, ambience beds, 6 music tracks, mix and ducking |
| M7 | Polish | juice list, before/after polaroids, animation, performance pass |
| M8 | Verify and ship | AutoPilot plays every night PASS; ending tests pass; screenshots reviewed; Linux build; README |

Commit after every milestone, and more often during long ones.

## 15. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Shared machine (8 sessions) | batch mode wherever possible; build the player for captures; close editors |
| Batch-mode Editor can't render play mode | verify through a built player that screenshots itself (`-ahCapture`) |
| A hand-written URP lit transparent shader breaks across URP versions | keep the shader small, include URP lighting headers, test early (M1) |
| Painting perf (many RTs) | a mask only becomes active when touched; CPU mirror at ¼ res; RT sizes capped by area |
| Writing volume (7 nights of text) | short documents; chat lines under 90 characters; writing in data, edited together |
| Blender asset count | a shared bevelled-primitive library; furniture from a few parametric builders |
| First-person feel in automation | AutoPilot drives the real tools by pointing the camera at a world target and holding the button |
| Scope | M1 to M4 is the vertical slice; content past Night 4 builds on the same systems; README says plainly if anything is cut |

## 16. The 5-minute prototype test

A new player gets five minutes, starting at Night 1. They should ask to play again if all of
these happen:

1. **First 20 s:** they wipe a coffee-ringed desk and the ring *lifts*, crisp, under the cloth. At
   100% it gleams and dings. They wipe another one just to hear it again.
2. **First minute:** they throw a paper ball across the room into the bin, hear the thunk, and get
   "Nice shot."
3. **Minute 2:** the vacuum leaves stripes in the carpet they want to straighten, and confetti
   zips into the nozzle with pops.
4. **Minute 3:** the vacuum *clunks* on a key nobody mentioned, and an unfolded paper ball reads
   *"I can't keep signing these."* Now they're curious.
5. **Minute 4:** they decide what to do with the note. Putting it on someone's desk feels like a
   real choice.
6. **Minute 5:** the lights go off, the locked office's monitor wakes up alone, and the screen
   says "remote session 01:12". Then *Night 1 complete*, the before/after polaroids, and a morning
   chat where someone mentions the note.

If any step feels flat in a recorded playthrough, fix it before building more content.
