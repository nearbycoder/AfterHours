"""Cleaning tools (view models) -> Assets/Resources/Models/Tools/<name>.fbx

    blender -b -P ArtSource/tools.py -- [--render DIR]

Conventions (match ToolRig.cs):
  *_grip   handle held in view space; the shaft leaves along local +Z.
  *_head   touches the surface: local +Y = surface normal (bottom at y=0), +Z = away from player.
  spray_bottle / cloth / uv_torch: origin at the centre of the object, nozzle/lens toward +Z.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ah_lib as L  # noqa: E402
from ah_lib import MeshBuilder, col, glass, glow  # noqa: E402

YELLOW = col("F4C430", 50)
ORANGE = col("F08A24", 50)
BLUE = col("2F6FD8", 55)
RUBBER = col("1C1E21", 25)
STEEL = col("C3CAD1", 75, 90)
WHITE = col("EEF0F2", 45)
GREY = col("3A3E45", 45)


def spray_bottle():
    mb = MeshBuilder("spray_bottle")
    mb.cylinder((0, -0.02, 0), 0.036, 0.16, glass("CFE9F5", 55), segments=24, bevel=0.006)
    mb.cylinder((0, -0.04, 0), 0.0345, 0.11, col("57C8E8", 40), segments=24)       # liquid
    mb.cylinder((0, 0.068, 0), 0.02, 0.02, WHITE, segments=18)
    mb.box((0, 0.1, 0.012), (0.036, 0.05, 0.075), BLUE, bevel=0.01)                 # trigger head
    mb.box((0, 0.11, 0.06), (0.016, 0.016, 0.03), BLUE, bevel=0.005)                # nozzle
    mb.box((0, 0.07, 0.035), (0.012, 0.045, 0.012), BLUE, bevel=0.004, rot_x=-15)   # trigger
    mb.box((0, -0.02, 0.0362), (0.045, 0.06, 0.002), col("F4F1E8", 20))            # label
    return mb.build()


def cloth():
    mb = MeshBuilder("cloth")
    c = col("F2C14E", 5)
    c2 = col("E5B23F", 5)
    mb.box((0, 0, 0), (0.12, 0.022, 0.1), c, bevel=0.009, segments=3)
    mb.box((0.012, 0.016, -0.008), (0.1, 0.018, 0.085), c2, bevel=0.008, segments=3, rot_y=8)
    mb.box((-0.03, 0.03, 0.01), (0.06, 0.016, 0.07), c, bevel=0.007, segments=3, rot_y=-15)
    return mb.build()


def vacuum_grip():
    mb = MeshBuilder("vacuum_grip")
    mb.box((0, 0, 0), (0.045, 0.05, 0.17), ORANGE, bevel=0.016, segments=3)
    mb.box((0, 0.03, -0.02), (0.03, 0.03, 0.08), GREY, bevel=0.01)
    mb.box((0, 0.042, 0.02), (0.012, 0.008, 0.02), col("E83A3A", 50), bevel=0.003)
    mb.cylinder((0, 0, 0.09), 0.019, 0.04, GREY, segments=16, axis="z")
    return mb.build()


def vacuum_head():
    mb = MeshBuilder("vacuum_head")
    mb.box((0, 0.022, 0), (0.36, 0.04, 0.11), GREY, bevel=0.014, segments=3)
    mb.box((0, 0.006, 0), (0.355, 0.012, 0.1), RUBBER, bevel=0.004)
    mb.box((0, 0.002, 0.052), (0.34, 0.004, 0.006), glow("FFB45E", 15))            # headlight strip
    mb.cylinder((0, 0.06, -0.02), 0.024, 0.05, ORANGE, segments=16)
    mb.sphere((0, 0.08, -0.02), 0.026, ORANGE, segments=14, rings=8)
    for sx in (-1, 1):
        mb.cylinder((sx * 0.15, 0.014, -0.04), 0.014, 0.02, RUBBER, segments=12, axis="x")
    return mb.build()


def squeegee_grip():
    mb = MeshBuilder("squeegee_grip")
    mb.box((0, 0, 0), (0.04, 0.04, 0.15), YELLOW, bevel=0.016, segments=3)
    for i in range(4):
        mb.box((0, 0.021, -0.05 + i * 0.03), (0.036, 0.004, 0.012), col("C9A11F", 40))
    return mb.build()


def squeegee_head():
    mb = MeshBuilder("squeegee_head")
    mb.box((0, 0.008, 0), (0.39, 0.016, 0.012), RUBBER, bevel=0.003)
    mb.box((0, 0.024, 0), (0.4, 0.018, 0.028), STEEL, bevel=0.005)
    mb.box((0, 0.045, 0), (0.06, 0.03, 0.03), YELLOW, bevel=0.01)
    mb.cylinder((0, 0.065, 0), 0.014, 0.02, YELLOW, segments=12)
    return mb.build()


def mop_grip():
    mb = MeshBuilder("mop_grip")
    mb.box((0, 0, 0), (0.042, 0.042, 0.14), BLUE, bevel=0.016, segments=3)
    mb.cylinder((0, 0, -0.08), 0.018, 0.02, col("1E4DA0", 40), segments=14, axis="z")
    return mb.build()


def mop_head():
    mb = MeshBuilder("mop_head")
    mb.box((0, 0.035, 0), (0.44, 0.02, 0.11), BLUE, bevel=0.008)
    mb.box((0, 0.055, 0), (0.06, 0.03, 0.04), col("1E4DA0", 40), bevel=0.01)
    fringe = col("E2DCCD", 5)
    fringe2 = col("D3CCBA", 5)
    for i in range(14):
        x = -0.2 + i * (0.4 / 13)
        mb.box((x, 0.014, 0.0), (0.03, 0.026, 0.14 + (i % 3) * 0.01), fringe if i % 2 else fringe2, bevel=0.008, rot_y=(i % 5 - 2) * 4)
    return mb.build()


def uv_torch():
    mb = MeshBuilder("uv_torch")
    mb.cylinder((0, 0, 0), 0.016, 0.13, GREY, segments=18, axis="z", bevel=0.003)
    mb.cylinder((0, 0, 0.075), 0.022, 0.03, GREY, segments=18, axis="z", bevel=0.003)
    mb.cylinder((0, 0, 0.0905), 0.019, 0.004, glow("8E6BFF", 30), segments=18, axis="z")
    mb.box((0, 0.017, 0.02), (0.01, 0.006, 0.02), col("8E6BFF", 50), bevel=0.002)
    return mb.build()


TOOLS = {
    "spray_bottle": spray_bottle, "cloth": cloth,
    "vacuum_grip": vacuum_grip, "vacuum_head": vacuum_head,
    "squeegee_grip": squeegee_grip, "squeegee_head": squeegee_head,
    "mop_grip": mop_grip, "mop_head": mop_head,
    "uv_torch": uv_torch,
}


if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.join(L.ROOT, "Assets", "Resources", "Models", "Tools")
    objs = []
    for name, fn in TOOLS.items():
        L.reset_scene()
        o = fn()
        o.name = name
        L.export_fbx(os.path.join(out, name + ".fbx"), [o])
    if "--render" in a:
        L.reset_scene()
        objs = []
        for i, (name, fn) in enumerate(TOOLS.items()):
            o = fn()
            o.location = L.u2b(((i % 5) * 0.5, 0.2, -(i // 5) * 0.5))
            objs.append(o)
        L.setup_render((1400, 700), 16)
        L.add_sun(3.0, (40, 0, 30))
        L.add_area((1.0, 2.0, 0.5), 600, 4)
        cam = L.add_camera("cam", (1.0, 1.3, 1.2), (1.0, 0.1, -0.3), 35)
        L.render(os.path.join(a[a.index("--render") + 1], "tools_sheet.png"), cam)
