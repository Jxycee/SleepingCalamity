"""Market and lane clutter for Hollowmere (queue Q14): handcart, crate stack, sack stack, rain barrel, flower box, well, six trade signs and the washing-line parts,
each with a clearly broken / ruined twin.

Tier T2 decoration family, method Gen (as the Q6 garden, sc_garden.py, whose geometry kit is copied here: chamfered boxes, lathes, tubes, leaf lenses, `tint` / `wear`
corner attributes, no alpha cards, NO BAKES, no textures exported). New here: `loft` (a rectangular section swept along a polyline: staves, shafts, rope), `beam`
and `box(long=True)` chamfer only the long edges (cheap boards), thin double-sided cloth sheets for the washing line.
Style bible PHASE1 3: chunky storybook forms ~10% oversize, timber ~1.4x the real section, 1.5 cm chamfers, nothing dead straight (every board turned a degree or two).

Contract: Reference/hollowmere/clutter/CONTRACT.md (asset names, footprints, pivots, material SLOT NAMES CL_*; Unity maps them to the shared B1 tileables, so the
review renders here use the real B1 tileables for CL_Oak/OakGrey/Paint/PaintRed/Iron/Stone/Tile/Canvas/Rope/Linen/ClothBlue/ClothRed/Rag, box-projected UVs in metres,
1 UV unit = 2 m, multiplied by the contract tint, and flat colours for the rest).
Conventions: metres, Blender z up, front -Y (FBX -Z forward / Y up, so the front is Unity +Z), pivot = ground centre of the footprint; root empty <Asset>, mesh `Body`, flat 0.1 m
`FOOTPRINT` box. Signs: pivot = centre of the wall plate at the wall plane (y = 0), the arm runs out along -Y (Unity +Z), board face in the Y-Z plane, no FOOTPRINT.
WashingLine: one FBX, separate objects Rope_1m (x 0..1), Hook, Cloth_* (pivot = top centre on the line, hang in the X-Z plane), Peg, torn Cloth_Rag_A/B; no FOOTPRINT.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_clutter.py -- --out <scratch> [--only Handcart,Well] [--export]
      [--no-render] [--rres 960] [--samples 20]
  --export copies the FBXs and SOURCES.md to Assets/_Project/Content/Art/Town/Clutter/. Renders: <out>/renders/<group>.png (rebuilt left, ruined right).

Assets: Handcart, Handcart_Broken, CrateStack, CrateStack_Broken, SackStack, SackStack_Broken, RainBarrel, RainBarrel_Broken, FlowerBox, FlowerBox_Broken, Well,
Well_Ruined, ShopSign_Bakery / _Inn / _Smithy / _Workshop / _Tailor / _Grocer / _Broken, WashingLine. Deterministic: the seed is fixed per asset name.
Status (2026-10-31): all 21 assets build and export, every budget met (a prop <= 3000 tris, Well <= 6000, a sign <= 1500, each washing-line part <= 600, <= 6 slots).
Review renders colour the painted and cloth slots by the tileable's luminance times the contract tint (the blue-grey painted_wood / ochre canvas hue would otherwise swamp
the tint); Unity multiplies the tint directly, so check those slots there. Self-score (sc-asset-review rubric, mid camera): round 1 27/40, round 2 28.5, round 3 29
(gains under 1.5, stopped by the beauty-corner rule). Sheet: Reference/hollowmere/clutter/clutter_sheet_r3.jpg.
"""
import sys, os, math, random, zlib, shutil, contextlib
import bpy, bmesh
from mathutils import Vector, Matrix, Quaternion, Euler, noise

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
ONLY = arg('--only').split(',') if arg('--only') else None
EXPORT = '--export' in argv; RENDER = '--no-render' not in argv
RRES = int(arg('--rres', '960')); SAMPLES = int(arg('--samples', '20'))
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
TEX = os.path.join(ROOT, 'Art', 'Textures', 'B1_town')
DEST = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Art', 'Town', 'Clutter')
SEED = 1431
BEV = 0.016

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode()))

# ---------------------------------------------------------------- geometry kit
def T(c=(0, 0, 0), rot=None, sc=(1, 1, 1)):
    if rot is None: R = Matrix.Identity(4)
    elif isinstance(rot, Quaternion): R = rot.to_matrix().to_4x4()
    elif isinstance(rot, Matrix): R = rot.to_4x4()
    else: R = Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4()
    return Matrix.Translation(Vector(c)) @ R @ Matrix.Diagonal(Vector(list(sc) + [1.0]))

def aim(d):
    d = Vector(d).normalized()
    return d.to_track_quat('Z', 'X' if abs(d.y) > 0.99 else 'Y')

def basis(d, up=(0, 0, 1)):
    """Rotation with local +X along d and local +Z as close to `up` as possible."""
    x = Vector(d).normalized(); u = Vector(up).normalized()
    if abs(x.dot(u)) > 0.95: u = Vector((0, 1, 0)) if abs(x.y) < 0.9 else Vector((1, 0, 0))
    z = (u - x * u.dot(x)).normalized(); y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_4x4()

def pivot(p, rot):
    return Matrix.Translation(Vector(p)) @ Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4() @ Matrix.Translation(-Vector(p))

def jit(r, a):
    return r.uniform(-a, a)

class Piece:
    """One mesh object of an asset: a bmesh plus its material slot names."""
    def __init__(self, name, origin=(0, 0, 0)):
        self.name = name; self.bm = bmesh.new(); self.slots = []; self.G = Matrix.Identity(4); self.origin = Vector(origin); self.ftint = []

    @contextlib.contextmanager
    def xf(self, M):
        old = self.G; self.G = old @ M
        try: yield
        finally: self.G = old

    def absorb(self, t, slot, smooth=None, tint=None, fix=True):
        if slot not in self.slots: self.slots.append(slot)
        mi = self.slots.index(slot)
        if fix: bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
        t.transform(self.G)
        if self.G.determinant() < 0: bmesh.ops.reverse_faces(t, faces=t.faces[:])
        vm = {v: self.bm.verts.new(v.co) for v in t.verts}
        for f in t.faces:
            nf = self.bm.faces.new([vm[v] for v in f.verts]); nf.material_index = mi
            nf.smooth = f.smooth if smooth is None else smooth
            self.ftint.append(tint or (1.0, 1.0, 1.0))
        t.free()

    def box(self, slot, c, s, rot=None, bev=BEV, taper=None, long=False, **kw):
        """Chamfered box centred on c. long=True chamfers only the four edges parallel to the longest side (cheap boards)."""
        t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts:
            k = taper if (taper is not None and v.co.z > 0) else 1.0
            v.co = Vector((v.co.x * s[0] * k, v.co.y * s[1] * k, v.co.z * s[2]))
        b = min(bev, 0.34 * min(s))
        if b > 0.0025:
            es = list(t.edges)
            if long:
                ax = max(range(3), key=lambda i: s[i])
                es = [e for e in es if abs((e.verts[0].co - e.verts[1].co)[ax]) > 1e-6 and all(abs((e.verts[0].co - e.verts[1].co)[i]) < 1e-6 for i in range(3) if i != ax)]
            bmesh.ops.bevel(t, geom=es, offset=b, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        t.transform(T(c, rot)); self.absorb(t, slot, **kw)

    def beam(self, slot, a, b, w, h, bev=BEV * 0.6, up=(0, 0, 1), **kw):
        """Box from a to b, section w (local y) x h (local z, toward `up`); chamfers the long edges."""
        a = Vector(a); b = Vector(b); d = b - a
        t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts: v.co = Vector((v.co.x * d.length, v.co.y * w, v.co.z * h))
        bb = min(bev, 0.34 * min(w, h))
        if bb > 0.0025:
            es = [e for e in t.edges if abs((e.verts[0].co - e.verts[1].co).x) > 1e-6]
            bmesh.ops.bevel(t, geom=es, offset=bb, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        t.transform(Matrix.Translation((a + b) / 2) @ basis(d, up)); self.absorb(t, slot, **kw)

    def cyl(self, slot, c, r, h, rot=None, segs=8, r2=None, sc=(1, 1, 1), smooth=False, **kw):
        t = bmesh.new()
        bmesh.ops.create_cone(t, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=max(0.002, r if r2 is None else r2), depth=h)
        if smooth:
            for f in t.faces: f.smooth = abs(f.normal.z) < 0.9
        t.transform(T(c, rot, sc)); self.absorb(t, slot, **kw)

    def blade(self, slot, base, d, length, r0, r1=0.003, segs=4, **kw):
        d = Vector(d).normalized()
        self.cyl(slot, Vector(base) + d * length / 2, r0, length, rot=aim(d), segs=segs, r2=r1, **kw)

    def blob(self, slot, c, r, rot=None, u=8, v=5, **kw):
        t = bmesh.new(); bmesh.ops.create_uvsphere(t, u_segments=u, v_segments=v, radius=1.0)
        r = (r, r, r) if isinstance(r, (int, float)) else r
        t.transform(T(c, rot, r)); self.absorb(t, slot, **kw)

    def lens(self, slot, base, d, length, width, thick=0.008, up=(0, 0, 1), **kw):
        """A small lens leaf (closed, no alpha card) growing from base toward d; width across `up`... thin axis is the local z (toward up)."""
        d = Vector(d).normalized(); t = bmesh.new()
        bmesh.ops.create_uvsphere(t, u_segments=5, v_segments=3, radius=1.0)
        t.transform(Matrix.Translation(Vector(base) + d * length / 2) @ basis(d, up) @ Matrix.Diagonal(Vector((length / 2, width / 2, thick / 2, 1))))
        self.absorb(t, slot, **kw)

    def prism(self, slot, pts, thick, c=(0, 0, 0), plane='xz', rot=None, bev=0.0, **kw):
        """Extrude a 2D profile: plane 'xz' (thickness along y) or 'yz' (thickness along x), centred on thickness."""
        t = bmesh.new()
        mk = (lambda a, b: (a, -thick / 2, b)) if plane == 'xz' else (lambda a, b: (-thick / 2, a, b))
        f = t.faces.new([t.verts.new(mk(a, b)) for a, b in pts])
        r = bmesh.ops.extrude_face_region(t, geom=[f])
        off = Vector((0, thick, 0)) if plane == 'xz' else Vector((thick, 0, 0))
        bmesh.ops.translate(t, vec=off, verts=[e for e in r['geom'] if isinstance(e, bmesh.types.BMVert)])
        if bev > 0.002: bmesh.ops.bevel(t, geom=list(t.edges), offset=bev, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        t.transform(T(c, rot)); self.absorb(t, slot, **kw)

    def lathe(self, slot, prof, c=(0, 0, 0), segs=12, closed=False, rot=None, vbev=0.0, sc=(1, 1, 1), smooth=False, **kw):
        """Revolve [(radius, z), ...] about z. closed: the profile is a loop (a hoop); otherwise both ends are capped."""
        t = bmesh.new(); rings = []
        for r, z in prof:
            rings.append([t.verts.new((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), z)) for k in range(segs)])
        n = len(rings); ved = []
        for i in range(n if closed else n - 1):
            a, b = rings[i], rings[(i + 1) % n]
            for k in range(segs):
                t.faces.new([a[k], a[(k + 1) % segs], b[(k + 1) % segs], b[k]]).smooth = smooth
                ved.append((a[k], b[k]))
        if not closed:
            t.faces.new(rings[0][::-1]); t.faces.new(rings[-1])
        if vbev > 0.001:
            es = [e for e in (t.edges.get(p) for p in ved) if e is not None]
            bmesh.ops.bevel(t, geom=es, offset=vbev, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        t.transform(T(c, rot, sc)); self.absorb(t, slot, **kw)

    def thin(self, slot, pts, faces, ref, half=0.003, tint=None, smooth=True):
        """A double-sided thin sheet (open shell): points, faces as index tuples, offset +-half along ref(p); front faces point along ref."""
        t = bmesh.new(); pts = [Vector(p) for p in pts]; nr = []
        for p in pts:
            n = Vector(ref(p)); nr.append(n.normalized() if n.length > 1e-6 else Vector((0, 0, 1)))
        fv = [t.verts.new(p + n * half) for p, n in zip(pts, nr)]; bv = [t.verts.new(p - n * half) for p, n in zip(pts, nr)]
        for fc in faces:
            a = [pts[i] for i in fc]; nn = Vector()
            for i in range(len(a)): nn += a[i].cross(a[(i + 1) % len(a)])
            c = sum(a, Vector()) / len(a)
            idx = list(fc) if nn.dot(Vector(ref(c))) >= 0 else list(fc)[::-1]
            t.faces.new([fv[i] for i in idx]); t.faces.new([bv[i] for i in idx[::-1]])
        self.absorb(t, slot, smooth=smooth, tint=tint, fix=False)

    def tube(self, slot, pts, r0, r1=None, segs=5, **kw):
        pts = [Vector(p) for p in pts]; r1 = r0 if r1 is None else r1; t = bmesh.new(); rings = []
        for i, p in enumerate(pts):
            tan = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            up = Vector((0, 0, 1)) if abs(tan.z) < 0.9 else Vector((1, 0, 0))
            side = tan.cross(up).normalized(); up2 = side.cross(tan).normalized()
            r = r0 + (r1 - r0) * i / max(1, len(pts) - 1)
            rings.append([t.verts.new(p + (side * math.cos(2 * math.pi * k / segs) + up2 * math.sin(2 * math.pi * k / segs)) * r) for k in range(segs)])
        for i in range(len(rings) - 1):
            for k in range(segs):
                t.faces.new([rings[i][k], rings[i][(k + 1) % segs], rings[i + 1][(k + 1) % segs], rings[i + 1][k]])
        t.faces.new(rings[0][::-1]); t.faces.new(rings[-1])
        self.absorb(t, slot, **kw)

    def loft(self, slot, pts, w, h, w1=None, h1=None, side=None, **kw):
        """Rectangular section (w across `side`, h the other way) swept along a polyline, capped: staves, shafts, planks that bend."""
        pts = [Vector(p) for p in pts]; n = len(pts); w1 = w if w1 is None else w1; h1 = h if h1 is None else h1
        t = bmesh.new(); rings = []
        for i, p in enumerate(pts):
            tan = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
            sd = Vector(side) if side is not None else tan.cross(Vector((0, 0, 1)) if abs(tan.z) < 0.9 else Vector((1, 0, 0)))
            sd = sd - tan * sd.dot(tan)
            if sd.length < 1e-4: sd = tan.cross(Vector((1, 0, 0)))
            sd.normalize(); nm = tan.cross(sd).normalized()
            f = i / max(1, n - 1); ww = w + (w1 - w) * f; hh = h + (h1 - h) * f
            rings.append([t.verts.new(p + sd * ww / 2 * sx + nm * hh / 2 * sy) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
        for i in range(n - 1):
            for k in range(4):
                t.faces.new([rings[i][k], rings[i][(k + 1) % 4], rings[i + 1][(k + 1) % 4], rings[i + 1][k]])
        t.faces.new(rings[0][::-1]); t.faces.new(rings[-1])
        self.absorb(t, slot, **kw)

class Asset:
    def __init__(self, name, foot=None, body=True):
        self.name = name; self.r = rng(name); self.foot = foot; self.sockets = {}
        self.lean = (0.0, 0.0); self.amp = 0.005; self.off = Vector((self.r.uniform(0, 50), self.r.uniform(0, 50), self.r.uniform(0, 50)))
        self.objs = []; self.body = self.piece('Body') if body else None
        self.notes = ''
    def piece(self, name, origin=(0, 0, 0)):
        p = Piece(name, origin); self.objs.append(p); return p

# ---------------------------------------------------------------- shared props
def crate(b, r, c, s, rot=(0, 0, 0), slot='CL_Oak', lid=True, strap=False):
    """A slatted crate standing on c (bottom centre): core, four corner posts, two slats per side with a gap, optional lid planks."""
    sx, sy, sz = s
    with b.xf(T(c, rot)):
        b.box(slot, (0, 0, sz / 2), (sx - 0.05, sy - 0.05, sz - 0.03), bev=0)
        for ix in (-1, 1):
            for iy in (-1, 1):
                b.box(slot, (ix * (sx / 2 - 0.03), iy * (sy / 2 - 0.03), sz / 2), (0.06, 0.06, sz), bev=0)
        for k, z in enumerate((sz * 0.24, sz * 0.74)):
            hh = sz * 0.4
            for iy in (-1, 1):
                b.box(slot, (jit(r, 0.004), iy * (sy / 2 - 0.0125), z), (sx - 0.1, 0.025, hh), rot=(jit(r, 1.2), 0, jit(r, 1.0)), long=True)
            for ix in (-1, 1):
                b.box(slot, (ix * (sx / 2 - 0.0125), jit(r, 0.004), z), (0.025, sy - 0.1, hh), rot=(0, jit(r, 1.2), jit(r, 1.0)), long=True)
        if lid:
            for k in range(3):
                b.box(slot, (jit(r, 0.004), -sy / 3 + k * sy / 3, sz + 0.004), (sx + 0.01, sy / 3 - 0.012, 0.024), rot=(0, 0, jit(r, 1.5)), long=True)
        if strap:
            b.box('CL_Iron', (0, 0, sz + 0.02), (0.05, sy + 0.02, 0.012), bev=0.004)

def sack(b, r, c, rad, h, rot=None, sc=(1, 1, 1), tint=(1, 1, 1), slot='CL_Canvas', segs=14, tie=True):
    """A tied sack: smooth lathe (belly, shoulder, pinched neck, flared ear) plus a rope band round the neck."""
    prof = [(0.7, 0.0), (0.93, 0.05), (1.06, 0.3), (1.0, 0.58), (0.7, 0.79), (0.26, 0.88), (0.4, 0.95), (0.13, 1.0)]
    b.lathe(slot, [(p * rad, z * h) for p, z in prof], c=c, segs=segs, rot=rot, sc=sc, smooth=True, tint=tint)
    with b.xf(T(c, rot, sc)):   # a stitched patch on the belly (placed wear)
        b.box('CL_Linen', (rad * 1.0, 0, h * 0.4), (0.03, rad * 0.55, h * 0.2), rot=(0, 0, 0), bev=0, tint=(tint[0] * 0.8, tint[1] * 0.75, tint[2] * 0.7))
    if tie:
        b.lathe('CL_Rope', [(0.22 * rad, 0.865 * h), (0.31 * rad, 0.865 * h), (0.31 * rad, 0.915 * h), (0.22 * rad, 0.915 * h)], c=c, segs=segs, closed=True, rot=rot, sc=sc, smooth=True)

def wheel(b, r, c, R=0.38, rot=(0, 90, 0), slot='CL_Oak', w=0.07, spokes=5):
    with b.xf(T(c, rot)):
        b.lathe(slot, [(R - 0.055, -w / 2), (R, -w / 2), (R, w / 2), (R - 0.055, w / 2)], closed=True, segs=14)
        b.lathe('CL_Iron', [(R, -w / 2 - 0.008), (R + 0.016, -w / 2 - 0.008), (R + 0.016, w / 2 + 0.008), (R, w / 2 + 0.008)], closed=True, segs=14)
        b.cyl(slot, (0, 0, 0), 0.07, w + 0.07, segs=8)
        ph = r.uniform(0, 6.28)
        for k in range(spokes):
            an = ph + k * 2 * math.pi / spokes
            b.beam(slot, (math.cos(an) * 0.05, math.sin(an) * 0.05, 0), (math.cos(an) * (R - 0.04), math.sin(an) * (R - 0.04), 0), 0.05, 0.05, bev=0.01, up=(0, 0, 1))

def barrel(b, r, c, R, H, n=14, slot='CL_Oak', splay=None, drop=(), tops=None, hoops=(0.08, 0.3, 0.7, 0.92)):
    """Staves lofted from the barrel profile. splay[i]: outward lean of stave i at the top (m); drop: staves left out; tops[i]: stave height factor."""
    def rad(t): return R * (0.86 + 0.14 * (1 - (2 * t - 1) ** 2))
    cx, cy, cz = c
    for i in range(n):
        if i in drop: continue
        th = 2 * math.pi * i / n + r.uniform(-0.02, 0.02); sp = 0 if splay is None else splay[i]; hf = 1.0 if tops is None else tops[i]
        pts = []
        for t in (0.0, 0.33, 0.66, 1.0):
            tt = t * hf; rr = rad(tt) + 0.02 + sp * t ** 2.2
            pts.append((cx + math.cos(th) * rr, cy + math.sin(th) * rr, cz + tt * H - sp * 0.25 * t * t))
        sd = (-math.sin(th), math.cos(th), 0)
        k = r.uniform(0.9, 1.04)
        b.loft(slot, pts, 2 * R * math.sin(math.pi / n) * 1.04, 0.036, side=sd, tint=(k, k, k))
    for t in hoops:
        z = cz + t * H; ro = rad(t) + 0.062; ri = rad(t) + 0.02
        b.lathe('CL_Iron', [(ri, z - 0.026), (ro - 0.005, z - 0.026), (ro, z - 0.02), (ro, z + 0.02), (ro - 0.005, z + 0.026), (ri, z + 0.026)], c=(cx, cy, 0), segs=18, closed=True)

# ---------------------------------------------------------------- Handcart
def handcart_geo(b, r, broken):
    P = 'CL_PaintRed'; E = 'CL_OakGrey' if broken else 'CL_Oak'
    y0, L = (-0.52, 0.74) if broken else (-0.2, 1.0)
    yc = y0 + L / 2; R = 0.38
    for sx in ((-1,) if broken else (-1, 1)):
        wheel(b, r, (sx * 0.445, yc, R), R, slot=E)
    b.cyl('CL_Iron', (0, yc, R), 0.027, 1.0 if not broken else 0.62, rot=(0, 90, 0), segs=6, sc=(1, 1, 1)) if False else None
    ax0, ax1 = (-0.5, 0.36) if broken else (-0.5, 0.5)
    b.beam('CL_Iron', (ax0 + 0.03, yc, R), (ax1 - 0.03, yc, R), 0.05, 0.05, bev=0.008)
    for sx in (-1, 1):
        b.beam(E, (sx * 0.31, y0, 0.50), (sx * 0.31, y0 + L, 0.50), 0.07, 0.075)   # bed rails
        b.box(E, (sx * 0.31, yc, 0.435), (0.1, 0.14, 0.1), bev=0.012)   # axle blocks
    nf = int(L / 0.2)
    for k in range(nf):
        b.box(E, (jit(r, 0.004), y0 + (k + 0.5) * L / nf, 0.55), (0.8, L / nf - 0.012, 0.035), rot=(0, jit(r, 1.0), jit(r, 1.5)), long=True)
    for sx in (-1, 1):   # side boards
        if broken and sx == 1:
            b.box(P, (0.395, y0 + 0.17, 0.66), (0.032, 0.32, 0.11), rot=(0, 0, 4), long=True)   # snapped board: two ends
            b.box(P, (0.40, y0 + 0.6, 0.62), (0.032, 0.2, 0.11), rot=(10, 0, -3), long=True)
            b.box(P, (0.395, y0 + 0.3, 0.77), (0.032, 0.7, 0.11), rot=(0, 0, 0), long=True)
            continue
        for z in (0.63, 0.77):
            b.box(P, (sx * 0.395 + jit(r, 0.004), yc, z + jit(r, 0.004)), (0.032, L, 0.125), rot=(jit(r, 1.0), 0, jit(r, 0.8)), long=True)
    for y in ((y0 + 0.015,) if broken else (y0 + 0.015, y0 + L - 0.015)):
        for z in (0.63, 0.77):
            b.box(P, (jit(r, 0.004), y, z), (0.76, 0.032, 0.125), rot=(0, jit(r, 1.0), jit(r, 0.8)), long=True)
    for sx in (-1, 1):
        for y in ((y0 + 0.03,) if broken else (y0 + 0.03, y0 + L - 0.03)):
            b.box(E, (sx * 0.395, y, 0.7), (0.055, 0.055, 0.34), bev=0.01)
    # shafts toward -Y (Unity +Z), resting on the ground with prop legs
    fy = -0.95
    for sx in (-1, 1):
        if broken and sx == 1:   # snapped stub, end dug into the ground
            b.beam(E, (0.31, y0, 0.5), (0.31, y0 - 0.32, 0.38), 0.07, 0.08)
            continue
        pts = [(sx * 0.31, y0 + 0.05, 0.5), (sx * 0.32, (y0 + fy) / 2, (0.5 + 0.25) / 2), (sx * 0.33, fy, 0.25)]
        b.loft(E, pts, 0.07, 0.08, side=(1, 0, 0))
        yl = fy * 0.78 + y0 * 0.22
        b.box(E, (sx * 0.33, yl, 0.13), (0.05, 0.05, 0.27), rot=(jit(r, 4), 0, jit(r, 3)), bev=0.01)
    if not broken or True:
        b.beam(E, (-0.33, fy + 0.01, 0.3), (0.33, fy + 0.01, 0.3), 0.055, 0.06) if not broken else None
    # tie rope, tailgate hinge pins
    for sx in (-1, 1):
        b.box('CL_Iron', (sx * 0.43, y0 + 0.5 * L, 0.5), (0.03, 0.2, 0.05), bev=0.006)

def handcart(a, broken):
    r = a.r; b = a.body
    if not broken:
        handcart_geo(b, r, False)
        sack(b, r, (0.0, 0.55, 0.57), 0.2, 0.44, tint=(1.0, 0.95, 0.9))
        crate(b, r, (0.0, 0.08, 0.57), (0.36, 0.32, 0.28), rot=(0, 0, 12), slot='CL_Oak', lid=False)
        a.foot = (1.0, 1.9)
    else:
        with b.xf(pivot((-0.445, 0, 0), (0, 21, 0))):
            handcart_geo(b, r, True)
        # the lost wheel lies flat behind, a plank and a toppled sack beside the low rail
        b.G = Matrix.Identity(4)
        wheel(b, r, (0.1, 0.57, 0.045), 0.38, rot=(jit(r, 3), jit(r, 3), 20), slot='CL_OakGrey')
        b.box('CL_PaintRed', (-0.38, 0.62, 0.02), (0.1, 0.45, 0.035), rot=(0, 0, 14), long=True)
        sack(b, r, (0.2, 0.0, 0.17), 0.19, 0.4, rot=(0, 78, 24), tint=(0.85, 0.8, 0.74))
        a.foot = (1.2, 1.9)
    a.amp = 0.004; a.lean = (0.0, 0.0)

# ---------------------------------------------------------------- CrateStack
def crate_stack(a):
    r = a.r; b = a.body
    crate(b, r, (-0.3, 0.0, 0.0), (0.64, 0.52, 0.52), rot=(0, 0, 3))
    crate(b, r, (0.33, 0.05, 0.0), (0.6, 0.48, 0.46), rot=(0, 0, -5), slot='CL_Paint', strap=True)
    crate(b, r, (-0.28, 0.0, 0.55), (0.52, 0.44, 0.46), rot=(0, 0, 21), strap=True)
    crate(b, r, (0.3, 0.04, 0.49), (0.48, 0.4, 0.4), rot=(0, 0, -9))
    crate(b, r, (-0.27, 0.0, 1.04), (0.4, 0.34, 0.34), rot=(0, 1.5, 36), slot='CL_Paint')
    crate(b, r, (0.33, 0.05, 0.92), (0.34, 0.3, 0.26), rot=(0, 0, 14), strap=True)
    a.foot = (1.3, 0.9); a.lean = (0.006, 0.0)

def crate_stack_broken(a):
    r = a.r; b = a.body; G = 'CL_OakGrey'
    crate(b, r, (-0.36, -0.05, 0.25), (0.62, 0.5, 0.46), rot=(0, 90, 0), slot=G, lid=True)   # toppled on its side
    crate(b, r, (0.38, 0.1, 0.0), (0.58, 0.46, 0.4), rot=(0, 0, -14), slot=G)
    # smashed crate: floor, two bent posts and half-hanging slats
    with b.xf(T((0.15, -0.3, 0.0), (0, 0, 24))):
        b.box(G, (0, 0, 0.02), (0.5, 0.42, 0.03), long=True)
        for ix, ang in ((-1, 14), (1, -22)):
            b.box(G, (ix * 0.23, 0.14, 0.17), (0.055, 0.055, 0.34), rot=(ang, 8 * ix, 0), bev=0.01)
        b.box(G, (-0.12, 0.2, 0.26), (0.3, 0.025, 0.1), rot=(-24, 0, 10), long=True)
        b.box(G, (0.2, 0.2, 0.12), (0.22, 0.025, 0.1), rot=(-70, 0, -20), long=True)
    for i in range(7):   # loose planks
        x = r.uniform(-0.6, 0.6); y = r.uniform(-0.5, 0.4); an = r.uniform(0, 180)
        z = 0.02 if i < 5 else 0.5
        if i == 5: x, y, z = 0.38, 0.1, 0.46
        b.box(G, (x, y, z), (r.uniform(0.3, 0.55), 0.09, 0.022), rot=(jit(r, 3) + (14 if i == 5 else 0), 0, an), long=True)
    a.foot = (1.4, 1.1); a.lean = (0.0, 0.0)

# ---------------------------------------------------------------- SackStack
def pallet(b, r, G, broken):
    E = G
    for sx in (-1, 0, 1):
        if broken and sx == 1:
            b.box(E, (0.5, -0.05, 0.04), (0.09, 0.4, 0.08), rot=(0, 0, 5), long=True)
            b.box(E, (0.52, 0.34, 0.03), (0.09, 0.3, 0.07), rot=(0, 10, 20), long=True)
        else:
            b.box(E, (sx * 0.5, 0, 0.04), (0.09, 0.8, 0.08), rot=(0, 0, jit(r, 1.0)), long=True)
    for k in range(5):
        y = -0.34 + k * 0.17
        if broken and k == 3:
            b.box(E, (-0.1, y, 0.1), (0.55, 0.14, 0.025), rot=(0, 0, 14), long=True)
            b.box(E, (0.45, y + 0.12, 0.13), (0.4, 0.14, 0.025), rot=(14, -6, -20), long=True)
            continue
        b.box(E, (jit(r, 0.006), y, 0.095), (1.12, 0.15, 0.028), rot=(0, jit(r, 0.8), jit(r, 1.2)), long=True)

def sack_stack(a):
    r = a.r; b = a.body; pallet(b, r, 'CL_Oak', False)
    s = lambda x, y, z, yaw, k=1.0, **kw: sack(b, r, (x, y, z), 0.19, 0.6, rot=(0, 90, yaw), tint=(k, k * 0.98, k * 0.94), sc=(0.9, 1.12, 1.0), **kw)
    s(-0.32, 0.0, 0.28, 94, 1.0); s(0.08, 0.02, 0.285, 84, 0.93); s(0.44, -0.02, 0.28, 96, 1.05)
    s(-0.12, 0.0, 0.54, 100, 0.97); s(0.27, 0.02, 0.54, 80, 1.02)
    s(0.06, 0.01, 0.72, 90, 0.95)
    a.foot = (1.2, 0.9); a.lean = (0.004, 0.0)

def sack_stack_broken(a):
    r = a.r; b = a.body; pallet(b, r, 'CL_OakGrey', True)
    sack(b, r, (-0.24, -0.08, 0.1), 0.17, 0.46, rot=(0, 90, 70), tint=(0.8, 0.77, 0.7))
    sack(b, r, (-0.03, 0.24, 0.11), 0.17, 0.46, rot=(0, 90, 120), tint=(0.85, 0.8, 0.72))
    sack(b, r, (0.3, 0.26, 0.1), 0.17, 0.44, rot=(0, 90, 40), tint=(0.78, 0.74, 0.66))
    for (x, y, ang) in ((0.0, -0.2, 20), (-0.3, 0.26, -50)):   # split, slumped sacks with ragged mouths
        sack(b, r, (x, y, 0.1), 0.19, 0.42, rot=(0, 90, ang), sc=(1, 1.1, 0.45), tint=(0.7, 0.66, 0.6), tie=False)
        for i in range(5):
            an = math.radians(ang + i * 72 + r.uniform(-15, 15)); ln = r.uniform(0.06, 0.13)
            b.box('CL_Canvas', (x + 0.24 * math.cos(math.radians(ang)) + math.cos(an) * 0.07, y + 0.24 * math.sin(math.radians(ang)) + math.sin(an) * 0.07, 0.12),
                  (ln, 0.04, 0.012), rot=(0, 0, math.degrees(an)), bev=0, tint=(0.7, 0.66, 0.6))
    for (x, y, rr, hh) in ((-0.2, 0.1, 0.34, 0.13), (0.22, -0.16, 0.26, 0.1), (0.1, 0.12, 0.2, 0.12), (-0.05, -0.28, 0.18, 0.07)):   # spilled grain mound
        b.blob('CL_Grain', (x, y, 0.1), (rr, rr * 0.8, hh), u=8, v=4)
    for i in range(12):
        b.blob('CL_Grain', (r.uniform(-0.5, 0.5), r.uniform(-0.4, 0.4), 0.02), (0.03, 0.03, 0.012), u=4, v=3)
    a.foot = (1.2, 1.0); a.lean = (0.0, 0.0)

# ---------------------------------------------------------------- RainBarrel
def rain_barrel(a):
    r = a.r; b = a.body; E = 'CL_Oak'; R = 0.3; H = 0.9; z0 = 0.14
    b.box('CL_Stone', (0, 0, z0 / 2), (0.74, 0.74, z0), rot=(0, 0, 8), bev=0.02)
    barrel(b, r, (0, 0, z0), R, H)
    top = z0 + H
    b.cyl('CL_Oak', (0, 0, z0 + 0.04), R * 0.9, 0.04, segs=14)
    b.cyl('CL_Water', (0, 0, top - 0.07), R * 0.93, 0.02, segs=14)
    # half lid, askew, with a batten
    b.prism(E, [(math.cos(a_) * R * 0.99, math.sin(a_) * R * 0.99) for a_ in [math.radians(-90 + k * 15) for k in range(13)]], 0.03, c=(0.02, 0.02, top + 0.002), rot=(0, 0, 0), plane='xz') if False else None
    pts = [(R * 0.99 * math.cos(math.radians(-90 + k * 15)), R * 0.99 * math.sin(math.radians(-90 + k * 15))) for k in range(13)]
    with b.xf(T((0.03, 0.0, top + 0.012), (0, 3, 6))):
        t = bmesh.new(); f = t.faces.new([t.verts.new((p[0], p[1], 0)) for p in pts]); rr = bmesh.ops.extrude_face_region(t, geom=[f])
        bmesh.ops.translate(t, vec=(0, 0, 0.03), verts=[e for e in rr['geom'] if isinstance(e, bmesh.types.BMVert)])
        bmesh.ops.bevel(t, geom=list(t.edges), offset=0.006, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        b.absorb(t, E)
        b.box(E, (0.12, 0, 0.045), (0.07, 0.56, 0.03), bev=0.008)
        b.box('CL_Iron', (0.12, 0.22, 0.062), (0.09, 0.02, 0.02), bev=0.004)
    # spigot out the front (-Y)
    fy = -(R * 0.93 + 0.04); tz = z0 + 0.2 * H
    b.box('CL_Iron', (0, fy + 0.01, tz), (0.1, 0.03, 0.1), bev=0.01)
    b.cyl('CL_Iron', (0, fy - 0.06, tz), 0.024, 0.14, rot=(90, 0, 0), segs=8)
    b.cyl('CL_Iron', (0, fy - 0.12, tz - 0.035), 0.021, 0.09, segs=8, r2=0.016)
    b.box('CL_Iron', (0, fy - 0.045, tz + 0.045), (0.1, 0.022, 0.022), rot=(0, 0, 20), bev=0.006)
    # dipper hung on the rim, right side
    with b.xf(T((R * 0.95, 0.05, top - 0.02), (0, 0, 0))):
        b.lathe('CL_Oak', [(0.0, 0.0), (0.06, 0.012), (0.075, 0.07), (0.07, 0.085)], c=(0.07, 0, -0.13), segs=10, smooth=True)
        b.beam('CL_Oak', (0.07, 0, -0.07), (0.07, 0, 0.14), 0.028, 0.022, bev=0.008)
        b.beam('CL_Oak', (0.07, 0, 0.13), (-0.05, 0, 0.1), 0.026, 0.022, bev=0.006)
    a.foot = (0.8, 0.8); a.lean = (0.0, 0.0); a.amp = 0.004

def rain_barrel_broken(a):
    r = a.r; b = a.body; E = 'CL_OakGrey'; R = 0.3; H = 0.78; z0 = 0.14; n = 14
    b.box('CL_Stone', (0, 0, z0 / 2), (0.78, 0.74, z0), rot=(0, 2, -9), bev=0.02)
    b.cyl(E, (0, 0, z0 + 0.04), R * 0.88, 0.04, segs=14)
    splay = [r.uniform(0.0, 0.05) for _ in range(n)]
    for i in (1, 4, 8, 11): splay[i] = r.uniform(0.1, 0.14)
    splay[6] = 0.17
    tops = [r.uniform(0.84, 1.0) for _ in range(n)]
    barrel(b, r, (0, 0, z0), R, H, n=n, slot=E, splay=splay, drop=(3, 10), tops=tops, hoops=(0.1, 0.3))
    # the fallen hoop and two loose staves on the ground
    b.lathe('CL_Iron', [(0.28, 0.0), (0.32, 0.0), (0.32, 0.04), (0.28, 0.04)], c=(0.12, -0.12, 0.02), segs=16, closed=True, rot=(10, 4, 0))
    for (x, y, an) in ((-0.3, -0.12, 70), (0.3, 0.1, 100)):
        d = (math.cos(math.radians(an)), math.sin(math.radians(an)), 0)
        pts = [(x, y, 0.02), (x + d[0] * 0.2, y + d[1] * 0.2, 0.02), (x + d[0] * 0.4, y + d[1] * 0.4, 0.025)]
        b.loft(E, pts, 0.085, 0.036, side=(-d[1], d[0], 0))
    a.foot = (0.9, 0.9); a.lean = (0.0, 0.0); a.amp = 0.005

# ---------------------------------------------------------------- FlowerBox
def flower_box(a, broken):
    r = a.r; b = a.body; E = 'CL_OakGrey' if broken else 'CL_Oak'; P = 'CL_OakGrey' if broken else 'CL_Paint'
    W, D = 1.3, 0.4
    for ix in (-1, 1):
        for iy in (-1, 1):
            b.box(E, (ix * 0.62, iy * 0.16, 0.22), (0.075, 0.075, 0.44), rot=(jit(r, 1.5), jit(r, 1.5), jit(r, 3)), bev=0.012)
        b.beam(E, (ix * 0.62, -0.16, 0.09), (ix * 0.62, 0.16, 0.09), 0.05, 0.05)
    b.box(E, (0, 0, 0.1), (1.2, 0.34, 0.03), bev=0.008, long=True)
    for iy in (-1, 1):
        for zi, z in enumerate((0.29, 0.39)):
            if broken and iy == -1 and zi == 1: continue
            b.box(P, (jit(r, 0.004), iy * (D / 2 - 0.015), z), (1.26, 0.03, 0.098), rot=(jit(r, 1.2), 0, jit(r, 0.9)), long=True)
    for ix in (-1, 1):
        b.box(P, (ix * (W / 2 - 0.015), 0, 0.34), (0.03, D - 0.02, 0.2), rot=(0, 0, jit(r, 1.0)), long=True)
    soil_t = (1.0, 1.0, 1.0) if not broken else (2.2, 2.0, 1.8)
    b.box('CL_Soil', (0, 0, 0.22 if not broken else 0.2), (1.22, 0.34, 0.26), bev=0.015, tint=soil_t)
    if not broken:
        TINTS = [(1, 1, 1), (1.12, 2.3, 2.5), (1.15, 1.7, 1.9), (1, 1, 1), (1.15, 2.6, 2.9), (1, 1, 1), (1.1, 1.9, 2.1), (1.15, 2.6, 2.9)]
        for k in range(8):
            x = -0.56 + k * 0.16 + jit(r, 0.025); y = jit(r, 0.05); z0 = 0.35
            for q in range(5):
                an = r.uniform(0, 2 * math.pi)
                b.lens('CL_Leaf', (x, y, z0), (math.cos(an) * 0.6, math.sin(an) * 0.6 - 0.05, 0.9), r.uniform(0.2, 0.3), 0.1, 0.012)
            for q in range(3):
                an = r.uniform(0, 2 * math.pi); rr = r.uniform(0.0, 0.09)
                ptc = (x + math.cos(an) * rr, y + math.sin(an) * rr - 0.02, z0 + r.uniform(0.15, 0.27))
                if k % 3 == 1: b.blob('CL_PetalB', ptc, r.uniform(0.065, 0.085), u=6, v=4, smooth=True)
                else: b.blob('CL_Petal', ptc, r.uniform(0.065, 0.085), u=6, v=4, smooth=True, tint=TINTS[k])
            if k in (1, 3, 6):   # trail over the front edge
                pts = [(x, -0.1, z0 + 0.02), (x + 0.02, -0.19, z0 + 0.04), (x + 0.04, -0.235, z0 - 0.06), (x + 0.03, -0.24, z0 - 0.2), (x + 0.05, -0.235, z0 - 0.3)]
                b.tube('CL_Leaf', pts, 0.011, 0.005, segs=4)
                for j in (2, 3, 4):
                    p = pts[j]
                    for sd in (-1, 1): b.lens('CL_Leaf', p, (sd * 0.8, -0.35, -0.35), 0.1, 0.06, 0.008)
                b.blob('CL_PetalB' if k == 3 else 'CL_Petal', (pts[3][0], -0.26, pts[3][2] - 0.01), 0.04, u=6, v=4, smooth=True)
        a.foot = (1.4, 0.45)
    else:
        for k in range(6):
            x = -0.5 + k * 0.2 + jit(r, 0.03); y = jit(r, 0.05); z0 = 0.33
            for q in range(3):
                an = r.uniform(0, 2 * math.pi)
                d = Vector((math.cos(an) * 0.35, math.sin(an) * 0.35 - 0.1, 1.0))
                b.blade('CL_Dead', (x, y, z0), d, r.uniform(0.15, 0.3), 0.007, 0.003, segs=4)
                tip = Vector((x, y, z0)) + d.normalized() * 0.2
                if q == 0: b.blob('CL_Dead', tip, (0.026, 0.026, 0.018), u=5, v=3)
            b.lens('CL_Dead', (x, y, z0), (0.8, -0.3, 0.1), 0.1, 0.045, 0.006)
        b.blob('CL_Soil', (0.12, -0.27, 0.03), (0.18, 0.07, 0.05), u=7, v=4, tint=soil_t)   # soil spilled out of the gap
        b.blob('CL_Soil', (-0.3, -0.25, 0.02), (0.1, 0.05, 0.03), u=6, v=3, tint=soil_t)
        b.box(P, (0.05, -0.235, 0.025), (1.2, 0.095, 0.03), rot=(-8, 0, 3), long=True)   # the fallen front board
        a.foot = (1.4, 0.5)
    a.lean = (0.0, 0.0); a.amp = 0.004

# ---------------------------------------------------------------- Well
def well_ring(b, r, broken):
    S = 'CL_Stone'
    b.lathe(S, [(0.5, 0.0), (0.53, 0.0), (0.53, 0.8), (0.5, 0.8)], closed=True, segs=16)   # inner wall
    nb = 10
    for course, (z, h, rr, nn) in enumerate(((0.0, 0.4, 0.665, 10), (0.4, 0.4, 0.665, 10))):
        for i in range(nn):
            an = 2 * math.pi * (i + 0.5 * course) / nn
            if broken and (math.cos(an) > 0.25 or (course == 1 and math.cos(an) > -0.2 and math.sin(an) < 0.3)): continue
            wd = 2 * 0.665 * math.sin(math.pi / nn) * 0.97
            b.box(S, (math.cos(an) * rr, math.sin(an) * rr, z + h / 2), (0.28, wd, h - 0.012), rot=(0, 0, math.degrees(an) + jit(r, 3)), bev=0.016, tint=(r.uniform(0.9, 1.05),) * 3)
    for i in range(10):   # chunky coping
        an = 2 * math.pi * (i + 0.25) / 10
        if broken and (math.cos(an) > 0.0): continue
        b.box(S, (math.cos(an) * 0.665, math.sin(an) * 0.665, 0.84), (0.34, 2 * 0.665 * math.sin(math.pi / 10) * 0.99, 0.09), rot=(0, 0, math.degrees(an) + jit(r, 2)), bev=0.02, tint=(r.uniform(0.92, 1.05),) * 3)

def well(a):
    r = a.r; b = a.body; E = 'CL_Oak'
    well_ring(b, r, False)
    b.cyl('CL_Water', (0, 0, 0.3), 0.5, 0.03, segs=14)
    for sx in (-1, 1):
        b.box(E, (sx * 0.8, 0, 1.2), (0.16, 0.16, 2.4), rot=(jit(r, 0.8), jit(r, 0.8), jit(r, 4)), bev=0.016)
        b.box('CL_Stone', (sx * 0.8, 0, 0.06), (0.3, 0.3, 0.12), bev=0.02)
        for sy in (-1, 1):   # braces
            b.beam(E, (sx * 0.8, sy * 0.06, 1.9), (sx * 0.5, sy * 0.06, 2.35), 0.07, 0.09)
    b.beam(E, (-0.9, 0, 2.38), (0.9, 0.0, 2.38), 0.15, 0.17)   # crossbeam
    # winch drum, rope wound on it, crank, rope to the bucket
    b.cyl(E, (0, 0, 1.95), 0.11, 1.4, rot=(0, 90, 0), segs=10)
    b.cyl('CL_Rope', (0.1, 0, 1.95), 0.135, 0.5, rot=(0, 90, 0), segs=10, tint=(0.95, 0.9, 0.85))
    b.beam('CL_Iron', (0.7, 0, 1.95), (0.93, 0.0, 1.95), 0.04, 0.04); b.beam('CL_Iron', (0.92, 0.0, 1.95), (0.92, -0.2, 2.12), 0.035, 0.035)
    b.beam(E, (0.92, -0.2, 2.08), (0.92, -0.2, 2.22), 0.05, 0.05)
    bk = Vector((0.22, -0.66, 0.89))
    b.tube('CL_Rope', [(0.1, -0.135, 1.95), (0.14, -0.3, 1.7), (0.2, -0.5, 1.2), (0.22, -0.64, 1.08), (bk.x, bk.y, bk.z + 0.26)], 0.012, segs=5)
    b.lathe(E, [(0.12, 0.0), (0.15, 0.02), (0.17, 0.26), (0.0, 0.26)][:3] + [(0.17, 0.26)], c=bk, segs=10, smooth=False)
    b.lathe('CL_Iron', [(0.165, 0.04), (0.18, 0.04), (0.18, 0.07), (0.165, 0.07)], c=bk, segs=10, closed=True)
    b.lathe('CL_Iron', [(0.17, 0.2), (0.185, 0.2), (0.185, 0.23), (0.17, 0.23)], c=bk, segs=10, closed=True)
    b.beam('CL_Iron', (bk.x - 0.17, bk.y, bk.z + 0.24), (bk.x + 0.17, bk.y, bk.z + 0.24), 0.015, 0.015, bev=0.003)
    b.cyl('CL_Water', bk + Vector((0, 0, 0.22)), 0.15, 0.01, segs=10)
    # small tiled gable roof, ridge along x
    ys = 0.82; z_e = 2.5; z_r = 3.0
    pitch = math.degrees(math.atan2(z_r - z_e, ys))
    for sy in (-1, 1):
        for c_ in range(4):   # four overlapping tile courses per slope, eave to ridge
            f = (c_ + 0.5) / 4.0
            b.box('CL_Tile', (0, sy * ys * (1 - f), z_e + (z_r - z_e) * f + 0.03), (1.95, 0.3, 0.05), rot=(-pitch * sy, 0, jit(r, 0.8)), bev=0.012, long=True)
    b.beam(E, (-1.0, 0, z_r + 0.02), (1.0, 0.0, z_r + 0.02), 0.14, 0.1)
    for sx in (-1, 1):   # gable ends
        b.prism(E, [(-ys, z_e - 0.04), (ys, z_e - 0.04), (0, z_r)], 0.05, c=(sx * 0.93, 0, 0), plane='yz', bev=0.008)
    a.foot = (2.0, 2.0); a.lean = (0.0, 0.004); a.amp = 0.005

def well_ruined(a):
    r = a.r; b = a.body; E = 'CL_OakGrey'
    well_ring(b, r, True)
    for (x, y, an, z) in ((0.82, 0.35, 20, 0.14), (0.72, -0.62, -40, 0.14), (0.84, -0.2, 70, 0.13), (0.55, 0.74, 100, 0.12), (0.2, -0.82, 5, 0.12), (0.8, 0.05, 200, 0.28)):
        b.box('CL_Stone', (x, y, z), (0.3, 0.2, z * 2 - 0.01), rot=(jit(r, 8), jit(r, 8), an), bev=0.02, tint=(r.uniform(0.85, 1.0),) * 3)
    # post stump, leaning post, crossbeam fallen across the ring
    b.box(E, (-0.8, 0, 0.17), (0.16, 0.16, 0.34), rot=(2, -3, 5), bev=0.016, taper=0.82)
    b.box('CL_Stone', (-0.8, 0, 0.06), (0.3, 0.3, 0.12), bev=0.02)
    b.box('CL_Stone', (0.8, 0, 0.06), (0.3, 0.3, 0.12), bev=0.02)
    b.G = pivot((0.8, 0, 0.1), (0, 9, 3))
    b.box(E, (0.8, 0, 0.74), (0.16, 0.16, 1.28), bev=0.016, taper=0.9)
    b.G = Matrix.Identity(4)
    b.beam(E, (-0.5, 0.7, 0.88), (0.5, -0.65, 0.985), 0.15, 0.17)
    b.beam(E, (-0.35, -0.5, 0.02), (-0.05, -0.9, 0.025), 0.07, 0.09)   # a brace on the ground
    for (x, y, an) in ((-0.9, -0.55, 25), (-0.7, 0.75, 70)):
        b.box('CL_Tile', (x, y, 0.025), (0.28, 0.2, 0.04), rot=(jit(r, 8), jit(r, 8), an), bev=0.01)
    b.box('CL_Iron', (0.1, 0.4, 0.88), (0.2, 0.02, 0.05), rot=(0, 0, 40), bev=0.005)   # the crank, bent
    a.foot = (2.0, 2.0); a.lean = (0.0, 0.0); a.amp = 0.005

# ---------------------------------------------------------------- signs
def board_pts(shape, hw, hh):
    if shape == 'round':
        arc = [(hw * math.cos(math.radians(a_)), hh - hw * 0.45 + hw * 0.45 * math.sin(math.radians(a_))) for a_ in range(0, 181, 30)]
        return [(hw, -hh)] + arc + [(-hw, -hh)]
    if shape == 'shield':
        return [(hw, hh), (hw, -hh * 0.1), (hw * 0.75, -hh * 0.6), (0, -hh), (-hw * 0.75, -hh * 0.6), (-hw, -hh * 0.1), (-hw, hh), (0, hh - 0.03)][::-1]
    n = 0.07
    return [(hw - n, -hh), (hw, -hh + n), (hw, hh - n), (hw - n, hh), (-hw + n, hh), (-hw, hh - n), (-hw, -hh + n), (-hw + n, -hh)]

G = 'CL_Gilt'
def emblem(b, r, trade):
    """Built in the board frame (x = out of the face, y along the arm, z up), face at x = 0.025; called once per side with x mirrored."""
    z0 = 0.025
    if trade == 'Bakery':
        b.blob(G, (z0 + 0.02, 0, -0.01), (0.045, 0.17, 0.095), u=10, v=6, smooth=True)
        for k in (-1, 0, 1):
            b.beam(G, (z0 + 0.052, k * 0.07 - 0.03, -0.045), (z0 + 0.052, k * 0.07 + 0.02, 0.04), 0.03, 0.02, bev=0.006, up=(1, 0, 0))
        b.blob(G, (z0 + 0.012, 0.0, -0.17), (0.02, 0.15, 0.025), u=8, v=4)   # a cloth under the loaf
    elif trade == 'Inn':
        b.cyl(G, (z0 + 0.06, -0.01, -0.02), 0.095, 0.25, segs=12, r2=0.112, smooth=True)
        b.lathe(G, [(0.085, -0.012), (0.108, -0.012), (0.108, 0.012), (0.085, 0.012)], c=(z0 + 0.06, -0.01, 0.1), segs=12, closed=True)   # rim
        b.tube(G, [(z0 + 0.06, 0.09, 0.07), (z0 + 0.06, 0.18, 0.07), (z0 + 0.06, 0.205, -0.0), (z0 + 0.06, 0.18, -0.07), (z0 + 0.06, 0.1, -0.08)], 0.02, segs=5)
        for k in range(5): b.blob(G, (z0 + 0.06 + 0.045 * math.cos(k * 1.3), -0.01 + 0.045 * math.sin(k * 1.3), 0.125 + 0.015 * (k % 2)), (0.045, 0.045, 0.034), u=6, v=4, smooth=True)
    elif trade == 'Smithy':
        pts = [(-0.11, -0.17), (0.11, -0.17), (0.09, -0.1), (0.05, -0.08), (0.05, -0.02), (0.15, 0.0), (0.2, 0.02), (0.2, 0.07), (-0.02, 0.09), (-0.2, 0.025), (-0.2, 0.0), (-0.05, -0.02), (-0.05, -0.08), (-0.09, -0.1)]
        b.prism(G, pts, 0.05, c=(z0 + 0.025, 0, 0.0), plane='yz', bev=0.006)
        b.beam(G, (z0 + 0.0, -0.17, 0.2), (z0 + 0.0, -0.17, 0.2), 0.001, 0.001) if False else None
    elif trade == 'Workshop':
        for sgn, slot_off in ((1, 0.0), (-1, 0.03)):
            with b.xf(T((z0 + 0.02 + slot_off, 0, 0), (sgn * 42, 0, 0))):
                if sgn == 1:   # saw: toothed blade and a handle
                    teeth = [(-0.19 + k * 0.038, -0.035 - (0.02 if k % 2 else 0)) for k in range(11)]
                    b.prism(G, [(-0.19, 0.04)] + teeth + [(0.19, 0.04)], 0.02, plane='yz')
                    b.box(G, (0.0, 0.22, 0.0), (0.02, 0.1, 0.09), bev=0.008)
                    b.box(G, (0.0, 0.0, 0.0), (0.02, 0.0, 0.0), bev=0) if False else None
                else:   # hammer: handle and head
                    b.beam(G, (0.0, -0.22, 0.0), (0.0, 0.2, 0.0), 0.035, 0.03, bev=0.008, up=(1, 0, 0))
                    b.box(G, (0.0, 0.17, 0.0), (0.05, 0.075, 0.14), bev=0.01)
    elif trade == 'Tailor':
        for sd in (-1, 1):
            b.beam(G, (z0 + 0.025 + (0.0 if sd < 0 else 0.02), -sd * 0.07, -0.13), (z0 + 0.025 + (0.0 if sd < 0 else 0.02), sd * 0.11, 0.2), 0.04, 0.03, bev=0.007, up=(1, 0, 0))
            b.lathe(G, [(0.035, -0.011), (0.062, -0.011), (0.062, 0.011), (0.035, 0.011)], c=(z0 + 0.03, -sd * 0.085, -0.17), segs=10, closed=True, rot=(0, 90, 0))
        b.cyl(G, (z0 + 0.065, 0, 0.0), 0.022, 0.03, rot=(0, 90, 0), segs=8)
    elif trade == 'Grocer':
        for sd in (-1, 1): b.blob(G, (z0 + 0.04, sd * 0.055, -0.03), (0.06, 0.085, 0.1), u=8, v=5, smooth=True)
        b.blob(G, (z0 + 0.06, 0, -0.1), (0.04, 0.07, 0.035), u=6, v=4, smooth=True)
        b.beam(G, (z0 + 0.05, 0, 0.06), (z0 + 0.05, 0.02, 0.17), 0.022, 0.022, bev=0.004)
        b.lens(G, (z0 + 0.055, 0.02, 0.14), (0, 0.75, 0.55), 0.17, 0.09, 0.016, up=(1, 0, 0))

def chain(b, y, zt, zb, r, slot='CL_Iron'):
    n = max(2, int(abs(zt - zb) / 0.045)); step = (zt - zb) / n
    for i in range(n):
        b.box(slot, (jit(r, 0.002), y, zt - (i + 0.5) * step), (0.012, 0.012, step * 1.25) if i % 2 == 0 else (0.012, 0.03, step * 1.25), bev=0.0, rot=(0, 0, 0))

def bracket(b, r, bend=0.0, reach=0.9):
    I = 'CL_Iron'
    b.box(I, (0, -0.015, 0.0), (0.3, 0.03, 0.4), rot=(0, 0, jit(r, 1.5)), bev=0.012)
    for sx in (-1, 1):
        for sz in (-1, 1): b.blob(I, (sx * 0.1, -0.034, sz * 0.14), (0.022, 0.012, 0.022), u=6, v=4)
    ab = math.radians(bend); bendy = -0.3 if bend else -reach
    if bend:
        e1 = Vector((0, -0.3, 0.06)); e2 = e1 + Vector((0, -(reach - 0.3) * math.cos(ab), -(reach - 0.3) * math.sin(ab)))
        b.beam(I, (0, -0.03, 0.06), e1, 0.04, 0.045); b.beam(I, e1 - Vector((0, 0.02, 0)), e2, 0.04, 0.045)
    else:
        e2 = Vector((0, -reach, 0.06)); b.beam(I, (0, -0.03, 0.06), e2, 0.04, 0.045)
    b.blob(I, e2 + Vector((0, -0.02, 0.0)), 0.034, u=6, v=4)
    # diagonal brace and a scroll below the arm
    b.beam(I, (0, -0.03, -0.15), (0, -0.52, 0.05) if not bend else (0, -0.38, 0.02), 0.03, 0.036, bev=0.006)
    pts = [(0, -0.2 - 0.07 * math.cos(2.0 + k * 0.45), -0.02 - 0.07 * math.sin(2.0 + k * 0.45) * 1.0 - 0.02 * k * 0.1) for k in range(7)]
    spiral = []
    for k in range(13):
        an = math.radians(-60 + k * 22); rad = 0.095 - 0.05 * k / 12
        spiral.append((0, -0.3 + rad * math.cos(an) * 0.0 - rad * math.cos(an), -0.05 + rad * math.sin(an)))
    b.tube(I, spiral, 0.011, 0.007, segs=4)
    return e2

def sign_asset(a, trade, slot, shape, broken=False):
    r = a.r; b = a.body; hw, hh = 0.3, 0.25
    if not broken:
        bracket(b, r)
        cy, cz = -0.55, -0.42
        for y in (-0.33, -0.77):
            chain(b, y, 0.035, cz + hh - 0.01, r)
        with b.xf(T((0, cy, cz), (jit(r, 1.2), 0, 0))):
            pts = board_pts(shape, hw, hh)
            b.prism(slot, pts, 0.05, plane='yz', bev=0.008)
            rim = [(p[0] * 0.86, p[1] * 0.86) for p in pts]
            b.blob('CL_Iron', (0, -hw * 0.5, hh + 0.0), 0.02, u=5, v=3); b.blob('CL_Iron', (0, hw * 0.5, hh), 0.02, u=5, v=3)
            for sgn in (1, -1):
                with b.xf(T(sc=(sgn, 1, 1))):
                    emblem(b, r, trade)
    else:
        e2 = bracket(b, r, bend=25.0)
        ax_y = -0.74; az = 0.06 - (abs(ax_y) - 0.3) * math.tan(math.radians(25)) - 0.025
        topz = az - 0.075
        chain(b, ax_y, az, topz, r)
        hw2, hh2 = 0.21, 0.14
        pts = board_pts('plain', hw2, hh2)
        pts = [p for p in pts if not (p[0] < -0.1 and p[1] < -0.02)] + []
        pts = [(hw2, -hh2), (hw2, hh2 - 0.07), (hw2 - 0.07, hh2), (-hw2 + 0.02, hh2), (-hw2, hh2 - 0.04), (-hw2 + 0.06, hh2 * 0.1), (-hw2 + 0.02, -hh2 * 0.35), (-hw2 + 0.12, -hh2 * 0.6), (-hw2 + 0.1, -hh2)]
        # board hangs from its far top corner (toward -y), tilted so the wall side drops
        corner = Vector((0, ax_y, topz)); loc = Vector((0, hw2, -hh2))   # board centre relative to the corner when upright (the board extends toward +y)
        with b.xf(Matrix.Translation(corner) @ Euler((math.radians(-35), 0, 0)).to_matrix().to_4x4() @ Matrix.Translation((0, hw2, -hh2))):
            b.prism('CL_OakGrey', [(-p[0], p[1]) for p in pts], 0.05, plane='yz', bev=0.008)
            b.box(G, (0.0, 0.0, 0.0), (0.06, 0.0001, 0.0001), bev=0) if False else None
            for sgn in (1, -1):
                b.box(G, (sgn * 0.0275, 0.0, 0.02), (0.006, 0.26, 0.026), bev=0.002, tint=(0.8, 0.8, 0.8))
            b.blob('CL_Iron', (0, -hw2 + 0.03, hh2 - 0.02), 0.02, u=5, v=3)
    a.foot = None; a.amp = 0.0; a.lean = (0.0, 0.0)

# ---------------------------------------------------------------- washing line
def cloth(p, slot, shape, nu, nv, r, billow=0.03, t0=0.0, t1=1.0, s0=0.0, s1=1.0, yoff=0.0, holes=(), tint=None, k1=11.0, ph=None):
    """A double-sided cloth sheet in the X-Z plane; shape(s, t) -> (x, z), s across 0..1, t down 0..1; billow in Y (more toward the hem)."""
    ph = r.uniform(0, 6) if ph is None else ph
    def fn(s, t):
        x, z = shape(s, t)
        y = billow * (0.6 * math.sin(x * k1 + ph) + 0.4 * math.sin(x * k1 * 2.1 + ph * 1.7)) * (0.25 + 0.75 * t) + yoff
        return (x, y, z)
    pts = [fn(s0 + (s1 - s0) * i / (nu - 1), t0 + (t1 - t0) * j / (nv - 1)) for i in range(nu) for j in range(nv)]
    faces = [(i * nv + j, i * nv + j + 1, (i + 1) * nv + j + 1, (i + 1) * nv + j) for i in range(nu - 1) for j in range(nv - 1) if (i, j) not in holes]
    p.thin(slot, pts, faces, lambda q: (0, -1, 0), half=0.0035, tint=tint)

def washing_line(a):
    r = a.r
    p = a.piece('Rope_1m')
    pts = [(k / 8.0, jit(r, 0.004), jit(r, 0.004)) for k in range(9)]
    p.tube('CL_Rope', pts, 0.008, segs=5)
    for k in range(1, 8):   # twist ridges
        p.box('CL_Rope', (k / 8.0, 0, 0), (0.012, 0.018, 0.018), rot=(k * 40, 0, 0), bev=0.0)
    p = a.piece('Hook')
    p.box('CL_Iron', (0, -0.008, 0), (0.075, 0.016, 0.085), rot=(0, 0, jit(r, 3)), bev=0.008)
    for sz in (-1, 1): p.blob('CL_Iron', (0, -0.019, sz * 0.027), (0.011, 0.007, 0.011), u=5, v=3)
    p.beam('CL_Iron', (0, -0.012, 0), (0, -0.115, 0.005), 0.02, 0.02, bev=0.004)
    p.lathe('CL_Iron', [(0.028, -0.007), (0.043, -0.007), (0.043, 0.007), (0.028, 0.007)], c=(0, -0.135, 0.0), segs=12, closed=True, rot=(0, 90, 0))
    # --- cloths
    def rect(W, L, wob=0.0):
        return lambda s, t: ((s - 0.5) * W, -t * L - wob * math.sin(s * 9) * t)
    p = a.piece('Cloth_Shirt')
    def shirt_body(s, t):
        W = 0.44 + 0.04 * t; zt = -0.075 * math.exp(-((s - 0.5) / 0.15) ** 2) * (1 - t)
        return ((s - 0.5) * W, zt - t * 0.68 * (1 - 0.0) + 0.02 * math.sin(s * 7) * t)
    cloth(p, 'CL_ClothBlue', shirt_body, 8, 8, r, billow=0.06)
    for sd in (-1, 1):
        def sleeve(s, t, sd=sd):
            x0 = sd * (0.215 + 0.26 * s); z0 = -0.17 * s - 0.0
            return (x0 + sd * 0.0, z0 - t * 0.17 + 0.04 * (1 - s) * 0)
        cloth(p, 'CL_ClothBlue', sleeve, 5, 4, r, billow=0.02, yoff=0.004)
    p = a.piece('Cloth_Sheet')
    cloth(p, 'CL_Linen', lambda s, t: ((s - 0.5) * 1.2, -t * 0.93 + 0.025 * math.sin(s * 14) * t), 13, 9, r, billow=0.1, k1=8.0)
    p = a.piece('Cloth_Apron')
    cloth(p, 'CL_ClothRed', lambda s, t: ((s - 0.5) * (0.30 + 0.36 * t ** 0.8), -t * 0.66 + 0.03 * math.sin(s * 8) * t), 8, 8, r, billow=0.06)
    cloth(p, 'CL_Linen', lambda s, t: ((s - 0.5) * 0.2, -0.33 - t * 0.17), 4, 3, r, billow=0.0, yoff=-0.012, ph=0)
    for sd in (-1, 1):
        cloth(p, 'CL_ClothRed', lambda s, t, sd=sd: (sd * (0.15 + 0.045 * t + 0.01 * math.sin(s * 9)), -s * 0.34), 3, 5, r, billow=0.015, yoff=0.004, ph=sd)
    p = a.piece('Cloth_Towel')
    tw = lambda s, t: ((s - 0.5) * 0.4, -t * 0.7 + 0.015 * math.sin(s * 10) * t)
    cloth(p, 'CL_Linen', tw, 7, 8, r, billow=0.03)
    for (ta, tb) in ((0.74, 0.8), (0.86, 0.92)):
        cloth(p, 'CL_ClothBlue', tw, 7, 3, r, billow=0.03, t0=ta, t1=tb, yoff=-0.004, ph=0)
    p = a.piece('Cloth_Socks')
    for k, sd in enumerate((-1, 1)):
        leg = lambda s, t, sd=sd: (sd * 0.075 + (s - 0.5) * 0.095, -t * 0.2)
        foot = lambda s, t, sd=sd: (sd * 0.075 + (s - 0.5) * 0.095 + sd * 0.06 * t * (2 * s if False else 1.0) * (0.0 + s * 0.0) + sd * 0.1 * t, -0.2 - 0.1 * t * (1 - 0.3 * s * 0) - 0.0)
        cloth(p, 'CL_Linen', leg, 4, 5, r, billow=0.01, ph=k)
        def foot2(s, t, sd=sd):
            x = sd * 0.075 + (s - 0.5) * 0.095 * (1 - 0.2 * t) + sd * 0.1 * t * t; z = -0.2 - 0.1 * (0.3 + 0.7 * t) * (0.5 + s * 0.5 if sd > 0 else 1.0 - 0.5 * s)
            return (x, -0.2 - (0.1 * (1 - abs(s - 0.5) * 0.6)) * (t * 0.0 + 1.0) * t - 0.0)
        cloth(p, 'CL_Linen', foot2, 4, 4, r, billow=0.01, ph=k)
        cloth(p, 'CL_ClothRed', leg, 4, 2, r, billow=0.01, t1=0.2, yoff=-0.004, ph=k)
    p = a.piece('Peg')
    for sd in (-1, 1):
        p.box('CL_Oak', (sd * 0.006, 0, -0.04), (0.013, 0.012, 0.1), rot=(0, sd * 2.0, 0), bev=0.002)
    p.box('CL_Iron', (0, 0, -0.012), (0.03, 0.016, 0.01), bev=0.0)
    # torn
    p = a.piece('Cloth_Rag_A')
    def ragA(s, t):
        W = 0.5 - 0.1 * t * (1 if s < 0.5 else 0.3); bot = 0.62 + 0.14 * math.sin(s * 17) * t + 0.1 * (s - 0.5) * t
        return ((s - 0.5) * W * 1.0 - 0.04 * t, -t * bot)
    cloth(p, 'CL_Rag', ragA, 9, 9, r, billow=0.035, holes=((3, 5), (3, 6), (4, 6), (6, 7)))
    for k in range(3):
        cloth(p, 'CL_Rag', lambda s, t, k=k: (-0.2 + k * 0.12 + 0.02 * math.sin(t * 5 + k), -0.56 - t * (0.2 + 0.05 * k)), 2, 4, r, billow=0.01, yoff=0.006, s1=0.25)
    p = a.piece('Cloth_Rag_B')
    def ragB(s, t):
        W = 0.34 + 0.06 * t; bot = 0.46 + 0.1 * math.sin(s * 11 + 1) * t
        return ((s - 0.5) * W + 0.03 * t, -t * bot)
    cloth(p, 'CL_Rag', ragB, 7, 8, r, billow=0.03, holes=((2, 4), (4, 6), (5, 2)))
    for k in range(3):
        cloth(p, 'CL_Rag', lambda s, t, k=k: (-0.12 + k * 0.1, -0.44 - t * (0.12 + 0.06 * k)), 2, 4, r, billow=0.01, yoff=-0.006, s1=0.3)

# ---------------------------------------------------------------- registry
SIGNS = [('Bakery', 'CL_PaintRed', 'round'), ('Inn', 'CL_Oak', 'plain'), ('Smithy', 'CL_Paint', 'shield'), ('Workshop', 'CL_Oak', 'plain'), ('Tailor', 'CL_Paint', 'round'), ('Grocer', 'CL_PaintRed', 'shield')]
def mk(name, fn, *args, body=True):
    def go():
        a = Asset(name, None, body=body); fn(a, *args); return a
    return go
ASSETS = {
    'Handcart': mk('Handcart', handcart, False), 'Handcart_Broken': mk('Handcart_Broken', handcart, True),
    'CrateStack': mk('CrateStack', crate_stack), 'CrateStack_Broken': mk('CrateStack_Broken', crate_stack_broken),
    'SackStack': mk('SackStack', sack_stack), 'SackStack_Broken': mk('SackStack_Broken', sack_stack_broken),
    'RainBarrel': mk('RainBarrel', rain_barrel), 'RainBarrel_Broken': mk('RainBarrel_Broken', rain_barrel_broken),
    'FlowerBox': mk('FlowerBox', flower_box, False), 'FlowerBox_Broken': mk('FlowerBox_Broken', flower_box, True),
    'Well': mk('Well', well), 'Well_Ruined': mk('Well_Ruined', well_ruined),
}
for _t, _s, _sh in SIGNS:
    ASSETS['ShopSign_' + _t] = mk('ShopSign_' + _t, sign_asset, _t, _s, _sh)
ASSETS['ShopSign_Broken'] = mk('ShopSign_Broken', sign_asset, 'Broken', 'CL_OakGrey', 'plain', True)
ASSETS['WashingLine'] = mk('WashingLine', washing_line, body=False)
BUDGET = {'Well': 6000, 'Well_Ruined': 6000}

# ---------------------------------------------------------------- materials
def hex2lin(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]

TILE = {'CL_Oak': ('oak_timber', (0.80, 0.72, 0.62)), 'CL_OakGrey': ('oak_timber', (0.62, 0.60, 0.58)), 'CL_Paint': ('painted_wood', (0.60, 0.72, 0.52)),
        'CL_PaintRed': ('painted_wood', (0.85, 0.45, 0.32)), 'CL_Iron': ('iron', (1, 1, 1)), 'CL_Stone': ('sandstone', (1, 1, 1)), 'CL_Tile': ('roof_tile', (1, 1, 1)),
        'CL_Canvas': ('canvas', (1.0, 0.94, 0.82)), 'CL_Rope': ('canvas', (1.0, 0.85, 0.62)), 'CL_Linen': ('canvas', (1.15, 1.12, 1.05)),
        'CL_ClothBlue': ('canvas', (0.45, 0.58, 0.80)), 'CL_ClothRed': ('canvas', (0.95, 0.55, 0.45)), 'CL_Rag': ('canvas', (0.62, 0.58, 0.52))}
FLATM = {'CL_Soil': ((0.10, 0.07, 0.045), 0.15), 'CL_Grain': ((0.62, 0.48, 0.25), 0.2), 'CL_Leaf': ((0.18, 0.30, 0.08), 0.3), 'CL_Petal': ((0.85, 0.35, 0.30), 0.3),
         'CL_PetalB': ((0.85, 0.72, 0.25), 0.3), 'CL_Dead': ((0.30, 0.20, 0.10), 0.2), 'CL_Water': ((0.025, 0.04, 0.03), 0.94), 'CL_Gilt': ((0.70, 0.48, 0.16), 0.5)}
LUMA = {'CL_Paint': 2.0, 'CL_PaintRed': 2.0, 'CL_Linen': 2.4, 'CL_ClothBlue': 2.2, 'CL_ClothRed': 2.2, 'CL_Rag': 2.2}
MATS = {}
def get_mat(name):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    at = nt.nodes.new('ShaderNodeAttribute'); at.attribute_name = 'tint'; at.attribute_type = 'GEOMETRY'
    mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.blend_type = 'MULTIPLY'; mx.inputs[0].default_value = 1.0
    nt.links.new(at.outputs['Color'], mx.inputs[7]); nt.links.new(mx.outputs[2], bs.inputs['Base Color'])
    if name in FLATM:
        col, rough = FLATM[name]; mx.inputs[6].default_value = (*col, 1.0); bs.inputs['Roughness'].default_value = rough
        if name == 'CL_Water': bs.inputs['Metallic'].default_value = 0.0
    elif name in TILE:
        tn, tc = TILE[name]; d = os.path.join(TEX, tn)
        def img(suffix, cs):
            n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(os.path.join(d, '%s_%s.png' % (tn, suffix)), check_existing=True)
            n.image.colorspace_settings.name = cs; return n
        c = img('albedo', 'sRGB'); o = img('orm', 'Non-Color'); nrm = img('normal', 'Non-Color')
        sep = nt.nodes.new('ShaderNodeSeparateColor'); nm = nt.nodes.new('ShaderNodeNormalMap')
        m1 = nt.nodes.new('ShaderNodeMix'); m1.data_type = 'RGBA'; m1.blend_type = 'MULTIPLY'; m1.inputs[0].default_value = 1.0; m1.inputs[7].default_value = (*tc, 1.0)
        if name in LUMA:   # review renders only: the tileable's blue-grey / ochre hue would swamp the contract colour, so its luminance carries the tint instead
            bw = nt.nodes.new('ShaderNodeRGBToBW'); mu = nt.nodes.new('ShaderNodeMath'); mu.operation = 'MULTIPLY'; mu.inputs[1].default_value = LUMA[name]
            cc = nt.nodes.new('ShaderNodeCombineColor'); nt.links.new(c.outputs['Color'], bw.inputs[0]); nt.links.new(bw.outputs[0], mu.inputs[0])
            for k in range(3): nt.links.new(mu.outputs[0], cc.inputs[k])
            nt.links.new(cc.outputs[0], m1.inputs[6])
        else:
            nt.links.new(c.outputs['Color'], m1.inputs[6])
        nt.links.new(m1.outputs[2], mx.inputs[6])
        nt.links.new(o.outputs['Color'], sep.inputs[0]); nt.links.new(nrm.outputs['Color'], nm.inputs['Color'])
        nt.links.new(sep.outputs[1], bs.inputs['Roughness']); nt.links.new(sep.outputs[2], bs.inputs['Metallic']); nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    elif name == 'FOOTPRINT':
        mx.inputs[6].default_value = (0.8, 0.2, 0.2, 1)
    else:
        raise KeyError('unknown slot ' + name)
    MATS[name] = m
    return m

def finish(a, piece):
    bm = piece.bm
    for v in bm.verts:
        co = v.co; f = min(1.0, max(0.0, co.z / 0.4))
        if a.amp:
            n = Vector((noise.noise(co * 0.9 + a.off), noise.noise(co * 0.9 + a.off + Vector((17.3, 0, 0))), noise.noise(co * 0.9 + a.off + Vector((0, 31.7, 0)))))
            co += n * a.amp * f
        co.x += a.lean[0] * max(0.0, co.z); co.y += a.lean[1] * max(0.0, co.z)
    bm.normal_update()
    uv = bm.loops.layers.uv.get('UVMap') or bm.loops.layers.uv.new('UVMap')
    for f in bm.faces:
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            u = (p.y, p.z) if ax == 0 else (p.x, p.z) if ax == 1 else (p.x, p.y)
            l[uv].uv = (u[0] / 2.0, u[1] / 2.0)
    me = bpy.data.meshes.new(a.name + '_' + piece.name)
    bm.to_mesh(me)
    for s in piece.slots: me.materials.append(get_mat(s))
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    for poly in me.polygons:
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 3.1 + a.off)
            grime = max(0.0, 1.0 - p.z / 0.6) * 0.75 * (0.65 + 0.35 * nz) + 0.15 * max(0.0, noise.noise(p * 1.3 + a.off))
            moss = (max(0.0, 1.0 - p.z / 0.3) * max(0.0, nz) * 0.6) if poly.normal.z < 0.7 else 0.0
            attr.data[li].color = (min(1.0, grime), min(1.0, moss), 0.0, 1.0)
    tn = me.color_attributes.new('tint', 'FLOAT_COLOR', 'CORNER')
    for poly in me.polygons:
        tc = piece.ftint[poly.index]
        for li in poly.loop_indices: tn.data[li].color = (tc[0], tc[1], tc[2], 1.0)
    bm.free()
    me.transform(Matrix.Translation(-piece.origin))
    ob = bpy.data.objects.new(piece.name, me); bpy.context.scene.collection.objects.link(ob)
    ob.location = piece.origin
    return ob

def build(name):
    a = ASSETS[name]()
    root = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(root); root.empty_display_type = 'PLAIN_AXES'
    objs = []
    if a.foot:   # pivot = ground centre of the footprint: centre the built geometry's x-y bounds on the origin
        vs = [v.co for p in a.objs for v in p.bm.verts]
        sx = -(min(v.x for v in vs) + max(v.x for v in vs)) / 2; sy = -(min(v.y for v in vs) + max(v.y for v in vs)) / 2
        for p in a.objs: bmesh.ops.translate(p.bm, vec=(sx, sy, 0), verts=p.bm.verts[:])
    for p in a.objs:
        o = finish(a, p); o.parent = root; objs.append(o); o.matrix_parent_inverse = Matrix.Identity(4)
    if a.foot:
        fm = bpy.data.meshes.new('FOOTPRINT'); fb = bmesh.new(); bmesh.ops.create_cube(fb, size=1.0)
        for v in fb.verts: v.co = Vector((v.co.x * a.foot[0], v.co.y * a.foot[1], v.co.z * 0.1 + 0.05))
        fb.to_mesh(fm); fb.free(); fm.materials.append(get_mat('FOOTPRINT'))
        fo = bpy.data.objects.new('FOOTPRINT', fm); bpy.context.scene.collection.objects.link(fo); fo.parent = root; fo.hide_render = True
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs}
    return a, root, objs, tris

def export(a, root):
    d = os.path.join(OUT, a.name); os.makedirs(d, exist_ok=True); fbx = os.path.join(d, a.name + '.fbx')
    bpy.ops.object.select_all(action='DESELECT'); root.select_set(True)
    for o in root.children: o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'EMPTY', 'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False, path_mode='STRIP')
    if EXPORT:
        dd = os.path.join(DEST, a.name); os.makedirs(dd, exist_ok=True); shutil.copy(fbx, os.path.join(dd, a.name + '.fbx'))

def world_bounds(root, names=None):
    pts = []
    for o in root.children:
        if o.type == 'MESH' and o.name.split('@')[0] != 'FOOTPRINT' and (names is None or o.name.split('@')[0] in names):
            pts += [o.matrix_world @ v.co for v in o.data.vertices]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx

# ---------------------------------------------------------------- review renders
def proj_mat(name, tex, scale, rough=None):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    tc = nt.nodes.new('ShaderNodeTexCoord'); mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (scale, scale, scale)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    def img(suffix, cs):
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(os.path.join(TEX, tex, '%s_%s.png' % (tex, suffix)), check_existing=True)
        n.image.colorspace_settings.name = cs; nt.links.new(mp.outputs[0], n.inputs['Vector']); return n
    c = img('albedo', 'sRGB'); o = img('orm', 'Non-Color'); nrm = img('normal', 'Non-Color')
    sep = nt.nodes.new('ShaderNodeSeparateColor'); nm = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(o.outputs['Color'], sep.inputs[0]); nt.links.new(nrm.outputs['Color'], nm.inputs['Color'])
    nt.links.new(c.outputs['Color'], bs.inputs['Base Color']); nt.links.new(sep.outputs[1], bs.inputs['Roughness']); nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    return m

def review_scene(wall=False):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.5, 0.62, 0.78, 1.0); bg.inputs['Strength'].default_value = 0.8
    bpy.ops.mesh.primitive_plane_add(size=60); g = bpy.context.active_object; g.name = 'REV_ground'; g.data.materials.append(proj_mat('REV_cobble', 'cobble', 0.5))
    sd = bpy.data.lights.new('REV_sun', 'SUN'); sd.energy = 4.5; sd.color = (1.0, 0.95, 0.86); sd.angle = math.radians(2)
    so = bpy.data.objects.new('REV_sun', sd); sc.collection.objects.link(so); so.rotation_euler = (math.radians(48), 0, math.radians(-30))
    cam = bpy.data.cameras.new('REV_cam'); co = bpy.data.objects.new('REV_cam', cam); sc.collection.objects.link(co); sc.camera = co
    return co

def look(co, pos, target, lens):
    co.location = Vector(pos); co.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler(); co.data.lens = lens

def shoot(path, w, h):
    sc = bpy.context.scene; sc.render.resolution_x = w; sc.render.resolution_y = h; sc.render.filepath = path; bpy.ops.render.render(write_still=True)

def show(roots, vis):
    for rt in roots:
        rt.hide_render = rt not in vis
        for o in rt.children: o.hide_render = rt not in vis or o.name.split('@')[0] == 'FOOTPRINT'

PAIRS = [('Handcart', 'Handcart_Broken'), ('CrateStack', 'CrateStack_Broken'), ('SackStack', 'SackStack_Broken'), ('RainBarrel', 'RainBarrel_Broken'),
         ('FlowerBox', 'FlowerBox_Broken'), ('Well', 'Well_Ruined')]

def render_all(built):
    rd = os.path.join(OUT, 'renders'); os.makedirs(rd, exist_ok=True); co = review_scene()
    ground = bpy.data.objects['REV_ground']
    allroots = [rt for _, rt in built.values()]
    for A, B in PAIRS:
        if A not in built or B not in built: continue
        ra, rb = built[A][1], built[B][1]
        mnA, mxA = world_bounds(ra); mnB, mxB = world_bounds(rb)
        gap = 0.25
        ra.location = (-(mxA.x + gap / 2) + 0.0, 0, 0) if False else (-(abs(mnA.x) + mxA.x) / 2 - gap - 0.4 * 0, 0, 0)
        wA = mxA.x - mnA.x; wB = mxB.x - mnB.x
        ra.location = (-(wA / 2 + gap), 0, 0); rb.location = (wB / 2 + gap, 0, 0)
        show(allroots, (ra, rb))
        hi = max(mxA.z, mxB.z); span = wA + wB + 2 * gap; size = max(span, hi * 1.2, 1.6)
        c = Vector((0, 0, hi * 0.42)); d = size * 1.25 + 1.1
        look(co, (c.x + d * 0.38, c.y - d * 0.88, c.z + d * 0.42), c, 44)
        shoot(os.path.join(rd, A + '.png'), RRES, int(RRES * 0.75))
        ra.location = (0, 0, 0); rb.location = (0, 0, 0)

def render_signs(built):
    rd = os.path.join(OUT, 'renders'); os.makedirs(rd, exist_ok=True)
    names = [n for n in built if n.startswith('ShopSign_')]
    if not names: return
    co = bpy.context.scene.camera
    ground = bpy.data.objects['REV_ground']; ground.hide_render = True
    bpy.ops.mesh.primitive_plane_add(size=30); wl = bpy.context.active_object; wl.name = 'REV_wall'
    wl.rotation_euler = (math.radians(90), 0, 0); wl.location = (0, 0.0, 0); wl.data.materials.append(proj_mat('REV_plaster', 'plaster', 0.5))
    allroots = [rt for _, rt in built.values()]
    for n in names:
        rt = built[n][1]; show(allroots, (rt,)); rt.hide_render = False
        look(co, (-1.6, -2.0, 0.5), (0.0, -0.5, -0.35), 40)
        shoot(os.path.join(rd, n + '.png'), int(RRES * 0.7), int(RRES * 0.7))
    wl.hide_render = True; ground.hide_render = False

def render_line(built):
    if 'WashingLine' not in built: return
    rd = os.path.join(OUT, 'renders'); os.makedirs(rd, exist_ok=True); co = bpy.context.scene.camera
    rt = built['WashingLine'][1]; show([r_ for _, r_ in built.values()], ()); rt.hide_render = True
    for o in rt.children: o.hide_render = True
    P = {o.name.split('@')[0]: o for o in rt.children}
    zL = 1.95; X1 = 4.6
    def inst(nm, loc, rot=(0, 0, 0), sc=(1, 1, 1)):
        ob = bpy.data.objects.new('REV_' + nm, P[nm].data); bpy.context.scene.collection.objects.link(ob)
        ob.location = loc; ob.rotation_euler = [math.radians(q) for q in rot]; ob.scale = sc; return ob
    # two posts
    for x in (0.0, X1):
        bpy.ops.mesh.primitive_cube_add(size=1); po = bpy.context.active_object; po.scale = (0.2, 0.2, 2.25); po.location = (x, 0, 1.12); po.name = 'REV_post'
        pm = get_mat('CL_Oak'); po.data.materials.append(pm)
    def line_z(x): return zL - 0.12 * (1 - ((x - X1 / 2) / (X1 / 2)) ** 2) * 1.0 + 0.0
    def sag(x): return zL - 0.14 * (1 - ((x - X1 / 2) / (X1 / 2 - 0.13)) ** 2)
    def rope(pts):
        for p0, p1 in zip(pts, pts[1:]):
            d = Vector(p1) - Vector(p0); ang = -math.degrees(math.atan2(d.z, d.x)); yaw = math.degrees(math.atan2(d.y, d.x)) if abs(d.y) > 1e-6 else 0
            inst('Rope_1m', p0, (0, ang, 0), (d.length, 1, 1))
    def hooks(right_ok=True):
        inst('Hook', (0.1, 0, zL), (0, 0, 90)); inst('Hook', (X1 - 0.1, 0, zL), (0, 0, -90)) if right_ok else None
    outs = {}
    for variant in ('WashingLine', 'WashingLine_Torn'):
        objs = []
        before = set(bpy.data.objects)
        if variant == 'WashingLine':
            hooks(); xs = [0.23 + (X1 - 0.46) * k / 8 for k in range(9)]
            rope([(x, 0.0, sag(x)) for x in xs])
            for nm, x in (('Cloth_Shirt', 0.85), ('Cloth_Sheet', 2.05), ('Cloth_Apron', 3.0), ('Cloth_Towel', 3.5), ('Cloth_Socks', 4.0)):
                inst(nm, (x, 0, sag(x) - 0.004))
            for x in (0.7, 1.0, 1.55, 2.55, 2.8, 3.2, 3.38, 3.7, 3.84): inst('Peg', (x, 0, sag(x)))
        else:
            hooks(); xs = [0.23 + 1.9 * k / 5 for k in range(6)]
            rope([(x, 0.0, sag(x) + 0.0 - 0.0 * x) for x in xs] + [(2.5, 0.05, 1.55), (2.85, 0.35, 0.8), (3.2, 0.5, 0.03), (3.9, 0.7, 0.02), (4.4, 0.95, 0.025)])
            inst('Cloth_Rag_A', (1.0, 0, sag(1.0))); inst('Cloth_Rag_B', (1.85, 0, sag(1.85)))
            inst('Peg', (1.0, 0, sag(1.0))); inst('Peg', (1.12, 0, sag(1.12))); inst('Peg', (3.0, 0.45, 0.02), (80, 20, 40))
            inst('Cloth_Socks', (3.2, 0.45, 0.01), (90, 0, 20), (1, 1, 1))
            inst('Cloth_Rag_B', (3.7, 0.5, 0.04), (90, 0, -35))
        look(co, (X1 * 0.5 + 1.2, -4.9, 1.6), (X1 * 0.5 - 0.1, 0, 1.2), 36)
        shoot(os.path.join(rd, variant + '.png'), RRES, int(RRES * 0.7))
        for o in set(bpy.data.objects) - before:
            if o.name.startswith('REV_'): bpy.data.objects.remove(o)
        for o in [o for o in bpy.data.objects if o.name == 'REV_post']: bpy.data.objects.remove(o)

def write_sources(rows):
    os.makedirs(DEST, exist_ok=True)
    L = ['# Market and lane clutter (queue Q14)', '',
         'Generated by `Art/Blender/town/sc_clutter.py` (procedural, deterministic; the run line is in its header). Original geometry, no external source; licence: ours. Contract: `Reference/hollowmere/clutter/CONTRACT.md`.',
         'Metres, front -Y in Blender (+Z in Unity), pivot at the ground centre (signs: centre of the wall plate at the wall plane, arm along Unity +Z; WashingLine parts: see below).',
         'Each FBX: root `<Asset>`, mesh `Body` (WashingLine: one object per part) and a flat 0.1 m `FOOTPRINT` box (ground props only). No maps: materials are the contract `CL_*` slot names.', '',
         '| Asset | Tris | Size x y z (m) | Footprint X x Z (m) | Slots |', '|---|---|---|---|---|']
    for n, total, tris, a, size, mn, mx in rows:
        slots = sorted({s for p in a.objs for s in p.slots})
        L.append('| %s | %d | %.2f x %.2f x %.2f | %s | %s |' % (n, total, size.x, size.y, size.z, ('%.2f x %.2f' % a.foot) if a.foot else 'none', ', '.join(slots)))
    L += ['', '## Signs: pivot and lowest point', '', '| Sign | Pivot | Arm reach (m, along Unity +Z) | Lowest point below the pivot (m) |', '|---|---|---|---|']
    for n, total, tris, a, size, mn, mx in rows:
        if n.startswith('ShopSign_'): L.append('| %s | centre of the wall plate at the wall plane | %.2f | %.3f |' % (n, -mn.y, -mn.z))
    for n, total, tris, a, size, mn, mx in rows:
        if n == 'WashingLine':
            L += ['', '## WashingLine objects (no FOOTPRINT; every object sits at the FBX origin, geometry in its own pivot frame)', '', '| Object | Tris | Extent x (m) | Hang below the pivot (m) |', '|---|---|---|---|']
            for o in a.objs:
                m = [Vector(v) for v in [(p.co.x + o.origin.x, p.co.y + o.origin.y, p.co.z + o.origin.z) for p in a.mesh_of[o.name].vertices]]
                L.append('| %s | %d | %.2f .. %.2f | %.2f |' % (o.name, tris[o.name], min(v.x for v in m), max(v.x for v in m), -min(v.z for v in m)))
    with open(os.path.join(DEST, 'SOURCES.md'), 'w', encoding='utf-8', newline='\n') as fh: fh.write('\n'.join(L) + '\n')

def main():
    bpy.ops.wm.read_homefile(use_empty=True)
    names = [n for n in ASSETS if ONLY is None or n in ONLY]
    built = {}; rows = []
    for n in names:
        a, root, objs, tris = build(n)
        export(a, root)
        for o in root.children: o.name += '@' + n
        mn, mx = world_bounds(root); total = sum(tris.values())
        a.mesh_of = {o.name.split('@')[0]: o.data for o in objs}
        built[n] = (a, root); rows.append((n, total, tris, a, mx - mn, mn, mx))
        lim = BUDGET.get(n, 1500 if n.startswith('ShopSign_') else 3000)
        print('CLUTTER %-18s tris %5d (%s) size %.2f x %.2f x %.2f  min z %.3f slots %s  foot %s %s' % (n, total, 'ok' if total <= lim else 'OVER %d' % lim, *(mx - mn), mn.z,
              '/'.join(sorted({s for p in a.objs for s in p.slots})), (a.foot, 'x %.2f..%.2f y %.2f..%.2f' % (mn.x, mx.x, mn.y, mx.y)), '' if not n.startswith('ShopSign_') else 'reach %.2f' % (-mn.y)))
    if EXPORT: write_sources(rows)
    if not RENDER: return
    review_scene_ok = False
    render_all(built)
    render_signs(built)
    render_line(built)

main()
