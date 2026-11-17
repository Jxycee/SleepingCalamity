"""Area 1 nature set (N01-N07): adopt CC0 Poly Haven rocks, a stump, a log and two meadow flowers as game-budget meshes.

Run (headless, from the repo root; fetches what is missing, then builds, exports and renders the contact sheet):
  blender -b -P Art/Blender/town/sc_nature_adopt.py -- [--out <dir>] [--only N01,N06] [--no-sheet] [--sheet <png>]

Outputs <out>/<code>_<name>/ (default Art/Staging/area1-world/nature/): the FBX plus textures, and <out>/SOURCES.md.
Raw downloads: Art/Source/PolyHaven/<id>/ (glTF 2K), fetched with Art/Blender/tools/sc_fetch_models.py and recorded in its SOURCES.md.

Pieces (decided 2026-11-16, Poly Haven, CC0):
  N01 cliff_slab    rock_face_02          stratified outcrop, 3.2 m, LOD0 3000 tris, LOD1 40%, LOD2 12%
  N02 angular_rock  boulder_01            angular boulder, 1.8 m, 3000 tris
  N03 scree_cluster namaqualand_stones_01 five flat stones as one cluster (~0.9 m), 1200 tris
  N04 stump         tree_stump_01         flared stump, 2500 tris, LOD1 40%
  N05 fallen_log    dead_tree_trunk_02    dead trunk with a fork, 3.4 m, 2500 tris, LOD1 40%
  N06 dandelion     dandelion_01          yellow ground-cover clump, 6 cards, 72 tris, 1K atlas
  N07 meadow_spike  shrub_01              white-flowered meadow plant clump, 8 cards, 96 tris, 1K atlas

Rocks, stump, log: the scan is joined, scaled to real metres, collapse-decimated (UVs ride along, so the source albedo and normal still fit), pivot at the base
centre (z = 0 in Blender, y = 0 in Unity). Textures follow the adopted T11a convention: <code>_albedo.jpg, _normal.jpg (OpenGL), _ms.png (R metallic 0, A smoothness =
1 - roughness), _occ.png (grey AO). FBX axes -Z forward, +Y up, path_mode STRIP, meshes <code>_LOD0/1/2.
Flowers: the scans carry no alpha (leaf shapes are geometry), so each plant is rendered from the side (orthographic, unlit albedo, film alpha = silhouette, colour bled
outward so there is no dark fringe) into a 1K atlas of 2x2 or 4x1 cells, with a matching camera-space normal atlas. The clump mesh is crossed cards (3x2 or 2x3 quads each,
the top leaning outward), pivot at the ground, vertex normals mostly up (blended a little with the card normal, like the grass clumps) and an AO colour attribute
'Col' (dark at the foot), UV0 over the atlas cell. Material M_<code>_<name>. Deterministic (fixed seeds).
"""
import bpy, bmesh, sys, os, math, shutil, random, datetime
import numpy as np
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
SRC = os.path.join(REPO, 'Art', 'Source', 'PolyHaven')
OUT = os.path.abspath(arg('--out', os.path.join(REPO, 'Art', 'Staging', 'area1-world', 'nature')))
ONLY = arg('--only'); ONLY = ONLY.split(',') if ONLY else None
SHEET = arg('--sheet', os.path.join(OUT, 'contact_sheet.png'))
TODAY = datetime.date.today().isoformat()

LOD_FRAC = (1.0, 0.40, 0.12)
SPEC = {
    'N01': dict(name='cliff_slab', kind='rock', src=['rock_face_02'], long=3.2, tris=3000, lods=3, what='stratified outcrop / cliff chunk'),
    'N02': dict(name='angular_rock', kind='rock', src=['boulder_01'], long=1.8, tris=3000, lods=3, what='angular boulder'),
    'N03': dict(name='scree_cluster', kind='cluster', src=['namaqualand_stones_01'], scale=2.7, tris=1200, lods=3, what='cluster of five flat stones (pebbles scaled 2.7x)'),
    'N04': dict(name='stump', kind='rock', src=['tree_stump_01'], long=1.5, tris=2500, lods=2, what='flared tree stump'),
    'N05': dict(name='fallen_log', kind='rock', src=['dead_tree_trunk_02'], long=3.4, tris=2500, lods=2, what='fallen dead trunk with a fork'),
    'N06': dict(name='dandelion', kind='flower', src=['dandelion_01'], what='yellow dandelion ground-cover clump', cells=(2, 2), k=2.2, ncards=6, grid=(3, 2), seed=11,
                picks=[('dandelion_01_a_LOD0', 0), ('dandelion_01_b_LOD0', 0), ('dandelion_01_a_LOD0', 100), ('dandelion_01_c_LOD0', 0)], hscale=(0.85, 1.0, 0.9, 0.75)),
    'N07': dict(name='meadow_spike', kind='flower', src=['shrub_01'], what='white-flowered meadow plant clump', cells=(4, 1), k=1.0, ncards=8, grid=(2, 3), seed=23,
                split='shrub_01_a', nsplit=4, hscale=(1.0, 0.85, 0.95, 0.75)),
}
CREDIT = {}   # filled from Art/Source/PolyHaven/SOURCES.md


# ----------------------------------------------------------------------------- fetch
def fetch_missing():
    sys.path.insert(0, os.path.join(REPO, 'Art', 'Blender', 'tools'))
    import sc_fetch_models as F
    for code, s in SPEC.items():
        if ONLY and code not in ONLY: continue
        for aid in s['src']:
            d = os.path.join(SRC, aid)
            if os.path.isdir(d) and os.listdir(d): continue
            r = F.polyhaven(aid, '2k', 400000, False)
            F.record('polyhaven', [aid, r['url'], F.LICENCE['polyhaven'], r['author'], TODAY, r['tris'], 'N-set', code + ' ' + s['what'], r['changed']])
    for line in open(os.path.join(SRC, 'SOURCES.md'), encoding='utf-8'):
        c = [x.strip() for x in line.split('|')]
        if len(c) > 5 and c[1]: CREDIT[c[1]] = (c[4], c[5], c[2])



# ----------------------------------------------------------------------------- images (bpy only: Blender's bundled PIL has no JPEG codec here)
def load_np(path):
    """raw (Non-Color) float32 array (h, w, 4), bottom row first as Blender stores it."""
    im = bpy.data.images.load(path, check_existing=False); im.colorspace_settings.name = 'Non-Color'
    w, h = im.size; a = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(a); bpy.data.images.remove(im); return a.reshape(h, w, 4)

def save_np(a, path, fmt='PNG', rgb=False):
    h, w = a.shape[:2]; im = bpy.data.images.new(os.path.basename(path), w, h, alpha=True); im.colorspace_settings.name = 'Non-Color'; im.alpha_mode = 'STRAIGHT'
    b = np.ascontiguousarray(a, np.float32).copy()
    if rgb: b[..., 3] = 1.0
    im.pixels.foreach_set(b.ravel()); im.filepath_raw = path; im.file_format = fmt
    sc = bpy.context.scene; sc.render.image_settings.color_mode = 'RGB' if rgb else 'RGBA'
    im.save(); bpy.data.images.remove(im)

def resize_np(a, nw, nh):
    """bilinear-area resize through Blender's own scaler, premultiplied so the alpha edge does not pick up a dark fringe."""
    pm = a.copy(); pm[..., :3] *= a[..., 3:4]
    im = bpy.data.images.new('rs', a.shape[1], a.shape[0], alpha=True); im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(np.ascontiguousarray(pm, np.float32).ravel()); im.scale(nw, nh)
    r = np.empty(nw * nh * 4, np.float32); im.pixels.foreach_get(r); bpy.data.images.remove(im); r = r.reshape(nh, nw, 4)
    al = r[..., 3:4]; r[..., :3] = np.where(al > 1e-3, r[..., :3] / np.maximum(al, 1e-3), 0); return r

# ----------------------------------------------------------------------------- blender helpers
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def import_src(aid):
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, aid, aid + '_2k.gltf'))
    objs = [o for o in bpy.data.objects if o.type == 'MESH']
    for o in objs:
        o.data.transform(o.matrix_world); o.parent = None; o.matrix_world = Matrix()
        bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6); bm.to_mesh(o.data); bm.free()   # glTF import splits verts at every seam; weld by position (UVs live on the loops) or collapse stalls
    for o in [o for o in bpy.data.objects if o.type != 'MESH']: bpy.data.objects.remove(o)
    return objs

def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join(); o = bpy.context.view_layer.objects.active; o.name = name; o.data.name = name
    return o

def np_verts(o):
    me = o.data; a = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', a); return a.reshape(-1, 3).astype(np.float64)

def bbox(o):
    v = np_verts(o); return v.min(0), v.max(0)

def ground(o):
    """pivot at the base centre: bbox centre in xy, lowest point at z = 0."""
    lo, hi = bbox(o); o.data.transform(Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])))

def scale_to_long(o, long):
    lo, hi = bbox(o); s = long / max(hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]); o.data.transform(Matrix.Scale(s, 4)); return s

def tri_count(o):
    o.data.calc_loop_triangles(); return len(o.data.loop_triangles)

def smooth(o):
    o.data.polygons.foreach_set('use_smooth', [True] * len(o.data.polygons)); o.data.update()

def decimate(o, tris, name):
    """collapse-decimated copy of o with at most `tris` triangles (UVs and the material come along)."""
    n0 = tri_count(o); ratio = min(1.0, tris / n0); me = None
    for it in range(8):
        mod = o.modifiers.new('dec', 'DECIMATE'); mod.decimate_type = 'COLLAPSE'; mod.ratio = ratio; mod.use_collapse_triangulate = True
        cand = bpy.data.meshes.new_from_object(o.evaluated_get(bpy.context.evaluated_depsgraph_get())); o.modifiers.remove(mod)
        n = len(cand.polygons)
        if n <= tris:                                   # keep the best candidate that fits the budget
            if me is not None: bpy.data.meshes.remove(me)
            me = cand
            if n >= tris * 0.92: break
        else: bpy.data.meshes.remove(cand)
        ratio *= (tris / max(n, 1)) * 0.985
    me.name = name; ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    smooth(ob); return ob

def pbr_outputs(aid, outd, code):
    """albedo.jpg and normal.jpg (OpenGL) copied as published, ms.png (R metallic 0, A smoothness) and occ.png (grey AO) in the T11a convention."""
    t = os.path.join(SRC, aid, 'textures'); os.makedirs(outd, exist_ok=True)
    shutil.copy(os.path.join(t, aid + '_diff_2k.jpg'), os.path.join(outd, code + '_albedo.jpg'))
    shutil.copy(os.path.join(t, aid + '_nor_gl_2k.jpg'), os.path.join(outd, code + '_normal.jpg'))
    arm = load_np(os.path.join(t, aid + '_arm_2k.jpg'))
    ms = np.zeros_like(arm); ms[..., 3] = 1 - arm[..., 1]; save_np(ms, os.path.join(outd, code + '_ms.png'))
    occ = np.ones_like(arm); occ[..., :3] = arm[..., :1]; save_np(occ, os.path.join(outd, code + '_occ.png'), rgb=True)
    return ['_albedo.jpg', '_normal.jpg', '_ms.png', '_occ.png']

def link_pbr_material(mat, outd, code):
    """simple Principled material on the staged textures (only for the FBX material name and the contact sheet; Unity builds its own)."""
    mat.use_nodes = True; nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); nt.links.new(bs.outputs[0], out.inputs[0])
    def img(suffix, cs):
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(os.path.join(outd, code + suffix), check_existing=True)
        n.image.colorspace_settings.name = cs; return n
    a = img('_albedo.jpg', 'sRGB'); nt.links.new(a.outputs[0], bs.inputs['Base Color'])
    n = img('_normal.jpg', 'Non-Color'); nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(n.outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value = 0.85

def export_fbx(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', global_scale=1.0, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='OFF', add_leaf_bones=False, bake_anim=False,
                             use_custom_props=False, path_mode='STRIP')


# ----------------------------------------------------------------------------- rocks, stump, log, cluster
def build_rock(code, s):
    reset(); aid = s['src'][0]; objs = import_src(aid)
    if s['kind'] == 'cluster':
        return build_cluster(code, s, objs)
    hi = join(objs, code + '_src') if len(objs) > 1 else objs[0]
    sc = scale_to_long(hi, s['long']); ground(hi)
    return finish_lods(code, s, hi, aid, sc)

def build_cluster(code, s, objs):
    aid = s['src'][0]; r = random.Random(5)
    for o in objs:
        o.data.transform(Matrix.Scale(s['scale'], 4)); ground(o)
    lay = [(0.0, 0.0), (0.37, 0.13), (-0.32, 0.16), (0.05, -0.30), (-0.08, 0.34)]
    objs.sort(key=lambda o: -len(o.data.polygons))
    tot = sum(len(o.data.polygons) for o in objs)
    parts = []
    for o, (x, y) in zip(objs, lay):
        o.data.transform(Matrix.Translation((x, y, -0.015)) @ Matrix.Rotation(r.uniform(0, 6.28), 4, 'Z'))
        parts.append(decimate(o, max(60, int(s['tris'] * len(o.data.polygons) / tot * 0.97)), o.name + '_d'))
    for o in objs: bpy.data.objects.remove(o)
    lo0 = join(parts, code + '_LOD0'); lo0.data.name = code + '_LOD0'; ground(lo0)
    return finish_lods(code, s, lo0, aid, s['scale'], already_low=True)

def finish_lods(code, s, hi, aid, sc, already_low=False):
    outd = os.path.join(OUT, '%s_%s' % (code, s['name']))
    if os.path.isdir(outd): shutil.rmtree(outd)
    suff = pbr_outputs(aid, outd, code)
    lod0 = hi if already_low else decimate(hi, s['tris'], code + '_LOD0')
    lod0.name = code + '_LOD0'; ground(lod0)
    lods = [lod0]
    for i in range(1, s['lods']):
        lods.append(decimate(lod0, int(s['tris'] * LOD_FRAC[i]), '%s_LOD%d' % (code, i)))
    if not already_low: bpy.data.objects.remove(hi)
    mat = bpy.data.materials.new('M_%s_%s' % (code, s['name'])); link_pbr_material(mat, outd, code)
    for o in lods:
        o.data.materials.clear(); o.data.materials.append(mat)
        if not o.data.uv_layers: raise RuntimeError(code + ': no UVs')
        o.data.uv_layers[0].name = 'UVMap'
    export_fbx(lods, os.path.join(outd, code + '.fbx'))
    lo, hi_ = bbox(lod0)
    return dict(code=code, dir=outd, tris=[tri_count(o) for o in lods], size=[round(float(x), 2) for x in (hi_ - lo)], files=[code + '.fbx'] + [code + x for x in suff], scale=sc)


# ----------------------------------------------------------------------------- flowers: side renders -> atlas, cards -> clump
def setup_scene(w, h):
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 24; sc.cycles.use_denoising = False
    sc.render.film_transparent = True; sc.render.resolution_x = w; sc.render.resolution_y = h; sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'; sc.render.image_settings.color_depth = '8'
    sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
    sc.world = bpy.data.worlds.new('w'); return sc

def flat_albedo_materials():
    for m in bpy.data.materials:
        if not m.use_nodes: continue
        nt = m.node_tree
        bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None); out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None)
        if not bsdf or not out: continue
        em = nt.nodes.new('ShaderNodeEmission'); src = bsdf.inputs['Base Color']
        if src.links: nt.links.new(src.links[0].from_socket, em.inputs['Color'])
        else: em.inputs['Color'].default_value = src.default_value
        nt.links.new(em.outputs[0], out.inputs['Surface'])

def normal_material():
    m = bpy.data.materials.new('camnormal'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    geo = nt.nodes.new('ShaderNodeNewGeometry'); vt = nt.nodes.new('ShaderNodeVectorTransform')
    vt.vector_type = 'NORMAL'; vt.convert_from = 'WORLD'; vt.convert_to = 'CAMERA'
    ma = nt.nodes.new('ShaderNodeVectorMath'); ma.operation = 'MULTIPLY_ADD'; ma.inputs[1].default_value = (0.5, 0.5, 0.5); ma.inputs[2].default_value = (0.5, 0.5, 0.5)
    em = nt.nodes.new('ShaderNodeEmission'); out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(geo.outputs['Normal'], vt.inputs[0]); nt.links.new(vt.outputs[0], ma.inputs[0]); nt.links.new(ma.outputs[0], em.inputs['Color']); nt.links.new(em.outputs[0], out.inputs[0])
    return m

def render_png(path):
    bpy.context.scene.render.filepath = path; bpy.ops.render.render(write_still=True)
    return load_np(path)

def render_view(obj, yaw, ppm, nmat, tmp):
    """front orthographic render (camera on -Y, looking +Y) of obj turned by yaw degrees about its bbox centre: (albedo RGBA, normal RGBA, metres per pixel)."""
    for o in bpy.data.objects:
        if o.type == 'MESH': o.hide_render = (o is not obj)
    v = np_verts(obj); c = (v.min(0) + v.max(0)) / 2
    obj.matrix_world = Matrix.Translation(c) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Translation(-c)
    v = np.array([(obj.matrix_world @ Vector(p))[:] for p in v[::max(1, len(v) // 20000)]])
    x0, x1, z0, z1 = v[:, 0].min(), v[:, 0].max(), v[:, 2].min(), v[:, 2].max()
    pad = 6; W = int(math.ceil((x1 - x0) * ppm)) + 2 * pad; H = int(math.ceil((z1 - z0) * ppm)) + 2 * pad
    sc = setup_scene(W, H) if not bpy.context.scene.world else bpy.context.scene
    sc.render.resolution_x = W; sc.render.resolution_y = H
    cam = bpy.data.objects.get('cam')
    if cam is None:
        cd = bpy.data.cameras.new('cam'); cd.type = 'ORTHO'; cam = bpy.data.objects.new('cam', cd); bpy.context.collection.objects.link(cam); sc.camera = cam
    cam.data.ortho_scale = max(W, H) / ppm; cam.data.clip_end = 100
    cam.location = ((x0 + x1) / 2, c[1] - 20, (z0 + z1) / 2); cam.rotation_euler = (math.pi / 2, 0, 0)
    sc.view_settings.view_transform = 'Standard'; bpy.context.view_layer.material_override = None
    alb = render_png(os.path.join(tmp, 'a.png'))
    sc.view_settings.view_transform = 'Raw'; bpy.context.view_layer.material_override = nmat
    nor = render_png(os.path.join(tmp, 'n.png'))
    bpy.context.view_layer.material_override = None; sc.view_settings.view_transform = 'Standard'
    return alb, nor, 1.0 / ppm

def bleed_rgb(rgb, valid, iters=24):
    rgb = rgb.copy(); valid = valid.copy()
    for _ in range(iters):
        if valid.all(): break
        v = valid.astype(np.float32); acc = np.zeros_like(rgb); cnt = np.zeros(valid.shape, np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dx == 0 and dy == 0: continue
                cnt += np.roll(v, (dy, dx), (0, 1)); acc += np.roll(rgb * v[..., None], (dy, dx), (0, 1))
        new = (~valid) & (cnt > 0); rgb[new] = acc[new] / cnt[new][:, None]; valid = valid | new
    return rgb

def crop_sprite(alb, nor, mpp):
    """tight crop on alpha; returns dict(alb, nor, w_m, h_m) with straight RGBA arrays (0-1)."""
    a = alb[..., 3]; ys, xs = np.where(a > 0.05)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    return dict(alb=alb[y0:y1, x0:x1], nor=nor[y0:y1, x0:x1], w_m=(x1 - x0) * mpp, h_m=(y1 - y0) * mpp)

def split_columns(alb, nor, mpp, n):
    """one render of a row of plants: split on empty alpha columns, keep the n tallest."""
    occ = (alb[..., 3] > 0.05).sum(0) > 0; runs = []; i = 0
    while i < len(occ):
        if occ[i]:
            j = i
            while j < len(occ) and occ[j]: j += 1
            if j - i > 12: runs.append((i, j))
            i = j
        else: i += 1
    out = []
    for i, j in runs:
        sp = crop_sprite(alb[:, i:j], nor[:, i:j], mpp); out.append(sp)
    out.sort(key=lambda sp: -sp['h_m'] * min(1.0, sp['w_m'] / 0.08)); return out[:n]

def pack_atlas(sprites, cells, k, hscale):
    """sprites -> 1K albedo(+alpha) and normal atlas (cells = (cols, rows)); returns (alb, nor, entries[uv rect px, w_m, h_m])."""
    cw, ch = 1024 // cells[0], 1024 // cells[1]
    A = np.zeros((1024, 1024, 4), np.float32); N = np.zeros((1024, 1024, 4), np.float32); N[..., :3] = (0.5, 0.5, 1.0); entries = []
    for i, sp in enumerate(sprites):
        h, w = sp['alb'].shape[:2]; f = min((cw - 8) / w, (ch - 8) / h)
        nw, nh = max(1, int(round(w * f))), max(1, int(round(h * f)))
        a, n = resize_np(sp['alb'], nw, nh), resize_np(sp['nor'], nw, nh)
        cx, cy = (i % cells[0]) * cw, (i // cells[0]) * ch                    # arrays are bottom-row-first, like the UVs
        ox = cx + (cw - nw) // 2; oy = cy + 4                                  # the foot sits on the cell floor
        A[oy:oy + nh, ox:ox + nw] = a; N[oy:oy + nh, ox:ox + nw] = n
        hm = sp['h_m'] * k * hscale[i % len(hscale)]; wm = hm * (w / h)
        entries.append(dict(px=(ox, oy, ox + nw, oy + nh), w=wm, h=hm))
    valid = A[..., 3] > 0.5
    A[..., :3] = bleed_rgb(A[..., :3], valid, 32)
    nv = valid.copy(); N[..., :3] = bleed_rgb(N[..., :3], nv, 32)
    N[..., 3] = 1.0
    return A, N, entries

def build_clump(code, s, entries, mat):
    r = random.Random(s['seed']); cols, rows = s['grid']; n = s['ncards']
    verts, uvs, nrms, aos, faces = [], [], [], [], []
    for k in range(n):
        e = entries[k % len(entries)]; half = n // 2          # two sets of crossed cards, the second turned half a step
        yaw = math.pi * (k % half) / half + (math.pi / (2 * half)) * (k // half) + r.uniform(-0.12, 0.12)
        off = r.uniform(0.0, 0.07); oang = r.uniform(0, 2 * math.pi); lean = math.radians(r.uniform(6, 16)); hs = r.uniform(0.88, 1.0)
        ex = Vector((math.cos(yaw), math.sin(yaw), 0)); nz = Vector((-math.sin(yaw), math.cos(yaw), 0)); base = Vector((off * math.cos(oang), off * math.sin(oang), 0))
        outd = Vector((math.cos(yaw + math.pi / 2), math.sin(yaw + math.pi / 2), 0)) * (1 if r.random() < 0.5 else -1)
        x0, y0, x1, y1 = e['px']; i0 = len(verts)
        for j in range(rows + 1):
            t = j / rows
            for i in range(cols + 1):
                u = i / cols - 0.5
                p = base + ex * (u * e['w'] * hs) + Vector((0, 0, t * e['h'] * hs)) + outd * (math.sin(lean) * e['h'] * hs * t ** 1.6)
                p.z = t * e['h'] * hs * math.cos(lean * t)
                verts.append(p); uvs.append(((x0 + (x1 - x0) * (i / cols)) / 1024.0, (y0 + (y1 - y0) * t) / 1024.0))
                nn = (Vector((0, 0, 1)) * 0.85 + nz * 0.15 + outd * 0.2).normalized(); nrms.append(nn); aos.append(0.55 + 0.45 * t)
        for j in range(rows):
            for i in range(cols):
                a = i0 + j * (cols + 1) + i; faces.append((a, a + 1, a + cols + 2, a + cols + 1))
    me = bpy.data.meshes.new(code + '_LOD0'); me.from_pydata([v[:] for v in verts], [], faces); me.update()
    uv = me.uv_layers.new(name='UVMap'); ca = me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    for p in me.polygons:
        for li in p.loop_indices:
            vi = me.loops[li].vertex_index; uv.data[li].uv = uvs[vi]; ca.data[li].color = (aos[vi],) * 3 + (1.0,)
    for p in me.polygons: p.use_smooth = True
    try: me.normals_split_custom_set_from_vertices([n[:] for n in nrms])
    except Exception as ex_: print('custom normals failed', ex_)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    ob = bpy.data.objects.new(code + '_LOD0', me); bpy.context.collection.objects.link(ob); me.materials.append(mat); return ob, tris

def flower_material(name, outd, code):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); nt.links.new(bs.outputs[0], out.inputs[0])
    a = nt.nodes.new('ShaderNodeTexImage'); a.image = bpy.data.images.load(os.path.join(outd, code + '_albedo.png'), check_existing=True)
    nt.links.new(a.outputs[0], bs.inputs['Base Color']); nt.links.new(a.outputs[1], bs.inputs['Alpha'])
    n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(os.path.join(outd, code + '_normal.png'), check_existing=True); n.image.colorspace_settings.name = 'Non-Color'
    nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(n.outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value = 0.8
    try: m.surface_render_method = 'DITHERED'
    except Exception: pass
    return m

def build_flower(code, s):
    reset(); aid = s['src'][0]; objs = {o.name: o for o in import_src(aid)}
    outd = os.path.join(OUT, '%s_%s' % (code, s['name']))
    if os.path.isdir(outd): shutil.rmtree(outd)
    os.makedirs(outd); tmp = os.path.join(OUT, '_tmp'); os.makedirs(tmp, exist_ok=True)
    flat_albedo_materials(); nmat = normal_material(); setup_scene(64, 64)
    sprites = []
    if 'split' in s:
        o = objs[s['split']]; lo, hi = bbox(o); ppm = 1400.0 if (hi[0] - lo[0]) < 4 else 1000.0
        alb, nor, mpp = render_view(o, 0, ppm, nmat, tmp); sprites = split_columns(alb, nor, mpp, s['nsplit'])
    else:
        hmax = max(bbox(objs[n])[1][2] - bbox(objs[n])[0][2] for n, _ in s['picks']); ppm = 900.0 / hmax
        for n, yaw in s['picks']:
            alb, nor, mpp = render_view(objs[n], yaw, ppm, nmat, tmp); sprites.append(crop_sprite(alb, nor, mpp))
    A, N, entries = pack_atlas(sprites, s['cells'], s['k'], s['hscale'])
    save_np(np.clip(A, 0, 1), os.path.join(outd, code + '_albedo.png')); save_np(np.clip(N, 0, 1), os.path.join(outd, code + '_normal.png'), rgb=True)
    shutil.rmtree(tmp, ignore_errors=True)
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    mat = flower_material('M_%s_%s' % (code, s['name']), outd, code)
    ob, tris = build_clump(code, s, entries, mat)
    export_fbx([ob], os.path.join(outd, code + '.fbx'))
    lo, hi = bbox(ob)
    return dict(code=code, dir=outd, tris=[tris], size=[round(float(x), 2) for x in (hi - lo)], files=[code + '.fbx', code + '_albedo.png', code + '_normal.png'], scale=s['k'])


# ----------------------------------------------------------------------------- contact sheet
def sheet(results):
    """re-import the exported FBX files with their staged textures: LOD0 | LOD1 | LOD2 per panel, a 1.75 m post at the left; one 1000 px wide PNG."""
    panels = []
    for res in results:
        code = res['code']; s = SPEC[code]; reset()
        bpy.ops.import_scene.fbx(filepath=os.path.join(res['dir'], code + '.fbx'))
        meshes = sorted([o for o in bpy.data.objects if o.type == 'MESH'], key=lambda o: o.name)
        flower = s['kind'] == 'flower'
        mat = flower_material('sheet', res['dir'], code) if flower else None
        if not flower:
            mat = bpy.data.materials.new('sheet'); link_pbr_material(mat, res['dir'], code)
        for o in meshes:
            o.data.materials.clear(); o.data.materials.append(mat)
        extent = max(res['size'][0], res['size'][1], 0.4)
        step = extent * (1.25 if not flower else 1.3); start = max(1.0, extent / 2 + 0.7) if not flower else 0.9
        if flower:                                              # the clump three times (turned) beside each other, then LOD-less
            base = meshes[0]; meshes = [base]
            for i in (1, 2):
                c = base.copy(); bpy.context.collection.objects.link(c); c.rotation_euler.z = i * 0.9; meshes.append(c)
        for i, o in enumerate(meshes): o.location = (i * step + start, 0, 0)
        # post, ground
        bpy.ops.mesh.primitive_cylinder_add(radius=0.04, depth=1.75, location=(0, 0, 0.875)); post = bpy.context.object
        pm = bpy.data.materials.new('post'); pm.diffuse_color = (0.9, 0.2, 0.1, 1); post.data.materials.append(pm)
        W = start + step * (len(meshes) - 0.5) + extent / 2 + 0.3
        bpy.ops.mesh.primitive_plane_add(size=60, location=(W / 2, 0, -0.001)); g = bpy.context.object
        gm = bpy.data.materials.new('ground'); gm.use_nodes = True; gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.30, 0.33, 0.20, 1); g.data.materials.append(gm)
        pxw = 1000; ph = max(2.0, res['size'][2] * 1.1 + 0.5 * extent * 0.35 + 0.2); H = int(pxw * ph / W)
        sc = bpy.context.scene
        try: sc.render.engine = 'BLENDER_EEVEE'
        except Exception: sc.render.engine = 'CYCLES'
        sc.render.resolution_x = pxw; sc.render.resolution_y = H; sc.render.film_transparent = False
        try: sc.eevee.taa_render_samples = 20
        except Exception: pass
        sc.world = bpy.data.worlds.new('w'); sc.world.use_nodes = True
        bg = sc.world.node_tree.nodes['Background']; bg.inputs[0].default_value = (0.55, 0.62, 0.72, 1); bg.inputs[1].default_value = 0.8
        sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(-35)); bpy.context.collection.objects.link(sun)
        cd = bpy.data.cameras.new('cam'); cd.type = 'ORTHO'; cd.ortho_scale = W; cam = bpy.data.objects.new('cam', cd); bpy.context.collection.objects.link(cam); sc.camera = cam
        d = Vector((0.25, -1.0, 0.32)).normalized(); mid = Vector((W / 2, 0, ph / 2 - 0.05))
        cam.location = mid + d * 40; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.view_settings.view_transform = 'Standard'
        p = os.path.join(OUT, '_panel_%s.png' % code); sc.render.filepath = p; sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'
        bpy.ops.render.render(write_still=True); panels.append(p)
    ims = [load_np(p) for p in panels]; out = np.concatenate(ims[::-1], 0)         # bottom-row-first: stack the last panel first
    save_np(out, SHEET, rgb=True)
    for p in panels: os.remove(p)
    return SHEET


# ----------------------------------------------------------------------------- SOURCES.md
def write_sources(results):
    lines = ['# Area 1 nature set (N01-N07): sources and licence', '',
             'All Poly Haven models, licence CC0 (https://polyhaven.com/license), no credit required (credited anyway). Fetched %s as 2K glTF into `Art/Source/PolyHaven/<id>/` with '
             '`Art/Blender/tools/sc_fetch_models.py` (record in `Art/Source/PolyHaven/SOURCES.md`) and adopted with `Art/Blender/town/sc_nature_adopt.py`.' % TODAY, '',
             'Regenerate: `"C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b -P Art/Blender/town/sc_nature_adopt.py` (fetches what is missing).', '',
             'Texture convention of the rocks, stump and log follows the adopted T11a: `<code>_albedo.jpg`, `<code>_normal.jpg` (OpenGL), `<code>_ms.png` (R metallic 0, A smoothness = 1 - roughness), '
             '`<code>_occ.png` (grey AO). Flowers carry `<code>_albedo.png` (RGBA, alpha = silhouette) and `<code>_normal.png`. FBX axes -Z forward, +Y up, meshes `<code>_LOD0/1/2`.', '',
             '| code | Poly Haven id | URL | licence | author | date | what | size (m) | tris per LOD | files |', '|---|---|---|---|---|---|---|---|---|---|']
    for r in results:
        s = SPEC[r['code']]; ids = s['src'][0]; au = CREDIT.get(ids, ('?', '?', '?'))[1]
        lines.append('| %s | %s | https://polyhaven.com/a/%s | CC0 | %s | %s | %s | %s | %s | `%s_%s/`: %s |' % (
            r['code'], ids, ids, au, TODAY, s['what'], ' x '.join(str(x) for x in r['size']), ' / '.join(str(t) for t in r['tris']), r['code'], s['name'], ', '.join(r['files'])))
    lines += ['', 'Changes: scans scaled to real metres, collapse-decimated (UVs kept, source albedo/normal reused), pivot at the base centre; N03 is five of the source stones arranged as one cluster '
              'at 2.7x (they are 4-15 cm pebbles); N06/N07 are rebuilt as card clumps from side renders of the scan (the scan has no alpha).']
    open(os.path.join(OUT, 'SOURCES.md'), 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')


def main():
    fetch_missing(); os.makedirs(OUT, exist_ok=True); results = []
    for code, s in SPEC.items():
        if ONLY and code not in ONLY: continue
        r = build_flower(code, s) if s['kind'] == 'flower' else build_rock(code, s)
        print('BUILT', code, r['tris'], r['size']); results.append(r)
    write_sources(results) if not ONLY else None
    if '--no-sheet' not in argv: print('SHEET', sheet(results))

main()
