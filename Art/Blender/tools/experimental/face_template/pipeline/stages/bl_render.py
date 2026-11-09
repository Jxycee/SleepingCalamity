"""Shared front-ortho Workbench render used by r6_01b_batch_blender.py (one launch per call) and r6_render_server.py (persistent worker). Runs inside Blender (bpy).
Workbench is Blender's OpenGL rasteriser: it already runs on the GPU (also with `blender -b`) and has no CPU mode; round 17 measured EEVEE (0.28 s, landmarks +2.5-3.1 px mean) and Cycles OPTIX
(1.4 s) against it for the landmark loop and kept Workbench (0.19 s/render in a running Blender). Every call starts from empty factory settings, so a render never depends on the previous one."""
import bpy, json, numpy as np
R_PX = 1400
def render_one(fp, z0, z1, out_png, out_json):
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    d = np.load(fp); v, f = d['v'].astype(np.float64), d['f'].astype(np.int64)
    ev, ef = d['ev'], d['ef']
    for k, (vv, ff) in enumerate([(v, f), (ev, ef)]):
        me = bpy.data.meshes.new(f'm{k}'); me.from_pydata(vv.astype(np.float32).tolist(), [], ff.astype(np.int32).tolist()); me.update()
        ob = bpy.data.objects.new(f'm{k}', me); sc.collection.objects.link(ob)
        for p in me.polygons: p.use_smooth = True
    cx = float((v[:, 0].min() + v[:, 0].max()) / 2); cz = (z0 + z1) / 2; S = (z1 - z0) * 1.04; R = R_PX
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = S; cam.location = (cx, -5, cz); cam.rotation_euler = (np.pi / 2, 0, 0)
    sc.render.resolution_x = sc.render.resolution_y = R; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.75, 0.72, 0.68); sh.show_cavity = False
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w'); w.use_nodes = False; w.color = (0.55, 0.55, 0.57); sc.world = w
    sc.render.filepath = out_png; bpy.ops.render.render(write_still=True)
    json.dump(dict(cx=cx, cz=cz, S=S, R=R), open(out_json, 'w'))
