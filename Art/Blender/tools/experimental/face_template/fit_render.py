# Blender bg: flat-grey ortho renders (front + right side) of one or more meshes given as npz(v,f), same camera.
# usage: -- out_prefix mesh1.npz [mesh2.npz ...]   (each mesh -> prefix_<i>_front.png/_side.png)
import bpy, sys, numpy as np, bmesh
a = sys.argv[sys.argv.index('--') + 1:]; pref = a[0]; files = a[1:]
CZ, S, R = 1.66, 0.46, 900
for i, fpath in enumerate(files):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    d = np.load(fpath); v, f = d['v'].astype(np.float32), d['f'].astype(np.int32)
    me = bpy.data.meshes.new('m'); me.from_pydata(v.tolist(), [], f.tolist()); me.update()
    ob = bpy.data.objects.new('m', me); sc = bpy.context.scene; sc.collection.objects.link(ob)
    for p in me.polygons: p.use_smooth = True
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = S
    sc.render.resolution_x = sc.render.resolution_y = R; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.62, 0.62, 0.62); sh.show_cavity = False
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w'); w.use_nodes = False; w.color = (0.35, 0.35, 0.38); sc.world = w
    for tag, loc, rot in (('front', (0, -3, CZ), (np.pi / 2, 0, 0)), ('side', (3, 0, CZ), (np.pi / 2, 0, np.pi / 2))):
        cam.location = loc; cam.rotation_euler = rot
        sc.render.filepath = f'{pref}_{i}_{tag}.png'; bpy.ops.render.render(write_still=True)
