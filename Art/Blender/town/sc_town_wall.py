"""TownWall: Hollowmere's old town wall kit and its two gatehouses (Q8, T2 kit, tiled about 60 times).

A weathered sandstone curtain wall in grey-and-ochre rubble: a battered outer face (12 cm over its height) on a deep
footing (1.5 m below local 0, so uneven ground never shows a gap), 1.2 m thick, a dressed pale string course and a pale
coping band under crenellated merlons on the OUTER parapet (merlon tops 3.4 m above ground), and a timber (variant A) or
stone-flag (variant B) walkway 2.2 m up on the INNER side, carried on a pale corbel course. Both faces are detailed.
Masonry is real geometry: every face stone is its own tilted, slightly proud slab (10 tris) over a dark mortar core, on
the shared B1 tileables with metre UVs (no unique bake); placed wear lives in the linear "wear" colour attribute.

Pieces (each its own FBX, one mesh object named like the piece):
  Wall_Straight_A, Wall_Straight_B   4 m modules, x -2..2 (timber walkway / stone-flag walkway, loopholes in B)
  Wall_Buttress                      outer buttress that clips onto the outer face (-Y), pivot on the wall centre line
  Wall_End                           dressed jamb where a lane or lot opening cuts the run (x -1.6..0, end face at x 0)
  Wall_Stair                         stone stair up to the walkway on the inner side (+Y), rising toward +X
  Wall_Breach, Wall_Crumbled         Ruined variants (gap with rubble spilling both ways; broken top, no merlons)
  Wall_Patched                       Rebuilt variant: paler newer stone patches and a repaired coping
  LowerGate, EastGate                gatehouses between their piers: children Shell, Frame, Part_Door, SOCKET_Door, FOOTPRINT

Axes (Blender): z up, metres. Wall pieces: pivot at the module centre on the wall centre line at local ground 0, run along
+X, the OUTSIDE of the town faces -Y. Gatehouses: pivot at the passage centre at ground, passage axis along Y, outside -Y.
Export FBX -Z forward, Y up, FBX_SCALE_UNITS, bake_space_transform, FACE smoothing, linear colours, path_mode STRIP.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_town_wall.py -- --out <scratch> --export
      [--no-render] [--only wall|gates|<PieceName>] [--rres 1200] [--samples 40] [--figure]
  --export writes <out>/<Piece>/<Piece>.fbx for every piece; --figure adds a 1.75 m scale figure to review renders;
  --showcase renders the ruined and kit shots, --with-walls puts wall stubs beside the gates, --quick renders one view per gate state.
Tris: straight modules 3.4-3.8k, Breach 6.0k, Crumbled 3.7k, Buttress 0.8k, End 2.4k, Stair 1.6k; gate Frame 31.3k / 26.0k,
Shell 18.5k / 20.8k, Part_Door 1.2k. Exports go to Assets/_Project/Content/Art/Town/Wall (see SOURCES.md there).
Status (2026-10-31, cloud): self-review 28.5-30/40 per family (rubric in SOURCES.md). Next visible gains: lichen colour (needs an
orange channel in the wear shader), moss and ivy at the wall foot (B9 foliage), a more broken top on Wall_Crumbled, ivy on the gates.
"""
import sys, os, math, random, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT; argv = FK.argv
TILE = FK.TILE
def R_(s):
    return random.Random(0x7A11 ^ zlib.crc32(s.encode()))

# ----------------------------------------------------------------------------- materials (TW_ slots on the B1 tileables)
EXTRA = {
    'rubble': lambda: FK.tile_mat('TW_Rubble', 'B1_town/sandstone', tint=(0.64, 0.61, 0.57), normal=1.4),
    'ochre': lambda: FK.tile_mat('TW_RubbleOchre', 'B1_town/sandstone', tint=(1.0, 0.89, 0.73), normal=1.4),
    'joint': lambda: FK.tile_mat('TW_Joint', 'B1_town/sandstone', tint=(0.66, 0.61, 0.54), normal=1.0),
    'coping': lambda: FK.tile_mat('TW_Coping', 'B1_town/sandstone', tint=(1.14, 1.09, 1.00), normal=0.9),
    'patch': lambda: FK.tile_mat('TW_Patch', 'B1_town/sandstone', tint=(1.30, 1.16, 0.94), normal=1.0),
    'foot': lambda: FK.tile_mat('TW_Footing', 'B1_town/sandstone', tint=(0.46, 0.43, 0.39), normal=1.2),
    'walk': lambda: FK.tile_mat('TW_Walkway', 'B1_town/oak_timber', tint=(2.7, 2.5, 2.3), normal=1.2),
    'crackdark': lambda: FK.tile_mat('TW_Crack', 'B1_town/sandstone', tint=(0.10, 0.09, 0.08), normal=0.6),
    'crack': lambda: FK.decal_mat('crack'),
}
_orig_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)

# ----------------------------------------------------------------------------- geometry helpers
def newell(P):
    n = Vector()
    for i in range(len(P)):
        a = P[i]; b = P[(i + 1) % len(P)]
        n.x += (a.y - b.y) * (a.z + b.z); n.y += (a.z - b.z) * (a.x + b.x); n.z += (a.x - b.x) * (a.y + b.y)
    return n

def wuv(p, n, off=(0.0, 0.0)):
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 2:
        a, b = p.x, p.y
    elif ax == 1:
        a, b = p.x, p.z
    else:
        a, b = p.y, p.z
    return (a / TILE + off[0], b / TILE + off[1])

def add_face(pc, key, pts, ref=None, off=(0.0, 0.0)):
    """One polygon on the piece; normal points away from `ref` when given. Returns the face (or None if degenerate)."""
    P = []
    for p in pts:
        p = Vector(p)
        if not P or (p - P[-1]).length > 1e-5:
            P.append(p)
    if len(P) > 1 and (P[0] - P[-1]).length <= 1e-5:
        P.pop()
    if len(P) < 3:
        return None
    n = newell(P)
    if n.length < 1e-9:
        return None
    if ref is not None:
        c = sum(P, Vector()) / len(P)
        if n.dot(c - ref) < 0:
            P.reverse(); n = -n
    try:
        f = pc.bm.faces.new([pc.bm.verts.new(p) for p in P])
    except ValueError:
        return None
    f.material_index = pc.slot(key)
    nn = n.normalized()
    for l in f.loops:
        l[pc.uv].uv = wuv(l.vert.co, nn, off)
    return f

def box(pc, key, size, xf, cham=0.0, **kw):
    FK.add_box(pc, key, size, xf, cham=cham, uv_world=kw.pop('uv_world', True), **kw)

def T(x, y, z):
    return Matrix.Translation((x, y, z))

def RZ(a):
    return Matrix.Rotation(a, 4, 'Z')

# ----------------------------------------------------------------------------- stones and masonry
def stone(pc, key, O, U, Vv, N, ua, ub, va, vb, d, R, bat=None, twist=0.014, shape=0.0, jit=0.016, flush=(False, False), bulge=0.018):
    """One face stone: an irregular, slightly twisted and pillowed slab d proud of the core plane, four skirts back to the
    plane (12 tris). Frame (U x V = N): point(u, v, depth) = O + U u + V v + N (bat(v) + depth)."""
    def P(u, v, dd):
        return O + U * u + Vv * v + N * ((bat(v) if bat else 0.0) + dd)
    jl = 0.0 if flush[0] else jit; jr = 0.0 if flush[1] else jit
    cs = [(ua + R.uniform(0, jl), va + R.uniform(0, jit)), (ub - R.uniform(0, jr), va + R.uniform(0, jit)),
          (ub - R.uniform(0, jr), vb - R.uniform(0, jit + shape)), (ua + R.uniform(0, jl), vb - R.uniform(0, jit + shape))]
    front = [P(u, v, max(0.006, d + R.uniform(-twist, twist))) for u, v in cs]
    back = [P(u, v, 0.0) for u, v in cs]
    off = (R.random(), R.random())
    c = sum(front + back, Vector()) / 8
    cen = sum(front, Vector()) / 4 + N * (bulge * R.uniform(0.3, 1.0))
    for i in range(4):
        j = (i + 1) % 4
        add_face(pc, key, [front[i], front[j], cen], ref=c - N * 0.5, off=off)
        add_face(pc, key, [back[i], back[j], front[j], front[i]], ref=c, off=off)

def masonry(pc, O, U, Vv, N, u0, u1, v0, v1, R, key='rubble', vmax=None, skip=None, keyf=None, course=(0.20, 0.37),
            length=(0.28, 0.70), dmax=0.06, joint=0.03, bat=None, shape=0.03, ochre=0.2, flush=True, clip=None):
    """Coursed rubble over the rectangle [u0,u1] x [v0,v1] of a face frame. vmax(u) caps stone tops (broken walls);
    skip(u, v, ua, ub, va, vb) drops a stone; keyf(u, v) overrides the material; flush: the first/last stones of a course
    end exactly at u0/u1 (module seams, no joint)."""
    v = v0; ci = 0
    nseed = R.random() * 80
    while v < v1 - 0.06:
        h = R.uniform(*course)
        if v1 - (v + h) < 0.15:
            h = v1 - v
        va, vb = v + joint / 2, v + h - joint / 2
        u = u0
        while u < u1 - 0.06:
            ln = R.uniform(*length)
            if u == u0 and ci % 2:
                ln = R.uniform(0.2, length[0] + 0.1)
            if u1 - (u + ln) < 0.28:
                ln = u1 - u
            first = u == u0; last = abs(u + ln - u1) < 1e-6
            ua = u if (first and flush) else u + joint / 2
            ub = u + ln if (last and flush) else u + ln - joint / 2
            uc = (ua + ub) / 2
            vt = vb
            if vmax is not None:
                vt = min(vb, vmax(uc) - R.uniform(0.0, 0.07))
            ok = vt - va >= 0.1
            fl_l, fl_r = first and flush, last and flush
            if ok and clip is not None:
                r = clip(ua, ub, va, vt)
                if r is None:
                    ok = False
                else:
                    fl_l = fl_l and r[0] == ua; fl_r = fl_r and r[1] == ub
                    ua, ub = r[0], r[1]; uc = (ua + ub) / 2
            if ok and skip is not None and skip(uc, (va + vt) / 2, ua, ub, va, vt):
                ok = False
            if ok:
                k = keyf(uc, (va + vt) / 2) if keyf else None
                if k is None:
                    k = key
                    if key == 'rubble':   # ochre comes in coherent patches (a different batch of stone), plus a few strays
                        t = noise.noise(Vector((uc * 0.6, (va + vt) * 0.3, nseed)))
                        if (t > 0.1 and R.random() < 0.85) or R.random() < ochre * 0.3:
                            k = 'ochre'
                stone(pc, k, O, U, Vv, N, ua, ub, va, vt, R.uniform(0.014, dmax), R, bat=bat, shape=shape if vt < vb else 0.0,
                      flush=(fl_l, fl_r))
            u += ln
        v += h; ci += 1

# ----------------------------------------------------------------------------- rocks and rubble heaps
_PHI = (1 + 5 ** 0.5) / 2
ICO_V = [(-1, _PHI, 0), (1, _PHI, 0), (-1, -_PHI, 0), (1, -_PHI, 0), (0, -1, _PHI), (0, 1, _PHI), (0, -1, -_PHI),
         (0, 1, -_PHI), (_PHI, 0, -1), (_PHI, 0, 1), (-_PHI, 0, -1), (-_PHI, 0, 1)]
ICO_F = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]

def rock(pc, key, c, size, R, block=False):
    """A low-poly rubble stone (20 tris) or a fallen ashlar block (12 tris) centred at c, half sizes `size`."""
    sx, sy, sz = size
    rot = Matrix.Rotation(R.uniform(0, 6.283), 4, 'Z') @ Matrix.Rotation(R.uniform(-0.5, 0.5), 4, 'X') @ \
        Matrix.Rotation(R.uniform(-0.5, 0.5), 4, 'Y')
    c = Vector(c)
    if block:
        pts = [Vector((a * sx, b * sy, e * sz)) for a in (-1, 1) for b in (-1, 1) for e in (-1, 1)]
        faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    else:
        pts = [Vector(v).normalized() for v in ICO_V]
        pts = [Vector((p.x * sx, p.y * sy, p.z * sz)) for p in pts]
        faces = ICO_F
    pts = [rot @ (p * R.uniform(0.82, 1.12)) + c for p in pts]
    for f in faces:
        add_face(pc, key, [pts[i] for i in f], ref=c, off=(R.random(), R.random()))

def heap(pc, R, cx, cy, rx, ry, hmax, n, zbase=0.0, keys=(('rubble', 0.45), ('ochre', 0.3), ('coping', 0.1), ('foot', 0.15)),
         smin=0.1, smax=0.36, clip=None, blocks=0.25, yaw=0.0):
    ca, sa = math.cos(yaw), math.sin(yaw)
    for _ in range(n):
        r = math.sqrt(R.random()); a = R.uniform(0, 6.283)
        lx, ly = rx * r * math.cos(a), ry * r * math.sin(a)
        x, y = cx + lx * ca - ly * sa, cy + lx * sa + ly * ca
        if clip and not clip(x, y):
            continue
        h = hmax * (1 - r ** 1.6)
        s = R.uniform(smin, smax) * (1.0 + 0.5 * (1 - r))
        z = zbase + h * R.uniform(0.35, 1.0) + s * 0.25
        t = R.random(); acc = 0; k = keys[0][0]
        for kk, w in keys:
            acc += w
            if t <= acc:
                k = kk; break
        rock(pc, k, (x, y, z), (s * R.uniform(0.8, 1.3), s * R.uniform(0.8, 1.3), s * R.uniform(0.55, 0.9)), R,
             block=R.random() < blocks)

# ----------------------------------------------------------------------------- loft (extruded section with a varying profile)
def loft_bm(secs, to3d, key_uv=True):
    """secs: list of (s, [(a, b), ...]) with CCW polygons in (a, b), same length. to3d(s, a, b) -> Vector, a right-handed
    map (a mirrored one is detected and the faces flipped). Shared vertices, so it can feed a boolean."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    cache = {}
    def vert(p):
        k = (round(p.x, 5), round(p.y, 5), round(p.z, 5))
        if k not in cache:
            cache[k] = bm.verts.new(p)
        return cache[k]
    m0 = to3d(0, 0, 0)
    det = (to3d(1, 0, 0) - m0).cross(to3d(0, 1, 0) - m0).dot(to3d(0, 0, 1) - m0)
    flip = det < 0
    def mk(vs):
        u = []
        for v in vs:
            if not u or v is not u[-1]:
                u.append(v)
        if len(u) > 1 and u[0] is u[-1]:
            u.pop()
        if len(u) < 3 or len(set(u)) < 3:
            return
        if flip:
            u.reverse()
        try:
            bm.faces.new(u)
        except ValueError:
            pass
    ring = [[vert(to3d(s, a, b)) for (a, b) in poly] for s, poly in secs]
    n = len(ring[0])
    for i in range(len(ring) - 1):
        for k in range(n):
            k2 = (k + 1) % n
            mk([ring[i][k], ring[i][k2], ring[i + 1][k2], ring[i + 1][k]])
    mk(list(ring[-1]))
    mk(list(reversed(ring[0])))
    bm.normal_update()
    for f in bm.faces:
        nn = f.normal
        for l in f.loops:
            l[uvl].uv = wuv(l.vert.co, nn)
    return bm

def merge_bm(pc, bm, key):
    FK._merge(pc, bm, key)

# ----------------------------------------------------------------------------- the wall module
HALF = 2.0
TH = 0.6                      # half thickness
BODY_TOP = 2.1                # stone top under the walkway (deck 2.1-2.2)
PAR_IN = -0.15                # inner face of the parapet
PAR_TOP = 2.82
COPE0, COPE1 = 2.82, 2.94
MER0, MER1, CAP1 = 2.94, 3.32, 3.40
FOOT_BOT, FOOT_TOP = -1.5, -0.45
def yo(z):
    return -(TH + 0.12 * (1 - z / 3.4))        # outer face plane (battered)
def bat_out(v):
    return TH + 0.12 * (1 - v / 3.4)

class WallSpec:
    def __init__(self, **kw):
        self.name = 'Wall'
        self.x0, self.x1 = -HALF, HALF
        self.ext = (True, True)             # core plug past each end (butting and bend tolerance)
        self.seed = 1
        self.deck = 'plank'                 # plank | slab | None
        self.tb = lambda x: BODY_TOP         # stone body top
        self.tp = lambda x: PAR_TOP          # parapet stone top (== tb where there is no parapet)
        self.merlons = True                  # or callable(i, x) -> bool
        self.coping = True                   # or callable(i, x) -> bool
        self.loops = False
        self.gap = None                      # callable(x) -> True where the walkway boards are missing
        self.patches = []                    # (face, ua, ub, va, vb) face 'out' or 'in'
        self.patched_coping = []             # slab indexes in new stone
        self.heaps = []                      # (cx, cy, rx, ry, hmax, n, zbase)
        self.dressed_end = False
        self.string = True
        self.corbel = True
        self.foot = True
        self.mossy = 0.0
        self.spout = None
        self.__dict__.update(kw)

def core_loft(S):
    n = max(2, int(round((S.x1 - S.x0) / 0.25)))
    xs = [S.x0 + (S.x1 - S.x0) * i / n for i in range(n + 1)]
    secs = []
    def poly(x, inset=0.0, drop=0.0):
        tb = min(BODY_TOP, S.tb(x)) - drop; tp = max(tb, S.tp(x) - drop)
        zb = -0.55
        return [(yo(zb) + inset, zb), (TH, zb), (TH, tb), (PAR_IN, tb), (PAR_IN, tp), (yo(tp) + inset, tp)]
    if S.ext[0]:
        secs.append((S.x0 - 0.2, poly(S.x0, 0.045, 0.03)))
    for x in xs:
        secs.append((x, poly(x)))
    if S.ext[1]:
        secs.append((S.x1 + 0.2, poly(S.x1, 0.045, 0.03)))
    return loft_bm(secs, lambda s, a, b: Vector((s, a, b)))

def wall_pc(S):
    R = R_(S.name + str(S.seed))
    pc = FK.Piece(S.name)
    merge_bm(pc, core_loft(S), 'joint')
    # footing: deep, rough, a little wider than the batter; extends 10 cm past each end
    xa = S.x0 - (0.1 if S.ext[0] else 0.0); xb = S.x1 + (0.1 if S.ext[1] else 0.0)
    if S.foot:
        h = FOOT_TOP - FOOT_BOT
        mid = (xa + xb) / 2
        box(pc, 'foot', (xb - xa, 1.7, h), T(mid, 0.0, FOOT_BOT + h / 2), cham=0.05, wob=0.01, seed=R.random() * 50)
    ex = 0.0
    # --- outer face (normal -Y): U=+X, V=+Z
    O_out = Vector((0, 0, 0)); Uo = Vector((1, 0, 0)); Vz = Vector((0, 0, 1)); No = Vector((0, -1, 0))
    def in_patch(face, u, v, ua=0, ub=0, va=0, vb=0):
        for (f, pa, pb, qa, qb) in S.patches:
            if f == face and pa - 0.05 < u < pb + 0.05 and qa - 0.05 < v < qb + 0.05:
                return True
        return False
    vm_out = lambda u: S.tp(u) - (COPE1 - COPE0 if S.coping and S.tp(u) >= PAR_TOP - 1e-3 else 0.0)
    masonry(pc, O_out, Uo, Vz, No, S.x0, S.x1, -0.5, 3.0, R, vmax=vm_out, bat=bat_out,
            skip=lambda u, v, *a: in_patch('out', u, v), ochre=0.28)
    # --- inner face (normal +Y): U=-X, V=+Z, up to the corbel under the deck
    Ui = Vector((-1, 0, 0)); Ni = Vector((0, 1, 0))
    top_in = lambda u: (S.tb(-u) - (0.14 if S.corbel and S.tb(-u) >= BODY_TOP - 1e-3 else 0.0))
    masonry(pc, Vector((0, 0, 0)), Ui, Vz, Ni, -S.x1, -S.x0, -0.5, 2.2, R, vmax=top_in, bat=lambda v: TH,
            skip=lambda u, v, *a: in_patch('in', u, v), ochre=0.3)
    # --- parapet inner face (normal +Y at y = PAR_IN)
    Op = Vector((0, PAR_IN, 0))
    masonry(pc, Op, Ui, Vz, Ni, -S.x1, -S.x0, BODY_TOP, 3.0, R, vmax=lambda u: S.tp(-u) - (COPE1 - COPE0 if S.coping and S.tp(-u) >= PAR_TOP - 1e-3 else 0.0),
            bat=lambda v: 0.0, ochre=0.3, course=(0.22, 0.34),
            skip=lambda u, v, *a: S.tp(-u) <= S.tb(-u) + 0.12 or v > S.tp(-u) - 0.1 and False)
    # patches: tidy newer courses, a little proud
    for (face, pa, pb, qa, qb) in S.patches:
        if face == 'out':
            masonry(pc, O_out, Uo, Vz, No, pa, pb, qa, qb, R, key='patch', bat=bat_out, course=(0.25, 0.28), length=(0.36, 0.55),
                    dmax=0.075, joint=0.03, ochre=0.0, shape=0.0, flush=False)
        else:
            masonry(pc, Vector((0, 0, 0)), Ui, Vz, Ni, -pb, -pa, qa, qb, R, key='patch', bat=lambda v: TH, course=(0.25, 0.28),
                    length=(0.36, 0.55), dmax=0.075, joint=0.03, ochre=0.0, shape=0.0, flush=False)
    # --- string course (outer, pale) and corbel course (inner, pale)
    if S.string:
        zc = 2.15
        xs = _split(S.x0, S.x1, R, 0.8, 1.4)
        for (a, b) in xs:
            if min(S.tb(a), S.tb(0.5 * (a + b)), S.tb(b)) < BODY_TOP - 0.01:
                continue
            box(pc, 'coping', (b - a - 0.03, 0.2, 0.15), T((a + b) / 2, yo(zc) - 0.02, zc), cham=0.02, wob=0.004, seed=R.random() * 40)
    if S.corbel:
        zc = 2.04
        for (a, b) in _split(S.x0, S.x1, R, 0.7, 1.3):
            if min(S.tb(a), S.tb(0.5 * (a + b)), S.tb(b)) < BODY_TOP - 0.01:
                continue
            box(pc, 'coping', (b - a - 0.03, 0.22, 0.14), T((a + b) / 2, TH + 0.06, zc), cham=0.02, wob=0.004, seed=R.random() * 40)
    # --- coping band and merlons on the outer parapet
    if S.coping:
        slabs = _split(S.x0, S.x1, R, 0.95, 1.05, even=True)
        for i, (a, b) in enumerate(slabs):
            xm = 0.5 * (a + b)
            if not _on(S.coping, i, xm) or min(S.tp(a), S.tp(xm), S.tp(b)) < PAR_TOP - 1e-3:
                continue
            key = 'patch' if i in S.patched_coping else 'coping'
            # overlap trick: both ends overshoot 6 cm into the neighbour; the right-end slab sits 4 mm lower so it never z-fights
            lo = a + 0.006 if (i > 0 or not S.ext[0]) else a - 0.06
            hi = b - 0.006 if (i < len(slabs) - 1 or not S.ext[1]) else b + 0.06
            dz = 0.02 if i in S.patched_coping else 0.0
            box(pc, key, (hi - lo, 0.62, COPE1 - COPE0 + dz), T((lo + hi) / 2, -0.38, (COPE0 + COPE1 + dz) / 2 - (0.004 if i == len(slabs) - 1 else 0.0)),
                cham=0.025, wob=0.004, seed=R.random() * 30)
    if S.spout is not None and S.tp(S.spout) >= PAR_TOP - 1e-3:
        # a stone water spout under the coping on the outer face (rain from the walkway), with a dark mouth
        box(pc, 'coping', (0.2, 0.55, 0.2), T(S.spout, -0.9, 2.52) @ Matrix.Rotation(-0.12, 4, 'X'), cham=0.025, wob=0.003, seed=5.5)
        box(pc, 'foot', (0.1, 0.1, 0.1), T(S.spout, -1.19, 2.47), cham=0.0)
    if S.merlons:
        for i in range(4):
            xm = -1.5 + i
            if not (_on(S.merlons, i, xm) and S.tp(xm) >= PAR_TOP - 1e-3):
                continue
            key = 'patch' if (i in S.patched_coping) else 'rubble'
            ck = 'patch' if (i in S.patched_coping) else 'coping'
            box(pc, key, (0.62, 0.42, MER1 - MER0), T(xm, -0.36, (MER0 + MER1) / 2), cham=0.03, segs=1, wob=0.006, seed=R.random() * 30)
            box(pc, ck, (0.76, 0.58, CAP1 - MER1), T(xm, -0.37, (MER1 + CAP1) / 2), cham=0.025, wob=0.004, seed=R.random() * 30)
            if S.loops and i % 2 == 1:
                for yy, sg in ((yo(3.1) - 0.006, 1), ):
                    box(pc, 'glass', (0.07, 0.03, 0.36), T(xm, -0.575, 3.13), cham=0.0)
    # --- walkway
    if S.deck == 'plank':
        npl = 22; pitch = (HALF * 2) / npl
        for i in range(npl):
            xm = -HALF + pitch * (i + 0.5)
            if S.gap and S.gap(xm):
                continue
            if S.tb(xm) < BODY_TOP - 0.01 or not (S.x0 < xm < S.x1):
                continue
            w = pitch - 0.022
            box(pc, 'walk', (w, 0.87, 0.1), T(xm, 0.285 + R.uniform(-0.004, 0.004), 2.15 + R.uniform(-0.006, 0.008)) @ RZ(R.uniform(-0.012, 0.012)),
                cham=0.0, long_axis=1, uv_world=False, seed=R.random() * 20)
        # bearers: heavy beam ends under the boards poking out of the inner face
        for xm in (-1.5, -0.5, 0.5, 1.5):
            if S.x0 < xm < S.x1 - 0.2 and S.tb(xm) >= BODY_TOP - 0.01 and not (S.gap and S.gap(xm)):
                box(pc, 'walk', (0.2, 0.3, 0.14), T(xm + R.uniform(-0.02, 0.02), TH + 0.1, 2.0), cham=0.02, uv_world=False, long_axis=1, seed=R.random() * 20)
    elif S.deck == 'slab':
        for (a, b) in _split(S.x0, S.x1, R, 1.2, 1.4, even=True):
            if S.tb(0.5 * (a + b)) < BODY_TOP - 0.01 or (S.gap and S.gap(0.5 * (a + b))):
                continue
            box(pc, 'coping', (b - a - 0.04, 0.87, 0.1), T((a + b) / 2, 0.285, 2.15), cham=0.02, wob=0.003, seed=R.random() * 20)
    # --- rubble and spill
    for (cx, cy, rx, ry, hm, nn, zb) in S.heaps:
        heap(pc, R, cx, cy, rx, ry, hm, nn, zbase=zb, clip=lambda x, y: True, smin=0.13, smax=0.46)
    return pc

def _on(spec, i, x):
    return spec(i, x) if callable(spec) else bool(spec)

def _split(a, b, R, lo, hi, even=False):
    """Split [a, b] into segments of length lo..hi (even: equal)."""
    if even:
        n = max(1, int(round((b - a) / ((lo + hi) / 2))))
        return [(a + (b - a) * i / n, a + (b - a) * (i + 1) / n) for i in range(n)]
    out = []; x = a
    while x < b - 1e-6:
        ln = R.uniform(lo, hi)
        if b - (x + ln) < lo * 0.6:
            ln = b - x
        out.append((x, x + ln)); x += ln
    return out

# ----------------------------------------------------------------------------- wear (linear attribute: R grime, G moss, B polish)
def wear(ob, seed=0, grime=1.0, moss=1.0):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    off = Vector((seed * 1.7, seed * 2.3, 0.0))
    mats = [m.name if m else '' for m in me.materials]
    for poly in me.polygons:
        n = poly.normal
        mn = mats[poly.material_index] if poly.material_index < len(mats) else ''
        tv = ((poly.index * 2654435761) % 1009) / 1009.0
        for li in poly.loop_indices:
            p0 = me.vertices[me.loops[li].vertex_index].co
            p = p0 + off; z = p0.z
            nz = noise.noise(p * 1.9)
            streak = noise.noise(Vector((p.x * 5.0, p.y * 5.0, z * 0.6)))
            r = max(0.0, 1 - (z + 0.3) / (1.3 + 0.3 * nz + 0.2 * streak)) * 0.85
            if abs(n.z) < 0.6 and 1.0 < z < 3.2:
                r = max(r, 0.3 * max(0.0, streak + 0.25) * min(1.0, (z - 1.0) / 0.8))
            r = max(r, 0.4 * max(0.0, noise.noise(p * 0.6 + Vector((3.0, 1.0, 2.0)))) + 0.1 * max(0.0, nz))
            if mn in ('TW_Rubble', 'TW_RubbleOchre', 'TW_Footing'):
                r = max(r, 0.4 * tv * tv)
            g = 0.0
            if n.z > 0.5:
                g = min(1.0, 0.55 * max(0.0, 0.3 + nz) + max(0.0, 0.6 - z) * 0.5)
            elif z < 0.8:
                g = 0.3 * max(0.0, nz + 0.1) * (1 - z / 0.8)
            b = 0.0
            if mn == 'TW_Walkway' and n.z > 0.5:
                b = 0.45 + 0.2 * max(0.0, streak)
            if mn == 'TW_Coping' and n.z > 0.8 and ob.name == 'Wall_Stair':
                b = 0.5
            if mn == 'TW_Patch':
                r *= 0.35; g *= 0.3
            attr.data[li].color = (min(1.0, r * grime), min(1.0, g * moss), min(1.0, b), 1.0)

def make_obj(pc, name=None, seed=0, grime=1.25, moss=1.0, decal=False):
    name = name or pc.name
    pc.bm.normal_update()
    me = bpy.data.meshes.new(name); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    wear(ob, seed, grime, moss)
    return ob

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

# ----------------------------------------------------------------------------- wall variants
def n1(x, s):
    return noise.noise(Vector((x * 3.1, s, 0.5)))

def straight_a():
    return WallSpec(name='Wall_Straight_A', seed=1, deck='plank', spout=0.9)

def straight_b():
    return WallSpec(name='Wall_Straight_B', seed=2, deck='slab', loops=True, spout=-0.7)

def patched():
    return WallSpec(name='Wall_Patched', seed=3, deck='plank',
                    patches=[('out', -1.45, -0.35, 0.15, 1.25), ('out', 0.65, 1.65, 1.5, 2.8), ('in', -1.75, -0.85, 0.1, 1.6)],
                    patched_coping=[2], spout=-1.15)

def breach():
    s = 11
    def tb(x):
        ax = abs(x)
        if ax >= 1.2:
            return BODY_TOP
        return max(0.32, 0.32 + (ax - 0.55) * 2.0) + 0.16 * n1(x, s) + (0.12 if x < 0 else 0.0)
    def tp(x):
        return PAR_TOP if abs(x) >= 1.2 else tb(x)
    return WallSpec(name='Wall_Breach', seed=s, deck='plank', tb=tb, tp=tp, merlons=lambda i, x: abs(x) > 1.2,
                    coping=lambda i, x: abs(x) > 1.2,
                    heaps=[(0.1, -1.5, 2.3, 1.5, 1.15, 95, -0.05), (-0.1, 1.5, 2.2, 1.4, 1.0, 80, -0.05),
                           (0.0, 0.0, 1.3, 0.7, 0.8, 45, 0.1)])

def crumbled():
    s = 12
    def tb(x):
        return BODY_TOP - 0.3 * max(0.0, n1(x, s) + 0.1)
    def tp(x):
        step = math.floor((x + 2.0) * 3.0) / 3.0
        return max(tb(x), 2.15 + 0.55 * (0.5 + 0.9 * noise.noise(Vector((step * 2.3, s, 1.0)))))
    return WallSpec(name='Wall_Crumbled', seed=s, deck='plank', tb=tb, tp=tp, merlons=False, coping=lambda i, x: False,
                    gap=lambda x: (-1.25 < x < -0.5) or (0.95 < x < 1.3), loops=False,
                    heaps=[(0.0, -1.3, 2.0, 0.9, 0.7, 44, -0.05), (0.2, 1.3, 1.9, 0.85, 0.6, 34, -0.05)])

def end_piece():
    return WallSpec(name='Wall_End', seed=4, deck='plank', x0=-2.0, x1=0.0, ext=(True, False),
                    merlons=lambda i, x: x < -0.9, coping=lambda i, x: True, dressed_end=True)

def build_wall(S):
    pc = wall_pc(S)
    R = R_(S.name + 'extra')
    if S.name == 'Wall_Crumbled':
        # planks slumped into the hole, fallen coping blocks on the walkway
        for (xm, rz) in ((-0.45, 0.28), (0.9, -0.35)):
            box(pc, 'walk', (0.17, 0.87, 0.1), T(xm, 0.3, 1.75) @ Matrix.Rotation(rz, 4, 'X') @ RZ(0.1), uv_world=False, long_axis=1, seed=R.random())
        for xm in (-1.0, 1.1):
            rock(pc, 'coping', (xm, 0.2 + R.uniform(-0.1, 0.1), 2.3), (0.3, 0.2, 0.1), R, block=True)
    if S.dressed_end:
        end_dressing(pc, S, R)
    return pc

def end_dressing(pc, S, R):
    """Pale dressed quoins on both faces of the cut end, a dressed end face and a solid end pier under a coping cap."""
    Vz = Vector((0, 0, 1))
    # end face (+X normal at x = 0): U = +Y
    masonry(pc, Vector((0, 0, 0)), Vector((0, 1, 0)), Vz, Vector((1, 0, 0)), -0.76, TH, -0.5, 2.1, R, key='coping', bat=lambda v: 0.0,
            course=(0.3, 0.4), length=(0.5, 0.7), dmax=0.06, ochre=0.0)
    masonry(pc, Vector((0, 0, 0)), Vector((0, 1, 0)), Vz, Vector((1, 0, 0)), -0.76, PAR_IN, 2.1, 2.82, R, key='coping', bat=lambda v: 0.0,
            course=(0.3, 0.4), length=(0.5, 0.7), dmax=0.06, ochre=0.0)
    # quoins: alternating long and short stones up both corners
    z = -0.5; i = 0
    while z < 2.1:
        h = 0.34 + R.uniform(-0.02, 0.03)
        ln = 0.62 if i % 2 == 0 else 0.4
        stone(pc, 'coping', Vector((0, 0, 0)), Vector((1, 0, 0)), Vz, Vector((0, -1, 0)), -ln, -0.01, z + 0.015, z + h - 0.015, 0.075, R,
              bat=bat_out, shape=0.0)
        stone(pc, 'coping', Vector((0, 0, 0)), Vector((-1, 0, 0)), Vz, Vector((0, 1, 0)), 0.01, ln, z + 0.015, min(2.0, z + h - 0.015), 0.075, R,
              bat=lambda v: TH, shape=0.0)
        z += h; i += 1
    # end pier replaces the last merlon: solid, with a pale cap
    box(pc, 'rubble', (0.84, 0.68, 0.76), T(-0.44, -0.4, 3.2), cham=0.03, wob=0.006, seed=2.0)
    box(pc, 'coping', (0.92, 0.78, 0.12), T(-0.44, -0.4, 3.64), cham=0.03, wob=0.004, seed=3.0)
    # kerb stone where the boards stop
    box(pc, 'coping', (0.2, 0.87, 0.16), T(-0.12, 0.285, 2.18), cham=0.02, seed=4.0)

# --- buttress
def build_buttress():
    pc = FK.Piece('Wall_Buttress'); R = R_('buttress')
    Z1 = 2.3
    def wd(z): return 0.5 - 0.1 * z / Z1                      # half width
    def fy(z): return -(1.62 - 0.57 * z / Z1)                  # front plane y
    secs = []
    for z in (-0.55, 0.0, 1.2, Z1):
        secs.append((z, [(-wd(z), fy(z)), (wd(z), fy(z)), (wd(z), -0.4), (-wd(z), -0.4)]))
    merge_bm(pc, loft_bm(secs, lambda s, a, b: Vector((a, b, s))), 'joint')
    box(pc, 'foot', (1.3, 1.5, 1.05), T(0, -1.18, -1.0), cham=0.05, wob=0.01, seed=5.0)
    Vz = Vector((0, 0, 1))
    masonry(pc, Vector((0, 0, 0)), Vector((1, 0, 0)), Vz, Vector((0, -1, 0)), -0.46, 0.46, -0.5, Z1, R, bat=lambda v: -fy(v), ochre=0.3)
    for sgn in (1, -1):
        U = Vector((0, 1, 0)) if sgn > 0 else Vector((0, -1, 0))
        yy = (lambda u: u) if sgn > 0 else (lambda u: -u)
        masonry(pc, Vector((0, 0, 0)), U, Vz, Vector((sgn, 0, 0)), -1.7 if sgn > 0 else 0.4, -0.4 if sgn > 0 else 1.7, -0.5, Z1, R,
                bat=wd, ochre=0.3, skip=lambda u, v, *a, yy=yy: yy(u) < fy(v) + 0.12 or yy(u) > -0.45)
    for z in (0.95, 1.75):   # pale set-offs
        d = abs(fy(z)) - 0.3
        box(pc, 'coping', (2 * wd(z) + 0.12, d, 0.13), T(0, fy(z) + d / 2 - 0.03, z), cham=0.02, wob=0.004, seed=z * 7)
    cap = [(-1.14, 2.3), (-0.5, 2.3), (-0.5, 2.84), (-1.14, 2.42)]
    merge_bm(pc, loft_bm([(-0.44, cap), (0.44, cap)], lambda s, a, b: Vector((s, a, b))), 'coping')
    return pc

# --- stair (rises toward +X on the inner side, +Y)
def build_stair():
    pc = FK.Piece('Wall_Stair'); R = R_('stair')
    n = 12; rise = 2.2 / n; run = 0.25; x0 = -1.5
    sx = lambda i: x0 + run * i
    secs = []
    for k in range(1, n + 1):
        for x in (sx(k - 1), sx(k)):
            secs.append((x, [(0.55, -0.5), (1.8, -0.5), (1.8, k * rise), (0.55, k * rise)]))
    merge_bm(pc, loft_bm(secs, lambda s, a, b: Vector((s, a, b))), 'joint')
    for k in range(1, n + 1):
        h = k * rise + 0.5
        box(pc, 'coping', (run + 0.06, 1.0, h), T(sx(k - 1) + run / 2 - 0.02, 1.05, -0.5 + h / 2), cham=0.02, wob=0.004, seed=k * 3.1)
    box(pc, 'coping', (0.5, 1.2, 0.1), T(1.75, 1.15, 2.15), cham=0.02, seed=1.0)          # landing, flush with the module deck
    box(pc, 'joint', (0.5, 1.2, 1.9), T(1.75, 1.15, 1.2), cham=0.0)
    Vz = Vector((0, 0, 1))
    slope = rise / run
    line = lambda x: slope * (x - x0) + rise / 2
    wall_top = lambda u: min(2.7, max(0.35, line(-u) + 0.45)) if -u > x0 - 0.2 else 0.0
    masonry(pc, Vector((0, 1.8, 0)), Vector((-1, 0, 0)), Vz, Vector((0, 1, 0)), -2.0, 2.0, -0.5, 2.8, R, vmax=wall_top,
            bat=lambda v: 0.0, ochre=0.3, skip=lambda u, v, *a: -u > 1.5 and v > 2.1 and False)
    ang = math.atan2(2.2, run * n)
    ln = math.hypot(run * n, 2.2) + 0.3
    box(pc, 'joint', (ln, 0.3, 0.5), T(0.0, 1.62, line(0) + 0.2) @ Matrix.Rotation(-ang, 4, 'Y'), cham=0.0)
    box(pc, 'coping', (ln + 0.1, 0.4, 0.16), T(0.0, 1.62, line(0) + 0.45 + 0.08) @ Matrix.Rotation(-ang, 4, 'Y'), cham=0.03, wob=0.004, seed=9.0)
    return pc

# ============================================================================= gatehouses
ZS = 2.9          # arch spring line
ZL = 5.9          # lower block top (upper storey floor)
ZP = 8.5          # upper storey top (wall plate)
RING_IN = 0.43    # pale voussoir ring width
RING_OUT = 0.78   # outer relieving ring ends here

GATES = {
    'LowerGate': dict(c2c=6.6, D=2.2, kind='timber', seed=21),     # piers (-24.6,-64.6) and (-18.0,-64.6), yaw 0
    'EastGate': dict(c2c=6.44, D=2.3, kind='crenel', seed=22),     # piers (59.2,-44.1) and (54.0,-47.9), yaw 37
}

class Gate:
    def __init__(self, name, c2c, D, kind, seed):
        self.name = name; self.D = D; self.kind = kind; self.seed = seed
        self.hw = c2c / 2 + 1.2          # half width over the piers (piers are 2.4 m)
        self.ch = c2c / 2 - 1.2          # half of the clear passage
        self.yd = -(D - 1.0)             # door plane
        self.Dp = D + 0.25 if kind == 'timber' else D    # upper-storey half depth (jetty on the timber gate)

def face_frames(hw, D):
    X = Vector((1, 0, 0)); Y = Vector((0, 1, 0))
    return {'F': (Vector((0, -D, 0)), X, -Y, hw), 'B': (Vector((0, D, 0)), -X, Y, hw),
            'R': (Vector((hw, 0, 0)), Y, X, D), 'L': (Vector((-hw, 0, 0)), -Y, -X, D)}

Z3 = Vector((0, 0, 1))

def panel(pc, key, O, U, N, u0, u1, v0, v1, holes=(), cell=0.55, bulge=0.012, R=None):
    us = sorted(set([u0, u1] + [h[0] for h in holes] + [h[1] for h in holes] + [u0 + (u1 - u0) * k / max(1, round((u1 - u0) / cell)) for k in range(1, max(1, round((u1 - u0) / cell)))]))
    vs = sorted(set([v0, v1] + [h[2] for h in holes] + [h[3] for h in holes] + [v0 + (v1 - v0) * k / max(1, round((v1 - v0) / cell)) for k in range(1, max(1, round((v1 - v0) / cell)))]))
    us = [u for u in us if u0 <= u <= u1]; vs = [v for v in vs if v0 <= v <= v1]
    def P(u, v):
        a = (u - u0) / (u1 - u0); b = (v - v0) / (v1 - v0)
        return O + U * u + Z3 * v + N * (bulge * math.sin(math.pi * a) * math.sin(math.pi * b))
    for i in range(len(us) - 1):
        for j in range(len(vs) - 1):
            cu, cv = (us[i] + us[i + 1]) / 2, (vs[j] + vs[j + 1]) / 2
            if any(h[0] < cu < h[1] and h[2] < cv < h[3] for h in holes):
                continue
            add_face(pc, key, [P(us[i], vs[j]), P(us[i + 1], vs[j]), P(us[i + 1], vs[j + 1]), P(us[i], vs[j + 1])], ref=O - N * 5 + U * cu + Z3 * cv)

def poly_stone(pc, key, O, U, N, cs, d, R, plane=0.0):
    """A wedge-shaped dressed stone: cs = [(u, v), ...] quad in a face frame, d proud of `plane`, skirts back to the plane."""
    front = [O + U * u + Z3 * v + N * (plane + d) for u, v in cs]
    back = [O + U * u + Z3 * v + N * plane for u, v in cs]
    c = sum(front + back, Vector()) / 8
    off = (R.random(), R.random())
    add_face(pc, key, front, ref=c - N * 0.5, off=off)
    for i in range(len(cs)):
        j = (i + 1) % len(cs)
        add_face(pc, key, [back[i], back[j], front[j], front[i]], ref=c, off=off)

def decal(pc, centre, size, normal, rot=0.0, lift=0.09):
    n = Vector(normal).normalized()
    up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((0, 1, 0))
    side = up.cross(n).normalized(); up = n.cross(side).normalized()
    c = Vector(centre) + n * lift
    ca, sa = math.cos(rot), math.sin(rot)
    sx, sz = side * (size[0] / 2), up * (size[1] / 2)
    sx, sz = sx * ca + sz * sa, -sx * sa + sz * ca
    pts = [c - sx - sz, c + sx - sz, c + sx + sz, c - sx + sz]
    f = pc.bm.faces.new([pc.bm.verts.new(p) for p in pts])
    f.material_index = pc.slot('crack')
    for l, uv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        l[pc.uv].uv = uv

def crack_ribbon(pc, p0, along, normal, length, R, w0=0.07, lift=0.085, branches=2):
    """A jagged dark crack of flat quads lifted just off the stone face (geometry, so it reads at any distance)."""
    n = Vector(normal).normalized(); a = Vector(along).normalized(); side = a.cross(n).normalized()
    pts = []; lat = 0.0; seg = 9
    for i in range(seg + 1):
        t = i / seg
        lat += R.uniform(-0.09, 0.09)
        pts.append((Vector(p0) + a * (t * length) + side * lat + n * lift, w0 * (1.0 - 0.8 * t) + 0.012))
    for i in range(seg):
        (pa, wa), (pb, wb) = pts[i], pts[i + 1]
        add_face(pc, 'crackdark', [pa - side * wa / 2, pa + side * wa / 2, pb + side * wb / 2, pb - side * wb / 2], ref=pa - n * 2)
    for b in range(branches):
        i = R.randint(2, seg - 3); pb, wb = pts[i]
        ang = R.choice((-1, 1)) * R.uniform(0.5, 0.9)
        bd = (a * math.cos(ang) + side * math.sin(ang)).normalized()
        crack_ribbon(pc, pb - n * lift, bd, n, length * R.uniform(0.25, 0.4), R, w0=wb * 0.8, lift=lift, branches=0)

# ----------------------------------------------------------------------------- boolean core with the passage
def passage_core(pc, G, top, core_key, vault_key='rubble'):
    hw, D, ch = G.hw, G.D, G.ch
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * 2 * hw, v.co.y * 2 * D, v.co.z * (top + 0.55) + (top - 0.55) / 2))
    me = bpy.data.meshes.new('core'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('core', me); bpy.context.scene.collection.objects.link(ob)
    tm = bpy.data.meshes.new('tun1'); tb = bmesh.new(); bmesh.ops.create_cube(tb, size=1.0)
    for v in tb.verts:
        v.co = Vector((v.co.x * 2 * ch, v.co.y * (2 * D + 2), v.co.z * (ZS + 1.0) + (ZS - 1.0) / 2))
    tb.to_mesh(tm); tb.free()
    t1 = bpy.data.objects.new('tun1', tm); bpy.context.scene.collection.objects.link(t1)
    cm = bpy.data.meshes.new('tun2'); cb = bmesh.new()
    bmesh.ops.create_cone(cb, cap_ends=True, cap_tris=False, segments=32, radius1=ch, radius2=ch, depth=2 * D + 2)
    bmesh.ops.rotate(cb, verts=cb.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'X'))
    bmesh.ops.translate(cb, verts=cb.verts, vec=(0, 0, ZS))
    cb.to_mesh(cm); cb.free()
    t2 = bpy.data.objects.new('tun2', cm); bpy.context.scene.collection.objects.link(t2)
    bpy.context.view_layer.objects.active = ob
    for t in (t1, t2):
        md = ob.modifiers.new('b', 'BOOLEAN'); md.operation = 'DIFFERENCE'; md.object = t; md.solver = 'EXACT'
        bpy.ops.object.modifier_apply(modifier=md.name)
    res = bmesh.new(); res.from_mesh(ob.data)
    for f in res.faces:
        c = f.calc_center_median(); n = f.normal
        on_tunnel = abs(c.y) < D - 1e-3 and abs(c.x) <= ch + 1e-3 and (
            (c.z <= ZS + 1e-3 and abs(abs(c.x) - ch) < 1e-3) or (c.z > ZS - 1e-3 and abs(math.hypot(c.x, c.z - ZS) - ch) < 0.02))
        key = vault_key if on_tunnel else core_key
        pts = [l.vert.co.copy() for l in f.loops]
        add_face(pc, key, pts, ref=None)
    res.free()
    for o in (ob, t1, t2):
        m = o.data; bpy.data.objects.remove(o); bpy.data.meshes.remove(m)

def lantern(pc, G, sign, R):
    """An iron wall bracket and a small cage lantern beside the arch (front sign -1, back +1)."""
    x = G.ch + RING_OUT + 0.45; z = 3.5
    y0 = sign * G.D
    for sx in (-1, 1):
        px = sx * x
        box(pc, 'iron', (0.06, 0.07, 0.4), T(px, y0 + sign * 0.07, z + 0.1), cham=0.0)            # plate
        box(pc, 'iron', (0.05, 0.6, 0.05), T(px, y0 + sign * 0.35, z + 0.28), cham=0.0)           # arm
        box(pc, 'iron', (0.05, 0.05, 0.4), T(px, y0 + sign * 0.6, z + 0.1), cham=0.0)             # drop
        box(pc, 'iron', (0.22, 0.22, 0.05), T(px, y0 + sign * 0.6, z - 0.1), cham=0.0)            # base
        box(pc, 'iron', (0.26, 0.26, 0.05), T(px, y0 + sign * 0.6, z + 0.24), cham=0.0)           # hood
        box(pc, 'glass', (0.16, 0.16, 0.3), T(px, y0 + sign * 0.6, z + 0.07), cham=0.0)
        for cx, cy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            box(pc, 'iron', (0.025, 0.025, 0.34), T(px + cx * 0.095, y0 + sign * 0.6 + cy * 0.095, z + 0.07), cham=0.0)

# ----------------------------------------------------------------------------- the lower block
def arch_skip_factory(G):
    ch = G.ch
    def skip(u, v, ua, ub, va, vt):
        for (cu, cv) in ((ua, va), (ub, va), (ub, vt), (ua, vt)):
            if cv < ZS:
                if abs(cu) < ch + RING_IN:
                    return True
            elif math.hypot(cu, cv - ZS) < ch + RING_IN + 0.12 and True:
                return True
        # the outer relieving ring covers up to RING_OUT: drop stones whose centre is well inside it
        return False
    return skip

def arch_clip_factory(G):
    ch = G.ch
    lim = ch + RING_IN
    rc = ch + RING_IN + 0.08
    def clip(ua, ub, va, vt):
        if vt <= ZS + 0.02:
            if ub <= -lim or ua >= lim:
                return (ua, ub, va, vt)[:2]
            if ua > -lim and ub < lim:
                return None
            if ua < -lim and ub > lim:
                return None
            if ua < -lim < ub:
                return (ua, -lim) if -lim - ua > 0.18 else None
            return (lim, ub) if ub - lim > 0.18 else None
        for _ in range(12):
            inside = [(cu, cv) for (cu, cv) in ((ua, va), (ub, va), (ub, vt), (ua, vt)) if (math.hypot(cu, cv - ZS) < rc if cv >= ZS else abs(cu) < lim)]
            if not inside:
                return (ua, ub)
            if ua < 0 < ub:
                return None
            if ua >= 0:
                ua += 0.07
            else:
                ub -= 0.07
            if ub - ua < 0.2:
                return None
        return None
    return clip

def arch_ring(pc, G, R, sign):
    """Voussoir ring and outer relieving ring on the front (sign -1) or back (+1) face; jamb quoins below the spring."""
    ch = G.ch; D = G.D
    F = face_frames(G.hw, D)['F' if sign < 0 else 'B']
    O, U, N, half = F
    n = 15
    for i in range(n):
        a0 = math.pi * i / n; a1 = math.pi * (i + 1) / n
        ia = a0 + 0.006; ib = a1 - 0.006
        rr = RING_IN + (0.1 if i == n // 2 else 0.0)
        cs = [(math.cos(ia) * ch, ZS + math.sin(ia) * ch), (math.cos(ia) * (ch + rr), ZS + math.sin(ia) * (ch + rr)),
              (math.cos(ib) * (ch + rr), ZS + math.sin(ib) * (ch + rr)), (math.cos(ib) * ch, ZS + math.sin(ib) * ch)]
        cs = [(-u, v) for (u, v) in cs]    # frame u runs left to right as seen from outside; symmetric anyway
        poly_stone(pc, 'coping', O, U, N, cs, 0.12 if i == n // 2 else 0.085, R)
    m = 21
    for i in range(m):
        a0 = math.pi * i / m; a1 = math.pi * (i + 1) / m
        ia = a0 + 0.005; ib = a1 - 0.005
        r0 = ch + RING_IN - 0.02; r1 = ch + RING_OUT
        cs = [(math.cos(ia) * r0, ZS + math.sin(ia) * r0), (math.cos(ia) * r1, ZS + math.sin(ia) * r1),
              (math.cos(ib) * r1, ZS + math.sin(ib) * r1), (math.cos(ib) * r0, ZS + math.sin(ib) * r0)]
        poly_stone(pc, 'ochre' if R.random() < 0.3 else 'rubble', O, U, N, [(-u, v) for u, v in cs], R.uniform(0.03, 0.055), R)
    # jamb quoins: alternating long and short, pale
    z = -0.5; i = 0
    while z < ZS - 0.05:
        h = min(0.42 + R.uniform(-0.03, 0.04), ZS - z)
        ln = RING_IN + (0.27 if i % 2 else 0.0)
        for sg in (-1, 1):
            u0 = sg * ch; u1 = sg * (ch + ln)
            a, b = min(u0, u1), max(u0, u1)
            stone(pc, 'coping', O, U, Z3, N, a, b, z + 0.012, z + h - 0.012, 0.075, R, flush=(True, True), bulge=0.015)
        z += h; i += 1

def lower_block(pc, G, R, shell):
    hw, D, ch = G.hw, G.D, G.ch
    # footing
    box(pc, 'foot', (2 * hw + 0.3, 2 * D + 0.3, FOOT_TOP - FOOT_BOT), T(0, 0, (FOOT_TOP + FOOT_BOT) / 2), cham=0.05, wob=0.01, seed=3.0)
    passage_core(pc, G, ZL, 'rubble')
    skip = arch_skip_factory(G)
    fr = face_frames(hw, D)
    for name, (O, U, N, half) in fr.items():
        if name in ('F', 'B'):
            sk = lambda u, v, ua, ub, va, vt: False
        else:
            sk = lambda u, v, ua, ub, va, vt: ua > -0.64 and ub < 0.64 and vt < 3.4
        def sk2(u, v, ua, ub, va, vt, name=name, sk=sk):
            if shell and R.random() < 0.025:
                return True
            return sk(u, v, ua, ub, va, vt)
        masonry(pc, O, U, Z3, N, -half, half, -0.5, ZL - 0.3, R, skip=sk2, clip=arch_clip_factory(G) if name in ('F', 'B') else None,
                bat=None, course=(0.26, 0.46), length=(0.4, 0.95), dmax=0.07, ochre=0.22)
    arch_ring(pc, G, R, -1); arch_ring(pc, G, R, 1)
    # corner quoins up the four corners
    z = -0.5; i = 0
    while z < ZL - 0.3:
        h = min(0.42 + R.uniform(-0.03, 0.04), ZL - 0.3 - z)
        for sx in (-1, 1):
            for sy in (-1, 1):
                ln, sh = R.uniform(0.7, 0.85), R.uniform(0.38, 0.46)
                lx, ly = (ln, sh) if (i + (sx * sy > 0)) % 2 else (sh, ln)
                c = Vector((sx * (hw - lx / 2 + 0.035), sy * (D - ly / 2 + 0.035), z + h / 2))
                box(pc, 'coping', (lx, ly, h - 0.03), T(*c) @ RZ(math.radians(R.uniform(-1.2, 1.2))), cham=0.04, wob=0.006, seed=R.random() * 99)
        z += h; i += 1
    # string course (pale band) at the floor line
    prj = 0.16
    for (cx, cy, sx, sy) in ((0, -D - prj / 2 + 0.03, 2 * hw + 2 * prj, prj), (0, D + prj / 2 - 0.03, 2 * hw + 2 * prj, prj),
                             (hw + prj / 2 - 0.03, 0, prj, 2 * D), (-hw - prj / 2 + 0.03, 0, prj, 2 * D)):
        long_x = sx > sy
        for (a, b) in _split(-(sx if long_x else sy) / 2, (sx if long_x else sy) / 2, R, 0.9, 1.5):
            ln = b - a - 0.03
            size = (ln, sy, 0.2) if long_x else (sx, ln, 0.2)
            pos = (cx + (a + b) / 2, cy, ZL - 0.2) if long_x else (cx, cy + (a + b) / 2, ZL - 0.2)
            box(pc, 'coping', size, T(*pos), cham=0.025, wob=0.004, seed=R.random() * 30)
    if shell:
        # cracks radiating from the arch crowns, and one down the left return
        decal(pc, (-1.9, -D, 4.9), (2.4, 3.1), (0, -1, 0), rot=0.3)
        decal(pc, (2.3, -D, 5.1), (2.1, 2.8), (0, -1, 0), rot=-0.5)
        decal(pc, (0.8, D, 5.2), (2.2, 2.9), (0, 1, 0), rot=0.2)
        decal(pc, (-hw, 0.2, 3.4), (2.6, 3.4), (-1, 0, 0), rot=0.1)
        cr = R_(G.name + 'cracks')
        crack_ribbon(pc, (-G.ch - 0.7, -D, 5.2), (-0.35, 0, -1), (0, -1, 0), 2.6, cr)
        crack_ribbon(pc, (G.ch + 1.1, -D, 5.55), (0.2, 0, -1), (0, -1, 0), 3.1, cr)
        crack_ribbon(pc, (-0.8, -D, 5.78), (0.25, 0, -1), (0, -1, 0), 0.5, cr, branches=1)
        crack_ribbon(pc, (-1.2, D, 5.7), (0.1, 0, -1), (0, 1, 0), 2.8, cr)
        crack_ribbon(pc, (-hw, 0.4, 5.6), (0, 0.3, -1), (-1, 0, 0), 3.4, cr)
        crack_ribbon(pc, (hw, -1.2, 5.5), (0, -0.2, -1), (1, 0, 0), 3.0, cr)
        decal(pc, (hw, -1.0, 2.0), (2.2, 2.9), (1, 0, 0), rot=-0.4)

# ----------------------------------------------------------------------------- doors
def door_leaves(pc, G, R, broken=False):
    """Two timber leaves in the arch: vertical planks, iron straps and studs, a ring pull; local frame = door plane y 0."""
    ch = G.ch
    N_PL = 12
    w = ch - 0.04
    pw = w / N_PL
    for side in (-1, 1):
        if broken and side == -1:
            continue
        base = T(side * w, 0, 0) @ Matrix.Scale(-side, 4, Vector((1, 0, 0)))   # leaf local: hinge at 0, +x toward the centre
        L = base
        if broken:
            L = T(side * w, 0, 2.4) @ Matrix.Rotation(-0.55 * side, 4, 'Z') @ Matrix.Rotation(0.2, 4, 'Y') @ T(-side * w, 0, -2.4) @ base
        miss = set()
        if broken:
            miss = {k for k in range(N_PL) if R.random() < 0.3}
        for k in range(N_PL):
            if k in miss:
                continue
            xl = (k + 0.5) * pw
            xg = abs(w - xl)
            h = ZS + math.sqrt(max(0.0, (ch - 0.05) ** 2 - xg ** 2)) - 0.03
            if broken:
                h *= R.uniform(0.55, 0.98) if k < 7 else 1.0
            box(pc, 'oak_lt', (pw - 0.012, 0.11, h), L @ T(xl, 0, h / 2 + 0.02) @ RZ(R.uniform(-0.01, 0.01)), cham=0.0, uv_world=False,
                long_axis=2, seed=R.random() * 20 + k)
        for zc in (0.55, 1.9, 3.35):
            ln = max(0.4, min(w - 0.04, math.sqrt(max(0.0, (ch - 0.1) ** 2 - max(0.0, zc - ZS) ** 2)) + 0.0))
            for yy in (-0.075, 0.075):
                box(pc, 'iron', (ln * 0.98, 0.03, 0.12), L @ T(ln * 0.49 + 0.02, yy, zc) , cham=0.0)
                box(pc, 'iron', (0.1, 0.034, 0.16), L @ T(ln * 0.98 + 0.01, yy, zc) @ RZ(0.0), cham=0.0)   # curled end
                box(pc, 'iron', (0.1, 0.04, 0.16), L @ T(0.07, yy, zc), cham=0.0)                        # hinge eye
            for sx in (0.3, 0.7):
                for yy in (-0.095, 0.095):
                    box(pc, 'iron', (0.055, 0.03, 0.055), L @ T(ln * sx, yy, zc + 0.09), cham=0.0)
        # ring pull on the meeting stile
        rc = L @ T(w - 0.12, -0.12, 1.15)
        for kseg in range(8):
            a = kseg * math.pi / 4
            box(pc, 'iron', (0.05, 0.025, 0.05), rc @ T(0.07 * math.cos(a), 0, 0.07 * math.sin(a)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Y'), cham=0.0)

def build_part_door(G):
    pc = FK.Piece('Part_Door')
    door_leaves(pc, G, R_(G.name + 'door'))
    return pc

# ----------------------------------------------------------------------------- the timber-framed upper storey (Lower Gate, restored)
def tile_patch(pc, p00, p10, p11, p01, R, ref, course=0.26, key='roof'):
    p00, p10, p11, p01 = [Vector(p) for p in (p00, p10, p11, p01)]
    Lu = max((p10 - p00).length, (p11 - p01).length); Lv = max((p01 - p00).length, (p11 - p10).length)
    nu = max(2, round(Lu / 0.34)); ncourse = max(2, round(Lv / course)); nv = ncourse * 2
    nrm = (p10 - p00).cross(p01 - p00)
    if nrm.length < 1e-6:
        nrm = (p11 - p00).cross(p01 - p10)
    nrm.normalize()
    if nrm.dot((p00 + p10 + p11 + p01) / 4 - ref) < 0:
        nrm = -nrm
    grid = []
    for j in range(nv + 1):
        row = []
        for i in range(nu + 1):
            u, v = i / nu, j / nv
            p = p00.lerp(p10, u).lerp(p01.lerp(p11, u), v)
            p = p + nrm * (0.035 * (j % 2) + 0.012 * R.uniform(-1, 1))
            row.append(p)
        grid.append(row)
    for j in range(nv):
        for i in range(nu):
            q = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
            f = add_face(pc, key, q, ref=ref)
            if f:   # tile UVs: u across, v up the slope, in metres / 2
                for l in f.loops:
                    pass

def lower_frame_top(pc, G, R):
    hw, D, Dp = G.hw, G.D, G.Dp
    # upper core (hidden), joists and bressumers
    box(pc, 'joint', (2 * hw - 0.04, 2 * Dp - 0.04, ZP - ZL), T(0, 0, (ZP + ZL) / 2), cham=0.0)
    ex = Dp - D
    for sgn in (-1, 1):
        for xj in [-hw + 0.45 + k * (2 * hw - 0.9) / 9 for k in range(10)]:
            box(pc, 'oak', (0.2, ex + 0.62, 0.24), T(xj, sgn * (D + ex / 2 - 0.2), ZL - 0.02), cham=0.02, wob=0.006, seed=R.random() * 50, long_axis=1, uv_world=False)
    fr = face_frames(hw, Dp)
    PR = 0.07; TIMW = 0.26
    zb0, zb1 = ZL + 0.12, ZL + 0.4          # bressumer
    zp0, zp1 = ZP - 0.28, ZP                # wall plate
    for name, (O, U, N, half) in fr.items():
        nx = 7 if name in ('F', 'B') else 5
        us = [-half + TIMW / 2 + k * (2 * half - TIMW) / (nx - 1) for k in range(nx)]
        win_bays = (1, 4) if name in ('F', 'B') else (2,)
        holes = []
        for bi in win_bays:
            a, b = us[bi] + TIMW / 2 + 0.2, us[bi + 1] - TIMW / 2 - 0.2
            holes.append((a, b, ZL + 1.15, ZL + 2.25))
        panel(pc, 'plaster', O, U, N, -half, half, zb1 - 0.02, zp0 + 0.02, holes=holes, cell=0.6)
        def mem(a, b, w, d, nm, extra=0.0):
            FK.member(pc, 'oak', a, b, w, d, name='%s%s%d' % (G.name, name, nm), up=N, extend=extra)
        off = N * (PR - TIMW / 2)
        for k, u in enumerate(us):
            mem(O + U * u + Z3 * zb1 + off, O + U * u + Z3 * zp0 + off, TIMW, TIMW, k)
        mem(O + U * (-half) + Z3 * ((zb0 + zb1) / 2) + off, O + U * half + Z3 * ((zb0 + zb1) / 2) + off, zb1 - zb0, TIMW + 0.04, 50)
        mem(O + U * (-half) + Z3 * ((zp0 + zp1) / 2) + off, O + U * half + Z3 * ((zp0 + zp1) / 2) + off, zp1 - zp0, TIMW + 0.04, 51)
        for k in range(nx - 1):
            if k in win_bays:
                a, b = us[k] + TIMW / 2, us[k + 1] - TIMW / 2
                ux0, ux1 = a + 0.2, b - 0.2
                zs0, zs1 = ZL + 1.15, ZL + 2.25
                mem(O + U * a + Z3 * (zs0 - 0.1) + off, O + U * b + Z3 * (zs0 - 0.1) + off, 0.16, TIMW, 60 + k)
                mem(O + U * a + Z3 * (zs1 + 0.1) + off, O + U * b + Z3 * (zs1 + 0.1) + off, 0.16, TIMW, 70 + k)
                for uu, nm in ((ux0, 80), (ux1, 81)):
                    mem(O + U * uu + Z3 * zs0 + off, O + U * uu + Z3 * zs1 + off, 0.1, 0.14, nm + k)
                # window glass (dark) and open shutters against the wall
                gc = O + U * ((ux0 + ux1) / 2) + Z3 * ((zs0 + zs1) / 2) - N * 0.1
                box(pc, 'glass', ((ux1 - ux0) - 0.08, 0.02, (zs1 - zs0) - 0.08), T(*gc) @ Matrix.Rotation(0, 4, 'Z') @ (RZ(math.atan2(N.y, N.x) + math.pi / 2)), cham=0.0)
                for sg in (-1, 1):
                    sc = O + U * ((ux0 + ux1) / 2 + sg * ((ux1 - ux0) / 2 + 0.26)) + Z3 * ((zs0 + zs1) / 2) + N * 0.05
                    box(pc, 'shutter', (0.46, 0.045, zs1 - zs0 - 0.02), T(*sc) @ RZ(math.atan2(N.y, N.x) + math.pi / 2), cham=0.012, uv_world=False, long_axis=2, seed=R.random() * 20)
            else:
                a = O + U * (us[k] + (us[k + 1] - us[k]) * (0.0 if k % 2 == 0 else 1.0)) + Z3 * zb1 + off
                b = O + U * (us[k] + (us[k + 1] - us[k]) * (1.0 if k % 2 == 0 else 0.0)) + Z3 * zp0 + off
                mem(a, b, 0.2, TIMW - 0.04, 90 + k, extra=0.0)
    # roof
    hwr, hdr = hw + 0.55, Dp + 0.6
    zE, zR = ZP - 0.05, ZP + 4.3
    rl = hwr - hdr
    ref = Vector((0, 0, ZP))
    tile_patch(pc, (-hwr, -hdr, zE), (hwr, -hdr, zE), (rl, 0, zR), (-rl, 0, zR), R, ref)
    tile_patch(pc, (hwr, -hdr, zE), (hwr, hdr, zE), (rl, 0, zR), (rl, 0, zR), R, ref)
    tile_patch(pc, (hwr, hdr, zE), (-hwr, hdr, zE), (-rl, 0, zR), (rl, 0, zR), R, ref)
    tile_patch(pc, (-hwr, hdr, zE), (-hwr, -hdr, zE), (-rl, 0, zR), (-rl, 0, zR), R, ref)
    # soffit and fascia
    cw, cd = hw + 0.02, Dp + 0.02
    corners_o = [(-hwr, -hdr), (hwr, -hdr), (hwr, hdr), (-hwr, hdr)]
    corners_i = [(-cw, -cd), (cw, -cd), (cw, cd), (-cw, cd)]
    for i in range(4):
        j = (i + 1) % 4
        add_face(pc, 'oak', [(*corners_i[i], zE - 0.12), (*corners_i[j], zE - 0.12), (*corners_o[j], zE - 0.12), (*corners_o[i], zE - 0.12)], ref=Vector((0, 0, zE + 3)))
        add_face(pc, 'oak', [(*corners_o[i], zE - 0.14), (*corners_o[j], zE - 0.14), (*corners_o[j], zE + 0.03), (*corners_o[i], zE + 0.03)], ref=Vector((0, 0, zE)))
    # ridge and hip caps, finial
    FK.member(pc, 'roof', (-rl - 0.05, 0, zR + 0.06), (rl + 0.05, 0, zR + 0.06), 0.34, 0.2, name='ridge', up=(0, 1, 0), wob=0.004)
    for (cx, cy, rx) in ((-hwr, -hdr, -rl), (hwr, -hdr, rl), (hwr, hdr, rl), (-hwr, hdr, -rl)):
        FK.member(pc, 'roof', (cx, cy, zE + 0.05), (rx, 0, zR + 0.06), 0.3, 0.17, name='hip%d' % int(cx * 10), up=(0, 0, 1), wob=0.004)
    box(pc, 'iron', (0.06, 0.06, 1.0), T(rl + 0.1, 0, zR + 0.55), cham=0.0)
    box(pc, 'iron', (0.5, 0.02, 0.3), T(rl + 0.1 + 0.25, 0, zR + 0.9), cham=0.0)
    chimney(pc, G, R, 8.7, 11.7, broken=False)

def chimney(pc, G, R, z0, z1, broken):
    cx, cy = -G.hw + 1.0, G.Dp - 1.0
    hs = 0.5
    box(pc, 'joint', (2 * hs, 2 * hs, z1 - z0), T(cx, cy, (z0 + z1) / 2), cham=0.0)
    for name, (O, U, N, half) in face_frames(1, 1).items():
        # frame for this chimney: translate the unit-box frames to the stack
        Oc = Vector((cx, cy, 0)) + (N * hs)
        Uc = U
        if name in ('F', 'B'):
            span = hs
        else:
            span = hs
        vmax = None
        if broken:
            vmax = lambda u, name=name: z1 - 0.9 * (0.5 + 0.5 * noise.noise(Vector((u * 3 + 5, ord(name), 0.0))))
        masonry(pc, Oc, Uc, Z3, N, -span, span, z0, z1, R, vmax=vmax, course=(0.2, 0.34), length=(0.3, 0.55), dmax=0.045, ochre=0.2)
    if not broken:
        box(pc, 'coping', (1.3, 1.3, 0.2), T(cx, cy, z1 + 0.05), cham=0.03, seed=2.0)
        for dx in (-0.22, 0.22):
            box(pc, 'roof', (0.3, 0.3, 0.55), T(cx + dx, cy, z1 + 0.37), cham=0.04, seed=3.0 + dx)

# ----------------------------------------------------------------------------- the stone upper storey and battlements (East Gate, restored)
def east_frame_top(pc, G, R):
    hw, D = G.hw, G.D
    box(pc, 'joint', (2 * hw - 0.04, 2 * D - 0.04, 8.5 - ZL), T(0, 0, (8.5 + ZL) / 2), cham=0.0)
    fr = face_frames(hw, D)
    ZT = 8.3
    for name, (O, U, N, half) in fr.items():
        holes = []
        if name in ('F', 'B'):
            holes = [(-2.9 - 0.7, -2.9 + 0.7, 6.55, 8.05), (2.9 - 0.7, 2.9 + 0.7, 6.55, 8.05)]
        else:
            holes = [(-0.15, 0.15, 6.6, 7.7)]
        def skip(u, v, ua, ub, va, vt, holes=holes):
            for h in holes:
                if ua < h[1] + 0.12 and ub > h[0] - 0.12 and va < h[3] + 0.12 and vt > h[2] - 0.12:
                    return True
            return False
        masonry(pc, O, U, Z3, N, -half, half, ZL - 0.4, ZT, R, skip=skip, course=(0.26, 0.44), length=(0.4, 0.9), dmax=0.065, ochre=0.2)
        for h in holes:
            cu = (h[0] + h[1]) / 2; w = h[1] - h[0]
            slit = w < 0.5
            gcen = O + U * cu + Z3 * ((h[2] + h[3]) / 2) + N * 0.03
            box(pc, 'glass', (w, 0.03, h[3] - h[2]), T(*gcen) @ RZ(math.atan2(N.y, N.x) + math.pi / 2), cham=0.0)
            # dressed surround: jambs, sill, lintel
            surr = [(-w / 2 - 0.14, 0.28, h[3] - h[2] + 0.1, 0.0), (w / 2 + 0.14, 0.28, h[3] - h[2] + 0.1, 0.0)]
            for (du, sw, sh, dz) in surr:
                c = O + U * (cu + du) + Z3 * ((h[2] + h[3]) / 2) + N * 0.06
                box(pc, 'coping', (sw, 0.14, sh), T(*c) @ RZ(math.atan2(N.y, N.x) + math.pi / 2), cham=0.025, wob=0.004, seed=R.random() * 20)
            for zz, hh in ((h[2] - 0.07, 0.14), (h[3] + 0.09, 0.18)):
                c = O + U * cu + Z3 * zz + N * 0.07
                box(pc, 'coping', (w + 0.6, 0.16, hh), T(*c) @ RZ(math.atan2(N.y, N.x) + math.pi / 2), cham=0.025, wob=0.004, seed=R.random() * 20)
    # corner quoins to the top
    z = ZL - 0.3; i = 0
    while z < ZT - 0.1:
        h = min(0.42, ZT - z)
        for sx in (-1, 1):
            for sy in (-1, 1):
                ln, sh = R.uniform(0.7, 0.85), R.uniform(0.38, 0.46)
                lx, ly = (ln, sh) if (i + (sx * sy > 0)) % 2 else (sh, ln)
                box(pc, 'coping', (lx, ly, h - 0.03), T(sx * (hw - lx / 2 + 0.035), sy * (D - ly / 2 + 0.035), z + h / 2), cham=0.04, wob=0.006, seed=R.random() * 99)
        z += h; i += 1
    battlements(pc, G, R, ZT, broken=None)
    east_turret(pc, G, R, broken=False)

def battlements(pc, G, R, ZT, broken=None):
    """Corbels, a projecting slab, a parapet and merlons all round; broken(x, y) -> True drops a merlon (Shell)."""
    hw, D = G.hw, G.D
    prj = 0.38
    HX, HY = hw + prj, D + prj
    for (name, (O, U, N, half)) in face_frames(hw, D).items():
        n = int((2 * half) / 0.78)
        for k in range(n):
            u = -half + (k + 0.5) * (2 * half) / n
            pp = O + U * u
            if broken and broken(pp.x, pp.y):
                continue
            c = O + U * u + Z3 * (ZT + 0.15) + N * 0.16
            box(pc, 'coping', (0.42, 0.36, 0.34), T(*c) @ RZ(math.atan2(N.y, N.x) + math.pi / 2), cham=0.035, wob=0.005, seed=R.random() * 20)
    slab_z = ZT + 0.40
    if broken:   # Shell: only the back-right stretch of slab and parapet is left
        slabs = [(HX + 1.0, 0.55, (HX - 1.0) / 2, HY - 0.27), (0.55, HY, HX - 0.27, HY / 2)]
        pars = [(HX + 0.9, 0.5, (HX - 1.0) / 2, HY - 0.3), (0.5, HY - 0.5, HX - 0.3, (HY - 0.5) / 2)]
    else:
        slabs = [(2 * HX, 0.55, 0, -HY + 0.27), (2 * HX, 0.55, 0, HY - 0.27), (0.55, 2 * HY, -HX + 0.27, 0), (0.55, 2 * HY, HX - 0.27, 0)]
        pars = [(2 * HX - 0.1, 0.5, 0, -HY + 0.3), (2 * HX - 0.1, 0.5, 0, HY - 0.3), (0.5, 2 * HY - 1.0, -HX + 0.3, 0), (0.5, 2 * HY - 1.0, HX - 0.3, 0)]
    for (sx, sy, cx, cy) in slabs:
        box(pc, 'coping', (sx, sy, 0.2), T(cx, cy, slab_z), cham=0.03, wob=0.004, seed=R.random() * 20)
    zpar = slab_z + 0.1
    for (sx, sy, cx, cy) in pars:
        box(pc, 'rubble', (sx, sy, 0.7), T(cx, cy, zpar + 0.35), cham=0.03, wob=0.005, seed=R.random() * 20)
        box(pc, 'coping', (sx + 0.06, sy + 0.1, 0.1), T(cx, cy, zpar + 0.75), cham=0.025, wob=0.004, seed=R.random() * 20)
    # merlons
    for (nm, length, cx0, cy0, ax) in (('F', 2 * HX, 0, -HY + 0.3, 0), ('B', 2 * HX, 0, HY - 0.3, 0), ('L', 2 * HY, -HX + 0.3, 0, 1), ('R', 2 * HY, HX - 0.3, 0, 1)):
        nm_ = 8 if ax == 0 else 4
        p = length / nm_
        for k in range(nm_):
            u = -length / 2 + (k + 0.5) * p
            x, y = (cx0 + u, cy0) if ax == 0 else (cx0, cy0 + u)
            if broken and broken(x, y):
                continue
            wx, wy = (0.78, 0.5) if ax == 0 else (0.5, 0.78)
            box(pc, 'rubble', (wx, wy, 0.45), T(x, y, zpar + 0.8 + 0.225 + 0.05), cham=0.03, wob=0.006, seed=R.random() * 20)
            box(pc, 'coping', (wx + 0.12, wy + 0.12, 0.1), T(x, y, zpar + 0.8 + 0.5 + 0.1), cham=0.025, wob=0.004, seed=R.random() * 20)

def east_turret(pc, G, R, broken):
    hw, D = G.hw, G.D
    HX, HY = hw + 0.38, D + 0.38
    cx, cy = -hw + 1.2, D - 1.5
    zb = ZT_DECK = 8.3 + 0.4 + 0.2 + 0.1
    top = 11.6
    if not broken:
        # deck planks inside the parapet
        n = int((2 * HX - 1.0) / 0.2)
        for k in range(n):
            x = -HX + 0.5 + (k + 0.5) * (2 * HX - 1.0) / n
            box(pc, 'walk', ((2 * HX - 1.0) / n - 0.02, 2 * HY - 1.0, 0.08), T(x, 0, zb - 0.04 + R.uniform(-0.004, 0.004)), cham=0.0, long_axis=1, uv_world=False, seed=R.random() * 20)
    hs = 1.0
    box(pc, 'joint', (2 * hs, 2 * hs, top - zb), T(cx, cy, (top + zb) / 2), cham=0.0)
    for name, (O0, U, N, half) in face_frames(1, 1).items():
        Oc = Vector((cx, cy, 0)) + N * hs
        holes = [(-0.12, 0.12, zb + 1.1, zb + 2.0)]
        def skip(u, v, ua, ub, va, vt, holes=holes):
            return any(ua < h[1] + 0.1 and ub > h[0] - 0.1 and va < h[3] + 0.1 and vt > h[2] - 0.1 for h in holes)
        masonry(pc, Oc, U, Z3, N, -hs, hs, zb, top, R, skip=skip, course=(0.24, 0.4), length=(0.35, 0.7), dmax=0.055, ochre=0.2)
        gc = Oc + Z3 * (zb + 1.55) + N * 0.03
        box(pc, 'glass', (0.24, 0.03, 0.9), T(*gc) @ RZ(math.atan2(N.y, N.x) + math.pi / 2), cham=0.0)
        c = Oc + Z3 * (zb + 1.55) + N * 0.06
        for du in (-0.3, 0.3):
            box(pc, 'coping', (0.2, 0.12, 1.05), T(*(c + U * du)) @ RZ(math.atan2(N.y, N.x) + math.pi / 2), cham=0.02, seed=R.random() * 20)
    # corner quoins
    z = zb; i = 0
    while z < top - 0.2:
        h = 0.4
        for sx in (-1, 1):
            for sy in (-1, 1):
                lx, ly = (0.62, 0.34) if (i + (sx * sy > 0)) % 2 else (0.34, 0.62)
                box(pc, 'coping', (lx, ly, h - 0.03), T(cx + sx * (hs - lx / 2 + 0.03), cy + sy * (hs - ly / 2 + 0.03), z + h / 2), cham=0.035, seed=R.random() * 99)
        z += h; i += 1
    # corbelled cap and a steep tile pyramid
    ov = 0.4
    box(pc, 'coping', (2 * hs + 0.5, 2 * hs + 0.5, 0.2), T(cx, cy, top + 0.1), cham=0.03, seed=7.0)
    r0 = hs + ov + 0.1; zE = top + 0.2; zA = zE + 2.7
    ref = Vector((cx, cy, zE))
    cs = [(cx - r0, cy - r0), (cx + r0, cy - r0), (cx + r0, cy + r0), (cx - r0, cy + r0)]
    for i in range(4):
        j = (i + 1) % 4
        tile_patch(pc, (*cs[i], zE), (*cs[j], zE), (cx, cy, zA), (cx, cy, zA), R, ref)
    box(pc, 'iron', (0.06, 0.06, 0.9), T(cx, cy, zA + 0.4), cham=0.0)
    for i in range(4):
        FK.member(pc, 'roof', (*cs[i], zE + 0.05), (cx, cy, zA), 0.26, 0.16, name='thip%d' % i, up=(0, 0, 1), wob=0.004)

# ----------------------------------------------------------------------------- Shells (ruined, roof gone)
def jag(s, seed, lo, hi, step=0.4):
    k = math.floor(s / step)
    a = noise.noise(Vector((k * 1.7 + seed, seed * 0.3, 0.2)))
    return lo + (hi - lo) * (0.5 + 0.7 * a)

def ring_walls(pc, G, R, top_fn, thick=0.85, key='rubble'):
    """The upper storey's masonry as four broken walls (hollow), tops from top_fn(face, s)."""
    hw, D = G.hw, G.D
    xs = [-hw + k * (2 * hw) / 16 for k in range(17)]
    ys = [-D + thick + k * (2 * D - 2 * thick) / 6 for k in range(7)]
    for sgn, name in ((-1, 'F'), (1, 'B')):
        secs2 = []
        for x in xs:
            t = top_fn(name, x)
            ya, yb = (-D, -D + thick) if sgn < 0 else (D - thick, D)
            secs2.append((x, [(ya, ZL - 0.3), (yb, ZL - 0.3), (yb, t), (ya, t)]))
        merge_bm(pc, loft_bm(secs2, lambda s, a, b: Vector((s, a, b))), key)
    for sgn, name in ((-1, 'L'), (1, 'R')):
        secs2 = []
        for y in [-D + thick + k * (2 * D - 2 * thick) / 6 for k in range(7)]:
            t = top_fn(name, y)
            xa, xb = (-hw, -hw + thick) if sgn < 0 else (hw - thick, hw)
            secs2.append((y, [(xa, ZL - 0.3), (xb, ZL - 0.3), (xb, t), (xa, t)]))
        merge_bm(pc, loft_bm(secs2, lambda s, a, b: Vector((a, s, b))), key)

def shell_skin(pc, G, R, top_fn):
    hw, D = G.hw, G.D
    for name, (O, U, N, half) in face_frames(hw, D).items():
        def vmax(u, name=name, U=U, O=O):
            p = O + U * u
            return top_fn(name, p.x if name in ('F', 'B') else p.y)
        masonry(pc, O, U, Z3, N, -half, half, ZL - 0.3, ZL + 3.2, R, vmax=vmax, course=(0.26, 0.44), length=(0.4, 0.9), dmax=0.07, ochre=0.25)

def build_shell(G):
    R = R_(G.name + 'shell')
    pc = FK.Piece('Shell')
    hw, D, ch = G.hw, G.D, G.ch
    lower_block(pc, G, R, shell=True)
    seed = G.seed
    if G.kind == 'timber':
        # knee walls of the upper storey, a lone chimney stack, charred joists and posts, rafter stubs
        def top_fn(face, s):
            base = {'F': 0.35, 'B': 1.1, 'L': 0.9, 'R': 0.25}[face]
            return ZL + base + 0.5 * (noise.noise(Vector((s * 1.4 + seed, ord(face), 0.0))) + 0.4)
        top_fn2 = lambda f, s: max(ZL + 0.15, top_fn(f, s))
        ring_walls(pc, G, R, top_fn2, thick=0.8)
        shell_skin(pc, G, R, top_fn2)
        chimney(pc, G, R, ZL - 0.3, 10.1, broken=True)
        # joists: stubs poking out of the front, a few across the floor, snapped
        for k in range(10):
            xj = -hw + 0.45 + k * (2 * hw - 0.9) / 9
            for sgn in (-1, 1):
                if R.random() < 0.45:
                    continue
                ln = R.uniform(0.3, 0.62)
                box(pc, 'oak', (0.2, ln, 0.22), T(xj, sgn * (D + 0.25 - ln / 2 + 0.05), ZL - 0.02) @ RZ(R.uniform(-0.05, 0.05)), cham=0.02, wob=0.01, seed=R.random() * 50, long_axis=1, uv_world=False)
        for k in range(6):
            x = R.uniform(-hw + 1.0, hw - 1.0); y = R.uniform(-D + 1.0, D - 1.2)
            if abs(x - (-hw + 1.0)) < 1.0 and y > D - 2.3:
                continue
            ang = R.uniform(0, 3.14)
            box(pc, 'oak', (R.uniform(0.8, 2.2), 0.2, 0.22), T(x, y, ZL + 0.13 + R.uniform(0, 0.3)) @ RZ(ang) @ Matrix.Rotation(R.uniform(-0.2, 0.2), 4, 'Y'), cham=0.02, wob=0.012, seed=R.random() * 50, long_axis=0, uv_world=False)
        # snapped posts standing in the knee walls
        for (px, py, ph) in ((-hw + 0.45, -D - 0.12, 1.5), (-1.0, -D - 0.12, 0.7), (hw - 0.45, D + 0.12, 1.9), (2.2, D + 0.12, 0.9), (hw + 0.1, -1.0, 1.3)):
            box(pc, 'oak', (0.26, 0.26, ph), T(px, py, ZL + 0.12 + ph / 2), cham=0.02, wob=0.012, seed=px * 7, long_axis=2, uv_world=False)
    else:
        def top_fn(face, s):
            if face == 'F':
                return ZL + 0.2 + 2.0 * max(0.0, noise.noise(Vector((s * 0.8 + seed, 1.0, 0.0))) + 0.55) * (1.0 if s < 1.5 else 0.5)
            if face == 'B':
                if s > -1.0:
                    return 8.05 + 0.2 * noise.noise(Vector((s * 1.3 + seed, 3.0, 0.0)))
                return ZL + 0.3 + 1.6 * max(0.0, noise.noise(Vector((s * 0.9 + seed, 3.0, 0.0))) + 0.5)
            if face == 'L':
                return 8.0 - 2.4 * max(0.0, noise.noise(Vector((s * 1.1 + seed, 5.0, 0.0))) + 0.5)
            return ZL + 0.3 + 1.2 * max(0.0, noise.noise(Vector((s * 1.2 + seed, 7.0, 0.0))) + 0.5)
        top_fn2 = lambda f, s: max(ZL + 0.2, top_fn(f, s))
        ring_walls(pc, G, R, top_fn2, thick=0.9)
        shell_skin(pc, G, R, top_fn2)
        # a fragment of the corbelled parapet along the back
        battlements_frag(pc, G, R)
        # broken turret stump
        east_turret_stump(pc, G, R)
        # a few snapped floor beams
        for k in range(8):
            x = R.uniform(-hw + 0.8, hw - 0.8); y = R.uniform(-D + 0.9, D - 0.9)
            box(pc, 'oak', (R.uniform(1.0, 2.4), 0.22, 0.24), T(x, y, ZL + 0.14 + R.uniform(0, 0.25)) @ RZ(R.uniform(0, 3.14)) @ Matrix.Rotation(R.uniform(-0.15, 0.15), 4, 'Y'), cham=0.02, wob=0.012, seed=R.random() * 50, long_axis=0, uv_world=False)
    # rubble: spilled front and back, beside the sides, and on the floor inside
    clip_out = lambda x, y: not (abs(x) < hw - 0.1 and abs(y) < D - 0.1)
    heap(pc, R, -hw * 0.45, -D - 1.5, 3.2, 1.6, 0.85, 42, zbase=-0.05, clip=clip_out)
    heap(pc, R, hw * 0.5, D + 1.4, 3.0, 1.5, 0.8, 36, zbase=-0.05, clip=clip_out)
    heap(pc, R, hw + 1.3, -D * 0.6, 1.5, 2.0, 0.6, 22, zbase=-0.05, clip=clip_out)
    heap(pc, R, -hw - 1.3, D * 0.5, 1.4, 1.8, 0.55, 18, zbase=-0.05, clip=clip_out)
    heap(pc, R, 0.5, 0.2, hw - 1.2, D - 1.1, 0.5, 40, zbase=ZL - 0.1, clip=lambda x, y: True)
    # passage floor: a few fallen stones in the arch mouth, and the broken door leaf
    heap(pc, R, 0.0, yd_front(G) - 0.5, ch - 0.3, 0.8, 0.35, 10, zbase=0.0, clip=lambda x, y: True, smin=0.1, smax=0.22)
    broken_door(pc, G, R)
    return pc

def yd_front(G):
    return G.yd

def broken_door(pc, G, R):
    sub = FK.Piece('tmp')
    door_leaves(sub, G, R, broken=True)
    # move the door geometry into the Shell: copy faces translated by the door plane
    bm = sub.bm
    for f in bm.faces:
        pts = [l.vert.co + Vector((0, G.yd, 0)) for l in f.loops]
        nf = pc.bm.faces.new([pc.bm.verts.new(p) for p in pts])
        key = sub.mats[f.material_index]
        if key not in pc.mats:
            pc.mats.append(key)
        nf.material_index = pc.mats.index(key)
        for l, ol in zip(nf.loops, f.loops):
            l[pc.uv].uv = ol[sub.uv].uv
    bm.free()
    # planks on the ground
    for k in range(5):
        box(pc, 'oak_lt', (0.16, R.uniform(0.8, 1.8), 0.1), T(R.uniform(-1.2, 1.2), G.yd - R.uniform(0.3, 1.4), 0.07) @ RZ(R.uniform(0, 3.14)), cham=0.0, uv_world=False, long_axis=1, seed=R.random() * 30)

def battlements_frag(pc, G, R):
    hw, D = G.hw, G.D
    # keep only a stretch of corbel/parapet/merlons along the back-right
    broken = lambda x, y: not (y > 0 and x > -1.0)
    battlements(pc, G, R, 8.0, broken=broken)

def east_turret_stump(pc, G, R):
    hw, D = G.hw, G.D
    cx, cy = -hw + 1.2, D - 1.5
    zb = ZL - 0.3; hs = 1.0
    def tf(name, s):
        return ZL + 2.2 + 1.5 * (0.5 + noise.noise(Vector((s * 2.0 + ord(name), 2.0, 0.0))))
    for name, (O0, U, N, half) in face_frames(1, 1).items():
        Oc = Vector((cx, cy, 0)) + N * hs
        masonry(pc, Oc, U, Z3, N, -hs, hs, zb, zb + 4.5, R, vmax=lambda u, name=name: tf(name, u), course=(0.24, 0.4), length=(0.35, 0.7), dmax=0.055, ochre=0.2)
    # hollow-ish: just a core block lower than the skin
    box(pc, 'rubble', (2 * hs - 0.1, 2 * hs - 0.1, 2.9), T(cx, cy, zb + 1.45 + 0.1), cham=0.0)

# ----------------------------------------------------------------------------- gate assembly
def build_gate(name):
    cfg = GATES[name]
    G = Gate(name, **cfg)
    R = R_(name + 'frame')
    pcF = FK.Piece('Frame')
    lower_block(pcF, G, R, shell=False)
    lantern(pcF, G, -1, R); lantern(pcF, G, 1, R)
    if G.kind == 'timber':
        lower_frame_top(pcF, G, R)
    else:
        east_frame_top(pcF, G, R)
    pcS = build_shell(G)
    pcD = build_part_door(G)
    root = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(root)
    kids = []
    for pc, nm, gr, ms in ((pcF, 'Frame', 1.0, 1.0), (pcS, 'Shell', 1.3, 1.5), (pcD, 'Part_Door', 1.0, 1.0)):
        ob = make_obj(pc, nm, seed=G.seed, grime=gr, moss=ms)
        ob.name = '%s:%s' % (name, nm); ob.parent = root; kids.append(ob)
        print('  %-10s %-10s %6d tris  %s' % (name, nm, tris(ob), ', '.join(m.name for m in ob.data.materials)))
    kids[2].location = (0, G.yd, 0)
    # Part_Door is authored about the door plane; move its geometry so the object origin sits on SOCKET_Door
    sock = bpy.data.objects.new(name + ':SOCKET_Door', None); bpy.context.scene.collection.objects.link(sock)
    sock.parent = root; sock.location = (0, G.yd, 0); sock.empty_display_type = 'ARROWS'
    # footprint: the gatehouse block, flat
    fm = bpy.data.meshes.new('FOOTPRINT'); fb = bmesh.new(); bmesh.ops.create_cube(fb, size=1.0)
    for v in fb.verts:
        v.co = Vector((v.co.x * 2 * G.hw, v.co.y * 2 * G.D, v.co.z * 0.1 + 0.05))
    fb.normal_update(); fb.to_mesh(fm); fb.free()
    fp = bpy.data.objects.new(name + ':FOOTPRINT', fm); bpy.context.scene.collection.objects.link(fp); fp.parent = root
    a = fm.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    for d in a.data:
        d.color = (0, 0, 0, 1)
    return root, kids, sock, fp, G

# ----------------------------------------------------------------------------- assembly: wall pieces
WALL_PIECES = {
    'Wall_Straight_A': lambda: make_obj(build_wall(straight_a()), seed=1),
    'Wall_Straight_B': lambda: make_obj(build_wall(straight_b()), seed=2),
    'Wall_Buttress': lambda: make_obj(build_buttress(), seed=5),
    'Wall_End': lambda: make_obj(build_wall(end_piece()), seed=4),
    'Wall_Stair': lambda: make_obj(build_stair(), seed=6),
    'Wall_Breach': lambda: make_obj(build_wall(breach()), seed=11, grime=1.15, moss=1.4),
    'Wall_Crumbled': lambda: make_obj(build_wall(crumbled()), seed=12, grime=1.15, moss=1.4),
    'Wall_Patched': lambda: make_obj(build_wall(patched()), seed=3, grime=0.9),
}
BUDGET = {'Wall_Straight_A': 6000, 'Wall_Straight_B': 6000, 'Wall_Patched': 6500, 'Wall_Breach': 7000, 'Wall_Crumbled': 6000,
          'Wall_Buttress': 3000, 'Wall_End': 3000, 'Wall_Stair': 3000}

def build_walls(only=None):
    objs = {}
    for name, fn in WALL_PIECES.items():
        if only and only not in ('wall', name):
            continue
        ob = fn(); objs[name] = ob
        print('  %-18s %6d tris  (budget %d)  %s' % (name, tris(ob), BUDGET[name], ', '.join(m.name for m in ob.data.materials)))
    return objs

# ----------------------------------------------------------------------------- review scene
def place(src, loc, yaw=0.0, mirror=False, name=None):
    o = bpy.data.objects.new(name or src.name, src.data)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc; o.rotation_euler = (0, 0, yaw)
    if mirror:
        o.scale = (-1, 1, 1)
    return o

def lay_run(objs, seq, start=(0.0, 0.0), yaw=0.0):
    """seq items: (piece, bend_deg[, mirror[, [(attached_piece, local_x), ...]]]). A mirrored Wall_End has its cut face at the
    START of the run (its body extends +X)."""
    pos = Vector((start[0], start[1], 0.0)); h = yaw; out = []
    for item in seq:
        piece, bend = item[0], item[1]
        mirror = len(item) > 2 and item[2]
        att = item[3] if len(item) > 3 else []
        h += math.radians(bend)
        d = Vector((math.cos(h), math.sin(h), 0))
        ln = 2.0 if piece == 'Wall_End' else 4.0
        c = pos if (piece == 'Wall_End' and mirror) else pos + d * 2
        out.append(place(objs[piece], c, h, mirror=mirror))
        for (ap, lx) in att:
            out.append(place(objs[ap], c + d * lx, h))
        pos = pos + d * ln
    return out

def review_ground(size=(60, 60), centre=(0, 0)):
    gm = FK.tile_mat('TW_ReviewGround', 'B1_town/meadow_grass')
    bpy.ops.mesh.primitive_plane_add(size=1); g = bpy.context.object; g.name = 'Ground'
    g.scale = (size[0], size[1], 1); g.location = (centre[0], centre[1], -0.02)
    bpy.ops.object.transform_apply(scale=True)
    g.data.materials.append(gm)
    uvl = g.data.uv_layers[0]
    for poly in g.data.polygons:
        for li in poly.loop_indices:
            co = g.data.vertices[g.data.loops[li].vertex_index].co
            uvl.data[li].uv = (co.x / TILE, co.y / TILE)
    FK.zero_wear(g)
    return g

def figure(loc):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.5, location=(loc[0], loc[1], 0.75)); f = bpy.context.object
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.13, location=(loc[0], loc[1], 1.62))
    fm = bpy.data.materials.new('Figure'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.18, 0.12, 1)
    for o in (f, bpy.context.object):
        o.data.materials.append(fm)

def shoot(cam, frm, to, fov, path):
    FK.look(cam, Vector(frm), Vector(to)); cam.data.angle = math.radians(fov)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print('rendered', os.path.basename(path))

def setup_review():
    for o in list(bpy.context.scene.objects):
        if o.type == 'MESH':
            o.hide_render = True; o.hide_viewport = True
    return FK.review_lights(bpy.context.scene)

# ----------------------------------------------------------------------------- export
def export_selected(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS',
                             bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR',
                             path_mode='STRIP', object_types={'MESH', 'EMPTY'})

def set_state(gobjs, shell):
    root, kids, sock, fp, G = gobjs
    for k in kids:
        base = k.name.split(':')[-1]
        k.hide_render = (base == 'Shell') != shell if base in ('Shell', 'Frame') else shell
        k.hide_viewport = k.hide_render

def render_gate_views(objs, gates, cam):
    for gname, gobjs in gates.items():
        G = gobjs[4]
        for other, og in gates.items():
            for k in og[1]:
                if other != gname:
                    k.hide_render = True; k.hide_viewport = True
        for shell in (False, True):
            set_state(gobjs, shell)
            tag = '%s_%s' % (gname, 'shell' if shell else 'frame')
            hw = G.hw
            shoot(cam, (-hw - 9.0, -17.0, 5.2), (0, 0, 4.2), 44, os.path.join(OUT, tag + '_out.png'))
            if '--quick' in argv:
                continue
            shoot(cam, (hw + 8.0, 15.0, 5.5), (0, 0, 4.2), 44, os.path.join(OUT, tag + '_in.png'))
            shoot(cam, (0.0, -12.0, 2.4), (0, 0, 3.6), 42, os.path.join(OUT, tag + '_front.png'))

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ONLY = FK.arg('--only')
    want_walls = ONLY in (None, 'wall') or ONLY in WALL_PIECES
    want_gates = ONLY in (None, 'gates') or ONLY in GATES
    objs = build_walls(ONLY if want_walls and ONLY != None else None) if want_walls else {}
    if not objs and want_gates and '--with-walls' in argv:
        objs = build_walls('wall')
    show_walls = want_walls
    gates = {}
    if want_gates:
        for gname in GATES:
            if ONLY in GATES and ONLY != gname:
                continue
            gates[gname] = build_gate(gname)
    if '--export' in argv:
        for n, o in objs.items():
            export_selected([o], os.path.join(OUT, n, n + '.fbx'))
        for gname, (root, kids, sock, fp, G) in gates.items():
            parts = kids + [sock, fp]
            saved = [(o, o.name) for o in parts]
            for o in parts:                       # plain child names inside the FBX: Frame, Shell, Part_Door, SOCKET_Door, FOOTPRINT
                o.name = o.name.split(':')[-1] + '~' + gname
            for o in parts:
                o.name = o.name.split('~')[0]
            export_selected([root] + parts, os.path.join(OUT, gname, gname + '.fbx'))
            for o, n in saved:
                o.name = n
    if '--no-render' not in argv:
        cam = setup_review()
        review_ground((90, 90), (0, 0))
        if show_walls and objs and ('Wall_Straight_A' in objs) and '--showcase' not in argv:
            run = [('Wall_Straight_A', 0), ('Wall_Straight_B', 0), ('Wall_Straight_A', 15), ('Wall_Patched', 0), ('Wall_Straight_B', -15)]
            lay_run(objs, run, (-10, 0))
            if '--figure' in argv:
                figure((-3.0, 1.6))
            shoot(cam, (-2, -9, 3.0), (-1, 0, 1.6), 46, os.path.join(OUT, 'run_out.png'))
            shoot(cam, (-3, 7, 3.4), (-2, 0, 1.6), 46, os.path.join(OUT, 'run_in.png'))
        if show_walls and objs and '--showcase' in argv:
            for o in list(bpy.context.scene.objects):
                if o.name.startswith('Wall_') and o.name not in objs:
                    pass
            # S1: ruined variants in a run
            lay_run(objs, [('Wall_Straight_A', 0), ('Wall_Breach', 0), ('Wall_Straight_B', 0), ('Wall_Crumbled', 0), ('Wall_Straight_A', 0)], (-30, 30))
            shoot(cam, (-23.5, 20.5, 2.6), (-24, 30, 1.2), 46, os.path.join(OUT, 'breach_out.png'))
            shoot(cam, (-24.5, 40.0, 3.0), (-24, 30, 1.1), 46, os.path.join(OUT, 'breach_in.png'))
            shoot(cam, (-15.5, 20.5, 2.6), (-16, 30, 1.3), 46, os.path.join(OUT, 'crumbled_out.png'))
            shoot(cam, (-16.5, 40.0, 3.4), (-16, 30, 1.4), 46, os.path.join(OUT, 'crumbled_in.png'))
            # S2: kit pieces: mirrored end, buttress, stair, patched, end
            lay_run(objs, [('Wall_End', 0, True), ('Wall_Straight_A', 0, False, [('Wall_Buttress', 0.0)]),
                           ('Wall_Straight_B', 0, False, [('Wall_Stair', 0.0)]), ('Wall_Patched', 0), ('Wall_End', 0)], (-30, -30))
            shoot(cam, (-25, -40.0, 3.0), (-24, -30, 1.6), 46, os.path.join(OUT, 'kit_out.png'))
            shoot(cam, (-21, -20.0, 4.2), (-23, -30, 1.4), 46, os.path.join(OUT, 'kit_in.png'))
            shoot(cam, (-25.5, -24.5, 3.2), (-24.5, -29.4, 1.3), 50, os.path.join(OUT, 'stair_in.png'))
        if gates:
            for o in list(bpy.context.scene.objects):
                if o.name.startswith('Wall_') :
                    o.hide_render = True
            if objs:
                wa = objs['Wall_Straight_A']
                for gname, (root, kids, sock, fp, G) in gates.items():
                    for sx in (-1, 1):
                        place(wa, (sx * (G.hw + 2.0), 0, 0), 0.0)
                        place(objs['Wall_Straight_B'], (sx * (G.hw + 6.0), 0, 0), 0.0)
            if '--figure' in argv:
                figure((-1.0, -D_FIG if False else -5.0))
            render_gate_views(objs, gates, cam)
