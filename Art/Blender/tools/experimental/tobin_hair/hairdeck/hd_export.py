# hd_fit.blend (HairDeck cards fitted to TobinBase) -> Tobin_shell_hair.blend for ../shell/rig_export.py (Blender 5.x headless).
#   blender -b <fit_dir>/hd_fit.blend -P hd_export.py -- <out_dir> [res=700]
# 1. HairDeck writes every card as unconnected flat-shaded segments: merge by distance -> one island per card.
# 2. UVs for SC_HairCards.shader: uv0 = (u across 0..1, v root 0 .. tip 1; the root end is checked per card, the v end nearer the skin),
#    uv1 = (per-strand random 0..1, the strand's own root..tip fraction for the strand texture). HairDeck's colours along each card stay in the colour attribute (FBX vertex colours).
# 3. Cards stay single sheets: the shader is two-sided.
# 4. Tobin_HairShell = AvatarHead faces under the hair, offset 1.2 mm (no skin between cards). 'free' = how far along its card a vertex
#    lies (rig_export.py hangs that part on spring chains). Root end checked per card (the v end nearer the skin).
import bpy, bmesh, sys, os, math, random, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index('--') + 1:]; OUT = a[0]; kw = dict(x.split('=', 1) for x in a[1:] if '=' in x); os.makedirs(OUT, exist_ok=True)
rnd = random.Random(7)
hair = bpy.data.objects['Tobin_Hair']; head = bpy.data.objects['AvatarHead']
dg = bpy.context.evaluated_depsgraph_get(); bmh = bmesh.new(); bmh.from_object(head, dg); bmh.transform(head.matrix_world); bmh.normal_update()
TREE = BVHTree.FromBMesh(bmh)
bm = bmesh.new(); bm.from_mesh(hair.data)
uvl = bm.loops.layers.uv.active; uv1 = bm.loops.layers.uv.new('card')
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
# islands
fid = {}; cards = []
for f in bm.faces:
    if f in fid: continue
    st = [f]; fid[f] = len(cards); comp = [f]
    while st:
        g = st.pop()
        for e in g.edges:
            for h in e.link_faces:
                if h not in fid: fid[h] = len(cards); st.append(h); comp.append(h)
    cards.append(comp)
print('CARDS', len(cards))
vfree = {}; flipped = 0
for comp in cards:
    loops = [l for f in comp for l in f.loops]
    vs = np.array([l[uvl].uv[1] for l in loops]); P = np.array([l.vert.co[:] for l in loops])
    r0 = P[vs <= vs.min() + 0.08].mean(0); r1 = P[vs >= vs.max() - 0.08].mean(0)
    d0 = (Vector(r0) - TREE.find_nearest(Vector(r0))[0]).length; d1 = (Vector(r1) - TREE.find_nearest(Vector(r1))[0]).length
    flip = d1 < d0; flipped += flip
    L = max(0.02, float(np.linalg.norm(r1 - r0)) * 1.15)
    ou, ov = rnd.random(), rnd.random()
    tmax = max(1e-6, float((1 - vs.min()) if flip else vs.max()))          # this strand's own length as a card fraction
    for l in loops:
        u, v = l[uvl].uv; t = (1 - v) if flip else v
        l[uvl].uv = (u, t); l[uv1].uv = (ou, min(1.0, t / tmax))            # uv1.y: root..tip of this strand (strand texture)
        vfree[l.vert.index] = t
print('ROOT flipped', flipped)
bm.verts.ensure_lookup_table(); bm.normal_update()
orig = np.array([bm.verts[i].co[:] for i in sorted(vfree)]); tv = np.array([vfree[i] for i in sorted(vfree)])
bm.to_mesh(hair.data); hair.data.update()
# 'free' from position along the card (t of the nearest vertex)
from mathutils.kdtree import KDTree
kd = KDTree(len(orig))
for i, p in enumerate(orig): kd.insert(p, i)
kd.balance()
g = hair.vertex_groups.get('free') or hair.vertex_groups.new(name='free')
for v in hair.data.vertices:
    _, i, _ = kd.find(v.co); t = tv[i]
    g.add([v.index], float(np.clip((t - 0.18) / 0.62, 0, 1)), 'REPLACE')
for p in hair.data.polygons: p.use_smooth = True   # smooth along and across each card (HairDeck exports flat-shaded segments)
# scalp shell
th = BVHTree.FromObject(hair, bpy.context.evaluated_depsgraph_get())
bs = bmh.copy(); bs.faces.ensure_lookup_table(); bs.normal_update()
NAPE_Z = float(kw.get('nape', 0))   # nape=1.62: extend the scalp layer down the back of the head (default 0 = old edge)
NAPE_RING = int(kw.get('nape_ring', 1))   # drop this many face rings along the extended lower edge (cards cover it; no straight edge or triangles show)
def scalp(f):   # under hair within 2.5 cm, and not the face below the hairline (the fringe hangs 3-5 cm in front of the forehead)
    c = f.calc_center_median()
    if c.y < -0.06 and c.z < 1.79: return False
    if NAPE_Z and c.y > 0.02:               # back (nape=<z>): hairline at nape_z at the back centre, rising behind the ears (1.5 m per m of |x| past 3 cm), never above the old 1.71 limit
        if c.z < min(1.71, NAPE_Z + 1.5 * max(0.0, abs(c.x) - 0.03)): return False
    else:
        if c.z < 1.67: return False            # nape: strand ends would show the scalp layer as dark stripes
        if c.z < 1.71 and (abs(c.x) > 0.04 or c.y > 0.02): return False   # back / sides: its edge showed as dark shards through gaps when the head turns
    return th.ray_cast(c + f.normal * 0.0005, f.normal, 0.025)[0] is not None
keep = {f.index for f in bs.faces if scalp(f)}
for _ in range(NAPE_RING if NAPE_Z else 0):   # faces touching a vertex that has a non-kept neighbour face = the lower edge ring (back only)
    edge = {f.index for f in bs.faces if f.index in keep and f.calc_center_median().y > 0.02 and any(g.index not in keep for v in f.verts for g in v.link_faces)}
    keep -= {i for i in edge if bs.faces[i].calc_center_median().z < 1.70}
bmesh.ops.delete(bs, geom=[f for f in bs.faces if f.index not in keep], context='FACES')
bmesh.ops.delete(bs, geom=[v for v in bs.verts if not v.link_faces], context='VERTS'); bs.normal_update()
for v in bs.verts: v.co += v.normal * 0.0012
ms = bpy.data.meshes.new('Tobin_HairShell'); bs.to_mesh(ms)
shell = bpy.data.objects.new('Tobin_HairShell', ms); bpy.context.scene.collection.objects.link(shell)
bpy.context.view_layer.objects.active = shell; shell.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(angle_limit=math.radians(66)); bpy.ops.object.mode_set(mode='OBJECT')
# materials named for TobinHairMaterialBuilder (FBX material *HairCards* -> SC_HairCards, *HairCap* -> flat root colour)
def mat(name, col):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = col; return m
hair.data.materials.clear(); hair.data.materials.append(mat('Tobin_HairCards', (0.30, 0.20, 0.13, 1)))
shell.data.materials.clear(); shell.data.materials.append(mat('Tobin_HairCap', (0.09, 0.055, 0.035, 1)))
print('COLOURS', [c.name for c in hair.data.color_attributes], 'UV', [u.name for u in hair.data.uv_layers])
print('TRIS hair %d shell %d' % (sum(len(p.vertices) - 2 for p in hair.data.polygons), sum(len(p.vertices) - 2 for p in shell.data.polygons)))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'Tobin_shell_hair.blend'))
print('EXPORT_PREP_DONE', OUT)
