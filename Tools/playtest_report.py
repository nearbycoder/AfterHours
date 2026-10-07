#!/usr/bin/env python3
"""Summarise After Hours playtest logs (the JSON-lines files the game writes with the playtest log
on; see docs/PLAYTEST.md).

    python3 Tools/playtest_report.py PATH [PATH...]     files, or folders of session-*.jsonl

Prints, per session, a line per night (time on the clock, grade, secrets, stuck moments, the task
that took longest to tick off, how it ended), then a summary across sessions per night. Uses only
the standard library.
"""
import glob
import json
import os
import statistics
import sys


def load(paths):
    files = []
    for p in paths:
        files += sorted(glob.glob(os.path.join(p, "session-*.jsonl"))) if os.path.isdir(p) else [p]
    sessions = []
    for f in files:
        events = []
        with open(f, encoding="utf-8") as fh:
            for n, line in enumerate(fh, 1):
                line = line.strip()
                if not line:
                    continue
                try:
                    events.append(json.loads(line))
                except json.JSONDecodeError:
                    print(f"warning: {f}:{n} is not JSON, skipped", file=sys.stderr)
        sessions.append((f, events))
    return sessions


def mmss(s):
    return f"{int(s) // 60}:{int(s) % 60:02d}" if s is not None else "-"


def nights(events):
    """Each attempt at a night: from night_start to night_end, a quit or a restart."""
    out, cur = [], None
    for e in events:
        t = e.get("type")
        if t == "night_start":
            if cur:
                cur["how"] = cur.get("how") or "abandoned"
                out.append(cur)
            cur = {"night": e["night"], "title": e.get("title", ""), "tasks": [], "secrets": 0, "glints": 0,
                   "recovered": 0, "clipboard": 0, "pauses": 0, "how": None, "seconds": None, "grade": None, "open": []}
        elif cur is None:
            continue
        elif t == "task_done":
            cur["tasks"].append((e["nt"], e.get("id"), e.get("optional", False)))
        elif t == "secret":
            cur["secrets"] += 1
        elif t == "stuck_glint":
            cur["glints"] += 1
        elif t == "item_recovered":
            cur["recovered"] += 1
        elif t == "clipboard":
            cur["clipboard"] += 1
        elif t == "pause":
            cur["pauses"] += 1
        elif t == "night_end":
            cur.update(seconds=e.get("seconds"), grade=e.get("grade"), open=e.get("open") or [],
                       secrets_total=e.get("secrets_total"), how="clocked out early" if e.get("open") else "finished")
            out.append(cur)
            cur = None
        elif t in ("quit_to_title", "quit_mid_night", "restart_night"):
            cur.update(how={"quit_to_title": "quit to title", "quit_mid_night": "quit the game",
                            "restart_night": "restarted"}[t], seconds=e.get("nt"), open=e.get("open") or [])
            out.append(cur)
            cur = None
    if cur:
        cur["how"] = "log ends mid-night"
        out.append(cur)
    return out


def slowest(tasks):
    """The required task with the longest wait since the previous tick."""
    req = sorted((t, i) for t, i, opt in tasks if not opt)
    best, prev = None, 0.0
    for t, i in req:
        if best is None or t - prev > best[0]:
            best = (t - prev, i)
        prev = t
    return f"{best[1]} (+{mmss(best[0])})" if best else "-"


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    sessions = load(sys.argv[1:])
    if not sessions:
        sys.exit("no logs found")
    per_night = {}
    for f, events in sessions:
        start = next((e for e in events if e.get("type") == "session_start"), {})
        print(f"\n## {os.path.basename(f)}")
        print(f"{start.get('platform', '?')} · {start.get('gpu', '?')} · {start.get('screen', '?')} · quality {start.get('quality', '?')}"
              f" · pad {start.get('pad') or 'none'} · {len(events)} events")
        ending = next((e.get("id") for e in events if e.get("type") == "ending"), None)
        # The game offered a lower graphics setting because a night ran slowly (and what was chosen).
        for e in events:
            if e.get("type") == "slow_frames":
                print(f"Running slowly on Night {e.get('night', '?')}: about {e.get('fps')} fps at quality {e.get('quality')},"
                      f" render scale {e.get('scale')}")
            elif e.get("type") == "slow_frames_choice":
                print(f"  chose to {e.get('choice')}" + (f" (quality {e.get('quality')}, render scale {e.get('scale')})" if e.get("choice") == "lower" else ""))
        print()
        print("| Night | Time | Grade | Secrets | Stuck glints | Recovered | Clipboard | Slowest task | How it ended |")
        print("|---|---|---|---|---|---|---|---|---|")
        for n in nights(events):
            secrets = f"{n['secrets']}/{n.get('secrets_total')}" if n.get("secrets_total") is not None else str(n["secrets"])
            how = n["how"] + (f" (open: {', '.join(n['open'])})" if n["open"] else "")
            print(f"| {n['night']} {n['title']} | {mmss(n['seconds'])} | {n['grade'] or '-'} | {secrets} | {n['glints']} | {n['recovered']}"
                  f" | {n['clipboard']} | {slowest(n['tasks'])} | {how} |")
            per_night.setdefault(n["night"], []).append(n)
        if ending:
            print(f"\nEnding: **{ending}**")
    if len(sessions) > 1:
        print("\n## All sessions")
        print()
        print("| Night | Attempts | Finished | Median time | Stuck glints (total) | Clocked out early or quit |")
        print("|---|---|---|---|---|---|")
        for night in sorted(per_night):
            runs = per_night[night]
            done = [r for r in runs if r["how"] == "finished"]
            times = [r["seconds"] for r in done if r["seconds"] is not None]
            print(f"| {night} | {len(runs)} | {len(done)} | {mmss(statistics.median(times)) if times else '-'}"
                  f" | {sum(r['glints'] for r in runs)} | {len(runs) - len(done)} |")


if __name__ == "__main__":
    main()
