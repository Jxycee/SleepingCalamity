"""Schoolhouse: Hollowmere's village schoolhouse (ring 2, M class, frame + parts).

A steep, tall, single-room schoolhouse: limewashed rubble walls on a dressed stone ground course with pale long-and-short
quoins, heavy oak lintels and tall deep-set multi-paned windows (three a side, two at the back), a full red-tile gable
roof (ridge runs front to back, about 50 deg, sagging) and green-painted boarded gables with battens and a round loft
vent: so it never reads like the bakery (clipped half-hip, jettied timber front, brick chimney) or the approved facade
(eave-front, two storeys). A small open timber bell-cote with its own tiled gable roof straddles the ridge over the front
gable; a tiled pent hood on knee brackets shelters the door; a bench stands left of the door and a slate notice board
(chalk marks, pinned papers) hangs to its right; two empty iron sign hooks sit above the hood. One small stone chimney
rises from the ridge at the back. The yard turns with the building: stepping slabs in front, chalked hopscotch slabs
along +X, a hooped water butt back right.

Parts (each its own mesh with its pivot at its socket):
  Part_Door  planked green door (FK.kit_door); without it the doorway stands open on a dark lined interior.
  Part_Bell  bronze bell, oak headstock with iron straps and gudgeons, half wheel, clapper, rope and toggle, hung in the
             bell-cote; without it the cote stands empty with bare iron bearing rings and a bare cross rail.
  Part_Sign  green board with gilt lettering blocks and an oak edge, hung on short chains from the two hooks; without it
             the two iron hooks above the hood hang empty.
SOCKET_Door / SOCKET_Bell / SOCKET_Sign are empties parented to Frame. FOOTPRINT is a flat box over the body (plinth
included, yard excluded): the collider / NPC-block shape.

Built from the B4 facade kit and the bakery helpers (imported: sc_bakery piece/merge/face_xf/window/doorway/plinth_run/
tile_courses/ridge_tiles/tube). T2: no unique bake; shared tileables with metre UVs (2 m per tile, 2K maps): B1 plaster,
oak_timber, sandstone, roof_tile, painted_wood, iron, canvas plus the B2 soot_coal for the slate. Wear in the linear float
colour attribute "wear" like the facade kit (R grime/soot, G moss, B polish). No emissive slot.

refs: Reference/hollowmere/kit_ready/schoolhouse/REFS.md, PLAN_schoolhouse.md, PHASE1 3.1/3.4.

Axes (Blender): z up, front (door side) -Y, metres. Origin = foundation centre at ground (M foundation 8 x 8 m). Export FBX
-Z forward, Y up: Blender -Y is local +Z in Unity, so the builder adds yaw 180 to face the lot's -Z.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_schoolhouse.py -- --out <scratch>
      [--views tq,tql,back,frame,cote,door] [--rres 1000] [--samples 20] [--no-render] [--export] [--blend] [--tris]
  --export writes <out>/Schoolhouse.fbx (root Schoolhouse: Frame, Part_Door, Part_Bell, Part_Sign, SOCKET_* under Frame,
  FOOTPRINT). Renders go to <out>/schoolhouse_<view>.png; views starting 'frame' hide every part.

Budget: under 40k tris, 2048 px maps (shared sets).
Status (2026-10-31, cloud): 2 render rounds; self-review 28/40 (silhouette 4, proportion 3.5, form 3.5, surface 3.5,
colour 3.5, wear 3, set 3.5, glass/metal 3.5); first round about 26, round 2 (moss cut back, notice board and hood
resized, camera) gained about 2; stopped under the 1.5-point rule for a third.
29,480 tris (Frame 25,192, Part_Door 1,558, Part_Bell 1,962, Part_Sign 768; reproducible); FOOTPRINT 5.40 x 6.40 m;
everything inside x -3.14..3.92, y -3.89..3.95, z 0..10.65. Sheet: Reference/hollowmere/kit_ready/schoolhouse/SHEET_schoolhouse_final.jpg.
Next visible gains: a shorter, sturdier bell-cote (it is tall and thin at mid distance), per-panel limewash value
variation and flaking, green gable boards less saturated, the empty-state close render of the bare bearings (not
rendered), a real mid-height wear stain pass on the long walls, tiled ridge reads thin next to the cote.
"""
import sys, os, math, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK
import sc_bakery as BK

OUT = FK.OUT
argv = FK.argv
VIEWS = FK.arg('--views', 'tq,tql,back,frame,cote,door').split(',')
RRES = int(FK.arg('--rres', '1000')); SAMPLES = int(FK.arg('--samples', '20'))
TILE = FK.TILE
PROUD = FK.PROUD

# ----------------------------------------------------------------------------- dimensions (metres)
HX = 2.6                          # body half width (x)
Y0, Y1 = -2.7, 3.5                # front and back wall planes
YC, HL = (Y0 + Y1) / 2, (Y1 - Y0) / 2 + 0.4
PL = 0.6                          # ground course height
PLATE = 4.4                       # eave plate
XE = 3.05                         # eave tip x (overhang 0.45)
ZE, ZR = 4.01, 7.65               # deck top at the eave tip and at the ridge
T = (ZR - ZE) / XE                # slope (about 50 deg)
RS = 0.12                         # ridge sag
YF, YB = Y0 - 0.4, Y1 + 0.4       # verge tips
DOOR = (2.0, 3.2, 0.16, 2.35)     # wall-local u0, u1, z0, z1 (front wall; u from X0 = -2.6)
WIN_Z = (1.5, 3.35)
WIN_W = 0.95
COTE_Y = -2.55
AXLE_Z = 9.15
CH = (-0.42, 0.42, 2.1, 2.85, 8.95)   # chimney x0 x1 y0 y1 top
LEAN = (0.008, 0.005)

def top_z(ax):
    return ZE + (XE - ax) * T

EXTRA = {
    'lime': lambda: FK.tile_mat('SH_Limewash', 'B1_town/plaster', tint=(1.05, 1.03, 0.97), normal=0.7),
    'slate': lambda: FK.tile_mat('SH_Slate', 'B2_cellar/soot_coal', tint=(0.85, 0.9, 0.95), normal=0.5),
    'bronze': lambda: FK.tile_mat('SH_Bronze', 'B1_town/iron', tint=(3.0, 1.75, 0.60), rough_add=-0.05, normal=0.5),
    'chalk': lambda: FK.tile_mat('SH_Chalk', 'B1_town/plaster', tint=(1.45, 1.45, 1.42), normal=0.1),
    'rope': lambda: FK.tile_mat('SH_Rope', 'B1_town/canvas', tint=(1.0, 0.85, 0.62), normal=1.2),
    'interior': lambda: FK.flat_mat('SH_Interior', (0.035, 0.025, 0.018), 0.9),
}
_prev_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _prev_M(k)

piece, merge, face_xf, tube = BK.piece, BK.merge, BK.face_xf, BK.tube
mem = BK.mem

X0, X1 = -HX, HX
F_FRONT = face_xf((X0, Y0, 0), (0, -1, 0))
F_RIGHT = face_xf((X1, Y0, 0), (1, 0, 0))
F_BACK = face_xf((X1, Y1, 0), (0, 1, 0))
F_LEFT = face_xf((X0, Y1, 0), (-1, 0, 0))
LW, LS = X1 - X0, Y1 - Y0

# ----------------------------------------------------------------------------- helpers
def box(pc, mat, size, c, cham=0.02, rz=0.0, **kw):
    FK.add_box(pc, mat, size, Matrix.Translation(c) @ Matrix.Rotation(rz, 4, 'Z'), cham=cham, **kw)

def sheet(pc, mat, P, nu, nv, out, uv):
    """Grid patch of P(u, v), wound to face `out`."""
    n = (P(1, 0) - P(0, 0)).cross(P(0, 1) - P(0, 0))
    if n.dot(out) >= 0:
        f = P
    else:
        f = lambda u, v: P(v, u)
        nu, nv = nv, nu
    FK.quad_grid(pc, mat, [(0, 0, 0)] * 4, nu, nv, push=lambda p, u, v: f(u, v), uv_fn=uv)

def tc(pc, edge, n, nc, tag, lift=0.0):
    a0, b0 = edge(0.3); a1, b1 = edge(0.6)
    flip = (b0 - a0).cross(a1 - a0).dot(n) < 0
    BK.tile_courses(pc, edge, n, nc, tag, flip=flip, lift=lift)

def lathe(pc, mat, prof, segs, c, closed=False, soft=True):
    """Revolve a closed or open (r, z) profile about the vertical axis at c."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rings = [[bm.verts.new(Vector((c.x + r * math.cos(2 * math.pi * k / segs), c.y + r * math.sin(2 * math.pi * k / segs), c.z + z)))
              for k in range(segs)] for r, z in prof]
    last = len(prof) if closed else len(prof) - 1
    for i in range(last):
        a, b = rings[i], rings[(i + 1) % len(prof)]
        for k in range(segs):
            bm.faces.new((a[k], a[(k + 1) % segs], b[(k + 1) % segs], b[k]))
    bm.normal_update()
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0.1, 0.2))
    FK._merge(pc, bm, mat)

def disc(pc, mat, c, r, segs=14, normal_neg_y=True, uv=(0, 0)):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    bmesh.ops.create_circle(bm, cap_ends=True, segments=segs, radius=r)
    bm.transform(Matrix.Translation(c) @ Matrix.Rotation(math.radians(90 if normal_neg_y else -90), 4, 'X'))
    bm.normal_update()
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, uv)
    FK._merge(pc, bm, mat)

def ring_pts(c, r, plane='x', n=14):
    """Circle points about c in the plane normal to x ('x'), y ('y') or z ('z')."""
    out = []
    for k in range(n + 1):
        a = 2 * math.pi * k / n
        if plane == 'x':
            out.append(c + Vector((0, math.cos(a) * r, math.sin(a) * r)))
        elif plane == 'y':
            out.append(c + Vector((math.cos(a) * r, 0, math.sin(a) * r)))
        else:
            out.append(c + Vector((math.cos(a) * r, math.sin(a) * r, 0)))
    return out

# ----------------------------------------------------------------------------- walls
def quoins(pc, L, phase):
    R = FK.rng('quoin%d%f' % (phase, L))
    for ci in range(9):
        z0 = PL + 0.02 + ci * 0.42
        for end in (0, 1):
            ln = 0.56 if (ci + phase + end) % 2 == 0 else 0.34
            ln += R.uniform(-0.03, 0.03)
            u = ln / 2 if end == 0 else L - ln / 2
            xf = Matrix.Translation((u, 0.025 + R.uniform(-0.01, 0.01), z0 + 0.2)) @ Matrix.Rotation(math.radians(R.uniform(-0.8, 0.8)), 4, 'Y')
            FK.add_box(pc, 'stone', (ln - 0.03, 0.14, 0.39), xf, cham=0.03, uv_world=True, wob=0.01, seed=R.random() * 90)

def wall(tag, L, opens, door=None, quoin_phase=0):
    pc = piece(tag)
    holes = [(o[0], o[1], o[2], o[3]) for o in opens]
    if door:
        holes.append((door[0], door[1], PL - 0.05, door[3]))
    FK.wall_panel(pc, 'lime', 0.0, 0.0, L, PL - 0.05, PLATE, holes=holes, bulge=0.014, cell=0.6)
    quoins(pc, L, quoin_phase)
    # wall plate beam at the eave
    mem(pc, (0.0, PLATE - 0.12), (L, PLATE - 0.12), w=0.24, d=0.30, proud=0.06, name=tag + 'plate')
    for k, (u0, u1, z0, z1) in enumerate(opens):
        BK.window(pc, 0.0, u0, u1, z0, z1, zlib.crc32(tag.encode()) % 97 + k, came=0.30)
        q = FK.quad_grid
        q(pc, 'interior', [(u0, 0.5, z0), (u1, 0.5, z0), (u1, 0.5, z1), (u0, 0.5, z1)], 1, 1, uv_fn=lambda c: (c.x, c.z))
        mem(pc, (u0 - 0.3, z1 + 0.14), (u1 + 0.3, z1 + 0.14), w=0.28, d=0.22, proud=0.09, name='%slin%d' % (tag, k))
        for s, uu in ((0, u0 - 0.075), (1, u1 + 0.075)):
            FK.add_box(pc, 'stone', (0.15, 0.14, z1 - z0 + 0.22), Matrix.Translation((uu, 0.025, (z0 + z1) / 2 + 0.05)),
                       cham=0.025, uv_world=True, wob=0.008, seed=k * 3 + s + L)
    segs, a = [], 0.0
    if door:
        segs = [(0.0, door[0] - 0.02), (door[1] + 0.02, L)]
        BK.doorway(pc, door[0], door[1], door[2], door[3])
        for uu in (door[0] - 0.12, door[1] + 0.12):
            mem(pc, (uu, door[2]), (uu, door[3] + 0.28), w=0.22, d=0.22, proud=0.07, name='%sjamb%f' % (tag, uu))
        mem(pc, (door[0] - 0.34, door[3] + 0.17), (door[1] + 0.34, door[3] + 0.17), w=0.32, d=0.28, proud=0.11, name=tag + 'doorlin')
    else:
        segs = [(0.0, L)]
    for s0, s1 in segs:
        BK.plinth_run(pc, s0, s1, zlib.crc32(tag.encode()) % 31 + s0)
    return pc

def gable_boards(tag, L, vent_z=5.9):
    """Green boarded gable above the eave plate: vertical boards, bressumer, battens and a round loft vent."""
    pc = piece(tag)
    R = FK.rng('gb' + tag)
    half = L / 2
    n = 22; w = L / n
    mem(pc, (0.0, PLATE + 0.02), (L, PLATE + 0.02), w=0.22, d=0.24, proud=0.08, name=tag + 'bress')
    for k in range(n):
        u = (k + 0.5) * w
        ax = abs(u - half) + w / 2
        h = top_z(ax) - 0.27 - (PLATE + 0.1)
        if h < 0.12:
            continue
        h -= R.uniform(0.0, 0.04)
        FK.add_box(pc, 'shutter', (w - 0.012, 0.05, h), Matrix.Translation((u, -0.012 + R.uniform(-0.006, 0.006), PLATE + 0.1 + h / 2)) @
                   Matrix.Rotation(math.radians(R.uniform(-0.5, 0.5)), 4, 'Y'), cham=0.008, long_axis=2, wob=0.003, seed=R.random() * 80)
    for zb in (5.35, 6.55):
        ax = XE - (zb + 0.27 - ZE) / T
        if ax > 0.3:
            mem(pc, (half - ax + 0.05, zb), (half + ax - 0.05, zb), w=0.13, d=0.07, proud=0.075, name='%sbat%f' % (tag, zb))
    # round loft vent: applied oak ring, dark liner, cross bars
    c = Vector((half, -0.07, vent_z))
    tube(pc, 'oak', ring_pts(c, 0.30, 'y', 16), 0.035, 5)
    disc(pc, 'interior', Vector((half, -0.055, vent_z)), 0.29, 16)
    for sgn in (0, 1):
        FK.add_box(pc, 'oak', (0.58 if sgn == 0 else 0.04, 0.04, 0.04 if sgn == 0 else 0.58),
                   Matrix.Translation((half, -0.075, vent_z)), cham=0.004, seed=sgn)
    return pc

def hood():
    """Tiled pent hood over the door on knee brackets."""
    pc = piece('Hood')
    hw = 1.12
    def P(u, v, off=0.0):
        return Vector((u * hw, Y0 - 0.98 * v, 3.12 - 0.60 * v))
    n = Vector((0, -0.52, 0.85)).normalized()
    sheet(pc, 'oak', lambda u, v: P(2 * u - 1, v) - n * 0.20, 3, 3, -n, lambda c: (c.x / TILE, c.y / TILE))
    sheet(pc, 'roof', lambda u, v: P(2 * u - 1, v) - n * 0.06, 3, 3, n, lambda c: (c.x / TILE, c.y / TILE))
    for sx in (-1, 1):
        sheet(pc, 'oak', lambda u, v, sx=sx: P(sx, v) - n * (0.06 + 0.14 * u), 1, 3, Vector((sx, 0, 0)), lambda c: (c.y / TILE, c.z / TILE))
    tc(pc, lambda t: (P(-1, t), P(1, t)), n, 6, 'hood')
    # fascia and two rafters
    FK.member(pc, 'oak', P(-0.92, 1.0) - n * 0.12, P(0.92, 1.0) - n * 0.12, 0.2, 0.07, name='hoodfascia', up=(0, -1, 0.3))
    for sx in (-0.5, 0.5):
        FK.member(pc, 'oak', P(sx / hw, 0.0) - n * 0.22, P(sx / hw, 1.0) - n * 0.22, 0.10, 0.12, name='hoodraft%f' % sx, up=tuple(-n))
    # knee brackets
    for sx in (-0.95, 0.95):
        FK.member(pc, 'oak', Vector((sx, Y0 - 0.04, 1.95)), Vector((sx, Y0 - 0.88, 2.43)), 0.11, 0.10, name='knee%f' % sx, up=(1, 0, 0))
        FK.member(pc, 'oak', Vector((sx, Y0 - 0.88, 2.0)), Vector((sx, Y0 - 0.88, 2.55)), 0.10, 0.10, name='hpost%f' % sx, up=(1, 0, 0))
    return pc

def front_extras():
    """Sign hooks, bench, notice board (frame, empty-sign state)."""
    pc = piece('FrontKit')
    R = FK.rng('front')
    # two sign hooks above the hood: wall plate, arm and upturned tip
    for sx in (-0.62, 0.62):
        box(pc, 'iron', (0.12, 0.03, 0.16), Vector((sx, Y0 - 0.025, 4.10)), cham=0.006, seed=sx)
        box(pc, 'iron', (0.035, 0.22, 0.035), Vector((sx, Y0 - 0.13, 4.13)), cham=0.006, seed=sx + 1)
        box(pc, 'iron', (0.035, 0.035, 0.09), Vector((sx, Y0 - 0.225, 4.17)), cham=0.006, seed=sx + 2)
        for bx in (-0.04, 0.04):
            box(pc, 'iron', (0.03, 0.03, 0.03), Vector((sx + bx, Y0 - 0.05, 4.14)), cham=0.006, seed=bx)
    # bench left of the door
    bx0, bx1, by = -2.2, -0.85, Y0 - 0.26
    for i in range(2):
        FK.add_box(pc, 'oak', (bx1 - bx0, 0.19, 0.07), Matrix.Translation(((bx0 + bx1) / 2, by - 0.1 + i * 0.2 - 0.01, 0.52)), cham=0.012,
                   long_axis=0, wob=0.004, seed=40 + i)
    for sx in (bx0 + 0.18, bx1 - 0.18):
        FK.add_box(pc, 'oak', (0.09, 0.36, 0.50), Matrix.Translation((sx, by, 0.25)), cham=0.012, wob=0.004, seed=sx + 30)
    FK.member(pc, 'oak', Vector((bx0 + 0.2, by + 0.14, 0.52)), Vector((bx1 - 0.2, by + 0.14, 0.52)), 0.06, 0.06, name='benchrail')
    # notice board right of the door: oak frame, slate, pinned papers, chalk marks, a little pent cap
    nx0, nx1, nz0, nz1 = 1.30, 2.02, 1.35, 2.25
    cx, cz = (nx0 + nx1) / 2, (nz0 + nz1) / 2
    yb = Y0 - 0.05
    FK.add_box(pc, 'slate', (nx1 - nx0 - 0.12, 0.04, nz1 - nz0 - 0.12), Matrix.Translation((cx, yb - 0.01, cz)), cham=0.006, seed=5.5)
    for nm, a, b, w in (('l', (nx0 + 0.05, nz0), (nx0 + 0.05, nz1), 0.10), ('r', (nx1 - 0.05, nz0), (nx1 - 0.05, nz1), 0.10),
                        ('b', (nx0, nz0 + 0.05), (nx1, nz0 + 0.05), 0.10), ('t', (nx0, nz1 - 0.05), (nx1, nz1 - 0.05), 0.10)):
        FK.member(pc, 'oak', Vector((a[0], yb - 0.04, a[1])), Vector((b[0], yb - 0.04, b[1])), w, 0.07, name='nb' + nm, cham=0.012,
                  up=(0, -1, 0))
    FK.add_box(pc, 'oak', (nx1 - nx0 + 0.24, 0.26, 0.05), Matrix.Translation((cx, yb - 0.09, nz1 + 0.07)) @ Matrix.Rotation(math.radians(-18), 4, 'X'),
               cham=0.01, long_axis=0, seed=6.6)
    for sx in (nx0 + 0.04, nx1 - 0.04):
        FK.member(pc, 'oak', Vector((sx, yb - 0.02, nz1 - 0.06)), Vector((sx, yb - 0.17, nz1 + 0.07)), 0.05, 0.05, name='nbk%f' % sx,
                  up=(1, 0, 0))
    for k in range(6):
        px = nx0 + 0.20 + (k % 2) * 0.30 + R.uniform(-0.02, 0.02)
        pz = nz0 + 0.24 + (k // 2) * 0.26 + R.uniform(-0.02, 0.02)
        FK.add_box(pc, 'chalk', (0.17, 0.012, 0.22), Matrix.Translation((px, yb - 0.05 - k * 0.001, pz)) @ Matrix.Rotation(math.radians(R.uniform(-7, 7)), 4, 'Y'),
                   cham=0.0, seed=k + 8)
        box(pc, 'iron', (0.022, 0.02, 0.022), Vector((px, yb - 0.065, pz + 0.09)), cham=0.004, seed=k)
    for k in range(4):   # chalk scrawl on the bare slate
        FK.add_box(pc, 'chalk', (0.10 + 0.04 * (k % 2), 0.008, 0.012), Matrix.Translation((nx0 + 0.25 + 0.2 * (k % 2), yb - 0.035, nz0 + 0.06 + 0.04 * k)) @
                   Matrix.Rotation(math.radians(R.uniform(-5, 5)), 4, 'Y'), cham=0.0, seed=k + 20)
    return pc

# ----------------------------------------------------------------------------- roof
def sp(side, y, t):
    """Top of a side slope: t = 0 at the eave tip, 1 at the ridge."""
    s = (y - YC) / HL
    sag = RS * t * max(0.0, 1 - s * s)
    wave = 0.02 * noise.noise(Vector((y * 0.6, t * 2.5, side * 1.7)))
    return Vector((side * XE * (1 - t), y, ZE + (ZR - ZE) * t - sag + wave * math.sin(math.pi * min(1.0, t))))

def roof():
    pc = piece('Roof')
    for side in (1, -1):
        n = Vector((side * T, 0, 1)).normalized()
        ynp = lambda u: YF + (YB - YF) * u
        sheet(pc, 'roof', lambda u, v: sp(side, ynp(u), v) - n * 0.06, 14, 8, n, lambda c: (c.y / TILE, c.z / TILE))
        sheet(pc, 'oak', lambda u, v: sp(side, ynp(u), v) - n * 0.20, 14, 4, -n, lambda c: (c.y / TILE, c.x / TILE))
        for end, ye in ((0, YF), (1, YB)):
            sheet(pc, 'oak', lambda u, v, ye=ye: sp(side, ye, v) - n * (0.06 + 0.14 * u), 1, 4, Vector((0, -1 if end == 0 else 1, 0)),
                  lambda c: (c.x / TILE, c.z / TILE))
        nseg = 4
        for k in range(nseg):
            ya, yb_ = ynp(k / nseg), ynp((k + 1) / nseg)
            tc(pc, lambda t, ya=ya, yb_=yb_: (sp(side, ya, t), sp(side, yb_, t)), n, 18, 'rs%d%d' % (side, k))
        for k in range(7):
            y = YF + 0.3 + k * (YB - YF - 0.6) / 6
            FK.member(pc, 'oak', sp(side, y, 0.02) - n * 0.28, sp(side, y, 0.17) - n * 0.28, 0.13, 0.17, name='raft%d%d' % (side, k), up=tuple(-n))
        FK.member(pc, 'oak', sp(side, YF, 0) - n * 0.13, sp(side, YB, 0) - n * 0.13, 0.2, 0.07, name='fascia%d' % side, up=(side, 0, 0.4))
        for end, ye in ((0, YF), (1, YB)):
            sg = -1 if end == 0 else 1
            FK.member(pc, 'oak', sp(side, ye, 0) - n * 0.11 + Vector((0, sg * 0.03, 0)), sp(side, ye, 1) - n * 0.11 + Vector((0, sg * 0.03, 0)),
                      0.30, 0.08, name='barge%d%d' % (side, end), up=(0, sg, 0), wob=0.003)
    for k in range(7):    # ridge in short runs that follow the sag
        ya = YF + (YB - YF) * k / 7; yb_ = YF + (YB - YF) * (k + 1) / 7
        pa = sp(1, ya, 1.0) + Vector((0, 0, 0.12)); pb = sp(1, yb_, 1.0) + Vector((0, 0, 0.12))
        BK.ridge_tiles(pc, pa, pb, 2, 'ridge%d' % k)
    return pc

def chimney():
    pc = piece('Chimney')
    x0, x1, y0, y1, top = CH
    R = FK.rng('chim')
    z = 6.5
    ci = 0
    while z < top - 0.55:
        h = R.uniform(0.34, 0.46)
        FK.add_box(pc, 'stone_dk', (x1 - x0 + R.uniform(-0.02, 0.02), y1 - y0 + R.uniform(-0.02, 0.02), h - 0.03),
                   Matrix.Translation(((x0 + x1) / 2 + R.uniform(-0.012, 0.012), (y0 + y1) / 2, z + h / 2)), cham=0.03, uv_world=True, wob=0.008, seed=ci)
        z += h; ci += 1
    for k, (g, h) in enumerate(((0.10, 0.12), (0.17, 0.10))):
        FK.add_box(pc, 'stone', (x1 - x0 + g, y1 - y0 + g, h), Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, z + 0.06 + k * 0.11)), cham=0.02,
                   uv_world=True, seed=k + 9)
    z += 0.24
    for px in (-0.14, 0.14):
        FK.add_box(pc, 'roof', (0.2, 0.2, 0.34), Matrix.Translation((px, (y0 + y1) / 2, z + 0.17)), cham=0.03, uv_world=True, seed=px + 5)
        FK.add_box(pc, 'roof', (0.25, 0.25, 0.06), Matrix.Translation((px, (y0 + y1) / 2, z + 0.37)), cham=0.015, uv_world=True, seed=px + 6)
    return pc

# ----------------------------------------------------------------------------- bell-cote
def cote():
    pc = piece('Cote')
    px = 0.55; ya, yb_ = COTE_Y - 0.30, COTE_Y + 0.30
    zb, zt = 6.95, 9.55
    # sill beams resting on the roof, posts, plates, rail
    for sx in (-1, 1):
        FK.member(pc, 'oak', Vector((sx * px, YF + 0.15, 7.10)), Vector((sx * px, ya + 0.9, 7.10)), 0.22, 0.20, name='csill%d' % sx, up=(1, 0, 0))
        for yy in (ya, yb_):
            FK.member(pc, 'oak', Vector((sx * px, yy, zb)), Vector((sx * px, yy, zt)), 0.15, 0.15, name='cpost%d%f' % (sx, yy), up=(1, 0, 0))
        FK.member(pc, 'oak', Vector((sx * px, ya - 0.12, zt)), Vector((sx * px, yb_ + 0.12, zt)), 0.17, 0.17, name='cplate%d' % sx, up=(1, 0, 0))
        FK.member(pc, 'oak', Vector((sx * px, ya, AXLE_Z)), Vector((sx * px, yb_, AXLE_Z)), 0.13, 0.13, name='crail%d' % sx, up=(1, 0, 0))
        for yy, sg in ((ya, 1), (yb_, -1)):
            FK.member(pc, 'oak', Vector((sx * px, yy, 8.55)), Vector((sx * px, yy + sg * 0.26, zt - 0.04)), 0.09, 0.09, name='cbr%d%f' % (sx, yy), up=(1, 0, 0))
        # bare bearing: iron plate on the rail's inner face with a ring around the gudgeon hole
        c = Vector((sx * (px - 0.075), COTE_Y, AXLE_Z))
        box(pc, 'iron', (0.03, 0.24, 0.22), c, cham=0.006, seed=sx + 70)
        tube(pc, 'iron', ring_pts(c + Vector((-sx * 0.025, 0, 0)), 0.075, 'x', 12), 0.018, 5)
        for dy, dz in ((-0.09, -0.08), (0.09, -0.08), (-0.09, 0.08), (0.09, 0.08)):
            box(pc, 'iron', (0.025, 0.025, 0.025), c + Vector((-sx * 0.02, dy, dz)), cham=0.004, seed=dy + dz)
    for yy in (ya, yb_):
        FK.member(pc, 'oak', Vector((-px - 0.12, yy, zt)), Vector((px + 0.12, yy, zt)), 0.17, 0.17, name='ctie%f' % yy)
        FK.member(pc, 'oak', Vector((-px, yy, 8.0)), Vector((px, yy, 8.0)), 0.10, 0.10, name='cgirt%f' % yy)
    # cote roof: small tiled gable, ridge along y like the main roof
    hx = 0.88; ey0, ey1 = YF + 0.05, COTE_Y + 0.50
    ze, zr = 9.62, 10.50
    def cp(side, y, t):
        return Vector((side * hx * (1 - t), y, ze + (zr - ze) * t))
    for side in (1, -1):
        n = Vector((side * (zr - ze) / hx, 0, 1)).normalized()
        sheet(pc, 'roof', lambda u, v: cp(side, ey0 + (ey1 - ey0) * u, v) - n * 0.05, 2, 2, n, lambda c: (c.y / TILE, c.z / TILE))
        sheet(pc, 'oak', lambda u, v: cp(side, ey0 + (ey1 - ey0) * u, v) - n * 0.13, 2, 2, -n, lambda c: (c.y / TILE, c.x / TILE))
        for end, ye in ((0, ey0), (1, ey1)):
            sg = -1 if end == 0 else 1
            sheet(pc, 'oak', lambda u, v, ye=ye: cp(side, ye, v) - n * (0.05 + 0.08 * u), 1, 2, Vector((0, sg, 0)), lambda c: (c.x / TILE, c.z / TILE))
            FK.member(pc, 'oak', cp(side, ye, 0) - n * 0.09 + Vector((0, sg * 0.03, 0)), cp(side, ye, 1) - n * 0.09 + Vector((0, sg * 0.03, 0)),
                      0.20, 0.06, name='cbarge%d%d' % (side, end), up=(0, sg, 0))
        tc(pc, lambda t: (cp(side, ey0, t), cp(side, ey1, t)), n, 6, 'cote%d' % side)
        FK.member(pc, 'oak', cp(side, ey0, 0) - n * 0.10, cp(side, ey1, 0) - n * 0.10, 0.14, 0.06, name='cfas%d' % side, up=(side, 0, 0.4))
    BK.ridge_tiles(pc, Vector((0, ey0 + 0.05, zr + 0.05)), Vector((0, ey1 - 0.12, zr + 0.05)), 2, 'cote')
    return pc

# ----------------------------------------------------------------------------- yard
def yard():
    pc = piece('Yard')
    R = FK.rng('yard')
    for k, (y, w) in enumerate(((-3.02, 0.85), (-3.36, 0.70), (-3.64, 0.60))):
        c = Vector((R.uniform(-0.1, 0.1), y, 0.025))
        FK.add_box(pc, 'stone', (w, 0.45 + R.uniform(-0.03, 0.03), 0.07), Matrix.Translation(c) @ Matrix.Rotation(R.uniform(-0.2, 0.2), 4, 'Z'),
                   cham=0.03, segs=2, uv_world=True, wob=0.012, seed=k + 50)
    # hopscotch along +X, chalked
    ys = [-2.0, -1.45, -0.9, -0.2, 0.35, 1.05, 1.6, 2.3]
    dbl = {3, 6}
    for k, y in enumerate(ys):
        xs = (3.38, 3.74) if k in dbl else (3.56,)
        for xi, x in enumerate(xs):
            sz = 0.34 if k in dbl else 0.40
            FK.add_box(pc, 'stone', (sz, sz, 0.05), Matrix.Translation((x + R.uniform(-0.01, 0.01), y, 0.02)) @ Matrix.Rotation(R.uniform(-0.15, 0.15), 4, 'Z'),
                       cham=0.02, segs=2, uv_world=True, wob=0.008, seed=k * 2 + xi + 60)
            FK.add_box(pc, 'chalk', (sz + 0.03, 0.012, 0.008), Matrix.Translation((x, y + sz / 2 - 0.01, 0.048)), cham=0.0, seed=k + 80)
            FK.add_box(pc, 'chalk', (0.008, sz + 0.03, 0.008), Matrix.Translation((x + (sz / 2 - 0.01), y, 0.048)), cham=0.0, seed=k + 81)
            FK.add_box(pc, 'chalk', (0.07, 0.10, 0.008), Matrix.Translation((x - 0.05, y - 0.04, 0.048)) @ Matrix.Rotation(R.uniform(-0.5, 0.5), 4, 'Z'),
                       cham=0.0, seed=k + 82)
    # water butt, back right
    c = Vector((3.35, 2.75, 0.0))
    prof = [(0.28, 0.0), (0.33, 0.22), (0.35, 0.46), (0.33, 0.70), (0.30, 0.86)]
    bm_prof = prof
    lathe(pc, 'oak', bm_prof, 14, c)
    lathe(pc, 'oak', [(r - 0.025, z) for r, z in reversed(bm_prof)], 14, c)
    lathe(pc, 'oak', [(0.28, 0.0), (0.0 + 0.001, 0.0)], 14, c)   # floor
    disc(pc, 'glass', c + Vector((0, 0, 0.76)), 0.285, 14, normal_neg_y=False)
    for z, r in ((0.10, 0.295), (0.46, 0.357), (0.78, 0.315)):
        tube(pc, 'iron', ring_pts(c + Vector((0, 0, z)), r, 'z', 14), 0.022, 5)
    # a leaning chalk slate and a tin cup by the step? keep it plain: one tipped stool beside the bench
    return pc

# ----------------------------------------------------------------------------- parts
def part_door():
    FK.DOOR = DOOR
    pc = FK.kit_door()
    pc.name = 'Part_Door'
    return pc, F_FRONT, Vector(((DOOR[0] + DOOR[1]) / 2, 0.0, DOOR[2]))

def part_bell():
    pc = piece('Part_Bell')
    az = AXLE_Z
    c = Vector((0, COTE_Y, az))
    # bell: closed (r, z) profile, outer lip upward over the crown, then down the inside
    top = az - 0.07
    outer = [(0.335, -0.62), (0.31, -0.52), (0.26, -0.38), (0.20, -0.22), (0.14, -0.10), (0.09, -0.03), (0.04, 0.0)]
    inner = [(0.04, -0.05), (0.10, -0.10), (0.16, -0.20), (0.22, -0.34), (0.27, -0.48), (0.285, -0.58), (0.275, -0.62)]
    lathe(pc, 'bronze', outer + inner, 16, Vector((0, COTE_Y, top)), closed=True)
    # lip band and shoulder ridges
    for z, r in ((-0.60, 0.338), (-0.52, 0.312), (-0.25, 0.215)):
        tube(pc, 'bronze', ring_pts(Vector((0, COTE_Y, top + z)), r, 'z', 16), 0.014, 4)
    # clapper
    tube(pc, 'iron', [Vector((0, COTE_Y, top - 0.08)), Vector((0, COTE_Y, top - 0.50))], 0.012, 5)
    lathe(pc, 'iron', [(0.0001, -0.07), (0.05, -0.03), (0.06, 0.03), (0.05, 0.09), (0.0001, 0.12)], 8, Vector((0, COTE_Y, top - 0.60)))
    # oak headstock with iron straps, crown bolts, axle gudgeons
    FK.add_box(pc, 'oak', (1.02, 0.17, 0.15), Matrix.Translation(c), cham=0.015, long_axis=0, wob=0.003, seed=71)
    for sx in (-0.26, 0.26):
        FK.add_box(pc, 'iron', (0.05, 0.20, 0.18), Matrix.Translation(c + Vector((sx, 0, -0.01))), cham=0.006, seed=sx + 72)
    for sx in (-0.08, 0.08):
        FK.add_box(pc, 'iron', (0.035, 0.035, 0.05), Matrix.Translation(c + Vector((sx, 0, -0.095))), cham=0.006, seed=sx + 73)
    for sx in (-1, 1):
        tube(pc, 'iron', [c + Vector((sx * 0.48, 0, 0)), c + Vector((sx * 0.585, 0, 0))], 0.035, 6)
        FK.add_box(pc, 'iron', (0.05, 0.12, 0.12), Matrix.Translation(c + Vector((sx * 0.46, 0, 0))), cham=0.008, seed=sx + 74)
    # half wheel: oak half-disc with an iron tyre and the rope eye at its lower tip
    wr = 0.34; wx = 0.40
    for k in range(7):
        a0 = math.pi * k / 7; a1 = math.pi * (k + 1) / 7
        mid = (a0 + a1) / 2
        arcl = wr * (a1 - a0)
        FK.add_box(pc, 'oak', (0.055, arcl + 0.01, wr * 0.92), Matrix.Translation(c + Vector((wx, math.cos(mid) * wr * 0.54, math.sin(mid) * wr * 0.54))) @
                   Matrix.Rotation(mid - math.pi / 2, 4, 'X'), cham=0.006, seed=k + 75)
    tube(pc, 'iron', [c + Vector((wx, math.cos(math.pi * k / 14) * (wr + 0.01), math.sin(math.pi * k / 14) * (wr + 0.01))) for k in range(15)], 0.018, 5)
    for sy in (-1, 1):
        FK.add_box(pc, 'iron', (0.06, 0.04, 0.04), Matrix.Translation(c + Vector((wx, sy * wr * 0.2, 0.0))), cham=0.004, seed=sy + 76)
    # rope from the wheel's front tip, a loose S down to a wooden toggle
    p0 = c + Vector((wx, wr, 0.0))
    pts = [p0 + Vector((0.01 * math.sin(i * 0.9), 0.02 * math.sin(i * 0.5), -0.1 * i)) for i in range(9)]
    tube(pc, 'rope', pts, 0.02, 6)
    FK.add_box(pc, 'oak', (0.05, 0.05, 0.16), Matrix.Translation(pts[-1] + Vector((0, 0, -0.07))), cham=0.012, seed=77)
    return pc, None, c

def part_sign():
    pc = piece('Part_Sign')
    ys = Y0 - 0.225                   # hook tips
    yb_ = Y0 - 0.20
    bw, bh = 1.46, 0.50
    zt = 3.96; zc = zt - bh / 2
    R = FK.rng('sign')
    # chains from the hook tips to the board's eyes
    for sx in (-0.62, 0.62):
        for k in range(3):
            FK.add_box(pc, 'iron', (0.012, 0.03, 0.05), Matrix.Translation((sx, ys, 4.115 - k * 0.055)) @ Matrix.Rotation((k % 2) * 1.57, 4, 'Z'),
                       cham=0.0, seed=k + sx)
        box(pc, 'iron', (0.05, 0.03, 0.05), Vector((sx, yb_, zt + 0.005)), cham=0.006, seed=sx + 3)
    # board: planks, oak edge, gilt lettering blocks
    FK.add_box(pc, 'shutter', (bw - 0.1, 0.045, bh - 0.1), Matrix.Translation((0, yb_, zc)), cham=0.008, seed=90, wob=0.002)
    for nm, a, b, w in (('t', (-bw / 2, zt - 0.025), (bw / 2, zt - 0.025), 0.06), ('b', (-bw / 2, zt - bh + 0.025), (bw / 2, zt - bh + 0.025), 0.06),
                        ('l', (-bw / 2 + 0.03, zt), (-bw / 2 + 0.03, zt - bh), 0.06), ('r', (bw / 2 - 0.03, zt), (bw / 2 - 0.03, zt - bh), 0.06)):
        FK.member(pc, 'oak', Vector((a[0], yb_ - 0.01, a[1])), Vector((b[0], yb_ - 0.01, b[1])), w, 0.07, name='sg' + nm, cham=0.01, up=(0, -1, 0))
    x = -0.50
    for k in range(7):
        w = R.choice((0.07, 0.10, 0.13))
        FK.add_box(pc, 'gilt', (w, 0.02, 0.19), Matrix.Translation((x + w / 2, yb_ - 0.032, zc + 0.02)), cham=0.004, seed=k + 91)
        if k % 3 == 0:
            FK.add_box(pc, 'gilt', (w, 0.02, 0.035), Matrix.Translation((x + w / 2, yb_ - 0.032, zc + 0.02)), cham=0.0, seed=k + 92)
        x += w + 0.065
    FK.add_box(pc, 'gilt', (0.9, 0.02, 0.03), Matrix.Translation((0, yb_ - 0.032, zc - 0.15)), cham=0.004, seed=93)
    return pc, None, Vector((0, ys, 4.12))

# ----------------------------------------------------------------------------- wear, assembly
def lean_co(p):
    return Vector((p.x - LEAN[0] * max(0.0, p.z - PL), p.y - LEAN[1] * max(0.0, p.z - PL), p.z))

SOCKETS = {}
RUNOFF = [WIN_Z[0] - 0.11, DOOR[3] + 0.4]

def write_wear(ob):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    mw = ob.matrix_world
    mats = [m.name for m in me.materials]
    cx, cy = (CH[0] + CH[1]) / 2, (CH[2] + CH[3]) / 2
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        mname = mats[poly.material_index] if mats else ''
        roofish = mname == 'FK_RoofTile'
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 1.7)
            streak = noise.noise(Vector((p.x * 4.0, p.y * 4.0, p.z * 0.5)))
            r = max(0.0, 1 - p.z / (0.9 + 0.25 * nz)) ** 1.2 * 0.75
            r = max(r, 0.22 * max(0.0, noise.noise(p * 0.6 + Vector((3.0, 1.0, 2.0)))) + 0.06 * max(0.0, nz))
            if PLATE - 0.7 < p.z < PLATE + 0.1 and abs(p.x) > HX - 0.35:
                r = max(r, 0.24 + 0.1 * streak)                 # damp under the eaves
            for zs_ in RUNOFF:
                dz = zs_ - p.z
                if 0.0 < dz < 0.9 and abs(n.z) < 0.5:
                    r = max(r, (1 - dz / 0.9) ** 1.6 * max(0.0, 0.10 + 0.32 * streak))
            if abs(p.x - cx) < 0.9 and abs(p.y - cy) < 0.9 and p.z > CH[4] - 1.5:
                r = max(r, min(0.9, (p.z - (CH[4] - 1.5)) / 1.2) * (0.7 + 0.2 * streak))   # flue soot
            if p.z > 5.0 and abs(p.y - cy) < 0.7 and abs(p.x) < 1.2 and n.z > 0.2:
                r = max(r, 0.30 * (1 - min(1.0, abs(p.y - cy) / 0.7)) * (0.7 + 0.3 * streak))   # smoke drift on the ridge tiles
            g = 0.0
            if roofish and n.z > 0.2:
                z_rel = p.z - ZE
                if p.z > 3.8:
                    g = max(0.0, 1 - z_rel / 1.3) * 0.5 + max(0.0, 0.1 + 0.4 * nz) * 0.18
                else:
                    g = 0.12 + 0.12 * max(0.0, nz)
                if p.y > 1.4:
                    g = max(g, 0.2 + 0.2 * nz)
                tv = ((poly.index * 2654435761) % 1009) / 1009.0
                r = max(r, 0.26 * tv * tv)
            elif n.z > 0.6 and p.z < PL + 0.1:
                g = 0.3 + 0.3 * nz
            if mname == 'FK_Shutter' and ob.name == 'Frame':
                r = max(r, 0.30 * (1 - min(1.0, (p.z - PLATE) / 3.0)) + 0.14 * streak + 0.10 * max(0.0, nz))   # splash on the lower gable boards
            b = 0.0
            dd = Vector((p.x, p.y, 0)).length
            dstep = (Vector((p.x, p.y, 0)) - Vector((0, Y0 - 0.5, 0))).length
            if p.z < 0.3 and dstep < 1.6 and n.z > 0.5:
                b = 0.9 * (1 - dstep / 1.6) ** 0.5               # foot-polished step and path
                g = 0.0
            if 0.4 < p.z < 0.6 and p.y < Y0 - 0.1 and p.x < -0.7 and n.z > 0.5:
                b = max(b, 0.55)                                  # the bench seat
            if p.x > 3.2 and p.z < 0.2 and n.z > 0.5 and p.y < 2.6:
                b = max(b, 0.45)                                  # hopscotch slabs, hop-worn
                g = 0.0
            if ob.name == 'Part_Door':
                lx = abs(p.x - (DOOR[1] - HX - 0.17)); lz = abs(p.z - 1.1)
                if lx < 0.25 and lz < 0.35:
                    b = max(b, 0.5 * (1 - lx / 0.25)); r = max(r, 0.35 * (1 - lz / 0.35))
            if ob.name == 'Part_Bell':
                if mname == 'SH_Bronze':
                    r = 0.15 + 0.2 * max(0.0, streak)
                    b = max(0.0, min(1.0, (AXLE_Z - 0.55 - p.z) / 0.2)) * 0.7      # striking band polished
                if mname == 'SH_Rope':
                    b = 0.2 + 0.4 * max(0.0, (AXLE_Z - 0.7 - p.z) / 0.2)
                    r = 0.15
            if ob.name == 'Part_Sign' and mname == 'FK_Shutter':
                r = max(r, 0.12 + 0.12 * streak + 0.2 * max(0.0, 0.1 - (p.z - 3.46)) * 5)
            attr.data[li].color = (min(1.0, r), min(1.0, g), min(1.0, b), 1.0)

SOFT = {'SH_Bronze', 'SH_Rope', 'SH_Chalk'}

def to_object(pc, parent, origin=None):
    for v in pc.bm.verts:
        v.co = lean_co(v.co)
    if origin is not None:
        bmesh.ops.translate(pc.bm, verts=pc.bm.verts[:], vec=-origin)
    me = bpy.data.meshes.new(pc.name)
    pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats:
        me.materials.append(m)
    for poly in me.polygons:
        if me.materials[poly.material_index].name in SOFT:
            poly.use_smooth = True
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    if origin is not None:
        ob.location = origin
    bpy.context.view_layer.update()      # write_wear reads matrix_world
    write_wear(ob)
    return ob

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def build():
    root = bpy.data.objects.new('Schoolhouse', None); bpy.context.scene.collection.objects.link(root)
    frame = piece('Frame')
    door = (DOOR[0], DOOR[1], DOOR[2], DOOR[3])
    def sidewin(cs):
        return [(c - WIN_W / 2, c + WIN_W / 2, WIN_Z[0], WIN_Z[1]) for c in cs]
    walls = [
        (wall('front', LW, [], door=door, quoin_phase=0), F_FRONT),
        (wall('right', LS, sidewin((1.4, 3.1, 4.8)), quoin_phase=1), F_RIGHT),
        (wall('back', LW, sidewin((1.5, 3.7)), quoin_phase=0), F_BACK),
        (wall('left', LS, sidewin((1.4, 3.1, 4.8)), quoin_phase=1), F_LEFT),
        (gable_boards('gfront', LW), F_FRONT),
        (gable_boards('gback', LW), F_BACK),
    ]
    for pc, xf in walls:
        merge(frame, pc, xf)
    for pc in (hood(), front_extras(), roof(), chimney(), cote(), yard()):
        merge(frame, pc)
    fo = to_object(frame, root)
    total = tris(fo)
    print('  %-12s %6d tris  %d slots' % ('Frame', total, len(fo.data.materials)))
    parts = {}
    for fn, sock in ((part_door, 'Door'), (part_bell, 'Bell'), (part_sign, 'Sign')):
        pc, xf, s_local = fn()
        if xf is not None:
            bmesh.ops.transform(pc.bm, matrix=xf, verts=pc.bm.verts[:])
            s = xf @ s_local
        else:
            s = s_local
        s = lean_co(s)
        ob = to_object(pc, root, origin=s)
        parts[sock] = ob
        t = tris(ob); total += t
        print('  %-12s %6d tris  %s' % (ob.name, t, ', '.join(m.name for m in ob.data.materials)))
        e = bpy.data.objects.new('SOCKET_' + sock, None); bpy.context.scene.collection.objects.link(e)
        e.parent = fo; e.location = s; e.empty_display_size = 0.3
        SOCKETS[sock] = s
    print('  slots:', ', '.join(m.name for m in fo.data.materials))
    fx0, fx1 = X0 - 0.10, X1 + 0.10
    fy0, fy1 = Y0 - 0.10, Y1 + 0.10
    me = bpy.data.meshes.new('FOOTPRINT'); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation(((fx0 + fx1) / 2, (fy0 + fy1) / 2, 0.05)) @ Matrix.Diagonal((fx1 - fx0, fy1 - fy0, 0.1, 1)))
    bm.to_mesh(me); bm.free()
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root
    fp.display_type = 'WIRE'; fp.hide_render = True
    print('FOOTPRINT x %.2f..%.2f  y %.2f..%.2f  (%.2f x %.2f m; M class max 7.5)' % (fx0, fx1, fy0, fy1, fx1 - fx0, fy1 - fy0))
    allx = [(o.matrix_world @ Vector(c)).x for o in [fo] + list(parts.values()) for c in o.bound_box]
    ally = [(o.matrix_world @ Vector(c)).y for o in [fo] + list(parts.values()) for c in o.bound_box]
    allz = [(o.matrix_world @ Vector(c)).z for o in [fo] + list(parts.values()) for c in o.bound_box]
    print('bounds x %.2f..%.2f  y %.2f..%.2f  z 0..%.2f  (foundation +-4.0)' % (min(allx), max(allx), min(ally), max(ally), max(allz)))
    for k, v in SOCKETS.items():
        print('SOCKET_%s (%.3f, %.3f, %.3f)' % (k, v.x, v.y, v.z))
    print('Schoolhouse total tris: %d (cap 40000)' % total)
    # facing check: roof tiles should face up (the thin shells are two-sided)
    up = sum(1 for p in fo.data.polygons if fo.data.materials[p.material_index].name == 'FK_RoofTile' and p.normal.z > 0)
    dn = sum(1 for p in fo.data.polygons if fo.data.materials[p.material_index].name == 'FK_RoofTile' and p.normal.z < -0.2)
    print('roof tile faces up %d down %d' % (up, dn))
    return root, fo, parts, fp

# ----------------------------------------------------------------------------- review
VIEWS_DEF = {
    'tq': (Vector((10.5, -14.5, 4.6)), Vector((0.0, 0, 5.2)), 46),
    'tql': (Vector((-11.5, -13.5, 4.4)), Vector((0.0, 0, 5.2)), 46),
    'back': (Vector((9.0, 13.0, 4.0)), Vector((0.0, 0.5, 4.8)), 44),
    'frame': (Vector((10.5, -14.5, 4.6)), Vector((0.0, 0, 5.2)), 46),
    'frameback': (Vector((-9.0, 13.0, 4.0)), Vector((0.0, 0.5, 4.8)), 44),
    'cote': (Vector((3.4, -8.0, 8.6)), Vector((0.0, -2.6, 8.7)), 36),
    'door': (Vector((2.8, -7.0, 1.9)), Vector((0.0, -3.0, 1.9)), 44),
    'front': (Vector((0.0, -17.0, 4.0)), Vector((0.0, 0, 4.8)), 40),
}

def review(root, parts):
    sc = bpy.context.scene
    FK.RRES = RRES; FK.SAMPLES = SAMPLES
    cam = FK.review_scene(root, variant=True)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(-0.9, -3.4, 0.875))
    fig = bpy.context.object; fig.name = 'Figure175'
    fm = bpy.data.materials.new('Figure'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.18, 0.12, 1)
    fig.data.materials.append(fm)
    for v in VIEWS:
        frm, to, fov = VIEWS_DEF[v]
        hide = v.startswith('frame')
        for o in parts.values():
            o.hide_render = hide
        FK.look(cam, frm, to); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, 'schoolhouse_%s.png' % v)
        bpy.ops.render.render(write_still=True)
        print('rendered', v)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, fo, parts, fp = build()
    if '--export' in argv:
        for o in bpy.context.scene.objects:
            o.select_set(o == root or o.parent in (root, fo))
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Schoolhouse.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                                 mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR',
                                 path_mode='STRIP')   # no texture paths: Unity names textured slots 'Lit' when a cloud path exists
        print('exported', os.path.join(OUT, 'Schoolhouse.fbx'))
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'schoolhouse.blend'))
    if '--no-render' not in argv:
        review(root, parts)
