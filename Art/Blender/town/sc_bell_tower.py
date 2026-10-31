"""BellTower: Hollowmere's bell tower (T2 backdrop landmark), seen behind the facade chimney in the corner's wide shot.

A square rubble-stone tower about 17 m to the vane: a battered shaft (4.2 m square at the foot, 3.9 m under the
belfry) on a stepped plinth, chunky pillowy quoins at every corner, three projecting string courses, slit windows,
an arched oak door at the foot, and a storybook clock face; an open oak belfry stage (corner and middle posts, arch
braces, St Andrew's crosses under a rail) with a bronze bell hung from a headstock; a steep red-tile spire with a
bellcast kick, sagging faces, hip ridge tiles and a slight lean; a lead cap and an iron weathervane. Detail is even
on all four sides (the tower is seen at a slant from the plaza); the back carries the same cheap kit.

Built from the B4 facade kit helpers (sc_facade_kit: Piece, add_box, member, quad_grid, materials) on the shared
B1 tileables (sandstone tinted to rubble grey, oak_timber, roof_tile, iron tinted to bronze, plaster for the clock
face). T2: no unique bake, metre UVs (2 m per tile, 2K maps), wear in the linear float colour attribute "wear"
(R grime and run-off, G moss, B unused) like the facade kit.

refs: Reference/hollowmere/bell_tower/REFS.md (Rothenburg Weisser Turm: square rubble shaft, dressed quoins,
clock face, belfry under a steep spire; Glockenstapel spires: steep pyramids with a flared eave; bells: headstock
and yoke), approved corner captures in Reference/hollowmere/approved_corner/.

Axes (Blender): z up, front -Y, metres; origin at the bottom centre of the shaft. Unity: export -Z forward, Y up.
Top of the vane about 17.9 m; spire apex 16.9 m.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_bell_tower.py -- --out <scratch>
      [--rres 1600] [--samples 48] [--no-render] [--export] [--with-facade]
  --with-facade also builds the hero facade and renders the corner's wide camera with the tower behind it.
  --export writes <out>/BellTower.fbx. Cloud sessions commit only this generator.

Budget: under 40k tris (user cap), 2048 px maps.
Status (2026-10-31, cloud): round 2, self-review 28.5/40 (silhouette 4, proportion 4, form 3.5, surface 3.5,
colour 3.5, wear 3, set 4, metal 3; target 28). Round 1 was squat (pagoda spire, quoins lost in the wall, bell
unreadable): spire raised to 5.2 m (about 64 deg), rubble greyed so the pale quoins read, bell enlarged 25%.
About 22k tris, audit clean; ray-cast facing check from three cameras clean (eave fascia, solid belfry floor,
leaned belfry ceiling close every gap). Next: the bell still reads mostly as a dark shape at distance (a warmer,
brighter bronze or a lamp-lit belfry in Unity), a clock-face colour accent, louvres if the belfry reads too open.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT
RRES = FK.RRES; SAMPLES = FK.SAMPLES
argv = FK.argv

# ----------------------------------------------------------------------------- dimensions (metres)
SHAFT_H = 9.0
A0, A1 = 2.1, 1.95            # half width of the shaft at the foot and under the belfry (batter)
BANDS = [3.2, 6.3, SHAFT_H]   # string courses
BEL0, BEL1 = SHAFT_H + 0.22, 11.45   # belfry stage (floor top to wall plate)
PLATE = 0.25
SP0 = BEL1 + PLATE            # spire springing
SP_HALF = 2.5                 # half width at the eave edge (overhang)
SP_H = 5.2                    # springing to apex (steep, about 64 deg)
SP_LEAN = (1.6, -0.7)         # degrees about Y and X
CLOCK_Z = 7.55

def a_at(z):
    return A0 + (A1 - A0) * min(1.0, max(0.0, z / SHAFT_H))

EXTRA = {
    'rubble': lambda: FK.tile_mat('BT_Rubble', 'B1_town/sandstone', tint=(0.62, 0.60, 0.57), normal=1.4),   # grey rubble so the pale quoins read
    'bronze': lambda: FK.tile_mat('BT_Bronze', 'B1_town/iron', tint=(2.1, 1.5, 0.75)),
    'clock': lambda: FK.tile_mat('BT_ClockFace', 'B1_town/plaster', tint=(0.96, 0.93, 0.86), normal=0.4),
}
_orig_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)

def frame(n):
    """Face frame for an outward normal n (one of the four sides): tangent t = Z x n (left to right seen from outside)."""
    n = Vector(n); t = Vector((0, 0, 1)).cross(n)
    return n, t

SIDES = [(0, -1, 0), (1, 0, 0), (0, 1, 0), (-1, 0, 0)]

# ----------------------------------------------------------------------------- shaft
def kit_shaft():
    pc = FK.Piece('Tower_Shaft')
    # stepped plinth
    FK.add_box(pc, 'stone', (2 * A0 + 0.5, 2 * A0 + 0.5, 0.45), Matrix.Translation((0, 0, 0.225)), cham=0.06, segs=2,
               uv_world=True, wob=0.01, seed=1.0)
    FK.add_box(pc, 'stone', (2 * A0 + 0.24, 2 * A0 + 0.24, 0.35), Matrix.Translation((0, 0, 0.62)), cham=0.05, segs=2,
               uv_world=True, wob=0.01, seed=2.0)
    # battered rubble shaft
    def batter(p):
        t = (p.z + SHAFT_H / 2) / SHAFT_H
        k = (A0 + (A1 - A0) * t) / A0
        n = 0.02 * noise.noise(p * 0.9)
        return Vector((p.x * (k - 1) + n, p.y * (k - 1) + n, 0))
    FK.add_box(pc, 'rubble', (2 * A0, 2 * A0, SHAFT_H), Matrix.Translation((0, 0, SHAFT_H / 2)), cham=0.04,
               cuts=(3, 3, 12), bend=batter, uv_world=True, seed=3.0)
    # string courses
    for i, zb in enumerate(BANDS):
        a = a_at(zb) + 0.12
        FK.add_box(pc, 'stone', (2 * a, 2 * a, 0.22), Matrix.Translation((0, 0, zb + 0.02)), cham=0.045, segs=2,
                   uv_world=True, wob=0.006, seed=10.0 + i)
    return pc

def kit_quoins():
    pc = FK.Piece('Tower_Quoins')
    R = FK.rng('quoins')
    z = 0.8
    i = 0
    while z < SHAFT_H - 0.3:
        h = 0.40 + R.uniform(-0.03, 0.04)
        if any(zb - 0.02 < z + h / 2 < zb + 0.3 for zb in BANDS):
            z += 0.26; continue
        a = a_at(z + h / 2) + 0.035
        for sx in (-1, 1):
            for sy in (-1, 1):
                ln, sh = R.uniform(0.62, 0.78), R.uniform(0.34, 0.42)
                lx, ly = (ln, sh) if (i + (sx * sy > 0)) % 2 else (sh, ln)
                c = Vector((sx * (a - lx / 2 + 0.01), sy * (a - ly / 2 + 0.01), z + h / 2))
                FK.add_box(pc, 'stone', (lx, ly, h - 0.03), Matrix.Translation(c) @
                           Matrix.Rotation(math.radians(R.uniform(-1.5, 1.5)), 4, 'Z'), cham=0.045, segs=2,
                           uv_world=True, wob=0.008, seed=R.random() * 99)
        z += h; i += 1
    return pc

def opening(pc, n, u, z0, w, h, depth=0.18, mat_back='glass'):
    """A dark recessed opening with a dressed stone surround on side n at offset u."""
    n, t = frame(n)
    a = a_at(z0 + h / 2)
    c = n * (a + 0.004) + t * u
    # dark back
    p0 = c + t * (-w / 2) + Vector((0, 0, z0)); p1 = c + t * (w / 2) + Vector((0, 0, z0))
    FK.quad_grid(pc, mat_back, [p0 - n * depth, p1 - n * depth, p1 - n * depth + Vector((0, 0, h)),
                                p0 - n * depth + Vector((0, 0, h))], 1, 1, uv_fn=lambda q: (q.x, q.z))
    # reveals
    for sgn in (-1, 1):
        e = c + t * (sgn * w / 2)
        q = [e + Vector((0, 0, z0)), e - n * depth + Vector((0, 0, z0)), e - n * depth + Vector((0, 0, z0 + h)),
             e + Vector((0, 0, z0 + h))]
        FK.quad_grid(pc, 'stone', q if sgn > 0 else [q[1], q[0], q[3], q[2]], 1, 1,
                     uv_fn=lambda q_: ((q_.x + q_.y) / FK.TILE, q_.z / FK.TILE))
    # surround: jambs, sill, lintel (chunky, proud)
    rot = Matrix((t, n, Vector((0, 0, 1)))).transposed().to_4x4()
    for sgn in (-1, 1):
        FK.add_box(pc, 'stone', (0.16, 0.12, h + 0.2), Matrix.Translation(c + t * (sgn * (w / 2 + 0.08)) +
                   Vector((0, 0, z0 + h / 2))) @ rot, cham=0.025, uv_world=True, seed=u + sgn)
    FK.add_box(pc, 'stone', (w + 0.5, 0.16, 0.16), Matrix.Translation(c + n * 0.02 + Vector((0, 0, z0 - 0.08))) @ rot,
               cham=0.03, uv_world=True, seed=u + 5)
    FK.add_box(pc, 'stone', (w + 0.44, 0.13, 0.22), Matrix.Translation(c + Vector((0, 0, z0 + h + 0.11))) @ rot,
               cham=0.03, uv_world=True, seed=u + 7)

def kit_openings():
    pc = FK.Piece('Tower_Openings')
    for k, n in enumerate(SIDES):
        opening(pc, n, 0.0, 4.4, 0.24, 1.05)
        if n != (0, -1, 0):
            opening(pc, n, 0.0, 7.4, 0.24, 1.0)
    # the door at the foot of the front: oak planks with iron straps in a stone surround
    n, t = frame(SIDES[0]); a = a_at(1.0)
    c = n * (a + 0.004)
    w, h, z0 = 1.15, 2.2, 0.97
    FK.quad_grid(pc, 'stone', [c - n * 0.25 + t * (-w / 2) + Vector((0, 0, z0)), c - n * 0.25 + t * (w / 2) + Vector((0, 0, z0)),
                               c + t * (w / 2) + Vector((0, 0, z0)), c + t * (-w / 2) + Vector((0, 0, z0))], 1, 1,
                 uv_fn=lambda q: (q.x / FK.TILE, q.y / FK.TILE))
    rot = Matrix((t, n, Vector((0, 0, 1)))).transposed().to_4x4()
    R = FK.rng('tdoor')
    for i in range(5):
        x = -w / 2 + w * (i + 0.5) / 5
        FK.add_box(pc, 'oak', (w / 5 - 0.008, 0.06, h - R.uniform(0, 0.02)),
                   Matrix.Translation(c - n * 0.2 + t * x + Vector((0, 0, z0 + h / 2))) @ rot, cham=0.01, cuts=(0, 0, 3),
                   long_axis=2, wob=0.004, seed=R.random() * 50)
    for zz in (z0 + 0.4, z0 + h - 0.4):
        FK.add_box(pc, 'iron', (w * 0.8, 0.014, 0.07), Matrix.Translation(c - n * 0.165 + t * (-w * 0.08) +
                   Vector((0, 0, zz))) @ rot, cham=0.004, long_axis=0, seed=zz)
    for sgn in (-1, 1):
        FK.add_box(pc, 'stone', (0.26, 0.16, h + 0.1), Matrix.Translation(c + t * (sgn * (w / 2 + 0.13)) +
                   Vector((0, 0, z0 + h / 2))) @ rot, cham=0.035, segs=2, uv_world=True, seed=40 + sgn)
    # a chunky lintel with a shallow relieving arch of voussoirs
    FK.add_box(pc, 'stone', (w + 0.6, 0.18, 0.3), Matrix.Translation(c + Vector((0, 0, z0 + h + 0.15))) @ rot,
               cham=0.04, segs=2, uv_world=True, seed=44)
    for j in range(7):
        ang = math.pi * (0.15 + 0.7 * j / 6)
        p = c + t * (math.cos(ang) * (w / 2 + 0.25)) + Vector((0, 0, z0 + h + 0.3 + math.sin(ang) * 0.45))
        FK.add_box(pc, 'stone', (0.24, 0.14, 0.34), Matrix.Translation(p) @ rot @
                   Matrix.Rotation(math.pi / 2 - ang, 4, 'Y'), cham=0.03, uv_world=True, seed=50 + j)
    # worn step
    FK.add_box(pc, 'stone', (w + 0.7, 0.55, 0.2), Matrix.Translation(c + n * 0.3 + Vector((0, 0, 0.87))) @ rot,
               cham=0.04, segs=2, uv_world=True, seed=60)
    return pc

def kit_clock():
    """Storybook clock face on the front: limewashed disc in a stone ring, iron chapter marks and hands."""
    pc = FK.Piece('Tower_Clock')
    n, t = frame(SIDES[0]); a = a_at(CLOCK_Z)
    c = n * (a + 0.07) + Vector((0, 0, CLOCK_Z))
    rot = Matrix((t, n, Vector((0, 0, 1)))).transposed().to_4x4()
    seg = 28; r = 0.62
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    ctr = bm.verts.new(c + n * 0.01)
    ring = [bm.verts.new(c + t * (math.cos(2 * math.pi * k / seg) * r) + Vector((0, 0, math.sin(2 * math.pi * k / seg) * r)))
            for k in range(seg)]
    for k in range(seg):
        f = bm.faces.new((ctr, ring[k], ring[(k + 1) % seg]))
        for l in f.loops:
            l[uvl].uv = (l.vert.co.x / FK.TILE, l.vert.co.z / FK.TILE)
    bm.normal_update(); FK._merge(pc, bm, 'clock')
    # stone ring of voussoirs
    for k in range(16):
        ang = 2 * math.pi * k / 16
        p = c + t * (math.cos(ang) * (r + 0.13)) + Vector((0, 0, math.sin(ang) * (r + 0.13)))
        FK.add_box(pc, 'stone', (0.24, 0.16, 0.22), Matrix.Translation(p - n * 0.04) @ rot @
                   Matrix.Rotation(-ang, 4, 'Y'), cham=0.03, uv_world=True, seed=70 + k)
    # chapter marks and hands (ten past ten, the storybook hour)
    for k in range(12):
        ang = 2 * math.pi * k / 12
        p = c + n * 0.02 + t * (math.cos(ang) * (r - 0.1)) + Vector((0, 0, math.sin(ang) * (r - 0.1)))
        FK.add_box(pc, 'iron', (0.11 if k % 3 == 0 else 0.07, 0.015, 0.03), Matrix.Translation(p) @ rot @
                   Matrix.Rotation(-ang, 4, 'Y'), cham=0.0, long_axis=0, seed=k)
    for ang, ln, wd in ((math.radians(150), 0.34, 0.055), (math.radians(30), 0.5, 0.04)):
        p = c + n * 0.035 + t * (math.cos(ang) * ln / 2) + Vector((0, 0, math.sin(ang) * ln / 2))
        FK.add_box(pc, 'iron', (ln, 0.015, wd), Matrix.Translation(p) @ rot @ Matrix.Rotation(-ang, 4, 'Y'), cham=0.0,
                   long_axis=0, seed=ang)
    FK.add_box(pc, 'iron', (0.07, 0.03, 0.07), Matrix.Translation(c + n * 0.045), cham=0.01, seed=99)
    return pc

# ----------------------------------------------------------------------------- belfry
def kit_belfry():
    pc = FK.Piece('Tower_Belfry')
    a = A1 - 0.02
    P = 0.32
    zr = BEL0 + 0.95
    for sx in (-1, 1):
        for sy in (-1, 1):
            FK.member(pc, 'oak', (sx * (a - P / 2), sy * (a - P / 2), BEL0), (sx * (a - P / 2), sy * (a - P / 2), BEL1),
                      P, P, name='bp%d%d' % (sx, sy), up=(0, -1, 0) if sy < 0 else (0, 1, 0))
    for k, n in enumerate(SIDES):
        n, t = frame(n)
        base = n * (a - P / 2)
        FK.member(pc, 'oak', base + Vector((0, 0, BEL0)), base + Vector((0, 0, BEL1)), 0.26, P, name='bm%d' % k,
                  up=tuple(n))
        # sole, rail and wall plate rings
        for zz, sec, nm in ((BEL0 + 0.12, 0.24, 'sole'), (zr, 0.18, 'rail'), (BEL1 + PLATE / 2, PLATE, 'plate')):
            FK.member(pc, 'oak', base + t * (-a) + Vector((0, 0, zz)), base + t * a + Vector((0, 0, zz)), sec, P,
                      name='%s%d' % (nm, k), up=tuple(n), extend=0.04, sag=0.02 if nm == 'plate' else 0.0)
        # St Andrew's crosses under the rail, arch braces under the plate
        for half in (-1, 1):
            u0, u1 = (half * (a - P), half * P / 2) if half < 0 else (half * P / 2, half * (a - P))
            lo, hi = min(u0, u1), max(u0, u1)
            yb = base + n * 0.01
            FK.member(pc, 'oak', yb + t * lo + Vector((0, 0, BEL0 + 0.24)), yb + t * hi + Vector((0, 0, zr - 0.09)), 0.12,
                      0.2, name='x%d%da' % (k, half), cham=0.015, up=tuple(n))
            FK.member(pc, 'oak', yb + t * hi + Vector((0, 0, BEL0 + 0.24)), yb + t * lo + Vector((0, 0, zr - 0.09)), 0.12,
                      0.2, name='x%d%db' % (k, half), cham=0.015, up=tuple(n))
            for e in (lo, hi):
                inward = 1 if e == lo else -1
                FK.member(pc, 'oak', yb + t * e + Vector((0, 0, BEL1 - 0.6)), yb + t * (e + inward * 0.5) +
                          Vector((0, 0, BEL1)), 0.16, 0.2, name='ab%d%d%d' % (k, half, inward), cham=0.02, up=tuple(n))
    # floor (stone flags on the top string course) and a ceiling under the spire
    FK.add_box(pc, 'stone', (2 * a + 0.1, 2 * a + 0.1, 0.24), Matrix.Translation((0, 0, BEL0 - 0.13)), cham=0.0,
               uv_world=True, seed=4)   # solid floor slab: no gap under the sole plates

    # bell frame: two trestles and a headstock across x
    zh = BEL1 - 0.55
    for sy in (-1, 1):
        FK.member(pc, 'oak', (-a + P, sy * 0.45, zh), (a - P, sy * 0.45, zh), 0.22, 0.22, name='hs%d' % sy, up=(0, sy, 0))
        for sx in (-1, 1):
            FK.member(pc, 'oak', (sx * 0.95, sy * 0.45, BEL0), (sx * 0.62, sy * 0.45, zh - 0.1), 0.18, 0.18,
                      name='tr%d%d' % (sx, sy), up=(0, sy, 0))
    FK.add_box(pc, 'oak', (0.5, 1.1, 0.26), Matrix.Translation((0, 0, zh + 0.02)), cham=0.03, long_axis=1, seed=5)
    # the bell: a turned profile with a sound bow, crown loops and a clapper
    prof = [(0.06, 0.0), (0.2, 0.01), (0.27, 0.06), (0.30, 0.18), (0.33, 0.36), (0.40, 0.55), (0.50, 0.70),
            (0.55, 0.78), (0.54, 0.84), (0.48, 0.84), (0.44, 0.78), (0.36, 0.62), (0.28, 0.40), (0.24, 0.18),
            (0.18, 0.08), (0.05, 0.06)]
    seg = 20; top = zh - 0.12
    prof = [(r * 1.25, zz * 1.2) for (r, zz) in prof]   # a big storybook bell that reads from the plaza
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rings = [[bm.verts.new(Vector((r * math.cos(2 * math.pi * s / seg), r * math.sin(2 * math.pi * s / seg), top - zz)))
              for s in range(seg)] for (r, zz) in prof]
    for j in range(len(rings) - 1):
        for s in range(seg):
            f = bm.faces.new((rings[j][s], rings[j + 1][s], rings[j + 1][(s + 1) % seg], rings[j][(s + 1) % seg]))
            for l in f.loops:
                l[uvl].uv = (math.atan2(l.vert.co.y, l.vert.co.x) * 0.5 / FK.TILE, l.vert.co.z / FK.TILE)
    cap = bm.faces.new(rings[0])
    for l in cap.loops:
        l[uvl].uv = (l.vert.co.x, l.vert.co.y)
    bm.normal_update(); FK._merge(pc, bm, 'bronze')
    FK.add_box(pc, 'bronze', (0.12, 0.06, 0.14), Matrix.Translation((0, 0, top + 0.05)), cham=0.02, seed=7)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.09)
    bm.transform(Matrix.Translation((0.03, 0, top - 0.92))); bm.loops.layers.uv.new('UVMap'); FK._merge(pc, bm, 'iron')
    FK.add_box(pc, 'iron', (0.03, 0.03, 0.62), Matrix.Translation((0.015, 0, top - 0.42)), cham=0.0, seed=8)
    return pc

# ----------------------------------------------------------------------------- spire
def spire_pt(n, t, u, s):
    """Point on the spire face with outward normal n: u in [-1, 1] across, s in [0, 1] eave to apex."""
    hw = SP_HALF * (1 - s)
    kick = 0.22 * max(0.0, 1 - s / 0.16) ** 2               # bellcast: the eave flares out and flattens
    sag = 0.07 * math.sin(math.pi * s) * math.cos(math.pi * u / 2)   # faces sag between the hips
    out = hw + 0.28 * max(0.0, 1 - s / 0.16) ** 2
    return n * (out - sag) + t * (u * out) + Vector((0, 0, SP0 + s * SP_H - kick * 0.4))

def kit_spire():
    pc = FK.Piece('Tower_Spire')
    R = FK.rng('spire')
    for k, n0 in enumerate(SIDES):
        n, t = frame(n0)
        # deck
        FK.quad_grid(pc, 'roof', [(0, 0, 0)] * 4, 8, 10, push=lambda p, u, v, n=n, t=t: spire_pt(n, t, 2 * u - 1, v),
                     uv_fn=lambda q, t=t: (q.dot(t) / FK.TILE, q.z / FK.TILE))
        # underside of the overhang
        a = A1 - 0.12   # starts inside the wall plate so the leaned spire never opens a gap above the belfry
        def under(p, u, v, n=n, t=t):
            e = spire_pt(n, t, 2 * u - 1, 0.0)
            w = n * a + t * ((2 * u - 1) * a) + Vector((0, 0, SP0 - 0.14))
            return w.lerp(e - Vector((0, 0, 0.12)), v)
        FK.quad_grid(pc, 'oak', [(0, 0, 0)] * 4, 6, 1, push=lambda p, u, v, f=under: f(p, u, v),
                     uv_fn=lambda q: (q.x / FK.TILE, q.y / FK.TILE))
        # fascia closing the slab edge between the deck and its underside along the eave
        FK.quad_grid(pc, 'oak', [(0, 0, 0)] * 4, 8, 1,
                     push=lambda p, u, v, n=n, t=t: spire_pt(n, t, 2 * u - 1, 0.0) - Vector((0, 0, 0.12 * (1 - v))),
                     uv_fn=lambda q, t=t: (q.dot(t) / FK.TILE, q.z / FK.TILE))
        # tile courses
        nc = 13
        for c in range(nc):
            s0, s1 = c / nc * 0.97, (c + 1) / nc * 0.97 + 0.02
            runs = max(2, int(12 * (1 - s0)))
            bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
            for i in range(runs):
                ua, ub = -1 + 2 * i / runs + 0.004, -1 + 2 * (i + 1) / runs - 0.004
                j = R.uniform(-0.01, 0.02) if c else R.uniform(0.0, 0.05)
                nn = (n * SP_H + Vector((0, 0, SP_HALF))).normalized()
                pa, pb = spire_pt(n, t, ua, s0) + nn * (0.045 + j), spire_pt(n, t, ub, s0) + nn * (0.045 + j)
                ta, tb = spire_pt(n, t, ua * (1 - s1) / (1 - s0), min(1, s1)), spire_pt(n, t, ub * (1 - s1) / (1 - s0), min(1, s1))
                v = [bm.verts.new(q) for q in (pa, pb, tb, ta)]
                f = bm.faces.new(v)
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.dot(t) / FK.TILE, (c * 0.4 + (l.vert.co.z - pa.z)) / FK.TILE)
                u0, u1 = bm.verts.new(pa - nn * 0.05), bm.verts.new(pb - nn * 0.05)
                f = bm.faces.new((u0, u1, v[1], v[0]))
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.dot(t) / FK.TILE, l.vert.co.z / FK.TILE)
            bm.normal_update(); FK._merge(pc, bm, 'roof')
        # hip ridge: a run of half-round tiles up the corner between this face and the next
        n2, t2 = frame(SIDES[(k + 1) % 4])
        nh = 11
        for i in range(nh):
            s_a, s_b = i / nh * 0.96, (i + 1) / nh * 0.96 + 0.015
            pa, pb = spire_pt(n, t, 1, s_a), spire_pt(n, t, 1, s_b)
            ax = (pb - pa).normalized(); out = (n + n2).normalized()
            side = ax.cross(out).normalized(); out = side.cross(ax).normalized()
            bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); segs = 6; r = 0.1
            rows = []
            for e, pp in ((0, pa), (1, pb)):
                rows.append([bm.verts.new(pp + side * (math.cos(math.pi * q / segs) * r) + out * (math.sin(math.pi * q / segs) * r * 0.9))
                             for q in range(segs + 1)])
            for q in range(segs):
                f = bm.faces.new((rows[0][q], rows[0][q + 1], rows[1][q + 1], rows[1][q]))
                for l in f.loops:
                    l[uvl].uv = (q * r / FK.TILE, (l.vert.co - pa).length / FK.TILE)
            bm.normal_update(); FK.two_sided(bm); FK._merge(pc, bm, 'roof')
    # ceiling over the belfry (leans with the spire), closing the hollow inside
    c = A1 - 0.1
    FK.quad_grid(pc, 'oak', [(-c, c, SP0 - 0.13), (c, c, SP0 - 0.13), (c, -c, SP0 - 0.13), (-c, -c, SP0 - 0.13)], 3, 3,
                 uv_fn=lambda q: (q.x / FK.TILE, q.y / FK.TILE))
    # lead cap and weathervane
    apex = Vector((0, 0, SP0 + SP_H))
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.24, radius2=0.03, depth=0.55)
    bm.transform(Matrix.Translation(apex + Vector((0, 0, 0.12)))); bm.loops.layers.uv.new('UVMap'); FK._merge(pc, bm, 'iron')
    FK.add_box(pc, 'iron', (0.035, 0.035, 0.95), Matrix.Translation(apex + Vector((0, 0, 0.75))), cham=0.0, seed=1)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.11)
    bm.transform(Matrix.Translation(apex + Vector((0, 0, 0.5)))); bm.loops.layers.uv.new('UVMap'); FK._merge(pc, bm, 'bronze')
    for ang in (0, math.pi / 2):
        FK.add_box(pc, 'iron', (0.6, 0.02, 0.02), Matrix.Translation(apex + Vector((0, 0, 0.72))) @
                   Matrix.Rotation(ang, 4, 'Z'), cham=0.0, long_axis=0, seed=ang)
    vz = apex + Vector((0, 0, 1.05))
    rot = Matrix.Rotation(math.radians(-25), 4, 'Z')
    FK.add_box(pc, 'iron', (1.0, 0.018, 0.03), Matrix.Translation(vz) @ rot, cham=0.0, long_axis=0, seed=2)
    FK.add_box(pc, 'iron', (0.16, 0.018, 0.16), Matrix.Translation(vz) @ rot @ Matrix.Translation((0.52, 0, 0)) @
               Matrix.Rotation(math.pi / 4, 4, 'Y'), cham=0.0, seed=3)
    FK.add_box(pc, 'iron', (0.3, 0.012, 0.24), Matrix.Translation(vz) @ rot @ Matrix.Translation((-0.42, 0, 0.02)),
               cham=0.0, seed=4)
    # lean the whole spire (and its vane) a little about its springing
    lean = Matrix.Translation((0, 0, SP0)) @ Matrix.Rotation(math.radians(SP_LEAN[0]), 4, 'Y') @ \
        Matrix.Rotation(math.radians(SP_LEAN[1]), 4, 'X') @ Matrix.Translation((0, 0, -SP0))
    bmesh.ops.transform(pc.bm, matrix=lean, verts=pc.bm.verts[:])
    return pc

# ----------------------------------------------------------------------------- wear
def tower_wear(ob):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    mw = ob.matrix_world
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 1.7); streak = noise.noise(Vector((p.x * 4.0, p.y * 4.0, p.z * 0.5)))
            r = max(0.0, 1 - p.z / (1.6 + 0.3 * nz)) ** 1.1 * 0.8
            r = max(r, 0.2 * max(0.0, noise.noise(p * 0.4 + Vector((2.0, 5.0, 1.0)))) + 0.06 * max(0.0, nz))
            for zb in BANDS:                                    # run-off below each string course
                dz = zb - p.z
                if 0.0 < dz < 1.1:
                    r = max(r, (1 - dz / 1.1) ** 1.5 * (0.32 + 0.25 * streak))
            g = 0.0
            if n.z > 0.5 and ob.name in ('Tower_Shaft', 'Tower_Quoins', 'Tower_Openings'):
                g = 0.65
            if ob.name == 'Tower_Spire' and n.z > 0.3:
                s = (p.z - SP0) / SP_H
                g = max(0.0, 1 - s * 3.0) * 0.8 + max(0.0, 0.1 + 0.5 * nz) * 0.25
                tv = ((poly.index * 2654435761) % 1009) / 1009.0
                r = max(r, 0.3 * tv * tv)
            attr.data[li].color = (min(1, r), min(1, g), 0.0, 1.0)

# ----------------------------------------------------------------------------- assembly
def build_tower(parent_loc=None):
    root = bpy.data.objects.new('BellTower', None); bpy.context.scene.collection.objects.link(root)
    total = 0
    for pc in (kit_shaft(), kit_quoins(), kit_openings(), kit_clock(), kit_belfry(), kit_spire()):
        me = bpy.data.meshes.new(pc.name)
        pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
        for m in pc.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob); ob.parent = root
        tower_wear(ob)
        t = sum(len(p.vertices) - 2 for p in me.polygons); total += t
        print('  %-16s %6d tris  %s' % (pc.name, t, ', '.join(m.name for m in pc.mats)))
    print('BellTower total tris: %d (cap 40000)' % total)
    if parent_loc is not None:
        root.location = parent_loc
    return root

TOWER_AT = FK.U(-4.0, 0, 27.0)   # review placement behind the facade chimney (the controller places it in Unity)
VIEWS = {
    'tfront': (Vector((0, -26, 6.0)), Vector((0, 0, 8.6)), 42),
    'ttq': (Vector((16, -19, 4.0)), Vector((0, 0, 9.0)), 42),
    'tbelfry': (Vector((6.5, -9.5, 10.5)), Vector((0, 0, 11.6)), 36),
}

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with_facade = '--with-facade' in argv
    if with_facade:
        froot, _ = FK.build()
    troot = build_tower()
    if '--export' in argv:
        for o in bpy.context.scene.objects:
            o.select_set(o.name.startswith('Tower_') or o == troot)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'BellTower.fbx'), use_selection=True, axis_forward='-Z',
                                 axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
                                 mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR',
                                 path_mode='STRIP')   # no texture paths: Unity's material import renames every textured slot 'Lit' when an absolute path resolves on its machine
    if '--no-render' not in argv:
        if with_facade:
            cam = FK.review_scene(froot)
            troot.location = TOWER_AT
            views = {'wide': FK.VIEWS_DEF['wide']}
        else:
            cam = FK.review_scene(troot, variant=True)
            views = VIEWS
        for v, (frm, to, fov) in views.items():
            FK.look(cam, frm, to); cam.data.angle = math.radians(fov)
            bpy.context.scene.render.filepath = os.path.join(OUT, 'tower_%s.png' % v)
            bpy.ops.render.render(write_still=True)
            print('rendered', v)
