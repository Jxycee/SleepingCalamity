"""Square ground: the B6 granite setts (sc_cobbles.py family) laid over the whole Hollowmere square, around the approved corner.

Design: docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md section 6, row "Square ground" (cobbles over the
full ~40 x 28 m square, lane stubs, edges, drains). The approved beauty-corner patch (sc_cobbles.py, x -12..8, z -6..8 Unity) is
NOT relaid: its cobble_placements.json is read, its stones are rasterised as occupied, and this script only lays new setts
around it. The corner's ragged fade bands (its open edges, where its setts thin out) are filled from outside, so the seam
disappears and every corner stone keeps its place.

The plan is read from the C# layout (Assets/_Project/Core/Town/HollowmereLayout.cs: the square bounds, the ring 1 lots, the
landmarks and the lane polylines) so the ground follows the one town plan; nothing about the plan is copied here.

Laying order (an occupancy raster at 2 cm, as in sc_cobbles.Layout):
  1. occupied: outside the paved area, the corner's interior, every corner stone, lot footprints, the heroes' cistern house;
  2. drains: a 0.56 m gully hole at each drain, two courses of setts round it;
  3. gutters: a double course laid lengthwise down the middle of each lane stub (a "Rinne"), sunk 1.2 cm, damp toward the drain;
  4. lane stubs: straight rows across the lane (Reihenpflaster), 0.15 m apart;
  5. the square: the corner's fan grid (2.0 m fans, rows 1.0 m apart, every other row offset half a fan) continued exactly,
     so the fans run on across the seam;
  6. fill: setts, small setts, cut small setts.
Heights: the corner's dish (sc_plaza_common.dish, 0 beyond 8.7 m) plus the slow undulation; a 2.5 cm sink into every drain;
clustered sunken setts, missing setts (weed slots), loose stones; worn paler path setts on the desire lines from each lane
mouth and doorway to the fountain loop; thinning, sunken, ragged edges against the lots (dirt shows, `lot_edge` weed slots)
and at the open far ends of the lane stubs (`edge_grass`).

Outputs (--out): square_placements.json (same record format as cobble_placements.json: stones [{v, kind, p, q, s}] in Unity
metres around the fountain centre, local to the shelf height; weed_slots [{p, kind}]; drains [{p, yaw}] for the builder's
Drain_A gully; stats) and SquareBed.fbx (the joint bed under the new setts only: the corner keeps its PlazaBed; same CobbleBed
material, top at -1.2 cm so it shows in the joints over the -2 cm square paving slab, rising to -0.4 cm at the open edges),
plus plan.png (a top-down map of every stone for review). --export copies them into
Assets/_Project/Content/Art/Town/B6/SquareCobbles/.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_square_ground.py -- --out <scratch> [--export]

No new meshes or textures: the instances use the corner's 14 sett/fragment variants (Content/Art/Town/B6/Cobbles/CobbleFamily.fbx)
and the CobbleBed set, so the GPU Resident Drawer still sees 11 meshes and one material.
"""
import sys, os, math, json, re, shutil
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'tools')); sys.path.insert(0, HERE)
import bpy, bmesh
import numpy as np
from mathutils import Matrix
import sc_cobbles as C          # the family, the Layout class and the course/fill logic (its grid globals are re-pointed below)
import sc_plaza_common as P
import sc_aaa_kit as K

OUT = C.OUT
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
LAYOUT_CS = os.path.join(ROOT, 'Assets', '_Project', 'Core', 'Town', 'HollowmereLayout.cs')
CORNER_JSON = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B6', 'Cobbles', 'cobble_placements.json')
DEST = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B6', 'SquareCobbles')

# ----------------------------------------------------------------------------- the plan, parsed from HollowmereLayout.cs
SRC = open(LAYOUT_CS, encoding='utf-8').read()
def _const(name):
    return float(re.search(name + r'\s*=\s*(-?[\d.]+)f', SRC).group(1))
SQ = (_const('SquareMinX'), _const('SquareMaxX'), _const('SquareMinZ'), _const('SquareMaxZ'))
SIDE = {'S': 6.0, 'M': 8.0, 'L': 11.0}                     # TownLayout.Foundation.SideLength
LOTS = [(m.group(1), SIDE[m.group(2)], float(m.group(3)), float(m.group(4)))
        for m in re.finditer(r'\("(F\d\d)", FoundationSize\.(\w), (-?[\d.]+)f, (-?[\d.]+)f, 0f', SRC)]
LANDMARKS = {m.group(1): (float(m.group(2)), float(m.group(3)), float(m.group(4)), float(m.group(5)))
             for m in re.finditer(r'\("(\w+)", "[^"]*", LandmarkKind\.\w+, (-?[\d.]+)f, (-?[\d.]+)f, (-?[\d.]+)f, (-?[\d.]+)f', SRC)}
def _lane(lid):
    m = re.search(r'\("' + lid + r'", "[^"]*", \d, ([\d.]+)f, new\[\] \{([^}]*)\}', SRC)
    v = [float(t.strip().rstrip('f')) for t in m.group(2).split(',') if t.strip()]
    return float(m.group(1)), [(v[i], v[i + 1]) for i in range(0, len(v), 2)]
assert len(LOTS) == 10 and 'HeroesHome_Cistern' in LANDMARKS, 'HollowmereLayout.cs changed shape: update the parser'

# Lane stubs: (lane id, how far the setts run out from the square edge along the lane, metres). The far end thins out raggedly.
STUBS = [('WestStub', 7.0), ('MarketStreet', 8.0), ('EastStub', 7.0), ('NorthEastStub', 5.0), ('NorthWestGinnel', 6.0)]
def stub_polyline(lid, run):
    """The lane's first `run` metres measured from where it leaves the square, plus 1 m back inside the square."""
    w, pts = _lane(lid)
    a, b = pts[0], pts[1]
    d = math.hypot(b[0] - a[0], b[1] - a[1]); ux, uz = (b[0] - a[0]) / d, (b[1] - a[1]) / d
    out = [(a[0] - ux * 1.0, a[1] - uz * 1.0)]
    left = run + 0.0
    for i in range(1, len(pts)):
        a, b = pts[i - 1], pts[i]; d = math.hypot(b[0] - a[0], b[1] - a[1])
        if i == 1: out.append(a)
        if d >= left: out.append((a[0] + (b[0] - a[0]) * left / d, a[1] + (b[1] - a[1]) * left / d)); break
        out.append(b); left -= d
    return w, out
STUB = {lid: stub_polyline(lid, run) for lid, run in STUBS}

# Drains (Unity x, z, yaw): in the gutter at each lane mouth and one down Market Street; the square falls gently to them.
DRAINS = [(0.0, -20.1, 0.0), (0.0, -26.6, 0.0), (-19.0, -7.25, 90.0), (19.2, -16.0, 90.0), (-11.5, -14.5, 15.0)]
GULLY = 0.56                    # Drain_A frame (0.5 m) + joint

# ----------------------------------------------------------------------------- geometry helpers (Unity xz)
def seg_dist(px, pz, a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]; L2 = dx * dx + dz * dz
    t = max(0.0, min(1.0, ((px - a[0]) * dx + (pz - a[1]) * dz) / L2)) if L2 > 0 else 0.0
    return math.hypot(px - (a[0] + t * dx), pz - (a[1] + t * dz)), t

def poly_dist(px, pz, pts):
    """(distance, arc length of the closest point, total length)."""
    best, best_s, s0 = 1e9, 0.0, 0.0
    for i in range(1, len(pts)):
        d, t = seg_dist(px, pz, pts[i - 1], pts[i]); L = math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1])
        if d < best: best, best_s = d, s0 + t * L
        s0 += L
    return best, best_s, s0

def rect_dist(px, pz, cx, cz, hx, hz):
    """Distance outside an axis-aligned rect (negative inside: minus the distance to its edge)."""
    ox, oz = abs(px - cx) - hx, abs(pz - cz) - hz
    if ox <= 0 and oz <= 0: return max(ox, oz)
    return math.hypot(max(ox, 0.0), max(oz, 0.0))

def lot_dist(px, pz):
    d = min(rect_dist(px, pz, x, z, s / 2, s / 2) for _, s, x, z in LOTS)
    cx, cz, sx, sz = LANDMARKS['HeroesHome_Cistern']
    return min(d, rect_dist(px, pz, cx, cz, sx / 2, sz / 2))

SQ_PAD = 0.6                    # the paving runs this far past the square bounds, into the strip before the lots
def in_square(px, pz, m=0.0):
    return SQ[0] - SQ_PAD - m <= px <= SQ[1] + SQ_PAD + m and SQ[2] - SQ_PAD - m <= pz <= SQ[3] + SQ_PAD + m

def stub_info(px, pz):
    """(lane id, distance from the lane centreline / half width, arc position 0..1 along the stub) of the nearest stub, or None."""
    best = None
    for lid, (w, pts) in STUB.items():
        d, s, L = poly_dist(px, pz, pts)
        if d < w / 2 + 0.25 and (best is None or d < best[1] * best[3]):
            best = (lid, d / (w / 2), s / L, w / 2)
    return best

def corner_edge(px, pz):
    """sc_cobbles' own edge distance (in fade-band units) inside the corner patch, None outside it."""
    X0, X1, Z0, Z1 = P.PATCH_U
    if not (X0 <= px <= X1 and Z0 <= pz <= Z1): return None
    return C.edge_dist(px, pz)

# ----------------------------------------------------------------------------- the raster: re-point sc_cobbles' grid globals
RES_G = C.RES_G
GX0, GX1, GZ0, GZ1 = -30.0, 30.0, -30.0, 13.0
C.X0, C.X1, C.Z0, C.Z1 = GX0, GX1, GZ0, GZ1
C.NX, C.NZ = int((GX1 - GX0) / RES_G), int((GZ1 - GZ0) / RES_G)
C.GX, C.GZ = np.meshgrid((GX0 + (np.arange(C.NX) + 0.5) * RES_G).astype(np.float32),
                         (GZ0 + (np.arange(C.NZ) + 0.5) * RES_G).astype(np.float32), indexing='ij')
GX, GZ = C.GX, C.GZ

# Worn desire lines: lane mouths and doorways to the fountain loop (r 4.2). The corner keeps its own path_weight.
DESIRE = [((-20.0, -7.25), (-4.2, -0.6)), ((0.0, -21.0), (0.3, -4.2)), ((20.0, -16.0), (3.0, -3.0)),
          ((18.8, 6.4), (12.5, 6.6)), ((12.5, 6.6), (3.1, 3.0)), ((-19.4, 6.4), (-12.5, 6.4)), ((-12.5, 6.4), (-3.0, 3.0)),
          ((0.0, -21.0), (0.0, -29.0)), ((-20.0, -7.25), (-28.0, -7.3)), ((20.0, -16.0), (28.0, -16.2))]
def square_path_weight(x, z):
    best = min(seg_dist(x, z, a, b)[0] for a, b in DESIRE)
    fl = abs(math.hypot(x, z) - 4.2)
    return max(P.smoothstep(1.0 - best / 1.3), P.smoothstep(1.0 - fl / 0.9), CORNER_PATH(x, z))
CORNER_PATH = C.path_weight
C.path_weight = square_path_weight

def drain_sink(x, z):
    """0..1 near a drain (the fall toward it)."""
    return max(P.smoothstep(1.0 - math.hypot(x - dx, z - dz) / 1.4) for dx, dz, _ in DRAINS)

class SquareLayout(C.Layout):
    def __init__(self, corner):
        self.stones = []
        allowed = np.zeros((C.NX, C.NZ), dtype=bool)
        # the square (+ pad) and the stub corridors
        allowed |= (GX >= SQ[0] - SQ_PAD) & (GX <= SQ[1] + SQ_PAD) & (GZ >= SQ[2] - SQ_PAD) & (GZ <= SQ[3] + SQ_PAD)
        for lid, (w, pts) in STUB.items():
            for i in range(1, len(pts)):
                a, b = pts[i - 1], pts[i]; dx, dz = b[0] - a[0], b[1] - a[1]; L2 = dx * dx + dz * dz
                t = np.clip(((GX - a[0]) * dx + (GZ - a[1]) * dz) / L2, 0, 1)
                allowed |= np.hypot(GX - (a[0] + t * dx), GZ - (a[1] + t * dz)) < w / 2 + 0.2
        # lots (0.25 m clear of the lot edge: the ruin footing and the doorstep sit there) and the cistern house
        for _, s, x, z in LOTS:
            allowed &= ~((np.abs(GX - x) < s / 2 + 0.25) & (np.abs(GZ - z) < s / 2 + 0.25))
        cx, cz, sx, sz = LANDMARKS['HeroesHome_Cistern']
        allowed &= ~((np.abs(GX - cx) < sx / 2 + 0.2) & (np.abs(GZ - cz) < sz / 2 + 0.2))
        allowed &= np.hypot(GX, GZ) > P.RING_R + 0.006
        self.occ = ~allowed
        # the corner: its interior is taken whole (its holes and kicked-loose doorstep are art); in its fade bands only its stones
        X0, X1, Z0, Z1 = P.PATCH_U
        inside = (GX >= X0) & (GX <= X1) & (GZ >= Z0) & (GZ <= Z1)
        # vectorised sc_cobbles.edge_dist (west 4 m, east 2 m, south 2 m, north 2.5 m outside the facade footprint)
        e = np.minimum(np.minimum((GX - X0) / 4.0, (X1 - GX) / 2.0), (GZ - Z0) / 2.0)
        fac = (GX > C.FACADE[0] - 0.3) & (GX < C.FACADE[1] + 0.3)
        e = np.where(fac, e, np.minimum(e, (Z1 - GZ) / 2.5))
        self.occ |= inside & (e >= 0.85)          # fully laid there (edge_thin is ~0 from about 0.85 band units in)
        for st in corner:
            x, z = st['p'][0], st['p'][2]
            r = 0.5 * math.sqrt(C.VAR[st['v']][1] * C.VAR[st['v']][2]) * st['s'][0] + C.JOINT * 0.5
            i0, i1 = int((x - r - GX0) / RES_G), int((x + r - GX0) / RES_G) + 1
            j0, j1 = int((z - r - GZ0) / RES_G), int((z + r - GZ0) / RES_G) + 1
            sub = (GX[i0:i1, j0:j1] - x) ** 2 + (GZ[i0:i1, j0:j1] - z) ** 2 < r * r
            self.occ[i0:i1, j0:j1] |= sub
        self.n_free0 = int((~self.occ).sum())

    def pick(self, r, x, z, small=False):
        if small: return 'small_a'
        if r.random() < square_path_weight(x, z) * 0.8: return r.choice(C.PATHS)
        if r.random() < 0.8 * drain_sink(x, z) + 0.03: return r.choice(C.DAMPS)
        return r.choice(C.SETTS)

    def hole(self, x, z, half, yaw):
        """Mark a square (the gully) as occupied."""
        c = self._cells(x, z, 2 * half, 2 * half, math.radians(yaw), 0.0)
        if c: self.occ[c[0]] |= c[1]

    def build_square(self):
        r = C.rng('square-layout')
        # 2. drains: gully hole, two courses round it (square rings of setts)
        for dx, dz, yaw in DRAINS:
            self.hole(dx, dz, GULLY / 2, yaw)
            a = math.radians(yaw); ca, sa = math.cos(a), math.sin(a)
            for k in range(2):
                h = GULLY / 2 + C.JOINT + 0.07 + k * (0.14 + C.JOINT)
                for side in range(4):          # each side of the square ring, laid along it
                    ang = a + side * math.pi / 2
                    ux, uz = math.cos(ang), math.sin(ang); nx, nz = -uz, ux
                    sx, sz = dx - ux * h + nx * h, dz - uz * h + nz * h   # start corner
                    self.course(r, lambda s, sx=sx, sz=sz, ux=ux, uz=uz, ang=ang: (sx + ux * s, sz + uz * s, ang), 2 * h, tol=0.12)
        # 3. gutters: a double course lengthwise down each stub centreline
        for lid, (w, pts) in STUB.items():
            for off in (-0.075 - C.JOINT / 2, 0.075 + C.JOINT / 2):
                for i in range(1, len(pts)):
                    a_, b_ = pts[i - 1], pts[i]; L = math.hypot(b_[0] - a_[0], b_[1] - a_[1])
                    ux, uz = (b_[0] - a_[0]) / L, (b_[1] - a_[1]) / L; nx, nz = -uz, ux; ang = math.atan2(uz, ux)
                    self.course(r, lambda s, a_=a_, ux=ux, uz=uz, nx=nx, nz=nz, off=off, ang=ang: (a_[0] + ux * s + nx * off, a_[1] + uz * s + nz * off, ang), L, tol=0.08)
        # 4. stub rows across the lane
        for lid, (w, pts) in STUB.items():
            for i in range(1, len(pts)):
                a_, b_ = pts[i - 1], pts[i]; L = math.hypot(b_[0] - a_[0], b_[1] - a_[1])
                ux, uz = (b_[0] - a_[0]) / L, (b_[1] - a_[1]) / L; nx, nz = -uz, ux; ang = math.atan2(nz, nx)
                s = 0.0
                while s < L:
                    ox, oz = a_[0] + ux * s - nx * (w / 2 + 0.3), a_[1] + uz * s - nz * (w / 2 + 0.3)
                    self.course(r, lambda t, ox=ox, oz=oz, nx=nx, nz=nz, ang=ang: (ox + nx * t, oz + nz * t, ang), w + 0.6, tol=0.08)
                    s += 0.14 + C.JOINT
        # 5. fans: the corner's grid (X0 -12, Z0 -6, FW 2, FH 1, odd rows offset FW/2) continued over the whole square
        FW, FH, RMAX = 2.0, 1.0, 1.45
        cx0, cz0 = P.PATCH_U[0], P.PATCH_U[2]
        for j in range(-26, 22):
            for i in range(-10, 22):
                ox = cx0 + i * FW + (FW / 2 if j % 2 else 0.0) + r.uniform(-0.03, 0.03); oz = cz0 + j * FH - 0.6
                if not in_square(ox, oz + 1.0, 1.6): continue
                i0, i1 = max(0, int((ox - 1.2 - GX0) / RES_G)), min(C.NX, int((ox + 1.2 - GX0) / RES_G))
                j0, j1 = max(0, int((oz - GZ0) / RES_G)), min(C.NZ, int((oz + 1.6 - GZ0) / RES_G))
                if i0 >= i1 or j0 >= j1 or self.occ[i0:i1, j0:j1].all(): continue
                Rk = RMAX
                while Rk > 0.22:
                    a = math.asin(min(0.97, (FW * 0.52) / Rk)); arc = 2 * a * Rk
                    self.course(r, lambda s, R=Rk, a0=math.pi / 2 + a, ox=ox, oz=oz: (ox + R * math.cos(a0 - s / R), oz + R * math.sin(a0 - s / R), a0 - s / R - math.pi / 2), arc, tol=0.07)
                    Rk -= 0.14 + C.JOINT
        # 6. fill
        for small, scl, step in ((False, (0.9, 1.0), 5), (True, (0.92, 1.04), 3), (True, (0.72, 0.8), 2)):
            free = np.argwhere(~self.occ)
            idx = list(range(0, len(free), step)); r.shuffle(idx)
            for t in idx:
                i, j = free[t]
                if self.occ[i, j]: continue
                x, z = float(GX[i, j]), float(GZ[i, j])
                si = stub_info(x, z)
                base = (math.atan2(*reversed(self._stub_dir(si[0]))) + math.pi / 2) if si and not in_square(x, z) else r.uniform(-0.3, 0.3)
                v = self.pick(r, x, z, small); sc = r.uniform(*scl)
                for da in (0.0, math.pi / 2, math.pi / 4, -math.pi / 4):
                    if self.place(v, x, z, base + da, sc, tol=0.16): break
        return self.stones

    def _stub_dir(self, lid):
        pts = STUB[lid][1]; a, b = pts[1], pts[2]; L = math.hypot(b[0] - a[0], b[1] - a[1])
        return ((b[0] - a[0]) / L, (b[1] - a[1]) / L)

# ----------------------------------------------------------------------------- edges, heights and damage
def edge_band(x, z):
    """Metres-in from a worn edge, in band units (0 at the edge, 1 fully laid): against the lots (1.3 m), the open square
    boundary where no lane leaves (1.5 m), the far end of each lane stub (3 m) and the stub's sides (0.8 m)."""
    e = (lot_dist(x, z) - 0.25) / 1.3
    si = stub_info(x, z)
    if si and not in_square(x, z, -0.2):
        lid, dn, s01, hw = si
        L = poly_dist(x, z, STUB[lid][1])[2]
        e = min(e, (1.0 - s01) * L / 3.0, (1.0 - dn) * hw / 0.8 + 0.4)
    else:
        b = min(x - (SQ[0] - SQ_PAD), (SQ[1] + SQ_PAD) - x, z - (SQ[2] - SQ_PAD), (SQ[3] + SQ_PAD) - z)
        if not si: e = min(e, b / 1.5)
    return e

def heights_and_damage(stones):
    r = C.rng('square-damage')
    pts = np.array([[s['x'], 0.0, s['z']] for s in stones])
    und = (K.fbm(pts, 0.7, 3, 0.5, 11.0) - 0.5) * 2
    clus = K.fbm(pts, 0.45, 2, 0.5, 23.0)
    ragn = K.fbm(pts, 0.9, 3, 0.5, 31.0)
    kept, loose, weeds = [], [], []
    for s, u, c, g in zip(stones, und, clus, ragn):
        x, z = s['x'], s['z']; rr = math.hypot(x, z); pw = square_path_weight(x, z)
        base = P.dish(rr) - 0.025 * drain_sink(x, z) ** 1.5
        top = base + 0.006 * u + r.gauss(0, 0.0018)
        tx, tz = r.gauss(0, 0.006), r.gauss(0, 0.006)
        si = stub_info(x, z)
        if si and si[1] < 0.2 and not in_square(x, z, -0.3): top -= 0.012 * (1 - si[1] / 0.2)   # the gutter's V
        e = edge_band(x, z) + (g - 0.5) * 0.6
        th = min(1.0, (1.0 - P.smoothstep(e) ** 0.6) * 1.1)
        if th > 0:
            if r.random() < th:
                if r.random() < 0.14: weeds.append(dict(p=[x, base - 0.02, z], kind='lot_edge' if lot_dist(x, z) < 1.8 else 'edge_grass'))
                if r.random() < 0.012 * th:    # a dislodged sett left lying in the dirt (r2: was 0.05, read as a field of pegs)
                    loose.append(dict(v=s['v'], x=x, z=z, a=r.uniform(0, 6.28), top=None, tilt=(r.uniform(0.2, 0.5), r.uniform(-0.3, 0.3)), s=s['s']))
                continue
            top -= th * r.uniform(0.004, 0.02); tx += r.gauss(0, 0.05 * th); tz += r.gauss(0, 0.05 * th)
        sink_p = 0.03 + 0.25 * P.smoothstep((c - 0.58) / 0.12)
        if r.random() < sink_p:
            top -= r.uniform(0.008, 0.022); tx += r.uniform(-1, 1) * 0.03; tz += r.uniform(-1, 1) * 0.03
        miss_p = 0.002 + 0.05 * P.smoothstep((c - 0.66) / 0.08) * (1 - pw)
        if r.random() < miss_p and s['v'] != 'small_a':
            weeds.append(dict(p=[x, base - 0.02, z], kind='missing_sett'))
            if r.random() < 0.45:
                ang = r.uniform(0, 2 * math.pi); d = r.uniform(0.18, 0.32)
                loose.append(dict(v=s['v'], x=x + d * math.cos(ang), z=z + d * math.sin(ang), a=r.uniform(0, 6.28),
                                  top=None, tilt=(r.uniform(0.2, 0.5), r.uniform(-0.3, 0.3)), s=s['s']))
            continue
        s.update(top=top, tilt=(tx, tz)); kept.append(s)
    # sparse joint weeds away from the worn lines, and grit/fragments in the gutters by the drains
    for _ in range(160):
        x, z = r.uniform(SQ[0], SQ[1]), r.uniform(SQ[2], SQ[3])
        if corner_edge(x, z) is None and math.hypot(x, z) > 5 and square_path_weight(x, z) < 0.2:
            weeds.append(dict(p=[x, P.dish(math.hypot(x, z)) - 0.01, z], kind='joint'))
    for dx, dz, _ in DRAINS:
        for _ in range(3):
            a = r.uniform(0, 6.28); d = r.uniform(0.45, 0.8)
            loose.append(dict(v=r.choice(['frag_a', 'frag_b']), x=dx + d * math.cos(a), z=dz + d * math.sin(a), a=r.uniform(0, 6.28),
                              top=None, tilt=(r.uniform(-0.35, 0.35), r.uniform(-0.35, 0.35)), s=r.uniform(0.8, 1.0)))
    return kept, loose, weeds

def records(kept, loose):
    """sc_cobbles.instance_all's transform maths without the Blender objects."""
    r = C.rng('square-loose'); recs = []
    for kind, lst in (('sett', kept), ('loose', loose)):
        for s in lst:
            v = s['v']; ground = P.dish(math.hypot(s['x'], s['z'])) - 0.025 * drain_sink(s['x'], s['z']) ** 1.5
            yaw_b = math.atan2(-math.sin(s['a']), -math.cos(s['a'])); tx, tz = s['tilt']
            Mb = Matrix.Rotation(tx, 4, 'X') @ Matrix.Rotation(tz, 4, 'Y') @ Matrix.Rotation(yaw_b, 4, 'Z')
            if kind == 'sett': y = s['top']
            elif C.VAR[v][4] == 'frag': y = ground - 0.004
            else:
                if r.random() < 0.25:                     # a few lie on their side, as in the corner
                    Mb = Matrix.Rotation(r.uniform(-1, 1) * 0.3, 4, 'Z') @ Matrix.Rotation(math.pi / 2 * r.choice((-1, 1)), 4, 'X') @ Matrix.Rotation(yaw_b, 4, 'Z')
                    y = ground + C.VAR[v][2] * s['s'] / 2 - 0.004
                else:                                     # most lie top-up, tipped and half sunk in the dirt (r2: on-side stones read as dark pegs)
                    Mb = Matrix.Rotation(r.uniform(0.15, 0.4) * r.choice((-1, 1)), 4, 'X') @ Matrix.Rotation(r.uniform(-0.2, 0.2), 4, 'Y') @ Matrix.Rotation(yaw_b, 4, 'Z')
                    y = ground + r.uniform(0.0, 0.03)
            sc = (s['s'], s['s'], 1.0 if kind == 'sett' else s['s'])
            recs.append(dict(v=v, kind=kind, p=[round(s['x'], 3), round(y, 4), round(s['z'], 3)], q=[round(c, 4) for c in P.rot_b2u_quat(Mb)],
                             s=[round(sc[0], 3), round(sc[2], 3), round(sc[1], 3)]))
    return recs

# ----------------------------------------------------------------------------- joint bed under the new setts
def build_bed(stones):
    """0.25 m grid over the cells that hold new setts (or their worn edge). Top -1.2 cm (over the -2 cm square slab, under the
    sett tops), rising to -0.4 cm where the setts thin out; vertex colour damp (R damp, G worn path, A edge fade) as PlazaBed."""
    step = 0.25
    nx, nz = int((GX1 - GX0) / step), int((GZ1 - GZ0) / step)
    have = np.zeros((nx, nz), dtype=bool)
    for s in stones:
        i, j = int((s['x'] - GX0) / step), int((s['z'] - GZ0) / step)
        have[max(0, i - 2):i + 3, max(0, j - 2):j + 3] = True      # a margin under the ragged edge
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); col = bm.loops.layers.color.new('damp'); vs = {}
    def vert(i, j):
        if (i, j) not in vs:
            xu, zu = GX0 + i * step, GZ0 + j * step
            fade = P.smoothstep(edge_band(xu, zu))
            y = P.dish(math.hypot(xu, zu)) - 0.025 * drain_sink(xu, zu) ** 1.5 - 0.008 * fade - 0.004
            vs[i, j] = bm.verts.new(P.u2b((xu, y, zu)))
        return vs[i, j]
    for i in range(nx):
        for j in range(nz):
            if not have[i, j]: continue
            xc, zc = GX0 + (i + 0.5) * step, GZ0 + (j + 0.5) * step
            ce = corner_edge(xc, zc)
            if ce is not None and ce > 0.6: continue                 # the corner keeps its own PlazaBed
            if math.hypot(xc, zc) < P.RING_R or lot_dist(xc, zc) < 0.05: continue
            f = bm.faces.new((vert(i, j), vert(i, j + 1), vert(i + 1, j + 1), vert(i + 1, j)))
            for lp in f.loops:
                b = lp.vert.co; xu, zu = -b.x, -b.y
                lp[uvl].uv = (xu / 2.0, zu / 2.0)
                pw = square_path_weight(xu, zu); d = min(1.0, 0.15 + 0.8 * drain_sink(xu, zu))
                lp[col] = (d * (1 - pw), pw, 0.0, P.smoothstep(edge_band(xu, zu)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = K._obj_from_bm('SquareBed', bm)
    if ob.data.polygons and ob.data.polygons[0].normal.z < 0: ob.data.flip_normals()
    ob.data.materials.append(P.bed_material('M_CobbleBed'))
    return ob

# ----------------------------------------------------------------------------- review plan (top-down map)
def plan_png(recs, corner, weeds, path):
    from PIL import Image, ImageDraw
    S = 20                                      # px per metre
    W, H = int((GX1 - GX0) * S), int((GZ1 - GZ0) * S)
    im = Image.new('RGB', (W, H), (92, 74, 56)); d = ImageDraw.Draw(im)
    def px(x, z): return ((x - GX0) * S, (GZ1 - z) * S)
    for _, s, x, z in LOTS: d.rectangle([px(x - s / 2, z + s / 2), px(x + s / 2, z - s / 2)], outline=(230, 220, 120), width=2)
    cx, cz, sx, sz = LANDMARKS['HeroesHome_Cistern']; d.rectangle([px(cx - sx / 2, cz + sz / 2), px(cx + sx / 2, cz - sz / 2)], outline=(200, 160, 255), width=2)
    for rec, colr in ((corner, (150, 150, 165)), (recs, None)):
        for st in rec:
            x, z = st['p'][0], st['p'][2]; v = st['v']
            c = colr or ((200, 190, 170) if v.startswith('path') else (90, 110, 130) if v.startswith('damp') else (240, 120, 60) if st.get('kind') == 'loose' else (175, 170, 168))
            d.point(px(x, z), fill=c)
    for w in weeds: x, z = w['p'][0], w['p'][2]; d.ellipse([px(x - 0.06, z + 0.06), px(x + 0.06, z - 0.06)], fill=(80, 160, 60))
    for dx, dz, _ in DRAINS: d.rectangle([px(dx - 0.25, dz + 0.25), px(dx + 0.25, dz - 0.25)], fill=(20, 20, 20))
    d.rectangle([px(SQ[0], SQ[3]), px(SQ[1], SQ[2])], outline=(255, 80, 80), width=1)
    im.save(path)

# ----------------------------------------------------------------------------- main
if __name__ == '__main__':
    K.reset()
    corner = json.load(open(CORNER_JSON))['stones']
    lay = SquareLayout(corner)
    print('SQUARE free cells', lay.n_free0, 'lots', [l[0] for l in LOTS], 'stubs', {k: [tuple(round(c, 1) for c in p) for p in v[1]] for k, v in STUB.items()})
    stones = lay.build_square()
    kept, loose, weeds = heights_and_damage(stones)
    recs = records(kept, loose)
    tris = sum(C.LOD0_T if r_['v'] not in ('frag_a', 'frag_b') else C.FRAG_T for r_ in recs)
    bed = build_bed(kept)
    stats = dict(setts=len(kept), loose=len(loose), weed_slots=len(weeds), drains=len(DRAINS), corner_stones=len(corner),
                 instance_tris_lod0=tris, bed_tris=K.tris(bed), square=SQ, stubs={k: v for k, v in STUBS})
    print('STATS', json.dumps(stats))
    with open(os.path.join(OUT, 'square_placements.json'), 'w') as f:
        json.dump(dict(units='metres, Unity axes, origin = fountain centre at the square shelf height; corner stones are in cobble_placements.json',
                       stats=stats, stones=recs, weed_slots=[dict(p=[round(c, 3) for c in w['p']], kind=w['kind']) for w in weeds],
                       drains=[dict(p=[x, round(P.dish(math.hypot(x, z)) - 0.025, 4), z], yaw=yaw) for x, z, yaw in DRAINS]), f, separators=(',', ':'))
    plan_png(recs, corner, weeds, os.path.join(OUT, 'plan.png'))
    for o in bpy.context.selected_objects: o.select_set(False)
    bed.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'SquareBed.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='FACE', colors_type='LINEAR')
    if '--export' in C.argv:
        os.makedirs(DEST, exist_ok=True)
        for fn in ('square_placements.json', 'SquareBed.fbx'):
            shutil.copy2(os.path.join(OUT, fn), os.path.join(DEST, fn))
        print('EXPORTED to', DEST)
