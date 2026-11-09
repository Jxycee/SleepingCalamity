"""Blender (background): many meshes, clay front + side renders, same camera/light as clay_views.py (fixed frame: centre x 0, y -0.02, z 1.70, ortho 0.34 m).
usage: blender -b --factory-startup --python atlas_render.py -- <jobs.json> <res>
jobs.json = {"<name>": "<mesh.npz with v, f [, ev, ef]>", ...}; writes <mesh dir>/<name>_front.png and _side.png next to each npz."""
import bpy, sys, json, os, numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; jobs = json.load(open(a[0])); R = int(a[1]) if len(a) > 1 else 384
CZ, S = 1.70, 0.34
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = S
sc.render.resolution_x = sc.render.resolution_y = R; sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.78, 0.76, 0.73); sh.show_cavity = False; sh.show_specular_highlight = False
sc.view_settings.view_transform = 'Standard'
sc.render.film_transparent = True; sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'   # alpha = exact silhouette; viewers composite on grey (vcommon.read_clay)
w = bpy.data.worlds.new('w'); w.use_nodes = False; w.color = (0.30, 0.31, 0.34); sc.world = w
c0 = np.array([0.0, -0.02, CZ])
def add(v, f, name):
    me = bpy.data.meshes.new(name); me.from_pydata(v.astype(np.float32).tolist(), [], f.astype(np.int32).tolist()); me.update()
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); return ob
for name, path in jobs.items():
    d = np.load(path); obs = [add(d['v'], d['f'], 'head')]
    if 'ev' in d.files: obs.append(add(d['ev'], d['ef'], 'eyes'))
    for tag, th in (('front', 0.0), ('side', np.pi / 2)):
        cam.location = c0 + 3 * np.array([np.sin(th), -np.cos(th), 0]); cam.rotation_euler = (np.pi / 2, 0, th)
        sc.render.filepath = os.path.join(os.path.dirname(path), f'{name}_{tag}.png'); bpy.ops.render.render(write_still=True)
    for ob in obs:
        me = ob.data; bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
print('ATLAS_DONE', len(jobs))
