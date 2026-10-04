"""Modeling helpers for After Hours (Blender 4.5, bpy + bmesh).

Everything is authored in *Unity* coordinates (x right/east, y up, z forward/north) and converted
to Blender space when vertices are created. The FBX export settings in `export_fbx` map Blender
(x, y, z) -> Unity (-x, z, -y); `u2b` is the inverse. Objects keep identity rotation, so what you
author here is exactly what Unity sees.

Material names carry the look so Unity rebuilds materials from them (see Materials.cs):
    col_RRGGBB[_sNN][_mNN]  colour, smoothness %, metallic %
    glow_RRGGBB[_iNN]       emissive, intensity NN/10
    glass_RRGGBB_aNN        transparent glass
    tex_NAME[_RRGGBB]       textured (Resources/Textures/NAME), optional tint
    screen_NAME             monitor screen (driven at runtime)
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def u2b(p):
    """Unity (x, y, z) -> Blender (x, y, z)."""
    return Vector((-p[0], -p[2], p[1]))


# ---------------------------------------------------------------------------------------------
# scene
# ---------------------------------------------------------------------------------------------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)


def collection(name, parent=None):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(c)
    return c


_active = None


def use_collection(c):
    global _active
    _active = c


def _link(obj):
    (_active or bpy.context.scene.collection).objects.link(obj)


# ---------------------------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------------------------

def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def col(hexstr, s=None, m=None):
    name = "col_" + hexstr.lstrip("#").upper()
    if s is not None:
        name += f"_s{int(s)}"
    if m is not None:
        name += f"_m{int(m)}"
    return name


def glow(hexstr, i=20):
    return f"glow_{hexstr.lstrip('#').upper()}_i{int(i)}"


def glass(hexstr, a=12):
    return f"glass_{hexstr.lstrip('#').upper()}_a{int(a)}"


def tex(name, tint=None, s=None):
    n = "tex_" + name
    if tint:
        n += "_" + tint.lstrip("#").upper()
    if s is not None:
        n += f"_s{int(s)}"
    return n


def shade(hexstr, factor):
    h = hexstr.lstrip("#")
    rgb = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    if factor >= 1:
        rgb = [int(c + (255 - c) * (factor - 1)) for c in rgb]
    else:
        rgb = [int(c * factor) for c in rgb]
    return "".join(f"{max(0, min(255, c)):02X}" for c in rgb)


def material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    parts = name.split("_")
    rgb = (0.8, 0.8, 0.8)
    for p in parts[1:]:
        if len(p) == 6:
            try:
                rgb = tuple(_srgb_to_linear(int(p[i:i + 2], 16) / 255) for i in (0, 2, 4))
                break
            except ValueError:
                pass
    if parts[0] == "tex":
        tints = {"carpet": (0.12, 0.15, 0.2), "wood": (0.45, 0.3, 0.17), "tile": (0.7, 0.7, 0.68), "ceiling": (0.85, 0.85, 0.83)}
        rgb = next((v for k, v in tints.items() if k in name), rgb)
    mat.diffuse_color = (*rgb, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        rough = 0.6
        for p in parts:
            if p.startswith("s") and p[1:].isdigit():
                rough = 1 - int(p[1:]) / 100
        bsdf.inputs["Roughness"].default_value = rough
        if parts[0] == "glow":
            bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 4.0
        if parts[0] == "glass":
            bsdf.inputs["Alpha"].default_value = 0.2
            mat.blend_method = "BLEND" if hasattr(mat, "blend_method") else None
        for p in parts:
            if p.startswith("m") and p[1:].isdigit():
                bsdf.inputs["Metallic"].default_value = int(p[1:]) / 100
    return mat


# ---------------------------------------------------------------------------------------------
# mesh building (Unity space)
# ---------------------------------------------------------------------------------------------

class MeshBuilder:
    """Accumulates parts (each with a material) and creates one Blender object."""

    def __init__(self, name, origin=(0, 0, 0)):
        self.name = name
        self.origin = Vector(origin)
        self.bm = bmesh.new()
        self.mats = []
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _mat_index(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def box(self, center, size, mat, bevel=0.0, rot_y=0.0, segments=2, faces=None, uv_scale=1.0):
        """Axis-aligned box in Unity space (optionally yawed about its centre)."""
        cx, cy, cz = center
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        for v in tmp.verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bevel > 0:
            b = min(bevel, sx * 0.95, sy * 0.95, sz * 0.95)
            bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=b, segments=segments, affect="EDGES", profile=0.5)
        if faces is not None:
            # Remove faces whose normal is not in the allowed set (e.g. skip bottoms)
            kill = []
            for f in tmp.faces:
                n = f.normal
                keep = False
                for axis in faces:
                    a = {"+x": (1, 0, 0), "-x": (-1, 0, 0), "+y": (0, 1, 0), "-y": (0, -1, 0), "+z": (0, 0, 1), "-z": (0, 0, -1)}[axis]
                    if n.dot(Vector(a)) > 0.5:
                        keep = True
                if not keep and max(abs(n.x), abs(n.y), abs(n.z)) > 0.99:
                    kill.append(f)
            bmesh.ops.delete(tmp, geom=kill, context="FACES")
        rot = Matrix.Rotation(math.radians(rot_y), 4, "Y")
        self._merge(tmp, mat, lambda co: rot @ co + Vector((cx, cy, cz)), uv_scale)
        tmp.free()
        return self

    def cylinder(self, center, radius, height, mat, segments=24, axis="y", bevel=0.0, cap=True, radius_top=None):
        tmp = bmesh.new()
        r2 = radius if radius_top is None else radius_top
        bmesh.ops.create_cone(tmp, cap_ends=cap, cap_tris=False, segments=segments, radius1=radius, radius2=r2, depth=height)
        # create_cone is along local Z; we want along `axis` in Unity space (treat tmp as unity coords)
        if axis == "y":
            m = Matrix.Rotation(math.radians(-90), 4, "X")
        elif axis == "x":
            m = Matrix.Rotation(math.radians(90), 4, "Y")
        else:
            m = Matrix.Identity(4)
        bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
        if bevel > 0:
            edges = [e for e in tmp.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > 0.5]
            bmesh.ops.bevel(tmp, geom=edges, offset=bevel, segments=2, affect="EDGES", profile=0.5)
        c = Vector(center)
        self._merge(tmp, mat, lambda co: co + c)
        tmp.free()
        return self

    def sphere(self, center, radius, mat, segments=16, rings=10, scale=(1, 1, 1)):
        tmp = bmesh.new()
        bmesh.ops.create_uvsphere(tmp, u_segments=segments, v_segments=rings, radius=radius)
        s = Vector(scale)
        c = Vector(center)
        self._merge(tmp, mat, lambda co: Vector((co.x * s.x, co.z * s.y, co.y * s.z)) + c)
        tmp.free()
        return self

    def poly_extrude(self, points_xz, y0, y1, mat, bevel=0.0):
        """Extrude a 2D polygon (Unity x,z) between heights y0..y1."""
        tmp = bmesh.new()
        bottom = [tmp.verts.new((x, y0, z)) for x, z in points_xz]
        top = [tmp.verts.new((x, y1, z)) for x, z in points_xz]
        n = len(points_xz)
        tmp.faces.new(list(reversed(bottom)))
        tmp.faces.new(top)
        for i in range(n):
            j = (i + 1) % n
            tmp.faces.new([bottom[i], bottom[j], top[j], top[i]])
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
        if bevel > 0:
            bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bevel, segments=2, affect="EDGES", profile=0.5)
        self._merge(tmp, mat, lambda co: co)
        tmp.free()
        return self

    def quad(self, corners, mat, uv=None):
        """A single face from 4 Unity-space corners (counter-clockwise seen from the front)."""
        tmp = bmesh.new()
        vs = [tmp.verts.new(Vector(c)) for c in corners]
        f = tmp.faces.new(vs)
        self._merge(tmp, mat, lambda co: co, uv_override=uv)
        tmp.free()
        return self

    def _merge(self, tmp, mat, xform, uv_scale=1.0, uv_override=None):
        mi = self._mat_index(mat)
        vmap = {}
        for v in tmp.verts:
            p = xform(v.co.copy()) - self.origin
            vmap[v] = self.bm.verts.new(u2b(p))
        self.bm.verts.ensure_lookup_table()
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = mi
            nf.smooth = True
            # Box-projected UVs in metres (Unity space), so tiling textures are scale-consistent.
            nrm = f.normal
            for k, loop in enumerate(nf.loops):
                if uv_override:
                    loop[self.uv].uv = uv_override[k]
                    continue
                co = xform(f.verts[k].co.copy())
                ax, ay, az = abs(nrm.x), abs(nrm.y), abs(nrm.z)
                if ay >= ax and ay >= az:
                    u, w = co.x, co.z
                elif ax >= az:
                    u, w = co.z, co.y
                else:
                    u, w = co.x, co.y
                loop[self.uv].uv = (u * uv_scale, w * uv_scale)

    def build(self, smooth_angle=35):
        mesh = bpy.data.meshes.new(self.name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.mats:
            mesh.materials.append(material(m))
        try:
            mesh.set_sharp_from_angle(angle=math.radians(smooth_angle))
        except AttributeError:
            pass
        obj = bpy.data.objects.new(self.name, mesh)
        obj.location = u2b(self.origin)
        _link(obj)
        return obj


def box(name, center, size, mat, bevel=0.0, rot_y=0.0, pivot=None, **kw):
    """Single-material box object. Pivot defaults to the box centre."""
    mb = MeshBuilder(name, pivot if pivot is not None else center)
    mb.box(center, size, mat, bevel=bevel, rot_y=rot_y, **kw)
    return mb.build()


def empty(name, pos, size=0.15):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = size
    e.location = u2b(pos)
    _link(e)
    return e


def grime_plane(name, center, normal, size_uv, v_axis=None):
    """A quad marking a cleanable surface. normal/v_axis are Unity-space unit vectors."""
    n = Vector(normal).normalized()
    if v_axis is None:
        v_axis = (0, 1, 0) if abs(n.y) < 0.5 else (0, 0, 1)
    v = Vector(v_axis).normalized()
    u = n.cross(v)  # Unity: right = up x forward with up=normal, forward=v  -> right = n x v
    u.normalize()
    c = Vector(center) + n * 0.002
    hu, hv = size_uv[0] / 2, size_uv[1] / 2
    corners = [c - u * hu - v * hv, c + u * hu - v * hv, c + u * hu + v * hv, c - u * hu + v * hv]
    mb = MeshBuilder(name, center)
    mb.quad(corners, "col_FF00FF")
    obj = mb.build()
    return obj


# ---------------------------------------------------------------------------------------------
# export / render
# ---------------------------------------------------------------------------------------------

def export_fbx(path, objects=None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    objs = objects if objects is not None else [o for o in bpy.context.scene.objects]
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP", use_custom_props=False)
    print("exported", os.path.relpath(path, ROOT), len(objs), "objects")


def setup_render(res=(1280, 720), samples=32):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = samples
    except AttributeError:
        pass
    world = bpy.data.worlds.new("World") if not bpy.data.worlds else bpy.data.worlds[0]
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.05, 0.06, 0.08, 1)
        bg.inputs[1].default_value = 1.0
    sc.view_settings.view_transform = "AgX" if "AgX" in [v.identifier for v in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items] else "Filmic"


def add_camera(name, pos_u, target_u, lens=24):
    cam = bpy.data.cameras.new(name)
    cam.lens = lens
    obj = bpy.data.objects.new(name, cam)
    obj.location = u2b(pos_u)
    direction = u2b(target_u) - u2b(pos_u)
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(obj)
    return obj


def add_sun(strength=3.0, rot=(50, 0, 30)):
    s = bpy.data.lights.new("Sun", "SUN")
    s.energy = strength
    o = bpy.data.objects.new("Sun", s)
    o.rotation_euler = tuple(math.radians(a) for a in rot)
    bpy.context.scene.collection.objects.link(o)
    return o


def add_area(pos_u, energy=300, size=4.0):
    l = bpy.data.lights.new("Area", "AREA")
    l.energy = energy
    l.size = size
    o = bpy.data.objects.new("Area", l)
    o.location = u2b(pos_u)
    bpy.context.scene.collection.objects.link(o)
    return o


def render(path, camera):
    sc = bpy.context.scene
    sc.camera = camera
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print("rendered", path)
