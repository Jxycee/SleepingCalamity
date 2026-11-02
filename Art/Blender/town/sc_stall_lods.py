# sc_stall_lods.py: adds LODs to a combined Wave 1 stall FBX (output of sc_market_stall.py --combine) and re-exports it.
# Run:  blender -b --factory-startup --python Art/Blender/town/sc_stall_lods.py -- <in.fbx> <out.fbx> [--r1 0.5] [--r2 0.3] [--floor 0.6] [--thin 0.02]
# Thin closed shells (cloth, awnings, ~5 mm slabs) are decimated separately with a floor ratio (plain collapse punched a hole through the counter skirt); custom normals are
# re-projected from LOD0 with a Data Transfer modifier after the collapse (the collapse scrambled them: dark smears).
# Input: root Stall_<V> with mesh children Ruined and Rebuilt (one baked atlas each, one material).
# Output: each state becomes an empty node holding <State>_LOD0 (the untouched baked mesh), <State>_LOD1 (~50%) and <State>_LOD2 (~20%); Unity's FBX
# importer builds a LODGroup on the node from the _LODn suffixes. LOD0 keeps every vertex, UV and normal, so the baked maps stay valid. The lower levels
# are Decimate (collapse) copies with the same material and UVs; planar dissolve first removes flat faces for free (posts, counter, plank faces), so the
# collapse spends its budget on the dense goods, rope and cloth folds rather than on the silhouette edges.
# Same axes, scale, STRIP paths and smoothing as the generator's export, so pivot and size match the old file.
import bpy, bmesh, sys, os

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
src, dst = argv[0], argv[1]
def opt(k, d): return float(argv[argv.index(k) + 1]) if k in argv else d
R1, R2 = opt('--r1', 0.5), opt('--r2', 0.3)
FLOOR = opt('--floor', 0.6)   # min kept fraction of thin shells (cloth)
THIN = opt('--thin', 0.02) # shell thickness (m) below which a part counts as thin

bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
states = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.parent is None]   # the combine export drops the root empty

def tri_count(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)

def thin_faces(me):
    """Face indices of thin closed shells (cloth, awning: ~5 mm slabs). Plain collapse merges their front and back and punches holes."""
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    seen, thin = set(), set()
    for f in bm.faces:
        if f.index in seen: continue
        seen.add(f.index); st = [f]; fs = [f]
        while st:
            x = st.pop()
            for e in x.edges:
                for g in e.link_faces:
                    if g.index not in seen: seen.add(g.index); st.append(g); fs.append(g)
        area = sum(g.calc_area() for g in fs)
        vol = abs(sum(g.verts[0].co.dot(g.verts[i].co.cross(g.verts[i + 1].co)) / 6.0 for g in fs for i in range(1, len(g.verts) - 1)))
        if area > 0.2 and 2 * vol / area < THIN: thin.update(g.index for g in fs)
    bm.free()
    return thin

def piece(c, faces, keep, name):
    """Copy of c holding only the faces whose membership in `faces` equals `keep`."""
    d = c.copy(); d.data = c.data.copy(); d.name = name; bpy.context.scene.collection.objects.link(d)
    bm = bmesh.new(); bm.from_mesh(d.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if (f.index in faces) != keep], context='FACES')
    bm.to_mesh(d.data); bm.free()
    return d

def collapse(o, ratio):
    bpy.context.view_layer.objects.active = o
    cl = o.modifiers.new('collapse', 'DECIMATE'); cl.decimate_type = 'COLLAPSE'; cl.ratio = max(0.01, min(1.0, ratio))
    cl.use_collapse_triangulate = False; cl.use_symmetry = False
    bpy.ops.object.modifier_apply(modifier=cl.name)

def lod(ob, ratio, name):
    c = ob.copy(); c.data = ob.data.copy(); c.name = name; c.data.name = name
    bpy.context.scene.collection.objects.link(c)
    bpy.context.view_layer.objects.active = c
    pl = c.modifiers.new('planar', 'DECIMATE'); pl.decimate_type = 'DISSOLVE'; pl.angle_limit = 0.0087   # 0.5 deg: only truly flat faces
    bpy.ops.object.modifier_apply(modifier=pl.name)
    full, tris = tri_count(ob), tri_count(c)
    # Thin shells keep at least FLOOR of their tris; the solid parts (posts, planks, goods) absorb the rest of the budget.
    thin = thin_faces(c.data)
    a, b = piece(c, thin, True, name + '_thin'), piece(c, thin, False, name + '_rest')
    ta, tb = tri_count(a), tri_count(b)
    ra = max(ratio, FLOOR); rb = (ratio * full - ra * ta) / max(tb, 1)
    collapse(a, ra); collapse(b, rb)
    bpy.data.objects.remove(c)
    for o in bpy.context.scene.objects: o.select_set(False)
    a.select_set(True); b.select_set(True); bpy.context.view_layer.objects.active = a
    bpy.ops.object.join()
    c = bpy.context.view_layer.objects.active; c.name = name; c.data.name = name
    # Collapse scrambles the imported custom normals (the dark smears): re-project them from the untouched LOD0, face corner to nearest face, interpolated.
    dt = c.modifiers.new('normals', 'DATA_TRANSFER'); dt.object = ob; dt.use_loop_data = True; dt.data_types_loops = {'CUSTOM_NORMAL'}
    dt.loop_mapping = 'POLYINTERP_NEAREST'; dt.mix_mode = 'REPLACE'; dt.mix_factor = 1.0
    bpy.ops.object.modifier_apply(modifier=dt.name)
    return c

for st in states:
    n = st.name
    node = bpy.data.objects.new(n, None); bpy.context.scene.collection.objects.link(node)
    node.matrix_parent_inverse = st.matrix_parent_inverse
    node.matrix_basis = st.matrix_basis.copy()
    kids = [st, lod(st, R1, n + '_LOD1'), lod(st, R2, n + '_LOD2')]
    st.name = n + '_LOD0'; st.data.name = n + '_LOD0'
    for k in kids:
        k.parent = node
        k.matrix_parent_inverse = type(k.matrix_basis)()
        k.matrix_basis = type(k.matrix_basis)()      # node carries the transform; the meshes sit at identity inside it
    for k in kids:
        print('LOD', k.name, sum(len(p.vertices) - 2 for p in k.data.polygons))

for o in bpy.context.scene.objects: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS',
                         bake_space_transform=True, object_types={'MESH', 'EMPTY'}, mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='STRIP')
print('LODS_EXPORTED', dst)
