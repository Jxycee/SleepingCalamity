# blender -b <out>/Tobin_shell_hair.blend -P rig_export.py -- <out_fbx> [K=40] [bones=4]
# Rigs the shell + lock-card hair for TobinHairSpringChains and exports TobinHairMesh.fbx (same frame / settings as ../export_fbx2.py):
#   HairArmature = Head (copied from the MetaPerson Head bone) + K chains hair_<k>_0..N-1 (bone 0 non-deforming), Tobin_HairShell 100 % on Head,
#   Tobin_Hair (cards) on Head except where its 'free' group says it hangs past the shell (written by build_shell_hair.py), which follows the chains.
import bpy, sys, json, os, numpy as np
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]; OUT = a[0]; kw = dict(x.split('=') for x in a[1:]); K = int(kw.get('K', 40)); NB = int(kw.get('bones', 4))
mp = bpy.data.objects['AvatarRoot']; hb = mp.data.bones['Head']
HH = mp.matrix_world @ hb.head_local; HT = mp.matrix_world @ hb.tail_local
cards = bpy.data.objects.get('Tobin_HairClumps') or bpy.data.objects['Tobin_Hair']; shell = bpy.data.objects['Tobin_HairShell']
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0)); arm = bpy.context.active_object; arm.name = 'HairArmature'; arm.data.name = 'HairArmature'
for e in list(arm.data.edit_bones): arm.data.edit_bones.remove(e)
e0 = arm.data.edit_bones.new('Head'); e0.head = HH; e0.tail = HT
W = np.array([v.co[:] for v in cards.data.vertices]); gi = cards.vertex_groups['free'].index
FR = np.array([next((g.weight for g in v.groups if g.group == gi), 0.0) for v in cards.data.vertices])
fm = FR > 0.05; Pm = W[fm]
rng = np.random.default_rng(3); cen = Pm[rng.choice(len(Pm), K, replace=False)].copy()
for _ in range(30):
    lab = np.argmin(((Pm[:, None, :] - cen[None]) ** 2).sum(2), 1)
    for k in range(K):
        if (lab == k).any(): cen[k] = Pm[lab == k].mean(0)
labels = np.full(len(W), -1); labels[np.where(fm)[0]] = lab
chains = []
for k in range(K):
    idx = np.where(labels == k)[0]
    if len(idx) < 12: continue
    f = FR[idx]; root = W[idx][f <= np.percentile(f, 10)].mean(0); far = W[idx][f >= np.percentile(f, 90)].mean(0)
    if np.linalg.norm(far - root) < 0.01: continue
    chains.append(dict(k=k, pts=np.array([root + (far - root) * i / NB for i in range(NB + 1)]), idx=idx))
eb = arm.data.edit_bones
for c in chains:
    prev = eb['Head']
    for i in range(NB):
        b = eb.new(f'hair_{c["k"]}_{i}'); b.head = Vector(c['pts'][i]); b.tail = Vector(c['pts'][i + 1]); b.parent = prev; b.use_connect = False; b.use_deform = i > 0; prev = b
bpy.ops.object.mode_set(mode='OBJECT')
print('CHAINS', len(chains), 'free verts', int(fm.sum()), 'of', len(W))
# weights
for o in (cards, shell):
    for g in list(o.vertex_groups):
        if g.name != 'free': o.vertex_groups.remove(g)
hs = shell.vertex_groups.new(name='Head'); hs.add(list(range(len(shell.data.vertices))), 1.0, 'REPLACE')
hc = cards.vertex_groups.new(name='Head')
vg = {c['k']: [cards.vertex_groups.new(name=f'hair_{c["k"]}_{i}') for i in range(NB)] for c in chains}
headw = np.ones(len(W))
for c in chains:
    P = c['pts']; ax = P[-1] - P[0]; L = np.linalg.norm(ax); ax /= L
    for vi in c['idx']:
        sj = float(np.clip((W[vi] - P[0]) @ ax / L, 0, 1)) * NB; lo = int(min(np.floor(sj), NB - 1)); fr = sj - lo; f = FR[vi]
        # bone 0 never deforms: weight goes to bones 1..NB-1 (bone lo>=1), lower part blends with Head
        wl = (1 - fr) * f; wh = fr * f
        if lo >= 1: vg[c['k']][lo].add([int(vi)], wl, 'ADD')
        else: wl = 0.0
        if lo + 1 < NB: vg[c['k']][lo + 1].add([int(vi)], wh, 'ADD')
        else: vg[c['k']][lo].add([int(vi)], wh, 'ADD') if lo >= 1 else None
        headw[vi] = 1.0 - (wl + wh)
for vi in range(len(W)): hc.add([vi], float(max(headw[vi], 0.0)), 'REPLACE')
cards.vertex_groups.remove(cards.vertex_groups['free'])
for o in (cards, shell):
    for m in list(o.modifiers): o.modifiers.remove(m)
    o.parent = arm; md = o.modifiers.new('Armature', 'ARMATURE'); md.object = arm
arm.data.pose_position = 'REST'
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); cards.select_set(True); shell.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, path_mode='STRIP', embed_textures=False, apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False,
                         mesh_smooth_type='OFF', axis_forward='-Z', axis_up='Y', bake_anim=False, use_armature_deform_only=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(OUT), 'Tobin_shell_hair_rigged.blend'))
json.dump([dict(k=c['k'], bones=[f'hair_{c["k"]}_{i}' for i in range(NB)], pts=c['pts'].tolist()) for c in chains], open(os.path.join(os.path.dirname(OUT), 'chains.json'), 'w'))
print('EXPORT_DONE bones', len(arm.data.bones))
