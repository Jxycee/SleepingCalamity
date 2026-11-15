"""Rat-king: floor 1 key carrier of the Cellars (docs/design/dungeon1-cellars.md, decision DC2). Hand-built, rigged and animated.

A big storybook rat in a tin-can crown of spoons, a ragged olive cloak with tin shoulder plates, a rope belt with a ring of keys and ember eyes:
whimsical, with one wrong note (the glowing eyes and the too-wide grin). Stands hunched on its hind legs, forelimbs hanging.

Reference: Reference/private/concepts/Rat-king.jpg (gitignored, study only; the --sheet step reads it from the main checkout if present).
Size (Core/Combat/EnemyModel.cs RatKing): capsule Height 1.2, Radius 0.5, VisualReach 0.85. Crown spoons top out at ~1.26 m, snout tip 0.62 m
ahead of the origin, tail trails 1.5 m behind. Origin at the feet, Unity axes (front +z, y up), metres.
Budget (sc-asset-budget enemy tier, PHASE1 3.3): <= 25k tris, one material set; this model is ~5-8k tris, procedural Principled materials
(no textures: the Unity builder takes colour/metal/rough from the FBX materials; ember-eye emission is set by the builder).

Objects (exact names, the presentation code finds them by name): Body, Head, Crown, Tail, beside the armature "RatKingRig".
Bones: root, hips, spine, chest, neck, head, jaw, ear_L/R, crown, thigh/shin/foot_L/R, upperarm/forearm/hand_L/R, tail1-5, key1, key2.
Clips (60 fps, 1 frame == the game's AttackFrame; Core/Combat/AttackCatalog.cs, read, never changed):
  Idle 90 (loop) | Move 20 (loop) | Hit 20 | Death 72 (ends held) | Attack_rat_lunge 62 | Attack_rat_spin 62
  lunge: tell peak f8, glint f14, lunge motion f16-28, hit f28-36, recovery to rest at f62
  spin : tell peak f12, glint f16, tail whip f20-46 (hit f30-40), recovery to rest at f62
The root bone never moves (the game moves the enemy). Every clip starts and ends on the bind pose (Death ends on the floor).

Run (from the repo root):
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python Art/Blender/enemies/sc_enemy_ratking.py -- \
      --out <scratch dir> [--export Assets/_Project/Content/Art/Enemies/RatKing] [--no-render] [--blend] [--rres 520]
  python Art/Blender/enemies/sc_enemy_ratking.py --sheet <scratch dir> <sheet.png> [--concept <Rat-king.jpg>] [--no-concept]
"""
import sys, os, math, random, zlib, bisect

SEED = 0x5A7C1D6
HAVE_BPY = True
try:
    import bpy, bmesh
    from mathutils import Vector, Matrix, Euler
except ImportError:
    HAVE_BPY = False

NAME = 'RatKing'

# ----------------------------------------------------------------------------------------------------------------------------- skeleton
# (name, parent, head, tail) in Unity axes: x right, y up, z forward. The character's right is +x.
def _lr(name, parent, h, t):
    sided = parent in ('thigh', 'shin', 'upperarm', 'forearm')
    return [(name + '_L', parent + '_L' if sided else parent, (-h[0], h[1], h[2]), (-t[0], t[1], t[2])), (name + '_R', parent + '_R' if sided else parent, h, t)]
BONES = [('root', None, (0, 0, 0), (0, 0.14, 0)),
         ('hips', 'root', (0, 0.45, -0.16), (0, 0.55, -0.08)),
         ('spine', 'hips', (0, 0.55, -0.08), (0, 0.70, 0.05)),
         ('chest', 'spine', (0, 0.70, 0.05), (0, 0.84, 0.18)),
         ('neck', 'chest', (0, 0.84, 0.18), (0, 0.92, 0.28)),
         ('head', 'neck', (0, 0.92, 0.28), (0, 0.97, 0.50)),
         ('jaw', 'head', (0, 0.90, 0.39), (0, 0.86, 0.60)),
         ('crown', 'head', (0, 1.00, 0.335), (0, 1.12, 0.335))]
BONES += _lr('ear', 'head', (0.14, 0.99, 0.27), (0.20, 1.07, 0.25))
BONES += _lr('thigh', 'hips', (0.17, 0.47, -0.15), (0.20, 0.30, -0.04))
BONES += _lr('shin', 'thigh', (0.20, 0.30, -0.04), (0.18, 0.11, -0.19))
BONES += _lr('foot', 'shin', (0.18, 0.11, -0.19), (0.18, 0.02, 0.07))
BONES += _lr('upperarm', 'chest', (0.20, 0.80, 0.12), (0.26, 0.62, 0.10))
BONES += _lr('forearm', 'upperarm', (0.26, 0.62, 0.10), (0.25, 0.47, 0.22))
BONES += _lr('hand', 'forearm', (0.25, 0.47, 0.22), (0.25, 0.40, 0.31))
TAIL_PTS = [(0, 0.43, -0.25), (0, 0.35, -0.50), (0, 0.22, -0.77), (0, 0.10, -1.03), (0, 0.04, -1.28), (0, 0.025, -1.52)]
for _i in range(5):
    BONES.append(('tail%d' % (_i + 1), 'hips' if _i == 0 else 'tail%d' % _i, TAIL_PTS[_i], TAIL_PTS[_i + 1]))
BONES += [('key1', 'hips', (0.17, 0.52, 0.0), (0.18, 0.42, 0.02)), ('key2', 'key1', (0.18, 0.42, 0.02), (0.19, 0.30, 0.03))]
BONE_NAMES = [b[0] for b in BONES]
BI = {n: i for i, n in enumerate(BONE_NAMES)}


def U(x, y, z):
    """Unity axes -> Blender (z up, front -Y): Blender = (-x, -z, y)."""
    return Vector((-x, -z, y))


# ----------------------------------------------------------------------------------------------------------------------------- geometry
def catmull(pts, sub):
    n = len(pts); out = []
    for k in range(n - 1):
        p0 = pts[max(k - 1, 0)]; p1 = pts[k]; p2 = pts[k + 1]; p3 = pts[min(k + 2, n - 1)]
        for j in range(sub):
            t = j / sub
            out.append((0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3), k + t))
    out.append((pts[-1], n - 1))
    return out


def seg_weights(s, bones):
    ws = {}; s = min(max(s, 0.5), len(bones) - 0.5)
    for k, b in enumerate(bones):
        w = max(0.0, 1 - abs(s - (k + 0.5)))
        if w > 0: ws[b] = ws.get(b, 0) + w
    tot = sum(ws.values())
    return {b: w / tot for b, w in ws.items()}


class MB:
    """Mesh builder in Unity axes. Weights are dicts bone -> weight; faces take the current material slot."""
    def __init__(self, name, mats):
        self.name = name; self.mats = mats; self.bm = bmesh.new(); self.dl = self.bm.verts.layers.deform.verify(); self.mat = 0

    def vert(self, p, w):
        v = self.bm.verts.new(U(*p))
        for b, x in w.items(): v[self.dl][BI[b]] = x
        return v

    def face(self, vs, mat=None):
        try:
            f = self.bm.faces.new(vs)
        except ValueError:
            return None
        f.material_index = self.mat if mat is None else mat; f.smooth = True
        return f

    def tube(self, pts, rad, bones, ring=12, sub=4, side=None, dome=0.6, matsplit=None, ann=0.0, wfix=None):
        """A tapered tube along a Catmull-Rom path; rad = (rx, ry) per control point; weights blend along `bones` (one per segment)."""
        pts = [Vector(p) for p in pts]; R = [Vector((r[0], r[1], 0)) if not isinstance(r, (int, float)) else Vector((r, r, 0)) for r in rad]
        P = catmull(pts, sub); Q = catmull(R, sub); rings = []
        for i, ((p, s), (r, _)) in enumerate(zip(P, Q)):
            a = P[min(i + 1, len(P) - 1)][0]; b = P[max(i - 1, 0)][0]; T = (a - b).normalized()
            S = Vector(side) if side is not None else Vector((1, 0, 0))
            N1 = S - T * S.dot(T)
            if N1.length < 1e-4: N1 = Vector((0, 0, 1)) - T * T.z
            N1.normalize(); N2 = T.cross(N1)
            w = wfix if wfix is not None else seg_weights(s, bones)
            k = 1.0 + ann * math.sin(i * 1.7)
            vs = [self.vert(p + N1 * (r.x * k * math.cos(2 * math.pi * j / ring)) + N2 * (r.y * k * math.sin(2 * math.pi * j / ring)), w) for j in range(ring)]
            rings.append((vs, s, p, T, r, w))
        for i in range(len(rings) - 1):
            m = None
            if matsplit is not None: m = matsplit[1] if rings[i][1] >= matsplit[0] else self.mat
            a, b = rings[i][0], rings[i + 1][0]
            for j in range(ring): self.face([a[j], a[(j + 1) % ring], b[(j + 1) % ring], b[j]], m)
        for end, sgn in ((0, -1), (-1, 1)):
            vs, s, p, T, r, w = rings[end]
            m = None
            if matsplit is not None and end == -1 and s >= matsplit[0]: m = matsplit[1]
            c = self.vert(p + T * sgn * dome * (r.x + r.y) * 0.5, w)
            for j in range(ring): self.face([vs[j], vs[(j + 1) % ring], c], m)

    def blob(self, c, r, w, rot=(0, 0, 0), seg=10, rings=6):
        M = Euler([math.radians(a) for a in rot], 'XYZ').to_matrix(); c = Vector(c)
        def P(x, y, z): return c + M @ Vector((x, y, z))
        lo = self.vert(P(0, -r[1], 0), w); hi = self.vert(P(0, r[1], 0), w); rs = []
        for i in range(1, rings):
            ph = -math.pi / 2 + math.pi * i / rings
            rs.append([self.vert(P(r[0] * math.cos(ph) * math.cos(2 * math.pi * j / seg), r[1] * math.sin(ph), r[2] * math.cos(ph) * math.sin(2 * math.pi * j / seg)), w) for j in range(seg)])
        for j in range(seg): self.face([lo, rs[0][(j + 1) % seg], rs[0][j]])
        for i in range(len(rs) - 1):
            for j in range(seg): self.face([rs[i][j], rs[i][(j + 1) % seg], rs[i + 1][(j + 1) % seg], rs[i + 1][j]])
        for j in range(seg): self.face([hi, rs[-1][j], rs[-1][(j + 1) % seg]])

    def loop(self, c, axis, R, r, w, segs=20, rsegs=6, mat=None):
        """A torus: centre c, ring plane perpendicular to axis, radii R=(rx, rz), tube radius r."""
        c = Vector(c); A = Vector(axis).normalized(); u = Vector((1, 0, 0)); v = A.cross(u).normalized(); grid = []
        for i in range(segs):
            th = 2 * math.pi * i / segs; P = c + u * (R[0] * math.cos(th)) + v * (R[1] * math.sin(th)); n = (P - c).normalized()
            grid.append([self.vert(P + n * (r * math.cos(2 * math.pi * k / rsegs)) + A * (r * math.sin(2 * math.pi * k / rsegs)), w) for k in range(rsegs)])
        for i in range(segs):
            a, b = grid[i], grid[(i + 1) % segs]
            for k in range(rsegs): self.face([a[k], a[(k + 1) % rsegs], b[(k + 1) % rsegs], b[k]], mat)

    def shell(self, outer, inner, wgrid, wrap=False):
        """Thin closed sheet from two same-shaped point grids (rows x cols); wgrid[i][j] = weights."""
        rows, cols = len(outer), len(outer[0])
        O = [[self.vert(outer[i][j], wgrid[i][j]) for j in range(cols)] for i in range(rows)]
        I = [[self.vert(inner[i][j], wgrid[i][j]) for j in range(cols)] for i in range(rows)]
        cj = cols if wrap else cols - 1
        for i in range(rows - 1):
            for j in range(cj):
                j2 = (j + 1) % cols
                self.face([O[i][j], O[i][j2], O[i + 1][j2], O[i + 1][j]]); self.face([I[i][j], I[i + 1][j], I[i + 1][j2], I[i][j2]])
        for j in range(cj):
            j2 = (j + 1) % cols
            self.face([O[0][j], I[0][j], I[0][j2], O[0][j2]]); self.face([O[-1][j], O[-1][j2], I[-1][j2], I[-1][j]])
        if not wrap:
            for i in range(rows - 1):
                self.face([O[i][0], O[i + 1][0], I[i + 1][0], I[i][0]]); self.face([O[i][-1], I[i][-1], I[i + 1][-1], O[i + 1][-1]])

    def finish(self, rig):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(self.name + '_mesh'); bm.to_mesh(me); bm.free()
        for m in self.mats: me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me); bpy.context.scene.collection.objects.link(ob)
        for b in BONE_NAMES: ob.vertex_groups.new(name=b)
        ob.parent = rig; md = ob.modifiers.new('Armature', 'ARMATURE'); md.object = rig
        me.polygons.foreach_set('use_smooth', [True] * len(me.polygons)); me.update()
        return ob


# ----------------------------------------------------------------------------------------------------------------------------- materials
def material(name, base, rough, metal=0.0, vary=None, emission=None, sheen=0.0, sss=0.0, rust=None):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    def setin(n, v):
        try: bs.inputs[n].default_value = v
        except KeyError: pass
    setin('Base Color', (*base, 1)); setin('Roughness', rough); setin('Metallic', metal); setin('Sheen Weight', sheen); setin('Subsurface Weight', sss)
    if emission is not None:
        setin('Emission Color', (*emission[0], 1)); setin('Emission Strength', emission[1])
    if vary or rust:
        tc = nt.nodes.new('ShaderNodeTexCoord'); no = nt.nodes.new('ShaderNodeTexNoise')
        no.inputs['Scale'].default_value = 28.0 if rust is None else 9.0; no.inputs['Detail'].default_value = 8.0
        nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
        cr = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(no.outputs['Fac'], cr.inputs['Fac'])
        if rust:
            cr.color_ramp.elements[0].position = 0.42; cr.color_ramp.elements[1].position = 0.58
            cr.color_ramp.elements[0].color = (*base, 1); cr.color_ramp.elements[1].color = (*rust, 1)
            mr = nt.nodes.new('ShaderNodeMath'); mr.operation = 'MULTIPLY'; mr.inputs[1].default_value = 0.4
            nt.links.new(no.outputs['Fac'], mr.inputs[0])
        else:
            cr.color_ramp.elements[0].color = (*[c * 0.6 for c in base], 1); cr.color_ramp.elements[1].color = (*[min(1, c * 1.45) for c in base], 1)
        nt.links.new(cr.outputs['Color'], bs.inputs['Base Color'])
    return m


def make_materials():
    return {
        'fur': material('RK_Fur', (0.17, 0.145, 0.125), 0.9, vary=True, sheen=0.6),
        'skin': material('RK_Skin', (0.46, 0.27, 0.26), 0.55, sss=0.15),
        'cloth': material('RK_Cloak', (0.27, 0.26, 0.10), 0.95, vary=True),
        'rope': material('RK_Rope', (0.40, 0.30, 0.17), 0.9, vary=True),
        'brass': material('RK_Brass', (0.55, 0.38, 0.12), 0.35, metal=1.0),
        'tin': material('RK_RustTin', (0.42, 0.42, 0.40), 0.55, metal=0.9, rust=(0.20, 0.10, 0.05)),
        'spoon': material('RK_Spoon', (0.62, 0.62, 0.60), 0.32, metal=1.0),
        'eye': material('RK_EmberEye', (0.05, 0.02, 0.01), 0.3, emission=((1.0, 0.13, 0.01), 1.1)),
        'tooth': material('RK_Bone', (0.78, 0.70, 0.52), 0.4),
    }


# ----------------------------------------------------------------------------------------------------------------------------- the rat
def build_meshes(rig, M):
    """Returns the four objects. Material slots per object are listed beside each builder."""
    rng = random.Random(SEED ^ zlib.crc32(b'ratking'))
    # ---- Body: 0 fur, 1 skin, 2 cloak, 3 rope, 4 brass, 5 tin
    b = MB('Body', [M['fur'], M['skin'], M['cloth'], M['rope'], M['brass'], M['tin']])
    torso = [(0, 0.40, -0.24), (0, 0.47, -0.15), (0, 0.58, -0.05), (0, 0.72, 0.07), (0, 0.85, 0.19), (0, 0.935, 0.29)]
    b.tube(torso, [(0.12, 0.13), (0.19, 0.20), (0.245, 0.25), (0.225, 0.22), (0.175, 0.17), (0.10, 0.10)], ['hips', 'hips', 'spine', 'chest', 'neck'], ring=16, sub=4, dome=0.5)
    b.blob((0, 0.43, -0.23), (0.17, 0.15, 0.17), {'hips': 1}, seg=12, rings=7)
    for sx, sd in ((-1, 'L'), (1, 'R')):
        b.blob((sx * 0.16, 0.44, -0.14), (0.13, 0.16, 0.15), {'hips': 0.5, 'thigh_' + sd: 0.5}, seg=10, rings=6)
        # leg: fur thigh and shin, skin foot (slot 1 from s >= 2)
        leg = [(sx * 0.17, 0.47, -0.15), (sx * 0.20, 0.30, -0.04), (sx * 0.18, 0.11, -0.19), (sx * 0.18, 0.04, -0.06), (sx * 0.18, 0.026, 0.075)]
        b.tube(leg, [(0.115, 0.125), (0.07, 0.075), (0.052, 0.055), (0.05, 0.034), (0.047, 0.026)], ['thigh_' + sd, 'shin_' + sd, 'foot_' + sd, 'foot_' + sd], ring=10, sub=4, matsplit=(2.0, 1))
        b.mat = 1
        for dx in (-0.045, 0.0, 0.045):
            b.tube([(sx * 0.18 + dx * 0.3, 0.032, 0.07), (sx * 0.18 + dx, 0.014, 0.15)], [(0.02, 0.016), (0.012, 0.01)], ['foot_' + sd], ring=5, sub=1, dome=0.3)
            b.mat = 3
            b.tube([(sx * 0.18 + dx, 0.014, 0.145), (sx * 0.18 + dx * 1.2, 0.01, 0.185)], [(0.011, 0.009), (0.002, 0.002)], ['foot_' + sd], ring=4, sub=1, dome=0.0)
            b.mat = 1
        b.mat = 0
        arm = [(sx * 0.20, 0.80, 0.12), (sx * 0.26, 0.62, 0.10), (sx * 0.25, 0.47, 0.22), (sx * 0.25, 0.405, 0.30)]
        b.tube(arm, [(0.085, 0.085), (0.058, 0.058), (0.046, 0.042), (0.040, 0.03)], ['upperarm_' + sd, 'forearm_' + sd, 'hand_' + sd], ring=10, sub=4, matsplit=(1.75, 1))
        b.mat = 1
        for dx in (-0.025, 0.0, 0.025):
            b.tube([(sx * 0.25 + dx * 0.4, 0.41, 0.29), (sx * 0.25 + dx, 0.365, 0.345)], [(0.014, 0.012), (0.008, 0.007)], ['hand_' + sd], ring=5, sub=1, dome=0.2)
            b.mat = 3
            b.tube([(sx * 0.25 + dx, 0.368, 0.342), (sx * 0.25 + dx * 1.3, 0.34, 0.375)], [(0.008, 0.007), (0.0015, 0.0015)], ['hand_' + sd], ring=4, sub=1, dome=0.0)
            b.mat = 1
        b.mat = 5   # tin shoulder plate, patched onto the cloak
        b.blob((sx * 0.215, 0.855, 0.135), (0.115, 0.022, 0.095), {'chest': 1}, rot=(8, 0, -sx * 28), seg=10, rings=5)
        b.blob((sx * 0.235, 0.825, 0.125), (0.10, 0.02, 0.085), {'chest': 0.8, 'upperarm_' + sd: 0.2}, rot=(8, 0, -sx * 45), seg=10, rings=5)
    # cloak: an arc from the front-left shoulder round the back to the front-right, ragged hem, thin double sheet
    b.mat = 2
    rows, cols = 7, 15; outer = []; inner = []; wg = []
    for i in range(rows):
        v = i / (rows - 1); cy = 0.90 - 0.03 * v - v * 0.60; cz = 0.17 - 0.45 * v
        rx = 0.275 + 0.11 * v; rz = 0.275 + 0.07 * v; oro = []; iro = []; wro = []
        for j in range(cols):
            ph = math.radians(-112 + 224 * j / (cols - 1)); hem = 0.0
            if i == rows - 1: hem = rng.uniform(0.0, 0.09) * (1 if j % 2 else 0.4)
            ex = 1.0 + 0.02 * math.sin(j * 2.3 + i)
            ox = rx * ex * math.sin(ph); oz = cz - rz * ex * math.cos(ph); iy = cy - hem
            oro.append(Vector((ox, iy, oz))); iro.append(Vector((ox * (1 - 0.04 / rx), iy, cz + (oz - cz) * (1 - 0.04 / rz))))
            wro.append({'chest': 1 - v * 0.75, 'hips': v * 0.75} if v < 0.99 else {'chest': 0.2, 'hips': 0.8})
        outer.append(oro); inner.append(iro); wg.append(wro)
    b.shell(outer, inner, wg)
    b.mat = 3   # rope belt with a knot, the key ring and keys on the right hip
    b.loop((0, 0.585, -0.045), (0, 0.76, 0.65), (0.268, 0.268), 0.022, {'spine': 1}, segs=22, rsegs=6, mat=3)
    b.blob((0, 0.585, 0.205), (0.04, 0.035, 0.03), {'spine': 1}, seg=8, rings=5)
    b.mat = 4
    b.loop((0.215, 0.525, 0.02), (1, 0, 0), (0.05, 0.05), 0.007, {'key1': 1}, segs=14, rsegs=5, mat=4)
    for kx, kz, kl, kr in ((0.205, 0.04, 0.15, 0.011), (0.235, 0.0, 0.13, 0.010), (0.225, 0.075, 0.115, 0.012)):
        b.tube([(kx, 0.50, kz), (kx + 0.005, 0.50 - kl * 0.55, kz + 0.004), (kx + 0.01, 0.50 - kl, kz + 0.008)], [kr, kr, kr], ['key1', 'key2'], ring=5, sub=2, dome=0.3)
        b.blob((kx, 0.512, kz), (0.026, 0.026, 0.008), {'key1': 1}, seg=8, rings=4)
        b.blob((kx + 0.012, 0.50 - kl, kz + 0.012), (0.014, 0.013, 0.006), {'key2': 1}, seg=6, rings=3)
    body = b.finish(rig)

    # ---- Head: 0 fur, 1 skin, 2 ember eye, 3 tooth
    h = MB('Head', [M['fur'], M['skin'], M['eye'], M['tooth']])
    wh = {'head': 1}
    h.blob((0, 0.96, 0.335), (0.152, 0.142, 0.165), wh, seg=14, rings=8)
    h.tube([(0, 0.955, 0.44), (0, 0.935, 0.52), (0, 0.912, 0.60)], [(0.105, 0.088), (0.075, 0.064), (0.04, 0.036)], ['head'], ring=12, sub=4, dome=0.35)
    h.mat = 1; h.blob((0, 0.915, 0.625), (0.03, 0.026, 0.03), wh, seg=8, rings=5)
    h.mat = 3
    for sx in (-1, 1):
        h.tube([(sx * 0.012, 0.888, 0.595), (sx * 0.012, 0.842, 0.605)], [(0.011, 0.007), (0.004, 0.003)], ['head'], ring=5, sub=1, dome=0.0)
    h.mat = 0   # lower jaw, with its two teeth up
    h.tube([(0, 0.905, 0.385), (0, 0.872, 0.50), (0, 0.862, 0.585)], [(0.075, 0.04), (0.054, 0.032), (0.03, 0.022)], ['jaw'], ring=10, sub=3, dome=0.4, wfix={'jaw': 1})
    h.mat = 3
    for sx in (-1, 1):
        h.tube([(sx * 0.011, 0.874, 0.578), (sx * 0.011, 0.9, 0.583)], [(0.007, 0.005), (0.003, 0.002)], ['jaw'], ring=4, sub=1, dome=0.0, wfix={'jaw': 1})
    h.mat = 2
    for sx in (-1, 1):   # ember eyes under a heavy brow
        h.blob((sx * 0.098, 0.99, 0.42), (0.032, 0.034, 0.03), wh, seg=10, rings=6)
    h.mat = 0
    for sx in (-1, 1):
        h.blob((sx * 0.085, 1.032, 0.425), (0.055, 0.016, 0.034), wh, rot=(0, 0, sx * -24), seg=8, rings=4)
    h.mat = 3
    for sx, sd in ((-1, 'L'), (1, 'R')):   # whiskers
        for k, (dz, dy) in enumerate(((0.10, 0.03), (0.2, 0.0), (0.12, -0.04))):
            x0 = sx * 0.05
            h.tube([(x0, 0.925 + 0.006 * k, 0.575), (x0 + sx * 0.08, 0.925 + dy * 0.5, 0.575 + dz * 0.3), (x0 + sx * 0.15, 0.925 + dy, 0.575 + dz * 0.15 - 0.03)], [0.0022, 0.0018, 0.001], ['head'], ring=4, sub=3, dome=0.0)
        h.mat = 1   # ear: pink disc with a fur back
        h.blob((sx * 0.158, 1.0, 0.27), (0.012, 0.072, 0.066), {'ear_' + sd: 1}, rot=(0, sx * -22, sx * -20), seg=10, rings=6)
        h.mat = 0
        h.blob((sx * 0.151, 1.0, 0.262), (0.009, 0.074, 0.068), {'ear_' + sd: 1}, rot=(0, sx * -22, sx * -20), seg=10, rings=6)
        h.mat = 3
    head = h.finish(rig)

    # ---- Crown: 0 rust tin band, 1 brass wire, 2 spoons
    c = MB('Crown', [M['tin'], M['brass'], M['spoon']]); wc = {'crown': 1}
    rows, cols = 3, 28; outer = []; inner = []; wg = []
    for i in range(rows):
        v = i / (rows - 1); oro = []; iro = []; wro = []
        for j in range(cols):
            ph = 2 * math.pi * j / cols; jag = 0.0
            if i == rows - 1: jag = 0.018 * (1 if (j // 2) % 2 else 0) + rng.uniform(0, 0.006)
            y = 0.995 + 0.105 * v - jag + (0.01 * math.cos(ph) * v)
            oro.append(Vector((0.150 * math.sin(ph), y, 0.335 + 0.158 * math.cos(ph)))); iro.append(Vector((0.142 * math.sin(ph), y, 0.335 + 0.150 * math.cos(ph)))); wro.append(wc)
        outer.append(oro); inner.append(iro); wg.append(wro)
    c.shell(outer, inner, wg, wrap=True)
    c.mat = 1
    for yy in (1.02, 1.075):
        c.loop((0, yy, 0.335), (0, 1, 0), (0.153, 0.161), 0.0045, wc, segs=24, rsegs=4, mat=1)
    c.blob((0, 1.045, 0.335 + 0.158), (0.034, 0.03, 0.01), wc, seg=8, rings=4)
    c.mat = 2
    for k in range(8):
        ph = 2 * math.pi * (k + 0.5) / 8; ca, sa = math.cos(ph), math.sin(ph)
        rad_in = 0.8 + 0.1 * (k % 2); L = 0.14 + 0.05 * ((k * 3) % 4) / 3
        base = Vector((0.150 * rad_in * sa, 1.03, 0.335 + 0.158 * rad_in * ca)); out = Vector((sa, 0, ca)); tan = Vector((ca, 0, -sa))
        d = (Vector((0, 1, 0)) + out * (0.20 + 0.06 * (k % 3))).normalized()
        mid = base + d * (L * 0.55); tip = base + d * L
        c.tube([base, mid], [(0.0065, 0.0065), (0.006, 0.006)], ['crown'], ring=5, sub=1, dome=0.0, wfix=wc)
        c.tube([mid, mid + d * (L * 0.25), tip], [(0.012, 0.005), (0.026, 0.0065), (0.004, 0.004)], ['crown'], ring=8, sub=3, side=tan, dome=0.4, wfix=wc)
    crown = c.finish(rig)

    # ---- Tail: 0 skin (annulated)
    t = MB('Tail', [M['skin']])
    t.tube([TAIL_PTS[0]] + TAIL_PTS[1:], [0.06, 0.044, 0.032, 0.022, 0.014, 0.008], ['tail1', 'tail2', 'tail3', 'tail4', 'tail5'], ring=10, sub=5, side=(1, 0, 0), dome=0.5, ann=0.07)
    tail = t.finish(rig)
    return body, head, crown, tail


def build_rig():
    ad = bpy.data.armatures.new('RatKingRig'); rig = bpy.data.objects.new('RatKingRig', ad); bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig; rig.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
    eb = {}
    for n, par, h, t in BONES:
        e = ad.edit_bones.new(n); e.head = U(*h); e.tail = U(*t); eb[n] = e
        if par: e.parent = eb[par]
        e.use_deform = n != 'root'
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig


# ----------------------------------------------------------------------------------------------------------------------------- animation
class Curve:
    """Monotone cubic (pchip) through (frame, value); holds the end values outside."""
    def __init__(self, pts):
        d = {}
        for f, v in pts: d[f] = v
        self.x = sorted(d); self.y = [d[f] for f in self.x]; n = len(self.x)
        if n == 1: self.m = [0.0]; return
        h = [self.x[i + 1] - self.x[i] for i in range(n - 1)]; de = [(self.y[i + 1] - self.y[i]) / h[i] for i in range(n - 1)]
        if n == 2: self.m = [de[0], de[0]]; return
        m = [0.0] * n
        for k in range(1, n - 1):
            if de[k - 1] * de[k] > 0:
                w1 = 2 * h[k] + h[k - 1]; w2 = h[k] + 2 * h[k - 1]; m[k] = (w1 + w2) / (w1 / de[k - 1] + w2 / de[k])
        def end(h0, h1, d0, d1):
            e = ((2 * h0 + h1) * d0 - h0 * d1) / (h0 + h1)
            if e * d0 <= 0: return 0.0
            if d0 * d1 <= 0 and abs(e) > 3 * abs(d0): return 3 * d0
            return e
        m[0] = end(h[0], h[1], de[0], de[1]); m[-1] = end(h[-1], h[-2], de[-1], de[-2]); self.m = m

    def __call__(self, t):
        x, y, m = self.x, self.y, self.m
        if t <= x[0]: return y[0]
        if t >= x[-1]: return y[-1]
        i = bisect.bisect_right(x, t) - 1; h = x[i + 1] - x[i]; s = (t - x[i]) / h
        h00 = 2 * s ** 3 - 3 * s ** 2 + 1; h10 = s ** 3 - 2 * s ** 2 + s; h01 = -2 * s ** 3 + 3 * s ** 2; h11 = s ** 3 - s ** 2
        return h00 * y[i] + h10 * h * m[i] + h01 * y[i + 1] + h11 * h * m[i + 1]


BCOMP = ('p', 'y', 'r')       # pitch (+ = bow forward / swing a hanging limb back / open the jaw), yaw (+ = turn left), roll (+ = lean left): degrees
LCOMP = ('ta', 'sa', 'fa')    # a leg: thigh angle, shin bend, toe lift; the foot stays planted
DELAY = {'head': 1, 'jaw': 0, 'ear_L': 3, 'ear_R': 3, 'crown': 3, 'key1': 3, 'key2': 5, 'tail2': 2, 'tail3': 4, 'tail4': 6, 'tail5': 8,
         'forearm_L': 1, 'forearm_R': 1, 'hand_L': 2, 'hand_R': 2}
TAILS = ['tail1', 'tail2', 'tail3', 'tail4', 'tail5']


def _expand(tgt):
    """'ear_S' -> both sides (yaw and roll mirrored); 'leg_S' -> legL, legR."""
    if tgt.endswith('_S'):
        base = tgt[:-2]
        return [(base + '_L' if base != 'leg' else 'legL', 1), (base + '_R' if base != 'leg' else 'legR', -1)]
    return [(tgt, 1)]


class Clip:
    def __init__(self, name, length, loop=False):
        self.name = name; self.length = length; self.loop = loop; self.keys = {}; self.fns = {}; self.wob = []; self.curves = None; self.tailfollow = True

    def key(self, frame, d):
        for tgt, tup in d.items():
            for real, sgn in _expand(tgt):
                comps = LCOMP if real.startswith('leg') else (('lx', 'ly', 'lz') if real == 'loc' else BCOMP)
                for i, (c, v) in enumerate(zip(comps, tup)):
                    if sgn < 0 and c in ('y', 'r'): v = -v
                    self.keys.setdefault((real, c), []).append((frame, v))

    def fn(self, tgt, comp, f):
        for real, sgn in _expand(tgt):
            self.fns[(real, comp)] = (lambda t, f=f, sgn=sgn: f(t) * (sgn if comp in ('y', 'r') else 1))

    def wobble(self, tgt, comp, f0, f1, amp, period, phase=0.0):
        for real, sgn in _expand(tgt): self.wob.append((real, comp, f0, f1, amp, period, phase))

    def finalize(self):
        self.curves = {}
        for ch, pts in self.keys.items():
            fr = {f for f, _ in pts}
            if 0 not in fr: pts = pts + [(0, 0.0)]
            if self.length not in fr: pts = pts + [(self.length, 0.0)]
            self.curves[ch] = Curve(pts)

    def value(self, ch, t):
        if ch in self.fns: v = self.fns[ch](t)
        elif ch in self.curves: v = self.curves[ch](t - DELAY.get(ch[0], 0) if self.keys is not None and not self.loop else t)
        else: v = 0.0
        for (tg, c, f0, f1, amp, per, ph) in self.wob:
            if (tg, c) == ch and f0 <= t <= f1:
                env = min(1.0, (t - f0) / 3.0, (f1 - t) / 3.0); v += amp * env * math.sin(2 * math.pi * (t - f0) / per + ph)
        return v


def solve(clip, t):
    """Clip -> {bone: (pitch, yaw, roll)} and hips offset (Unity metres) at time t (frames)."""
    out = {b: [0.0, 0.0, 0.0] for b in BONE_NAMES}
    for b in BONE_NAMES:
        for i, c in enumerate(BCOMP): out[b][i] = clip.value((b, c), t)
    hp = out['hips'][0]; hy = out['hips'][1]
    for sd in ('L', 'R'):
        ta = clip.value(('leg' + sd, 'ta'), t); sa = clip.value(('leg' + sd, 'sa'), t); fa = clip.value(('leg' + sd, 'fa'), t)
        out['thigh_' + sd][0] += ta - hp; out['thigh_' + sd][1] += -hy; out['shin_' + sd][0] += sa; out['foot_' + sd][0] += -(ta + sa) + fa
    if clip.tailfollow:
        t1y = lambda tt: clip.value(('tail1', 'y'), tt)
        for i, n in enumerate(TAILS[1:], 1):
            lag = 3.0 * i
            out[n][1] += max(-38.0, min(38.0, -0.55 * (t1y(t) - t1y(t - 3))))
            out[n][0] += 0.35 * clip.value(('tail1', 'p'), t - 2 * i)
    loc = (clip.value(('loc', 'lx'), t), clip.value(('loc', 'ly'), t), clip.value(('loc', 'lz'), t))
    return out, loc


_RB = {}
def apply_pose(rig, clip, t):
    vals, loc = solve(clip, t)
    for n in BONE_NAMES:
        pb = rig.pose.bones[n]
        if n not in _RB: _RB[n] = rig.data.bones[n].matrix_local.to_3x3()
        Rb = _RB[n]; p, y, r = vals[n]
        D = Matrix.Rotation(math.radians(y), 3, 'Z') @ Matrix.Rotation(math.radians(r), 3, 'Y') @ Matrix.Rotation(math.radians(p), 3, 'X')
        pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (Rb.inverted() @ D @ Rb).to_quaternion()
        if n == 'hips': pb.location = Rb.inverted() @ U(*loc)


def reset_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)


def make_clips():
    S = lambda w: 2 * math.pi * w
    clips = []

    # ---- Idle: breathing, a restless sniff, ears and tail drifting, keys and crown settling. Loops on 90 frames.
    c = Clip('Idle', 90, loop=True); c.tailfollow = False; w = lambda t: S(t / 90.0)
    c.fn('chest', 'p', lambda t: 1.8 * math.sin(w(t))); c.fn('spine', 'p', lambda t: 1.0 * math.sin(w(t) + 0.3)); c.fn('neck', 'p', lambda t: -1.2 * math.sin(w(t) + 0.6))
    c.fn('loc', 'ly', lambda t: 0.009 * math.sin(w(t))); c.fn('hips', 'r', lambda t: 0.8 * math.sin(w(t) + 1.0))
    c.fn('head', 'p', lambda t: 4.0 * math.sin(4 * w(t)) * (0.55 + 0.45 * math.sin(w(t))) - 1.5 * math.sin(w(t)))
    c.fn('head', 'y', lambda t: 7.0 * math.sin(2 * w(t) + 1.0)); c.fn('head', 'r', lambda t: 2.0 * math.sin(w(t) + 2.0))
    c.fn('jaw', 'p', lambda t: 2.5 * (0.5 + 0.5 * math.sin(4 * w(t) + 0.4)) ** 2 * 2)
    c.fn('ear_S', 'r', lambda t: 4.0 * math.sin(3 * w(t) + 0.5)); c.fn('ear_S', 'p', lambda t: 3.0 * math.sin(2 * w(t)))
    c.fn('crown', 'p', lambda t: 1.6 * math.sin(w(t) - 0.9)); c.fn('crown', 'r', lambda t: 1.2 * math.sin(2 * w(t) - 0.5))
    c.fn('key1', 'p', lambda t: 3.0 * math.sin(w(t) - 0.5)); c.fn('key2', 'p', lambda t: 5.0 * math.sin(w(t) - 1.1))
    c.fn('upperarm_L', 'p', lambda t: 2.5 * math.sin(w(t) + 0.5)); c.fn('upperarm_R', 'p', lambda t: 2.5 * math.sin(w(t) + 0.9))
    c.fn('forearm_S', 'p', lambda t: -3.0 * (0.5 + 0.5 * math.sin(w(t) + 1.2))); c.fn('hand_S', 'p', lambda t: 4.0 * math.sin(2 * w(t) + 0.3))
    for i, n in enumerate(TAILS):
        c.fn(n, 'y', lambda t, i=i: (7 + 2.5 * i) * math.sin(w(t) - 0.55 * i)); c.fn(n, 'p', lambda t, i=i: 3.0 * math.sin(2 * w(t) - 0.4 * i) + (2.5 if i == 0 else 0))
    clips.append(c)

    # ---- Move: a hunched scurry, one gait cycle in 20 frames (two steps), tail streaming, ears flopping, keys jingling.
    c = Clip('Move', 20, loop=True); c.tailfollow = False; w = lambda t: S(t / 20.0)
    c.fn('spine', 'p', lambda t: 9.0 + 1.5 * math.sin(2 * w(t))); c.fn('chest', 'p', lambda t: 6.0); c.fn('neck', 'p', lambda t: -7.0); c.fn('head', 'p', lambda t: -4.0 + 3.0 * math.sin(2 * w(t) + 0.5))
    c.fn('loc', 'ly', lambda t: -0.035 + 0.03 * math.sin(2 * w(t) + 0.6)); c.fn('loc', 'lz', lambda t: 0.04)
    c.fn('hips', 'y', lambda t: 7.0 * math.sin(w(t))); c.fn('hips', 'r', lambda t: 4.0 * math.sin(w(t) + 0.5)); c.fn('chest', 'y', lambda t: -9.0 * math.sin(w(t))); c.fn('head', 'y', lambda t: 5.0 * math.sin(w(t) + 1.0))
    c.fn('jaw', 'p', lambda t: 6.0 + 3.0 * math.sin(2 * w(t) + 1.0))
    for sd, ph in (('L', 0.0), ('R', math.pi)):
        c.fn('leg' + sd, 'ta', lambda t, ph=ph: -8.0 + 32.0 * math.sin(w(t) + ph))
        c.fn('leg' + sd, 'sa', lambda t, ph=ph: 28.0 + 26.0 * math.cos(w(t) + ph + 0.9))
        c.fn('leg' + sd, 'fa', lambda t, ph=ph: 10.0 * max(0.0, math.cos(w(t) + ph + 0.4)))
        sg = 1 if sd == 'L' else -1
        c.fn('upperarm_' + sd, 'p', lambda t, ph=ph: -14.0 - 22.0 * math.sin(w(t) + ph + math.pi)); c.fn('forearm_' + sd, 'p', lambda t, ph=ph: -48.0 - 12.0 * math.sin(w(t) + ph + math.pi))
        c.fn('hand_' + sd, 'p', lambda t: -10.0)
    c.fn('ear_S', 'p', lambda t: -10.0 + 8.0 * math.sin(2 * w(t) - 0.5)); c.fn('ear_S', 'r', lambda t: 6.0 + 4.0 * math.sin(2 * w(t)))
    c.fn('crown', 'p', lambda t: 4.0 * math.sin(2 * w(t) - 1.0)); c.fn('key1', 'p', lambda t: 12.0 * math.sin(2 * w(t) - 1.0)); c.fn('key2', 'p', lambda t: 22.0 * math.sin(2 * w(t) - 1.7))
    for i, n in enumerate(TAILS):
        c.fn(n, 'y', lambda t, i=i: (16 + 4 * i) * math.sin(w(t) - 0.75 * i)); c.fn(n, 'p', lambda t, i=i: (14.0 if i == 0 else 3.0) + 4.0 * math.sin(2 * w(t) - 0.5 * i))
    clips.append(c)

    # ---- Hit: a flinch that rocks the body back, jaw yelping, tail snapping up; settles on the bind pose by f20.
    c = Clip('Hit', 20)
    c.key(3, {'loc': (0, -0.035, -0.075), 'hips': (-4, 0, 3), 'spine': (-8, 0, 0), 'chest': (-8, 0, 0), 'neck': (0, 0, 0), 'head': (6, 0, 5), 'jaw': (30, 0, 0),
              'leg_S': (-18, 28, 0), 'upperarm_S': (-34, 0, 6), 'forearm_S': (-30, 0, 0), 'hand_S': (-20, 0, 0), 'ear_S': (-24, 0, -10), 'tail1': (24, 12, 0), 'crown': (-14, 0, 6), 'key1': (-14, 0, 0), 'key2': (-24, 0, 0)})
    c.key(8, {'loc': (0, -0.02, -0.045), 'hips': (-2, 0, 1), 'spine': (-5, 0, 0), 'chest': (-5, 0, 0), 'neck': (0, 0, 0), 'head': (3, 0, 2), 'jaw': (13, 0, 0),
              'leg_S': (-8, 14, 0), 'upperarm_S': (-18, 0, 3), 'forearm_S': (-16, 0, 0), 'ear_S': (-12, 0, -4), 'tail1': (12, -8, 0), 'crown': (6, 0, -3)})
    c.key(14, {'loc': (0, 0.004, 0.01), 'spine': (2, 0, 0), 'chest': (2, 0, 0), 'head': (3, 0, 0), 'jaw': (1, 0, 0), 'tail1': (3, 4, 0), 'crown': (-2, 0, 1)})
    c.key(20, {})
    clips.append(c)

    # ---- Death: recoil, the knees go, a tip onto the left side, head drops, crown rolls off-centre. Held from f60.
    c = Clip('Death', 72)
    c.key(6, {'loc': (0, -0.02, -0.11), 'hips': (-6, 0, 0), 'spine': (-14, 0, 0), 'chest': (-12, 0, 0), 'neck': (0, 0, 0), 'head': (6, 0, 6), 'jaw': (38, 0, 0), 'leg_S': (-12, 18, 0),
              'upperarm_S': (-48, 0, 12), 'forearm_S': (-28, 0, 0), 'hand_S': (-20, 0, 0), 'ear_S': (-26, 0, -12), 'tail1': (30, 10, 0), 'crown': (-14, 0, 8)})
    c.key(18, {'loc': (0, -0.12, -0.12), 'hips': (-4, 0, 14), 'spine': (-4, 0, 6), 'chest': (0, 0, 0), 'neck': (2, 0, 0), 'head': (-4, 0, 10), 'jaw': (24, 0, 0), 'leg_S': (-38, 62, -10),
              'upperarm_S': (-34, 0, 14), 'forearm_S': (-24, 0, 0), 'ear_S': (-10, 0, -8), 'tail1': (4, 18, 0), 'crown': (4, 0, 14)})
    c.key(34, {'loc': (0, -0.26, -0.02), 'hips': (-6, 0, 68), 'spine': (16, 0, 6), 'chest': (14, 0, 6), 'neck': (10, 0, 4), 'head': (12, 10, 10), 'jaw': (20, 0, 0), 'leg_S': (-46, 72, -16),
              'upperarm_L': (-8, 0, 20), 'upperarm_R': (-24, 0, -40), 'forearm_S': (-20, 0, 0), 'hand_S': (-10, 0, 0), 'ear_S': (8, 0, 6), 'tail1': (-8, 28, 0), 'crown': (10, 0, 20)})
    c.key(50, {'loc': (0, -0.30, 0.0), 'hips': (-6, 0, 80), 'spine': (20, 0, 8), 'chest': (16, 0, 8), 'neck': (12, 0, 6), 'head': (14, 14, 16), 'jaw': (16, 0, 0), 'leg_S': (-50, 78, -20),
              'upperarm_L': (-4, 0, 22), 'upperarm_R': (-30, 0, -46), 'forearm_S': (-16, 0, 0), 'ear_S': (14, 0, 12), 'tail1': (-12, 32, 0), 'crown': (14, 0, 26), 'key1': (10, 0, 6)})
    c.key(60, {'loc': (0, -0.305, 0.0), 'hips': (-6, 0, 82), 'spine': (20, 0, 8), 'chest': (16, 0, 8), 'neck': (12, 0, 6), 'head': (14, 14, 16), 'jaw': (14, 0, 0), 'leg_S': (-50, 78, -20),
              'upperarm_L': (-4, 0, 22), 'upperarm_R': (-30, 0, -46), 'forearm_S': (-16, 0, 0), 'ear_S': (16, 0, 14), 'tail1': (-12, 32, 0), 'crown': (16, 0, 28), 'key1': (10, 0, 6)})
    c.key(72, {'loc': (0, -0.305, 0.0), 'hips': (-6, 0, 82), 'spine': (20, 0, 8), 'chest': (16, 0, 8), 'neck': (12, 0, 6), 'head': (14, 14, 16), 'jaw': (14, 0, 0), 'leg_S': (-50, 78, -20),
              'upperarm_L': (-4, 0, 22), 'upperarm_R': (-30, 0, -46), 'forearm_S': (-16, 0, 0), 'ear_S': (16, 0, 14), 'tail1': (-12, 32, 0), 'crown': (16, 0, 28), 'key1': (10, 0, 6)})
    clips.append(c)

    # ---- Attack_rat_lunge (62): rear back on the haunches (tell peak f8), glint f14, spring f16-28, bite f28-36, recover to f62.
    c = Clip('Attack_rat_lunge', 62)
    rear = {'loc': (0, -0.10, -0.09), 'hips': (-14, 0, 0), 'spine': (-8, 0, 0), 'chest': (-6, 0, 0), 'neck': (2, 0, 0), 'head': (14, 0, 0), 'jaw': (5, 0, 0), 'leg_S': (-34, 58, 0),
            'upperarm_S': (-52, 0, 8), 'forearm_S': (-46, 0, 0), 'hand_S': (-20, 0, 0), 'ear_S': (-18, 0, -8), 'tail1': (26, 0, 0), 'crown': (-8, 0, 0), 'key1': (-10, 0, 0), 'key2': (-16, 0, 0)}
    c.key(8, rear)
    c.key(14, dict(rear, **{'loc': (0, -0.11, -0.10), 'hips': (-16, 0, 0), 'head': (8, 0, 0), 'jaw': (16, 0, 0), 'crown': (-14, 0, 0), 'ear_S': (-26, 0, -10)}))
    c.key(17, dict(rear, **{'loc': (0, -0.12, -0.12), 'hips': (-18, 0, 0), 'spine': (-9, 0, 0), 'head': (10, 0, 0), 'jaw': (18, 0, 0), 'leg_S': (-38, 66, 0), 'crown': (-14, 0, 0), 'ear_S': (-30, 0, -10), 'tail1': (30, 0, 0)}))
    c.key(22, {'loc': (0, -0.02, 0.17), 'hips': (10, 0, 0), 'spine': (7, 0, 0), 'chest': (6, 0, 0), 'neck': (-8, 0, 0), 'head': (-10, 0, 0), 'jaw': (32, 0, 0), 'leg_S': (34, 6, 8),
               'upperarm_S': (-64, 0, 14), 'forearm_S': (-18, 0, 0), 'hand_S': (-28, 0, 0), 'ear_S': (-38, 0, -6), 'tail1': (12, 0, 0), 'crown': (10, 0, 0), 'key1': (14, 0, 0), 'key2': (20, 0, 0)})
    c.key(28, {'loc': (0, 0.0, 0.27), 'hips': (12, 0, 0), 'spine': (10, 0, 0), 'chest': (8, 0, 0), 'neck': (-12, 0, 0), 'head': (-18, 0, 0), 'jaw': (50, 0, 0), 'leg_S': (46, -4, 14),
               'upperarm_S': (-72, 0, 18), 'forearm_S': (-26, 0, 0), 'hand_S': (-30, 0, 0), 'ear_S': (-40, 0, -6), 'tail1': (22, 0, 0), 'crown': (12, 0, 0), 'key1': (10, 0, 0), 'key2': (16, 0, 0)})
    c.key(32, {'loc': (0, 0.0, 0.31), 'hips': (12, 0, 0), 'spine': (10, 0, 0), 'chest': (8, 0, 0), 'neck': (-12, 0, 0), 'head': (-12, 0, 0), 'jaw': (-3, 0, 0), 'leg_S': (48, -4, 14),
               'upperarm_S': (-72, 0, 18), 'forearm_S': (-30, 0, 0), 'hand_S': (-30, 0, 0), 'ear_S': (-42, 0, -6), 'tail1': (24, 0, 0), 'crown': (16, 0, 0), 'key1': (14, 0, 0), 'key2': (20, 0, 0)})
    c.key(36, {'loc': (0, 0.0, 0.30), 'hips': (12, 0, 0), 'spine': (10, 0, 0), 'chest': (8, 0, 0), 'neck': (-12, 0, 0), 'head': (-14, 0, 0), 'jaw': (3, 0, 0), 'leg_S': (48, -4, 14),
               'upperarm_S': (-70, 0, 18), 'forearm_S': (-30, 0, 0), 'hand_S': (-30, 0, 0), 'ear_S': (-40, 0, -6), 'tail1': (22, 0, 0), 'crown': (10, 0, 0)})
    c.key(46, {'loc': (0, -0.03, 0.12), 'hips': (6, 0, 0), 'spine': (4, 0, 0), 'chest': (3, 0, 0), 'neck': (-3, 0, 0), 'head': (-4, 0, 0), 'jaw': (7, 0, 0), 'leg_S': (14, 10, 0), 'upperarm_S': (-26, 0, 4), 'forearm_S': (-16, 0, 0), 'ear_S': (-14, 0, -2), 'tail1': (8, 0, 0)})
    c.key(56, {'loc': (0, 0.004, 0.0), 'head': (-1, 0, 0), 'tail1': (2, 0, 0)})
    c.wobble('head', 'r', 8, 14, 1.4, 3.0); c.wobble('chest', 'r', 8, 14, 1.2, 3.0, 1.0); c.wobble('crown', 'r', 8, 17, 3.0, 3.0, 0.6)
    clips.append(c)

    # ---- Attack_rat_spin (62): coil to the left (tell peak f12), tail raised and glinting (commit f16), then the tail whips the full circle f20-46.
    c = Clip('Attack_rat_spin', 62); c.tailfollow = True
    coil = {'loc': (0, -0.07, -0.02), 'hips': (2, 28, 4), 'spine': (-2, 22, 0), 'chest': (-2, 20, 0), 'neck': (0, 8, 0), 'head': (-4, 12, 2), 'jaw': (6, 0, 0), 'leg_S': (-24, 38, 0),
            'upperarm_S': (-22, 0, 10), 'forearm_S': (-70, 0, 0), 'hand_S': (-24, 0, 0), 'ear_S': (-14, 0, -6), 'crown': (-5, 0, 0)}
    c.key(12, dict(coil, tail1=(36, 0, 0)))
    c.key(16, dict(coil, loc=(0, -0.075, -0.02), hips=(2, 31, 4), jaw=(10, 0, 0), tail1=(40, 0, 0)))
    c.key(20, dict(coil, loc=(0, -0.08, -0.02), hips=(2, 36, 4), spine=(-2, 28, 0), chest=(-2, 24, 0), head=(-4, 16, 2), jaw=(14, 0, 0), tail1=(38, 0, 0)))
    c.key(30, {'loc': (0, -0.06, 0.02), 'hips': (2, -5, -3), 'spine': (-1, -4, 0), 'chest': (-1, -4, 0), 'neck': (0, -6, 0), 'head': (-2, -6, 0), 'jaw': (18, 0, 0), 'leg_S': (-22, 36, 0),
               'upperarm_S': (-30, 0, 24), 'forearm_S': (-50, 0, 0), 'ear_S': (-34, 0, -6), 'crown': (6, 0, 0), 'tail1': (24, 0, 0)})
    c.key(40, {'loc': (0, -0.05, 0.02), 'hips': (2, -34, -4), 'spine': (-1, -26, 0), 'chest': (-1, -24, 0), 'neck': (0, -12, 0), 'head': (-2, -16, 0), 'jaw': (12, 0, 0), 'leg_S': (-20, 34, 0),
               'upperarm_S': (-24, 0, 30), 'forearm_S': (-40, 0, 0), 'ear_S': (-30, 0, -4), 'crown': (8, 0, 0), 'tail1': (16, 0, 0)})
    c.key(50, {'loc': (0, -0.02, 0.0), 'hips': (1, -14, 0), 'spine': (0, -10, 0), 'chest': (0, -8, 0), 'head': (0, -6, 0), 'jaw': (5, 0, 0), 'leg_S': (-8, 12, 0), 'upperarm_S': (-8, 0, 6), 'forearm_S': (-14, 0, 0), 'tail1': (6, 0, 0)})
    c.key(58, {'tail1': (1, 0, 0)})
    # tail1 yaw: wind to the left (-70), whip a full 360 (+ 2.6 turns of speed in the hit window f30-40), settle on the bind pose
    tl = [(0, 0), (12, -58), (16, -66), (20, -76), (25, -42), (30, 52), (35, 170), (40, 292), (44, 360), (48, 372), (53, 358), (58, 360), (62, 360)]
    c.keys[('tail1', 'y')] = list(tl)
    c.wobble('head', 'r', 12, 17, 1.6, 3.0); c.wobble('tail1', 'r', 12, 18, 1.8, 3.0, 0.5)
    clips.append(c)
    for c in clips: c.finalize()
    return clips


def key_clips(rig, clips):
    ad = rig.animation_data_create(); out = {}
    for clip in clips:
        act = bpy.data.actions.new(clip.name); act.use_fake_user = True; ad.action = act
        prev = {}
        for f in range(0, clip.length + 1):
            apply_pose(rig, clip, float(f))
            for n in BONE_NAMES:
                if n == 'root': continue
                pb = rig.pose.bones[n]; q = pb.rotation_quaternion.copy()
                if n in prev and q.dot(prev[n]) < 0: q.negate(); pb.rotation_quaternion = q
                prev[n] = q
                pb.keyframe_insert('rotation_quaternion', frame=f, group=n)
                if n == 'hips': pb.keyframe_insert('location', frame=f, group=n)
        act.use_frame_range = True; act.frame_start = 0; act.frame_end = clip.length
        if clip.loop: act.use_cyclic = True
        ad.action = None
        tr = ad.nla_tracks.new(); tr.name = clip.name; st = tr.strips.new(clip.name, 0, act); st.name = clip.name
        out[clip.name] = act
    reset_pose(rig)
    return out


# ----------------------------------------------------------------------------------------------------------------------------- preview render + export
PREVIEW = [('rest', 'Idle', 0), ('lunge_tell', 'Attack_rat_lunge', 8), ('lunge_glint', 'Attack_rat_lunge', 14), ('lunge_strike', 'Attack_rat_lunge', 30),
           ('spin_tell', 'Attack_rat_spin', 14), ('spin_whip', 'Attack_rat_spin', 36), ('hit', 'Hit', 4), ('death', 'Death', 72)]


def setup_scene(res):
    sc = bpy.context.scene; sc.render.fps = 60; sc.render.engine = 'CYCLES'; sc.cycles.samples = 28; sc.cycles.use_denoising = True
    sc.render.resolution_x = res; sc.render.resolution_y = res; sc.view_settings.view_transform = 'AgX'
    try:
        pr = bpy.context.preferences.addons['cycles'].preferences
        for dt in ('OPTIX', 'CUDA'):
            try:
                pr.compute_device_type = dt; pr.get_devices()
                if any(d.type == dt for d in pr.devices):
                    for d in pr.devices: d.use = d.type == dt
                    sc.cycles.device = 'GPU'; break
            except Exception: pass
    except Exception: pass
    w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.045, 0.04, 0.038, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 1.0; sc.world = w
    def light(loc, energy, col, size, name):
        ld = bpy.data.lights.new(name, 'AREA'); ld.energy = energy; ld.color = col; ld.size = size; ob = bpy.data.objects.new(name, ld); sc.collection.objects.link(ob); ob.location = U(*loc)
        d = Vector((0, 0, 0.6)) - ob.location; ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    light((1.8, 2.6, 2.4), 140, (1.0, 0.62, 0.30), 1.6, 'key'); light((-2.2, 1.4, 0.6), 25, (0.95, 0.55, 0.28), 1.5, 'fill'); light((0.2, 1.6, -2.4), 90, (1.0, 0.58, 0.25), 1.2, 'rim')
    gm = bpy.data.meshes.new('floor'); gm.from_pydata([(-6, -6, 0), (6, -6, 0), (6, 6, 0), (-6, 6, 0)], [], [(0, 1, 2, 3)]); fo = bpy.data.objects.new('floor', gm); sc.collection.objects.link(fo)
    fm = material('floor', (0.07, 0.06, 0.055), 0.9); fo.data.materials.append(fm)
    cd = bpy.data.cameras.new('cam'); cd.lens = 70; co = bpy.data.objects.new('cam', cd); sc.collection.objects.link(co); sc.camera = co
    tgt = U(0.0, 0.62, -0.20); co.location = tgt + Vector((2.35, -3.6, 0.45)) * 0.95
    co.rotation_euler = (tgt - co.location).to_track_quat('-Z', 'Y').to_euler()
    return fo


def render_previews(rig, clips, outdir, res):
    fo = setup_scene(res); bc = {c.name: c for c in clips}; sc = bpy.context.scene; rig.animation_data.use_nla = False
    for label, cn, f in PREVIEW:
        apply_pose(rig, bc[cn], float(f)); bpy.context.view_layer.update()
        sc.render.filepath = os.path.join(outdir, 'pose_%s.png' % label); bpy.ops.render.render(write_still=True)
    reset_pose(rig); rig.animation_data.use_nla = True
    bpy.data.objects.remove(fo)


def export_fbx(rig, objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in [rig] + list(objs): o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'}, axis_forward='-Z', axis_up='Y', global_scale=1.0, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=False, mesh_smooth_type='OFF', add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
                             armature_nodetype='NULL', bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP')


def main(argv):
    def opt(n, d=None): return argv[argv.index(n) + 1] if n in argv else d
    out = opt('--out'); res = int(opt('--rres', '520')); os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.context.scene.render.fps = 60; bpy.context.scene.render.fps_base = 1.0
    M = make_materials(); rig = build_rig(); objs = build_meshes(rig, M); clips = make_clips(); acts = key_clips(rig, clips)
    tris = 0
    for o in objs:
        o.data.calc_loop_triangles(); n = len(o.data.loop_triangles); tris += n; print('OBJ', o.name, 'tris', n, 'verts', len(o.data.vertices))
    print('TRIS', tris, 'BONES', len(BONE_NAMES), 'CLIPS', {c.name: c.length for c in clips})
    dims = [max(v.co.y for o in objs for v in o.data.vertices), min(v.co.y for o in objs for v in o.data.vertices)]
    print('BLENDER Y extent (front is -Y): min %.3f max %.3f' % (dims[1], dims[0]), 'height', max(v.co.z for o in objs for v in o.data.vertices))
    if '--blend' in argv: bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, 'ratking.blend'))
    if '--export' in argv:
        ed = opt('--export'); os.makedirs(ed, exist_ok=True); export_fbx(rig, objs, os.path.join(ed, NAME + '.fbx')); print('EXPORT', ed, sorted(os.listdir(ed)))
    if '--no-render' not in argv: render_previews(rig, clips, out, res)


# ----------------------------------------------------------------------------------------------------------------------------- sheet (system python + Pillow)
def sheet(argv):
    from PIL import Image, ImageDraw
    d, outp = argv[argv.index('--sheet') + 1], argv[argv.index('--sheet') + 2]
    cp = argv[argv.index('--concept') + 1] if '--concept' in argv else None
    if cp is None:
        for p in ('C:/Dev/SleepingCalamity/Reference/private/concepts/Rat-king.jpg', os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Reference', 'private', 'concepts', 'Rat-king.jpg')):
            if os.path.exists(p): cp = p; break
    labels = {'rest': 'rest (Idle f0)', 'lunge_tell': 'lunge tell peak f8', 'lunge_glint': 'lunge glint f14', 'lunge_strike': 'lunge strike f30', 'spin_tell': 'spin glint f14',
              'spin_whip': 'spin whip f36', 'hit': 'hit f4', 'death': 'death f72 (held)'}
    tiles = []
    for k, _, _ in PREVIEW:
        im = Image.open(os.path.join(d, 'pose_%s.png' % k)).convert('RGB'); ImageDraw.Draw(im).text((10, 8), labels[k], fill=(255, 220, 160)); tiles.append(im)
    tw = tiles[0].width; H = tw * 2; left = None
    if cp and '--no-concept' not in argv:
        c = Image.open(cp).convert('RGB'); c = c.crop((0, 0, int(c.width * 0.62), c.height)); c = c.resize((int(c.width * H / c.height), H)); left = c
    W = (left.width if left else 0) + tw * 4
    S = Image.new('RGB', (W, H), (22, 20, 19)); x0 = 0
    if left:
        S.paste(left, (0, 0)); ImageDraw.Draw(S).text((10, H - 18), 'concept (reference only)', fill=(255, 255, 255)); x0 = left.width
    for i, t in enumerate(tiles): S.paste(t, (x0 + (i % 4) * tw, (i // 4) * tw))
    os.makedirs(os.path.dirname(os.path.abspath(outp)), exist_ok=True); S.save(outp); print('sheet', outp, S.size)


if __name__ == '__main__':
    if '--sheet' in sys.argv: sheet(sys.argv)
    elif HAVE_BPY: main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
