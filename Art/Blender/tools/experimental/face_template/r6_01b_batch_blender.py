# Blender bg: render many front-ortho Workbench images in one session.  usage: -- z0 z1 out_prefix mesh1.npz mesh2.npz ...   -> <out_prefix><i>.png + <out_prefix><i>.json (cam)
import bpy, sys, json, numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; z0, z1, pref = float(a[0]), float(a[1]), a[2]; files = a[3:]
for i, fp in enumerate(files):
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    d = np.load(fp); v, f = d['v'].astype(np.float64), d['f'].astype(np.int64)
    ev, ef = d['ev'], d['ef']
    for k, (vv, ff) in enumerate([(v, f), (ev, ef)]):
        me = bpy.data.meshes.new(f'm{k}'); me.from_pydata(vv.astype(np.float32).tolist(), [], ff.astype(np.int32).tolist()); me.update()
        ob = bpy.data.objects.new(f'm{k}', me); sc.collection.objects.link(ob)
        for p in me.polygons: p.use_smooth = True
    cx = float((v[:, 0].min() + v[:, 0].max()) / 2); cz = (z0 + z1) / 2; S = (z1 - z0) * 1.04; R = 1400
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = S; cam.location = (cx, -5, cz); cam.rotation_euler = (np.pi / 2, 0, 0)
    sc.render.resolution_x = sc.render.resolution_y = R; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.75, 0.72, 0.68); sh.show_cavity = False
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w'); w.use_nodes = False; w.color = (0.55, 0.55, 0.57); sc.world = w
    sc.render.filepath = f'{pref}{i}.png'; bpy.ops.render.render(write_still=True)
    json.dump(dict(cx=cx, cz=cz, S=S, R=R), open(f'{pref}{i}.json', 'w'))
print('OK', len(files))
