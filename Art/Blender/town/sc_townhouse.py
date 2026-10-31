"""Townhouse: home kit family 3, the tall jettied townhouse (M class, mass-production spec 5.4 #3).

Three storeys, narrow, gable to the street, each upper storey jettied out over the one below, leaning a little.
Built from the B4 facade kit pieces (Art/Blender/town/sc_facade_kit.py, imported as a library: member, window,
wall_panel, kit_door, kit_shutters, kit_roof, kit_chimney, kit_flowerbox, plinth_run, write_wear, the shared B1
tileables and the linear `wear` colour attribute), so it sits beside the approved corner facade.

Seeded variation per variant (VARIANTS -> cfg()): width within M (4.7-5.7 m), depth (so the roof stays inside the
8 m foundation), storey heights, jetty depth (0.32-0.50 m), roof pitch (52-60 deg), plaster and shutter colours from
a palette, chimney side and place, flower boxes, wear amount, a gentle lean (x and outward tip of the upper storeys).

All four sides are framed and detailed (four turns, spec 5.2): front (door, jetties, shutters), both sides (bays,
windows, braces, floor beams at the jetty steps), back (windows, braces), both gables (king post, collar, braces),
both roof slopes with tile courses, rafter tails and rolled eaves.

Export (spec 5.2), one FBX per variant, root <Variant> with children:
  Frame          every kit piece except the parts, joined (multi-material, `wear` attribute)
  Part_Door      the planked door; object origin = SOCKET_Door
  Part_Shutters  every shutter on every side; object origin = SOCKET_Shutters
  SOCKET_Door, SOCKET_Shutters   empties on the frame where the parts attach (SOCKET_Door marks the door side)
  FOOTPRINT      flat box (0.1 m) over the building body: collider and NPC-block shape
Axes: metres, Blender z up, front -Y; pivot = foundation centre at ground (the body is centred on it). Export FBX
-Z forward, Y up, so the front comes in as Unity local +Z like the B4 facade (builder turns the art 180).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_townhouse.py -- --out <scratch>
      [--variant Townhouse_A|Townhouse_B|Townhouse_C|all] [--export] [--no-render] [--views fl,fr,bl,br,front]
      [--rres 960] [--samples 32] [--frame-only]   (--frame-only: render with the parts hidden)
"""
import sys, os, math, random, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as fk

import inspect

def repatch(fn, subs):
    """Re-define a kit function with a few text edits (tris budget), in the kit's own namespace so every kit piece uses it."""
    src = inspect.getsource(getattr(fk, fn))
    for a, b in subs:
        assert a in src, (fn, a)
        src = src.replace(a, b)
    exec(compile(src, fk.__file__, 'exec'), fk.__dict__)

# Budget (35k per house, all four sides detailed): longer timber segments, flat lead cames (2 tris, not a 12-tri box),
# side/back windows without mullion and with a wider lattice, single-segment plinth chamfers, a lighter roof.
repatch('member', [('cuts = max(0, int(L / 0.6))', 'cuts = max(0, int(L / 1.6))')])
repatch('window', [('def window(pc, plane, x0, x1, z0, z1, k, sill_out=0.10):', 'def window(pc, plane, x0, x1, z0, z1, k, sill_out=0.10, lite=False):'),
                   ("    member(pc, 'oak', (xm, yc + 0.02, z0)", "    if not lite: member(pc, 'oak', (xm, yc + 0.02, z0)"),
                   ("    member(pc, 'oak', (x0, yc + 0.02, zt)", "    if not lite: member(pc, 'oak', (x0, yc + 0.02, zt)"),
                   ('    s = 0.16\n', '    s = 0.30 if lite else 0.22\n'),
                   ("            add_box(pc, 'iron', (ln, 0.010, 0.012), xf, cham=0.0, long_axis=0, seed=i)",
                    "            pa = Vector((a[0], gy - 0.008, a[1])); pb = Vector((b[0], gy - 0.008, b[1])); dd = (pb - pa).normalized()\n"
                    "            nn = Vector((-dd.z, 0, dd.x)) * 0.007\n"
                    "            quad_grid(pc, 'iron', [pa - nn, pb - nn, pb + nn, pa + nn], 1, 1, uv_fn=lambda c: (c.x, c.z))")])
repatch('plinth_run', [('cham=0.05, segs=2, cuts=(1, 0, 0) if face == \'front\' else (0, 1, 0)', 'cham=0.05, segs=1, cuts=(0, 0, 0)'),
                       ('ln = min(R.uniform(0.55, 0.95), b - x)', 'ln = min(R.uniform(0.75, 1.25), b - x)')])
repatch('kit_leanto', [('x0, x1, y0, y1 = W + 0.02, W + 1.55, 2.75, 5.35', 'x0, x1, y0, y1 = W + 0.02, W + 1.0, 2.75, 5.35'),
                       ('for row in range(6):', 'for row in range(5):')])   # inside the 8 m foundation; a lighter woodpile
repatch('kit_roof', [('segs = 10; rr = 0.11', 'segs = 6; rr = 0.11'), ('nt = nx * 2 ', 'nt = nx '), ('segs = 8; r = 0.15', 'segs = 5; r = 0.15'),
                     ('for i in range(13):', 'for i in range(9):'), ('for i in range(nx * 2 + 1):', 'for i in range(nx + 1):'),
                     ('x = X0 + (X1 - X0) * i / (nx * 2)', 'x = X0 + (X1 - X0) * i / nx'), ('nx, 10, push=deck', 'nx, 6, push=deck'), ('i * (2 * W - 0.6) / 12', 'i * (2 * W - 0.6) / 8')])

argv = fk.argv
arg = fk.arg
OUT = fk.OUT

VARIANTS = {'Townhouse_A': 0x7A11, 'Townhouse_B': 0x7B22, 'Townhouse_C': 0x7C33}

# palettes (linear plaster tint multiplier, shutter/door paint hex) - PHASE1 3.1 warm muted storybook range
PLASTER = [(1.0, 0.93, 0.82), (1.0, 0.88, 0.80), (0.95, 0.96, 0.97), (1.0, 0.95, 0.86), (1.0, 0.84, 0.70)]
PAINT = ['#4a6a48', '#6a2e29', '#64807a', '#9c7f4c', '#3e5670', '#7a4a5c']
COMBOS = [((0.95, 0.96, 0.97), '#3e5670'),   # cool lime white, slate blue
          ((1.0, 0.93, 0.82), '#7a4a5c'),    # warm cream, plum
          ((1.0, 0.78, 0.58), '#4a6a48'),    # ochre wash, sage-forest green
          ((1.0, 0.88, 0.80), '#6a2e29')]    # faint rose, oxblood

def cfg(name):
    R = random.Random(VARIANTS[name])
    idx = list(VARIANTS).index(name)
    # round 3 (coordinator r1 review): the silhouette varies by seed, not only the paint. A: gable to the street;
    # B: eave to the street (side gables); C: gable to the street with a log-store lean-to and a crooked twist.
    roof = ('front', 'side', 'front')[idx] if idx < 3 else R.choice(('front', 'side'))
    width = round(R.uniform(4.7, 5.7) if idx != 2 else R.uniform(4.7, 5.2), 2)   # C keeps room for its lean-to
    jetty = round(R.uniform(0.42, 0.58), 2)                                      # deep jetties: shadow lines that read from the street
    if roof == 'front':   # roof verges (0.55) stay inside the 8 m foundation
        depth = round(min(6.0, 6.85 - 2 * jetty - R.uniform(0.0, 0.2)), 2)
    else:                 # eaves (0.8) front and back
        depth = round(6.15 - 2 * jetty - R.uniform(0.0, 0.15), 2)
    walls = [round(R.uniform(2.0, 2.45), 2), round(R.uniform(1.9, 2.3), 2)]
    pitch = R.uniform(52, 60) if roof == 'front' else R.uniform(44, 50)
    combo = COMBOS[(idx + R.randrange(len(COMBOS))) % len(COMBOS)] if idx >= len(COMBOS) else COMBOS[idx]
    plaster, paint = combo   # the three named variants each get their own pair, distinct from the B4 neighbours' (square variety)
    return dict(width=width, depth=depth, jetty=jetty, walls=walls, pitch=pitch, plaster=plaster, paint=fk._lin(paint),
                paint_hex=paint, lean=(R.choice((-1, 1)) * R.uniform(0.014, 0.022), R.uniform(0.028, 0.040)),   # round 2: the storybook lean has to read at mid distance
                chimney=(R.uniform(-0.25, 0.3), R.choice((-1, 1))), flowerbox=R.random() < 0.8 or idx == 0,
                wear=R.uniform(1.15, 1.4), seed=VARIANTS[name], door_side=R.choice((-1, 1)),
                roof=roof, leanto=idx == 2, twist=0.006 if idx == 2 else 0.0, back_win=R.choice((0, 2)))

# ----------------------------------------------------------------------------- lean (+ an optional twist about the house's centre)
TWIST = 0.0; TWIST_YC = 0.0
PLINTH0 = fk.PLINTH
def lean_pt_tw(p):
    q = Vector((p.x + fk.LEAN_X * max(0.0, p.z - PLINTH0), p.y - fk.LEAN_Y * max(0.0, p.z - fk.BRES0), p.z))
    a = TWIST * max(0.0, p.z - fk.BRES0)
    if a:
        dx, dy = q.x, q.y - TWIST_YC
        q.x, q.y = dx * math.cos(a) - dy * math.sin(a), TWIST_YC + dx * math.sin(a) + dy * math.cos(a)
    return q
def lean_tw(me):
    for v in me.vertices:
        v.co = lean_pt_tw(v.co)
fk.lean, fk.lean_pt = lean_tw, lean_pt_tw

# ----------------------------------------------------------------------------- helpers
def xf_side(side):
    """Wall frame -> world. Every wall is built as a front (plaster plane y 0, facing -Y, x along the wall)."""
    W, D = fk.W, fk.DEPTH
    if side == 'front':
        return Matrix.Identity(4)
    if side == 'right':   # +X face, wall x = world y
        return Matrix.Translation((W, 0, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'Z')
    if side == 'left':    # -X face, wall x = -world y
        return Matrix.Translation((-W, 0, 0)) @ Matrix.Rotation(-math.pi / 2, 4, 'Z')
    return Matrix.Translation((0, D, 0)) @ Matrix.Rotation(math.pi, 4, 'Z')   # back, wall x = -world x

def wall_range(side, plane):
    """Wall-frame x range of a storey whose front plane is `plane` (world y)."""
    W, D = fk.W, fk.DEPTH
    return {'front': (-W, W), 'back': (-W, W), 'right': (plane, D), 'left': (-D, -plane)}[side]

def absorb(dst, src, xf):
    """Move a Piece's geometry, transformed by xf, into dst (material slots remapped)."""
    bmesh.ops.transform(src.bm, matrix=xf, verts=src.bm.verts[:])
    if xf.to_3x3().determinant() < 0:
        bmesh.ops.reverse_faces(src.bm, faces=src.bm.faces[:])
    me = bpy.data.meshes.new('tmp'); src.bm.to_mesh(me); src.bm.free()
    for m in src.mats:
        if m not in dst.mats:
            dst.mats.append(m)
    remap = [dst.mats.index(m) for m in src.mats]
    for p in me.polygons:
        p.material_index = remap[p.material_index] if p.material_index < len(remap) else 0
    dst.bm.from_mesh(me)
    bpy.data.meshes.remove(me)

class Side:
    """Fresh pieces for one wall, built in the wall frame, then absorbed into the house pieces."""
    NAMES = ('Kit_Posts', 'Kit_Rails', 'Kit_Braces', 'Kit_JettyBeams', 'Kit_Panels', 'Kit_Windows', 'Kit_Plinth')
    def __init__(self, side):
        self.side = side
        self.p = {n: fk.Piece(n) for n in self.NAMES}
        self.t0 = len(fk.TIMBERS)
        self.shut = []      # (a, b, z0, z1, plane) in the wall frame
        self.decals = []
    def commit(self, house):
        xf = xf_side(self.side)
        for n, pc in self.p.items():
            absorb(house[n], pc, xf)
        for i in range(self.t0, len(fk.TIMBERS)):
            a, b, hw = fk.TIMBERS[i]
            fk.TIMBERS[i] = (xf @ a, xf @ b, hw)
        if self.shut:
            sp = fk.kit_shutters(self.shut)
            absorb(house['Kit_Shutters'], sp, xf)
        r3 = xf.to_3x3()
        for st, c, sz, nrm, rt in self.decals:
            house['_decals'].append((st, tuple(xf @ Vector(c)), sz, tuple(r3 @ Vector(nrm)), rt))

def bays(x0, x1, target=1.15):
    n = max(2, round((x1 - x0) / target))
    return [x0 + (x1 - x0) * i / n for i in range(n + 1)]

# ----------------------------------------------------------------------------- the walls
TIM, PROUD, PLINTH = fk.TIM, fk.PROUD, fk.PLINTH

def storey_wall(S, k, xs, winbays, door_bay, crosses, shutters, wpw, cell, ends=(True, True), base_band=False, lite=False):
    """One storey of framing on wall S (a Side), wall frame. xs: bay lines; the plaster spans xs[0]..xs[-1]."""
    st = S.st[k]; pc = S.p; nm = '%s%d_%d' % (S.side, k, S.seed)
    yc = -PROUD + TIM / 2
    n = len(xs) - 1
    x0, x1 = xs[0], xs[-1]
    d0 = d1 = None
    if door_bay is not None:
        d0, d1 = xs[door_bay] + TIM / 2 + 0.02, xs[door_bay + 1] - TIM / 2 - 0.02
    wins = []
    for i in winbays:
        a, b = xs[i] + TIM / 2 + 0.05, xs[i + 1] - TIM / 2 - 0.05
        if b - a > wpw + 0.1:   # wide bay: centre a window of the house's width
            c = (a + b) / 2; a, b = c - wpw / 2, c + wpw / 2
        if k == 0:
            wins.append((a, b, 1.33, 2.25))
        else:
            hz = min(1.2, st['zt'] - st['zb'] - 0.85)
            wins.append((a, b, st['zb'] + 0.55, st['zb'] + 0.55 + hz))
    # posts
    for i, x in enumerate(xs):
        if (i == 0 and not ends[0]) or (i == n and not ends[1]):
            continue
        xp = max(x0 + TIM / 2, min(x1 - TIM / 2, x))
        zb = 0.16 if (door_bay is not None and i in (door_bay, door_bay + 1)) else st['zb']
        fk.member(pc['Kit_Posts'], 'oak', (xp, yc, zb), (xp, yc, st['zt']), TIM, TIM, name=nm + 'p%d' % i)
    if k == 0:
        segs = [(0, n)] if door_bay is None else [(0, door_bay), (door_bay + 1, n)]
        for i0, i1 in segs:
            if i1 > i0:
                fk.member(pc['Kit_Rails'], 'oak', (xs[i0], yc, PLINTH + 0.12), (xs[i1], yc, PLINTH + 0.12), 0.24, TIM,
                          name=nm + 'sole%d' % i0, extend=0.02)
        fk.member(pc['Kit_Rails'], 'oak', (x0 - 0.04, yc, (fk.GIRT0 + fk.GIRT1) / 2), (x1 + 0.04, yc, (fk.GIRT0 + fk.GIRT1) / 2),
                  fk.GIRT1 - fk.GIRT0, TIM, name=nm + 'girt', sag=0.02)
        if d0 is not None:
            fk.member(pc['Kit_Rails'], 'oak', (d0 - 0.02, yc, 2.47), (d1 + 0.02, yc, 2.47), 0.22, TIM, name=nm + 'lintel', sag=0.012)
    else:
        if base_band:   # side and back walls: a floor beam closes the band between storeys
            fk.member(pc['Kit_JettyBeams'], 'oak', (x0 - 0.06, -PROUD + 0.15, st['base'] + 0.37),
                      (x1 + 0.06, -PROUD + 0.15, st['base'] + 0.37), 0.30, 0.30, name=nm + 'fb', sag=0.03, cham=0.03)
            fk.wall_panel(pc['Kit_Panels'], 'plaster', 0.0, x0, x1, st['base'] + 0.10, st['zb'] - 0.03, bulge=0.0, cell=cell * 2)
        fk.member(pc['Kit_Rails'], 'oak', (x0 - 0.08, yc, st['zt'] + 0.13), (x1 + 0.08, yc, st['zt'] + 0.13), 0.26, TIM + 0.04,
                  name=nm + 'plate', sag=0.03)
    for wi, (a, b, z0, z1) in enumerate(wins):
        i = winbays[wi]
        fk.member(pc['Kit_Rails'], 'oak', (xs[i], yc, z0 - 0.08), (xs[i + 1], yc, z0 - 0.08), 0.16, TIM, name=nm + 's%d' % wi)
        fk.member(pc['Kit_Rails'], 'oak', (xs[i], yc, z1 + 0.08), (xs[i + 1], yc, z1 + 0.08), 0.16, TIM, name=nm + 'h%d' % wi)
        fk.window(pc['Kit_Windows'], 0.0, a, b, z0, z1, zlib.crc32(nm.encode()) % 1000 + wi, lite=lite)
        S.runoff.append(z0 - 0.16)
        S.decals.append(('drip', ((a + b) / 2, -0.003, z0 - 0.42), (b - a + 0.1, 0.65), (0, -1, 0), 0.0))
        if shutters and (k > 0 or S.side != 'front' or True):
            S.shut.append((a, b, z0, z1, 0.0))
        if k > 0 and crosses:
            ya = yc + 0.015
            fk.member(pc['Kit_Braces'], 'oak', (xs[i] + TIM / 2, ya, st['zb'] + 0.02), (xs[i + 1] - TIM / 2, ya, z0 - 0.16),
                      0.13, TIM - 0.03, name=nm + 'xa%d' % wi, cham=0.02)
            fk.member(pc['Kit_Braces'], 'oak', (xs[i + 1] - TIM / 2, ya, st['zb'] + 0.02), (xs[i] + TIM / 2, ya, z0 - 0.16),
                      0.13, TIM - 0.03, name=nm + 'xb%d' % wi, cham=0.02)
    xm = (x0 + x1) / 2
    for i in range(n):
        if i in winbays or i == door_bay:
            continue
        x0b, x1b = xs[i] + TIM / 2, xs[i + 1] - TIM / 2
        ztb = st['zb'] + 0.75 * (st['zt'] - st['zb'])
        a, b = ((x0b, st['zb']), (x1b, ztb)) if (xs[i] + xs[i + 1]) / 2 < xm else ((x1b, st['zb']), (x0b, ztb))
        fk.member(pc['Kit_Braces'], 'oak', (a[0], yc + 0.015, a[1]), (b[0], yc + 0.015, b[1]), 0.22, TIM - 0.03,
                  name=nm + 'b%d' % i, cham=0.02)
    holes = list(wins)
    if d0 is not None:
        holes.append((d0, d1, 0.16 - 0.5, 2.36))
    zlo = (PLINTH + 0.2) if k == 0 else st['zb'] - 0.05
    fk.wall_panel(pc['Kit_Panels'], 'plaster', 0.0, x0, x1, zlo, st['zt'] + 0.05, holes=holes, cell=cell)
    return d0, d1

def plinth_wall(S, x0, x1, gap=None):
    pc = S.p['Kit_Plinth']
    runs = [(x0, x1)] if gap is None else [(x0, gap[0]), (gap[1], x1)]
    for (a, b) in runs:
        if b - a < 0.05:
            continue
        fk.add_box(pc, 'mortar', (b - a, 0.36, PLINTH - 0.02), Matrix.Translation(((a + b) / 2, 0.16, (PLINTH - 0.02) / 2)),
                   cham=0.0, uv_world=True)
        fk.plinth_run(pc, a, b, 'front', seed=a + 13.0 * len(S.side))

def gable(S, half, z0, ztop, cell):
    """Framed gable triangle on the wall plane: plaster, king post, collar, raking braces, queen posts."""
    pc = S.p; nm = 'g%s' % S.side
    yt = -PROUD + TIM / 2
    nz = 8
    for j in range(nz):
        za = z0 + (ztop - z0) * j / nz; zb2 = z0 + (ztop - z0) * (j + 1) / nz
        ha = half * (1 - j / nz); hb = half * (1 - (j + 1) / nz)
        fk.quad_grid(pc['Kit_Panels'], 'plaster', [(-ha, 0.0, za), (ha, 0.0, za), (hb, 0.0, zb2), (-hb, 0.0, zb2)],
                     max(1, int(2 * ha / (cell * 4))), 1, uv_fn=lambda c: (c.x / fk.TILE, c.z / fk.TILE))
    fk.member(pc['Kit_Posts'], 'oak', (0, yt, z0), (0, yt, ztop - 0.25), TIM, TIM, name=nm + 'king')
    zc = z0 + 0.45 * (ztop - z0); hc = half * (1 - (zc - z0) / (ztop - z0))
    fk.member(pc['Kit_Rails'], 'oak', (-hc + 0.1, yt, zc), (hc - 0.1, yt, zc), 0.24, TIM, name=nm + 'collar')
    for sgn in (-1, 1):
        fk.member(pc['Kit_Braces'], 'oak', (sgn * half * 0.62, yt + 0.015, z0), (sgn * TIM / 2, yt + 0.015, zc - 0.15), 0.22,
                  TIM - 0.03, name='%s%d' % (nm, sgn), cham=0.02)
        xq = sgn * half * 0.42
        fk.member(pc['Kit_Posts'], 'oak', (xq, yt, z0), (xq, yt, z0 + (ztop - z0) * (1 - abs(xq) / half) - 0.15),
                  0.22, TIM, name='%sq%d' % (nm, sgn))
    # a small attic vent under the apex (dark louvres), so the gable has a focal point at mid distance
    zv = zc + 0.55
    for i in range(4):
        fk.add_box(pc['Kit_Windows'], 'oak', (0.46, 0.05, 0.05), Matrix.Translation((0.32, -0.02, zv + i * 0.1)) @
                   Matrix.Rotation(math.radians(-30), 4, 'X'), cham=0.008, long_axis=0, seed=i + 0.3)
    fk.quad_grid(pc['Kit_Windows'], 'glass', [(0.09, 0.08, zv - 0.06), (0.55, 0.08, zv - 0.06), (0.55, 0.08, zv + 0.36),
                                               (0.09, 0.08, zv + 0.36)], 1, 1, uv_fn=lambda c: (c.x, c.z))

# ----------------------------------------------------------------------------- assembly
def build_house(name):
    c = cfg(name)
    fk.TIMBERS.clear(); fk.MATS.clear()
    fk.W = c['width'] / 2; fk.DEPTH = c['depth']; fk.JETTY = c['jetty']
    fk.LEAN_X, fk.LEAN_Y = c['lean']; fk.SHUTTER_PAINT = fk.DOOR_PAINT = c['paint']; fk.PLASTER_TINT = c['plaster']
    W, D, J = fk.W, fk.DEPTH, fk.JETTY
    R = random.Random(c['seed'])
    st = [dict(plane=0.0, zb=PLINTH + 0.24, zt=fk.GIRT0, top=fk.GIRT1, base=0.0)]
    for k, h in enumerate(c['walls']):
        base = st[-1]['top']
        st.append(dict(plane=-J * (k + 1), zb=base + 0.52, zt=base + 0.52 + h, top=base + 0.78, base=base))
        st[-1]['top'] = st[-1]['zt'] + (fk.PLATE1 - fk.PLATE0)
    YU = st[-1]['plane']; fk.YU = YU; fk.PLATE0 = st[-1]['zt']; fk.PLATE1 = st[-1]['top']
    global TWIST, TWIST_YC
    TWIST, TWIST_YC = c['twist'], (YU + D) / 2
    fk.RIDGE_SAG = 0.32   # a sway-backed ridge (storybook crooked roofline)
    yc2 = (YU + D) / 2
    front_gable = c['roof'] == 'front'
    rise = (W if front_gable else (D - YU) / 2) * math.tan(math.radians(c['pitch']))
    fk.RIDGE = fk.PLATE1 + rise
    house = {n: fk.Piece(n) for n in Side.NAMES + ('Kit_Shutters',)}
    house['_decals'] = []
    runoff = []
    wpw = R.uniform(0.80, 0.95)          # window clear width: narrow, so open shutters stay inside their bay
    cell = 0.30                           # plaster grid on the front (tris budget: the hero uses 0.125); sides/back 0.35
    side_win = {sd: [R.randrange(1, 3) for _ in st] for sd in ('right', 'left')}   # one window per storey per side
    back_win = [[c['back_win']], [0, 2], [1]]   # bay 1 of the ground storey is the back door
    if c['leanto']:
        side_win['left'][0] = side_win['right'][0] = 1

    # front: door bay at one side of centre, windows in the other bays, shutters and crosses on the upper storeys
    xs_g = bays(-W, W, 2 * W / 3)   # three bays: door in the middle
    nb = len(xs_g) - 1
    door_bay = nb // 2 if nb % 2 else (nb // 2 - (1 if c['door_side'] < 0 else 0))
    for side in ('front', 'right', 'back', 'left'):
        S = Side(side); S.st = st; S.seed = c['seed'] % 97; S.runoff = runoff
        for k in range(len(st)):
            plane = st[k]['plane']
            x0, x1 = wall_range(side, plane)
            if side in ('front', 'back'):
                xs = bays(x0, x1, (x1 - x0) / 3)
                n = len(xs) - 1
                if side == 'front':
                    winb = [0, 2]
                    d = door_bay if k == 0 else None
                else:
                    winb = back_win[k]
                    d = 1 if k == 0 else None
                xf_y = Matrix.Identity(4) if side == 'back' else Matrix.Translation((0, plane, 0))   # the back does not jetty
            else:
                xs = bays(x0, x1, 1.6)
                n = len(xs) - 1
                winb = [max(1, min(n - 2, side_win[side][k]))]   # never the back bay (chimney breast)
                d = None
                xf_y = Matrix.Identity(4)
            # front / back walls sit on their storey plane; the frame offsets a sub-Side by that plane
            sub = Side(side); sub.st = st; sub.seed = S.seed; sub.runoff = runoff
            dd = storey_wall(sub, k, xs, winb, d, crosses=(side != 'back' or k == 1),
                             shutters=(k > 0 and side == 'front') or (k == 1 and side in ('right', 'left')),
                             wpw=wpw, cell=cell if side == 'front' else 0.45, lite=side != 'front', ends=(True, True) if side in ('front', 'back') else (False, False),
                             base_band=(k > 0 and side in ('right', 'left', 'back')))
            if side == 'front' and k == 0:
                fk.DOOR = (dd[0], dd[1], 0.16, 2.36)
            if side == 'back' and k == 0:
                back_door(sub.p['Kit_Windows'], dd[0], dd[1])
                S.back_gap = (dd[0] - TIM, dd[1] + TIM)
            # soffit under each jetty on the front
            if side == 'front' and k > 0:
                prev = st[k - 1]['plane'] - plane   # positive, in this sub-frame
                for j in range(int(2 * W / 0.62) + 1):
                    x = -W + 0.25 + j * (2 * W - 0.5) / int(2 * W / 0.62)
                    fk.member(sub.p['Kit_JettyBeams'], 'oak', (x, prev + 0.15, st[k]['base'] + 0.11), (x, -0.10, st[k]['base'] + 0.11),
                              0.17, 0.22, name='j%d_%d_%d' % (k, j, S.seed), cham=0.03, up=(0, 0, -1))
                fk.member(sub.p['Kit_JettyBeams'], 'oak', (-W - 0.10, -PROUD + 0.15, st[k]['base'] + 0.37),
                          (W + 0.10, -PROUD + 0.15, st[k]['base'] + 0.37), 0.30, 0.32, name='bres%d_%d' % (k, S.seed), sag=0.05, cham=0.03)
                fk.quad_grid(sub.p['Kit_Panels'], 'plaster', [(-W, prev, st[k]['base'] + 0.22), (W, prev, st[k]['base'] + 0.22),
                                                              (W, 0.0, st[k]['base'] + 0.22), (-W, 0.0, st[k]['base'] + 0.22)],
                             max(1, nb), 1, uv_fn=lambda cc: (cc.x / fk.TILE, cc.y / fk.TILE))
                # bracket ends under the jetty at every post line: the jetty reads from the street
                for i, x in enumerate(xs):
                    xp = max(-W + TIM / 2, min(W - TIM / 2, x))
                    fk.member(sub.p['Kit_JettyBeams'], 'oak', (xp, prev - PROUD + 0.10, st[k]['base'] - 0.85),
                              (xp, -PROUD + 0.14, st[k]['base'] + 0.24), 0.17, 0.19, name='bk%d_%d_%d' % (k, i, S.seed),
                              cham=0.03, up=(1, 0, 0), sag=0.05)
            # bring the storey into the side frame (front/back planes)
            for nme, pc in sub.p.items():
                absorb(S.p[nme], pc, xf_y)
            for i in range(sub.t0, len(fk.TIMBERS)):
                a, b, hw = fk.TIMBERS[i]; fk.TIMBERS[i] = (xf_y @ a, xf_y @ b, hw)
            yoff = xf_y.translation.y
            S.shut += [(a, b, z0, z1, pl + yoff) for (a, b, z0, z1, pl) in sub.shut]
            S.decals += [(s_, (cc[0], cc[1] + yoff, cc[2]), sz, nr, rt) for (s_, cc, sz, nr, rt) in sub.decals]
        # plinth on every side; the front plinth gaps for the door
        x0, x1 = wall_range(side, 0.0)
        plinth_wall(S, x0 - (0.02 if side in ('front', 'back') else 0.0), x1 + (0.02 if side in ('front', 'back') else 0.0),
                    gap=(fk.DOOR[0] - TIM, fk.DOOR[1] + TIM) if side == 'front' else getattr(S, 'back_gap', None))
        if side == 'back':   # a plain step at the back door
            g0, g1 = S.back_gap
            fk.add_box(S.p['Kit_Plinth'], 'stone', (g1 - g0 + 0.2, 0.5, 0.16), Matrix.Translation(((g0 + g1) / 2, -0.1, 0.08)),
                       cham=0.03, uv_world=True, wob=0.006, seed=5.5)
        if not front_gable and side in ('right', 'left'):   # side gables (eave to the street)
            G = Side(side)
            gable(G, (D - YU) / 2, fk.PLATE1, fk.RIDGE - 0.2, cell)
            gxf = Matrix.Translation((yc2 if side == 'right' else -yc2, 0, 0))
            for nme, pc in G.p.items():
                absorb(S.p[nme], pc, gxf)
            for i in range(G.t0, len(fk.TIMBERS)):
                a, b, hw = fk.TIMBERS[i]; fk.TIMBERS[i] = (gxf @ a, gxf @ b, hw)
        # gable on front and back (the top storey plane), framed
        if front_gable and side in ('front', 'back'):
            G = Side(side)
            gable(G, W, fk.PLATE1, fk.RIDGE - 0.2, cell)
            yg = YU if side == 'front' else -0.0
            gxf = Matrix.Translation((0, yg if side == 'front' else 0.0, 0))
            for nme, pc in G.p.items():
                absorb(S.p[nme], pc, gxf)
            for i in range(G.t0, len(fk.TIMBERS)):
                a, b, hw = fk.TIMBERS[i]; fk.TIMBERS[i] = (gxf @ a, gxf @ b, hw)
        S.commit(house)
    fk.RUNOFF_Z = runoff + [PLINTH + 0.36]
    # the worn threshold step at the door
    xd0, xd1 = fk.DOOR[0] - TIM, fk.DOOR[1] + TIM
    def dish(p):
        u = max(0.0, 1 - (p.x / 0.55) ** 2); v = max(0.0, 1 - ((p.y + 0.05) / 0.3) ** 2)
        return Vector((0, 0, -0.035 * u * v)) if p.z > 0 else Vector()
    fk.add_box(house['Kit_Plinth'], 'stone', (xd1 - xd0 + 0.25, 0.62, 0.16), Matrix.Translation(((xd0 + xd1) / 2, -0.06, 0.08)),
               cham=0.03, cuts=(6, 4, 0), bend=dish, wob=0.006, seed=3.3)
    # dark interior liner boxes behind every wall: gaps and glass show a dark face, never sky
    pan = house['Kit_Panels']
    for (a, b) in (((-W + 0.45, 0.45), (W - 0.45, 0.45)), ((W - 0.45, 0.45), (W - 0.45, D - 0.45)),
                   ((W - 0.45, D - 0.45), (-W + 0.45, D - 0.45)), ((-W + 0.45, D - 0.45), (-W + 0.45, 0.45))):
        fk.quad_grid(pan, 'plaster', [(a[0], a[1], 0.0), (b[0], b[1], 0.0), (b[0], b[1], fk.PLATE1), (a[0], a[1], fk.PLATE1)], 1, 1,
                     uv_fn=lambda cc: ((cc.x + cc.y) / fk.TILE, cc.z / fk.TILE))
    # chimney: an external stone stack on one side wall over the back bay (round 3: the sides get a chimney breast)
    cside = c['chimney'][1]
    nb0 = max(2, round(D / 1.6))
    chim_w = Vector((cside * (W + 0.36), D - D / nb0 / 2, 0))
    fk.CHIM_TOP = fk.RIDGE + 0.5
    if front_gable:
        # roof built in the kit's roof frame (ridge along local X), then turned so the ridge runs front-back
        save = (fk.W, fk.YU, fk.DEPTH)
        fk.W, fk.YU, fk.DEPTH = (D - YU) / 2, -W, W
        fk.OVER_EAVE = 1.0   # a heavy cap over the narrow walls (eaves stay inside the 8 m foundation)
        fk.EAVE_Z = fk.roof_surface(0, 0).z
        roof = fk.kit_roof()
        mirror_roof(roof)
        fk.CHIM_X, fk.CHIM_Y = chim_w.y - yc2, -chim_w.x
        chim = fk.kit_chimney()
        zb_chim = fk.roof_surface(fk.CHIM_X, 0).z + 1.5
        fk.W, fk.YU, fk.DEPTH = save
        xf = Matrix.Translation((0, yc2, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'Z')
        for pc in (roof, chim):
            bmesh.ops.transform(pc.bm, matrix=xf, verts=pc.bm.verts[:])
        bw = (0.72, 1.15)
    else:
        fk.OVER_EAVE = 0.8
        fk.EAVE_Z = fk.roof_surface(0, 0).z
        roof = fk.kit_roof()
        mirror_roof(roof, yc2)
        fk.CHIM_X, fk.CHIM_Y = chim_w.x, chim_w.y
        chim = fk.kit_chimney()
        zb_chim = fk.roof_surface(fk.CHIM_X, 0).z + 1.5
        bw = (0.92, 0.95)
    fk.CHIM_WX, fk.CHIM_WY = chim_w.x, chim_w.y
    # the breast: from the ground up to the stack's base, stepping in twice
    fk.add_box(chim, 'stone_dk', (bw[0], bw[1], zb_chim + 0.05), Matrix.Translation((chim_w.x, chim_w.y, (zb_chim + 0.05) / 2)),
               cham=0.035, cuts=(0, 0, int(zb_chim / 1.2)), wob=0.012, seed=41.0, uv_world=True)
    for zo in (0.7, 2.9):
        fk.add_box(chim, 'stone_dk', (bw[0] + 0.1, bw[1] + 0.1, 0.16), Matrix.Translation((chim_w.x, chim_w.y, zo)), cham=0.03,
                   seed=zo, uv_world=True)
    pieces = [house[n] for n in Side.NAMES] + [house['Kit_Shutters'], fk.kit_door(), roof, chim]
    if c['leanto']:   # log store on the side away from the chimney
        lt = fk.kit_leanto()   # built on -X
        if -cside > 0:
            bmesh.ops.scale(lt.bm, vec=(-1, 1, 1), verts=lt.bm.verts[:]); bmesh.ops.reverse_faces(lt.bm, faces=lt.bm.faces[:])
        pieces.append(lt)
    return c, pieces, house['_decals'], st

FRONT_SHUT = []

def back_door(pc, x0, x1, z0=0.16, z1=2.30):
    """A plain ledged back door (part of the frame, not a carried part): four painted planks, two straps, a ring."""
    n = 4; pw = (x1 - x0) / n; yd = 0.10
    for i in range(n):
        fk.add_box(pc, 'door', (pw - 0.01, 0.05, z1 - z0), Matrix.Translation((x0 + pw * (i + 0.5), yd, (z0 + z1) / 2)),
                   cham=0.01, long_axis=2, wob=0.004, seed=60 + i)
    fk.quad_grid(pc, 'oak', [(x0 - 0.02, yd + 0.04, z0), (x1 + 0.02, yd + 0.04, z0), (x1 + 0.02, yd + 0.04, z1), (x0 - 0.02, yd + 0.04, z1)],
                 1, 1, uv_fn=lambda c: (c.x / fk.TILE, c.z / fk.TILE))
    for zz in (z0 + 0.4, z1 - 0.4):
        fk.add_box(pc, 'iron', (x1 - x0 - 0.2, 0.012, 0.06), Matrix.Translation(((x0 + x1) / 2 - 0.05, yd - 0.032, zz)), cham=0.0, seed=zz)
    fk.add_box(pc, 'iron', (0.05, 0.04, 0.05), Matrix.Translation((x1 - 0.16, yd - 0.04, 1.08)), cham=0.008, seed=61.5)

def mirror_roof(roof, yr=0.0):
    """The kit roof details one slope; drop its plain back slope and mirror the detailed one onto that side (about y = yr)."""
    bm = roof.bm
    bm.faces.ensure_lookup_table()
    def ridge(cc):   # the half-round ridge tiles straddle the ridge line: keep them once
        return abs(cc.y - yr) < 0.2 and cc.z > fk.ridge_z(cc.x) - 0.12
    kill = [f for f in bm.faces if f.calc_center_median().y > yr + 0.001 and not ridge(f.calc_center_median())]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    dup_src = [f for f in bm.faces if f.calc_center_median().y < yr - 0.001 and not ridge(f.calc_center_median())]
    ret = bmesh.ops.duplicate(bm, geom=dup_src)
    nv = [e for e in ret['geom'] if isinstance(e, bmesh.types.BMVert)]
    nf = [e for e in ret['geom'] if isinstance(e, bmesh.types.BMFace)]
    for v in nv:
        v.co.y = 2 * yr - v.co.y
    bmesh.ops.reverse_faces(bm, faces=nf)
    bm.normal_update()

# ----------------------------------------------------------------------------- objects, parts, sockets, footprint
def assemble(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    global FRONT_SHUT
    FRONT_SHUT = []
    c = cfg(name)
    # the front's first-floor windows (flower boxes) are known after the walls: build once to collect them
    c, pieces, decals, st = build_house_collect(name)
    root = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(root)
    fk.TIMBERS_L = [(fk.lean_pt(a), fk.lean_pt(b), hw) for (a, b, hw) in fk.TIMBERS]
    obs = []
    for pc in pieces:
        me = bpy.data.meshes.new(pc.name)
        smooth = pc.name == 'Kit_Panels'
        if smooth:
            bmesh.ops.remove_doubles(pc.bm, verts=pc.bm.verts[:], dist=0.002)
        pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
        for m in pc.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
        fk.lean(me)
        fk.write_wear(ob, chimney_top=fk.CHIM_TOP)
        for p in me.polygons:
            p.use_smooth = smooth
        if smooth:
            me.set_sharp_from_angle(angle=math.radians(30))
        print('  %-18s %6d tris' % (pc.name, sum(len(p.vertices) - 2 for p in me.polygons)))
        obs.append(ob)
    for pc, me in fk.kit_decals(decals):
        fk.lean(me)
        for m in pc.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
        a = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
        for d in a.data:
            d.color = (0, 0, 0, 1)
        obs.append(ob)
    # wear amount per variant (grime channel)
    for ob in obs:
        a = ob.data.color_attributes.get('wear')
        if a and abs(c['wear'] - 1) > 1e-3:
            for d in a.data:
                r, g, b, al = d.color
                d.color = (min(1.0, r * c['wear']), g, b, al)
    # centre the body on the foundation: body y from the top storey's bressumer face to the back plinth
    W = fk.W
    y_front = st[-1]['plane'] - 0.10
    y_back = fk.DEPTH + 0.06
    cy = (y_front + y_back) / 2
    shift = Matrix.Translation((0, -cy, 0))
    for ob in obs:
        ob.data.transform(shift)
    # parts
    door = [o for o in obs if o.name == 'Kit_Door'][0]
    shut = [o for o in obs if o.name == 'Kit_Shutters'][0]
    frame_obs = [o for o in obs if o not in (door, shut)]
    sock_door = Vector(((fk.DOOR[0] + fk.DOOR[1]) / 2, 0.10 - cy, fk.DOOR[2]))
    if FRONT_SHUT:
        a, b, z0, z1, plane = FRONT_SHUT[0]
        sock_shut = fk.lean_pt(Vector(((a + b) / 2, plane, (z0 + z1) / 2))) + Vector((0, -cy - PROUD, 0))
    else:
        sock_shut = Vector((0, -cy, 4.0))
    def as_part(ob, nm, at):
        ob.name = nm; ob.data.name = nm
        ob.data.transform(Matrix.Translation(-at)); ob.location = at
        ob.parent = root
    as_part(door, 'Part_Door', sock_door)
    as_part(shut, 'Part_Shutters', sock_shut)
    # frame: join every other piece
    bpy.ops.object.select_all(action='DESELECT')
    for o in frame_obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = frame_obs[0]
    bpy.ops.object.join()
    frame = bpy.context.view_layer.objects.active
    frame.name = 'Frame'; frame.data.name = 'Frame'; frame.parent = root
    for nm, at in (('SOCKET_Door', sock_door), ('SOCKET_Shutters', sock_shut)):
        e = bpy.data.objects.new(nm, None); e.empty_display_size = 0.3; e.location = at; e.parent = root
        bpy.context.scene.collection.objects.link(e)
    # footprint: the body (walls, jetties, plinth), flat
    fw, fd = 2 * W + 2 * PROUD + 0.06, y_back - y_front
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation((0, 0, 0.05)) @ Matrix.Diagonal((fw, fd, 0.1, 1)))
    me = bpy.data.meshes.new('FOOTPRINT'); bm.to_mesh(me); bm.free()
    fpm = bpy.data.materials.new('FOOTPRINT'); me.materials.append(fpm)
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root
    fp.hide_render = True
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in (frame, door, shut)}
    total = sum(tris.values())
    print('SC TOWNHOUSE %s width=%.2f depth=%.2f jetty=%.2f pitch=%.1f footprint=%.2fx%.2f tris=%d (%s)' %
          (name, c['width'], c['depth'], c['jetty'], c['pitch'], fw, fd, total, ', '.join('%s %d' % kv for kv in tris.items())))
    return root, frame, door, shut, fp, c, dict(footprint=(fw, fd), tris=total, parts=tris, ridge=fk.RIDGE, chim=fk.CHIM_TOP)

def build_house_collect(name):
    """build_house, recording the front's first-floor shutter windows (flower boxes, SOCKET_Shutters)."""
    global FRONT_SHUT
    orig = fk.kit_shutters
    rec = []
    def spy(wins=None):
        rec.append(list(wins or []))
        return orig(wins)
    fk.kit_shutters = spy
    try:
        # first pass to learn the front windows, cheap enough (no meshes kept)
        c, pieces, decals, st = build_house(name)
    finally:
        fk.kit_shutters = orig
    # rec[0] is the front side's shutters, in the front frame = world; first-floor ones have z0 below the second plate
    front = rec[0] if rec else []
    FRONT_SHUT = sorted([w for w in front if st[1]['zb'] < w[2] < st[1]['zt']], key=lambda w: w[0])
    if c['flowerbox']:
        for (a, b, z0, z1, plane) in FRONT_SHUT[:1] if c['door_side'] < 0 else FRONT_SHUT[1:2]:   # one box per house (budget)
            pieces.append(fk.kit_flowerbox(a, b, z0 - 0.02, plane))
    return c, pieces, decals, st

# ----------------------------------------------------------------------------- review renders
VIEWS = {   # the four three-quarter corners (four turns: every side is seen), at the square's mid distance
    'fl': (Vector((-10.5, -14.5, 3.6)), Vector((0, 0, 5.6)), 50),
    'fr': (Vector((10.5, -14.5, 3.6)), Vector((0, 0, 5.6)), 50),
    'bl': (Vector((-10.5, 14.5, 3.6)), Vector((0, 0, 5.6)), 50),
    'br': (Vector((10.5, 14.5, 3.6)), Vector((0, 0, 5.6)), 50),
    'front': (Vector((0, -17.0, 2.4)), Vector((0, 0, 5.6)), 50),
}

def render(name, views, suffix=''):
    sc = bpy.context.scene
    fk.RRES = int(arg('--rres', '960')); fk.SAMPLES = int(arg('--samples', '32'))
    cam = fk.review_scene(bpy.data.objects[name], variant=True)
    fig = bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(1.6, -4.6, 0.875))
    for v in views:
        frm, to, fov = VIEWS[v]
        fk.look(cam, frm, to); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, '%s_%s%s.png' % (name, v, suffix))
        bpy.ops.render.render(write_still=True)
        print('rendered', sc.render.filepath)

def export(name, root):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for o in root.children:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='FACE',
                             add_leaf_bones=False, colors_type='LINEAR', object_types={'EMPTY', 'MESH'},
                             path_mode='STRIP')   # no texture paths: Unity's legacy import found the cloud paths and named every slot 'Lit'

if __name__ == '__main__':
    which = arg('--variant', 'all')
    names = list(VARIANTS) if which == 'all' else [which]
    views = arg('--views', 'fl,fr,bl,br').split(',')
    for nm in names:
        root, frame, door, shut, fp, c, info = assemble(nm)
        if '--export' in argv:
            export(nm, root)
        if '--frame-only' in argv:
            door.hide_render = shut.hide_render = True
        if '--no-render' not in argv:
            render(nm, views, '_frame' if '--frame-only' in argv else '')
        with open(os.path.join(OUT, nm + '_info.txt'), 'w') as f:
            f.write(repr(dict(cfg={k: v for k, v in c.items() if k != 'paint'}, **info)) + '\n')
