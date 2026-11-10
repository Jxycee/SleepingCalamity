# Hunyuan3D-2mv head+hair mesh -> TobinBase head frame -> hair-only mesh (Blender 5.x, headless).
#   blender -b --factory-startup -P mv_register.py -- <hy.glb> <cut_dir> <out_dir> [clear=0.0025] [icp=1] [views=front,q34,side,back]
# 1. orientation: glTF import gives z-up; the nose is found as the most-forward point at eye height and turned to -y.
# 2. scale / offset: mesh bottom = the cutout chin cut (zcut), mesh top = the hair top seen in the front cutout, centred in x;
#    y offset puts the mesh's face where TobinBase's face is (ICP on face points below the brow, scale + translation only).
# 3. hair = faces whose centre lies more than `clear` outside AvatarHead (signed distance via BVH nearest + normal), largest pieces.
# Writes hy_reg.glb (whole mesh, head frame), hair_raw.glb (hair pieces), prev_*.png and a printed report.
import bpy, bmesh, sys, os, json, math, numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
argv = sys.argv[sys.argv.index('--') + 1:]
GLB, CUT, OUT = argv[0], argv[1], argv[2]; kw = dict(a.split('=', 1) for a in argv[3:] if '=' in a); os.makedirs(OUT, exist_ok=True)
F = lambda k, d: float(kw.get(k, d))
bpy.ops.wm.read_factory_settings(use_empty=True)
HERE = os.path.dirname(os.path.abspath(__file__))
HEAD_SRC = os.path.normpath(os.path.join(HERE, '../../../../../../Assets/_Project/Content/Characters/TobinBase/TobinBase.fbx'))
bpy.ops.import_scene.fbx(filepath=HEAD_SRC)
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith('Avatar'):
        for m in o.data.materials:
            if m and m.use_nodes:
                for n in [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and not (n.image and n.image.has_data)]: m.node_tree.nodes.remove(n)
                b = m.node_tree.nodes.get('Principled BSDF')
                if b: b.inputs['Base Color'].default_value = (0.80, 0.56, 0.44, 1) if 'Eye' not in o.name else (0.9, 0.9, 0.9, 1)
avatar = [o for o in bpy.data.objects if o.type == 'MESH']
dg = bpy.context.evaluated_depsgraph_get()
def world_bm(o):
    bm = bmesh.new(); bm.from_object(o, dg); bm.transform(o.matrix_world); bm.faces.ensure_lookup_table(); bm.normal_update(); return bm
bmh = world_bm(bpy.data.objects['AvatarHead']); TREE = BVHTree.FromBMesh(bmh)
HV = np.array([v.co[:] for v in bmh.verts])

# ---- Hunyuan mesh ----
before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=GLB)
hy = [o for o in bpy.data.objects if o not in before and o.type == 'MESH'][0]
bpy.context.view_layer.update(); hy.data.transform(hy.matrix_world); hy.matrix_world = Matrix.Identity(4); hy.name = 'HY'
if len(hy.data.polygons) > 400000:   # Hunyuan octree 384 gives ~1.5 M faces; 300 k keeps every lock
    md = hy.modifiers.new('dec', 'DECIMATE'); md.ratio = 300000 / len(hy.data.polygons); bpy.context.view_layer.objects.active = hy; bpy.ops.object.modifier_apply(modifier='dec')
V = np.array([v.co[:] for v in hy.data.vertices])
# cut/frame facts from the cutouts
cal = json.load(open(os.path.join(CUT, 'frame.json')))
a_front = None
try:
    import cv2  # noqa  (not in Blender python) -> fall back to frame.json
except Exception: pass
ZCUT, ZTOP = cal['zcut'], cal['ztop_front']
# orientation: try the 4 yaw turns; each is placed (scale from chin cut to hair top, centred, forehead matched) and ICP-fitted on the face;
# the turn with the lowest face rms wins (the back of the head never fits a face).
def front_y(P, z, tol=0.006): m = np.abs(P[:, 2] - z) < tol; m &= np.abs(P[:, 0]) < 0.02; return P[m, 1].min() if m.any() else P[:, 1].min()
def place(V0, k):
    W = V0 @ np.array(Matrix.Rotation(k * math.pi / 2, 3, 'Z')).T
    s = (ZTOP - ZCUT) / np.ptp(W[:, 2]); W = W * s
    W[:, 0] -= 0.5 * (W[:, 0].min() + W[:, 0].max()); W[:, 2] += ZCUT - W[:, 2].min()
    W[:, 1] += front_y(HV, 1.66) - front_y(W, 1.66)
    rms = 1.0
    for it in range(int(kw.get('icp_it', 25))):
        m = (W[:, 2] < 1.745) & (W[:, 2] > 1.61) & (W[:, 1] < -0.06)
        if m.sum() < 50: break
        P = W[m][::max(1, int(m.sum() // 4000))]; Q = np.array([TREE.find_nearest(Vector(p))[0][:] for p in P])
        d = np.linalg.norm(P - Q, axis=1); kp = d < np.percentile(d, 80); P, Q = P[kp], Q[kp]
        pc, qc = P.mean(0), Q.mean(0); sc = np.clip(np.sqrt(((Q - qc) ** 2).sum() / ((P - pc) ** 2).sum()), 0.97, 1.03)
        W = (W - pc) * sc + qc; rms = float(np.sqrt((d[kp] ** 2).mean()))
    return W, rms, s
V0 = V; YAWS = [int(kw["yaw"]) // 90] if "yaw" in kw else [0]   # Hunyuan glTF + Blender import: face at -y (yaw 0); face-ICP rms cannot tell turns apart
res = {k: place(V0, k) for k in YAWS}
for k, r in res.items(): print('ORIENT yaw %d face rms %.4f scale %.4f' % (k * 90, r[1], r[2]))
kb = min(res, key=lambda k: res[k][1]); V = res[kb][0]; print('ORIENT pick', kb * 90)
for i, v in enumerate(hy.data.vertices): v.co = V[i]
hy.data.update()
# ---- signed distance per face, hair pieces ----
bm = bmesh.new(); bm.from_mesh(hy.data); bm.faces.ensure_lookup_table(); bm.normal_update()
CL = F('clear', 0.0025); out_f = []
for f in bm.faces:
    c = f.calc_center_median(); loc, n, idx, dist = TREE.find_nearest(c)
    sd = (c - loc).dot(n) if loc else 1.0
    if sd > CL: out_f.append(f.index)
keep = set(out_f)
bmh2 = bm.copy(); bmh2.faces.ensure_lookup_table()
bmesh.ops.delete(bmh2, geom=[f for f in bmh2.faces if f.index not in keep], context='FACES')
# drop small islands (< 3 % of the kept faces)
bmh2.faces.ensure_lookup_table(); seen = set(); isl = []
for f in bmh2.faces:
    if f in seen: continue
    st = [f]; comp = []; seen.add(f)
    while st:
        g = st.pop(); comp.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h not in seen: seen.add(h); st.append(h)
    isl.append(comp)
tot = len(bmh2.faces); small = [f for c in isl if len(c) < 0.03 * tot for f in c]
bmesh.ops.delete(bmh2, geom=small, context='FACES')
me = bpy.data.meshes.new('HairRaw'); bmh2.to_mesh(me); hair = bpy.data.objects.new('HairRaw', me); bpy.context.collection.objects.link(hair)
print('HAIR faces', len(me.polygons), 'of', len(bm.faces), 'islands kept', sum(1 for c in isl if len(c) >= 0.03 * tot))
# coverage: rays from the skull centre over the upper head that hit hair before skin
C = Vector((0, -0.045, 1.735)); th = BVHTree.FromBMesh(bmh2); hit = tot_r = 0
for el in np.linspace(5, 85, 9):
    for az in np.linspace(-180, 180, 36, endpoint=False):
        d = Vector((math.cos(math.radians(el)) * math.sin(math.radians(az)), -math.cos(math.radians(el)) * math.cos(math.radians(az)), math.sin(math.radians(el))))
        if az > -50 and az < 50 and el < 30: continue        # face
        tot_r += 1; r = th.ray_cast(C + d * 0.4, -d, 0.4)
        if r[0] is not None: hit += 1
print('COVER %d/%d' % (hit, tot_r))
# ---- materials and previews ----
def mat(name, col, rough=0.6):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = col; b.inputs['Roughness'].default_value = rough; return m
hair.data.materials.append(mat('hair', (0.30, 0.20, 0.13, 1), 0.5)); hy.data.materials.clear(); hy.data.materials.append(mat('clay', (0.6, 0.6, 0.62, 1)))
bpy.ops.object.select_all(action='DESELECT'); hy.select_set(True); bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, 'hy_reg.glb'), use_selection=True)
bpy.ops.object.select_all(action='DESELECT'); hair.select_set(True); bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, 'hair_raw.glb'), use_selection=True)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = int(kw.get('res', 700))
sc.world = bpy.data.worlds.new('w'); sc.world.use_nodes = True; sc.world.node_tree.nodes['Background'].inputs[0].default_value = (0.35, 0.35, 0.37, 1)
for nm, rot, en in (('key', (50, 0, -35), 3.0), ('fill', (60, 0, 140), 1.2)):
    l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); l.data.energy = en; l.rotation_euler = [math.radians(a) for a in rot]; sc.collection.objects.link(l)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.40
VIEWS = dict(front=0, q34=-40, side=-90, back=180, q34b=140)
def shoot(tag, show):
    for o in bpy.data.objects:
        if o.type == 'MESH': o.hide_render = o not in show
    for v in kw.get('views', 'front,q34,side,back').split(','):
        a = math.radians(VIEWS[v]); tgt = Vector((0, -0.03, 1.745)); d = Vector((math.sin(a), -math.cos(a), 0.05)).normalized()
        cam.location = tgt + d * 1.0; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, f'prev_{tag}_{v}.png'); bpy.ops.render.render(write_still=True)
shoot('hy', [hy]); shoot('hair', avatar + [hair])
print('REG_DONE', OUT)

