"""StoneCottage: home kit family 2, the low wide rubble-stone cottage (S class, mass-production spec 5.4 #2).

One storey, ridge along X, door and a small deep-set window on the front, chunky pillowy pale quoins at the corners and
round every opening, a fat chimney (gable end or ridge), deep eaves. Three seeded variants (VARIANTS -> cfg()):
  StoneCottage_A  red tile roof (B4 kit roof), gabled porch hood over the door, gable-end chimney (+X), flower box
  StoneCottage_B  thatch roof, gable-end chimney (-X), gabled dormer in the thatch, flower box
  StoneCottage_C  thatch roof, ridge chimney, low tiled lean-to woodshed on the +X end, no flower box
Seeded per variant: width/depth/wall height (the building body stays inside 5 x 5 m), roof pitch, door side, chimney
offset, rubble and thatch tint, shutter and door paint (B4 palette), wear amount.

Built on the B4 facade kit (sc_facade_kit.py, imported as a library: tile_mat, Piece, add_box, member, quad_grid, window,
kit_door, kit_shutters, kit_roof, kit_flowerbox, the shared B1 tileables, linear `wear` colour attribute). T2: no unique
bake. Rubble = B1 sandstone tinted grey like the bell tower (SC_Rubble); thatch has no tileable, so SC_Thatch is the B1
oak_timber (vertical grain, running down the slope) tinted straw plus layered, billowy geometry (laps, rounded eave and
verge lips, ridge roll). Interior liner is the dark FK_PlinthJoint, so the frame without its parts reads as a cottage
waiting for them: an open doorway into a dark room, bare hinge pintles at the window jambs.

Export (spec 5.2), one FBX per variant, root <Variant> with children:
  Frame          the building without door and shutters, joined (multi-material, `wear` attribute)
  Part_Door      the planked door; object origin = SOCKET_Door (door bottom centre, in the door recess)
  Part_Shutters  every shutter (front and back windows); object origin = SOCKET_Shutters (first front window centre)
  SOCKET_Door, SOCKET_Shutters   empties where the parts attach (SOCKET_Door marks the door side, front)
  FOOTPRINT      flat box (0.1 m) over the building body (walls, plinth, chimney breast, porch, lean-to)
Axes: metres, Blender z up, front (door side) = -Y; pivot = foundation centre at ground (the body footprint is centred
on it). FBX -Z forward, Y up, path_mode STRIP so Unity keeps the slot names.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_cottage.py -- --out <scratch>
      [--variant StoneCottage_A|_B|_C|all] [--export] [--no-render] [--views fl,fr,bl,br,front]
      [--rres 960] [--samples 32] [--frame-only] [--audit]
  --audit re-imports <out>/<Variant>.fbx and prints names, slots, wear, tris, footprint and a normal-facing check.
"""
import sys, os, math, random, zlib, inspect
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
from mathutils.bvhtree import BVHTree
import sc_facade_kit as FK

argv = FK.argv
arg = FK.arg
OUT = FK.OUT
TILE = FK.TILE

def repatch(fn, subs):
    """Re-define a kit function with a few text edits (tris budget), in the kit's own namespace."""
    src = inspect.getsource(getattr(FK, fn))
    for a, b in subs:
        assert a in src, (fn, a)
        src = src.replace(a, b)
    exec(compile(src, FK.__file__, 'exec'), FK.__dict__)

repatch('window', [("    s = 0.16\n", "    s = 0.22\n"),
                   ("            add_box(pc, 'iron', (ln, 0.010, 0.012), xf, cham=0.0, long_axis=0, seed=i)",
                    "            pa = Vector((a[0], gy - 0.008, a[1])); pb = Vector((b[0], gy - 0.008, b[1])); dd = (pb - pa).normalized()\n"
                    "            nn = Vector((-dd.z, 0, dd.x)) * 0.006\n"
                    "            quad_grid(pc, 'iron', [pa - nn, pb - nn, pb + nn, pa + nn], 1, 1, uv_fn=lambda c: (c.x, c.z))")])
repatch('kit_roof', [('for i in range(13):', 'for i in range(9):'), ('i * (2 * W - 0.6) / 12', 'i * (2 * W - 0.6) / 8')])

# ----------------------------------------------------------------------------- variants
VARIANTS = {
    'StoneCottage_A': dict(roof='tile', chimney='gable+', porch=True, lean=False, dormer=False, flower=True,
                           hex='#3e5670', rubble=(0.54, 0.56, 0.58), thatch=None),
    'StoneCottage_B': dict(roof='thatch', chimney='gable-', porch=False, lean=False, dormer=True, flower=True,
                           hex='#4a6a48', rubble=(0.62, 0.58, 0.52), thatch=(2.55, 2.35, 1.20)),
    'StoneCottage_C': dict(roof='thatch', chimney='ridge', porch=False, lean=True, dormer=False, flower=False,
                           hex='#6a2e29', rubble=(0.56, 0.56, 0.57), thatch=(2.05, 1.92, 1.30)),
}
for _n in VARIANTS:
    VARIANTS[_n]['seed'] = zlib.crc32(_n.encode())

PH = 0.34           # plinth height
T = 0.42            # wall thickness
DOORW, DOORZ0, DOORZ1 = 0.95, 0.22, 2.05
WIN = (0.70, 0.92, 1.72)   # window clear width, sill z, head z
LL = 1.10           # lean-to depth

def cfg(name):
    V = dict(VARIANTS[name]); R = random.Random(V['seed'])
    gable = V['chimney'].startswith('gable')
    V['w'] = round(R.uniform(3.80, 4.15) if gable else R.uniform(3.50, 3.65), 2)
    V['d'] = round(R.uniform(3.45, 3.60) if V['porch'] else R.uniform(3.55, 3.95), 2)
    V['WH'] = round(R.uniform(2.30, 2.55), 2)
    V['pitch'] = round(R.uniform(36, 40) if V['roof'] == 'tile' else R.uniform(44, 48), 1)
    V['ds'] = R.choice((-1, 1))                       # door side: -1 door left of the window
    V['door_x'] = round(-V['ds'] * (0.55 + R.uniform(-0.05, 0.10)), 2)
    V['win_x'] = round(V['ds'] * (V['w'] / 2 - 0.95), 2)
    V['chim_off'] = round(R.uniform(-0.25, 0.25), 2)
    V['wear'] = round(R.uniform(1.1, 1.35), 2)
    V['paint'] = FK._lin(V['hex'])
    return V

# ----------------------------------------------------------------------------- materials
MATINFO = {}
_orig_tile = FK.tile_mat
def _tile_rec(name, folder, stem=None, tint=(1, 1, 1), rough_add=0.0, normal=1.0, paint=None):
    MATINFO[name] = dict(folder=folder, tint=tuple(tint), normal=normal, rough_add=rough_add,
                         paint=None if paint is None else tuple(round(x, 4) for x in paint))
    return _orig_tile(name, folder, stem, tint, rough_add, normal, paint)
FK.tile_mat = _tile_rec

CUR = {}
_orig_M = FK.M
def _M(k):
    if k == 'rubble':
        return FK.tile_mat('SC_Rubble', 'B1_town/sandstone', tint=CUR['rubble'], normal=1.4)
    if k == 'thatch':
        return FK.tile_mat('SC_Thatch', 'B1_town/oak_timber', tint=CUR['thatch'] or (1.7, 1.6, 1.0), normal=1.1, rough_add=0.2)
    return _orig_M(k)
FK.M = _M

# ----------------------------------------------------------------------------- mesh helpers
EXT = [9e9, -9e9, 9e9, -9e9]
def ext(x0, x1, y0, y1):
    EXT[0] = min(EXT[0], x0); EXT[1] = max(EXT[1], x1); EXT[2] = min(EXT[2], y0); EXT[3] = max(EXT[3], y1)

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

def _uv_world(p, n):
    a = max(range(3), key=lambda i: abs(n[i]))
    if a == 2:
        return (p.x / TILE, p.y / TILE)
    return ((p.x if a == 1 else p.y) / TILE, p.z / TILE)

def poly(pc, mat, pts, towards=None, away=None, uvf=None):
    """One polygon from points; flipped so its normal looks toward/away from a reference point. Box-projected metre UVs."""
    pts = [Vector(p) for p in pts]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    bm.normal_update()
    ctr = f.calc_center_median()
    if towards is not None and f.normal.dot(Vector(towards) - ctr) < 0:
        bmesh.ops.reverse_faces(bm, faces=[f])
    if away is not None and f.normal.dot(Vector(away) - ctr) > 0:
        bmesh.ops.reverse_faces(bm, faces=[f])
    bm.normal_update()
    n = f.normal.copy()
    for l in f.loops:
        l[uvl].uv = uvf(l.vert.co) if uvf else _uv_world(l.vert.co, n)
    FK._merge(pc, bm, mat)

def lump_fn(p, amp):
    return amp * (0.65 * noise.noise(p * 2.3) + 0.35 * noise.noise(p * 6.1 + Vector((7.0, 1.0, 3.0))))

def _hole_fall(u, z, holes):
    f = 1.0
    for (a, b, c0, c1) in holes:
        dx = max(a - u, 0.0, u - b); dz = max(c0 - z, 0.0, z - c1)
        f = min(f, math.hypot(dx, dz) / 0.22)
    return min(1.0, f)

def _grid_faces(pc, mat, org, n, t, us, zrows, holes, lump, skip_hole=True):
    """zrows: list over us of the row z-values (equal length lists). Builds a lumpy panel on the frame (org, t, n)."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    V = {}
    for i, u in enumerate(us):
        for j, z in enumerate(zrows[i]):
            p = org + t * u + Vector((0, 0, z))
            d = lump_fn(p, lump) * _hole_fall(u, z, holes)
            V[(i, j)] = bm.verts.new(p + n * d)
    for i in range(len(us) - 1):
        for j in range(len(zrows[i]) - 1):
            cu = (us[i] + us[i + 1]) / 2
            cz = (zrows[i][j] + zrows[i][j + 1]) / 2
            if skip_hole and any(h[0] < cu < h[1] and h[2] < cz < h[3] for h in holes):
                continue
            bm.faces.new((V[(i, j)], V[(i + 1, j)], V[(i + 1, j + 1)], V[(i, j + 1)]))
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.normal_update()
    for f in bm.faces:
        for l in f.loops:
            l[uvl].uv = _uv_world(l.vert.co, n)
    FK._merge(pc, bm, mat)

def panel(pc, mat, org, nrm, u0, u1, z0, z1, holes=(), cell=0.20, lump=0.045):
    n = Vector(nrm); t = Vector((0, 0, 1)).cross(n); org = Vector(org)
    k = lambda a, b: max(1, round((b - a) / cell))
    us = sorted(set([u0, u1] + [h[0] for h in holes] + [h[1] for h in holes] + [u0 + (u1 - u0) * i / k(u0, u1) for i in range(1, k(u0, u1))]))
    zs = sorted(set([z0, z1] + [h[2] for h in holes] + [h[3] for h in holes] + [z0 + (z1 - z0) * i / k(z0, z1) for i in range(1, k(z0, z1))]))
    us = [u for u in us if u0 <= u <= u1]; zs = [z for z in zs if z0 <= z <= z1]
    _grid_faces(pc, mat, org, n, t, us, [zs] * len(us), holes, lump)

def panel_top(pc, mat, org, nrm, u0, u1, z0, topfn, cell=0.22, nv=4, lump=0.045):
    """Panel whose top edge follows topfn(u) (gable triangles, lean-to walls)."""
    n = Vector(nrm); t = Vector((0, 0, 1)).cross(n); org = Vector(org)
    nu = max(1, round((u1 - u0) / cell))
    us = [u0 + (u1 - u0) * i / nu for i in range(nu + 1)]
    rows = [[z0 + (max(z0 + 0.02, topfn(u)) - z0) * j / nv for j in range(nv + 1)] for u in us]
    _grid_faces(pc, mat, org, n, t, us, rows, [], lump)

def reveal(pc, org, nrm, a, b, z0, z1, depth, mat='stone'):
    """Four cut-stone faces lining an opening from the wall face to `depth` behind it."""
    n = Vector(nrm); t = Vector((0, 0, 1)).cross(n); org = Vector(org)
    P = lambda u, z, dd: org + t * u + Vector((0, 0, z)) - n * dd
    ctr = P((a + b) / 2, (z0 + z1) / 2, depth / 2)
    for (p0, p1, p2, p3) in [(P(a, z0, 0), P(a, z0, depth), P(a, z1, depth), P(a, z1, 0)),
                             (P(b, z0, 0), P(b, z0, depth), P(b, z1, depth), P(b, z1, 0)),
                             (P(a, z1, 0), P(b, z1, 0), P(b, z1, depth), P(a, z1, depth)),
                             (P(a, z0, 0), P(b, z0, 0), P(b, z0, depth), P(a, z0, depth))]:
        poly(pc, mat, [p0, p1, p2, p3], towards=ctr)

def box_xf(centre, n):
    """Placement for a box whose local x runs along the wall (t), local y outward (n), z up."""
    n = Vector(n); t = Vector((0, 0, 1)).cross(n)
    return Matrix.Translation(centre) @ Matrix((t, n, Vector((0, 0, 1)))).transposed().to_4x4()

def wall_pt(org, n, u, z, out=0.0):
    n = Vector(n); t = Vector((0, 0, 1)).cross(n)
    return Vector(org) + t * u + Vector((0, 0, z)) + n * out

# ----------------------------------------------------------------------------- stone dressings
def surround(pc, org, n, a, b, z0, z1, door=False, seed=0.0, sill=False):
    """Pale long-and-short jambs and a chunky lintel round an opening (u range a..b, z0..z1), proud of the lumpy wall."""
    R = FK.rng('sur%f' % seed)
    xf = lambda u, z, o: box_xf(wall_pt(org, n, u, z, o), n)
    nrm = Vector(n)
    h = z1 - z0
    cnt = max(3, int(h / 0.30))
    hc = h / cnt
    for side in (-1, 1):
        for i in range(cnt):
            long_ = (i + (side > 0)) % 2 == 0
            wj = (0.31 if long_ else 0.19) + R.uniform(-0.015, 0.015)
            u = (a - wj / 2 + 0.015) if side < 0 else (b + wj / 2 - 0.015)
            z = z0 + hc * (i + 0.5)
            FK.add_box(pc, 'stone', (wj, 0.20, hc - 0.025), xf(u, z, 0.0) @ Matrix.Rotation(math.radians(R.uniform(-1.2, 1.2)), 4, 'Y'),
                       cham=0.04, segs=1, uv_world=True, wob=0.008, seed=R.random() * 90)
    lw = (b - a) + 0.70
    FK.add_box(pc, 'stone', (lw, 0.22, 0.22 if not door else 0.28), xf((a + b) / 2, z1 + (0.11 if not door else 0.14), 0.02),
               cham=0.05, segs=2, uv_world=True, wob=0.008, seed=seed + 3.0)
    if sill:
        FK.add_box(pc, 'stone', ((b - a) + 0.42, 0.26, 0.11), xf((a + b) / 2, z0 - 0.045, 0.03) @ Matrix.Rotation(math.radians(-4), 4, 'X'),
                   cham=0.03, segs=2, uv_world=True, wob=0.006, seed=seed + 4.0)

def pintles(pc, front_org, a, b, z0, z1, plane_out, k):
    """Bare shutter hinge pins on the jamb stones (front frame: wall plane y = 0 facing -Y, built then absorbed)."""
    for xi, side in ((a, -1), (b, 1)):
        for zz in (z0 + 0.14, z1 - 0.14):
            FK.add_box(pc, 'iron', (0.045, 0.045, 0.13), Matrix.Translation((xi + side * 0.0, plane_out - 0.125, zz)),
                       cham=0.008, long_axis=2, seed=zz + xi)
            FK.add_box(pc, 'iron', (0.15, 0.02, 0.035), Matrix.Translation((xi - side * 0.07, plane_out - 0.118, zz)),
                       cham=0.004, long_axis=0, seed=zz + k)

def plinth_seg(pc, p0, p1, n, seed, gap=None):
    """A course of big pale foundation stones along p0 -> p1 (outward normal n), broken for a gap (u range) if given."""
    R = FK.rng('pl%f' % seed)
    p0 = Vector(p0); p1 = Vector(p1); n = Vector(n)
    L = (p1 - p0).length; tdir = (p1 - p0) / L
    x = 0.0
    while x < L - 0.05:
        ln = min(R.uniform(0.62, 1.05), L - x)
        if L - (x + ln) < 0.28:
            ln = L - x
        xa, xb = x, x + ln
        x += ln
        if gap and xa < gap[1] and xb > gap[0]:
            # clip the stone around the gap
            segs = [(xa, min(xb, gap[0])), (max(xa, gap[1]), xb)]
        else:
            segs = [(xa, xb)]
        for (sa, sb) in segs:
            if sb - sa < 0.12:
                continue
            g = 0.03
            c = p0 + tdir * ((sa + sb) / 2) + Vector((0, 0, PH / 2)) + n * (0.10 - 0.15)
            size = (sb - sa - g, 0.30, PH - 0.01 + R.uniform(-0.02, 0.02))
            xf = Matrix.Translation(c) @ Matrix((tdir, n, Vector((0, 0, 1)))).transposed().to_4x4() @ \
                Matrix.Rotation(math.radians(R.uniform(-1.0, 1.0)), 4, 'Y')
            FK.add_box(pc, 'stone', size, xf, cham=0.045, segs=2, uv_world=True, wob=0.012, seed=R.random() * 90)

def quoins(pc, cx, cy, sx, sy, WH, seed):
    """Alternating long-and-short pale corner stones at one wall corner (cx, cy), going outward by (sx, sy)."""
    R = FK.rng('quo%f' % seed)
    nn = max(5, int((WH - PH) / 0.32))
    h = (WH - PH) / nn
    for i in range(nn):
        ln, sh = R.uniform(0.60, 0.74), R.uniform(0.34, 0.42)
        lx, ly = (ln, sh) if (i + (sx * sy > 0)) % 2 else (sh, ln)
        c = Vector((cx - sx * (lx / 2 - 0.06), cy - sy * (ly / 2 - 0.06), PH + h * (i + 0.5)))
        FK.add_box(pc, 'stone', (lx, ly, h - 0.03), Matrix.Translation(c) @ Matrix.Rotation(math.radians(R.uniform(-1.5, 1.5)), 4, 'Z'),
                   cham=0.05, segs=2, uv_world=True, wob=0.009, seed=R.random() * 99)

# ----------------------------------------------------------------------------- chimney
def pots(pc, base):
    for k, (px, ph, tip) in enumerate(((-0.17, 0.50, 0.0), (0.19, 0.40, 4.0))):
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        prof = [(0.14, 0.0), (0.15, 0.05), (0.12, 0.12), (0.10, ph * 0.7), (0.11, ph - 0.06), (0.135, ph - 0.03), (0.135, ph), (0.09, ph)]
        segs = 10; rings = []
        for (r, z) in prof:
            rings.append([bm.verts.new(Vector((r * math.cos(2 * math.pi * s / segs), r * math.sin(2 * math.pi * s / segs), z))) for s in range(segs)])
        for j in range(len(rings) - 1):
            for s in range(segs):
                f = bm.faces.new((rings[j][s], rings[j][(s + 1) % segs], rings[j + 1][(s + 1) % segs], rings[j + 1][s]))
                for l in f.loops:
                    co = l.vert.co
                    l[uvl].uv = (math.atan2(co.y, co.x) * 0.15 / TILE, co.z / TILE)
        bm.normal_update()
        bm.transform(base @ Matrix.Translation((px, 0, 0)) @ Matrix.Rotation(math.radians(tip), 4, 'X'))
        FK._merge(pc, bm, 'roof')

def chimney_stack(pc, cx, cy, z_from, z_top, sx, sy, seed):
    """Fat rubble shaft (sx x sy) from z_from to z_top with a two-course pale oversailing cap and two clay pots."""
    h = z_top - z_from
    def taper(p, h=h):
        t = (p.z + h / 2) / h
        return Vector((p.x * -0.04 * t, p.y * -0.04 * t, 0))
    FK.add_box(pc, 'rubble', (sx, sy, h), Matrix.Translation((cx, cy, z_from + h / 2)), cham=0.05, cuts=(2, 2, max(2, int(h / 0.4))),
               bend=taper, wob=0.012, seed=seed, uv_world=True)
    for j, (dx, dy, zz, hh) in enumerate(((0.16, 0.16, 0.02, 0.15), (0.08, 0.08, 0.14, 0.12))):
        FK.add_box(pc, 'stone', (sx + dx, sy + dy, hh), Matrix.Translation((cx, cy, z_top + zz)), cham=0.035, segs=2,
                   uv_world=True, wob=0.006, seed=seed + 1 + j)
    pots(pc, Matrix.Translation((cx, cy, z_top + 0.20)))

def chimney_gable(pc, c, sgn, ridge_top):
    w, WH = c['w'], c['WH']
    xw = sgn * w / 2
    cy = c['chim_off']
    # breast: wide rubble mass from the ground to above the eaves, with a pale weathering slab on top
    FK.add_box(pc, 'rubble', (0.78, 1.40, WH + 0.30), Matrix.Translation((xw + sgn * 0.27, cy, (WH + 0.30) / 2)), cham=0.05,
               cuts=(1, 2, 5), wob=0.012, seed=31.0, uv_world=True)
    FK.add_box(pc, 'stone', (0.86, 1.48, 0.13), Matrix.Translation((xw + sgn * 0.27, cy, WH + 0.30 + 0.05)), cham=0.04, segs=2,
               uv_world=True, wob=0.006, seed=32.0)
    zb = WH + 0.36
    chimney_stack(pc, xw + sgn * 0.27 + sgn * 0.02, cy, zb, ridge_top + 0.60, 0.80, 1.18, 33.0)
    # pale quoins up the stack corners
    R = FK.rng('chq%d' % sgn)
    nn = int((ridge_top + 0.60 - zb) / 0.30)
    for i in range(nn):
        for sy in (-1, 1):
            ln = R.uniform(0.28, 0.40)
            FK.add_box(pc, 'stone', (0.12 + (0.1 if i % 2 else 0.0), ln if i % 2 else 0.12, 0.27),
                       Matrix.Translation((xw + sgn * (0.27 + 0.33), cy + sy * 0.46, zb + 0.15 + i * 0.30)),
                       cham=0.03, segs=2, uv_world=True, wob=0.006, seed=R.random() * 50)
    ext(min(xw, xw + sgn * 0.66), max(xw, xw + sgn * 0.66), cy - 0.70, cy + 0.70)

# ----------------------------------------------------------------------------- roofs
def mirror_roof(roof):
    """The kit details one slope; drop its plain back slope and mirror the detailed one onto that side."""
    bm = roof.bm
    bm.faces.ensure_lookup_table()
    def ridge(cc):
        return abs(cc.y) < 0.2 and cc.z > FK.ridge_z(cc.x) - 0.12
    kill = [f for f in bm.faces if f.calc_center_median().y > 0.001 and not ridge(f.calc_center_median())]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    dup_src = [f for f in bm.faces if f.calc_center_median().y < -0.001 and not ridge(f.calc_center_median())]
    ret = bmesh.ops.duplicate(bm, geom=dup_src)
    nv = [e for e in ret['geom'] if isinstance(e, bmesh.types.BMVert)]
    nf = [e for e in ret['geom'] if isinstance(e, bmesh.types.BMFace)]
    for v in nv:
        v.co.y = -v.co.y
    bmesh.ops.reverse_faces(bm, faces=nf)
    bm.normal_update()

def tile_roof(c):
    w, d, WH = c['w'], c['d'], c['WH']
    tanp = math.tan(math.radians(c['pitch']))
    FK.W = w / 2; FK.YU = -d / 2; FK.DEPTH = d / 2
    FK.PLATE1 = WH - 0.02; FK.PLATE0 = WH - 0.3
    FK.OVER_EAVE = 0.52; FK.OVER_VERGE = 0.30; FK.RIDGE_SAG = 0.06
    FK.RIDGE = FK.PLATE1 + 0.22 + (d / 2) * tanp
    FK.EAVE_Z = FK.roof_surface(0, 0).z
    roof = FK.kit_roof(); mirror_roof(roof)
    zt = FK.PLATE1 + 0.22
    return roof, (lambda y: zt - 0.15 + (d / 2 - abs(y)) * tanp), FK.RIDGE + 0.15, zt

class ThatchSurf:
    """Plan -> thick, billowy thatch: top surface function, rounded lips, ridge roll."""
    def __init__(self, c):
        self.w, self.d, self.WH = c['w'], c['d'], c['WH']
        self.tanp = math.tan(math.radians(c['pitch'])); self.cosp = math.cos(math.radians(c['pitch']))
        self.zt = self.WH + 0.26
        self.th = 0.36; self.r = self.th / 2
        self.Xe = self.w / 2 + 0.36; self.Ye = self.d / 2 + 0.52
        self.X1 = self.Xe - self.r; self.Y1 = self.Ye - self.r
        self.zr = self.zt + (self.d / 2) * self.tanp
    def base(self, x, y):
        ys = abs(y)
        z = self.zt + (self.d / 2 - ys) * self.tanp
        e = max(0.0, (ys - self.d / 2) / 0.52)
        z += 0.12 * e * e                                  # eave kick: the slope flattens out over the wall
        u = x / self.Xe
        z -= 0.06 * (1 - u * u) * (1 - ys / self.Ye)       # the ridge settles in the middle
        return z
    def smooth(self, x, y):
        z = self.base(x, y)
        z += 0.05 * noise.noise(Vector((x * 0.8, y * 0.8, 3.1))) + 0.02 * noise.noise(Vector((x * 2.4, y * 2.4, 9.3)))
        # billow: pillowy swell mid-slope, falling off toward the gable edges
        ys = abs(y); u = x / self.Xe
        z += 0.06 * math.sin(math.pi * min(1, ys / self.Ye)) * (1 - u * u)
        return z
    def top(self, x, y):
        z = self.smooth(x, y)
        s = (self.Ye - abs(y)) * 1.0                       # up from the eave
        f = (s / 0.40) % 1.0
        z += 0.050 * (f ** 3 - 0.25) if abs(y) < self.Y1 - 0.1 else 0.0   # laps: a gentle rise, a sharp step down
        return z

def thatch_roof(c):
    S = ThatchSurf(c)
    pc = FK.Piece('Kit_Thatch')
    sec = 1.0 / FK.TILE
    Sc = 1 / S.cosp
    nx, ny = 36, 28
    xs = [-S.X1 + 2 * S.X1 * i / nx for i in range(nx + 1)]
    ys = [-S.Y1 + 2 * S.Y1 * j / (2 * ny) for j in range(2 * ny + 1)]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    V = {}
    for i, x in enumerate(xs):
        for j, y in enumerate(ys):
            V[(i, j)] = bm.verts.new((x, y, S.top(x, y)))
    for i in range(nx):
        for j in range(2 * ny):
            f = bm.faces.new((V[(i, j)], V[(i + 1, j)], V[(i + 1, j + 1)], V[(i, j + 1)]))
            for l in f.loops:
                co = l.vert.co
                l[uvl].uv = (co.x / TILE, abs(co.y) * Sc / TILE)
    bm.normal_update()
    FK._merge(pc, bm, 'thatch')
    # rounded lips: eaves (front/back) and gable verges, a half-round of thatch each
    K = 6
    def lip_mesh(kind, sgn):
        """kind 'eave': along x at y = sgn*Y1 ; 'verge': along y at x = sgn*X1."""
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        n_pts = 36 if kind == 'eave' else 24
        rows = []
        for q in range(n_pts + 1):
            if kind == 'eave':
                x = -S.X1 + 2 * S.X1 * q / n_pts; y = sgn * S.Y1
                p0 = Vector((x, y, S.smooth(x, y))); o = Vector((0, sgn, 0))
            else:
                y = -S.Y1 + 2 * S.Y1 * q / n_pts; x = sgn * S.X1
                p0 = Vector((x, y, S.smooth(x, y))); o = Vector((sgn, 0, 0))
            row = []
            for k in range(K + 1):
                ph = math.pi * k / K
                row.append(bm.verts.new(p0 + o * (S.r * math.sin(ph)) + Vector((0, 0, -S.r * (1 - math.cos(ph))))))
            rows.append(row)
        for q in range(n_pts):
            for k in range(K):
                f = bm.faces.new((rows[q][k], rows[q + 1][k], rows[q + 1][k + 1], rows[q][k + 1]))
                for l in f.loops:
                    co = l.vert.co
                    if kind == 'eave':
                        arc = S.r * (k / K) * math.pi
                        l[uvl].uv = (co.x / TILE, (abs(S.Y1) * Sc + arc) / TILE)
                    else:
                        arc = S.r * (k / K) * math.pi
                        l[uvl].uv = ((co.x + sgn * 0) / TILE + 0.0, (abs(co.y) * Sc) / TILE + arc * 0.0)
        bm.normal_update()
        for f in list(bm.faces):
            ctr = f.calc_center_median()
            if kind == 'eave':
                cen = Vector((ctr.x, sgn * S.Y1, S.smooth(ctr.x, sgn * S.Y1) - S.r))
            else:
                cen = Vector((sgn * S.X1, ctr.y, S.smooth(sgn * S.X1, ctr.y) - S.r))
            if f.normal.dot(ctr - cen) < 0:
                bmesh.ops.reverse_faces(bm, faces=[f])
        bm.normal_update()
        FK._merge(pc, bm, 'thatch')
    for sgn in (-1, 1):
        lip_mesh('eave', sgn)
        lip_mesh('verge', sgn)
        for sy in (-1, 1):   # corner caps
            bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
            x, y = sgn * S.X1, sy * S.Y1
            bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=S.r)
            bm.transform(Matrix.Translation((x, y, S.smooth(x, y) - S.r)))
            for f in bm.faces:
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.x / TILE, l.vert.co.y / TILE)
            FK._merge(pc, bm, 'thatch')
    # underside: three rows (eave, ridge, eave), dark oak seen from below
    pu = [(-S.X1, -S.Y1), (S.X1, -S.Y1), (S.X1, 0.0), (-S.X1, 0.0), (S.X1, S.Y1), (-S.X1, S.Y1)]
    zu = lambda x, y: S.smooth(x, y) - S.th
    ref_in = Vector((0, 0, S.zt))
    for (a, b, cc, dd) in [((-S.X1, -S.Y1), (S.X1, -S.Y1), (S.X1, 0.0), (-S.X1, 0.0)),
                           ((-S.X1, 0.0), (S.X1, 0.0), (S.X1, S.Y1), (-S.X1, S.Y1))]:
        pts = [Vector((p[0], p[1], zu(*p))) for p in (a, b, cc, dd)]
        poly(pc, 'oak', pts, away=Vector((0, 0, S.zr + 2)))
    # ridge roll along x: a fat half-round of thatch, bound, with rounded ends
    rr = 0.25; segs = 10; nxr = 28
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rows = []
    for q in range(nxr + 1):
        x = -S.X1 - 0.06 + (2 * S.X1 + 0.12) * q / nxr
        u = abs(x) / (S.X1 + 0.06)
        end = max(0.0, (u - 0.80) / 0.20)
        sc = math.sqrt(max(0.0, 1 - end * end * 0.92))                 # round the end
        xe = x + math.copysign(0.0, x)
        row = []
        zc = S.smooth(x, 0.0) - 0.03
        for k in range(segs + 1):
            ph = math.pi * k / segs
            yy = -(rr + 0.05) * math.cos(ph) * sc
            zz = zc + (rr * 0.9 * math.sin(ph) + 0.015 * noise.noise(Vector((x * 4, k, 0)))) * sc
            if k in (0, segs):
                zz = S.smooth(x, yy) - 0.04
            row.append(bm.verts.new((x, yy, zz)))
        rows.append(row)
    for q in range(nxr):
        for k in range(segs):
            f = bm.faces.new((rows[q][k], rows[q + 1][k], rows[q + 1][k + 1], rows[q][k + 1]))
            for l in f.loops:
                co = l.vert.co
                l[uvl].uv = (co.x / TILE, (math.atan2(co.z - S.zr + 0.5, co.y) * rr) / TILE)
    bm.normal_update()
    for f in list(bm.faces):
        ctr = f.calc_center_median()
        if f.normal.dot(ctr - Vector((ctr.x, 0.0, S.smooth(ctr.x, 0.0) - 0.20))) < 0:
            bmesh.ops.reverse_faces(bm, faces=[f])
    bm.normal_update()
    FK._merge(pc, bm, 'thatch')
    bmesh.ops.remove_doubles(pc.bm, verts=pc.bm.verts[:], dist=0.004)
    pc.bm.normal_update()
    return pc, S

# ----------------------------------------------------------------------------- slabs, dark rooms
def slab(pc, mat, p00, p10, p11, p01, thick, nu=3, nv=3, amp=0.0, edge_mat=None, inner=None):
    """Thick roof slab: lumpy top over corners (p00,p10,p11,p01), underside and four edge faces. p* are top-surface points."""
    p00, p10, p11, p01 = [Vector(p) for p in (p00, p10, p11, p01)]
    nrm = (p10 - p00).cross(p01 - p00).normalized()
    if nrm.z < 0:
        nrm = -nrm
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    V = {}
    for j in range(nv + 1):
        for i in range(nu + 1):
            u, v = i / nu, j / nv
            p = p00.lerp(p10, u).lerp(p01.lerp(p11, u), v)
            p = p + nrm * lump_fn(p * 1.7, amp) * math.sin(math.pi * u) * math.sin(math.pi * v) if amp else p
            V[(i, j)] = bm.verts.new(p)
    for i in range(nu):
        for j in range(nv):
            bm.faces.new((V[(i, j)], V[(i + 1, j)], V[(i + 1, j + 1)], V[(i, j + 1)]))
    bm.normal_update()
    for f in list(bm.faces):
        if f.normal.dot(nrm) < 0:
            bmesh.ops.reverse_faces(bm, faces=[f])
    bm.normal_update()
    for f in bm.faces:
        for l in f.loops:
            l[uvl].uv = (l.vert.co.x / TILE + l.vert.co.y / TILE * 0.7, l.vert.co.z / TILE * 1.3)
    FK._merge(pc, bm, mat)
    corners = [p00, p10, p11, p01]
    ctr = sum(corners, Vector()) / 4 - nrm * thick / 2
    poly(pc, edge_mat or 'oak', [c - nrm * thick for c in reversed(corners)], away=ctr + nrm * 3)
    for i in range(4):
        a, b = corners[i], corners[(i + 1) % 4]
        poly(pc, edge_mat or 'oak', [a, b, b - nrm * thick, a - nrm * thick], away=ctr)

def dark_room(pc, x0, x1, y0, y1, z0, z1, skip_front=False, door=None):
    """Inward-facing dark liner box (FK_PlinthJoint) so openings never show the sky. door=(u0,u1,zt) cuts the front wall (y0)."""
    ctr = Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    P = lambda x, y, z: Vector((x, y, z))
    poly(pc, 'mortar', [P(x0, y0, z0), P(x1, y0, z0), P(x1, y1, z0), P(x0, y1, z0)], towards=ctr)
    poly(pc, 'mortar', [P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1)], towards=ctr)
    poly(pc, 'mortar', [P(x0, y1, z0), P(x1, y1, z0), P(x1, y1, z1), P(x0, y1, z1)], towards=ctr)
    poly(pc, 'mortar', [P(x0, y0, z0), P(x0, y1, z0), P(x0, y1, z1), P(x0, y0, z1)], towards=ctr)
    poly(pc, 'mortar', [P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1)], towards=ctr)
    if not skip_front:
        if door is None:
            poly(pc, 'mortar', [P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1)], towards=ctr)
        else:
            a, b, zt = door
            poly(pc, 'mortar', [P(x0, y0, z0), P(a, y0, z0), P(a, y0, z1), P(x0, y0, z1)], towards=ctr)
            poly(pc, 'mortar', [P(b, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(b, y0, z1)], towards=ctr)
            poly(pc, 'mortar', [P(a, y0, zt), P(b, y0, zt), P(b, y0, z1), P(a, y0, z1)], towards=ctr)

# ----------------------------------------------------------------------------- assembly
SIDE_XF = None
_last = [0]
def mark(F, label):
    if '--tris' in argv:
        n = sum(len(f.verts) - 2 for f in F.bm.faces)
        print('  tris +%5d  %s' % (n - _last[0], label)); _last[0] = n

def side_xf(side, w, d):
    if side == 'front':
        return Matrix.Translation((0, -d / 2, 0))
    if side == 'back':
        return Matrix.Translation((0, d / 2, 0)) @ Matrix.Rotation(math.pi, 4, 'Z')
    if side == 'right':
        return Matrix.Translation((w / 2, 0, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'Z')
    return Matrix.Translation((-w / 2, 0, 0)) @ Matrix.Rotation(-math.pi / 2, 4, 'Z')

NORMALS = {'front': (0, -1, 0), 'back': (0, 1, 0), 'right': (1, 0, 0), 'left': (-1, 0, 0)}

def build(name):
    c = cfg(name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    FK.MATS.clear(); FK.TIMBERS.clear(); MATINFO.clear()
    CUR.clear(); CUR.update(rubble=c['rubble'], thatch=c['thatch'])
    FK.SHUTTER_PAINT = FK.DOOR_PAINT = c['paint']
    EXT[:] = [9e9, -9e9, 9e9, -9e9]
    w, d, WH = c['w'], c['d'], c['WH']
    F = FK.Piece('Frame'); D = FK.Piece('Part_Door'); SH = FK.Piece('Part_Shutters')
    R = random.Random(c['seed'])
    Z = Vector((0, 0, 1))
    chim_side = {'gable+': 1, 'gable-': -1}.get(c['chimney'])
    lean_side = 1 if c['lean'] else 0
    free_gable = -chim_side if chim_side else (-1 if c['lean'] else 1)
    # ---- roof first (it fixes the gable top lines and the ridge height)
    if c['roof'] == 'tile':
        roof, under, ridge_top, zt = tile_roof(c)
        absorb(F, roof, Matrix.Identity(4))
    else:
        roof, S = thatch_roof(c)
        absorb(F, roof, Matrix.Identity(4))
        under = lambda y: S.zt - S.th * 0.78 + (d / 2 - abs(y)) * S.tanp
        ridge_top = S.zr + 0.28; zt = S.zt
    mark(F, 'roof')
    # ---- walls
    front_wins = [(c['win_x'] - WIN[0] / 2, c['win_x'] + WIN[0] / 2, WIN[1], WIN[2])]
    door = (c['door_x'] - DOORW / 2, c['door_x'] + DOORW / 2, DOORZ0, DOORZ1)
    back_wins = [(-0.95 - WIN[0] / 2, -0.95 + WIN[0] / 2, WIN[1], WIN[2]), (0.95 - WIN[0] / 2, 0.95 + WIN[0] / 2, WIN[1], WIN[2])]
    gw = (-0.30, 0.30, 1.00, 1.70)
    holes = {'front': front_wins + [door], 'back': back_wins, 'right': [], 'left': []}
    if free_gable == 1:
        holes['right'] = [gw]
    else:
        holes['left'] = [gw]
    spans = {'front': (-w / 2, w / 2), 'back': (-w / 2, w / 2), 'right': (-d / 2, d / 2), 'left': (-d / 2, d / 2)}
    orgs = {'front': (0, -d / 2, 0), 'back': (0, d / 2, 0), 'right': (w / 2, 0, 0), 'left': (-w / 2, 0, 0)}
    for side in ('front', 'back', 'right', 'left'):
        n = NORMALS[side]; org = orgs[side]
        u0, u1 = spans[side]
        # the wall's u runs along t = Z x n; for back/left the u axis flips, so holes are given in u already
        hs = [(a, b, z0, z1) for (a, b, z0, z1) in holes[side]]
        panel(F, 'rubble', org, n, u0, u1, 0.20, WH, hs, cell=0.20)
        if side == 'front':
            reveal(F, org, n, door[0], door[1], door[2], door[3], T)   # windows get the kit's oak reveals
    # gable triangles
    for sgn in (-1, 1):
        topf = lambda u: max(WH + 0.02, under(u))
        panel_top(F, 'rubble', (sgn * w / 2, 0, 0), NORMALS['right' if sgn > 0 else 'left'], -d / 2, d / 2, WH - 0.02, topf, cell=0.26, nv=5)
    mark(F, 'walls+gables')
    # dark interior
    dark_room(F, -w / 2 + T, w / 2 - T, -d / 2 + T, d / 2 - T, 0.20, WH, door=(door[0], door[1], door[3]))
    # ---- plinth (broken for the door), quoins
    pw, pd = w / 2 + 0.10, d / 2
    plinth_seg(F, (-pw, -d / 2, 0), (pw, -d / 2, 0), (0, -1, 0), 1.0, gap=(door[0] + pw - 0.12, door[1] + pw + 0.12))
    plinth_seg(F, (pw, d / 2, 0), (-pw, d / 2, 0), (0, 1, 0), 2.0)
    plinth_seg(F, (w / 2, -d / 2, 0), (w / 2, d / 2, 0), (1, 0, 0), 3.0)
    plinth_seg(F, (-w / 2, d / 2, 0), (-w / 2, -d / 2, 0), (-1, 0, 0), 4.0)
    ext(-w / 2 - 0.10, w / 2 + 0.10, -d / 2 - 0.10, d / 2 + 0.10)
    for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        quoins(F, sx * w / 2, sy * d / 2, sx, sy, WH, k + c['seed'] % 7)
    mark(F, 'liner+plinth+quoins')
    # ---- openings: surrounds, windows (kit window in the wall frame), pintles
    pin = FK.Piece('tmp')
    for side in ('front', 'back', 'right', 'left'):
        n = NORMALS[side]; org = orgs[side]
        for oi, (a, b, z0, z1) in enumerate(holes[side]):
            isdoor = (a, b, z0, z1) == door and side == 'front'
            surround(F, org, n, a, b, z0, z1, door=isdoor, seed=oi + 7 * len(side) + c['seed'] % 11, sill=False)
            if not isdoor:
                tmp = FK.Piece('tmp')
                FK.window(tmp, 0.0, a, b, z0, z1, oi + 10 * len(side))
                absorb(F, tmp, side_xf(side, w, d))
    for side in ('front', 'back'):
        tmp = FK.Piece('tmp')
        for oi, (a, b, z0, z1) in enumerate([h for h in holes[side] if h != door]):
            pintles(tmp, None, a, b, z0, z1, 0.0, oi)
        absorb(F, tmp, side_xf(side, w, d))
    mark(F, 'surrounds+windows')
    # door step: a worn stone slab; the door itself is Part_Door
    xd0, xd1 = door[0] - 0.16, door[1] + 0.16
    def dish(p):
        u = max(0.0, 1 - (p.x / 0.62) ** 2); v = max(0.0, 1 - (p.y / 0.32) ** 2)
        return Vector((0, 0, -0.03 * u * v)) if p.z > 0 else Vector()
    FK.add_box(F, 'stone', (xd1 - xd0 + 0.30, 0.66, DOORZ0 - 0.01), Matrix.Translation(((xd0 + xd1) / 2, -d / 2 - 0.20, (DOORZ0 - 0.01) / 2)),
               cham=0.04, segs=2, cuts=(6, 4, 0), bend=dish, wob=0.007, seed=3.3, uv_world=True)
    ext(xd0 - 0.15, xd1 + 0.15, -d / 2 - 0.53, -d / 2)
    # door (kit door in the wall frame)
    FK.DOOR = (door[0], door[1], door[2], door[3])
    kd = FK.kit_door()
    absorb(D, kd, side_xf('front', w, d))
    sock_door = Vector((c['door_x'], -d / 2 + 0.10, DOORZ0))
    # shutters (front + back windows), open and standing off the wall
    for side in ('front', 'back'):
        wl = [(a, b, z0, z1, 0.0) for (a, b, z0, z1) in holes[side] if (a, b, z0, z1) != door]
        absorb(SH, FK.kit_shutters(wl), side_xf(side, w, d))
    fw = holes['front'][0]
    sock_shut = Vector(((fw[0] + fw[1]) / 2, -d / 2 - FK.PROUD, (fw[2] + fw[3]) / 2))
    # flower box under the front window
    if c['flower']:
        absorb(F, FK.kit_flowerbox(fw[0], fw[1], fw[2] - 0.02, 0.0), side_xf('front', w, d))
    mark(F, 'step flowerbox')
    # ---- chimney
    if c['chimney'] == 'ridge':
        cx = round(R.uniform(-0.35, 0.35), 2) + (0.0)
        chimney_stack(F, cx, 0.0, ridge_top - 1.15, ridge_top + 0.55, 0.98, 0.84, 41.0)
        c['chim_pos'] = (cx, 0.0)
    else:
        chimney_gable(F, c, chim_side, ridge_top)
        c['chim_pos'] = (chim_side * (w / 2 + 0.3), c['chim_off'])
    c['chim_top'] = ridge_top + 0.60 + 0.5
    mark(F, 'chimney')
    # ---- porch (A)
    if c['porch']:
        porch(F, c, door, ridge_top)
    # ---- dormer (B)
    if c['dormer']:
        dormer(F, c, S)
    mark(F, 'porch/dormer')
    # ---- lean-to (C)
    if c['lean']:
        lean_to(F, c, lean_side, zt)
    mark(F, 'lean-to')
    _last[0] = 0
    return c, F, D, SH, sock_door, sock_shut

def porch(F, c, door, ridge_top):
    w, d = c['w'], c['d']
    dx = c['door_x']
    hw = 0.92; P = 0.85
    y0 = -d / 2; y1 = y0 - P
    zE = 2.30; rise = 0.66
    for sx in (-1, 1):
        FK.member(F, 'oak', (dx + sx * 0.80, y1 + 0.12, 0.0), (dx + sx * 0.80, y1 + 0.12, zE), 0.17, 0.17, name='pp%d' % sx, cham=0.02)
        FK.member(F, 'oak', (dx + sx * 0.80, y1 + 0.12, zE - 0.02), (dx + sx * 0.80, y0, zE - 0.02), 0.15, 0.20, name='pb%d' % sx, up=(1, 0, 0), cham=0.02)
        FK.member(F, 'oak', (dx + sx * 0.80, y1 + 0.12, zE - 0.5), (dx + sx * 0.52, y1 + 0.12, zE - 0.02), 0.09, 0.10, name='pk%d' % sx, cham=0.015)
    FK.member(F, 'oak', (dx - 0.80, y1 + 0.12, zE - 0.02), (dx + 0.80, y1 + 0.12, zE - 0.02), 0.20, 0.17, name='pbeam', cham=0.02)
    # tiled roof: two slabs, ridge to the wall
    zr = zE + 0.18 + rise
    for sx in (-1, 1):
        slab(F, 'roof', (dx, y0 + 0.02, zr), (dx, y1 - 0.12, zr - 0.04), (dx + sx * (hw + 0.10), y1 - 0.12, zE + 0.12),
             (dx + sx * (hw + 0.10), y0 + 0.02, zE + 0.16), 0.12, nu=1, nv=3)
    # front gable board with a pale rubble triangle under the roof
    panel_top(F, 'rubble', (dx, y1 + 0.02, 0), (0, -1, 0), -hw, hw, zE + 0.0, lambda u: zE + 0.04 + (hw - abs(u)) * (rise / hw) * 0.9, cell=0.3, nv=3, lump=0.012)
    FK.add_box(F, 'stone', (1.3, 0.50, 0.12), Matrix.Translation((dx, y1 + 0.05, 0.06)), cham=0.03, segs=2, uv_world=True, seed=9.0)
    ext(dx - 1.0, dx + 1.0, y1 - 0.30, y0)

def dormer(F, c, S):
    w, d, WH = c['w'], c['d'], c['WH']
    dx = c['door_x']
    yf = -d / 2 + 0.40
    zf = S.zt + (d / 2 - abs(yf)) * S.tanp + 0.02
    ze = zf + 0.88
    half = 0.52
    tn = math.tan(math.radians(46))
    zr = ze + (half + 0.14) * tn
    win = (-0.27, 0.27, zf + 0.20, zf + 0.78)
    panel(F, 'rubble', (dx, yf, 0), (0, -1, 0), -half, half, zf - 0.12, ze, [win], cell=0.2, lump=0.02)
    surround(F, (dx, yf, 0), (0, -1, 0), win[0], win[1], win[2], win[3], seed=77.0, sill=True)
    tmp = FK.Piece('tmp')
    FK.window(tmp, 0.0, win[0], win[1], win[2], win[3], 99)
    absorb(F, tmp, Matrix.Translation((dx, yf, 0)))
    panel_top(F, 'rubble', (dx, yf, 0), (0, -1, 0), -half, half, ze - 0.02, lambda u: ze + (half + 0.14 - abs(u)) * tn - 0.10, cell=0.2, nv=3, lump=0.012)
    yh = yf + (zr - zf) / S.tanp
    for sx in (-1, 1):
        poly(F, 'oak', [(dx + sx * half, yf, zf - 0.10), (dx + sx * half, yh + 0.05, ze), (dx + sx * half, yf, ze)],
             towards=(dx + sx * (half + 1), yf, ze))
        slab(F, 'thatch', (dx, yf - 0.24, zr), (dx, yh + 0.12, zr - 0.02), (dx + sx * (half + 0.14), yh + 0.12, ze - 0.02),
             (dx + sx * (half + 0.14), yf - 0.24, ze + 0.02), 0.20, nu=2, nv=4, amp=0.05, edge_mat='thatch')

def lean_to(F, c, sgn, zt):
    w, d = c['w'], c['d']
    xw = sgn * w / 2; xo = sgn * (w / 2 + LL)
    yf, yb = -d / 2 + 0.24, d / 2 - 0.24
    zl = 1.78 - 0.14; zh = 2.02
    # roof surface height over x (falls outward)
    def rz(x):
        t = (abs(x) - w / 2) / LL
        return zh + (zl - zh) * t
    topw = lambda x: rz(x) - 0.18          # wall top under the slab
    # front wall with a wood-store opening, back wall, end wall
    op = (xw + sgn * 0.20, xw + sgn * 0.93) if sgn > 0 else (xw + sgn * 0.93, xw + sgn * 0.20)
    hole = (op[0], op[1], 0.20, 1.40)
    panel(F, 'rubble', (0, yf, 0), (0, -1, 0), min(xw, xo), max(xw, xo), 0.20, 1.55, [hole], cell=0.22)
    reveal(F, (0, yf, 0), (0, -1, 0), hole[0], hole[1], hole[2], hole[3], 0.36)
    surround(F, (0, yf, 0), (0, -1, 0), hole[0], hole[1], hole[2], hole[3], seed=55.0, door=True)
    panel_top(F, 'rubble', (0, yf, 0), (0, -1, 0), min(xw, xo), max(xw, xo), 1.53, lambda u: max(1.56, topw(u)), cell=0.3, nv=2)
    panel(F, 'rubble', (0, yb, 0), (0, 1, 0), -max(xw, xo), -min(xw, xo), 0.20, 1.55, [], cell=0.26)
    panel_top(F, 'rubble', (0, yb, 0), (0, 1, 0), -max(xw, xo), -min(xw, xo), 1.53, lambda u: max(1.56, topw(-u)), cell=0.3, nv=2)
    panel(F, 'rubble', (xo, 0, 0), (sgn, 0, 0), yf, yb, 0.20, 1.55, [], cell=0.26)
    panel_top(F, 'rubble', (xo, 0, 0), (sgn, 0, 0), yf, yb, 1.53, lambda u: topw(xo), cell=0.4, nv=2)
    # dark store and logs seen through the opening
    ya, yz = yf + 0.36, yb - 0.12
    dark_room(F, min(xw, xo) + 0.12, max(xw, xo) - 0.12, ya, yz, 0.20, 1.55, skip_front=True)
    Rr = FK.rng('logs')
    for row in range(5):
        for i in range(6):
            r = Rr.uniform(0.05, 0.065)
            x = op[0] + 0.07 + i * 0.116 + (0.058 if row % 2 else 0.0)
            if x > op[1] - 0.05:
                continue
            bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
            bmesh.ops.create_cone(bm, cap_ends=True, segments=7, radius1=r, radius2=r * 0.97, depth=0.9)
            bm.transform(Matrix.Translation((x, ya + 0.46, 0.30 + row * 0.205)) @ Matrix.Rotation(math.pi / 2, 4, 'X') @
                         Matrix.Rotation(Rr.random() * 6.3, 4, 'Z'))
            for f in bm.faces:
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.x / TILE * 3, l.vert.co.y / TILE)
            bm.normal_update(); FK._merge(F, bm, 'oak_lt')
    # plinth round the lean-to
    plinth_seg(F, (xw, yf, 0), (xo + sgn * 0.10, yf, 0), (0, -1, 0), 11.0)
    plinth_seg(F, (xo + sgn * 0.10, yb, 0), (xw, yb, 0), (0, 1, 0), 12.0)
    plinth_seg(F, (xo, yf, 0), (xo, yb, 0), (sgn, 0, 0), 13.0)
    # quoins at the two outer corners
    for k, sy in enumerate((-1, 1)):
        quoins(F, xo, yf if sy < 0 else yb, sgn, sy, 1.62, 20 + k)
    # tiled mono-pitch roof (flat slab on the shared roof tileable), overhanging the walls
    ov = 0.22
    xa, xb = xw - sgn * 0.02, xo + sgn * ov
    slab(F, 'roof', (xa, yf - ov, rz(xa)), (xb, yf - ov, rz(xb) - 0.02), (xb, yb + ov, rz(xb) - 0.02), (xa, yb + ov, rz(xa)),
         0.14, nu=4, nv=8, amp=0.015)
    ext(min(xw, xo + sgn * 0.10), max(xw, xo + sgn * 0.10), yf - 0.1, yb + 0.1)

# ----------------------------------------------------------------------------- wear
def write_wear(ob, c, kind):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    WH = c['WH']; w, d = c['w'], c['d']
    cp = c.get('chim_pos', (0, 0)); ctop = c.get('chim_top', 5.0)
    names = [m.name if m else '' for m in me.materials]
    for poly in me.polygons:
        mn = names[poly.material_index] if poly.material_index < len(names) else ''
        n = poly.normal
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 2.1)
            streak = noise.noise(Vector((p.x * 5.0, p.y * 5.0, p.z * 0.7)))
            r = max(0.0, 0.22 * noise.noise(p * 0.45 + Vector((3.1, 0.0, 1.7))) + 0.06 * max(0.0, noise.noise(p * 2.3)))
            g = 0.0; b = 0.0
            if p.z < 1.45:   # splashback band at the foot
                r = max(r, max(0.0, 1 - p.z / (1.0 + 0.3 * nz + 0.25 * streak)) * 0.85)
            if mn in ('SC_Rubble', 'FK_Sandstone') and WH - 0.55 < p.z < WH + 0.1:
                r = max(r, (p.z - (WH - 0.55)) / 0.55 * 0.35)       # shade-damp band under the eave
            if mn == 'FK_Sandstone' and p.z < 0.5 and n.z > 0.5:
                g = 0.75; r = max(r, 0.5)                            # moss on the plinth top
            if mn == 'FK_Sandstone' and p.z > 0.5:
                r = max(r, 0.10 * max(0.0, noise.noise(p * 3.0)))    # pale stones stay pale
            if mn == 'SC_Rubble' and p.z < 1.1:
                g = max(g, max(0.0, noise.noise(p * 2.7 + Vector((1, 4, 2))) - 0.15) * 0.8 * (1 - p.z / 1.1))
            if mn == 'FK_RoofTile' and n.z > 0.3 and p.z > WH - 0.2:
                s = max(0.0, (p.z - WH) / 1.7)
                r = max(r, max(0.0, 1 - s * 2.0) * 0.55 + 0.3 * max(0.0, noise.noise(Vector((p.x * 1.4, p.y * 0.8, p.z * 2.2)))))
                tv = ((poly.index * 2654435761) % 1009) / 1009.0
                r = max(r, 0.30 * tv * tv)
                g = max(g, max(0.0, noise.noise(p * 1.6 + Vector((2, 0, 5))) * 1.5 - 0.25) * (0.9 if p.y > 0 else 0.45))
            if mn == 'SC_Thatch':
                low = max(0.0, 1 - (p.z - (WH - 0.2)) / 0.9)
                r = max(r, low * 0.35 + 0.10 * max(0.0, nz))
                g = max(g, max(0.0, noise.noise(p * 2.0 + Vector((5, 1, 0))) * 1.3 - 0.35) * (0.6 if p.y > 0 else 0.2))
                if abs(p.y) < 0.38 and p.z > c['WH'] + 1.0 and n.z > 0.2:
                    b = max(b, 0.45)                                  # fresher, paler straw on the ridge roll
            if mn == 'FK_ChimneyStone' or (mn in ('SC_Rubble', 'FK_Sandstone', 'FK_RoofTile') and p.z > ctop - 1.4 and
                                           abs(p.x - cp[0]) < 0.9 and abs(p.y - cp[1]) < 0.8):
                r = max(r, min(1.0, (p.z - (ctop - 1.9)) / 1.2) ** 0.9 * 0.85)
            if kind == 'door':
                b = max(0.0, 1 - Vector((p.x - (DOORZ0 * 0 + 0.0), 0, (p.z - 1.1) * 0.6)).length / 0.5) * 0.0
                ch = noise.noise(p * 9.0 + Vector((0.0, 7.0, 0.0)))
                r = max(r, min(0.6, max(0.0, ch - 0.22) * 3.0))
            if kind == 'shut':
                ch = noise.noise(p * 9.0 + Vector((0.0, 7.0, 0.0)))
                r = max(r, min(0.6, max(0.0, ch - 0.22) * 3.0))
            if mn == 'FK_Sandstone' and p.z < 0.3 and n.z > 0.5 and abs(p.y + d / 2) < 1.0 and abs(p.x - c['door_x']) < 0.6:
                b = max(b, 0.5)                                       # the dished, hand-worn step
            attr.data[li].color = (min(1, r * (c['wear'] if kind == 'frame' else 1.0)), min(1, g), min(1, b), 1.0)

def tri_count(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)

def assemble(name):
    c, F, D, SH, sock_door, sock_shut = build(name)
    root = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(root)
    # centre the footprint of the body on the pivot
    cx = (EXT[0] + EXT[1]) / 2; cy = (EXT[2] + EXT[3]) / 2
    fw, fd = EXT[1] - EXT[0], EXT[3] - EXT[2]
    shift = Matrix.Translation((-cx, -cy, 0))
    objs = {}
    for pc, kind in ((F, 'frame'), (D, 'door'), (SH, 'shut')):
        me = bpy.data.meshes.new(pc.name)
        if kind == 'frame':
            pass
        pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
        for m in pc.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
        objs[kind] = ob
    # smooth the lumpy rubble and the thatch, flat elsewhere
    for kind, ob in objs.items():
        me = ob.data
        names = [m.name for m in me.materials]
        for p in me.polygons:
            p.use_smooth = names[p.material_index] in ('SC_Rubble', 'SC_Thatch')
        if kind == 'frame':
            me.set_sharp_from_angle(angle=math.radians(35))
    for kind, ob in objs.items():
        write_wear(ob, c, kind)
    for ob in objs.values():
        ob.data.transform(shift)
    sd = sock_door - Vector((cx, cy, 0)); ss = sock_shut - Vector((cx, cy, 0))
    frame, door, shut = objs['frame'], objs['door'], objs['shut']
    frame.name = 'Frame'; frame.data.name = 'Frame'; frame.parent = root
    for ob, nm, at in ((door, 'Part_Door', sd), (shut, 'Part_Shutters', ss)):
        ob.name = nm; ob.data.name = nm
        ob.data.transform(Matrix.Translation(-at)); ob.location = at; ob.parent = root
    for nm, at in (('SOCKET_Door', sd), ('SOCKET_Shutters', ss)):
        e = bpy.data.objects.new(nm, None); e.empty_display_size = 0.3; e.location = at; e.parent = root
        bpy.context.scene.collection.objects.link(e)
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation((0, 0, 0.05)) @ Matrix.Diagonal((fw, fd, 0.1, 1)))
    me = bpy.data.meshes.new('FOOTPRINT'); bm.to_mesh(me); bm.free()
    me.materials.append(bpy.data.materials.new('FOOTPRINT'))
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root
    fp.hide_render = True
    a = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    for dd in a.data:
        dd.color = (0, 0, 0, 1)
    tris = {o.name: tri_count(o) for o in (frame, door, shut)}
    total = sum(tris.values())
    bb = [frame.matrix_world @ Vector(v) for v in frame.bound_box]
    ox = (min(v.x for v in bb), max(v.x for v in bb)); oy = (min(v.y for v in bb), max(v.y for v in bb)); oz = max(v.z for v in bb)
    print('SC COTTAGE %s w=%.2f d=%.2f WH=%.2f pitch=%.1f footprint=%.2fx%.2f roofbbox=%.2fx%.2f h=%.2f tris=%d (%s)' %
          (name, c['w'], c['d'], c['WH'], c['pitch'], fw, fd, ox[1] - ox[0], oy[1] - oy[0], oz, total, ', '.join('%s %d' % kv for kv in tris.items())))
    assert fw <= 5.0 and fd <= 5.0, 'footprint outside 5x5'
    if total >= 30000: print('WARNING over the 30k budget')
    mats = {m.name: MATINFO.get(m.name, 'flat') for o in (frame, door, shut) for m in o.data.materials}
    return root, frame, door, shut, fp, c, dict(footprint=(round(fw, 3), round(fd, 3)), tris=tris, total=total, mats=mats,
                                                 sock_door=tuple(round(x, 3) for x in sd), sock_shut=tuple(round(x, 3) for x in ss),
                                                 roofbbox=(round(ox[1] - ox[0], 2), round(oy[1] - oy[0], 2)), height=round(oz, 2))

# ----------------------------------------------------------------------------- review renders
VIEWS = {
    'fl': (Vector((-8.2, -10.5, 3.3)), Vector((0, 0, 1.9)), 40),
    'fr': (Vector((8.2, -10.5, 3.3)), Vector((0, 0, 1.9)), 40),
    'bl': (Vector((-8.2, 10.5, 3.3)), Vector((0, 0, 1.9)), 40),
    'br': (Vector((8.2, 10.5, 3.3)), Vector((0, 0, 1.9)), 40),
    'front': (Vector((0, -12.5, 2.2)), Vector((0, 0, 1.9)), 38),
}

def render(name, views, suffix=''):
    sc = bpy.context.scene
    FK.RRES = int(arg('--rres', '960')); FK.SAMPLES = int(arg('--samples', '32'))
    cam = FK.review_scene(bpy.data.objects[name], variant=True)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(2.0, -4.2, 0.875))
    for v in views:
        frm, to, fov = VIEWS[v]
        FK.look(cam, frm, to); cam.data.angle = math.radians(fov)
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
                             add_leaf_bones=False, colors_type='LINEAR', object_types={'EMPTY', 'MESH'}, path_mode='STRIP')

# ----------------------------------------------------------------------------- audit (re-import)
def audit(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'))
    print('AUDIT', name)
    allo = list(bpy.context.scene.objects)
    top = [o for o in allo if o.parent is None]
    print('  roots:', [o.name for o in top])
    for o in allo:
        if o.type == 'MESH':
            me = o.data
            ca = [a.name for a in me.color_attributes]
            slots = [m.name if m else None for m in me.materials]
            tris = sum(len(p.vertices) - 2 for p in me.polygons)
            bb = [Vector(v) for v in o.bound_box]
            print('  mesh %-14s parent=%-16s tris=%5d wear=%s slots=%s' % (o.name, o.parent.name if o.parent else None, tris, 'wear' in ca, slots))
        else:
            print('  empty %-14s parent=%s loc=%s' % (o.name, o.parent.name if o.parent else None, tuple(round(x, 2) for x in o.matrix_world.translation)))
    # facing check: every closed island (stones, posts, pots, logs...) must have positive signed volume (outward normals);
    # open shells (wall panels, roof decks, lips) are oriented by construction and are checked by --ray below.
    tot_closed = tot_open = tot_neg = 0
    for o in allo:
        if o.type != 'MESH' or o.name == 'FOOTPRINT':
            continue
        bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
        seen = set(); neg = []; nclosed = nopen = 0
        for f0 in bm.faces:
            if f0.index in seen:
                continue
            stack = [f0]; isl = []
            seen.add(f0.index)
            while stack:
                f = stack.pop(); isl.append(f)
                for e in f.edges:
                    for g in e.link_faces:
                        if g.index not in seen:
                            seen.add(g.index); stack.append(g)
            closed = all(len(e.link_faces) == 2 for f in isl for e in f.edges)
            if closed:
                nclosed += 1
                vol = 0.0
                for f in isl:
                    vs = [v.co for v in f.verts]
                    for i in range(1, len(vs) - 1):
                        vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
                if vol < 0:
                    neg.append((round(vol, 4), tuple(round(x, 2) for x in isl[0].calc_center_median())))
            else:
                nopen += 1
        bm.free()
        tot_closed += nclosed; tot_open += nopen; tot_neg += len(neg)
        print('  normals %-14s closed islands %4d (inside-out %d)  open shells %4d  %s' % (o.name, nclosed, len(neg), nopen, neg[:4]))
    print('  NORMALS closed inside-out total:', tot_neg)
    fp = [o for o in allo if o.name == 'FOOTPRINT']
    if fp:
        bb = [fp[0].matrix_world @ Vector(v) for v in fp[0].bound_box]
        print('  FOOTPRINT size (Unity x,z):', round(max(v.x for v in bb) - min(v.x for v in bb), 2), 'x', round(max(v.y for v in bb) - min(v.y for v in bb), 2))
    if '--ray' not in argv:
        return
    dg = bpy.context.evaluated_depsgraph_get()
    meshes = [o for o in allo if o.type == 'MESH' and o.name != 'FOOTPRINT']
    verts = []; tri = []; owner = []
    for o in meshes:
        me = o.data; me.calc_loop_triangles()
        base = len(verts)
        verts += [o.matrix_world @ v.co for v in me.vertices]
        for lt in me.loop_triangles:
            tri.append(tuple(base + i for i in lt.vertices)); owner.append((o.name, me.materials[me.polygons[lt.polygon_index].material_index].name))
    bvh = BVHTree.FromPolygons(verts, tri)
    from collections import Counter
    rr = random.Random(5)
    bad = Counter(); n_hit = 0
    for _ in range(6000):
        # a ray from a sphere around the building aimed at a random point of the body: the first surface it meets must face it
        th = rr.uniform(0, 2 * math.pi); el = rr.uniform(-0.05, 1.1)
        o = Vector((14 * math.cos(el) * math.cos(th), 14 * math.cos(el) * math.sin(th), 14 * math.sin(el) + 1.0))
        tgt = Vector((rr.uniform(-2.5, 2.5), rr.uniform(-2.5, 2.5), rr.uniform(0.2, 5.0)))
        dirv = (tgt - o).normalized()
        hit = bvh.ray_cast(o, dirv)
        if hit[0] is None:
            continue
        n_hit += 1
        tn = tri[hit[2]]
        hn = (verts[tn[1]] - verts[tn[0]]).cross(verts[tn[2]] - verts[tn[0]])
        if hn.length > 1e-9 and hn.normalized().dot(dirv) > 0.0:
            bad[owner[hit[2]]] += 1
    print('  VISIBLE-FACE ray test: %d rays hit, %d first hits are back faces: %s' % (n_hit, sum(bad.values()), dict(bad)))

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
    if '--audit' in argv:
        for nm in names:
            audit(nm)
