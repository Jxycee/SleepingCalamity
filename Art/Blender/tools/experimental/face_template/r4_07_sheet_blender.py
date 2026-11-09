# Blender bg: flat-grey ortho renders (front, side, 3/4) of meshes given as npz(v,f); same camera. usage: -- out_prefix mesh1.npz [mesh2.npz ...]
import bpy, sys, numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; pref = a[0]; files = a[1:]
CZ, S, R = 1.70, 0.30, 800
cx = 0.0
for i, fpath in enumerate(files):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    d = np.load(fpath); v, f = d['v'].astype(np.float32), d['f'].astype(np.int32)
    me = bpy.data.meshes.new('m'); me.from_pydata(v.tolist(), [], f.tolist()); me.update()
    ob = bpy.data.objects.new('m', me); sc = bpy.context.scene; sc.collection.objects.link(ob)
    for p in me.polygons: p.use_smooth = True
    if 'ev' in d.files:
        me2 = bpy.data.meshes.new('e'); me2.from_pydata(d['ev'].astype(np.float32).tolist(), [], d['ef'].astype(np.int32).tolist()); me2.update()
        sc.collection.objects.link(bpy.data.objects.new('e', me2))
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = S
    sc.render.resolution_x = sc.render.resolution_y = R; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.7, 0.68, 0.65); sh.show_cavity = False
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w'); w.use_nodes = False; w.color = (0.35, 0.35, 0.38); sc.world = w
    c0 = np.array([0, -0.02, CZ])
    for tag, ang in (('front', 0), ('side', 90), ('q34', 40)):
        th = np.radians(ang)   # camera orbits about +Z from the -Y axis towards +X
        pos = c0 + 3 * np.array([np.sin(th), -np.cos(th), 0])
        cam.location = pos; cam.rotation_euler = (np.pi / 2, 0, th)
        sc.render.filepath = f'{pref}_{i}_{tag}.png'; bpy.ops.render.render(write_still=True)
