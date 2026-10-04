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



# =============================================================================================
# Tileable material textures (1 texture = 1 m unless the material name carries _tNN tiling)
# =============================================================================================

def _tile_noise(size, cells, rng, octaves=4):
    """Periodic value-noise fBm on a size x size grid, `cells` noise cells across."""
    out = np.zeros((size, size), np.float32)
    amp, norm, c = 1.0, 0.0, cells
    for _ in range(octaves):
        g = rng.random((c, c)).astype(np.float32)
        g = np.concatenate([g, g[:1]], 0)
        g = np.concatenate([g, g[:, :1]], 1)
        x = np.linspace(0, c, size, endpoint=False)
        xi = x.astype(int)
        xf = x - xi
        xf = xf * xf * (3 - 2 * xf)
        a = g[np.ix_(xi, xi)]
        b = g[np.ix_(xi, xi + 1)]
        cc = g[np.ix_(xi + 1, xi)]
        d = g[np.ix_(xi + 1, xi + 1)]
        fy = xf[:, None]
        fx = xf[None, :]
        out += amp * ((a * (1 - fx) + b * fx) * (1 - fy) + (cc * (1 - fx) + d * fx) * fy)
        norm += amp
        amp *= 0.5
        c *= 2
    return out / norm


def _normal_from_height(h, strength=2.0):
    gy, gx = np.gradient(h)
    n = np.stack([-gx * strength, -gy * strength, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)


def _save_rgb(arr, rel):
    save(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB"), rel)


def _hex(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


def carpet(name, base, accent, size=1024, tiles=2, seed=1):
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32) / size
    loops = _tile_noise(size, 256, rng, 2)               # loop pile
    fibre = _tile_noise(size, 128, rng, 3)
    mott = _tile_noise(size, 8, rng, 3)
    # Quarter-turned carpet tiles: alternate a directional texture per tile.
    tx = (xx * tiles).astype(int)
    ty = (yy * tiles).astype(int)
    flip = (tx + ty) % 2 == 0
    lines = np.where(flip, np.sin(xx * size * 0.9), np.sin(yy * size * 0.9)) * 0.5 + 0.5
    shade = 0.82 + 0.1 * mott + 0.08 * loops + 0.05 * lines + 0.04 * (fibre - 0.5)
    tile_var = rng.random((tiles, tiles))[ty % tiles, tx % tiles] * 0.05
    shade += tile_var
    # Thin seams between tiles.
    fx, fy = (xx * tiles) % 1, (yy * tiles) % 1
    seam = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy))
    shade *= 0.93 + 0.07 * np.clip(seam * 200, 0, 1)
    speck = (rng.random((size, size)) > 0.985).astype(np.float32)
    col = base[None, None, :] * shade[..., None]
    col = col * (1 - speck[..., None] * 0.5) + accent[None, None, :] * speck[..., None] * 0.5
    _save_rgb(col, f"Mat/{name}.png")
    h = loops * 0.6 + lines * 0.2 + fibre * 0.2
    save(Image.fromarray(_normal_from_height(h, 3.0), "RGB"), f"Mat/{name}_n.png")


def floor_tile(name, base, grout, size=1024, tiles=4, seed=2, speckle=0.0, vein=0.0):
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32) / size
    fx, fy = (xx * tiles) % 1, (yy * tiles) % 1
    tx, ty = (xx * tiles).astype(int), (yy * tiles).astype(int)
    edge = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy)) * (size / tiles)
    grout_w = 2.2
    g = np.clip((edge - grout_w) / 1.5, 0, 1)
    mott = _tile_noise(size, 16, rng, 4)
    tile_var = rng.random((tiles + 1, tiles + 1))[ty, tx]
    shade = 0.93 + 0.05 * mott + 0.04 * (tile_var - 0.5)
    col = base[None, None, :] * shade[..., None]
    if speckle > 0:
        sp = rng.random((size, size))
        dots = (sp > 1 - speckle).astype(np.float32)
        tone = rng.random((size, size))[..., None]
        col = col * (1 - dots[..., None]) + dots[..., None] * (np.array([70, 70, 75]) * tone + np.array([170, 160, 150]) * (1 - tone))
    if vein > 0:
        v = _tile_noise(size, 4, rng, 5)
        veins = np.exp(-((np.sin((xx + v * 1.5) * 9) ) ** 2) / 0.01) * vein
        col = col * (1 - veins[..., None] * 0.25)
    col = col * g[..., None] + grout[None, None, :] * (1 - g[..., None])
    _save_rgb(col, f"Mat/{name}.png")
    h = g * 0.8 + mott * 0.05
    save(Image.fromarray(_normal_from_height(h, 6.0), "RGB"), f"Mat/{name}_n.png")


def wood(name, base, dark, size=1024, seed=3, planks=0):
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32) / size
    warp = _tile_noise(size, 4, rng, 4)
    fine = _tile_noise(size, 64, rng, 3)
    # Grain runs along u (x): rings are bands in v distorted by noise.
    rings = np.sin((yy * 22 + warp * 3.0) * np.pi * 2) * 0.5 + 0.5
    rings = rings ** 3
    streak = _tile_noise(size, 32, rng, 2)
    streak = np.repeat(streak.mean(axis=1, keepdims=True), size, axis=1) * 0.6 + streak * 0.4
    t = np.clip(0.35 * rings + 0.35 * streak + 0.2 * fine + 0.1 * warp, 0, 1)
    col = base[None, None, :] * (1 - t[..., None]) + dark[None, None, :] * t[..., None]
    if planks:
        py = (yy * planks) % 1
        seam = np.minimum(py, 1 - py) * size / planks
        col *= (0.8 + 0.2 * np.clip(seam / 2.0, 0, 1))[..., None]
    _save_rgb(col, f"Mat/{name}.png")
    save(Image.fromarray(_normal_from_height(rings * 0.3 + fine * 0.2, 1.5), "RGB"), f"Mat/{name}_n.png")


def ceiling(name, size=1024, tiles=2, seed=4):
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32) / size
    fx, fy = (xx * tiles) % 1, (yy * tiles) % 1
    edge = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy)) * (size / tiles)
    bar = np.clip((edge - 6) / 2, 0, 1)
    pits = (rng.random((size, size)) > 0.93).astype(np.float32) * 0.12
    mott = _tile_noise(size, 32, rng, 3)
    tile = np.array([226, 224, 218], np.float32) * (0.95 + 0.05 * mott - pits)[..., None]
    metal = np.array([200, 202, 205], np.float32)
    col = tile * bar[..., None] + metal * (1 - bar[..., None])
    _save_rgb(col, f"Mat/{name}.png")
    save(Image.fromarray(_normal_from_height(bar * 1.0 - pits * 2, 5.0), "RGB"), f"Mat/{name}_n.png")


def fabric(name, base, size=512, seed=5):
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    weave = (np.sin(xx * np.pi * 2 / 4) * np.sin(yy * np.pi * 2 / 4)) * 0.5 + 0.5
    mott = _tile_noise(size, 16, rng, 3)
    shade = 0.85 + 0.1 * weave + 0.08 * mott
    _save_rgb(base[None, None, :] * shade[..., None], f"Mat/{name}.png")
    save(Image.fromarray(_normal_from_height(weave * 0.5, 2.0), "RGB"), f"Mat/{name}_n.png")


def cork(name, size=512, seed=6):
    rng = np.random.default_rng(seed)
    n1 = _tile_noise(size, 64, rng, 2)
    n2 = rng.random((size, size))
    base = np.array([176, 128, 82], np.float32)
    shade = 0.75 + 0.3 * n1 - (n2 > 0.92) * 0.25 + (n2 < 0.05) * 0.15
    _save_rgb(base[None, None, :] * shade[..., None], f"Mat/{name}.png")


def rug(name, a, b, c, size=1024, seed=7):
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32) / size
    fibre = _tile_noise(size, 128, rng, 2)
    stripes = ((np.floor(yy * 10) % 3) == 0).astype(np.float32)
    diamonds = (np.abs(((xx * 4) % 1) - 0.5) + np.abs(((yy * 4) % 1) - 0.5) < 0.18).astype(np.float32)
    col = a[None, None, :] * (1 - stripes[..., None]) + b[None, None, :] * stripes[..., None]
    col = col * (1 - diamonds[..., None]) + c[None, None, :] * diamonds[..., None]
    col *= (0.85 + 0.2 * fibre)[..., None]
    _save_rgb(col, f"Mat/{name}.png")
    save(Image.fromarray(_normal_from_height(fibre, 2.0), "RGB"), f"Mat/{name}_n.png")


def plaster(name, base, size=512, seed=8):
    rng = np.random.default_rng(seed)
    n = _tile_noise(size, 16, rng, 5)
    _save_rgb(base[None, None, :] * (0.96 + 0.06 * n)[..., None], f"Mat/{name}.png")
    save(Image.fromarray(_normal_from_height(n, 1.0), "RGB"), f"Mat/{name}_n.png")


def materials():
    carpet("carpet_slate", _hex("3E4D63"), _hex("8FA3BF"), seed=11)
    carpet("carpet_rust", _hex("5E3530"), _hex("C08060"), seed=12, tiles=1)
    carpet("carpet_teal", _hex("2E5257"), _hex("7FB3B0"), seed=13)
    floor_tile("tile_break", _hex("DCD8CF"), _hex("8E8A83"), tiles=4, speckle=0.02, seed=21)
    floor_tile("tile_lobby", _hex("B9B1A5"), _hex("6D675F"), tiles=2, vein=0.6, seed=22)
    floor_tile("tile_closet", _hex("9EA3A1"), _hex("5B605E"), tiles=4, seed=23)
    wood("wood_oak", _hex("B07D4A"), _hex("7A5030"), seed=31)
    wood("wood_walnut", _hex("6A4630"), _hex("3B2518"), seed=32)
    wood("wood_floor", _hex("8F6A48"), _hex("5C4028"), seed=33, planks=6)
    ceiling("ceiling_tile")
    fabric("fabric_teal", _hex("2F6F73"), seed=41)
    fabric("fabric_charcoal", _hex("3A3E45"), seed=42)
    fabric("fabric_mustard", _hex("C9963A"), seed=43)
    cork("cork")
    rug("rug_lobby", _hex("2B3A4A"), _hex("3B5268"), _hex("C9963A"))
    plaster("plaster_warm", _hex("D2CCC0"))
    plaster("plaster_teal", _hex("23464C"), seed=9)


TASKS["materials"] = materials



# =============================================================================================
# Printed / paper textures for props (UV-fitted, so one texture = one object face)
# =============================================================================================

def paper_printed():
    W, H = 420, 594
    img = Image.new("RGB", (W, H), (243, 241, 234))
    d = ImageDraw.Draw(img)
    rng = random.Random(3)
    d.rectangle((30, 30, 200, 52), fill=(60, 64, 72))
    y = 80
    while y < H - 50:
        if rng.random() < 0.12:
            y += 18
            continue
        w = rng.randint(220, W - 70) if rng.random() > 0.15 else rng.randint(80, 200)
        d.rectangle((30, y, 30 + w, y + 5), fill=(120, 124, 132))
        y += 14
    d.rectangle((30, H - 60, 160, H - 40), outline=(90, 90, 100), width=2)
    save(img, "Mat/paper_printed.png")


def legal_pad():
    W, H = 420, 600
    img = Image.new("RGB", (W, H), (250, 236, 150))
    d = ImageDraw.Draw(img)
    for y in range(70, H, 22):
        d.line((0, y, W, y), fill=(140, 170, 205), width=2)
    d.line((60, 0, 60, H), fill=(214, 120, 120), width=3)
    d.rectangle((0, 0, W, 28), fill=(140, 60, 45))
    save(img, "Mat/legal_pad.png")


def sticky():
    W = 256
    img = Image.new("RGB", (W, W), (255, 213, 74))
    d = ImageDraw.Draw(img)
    f = font("Caveat.ttf", 54)
    d.text((22, 40), "call me", font=f, fill=(40, 40, 60))
    d.text((40, 120), "- T", font=f, fill=(40, 40, 60))
    a = np.asarray(img).astype(np.float32)
    a[:30] *= 0.93
    save(Image.fromarray(a.astype(np.uint8)), "Mat/sticky.png")


def photo():
    W, H = 240, 310
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    sky = np.stack([255 - yy * 0.25, 170 - yy * 0.15, 120 + yy * 0.1], -1)
    img = Image.fromarray(np.clip(sky, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    d.ellipse((150, 60, 210, 120), fill=(255, 225, 160))
    d.rectangle((0, 210, W, H), fill=(60, 80, 90))
    for cx, h in ((80, 120), (125, 95), (160, 70)):
        d.ellipse((cx - 16, 210 - h - 30, cx + 16, 210 - h + 2), fill=(40, 40, 50))
        d.rounded_rectangle((cx - 24, 210 - h, cx + 24, 210 + 20), 14, fill=(40, 40, 50))
    save(img, "Mat/photo.png")


def prop_textures():
    paper_printed()
    legal_pad()
    sticky()
    photo()


TASKS["props"] = prop_textures



# =============================================================================================
# Monitor screens (emissive textures, 16:9)
# =============================================================================================

SW, SH = 1024, 576


def _screen_base(top, bottom):
    yy = np.linspace(0, 1, SH)[:, None, None]
    a = np.array(top, np.float32)[None, None, :]
    b = np.array(bottom, np.float32)[None, None, :]
    img = a * (1 - yy) + b * yy
    img = np.repeat(img, SW, axis=1)
    return Image.fromarray(img.astype(np.uint8), "RGB")


def _lock(name, user, top, bottom, accent, sub=None):
    img = _screen_base(top, bottom)
    d = ImageDraw.Draw(img)
    big = font("FiraSans-Light.ttf", 150)
    d.text((70, 90), "10:04", font=big, fill=(240, 244, 250))
    d.text((80, 260), "Monday, March 11", font=font("FiraSans-Regular.ttf", 40), fill=(220, 226, 236))
    d.ellipse((SW - 300, SH - 250, SW - 200, SH - 150), fill=accent)
    initials = "".join(p[0] for p in user.split()[:2])
    d.text((SW - 250, SH - 200), initials, font=font("FiraSans-Bold.ttf", 44), fill=(255, 255, 255), anchor="mm")
    d.text((SW - 250, SH - 115), user, font=font("FiraSans-Medium.ttf", 30), fill=(240, 244, 250), anchor="mm")
    d.text((SW - 250, SH - 75), sub or "Press Ctrl+Alt+Del to unlock", font=font("FiraSans-Regular.ttf", 20), fill=(200, 208, 220), anchor="mm")
    save(img, f"Screens/{name}.png")


def _window(img, title, x0, y0, x1, y1):
    d = ImageDraw.Draw(img)
    d.rectangle((x0, y0, x1, y1), fill=(246, 247, 250))
    d.rectangle((x0, y0, x1, y0 + 40), fill=(54, 62, 78))
    d.text((x0 + 16, y0 + 9), title, font=font("FiraSans-Medium.ttf", 20), fill=(235, 238, 245))
    for i, c in enumerate([(236, 95, 90), (240, 190, 70), (98, 200, 110)]):
        d.ellipse((x1 - 90 + i * 26, y0 + 13, x1 - 76 + i * 26, y0 + 27), fill=c)
    return d


def screens():
    _lock("login_dana", "Dana Whitfield", (240, 140, 110), (60, 70, 140), (232, 160, 191))
    _lock("login_priya", "Priya Anand", (20, 30, 40), (10, 60, 70), (127, 168, 232), "lock screen · don't even try")
    _lock("marian_lock", "Marian Cole", (70, 30, 40), (20, 20, 30), (176, 64, 64), "Last sign-in: 01:12 AM (remote)")

    # Theo's open email
    img = _screen_base((32, 40, 52), (24, 30, 40))
    d = _window(img, "Mail — Inbox (3)", 30, 24, SW - 30, SH - 24)
    d.rectangle((30, 64, 300, SH - 24), fill=(232, 235, 241))
    for i, (who, subj) in enumerate([("Marian Cole", "Northgate — today please"), ("Dana W.", "Birthday cake for Russ!!"), ("IT", "Password expires in 3 days")]):
        y = 80 + i * 74
        if i == 0:
            d.rectangle((30, y - 8, 300, y + 62), fill=(205, 222, 250))
        d.text((46, y), who, font=font("FiraSans-Bold.ttf", 20), fill=(30, 36, 48))
        d.text((46, y + 28), subj, font=font("FiraSans-Regular.ttf", 17), fill=(80, 88, 100))
    d.text((330, 84), "Northgate — today please", font=font("FiraSans-Bold.ttf", 30), fill=(20, 24, 32))
    d.text((330, 128), "From: Marian Cole   To: Theo Marsh", font=font("FiraSans-Regular.ttf", 19), fill=(90, 98, 110))
    body = ["Theo,", "", "The three Northgate invoices need approving", "by EOD. Use my sign-off if Dana asks why.", "No need to loop anyone else in.", "", "Thanks! — M"]
    for i, line in enumerate(body):
        d.text((330, 180 + i * 34), line, font=font("FiraSans-Regular.ttf", 25), fill=(30, 34, 44))
    save(img, "Screens/email_theo.png")

    # Screensaver
    img = Image.new("RGB", (SW, SH), (6, 8, 12))
    d = ImageDraw.Draw(img)
    d.text((620, 380), "HALVORSEN", font=font("FiraSans-Bold.ttf", 48), fill=(63, 167, 181))
    d.text((622, 434), "FREIGHT", font=font("FiraSans-Light.ttf", 34), fill=(63, 167, 181))
    save(img, "Screens/screensaver.png")

    # Remote session
    img = _screen_base((12, 16, 22), (8, 10, 14))
    d = _window(img, "Northgate_Q3.xlsx — Remote Desktop (mcole)", 30, 60, SW - 30, SH - 24)
    cols = [60, 190, 470, 640, 800]
    heads = ["Invoice", "Vendor", "Amount", "Approved", "Account"]
    for c, h in zip(cols, heads):
        d.text((c, 116), h, font=font("FiraSans-Bold.ttf", 20), fill=(30, 36, 48))
    rows = [("NG-0409", "Northgate Supply", "12,400", "T.M.", "••7731"), ("NG-0410", "Northgate Supply", "18,900", "T.M.", "••7731"),
            ("NG-0411", "Northgate Supply", "9,750", "T.M.", "••7731"), ("NG-0412", "Northgate Supply", "48,500", "—", "••7731")]
    for i, r in enumerate(rows):
        y = 156 + i * 40
        if i % 2 == 0:
            d.rectangle((40, y - 6, SW - 40, y + 30), fill=(236, 240, 246))
        for c, v in zip(cols, r):
            d.text((c, y), v, font=font("CourierPrime.ttf", 21), fill=(40, 44, 54))
    d.rectangle((0, 0, SW, 48), fill=(180, 40, 40))
    d.text((24, 10), "REMOTE SESSION ACTIVE   ·   user: mcole   ·   connected 01:12", font=font("FiraSans-Bold.ttf", 24), fill=(255, 240, 240))
    save(img, "Screens/remote_session.png")


TASKS["screens"] = screens



# =============================================================================================
# Story textures: shredded invoice, UV ink marks, notepad rubbing
# =============================================================================================

def shred_invoice():
    W, H = 720, 960
    img = Image.new("RGB", (W, H), (246, 243, 234))
    d = ImageDraw.Draw(img)
    t = font("SpecialElite.ttf", 44)
    m = font("SpecialElite.ttf", 30)
    s = font("CourierPrime.ttf", 26)
    d.text((50, 50), "NORTHGATE SUPPLY CO.", font=t, fill=(30, 32, 40))
    d.text((50, 108), "PO Box 77  ·  no phone listed", font=s, fill=(80, 84, 92))
    d.line((50, 160, W - 50, 160), fill=(60, 60, 70), width=3)
    d.text((50, 190), "INVOICE  NG-0412", font=m, fill=(30, 32, 40))
    d.text((50, 240), "Date: 03/21", font=s, fill=(50, 52, 60))
    d.text((50, 320), "Strategic review services", font=s, fill=(30, 32, 40))
    d.text((W - 270, 320), "$48,500.00", font=m, fill=(30, 32, 40))
    d.line((50, 400, W - 50, 400), fill=(160, 160, 170), width=2)
    d.text((50, 430), "TOTAL DUE", font=m, fill=(30, 32, 40))
    d.text((W - 270, 430), "$48,500.00", font=m, fill=(170, 30, 30))
    d.text((50, 540), "Remit to account  ••7731", font=m, fill=(30, 32, 40))
    d.text((50, 640), "Approved:", font=s, fill=(50, 52, 60))
    d.text((210, 610), "M. Cole", font=font("Caveat.ttf", 90), fill=(25, 40, 110))
    d.rectangle((W - 260, 700, W - 60, 800), outline=(170, 40, 40), width=5)
    d.text((W - 238, 728), "PAID", font=font("SpecialElite.ttf", 54), fill=(170, 40, 40))
    a = np.asarray(img).astype(np.float32)
    rng = np.random.default_rng(3)
    # Strip edges: torn, slightly darker, with tape glints.
    n = 6
    sw = W // n
    for i in range(1, n):
        x = i * sw
        for y in range(H):
            j = int(rng.integers(-3, 4))
            a[y, max(0, x + j - 2):x + j + 2] *= 0.75
    for i in range(n):
        y0 = int(rng.integers(100, 800))
        a[y0:y0 + 46, i * sw + 6:(i + 1) * sw - 6] = a[y0:y0 + 46, i * sw + 6:(i + 1) * sw - 6] * 0.85 + np.array([255, 250, 225]) * 0.15
    save(Image.fromarray(a.clip(0, 255).astype(np.uint8)), "Docs/shred_invoice.png")


def uv_marks():
    def mark(name, draw_fn, size=(512, 512)):
        img = Image.new("L", size, 0)
        d = ImageDraw.Draw(img)
        draw_fn(d)
        img = img.filter(ImageFilter.GaussianBlur(2))
        out = np.zeros((size[1], size[0], 4), np.uint8)
        out[..., :3] = 255
        out[..., 3] = np.asarray(img)
        save(Image.fromarray(out, "RGBA"), f"Uv/{name}.png")
    rng = random.Random(1)

    def arrow(d):
        wobble_line(d, [(60, 256), (420, 256)], 255, 26, rng, 3)
        wobble_line(d, [(420, 256), (320, 160)], 255, 26, rng, 3)
        wobble_line(d, [(420, 256), (320, 352)], 255, 26, rng, 3)
    mark("arrow", arrow)

    def w_sig(d):
        d.text((256, 250), "W", font=font("PermanentMarker.ttf", 300), fill=255, anchor="mm")
    mark("w", w_sig)

    def look_up(d):
        d.text((256, 180), "LOOK", font=font("PermanentMarker.ttf", 120), fill=255, anchor="mm")
        d.text((256, 330), "UP", font=font("PermanentMarker.ttf", 150), fill=255, anchor="mm")
        wobble_line(d, [(256, 500), (256, 420)], 255, 16, rng, 2)
    mark("look_up", look_up)

    def in_here(d):
        d.text((256, 200), "IN", font=font("PermanentMarker.ttf", 150), fill=255, anchor="mm")
        d.text((256, 360), "HERE", font=font("PermanentMarker.ttf", 130), fill=255, anchor="mm")
    mark("in_here", in_here)

    def boxes(d):
        d.text((256, 256), "?? -W", font=font("PermanentMarker.ttf", 120), fill=255, anchor="mm")
    mark("question", boxes)


def notepad_rubbing():
    """Graphite shading with the indented letters left white (revealed by rubbing)."""
    W, H = 420, 560
    rng = np.random.default_rng(5)
    shade = (rng.random((H, W)) * 0.35 + 0.55)
    yy, xx = np.mgrid[0:H, 0:W]
    shade *= 0.85 + 0.15 * np.sin((xx * 0.9 + yy * 0.4) * 0.35)
    letters = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(letters)
    f1 = font("Caveat.ttf", 72)
    f2 = font("Caveat.ttf", 58)
    d.text((40, 70), "NORTHGATE", font=f1, fill=255)
    d.text((40, 170), "wire 48,500", font=f2, fill=255)
    d.text((40, 250), "acct ••7731", font=f2, fill=255)
    d.text((40, 330), "FRI — before", font=f2, fill=255)
    d.text((40, 400), "  audit", font=f2, fill=255)
    lt = np.asarray(letters.filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255
    alpha = np.clip(shade * (1 - lt * 0.95), 0, 1)
    out = np.zeros((H, W, 4), np.uint8)
    out[..., 0] = 70
    out[..., 1] = 72
    out[..., 2] = 80
    out[..., 3] = (alpha * 220).astype(np.uint8)
    save(Image.fromarray(out, "RGBA"), "Grime/notepad_rubbing.png")


def story_textures():
    shred_invoice()
    uv_marks()
    notepad_rubbing()


TASKS["story"] = story_textures


if __name__ == "__main__":
    names = sys.argv[1:] or list(TASKS)
    for n in names:
        TASKS[n]()
