"""Blender bg: which template regions are front-visible (ray from -y) on tpl / full head in out4/cloud4.blend. Flags interior faces poking through the outer skin."""
import bpy, numpy as np, json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
D = 'C:/AI/test_out/heroes/face_spike/'; O4 = 'C:/AI/test_out/heroes/face_spike2/out4/'
z = np.load(D + 'template/tobin_head_template.npz', allow_pickle=True)
bpy.ops.wm.open_mainfile(filepath=O4 + 'cloud4.blend')
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; arm.data.pose_position = 'REST'
faces = z['head/faces'].astype(int)
reg = {}
for k in ('nostril', 'mouth_interior', 'eye_socket'):
    m = np.zeros(len(z['head/basis']), bool); m[z['tpl/' + k]] = True; reg[k] = m[faces].all(1)
pm = np.zeros(len(z['head/basis']), bool); pm[z['tpl/lip_patch_upper_verts']] = True; pm[z['tpl/lip_patch_lower_verts']] = True; reg['lip_patch'] = pm[faces].all(1)
out = {}
for var in ('tpl', 'full'):
    o = [x for x in bpy.data.objects if x.type == 'MESH' and x.name.startswith('AvatarHead') and x.name.endswith('_' + var)][0]
    me = o.data; v = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', v); v = v.reshape(-1, 3)
    tree = BVHTree.FromPolygons([tuple(x) for x in v.tolist()], [tuple(f) for f in faces.tolist()])
    cen = v[faces].mean(1); res = {}
    for k, m in reg.items():
        idx = np.where(m)[0]; vis = []
        for i in idx:
            c = cen[i]; loc, n, fi, d = tree.ray_cast(Vector((c[0], -2.0, c[2])), Vector((0, 1, 0)))
            if fi == i: vis.append(i)
        res[k] = dict(n=len(idx), front_visible=len(vis), pos_mm=[[round(float(x) * 1e3, 1) for x in cen[i]] for i in vis[:12]])
    out[var] = res
print('RES', json.dumps(out))
