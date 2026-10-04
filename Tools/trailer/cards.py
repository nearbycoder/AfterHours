"""Caption, title-card and end-card artwork for the trailer (1920x1080 RGBA PNGs).

The look follows the game's diegetic paper UI: typewriter titles (Special Elite) on a cream
time-card tag, evidence-red numbers, Fira Sans body text and Caveat handwriting.
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONTS = os.path.join(ROOT, "ArtSource", "fonts")
W, H = 1920, 1080

PAPER = (244, 239, 227)
INK = (27, 34, 48)
RED = (217, 72, 59)
CREAM = (242, 237, 224)
AMBER = (255, 200, 87)
CYAN = (158, 217, 242)
WHITE = (238, 242, 247)


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


TYPE = lambda s: font("SpecialElite.ttf", s)
SANS = lambda s: font("FiraSans-Regular.ttf", s)
SANS_MED = lambda s: font("FiraSans-Medium.ttf", s)
SANS_BOLD = lambda s: font("FiraSans-Bold.ttf", s)
HAND = lambda s: font("Caveat.ttf", s)
MONO = lambda s: font("CourierPrime.ttf", s)


def text_size(text, f, tracking=0):
    w = 0
    for i, ch in enumerate(text):
        w += f.getlength(ch) + (tracking if i < len(text) - 1 else 0)
    asc, desc = f.getmetrics()
    return int(round(w)), asc + desc


def draw_text(img, xy, text, f, fill, tracking=0, anchor="ls"):
    """Draw text with letter spacing. xy is the left baseline (anchor ls) or centre baseline (ms)."""
    d = ImageDraw.Draw(img)
    w, _ = text_size(text, f, tracking)
    x, y = xy
    if anchor == "ms":
        x -= w / 2
    for ch in text:
        d.text((x, y), ch, font=f, fill=fill, anchor="ls")
        x += f.getlength(ch) + tracking
    return w


def shadow(layer, radius, opacity, offset=(0, 0), color=(0, 0, 0)):
    a = layer.split()[3].filter(ImageFilter.GaussianBlur(radius))
    a = a.point(lambda v: int(v * opacity))
    sh = Image.new("RGBA", layer.size, color + (0,))
    sh.putalpha(a)
    out = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    out.alpha_composite(sh, (offset[0], offset[1]))
    return out


def paper_texture(w, h, seed=3):
    rng = np.random.default_rng(seed)
    base = np.array(PAPER, np.float32)[None, None, :] * np.ones((h, w, 1), np.float32)
    grain = rng.normal(0, 3.2, (h, w, 1)).astype(np.float32)
    fib = np.cumsum(rng.normal(0, 0.6, (h, w, 1)), axis=1).astype(np.float32) * 0.05
    img = np.clip(base + grain + fib, 0, 255).astype(np.uint8)
    return Image.fromarray(img, "RGB").convert("RGBA")


def caption(num, title, sub, path):
    """Lower-left feature caption: a time-card tag with a red number and a typewriter title, the
    explanation underneath in Fira Sans."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # Soft dark band behind the text so it reads over bright footage.
    band = Image.new("L", (W, H), 0)
    bd = ImageDraw.Draw(band)
    bd.rectangle((0, 760, 1300, H), fill=150)
    band = band.filter(ImageFilter.GaussianBlur(90))
    dark = Image.new("RGBA", (W, H), (6, 8, 14, 0))
    dark.putalpha(band)
    img.alpha_composite(dark)

    tf = TYPE(58)
    nf = MONO(34)
    nf_bold = font("CourierPrime.ttf", 34)
    tw, _ = text_size(title, tf, 3)
    numw = text_size(num, nf_bold, 2)[0] if num else 0
    pad_l, pad_r = 34, 40
    gap = 26 if num else 0
    tag_w = pad_l + numw + gap + tw + pad_r
    tag_h = 92
    tag = paper_texture(tag_w, tag_h)
    td = ImageDraw.Draw(tag)
    td.rectangle((0, 0, tag_w - 1, 7), fill=(217, 201, 163, 255))           # punch-card stripe
    td.rectangle((0, 0, tag_w - 1, tag_h - 1), outline=(205, 195, 172, 255))
    x = pad_l
    if num:
        draw_text(tag, (x, 61), num, nf_bold, RED + (255,), 2)
        x += numw + gap
        td.line((x - gap / 2, 22, x - gap / 2, tag_h - 16), fill=(196, 182, 150, 255), width=2)
    draw_text(tag, (x, 66), title, tf, INK + (255,), 3)
    mask = Image.new("L", tag.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, tag_w - 1, tag_h - 1), 6, fill=255)
    tag.putalpha(mask)
    tag = tag.rotate(1.1, resample=Image.BICUBIC, expand=True)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    tx, ty = 110, 786
    layer.alpha_composite(tag, (tx, ty))
    img.alpha_composite(shadow(layer, 10, 0.55, (0, 6)))
    img.alpha_composite(layer)

    if sub:
        sf = SANS_MED(36)
        lines = wrap(sub, sf, 1150)
        y = ty + tag.size[1] + 52
        txt = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        for ln in lines:
            draw_text(txt, (tx + 6, y), ln, sf, WHITE + (255,), 0.5)
            y += 46
        img.alpha_composite(shadow(txt, 4, 0.9, (0, 2)))
        img.alpha_composite(txt)
    img.save(path)


def wrap(text, f, width):
    words, lines, cur = text.split(), [], ""
    for w in words:
        t = (cur + " " + w).strip()
        if text_size(t, f)[0] > width and cur:
            lines.append(cur)
            cur = w
        else:
            cur = t
    if cur:
        lines.append(cur)
    return lines


def handwritten(text, path, y=930, size=76, color=CYAN):
    """A handwritten line centred low in frame (cold open, montage)."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    band = Image.new("L", (W, H), 0)
    ImageDraw.Draw(band).rectangle((300, y - 110, W - 300, y + 40), fill=140)
    band = band.filter(ImageFilter.GaussianBlur(80))
    dark = Image.new("RGBA", (W, H), (6, 8, 14, 0))
    dark.putalpha(band)
    img.alpha_composite(dark)
    txt = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw_text(txt, (W / 2, y), text, HAND(size), color + (255,), 0, anchor="ms")
    img.alpha_composite(shadow(txt, 6, 0.9, (0, 3)))
    img.alpha_composite(txt)
    img.save(path)


def logo_layer(y, size=150, glow=True, dim=1.0):
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    col = tuple(int(c * dim) for c in CREAM) + (255,)
    draw_text(layer, (W / 2, y), "AFTER HOURS", TYPE(size), col, 10, anchor="ms")
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if glow:
        g = shadow(layer, 28, 0.55 * dim, color=(255, 170, 80))
        out.alpha_composite(g)
        out.alpha_composite(g)
    out.alpha_composite(shadow(layer, 6, 0.8, (0, 4)))
    out.alpha_composite(layer)
    return out


def title_card(path_logo, path_logo_dim, path_tag):
    """The title: the logo (and a dimmed copy for the tube-light flicker) and the tagline."""
    logo_layer(560).save(path_logo)
    logo_layer(560, glow=False, dim=0.45).save(path_logo_dim)
    tag = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw_text(tag, (W / 2, 660), "Clean the office. Learn its secrets. Decide what survives.", HAND(58), CYAN + (255,), 0, anchor="ms")
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.alpha_composite(shadow(tag, 5, 0.9, (0, 3)))
    out.alpha_composite(tag)
    out.save(path_tag)


def end_card(path_logo, path_info):
    logo_layer(470, size=136).save(path_logo)
    info = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw_text(info, (W / 2, 568), "Clean the office. Learn its secrets. Decide what survives.", HAND(56), CYAN + (255,), 0, anchor="ms")
    draw_text(info, (W / 2, 660), "SEVEN NIGHTS  ·  FOUR ENDINGS  ·  ONE OFFICE", SANS_MED(30), (225, 228, 235, 230), 5, anchor="ms")
    d = ImageDraw.Draw(info)
    d.line((W / 2 - 330, 712, W / 2 + 330, 712), fill=(255, 200, 87, 120), width=2)
    draw_text(info, (W / 2, 785), "github.com/nearbycoder/AfterHours", SANS_MED(44), AMBER + (255,), 0, anchor="ms")
    draw_text(info, (W / 2, 840), "Linux build on GitHub Releases  ·  Made with Unity and Blender", SANS(26), (200, 205, 215, 200), 1, anchor="ms")
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.alpha_composite(shadow(info, 5, 0.85, (0, 3)))
    out.alpha_composite(info)
    out.save(path_info)


def play_overlay(src, dst, label="Watch the trailer"):
    """README poster: a frame with a play button and a label."""
    img = Image.open(src).convert("RGBA")
    w, h = img.size
    veil = Image.new("RGBA", img.size, (5, 7, 12, 70))
    img.alpha_composite(veil)
    r = int(h * 0.11)
    cx, cy = w // 2, h // 2
    btn = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(btn)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(12, 16, 26, 190), outline=AMBER + (255,), width=max(4, r // 18))
    tri = [(cx - r * 0.32, cy - r * 0.48), (cx - r * 0.32, cy + r * 0.48), (cx + r * 0.55, cy)]
    d.polygon(tri, fill=AMBER + (255,))
    img.alpha_composite(shadow(btn, 12, 0.6, (0, 6)))
    img.alpha_composite(btn)
    lab = Image.new("RGBA", img.size, (0, 0, 0, 0))
    draw_text(lab, (cx, cy + r + int(h * 0.075)), label.upper(), TYPE(int(h * 0.05)), CREAM + (255,), 4, anchor="ms")
    img.alpha_composite(shadow(lab, 6, 0.9, (0, 3)))
    img.alpha_composite(lab)
    img.convert("RGB").save(dst, quality=88, optimize=True, progressive=True)
