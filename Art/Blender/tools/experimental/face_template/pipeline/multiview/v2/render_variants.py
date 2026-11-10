"""Blender (background) Workbench renders of template-topology basis meshes on a pipeline run's head object, same camera for every variant.
usage: blender -b <run>/out/<name>.blend -P render_variants.py -- <template.npz> <out_dir> label=basis.npy [label=basis.npy ...]
Writes <label>_tex_f / _clay_q / _clay_s / _ear_L / _ear_R / _mouth .png (textured front, clay 3/4, clay side, ear closeups, mouth closeup)."""
import bpy, numpy as np, math, sys, os
a = sys.argv[sys.argv.index('--') + 1:]; tpl, out = a[0], a[1]; var = dict(x.split('=', 1) for x in a[2:]); os.makedirs(out, exist_ok=True)
z = np.load(tpl, allow_pickle=True); T = z['head/basis']; earc = {s: T[z['tpl/ear_' + s].astype(int)].mean(0) for s in 'LR'}; mouth = T[z['tpl/lip_line'].astype(int)].mean(0)
sc = bpy.context.scene
for o in bpy.data.objects: o.hide_render = True; o.hide_viewport = True
o = bpy.data.objects['AvatarHead_tpl_full']; o.hide_render = False; o.hide_viewport = False
sk = o.data.shape_keys
for m in (sk.key_blocks if sk else []): m.value = 0
cam = bpy.data.cameras.new('c'); cam.type = 'ORTHO'; co = bpy.data.objects.new('c', cam); sc.collection.objects.link(co); sc.camera = co
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.render.resolution_x = 520; sc.render.resolution_y = 520
def shot(name, scale, target, az, el=0.0):
    cam.ortho_scale = scale; r = 1.0; azr = math.radians(az); co.location = (target[0] + r * math.sin(azr), target[1] - r * math.cos(azr), target[2]); co.rotation_euler = (math.pi / 2, 0, azr)
    sc.render.filepath = os.path.join(out, name + '.png'); bpy.ops.render.render(write_still=True)
for lab, p in var.items():
    P = np.load(p)
    for i, v in enumerate(o.data.vertices): v.co = P[i]
    if sk:
        for i, kd in enumerate(sk.key_blocks['Basis'].data): kd.co = P[i]
    o.data.update()
    sc.display.shading.color_type = 'TEXTURE'; shot(lab + '_tex_f', 0.30, (0, 0, 1.67), 0); shot(lab + '_mouth', 0.15, (0, 0, 1.665), 0)
    sc.display.shading.color_type = 'SINGLE'; sc.display.shading.single_color = (0.75, 0.75, 0.75)
    shot(lab + '_clay_q', 0.30, (0, 0, 1.67), 35); shot(lab + '_clay_s', 0.30, (0, 0, 1.67), 90)
    shot(lab + '_ear_L', 0.12, (earc['L'][0], earc['L'][1], earc['L'][2]), 90); shot(lab + '_ear_R', 0.12, (earc['R'][0], earc['R'][1], earc['R'][2]), -90)
print('DONE')
