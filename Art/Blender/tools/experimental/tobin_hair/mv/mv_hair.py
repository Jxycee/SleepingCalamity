# Registered Hunyuan head+hair (mv_register.py hy_reg.glb) -> game hair: Tobin_Hair (outer mass, baked colour) + Tobin_HairShell (scalp layer).
#   blender -b --factory-startup -P mv_hair.py -- <hy_reg.glb> <cut_dir> <out_dir> [voxel=0.0035] [smooth=4] [clear=0.0025] [tris=40000] [res=700]
# 1. voxel remesh + smooth: drops Hunyuan's fine noodle relief, keeps the big FF/KH lock shapes.
# 2. hair / skin label per vertex from the four cutout skin masks (mv_cutouts.py), projected orthographically in the shared frame, only from views
#    that see the vertex (BVH ray toward the camera) and weighted by facing; unseen vertices (crown, undersides) are hair unless they sit in the
#    face / ear zone within 6 mm of the TobinBase skin.
#    Skin faces, the bust cut and the collar are deleted; then every hair vertex is pushed out to >= clear above AvatarHead (no clipping).
# 3. Tobin_HairShell = AvatarHead faces under the hair, offset 1.2 mm (dark roots; closes the underside); Tobin_Hair decimated to `tris`,
#    smart-UV'd and its colour baked from the same four projections (Cycles EMIT bake, 2048) -> tex/Hair_Cards_*.png.
# 4. 'free' group (how far a vertex hangs off the skull) for rig_export.py; saves <out>/Tobin_shell_hair.blend + prev_*.png (sheet.sh layout).
import bpy, bmesh, sys, os, json, math, numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
argv = sys.argv[sys.argv.index('--') + 1:]
REG, CUT, OUT = argv[0], argv[1], argv[2]; kw = dict(a.split('=', 1) for a in argv[3:] if '=' in a)
F = lambda k, d: float(kw.get(k, d)); TEX = os.path.join(OUT, 'tex'); os.makedirs(TEX, exist_ok=True)
fr = json.load(open(os.path.join(CUT, 'frame.json'))); K, ZC, ZCUT = fr['px_per_m'], fr['zc'], fr['zcut']
bpy.ops.wm.read_factory_settings(use_empty=True)
HERE = os.path.dirname(os.path.abspath(__file__))
bpy.ops.import_scene.fbx(filepath=os.path.normpath(os.path.join(HERE, '../../../../../../Assets/_Project/Content/Characters/TobinBase/TobinBase.fbx')))
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith('Avatar'):
        for m in o.data.materials:
            if m and m.use_nodes:
                for n in [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and not (n.image and n.image.has_data)]: m.node_tree.nodes.remove(n)
                b = m.node_tree.nodes.get('Principled BSDF')
                if b: b.inputs['Base Color'].default_value = (0.80, 0.56, 0.44, 1) if 'Eye' not in o.name else (0.9, 0.9, 0.9, 1)
avatar = [o for o in bpy.data.objects if o.type == 'MESH']
dg = bpy.context.evaluated_depsgraph_get()
head = bpy.data.objects['AvatarHead']; bmh = bmesh.new(); bmh.from_object(head, dg); bmh.transform(head.matrix_world); bmh.normal_update()
TREE = BVHTree.FromBMesh(bmh)
before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=REG)
hy = [o for o in bpy.data.objects if o not in before and o.type == 'MESH'][0]; bpy.context.view_layer.update()
hy.data.transform(hy.matrix_world); hy.matrix_world = Matrix.Identity(4); hy.parent = None
for o in [o for o in bpy.data.objects if o not in before and o != hy]: bpy.data.objects.remove(o)
bpy.context.view_layer.objects.active = hy
def apply(o, typ, **p):
    md = o.modifiers.new('m', typ)
    for k_, v in p.items(): setattr(md, k_, v)
    bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=md.name)
apply(hy, 'REMESH', mode='VOXEL', voxel_size=F('voxel', 0.0035))
apply(hy, 'SMOOTH', factor=0.5, iterations=int(kw.get('smooth', 4)))
# ---- 2. labels ----
VIEWS = dict(front=((0, -1, 0), (1, 0, 0)), left=((1, 0, 0), (0, 1, 0)), right=((-1, 0, 0), (0, -1, 0)), back=((0, 1, 0), (-1, 0, 0)))  # (to camera, image right)
def load(p):
    im = bpy.data.images.load(p); a = np.array(im.pixels[:], np.float32).reshape(im.size[1], im.size[0], im.channels); return a, im
SK = {v: load(os.path.join(CUT, f'skin_{v}.png'))[0][..., 0] for v in VIEWS}; NPX = SK['front'].shape[0]
bm = bmesh.new(); bm.from_mesh(hy.data); bm.verts.ensure_lookup_table(); bm.normal_update(); SELF = BVHTree.FromBMesh(bm)
P = np.array([v.co[:] for v in bm.verts]); NR = np.array([v.normal[:] for v in bm.verts])
num = np.zeros(len(P)); den = np.zeros(len(P))
for v, (d, hr) in VIEWS.items():
    d = np.array(d, float); w = np.clip(NR @ d, 0, 1) ** 2
    col = np.clip((NPX / 2 + K * (P @ np.array(hr))).astype(int), 0, NPX - 1); row = np.clip((NPX / 2 - K * (P[:, 2] - ZC)).astype(int), 0, NPX - 1)
    s = SK[v][NPX - 1 - row, col]
    for i in np.where(w > 0.05)[0]:
        o = Vector(P[i] + NR[i] * 0.0006 + d * 0.0006)
        if SELF.ray_cast(o, Vector(d), 1.0)[0] is not None: w[i] = 0
    num += w * s; den += w
sd = np.zeros(len(P))
for i, p in enumerate(P):
    loc, n, _, _ = TREE.find_nearest(Vector(p)); sd[i] = (Vector(p) - loc).dot(n)
face_zone = ((P[:, 1] < -0.04) & (P[:, 2] < 1.77)) | ((np.abs(P[:, 0]) > 0.06) & (P[:, 2] < 1.745) & (P[:, 1] < 0.03))   # face / ears / jaw
skin = np.where(den > 0.02, num / np.maximum(den, 1e-6), (face_zone & (sd < 0.006)).astype(float))   # unseen (crown, undersides): hair unless face zone
drop = (skin > 0.5) | (P[:, 2] < ZCUT + 0.012) | ((P[:, 2] < 1.63) & (P[:, 1] < 0.02))
print('LABEL verts %d skin %d seen %d' % (len(P), int((skin > 0.5).sum()), int((den > 0.02).sum())))
for zb in (1.62, 1.68, 1.74, 1.80, 1.86):
    m = (P[:, 2] >= zb) & (P[:, 2] < zb + 0.06); print('  z %.2f verts %d skin %d unseen %d sd<0 %d' % (zb, m.sum(), (m & (skin > 0.5)).sum(), (m & (den <= 0.02)).sum(), (m & (sd < 0)).sum()))
bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(drop[v.index] for v in f.verts)], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.faces.ensure_lookup_table(); seen = set(); isl = []
for f in bm.faces:
    if f in seen: continue
    st = [f]; comp = []; seen.add(f)
    while st:
        g = st.pop(); comp.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h not in seen: seen.add(h); st.append(h)
    isl.append(comp)
tot = len(bm.faces); bmesh.ops.delete(bm, geom=[f for c in isl if len(c) < 0.02 * tot for f in c], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
CL = F('clear', 0.0025)
def push():
    for v in bm.verts:
        loc, n, _, _ = TREE.find_nearest(v.co); s_ = (v.co - loc).dot(n)
        if s_ < CL: v.co = loc + n * CL
for it in range(3):
    push(); bmesh.ops.smooth_vert(bm, verts=[v for v in bm.verts if v.is_boundary], factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
push()
me = bpy.data.meshes.new('Tobin_Hair'); bm.to_mesh(me); hair = bpy.data.objects.new('Tobin_Hair', me); bpy.context.collection.objects.link(hair)
bpy.data.objects.remove(hy)
apply(hair, 'DECIMATE', ratio=min(1.0, F('tris', 40000) / max(1, sum(len(p.vertices) - 2 for p in me.polygons))))
# ---- 3. scalp layer ----
th = BVHTree.FromObject(hair, bpy.context.evaluated_depsgraph_get())
bs = bmh.copy(); bs.faces.ensure_lookup_table(); bs.normal_update()
under = set()
for f in bs.faces:
    c = f.calc_center_median(); n = f.normal
    if th.ray_cast(c + n * 0.0005, n, 0.08)[0] is not None: under.add(f.index)
bmesh.ops.delete(bs, geom=[f for f in bs.faces if f.index not in under], context='FACES')
bmesh.ops.delete(bs, geom=[v for v in bs.verts if not v.link_faces], context='VERTS'); bs.normal_update()
for v in bs.verts: v.co += v.normal * 0.0012
ms = bpy.data.meshes.new('Tobin_HairShell'); bs.to_mesh(ms); shell = bpy.data.objects.new('Tobin_HairShell', ms); bpy.context.collection.objects.link(shell)
# ---- checks ----
def checks():
    dg2 = bpy.context.evaluated_depsgraph_get(); b2 = bmesh.new(); b2.from_object(hair, dg2)
    mn = min((v.co - TREE.find_nearest(v.co)[0]).dot(TREE.find_nearest(v.co)[1]) for v in b2.verts)
    t2 = BVHTree.FromBMesh(b2); C = Vector((0, -0.045, 1.735)); hit = tot_r = 0
    for el in np.linspace(5, 85, 9):
        for az in np.linspace(-180, 180, 36, endpoint=False):
            if -50 < az < 50 and el < 30: continue
            d = Vector((math.cos(math.radians(el)) * math.sin(math.radians(az)), -math.cos(math.radians(el)) * math.cos(math.radians(az)), math.sin(math.radians(el))))
            tot_r += 1; hit += t2.ray_cast(C + d * 0.4, -d, 0.4)[0] is not None
    print('CLEARANCE min %.4f m  COVER %d/%d  TRIS hair %d shell %d' % (mn, hit, tot_r, sum(len(p.vertices) - 2 for p in hair.data.polygons), sum(len(p.vertices) - 2 for p in shell.data.polygons)))
checks()
# ---- free weights ----
g = hair.vertex_groups.new(name='free')
for v in hair.data.vertices:
    loc, n, _, _ = TREE.find_nearest(v.co); s_ = (v.co - loc).dot(n)
    g.add([v.index], float(np.clip((s_ - 0.008) / 0.03, 0, 1) * np.clip((1.82 - v.co.z) / 0.06, 0, 1)), 'REPLACE')
# ---- UV + bake ----
for o in (hair, shell):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
def proj_mat(name, img):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; N = nt.nodes; L = nt.links
    geo = N.new('ShaderNodeNewGeometry'); acc = None; wsum = None
    for v, (d, hr) in VIEWS.items():
        h = N.new('ShaderNodeVectorMath'); h.operation = 'DOT_PRODUCT'; L.new(geo.outputs['Position'], h.inputs[0]); h.inputs[1].default_value = hr
        sep = N.new('ShaderNodeSeparateXYZ'); L.new(geo.outputs['Position'], sep.inputs[0])
        u = N.new('ShaderNodeMath'); u.operation = 'MULTIPLY_ADD'; L.new(h.outputs['Value'], u.inputs[0]); u.inputs[1].default_value = K / NPX; u.inputs[2].default_value = 0.5
        vv = N.new('ShaderNodeMath'); vv.operation = 'MULTIPLY_ADD'; L.new(sep.outputs['Z'], vv.inputs[0]); vv.inputs[1].default_value = K / NPX; vv.inputs[2].default_value = 0.5 - ZC * K / NPX
        cmb = N.new('ShaderNodeCombineXYZ'); L.new(u.outputs[0], cmb.inputs[0]); L.new(vv.outputs[0], cmb.inputs[1])
        tx = N.new('ShaderNodeTexImage'); tx.image = bpy.data.images.load(os.path.join(CUT, f'hair_{v}.png')); tx.extension = 'EXTEND'; L.new(cmb.outputs[0], tx.inputs[0])
        fdot = N.new('ShaderNodeVectorMath'); fdot.operation = 'DOT_PRODUCT'; L.new(geo.outputs['Normal'], fdot.inputs[0]); fdot.inputs[1].default_value = d
        w = N.new('ShaderNodeMath'); w.operation = 'MAXIMUM'; L.new(fdot.outputs['Value'], w.inputs[0]); w.inputs[1].default_value = 0.0
        w3 = N.new('ShaderNodeMath'); w3.operation = 'POWER'; L.new(w.outputs[0], w3.inputs[0]); w3.inputs[1].default_value = 3.0
        sc = N.new('ShaderNodeVectorMath'); sc.operation = 'SCALE'; L.new(tx.outputs['Color'], sc.inputs[0]); L.new(w3.outputs[0], sc.inputs['Scale'])
        if acc is None: acc, wsum = sc.outputs[0], w3.outputs[0]
        else:
            a2 = N.new('ShaderNodeVectorMath'); a2.operation = 'ADD'; L.new(acc, a2.inputs[0]); L.new(sc.outputs[0], a2.inputs[1]); acc = a2.outputs[0]
            s2 = N.new('ShaderNodeMath'); s2.operation = 'ADD'; L.new(wsum, s2.inputs[0]); L.new(w3.outputs[0], s2.inputs[1]); wsum = s2.outputs[0]
    inv = N.new('ShaderNodeMath'); inv.operation = 'DIVIDE'; inv.inputs[0].default_value = 1.0; L.new(wsum, inv.inputs[1])
    fin = N.new('ShaderNodeVectorMath'); fin.operation = 'SCALE'; L.new(acc, fin.inputs[0]); L.new(inv.outputs[0], fin.inputs['Scale'])
    em = N.new('ShaderNodeEmission'); L.new(fin.outputs[0], em.inputs['Color'])
    out = N['Material Output']; L.new(em.outputs[0], out.inputs['Surface'])
    t = N.new('ShaderNodeTexImage'); t.image = img; N.active = t
    return m
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 1; sc.cycles.device = 'CPU'
RES = int(kw.get('texres', 2048))
bake = bpy.data.images.new('HairBake', RES, RES, alpha=False); bake.colorspace_settings.name = 'sRGB'
hair.data.materials.clear(); hair.data.materials.append(proj_mat('bake_proj', bake))
bpy.ops.object.select_all(action='DESELECT'); hair.select_set(True); bpy.context.view_layer.objects.active = hair
sc.render.bake.margin = 16; bpy.ops.object.bake(type='EMIT')
def save(img, name): img.filepath_raw = os.path.join(TEX, name); img.file_format = 'PNG'; img.save()
save(bake, 'Hair_Cards_BaseColor.png')
def flat(name, rgba, srgb=True):
    im = bpy.data.images.new(name, 256, 256, alpha=True); im.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    im.pixels[:] = list(rgba) * (256 * 256); save(im, name + '.png'); return im
flat('Hair_Cards_Normal', (0.5, 0.5, 1, 1), False); flat('Hair_Cards_MetallicSmooth', (0, 0, 0, 0.32), False)
flat('Hair_Shell_BaseColor', (0.026, 0.016, 0.010, 1)); flat('Hair_Shell_Normal', (0.5, 0.5, 1, 1), False); flat('Hair_Shell_MetallicSmooth', (0, 0, 0, 0.2), False)
# ---- final materials (names TobinHairMaterialBuilder maps) + previews ----
def lit(name, img=None, col=None):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']; b.inputs['Roughness'].default_value = 0.55
    if img is not None:
        t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = img; m.node_tree.links.new(t.outputs['Color'], b.inputs['Base Color'])
    else: b.inputs['Base Color'].default_value = col
    return m
hair.data.materials.clear(); hair.data.materials.append(lit('Tobin_Hair', bake))
shell.data.materials.clear(); shell.data.materials.append(lit('Tobin_HairShell', col=(0.026, 0.016, 0.010, 1)))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'Tobin_shell_hair.blend'))
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = int(kw.get('res', 700))
sc.world = bpy.data.worlds.new('w'); sc.world.use_nodes = True; sc.world.node_tree.nodes['Background'].inputs[0].default_value = (0.35, 0.35, 0.37, 1)
for nm, rot, en in (('key', (50, 0, -35), 3.0), ('fill', (60, 0, 140), 1.2)):
    l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); l.data.energy = en; l.rotation_euler = [math.radians(a) for a in rot]; sc.collection.objects.link(l)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.40
for v, az in dict(front=0, q34=-40, side=90, back=180).items():
    a = math.radians(az); tgt = Vector((0, -0.03, 1.745)); d = Vector((math.sin(a), -math.cos(a), 0.05)).normalized()
    cam.location = tgt + d * 1.0; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, f'prev_{v}.png'); bpy.ops.render.render(write_still=True)
print('HAIR_DONE', OUT)
