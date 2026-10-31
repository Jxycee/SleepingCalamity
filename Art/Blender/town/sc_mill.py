"""Mill: Hollowmere's watermill (ring 3 fixed landmark, two states in one FBX). 

Root `Mill` with children: Shell (ruined start), Frame (restored), Part_Wheel (pivot SOCKET_Wheel at the axle),
Part_Door (pivot SOCKET_Door at the threshold), SOCKET_* empties under Frame, FOOTPRINT (9 x 7 m body, 0.1 m tall).
Body 9 x 7 m centred at the origin: rubble stone ground storey (to z 4.4), timber-framed upper storey (to the 8.0 m plate),
steep tile roof along X (ridge 12.35). The wheel (about 4.3 m, recessed into a wheel pit notch in the +X wall because the brief's
x <= 8.0 limit does not allow a 5 m wheel against the wall) turns between two stone cheek walls; a wooden launder on trestles
feeds it from the +X stream side. Shell = ruined state (ragged walls, broken upper timbers, half the roof, tilted broken wheel,
broken flume, rubble heaps).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_mill.py -- --out <scratch> [--no-render] [--export] [--tris]

Status (2026-10-31, cloud, Blender 4.5.3): exported to Assets/_Project/Content/Art/Town/Ring3/Mill/Mill.fbx, sheet Reference/hollowmere/mill/SHEET_mill_final.jpg.
  Tris: Shell 24,784 (cap 30k); Frame 27,404 + Part_Wheel 3,588 + Part_Door 540 = 31,532 (cap 45k). Deterministic (two runs equal).
  Bounds: Shell x -5.15..8.00 y -4.96..4.36 z -0.56..12.66; Frame x -5.46..8.00 y -4.93..4.36 z -0.55..12.66; wheel z min -0.38 (0.17 m above the pit floor).
  Fixed this round: pit pieces were offset by PIT_Z (z -1.1), shell inner wall skins sat 9 m off (origin at the wrong end), yard/heaps/millstones moved in
  (door side), roof decks/tiles/plaster are orient()ed (no inside-out faces on the +Y slope), torn plaster is double-skinned (back_skin) so the roofless shell
  is solid from inside; added ridge tiles, rolled eave, barge boards, shutters, hoist hood, wheel/roof wear. Facing check: `--backface --views ...` renders
  backfaces red; remaining red is only the far side of single-sided walls seen through window gaps (culled in Unity, shows the room).
  Self-score (sc-asset-review rubric, 2 rounds, floor 25): silhouette 4, proportion 3.5, form 3.5, surface 3.5, colour 4, wear 3, set fit 3.5, glass/metal 3 = 28/40.
  Coordinator fixes 1-5 applied (2026-10-31): wear-only change (tris and bounds unchanged): per-face stone value patches, streaky damp at the wall foot,
  cheek/pit/flume-runoff staining, eave band and splash-back staining on the plaster (grime gain 1.5 stone, 1.7 plaster).
  Next visible gains: the wheel reads small behind the stone cheeks (a bigger, brighter paddle wheel needs the x <= 8.0 limit lifted), flume has no water,
  shell lacks sagging on the plate beams, hoist hood is small, ground-floor windows have no shutters.
"""
import sys, os, math, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT; argv = FK.argv; TILE = FK.TILE; TIM = FK.TIM
RRES = int(FK.arg('--rres', '1100')); SAMPLES = int(FK.arg('--samples', '24'))
VIEWS = FK.arg('--views', 'tq,back,left,wheel,door,frame,shell,shellb').split(',')

X0, X1, Y0, Y1 = -4.5, 4.5, -3.5, 3.5
T = 0.6; GZ = 4.4; PL = 8.0; RIDGE = 12.35; HWY = 3.5
TANP = (RIDGE - PL) / HWY
OV_E, OV_V = 0.8, 0.6
AXLE = Vector((5.7, 0.0, 1.75)); WR = 2.15
XN, PIT_Z, PW, CT = 3.1, -0.55, 0.95, 0.5
STEPS = [(4.5, 5.0, 3.1), (5.0, 5.35, 2.2), (5.35, 6.05, 1.55), (6.05, 7.3, 1.15)]
DOOR = (1.45, 3.15, 0.22, 2.75)

EXTRA = {
    'rubble': lambda: FK.tile_mat('ML_Rubble', 'B1_town/sandstone', tint=(0.80, 0.77, 0.72), normal=1.4),
    'oakw': lambda: FK.tile_mat('ML_OakWeathered', 'B1_town/oak_timber', tint=(0.62, 0.60, 0.58), normal=1.3),
    'wheel': lambda: FK.tile_mat('ML_WheelOak', 'B1_town/oak_timber', tint=(0.72, 0.64, 0.54), normal=1.3),
    'earth': lambda: FK.tile_mat('ML_Earth', 'B1_town/plaster', tint=(0.42, 0.32, 0.22), normal=0.8),
    'sack': lambda: FK.tile_mat('ML_Sack', 'B1_town/canvas', tint=(1.0, 0.94, 0.82)),
    'millstone': lambda: FK.tile_mat('ML_Millstone', 'B1_town/sandstone', tint=(1.1, 1.08, 1.0)),
    'interior': lambda: FK.flat_mat('ML_Interior', (0.035, 0.025, 0.018), 0.9),
}
_orig_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)

def lean_member(pc, mat, a, b, w, d, cham=0.025, name='m', sag=0.0, wob=0.006, extend=0.0, up=None):
    a = Vector(a); b = Vector(b)
    axis = (b - a); L = axis.length + 2 * extend
    if L < 1e-4:
        return
    axis.normalize()
    up = Vector(up) if up else Vector((0, -1, 0))
    side = axis.cross(up)
    if side.length < 1e-4:
        up = Vector((1, 0, 0)); side = axis.cross(up)
    side.normalize(); up = side.cross(axis).normalized()
    R = FK.rng('mem' + name)
    rot = Matrix.Rotation(math.radians(R.uniform(-1.6, 1.6)), 4, up)
    xf = Matrix.Translation((a + b) / 2) @ rot @ Matrix((side, up, axis)).transposed().to_4x4()
    cuts = max(1, int(L / 1.8)) if sag else 0
    bend = (lambda p, L=L: Vector((sag * (1 - (p.z / L * 2) ** 2), 0, 0))) if sag else None
    FK.add_box(pc, mat, (w, d, L), xf, cham=cham, cuts=(0, 0, cuts), long_axis=2, bend=bend, wob=wob, seed=R.random() * 100)
FK.member = lean_member

def piece(n): return FK.Piece(n)

def back_skin(pc, n0):
    """Duplicate the faces added since n0 with reversed winding: torn plaster seen from inside the roofless shell stays solid."""
    pc.bm.faces.ensure_lookup_table()
    d = bmesh.ops.duplicate(pc.bm, geom=pc.bm.faces[n0:])
    bmesh.ops.reverse_faces(pc.bm, faces=[g for g in d['geom'] if isinstance(g, bmesh.types.BMFace)])

def orient(pc, n0, expect):
    """Reverse every face added since index n0 whose normal points away from expect (a vector or fn(face_centre)); no inside-out walls."""
    pc.bm.faces.ensure_lookup_table(); pc.bm.normal_update()
    bad = []
    for f in pc.bm.faces[n0:]:
        e = expect(f.calc_center_median()) if callable(expect) else expect
        if f.normal.dot(e) < 0: bad.append(f)
    if bad: bmesh.ops.reverse_faces(pc.bm, faces=bad)


def merge(dst, src, xf=None, lean=False):
    if xf is not None:
        bmesh.ops.transform(src.bm, matrix=xf, verts=src.bm.verts[:])
    if '--tris' in argv and src.bm.verts:
        co = [v.co for v in src.bm.verts]
        print('    %-12s %6d tris  x %.1f..%.1f y %.1f..%.1f z %.1f..%.1f' % ((src.name, sum(len(f.verts) - 2 for f in src.bm.faces)) + tuple(f(c[i] for c in co) for i in range(3) for f in (min, max))))
    if lean:
        for v in src.bm.verts:
            v.co = Vector((v.co.x - 0.010 * max(0.0, v.co.z - GZ), v.co.y - 0.006 * max(0.0, v.co.z - GZ), v.co.z))
    if '--facing' in argv and src.name[0] in 'GU':
        src.bm.normal_update(); out = inn = 0
        for f in src.bm.faces:
            if abs(f.normal.z) > 0.5: continue
            c = f.calc_center_median(); d = Vector((c.x, c.y, 0))
            if f.normal.dot(d) > 0: out += f.calc_area()
            else: inn += f.calc_area()
        print('    FACING %-10s outward %.1f inward %.1f' % (src.name, out, inn))
    remap = []
    for m in src.mats:
        if m not in dst.mats: dst.mats.append(m)
        remap.append(dst.mats.index(m))
    vmap = {v: dst.bm.verts.new(v.co) for v in src.bm.verts}
    for f in src.bm.faces:
        try: nf = dst.bm.faces.new([vmap[v] for v in f.verts])
        except ValueError: continue
        nf.material_index = remap[f.material_index] if remap else 0
        for l, nl in zip(f.loops, nf.loops):
            nl[dst.uv].uv = l[src.uv].uv
    src.bm.free()

def face_xf(origin, n):
    n = Vector(n); t = Vector((0, 0, 1)).cross(n)
    return Matrix.Translation(origin) @ Matrix((t, -n, Vector((0, 0, 1)))).transposed().to_4x4()

def flat(pc, mat, pts, facing, nu=1, nv=1):
    """Quad whose winding is chosen so its normal faces `facing`; world-metre UVs by the dominant axis."""
    a, b, c, d = [Vector(p) for p in pts]
    if (b - a).cross(d - a).dot(Vector(facing)) < 0:
        b, d = d, b
    ax = max(range(3), key=lambda i: abs(facing[i]))
    uv = {0: lambda p: (p.y / TILE, p.z / TILE), 1: lambda p: (p.x / TILE, p.z / TILE), 2: lambda p: (p.x / TILE, p.y / TILE)}[ax]
    FK.quad_grid(pc, mat, [a, b, c, d], nu, nv, uv_fn=uv)

def ragged(seed, depth=3.0, top=GZ):
    def f(u):
        n = noise.noise(Vector((u * 0.8, seed * 3.7, 0.5)))
        d = max(0.0, min(1.0, (n + 0.05) * 2.4)) * depth + 0.3 * noise.noise(Vector((u * 3.1, seed, 1.5)))
        return top - max(0.0, d)
    return f

# ----------------------------------------------------------------------------- stone
def pillow(pc, u0, u1, z0, z1, R):
    g = 0.018
    us = [u0 + g, (u0 + u1) / 2, u1 - g]; zs = [z0 + g, (z0 + z1) / 2, z1 - g]
    dep = [[0.0, R.uniform(0.02, 0.04), 0.0], [R.uniform(0.02, 0.04), R.uniform(0.05, 0.09), R.uniform(0.02, 0.04)], [0.0, R.uniform(0.02, 0.04), 0.0]]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    V = [[bm.verts.new((us[i], -dep[j][i], zs[j])) for i in range(3)] for j in range(3)]
    off = (R.random(), R.random())
    for j in range(2):
        for i in range(2):
            f = bm.faces.new((V[j][i], V[j][i + 1], V[j + 1][i + 1], V[j + 1][i]))
            for l in f.loops:
                l[uvl].uv = (l.vert.co.x / TILE + off[0], l.vert.co.z / TILE + off[1])
    bm.normal_update(); FK._merge(pc, bm, 'rubble')

def stone_wall(pc, u0, u1, z0, z1, top=None, holes=(), seed='sw'):
    R = FK.rng('mlsw' + seed); z = z0; ci = 0
    while z < z1 - 0.05:
        h = R.uniform(0.30, 0.46)
        if z + h > z1 - 0.12: h = z1 - z
        blocks = sorted((hh[0], hh[1]) for hh in holes if hh[2] < z + h - 0.02 and hh[3] > z + 0.02)
        segs = []; a = u0
        for b0, b1 in blocks:
            if b0 > a: segs.append((a, b0))
            a = max(a, b1)
        if a < u1: segs.append((a, u1))
        for s0, s1 in segs:
            u = s0; first = True
            while u < s1 - 0.04:
                ln = R.uniform(0.2, 0.95) if (first and ci % 2) else R.uniform(0.5, 0.95)
                if s1 - (u + ln) < 0.3: ln = s1 - u
                zt = top(u + ln / 2) if top else z1
                if z + h <= zt + 0.02: pillow(pc, u, u + ln, z, z + h, R)
                u += ln; first = False
        z += h; ci += 1

def backing(pc, u0, u1, z0, z1, top=None, holes=()):
    cuts = sorted(set([u0, u1] + [h[0] for h in holes if u0 < h[0] < u1] + [h[1] for h in holes if u0 < h[1] < u1]))
    us = []
    for a, b in zip(cuts, cuts[1:]):
        n = max(1, int((b - a) / 0.8 + 0.99)); us += [a + (b - a) * k / n for k in range(n)]
    us.append(cuts[-1])
    for a, b in zip(us, us[1:]):
        mid = (a + b) / 2; zt = min(z1, top(mid)) if top else z1
        segs = [(z0, zt)]
        for h in holes:
            if h[0] < mid < h[1]:
                new = []
                for s0, s1 in segs:
                    if h[3] <= s0 or h[2] >= s1: new.append((s0, s1)); continue
                    if h[2] > s0: new.append((s0, h[2]))
                    if h[3] < s1: new.append((h[3], s1))
                segs = new
        for s0, s1 in segs:
            if s1 - s0 > 0.02:
                flat(pc, 'mortar', [(a, 0.04, s0), (b, 0.04, s0), (b, 0.04, s1), (a, 0.04, s1)], (0, -1, 0))
                if top and zt < z1 - 0.01 and s1 >= zt - 1e-6:     # broken top: cap across the wall thickness
                    flat(pc, 'stone_dk', [(a, 0.04, zt), (b, 0.04, zt), (b, T, zt), (a, T, zt)], (0, 0, 1))

def opening(pc, kind, u0, u1, z0, z1, ruin):
    for (a, b, f) in (((u0, 0, z0), (u0, T, z1), (1, 0, 0)), ((u1, 0, z0), (u1, T, z1), (-1, 0, 0))):
        flat(pc, 'stone', [(a[0], 0, z0), (a[0], T, z0), (a[0], T, z1), (a[0], 0, z1)], f)
    flat(pc, 'stone', [(u0, 0, z1), (u1, 0, z1), (u1, T, z1), (u0, T, z1)], (0, 0, -1))
    if kind == 'win':
        flat(pc, 'stone', [(u0, 0, z0), (u1, 0, z0), (u1, T, z0), (u0, T, z0)], (0, 0, 1))
        FK.add_box(pc, 'stone', (u1 - u0 + 0.5, 0.26, 0.16), Matrix.Translation(((u0 + u1) / 2, -0.07, z0 - 0.08)), cham=0.02, uv_world=True, seed=u0)
        FK.add_box(pc, 'stone', (u1 - u0 + 0.5, 0.26, 0.24), Matrix.Translation(((u0 + u1) / 2, -0.07, z1 + 0.12)), cham=0.02, uv_world=True, seed=u1)
        if not ruin:
            flat(pc, 'glass', [(u0, 0.38, z0), (u1, 0.38, z0), (u1, 0.38, z1), (u0, 0.38, z1)], (0, -1, 0))
            for k in range(1, 4):
                uu = u0 + (u1 - u0) * k / 4
                FK.add_box(pc, 'iron', (0.03, 0.03, z1 - z0), Matrix.Translation((uu, 0.0, (z0 + z1) / 2)), cham=0.0, seed=k + u0)
    else:   # door: stone step and lintel; dark room behind in the restored state
        FK.add_box(pc, 'stone', (u1 - u0 + 0.5, 0.6, z0), Matrix.Translation(((u0 + u1) / 2, -0.25, z0 / 2)), cham=0.03, uv_world=True, seed=u0)
        FK.add_box(pc, 'stone', (u1 - u0 + 0.7, 0.26, 0.3), Matrix.Translation(((u0 + u1) / 2, -0.07, z1 + 0.15)), cham=0.02, uv_world=True, seed=3.3)
        if not ruin:
            yb = T + 1.3; zt = z1 + 0.3
            flat(pc, 'interior', [(u0 - 0.5, yb, z0), (u1 + 0.5, yb, z0), (u1 + 0.5, yb, zt), (u0 - 0.5, yb, zt)], (0, -1, 0))
            flat(pc, 'oak', [(u0, T, z0), (u1, T, z0), (u1, yb, z0), (u0, yb, z0)], (0, 0, 1), 2, 3)
            flat(pc, 'interior', [(u0, T, z0), (u0 - 0.5, yb, z0), (u0 - 0.5, yb, zt), (u0, T, z1)], (1, 0, 0))
            flat(pc, 'interior', [(u1 + 0.5, yb, z0), (u1, T, z0), (u1, T, z1), (u1 + 0.5, yb, zt)], (-1, 0, 0))
            flat(pc, 'interior', [(u0, T, z1), (u0 - 0.5, yb, zt), (u1 + 0.5, yb, zt), (u1, T, z1)], (0, 0, -1))

def ground_wall(tag, F, L, spans, opens, topf, ruin):
    out = []
    holes = [(o[1], o[2], o[3], o[4]) for o in opens]
    pc = piece(tag)
    for k, (a, b) in enumerate(spans):
        stone_wall(pc, a, b, -0.15, GZ, topf, holes, seed=tag + str(k))
        backing(pc, a, b, -0.15, GZ, topf, holes)
        if not ruin:
            FK.add_box(pc, 'stone', (b - a + 0.1, 0.24, 0.2), Matrix.Translation(((a + b) / 2, -0.05, GZ - 0.02)), cham=0.025, uv_world=True, seed=a + 7)
    for o in opens:
        opening(pc, o[0], o[1], o[2], o[3], o[4], ruin)
    out.append((pc, F))
    if ruin:   # inner face: the roofless shell shows it
        o_in = F @ Vector((L, T, 0)); n_in = -(F.to_3x3() @ Vector((0, -1, 0)))
        Fi = face_xf(o_in, n_in)
        pi = piece(tag + 'In')
        ih = [(L - h[1], L - h[0], h[2], h[3]) for h in holes]
        tf = (lambda u: topf(L - u))
        for k, (a, b) in enumerate(spans):
            stone_wall(pi, L - b, L - a, -0.15, GZ, tf, ih, seed=tag + 'i' + str(k))
            backing(pi, L - b, L - a, -0.15, GZ, tf, ih)
        out.append((pi, Fi))
    return out

# ----------------------------------------------------------------------------- upper storey and roof
F_FRONT = face_xf((X0, Y0, 0), (0, -1, 0)); F_RIGHT = face_xf((X1, Y0, 0), (1, 0, 0))
F_BACK = face_xf((X1, Y1, 0), (0, 1, 0)); F_LEFT = face_xf((X0, Y1, 0), (-1, 0, 0))

def mem(pc, a, b, w=TIM, d=TIM, proud=FK.PROUD, name='m', **kw):
    FK.member(pc, kw.pop('mat', 'oak'), (a[0], d / 2 - proud, a[1]), (b[0], d / 2 - proud, b[1]), w, d, name=name, **kw)

def upper_wall(tag, L, wins, ruin):
    """Timber frame from the stone string course to the plate; ruin: broken posts, torn plaster, empty window frames."""
    pc = piece(tag); R = FK.rng('mlup' + tag)
    za, zb = GZ, PL
    posts = [TIM / 2]
    for w in wins: posts += [w[0] - TIM / 2, w[1] + TIM / 2]
    posts.append(L - TIM / 2)
    posts = sorted(posts)
    holes = [(w[0], w[1], w[2], w[3]) for w in wins]
    if not ruin:
        n0 = len(pc.bm.faces); FK.wall_panel(pc, 'plaster', 0.0, 0.0, L, za, zb, holes=holes, bulge=0.014, cell=0.7); orient(pc, n0, Vector((0, -1, 0)))
    for i, u in enumerate(posts):
        if not ruin: mem(pc, (u, za), (u, zb), name=tag + 'p%d' % i)
        elif R.random() < 0.8:
            h = zb if R.random() < 0.25 else R.uniform(za + 0.6, zb - 0.4)
            mem(pc, (u, za), (u, h), name=tag + 'p%d' % i)
            if h < zb: mem(pc, (u + 0.05, h), (u + 0.18, h + 0.35), w=0.12, d=0.12, name=tag + 's%d' % i, mat='oak')
    for j, (zc, h) in enumerate(((za + 0.14, 0.26), (6.9, 0.22), (zb - 0.13, 0.28))):
        for k, (s0, s1) in enumerate(zip(posts, posts[1:])):
            if ruin and R.random() < (0.15 if j == 0 else 0.55): continue
            mem(pc, (s0, zc), (s1, zc), w=h, proud=FK.PROUD + 0.02, name='%sr%d%d' % (tag, j, k), sag=0.01 if s1 - s0 > 2 else 0.0)
    for k, (s0, s1) in enumerate(zip(posts, posts[1:])):
        if any(w[0] - 0.2 < (s0 + s1) / 2 < w[1] + 0.2 for w in wins): continue
        if ruin and R.random() < 0.6: continue
        a, b = (s0, za + 0.25), (s1, 6.8)
        if k % 2: a, b = (s1, za + 0.25), (s0, 6.8)
        mem(pc, a, b, w=0.22, proud=FK.PROUD - 0.01, name='%sb%d' % (tag, k))
    if ruin:   # a few torn plaster bays
        for k, (s0, s1) in enumerate(zip(posts, posts[1:])):
            if R.random() < 0.45 and not any(w[0] - 0.2 < (s0 + s1) / 2 < w[1] + 0.2 for w in wins):
                n0 = len(pc.bm.faces); FK.wall_panel(pc, 'plaster', 0.0, s0, s1, za, R.uniform(za + 1.2, zb - 0.3), bulge=0.01, cell=0.6); orient(pc, n0, Vector((0, -1, 0))); back_skin(pc, n0)
    for k, w in enumerate(wins):
        u0, u1, z0, z1 = w[:4]
        if ruin:
            c = 0.09
            for nm, a, b in (('l', (u0 + c / 2, z0), (u0 + c / 2, z1)), ('r', (u1 - c / 2, z0), (u1 - c / 2, z1)), ('t', (u0, z1 - c / 2), (u1, z1 - c / 2))):
                if R.random() < 0.7: mem(pc, a, b, w=c, d=0.12, proud=-0.06, name='%sw%d%s' % (tag, k, nm))
        else:
            c = 0.09
            for nm, a, b in (('l', (u0 + c / 2, z0), (u0 + c / 2, z1)), ('r', (u1 - c / 2, z0), (u1 - c / 2, z1)), ('t', (u0, z1 - c / 2), (u1, z1 - c / 2))):
                mem(pc, a, b, w=c, d=0.12, proud=-0.06, name='%sw%d%s' % (tag, k, nm))
            flat(pc, 'glass', [(u0, 0.24, z0), (u1, 0.24, z0), (u1, 0.24, z1), (u0, 0.24, z1)], (0, -1, 0))
            for (a, b, f) in (((u0, 0, z0), (u0, 0.24, z1), (1, 0, 0)), ((u1, 0, z0), (u1, 0.24, z1), (-1, 0, 0))):
                flat(pc, 'oak', [(a[0], 0, z0), (a[0], 0.24, z0), (a[0], 0.24, z1), (a[0], 0, z1)], f)
            flat(pc, 'oak', [(u0, 0, z1), (u1, 0, z1), (u1, 0.24, z1), (u0, 0.24, z1)], (0, 0, -1))
            flat(pc, 'oak', [(u0, 0, z0), (u1, 0, z0), (u1, 0.24, z0), (u0, 0.24, z0)], (0, 0, 1))
            xm = (u0 + u1) / 2
            mem(pc, (xm, z0), (xm, z1), w=0.06, d=0.09, proud=-0.1, name='%swm%d' % (tag, k))
            FK.add_box(pc, 'oak', (u1 - u0 + 0.22, 0.2, 0.07), Matrix.Translation((xm, -0.04, z0 - 0.02)), cham=0.015, long_axis=0, seed=k + 0.5)
    for k, w in enumerate(wins):
        shutters(pc, w, k, tag, ruin)
    return pc

def shutters(pc, w, k, tag, ruin):
    """Green-painted leaves hung open against the wall either side of an upper window (ruin: one left, hanging off a hinge)."""
    R = FK.rng('mlsh' + tag + str(k)); u0, u1, z0, z1 = w[:4]; lw = (u1 - u0) / 2 + 0.03; h = z1 - z0 + 0.1; zc = (z0 + z1) / 2
    for side in (-1, 1):
        if ruin and (side > 0 or k % 2):
            continue
        hx = u0 - 0.03 if side < 0 else u1 + 0.03
        ang = math.radians(R.uniform(8, 18) if not ruin else R.uniform(50, 62))
        hinge = Matrix.Translation((hx, -0.11, zc)) @ Matrix.Rotation(-side * ang, 4, 'Z')
        if ruin: hinge = hinge @ Matrix.Rotation(math.radians(R.uniform(6, 12)), 4, 'X')
        leaf = Matrix.Translation((side * lw / 2, 0, 0))
        FK.add_box(pc, 'door', (lw, 0.045, h), hinge @ leaf, cham=0.012, cuts=(0, 0, 2), long_axis=2, wob=0.004, seed=R.random() * 90)
        for dz in (-h * 0.32, h * 0.32):
            FK.add_box(pc, 'oak', (lw * 0.96, 0.03, 0.09), hinge @ leaf @ Matrix.Translation((0, -0.036, dz)), cham=0.008, long_axis=0, seed=R.random() * 90)

def slope_pt(side, x, t, ruin=False):
    xr = X1 + OV_V
    sag = 0.2 * (1 - (x / xr) ** 2)
    ye = side * (HWY + OV_E); ze = PL + 0.24 - OV_E * TANP
    zr = RIDGE + 0.24 - sag
    kick = 0.30 * max(0.0, 1 - t / 0.2) ** 2
    return Vector((x, ye * (1 - t), ze + (zr - ze) * t + kick))

def roof_range(side, t, ruin):
    xa, xb = X0 - OV_V, X1 + OV_V
    if ruin:
        base = 1.4 if side < 0 else -0.2
        xa = base + 0.9 * noise.noise(Vector((t * 3.0, side * 2.0, 0.7)))
    return xa, xb

def tile_courses(pc, edge, n, nc, tag, flip, drop=None):
    for c in range(nc):
        R = FK.rng('mlcourse%s%d' % (tag, c))
        t0, t1 = c / nc, min(1.0, (c + 1) / nc + 0.03)
        a0, b0 = edge(t0); a1, b1 = edge(t1)
        L = (b0 - a0).length; nt = max(2, int(L / 0.4))
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        for i in range(nt):
            if drop and drop(c, i, nt): continue
            u0, u1 = i / nt, (i + 1) / nt
            j = R.uniform(-0.012, 0.012) if c else R.uniform(-0.008, 0.04)
            pts = []
            for u in (u0 + 0.002, u1 - 0.002):
                pb = a0.lerp(b0, u) + n * (0.045 + j); pt = a1.lerp(b1, u)
                pts.append((bm.verts.new(pb - n * 0.05), bm.verts.new(pb), bm.verts.new(pt)))
            a, b = pts
            for qi, q in enumerate(((a[1], b[1], b[2], a[2]), (a[0], b[0], b[1], a[1]))):
                f = bm.faces.new(q)
                f.normal_update()
                want = n if qi == 0 else (a[1].co - a[2].co)       # top faces up out of the roof, risers down-slope toward the eave
                if f.normal.dot(want) < 0: f.normal_flip()
                for l in f.loops:
                    co = l.vert.co
                    l[uvl].uv = ((co - a0).dot((b0 - a0).normalized()) / TILE, (c * 0.37 + (co - a0).dot((a1 - a0).normalized())) / TILE)
        bm.normal_update(); FK._merge(pc, bm, 'roof')

def roof(ruin):
    pc = piece('Roof'); TH = 0.2
    for side in (1, -1):
        n = Vector((0, side * TANP, 1)).normalized()
        def deck(p, u, v, side=side):
            xa, xb = roof_range(side, v, ruin)
            return slope_pt(side, xa + (xb - xa) * u, v) - n * 0.06
        def under(p, u, v, side=side):
            xa, xb = roof_range(side, v, ruin)
            return slope_pt(side, xa + (xb - xa) * u, v) - n * TH
        flip = side > 0
        g = lambda p, u, v: (p.x / TILE, p.y / TILE)
        n0 = len(pc.bm.faces); FK.quad_grid(pc, 'roof' if not ruin else 'oakw', [(0, 0, 0)] * 4, 20, 8, push=deck, uv_fn=lambda c: (c.x / TILE, c.y / TILE)); orient(pc, n0, n)
        n0 = len(pc.bm.faces); FK.quad_grid(pc, 'oak', [(0, 0, 0)] * 4, 20, 4, push=under, uv_fn=lambda c: (c.x / TILE, c.y / TILE)); orient(pc, n0, -n)
        if ruin:   # tile courses with dropouts: bare sarking shows through
            drop = lambda c, i, nt: FK.rng('mld%d%d%d' % (side, c, i)).random() < (0.25 + 0.08 * c / 15)
        else:
            drop = None
        n0 = len(pc.bm.faces); tile_courses(pc, lambda t, side=side: tuple(slope_pt(side, x, t) for x in roof_range(side, t, ruin)), n, 15, 'mill%d' % side, flip=flip, drop=drop)
        xa, xb = roof_range(side, 0, ruin)
        FK.member(pc, 'oak', slope_pt(side, xa, 0) - n * 0.13, slope_pt(side, xb, 0) - n * 0.13, 0.2, 0.07, name='fascia%d' % side, up=(0, side, 0.4))
        if ruin:   # bare rafters in the roofless part
            for k in range(10):
                x = X0 + 0.4 + k * 0.9
                if x > roof_range(side, 0.5, True)[0] and k > 3: continue
                tt = FK.rng('mlraft%d%d' % (side, k)).uniform(0.45, 1.0)
                FK.member(pc, 'oakw', slope_pt(side, x, 0.0) - n * 0.2, slope_pt(side, x, tt) - n * 0.2, 0.13, 0.17, name='raft%d%d' % (side, k), up=tuple(-n))
    ridge_tiles(pc, X0 - OV_V + (3.0 if ruin else 0), X1 + OV_V)
    for side in (1, -1):
        n = Vector((0, side * TANP, 1)).normalized()
        rolled_eave(pc, side, roof_range(side, 0, ruin)[0], X1 + OV_V)
        for xe in ((X1 + OV_V,) if ruin else (X0 - OV_V, X1 + OV_V)):
            FK.member(pc, 'oak', slope_pt(side, xe, 0) - n * 0.12, slope_pt(side, xe, 1) - n * 0.12, 0.3, 0.07, name='barge%d%d' % (side, xe), up=(1, 0, 0))
    return pc

def orient_bm(bm, expect):
    bm.normal_update()
    bad = [f for f in bm.faces if f.normal.dot(expect(f.calc_center_median())) < 0]
    if bad: bmesh.ops.reverse_faces(bm, faces=bad)

def ridge_tiles(pc, xa, xb):
    xr = X1 + OV_V; n = max(2, int((xb - xa) / 0.42))
    for i in range(n):
        R = FK.rng('mlrt%d' % i)
        x = xa + (xb - xa) * (i + 0.5) / n
        zc = RIDGE + 0.24 - 0.2 * (1 - (x / xr) ** 2) - 0.02
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); segs = 8; Lt = (xb - xa) / n + 0.05; rows = []
        for e in (-1, 1):
            rr = 0.17 * (1.0 if e < 0 else 1.06)
            rows.append([bm.verts.new((x + e * Lt / 2 + R.uniform(-0.01, 0.01), -rr * math.cos(math.pi * k / segs), zc + rr * math.sin(math.pi * k / segs) * 0.85 - 0.05)) for k in range(segs + 1)])
        for k in range(segs):
            f = bm.faces.new((rows[0][k], rows[1][k], rows[1][k + 1], rows[0][k + 1]))
            for l in f.loops: l[uvl].uv = (l.vert.co.x / TILE, k * 0.07 / TILE)
        orient_bm(bm, lambda c, zc=zc: Vector((0, c.y, c.z - (zc - 0.05))))
        FK.two_sided(bm); FK._merge(pc, bm, 'roof')

def rolled_eave(pc, side, xa, xb):
    segs = 6; rr = 0.11; rows = []; ny = max(2, int((xb - (X0 - OV_V)) / 0.5))
    xa = max(xa, X0 - OV_V)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); ny = max(2, int((xb - xa) / 0.5))
    for i in range(ny + 1):
        x = xa + (xb - xa) * i / ny
        c = slope_pt(side, x, 0) + Vector((0, -side * 0.02, -0.03))
        rows.append([bm.verts.new((x, c.y + side * math.cos(math.pi * 0.15 + math.pi * 1.25 * k / segs) * rr, c.z + math.sin(math.pi * 0.15 + math.pi * 1.25 * k / segs) * rr)) for k in range(segs + 1)])
    for i in range(ny):
        for k in range(segs):
            f = bm.faces.new((rows[i][k], rows[i + 1][k], rows[i + 1][k + 1], rows[i][k + 1]))
            for l in f.loops: l[uvl].uv = (l.vert.co.x / TILE, k * rr / TILE)
    cz = slope_pt(side, (xa + xb) / 2, 0).z - 0.03; cy = slope_pt(side, (xa + xb) / 2, 0).y - side * 0.02
    orient_bm(bm, lambda c: Vector((0, c.y - cy, c.z - cz)))
    FK.two_sided(bm); FK._merge(pc, bm, 'roof')

def hood(pc, tag):
    """Hoist hood: a small boarded gable over the loft door and its beam, knee-braced off the wall."""
    rz = 11.5; pa = math.radians(36); L = 1.05; zy = -0.47
    for sd in (-1, 1):
        c = Vector((HWY + sd * 0.5 * L * math.cos(pa) * 0.95, zy, rz - 0.5 * L * math.sin(pa) * 0.95 - 0.02))
        FK.add_box(pc, 'roof', (L, 0.8, 0.07), Matrix.Translation(c) @ Matrix.Rotation(sd * pa, 4, 'Y'), cham=0.012, long_axis=0, seed=sd + 9.0)
        FK.add_box(pc, 'oak', (0.1, 0.84, 0.1), Matrix.Translation(c + Vector((sd * 0.2, 0, -0.17))) @ Matrix.Rotation(sd * pa, 4, 'Y'), cham=0.01, long_axis=1, seed=sd + 9.5)
        FK.member(pc, 'oak', (HWY + sd * 0.95, -0.12, 10.55), (HWY + sd * 0.6, -0.78, 10.95), 0.1, 0.1, name=tag + 'knee%d' % sd, up=(0, 0, 1))
    FK.add_box(pc, 'oak', (0.12, 0.82, 0.1), Matrix.Translation((HWY, zy, rz + 0.02)), cham=0.015, long_axis=1, seed=9.9)

def gable(tag, F, ruin, loft):
    pc = piece(tag); L = 7.0; R = FK.rng('mlg' + tag)
    top = lambda u: min(RIDGE, PL + (HWY - abs(u - HWY)) * TANP) - 0.04
    cols = 14
    for i in range(cols):
        a, b = L * i / cols, L * (i + 1) / cols
        zt = top((a + b) / 2)
        if ruin:
            if R.random() < 0.55: continue
            zt = min(zt, R.uniform(PL + 0.3, PL + 2.8))
        n0 = len(pc.bm.faces); FK.quad_grid(pc, 'plaster', [(a, 0, PL), (b, 0, PL), (b, 0, zt), (a, 0, zt)], 1, 3, uv_fn=lambda c: (c.x / TILE, c.z / TILE)); orient(pc, n0, Vector((0, -1, 0)))
        if ruin: back_skin(pc, n0)
    mem(pc, (0, PL + 0.12), (L, PL + 0.12), w=0.24, proud=FK.PROUD + 0.02, name=tag + 'tie')
    h = RIDGE - 0.4 if not ruin else PL + 1.6
    if loft:   # the king post stops short of the loft door and the hood
        mem(pc, (HWY, PL + 0.2), (HWY, 8.85), name=tag + 'king'); mem(pc, (HWY, 10.55), (HWY, h), name=tag + 'kingu')
    else:
        mem(pc, (HWY, PL + 0.2), (HWY, h), name=tag + 'king')
    if loft:
        lo = (HWY - 0.5, HWY + 0.5, 9.0, 10.3)
        for u in (lo[0] - 0.1, lo[1] + 0.1): mem(pc, (u, lo[2] - 0.1), (u, lo[3] + 0.1), w=0.14, name=tag + 'lp%f' % u)
        mem(pc, (lo[0] - 0.2, lo[3] + 0.1), (lo[1] + 0.2, lo[3] + 0.1), w=0.2, name=tag + 'lh')
        if not ruin:
            for i in range(4):
                pw = (lo[1] - lo[0]) / 4
                FK.add_box(pc, 'door', (pw - 0.01, 0.05, lo[3] - lo[2]), Matrix.Translation((lo[0] + pw * (i + 0.5), -0.03, (lo[2] + lo[3]) / 2)), cham=0.01, long_axis=2, seed=i)
            FK.add_box(pc, 'oak', (0.24, 0.76, 0.26), Matrix.Translation((HWY, -0.4, 10.75)), cham=0.03, cuts=(0, 2, 0), long_axis=1, seed=6.1)
            hood(pc, tag)
        else:
            FK.add_box(pc, 'oak', (0.24, 0.4, 0.26), Matrix.Translation((HWY, -0.15, 10.75)), cham=0.03, long_axis=1, seed=6.2)
    return pc

# ----------------------------------------------------------------------------- wheel pit, flume
def cheek_top(x):
    if x < 4.5: return GZ
    for a, b, h in STEPS:
        if a <= x < b: return h
    return 1.15

def pit(ruin):
    pc = piece('Pit'); R = FK.rng('mlpit')
    flat(pc, 'stone', [(XN, -PW, PIT_Z), (8.0, -PW, PIT_Z), (8.0, PW, PIT_Z), (XN, PW, PIT_Z)], (0, 0, 1), 4, 2)
    # back wall of the notch, soffit over it, oak lintel beam
    Fb = face_xf((XN, -PW, 0), (1, 0, 0)); pb = piece('PitBack')
    stone_wall(pb, 0, 2 * PW, PIT_Z, GZ, None, [(0.5, 1.4, PIT_Z - 0.1, 0.4)], seed='pitback')
    backing(pb, 0, 2 * PW, PIT_Z, GZ, None, [(0.5, 1.4, PIT_Z - 0.1, 0.4)])
    flat(pb, 'interior', [(0.5, 0.5, PIT_Z), (1.4, 0.5, PIT_Z), (1.4, 0.5, 0.4), (0.5, 0.5, 0.4)], (0, -1, 0))
    merge(pc, pb, Fb)
    flat(pc, 'oakw', [(XN, -PW, GZ), (X1, -PW, GZ), (X1, PW, GZ), (XN, PW, GZ)], (0, 0, -1), 2, 2)
    FK.add_box(pc, 'oakw', (0.5, 2 * PW + 0.7, 0.5), Matrix.Translation((X1 - 0.3, 0, GZ - 0.25)), cham=0.03, cuts=(0, 2, 0), long_axis=1, seed=4.4)
    for s in (-1, 1):
        broken = ruin and s > 0
        topf = lambda x, broken=broken, s=s: cheek_top(x) - ((1.6 * max(0.0, noise.noise(Vector((x * 1.3, 2.0, 0.3))) * 2 + 0.6)) if broken and x > 4.4 else 0.0)
        # inner face: x XN..7.3, outward = toward the pit centre
        t = Vector((s, 0, 0))
        Fi = face_xf((XN if s > 0 else 7.3, s * PW, 0), (0, -s, 0))
        p1 = piece('CheekIn')
        tfun = (lambda u, s=s, topf=topf: topf(XN + u if s > 0 else 7.3 - u))
        stone_wall(p1, 0, 7.3 - XN, PIT_Z, GZ, tfun, (), seed='ci%d' % s)
        backing(p1, 0, 7.3 - XN, PIT_Z, GZ, tfun, ())
        merge(pc, p1, Fi)
        Fo = face_xf((7.3 if s > 0 else 4.5, s * (PW + CT), 0), (0, s, 0))
        p2 = piece('CheekOut')
        tfo = (lambda u, s=s, topf=topf: topf(7.3 - u if s > 0 else 4.5 + u))
        stone_wall(p2, 0, 7.3 - 4.5, PIT_Z, GZ, tfo, (), seed='co%d' % s)
        backing(p2, 0, 7.3 - 4.5, PIT_Z, GZ, tfo, ())
        merge(pc, p2, Fo)
        for a, b, h in STEPS:   # solid core and coping per step
            hh = h - (0.0 if not broken else 1.0 if h > 2 else 0.4)
            FK.add_box(pc, 'rubble', (b - a, CT, hh - PIT_Z), Matrix.Translation(((a + b) / 2, s * (PW + CT / 2), (hh + PIT_Z) / 2)), cham=0.0, uv_world=True, seed=a)
            FK.add_box(pc, 'stone', (b - a + 0.06, CT + 0.12, 0.2), Matrix.Translation(((a + b) / 2, s * (PW + CT / 2), hh + 0.1)), cham=0.03, uv_world=True, seed=a + s)
        if not ruin:   # pier carrying the flume's upstream end, and the bearing block holding the axle
            FK.add_box(pc, 'rubble', (0.7, CT, 4.05 - PIT_Z), Matrix.Translation((7.6, s * (PW + CT / 2), (4.05 + PIT_Z) / 2)), cham=0.02, uv_world=True, seed=7.7 + s)
            FK.add_box(pc, 'stone', (0.76, CT + 0.12, 0.2), Matrix.Translation((7.6, s * (PW + CT / 2), 4.15)), cham=0.03, uv_world=True, seed=8 + s)
            FK.add_box(pc, 'oakw', (0.6, 0.44, 0.45), Matrix.Translation((AXLE.x, s * (PW + CT / 2), 1.55 + 0.325 + 0.0)), cham=0.03, seed=s)
            FK.add_box(pc, 'iron', (0.64, 0.48, 0.05), Matrix.Translation((AXLE.x, s * (PW + CT / 2), 2.15)), cham=0.0, seed=s + 1)
    return pc

def flume(ruin):
    pc = piece('Flume')
    x_a, x_b = (5.95, 7.95) if not ruin else (7.0, 7.95)
    z_a, z_b = 4.2, 4.38
    ang = math.atan2(z_b - z_a, x_b - x_a); ln = (x_b - x_a) / math.cos(ang); cx = (x_a + x_b) / 2; cz = (z_a + z_b) / 2
    base = Matrix.Translation((cx, 0, cz)) @ Matrix.Rotation(-ang, 4, 'Y')
    FK.add_box(pc, 'oakw', (ln, 1.4, 0.08), base, cham=0.01, long_axis=0, seed=1.1)
    for s in (-1, 1):
        FK.add_box(pc, 'oakw', (ln, 0.1, 0.46), base @ Matrix.Translation((0, s * 0.7, 0.25)), cham=0.012, long_axis=0, seed=2.1 + s)
        FK.add_box(pc, 'oakw', (ln, 0.2, 0.3), Matrix.Translation((cx, s * 1.0, cz - 0.3)) @ Matrix.Rotation(-ang, 4, 'Y'), cham=0.02, long_axis=0, seed=3.1 + s)
    for bx in ((6.25, 6.95) if not ruin else (6.95,)):
        zt = z_a + (z_b - z_a) * (bx - x_a) / (x_b - x_a) - 0.55
        zc = cheek_top(bx) + 0.2
        for s in (-1, 1):
            if ruin and s > 0:
                FK.member(pc, 'oakw', (bx + 0.6, 1.0 * s + 0.3, 0.25), (bx + 0.1, 1.0 * s + 0.1, 1.6), 0.26, 0.26, name='fallen%f' % bx, up=(0, 0, 1))
                continue
            FK.member(pc, 'oakw', (bx, 1.2 * s, zc), (bx, 1.2 * s, zt), 0.26, 0.26, name='bent%f%d' % (bx, s), up=(1, 0, 0))
            FK.member(pc, 'oakw', (bx - 0.9, 1.2 * s, zc + 0.4), (bx, 1.2 * s, zt - 0.5), 0.14, 0.14, name='brace%f%d' % (bx, s), up=(0, 1, 0))
        FK.add_box(pc, 'oakw', (0.3, 3.0, 0.3), Matrix.Translation((bx, 0, zt + 0.15)), cham=0.02, cuts=(0, 2, 0), long_axis=1, seed=bx)
    return pc

def wheel(broken):
    pc = piece('Part_Wheel' if not broken else 'ShellWheel'); R = FK.rng('mlwheel' + str(broken))
    def cyl(r, y0, y1, mat, seg=12):
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=abs(y1 - y0))
        bm.transform(Matrix.Translation((0, (y0 + y1) / 2, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
        bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), 1, (0, 0)); FK._merge(pc, bm, mat)
    cyl(0.17, -1.35, 1.35 if not broken else 0.3, 'wheel', 10)
    for s in (-1, 1):
        cyl(0.2, s * 0.98, s * 1.10, 'iron', 10); cyl(0.42, s * 0.54, s * 0.68, 'wheel')
    NS = 24
    for k in range(NS):
        a = 2 * math.pi * k / NS; nxt = a + 2 * math.pi / NS
        for s in (-1, 1):
            if broken and ((s > 0 and k in (3, 4, 5)) or (s < 0 and k in (11, 12))): continue
            c = Vector((math.cos(a + math.pi / NS) * 1.80, s * 0.74, math.sin(a + math.pi / NS) * 1.80))
            chord = 2 * 1.80 * math.sin(math.pi / NS) + 0.02
            FK.add_box(pc, 'wheel', (chord, 0.14, 0.22), Matrix.Translation(c) @ Matrix.Rotation(-(a + math.pi / NS), 4, 'Y'), cham=0.01, long_axis=0, seed=k + s)
        if broken and R.random() < 0.35: continue
        p0 = Vector((math.cos(a) * 2.12, 0, math.sin(a) * 2.12)); p1 = Vector((math.cos(a) * 1.82, 0, math.sin(a) * 1.82))
        FK.add_box(pc, 'wheel', (0.04, 1.34, 0.34), Matrix.Translation((p0 + p1) / 2) @ Matrix.Rotation(-a, 4, 'Y'), cham=0.0, seed=k)
        q0 = p0; q1 = Vector((math.cos(a + 0.22) * 1.84, 0, math.sin(a + 0.22) * 1.84)); d = q1 - q0
        FK.add_box(pc, 'wheel', (0.035, 1.34, d.length), Matrix.Translation((q0 + q1) / 2) @ Matrix.Rotation(math.atan2(d.x, d.z), 4, 'Y'), cham=0.0, seed=k + 9)
    for k in range(8):
        a = 2 * math.pi * (k + 0.5) / 8
        for s in (-1, 1):
            if broken and R.random() < 0.4: continue
            e = Vector((math.cos(a) * 1.78, s * 0.6, math.sin(a) * 1.78))
            FK.add_box(pc, 'wheel', (1.8, 0.1, 0.17), Matrix.Translation(e / 2 + Vector((0, s * 0.6, 0)) - Vector((0, 0.6 * s * 0.5 * 0, 0))) @ Matrix.Rotation(-a, 4, 'Y'), cham=0.01, long_axis=0, seed=k + s)
    if broken:   # tilt against the cheek and drop on its axle
        bmesh.ops.transform(pc.bm, matrix=Matrix.Translation((0, 0, -0.1)) @ Matrix.Rotation(math.radians(9), 4, 'X'), verts=pc.bm.verts[:])
    return pc

def door_part():
    pc = piece('Part_Door'); u0, u1, z0, z1 = DOOR; n = 5; R = FK.rng('mldoor'); pw = (u1 - u0) / n
    for i in range(n):
        FK.add_box(pc, 'door', (pw - 0.008, 0.06, z1 - z0), Matrix.Translation((u0 + pw * (i + 0.5), 0.30, (z0 + z1) / 2)), cham=0.01, cuts=(0, 0, 3), long_axis=2, wob=0.004, seed=R.random() * 80)
    for zz in (z0 + 0.35, (z0 + z1) / 2, z1 - 0.35):
        FK.add_box(pc, 'iron', (1.0, 0.02, 0.1), Matrix.Translation((u0 + 0.5, 0.26, zz)), cham=0.0, long_axis=0, seed=zz)
    FK.add_box(pc, 'iron', (0.06, 0.05, 0.16), Matrix.Translation((u1 - 0.2, 0.26, 1.2)), cham=0.008, seed=2)
    return pc

def millstones():
    pc = piece('Millstones')
    for k, (x, y, yaw) in enumerate(((-0.25, -3.95, 0.1), (0.42, -3.92, -0.15))):
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=0.65, radius2=0.65, depth=0.22)
        bm.transform(Matrix.Translation((x, y, 0.62)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(math.radians(80), 4, 'X'))
        bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (k * 0.3, 0)); FK._merge(pc, bm, 'millstone')
    return pc

def heaps():
    pc = piece('Heaps'); R = FK.rng('mlheaps')
    for k, (cx, cy, r) in enumerate(((-1.6, -0.5, 1.4), (1.0, 1.2, 1.0), (-3.0, 1.5, 0.9), (-2.4, -4.0, 0.8), (2.3, -4.0, 0.8))):
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
        for v in bm.verts:
            p = v.co; q = p * 2.0 + Vector((k * 3.1, 0, 0))
            v.co = Vector((p.x * r, p.y * r, max(p.z, -0.1) * r * 0.55 * (1 + 0.25 * noise.noise(q))))
        bm.transform(Matrix.Translation((cx, cy, 0.0))); bm.normal_update()
        uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (k * 0.2, 0)); FK._merge(pc, bm, 'earth')
        for j in range(7):
            FK.add_box(pc, 'rubble', (R.uniform(0.25, 0.6), R.uniform(0.25, 0.5), R.uniform(0.2, 0.4)),
                       Matrix.Translation((cx + R.uniform(-r, r) * 0.8, cy + R.uniform(-r, r) * 0.8, R.uniform(0.1, r * 0.5))) @ Matrix.Rotation(R.uniform(0, 3), 4, 'Z') @ Matrix.Rotation(R.uniform(-0.4, 0.4), 4, 'X'), cham=0.04, uv_world=True, wob=0.02, seed=R.random() * 90)
        for j in range(14):
            FK.add_box(pc, 'roof', (0.34, 0.24, 0.03), Matrix.Translation((cx + R.uniform(-r, r) * 0.9, cy + R.uniform(-r, r) * 0.9, R.uniform(0.05, r * 0.45))) @ Matrix.Rotation(R.uniform(0, 3), 4, 'Z') @ Matrix.Rotation(R.uniform(-0.5, 0.5), 4, 'X'), cham=0.0, seed=R.random() * 90)
    for k in range(6):   # fallen timbers
        a = Vector((R.uniform(-3, 2), R.uniform(-2.5, 2.5), 0.4)); d = Vector((R.uniform(-1, 1), R.uniform(-1, 1), 0)).normalized() * R.uniform(1.6, 3.0)
        FK.member(pc, 'oakw', a, a + d + Vector((0, 0, R.uniform(0, 0.8))), 0.22, 0.26, name='fall%d' % k, up=(0, 0, 1))
    flat(pc, 'earth', [(-3.9, -2.9, 0.02), (2.5, -2.9, 0.02), (2.5, 2.9, 0.02), (-3.9, 2.9, 0.02)], (0, 0, 1), 4, 4)
    return pc

# ----------------------------------------------------------------------------- assembly
def spans_openings():
    W = {}
    W['front'] = ([(0, 9)], [('door', DOOR[0], DOOR[1], DOOR[2], DOOR[3]), ('win', 4.4, 5.2, 1.6, 2.6), ('win', 6.6, 7.4, 1.6, 2.6)])
    W['right'] = ([(0, 2.55), (4.45, 7)], [('win', 5.5, 6.3, 1.7, 2.6)])
    W['back'] = ([(0, 9)], [('win', 1.2, 2.0, 1.6, 2.6), ('win', 5.0, 5.8, 1.6, 2.6), ('win', 7.0, 7.8, 1.6, 2.6)])
    W['left'] = ([(0, 7)], [('win', 1.6, 2.4, 1.6, 2.6), ('win', 4.6, 5.4, 1.6, 2.6)])
    return W

UP = {'front': [(1.3, 2.3, 5.2, 6.4), (4.0, 5.0, 5.2, 6.4), (6.7, 7.7, 5.2, 6.4)],
      'right': [(1.4, 2.4, 5.2, 6.4), (4.6, 5.6, 5.2, 6.4)],
      'back': [(1.1, 2.1, 5.2, 6.4), (4.2, 5.2, 5.2, 6.4), (6.7, 7.7, 5.2, 6.4)],
      'left': [(1.4, 2.4, 5.2, 6.4), (4.6, 5.6, 5.2, 6.4)]}
FR = {'front': (F_FRONT, 9.0), 'right': (F_RIGHT, 7.0), 'back': (F_BACK, 9.0), 'left': (F_LEFT, 7.0)}

def build_state(ruin):
    pc = piece('Shell' if ruin else 'Frame'); W = spans_openings()
    for i, (name, (F, L)) in enumerate(FR.items()):
        spans, opens = W[name]
        topf = ragged(i + 1) if ruin else None
        for p, xf in ground_wall('G' + name + ('R' if ruin else ''), F, L, spans, opens, topf, ruin):
            merge(pc, p, xf)
        merge(pc, upper_wall('U' + name + ('R' if ruin else ''), L, UP[name], ruin), F, lean=True)
    merge(pc, gable('GabL' + ('R' if ruin else ''), face_xf((X0, Y1, 0), (-1, 0, 0)) @ Matrix.Translation((0, 0, 0)), ruin, True), F_LEFT, lean=True)
    merge(pc, gable('GabR' + ('R' if ruin else ''), F_RIGHT, ruin, False), F_RIGHT, lean=True)
    merge(pc, roof(ruin), lean=True)
    merge(pc, pit(ruin)); merge(pc, flume(ruin))
    if ruin:
        merge(pc, wheel(True), Matrix.Translation(AXLE)); merge(pc, heaps())
    else:
        merge(pc, millstones())
        FK_yard(pc)
    return pc

def FK_yard(pc):
    y = piece('Yard'); R = FK.rng('mlyard')
    for k in range(3):
        FK.add_box(y, 'stone', (0.9, 0.26, 0.07), Matrix.Translation((-2.2 + R.uniform(-0.1, 0.1), -4.35 - k * 0.22, 0.025)) @ Matrix.Rotation(R.uniform(-0.05, 0.05), 4, 'Z'), cham=0.03, uv_world=True, seed=k + 50)
    merge(pc, y)

def _clamp01(v): return max(0.0, min(1.0, v))

def _streak(p, fx=6.0, fz=0.5):
    """0..1 vertical streak mask: tight across the wall, long in z (stains run down)."""
    return _clamp01((noise.noise(Vector((p.x * fx, p.y * fx, p.z * fz + 3.1))) + 0.1) * 2.2)

STONE_MATS = ('FK_Sandstone', 'ML_Rubble', 'FK_ChimneyStone')

def write_wear(ob):
    me = ob.data; attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER'); mw = ob.matrix_world
    shell = ob.name == 'Shell'
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        mname = me.materials[poly.material_index].name
        stone = mname in STONE_MATS; plaster = mname == 'FK_Plaster'
        c = mw @ poly.center
        # per-face stone value: low-frequency patches plus a medium-frequency per-stone-ish term (R 0.1-0.3)
        sv = 0.08 + 0.28 * _clamp01(0.5 + 1.2 * noise.noise(Vector((c.x * 2.6 + 7, c.y * 2.6, c.z * 3.2))) + 0.6 * noise.noise(c * 0.55 + Vector((1, 4, 2))))
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 1.7)
            r = max(0.0, 1 - p.z / (1.0 + 0.3 * nz)) ** 1.2 * 0.75
            r = max(r, 0.12 * max(0.0, noise.noise(p * 0.5 + Vector((3, 1, 2)))) + (0.25 if shell else 0.0))
            g = 0.0
            if p.x > 4.4 and p.z < 3.0: g = 0.5 + 0.3 * nz            # damp cheek walls and pit
            if n.z > 0.5 and p.z < 0.2: g = max(g, 0.3 + 0.3 * nz)
            if shell: g = max(g, 0.3 * max(0.0, nz + 0.4))
            st = _streak(p)
            wall = abs(n.z) < 0.7
            if p.x > 4.4 and abs(p.y) < 1.6 and p.z < 4.6:             # wheel splash
                r = max(r, (0.25 + 0.45 * st) if stone else 0.4)
            if stone:
                r = max(sv + (0.08 if shell else 0.0) + 0.04 * nz, r if p.z < 0.6 else 0.0)
                if wall:    # damp rising at the wall foot, streaky (R up to 0.8 in the bottom 0.9 m)
                    foot = _clamp01(1 - max(p.z, 0.0) / 1.0) ** 0.55
                    r = max(r, 0.8 * foot * (0.5 + 0.5 * st), 0.3 * _clamp01(1 - p.z / 1.5) * st)
                    if p.x > 3.0 and abs(p.y) < 1.2 and 1.5 < p.z < 4.45 and st > 0.2:   # runoff under the flume outlet
                        r = max(r, 0.65 * st * _clamp01(1 - (4.45 - p.z) / 2.9))
                    if p.x > 3.0 and abs(p.y) < 1.55 and p.z < 3.2:   # cheek walls and pit: dark, streaky
                        r = max(r, 0.35 + 0.4 * st)
                elif p.x > 3.0 and abs(p.y) < 1.7 and p.z < 3.0:      # pit floor and edge
                    r = max(r, 0.45 + 0.25 * st)
            if plaster and wall and p.z > 4.4:
                eave = _clamp01((p.z - 6.85) / 1.15)                  # soot/damp band just below the eaves (R 0.2-0.35)
                r = max(r, (0.2 + 0.15 * st) * eave, 0.14 * st * _clamp01(0.4 + (7.2 - p.z) / 3.0), 0.22 * _clamp01(1 - (p.z - 4.4) / 0.9))
            if stone and ob.name != 'Part_Wheel': r = min(1.0, r * 1.5)    # the grime mix reads weak on this sandstone: gain so the values below show at mid distance
            if plaster: r = min(1.0, r * 1.7)
            if n.z > 0.4 and p.z > 8.0 and p.y > 0.3: g = max(g, 0.28 + 0.3 * nz)   # moss on the shaded roof slope
            if ob.name == 'Part_Wheel': g = 0.16 * max(0.0, nz + 0.6); r = min(r, 0.3)
            b = 0.0
            if ob.name == 'Part_Door': b = 0.4 * max(0.0, 1 - abs(p.z - 1.2) / 0.5)
            attr.data[li].color = (min(1.0, r), min(1.0, g), b, 1.0)

SOFT = {'ML_Rubble', 'ML_Earth', 'ML_Sack'}
def to_object(pc, parent, origin=None):
    if origin is not None:
        bmesh.ops.translate(pc.bm, verts=pc.bm.verts[:], vec=-origin)
    me = bpy.data.meshes.new(pc.name); pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats: me.materials.append(m)
    for poly in me.polygons:
        if me.materials[poly.material_index].name in SOFT: poly.use_smooth = True
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob); ob.parent = parent
    if origin is not None: ob.location = origin
    bpy.context.view_layer.update(); write_wear(ob)
    return ob

def tris(ob): return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def build():
    root = bpy.data.objects.new('Mill', None); bpy.context.scene.collection.objects.link(root)
    shell = to_object(build_state(True), root); frame = to_object(build_state(False), root)
    print('  Shell %d tris (cap 30000), %d slots' % (tris(shell), len(shell.data.materials)))
    objs = [frame]; total = tris(frame)
    for fn, sock, s in ((wheel, 'Wheel', AXLE.copy()), (door_part, 'Door', None)):
        pc = fn(False) if fn is wheel else fn()
        if sock == 'Wheel':
            bmesh.ops.translate(pc.bm, verts=pc.bm.verts[:], vec=AXLE)
        else:
            bmesh.ops.transform(pc.bm, matrix=F_FRONT, verts=pc.bm.verts[:]); s = F_FRONT @ Vector(((DOOR[0] + DOOR[1]) / 2, 0, DOOR[2]))
        ob = to_object(pc, root, origin=s); objs.append(ob); total += tris(ob)
        e = bpy.data.objects.new('SOCKET_' + sock, None); bpy.context.scene.collection.objects.link(e); e.parent = frame; e.location = s
        print('  %-12s %6d tris' % (ob.name, tris(ob)))
    me = bpy.data.meshes.new('FOOTPRINT'); bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation((0, 0, 0.05)) @ Matrix.Diagonal((9.0, 7.0, 0.1, 1))); bm.to_mesh(me); bm.free()
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root; fp.display_type = 'WIRE'; fp.hide_render = True
    print('  Frame %d tris' % tris(frame))
    for o in [shell] + objs:
        xs = [(o.matrix_world @ Vector(c)) for c in o.bound_box]
        print('  bounds %-10s x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f' % (o.name, min(v.x for v in xs), max(v.x for v in xs), min(v.y for v in xs), max(v.y for v in xs), min(v.z for v in xs), max(v.z for v in xs)))
    print('Mill: Shell %d, Frame+parts %d tris (caps 30000 / 45000)' % (tris(shell), total))
    return root, shell, objs

VIEWS_DEF = {
    'tq': (Vector((13.0, -16.0, 5.5)), Vector((1.0, 0, 5.0)), 46),
    'back': (Vector((11.0, 15.0, 5.5)), Vector((0.5, 0, 5.0)), 46),
    'left': (Vector((-15.0, -9.0, 6.0)), Vector((0.0, 0, 5.2)), 44),
    'wheel': (Vector((14.0, -5.0, 3.0)), Vector((5.5, 0, 2.4)), 46),
    'door': (Vector((-1.0, -9.5, 2.0)), Vector((-1.0, -3.5, 1.7)), 44),
    'frame': (Vector((13.0, -16.0, 5.5)), Vector((1.0, 0, 5.0)), 46),
    'shell': (Vector((13.0, -16.0, 5.5)), Vector((1.0, 0, 5.0)), 46),
    'shellb': (Vector((11.0, 15.0, 5.5)), Vector((0.5, 0, 5.0)), 46),
    'shellf': (Vector((0.0, -10.0, 4.0)), Vector((0.0, 0, 3.8)), 60),
    'eave': (Vector((9.0, -9.0, 4.0)), Vector((3.0, -3.5, 7.5)), 50),
    'loft': (Vector((-10.0, -3.0, 9.5)), Vector((-4.5, 0.0, 10.2)), 40),
    'shellc': (Vector((2.0, -12.0, 9.0)), Vector((0.0, 0, 3.5)), 50),
    'shelld': (Vector((-3.0, 13.0, 9.0)), Vector((0.0, 0, 3.0)), 50),
}

def backface_override():
    m = bpy.data.materials.new('BF'); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    g = nt.nodes.new('ShaderNodeNewGeometry'); e1 = nt.nodes.new('ShaderNodeEmission'); e2 = nt.nodes.new('ShaderNodeEmission')
    e1.inputs[0].default_value = (1, 0, 0, 1); e2.inputs[0].default_value = (0.35, 0.35, 0.35, 1)
    mx = nt.nodes.new('ShaderNodeMixShader'); out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(g.outputs['Backfacing'], mx.inputs[0]); nt.links.new(e2.outputs[0], mx.inputs[1]); nt.links.new(e1.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs[0])
    return m

def review(root, shell, objs):
    sc = bpy.context.scene; FK.RRES = RRES; FK.SAMPLES = SAMPLES
    cam = FK.review_scene(root, variant=True)
    bf = '--backface' in argv
    if bf:
        bpy.context.view_layer.material_override = backface_override(); sc.cycles.samples = 6
    fig = bpy.data.objects.get('Figure175')
    if fig is None:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(-2.2, -5.0, 0.875))
        fig = bpy.context.object; fig.name = 'Figure175'
        fm = bpy.data.materials.new('Figure'); fm.use_nodes = True
        fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.18, 0.12, 1)
        fig.data.materials.append(fm)
    for v in VIEWS:
        frm, to, fov = VIEWS_DEF[v]
        isshell = v.startswith('shell')
        shell.hide_render = not isshell
        for o in objs: o.hide_render = isshell
        if v == 'frame': objs[1].hide_render = True; objs[2].hide_render = True
        FK.look(cam, frm, to); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, 'mill_%s%s.png' % (v, '_bf' if bf else ''))
        bpy.ops.render.render(write_still=True)
        print('rendered', v)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, shell, objs = build()
    if '--export' in argv:
        for o in bpy.context.scene.objects: o.select_set(o == root or o.parent in (root, objs[0]))
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Mill.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS',
                                 bake_space_transform=True, object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR', path_mode='STRIP')
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'mill.blend'))
    if '--no-render' not in argv:
        review(root, shell, objs)
