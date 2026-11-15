"""Jam-jar slime: rigged, animated Cellars enemy (T4-lite prop-creature). Generator is the source; the FBX is its output.

What: a glass jam jar (the jar is the body, 1.0 m wide) holding a living blob of dark jam with two cream eyes looking out
through the front glass, a ridged tin lid that sits on the mouth (hinged at the back so it can pop), and a paper label on the back.
It hops, lobs globs off its lid, and on death tips over and pours out into a puddle.
Refs: docs/design/dungeon1-cellars.md, docs/PHASE1.md 3.6 (storybook proportion), sc-asset-craft creatures.md (prop-creatures keep their
object's real construction; character comes from pose and silhouette). No slime concept frame exists (Reference/private/concepts has none).
Real size: 1.0 m wide x 1.0 m tall (EnemyDefinition.Slime Radius 0.55, Height 1.0). Origin at the bottom centre of the jar (feet).
Axes: authored Blender z up, front = -Y (the eyes face -Y); FBX export -Z forward, Y up, apply transforms = Unity front +z, y up.
Budget (PHASE1 3.3 / brief): about 3-6k tris, simple Principled materials (no baked textures: the builder assigns URP materials by the
M_Slime_* names; glass is a transparent material there). Run:

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/enemies/sc_enemy_slime.py -- --out <scratch dir>
      [--export <dir>]  FBX target dir (default Assets/_Project/Content/Art/Enemies/Slime)
      [--render] [--rres 640] [--samples 24]   key-pose panels into <out>/panels (compose with Art/Blender/enemies/sc_enemy_sheet.py)
      [--no-export]

Hierarchy and names (presentation code finds these by name; mesh nodes are the renderers, bones are named *_Bone or rig names):
  SlimeRig (armature): Root > Body > { Jam > JamTop > { EyeL_Bone, EyeR_Bone }, Lid_Bone }, Root > Splat_Bone. All deform bones.
  Skinned meshes (to Body / Jam+JamTop): Jar (glass + label), Jam.
  Rigid meshes parented to bones (own Renderer, move with the bone): Lid (Lid_Bone), EyeL, EyeR, Splat (death puddle; rest scale is
  1 cm wide and Splat_Bone scales it x50 in Death, so it is invisible in every other clip).
Clips (60 fps; frame 1 == one game AttackFrame; keys start at frame 0): Idle 120 (loop), Move 40 (loop), Hit 20, Death 72 (ends held),
  Attack_slime_hop 60 (AttackCatalog.SlimeHop total 60; tell poseEnd 10, commit/glint 16, lunge 16-34, HitOn 34-40),
  Attack_slime_glob 66 (SlimeGlob total 66; tell poseEnd 18, commit/glint 26, HitOn 46-52).
Root never translates in any clip; the hop arc is Body only (the game moves the enemy).
Dated pass lines: 2026-11-15 first pass.
"""
import sys, os, math, random
import bpy, bmesh
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
EXPORT = os.path.abspath(arg('--export', os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Enemies', 'Slime')))
RRES = int(arg('--rres', '640')); SAMPLES = int(arg('--samples', '24'))

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 60
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1.0

# ------------------------------------------------------------------ materials (simple Principled; names are the Unity contract)
def mat(name, base, rough=0.5, metal=0.0, **kw):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*base, 1); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metal
    for k, v in kw.items():
        if k in b.inputs:
            b.inputs[k].default_value = v
    return m, b

M_GLASS, bg = mat('M_Slime_Glass', (0.93, 0.95, 0.88), 0.03, 0.0, **{'Transmission Weight': 1.0, 'IOR': 1.5})
M_JAM, bj = mat('M_Slime_Jam', (0.20, 0.010, 0.030), 0.14, 0.0, **{'Subsurface Weight': 0.22, 'Subsurface Scale': 0.05, 'Coat Weight': 0.6, 'Coat Roughness': 0.05})
bj.inputs['Subsurface Radius'].default_value = (1.0, 0.18, 0.12)
M_LID, bl = mat('M_Slime_Lid', (0.42, 0.31, 0.16), 0.40, 0.9)
M_EYE, be = mat('M_Slime_Eye', (0.96, 0.92, 0.80), 0.10, 0.0, **{'Emission Strength': 0.0})
M_PUPIL, bp = mat('M_Slime_Pupil', (0.04, 0.025, 0.03), 0.08, 0.0)
M_LABEL, bb = mat('M_Slime_Label', (0.80, 0.70, 0.50), 0.85, 0.0)
M_PUDDLE, bd = mat('M_Slime_Puddle', (0.30, 0.02, 0.04), 0.12, 0.0, **{'Coat Weight': 0.7, 'Coat Roughness': 0.04})
# Look-dev extras for the Blender previews only (the FBX carries the base colours): speckled jam, rusty tin.
def noise_to(b, name, scale, c0, c1, detail=6.0):
    nt = b.id_data
    n = nt.nodes.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
    tc = nt.nodes.new('ShaderNodeTexCoord'); nt.links.new(tc.outputs['Object'], n.inputs['Vector'])
    r = nt.nodes.new('ShaderNodeValToRGB'); r.color_ramp.elements[0].color = (*c0, 1); r.color_ramp.elements[1].color = (*c1, 1)
    nt.links.new(n.outputs['Fac'], r.inputs['Fac']); nt.links.new(r.outputs['Color'], b.inputs['Base Color'])
noise_to(bj, 'jam', 38.0, (0.09, 0.004, 0.012), (0.36, 0.022, 0.04))
noise_to(bl, 'tin', 14.0, (0.26, 0.18, 0.09), (0.58, 0.44, 0.24))

# ------------------------------------------------------------------ mesh helpers
def make_obj(name, bm_or_mesh, materials, collection=None):
    me = bm_or_mesh
    ob = bpy.data.objects.new(name, me)
    (collection or scene.collection).objects.link(ob)
    for m in materials: me.materials.append(m)
    return ob

def lathe(name, prof, seg, materials, knurl=None, radial=None, lump=None, center=(0, 0, 0), scale=1.0):
    """Revolve (r, z) profile points around z. r==0 points are single axis vertices. knurl(r, z, th)->r' and lump(r, z, th)->r' bend the surface."""
    bm = bmesh.new(); rings = []
    for (r, z) in prof:
        if r < 1e-6:
            rings.append([bm.verts.new((center[0], center[1], center[2] + z * scale))]); continue
        ring = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            rr = r
            if radial: rr *= radial(th)
            if knurl: rr = knurl(rr, z, th)
            if lump: rr = lump(rr, z, th)
            ring.append(bm.verts.new((center[0] + rr * scale * math.cos(th), center[1] + rr * scale * math.sin(th), center[2] + z * scale)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        if len(a) == 1 and len(b) == 1: continue
        if len(a) == 1:
            for k in range(seg): bm.faces.new((a[0], b[(k + 1) % seg], b[k]))
        elif len(b) == 1:
            for k in range(seg): bm.faces.new((a[k], a[(k + 1) % seg], b[0]))
        else:
            for k in range(seg): bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bm.normal_update()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    return make_obj(name, me, materials)

def tris(ob):
    me = ob.data; me.calc_loop_triangles(); return len(me.loop_triangles)

# ------------------------------------------------------------------ geometry (Blender axes: z up, front -Y), metres
SEG = 40
# Jar: closed profile outside bottom -> lip -> inside -> axis, so one revolve gives a thick-walled glass shell (inner wall normals face the cavity).
JAR_PROF = [(0, 0), (0.30, 0), (0.44, 0.006), (0.485, 0.025), (0.503, 0.06), (0.508, 0.14), (0.508, 0.60), (0.500, 0.68), (0.478, 0.75),
            (0.440, 0.81), (0.405, 0.855), (0.388, 0.885), (0.394, 0.90), (0.394, 0.925), (0.376, 0.932), (0.356, 0.926), (0.350, 0.89),
            (0.372, 0.845), (0.410, 0.80), (0.452, 0.735), (0.474, 0.66), (0.478, 0.60), (0.478, 0.14), (0.472, 0.07), (0.450, 0.045),
            (0.36, 0.036), (0, 0.036)]
jar = lathe('Jar', JAR_PROF, SEG, [M_GLASS, M_LABEL])
# Paper label: a curved strip stuck on the back of the glass (+Y side; the eyes face -Y), added into the Jar mesh as a second material.
def add_label(ob):
    bm = bmesh.new(); bm.from_mesh(ob.data)
    a0, a1 = math.radians(40), math.radians(140); cols = 12; rows = [0.20, 0.42, 0.56]
    zs = [0.20, 0.42, 0.60]; grid = []
    for z in zs:
        row = []
        for c in range(cols + 1):
            th = a0 + (a1 - a0) * c / cols
            r = 0.5105 + (0.004 if c in (0, cols) else 0.0)
            row.append(bm.verts.new((r * math.cos(th), r * math.sin(th), z + 0.01 * math.sin(c * 1.7))))
        grid.append(row)
    for i in range(len(zs) - 1):
        for c in range(cols):
            f = bm.faces.new((grid[i][c], grid[i][c + 1], grid[i + 1][c + 1], grid[i + 1][c])); f.material_index = 1; f.smooth = True
    bm.normal_update(); bm.to_mesh(ob.data); bm.free()
add_label(jar)

# Jam blob: fills the jar to a domed top, lumpy with fruit, seeded.
rnd = random.Random(7)
PH = [(rnd.uniform(0, 6.28), rnd.uniform(0, 6.28), rnd.uniform(0, 6.28)) for _ in range(4)]
def jam_lump(r, z, th):
    d = 0.009 * math.sin(3 * th + 5 * z + PH[0][0]) + 0.007 * math.sin(5 * th - 9 * z + PH[1][1]) + 0.006 * math.sin(2 * th + 13 * z + PH[2][2])
    return r + d * (1.0 if r > 0.05 else 0.0)
JAM_PROF = [(0, 0.045), (0.25, 0.045), (0.375, 0.055), (0.440, 0.09), (0.453, 0.16), (0.455, 0.32), (0.453, 0.50), (0.449, 0.62), (0.438, 0.72),
            (0.410, 0.795), (0.355, 0.84), (0.27, 0.868), (0.15, 0.885), (0, 0.892)]
jam = lathe('Jam', JAM_PROF, SEG, [M_JAM], lump=jam_lump)

# Lid: ridged tin cap, hinged at its back edge (bone Lid_Bone head at (0, 0.40, 0.91)); built around its world position, tilted 2.5 deg ajar.
LID_PROF = [(0, 0.998), (0.12, 0.998), (0.26, 0.994), (0.335, 0.985), (0.362, 0.976), (0.398, 0.971), (0.424, 0.963), (0.433, 0.95), (0.433, 0.917),
            (0.427, 0.905), (0.402, 0.903), (0.392, 0.915), (0, 0.915)]
KN = 36
def knurl(r, z, th): return r + (0.0075 * (0.5 + 0.5 * math.cos(KN * th)) if (r > 0.42 and 0.918 < z < 0.96) else 0.0)
lid = lathe('Lid', LID_PROF, 64, [M_LID], knurl=knurl)
HINGE = Vector((0.0, 0.40, 0.91))
def tilt_about(ob, pivot, rx_deg=0.0, ry_deg=0.0):
    m = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(rx_deg), 4, 'X') @ Matrix.Rotation(math.radians(ry_deg), 4, 'Y') @ Matrix.Translation(-pivot)
    ob.data.transform(m)
tilt_about(lid, HINGE, -2.5, 2.0)

# Eyes: cream ellipsoids with a dark pupil cap on the front (-Y), sitting on the jam just behind the glass.
EYE_C = {'L': Vector((0.150, -0.366, 0.530)), 'R': Vector((-0.150, -0.366, 0.530))}   # slime's left = +x when seen from the front (-Y)... mirrored, symmetric
def eye(side):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=0.112)
    for v in bm.verts: v.co.x *= 0.95; v.co.y *= 0.66; v.co.z *= 1.12
    bm.normal_update()
    for f in bm.faces:
        n = f.normal
        f.material_index = 1 if (n.y < -0.62 and abs(n.x) < 0.55 and abs(n.z) < 0.6) else 0
        f.smooth = True
    me = bpy.data.meshes.new('Eye' + side); bm.to_mesh(me); bm.free()
    me.transform(Matrix.Translation(EYE_C[side]))
    return make_obj('Eye' + side, me, [M_EYE, M_PUPIL])
eyeL, eyeR = eye('L'), eye('R')

# Splat: the death puddle, authored 1 cm wide; Splat_Bone scales it x50 in Death (rest scale hides it in every other clip).
PUD_C = Vector((1.12, -0.05, 0.0)); PUD_K = 1.0 / 50.0
prn = random.Random(3); PR = [prn.uniform(0.82, 1.18) for _ in range(9)]
def pud_radial(th):
    t = th / (2 * math.pi) * 9; i = int(t) % 9; f = t - int(t); return PR[i] * (1 - f) + PR[(i + 1) % 9] * f
splat = lathe('Splat', [(0, 0.062), (0.20, 0.060), (0.36, 0.048), (0.48, 0.026), (0.545, 0.004), (0.55, 0.0), (0, 0.0)], 36, [M_PUDDLE],
              radial=pud_radial, center=(PUD_C.x * PUD_K, PUD_C.y * PUD_K, 0.0), scale=PUD_K)
# the puddle is built around the bone head at the world position PUD_C; the bone scales about that head.
splat.data.transform(Matrix.Translation(PUD_C * (1 - PUD_K)))   # undo the K scale on the centre offset: centre at PUD_C, size 1 cm

# ------------------------------------------------------------------ armature (all bones point +Y, roll 0, so pose axes == world axes)
arm_data = bpy.data.armatures.new('SlimeRig'); arm = bpy.data.objects.new('SlimeRig', arm_data); scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
BONES = {   # name: (head, parent)
    'Root': ((0, 0, 0), None), 'Body': ((0, 0, 0), 'Root'), 'Jam': ((0, 0, 0.05), 'Body'), 'JamTop': ((0, 0, 0.55), 'Jam'),
    'EyeL_Bone': (tuple(EYE_C['L']), 'JamTop'), 'EyeR_Bone': (tuple(EYE_C['R']), 'JamTop'),
    'Lid_Bone': (tuple(HINGE), 'Body'), 'Splat_Bone': (tuple(PUD_C), 'Root'),
}
for n, (h, p) in BONES.items():
    eb = arm_data.edit_bones.new(n); eb.head = h; eb.tail = (h[0], h[1] + 0.10, h[2]); eb.roll = 0.0; eb.use_deform = True
for n, (h, p) in BONES.items():
    if p: arm_data.edit_bones[n].parent = arm_data.edit_bones[p]
bpy.ops.object.mode_set(mode='OBJECT')

def skin(ob, weights):
    """weights: fn(vertex co) -> {bone: w}"""
    for n in BONES: ob.vertex_groups.new(name=n)
    for v in ob.data.vertices:
        for b, w in weights(v.co).items():
            if w > 0.001: ob.vertex_groups[b].add([v.index], w, 'REPLACE')
    ob.parent = arm; ob.matrix_parent_inverse = Matrix.Identity(4)
    md = ob.modifiers.new('Armature', 'ARMATURE'); md.object = arm

def smooth(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)
skin(jar, lambda co: {'Body': 1.0})
skin(jam, lambda co: {'Jam': 1 - smooth(0.42, 0.78, co.z), 'JamTop': smooth(0.42, 0.78, co.z)})
def bone_parent(ob, bone):
    pb = arm.pose.bones[bone]; tail = arm.matrix_world @ pb.tail
    ob.parent = arm; ob.parent_type = 'BONE'; ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Translation(-tail)   # Blender parents a bone child to the bone TAIL; bones here have identity orientation
bone_parent(lid, 'Lid_Bone'); bone_parent(eyeL, 'EyeL_Bone'); bone_parent(eyeR, 'EyeR_Bone'); bone_parent(splat, 'Splat_Bone')
for ob in (jar, jam, lid, eyeL, eyeR, splat): ob.rotation_mode = 'XYZ'

# ------------------------------------------------------------------ animation
PB = arm.pose.bones
for pb in PB: pb.rotation_mode = 'XYZ'
arm.animation_data_create()
CUR = {'act': None}

def new_action(name, length, loop=False):
    BODYSC.clear()
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm.animation_data.action = act
    CUR.update(act=act, length=length, name=name)
    for pb in PB:
        pb.location = (0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    return act

def K(bone, f, loc=(0, 0, 0), rot=(0, 0, 0), sc=(1, 1, 1)):
    pb = PB[bone]; pb.location = loc; pb.rotation_euler = tuple(math.radians(a) for a in rot); pb.scale = sc
    for path in ('location', 'rotation_euler', 'scale'):
        pb.keyframe_insert(path, frame=f)

BODYSC = {}
def vol(sz, k=0.35): return sz ** -k
def body(f, lift=0, sz=1, sxy=None, rx=0, ry=0, rz=0, dx=0, dy=0, ground=True):
    sxy = vol(sz) if sxy is None else sxy
    if ground:   # a lean about the base centre would sink the low rim into the floor: lift by the rim's drop
        lift += 0.5 * sxy * max(math.sin(math.radians(abs(rx))), math.sin(math.radians(abs(ry))))
    BODYSC[f] = (sxy, sz)
    K('Body', f, (dx, dy, lift), (rx, ry, rz), (sxy, sxy, sz))
def jamb(f, sz=1, sxy=None, rx=0, ry=0, dx=0, dz=0):
    sxy = vol(sz, 0.25) if sxy is None else sxy
    K('Jam', f, (dx, 0, dz), (rx, ry, 0), (min(sxy, 1.03), min(sxy, 1.03), sz))
def top(f, sx=1, sy=None, sz=1, rx=0, ry=0, dz=0, dx=0):
    K('JamTop', f, (dx, 0, dz), (rx, ry, 0), (sx, sx if sy is None else sy, sz))
def lidb(f, rx=0, ry=0, rz=0, dz=0, dx=0, dy=0):
    sxy, sz = BODYSC.get(f, (1.0, 1.0))      # cancel the Body squash on the lid (a tin lid does not squash); keeps shear small
    K('Lid_Bone', f, (dx, dy, dz), (rx, ry, rz), (1 / sxy, 1 / sxy, 1 / sz))
def eyes(f, sz=1, sx=1, dx=0, dz=0, dy=0):
    K('EyeL_Bone', f, (dx, dy, dz), (0, 0, 0), (sx, sx, sz)); K('EyeR_Bone', f, (dx, dy, dz), (0, 0, 0), (sx, sx, sz))
def splatb(f, s=1.0): K('Splat_Bone', f, (0, 0, 0), (0, 0, 0), (s, s, s))
def rest(f):
    body(f); jamb(f); top(f); lidb(f); eyes(f); splatb(f)

def finish(loop=False):
    act = CUR['act']
    return act   # keys use Blender's default Bezier / auto-clamped handles

def trem(i, a=1.0): return a * (1 if i % 2 == 0 else -1) * (0.85 if (i // 2) % 2 else 1.0)

# ---- Idle: breathing, jam sway, a slow blink (120 frames, loop)
def build_idle():
    new_action('Idle', 120)
    for f in range(0, 121, 10):
        s = math.sin(2 * math.pi * f / 120); c = math.cos(2 * math.pi * f / 120); sl = math.sin(2 * math.pi * (f - 14) / 120)
        body(f, sz=1 + 0.012 * s, rx=0.8 * c)
        jamb(f, sz=1 + 0.03 * s, ry=1.2 * sl)
        top(f, sx=1 - 0.02 * s, sz=1 + 0.025 * s, ry=3.2 * sl, rx=2.0 * math.sin(2 * math.pi * (f - 30) / 120), dz=0.008 * s)
        lidb(f, rx=0.8 * s + 0.4, ry=0.5 * c, dz=0.004 * (s + 1))
        eyes(f, dx=0.008 * math.sin(2 * math.pi * f / 120 + 1.0))
        splatb(f)
    # blink (overrides the eye keys at those frames; eyes re-opened by 94)
    for f, v in ((82, 1.0), (86, 0.1), (89, 0.1), (94, 1.0)): eyes(f, sz=v, dx=0.008 * math.sin(2 * math.pi * f / 120 + 1.0))
    finish()

# ---- Move: hop-crawl, one low hop per 40 frames (loop). The game moves the enemy; Body lifts only.
def build_move():
    new_action('Move', 40)
    seq = [  # f, lift, sz, rx, jam sz, top rx, lid dz
        (0, 0.0, 0.86, 4, 0.90, 4, 0.0), (6, 0.04, 1.12, 5, 1.08, 3, 0.006), (17, 0.14, 1.03, 8, 1.0, -5, 0.01),
        (29, 0.05, 1.08, 6, 1.05, -8, 0.004), (34, 0.0, 0.80, 3, 0.86, 5, 0.0), (40, 0.0, 0.86, 4, 0.90, 4, 0.0)]
    for f, l, sz, rx, jsz, trx, ldz in seq:
        body(f, lift=l, sz=sz, rx=rx); jamb(f, sz=jsz); top(f, rx=trx, sz=1.0 + (jsz - 1) * 0.5, dz=0.01 * (jsz - 1))
        lidb(f, rx=-3 * (sz - 0.9) * 8, dz=ldz); eyes(f, sz=1.0 if f not in (0, 40, 34) else 0.82); splatb(f)
    finish()

# ---- Hit: short flinch (20 frames). Struck from the front: snaps back, eyes squeeze shut, lid jumps.
def build_hit():
    new_action('Hit', 20)
    rest(0)
    for f, rx, sz, jsz, trx, ldz, lrx, ey in (
            (2, -17, 0.86, 0.92, 9, 0.03, -9, 0.12), (6, 7, 1.05, 1.06, -7, 0.01, -3, 0.45), (11, -3, 0.98, 0.99, 3, 0.0, 1, 0.9), (16, 1, 1.0, 1.0, -1, 0.0, 0, 1.0)):
        body(f, sz=sz, rx=rx); jamb(f, sz=jsz); top(f, ry=trx, sz=1.0 + (jsz - 1) * 0.6); lidb(f, rx=lrx, dz=ldz); eyes(f, sz=ey); splatb(f)
    rest(20)
    finish()

# ---- Death: flinch, wobble, tip over toward +x (pivot = the jar's bottom rim), lid pops, jam drains into a puddle, held from 62.
def tip(deg):
    t = math.radians(deg); return (0.5 * (1 - math.cos(t)), 0.0, 0.5 * math.sin(t))
def build_death():
    new_action('Death', 72)
    def tb(f, deg, sz=1.0, rx=0.0, rz=0.0):
        x, _, z = tip(deg); body(f, lift=z, dx=x, sz=sz, sxy=1.0 if deg > 5 else None, rx=rx, ry=deg, rz=rz, ground=deg <= 5)
    rest(0)
    tb(4, 0, 0.88, -14); lidb(4, rx=-12, dz=0.04); eyes(4, sz=0.2); jamb(4, sz=0.92); top(4, ry=8); splatb(4)
    tb(10, 0, 0.95, 6); lidb(10, rx=2); eyes(10, sz=0.35); jamb(10); top(10, ry=-5); splatb(10)
    tb(16, 0, 1.0, -2); lidb(16, rx=-4, dz=0.01); eyes(16, sz=0.4); jamb(16); top(16, ry=3); splatb(16)
    tb(22, 6); lidb(22, rx=-5, dz=0.012); eyes(22, sz=0.3); jamb(22); top(22, ry=-2); splatb(22)
    tb(28, 26); lidb(28, rx=-18, dz=0.05); eyes(28, sz=0.25); jamb(28, sz=0.98); top(28, ry=5, sz=1.1); splatb(28)
    tb(32, 62); lidb(32, rx=-35, dz=0.12); eyes(32, sz=0.2); jamb(32, sz=0.95); top(32, ry=-5, sz=1.15); splatb(32, 1.0)
    tb(35, 90, 0.9); lidb(35, rx=-42, dz=0.18, ry=8); eyes(35, sz=0.15); jamb(35, sz=0.9); top(35, sz=1.2); splatb(35, 2.0)       # impact
    tb(38, 82, 1.0); lidb(38, rx=-36, dz=0.17, ry=10); eyes(38, sz=0.12); jamb(38, sz=0.8, dx=0.02); top(38, sz=1.0); splatb(38, 10.0)
    tb(42, 90, 0.98); lidb(42, rx=-30, dz=0.14, ry=11); eyes(42, sz=0.1); jamb(42, sz=0.62, sxy=0.93, dx=0.04); top(42, sz=0.8); splatb(42, 22.0)
    tb(47, 88, 1.0); lidb(47, rx=-28, dz=0.13, ry=12); eyes(47, sz=0.1); jamb(47, sz=0.46, sxy=0.9, dx=0.05); top(47, sz=0.65); splatb(47, 34.0)
    tb(52, 90); lidb(52, rx=-28, dz=0.12, ry=12); eyes(52, sz=0.1); jamb(52, sz=0.34, sxy=0.88, dx=0.06); top(52, sz=0.55); splatb(52, 43.0)
    for f in (62, 72):                                                                                                         # held
        tb(f, 90); lidb(f, rx=-28, dz=0.12, ry=12); eyes(f, sz=0.1); jamb(f, sz=0.24, sxy=0.86, dx=0.07); top(f, sz=0.5); splatb(f, 50.0)
    finish()

# ---- Attack_slime_hop (60): tell pose peak f10 (poseEnd), tremble 10-15, glint/spring f16, hop arc 16-34 (Body lift only), strike = landing 34-40.
def build_hop():
    new_action('Attack_slime_hop', 60)
    rest(0)
    body(4, sz=0.90, rx=3); jamb(4, sz=0.95); top(4, sz=1.03); lidb(4, rx=2); eyes(4, sz=0.9); splatb(4)
    body(10, sz=0.74, rx=7); jamb(10, sz=0.86); top(10, sz=0.95, rx=5); lidb(10, rx=2, dz=0.0); eyes(10, sz=0.62, sx=1.05); splatb(10)       # tell peak
    for i, f in enumerate(range(11, 16)):                                                                                              # tremble
        t = trem(i)
        body(f, sz=0.74 + 0.012 * t, rx=7, ry=1.6 * t, dx=0.011 * t); jamb(f, sz=0.86 - 0.01 * t, ry=2 * t); top(f, sz=0.95, rx=5, ry=4 * t)
        lidb(f, rx=2 + 2.0 * t, dz=0.003 * abs(t)); eyes(f, sz=0.6, sx=1.06); splatb(f)
    body(16, sz=0.72, rx=7); jamb(16, sz=0.85); top(16, sz=0.94, rx=5); lidb(16, rx=-6, dz=0.03); eyes(16, sz=0.55, sx=1.08); splatb(16)  # glint, then spring
    body(19, lift=0.22, sz=1.20, rx=4); jamb(19, sz=1.12); top(19, sz=1.12, rx=-6); lidb(19, rx=-6, dz=0.02); eyes(19, sz=1.2, sx=0.95); splatb(19)
    body(25, lift=0.56, sz=1.06, rx=10); jamb(25, sz=1.0); top(25, sz=1.04, rx=-8); lidb(25, rx=-3, dz=0.012); eyes(25, sz=1.15); splatb(25)  # apex
    body(30, lift=0.24, sz=1.10, rx=15); jamb(30, sz=1.06); top(30, sz=1.08, rx=-10); lidb(30, rx=4, dz=0.006); eyes(30, sz=1.2); splatb(30)
    body(34, lift=0.0, sz=0.70, rx=8); jamb(34, sz=0.80); top(34, sz=1.0, rx=-4); lidb(34, rx=6, dz=0.0); eyes(34, sz=0.8); splatb(34)      # touchdown: strike window opens
    body(36, lift=0.0, sz=0.66, rx=6); jamb(36, sz=0.74); top(36, sz=1.22, rx=-2); lidb(36, rx=-9, dz=0.03); eyes(36, sz=1.3, sx=1.1); splatb(36)  # splat peak
    body(38, lift=0.0, sz=0.69, rx=5); jamb(38, sz=0.78); top(38, sz=1.12, rx=2); lidb(38, rx=-5, dz=0.018); eyes(38, sz=1.25, sx=1.1); splatb(38)
    body(40, lift=0.0, sz=0.74, rx=4); jamb(40, sz=0.84); top(40, sz=1.05, rx=3); lidb(40, rx=-1, dz=0.008); eyes(40, sz=1.1); splatb(40)   # window closes
    body(44, lift=0.04, sz=1.10, rx=1); jamb(44, sz=1.06); top(44, sz=1.10, rx=-3); lidb(44, rx=-4, dz=0.02); eyes(44, sz=1.0); splatb(44)
    body(50, sz=0.94, rx=-1); jamb(50, sz=0.97); top(50, sz=0.96, rx=2); lidb(50, rx=1, dz=0.0); eyes(50, sz=0.95); splatb(50)
    body(55, sz=1.02); jamb(55, sz=1.01); top(55, sz=1.01); lidb(55); eyes(55); splatb(55)
    rest(60)
    finish()

# ---- Attack_slime_glob (66): lean back with the glob swelling under the lid (peak f18 = poseEnd), hold and tremble to the glint f26, throw f26-31,
# recoil, lid clank-reseats across the HitOn window 46-52 (the glob lands on the target then).
def build_glob():
    new_action('Attack_slime_glob', 66)
    rest(0)
    body(8, sz=0.94, rx=-8); jamb(8, sz=1.0); top(8, sx=0.97, sz=1.12, dz=0.03); lidb(8, rx=-4, dz=0.015); eyes(8, sz=1.05); splatb(8)
    body(18, sz=0.88, rx=-16); jamb(18, sz=1.0); top(18, sx=0.93, sz=1.22, dz=0.06, rx=3); lidb(18, rx=-14, dz=0.06); eyes(18, sz=1.12, sx=1.05); splatb(18)  # tell peak
    for i, f in enumerate(range(19, 26)):                                                                                              # tremble, growing
        t = trem(i); a = 0.6 + 0.12 * i
        body(f, sz=0.88 + 0.006 * t, rx=-16 + 0.7 * t * a, ry=1.0 * t * a); jamb(f, sz=1.0 + 0.01 * t)
        top(f, sx=0.93 + 0.01 * t * a, sz=1.22 + 0.025 * t * a, dz=0.06 + 0.003 * i, rx=3, ry=3 * t * a)
        lidb(f, rx=-14 - 0.6 * i + 3 * t * a, dz=0.06 + 0.002 * i); eyes(f, sz=1.12, sx=1.05); splatb(f)
    body(26, sz=0.88, rx=-17); jamb(26, sz=1.0); top(26, sx=0.92, sz=1.27, dz=0.08, rx=3); lidb(26, rx=-20, dz=0.078); eyes(26, sz=1.15, sx=1.06); splatb(26)  # glint
    body(28, sz=1.04, rx=11); jamb(28, sz=0.92); top(28, sx=0.94, sz=0.86, dz=-0.02, rx=-6); lidb(28, rx=-24, dz=0.10); eyes(28, sz=0.78); splatb(28)        # throw
    body(31, sz=1.02, rx=14); jamb(31, sz=0.96); top(31, sx=0.98, sz=0.95, dz=-0.005, rx=-4); lidb(31, rx=-14, dz=0.06); eyes(31, sz=0.85); splatb(31)
    body(36, sz=0.96, rx=4); jamb(36, sz=0.98); top(36, sz=1.0); lidb(36, rx=-4, dz=0.02); eyes(36, sz=1.0); splatb(36)
    body(40, sz=1.0, rx=1); jamb(40); top(40); lidb(40, rx=-8, dz=0.03); eyes(40); splatb(40)
    body(44, sz=1.0); jamb(44); top(44); lidb(44, rx=-8, dz=0.03); eyes(44); splatb(44)
    body(46, sz=0.98, rx=-1); jamb(46, sz=0.98); top(46); lidb(46, rx=0, dz=0.0); eyes(46, sz=0.9); splatb(46)                            # lid clanks down (window opens)
    body(49, sz=1.01, rx=1); jamb(49, sz=1.01); top(49); lidb(49, rx=-2, dz=0.012); eyes(49, sz=0.95); splatb(49)
    body(52, sz=0.99); jamb(52); top(52); lidb(52, rx=0, dz=0.0); eyes(52); splatb(52)                                                  # window closes
    body(58, sz=1.005); jamb(58); top(58); lidb(58); eyes(58); splatb(58)
    rest(66)
    finish()

for fn in (build_idle, build_move, build_hit, build_death, build_hop, build_glob): fn()
ACTIONS = ['Idle', 'Move', 'Hit', 'Death', 'Attack_slime_hop', 'Attack_slime_glob']

def set_action(name, frame):
    arm.animation_data.action = bpy.data.actions[name]
    if hasattr(arm.animation_data, 'action_slot') and bpy.data.actions[name].slots:
        arm.animation_data.action_slot = bpy.data.actions[name].slots[0]
    scene.frame_set(int(frame)); bpy.context.view_layer.update()

# ------------------------------------------------------------------ report
parts = [jar, jam, lid, eyeL, eyeR, splat]
report = {'tris': {o.name: tris(o) for o in parts}}
report['total_tris'] = sum(report['tris'].values())
report['clips'] = {n: tuple(round(x) for x in bpy.data.actions[n].frame_range) for n in ACTIONS}
set_action('Idle', 0)
bb = [Vector(c) for o in (jar, lid) for c in o.bound_box]
report['jar_lid_height'] = round(max(v.z for v in bb) - min(v.z for v in bb), 3)
report['jar_width'] = round(max(v.x for v in bb) - min(v.x for v in bb), 3)
print('SLIME_REPORT', report)
with open(os.path.join(OUT, 'report.txt'), 'w') as fh: fh.write(repr(report))

# ------------------------------------------------------------------ export
if '--no-export' not in argv:
    os.makedirs(EXPORT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in [arm] + parts: o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    set_action('Idle', 0)
    bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT, 'Slime.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'ARMATURE', 'MESH'},
                             mesh_smooth_type='FACE', add_leaf_bones=False, use_armature_deform_only=True, path_mode='STRIP',
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                             bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    print('exported', os.path.join(EXPORT, 'Slime.fbx'))

# ------------------------------------------------------------------ preview panels
POSES = [  # file, label, action, frame, cam az (deg from front, +=toward viewer's right... camera orbit), elevation, distance, target
    ('01', 'Rest (Idle f0), 3/4', 'Idle', 0, -30, 14, 3.4, (0, 0, 0.5)),
    ('02', 'Rest, side', 'Idle', 0, -90, 8, 3.4, (0, 0, 0.5)),
    ('03', 'Hop tell peak f10', 'Attack_slime_hop', 10, -30, 14, 3.4, (0, 0, 0.5)),
    ('04', 'Hop glint f16', 'Attack_slime_hop', 16, -30, 14, 3.4, (0, 0, 0.5)),
    ('05', 'Hop apex f25 (side)', 'Attack_slime_hop', 25, -90, 8, 4.0, (0, 0, 0.6)),
    ('06', 'Hop strike f36', 'Attack_slime_hop', 36, -30, 14, 3.4, (0, 0, 0.45)),
    ('07', 'Glob tell peak f18', 'Attack_slime_glob', 18, -30, 14, 3.4, (0, 0, 0.5)),
    ('08', 'Glob throw f28', 'Attack_slime_glob', 28, -30, 14, 3.4, (0, 0, 0.5)),
    ('09', 'Glob reseat f46', 'Attack_slime_glob', 46, -30, 14, 3.4, (0, 0, 0.5)),
    ('10', 'Hit f2', 'Hit', 2, -30, 14, 3.4, (0, 0, 0.5)),
    ('11', 'Death f35 (impact)', 'Death', 35, -60, 20, 5.0, (0.6, 0, 0.4)),
    ('12', 'Death f72 (held)', 'Death', 72, -60, 28, 5.2, (0.6, 0, 0.1)),
]
def render_panels():
    pdir = os.path.join(OUT, 'panels'); os.makedirs(pdir, exist_ok=True)
    scene.render.engine = 'CYCLES'; scene.cycles.samples = SAMPLES; scene.cycles.use_denoising = True
    try:
        cp = bpy.context.preferences.addons['cycles'].preferences
        for t in ('OPTIX', 'CUDA', 'HIP'):
            try:
                cp.compute_device_type = t; cp.get_devices()
                if any(d.type != 'CPU' for d in cp.devices):
                    for d in cp.devices: d.use = d.type != 'CPU'
                    scene.cycles.device = 'GPU'; break
            except Exception: pass
    except Exception: pass
    scene.cycles.transparent_max_bounces = 12; scene.cycles.glossy_bounces = 6; scene.cycles.transmission_bounces = 8
    scene.render.resolution_x = scene.render.resolution_y = RRES; scene.render.image_settings.file_format = 'PNG'
    w = bpy.data.worlds.new('W'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.06, 0.04, 0.03, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6; scene.world = w
    # floor: warm cellar flagstone slab, large
    bpy.ops.mesh.primitive_plane_add(size=14, location=(0.4, 0, 0)); fl = bpy.context.object; fl.name = 'Floor'
    fm, fb = mat('M_PreviewFloor', (0.34, 0.26, 0.19), 0.7); fl.data.materials.append(fm)
    noise_to(fb, 'floor', 6.0, (0.20, 0.15, 0.11), (0.42, 0.33, 0.24))
    def light(name, loc, energy, color, size):
        d = bpy.data.lights.new(name, 'AREA'); d.energy = energy; d.color = color; d.size = size
        o = bpy.data.objects.new(name, d); scene.collection.objects.link(o); o.location = loc
        tr = o.constraints.new('TRACK_TO'); tr.target = tgt; tr.track_axis = 'TRACK_NEGATIVE_Z'; tr.up_axis = 'UP_Y'
    tgt = bpy.data.objects.new('Tgt', None); scene.collection.objects.link(tgt); tgt.location = (0, 0, 0.5)
    light('Key', (-2.6, -3.0, 3.2), 700, (1.0, 0.68, 0.38), 2.4)
    light('Fill', (3.4, -2.4, 1.8), 120, (1.0, 0.80, 0.60), 3.0)
    light('Rim', (1.5, 3.2, 2.4), 420, (1.0, 0.58, 0.30), 2.0)
    cam_d = bpy.data.cameras.new('Cam'); cam_d.lens = 52; cam = bpy.data.objects.new('Cam', cam_d); scene.collection.objects.link(cam); scene.camera = cam
    tr = cam.constraints.new('TRACK_TO'); tr.target = tgt; tr.track_axis = 'TRACK_NEGATIVE_Z'; tr.up_axis = 'UP_Y'
    for fid, label, act, fr, az, el, dist, target in POSES:
        set_action(act, fr)
        tgt.location = target
        a = math.radians(az - 90); e = math.radians(el)    # az -90 = camera on -Y looking at the front... (-38 = from the front-left)
        cam.location = (target[0] + dist * math.cos(e) * math.cos(a + math.radians(0)), target[1] + dist * math.cos(e) * math.sin(a), target[2] + dist * math.sin(e))
        bpy.context.view_layer.update()
        scene.render.filepath = os.path.join(pdir, fid + '.png'); bpy.ops.render.render(write_still=True)
        with open(os.path.join(pdir, fid + '.txt'), 'w') as fh: fh.write(label)
    print('panels', pdir)
if '--render' in argv:
    render_panels()
