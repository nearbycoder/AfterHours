#!/usr/bin/env python3
"""Quality check for the cut trailer: a labelled frame from every beat (with its caption fully on
screen), black/frozen-frame detection, stream info and loudness.

    Tools/.venv/bin/python Tools/trailer/check_trailer.py [--trailer docs/media/AfterHours-trailer.mp4]

Writes Tools/trailer/work/check/beats.jpg and prints the numbers.
"""
import argparse
import json
import os
import subprocess

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
WORK = os.path.join(ROOT, "Tools", "trailer", "work")


def frame(trailer, t):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.3f}", "-i", trailer, "-frames:v", "1", "-vf", "scale=480:270",
                          "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE, check=True).stdout
    return np.frombuffer(raw, np.uint8).reshape(270, 480, 3)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--trailer", default=os.path.join(ROOT, "docs", "media", "AfterHours-trailer.mp4"))
    ap.add_argument("--work", default=WORK, help="make_trailer.py's work folder (its timeline.json; the sheet goes in check/)")
    a = ap.parse_args()
    work = os.path.abspath(a.work)
    out = os.path.join(work, "check")
    os.makedirs(out, exist_ok=True)
    beats = json.load(open(os.path.join(work, "timeline.json")))

    probe = json.loads(subprocess.run(["ffprobe", "-v", "error", "-show_entries",
                                       "format=duration,size,bit_rate:stream=codec_name,width,height,r_frame_rate,sample_rate,channels",
                                       "-of", "json", a.trailer], stdout=subprocess.PIPE, check=True).stdout)
    print(json.dumps(probe, indent=1))

    # One frame per beat, where its caption is fully on screen (or mid-beat).
    paths = []
    for i, b in enumerate(beats):
        t = b["t0"] + min(b["dur"] * 0.6, 1.4) if (b["cap"] or b["hand"]) else b["t0"] + b["dur"] / 2
        p = os.path.join(out, f"b{i:02d}.png")
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", f"{t:.3f}", "-i", a.trailer, "-frames:v", "1", "-vf", "scale=640:360", p], check=True)
        label = f"{i:02d} {t:5.1f}s {b['clip']}"
        subprocess.run(["magick", p, "-font", os.path.join(ROOT, "ArtSource", "fonts", "FiraSans-Medium.ttf"), "-fill", "white", "-undercolor", "#000c", "-gravity", "northwest", "-pointsize", "18",
                        "-annotate", "+4+4", label, p], check=True)
        paths.append(p)
    subprocess.run(["magick", "montage"] + paths + ["-tile", "6x", "-geometry", "+2+2", "-background", "black",
                                                   os.path.join(out, "beats.jpg")], check=True)
    print("beat sheet:", os.path.join(out, "beats.jpg"))

    # Black or frozen stretches (sampled every 0.5 s).
    dur = float(probe["format"]["duration"])
    prev, frozen, black = None, [], []
    for t in np.arange(0.25, dur - 0.1, 0.5):
        f = frame(a.trailer, t).astype(np.float32)
        if f.mean() < 4:
            black.append(round(float(t), 2))
        if prev is not None and np.abs(f - prev).mean() < 0.15:
            frozen.append(round(float(t), 2))
        prev = f
    print("near-black samples:", black or "none")
    print("frozen samples (no change over 0.5 s):", frozen or "none")

    vd = subprocess.run(["ffmpeg", "-v", "info", "-i", a.trailer, "-map", "0:a", "-af", "volumedetect", "-f", "null", "-"],
                        stderr=subprocess.PIPE, text=True).stderr
    print("\n".join(l.split("] ")[-1] for l in vd.splitlines() if "mean_volume" in l or "max_volume" in l))
    ln = subprocess.run(["ffmpeg", "-v", "info", "-i", a.trailer, "-map", "0:a", "-af", "ebur128=peak=true", "-f", "null", "-"],
                        stderr=subprocess.PIPE, text=True).stderr
    summary = ln[ln.rfind("Summary:"):]
    print(" ".join(summary.split()))


if __name__ == "__main__":
    main()
