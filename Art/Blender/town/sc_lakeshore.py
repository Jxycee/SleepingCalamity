"""LakeShore: Hollowmere's lake-shore and stream dressing (ring 3, queue job Q10), one FBX of separate root-level objects.

Rock_Shore_A/B/C (rounded lake-worn stones, damp algae band in the bottom 18 cm, moss on the tops), Rock_Stream_A/B/C (chunky
flat-topped boulders for the rock weirs, wet dark lower half, moss tops), Reed_A/B/C (25-40 tapered two-sided blade ribbons with
slight bends, cattail heads on A and B, warm green to straw), Jetty (1.8 x 9 m oak landing jetty: chunky gapped planks with three
off-true boards, 10 round piles to -3.0 m, two tall mooring posts with a rope coil and a wrapped line, a short side ladder,
iron nails and bands).
Axes: Blender z up, FBX -Z forward / Y up, metres, transforms applied. Rocks: pivot = ground centre of the footprint (bottom 3 cm
buried); Reeds: pivot at the clump base; Jetty: pivot = shore end of the deck, deck top at 0, deck runs along Blender -Y = Unity
local +Z, piles to -3.0, waterline -0.6 (wet stain below).
Textures (all unique, no vertex colours, no shader needed): LS_Rock 2048 (6 rocks in one atlas, high-poly -> low bake: normal,
albedo, roughness, AO from 20k-tri sculpts with the graded sandstone tileable as the stone base), LS_Jetty 2048 (self bake of the
deck, beams, rope, ladder, iron from the graded oak_timber / iron tileables), LS_Pile 512 x 2048 (piles: oak_timber with the
waterline wet band and algae painted along height, z -3.0..+1.2 -> v 0..1), LS_Reed 512 x 1024 (blade variants, cattail head,
stalk). Each set is BaseColor + Normal + ORM (R AO, G roughness, B metal).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_lakeshore.py -- --out <scratch>
      [--no-render] [--export] [--views jetty,jettyfar,shore,weir,rocks,reeds] [--rres 1100] [--samples 32] [--tris] [--hi 4]

  --export writes <out>/LakeShore.fbx (path_mode STRIP) and <out>/tex/*.png; the PC/cloud copies them to
  Assets/_Project/Content/Art/Town/Ring3/Lake/ (see SOURCES.md there).

Status (2026-10-31, cloud, Blender 4.5.3): exported to Assets/_Project/Content/Art/Town/Ring3/Lake/LakeShore.fbx (+ Textures/, SOURCES.md),
sheet Reference/hollowmere/lake/SHEET_lakeshore_r1.jpg. Bake run takes about 15 min on 4 CPU cores at --hi 5 (--hi 4 for a quick pass; --from-blend
<scratch>/lakeshore.blend --views ... re-renders the review without baking). Deterministic seeds.
  Tris: Rock_Shore A/B/C 640/480/330, Rock_Stream A/B/C 880/720/540, Reed A/B/C 732/586/560, Jetty 5,650 (cap 6,000): total 11,118 (cap ~20k).
  Self-score (sc-asset-review rubric, 1 improvement round after the first look): silhouette 4, proportion 4, form 3.5, surface 3.5, colour 3.5,
  wear 3.5, set fit 3.5, glass/metal (iron, rope) 3.5 = 29/40.
  Next visible gains: more and deeper moss on the boulders (the reference is far greener), crisper fractured facets on the stream boulders, reed
  blades with a visible mid-rib normal and more blades per clump at the edge, a mossy/slimed lower deck edge, ladder rungs that read at the waterline.
"""
import sys, os, math, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT; argv = FK.argv; TEX = FK.TEX
RRES = int(FK.arg('--rres', '1100')); SAMPLES = int(FK.arg('--samples', '32'))
VIEWS = FK.arg('--views', 'jetty,jettyfar,shore,weir,reeds').split(',')
HI = int(FK.arg('--hi', '5'))            # icosphere subdivisions of the rock sculpt (5 = 20k tris)
TEXOUT = os.path.join(OUT, 'tex'); os.makedirs(TEXOUT, exist_ok=True)
FK.RRES = RRES; FK.SAMPLES = SAMPLES

def rng(s): return FK.rng('ls' + s)
def clamp01(v): return max(0.0, min(1.0, v))
def sstep(a, b, x):
    t = clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t)
def smin(a, b, k): return -k * math.log(math.exp(-a / k) + math.exp(-b / k))
def fbm(p, oct=4):
    s = 0.0; a = 0.5; f = 1.0
    for _ in range(oct):
        s += a * noise.noise(p * f); a *= 0.5; f *= 2.03
    return s

# ----------------------------------------------------------------------------- node helpers
class NB:
    def __init__(s, nt): s.nt = nt; s.N = nt.nodes; s.L = nt.links
    def put(s, sock, v):
        if isinstance(v, bpy.types.NodeSocket): s.L.new(v, sock)
        else: sock.default_value = v
    def mixc(s, a, b, t, blend='MIX'):
        n = s.N.new('ShaderNodeMix'); n.data_type = 'RGBA'; n.blend_type = blend
        s.put(n.inputs[0], t); s.put(n.inputs[6], a); s.put(n.inputs[7], b); return n.outputs[2]
    def mixf(s, a, b, t):
        n = s.N.new('ShaderNodeMix'); n.data_type = 'FLOAT'
        s.put(n.inputs[0], t); s.put(n.inputs[2], a); s.put(n.inputs[3], b); return n.outputs[0]
    def math(s, op, a, b=None, clamp=False):
        n = s.N.new('ShaderNodeMath'); n.operation = op; n.use_clamp = clamp
        s.put(n.inputs[0], a)
        if b is not None: s.put(n.inputs[1], b)
        return n.outputs[0]
    def rng(s, v, a, b, c=0.0, d=1.0):
        n = s.N.new('ShaderNodeMapRange'); n.clamp = True
        s.put(n.inputs[0], v); n.inputs[1].default_value = a; n.inputs[2].default_value = b
        n.inputs[3].default_value = c; n.inputs[4].default_value = d; return n.outputs[0]
    def noise(s, vec, scale, detail=5.0, rough=0.55):
        n = s.N.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail; n.inputs['Roughness'].default_value = rough
        s.L.new(vec, n.inputs['Vector']); return n.outputs[0]
    def sep(s, col):
        n = s.N.new('ShaderNodeSeparateXYZ'); s.L.new(col, n.inputs[0]); return n.outputs
    def img(s, path, nc, vec=None, box=False, blend=0.3):
        n = s.N.new('ShaderNodeTexImage'); n.image = FK._img(path, nc)
        if box: n.projection = 'BOX'; n.projection_blend = blend
        if vec is not None: s.L.new(vec, n.inputs[0])
        return n

def new_mat(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    return m, NB(m.node_tree), m.node_tree.nodes['Principled BSDF']

def tex_base(folder): return os.path.join(TEX, folder, folder.split('/')[-1])

def name_nodes(m, alb, rough, metal):
    """Label the sockets the bake passes read: albedo colour, roughness float, metallic float."""
    nt = m.node_tree
    for key, sock in (('SC_ALB', alb), ('SC_ROUGH', rough), ('SC_METAL', metal)):
        r = nt.nodes.new('NodeReroute'); r.name = key; nt.links.new(sock, r.inputs[0])

# ----------------------------------------------------------------------------- source materials (what the bakes sample)
def rock_src(name, tint, band, moss, seed, wetfrac=None, algae=0.8):
    """High-poly stone: graded sandstone tileable (box projected, object space) x tint, grey-green damp algae band
    below `band` m, moss on up-facing noisy patches. Object space z = 0 is the ground line."""
    m, b, bsdf = new_mat(name)
    tc = b.N.new('ShaderNodeTexCoord'); mp = b.N.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (0.7, 0.7, 0.7); mp.inputs['Location'].default_value = (seed * 1.7, seed * 0.9, seed * 2.3)
    b.L.new(tc.outputs['Object'], mp.inputs[0])
    n0 = b.noise(tc.outputs['Object'], 2.4 + 0.15 * seed, 5.0)
    base = b.mixc((0.135, 0.115, 0.092, 1), (0.27, 0.23, 0.18, 1), b.rng(n0, 0.38, 0.62, 0.0, 1.0))
    base = b.mixc(base, tint, 1.0, 'MULTIPLY')
    grain = b.noise(tc.outputs['Object'], 38.0, 8.0, 0.65)
    base = b.mixc(base, (0.75, 0.74, 0.72, 1), b.rng(grain, 0.35, 0.65, 0.0, 1.0), 'MULTIPLY')
    big = b.noise(tc.outputs['Object'], 1.3 + 0.1 * seed, 4.0)
    patch = b.rng(big, 0.35, 0.65, 0.0, 1.0)
    base = b.mixc(base, (0.62, 0.62, 0.60, 1), patch, 'MULTIPLY')          # darker mottled patches (value variation)
    small = b.noise(tc.outputs['Object'], 9.0, 6.0)
    base = b.mixc(base, (0.82, 0.80, 0.76, 1), b.rng(small, 0.5, 0.8, 0, 0.6), 'MULTIPLY')
    z = b.sep(tc.outputs['Object'])[2]
    wob = b.noise(tc.outputs['Object'], 3.0, 3.0)
    zz = b.math('ADD', z, b.math('MULTIPLY', b.math('SUBTRACT', wob, 0.5), 0.07))
    d = b.rng(zz, band, band * 0.45, 0.0, 1.0)                             # 1 deep in the band, 0 above it
    wetc = b.mixc(base, (0.30, 0.33, 0.26, 1), 0.85, 'MULTIPLY')
    c = b.mixc(base, wetc, d)
    a = b.math('MULTIPLY', b.math('MULTIPLY', d, b.math('SUBTRACT', 1.0, d)), 4.0)
    an = b.rng(b.noise(tc.outputs['Object'], 14.0, 4.0), 0.30, 0.52, 0.5, 1.0)
    algc = b.mixc((0.02, 0.04, 0.01, 1), (0.05, 0.075, 0.02, 1), b.noise(tc.outputs['Object'], 25.0, 3.0))
    c = b.mixc(c, algc, b.math('MULTIPLY', b.math('MULTIPLY', a, an, True), algae, True))
    geo = b.N.new('ShaderNodeNewGeometry')
    nzs = b.sep(geo.outputs['Normal'])[2]
    up = b.rng(nzs, 0.15, 0.62, 0.0, 1.0)
    mn = b.rng(b.noise(tc.outputs['Object'], 5.5 + 0.2 * seed, 8.0, 0.6), 0.30, 0.46, 0.0, 1.0)
    mm = b.math('MULTIPLY', b.math('MULTIPLY', up, mn), moss * (1.0), True)
    mm = b.math('MULTIPLY', mm, b.math('SUBTRACT', 1.0, d), True)
    mosscol = b.mixc((0.025, 0.04, 0.006, 1), (0.065, 0.085, 0.011, 1), b.noise(tc.outputs['Object'], 18.0, 3.0))
    c = b.mixc(c, mosscol, b.rng(mm, 0.08, 0.35, 0.0, 1.0))
    msk = b.rng(mm, 0.08, 0.35, 0.0, 1.0)
    r0 = b.math('ADD', 0.72, b.math('MULTIPLY', b.math('SUBTRACT', grain, 0.5), 0.5))
    r = b.mixf(r0, 0.30, b.math('MULTIPLY', d, 0.85))
    r = b.mixf(r, 0.93, msk)
    h = b.math('ADD', b.math('MULTIPLY', grain, 1.2), b.math('MULTIPLY', b.noise(tc.outputs['Object'], 6.0, 6.0), 1.4))
    h = b.math('ADD', h, b.math('MULTIPLY', msk, 0.9))
    bump = b.N.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 1.0; bump.inputs['Distance'].default_value = 0.02
    b.L.new(h, bump.inputs['Height']); b.L.new(bump.outputs[0], bsdf.inputs['Normal'])
    b.L.new(c, bsdf.inputs['Base Color']); b.L.new(r, bsdf.inputs['Roughness'])
    name_nodes(m, c, r, b.math('MULTIPLY', 0.0, 0.0))
    return m

def uv_tile_src(name, folder, tint, normal=1.0, rough_add=0.0, varattr=True, metal=False):
    """Tileable sampled on UVMap (metre UVs) x tint x per-board value (colour attribute `var`)."""
    m, b, bsdf = new_mat(name)
    uv = b.N.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'
    alb = b.img(tex_base(folder) + '_albedo.png', False, uv.outputs[0])
    nrm = b.img(tex_base(folder) + '_normal.png', True, uv.outputs[0])
    orm = b.img(tex_base(folder) + '_orm.png', True, uv.outputs[0])
    c = b.mixc(alb.outputs[0], tint, 1.0, 'MULTIPLY')
    if varattr:
        at = b.N.new('ShaderNodeAttribute'); at.attribute_name = 'var'; at.attribute_type = 'GEOMETRY'
        c = b.mixc(c, at.outputs[0], 1.0, 'MULTIPLY')
    so = b.N.new('ShaderNodeSeparateColor'); b.L.new(orm.outputs[0], so.inputs[0])
    r = b.math('ADD', so.outputs[1], rough_add, True)
    mt = so.outputs[2] if metal else b.math('MULTIPLY', 0.0, 0.0)
    nm = b.N.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = normal
    b.L.new(nrm.outputs[0], nm.inputs['Color']); b.L.new(nm.outputs[0], bsdf.inputs['Normal'])
    b.L.new(c, bsdf.inputs['Base Color']); b.L.new(r, bsdf.inputs['Roughness'])
    name_nodes(m, c, r, mt)
    return m

def rope_src():
    m, b, bsdf = new_mat('LS_src_rope')
    uv = b.N.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'
    mp = b.N.new('ShaderNodeMapping'); b.L.new(uv.outputs[0], mp.inputs[0])
    mp.inputs['Scale'].default_value = (1.0, 1.0, 1.0)
    wv = b.N.new('ShaderNodeTexWave'); wv.wave_type = 'BANDS'; wv.bands_direction = 'DIAGONAL'
    wv.inputs['Scale'].default_value = 7.0; wv.inputs['Distortion'].default_value = 0.8
    b.L.new(mp.outputs[0], wv.inputs['Vector'])
    f = b.rng(wv.outputs[0], 0.3, 0.7, 0.0, 1.0)
    nz = b.noise(uv.outputs[0], 60.0, 4.0)
    c = b.mixc((0.30, 0.20, 0.10, 1), (0.62, 0.48, 0.28, 1), f)
    c = b.mixc(c, (0.8, 0.75, 0.7, 1), b.rng(nz, 0.4, 0.7, 0.6, 1.0), 'MULTIPLY')
    h = b.math('ADD', f, b.math('MULTIPLY', nz, 0.4))
    bump = b.N.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.6; bump.inputs['Distance'].default_value = 0.01
    b.L.new(h, bump.inputs['Height']); b.L.new(bump.outputs[0], bsdf.inputs['Normal'])
    b.L.new(c, bsdf.inputs['Base Color']); bsdf.inputs['Roughness'].default_value = 0.9
    name_nodes(m, c, b.math('MULTIPLY', 0.0, 0.0) if False else 0.9 and b.math('ADD', 0.9, 0.0), b.math('MULTIPLY', 0.0, 0.0))
    return m

JSRC = {}
def jsrc(key):
    if key in JSRC: return JSRC[key]
    mk = {
        'oak': lambda: uv_tile_src('LS_src_oak', 'B1_town/oak_timber', (2.6, 3.7, 5.2, 1), 1.3),
        'oak_lt': lambda: uv_tile_src('LS_src_oak_lt', 'B1_town/oak_timber', (3.4, 4.6, 6.0, 1), 1.3),
        'oak_dk': lambda: uv_tile_src('LS_src_oak_dk', 'B1_town/oak_timber', (1.9, 2.6, 3.6, 1), 1.3),
        'iron': lambda: uv_tile_src('LS_src_iron', 'B1_town/iron', (0.6, 0.6, 0.65, 1), 1.0, varattr=False, metal=True),
        'rope': rope_src,
        'pile': lambda: uv_tile_src('LS_src_pile', 'B1_town/oak_timber', (1, 1, 1, 1), 1.0, varattr=False),
    }
    JSRC[key] = mk[key](); return JSRC[key]
FK.M = lambda k: jsrc(k)

# ----------------------------------------------------------------------------- generic tube (posts, rope, cattails, rungs, bands)
def tube(pc, key, pts, radii, sides, uvf, cap0=False, cap1=False, wob=0.0, seed=0.0, up=None):
    """Tube along polyline pts (Vector list), radius per point, `sides` around. uvf(i, j_frac, k) -> (u, v), k = 0 ring, 1 cap centre.
    Frame N x B = T so the ring order is outward-wound. Appended to the Piece pc with material slot of `key`."""
    bm = pc.bm; si = pc.slot(key); uvl = pc.uv
    n = len(pts); T = []
    for i in range(n):
        a = pts[max(i - 1, 0)]; c = pts[min(i + 1, n - 1)]
        T.append((c - a).normalized())
    ref = Vector(up) if up else (Vector((0, 1, 0)) if abs(T[0].y) < 0.9 else Vector((1, 0, 0)))
    Nv = (ref - T[0] * ref.dot(T[0])).normalized(); frames = []
    for i in range(n):
        if i > 0:
            Nv = (Nv - T[i] * Nv.dot(T[i]))
            if Nv.length < 1e-5: Nv = Vector((1, 0, 0))
            Nv.normalize()
        Bv = T[i].cross(Nv)   # N x B = T  =>  B = T x N
        frames.append((Nv.copy(), Bv))
    rings = []
    for i in range(n):
        Nf, Bf = frames[i]; ring = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            r = radii[i]
            if wob:
                q = Vector((math.cos(a), math.sin(a), pts[i].z * 1.3)) * 1.7 + Vector((seed, seed * 1.3, seed * 0.7))
                r *= 1 + wob * noise.noise(q)
            ring.append(bm.verts.new(pts[i] + (Nf * math.cos(a) + Bf * math.sin(a)) * r))
        rings.append(ring)
    def face(vs, uvs):
        try: f = bm.faces.new(vs)
        except ValueError: return
        f.material_index = si
        for l, uv in zip(f.loops, uvs): l[uvl].uv = uv
    for i in range(n - 1):
        for j in range(sides):
            j1 = (j + 1) % sides
            face([rings[i][j], rings[i][j1], rings[i + 1][j1], rings[i + 1][j]],
                 [uvf(i, j / sides, 0), uvf(i, (j + 1) / sides, 0), uvf(i + 1, (j + 1) / sides, 0), uvf(i + 1, j / sides, 0)])
    for cap, i in ((cap0, 0), (cap1, n - 1)):
        if not cap: continue
        cen = bm.verts.new(pts[i])
        for j in range(sides):
            j1 = (j + 1) % sides
            vs = [cen, rings[i][j1], rings[i][j]] if i == 0 else [cen, rings[i][j], rings[i][j1]]
            uvs = [uvf(i, 0.5, 1), uvf(i, (j + 1) / sides, 0), uvf(i, j / sides, 0)] if i == 0 else [uvf(i, 0.5, 1), uvf(i, j / sides, 0), uvf(i, (j + 1) / sides, 0)]
            face(vs, uvs)

def to_obj(pc, name, smooth_mats=(), flat=False):
    pc.bm.normal_update()
    me = bpy.data.meshes.new(name); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats: me.materials.append(m)
    for p in me.polygons: p.use_smooth = not flat
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob

def tris(ob): return sum(len(p.vertices) - 2 for p in ob.data.polygons)

# ----------------------------------------------------------------------------- bake machinery
def make_img(name, w, h, nc, fill):
    im = bpy.data.images.new(name, w, h, alpha=False)
    im.generated_color = fill
    if nc: im.colorspace_settings.name = 'Non-Color'
    return im

def setup_bake():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'
    sc.render.bake.target = 'IMAGE_TEXTURES'
    if sc.world is None: sc.world = bpy.data.worlds.new('BakeW')
    sc.world.light_settings.distance = 0.45
    sc.cycles.use_denoising = False

def surface_emit(mat, sock_name):
    """Point the material output at an emission of the labelled socket; returns the undo closure."""
    nt = mat.node_tree
    out = [n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'][0]
    old = out.inputs['Surface'].links[0].from_socket if out.inputs['Surface'].links else None
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs['Strength'].default_value = 1.0
    src = nt.nodes[sock_name].outputs[0]
    nt.links.new(src, em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs['Surface'])
    def undo():
        if old: nt.links.new(old, out.inputs['Surface'])
        nt.nodes.remove(em)
    return undo

def bake_pass(low, highs, srcmats, target_mat, img, btype, margin=14, cage=0.05, ray=0.0, samples=4, sock=None):
    sc = bpy.context.scene; sc.cycles.samples = samples
    node = target_mat.node_tree.nodes['SC_TARGET']; node.image = img; target_mat.node_tree.nodes.active = node
    undos = [surface_emit(m, sock) for m in srcmats] if sock else []
    bpy.ops.object.select_all(action='DESELECT')
    for h in highs: h.select_set(True)
    low.select_set(True); bpy.context.view_layer.objects.active = low
    kw = dict(type=btype, margin=margin, margin_type='EXTEND', use_clear=False)
    if highs: kw.update(use_selected_to_active=True, cage_extrusion=cage, max_ray_distance=ray)
    if btype == 'NORMAL': kw.update(normal_space='TANGENT')
    bpy.ops.object.bake(**kw)
    for u in undos: u()

def target_material(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes.new('ShaderNodeTexImage'); n.name = 'SC_TARGET'
    return m

def img_np(im):
    w, h = im.size; a = np.empty(w * h * 4, dtype=np.float32); im.pixels.foreach_get(a); return a.reshape(h, w, 4)

def save_img(arr, name, nc, path):
    h, w = arr.shape[:2]
    im = bpy.data.images.new(name, w, w if False else h, alpha=False) if False else bpy.data.images.new(name, w, h, alpha=False)
    if nc: im.colorspace_settings.name = 'Non-Color'
    a = np.ones((h, w, 4), dtype=np.float32); a[..., :arr.shape[2]] = arr[..., :arr.shape[2]] if arr.shape[2] <= 4 else arr[..., :4]
    im.pixels.foreach_set(a.ravel()); im.update()
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    return im

def pack_orm(ao, rough, metal):
    h, w = rough.shape[:2]; o = np.ones((h, w, 3), dtype=np.float32)
    o[..., 0] = ao[..., 0]; o[..., 1] = rough[..., 0]; o[..., 2] = metal[..., 0] if metal is not None else 0.0
    return o

def final_mat(name, base_p, norm_p, orm_p):
    m, b, bsdf = new_mat(name)
    uv = b.N.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'
    alb = b.img(base_p, False, uv.outputs[0]); nrm = b.img(norm_p, True, uv.outputs[0]); orm = b.img(orm_p, True, uv.outputs[0])
    so = b.N.new('ShaderNodeSeparateColor'); b.L.new(orm.outputs[0], so.inputs[0])
    b.L.new(b.mixc(alb.outputs[0], (1, 1, 1, 1), so.outputs[0], 'MULTIPLY') if False else alb.outputs[0], bsdf.inputs['Base Color'])
    b.L.new(so.outputs[1], bsdf.inputs['Roughness']); b.L.new(so.outputs[2], bsdf.inputs['Metallic'])
    nm = b.N.new('ShaderNodeNormalMap'); b.L.new(nrm.outputs[0], nm.inputs['Color']); b.L.new(nm.outputs[0], bsdf.inputs['Normal'])
    return m

def uv_fit(ob, cell):
    """Normalise ob's UV island bbox uniformly into cell = (u0, v0, u1, v1) with a small gutter."""
    me = ob.data; uvl = me.uv_layers.active
    n = len(me.loops); a = np.empty(n * 2, dtype=np.float32); uvl.data.foreach_get('uv', a); a = a.reshape(n, 2)
    lo = a.min(0); hi = a.max(0); sc = min((cell[2] - cell[0]) / (hi[0] - lo[0]), (cell[3] - cell[1]) / (hi[1] - lo[1])) * 0.985
    a = (a - lo) * sc + np.array([cell[0], cell[1]]) + 0.004
    uvl.data.foreach_set('uv', a.ravel().astype(np.float32))

def smart_uv(ob, margin=0.012, layer='UVMap', faces=None, angle=80):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    if layer not in ob.data.uv_layers: ob.data.uv_layers.new(name=layer)
    ob.data.uv_layers.active = ob.data.uv_layers[layer]
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(ob.data)
    for f in bm.faces: f.select = (faces is None) or (f.index in faces)
    bmesh.update_edit_mesh(ob.data)
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin, area_weight=0.6, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')

def isometrize_uv(ob, layer, faces, margin=0.006):
    """Make each UV island of `faces` on `layer` locally isometric (same metres per unit on U and V), then equalise island scale and repack.
    smart_project with area_weight leaves 2.5x+ anisotropy on bevels and curved parts, which HollowmereFaultScan.Uv() flags as stretch. Per island:
    G = area-weighted mean of J^T J (J = d world / d uv per triangle), uv' = sqrt(G) uv with det 1 (island area unchanged). Run before the bake."""
    me = ob.data; bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); uv = bm.loops.layers.uv[layer]
    fs = [f for f in bm.faces if f.index in faces]
    par = {f.index: f.index for f in fs}
    def find(a):
        while par[a] != a: par[a] = par[par[a]]; a = par[a]
        return a
    for f in fs:
        for l in f.loops:
            o = l.link_loop_radial_next
            if o is l or o.face is f or o.face.index not in par: continue
            if (l[uv].uv - o.link_loop_next[uv].uv).length < 1e-6 and (l.link_loop_next[uv].uv - o[uv].uv).length < 1e-6:
                par[find(f.index)] = find(o.face.index)
    acc = {}; cen = {}
    for f in fs:
        k = find(f.index); n = cen.setdefault(k, [Vector((0, 0)), 0])
        for l in f.loops: n[0] += l[uv].uv; n[1] += 1
        ls = list(f.loops)
        for t in range(1, len(ls) - 1):
            a, b, c = ls[0].vert.co, ls[t].vert.co, ls[t + 1].vert.co; ua, ub, uc = ls[0][uv].uv, ls[t][uv].uv, ls[t + 1][uv].uv
            e1, e2 = b - a, c - a; d1, d2 = ub - ua, uc - ua
            det = d1.x * d2.y - d1.y * d2.x; wa = e1.cross(e2).length * 0.5
            if wa < 1e-9 or abs(det) < 1e-14: continue
            inv = 1.0 / det; pu = (e1 * d2.y - e2 * d1.y) * inv; pv = (e2 * d1.x - e1 * d2.x) * inv
            g = acc.setdefault(k, [0.0, 0.0, 0.0, 0.0])
            g[0] += wa * pu.dot(pu); g[1] += wa * pu.dot(pv); g[2] += wa * pv.dot(pv); g[3] += wa
    mats = {}
    for k, (A, B, C, W) in acc.items():
        w, V = np.linalg.eigh(np.array([[A, B], [B, C]]) / W); w = np.maximum(w, 1e-12)
        M = V @ np.diag(np.sqrt(w)) @ V.T; mats[k] = M / math.sqrt(abs(np.linalg.det(M)))
    for f in fs:
        k = find(f.index); M = mats.get(k)
        if M is None: continue
        c = cen[k][0] / cen[k][1]
        for l in f.loops:
            q = M @ np.array(l[uv].uv - c); l[uv].uv = (c.x + q[0], c.y + q[1])
    bm.to_mesh(me); bm.free(); me.update()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    me.uv_layers.active = me.uv_layers[layer]
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(me)
    for f in bm.faces: f.select = f.index in faces
    bmesh.update_edit_mesh(me)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(margin_method='FRACTION', margin=margin, rotate=True, scale=True, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')

# ----------------------------------------------------------------------------- rocks
ROCKS = {   # name: (w, d, h, tris, flat_top, tint, band m, moss, wet-lower-half?)
    'Rock_Shore_A': ((1.20, 0.95, 0.72), 640, False, (1.05, 0.97, 0.86, 1), 0.18, 1.0),
    'Rock_Shore_B': ((0.82, 0.70, 0.50), 480, False, (1.0, 0.93, 0.84, 1), 0.17, 0.9),
    'Rock_Shore_C': ((0.52, 0.44, 0.34), 330, False, (1.1, 1.0, 0.88, 1), 0.15, 0.8),
    'Rock_Stream_A': ((1.80, 1.30, 0.95), 880, True, (0.98, 0.90, 0.80, 1), 0.46, 0.9),
    'Rock_Stream_B': ((1.30, 1.10, 0.80), 720, True, (1.0, 0.92, 0.82, 1), 0.38, 0.85),
    'Rock_Stream_C': ((0.95, 0.85, 0.64), 540, True, (1.05, 0.95, 0.83, 1), 0.30, 0.8),
}
ROCK_ORDER = list(ROCKS)

def sculpt_rock(name, dims, flat_top, seed):
    R = rng(name)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=HI, radius=1.0)
    planes = []
    for k in range(7 if flat_top else 6):
        n = Vector((R.gauss(0, 1), R.gauss(0, 1), R.gauss(0, 1) * (0.5 if flat_top else 0.8))).normalized()
        if n.z < -0.2: n.z = -n.z * 0.3
        planes.append((n.normalized(), R.uniform(0.70, 0.86)))
    if flat_top:
        planes.append((Vector((R.uniform(-0.08, 0.08), R.uniform(-0.08, 0.08), 1.0)).normalized(), 0.60))
    off = Vector((seed * 3.7, seed * 1.9, seed * 5.3))
    for v in bm.verts:
        d = v.co.normalized(); q = d * 1.25 + off
        r = 1.0 + 0.20 * fbm(q * 0.9, 3) + 0.06 * fbm(q * 3.0, 3) + 0.010 * noise.noise(q * 9.0)
        strata = noise.noise(Vector((q.x * 2.0, q.y * 2.0, q.z * 9.0)))
        r -= 0.018 * (1.0 - abs(strata)) ** 6 * 4            # shallow worn bedding grooves
        for pn, pd in planes:
            c = d.dot(pn)
            if c > 0.08: r = smin(r, pd / c, 0.10)
        v.co = d * r
    bm.verts.ensure_lookup_table()
    zs = [v.co.z for v in bm.verts]; zmin = min(zs)
    for v in bm.verts:                                       # flat buried bottom
        if v.co.z < zmin * 0.55: v.co.z = zmin * 0.55
    xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
    sx = dims[0] / (max(xs) - min(xs)); sy = dims[1] / (max(ys) - min(ys)); sz = dims[2] / (max(zs) - min(zs))
    cx = (max(xs) + min(xs)) / 2; cy = (max(ys) + min(ys)) / 2
    for v in bm.verts:
        v.co = Vector(((v.co.x - cx) * sx, (v.co.y - cy) * sy, (v.co.z - min(zs)) * sz - 0.03))
    bm.normal_update()
    me = bpy.data.meshes.new(name + '_hi'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name + '_hi', me); bpy.context.scene.collection.objects.link(ob)
    return ob

def decimate(hi, name, target):
    lo = hi.copy(); lo.data = hi.data.copy(); lo.name = name; lo.data.name = name
    bpy.context.scene.collection.objects.link(lo)
    bpy.ops.object.select_all(action='DESELECT'); lo.select_set(True); bpy.context.view_layer.objects.active = lo
    md = lo.modifiers.new('dec', 'DECIMATE'); md.decimate_type = 'COLLAPSE'
    md.ratio = target / max(1, len(lo.data.polygons)); md.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier='dec')
    return lo

def build_rocks():
    setup_bake()
    S = 2048
    tgt = target_material('LS_RockBake')
    im_alb = make_img('rk_alb', S, S, False, (0.30, 0.29, 0.26, 1)); im_nrm = make_img('rk_nrm', S, S, True, (0.5, 0.5, 1.0, 1))
    im_rgh = make_img('rk_rgh', S, S, True, (0.8, 0.8, 0.8, 1)); im_ao = make_img('rk_ao', S, S, True, (1, 1, 1, 1))
    cells = {}
    cw = 1.0 / 3.0
    for i, n in enumerate(ROCK_ORDER):                      # shore rocks bottom row, stream rocks top row
        col = i % 3; row = 0 if n.startswith('Rock_Shore') else 1
        cells[n] = (col * cw, row * 0.5, (col + 1) * cw, row * 0.5 + 0.5)
    lows = []
    for i, n in enumerate(ROCK_ORDER):
        dims, tri, flat, tint, band, moss = ROCKS[n]
        hi = sculpt_rock(n, dims, flat, 1 + i)
        src = rock_src('LS_src_' + n, tint, band, moss, 1 + i)
        hi.data.materials.append(src)
        lo = decimate(hi, n, tri)
        lo.data.materials.clear(); lo.data.materials.append(tgt)
        for p in lo.data.polygons: p.use_smooth = True
        smart_uv(lo, 0.02); uv_fit(lo, cells[n])
        ext = 0.05 + 0.04 * max(dims)
        print('  bake', n, 'hi', len(hi.data.polygons), 'lo', tris(lo))
        bake_pass(lo, [hi], [src], tgt, im_nrm, 'NORMAL', cage=ext, ray=ext * 2.2)
        bake_pass(lo, [hi], [src], tgt, im_alb, 'EMIT', cage=ext, ray=ext * 2.2, sock='SC_ALB')
        bake_pass(lo, [hi], [src], tgt, im_rgh, 'EMIT', cage=ext, ray=ext * 2.2, sock='SC_ROUGH')
        bake_pass(lo, [hi], [src], tgt, im_ao, 'AO', cage=ext, ray=ext * 2.2, samples=40)
        lows.append(lo)
        bpy.data.objects.remove(hi)
    a = img_np(im_alb); nr = img_np(im_nrm); rg = img_np(im_rgh); ao = img_np(im_ao)
    p = lambda s: os.path.join(TEXOUT, 'LakeShore_Rock_' + s + '.png')
    save_img(a, 'LakeShore_Rock_BaseColor', False, p('BaseColor'))
    save_img(nr, 'LakeShore_Rock_Normal', True, p('Normal'))
    save_img(pack_orm(ao, rg, None), 'LakeShore_Rock_ORM', True, p('ORM'))
    fm = final_mat('LS_Rock', p('BaseColor'), p('Normal'), p('ORM'))
    for lo in lows:
        lo.data.materials.clear(); lo.data.materials.append(fm)
    return lows

# ----------------------------------------------------------------------------- reeds
REED_TEX = (512, 1024)
def reed_textures():
    W, H = REED_TEX; R = np.random.RandomState(7)
    v = (np.arange(H) + 0.5) / H; u = (np.arange(W) + 0.5) / W
    U, V = np.meshgrid(u, v)
    stops_v = [0.0, 0.07, 0.22, 0.5, 0.78, 1.0]
    stops_c = np.array([(0.64, 0.62, 0.40), (0.50, 0.58, 0.24), (0.33, 0.50, 0.13), (0.38, 0.55, 0.14), (0.60, 0.60, 0.22), (0.78, 0.64, 0.32)])
    base = np.stack([np.interp(V, stops_v, stops_c[:, k]) for k in range(3)], -1).astype(np.float32)
    alb = base.copy(); nrm = np.zeros((H, W, 3), np.float32); nrm[..., 2] = 1
    ao = np.ones((H, W), np.float32); rough = np.full((H, W), 0.58, np.float32)
    streak = R.rand(1, W) ; streak = np.repeat(streak, H, 0) * 0.5 + R.rand(H, W) * 0.12
    for k in range(4):                                       # four blade variants, 64 px wide each
        u0, u1 = k * 64, (k + 1) * 64
        loc = np.linspace(-1, 1, 64)[None, :]                # across the blade
        cell = base[:, u0:u1].copy()
        shade = [0.96, 1.05, 0.90, 1.0][k]; warm = [(1.0, 1.0, 1.0), (1.03, 1.0, 0.93), (0.93, 1.02, 0.96), (1.06, 1.0, 0.9)][k]
        cell *= np.array(warm)[None, None, :] * shade
        rib = np.exp(-(loc / 0.14) ** 2) * 0.10
        cell *= (1.0 + rib - (np.abs(loc) ** 3) * 0.16)[:, :, None]
        cell *= (1.0 + (streak[:, u0:u1] - 0.3) * 0.25)[:, :, None]
        dry = (R.rand(H, 64) > 0.9965) * 0.35                # a few brown flecks
        cell = cell * (1 - dry[:, :, None]) + dry[:, :, None] * np.array([0.35, 0.25, 0.1])
        alb[:, u0:u1] = cell
        nrm[:, u0:u1, 0] = (loc * 0.45) * np.ones((H, 1)); nrm[:, u0:u1, 2] = np.sqrt(1 - nrm[:, u0:u1, 0] ** 2)
        ao[:, u0:u1] = np.clip(0.45 + V[:, u0:u1] * 4.0, 0, 1) * (1 - 0.15 * np.abs(loc) ** 2)
    # cattail head (u 256..384): brown suede with speckle, tip lighter; stalk (u 384..512): green-straw
    hu = slice(256, 384)
    sp = R.rand(H, 128) * 0.5 + np.repeat(R.rand(H // 8, 128), 8, 0) * 0.5
    head = np.stack([0.30 + 0.12 * sp, 0.16 + 0.07 * sp, 0.075 + 0.04 * sp], -1) * (1.0 + 0.0 * V[:, hu, None])
    alb[:, hu] = head; ao[:, hu] = 0.85 + 0.15 * sp; rough[:, hu] = 0.85
    nrm[:, hu, 0] = (sp - 0.5) * 0.5; nrm[:, hu, 1] = (np.roll(sp, 1, 0) - 0.5) * 0.5; nrm[:, hu, 2] = np.sqrt(1 - nrm[:, hu, 0] ** 2 - nrm[:, hu, 1] ** 2)
    su = slice(384, 512)
    stalk = np.stack([np.interp(V[:, su], [0, 0.6, 1], [0.34, 0.44, 0.62]), np.interp(V[:, su], [0, 0.6, 1], [0.46, 0.50, 0.50]), np.interp(V[:, su], [0, 0.6, 1], [0.14, 0.18, 0.24])], -1)
    alb[:, su] = stalk * (1 + (R.rand(1, 128, 1) - 0.5) * 0.1)
    n = np.sqrt(nrm[..., 0] ** 2 + nrm[..., 1] ** 2 + nrm[..., 2] ** 2)
    nrm = np.stack([nrm[..., 0] / n * 0.5 + 0.5, nrm[..., 1] / n * 0.5 + 0.5, nrm[..., 2] / n * 0.5 + 0.5], -1)
    orm = np.stack([ao, rough, np.zeros_like(ao)], -1)
    p = lambda s: os.path.join(TEXOUT, 'LakeShore_Reed_' + s + '.png')
    save_img(np.clip(alb, 0, 1), 'LakeShore_Reed_BaseColor', False, p('BaseColor'))
    save_img(nrm, 'LakeShore_Reed_Normal', True, p('Normal')); save_img(orm, 'LakeShore_Reed_ORM', True, p('ORM'))
    return final_mat('LS_Reed', p('BaseColor'), p('Normal'), p('ORM'))

def reed(name, nblades, heads, seed, hmin, hmax, spread, mat):
    R = rng(name); pc = FK.Piece(name); bm = pc.bm; uvl = pc.uv; si = pc.slot_obj(mat) if hasattr(pc, 'slot_obj') else None
    pc.mats.append(mat); si = 0
    def face(vs, uvs):
        try: f = bm.faces.new(vs)
        except ValueError: return
        f.material_index = si
        for l, uv in zip(f.loops, uvs): l[uvl].uv = uv
    nseg = 4; wf = [1.0, 0.97, 0.86, 0.58, 0.0]
    for bi in range(nblades):
        th = R.uniform(0, 2 * math.pi); rr = spread * math.sqrt(R.random())
        base = Vector((math.cos(th) * rr, math.sin(th) * rr, -0.04))
        L = R.uniform(hmin, hmax) * (1.0 - 0.18 * (rr / spread))   # outer blades a little shorter
        lean = math.radians(R.uniform(2, 6) + 11 * (rr / spread) ** 1.2)
        bend = math.radians(R.uniform(8, 30))
        bdir = th + R.uniform(-0.5, 0.5)
        d = Vector((math.cos(bdir), math.sin(bdir), 0)); side0 = Vector((-d.y, d.x, 0))
        w0 = R.uniform(0.026, 0.040); twist = R.uniform(-0.7, 0.7); k = R.randrange(4)
        pts = [base.copy()]; p = base.copy(); ang = lean
        for s in range(1, nseg + 1):
            t = s / nseg; ang = lean + bend * t ** 1.5
            p = p + (Vector((0, 0, 1)) * math.cos(ang) + d * math.sin(ang)) * (L / nseg)
            pts.append(p.copy())
        rows = []
        for s, pt in enumerate(pts):
            t = s / nseg
            tang = ((pts[min(s + 1, nseg)] - pts[max(s - 1, 0)])).normalized()
            sd = Matrix.Rotation(twist * t, 3, tang) @ side0
            sd = (sd - tang * sd.dot(tang)).normalized()
            nrm = tang.cross(sd).normalized()
            w = w0 * wf[s] * 0.5
            u0 = k * 0.125 + 0.125 * 0.04; u1 = k * 0.125 + 0.125 * 0.96; um = (u0 + u1) / 2
            if s == nseg:
                rows.append(([bm.verts.new(pt)], [(um, t)], [bm.verts.new(pt + nrm * -0.0006)]))
            else:
                a = bm.verts.new(pt - sd * w); b = bm.verts.new(pt + sd * w)
                a2 = bm.verts.new(pt - sd * w - nrm * 0.0006); b2 = bm.verts.new(pt + sd * w - nrm * 0.0006)
                rows.append(([a, b], [(u0, t), (u1, t)], [a2, b2]))
        for s in range(nseg):
            (A, uA, A2), (B, uB, B2) = rows[s], rows[s + 1]
            if len(B) == 1:
                face([A[0], A[1], B[0]], [uA[0], uA[1], uB[0]])
                face([A2[1], A2[0], B2[0]], [uA[1], uA[0], uB[0]])
            else:
                face([A[0], A[1], B[1], B[0]], [uA[0], uA[1], uB[1], uB[0]])
                face([A2[1], A2[0], B2[0], B2[1]], [uA[1], uA[0], uB[0], uB[1]])
    for hi in range(heads):                                  # cattails
        th = R.uniform(0, 2 * math.pi); rr = spread * R.uniform(0.1, 0.6)
        H = R.uniform(hmax * 0.82, hmax * 1.0); dx = Vector((math.cos(th), math.sin(th), 0)) * R.uniform(0.04, 0.16)
        base = Vector((math.cos(th + 1) * rr, math.sin(th + 1) * rr, -0.04))
        pts = [base + Vector((0, 0, 0)) + dx * (t ** 2) + Vector((0, 0, H * t)) for t in (0, 0.33, 0.66, 1.0)]
        tube(pc, 'x', pts, [0.012, 0.010, 0.009, 0.0085], 5, lambda i, jf, kk: (0.75 + 0.25 * jf, i / 3.0 if kk == 0 else 0.5), wob=0.0)
        top = pts[-1]; tdir = (pts[-1] - pts[-2]).normalized()
        hp = [top - tdir * 0.02 + tdir * (0.19 * t) for t in (0, 0.25, 0.5, 0.75, 1.0)]
        tube(pc, 'x', hp, [0.020, 0.030, 0.033, 0.029, 0.012], 6, lambda i, jf, kk: (0.5 + 0.25 * jf, 0.1 + 0.8 * i / 4.0), cap1=True, wob=0.05, seed=hi + seed)
        sp = [hp[-1], hp[-1] + tdir * 0.05, hp[-1] + tdir * 0.11]
        tube(pc, 'x', sp, [0.006, 0.0045, 0.0015], 4, lambda i, jf, kk: (0.75 + 0.25 * jf, 0.9), cap1=True)
    ob = to_obj(pc, name)
    return ob

# tube() looks its slot up through pc.slot('x'); give reeds a single-slot override
_old_slot = FK.Piece.slot
def _slot(self, key):
    if key == 'x': return 0
    return _old_slot(self, key)
FK.Piece.slot = _slot

# ----------------------------------------------------------------------------- jetty
DECK_W = 1.8; DECK_L = 9.0; PLK_T = 0.075
POSTS_Y = [-0.40, -2.50, -4.60, -6.70, -8.65]
POST_X = 0.93
PILE_ZLO, PILE_ZHI = -3.0, 1.2

def jetty_pieces():
    R = rng('jetty'); pc = FK.Piece('Jetty')
    plank_n = [0]
    var = []                      # (face_start, face_end, value) for the per-board 'var' colour attribute
    def addbox(key, size, xf, **kw):
        n0 = len(pc.bm.faces); FK.add_box(pc, key, size, xf, **kw); n1 = len(pc.bm.faces)
        return n0, n1
    # ---- planks (grain along x)
    y = -0.04; i = 0; nails = []
    SAG = {7: (0.0, 1.9, 0.0), 19: (-0.028, 0.0, 1.4), 28: (0.014, -2.6, 0.0), 33: (-0.012, 1.2, -0.6)}   # idx: (dz, roll deg, pitch deg)
    while y > -DECK_L + 0.12:
        w = R.uniform(0.185, 0.235) if i not in (19, 28) else (0.255 if i == 19 else 0.165)
        t = PLK_T + R.uniform(-0.006, 0.008)
        L = DECK_W + R.uniform(-0.05, 0.05); xo = R.uniform(-0.025, 0.025)
        dz, roll, pitch = SAG.get(i, (R.uniform(-0.004, 0.004), R.uniform(-0.45, 0.45), R.uniform(-0.2, 0.2)))
        yaw = R.uniform(-0.9, 0.9) if i not in SAG else R.choice([-1.6, 1.4])
        yc = y - w / 2
        zc = -t / 2 + dz
        xf = Matrix.Translation((xo, yc, zc)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Rotation(math.radians(roll), 4, 'Y') @ Matrix.Rotation(math.radians(pitch), 4, 'X')
        n0, n1 = addbox('oak', (L, w, t), xf, cham=0.011, long_axis=0, wob=0.0035, seed=R.random() * 90)
        shade = R.uniform(0.82, 1.1) if i not in SAG else (0.74 if i in (19, 33) else 1.12)
        var.append((n0, n1, shade)); plank_n[0] = len(var)
        for sx in (-0.55, 0.55):                              # nail heads at the two stringers
            ztop = zc + t / 2 + sx * math.sin(math.radians(roll)) * (1 if True else 0)
            nails.append((Vector((xo + sx, yc + R.uniform(-0.01, 0.01), ztop)), roll))
        y -= w + R.uniform(0.010, 0.022); i += 1
    # ---- stringers and cross bearers (under the deck)
    for sx in (-0.55, 0.55):
        n0, n1 = addbox('oak_dk', (0.16, DECK_L + 0.06, 0.20), Matrix.Translation((sx, -DECK_L / 2, -PLK_T - 0.10)), cham=0.02, long_axis=1, wob=0.004, seed=sx * 7 + 3)
        var.append((n0, n1, 1.0))
    for k, py in enumerate(POSTS_Y):
        n0, n1 = addbox('oak_dk', (2.06, 0.15, 0.17), Matrix.Translation((R.uniform(-0.02, 0.02), py, -PLK_T - 0.20 - 0.085)) @ Matrix.Rotation(math.radians(R.uniform(-0.6, 0.6)), 4, 'Y'), cham=0.018, long_axis=0, wob=0.004, seed=k * 11 + 1)
        var.append((n0, n1, 0.95))
    # ---- nails: little four-sided pyramid heads
    for p, roll in nails:
        for (ax, ay) in ((0, 0),):
            s = 0.017; h = 0.011
            bm = bmesh.new(); vs = [bm.verts.new((p.x + dx * s, p.y + dy * s, p.z)) for dx, dy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            tp = [bm.verts.new((p.x + dx * s * 0.62, p.y + dy * s * 0.62, p.z + h)) for dx, dy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            for j in range(4):
                bm.faces.new([vs[j], vs[(j + 1) % 4], tp[(j + 1) % 4], tp[j]])
            bm.faces.new(tp[::-1]); bm.normal_update()
            bm.verts.ensure_lookup_table()
            uvl = bm.loops.layers.uv.new('UVMap')
            for f in bm.faces:
                for l in f.loops: l[uvl].uv = (l.vert.co.x * 3, l.vert.co.y * 3)
            n0 = len(pc.bm.faces); FK._merge(pc, bm, 'iron'); var.append((n0, len(pc.bm.faces), 1.0))
    # ---- piles and mooring posts (material 'pile', direct UVs: u around, v = height 0..1 over z -3.0..1.2)
    def vmap(z): return (z - PILE_ZLO) / (PILE_ZHI - PILE_ZLO)
    for k, py in enumerate(POSTS_Y):
        for sgn in (-1, 1):
            moor = (k == len(POSTS_Y) - 1)
            x = sgn * (POST_X + (0.04 if moor else 0.0)) + R.uniform(-0.015, 0.015); yy = py + R.uniform(-0.03, 0.03)
            top = 1.05 + R.uniform(-0.03, 0.04) if moor else 0.02 + R.uniform(-0.01, 0.03)
            r0 = 0.165 if moor else 0.14
            lean = Vector((R.uniform(-0.01, 0.01), R.uniform(-0.01, 0.01), 0))
            zs = [PILE_ZLO, -1.8, -0.9, -0.35, 0.0] + ([0.45, 0.8, top - 0.06, top] if moor else [top])
            zs = sorted(set(zs))
            pts = [Vector((x, yy, z)) + lean * (z - PILE_ZLO) * 0.6 for z in zs]
            rad = [r0 * (0.9 + 0.1 * sstep(PILE_ZLO, 0, z)) * (1.0 if z <= (top - 0.07) or not moor else 0.88) for z in zs]
            if moor: rad[-1] = r0 * 0.7; rad[-2] = r0 * 0.96
            tube(pc, 'pile', pts, rad, 10, lambda i, jf, kk, zs=zs, col=(k * 2 + (sgn > 0)) % 4: ((col + (jf if kk == 0 else 0.5)) * 0.25, vmap(zs[i])), cap0=False, cap1=True, wob=0.05, seed=k * 3 + sgn)
            # iron band(s)
            for bz in ([-PLK_T - 0.20 - 0.085, 0.9] if moor else [-PLK_T - 0.20 - 0.085]):
                ctr = Vector((x, yy, bz)) + lean * (bz - PILE_ZLO) * 0.6
                rb = r0 * 1.06 + 0.012
                tube(pc, 'iron', [ctr - Vector((0, 0, 0.035)), ctr + Vector((0, 0, 0.035))], [rb, rb], 10,
                     lambda i, jf, kk: (jf * 2.0, i * 0.2), cap0=False, cap1=False)
    # ---- rope: coil on the deck by the +x mooring post and a wrapped line on the -x post with a hanging tail
    cc = Vector((0.40, -8.35, 0.0)); pts = []; rad = []
    for s in range(0, 3 * 12 + 1):
        t = s / 12.0; a = t * 2 * math.pi
        rr = 0.19 - 0.012 * t
        pts.append(cc + Vector((math.cos(a) * rr, math.sin(a) * rr, 0.027 + t * 0.043 + 0.006 * math.sin(t * 9)))); rad.append(0.027)
    n0 = len(pc.bm.faces)
    tube(pc, 'rope', pts, rad, 6, lambda i, jf, kk: (jf, i * 0.12), cap0=True, cap1=True)
    xm = -(POST_X + 0.04); ym = POSTS_Y[-1]
    pts = [Vector((-0.45, -8.1, 0.03)), Vector((-0.62, -8.2, 0.03)), Vector((xm + 0.14, ym + 0.12, 0.06))]
    for s in range(0, 2 * 10 + 1):
        t = s / 10.0; a = -0.7 + t * 2 * math.pi
        pts.append(Vector((xm + math.cos(a) * 0.205, ym + math.sin(a) * 0.205, 0.62 + t * 0.07)))
    pts += [Vector((xm + 0.03, ym + 0.22, 0.55)), Vector((xm + 0.1, ym + 0.2, 0.2)), Vector((xm + 0.12, ym + 0.2, -0.3)), Vector((xm + 0.1, ym + 0.19, -0.55))]
    # smooth with simple subdivision of the polyline to keep the tube clean
    sm = []
    for i in range(len(pts) - 1):
        sm.append(pts[i]); sm.append((pts[i] + pts[i + 1]) / 2)
    sm.append(pts[-1])
    tube(pc, 'rope', sm, [0.024] * len(sm), 5, lambda i, jf, kk: (jf, i * 0.1), cap0=True, cap1=True)
    var.append((n0, len(pc.bm.faces), 1.0))
    # ---- ladder on the +x side near the far end: two rails, four rungs, iron straps over the deck edge
    ly = [-7.40, -7.95]; lx = DECK_W / 2 + 0.115
    for k, yy in enumerate(ly):
        n0, n1 = addbox('oak_lt', (0.075, 0.095, 1.80), Matrix.Translation((lx, yy, -0.60)) @ Matrix.Rotation(math.radians(R.uniform(-0.8, 0.8)), 4, 'Y'), cham=0.014, long_axis=2, wob=0.004, seed=40 + k)
        var.append((n0, n1, 1.0))
        n0, n1 = addbox('iron', (0.17, 0.05, 0.012), Matrix.Translation((lx - 0.045, yy, 0.0 - 0.015)), cham=0.003)
        n0, n1 = addbox('iron', (0.17, 0.05, 0.012), Matrix.Translation((lx - 0.045, yy, -0.42)), cham=0.003)
        var.append((n0, len(pc.bm.faces), 1.0))
    for k, zr in enumerate((-0.20, -0.56, -0.92, -1.28)):
        pr = [Vector((lx, ly[0] + 0.04, zr)), Vector((lx, (ly[0] + ly[1]) / 2, zr + R.uniform(-0.01, 0.01))), Vector((lx, ly[1] - 0.04, zr))]
        n0 = len(pc.bm.faces)
        tube(pc, 'oak_lt', pr, [0.030, 0.030, 0.030], 6, lambda i, jf, kk: (jf, i * 0.3), cap0=True, cap1=True, up=(0, 0, 1))
        var.append((n0, len(pc.bm.faces), 1.0))
    return pc, var, plank_n[0]

def pile_textures():
    """Piles: oak_timber stacked along height, weathered silver-grey above the splash zone, damp dark brown below -0.35, an algae band
    round the waterline (-0.6), darker and glossier underwater. four strips of 512 wide (one strip = circumference, UV 0.25 wide), 2048 high = z -3.0..1.2, in a 2048 x 2048 atlas."""
    W, H = 512, 2048   # per strip
    def load(stem, nc):
        im = FK._img(os.path.join(TEX, 'B1_town', 'oak_timber', 'oak_timber_' + stem + '.png'), nc)
        w, h = im.size; a = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(a); return a.reshape(h, w, 4)[..., :3]
    alb0 = load('albedo', False); nrm0 = load('normal', True); orm0 = load('orm', True)
    R = np.random.RandomState(11)
    def stack(a, nrm=False):
        tw = a.shape[1] // 2                                  # one metre of the 2 m tile = 1024 px wide
        tiles = [np.roll(a, (R.randint(0, a.shape[0]), 0), axis=(0, 1))[:1024, :tw] if False else np.roll(a, (int(R.randint(0, a.shape[0])), int(R.randint(0, a.shape[1]))), axis=(0, 1))[:1024, :1024] for _ in range(4)]
        big = np.concatenate(tiles, 0)                        # 4096 x 1024
        # grain along v: the tileable's grain runs along image x or y? use as is; downsample by 2 (box)
        return big.reshape(2048, 2, 512, 2, 3).mean((1, 3))
    def column():
        alb = stack(np.clip(alb0, 0, 0.34)); nrm = stack(nrm0); orm = stack(orm0)
        nrm = nrm / np.maximum(np.linalg.norm(nrm * 2 - 1, axis=2, keepdims=True) * 0 + 1, 1)
        z = PILE_ZLO + (np.arange(H) + 0.5) / H * (PILE_ZHI - PILE_ZLO); Z = z[:, None]
        wob = (R.rand(1, W) * 0.5 + 0.5)
        from math import pi
        xs = np.arange(W)[None, :] / W
        wz = Z + 0.06 * np.sin(xs * 2 * pi * 3 + 1.3) + 0.04 * np.sin(xs * 2 * pi * 7 + 0.4) + 0.025 * (np.roll(np.roll(R.rand(H, W), 1, 0), 1, 1) - 0.5)
        wet = np.clip((-0.25 - wz) / 0.35, 0, 1)[..., None]       # 0 above -0.25, 1 below -0.6
        splash = np.clip((0.1 - wz) / 0.35, 0, 1)[..., None] * (1 - wet)
        alg = np.exp(-(((wz + 0.62) / 0.16) ** 2))[..., None] * (0.55 + 0.45 * R.rand(H, W)[..., None]) ** 2
        tint = np.array([1.57, 2.0, 2.6])                         # weathered silver-brown oak (the tileable is very dark)
        c = alb * tint * (1 - 0.35 * splash) * (1 - wet * 0.58) * np.array([1, 1.0, 0.95]) ** wet
        c = c * (1 - alg * 0.8) + alg * 0.8 * np.array([0.12, 0.17, 0.05])
        below = np.clip((-0.9 - wz) / 0.8, 0, 1)[..., None]; c *= (1 - 0.35 * below)
        top = np.clip((wz - 0.7) / 0.5, 0, 1)[..., None]; c = c * (1 + 0.1 * top)     # mooring posts a touch sun-bleached above the deck
        rough = orm[..., 1:2] * (1 - 0.45 * wet) + 0.0
        ao = orm[..., 0:1]
        return c, nrm, rough, ao
    cols = [column() for _ in range(4)]   # four 512 px strips side by side: the atlas is square, so a UV unit is the same on U and V (the piles pick a strip)
    c = np.concatenate([x[0] for x in cols], 1); nrm = np.concatenate([x[1] for x in cols], 1)
    rough = np.concatenate([x[2] for x in cols], 1); ao = np.concatenate([x[3] for x in cols], 1)
    p = lambda s: os.path.join(TEXOUT, 'LakeShore_Pile_' + s + '.png')
    save_img(np.clip(c, 0, 1), 'LakeShore_Pile_BaseColor', False, p('BaseColor'))
    save_img(nrm, 'LakeShore_Pile_Normal', True, p('Normal'))
    save_img(np.concatenate([ao, rough, np.zeros_like(ao)], -1), 'LakeShore_Pile_ORM', True, p('ORM'))
    return final_mat('LS_Pile', p('BaseColor'), p('Normal'), p('ORM'))

def build_jetty():
    setup_bake()
    pc, var, nplank = jetty_pieces()
    # classify faces: deck set (everything except 'pile') bakes; pile faces keep direct UVs
    pile_mat = jsrc('pile')
    ob = to_obj(pc, 'Jetty')
    me = ob.data
    pile_idx = [i for i, m in enumerate(me.materials) if m == pile_mat]
    vattr = me.color_attributes.new('var', 'FLOAT_COLOR', 'CORNER')
    for (a, b_, val) in var:
        for pi in range(a, min(b_, len(me.polygons))):
            for li in me.polygons[pi].loop_indices: vattr.data[li].color = (val, val, val * 0.99, 1.0)
    for p in me.polygons:
        if p.material_index in pile_idx:
            for li in p.loop_indices: vattr.data[li].color = (1, 1, 1, 1)
    # drop the plank undersides (never seen: the deck hides them, only gaps of 1-2 cm) so the deck bake spends its texels on what shows
    drop = set()
    for (a, b_, val) in var[:nplank]:
        for pi in range(a, min(b_, len(me.polygons))):
            if me.polygons[pi].normal.z < -0.8: drop.add(pi)
    bmj = bmesh.new(); bmj.from_mesh(me); bmj.faces.ensure_lookup_table()
    bmesh.ops.delete(bmj, geom=[bmj.faces[i] for i in sorted(drop)], context='FACES'); bmj.to_mesh(me); bmj.free(); me.update()
    pile_idx = [i for i, m in enumerate(me.materials) if m == pile_mat]
    # unique bake UVs for the deck faces
    deck_faces = {p.index for p in me.polygons if p.material_index not in pile_idx}
    smart_uv(ob, 0.006, 'UVBake', deck_faces, angle=45)   # 80 deg islands put the round piles' grazing faces at 5x stretch; 45 deg keeps them under ~1.5
    isometrize_uv(ob, 'UVBake', deck_faces)   # metric islands before the bake (stretch 2.6 -> ~1.2)
    bl = me.uv_layers['UVBake']
    # move pile faces out of the bake range
    for p in me.polygons:
        if p.material_index in pile_idx:
            for li in p.loop_indices: bl.data[li].uv = (9.0, 9.0)
    S = 2048
    tgt = target_material('LS_JettyBake')
    srcmats = [m for i, m in enumerate(me.materials) if i not in pile_idx]
    for m in srcmats:                                        # every source material needs the bake target node, active
        n = m.node_tree.nodes.new('ShaderNodeTexImage'); n.name = 'SC_TARGET'
    pm = [m for i, m in enumerate(me.materials) if i in pile_idx]
    for m in pm:
        n = m.node_tree.nodes.new('ShaderNodeTexImage'); n.name = 'SC_TARGET'
    me.uv_layers.active = bl
    im_alb = make_img('jt_alb', S, S, False, (0.25, 0.22, 0.2, 1)); im_nrm = make_img('jt_nrm', S, S, True, (0.5, 0.5, 1, 1))
    im_rgh = make_img('jt_rgh', S, S, True, (0.8, 0.8, 0.8, 1)); im_ao = make_img('jt_ao', S, S, True, (1, 1, 1, 1)); im_mt = make_img('jt_mt', S, S, True, (0, 0, 0, 1))
    def jbake(img, btype, sock=None, samples=4):
        for m in srcmats:
            m.node_tree.nodes['SC_TARGET'].image = img; m.node_tree.nodes.active = m.node_tree.nodes['SC_TARGET']
        sc = bpy.context.scene; sc.cycles.samples = samples
        undos = [surface_emit(m, sock) for m in srcmats] if sock else []
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        # pile faces would also bake (their uv is out of range, nothing is written)
        for m in pm:
            m.node_tree.nodes['SC_TARGET'].image = img; m.node_tree.nodes.active = m.node_tree.nodes['SC_TARGET']
        kw = dict(type=btype, margin=10, margin_type='EXTEND', use_clear=False)
        if btype == 'NORMAL': kw.update(normal_space='TANGENT')
        bpy.ops.object.bake(**kw)
        for u in undos: u()
    jbake(im_nrm, 'NORMAL'); jbake(im_alb, 'EMIT', 'SC_ALB'); jbake(im_rgh, 'EMIT', 'SC_ROUGH'); jbake(im_mt, 'EMIT', 'SC_METAL')
    jbake(im_ao, 'AO', samples=48)
    a = img_np(im_alb); nr = img_np(im_nrm); rg = img_np(im_rgh); ao = img_np(im_ao); mt = img_np(im_mt)
    p = lambda s: os.path.join(TEXOUT, 'LakeShore_Jetty_' + s + '.png')
    save_img(a, 'LakeShore_Jetty_BaseColor', False, p('BaseColor')); save_img(nr, 'LakeShore_Jetty_Normal', True, p('Normal'))
    save_img(pack_orm(ao, rg, mt), 'LakeShore_Jetty_ORM', True, p('ORM'))
    fm = final_mat('LS_Jetty', p('BaseColor'), p('Normal'), p('ORM'))
    pile_final = pile_textures()
    # final UVs: deck faces take the bake UVs, pile faces keep UVMap; one layer 'UVMap'
    um = me.uv_layers['UVMap']
    for pl in me.polygons:
        if pl.material_index not in pile_idx:
            for li in pl.loop_indices: um.data[li].uv = bl.data[li].uv
    me.uv_layers.remove(bl)
    me.color_attributes.remove(me.color_attributes['var'])
    new_idx = []
    pile_faces = {pl.index for pl in me.polygons if pl.material_index in pile_idx}   # read before clear(): clearing the slots resets every index to 0 (the piles then shipped in LS_Jetty with pile UVs)
    me.materials.clear(); me.materials.append(fm); me.materials.append(pile_final)
    for pl in me.polygons: pl.material_index = 1 if pl.index in pile_faces else 0
    return ob, pile_idx

# ----------------------------------------------------------------------------- assembly
def build():
    objs = []
    objs += build_rocks()
    rm = reed_textures()
    reeds = {'Reed_A': (30, 3, 1.55, 1.95, 0.20), 'Reed_B': (27, 2, 1.20, 1.75, 0.18), 'Reed_C': (40, 0, 1.00, 1.55, 0.24)}
    for i, (n, (nb, nh, hmin, hmax, sp)) in enumerate(reeds.items()):
        objs.append(reed(n, nb, nh, i * 5 + 2, hmin, hmax, sp, rm))
    jt, _ = build_jetty()
    objs.append(jt)
    return objs

def fix_jetty_slots(ob, pile_idx_before): pass

def report(objs):
    total = 0
    for o in objs:
        xs = [o.matrix_world @ Vector(c) for c in o.bound_box]
        mn = Vector((min(v.x for v in xs), min(v.y for v in xs), min(v.z for v in xs))); mx = Vector((max(v.x for v in xs), max(v.y for v in xs), max(v.z for v in xs)))
        t = tris(o); total += t
        print('  %-14s %5d tris  size %.2f x %.2f x %.2f  unity x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f  slots %s' % (
            o.name, t, mx.x - mn.x, mx.z - mn.z, mx.y - mn.y, mn.x, mx.x, mn.z, mx.z, -mx.y, -mn.y, [m.name for m in o.data.materials]))
    print('LakeShore total %d tris' % total)


# ----------------------------------------------------------------------------- review scene (not exported)
def simple_tile(name, folder, tint, scale=2.0):
    m, b, bsdf = new_mat(name)
    uv = b.N.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'
    mp = b.N.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (2.0 / scale,) * 3; b.L.new(uv.outputs[0], mp.inputs[0])
    alb = b.img(tex_base(folder) + '_albedo.png', False, mp.outputs[0]); nrm = b.img(tex_base(folder) + '_normal.png', True, mp.outputs[0])
    b.L.new(b.mixc(alb.outputs[0], tint, 1.0, 'MULTIPLY'), bsdf.inputs['Base Color'])
    nm = b.N.new('ShaderNodeNormalMap'); b.L.new(nrm.outputs[0], nm.inputs['Color']); b.L.new(nm.outputs[0], bsdf.inputs['Normal'])
    bsdf.inputs['Roughness'].default_value = 0.85
    return m

def grid(name, x0, x1, y0, y1, nx, ny, zf, mat, uvs=0.5):
    bm = bmesh.new(); vs = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            x = x0 + (x1 - x0) * i / nx; y = y0 + (y1 - y0) * j / ny; vs[(i, j)] = bm.verts.new((x, y, zf(x, y)))
    uvl = bm.loops.layers.uv.new('UVMap')
    for i in range(nx):
        for j in range(ny):
            f = bm.faces.new([vs[(i, j)], vs[(i + 1, j)], vs[(i + 1, j + 1)], vs[(i, j + 1)]])
            for l in f.loops: l[uvl].uv = (l.vert.co.x * uvs, l.vert.co.y * uvs)
    bm.normal_update(); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob); return ob

def water_mat():
    m, b, bsdf = new_mat('RV_Water')
    bsdf.inputs['Base Color'].default_value = (0.012, 0.05, 0.06, 1); bsdf.inputs['Roughness'].default_value = 0.05
    return m

def dup(ob, name, loc, rot=0.0, scale=1.0):
    o = ob.copy(); o.name = name; bpy.context.scene.collection.objects.link(o)
    o.hide_render = False; o.location = loc; o.rotation_euler = (0, 0, math.radians(rot)); o.scale = (scale,) * 3; return o

def review_scene(objs):
    sc = bpy.context.scene; byn = {o.name: o for o in objs}
    soil = simple_tile('RV_Soil', 'B1_town/packed_soil', (0.9, 0.85, 0.8, 1), 2.0)
    grass = simple_tile('RV_Grass', 'B1_town/meadow_grass', (1.0, 1.0, 1.0, 1), 2.0)
    wm = water_mat()
    def bank(x, y):
        return 0.0 if y >= -0.2 else max(-1.6, 1.6 * (y + 0.2))
    grid('RV_Bank', -30, 30, -30, 8, 60, 76, bank, soil)
    g2 = grid('RV_Grass', -30, 30, 1.2, 12, 30, 12, lambda x, y: 0.02, grass); g2.location.z = 0.0
    w = grid('RV_Water', -60, 60, -60, 0.4, 2, 2, lambda x, y: -0.6, wm)
    fig = bpy.data.objects.new('Fig', bpy.data.meshes.new('Fig')); sc.collection.objects.link(fig)
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.22, radius2=0.2, depth=1.75); bm.to_mesh(fig.data); bm.free()
    fm = bpy.data.materials.new('Figure'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.18, 0.12, 1); fig.data.materials.append(fm)
    fig.location = (0.35, -2.1, 0.875)
    for n in ROCK_ORDER + ['Reed_A', 'Reed_B', 'Reed_C']: byn[n].hide_render = True
    zw = -0.6 - 0.17      # shore stones: waterline reaches the top of the 18 cm band
    shore = [dup(byn['Rock_Shore_A'], 'sA', (-3.2, -0.95, zw), 20), dup(byn['Rock_Shore_B'], 'sB', (-4.4, -0.7, zw - 0.05), 70), dup(byn['Rock_Shore_C'], 'sC', (-2.2, -1.3, zw), 140),
             dup(byn['Rock_Shore_B'], 'sB2', (-6.0, -0.9, zw), 200), dup(byn['Rock_Shore_C'], 'sC2', (-5.2, -1.4, zw), 10), dup(byn['Rock_Shore_A'], 'sA2', (-7.6, -1.0, zw), 250)]
    reeds = [dup(byn['Reed_A'], 'rA', (3.0, -1.0, -0.65), 30), dup(byn['Reed_C'], 'rC', (4.1, -0.7, -0.6), 100), dup(byn['Reed_B'], 'rB', (5.1, -1.1, -0.65), 200),
             dup(byn['Reed_C'], 'rC2', (6.4, -0.8, -0.6), 10), dup(byn['Reed_A'], 'rA2', (7.5, -1.2, -0.65), 140), dup(byn['Reed_B'], 'rB2', (2.2, -1.5, -0.65), 300)]
    # weir far away at y = 60, x = 0: three stream rocks plus echoes across a 10 m stream, water 0.30 up, lower 0.05 below
    wy = 60.0
    wsoil = grid('RV_StreamBed', -12, 12, wy - 14, wy + 14, 24, 28, lambda x, y: -0.35 + 0.10 * max(0, abs(x) - 5.2) ** 1.3, soil)
    wsoil.location.z = 0.0
    up = grid('RV_StreamUp', -5.2, 5.2, wy, wy + 14, 2, 2, lambda x, y: 0.30, wm); dn = grid('RV_StreamDn', -5.2, 5.2, wy - 14, wy, 2, 2, lambda x, y: 0.02, wm)
    wsrc = ['Rock_Stream_A', 'Rock_Stream_B', 'Rock_Stream_C']
    xs = [-4.4, -3.0, -1.8, -0.7, 0.5, 1.7, 2.8, 4.0, 5.0]
    for k, x in enumerate(xs):
        dup(byn[wsrc[k % 3]], 'w%d' % k, (x + 0.2 * math.sin(k), wy + 0.25 * math.cos(k * 2), -0.02), k * 47, 1.0 if k % 2 == 0 else 0.92)
    # reed + rock closeup bed
    cam = FK.review_lights(sc)
    return cam, fig

VIEWS_DEF = {
    'jetty': (Vector((3.9, 3.8, 1.8)), Vector((0.0, -5.0, -0.5)), 50),
    'jettyfar': (Vector((-4.2, -15.0, 1.1)), Vector((0.3, -8.0, 0.1)), 50),
    'jettyside': (Vector((8.0, -6.0, 0.7)), Vector((0.0, -6.0, -0.8)), 46),
    'shore': (Vector((-2.0, -7.5, 1.0)), Vector((-4.2, -0.8, -0.2)), 48),
    'reeds': (Vector((4.8, -6.0, 0.9)), Vector((4.8, -1.0, 0.4)), 44),
    'moor': (Vector((-2.6, -11.2, 1.5)), Vector((0.2, -8.3, 0.2)), 46),
    'ladder': (Vector((4.2, -9.8, 0.9)), Vector((1.0, -7.7, -0.4)), 40),
    'weir': (Vector((1.0, 52.0, 1.5)), Vector((0.0, 60.0, 0.2)), 52),
    'weirhi': (Vector((9.0, 63.0, 3.0)), Vector((0.0, 60.0, 0.1)), 50),
}

def render_views(cam, fig):
    sc = bpy.context.scene
    for v in VIEWS:
        frm, to, fov = VIEWS_DEF[v]
        fig.hide_render = not v.startswith('jetty')
        FK.look(cam, frm, to); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, 'ls_%s.png' % v)
        bpy.ops.render.render(write_still=True); print('rendered', v)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if '--from-blend' in argv:     # review-only rerun: reuse a saved --blend (skips the bakes)
        bpy.ops.wm.open_mainfile(filepath=FK.arg('--from-blend'))
        objs = [o for o in bpy.data.objects if o.name.startswith(('Rock_', 'Reed_', 'Jetty'))]
    else:
        objs = build()
    report(objs)
    if '--export' in argv:
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'LakeShore.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'MESH'},
                                 mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='STRIP')
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'lakeshore.blend'))
    if '--no-render' not in argv:
        cam, fig = review_scene(objs)
        render_views(cam, fig)
