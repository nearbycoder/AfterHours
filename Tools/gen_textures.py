#!/usr/bin/env python3
"""Procedural textures for After Hours (numpy + Pillow).

Writes into Assets/Resources/Textures/...:
  Grime/   whiteboard marker art, ghost writing, finger writing masks (secret layers)
  (more sets are added by later milestones: materials, documents, UI)

Run with the project venv:  Tools/.venv/bin/python Tools/gen_textures.py [names...]
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, "ArtSource", "fonts")
OUT = os.path.join(ROOT, "Assets", "Resources", "Textures")


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def save(img, rel):
    path = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=True)
    print("wrote", os.path.relpath(path, ROOT), img.size)


def rgba(hex_, a=255):
    hex_ = hex_.lstrip("#")
    return tuple(int(hex_[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


# ---------------------------------------------------------------------------------------------
# Marker drawing helpers: wobbly strokes with a soft edge so text reads like real marker.
# ---------------------------------------------------------------------------------------------

def wobble_line(draw, pts, color, width, rng, jitter=2.0):
    out = []
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        n = max(2, int(math.hypot(x1 - x0, y1 - y0) / 12))
        for k in range(n):
            t = k / n
            out.append((x0 + (x1 - x0) * t + rng.uniform(-jitter, jitter),
                        y0 + (y1 - y0) * t + rng.uniform(-jitter, jitter)))
    out.append(pts[-1])
    draw.line(out, fill=color, width=width, joint="curve")


def arrow(draw, a, b, color, width, rng):
    wobble_line(draw, [a, b], color, width, rng)
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    for s in (-1, 1):
        h = (b[0] - 26 * math.cos(ang + s * 0.5), b[1] - 26 * math.sin(ang + s * 0.5))
        wobble_line(draw, [b, h], color, width, rng)


def box(draw, x0, y0, x1, y1, color, width, rng):
    wobble_line(draw, [(x0, y0), (x1, y0 + rng.uniform(-4, 4)), (x1, y1), (x0 + rng.uniform(-4, 4), y1), (x0, y0)], color, width, rng)


def text(draw, xy, s, f, color, rot_img=None):
    draw.text(xy, s, font=f, fill=color)


def marker_finish(img, blur=0.8, streak=True, seed=1):
    """Soften edges and add the streaky, slightly uneven density of dry-erase ink."""
    a = np.asarray(img).astype(np.float32)
    rng = np.random.default_rng(seed)
    h, w = a.shape[:2]
    if streak:
        n = rng.random((h // 4 + 1, w // 16 + 1)).astype(np.float32)
        n = np.asarray(Image.fromarray((n * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR)).astype(np.float32) / 255
        a[..., 3] *= 0.78 + 0.22 * n
    out = Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")
    return out.filter(ImageFilter.GaussianBlur(blur))


# ---------------------------------------------------------------------------------------------
# Textures
# ---------------------------------------------------------------------------------------------

def whiteboard_proto():
    W, H = 1400, 770
    rng = random.Random(4)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    blue, red, black, green = rgba("1F4FB8"), rgba("C8312B"), rgba("1B1D22"), rgba("1E8A4C")
    big = font("PermanentMarker.ttf", 64)
    mid = font("PermanentMarker.ttf", 40)
    small = font("PermanentMarker.ttf", 32)
    text(d, (60, 40), "Q3 GROWTH SPRINT!!", big, blue)
    wobble_line(d, [(60, 120), (700, 126)], blue, 6, rng)
    y = 170
    for line in ["- leverage freight synergies", "- KPI dashboard (Priya)", "- client retention +12%", "- \"own the last mile\""]:
        text(d, (80, y), line, small, black)
        y += 58
    box(d, 760, 60, 980, 160, red, 7, rng)
    text(d, (790, 82), "SALES", mid, red)
    box(d, 1080, 60, 1320, 160, red, 7, rng)
    text(d, (1120, 82), "OPS", mid, red)
    arrow(d, (985, 110), (1075, 110), red, 7, rng)
    box(d, 930, 260, 1180, 370, green, 7, rng)
    text(d, (975, 285), "$$$ !!", mid, green)
    arrow(d, (1200, 165), (1120, 255), green, 7, rng)
    arrow(d, (870, 165), (980, 255), green, 7, rng)
    # Doodles and a dense hatch so the board really looks used.
    d.ellipse((1180, 470, 1290, 580), outline=blue, width=6)
    d.arc((1205, 500, 1265, 560), 20, 160, fill=blue, width=5)
    d.ellipse((1210, 500, 1222, 512), fill=blue)
    d.ellipse((1248, 500, 1260, 512), fill=blue)
    text(d, (90, 470), "RUSS = PIZZA GUY", mid, red)
    text(d, (90, 540), "fri 4pm review w/ M.", small, black)
    for k in range(26):
        x = 520 + k * 22
        wobble_line(d, [(x, 470), (x + 120, 690)], green, 5, rng, 1.5)
    text(d, (560, 620), "TARGETS", big, blue)
    save(marker_finish(img, seed=4), "Grime/wb_marker.png")


def whiteboard_ghost_proto():
    W, H = 1400, 770
    rng = random.Random(9)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    ghost = (92, 100, 118, 150)
    f = font("PermanentMarker.ttf", 58)
    s = font("ReenieBeanie.ttf", 64)
    text(d, (120, 250), "NORTHGATE SUPPLY", f, ghost)
    arrow(d, (720, 290), (860, 290), ghost, 7, rng)
    text(d, (880, 250), "???", f, ghost)
    arrow(d, (1010, 290), (1110, 290), ghost, 7, rng)
    text(d, (1120, 250), "M.C.", f, ghost)
    text(d, (220, 380), "who approves these??", s, ghost)
    d.ellipse((1090, 220, 1290, 360), outline=ghost, width=6)
    img = img.filter(ImageFilter.GaussianBlur(2.2))
    save(img, "Grime/wb_ghost.png")


def finger_writing(name, message, W=1200, H=547, size=150, seed=2):
    """Alpha mask of finger-traced letters on glass (resists cleaning foam)."""
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    f = font("Caveat.ttf", size)
    lines = message.split("\n")
    y = H * 0.5 - len(lines) * size * 0.45
    for ln in lines:
        bbox = d.textbbox((0, 0), ln, font=f)
        x = (W - (bbox[2] - bbox[0])) / 2
        # Thicken by stamping slightly offset copies (a fingertip is wide).
        for ox in range(-5, 6, 2):
            for oy in range(-5, 6, 2):
                d.text((x + ox, y + oy), ln, font=f, fill=255)
        y += size * 0.95
    a = np.asarray(img.filter(ImageFilter.GaussianBlur(3.5))).astype(np.float32) / 255
    rng = np.random.default_rng(seed)
    a *= 0.75 + 0.25 * rng.random(a.shape)
    out = np.zeros((H, W, 4), np.uint8)
    out[..., :3] = 255
    out[..., 3] = (np.clip(a * 1.3, 0, 1) * 255).astype(np.uint8)
    save(Image.fromarray(out, "RGBA"), f"Grime/{name}.png")


TASKS = {
    "wb_proto": whiteboard_proto,
    "wb_ghost_proto": whiteboard_ghost_proto,
    "finger_walt": lambda: finger_writing("finger_walt", "WALT DIDN'T\nTAKE IT"),
}


if __name__ == "__main__":
    names = sys.argv[1:] or list(TASKS)
    for n in names:
        TASKS[n]()
