"""Blender (background) clay renders of one head mesh from FIXED ortho cameras (same camera, scale and light for every character).
usage: blender -b --factory-startup --python clay_views.py -- <mesh.npz|mesh.glb-less> <out_prefix> [view,view,...] [res]
mesh.npz = pipeline format (v, f [, ev, ef eyeballs]) like fit/_rr_<tag>.npz. Views: front, q34 (40 deg), side (90 deg, subject's left), top (nose up).
Writes <out_prefix>_<view>.png and <out_prefix>_cam.json (pixel -> metre mapping: centre, scale). Matte clay colour, studio light, no cavity: the
only thing visible is shape. Fixed frame: centre x 0, y -0.02, z 1.70; ortho scale 0.34 m (so 1 px = 0.34/res m for every character).
Reuses the Workbench setup of ../stages/r6_07_sheet_blender.py (same camera orbit convention)."""
import bpy, sys, json, numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src, pref = a[0], a[1]
views = a[2].split(',') if len(a) > 2 else ['front', 'q34', 'side', 'top']; R = int(a[3]) if len(a) > 3 else 700
CZ, S = 1.70, 0.34
bpy.ops.wm.read_factory_settings(use_empty=True)
d = np.load(src); sc = bpy.context.scene
def add(v, f, name):
    me = bpy.data.meshes.new(name); me.from_pydata(v.astype(np.float32).tolist(), [], f.astype(np.int32).tolist()); me.update()
    for p in me.polygons: p.use_smooth = True
    sc.collection.objects.link(bpy.data.objects.new(name, me))
add(d['v'], d['f'], 'head')
if 'ev' in d.files: add(d['ev'], d['ef'], 'eyes')
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = S
sc.render.resolution_x = sc.render.resolution_y = R; sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.78, 0.76, 0.73); sh.show_cavity = False; sh.show_specular_highlight = False
sc.view_settings.view_transform = 'Standard'
sc.render.film_transparent = True; sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'   # alpha = exact silhouette; viewers composite on grey (vcommon.read_clay)
w = bpy.data.worlds.new('w'); w.use_nodes = False; w.color = (0.30, 0.31, 0.34); sc.world = w
c0 = np.array([0.0, -0.02, CZ])
for tag in views:
    if tag == 'top':
        cam.location = (c0[0], c0[1], c0[2] + 3); cam.rotation_euler = (0, 0, np.pi)      # looks down, nose to image top
    else:
        th = np.radians({'front': 0, 'side': 90, 'q34': 40}[tag]); pos = c0 + 3 * np.array([np.sin(th), -np.cos(th), 0])
        cam.location = pos; cam.rotation_euler = (np.pi / 2, 0, th)
    sc.render.filepath = f'{pref}_{tag}.png'; bpy.ops.render.render(write_still=True)
json.dump(dict(cx=float(c0[0]), cy=float(c0[1]), cz=CZ, S=S, R=R), open(pref + '_cam.json', 'w'))
print('CLAY_DONE', views)
