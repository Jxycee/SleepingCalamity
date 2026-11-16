"""Hollowmere trees (batch TR01-TR07): adopt CC0 Poly Haven tree/shrub scans as game-budget LOD meshes.

Run (headless, from the repo root):
  blender -b -P Art/Blender/town/sc_trees_adopt.py -- --dl <download dir> --out <output dir> [--only TR02,TR05] [--checks <render dir>]

<download dir>/<polyhaven id>/<id>_2k.gltf (+ textures/) is what Poly Haven's glTF download unpacks to (see SOURCES.md in Content/Art/Town/Trees).
Output: <out>/<code>/<code>.fbx (meshes <code>_LOD0/1/2, material slots: trees bark(0) + leaves(1), shrubs leaves(0)) and the textures
  <code>_bark_albedo.jpg/_normal.jpg/_ms.png/_occ.png, <code>_leaf_albedo.png (alpha)/_normal.jpg/_ms.png   (ms = R metallic 0, A smoothness = 1 - roughness).

Method (the sources are 1.7-4.7 M tris; leaves are separate leaf-shaped mesh islands with no alpha, the shape is geometry):
  * every leaf/twig island becomes one card (2 tris, or a small bent grid on shrubs) fitted to the island's plane, UV-mapped over the island's atlas region;
    the alpha comes from rasterising the source leaf UV triangles into a mask, colour bled outward so no black fringe;
  * LODs keep a seeded random subset (nested: LOD2 inside LOD1 inside LOD0) and scale the kept cards up about their own centre so the crown stays full;
  * crown normals come from a crown-fitting ellipsoid (blended a little with the card normal) so the crown shades as one mass;
  * wood: trunk + the longest branch islands, collapse-decimated; tree branches get a box UV into the trunk bark texture.
Deterministic (fixed seeds). Pivot at the trunk base on the ground (z = 0 in Blender, y = 0 in Unity), height normalised per code.
"""
import bpy, bmesh, sys, os, json, math, shutil
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default
DL = arg('--dl'); OUT = arg('--out'); CHECKS = arg('--checks'); ONLY = arg('--only')
ONLY = ONLY.split(',') if ONLY else None

# cov = how much of the original crown area the kept cards re-cover (tuned on the preview renders)
TREE_BUDGET = dict(wood_t=(4500, 1400, 420), leaf_tris=(15500, 5200, 1500), keep_branches=(700, 220, 50))
SPEC = {
    'TR01': dict(src='tree_small_02', height=6.0, kind='tree', res=2048, trunk='tree_small_02_trunk', bark_tex='tree_small_02_trunk',
                 wood_parts=[('tree_small_02_trunk', False, True), ('tree_small_02_branches', True, False)], leaf='tree_small_02_leaves', cov=(1.45, 1.25, 1.1), **TREE_BUDGET),
    'TR02': dict(src='island_tree_01', height=13.0, kind='tree', res=2048, bark_tex='island_tree_01',
                 wood_parts=[('island_tree_01', False, True), ('island_tree_01_branches', True, False)], leaf='island_tree_01_leaves', cov=(1.45, 1.25, 1.1), **TREE_BUDGET),
    'TR03': dict(src='island_tree_02', height=9.0, kind='tree', res=2048, bark_tex='island_tree_02',
                 wood_parts=[('island_tree_02', False, True), ('island_tree_02_branches', True, False)], leaf='island_tree_02_leaves', cov=(1.45, 1.25, 1.1), **TREE_BUDGET),
    'TR04': dict(src='fir_sapling_medium', height=11.0, kind='tree', res=2048, bark_tex='fir_sapling_medium_branches',
                 wood_parts=[('fir_sapling_medium_branches', False, False), ('fir_sapling_medium_branches_dead', False, False)], leaf='fir_sapling_medium_twigs', cov=(2.0, 1.7, 1.4),
                 wood_t=(6000, 3000, 650), leaf_tris=(17000, 5600, 1600), keep_branches=(110, 70, 14), tubes=(6, 4, 3)),
    'TR05': dict(src='shrub_04', height=2.2, kind='shrub', res=1024, mixed='shrub_04', cov=(0.8, 0.9, 1.0), wood_t=(1200, 400, 150), tubes=(5, 4, 3), leafscale=1.5, leaf_tris=(2400, 600, 160), segs=((3, 2), (2, 1), (1, 1)), cluster=(4, 0)),
    'TR06': dict(src='shrub_04', height=0.9, kind='shrub', res=1024, mixed='shrub_04', cov=(0.8, 0.9, 1.0), wood_t=(1200, 400, 150), tubes=(5, 4, 3), leafscale=1.5, leaf_tris=(2400, 600, 160), segs=((3, 2), (2, 1), (1, 1)), cluster=(4, 1)),
    'TR07': dict(src='shrub_04', height=0.7, kind='shrub', res=1024, mixed='shrub_04', cov=(0.8, 0.9, 1.0), wood_t=(1200, 400, 150), tubes=(5, 4, 3), leafscale=1.5, leaf_tris=(2400, 600, 160), segs=((3, 2), (2, 1), (1, 1)), cluster=(4, 2)),
}
PLANAR_MAX = 0.78   # shrubs: sqrt(lambda_min / lambda_mid) below this = leaf card, above = stem tube (about 1.0)
LOD_NAMES = ('LOD0', 'LOD1', 'LOD2')


# ----------------------------------------------------------------------------- source access
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def mat_textures(gltf_path):
    """material name -> base colour image file name (parsed from the glTF json)."""
    g = json.load(open(gltf_path, 'r', encoding='utf-8'))
    out = {}
    for m in g['materials']:
        t = m['pbrMetallicRoughness']['baseColorTexture']['index']
        out[m['name']] = os.path.basename(g['images'][g['textures'][t]['source']]['uri'])
    return out

def extract(o):
    me = o.data; me.calc_loop_triangles()
    mw = np.array(o.matrix_world, np.float64)
    nv = len(me.vertices); co = np.empty(nv * 3, np.float32); me.vertices.foreach_get('co', co)
    co = (co.reshape(-1, 3).astype(np.float64) @ mw[:3, :3].T + mw[:3, 3])
    nt = len(me.loop_triangles)
    tv = np.empty(nt * 3, np.int32); me.loop_triangles.foreach_get('vertices', tv)
    tl = np.empty(nt * 3, np.int32); me.loop_triangles.foreach_get('loops', tl)
    pm = np.empty(nt, np.int32); me.loop_triangles.foreach_get('material_index', pm)
    uv = np.empty(len(me.loops) * 2, np.float32); me.uv_layers.active.uv.foreach_get('vector', uv)
    return co, tv.reshape(-1, 3), uv.reshape(-1, 2)[tl].reshape(nt, 3, 2), pm, [s.material.name for s in o.material_slots]

def island_labels(nv, tv):
    lab = np.arange(nv, dtype=np.int64)
    while True:
        m = lab[tv].min(1)
        new = lab.copy()
        for k in range(3): np.minimum.at(new, tv[:, k], m)
        new = new[new]
        if np.array_equal(new, lab): break
        lab = new
    return lab


# ----------------------------------------------------------------------------- islands -> cards
class Islands:
    """per-island fit: centre, axes, extents, planarity, uv affine; island = connected tris."""
    def __init__(self, co, tv, tuv):
        lab = island_labels(len(co), tv)
        tl = lab[tv[:, 0]]
        order = np.argsort(tl, kind='stable'); self.order = order
        sl = tl[order]
        ids, start = np.unique(sl, return_index=True)
        self.n = len(ids); self.start = start; self.end = np.append(start[1:], len(sl))
        self.co, self.tv, self.tuv = co, tv, tuv
        self.tri_count = self.end - self.start
        self.island_of_tri = np.empty(len(tv), np.int64); self.island_of_tri[order] = np.repeat(np.arange(self.n), self.tri_count)

    def tris(self, i): return self.order[self.start[i]:self.end[i]]

    def fit(self, i):
        t = self.tris(i); P = self.co[self.tv[t].reshape(-1)]
        uv = self.tuv[t].reshape(-1, 2)
        c = P.mean(0); Q = P - c
        w, V = np.linalg.eigh(Q.T @ Q / len(Q))
        e0, e1, e2 = V[:, 2], V[:, 1], V[:, 0]
        planar = math.sqrt(max(w[0], 0) / max(w[1], 1e-12))
        s = Q @ e0; tt = Q @ e1
        A = np.stack([s, tt, np.ones_like(s)], 1)
        M, *_ = np.linalg.lstsq(A, uv, rcond=None)
        return dict(c=c, e0=e0, e1=e1, e2=e2, s0=s.min(), s1=s.max(), t0=tt.min(), t1=tt.max(), planar=planar, M=M,
                    uvmin=uv.min(0), uvmax=uv.max(0), ext=float(np.linalg.norm(P.max(0) - P.min(0))))


def rasterise(uvtris, W, H):
    mask = np.zeros((H, W), bool)
    for tri in uvtris:
        p = tri * np.array([W, H])
        x0 = max(int(math.floor(p[:, 0].min())) - 1, 0); x1 = min(int(math.ceil(p[:, 0].max())) + 1, W - 1)
        y0 = max(int(math.floor(p[:, 1].min())) - 1, 0); y1 = min(int(math.ceil(p[:, 1].max())) + 1, H - 1)
        if x1 < x0 or y1 < y0: continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (p[1, 1] - p[2, 1]) * (p[0, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (p[0, 1] - p[2, 1])
        if abs(d) < 1e-9: continue
        a = ((p[1, 1] - p[2, 1]) * (gx - p[2, 0]) + (p[2, 0] - p[1, 0]) * (gy - p[2, 1])) / d
        b = ((p[2, 1] - p[0, 1]) * (gx - p[2, 0]) + (p[0, 0] - p[2, 0]) * (gy - p[2, 1])) / d
        c = 1 - a - b
        eps = -0.02
        mask[y0:y1 + 1, x0:x1 + 1] |= (a >= eps) & (b >= eps) & (c >= eps)
    return mask

def shift(a, dy, dx): return np.roll(np.roll(a, dy, 0), dx, 1)

def erode(m, n):
    for _ in range(n):
        r = m.copy()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1): r &= shift(m, dy, dx)
        m = r
    return m

def bleed(rgb, valid, iters):
    rgb = rgb.copy(); valid = valid.copy()
    for _ in range(iters):
        if valid.all(): break
        v = valid.astype(np.float32); acc = np.zeros_like(rgb); cnt = np.zeros(valid.shape, np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dx == 0 and dy == 0: continue
                cnt += shift(v, dy, dx); acc += shift(rgb * v[..., None], dy, dx)
        new = (~valid) & (cnt > 0)
        rgb[new] = acc[new] / cnt[new][:, None]; valid = valid | new
    return rgb


# ----------------------------------------------------------------------------- images
def load_raw(path):
    im = bpy.data.images.load(path, check_existing=False); im.colorspace_settings.name = 'Non-Color'
    w, h = im.size
    a = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)

def save_img(arr, path, fmt):
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=True); im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(np.ascontiguousarray(arr, np.float32).ravel())
    im.filepath_raw = path; im.file_format = fmt; im.save(); bpy.data.images.remove(im)

def resize(arr, res):
    while arr.shape[0] > res:
        arr = (arr[0::2, 0::2] + arr[1::2, 0::2] + arr[0::2, 1::2] + arr[1::2, 1::2]) * 0.25
    return arr

def arm_maps(armfile, res):
    a = resize(load_raw(armfile), res)
    ms = np.zeros_like(a); ms[..., 3] = 1 - a[..., 1]; ms[..., 1] = 0
    ms[..., 0] = 0; ms[..., 1] = 0; ms[..., 2] = 0
    occ = np.ones_like(a); occ[..., 0] = occ[..., 1] = occ[..., 2] = a[..., 0]
    return ms, occ

def tex_paths(texdir, diff):
    base = diff.replace('_diff_2k.jpg', '')
    return os.path.join(texdir, diff), os.path.join(texdir, base + '_arm_2k.jpg'), os.path.join(texdir, base + '_nor_gl_2k.jpg')


# ----------------------------------------------------------------------------- mesh building
def grid_card(f, L, W, scale, ctr, rad):
    """vertices (L+1)*(W+1) positions, uvs and tri index list for island fit f scaled about its own centre."""
    sc = (f['s0'] + f['s1']) / 2; tc = (f['t0'] + f['t1']) / 2
    hs = (f['s1'] - f['s0']) / 2; ht = (f['t1'] - f['t0']) / 2
    ii = np.linspace(-1, 1, L + 1); jj = np.linspace(-1, 1, W + 1)
    S, T = np.meshgrid(ii, jj, indexing='ij')
    s_un = sc + hs * S; t_un = tc + ht * T
    out = (f['c'] - ctr) / rad; out = out / max(np.linalg.norm(out), 1e-6)
    e2 = f['e2'] if f['e2'] @ out >= 0 else -f['e2']
    flip = (np.cross(f['e0'], f['e1']) @ e2) < 0      # winding so the geometric front faces out of the crown
    bend = (-0.22 * S ** 2 - 0.10 * T ** 2) * hs * scale if (L > 1 or W > 1) else np.zeros_like(S)
    cc = f['c'] + f['e0'] * sc + f['e1'] * tc
    pos = cc[None, None, :] + f['e0'][None, None, :] * (hs * scale * S)[..., None] + f['e1'][None, None, :] * (ht * scale * T)[..., None] + e2[None, None, :] * bend[..., None]
    uv = np.stack([s_un, t_un, np.ones_like(s_un)], -1) @ f['M']
    return pos.reshape(-1, 3), uv.reshape(-1, 2), flip

def grid_tris(L, W):
    t = []
    for i in range(L):
        for j in range(W):
            a = i * (W + 1) + j; b = a + 1; c = a + (W + 1); d = c + 1
            t += [(a, c, d), (a, d, b)]
    return t

def tube(P, sides, bark_scale=0.9):
    """tapered tube along the island's main axis: (pos, tris, uv per corner) from the island's vertices P"""
    c = P.mean(0); Q = P - c
    w, V = np.linalg.eigh(Q.T @ Q / len(Q)); ax = V[:, 2]
    s = Q @ ax; L = s.max() - s.min()
    perp = Q - np.outer(s, ax); d = np.linalg.norm(perp, axis=1)
    lo = s < s.min() + 0.25 * L; hi = s > s.max() - 0.25 * L
    r0 = min(max(np.median(d[lo]), 1e-3), 0.03 * L) if lo.any() else 0.01; r1 = min(max(np.median(d[hi]) * 0.7, 1e-3), 0.02 * L) if hi.any() else 0.01
    # a perpendicular frame
    u = np.cross(ax, [0, 0, 1.0]); u = u / np.linalg.norm(u) if np.linalg.norm(u) > 1e-6 else np.array([1.0, 0, 0]); v = np.cross(ax, u)
    ang = np.linspace(0, 2 * math.pi, sides, endpoint=False)
    ring = lambda r: np.array([u * math.cos(a) * r + v * math.sin(a) * r for a in ang])
    pos = np.concatenate([c + ax * s.min() + ring(r0), c + ax * s.max() + ring(r1)])
    tris, uvc = [], []
    for k in range(sides):
        k2 = (k + 1) % sides
        for (a, b, cc) in ((k, k2, sides + k2), (k, sides + k2, sides + k)):
            tris.append((a, b, cc))
    tris = np.array(tris)
    uvv = np.zeros((len(pos), 2)); uvv[:sides, 0] = np.arange(sides) / sides; uvv[sides:, 0] = np.arange(sides) / sides; uvv[sides:, 1] = L / bark_scale
    uvc = uvv[tris]
    return pos, tris, uvc

def make_mesh_obj(name, pos, tris, uvs_per_vert=None, uv_per_corner=None, mat_count=0):
    me = bpy.data.meshes.new(name)
    me.from_pydata(pos.tolist(), [], np.asarray(tris).tolist())
    me.update()
    uvl = me.uv_layers.new(name='UVMap')
    if uv_per_corner is None:
        tri = np.asarray(tris); uv_per_corner = uvs_per_vert[tri.reshape(-1)]
    uvl.uv.foreach_set('vector', np.asarray(uv_per_corner, np.float32).reshape(-1))
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o

def decimate(o, target):
    n = len(o.data.polygons)
    if n <= target: return o
    md = o.modifiers.new('d', 'DECIMATE'); md.decimate_type = 'COLLAPSE'; md.ratio = max(0.02, target / n); md.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    o.modifiers.remove(md)
    old = o.data; o.data = me; bpy.data.meshes.remove(old)
    return o

def box_uv(pos, tris, scale):
    """box projection, metres/tile = scale; returns per-corner uv (ntri,3,2)"""
    p = pos[tris]; n = np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]); ax = np.abs(n).argmax(1)
    uv = np.empty((len(tris), 3, 2))
    for k, (a, b) in enumerate(((1, 2), (0, 2), (0, 1))):
        m = ax == k; uv[m, :, 0] = p[m][:, :, a] / scale; uv[m, :, 1] = p[m][:, :, b] / scale
    return uv

def smooth(o):
    for p in o.data.polygons: p.use_smooth = True

def set_normals(o, fn):
    """fn(positions(loop,3), default corner normals(loop,3)) -> new corner normals"""
    me = o.data
    cn = np.empty(len(me.loops) * 3, np.float32); me.corner_normals.foreach_get('vector', cn)
    cn = cn.reshape(-1, 3)
    lv = np.empty(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', lv)
    co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    out = fn(co[lv], cn)
    me.normals_split_custom_set(out.astype(np.float32).tolist())

def tri_count(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)


# ----------------------------------------------------------------------------- one tree
def build(code, spec):
    reset()
    srcdir = os.path.join(DL, spec['src']); gltf = os.path.join(srcdir, spec['src'] + '_2k.gltf'); texdir = os.path.join(srcdir, 'textures')
    bpy.ops.import_scene.gltf(filepath=gltf)
    mtex = mat_textures(gltf)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if len(meshes) > 1 and spec.get('join'):
        bpy.ops.object.select_all(action='DESELECT')
        for m in meshes: m.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]; bpy.ops.object.join(); meshes = [bpy.context.view_layer.objects.active]
    if len(meshes) > 1:
        meshes.sort(key=lambda o: -len(o.data.polygons));
        for o in meshes[1:]: bpy.data.objects.remove(o)
    o = meshes[0]; print(code, 'source object', o.name, 'polys', len(o.data.polygons))
    co, tv, tuv, pm, slots = extract(o)
    matname = np.array(slots)[pm]
    isl = Islands(co, tv, tuv)
    shrub = spec['kind'] == 'shrub'
    rng = np.random.default_rng(1000 + int(code[2:]))

    if spec.get('cluster'):
        # several plants in one mesh: keep the island cluster (split at the largest gaps along the widest axis) with most triangles
        ncl, rank = spec['cluster']
        cen = np.array([co[np.unique(tv[isl.tris(i)])].mean(0) for i in range(isl.n)])
        ax = np.ptp(cen, 0).argmax(); o_ = np.argsort(cen[:, ax]); gaps = np.diff(cen[o_, ax]); cuts = np.sort(np.argsort(-gaps)[:ncl - 1])
        grp = np.zeros(isl.n, int); grp[o_] = np.searchsorted(cuts, np.arange(isl.n), side='left')
        best = sorted(range(ncl), key=lambda g: -isl.tri_count[grp == g].sum())[rank]
        keep_isl = set(np.nonzero(grp == best)[0].tolist()); print(code, 'cluster', best, 'of', ncl, 'islands', len(keep_isl))
    else: keep_isl = None
    # --- classify islands -> card islands / wood islands
    isl_mat = matname[isl.order[isl.start]]
    card_ids, wood_ids, fits = [], [], {}
    leafmat = spec.get('leaf')
    for i in range(isl.n):
        if keep_isl is not None and i not in keep_isl: continue
        if shrub or isl_mat[i] == leafmat:
            f = isl.fit(i)
            if (not shrub) or (f['planar'] <= PLANAR_MAX and isl.tri_count[i] <= 400): card_ids.append(i); fits[i] = f
            else: wood_ids.append(i)       # shrub stems; leaf mats: odd islands are dropped below
        else: wood_ids.append(i)
    if not shrub:
        wood_ids = [i for i in wood_ids if isl_mat[i] != leafmat]
        med = np.median([fits[i]['ext'] for i in card_ids])
        keep = [i for i in card_ids if fits[i]['ext'] <= 2.5 * med]
        print(code, 'dropped oversize leaf islands', len(card_ids) - len(keep)); card_ids = keep
    if shrub:
        pl = np.array([isl.fit(i)['planar'] for i in range(isl.n)]); print(code, 'planar hist', np.histogram(pl, bins=[0, .1, .2, .3, .4, .5, .6, .8, 1.01])[0], 'tris', np.percentile(isl.tri_count, [0, 25, 50, 75, 100]))
    print(code, 'islands', isl.n, 'cards', len(card_ids), 'wood islands', len(wood_ids), 'dropped nonplanar leaf islands', int((isl_mat == leafmat).sum()) - len(card_ids) if not shrub else 0)
    # --- normalise: ground, pivot, height
    wood_tri = np.concatenate([isl.tris(i) for i in wood_ids]) if wood_ids else np.zeros(0, int)
    wv = co[np.unique(tv[wood_tri].reshape(-1))] if len(wood_tri) else co
    if shrub: wv = co[np.unique(tv[np.concatenate([isl.tris(i) for i in card_ids + wood_ids])].reshape(-1))]
    z0 = wv[:, 2].min()
    cardpts = np.array([fits[i]['c'] for i in card_ids])
    zmax = max(co[np.unique(tv[wood_tri].reshape(-1))][:, 2].max() if len(wood_tri) else -1e9, np.percentile(cardpts[:, 2], 99.9) + 0.0)
    if not shrub: zmax = max(zmax, cardpts[:, 2].max())
    else: zmax = max(zmax, wv[:, 2].max())
    k = spec['height'] / (zmax - z0)
    base = wv[wv[:, 2] < z0 + 0.04 * (zmax - z0)]
    pivot = np.array([base[:, 0].mean(), base[:, 1].mean(), z0])
    X = lambda p: (p - pivot) * k
    for i in card_ids:
        f = fits[i]; f['c'] = X(f['c']); f['s0'] *= k; f['s1'] *= k; f['t0'] *= k; f['t1'] *= k
    co_n = X(co)
    print(code, 'height src', round(zmax - z0, 3), 'scale', round(k, 4))

    # --- textures
    os.makedirs(os.path.join(OUT, code), exist_ok=True)
    outd = os.path.join(OUT, code)
    res = spec['res']
    # leaf/atlas texture
    if shrub:
        ldiff = mtex[spec['mixed']]
    else:
        ldiff = mtex[spec['leaf']]
    dp, ap, np_ = tex_paths(texdir, ldiff)
    albedo = resize(load_raw(dp), res)
    H, W = albedo.shape[:2]
    sigs = {}
    pool = card_ids + (wood_ids if shrub else [])
    for i in pool:
        t = isl.tris(i); u = isl.tuv[t].reshape(-1, 2)
        key = tuple(np.round(np.concatenate([u.min(0), u.max(0)]) * W / 2).astype(int)) + (len(t),)
        sigs.setdefault(key, i)
    print(code, 'unique uv signatures', len(sigs), 'of', len(pool))
    uvt = np.concatenate([isl.tuv[isl.tris(i)] for i in sigs.values()])
    mask = rasterise(uvt, W, H)
    valid = erode(mask, 2)
    rgb = bleed(albedo[..., :3], valid, 8)
    alb = np.ones((H, W, 4), np.float32); alb[..., :3] = rgb; alb[..., 3] = mask.astype(np.float32)
    save_img(alb, os.path.join(outd, code + '_leaf_albedo.png'), 'PNG')
    ms, _occ = arm_maps(ap, res)
    msb = bleed(ms, valid, 8); msb[..., 3] = np.where(valid | mask, msb[..., 3], msb[..., 3])
    save_img(msb, os.path.join(outd, code + '_leaf_ms.png'), 'PNG')
    nrm = resize(load_raw(np_), res); save_img(nrm, os.path.join(outd, code + '_leaf_normal.jpg'), 'JPEG')
    if not shrub:
        bdiff = mtex[spec['bark_tex']]
        dp, ap, np_ = tex_paths(texdir, bdiff)
        shutil.copyfile(dp, os.path.join(outd, code + '_bark_albedo.jpg')); shutil.copyfile(np_, os.path.join(outd, code + '_bark_normal.jpg'))
        ms, occ = arm_maps(ap, res)
        save_img(ms, os.path.join(outd, code + '_bark_ms.png'), 'PNG'); save_img(occ, os.path.join(outd, code + '_bark_occ.png'), 'PNG')

    # --- wood candidates (trees: trunk always, branches ranked by length)
    if shrub:
        cand = [(i, False, True) for i in wood_ids]
        rank_ids = []
    keep_all = []; ranked = []
    if not shrub:
        for (mname, remap, always) in spec['wood_parts']:
            for i in wood_ids:
                if isl_mat[i] != mname: continue
                (keep_all if always else ranked).append((i, remap))
        ranked.sort(key=lambda r: -isl.fit(r[0])['ext'] if True else 0)
    else:
        keep_all = [(i, False) for i in wood_ids]
    nvalid = len(card_ids)
    order_cards = rng.permutation(nvalid)    # rank -> card index; first N kept at LOD with N cards (nested)
    crown_pts = np.array([fits[i]['c'] for i in card_ids])
    cmin, cmax = crown_pts.min(0), crown_pts.max(0)
    ctr = (cmin + cmax) / 2; rad = np.maximum((cmax - cmin) / 2, 1e-3) * 1.15
    ctr[2] = cmin[2] + (cmax[2] - cmin[2]) * 0.5
    objs = []
    for lod in range(3):
        parts = []
        # wood
        if shrub:
            srt = sorted(keep_all, key=lambda r: -isl.tri_count[r[0]]); sel = srt[:max(1, int(round(len(srt) * (1.0, 0.6, 0.25)[lod])))] if srt else []
        else: sel = list(keep_all) + ranked[:spec['keep_branches'][lod]]
        if sel:
            pos_l, tri_l, uv_l, off = [], [], [], 0
            allv = []
            for (i, remap) in sel:
                t = isl.tris(i); vv = isl.tv[t]
                if spec.get('tubes') and isl.tri_count[i] <= 300:
                    tp, tt_, tu = tube(co_n[np.unique(vv.reshape(-1))], spec['tubes'][lod])
                    pos_l.append(tp); tri_l.append(tt_ + off); uv_l.append(tu); off += len(tp); continue
                uniq, inv = np.unique(vv.reshape(-1), return_inverse=True)
                pos_l.append(co_n[uniq]); tri_l.append(inv.reshape(-1, 3) + off)
                uv_l.append(box_uv(co_n, vv, 0.9) if remap else isl.tuv[t]); off += len(uniq)
            pos = np.concatenate(pos_l); tris = np.concatenate(tri_l); uvc = np.concatenate(uv_l)
            wo = make_mesh_obj(code + '_wood%d' % lod, pos, tris, uv_per_corner=uvc)
            # weld by distance keeps seams per-corner UVs; decimate to the wood budget
            wo = decimate(wo, spec['wood_t'][lod])
            smooth(wo); parts.append(('wood', wo))
        # cards
        if shrub: L, W = spec['segs'][lod]
        else: L, W = 1, 1
        per = 2 * L * W
        N = min(nvalid, max(1, spec['leaf_tris'][lod] // per))
        sc = spec['cov'][lod] * math.sqrt(nvalid / N) if N < nvalid else 1.0
        if shrub: sc = min(sc, 2.4) * spec.get('leafscale', 1.0)
        idx = order_cards[:N]
        pos_l, uv_l, tri_l = [], [], []; off = 0; tt = grid_tris(L, W)
        for ci in sorted(idx):
            p, u, fl = grid_card(fits[card_ids[ci]], L, W, sc, ctr, rad)
            pos_l.append(p); uv_l.append(u); tri_l += [((a + off, d + off, c + off) if fl else (a + off, c + off, d + off)) for a, c, d in tt]; off += len(p)
        pos = np.concatenate(pos_l); uvv = np.concatenate(uv_l)
        co_card = make_mesh_obj(code + '_cards%d' % lod, pos, np.array(tri_l), uvs_per_vert=uvv)
        def crown_n(lp, cn, ctr=ctr, rad=rad):
            s = (lp - ctr) / rad; s /= np.maximum(np.linalg.norm(s, axis=1, keepdims=True), 1e-6)
            flip = np.sign(np.einsum('ij,ij->i', cn, s)); flip[flip == 0] = 1
            n = s * 0.85 + cn * flip[:, None] * 0.15
            return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-6)
        set_normals(co_card, crown_n)
        parts.append(('cards', co_card))
        print(code, 'LOD%d' % lod, 'cards', N, 'of', nvalid, 'scale', round(sc, 2), 'card tris', tri_count(co_card), 'wood tris', tri_count(dict(parts)['wood']) if 'wood' in dict(parts) else 0)
        # join: wood slot 0 (bark) then cards slot 1 (leaves); shrubs: leaves slot only (wood shares the atlas)
        bpy.ops.object.select_all(action='DESELECT')
        mats = make_materials(code, shrub)
        for name, po in parts:
            po.data.materials.clear()
            if shrub: po.data.materials.append(mats['leaf'])
            else: po.data.materials.append(mats['bark'] if name == 'wood' else mats['leaf'])
            if not shrub and name == 'cards':
                pass
        if not shrub:
            # make sure a card-only mesh still has bark in slot 0
            pass
        for name, po in parts: po.select_set(True)
        bpy.context.view_layer.objects.active = parts[0][1]
        if len(parts) > 1: bpy.ops.object.join()
        jo = bpy.context.view_layer.objects.active
        assert [m.name for m in jo.data.materials] == ([mats['bark'].name, mats['leaf'].name] if not shrub else [mats['leaf'].name]), [m.name for m in jo.data.materials]
        jo.name = '%s_%s' % (code, LOD_NAMES[lod]); jo.data.name = jo.name
        objs.append(jo)
        print(code, jo.name, 'total tris', tri_count(jo))
    # the random LOD0 subset decides the real height: bring all LODs to the code's height exactly
    kf = spec['height'] / objs[0].dimensions.z
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    for o in objs: o.scale = (kf, kf, kf)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    print(code, 'height fix', round(kf, 4), [round(o.dimensions.z, 3) for o in objs])
    return objs

_mats = {}
def make_materials(code, shrub):
    if code in _mats: return _mats[code]
    outd = os.path.join(OUT, code)
    def mk(name, tex, alpha):
        m = bpy.data.materials.new(name); m.use_nodes = True
        nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(os.path.join(outd, tex), check_existing=False)
        nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
        if alpha: nt.links.new(t.outputs['Alpha'], bsdf.inputs['Alpha']); m.surface_render_method = 'DITHERED'
        m.use_backface_culling = False
        bsdf.inputs['Roughness'].default_value = 0.8
        return m
    r = {'leaf': mk('M_%s_Leaves' % code, code + '_leaf_albedo.png', True)}
    if not shrub: r['bark'] = mk('M_%s_Bark' % code, code + '_bark_albedo.jpg', False)
    _mats[code] = r
    return r


# ----------------------------------------------------------------------------- export + preview
def export(code, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, code, code + '.fbx'), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
                             mesh_smooth_type='OFF', add_leaf_bones=False, bake_anim=False, use_custom_props=False, path_mode='STRIP')

def preview(code, spec):
    """re-import the exported FBX with the generated textures and render LOD0 beside LOD2."""
    reset(); _mats.clear()
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, code, code + '.fbx'))
    shrub = spec['kind'] == 'shrub'
    mats = make_materials(code, shrub)
    objs = {o.name.split('.')[0]: o for o in bpy.data.objects if o.type == 'MESH'}
    print('REIMPORT', {n: (round(o.dimensions.z, 2), [round(v, 2) for v in o.location]) for n, o in objs.items()})
    h = spec['height']; w = max(objs[code + '_LOD0'].dimensions.x, objs[code + '_LOD0'].dimensions.y)
    for n, o in objs.items():
        for si, sl in enumerate(o.material_slots):
            sl.material = mats['leaf'] if (shrub or si == 1) else mats['bark']
        for p in o.data.polygons: p.use_smooth = True
    gap = max(w, h * 0.5) * 1.1
    objs[code + '_LOD0'].location.x = -gap * 0.6; objs[code + '_LOD2'].location.x = gap * 0.6
    objs[code + '_LOD1'].hide_render = True
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.eevee.taa_render_samples = 20
    sc.render.resolution_x = 960; sc.render.resolution_y = 640 if h > 3 else 520
    bpy.ops.mesh.primitive_plane_add(size=h * 6, location=(0, 0, 0)); gp = bpy.context.active_object
    gm = bpy.data.materials.new('g'); gm.use_nodes = True; gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.18, 0.2, 0.1, 1); gp.data.materials.append(gm)
    w_ = bpy.data.worlds.new('w'); w_.use_nodes = True; w_.node_tree.nodes['Background'].inputs['Color'].default_value = (0.55, 0.7, 0.9, 1); w_.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0
    sc.world = w_
    bpy.ops.object.light_add(type='SUN', location=(5, -5, 10)); sun = bpy.context.active_object; sun.data.energy = 3.5; sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    bpy.ops.object.camera_add(); cam = bpy.context.active_object; sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = max(gap * 2.0 + w, h * 1.5 * 960 / (640 if h > 3 else 520)) * 1.05
    cam.location = (0, -h * 4, h * 0.55); cam.rotation_euler = (math.radians(86), 0, 0)
    zoom = float(os.environ.get('SC_ZOOM', '0'))
    if zoom:
        cam.data.ortho_scale = h * zoom; cam.location = (-gap * 0.6, -h * 4, h * 0.7)
        objs[code + '_LOD2'].hide_render = True
    sc.view_settings.view_transform = 'Standard'
    os.makedirs(CHECKS, exist_ok=True)
    sc.render.filepath = os.path.join(CHECKS, code + ('_zoom.png' if zoom else '_preview.png')); sc.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    print('PREVIEW', sc.render.filepath)


if __name__ == '__main__':
    for code, spec in SPEC.items():
        if ONLY and code not in ONLY: continue
        _mats.clear()
        objs = build(code, spec)
        export(code, objs)
        if CHECKS: preview(code, spec)
    print('SC TREES DONE')
