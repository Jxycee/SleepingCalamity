"""Blender bg: build face2.blend = template (tpl), slider-only (slider) and full wrap (full) variants of head/lashes/eyes/teeth on the Tobin armature.
Lashes are bound to the OLD head with Surface Deform and re-evaluated on the new Basis; lash keys get the same delta.
Asserts the shared-topology invariants. usage: blender -b --factory-startup --python face2_03b_build.py"""
import bpy, bmesh, numpy as np, json, sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
D = 'C:/AI/test_out/heroes/face_spike/'; D2 = 'C:/AI/test_out/heroes/face_spike2/'; B2 = D2 + 'bake/'; O2 = D2 + 'out/'
GLB = 'C:/Dev/SleepingCalamity/.claude/worktrees/procgen-next-73e90d/Art/Source/MetaPerson/Tobin_Base_v2_sliders.glb'
TEX = 'C:/Dev/SleepingCalamity/.claude/worktrees/procgen-next-73e90d/Assets/_Project/Content/Characters/TobinBase/Textures/'
z = np.load(D + 'template/tobin_head_template.npz', allow_pickle=True)
hv = np.load(B2 + 'head_variants.npz'); pp = np.load(B2 + 'parts.npz')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
arm.data.pose_position = 'REST'
bpy.context.view_layer.update()
short = {'AvatarHead': 'head', 'AvatarEyelashes': 'lashes', 'AvatarLeftEyeball': 'eyeL', 'AvatarRightEyeball': 'eyeR', 'AvatarTeethLower': 'teethLower', 'AvatarTeethUpper': 'teethUpper'}
orig = {}
for o in bpy.data.objects:
    if o.type == 'MESH':
        for k, v in short.items():
            if o.name == k or o.name.startswith(k): orig[v] = o
assert set(orig) == set(short.values()), orig.keys()

def setco(me, arr):
    arr = np.ascontiguousarray(arr, np.float32).reshape(-1)
    me.vertices.foreach_set('co', arr); me.update()
def set_keys(o, basis, keys):
    me = o.data; kb = me.shape_keys.key_blocks
    assert len(kb) == len(keys), (o.name, len(kb), len(keys))
    for i, k in enumerate(kb): k.data.foreach_set('co', np.ascontiguousarray(keys[i], np.float32).reshape(-1))
    setco(me, basis)
def getco(coll, n):
    a = np.empty(n * 3, np.float32); coll.foreach_get('co', a); return a
def dup(o, suffix):
    n = o.copy(); n.data = o.data.copy(); n.name = o.name + suffix; n.data.name = o.data.name + suffix
    for c in o.users_collection: c.objects.link(n)
    return n
# keep imported objects as the template (tpl)
for k, o in orig.items(): o.name = o.name + '_tpl'; o.data.name = o.data.name + '_tpl'
var = {'tpl': dict(orig)}
for v in ('slider', 'full'): var[v] = {k: dup(o, '_' + v) for k, o in orig.items()}
# ---- head
for v in ('slider', 'full'): set_keys(var[v]['head'], hv[v + '_basis'], hv[v + '_keys'])
# ---- eyes / teeth
for v in ('slider', 'full'):
    for k in ('eyeL', 'eyeR', 'teethLower', 'teethUpper'): set_keys(var[v][k], pp[f'{v}/{k}/basis'], pp[f'{v}/{k}/keys'])
# ---- lashes: Surface Deform bound to the OLD head
old_b = z['head/basis']; faces = z['head/faces']; lb = z['lashes/basis']; lk = z['lashes/keys']
assert np.abs(lk[0] - lb).max() == 0
def eval_lashes(new_b):
    me = bpy.data.meshes.new('oldhead'); me.from_pydata(old_b.tolist(), [], faces.tolist()); me.update()
    H = bpy.data.objects.new('_oldhead', me); bpy.context.scene.collection.objects.link(H)
    lm = bpy.data.meshes.new('l'); lm.from_pydata(lb.tolist(), [], z['lashes/faces'].tolist()); lm.update()
    L = bpy.data.objects.new('_lash', lm); bpy.context.scene.collection.objects.link(L)
    md = L.modifiers.new('sd', 'SURFACE_DEFORM'); md.target = H; md.falloff = 4.0
    bpy.context.view_layer.update()
    with bpy.context.temp_override(object=L, active_object=L, selected_objects=[L]):
        r = bpy.ops.object.surfacedeform_bind(modifier='sd')
    assert md.is_bound, 'surface deform bind failed'
    # sanity: unchanged target -> unchanged lashes
    dg = bpy.context.evaluated_depsgraph_get(); e = L.evaluated_get(dg).to_mesh(); c0 = np.empty(len(e.vertices) * 3, np.float32); e.vertices.foreach_get('co', c0)
    drift0 = float(np.abs(c0.reshape(-1, 3) - lb).max() * 1e3)
    L.evaluated_get(dg).to_mesh_clear()
    setco(H.data, new_b); bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get(); e = L.evaluated_get(dg).to_mesh(); c = np.empty(len(e.vertices) * 3, np.float32); e.vertices.foreach_get('co', c)
    L.evaluated_get(dg).to_mesh_clear()
    bpy.data.objects.remove(L); bpy.data.objects.remove(H)
    return c.reshape(-1, 3), drift0
report = {}
for v in ('slider', 'full'):
    nb, drift = eval_lashes(hv[v + '_basis'])
    delta = nb - lb
    nk = lk + delta[None]; nk[0] = nb
    set_keys(var[v]['lashes'], nb, nk)
    report[f'lash_{v}'] = dict(bind_rest_drift_mm=drift, delta_mean_mm=float(np.linalg.norm(delta, axis=1).mean() * 1e3), delta_max_mm=float(np.linalg.norm(delta, axis=1).max() * 1e3))
# ---- invariants
inv = []
def uvs(o):
    me = o.data; a = np.empty(len(me.loops) * 2, np.float32); me.uv_layers[0].data.foreach_get('uv', a); return a.reshape(-1, 2)
def lv(o):
    me = o.data; a = np.empty(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', a); return a
nk_exp = dict(head=150, lashes=68, teethLower=21, teethUpper=2, eyeL=4, eyeR=4)
EXP = dict(head=(7321, 14212), lashes=(6300, 4900), eyeL=(512, 791), eyeR=(512, 791), teethLower=(2511, 3464), teethUpper=(2497, 3654))
ok = True
for v in ('slider', 'full'):
    for k, o in var[v].items():
        me = o.data; kb = me.shape_keys.key_blocks
        names = [x.name for x in kb]
        c = dict(variant=v, part=k, verts=len(me.vertices), faces=len(me.polygons), nkeys=len(kb))
        c['counts_expected'] = (len(me.vertices), len(me.polygons)) == EXP[k]
        c['verts_eq'] = len(me.vertices) == z[k + '/basis'].shape[0]
        c['faces_eq'] = len(me.polygons) == z[k + '/faces'].shape[0]
        c['uv_equal'] = bool(np.array_equal(uvs(o), z[k + '/loop_uv'])) and np.array_equal(uvs(o), uvs(var['tpl'][k]))
        c['loop_vertex_equal'] = bool(np.array_equal(lv(o), z[k + '/loop_vertex']))
        c['keys_expected'] = len(kb) == nk_exp[k]
        c['key_names_equal'] = names == [str(x) for x in z[k + '/key_names']]
        co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', co)
        allk = np.stack([getco(k.data, len(me.vertices)) for k in kb])
        c['no_nan'] = bool(np.isfinite(co).all() and np.isfinite(allk).all())
        c['basis_key_eq_mesh'] = bool(np.array_equal(allk[0], co))
        c['all_ok'] = all(vv for kk, vv in c.items() if isinstance(vv, (bool, np.bool_)))
        ok &= c['all_ok']; inv.append(c)
        assert c['all_ok'], c
        print('INVARIANT', json.dumps(c, default=bool))
report['invariants_all_ok'] = bool(ok)
# ---- materials: simple principled (albedo + Tobin normal)
def skin_mat(name, albedo):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes['Principled BSDF']
    t1 = nt.nodes.new('ShaderNodeTexImage'); t1.image = bpy.data.images.load(albedo); nt.links.new(t1.outputs['Color'], b.inputs['Base Color'])
    t2 = nt.nodes.new('ShaderNodeTexImage'); t2.image = bpy.data.images.load(TEX + 'Head_Normal.png'); t2.image.colorspace_settings.name = 'Non-Color'
    nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(t2.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    b.inputs['Roughness'].default_value = 0.5
    for nme in ('Subsurface Weight',):
        if nme in b.inputs: b.inputs[nme].default_value = 0.04
    return m
mf = skin_mat('Head_cloud', B2 + 'Head_BaseColor_cloud.png')
mt = skin_mat('Head_tobin', TEX + 'Head_BaseColor.png')
for v, m in (('slider', mf), ('full', mf), ('tpl', mt)):
    h = var[v]['head']; h.data.materials.clear(); h.data.materials.append(m)
# irises: cyan albedo + mild emission (emission texture = iris disc only)
def eye_mat(src):
    m = src.copy(); m.name = 'Eye_cloud'; nt = m.node_tree; b = [n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'][0]
    tex = [n for n in nt.nodes if n.type == 'TEX_IMAGE' and n.outputs['Color'].links and any(l.to_socket == b.inputs['Base Color'] for l in n.outputs['Color'].links)]
    assert tex, 'eye base colour texture node not found'
    tex[0].image = bpy.data.images.load(B2 + 'Eye_BaseColor_cloud.png')
    e = nt.nodes.new('ShaderNodeTexImage'); e.image = bpy.data.images.load(B2 + 'Eye_Emission_cloud.png')
    for l in list(tex[0].inputs['Vector'].links): nt.links.new(l.from_socket, e.inputs['Vector'])
    nt.links.new(e.outputs['Color'], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 0.6
    return m
em_ = None
for v in ('slider', 'full'):
    for k in ('eyeL', 'eyeR'):
        o = var[v][k]; src = o.data.materials[0]
        if em_ is None: em_ = eye_mat(src)
        o.data.materials[0] = em_
# collections / visibility
for v in ('tpl', 'slider', 'full'):
    col = bpy.data.collections.new('variant_' + v); bpy.context.scene.collection.children.link(col)
    for o in var[v].values():
        for c in list(o.users_collection): c.objects.unlink(o)
        col.objects.link(o)
bpy.data.collections['variant_tpl'].hide_render = False
json.dump(report, open(O2 + 'build_report.json', 'w'), indent=1, default=float)
bpy.ops.wm.save_as_mainfile(filepath=O2 + 'cloud.blend')
print('BUILD DONE', json.dumps(report, default=float))
