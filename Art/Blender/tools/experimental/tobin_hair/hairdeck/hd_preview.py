# Render HairDeck GLBs (hair + head proxy, glTF y-up face +z -> Blender z-up face -y) from front / 3/4 / side / back.
#   blender -b --factory-startup -P hd_preview.py -- <out_dir> <a.glb> [<b.glb> ...]   -> <out_dir>/<glb stem>_<view>.png
import bpy, sys, os, math
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]; OUT = a[0]; os.makedirs(OUT, exist_ok=True)
for glb in a[1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    bpy.ops.import_scene.gltf(filepath=glb)
    ms = [o for o in sc.objects if o.type == 'MESH']
    pts = [o.matrix_world @ Vector(c) for o in ms for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    tgt = (lo + hi) / 2; size = max(hi - lo) * 1.15
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = 600; sc.render.film_transparent = False
    sc.world = bpy.data.worlds.new('w'); sc.world.use_nodes = True; sc.world.node_tree.nodes['Background'].inputs[0].default_value = (0.4, 0.4, 0.42, 1)
    for nm, rot, en in (('key', (50, 0, -35), 3.0), ('fill', (60, 0, 140), 1.2)):
        l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); l.data.energy = en; l.rotation_euler = [math.radians(x) for x in rot]; sc.collection.objects.link(l)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = size; cam.data.clip_end = 100
    for v, az in dict(front=0, q34=-40, side=90, back=180).items():
        r = math.radians(az); d = Vector((math.sin(r), -math.cos(r), 0.05)).normalized()
        cam.location = tgt + d * size * 4; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, f'{os.path.basename(os.path.dirname(glb))}_{os.path.splitext(os.path.basename(glb))[0]}_{v}.png')
        bpy.ops.render.render(write_still=True)
print('HD_PREVIEW_DONE')
