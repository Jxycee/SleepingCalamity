"""RuinKit: the ruined-foundation kit for Hollowmere's empty plots (Hollowmere mass-production design 5.1-5.3, STORY 8.2).

Ten years empty: the houses were pulled away and only the footing and the doorstep are left. One generator, nine seeded
variants: three per size class (S 6 x 6 m, M 8 x 8 m, L 11 x 11 m). Each is a square ring of low, chunky, slightly soft
sandstone blocks (one or two courses, 0.2-0.5 m, broken and uneven: the top course gone in runs, corner stones missing, a
few blocks split, tipped or slumped outward) over a dark rubble core, an old threshold and step on the front (-Y) side with a
worn dip (SOCKET_Doorstep on the threshold top), heaps of fallen rubble, a few charred and rotted oak post stubs and a
fallen sill beam, and B9 foliage (crack weeds in the joints, grass clumps against the stones, moss tufts on the shaded back
course). Medium whimsy: blocks lean 1-4 deg, nothing is square.

Reads at 20 m from the square cameras: silhouette first (a broken, stepped stone line with gaps and dark heaps), then wear
(grime band at the foot, moss on the shaded back side and the tops, char on the stubs, polish in the step's dip).

Contract (design 5.1-5.3): Blender z up, metres, pivot at the foundation centre at ground, front (doorstep side) -Y; export
-Z forward, Y up (front = local +Z in Unity, like the B4 kit). All geometry stays inside the S x S square, under 0.5 m, and
inside the outer 1 m band, so the interior is clear and a building's plinth covers the footing. Children of the root empty
Ruin_<C>_<V>: Footing (stones, core, wood; FK_Sandstone, FK_ChimneyStone, FK_PlinthJoint, FK_Oak), Foliage (M_B9_Foliage,
the B9 atlas T_B9_Foliage), FOOTPRINT (flat S x S x 0.1 m box, material FOOTPRINT), SOCKET_Doorstep (empty, threshold top
centre, facing -Y). T2: no unique maps; the stones sample the shared B1 tileables on metre UVs (2 m per tile, like B4) and
placed wear lives in the CORNER colour attribute 'wear' (linear 0-1; R grime/char, G moss, B polish), read by WearLit.
Budget: < 8k tris per variant (printed per variant).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_ruin_kit.py -- --out <scratch>
      [--only Ruin_M_B,...] [--rres 960] [--samples 32] [--no-render] [--export]
  Renders <out>/<Name>_far.png (20 m, square-camera height) and <Name>_near.png (8 m, 3/4), plus <out>/ruin_contact.png (all
  nine). --export writes <out>/fbx/<Name>.fbx and copies each to Assets/_Project/Content/Art/Town/Ruins/<Name>/<Name>.fbx with
  the B9 foliage atlas beside it (identical copies dedupe in LFS) and Ruins/SOURCES.md.

refs: Reference/hollowmere/ruins/REFS.md (deserted-village house platforms and cottage footings), PHASE1 3.1 palette, 3.4 shape
language; docs/art/beauty-corner-brief.md for the scoring.

Status (2026-10-31, cloud): exported. Self-review 30/40 (calibrated rubric; rounds 26, 28, 29.5, 30; target 31 not reached
within the 4-round limit). Tris S 5.8-6.6k, M 7.0-7.5k, L 6.9-7.6k. Rounds, per-variant table and parked ideas: Ruins/SOURCES.md.
"""
import sys, os, math, random, zlib, shutil
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, noise
import sc_b9_lib as L

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
RRES = int(arg('--rres', '960')); SAMPLES = int(arg('--samples', '32'))
RENDER = '--no-render' not in argv; EXPORT = '--export' in argv
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
TEX = os.path.join(REPO, 'Art', 'Textures')
B9 = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B9')
EXP = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'Ruins')
TILE = 2.0                      # metres per tileable repeat, as the B4 kit
CLASSES = {'S': 6.0, 'M': 8.0, 'L': 11.0}
NAMES = ['Ruin_%s_%s' % (c, v) for c in 'SML' for v in 'ABC']
ONLY = arg('--only', '').split(',') if arg('--only') else NAMES
BLOCK = {'S': 1.0, 'M': 1.1, 'L': 1.4}   # block length scale per class (big houses, big stones; holds the budget)
FOOT_W = 0.58                   # footing width (one block across)
FOOT_IN = 0.16                  # footing outer face this far inside the square edge (room for the step and slumped blocks)
MAX_Z = 0.5                     # contract: nothing taller (a building's plinth covers it)
OAK_TINT = (0.80, 0.76, 0.72)

# ----------------------------------------------------------------------------- materials (same slots as the B4 kit)
MATS = {}
def _img(path, nonc):
    im = bpy.data.images.load(path, check_existing=True)
    if nonc:
        im.colorspace_settings.name = 'Non-Color'
    return im

def tile_mat(name, folder, tint=(1, 1, 1), normal=1.0):
    """The B4 kit's tile material (sc_facade_kit.tile_mat, trimmed): tileable x tint, wear R grime, G moss, B polish."""
    if name in MATS:
        return MATS[name]
    stem = folder.split('/')[-1]; base = os.path.join(TEX, folder, stem)
    m = bpy.data.materials.new(name); m.use_nodes = True
    N = m.node_tree.nodes; Lk = m.node_tree.links; bsdf = N['Principled BSDF']
    alb = N.new('ShaderNodeTexImage'); alb.image = _img(base + '_albedo.png', False)
    nrm = N.new('ShaderNodeTexImage'); nrm.image = _img(base + '_normal.png', True)
    orm = N.new('ShaderNodeTexImage'); orm.image = _img(base + '_orm.png', True)
    col = N.new('ShaderNodeVertexColor'); col.layer_name = 'wear'
    sep = N.new('ShaderNodeSeparateColor'); Lk.new(col.outputs[0], sep.inputs[0])
    tin = N.new('ShaderNodeMix'); tin.data_type = 'RGBA'; tin.blend_type = 'MULTIPLY'
    tin.inputs[0].default_value = 1.0; tin.inputs[7].default_value = (*tint, 1); Lk.new(alb.outputs[0], tin.inputs[6])
    gri = N.new('ShaderNodeMix'); gri.data_type = 'RGBA'; gri.inputs[7].default_value = (0.055, 0.045, 0.036, 1)
    Lk.new(sep.outputs[0], gri.inputs[0]); Lk.new(tin.outputs[2], gri.inputs[6])
    pol = N.new('ShaderNodeMix'); pol.data_type = 'RGBA'; pol.blend_type = 'SCREEN'; pol.inputs[7].default_value = (0.30, 0.26, 0.20, 1)
    Lk.new(sep.outputs[2], pol.inputs[0]); Lk.new(gri.outputs[2], pol.inputs[6])
    tco = N.new('ShaderNodeTexCoord')
    nz = N.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3.0; nz.inputs['Detail'].default_value = 6
    Lk.new(tco.outputs['Object'], nz.inputs['Vector'])
    mr = N.new('ShaderNodeMapRange'); Lk.new(nz.outputs[0], mr.inputs[0]); mr.inputs[1].default_value = 0.42; mr.inputs[2].default_value = 0.62
    mm = N.new('ShaderNodeMath'); mm.operation = 'MULTIPLY'; Lk.new(sep.outputs[1], mm.inputs[0]); Lk.new(mr.outputs[0], mm.inputs[1])
    mc = N.new('ShaderNodeMix'); mc.data_type = 'RGBA'; mc.inputs[6].default_value = (0.10, 0.13, 0.03, 1); mc.inputs[7].default_value = (0.30, 0.30, 0.04, 1)
    Lk.new(nz.outputs[0], mc.inputs[0])
    mo = N.new('ShaderNodeMix'); mo.data_type = 'RGBA'
    Lk.new(mm.outputs[0], mo.inputs[0]); Lk.new(pol.outputs[2], mo.inputs[6]); Lk.new(mc.outputs[2], mo.inputs[7])
    Lk.new(mo.outputs[2], bsdf.inputs['Base Color'])
    so = N.new('ShaderNodeSeparateColor'); Lk.new(orm.outputs[0], so.inputs[0])
    ra = N.new('ShaderNodeMath'); ra.operation = 'MULTIPLY_ADD'; ra.use_clamp = True
    Lk.new(mm.outputs[0], ra.inputs[0]); ra.inputs[1].default_value = 0.25; Lk.new(so.outputs[1], ra.inputs[2])
    Lk.new(ra.outputs[0], bsdf.inputs['Roughness']); Lk.new(so.outputs[2], bsdf.inputs['Metallic'])
    nm = N.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = normal
    Lk.new(nrm.outputs[0], nm.inputs['Color']); Lk.new(nm.outputs[0], bsdf.inputs['Normal'])
    MATS[name] = m
    return m

SLOTS = ['stone', 'stone_dk', 'core', 'oak']
def slot_mats():
    return [tile_mat('FK_Sandstone', 'B1_town/sandstone'),
            tile_mat('FK_ChimneyStone', 'B1_town/sandstone', tint=(0.80, 0.74, 0.68)),
            tile_mat('FK_PlinthJoint', 'B1_town/sandstone', tint=(0.30, 0.28, 0.24)),
            tile_mat('FK_Oak', 'B1_town/oak_timber', tint=OAK_TINT, normal=1.2)]

# ----------------------------------------------------------------------------- geometry builder
class Geo:
    """Verts + faces with a material slot per face; stones share verts (smooth, soft), UVs are metre box projections.
    fit(): every piece is nudged whole so it stays inside the square (lim) and out of the clear interior (inner)."""
    def __init__(self, lim, inner):
        self.v = []; self.f = []; self.m = []; self.pid = []; self.np = 0; self.lim = lim; self.inner = inner
    def fit(self, verts):
        vs = [Vector(p) for p in verts]
        for ax in (0, 1):
            hi = max(v[ax] for v in vs) - self.lim; lo = -self.lim - min(v[ax] for v in vs)
            sh = -hi if hi > 0 else (lo if lo > 0 else 0.0)
            for v in vs:
                v[ax] += sh
        inside = [v for v in vs if abs(v.x) < self.inner and abs(v.y) < self.inner]
        if inside:
            c = sum(vs, Vector()) / len(vs)
            ax = 0 if abs(c.x) > abs(c.y) else 1
            sgn = 1.0 if c[ax] >= 0 else -1.0
            need = max(self.inner - abs(v[ax]) for v in inside) if all(v[ax] * sgn >= 0 for v in inside) else \
                max(self.inner + abs(v[ax]) if v[ax] * sgn < 0 else self.inner - abs(v[ax]) for v in inside)
            for v in vs:
                v[ax] += sgn * need
        return vs
    def add(self, verts, faces, slot):
        o = len(self.v); self.v += self.fit(verts)
        for f in faces:
            self.f.append(tuple(i + o for i in f)); self.m.append(SLOTS.index(slot)); self.pid.append(self.np)
        self.np += 1
    def tris(self):
        return sum(len(f) - 2 for f in self.f)

def soft_box(size, xf, R, soft=0.22, wob=0.02, bulge=0.025, cuts=1, bottom=False, top_break=0.0):
    """A chunky pillow block: a cube grid (cuts per axis) with corners and edges pulled in (soft), per-vertex wobble, a
    domed top and optionally a broken (jagged, lowered) top. No bottom face unless asked (it sits in the ground)."""
    n = cuts + 1
    idx = {}; verts = []
    def vid(i, j, k):
        key = (i, j, k)
        if key not in idx:
            c = [2 * i / n - 1, 2 * j / n - 1, 2 * k / n - 1]
            edge = sum(1 for a in c if abs(a) > 0.999)
            pull = 1.0 - soft * max(0, edge - 1) / 2.0
            p = Vector((c[0] * pull * size[0] / 2, c[1] * pull * size[1] / 2, c[2] * pull * size[2] / 2))
            if c[2] > 0.999:
                p.z += bulge * (1 - c[0] ** 2) * (1 - c[1] ** 2)
                if top_break:
                    p.z -= top_break * max(0.0, noise.noise(Vector((p.x * 7 + R.random() * 50, p.y * 7, 3.3))) + 0.35)
            p += Vector((R.uniform(-wob, wob), R.uniform(-wob, wob), R.uniform(-wob, wob) * 0.6))
            idx[key] = len(verts); verts.append(xf @ p)
        return idx[key]
    faces = []
    r = range(n)
    for a in r:
        for b in r:
            faces.append((vid(a, b, n), vid(a + 1, b, n), vid(a + 1, b + 1, n), vid(a, b + 1, n)))          # top
            faces.append((vid(a, 0, b), vid(a + 1, 0, b), vid(a + 1, 0, b + 1), vid(a, 0, b + 1)))          # -y
            faces.append((vid(a, n, b), vid(a, n, b + 1), vid(a + 1, n, b + 1), vid(a + 1, n, b)))          # +y
            faces.append((vid(0, a, b), vid(0, a, b + 1), vid(0, a + 1, b + 1), vid(0, a + 1, b)))          # -x
            faces.append((vid(n, a, b), vid(n, a + 1, b), vid(n, a + 1, b + 1), vid(n, a, b + 1)))          # +x
            if bottom:
                faces.append((vid(a, b, 0), vid(a, b + 1, 0), vid(a + 1, b + 1, 0), vid(a + 1, b, 0)))
    return verts, faces

def place(x, y, z, yaw=0.0, tilt_x=0.0, tilt_y=0.0):
    return (Matrix.Translation((x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(tilt_x, 4, 'X')
            @ Matrix.Rotation(tilt_y, 4, 'Y'))

# ----------------------------------------------------------------------------- the ruin
class Ruin:
    """One variant. The footing runs round the square as four sides; t along each side, side 0 = front (-Y, doorstep)."""
    def __init__(self, name):
        self.name = name; self.cls = name.split('_')[1]; self.S = CLASSES[self.cls]; self.h = self.S / 2
        self.seed = zlib.crc32(name.encode()) & 0xFFFF
        self.R = random.Random(self.seed); self.nofs = Vector((self.seed % 97, self.seed % 61, 0.0))
        self.g = Geo(self.h - 0.03, self.h - 1.0); self.plants = []        # (kind, x, y, z, yaw, scale)
        self.rc = self.h - FOOT_IN - FOOT_W / 2  # footing centre line (distance from the centre)
        R = self.R
        self.door_x = R.choice([-1, 0, 1]) * R.uniform(0.15, 0.3) * self.h
        self.door_w = 1.15
        # which corners lost their blocks (1-2 of the 4), which side carries a long collapse
        self.lost = R.sample(range(4), R.choice([1, 2]))
        self.collapse = R.choice([1, 2, 3])
        self.decay = {'A': 0.25, 'B': -0.05, 'C': -0.3}[name[-1]] + R.uniform(-0.1, 0.1)   # A mostly whole, C mostly gone
        self.heaps = []

    # side frame: side k runs from corner k to corner k+1, counter-clockwise from front-left
    def side_frame(self, k):
        c = self.rc
        corners = [Vector((-c, -c, 0)), Vector((c, -c, 0)), Vector((c, c, 0)), Vector((-c, c, 0))]
        a, b = corners[k], corners[(k + 1) % 4]
        d = (b - a).normalized(); out = Vector((d.y, -d.x, 0))
        return a, b, d, out

    def height_profile(self, k, t, L_):
        """Footing top at distance t along side k (0..L_): 0.2-0.5 m, with runs where the top course has gone."""
        R = self.R
        p = Vector((k * 13.1 + t * 0.55, 0.0, 0.0)) + self.nofs
        nz = noise.noise(p)
        top = nz > self.decay - (0.3 if k == 2 else 0.0)        # the back side keeps most of its top course
        if k == self.collapse and 0.25 * L_ < t < 0.75 * L_:
            top = False
        return top

    def build_footing(self):
        R = self.R; g = self.g
        for k in range(4):
            a, b, d, out = self.side_frame(k)
            L_ = (b - a).length
            yaw = math.atan2(d.y, d.x)
            # the dark rubble-and-earth core under the blocks: irregular low lumps (deep joints, never a continuous sill)
            for t0, t1 in self.runs(k, L_):
                t = t0
                while t < t1 - 0.15:
                    ln = min(R.uniform(0.9, 1.6), t1 - t)
                    mid = a + d * (t + ln / 2) + out * R.uniform(-0.06, 0.06)
                    hh = R.uniform(0.09, 0.15)
                    vs, fs = soft_box((ln + 0.1, FOOT_W - R.uniform(0.14, 0.24), hh), place(mid.x, mid.y, hh / 2 - 0.03, yaw + math.radians(R.uniform(-5, 5)),
                                      math.radians(R.uniform(-3, 3)), math.radians(R.uniform(-3, 3))), R, soft=0.0, wob=0.04, bulge=0.0, cuts=0)
                    g.add(vs, fs, 'core')
                    t += ln
            # bottom course
            t = -FOOT_W / 2 if k else -FOOT_W / 2
            ti = 0
            while t < L_ + FOOT_W / 2 - 0.1:
                ln = R.uniform(0.55, 0.95) * BLOCK[self.cls]
                tc = t + ln / 2
                if self.skip(k, tc, L_, course=0):
                    t += ln; continue
                p = a + d * tc
                h0 = R.uniform(0.18, 0.30)
                if k == self.collapse and 0.25 * L_ < tc < 0.75 * L_:
                    h0 = R.uniform(0.10, 0.17)
                split = R.random() < 0.10
                slump = R.random() < 0.08 and not self.is_door(k, tc)
                off = out * (R.uniform(0.15, 0.35) if slump else R.uniform(-0.03, 0.03))
                tilt = math.radians(R.uniform(-3, 3)); roll = math.radians(R.uniform(-2.5, 2.5) + (12 if slump else 0))
                mat = 'stone_dk' if R.random() < 0.45 else 'stone'
                pieces = [(-ln / 4, ln / 2 - 0.03), (ln / 4, ln / 2 - 0.03)] if split else [(0.0, ln - 0.035)]
                for dx, pl in pieces:
                    q = p + d * dx + off
                    xf = place(q.x, q.y, h0 / 2 - 0.04, yaw + math.radians(R.uniform(-3, 3)), roll * (1 if k % 2 == 0 else 0), tilt)
                    if k % 2:
                        xf = place(q.x, q.y, h0 / 2 - 0.04, yaw + math.radians(R.uniform(-3, 3)), roll, tilt)
                    covered = self.height_profile(k, tc, L_) and not split and not slump
                    vs, fs = soft_box((pl, FOOT_W - R.uniform(0.0, 0.08), h0 + (R.uniform(-0.05, 0.0) if split else 0)), xf, R,
                                      soft=R.uniform(0.07, 0.16), wob=0.02, bulge=R.uniform(0.0, 0.012),
                                      top_break=0.04 if (split or R.random() < 0.3) else 0.0, cuts=0 if covered else 1)
                    g.add(vs, fs, mat)
                t += ln; ti += 1
            # top course: broken runs, joints offset from the course below
            t = -FOOT_W / 2 + R.uniform(0.2, 0.4)
            while t < L_ + FOOT_W / 2 - 0.2:
                ln = R.uniform(0.5, 0.85) * BLOCK[self.cls]
                tc = t + ln / 2
                if self.skip(k, tc, L_, course=1) or not self.height_profile(k, tc, L_):
                    t += ln; continue
                p = a + d * tc + out * R.uniform(-0.04, 0.04)
                h1 = R.uniform(0.15, 0.22)
                z0 = R.uniform(0.20, 0.25)
                h1 = min(h1, MAX_Z - z0 - 0.04)
                xf = place(p.x, p.y, z0 + h1 / 2, yaw + math.radians(R.uniform(-4, 4)), math.radians(R.uniform(-3, 3)),
                           math.radians(R.uniform(-3, 3)))
                vs, fs = soft_box((ln - 0.05, FOOT_W - R.uniform(0.04, 0.16), h1), xf, R, soft=R.uniform(0.08, 0.18), wob=0.022,
                                  bulge=R.uniform(0.0, 0.015), top_break=0.05 if R.random() < 0.4 else 0.0)
                g.add(vs, fs, 'stone_dk' if R.random() < 0.6 else 'stone')
                t += ln

    def build_quoins(self):
        """Big corner blocks where a corner survived: the tallest, squarest stones, the anchors of the silhouette."""
        R = self.R
        for c in range(4):
            if c in self.lost:
                continue
            a, b, d, out = self.side_frame(c)
            hq = R.uniform(0.40, 0.47)
            vs, fs = soft_box((0.86, FOOT_W + 0.06, hq), place(a.x, a.y, hq / 2 - 0.04, math.atan2(d.y, d.x) + math.radians(R.uniform(-4, 4)),
                              math.radians(R.uniform(-2.5, 2.5)), math.radians(R.uniform(-2.5, 2.5))), R,
                              soft=0.2, wob=0.025, bulge=0.03, top_break=0.05 if R.random() < 0.5 else 0.0)
            self.g.add(vs, fs, 'stone_dk' if R.random() < 0.4 else 'stone')

    def runs(self, k, L_):
        """Core runs (t0, t1) for side k: the whole side, minus the doorway on the front."""
        if k == 0:
            dx = self.door_x + self.rc
            return [(-0.1, dx - self.door_w / 2), (dx + self.door_w / 2, L_ + 0.1)]
        return [(-0.1, L_ + 0.1)]

    def is_door(self, k, t):
        return k == 0 and abs((t - self.rc) - self.door_x) < self.door_w / 2 + 0.38

    def skip(self, k, t, L_, course):
        R = self.R
        if self.is_door(k, t):
            return True
        # lost corners: both courses go for about a block either side of the corner
        for c in self.lost:
            if (c == k and t < 0.55) or ((c - 1) % 4 == k and t > L_ - 0.55):
                if course == 1 or R.random() < 0.8:
                    if t < 0.55 or t > L_ - 0.55:
                        self.heaps.append((k, t)); return True
        for c in range(4):                             # surviving corners: the quoin block stands there instead
            if c in self.lost:
                continue
            if (c == k and t < 0.42) or ((c - 1) % 4 == k and t > L_ - 0.42):
                return True
        if course == 0 and R.random() < 0.06:          # a missing block mid-run
            self.heaps.append((k, t)); return True
        return False

    def build_doorstep(self):
        R = self.R; g = self.g
        c = self.rc; x = self.door_x
        def dish(depth, cx, sx, sy, cy):
            def f(vs):
                for v in vs:
                    if v.z > 0.05:
                        u = max(0.0, 1 - ((v.x - cx) / sx) ** 2); w = max(0.0, 1 - ((v.y - cy) / sy) ** 2)
                        v.z -= depth * u * w
            return f
        # threshold: one long worn block in the footing gap
        thr_top = 0.24
        vs, fs = soft_box((self.door_w + 0.14, FOOT_W, thr_top + 0.04), place(x, -c, thr_top / 2 - 0.02, math.radians(R.uniform(-1.5, 1.5))),
                          R, soft=0.16, wob=0.008, bulge=0.004, cuts=3)
        dish(0.04, x, 0.5, 0.35, -c)(vs)
        g.add(vs, fs, 'stone')
        # step: a lower slab in front of it, slightly cracked off the line
        sy = -self.h + 0.33
        vs, fs = soft_box((self.door_w + 0.45, 0.62, 0.20), place(x + R.uniform(-0.05, 0.05), sy, 0.07, math.radians(R.uniform(-3, 3)),
                          math.radians(R.uniform(-1.5, 1.5))), R, soft=0.18, wob=0.01, bulge=0.004, cuts=3)
        dish(0.035, x, 0.55, 0.3, sy)(vs)
        g.add(vs, fs, 'stone')
        # door-jamb bases: two squarer stones standing proud either side of the threshold (the doorway reads in silhouette)
        for sgn in (-1, 1):
            hj = R.uniform(0.40, 0.47)
            vs, fs = soft_box((0.36, FOOT_W + 0.04, hj), place(x + sgn * (self.door_w / 2 + 0.2), -c, hj / 2 - 0.04, math.radians(R.uniform(-3, 3)),
                              math.radians(R.uniform(-2, 2)), math.radians(R.uniform(-3, 3))), R, soft=0.14, wob=0.015, bulge=0.01,
                              top_break=0.05 if sgn > 0 else 0.0)
            g.add(vs, fs, 'stone_dk')
        self.socket = Vector((x, -c, thr_top - 0.02))
        self.step = (x, sy)

    def build_rubble(self):
        """Heaps where blocks fell (corners, gaps, the collapsed run) and a scatter along the band; inner band only 1 m deep."""
        R = self.R; g = self.g
        spots = list(self.heaps)
        a, b, d, out = self.side_frame(self.collapse)
        L_ = (b - a).length
        main = [(self.collapse, L_ * f) for f in (0.35, 0.5, 0.65)]
        R.shuffle(spots)
        spots = main + spots[:2 + {'S': 0, 'M': 1, 'L': 2}[self.cls]]
        for (k, t) in spots:
            a, b, d, out = self.side_frame(k)
            base = a + d * t
            n = R.randint(5, 8)
            for i in range(n):
                side = R.choice([-1, 1, 1])                   # mostly spilled outward
                q = base + d * R.uniform(-0.45, 0.45) + out * side * R.uniform(0.15, 0.45)
                q = self.clamp_band(q)
                if self.near_step(q):
                    continue
                big = R.random() < (0.6 if k == self.collapse else 0.4)
                s = R.uniform(0.32, 0.48) if big else R.uniform(0.18, 0.28)
                size = (s * R.uniform(0.9, 1.5), s * R.uniform(0.8, 1.2), s * R.uniform(0.45, 0.75))
                xf = place(q.x, q.y, size[2] * 0.35, R.uniform(0, math.pi), math.radians(R.uniform(-18, 18)), math.radians(R.uniform(-18, 18)))
                vs, fs = soft_box(size, xf, R, soft=0.3, wob=0.03 * s / 0.3, bulge=0.01, cuts=1 if big else 0, top_break=0.03 if big else 0.0)
                g.add(vs, fs, 'stone_dk' if R.random() < 0.5 else 'stone')
                if big and i < 3:                               # a second layer: heaps, not a scatter
                    s2 = R.uniform(0.2, 0.3)
                    size2 = (s2 * R.uniform(1.0, 1.4), s2, s2 * 0.6)
                    q2 = q + d * R.uniform(-0.12, 0.12)
                    xf = place(q2.x, q2.y, size[2] * 0.7 + size2[2] * 0.3, R.uniform(0, math.pi), math.radians(R.uniform(-25, 25)), math.radians(R.uniform(-25, 25)))
                    vs, fs = soft_box(size2, xf, R, soft=0.3, wob=0.02, bulge=0.01, cuts=0)
                    g.add(vs, fs, 'stone_dk' if R.random() < 0.5 else 'stone')
            if R.random() < 0.2:
                self.plants.append(('grass', base.x + out.x * 0.4, base.y + out.y * 0.4, 0.0, R.uniform(0, 6.3), R.uniform(1.4, 1.8)))

    def clamp_band(self, q):
        """Keep a point inside the square (0.1 m margin) and inside the outer 1 m band."""
        lim = self.h - 0.12
        x = max(-lim, min(lim, q.x)); y = max(-lim, min(lim, q.y))
        inner = self.h - 1.0
        if abs(x) < inner and abs(y) < inner:          # pushed into the clear interior: move it out to the band
            if abs(x) > abs(y):
                x = math.copysign(inner + 0.05, x)
            else:
                y = math.copysign(inner + 0.05, y)
        return Vector((x, y, 0.0))

    def near_step(self, q):
        return abs(q.x - self.door_x) < self.door_w / 2 + 0.35 and q.y < -self.rc + 0.4

    def build_wood(self):
        """Charred and rotted oak: 2-3 post stubs standing in the footing line, one fallen sill beam along a side."""
        R = self.R; g = self.g
        n = 2 + (self.cls == 'L') + R.choice([0, 1])
        for i in range(n):
            k = R.choice([1, 2, 3, 0])
            a, b, d, out = self.side_frame(k)
            L_ = (b - a).length
            t = R.choice([0.0, L_]) if R.random() < 0.5 else R.uniform(0.25, 0.75) * L_
            if self.is_door(k, t):
                t = 0.0
            p = a + d * t + out * R.uniform(-0.1, 0.1)
            hgt = R.uniform(0.32, 0.48)
            sec = R.uniform(0.28, 0.34)
            xf = place(p.x, p.y, hgt / 2 - 0.06, math.radians(R.uniform(0, 90)), math.radians(R.uniform(-6, 6)), math.radians(R.uniform(-6, 6)))
            vs, fs = soft_box((sec, sec, hgt + 0.06), xf, R, soft=0.10, wob=0.015, bulge=0.0, cuts=2, top_break=0.12)
            g.add(vs, fs, 'oak')
        # fallen sill beam lying in the outer band along a side, half on the footing
        k = R.choice([1, 2, 3])
        a, b, d, out = self.side_frame(k)
        L_ = (b - a).length
        ln = min(R.uniform(1.6, 2.6), L_ * 0.5)
        t = R.uniform(ln / 2 + 0.4, L_ - ln / 2 - 0.4)
        p = a + d * t + out * R.uniform(0.25, 0.4)
        p = self.clamp_band(p)
        yaw = math.atan2(d.y, d.x) + math.radians(R.uniform(-8, 8))
        vs, fs = soft_box((ln, 0.24, 0.20), place(p.x, p.y, 0.07, yaw, math.radians(R.uniform(-4, 4)), math.radians(R.uniform(-3, 3))), R,
                          soft=0.12, wob=0.02, bulge=0.0, cuts=1)
        # rotted ends: pull the end verts down and in
        for v in vs:
            lx = (Matrix.Rotation(-yaw, 3, 'Z') @ (v - Vector((p.x, p.y, 0)))).x
            if abs(lx) > ln / 2 - 0.08:
                v.z -= 0.06 * R.random()
        g.add(vs, fs, 'oak')
        self.beam = (p, yaw, ln)

    def plan_plants(self):
        """Weeds in the joints at the footing's foot (both faces), grass at corners and against the stones, moss on the back course."""
        R = self.R
        nweed = {'S': 1, 'M': 1, 'L': 0}[self.cls]
        ngrass = {'S': 1, 'M': 1, 'L': 1}[self.cls]
        # B9 grass strips lapping off the footing's foot (breaks the stone-to-ground seam), alternating faces
        nstrip = {'S': 4, 'M': 5, 'L': 4}[self.cls]
        ks = [0, 1, 2, 3] + [R.choice([1, 2, 3]) for _ in range(nstrip - 4)]
        for j, k in enumerate(ks):
            a, b, d, out = self.side_frame(k)
            L_ = (b - a).length
            t = R.uniform(0.2, 0.8) * L_
            if self.is_door(k, t):
                t = (t + L_ / 2) % L_
            side = -1 if (k == 2 or R.random() < 0.4) else 1     # the shaded back side grows on its inner face too
            dvec = out * side
            q = a + d * t + dvec * (FOOT_W / 2 - 0.03)
            self.plants.append(('strip', q.x, q.y, 0.0, math.atan2(dvec.x, -dvec.y), R.uniform(0.95, 1.25)))
        nmoss = {'S': 5, 'M': 6, 'L': 7}[self.cls]
        for i in range(nweed):
            k = R.choice([1, 2, 3, 0, 2])
            a, b, d, out = self.side_frame(k)
            L_ = (b - a).length; t = R.uniform(0.15, 0.85) * L_
            if self.is_door(k, t):
                continue
            side = R.choice([-1, 1])
            q = a + d * t + out * side * (FOOT_W / 2 + 0.05)
            q = self.clamp_band(q)
            self.plants.append(('weed', q.x, q.y, 0.0, math.atan2(d.y, d.x) + (0 if side > 0 else math.pi), R.uniform(1.6, 1.9)))
        for i in range(ngrass):
            c = R.randrange(4)
            a, b, d, out = self.side_frame(c)
            q = a + out * R.uniform(0.35, 0.5) + d * R.uniform(-0.3, 0.3)
            q = self.clamp_band(q)
            self.plants.append(('grass', q.x, q.y, 0.0, R.uniform(0, 6.3), R.uniform(1.6, 2.0)))
        # moss tufts on the shaded back course tops (+Y side) and the inner foot
        for i in range(nmoss):
            k = 2 if i % 2 == 0 else R.choice([0, 1, 3])
            a, b, d, out = self.side_frame(k)
            L_ = (b - a).length
            t = R.uniform(0.1, 0.9) * L_
            if self.is_door(k, t):
                continue
            on_top = R.random() < 0.8
            q = a + d * t + (Vector() if on_top else -out * (FOOT_W / 2 + 0.08))
            z = self.top_at(q) if on_top else 0.0
            self.plants.append(('moss', q.x, q.y, z, math.atan2(d.y, d.x), R.uniform(1.4, 1.8)))

    def top_at(self, q):
        """Highest stone vertex within 0.2 m of q (moss sits on what is really there)."""
        best = 0.0
        for v in self.g.v:
            if (v.x - q.x) ** 2 + (v.y - q.y) ** 2 < 0.04 and v.z > best:
                best = v.z
        return max(0.0, best - 0.03)

    # ----------------------------------------------------------------------------- mesh + wear
    def to_object(self, coll):
        g = self.g
        me = bpy.data.meshes.new('Footing'); me.from_pydata([tuple(v) for v in g.v], [], g.f)
        me.polygons.foreach_set('material_index', g.m)
        me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
        for m in slot_mats():
            me.materials.append(m)
        me.validate(clean_customdata=False)          # before the layers: validate reallocates custom data
        nl = len(me.loops); UV = np.zeros((nl, 2), np.float32); WC = np.ones((nl, 4), np.float32)
        h = self.h
        sx, sy = self.step
        pv = [random.Random(self.seed * 7919 + i).random() for i in range(self.g.np + 1)]
        for poly in me.polygons:
            n = poly.normal
            self.piece_v = pv[self.g.pid[poly.index]]
            ax = max(range(3), key=lambda i: abs(n[i]))
            slot = SLOTS[poly.material_index]
            for li in poly.loop_indices:
                p = me.vertices[me.loops[li].vertex_index].co
                # metre UVs, box-projected on the face's dominant axis (2 m per tile); oak grain runs along the long axis
                if ax == 2:
                    uv = (p.x / TILE, p.y / TILE)
                elif ax == 1:
                    uv = (p.x / TILE, p.z / TILE)
                else:
                    uv = (p.y / TILE, p.z / TILE)
                UV[li] = uv
                WC[li] = self.wear_at(p, n, slot)
        # bulk-set at the end: per-element UV writes create UV sub-layers that reallocate attribute storage under a held handle
        me.uv_layers.new(name='UVMap'); me.uv_layers['UVMap'].data.foreach_set('uv', UV.ravel())
        me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER'); me.color_attributes['wear'].data.foreach_set('color', WC.ravel())
        ob = bpy.data.objects.new('Footing', me); coll.objects.link(ob)
        return ob

    def wear_at(self, p, n, slot):
        nz = noise.noise(p * 2.3 + self.nofs)
        streak = noise.noise(Vector((p.x * 4.0, p.y * 4.0, p.z * 0.8)) + self.nofs)
        # R: grime / damp at the foot, ragged top; dark core; char on the oak (heaviest at the broken top)
        r = max(0.0, 1 - p.z / (0.24 + 0.07 * nz + 0.05 * streak)) * 0.92
        r = max(r, 0.18 * max(0.0, noise.noise(p * 0.6 + self.nofs + Vector((2.0, 1.0, 0.0)))) + 0.05)
        r = max(r, 0.55 * self.piece_v ** 1.3)          # block-to-block value: weathered, reused and fire-darkened stones
        if n.z < 0.3:                                     # damp creeps up the vertical faces, streaked
            r = max(r, (0.5 + 0.2 * streak) * max(0.0, 1 - p.z / 0.32))
        if slot == 'oak':
            r = max(r, min(0.95, 0.55 + 0.9 * p.z + 0.25 * nz))
        if slot == 'core':
            r = max(r, 0.5)
        # G: moss on the shaded back side (+Y) and in a broken patch on tops everywhere
        back = max(0.0, (p.y - (self.h - 1.2)) / 1.0)
        gm = 0.0
        if n.z > 0.45:
            gm = max(gm, min(1.0, back) * (0.8 + 0.3 * nz))
            gm = max(gm, min(1.0, 0.45 + 0.9 * noise.noise(p * 1.4 + self.nofs + Vector((5.0, 0.0, 0.0)))))
        if n.y > 0.4 and p.z < 0.35:                      # the north-facing (back) faces of every stone
            gm = max(gm, 0.55 * n.y + 0.2 * nz)
        if slot == 'oak':
            gm *= 0.4
        # B: polish in the threshold and step dips (feet went in and out for years)
        b = 0.0
        if n.z > 0.6 and abs(p.x - self.door_x) < 0.6 and p.y < -self.rc + 0.3:
            b = max(0.0, 1 - abs(p.x - self.door_x) / 0.6) * 0.8
            gm *= 0.1; r *= 0.35
        return (min(1.0, max(0.0, r)), min(1.0, max(0.0, gm)), min(1.0, b), 1.0)

# ----------------------------------------------------------------------------- B9 foliage (the exported meshes, re-used)
B9_SRC = {'weed': ['Weed_A/SC_B9_CrackWeed_A', 'Weed_B/SC_B9_CrackWeed_B', 'Weed_C/SC_B9_CrackWeed_C',
                   'Weed_D/SC_B9_CrackWeed_D', 'Weed_E/SC_B9_CrackWeed_E'],
          'strip': ['Grass_Strip/SC_B9_GrassStrip'],
          'grass': ['Grass_A/SC_B9_GrassClump_A', 'Grass_B/SC_B9_GrassClump_B', 'Grass_C/SC_B9_GrassClump_C'],
          'moss': ['Moss_Tuft_A/SC_B9_MossTuft_A', 'Moss_Tuft_B/SC_B9_MossTuft_B', 'Moss_Tuft_C/SC_B9_MossTuft_C']}
FOLIAGE_PREFIX = os.path.join(B9, 'Weed_A', 'T_B9_Foliage')

def load_b9():
    """Import each B9 FBX once into a hidden library collection (same axes as the export: -Z forward, Y up)."""
    lib = bpy.data.collections.new('B9_lib')
    mat = L.pbr_material('M_B9_Foliage', FOLIAGE_PREFIX)
    out = {}
    for kind, rels in B9_SRC.items():
        out[kind] = []
        for rel in rels:
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=os.path.join(B9, rel + '.fbx'), use_custom_normals=True)
            new = [o for o in bpy.data.objects if o not in before and o.type == 'MESH']
            for o in bpy.data.objects:
                if o not in before and o.type != 'MESH':
                    bpy.data.objects.remove(o)
            o = new[0]
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            o.data.materials.clear(); o.data.materials.append(mat)
            for c in o.users_collection:
                c.objects.unlink(o)
            lib.objects.link(o)
            out[kind].append(o)
    return out

def build_foliage(ruin, lib, coll):
    R = random.Random(ruin.seed ^ 0xF0)
    parts = []
    for (kind, x, y, z, yaw, s) in ruin.plants:
        src = R.choice(lib[kind])
        o = src.copy(); o.data = src.data.copy(); coll.objects.link(o)
        top = max(c[2] for c in src.bound_box) * s
        sz = s * min(1.0, (MAX_Z - 0.02 - z) / top) if top > 0 else s     # nothing over the contract height
        o.matrix_world = Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Diagonal((s, s, sz, 1.0))
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        lim = ruin.h - 0.03
        for ax, val in ((0, x), (1, y)):
            hi = max(c[ax] for c in bb) + val - lim; lo = -lim - (min(c[ax] for c in bb) + val)
            if hi > 0: val -= hi
            elif lo > 0: val += lo
            if ax == 0: x = val
            else: y = val
        o.matrix_world = Matrix.Translation((x, y, z)) @ o.matrix_world
        parts.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.name = 'Foliage'; ob.data.name = 'Foliage'
    return ob

# ----------------------------------------------------------------------------- assembly
def footprint_box(S, coll):
    h = S / 2
    vs = [(-h, -h, 0), (h, -h, 0), (h, h, 0), (-h, h, 0), (-h, -h, 0.1), (h, -h, 0.1), (h, h, 0.1), (-h, h, 0.1)]
    fs = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    me = bpy.data.meshes.new('FOOTPRINT'); me.from_pydata(vs, [], fs)
    m = bpy.data.materials.get('FOOTPRINT') or bpy.data.materials.new('FOOTPRINT')
    me.materials.append(m)
    ob = bpy.data.objects.new('FOOTPRINT', me); coll.objects.link(ob); ob.hide_render = True
    return ob

def build_variant(name, lib):
    coll = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(coll)
    rn = Ruin(name)
    rn.build_footing(); rn.build_quoins(); rn.build_doorstep(); rn.build_wood(); rn.build_rubble(); rn.plan_plants()
    foot = rn.to_object(coll)
    fol = build_foliage(rn, lib, coll)
    for c in fol.users_collection:
        if c != coll:
            c.objects.unlink(fol)
    if coll not in fol.users_collection:
        coll.objects.link(fol)
    fp = footprint_box(rn.S, coll)
    sock = bpy.data.objects.new('SOCKET_Doorstep', None); coll.objects.link(sock)
    sock.empty_display_type = 'ARROWS'; sock.empty_display_size = 0.3; sock.location = rn.socket
    root = bpy.data.objects.new(name, None); coll.objects.link(root); root.empty_display_type = 'PLAIN_AXES'
    for o in (foot, fol, fp, sock):
        o.parent = root
        o['base'] = o.name.split('.')[0]
        o.name = name + '__' + o['base']
    tris = L.tri_count(foot) + L.tri_count(fol)
    zmax = max(v.co.z for o in (foot, fol) for v in o.data.vertices)
    ext = max(max(abs(v.co.x), abs(v.co.y)) for o in (foot, fol) for v in o.data.vertices)
    inner = rn.h - 1.0
    intr = sum(1 for v in foot.data.vertices if abs(v.co.x) < inner - 0.02 and abs(v.co.y) < inner - 0.02)
    print('VARIANT %s: tris %d (footing %d, foliage %d), max z %.3f, max |xy| %.3f (half %.2f), verts in interior %d'
          % (name, tris, L.tri_count(foot), L.tri_count(fol), zmax, ext, rn.h, intr))
    return dict(name=name, root=root, objs=[root, foot, fol, fp, sock], coll=coll, tris=tris, ruin=rn,
                foot_tris=L.tri_count(foot), fol_tris=L.tri_count(fol), zmax=zmax, ext=ext, intr=intr)

def export(v):
    path = os.path.join(OUT, 'fbx', v['name'] + '.fbx'); os.makedirs(os.path.dirname(path), exist_ok=True)
    kids = [o for o in v['objs'] if 'base' in o]
    for o in kids:
        o.name = o['base']
    bpy.ops.object.select_all(action='DESELECT')
    for o in v['objs']:
        o.select_set(True)
    bpy.context.view_layer.objects.active = v['root']
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True,
                             mesh_smooth_type='OFF', colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
                             use_custom_props=False, use_tspace=True, path_mode='STRIP')
    for o in kids:
        o.name = v['name'] + '__' + o['base']
    dst = os.path.join(EXP, v['name']); os.makedirs(dst, exist_ok=True)
    shutil.copy(path, os.path.join(dst, v['name'] + '.fbx'))
    for suf in ('_BaseColor.png', '_Normal.png', '_ORM.png'):
        shutil.copy(FOLIAGE_PREFIX + suf, os.path.join(dst, 'T_B9_Foliage' + suf))
    print('EXPORT', path, '->', dst)

# ----------------------------------------------------------------------------- review
def stage():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.42, 0.55, 0.75, 1); bg.inputs['Strength'].default_value = 0.9
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); sc.collection.objects.link(sun)
    sun.data.energy = 4.5; sun.data.color = (1.0, 0.93, 0.82); sun.data.angle = math.radians(1.5)
    sun.rotation_euler = (math.radians(38), 0, math.radians(160))
    # packed earth with thin grass: a stand-in for the square's plot ground (the square's ground is another batch)
    m = bpy.data.materials.new('SB_Earth'); m.use_nodes = True; nt = m.node_tree
    bs = nt.nodes['Principled BSDF']; bs.inputs['Roughness'].default_value = 0.95
    nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 0.9; nz.inputs['Detail'].default_value = 8
    ramp = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(nz.outputs[0], ramp.inputs[0])
    ramp.color_ramp.elements[0].position = 0.42; ramp.color_ramp.elements[0].color = (*L.hex2lin('#5d6234'), 1)
    ramp.color_ramp.elements[1].position = 0.62; ramp.color_ramp.elements[1].color = (*L.hex2lin('#56702f'), 1)
    nt.links.new(ramp.outputs[0], bs.inputs['Base Color'])
    bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 0, 0)); g = bpy.context.active_object; g.name = 'SB_Ground'; g.data.materials.append(m)
    return sun

def cam_at(name, eye, at, fov=42):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name)); bpy.context.scene.collection.objects.link(cam)
    cam.data.sensor_fit = 'VERTICAL'; cam.data.angle = math.radians(fov); cam.data.clip_start = 0.05
    e = Vector(eye); t = Vector(at); cam.location = e
    cam.rotation_euler = (t - e).to_track_quat('-Z', 'Y').to_euler()
    return cam

def show_only(vs, keep):
    for v in vs:
        hide = v is not keep
        for o in v['objs']:
            o.hide_render = hide or o.name.endswith('FOOTPRINT')

def review(vs):
    sun = stage()
    sc = bpy.context.scene
    for v in vs:
        show_only(vs, v)
        h = v['ruin'].h
        # far: 20 m away at standing eye height from the front-left (square camera distance); near: 8 m, higher 3/4
        far = cam_at('far', (-12.0, -16.0, 1.75), (0, 0, 0.2), fov=26)
        near = cam_at('near', (-h - 3.2, -h - 4.2, 3.4), (0, 0.4, 0.0), fov=52)
        L.render(far, os.path.join(OUT, v['name'] + '_far.png'), res=RRES, samples=SAMPLES)
        L.render(near, os.path.join(OUT, v['name'] + '_near.png'), res=RRES, samples=SAMPLES)
        sk = v['ruin'].socket
        door = cam_at('door', (sk.x + 1.8, -h - 3.6, 1.5), (sk.x, sk.y, 0.15), fov=50)
        L.render(door, os.path.join(OUT, v['name'] + '_door.png'), res=RRES, samples=SAMPLES)

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    lib = load_b9()
    vs = [build_variant(n, lib) for n in NAMES if n in ONLY]
    if EXPORT:
        for v in vs:
            export(v)
    with open(os.path.join(OUT, 'ruin_stats.txt'), 'w') as f:
        for v in vs:
            f.write('%s tris %d footing %d foliage %d zmax %.3f ext %.3f interior_verts %d socket %s\n' % (
                v['name'], v['tris'], v['foot_tris'], v['fol_tris'], v['zmax'], v['ext'], v['intr'], tuple(round(c, 3) for c in v['ruin'].socket)))
    if RENDER:
        review(vs)

main()
