"""CellarKit: B8 cellar kit additions (docs/design/dungeon1-cellars.md sections 5 and 6, proposal P7 stone scale).

Imports sc_cellar_vault (same authoring frame: A = (x across, y along, z up) = Unity (x, z, y), pieces rotated 180 deg about Z on
output, same CV_* materials, same wear attribute) and builds the existing 12 kit pieces PLUS the new ones, then exports ONE
CellarVault.fbx so CellarFloorTestBuilder.ArtPieces keeps resolving the old names. Existing pieces are unchanged.

New pieces (own pivots, overlap in the file; budget 0.5-5k tris each):
  GroinBay     4 x 4 m bay centred on the origin, floor z 0: four corner piers (ashlar courses 0.28 m), four stone transverse arches,
               two diagonal groin ribs, groin vault shell (intersection of two barrel vaults, radius 2, springing z 2.4, crown 4.4)
  PillarRound  round drum pillar, floor at 0: plinth, 7 stacked drums (0.27 m courses), flared capital, abacus; top at z 2.4
  WallAshlar   wall body like EndWall (y -0.5..0, face +y) with 0.28 m ashlar courses in relief
  WallBrickArch  ashlar wall with a blind arch (stone voussoirs and jambs) whose recess is filled with brick
  WallSoot     soot-black ashlar wall with a flue recess
  WallLime     limewashed wall with a stone plinth course and a moulded springing string (no fallen-render windows)
  SconceCage   wall cage sconce, origin at the mount centre, projects +y 0.30 m
  LanternChain chain cage lantern, origin at the top hook, hangs down 1.0 m
  LanternOil   oil (hurricane) lantern, origin at the base, 0.34 m tall
Stone scale (P7): courses 25-30 cm everywhere in the new pieces (the old Pillar/ArchRib keep their 38-42 cm blocks).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/cellars/sc_cellar_kit.py -- --out <scratch>
      [--export] [--export-dir <dir>] [--no-render] [--rres 960] [--samples 20] [--tris]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix
import sc_cellar_vault as V
FK = V.FK
box, lathe, tube, arc_stone = V.box, V.lathe, V.tube, V.arc_stone
Rx, Ry, Rz = V.Rx, V.Ry, V.Rz
R_ = lambda s: FK.rng('ck:' + s)

V.EXTRA['flame'] = lambda: V.emissive('CV_Flame', (0.55, 0.30, 0.06), (1.0, 0.62, 0.20), 5.0)
V.SMOOTH.add('CV_Flame')
V.WALLY.update({'WallAshlar', 'WallBrickArch', 'WallSoot', 'WallLime', 'GroinBay', 'PillarRound'})

COURSE = 0.28

# ----------------------------------------------------------------------------- GroinBay
SPR, RAD = 2.4, 2.0
HALF = 2.0

def groin_z(x, y):
    m = max(abs(x), abs(y))
    return SPR + math.sqrt(max(0.0, RAD * RAD - m * m))

def groin_shell(pc, n=12, thick=0.32):
    """Inner shell and outer extrados of the groin vault over [-2, 2]^2 (angle-spaced grid so the steep haunches stay smooth)."""
    ts = [math.sin(math.radians(-90 + 180 * i / n)) * HALF for i in range(n + 1)]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    keys = {}
    def arc(c):   # arc length from the crown of a barrel of radius RAD, metres
        return RAD * math.asin(max(-1.0, min(1.0, c / RAD)))
    def uv(x, y):
        if abs(x) >= abs(y):
            return (arc(x) / FK.TILE, y / FK.TILE)
        return (x / FK.TILE, arc(y) / FK.TILE)
    for lift, flip, mat in ((0.0, False, 'vault'), (thick, True, 'mortar')):
        g = [[bm.verts.new((x, y, groin_z(x, y) + lift)) for y in ts] for x in ts]
        for i in range(n):
            for j in range(n):
                vs = [g[i][j], g[i + 1][j], g[i + 1][j + 1], g[i][j + 1]]
                f = bm.faces.new(vs[::-1] if flip else vs)
                cx = (ts[i] + ts[i + 1]) / 2; cy = (ts[j] + ts[j + 1]) / 2
                keys[f] = mat
                for l in f.loops:
                    l[uvl].uv = uv(l.vert.co.x, l.vert.co.y)
    # rim: close the shell between the inner and outer surface along the four edges
    def vat(x, y, lift):
        for v in bm.verts:
            if abs(v.co.x - x) < 1e-6 and abs(v.co.y - y) < 1e-6 and abs(v.co.z - (groin_z(x, y) + lift)) < 1e-6:
                return v
    for i in range(n):
        for (ax, ay, bx, by, rev) in ((ts[i], -HALF, ts[i + 1], -HALF, True), (ts[i], HALF, ts[i + 1], HALF, False),
                                      (-HALF, ts[i], -HALF, ts[i + 1], False), (HALF, ts[i], HALF, ts[i + 1], True)):
            q = [vat(ax, ay, 0.0), vat(bx, by, 0.0), vat(bx, by, thick), vat(ax, ay, thick)]
            f = bm.faces.new(q[::-1] if rev else q); keys[f] = 'mortar'
            for l in f.loops:
                l[uvl].uv = (l.vert.co.x / FK.TILE, l.vert.co.z / FK.TILE)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    V.merge_faces(pc, bm, uvl, lambda f: keys[f])

def pier(pc, px, py, R):
    box(pc, 'vault', (0.78, 0.78, 0.22), (px, py, 0.11), cham=0.035, wob=0.004, seed=R.random() * 60)
    z = 0.22
    for k in range(7):
        h = 0.27 + (0.01 if k == 6 else 0.0)
        box(pc, 'vault', (0.62 + R.uniform(-0.01, 0.02), 0.62 + R.uniform(-0.01, 0.02), h - 0.01), (px + R.uniform(-0.008, 0.008), py + R.uniform(-0.008, 0.008), z + h / 2),
            Rz(R.uniform(-1.0, 1.0)), cham=0.02, wob=0.004, seed=R.random() * 80)
        z += h
    box(pc, 'vault', (0.80, 0.80, 0.14), (px, py, 2.33), cham=0.035, wob=0.004, seed=R.random() * 60)   # cap, top at 2.4

def ring_xz(pc, mat, xf, r_in, r_out, n, depth, R, keystone=0.05):
    """Semicircular arch of n voussoirs centred at the local origin of xf (ring in local x/z, thickness along local y)."""
    da = 180 / n
    for i in range(n):
        a = math.radians(da * (i + 0.5)); rc = (r_in + r_out) / 2
        tang = 2 * r_in * math.tan(math.radians(da / 2)) * 1.04
        kst = keystone if i == n // 2 and n % 2 else 0.0
        c = Vector((rc * math.cos(a), 0, rc * math.sin(a) - kst / 2))
        FK.add_box(pc, mat, (r_out - r_in + kst, depth, tang), xf @ Matrix.Translation(c) @ Ry(-math.degrees(a)) @ Matrix.Identity(4),
                   cham=0.014, uv_world=True, wob=0.004, seed=R.random() * 90)

def p_groin_bay():
    pc = FK.Piece('GroinBay'); R = R_('groin')
    groin_shell(pc)
    for sx in (-1, 1):
        for sy in (-1, 1):
            pier(pc, sx * 2.0, sy * 2.0, R_('gp%d%d' % (sx, sy)))
    # four transverse arches over the bay edges: in the plane x = +-2 (ring about y) and y = +-2 (ring about x)
    for sgn in (-1, 1):
        ring_xz(pc, 'vault', Matrix.Translation(Vector((0, sgn * HALF, SPR))), 1.72, 2.0 + 0.04, 11, 0.5, R_('ra%d' % sgn))
        ring_xz(pc, 'vault', Matrix.Translation(Vector((sgn * HALF, 0, SPR))) @ Rz(90), 1.72, 2.0 + 0.04, 11, 0.5, R_('rb%d' % sgn))
    # diagonal groin ribs, brick-banded stone under the groin line
    for yaw in (45, -45):
        L = HALF * math.sqrt(2); nseg = 12
        pts = []
        for k in range(nseg + 1):
            s = -L + 2 * L * k / nseg
            x = s / math.sqrt(2); pts.append((s, groin_z(x, x) - 0.05))
        for k in range(nseg):
            (s0, z0), (s1, z1) = pts[k], pts[k + 1]
            d = math.hypot(s1 - s0, z1 - z0); a = math.degrees(math.atan2(z1 - z0, s1 - s0))
            mid = Vector(((s0 + s1) / 2, 0, (z0 + z1) / 2))
            FK.add_box(pc, 'brick' if k % 2 else 'vault', (d * 1.14, 0.28, 0.26), Rz(yaw) @ Matrix.Translation(mid) @ Ry(-a),
                       cham=0.012, uv_world=True, wob=0.003, seed=R.random() * 90)
    return pc

# ----------------------------------------------------------------------------- PillarRound
def p_pillar_round():
    pc = FK.Piece('PillarRound'); R = R_('round')
    lathe(pc, [(0, 0), (0.50, 0), (0.52, 0.02), (0.52, 0.12), (0.45, 0.20), (0, 0.20)], Matrix.Identity(4), sides=14, mat='vault', seed=0.3)
    z = 0.20
    for k in range(7):
        r = 0.34 + R.uniform(-0.008, 0.012)
        h = 0.27
        lathe(pc, [(0, z), (r - 0.012, z + 0.003), (r, z + 0.012), (r, z + h - 0.012), (r - 0.012, z + h - 0.003), (0, z + h)],
              Rz(R.uniform(0, 30)) @ Matrix.Translation(Vector((R.uniform(-0.004, 0.004), R.uniform(-0.004, 0.004), 0))), sides=12, mat='vault', seed=R.random() * 9)
        z += h
    # capital: necking ring, cyma flare, abacus
    lathe(pc, [(0, z), (0.36, z), (0.36, z + 0.03), (0.40, z + 0.06), (0.47, z + 0.11), (0.54, z + 0.17), (0.56, z + 0.20), (0, z + 0.20)],
          Matrix.Identity(4), sides=14, mat='vault', seed=2.2)
    box(pc, 'vault', (0.98, 0.98, 0.11), (0, 0, 2.345), cham=0.03, wob=0.004, seed=4.4)     # top at 2.4
    return pc

# ----------------------------------------------------------------------------- walls
def inside_profile(x0, x1, z0, z1, m=0.05):
    r = V.R_OUT - m
    for x in (x0, x1):
        if abs(x) > r:
            return False
    zt = z1
    if zt > V.SPRING:
        for x in (x0, x1):
            if math.hypot(x, zt - V.SPRING) > r:
                return False
    return True

def ashlar_relief(pc, mat, R, skip=None, proud=0.028, y_len=0.05, zmax=4.4):
    """Courses of 0.28 m (x 0.5-1.1 m long, offset joints) in shallow relief on the wall face y = 0 (+y)."""
    z = 0.0; row = 0
    while z + COURSE <= zmax:
        z1 = z + COURSE
        x = -V.R_OUT + (0.0 if row % 2 else R.uniform(0.2, 0.5))
        while x < V.R_OUT:
            w = R.uniform(0.5, 1.1)
            x1 = min(x + w, V.R_OUT)
            if x1 - x > 0.3 and inside_profile(x, x1, z, z1) and not (skip and skip(x, x1, z, z1)):
                box(pc, mat, (x1 - x - 0.014, y_len, COURSE - 0.014), ((x + x1) / 2, y_len / 2 - 0.012 + R.uniform(0, 0.012), (z + z1) / 2),
                    Rx(R.uniform(-0.4, 0.4)), cham=0.01, wob=0.003, seed=R.random() * 80)
            x = x1
        z = z1; row += 1

def p_wall_ashlar():
    pc = FK.Piece('WallAshlar'); R = R_('ashlar')
    V.wall_body(pc, [], -0.5, 0.0, front_key=lambda f, c: 'vault')
    ashlar_relief(pc, 'vault', R)
    return pc

def p_wall_brick_arch():
    pc = FK.Piece('WallBrickArch'); R = R_('brickarch')
    W, ZS = 2.6, 1.7
    cut = (V.arch_profile(0, 0.0, W, ZS, 12), -0.16, 0.12)
    def fk(f, c):
        if abs(c.x) < W / 2 - 0.02 and c.z < ZS + W / 2 - 0.02 and c.y < -0.05:
            return 'brick'
        return 'vault'
    V.wall_body(pc, [cut], -0.5, 0.0, front_key=fk)
    ashlar_relief(pc, 'vault', R, skip=lambda x0, x1, z0, z1: x1 > -W / 2 - 0.34 and x0 < W / 2 + 0.34 and z0 < ZS + W / 2 + 0.34 and
                  ((x1 > -W / 2 - 0.34 and x0 < W / 2 + 0.34) and (z0 < ZS or math.hypot(max(abs(x0), abs(x1)) if True else 0, z0 - ZS) < W / 2 + 0.34)))
    # voussoirs + jambs standing proud around the recess
    ring_xz(pc, 'vault', Matrix.Translation(Vector((0, -0.02, ZS))) @ Rx(0), W / 2 - 0.02, W / 2 + 0.30, 13, 0.18, R, keystone=0.06)
    for sx in (-1, 1):
        z = 0.0
        while z + 0.01 < ZS:
            h = min(COURSE, ZS - z)
            w = 0.30 + (0.1 if int(z / COURSE) % 2 else 0.0)
            box(pc, 'vault', (w, 0.18, h - 0.012), (sx * (W / 2 + w / 2 - 0.0), -0.02, z + h / 2), Rx(R.uniform(-0.4, 0.4)), cham=0.012, wob=0.003, seed=R.random() * 90)
            z += h
    # brick infill detail: a rowlock sill and a few proud bricks to break the recess
    return pc

def p_wall_soot():
    pc = FK.Piece('WallSoot'); R = R_('soot')
    W, ZS = 1.5, 1.3
    cut = (V.arch_profile(0.9, 0.0, W, ZS, 10), -0.18, 0.12)
    V.wall_body(pc, [cut], -0.5, 0.0, front_key=lambda f, c: 'soot')
    ashlar_relief(pc, 'soot', R, skip=lambda x0, x1, z0, z1: x1 > 0.9 - W / 2 - 0.05 and x0 < 0.9 + W / 2 + 0.05 and z0 < ZS + W / 2 + 0.05)
    ring_xz(pc, 'vault', Matrix.Translation(Vector((0.9, -0.02, ZS))), W / 2 - 0.02, W / 2 + 0.22, 9, 0.16, R, keystone=0.04)
    return pc

def p_wall_lime():
    pc = FK.Piece('WallLime'); R = R_('lime')
    V.wall_body(pc, [], -0.5, 0.0, front_key=lambda f, c: 'lime')
    # stone plinth course and a moulded springing string, both proud of the plaster
    x = -V.R_OUT
    while x < V.R_OUT - 0.2:
        w = R.uniform(0.6, 1.1); x1 = min(x + w, V.R_OUT)
        box(pc, 'vault', (x1 - x - 0.014, 0.12, 0.34), ((x + x1) / 2, 0.0, 0.17), Rx(R.uniform(-0.4, 0.4)), cham=0.02, wob=0.003, seed=R.random() * 90)
        x = x1
    for sx in (-1, 1):
        box(pc, 'vault', (0.5, 0.1, 0.14), (sx * 2.75, 0.02, V.SPRING), cham=0.02, wob=0.002, seed=sx + 3)
    for i in range(10):
        a = 180 / 10 * (i + 0.5)
        arc_stone(pc, 'vault', 0, V.SPRING, a, 2.88, 3.0, 2 * 2.88 * math.tan(math.radians(9)) * 1.02, 0.0, 0.12, cham=0.015, R=R, wob=0.002)
    return pc

# ----------------------------------------------------------------------------- light fixtures
def flame(pc, c, h=0.10, r=0.035):
    lathe(pc, [(0, 0), (r, 0.02), (r * 0.8, h * 0.5), (r * 0.3, h * 0.9), (0, h)], Matrix.Translation(Vector(c)), sides=6, mat='flame')

def bars(pc, n, r, z0, z1, w=0.012, cx=(0, 0), seed=1.0):
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        box(pc, 'iron', (w, w, z1 - z0), (cx[0] + r * math.cos(a), cx[1] + r * math.sin(a), (z0 + z1) / 2), Rz(math.degrees(a)), cham=0.002, wob=0.001, seed=seed + k)

def ring(pc, c, r, w=0.014, n=10, seed=0.0):
    pts = [Vector((c[0] + r * math.cos(2 * math.pi * k / n), c[1] + r * math.sin(2 * math.pi * k / n), c[2])) for k in range(n + 1)]
    tube(pc, 'iron', pts, w, w, sides=4)

def p_sconce_cage():
    pc = FK.Piece('SconceCage'); R = R_('sconce')
    # wall plate (y 0..0.02), forged bracket arm, drip pan, caged candle/oil flame; local origin at the mount centre, +y into the room
    box(pc, 'iron', (0.12, 0.025, 0.30), (0, 0.0125, 0), cham=0.004, wob=0.002, seed=1)
    for z in (-0.12, 0.12):
        box(pc, 'iron', (0.02, 0.02, 0.02), (0, 0.03, z), cham=0.003)          # rivet heads
    box(pc, 'iron', (0.035, 0.22, 0.03), (0, 0.13, -0.08), cham=0.004, wob=0.002, seed=2)       # arm
    box(pc, 'iron', (0.025, 0.2, 0.025), (0, 0.12, 0.0), Rx(-24), cham=0.004, wob=0.002, seed=3)   # stay
    c = (0, 0.24, 0.0)
    lathe(pc, [(0, -0.07), (0.07, -0.07), (0.085, -0.045), (0, -0.045)], Matrix.Translation(Vector(c)), sides=10, mat='iron')       # pan
    bars(pc, 6, 0.075, -0.045, 0.16, cx=(c[0], c[1]), seed=7)
    ring(pc, (c[0], c[1], -0.04), 0.075, n=10); ring(pc, (c[0], c[1], 0.16), 0.075, n=10)
    lathe(pc, [(0.075, 0.16), (0.06, 0.20), (0.03, 0.235), (0.012, 0.25), (0, 0.255)], Matrix.Translation(Vector(c)), sides=6, mat='iron')   # cone cap
    ring(pc, (c[0], c[1], 0.07), 0.075, w=0.008, n=10)
    flame(pc, (c[0], c[1], -0.035), h=0.13, r=0.035)
    return pc

def p_lantern_chain():
    pc = FK.Piece('LanternChain'); R = R_('lchain')
    # 8 chain links hanging from the origin (hook at z 0), then a caged lantern 0.34 m tall ending near z -1.0
    z = 0.0
    for k in range(9):
        a = 0.0 if k % 2 == 0 else 90.0
        L = 0.075
        pts = []
        for j in range(9):
            t = 2 * math.pi * j / 8
            u = 0.016 * math.cos(t); v = (L / 2) * math.sin(t)
            pts.append(Vector((u * math.cos(math.radians(a)), u * math.sin(math.radians(a)), z - L / 2 + v)))
        tube(pc, 'iron', pts, 0.0065, 0.0065, sides=4)
        z -= L * 0.82
    zt = z - 0.04          # top ring of the cage
    ring(pc, (0, 0, zt), 0.07, w=0.012, n=10)
    # cage: crown cone, 6 bars, bottom bowl; body 0.36 tall
    h = 0.36; zb = zt - h
    lathe(pc, [(0.17, zt - 0.1), (0.15, zt - 0.06), (0.09, zt - 0.02), (0.045, zt), (0.015, zt + 0.02)], Matrix.Identity(4), sides=8, mat='iron')
    lathe(pc, [(0.17, zt - 0.1), (0.17, zt - 0.12), (0.145, zt - 0.115), (0.145, zt - 0.1)], Matrix.Identity(4), sides=12, mat='iron')
    bars(pc, 8, 0.165, zb + 0.06, zt - 0.1, w=0.014, seed=11)
    ring(pc, (0, 0, zb + 0.06), 0.165, w=0.012, n=12); ring(pc, (0, 0, (zb + zt) / 2 - 0.04), 0.168, w=0.008, n=12)
    lathe(pc, [(0.165, zb + 0.07), (0.14, zb + 0.03), (0.07, zb + 0.005), (0.02, zb), (0, zb - 0.02)], Matrix.Identity(4), sides=10, mat='iron')
    lathe(pc, [(0, zb + 0.08), (0.10, zb + 0.08), (0.10, zb + 0.095), (0, zb + 0.095)], Matrix.Identity(4), sides=10, mat='iron')    # candle plate
    flame(pc, (0, 0, zb + 0.095), h=0.22, r=0.06)
    return pc

def p_lantern_oil():
    pc = FK.Piece('LanternOil'); R = R_('loil')
    # hurricane lantern: tank base, glass chimney, wire cage of 4 posts, top cap, bail handle; origin at the base
    lathe(pc, [(0, 0), (0.085, 0), (0.095, 0.015), (0.095, 0.06), (0.075, 0.09), (0.062, 0.11), (0, 0.11)], Matrix.Identity(4), sides=12, mat='iron')
    lathe(pc, [(0.062, 0.11), (0.052, 0.12), (0.045, 0.135), (0, 0.135)], Matrix.Identity(4), sides=10, mat='iron')
    lathe(pc, [(0.044, 0.13), (0.062, 0.17), (0.068, 0.22), (0.052, 0.27), (0.04, 0.29)], Matrix.Identity(4), sides=12, mat='glass')     # chimney bulb
    flame(pc, (0, 0, 0.125), h=0.12, r=0.022)
    bars(pc, 4, 0.082, 0.11, 0.285, w=0.012, seed=21)
    lathe(pc, [(0.085, 0.285), (0.085, 0.30), (0.05, 0.325), (0.022, 0.335), (0.022, 0.345), (0, 0.345)], Matrix.Identity(4), sides=10, mat='iron')    # top cap
    for k in range(10):    # bail handle: arch over the top, in the x-z plane
        a0 = math.pi * k / 10; a1 = math.pi * (k + 1) / 10
        p0 = Vector((0.09 * math.cos(a0), 0, 0.30 + 0.07 * math.sin(a0))); p1 = Vector((0.09 * math.cos(a1), 0, 0.30 + 0.07 * math.sin(a1)))
        d = p1 - p0
        box(pc, 'iron', (d.length * 1.15, 0.012, 0.012), (p0 + p1) / 2, Ry(-math.degrees(math.atan2(d.z, d.x))), cham=0.002)
    return pc

NEW_PIECES = [('GroinBay', p_groin_bay), ('PillarRound', p_pillar_round), ('WallAshlar', p_wall_ashlar), ('WallBrickArch', p_wall_brick_arch),
              ('WallSoot', p_wall_soot), ('WallLime', p_wall_lime), ('SconceCage', p_sconce_cage), ('LanternChain', p_lantern_chain), ('LanternOil', p_lantern_oil)]

def build_all():
    V.PIECES = V.PIECES + [p for p in NEW_PIECES if p[0] not in dict(V.PIECES)]
    root, obs = V.build()
    return root, obs

# ----------------------------------------------------------------------------- preview (scratch only)
def preview(obs):
    sc = bpy.context.scene
    V.setup_render(sc)
    cam_d = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cam_d); sc.collection.objects.link(cam)
    sc.camera = cam; cam_d.sensor_fit = 'VERTICAL'; cam_d.clip_start = 0.05
    V.world_color(sc, (0.30, 0.26, 0.22), 1.0)
    sun = V.light(sc, 'SUN', 'Key', (0, 0, 10), (1.0, 0.85, 0.66), 3.2, angle=math.radians(4)); sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
    fill = V.light(sc, 'SUN', 'Fill', (0, 0, 10), (0.9, 0.9, 1.0), 0.8, angle=math.radians(40)); fill.rotation_euler = (math.radians(60), 0, math.radians(150))
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.07)); fl = bpy.context.object
    fl.data.materials.append(FK.flat_mat('CV_proxyGround', (0.12, 0.10, 0.085), 0.9))
    sets = {
        'groin': ([('GroinBay', 0, 0)], (7.0, -9.5, 5.5), (0, 0, 2.6), 42),
        'pillars': ([('PillarRound', -2.2, 0), ('Pillar', 0, 0), ('SconceCage', 2.0, 0), ('LanternOil', 3.0, 0)], (0, -7.5, 2.0), (0.6, 0, 1.2), 38),
        'walls': ([('WallAshlar', -6.5, 0), ('WallBrickArch', 0, 0), ('WallSoot', 6.5, 0), ('WallLime', 13.0, 0)], (3.2, -17, 4.0), (3.2, 0, 2.2), 44),
        'lights': ([('SconceCage', -0.8, 0), ('LanternChain', 0, 0), ('LanternOil', 0.8, 0)], (0, -3.2, -0.3), (0.0, 0, -0.45), 38),
    }
    only = (V.FK.arg('--views') or ','.join(sets)).split(',')
    for name in only:
        if name not in sets:
            continue
        lst, frm, to, fov = sets[name]
        for o in obs.values():
            o.hide_render = True
        for pn, x, rz in lst:
            ob = obs[pn]; ob.hide_render = False; ob.rotation_euler = (0, 0, math.radians(rz)); ob.location = (0, 0, 0)
            bpy.context.view_layer.update()
            cs = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
            ob.location = (x - (min(c.x for c in cs) + max(c.x for c in cs)) / 2, -(min(c.y for c in cs) + max(c.y for c in cs)) / 2, 0)
        V.look(cam, Vector(frm), Vector(to)); cam_d.angle = math.radians(fov)
        sc.render.filepath = os.path.join(V.OUT, 'kit_%s.png' % name)
        bpy.ops.render.render(write_still=True); print('rendered', name)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, obs = build_all()
    if '--export' in V.argv:
        V.export_fbx(root, 'CellarVault.fbx')
    if '--blend' in V.argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(V.OUT, 'kit.blend'))
    if '--no-render' not in V.argv:
        preview(obs)
