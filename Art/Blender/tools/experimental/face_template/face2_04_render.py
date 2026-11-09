"""Blender bg: render proof cells + teeth/lash checks from out/face2.blend.
usage: blender -b --factory-startup --python face2_04_render.py [-- only_checks]   -> out/cells/<view>_<row>_<col>.png, out/checks.json"""
import bpy, numpy as np, json, math, os, sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
D = 'C:/AI/test_out/heroes/face_spike/'
CELLS = D + 'out/cells/'; os.makedirs(CELLS, exist_ok=True)
only_checks = 'only_checks' in sys.argv
z = np.load(D + 'template/tobin_head_template.npz', allow_pickle=True)
bpy.ops.wm.open_mainfile(filepath=D + 'out/face2.blend')
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
def signed(tree, ps, pts, q, outer_only):
    """per query: (signed distance along face normal, nearest face is outer-skin)"""
    res = []
    for p in q:
        loc, nor, idx, dist = tree.find_nearest(Vector(p))
        if loc is None: res.append((0.0, False)); continue
        d = float((Vector(p) - loc).dot(nor)); res.append((d, not interior[list(ps[idx])].all()))
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
                q = evalpts(parts[v][tk]); nf = fwd_counts(tree, q); c[tk] = dict(fwd_hits=nf.tolist())
        q = evalpts(parts[v]['Eyelashes']); r = signed(tree, ps, hp, q, True)
        d = np.array([x for x, _ in r]) * 1e3
        c['lashes'] = dict(n=len(d), embedded_lt_m0p5mm=int((d < -0.5).sum()), embedded_lt_m1mm=int((d < -1.0).sum()), min_signed_mm=float(d.min()), p05_mm=float(np.percentile(d, 5)), median_mm=float(np.median(d)))
        checks[f'{v}/{state}'] = c
for v in ('slider', 'full'):
    for st in ('neutral', 'jawOpen0.5'):
        for tk in ('TeethUpper', 'TeethLower'):
            a = np.array(checks[f'tpl/{st}'][tk]['fwd_hits']); b_ = np.array(checks[f'{v}/{st}'][tk]['fwd_hits'])
            checks[f'{v}/{st}'][tk] = dict(n=len(a), tpl_verts_with_0_hits=int((a == 0).sum()), var_verts_with_0_hits=int((b_ == 0).sum()), verts_losing_cover_vs_tpl=int((b_ < a).sum()), verts_gaining_cover=int((b_ > a).sum()))
for st in ('neutral', 'jawOpen0.5'):
    for tk in ('TeethUpper', 'TeethLower'):
        a = np.array(checks[f'tpl/{st}'][tk]['fwd_hits']); checks[f'tpl/{st}'][tk] = dict(n=len(a), verts_with_0_hits=int((a == 0).sum()))
json.dump(checks, open(D + 'out/checks.json', 'w'), indent=1)
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
# target (original texture) in the template frame; neutral rows only
al = json.load(open(D + 'fit/align.json'))
M = np.eye(4); M[:3, :3] = al['scale'] * np.array(al['R']); M[:3, 3] = al['t']
before = set(bpy.data.objects.keys())
bpy.ops.import_scene.gltf(filepath=D + 'target/target.glb')
tg = [bpy.data.objects[n] for n in set(bpy.data.objects.keys()) - before]
for o in tg:
    if o.parent is None: o.matrix_world = Matrix(M.tolist()) @ o.matrix_world
    o.hide_render = True
for m in bpy.data.materials:
    if m.name.startswith('Material_0') and m.node_tree:
        for n in m.node_tree.nodes:
            if n.type == 'BSDF_PRINCIPLED':
                for l in list(n.inputs['Metallic'].links): m.node_tree.links.remove(l)
                n.inputs['Metallic'].default_value = 0.0; n.inputs['Roughness'].default_value = 0.55
bpy.context.view_layer.update()
tobj = [o for o in tg if o.type == 'MESH']
for view, row in jobs:
    aim(*VIEWS[view])
    for v in VARS:
        show(v); setkeys(v, EXPR[row]); bpy.context.view_layer.update()
        shoot(f'{CELLS}{view}_{row}_{v}.png')
    if row == 'neutral':
        for vv in VARS:
            for o in parts[vv].values(): o.hide_render = True
        for o in tobj: o.hide_render = False
        shoot(f'{CELLS}{view}_{row}_target.png')
        for o in tobj: o.hide_render = True
print('RENDER DONE')
