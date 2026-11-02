# sc_stall_lods.py: adds LODs to a combined Wave 1 stall FBX (output of sc_market_stall.py --combine) and re-exports it.
# Run:  blender -b --factory-startup --python Art/Blender/town/sc_stall_lods.py -- <in.fbx> <out.fbx> [--r1 0.5] [--r2 0.2]
# Input: root Stall_<V> with mesh children Ruined and Rebuilt (one baked atlas each, one material).
# Output: each state becomes an empty node holding <State>_LOD0 (the untouched baked mesh), <State>_LOD1 (~50%) and <State>_LOD2 (~20%); Unity's FBX
# importer builds a LODGroup on the node from the _LODn suffixes. LOD0 keeps every vertex, UV and normal, so the baked maps stay valid. The lower levels
# are Decimate (collapse) copies with the same material and UVs; planar dissolve first removes flat faces for free (posts, counter, plank faces), so the
# collapse spends its budget on the dense goods, rope and cloth folds rather than on the silhouette edges.
# Same axes, scale, STRIP paths and smoothing as the generator's export, so pivot and size match the old file.
import bpy, sys, os

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
src, dst = argv[0], argv[1]
def opt(k, d): return float(argv[argv.index(k) + 1]) if k in argv else d
R1, R2 = opt('--r1', 0.5), opt('--r2', 0.2)

bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
states = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.parent is None]   # the combine export drops the root empty

def lod(ob, ratio, name):
    c = ob.copy(); c.data = ob.data.copy(); c.name = name; c.data.name = name
    bpy.context.scene.collection.objects.link(c)
    bpy.context.view_layer.objects.active = c
    pl = c.modifiers.new('planar', 'DECIMATE'); pl.decimate_type = 'DISSOLVE'; pl.angle_limit = 0.0087   # 0.5 deg: only truly flat faces
    bpy.ops.object.modifier_apply(modifier=pl.name)
    tris = sum(len(p.vertices) - 2 for p in c.data.polygons)
    full = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    cl = c.modifiers.new('collapse', 'DECIMATE'); cl.decimate_type = 'COLLAPSE'; cl.ratio = max(0.01, min(1.0, ratio * full / max(tris, 1)))
    cl.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=cl.name)
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
