# Blender bg: bake target.glb diffuse colour (no lighting) onto Tobin's head UV (wrap_r3 Basis), 2048 px.
# Outputs bake/raw_color.png (RGBA) and bake/raw_hit.png (white = ray hit).  usage: blender -b --factory-startup --python face2_01_bake.py
import bpy, numpy as np, json
from mathutils import Matrix
D = 'C:/AI/test_out/heroes/face_spike/'
R_ = 2048
z = np.load(D + 'template/tobin_head_template.npz', allow_pickle=True)
faces = z['head/faces']; uv = z['head/loop_uv']; assert (z['head/loop_vertex'] == faces.ravel()).all()
basis = np.load(D + 'fit/wrap_r3_basis.npy')
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=D + 'target/target.glb')
tg = [o for o in bpy.data.objects if o.type == 'MESH']
al = json.load(open(D + 'fit/align.json'))
M = np.eye(4); M[:3, :3] = al['scale'] * np.array(al['R']); M[:3, 3] = al['t']
for o in bpy.data.objects:
    if o.parent is None: o.matrix_world = Matrix(M.tolist()) @ o.matrix_world
bpy.context.view_layer.update()
me = bpy.data.meshes.new('head'); me.from_pydata(basis.tolist(), [], faces.tolist()); me.update()
me.uv_layers.new(name='UVMap'); me.uv_layers[0].data.foreach_set('uv', uv.ravel())
head = bpy.data.objects.new('AvatarHead', me); sc.collection.objects.link(head)
for p in me.polygons: p.use_smooth = True
img = bpy.data.images.new('bake', R_, R_, alpha=True, float_buffer=False)
mat = bpy.data.materials.new('hm'); mat.use_nodes = True
n = mat.node_tree.nodes; tn = n.new('ShaderNodeTexImage'); tn.image = img; n.active = tn
head.data.materials.append(mat)
sc.render.engine = 'CYCLES'; sc.cycles.samples = 16
try:
    pref = bpy.context.preferences.addons['cycles'].preferences
    pref.compute_device_type = 'OPTIX'; pref.get_devices()
    for d in pref.devices: d.use = d.type != 'CPU'
    sc.cycles.device = 'GPU'
except Exception as e: print('gpu fail', e)
def bake(btype, path, margin):
    img.generated_color = (0, 0, 0, 0)
    bpy.ops.object.select_all(action='DESELECT')
    for o in tg: o.select_set(True)
    head.select_set(True); bpy.context.view_layer.objects.active = head
    kw = dict(type=btype, use_selected_to_active=True, cage_extrusion=0.003, max_ray_distance=0.01, margin=margin, margin_type='EXTEND', use_clear=True)
    if btype == 'DIFFUSE': kw.update(pass_filter={'COLOR'})
    bpy.ops.object.bake(**kw)
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()
# target metallic (from its MR texture) would darken Diffuse colour: cut the metallic link, force 0
for m in bpy.data.materials:
    if m.node_tree is None: continue
    for n in m.node_tree.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            for l in list(n.inputs['Metallic'].links): m.node_tree.links.remove(l)
            n.inputs['Metallic'].default_value = 0.0
bake('DIFFUSE', D + 'bake/raw_color.png', 0)
# hit mask: emission white on target
for o in tg:
    for s in o.material_slots:
        m = s.material; m.use_nodes = True; nt = m.node_tree
        for x in list(nt.nodes):
            if x.type != 'OUTPUT_MATERIAL': nt.nodes.remove(x)
        out = [x for x in nt.nodes if x.type == 'OUTPUT_MATERIAL'][0]
        e = nt.nodes.new('ShaderNodeEmission'); e.inputs['Color'].default_value = (1, 1, 1, 1); nt.links.new(e.outputs[0], out.inputs['Surface'])
bake('EMIT', D + 'bake/raw_hit.png', 0)
print('BAKE DONE')
