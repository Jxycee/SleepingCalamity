"""Review renders of a game .blend (baked maps only) in Eevee.
blender -b <file.blend> --python review.py -- --out <dir> --tag <name> [--engine EEVEE|CYCLES] [--target Bench]
Two moods x fixed cameras: 'studio' (neutral grey, soft key) and 'hearth' (dim room, warm fire/candle key).
"""
import sys, os, math
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); TAG = arg('--tag', 'x'); ENG = arg('--engine', 'EEVEE')
RES = int(arg('--res', '1280'))
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene

objs = [o for o in sc.objects if o.type == 'MESH']
mn = Vector((min(min((o.matrix_world @ Vector(c)).x for c in o.bound_box) for o in objs),
             min(min((o.matrix_world @ Vector(c)).y for c in o.bound_box) for o in objs),
             min(min((o.matrix_world @ Vector(c)).z for c in o.bound_box) for o in objs)))
mx = Vector((max(max((o.matrix_world @ Vector(c)).x for c in o.bound_box) for o in objs),
             max(max((o.matrix_world @ Vector(c)).y for c in o.bound_box) for o in objs),
             max(max((o.matrix_world @ Vector(c)).z for c in o.bound_box) for o in objs)))
ctr = (mn + mx) / 2; size = (mx - mn).length

# floor + back wall (plain, rough plaster-ish grey) so contact shadows show
def plane(name, loc, rot, s, col, rough=0.9):
    bpy.ops.mesh.primitive_plane_add(size=s, location=loc, rotation=rot)
    p = bpy.context.active_object; p.name = name
    m = bpy.data.materials.new(name); m.use_nodes = True
    bs = m.node_tree.nodes['Principled BSDF']; bs.inputs['Base Color'].default_value = (*col, 1); bs.inputs['Roughness'].default_value = rough
    p.data.materials.append(m)
plane('floor', (ctr.x, ctr.y, mn.z), (0, 0, 0), 12, (0.09, 0.08, 0.07))
plane('wall', (ctr.x, mx.y + 0.35, mn.z + 3), (math.radians(90), 0, 0), 12, (0.12, 0.11, 0.1))

if ENG == 'EEVEE':
    sc.render.engine = 'BLENDER_EEVEE'
    e = sc.eevee
    for k, v in (('use_raytracing', True), ('taa_render_samples', 64), ('use_shadows', True), ('use_gtao', True)):
        if hasattr(e, k): setattr(e, k, v)
else:
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 128; sc.cycles.use_denoising = True
    p = bpy.context.preferences.addons['cycles'].preferences; p.compute_device_type = 'OPTIX'; p.get_devices()
    for d in p.devices: d.use = d.type == 'OPTIX'
    sc.cycles.device = 'GPU'
sc.render.resolution_x = RES; sc.render.resolution_y = int(RES * 0.66)
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Base Contrast' if 'AgX - Base Contrast' in [l.identifier for l in bpy.types.ColorManagedViewSettings.bl_rna.properties['look'].enum_items] else 'None'

def light(name, typ, loc, energy, col, size=0.5):
    ld = bpy.data.lights.new(name, typ); ld.energy = energy; ld.color = col
    if typ == 'AREA': ld.size = size
    else: ld.shadow_soft_size = size
    ob = bpy.data.objects.new(name, ld); sc.collection.objects.link(ob); ob.location = loc
    d = ctr - Vector(loc); ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    return ob
def clear_lights():
    for o in [o for o in sc.objects if o.type == 'LIGHT']: bpy.data.objects.remove(o)

cam_d = bpy.data.cameras.new('cam'); cam = bpy.data.objects.new('cam', cam_d); sc.collection.objects.link(cam); sc.camera = cam
def shoot(name, pos, look, lens=40):
    cam.location = pos; cam_d.lens = lens
    cam.rotation_euler = (Vector(look) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, f'{TAG}_{name}.png'); bpy.ops.render.render(write_still=True)

top = Vector((ctr.x, ctr.y, mx.z))
views = [('34', ctr + Vector((size * 0.75, -size * 1.0, size * 0.55)), ctr, 45),
         ('front', ctr + Vector((0, -size * 1.3, size * 0.15)), ctr, 50),
         ('close', top + Vector((0.35, -0.55, 0.35)) * min(1.0, size / 1.8), top + Vector((0.05, 0, -0.03)) * min(1.0, size / 1.8), 50),
         ('corner', Vector((mx.x + 0.45, mn.y - 0.55, mx.z + 0.1)) if size > 1 else ctr + Vector((-0.5, -0.9, 0.7)) * size, Vector((mx.x - 0.12, mn.y + 0.12, mx.z - 0.2)) if size > 1 else ctr, 50)]

w = sc.world or bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']
# studio
clear_lights(); bg.inputs['Color'].default_value = (0.05, 0.05, 0.055, 1); bg.inputs['Strength'].default_value = 1.0
light('key', 'AREA', ctr + Vector((-1.5, -2.0, 2.2)), 160, (1, 0.97, 0.92), 1.5)
light('fill', 'AREA', ctr + Vector((2.2, -1.2, 1.0)), 50, (0.9, 0.95, 1.0), 2.0)
light('rim', 'AREA', ctr + Vector((0.5, 2.0, 2.0)), 120, (1, 1, 1), 1.0)
for n, p, l, lens in views: shoot('studio_' + n, p, l, lens)
# hearth: dim, warm low key (fire at one side), small candle, cool weak skylight
clear_lights(); bg.inputs['Color'].default_value = (0.012, 0.014, 0.02, 1)
light('fire', 'POINT', ctr + Vector((-1.1, -0.5, -0.1)), 180, (1.0, 0.45, 0.15), 0.25)
light('candle', 'POINT', top + Vector((0.35, -0.05, 0.25)) * min(1.0, size), 25 * min(1.0, size) ** 2, (1.0, 0.62, 0.3), 0.03)
light('window', 'AREA', ctr + Vector((1.5, -1.5, 2.5)), 60, (0.7, 0.8, 1.0), 1.0)
for n, p, l, lens in views: shoot('hearth_' + n, p, l, lens)
print('RENDERED', TAG)
