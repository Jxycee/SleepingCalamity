"""Blender (-b -P) workbench previews: flat grey head, vertex colour = |delta| heat. Args after '--': job names (existing slider
names or SC_ names); writes OUT/prev_<name>_<view>.png (front, side). Run via sl_03_preview.py (which composes sheets)."""
import bpy, sys, numpy as np, math
from mathutils import Vector
OUT = 'C:/AI/test_out/heroes/face_spike2/sliders/'
T = np.load('C:/AI/test_out/heroes/face_spike/template/tobin_head_template.npz', allow_pickle=False)
B = T['head/basis']; Fc = T['head/faces']; K = T['head/keys']; kn = [str(x) for x in T['head/key_names']]
Z = np.load(OUT + 'new_sliders.npz'); nn = [str(x) for x in Z['names']]
jobs = sys.argv[sys.argv.index('--') + 1:]
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 420; sc.render.resolution_y = 520
sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'VERTEX'; sh.show_cavity = False
sc.view_settings.view_transform = 'Standard'
sc.world = bpy.data.worlds.new('w'); sc.world.color = (0.12, 0.12, 0.13)
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.30
def build(delta):
    me = bpy.data.meshes.new('m'); me.from_pydata(B + delta, [], Fc.tolist()); me.update()
    mag = np.linalg.norm(delta, axis=1) * 1000
    a = me.color_attributes.new('c', 'FLOAT_COLOR', 'POINT')
    h = np.clip(mag / 6.0, 0, 1)[:, None]
    col = (1 - h) * np.array([0.62, 0.62, 0.64]) + h * np.array([0.95, 0.15, 0.05])
    a.data.foreach_set('color', np.c_[col, np.ones(len(col))].ravel())
    ob = bpy.data.objects.new('o', me); sc.collection.objects.link(ob); return ob
for j in jobs:
    d = (Z['delta'][nn.index(j)] if j in nn else K[kn.index(j)] - B).astype(np.float64)
    ob = build(d)
    for view, loc, rot in (('front', (0.0, -1.0, 1.70), (90, 0, 0)), ('side', (-1.0, 0.0, 1.70), (90, 0, -90))):
        cam.location = loc; cam.rotation_euler = [math.radians(r) for r in rot]
        sc.render.filepath = OUT + 'prev_%s_%s.png' % (j.replace('+', 'P').replace('-', 'M'), view)
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(ob)
