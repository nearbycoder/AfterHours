"""Furniture builders. Each adds parts to a MeshBuilder in a local frame: pivot at floor centre,
front (the side a person uses) facing Unity +Z. Dimensions in metres."""
import math

from ah_lib import col, glass, glow, shade, tex

OAK = tex("wood-oak", s=45)
WALNUT = tex("wood-walnut", s=50)
STEEL_DARK = col("2E3238", 40, 70)
STEEL = col("B9C0C7", 60, 80)
CHROME = col("D5DAE0", 85, 100)
CHARCOAL = col("33373E", 35)
QUARTZ = tex("stone-quartz", s=55)
SAGE = col("71806E", 30)
LAMINATE_WHITE = col("CBC7BD", 45)
RUBBER = col("1C1E21", 20)
BLACK_PLASTIC = col("1F2226", 45)
GREY_METAL = col("B5BAC0", 45, 40)
LOCKER = col("4F6B7A", 40, 50)
TEAL_FABRIC = tex("fabric-teal", s=10)
CHAR_FABRIC = tex("fabric-charcoal", s=10)
MUSTARD_FABRIC = tex("fabric-mustard", s=10)


def office_desk(mb, w=1.6, d=0.8, h=0.74, top=OAK, frame=STEEL_DARK, drawers=True):
    t = 0.035
    mb.box((0, h - t / 2, 0), (w, t, d), top, bevel=0.006)
    # Sled legs (panel style) and a modesty panel at the back.
    for sx in (-1, 1):
        x = sx * (w / 2 - 0.05)
        mb.box((x, (h - t) / 2, 0), (0.04, h - t, d - 0.08), frame, bevel=0.008)
        mb.box((x, 0.015, 0), (0.07, 0.03, d - 0.02), frame, bevel=0.008)
    mb.box((0, h - t - 0.22, -d / 2 + 0.06), (w - 0.12, 0.34, 0.018), frame, bevel=0.004)
    if drawers:
        # Pedestal under the right side
        px = w / 2 - 0.3
        mb.box((px, 0.31, 0.02), (0.42, 0.58, d - 0.16), GREY_METAL, bevel=0.01)
        for i, yy in enumerate((0.47, 0.3, 0.13)):
            mb.box((px, yy, d / 2 - 0.135), (0.4, 0.15, 0.012), shade_mat(GREY_METAL, 1.05), bevel=0.004)
            mb.box((px, yy + 0.04, d / 2 - 0.125), (0.16, 0.018, 0.02), CHROME, bevel=0.004)


def shade_mat(m, f):
    parts = m.split("_")
    if parts[0] == "col":
        parts[1] = shade(parts[1], f)
    return "_".join(parts)


def executive_desk(mb, w=2.0, d=0.95, h=0.76):
    t = 0.05
    mb.box((0, h - t / 2, 0), (w, t, d), WALNUT, bevel=0.012)
    # Solid pedestals both sides + front modesty panel in walnut.
    for sx in (-1, 1):
        mb.box((sx * (w / 2 - 0.24), (h - t) / 2, 0), (0.46, h - t, d - 0.06), WALNUT, bevel=0.01)
        for yy in (0.5, 0.25):
            mb.box((sx * (w / 2 - 0.24), yy, d / 2 - 0.025), (0.42, 0.2, 0.012), shade_mat(col("5A3B28", 45), 1.1), bevel=0.004)
            mb.box((sx * (w / 2 - 0.24), yy + 0.06, d / 2 - 0.015), (0.18, 0.015, 0.02), col("C9A66B", 70, 90), bevel=0.004)
    mb.box((0, 0.42, -d / 2 + 0.03), (w - 0.95, 0.62, 0.03), WALNUT, bevel=0.006)
    # Leather desk blotter
    mb.box((0.15, h + 0.002, 0.1), (0.8, 0.004, 0.45), col("2A1F1B", 25), bevel=0.002)


def reception_desk(mb, w=3.2, d=0.85, h=1.08, work_h=0.75):
    # Front panel with vertical slats, raised transaction counter, lower work surface behind.
    mb.box((0, h / 2, d / 2 - 0.05), (w, h, 0.1), col("23464C", 25), bevel=0.01)
    n = 22
    for i in range(n):
        x = -w / 2 + 0.07 + i * (w - 0.14) / (n - 1)
        mb.box((x, h / 2 - 0.02, d / 2 + 0.005), (0.05, h - 0.12, 0.03), OAK, bevel=0.004)
    mb.box((0, h + 0.02, d / 2 - 0.12), (w + 0.06, 0.04, 0.38), QUARTZ, bevel=0.008)
    mb.box((0, work_h - 0.02, -0.05), (w - 0.1, 0.035, d - 0.2), OAK, bevel=0.006)
    for sx in (-1, 1):
        mb.box((sx * (w / 2 - 0.05), work_h / 2, -0.05), (0.06, work_h, d - 0.2), col("23464C", 25), bevel=0.006)


def conference_table(mb, w=3.6, d=1.3, h=0.75):
    t = 0.05
    mb.box((0, h - t / 2, 0), (w, t, d), WALNUT, bevel=0.012)
    for sx in (-1, 1):
        x = sx * (w / 2 - 0.5)
        mb.box((x, (h - t) / 2, 0), (0.08, h - t, d * 0.6), CHROME, bevel=0.01)
        mb.box((x, 0.02, 0), (0.14, 0.04, d * 0.75), CHROME, bevel=0.012)
    mb.box((0, h - t - 0.03, 0), (w - 1.1, 0.04, 0.08), CHROME, bevel=0.01)
    # Cable grommet
    mb.cylinder((0, h + 0.001, 0), 0.05, 0.004, BLACK_PLASTIC, segments=20)


def square_table(mb, s=1.1, h=0.74, top=LAMINATE_WHITE):
    mb.box((0, h - 0.02, 0), (s, 0.035, s), top, bevel=0.008)
    mb.cylinder((0, (h - 0.04) / 2, 0), 0.04, h - 0.04, CHROME, segments=16)
    mb.box((0, 0.015, 0), (0.6, 0.03, 0.08), CHROME, bevel=0.008)
    mb.box((0, 0.015, 0), (0.08, 0.03, 0.6), CHROME, bevel=0.008)


def coffee_table(mb, w=1.1, d=0.6, h=0.42):
    mb.box((0, h - 0.025, 0), (w, 0.05, d), OAK, bevel=0.01)
    mb.box((0, 0.12, 0), (w - 0.12, 0.02, d - 0.12), OAK, bevel=0.006)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.box((sx * (w / 2 - 0.05), (h - 0.05) / 2, sz * (d / 2 - 0.05)), (0.035, h - 0.05, 0.035), STEEL_DARK, bevel=0.006)


def sofa(mb, w=2.1, d=0.85, fabric=TEAL_FABRIC):
    seat_h = 0.43
    mb.box((0, 0.12, 0), (w, 0.16, d), CHARCOAL, bevel=0.02)
    for i in range(3):
        x = -w / 2 + 0.1 + (i + 0.5) * (w - 0.2) / 3
        mb.box((x, seat_h - 0.1, 0.06), ((w - 0.2) / 3 - 0.02, 0.17, d - 0.2), fabric, bevel=0.05, segments=3)
        mb.box((x, seat_h + 0.2, -d / 2 + 0.14), ((w - 0.2) / 3 - 0.02, 0.42, 0.2), fabric, bevel=0.06, segments=3, rot_x=-8)
    mb.box((0, seat_h + 0.08, -d / 2 + 0.06), (w - 0.1, 0.6, 0.12), fabric, bevel=0.04)
    for sx in (-1, 1):
        mb.box((sx * (w / 2 - 0.05), 0.42, 0), (0.12, 0.42, d), fabric, bevel=0.05, segments=3)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.cylinder((sx * (w / 2 - 0.1), 0.02, sz * (d / 2 - 0.1)), 0.02, 0.06, CHROME, segments=10)


def bookshelf(mb, w=1.6, d=0.36, h=2.1, mat=WALNUT, shelves=5):
    t = 0.025
    for sx in (-1, 1):
        mb.box((sx * (w / 2 - t / 2), h / 2, 0), (t, h, d), mat, bevel=0.004)
    mb.box((0, h - t / 2, 0), (w, t, d), mat, bevel=0.004)
    mb.box((0, 0.05, 0), (w - 2 * t, 0.1, d), mat, bevel=0.004)
    mb.box((0, h / 2, -d / 2 + 0.006), (w - 2 * t, h - 0.02, 0.012), shade_mat(col("4E3426", 30), 1.0))
    for i in range(1, shelves):
        y = 0.1 + i * (h - 0.12) / shelves
        mb.box((0, y, 0), (w - 2 * t, t, d - 0.02), mat, bevel=0.003)


def filing_cabinet(mb, drawers=4, w=0.47, d=0.62, mat=GREY_METAL):
    h = 0.33 * drawers + 0.06
    mb.box((0, h / 2, 0), (w, h, d), mat, bevel=0.01)
    for i in range(drawers):
        y = 0.04 + 0.33 * i + 0.165
        mb.box((0, y, d / 2 + 0.004), (w - 0.04, 0.31, 0.012), shade_mat(mat, 1.06), bevel=0.004)
        mb.box((0, y + 0.07, d / 2 + 0.02), (0.14, 0.022, 0.025), CHROME, bevel=0.005)
        mb.box((0, y + 0.115, d / 2 + 0.012), (0.07, 0.03, 0.006), col("F1EEE4", 30))


def kitchen_counter(mb, w=4.2, d=0.62, h=0.9, sink_x=None):
    mb.box((0, h / 2 - 0.02, -0.01), (w, h - 0.08, d - 0.04), SAGE, bevel=0.006)
    mb.box((0, 0.05, 0.02), (w - 0.02, 0.1, d - 0.12), RUBBER)
    n = max(1, int(w / 0.6))
    for i in range(n):
        x = -w / 2 + (i + 0.5) * w / n
        mb.box((x, h / 2 + 0.02, d / 2 - 0.025), (w / n - 0.012, h - 0.24, 0.02), shade_mat(SAGE, 1.08), bevel=0.004)
        mb.box((x, h - 0.2, d / 2 - 0.005), (0.14, 0.016, 0.022), CHROME, bevel=0.004)
    top_h = 0.035
    mb.box((0, h + top_h / 2 - 0.02, 0.02), (w + 0.02, top_h, d + 0.04), QUARTZ, bevel=0.006)
    if sink_x is not None:
        mb.box((sink_x, h - 0.01, 0.02), (0.6, 0.04, 0.42), STEEL, bevel=0.01)
        mb.box((sink_x, h - 0.08, 0.02), (0.52, 0.16, 0.36), col("8D949B", 60, 80), bevel=0.01, faces=["-y", "+x", "-x", "+z", "-z"])
        # Faucet
        mb.cylinder((sink_x, h + 0.12, -d / 2 + 0.12), 0.018, 0.24, CHROME, segments=12)
        mb.box((sink_x, h + 0.24, -d / 2 + 0.2), (0.03, 0.03, 0.18), CHROME, bevel=0.012)


def upper_cabinets(mb, w=2.4, d=0.34, h=0.7):
    mb.box((0, h / 2, 0), (w, h, d), LAMINATE_WHITE, bevel=0.006)
    n = max(1, int(w / 0.6))
    for i in range(n):
        x = -w / 2 + (i + 0.5) * w / n
        mb.box((x, h / 2, d / 2 + 0.005), (w / n - 0.012, h - 0.02, 0.016), col("F4F2EC", 50), bevel=0.004)
        mb.box((x + (0.2 if i % 2 else -0.2) * (w / n) / 0.6, 0.08, d / 2 + 0.022), (0.016, 0.12, 0.018), CHROME, bevel=0.004)


def fridge(mb, w=0.75, d=0.72, h=1.85):
    mb.box((0, h / 2, 0), (w, h, d), col("D9DCDF", 55, 25), bevel=0.02)
    mb.box((0, h * 0.68, d / 2 + 0.006), (w - 0.02, h * 0.62, 0.014), col("E2E5E8", 60, 25), bevel=0.006)
    mb.box((0, h * 0.2, d / 2 + 0.006), (w - 0.02, h * 0.36, 0.014), col("E2E5E8", 60, 25), bevel=0.006)
    for y0, y1 in ((h * 0.42, h * 0.95), (h * 0.08, h * 0.34)):
        mb.box((w / 2 - 0.07, (y0 + y1) / 2, d / 2 + 0.035), (0.025, (y1 - y0) * 0.6, 0.03), CHROME, bevel=0.008)


def locker(mb, w=0.45, d=0.5, h=1.85, mat=LOCKER, label=None):
    mb.box((0, h / 2, 0), (w, h, d), mat, bevel=0.008)
    mb.box((0, h / 2, d / 2 + 0.004), (w - 0.03, h - 0.04, 0.01), shade_mat(mat, 1.08), bevel=0.003)
    for i in range(5):
        mb.box((0, h - 0.18 - i * 0.03, d / 2 + 0.01), (w * 0.5, 0.012, 0.004), shade_mat(mat, 0.7))
    mb.box((w / 2 - 0.07, h * 0.52, d / 2 + 0.02), (0.03, 0.12, 0.025), CHROME, bevel=0.006)


def wire_shelf(mb, w=1.2, d=0.45, h=1.8, levels=4):
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.cylinder((sx * (w / 2 - 0.02), h / 2, sz * (d / 2 - 0.02)), 0.012, h, CHROME, segments=10)
    for i in range(levels):
        y = 0.12 + i * (h - 0.2) / (levels - 1)
        mb.box((0, y, 0), (w, 0.02, d), col("C5CACF", 55, 70), bevel=0.004)


def whiteboard(mb, w=2.4, h=1.2):
    mb.box((0, 0, 0.012), (w + 0.04, h + 0.04, 0.024), col("A7ADB4", 70, 80), bevel=0.008)
    mb.box((0, 0, 0.026), (w - 0.02, h - 0.02, 0.006), col("F7F8F9", 88))
    mb.box((0, -h / 2 - 0.035, 0.05), (w * 0.8, 0.02, 0.07), col("A7ADB4", 70, 80), bevel=0.006)


def corkboard(mb, w=1.2, h=0.8):
    mb.box((0, 0, 0.015), (w + 0.05, h + 0.05, 0.03), OAK, bevel=0.006)
    mb.box((0, 0, 0.032), (w - 0.02, h - 0.02, 0.006), tex("cork", s=5))


def tv(mb, w=1.4, h=0.8):
    mb.box((0, 0, 0.025), (w, h, 0.04), BLACK_PLASTIC, bevel=0.008)
    mb.box((0, 0, 0.046), (w - 0.03, h - 0.03, 0.004), "screen_tv")


def credenza(mb, w=1.8, d=0.45, h=0.72):
    mb.box((0, h / 2 + 0.05, 0), (w, h - 0.1, d), WALNUT, bevel=0.008)
    for i in range(3):
        x = -w / 2 + (i + 0.5) * w / 3
        mb.box((x, h / 2 + 0.05, d / 2 + 0.004), (w / 3 - 0.012, h - 0.14, 0.012), shade_mat(col("5A3B28", 45), 1.1), bevel=0.003)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.box((sx * (w / 2 - 0.06), 0.03, sz * (d / 2 - 0.05)), (0.03, 0.06, 0.03), STEEL_DARK)


def coat_rack(mb, h=1.75):
    mb.cylinder((0, h / 2, 0), 0.018, h, STEEL_DARK, segments=12)
    mb.cylinder((0, 0.012, 0), 0.22, 0.024, STEEL_DARK, segments=24)
    for i in range(4):
        a = i * math.pi / 2
        mb.box((math.cos(a) * 0.1, h - 0.1, math.sin(a) * 0.1), (0.2, 0.02, 0.02), STEEL_DARK, rot_y=-math.degrees(a), bevel=0.006)


def water_cooler(mb):
    mb.box((0, 0.5, 0), (0.32, 1.0, 0.34), LAMINATE_WHITE, bevel=0.02)
    mb.box((0, 0.78, 0.172), (0.22, 0.12, 0.01), col("2B2F35", 50))
    mb.box((-0.05, 0.8, 0.19), (0.03, 0.04, 0.04), col("3D7FD8", 50), bevel=0.006)
    mb.box((0.05, 0.8, 0.19), (0.03, 0.04, 0.04), col("D84B3D", 50), bevel=0.006)
    mb.cylinder((0, 1.21, 0), 0.14, 0.42, glass("8FC7E8", 40), segments=24)
    mb.cylinder((0, 1.0, 0), 0.06, 0.04, LAMINATE_WHITE, segments=16)


def printer(mb):
    mb.box((0, 0.45, 0), (0.62, 0.9, 0.58), col("E4E5E3", 40), bevel=0.02)
    mb.box((0, 0.93, -0.03), (0.6, 0.12, 0.5), col("D2D4D2", 45), bevel=0.02)
    mb.box((0.18, 0.92, 0.27), (0.2, 0.06, 0.06), col("2B2F35", 55), bevel=0.01)
    mb.box((0.18, 0.93, 0.3), (0.16, 0.035, 0.006), glow("6FD3FF", 8))
    mb.box((-0.1, 0.72, 0.3), (0.34, 0.03, 0.06), col("C6C8C6", 40), bevel=0.008)
    for i in range(3):
        mb.box((0, 0.15 + i * 0.16, 0.292), (0.56, 0.13, 0.012), col("D8D9D7", 45), bevel=0.006)


def shelf_unit_low(mb, w=1.2, d=0.4, h=0.75):
    mb.box((0, h / 2, 0), (w, h, d), GREY_METAL, bevel=0.008)
    mb.box((0, h / 2, d / 2 + 0.003), (w - 0.03, h - 0.03, 0.008), shade_mat(GREY_METAL, 1.07))


def door_leaf(mb, w=0.9, h=2.1, t=0.045, mat=OAK, glass_panel=False, handle_side=1):
    """Door built with its hinge at the local origin, closing along +X."""
    if glass_panel:
        mb.box((w / 2, h / 2, 0), (w, h, t), col("2E3238", 45, 60), bevel=0.006, faces=None)
        mb.box((w / 2, h / 2, 0), (w - 0.1, h - 0.12, t + 0.004), glass("A7C4D0", 14))
    else:
        mb.box((w / 2, h / 2, 0), (w, h, t), mat, bevel=0.004)
    hx = w - 0.08
    for sz in (-1, 1):
        mb.box((hx, 1.0, sz * (t / 2 + 0.035)), (0.12, 0.02, 0.02), CHROME, bevel=0.006)
        mb.cylinder((hx + 0.05, 1.0, sz * (t / 2 + 0.017)), 0.03, 0.034, CHROME, axis="z", segments=14)
