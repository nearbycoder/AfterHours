#!/usr/bin/env python3
"""The After Hours app icon (numpy + Pillow): a night-time office window, lit squares of the
city behind it, one clean streak wiped across the glass, and "AH" in the game's typewriter face.

Writes Assets/Icons/AppIcon.png (1024x1024); ProjectSetup assigns it to every platform.

Run with the project venv:  Tools/.venv/bin/python Tools/make_icon.py [--preview DIR]
  --preview DIR  also writes 16-512 px downscales side by side, to check it still reads small
"""
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT = os.path.join(ROOT, "Assets", "Resources", "Fonts", "SpecialElite.ttf")
OUT = os.path.join(ROOT, "Assets", "Icons", "AppIcon.png")
S = 1024

# Palette from docs/PLAN.md (art direction).
INK = (20, 28, 43)        # 141C2B
NIGHT = (44, 67, 102)     # 2C4366
TUNGSTEN = (255, 180, 94) # FFB45E
TEAL = (63, 167, 181)     # 3FA7B5
PAPER = (242, 238, 226)   # F2EEE2


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def main():
    rng = random.Random(1408)
    # Background: ink at the top to night blue at the bottom.
    y = np.linspace(0, 1, S)[:, None, None]
    bg = (np.array(INK) * (1 - y) + np.array(NIGHT) * y).repeat(S, axis=1)
    img = Image.fromarray(bg.astype(np.uint8), "RGB").convert("RGBA")

    # City windows: a grid of small squares, a few lit warm, a few teal (monitors).
    win = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(win)
    cell, pad = 64, 14
    for gy in range(2, S // cell - 1):
        for gx in range(1, S // cell - 1):
            r = rng.random()
            if r < 0.30:
                col = TUNGSTEN + (int(150 + 90 * rng.random()),)
            elif r < 0.36:
                col = TEAL + (170,)
            else:
                col = (90, 110, 150, 40)
            x0, y0 = gx * cell + pad, gy * cell + pad
            d.rectangle([x0, y0, x0 + cell - 2 * pad, y0 + cell - 2 * pad], fill=col)
    img.alpha_composite(win)

    # Grime haze over the glass, except a diagonal band that has been wiped clean.
    haze = np.zeros((S, S), np.float32)
    noise = Image.effect_noise((S, S), 60).filter(ImageFilter.GaussianBlur(18))
    haze += np.asarray(noise, np.float32) / 255.0 * 0.55 + 0.25
    yy, xx = np.mgrid[0:S, 0:S]
    band = np.abs((xx - S * 0.5) * 0.55 - (yy - S * 0.62)) / (S * 0.11)
    clean = np.clip(1.0 - band, 0, 1) ** 0.6
    haze *= 1.0 - clean
    haze_rgba = np.zeros((S, S, 4), np.uint8)
    haze_rgba[..., :3] = (70, 64, 58)
    haze_rgba[..., 3] = (np.clip(haze, 0, 1) * 200).astype(np.uint8)
    img.alpha_composite(Image.fromarray(haze_rgba, "RGBA"))

    # A gleam along the wiped streak's leading edge.
    gleam = np.clip(1.0 - np.abs(band - 0.85) / 0.08, 0, 1) * (yy < S * 0.9)
    g = np.zeros((S, S, 4), np.uint8)
    g[..., :3] = 255
    g[..., 3] = (gleam * 110).astype(np.uint8)
    img.alpha_composite(Image.fromarray(g, "RGBA").filter(ImageFilter.GaussianBlur(3)))

    # "AH", typewriter cream with a soft shadow.
    font = ImageFont.truetype(FONT, 470)
    text = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    td = ImageDraw.Draw(text)
    box = td.textbbox((0, 0), "AH", font=font)
    tx = (S - (box[2] - box[0])) // 2 - box[0]
    ty = (S - (box[3] - box[1])) // 2 - box[1] - 20
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).text((tx + 10, ty + 16), "AH", font=font, fill=(0, 0, 0, 200))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(14)))
    td.text((tx, ty), "AH", font=font, fill=PAPER + (255,))
    img.alpha_composite(text)

    # Rounded-square mask with a thin frame, so it sits well in docks and launchers.
    radius = 190
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius, fill=255)
    frame = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(frame).rounded_rectangle([10, 10, S - 11, S - 11], radius - 10, outline=(255, 255, 255, 40), width=10)
    img.alpha_composite(frame)
    img.putalpha(mask)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT, optimize=True)
    print("wrote", os.path.relpath(OUT, ROOT), img.size)

    if "--preview" in sys.argv:
        out_dir = sys.argv[sys.argv.index("--preview") + 1]
        os.makedirs(out_dir, exist_ok=True)
        sizes = [512, 256, 128, 64, 32, 16]
        sheet = Image.new("RGBA", (sum(sizes) + 20 * len(sizes) + 20, 552), (40, 40, 44, 255))
        x = 20
        for s in sizes:
            sheet.alpha_composite(img.resize((s, s), Image.LANCZOS), (x, 20))
            x += s + 20
        path = os.path.join(out_dir, "icon-sizes.png")
        sheet.save(path)
        print("wrote", path)


if __name__ == "__main__":
    main()
