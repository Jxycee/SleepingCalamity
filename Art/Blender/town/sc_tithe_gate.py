"""TitheGate: the sealed tithe gate, Hollowmere's fixed landmark in the keep terrace wall (Hollowmere mass-production design 2, 3, 5;
STORY 8.1-8.2: the tithe ramp ran down under the bell tower into the old mine-mouth; "the tithe ramp is sealed with a cracked iron gate
that breathes warm air"). Sealed in Phase 1: it never opens.

What it is: a tall, old, two-leaf iron gate (riveted plate leaves, five heavy riveted bands that wrap into strap hinges on pintles,
edge stiles, a lock plate with a keyhole and a drop bar across the meeting edge, a ring pull) in a carved sandstone arch (quoined jambs,
stepped imposts, eleven voussoirs with a proud keystone carved with a wheat sheaf, a gable with coping and a ball finial over the
parapet line, pilasters with pinnacles). The right leaf is sprung: twisted off its hinge line (4 deg at the top), dropped and pulled
away from the meeting edge, so a tapering warm gap shows. A faint warm "breath" glow seeps through the cracks, the sprung gap, the
keyhole and the leaf edges (emissive slot TG_Breath; the builder adds a small warm light at SOCKET_Breath). Soot from the breath
darkens the keystone and gable above the opening. Dressing: three worn steps and a threshold, polished and dished in the middle;
offerings at the foot (clay bowls of grain, one tipped; candle stubs in wax puddles; grain sheaves); tithe tallies carved into the
left jamb and pilaster and chalked on the left leaf; ivy at both pilaster bases, crack weeds and moss on the steps (B9 meshes).

Placement contract (HollowmereLayout row "TitheGate": centre x 5.6, z KeepWallZ 19.6, y 0, footprint 5 x 1.2 m; the keep wall is
1.2 m thick, top 4.9 m incl. parapet, and WallRun leaves a gap from world x 3.0 to 9.0): this piece fills the gap and overlaps the
wall ends. Blender z up, metres; pivot = the wall centre line at ground under the gate centre; the gate's FRONT faces Blender -Y.
Export -Z forward, Y up (front = local +Z in Unity, the project convention), so the builder places it with ArtYaw 180; then world
= (5.6 + bx, by(z up), 19.6 + by). Extents: x -3.5..3.5, y -1.75 (bottom step) .. 0.66, z -0.3..6.9 (finial).

T2: no unique maps. Stone and iron sample the shared B1 tileables (Art/Textures/B1_town) on metre UVs (stone 2 m per tile, carved
blocks 3 m with a per-block offset, iron 1 m) through the B4 kit slot names (FK_Sandstone, FK_ChimneyStone, FK_PlinthJoint,
FK_Iron), so BeautyCornerArt's facade map binds them; placed wear lives in the CORNER colour attribute 'wear' (exported LINEAR, 0-1;
R grime/rust/soot, G moss, B polish) for WearLit. Own slots, made by the builder: TG_Breath (emissive warm amber), TG_Clay, TG_Straw,
TG_Wax, TG_Chalk (flat). Foliage = the B9 atlas (M_B9_Foliage; T_B9_Foliage_* copied beside the FBX).
Children of the root empty TitheGate: Gate (all masonry, iron, glow and offerings), Foliage, SOCKET_Breath (empty at the glow centre,
15 cm in front of the leaves; the builder hangs its spill light 0.6 m below it). Budget: < 22k tris (landmark, T2, under the 30-60k hero band; printed). Round 3: 20,855 (gate 15,387 + foliage 5,468).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_tithe_gate.py -- --out <scratch>
      [--rres 960] [--samples 32] [--no-render] [--export] [--cams cap,near,close,lowsun]
  Renders <out>/TG_<cam>.png: cap = the builder's Cap_HM_TitheGate view (eye 5.2, 1.65, 8.2 -> 5.6, 5.5, 22, fov 55) with stand-ins for
  the keep wall, terrace, bell tower and cistern house; near = 3/4 from the front-left; close = the lock plate and the offerings;
  lowsun = the cap view at low warm sun. --export writes <out>/fbx/TitheGate.fbx and copies it to
  Assets/_Project/Content/Art/Town/Ring1/TitheGate/ with the B9 foliage atlas.

refs: Reference/hollowmere/tithe_gate/REFS.md (studded and banded iron doors and gates in stone arches; CC BY / CC0, study only);
PHASE1 3.1 palette (sandstone warm mid, iron dark, the gold glow as the one warm accent), 3.4 shape language (1.3x voussoirs, nothing
dead straight); docs/art/beauty-corner-brief.md scoring.

Status (2026-10-31, cloud): pass log in Reference/hollowmere/tithe_gate/STATUS.md.
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
CAMS = arg('--cams', 'cap,near,close,lowsun').split(',')
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
TEX = os.path.join(REPO, 'Art', 'Textures')
B9 = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B9')
EXP = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'Ring1', 'TitheGate')
SEED = 0x7177E

# ----------------------------------------------------------------------------- the plan (metres, Blender axes, front -Y)
WALL_F, WALL_B, WALL_TOP = -0.6, 0.6, 4.9      # keep wall faces and top (kh 4 + parapet 0.9)
TERRACE = 4.0
HALF_W = 3.45                                  # outer half width (pilaster outer faces): the wall gap is x -2.6..3.4
PIL_IN = 2.55                                  # pilaster inner face
OPEN_R = 1.7                                   # opening half width = arch radius
SPRING = 3.0                                   # springing height
SILL = 0.45                                    # threshold top
FACE = -0.88                                   # front plane of jambs and voussoirs
GATE_F, GATE_B = -0.245, -0.17                 # leaf plate front / back
GLOW_Y = -0.12                                 # breath card, behind the leaves
BAND_Z = [0.80, 1.56, 2.32, 3.08, 3.80]        # band centres
BAND_H, BAND_P = 0.15, 0.028                   # band height, proud of the plate
HINGE_X = 1.72

def arch_z(x, r=OPEN_R):
    return SPRING + math.sqrt(max(0.0, r * r - x * x))

def sstep(x, a, b):
    t = min(1.0, max(0.0, (x - a) / (b - a))) if b != a else float(x >= a)
    return t * t * (3 - 2 * t)

def R_(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode()))

# ----------------------------------------------------------------------------- materials (the B4 kit's slots + own)
MATS = {}
def _img(path, nonc):
    im = bpy.data.images.load(path, check_existing=True)
    if nonc:
        im.colorspace_settings.name = 'Non-Color'
    return im

def tile_mat(name, folder, tint=(1, 1, 1), normal=1.0):
    """The B4 kit's tile material (sc_facade_kit.tile_mat via sc_ruin_kit): tileable x tint, wear R grime, G moss, B polish (WearLit)."""
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
    gk = N.new('ShaderNodeMath'); gk.operation = 'MULTIPLY'; gk.inputs[1].default_value = 0.85   # SC_GRIME_STRENGTH
    Lk.new(sep.outputs[0], gk.inputs[0]); Lk.new(gk.outputs[0], gri.inputs[0]); Lk.new(tin.outputs[2], gri.inputs[6])
    pol = N.new('ShaderNodeMix'); pol.data_type = 'RGBA'; pol.blend_type = 'MULTIPLY'
    pk = N.new('ShaderNodeMapRange'); Lk.new(sep.outputs[2], pk.inputs[0]); pk.inputs[3].default_value = 1.0; pk.inputs[4].default_value = 1.18
    pc = N.new('ShaderNodeCombineColor'); Lk.new(pk.outputs[0], pc.inputs[0]); Lk.new(pk.outputs[0], pc.inputs[1]); Lk.new(pk.outputs[0], pc.inputs[2])
    pol.inputs[0].default_value = 1.0; Lk.new(gri.outputs[2], pol.inputs[6]); Lk.new(pc.outputs[0], pol.inputs[7])
    mo = N.new('ShaderNodeMix'); mo.data_type = 'RGBA'; mo.inputs[7].default_value = (0.075, 0.115, 0.03, 1)
    mk = N.new('ShaderNodeMath'); mk.operation = 'MULTIPLY'; mk.inputs[1].default_value = 0.8
    Lk.new(sep.outputs[1], mk.inputs[0]); Lk.new(mk.outputs[0], mo.inputs[0]); Lk.new(pol.outputs[2], mo.inputs[6])
    Lk.new(mo.outputs[2], bsdf.inputs['Base Color'])
    so = N.new('ShaderNodeSeparateColor'); Lk.new(orm.outputs[0], so.inputs[0])
    ra = N.new('ShaderNodeMath'); ra.operation = 'MULTIPLY_ADD'; ra.use_clamp = True
    Lk.new(sep.outputs[0], ra.inputs[0]); ra.inputs[1].default_value = 0.35; Lk.new(so.outputs[1], ra.inputs[2])
    Lk.new(ra.outputs[0], bsdf.inputs['Roughness']); Lk.new(so.outputs[2], bsdf.inputs['Metallic'])
    nm = N.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = normal
    Lk.new(nrm.outputs[0], nm.inputs['Color']); Lk.new(nm.outputs[0], bsdf.inputs['Normal'])
    MATS[name] = m
    return m

def glow_mat(name, col, strength):
    m = bpy.data.materials.new(name); m.use_nodes = True
    bs = m.node_tree.nodes['Principled BSDF']
    bs.inputs['Base Color'].default_value = (0.02, 0.012, 0.006, 1); bs.inputs['Roughness'].default_value = 0.9
    bs.inputs['Emission Color'].default_value = (*col, 1); bs.inputs['Emission Strength'].default_value = strength
    return m

# Glow colour and strength: shared with the builder (TitheGateArt.BreathColor / BreathIntensity), linear.
BREATH = (1.0, 0.42, 0.10)
BREATH_STRENGTH = float(arg('--breath', '1.1'))
BREATH_LIGHT_W = 25.0     # preview of the builder's spill light (Unity: point, range 3.5 m)

SLOTS = ['stone', 'stone_dk', 'groove', 'iron', 'breath', 'clay', 'straw', 'wax', 'chalk']
def slot_mats():
    return [tile_mat('FK_Sandstone', 'B1_town/sandstone'),
            tile_mat('FK_ChimneyStone', 'B1_town/sandstone', tint=(0.80, 0.74, 0.68)),
            tile_mat('FK_PlinthJoint', 'B1_town/sandstone', tint=(0.30, 0.28, 0.24)),
            tile_mat('FK_Iron', 'B1_town/iron', normal=1.0),
            glow_mat('TG_Breath', BREATH, BREATH_STRENGTH),
            L.flat_material('TG_Clay', '#8a4a30', rough=0.8),
            L.flat_material('TG_Straw', '#b89a52', rough=0.9),
            L.flat_material('TG_Wax', '#d8cdb0', rough=0.55),
            L.flat_material('TG_Chalk', '#ddd6c4', rough=0.95)]

# ----------------------------------------------------------------------------- geometry accumulator
class Geo:
    """Verts + faces with per-face slot, kind (wear rule), part id (per-block noise, UV offset) and smooth flag."""
    def __init__(self):
        self.v = []; self.f = []; self.slot = []; self.kind = []; self.pid = []; self.smooth = []; self.uvs = []; self.np = 0
        self.post = []          # (vertex range, fn) deformations applied in build
    def add(self, verts, faces, slot, kind=None, smooth=False, uvs=3.0):
        o = len(self.v); self.v += [Vector(p) for p in verts]
        for f in faces:
            self.f.append(tuple(i + o for i in f)); self.slot.append(SLOTS.index(slot)); self.kind.append(kind or slot)
            self.pid.append(self.np); self.smooth.append(smooth); self.uvs.append(uvs)
        self.np += 1
        return (o, len(self.v))
    def add_bm(self, bm, xf, slot, kind=None, smooth=False, uvs=3.0):
        bm.verts.ensure_lookup_table(); bm.verts.index_update()
        verts = [xf @ v.co for v in bm.verts]
        faces = [tuple(v.index for v in f.verts) for f in bm.faces]
        bm.free()
        return self.add(verts, faces, slot, kind, smooth, uvs)
    def deform(self, rng, fn):
        for i in range(*rng):
            self.v[i] = fn(self.v[i])
    def tris(self):
        return sum(len(f) - 2 for f in self.f)

G = Geo()

def bm_box(sx, sy, sz, bevel=0.0, wob=0.0, rng=None, cuts=0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=min(bevel, 0.45 * min(sx, sy, sz)), segments=1, affect='EDGES', profile=0.5)
    if wob and rng:
        for v in bm.verts:
            v.co += Vector((rng.uniform(-wob, wob), rng.uniform(-wob, wob), rng.uniform(-wob, wob)))
    return bm

def box(c, s, slot, kind=None, bevel=0.03, rot=(0, 0, 0), wob=0.0, rng=None, uvs=3.0, cuts=0):
    """Box centred c, size s, rotated rot (radians, XYZ) about its centre."""
    bm = bm_box(*s, bevel=bevel, wob=wob, rng=rng, cuts=cuts)
    xf = Matrix.Translation(c) @ Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y') @ Matrix.Rotation(rot[0], 4, 'X')
    return G.add_bm(bm, xf, slot, kind, uvs=uvs)

def bbox(x0, x1, y0, y1, z0, z1, slot, kind=None, bevel=0.03, **kw):
    return box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), slot, kind, bevel, **kw)

def prism_xz(poly, y0, y1, slot, kind=None, bevel=0.0, uvs=3.0, smooth=False):
    """Extrude a convex-or-simple polygon in the xz plane (counter-clockwise seen from -Y) from y0 (front) to y1 (back)."""
    bm = bmesh.new()
    fr = [bm.verts.new((x, y0, z)) for x, z in poly]
    bk = [bm.verts.new((x, y1, z)) for x, z in poly]
    n = len(poly)
    bm.faces.new(fr)
    bm.faces.new(list(reversed(bk)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((fr[j], fr[i], bk[i], bk[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, segments=1, affect='EDGES', profile=0.5, clamp_overlap=True)
    return G.add_bm(bm, Matrix(), slot, kind, smooth, uvs)

def lathe(profile, segs, xf, slot, kind=None, cap_top=True):
    """profile: [(r, z)] bottom to top; first point closes with a bottom cap, last with a top cap if cap_top."""
    verts = []; faces = []
    for (r, z) in profile:
        for s in range(segs):
            a = 2 * math.pi * s / segs
            verts.append((r * math.cos(a), r * math.sin(a), z))
    for k in range(len(profile) - 1):
        for s in range(segs):
            a = k * segs + s; b = k * segs + (s + 1) % segs
            faces.append((a, b, b + segs, a + segs))
    faces.append(tuple(reversed(range(segs))))
    if cap_top:
        top = (len(profile) - 1) * segs
        faces.append(tuple(top + s for s in range(segs)))
    return G.add([xf @ Vector(v) for v in verts], faces, slot, kind, smooth=True, uvs=1.0)

def ribbon(pts, widths, slot, kind=None, nrm=Vector((0, -1, 0))):
    """A flat strip along pts (Vectors) in the plane facing nrm, width per point."""
    verts = []; faces = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        side = t.cross(nrm).normalized() * widths[i] * 0.5
        verts += [p - side, p + side]
    for i in range(len(pts) - 1):
        faces.append((2 * i, 2 * i + 2, 2 * i + 3, 2 * i + 1))
    return G.add(verts, faces, slot, kind, uvs=1.0)

def rivet(x, y, z, r=0.022, h=0.018, slot='iron', axis='Y'):
    """A low dome (6-sided pyramid with a flat crown) proud of a plate facing -Y."""
    verts = []; faces = []
    for s in range(6):
        a = 2 * math.pi * s / 6 + 0.3
        verts.append((x + r * math.cos(a), y, z + r * math.sin(a)))
    for s in range(6):
        a = 2 * math.pi * s / 6 + 0.3
        verts.append((x + 0.5 * r * math.cos(a), y - h, z + 0.5 * r * math.sin(a)))
    for s in range(6):
        t = (s + 1) % 6
        faces.append((s, t, t + 6, s + 6))
    faces.append(tuple(range(11, 5, -1)))
    return G.add(verts, faces, slot, 'iron', smooth=False, uvs=1.0)

# ----------------------------------------------------------------------------- masonry
def build_masonry():
    R = R_('masonry')
    # wall fill between the pilasters: front face as vertical strips around the (hidden) opening outline, top, parapet back face
    xs = [-PIL_IN + i * (2 * PIL_IN) / 34 for i in range(35)]
    verts = []; faces = []
    hole_r = 1.98
    def v(p):
        verts.append(p); return len(verts) - 1
    for i in range(len(xs) - 1):
        x0, x1 = xs[i], xs[i + 1]
        xm = 0.5 * (x0 + x1)
        if abs(xm) < hole_r:
            z0a, z0b = arch_z(x0, hole_r), arch_z(x1, hole_r)
            faces.append((v((x0, WALL_F, -0.3)), v((x1, WALL_F, -0.3)), v((x1, WALL_F, 0.32)), v((x0, WALL_F, 0.32))))
            faces.append((v((x0, WALL_F, z0a)), v((x1, WALL_F, z0b)), v((x1, WALL_F, WALL_TOP)), v((x0, WALL_F, WALL_TOP))))
        else:
            faces.append((v((x0, WALL_F, -0.3)), v((x1, WALL_F, -0.3)), v((x1, WALL_F, WALL_TOP)), v((x0, WALL_F, WALL_TOP))))
    faces.append((v((-PIL_IN, WALL_F, WALL_TOP)), v((PIL_IN, WALL_F, WALL_TOP)), v((PIL_IN, WALL_B, WALL_TOP)), v((-PIL_IN, WALL_B, WALL_TOP))))
    faces.append((v((PIL_IN, WALL_B, TERRACE - 0.2)), v((-PIL_IN, WALL_B, TERRACE - 0.2)), v((-PIL_IN, WALL_B, WALL_TOP)), v((PIL_IN, WALL_B, WALL_TOP))))
    G.add(verts, faces, 'stone', 'fill', uvs=2.0)
    # pilasters with a base course, a cap and a stepped pinnacle
    for sgn in (-1, 1):
        cx = sgn * (PIL_IN + HALF_W) / 2; w = HALF_W - PIL_IN
        rr = R_('pil%d' % sgn)
        box((cx, (-0.82 + WALL_B) / 2, (-0.3 + 5.25) / 2), (w, WALL_B + 0.82, 5.55), 'stone', 'block', bevel=0.035, rot=(0, rr.uniform(-0.006, 0.006), 0), uvs=2.0)
        box((cx, (-0.97 + WALL_B) / 2, (-0.3 + 0.62) / 2), (w + 0.16, WALL_B + 0.97, 0.92), 'stone_dk', 'plinth', bevel=0.06, uvs=3.0)
        box((cx, (-0.92 + WALL_B + 0.05) / 2, 5.25 + 0.11), (w + 0.16, WALL_B + 0.97, 0.22), 'stone', 'carve', bevel=0.05, uvs=3.0)
        box((cx, -0.12, 5.47 + 0.16), (w - 0.18, 0.62, 0.32), 'stone', 'carve', bevel=0.04, rot=(0, 0, rr.uniform(-0.03, 0.03)), uvs=3.0)
        # pyramid cap
        b = (w - 0.3) / 2; zb = 5.79; zt = 6.32
        pv = [(cx - b, -0.12 - 0.26, zb), (cx + b, -0.12 - 0.26, zb), (cx + b, -0.12 + 0.26, zb), (cx - b, -0.12 + 0.26, zb), (cx + rr.uniform(-0.02, 0.02), -0.12, zt)]
        G.add(pv, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (3, 2, 1, 0)], 'stone', 'carve', uvs=3.0)
    # quoined jamb blocks from the sill to the impost, alternating long and short
    heights = [0.62, 0.56, 0.6, 0.55]
    for sgn in (-1, 1):
        z = SILL
        for k, h in enumerate(heights):
            rr = R_('jamb%d_%d' % (sgn, k))
            outer = OPEN_R + (0.66 if k % 2 == 0 else 0.46) + rr.uniform(-0.03, 0.03)
            x0, x1 = sorted((sgn * OPEN_R, sgn * outer))
            bbox(x0, x1, FACE + rr.uniform(-0.015, 0.01), -0.19, z + 0.006, z + h - 0.006, 'stone', 'block', bevel=0.035,
                 wob=0.006, rng=rr, rot=(rr.uniform(-0.006, 0.006), 0, rr.uniform(-0.01, 0.01)))
            z += h
        # impost: a proud top slab over a set-back lower slab (stepped, carved profile)
        x0, x1 = sorted((sgn * (OPEN_R - 0.09), sgn * (OPEN_R + 0.86)))
        bbox(x0, x1, FACE - 0.11, -0.19, SPRING - 0.11, SPRING, 'stone', 'carve', bevel=0.03)
        x0, x1 = sorted((sgn * (OPEN_R - 0.04), sgn * (OPEN_R + 0.78)))
        bbox(x0, x1, FACE - 0.05, -0.19, SPRING - 0.24, SPRING - 0.11, 'stone', 'carve', bevel=0.035)
    # voussoirs: eleven wedges, alternating extrados, keystone proud and tall
    n = 11
    for i in range(n):
        rr = R_('vous%d' % i)
        a0 = math.pi * (1 - i / n) - 0.006; a1 = math.pi * (1 - (i + 1) / n) + 0.006
        key = i == n // 2
        ro = 2.68 if key else (2.36 if i % 2 else 2.5) + rr.uniform(-0.03, 0.03)
        y0 = FACE - (0.1 if key else rr.uniform(-0.01, 0.02))
        poly = []
        for t in (0.0, 0.5, 1.0):
            a = a0 + (a1 - a0) * t
            poly.append((OPEN_R * math.cos(a), SPRING + OPEN_R * math.sin(a)))
        if key:
            poly += [(0.36, SPRING + ro), (-0.36, SPRING + ro)]
        else:
            poly += [(ro * math.cos(a1), SPRING + ro * math.sin(a1)), (ro * math.cos(a0), SPRING + ro * math.sin(a0))]
        prism_xz(poly, y0, -0.19, 'stone', 'block', bevel=0.03, uvs=3.0)
    # keystone relief: a bound wheat sheaf (stalks fanned from a tie, ears at the top)
    kz = SPRING + (OPEN_R + 2.68) / 2 + 0.05; ky = FACE - 0.1
    for j in range(7):
        a = (j - 3) * 0.13
        L_ = 0.42 - abs(j - 3) * 0.03
        c = Vector((math.sin(a) * L_ * 0.5, ky - 0.012, kz - 0.06 + math.cos(a) * L_ * 0.5 - 0.1))
        box(c, (0.026, 0.024, L_), 'stone', 'carve', bevel=0.0, rot=(0, a, 0))
        e = Vector((math.sin(a) * (L_ + 0.04), ky - 0.016, kz - 0.06 + math.cos(a) * (L_ + 0.04) - 0.1))
        box(e, (0.05, 0.032, 0.13), 'stone', 'carve', bevel=0.01, rot=(0, a, 0))
    box((0, ky - 0.018, kz - 0.06), (0.15, 0.036, 0.05), 'stone', 'carve', bevel=0.008)
    # gable over the arch, coping on the rakes, a ball finial
    apex = 6.55
    prism_xz([(-PIL_IN, WALL_TOP - 0.05), (PIL_IN, WALL_TOP - 0.05), (0.0, apex)], -0.64, WALL_B, 'stone', 'fill', bevel=0.0, uvs=2.0)
    ang = math.atan2(apex - WALL_TOP, PIL_IN)
    for sgn in (-1, 1):
        L_ = math.hypot(PIL_IN, apex - WALL_TOP) + 0.18
        c = Vector((sgn * PIL_IN / 2, -0.03, (WALL_TOP + apex) / 2 + 0.13))
        box(c, (L_, 1.38, 0.2), 'stone', 'carve', bevel=0.04, rot=(0, sgn * ang, 0))
    box((0, -0.03, apex + 0.2), (0.42, 0.5, 0.2), 'stone', 'carve', bevel=0.04)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=7, radius=0.2)
    G.add_bm(bm, Matrix.Translation((0, -0.03, apex + 0.47)), 'stone', 'carve', smooth=True, uvs=3.0)
    # threshold and steps: soft, dished in the middle where feet went
    bbox(-1.98, 1.98, -0.98, -0.12, -0.3, SILL, 'stone', 'step', bevel=0.04, cuts=3)
    steps = [(0.30, -1.36, -0.98, 2.62, [-0.95, 0.82]), (0.15, -1.75, -1.36, 2.98, [-1.7, -0.3, 1.05])]
    for k, (top, y0, y1, hw, joints) in enumerate(steps):
        edges = [-hw] + joints + [hw]
        for j in range(len(edges) - 1):
            rr = R_('step%d_%d' % (k, j))
            x0, x1 = edges[j] + 0.008, edges[j + 1] - 0.008
            sink = 0.04 if (k == 1 and j == len(edges) - 2) else 0.0
            tilt = 0.05 if sink else rr.uniform(-0.01, 0.01)
            box(((x0 + x1) / 2, (y0 + y1) / 2 + rr.uniform(-0.01, 0.01), (top - 0.3) / 2 - sink), (x1 - x0, y1 - y0, top + 0.3),
                'stone', 'step', bevel=0.045, rot=(tilt, rr.uniform(-0.01, 0.01), rr.uniform(-0.012, 0.012)), wob=0.006, rng=rr, cuts=3)

# ----------------------------------------------------------------------------- the iron gate
def leaf_top(x):
    return arch_z(x) - 0.018

def build_leaf(side):
    """side -1 = left leaf (x -1.69..-0.005), +1 = right (sprung). Returns the vertex ranges to deform."""
    rngs = []
    xa, xb = (-1.69, -0.002) if side < 0 else (0.002, 1.69)
    zb = SILL + 0.02
    # plate: vertical strips so the top follows the arch
    ncol = 12
    xs = [xa + (xb - xa) * i / ncol for i in range(ncol + 1)]
    verts = []; faces = []
    for x in xs:
        zt = leaf_top(x)
        verts += [(x, GATE_F, zb), (x, GATE_F, zt), (x, GATE_B, zb), (x, GATE_B, zt)]
    for i in range(ncol):
        a, b = 4 * i, 4 * (i + 1)
        faces.append((a, b, b + 1, a + 1))                   # front
        faces.append((a + 2, a + 3, b + 3, b + 2))           # back
        faces.append((a + 1, b + 1, b + 3, a + 3))           # top
        faces.append((a, a + 2, b + 2, b))                   # bottom
    faces.append((0, 1, 3, 2)); e = 4 * ncol
    faces.append((e, e + 2, e + 3, e + 1))
    rngs.append(G.add(verts, faces, 'iron', 'iron', uvs=1.0))
    # edge stiles and two plate seams with rivet rows
    stile_x = [xa + 0.05, xb - 0.05] if side < 0 else [xa + 0.05, xb - 0.05]
    for sx in stile_x:
        zt = leaf_top(sx) - 0.02
        rngs.append(bbox(sx - 0.05, sx + 0.05, GATE_F - 0.02, GATE_F + 0.004, zb + 0.01, zt, 'iron', 'iron', bevel=0.008, uvs=1.0))
    for t in (1 / 3, 2 / 3):
        sx = xa + (xb - xa) * t
        zt = leaf_top(sx) - 0.04
        rngs.append(bbox(sx - 0.035, sx + 0.035, GATE_F - 0.01, GATE_F + 0.004, zb + 0.03, zt, 'iron', 'iron', bevel=0.005, uvs=1.0))
        z = zb + 0.18
        while z < zt - 0.1:
            if all(abs(z - bz) > BAND_H * 0.7 for bz in BAND_Z):
                rngs.append(rivet(sx, GATE_F - 0.01, z, r=0.014, h=0.01))
            z += 0.24
    # bands: wrap into a strap hinge on the hinge side, rivets along, arrow-cut tip at the meeting side
    for bi, bz in enumerate(BAND_Z):
        xl = math.sqrt(max(0.0, OPEN_R ** 2 - (bz + BAND_H / 2 + 0.04 - SPRING) ** 2)) if bz + BAND_H / 2 > SPRING else OPEN_R
        x_hinge = side * min(1.69, xl - 0.04)
        x_meet = side * 0.03
        x0, x1 = sorted((x_hinge, x_meet))
        rr = R_('band%d_%d' % (side, bi))
        rngs.append(bbox(x0, x1, GATE_F - BAND_P, GATE_F + 0.004, bz - BAND_H / 2, bz + BAND_H / 2, 'iron', 'iron', bevel=0.01,
                         rot=(0, rr.uniform(-0.004, 0.004), 0), uvs=1.0))
        n = max(3, int(abs(x1 - x0) / 0.19))
        for k in range(n):
            x = x0 + 0.08 + (abs(x1 - x0) - 0.16) * k / (n - 1)
            for dz in (-0.04, 0.04):
                rngs.append(rivet(x, GATE_F - BAND_P, bz + dz, r=0.019, h=0.016))
        # hinge knuckle on the jamb side (only below the arch)
        if bz < SPRING:
            hx = side * HINGE_X
            bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.055, radius2=0.055, depth=0.2)
            G.add_bm(bm, Matrix.Translation((hx, GATE_F + 0.01, bz)), 'iron', 'iron', smooth=True, uvs=1.0)
            x0p, x1p = sorted((side * (OPEN_R + 0.005), side * (OPEN_R + 0.1)))
            bbox(x0p, x1p, GATE_F - 0.03, GATE_F + 0.05, bz - 0.13, bz - 0.08, 'iron', 'iron', bevel=0.006, uvs=1.0)
    return rngs

def build_lock(rngs_left, rngs_right):
    # lock plate on the left leaf, over the second band
    rngs_left.append(bbox(-0.47, -0.02, GATE_F - 0.05, GATE_F - 0.02, 1.2, 1.8, 'iron', 'lock', bevel=0.012, uvs=1.0))
    for x in (-0.42, -0.07):
        for z in (1.25, 1.75):
            rngs_left.append(rivet(x, GATE_F - 0.05, z, r=0.026, h=0.02))
    # keyhole (glowing): a round head and a tapered slot
    kx, kz, ky = -0.245, 1.44, GATE_F - 0.0505
    verts = [(kx + 0.032 * math.cos(2 * math.pi * s / 8), ky, kz + 0.032 * math.sin(2 * math.pi * s / 8)) for s in range(8)]
    rngs_left.append(G.add(verts, [tuple(reversed(range(8)))], 'breath', 'breath', uvs=1.0))
    rngs_left.append(G.add([(kx - 0.014, ky, kz - 0.01), (kx + 0.014, ky, kz - 0.01), (kx + 0.024, ky, kz - 0.12), (kx - 0.024, ky, kz - 0.12)],
                           [(3, 2, 1, 0)], 'breath', 'breath', uvs=1.0))
    # drop bar across the meeting edge in two staples
    rngs_left.append(bbox(-0.62, 0.58, GATE_F - 0.105, GATE_F - 0.055, 1.9, 1.99, 'iron', 'lock', bevel=0.012, rot=(0, -0.012, 0), uvs=1.0))
    for x, lst in ((-0.5, rngs_left), (0.46, rngs_right)):
        lst.append(bbox(x - 0.05, x + 0.05, GATE_F - 0.12, GATE_F, 1.86, 1.885, 'iron', 'iron', bevel=0.004, uvs=1.0))
        lst.append(bbox(x - 0.05, x + 0.05, GATE_F - 0.12, GATE_F, 2.005, 2.03, 'iron', 'iron', bevel=0.004, uvs=1.0))
        lst.append(bbox(x - 0.05, x + 0.05, GATE_F - 0.13, GATE_F - 0.105, 1.86, 2.03, 'iron', 'iron', bevel=0.004, uvs=1.0))
    # ring pull on the right leaf, hanging from a boss
    rngs_right.append(rivet(0.34, GATE_F, 1.36, r=0.05, h=0.04))
    verts = []; faces = []; S, s2 = 10, 5
    for i in range(S):
        a = 2 * math.pi * i / S
        cpos = Vector((0.13 * math.sin(a), 0, -0.13 * math.cos(a) - 0.13))
        rad = Vector((math.sin(a), 0, -math.cos(a)))
        for j in range(s2):
            b = 2 * math.pi * j / s2
            verts.append(cpos + rad * 0.018 * math.cos(b) + Vector((0, 0.018 * math.sin(b), 0)))
    for i in range(S):
        for j in range(s2):
            a = i * s2 + j; b = i * s2 + (j + 1) % s2; c = ((i + 1) % S) * s2 + (j + 1) % s2; d = ((i + 1) % S) * s2 + j
            faces.append((a, d, c, b))
    xf = Matrix.Translation((0.34, GATE_F - 0.06, 1.36)) @ Matrix.Rotation(0.22, 4, 'X') @ Matrix.Rotation(0.08, 4, 'Y')
    rngs_right.append(G.add([xf @ v for v in verts], faces, 'iron', 'lock', smooth=True, uvs=1.0))

def crack(pts2, side, rngs, w0=0.026):
    """A jagged crack in a leaf: a dark rust lip and a glowing core, laid on the plate (or on a band where it crosses one)."""
    rr = R_('crack%.2f%.2f' % pts2[0])
    pts = []
    for i in range(len(pts2) - 1):
        a, b = Vector(pts2[i]), Vector(pts2[i + 1])
        for k in range(3):
            t = k / 3
            p = a.lerp(b, t)
            if 0 < i + k:
                p += Vector((rr.uniform(-0.02, 0.02), rr.uniform(-0.02, 0.02)))
            pts.append(p)
    pts.append(Vector(pts2[-1]))
    def y_at(z):
        return GATE_F - (BAND_P if any(abs(z - bz) < BAND_H / 2 + 0.005 for bz in BAND_Z) else 0.0)
    n = len(pts)
    taper = [math.sin(math.pi * (0.08 + 0.84 * i / (n - 1))) for i in range(n)]
    lip = [Vector((p.x, y_at(p.y) - 0.0025, p.y)) for p in pts]
    core = [Vector((p.x, y_at(p.y) - 0.0045, p.y)) for p in pts]
    rngs.append(ribbon(lip, [w0 * 2.6 * t + 0.01 for t in taper], 'iron', 'rust'))
    rngs.append(ribbon(core, [0.55 * w0 * t * rr.uniform(0.6, 1.1) + 0.003 for t in taper], 'breath', 'breath'))

def build_gate():
    left = build_leaf(-1); right = build_leaf(1)
    build_lock(left, right)
    # cracks: left leaf from the lock plate up and out, and low from the foot; right leaf from the meeting edge and across a band
    crack([(-0.48, 1.42), (-0.66, 1.66), (-0.8, 1.9), (-1.02, 2.12), (-1.12, 2.4)], -1, left, 0.03)
    crack([(-1.25, 0.49), (-1.18, 0.62), (-1.02, 0.7), (-0.94, 0.86)], -1, left, 0.022)
    crack([(0.04, 2.55), (0.22, 2.7), (0.44, 2.78), (0.7, 3.0)], 1, right, 0.028)
    crack([(1.05, 1.22), (1.15, 1.42), (1.12, 1.62), (1.3, 1.95), (1.36, 2.12)], 1, right, 0.022)
    crack([(0.2, 3.5), (0.36, 3.72), (0.3, 3.95), (0.42, 4.25)], 1, right, 0.018)
    # chalked tithe tallies on the left leaf, low, between the first two bands (pale on dark iron)
    rr = R_('chalk')
    for row, z in enumerate((0.98, 1.18, 1.36)):
        x = -1.52
        for g in range(4 if row < 2 else 2):
            for k in range(4):
                xx = x + k * 0.03
                left.append(ribbon([Vector((xx, GATE_F - 0.002, z - 0.05)), Vector((xx + rr.uniform(-0.006, 0.006), GATE_F - 0.002, z + 0.05))],
                                   [0.009, 0.009], 'chalk', 'chalk'))
            left.append(ribbon([Vector((x - 0.02, GATE_F - 0.0025, z - 0.04)), Vector((x + 0.11, GATE_F - 0.0025, z + 0.045))], [0.009, 0.009], 'chalk', 'chalk'))
            x += 0.17
    # the right leaf is sprung: pulled off the meeting edge, dropped and twisted out of the hinge line (more at the top)
    hx, hy = HINGE_X, GATE_F + 0.01
    def sprung(p):
        tz = (p.z - SILL) / (arch_z(0) - SILL)
        x = hx + (p.x - hx) * (1.0 - 0.02 * tz ** 1.6)                     # warped narrower: a gap at the meeting edge, widest at the top
        ph = math.radians(1.2 + 3.0 * tz)
        dx, dy = x - hx, p.y - hy
        x2 = hx + dx * math.cos(ph) - dy * math.sin(ph)
        y2 = hy + dx * math.sin(ph) + dy * math.cos(ph)
        z2 = p.z - 0.022 * (hx - x) / hx                              # dropped at the meeting side
        return Vector((x2, y2, z2))
    for rg in right:
        G.deform(rg, sprung)
    # breath card behind the leaves, the shape of the opening
    poly = [(-OPEN_R, SILL - 0.01)] + [(OPEN_R * math.cos(math.pi * (1 - k / 16)), SPRING + OPEN_R * math.sin(math.pi * (1 - k / 16))) for k in range(17)] + [(OPEN_R, SILL - 0.01)]
    poly[1] = (-OPEN_R, SPRING)
    verts = [(x, GLOW_Y, z) for x, z in poly]
    G.add(verts, [tuple(range(len(verts)))], 'breath', 'breath', uvs=1.0)
    # the reveal behind the leaves: a dark lining so the glow only shows at the gaps
    for sgn in (-1, 1):
        x0, x1 = sorted((sgn * (OPEN_R - 0.002), sgn * (OPEN_R + 0.3)))
        bbox(x0, x1, -0.2, -0.1, SILL - 0.02, SPRING, 'stone_dk', 'groove', bevel=0.0)

# ----------------------------------------------------------------------------- tallies carved in the stone
def build_tallies():
    rr = R_('tally')
    def groups(x0, z0, rows, per_row, y):
        for r in range(rows):
            x = x0; z = z0 - r * 0.15
            for g in range(per_row - (1 if r == rows - 1 else 0)):
                for k in range(4):
                    xx = x + k * 0.028
                    ribbon([Vector((xx, y, z - 0.045)), Vector((xx + rr.uniform(-0.005, 0.005), y, z + 0.045 + rr.uniform(-0.01, 0.01)))], [0.011, 0.011], 'groove', 'groove')
                ribbon([Vector((x - 0.02, y - 0.0005, z - 0.035)), Vector((x + 0.105, y - 0.0005, z + 0.04))], [0.011, 0.011], 'groove', 'groove')
                x += 0.155
    groups(-3.36, 2.2, 7, 4, -0.8235)          # left pilaster face: years of tithes
    groups(-2.26, 1.6, 4, 3, FACE - 0.008)     # left jamb block face

# ----------------------------------------------------------------------------- offerings
def build_offerings():
    def bowl(x, y, z, r, tip=0.0, yaw=0.0, grain=True):
        xf = Matrix.Translation((x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(tip, 4, 'X')
        prof = [(r * 0.55, 0.0), (r * 0.8, 0.012), (r, r * 0.45), (r * 0.93, r * 0.5), (r * 0.82, r * 0.18)]
        lathe(prof, 12, xf, 'clay', 'flat', cap_top=True)
        if grain:
            lathe([(r * 0.86, r * 0.3), (r * 0.5, r * 0.42), (r * 0.05, r * 0.45)], 10, xf, 'straw', 'flat', cap_top=True)
    bowl(-1.35, -1.12, 0.30, 0.12, yaw=0.4)
    bowl(-0.95, -1.22, 0.30, 0.09, yaw=1.1)
    bowl(1.55, -1.5, 0.15 + 0.035, 0.11, tip=1.1, yaw=2.6, grain=False)          # tipped over, spilled
    lathe([(0.12, 0.0), (0.07, 0.006), (0.0, 0.008)], 10, Matrix.Translation((1.55, -1.43, 0.152)) @ Matrix.Diagonal((1.6, 0.8, 1, 1)), 'straw', 'flat')
    def candle(x, y, z, r, h):
        lathe([(r * 2.4, 0.0), (r * 1.6, 0.006), (r * 1.05, 0.012), (r, h * 0.8), (r * 0.8, h)], 8, Matrix.Translation((x, y, z)), 'wax', 'flat')
        box((x, y, z + h + 0.008), (0.004, 0.004, 0.016), 'groove', 'flat', bevel=0.0)
    for (x, y, r, h) in ((0.95, -1.12, 0.032, 0.11), (1.08, -1.2, 0.026, 0.06), (1.19, -1.1, 0.038, 0.15), (-1.62, -0.62, 0.03, 0.08)):
        candle(x, y, 0.30 if y < -0.98 else SILL, r, h)
    def sheaf(xf, L_=0.55):
        lathe([(0.045, 0.0), (0.05, L_ * 0.4), (0.032, L_ * 0.55), (0.06, L_ * 0.85), (0.04, L_)], 9, xf, 'straw', 'flat')
        lathe([(0.036, L_ * 0.5), (0.036, L_ * 0.58)], 9, xf, 'groove', 'flat', cap_top=False)
    sheaf(Matrix.Translation((-2.1, -1.55, 0.2)) @ Matrix.Rotation(0.5, 4, 'Z') @ Matrix.Rotation(math.pi / 2, 4, 'Y'), 0.6)
    sheaf(Matrix.Translation((-2.72, -1.08, 0.0)) @ Matrix.Rotation(-0.22, 4, 'X') @ Matrix.Rotation(0.18, 4, 'Y'), 0.62)

# ----------------------------------------------------------------------------- build, UVs and wear
def wear_at(p, n, kind, pv, nofs):
    nz = noise.noise(p * 2.1 + nofs)
    streak = noise.noise(Vector((p.x * 5.0, p.y * 5.0, p.z * 0.7)) + nofs)
    r = g = b = 0.0
    if kind in ('fill', 'block', 'carve', 'plinth', 'step'):
        r = max(0.0, 1 - p.z / (0.45 + 0.12 * nz + 0.08 * streak)) * 0.85                     # damp and grime at the foot
        if n.z < 0.3:
            r = max(r, (0.45 + 0.2 * streak) * max(0.0, 1 - p.z / 0.9))
        r = max(r, 0.12 + 0.35 * pv ** 1.4 + 0.08 * nz)                                          # block-to-block value
        soot = math.exp(-(p.x / 1.15) ** 2) * sstep(p.z, SPRING + 0.6, SPRING + 1.6) * (0.8 + 0.2 * streak)
        if p.y < -0.5 and p.z < 6.2:
            r = max(r, 0.9 * soot)                                                            # the breath's soot plume above the opening
        if abs(p.x) < OPEN_R + 0.75 and p.z < SILL + 0.1 and p.y > -1.0:
            r = max(r, 0.4 * (0.6 + 0.4 * streak))                                             # rust run-off at the threshold
        if n.z > 0.55:
            g = max(0.0, 0.25 + 0.9 * nz) * (0.9 if kind in ('carve', 'plinth') else 0.55)     # moss on tops, copings, the plinth
        if p.z < 0.7 and abs(p.x) > 2.3:
            g = max(g, (0.6 + 0.5 * nz) * (1 - p.z / 0.7))                                     # moss at the pilaster feet
        if kind == 'step':
            foot = math.exp(-(p.x / 1.05) ** 2)
            if n.z > 0.5:
                b = 0.95 * foot; g *= (1 - foot); r *= (1 - 0.6 * foot)
            elif n.y < -0.5 and p.z > 0.0:
                b = 0.5 * foot
            if abs(p.x) > 2.0:
                g = max(g, 0.5 + 0.4 * nz) if n.z > 0.4 else g
    elif kind in ('iron', 'lock', 'rust'):
        r = 0.12 + 0.14 * nz + 0.4 * sstep(1.0 - p.z, 0.0, 0.5) + 0.25 * max(0.0, streak) * (1.0 if n.y < -0.5 else 0.3)
        if kind == 'rust':
            r = 0.95
        if kind == 'lock':
            r = 0.3 + 0.12 * nz; b = 0.55
        if abs(p.x) < 0.5 and 1.0 < p.z < 2.0 and kind == 'iron':
            b = 0.45 * (1 - abs(p.x) / 0.5); r *= 0.7                                           # hands on the meeting edge
    elif kind == 'groove':
        r = 0.55
    elif kind == 'chalk':
        r = 0.05
    else:
        return (0.0, 0.0, 0.0, 1.0)
    return (min(0.95, max(0.0, r)), min(0.95, max(0.0, g)), min(0.95, max(0.0, b)), 1.0)

def to_object(coll):
    # worn dip: lower the top verts of step parts near the middle, most at the front edge
    step_verts = set()
    for fi, f in enumerate(G.f):
        if G.kind[fi] == 'step':
            step_verts.update(f)
    tops = {}
    for vi in step_verts:
        p = G.v[vi]
        for top in (SILL, 0.30, 0.15):
            if abs(p.z - top) < 0.03:
                tops[vi] = top
    for vi, top in tops.items():
        p = G.v[vi]
        d = 0.035 * math.exp(-(p.x / 0.85) ** 2)
        G.v[vi] = Vector((p.x, p.y, p.z - d))
    me = bpy.data.meshes.new('Gate'); me.from_pydata([tuple(v) for v in G.v], [], G.f)
    me.polygons.foreach_set('material_index', G.slot)
    me.polygons.foreach_set('use_smooth', G.smooth)
    for m in slot_mats():
        me.materials.append(m)
    me.validate(clean_customdata=False)
    nl = len(me.loops); UV = np.zeros((nl, 2), np.float32); WC = np.ones((nl, 4), np.float32)
    nofs = Vector((13.1, 7.7, 0.0))
    pv = [random.Random(SEED * 7919 + i).random() for i in range(G.np + 1)]
    for poly in me.polygons:
        n = poly.normal
        k = G.kind[poly.index]; pid = G.pid[poly.index]; tile = G.uvs[poly.index]
        off = (pv[pid] * 7.3 % 1.0, pv[(pid * 3 + 1) % len(pv)] * 5.1 % 1.0) if tile >= 3.0 else (0.0, 0.0)
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            if ax == 2:
                uv = (p.x / tile, p.y / tile)
            elif ax == 1:
                uv = (p.x / tile, p.z / tile)
            else:
                uv = (p.y / tile, p.z / tile)
            UV[li] = (uv[0] + off[0], uv[1] + off[1])
            WC[li] = wear_at(p, n, k, pv[pid], nofs)
    me.uv_layers.new(name='UVMap'); me.uv_layers['UVMap'].data.foreach_set('uv', UV.ravel())
    me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER'); me.color_attributes['wear'].data.foreach_set('color', WC.ravel())
    ob = bpy.data.objects.new('Gate', me); coll.objects.link(ob)
    return ob

# ----------------------------------------------------------------------------- B9 foliage (the exported meshes, re-used like the ruin kit)
B9_SRC = {'ivyA': ['Ivy_Patch_A/SC_B9_IvyPatchA'], 'ivyB': ['Ivy_Patch_B/SC_B9_IvyPatchB'],
          'weed': ['Weed_A/SC_B9_CrackWeed_A', 'Weed_B/SC_B9_CrackWeed_B', 'Weed_C/SC_B9_CrackWeed_C', 'Weed_D/SC_B9_CrackWeed_D'],
          'grass': ['Grass_A/SC_B9_GrassClump_A', 'Grass_B/SC_B9_GrassClump_B', 'Grass_C/SC_B9_GrassClump_C'],
          'moss': ['Moss_Tuft_A/SC_B9_MossTuft_A', 'Moss_Tuft_B/SC_B9_MossTuft_B', 'Moss_Tuft_C/SC_B9_MossTuft_C']}
FOLIAGE_PREFIX = os.path.join(B9, 'Weed_A', 'T_B9_Foliage')

def load_b9():
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
            if len(new) > 1:
                bpy.ops.object.select_all(action='DESELECT')
                for o in new:
                    o.select_set(True)
                bpy.context.view_layer.objects.active = new[0]; bpy.ops.object.join()
            o = new[0]
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            o.data.materials.clear(); o.data.materials.append(mat)
            o.hide_render = True; o.hide_viewport = True
            bb = [Vector(c) for c in o.bound_box]
            print('B9 %s: x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f tris %d' % (rel, min(c.x for c in bb), max(c.x for c in bb), min(c.y for c in bb),
                  max(c.y for c in bb), min(c.z for c in bb), max(c.z for c in bb), L.tri_count(o)))
            out[kind].append(o)
    return out

PLANTS = [  # (kind, x, y, z, yaw, scale, squash z)
    ('ivyA', -3.0, -0.84, 0.0, 0.0, 1.7, 1.15), ('ivyB', 3.05, -0.84, 0.0, 0.0, 1.45, 1.1), ('ivyA', 2.62, -0.9, 0.0, 0.15, 0.9, 1.0),
    ('ivyB', -2.55, -0.92, 0.0, -0.1, 1.0, 1.0),
    ('weed', -2.85, -1.45, 0.0, 0.3, 1.2, 1.0), ('weed', 2.95, -1.5, 0.0, 1.9, 1.1, 1.0), ('weed', -1.69, -1.52, 0.13, 0.9, 0.7, 0.8),
    ('weed', 1.06, -1.16, 0.28, 2.2, 0.6, 0.7), ('weed', 2.6, -1.1, 0.28, 4.0, 0.8, 0.8), ('weed', -0.29, -1.56, 0.13, 3.1, 0.55, 0.6),
    ('grass', -3.4, -1.2, 0.0, 0.0, 1.4, 1.0), ('grass', 3.45, -1.25, 0.0, 2.0, 1.3, 1.0), ('grass', -3.2, -1.95, 0.0, 1.0, 1.1, 0.9),
    ('grass', 3.15, -2.0, 0.0, 3.0, 1.0, 0.9), ('grass', -0.9, -1.98, 0.0, 0.5, 0.8, 0.7), ('grass', 1.8, -1.98, 0.0, 1.5, 0.9, 0.7),
    ('moss', -2.5, -1.25, 0.29, 0.0, 1.3, 1.0), ('moss', 2.45, -1.2, 0.29, 1.0, 1.2, 1.0), ('moss', -2.85, -1.66, 0.14, 2.0, 1.2, 1.0),
    ('moss', 2.8, -1.62, 0.11, 0.5, 1.4, 1.0), ('moss', -1.9, -0.9, SILL, 0.3, 0.9, 1.0), ('moss', 1.88, -0.85, SILL, 2.3, 0.8, 1.0),
    ('moss', -3.0, -0.6, 5.42, 0.4, 1.2, 1.0), ('moss', 3.0, -0.6, 5.42, 1.4, 1.0, 1.0),
]

def build_foliage(lib, coll):
    R = R_('plants'); parts = []
    for (kind, x, y, z, yaw, s, sq) in PLANTS:
        src = R.choice(lib[kind])
        o = src.copy(); o.data = src.data.copy(); coll.objects.link(o); o.hide_render = False; o.hide_viewport = False
        o.matrix_world = Matrix.Translation((x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Diagonal((s, s, s * sq, 1.0))
        parts.append(o)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.name = 'Foliage'; ob.data.name = 'Foliage'
    return ob

def build(lib):
    coll = bpy.data.collections.new('TitheGate'); bpy.context.scene.collection.children.link(coll)
    build_masonry(); build_gate(); build_tallies(); build_offerings()
    gate = to_object(coll)
    fol = build_foliage(lib, coll)
    for c in fol.users_collection:
        if c != coll:
            c.objects.unlink(fol)
    if coll not in fol.users_collection:
        coll.objects.link(fol)
    sock = bpy.data.objects.new('SOCKET_Breath', None); coll.objects.link(sock)
    sock.empty_display_type = 'ARROWS'; sock.empty_display_size = 0.3; sock.location = (0.0, -0.4, 1.6)
    root = bpy.data.objects.new('TitheGate', None); coll.objects.link(root); root.empty_display_type = 'PLAIN_AXES'
    for o in (gate, fol, sock):
        o.parent = root
    gt, ft = L.tri_count(gate), L.tri_count(fol)
    vs = [v.co for o in (gate, fol) for v in o.data.vertices]
    ext = (min(v.x for v in vs), max(v.x for v in vs), min(v.y for v in vs), max(v.y for v in vs), min(v.z for v in vs), max(v.z for v in vs))
    print('TITHEGATE tris %d (gate %d, foliage %d) extents x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f' % ((gt + ft, gt, ft) + ext))
    return dict(root=root, objs=[root, gate, fol, sock], gate=gate, fol=fol, tris=gt + ft, gate_tris=gt, fol_tris=ft, ext=ext)

def export(v):
    path = os.path.join(OUT, 'fbx', 'TitheGate.fbx'); os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in v['objs']:
        o.hide_set(False); o.select_set(True)
    bpy.context.view_layer.objects.active = v['root']
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True,
                             mesh_smooth_type='OFF', colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
                             use_custom_props=False, use_tspace=True, path_mode='STRIP')
    os.makedirs(EXP, exist_ok=True)
    shutil.copy(path, os.path.join(EXP, 'TitheGate.fbx'))
    for suf in ('_BaseColor.png', '_Normal.png', '_ORM.png'):
        shutil.copy(FOLIAGE_PREFIX + suf, os.path.join(EXP, 'T_B9_Foliage' + suf))
    print('EXPORT', path, '->', EXP)

# ----------------------------------------------------------------------------- review stage
def w2b(x, y, z):
    """Unity world (Hollowmere) -> this asset's Blender frame (pivot at world 5.6, 0, 19.6; front -Y = world -Z)."""
    return Vector((x - 5.6, z - 19.6, y))

def stage(low_sun=False):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
    w = bpy.data.worlds.get('W') or bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']
    sun = bpy.data.objects.get('Sun')
    if sun is None:
        sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); sc.collection.objects.link(sun)
    if low_sun:
        bg.inputs['Color'].default_value = (0.32, 0.36, 0.5, 1); bg.inputs['Strength'].default_value = 0.35
        sun.data.energy = 2.6; sun.data.color = (1.0, 0.62, 0.36); sun.data.angle = math.radians(1.5)
        sun.rotation_euler = (math.radians(80), 0, math.radians(215))       # low, from the south-west
    else:
        bg.inputs['Color'].default_value = (0.42, 0.55, 0.75, 1); bg.inputs['Strength'].default_value = 0.9
        sun.data.energy = 4.5; sun.data.color = (1.0, 0.93, 0.82); sun.data.angle = math.radians(1.5)
        sun.rotation_euler = (math.radians(38), 0, math.radians(160))
    if bpy.data.objects.get('SB_Ground'):
        return
    pl = bpy.data.objects.new('BreathLight', bpy.data.lights.new('BreathLight', 'POINT')); sc.collection.objects.link(pl)
    pl.data.energy = BREATH_LIGHT_W; pl.data.color = BREATH; pl.data.shadow_soft_size = 0.4; pl.location = (0.0, -0.45, 1.0)
    def sbox(name, a, b, mat):
        c = (a + b) / 2; s = b - a
        bpy.ops.mesh.primitive_cube_add(size=1, location=c); o = bpy.context.active_object; o.name = name; o.scale = s
        bpy.ops.object.transform_apply(scale=True)
        o.data.materials.append(mat)
        # metre UVs for the tileable walls
        me = o.data; uvl = me.uv_layers.active
        for poly in me.polygons:
            n = poly.normal; ax = max(range(3), key=lambda i: abs(n[i]))
            for li in poly.loop_indices:
                p = o.matrix_world @ me.vertices[me.loops[li].vertex_index].co
                uvl.data[li].uv = (p.x / 2, p.y / 2) if ax == 2 else ((p.x / 2, p.z / 2) if ax == 1 else (p.y / 2, p.z / 2))
        return o
    m = bpy.data.materials.new('SB_Earth'); m.use_nodes = True; nt = m.node_tree
    bs = nt.nodes['Principled BSDF']; bs.inputs['Roughness'].default_value = 0.95
    nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 0.9; nz.inputs['Detail'].default_value = 8
    ramp = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(nz.outputs[0], ramp.inputs[0])
    ramp.color_ramp.elements[0].position = 0.42; ramp.color_ramp.elements[0].color = (*L.hex2lin('#6b6455'), 1)
    ramp.color_ramp.elements[1].position = 0.62; ramp.color_ramp.elements[1].color = (*L.hex2lin('#5d6a35'), 1)
    nt.links.new(ramp.outputs[0], bs.inputs['Base Color'])
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, -20, 0)); g = bpy.context.active_object; g.name = 'SB_Ground'; g.data.materials.append(m)
    sand = MATS['FK_Sandstone']
    wall = lambda name, x0, x1: sbox(name, Vector((x0, WALL_F, -0.6)), Vector((x1, WALL_B, WALL_TOP)), sand)
    wall('SB_WallL', -18.0, -2.6); wall('SB_WallR', 3.4, 12.0)
    sbox('SB_Terrace', Vector((-18, WALL_B, -0.5)), Vector((12, 12, TERRACE)), L.flat_material('SB_Turf', '#5f6c38'))
    tw = w2b(5.6, 4.0, 24.0)
    sbox('SB_Tower', Vector((tw.x - 2.2, tw.y - 2.2, 4.0)), Vector((tw.x + 2.2, tw.y + 2.2, 15.6)), sand)
    sbox('SB_TowerBase', Vector((tw.x - 3.25, tw.y - 3.25, 4.0)), Vector((tw.x + 3.25, tw.y + 3.25, 5.0)), sand)
    a = w2b(8.0, 0, 7.5); b = w2b(17.0, 6.5, 15.0)
    sbox('SB_Cistern', Vector((a.x, a.y, 0)), Vector((b.x, b.y, 6.5)), MATS['FK_ChimneyStone'])

def cam_at(name, eye, at, fov):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name)); bpy.context.scene.collection.objects.link(cam)
    cam.data.sensor_fit = 'VERTICAL'; cam.data.angle = math.radians(fov); cam.data.clip_start = 0.05
    e = Vector(eye); t = Vector(at); cam.location = e
    cam.rotation_euler = (t - e).to_track_quat('-Z', 'Y').to_euler()
    return cam

def review():
    shots = {
        'cap': (w2b(5.2, 1.65, 8.2), w2b(5.6, 5.5, 22.0), 55, False),
        'near': (Vector((-3.6, -6.2, 1.7)), Vector((0.0, -0.4, 2.4)), 50, False),
        'close': (Vector((0.9, -2.9, 1.25)), Vector((-0.35, -0.7, 0.85)), 48, False),
        'lowsun': (w2b(5.2, 1.65, 8.2), w2b(5.6, 5.5, 22.0), 55, True),
    }
    for name in CAMS:
        eye, at, fov, low = shots[name]
        stage(low)
        cam = cam_at('cam_' + name, eye, at, fov)
        L.render(cam, os.path.join(OUT, 'TG_%s.png' % name), res=RRES, samples=SAMPLES)

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    lib = load_b9()
    v = build(lib)
    if EXPORT:
        export(v)
    with open(os.path.join(OUT, 'tithe_gate_stats.txt'), 'w') as f:
        f.write('tris %d gate %d foliage %d ext %s\n' % (v['tris'], v['gate_tris'], v['fol_tris'], tuple(round(c, 3) for c in v['ext'])))
    if RENDER:
        review()

main()
