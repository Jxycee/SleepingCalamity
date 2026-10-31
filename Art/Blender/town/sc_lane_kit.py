"""LaneKit: the Hollowmere ring 2 lane kit (Q11, T2 kit): garden walls, terrace retaining walls, lane steps, kerbs, drain gutters, wall
lanterns and yard gates. Original storybook stonework in the Dark Cloud tradition rendered in stylised realism: chunky rubble with a
different value and texture patch per stone, dressed quoins on piers, 1-3 degrees off true, chamfers, damp dark foot, moss on tops,
faded sage-green gate paint. Built on the Q9 keep terrace kit (sc_keep_terrace.py geometry helpers, sc_facade_kit.py materials).
Contract: Reference/hollowmere/lanes/CONTRACT.md (piece names, extents in Unity terms, slots, budgets).

Pieces (each a direct child mesh of root LaneKit at its own pivot at the origin; Unity local extents, lane face = local -Z):
  GardenWall, GardenWallBroken, GardenWallEnd, RetainWall, RetainWallLow, RetainWallBreach, RetainButtress, LaneStep, LaneKerb, Gutter,
  GutterChoked, GutterGrate, WallLantern, WallLanternBroken, GatePier, GateLeaf, GateLeafBroken.
Wall modules (GardenWall, RetainWall*) tile in X every 2 m: stones crossing the seam are cut on the seam plane, UVs are world metres
(2 m per tile) with per-stone offsets that are multiples of the tile, wear noise repeats every 2 m. LaneStep setts and Gutter slabs likewise.

Authoring frame (this file): A = (x across, y = Unity z depth, z = Unity y up); meshes are rotated 180 deg about Z into Blender axes so the
FBX (-Z forward, Y up) lands on Unity (x, y, z) = Blender (-x, z, -y). Materials (shared B1 tileables, metre UVs; linear CORNER float
colour attribute "wear": R damp/grime (also the dark end of the per-stone value variation), G moss, B polish (also the light end)):
LK_Rubble, LK_Dressed, LK_Mortar (sandstone), LK_Oak, LK_GatePaint (painted_wood tinted green), LK_Iron, LK_GlassLit (emissive), LK_Earth.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_lane_kit.py -- --out <scratch>
      [--assemble] [--views lane,flight,close,ruined] [--rres 900] [--samples 16] [--no-render] [--tris] [--export]
      [--export-dir <dir>] [--blend]
  --assemble lays out the review lane (never exported): a 0.29-grade flight of 12 LaneSteps 5 m wide with kerbs and a gutter, a garden wall
  run stepping down beside it with a gate and lanterns and a 3 m retaining wall with a buttress and lantern (left, Rebuilt), the Ruined
  variants on the right. Without it the loose kit is lined up. --tris prints the table and stops. Budget: kit under 30k tris.

Status (2026-10-31, cloud): 3 render rounds (+1 review-scene UV fix), self-score 26.5 -> 28 -> 29/40 (calibrated; first + 5 = 31.5 not reached,
stopped at the 3-round limit). Final lines: silhouette 4, proportion 3.5, form 4, surface 3.5, colour 3, wear 3.5, set 3.5, efficiency 4.
18,562 tris (--tris, two runs agree). Sheet: Reference/hollowmere/lanes/SHEET_lane_kit.jpg (existing refs: the Commons API was rate-limited).
Round changes: r1 baseline (textures were LFS pointers: pulled); r2 greyer rubble tint plus stronger per-stone tone, gate boards with relief;
r3 cooler grey rubble against warm pale dressed stone, one mossy stone in three, heavier moss on tops and damp feet.
Next visible gains: moss that reads as cushions on wall tops (a B9 foliage tuft row), warmer ochre stones mixed into the rubble (needs a second
rubble slot or a tint channel), board-and-batten paint chipping on the gate, more varied cap heights, a dedicated sett normal for LaneStep.
"""
import sys, os, math, zlib, shutil
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK
import sc_keep_terrace as KT

OUT = FK.OUT
argv = FK.argv
RRES = int(FK.arg('--rres', '900')); SAMPLES = int(FK.arg('--samples', '16'))
TILE = FK.TILE
R_ = lambda s: FK.rng('lk:' + s)
def Rx(d): return Matrix.Rotation(math.radians(d), 4, 'X')
def Ry(d): return Matrix.Rotation(math.radians(d), 4, 'Y')
def Rz(d): return Matrix.Rotation(math.radians(d), 4, 'Z')
def T(*v): return Matrix.Translation(Vector(v))

# ----------------------------------------------------------------------------- materials
def glit_mat():
    if 'LK_GlassLit' in FK.MATS:
        return FK.MATS['LK_GlassLit']
    m = bpy.data.materials.new('LK_GlassLit'); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (0.9, 0.55, 0.2, 1); b.inputs['Roughness'].default_value = 0.3
    b.inputs['Emission Color'].default_value = (1.0, 0.62, 0.28, 1); b.inputs['Emission Strength'].default_value = 3.0
    FK.MATS['LK_GlassLit'] = m
    return m

LKM = {
    'rub': lambda: FK.tile_mat('LK_Rubble', 'B1_town/sandstone', tint=(0.57, 0.60, 0.66), normal=1.5),
    'dress': lambda: FK.tile_mat('LK_Dressed', 'B1_town/sandstone', tint=(1.0, 0.95, 0.86), normal=1.3),
    'mort': lambda: FK.tile_mat('LK_Mortar', 'B1_town/sandstone', tint=(0.26, 0.245, 0.21), normal=0.6),
    'oak': lambda: FK.tile_mat('LK_Oak', 'B1_town/oak_timber', tint=(0.80, 0.76, 0.72), normal=1.2),
    'paint': lambda: FK.tile_mat('LK_GatePaint', 'B1_town/painted_wood', tint=(0.66, 0.72, 0.28), normal=1.0),
    'iron': lambda: FK.tile_mat('LK_Iron', 'B1_town/iron'),
    'glit': glit_mat,
    'earth': lambda: FK.flat_mat('LK_Earth', (0.034, 0.025, 0.017), 0.95),
}
_prev_M = FK.M
FK.M = lambda k: LKM[k]() if k in LKM else _prev_M(k)

# ----------------------------------------------------------------------------- stone helpers (per-stone tone + uv patch)
STONES = {}      # piece -> [(centre, size, tone, mat)] for the wear pass
def tone_of(sid):
    r = FK.rng('tone:' + sid)
    return max(-1.0, min(1.0, r.gauss(0.0, 0.75))), r.random(), r.random()

def fb(pc, mat, size, c, rot=None, sid=None, **kw):
    """KT.fbox + per-stone UV patch (same offset on both halves of a seam stone) + tone registry."""
    n0 = len(pc.bm.faces)
    KT.fbox(pc, mat, size, c, rot=rot, **kw)
    if mat in ('rub', 'dress'):
        sid = sid or ('%s:%.3f,%.3f,%.3f' % (pc.name, c[0], c[1], c[2]))
        tone, ou, ov = tone_of(sid)
        pc.bm.faces.ensure_lookup_table()
        for f in pc.bm.faces[n0:]:
            for l in f.loops:
                l[pc.uv].uv = (l[pc.uv].uv[0] + ou, l[pc.uv].uv[1] + ov)
        STONES.setdefault(pc.name, []).append((tuple(c), tuple(size), tone, mat, ou))

def rcourse(pc, mat, R, z0, z1, yf, depth, lens, face=-1, period=2.0, xlo=-1.0, xhi=1.0, cham=0.03, segs=1, gap=0.03, proud=0.07,
            tilt=1.2, wob=0.012, cuts_over=0.7, hjit=0.02, split=0.0, skip=None, tag=''):
    """Course of stones across [xlo, xhi] repeating every `period`; the stone crossing the seam is cut on the seam planes.
    skip(xmid, ztop) -> True omits a stone (ruined variants)."""
    u0 = R.uniform(0.0, 0.5); seq = []; tot = 0.0
    while tot < period - 0.3:
        ln = R.uniform(*lens)
        if period - (tot + ln) < lens[0] * 0.8:
            ln = period - tot
        seq.append(ln); tot += ln
    if tot < period:
        seq[-1] += period - tot
    stones = []; a = xlo + u0
    for i, ln in enumerate(seq):
        stones.append(dict(a=a, b=a + ln, h=(z1 - z0) - gap + R.uniform(-hjit, hjit), pr=R.uniform(0, proud),
                           tl=R.uniform(-tilt, tilt), sd=R.random() * 90, dz=R.uniform(-0.008, 0.008),
                           sp=R.random() < split, hs=R.uniform(0.4, 0.6), i=i))
        a += ln
    for s in stones:
        ln = s['b'] - s['a'] - gap
        if skip and skip((s['a'] + s['b']) / 2 % period if False else (((s['a'] + s['b']) / 2 + 1.0) % 2.0) - 1.0, z1):
            continue
        sid = '%s:%s:%d:%.2f:%d' % (pc.name, tag, face, z0, s['i'])
        for sh in (-period, 0.0, period):
            xa, xb = s['a'] + sh, s['b'] + sh
            if xb <= xlo + 0.02 or xa >= xhi - 0.02:
                continue
            clip = (xlo, xhi) if (xa < xlo or xb > xhi) else None
            yfront = yf + face * s['pr']
            cy = yfront - face * depth / 2 if face == 1 else yfront + depth / 2
            cuts = (1 if ln > cuts_over else 0, 0, 0)
            if s['sp'] and ln > 0.45:
                h1 = s['h'] * s['hs'] - 0.015; h2 = s['h'] * (1 - s['hs']) - 0.015; zb = (z0 + z1) / 2 + s['dz'] - s['h'] / 2
                for k, (hh, zc) in enumerate(((h1, zb + h1 / 2), (h2, zb + s['h'] * s['hs'] + 0.015 + h2 / 2))):
                    fb(pc, mat, (ln, depth, hh), ((xa + xb) / 2, cy, zc), rot=Ry(s['tl']), sid=sid + 's%d' % k, cham=cham, segs=segs,
                       cuts=cuts, wob=wob, seed=s['sd'] + k, xclip=clip)
                continue
            fb(pc, mat, (ln, depth, s['h']), ((xa + xb) / 2, cy, (z0 + z1) / 2 + s['dz']), rot=Ry(s['tl']), sid=sid,
               cham=cham, segs=segs, cuts=cuts, wob=wob, seed=s['sd'], xclip=clip)

def sett(pc, x0, x1, d0, d1, zb, zt, ch, cutL, cutR, jit, sid):
    """A sett: low frustum with a chamfered top (10 tris); cut sides are flat so halves mate on the seam."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    gx = 0.006
    ax0 = x0 + (0 if cutL else gx); ax1 = x1 - (0 if cutR else gx); ad0 = d0 + 0.006; ad1 = d1 - 0.006
    bot = [bm.verts.new(p) for p in ((ax0, ad0, zb), (ax1, ad0, zb), (ax1, ad1, zb), (ax0, ad1, zb))]
    tx0 = ax0 + (0 if cutL else ch); tx1 = ax1 - (0 if cutR else ch)
    top = [bm.verts.new((px, py, zt + jit.uniform(-0.004, 0.004))) for (px, py) in ((tx0, ad0 + ch), (tx1, ad0 + ch), (tx1, ad1 - ch), (tx0, ad1 - ch))]
    bm.faces.new(top)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); bm.normal_update()
    tone, ou, ov = tone_of(sid)
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
    for f in bm.faces:
        for l in f.loops:
            l[uvl].uv = (l[uvl].uv[0] + ou, l[uvl].uv[1] + ov)
    STONES.setdefault(pc.name, []).append((((x0 + x1) / 2, (d0 + d1) / 2, (zb + zt) / 2), (x1 - x0, d1 - d0, zt - zb), tone, 'rub', ou))
    FK._merge(pc, bm, 'rub')

def rbar(pc, mat, a, b, w, t, cham=0.004):
    """Box of cross-section w x t between points a and b (authoring frame)."""
    a = Vector(a); b = Vector(b); d = b - a
    rot = Vector((1, 0, 0)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    fb(pc, mat, (d.length, t, w), (a + b) / 2, rot=rot, cham=cham)

def since(pc, n0, M, pivot=(0, 0, 0)):
    pc.bm.verts.ensure_lookup_table()
    P = Vector(pivot); m = T(*P) @ M @ T(*(-P))
    for v in pc.bm.verts[n0:]:
        v.co = m @ v.co

def spill(pc, R, n, xr, dr, zr, sz, mat='rub', tag='sp', side=0):
    """Loose rubble: n stones at x in xr, depth |d| in dr (either side per sign), z base in zr."""
    for i in range(n):
        s = (R.uniform(sz[0], sz[1]), R.uniform(sz[0], sz[1]) * 0.85, R.uniform(sz[0], sz[1]) * 0.6)
        d = R.uniform(*dr) * (side or (R.choice((-1, 1)) if dr[0] >= 0 else 1))
        c = (R.uniform(*xr), d, R.uniform(*zr) + s[2] / 2)
        rot = Rz(R.uniform(0, 180)) @ Rx(R.uniform(-14, 14)) @ Ry(R.uniform(-14, 14))
        fb(pc, mat, s, c, rot=rot, sid='%s:%s%d' % (pc.name, tag, i), cham=0.025, wob=0.02, seed=R.random() * 90)

def heights(R, z0, z1, lo, hi):
    return KT.heights(R, z0, z1, lo, hi)

# ----------------------------------------------------------------------------- garden walls
def ragged(x, base=0.62, amp=0.28, gap=(0.1, 0.75), left=(-1.01, -0.55)):
    if gap[0] < x < gap[1]:
        return -0.15
    if left[0] < x < left[1]:
        return 1.02
    return base + amp * noise.noise(Vector((x * 2.3 + 3.1, 1.7, 0.4)))

def garden_wall(name, broken=False):
    pc = FK.Piece(name); R = R_(name)
    if not broken:
        fb(pc, 'mort', (2.0, 0.40, 1.95), (0, 0, 0.175), cham=0)
        skip = None
    else:
        for i in range(8):
            xc = -0.875 + 0.25 * i; top = min(ragged(xc) - 0.06, 1.1)
            if top > -0.7:
                fb(pc, 'mort', (0.25, 0.40, top + 0.8), (xc, 0, -0.8 + (top + 0.8) / 2), cham=0)
        skip = lambda xm, zt: zt > ragged(xm) + 0.10
    for tag, (face, yf, lens, proud, wob, split, depth) in (('L', (-1, -0.20, (0.38, 0.95), 0.05, 0.02, 0.08, 0.22)),
                                                           ('Y', (1, 0.20, (0.5, 1.1), 0.03, 0.012, 0.0, 0.20))):
        RR = R_(name + tag)
        for (z0, z1) in heights(RR, -0.8, 1.0, 0.25, 0.37):
            rcourse(pc, 'rub', RR, z0, z1, yf, depth, lens, face=face, cham=0.032, segs=1, proud=proud, wob=wob, split=split, skip=skip, tag=tag)
    if not broken:   # cock-and-hen coping: tall rounded through-stones of uneven heights
        RC = R_(name + 'cap')
        rcourse(pc, 'rub', RC, 1.0, 1.34, -0.25, 0.5, (0.34, 0.56), face=-1, cham=0.07, segs=2, proud=0.0, tilt=4.0, wob=0.022, hjit=0.05,
                cuts_over=9, gap=0.025, tag='C')
    else:
        spill(pc, R, 8, (-0.85, 0.85), (0.2, 0.48), (0.0, 0.08), (0.18, 0.36))
        for i in range(3):   # fallen coping stones
            fb(pc, 'rub', (0.46, 0.5, 0.26), (R.uniform(-0.8, 0.8), R.choice((-1, 1)) * R.uniform(0.25, 0.4), 0.13),
               rot=Rz(R.uniform(0, 180)) @ Rx(R.uniform(-6, 6)), sid='%s:cap%d' % (name, i), cham=0.07, segs=1, wob=0.02, seed=i * 7.0)
        for i in range(5):   # tumbled stones left on the wall top
            xc = R.uniform(-0.85, -0.6) if i < 2 else R.uniform(0.8, 0.86)
            fb(pc, 'rub', (0.3, 0.26, 0.2), (xc, R.uniform(-0.05, 0.05), ragged(xc) + 0.1 if xc < -0.55 else ragged(xc) + 0.08),
               rot=Rz(R.uniform(0, 90)), sid='%s:top%d' % (name, i), cham=0.03, wob=0.02, seed=i * 3.0)
    return pc

def pier(name, top, cap_top, ball=False, sec=0.56):
    """Dressed pier: pinwheel courses (long stone, then cross stone), cap, optional ball."""
    pc = FK.Piece(name); R = R_(name)
    fb(pc, 'mort', (0.48, 0.48, cap_top - 0.1 + 0.8), (0, 0, -0.8 + (cap_top - 0.1 + 0.8) / 2), cham=0)
    plan = [(-0.8, -0.4)] + heights(R, -0.4, top, 0.34, 0.44)
    for i, (z0, z1) in enumerate(plan):
        w = 0.6 if i <= 1 else sec
        w -= (0.01 if i % 2 else 0.0)
        h = z1 - z0 - 0.025; zc = (z0 + z1) / 2
        rz = Rz(R.uniform(-0.9, 0.9))
        if i % 2 == 0:
            for sy in (-1, 1):
                fb(pc, 'dress', (w, w / 2 - 0.005, h), (R.uniform(-0.004, 0.004), sy * w / 4, zc), rot=rz, sid='%s:%d%d' % (name, i, sy), cham=0.035, wob=0.007, seed=R.random() * 90)
        else:
            for sx in (-1, 1):
                fb(pc, 'dress', (w / 2 - 0.005, w, h), (sx * w / 4, R.uniform(-0.004, 0.004), zc), rot=rz, sid='%s:%d%d' % (name, i, sx), cham=0.035, wob=0.007, seed=R.random() * 90)
    fb(pc, 'dress', (0.6, 0.6, cap_top - top - 0.01), (0, 0, (top + cap_top) / 2), cham=0.05, segs=2, wob=0.004, seed=3.0, sid=name + ':cap')
    if ball:
        KT.lathe(pc, 'dress', [(0.0, cap_top), (0.1, cap_top + 0.01), (0.13, cap_top + 0.05), (0.14, cap_top + 0.1), (0.12, cap_top + 0.16), (0.0, cap_top + 0.2)] if False else
                 [(0.0, cap_top - 0.005), (0.09, cap_top), (0.115, cap_top + 0.04), (0.12, cap_top + 0.08), (0.09, cap_top + 0.12), (0.0, cap_top + 0.14)],
                 (0, 0, 0), sides=8, seed=9.0, jit=0.02)
    return pc

# ----------------------------------------------------------------------------- retaining walls
def retain(name, top, bottom, breach=False):
    """2 m terrace wall: coursed rubble lane face at d -0.35, coping flush with the upper ground at `top`, low parapet above it."""
    pc = FK.Piece(name); R = R_(name)
    P0, P1, P2 = top - 0.12, top + 0.02, top + 0.30     # rubble to P0, string course to P1, parapet row to P2, cap to P2 + 0.15
    hb = lambda x: (3.45 if x < -0.78 else 0.5 + (top - 1.0) * 0.32 + (top - 0.5) * 0.28 * (0.5 + noise.noise(Vector((x * 2.1 + 9, 2.2, 1.1))))) if breach else 99
    if breach:
        hb2 = lambda x: (top + 0.5) if x < -0.78 else ((bottom + 1.3 + 0.3 * noise.noise(Vector((x * 3 + 2, 1, 1)))) if -0.2 < x < 0.7 else top * 0.8 + 0.5 * noise.noise(Vector((x * 2.1 + 9, 2.2, 1.1))))
        hb = hb2
        skip = lambda xm, zt: zt > hb(xm) + 0.1
        for i in range(8):
            xc = -0.875 + 0.25 * i; tp = min(hb(xc) - 0.05, top)
            fb(pc, 'mort', (0.25, 0.5, tp - bottom), (xc, 0.05, bottom + (tp - bottom) / 2), cham=0)
    else:
        skip = None
        fb(pc, 'mort', (2.0, 0.5, top - bottom), (0, 0.05, bottom + (top - bottom) / 2), cham=0)
        fb(pc, 'rub', (2.0, 0.04, top - bottom), (0, 0.33, bottom + (top - bottom) / 2), cham=0, sid=name + ':back')   # buried back face
    RR = R_(name + 'face')
    first = True
    for (z0, z1) in heights(RR, bottom, P0, 0.26, 0.40):
        if first:    # footing course: heavier stones
            rcourse(pc, 'rub', RR, z0, z1, -0.28, 0.30, (0.6, 1.2), cham=0.034, segs=1, proud=0.07, wob=0.02, skip=skip, tag='F'); first = False
            continue
        rcourse(pc, 'rub', RR, z0, z1, -0.28, 0.30, (0.45, 1.05), cham=0.034, segs=1, proud=0.07, wob=0.022, split=0.2, skip=skip, tag='R')
    if not breach or True:
        rcourse(pc, 'dress', R_(name + 's'), P0, P1, -0.30, 0.34, (0.9, 1.3), cham=0.03, segs=1, proud=0.05, tilt=0.5, wob=0.006, skip=skip, tag='S')   # string course
        rcourse(pc, 'rub', R_(name + 'p'), P1, P2, -0.28, 0.28, (0.4, 0.95), cham=0.03, segs=1, proud=0.04, wob=0.015, skip=skip, tag='P')
        rcourse(pc, 'dress', R_(name + 'c'), P2, P2 + 0.15, -0.35, 0.34, (0.6, 1.0), cham=0.05, segs=2, proud=0.0, tilt=0.3, wob=0.006, cuts_over=9, skip=skip, tag='K')
    if not breach:   # flush walkway coping behind the parapet
        RS = R_(name + 'w')
        for (a, b) in ((-1.0, -0.2), (-0.2, 0.55), (0.55, 1.0)):
            fb(pc, 'dress', (b - a - 0.02, 0.40, 0.16), ((a + b) / 2, 0.15, top - 0.08), cham=0.025, wob=0.004, seed=a * 9, sid=name + ':w%.1f' % a)
    else:
        spill(pc, R, 14, (-0.9, 0.95), (0.45, 1.3), (0.0, 0.12), (0.2, 0.42), tag='fan', side=-1)
        for i in range(3):
            fb(pc, 'dress', (0.7, 0.34, 0.22), (R.uniform(-0.6, 0.8), -R.uniform(0.7, 1.15), 0.11),
               rot=Rz(R.uniform(60, 120)) @ Rx(R.uniform(-5, 5)), sid='%s:fc%d' % (name, i), cham=0.04, segs=2, wob=0.02, seed=i * 5.0)
        # slumped soil on the upper side showing through the gap
        fb(pc, 'earth', (0.9, 0.4, 0.45), (0.2, 0.1, bottom + 1.55), cham=0.1, segs=2, wob=0.06, seed=4.0)
    return pc

BUT = dict(y0=-1.0, y1=-0.62, z0=-1.0, z1=2.4)
def but_front(z):
    return BUT['y0'] + (BUT['y1'] - BUT['y0']) * (z - BUT['z0']) / (BUT['z1'] - BUT['z0'])

def p_buttress():
    pc = FK.Piece('RetainButtress'); R = R_('buttress'); zc = 2.35
    KT.hull(pc, 'mort', [(sx, -0.35, -1.0) for sx in (-0.46, 0.46)] + [(sx, but_front(-1.0) + 0.08, -1.0) for sx in (-0.46, 0.46)] +
            [(sx, but_front(zc) + 0.1, zc) for sx in (-0.46, 0.46)] + [(sx, -0.35, zc) for sx in (-0.46, 0.46)])
    i = 0
    for (z0, z1) in [(-1.0, -0.55)] + heights(R, -0.55, zc, 0.30, 0.40):
        zm = (z0 + z1) / 2; yf = but_front(zm); h = z1 - z0 - 0.025; d = -0.35 - yf
        qa, qb = (0.5, 0.3) if i % 2 == 0 else (0.3, 0.5)
        qb = min(qb, d - 0.04)
        for sx in (-1, 1):
            fb(pc, 'dress', (qa, qb, h), (sx * (0.51 - qa / 2), yf + qb / 2 - 0.005, zm), rot=Rz(R.uniform(-1.2, 1.2)), sid='b%d%d' % (i, sx),
               cham=0.035, wob=0.008, seed=R.random() * 90)
        mw = 1.02 - 2 * qa - 0.02
        fb(pc, 'rub', (mw, 0.2, h - R.uniform(0, 0.03)), (R.uniform(-0.01, 0.01), yf + 0.1 - R.uniform(0, 0.02), zm), rot=Rz(R.uniform(-1.0, 1.0)),
           sid='bm%d' % i, cham=0.03, wob=0.018, seed=R.random() * 90)
        i += 1
    yl = but_front(zc) - 0.02
    KT.hull(pc, 'dress', [(sx, y, z) for sx in (-0.5, 0.5) for (y, z) in ((-0.35, 2.35), (yl, 2.35), (yl, 2.45), (-0.37, 2.6), (-0.35, 2.6))], cham=0.03)
    return pc

# ----------------------------------------------------------------------------- street furniture
def p_step():
    pc = FK.Piece('LaneStep'); R = R_('step')
    fb(pc, 'mort', (2.0, 0.55, 0.87), (0, 0.275, -0.365), cham=0)       # hidden body, z -0.8..0.07
    rows = [(0.0, 0.19, -0.05, 0.30, 0.42), (0.19, 0.38, 0.07, 0.26, 0.34), (0.38, 0.55, 0.07, 0.26, 0.34)]
    for ri, (d0, d1, zb, wlo, whi) in enumerate(rows):
        RR = R_('steprow%d' % ri)
        seq = []; tot = 0.0
        while tot < 2.0 - 0.2:
            w = RR.uniform(wlo, whi)
            if 2.0 - (tot + w) < wlo * 0.8:
                w = 2.0 - tot
            seq.append(w); tot += w
        seq[-1] += 2.0 - tot
        a = -1.0 + RR.uniform(0.0, 0.2); cells = []
        for k, w in enumerate(seq):
            cells.append((a, a + w, k)); a += w
        for (xa, xb, k) in cells:
            xc = (xa + xb) / 2
            dip = 0.014 * math.exp(-(xc / 0.6) ** 2)
            for sh in (-2.0, 0.0, 2.0):
                x0, x1 = xa + sh, xb + sh
                if x1 <= -0.98 or x0 >= 0.98:
                    continue
                cl, cr = x0 < -1.0, x1 > 1.0
                sett(pc, max(x0, -1.0), min(x1, 1.0), d0, d1, zb, 0.16 - dip - RR.uniform(0, 0.006), 0.012, cl, cr, RR, 'step%d_%d' % (ri, k))
    return pc

def p_kerb():
    pc = FK.Piece('LaneKerb'); R = R_('kerb')
    for (a, b) in ((-1.0, -0.32), (-0.32, 0.36), (0.36, 1.0)):
        fb(pc, 'dress', (b - a - 0.012, 0.30, 0.42), ((a + b) / 2, 0, -0.09), rot=Ry(R.uniform(-0.6, 0.6)), sid='kerb%.2f' % a, cham=0.04, segs=1, wob=0.008, seed=a * 11)
    return pc

GUT = [(-0.2, -0.25), (-0.2, 0.02), (-0.1, 0.02), (-0.075, -0.03), (-0.04, -0.07), (0.04, -0.07), (0.075, -0.03), (0.1, 0.02), (0.2, 0.02), (0.2, -0.25)]
def p_gutter(choked=False):
    pc = FK.Piece('GutterChoked' if choked else 'Gutter'); R = R_('gutter')
    cuts = (-1.0, -0.52, 0.0, 0.5, 1.0)
    for a, b in zip(cuts, cuts[1:]):
        KT.extrude_x(pc, 'dress', GUT, a + 0.006, b - 0.006, nx=1)
    if choked:   # silt with a few stones
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        nx = 6; rows = []
        for j in (0, 1):
            row = []
            for i in range(nx + 1):
                x = -0.97 + 1.94 * i / nx
                z = -0.04 + 0.05 * noise.noise(Vector((x * 3, j, 4.0))) + 0.012
                row.append(bm.verts.new((x, -0.095 + 0.19 * j, z - 0.06 * (1 - math.sin(math.pi * j)) * 0)))
            rows.append(row)
        for i in range(nx):
            f = bm.faces.new((rows[0][i], rows[0][i + 1], rows[1][i + 1], rows[1][i]))
            for l in f.loops:
                l[uvl].uv = (l.vert.co.x / TILE, l.vert.co.y / TILE)
        bm.normal_update(); FK._merge(pc, bm, 'earth')
        spill(pc, R, 2, (-0.5, 0.5), (0.0, 0.05), (-0.06, -0.03), (0.07, 0.12), tag='gs')
    return pc

def p_grate():
    pc = FK.Piece('GutterGrate'); R = R_('grate')
    fb(pc, 'mort', (0.6, 0.6, 0.02), (0, 0, -0.19), cham=0)
    for (sx, sy, w, d) in ((-1, 0, 0.1, 0.6), (1, 0, 0.1, 0.6), (0, -1, 0.4, 0.1), (0, 1, 0.4, 0.1)):
        fb(pc, 'dress', (w - 0.01 if sx else w, d - 0.01 if sy == 0 and sx == 0 else d, 0.22), (sx * 0.25, sy * 0.25, -0.09), cham=0.015, wob=0.004, seed=sx + sy * 3 + 5, sid='gr%d%d' % (sx, sy))
    for k in range(6):
        fb(pc, 'iron', (0.024, 0.4, 0.03), (-0.15 + 0.06 * k, 0, -0.005), cham=0)
    fb(pc, 'iron', (0.4, 0.024, 0.03), (0, 0, -0.04), cham=0)
    return pc

def lantern(name, broken=False):
    pc = FK.Piece(name); R = R_(name)
    fb(pc, 'iron', (0.14, 0.03, 0.5), (0, -0.015, 0.05), cham=0.007)
    for z in (-0.12, 0.22):
        fb(pc, 'iron', (0.05, 0.025, 0.05), (0, -0.04, z), cham=0.006)
    n0 = len(pc.bm.verts)
    rbar(pc, 'iron', (0, -0.03, 0.2), (0, -0.47, 0.2), 0.04, 0.04, 0.005)
    rbar(pc, 'iron', (0, -0.03, -0.08), (0, -0.34, 0.19), 0.03, 0.03, 0.004)
    fb(pc, 'iron', (0.035, 0.1, 0.035), (0, -0.475, 0.2), cham=0.004)      # scroll end
    fb(pc, 'iron', (0.025, 0.025, 0.07), (0, -0.46, 0.155), cham=0.003)     # hook
    n1 = len(pc.bm.verts)
    cx = -0.46
    fb(pc, 'iron', (0.27, 0.27, 0.035), (0, cx, -0.2), cham=0.006)         # base plate
    hull = [(sx * 0.14, cx + sy * 0.14, 0.1) for sx in (-1, 1) for sy in (-1, 1)] + [(sx * 0.04, cx + sy * 0.04, 0.18) for sx in (-1, 1) for sy in (-1, 1)]
    KT.hull(pc, 'iron', hull, cham=0.006)                                  # hood
    fb(pc, 'iron', (0.05, 0.05, 0.04), (0, cx, 0.2), cham=0.006)           # ring
    KT.hull(pc, 'iron', [(sx * 0.1, cx + sy * 0.1, -0.22) for sx in (-1, 1) for sy in (-1, 1)] + [(0, cx, -0.31)], cham=0.004)   # drip foot
    if not broken:
        fb(pc, 'glit', (0.17, 0.17, 0.3), (0, cx, -0.05), cham=0.01)
    for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
        if broken and k == 2:
            continue
        fb(pc, 'iron', (0.03, 0.03, 0.32), (sx * 0.095, cx + sy * 0.095, -0.05), cham=0.004)
    if broken:
        since(pc, n1, Rx(-10) @ Ry(8), (0, cx, 0.2))      # cage hangs crooked
        since(pc, n0, Rx(24), (0, -0.03, 0.2))            # arm sagged
    return pc

def gate_leaf(name, broken=False):
    pc = FK.Piece(name); R = R_(name)
    for i in range(7):
        if broken and i == 4:
            continue
        h = 1.2 - R.uniform(0, 0.03) - (0.5 if (broken and i == 5) else 0.0)
        fb(pc, 'paint', (0.186, 0.035, h), (0.1 + 0.2 * i, -0.0125, 0.05 + h / 2), rot=Ry(R.uniform(-0.4, 0.4)), cham=0.008, wob=0.002, seed=i * 3.0)
    for z in (0.2, 0.65, 1.1):
        fb(pc, 'oak', (1.38, 0.032, 0.1), (0.7, 0.0225, z), cham=0.005)
    rbar(pc, 'oak', (0.1, 0.0225, 0.22), (1.28, 0.0225, 1.08), 0.09, 0.03, 0.004)
    for z in ((0.25, 1.05) if not broken else (1.05,)):
        fb(pc, 'iron', (0.55, 0.012, 0.07), (0.275, -0.036, z), cham=0.003)
        fb(pc, 'iron', (0.05, 0.05, 0.13), (0.02, -0.015, z), cham=0.005)
    fb(pc, 'iron', (0.11, 0.012, 0.045), (1.3, -0.036, 0.65), cham=0.003)
    if broken:
        since(pc, 0, Ry(15) @ Rz(7))
    return pc

PIECES = [
    ('GardenWall', lambda: garden_wall('GardenWall')), ('GardenWallBroken', lambda: garden_wall('GardenWallBroken', True)),
    ('GardenWallEnd', lambda: pier('GardenWallEnd', 1.40, 1.55)),
    ('RetainWall', lambda: retain('RetainWall', 3.0, -1.0)), ('RetainWallLow', lambda: retain('RetainWallLow', 1.5, -0.6)),
    ('RetainWallBreach', lambda: retain('RetainWallBreach', 3.0, -1.0, True)), ('RetainButtress', p_buttress),
    ('LaneStep', p_step), ('LaneKerb', p_kerb), ('Gutter', p_gutter), ('GutterChoked', lambda: p_gutter(True)), ('GutterGrate', p_grate),
    ('WallLantern', lambda: lantern('WallLantern')), ('WallLanternBroken', lambda: lantern('WallLanternBroken', True)),
    ('GatePier', lambda: pier('GatePier', 1.85, 1.97, True)),
    ('GateLeaf', lambda: gate_leaf('GateLeaf')), ('GateLeafBroken', lambda: gate_leaf('GateLeafBroken', True)),
]
BUDGET = {'GardenWall': 2500, 'GardenWallBroken': 2500, 'GardenWallEnd': 800, 'RetainWall': 4000, 'RetainWallLow': 2500, 'RetainWallBreach': 4500,
          'RetainButtress': 1500, 'LaneStep': 250, 'LaneKerb': 200, 'Gutter': 300, 'GutterChoked': 300, 'GutterGrate': 400, 'WallLantern': 1500,
          'WallLanternBroken': 1500, 'GatePier': 1000, 'GateLeaf': 1200, 'GateLeafBroken': 1200}

# ----------------------------------------------------------------------------- wear (authoring frame)
def sstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)

TOPS = {'RetainWall': 3.0, 'RetainWallLow': 1.5, 'RetainWallBreach': 3.0}
def stone_tone(name, p):
    best = None; bv = 1e9
    for (c, s, t, m, ou) in STONES.get(name, ()):
        if abs(p.x - c[0]) <= s[0] / 2 + 0.012 and abs(p.y - c[1]) <= s[1] / 2 + 0.012 and abs(p.z - c[2]) <= s[2] / 2 + 0.012:
            v = s[0] * s[1] * s[2]
            if v < bv:
                bv = v; best = (t, ou)
    return best

def wear_at(name, p, n, mname):
    if mname in ('LK_Earth', 'LK_GlassLit'):
        return 0.0, 0.0, 0.0
    nz = KT.pnoise(p); st = KT.pnoise(p, 3.1, 4.0); nzp = noise.noise(p * 1.7)
    r = 0.04 + 0.07 * max(0.0, nz) + 0.14 * max(0.0, noise.noise(p * 3.9 + Vector((3, 1, 7)))); g = 0.0; b = 0.0
    up = n.z > 0.5
    top = TOPS.get(name, 1.35)
    if name.startswith(('GardenWall', 'Retain', 'GatePier')):
        h = 0.55 + 0.3 * nz + 0.12 * st
        if p.z < h:
            r = max(r, 0.9 - 0.3 * max(0.0, p.z) / h)                    # damp foot
            g = max(g, 0.62 * max(0.0, nz * 1.6 + 0.25) * (1 if n.y < 0.3 else 0.6))
        elif p.z < h + 0.5:
            r = max(r, 0.3 * (1 - (p.z - h) / 0.5) * (0.7 + 0.5 * st))
        if up and p.z > top - 0.45 and name.startswith('Retain'):
            g = max(g, 0.8 + 0.2 * (nz + 0.2)); r = max(r, 0.2)
        if name.startswith('GardenWall') and not name.endswith('End') and p.z > 0.95:
            g = max(g, (0.85 + 0.15 * (nz + 0.2)) if up else 0.3 * max(0.0, nzp + 0.2)); r = max(r, 0.2)
        if name == 'GatePier' or name == 'GardenWallEnd':
            if p.z > 1.45 and up:
                g = max(g, 0.4 * max(0.0, nzp + 0.2)); r = max(r, 0.2)
        if name.startswith('Retain'):
            ph = top - 0.3
            if ph - 0.5 < p.z < ph:
                r = max(r, 0.35 * sstep(ph - 0.5, ph, p.z) * (0.7 + 0.5 * st))
    if name == 'RetainButtress':
        if p.z < 0.7 + 0.2 * nz:
            r = max(r, 0.85 - 0.3 * max(0.0, p.z)); g = max(g, 0.3 * max(0.0, nz + 0.2))
        if up and p.z > 2.3:
            g = max(g, 0.6); r = max(r, 0.25)
    if name == 'LaneStep':
        if up and p.z > 0.1:
            b = 0.9 * math.exp(-(p.x / 0.55) ** 2) * (0.75 + 0.25 * nzp) * (1.0 if p.y < 0.4 else 0.75)
            r = 0.12 + 0.35 * sstep(0.7, 1.0, abs(p.x)); g = 0.3 * sstep(0.75, 1.0, abs(p.x)) * max(0.0, nzp + 0.4)
            if p.y < 0.03:
                b = max(b, 0.7)
        else:
            r = 0.2 + 0.4 * max(0.0, -n.z) + (0.5 if p.z < -0.3 else 0.0)
    if name == 'LaneKerb':
        r = 0.2 + 0.4 * max(0.0, 1 - (p.z + 0.3) / 0.25)
        if up:
            b = 0.5 * (0.7 + 0.3 * nzp); g = 0.25 * max(0.0, nzp)
    if name.startswith('Gutter'):
        if abs(p.y) < 0.11 and p.z < 0.0:
            r = 0.75 + 0.2 * nzp
        else:
            r = 0.25 + 0.2 * max(0.0, nz); g = 0.3 * max(0.0, nzp) * (1 if up else 0.4)
        if name == 'GutterGrate' and mname == 'LK_Iron':
            r = 0.45
    if name.startswith('WallLantern'):
        r = 0.3 + 0.25 * max(0.0, nzp) + (0.3 if p.z > 0.1 else 0.0); b = 0.0
    if name.startswith('GateLeaf'):
        i = int(max(0.0, p.x) / 0.2)
        bd = FK.rng('board%d' % i).random()
        r = 0.12 + 0.3 * bd + (0.45 * (1 - p.z / 0.45) if p.z < 0.45 else 0.0) + 0.1 * max(0.0, nz)
        g = 0.3 * (1 - p.z / 0.3) * max(0.0, nzp + 0.5) if p.z < 0.3 else 0.0
        if mname == 'LK_Iron':
            r = 0.4
        if up:
            r = max(r, 0.3)
    # per-stone value variation: dark end through grime (R), light end through polish (B)
    tt = stone_tone(name, p)
    if tt and mname in ('LK_Rubble', 'LK_Dressed'):
        t, mo = tt; amp = 1.0 if mname == 'LK_Rubble' else 0.45
        if t < 0:
            r = min(1.0, r + (-t) * 0.5 * amp)
        else:
            b = min(1.0, b + t * 0.5 * amp)
        if mo > 0.68 and mname == 'LK_Rubble' and name != 'LaneStep' and n.z > -0.2:
            g = max(g, 0.62 * (0.55 + 0.45 * nzp + 0.2 * n.z))      # a mossy stone in three
    return min(1.0, r), min(1.0, g), min(1.0, b)

def write_wear(ob):
    me = ob.data
    if 'wear' in me.color_attributes:
        me.color_attributes.remove(me.color_attributes['wear'])
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    mats = [m.name for m in me.materials]
    inv = lambda v: Vector((-v.x, -v.y, v.z))
    for poly in me.polygons:
        n = inv(poly.normal); mname = mats[poly.material_index]
        for li in poly.loop_indices:
            p = inv(me.vertices[me.loops[li].vertex_index].co)
            attr.data[li].color = (*wear_at(ob['piece'], p, n, mname), 1.0)

# ----------------------------------------------------------------------------- build
def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def to_object(pc, parent):
    bmesh.ops.transform(pc.bm, matrix=Rz(180), verts=pc.bm.verts[:])
    me = bpy.data.meshes.new(pc.name)
    pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
    ob['piece'] = pc.name; ob.parent = parent
    bpy.context.view_layer.update()
    write_wear(ob)
    return ob

def build():
    root = bpy.data.objects.new('LaneKit', None); bpy.context.scene.collection.objects.link(root)
    obs = {}; total = 0
    for name, fn in PIECES:
        ob = to_object(fn(), root); obs[name] = ob; t = tris(ob); total += t
        bb = [Vector(c) for c in ob.bound_box]
        ux = sorted([-c.x for c in bb]); uy = sorted([c.z for c in bb]); uz = sorted([-c.y for c in bb])
        print('  %-18s %5d tris (budget %4d)%s  %s\n      Unity extent x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f' %
              (name, t, BUDGET[name], ' OVER' if t > BUDGET[name] else '', ', '.join(m.name for m in ob.data.materials), ux[0], ux[-1], uy[0], uy[-1], uz[0], uz[-1]))
    print('LaneKit total tris: %d (cap 30000)' % total)
    return root, obs

# ----------------------------------------------------------------------------- review scene
W = KT.W
def place(obs, name, upos, run=(1, 0, 0), up=(0, 1, 0), sx=1.0):
    x = Vector(run).normalized(); y = (Vector(up) - x * Vector(up).dot(x)).normalized(); z = x.cross(y)
    Mu = Matrix((x, y, z)).transposed()
    C = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))
    ob = bpy.data.objects.new(name + '_i', obs[name].data); ob['piece'] = name
    bpy.context.scene.collection.objects.link(ob)
    ob.matrix_world = T(*W(*upos)) @ (C @ Mu @ C.inverted()).to_4x4() @ Matrix.Diagonal((sx, 1, 1, 1))

def quad(sc, name, pts, mat, wear=(0.2, 0, 0), scale=1.0):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    f = bm.faces.new([bm.verts.new(W(*p)) for p in pts]); bm.normal_update()
    if f.normal.z < 0:
        f.normal_flip()
    for l in f.loops:
        l[uvl].uv = ((l.vert.co.x + (l.vert.co.y if abs(f.normal.z) < 0.5 else 0)) / TILE * scale, (l.vert.co.z if abs(f.normal.z) < 0.5 else l.vert.co.y) / TILE * scale)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob)
    a = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    for d in a.data:
        d.color = (*wear, 1.0)

SLOPE = 0.16 / 0.55
def gy(z):
    return 0.0 if z < 0 else (SLOPE * z if z < 6.6 else 1.92)

def build_assembly(obs):
    sc = bpy.context.scene
    for o in obs.values():
        o.hide_render = True; o.hide_viewport = True
    cob = FK.tile_mat('FK_Cobble', 'B1_town/cobble'); gra = FK.tile_mat('LK_Verge', 'B1_town/meadow_grass')
    # ground: lane, verges (left/right), terraces behind the retaining walls
    for (xa, xb, wr, m) in ((-2.6, 2.6, (0.25, 0, 0), cob), (-14, -2.6, (0.05, 0.15, 0), gra), (2.6, 14, (0.05, 0.15, 0), gra)):
        quad(sc, 'g', [(xa, 0, -10), (xb, 0, -10), (xb, 0, 0), (xa, 0, 0)], m, wr)
        quad(sc, 'g', [(xa, 0, 0), (xb, 0, 0), (xb, 1.92, 6.6), (xa, 1.92, 6.6)], m, wr)
        quad(sc, 'g', [(xa, 1.92, 6.6), (xb, 1.92, 6.6), (xb, 1.92, 22), (xa, 1.92, 22)], m, wr)
    for s in (-1, 1):
        x0, x1 = (3.2 * s + 0.36 * s, 14 * s)
        quad(sc, 'terr', [(x0, 4.92, 8.5), (x1, 4.92, 8.5), (x1, 4.92, 22), (x0, 4.92, 22)], gra, (0.05, 0.15, 0))
        quad(sc, 'face', [(x0, 1.92, 8.5), (x1, 1.92, 8.5), (x1, 4.92, 8.5), (x0, 4.92, 8.5)], FK.tile_mat('LK_Bank', 'B1_town/packed_soil'), (0.15, 0.05, 0))
    # the flight: 12 LaneSteps 5 m wide (2.5 steps scaled across)
    for k in range(12):
        for cx in (-1.667, 0.0, 1.667):
            place(obs, 'LaneStep', (cx, 0.16 * k, 0.55 * k), sx=0.8333)
    pitch = Vector((0, SLOPE, 1)).normalized()
    for cx in (-2.68, 2.68):    # kerb cheeks tilted to the slope
        for k in range(3):
            zc = 1.0 + 2.1 * k / 1.0
            zz = 1.0 + 2.1 * k * 1.0
            place(obs, 'LaneKerb', (cx, SLOPE * (1.05 + 2.1 * k) + 0.12, 1.05 + 2.1 * k), run=(0, SLOPE, 1), up=(0, 1, -SLOPE))
    # gutter along the right edge: flat run then the slope; grate at the foot
    for k in range(3):
        place(obs, 'Gutter' if True else 'Gutter', (3.1 if False else 2.95, 0, -5.0 + 2.0 * k) if False else (2.95, 0.0, -4.0 + 2.0 * k), run=(0, 0, 1))
    for k in range(3):
        place(obs, 'Gutter', (2.95, SLOPE * (1.05 + 2.1 * k), 1.05 + 2.1 * k), run=(0, SLOPE, 1), up=(0, 1, -SLOPE))
    place(obs, 'GutterGrate', (2.95, 0.0, -5.3))
    for k in range(3):
        place(obs, 'GutterChoked', (-2.95, 0.0, -4.0 + 2.0 * k), run=(0, 0, 1))
    # left side (Rebuilt): garden wall run stepping down beside the flight, gate, lantern
    for s, ruined in ((-1, False), (1, True)):
        xw = 3.4 * s
        run = (0, 0, 1) if s < 0 else (0, 0, -1)
        names = ['GardenWall', 'GardenWallBroken', 'GardenWall', 'GardenWallBroken', 'GardenWall', 'GardenWallBroken', 'GardenWall'] if ruined else ['GardenWall'] * 7
        zs = [-5, -3, -1, 1, 5, 7, 9]
        for nm, zc in zip(names, zs):
            place(obs, nm, (xw, gy(zc), zc), run=run)
        place(obs, 'GardenWallEnd', (xw, gy(-6.2), -6.2))
        place(obs, 'GatePier', (xw, gy(2.0), 2.0)); place(obs, 'GatePier', (xw, gy(4.0), 4.0))
        gz = 2.3 if s < 0 else 3.7
        place(obs, 'GateLeafBroken' if ruined else 'GateLeaf', (xw, gy(3.0), gz), run=run)
        # lantern on the lane face of the first pier (y 1.8 above its base)
        lane_dx = 0.3 if s < 0 else -0.3
        place(obs, 'WallLanternBroken' if ruined else 'WallLantern', (xw + lane_dx, gy(2.0) + 1.7, 2.0), run=run)
        # retaining wall: 3 modules, buttress, lantern
        for k in range(3):
            zc = 9.5 + 2.0 * k
            nm = ('RetainWallBreach' if k == 1 else 'RetainWall') if ruined else 'RetainWall'
            place(obs, nm, (3.2 * s, 1.92, zc), run=run)
        place(obs, 'RetainButtress', (3.2 * s - 0.0, 1.92, 9.5 if ruined else 13.5), run=run)
        place(obs, 'WallLanternBroken' if ruined else 'WallLantern', (3.2 * s + (-0.35 * s) * -1 * -1 + 0.0 if False else 3.2 * s - 0.35 * s, 1.92 + 2.2, 11.5 if not ruined else 13.5), run=run)

def look(cam, frm, to):
    cam.location = frm; cam.rotation_euler = (to - frm).to_track_quat('-Z', 'Y').to_euler()

CAMS = {   # Unity (from, to, vertical fov)
    'lane': ((0.8, 3.3, -9.5), (-0.2, 2.9, 8.0), 55),
    'flight': ((7.5, 3.6, -2.5), (-0.5, 1.0, 5.0), 48),
    'close': ((-0.2, 2.2, -0.2), (-3.4, 1.6, 3.2), 42),
    'ruined': ((-0.5, 3.1, 5.0), (3.4, 2.9, 11.0), 62),
}

def setup_light(sc):
    FK.RRES = RRES; FK.SAMPLES = SAMPLES
    cam = FK.review_lights(sc)
    sun = bpy.data.objects['Sun']
    sun.rotation_euler = (math.radians(38), 0, math.radians(215))
    cam.data.clip_start = 0.05; cam.data.clip_end = 200
    return cam

def review(root, obs):
    sc = bpy.context.scene
    cam = setup_light(sc)
    views = FK.arg('--views', 'lane,flight,close,ruined').split(',')
    if '--assemble' in argv:
        build_assembly(obs)
    else:
        for o in obs.values():
            o.hide_render = True; o.hide_viewport = True
        x = -12.0
        for nm, _ in PIECES:
            place(obs, nm, (x, 0.0, 0.0)); x += 2.4
        quad(sc, 'g', [(-14, 0, -4), (32, 0, -4), (32, 0, 4), (-14, 0, 4)], FK.tile_mat('FK_Cobble', 'B1_town/cobble'))
        CAMS['lane'] = ((6.0, 2.2, -9.0), (6.0, 0.8, 0.0), 60)
        views = ['lane']
    for v in views:
        frm, to, fov = CAMS[v]
        look(cam, W(*frm), W(*to)); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, 'lane_%s.png' % v)
        bpy.ops.render.render(write_still=True); print('rendered', v)

def export_kit(root, ed):
    os.makedirs(ed, exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(o == root or o.parent == root)
    bpy.ops.export_scene.fbx(filepath=os.path.join(ed, 'LaneKit.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                             mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR', path_mode='STRIP')
    print('exported', os.path.join(ed, 'LaneKit.fbx'))

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, obs = build()
    if '--tris' in argv:
        sys.exit(0)
    if '--export' in argv:
        export_kit(root, os.path.abspath(FK.arg('--export-dir', OUT)))
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'lane.blend'))
    if '--no-render' not in argv:
        review(root, obs)
