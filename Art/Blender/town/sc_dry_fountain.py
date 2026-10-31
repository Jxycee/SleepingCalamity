"""DryFountain + BannerPole: B10 hero prop (T4, PHASE1 4 and P40), the dry fountain at the centre of the Hollowmere square.

A round dressed-sandstone basin, chunky and storybook: an outer diameter of 3.4 m, a 0.55 m rim, a footing course that
steps out, two coursed rings of blocks with recessed lime joints (drafted margins, pillowed faces), and a fat rounded
coping in nine stones (bullnose outside, chamfer under, a crowned top). In the bowl stands a carved column: a moulded
pedestal with a scallop-shell roundel on each face, a shaft with a rope band, a lobed upper dish (tazza) with calcite
drip nubs under its lips, and on top the spout (user, 2026-10-30): an original drowsy beast's head, medium whimsy, chin
resting on its two forepaws, eyes shut in heavy lid lines, a carved ruff of chunky locks, small drooping ears, and the
mouth a little open where the water once poured into the dish. Nothing from Dark Cloud.
Story (dry since the calamity), each beat visible from the capture cameras: a pale mineral tide line inside the
bowl with fainter rings below it where the water fell in stages, a dried green-brown film under it; cracked silt
plates curling up on the floor with dead leaves blown against the north wall; calcite crust and white drip streaks
where water ran from the beast's mouth down its chin and paws and over the dish lips; moss and green algae streaks on the shaded north side
and moss in the low joints; a seat worn smooth and pale into the coping on the plaza side (south-west), where people
sit. A few spalls with square arrises (flat fresh scars) on the coping and the block corners.
BannerPole: an iron-banded, sun-greyed timber pole (4.5 m) in a sandstone socket, with an arm and a brace, carrying a
0.7 x 2.2 m harvest banner bleached from madder red to pink-cream, with a frayed, torn hem.

Layout (docs/art/beauty-corner-brief.md 1): Unity metres, +Z north, origin = fountain centre. Authored in Blender axes,
z up, Unity (x, y, z) = Blender (-x, -z, y) (sc-asset-craft 2), so north is Blender -Y. Both pieces have their origin on
the ground: DryFountain at the basin centre, BannerPole at the foot of the pole; place the pole at Unity (-2.6, 0, 1.8) at rotation 0 (its banner faces south, toward the cameras; the arm points east).
The cameras below are the brief's three captures in those coordinates (vertical FOVs from BeautyCornerTable.cs:
close 38, mid 42, wide 46) plus a three-quarter view. Two unique bakes: DryFountain (stone, silt, leaves; up to 4K)
and BannerPole (timber, iron, cloth, socket; 2K). Budget (PHASE1 3.3, T4): 30-60k tris together; the lows default to
46k + 9k. Colliders: a cylinder r 1.70 h 0.55 for the basin plus a capsule on the column; a box on the socket and pole.
refs: Reference/hollowmere/market_square/REFS.md (sandstone market fountains: Rottweil, Rothenburg Marktplatz:
coursed basin, heavy coping, central column, wear on the seat edge); colour from PHASE1 3.1 and the B1 sandstone
tileable's measured hue #b9a07a (Art/Textures/B1_town/SOURCES.md).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_dry_fountain.py -- --out <scratch>
      [--stage look|bake]   look (default): procedural highs rendered directly, no bake; bake: lows + baked maps + render
      [--res 1024]          bake resolution (the PC's final bake: --res 4096 for DryFountain, 2048 for BannerPole)
      [--only fountain,pole] [--tris 46000] [--ptris 9000] [--rres 960] [--samples 48]
      [--views wide,mid,close,tq] [--moods noon,lowsun] [--no-render]
Cloud sessions commit this generator only; the PC runs --stage bake --res 4096 to write the textures (sc-asset-craft 4).

Status (2026-10-31, cloud): look rounds 1-6 scored (calibrated sc-asset-review, camera-visible only) 23.5 -> 27 -> 28 -> 29.5 -> 30.5 -> 31/40:
round 2 dropped the dish and beast 0.30 m into the 38 deg close camera, fixed the values, tide line, ground-line damp
and the ruff; round 3 added the shaft flutes, rosier and paler replacement stones, rain streaks under the coping, a
clean dish and a paler seat; round 4 gave the beast puffy shut lids with lid lines deep enough to bake, twisted locks of
varied length in the ruff, loose threads at the banner's tears and hem, and sun-bleached banner edges; round 5 (the controller's 4K in-engine note: the beast read awake, its deep nostril
holes looked like two open eyes) made it read asleep: small shallow nostril slits on a low flat nose pad, sagging
crescent lid lines cut deeper, a low relaxed brow drooping outward, slumped cheeks, a more lolling head, one slack
mouth slit round the spout bore, and the beast's tail draped round the column under the dish; round 6 (in-engine: the snout read as a pig's disc with two round nostrils)
built a lion's muzzle: a broad bridge down from the brow, a heart-shaped nose pad with curled nostril slits, two puffy
whisker pads with whisker pits either side of a groove, and heavier shut lids. Beside the
fountain refs (Reference/hollowmere/fountain/) the gap is weathering: the real basins carry darker grey-green patina low
down and more tone change between stones; bc_109 stands on two wide steps. Bake proof (round 3) (--stage bake --res 1024, cloud): DryFountain high 4.31M -> low 48,724 tris,
BannerPole high 0.79M -> low 8,996 (57.7k together, in the T4 30-60k band); at 1024 the closed-eye lid lines soften
toward brows, so check them first in the PC's 4K bake. Next: the PC runs --stage bake --res 4096 (DryFountain) / 2048 (BannerPole) and exports.
Parked (optional, after the captures): an octagonal basin variant on one worn step A/B'd at the mid camera; B9 crack
weeds and moss tufts at the basin foot and in the north joints; ruff locks with a spiral twist and varied length (side-on
they read as stacked discs); a sun-side bleach gradient across the banner and a loose thread from each tear.
"""
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, kdtree
import sc_aaa_kit as K, sc_aaa_cuts as C

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
STAGE = arg('--stage', 'look'); RES = int(arg('--res', '1024')); RRES = int(arg('--rres', '960'))
SAMPLES = int(arg('--samples', '48'))
ONLY = arg('--only', 'fountain,pole').split(',')
VIEWS = arg('--views', 'wide,mid,close,tq').split(','); MOODS = arg('--moods', 'noon').split(',')
NAME_F, NAME_P = 'DryFountain', 'BannerPole'

def U(x, y, z):
    """Unity metres (+Z north, y up) -> Blender."""
    return Vector((-x, -z, y))

# ----------------------------------------------------------------------------- dimensions (metres, Blender axes)
R_OUT = 1.70                          # coping outer radius: 3.4 m across
R_WO, R_WI = 1.62, 1.30               # wall outer and inner faces
Z_RIM = 0.555                         # coping crown
Z_COPE = 0.44                         # coping bed
Z_SILT = 0.18                         # dried silt surface in the bowl
Z_TIDE = 0.40                         # the old water line (rings below it at the stages it fell)
GAP = 0.016                           # joint width (resolves at the 5 mm voxel)
COURSES = [  # (z0, z1, blocks, phase, r_in, r_out)
    (-0.03, 0.10, 11, 0.30, 1.27, 1.74),     # footing: a 12 cm worn step out from the wall (the refs' low step)
    (0.10, 0.31, 13, 0.00, R_WI, R_WO),
    (0.31, Z_COPE + 0.01, 15, 0.50, R_WI, R_WO),
]
N_COPE = 9
ZT = -0.30                            # dish and spout drop: the 38 deg close camera sees the beast's face (BeautyCornerTable)
A_SIT = math.atan2(0.72, -0.69)       # the worn seat faces the plaza and the cameras (south-west, Blender +Y -X)
FACE_AZ = math.atan2(0.78, -0.62)     # the beast faces the cameras
POLE = U(-2.6, 0.0, 1.8)              # BannerPole placement (controller, 2026-10-30; its own origin is at its foot)

def bake_xf(ob):
    bpy.context.view_layer.update()
    ob.data.transform(ob.matrix_world); ob.matrix_world = Matrix.Identity(4)
    return ob

PIDS = {}
def pid(name):
    PIDS[name] = 0.173 * (len(PIDS) + 1); return PIDS[name]

def joints(ci):
    """Joint angles of course ci: even spacing, jittered so the blocks differ in length (one stream per course)."""
    z0, z1, n, ph, ri, ro = COURSES[ci]; r = K.rng(f'joints{ci}'); st = 2 * math.pi / n
    return [st * (k + ph) + r.uniform(-0.2, 0.2) * st for k in range(n)]

JOINTS = [joints(i) for i in range(len(COURSES))]
CJ = (lambda r: [2 * math.pi * (k + 0.15) / N_COPE + r.uniform(-0.12, 0.12) * 2 * math.pi / N_COPE for k in range(N_COPE)])(K.rng('cope'))

# ----------------------------------------------------------------------------- geometry helpers
def sweep(name, prof, a0, a1, step=0.012, rmid=1.5, zshear=0.0):
    """Closed solid: a 2D (r, z) profile (closed polygon, CCW) swept round the z axis from a0 to a1, end caps on.
    zshear tilts the stone along its length (old work is never dead level)."""
    n = max(3, int(abs(a1 - a0) * rmid / step)); bm = bmesh.new(); rings = []
    for i in range(n + 1):
        t = i / n; a = a0 + (a1 - a0) * t; c, s = math.cos(a), math.sin(a)
        rings.append([bm.verts.new((r * c, r * s, z + zshear * (t - 0.5))) for r, z in prof])
    m = len(prof)
    for A, B in zip(rings[:-1], rings[1:]):
        for j in range(m):
            k = (j + 1) % m; bm.faces.new((A[j], A[k], B[k], B[j]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K._obj_from_bm(name, bm)

def sq_loft(name, prof, segs=96, rot=0.0):
    """Closed solid through rounded-square rings: prof [(half-width, z, n)] bottom -> top (n 2 = circle, 6 = square)."""
    bm = bmesh.new(); rings = []
    for hw, z, n in prof:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs; c, s = math.cos(a), math.sin(a)
            r = hw / (abs(c) ** n + abs(s) ** n) ** (1.0 / n)
            ring.append(bm.verts.new((r * math.cos(a + rot), r * math.sin(a + rot), z)))
        rings.append(ring)
    for A, B in zip(rings[:-1], rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs; bm.faces.new((A[i], A[j], B[j], B[i]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K._obj_from_bm(name, bm)

def sphere(name, ctr, r, scale=(1, 1, 1), seg=24):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2, radius=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * r * scale[0], v.co.y * r * scale[1], v.co.z * r * scale[2])) + Vector(ctr)
    return K._obj_from_bm(name, bm)

def plate(name, base, out, lat, height, thick, rays=0, curl=None, J=10, tip=0.35):
    """A fin or a flat carved plate as a closed solid. base: points along the root; out: unit outward direction per
    root point; height: per root point; lat: thickness axis (unit, or one per root point). Thickness tapers to `tip`
    of the root value and swells along `rays` ridges. curl(i_t, j_t) -> Vector offset bends the tips."""
    I = len(base); bm = bmesh.new(); A = []; B = []
    for i in range(I):
        ti = i / (I - 1); la = Vector(lat[i] if isinstance(lat, list) else lat)
        ray = (0.5 + 0.5 * math.cos(2 * math.pi * ti * rays)) if rays else 0.0
        ra, rb = [], []
        for j in range(J + 1):
            tj = j / J
            c = Vector(base[i]) + Vector(out[i]) * height[i] * tj
            if curl: c = c + curl(ti, tj)
            th = thick * (1 - (1 - tip) * tj) * (0.7 + 0.6 * ray) * (0.35 + 0.65 * math.sin(math.pi * min(max(ti, 0.04), 0.96)) ** 0.4)
            ra.append(bm.verts.new(c + la * th / 2)); rb.append(bm.verts.new(c - la * th / 2))
        A.append(ra); B.append(rb)
    for i in range(I - 1):
        for j in range(J):
            bm.faces.new((A[i][j], A[i + 1][j], A[i + 1][j + 1], A[i][j + 1]))
            bm.faces.new((B[i][j], B[i][j + 1], B[i + 1][j + 1], B[i + 1][j]))
        for j in (0, J):
            bm.faces.new((A[i][j], B[i][j], B[i + 1][j], A[i + 1][j]))
    for i in (0, I - 1):
        for j in range(J):
            bm.faces.new((A[i][j], A[i][j + 1], B[i][j + 1], B[i][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K._obj_from_bm(name, bm)

def cyl(name, p, axis, r, depth, back=0.01, seg=24):
    """Cylinder from p - axis*back to p + axis*depth (drills, spout bore)."""
    a = Vector(axis).normalized()
    ob = K.lathe(name, [(0.001, -back), (r, -back), (r, depth), (0.001, depth)], seg, close=True)
    M = C._orient(a).to_4x4(); M.translation = Vector(p); ob.matrix_world = M
    return bake_xf(ob)

def store_attr(ob, name, arr):
    me = ob.data; arr = np.asarray(arr, dtype=np.float32)
    if arr.ndim == 1:
        a = me.attributes.get(name) or me.attributes.new(name, 'FLOAT', 'POINT'); a.data.foreach_set('value', arr)
    else:
        a = me.attributes.get(name) or me.attributes.new(name, 'FLOAT_VECTOR', 'POINT'); a.data.foreach_set('vector', arr.reshape(-1))

def block_of(co):
    """Per vertex: (block id, u along the block 0..1, v up the block 0..1) from the masonry layout; -1 outside it."""
    r = np.hypot(co[:, 0], co[:, 1]); a = np.mod(np.arctan2(co[:, 1], co[:, 0]), 2 * math.pi); z = co[:, 2]
    bid = np.full(len(co), -1.0); u = np.zeros(len(co)); v = np.zeros(len(co))
    for ci, (z0, z1, n, ph, ri, ro) in enumerate(COURSES):
        m = (z >= z0 - 0.004) & (z < z1 + 0.004) & (r > ri - 0.03) & (r < ro + 0.03) & (z < Z_COPE)
        J = np.mod(np.array(JOINTS[ci]), 2 * math.pi); order = np.argsort(J); J = J[order]
        k = np.searchsorted(J, a[m], side='right') - 1
        lo = J[k % n]; hi = np.where(k + 1 < n, J[(k + 1) % n], J[0] + 2 * math.pi)
        aa = np.where(a[m] < lo, a[m] + 2 * math.pi, a[m])
        bid[m] = ci * 100 + (k % n); u[m] = (aa - lo) / (hi - lo); v[m] = (z[m] - z0) / (z1 - z0)
    mc = z >= Z_COPE - 0.004
    J = np.sort(np.mod(np.array(CJ), 2 * math.pi)); k = np.searchsorted(J, a[mc], side='right') - 1
    lo = J[k % N_COPE]; hi = np.where(k + 1 < N_COPE, J[(k + 1) % N_COPE], J[0] + 2 * math.pi)
    aa = np.where(a[mc] < lo, a[mc] + 2 * math.pi, a[mc])
    bid[mc] = 900 + (k % N_COPE); u[mc] = (aa - lo) / (hi - lo); v[mc] = (r[mc] - 1.235) / (R_OUT - 1.235)
    return bid, u, v

# ----------------------------------------------------------------------------- basin
def basin_blocks():
    obs = []
    for ci, (z0, z1, n, ph, ri, ro) in enumerate(COURSES):
        J = JOINTS[ci]; r = K.rng(f'blocks{ci}')
        for k in range(n):
            a0 = J[k]; a1 = J[(k + 1) % n] + (2 * math.pi if k + 1 == n else 0.0)
            g = GAP / 2 / ro
            dro = r.uniform(-0.006, 0.009); dri = r.uniform(-0.004, 0.004)
            zb = z0 + (GAP / 2 if ci else 0.0); zt = z1 - GAP / 2 + r.uniform(-0.006, 0.004)
            prof = [(ri + dri, zb), (ro + dro, zb), (ro + dro, zt), (ri + dri, zt)]
            ob = sweep(f'blk{ci}_{k}', prof, a0 + g, a1 - g, step=0.03, rmid=ro, zshear=r.uniform(-0.008, 0.008))
            K.bevel(ob, 0.02, 3, angle=30, harden=False); K.apply_mods(ob)
            obs.append(ob)
    # lime mortar core behind the joints (1.6 cm recessed), so a joint reads as a filled bed, not a black void
    core = sweep('mortar', [(R_WI + 0.016, -0.02), (R_WO - 0.016, -0.02), (R_WO - 0.016, Z_COPE), (R_WI + 0.016, Z_COPE)], 0.0, 2 * math.pi, step=0.05, rmid=R_WO)
    foot = sweep('mortarF', [(1.285, -0.03), (1.725, -0.03), (1.725, 0.09), (1.285, 0.09)], 0.0, 2 * math.pi, step=0.05, rmid=1.6)
    return obs + [core, foot]

def masonry_erode(seed):
    """Dressed faces: drafted margins kept flat and a pillowed, pick-dressed centre on the faces the cameras see,
    plus fine surface noise. Stores bid (block id) and buv (u, v, margin) for the material."""
    def f(ob):
        co, no = K.mesh_arrays(ob)
        bid, u, v = block_of(co)
        r = np.hypot(co[:, 0], co[:, 1]) + 1e-9
        radial = (no[:, 0] * co[:, 0] + no[:, 1] * co[:, 1]) / r
        L = np.where(bid >= 900, 2 * math.pi * 1.5 / N_COPE, 0.8)              # rough block length for the margin
        H = np.where(bid >= 900, 0.46, 0.2)
        mu = np.minimum(u, 1 - u) * L; mv = np.minimum(v, 1 - v) * H
        marg = np.clip(np.minimum(mu, mv) / 0.035, 0, 1)                       # 0 at the arris .. 1 past the 3.5 cm margin
        face = (np.abs(radial) > 0.7) & (bid >= 0) & (bid < 900)
        pil = np.sin(np.clip(u, 0, 1) * math.pi) ** 0.5 * np.sin(np.clip(v, 0, 1) * math.pi) ** 0.5
        pick = K.fbm(co, 60.0, 3, 0.5, seed + 2.0)
        d = np.where(face, -(0.006 * pil * marg) + 0.0018 * pick * marg, 0.0)  # pillow out, pick back in
        d += 0.0008 * (K.fbm(co, 25.0, 4, 0.55, seed) - 0.5) * 2
        K.set_co(ob, co - no * d[:, None])
        store_attr(ob, 'bid', bid); store_attr(ob, 'buv', np.stack([u, v, marg], 1))
    return f

def basin_cuts(ob):
    """Spalls with square arrises: flat fresh scars on block corners the cameras see (chamfer cutters)."""
    cut = []
    rr = K.rng('spalls')
    for (ci, k, end, top) in ((1, 4, 1, 1), (2, 6, 0, 1), (1, 9, 0, 0), (2, 10, 1, 1)):
        z0, z1, n, ph, ri, ro = COURSES[ci]; J = JOINTS[ci]
        a = J[(k + end) % n] + (-1 if end else 1) * (GAP / 2 / ro + 0.004)
        out = Vector((math.cos(a), math.sin(a), 0.0)); tng = Vector((-math.sin(a), math.cos(a), 0.0)) * (1 if end else -1)
        zc = (z1 - GAP / 2 - 0.004) if top else (z0 + GAP / 2 + 0.004)
        p = out * (ro + 0.002) + Vector((0, 0, zc))
        nrm = (out + tng * rr.uniform(0.6, 1.0) + Vector((0, 0, 0.8 if top else -0.8))).normalized()
        c = C.chamfer(p - nrm * rr.uniform(0.022, 0.035), nrm, half=(rr.uniform(0.04, 0.07), rr.uniform(0.03, 0.05)), thick=0.12)
        c['fresh'] = 1; cut.append(c)
    return C.subtract(ob, cut)

def coping():
    obs = []
    prof = [(1.235, Z_COPE), (1.655, Z_COPE), (1.682, 0.462), (1.698, 0.488), (1.700, 0.510), (1.690, 0.532), (1.668, 0.548),
            (1.620, 0.556), (1.47, 0.560), (1.32, 0.556), (1.272, 0.549), (1.246, 0.534), (1.235, 0.512)]
    r = K.rng('copestones')
    for k in range(N_COPE):
        a0 = CJ[k]; a1 = CJ[(k + 1) % N_COPE] + (2 * math.pi if k + 1 == N_COPE else 0.0)
        g = GAP / 2 / 1.5; dz = r.uniform(-0.004, 0.006); dr = r.uniform(-0.006, 0.006)
        p = [(rr + (dr if rr > 1.5 else 0.0), z + (dz if z > Z_COPE + 0.01 else 0.0)) for rr, z in prof]
        ob = sweep(f'cope{k}', p, a0 + g, a1 - g, step=0.012, rmid=1.6, zshear=r.uniform(-0.01, 0.01))
        K.bevel(ob, 0.018, 3, angle=50, harden=False); K.apply_mods(ob)
        obs.append(ob)
    return obs

def coping_erode(seed):
    """The seat: the crown worn down about a centimetre and rounded where people have sat on the plaza side."""
    inner = masonry_erode(seed)
    def f(ob):
        inner(ob)
        co, no = K.mesh_arrays(ob)
        a = np.arctan2(co[:, 1], co[:, 0]); da = np.angle(np.exp(1j * (a - A_SIT)))
        r = np.hypot(co[:, 0], co[:, 1])
        w = np.exp(-(da / 0.32) ** 2) * np.clip((co[:, 2] - 0.50) / 0.05, 0, 1) * np.exp(-((r - 1.56) / 0.12) ** 2)
        co = co.copy(); co[:, 2] -= 0.011 * w
        K.set_co(ob, co)
    return f

def coping_cuts(ob):
    cut = []; rr = K.rng('copespall')
    for a in (A_SIT + 0.55, A_SIT - 0.75, A_SIT + 1.6, -1.2):
        out = Vector((math.cos(a), math.sin(a), 0.0))
        p = out * 1.693 + Vector((0, 0, 0.528))
        nrm = (out + Vector((0, 0, rr.uniform(0.3, 0.9)))).normalized()
        c = C.chamfer(p - nrm * rr.uniform(0.012, 0.02), nrm, half=(rr.uniform(0.05, 0.09), rr.uniform(0.025, 0.04)), thick=0.1)
        c['fresh'] = 1; cut.append(c)
    return C.subtract(ob, cut)

# ----------------------------------------------------------------------------- column, dish and the beast
def shell_roundel(name, ctr, nrm, R=0.085):
    """A scallop shell in relief on a pedestal face: a fan of ribs over a backing slab, hinge at the bottom."""
    n = Vector(nrm).normalized(); up = Vector((0, 0, 1)); side = up.cross(n).normalized()
    hinge = Vector(ctr) - up * R * 0.75; obs = []
    for i in range(9):
        a = math.radians(-70 + 140 * i / 8); d = up * math.cos(a) + side * math.sin(a)
        L = R * (1.45 - 0.25 * abs(math.sin(a)))
        pts = [hinge + d * L * t + n * (0.016 * math.sin(math.pi * min(t * 1.1, 1.0)) + 0.004) for t in np.linspace(0.04, 1.0, 10)]
        obs.append(K.tube(f'{name}r{i}', pts, 0.011, 8, radii=[0.006 + 0.008 * t for t in np.linspace(0, 1, 10)]))
    slab = plate(f'{name}s', [hinge + side * x * R * 0.4 for x in np.linspace(-1, 1, 7)],
                 [(up * 1.0 + side * x * 0.9).normalized() for x in np.linspace(-1, 1, 7)], n,
                 [R * (1.35 - 0.2 * abs(x)) for x in np.linspace(-1, 1, 7)], 0.02, J=6, tip=0.8)
    hb = sphere(f'{name}h', hinge + n * 0.006, 0.022, (1.6, 1.6, 1.0))
    return obs + [slab, hb]

def column():
    obs = []
    rot = math.pi / 4 + A_SIT - math.pi / 2 + 0.25                 # a die face toward the cameras
    ped = sq_loft('pedestal', [(0.30, 0.08, 4), (0.37, 0.10, 4), (0.375, 0.14, 4), (0.35, 0.17, 4), (0.32, 0.19, 4), (0.30, 0.215, 4),
                               (0.30, 0.52, 4), (0.315, 0.535, 4), (0.345, 0.555, 4), (0.35, 0.585, 4), (0.33, 0.61, 4), (0.20, 0.625, 3)], 128, rot)
    obs.append(ped)
    for f in range(4):                                               # shell roundels on the four die faces
        a = rot + f * math.pi / 2; n = Vector((math.cos(a), math.sin(a), 0))
        obs += shell_roundel(f'shell{f}', n * 0.30 + Vector((0, 0, 0.375)), n)
    sz = lambda z: 0.60 + (z - 0.60) * (0.71 + ZT) / 0.71           # the shaft is shortened by the drop, a stubbier storybook column
    shaft = K.lathe('shaft', K.smooth_profile([(0.22, 0.60), (0.215, 0.625, 'c'), (0.18, 0.64), (0.175, sz(0.67)), (0.165, sz(0.70)), (0.162, sz(0.80)), (0.158, sz(1.0)),
                                               (0.15, sz(1.15)), (0.146, sz(1.22)), (0.168, sz(1.245)), (0.172, sz(1.265)), (0.145, sz(1.29)), (0.13, sz(1.31))], 0.006), 252, close=True)
    obs.append(shaft)
    for zc, rt, rr_ in ((sz(0.685), 0.024, 0.168), (sz(1.235), 0.022, 0.15)):
        obs.append(K.tube(f'ring{zc}', [(math.cos(a) * rr_, math.sin(a) * rr_, zc) for a in np.linspace(0, 2 * math.pi, 97)], rt, 12))
    zb = sz(0.96)
    for k in range(3):                                               # rope band: three strands twisting round a ring
        pts, ph = [], 2 * math.pi * k / 3
        for th in np.linspace(0, 2 * math.pi * 1.003, 420):
            phi = th * 22 + ph; rb = 0.161 + 0.013 * math.cos(phi)
            pts.append((rb * math.cos(th), rb * math.sin(th), zb + 0.013 * math.sin(phi)))
        obs.append(K.tube(f'rope{k}', pts, 0.0115, 10))
    # tazza: a lobed dish with a thick rolled lip; a boss in its centre takes the beast's paws
    dish = K.lathe('dish', K.smooth_profile([(0.125, 1.29), (0.16, 1.31), (0.26, 1.36), (0.38, 1.40), (0.45, 1.425), (0.475, 1.445), (0.478, 1.465),
                                             (0.465, 1.48), (0.44, 1.483), (0.42, 1.47), (0.33, 1.452), (0.2, 1.445), (0.16, 1.45), (0.135, 1.47),
                                             (0.12, 1.50), (0.09, 1.515), (0.03, 1.52)], 0.006), 160, close=False)
    dish.location.z = ZT; bake_xf(dish)
    me = dish.data; bm = bmesh.new(); bm.from_mesh(me)
    for v in bm.verts:                                               # 8 scalloped lobes
        rr = math.hypot(v.co.x, v.co.y)
        if rr > 0.2:
            a = math.atan2(v.co.y, v.co.x) - FACE_AZ; w = min(1.0, (rr - 0.2) / 0.2)
            s = 1 + 0.075 * w * math.cos(8 * a); v.co.x *= s; v.co.y *= s
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary])
    bm.to_mesh(me); bm.free()
    obs.append(dish)
    return obs

SHAFT_FLUTES = (0.60 + 0.12 * (0.71 + ZT) / 0.71, 0.60 + 0.60 * (0.71 + ZT) / 0.71)
def tail():
    """The beast's tail, curled down round the fluted shaft from under the dish, ending in a carved tuft."""
    cam = math.atan2(1.6, -1.6)                                      # the close camera's bearing: drape the tail across its view
    z0, z1 = 0.98, 0.70; pts = []; rads = []; a0, a_end = cam + 1.5, cam - 1.3
    for t in np.linspace(0, 1, 90):
        a = a0 + (a_end - a0) * t; z = z0 + (z1 - z0) * t ** 0.9
        R = 0.188 - 0.008 * t
        pts.append((R * math.cos(a), R * math.sin(a), z)); rads.append(0.044 - 0.022 * t)
    obs = [K.tube('tail', pts, 0.03, 14, radii=rads)]
    a = a_end; tip = Vector((0.19 * math.cos(a), 0.19 * math.sin(a), z1 - 0.03))
    for k in range(5):                                               # tuft: five chunky curls
        d = Vector((math.cos(a + 0.5 * (k - 2) * 0.5), math.sin(a + 0.5 * (k - 2) * 0.5), 0)) * 0.012
        obs.append(sphere(f'tuft{k}', tip + d + Vector((0, 0, -0.012 * abs(k - 2))), 0.026, (1.0, 1.0, 1.5), 16))
    return obs

def flutes(ob):
    """14 flutes cut into the shaft after the remesh and fillet (the seam fillet smooths concave channels away):
    round channels between flat fillets, with rounded stops top and bottom."""
    co, _ = K.mesh_arrays(ob); z0f, z1f = SHAFT_FLUTES
    r = np.hypot(co[:, 0], co[:, 1]); m = (co[:, 2] > z0f) & (co[:, 2] < z1f) & (r < 0.168)   # the shaft, not the tail
    u = np.mod(14 * np.arctan2(co[:, 1], co[:, 0]) / (2 * math.pi), 1.0)
    ch = np.sqrt(np.clip(1 - ((u - 0.5) / 0.4) ** 2, 0, 1))
    e = np.sqrt(np.clip(np.minimum((co[:, 2] - z0f) / 0.05, (z1f - co[:, 2]) / 0.05), 0, 1))
    k = np.where(m, 1 - 0.09 * ch * e, 1.0)
    co = co.copy(); co[:, 0] *= k; co[:, 1] *= k; K.set_co(ob, co)

def dish_nubs():
    """Calcite drip nubs hanging under each lobe tip, where the overflow ran for years."""
    obs = []; r = K.rng('nubs')
    for i in range(8):
        a = FACE_AZ + i * math.pi / 4; R = 0.478 * 1.075 - 0.02
        for j in range(2 if i % 3 else 3):
            aa = a + r.uniform(-0.06, 0.06); L = r.uniform(0.025, 0.05); rr = R - r.uniform(0.0, 0.03)
            ob = K.lathe(f'nub{i}{j}', K.smooth_profile([(0.002, -L), (0.008, -L * 0.6), (0.013, -L * 0.2), (0.016, 0.012), (0.002, 0.014)], 0.003), 12, close=True)
            ob.location = (rr * math.cos(aa), rr * math.sin(aa), 1.445 + ZT); obs.append(bake_xf(ob))
    return obs

# the spout: a drowsy beast's head, chin on its paws on the dish boss, eyes shut, mouth a little open.
# Built in a head frame (x forward toward the cameras, y left, z up) from overlapping soft volumes that the voxel
# remesh unions into one carving (Tripo-style compound, sc-asset-craft 5).
PIVOT = Vector((0.0, 0.0, 1.60 + ZT))
R_FACE = Matrix.Rotation(FACE_AZ, 4, 'Z')
R_TILT = Matrix.Rotation(math.radians(10.0), 4, 'X') @ Matrix.Rotation(math.radians(9.0), 4, 'Y')   # dozing: head lolling, nose dipping
FF = Vector((math.cos(FACE_AZ), math.sin(FACE_AZ), 0.0)); FL = Vector((0, 0, 1)).cross(FF)

def HW(p, tilt=True):
    """Head frame -> world."""
    M = Matrix.Translation(PIVOT) @ R_FACE @ (R_TILT if tilt else Matrix.Identity(4))
    return M @ Vector(p)

def blob(name, ctr, radii, tilt=True, rot=None, seg=32):
    ob = sphere(name, (0, 0, 0), 1.0, radii, seg)
    M = Matrix.Translation(PIVOT) @ R_FACE @ (R_TILT if tilt else Matrix.Identity(4)) @ Matrix.Translation(Vector(ctr))
    if rot: M = M @ rot
    ob.matrix_world = M
    return bake_xf(ob)

EYE = [(0.205, s * 0.118, 0.318) for s in (1, -1)]
MOUTH = (0.305, 0.0, 0.105)

def beast():
    obs = [blob('chest', (-0.05, 0.0, 0.03), (0.21, 0.25, 0.15), tilt=False),
           blob('cranium', (0.02, 0.0, 0.27), (0.24, 0.27, 0.23)),
           blob('muzzle', (0.21, 0.0, 0.19), (0.15, 0.16, 0.11)),
           # a lion's muzzle, not a pig's disc: a broad bridge down from the brow to a heart-shaped nose pad,
           # two puffy whisker pads under it either side of a groove, the mouth below them
           blob('bridge', (0.27, 0.0, 0.275), (0.10, 0.055, 0.04), rot=Matrix.Rotation(math.radians(-18), 4, 'Y')),
           blob('noseL', (0.335, 0.03, 0.238), (0.036, 0.04, 0.027)), blob('noseR', (0.335, -0.03, 0.238), (0.036, 0.04, 0.027)),
           blob('nosetip', (0.348, 0.0, 0.214), (0.03, 0.03, 0.022)),
           blob('chin', (0.18, 0.0, 0.065), (0.10, 0.12, 0.06))]
    for s in (1, -1):
        obs.append(blob(f'jowl{s}', (0.215, s * 0.11, 0.10), (0.105, 0.105, 0.095)))   # cheeks slumped low, dozing
        e = EYE[(1 - s) // 2]
        obs.append(blob(f'eyemound{s}', e, (0.058, 0.07, 0.05)))       # a heavy shut lid, and the fold above it
        obs.append(blob(f'lidfold{s}', (e[0] + 0.01, e[1], e[2] + 0.024), (0.046, 0.074, 0.028)))
        obs.append(blob(f'whisker{s}', (0.302, s * 0.048, 0.168), (0.056, 0.058, 0.046)))
        # a relaxed brow: low, soft, drooping toward the outer corner (never arched, which reads surprised)
        brow = [HW((0.195 + 0.025 * math.cos(u), s * (0.118 + 0.065 * math.sin(u)), 0.352 + 0.006 * math.cos(u) - 0.016 * math.sin(u))) for u in np.linspace(-1.4, 1.4, 16)]
        obs.append(K.tube(f'brow{s}', brow, 0.02, 12, radii=[0.012 + 0.01 * math.cos(u) for u in np.linspace(-1.4, 1.4, 16)]))
        obs.append(blob(f'ear{s}', (-0.04, s * 0.235, 0.41), (0.045, 0.07, 0.088), rot=Matrix.Rotation(s * math.radians(-35), 4, 'X')))
        obs.append(blob(f'paw{s}', (0.21, s * 0.12, -0.03), (0.12, 0.078, 0.056), tilt=False))
        for k, dy in enumerate((-0.042, 0.0, 0.042)):
            obs.append(blob(f'toe{s}{k}', (0.315, s * 0.12 + dy, -0.032), (0.036, 0.03, 0.036), tilt=False, seg=20))
    rl = K.rng('locks')
    for k, a in enumerate(np.linspace(-2.0, 2.0, 13)):              # the ruff: broad overlapping locks, each twisted and its own length
        L = rl.uniform(0.85, 1.2); R = 0.275 + 0.02 * (L - 1)
        ctr = (-0.06, R * math.sin(a), 0.22 + R * math.cos(a))
        rot = Matrix.Rotation(-a, 4, 'X') @ Matrix.Rotation(0.35, 4, 'Y') @ Matrix.Rotation((0.45 if k % 2 else -0.45) * L, 4, 'Z')
        obs.append(blob(f'lock{k}', ctr, (0.055, 0.095, 0.12 * L), rot=rot, seg=24))
        if abs(a) < 1.8:
            ctr2 = (-0.12, 0.215 * math.sin(a + 0.15), 0.22 + 0.215 * math.cos(a + 0.15))
            obs.append(blob(f'lockb{k}', ctr2, (0.05, 0.085, 0.10), rot=Matrix.Rotation(-(a + 0.15), 4, 'X'), seg=20))
    return obs

def _snap_tube(bvh, name, pts, r, sides=8, radii=None):
    q = []
    for p in pts:
        hit = bvh.find_nearest(p)
        if hit[0] is not None: q.append(hit[0])
    return K.tube(name, q, r, sides, radii=radii)

def beast_cuts(ob):
    """Closed-eye lid lines with lash ticks, nostrils, the open mouth and its bore, ear cups, toe and lock grooves,
    all snapped onto the carved surface."""
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
    cut = []
    for s in (1, -1):
        e = Vector(EYE[(1 - s) // 2])
        lid = [HW(e + Vector((0.06, s * 0.058 * u, -0.01 + 0.022 * u * u))) for u in np.linspace(-1, 1, 16)]   # a sagging crescent: the shut lid line
        cut.append(_snap_tube(bvh, f'lid{s}', lid, 0.01, radii=[0.007 + 0.007 * math.cos(u * 1.4) for u in np.linspace(-1, 1, 16)]))   # deep enough to survive the bake
        for k, u in enumerate((-0.55, -0.1, 0.35, 0.75)):
            a = e + Vector((0.06, s * 0.058 * u, -0.012 + 0.022 * u * u))
            b = a + Vector((0.0, s * 0.012 * (u + 0.3), -0.02))
            cut.append(_snap_tube(bvh, f'lash{s}{k}', [HW(a.lerp(b, t)) for t in np.linspace(0, 1, 5)], 0.0055, 6))
        # nostrils: small shallow slits curling outward, not deep holes (deep dark holes read as wide-open eyes)
        ns = [HW((0.37, s * (0.012 + 0.03 * t), 0.226 + 0.012 * t - 0.018 * t * t)) for t in np.linspace(0, 1, 7)]
        cut.append(_snap_tube(bvh, f'nostril{s}', ns, 0.006, 8, radii=[0.0035 + 0.0035 * math.sin(math.pi * t) for t in np.linspace(0, 1, 7)]))
        for j, (dy, dz) in enumerate(((0.035, 0.18), (0.055, 0.17), (0.045, 0.152))):   # whisker pits on the pads
            q = bvh.find_nearest(HW((0.36, s * dy, dz)))[0]
            if q is not None: cut.append(sphere(f'whpit{s}{j}', q, 0.0065, (1, 1, 1), 12))
        ec = HW((-0.02, s * 0.25, 0.41)); out = (HW((0.04, s * 0.24, 0.41)) - ec).normalized()
        cup = sphere(f'earcup{s}', ec + out * 0.045, 0.04, (1.0, 1.0, 1.4)); cut.append(cup)
        for dy in (-0.021, 0.021):                                   # toe grooves
            pts = [HW((x, s * 0.12 + dy, 0.0), tilt=False) for x in np.linspace(0.27, 0.36, 6)]
            cut.append(_snap_tube(bvh, f'toeg{s}{dy}', pts, 0.006, 6))
    m = Vector(MOUTH)
    cut.append(_snap_tube(bvh, 'philtrum', [HW((0.36, 0.0, z)) for z in np.linspace(0.198, 0.13, 6)], 0.006, 8))   # the groove between the pads
    for k, dy in enumerate(np.linspace(-0.05, 0.05, 9)):            # slack, a little open: one wide shallow slit, corners turned down
        mo = m + Vector((-0.004 * abs(dy) / 0.05, dy, -0.014 - 0.012 * (abs(dy) / 0.05) ** 2))
        cut.append(cyl(f'mouth{k}', HW(mo + Vector((0.04, 0, 0))), HW(mo - Vector((0.04, 0, 0.004))) - HW(mo + Vector((0.04, 0, 0))), 0.011, 0.07, back=0.0, seg=12))
    cut.append(cyl('bore', HW(m + Vector((0.04, 0, 0))), HW(m - Vector((0.1, 0, 0.02))) - HW(m + Vector((0.04, 0, 0))), 0.026, 0.2, back=0.0))
    for k, a in enumerate(np.linspace(-2.0, 2.0, 13)):              # a lock groove down each curl of the ruff
        c0 = (-0.06 + 0.05, 0.29 * math.sin(a), 0.22 + 0.29 * math.cos(a))
        pts = [HW((c0[0], c0[1] + 0.075 * math.sin(a) * t, c0[2] + 0.075 * math.cos(a) * t)) for t in np.linspace(-1, 1, 8)]
        cut.append(_snap_tube(bvh, f'lockg{k}', pts, 0.0065, 6))
    C.subtract(ob, cut)
    return None

# ----------------------------------------------------------------------------- bowl contents
def silt():
    """Cracked silt: Voronoi plates over the bowl floor, cracks 1-2 cm deep, plate edges curling up as they dried."""
    from scipy.spatial import cKDTree
    R = R_WI + 0.01; step = 0.011; xs = np.arange(-R, R + step, step)
    X, Y = np.meshgrid(xs, xs); keep = np.hypot(X, Y) < R; P = np.stack([X[keep], Y[keep]], 1)
    r = K.rng('silt'); seeds = []
    while len(seeds) < 260:
        x, y = r.uniform(-R, R), r.uniform(-R, R)
        if math.hypot(x, y) < R: seeds.append((x, y))
    d, _ = cKDTree(np.array(seeds)).query(P, k=2); edge = d[:, 1] - d[:, 0]
    rr = np.hypot(P[:, 0], P[:, 1])
    z = Z_SILT - 0.02 * (1 - np.clip(edge / 0.012, 0, 1)) ** 1.5 + 0.006 * np.exp(-edge / 0.03) * np.clip(edge / 0.012, 0, 1)
    z += 0.012 * np.clip((rr - 1.05) / 0.25, 0, 1) ** 2                # silt banked against the wall
    z -= 0.025 * np.clip((0.42 - rr) / 0.12, 0, 1)                    # scoured hollow round the pedestal
    z += 0.004 * (K.fbm(np.stack([P[:, 0], P[:, 1], np.zeros(len(P))], 1), 3.0, 3, 0.5, 4.0) - 0.5)
    bm = bmesh.new(); idx = -np.ones(X.shape, dtype=int); vs = []
    for n, (i, j) in enumerate(zip(*np.nonzero(keep))):
        idx[i, j] = n; vs.append(bm.verts.new((P[n, 0], P[n, 1], z[n])))
    for i in range(X.shape[0] - 1):
        for j in range(X.shape[1] - 1):
            q = (idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j])
            if min(q) >= 0: bm.faces.new([vs[k] for k in q])
    ob = K._obj_from_bm('silt', bm)
    store_attr(ob, 'cr', np.clip(1 - edge / 0.012, 0, 1))
    return ob

def leaves():
    """Dead leaves: curled, blown into the north side of the bowl and a few on the silt and the rim."""
    obs = []; r = K.rng('leaves'); n = 34
    for i in range(n):
        if i < 22:                                                    # drift against the north wall
            a = -math.pi / 2 + r.gauss(0, 0.55); rr = r.uniform(1.0, 1.26)
        elif i < 31:
            a = r.uniform(0, 2 * math.pi); rr = r.uniform(0.45, 1.2)
        else:
            a = A_SIT + r.uniform(-0.8, 0.8); rr = r.uniform(1.4, 1.6)
        L = r.uniform(0.07, 0.12); W = L * r.uniform(0.45, 0.6); curl = r.uniform(0.2, 0.9)
        bm = bmesh.new(); grid = []
        for u in np.linspace(0, 1, 9):
            row = []
            for v in np.linspace(-1, 1, 7):
                w = W * 0.5 * math.sin(math.pi * u) ** 0.8 * (1 - 0.15 * u)
                x = (u - 0.5) * L; y = v * w; zz = curl * 0.25 * (v * w) ** 2 / max(W, 1e-3) * 4 + 0.01 * curl * math.sin(u * 3)
                row.append(bm.verts.new((x, y, zz)))
            grid.append(row)
        for a_, b_ in zip(grid[:-1], grid[1:]):
            for j in range(6): bm.faces.new((a_[j], a_[j + 1], b_[j + 1], b_[j]))
        ob = K._obj_from_bm(f'leaf{i}', bm)
        sm = ob.modifiers.new('sol', 'SOLIDIFY'); sm.thickness = 0.0025; K.apply_mods(ob)
        top = Z_RIM - 0.003 if i >= 31 else Z_SILT + 0.012 * max(0.0, (rr - 1.05) / 0.25) ** 2 + 0.008
        ob.rotation_euler = (r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3), r.uniform(0, 2 * math.pi))
        ob.location = (rr * math.cos(a), rr * math.sin(a), top + (0.004 * (i % 3) if i < 22 else 0.0)); bake_xf(ob)
        store_attr(ob, 'bid', np.full(len(ob.data.vertices), float(i)))
        obs.append(ob)
    return obs

# ----------------------------------------------------------------------------- banner pole (own origin at its foot)
POLE_H = 4.5
def pole_parts():
    stone = [sq_loft('socket', [(0.30, -0.05, 5), (0.30, 0.0, 5), (0.295, 0.22, 5), (0.28, 0.26, 5), (0.25, 0.285, 5), (0.20, 0.30, 4)], 64, 0.3)]
    rr = K.rng('pole')
    lean = Matrix.Rotation(math.radians(1.3), 4, Vector((0.6, 0.8, 0)).normalized())
    def P(z):                                                         # pole axis: a 1.3 deg lean and a slight bow
        return lean @ Vector((0.012 * math.sin(math.pi * z / POLE_H), 0.0, z))
    bm = bmesh.new(); rings = []; segs = 32
    for z in np.linspace(0.1, POLE_H, 120):
        hw = 0.085 - 0.018 * z / POLE_H; c = P(z)
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs + 0.3; ca, sa = math.cos(a), math.sin(a)
            r = hw / (abs(ca) ** 3.2 + abs(sa) ** 3.2) ** (1 / 3.2)
            ring.append(bm.verts.new(c + Vector((r * math.cos(a - 0.3 + 0.3), r * math.sin(a), 0))))
        rings.append(ring)
    for A_, B_ in zip(rings[:-1], rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs; bm.faces.new((A_[i], A_[j], B_[j], B_[i]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    timber = [K._obj_from_bm('pole', bm)]
    top = P(POLE_H)
    timber.append(K.lathe('cap', K.smooth_profile([(0.05, POLE_H - 0.02), (0.075, POLE_H), (0.07, POLE_H + 0.03), (0.04, POLE_H + 0.06), (0.012, POLE_H + 0.07)], 0.004), 32, close=True))
    timber[-1].location = (top.x, top.y, 0); bake_xf(timber[-1])
    # the arm points east (Blender -X), banner faces south toward the cameras
    za = POLE_H - 0.30; ca = P(za)
    arm = K.box('arm', (0.95, 0.075, 0.085), (ca.x - 0.44, ca.y, za), cuts=(12, 1, 1)); bake_xf(arm)
    for v in arm.data.vertices: v.co.z -= 0.015 * ((ca.x - v.co.x) / 0.9) ** 2      # the arm sags at its tip
    timber.append(arm)
    iron = []
    for zb in (0.42, 1.55, 2.75, POLE_H - 0.55):
        c = P(zb); hw = 0.085 - 0.018 * zb / POLE_H + 0.006
        band = sq_loft(f'band{zb}', [(hw, zb - 0.032, 3.2), (hw + 0.004, zb - 0.026, 3.2), (hw + 0.004, zb + 0.026, 3.2), (hw, zb + 0.032, 3.2)], 48)
        band.location = (c.x, c.y, 0); iron.append(bake_xf(band))
        for k in range(4):
            a = k * math.pi / 2 + 0.3
            iron.append(sphere(f'riv{zb}{k}', (c.x + math.cos(a) * (hw + 0.004), c.y + math.sin(a) * (hw + 0.004), zb), 0.011, (1, 1, 1), 12))
    brace = [P(za - 0.55) + Vector((-0.07, 0, 0)), Vector((ca.x - 0.55, ca.y, za - 0.04))]
    iron.append(K.tube('brace', [brace[0].lerp(brace[1], t) for t in np.linspace(0, 1, 12)], 0.013, 10))
    for x in (0.18, 0.70):                                            # banner hangers: rings through the arm
        iron.append(K.tube(f'hook{x}', [(ca.x - x, ca.y, za - 0.045), (ca.x - x, ca.y, za - 0.11)], 0.007, 8))
    iron.append(K.tube('collar', [(math.cos(a) * 0.1, math.sin(a) * 0.1, 0.305) for a in np.linspace(0, 2 * math.pi, 49)], 0.012, 10))
    rod = K.tube('rod', [(ca.x - 0.06, ca.y, za - 0.13), (ca.x - 0.82, ca.y, za - 0.13)], 0.016, 12)
    timber.append(rod)
    return stone, timber, iron, (ca.x - 0.44, ca.y, za - 0.13)

def banner(top_ctr):
    """0.7 x 2.2 m cloth hanging from the rod: a sleeve at the top, soft vertical folds, a frayed and torn hem."""
    W, H = 0.70, 2.20; NU, NV = 40, 120; r = K.rng('banner')
    tears = [(0.27, 0.16, 0.035), (0.71, 0.09, 0.025)]                # (u, depth, half-width) V tears up from the hem
    def hem(u):
        L = H - 0.025 * (0.5 + 0.5 * math.sin(u * 23.0 + 1.3)) - 0.018 * (0.5 + 0.5 * math.sin(u * 61.0))
        for tu, td, tw in tears:
            L -= td * max(0.0, 1 - abs(u - tu) / tw)
        L -= 0.03 * u                                                 # the bleached hem has frayed shorter at the east end
        return L
    bm = bmesh.new(); grid = []
    x0 = Vector(top_ctr)
    for i in range(NU + 1):
        u = i / NU; L = hem(u); col = []
        for j in range(NV + 1):
            v = j / NV; zz = L * v
            fold = 0.028 * math.sin(2 * math.pi * (u * 2.6 + 0.15)) * (0.25 + 0.75 * v) + 0.01 * math.sin(2 * math.pi * u * 6.3) * v
            sway = 0.07 * v ** 2 + 0.03 * v ** 2 * math.sin(u * 4.0)
            p = x0 + Vector(((u - 0.5) * W * (1 - 0.04 * v), fold + sway, -zz))
            if zz < 0.05:                                             # sleeve: wraps round the rod
                ang = (1 - zz / 0.05) * math.pi * 0.9
                p = x0 + Vector(((u - 0.5) * W, 0.022 * math.sin(ang), -0.022 * (1 - math.cos(ang)) * 0.5 - zz * 0.3))
            col.append(bm.verts.new(p))
        grid.append(col)
    for i in range(NU):
        for j in range(NV): bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    ob = K._obj_from_bm('banner', bm)
    uv = np.array([(i / NU, j / NV, hem(i / NU)) for i in range(NU + 1) for j in range(NV + 1)])
    store_attr(ob, 'cuv', np.stack([uv[:, 0], uv[:, 1] * uv[:, 2] / H, uv[:, 1]], 1))
    sm = ob.modifiers.new('sol', 'SOLIDIFY'); sm.thickness = 0.004; sm.offset = 0.0; K.apply_mods(ob)
    threads = []                                                     # loose threads hanging from the tears and the hem
    for k in range(14):
        u = r.choice([0.25, 0.27, 0.29, 0.69, 0.71, 0.73]) if k < 6 else r.uniform(0.04, 0.96)
        L0 = hem(u); Lt = r.uniform(0.04, 0.10)
        x = x0 + Vector(((u - 0.5) * W * (1 - 0.04), 0.07 + 0.03 * math.sin(u * 4.0) + 0.028 * math.sin(2 * math.pi * (u * 2.6 + 0.15)), -L0 + 0.004))
        pts = [x + Vector((0.006 * math.sin(t * 5 + k), 0.004 * t, -Lt * t)) for t in np.linspace(0, 1, 6)]
        th = K.tube(f'thread{k}', pts, 0.0018, 5)
        store_attr(th, 'cuv', np.tile([u, 1.0, 1.0], (len(th.data.vertices), 1))); threads.append(th)
    return K.join('banner', [ob] + threads)

# ----------------------------------------------------------------------------- materials
def _blockspace(T):
    """Per-block random tone, the drafted margin, and a block-offset sample position."""
    bid = T.attr('bid', vec=False); buv = T.sep(T.attr('buv'))
    w = T.node('ShaderNodeTexWhiteNoise', noise_dimensions='1D'); T.set(w, 'W', bid)
    return (w, 'Value'), (w, 'Color'), (buv, 'Z'), buv

def sandstone(name, column=False):
    """Warm dressed sandstone (#b9a07a family, PHASE1 3.1: no chroma boost): bedding striations, mineral grain,
    per-block tone, drafted margins smoother than the pick-dressed centres, then the story layers: tide line and
    staged rings, the dried green-brown water film, moss and algae on the north, moss in the low joints, the worn
    pale seat, fresh spall scars, calcite crust and streaks on the column and dish, dust in the crevices."""
    m, T = K.new_mat(name)
    lp = T.attr('lp'); ln = T.sep(T.attr('ln')); L = T.sep(lp)
    tone, tcol, marg, buv = _blockspace(T)
    p = T.vmath('ADD', lp, T.vmath('MULTIPLY', tcol, (5.0, 5.0, 5.0)))
    x, y, z = (L, 'X'), (L, 'Y'), (L, 'Z')
    rr = T.vmath('LENGTH', T.comb(x, y, 0.0))
    nz = (ln, 'Z')
    radial = T.math('DIVIDE', T.math('ADD', T.math('MULTIPLY', (ln, 'X'), x), T.math('MULTIPLY', (ln, 'Y'), y)), T.math('MAXIMUM', rr, 0.001))
    edge = T.edge(radius=0.008, lo=0.03, hi=0.22)
    cav = T.cavity(dist=0.05, lo=0.3, hi=0.95)
    dirt = T.math('SUBTRACT', 1.0, cav)
    # base: tone per block, bedding (stretched noise in z), grain
    base = (0.45, 0.33, 0.19)
    bed = T.noise(T.vmath('MULTIPLY', p, (0.6, 0.6, 9.0)), scale=3.0, detail=5, rough=0.6, dist=0.4)
    mot = T.noise(p, scale=4.0, detail=6, rough=0.6)
    c = T.mix(T.maprange(tone, 0.0, 1.0, 0.0, 1.0), T.hsv(base, 0.505, 0.9, 0.8), T.hsv(base, 0.49, 1.0, 1.2))
    rosy = T.smoothstep((T.sep(tcol), 'X'), 0.78, 0.8)                 # a few rosier stones from another bed of the quarry
    c = T.mix(T.math('MULTIPLY', rosy, 0.7), c, (0.46, 0.27, 0.17))
    pale = T.smoothstep((T.sep(tcol), 'Y'), 0.86, 0.88)                # and a few paler replacements
    c = T.mix(T.math('MULTIPLY', pale, 0.6), c, T.hsv(base, 0.5, 0.75, 1.35))
    c = T.mix(T.math('MULTIPLY', T.smoothstep(bed, 0.42, 0.62), 0.45), c, T.hsv(base, 0.5, 1.12, 0.8))
    c = T.mix(T.math('MULTIPLY', T.smoothstep(mot, 0.5, 0.72), 0.35), c, T.hsv(base, 0.47, 0.85, 1.15))
    gr = T.voronoi(p, scale=900.0, out='Color'); gv = T.sep(gr)
    c = T.mix(0.25, c, T.mix(1.0, c, T.comb(T.maprange((gv, 'X'), 0, 1, 0.8, 1.2), T.maprange((gv, 'X'), 0, 1, 0.8, 1.18), T.maprange((gv, 'X'), 0, 1, 0.82, 1.15)), 'MULTIPLY'))
    # edges a touch paler (worn arrises), crevices darker
    c = T.mix(T.math('MULTIPLY', edge, 0.35), c, T.hsv(base, 0.5, 0.8, 1.22))
    c = T.mix(T.math('MULTIPLY', dirt, 0.55), c, T.hsv(base, 0.5, 0.9, 0.42))
    r = T.mixf(T.smoothstep(mot, 0.3, 0.7), 0.74, 0.86)
    # dressing: pick marks in the block centres (bump), smoother drafted margins
    picks = T.math('MULTIPLY', T.smoothstep(T.voronoi(p, scale=70.0, out='Distance'), 0.25, 0.05), marg)
    h = T.math('ADD', T.math('MULTIPLY', T.noise(p, scale=40, detail=6, rough=0.65), 0.6), T.math('MULTIPLY', picks, -0.8))
    h = T.math('ADD', h, T.math('MULTIPLY', T.noise(p, scale=300, detail=3), 0.35))
    h = T.math('ADD', h, T.math('MULTIPLY', T.smoothstep(bed, 0.45, 0.6), 0.25))
    # fresh spall scars: paler, cleaner
    fr = T.smoothstep(T.attr('fr', vec=False), 0.05, 0.6)
    c = T.mix(T.math('MULTIPLY', fr, 0.8), c, T.hsv(base, 0.5, 0.78, 1.38))
    # water zone: inside the bowl (inward faces of the ring, or anything within it) below the tide line
    inbowl = T.math('MAXIMUM', T.smoothstep(rr, R_WI - 0.02, R_WI - 0.06), T.math('MULTIPLY', T.smoothstep(radial, -0.2, -0.6), T.smoothstep(rr, R_WO + 0.02, R_WO - 0.02)))
    # the ground line: dark damp stone and the thickest moss where the basin meets the paving (refs: darkest there)
    gl = T.math('ADD', z, T.math('MULTIPLY', T.math('SUBTRACT', T.noise(p, scale=5.0, detail=4), 0.5), 0.08))
    foot = T.math('MULTIPLY', T.smoothstep(gl, 0.22, 0.0), T.math('SUBTRACT', 1.0, T.smoothstep(rr, R_WI + 0.02, R_WI - 0.02)))
    c = T.mix(T.math('MULTIPLY', foot, 0.85), c, T.hsv(base, 0.5, 0.85, 0.32)); r = T.mixf(foot, r, 0.9)
    wob = T.math('MULTIPLY', T.math('SUBTRACT', T.noise(p, scale=6.0, detail=3), 0.5), 0.012)
    zt = T.math('ADD', z, wob)
    below = T.math('MULTIPLY', T.smoothstep(zt, Z_TIDE + 0.004, Z_TIDE - 0.004), inbowl)
    film = T.math('MULTIPLY', below, T.maprange(T.noise(p, scale=9.0, detail=5), 0.3, 0.7, 0.55, 1.0))
    c = T.mix(T.math('MULTIPLY', film, 0.8), c, T.mix(T.smoothstep(zt, Z_SILT, Z_TIDE), (0.09, 0.085, 0.05), (0.19, 0.16, 0.095)))
    tide = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(zt, Z_TIDE - 0.022, Z_TIDE - 0.004), T.smoothstep(zt, Z_TIDE + 0.006, Z_TIDE + 0.001)), inbowl)
    rings = 0.0
    for zr, s in ((0.345, 0.55), (0.29, 0.4), (0.24, 0.3)):
        b = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(zt, zr - 0.008, zr - 0.002), T.smoothstep(zt, zr + 0.003, zr)), s)
        rings = b if rings == 0.0 else T.math('MAXIMUM', rings, b)
    tide = T.math('MAXIMUM', tide, T.math('MULTIPLY', rings, inbowl))
    tide = T.math('MULTIPLY', tide, T.smoothstep(T.noise(p, scale=30.0, detail=5), 0.2, 0.38))
    c = T.mix(T.math('MULTIPLY', tide, 0.95), c, (0.70, 0.68, 0.62)); r = T.mixf(tide, r, 0.92)
    h = T.math('ADD', h, T.math('MULTIPLY', tide, 0.5))
    # north: moss on up-facing and low surfaces, algae streaks running down from the coping's drip edge
    north = T.smoothstep(T.math('DIVIDE', T.math('MULTIPLY', y, -1.0), T.math('MAXIMUM', rr, 0.001)), -0.25, 0.85)
    outer = T.math('MULTIPLY', T.smoothstep(radial, 0.2, 0.6), T.smoothstep(rr, R_WI + 0.1, R_WO - 0.05))
    streak = T.noise(T.vmath('MULTIPLY', p, (5.0, 5.0, 0.35)), scale=4.0, detail=4, rough=0.6)
    alg = T.math('MULTIPLY', T.math('MULTIPLY', outer, north), T.smoothstep(streak, 0.48, 0.66))
    alg = T.math('MULTIPLY', alg, T.smoothstep(z, Z_COPE + 0.01, 0.2))
    c = T.mix(T.math('MULTIPLY', alg, 0.75), c, (0.10, 0.12, 0.05)); r = T.mixf(alg, r, 0.9)
    rain = T.math('MULTIPLY', T.math('MULTIPLY', outer, T.smoothstep(streak, 0.5, 0.6)), T.smoothstep(z, 0.06, Z_COPE - 0.02))
    rain = T.math('MULTIPLY', rain, T.smoothstep(z, Z_COPE + 0.005, Z_COPE - 0.01))   # rain streaks off the coping's drip edge, all round
    c = T.mix(T.math('MULTIPLY', rain, 0.55), c, T.hsv(base, 0.5, 0.85, 0.55))
    mossmask = T.math('ADD', T.math('MULTIPLY', T.math('MULTIPLY', north, T.up(0.2, 0.8)), T.math('ADD', 0.7, T.math('MULTIPLY', T.math('MAXIMUM', dirt, T.math('MULTIPLY', edge, 0.8)), 1.2))), T.math('MULTIPLY', dirt, T.smoothstep(z, 0.2, 0.03)))
    mossmask = T.math('ADD', mossmask, T.math('MULTIPLY', T.math('MULTIPLY', north, dirt), 0.9))
    mossmask = T.math('ADD', mossmask, T.math('MULTIPLY', foot, T.math('ADD', 0.6, T.math('MULTIPLY', north, 0.8))))
    mn = T.noise(p, scale=8.0, detail=8, rough=0.7)
    moss = T.smoothstep(T.math('SUBTRACT', T.math('MULTIPLY', mossmask, mn), 0.3), 0.0, 0.12)
    moss = T.math('MULTIPLY', moss, T.math('SUBTRACT', 1.0, below))
    moss = T.math('MULTIPLY', moss, T.smoothstep(z, Z_RIM + 0.15, Z_RIM + 0.02))      # nothing grows up on the column or in the dish
    mcol = T.mix(T.noise(p, scale=60.0, detail=4), (0.05, 0.07, 0.018), (0.15, 0.18, 0.035))
    c = T.mix(moss, c, mcol); r = T.mixf(moss, r, 0.95)
    h = T.math('ADD', h, T.math('MULTIPLY', T.math('MULTIPLY', moss, T.noise(p, scale=220, detail=4)), 1.6))
    # the seat: worn smooth and pale on the coping's crown on the plaza side
    ang = T.math('ARCTAN2', y, x)
    da = T.math('ABSOLUTE', T.math('SUBTRACT', T.math('MODULO', T.math('ADD', T.math('SUBTRACT', ang, A_SIT), 3 * math.pi), 2 * math.pi), math.pi))
    seat = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(da, 0.7, 0.15), T.smoothstep(z, 0.515, 0.54)), T.up(0.55, 0.9))
    seat = T.math('MULTIPLY', seat, T.smoothstep(rr, 1.30, 1.42))
    seat = T.math('MULTIPLY', seat, T.smoothstep(rr, 1.70, 1.62))
    c = T.mix(T.math('MULTIPLY', seat, 0.85), c, T.hsv(base, 0.49, 0.75, 1.4)); r = T.mixf(seat, r, 0.35)
    h = T.math('MULTIPLY', h, T.math('SUBTRACT', 1.0, T.math('MULTIPLY', seat, 0.8)))
    if column:
        # calcite: crust on the dish lips and round the beast's mouth, white runs down its chin, paws and the dish
        mw = HW(MOUTH)
        dx = T.vmath('LENGTH', T.comb(T.math('SUBTRACT', x, mw.x), T.math('SUBTRACT', y, mw.y), 0.0))
        lipz = T.math('MULTIPLY', T.smoothstep(z, 1.43 + ZT, 1.465 + ZT), T.smoothstep(z, 1.50 + ZT, 1.48 + ZT))
        lipc = T.math('MULTIPLY', lipz, T.smoothstep(rr, 0.40, 0.47))
        md = T.vmath('LENGTH', T.vmath('SUBTRACT', lp, tuple(mw)))
        mouth = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(md, 0.11, 0.075), T.smoothstep(md, 0.06, 0.075)), T.smoothstep(z, mw.z - 0.01, mw.z - 0.035))   # a crust ring on the chin, outside the mouth
        streak2 = T.noise(T.vmath('MULTIPLY', p, (9.0, 9.0, 0.5)), scale=5.0, detail=4)
        runs = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(dx, 0.16, 0.05), T.smoothstep(z, mw.z + 0.02, mw.z - 0.03)), T.smoothstep(streak2, 0.5, 0.64))
        runs = T.math('MULTIPLY', runs, T.smoothstep(z, 1.42 + ZT, 1.47 + ZT))
        under = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(z, 1.25 + ZT, 1.42 + ZT), T.smoothstep(nz, 0.0, -0.4)), T.smoothstep(streak2, 0.5, 0.62))
        under = T.math('MULTIPLY', under, T.smoothstep(rr, 0.30, 0.42))
        cal = T.math('MAXIMUM', T.math('MAXIMUM', lipc, mouth), T.math('MAXIMUM', runs, under))
        cal = T.math('MULTIPLY', cal, T.smoothstep(T.noise(p, scale=25.0, detail=5), 0.28, 0.45))
        nub = T.math('MULTIPLY', T.smoothstep(z, 1.445 + ZT, 1.43 + ZT), T.smoothstep(rr, 0.42, 0.46))   # the drip nubs themselves
        cal = T.math('MAXIMUM', cal, nub)
        c = T.mix(T.math('MULTIPLY', cal, 0.85), c, T.mix(T.noise(p, scale=90.0, detail=3), (0.58, 0.57, 0.51), (0.72, 0.70, 0.64)))
        r = T.mixf(cal, r, 0.6)
        h = T.math('ADD', h, T.math('MULTIPLY', cal, T.noise(p, scale=120, detail=4)))
    c = T.hsv(c, 0.5, 1.0, 1.0)
    T.output(c, r, 0.0, T.bump(h, strength=0.55, distance=0.004))
    return m

def silt_mat(name):
    """Dried pale silt: cracks dark, plate edges paler and lifting, darker damp patches near the north wall."""
    m, T = K.new_mat(name)
    lp = T.attr('lp'); L = T.sep(lp); cr = T.attr('cr', vec=False)
    rr = T.vmath('LENGTH', T.comb((L, 'X'), (L, 'Y'), 0.0))
    c = T.mix(T.noise(lp, scale=3.0, detail=5), (0.21, 0.18, 0.13), (0.31, 0.27, 0.20))
    c = T.mix(T.math('MULTIPLY', T.smoothstep(T.noise(lp, scale=40.0, detail=4), 0.55, 0.7), 0.4), c, (0.36, 0.33, 0.27))
    lift = T.math('MULTIPLY', T.smoothstep(cr, 0.2, 0.55), T.smoothstep(cr, 0.85, 0.6))
    c = T.mix(T.math('MULTIPLY', lift, 0.5), c, (0.40, 0.36, 0.29))
    c = T.mix(T.smoothstep(cr, 0.8, 0.98), c, (0.06, 0.05, 0.035))
    damp = T.math('MULTIPLY', T.smoothstep((L, 'Y'), -0.6, -1.1), T.smoothstep(T.noise(lp, scale=2.5, detail=4), 0.45, 0.62))
    c = T.mix(T.math('MULTIPLY', damp, 0.6), c, (0.16, 0.15, 0.09))
    c = T.mix(T.math('MULTIPLY', T.smoothstep(rr, 1.15, 1.29), 0.35), c, (0.20, 0.19, 0.11))
    r = T.mixf(damp, 0.9, 0.75)
    h = T.math('ADD', T.math('MULTIPLY', T.noise(lp, scale=150, detail=5), 0.5), T.math('MULTIPLY', T.noise(lp, scale=600, detail=2), 0.3))
    T.output(c, r, 0.0, T.bump(h, strength=0.4, distance=0.002))
    return m

def leaf_mat(name):
    m, T = K.new_mat(name)
    lp = T.attr('lp'); bid = T.attr('bid', vec=False)
    w = T.node('ShaderNodeTexWhiteNoise', noise_dimensions='1D'); T.set(w, 'W', bid)
    c = T.mix((w, 'Value'), (0.20, 0.09, 0.03), (0.42, 0.25, 0.07))
    c = T.mix(T.math('MULTIPLY', T.smoothstep(T.noise(lp, scale=80.0, detail=4), 0.5, 0.7), 0.6), c, (0.10, 0.06, 0.03))
    T.output(c, 0.8, 0.0, T.bump(T.noise(lp, scale=200, detail=3), strength=0.2, distance=0.001))
    return m

def timber_mat(name):
    """Sun-greyed oak pole: open grain and drying checks along its length, grey-brown where the sun bleaches it,
    darker and browner low down where rain splashes, grime under the iron bands (PHASE1 3.1 timber range, lifted by weather)."""
    m, T = K.new_mat(name)
    lp = T.attr('lp'); L = T.sep(lp); z = (L, 'Z')
    g = T.vmath('MULTIPLY', lp, (14.0, 14.0, 0.4))
    grain = T.noise(g, scale=6.0, detail=8, rough=0.65, dist=0.6)
    chk = T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', T.noise(T.vmath('MULTIPLY', lp, (30.0, 30.0, 0.25)), scale=3.0, detail=3), 0.5)), 0.012, 0.0)
    c = T.mix(T.smoothstep(grain, 0.35, 0.7), (0.20, 0.17, 0.14), (0.34, 0.31, 0.27))
    c = T.mix(T.math('MULTIPLY', T.smoothstep(z, 1.2, 0.2), 0.6), c, (0.14, 0.10, 0.07))
    c = T.mix(chk, c, (0.04, 0.03, 0.025))
    cav = T.cavity(dist=0.03, lo=0.3, hi=0.95)
    c = T.mix(T.math('MULTIPLY', T.math('SUBTRACT', 1.0, cav), 0.6), c, (0.05, 0.04, 0.03))
    c = T.mix(T.math('MULTIPLY', T.edge(radius=0.006, lo=0.03, hi=0.2), 0.3), c, (0.42, 0.39, 0.34))
    h = T.math('ADD', T.math('MULTIPLY', grain, 0.6), T.math('MULTIPLY', chk, -1.5))
    T.output(c, T.mixf(grain, 0.72, 0.88), 0.0, T.bump(h, strength=0.6, distance=0.002))
    return m

def iron_mat(name):
    m, T = K.new_mat(name)
    lp = T.attr('lp')
    edge = T.edge(radius=0.004, lo=0.03, hi=0.2)
    rm = T.smoothstep(T.noise(lp, scale=35.0, detail=6, rough=0.7), 0.5, 0.62)
    base = T.mix(T.noise(lp, scale=20, detail=4), (0.07, 0.06, 0.055), (0.13, 0.115, 0.10))
    c = T.mix(T.math('MULTIPLY', edge, 0.6), base, (0.30, 0.28, 0.26))
    c = T.mix(rm, c, T.mix(T.noise(lp, scale=90, detail=3), (0.16, 0.06, 0.02), (0.32, 0.14, 0.05)))
    r = T.mixf(rm, T.mixf(edge, 0.55, 0.35), 0.9); metal = T.mixf(rm, 0.75, 0.0)
    T.output(c, r, metal, T.bump(T.math('ADD', T.voronoi(lp, scale=90.0), T.math('MULTIPLY', rm, 0.5)), strength=0.5, distance=0.001))
    return m

def banner_mat(name):
    """Madder-red linen bleached by years of sun: pink-cream at the top and on the sun side, deeper rose in the folds
    and the sleeve shadow, a ghost of the harvest device (two bands and a sheaf roundel), frayed hem darker with grime."""
    m, T = K.new_mat(name)
    cu = T.sep(T.attr('cuv')); u, v = (cu, 'X'), (cu, 'Z'); lp = T.attr('lp')
    blch = T.math('ADD', T.smoothstep(v, 0.95, 0.0), T.math('MULTIPLY', T.noise(lp, scale=3.0, detail=4), 0.5))
    blch = T.math('ADD', blch, T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', u, 0.5)), 0.38, 0.5), 0.45))   # sun-bleached edges
    blch = T.math('MULTIPLY', blch, 0.8)
    c = T.mix(T.smoothstep(blch, 0.2, 1.0), (0.36, 0.10, 0.08), (0.66, 0.46, 0.39))
    # the harvest device: two bands and a roundel, dyed paler; faded nearly away
    vv = (cu, 'Y')
    bands = T.math('MAXIMUM', T.math('MULTIPLY', T.smoothstep(vv, 0.10, 0.115), T.smoothstep(vv, 0.17, 0.155)),
                   T.math('MULTIPLY', T.smoothstep(vv, 0.80, 0.815), T.smoothstep(vv, 0.87, 0.855)))
    rd = T.vmath('LENGTH', T.comb(T.math('MULTIPLY', T.math('SUBTRACT', u, 0.5), 0.7), T.math('MULTIPLY', T.math('SUBTRACT', vv, 0.43), 2.2), 0.0))
    ring = T.math('MULTIPLY', T.smoothstep(rd, 0.15, 0.16), T.smoothstep(rd, 0.215, 0.205))
    sheaf = T.math('MULTIPLY', T.smoothstep(rd, 0.12, 0.1), T.smoothstep(T.math('ABSOLUTE', T.math('SINE', T.math('MULTIPLY', T.math('SUBTRACT', u, 0.5), 60.0))), 0.4, 0.8))
    dev = T.math('MULTIPLY', T.math('MAXIMUM', T.math('MAXIMUM', bands, ring), sheaf), T.smoothstep(T.noise(lp, scale=20.0, detail=5), 0.25, 0.55))
    c = T.mix(T.math('MULTIPLY', dev, 0.55), c, (0.74, 0.62, 0.45))
    cav = T.cavity(dist=0.05, lo=0.4, hi=0.95)
    c = T.mix(T.math('MULTIPLY', T.math('SUBTRACT', 1.0, cav), 0.5), c, (0.28, 0.08, 0.07))
    hemg = T.math('MULTIPLY', T.smoothstep(v, 0.9, 1.0), T.smoothstep(T.noise(lp, scale=12.0, detail=4), 0.35, 0.6))
    c = T.mix(T.math('MULTIPLY', hemg, 0.6), c, (0.20, 0.14, 0.10))
    weave = T.math('ADD', T.math('ABSOLUTE', T.math('SINE', T.math('MULTIPLY', u, 700.0))), T.math('ABSOLUTE', T.math('SINE', T.math('MULTIPLY', vv, 2200.0))))
    h = T.math('ADD', T.math('MULTIPLY', weave, 0.25), T.noise(lp, scale=40.0, detail=4))
    T.output(c, 0.88, 0.0, T.bump(h, strength=0.3, distance=0.001), Sheen_Weight=0.4)
    return m

# ----------------------------------------------------------------------------- build
def build_fountain():
    stone = sandstone('M_Sandstone'); colst = sandstone('M_SandstoneColumn', column=True)
    highs = []
    wear = K.wear_generic
    highs.append(K.make_part(basin_blocks(), stone, pid('basin'), 0.005, erode=masonry_erode(1.0), fillet=2, cuts=basin_cuts))
    highs.append(K.make_part(coping(), stone, pid('coping'), 0.0045, erode=coping_erode(2.0), fillet=2, cuts=coping_cuts))
    col = K.make_part(column() + dish_nubs() + tail(), colst, pid('column'), 0.004, erode=K.chain(flutes, wear(0.0007, 30, chips=14, chip_r=(0.006, 0.014), seed=3.0)), fillet=3)
    highs.append(col)
    highs.append(K.make_part(beast(), colst, pid('beast'), 0.003, erode=wear(0.0005, 40, chips=8, chip_r=(0.005, 0.01), seed=4.0), fillet=3, cuts=beast_cuts))
    s = K.make_part(silt(), silt_mat('M_Silt'), pid('silt'), 0.0); highs.append(s)
    highs.append(K.make_part(leaves(), leaf_mat('M_Leaf'), pid('leaves'), 0.0))
    for h in highs:                                                   # every high carries every attribute the materials read
        n = len(h.data.vertices)
        for nm, dflt in (('bid', -1.0), ('fr', 0.0), ('cr', 0.0)):
            if nm not in h.data.attributes: store_attr(h, nm, np.full(n, dflt))
        if 'buv' not in h.data.attributes: store_attr(h, 'buv', np.tile([0.5, 0.5, 1.0], (n, 1)))
    return highs

def build_pole():
    stone, timber, iron, top = pole_parts()
    st = sandstone('M_SandstoneSocket')
    highs = [K.make_part(stone, st, pid('socket'), 0.004, erode=K.wear_generic(0.0008, 25, chips=6, chip_r=(0.008, 0.02), seed=5.0), fillet=1),
             K.make_part(timber, timber_mat('M_PoleOak'), pid('timber'), 0.004, erode=K.wear_generic(0.0012, 18, seed=6.0), fillet=2),
             K.make_part(iron, iron_mat('M_PoleIron'), pid('iron'), 0.0025, erode=K.wear_generic(0.0004, 60, seed=7.0), fillet=2),
             K.make_part(banner(top), banner_mat('M_Banner'), pid('banner'), 0.0)]
    for h in highs:
        n = len(h.data.vertices)
        for nm, dflt in (('bid', -1.0), ('fr', 0.0)):
            if nm not in h.data.attributes: store_attr(h, nm, np.full(n, dflt))
        for nm, dv in (('buv', [0.5, 0.5, 1.0]), ('cuv', [0.5, 0.5, 0.5])):
            if nm not in h.data.attributes: store_attr(h, nm, np.tile(dv, (n, 1)))
    return highs

# ----------------------------------------------------------------------------- review render (Cycles CPU)
def render():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    sc.render.resolution_x = RRES; sc.render.resolution_y = RRES * 9 // 16
    sc.render.image_settings.file_format = 'JPEG'; sc.render.image_settings.quality = 92
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
    def mat(name, col, rough=0.85, scale=3.0, var=0.25, cobble=False):
        m = bpy.data.materials.new(name); m.use_nodes = True; n, l = m.node_tree.nodes, m.node_tree.links
        bs = n['Principled BSDF']; bs.inputs['Roughness'].default_value = rough
        nz = n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = scale; nz.inputs['Detail'].default_value = 6
        mx = n.new('ShaderNodeMix'); mx.data_type = 'RGBA'; l.new(nz.outputs['Fac'], mx.inputs[0])
        mx.inputs[6].default_value = (*[c * (1 - var) for c in col], 1); mx.inputs[7].default_value = (*col, 1)
        if cobble:                                                    # stand-in cobbles: dark joints of a Voronoi pattern
            vo = n.new('ShaderNodeTexVoronoi'); vo.feature = 'DISTANCE_TO_EDGE'; vo.inputs['Scale'].default_value = 7.0
            mr = n.new('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = 0.0; mr.inputs['From Max'].default_value = 0.06
            l.new(vo.outputs['Distance'], mr.inputs['Value'])
            mj = n.new('ShaderNodeMix'); mj.data_type = 'RGBA'; l.new(mr.outputs['Result'], mj.inputs[0])
            mj.inputs[6].default_value = (0.06, 0.055, 0.05, 1); l.new(mx.outputs[2], mj.inputs[7]); l.new(mj.outputs[2], bs.inputs['Base Color'])
            bp = n.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = 0.5; l.new(mr.outputs['Result'], bp.inputs['Height']); l.new(bp.outputs['Normal'], bs.inputs['Normal'])
        else:
            l.new(mx.outputs[2], bs.inputs['Base Color'])
        return m
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0)); g = bpy.context.active_object; g.name = 'ground'
    g.data.materials.append(mat('cobble', (0.19, 0.175, 0.17), cobble=True))
    ring = K.lathe('flagring', [(1.66, 0.002), (2.90, 0.002), (2.90, 0.018), (1.66, 0.018)], 96, close=True)
    ring.data.materials.append(mat('flags', (0.23, 0.21, 0.19), scale=1.5))
    fac = K.box('facade', (7.5, 0.6, 9.0), tuple(U(-1.25, 4.5, 7.8))); fac.data.materials.append(mat('plaster', (0.50, 0.43, 0.30), scale=0.8, var=0.12))
    plinth = K.box('plinth', (7.6, 0.66, 0.6), tuple(U(-1.25, 0.3, 7.8))); plinth.data.materials.append(mat('plinthst', (0.36, 0.33, 0.29)))
    for x in (-4.6, -2.0, 0.6, 2.2):
        b = K.box(f'post{x}', (0.28, 0.1, 5.4), tuple(U(x, 3.3, 7.47))); b.data.materials.append(mat('timber', (0.06, 0.045, 0.035)))
    stall = K.box('stall', (2.6, 1.6, 0.9), tuple(U(4.5, 0.45, 4.5)), rot=(0, 0, -200)); stall.data.materials.append(mat('stallwood', (0.22, 0.30, 0.34)))
    fig = None
    if 'wide' in VIEWS:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=tuple(U(-2.7, 0.875, -0.9))); fig = bpy.context.active_object; fig.name = 'figure_1.75m'
        fig.data.materials.append(mat('fig', (0.12, 0.12, 0.13)))
    sc.world = bpy.data.worlds.new('W'); sc.world.use_nodes = True; wn, wl = sc.world.node_tree.nodes, sc.world.node_tree.links
    bg = wn['Background']
    tcw = wn.new('ShaderNodeTexCoord'); sepw = wn.new('ShaderNodeSeparateXYZ'); wl.new(tcw.outputs['Generated'], sepw.inputs[0])
    rmp = wn.new('ShaderNodeValToRGB'); wl.new(sepw.outputs['Z'], rmp.inputs[0])
    sun = bpy.data.lights.new('sun', 'SUN'); so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
    cams = {'wide': (U(1, 1.8, -9), U(-1, 3, 6), 46), 'mid': (U(5, 1.7, -2.5), U(-1.5, 1.8, 5), 42),      # BeautyCornerTable.cs vertical FOVs
            'close': (U(1.6, 1.4, -1.6), U(0, 1.0, 0.6), 38), 'tq': (U(-3.6, 2.5, -3.9), U(0, 0.95, 0), 42),
            'north': (U(-1.5, 1.9, 4.6), U(0, 0.7, 0), 50)}
    for mood in MOODS:
        if mood == 'lowsun':
            el, az, col, E = 14.0, -62.0, (1.0, 0.72, 0.48), 3.2
            sky = ((0.30, 0.30, 0.34), (0.55, 0.62, 0.75))
        else:
            el, az, col, E = 62.0, 20.0, (1.0, 0.93, 0.84), 3.6
            sky = ((0.38, 0.40, 0.42), (0.42, 0.55, 0.78))
        rmp.color_ramp.elements[0].position = 0.5; rmp.color_ramp.elements[0].color = (*sky[0], 1)
        rmp.color_ramp.elements[1].position = 0.75; rmp.color_ramp.elements[1].color = (*sky[1], 1)
        wl.new(rmp.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 0.5
        e, a = math.radians(el), math.radians(az)
        to_sun = U(math.sin(a) * math.cos(e), math.sin(e), -math.cos(a) * math.cos(e))
        so.rotation_euler = (-to_sun).to_track_quat('-Z', 'Y').to_euler(); sun.energy = E; sun.color = col; sun.angle = math.radians(1.5)
        for v in VIEWS:
            if fig: fig.hide_render = v != 'wide'
            pos, look, fov = cams[v]
            cd = bpy.data.cameras.new(v); cd.sensor_fit = 'VERTICAL'; cd.angle_y = math.radians(fov)
            cam = bpy.data.objects.new(v, cd); sc.collection.objects.link(cam)
            cam.location = pos; cam.rotation_euler = (look - pos).to_track_quat('-Z', 'Y').to_euler()
            sc.camera = cam; sc.render.filepath = os.path.join(OUT, f'{NAME_F}_{STAGE}_{mood}_{v}.jpg')
            bpy.ops.render.render(write_still=True); print('RENDER', sc.render.filepath)

# ----------------------------------------------------------------------------- main
K.reset()
fh = build_fountain() if 'fountain' in ONLY else []
ph = build_pole() if 'pole' in ONLY else []
print('HIGH TRIS', {h.name: K.tris(h) for h in fh + ph}, 'total', sum(K.tris(h) for h in fh + ph))
if STAGE == 'bake':
    pdir = os.path.join(OUT, 'props'); os.makedirs(pdir, exist_ok=True)
    if fh:
        lf = K.bake_prop(NAME_F, fh, pdir, target=int(arg('--tris', '46000')), res=RES, extrusion=0.012, ray=0.03); print('LOW TRIS', NAME_F, K.tris(lf))
    if ph:
        lp_ = K.bake_prop(NAME_P, ph, pdir, target=int(arg('--ptris', '9000')), res=max(RES // 2, 512), extrusion=0.006, ray=0.015); print('LOW TRIS', NAME_P, K.tris(lp_))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(pdir, NAME_F + '.blend'))
for o in ph if STAGE == 'look' else ([bpy.data.objects.get(NAME_P)] if ph else []):
    if o: o.location = POLE; bpy.context.view_layer.update()
if '--no-render' not in argv:
    render()
