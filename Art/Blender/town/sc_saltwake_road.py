"""Saltwake road props (Q15): the wayside dressing on the road outside Hollowmere's Lower Gate (T3 instanced pieces, seen from the road at 5-15 m).

Original design, Dark Cloud whimsy in OoT-2026 stylised realism: chunky 1.4x timbers, 1-3 deg off true, placed wear. Real geometry on the shared
B1 tileables with metre UVs (no unique bake); the linear "wear" colour attribute carries placed wear (R grime, G moss, B polish). Built on
sc_town_wall.py's helpers (masonry, boxes, rocks, wear) and sc_facade_kit.py's materials: the slots are the Unity material names.

Pieces (each its own FBX, one mesh object named like the piece). Axes (Blender): z up, metres, pivot at the ground centre of the footprint,
local ground 0, FRONT (the side facing the road) = -Y, parts buried 0.15-0.3 m below 0.
  Milestone_A / _B      squat carved sandstone stone with a worn up-arrow and a notch tally / tapered grey post, pyramid cap, iron band
  Shrine / Shrine_Ruined  2.7 m wayside shrine: rubble pier on a two-step plinth, ledge with candle stubs and a jar, niche with a curled
                        sleeping-beast carving, steep sagging tile roof / the same pier with the roof slipped, tiles gone, crack, heavy moss
  Cart / Cart_Broken    two-wheeled oak farm cart with iron tyres (repaired, level, shafts on a block) / tipped on its left (-Y) side, left wheel
                        snapped and lying flat beside it with two broken spokes, loose planks
  Fence_Rail / _Broken  3 m post-and-rail segment (x -1.5..1.5; posts at x -1.5 and 0, rails run on to 1.5 so segments chain) / one rail down, post leaning 15 deg
  Hedge_A / Hedge_B     4 m hedgerow segments (x about -2.25..2.25), 1.2 m deep, 1.4-1.8 m tall: B9 ivy leaf-mass cards (shell + inner), woody stems
Far-tree impostor atlas: 4 broad deciduous silhouettes from the same B9 cards (orthographic, 2048 x 1024, 4 cells of 512 x 1024),
  Assets/_Project/Content/Art/Town/Road/FarTrees/T_Road_FarTrees_BaseColor.png (+ _Normal.png).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_saltwake_road.py -- --out <scratch> [--export] [--trees]
      [--only <Piece>] [--no-render] [--rres 960] [--samples 24] [--tag r1]
  --export writes Assets/_Project/Content/Art/Town/Road/<Piece>/<Piece>.fbx (+ FarTrees with --trees); renders go to <out>/shot_*.png.
Tris: see Assets/_Project/Content/Art/Town/Road/SOURCES.md (printed per piece at build).
Status (2026-10-31, cloud): see the round log at the end of this docstring.
"""
import sys, os, math, random, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, Euler, noise
import sc_facade_kit as FK
import sc_town_wall as TW          # importable: its generators only run under __main__
import sc_b9_lib as L

OUT = FK.OUT; argv = FK.argv
REPO = FK.REPO
EXP = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'Road')
B9 = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B9', 'Grass_A', 'T_B9_Foliage')
IVY_CELLS = [(0.0, 0.25, 0.25, 0.5), (0.25, 0.25, 0.5, 0.5)]   # B9 atlas: green leaf masses
IVY_DARK = (0.0, 0.0, 0.25, 0.25)                               # the shaded inner layer

def R_(s):
    return random.Random(0x5A17 ^ zlib.crc32(s.encode()))

# ----------------------------------------------------------------------------- materials: the Unity slot names, plus the B9 foliage atlas
_B9 = {}
def b9_mat():
    if 'm' not in _B9:
        _B9['m'] = L.pbr_material('B9_Foliage', B9)
    return _B9['m']
TW.EXTRA['b9'] = b9_mat
FK_M = FK.M   # already patched by sc_town_wall (TW_ and FK_ keys)

# ----------------------------------------------------------------------------- helpers
def V(*a):
    return Vector(a)

def T(x, y, z):
    return Matrix.Translation((x, y, z))

def RE(rx=0.0, ry=0.0, rz=0.0):
    return Euler((rx, ry, rz), 'XYZ').to_matrix().to_4x4()

def bx(pc, key, size, c, rot=(0, 0, 0), cham=0.02, wob=0.006, seed=0.0, uv_world=True, **kw):
    """Chamfered, wobbled box of `size` centred at c, euler rot (radians)."""
    TW.box(pc, key, size, T(*c) @ RE(*rot), cham=cham, wob=wob, seed=seed, uv_world=uv_world, **kw)

def tim(pc, key, a, b, w, d, name, up=(0, -1, 0), sag=0.0, cham=0.02, wob=0.007):
    """A timber from a to b; section w (across) x d (along up)."""
    FK.member(pc, key, a, b, w, d, cham=cham, name=name, sag=sag, wob=wob, up=up)

def convex(pc, key, pts, faces, R=None):
    pts = [Vector(p) for p in pts]; c = sum(pts, Vector()) / len(pts)
    off = (R.random(), R.random()) if R else (0.0, 0.0)
    for f in faces:
        TW.add_face(pc, key, [pts[i] for i in f], ref=c, off=off)

BOXF = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]

def prism_ring(pc, key, M, r0, r1, hw, n, a_from=0.0, a_to=2 * math.pi, gap=0.0, skip=(), R=None):
    """Ring segments around local z (r0..r1, half width hw), placed by matrix M."""
    for k in range(n):
        if k in skip:
            continue
        a0 = a_from + (a_to - a_from) * k / n + gap; a1 = a_from + (a_to - a_from) * (k + 1) / n - gap
        pts = []
        for a in (a0, a1):
            for r in (r0, r1):
                for z in (-hw, hw):
                    pts.append(M @ Vector((r * math.cos(a), r * math.sin(a), z)))
        convex(pc, key, pts, [(0, 1, 3, 2), (4, 5, 7, 6), (0, 1, 5, 4), (2, 3, 7, 6), (0, 2, 6, 4), (1, 3, 7, 5)], R)

def lathe(pc, key, M, prof, n=10, R=None, seed=0.0):
    """Revolve [(r, z), ...] about local z; placed by M. First/last r 0 closes it."""
    rings = []
    for (r, z) in prof:
        rings.append([M @ Vector((r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n), z)) for i in range(n)])
    off = (R.random(), R.random()) if R else (0.0, 0.0)
    for j in range(len(prof) - 1):
        zc = M @ Vector((0, 0, (prof[j][1] + prof[j + 1][1]) / 2))
        for i in range(n):
            i2 = (i + 1) % n
            q = [rings[j][i], rings[j][i2], rings[j + 1][i2], rings[j + 1][i]]
            TW.add_face(pc, key, q, ref=zc, off=off)
    cen = sum([M @ Vector((0, 0, p[1])) for p in prof], Vector()) / len(prof)
    for ring in (rings[0], rings[-1]):
        if prof[0][0] > 1e-4 and ring is rings[0]:
            TW.add_face(pc, key, ring, ref=cen, off=off)
        if prof[-1][0] > 1e-4 and ring is rings[-1]:
            TW.add_face(pc, key, ring, ref=cen, off=off)

_ICO = {}
def ico_faces(sub):
    if sub not in _ICO:
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
        _ICO[sub] = ([v.co.copy() for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces]); bm.free()
    return _ICO[sub]

def ellip(pc, key, c, r, rot=(0, 0, 0), sub=1, R=None, jit=0.0):
    """Ellipsoid (icosphere scaled to half sizes r), euler rot, centre c."""
    vs, fs = ico_faces(sub)
    M = T(*c) @ RE(*rot)
    pts = [M @ Vector((v.x * r[0] * (1 + (R.uniform(-jit, jit) if R else 0)), v.y * r[1] * (1 + (R.uniform(-jit, jit) if R else 0)),
                       v.z * r[2] * (1 + (R.uniform(-jit, jit) if R else 0)))) for v in vs]
    off = (R.random(), R.random()) if R else (0.0, 0.0)
    for f in fs:
        TW.add_face(pc, key, [pts[i] for i in f], ref=Vector(c), off=off)

def transform_pc(pc, M):
    bmesh.ops.transform(pc.bm, matrix=M, verts=pc.bm.verts[:])
    pc.bm.normal_update()

def rrect(w, d, rad, n=3):
    pts = []
    for (cx, cy, a0) in ((w / 2 - rad, d / 2 - rad, 0), (-w / 2 + rad, d / 2 - rad, 90), (-w / 2 + rad, -d / 2 + rad, 180), (w / 2 - rad, -d / 2 + rad, 270)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + rad * math.cos(a), cy + rad * math.sin(a)))
    return pts

def tiles(pc, p00, p10, p11, p01, R, ref, course=0.2, key='roof', skip=None, sag=None, lift=0.04):
    """Coursed tile roof over a quad: stepped courses, one face per tile, skip(u, v) drops a tile, sag(p) -> dz."""
    p00, p10, p11, p01 = [Vector(p) for p in (p00, p10, p11, p01)]
    Lu = max((p10 - p00).length, (p11 - p01).length); Lv = max((p01 - p00).length, (p11 - p10).length)
    nu = max(2, round(Lu / 0.3)); nc = max(2, round(Lv / course)); nv = nc * 2
    nrm = (p10 - p00).cross(p01 - p00).normalized()
    if nrm.dot((p00 + p10 + p11 + p01) / 4 - ref) < 0:
        nrm = -nrm
    grid = []
    for j in range(nv + 1):
        row = []
        for i in range(nu + 1):
            u, v = i / nu, j / nv
            p = p00.lerp(p10, u).lerp(p01.lerp(p11, u), v)
            p = p + nrm * (lift * (j % 2) + 0.012 * R.uniform(-1, 1)) + Vector((0, 0, 0))
            if sag:
                p.z += sag(p) * (0.4 + 0.6 * v)
            row.append(p)
        grid.append(row)
    for j in range(nv):
        for i in range(nu):
            if skip and skip((i + 0.5) / nu, (j + 0.5) / nv):
                continue
            q = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
            TW.add_face(pc, key, q, ref=ref)

# ----------------------------------------------------------------------------- wear (props: grime at the foot, moss on tops, lichen low)
def prop_obj(pc, name, seed, grime=1.0, moss=1.0):
    return TW.make_obj(pc, name, seed=seed, grime=grime, moss=moss)

# ============================================================================= 1-2. MILESTONES
def milestone_a():
    R = R_('msA'); pc = FK.Piece('Milestone_A')
    prof = [(-0.25, 1.04), (0.0, 1.04), (0.1, 1.0), (0.45, 0.96), (0.7, 0.93), (0.82, 0.88), (0.89, 0.76), (0.93, 0.55), (0.95, 0.28)]
    secs = [(z, TW and [(x * s, y * s) for (x, y) in rrect(0.64, 0.42, 0.11, 3)]) for z, s in prof]
    bm = TW.loft_bm(secs, lambda s, a, b: Vector((a, b, s)))
    for v in bm.verts:
        q = v.co * 6.0
        j = 0.014 if v.co.z < 0.9 else 0.02
        v.co += Vector((noise.noise(q), noise.noise(q + Vector((4, 1, 2))), noise.noise(q + Vector((7, 3, 1))) * 0.6)) * j
        if v.co.z > 0.7 and v.co.y < -0.08 and v.co.x > 0.12:      # a chipped top-front corner
            v.co += Vector((-0.04, 0.035, -0.035)) * (v.co.z - 0.6)
    bm.normal_update()
    TW.merge_bm(pc, bm, 'stone')
    def front(z):   # front plane (-Y) at height z
        for (z0, s0), (z1, s1) in zip(prof, prof[1:]):
            if z0 <= z <= z1:
                s = s0 + (s1 - s0) * (z - z0) / (z1 - z0); return -0.21 * s
        return -0.2
    # carved up-arrow (dark grooves) and a notch tally
    for (x0, z0, w, h) in ((0.0, 0.43, 0.06, 0.30), ):
        bx(pc, 'mortar', (w, 0.03, h), (x0, front(z0) - 0.004, z0), cham=0.004, wob=0.002, seed=1)
    for sgn in (-1, 1):   # arrow head as two diagonals
        TW.box(pc, 'mortar', (0.05, 0.03, 0.2), T(0.065 * sgn, front(0.66) - 0.004, 0.65) @ RE(0, sgn * 0.62, 0) , cham=0.004, wob=0.002, seed=2 + sgn)
    for i in range(4):
        bx(pc, 'mortar', (0.03, 0.03, 0.13), (-0.15 + i * 0.07, front(0.2) - 0.004, 0.19), cham=0.003, wob=0.002, seed=9 + i)
    TW.box(pc, 'mortar', (0.03, 0.03, 0.24), T(-0.04, front(0.2) - 0.004, 0.19) @ RE(0, 1.2, 0), cham=0.003, wob=0.002, seed=15)
    # a worn band under the dome and a few stones at the foot
    bx(pc, 'coping', (0.5, 0.36, 0.03), (0, 0, 0.855), cham=0.008, wob=0.003, seed=3)
    for k in range(7):
        a = R.uniform(0, 6.283); r = R.uniform(0.36, 0.5)
        TW.rock(pc, R.choice(('foot', 'rubble', 'ochre')), (math.cos(a) * r, math.sin(a) * r * 0.8, 0.02), (R.uniform(0.07, 0.13),) * 2 + (0.06,), R)
    transform_pc(pc, RE(math.radians(-1.8), math.radians(1.2), 0))
    return prop_obj(pc, 'Milestone_A', 1, grime=1.1, moss=1.6)

def milestone_b():
    R = R_('msB'); pc = FK.Piece('Milestone_B')
    def taper(p):
        t = (p.z + 0.6) / 1.2; f = 1.0 - 0.3 * t
        return Vector((p.x * (f - 1), p.y * (f - 1), 0))
    bx(pc, 'rubble', (0.44, 0.44, 1.2), (0, 0, 0.35), cham=0.025, wob=0.01, seed=4, cuts=(0, 0, 5), bend=taper)
    bx(pc, 'foot', (0.64, 0.64, 0.4), (0, 0, -0.05), cham=0.03, wob=0.012, seed=5)          # chunky footing, half buried
    bx(pc, 'coping', (0.54, 0.54, 0.1), (0, 0, 0.99), cham=0.02, wob=0.006, seed=6)         # cap block
    apex = V(0.01, -0.01, 1.28); b = 0.27
    base = [V(-b, -b, 1.03), V(b, -b, 1.03), V(b, b, 1.03), V(-b, b, 1.03)]
    convex(pc, 'coping', base + [apex], [(0, 1, 2, 3), (0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)], R)
    bx(pc, 'iron', (0.37, 0.37, 0.09), (0, 0, 0.76), cham=0.012, wob=0.003, seed=7)         # iron band
    for sgn in (-1, 1):
        bx(pc, 'iron', (0.05, 0.06, 0.12), (0.185 * sgn, -0.0, 0.76), cham=0.01, wob=0.002, seed=8)
    # worn tally bands and a carved lozenge on the front
    for i in range(3):
        bx(pc, 'mortar', (0.2, 0.02, 0.025), (0, -0.225 + i * 0.0, 0.35 + i * 0.1), cham=0.003, wob=0.002, seed=20 + i)
    TW.box(pc, 'mortar', (0.1, 0.02, 0.1), T(0, -0.235, 0.12) @ RE(0, 0, 0) @ Matrix.Rotation(math.radians(45), 4, 'Y'), cham=0.003, wob=0.002, seed=24)
    TW.rock(pc, 'rubble', (0.32, -0.3, 0.04), (0.14, 0.1, 0.08), R); TW.rock(pc, 'foot', (-0.34, -0.26, 0.03), (0.12, 0.1, 0.07), R)
    TW.rock(pc, 'ochre', (0.1, 0.36, 0.03), (0.13, 0.1, 0.07), R); TW.rock(pc, 'rubble', (-0.3, 0.3, 0.03), (0.1, 0.1, 0.06), R)
    transform_pc(pc, RE(math.radians(2.2), math.radians(-2.4), math.radians(4)))
    return prop_obj(pc, 'Milestone_B', 2, grime=1.2, moss=1.2)

# ============================================================================= 3-4. SHRINE
PIER = 0.33          # half width of the pier
Z_PLINTH = 0.37; Z_PIER = 1.2; Z_LEDGE = 1.3; Z_LINTEL = 1.78; Z_EAVE = 1.96; Z_RIDGE = 2.7

def shrine_base(pc, R, ruined):
    # two-step plinth: slabs round the sides over a dark core
    bx(pc, 'joint', (1.34, 1.34, 0.5), (0, 0, 0.0), cham=0.0, wob=0.0, seed=1)
    for (half, z0, z1, key) in ((0.75, -0.3, 0.17, 'foot'), (0.6, 0.17, Z_PLINTH, 'coping')):
        th = 0.3
        for side in range(4):
            segs = TW._split(-half, half, R, 0.5, 0.8)
            for (a, b) in segs:
                m = 0.5 * (a + b); ln = b - a
                for sgn in ((-1, 1) if True else ()):
                    pass
                rot = (R.uniform(-0.02, 0.02), R.uniform(-0.02, 0.02), 0)
                zc = (z0 + z1) / 2 + R.uniform(-0.01, 0.015)
                if side == 0:
                    c = (m, -(half - th / 2), zc); sz = (ln - 0.02, th, z1 - z0)
                elif side == 1:
                    c = (m, (half - th / 2), zc); sz = (ln - 0.02, th, z1 - z0)
                elif side == 2:
                    c = (-(half - th / 2), m * 0.78, zc); sz = (th, ln * 0.78 - 0.02, z1 - z0)
                else:
                    c = ((half - th / 2), m * 0.78, zc); sz = (th, ln * 0.78 - 0.02, z1 - z0)
                if ruined and R.random() < 0.12:
                    continue
                bx(pc, key if not (ruined and R.random() < 0.3) else 'rubble', sz, c, rot=rot, cham=0.03, wob=0.01, seed=R.random() * 40)
    # pier: core + coursed rubble on four faces
    core = (2 * PIER - 0.04, 2 * PIER - 0.04, Z_PIER - Z_PLINTH)
    bx(pc, 'joint', core, (0, 0, (Z_PIER + Z_PLINTH) / 2), cham=0.0, wob=0.0, seed=2)
    Vz = V(0, 0, 1)
    for (N, U) in ((V(0, -1, 0), V(1, 0, 0)), (V(1, 0, 0), V(0, 1, 0)), (V(0, 1, 0), V(-1, 0, 0)), (V(-1, 0, 0), V(0, -1, 0))):
        TW.masonry(pc, N * PIER, U, Vz, N, -PIER, PIER, Z_PLINTH - 0.02, Z_PIER, R, course=(0.15, 0.24), length=(0.2, 0.42), dmax=0.055,
                   joint=0.03, ochre=0.25, flush=True)
    # dressed cap course under the ledge
    bx(pc, 'coping', (2 * PIER + 0.08, 2 * PIER + 0.08, 0.1), (0, 0, Z_PIER + 0.05), cham=0.02, wob=0.005, seed=3)

def niche_and_ledge(pc, R, ruined):
    # offering ledge projecting to the road
    bx(pc, 'coping', (0.98, 0.84, 0.1), (0, -0.1, Z_LEDGE - 0.05), cham=0.02, wob=0.005, seed=4)
    for sgn in (-1, 1):    # corbels under it
        bx(pc, 'coping', (0.18, 0.3, 0.12), (0.34 * sgn, -0.4, Z_PIER - 0.03), rot=(0, 0, 0), cham=0.02, wob=0.004, seed=5 + sgn)
    # niche: dark back wall, two dressed jambs, lintel
    bx(pc, 'foot', (0.74, 0.14, Z_LINTEL - Z_LEDGE), (0, 0.27, (Z_LINTEL + Z_LEDGE) / 2), cham=0.0, wob=0.0, seed=7)
    for sgn in (-1, 1):
        bx(pc, 'coping', (0.14, 0.7, Z_LINTEL - Z_LEDGE), (0.31 * sgn, -0.0, (Z_LINTEL + Z_LEDGE) / 2), cham=0.02, wob=0.006, seed=8 + sgn)
    bx(pc, 'coping', (0.9, 0.76, 0.16), (0, -0.02, Z_LINTEL + 0.08), rot=(0, 0.01 if not ruined else 0.04, 0), cham=0.025, wob=0.006, seed=11)

def sleeping_beast(pc, R, ruined):
    """Chunky curled beast carved in pale stone: body, head on its paws, two ears, a tail wrapped round the front."""
    k = 'coping'; z0 = Z_LEDGE
    ellip(pc, k, (0.0, 0.06, z0 + 0.1), (0.2, 0.15, 0.1), (0, 0, 0.2), 2, R, 0.04)
    for (x, y, z, r) in ((-0.08, 0.1, z0 + 0.19, 0.05), (0.04, 0.12, z0 + 0.2, 0.05), (0.14, 0.09, z0 + 0.16, 0.045)):   # spine bumps
        ellip(pc, k, (x, y, z), (r, r, r * 0.8), (0, 0, 0), 1, R)
    if not ruined:
        ellip(pc, k, (-0.13, -0.14, z0 + 0.09), (0.1, 0.09, 0.08), (0, 0, 0.3), 2, R, 0.03)      # head
        for sgn in (-1, 1):
            convex(pc, k, [V(-0.13 + 0.07 * sgn - 0.03, -0.1, z0 + 0.14), V(-0.13 + 0.07 * sgn + 0.03, -0.1, z0 + 0.14), V(-0.13 + 0.07 * sgn, -0.16, z0 + 0.14),
                           V(-0.13 + 0.075 * sgn, -0.13, z0 + 0.22)], [(0, 1, 2), (0, 1, 3), (1, 2, 3), (2, 0, 3)], R)
        ellip(pc, k, (-0.05, -0.2, z0 + 0.04), (0.05, 0.06, 0.035), (0, 0, 0), 1, R)             # paws
        ellip(pc, k, (-0.2, -0.2, z0 + 0.04), (0.05, 0.06, 0.035), (0, 0, 0), 1, R)
        bx(pc, 'mortar', (0.05, 0.02, 0.008), (-0.1, -0.225, z0 + 0.105), cham=0.0, wob=0.0, seed=1)   # closed eyes
        bx(pc, 'mortar', (0.05, 0.02, 0.008), (-0.18, -0.225, z0 + 0.105), cham=0.0, wob=0.0, seed=2)
    else:
        ellip(pc, k, (-0.13, -0.14, z0 + 0.05), (0.06, 0.05, 0.04), (0.3, 0.2, 0.3), 1, R, 0.2)   # a broken stump
    pts = [(0.17, 0.0, 0.08), (0.2, -0.1, 0.07), (0.14, -0.2, 0.06), (0.04, -0.24, 0.05), (-0.06, -0.24, 0.04)]
    for i, (x, y, r) in enumerate(pts):
        ellip(pc, k, (x, y, z0 + 0.05 + (0.02 if i == 0 else 0)), (r, r, r * 0.8), (0, 0, 0), 1, R)

def offerings(pc, R):
    z = Z_LEDGE
    for (x, y, h) in ((-0.36, -0.36, 0.1), (-0.27, -0.42, 0.15), (0.04, -0.4, 0.07)):
        lathe(pc, 'coping', T(x, y, z), [(0.0, 0.0), (0.04, 0.0), (0.04, h), (0.025, h + 0.005), (0.0, h + 0.005)], 8, R)
        lathe(pc, 'mortar', T(x, y, z + h), [(0.0, 0.0), (0.006, 0.0), (0.006, 0.03), (0, 0.03)], 4, R)
        lathe(pc, 'coping', T(x, y, z - 0.01), [(0.0, 0.0), (0.075, 0.0), (0.06, 0.02), (0, 0.02)], 8, R)           # wax pool
    jar = [(0.0, 0.0), (0.075, 0.0), (0.105, 0.05), (0.115, 0.13), (0.09, 0.21), (0.06, 0.25), (0.07, 0.29), (0.0, 0.29)]
    lathe(pc, 'roof', T(0.33, -0.34, z), jar, 10, R)

def shrine_roof(pc, R, ruined):
    """Steep gabled tile roof; the ridge runs along Y. Built in its own frame so the ruin can slip it as one piece."""
    sub = FK.Piece('roof'); sub.bm = bmesh.new(); sub.uv = sub.bm.loops.layers.uv.new('UVMap')
    hs = 0.76; y0, y1 = -0.7, 0.52
    def sag(p):
        t = (p.y - (y0 + y1) / 2) / ((y1 - y0) / 2)
        return -0.035 * (1 - t * t)
    ref_l = V(-0.2, 0, Z_EAVE - 0.4); ref_r = V(0.2, 0, Z_EAVE - 0.4)
    def skipf(side):
        if not ruined:
            return None
        rr = R_('roofruin' + str(side))
        holes = [(rr.random(), rr.random(), rr.uniform(0.15, 0.3)) for _ in range(5)]
        def f(u, v):
            if side == 0 and v > 0.28:     # left slope: mostly stripped
                return rr.random() < 0.78 or v > 0.75
            for (hu, hv, hr) in holes:
                if (u - hu) ** 2 + (v - hv) ** 2 < hr * hr * (1.4 if side == 1 else 0.8):
                    return True
            return side == 1 and u > 0.8 and v > 0.5
        return f
    tiles(sub, V(-hs, y0, Z_EAVE), V(-hs, y1, Z_EAVE), V(0, y1, Z_RIDGE - 0.05), V(0, y0, Z_RIDGE - 0.05), R, ref_l, course=0.17, skip=skipf(0), sag=sag)
    tiles(sub, V(hs, y1, Z_EAVE), V(hs, y0, Z_EAVE), V(0, y0, Z_RIDGE - 0.05), V(0, y1, Z_RIDGE - 0.05), R, ref_r, course=0.17, skip=skipf(1), sag=sag)
    # roof boards under the tiles (dark slab), rafters, fascias, ridge
    for sgn in (-1, 1):
        ang = math.atan2(Z_RIDGE - Z_EAVE, hs)
        mid = V(sgn * hs / 2, (y0 + y1) / 2, (Z_EAVE + Z_RIDGE) / 2 - 0.04)
        ln = math.hypot(hs, Z_RIDGE - Z_EAVE)
        TW.box(sub, 'oak', (ln, y1 - y0 - 0.04, 0.05), T(*mid) @ Matrix.Rotation(-sgn * ang, 4, 'Y'), cham=0.0, wob=0.004, uv_world=True)
        for y in (y0 + 0.04, y1 - 0.04):     # barge boards along the slope edges
            a = V(sgn * (hs + 0.03), y, Z_EAVE - 0.04); b = V(0, y, Z_RIDGE + 0.02)
            tim(sub, 'oak', a, b, 0.09, 0.07, 'barge%d%d' % (sgn, int(y * 10)), up=(0, 1 if y < 0 else -1, 0), cham=0.015)
        if ruined and sgn == -1:
            for y in (-0.5, -0.2, 0.1, 0.4):    # bare rafters where the tiles are gone
                tim(sub, 'oak_lt' if y == 0.1 else 'oak', V(sgn * hs, y, Z_EAVE - 0.03), V(0, y, Z_RIDGE - 0.03), 0.08, 0.09, 'raf%d' % int(y * 10), up=(0, 1, 0), cham=0.015)
    # ridge cap (sags), gable infill at the front
    tim(sub, 'coping', V(0, y0 - 0.02, Z_RIDGE + 0.02), V(0, y1 + 0.02, Z_RIDGE + 0.02), 0.16, 0.13, 'ridge', up=(1, 0, 0), sag=0.035, cham=0.02)
    if not (ruined and False):
        convex(sub, 'plaster', [V(-0.66, -0.34, Z_EAVE - 0.02), V(0.66, -0.34, Z_EAVE - 0.02), V(0, -0.34, Z_RIDGE - 0.06),
                                V(-0.66, -0.22, Z_EAVE - 0.02), V(0.66, -0.22, Z_EAVE - 0.02), V(0, -0.22, Z_RIDGE - 0.06)],
               [(0, 1, 2), (3, 4, 5), (0, 1, 4, 3), (0, 2, 5, 3), (1, 2, 5, 4)], R)
    return sub

def shrine(ruined=False):
    name = 'Shrine_Ruined' if ruined else 'Shrine'
    pc = FK.Piece(name)
    Rp = R_('shrine_pier'); shrine_base(pc, Rp, ruined)
    R = R_(name)
    niche_and_ledge(pc, R, ruined); sleeping_beast(pc, R, ruined)
    if not ruined:
        offerings(pc, R)
    roof = shrine_roof(pc, R, ruined)
    if ruined:   # the roof slipped: yawed, dropped on the -X side, shifted off the lintel
        M = T(0.08, -0.06, -0.11) @ RE(math.radians(5.5), math.radians(8.5), math.radians(7.0))
        transform_pc(roof, T(0, 0, Z_EAVE) .inverted() @ Matrix.Identity(4))
        transform_pc(roof, M @ T(0, 0, 0)); transform_pc(roof, T(0, 0, Z_EAVE))
    merge_piece(pc, roof)
    # rubble at the foot (a bigger heap when ruined)
    TW.heap(pc, R, 0.0, -0.05, 1.15, 1.1, 0.16 if not ruined else 0.3, 34 if not ruined else 70, zbase=-0.1,
            keys=(('rubble', 0.45), ('ochre', 0.25), ('foot', 0.3)), smin=0.07, smax=0.2 if not ruined else 0.26,
            clip=lambda x, y: abs(x) > 0.6 or abs(y) > 0.6, blocks=0.2)
    if ruined:
        for _ in range(26):        # slipped roof tiles scattered round the plinth
            a = R.uniform(0, 6.283); r = R.uniform(0.8, 1.45)
            bx(pc, 'roof', (0.2, 0.3, 0.035), (math.cos(a) * r, math.sin(a) * r * 0.9 - 0.1, 0.03), rot=(R.uniform(-0.3, 0.3), R.uniform(-0.3, 0.3), R.uniform(0, 6.28)),
               cham=0.006, wob=0.004, seed=R.random() * 50)
        # toppled jar on the road side of the step, one candle stub lying beside it, a split head on the plinth
        lathe(pc, 'roof', T(0.4, -0.95, 0.16) @ RE(0, math.radians(86), math.radians(20)), [(0.0, 0.0), (0.075, 0.0), (0.105, 0.05), (0.115, 0.13), (0.09, 0.21), (0.06, 0.25), (0.07, 0.29), (0.0, 0.29)], 10, R)
        ellip(pc, 'coping', (-0.35, -0.9, 0.11), (0.1, 0.09, 0.08), (0.4, 0.3, 0.5), 2, R, 0.05)
        # the crack, down the pier front and across the lintel
        TW.crack_ribbon(pc, V(0.1, -PIER - 0.0, Z_PIER - 0.05), V(-0.2, 0, -1), V(0, -1, 0), 0.75, R, w0=0.06, lift=0.06, branches=2)
        TW.crack_ribbon(pc, V(0.25, -0.38, Z_LINTEL + 0.02), V(-1, 0, 0.1), V(0, -1, 0), 0.5, R, w0=0.04, lift=0.015, branches=1)
    return prop_obj(pc, name, 3 if not ruined else 4, grime=1.1 if not ruined else 1.6, moss=1.0 if not ruined else 2.3)

def merge_piece(pc, sub):
    """Copy a sub-Piece's faces into pc (materials by name)."""
    sub.bm.normal_update()
    vm = {}
    for f in sub.bm.faces:
        key = sub.mats[f.material_index]
        si = pc.slot_by_mat(key) if hasattr(pc, 'slot_by_mat') else None
        break
    mapping = {}
    for i, m in enumerate(sub.mats):
        if m not in pc.mats:
            pc.mats.append(m)
        mapping[i] = pc.mats.index(m)
    for v in sub.bm.verts:
        vm[v] = pc.bm.verts.new(v.co)
    for f in sub.bm.faces:
        try:
            nf = pc.bm.faces.new([vm[v] for v in f.verts])
        except ValueError:
            continue
        nf.material_index = mapping[f.material_index]
        for l, nl in zip(f.loops, nf.loops):
            nl[pc.uv].uv = l[sub.uv].uv
    sub.bm.free()

# ============================================================================= 5-6. CART
AX = 0.2          # axle x
WR = 0.62         # wheel outer radius (tyre)
WY = 0.80         # wheel centre |y|

def wheel(pc, M, R, broken=(), fresh=(), tag='w'):
    """Oak wheel with an iron tyre; axis = M's local z. broken: spoke indexes snapped to a stub; fresh: spokes in pale new oak."""
    axis = (M.to_3x3() @ V(0, 0, 1)).normalized()
    lathe(pc, 'oak', M, [(0.0, -0.13), (0.07, -0.13), (0.1, -0.09), (0.15, -0.04), (0.155, 0.04), (0.1, 0.09), (0.07, 0.13), (0.0, 0.13)], 10, R)
    for z in (-0.085, 0.085):
        lathe(pc, 'iron', M @ T(0, 0, z), [(0.15, -0.025), (0.168, -0.02), (0.168, 0.02), (0.15, 0.025)], 10, R)
    ns = 10
    for k in range(ns):
        a = 2 * math.pi * k / ns + 0.1
        d = V(math.cos(a), math.sin(a), 0)
        stub = k in broken
        r1 = 0.3 if stub else 0.53
        pa = M @ (d * 0.12 + V(0, 0, 0.012 * (1 if k % 2 else -1))); pb = M @ (d * r1)
        tim(pc, 'oak_lt' if k in fresh else 'oak', pa, pb, 0.08, 0.1, '%s_sp%d' % (tag, k), up=tuple(axis), cham=0.015, wob=0.004)
        if stub:   # splintered pale end
            e = d * r1
            for s in (-1, 1):
                convex(pc, 'oak_lt', [M @ (e + V(0, 0, 0.04 * s) + d * 0.0), M @ (e + V(0, 0, -0.04 * s) + d * 0.0), M @ (e + d * 0.06 + V(0, 0, 0.01 * s)),
                                      M @ (e - V(-d.y, d.x, 0) * 0.035 + V(0, 0, 0.0)), M @ (e + V(-d.y, d.x, 0) * 0.035 + V(0, 0, 0.0))],
                       [(0, 1, 2), (0, 1, 3, 4)] if False else [(0, 3, 2), (3, 1, 2), (1, 4, 2), (4, 0, 2), (0, 1, 3), (0, 1, 4)], R)
    prism_ring(pc, 'oak', M, 0.5, 0.592, 0.055, 8, gap=0.004, R=R)
    prism_ring(pc, 'iron', M, 0.585, WR, 0.058, 20, gap=0.0, R=R)

def cart_body(pc, R, repaired):
    bed_y = 0.58
    # axle, longitudinal sills, floor planks
    tim(pc, 'oak', V(AX, -0.98, 0.62), V(AX, 0.98, 0.62), 0.12, 0.12, 'axle', up=(0, 0, 1), cham=0.02)
    for sgn in (-1, 1):
        tim(pc, 'oak', V(-0.9, 0.46 * sgn, 0.74), V(1.45, 0.46 * sgn, 0.74), 0.13, 0.1, 'sill%d' % sgn, up=(0, 0, 1), sag=0.012)
    nplank = 6
    for i in range(nplank):
        y = -bed_y + (i + 0.5) * (2 * bed_y / nplank)
        pale = repaired and i in (1, 4)
        tim(pc, 'oak_lt' if pale else 'oak', V(-0.82, y, 0.815), V(1.42, y, 0.815), 2 * bed_y / nplank - 0.012, 0.075, 'fl%d' % i, up=(0, 0, 1), cham=0.012)
    for x in (-0.45, 0.2, 0.9):     # cross-bearers
        tim(pc, 'oak', V(x, -0.64, 0.775), V(x, 0.64, 0.775), 0.1, 0.07, 'xb%d' % int(x * 10), up=(0, 0, 1), cham=0.015)
    # sides: two boards each, stakes
    for sgn in (-1, 1):
        for zc, hh in ((1.0, 0.19), (1.23, 0.19)):
            tim(pc, 'oak', V(-0.85, sgn * 0.62, zc), V(1.47, sgn * 0.62, zc), hh, 0.055, 'sd%d_%d' % (sgn, int(zc * 10)), up=(0, -1, 0), sag=0.012)
        for x in (-0.78, -0.2, 0.4, 1.0, 1.4):
            tim(pc, 'oak', V(x, sgn * 0.655, 0.8), V(x, sgn * 0.655, 1.46), 0.09, 0.075, 'st%d_%d' % (sgn, int(x * 10)), up=(0, -1, 0), cham=0.015)
    tim(pc, 'oak', V(-0.87, -0.6, 1.1), V(-0.87, 0.6, 1.1), 0.46, 0.055, 'front', up=(1, 0, 0))
    tim(pc, 'oak_lt' if repaired else 'oak', V(1.5, -0.6, 0.98), V(1.5, 0.6, 0.98), 0.3, 0.05, 'tail', up=(1, 0, 0))
    # shafts and swingletree
    for sgn in (-1, 1):
        tim(pc, 'oak', V(0.95, 0.42 * sgn, 0.78), V(-2.0, 0.42 * sgn, 0.55), 0.1, 0.1, 'shaft%d' % sgn, up=(0, 0, 1), cham=0.02)
        bx(pc, 'iron', (0.12, 0.13, 0.12), (-1.9, 0.42 * sgn, 0.565), cham=0.01, wob=0.002, seed=3)    # iron ferrule
        bx(pc, 'iron', (0.1, 0.12, 0.14), (0.0, 0.42 * sgn, 0.77), cham=0.01, wob=0.002, seed=4)       # strap under the bed
    tim(pc, 'oak', V(-1.55, -0.55, 0.62), V(-1.55, 0.55, 0.62), 0.07, 0.07, 'xshaft', up=(0, 0, 1), cham=0.015)
    for sgn in (-1, 1):     # linchpins and axle caps
        bx(pc, 'iron', (0.06, 0.04, 0.06), (AX, 0.99 * sgn, 0.62), cham=0.005, wob=0.0, seed=5)

def cart(broken=False):
    name = 'Cart_Broken' if broken else 'Cart'
    R = R_(name); pc = FK.Piece(name)
    cart_body(pc, R, repaired=not broken)
    wheel_M = lambda y: T(AX, y, WR) @ RE(-math.pi / 2, 0, 0)
    if not broken:
        wheel(pc, wheel_M(WY), R, fresh=(2, 7), tag='wr'); wheel(pc, wheel_M(-WY), R, tag='wl')
        bx(pc, 'oak', (0.34, 0.9, 0.72), (-1.9, 0.0, 0.11), cham=0.04, wob=0.015, seed=9)      # the block under the shaft tips
        TW.heap(pc, R, -1.9, 0.0, 0.4, 0.7, 0.05, 6, zbase=-0.05, smin=0.06, smax=0.12)
        for sgn in (-1, 1):   # clay worn under the tyres
            bx(pc, 'foot', (0.5, 0.18, 0.05), (AX, WY * sgn, -0.02), cham=0.02, wob=0.01, seed=11)
        pc.pivot_fix = None
        transform_pc(pc, T(0, 0, -0.05) @ RE(0, 0, math.radians(0)))
        obj = prop_obj(pc, name, 5, grime=1.0, moss=0.7)
        return obj
    # --- broken: the body (right wheel on) tips onto its left (-Y) side, nose down
    wheel(pc, wheel_M(WY), R, tag='wr')
    for sgn in (-1,):     # left axle stub, snapped
        bx(pc, 'oak_lt', (0.11, 0.08, 0.11), (AX, -1.04, 0.62), rot=(0.2, 0, 0.1), cham=0.01, wob=0.01, seed=21)
    M = RE(0, 0, math.radians(5)) @ RE(math.radians(26), math.radians(-7), 0)
    transform_pc(pc, M)
    zs = [v.co.z for v in pc.bm.verts]
    transform_pc(pc, T(0.2, 0.55, -min(zs) - 0.12))
    # loose planks and a snapped stake; the left wheel lying flat beside the cart with two broken spokes
    for i, (a, b, c) in enumerate(((V(-0.6, -1.65, 0.05), V(0.6, -2.05, 0.03), 0.2), (V(0.9, -2.2, 0.03), V(1.9, -1.9, 0.04), 0.17),
                                    (V(-1.4, -0.45, 0.04), V(-0.5, -0.1, 0.05), 0.15))):
        tim(pc, 'oak_lt' if i == 1 else 'oak', a, b, c, 0.05, 'plank%d' % i, up=(0, 0, 1), cham=0.012)
    tim(pc, 'oak_lt', V(1.4, -1.0, 0.07), V(1.6, -1.55, 0.05), 0.08, 0.07, 'stakeb', up=(0, 0, 1), cham=0.01)
    Mw = T(-0.1, -1.55, 0.11) @ RE(math.radians(4), math.radians(-5), math.radians(30))
    wheel(pc, Mw, R, broken=(2, 6), tag='wb')
    TW.heap(pc, R, 0.2, -0.9, 1.6, 0.9, 0.04, 14, zbase=-0.05, smin=0.05, smax=0.11)
    transform_pc(pc, T(0, 0, 0))
    return prop_obj(pc, name, 6, grime=1.5, moss=1.7)

# ============================================================================= 7-8. FENCE
def fence(broken=False):
    name = 'Fence_Rail_Broken' if broken else 'Fence_Rail'
    R = R_(name); pc = FK.Piece(name)
    def post(x, h, lean=(0, 0), nm='p', top=0.0):
        sub = FK.Piece('post'); sub.bm = bmesh.new(); sub.uv = sub.bm.loops.layers.uv.new('UVMap')
        bx(sub, 'oak', (0.17, 0.21, h + 0.3), (0, 0, (h - 0.3) / 2), rot=(0, 0, 0), cham=0.025, wob=0.01, seed=R.random() * 30, uv_world=False, cuts=(0, 0, 3), long_axis=2)
        # split top: a sloped cap wedge
        convex(sub, 'oak_lt' if False else 'oak', [V(-0.085, -0.105, h), V(0.085, -0.105, h), V(0.085, 0.105, h), V(-0.085, 0.105, h),
                                                   V(-0.085, -0.105, h + 0.06), V(0.085, -0.105, h + 0.06)] , [(0, 1, 5, 4), (0, 4, 3), (1, 2, 5), (4, 5, 2, 3), (0, 1, 2, 3)], R) if False else None
        bx(sub, 'oak', (0.15, 0.19, 0.07), (0, 0, h + 0.01), rot=(0.25, 0, 0), cham=0.015, wob=0.006, seed=R.random() * 30, uv_world=False, long_axis=1)
        for p in sub.bm.verts:
            pass
        transform_pc(sub, T(x, 0, 0) @ RE(*lean))
        merge_piece(pc, sub)
    post(-1.5, 1.15, (math.radians(1.5), 0, math.radians(1.2)))
    if not broken:
        post(0.0, 1.1, (math.radians(-2.2), 0, math.radians(-1.0)))
    else:
        post(0.0, 1.1, (math.radians(15), 0, math.radians(1.0)))
        # the leaning post is held by a heap of stones at its foot
        TW.rock(pc, 'rubble', (0.0, -0.18, 0.05), (0.16, 0.12, 0.1), R); TW.rock(pc, 'foot', (-0.1, 0.12, 0.05), (0.14, 0.11, 0.09), R)
    # rails: split oak, 0.1 x 0.13 (chunky), through the posts
    zs = (0.52, 0.93)
    for i, z in enumerate(zs):
        sag = 0.035 if i == 1 else 0.02
        if broken and i == 1:
            # the upper rail dropped: one end still on the post at x -1.5, the other on the ground at x 1.4
            tim(pc, 'oak_lt', V(-1.6, 0.04, z + 0.02), V(1.5, 0.28, 0.1), 0.1, 0.12, 'railfall', up=(0, 0, 1), cham=0.02)
            continue
        if broken and i == 0:
            tim(pc, 'oak', V(-1.55, 0.02, z), V(1.5, 0.0, z - 0.1), 0.1, 0.12, 'rail%d' % i, up=(0, -1, 0), sag=0.03)
        else:
            tim(pc, 'oak', V(-1.55, 0.0, z), V(1.55, 0.0, z + 0.015 * i), 0.1, 0.12, 'rail%d' % i, up=(0, -1, 0), sag=sag)
    # pegs and ground wear
    for x in (-1.5, 0.0):
        for z in zs:
            bx(pc, 'iron', (0.03, 0.05, 0.03), (x, -0.12, z), cham=0.004, wob=0.0, seed=1)
    for _ in range(6):
        a = R.uniform(0, 6.283)
        TW.rock(pc, R.choice(('foot', 'rubble')), (-1.5 + math.cos(a) * 0.22, math.sin(a) * 0.2, 0.02), (0.07, 0.07, 0.05), R)
    return prop_obj(pc, name, 7 if not broken else 8, grime=1.2, moss=1.1)

# ============================================================================= 9. HEDGES (B9 card crowns)
def cell_uv(rect, uv):
    return (rect[0] + (rect[2] - rect[0]) * uv[0], rect[1] + (rect[3] - rect[1]) * uv[1])

def smooth(x):
    x = min(1.0, max(0.0, x)); return x * x * (3 - 2 * x)

def put_card(mb, c, nrm, size, rect, shade_n, ao_c, ao_e, R, roll=None, aspect=1.0, mat=1):
    """A leaf-mass card tangent to nrm at c, rotated by a random roll about its normal; corner normals = the crown's shading normal bent."""
    nrm = Vector(nrm).normalized()
    up = Vector((0, 0, 1)); up = up - nrm * up.dot(nrm)
    if up.length < 0.2:
        up = Vector((R.uniform(-1, 1), R.uniform(-1, 1), 0)); up = up - nrm * up.dot(nrm)
    up.normalize()
    roll = R.uniform(-1.3, 1.3) if roll is None else roll
    up = Matrix.Rotation(roll, 3, nrm) @ up
    rt = up.cross(nrm)
    hw, hh = size * 0.5 * aspect, size * 0.5
    pts = [c - rt * hw - up * hh, c + rt * hw - up * hh, c + rt * hw + up * hh, c - rt * hw + up * hh]
    uvs = [cell_uv(rect, q) for q in ((0, 0), (1, 0), (1, 1), (0, 1))]
    ns = []
    for p in pts:
        ns.append((Vector(shade_n(p)) * 0.8 + nrm * 0.2).normalized())
    mb.face(pts, uvs, ns, [ao_c, ao_c, ao_e, ao_e], mat)

def sample_blob_surface(blobs, R, per_area, zmin=0.04):
    """Points on the outer surface of a union of ellipsoids: (point, outward normal, blob index)."""
    out = []
    for bi, (c, r) in enumerate(blobs):
        area = 4 * math.pi * ((r[0] * r[1]) ** 1.6 + (r[0] * r[2]) ** 1.6 + (r[1] * r[2]) ** 1.6) / 3 ** (1 / 1.6) ** 1.0
        n = int(area * per_area)
        for _ in range(n):
            d = Vector((R.gauss(0, 1), R.gauss(0, 1), R.gauss(0, 1))).normalized()
            p = Vector((c[0] + d.x * r[0], c[1] + d.y * r[1], c[2] + d.z * r[2]))
            if p.z < zmin:
                continue
            inside = False
            for bj, (c2, r2) in enumerate(blobs):
                if bj == bi:
                    continue
                q = ((p.x - c2[0]) / r2[0]) ** 2 + ((p.y - c2[1]) / r2[1]) ** 2 + ((p.z - c2[2]) / r2[2]) ** 2
                if q < 0.78:
                    inside = True; break
            if inside:
                continue
            nrm = Vector((d.x / r[0], d.y / r[1], d.z / r[2])).normalized()
            out.append((p, nrm, bi))
    return out

def stem(mb, pts, radii, sides=6, mat=0):
    pts = [Vector(p) for p in pts]; n = len(pts)
    rings = []
    for k, p in enumerate(pts):
        t = (pts[min(k + 1, n - 1)] - pts[max(k - 1, 0)]).normalized()
        a = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        u = t.cross(a).normalized(); w = t.cross(u)
        rings.append([(p + (u * math.cos(2 * math.pi * i / sides) + w * math.sin(2 * math.pi * i / sides)) * radii[k],
                       (u * math.cos(2 * math.pi * i / sides) + w * math.sin(2 * math.pi * i / sides))) for i in range(sides + 1)])
    acc = 0.0
    for k in range(n - 1):
        seg = (pts[k + 1] - pts[k]).length
        for i in range(sides):
            a0, n0 = rings[k][i]; a1, n1 = rings[k][i + 1]; b1, m1 = rings[k + 1][i + 1]; b0, m0 = rings[k + 1][i]
            u0 = i / sides * 0.6; u1 = (i + 1) / sides * 0.6
            mb.face([a0, a1, b1, b0], [(u0, acc / 2), (u1, acc / 2), (u1, (acc + seg) / 2), (u0, (acc + seg) / 2)], [n0, n1, m1, m0], [0.6, 0.6, 0.75, 0.75], mat)
        acc += seg

def hedge(name, seed, hmin, hmax, nb, jitter):
    R = R_(name)
    blobs = []
    xs = [(-1.65 + 3.3 * i / (nb - 1)) for i in range(nb)]
    for i, x in enumerate(xs):
        end = 1.0 if i in (0, nb - 1) else 0.0
        x += R.uniform(-jitter, jitter)
        ht = R.uniform(hmin, hmax) * (1 - 0.12 * end)
        ry = R.uniform(0.54, 0.62) * (1 - 0.1 * end); rx = R.uniform(0.55, 0.7) * (1 - 0.12 * end)
        rz = ht * 0.5 + 0.12
        blobs.append(((x, R.uniform(-0.07, 0.07), ht - rz + 0.04), (rx, ry, rz)))
    for _ in range(int(nb * 1.1)):    # bumpy top: extra lumps
        x = R.uniform(-1.9, 1.9); hh = R.uniform(hmin, hmax)
        blobs.append(((x, R.uniform(-0.25, 0.25), hh - 0.16), (R.uniform(0.3, 0.45), R.uniform(0.3, 0.42), R.uniform(0.26, 0.38))))
    top = lambda x: max([b[0][2] + b[1][2] for b in blobs if abs(b[0][0] - x) < b[1][0]] or [hmin])
    def shade_n(p):
        return Vector((p.x * 0.18, p.y / 0.62, (p.z - 0.55) / 0.85)).normalized()
    mb = L.MB()
    pts = sample_blob_surface(blobs, R, 5.6)
    for (p, nrm, bi) in pts:
        rect = IVY_DARK if (p.z < 0.3 and R.random() < 0.7) else R.choice(IVY_CELLS)
        size = R.uniform(0.5, 0.78)
        ao_c = 0.55 + 0.45 * smooth((p.z - 0.1) / 1.3) * (0.8 + 0.2 * nrm.z) ; ao_e = min(1.0, ao_c + 0.18)
        cc = p - nrm * 0.1
        put_card(mb, cc, (nrm + Vector((R.uniform(-.25, .25), R.uniform(-.25, .25), R.uniform(-.1, .25)))), size, rect, shade_n, ao_c, ao_e, R)
    for _ in range(int(len(pts) * 0.22)):    # inner cards (dark, fill the see-through gaps with shadow colour)
        x = R.uniform(-1.8, 1.8); y = R.uniform(-0.4, 0.4); z = R.uniform(0.25, max(0.4, top(x) - 0.4))
        put_card(mb, Vector((x, y, z)), Vector((R.gauss(0, 1), R.gauss(0, 1), R.gauss(0, 1))), R.uniform(0.6, 0.85), IVY_DARK, shade_n, 0.45, 0.5, R)
    # woody stems at the base
    for k in range(6):
        x = -1.6 + 3.2 * (k + R.uniform(-0.3, 0.3)) / 5; y = R.uniform(-0.3, 0.3)
        lean = Vector((R.uniform(-0.15, 0.15), R.uniform(-0.15, 0.15), 1)).normalized(); hgt = R.uniform(0.55, 0.9)
        p0 = Vector((x, y, -0.12)); path = [p0 + lean * (hgt * t) + Vector((R.uniform(-0.03, 0.03), R.uniform(-0.03, 0.03), 0)) for t in (0, 0.35, 0.7, 1.0)]
        stem(mb, path, [R.uniform(0.045, 0.06), 0.042, 0.032, 0.022], 6, 0)
    ob = L.build(name, mb, [FK.M('oak'), b9_mat()])
    for m in ob.data.materials:
        pass
    # wear attribute: grime at the foot, no moss
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    for poly in me.polygons:
        for li in poly.loop_indices:
            z = me.vertices[me.loops[li].vertex_index].co.z
            attr.data[li].color = (max(0.0, 0.5 - z * 0.6), 0.0, 0.0, 1.0)
    return ob

# ============================================================================= registry
PIECES = {
    'Milestone_A': milestone_a, 'Milestone_B': milestone_b, 'Shrine': lambda: shrine(False), 'Shrine_Ruined': lambda: shrine(True),
    'Cart_Broken': lambda: cart(True), 'Cart': lambda: cart(False), 'Fence_Rail': lambda: fence(False), 'Fence_Rail_Broken': lambda: fence(True),
    'Hedge_A': lambda: hedge('Hedge_A', 1, 1.5, 1.8, 8, 0.12), 'Hedge_B': lambda: hedge('Hedge_B', 2, 1.4, 1.7, 9, 0.2),
}
BUDGET = {'Milestone_A': (600, 1200), 'Milestone_B': (600, 1200), 'Shrine': (4000, 7000), 'Shrine_Ruined': (4000, 7000), 'Cart_Broken': (5000, 8000),
          'Cart': (5000, 8000), 'Fence_Rail': (400, 900), 'Fence_Rail_Broken': (400, 900), 'Hedge_A': (1500, 3000), 'Hedge_B': (1500, 3000)}

def build_all(only=None):
    objs = {}
    for n, fn in PIECES.items():
        if only and only != n:
            continue
        o = fn(); o.name = n; objs[n] = o
        lo, hi = BUDGET[n]; t = TW.tris(o)
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        print('  %-18s %6d tris (budget %d-%d)  size %.2f x %.2f x %.2f  z %.2f..%.2f  %s' % (n, t, lo, hi,
              max(v.x for v in bb) - min(v.x for v in bb), max(v.y for v in bb) - min(v.y for v in bb), max(v.z for v in bb) - min(v.z for v in bb),
              min(v.z for v in bb), max(v.z for v in bb), ', '.join(m.name for m in o.data.materials)))
    return objs

# ============================================================================= review
def review(objs, tag):
    cam = TW.setup_review()
    TW.review_ground((200, 200), (60, 0))
    sc = bpy.context.scene
    P = TW.place
    shots = {}
    def pl(n, x, y, yaw=0.0):
        if n in objs:
            P(objs[n], (x, y, 0), yaw)
    pl('Milestone_A', 0, 0, 0.12); pl('Milestone_B', 1.7, 0.1, -0.1)
    pl('Shrine', 30, 0); pl('Shrine_Ruined', 33.6, 0.2, 0.15)
    pl('Cart_Broken', 60, 0, 0.0); pl('Cart', 66.5, 0, 0.2)
    for i in range(2):
        pl('Fence_Rail', 90 + i * 3.0, 0); pl('Hedge_A', 92 + i * 4.0 - 4, 1.6) if i == 0 else pl('Hedge_B', 92 + 0.3, 1.6)
    pl('Fence_Rail_Broken', 96, 0); pl('Hedge_A', 98.0, 1.6, 0.05); pl('Hedge_B', 102.1, 1.7, -0.04)
    if '--figure' in argv or True:
        TW.figure((-1.3, -1.2)); TW.figure((28.6, -1.6))
    shots['s1_milestones'] = ((0.9, -7.5, 1.7), (0.85, 0, 0.55), 36)
    shots['s2_shrines'] = ((31.8, -10.5, 2.0), (31.8, 0, 1.45), 38)
    shots['s3_carts'] = ((63.0, -11.5, 2.1), (63.0, 0, 0.7), 40)
    shots['s4_lane'] = ((96.0, -9.0, 1.8), (96.0, 0.8, 0.85), 44)
    for k, (frm, to, fov) in shots.items():
        TW.shoot(cam, frm, to, fov, os.path.join(OUT, 'shot_%s_%s.png' % (k, tag)))

# ============================================================================= export
def export_all(objs):
    for n, o in objs.items():
        if n.startswith('Hedge'):
            L.export_fbx([o], os.path.join(EXP, n, n + '.fbx'))
        else:
            TW.export_selected([o], os.path.join(EXP, n, n + '.fbx'))
    print('EXPORT', EXP)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ONLY = FK.arg('--only'); TAG = FK.arg('--tag', 'r1')
    if '--trees' not in argv or ONLY:
        objs = build_all(ONLY)
        if '--export' in argv:
            export_all(objs)
        if '--no-render' not in argv:
            review(objs, TAG)
    if '--trees' in argv:
        import sc_road_trees as RT
        RT.main(OUT, EXP, '--export' in argv)
