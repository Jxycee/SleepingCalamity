"""EnemyBroom: the sweeping broom, rigged and animated (Cellars tier-1/2 enemy, EnemyKind.Broom).

An animated broom that stands upright on its splayed bristles and hops / shuffles after you. The bristle head is wide and
flat with its broad face to the front (+z in Unity), so it doubles as the shield (EnemyDefinition.Broom frontalBlock). A band
of twine and an iron cuff bind the neck; two ember knot-eyes glare from the cuff (demon-made, STORY 4); two twig feet poke out
of the front of the head. Warm honey straw, soot-dark wood, old iron.

Size: 1.9 m tall (EnemyModel Height 1.9, Radius 0.55 -> head 0.84 m wide), origin at the feet (bristle tips on y=0), front = -Y in
Blender = +z in Unity (export -Z forward, Y up). Budget: 3-6k tris (enemy LOD0 cap 25k, PHASE1 3.3); measured and printed.
References: none on file for the broom (no concept in Reference/private/concepts); rope-and-straw broom look from the design doc
(docs/design/dungeon1-cellars.md) and the cellar palette. Textures are procedural (numpy), 1024 px max, written next to the FBX.

Objects (names are load-bearing): Bristles (straw head + core + twig feet), Handle (the pole), Band (twine wraps + iron cuff,
2 material slots), Eyes (ember). One armature "Armature".
Bones: root (never moves), spine1 / spine2 / spine3 (up the handle), brist_mid / brist_L / brist_R (head sway and splay),
foot_L / foot_R (twigs). Left/right are the broom's own (facing +z in Unity).

Clips (60 fps, frame f == AttackFrame f, keys 0..N inclusive, root never translates):
  Idle 120 loop, Move 30 loop, Hit 20, Brace 16, Death 72 (ends held),
  Attack_broom_sweep 70, Attack_broom_poke 52, Attack_broom_riposte 58 (== TotalFrames in Core/Combat/AttackCatalog.cs).
Tell pose lands on poseEnd, glint on commit, strike spans HitOn: sweep 14/22/36-44, poke 8/14/28-34, riposte 9/15/27-33.
FBX take names come out as "Armature|<action>" (Blender's exporter).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/enemies/sc_enemy_broom.py -- --out <scratch>
      [--export <dir>]     default Assets/_Project/Content/Art/Enemies/Broom (FBX + PNG maps)
      [--sheet <png>]      key-pose sheet (default Reference/sessions/rig-cellar-enemies/sheets/broom_poses.png)
      [--no-render] [--no-export]
"""
import math, os, sys, random, subprocess, shutil, json
if '--compose' in sys.argv:       # system-python mode (PIL): tiles -> sheet, no Blender needed
    from PIL import Image, ImageDraw
    _td, _out = sys.argv[sys.argv.index('--compose') + 1:][:2]
    labels = json.load(open(os.path.join(_td, 'labels.json'))); cols = 4; w, h = 420, 620; rows = (len(labels) + cols - 1) // cols
    S = Image.new('RGB', (cols * w, rows * h + 34), (22, 18, 16)); d = ImageDraw.Draw(S)
    for k, (p, lab) in enumerate(labels):
        S.paste(Image.open(p).convert('RGB'), ((k % cols) * w, (k // cols) * h)); d.text(((k % cols) * w + 8, (k // cols) * h + 6), lab, fill=(255, 235, 200))
    d.text((8, rows * h + 10), 'Sweeping broom key poses. No broom concept art on file (Reference/private/concepts has none), so no reference tile.', fill=(220, 200, 170))
    S.save(_out); print('sheet', _out); sys.exit(0)
import bpy, bmesh
from mathutils import Vector, Matrix, Euler
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default
OUT = arg('--out', os.path.join(os.environ.get('TEMP', '.'), 'broom_out'))
EXPORT = arg('--export', os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Enemies', 'Broom'))
SHEET = arg('--sheet', os.path.join(REPO, 'Reference', 'sessions', 'rig-cellar-enemies', 'sheets', 'broom_poses.png'))
NO_RENDER = '--no-render' in argv
NO_EXPORT = '--no-export' in argv
os.makedirs(OUT, exist_ok=True)
rng = random.Random(7)

# ------------------------------------------------------------------ frame data from Core/Combat/AttackCatalog.cs (do not change here)
SWEEP_N, POKE_N, RIPOSTE_N = 70, 52, 58
SWEEP = dict(pose_end=14, commit=22, hit=(36, 44))
POKE = dict(pose_end=8, commit=14, hit=(28, 34), lunge=(16, 28))
RIPOSTE = dict(pose_end=9, commit=15, hit=(27, 33))

# ------------------------------------------------------------------ textures (procedural, tileable)
def _up(a, h, w):
    H, W = a.shape
    ys = np.linspace(0, H, h, endpoint=False); xs = np.linspace(0, W, w, endpoint=False)
    y0 = np.floor(ys).astype(int); x0 = np.floor(xs).astype(int); fy = (ys - y0)[:, None]; fx = (xs - x0)[None, :]
    y1 = (y0 + 1) % H; x1 = (x0 + 1) % W; y0 %= H; x0 %= W
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    return (a[y0][:, x0] * (1 - fx) + a[y0][:, x1] * fx) * (1 - fy) + (a[y1][:, x0] * (1 - fx) + a[y1][:, x1] * fx) * fy

def fbm(h, w, cells_y, cells_x, octaves, seed, gain=0.5):
    r = np.random.RandomState(seed); out = np.zeros((h, w)); amp = 1.0; tot = 0.0
    for o in range(octaves):
        out += amp * _up(r.rand(max(1, cells_y * 2 ** o), max(1, cells_x * 2 ** o)), h, w); tot += amp; amp *= gain
    return out / tot

def normal_from_height(hm, k):
    dx = np.roll(hm, -1, 1) - np.roll(hm, 1, 1); dy = np.roll(hm, -1, 0) - np.roll(hm, 1, 0)
    n = np.dstack([-dx * k, dy * k, np.ones_like(hm)]); n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5

def lerp3(a, b, t):
    return np.array(a)[None, None, :] * (1 - t[..., None]) + np.array(b)[None, None, :] * t[..., None]

def tex_straw(S=1024):
    r = np.random.RandomState(3)
    v = np.linspace(0, 1, S)[:, None] * np.ones((1, S))                      # 0 at the neck (top), 1 at the tips
    cols = _up(r.rand(1, 140), 1, S)[0]                                     # per-column fibre shade
    fib = np.ones((S, 1)) * cols[None, :]
    lum = fbm(S, S, 3, 90, 3, 11)                                            # long streaks
    t = np.clip(0.55 * fib + 0.45 * lum, 0, 1)
    base = lerp3((0.30, 0.17, 0.06), (0.72, 0.50, 0.21), t)                  # warm honey straw
    base *= (0.55 + 0.45 * np.clip(v * 2.2, 0, 1))[..., None]                # stained dark at the neck
    base = lerp3_apply(base, (0.30, 0.22, 0.13), np.clip((v - 0.8) * 3.0, 0, 1) * lum * 0.8)   # worn, sooty tips
    base[:, :12] = np.array((0.16, 0.11, 0.07))                              # core stripe (u 0..0.012)
    hm = fib * 0.5 + lum * 0.5
    rough = np.clip(0.78 + 0.15 * (1 - t), 0, 1)
    return base, normal_from_height(hm, 2.5), np.dstack([0.55 + 0.45 * np.clip(v * 3, 0, 1), rough, np.zeros_like(rough)])

def lerp3_apply(base, col, t):
    return base * (1 - t[..., None]) + np.array(col)[None, None, :] * t[..., None]

def tex_wood(W=512, H=1024):
    grain = fbm(H, W, 2, 40, 4, 21); fine = fbm(H, W, 1, 160, 2, 22); wear = fbm(H, W, 6, 2, 3, 23)
    t = np.clip(grain * 0.7 + fine * 0.3, 0, 1)
    base = lerp3((0.20, 0.11, 0.05), (0.44, 0.27, 0.13), t)                  # soot-dark honey wood
    base = lerp3_apply(base, (0.52, 0.38, 0.22), np.clip((wear - 0.55) * 3, 0, 1) * 0.6)    # hand-worn pale patches
    hm = grain * 0.6 + fine * 0.4
    rough = np.clip(0.62 - 0.25 * np.clip((wear - 0.55) * 3, 0, 1) + 0.1 * fine, 0, 1)
    return base, normal_from_height(hm, 3.0), np.dstack([np.full_like(rough, 0.9), rough, np.zeros_like(rough)])

def tex_twine(S=256):
    y, x = np.mgrid[0:S, 0:S] / S
    twist = 0.5 + 0.5 * np.sin((x * 16 + y * 5) * 2 * math.pi)
    n = fbm(S, S, 8, 8, 3, 31)
    base = lerp3((0.30, 0.20, 0.11), (0.62, 0.48, 0.28), np.clip(twist * 0.7 + n * 0.3, 0, 1))
    return base, normal_from_height(twist * 0.8 + n * 0.2, 4.0), np.dstack([np.full((S, S), 0.8), np.full((S, S), 0.85), np.zeros((S, S))])

def tex_iron(S=256):
    n = fbm(S, S, 6, 6, 4, 41); rust = fbm(S, S, 4, 4, 3, 42)
    base = lerp3((0.07, 0.07, 0.08), (0.20, 0.19, 0.19), n)
    base = lerp3_apply(base, (0.36, 0.17, 0.07), np.clip((rust - 0.55) * 3, 0, 1) * 0.7)
    return base, normal_from_height(n, 1.5), np.dstack([np.full((S, S), 0.9), 0.45 + 0.35 * rust, 0.9 * (1 - np.clip((rust - 0.55) * 3, 0, 1) * 0.6)])

def save_png(path, arr):
    a = np.clip(arr, 0, 1)[::-1]; h, w = a.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False); img.colorspace_settings.name = 'Non-Color'
    px = np.ones((h, w, 4), dtype=np.float32); px[..., :3] = a
    img.pixels.foreach_set(px.ravel()); img.filepath_raw = path; img.file_format = 'PNG'; img.save(); bpy.data.images.remove(img)

TEXSETS = {'Broom_Straw': tex_straw, 'Broom_Wood': tex_wood, 'Broom_Twine': tex_twine, 'Broom_Iron': tex_iron}
def make_textures(d):
    os.makedirs(d, exist_ok=True)
    for name, fn in TEXSETS.items():
        c, n, orm = fn(); save_png(os.path.join(d, name + '_BaseColor.png'), c); save_png(os.path.join(d, name + '_Normal.png'), n)
        save_png(os.path.join(d, name + '_ORM.png'), orm)   # R occlusion, G roughness, B metallic

def make_material(name, d, ember=False):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled'); nt.links.new(bsdf.outputs[0], out.inputs[0])
    if ember:
        bsdf.inputs['Base Color'].default_value = (0.05, 0.02, 0.01, 1); bsdf.inputs['Roughness'].default_value = 0.35
        bsdf.inputs['Emission Color'].default_value = (1.0, 0.42, 0.08, 1); bsdf.inputs['Emission Strength'].default_value = 6.0
        return m
    def img(key, cs):
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(os.path.join(d, f'{name}_{key}.png'))
        n.image.colorspace_settings.name = cs; return n
    nt.links.new(img('BaseColor', 'sRGB').outputs[0], bsdf.inputs['Base Color'])
    nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(img('Normal', 'Non-Color').outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bsdf.inputs['Normal'])
    sp = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(img('ORM', 'Non-Color').outputs[0], sp.inputs[0])
    nt.links.new(sp.outputs[1], bsdf.inputs['Roughness']); nt.links.new(sp.outputs[2], bsdf.inputs['Metallic'])
    return m

# ------------------------------------------------------------------ geometry helpers
def normalize(v): return v.normalized() if v.length > 1e-9 else Vector((0, 0, 1))

def sweep(bm, uvl, pts, radii, sides, e_fn, uv_fn, closed=False, cap_end=False, mat=0):
    """Tube along pts. radii entries are a float or (rx, ry). e_fn(i) -> (e1, e2) section axes. uv_fn(i, j) -> (u, v)."""
    n = len(pts); rings = []
    for i in range(n):
        e1, e2 = e_fn(i); r = radii[i]; rx, ry = (r, r) if not isinstance(r, tuple) else r
        rings.append([bm.verts.new(pts[i] + e1 * (rx * math.cos(2 * math.pi * j / sides)) + e2 * (ry * math.sin(2 * math.pi * j / sides))) for j in range(sides)])
    out = []
    for i in range(n if closed else n - 1):
        i2 = (i + 1) % n
        for j in range(sides):
            j2 = (j + 1) % sides
            f = bm.faces.new([rings[i][j], rings[i][j2], rings[i2][j2], rings[i2][j]])
            for lp, (ii, jj) in zip(f.loops, [(i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)]): lp[uvl].uv = uv_fn(ii, jj)
            f.normal_update(); c = (pts[i] + pts[i2]) / 2
            if f.normal.dot(f.calc_center_median() - c) < 0: f.normal_flip()
            f.material_index = mat; out.append(f)
    if cap_end and not closed:
        f = bm.faces.new(rings[-1]); f.normal_update()
        if f.normal.dot(pts[-1] - pts[-2]) < 0: f.normal_flip()
        for lp in f.loops: lp[uvl].uv = uv_fn(n - 1, 0)
        f.material_index = mat; out.append(f)
    return out

def mesh_obj(name, bm, mats):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    for m in mats: me.materials.append(m)
    return ob

def tri_count(ob): return sum(len(p.vertices) - 2 for p in ob.data.polygons)

# ------------------------------------------------------------------ the model
HEAD_TOP, NECK_Z = 0.66, 0.66
def head_x(c, z): return c * (0.12 + (HEAD_TOP - z) / HEAD_TOP * 0.285)    # c in -1..1, clump centre line at height z
def head_y(row, z): return row * (0.075 + (HEAD_TOP - z) / HEAD_TOP * 0.06)

def build_bristles(mat):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    layers = [(-1, 13), (0, 11), (1, 13)]       # front, middle, back rows of straw clumps
    clumps = []
    for row, n in layers:
        for k in range(n):
            c = (k / (n - 1)) * 2 - 1 + rng.uniform(-0.02, 0.02)
            zb = rng.uniform(0.0, 0.05) + (0.0 if abs(c) < 0.9 else 0.015)
            top = Vector((head_x(c, HEAD_TOP), head_y(row, HEAD_TOP) + rng.uniform(-0.01, 0.01), HEAD_TOP))
            bot = Vector((head_x(c, zb) + rng.uniform(-0.02, 0.02), head_y(row, zb) + rng.uniform(-0.015, 0.015), zb))
            pts = [top.lerp(bot, t) for t in (0, 0.35, 0.7)] + [bot]
            for p in pts[1:3]: p.x += rng.uniform(-0.02, 0.02); p.y += rng.uniform(-0.012, 0.012)
            clumps.append((pts, [0.040, 0.040, 0.034, 0.012], rng.uniform(0.04, 0.9)))
    for pts, rads, u0 in clumps:
        def e_fn(i, pts=pts):
            t = normalize(pts[min(i + 1, 3)] - pts[max(i - 1, 0)]); e1 = normalize(t.cross(Vector((0, 1, 0)))); return e1, t.cross(e1)
        zs = [p.z for p in pts]
        sweep(bm, uvl, pts, rads, 6, e_fn, lambda i, j, u0=u0, zs=zs: (u0 + 0.08 * j / 6, 1 - (zs[min(i, 3)]) / HEAD_TOP * 0.95), cap_end=True)
    for _ in range(80):                              # loose wisps
        pts0, rads0, u0 = rng.choice(clumps); t0 = rng.uniform(0.5, 0.95)
        seg = min(2, int(t0 * 3)); a = pts0[seg].lerp(pts0[seg + 1], t0 * 3 - seg)
        out = Vector((a.x, a.y * 0.5, 0)); out = normalize(out) * 0.03 if out.length > 1e-4 else Vector((0, 0, 0))
        d = normalize((pts0[3] - pts0[0]) + Vector((rng.uniform(-0.45, 0.45) + a.x * 1.2, rng.uniform(-0.45, 0.45), 0)))
        p0 = a + out; p1 = p0 + d * rng.uniform(0.08, 0.16); p1.z = max(p1.z, 0.003)
        def e_fn(i, d=d):
            e1 = normalize(d.cross(Vector((0, 1, 0)))); return e1, d.cross(e1)
        sweep(bm, uvl, [p0, p1], [0.009, 0.002], 3, e_fn, lambda i, j, u0=u0: (u0 + 0.05 * j / 3, 0.45 + 0.5 * i))
    # core: a dark squashed plug so there is no see-through between the clumps
    cpts = [Vector((0, 0, 0.64)), Vector((0, 0, 0.35)), Vector((0, 0, 0.10))]
    sweep(bm, uvl, cpts, [(0.17, 0.09), (0.28, 0.12), (0.34, 0.14)], 12, lambda i: (Vector((1, 0, 0)), Vector((0, 1, 0))), lambda i, j: (0.005, 0.5), cap_end=True)
    # twig feet: three splayed twigs each, out of the front of the head
    foot_verts = {'foot_L': [], 'foot_R': []}
    for side, nm in ((1, 'foot_L'), (-1, 'foot_R')):
        before = len(bm.verts)
        for k in range(3):
            tip = Vector((side * (0.22 + 0.05 * (k - 1)), -0.29 - 0.03 * (k == 1), 0.005)); top = Vector((side * 0.19, -0.13, 0.24 + 0.02 * k))
            mid = top.lerp(tip, 0.55) + Vector((0, -0.01, 0.015)); pts = [top, mid, tip]
            def e_fn(i, pts=pts):
                t = normalize(pts[min(i + 1, 2)] - pts[max(i - 1, 0)]); e1 = normalize(t.cross(Vector((1, 0, 0)))); return e1, t.cross(e1)
            sweep(bm, uvl, pts, [0.018, 0.016, 0.008], 5, e_fn, lambda i, j: (0.3 + 0.1 * j / 5, 0.3 + 0.2 * i), cap_end=True)
        bm.verts.ensure_lookup_table(); foot_verts[nm] = list(range(before, len(bm.verts)))
    ob = mesh_obj('Bristles', bm, [mat]); ob['foot_L'] = foot_verts['foot_L']; ob['foot_R'] = foot_verts['foot_R']
    return ob

def handle_radius(z):
    r = 0.040 - (z - 0.4) / 1.5 * 0.007
    for zc, a, w in ((0.92, 0.007, 0.05), (1.36, 0.006, 0.04), (1.62, 0.005, 0.045)): r += a * math.exp(-((z - zc) / w) ** 2)
    return r + 0.0012 * math.sin(z * 31)

def build_handle(mat):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    zs = [0.42 + i * (1.84 - 0.42) / 24 for i in range(25)] + [1.855, 1.875, 1.892, 1.9]
    rs = [handle_radius(z) for z in zs[:25]] + [0.047, 0.056, 0.048, 0.016]
    pts = [Vector((0.006 * math.sin(z * 2.3), 0, z)) for z in zs]
    ax = lambda i: (Vector((1, 0, 0)), Vector((0, 1, 0)))
    sweep(bm, uvl, pts, rs, 14, ax, lambda i, j: (j / 14, zs[i] * 1.9), cap_end=True)
    return mesh_obj('Handle', bm, [mat])

def build_band(m_twine, m_iron):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    zc = [0.60, 0.62, 0.69, 0.705, 0.72]                       # iron cuff, an oval collar around the neck
    cr = [(0.19, 0.145), (0.18, 0.135), (0.172, 0.127), (0.10, 0.075), (0.043, 0.043)]
    cpts = [Vector((0, 0, z)) for z in zc]
    sweep(bm, uvl, cpts, cr, 28, lambda i: (Vector((1, 0, 0)), Vector((0, 1, 0))), lambda i, j: (j / 28 * 1.5, i / 4), cap_end=False, mat=1)
    def wrap(z, rx, ry, rad, N=36, seg=5, tilt=0.0, r_pt=None):
        pts = [Vector((rx * math.cos(2 * math.pi * k / N), ry * math.sin(2 * math.pi * k / N), z + tilt * math.cos(2 * math.pi * k / N + 1.0))) for k in range(N)]
        def e_fn(i):
            a = 2 * math.pi * i / N; e1 = normalize(Vector((math.cos(a) * ry, math.sin(a) * rx, 0))); return e1, Vector((0, 0, 1))
        sweep(bm, uvl, pts, [rad] * N, seg, e_fn, lambda i, j: (i / N * 6, j / seg), closed=True, mat=0)
    for z, tilt in ((0.575, 0.004), (0.545, -0.004), (0.515, 0.005), (0.485, -0.003)):          # twine binding below the cuff
        s = (HEAD_TOP - z) / HEAD_TOP
        wrap(z, 0.12 + s * 0.285 + 0.032, 0.075 + s * 0.06 + 0.036, 0.0145, tilt=tilt)
    wrap(1.83, 0.047, 0.047, 0.012, N=20, seg=5)                  # twine at the top of the pole (hanging tie)
    wrap(1.17, 0.043, 0.043, 0.011, N=20, seg=5, tilt=0.004)      # an old repair
    return mesh_obj('Band', bm, [m_twine, m_iron])

def build_eyes(mat):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    for side in (1, -1):
        r = bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=1.0)
        vs = r['verts']
        for v in vs:
            v.co = Vector((v.co.x * 0.026, v.co.y * 0.011, v.co.z * 0.014))
            ang = math.radians(-22 * side); v.co = Vector((v.co.x * math.cos(ang) - v.co.z * math.sin(ang), v.co.y, v.co.x * math.sin(ang) + v.co.z * math.cos(ang)))
            v.co += Vector((side * 0.062, -0.122, 0.655))
    return mesh_obj('Eyes', bm, [mat])

# ------------------------------------------------------------------ armature
BONES = [
    ('root', None, (0, 0, 0), (0, 0, 0.12)),
    ('spine1', 'root', (0, 0, 0.05), (0, 0, 0.72)),
    ('spine2', 'spine1', (0, 0, 0.72), (0, 0, 1.30)),
    ('spine3', 'spine2', (0, 0, 1.30), (0, 0, 1.90)),
    ('brist_mid', 'spine1', (0, 0, 0.58), (0, 0, 0.06)),
    ('brist_L', 'spine1', (0.10, 0, 0.56), (0.40, 0, 0.04)),      # broom's left = +X in Blender (facing -Y)
    ('brist_R', 'spine1', (-0.10, 0, 0.56), (-0.40, 0, 0.04)),
    ('foot_L', 'spine1', (0.19, -0.13, 0.24), (0.22, -0.29, 0.0)),
    ('foot_R', 'spine1', (-0.19, -0.13, 0.24), (-0.22, -0.29, 0.0)),
]
BONE_NAMES = [b[0] for b in BONES]

def build_armature():
    arm = bpy.data.armatures.new('Armature'); ob = bpy.data.objects.new('Armature', arm); bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
    eb = {}
    for name, parent, h, t in BONES:
        b = arm.edit_bones.new(name); b.head = h; b.tail = t; b.roll = 0.0
        if parent: b.parent = eb[parent]
        eb[name] = b
    bpy.ops.object.mode_set(mode='OBJECT')
    for p in ob.pose.bones: p.rotation_mode = 'XYZ'
    return ob

def hat_weights(z, centers):
    """Linear partition of unity along a chain of bone centres."""
    if z <= centers[0]: return [1.0] + [0.0] * (len(centers) - 1)
    if z >= centers[-1]: return [0.0] * (len(centers) - 1) + [1.0]
    for i in range(len(centers) - 1):
        if centers[i] <= z <= centers[i + 1]:
            t = (z - centers[i]) / (centers[i + 1] - centers[i]); t = t * t * (3 - 2 * t)
            w = [0.0] * len(centers); w[i] = 1 - t; w[i + 1] = t; return w

def skin(ob, arm_ob, fn):
    for n in BONE_NAMES: ob.vertex_groups.new(name=n)
    for v in ob.data.vertices:
        for name, w in fn(v).items():
            if w > 1e-4: ob.vertex_groups[name].add([v.index], w, 'REPLACE')
    m = ob.modifiers.new('Armature', 'ARMATURE'); m.object = arm_ob

SPINE = ['spine1', 'spine2', 'spine3']; SC = [0.38, 1.01, 1.60]
def spine_fn(v):
    w = hat_weights(v.co.z, SC); return dict(zip(SPINE, w))

def bristle_fn(foot_idx):
    def fn(v):
        if v.index in foot_idx['foot_L']: return {'foot_L': 1.0}
        if v.index in foot_idx['foot_R']: return {'foot_R': 1.0}
        x, z = v.co.x, v.co.z
        h = min(1.0, max(0.0, (0.60 - z) / 0.45))             # 0 at the neck, 1 low in the head
        s = max(-1.0, min(1.0, x / 0.36)); side = h * abs(s) ** 0.8
        mid = h * (1 - abs(s) ** 0.8) * 0.9
        w = {'spine1': max(0.0, 1 - side - mid)}
        w['brist_L' if s > 0 else 'brist_R'] = side; w['brist_mid'] = mid
        return w
    return fn

# ------------------------------------------------------------------ animation
SIGN = {}     # calibrated at runtime: how a + pitch / + yaw / + splay maps to Euler values per bone
def calibrate(arm_ob):
    pb = arm_ob.pose.bones; bpy.context.view_layer.update()
    def tail(name): return arm_ob.matrix_world @ pb[name].tail
    def head(name): return arm_ob.matrix_world @ pb[name].head
    def probe(name, axis, point_fn, comp, want):
        p0 = point_fn(name)[comp]; pb[name].rotation_euler[axis] = 0.2; bpy.context.view_layer.update()
        d = point_fn(name)[comp] - p0; pb[name].rotation_euler[axis] = 0.0; bpy.context.view_layer.update()
        return 1.0 if d * want > 0 else -1.0
    for n in ('spine1', 'spine2', 'spine3'): SIGN[n] = {'pitch': probe(n, 0, tail, 1, -1)}                       # forward = tail towards -Y
    SIGN['spine1']['yaw'] = probe('spine1', 1, lambda n: head('brist_L'), 1, +1)                               # ccw seen from above
    for n in ('spine2', 'spine3'): SIGN[n]['yaw'] = SIGN['spine1']['yaw'] * (1.0)
    # yaw sign for the chain: probe via child head
    SIGN['spine2']['yaw'] = probe('spine2', 1, lambda n: head('spine3'), 0, 0) if False else SIGN['spine1']['yaw']
    SIGN['spine3']['yaw'] = SIGN['spine1']['yaw']
    SIGN['brist_mid'] = {'pitch': probe('brist_mid', 0, tail, 1, -1)}
    SIGN['brist_L'] = {'pitch': probe('brist_L', 0, tail, 1, -1), 'splay': probe('brist_L', 2, tail, 0, +1)}
    SIGN['brist_R'] = {'pitch': probe('brist_R', 0, tail, 1, -1), 'splay': probe('brist_R', 2, tail, 0, -1)}
    for n in ('foot_L', 'foot_R'): SIGN[n] = {'pitch': probe(n, 0, tail, 1, -1)}
    print('SIGN', json.dumps(SIGN))

# pose: bone -> dict(p=pitch deg (+ forward), y=yaw deg (+ turns the front to the broom's left), s=splay deg (+ outward, brist_L/R) or roll,
#                    dx/dy/dz = offset metres in armature space)
def P(**k): return k

def blend(a, b, t):
    out = {}
    for bn in set(a) | set(b):
        x = a.get(bn, {}); y = b.get(bn, {}); d = {}
        for key in set(x) | set(y): d[key] = x.get(key, 0.0) * (1 - t) + y.get(key, 0.0) * t
        out[bn] = d
    return out

EASE = {'smooth': lambda t: t * t * (3 - 2 * t), 'in': lambda t: t * t, 'in3': lambda t: t ** 3, 'out': lambda t: 1 - (1 - t) ** 2, 'lin': lambda t: t,
        'snap': lambda t: 1 - (1 - t) ** 4, 'hold': lambda t: 0.0 if t < 1 else 1.0}

def keyed(keys, overlays=()):
    """keys: [(frame, pose, ease_into_this_key)]; overlays: [(f0, f1, amp_deg, bones)] tremble."""
    def f(fr):
        for (f0, p0, _), (f1, p1, e) in zip(keys, keys[1:]):
            if f0 <= fr <= f1: pose = blend(p0, p1, EASE[e]((fr - f0) / max(1, f1 - f0))); break
        else: pose = keys[-1][1] if fr > keys[-1][0] else keys[0][1]
        pose = {k: dict(v) for k, v in pose.items()}
        for (a, b, amp, bones) in overlays:
            if a <= fr <= b:
                env = min(1.0, (fr - a) / 3.0, (b - fr) / 3.0 + 0.0) if b > a else 1.0; env = max(0.0, min(1.0, env))
                for bn in bones:
                    d = pose.setdefault(bn, {})
                    d['p'] = d.get('p', 0.0) + amp * env * math.sin(fr * 2.3 + 1.1) * math.sin(fr * 0.9)
                    d['y'] = d.get('y', 0.0) + amp * env * math.sin(fr * 3.1 + 0.4)
        return pose
    return f

def S(a=0.0, b=0.0, c=0.0): return {}
def spine(p1=0, p2=0, p3=0, y1=0, y2=0, y3=0, dz=0.0, dy=0.0, dx=0.0, r1=0, r2=0, r3=0):
    return {'spine1': dict(p=p1, y=y1, r=r1, dz=dz, dy=dy, dx=dx), 'spine2': dict(p=p2, y=y2, r=r2), 'spine3': dict(p=p3, y=y3, r=r3)}
def head(sp=0.0, spR=None, sw=0.0, swL=0.0, swR=0.0):
    spR = sp if spR is None else spR
    return {'brist_L': dict(s=sp, p=swL), 'brist_R': dict(s=spR, p=swR), 'brist_mid': dict(p=sw)}
def feet(pl=0.0, pr=0.0, lift_l=0.0, lift_r=0.0):
    return {'foot_L': dict(p=pl, dz=lift_l), 'foot_R': dict(p=pr, dz=lift_r)}
def merge(*ps):
    o = {}
    for p in ps:
        for k, v in p.items(): o.setdefault(k, {}).update(v)
    return o

def apply_pose(arm_ob, pose, lift_extra=0.0):
    pb = arm_ob.pose.bones
    for n in BONE_NAMES:
        d = pose.get(n, {}); b = pb[n]; sg = SIGN.get(n, {})
        rx = math.radians(d.get('p', 0.0)) * sg.get('pitch', 1.0)
        ry = math.radians(d.get('y', 0.0)) * sg.get('yaw', 1.0)
        rz = math.radians(d.get('s', 0.0)) * sg.get('splay', 1.0) if n.startswith('brist_') and n != 'brist_mid' else math.radians(d.get('r', 0.0))
        b.rotation_euler = Euler((rx, ry, rz), 'XYZ')
        wd = Vector((d.get('dx', 0.0), d.get('dy', 0.0), d.get('dz', 0.0)))
        if n == 'spine1':     # the head is ~0.17 m deep: lean about the base lifts it so the front edge never sinks into the floor
            pitch = d.get('p', 0.0); wd.z += 0.19 * abs(math.sin(math.radians(pitch))) + lift_extra
        b.location = b.bone.matrix_local.to_3x3().inverted() @ wd if n != 'root' else Vector((0, 0, 0))

MESHES = []
def make_action(arm_ob, name, N, sampler, lift_fn=None):
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm_ob.animation_data_create(); arm_ob.animation_data.action = act
    for f in range(N + 1):
        pose = sampler(f); apply_pose(arm_ob, pose, lift_fn(pose) if lift_fn else 0.0)
        mz = eval_min_z(MESHES, arm_ob)           # ground contact: whatever sinks below the floor lifts the body (root stays put)
        if mz < -0.0005:
            apply_pose(arm_ob, pose, (lift_fn(pose) if lift_fn else 0.0) - mz)
        for b in arm_ob.pose.bones:
            b.keyframe_insert('rotation_euler', frame=f); b.keyframe_insert('location', frame=f)
    act.use_frame_range = True; act.frame_start = 0; act.frame_end = N
    return act

def linearize(act):
    try: fcs = list(act.fcurves)
    except Exception:
        fcs = []
        for layer in act.layers:
            for strip in layer.strips:
                for cb in strip.channelbags: fcs += list(cb.fcurves)
    for fc in fcs:
        for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'

# ------- clip definitions -------
def clip_idle():
    N = 120
    def f(fr):
        a = 2 * math.pi * fr / N; pulse = max(0.0, math.sin(2 * math.pi * (fr - 66) / 24)) if 66 <= fr <= 78 else 0.0     # a creak
        return merge(spine(p1=0.8 * math.sin(a), p2=1.2 * math.sin(a - 0.7), p3=1.8 * math.sin(a - 1.4), y3=1.5 * math.sin(2 * a) + 4.5 * pulse, y2=1.0 * math.sin(2 * a - 0.5),
                           r2=1.2 * math.sin(a), r3=2.0 * math.sin(a - 0.8), dz=0.006 * (1 + math.sin(2 * a)) / 2),
                     head(sp=2.0 + 1.5 * math.sin(2 * a), spR=2.0 + 1.5 * math.sin(2 * a + 1.5), sw=1.5 * math.sin(a + 1.0), swL=1.0 * math.sin(2 * a), swR=1.0 * math.sin(2 * a + 2.0)),
                     feet(pl=1.5 * math.sin(a), pr=1.5 * math.sin(a + 3.14)))
    return N, f

def clip_move():
    N = 30
    def f(fr):
        a = 2 * math.pi * fr / N; air = max(0.0, math.sin(a)); land = max(0.0, -math.sin(a))
        return merge(spine(p1=9 + 6 * math.sin(a), p2=-2 + 4 * math.sin(a - 0.9), p3=2 + 7 * math.sin(a - 1.7), y1=3 * math.sin(a), y2=-3 * math.sin(a - 0.5), y3=4 * math.sin(a - 1.2),
                           r3=2.5 * math.sin(a), dz=0.14 * air * (1 - 0.3 * air) - 0.035 * land ** 1.5),
                     head(sp=9 * air - 5 * land + 3, sw=-9 * math.cos(a), swL=-5 * math.sin(a - 0.5), swR=-5 * math.sin(a - 0.5)),
                     feet(pl=-35 * air + 22 * land, pr=-30 * air + 22 * land, lift_l=0.05 * air, lift_r=0.04 * air))
    return N, f

def clip_hit():
    N = 20
    K = [(0, {}, 'lin'),
         (2, merge(spine(p1=-14, p2=-9, p3=-12, y3=8, dz=0.035), head(sp=18, sw=-12, swL=-6, swR=-6), feet(pl=-14, pr=-14, lift_l=0.03, lift_r=0.02)), 'out'),
         (5, merge(spine(p1=-19, p2=-13, p3=-16, y3=11, y2=4, dz=0.03), head(sp=22, sw=-16, swL=-8, swR=-8), feet(pl=-8, pr=-8)), 'out'),
         (9, merge(spine(p1=-4, p2=3, p3=7, y3=-6, y2=-2, dz=0.005), head(sp=6, sw=6, swL=4, swR=4)), 'smooth'),
         (13, merge(spine(p1=-6, p2=-3, p3=-5, y3=4), head(sp=9, sw=-4, swL=-2, swR=-2)), 'smooth'),
         (17, merge(spine(p1=-1, p2=1, p3=2, y3=-1.5), head(sp=3, sw=1.5)), 'smooth'), (20, {}, 'smooth')]
    return N, keyed(K)

def clip_brace():
    N = 16
    set_ = merge(spine(p1=9, p2=5, p3=11, y3=0, dz=-0.03), head(sp=14, sw=14, swL=6, swR=6), feet(pl=10, pr=10))
    K = [(0, {}, 'lin'), (3, set_, 'out'), (5, merge(spine(p1=10, p2=6, p3=12, dz=-0.035), head(sp=16, sw=15, swL=7, swR=7), feet(pl=12, pr=12)), 'smooth'),
         (11, merge(spine(p1=9, p2=5, p3=11, dz=-0.033), head(sp=15, sw=14, swL=6, swR=6), feet(pl=11, pr=11)), 'lin'), (16, {}, 'smooth')]
    return N, keyed(K, [(5, 12, 0.8, ['spine1', 'spine2', 'spine3'])])

def clip_sweep():
    N = SWEEP_N
    wind = lambda a, b, c: merge(spine(p1=-12, p2=-1.5, p3=-0.5, y1=35 * a, y2=15 * a, y3=10 * a, dz=-0.02 * b), head(sp=6 + 18 * c, sw=4, swL=-5 * a, swR=-5 * a), feet(pl=6, pr=6))
    K = [(0, {}, 'lin'),
         (SWEEP['pose_end'], wind(1.0, 1, 0), 'smooth'),                                 # tell pose: twisted back 60, leaned back 10
         (SWEEP['commit'], merge(wind(1.03, 1.6, 1), spine(p1=-9, p2=-2, p3=-1, y1=36, y2=16, y3=11)), 'snap'),   # glint: bristles flare, body coils
         (33, merge(wind(1.12, 2.0, 1), spine(p1=-11, p2=-3, p3=-2, y1=39, y2=18, y3=12)), 'smooth'),               # creep of tension before the swing
         (SWEEP['hit'][0], merge(spine(p1=2, p2=0, p3=0, y1=38, y2=17, y3=11, dz=-0.02), head(sp=26, sw=4, swL=-5, swR=-5), feet(pl=4, pr=4)), 'in'),
         (40, merge(spine(p1=18, p2=10, p3=5, y1=-2, y2=-9, y3=-16), head(sp=34, sw=-6, swL=-14, swR=-14), feet(pl=-10, pr=-10)), 'lin'),
         (SWEEP['hit'][1], merge(spine(p1=24, p2=14, p3=8, y1=-40, y2=-47, y3=-55), head(sp=34, sw=-8, swL=-16, swR=-16), feet(pl=-14, pr=-14)), 'lin'),      # sweep spans the whole hit window
         (48, merge(spine(p1=26, p2=18, p3=14, y1=-66, y2=-72, y3=-82), head(sp=24, sw=-4, swL=-6, swR=-6), feet(pl=-8, pr=-8)), 'out'),                   # follow-through, handle whips past
         (53, merge(spine(p1=21, p2=12, p3=8, y1=-58, y2=-56, y3=-60), head(sp=16, sw=3, swL=3, swR=3)), 'smooth'),
         (62, merge(spine(p1=9, p2=5, p3=3, y1=-26, y2=-22, y3=-20), head(sp=8, sw=2)), 'smooth'), (N, {}, 'smooth')]
    return N, keyed(K, [(SWEEP['pose_end'], SWEEP['commit'], 0.8, SPINE), (SWEEP['commit'], 34, 0.6, SPINE)])

def clip_poke():
    N = POKE_N
    back = merge(spine(p1=-14, p2=-4, p3=-2, y3=-3, dz=-0.03), head(sp=8, sw=-8, swL=-3, swR=-3), feet(pl=10, pr=10))     # lean back 20 in all
    K = [(0, {}, 'lin'),
         (POKE['pose_end'], back, 'out'),
         (POKE['commit'], merge(back, spine(p1=-15, p2=-5, p3=-3, y3=6, dz=-0.035), head(sp=12, sw=-10)), 'smooth'),      # glint: the handle twitches
         (19, merge(spine(p1=-15, p2=-5, p3=-3, y3=6, dz=-0.04), head(sp=12, sw=-10), feet(pl=10, pr=10)), 'lin'),
         (23, merge(spine(p1=2, p2=3, p3=3, y3=0, dz=-0.01), head(sp=8, sw=6, swL=4, swR=4), feet(pl=-28, pr=-4, lift_l=0.07)), 'in'),
         (POKE['hit'][0], merge(spine(p1=22, p2=11, p3=9, dz=0.0), head(sp=14, sw=14, swL=8, swR=8), feet(pl=-10, pr=-30, lift_r=0.05)), 'snap'),            # thrust: handle tip drives forward
         (31, merge(spine(p1=25, p2=13, p3=11), head(sp=16, sw=16, swL=9, swR=9), feet(pl=-8, pr=-8)), 'out'),
         (POKE['hit'][1], merge(spine(p1=24, p2=12, p3=10), head(sp=15, sw=15, swL=8, swR=8)), 'lin'),
         (40, merge(spine(p1=12, p2=4, p3=2, y3=-2), head(sp=9, sw=5, swL=2, swR=2)), 'smooth'), (N, {}, 'smooth')]
    return N, keyed(K, [(POKE['pose_end'], POKE['commit'], 0.7, SPINE)])

def clip_riposte():
    N = RIPOSTE_N
    crouch = merge(spine(p1=-14, p2=-6, p3=-3, y3=4, dz=-0.05), head(sp=-6, sw=-10, swL=-4, swR=-4), feet(pl=14, pr=14))
    K = [(0, {}, 'lin'),
         (RIPOSTE['pose_end'], crouch, 'out'),
         (RIPOSTE['commit'], merge(spine(p1=-15, p2=-7, p3=-4, y3=-3, dz=-0.04), head(sp=30, sw=10, swL=6, swR=6), feet(pl=10, pr=10)), 'snap'),   # glint: bristles flare out
         (22, merge(spine(p1=-17, p2=-8, p3=-5, y3=3, dz=-0.045), head(sp=32, sw=8, swL=5, swR=5), feet(pl=12, pr=12)), 'smooth'),
         (24, merge(spine(p1=-19, p2=-9, p3=-6, dz=-0.05), head(sp=24, sw=-8, swL=-4, swR=-4), feet(pl=14, pr=14)), 'smooth'),
         (RIPOSTE['hit'][0], merge(spine(p1=14, p2=-9, p3=-12, dz=0.0), head(sp=36, sw=34, swL=14, swR=14), feet(pl=-14, pr=-14)), 'in3'),         # bristle face shoves forward
         (30, merge(spine(p1=17, p2=-11, p3=-15), head(sp=38, sw=36, swL=15, swR=15), feet(pl=-10, pr=-10)), 'out'),
         (RIPOSTE['hit'][1], merge(spine(p1=15, p2=-10, p3=-13), head(sp=34, sw=30, swL=12, swR=12)), 'lin'),
         (40, merge(spine(p1=3, p2=2, p3=6, y3=-3), head(sp=14, sw=4)), 'smooth'), (N, {}, 'smooth')]
    return N, keyed(K, [(RIPOSTE['pose_end'], RIPOSTE['commit'], 0.7, SPINE), (RIPOSTE['commit'], 24, 0.9, SPINE)])

def clip_death():
    N = 72
    K = [(0, {}, 'lin'),
         (4, merge(spine(p1=-6, p2=-3, p3=-5, y3=8, dz=0.02), head(sp=20, sw=-8, swL=-6, swR=-6), feet(pl=-10, pr=-10)), 'out'),
         (12, merge(spine(p1=-13, p2=-6, p3=-8, y3=-9, y2=-3), head(sp=16, sw=-6, swL=-4, swR=-4), feet(pl=6, pr=6)), 'smooth'),
         (18, merge(spine(p1=-9, p2=-4, p3=-3, y3=10), head(sp=12, sw=2, swL=2, swR=2)), 'smooth'),          # false recovery
         (24, merge(spine(p1=-22, p2=-8, p3=-5, y3=-6), head(sp=20, sw=-8, swL=-6, swR=-6), feet(pl=10, pr=10)), 'smooth'),   # tips past balance
         (40, merge(spine(p1=-90, p2=-5, p3=-9, y3=-4), head(sp=30, sw=-10, swL=-8, swR=-8), feet(pl=24, pr=24, lift_l=0.03)), 'in'),   # the topple
         (44, merge(spine(p1=-83, p2=-3, p3=-12, y3=4, dz=0.0), head(sp=8, sw=6, swL=6, swR=6), feet(pl=-6, pr=-6)), 'out'),            # bounce
         (50, merge(spine(p1=-90, p2=-1, p3=4, y3=-3), head(sp=12, sw=-3, swL=-3, swR=-3), feet(pl=10, pr=10)), 'in'),
         (56, merge(spine(p1=-90, p2=0, p3=-1), head(sp=8, sw=0), feet(pl=14, pr=18)), 'smooth'),
         (72, merge(spine(p1=-90, p2=0, p3=-1), head(sp=7, sw=0), feet(pl=16, pr=20)), 'lin')]
    return N, keyed(K, [(4, 22, 0.9, SPINE)])

CLIPS = [('Idle', clip_idle), ('Move', clip_move), ('Hit', clip_hit), ('Brace', clip_brace), ('Death', clip_death),
         ('Attack_broom_sweep', clip_sweep), ('Attack_broom_poke', clip_poke), ('Attack_broom_riposte', clip_riposte)]

def eval_min_z(ob_list, arm_ob):
    dg = bpy.context.evaluated_depsgraph_get(); dg.update(); mz = 9.0
    for ob in ob_list:
        ev = ob.evaluated_get(dg); me = ev.to_mesh(); mw = ob.matrix_world
        for v in me.vertices: mz = min(mz, (mw @ v.co).z)
        ev.to_mesh_clear()
    return mz

# ------------------------------------------------------------------ main
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene; sc.render.fps = 60; sc.render.fps_base = 1.0; sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0
    bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
    texdir = os.path.join(OUT, 'tex'); make_textures(texdir)
    m_straw, m_wood = make_material('Broom_Straw', texdir), make_material('Broom_Wood', texdir)
    m_twine, m_iron = make_material('Broom_Twine', texdir), make_material('Broom_Iron', texdir)
    m_ember = make_material('Broom_Ember', texdir, ember=True)
    bristles = build_bristles(m_straw); handle = build_handle(m_wood); band = build_band(m_twine, m_iron); eyes = build_eyes(m_ember)
    arm_ob = build_armature()
    foot_idx = {'foot_L': set(bristles['foot_L']), 'foot_R': set(bristles['foot_R'])}
    skin(bristles, arm_ob, bristle_fn(foot_idx)); skin(handle, arm_ob, spine_fn); skin(band, arm_ob, lambda v: ({'spine3': 1.0} if v.co.z > 1.0 else {'spine1': 1.0}))
    skin(eyes, arm_ob, lambda v: {'spine1': 1.0})
    for ob in (bristles, handle, band, eyes): ob.parent = arm_ob
    for ob in (bristles,): del ob['foot_L'], ob['foot_R']
    calibrate(arm_ob)
    meshes = [bristles, handle, band, eyes]; MESHES.extend(meshes)
    # ---- actions
    acts = {}; lift_death = 0.0
    for name, fn in CLIPS:
        N, sampler = fn()
        if name == 'Death':
            a = make_action(arm_ob, 'tmp_death', N, sampler); arm_ob.animation_data.action = a
            apply_pose(arm_ob, sampler(N)); bpy.context.view_layer.update()
            mn = min(eval_min_z(meshes, arm_ob), 9.0); lift_death = -mn if mn < 0 else 0.0
            bpy.data.actions.remove(a)
            print('death final min z', mn, '-> extra lift', lift_death)
            def lf(pose, L=lift_death): return L * min(1.0, max(0.0, -pose.get('spine1', {}).get('p', 0.0) / 90.0))
            acts[name] = make_action(arm_ob, name, N, sampler, lf)
        else:
            acts[name] = make_action(arm_ob, name, N, sampler)
        linearize(acts[name])
    # ---- stats and floor checks
    print('TRIS', {ob.name: tri_count(ob) for ob in meshes}, 'total', sum(tri_count(ob) for ob in meshes))
    bpy.context.view_layer.update(); report = {}
    for name, fn in CLIPS:
        N, sampler = fn(); arm_ob.animation_data.action = acts[name]
        if hasattr(arm_ob.animation_data, 'action_slot') and acts[name].slots: arm_ob.animation_data.action_slot = acts[name].slots[0]
        worst = 9.0
        for f in range(0, N + 1, 2):
            sc.frame_set(f); worst = min(worst, eval_min_z(meshes, arm_ob))
        report[name] = (N, round(worst, 3))
    print('CLIPS (frames, lowest vertex z)', report)
    arm_ob.animation_data.action = acts['Idle']
    if hasattr(arm_ob.animation_data, 'action_slot') and acts['Idle'].slots: arm_ob.animation_data.action_slot = acts['Idle'].slots[0]
    sc.frame_set(0)
    if not NO_RENDER: render_poses(arm_ob, acts, meshes)
    if not NO_EXPORT: export(arm_ob, meshes, texdir)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'Broom.blend'))

# ------------------------------------------------------------------ preview renders and sheet
def setup_render(w, h):
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.use_denoising = True; sc.cycles.device = 'CPU'
    sc.render.resolution_x, sc.render.resolution_y = w, h; sc.render.image_settings.file_format = 'PNG'
    w_ = bpy.data.worlds.new('W'); w_.use_nodes = True; w_.node_tree.nodes['Background'].inputs[0].default_value = (0.045, 0.032, 0.026, 1); w_.node_tree.nodes['Background'].inputs[1].default_value = 1.0
    sc.world = w_
    gm = bpy.data.materials.new('Floor'); gm.use_nodes = True; b = gm.node_tree.nodes['Principled BSDF']; b.inputs['Base Color'].default_value = (0.20, 0.17, 0.14, 1); b.inputs['Roughness'].default_value = 0.9
    bpy.ops.mesh.primitive_plane_add(size=14, location=(0, 0, -0.002)); fl = bpy.context.object; fl.data.materials.append(gm); fl.name = 'PreviewFloor'
    def light(name, kind, loc, energy, color, size=1.0):
        d = bpy.data.lights.new(name, kind); d.energy = energy; d.color = color
        if kind == 'AREA': d.size = size
        o = bpy.data.objects.new(name, d); o.location = loc; bpy.context.collection.objects.link(o)
        t = o.constraints.new('TRACK_TO'); t.target = tgt; t.track_axis = 'TRACK_NEGATIVE_Z'; t.up_axis = 'UP_Y'
    tgt = bpy.data.objects.new('Tgt', None); tgt.location = (0, 0, 0.85); bpy.context.collection.objects.link(tgt)
    light('Key', 'AREA', (-2.6, -2.8, 2.6), 900, (1.0, 0.58, 0.28), 2.5)      # warm firelight
    light('Fill', 'AREA', (3.2, -2.0, 1.2), 160, (0.45, 0.55, 0.85), 3.0)     # cool bounce
    light('Rim', 'AREA', (1.5, 3.0, 2.8), 500, (1.0, 0.75, 0.5), 2.0)
    cd = bpy.data.cameras.new('Cam'); cd.lens = 70; co = bpy.data.objects.new('Cam', cd); co.location = (3.3, -4.4, 2.0); bpy.context.collection.objects.link(co)
    t = co.constraints.new('TRACK_TO'); t.target = tgt; t.track_axis = 'TRACK_NEGATIVE_Z'; t.up_axis = 'UP_Y'; sc.camera = co

POSES = [('Rest (Idle 0)', 'Idle', 0), ('Sweep tell peak (f14)', 'Attack_broom_sweep', 14), ('Sweep glint (f22)', 'Attack_broom_sweep', 22), ('Sweep strike (f40)', 'Attack_broom_sweep', 40),
         ('Poke tell (f10)', 'Attack_broom_poke', 10), ('Poke thrust (f30)', 'Attack_broom_poke', 30), ('Riposte glint (f15)', 'Attack_broom_riposte', 15), ('Riposte shove (f30)', 'Attack_broom_riposte', 30),
         ('Brace (f8)', 'Brace', 8), ('Hit recoil (f5)', 'Hit', 5), ('Death topple (f34)', 'Death', 34), ('Death held (f72)', 'Death', 72)]

def render_poses(arm_ob, acts, meshes):
    sc = bpy.context.scene; setup_render(420, 620); td = os.path.join(OUT, 'tiles'); os.makedirs(td, exist_ok=True); labels = []
    for i, (label, clip, fr) in enumerate(POSES):
        arm_ob.animation_data.action = acts[clip]
        if hasattr(arm_ob.animation_data, 'action_slot') and acts[clip].slots: arm_ob.animation_data.action_slot = acts[clip].slots[0]
        sc.frame_set(fr); p = os.path.join(td, f'{i:02d}.png'); sc.render.filepath = p; bpy.ops.render.render(write_still=True); labels.append((p, label))
    json.dump(labels, open(os.path.join(td, 'labels.json'), 'w'))
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    subprocess.run([shutil.which('python') or 'py', os.path.abspath(__file__), '--compose', td, SHEET], check=False)

def compose(td, out):
    from PIL import Image, ImageDraw
    labels = json.load(open(os.path.join(td, 'labels.json'))); cols = 4; w, h = 420, 620; rows = (len(labels) + cols - 1) // cols
    S = Image.new('RGB', (cols * w, rows * h + 34), (22, 18, 16)); d = ImageDraw.Draw(S)
    for k, (p, lab) in enumerate(labels):
        S.paste(Image.open(p).convert('RGB'), ((k % cols) * w, (k // cols) * h)); d.text(((k % cols) * w + 8, (k // cols) * h + 6), lab, fill=(255, 235, 200))
    d.text((8, rows * h + 10), 'Sweeping broom key poses. No broom concept art on file (Reference/private/concepts has none), so no reference tile.', fill=(220, 200, 170))
    S.save(out); print('sheet', out)

# ------------------------------------------------------------------ export
def export(arm_ob, meshes, texdir):
    os.makedirs(EXPORT, exist_ok=True)
    for f in os.listdir(texdir): shutil.copy(os.path.join(texdir, f), os.path.join(EXPORT, f))
    for ob in bpy.data.objects: ob.select_set(False)
    arm_ob.select_set(True)
    for m in meshes: m.select_set(True)
    bpy.context.view_layer.objects.active = arm_ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT, 'Broom.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'}, axis_forward='-Z', axis_up='Y',
                             global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='OFF',
                             add_leaf_bones=False, use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP', use_custom_props=False)
    print('EXPORT', EXPORT, sorted(os.listdir(EXPORT)))

if '--compose' in sys.argv:
    i = sys.argv.index('--compose'); compose(sys.argv[i + 1], sys.argv[i + 2])
else:
    main()
