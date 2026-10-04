"""Builds every movable / spawnable prop into Assets/Resources/Models/Props.fbx.

    blender -b -P ArtSource/props.py -- [--render DIR] [--only a,b]

Each prop is one top-level object named PROP_<id>, pivot at its bottom centre, front facing
Unity +Z. Unity instantiates them by name (PropLibrary.cs). Child objects named
*_screen, *_shade etc. are kept separate where Unity needs to drive them.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ah_lib as L  # noqa: E402
import furniture as F  # noqa: E402
from ah_lib import MeshBuilder, col, glass, glow, tex  # noqa: E402

PROPS = {}


def prop(name):
    def deco(fn):
        PROPS[name] = fn
        return fn
    return deco


PAPER = col("F3F1EA", 20)
CARD = col("B88A55", 15)
WHITE_PLASTIC = col("ECECE8", 45)
BLACK = col("1D2024", 40)
DARK = col("2A2E33", 45)
GREEN_LEAF = col("3E7A45", 35)
DARK_LEAF = col("2C5A35", 35)
TERRACOTTA = col("B8643F", 20)
POT_WHITE = col("E7E4DC", 55)
POT_GREY = col("5B6168", 40)


def irregular_sphere(mb, center, radius, mat, seed=0, amount=0.25, subdiv=3, squash=(1, 1, 1)):
    tmp = bmesh.new()
    bmesh.ops.create_icosphere(tmp, subdivisions=subdiv, radius=radius)
    rnd = random.Random(seed)
    off = Vector((rnd.random() * 10, rnd.random() * 10, rnd.random() * 10))
    for v in tmp.verts:
        d = v.co.normalized()
        n = noise.noise(d * 2.2 + off) * 0.6 + noise.noise(d * 5.0 + off) * 0.4
        r = radius * (1 + amount * n)
        v.co = Vector((d.x * r * squash[0], d.y * r * squash[1], d.z * r * squash[2]))
    c = Vector(center)
    mb._merge(tmp, mat, lambda co: co + c)
    tmp.free()


def leaf(mb, base, direction, length, width, mat, droop=0.25, segments=6):
    """A curved strap leaf from base along direction (Unity space)."""
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 1, 0)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    pts = []
    for i in range(segments + 1):
        t = i / segments
        p = Vector(base) + d * (length * t) + Vector((0, -droop * length * t * t, 0))
        w = width * math.sin(math.pi * min(1.0, t * 1.15 + 0.08))
        pts.append((p - side * w / 2, p + side * w / 2))
    for i in range(segments):
        a0, a1 = pts[i]
        b0, b1 = pts[i + 1]
        mb.quad([a0, a1, b1, b0], mat)
        mb.quad([b0, b1, a1, a0], mat)


def finish(mb, name):
    obj = mb.build()
    obj.name = "PROP_" + name
    return obj


# =============================================================================================
# Desk kit
# =============================================================================================

@prop("monitor")
def monitor():
    mb = MeshBuilder("monitor")
    mb.box((0, 0.012, -0.03), (0.24, 0.024, 0.18), DARK, bevel=0.01)
    mb.box((0, 0.17, -0.07), (0.05, 0.3, 0.025), DARK, bevel=0.008)
    mb.box((0, 0.36, -0.035), (0.58, 0.35, 0.03), BLACK, bevel=0.008)
    mb.box((0, 0.375, -0.0185), (0.55, 0.31, 0.004), "screen_monitor")
    mb.box((0.25, 0.205, -0.018), (0.012, 0.006, 0.004), glow("5FFFB5", 20))
    return finish(mb, "monitor")


@prop("keyboard")
def keyboard():
    mb = MeshBuilder("keyboard")
    mb.box((0, 0.01, 0), (0.44, 0.02, 0.14), col("2B2E33", 45), bevel=0.006)
    for r in range(4):
        for c in range(14):
            mb.box((-0.2 + c * 0.0305, 0.023, -0.045 + r * 0.03), (0.026, 0.008, 0.025), col("3A3E45", 35), bevel=0.003)
    mb.box((0.0, 0.023, 0.075 - 0.002), (0.16, 0.008, 0.024), col("3A3E45", 35), bevel=0.003)
    return finish(mb, "keyboard")


@prop("mouse")
def mouse():
    mb = MeshBuilder("mouse")
    irregular_sphere(mb, (0, 0.016, 0), 0.03, col("2B2E33", 55), amount=0.0, squash=(0.9, 0.55, 1.6), subdiv=2)
    return finish(mb, "mouse")


@prop("desk_lamp")
def desk_lamp():
    mb = MeshBuilder("desk_lamp")
    m = col("2B2E33", 55, 60)
    mb.cylinder((0, 0.012, 0), 0.09, 0.024, m, segments=28)
    mb.tube((0, 0.02, -0.02), (0, 0.4, -0.06), 0.012, m, segments=12)
    mb.sphere((0, 0.4, -0.06), 0.018, m, segments=10, rings=6)
    mb.tube((0, 0.4, -0.06), (0, 0.47, 0.18), 0.011, m, segments=12)
    mb.tube((0, 0.5, 0.15), (0, 0.4, 0.27), 0.075, col("E8C46A", 45, 30), segments=24, radius_b=0.035)
    mb.sphere((0, 0.43, 0.24), 0.028, glow("FFD7A0", 40), segments=12, rings=8)
    return finish(mb, "desk_lamp")


@prop("desk_phone")
def desk_phone():
    mb = MeshBuilder("desk_phone")
    mb.box((0, 0.035, 0), (0.2, 0.06, 0.2), col("2E3136", 45), bevel=0.012, rot_x=-10)
    mb.box((-0.06, 0.08, 0), (0.06, 0.035, 0.19), col("26292D", 50), bevel=0.012)
    for r in range(4):
        for c in range(3):
            mb.box((0.0 + c * 0.03, 0.068, -0.04 + r * 0.025), (0.02, 0.006, 0.016), col("C8CCD2", 30), bevel=0.002)
    mb.box((0.055, 0.07, 0.07), (0.07, 0.004, 0.04), glow("FF8A3D", 10))
    return finish(mb, "desk_phone")


@prop("laptop")
def laptop():
    mb = MeshBuilder("laptop")
    m = col("A9AFB6", 60, 70)
    mb.box((0, 0.009, 0), (0.33, 0.018, 0.23), m, bevel=0.006)
    mb.box((0, 0.13, -0.115), (0.33, 0.23, 0.01), m, bevel=0.005, rot_x=-12)
    return finish(mb, "laptop")


@prop("inbox_tray")
def inbox_tray():
    mb = MeshBuilder("inbox_tray")
    m = col("2F3338", 45, 30)
    for y in (0.0, 0.08):
        mb.box((0, y + 0.004, 0), (0.27, 0.008, 0.34), m, bevel=0.003)
        mb.box((-0.13, y + 0.03, 0), (0.008, 0.05, 0.34), m, bevel=0.002)
        mb.box((0.13, y + 0.03, 0), (0.008, 0.05, 0.34), m, bevel=0.002)
        mb.box((0, y + 0.03, -0.165), (0.27, 0.05, 0.008), m, bevel=0.002)
        mb.box((0, y + 0.015, 0.165), (0.27, 0.02, 0.008), m, bevel=0.002)
    for sx in (-1, 1):
        mb.cylinder((sx * 0.12, 0.04, 0.15), 0.004, 0.08, m, segments=8)
        mb.cylinder((sx * 0.12, 0.04, -0.15), 0.004, 0.08, m, segments=8)
    mb.box((0, 0.065, 0.172), (0.12, 0.022, 0.004), col("F4F1E8", 20))
    return finish(mb, "inbox_tray")


@prop("pen_cup")
def pen_cup():
    mb = MeshBuilder("pen_cup")
    mb.cylinder((0, 0.05, 0), 0.035, 0.1, col("3A4A5C", 40, 40), segments=18)
    for i, (c, a) in enumerate([("2E64C8", 0), ("D9483B", 70), ("1D2024", 150), ("FFC845", 230)]):
        r = math.radians(a)
        mb.cylinder((math.cos(r) * 0.015, 0.11, math.sin(r) * 0.015), 0.004, 0.14, col(c, 50), segments=8, tilt=("Z", 8 * math.cos(r)))
    return finish(mb, "pen_cup")


@prop("stapler")
def stapler():
    mb = MeshBuilder("stapler")
    mb.box((0, 0.01, 0), (0.045, 0.02, 0.16), BLACK, bevel=0.006)
    mb.box((0, 0.035, -0.005), (0.04, 0.025, 0.15), col("C8312B", 55), bevel=0.01)
    return finish(mb, "stapler")


@prop("photo_frame")
def photo_frame():
    mb = MeshBuilder("photo_frame")
    mb.box((0, 0.08, 0), (0.15, 0.19, 0.015), col("8A6A4A", 40), bevel=0.004, rot_x=-12)
    mb.box((0, 0.082, 0.004), (0.12, 0.155, 0.01), tex("photo"), rot_x=-12, uv_fit=True)
    mb.box((0, 0.06, -0.05), (0.02, 0.12, 0.004), col("8A6A4A", 40), rot_x=35)
    return finish(mb, "photo_frame")


@prop("calendar_desk")
def calendar_desk():
    mb = MeshBuilder("calendar_desk")
    mb.box((0, 0.05, 0), (0.18, 0.1, 0.002), PAPER, rot_x=-25)
    mb.box((0, 0.05, -0.04), (0.18, 0.1, 0.002), CARD, rot_x=25)
    return finish(mb, "calendar_desk")


# =============================================================================================
# Drinkware, trash, food
# =============================================================================================

def _mug(name, body, band=None):
    mb = MeshBuilder(name)
    mb.cylinder((0, 0.047, 0), 0.042, 0.094, body, segments=24)
    mb.cylinder((0, 0.093, 0), 0.036, 0.004, col("3B2416", 70), segments=20)
    if band:
        mb.cylinder((0, 0.06, 0), 0.0425, 0.02, band, segments=24)
    # handle as a few boxes
    for (y, z, h) in ((0.075, 0.0, 0.012), (0.02, 0.0, 0.012)):
        mb.box((0.055, y, z), (0.03, h, 0.014), body, bevel=0.005)
    mb.box((0.068, 0.047, 0), (0.012, 0.064, 0.014), body, bevel=0.005)
    return finish(mb, name)


@prop("mug_white")
def mug_white():
    return _mug("mug_white", col("F2F0EA", 70), col("2E64C8", 60))


@prop("mug_red")
def mug_red():
    return _mug("mug_red", col("C8312B", 65))


@prop("mug_teal")
def mug_teal():
    return _mug("mug_teal", col("2F7F86", 65), col("F2F0EA", 60))


@prop("mug_yellow")
def mug_yellow():
    return _mug("mug_yellow", col("F2C14E", 65))


@prop("paper_cup")
def paper_cup():
    mb = MeshBuilder("paper_cup")
    mb.cylinder((0, 0.06, 0), 0.034, 0.12, col("F4EFE6", 25), segments=20, radius_top=0.044)
    mb.cylinder((0, 0.07, 0), 0.042, 0.04, col("7A4A2A", 20), segments=20, radius_top=0.0445)
    mb.cylinder((0, 0.124, 0), 0.046, 0.01, col("F4EFE6", 35), segments=20, radius_top=0.04)
    return finish(mb, "paper_cup")


@prop("soda_can")
def soda_can():
    mb = MeshBuilder("soda_can")
    mb.cylinder((0, 0.061, 0), 0.033, 0.112, col("D9332B", 70, 60), segments=20)
    mb.cylinder((0, 0.119, 0), 0.028, 0.006, col("C9CDD2", 75, 90), segments=20)
    mb.cylinder((0, 0.003, 0), 0.028, 0.006, col("C9CDD2", 75, 90), segments=20)
    mb.cylinder((0, 0.07, 0), 0.0332, 0.03, col("F2F0EA", 70, 40), segments=20)
    return finish(mb, "soda_can")


@prop("energy_can")
def energy_can():
    mb = MeshBuilder("energy_can")
    mb.cylinder((0, 0.08, 0), 0.027, 0.15, col("1E2A22", 70, 60), segments=20)
    mb.cylinder((0, 0.08, 0), 0.0272, 0.05, col("7CFF4F", 60, 40), segments=20)
    mb.cylinder((0, 0.157, 0), 0.023, 0.006, col("C9CDD2", 75, 90), segments=20)
    return finish(mb, "energy_can")


@prop("water_bottle")
def water_bottle():
    mb = MeshBuilder("water_bottle")
    mb.cylinder((0, 0.095, 0), 0.033, 0.19, glass("BFE3F2", 35), segments=18)
    mb.cylinder((0, 0.2, 0), 0.016, 0.03, glass("BFE3F2", 35), segments=12, radius_top=0.012)
    mb.cylinder((0, 0.222, 0), 0.015, 0.016, col("2E64C8", 50), segments=12)
    mb.cylinder((0, 0.1, 0), 0.0335, 0.06, col("6FB7E8", 30), segments=18)
    return finish(mb, "water_bottle")


@prop("paper_ball")
def paper_ball():
    mb = MeshBuilder("paper_ball")
    irregular_sphere(mb, (0, 0.035, 0), 0.036, PAPER, seed=3, amount=0.35, subdiv=2)
    return finish(mb, "paper_ball")


@prop("paper_ball_yellow")
def paper_ball_yellow():
    mb = MeshBuilder("paper_ball_yellow")
    irregular_sphere(mb, (0, 0.025, 0), 0.026, col("F7D96B", 20), seed=5, amount=0.35, subdiv=2)
    return finish(mb, "paper_ball_yellow")


@prop("paper_sheet")
def paper_sheet():
    mb = MeshBuilder("paper_sheet")
    mb.box((0, 0.0012, 0), (0.21, 0.0024, 0.297), tex("paper-printed", s=10), uv_fit=True)
    return finish(mb, "paper_sheet")


@prop("paper_stack")
def paper_stack():
    mb = MeshBuilder("paper_stack")
    for i in range(6):
        mb.box((random.Random(i).uniform(-0.008, 0.008), 0.003 + i * 0.004, 0), (0.21, 0.004, 0.297), PAPER, rot_y=random.Random(i + 9).uniform(-4, 4))
    mb.box((0, 0.028, 0), (0.21, 0.002, 0.297), tex("paper-printed", s=10), rot_y=2, uv_fit=True)
    return finish(mb, "paper_stack")


@prop("folder_manila")
def folder_manila():
    mb = MeshBuilder("folder_manila")
    mb.box((0, 0.004, 0), (0.24, 0.006, 0.31), col("D8B878", 15), bevel=0.001)
    mb.box((0.02, 0.009, -0.02), (0.22, 0.004, 0.29), col("E2C68A", 15), bevel=0.001)
    mb.box((0.06, 0.012, 0.15), (0.07, 0.003, 0.02), col("E2C68A", 15))
    return finish(mb, "folder_manila")


@prop("folder_red")
def folder_red():
    mb = MeshBuilder("folder_red")
    mb.box((0, 0.008, 0), (0.25, 0.016, 0.32), col("A8302A", 30), bevel=0.002)
    mb.box((0.005, 0.0165, 0.0), (0.235, 0.003, 0.305), col("B83A33", 30))
    mb.box((0.0, 0.019, 0.08), (0.12, 0.002, 0.05), col("F4F1E8", 20))
    mb.box((0.125, 0.008, 0.0), (0.01, 0.018, 0.08), col("1D2024", 40))
    return finish(mb, "folder_red")


@prop("binder")
def binder():
    mb = MeshBuilder("binder")
    mb.box((0, 0.15, 0), (0.06, 0.3, 0.26), col("2E5A8A", 35), bevel=0.006)
    mb.box((-0.031, 0.18, 0), (0.002, 0.08, 0.18), col("F4F1E8", 20))
    return finish(mb, "binder")


@prop("sticky_pad")
def sticky_pad():
    mb = MeshBuilder("sticky_pad")
    mb.box((0, 0.008, 0), (0.076, 0.016, 0.076), col("FFD54A", 15), bevel=0.001)
    return finish(mb, "sticky_pad")


@prop("sticky_note")
def sticky_note():
    mb = MeshBuilder("sticky_note")
    mb.box((0, 0.0005, 0), (0.076, 0.001, 0.076), tex("sticky", s=10), uv_fit=True)
    return finish(mb, "sticky_note")


@prop("envelope")
def envelope():
    mb = MeshBuilder("envelope")
    mb.box((0, 0.003, 0), (0.23, 0.006, 0.12), col("EFE7D6", 20), bevel=0.001)
    mb.box((0, 0.0065, 0.035), (0.21, 0.001, 0.045), col("E2D8C2", 20))
    return finish(mb, "envelope")


@prop("key")
def key():
    mb = MeshBuilder("key")
    brass = col("C9A247", 75, 100)
    mb.cylinder((0, 0.002, -0.022), 0.012, 0.004, brass, segments=16)
    mb.box((0, 0.002, 0.012), (0.007, 0.004, 0.05), brass)
    for i in range(3):
        mb.box((0.005, 0.002, 0.02 + i * 0.01), (0.006, 0.004, 0.004), brass)
    mb.box((-0.03, 0.001, -0.03), (0.035, 0.002, 0.02), col("F2EBD8", 20), rot_y=30)
    return finish(mb, "key")


@prop("pizza_box")
def pizza_box():
    mb = MeshBuilder("pizza_box")
    mb.box((0, 0.022, 0), (0.4, 0.044, 0.4), CARD, bevel=0.004)
    mb.box((0, 0.045, 0), (0.25, 0.002, 0.18), col("C8312B", 30))
    return finish(mb, "pizza_box")


@prop("takeout_box")
def takeout_box():
    mb = MeshBuilder("takeout_box")
    mb.box((0, 0.045, 0), (0.1, 0.09, 0.085), col("F4F1EA", 30), bevel=0.01)
    mb.box((0, 0.06, 0.044), (0.06, 0.04, 0.002), col("C8312B", 30))
    mb.cylinder((0, 0.1, 0), 0.003, 0.03, col("A9AFB6", 60, 90), segments=6, axis="x")
    return finish(mb, "takeout_box")


@prop("party_plate")
def party_plate():
    mb = MeshBuilder("party_plate")
    mb.cylinder((0, 0.006, 0), 0.11, 0.012, col("FFFFFF", 25), segments=28, radius_top=0.125)
    mb.cylinder((0, 0.0125, 0), 0.07, 0.002, col("E6D3B8", 20), segments=20)
    irregular_sphere(mb, (0.03, 0.02, 0.02), 0.018, col("8A4A2A", 25), seed=2, amount=0.4, subdiv=1, squash=(1.4, 0.5, 1))
    return finish(mb, "party_plate")


@prop("banana_peel")
def banana_peel():
    mb = MeshBuilder("banana_peel")
    y = col("F2CF3E", 30)
    for i, a in enumerate((0, 120, 240)):
        r = math.radians(a)
        leaf(mb, (0, 0.02, 0), (math.cos(r), -0.1, math.sin(r)), 0.12, 0.04, y, droop=0.1)
    mb.cylinder((0, 0.03, 0), 0.012, 0.04, col("6B5A2A", 25), segments=8)
    return finish(mb, "banana_peel")


@prop("party_hat")
def party_hat():
    mb = MeshBuilder("party_hat")
    mb.cylinder((0, 0.08, 0), 0.06, 0.16, col("C58CFF", 40), segments=20, radius_top=0.004)
    mb.sphere((0, 0.165, 0), 0.015, col("FFC845", 40), segments=10, rings=6)
    return finish(mb, "party_hat")


@prop("archive_box")
def archive_box():
    mb = MeshBuilder("archive_box")
    mb.box((0, 0.13, 0), (0.4, 0.26, 0.31), col("C9A97A", 15), bevel=0.006)
    mb.box((0, 0.265, 0), (0.41, 0.03, 0.32), col("BF9D6C", 15), bevel=0.006)
    mb.box((0, 0.14, 0.157), (0.16, 0.08, 0.004), col("F4F1E8", 15))
    return finish(mb, "archive_box")


@prop("water_glass")
def water_glass():
    mb = MeshBuilder("water_glass")
    mb.cylinder((0, 0.055, 0), 0.032, 0.11, glass("D6EEF5", 25), segments=20, radius_top=0.036)
    return finish(mb, "water_glass")


@prop("book")
def book():
    mb = MeshBuilder("book")
    mb.box((0, 0.11, 0), (0.035, 0.22, 0.16), col("6B2E2E", 35), bevel=0.003)
    mb.box((0.0005, 0.11, 0.0015), (0.034, 0.21, 0.155), PAPER)
    return finish(mb, "book")


@prop("award_plaque")
def award_plaque():
    mb = MeshBuilder("award_plaque")
    mb.box((0, 0.11, 0), (0.18, 0.22, 0.02), col("4A2E1E", 50), bevel=0.004, rot_x=-10)
    mb.box((0, 0.11, 0.012), (0.13, 0.15, 0.004), col("D4AF37", 80, 100), rot_x=-10)
    mb.box((0, 0.05, -0.05), (0.03, 0.1, 0.006), col("4A2E1E", 50), rot_x=35)
    return finish(mb, "award_plaque")


@prop("notepad")
def notepad():
    mb = MeshBuilder("notepad")
    mb.box((0, 0.006, 0), (0.21, 0.012, 0.3), tex("legal-pad", s=10), uv_fit=True)
    mb.box((0, 0.0125, -0.14), (0.21, 0.004, 0.02), col("8A3A2A", 30))
    return finish(mb, "notepad")


@prop("card_box")
def card_box():
    mb = MeshBuilder("card_box")
    mb.box((0, 0.15, 0), (0.45, 0.3, 0.35), col("B88A55", 15), bevel=0.004)
    for sx in (-1, 1):
        mb.box((sx * 0.12, 0.31, 0), (0.22, 0.004, 0.35), col("AE8150", 15), rot_z=sx * -25)
    return finish(mb, "card_box")


# =============================================================================================
# Plants
# =============================================================================================

@prop("plant_monstera")
def plant_monstera():
    mb = MeshBuilder("plant_monstera")
    mb.cylinder((0, 0.2, 0), 0.2, 0.4, POT_WHITE, segments=28, radius_top=0.23, bevel=0.01)
    mb.cylinder((0, 0.39, 0), 0.21, 0.02, col("3A2A1E", 5), segments=24)
    rnd = random.Random(4)
    for i in range(14):
        a = rnd.uniform(0, math.tau)
        up = rnd.uniform(0.6, 1.4)
        d = (math.cos(a), up, math.sin(a))
        base = (math.cos(a) * 0.05, 0.4, math.sin(a) * 0.05)
        stem_len = rnd.uniform(0.35, 0.8)
        tip = Vector(base) + Vector(d).normalized() * stem_len
        mb.tube(base, tip, 0.006, GREEN_LEAF, segments=6)
        leaf(mb, tuple(tip), (math.cos(a), -0.2, math.sin(a)), rnd.uniform(0.25, 0.38), rnd.uniform(0.22, 0.3), DARK_LEAF if i % 3 == 0 else GREEN_LEAF, droop=0.35)
    return finish(mb, "plant_monstera")


@prop("plant_snake")
def plant_snake():
    mb = MeshBuilder("plant_snake")
    mb.cylinder((0, 0.15, 0), 0.13, 0.3, POT_GREY, segments=24, radius_top=0.15, bevel=0.01)
    rnd = random.Random(7)
    for i in range(11):
        a = rnd.uniform(0, math.tau)
        lean = rnd.uniform(0.05, 0.25)
        leaf(mb, (math.cos(a) * 0.04, 0.29, math.sin(a) * 0.04), (math.cos(a) * lean, 1, math.sin(a) * lean), rnd.uniform(0.45, 0.8), 0.06, col("4F7A3A", 40) if i % 2 else col("6B8F3E", 40), droop=0.02)
    return finish(mb, "plant_snake")


@prop("plant_succulent")
def plant_succulent():
    mb = MeshBuilder("plant_succulent")
    mb.cylinder((0, 0.035, 0), 0.045, 0.07, TERRACOTTA, segments=18, radius_top=0.052)
    for ring in range(3):
        for i in range(6 + ring * 2):
            a = i / (6 + ring * 2) * math.tau + ring * 0.4
            r = 0.012 + ring * 0.012
            leaf(mb, (math.cos(a) * 0.005, 0.07 + ring * 0.006, math.sin(a) * 0.005), (math.cos(a) * r * 30, 0.5 - ring * 0.2, math.sin(a) * r * 30), 0.03 + ring * 0.008, 0.018, col("7FB38A", 30), droop=0.0, segments=3)
    return finish(mb, "plant_succulent")


@prop("orchid")
def orchid():
    mb = MeshBuilder("orchid")
    mb.cylinder((0, 0.06, 0), 0.06, 0.12, col("F2F2EE", 70), segments=22, radius_top=0.065)
    for i, a in enumerate((20, 160, 260)):
        r = math.radians(a)
        leaf(mb, (0, 0.12, 0), (math.cos(r), 0.1, math.sin(r)), 0.14, 0.05, col("2F6B3A", 50), droop=0.15)
    mb.cylinder((0.01, 0.3, 0), 0.003, 0.36, col("4A6B3A", 30), segments=6, tilt=("Z", -6))
    for i in range(6):
        t = i / 5
        p = (0.02 + t * 0.09, 0.42 + math.sin(t * 2.5) * 0.04, 0.0)
        for k in range(5):
            a = k / 5 * math.tau
            leaf(mb, p, (0.2, math.cos(a), math.sin(a)), 0.028, 0.024, col("F4E9F2", 30) if i % 2 else col("E9C8E3", 30), droop=0.0, segments=2)
    return finish(mb, "orchid")


# =============================================================================================
# Bins, appliances, equipment
# =============================================================================================

def _bin(name, body, rim, h=0.36, r=0.15, label=None):
    mb = MeshBuilder(name)
    mb.cylinder((0, h / 2, 0), r * 0.85, h, body, segments=26, radius_top=r, cap=False)
    mb.cylinder((0, 0.006, 0), r * 0.85, 0.012, body, segments=26)
    mb.cylinder((0, h - 0.008, 0), r + 0.006, 0.016, rim, segments=26, cap=False)
    # inner wall (so you see into the bin)
    mb.cylinder((0, h / 2 + 0.01, 0), r * 0.83, h - 0.02, col("15181B", 15), segments=26, radius_top=r * 0.97, cap=False)
    if label:
        mb.box((0, h * 0.6, r * 0.93), (0.1, 0.1, 0.004), label, rot_x=-5)
    return finish(mb, name)


@prop("bin_trash")
def bin_trash():
    return _bin("bin_trash", col("26292D", 35), col("33373C", 40))


@prop("bin_recycle")
def bin_recycle():
    return _bin("bin_recycle", col("2E6FC8", 40), col("2A63B0", 45), h=0.42, r=0.17, label=col("F4F4F0", 30))


@prop("bin_break")
def bin_break():
    mb = MeshBuilder("bin_break")
    mb.box((0, 0.34, 0), (0.38, 0.68, 0.32), col("B9C0C7", 60, 80), bevel=0.03)
    mb.box((0, 0.69, 0), (0.39, 0.04, 0.33), col("2A2E33", 45, 60), bevel=0.015)
    mb.box((0, 0.6, 0.17), (0.12, 0.08, 0.006), col("3A3E45", 30))
    return finish(mb, "bin_break")


@prop("shredder")
def shredder():
    mb = MeshBuilder("shredder")
    mb.box((0, 0.27, 0), (0.36, 0.54, 0.26), col("2B2E33", 40), bevel=0.02)
    mb.box((0, 0.58, 0), (0.37, 0.09, 0.27), col("3A3E45", 45), bevel=0.02)
    mb.box((0, 0.627, 0.0), (0.26, 0.006, 0.012), col("0A0B0C", 10))
    mb.box((0.13, 0.627, 0.08), (0.03, 0.006, 0.03), glow("5FFF8F", 20))
    mb.box((0, 0.27, 0.131), (0.3, 0.42, 0.006), glass("6E7A85", 40))
    return finish(mb, "shredder")


@prop("microwave")
def microwave():
    mb = MeshBuilder("microwave")
    mb.box((0, 0.15, 0), (0.5, 0.3, 0.36), col("2A2E33", 45, 40), bevel=0.012)
    mb.box((-0.06, 0.15, 0.181), (0.33, 0.24, 0.006), glass("1A1E22", 60))
    mb.box((0.18, 0.15, 0.181), (0.1, 0.25, 0.006), col("1E2125", 40))
    mb.box((0.18, 0.24, 0.186), (0.06, 0.02, 0.004), glow("7CFFB0", 15))
    mb.box((0.11, 0.15, 0.2), (0.02, 0.18, 0.025), col("C9CDD2", 70, 90), bevel=0.006)
    return finish(mb, "microwave")


@prop("coffee_machine")
def coffee_machine():
    mb = MeshBuilder("coffee_machine")
    mb.box((0, 0.2, -0.04), (0.26, 0.4, 0.22), col("1F2226", 50, 30), bevel=0.02)
    mb.box((0, 0.36, 0.07), (0.26, 0.08, 0.2), col("1F2226", 50, 30), bevel=0.02)
    mb.box((0, 0.02, 0.07), (0.24, 0.04, 0.2), col("A9AFB6", 60, 80), bevel=0.01)
    mb.cylinder((0, 0.1, 0.08), 0.07, 0.13, glass("3A2416", 70), segments=20)
    mb.box((0.09, 0.37, 0.172), (0.03, 0.02, 0.004), glow("FF5A3D", 25))
    return finish(mb, "coffee_machine")


@prop("dish_rack")
def dish_rack():
    mb = MeshBuilder("dish_rack")
    m = col("C9CDD2", 60, 80)
    mb.box((0, 0.006, 0), (0.42, 0.012, 0.3), m, bevel=0.004)
    for i in range(7):
        mb.box((-0.18 + i * 0.06, 0.05, 0), (0.006, 0.09, 0.28), m)
    return finish(mb, "dish_rack")


@prop("punch_clock")
def punch_clock():
    mb = MeshBuilder("punch_clock")
    mb.box((0, 0, 0.07), (0.28, 0.36, 0.14), col("C9B48A", 45, 20), bevel=0.02)
    mb.cylinder((0, 0.07, 0.142), 0.085, 0.01, col("F2EEE2", 40), segments=28, axis="z")
    mb.cylinder((0, 0.07, 0.146), 0.088, 0.006, col("6B5A3A", 50, 60), segments=28, axis="z", cap=False)
    mb.box((0, -0.11, 0.145), (0.16, 0.012, 0.01), col("1D2024", 30))
    mb.box((0, -0.15, 0.13), (0.2, 0.03, 0.02), col("8A7A5A", 45, 30), bevel=0.006)
    # card rack beside it
    mb.box((0.27, -0.02, 0.025), (0.16, 0.34, 0.05), col("6B5A3A", 40), bevel=0.006)
    for i in range(5):
        mb.box((0.27, 0.12 - i * 0.06, 0.055), (0.12, 0.05, 0.004), col("EFE7D6", 20))
    return finish(mb, "punch_clock")


@prop("wall_clock")
def wall_clock():
    mb = MeshBuilder("wall_clock")
    mb.cylinder((0, 0, 0.02), 0.17, 0.04, col("2A2E33", 50), segments=36, axis="z")
    mb.cylinder((0, 0, 0.041), 0.155, 0.004, col("F4F2EC", 40), segments=36, axis="z")
    for i in range(12):
        a = i / 12 * math.tau
        mb.box((math.sin(a) * 0.13, math.cos(a) * 0.13, 0.044), (0.008, 0.022, 0.002), col("1D2024", 30), rot_z=-math.degrees(a))
    return finish(mb, "wall_clock")


@prop("clock_hand_h")
def clock_hand_h():
    mb = MeshBuilder("clock_hand_h")
    mb.box((0, 0.04, 0.002), (0.012, 0.09, 0.003), col("1D2024", 30))
    return finish(mb, "clock_hand_h")


@prop("clock_hand_m")
def clock_hand_m():
    mb = MeshBuilder("clock_hand_m")
    mb.box((0, 0.06, 0.002), (0.008, 0.13, 0.003), col("1D2024", 30))
    return finish(mb, "clock_hand_m")


@prop("fire_extinguisher")
def fire_extinguisher():
    mb = MeshBuilder("fire_extinguisher")
    mb.cylinder((0, 0.25, 0), 0.08, 0.5, col("C8312B", 55, 20), segments=22, bevel=0.01)
    mb.cylinder((0, 0.53, 0), 0.03, 0.06, col("1D2024", 40), segments=12)
    mb.box((0.03, 0.57, 0), (0.1, 0.02, 0.03), col("1D2024", 40), bevel=0.005)
    mb.box((0, 0.3, 0.081), (0.08, 0.12, 0.002), col("F4F2EC", 20))
    return finish(mb, "fire_extinguisher")


@prop("janitor_cart")
def janitor_cart():
    mb = MeshBuilder("janitor_cart")
    y = col("F4C430", 45)
    g = col("3A3E45", 40)
    mb.box((0, 0.12, 0), (0.9, 0.04, 0.5), y, bevel=0.01)
    mb.box((0, 0.6, -0.05), (0.9, 0.03, 0.4), y, bevel=0.01)
    mb.box((0, 0.95, -0.05), (0.9, 0.03, 0.4), y, bevel=0.01)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.cylinder((sx * 0.42, 0.53, sz * 0.22 - 0.03), 0.014, 0.85, g, segments=10)
            mb.cylinder((sx * 0.38, 0.05, sz * 0.2), 0.045, 0.03, col("1D2024", 30), segments=16, axis="x")
    mb.cylinder((0.42, 1.0, 0.0), 0.016, 0.5, g, segments=10, axis="z")
    # vinyl trash bag on the end
    mb.box((-0.62, 0.55, 0), (0.32, 0.9, 0.42), col("2B2E33", 30), bevel=0.02)
    mb.cylinder((-0.62, 0.55, 0), 0.19, 0.95, col("15181B", 55), segments=20)
    # supplies
    for i, c in enumerate(("2E64C8", "7CC8A0", "F08A24")):
        mb.cylinder((-0.25 + i * 0.12, 0.72, -0.1), 0.035, 0.2, col(c, 50), segments=14)
        mb.box((-0.25 + i * 0.12, 0.84, -0.09), (0.02, 0.05, 0.05), col("ECECE8", 40))
    mb.box((0.2, 1.02, -0.05), (0.25, 0.12, 0.2), col("ECECE8", 30), bevel=0.01)
    mb.box((0.2, 0.18, 0), (0.4, 0.14, 0.32), col("4F6B7A", 40), bevel=0.02)
    return finish(mb, "janitor_cart")


@prop("mop_bucket")
def mop_bucket():
    mb = MeshBuilder("mop_bucket")
    y = col("F4C430", 45)
    mb.box((0, 0.2, 0), (0.42, 0.36, 0.32), y, bevel=0.04)
    mb.box((0, 0.36, 0), (0.38, 0.02, 0.28), glass("6E8FA0", 60))
    mb.box((0.14, 0.45, 0), (0.14, 0.2, 0.3), col("3A3E45", 40), bevel=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.cylinder((sx * 0.18, 0.025, sz * 0.13), 0.025, 0.02, col("1D2024", 30), segments=12, axis="x")
    return finish(mb, "mop_bucket")


# =============================================================================================
# Seating
# =============================================================================================

def _star_base(mb, m, r=0.32):
    mb.cylinder((0, 0.25, 0), 0.025, 0.28, col("A9AFB6", 70, 90), segments=14)
    for i in range(5):
        a = i / 5 * math.tau
        mb.box((math.sin(a) * r / 2, 0.075, math.cos(a) * r / 2), (0.04, 0.035, r), m, bevel=0.01, rot_y=math.degrees(a))
        mb.sphere((math.sin(a) * r, 0.03, math.cos(a) * r), 0.03, col("1D2024", 40), segments=10, rings=6)


@prop("chair_office")
def chair_office():
    mb = MeshBuilder("chair_office")
    m = col("26292D", 40)
    _star_base(mb, m)
    mb.box((0, 0.46, 0.02), (0.5, 0.08, 0.48), F.CHAR_FABRIC, bevel=0.03, segments=3)
    mb.box((0, 0.42, 0.0), (0.24, 0.05, 0.24), m, bevel=0.01)
    mb.box((0, 0.8, -0.24), (0.46, 0.52, 0.07), F.CHAR_FABRIC, bevel=0.03, segments=3, rot_x=8)
    mb.box((0, 0.56, -0.24), (0.06, 0.2, 0.04), m, bevel=0.01)
    for sx in (-1, 1):
        mb.box((sx * 0.27, 0.6, 0.0), (0.04, 0.02, 0.28), m, bevel=0.008)
        mb.box((sx * 0.27, 0.53, -0.05), (0.025, 0.14, 0.03), m, bevel=0.006)
    return finish(mb, "chair_office")


@prop("chair_exec")
def chair_exec():
    mb = MeshBuilder("chair_exec")
    leather = col("3A2620", 55)
    m = col("1D2024", 50, 60)
    _star_base(mb, m, 0.35)
    mb.box((0, 0.48, 0.02), (0.55, 0.11, 0.52), leather, bevel=0.04, segments=3)
    mb.box((0, 0.92, -0.26), (0.52, 0.8, 0.1), leather, bevel=0.05, segments=3, rot_x=8)
    for sx in (-1, 1):
        mb.box((sx * 0.3, 0.66, 0.0), (0.06, 0.05, 0.36), leather, bevel=0.02)
        mb.box((sx * 0.3, 0.58, -0.05), (0.03, 0.14, 0.04), m, bevel=0.008)
    return finish(mb, "chair_exec")


@prop("chair_guest")
def chair_guest():
    mb = MeshBuilder("chair_guest")
    m = col("2B2E33", 50, 60)
    mb.box((0, 0.45, 0.02), (0.5, 0.08, 0.48), F.MUSTARD_FABRIC, bevel=0.03, segments=3)
    mb.box((0, 0.78, -0.22), (0.48, 0.42, 0.06), F.MUSTARD_FABRIC, bevel=0.03, rot_x=10)
    for sx in (-1, 1):
        mb.box((sx * 0.24, 0.21, 0.02), (0.025, 0.42, 0.025), m)
        mb.box((sx * 0.24, 0.02, 0.0), (0.025, 0.025, 0.5), m)
        mb.box((sx * 0.24, 0.4, -0.2), (0.025, 0.8, 0.025), m, rot_x=10)
    return finish(mb, "chair_guest")


@prop("chair_break")
def chair_break():
    mb = MeshBuilder("chair_break")
    m = col("A9AFB6", 60, 90)
    shell = col("2F7F86", 50)
    mb.box((0, 0.45, 0.02), (0.44, 0.03, 0.42), shell, bevel=0.012)
    mb.box((0, 0.72, -0.19), (0.42, 0.34, 0.03), shell, bevel=0.012, rot_x=8)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.cylinder((sx * 0.18, 0.22, sz * 0.17), 0.012, 0.44, m, segments=8)
    return finish(mb, "chair_break")


# =============================================================================================
# main
# =============================================================================================

def build(only=None):
    L.reset_scene()
    L.use_collection(L.collection("Props"))
    objs = []
    for name, fn in PROPS.items():
        if only and name not in only:
            continue
        o = fn()
        objs.append(o)
    return objs


def render_sheet(objs, out_dir):
    """Lay props out on a grid and render a contact sheet."""
    L.setup_render((1600, 1000), 16)
    cols = 10
    spacing = 0.75
    for i, o in enumerate(objs):
        x = (i % cols) * spacing
        z = -(i // cols) * spacing
        o.location = L.u2b((x, 0, z)) + (o.location - L.u2b((0, 0, 0))) * 0
    L.add_sun(3.0, (40, 0, 30))
    L.add_area((cols * spacing / 2, 4, -1.5), 1500, 8)
    cam = L.add_camera("cam_sheet", (cols * spacing / 2 - 0.4, 3.2, 2.8), (cols * spacing / 2 - 0.4, 0.1, -1.7), 30)
    L.render(os.path.join(out_dir, "props_sheet.png"), cam)


if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = set(a[a.index("--only") + 1].split(",")) if "--only" in a else None
    objs = build(only)
    if not only:
        L.export_fbx(os.path.join(L.ROOT, "Assets", "Resources", "Models", "Props.fbx"), objs)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(L.ROOT, "ArtSource", "props.blend"))
    if "--render" in a:
        render_sheet(objs, a[a.index("--render") + 1])
