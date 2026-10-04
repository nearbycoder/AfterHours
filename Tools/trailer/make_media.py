#!/usr/bin/env python3
"""README media from the filmed trailer clips: screenshots, the trailer poster and the teaser loop.

    Tools/.venv/bin/python Tools/trailer/make_media.py [--clips Recordings/trailer] [--trailer docs/media/AfterHours-trailer.mp4]

Screenshots are full 1920x1080 frames from the running game (the trailer recorder's clips),
saved as high-quality JPEG. The teaser is a short looping animated WebP.
"""
import argparse
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cards  # noqa: E402

ROOT = cards.ROOT
MEDIA = os.path.join(ROOT, "docs", "media")
FPS = 30

# (file name, clip, seconds into the clip)
SHOTS = [
    ("01-title", "title_menu", 3.3),
    ("02-wipe", "n1_wipe", 3.2),
    ("03-whiteboard", "n3_whiteboard", 9.33),
    ("04-throw", "n1_throw", 2.0),
    ("05-vacuum", "n1_vacuum", 4.6),
    ("06-window", "n2_foam", 4.6),
    ("07-uv", "n2_uv", 2.2),
    ("08-evidence", "n1_note", 3.0),
    ("09-report", "n1_report", 5.6),
    ("10-corner-office", "n4_office", 2.0),
]

# Teaser: (clip, start, seconds, extra filter) pieces, joined with short crossfades and looped.
TEASER = [
    ("n2_foam", 2.8, 2.3, None),
    ("n3_whiteboard", 9.0, 2.4, "eq=gamma=1.28:brightness=0.025"),
    ("n1_vacuum", 2.4, 1.8, None),
    ("n2_uv", 2.2, 1.7, "eq=gamma=1.28:brightness=0.025"),
    ("n1_throw", 1.6, 1.9, None),
]

# The trailer frame used for the README poster (seconds into the trailer): the title card.
POSTER_AT = 10.8


def frame_path(clips, clip, t):
    d = os.path.join(clips, clip)
    n = len([f for f in os.listdir(d) if f.endswith(".jpg")])
    i = min(n - 1, max(0, int(round(t * FPS))))
    return os.path.join(d, f"{i:06d}.jpg")


def screenshots(clips):
    out = os.path.join(MEDIA, "screenshots")
    os.makedirs(out, exist_ok=True)
    for name, clip, t in SHOTS:
        src = frame_path(clips, clip, t)
        dst = os.path.join(out, name + ".jpg")
        subprocess.run(["magick", src, "-strip", "-quality", "90", "-sampling-factor", "4:4:4", dst], check=True)
        size = os.path.getsize(dst) / 1e6
        print(f"  {dst}  {size:.2f} MB")
        assert size < 1.5, f"{dst} is over 1.5 MB"


def poster(trailer):
    tmp = os.path.join(ROOT, "Tools", "trailer", "work", "poster_src.png")
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", f"{POSTER_AT:.2f}", "-i", trailer, "-frames:v", "1", tmp], check=True)
    dst = os.path.join(MEDIA, "trailer-poster.jpg")
    secs = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", trailer],
                                stdout=subprocess.PIPE, text=True, check=True).stdout)
    cards.play_overlay(tmp, dst, f"Watch the trailer  ·  {int(secs // 60)}:{int(round(secs % 60)):02d}", cy_frac=0.745, size=0.075, veil_alpha=0)
    print(f"  {dst}  {os.path.getsize(dst) / 1e6:.2f} MB")


def teaser(clips, width=960, fps=15, q=72):
    """Pieces cross-faded into each other; the last fades back into the first so it loops."""
    work = os.path.join(ROOT, "Tools", "trailer", "work")
    os.makedirs(work, exist_ok=True)
    xf = 0.35
    inputs, chain = [], []
    for i, (clip, start, dur, look) in enumerate(TEASER):
        d = os.path.join(clips, clip)
        inputs += ["-framerate", str(FPS), "-start_number", str(int(start * FPS)), "-t", f"{dur:.3f}", "-i", os.path.join(d, "%06d.jpg")]
        chain.append(f"[{i}:v]trim=duration={dur:.3f},setpts=PTS-STARTPTS,scale={width}:-2:flags=lanczos,{look + ',' if look else ''}fps={fps},settb=AVTB,format=yuv420p[p{i}]")
    # Repeat the first piece's opening at the end so the loop point is a crossfade too.
    c0, s0, _, _ = TEASER[0]
    inputs += ["-framerate", str(FPS), "-start_number", str(int(s0 * FPS)), "-t", f"{xf + 0.05:.3f}", "-i", os.path.join(clips, c0, "%06d.jpg")]
    k = len(TEASER)
    chain.append(f"[{k}:v]trim=duration={xf:.3f},setpts=PTS-STARTPTS,scale={width}:-2:flags=lanczos,fps={fps},settb=AVTB,format=yuv420p[p{k}]")
    last, t = "p0", TEASER[0][2]
    for i in range(1, k + 1):
        dur = TEASER[i][2] if i < k else xf
        chain.append(f"[{last}][p{i}]xfade=transition=fade:duration={xf}:offset={t - xf:.3f}[x{i}]")
        last = f"x{i}"
        t = t - xf + dur
    # The stream ends fading into the first piece at time xf, so start the loop at xf: the last
    # frame then leads straight into the first.
    total = t - xf
    chain.append(f"[{last}]trim=start={xf:.3f}:end={t:.3f},setpts=PTS-STARTPTS[out]")
    dst = os.path.join(MEDIA, "teaser.webp")
    subprocess.run(["nice", "-n", "10", "ffmpeg", "-v", "error", "-y"] + inputs + ["-filter_complex", ";".join(chain), "-map", "[out]",
                    "-c:v", "libwebp_anim", "-lossless", "0", "-q:v", str(q), "-compression_level", "6", "-loop", "0", "-an", dst], check=True)
    print(f"  {dst}  {total:.1f} s, {os.path.getsize(dst) / 1e6:.2f} MB")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--clips", default=os.path.join(ROOT, "Recordings", "trailer"))
    ap.add_argument("--trailer", default=os.path.join(MEDIA, "AfterHours-trailer.mp4"))
    ap.add_argument("--only", choices=["shots", "poster", "teaser"])
    a = ap.parse_args()
    clips = os.path.abspath(a.clips)
    if a.only in (None, "shots"):
        screenshots(clips)
    if a.only in (None, "poster"):
        poster(a.trailer)
    if a.only in (None, "teaser"):
        teaser(clips)


if __name__ == "__main__":
    main()
