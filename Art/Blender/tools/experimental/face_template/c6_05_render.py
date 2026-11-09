"""Blender bg: render proof cells + teeth/lash checks from out/face2.blend.
usage: blender -b --factory-startup --python face2_04_render.py [-- only_checks]   -> out/cells/<view>_<row>_<col>.png, out/checks.json"""
import bpy, numpy as np, json, math, os, sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
D = 'C:/AI/test_out/heroes/face_spike/'; D2 = 'C:/AI/test_out/heroes/face_spike2/'; B2 = D2 + 'bake6/'; O2 = D2 + 'out6/'
CELLS = O2 + 'cells/'; os.makedirs(CELLS, exist_ok=True)
only_checks = 'only_checks' in sys.argv
TPL6 = D2 + 'fit6/tobin_head_template6.npz'
z = np.load(TPL6, allow_pickle=True)
bpy.ops.wm.open_mainfile(filepath=O2 + 'cloud6.blend')
sc = bpy.context.scene
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; arm.data.pose_position = 'REST'
VARS = ('tpl', 'slider', 'full')
parts = {v: {} for v in VARS}
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    for v in VARS:
        if o.name.endswith('_' + v):
            for k in ('Head', 'Eyelashes', 'LeftEyeball', 'RightEyeball', 'TeethLower', 'TeethUpper'):
                if o.name.startswith('Avatar' + k + '_') and o.name.endswith('_' + v) and (v != 'tpl' or o.name == 'Avatar' + k + '_tpl'): parts[v][k] = o
body = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('AvatarBody')]
for o in body: o.hide_render = True
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith('Avatar') and not any(o.name.endswith('_' + v) for v in VARS): o.hide_render = True
def show(v):
    for vv in VARS:
        for o in parts[vv].values(): o.hide_render = vv != v; o.hide_viewport = vv != v
def setkeys(v, vals):
    for o in parts[v].values():
        for kb in o.data.shape_keys.key_blocks:
            if kb.name != 'Basis': kb.value = vals.get(kb.name, 0.0)
EXPR = {
    'neutral': {}, 'blink': {'eyeBlinkLeft': 1.0, 'eyeBlinkRight': 1.0}, 'jawOpen': {'jawOpen': 0.5},
    'aa': {'aa': 1.0}, 'oh': {'oh': 1.0}, 'smile': {'mouthSmileLeft': 1.0, 'mouthSmileRight': 1.0}}

# ---------------- checks
def bvh_of(o):
    dg = bpy.context.evaluated_depsgraph_get(); e = o.evaluated_get(dg); m = e.to_mesh()
    vs = [v.co.copy() for v in m.vertices]; ps = [tuple(p.vertices) for p in m.polygons]
    pts = np.array([tuple(v) for v in vs]); e.to_mesh_clear()
    return BVHTree.FromPolygons(vs, ps), ps, pts
def evalpts(o):
    dg = bpy.context.evaluated_depsgraph_get(); e = o.evaluated_get(dg); m = e.to_mesh()
    a = np.empty(len(m.vertices) * 3, np.float32); m.vertices.foreach_get('co', a); e.to_mesh_clear(); return a.reshape(-1, 3)
interior = np.zeros(len(z['head/basis']), bool); interior[z['tpl/mouth_interior']] = True
# ---- round 3: generalized winding number of the teeth vertices w.r.t. the closed mouth shell (mouth_interior + lip patches, capped by a fan on the lip-line loop)
_rep = z['tpl/weld_rep'].astype(int); _hf = z['head/faces'].astype(int)
_vs = np.zeros(len(z['head/basis']), bool)
for _k in ('mouth_interior', 'lip_patch_upper_verts', 'lip_patch_lower_verts'): _vs[z['tpl/' + _k]] = True
SHELL = _hf[_vs[_hf].all(1)]
_W = _rep[SHELL]; _dir = {}
for _a, _b, _c in _W:
    for _u, _v in ((_a, _b), (_b, _c), (_c, _a)): _dir[(_u, _v)] = _dir.get((_u, _v), 0) + 1
_bnd = [(_u, _v) for (_u, _v) in _dir if (_v, _u) not in _dir]
_loopv = sorted({x for e in _bnd for x in e})
# map welded id -> a representative real vertex index
_w2v = {}
for _i in range(len(_rep)): _w2v.setdefault(_rep[_i], _i)
print('SHELL faces', len(SHELL), 'boundary edges', len(_bnd), 'boundary verts', len(_loopv))
def wn_outside(P, Q):
    """P: head vertex coords (N,3) in the head mesh order; Q: query points. Returns winding numbers."""
    tri = [tuple(f) for f in SHELL]
    cen = P[[_w2v[v] for v in _loopv]].mean(0)
    A = P[SHELL[:, 0]]; B = P[SHELL[:, 1]]; C = P[SHELL[:, 2]]
    capA = np.array([P[_w2v[v]] for u, v in _bnd for v in (v,)]); capB = np.array([P[_w2v[u]] for u, v in _bnd]); capC = np.repeat(cen[None], len(_bnd), 0)
    A = np.concatenate([A, capA]); B = np.concatenate([B, capB]); C = np.concatenate([C, capC])   # cap triangle (v,u,centroid) closes the directed boundary
    out = np.zeros(len(Q))
    for i0 in range(0, len(Q), 400):
        q = Q[i0:i0 + 400][:, None, :]
        a = A[None] - q; b = B[None] - q; c = C[None] - q
        la, lb, lc = np.linalg.norm(a, axis=2), np.linalg.norm(b, axis=2), np.linalg.norm(c, axis=2)
        num = (a * np.cross(b, c)).sum(2); den = la * lb * lc + (a * b).sum(2) * lc + (b * c).sum(2) * la + (c * a).sum(2) * lb
        out[i0:i0 + 400] = (2 * np.arctan2(num, den)).sum(1) / (4 * np.pi)
    return out
def signed(tree, ps, pts, q, outer_only):
    """per query: (signed distance along face normal, nearest face is outer-skin)"""
    res = []
    for p in q:
        loc, nor, idx, dist = tree.find_nearest(Vector(p))
        if loc is None: res.append((0.0, False)); continue
        d = float((Vector(p) - loc).dot(nor)); res.append((d, (not interior[list(ps[idx])].all()) and nor.y < 0.3))   # outer skin = not mouth-interior and not a lip-back/cavity face (normal facing +y)
    return res
def fwd_counts(tree, q):
    out = []
    for p in q:
        o = Vector(p); n = 0
        for _ in range(8):
            loc, nor, idx, dist = tree.ray_cast(o, Vector((0, -1, 0)))
            if loc is None: break
            n += 1; o = loc + Vector((0, -1e-5, 0))
        out.append(n)
    return np.array(out)
checks = {}
for v in VARS:
    show(v); H = parts[v]['Head']
    for state, vals in (('neutral', {}), ('jawOpen0.5', {'jawOpen': 0.5}), ('blink1', {'eyeBlinkLeft': 1.0, 'eyeBlinkRight': 1.0})):
        setkeys(v, vals); bpy.context.view_layer.update()
        tree, ps, hp = bvh_of(H); c = {}
        if state in ('neutral', 'jawOpen0.5'):
            for tk in ('TeethUpper', 'TeethLower'):
                q = evalpts(parts[v][tk]); nf = fwd_counts(tree, q); r_ = signed(tree, ps, hp, q, True)
                dd = np.array([x for x, _ in r_]) * 1e3; ou = np.array([o_ for _, o_ in r_])
                _wn = wn_outside(hp, q); c[tk + '_wn'] = dict(n=len(q), outside=int((np.abs(_wn) < 0.5).sum()), inside=int((np.abs(_wn) >= 0.5).sum()), wn_min=float(_wn.min()), wn_max=float(_wn.max()))
                c[tk] = dict(fwd_hits=nf.tolist(), n=len(q), outside_skin_gt0=int((ou & (dd > 0)).sum()), outside_skin_gt0p3mm=int((ou & (dd > 0.3)).sum()),
                             outside_skin_max_mm=float(dd[ou].max()) if ou.any() else 0.0, nearest_is_outer=int(ou.sum()))
        q = evalpts(parts[v]['Eyelashes']); r = signed(tree, ps, hp, q, True)
        d = np.array([x for x, _ in r]) * 1e3
        c['lashes'] = dict(n=len(d), embedded_lt_m0p5mm=int((d < -0.5).sum()), embedded_lt_m1mm=int((d < -1.0).sum()), min_signed_mm=float(d.min()), p05_mm=float(np.percentile(d, 5)), median_mm=float(np.median(d)))
        checks[f'{v}/{state}'] = c
for v in ('slider', 'full'):
    for st in ('neutral', 'jawOpen0.5'):
        for tk in ('TeethUpper', 'TeethLower'):
            a = np.array(checks[f'tpl/{st}'][tk]['fwd_hits']); b_ = np.array(checks[f'{v}/{st}'][tk]['fwd_hits']); sd = {k_: checks[f'{v}/{st}'][tk][k_] for k_ in ('outside_skin_gt0', 'outside_skin_gt0p3mm', 'outside_skin_max_mm', 'nearest_is_outer')}
            checks[f'{v}/{st}'][tk] = dict(**sd, n=len(a), tpl_verts_with_0_hits=int((a == 0).sum()), var_verts_with_0_hits=int((b_ == 0).sum()), verts_losing_cover_vs_tpl=int((b_ < a).sum()), verts_gaining_cover=int((b_ > a).sum()))
for st in ('neutral', 'jawOpen0.5'):
    for tk in ('TeethUpper', 'TeethLower'):
        a = np.array(checks[f'tpl/{st}'][tk]['fwd_hits']); sd = {k_: checks[f'tpl/{st}'][tk][k_] for k_ in ('outside_skin_gt0', 'outside_skin_gt0p3mm', 'outside_skin_max_mm', 'nearest_is_outer')}; checks[f'tpl/{st}'][tk] = dict(**sd, n=len(a), verts_with_0_hits=int((a == 0).sum()))
json.dump(checks, open(O2 + 'checks.json', 'w'), indent=1)
print('CHECKS', json.dumps(checks))
if only_checks: sys.exit(0)

# ---------------- rendering
scn = sc; scn.render.engine = 'CYCLES'; scn.cycles.samples = 48; scn.cycles.use_denoising = True
try:
    pref = bpy.context.preferences.addons['cycles'].preferences; pref.compute_device_type = 'OPTIX'; pref.get_devices()
    for dv in pref.devices: dv.use = dv.type != 'CPU'
    scn.cycles.device = 'GPU'
except Exception as e: print('gpu fail', e)
scn.view_settings.view_transform = 'Standard'
scn.render.image_settings.file_format = 'PNG'
w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.55, 0.55, 0.57, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.35; scn.world = w
def light(name, loc, energy, size):
    L = bpy.data.lights.new(name, 'AREA'); L.energy = energy; L.size = size
    o = bpy.data.objects.new(name, L); scn.collection.objects.link(o); o.location = loc
    d = Vector((0, -0.06, 1.69)) - Vector(loc); o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); return o
light('key', (-0.55, -0.8, 2.05), 22, 0.7); light('fill', (0.7, -0.7, 1.7), 7, 0.8); light('rim', (0.0, 0.5, 2.0), 9, 0.5)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scn.collection.objects.link(cam); scn.camera = cam; cam.data.lens = 85
def aim(centre, dist, az_deg, res):
    scn.render.resolution_x, scn.render.resolution_y = res
    a = math.radians(az_deg); c = Vector(centre)
    cam.location = c + Vector((math.sin(a) * dist, -math.cos(a) * dist, 0)); cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
def shoot(path):
    scn.render.filepath = path; bpy.ops.render.render(write_still=True)
VIEWS = {'front': ((0, -0.06, 1.69), 0.72, 0, (640, 640)), 'q34': ((0, -0.06, 1.69), 0.72, 35, (640, 640)),
         'eyes': ((0, -0.10, 1.72), 0.38, 0, (720, 400)), 'mouth': ((0, -0.12, 1.645), 0.42, 0, (720, 400))}
jobs = [('front', r) for r in EXPR] + [('q34', 'neutral'), ('eyes', 'neutral'), ('eyes', 'blink')] + [('mouth', r) for r in ('neutral', 'jawOpen', 'aa', 'oh', 'smile')]
bpy.context.view_layer.update()
for view, row in jobs:
    aim(*VIEWS[view])
    for v in VARS:
        show(v); setkeys(v, EXPR[row]); bpy.context.view_layer.update()
        shoot(f'{CELLS}{view}_{row}_{v}.png')
# ---- overlay: full result at the photo framing (affine fit render px -> photo px; ortho camera)
cj = json.load(open(B2 + 'wrap_cam.json'))
# round 3: eye-aligned similarity (4 eye corners) wrap-render px -> photo px
_rl = json.load(open(B2 + 'wrap_lm.json')); _ph = np.array(json.load(open(D2 + 'target/landmarks.json'))['landmarks_px_xyz'])[:, :2]
_EC = [33, 263, 133, 362]; _a = np.array([_rl[str(i)] for i in _EC]); _b = _ph[_EC]; _ma, _mb = _a.mean(0), _b.mean(0)
_U, _S, _Vt = np.linalg.svd((_b - _mb).T @ (_a - _ma)); _d = np.sign(np.linalg.det(_U @ _Vt)); _Rm = _U @ np.diag([1, _d]) @ _Vt
_s = (_S * np.array([1, _d])).sum() / ((_a - _ma) ** 2).sum(); _t = _mb - _s * _Rm @ _ma
M = np.vstack([(_s * _Rm).T, _t[None]])
A = M[:2].T; sc_ = float(np.sqrt(abs(np.linalg.det(A))))          # photo px per render px
px_per_m = sc_ * cj['R'] / cj['S']
rp = np.linalg.solve(A.T, np.array([335.0, 355.0]) - M[2])        # render px that lands on the photo centre
wx = cj['cx'] + (rp[0] / cj['R'] - 0.5) * cj['S']; wz = cj['cz'] + (0.5 - rp[1] / cj['R']) * cj['S']
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 710 / px_per_m; cam.location = (wx, -2.0, wz); cam.rotation_euler = (math.pi / 2, 0, 0)
scn.render.resolution_x, scn.render.resolution_y = 670, 710; scn.render.film_transparent = True; scn.render.image_settings.color_mode = 'RGBA'
show('full'); setkeys('full', {}); bpy.context.view_layer.update(); shoot(f'{CELLS}overlay_full.png')
json.dump(dict(ortho_scale=cam.data.ortho_scale, centre=[wx, wz], photo_px_per_render_px=sc_), open(O2 + 'overlay_cam.json', 'w'))
print('RENDER DONE')
