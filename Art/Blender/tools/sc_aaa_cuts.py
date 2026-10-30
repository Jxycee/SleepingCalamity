"""Authored multi-scale form as geometry (lever 1): bounded boolean cutters instead of noise.
- spall(): a shallow ellipsoid bite at an arris -> conchoidal scar with a crisp rim (5-15 mm, 1.5-3 mm deep)
- chamfer(): a bounded box whose inner face is the cut plane -> a flat chisel facet with crisp arrises
- crack_cutter(): a jagged V-section wedge strip projected onto a dense high -> a hairline crack
- blows(): hammer blows as overlapping shallow spherical caps laid in courses (max depth, sharp ridges)
Cutters are subtracted with the EXACT solver. `fresh` marks verts near cutter surfaces (store as attr 'fr')."""
import math, random
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
import sc_aaa_kit as K


def _orient(z):
    z = Vector(z).normalized(); x = Vector((0, 0, 1)).cross(z)
    if x.length < 1e-4: x = Vector((1, 0, 0))
    x.normalize(); y = z.cross(x)
    return Matrix((x, y, z)).transposed()


def spall(p, n, size=(0.010, 0.007), depth=0.002, tilt=0.0, seed=0):
    """Ellipsoid cutter whose cap reaches `depth` below point p along -n; size = scar half-widths."""
    r = random.Random(seed)
    a, b = size
    c = max(a, b) * 1.6                                      # flat-ish cap: big radius along n
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=1.0)
    R = _orient(n) @ Matrix.Rotation(r.uniform(0, math.pi), 3, 'Z') @ Matrix.Rotation(tilt, 3, 'X')
    S = Matrix.Diagonal((a * 1.15, b * 1.15, c))
    ctr = Vector(p) + Vector(n).normalized() * (c - depth)
    for v in bm.verts: v.co = R @ (S @ v.co) + ctr
    ob = K._obj_from_bm('cut_spall', bm); ob['fresh'] = 1
    return ob


def chamfer(p, n, half=(0.02, 0.02), thick=0.03):
    """Box: its -n face is the cut plane through p (normal n points into the removed side)."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    R = _orient(n); n = Vector(n).normalized()
    for v in bm.verts:
        v.co = R @ Vector((v.co.x * 2 * half[0], v.co.y * 2 * half[1], (v.co.z + 0.5) * thick)) + Vector(p)
    return K._obj_from_bm('cut_chamfer', bm)


def crack_cutter(high, start, direction, length=0.04, step=0.0025, width=0.0012, depth=0.0028, jag=0.6, seed=0):
    """Jagged V strip walking over `high` from `start` (snapped to the surface), heading `direction`,
    narrowing to a point. One closed prism per segment (overlaps are fine for the EXACT solver)."""
    r = random.Random(seed)
    bvh = BVHTree.FromObject(high, bpy.context.evaluated_depsgraph_get())
    pts = []; p = Vector(start); d = Vector(direction).normalized()
    for i in range(int(length / step) + 1):
        hit = bvh.find_nearest(p)
        if hit[0] is None: break
        q, nrm = hit[0], hit[1]
        pts.append((q.copy(), nrm.copy()))
        d = (d - nrm * d.dot(nrm)).normalized()
        d = (Matrix.Rotation(r.uniform(-jag, jag), 3, nrm) @ d).normalized()
        p = q + d * step
    bm = bmesh.new()
    N = len(pts)
    for i in range(N - 1):
        (a, na), (b, nb) = pts[i], pts[i + 1]
        t0, t1 = 1 - i / N, 1 - (i + 1) / N
        sec = []
        for (q, nq, t) in ((a, na, t0), (b, nb, t1)):
            side = (b - a).cross(nq).normalized()
            w = width * (0.35 + 0.65 * t) * r.uniform(0.8, 1.2); dd = depth * (0.4 + 0.6 * t)
            sec.append([bm.verts.new(q + nq * 0.0008 + side * w * 0.5), bm.verts.new(q + nq * 0.0008 - side * w * 0.5), bm.verts.new(q - nq * dd)])
        (a0, a1, a2), (b0, b1, b2) = sec
        for f in ((a0, a1, a2), (b2, b1, b0), (a0, b0, b1, a1), (a1, b1, b2, a2), (a2, b2, b0, a0)):
            bm.faces.new(f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K._obj_from_bm('cut_crack', bm)


def subtract(ob, cutters, self_isect=False):
    """Boolean-difference all cutters (joined) from ob, EXACT solver; cutters are deleted."""
    if not cutters: return
    # only spall scars read as fresh (pale) stone; chisel chamfers keep the weathered surface
    fr = [K.dup(x, x.name + '_fr') for x in cutters if x.get('fresh')]
    c = K.join('cutters', cutters) if len(cutters) > 1 else cutters[0]
    # MANIFOLD: 0.6 s on a 400k-tri high; EXACT took 43 s and, without use_self on overlapping cutters,
    # returned an EMPTY mesh. Fall back to EXACT(use_self) if MANIFOLD rejects the operands.
    n0 = K.tris(ob); keep = ob.data.copy()
    for solver in ('MANIFOLD', 'EXACT'):
        m = ob.modifiers.new('cut', 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.object = c; m.solver = solver
        if solver == 'EXACT': m.use_self = True; m.use_hole_tolerant = True
        K.apply_mods(ob)
        if K.tris(ob) > n0 * 0.5: break
        print('CUT', solver, 'failed, retry'); ob.data = keep.copy()
    print('CUT', solver, n0, '->', K.tris(ob))
    bpy.data.objects.remove(c)
    if not fr: return None
    return K.join('fresh', fr) if len(fr) > 1 else fr[0]


def fresh_mask(ob, cutter, dist=0.0012):
    """Per-vertex 1 near the cutter surface (fresh breaks, chisel facets), 0 elsewhere; stored as 'fr'."""
    bvh = BVHTree.FromObject(cutter, bpy.context.evaluated_depsgraph_get())
    co, _ = K.mesh_arrays(ob)
    fr = np.zeros(len(co))
    for i, v in enumerate(co):
        h = bvh.find_nearest(Vector(v), dist * 2)
        if h[0] is not None: fr[i] = max(0.0, 1.0 - h[3] / (dist * 2))
    return fr


def store_fr(ob, fr):
    me = ob.data; a = me.attributes.get('fr') or me.attributes.new('fr', 'FLOAT', 'POINT')
    a.data.foreach_set('value', np.clip(fr, 0, 1))


def blows(ob, courses, per_course, r=(0.007, 0.011), depth=0.0006, zr=None, seed=0):
    """Hammer blows on a turned (z-axis) part: shallow spherical caps in staggered courses by z band,
    combined as MAX depth so neighbours meet in sharp ridges. No smoothing afterwards."""
    rnd = np.random.default_rng(seed)
    co, no = K.mesh_arrays(ob)
    z0, z1 = zr if zr else (co[:, 2].min(), co[:, 2].max())
    d = np.zeros(len(co))
    for ci in range(courses):
        zc = z0 + (ci + 0.5) * (z1 - z0) / courses
        for k in range(per_course):
            ang = (k + 0.5 * (ci % 2) + rnd.uniform(-0.25, 0.25)) * 2 * math.pi / per_course
            rad = rnd.uniform(*r)
            zz = zc + rnd.uniform(-0.25, 0.25) * (z1 - z0) / courses
            # nearest surface vertex to (ang, zz) on the outer side
            dirv = np.array([math.cos(ang), math.sin(ang), 0.0])
            cand = np.abs(co[:, 2] - zz) < rad
            cand &= (co[:, :2] @ dirv[:2]) > 0
            cand &= (no @ dirv) > 0.2
            if not cand.any(): continue
            idx = np.nonzero(cand)[0]
            j = idx[np.argmax(co[idx, :2] @ dirv[:2])]
            c = co[j]; nr = no[j]
            dist = np.linalg.norm(co - c, axis=1)
            R = rad ** 2 / (2 * depth) + depth / 2              # sphere radius giving cap depth `depth` over radius `rad`
            dd = depth - (R - np.sqrt(np.maximum(R * R - dist ** 2, 0)))
            dd = np.where((dist < rad) & ((no @ nr) > 0.5), dd * rnd.uniform(0.6, 1.3), 0)
            d = np.maximum(d, dd)
    K.set_co(ob, co - no * d[:, None])
