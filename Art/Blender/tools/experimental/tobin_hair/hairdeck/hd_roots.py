# Find HairDeck cards whose roots show: a strand that starts in mid-air or on bare skin (Blender 5.x headless, on hd_fit.blend).
#   blender -b <fit_dir>/hd_fit.blend -P hd_roots.py -- <strands.npz> [out.json] [reach=0.04] [float=0.012]
# For every ribbon (hd_strands.py 'sid', source card 'cid'), the root (lowest uv v) is tested:
#   bare   : a ray from the root outward (away from the skull centre) meets no other card's hair within `reach`
#   floating: the root sits more than `float` above the AvatarHead skin
# A card is reported when at least half of its ribbons have a bare or floating root. Writes {"bare": [...], "floating": [...]}.
import bpy, bmesh, sys, json, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index('--') + 1:]; NPZ = a[0]; OUT = a[1] if len(a) > 1 and '=' not in a[1] else None
kw = dict(x.split('=', 1) for x in a[1:] if '=' in x); REACH = float(kw.get('reach', 0.04)); FLOAT = float(kw.get('float', 0.012))
d = np.load(NPZ); sid, cid, uvv = d['sid'], d['cid'], d['uv'][:, 1]
dg = bpy.context.evaluated_depsgraph_get()
hair = bpy.data.objects['Tobin_Hair']; me = hair.data
V = np.array([v.co[:] for v in me.vertices]); assert len(V) == len(sid), (len(V), len(sid))
head = bpy.data.objects['AvatarHead']; bh = bmesh.new(); bh.from_object(head, dg); bh.transform(head.matrix_world); bh.normal_update(); TH = BVHTree.FromBMesh(bh)
C = Vector((0, -0.02, 1.745))
# one BVH per card would be slow; instead one BVH of all hair, and reject hits on the root's own card via the face's card id
polys = [list(p.vertices) for p in me.polygons]; TREE = BVHTree.FromPolygons([v.co for v in me.vertices], polys)
pcard = np.array([cid[p[0]] for p in polys])
bare, flo, tot = {}, {}, {}
for k in np.unique(sid):
    m = np.where(sid == k)[0]; r = m[uvv[m] <= uvv[m].min() + 1e-6]; P = Vector(V[r].mean(0)); c = int(cid[m[0]])
    tot[c] = tot.get(c, 0) + 1
    loc, n, _, _ = TH.find_nearest(P); sd = (P - loc).dot(n)
    if sd > FLOAT: flo[c] = flo.get(c, 0) + 1
    dvec = (P - C).normalized(); o = P + dvec * 0.0008; covered = False; dist = 0.0
    for _ in range(12):                      # walk along the ray, skipping hits on the same card
        hit = TREE.ray_cast(o, dvec, REACH - dist)
        if hit[0] is None: break
        dist += (hit[0] - o).length + 0.0005; o = hit[0] + dvec * 0.0005
        if pcard[hit[2]] != c: covered = True; break
        if dist >= REACH: break
    if not covered: bare[c] = bare.get(c, 0) + 1
rep = dict(bare=sorted(c for c in bare if bare[c] * 2 >= tot[c]), floating=sorted(c for c in flo if flo[c] * 2 >= tot[c]))
print('ROOTS ribbons %d cards %d bare %d floating %d' % (len(np.unique(sid)), len(tot), len(rep['bare']), len(rep['floating'])))
print('BARE', rep['bare']); print('FLOAT', rep['floating'])
if OUT: json.dump(rep, open(OUT, 'w'))
