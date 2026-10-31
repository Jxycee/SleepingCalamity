"""Bakery: Hollowmere's bakery (ring 1, L class, frame + parts), Phase 1's M6 placement target (PHASE1 7.1, P28).

A warm two-storey half-timbered bakery on a sandstone plinth: cream lime plaster, dark adzed oak at about 1.4x real section,
green paint. The front is a gable (ridge runs front to back) clipped into a half-hip at the top, with a bellcast kick at
the side eaves, a jettied upper storey on the front, and a flour loft door with a hoist beam, pulley and rope in the gable;
so it never reads like the approved eave-front facade. The ground storey front has the doorway (worn sandstone step dusted
with flour, flour sacks) and a wide shop window: its top shutter is propped out as an awning and its bottom shutter folds
down as the bread shelf (baskets, loaves, a cloth). The +X side carries a whitewashed oven annex under a catslide roof with
a round-arched brick oven niche and a tall brick chimney stack tied back to the house with iron stays; the -X side has a
firewood stack under a pent roof; the back has a plain back door and windows. Every side is detailed to mid distance
(the building turns four ways); the yard in front (stepping slabs) turns with it.

Parts (P28: the bakery counts only when all are placed), each its own mesh with its pivot at its socket:
  Part_Door  planked green door (hinges, ring pull); without it the doorway stands open on a dark lined interior.
  Part_Oven  the brick oven dome bulging out of the niche: arched mouth with a glowing ember bed, an iron door, a peel;
             without it the niche is a cold, soot-black alcove on an empty hearth slab.
  Part_Sign  oval green board with a carved loaf and a gilt wheat sprig, a gilt pretzel below, on chains; without it
             the wrought-iron bracket on the jetty hangs empty with two open hooks.
SOCKET_Door / SOCKET_Oven / SOCKET_Sign are empties parented to Frame. FOOTPRINT is a flat box over the ground-storey
body, annex, chimney and oven (the collider and NPC-block shape), inside the L class's 10.5 x 10.5 m.

Built from the B4 facade kit helpers (sc_facade_kit: Piece, add_box, member, quad_grid, wall_panel, window, kit_door,
kit_shutters, kit_flowerbox, plinth_run, materials). T2: no unique bake; shared tileables with metre UVs (2 m per tile,
2K maps): B1 plaster, oak_timber, sandstone, roof_tile, painted_wood, iron, canvas, plus the B2 shared old_brick and
soot_coal for the oven. Wear in the linear float colour attribute "wear" like the facade kit (R grime/soot, G moss,
B polish/flour). BK_Ember is the only emissive slot (the lit oven mouth).

refs: Reference/hollowmere/bakery/REFS.md (half-timbered bakehouse, brick oven mouths under a roof, scroll sign brackets
with a pretzel), Reference/hollowmere/architecture/REFS.md, the approved corner captures, PHASE1 3.1/3.4.

Axes (Blender): z up, front -Y, metres. Origin = foundation centre at ground (the L foundation is 11 x 11 m). Export FBX
-Z forward, Y up: the front (Blender -Y) is local +Z in Unity, like the facade, so the builder adds yaw 180 to face the lot's -Z.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_bakery.py -- --out <scratch>
      [--views tq,tql,back,frame,close] [--rres 1280] [--samples 32] [--no-render] [--export] [--blend] [--tris]
  --export writes <out>/Bakery.fbx (root Bakery: Frame, Part_Door, Part_Oven, Part_Sign, SOCKET_* under Frame, FOOTPRINT).
  Renders go to <out>/bakery_<view>.png; views starting 'frame' hide every part. --tris prints tris per wall/piece.

Budget: under 45k tris (task cap), 2048 px maps (shared sets).
Status (2026-10-31, cloud): round 3, self-review 31.5/40 (silhouette 4.5, proportion 4, form 3.5, surface 3.5, colour 4,
wear 3.5, set 4, metal/emissive 4); first review 29 (target 34 not reached: round 3 gained about 0.5, so stopped per brief 3).
44,774 tris (Frame 40,226, Part_Door 1,558, Part_Oven 656, Part_Sign 2,334); FOOTPRINT 9.62 x 6.84 m; everything inside
x -5.43..5.33, y -4.42..4.99. Round 2: wear was being written before the parts' transforms updated (every part read full
grime), painted gilt, smooth sacks, flour heap; round 3: a true pretzel, back door hood, run-off and smoke stains.
Sheet: Reference/hollowmere/bakery/SHEET_bakery_r3.jpg. Next visible gains: a stronger oven bulge (it had to shrink to fit
the foundation; a deeper niche would let it bulge more), plaster value variation per panel, a few B9 weeds at the wall foot,
a warm lit interior through the open doorway in the frame state.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT
argv = FK.argv
VIEWS = FK.arg('--views', 'tq,tql,back,frame,close').split(',')
RRES = int(FK.arg('--rres', '1280')); SAMPLES = int(FK.arg('--samples', '32'))
TILE = FK.TILE
TIM, PROUD = FK.TIM, FK.PROUD

# ----------------------------------------------------------------------------- dimensions (metres)
HW, XC = 3.7, -0.85                # body half width and centre x (the oven side needs the room)
X0, X1 = XC - HW, XC + HW          # -4.55 .. 2.85
Y0, Y1 = -2.4, 4.2                 # front plaster plane .. back
JET = 0.40; YJ = Y0 - JET          # jettied upper front
PLINTH = FK.PLINTH                 # 0.6, the kit's plinth_run courses
GIRT0, GIRT1 = 2.80, 3.06
BRES0, BRES1 = 3.26, 3.54
PLATE0, PLATE1 = 5.50, 5.76
RIDGE, CLIP = 10.4, 8.95           # ridge and the half-hip clip line
TANP = (RIDGE - PLATE1) / HW       # about 51 deg
HIPRUN = (RIDGE - CLIP) / TANP
OV_E, OV_V = 0.75, 0.45            # side eave and gable verge overhangs
RIDGE_SAG = 0.18
LEAN = (0.010, 0.006)              # settled: x per m above the plinth, y (forward) per m above the bressumer
# oven annex on +X, chimney behind the niche
AX0, AX1, AY0, AY1, AH = X1, X1 + 1.6, -0.45, 3.0, 2.85
NICHE = (-0.15, 1.85, 0.78, 1.40)   # y0, y1, hearth top, arch spring (radius = half the width)
CH = (3.55, 4.83, 1.98, 3.12, 10.75)   # chimney x0, x1, y0, y1, top
DOOR = (1.25, 2.35, 0.18, 2.40)    # front ground storey, local u (from X0) and z
SHOP = (3.32, 6.48, 0.98, 2.36)
SIGN_U, SIGN_Z, SIGN_OUT = 0.62, 4.42, 1.30

EXTRA = {
    'brick': lambda: FK.tile_mat('BK_Brick', 'B2_cellar/old_brick', tint=(1.08, 0.96, 0.90), normal=1.2),
    'soot': lambda: FK.tile_mat('BK_Soot', 'B2_cellar/soot_coal', tint=(0.8, 0.75, 0.7)),
    'lime': lambda: FK.tile_mat('BK_Limewash', 'B1_town/plaster', tint=(1.04, 1.03, 1.0), normal=0.7),
    'bread': lambda: FK.tile_mat('BK_Bread', 'B1_town/plaster', tint=(1.0, 0.68, 0.36), normal=0.6),
    'crust': lambda: FK.tile_mat('BK_Crust', 'B1_town/plaster', tint=(0.62, 0.36, 0.17), normal=0.6),
    'flour': lambda: FK.tile_mat('BK_Flour', 'B1_town/plaster', tint=(1.35, 1.33, 1.28), normal=0.15),
    'wicker': lambda: FK.tile_mat('BK_Wicker', 'B1_town/oak_timber', tint=(1.55, 1.25, 0.85), normal=1.4),
    'sack': lambda: FK.tile_mat('BK_Sack', 'B1_town/canvas', tint=(1.0, 0.94, 0.82)),
    'cloth': lambda: FK.tile_mat('BK_Cloth', 'B1_town/canvas', tint=(1.05, 0.62, 0.55)),
    'gilt': lambda: FK.tile_mat('BK_Gilt', 'B1_town/plaster', tint=(1.15, 0.80, 0.30), normal=0.3),
    'interior': lambda: FK.flat_mat('BK_Interior', (0.035, 0.025, 0.018), 0.9),
    'ember': lambda: ember_mat(),
}
_orig_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)

_orig_member = FK.member
def lean_member(pc, mat, a, b, w, d, cham=0.025, name='m', sag=0.0, wob=0.006, extend=0.0, up=None):
    """FK.member with a cut every 1.5 m instead of 0.6 m (the 45k cap); short members get no cuts."""
    a = Vector(a); b = Vector(b)
    axis = (b - a); L = axis.length + 2 * extend; axis.normalize()
    up = Vector(up) if up else Vector((0, -1, 0))
    side = axis.cross(up).normalized(); up = side.cross(axis).normalized()
    R = FK.rng('mem' + name)
    rot = Matrix.Rotation(math.radians(R.uniform(-1.6, 1.6)), 4, up)
    xf = Matrix.Translation((a + b) / 2) @ rot @ Matrix((side, up, axis)).transposed().to_4x4()
    cuts = max(1, int(L / 1.8)) if sag else 0
    def bend2(p, L=L):
        t = p.z / L * 2
        return Vector((sag * (1 - t * t), 0, 0))
    FK.add_box(pc, mat, (w, d, L), xf, cham=cham, cuts=(0, 0, cuts), long_axis=2, bend=bend2 if sag else None,
               wob=wob, seed=R.random() * 100)
FK.member = lean_member

def plinth_run(pc, a, b, seed):
    """FK.plinth_run in one chamfer segment and longer stones (the 45k cap)."""
    R = FK.rng('bkplinth%f' % seed)
    for ci, (z0, z1) in enumerate([(0.0, 0.31), (0.31, PLINTH)]):
        x = a + (R.uniform(0.15, 0.45) if ci else 0.0)
        if ci:
            FK.add_box(pc, 'stone', (x - a - 0.035, 0.40, z1 - z0 - 0.035), Matrix.Translation(((a + x) / 2, 0.14, (z0 + z1) / 2)),
                       cham=0.04, uv_world=True, wob=0.01, seed=R.random() * 90)
        while x < b - 0.05:
            ln = min(R.uniform(0.75, 1.25), b - x)
            if b - (x + ln) < 0.35:
                ln = b - x
            g = 0.035; h = z1 - z0 - g; dpt = 0.40 + R.uniform(-0.02, 0.03)
            proud = R.uniform(0.0, 0.025)
            xf = Matrix.Translation((x + ln / 2, 0.14 - proud, z0 + g / 2 + h / 2)) @ Matrix.Rotation(math.radians(R.uniform(-1.2, 1.2)), 4, 'Y')
            FK.add_box(pc, 'stone', (ln - g, dpt, h), xf, cham=0.05, segs=1, uv_world=True, wob=0.012, seed=R.random() * 90)
            x += ln

def window(pc, plane, x0, x1, z0, z1, k, sill_out=0.10, came=0.26):
    """FK.window with a coarser leaded lattice (the 45k cap): casing, projecting sill, mullion, transom, deep-set glass."""
    yc = plane + 0.06; c = 0.09
    for nm, a, b in (('l', (x0 + c / 2, yc, z0), (x0 + c / 2, yc, z1)), ('r', (x1 - c / 2, yc, z0), (x1 - c / 2, yc, z1)),
                     ('t', (x0, yc, z1 - c / 2), (x1, yc, z1 - c / 2))):
        FK.member(pc, 'oak', a, b, c, 0.12, name='bwc%d%s' % (k, nm), cham=0.012, wob=0.003)
    FK.add_box(pc, 'oak', (x1 - x0 + 0.22, 0.20, 0.07), Matrix.Translation(((x0 + x1) / 2, plane - sill_out + 0.06, z0 + 0.01)) @
               Matrix.Rotation(math.radians(-5), 4, 'X'), cham=0.015, long_axis=0, seed=k + 0.5)
    xm = (x0 + x1) / 2; zt = z0 + (z1 - z0) * 0.68
    FK.member(pc, 'oak', (xm, yc + 0.02, z0), (xm, yc + 0.02, z1), 0.07, 0.09, name='bwm%d' % k, cham=0.01, wob=0.002)
    FK.member(pc, 'oak', (x0, yc + 0.02, zt), (x1, yc + 0.02, zt), 0.06, 0.09, name='bwt%d' % k, cham=0.01, wob=0.002)
    gy = plane + 0.24
    q = FK.quad_grid
    q(pc, 'glass', [(x0, gy, z0), (x1, gy, z0), (x1, gy, z1), (x0, gy, z1)], 1, 1, uv_fn=lambda c: (c.x, c.z))
    q(pc, 'oak', [(x0, plane, z0), (x0, gy, z0), (x0, gy, z1), (x0, plane, z1)], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'oak', [(x1, gy, z0), (x1, plane, z0), (x1, plane, z1), (x1, gy, z1)], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'oak', [(x1, plane, z1), (x0, plane, z1), (x0, gy, z1), (x1, gy, z1)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    q(pc, 'oak', [(x0, plane, z0), (x1, plane, z0), (x1, gy, z0), (x0, gy, z0)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    for i in range(-8, 9):
        for sgn in (1, -1):
            zm = (z0 + z1) / 2 + i * came
            pts = [(x, zm + sgn * (x - xm) * 1.6) for x in (x0, x1)] + [(xm + (z - zm) / (1.6 * sgn), z) for z in (z0, z1)]
            pts = sorted(p for p in pts if x0 - 1e-6 <= p[0] <= x1 + 1e-6 and z0 - 1e-6 <= p[1] <= z1 + 1e-6)
            if len(pts) < 2 or (Vector(pts[0]) - Vector(pts[-1])).length < 0.08:
                continue
            a, b = pts[0], pts[-1]
            ln = (Vector(b) - Vector(a)).length; ang = math.atan2(b[1] - a[1], b[0] - a[0])
            FK.add_box(pc, 'iron', (ln, 0.010, 0.014), Matrix.Translation(((a[0] + b[0]) / 2, gy - 0.006, (a[1] + b[1]) / 2)) @
                       Matrix.Rotation(-ang, 4, 'Y'), cham=0.0, long_axis=0, seed=i)

def tube(pc, mat, pts, r, sides=6, closed=False):
    """A round bar swept along pts (the pretzel, the wheat stems)."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rings = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        s = d.cross(Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))).normalized(); u = s.cross(d)
        rings.append([bm.verts.new(p + (s * math.cos(2 * math.pi * k / sides) + u * math.sin(2 * math.pi * k / sides)) * r) for k in range(sides)])
    acc = 0.0
    for i in range(len(rings) - 1):
        for k in range(sides):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
            for l in f.loops:
                l[uvl].uv = (k / sides * 2 * math.pi * r / TILE, i * 0.05 / TILE)
    for ring in (rings[0], rings[-1][::-1]):
        bm.faces.new(ring[::-1])
    bm.normal_update(); FK._merge(pc, bm, mat)

def ember_mat():
    if 'BK_Ember' in FK.MATS:
        return FK.MATS['BK_Ember']
    m = bpy.data.materials.new('BK_Ember'); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (0.35, 0.08, 0.01, 1)
    b.inputs['Emission Color'].default_value = (1.0, 0.36, 0.06, 1)
    b.inputs['Emission Strength'].default_value = 6.0
    FK.MATS['BK_Ember'] = m
    return m

# ----------------------------------------------------------------------------- piece plumbing
def piece(name):
    return FK.Piece(name)

def merge(dst, src, xf=None):
    """Append piece src into dst (placed by xf), remapping material slots."""
    if '--tris' in argv:
        print('    %-14s %6d tris' % (src.name, sum(len(f.verts) - 2 for f in src.bm.faces)))
    if xf is not None:
        bmesh.ops.transform(src.bm, matrix=xf, verts=src.bm.verts[:])
    remap = []
    for m in src.mats:
        if m not in dst.mats:
            dst.mats.append(m)
        remap.append(dst.mats.index(m))
    vmap = {v: dst.bm.verts.new(v.co) for v in src.bm.verts}
    for f in src.bm.faces:
        try:
            nf = dst.bm.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            continue
        nf.material_index = remap[f.material_index] if remap else 0
        for l, nl in zip(f.loops, nf.loops):
            nl[dst.uv].uv = l[src.uv].uv
    src.bm.free()

def face_xf(origin, n):
    """Wall frame: local x = along the wall (left to right seen from outside), local -y = outward n, z up."""
    n = Vector(n); t = Vector((0, 0, 1)).cross(n)
    return Matrix.Translation(origin) @ Matrix((t, -n, Vector((0, 0, 1)))).transposed().to_4x4()

F_FRONT = face_xf((X0, Y0, 0), (0, -1, 0))
F_FRONT_UP = face_xf((X0, YJ, 0), (0, -1, 0))
F_RIGHT = face_xf((X1, Y0, 0), (1, 0, 0))
F_RIGHT_UP = face_xf((X1, YJ, 0), (1, 0, 0))
F_BACK = face_xf((X1, Y1, 0), (0, 1, 0))
F_LEFT = face_xf((X0, Y1, 0), (-1, 0, 0))
L_FB, L_SIDE, L_SIDE_UP = X1 - X0, Y1 - Y0, Y1 - YJ

def mem(pc, a, b, w=TIM, d=TIM, proud=PROUD, name='m', **kw):
    """Oak member in wall-local coords (u, z pairs), standing `proud` in front of the plaster plane y=0."""
    yc = d / 2 - proud
    FK.member(pc, kw.pop('mat', 'oak'), (a[0], yc, a[1]), (b[0], yc, b[1]), w, d, name=name, **kw)

# ----------------------------------------------------------------------------- walls
def timber_wall(tag, L, za, zb, posts, rails, opens=(), braces=(), plinth=False, flower=None):
    """One half-timbered wall in local coords. rails: (z centre, height); opens: (kind, u0, u1, z0, z1);
    kinds: win (kit window + shutters), win_plain (no shutters), door (opening only), shop (opening only)."""
    pc = piece(tag)
    holes = [(o[1], o[2], o[3], o[4]) for o in opens]
    FK.wall_panel(pc, 'plaster', 0.0, 0.0, L, za, zb, holes=holes, bulge=0.014, cell=0.55)
    doors = [o for o in opens if o[0] == 'door']
    for i, u in enumerate(posts):
        z0 = za
        for o in doors:
            if abs(u - o[1]) < 0.3 or abs(u - o[2]) < 0.3:
                z0 = o[3]
        mem(pc, (u, z0), (u, zb), name='%sp%d' % (tag, i))
    for j, (zc, h) in enumerate(rails):
        cuts = [(o[1] - TIM / 2, o[2] + TIM / 2) for o in doors if o[3] < zc < o[4]]
        segs, a = [], 0.0
        for c0, c1 in sorted(cuts):
            segs.append((a, c0)); a = c1
        segs.append((a, L))
        for k, (s0, s1) in enumerate(segs):
            if s1 - s0 > 0.1:
                mem(pc, (s0, zc), (s1, zc), d=TIM, w=h, proud=PROUD + 0.02, name='%sr%d%d' % (tag, j, k),
                    sag=0.012 if s1 - s0 > 3 else 0.0)
    for k, (ua, za_, ub, zb_) in enumerate(braces):
        mem(pc, (ua, za_), (ub, zb_), w=0.22, proud=PROUD - 0.01, name='%sb%d' % (tag, k))
    wins = []
    for k, o in enumerate(opens):
        kind, u0, u1, z0, z1 = o
        if kind.startswith('win'):
            window(pc, 0.0, u0, u1, z0, z1, hash(tag) % 97 + k)
            # sill and head rails across the bay
            mem(pc, (u0 - 0.2, z0 - 0.11), (u1 + 0.2, z0 - 0.11), w=0.20, proud=PROUD + 0.01, name='%sws%d' % (tag, k))
            mem(pc, (u0 - 0.2, z1 + 0.11), (u1 + 0.2, z1 + 0.11), w=0.20, proud=PROUD + 0.01, name='%swh%d' % (tag, k))
            if kind == 'win':
                wins.append((u0, u1, z0, z1, 0.0))
    if wins:
        merge(pc, FK.kit_shutters(wins))
    if flower:
        a, b, zt = flower
        merge(pc, FK.kit_flowerbox(a, b, zt, 0.0))
    if plinth:
        segs, a = [], 0.0
        for o in doors:
            segs.append((a, o[1] - 0.02)); a = o[2] + 0.02
        segs.append((a, L))
        for s0, s1 in segs:
            plinth_run(pc, s0, s1, hash(tag) % 31 + s0)
    return pc

def doorway(pc, u0, u1, z0, z1, depth=0.30, inner=1.3):
    """Open doorway: oak reveals, a stone threshold and a dark lined room behind, so it never shows sky or a hole."""
    q = FK.quad_grid
    q(pc, 'oak', [(u0, 0, z0), (u0, depth, z0), (u0, depth, z1), (u0, 0, z1)], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'oak', [(u1, depth, z0), (u1, 0, z0), (u1, 0, z1), (u1, depth, z1)], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'oak', [(u1, 0, z1), (u0, 0, z1), (u0, depth, z1), (u1, depth, z1)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    FK.add_box(pc, 'stone', (u1 - u0 + 0.16, depth + 0.06, z0), Matrix.Translation(((u0 + u1) / 2, depth / 2 - 0.03, z0 / 2)),
               cham=0.02, uv_world=True, wob=0.004, seed=u0)
    # the room: floorboards, side walls, back wall and ceiling in a dim interior colour
    a, b = u0 - 0.5, u1 + 0.5; yb = depth + inner; zt = z1 + 0.4
    q(pc, 'oak', [(u0, depth, z0), (u1, depth, z0), (u1, yb, z0), (u0, yb, z0)], 2, 3, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    q(pc, 'interior', [(a, yb, z0), (b, yb, z0), (b, yb, zt), (a, yb, zt)], 1, 1, uv_fn=lambda c: (c.x, c.z))
    q(pc, 'interior', [(u0, depth, z0), (a, yb, z0), (a, yb, zt), (u0, depth, z1)], 1, 1, uv_fn=lambda c: (c.y, c.z))
    q(pc, 'interior', [(b, yb, z0), (u1, depth, z0), (u1, depth, z1), (b, yb, zt)], 1, 1, uv_fn=lambda c: (c.y, c.z))
    q(pc, 'interior', [(u0, depth, z1), (a, yb, zt), (b, yb, zt), (u1, depth, z1)], 1, 1, uv_fn=lambda c: (c.x, c.y))

def shop_window(pc):
    """The wide shop window: deep oak reveals, small-paned glazing on two mullions, the top shutter propped out as an
    awning on iron stays, the bottom shutter folded down as the bread shelf on knee brackets and chains."""
    u0, u1, z0, z1 = SHOP
    R = FK.rng('shop')
    q = FK.quad_grid
    gy = 0.20
    q(pc, 'glass', [(u0, gy, z0), (u1, gy, z0), (u1, gy, z1), (u0, gy, z1)], 1, 1, uv_fn=lambda c: (c.x, c.z))
    q(pc, 'oak', [(u0, 0, z0), (u0, gy, z0), (u0, gy, z1), (u0, 0, z1)], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'oak', [(u1, gy, z0), (u1, 0, z0), (u1, 0, z1), (u1, gy, z1)], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'oak', [(u1, 0, z1), (u0, 0, z1), (u0, gy, z1), (u1, gy, z1)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    q(pc, 'oak', [(u0, 0, z0), (u1, 0, z0), (u1, gy, z0), (u0, gy, z0)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    # casing, two mullions, glazing bars
    for u in (u0 + 0.05, u1 - 0.05):
        mem(pc, (u, z0), (u, z1), w=0.10, d=0.12, proud=0.06, name='shc%f' % u, cham=0.012, wob=0.003)
    mem(pc, (u0, z1 - 0.05), (u1, z1 - 0.05), w=0.10, d=0.12, proud=0.06, name='shct', cham=0.012)
    third = (u1 - u0) / 3
    for k in (1, 2):
        mem(pc, (u0 + third * k, z0), (u0 + third * k, z1), w=0.11, d=0.14, proud=-0.02, name='shm%d' % k, cham=0.012)
    for k in range(3):
        a = u0 + third * k
        for j in (1, 2, 3):
            uu = a + third * j / 4
            FK.add_box(pc, 'oak', (0.025, 0.03, z1 - z0), Matrix.Translation((uu, gy - 0.02, (z0 + z1) / 2)), cham=0.0, seed=uu)
        for j in (1, 2):
            zz = z0 + (z1 - z0) * j / 3
            FK.add_box(pc, 'oak', (third, 0.03, 0.025), Matrix.Translation((a + third / 2, gy - 0.02, zz)), cham=0.0,
                       long_axis=0, seed=zz + k)
    # awning shutter: hinged at the head, swung out 62 deg, five painted planks with battens
    W = u1 - u0 + 0.16; H = 0.82
    hinge = Matrix.Translation(((u0 + u1) / 2, -0.08, z1 + 0.10)) @ Matrix.Rotation(math.radians(-62), 4, 'X')
    for i in range(6):
        pw = W / 6
        FK.add_box(pc, 'door', (pw - 0.01, 0.045, H - R.uniform(0, 0.02)),
                   hinge @ Matrix.Translation((-W / 2 + pw * (i + 0.5), 0, -H / 2)), cham=0.008, long_axis=2,
                   wob=0.003, seed=R.random() * 40)
    for zz in (-0.14, -H + 0.14):
        FK.add_box(pc, 'door', (W - 0.1, 0.035, 0.09), hinge @ Matrix.Translation((0, 0.04, zz)), cham=0.01, long_axis=0,
                   seed=zz)
    for uu in ((u0 + 0.25), (u1 - 0.25)):   # iron stays from the wall to the shutter's lip
        tip = hinge @ Vector((uu - (u0 + u1) / 2, 0.03, -H + 0.08))
        base = Vector((uu, -0.05, z1 - 0.55))
        d = tip - base; L = d.length
        rot = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        FK.add_box(pc, 'iron', (0.025, 0.025, L), Matrix.Translation((base + tip) / 2) @ rot, cham=0.0, seed=uu)
    # bread shelf: the lower shutter folded flat, on two knee brackets, chains up to the casing
    sh_d = 0.52; zs = z0 - 0.08
    FK.add_box(pc, 'oak', (W, sh_d, 0.06), Matrix.Translation(((u0 + u1) / 2, -sh_d / 2 - 0.02, zs)), cham=0.012,
               long_axis=0, wob=0.003, seed=3.3)
    FK.add_box(pc, 'door', (W, 0.03, 0.10), Matrix.Translation(((u0 + u1) / 2, -sh_d - 0.03, zs - 0.01)), cham=0.008,
               long_axis=0, seed=3.4)
    for uu in (u0 + 0.2, u1 - 0.2, (u0 + u1) / 2):
        a = Vector((uu, -0.03, zs - 0.48)); b = Vector((uu, -sh_d + 0.06, zs - 0.03))
        FK.member(pc, 'oak', a, b, 0.08, 0.08, name='knee%f' % uu, cham=0.012, up=(1, 0, 0))
    for uu in (u0 + 0.12, u1 - 0.12):
        a = Vector((uu, -sh_d + 0.05, zs + 0.03)); b = Vector((uu, -0.02, z1 - 0.25))
        n = 9
        for k in range(n):
            p = a.lerp(b, (k + 0.5) / n) + Vector((0, 0, -0.04 * math.sin(math.pi * (k + 0.5) / n)))
            FK.add_box(pc, 'iron', (0.012, 0.035, 0.05), Matrix.Translation(p) @
                       Matrix.Rotation(math.atan2(b.y - a.y, b.z - a.z), 4, 'X') @ Matrix.Rotation((k % 2) * 1.57, 4, 'Z'),
                       cham=0.0, seed=k + uu)
    # on the shelf: a cloth, two baskets of loaves, a row of loose loaves
    cl0, cl1 = u0 + 0.35, u1 - 0.35
    def drape(p, u, v):
        hang = max(0.0, (v - 0.82) / 0.18)
        y = -0.06 - v * (sh_d - 0.02) if hang == 0 else -sh_d - 0.025
        z = zs + 0.035 + 0.008 * noise.noise(Vector((p.x * 3, v * 4, 0.3))) - hang * 0.16 * (0.8 + 0.2 * math.sin(u * 19))
        return Vector((p.x, y, z))
    q(pc, 'cloth', [(cl0, 0, 0), (cl1, 0, 0), (cl1, 0, 0), (cl0, 0, 0)], 14, 7, push=drape, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    for bi, uc in enumerate((u0 + 0.62, u1 - 0.70)):
        basket(pc, Vector((uc, -0.28, zs + 0.04)), 0.21, 0.15, 'bk%d' % bi)
        for li in range(3):
            loaf(pc, Vector((uc + R.uniform(-0.09, 0.09), -0.28 + R.uniform(-0.07, 0.07), zs + 0.17 + 0.03 * li)),
                 R.uniform(0, 3.14), R.choice(('boule', 'baton')), R.random() * 50, tilt=R.uniform(-0.6, 0.6))
    for k in range(5):
        uc = (u0 + u1) / 2 - 0.50 + k * 0.25
        loaf(pc, Vector((uc, -0.24 + R.uniform(-0.05, 0.05), zs + 0.08)), math.pi / 2 + R.uniform(-0.3, 0.3),
             'baton' if k % 3 else 'boule', R.random() * 50)

def loaf(pc, c, yaw, kind, seed, tilt=0.0):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=1.0)
    sx, sy, sz = (0.17, 0.075, 0.065) if kind == 'baton' else (0.10, 0.10, 0.07)
    R = FK.rng('loaf%f' % seed)
    for v in bm.verts:
        p = v.co
        if p.z < -0.2:
            p.z = -0.2 - (p.z + 0.2) * 0.25                      # flat bottom
        bump = 0.06 * noise.noise(p * 2.0 + Vector((seed, 0, 0)))
        if kind == 'baton' and p.z > 0.3:                         # three score ridges along the top
            bump -= 0.10 * max(0.0, math.cos(p.x * 7.5)) * (p.z - 0.3)
        p.x, p.y, p.z = p.x * sx * (1 + bump), p.y * sy * (1 + bump), (p.z + 0.2) * sz * (1 + bump)
    bm.transform(Matrix.Translation(c) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(tilt * 0.4, 4, 'X'))
    bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (R.random(), R.random()))
    FK._merge(pc, bm, 'bread' if R.random() < 0.6 else 'crust')

def basket(pc, c, r, h, name):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=False, segments=12, radius1=r * 0.82, radius2=r, depth=h)
    bm.transform(Matrix.Translation(c + Vector((0, 0, h / 2))))
    bm.normal_update(); FK.two_sided(bm, 0.012)
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0.3, 0.1))
    FK._merge(pc, bm, 'wicker')
    bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, segments=12, radius=r * 0.8)
    bm.transform(Matrix.Translation(c + Vector((0, 0, 0.02)))); uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'wicker')
    tube(pc, 'wicker', [c + Vector((math.cos(a0) * r, math.sin(a0) * r, h)) for a0 in
                        [k / 12 * 2 * math.pi for k in range(13)]], 0.02, 5)   # the rolled rim

def sack(pc, c, s, yaw, seed):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
    for v in bm.verts:
        p = v.co
        sl = 1.0 + 0.25 * max(0.0, -p.z)                          # slumped: fat at the foot
        tie = 1.0 - 0.55 * max(0.0, p.z - 0.55) / 0.45            # gathered neck
        n = 0.08 * noise.noise(p * 2.3 + Vector((seed, 0, 0)))
        v.co = Vector((p.x * 0.30 * sl * tie * (1 + n), p.y * 0.24 * sl * tie * (1 + n), (max(p.z, -0.8) + 0.8) * 0.42 * (1 + n)))
    bm.transform(Matrix.Translation(c) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Scale(s, 4))
    bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (seed % 1, 0.2)); FK._merge(pc, bm, 'sack')

def gable(tag, plane_pc_name, loft=False, vent=False):
    """Clipped gable wall in local coords (u 0..L_FB), from the wall plate to the half-hip clip line."""
    pc = piece(tag)
    L = L_FB
    def top(u):
        return min(CLIP, PLATE1 + (HW - abs(u - HW)) * TANP) - 0.04
    nu = 14
    holes = []
    lo = (HW - 0.5, HW + 0.5, 6.05, 7.45)
    def push(p, u, v):
        uu = 0.02 + (L - 0.04) * u
        return Vector((uu, -0.012 * math.sin(math.pi * u) * math.sin(math.pi * v), PLATE1 + (max(top(uu), PLATE1 + 0.03) - PLATE1) * v))
    if loft:   # leave the loft door opening: four strips around it
        FK.wall_panel(pc, 'plaster', 0.0, lo[0] - 0.9, lo[1] + 0.9, PLATE1, lo[3], holes=[lo], bulge=0.01, cell=0.4)
        for (a, b) in ((0.02, lo[0] - 0.9), (lo[1] + 0.9, L - 0.02)):
            def pushs(p, u, v, a=a, b=b):
                uu = a + (b - a) * u
                return Vector((uu, 0.0, PLATE1 + (max(top(uu), PLATE1 + 0.03) - PLATE1) * v))
            FK.quad_grid(pc, 'plaster', [(0, 0, 0)] * 4, 5, 4, push=pushs, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
        def pusht(p, u, v):
            uu = lo[0] - 0.9 + (lo[1] - lo[0] + 1.8) * u
            return Vector((uu, 0.0, lo[3] + (top(uu) - lo[3]) * v))
        FK.quad_grid(pc, 'plaster', [(0, 0, 0)] * 4, 6, 3, push=pusht, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    else:
        FK.quad_grid(pc, 'plaster', [(0, 0, 0)] * 4, nu, 5, push=push, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    # frame: wall plate across, king post, collar, raking braces
    mem(pc, (0, PLATE1 + 0.12), (L, PLATE1 + 0.12), w=0.24, proud=PROUD + 0.02, name=tag + 'tie')
    if loft:
        for u in (lo[0] - 0.14, lo[1] + 0.14):
            mem(pc, (u, PLATE1 + 0.2), (u, lo[3] + 0.3), name=tag + 'lp%f' % u)
        mem(pc, (lo[0] - 0.4, lo[3] + 0.16), (lo[1] + 0.4, lo[3] + 0.16), w=0.26, proud=PROUD + 0.02, name=tag + 'lh')
        mem(pc, (HW, lo[3] + 0.3), (HW, CLIP - 0.05), name=tag + 'king')
        loft_door(pc, lo)
    else:
        mem(pc, (HW, PLATE1 + 0.2), (HW, CLIP - 0.05), name=tag + 'king')
    zc = 7.75
    hw_c = (RIDGE - zc) / TANP
    mem(pc, (HW - hw_c + 0.1, zc), (HW + hw_c - 0.1, zc), w=0.24, proud=PROUD + 0.02, name=tag + 'collar')
    for s in (-1, 1):
        a = (HW + s * 2.2, PLATE1 + 0.25); b = (HW + s * 0.25, zc - 0.1)
        if loft:
            a = (HW + s * 2.6, PLATE1 + 0.25); b = (HW + s * 1.1, zc - 0.15)
        mem(pc, a, b, w=0.22, proud=PROUD - 0.01, name=tag + 'rb%d' % s)
    if vent:
        window(pc, 0.0, HW - 0.32, HW + 0.32, 6.4, 7.1, 77)
    return pc

def loft_door(pc, lo):
    """Flour loft door (closed, planked green) with a hoist beam, pulley block, rope and hook above it."""
    u0, u1, z0, z1 = lo
    R = FK.rng('loft')
    FK.quad_grid(pc, 'interior', [(u0, 0.12, z0), (u1, 0.12, z0), (u1, 0.12, z1), (u0, 0.12, z1)], 1, 1, uv_fn=lambda c: (c.x, c.z))
    for i in range(4):
        pw = (u1 - u0) / 4
        FK.add_box(pc, 'door', (pw - 0.01, 0.05, z1 - z0 - 0.02), Matrix.Translation((u0 + pw * (i + 0.5), 0.05, (z0 + z1) / 2)) @
                   Matrix.Rotation(math.radians(R.uniform(-0.8, 0.8)), 4, 'Y'), cham=0.01, long_axis=2, wob=0.003, seed=R.random() * 30)
    for zz in (z0 + 0.25, z1 - 0.25):
        FK.add_box(pc, 'iron', (0.5, 0.012, 0.05), Matrix.Translation((u0 + 0.25, 0.018, zz)), cham=0.0, long_axis=0, seed=zz)
    FK.add_box(pc, 'oak', (u1 - u0 + 0.3, 0.16, 0.08), Matrix.Translation(((u0 + u1) / 2, -0.04, z0 - 0.04)), cham=0.015,
               long_axis=0, seed=5.5)
    # hoist beam out of the gable over the door
    zb = 8.25
    FK.add_box(pc, 'oak', (0.24, 1.25, 0.26), Matrix.Translation((HW, -0.55, zb)), cham=0.03, cuts=(0, 3, 0), long_axis=1,
               wob=0.006, seed=6.1)
    FK.member(pc, 'oak', (HW, -0.02, zb - 0.75), (HW, -0.62, zb - 0.1), 0.14, 0.14, name='hoistbrace', up=(1, 0, 0))
    FK.add_box(pc, 'oak', (0.12, 0.18, 0.24), Matrix.Translation((HW, -1.02, zb - 0.27)), cham=0.02, seed=6.2)
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.09, radius2=0.09, depth=0.06)
    bm.transform(Matrix.Translation((HW, -1.02, zb - 0.30)) @ Matrix.Rotation(math.pi / 2, 4, 'Y'))
    uvl = bm.loops.layers.uv.new('UVMap'); FK._merge(pc, bm, 'iron')
    for ry, zend in ((-0.93, 6.55), (-1.11, 7.35)):   # rope: two falls, one ending in a hook
        FK.add_box(pc, 'sack', (0.028, 0.028, zb - 0.3 - zend), Matrix.Translation((HW, ry, (zb - 0.3 + zend) / 2)), cham=0.0, seed=ry)
    for k in range(7):
        a0 = k / 6 * math.pi * 1.2
        FK.add_box(pc, 'iron', (0.025, 0.025, 0.05), Matrix.Translation((HW, -0.93 + math.sin(a0) * 0.06, 6.5 - (1 - math.cos(a0)) * 0.06)) @
                   Matrix.Rotation(a0, 4, 'X'), cham=0.0, seed=k)

# ----------------------------------------------------------------------------- the house body
def front_walls():
    pcs = []
    du0, du1, dz0, dz1 = DOOR
    su0, su1, sz0, sz1 = SHOP
    posts = [TIM / 2, du0 - TIM / 2, du1 + TIM / 2, (du1 + su0) / 2 + 0.1, su0 - TIM / 2, su1 + TIM / 2, L_FB - TIM / 2]
    g = timber_wall('FrontG', L_FB, PLINTH, BRES0, posts, [(PLINTH + 0.12, 0.24), ((GIRT0 + GIRT1) / 2, GIRT1 - GIRT0)],
                    opens=[('door', du0, du1, dz0, dz1), ('shop', su0, su1, sz0, sz1)],
                    braces=[(TIM, PLINTH + 0.24, du0 - TIM, GIRT0 - 0.05)], plinth=True)
    doorway(g, du0, du1, dz0, dz1)
    shop_window(g)
    # worn sandstone step, dusted with flour; flour sacks slumped beside the door
    FK.add_box(g, 'stone', (du1 - du0 + 0.55, 0.50, 0.18), Matrix.Translation(((du0 + du1) / 2, -0.25, 0.09)) @
               Matrix.Rotation(math.radians(1.2), 4, 'Y'), cham=0.035, segs=2, cuts=(3, 1, 0), uv_world=True, wob=0.01, seed=8.0)
    R = FK.rng('flour')
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.30 * (1 + 0.3 * noise.noise(v.co * 3)), v.co.y * 0.22 * (1 + 0.3 * noise.noise(v.co * 3 + Vector((4, 0, 0)))), max(0.0, v.co.z) * (0.06 + 0.05 * max(0.0, noise.noise(v.co * 5)))))
    bm.transform(Matrix.Translation((2.55, -0.62, 0.0)) @ Matrix.Rotation(0.4, 4, 'Z'))
    bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(g, bm, 'flour')
    for k, (uu, yy, s) in enumerate(((2.80, -0.32, 1.0), (3.08, -0.36, 0.9))):
        sack(g, Vector((uu, yy, 0.0)), s, R.uniform(-0.5, 0.5), k + 0.3)
    pcs.append((g, F_FRONT))
    # jetty: joist ends, bressumer, soffit
    j = piece('Jetty')
    for k in range(15):
        u = 0.2 + k * (L_FB - 0.4) / 14
        FK.add_box(j, 'oak', (0.16, JET + 0.12, 0.20), Matrix.Translation((u, -JET / 2 - 0.02, (GIRT1 + BRES0) / 2)),
                   cham=0.02, long_axis=1, wob=0.004, seed=k)
    FK.quad_grid(j, 'oak', [(0, 0.05, GIRT1 + 0.01), (L_FB, 0.05, GIRT1 + 0.01), (L_FB, -JET, GIRT1 + 0.01), (0, -JET, GIRT1 + 0.01)],
                 6, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    pcs.append((j, F_FRONT))
    uw = [(1.10, 2.15), (5.25, 6.30)]
    uz = (3.98, 5.08)
    up_posts = [TIM / 2, uw[0][0] - TIM / 2, uw[0][1] + TIM / 2, HW, uw[1][0] - TIM / 2, uw[1][1] + TIM / 2, L_FB - TIM / 2]
    u = timber_wall('FrontU', L_FB, BRES0, PLATE1, up_posts, [((BRES0 + BRES1) / 2, BRES1 - BRES0 + 0.04), ((PLATE0 + PLATE1) / 2, 0.26)],
                    opens=[('win', uw[0][0], uw[0][1], uz[0], uz[1]), ('win', uw[1][0], uw[1][1], uz[0], uz[1])],
                    braces=[(TIM, BRES1, uw[0][0] - TIM, PLATE0), (HW - 0.14, BRES1, uw[0][1] + TIM, PLATE0),
                            (HW + 0.14, BRES1, uw[1][0] - TIM, PLATE0), (L_FB - TIM, BRES1, uw[1][1] + TIM, PLATE0)],
                    flower=(uw[1][0], uw[1][1], uz[0] - 0.02))
    sign_bracket(u)
    pcs.append((u, F_FRONT_UP))
    pcs.append((gable('FrontGable', None, loft=True), F_FRONT_UP))
    return pcs

def sign_bracket(pc):
    """Wrought-iron arm out of the jetty: a square bar with a scroll in the brace triangle and two open hooks."""
    u = SIGN_U; z = SIGN_Z
    FK.add_box(pc, 'iron', (0.05, 0.03, 0.40), Matrix.Translation((u, -PROUD - 0.015, z - 0.30)), cham=0.004, seed=1)   # wall plate
    FK.add_box(pc, 'iron', (0.045, SIGN_OUT, 0.045), Matrix.Translation((u, -SIGN_OUT / 2 - PROUD, z)), cham=0.006, seed=2)
    a = Vector((u, -PROUD, z - 0.55)); b = Vector((u, -PROUD - SIGN_OUT * 0.72, z - 0.02))
    d = b - a; FK.add_box(pc, 'iron', (0.035, 0.035, d.length), Matrix.Translation((a + b) / 2) @
                          d.to_track_quat('Z', 'Y').to_matrix().to_4x4(), cham=0.0, seed=3)
    # scroll: a tightening spiral of short bar segments
    c = Vector((u, -PROUD - 0.33, z - 0.18)); pts = []
    for k in range(26):
        t = k / 25 * 2.6 * math.pi
        r = 0.16 * (1 - t / (3.2 * math.pi))
        pts.append(c + Vector((0, -math.cos(t) * r, math.sin(t) * r)))
    for p, q in zip(pts, pts[1:]):
        d = q - p
        FK.add_box(pc, 'iron', (0.022, 0.022, d.length + 0.008), Matrix.Translation((p + q) / 2) @
                   d.to_track_quat('Z', 'Y').to_matrix().to_4x4(), cham=0.0, seed=p.z)
    # finial curl at the tip
    tip = Vector((u, -PROUD - SIGN_OUT, z))
    for k in range(7):
        a0 = k / 6 * math.pi * 1.4
        FK.add_box(pc, 'iron', (0.03, 0.03, 0.05), Matrix.Translation(tip + Vector((0, -math.sin(a0) * 0.06, math.cos(a0) * 0.06 - 0.06))) @
                   Matrix.Rotation(-a0, 4, 'X'), cham=0.0, seed=k + 9)
    for hy in SIGN_HOOKS:   # open S-hooks, waiting for the sign's chains
        for k in range(8):
            a0 = k / 7 * math.pi * 1.5
            FK.add_box(pc, 'iron', (0.012, 0.016, 0.03), Matrix.Translation((u, -PROUD - hy + math.sin(a0) * 0.025,
                       z - 0.05 - (1 - math.cos(a0)) * 0.025)) @ Matrix.Rotation(-a0, 4, 'X'), cham=0.0, seed=k + hy)

SIGN_HOOKS = (0.55, 1.10)

def left_walls():
    pcs = []
    gw = (2.20, 3.25, 1.30, 2.30)
    g = timber_wall('LeftG', L_SIDE, PLINTH, BRES0,
                    [TIM / 2, gw[0] - TIM / 2, gw[1] + TIM / 2, 4.55, L_SIDE - TIM / 2],
                    [(PLINTH + 0.12, 0.24), ((GIRT0 + GIRT1) / 2, GIRT1 - GIRT0)],
                    opens=[('win_plain', gw[0], gw[1], gw[2], gw[3])],
                    braces=[(TIM, PLINTH + 0.24, gw[0] - TIM, GIRT0), (L_SIDE - TIM, PLINTH + 0.24, 4.55 + 0.14, GIRT0)], plinth=True)
    woodpile(g, 3.75, 6.35)
    pcs.append((g, F_LEFT))
    uw = [(1.35, 2.35), (4.55, 5.55)]
    uz = (4.0, 5.05)
    u = timber_wall('LeftU', L_SIDE_UP, BRES0, PLATE1,
                    [TIM / 2, uw[0][0] - TIM / 2, uw[0][1] + TIM / 2, 3.45, uw[1][0] - TIM / 2, uw[1][1] + TIM / 2, L_SIDE_UP - TIM / 2],
                    [((BRES0 + BRES1) / 2, BRES1 - BRES0), ((PLATE0 + PLATE1) / 2, 0.26)],
                    opens=[('win', uw[0][0], uw[0][1], uz[0], uz[1]), ('win_plain', uw[1][0], uw[1][1], uz[0], uz[1])],
                    braces=[(3.31, BRES1, uw[0][1] + TIM, PLATE0), (3.59, BRES1, uw[1][0] - TIM, PLATE0)])
    pcs.append((u, face_xf((X0, Y1, 0), (-1, 0, 0))))
    return pcs

def woodpile(pc, u0, u1):
    """Split firewood for the oven stacked against the wall under a small pent roof on brackets."""
    R = FK.rng('wood')
    depth = 0.45; zt = 1.32
    z = 0.08
    FK.add_box(pc, 'stone', (u1 - u0 + 0.1, depth + 0.05, 0.08), Matrix.Translation(((u0 + u1) / 2, -depth / 2 - 0.08, 0.04)),
               cham=0.02, uv_world=True, seed=4)
    row = 0
    while z < zt:
        r = R.uniform(0.085, 0.11)
        u = u0 + (r if row % 2 else r * 2)
        while u < u1 - r:
            rr = r * R.uniform(0.85, 1.12)
            bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=5, radius1=rr, radius2=rr, depth=depth - R.uniform(0, 0.06))
            bm.transform(Matrix.Translation((u, -depth / 2 - 0.08 + R.uniform(-0.03, 0.03), z + rr)) @ Matrix.Rotation(math.pi / 2, 4, 'X')
                         @ Matrix.Rotation(R.uniform(0, 1), 4, 'Z'))
            bm.normal_update()
            uvl = bm.loops.layers.uv.new('UVMap')
            FK._uv_project(bm.faces, uvl, Matrix.Identity(4), 1, (R.random(), R.random()))
            # end grain faces (normal along y) pale, bark sides dark
            ends = [f for f in bm.faces if abs(f.normal.y) > 0.9]
            sides = [f for f in bm.faces if abs(f.normal.y) <= 0.9]
            bme = bm.copy()
            bmesh.ops.delete(bm, geom=ends, context='FACES')
            FK._merge(pc, bm, 'oak')
            bmesh.ops.delete(bme, geom=[f for f in bme.faces if abs(f.normal.y) <= 0.9], context='FACES')
            FK._merge(pc, bme, 'oak_lt')
            u += rr * 2.05
        z += r * 1.85; row += 1
    # pent roof: boards on two brackets
    for uu in (u0 + 0.1, u1 - 0.1):
        FK.member(pc, 'oak', (uu, -0.05, 1.70), (uu, -depth - 0.12, 2.06), 0.09, 0.09, name='pentb%f' % uu, up=(1, 0, 0))
    for k in range(4):
        y = -0.12 - k * 0.13
        FK.add_box(pc, 'oak_lt', (u1 - u0 + 0.35, 0.17, 0.03), Matrix.Translation(((u0 + u1) / 2, y, 2.25 - k * 0.075)) @
                   Matrix.Rotation(math.radians(26), 4, 'X'), cham=0.006, long_axis=0, wob=0.003, seed=k + 0.7)

def right_walls():
    pcs = []
    gw = (0.70, 1.60, 1.30, 2.25)
    g = timber_wall('RightG', L_SIDE, PLINTH, BRES0, [TIM / 2, gw[0] - TIM / 2, gw[1] + TIM / 2, 2.1, 5.25, L_SIDE - TIM / 2],
                    [(PLINTH + 0.12, 0.24), ((GIRT0 + GIRT1) / 2, GIRT1 - GIRT0)],
                    opens=[('win', gw[0], gw[1], gw[2], gw[3])], braces=[], plinth=True)
    pcs.append((g, F_RIGHT))
    uw = (1.40, 2.40); uz = (4.0, 5.05)
    u = timber_wall('RightU', L_SIDE_UP, BRES0, PLATE1,
                    [TIM / 2, uw[0] - TIM / 2, uw[1] + TIM / 2, 3.9, 5.6, L_SIDE_UP - TIM / 2],
                    [((BRES0 + BRES1) / 2, BRES1 - BRES0), ((PLATE0 + PLATE1) / 2, 0.26)],
                    opens=[('win_plain', uw[0], uw[1], uz[0], uz[1])],
                    braces=[(3.76, BRES1, uw[1] + TIM, PLATE0), (4.04, BRES1, 5.46, PLATE0), (L_SIDE_UP - TIM, BRES1, 5.74, PLATE0)])
    pcs.append((u, F_RIGHT_UP))
    return pcs

def back_walls():
    pcs = []
    bd = (1.20, 2.20, 0.18, 2.30); bw = (4.30, 5.30, 1.30, 2.30)
    g = timber_wall('BackG', L_FB, PLINTH, BRES0, [TIM / 2, bd[0] - TIM / 2, bd[1] + TIM / 2, bw[0] - TIM / 2, bw[1] + TIM / 2, L_FB - TIM / 2],
                    [(PLINTH + 0.12, 0.24), ((GIRT0 + GIRT1) / 2, GIRT1 - GIRT0)],
                    opens=[('door', bd[0], bd[1], bd[2], bd[3]), ('win_plain', bw[0], bw[1], bw[2], bw[3])],
                    braces=[(bd[1] + TIM, PLINTH + 0.24, bw[0] - TIM, GIRT0), (L_FB - TIM, PLINTH + 0.24, bw[1] + TIM, GIRT0)], plinth=True)
    # plain ledged oak back door (closed), stone step
    R = FK.rng('backdoor')
    for i in range(5):
        pw = (bd[1] - bd[0]) / 5
        FK.add_box(g, 'oak_lt', (pw - 0.008, 0.05, bd[3] - bd[2]), Matrix.Translation((bd[0] + pw * (i + 0.5), 0.10, (bd[2] + bd[3]) / 2)),
                   cham=0.01, long_axis=2, wob=0.003, seed=R.random() * 30)
    for zz in (bd[2] + 0.4, bd[3] - 0.4):
        FK.add_box(g, 'iron', (0.6, 0.012, 0.05), Matrix.Translation((bd[0] + 0.3, 0.07, zz)), cham=0.0, long_axis=0, seed=zz)
    FK.add_box(g, 'stone', (bd[1] - bd[0] + 0.3, 0.36, 0.18), Matrix.Translation(((bd[0] + bd[1]) / 2, -0.16, 0.09)), cham=0.03,
               uv_world=True, wob=0.008, seed=9)
    FK.quad_grid(g, 'oak', [(bd[0], 0, bd[3]), (bd[1], 0, bd[3]), (bd[1], 0.12, bd[3]), (bd[0], 0.12, bd[3])], 1, 1,
                 uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    for uu, s in ((bd[0], 1), (bd[1], -1)):
        FK.quad_grid(g, 'oak', [(uu, 0, bd[2]), (uu, 0.12, bd[2]), (uu, 0.12, bd[3]), (uu, 0, bd[3])][::s], 1, 1,
                     uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    hc = (bd[0] + bd[1]) / 2; hw = (bd[1] - bd[0]) / 2 + 0.35; hz = bd[3] + 0.32
    for sgn in (-1, 1):
        a = Vector((hc, -0.02, hz + 0.42)); b = Vector((hc + sgn * hw, -0.02, hz))
        FK.quad_grid(g, 'roof', [a + Vector((0, 0.0, 0)), b, b + Vector((0, -0.75, 0)), a + Vector((0, -0.75, 0))][::sgn], 3, 2,
                     uv_fn=lambda c: (c.x / TILE, c.y / TILE))
        FK.quad_grid(g, 'oak', [a - Vector((0, 0, 0.06)), b - Vector((0, 0, 0.06)), b + Vector((0, -0.75, -0.06)), a + Vector((0, -0.75, -0.06))][::-sgn],
                     1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
        FK.member(g, 'oak', (hc + sgn * (hw - 0.2), -0.03, hz - 0.55), (hc + sgn * (hw - 0.2), -0.62, hz - 0.03), 0.08, 0.08,
                  name='hood%d' % sgn, up=(1, 0, 0))
    FK.member(g, 'oak', (hc, -0.79, hz + 0.40), (hc, -0.02, hz + 0.44), 0.10, 0.10, name='hoodridge', up=(1, 0, 0))
    # a water butt by the back door
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=0.30, radius2=0.33, depth=0.85)
    bm.transform(Matrix.Translation((bd[1] + 0.55, -0.42, 0.425))); uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), 2, (0.2, 0.4)); FK._merge(g, bm, 'oak_lt')
    for zz in (0.15, 0.7):
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=False, segments=14, radius1=0.30 + zz * 0.035 + 0.012,
                                                radius2=0.30 + zz * 0.035 + 0.012, depth=0.05)
        bm.transform(Matrix.Translation((bd[1] + 0.55, -0.42, zz))); uvl = bm.loops.layers.uv.new('UVMap')
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(g, bm, 'iron')
    pcs.append((g, F_BACK))
    uw = [(1.10, 2.15), (5.25, 6.30)]; uz = (4.0, 5.05)
    u = timber_wall('BackU', L_FB, BRES0, PLATE1,
                    [TIM / 2, uw[0][0] - TIM / 2, uw[0][1] + TIM / 2, HW, uw[1][0] - TIM / 2, uw[1][1] + TIM / 2, L_FB - TIM / 2],
                    [((BRES0 + BRES1) / 2, BRES1 - BRES0), ((PLATE0 + PLATE1) / 2, 0.26)],
                    opens=[('win_plain', uw[0][0], uw[0][1], uz[0], uz[1]), ('win_plain', uw[1][0], uw[1][1], uz[0], uz[1])],
                    braces=[(TIM, BRES1, uw[0][0] - TIM, PLATE0), (L_FB - TIM, BRES1, uw[1][1] + TIM, PLATE0)])
    pcs.append((u, F_BACK))
    pcs.append((gable('BackGable', None, vent=True), F_BACK))
    return pcs

# ----------------------------------------------------------------------------- roof
def slope_pt(side, y, t):
    """Top of a side slope. side +1 (+X) or -1; t = 0 at the eave edge, 1 at the ridge."""
    xe = XC + side * (HW + OV_E)
    ze = PLATE1 + 0.24 - OV_E * TANP
    sag = RIDGE_SAG * (1 - ((y - (Y1 + YJ) / 2) / ((Y1 - YJ) / 2 + OV_V)) ** 2)
    wave = 0.025 * noise.noise(Vector((y * 0.6, t * 2.5, side * 1.7)))
    kick = 0.30 * max(0.0, 1 - t / 0.2) ** 2
    zr = RIDGE + 0.24 - sag
    return Vector((xe + (XC - xe) * t, y, ze + (zr - ze) * t + wave * math.sin(math.pi * min(1.0, t)) + kick))

def slope_range(t):
    """Front and back y of a side slope at height t (the half-hips cut its top corners)."""
    yf, yb = YJ - OV_V, Y1 + OV_V
    ze = PLATE1 - OV_E * TANP
    z = ze + (RIDGE - ze) * t
    if z > CLIP:
        run = (z - CLIP) / TANP
        return yf + run, yb - run
    return yf, yb

def roof():
    pc = piece('Roof')
    TH = 0.20
    for side in (1, -1):
        n = Vector((side * TANP, 0, 1)).normalized()
        ny, nt = 30, 12
        def deck(p, u, v, side=side, off=0.06):
            yf, yb = slope_range(v)
            y = yf + (yb - yf) * (u if side > 0 else 1 - u)
            return slope_pt(side, y, v) - n * off
        FK.quad_grid(pc, 'roof', [(0, 0, 0)] * 4, ny, nt, push=deck, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
        def under(p, u, v, side=side):
            yf, yb = slope_range(v)
            y = yf + (yb - yf) * (1 - u if side > 0 else u)
            return slope_pt(side, y, v) - n * TH
        FK.quad_grid(pc, 'oak', [(0, 0, 0)] * 4, ny, 6, push=under, uv_fn=lambda c: (c.y / TILE, c.x / TILE))
        # verge ends close the slab (front and back), and the eave edge
        for end in (0, 1):
            def vend(p, u, v, side=side, end=end):
                vv = v if (end == 0) == (side > 0) else 1 - v
                yf, yb = slope_range(vv)
                return slope_pt(side, yf if end == 0 else yb, vv) - n * (0.06 + (TH - 0.06) * u)
            FK.quad_grid(pc, 'oak', [(0, 0, 0)] * 4, 1, nt, push=vend, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
        tile_courses(pc, lambda t, side=side: tuple(slope_pt(side, y, t) for y in slope_range(t)), n, 15,
                     'side%d' % side, flip=side < 0)
        # rafter tails and fascia under the eave
        for k in range(6):
            y = YJ + 0.2 + k * (Y1 - YJ - 0.4) / 5
            a = slope_pt(side, y, 0.03) - n * (TH + 0.08); b = slope_pt(side, y, 0.17) - n * (TH + 0.08)
            FK.member(pc, 'oak', a, b, 0.13, 0.17, name='raft%d%d' % (side, k), up=tuple(-n))
        yf, yb = slope_range(0)
        FK.member(pc, 'oak', slope_pt(side, yf, 0) - n * 0.13, slope_pt(side, yb, 0) - n * 0.13, 0.2, 0.07,
                  name='fascia%d' % side, up=(side, 0, 0.4))
        rolled_eave(pc, side, n)
        # barge boards along the clipped gable verges
        for end in (0, 1):
            ts = [i / 10 for i in range(11)]
            pts = []
            for t in ts:
                yf, yb = slope_range(t)
                z = PLATE1 - OV_E * TANP + (RIDGE - PLATE1 + OV_E * TANP) * t
                if z > CLIP + 0.05:
                    break
                pts.append(slope_pt(side, yf if end == 0 else yb, t) - n * 0.11 + Vector((0, -0.02 if end == 0 else 0.02, 0)))
            for k, (a, b) in enumerate(zip(pts, pts[1:])):
                FK.member(pc, 'oak', a, b, 0.30, 0.08, name='barge%d%d%d' % (side, end, k), up=(0, -1 if end == 0 else 1, 0), wob=0.003)
    # half-hips at front and back
    for end in (0, 1):
        sg = -1 if end == 0 else 1
        yb0 = (YJ - OV_V) if end == 0 else (Y1 + OV_V)
        apex_y = yb0 - sg * HIPRUN
        def hip_pt(s, t):
            """s -1..1 across, t 0 (clip eave) .. 1 (apex)."""
            hw = HIPRUN * (1 - t) + 0.04
            z = CLIP + 0.24 + (RIDGE - CLIP) * t - RIDGE_SAG * 0.0
            return Vector((XC + s * hw, yb0 - sg * HIPRUN * t, z))
        n = Vector((0, sg * TANP, 1)).normalized()
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        rows = []
        for j in range(5):
            t = j / 5
            rows.append([bm.verts.new(hip_pt(-1 + 2 * i / 4, t)) for i in range(5)])
        apex = bm.verts.new(hip_pt(0, 1.0))
        for j in range(4):
            for i in range(4):
                q = (rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i])
                bm.faces.new(q if end == 1 else q[::-1])
        for i in range(4):
            q = (rows[4][i], rows[4][i + 1], apex)
            bm.faces.new(q if end == 1 else q[::-1])
        for f in bm.faces:
            for l in f.loops:
                l[uvl].uv = (l.vert.co.x / TILE, l.vert.co.z / TILE)
        bm.normal_update()
        # underside
        und = bm.copy(); bmesh.ops.reverse_faces(und, faces=und.faces[:])
        bmesh.ops.translate(und, verts=und.verts[:], vec=-n * 0.16)
        FK._merge(pc, bm, 'roof'); FK._merge(pc, und, 'oak')
        def edge(t, end=end):
            return hip_pt(-1, t * 0.95), hip_pt(1, t * 0.95)
        tile_courses(pc, edge, n, 4, 'hip%d' % end, flip=end == 0, lift=0.07)
        # hip ridges and the clip eave fascia
        for s in (-1, 1):
            a = hip_pt(s, 0.0); b = hip_pt(0, 1.0)
            ridge_tiles(pc, a + Vector((0, 0, 0.06)), b + Vector((0, 0, 0.08)), 4, 'hr%d%d' % (end, s))
        FK.member(pc, 'oak', hip_pt(-1, 0) - n * 0.12, hip_pt(1, 0) - n * 0.12, 0.18, 0.07, name='clipf%d' % end, up=(0, sg, 0.3))
    # main ridge between the hip apexes
    ya, yb = YJ - OV_V + HIPRUN, Y1 + OV_V - HIPRUN
    ridge_tiles(pc, Vector((XC, ya, RIDGE + 0.30)), Vector((XC, yb, RIDGE + 0.30)), 10, 'ridge', sag=True)
    return pc

def tile_courses(pc, edge, n, nc, tag, flip=False, lift=0.0):
    """Courses of clay tile: tilted strips whose butts stand proud, jittered per run. edge(t) -> (a, b) at height t."""
    for c in range(nc):
        R = FK.rng('course%s%d' % (tag, c))
        t0, t1 = c / nc, min(1.0, (c + 1) / nc + 0.03)
        a0, b0 = edge(t0); a1, b1 = edge(t1)
        L = (b0 - a0).length
        nt = max(2, int(L / 0.36))
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        jit = 0.0
        for i in range(nt):
            u0, u1 = i / nt, (i + 1) / nt
            j = R.uniform(-0.012, 0.012) if c else R.uniform(-0.008, 0.04)
            pts = []
            for u in (u0 + 0.002, u1 - 0.002):
                pb = a0.lerp(b0, u) + n * (0.045 + j + lift)
                pt = a1.lerp(b1, u) + n * lift
                pts.append((bm.verts.new(pb - n * 0.05), bm.verts.new(pb), bm.verts.new(pt)))
            a, b = pts
            for q in ((a[1], b[1], b[2], a[2]), (a[0], b[0], b[1], a[1])):
                f = bm.faces.new(q[::-1] if flip else q)
                for l in f.loops:
                    co = l.vert.co
                    l[uvl].uv = ((co - a0).dot((b0 - a0).normalized()) / TILE, (c * 0.37 + (co - a0).dot((a1 - a0).normalized())) / TILE)
        bm.normal_update()
        FK._merge(pc, bm, 'roof')

def ridge_tiles(pc, a, b, n, tag, sag=False):
    ax = (b - a); L = ax.length; ax.normalize()
    side = ax.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize(); up = side.cross(ax).normalized()
    if up.z < 0:
        up = -up
    for i in range(n):
        R = FK.rng('rt%s%d' % (tag, i))
        c = a + ax * (L * (i + 0.5) / n)
        if sag:
            tt = (i + 0.5) / n * 2 - 1
            c = c - Vector((0, 0, RIDGE_SAG * (1 - tt * tt)))
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        segs = 8; r = 0.15; Lt = L / n + 0.05
        rows = []
        for e in (-1, 1):
            rr = r * (1.0 if e < 0 else 1.06)
            rows.append([bm.verts.new(c + ax * (e * Lt / 2) + side * (-rr * math.cos(math.pi * k / segs)) +
                                      up * (rr * math.sin(math.pi * k / segs) * 0.85 - 0.04)) for k in range(segs + 1)])
        for k in range(segs):
            f = bm.faces.new((rows[0][k], rows[1][k], rows[1][k + 1], rows[0][k + 1]))
            for l in f.loops:
                l[uvl].uv = ((l.vert.co - a).dot(ax) / TILE, k * r * 0.4 / TILE)
        bmesh.ops.rotate(bm, verts=bm.verts[:], cent=c, matrix=Matrix.Rotation(math.radians(R.uniform(-2.5, 2.5)), 3, ax))
        bm.normal_update()
        bm.faces.ensure_lookup_table()
        if bm.faces[0].normal.dot(up) < 0:
            bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        FK.two_sided(bm)
        FK._merge(pc, bm, 'roof')

def rolled_eave(pc, side, n):
    segs = 6; rr = 0.11; rows = []
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    yf, yb = slope_range(0)
    ny = 18
    for i in range(ny + 1):
        y = yf + (yb - yf) * i / ny
        c = slope_pt(side, y, 0) + Vector((-side * 0.02, 0, -0.02))
        rows.append([bm.verts.new(c + Vector((side * math.cos(math.pi * 0.15 + math.pi * 1.25 * k / segs) * rr, 0,
                                                 math.sin(math.pi * 0.15 + math.pi * 1.25 * k / segs) * rr))) for k in range(segs + 1)])
    for i in range(ny):
        for k in range(segs):
            q = (rows[i][k], rows[i + 1][k], rows[i + 1][k + 1], rows[i][k + 1])
            f = bm.faces.new(q if side < 0 else q[::-1])
            for l in f.loops:
                l[uvl].uv = (l.vert.co.y / TILE, k * rr / TILE)
    bm.normal_update(); FK.two_sided(bm); FK._merge(pc, bm, 'roof')

# ----------------------------------------------------------------------------- oven annex and chimney
def annex():
    pc = piece('Annex')
    y0n, y1n, zh, zs = NICHE
    r = (y1n - y0n) / 2; yc = (y0n + y1n) / 2
    D = 1.05                                   # niche depth
    xi = AX1 - D
    q = FK.quad_grid
    uvz = lambda c: ((c.x + c.y) / TILE, c.z / TILE)
    # limewashed walls: front (-Y), outer (+X, with the arch), back (+Y)
    q(pc, 'lime', [(AX0, AY0, PLINTH * 0.5), (AX1, AY0, PLINTH * 0.5), (AX1, AY0, AH), (AX0, AY0, AH)], 6, 8,
      push=lambda p, u, v: p + Vector((0, -0.012 * math.sin(math.pi * u) * math.sin(math.pi * v), 0)), uv_fn=uvz)
    q(pc, 'lime', [(AX1, AY1, PLINTH * 0.5), (AX0, AY1, PLINTH * 0.5), (AX0, AY1, AH), (AX1, AY1, AH)], 6, 8, uv_fn=uvz)
    # outer face around the arch: piers and the spandrel strips
    q(pc, 'lime', [(AX1, AY0, PLINTH * 0.5), (AX1, y0n, PLINTH * 0.5), (AX1, y0n, AH), (AX1, AY0, AH)], 2, 8, uv_fn=uvz)
    q(pc, 'lime', [(AX1, y1n, PLINTH * 0.5), (AX1, AY1, PLINTH * 0.5), (AX1, AY1, AH), (AX1, y1n, AH)], 4, 8, uv_fn=uvz)
    q(pc, 'lime', [(AX1, y0n, PLINTH * 0.5), (AX1, y1n, PLINTH * 0.5), (AX1, y1n, zh - 0.0), (AX1, y0n, zh - 0.0)], 4, 1, uv_fn=uvz)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    N = 14
    for i in range(N):
        ya, yb = y0n + 2 * r * i / N, y0n + 2 * r * (i + 1) / N
        za = zs + math.sqrt(max(0.0, r * r - (ya - yc) ** 2)); zb = zs + math.sqrt(max(0.0, r * r - (yb - yc) ** 2))
        f = bm.faces.new((bm.verts.new((AX1, ya, za)), bm.verts.new((AX1, yb, zb)), bm.verts.new((AX1, yb, AH)), bm.verts.new((AX1, ya, AH))))
        for l in f.loops:
            l[uvl].uv = (l.vert.co.y / TILE, l.vert.co.z / TILE)
    bm.normal_update(); FK._merge(pc, bm, 'lime')
    # sandstone plinth course round the annex foot
    FK.add_box(pc, 'stone', (AX1 - AX0 + 0.08, 0.10, PLINTH * 0.5 + 0.02), Matrix.Translation(((AX0 + AX1) / 2, AY0 - 0.02, PLINTH * 0.25)),
               cham=0.03, uv_world=True, wob=0.01, seed=21)
    FK.add_box(pc, 'stone', (0.10, AY1 - AY0 + 0.08, PLINTH * 0.5 + 0.02), Matrix.Translation((AX1 + 0.02, (AY0 + AY1) / 2, PLINTH * 0.25)),
               cham=0.03, uv_world=True, wob=0.01, seed=22)
    FK.add_box(pc, 'stone', (AX1 - AX0 + 0.08, 0.10, PLINTH * 0.5 + 0.02), Matrix.Translation(((AX0 + AX1) / 2, AY1 + 0.02, PLINTH * 0.25)),
               cham=0.03, uv_world=True, wob=0.01, seed=23)
    # chunky pale quoins at the two outer corners
    R = FK.rng('aquoin')
    z = PLINTH * 0.5
    i = 0
    while z < AH - 0.2:
        h = 0.36 + R.uniform(-0.03, 0.04)
        for (cx, cy, sx, sy) in ((AX1, AY0, 1, -1), (AX1, AY1, 1, 1)):
            ln, sh = R.uniform(0.42, 0.55), R.uniform(0.24, 0.30)
            lx, ly = (ln, sh) if i % 2 else (sh, ln)
            c = Vector((cx - sx * (lx / 2 - 0.04), cy - sy * (ly / 2 - 0.04), z + h / 2))
            FK.add_box(pc, 'stone', (lx, ly, h - 0.03), Matrix.Translation(c), cham=0.04, segs=1, uv_world=True, wob=0.008,
                       seed=R.random() * 99)
        z += h; i += 1
    # the niche: soot-black lining (back, jambs, barrel vault), hearth slab, brick arch ring with a keystone
    q(pc, 'soot', [(xi, y1n, zh), (xi, y0n, zh), (xi, y0n, zs + r), (xi, y1n, zs + r)], 2, 2, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'soot', [(xi, y0n, zh), (AX1, y0n, zh), (AX1, y0n, zs), (xi, y0n, zs)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    q(pc, 'soot', [(AX1, y1n, zh), (xi, y1n, zh), (xi, y1n, zs), (AX1, y1n, zs)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rows = []
    for k in range(N + 1):
        a0 = math.pi * k / N
        rows.append([bm.verts.new((x, yc - math.cos(a0) * r, zs + math.sin(a0) * r)) for x in (xi, AX1)])
    for k in range(N):
        f = bm.faces.new((rows[k][1], rows[k + 1][1], rows[k + 1][0], rows[k][0]))
        for l in f.loops:
            l[uvl].uv = (l.vert.co.x / TILE, k * r * math.pi / N / TILE)
    bm.normal_update(); FK._merge(pc, bm, 'soot')
    FK.add_box(pc, 'stone', (D + 0.12, y1n - y0n + 0.02, 0.14), Matrix.Translation(((xi + AX1) / 2 + 0.06, yc, zh - 0.07)), cham=0.03,
               segs=2, uv_world=True, wob=0.006, seed=24)
    FK.add_box(pc, 'stone', (D, y1n - y0n, zh - 0.14 - PLINTH * 0.5), Matrix.Translation(((xi + AX1) / 2, yc, (zh - 0.14 + PLINTH * 0.5) / 2)),
               cham=0.0, uv_world=True, seed=25)
    NV = 11
    for k in range(NV):
        a0 = math.pi * (k + 0.5) / NV
        big = k == NV // 2
        c = Vector((AX1 + 0.03, yc - math.cos(a0) * (r + 0.11), zs + math.sin(a0) * (r + 0.11)))
        FK.add_box(pc, 'stone' if big else 'brick', (0.14, 0.15 if not big else 0.22, 0.24 if not big else 0.32),
                   Matrix.Translation(c + (Vector((0, 0, 0.03)) if big else Vector())) @ Matrix.Rotation(-(a0 - math.pi / 2), 4, 'X'),
                   cham=0.02, uv_world=True, wob=0.004, seed=k + 30)
    for yj in (y0n - 0.09, y1n + 0.09):   # brick jambs up to the springing
        zz = zh
        k = 0
        while zz < zs - 0.02:
            h = min(0.085, zs - zz)
            FK.add_box(pc, 'brick', (0.14, 0.18 + 0.06 * (k % 2), h - 0.012), Matrix.Translation((AX1 + 0.03, yj, zz + h / 2)),
                       cham=0.008, uv_world=True, seed=k + yj)
            zz += h; k += 1
    # a few spare bricks and a cold ash smear on the empty hearth
    for k, (dx, dy, yaw) in enumerate(((0.25, 0.25, 0.3), (0.42, 1.25, 1.2), (0.34, 1.33, 0.9))):
        FK.add_box(pc, 'brick', (0.22, 0.10, 0.065), Matrix.Translation((xi + dx, y0n + dy, zh + 0.033 + (0.065 if k == 2 else 0))) @
                   Matrix.Rotation(yaw, 4, 'Z'), cham=0.008, uv_world=True, seed=k + 40)
    # catslide roof: from the house wall down over the annex
    z_hi, z_lo = 3.80, AH + 0.02
    def rp(x, y):
        t = (x - AX0) / (AX1 + 0.35 - AX0)
        return Vector((x, y, z_hi + (z_lo - z_hi) * t))
    n = Vector((z_hi - z_lo, 0, AX1 + 0.35 - AX0)).normalized()
    ya0, ya1 = AY0 - 0.32, AY1 + 0.32
    FK.quad_grid(pc, 'roof', [rp(AX0, ya0), rp(AX0, ya1), rp(AX1 + 0.35, ya1), rp(AX1 + 0.35, ya0)], 8, 3,
                 uv_fn=lambda c: (c.y / TILE, c.x / TILE))
    FK.quad_grid(pc, 'oak', [rp(AX0, ya0) - n * 0.14, rp(AX1 + 0.35, ya0) - n * 0.14, rp(AX1 + 0.35, ya1) - n * 0.14, rp(AX0, ya1) - n * 0.14],
                 8, 2, uv_fn=lambda c: (c.y / TILE, c.x / TILE))
    for yv in (ya0, ya1):
        a, b = rp(AX0, yv), rp(AX1 + 0.35, yv)
        FK.quad_grid(pc, 'oak', [a, b, b - n * 0.14, a - n * 0.14][::(1 if yv == ya0 else -1)], 2, 1, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    tile_courses(pc, lambda t: (rp(AX1 + 0.35 - (AX1 + 0.35 - AX0) * t, ya1), rp(AX1 + 0.35 - (AX1 + 0.35 - AX0) * t, ya0)), n, 6,
                 'annex', flip=False)
    FK.member(pc, 'oak', rp(AX1 + 0.35, ya0) - n * 0.1, rp(AX1 + 0.35, ya1) - n * 0.1, 0.18, 0.07, name='afascia', up=(1, 0, 0.4))
    # lead flashing where the catslide meets the house wall
    FK.add_box(pc, 'iron', (0.10, ya1 - ya0, 0.04), Matrix.Translation((AX0 + 0.05, (ya0 + ya1) / 2, z_hi + 0.02)), cham=0.01, seed=26)
    return pc

def chimney():
    pc = piece('Chimney')
    x0, x1, y0, y1, top = CH
    # stack: wide base to the shoulder, weathered set-off, tall shaft, corbelled cap, two pots; leans out a touch
    def brick_box(size, c, seed, cuts=(0, 0, 0)):
        FK.add_box(pc, 'brick', size, Matrix.Translation(c), cham=0.03, cuts=cuts, uv_world=True, wob=0.012, seed=seed)
    sh = 3.6
    brick_box((x1 - x0 + 0.16, y1 - y0 + 0.16, sh), Vector(((x0 + x1) / 2, (y0 + y1) / 2, sh / 2)), 1, cuts=(1, 1, 5))
    # set-off: sloped stone weathering
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=(x1 - x0 + 0.16) * 0.7071, radius2=(x1 - x0 - 0.12) * 0.7071, depth=0.32)
    bm.transform(Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, sh + 0.16)) @ Matrix.Rotation(math.pi / 4, 4, 'Z') @
                 Matrix.Diagonal((1, (y1 - y0 + 0.16) / (x1 - x0 + 0.16), 1, 1)))
    bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'stone')
    sx, sy = x1 - x0 - 0.12, y1 - y0 - 0.12
    z1s = 7.3
    brick_box((sx, sy, z1s - (sh + 0.32)), Vector(((x0 + x1) / 2, (y0 + y1) / 2, (sh + 0.32 + z1s) / 2)), 2, cuts=(1, 1, 6))
    FK.add_box(pc, 'stone', (sx + 0.06, sy + 0.06, 0.14), Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, z1s + 0.05)), cham=0.03,
               uv_world=True, wob=0.006, seed=12)
    sx, sy = sx - 0.08, sy - 0.06
    brick_box((sx, sy, top - 0.45 - (z1s + 0.12)), Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z1s + 0.12 + top - 0.45) / 2)), 13, cuts=(1, 1, 5))
    for k, (dz, grow) in enumerate(((0.0, 0.10), (0.13, 0.19), (0.27, 0.14))):
        brick_box((sx + grow * 2, sy + grow * 2, 0.13), Vector(((x0 + x1) / 2, (y0 + y1) / 2, top - 0.45 + dz + 0.065)), 3 + k)
    FK.add_box(pc, 'stone_dk', (sx + 0.22, sy + 0.22, 0.06), Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, top - 0.02)), cham=0.02,
               uv_world=True, seed=7)
    for k, dy in enumerate((-0.2, 0.22)):
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=False, segments=10, radius1=0.12, radius2=0.09, depth=0.42 + 0.06 * k)
        bm.transform(Matrix.Translation(((x0 + x1) / 2 + 0.02, (y0 + y1) / 2 + dy, top + 0.2 + 0.03 * k)))
        bm.normal_update(); FK.two_sided(bm, 0.02)
        uvl = bm.loops.layers.uv.new('UVMap')
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, k * 0.3)); FK._merge(pc, bm, 'brick')
        bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, segments=10, radius=0.085)
        bm.transform(Matrix.Translation(((x0 + x1) / 2 + 0.02, (y0 + y1) / 2 + dy, top + 0.15))); uvl = bm.loops.layers.uv.new('UVMap')
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'soot')
    # whimsy: the stack leans out, about 1.4 deg about its foot
    rot = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, sh)) @ Matrix.Rotation(math.radians(-1.4), 4, 'Y') @ \
        Matrix.Translation((-(x0 + x1) / 2, -(y0 + y1) / 2, -sh))
    bmesh.ops.transform(pc.bm, matrix=rot, verts=[v for v in pc.bm.verts if v.co.z > sh])
    # iron stays back to the house wall
    z = 6.9
    xs = (x0 + x1) / 2 + math.tan(math.radians(1.4)) * (z - sh) - sx / 2
    xr = XC + (RIDGE + 0.24 - 0.10 - z) / TANP           # where the stay meets the roof slope
    a = Vector((xr, (y0 + y1) / 2, z)); b = Vector((xs + 0.04, (y0 + y1) / 2, z))
    FK.add_box(pc, 'iron', ((b - a).length, 0.045, 0.045), Matrix.Translation((a + b) / 2), cham=0.0, long_axis=0, seed=z)
    FK.add_box(pc, 'iron', (0.04, sy + 0.08, 0.07), Matrix.Translation((xs + 0.02, (y0 + y1) / 2, z)), cham=0.0, seed=z + 1)
    FK.add_box(pc, 'iron', (0.03, 0.10, 0.22), Matrix.Translation((xr + 0.05, (y0 + y1) / 2, z - 0.06)) @
               Matrix.Rotation(math.atan(TANP), 4, 'Y'), cham=0.0, seed=z + 2)
    return pc

def yard():
    pc = piece('Yard')
    R = FK.rng('yard')
    du0, du1 = DOOR[0], DOOR[1]
    xc = X0 + (du0 + du1) / 2
    for k in range(5):
        y = Y0 - 0.95 - k * 0.78
        c = Vector((xc + R.uniform(-0.12, 0.12) + (0.1 if k % 2 else -0.1), y, 0.025))
        if y < -4.8:
            break
        FK.add_box(pc, 'stone', (0.72 + R.uniform(-0.08, 0.08), 0.52 + R.uniform(-0.05, 0.05), 0.07), Matrix.Translation(c) @
                   Matrix.Rotation(R.uniform(-0.25, 0.25), 4, 'Z'), cham=0.03, segs=2, uv_world=True, wob=0.012, seed=k + 50)
    return pc

# ----------------------------------------------------------------------------- parts
def part_door():
    FK.DOOR = DOOR
    pc = FK.kit_door()
    pc.name = 'Part_Door'
    return pc, F_FRONT, Vector(((DOOR[0] + DOOR[1]) / 2, 0.0, DOOR[2]))

def part_oven():
    pc = piece('Part_Oven')
    y0n, y1n, zh, zs = NICHE
    yc = (y0n + y1n) / 2
    xi = AX1 - 1.05
    # dome: a squashed half-ellipsoid of brick, bulging 0.5 m out of the arch
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=18, v_segments=10, radius=1.0)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.01], context='VERTS')
    ex = (AX1 + 0.55 - xi) / 2
    cx = xi + ex
    for v in bm.verts:
        p = v.co
        n = 0.035 * noise.noise(p * 2.2)
        v.co = Vector((cx + p.x * ex * (1 + n), yc + p.y * 0.92 * (1 + n), zh + max(0.0, p.z) * 1.18 * (1 + n)))
    bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0.1, 0.0)); FK._merge(pc, bm, 'brick')
    # mouth: a brick collar with a round-headed opening, glowing ember bed inside, iron door swung aside
    xm = AX1 + 0.45
    mw, mh = 0.66, 0.52
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    N = 10; outer = []; inner = []
    for k in range(N + 1):
        a0 = math.pi * k / N
        inner.append(bm.verts.new((xm + 0.12, yc - math.cos(a0) * mw / 2, zh + 0.05 + mh - mw / 2 + math.sin(a0) * mw / 2)))
        outer.append(bm.verts.new((xm + 0.12, yc - math.cos(a0) * (mw / 2 + 0.16), zh + 0.05 + mh - mw / 2 + math.sin(a0) * (mw / 2 + 0.16))))
    for k in range(N):
        bm.faces.new((outer[k], outer[k + 1], inner[k + 1], inner[k])[::-1])
    for s in (0, N):
        a, b = inner[s], outer[s]
        c, d = bm.verts.new((xm + 0.12, a.co.y, zh + 0.0)), bm.verts.new((xm + 0.12, b.co.y, zh + 0.0))
        f = bm.faces.new((a, b, d, c) if s == 0 else (b, a, c, d))
    for f in bm.faces:
        for l in f.loops:
            l[uvl].uv = (l.vert.co.y / TILE, l.vert.co.z / TILE)
    bm.normal_update()
    # thickness: extrude the collar back toward the dome
    ext = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, verts=[e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)], vec=(-0.30, 0, 0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    FK._merge(pc, bm, 'brick')
    # throat: soot lining behind the mouth, ember bed on its floor
    FK.quad_grid(pc, 'soot', [(xm - 0.25, yc + mw / 2, zh + 0.05), (xm - 0.25, yc - mw / 2, zh + 0.05),
                              (xm - 0.25, yc - mw / 2, zh + 0.05 + mh + 0.02), (xm - 0.25, yc + mw / 2, zh + 0.05 + mh + 0.02)],
                 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    for s in (-1, 1):
        y = yc + s * mw / 2
        pts = [(xm + 0.12, y, zh + 0.05), (xm - 0.25, y, zh + 0.05), (xm - 0.25, y, zh + 0.05 + mh), (xm + 0.12, y, zh + 0.05 + mh)]
        FK.quad_grid(pc, 'soot', pts if s > 0 else pts[::-1], 1, 1, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    FK.quad_grid(pc, 'soot', [(xm + 0.12, yc - mw / 2, zh + 0.05 + mh), (xm - 0.25, yc - mw / 2, zh + 0.05 + mh),
                              (xm - 0.25, yc + mw / 2, zh + 0.05 + mh), (xm + 0.12, yc + mw / 2, zh + 0.05 + mh)], 1, 1,
                 uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    R = FK.rng('embers')
    FK.quad_grid(pc, 'ember', [(xm - 0.25, yc + mw / 2 - 0.02, zh + 0.06), (xm + 0.1, yc + mw / 2 - 0.02, zh + 0.06),
                               (xm + 0.1, yc - mw / 2 + 0.02, zh + 0.06), (xm - 0.25, yc - mw / 2 + 0.02, zh + 0.06)][::-1], 1, 1,
                 uv_fn=lambda c: (c.x, c.y))
    for k in range(9):
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=R.uniform(0.03, 0.05))
        bm.transform(Matrix.Translation((xm + R.uniform(-0.22, 0.05), yc + R.uniform(-0.24, 0.24), zh + 0.07)) @ Matrix.Diagonal((1.4, 1, 0.6, 1)))
        uvl = bm.loops.layers.uv.new('UVMap'); FK._merge(pc, bm, 'ember' if k % 3 else 'soot')
    # the arched iron door, swung open against the dome on a pintle
    dw = mw + 0.08
    hinge = Vector((xm + 0.15, yc + dw / 2 + 0.04, zh + 0.05))
    rot = Matrix.Translation(hinge) @ Matrix.Rotation(math.radians(158), 4, 'Z')
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    ring = [bm.verts.new(p) for p in ([Vector((0, 0, 0))] + [Vector((0, -dw, 0))] +
                                      [Vector((0, -dw / 2 + math.cos(math.pi * k / N) * dw / 2, mh - dw / 2 + math.sin(math.pi * k / N) * dw / 2 + 0.02))
                                       for k in range(N, -1, -1)])]
    # order: bottom-hinge (0), bottom-free (-dw), then arch from free side over to hinge side
    f = bm.faces.new(ring)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, verts=[e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)], vec=(0.025, 0, 0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.transform(rot); bm.normal_update()
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0.4, 0.2)); FK._merge(pc, bm, 'iron')
    for zz in (0.12, mh - 0.12):
        FK.add_box(pc, 'iron', (0.02, dw * 0.8, 0.045), rot @ Matrix.Translation((0.03, -dw * 0.4, zz)), cham=0.0, long_axis=1, seed=zz)
    FK.add_box(pc, 'iron', (0.06, 0.05, 0.05), rot @ Matrix.Translation((0.05, -dw + 0.06, mh * 0.5)), cham=0.008, seed=3)
    # a wooden peel leaning against the hearth, and a small stack of split logs on the slab beside the dome
    a = Vector((AX1 + 0.62, y1n + 0.16, 0.0)); b = Vector((AX1 + 0.06, y1n + 0.02, 1.75))
    FK.member(pc, 'oak_lt', a, b, 0.045, 0.045, name='peel', cham=0.012, up=(1, 0, 0))
    FK.add_box(pc, 'oak_lt', (0.03, 0.30, 0.34), Matrix.Translation(b + (b - a).normalized() * 0.15) @
               Matrix.Rotation(math.atan2(b.x - a.x, b.z - a.z), 4, 'Y'), cham=0.01, seed=4)
    return pc, None, Vector((xi + 0.6, yc, zh))

def part_sign():
    pc = piece('Part_Sign')
    u = SIGN_U
    zt = SIGN_Z - 0.06
    bw, bh = 1.08, 0.72
    zb = zt - 0.30 - bh / 2
    hy0, hy1 = SIGN_HOOKS
    yc = -PROUD - (hy0 + hy1) / 2
    # chains from the hooks
    for hy in SIGN_HOOKS:
        y = -PROUD - hy
        for k in range(5):
            FK.add_box(pc, 'iron', (0.012, 0.03, 0.055), Matrix.Translation((u, y, zt - 0.03 - k * 0.05)) @ Matrix.Rotation((k % 2) * 1.57, 4, 'Z'),
                       cham=0.0, seed=k + hy)
    # oval board, its plane facing +-X (it hangs across the jetty, read along the street)
    def oval(rw, rh, th, mat, c, segs=24):
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        ring = [bm.verts.new((0, math.cos(2 * math.pi * k / segs) * rw, math.sin(2 * math.pi * k / segs) * rh)) for k in range(segs)]
        f = bm.faces.new(ring)
        ext = bmesh.ops.extrude_face_region(bm, geom=[f])
        bmesh.ops.translate(bm, verts=[e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)], vec=(th, 0, 0))
        bmesh.ops.translate(bm, verts=bm.verts[:], vec=(-th / 2, 0, 0))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.transform(Matrix.Translation(c))
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0.2, 0.3)); FK._merge(pc, bm, mat)
    c = Vector((u, yc, zb))
    oval(bh / 2 * 0 + bw / 2, bh / 2, 0.06, 'door', c)
    oval(bw / 2 - 0.07, bh / 2 - 0.07, 0.09, 'lime', c)
    # iron hoop round the rim, top eyes for the chains
    for k in range(20):
        a0 = 2 * math.pi * k / 20
        p = c + Vector((0, math.cos(a0) * (bw / 2 + 0.01), math.sin(a0) * (bh / 2 + 0.01)))
        FK.add_box(pc, 'iron', (0.08, 0.02, 0.16), Matrix.Translation(p) @ Matrix.Rotation(a0, 4, 'X'), cham=0.0, seed=k)
    # both faces: a carved loaf with score cuts and a gilt wheat sprig
    for s in (-1, 1):
        lc = c + Vector((s * 0.05, -0.06, -0.04))
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=7, radius=1.0)
        for v in bm.verts:
            p = v.co
            dz = -0.12 * max(0.0, math.cos(p.y * 9.0)) * max(0.0, -p.x * s)
            v.co = Vector((max(0.0, p.x * s) * s * 0.07 * (1 + dz) if p.x * s > 0 else p.x * 0.005, p.y * 0.30, p.z * 0.13 * (1 + 0.15 * p.y)))
        bm.transform(Matrix.Translation(lc) @ Matrix.Rotation(math.radians(-8), 4, 'X'))
        bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0.5, 0.1)); FK._merge(pc, bm, 'bread')
        for k in range(3):   # score cuts: dark crust slashes across the loaf
            FK.add_box(pc, 'crust', (0.02, 0.05, 0.16), Matrix.Translation(lc + Vector((s * 0.068, -0.12 + k * 0.12, 0.0))) @
                       Matrix.Rotation(math.radians(-8 + 35), 4, 'X'), cham=0.0, seed=k + s)
        stem_a = c + Vector((s * 0.04, 0.17, -0.22)); stem_b = c + Vector((s * 0.04, 0.27, 0.17))
        for j, dy in enumerate((-0.03, 0.0, 0.03)):
            a = stem_a + Vector((0, dy * 0.3, 0)); b = stem_b + Vector((0, dy * 2.4, 0))
            d = b - a
            tube(pc, 'gilt', [a, (a + b) / 2 + Vector((0, 0.01 * dy, 0)), b], 0.009, 4)
            for e in range(3):
                p = b - d.normalized() * (0.03 + e * 0.055)
                for side in (-1, 1):
                    FK.add_box(pc, 'gilt', (0.02, 0.025, 0.05), Matrix.Translation(p + Vector((0, side * 0.018, 0))) @
                               Matrix.Rotation(side * 0.5, 4, 'X'), cham=0.0, seed=e + side + j)
    # gilt pretzel hung under the board
    P = [(0.30, -0.08), (0.12, 0.22), (0.02, 0.38), (-0.10, 0.44), (-0.30, 0.34), (-0.46, 0.04), (-0.34, -0.30),
         (0.0, -0.42), (0.34, -0.30), (0.46, 0.04), (0.30, 0.34), (0.10, 0.44), (-0.02, 0.38), (-0.12, 0.22), (-0.30, -0.08)]
    def cr(p0, p1, p2, p3, t):
        return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)
    V = [Vector(p) for p in P]
    V = [V[0]] + V + [V[-1]]
    pts = []
    for i in range(1, len(V) - 2):
        for k in range(4):
            pts.append(cr(V[i - 1], V[i], V[i + 1], V[i + 2], k / 4))
    pts.append(V[-2])
    sc = 0.36; pc_c = c + Vector((0, 0, -bh / 2 - 0.28))
    W3 = [pc_c + Vector((0.012 * math.sin(i * 0.9), p.x * sc, p.y * sc)) for i, p in enumerate(pts)]
    tube(pc, 'gilt', W3, 0.024, 7)
    for k in range(3):
        FK.add_box(pc, 'iron', (0.012, 0.025, 0.05), Matrix.Translation(c + Vector((0, 0, -bh / 2 - 0.03 - k * 0.045))) @
                   Matrix.Rotation((k % 2) * 1.57, 4, 'Z'), cham=0.0, seed=k + 70)
    return pc, F_FRONT_UP, Vector((u, -PROUD - (hy0 + hy1) / 2, SIGN_Z - 0.05))

# ----------------------------------------------------------------------------- whimsy, wear, assembly
def lean_co(p):
    if p.x > X1 + 0.05:          # the annex and chimney stand on their own footing
        return p
    return Vector((p.x - LEAN[0] * max(0.0, p.z - PLINTH), p.y - LEAN[1] * max(0.0, p.z - BRES0), p.z))

SOCKETS = {}

RUNOFF = [0.98 - 0.11, 1.30 - 0.11, 3.98 - 0.11, 4.0 - 0.11, BRES0]

def write_wear(ob):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    mw = ob.matrix_world
    door_w = F_FRONT @ Vector(((DOOR[0] + DOOR[1]) / 2, 0, 0))
    cx, cy = (CH[0] + CH[1]) / 2, (CH[2] + CH[3]) / 2
    mats = [m.name for m in me.materials]
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        mname = mats[poly.material_index] if mats else ''
        roofish = mname == 'FK_RoofTile'
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 1.7)
            streak = noise.noise(Vector((p.x * 4.0, p.y * 4.0, p.z * 0.5)))
            r = max(0.0, 1 - p.z / (0.95 + 0.25 * nz)) ** 1.2 * 0.78
            r = max(r, 0.14 * max(0.0, noise.noise(p * 0.5 + Vector((3.0, 1.0, 2.0)))) + 0.05 * max(0.0, nz))
            # shade-damp band under the jetty and under the side eaves
            if 2.4 < p.z < 3.1 and p.y < Y0 + 0.2:
                r = max(r, 0.18 + 0.1 * streak)
            if PLATE0 - 0.6 < p.z < PLATE1 and abs(p.x - XC) > HW - 0.3:
                r = max(r, 0.22 + 0.1 * streak)
            # soot: chimney top, the plume above the oven niche, the niche lining
            if abs(p.x - cx) < 0.9 and abs(p.y - cy) < 0.9 and p.z > CH[4] - 1.4:
                r = max(r, min(0.9, (p.z - (CH[4] - 1.4)) / 1.2) * (0.7 + 0.2 * streak))
            if p.x > AX1 - 0.05 and NICHE[0] - 0.3 < p.y < NICHE[1] + 0.3 and NICHE[3] < p.z < AH + 0.5:
                r = max(r, 0.88 * max(0.0, 1 - abs(p.y - (NICHE[0] + NICHE[1]) / 2) / 1.35) ** 0.8 * (0.8 + 0.3 * streak))
            for zs_ in RUNOFF:
                dz = zs_ - p.z
                if 0.0 < dz < 0.9 and abs(n.z) < 0.5:
                    r = max(r, (1 - dz / 0.9) ** 1.6 * max(0.0, 0.10 + 0.32 * streak))
            if CH[0] - 0.1 < p.x and CH[2] - 0.1 < p.y < CH[3] + 0.1 and AH < p.z < 6.5 and n.x > 0.5:
                r = max(r, 0.35 * (1 - (p.z - AH) / 3.7) * (0.7 + 0.4 * streak))   # smoke-stained flank above the oven
            g = 0.0
            if roofish and n.z > 0.2:
                z_rel = p.z - (PLATE1 - OV_E * TANP)
                g = max(0.0, 1 - z_rel / 1.4) * 0.75 + max(0.0, 0.15 + 0.45 * nz) * 0.3
                if p.x > X1 + 0.05:
                    g = max(g, 0.45 + 0.3 * nz)               # the low catslide holds the damp
                tv = ((poly.index * 2654435761) % 1009) / 1009.0
                r = max(r, 0.28 * tv * tv)
            elif n.z > 0.6 and p.z < PLINTH + 0.1:
                g = 0.3 + 0.3 * nz
            b = 0.0
            dd = (Vector((p.x, p.y, 0)) - Vector((door_w.x, door_w.y, 0))).length
            if p.z < 0.3 and dd < 1.3 and n.z > 0.5:
                b = 0.95 * (1 - dd / 1.3) ** 0.5               # foot-polished, flour-dusted step
                g = 0.0
            if mname == 'BK_Flour':
                b, g, r = 0.6, 0.0, min(r, 0.1)
            if mname == 'BK_Sack':
                b = max(b, 0.55 * max(0.0, n.z) + 0.2)
            if mname == 'BK_Bread' or mname == 'BK_Crust':
                b = max(b, 0.25 * max(0.0, n.z))               # a dusting of flour on the crust
            if ob.name == 'Part_Door':
                lx = abs(p.x - (X0 + DOOR[1] - 0.17)); lz = abs(p.z - 1.1)
                if lx < 0.25 and lz < 0.35:
                    b = max(b, 0.5 * (1 - lx / 0.25)); r = max(r, 0.35 * (1 - lz / 0.35))
                if p.z < 0.5:
                    b = max(b, 0.4 * (1 - p.z / 0.5))          # flour kicked against the bottom rail
            if ob.name == 'Part_Oven' and mname == 'BK_Brick':
                r = max(r, 0.6 * max(0.0, 1 - abs(p.y - (NICHE[0] + NICHE[1]) / 2) / 0.5) * max(0.0, (p.z - NICHE[2] - 0.3) / 0.6) *
                        (1.0 if p.x > AX1 + 0.2 else 0.4))
            attr.data[li].color = (min(1.0, r), min(1.0, g), min(1.0, b), 1.0)

SOFT = {'BK_Flour', 'BK_Sack', 'BK_Bread', 'BK_Crust', 'BK_Cloth', 'BK_Wicker', 'BK_Gilt'}

def to_object(pc, parent, origin=None, lean=True):
    if lean:
        for v in pc.bm.verts:
            v.co = lean_co(v.co)
    if origin is not None:
        bmesh.ops.translate(pc.bm, verts=pc.bm.verts[:], vec=-origin)
    me = bpy.data.meshes.new(pc.name)
    pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats:
        me.materials.append(m)
    soft = SOFT | ({'BK_Brick'} if pc.name == 'Part_Oven' else set())
    for poly in me.polygons:
        if me.materials[poly.material_index].name in soft:
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
    root = bpy.data.objects.new('Bakery', None); bpy.context.scene.collection.objects.link(root)
    frame = piece('Frame')
    for pc, xf in front_walls() + left_walls() + right_walls() + back_walls():
        merge(frame, pc, xf)
    for pc in (roof(), annex(), chimney(), yard()):
        merge(frame, pc)
    fo = to_object(frame, root)
    total = tris(fo)
    print('  %-12s %6d tris  %d slots' % ('Frame', total, len(fo.data.materials)))
    parts = {}
    for fn, sock in ((part_door, 'Door'), (part_oven, 'Oven'), (part_sign, 'Sign')):
        pc, xf, s_local = fn()
        if xf is not None:
            bmesh.ops.transform(pc.bm, matrix=xf, verts=pc.bm.verts[:])
            s = xf @ s_local
        else:
            s = s_local
        s = lean_co(s) if s.x <= X1 + 0.05 else s
        ob = to_object(pc, root, origin=s)
        parts[sock] = ob
        t = tris(ob); total += t
        print('  %-12s %6d tris  %s' % (ob.name, t, ', '.join(m.name for m in ob.data.materials)))
        e = bpy.data.objects.new('SOCKET_' + sock, None); bpy.context.scene.collection.objects.link(e)
        e.parent = fo; e.location = s; e.empty_display_size = 0.3
        SOCKETS[sock] = s
    # footprint: the ground-storey body, annex, chimney and oven; flat
    fx0, fx1 = X0 - 0.12, AX1 + 0.50
    fy0, fy1 = Y0 - 0.12, Y1 + 0.12
    me = bpy.data.meshes.new('FOOTPRINT'); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation(((fx0 + fx1) / 2, (fy0 + fy1) / 2, 0.05)) @ Matrix.Diagonal((fx1 - fx0, fy1 - fy0, 0.1, 1)))
    bm.to_mesh(me); bm.free()
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root
    fp.display_type = 'WIRE'; fp.hide_render = True
    print('FOOTPRINT x %.2f..%.2f  y %.2f..%.2f  (%.2f x %.2f m; L class max 10.5)' % (fx0, fx1, fy0, fy1, fx1 - fx0, fy1 - fy0))
    allx = [ (o.matrix_world @ Vector(c)).x for o in [fo] + list(parts.values()) for c in o.bound_box]
    ally = [ (o.matrix_world @ Vector(c)).y for o in [fo] + list(parts.values()) for c in o.bound_box]
    print('bounds x %.2f..%.2f  y %.2f..%.2f  (foundation +-5.5)' % (min(allx), max(allx), min(ally), max(ally)))
    print('Bakery total tris: %d (cap 45000)' % total)
    return root, fo, parts, fp

# ----------------------------------------------------------------------------- review
VIEWS_DEF = {
    'tq': (Vector((11.5, -15.0, 3.2)), Vector((0.2, 0, 4.4)), 44),
    'tql': (Vector((-13.5, -13.0, 3.0)), Vector((-0.6, 0, 4.4)), 44),
    'back': (Vector((10.0, 15.5, 3.4)), Vector((0.2, 0.5, 4.6)), 44),
    'front': (Vector((-0.6, -19.0, 3.0)), Vector((-0.6, 0, 4.8)), 42),
    'frame': (Vector((11.5, -15.0, 3.2)), Vector((0.2, 0, 4.4)), 44),
    'close': (Vector((3.2, -7.4, 1.7)), Vector((-1.4, -2.4, 1.6)), 44),
    'oven': (Vector((9.0, -2.5, 2.0)), Vector((4.2, 0.9, 1.4)), 44),
    'door': (Vector((X0 + 3.4, Y0 - 3.0, 1.5)), Vector((X0 + 1.8, Y0, 0.6)), 44),
    'sign': (Vector((X0 - 2.6, YJ - 2.6, 3.2)), Vector((X0 + SIGN_U, YJ - 0.95, 3.6)), 40),
    'frameoven': (Vector((9.0, -2.5, 2.0)), Vector((4.2, 0.9, 1.4)), 44),
}

def review(root, parts):
    sc = bpy.context.scene
    FK.RRES = RRES; FK.SAMPLES = SAMPLES
    cam = FK.review_scene(root, variant=True)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(X0 + 0.35, Y0 - 1.5, 0.875))
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
        sc.render.filepath = os.path.join(OUT, 'bakery_%s.png' % v)
        bpy.ops.render.render(write_still=True)
        print('rendered', v)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, fo, parts, fp = build()
    if '--export' in argv:
        for o in bpy.context.scene.objects:
            o.select_set(o == root or o.parent in (root, fo))
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Bakery.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                                 mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR',
                                 path_mode='STRIP')   # no texture paths: Unity names textured slots 'Lit' when a cloud path exists
        print('exported', os.path.join(OUT, 'Bakery.fbx'))
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'bakery.blend'))
    if '--no-render' not in argv:
        review(root, parts)
