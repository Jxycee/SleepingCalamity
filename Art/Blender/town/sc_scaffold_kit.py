"""ScaffoldKit: the builders' timber for Hollowmere's rebuild stages (build-plan B4, design docs/design/area1-hollowmere.md sections 6, S1, S3).

Boards-based, low poly, in the style of sc_ruin_kit.py / sc_market_stall.py: chunky slightly crooked oak members (1-3 deg lean, per-piece jitter, boards never quite
flush), metre-scale UVs (2 m per tile) that sample the shared FK_Oak tileable, rope and iron on FK_Iron. The planks echo the [T04a] plank scaffold boards the Megascans
set gives the same scene; the poles and lashings are ours so the frame reads as a hand-built village scaffold, not a catalogue prop.

Pieces (Blender z up, metres, pivot at ground centre, front = -Y; exported -Z forward, Y up, so front = local +Z in Unity, like the ruin and facade kits):
  Scaffold_Bay    3.0 x 1.2 m bay, two lifts (decks 1.1 and 2.2 m), five poles to 3.5 m, ledgers, three diagonal braces, toe boards, one loose plank.  (< 3.5k tris)
  Scaffold_Corner a 1.2 x 1.2 m return post-set for the corner where two faces meet (poles, ledgers, one deck piece each lift).
  Woodshed        the home's lean-to: 3.2 x 1.45 m, posts 2.45 m high at the wall side falling to 1.75 m, sloped board roof with three boards missing, side rails,
                  plank back-rail; the log piles stand in it (adopted T06).
  Sawhorse        0.9 m trestle with a plank across (Tobin's bench set).
  Board_Stack     1.4 x 0.55 m stack of sawn boards on two bearers with a rope strap (the stock pile).
Contract: all geometry inside its stated footprint (+0.15 m); materials FK_Oak, FK_Iron only (the Unity builder assigns the facade-kit slots).
Budget: < 10k tris per FBX (printed per piece).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_scaffold_kit.py -- --out <scratch> [--only Woodshed,...] [--export]
  --export writes <out>/fbx/<Name>.fbx (path_mode='STRIP') and copies each to Assets/_Project/Content/Art/Town/B4Kit/<Name>/<Name>.fbx.
"""
import sys, os, math, random, shutil
import bpy, bmesh
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
ONLY = arg('--only'); ONLY = ONLY.split(',') if ONLY else None
EXPORT = '--export' in argv
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
DEST = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B4Kit')
TILE = 2.0   # metres per UV tile (as the ruin kit and B4 facade)


def mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


class Piece:
    def __init__(self, name, seed):
        self.name = name; self.rnd = random.Random(seed)
        self.bm = {}   # material name -> bmesh

    def _bm(self, m):
        if m not in self.bm: self.bm[m] = bmesh.new(); self.bm[m].loops.layers.uv.new('UVMap')
        return self.bm[m]

    def box(self, c, s, m='FK_Oak', yaw=0.0, lean=(0.0, 0.0), jit=0.012, taper=1.0):
        """A slightly crooked box: centre c, full size s (x, y, z), yaw about z, lean (rx, ry degrees about its own base-up axis), per-vertex jitter, optional taper of the top."""
        b = self._bm(m); uv = b.loops.layers.uv.active
        r = self.rnd
        hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        corners = []
        for z in (-1, 1):
            for y in (-1, 1):
                for x in (-1, 1):
                    k = taper if z > 0 else 1.0
                    corners.append(Vector((x * hx * k + r.uniform(-jit, jit), y * hy * k + r.uniform(-jit, jit), z * hz + r.uniform(-jit, jit) * 0.5)))
        rot = Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Rotation(math.radians(lean[0]), 4, 'X') @ Matrix.Rotation(math.radians(lean[1]), 4, 'Y')
        verts = [b.verts.new(rot @ (v + Vector((0, 0, 0))) + Vector(c)) for v in corners]
        # corner index = x + 2*y + 4*z with x,y,z in {0,1} for -1,+1
        def V(x, y, z): return verts[x + 2 * y + 4 * z]
        faces = [((0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)),   # bottom
                 ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)),   # top
                 ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),   # -y
                 ((1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)),   # +y
                 ((0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)),   # -x
                 ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1))]   # +x
        for f in faces:
            face = b.faces.new([V(*p) for p in f])
            n = face.normal if face.normal.length > 0 else Vector((0, 0, 1))
            ax = max(range(3), key=lambda i: abs(n[i]))
            for loop in face.loops:
                p = loop.vert.co
                u, v = [(p.y, p.z), (p.x, p.z), (p.x, p.y)][ax]
                loop[uv].uv = (u / TILE, v / TILE)
        return self

    def pole(self, base, top, thick=0.075, m='FK_Oak', sides=6):
        """A round-ish pole (low-sided prism) from base to top, both Vectors."""
        b = self._bm(m); uv = b.loops.layers.uv.active
        d = (top - base); length = d.length
        q = d.to_track_quat('Z', 'Y')
        ring0, ring1 = [], []
        for i in range(sides):
            a = 2 * math.pi * i / sides + self.rnd.uniform(-0.1, 0.1)
            off = Vector((math.cos(a) * thick / 2, math.sin(a) * thick / 2, 0))
            ring0.append(b.verts.new(base + q @ off))
            ring1.append(b.verts.new(base + q @ (off * 0.92) + d))
        for i in range(sides):
            j = (i + 1) % sides
            f = b.faces.new([ring0[i], ring0[j], ring1[j], ring1[i]])
            for loop in f.loops:
                p = loop.vert.co
                loop[uv].uv = ((p.x + p.y) / TILE, p.z / TILE)
        for ring, flip in ((ring0, True), (ring1, False)):
            f = b.faces.new(list(reversed(ring)) if flip else ring)
            for loop in f.loops: loop[uv].uv = (loop.vert.co.x / TILE, loop.vert.co.y / TILE)
        return self

    def beam(self, a, b_, thick=(0.07, 0.07), m='FK_Oak', jit=0.01):
        """A square member from point a to point b_ (a brace, a ledger)."""
        a = Vector(a); b_ = Vector(b_); d = b_ - a; length = d.length
        mid = (a + b_) / 2
        q = d.to_track_quat('Y', 'Z')
        e = Matrix.Translation(mid) @ q.to_matrix().to_4x4()
        bm = self._bm(m); uv = bm.loops.layers.uv.active
        hx, hz = thick[0] / 2, thick[1] / 2; hl = length / 2
        pts = {}
        for yy in (-1, 1):
            for xx in (-1, 1):
                for zz in (-1, 1):
                    pts[(xx, yy, zz)] = bm.verts.new(e @ Vector((xx * hx + self.rnd.uniform(-jit, jit), yy * hl, zz * hz + self.rnd.uniform(-jit, jit))))
        def F(*k):
            f = bm.faces.new([pts[i] for i in k])
            for loop in f.loops:
                p = loop.vert.co
                loop[uv].uv = ((p.x + p.y) / TILE, (p.z + (p.x - p.y) * 0.3) / TILE)
        F((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1)); F((-1, -1, 1), (-1, 1, 1), (1, 1, 1), (1, -1, 1))
        F((-1, -1, -1), (-1, -1, 1), (1, -1, 1), (1, -1, -1)); F((-1, 1, -1), (1, 1, -1), (1, 1, 1), (-1, 1, 1))
        F((-1, -1, -1), (-1, 1, -1), (-1, 1, 1), (-1, -1, 1)); F((1, -1, -1), (1, -1, 1), (1, 1, 1), (1, 1, -1))
        return self

    def build(self):
        objs = []
        me = bpy.data.meshes.new(self.name)
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        mats = list(self.bm.keys())
        for name in mats:
            me.materials.append(mat(name))
        merged = bmesh.new()
        for idx, name in enumerate(mats):
            src = self.bm[name]
            tmp = bpy.data.meshes.new('tmp'); src.to_mesh(tmp)
            merged.from_mesh(tmp)
            bpy.data.meshes.remove(tmp)
            # faces appended last get this material
        # from_mesh loses per-chunk material tags: rebuild by counting faces per source
        merged.faces.ensure_lookup_table()
        i = 0
        for idx, name in enumerate(mats):
            for _ in range(len(self.bm[name].faces)):
                merged.faces[i].material_index = idx; i += 1
        merged.to_mesh(me); merged.free()
        for b in self.bm.values(): b.free()
        bpy.context.view_layer.objects.active = ob
        me.update()
        return ob


def scaffold_bay():
    p = Piece('Scaffold_Bay', 11)
    W, D = 3.0, 1.2
    xs = [-W / 2, -W / 4 + 0.05, W / 2 - 0.05] ; xs = [-1.5, 0.0, 1.5]
    # poles at front and back of each end plus the middle front; 3.5 m, the back row one lift shorter
    for x in (-1.5, 1.5):
        for y, h in ((-D / 2, 3.5), (D / 2, 3.1)):
            p.pole(Vector((x + p.rnd.uniform(-.02, .02), y, 0.0)), Vector((x + p.rnd.uniform(-.03, .03), y + p.rnd.uniform(-.02, .02), h)), 0.085)
    p.pole(Vector((0.0, -D / 2, 0.0)), Vector((0.02, -D / 2, 2.9)), 0.075)
    for z in (0.55, 1.1, 1.65, 2.2, 2.75):   # ledgers on the front, every half lift
        p.beam((-1.55, -D / 2 - 0.07, z), (1.55, -D / 2 - 0.07, z + p.rnd.uniform(-0.02, 0.02)), (0.05, 0.07))
    for z in (1.1, 2.2):   # transoms and deck boards
        for x in (-1.5, 0.0, 1.5):
            p.beam((x, -D / 2 - 0.02, z - 0.06), (x, D / 2 + 0.05, z - 0.06), (0.06, 0.06))
        for k in range(5):
            y = -D / 2 + 0.12 + k * 0.23
            skip = (z > 2 and k == 3 and False)
            p.box((p.rnd.uniform(-0.03, 0.03), y, z + 0.02), (3.18 - p.rnd.uniform(0, 0.12), 0.22, 0.045), 'FK_Oak', yaw=p.rnd.uniform(-0.8, 0.8))
        p.box((0.0, -D / 2 - 0.1, z + 0.17), (3.1, 0.035, 0.2), 'FK_Oak')   # toe board
        p.beam((-1.45, -D / 2 - 0.11, z + 0.45), (1.45, -D / 2 - 0.11, z + 0.45), (0.045, 0.06))   # guard rail
    # diagonal braces on the front face (X pattern across two lifts, one the other way)
    p.beam((-1.5, -D / 2 - 0.1, 0.1), (0.0, -D / 2 - 0.1, 2.2), (0.045, 0.06))
    p.beam((1.5, -D / 2 - 0.1, 0.1), (0.0, -D / 2 - 0.1, 2.25), (0.045, 0.06))
    p.beam((1.5, -D / 2 - 0.02, 1.1), (1.5, D / 2, 2.2), (0.045, 0.06))   # side brace
    # a loose plank leaning against the poles and a lashed iron ring at the joints
    p.box((-0.9, D / 2 + 0.05, 0.6), (2.0, 0.22, 0.04), 'FK_Oak', lean=(0, 58), yaw=8)
    for x, z in ((-1.5, 1.1), (1.5, 1.1), (-1.5, 2.2), (1.5, 2.2)):
        p.box((x, -D / 2, z), (0.12, 0.12, 0.03), 'FK_Iron')
    return p


def scaffold_corner():
    p = Piece('Scaffold_Corner', 12)
    for x, y, h in ((-0.6, -0.6, 3.5), (0.6, -0.6, 3.3), (-0.6, 0.6, 3.1), (0.6, 0.6, 3.3)):
        p.pole(Vector((x, y, 0)), Vector((x + p.rnd.uniform(-.02, .02), y, h)), 0.085)
    for z in (1.1, 2.2):
        p.beam((-0.6, -0.65, z), (0.6, -0.65, z), (0.05, 0.07)); p.beam((-0.65, -0.6, z), (-0.65, 0.6, z), (0.05, 0.07))
        p.box((0.0, 0.0, z + 0.02), (1.25, 1.2, 0.045), 'FK_Oak', yaw=p.rnd.uniform(-1, 1))
        p.beam((-0.6, -0.65, z + 0.45), (0.6, -0.65, z + 0.45), (0.045, 0.06))
    p.beam((-0.6, -0.68, 0.1), (0.6, -0.68, 2.2), (0.045, 0.06))
    return p


def woodshed():
    p = Piece('Woodshed', 13)
    W, D = 3.2, 1.45   # along the house wall (x), out from it (-y = front)
    hb, hf = 2.45, 1.75   # height at the wall (+y), at the open front (-y)
    for x in (-W / 2 + 0.1, W / 2 - 0.1):
        p.pole(Vector((x, D / 2 - 0.08, 0.0)), Vector((x + p.rnd.uniform(-.02, .02), D / 2 - 0.08, hb)), 0.14)
        p.pole(Vector((x, -D / 2 + 0.08, 0.0)), Vector((x + p.rnd.uniform(-.02, .02), -D / 2 + 0.08, hf)), 0.14)
    p.pole(Vector((0.0, -D / 2 + 0.08, 0.0)), Vector((0.0, -D / 2 + 0.08, hf - 0.05)), 0.12)
    # plates (top rails) front and back, and rafters running down the slope
    p.beam((-W / 2 - 0.05, D / 2 - 0.08, hb), (W / 2 + 0.05, D / 2 - 0.08, hb), (0.1, 0.12))
    p.beam((-W / 2 - 0.1, -D / 2 + 0.08, hf), (W / 2 + 0.1, -D / 2 + 0.08, hf), (0.1, 0.12))
    slope = math.degrees(math.atan2(hb - hf, D - 0.16))
    for x in (-W / 2 + 0.1, -W / 6, W / 6, W / 2 - 0.1):
        p.beam((x, D / 2 - 0.08, hb + 0.07), (x, -D / 2 - 0.2, hf + 0.04 - 0.0), (0.09, 0.09))
    # roof boards, laid along the slope (over the rafters), three missing
    n = 14; bw = (W + 0.3) / n
    for k in range(n):
        if k in (3, 8, 9): continue
        x = -W / 2 - 0.15 + bw * (k + 0.5)
        ln = math.hypot(D + 0.3, hb - hf) + 0.05
        p.box((x + p.rnd.uniform(-.01, .01), 0.0 - 0.02, (hb + hf) / 2 + 0.16 + p.rnd.uniform(0, 0.02)), (bw - 0.012, ln, 0.035), 'FK_Oak', lean=(-slope, 0))
    # back-rail planks against the wall (a low guard board) and side rails
    p.box((0.0, D / 2 - 0.06, 0.45), (W - 0.3, 0.035, 0.22), 'FK_Oak')
    p.box((-W / 2 + 0.1, 0.0, 0.5), (0.04, D - 0.4, 0.2), 'FK_Oak'); p.box((W / 2 - 0.1, 0.0, 0.5), (0.04, D - 0.4, 0.2), 'FK_Oak')
    p.beam((-W / 2 + 0.1, D / 2 - 0.1, hb - 0.2), (-W / 2 + 0.1, -D / 2 + 0.1, hf - 0.1), (0.06, 0.07))   # knee braces
    p.beam((W / 2 - 0.1, D / 2 - 0.1, hb - 0.2), (W / 2 - 0.1, -D / 2 + 0.1, hf - 0.1), (0.06, 0.07))
    p.box((0.0, -D / 2 + 0.2, 0.05), (W - 0.2, 0.1, 0.1), 'FK_Oak')   # sill timber for the pile to stand on
    for x in (-W / 2 + 0.1, W / 2 - 0.1):
        p.box((x, -D / 2 + 0.08, hf), (0.1, 0.1, 0.03), 'FK_Iron')
    return p


def sawhorse():
    p = Piece('Sawhorse', 14)
    for x in (-0.38, 0.38):
        p.beam((x, -0.2, 0.0), (x, -0.04, 0.78), (0.07, 0.07)); p.beam((x, 0.2, 0.0), (x, 0.04, 0.78), (0.07, 0.07))
    p.beam((-0.45, 0.0, 0.8), (0.45, 0.0, 0.8), (0.09, 0.09))
    p.beam((-0.38, -0.12, 0.3), (0.38, 0.12, 0.34), (0.03, 0.05))
    p.box((0.05, 0.0, 0.875), (1.1, 0.25, 0.04), 'FK_Oak', yaw=3)
    return p


def board_stack():
    p = Piece('Board_Stack', 15)
    for x in (-0.5, 0.5): p.box((x, 0.0, 0.06), (0.1, 0.5, 0.12), 'FK_Oak')
    for k in range(6):
        for j in range(3):
            p.box((p.rnd.uniform(-0.03, 0.03), -0.17 + j * 0.17, 0.12 + 0.045 * k + 0.02), (1.38 - p.rnd.uniform(0, 0.2), 0.16, 0.04), 'FK_Oak', yaw=p.rnd.uniform(-2, 2))
    for x in (-0.3, 0.3):
        p.box((x, 0.0, 0.45), (0.025, 0.58, 0.02), 'FK_Iron')
    return p


PIECES = {'Scaffold_Bay': scaffold_bay, 'Scaffold_Corner': scaffold_corner, 'Woodshed': woodshed, 'Sawhorse': sawhorse, 'Board_Stack': board_stack}


def export(ob, name):
    os.makedirs(os.path.join(OUT, 'fbx'), exist_ok=True)
    path = os.path.join(OUT, 'fbx', name + '.fbx')
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='OFF',
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False, use_tspace=True, path_mode='STRIP')
    dst = os.path.join(DEST, name); os.makedirs(dst, exist_ok=True)
    shutil.copyfile(path, os.path.join(dst, name + '.fbx'))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name, fn in PIECES.items():
        if ONLY and name not in ONLY: continue
        ob = fn().build(); ob.name = name
        bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.triangulate(bm, faces=bm.faces[:]); tris = len(bm.faces); bm.free()
        print('SC SCAFFOLD', name, 'tris', tris)
        if tris >= 10000: sys.exit('over budget: ' + name)
        if EXPORT: export(ob, name)
        bpy.data.objects.remove(ob)


main()
