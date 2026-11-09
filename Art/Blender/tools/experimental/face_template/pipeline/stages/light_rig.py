"""Shared neutral soft-box rig for every textured face render in the pipeline (proof sheet / closeups / overlay in c6_05_render.py, skin_detail/blender_render.py).
Round 16 (user: 'a bit bright', then 'harsh side light'): the old rig was a 22 W 0.7 m key 35 deg off-axis + 7 W fill + 9 W rim on a 0.19 world; lit skin patches rendered at
L* 87-89 for an albedo of L* ~65, with a hard terminator down the face. This one is frontal-wrap: a large key 25 deg off-axis, a nearly as strong large fill from the other side
(key:fill 1.4:1), a big soft top box, a faint rim and a neutral world. Albedo is untouched; `SCP_EXPOSURE` (EV) trims the whole rig.
Usage inside Blender: light_rig.build(bpy, mathutils, scene, target_xyz, view_dir_xyz)  (view_dir = from target towards the camera at 0 deg azimuth)."""
import math, os

KEY_DEG = float(os.environ.get('SCP_KEY_DEG', '25'))
EXPOSURE = float(os.environ.get('SCP_EXPOSURE', '-0.2'))
# name, azimuth (deg, +ve = camera-left of the view axis... see side vector), elevation (deg), distance m, watts, size m
RIG = [('key', +1.0, 20, 1.1, 14.0, 1.4), ('fill', -1.0, 8, 1.1, 10.0, 1.8), ('top', 0.0, 65, 1.0, 4.0, 1.6), ('rim', 0.0, 25, 1.0, 3.0, 0.6)]


def clear(bpy):
    for o in [o for o in bpy.data.objects if o.type == 'LIGHT']: bpy.data.objects.remove(o)


def build(bpy, mathutils, scene, target, view_dir, key_deg=None, scale=1.0):
    clear(bpy)
    T = mathutils.Vector(target); D = mathutils.Vector(view_dir).normalized(); up = mathutils.Vector((0, 0, 1)); side = D.cross(up).normalized()
    kd = KEY_DEG if key_deg is None else key_deg
    for name, sgn, elev, dist, watts, size in RIG:
        if name == 'rim': direction = (-D * math.cos(math.radians(elev)) + up * math.sin(math.radians(elev)))
        else:
            az = math.radians(kd * sgn if name in ('key', 'fill') else 0.0); e = math.radians(elev)
            direction = (D * math.cos(az) + side * math.sin(az)) * math.cos(e) + up * math.sin(e)
        direction = direction.normalized()
        L = bpy.data.lights.new(name, 'AREA'); L.energy = watts * scale; L.size = size
        o = bpy.data.objects.new(name, L); scene.collection.objects.link(o); o.location = T + direction * dist
        o.rotation_euler = (T - o.location).to_track_quat('-Z', 'Y').to_euler()
    scene.view_settings.view_transform = 'Standard'; scene.view_settings.exposure = EXPOSURE
    w = bpy.data.worlds.new('w'); w.use_nodes = True; bg = w.node_tree.nodes['Background']
    bg.inputs[0].default_value = (0.58, 0.58, 0.6, 1); bg.inputs[1].default_value = 0.45; scene.world = w
