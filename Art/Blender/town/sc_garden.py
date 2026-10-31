"""Garden and allotment decorations for Hollowmere (queue Q6): raised veg beds, trellis, scarecrow, water butt, picket fence run and gate, cold frame.

Tier T2 decoration family (spec 2026-10-30-hollowmere-mass-production-design 5.2-5.3, free-grid decorations with footprints), method Gen: every piece is
built procedurally here from chamfered boxes, lathes, tubes and small leaf lenses (no alpha cards, no bakes). Style bible PHASE1 3: chunky storybook
proportions, timber at about 1.4x the real section, chamfers of 1.5 cm where the section allows (thin pickets 0.5-0.8 cm), nothing dead straight (a low
frequency warp and a lean on every piece, each board and picket turned a degree or two, rails sag).

Conventions (as the B4 kit, sc_townhouse.py): metres, front -Y in Blender (FBX -Z forward / Y up, so the front is +Z in Unity), pivot at the ground centre,
root empty <Asset> with child meshes `Body` and `Part_*` (origin at their SOCKET), `SOCKET_<Part>` empties and a flat 0.1 m `FOOTPRINT` box.
Materials are SLOT NAMES, Unity maps them to the shared tileables (no maps are exported): oak_timber, canvas, iron, sandstone, packed_soil (B1 tileables,
UVs in metres by box projection, 1 UV unit = 2 m) and the flat slots Leaf (#6f8a34 family), Produce (cabbage green), Straw (#c9a85a), Glass. The corner
attribute `wear` (R grime, G moss, B polish, linear) is written as the B4 kit does; the flat slots ignore it in Unity.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_garden.py -- --out <scratch> [--only VegBed_A,Trellis] [--export]
      [--no-render] [--rres 900] [--samples 24]
  --export copies the FBX and SOURCES.md to Assets/_Project/Content/Art/Town/Garden/<Asset>/. Renders: <out>/renders/<Asset>_tq.png and group.png.

Pieces (tris and sizes are printed per run and written to SOURCES.md): VegBed_A 2.4 x 1.1, VegBed_B 1.6 x 1.0 (bean tents), Trellis 2.0 x 1.9,
Scarecrow, WaterButt 0.73 dia, Fence_Run 2.0 (Part_PostA, Part_PostB), Fence_Gate 1.1 (Part_Leaf, SOCKET_Hinge), ColdFrame 1.2 x 0.8 (propped light).
Deterministic: the seed is fixed per asset name.
"""
import sys, os, math, random, zlib, shutil
import bpy, bmesh
from mathutils import Vector, Matrix, Quaternion, Euler, noise

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
ONLY = arg('--only').split(',') if arg('--only') else None
EXPORT = '--export' in argv; RENDER = '--no-render' not in argv
RRES = int(arg('--rres', '900')); SAMPLES = int(arg('--samples', '24'))
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
TEX = os.path.join(ROOT, 'Art', 'Textures', 'B1_town')
DEST = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Art', 'Town', 'Garden')
SEED = 3106
BEV = 0.016          # timber chamfer (style bible 3: 1.5 cm and up where the section allows)

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode()))

# ---------------------------------------------------------------- geometry kit
def T(c=(0, 0, 0), rot=None, sc=(1, 1, 1)):
    """Translation @ rotation (Euler degrees tuple, or a Quaternion) @ scale."""
    if rot is None: R = Matrix.Identity(4)
    elif isinstance(rot, Quaternion): R = rot.to_matrix().to_4x4()
    else: R = Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4()
    return Matrix.Translation(Vector(c)) @ R @ Matrix.Diagonal(Vector(list(sc) + [1.0]))

def aim(d, up='Y'):
    """Rotation that turns +Z onto direction d."""
    return Vector(d).normalized().to_track_quat('Z', up)

def pivot(p, rot):
    return Matrix.Translation(Vector(p)) @ Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4() @ Matrix.Translation(-Vector(p))

class Piece:
    """One mesh object of an asset (Body or a Part_*): a bmesh plus its material slot names."""
    def __init__(self, name, origin=(0, 0, 0)):
        self.name = name; self.bm = bmesh.new(); self.slots = []; self.G = Matrix.Identity(4); self.origin = Vector(origin)

    def absorb(self, t, slot):
        if slot not in self.slots: self.slots.append(slot)
        mi = self.slots.index(slot)
        bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
        t.transform(self.G)
        if self.G.determinant() < 0: bmesh.ops.reverse_faces(t, faces=t.faces[:])
        vm = {v: self.bm.verts.new(v.co) for v in t.verts}
        for f in t.faces:
            nf = self.bm.faces.new([vm[v] for v in f.verts]); nf.material_index = mi
        t.free()

    def box(self, slot, c, s, rot=None, bev=BEV, taper=None):
        t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts:
            k = taper if (taper is not None and v.co.z > 0) else 1.0
            v.co = Vector((v.co.x * s[0] * k, v.co.y * s[1] * k, v.co.z * s[2]))
        b = min(bev, 0.34 * min(s))
        if b > 0.0025: bmesh.ops.bevel(t, geom=list(t.edges), offset=b, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        t.transform(T(c, rot)); self.absorb(t, slot)

    def beam(self, slot, a, b, w, h, bev=BEV * 0.6):
        a = Vector(a); b = Vector(b); d = b - a
        t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts: v.co = Vector((v.co.x * d.length, v.co.y * w, v.co.z * h))
        bb = min(bev, 0.34 * min(w, h))
        if bb > 0.0025: bmesh.ops.bevel(t, geom=list(t.edges), offset=bb, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        t.transform(Matrix.Translation((a + b) / 2) @ d.to_track_quat('X', 'Z').to_matrix().to_4x4()); self.absorb(t, slot)

    def cyl(self, slot, c, r, h, rot=None, segs=8, r2=None, sc=(1, 1, 1)):
        t = bmesh.new()
        bmesh.ops.create_cone(t, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=max(0.002, r if r2 is None else r2), depth=h)
        t.transform(T(c, rot, sc)); self.absorb(t, slot)

    def blade(self, slot, base, d, length, r0, r1=0.003, segs=4):
        """A tapered cone standing on `base`, leaning along direction d."""
        d = Vector(d).normalized()
        self.cyl(slot, Vector(base) + d * length / 2, r0, length, rot=aim(d), segs=segs, r2=r1)

    def blob(self, slot, c, r, rot=None, u=8, v=5):
        t = bmesh.new(); bmesh.ops.create_uvsphere(t, u_segments=u, v_segments=v, radius=1.0)
        r = (r, r, r) if isinstance(r, (int, float)) else r
        t.transform(T(c, rot, r)); self.absorb(t, slot)

    def leaf(self, slot, base, d, length, width, thick=0.008):
        """A small lens leaf (closed geometry, no alpha card) growing from base toward d."""
        d = Vector(d).normalized(); t = bmesh.new()
        bmesh.ops.create_uvsphere(t, u_segments=5, v_segments=3, radius=1.0)
        t.transform(T(Vector(base) + d * length / 2, d.to_track_quat('Y', 'Z'), (width / 2, length / 2, thick / 2)))
        self.absorb(t, slot)

    def prism(self, slot, pts, thick, c=(0, 0, 0), plane='xz', rot=None, bev=0.0):
        """Extrude a 2D profile: plane 'xz' (thickness along y) or 'yz' (thickness along x), centred on thickness."""
        t = bmesh.new()
        mk = (lambda a, b: (a, -thick / 2, b)) if plane == 'xz' else (lambda a, b: (-thick / 2, a, b))
        f = t.faces.new([t.verts.new(mk(a, b)) for a, b in pts])
        r = bmesh.ops.extrude_face_region(t, geom=[f])
        off = Vector((0, thick, 0)) if plane == 'xz' else Vector((thick, 0, 0))
        bmesh.ops.translate(t, vec=off, verts=[e for e in r['geom'] if isinstance(e, bmesh.types.BMVert)])
        if bev > 0.002: bmesh.ops.bevel(t, geom=list(t.edges), offset=bev, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        t.transform(T(c, rot)); self.absorb(t, slot)

    def lathe(self, slot, prof, c=(0, 0, 0), segs=12, closed=False, rot=None, vbev=0.0, sc=(1, 1, 1)):
        """Revolve [(radius, z), ...] about z. closed: the profile is a loop (a hoop); otherwise both ends are capped."""
        t = bmesh.new(); rings = []
        for r, z in prof:
            rings.append([t.verts.new((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), z)) for k in range(segs)])
        n = len(rings); ved = []
        for i in range(n if closed else n - 1):
            a, b = rings[i], rings[(i + 1) % n]
            for k in range(segs):
                t.faces.new([a[k], a[(k + 1) % segs], b[(k + 1) % segs], b[k]])
                ved.append((a[k], b[k]))
        if not closed:
            t.faces.new(rings[0][::-1]); t.faces.new(rings[-1])
        if vbev > 0.001:
            es = [e for e in (t.edges.get(p) for p in ved) if e is not None]
            bmesh.ops.bevel(t, geom=es, offset=vbev, offset_type='OFFSET', segments=1, profile=0.5, affect='EDGES')
        t.transform(T(c, rot, sc)); self.absorb(t, slot)

    def tube(self, slot, pts, r0, r1=None, segs=5):
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
        self.absorb(t, slot)

class Asset:
    def __init__(self, name, foot):
        self.name = name; self.r = rng(name); self.body = Piece('Body'); self.parts = []; self.sockets = {}; self.foot = foot
        self.lean = (0.0, 0.0); self.amp = 0.006; self.off = Vector((self.r.uniform(0, 50), self.r.uniform(0, 50), self.r.uniform(0, 50)))
    def part(self, name, origin):
        p = Piece('Part_' + name, origin); self.parts.append(p); return p
    def pieces(self):
        return [self.body] + self.parts

def jit(r, a):
    return r.uniform(-a, a)

# ---------------------------------------------------------------- the assets
def vegbed(a, L, W, variant):
    r = a.r; b = a.body; E = 'oak_timber'
    hx, hy = L / 2, W / 2
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box(E, (sx * (hx - 0.045), sy * (hy - 0.045), 0.215), (0.09, 0.09, 0.43), rot=(0, 0, jit(r, 4)))
    for sy in (-1, 1):
        for k in range(2):
            z = 0.085 + k * 0.17; lo, hi = -hx + 0.07, hx - 0.07
            cuts = [lo] + ([r.uniform(-0.5, 0.5) * hx] if L > 2.0 else []) + [hi]
            for i in range(len(cuts) - 1):
                x0, x1 = cuts[i] + (0.004 if i else 0), cuts[i + 1] - (0.004 if i < len(cuts) - 2 else 0)
                b.box(E, ((x0 + x1) / 2, sy * (hy - 0.03) + jit(r, 0.004), z + jit(r, 0.004)), (x1 - x0, 0.05, 0.165), rot=(jit(r, 1.2), 0, jit(r, 0.8)))
    for sx in (-1, 1):
        for k in range(2):
            b.box(E, (sx * (hx - 0.03), jit(r, 0.004), 0.085 + k * 0.17 + jit(r, 0.004)), (0.05, W - 0.17, 0.165), rot=(0, jit(r, 1.2), jit(r, 0.8)))
    # soil to 4.5 cm under the plank tops, with raised rows
    b.box('packed_soil', (0, 0, 0.16), (L - 0.12, W - 0.12, 0.28), bev=0.015)
    rows = [-0.34, 0.0, 0.34] if W > 1.05 else [-0.26, 0.26]
    for y in rows:
        b.prism('packed_soil', [(-0.12, 0), (0.12, 0), (0.075, 0.055), (-0.075, 0.055)], L - 0.2, c=(0, y, 0.299), plane='yz', bev=0.01)

    def cabbage(x, y, rad, tilt=0):
        z = 0.35; b.blob('Produce', (x, y, z + rad * 0.52), (rad, rad, rad * 0.78), rot=(0, 0, r.uniform(0, 360)), u=9, v=5)
        n = 5
        for i in range(n):
            ang = i * 360 / n + r.uniform(-15, 15); ar = math.radians(ang)
            d = Vector((math.cos(ar) * 0.9, math.sin(ar) * 0.9, 0.38 + r.uniform(0, 0.2)))
            b.leaf('Produce', (x + math.cos(ar) * rad * 0.35, y + math.sin(ar) * rad * 0.35, z + rad * 0.12), d, rad * 1.25, rad * 0.95, thick=0.012)

    def leek(x, y, n=5, h=0.42):
        z = 0.35
        for i in range(n):
            ang = math.radians(i * 360 / n + r.uniform(-20, 20)); lean = r.uniform(0.12, 0.34)
            d = Vector((math.cos(ang) * lean, math.sin(ang) * lean, 1.0))
            b.blade('Leaf', (x + math.cos(ang) * 0.012, y + math.sin(ang) * 0.012, z), d, h * r.uniform(0.75, 1.1), 0.014, 0.004, segs=4)

    def onion(x, y):
        z = 0.35; b.blob('Produce', (x, y, z + 0.02), (0.05, 0.05, 0.043), u=6, v=4)
        for i in range(3):
            ang = math.radians(i * 120 + r.uniform(-30, 30))
            b.blade('Leaf', (x, y, z + 0.05), (math.cos(ang) * 0.22, math.sin(ang) * 0.22, 1.0), r.uniform(0.17, 0.26), 0.009, 0.003, segs=3)

    if variant == 'A':
        for x in (-0.82, -0.27, 0.27, 0.82):
            cabbage(x + jit(r, 0.04), rows[0] + jit(r, 0.03), r.uniform(0.15, 0.18))
        for i in range(6): leek(-0.72 + i * 0.29 + jit(r, 0.03), rows[1] + jit(r, 0.025))
        for i in range(5): onion(-0.84 + i * 0.42 + jit(r, 0.04), rows[2] + jit(r, 0.03))
    else:
        for x in (-0.5, 0.0, 0.5):
            cabbage(x + jit(r, 0.03), rows[0] + jit(r, 0.03), r.uniform(0.13, 0.16))
        for i in range(3): onion(-0.4 + i * 0.4 + jit(r, 0.04), rows[1] - 0.05 + jit(r, 0.03))
        # two bean tents (A-frames across the bed) joined by a ridge pole
        top = 1.55; poles = []
        for x in (-0.38, 0.38):
            for s in (-1, 1):
                base = Vector((x + jit(r, 0.02), s * 0.3 + jit(r, 0.02) + rows[1] * 0, 0.31))
                tip = Vector((x + jit(r, 0.02), s * 0.03, top))
                b.beam(E, base - Vector((0, 0, 0.02)), tip, 0.05, 0.05, bev=0.012); poles.append((base, tip))
        b.beam(E, (-0.5, 0.0, top - 0.04), (0.5, 0.01, top - 0.03), 0.045, 0.045, bev=0.012)
        for base, tip in poles:   # a vine spiralling up each pole, with leaf pairs
            pts = []
            for i in range(14):
                t = i / 13.0; p = base.lerp(tip, 0.9 * t); ang = t * 5.5 * math.pi + r.uniform(0, 0.3)
                p = p + Vector((math.cos(ang) * 0.03, 0.0, 0.0)) + Vector((0, math.sin(ang) * 0.03, 0))
                pts.append(p)
            b.tube('Leaf', pts, 0.008, 0.005, segs=4)
            for i in (3, 6, 9, 12):
                p = pts[i]; ang = r.uniform(0, 2 * math.pi)
                for k in range(2):
                    aa = ang + k * math.pi
                    b.leaf('Leaf', p, (math.cos(aa), math.sin(aa) * 0.5, 0.45), 0.13, 0.095)
    a.foot = (L + 0.1, W + 0.1)
    a.lean = (0.006, 0.0)

def trellis(a):
    r = a.r; b = a.body; E = 'oak_timber'
    for sx in (-1, 1):
        b.box(E, (sx * 0.945, 0, 0.9), (0.11, 0.11, 1.8), rot=(0, 0, jit(r, 3)))
        b.box(E, (sx * 0.945, 0, 1.87), (0.15, 0.15, 0.07), rot=(0, 0, jit(r, 5)), taper=0.88)
    b.box(E, (0, 0.0, 1.62), (1.78, 0.05, 0.075), rot=(0, jit(r, 0.6), 0.5))
    b.box(E, (0, 0.0, 0.22), (1.78, 0.05, 0.075), rot=(0, jit(r, 0.6), -0.4))
    nv = 9
    for i in range(nv):
        x = -0.85 + i * 1.7 / (nv - 1)
        b.box(E, (x + jit(r, 0.008), -0.025, 0.92), (0.045, 0.03, 1.4), rot=(0, 0, jit(r, 1.5)), bev=0.01)
    for j in range(5):
        z = 0.42 + j * 0.26
        b.box(E, (0, 0.025, z + jit(r, 0.01)), (1.78, 0.03, 0.045), rot=(0, jit(r, 0.5), jit(r, 1.0)), bev=0.01)
    for k, x0 in enumerate((-0.55, 0.05, 0.62)):   # three runners weaving front/back through the lattice
        pts = []; ph = r.uniform(0, 6)
        for i in range(22):
            t = i / 21.0
            pts.append((x0 + 0.17 * math.sin(t * 9 + ph) + (0.1 if k == 1 else 0) * t, 0.052 * math.sin(t * 14 + ph), 0.2 + t * (1.35 + 0.1 * k)))
        b.tube('Leaf', pts, 0.009, 0.005, segs=4)
        for i in (3, 7, 11, 15, 19):
            p = Vector(pts[i]); ang = r.uniform(0, 2 * math.pi)
            for q in range(3):
                aa = ang + q * 2.1
                b.leaf('Leaf', p, (math.cos(aa), -0.45 + (0.9 if q == 1 else 0) * 0.2, 0.3 + math.sin(aa) * 0.6), 0.14, 0.1)
    a.foot = (2.1, 0.3); a.lean = (0.004, 0.0)

def scarecrow(a):
    r = a.r; b = a.body; E = 'oak_timber'
    b.cyl(E, (0, 0, 1.05), 0.045, 2.1, segs=6, r2=0.04)
    b.beam(E, (-0.8, 0.0, 1.5), (0.8, 0.0, 1.43), 0.055, 0.055, bev=0.014)
    # canvas coat: lathe body, sleeves, ragged hem tabs, a patch
    b.lathe('canvas', [(0.23, 0.92), (0.29, 1.03), (0.27, 1.25), (0.19, 1.46), (0.1, 1.57)], c=(0, 0, 0), segs=9)
    for i in range(9):
        ang = i * 2 * math.pi / 9 + 0.15; ln = r.uniform(0.07, 0.19)
        b.cyl('canvas', (math.cos(ang) * 0.245, math.sin(ang) * 0.245, 0.92 - ln / 2 + 0.01), 0.06, ln, rot=(0, 0, math.degrees(ang) + 90 + jit(r, 8)),
              segs=4, r2=0.006, sc=(1.0, 0.16, 1.0))
    b.box('canvas', (0.07, -0.275, 1.23), (0.15, 0.02, 0.14), rot=(0, 0, 4), bev=0.006)
    for sx in (-1, 1):
        sh = Vector((sx * 0.12, 0, 1.46)); cuff = Vector((sx * 0.64, 0.0, 1.39 if sx < 0 else 1.40))
        d = cuff - sh
        b.cyl('canvas', (sh + cuff) / 2, 0.105, d.length, rot=aim(d), segs=8, r2=0.085)
        for i in range(4):   # ragged cuff
            ang = i * math.pi / 2 + 0.4; ln = r.uniform(0.04, 0.1)
            v = Vector((0, math.cos(ang), math.sin(ang))) * 0.07
            b.cyl('canvas', cuff + v + Vector((sx * ln / 2, 0, 0)), 0.03, ln, rot=(0, 90 * sx, 0), segs=3, r2=0.004, sc=(1.0, 1.0, 1.0))
        for i in range(10):   # straw tufts at the cuff
            ang = r.uniform(0, 2 * math.pi); sp = r.uniform(0.15, 0.55)
            dd = Vector((sx * 1.0, math.cos(ang) * sp, math.sin(ang) * sp - 0.15))
            b.blade('Straw', cuff + Vector((0, math.cos(ang) * 0.03, math.sin(ang) * 0.03)), dd, r.uniform(0.14, 0.26), 0.009, 0.003, segs=3)
    for i in range(6):   # straw at the collar and the hem
        ang = math.radians(r.uniform(190, 350))
        b.blade('Straw', (math.cos(ang) * 0.1, math.sin(ang) * 0.1, 1.55), (math.cos(ang) * 0.5, math.sin(ang) * 0.5, 0.9), r.uniform(0.1, 0.17), 0.008, 0.003, segs=3)
    for i in range(5):
        ang = r.uniform(0, 2 * math.pi)
        b.blade('Straw', (math.cos(ang) * 0.16, math.sin(ang) * 0.16, 1.0), (math.cos(ang) * 0.5, math.sin(ang) * 0.5, -0.9), r.uniform(0.12, 0.2), 0.008, 0.003, segs=3)
    # sack head with a stitched face
    hc = Vector((0.01, 0, 1.79)); hr = Vector((0.215, 0.205, 0.23))
    b.blob('canvas', hc, hr, rot=(0, 0, 12), u=10, v=7)
    b.cyl(E, (0, 0, 1.6), 0.085, 0.05, segs=8, r2=0.075)   # rope at the neck
    b.cyl('canvas', (0.03, 0.0, 1.985), 0.04, 0.05, rot=(0, 18, 0), segs=5, r2=0.012)   # the sack's knotted top
    def on_head(x, z):
        k = 1 - (x / hr.x) ** 2 - ((z - hc.z) / hr.z) ** 2
        return Vector((hc.x + x, hc.y - hr.y * math.sqrt(max(0.05, k)), z))
    for sx in (-1, 1):   # X eyes
        e = on_head(sx * 0.07, hc.z + 0.04)
        for rz in (45, -45):
            b.box(E, e + Vector((0, 0.002, 0)), (0.07, 0.012, 0.014), rot=(0, rz, 0), bev=0.003)
    n0 = on_head(0.0, hc.z - 0.03); b.cyl('canvas', n0 + Vector((0, -0.02, 0)), 0.035, 0.07, rot=(90, 0, 0), segs=6, r2=0.02)
    for i in range(7):   # stitched mouth
        x = -0.09 + i * 0.03; m = on_head(x, hc.z - 0.1 + (0.012 if i % 2 else 0))
        b.box(E, m, (0.012, 0.012, 0.045), bev=0.002)
    # straw hat, tipped back
    b.G = pivot(hc + Vector((0, 0, 0.12)), (-9, 7, 8))
    b.lathe('Straw', [(0.12, 1.945), (0.26, 1.965), (0.43, 1.915), (0.43, 1.895), (0.26, 1.935), (0.12, 1.915)], segs=14, closed=True)
    b.lathe('Straw', [(0.155, 1.93), (0.15, 2.02), (0.125, 2.12), (0.05, 2.145)], segs=12)
    b.lathe('canvas', [(0.16, 1.97), (0.162, 2.005), (0.152, 2.005), (0.15, 1.97)], segs=12, closed=True)
    for i in range(6):
        ang = r.uniform(0, 2 * math.pi)
        b.blade('Straw', (math.cos(ang) * 0.42, math.sin(ang) * 0.42, 1.91), (math.cos(ang), math.sin(ang), -0.4), r.uniform(0.08, 0.15), 0.008, 0.003, segs=3)
    b.G = Matrix.Identity(4)
    a.foot = (0.55, 0.55); a.lean = (0.05, -0.012); a.amp = 0.004

def water_butt(a):
    r = a.r; b = a.body; E = 'oak_timber'
    z0, H = 0.115, 0.84
    def rad(t): return 0.30 + 0.065 * (1 - (2 * t - 1) ** 2)
    for sx in (-1, 1):
        b.box('sandstone', (sx * 0.22, jit(r, 0.01), 0.057), (0.2, 0.62, 0.115), rot=(jit(r, 1.5), 0, jit(r, 3)))
    ts = [0, 0.1, 0.25, 0.5, 0.75, 0.9, 1.0]
    b.lathe(E, [(rad(t), z0 + t * H) for t in ts], segs=16, vbev=0.007)
    for t in (0.07, 0.29, 0.71, 0.93):
        z = z0 + t * H; ro = rad(t) + 0.016; ri = rad(t) - 0.01
        b.lathe('iron', [(ri, z - 0.026), (ro - 0.005, z - 0.026), (ro, z - 0.02), (ro, z + 0.02), (ro - 0.005, z + 0.026), (ri, z + 0.026)], segs=16, closed=True)
    top = z0 + H
    b.lathe(E, [(0.315, top), (0.315, top + 0.045), (0.28, top + 0.05), (0.0001 + 0.05, top + 0.05)], c=(0.03, 0.0, 0.0), segs=16, rot=(0, 0, 8 + jit(r, 3)), vbev=0.006)
    for y in (-0.12, 0.13):
        b.box(E, (0.03, y, top + 0.07), (0.54, 0.075, 0.035), rot=(0, 0, jit(r, 2)))
    b.box('iron', (0.03, 0, top + 0.1), (0.15, 0.035, 0.045))
    # tap: plate, spigot out the front, spout and lever (iron)
    tz = z0 + 0.2 * H; fy = -rad(0.2)
    b.box('iron', (0, fy + 0.005, tz), (0.1, 0.03, 0.1), bev=0.01)
    b.cyl('iron', (0, fy - 0.07, tz), 0.025, 0.16, rot=(90, 0, 0), segs=8)
    b.cyl('iron', (0, fy - 0.135, tz - 0.04), 0.022, 0.1, segs=8, r2=0.017)
    b.box('iron', (0, fy - 0.05, tz + 0.045), (0.1, 0.022, 0.022), rot=(0, 0, 20), bev=0.006)
    a.sockets['Tap'] = Vector((0, fy, tz))
    a.foot = (0.82, 0.82); a.lean = (0.0, 0.0); a.amp = 0.004

def picket_pts(w, h, tip):
    return [(-w / 2, 0), (w / 2, 0), (w / 2, h), (0, h + tip), (-w / 2, h)]

def fence_post(p, x, h, r, slot='oak_timber'):
    p.box(slot, (x, 0, h / 2), (0.11, 0.11, h), rot=(jit(r, 1.5), jit(r, 1.5), jit(r, 4)))
    p.box(slot, (x, 0, h + 0.015), (0.145, 0.145, 0.045), rot=(0, 0, jit(r, 6)), taper=0.85)

def fence_run(a):
    r = a.r; b = a.body; E = 'oak_timber'
    pa = a.part('PostA', (-0.95, 0, 0)); fence_post(pa, -0.95, 1.02, r)
    pb = a.part('PostB', (0.95, 0, 0)); fence_post(pb, 0.95, 1.0, r)
    a.sockets['PostA'] = Vector((-0.95, 0, 0)); a.sockets['PostB'] = Vector((0.95, 0, 0))
    def sagz(x): return -0.035 * (1 - (x / 0.95) ** 2)
    for z in (0.3, 0.7):
        b.beam(E, (-0.95, 0.045, z + jit(r, 0.01)), (0.0, 0.045, z + sagz(0) + jit(r, 0.01)), 0.04, 0.075)
        b.beam(E, (-0.01, 0.047, z + sagz(0)), (0.95, 0.045, z + jit(r, 0.01)), 0.04, 0.075)
    n = 12; skip = r.randrange(3, 10)
    for i in range(n):
        if i == skip: continue
        x = -0.84 + i * 1.68 / (n - 1)
        h = r.uniform(0.80, 0.94) if i != skip - 1 else 0.7
        z0 = 0.06 + sagz(x) * 0.8
        b.prism(E, picket_pts(0.088, h, 0.05), 0.03, c=(x, 0.0, z0), rot=(jit(r, 2.5), 0, jit(r, 2.8)), bev=0.006)
    a.foot = (2.0, 0.16); a.amp = 0.005; a.lean = (0.0, 0.004)

def fence_gate(a):
    r = a.r; b = a.body; E = 'oak_timber'
    for sx in (-1, 1):
        fence_post(b, sx * 0.555, 1.2 if sx < 0 else 1.14, r)
    # catch on the latch post
    b.box('iron', (0.49, -0.055, 0.78), (0.03, 0.03, 0.07), bev=0.006)
    a.sockets['Hinge'] = Vector((-0.47, 0.0, 0.0)); a.sockets['Gate'] = Vector((0, 0, 0))
    lf = a.part('Leaf', (-0.47, 0.0, 0.0))
    lf.G = pivot((-0.47, 0, 0.1), (0, 1.6, 0))
    for sx in (-1, 1):
        lf.box(E, (sx * 0.44, 0, 0.58), (0.07, 0.04, 0.96), rot=(0, 0, jit(r, 1.0)), bev=0.01)
    lf.box(E, (0, 0.0, 0.2), (0.88, 0.04, 0.075), rot=(0, 0, jit(r, 0.8)), bev=0.012)
    lf.box(E, (0, 0.0, 0.9), (0.88, 0.04, 0.075), rot=(0, 0, jit(r, 0.8)), bev=0.012)
    for i in range(6):
        x = -0.34 + i * 0.136
        lf.prism(E, picket_pts(0.092, 0.82, 0.05), 0.028, c=(x, -0.034, 0.17), rot=(0, 0, jit(r, 1.8)), bev=0.006)
    lf.beam(E, (-0.43, 0.034, 0.23), (0.42, 0.034, 0.88), 0.035, 0.07)
    for z in (0.26, 0.84):
        lf.box('iron', (-0.30, -0.032, z), (0.4, 0.012, 0.045), rot=(0, 0, jit(r, 2)), bev=0.005)
    lf.box('iron', (0.37, -0.034, 0.56), (0.06, 0.014, 0.1), bev=0.005)
    a.foot = (1.2, 0.16); a.amp = 0.004; a.lean = (0.0, 0.0)

def cold_frame(a):
    r = a.r; b = a.body; E = 'oak_timber'
    b.box(E, (0, 0.375, 0.21), (1.2, 0.05, 0.42), rot=(0, 0, jit(r, 0.6)))
    b.box(E, (0, -0.375, 0.13), (1.2, 0.05, 0.26), rot=(0, 0, jit(r, 0.6)))
    for sx in (-1, 1):
        b.prism(E, [(-0.4, 0), (0.4, 0), (0.4, 0.42), (-0.4, 0.26)], 0.05, c=(sx * 0.575, 0, 0), plane='yz', bev=0.012)
    b.box('packed_soil', (0, 0, 0.1), (1.1, 0.7, 0.2), bev=0.012)
    for j in range(3):
        for i in range(5):
            x = -0.44 + i * 0.22 + jit(r, 0.03); y = -0.22 + j * 0.2 + jit(r, 0.03); z = 0.2
            b.blade('Leaf', (x, y, z), (jit(r, 0.1), jit(r, 0.1), 1.0), r.uniform(0.04, 0.07), 0.006, 0.003, segs=3)
            for k in range(2):
                ang = r.uniform(0, math.pi) + k * math.pi
                b.leaf('Leaf', (x, y, z + 0.05), (math.cos(ang), math.sin(ang), 0.35), 0.07, 0.04, thick=0.006)
    # the propped light: frame bars and two glass panes, hinged on the back board
    b.G = Matrix.Translation((0, 0.40, 0.45)) @ Euler((math.radians(-30), 0, 0)).to_matrix().to_4x4()
    b.box(E, (0, -0.025, 0), (1.24, 0.05, 0.04), bev=0.012)
    b.box(E, (0, -0.795, 0), (1.24, 0.05, 0.04), bev=0.012)
    for sx in (-1, 1):
        b.box(E, (sx * 0.595, -0.41, 0), (0.05, 0.82, 0.04), bev=0.012)
    b.box(E, (0, -0.41, 0), (0.035, 0.74, 0.03), bev=0.008)
    for sx in (-1, 1):
        b.box('Glass', (sx * 0.29, -0.41, 0), (0.54, 0.74, 0.008), bev=0.0)
    lid_front = b.G @ Vector((0.3, -0.76, -0.02))
    b.G = Matrix.Identity(4)
    b.beam(E, (0.3, -0.31, 0.2), lid_front, 0.032, 0.032, bev=0.008)
    a.foot = (1.25, 0.85); a.amp = 0.004; a.lean = (0.0, 0.0)

ASSETS = {
    'VegBed_A': lambda: (lambda a: (vegbed(a, 2.4, 1.1, 'A'), a)[1])(Asset('VegBed_A', (2.5, 1.2))),
    'VegBed_B': lambda: (lambda a: (vegbed(a, 1.6, 1.0, 'B'), a)[1])(Asset('VegBed_B', (1.7, 1.1))),
    'Trellis': lambda: (lambda a: (trellis(a), a)[1])(Asset('Trellis', (2.1, 0.3))),
    'Scarecrow': lambda: (lambda a: (scarecrow(a), a)[1])(Asset('Scarecrow', (0.55, 0.55))),
    'WaterButt': lambda: (lambda a: (water_butt(a), a)[1])(Asset('WaterButt', (0.82, 0.82))),
    'Fence_Run': lambda: (lambda a: (fence_run(a), a)[1])(Asset('Fence_Run', (2.0, 0.16))),
    'Fence_Gate': lambda: (lambda a: (fence_gate(a), a)[1])(Asset('Fence_Gate', (1.2, 0.16))),
    'ColdFrame': lambda: (lambda a: (cold_frame(a), a)[1])(Asset('ColdFrame', (1.25, 0.85))),
}

# ---------------------------------------------------------------- finishing: warp, UVs (metres / 2), wear, objects
def hex2lin(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]

MATS = {}
FLAT = {'Leaf': ('#6f8a34', 0.7), 'Produce': ('#7f9c5c', 0.55), 'Straw': ('#c9a85a', 0.8)}
def get_mat(name):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    bs = nt.nodes['Principled BSDF']
    if name in FLAT:
        bs.inputs['Base Color'].default_value = (*hex2lin(FLAT[name][0]), 1); bs.inputs['Roughness'].default_value = FLAT[name][1]
    elif name == 'Glass':
        bs.inputs['Base Color'].default_value = (0.05, 0.075, 0.095, 1); bs.inputs['Roughness'].default_value = 0.05
        bs.inputs['Alpha'].default_value = 0.35; m.blend_method = 'BLEND' if hasattr(m, 'blend_method') else None
    elif name == 'FOOTPRINT':
        bs.inputs['Base Color'].default_value = (0.8, 0.2, 0.2, 1)
    else:
        d = os.path.join(TEX, name)
        def img(suffix, cs):
            n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(os.path.join(d, '%s_%s.png' % (name, suffix)), check_existing=True)
            n.image.colorspace_settings.name = cs; return n
        c = img('albedo', 'sRGB'); o = img('orm', 'Non-Color'); nrm = img('normal', 'Non-Color')
        sep = nt.nodes.new('ShaderNodeSeparateColor'); nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(o.outputs['Color'], sep.inputs[0]); nt.links.new(nrm.outputs['Color'], nm.inputs['Color'])
        nt.links.new(c.outputs['Color'], bs.inputs['Base Color']); nt.links.new(sep.outputs[1], bs.inputs['Roughness'])
        nt.links.new(sep.outputs[2], bs.inputs['Metallic']); nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    MATS[name] = m
    return m

def finish(a, piece):
    bm = piece.bm; r = a.r
    for v in bm.verts:
        co = v.co; f = min(1.0, max(0.0, co.z / 0.4))
        n = Vector((noise.noise(co * 0.9 + a.off), noise.noise(co * 0.9 + a.off + Vector((17.3, 0, 0))), noise.noise(co * 0.9 + a.off + Vector((0, 31.7, 0)))))
        co += n * a.amp * f
        co.x += a.lean[0] * max(0.0, co.z); co.y += a.lean[1] * max(0.0, co.z)
    bm.normal_update()
    uv = bm.loops.layers.uv.new('UVMap')
    for f in bm.faces:
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            l[uv].uv = ((p.y, p.z) if ax == 0 else (p.x, p.z) if ax == 1 else (p.x, p.y))
            l[uv].uv = (l[uv].uv[0] / 2.0, l[uv].uv[1] / 2.0)
    me = bpy.data.meshes.new(a.name + '_' + piece.name)
    # world-space wear needs the final positions: write after to_mesh
    bm.to_mesh(me)
    for s in piece.slots: me.materials.append(get_mat(s))
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    off = a.off
    for poly in me.polygons:
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 3.1 + off)
            grime = max(0.0, 1.0 - p.z / 0.6) * 0.75 * (0.65 + 0.35 * nz) + 0.15 * max(0.0, noise.noise(p * 1.3 + off))
            moss = (max(0.0, 1.0 - p.z / 0.3) * max(0.0, nz) * 0.6) if poly.normal.z < 0.7 else 0.0
            polish = 0.5 if (poly.normal.z > 0.85 and p.z > 0.25) else 0.0
            attr.data[li].color = (min(1.0, grime), min(1.0, moss), polish, 1.0)
    bm.free()
    # object origin: parts sit at their socket
    me.transform(Matrix.Translation(-piece.origin))
    ob = bpy.data.objects.new(piece.name, me); bpy.context.scene.collection.objects.link(ob)
    ob.location = piece.origin
    return ob

def build(name):
    a = ASSETS[name]()
    root = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(root); root.empty_display_type = 'PLAIN_AXES'
    objs = []
    for p in a.pieces():
        o = finish(a, p); o.parent = root; objs.append(o)
        o.matrix_parent_inverse = Matrix.Identity(4)
    for s, loc in a.sockets.items():
        e = bpy.data.objects.new('SOCKET_' + s, None); bpy.context.scene.collection.objects.link(e); e.parent = root; e.location = loc; e.empty_display_size = 0.1
    fm = bpy.data.meshes.new('FOOTPRINT'); fb = bmesh.new(); bmesh.ops.create_cube(fb, size=1.0)
    for v in fb.verts: v.co = Vector((v.co.x * a.foot[0], v.co.y * a.foot[1], v.co.z * 0.1 + 0.05))
    fb.to_mesh(fm); fb.free(); fm.materials.append(get_mat('FOOTPRINT'))
    fo = bpy.data.objects.new('FOOTPRINT', fm); bpy.context.scene.collection.objects.link(fo); fo.parent = root; fo.hide_render = True
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs}
    return a, root, objs, tris

def export(a, root, tris):
    d = os.path.join(OUT, a.name); os.makedirs(d, exist_ok=True); fbx = os.path.join(d, a.name + '.fbx')
    bpy.ops.object.select_all(action='DESELECT'); root.select_set(True)
    for o in root.children: o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'EMPTY', 'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False, path_mode='STRIP')
    if EXPORT:
        dd = os.path.join(DEST, a.name); os.makedirs(dd, exist_ok=True); shutil.copy(fbx, os.path.join(dd, a.name + '.fbx'))

# ---------------------------------------------------------------- review renders
def review_scene():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    sc.render.resolution_x = RRES; sc.render.resolution_y = int(RRES * 0.8)
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.5, 0.62, 0.78, 1.0); bg.inputs['Strength'].default_value = 0.7
    bpy.ops.mesh.primitive_plane_add(size=40); g = bpy.context.active_object; g.name = 'REV_ground'
    gm = bpy.data.materials.new('REV_grass'); gm.use_nodes = True; nt = gm.node_tree; bs = nt.nodes['Principled BSDF']
    tc = nt.nodes.new('ShaderNodeTexCoord'); mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (0.5, 0.5, 0.5)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    im = nt.nodes.new('ShaderNodeTexImage'); im.image = bpy.data.images.load(os.path.join(TEX, 'meadow_grass', 'meadow_grass_albedo.png'), check_existing=True)
    nt.links.new(mp.outputs[0], im.inputs['Vector']); nt.links.new(im.outputs['Color'], bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = 0.95
    g.data.materials.append(gm)
    sd = bpy.data.lights.new('REV_sun', 'SUN'); sd.energy = 4.5; sd.color = (1.0, 0.95, 0.86); sd.angle = math.radians(2)
    so = bpy.data.objects.new('REV_sun', sd); sc.collection.objects.link(so); so.rotation_euler = (math.radians(48), 0, math.radians(-30))
    cam = bpy.data.cameras.new('REV_cam'); co = bpy.data.objects.new('REV_cam', cam); sc.collection.objects.link(co); sc.camera = co
    return co

def look(co, pos, target, lens):
    co.location = Vector(pos); co.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler(); co.data.lens = lens

def world_bounds(root):
    pts = []
    for o in root.children:
        if o.type == 'MESH' and o.name != 'FOOTPRINT':
            pts += [o.matrix_world @ v.co for v in o.data.vertices]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx

def main():
    bpy.ops.wm.read_homefile(use_empty=True)
    names = [n for n in ASSETS if ONLY is None or n in ONLY]
    built = {}; rows = []
    for n in names:
        a, root, objs, tris = build(n)
        export(a, root, tris)
        for o in root.children: o.name += '@' + n   # the next asset must find the plain names free (Blender would number them .001 into the FBX)
        mn, mx = world_bounds(root); total = sum(tris.values())
        built[n] = (a, root); rows.append((n, total, tris, a, mx - mn))
        print('GARDEN %-11s tris %5d  size %.2f x %.2f x %.2f  slots %s  footprint %.2f x %.2f' % (n, total, *(mx - mn), '/'.join(sorted({s for o in objs for s in o.data.materials.keys()})), *a.foot))
    if EXPORT: write_sources(rows)
    if not RENDER: return
    rd = os.path.join(OUT, 'renders'); os.makedirs(rd, exist_ok=True); co = review_scene()
    for n, (a, root) in built.items():
        for m, (_, r2) in built.items(): hide(r2, m != n)
        mn, mx = world_bounds(root); c = (mn + mx) / 2; s = max(mx - mn); d = s * 2.2 + 0.4
        look(co, (c.x + d * 0.55, c.y - d * 0.85, c.z + d * 0.5), c, 50)
        bpy.context.scene.render.filepath = os.path.join(rd, n + '_tq.png'); bpy.ops.render.render(write_still=True)
    for n, (a, root) in built.items(): hide(root, False)
    layout = {'Scarecrow': (-4.6, 3.0, 20), 'WaterButt': (-3.0, 3.2, 0), 'Trellis': (-0.9, 4.0, 0), 'VegBed_A': (0.7, 1.4, 0), 'VegBed_B': (3.5, 1.8, 0),
              'ColdFrame': (-3.0, 1.4, 0), 'Fence_Run': (5.4, 3.6, 0), 'Fence_Gate': (7.1, 3.6, 0)}
    for n, (a, root) in built.items():
        x, y, rz = layout.get(n, (0, 0, 0)); root.location = (x, y, 0); root.rotation_euler = (0, 0, math.radians(rz))
    bpy.ops.mesh.primitive_cube_add(size=1); m = bpy.context.active_object; m.name = 'REV_mannequin'
    m.scale = (0.45, 0.25, 1.7); m.location = (-1.9, 1.0, 0.85)
    mm = bpy.data.materials.new('REV_grey'); mm.use_nodes = True; mm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.3, 0.3, 0.32, 1); m.data.materials.append(mm)
    look(co, (1.8, -4.8, 2.4), (1.2, 2.2, 0.7), 34)
    bpy.context.scene.render.filepath = os.path.join(rd, 'group.png'); bpy.ops.render.render(write_still=True)

def hide(root, h):
    root.hide_render = h
    for o in root.children: o.hide_render = h or o.name.startswith('FOOTPRINT')

def write_sources(rows):
    os.makedirs(DEST, exist_ok=True)
    L = ['# Garden and allotment decorations (queue Q6)', '',
         'Generated by `Art/Blender/town/sc_garden.py` (procedural, deterministic; the run line is in its header). Original geometry, no external source; licence: ours.',
         'Metres, front -Y in Blender (+Z in Unity), pivot at the ground centre. Each FBX: root `<Asset>`, mesh `Body`, `Part_*` meshes (origin at their `SOCKET_*`),',
         '`SOCKET_*` empties and a flat 0.1 m `FOOTPRINT` box (collider only). No maps: materials are slot names that Unity maps to the shared B1 tileables',
         '(`Art/Textures/B1_town/`, UVs in metres by box projection, 1 UV unit = 2 m) and flat colours (`Leaf` #6f8a34, `Produce` #7f9c5c, `Straw` #c9a85a, `Glass` dark, slightly transparent).',
         'A linear corner attribute `wear` (R grime, G moss, B polish) is written as in the B4 kit.', '',
         '| Asset | Tris (total) | Size x y z (m) | Slots | Footprint (m) | Sockets / parts |', '|---|---|---|---|---|---|']
    for n, total, tris, a, size in rows:
        slots = sorted({s for p in a.pieces() for s in p.slots})
        extra = ', '.join(['SOCKET_' + s for s in a.sockets] + [p.name for p in a.parts]) or 'none'
        L.append('| %s | %d (%s) | %.2f x %.2f x %.2f | %s | %.2f x %.2f | %s |' % (n, total, ', '.join('%s %d' % kv for kv in tris.items()), size.x, size.y, size.z, ', '.join(slots), a.foot[0], a.foot[1], extra))
    L += ['', 'Part notes: `Fence_Run/Part_PostA` and `Part_PostB` are the end posts at `SOCKET_PostA` / `SOCKET_PostB` (show one post per junction of a continuous fence); `Fence_Gate/Part_Leaf` has its origin at `SOCKET_Hinge`',
          '(swing about Y); the gate leaf hangs 1.6 degrees low on the latch side.', '']
    with open(os.path.join(DEST, 'SOURCES.md'), 'w', encoding='utf-8', newline='\n') as fh: fh.write('\n'.join(L))

main()
