# Playtesting After Hours

Nobody outside development has played the game yet. Pacing and clue difficulty are the big open
questions. This page is for running the first sessions: what testers do, what to send back, and
what to look for.

## For testers

1. Play from **New Game** on a computer you're comfortable with. Keyboard and mouse or a
   controller, whichever you'd normally use. Headphones help.
2. Turn the log on before you start: **Settings → Feedback → Playtest log**. The title screen
   then shows "● Playtest log on". (Or start the game with `-playtest`, e.g.
   `./AfterHours.sh -playtest`.)
3. Play as long as you like, at least the first two nights if you can. Stop whenever you'd stop
   if nobody were watching; that's useful too.
4. Send the log file(s) back, plus your answers to the questions below. The logs are in the save
   folder, under `playtest/`:
   - Linux: `~/.config/unity3d/After Hours Team/After Hours/playtest/`
   - macOS: under `~/Library/Application Support/`, in the game's folder, `playtest/`

The log is a plain text file and stays on your computer until you send it. It has timings and
game events only: when nights start and end, which tasks you ticked and when, the secrets you
found, what you did with each document, when the game helped you find leftovers, when you
paused, and your graphics card, screen size and a few settings. Nothing about you or your
computer beyond that, and nothing is sent anywhere.

## Questions to ask afterwards

- What did you think you were supposed to do in the first two minutes? When did it click?
- Was there a moment you didn't know where to go or what was left? What did you do?
- Which cleaning felt best? Which felt like a chore?
- Did you notice any of the hidden things before the game pointed at them? Which ones?
- What did you decide to do with the first note you found, and why?
- How long did a night feel? Did you want to play the next one?
- Anything that looked broken, or any text you couldn't read?

## Watching someone play

- Don't help unless they ask twice. Note where they got stuck and for how long.
- Note when they first open the clipboard, first throw something, first crouch.
- Note whether they read the documents or skip them.

## Reading the logs

```bash
python3 Tools/playtest_report.py path/to/playtest/      # a folder, or individual files
```

For each session, the report prints one line per night: time on the clock, grade, secrets found,
**stuck glints** (each one means a minute with no progress while tasks were still open), items
that had to be recovered, clipboard opens, the **slowest task** (the longest wait between ticks,
usually the thing people couldn't find), and how the night ended (finished, clocked out early
with what left, quit or restarted). With several sessions, it adds a per-night summary.

What to look for:

- **Nights much longer than 5–8 minutes**, or many stuck glints on one night: something is hard
  to find. The slowest task says what.
- **The same task slowest for most testers**: a candidate for a clearer hint, a different place,
  or a better shift-sheet line.
- **Few secrets on a night everyone finishes**: clues may be too hidden. All secrets found by
  everyone: maybe too obvious.
- **Quits mid-night**, and which night: that's where interest dropped.
