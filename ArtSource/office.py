"""Builds the whole office (Suite 1408, Meridian Tower) and exports Assets/Resources/Models/Office.fbx.

    blender -b -P ArtSource/office.py -- [--render DIR] [--no-export]

All coordinates are Unity space (x east, y up, z north, metres). Object-name conventions read
by OfficeBuilder.cs:
    FLOOR_<kind>_<room>    walkable floor (kind: carpet, tile, wood, rug)
    WALL_*, ARCH_*         static architecture (mesh colliders)
    GLASS_*                glass panes (Glass layer, box collider)
    DOOR_<id>              hinged door leaf, pivot on the hinge
    PANEL_<room>_<n>       ceiling light panel (emission toggled by the room switch)
    GRIME_<id>             quad marking a cleanable surface (replaced by GrimeSurface)
    ANCHOR_<id>            spawn point / reference frame for props
    LIGHT_<kind>_<room>_<n> light position
    SWITCH_<room>          light switch position (faces out of the wall)
    TRAY_<person>          inbox tray position
    BIN_<type>_<room>_<n>  bin position (type: trash, recycle)
    ROOM_<name>            room volume (box, not rendered)
    NC_*                   decorative, no collider
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ah_lib as L  # noqa: E402
import furniture as F  # noqa: E402
from ah_lib import MeshBuilder, col, glass, glow, tex  # noqa: E402

H = 2.9            # ceiling height
T_IN = 0.15        # interior wall thickness
T_EX = 0.3         # exterior wall thickness
DOOR_W, DOOR_H = 0.92, 2.12

WALL = tex("plaster-warm", s=15)
WALL_TEAL = tex("plaster-teal", s=20)
TRIM = col("2B3036", 40, 30)
BASE = col("3A3F47", 35)
FRAME = col("2A2E33", 45, 60)
SILL = col("D9D4C8", 45)
CEIL = tex("ceiling-tile_t83", s=10)
GLASS_EXT = glass("8FB3C4", 10)
GLASS_INT = glass("A9C8D4", 12)

ROOMS = {
    # name: (x0, z0, x1, z1)
    "reception": (7, 0, 15, 5),
    "closet": (15, 0, 18, 5),
    "bullpen": (7, 5, 18, 16),
    "conference": (0, 9, 7, 16),
    "breakroom": (0, 3, 7, 9),
    "office": (18, 9, 25, 16),
}


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = {"render": None, "export": True}
    if "--render" in a:
        out["render"] = a[a.index("--render") + 1]
    if "--no-export" in a:
        out["export"] = False
    return out


# ---------------------------------------------------------------------------------------------
# walls
# ---------------------------------------------------------------------------------------------

def wall(name, a, b, t, openings=(), mat_a=WALL, mat_b=None, base_a=True, base_b=True, h=H):
    """Straight wall from a=(x,z) to b=(x,z) (axis aligned). openings: (s0, s1, y0, y1) along the
    wall from a. Side 'a' is +normal (left of a->b when looking down... we just do both)."""
    mat_b = mat_b or mat_a
    ax, az = a
    bx, bz = b
    along_x = abs(bz - az) < 1e-6
    length = abs(bx - ax) if along_x else abs(bz - az)
    sgn = 1 if (bx - ax if along_x else bz - az) > 0 else -1
    mb = MeshBuilder("WALL_" + name, (0, 0, 0))

    def seg(s0, s1, y0, y1):
        if s1 - s0 < 1e-4 or y1 - y0 < 1e-4:
            return
        mid = (s0 + s1) / 2 * sgn
        if along_x:
            c = (ax + mid, (y0 + y1) / 2, az)
            size = (s1 - s0, y1 - y0, t)
        else:
            c = (ax, (y0 + y1) / 2, az + mid)
            size = (t, y1 - y0, s1 - s0)
        mb.box(c, size, mat_a)

    cuts = sorted(openings)
    pos = 0.0
    solid = []
    for s0, s1, y0, y1 in cuts:
        seg(pos, s0, 0, h)
        solid.append((pos, s0))
        seg(s0, s1, 0, y0)
        seg(s0, s1, y1, h)
        if y0 > 0.01:
            solid.append((s0, s1))
        pos = s1
    seg(pos, length, 0, h)
    solid.append((pos, length))
    obj = mb.build()

    # Baseboards on both faces along solid runs (skipped across door openings).
    bb = MeshBuilder("NC_BASE_" + name, (0, 0, 0))
    for s0, s1 in solid:
        if s1 - s0 < 0.02:
            continue
        mid = (s0 + s1) / 2 * sgn
        for side, enabled in ((1, base_a), (-1, base_b)):
            if not enabled:
                continue
            if along_x:
                bb.box((ax + mid, 0.05, az + side * (t / 2 + 0.008)), (s1 - s0, 0.1, 0.016), BASE, bevel=0.003)
            else:
                bb.box((ax + side * (t / 2 + 0.008), 0.05, az + mid), (0.016, 0.1, s1 - s0), BASE, bevel=0.003)
    bb.build()
    return obj


def window_run(name, a, b, wall_line_offset, y0, y1, panes, inner_normal, grime_ids=None):
    """Frames + glass for a window opening from a to b (along x or z), placed in the wall plane."""
    ax, az = a
    bx, bz = b
    along_x = abs(bz - az) < 1e-6
    length = abs(bx - ax) if along_x else abs(bz - az)
    fr = MeshBuilder("NC_FRAME_" + name, (0, 0, 0))

    def at(s, y, off=0.0):
        if along_x:
            return (min(ax, bx) + s, y, az + off)
        return (ax + off, y, min(az, bz) + s)

    def bar(s, y, ls, ly, depth=0.08):
        size = (ls, ly, depth) if along_x else (depth, ly, ls)
        fr.box(at(s, y), size, FRAME, bevel=0.004)

    # Outer frame
    bar(length / 2, y0 + 0.03, length, 0.06)
    bar(length / 2, y1 - 0.03, length, 0.06)
    bar(0.03, (y0 + y1) / 2, 0.06, y1 - y0)
    bar(length - 0.03, (y0 + y1) / 2, 0.06, y1 - y0)
    pw = length / panes
    for i in range(1, panes):
        bar(i * pw, (y0 + y1) / 2, 0.07, y1 - y0, 0.1)
    # Interior sill for windows that start above the floor.
    n = Vector(inner_normal)
    if y0 > 0.3:
        c = at(length / 2, y0 - 0.02, 0)
        c = (c[0] + n.x * 0.12, c[1], c[2] + n.z * 0.12)
        size = (length + 0.1, 0.04, 0.26) if along_x else (0.26, 0.04, length + 0.1)
        fr.box(c, size, SILL, bevel=0.006)
    fr.build()
    # Glass panes (one object each, so Unity can collide and clean them separately).
    for i in range(panes):
        s0, s1 = i * pw + 0.04, (i + 1) * pw - 0.04
        c = at((s0 + s1) / 2, (y0 + y1) / 2)
        size = (s1 - s0, y1 - y0 - 0.08, 0.012) if along_x else (0.012, y1 - y0 - 0.08, s1 - s0)
        L.box(f"GLASS_{name}_{i + 1}", c, size, GLASS_EXT)
        if grime_ids:
            gid = grime_ids[i] if i < len(grime_ids) else None
            if gid:
                gc = Vector(c) + n * 0.012
                L.grime_plane("GRIME_" + gid, tuple(gc), tuple(n), (s1 - s0 - 0.02, y1 - y0 - 0.1))


def door_frame(name, center, along_x, width=DOOR_W, height=DOOR_H, depth=0.17):
    fr = MeshBuilder("NC_DOORFRAME_" + name, (0, 0, 0))
    cx, cz = center
    for sgn in (-1, 1):
        if along_x:
            fr.box((cx + sgn * (width / 2 + 0.03), height / 2, cz), (0.06, height, depth), TRIM, bevel=0.006)
        else:
            fr.box((cx, height / 2, cz + sgn * (width / 2 + 0.03)), (depth, height, 0.06), TRIM, bevel=0.006)
    if along_x:
        fr.box((cx, height + 0.03, cz), (width + 0.12, 0.06, depth), TRIM, bevel=0.006)
    else:
        fr.box((cx, height + 0.03, cz), (depth, 0.06, width + 0.12), TRIM, bevel=0.006)
    fr.build()


def door(id_, hinge, yaw, mat=F.OAK, glass_panel=False, width=DOOR_W):
    """Door leaf with pivot on the hinge; closed when yaw places it in its frame."""
    mb = MeshBuilder("DOOR_" + id_, hinge)
    mb.push(hinge, yaw)
    if glass_panel:
        w, h, t = width - 0.02, DOOR_H - 0.02, 0.045
        for (cx, cy, sx, sy) in ((w / 2, 0.06, w, 0.12), (w / 2, h - 0.05, w, 0.1), (0.04, h / 2, 0.08, h), (w - 0.04, h / 2, 0.08, h)):
            mb.box((cx, cy, 0), (sx, sy, t), F.STEEL_DARK, bevel=0.005)
        mb.box((w / 2, h / 2, 0), (w - 0.1, h - 0.18, 0.012), GLASS_INT)
        mb.box((w - 0.1, 1.0, 0.06), (0.03, 0.5, 0.03), F.CHROME, bevel=0.01)
        mb.box((w - 0.1, 1.0, -0.06), (0.03, 0.5, 0.03), F.CHROME, bevel=0.01)
    else:
        F.door_leaf(mb, w=width - 0.02, h=DOOR_H - 0.02, mat=mat)
    mb.pop()
    return mb.build()


# ---------------------------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------------------------

def place(name, builder, pos, yaw=0.0, **kw):
    mb = MeshBuilder(name, pos)
    mb.push(pos, yaw)
    builder(mb, **kw)
    mb.pop()
    return mb.build()


def anchor(id_, pos, yaw=0.0):
    return L.empty(id_, pos, yaw=yaw)


def text_mesh(name, text, font_file, size, extrude, center, right, up, mat, align="CENTER"):
    curve = bpy.data.curves.new(name + "_curve", "FONT")
    curve.body = text
    curve.font = bpy.data.fonts.load(os.path.join(L.ROOT, "ArtSource", "fonts", font_file))
    curve.size = size
    curve.extrude = extrude
    curve.align_x = align
    curve.align_y = "CENTER"
    tmp = bpy.data.objects.new(name + "_tmp", curve)
    bpy.context.scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    r, u = Vector(right).normalized(), Vector(up).normalized()
    n = r.cross(u)  # Unity left-handed: right x up = -forward... we only need a consistent extrude dir
    c = Vector(center)
    for v in mesh.vertices:
        p = c + r * v.co.x + u * v.co.y - n * (v.co.z + extrude)
        v.co = L.u2b(p)
    mesh.materials.append(L.material(mat))
    obj = bpy.data.objects.new(name, mesh)
    L._link(obj)
    return obj


def room_volume(name, rect, y0=0.0, y1=H):
    x0, z0, x1, z1 = rect
    o = L.box("ROOM_" + name, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), "col_FF00FF")
    o.display_type = "WIRE"
    o.hide_render = True
    return o


# ---------------------------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------------------------

def build():
    L.reset_scene()
    root = L.collection("Office")
    L.use_collection(root)

    # ---- floors ----
    def floor(kind, room, rect, mat, y=0.0, t=0.05):
        x0, z0, x1, z1 = rect
        L.box(f"FLOOR_{kind}_{room}", ((x0 + x1) / 2, y - t / 2, (z0 + z1) / 2), (x1 - x0, t, z1 - z0), mat)

    floor("tile", "reception", (7, 0, 15, 5), tex("tile-lobby_t83", s=55))
    floor("tile", "closet", (15, 0, 18, 5), tex("tile-closet", s=40))
    floor("carpet", "bullpen", (7, 5, 18, 16), tex("carpet-slate", s=5))
    floor("carpet", "conference", (0, 9, 7, 16), tex("carpet-teal", s=5))
    floor("tile", "breakroom", (0, 3, 7, 9), tex("tile-break", s=50))
    floor("wood", "office", (18, 9, 25, 16), tex("wood-floor", s=45))
    floor("tile", "storage", (0, 0, 7, 3), tex("tile-closet", s=40))
    floor("tile", "lobby", (7.5, -3.6, 14.5, 0), tex("tile-lobby_t83", s=55))
    # Rugs (vacuumable)
    L.box("FLOOR_rug_reception", (8.55, 0.006, 2.45), (2.0, 0.012, 2.9), tex("rug-lobby", s=5), bevel=0.004)
    L.box("FLOOR_rug_office", (21.9, 0.006, 12.6), (3.4, 0.012, 2.6), tex("carpet-rust", s=5), bevel=0.004)

    # ---- ceiling ----
    L.box("WALL_ceiling", (12.5, H + 0.05, 7.0), (25.4, 0.1, 19.4), CEIL, faces=["-y"])
    L.box("NC_ceiling_back", (12.5, H + 0.12, 7.0), (25.4, 0.04, 19.4), col("1A1C20", 10))

    # ---- exterior walls ----
    wall("south_a", (0, 0), (7, 0), T_EX)
    wall("south_rec", (7, 0), (15, 0), T_EX, openings=[(2.4, 3.2, 0.0, 2.75), (3.2, 5.2, 0.0, DOOR_H + 0.15), (5.2, 6.0, 0.0, 2.75)])
    wall("south_closet", (15, 0), (18, 0), T_EX)
    wall("north", (0, 16), (25, 16), T_EX, openings=[(0.6, 6.4, 0.8, 2.6), (7.6, 17.4, 0.8, 2.6), (18.6, 24.4, 0.06, 2.78)])
    wall("west", (0, 0), (0, 16), T_EX, openings=[(3.6, 8.4, 0.8, 2.6), (9.6, 15.4, 0.8, 2.6)])
    wall("east_low", (18, 0), (18, 9), T_EX)
    wall("east_office", (25, 9), (25, 16), T_EX, openings=[(0.6, 6.4, 0.06, 2.78)])
    wall("south_office", (18, 9), (25, 9), T_IN + 0.1)

    window_run("bullpen", (7.6, 16), (17.4, 16), 0, 0.8, 2.6, 3, (0, 0, -1), ["win_bullpen_1", "win_bullpen_2", "win_bullpen_3"])
    window_run("conference_n", (0.6, 16), (6.4, 16), 0, 0.8, 2.6, 2, (0, 0, -1), ["win_conf_1", None])
    window_run("conference_w", (0, 9.6), (0, 15.4), 0, 0.8, 2.6, 2, (1, 0, 0), [None, None])
    window_run("breakroom", (0, 3.6), (0, 8.4), 0, 0.8, 2.6, 2, (1, 0, 0), ["win_break_1", "win_break_2"])
    window_run("office_n", (18.6, 16), (24.4, 16), 0, 0.06, 2.78, 3, (0, 0, -1), ["win_office_n1", "win_office_n2", "win_office_n3"])
    window_run("office_e", (25, 9.6), (25, 15.4), 0, 0.06, 2.78, 3, (-1, 0, 0), ["win_office_e1", "win_office_e2", None])

    # Reception entrance: glass sidelights + double glass doors.
    window_run("entry_l", (9.4, 0), (10.2, 0), 0, 0.0, 2.75, 1, (0, 0, 1), ["glass_entry_l"])
    window_run("entry_r", (12.2, 0), (13.0, 0), 0, 0.0, 2.75, 1, (0, 0, 1), ["glass_entry_r"])
    door("entry_l", (10.22, 0, 0.0), 0, glass_panel=True, width=1.0)
    door("entry_r", (12.18, 0, 0.0), 180, glass_panel=True, width=1.0)
    L.grime_plane("GRIME_glass_doors", (11.2, 1.1, 0.04), (0, 0, 1), (1.85, 1.9))

    # ---- interior walls ----
    wall("rec_closet", (15, 0), (15, 5), T_IN, openings=[(1.0, 1.0 + DOOR_W, 0.0, DOOR_H)])
    wall("rec_bullpen", (7, 5), (15, 5), T_IN, openings=[(2.8, 6.2, 0.0, 2.45)])
    wall("closet_bullpen", (15, 5), (18, 5), T_IN)
    wall("west_inner", (7, 0), (7, 5), T_IN, mat_a=WALL_TEAL, mat_b=WALL)
    wall("break_bullpen", (7, 5), (7, 9), T_IN, openings=[(1.4, 1.4 + DOOR_W, 0.0, DOOR_H)])
    wall("storage_break", (0, 3), (7, 3), T_IN)
    wall("conf_break", (0, 9), (7, 9), T_IN)
    wall("office_bullpen", (18, 9), (18, 16), T_IN, openings=[(1.0, 1.0 + DOOR_W, 0.0, DOOR_H)])
    # Conference glass wall (x = 7, z 9..16) with a glass door.
    wall("conf_glass_lo", (7, 9), (7, 10.0), T_IN)
    gw = MeshBuilder("NC_FRAME_confglass", (0, 0, 0))
    for z in (10.0, 11.05, 13.5, 16.0):
        gw.box((7, H / 2, z), (0.1, H, 0.08), F.STEEL_DARK, bevel=0.004)
    gw.box((7, H - 0.05, 13.0), (0.1, 0.1, 6.0), F.STEEL_DARK)
    gw.box((7, DOOR_H + 0.04, 10.5), (0.1, 0.08, 1.1), F.STEEL_DARK)
    gw.box((7, 0.04, 13.5), (0.1, 0.08, 5.0), F.STEEL_DARK)
    gw.build()
    L.box("GLASS_conf_1", (7, H / 2, 12.27), (0.012, H - 0.1, 2.36), GLASS_INT)
    L.box("GLASS_conf_2", (7, H / 2, 14.75), (0.012, H - 0.1, 2.4), GLASS_INT)
    L.box("GLASS_conf_top", (7, (DOOR_H + H) / 2 + 0.04, 10.52), (0.012, H - DOOR_H - 0.1, 1.0), GLASS_INT)
    L.grime_plane("GRIME_glasswall_conf", (6.98, 1.4, 13.5), (-1, 0, 0), (4.8, 2.5))

    # Doors (closed position): hinge then leaf extends along +X local.
    door_frame("closet", (15, 1.0 + DOOR_W / 2), False)
    door("closet", (15, 0, 1.0), -90)
    door_frame("breakroom", (7, 5 + 1.4 + DOOR_W / 2), False)
    door("breakroom", (7, 0, 6.4), -90)
    door_frame("office", (18, 10.0 + DOOR_W / 2), False)
    door("office", (18, 0, 10.0), -90, mat=F.WALNUT)
    door("conference", (7, 0, 10.0), -90, glass_panel=True, width=1.04)
    # Archway trim reception -> bullpen
    arch = MeshBuilder("NC_ARCH_rec", (0, 0, 0))
    arch.box((9.8, 1.22, 5), (0.06, 2.45, 0.19), TRIM, bevel=0.006)
    arch.box((13.2, 1.22, 5), (0.06, 2.45, 0.19), TRIM, bevel=0.006)
    arch.box((11.5, 2.48, 5), (3.46, 0.06, 0.19), TRIM, bevel=0.006)
    arch.build()

    # ---- lobby beyond the glass doors ----
    wall("lobby_s", (7.5, -3.6), (14.5, -3.6), 0.2, openings=[(2.8, 4.2, 0.0, 2.3)])
    wall("lobby_w", (7.5, -3.6), (7.5, 0), 0.2)
    wall("lobby_e", (14.5, -3.6), (14.5, 0), 0.2)
    elev = MeshBuilder("NC_elevator", (0, 0, 0))
    elev.box((10.65, 1.15, -3.5), (0.69, 2.3, 0.04), col("A9B0B8", 75, 90), bevel=0.004)
    elev.box((11.35, 1.15, -3.5), (0.69, 2.3, 0.04), col("A9B0B8", 75, 90), bevel=0.004)
    elev.box((11.0, 2.38, -3.47), (1.5, 0.12, 0.06), col("2A2E33", 50, 60), bevel=0.006)
    elev.box((11.0, 2.38, -3.44), (0.3, 0.06, 0.01), glow("FFB45E", 12))
    elev.box((11.95, 1.2, -3.47), (0.1, 0.18, 0.03), col("2A2E33", 50, 60), bevel=0.006)
    elev.cylinder((11.95, 1.24, -3.45), 0.02, 0.01, glow("FFFFFF", 6), axis="z", segments=12)
    elev.build()
    text_mesh("NC_lobby_sign", "MERIDIAN TOWER  ·  14", "FiraSans-Medium.ttf", 0.16, 0.01, (11.0, 2.6, -3.48), (-1, 0, 0), (0, 1, 0), col("C9A66B", 70, 90))

    # ---- reception ----
    place("FURN_reception_desk", F.reception_desk, (11.5, 0, 3.3), 180)
    place("FURN_sofa_reception", F.sofa, (7.55, 0, 2.45), 90)
    place("FURN_coffeetable_reception", F.coffee_table, (8.75, 0, 2.45), 90)
    place("FURN_coatrack", F.coat_rack, (14.45, 0, 0.6))
    place("FURN_watercooler", F.water_cooler, (14.65, 0, 4.4), -90)
    text_mesh("NC_logo", "HALVORSEN FREIGHT", "FiraSans-Bold.ttf", 0.3, 0.025, (7.09, 1.95, 2.45), (0, 0, 1), (0, 1, 0), col("D9C08A", 75, 90))
    text_mesh("NC_logo_sub", "& FORWARDING · EST. 1987", "FiraSans-Medium.ttf", 0.11, 0.01, (7.09, 1.68, 2.45), (0, 0, 1), (0, 1, 0), col("BFC6CC", 55, 60))
    exitb = MeshBuilder("NC_exit_sign", (0, 0, 0))
    exitb.box((11.2, 2.62, 0.22), (0.4, 0.16, 0.06), col("F2F2F2", 40), bevel=0.01)
    exitb.build()
    text_mesh("NC_exit_text", "EXIT", "FiraSans-Bold.ttf", 0.1, 0.004, (11.2, 2.62, 0.253), (-1, 0, 0), (0, 1, 0), glow("FF3B30", 30))
    L.grime_plane("GRIME_desk_reception", (11.5, 1.123, 3.42), (0, 1, 0), (3.2, 0.36))
    L.grime_plane("GRIME_coffeetable_reception", (8.75, 0.422, 2.45), (0, 1, 0), (1.08, 0.58), v_axis=(1, 0, 0))
    L.grime_plane("GRIME_rug_reception", (8.55, 0.0125, 2.45), (0, 1, 0), (2.0, 2.9))
    L.grime_plane("GRIME_floor_reception", (11.0, 0.002, 2.5), (0, 1, 0), (7.8, 4.8))
    anchor("ANCHOR_desk_dana", (11.5, 0.75, 3.75), 0)
    anchor("TRAY_dana", (12.6, 0.75, 3.7), 0)
    anchor("ANCHOR_chair_dana", (11.5, 0, 4.35), 180)
    anchor("BIN_trash_reception_1", (13.0, 0, 4.4), 0)
    anchor("ANCHOR_plant_reception", (7.45, 0, 0.55), 0)
    anchor("ANCHOR_phone_reception", (10.6, 0.75, 3.75), 0)
    anchor("ANCHOR_coffeetable", (8.75, 0.42, 2.45), 90)

    # ---- closet ----
    place("FURN_locker_you", F.locker, (17.68, 0, 3.15), -90)
    place("FURN_locker_walt", F.locker, (17.68, 0, 3.62), -90, mat=col("6E5A4A", 40, 50))
    place("FURN_wireshelf_closet", F.wire_shelf, (17.7, 0, 1.15), -90)
    place("FURN_corkboard_closet", F.corkboard, (16.5, 1.5, 4.925), 180)
    place("FURN_closet_table", F.square_table, (15.6, 0, 4.4), 0, s=0.7)
    text_mesh("NC_walt_label", "W. BREMNER", "FiraSans-Bold.ttf", 0.035, 0.002, (17.425, 1.62, 3.62), (0, 0, -1), (0, 1, 0), col("EDE6D6", 30))
    anchor("ANCHOR_spawn", (16.4, 0, 1.45), -90)
    anchor("ANCHOR_punchclock", (15.08, 1.35, 2.5), 90)
    anchor("ANCHOR_cart", (16.5, 0, 3.4), 0)
    anchor("ANCHOR_corkboard_closet", (16.5, 1.5, 4.89), 180)
    anchor("ANCHOR_locker_walt", (17.4, 0, 3.62), -90)
    anchor("ANCHOR_closet_table", (15.6, 0.74, 4.4), 0)
    anchor("ANCHOR_ceiling_tile_closet", (16.2, H - 0.02, 3.0), 0)

    # ---- bullpen ----
    desks = {"theo": (10.2, 11.6, 180), "priya": (10.2, 12.4, 0), "walt": (14.4, 11.6, 180), "russ": (14.4, 12.4, 0)}
    for who, (x, z, yaw) in desks.items():
        place(f"FURN_desk_{who}", F.office_desk, (x, 0, z), yaw)
        L.grime_plane(f"GRIME_desk_{who}", (x, 0.742, z), (0, 1, 0), (1.58, 0.78), v_axis=(0, 0, 1))
        fwd = 1 if yaw == 0 else -1
        anchor(f"ANCHOR_desk_{who}", (x, 0.742, z), yaw)
        anchor(f"TRAY_{who}", (x - 0.6, 0.742, z - fwd * 0.18), yaw)
        anchor(f"ANCHOR_chair_{who}", (x, 0, z + fwd * 0.75), yaw + 180)
        anchor(f"BIN_trash_bullpen_{who}", (x + 0.95, 0, z + fwd * 0.25), 0)
    # Low fabric dividers between facing desks.
    for x in (10.2, 14.4):
        dv = MeshBuilder(f"FURN_divider_{x}", (x, 0, 12.0))
        dv.box((x, 0.95, 12.0), (1.6, 0.4, 0.04), F.MUSTARD_FABRIC, bevel=0.015)
        dv.box((x, 1.155, 12.0), (1.62, 0.02, 0.05), F.STEEL_DARK, bevel=0.005)
        dv.build()
    place("FURN_printer", F.printer, (17.45, 0, 6.3), -90)
    anchor("ANCHOR_printer_tray", (17.15, 0.73, 6.3), -90)
    anchor("ANCHOR_shredder_bullpen", (17.55, 0, 7.4), -90)
    anchor("BIN_recycle_bullpen_1", (17.55, 0, 8.2), -90)
    for i, z in enumerate((13.3, 13.8, 14.3)):
        place(f"FURN_filing_bullpen_{i}", F.filing_cabinet, (17.58, 0, z), -90)
    place("FURN_shelf_bullpen", F.shelf_unit_low, (16.2, 0, 5.3), 0)
    anchor("ANCHOR_shelf_bullpen", (16.2, 0.75, 5.3), 0)
    anchor("ANCHOR_plant_bullpen_1", (7.5, 0, 15.5), 0)
    anchor("ANCHOR_plant_bullpen_2", (17.5, 0, 15.5), 0)
    anchor("ANCHOR_plant_bullpen_3", (8.0, 0, 5.5), 0)
    L.grime_plane("GRIME_floor_bullpen", (12.5, 0.002, 10.5), (0, 1, 0), (10.9, 10.9))
    L.grime_plane("GRIME_printer_top", (17.48, 0.992, 6.27), (0, 1, 0), (0.56, 0.46), v_axis=(1, 0, 0))

    # ---- conference ----
    place("FURN_table_conf", F.conference_table, (3.5, 0, 12.6), 0)
    place("FURN_whiteboard_conf", F.whiteboard, (2.6, 1.5, 9.075), 0)
    place("FURN_tv_conf", F.tv, (5.6, 1.55, 9.075), 0)
    place("FURN_credenza_conf", F.credenza, (0.4, 0, 12.6), 90)
    L.grime_plane("GRIME_whiteboard_conf", (2.6, 1.5, 9.105), (0, 0, 1), (2.36, 1.16))
    L.grime_plane("GRIME_table_conf", (3.5, 0.752, 12.6), (0, 1, 0), (3.55, 1.25), v_axis=(0, 0, 1))
    L.grime_plane("GRIME_floor_conf", (3.5, 0.002, 12.5), (0, 1, 0), (6.9, 6.9))
    anchor("ANCHOR_table_conf", (3.5, 0.752, 12.6), 0)
    for i, (x, z, yaw) in enumerate([(2.3, 11.65, 0), (3.5, 11.65, 0), (4.7, 11.65, 0), (2.3, 13.55, 180), (3.5, 13.55, 180), (4.7, 13.55, 180)]):
        anchor(f"ANCHOR_chair_conf_{i + 1}", (x, 0, z), yaw)
    anchor("TRAY_auditor", (4.9, 0.752, 12.45), 0)
    anchor("BIN_trash_conf_1", (6.5, 0, 9.5), 0)
    anchor("ANCHOR_plant_conf", (6.4, 0, 15.5), 0)
    anchor("ANCHOR_credenza_conf", (0.4, 0.72, 12.6), 90)

    # ---- corner office ----
    place("FURN_desk_marian", F.executive_desk, (22.1, 0, 12.6), 90)
    place("FURN_bookshelf_office_1", F.bookshelf, (20.6, 0, 9.33), 0)
    place("FURN_bookshelf_office_2", F.bookshelf, (22.3, 0, 9.33), 0)
    place("FURN_filing_fc2", F.filing_cabinet, (24.62, 0, 10.0), -90, drawers=2)
    place("FURN_credenza_office", F.credenza, (24.62, 0, 13.6), -90, w=1.6)
    L.grime_plane("GRIME_desk_marian", (22.1, 0.762, 12.6), (0, 1, 0), (1.95, 0.9), v_axis=(1, 0, 0))
    L.grime_plane("GRIME_floor_office", (21.5, 0.002, 12.5), (0, 1, 0), (6.9, 6.9))
    for i in range(4):
        y = 0.1 + (i + 1) * (2.1 - 0.12) / 5 + 0.0135
        L.grime_plane(f"GRIME_shelf_office_{i + 1}", (21.45, y, 9.35), (0, 1, 0), (3.3, 0.3), v_axis=(0, 0, 1))
    anchor("ANCHOR_desk_marian", (22.1, 0.762, 12.6), 90)
    anchor("TRAY_marian", (21.85, 0.762, 13.3), 90)
    anchor("ANCHOR_chair_marian", (22.9, 0, 12.6), -90)
    anchor("ANCHOR_guest_1", (20.85, 0, 12.15), 90)
    anchor("ANCHOR_guest_2", (20.85, 0, 13.05), 90)
    anchor("ANCHOR_fc2", (24.3, 0, 10.0), -90)
    anchor("ANCHOR_credenza_office", (24.62, 0.72, 13.6), -90)
    anchor("ANCHOR_shredder_office", (23.5, 0, 9.55), 0)
    anchor("BIN_trash_office_1", (23.2, 0, 11.6), 0)
    anchor("ANCHOR_plant_office", (24.5, 0, 15.5), 0)
    anchor("ANCHOR_shelf_office", (21.45, 0.5, 9.4), 0)

    # ---- break room ----
    place("FURN_counter_break", F.kitchen_counter, (2.35, 0, 3.4), 0, w=4.4, sink_x=-0.6)
    place("FURN_uppers_break", F.upper_cabinets, (2.0, 1.5, 3.25), 0, w=3.0)
    place("FURN_fridge_break", F.fridge, (5.25, 0, 3.47), 0)
    place("FURN_table_break", F.square_table, (3.3, 0, 6.6), 0, s=1.2)
    place("FURN_corkboard_break", F.corkboard, (3.5, 1.55, 8.925), 180, w=1.4, h=0.9)
    L.grime_plane("GRIME_counter_break", (2.6, 0.917, 3.43), (0, 1, 0), (3.4, 0.6), v_axis=(0, 0, 1))
    L.grime_plane("GRIME_table_break", (3.3, 0.74, 6.6), (0, 1, 0), (1.18, 1.18))
    L.grime_plane("GRIME_fridge_break", (5.25, 1.25, 3.85), (0, 0, 1), (0.7, 1.1))
    L.grime_plane("GRIME_floor_break", (3.5, 0.002, 6.0), (0, 1, 0), (6.9, 5.9))
    anchor("ANCHOR_counter_break", (2.35, 0.917, 3.45), 0)
    anchor("ANCHOR_microwave", (0.85, 0.917, 3.4), 0)
    anchor("ANCHOR_coffee_machine", (3.9, 0.917, 3.35), 0)
    anchor("ANCHOR_dishrack", (1.75, 0.917, 3.5), 0)
    anchor("ANCHOR_table_break", (3.3, 0.74, 6.6), 0)
    for i, (x, z, yaw) in enumerate([(3.3, 5.85, 0), (3.3, 7.35, 180), (2.55, 6.6, 90), (4.05, 6.6, -90)]):
        anchor(f"ANCHOR_chair_break_{i + 1}", (x, 0, z), yaw)
    anchor("BIN_trash_break_1", (6.5, 0, 4.0), 0)
    anchor("BIN_recycle_break_1", (6.5, 0, 4.6), 0)
    anchor("ANCHOR_corkboard_break", (3.5, 1.55, 8.89), 180)
    anchor("ANCHOR_plant_break", (0.5, 0, 8.5), 0)

    # ---- lights, switches, rooms ----
    panels = {
        "reception": [(9.6, 2.5), (13.2, 2.5)],
        "closet": [(16.5, 2.5)],
        "bullpen": [(9.6, 8.5), (12.5, 8.5), (15.4, 8.5), (9.6, 13.5), (12.5, 13.5), (15.4, 13.5)],
        "conference": [(2.3, 12.6), (4.7, 12.6)],
        "office": [(20.4, 12.6), (23.2, 12.6)],
        "breakroom": [(2.2, 6.0), (4.8, 6.0)],
        "lobby": [(11.0, -1.8)],
    }
    for room, pts in panels.items():
        for i, (x, z) in enumerate(pts):
            n = f"{room}_{i + 1}"
            if room == "closet":
                b = MeshBuilder("PANEL_" + n, (x, H - 0.1, z))
                b.cylinder((x, H - 0.06, z), 0.06, 0.12, col("2B2F35", 40), segments=16)
                b.sphere((x, H - 0.17, z), 0.07, glow("FFD9A0", 30), segments=16, rings=10)
                b.build()
            else:
                pm = MeshBuilder("PANEL_" + n, (x, H - 0.02, z))
                pm.box((x, H - 0.012, z), (1.24, 0.024, 0.64), col("D8DADC", 50, 40), bevel=0.004)
                pm.box((x, H - 0.026, z), (1.16, 0.006, 0.56), glow("F2F7FF", 25))
                pm.build()
            anchor("LIGHT_panel_" + n, (x, H - 0.05, z), 0)
    anchor("LIGHT_lamp_reception_1", (12.6, 1.15, 3.85), 0)
    anchor("LIGHT_lamp_office_1", (22.35, 1.2, 13.25), 0)

    switches = {
        "reception": ((14.925, 1.25, 2.15), -90),
        "closet": ((15.075, 1.25, 0.75), 90),
        "bullpen": ((13.6, 1.25, 5.075), 0),
        "conference": ((6.6, 1.25, 9.075), 0),
        "office": ((18.075, 1.25, 11.2), 90),
        "breakroom": ((6.925, 1.25, 7.6), -90),
    }
    for room, (p, yaw) in switches.items():
        anchor("SWITCH_" + room, p, yaw)

    for name, rect in ROOMS.items():
        room_volume(name, rect)
    room_volume("lobby", (7.5, -3.6, 14.5, 0))
    return root


def render_previews(out_dir):
    L.setup_render((1280, 720), 16)
    sc = bpy.context.scene
    # Hide markers
    for o in bpy.context.scene.objects:
        if o.name.startswith(("ROOM_", "GRIME_")):
            o.hide_render = True
    # Lights: a soft sky + a few area lights so the geometry reads.
    L.add_sun(1.5, (55, 0, 35))
    for room, (x0, z0, x1, z1) in ROOMS.items():
        L.add_area(((x0 + x1) / 2, 2.6, (z0 + z1) / 2), energy=400, size=3.0)
    # The ceiling hides interiors from above: render top-down without it.
    ceil = [o for o in sc.objects if o.name.startswith(("WALL_ceiling", "NC_ceiling", "PANEL_"))]
    for o in ceil:
        o.hide_render = True
    cams = [
        ("top", (12.5, 30, 7.9), (12.5, 0, 8.0), 18),
        ("iso", (-4, 14, -6), (11, 0, 8), 24),
    ]
    for name, pos, tgt, lens in cams:
        cam = L.add_camera("cam_" + name, pos, tgt, lens)
        L.render(os.path.join(out_dir, f"office_{name}.png"), cam)
    for o in ceil:
        o.hide_render = False
    interiors = [
        ("reception", (14.2, 1.6, 0.6), (9.0, 1.2, 4.0)),
        ("bullpen", (8.0, 1.6, 6.0), (14.0, 1.0, 13.0)),
        ("conference", (6.4, 1.6, 15.5), (1.5, 1.2, 9.5)),
        ("office", (18.6, 1.6, 15.4), (23.5, 1.0, 10.0)),
        ("breakroom", (6.5, 1.6, 8.5), (1.5, 1.0, 3.5)),
        ("closet", (15.3, 1.6, 0.4), (17.5, 1.3, 4.5)),
    ]
    for name, pos, tgt in interiors:
        cam = L.add_camera("cam_" + name, pos, tgt, 18)
        L.render(os.path.join(out_dir, f"office_in_{name}.png"), cam)


if __name__ == "__main__":
    a = args()
    build()
    if a["export"]:
        L.export_fbx(os.path.join(L.ROOT, "Assets", "Resources", "Models", "Office.fbx"))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(L.ROOT, "ArtSource", "office.blend"))
    if a["render"]:
        render_previews(a["render"])
