#!/usr/bin/env python3
"""Graphics Fidelity capture (round 12) -> a frame-time table and a labelled sheet.

    Tools/.venv/bin/python Tools/fidelity_report.py <capture dir> [sheet.jpg] [width]

Reads <capture dir>/player.log from `-ahCapture <dir> fidelity -ahNight 2 -ahFresh` and the
screenshots it took (fidelity_<view>_<step>_<name>.png). Prints a Markdown table of the median
and 95th-percentile frame times per view and step, with the load each was measured at, and,
given a path, writes a sheet: one row per view, one column per step, each labelled with its time.
"""
import glob, os, re, sys
from PIL import Image, ImageDraw, ImageFont

STEPS = ["Low", "Medium", "High", "Ultra"]
src = sys.argv[1]
log = open(os.path.join(src, "player.log"), encoding="utf-8", errors="replace").read()
rx = re.compile(r"fidelity \[(\w+)\] \[(\w+)\] median ([\d.]+) ms \((\d+) fps\), p95 ([\d.]+) ms, (?:p10 ([\d.]+) ms, )?(?:GPU ([\d.]+) ms, main thread ([\d.]+) ms, )?(?:middle of )?(\d+) (?:frames|rounds), load ([\d.]+) ([\d.]+) ([\d.]+)")
rows = {}
for view, step, med, fps, p95, p10, gpu, cpu, n, l1, l5, l15 in rx.findall(log):
    rows.setdefault(view, {})[step] = (float(med), float(p95), int(n), float(l1), float(p10 or 0), float(cpu or 0))
if not rows:
    sys.exit("no fidelity lines in " + src)
views = list(rows)
print("| View | " + " | ".join(STEPS) + " |")
print("|---|" + "---|" * len(STEPS))
for v in views:
    cells = []
    for s in STEPS:
        med, p95, n, load, p10, cpu = rows[v].get(s, (0, 0, 0, 0, 0, 0))
        cells.append(f"{med:.1f} ms (p10 {p10:.1f}, p95 {p95:.1f}; main thread {cpu:.1f})" if n else "–")
    print(f"| {v} | " + " | ".join(cells) + " |")
means = [sum(rows[v][s][0] for v in views if s in rows[v]) / max(1, sum(1 for v in views if s in rows[v])) for s in STEPS]
gmeans = [sum(rows[v][s][5] for v in views if s in rows[v]) / max(1, sum(1 for v in views if s in rows[v])) for s in STEPS]
print("| **mean of medians** | " + " | ".join(f"**{m:.1f} ms** (main thread {g:.1f})" for m, g in zip(means, gmeans)) + " |")
for line in re.findall(r"fidelity step (.+?)  \(t=", log):
    print("- " + line)

if len(sys.argv) > 2:
    out, width = sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 2400
    shots = {}
    for p in glob.glob(os.path.join(src, "*_fidelity_*.png")):
        m = re.search(r"fidelity_(\w+?)_(\d)_", os.path.basename(p))
        if m:
            shots[(m.group(1), int(m.group(2)))] = p
    w = width // len(STEPS)
    first = Image.open(next(iter(shots.values())))
    h = int(w * first.height / first.width)
    bar = 34
    sheet = Image.new("RGB", (w * len(STEPS), (h + bar) * len(views)), (16, 18, 24))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("Assets/Resources/Fonts/FiraSans-Medium.ttf", 20)
    except OSError:
        font = ImageFont.load_default()
    for r, v in enumerate(views):
        for c, s in enumerate(STEPS):
            x, y = c * w, r * (h + bar)
            if (v, c) in shots:
                sheet.paste(Image.open(shots[(v, c)]).convert("RGB").resize((w, h), Image.LANCZOS), (x, y + bar))
            med = rows[v].get(s, (0,))[0]
            d.text((x + 10, y + 6), f"{s} · {v} · {med:.1f} ms", fill=(240, 236, 226), font=font)
    sheet.save(out, quality=88)
    print("sheet ->", out)
