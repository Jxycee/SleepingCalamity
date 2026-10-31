"""B9 foliage for the beauty corner (PHASE1 4, B9; docs/art/beauty-corner-brief.md): ivy, crack weeds, moss tufts, a moss decal
and grass clumps, all sharing one 2K foliage atlas (the moss decal has its own 1K set).

Sources (CC0, Art/Source/): ambientCG LeafSet017 (ivy leaves, with opacity, normal, roughness) and Moss002 (moss tile);
Poly Haven weed_plant_02 (broadleaf rosettes) and nettle_plant (nettles), adopted as meshes: decimated, alpha from their
own UV coverage, regraded and packed into the atlas. moss_01 (246k tris) was not needed: the tufts are built from Moss002.

The atlas (T_B9_Foliage, 2048, quadrants in UV space):
  u 0-.5  v .5-1   nettle leaves (nettle_plant diff, regraded)       u .5-1  v .5-1   weed rosette leaves (weed_plant_02)
  u 0-.5  v 0-.5   ivy: 3 cluster cells (runner + 4-7 leaves each) and 4 single leaves, composited from LeafSet017
  u .5-1  v 0-.5   grass blade | moss tile / moss fringe | ivy stem bark
Leaf colour is pulled to the PHASE1 3.1 foliage key #6f8a34 (ivy inner layer #4a5a24, young tips #9aa83c), +10-18% chroma;
moss to #616e2e/#b6ab28.

Models follow the corner builder's convention (BeautyCornerTable.cs): Blender z up, metres, front -Y = Unity -Z, pivot at the
ground centre. Each exports to its own folder Assets/_Project/Content/Art/Town/B9/<Folder>/ (FOLDERS below) with its texture set.
  SC_B9_IvyWall     (Ivy_Facade) 2.1 x 5 m mass for the facade's left 2 m. Pivot = bottom centre, back on the wall plane, leaves
                    toward -Y, the dense corner side at local -x. Hugs the ground storey, wraps the jetty beam and climbs the upper
                    storey jettied 0.45 m (--jetty, default 3.0 m like the builder's placeholder, until B4 lands); a hanging fringe
                    drops from the jetty edge and a few tendrils escape at the top and the open side.
  SC_B9_IvyPatchA/B (Ivy_Patch_A/B) 1.0 x 1.4 m and 0.7 x 0.6 m flat-wall patches, bottom centre on the wall.
  SC_B9_CrackWeed_A..E  (Weed_A..E) narrow runs along local x for cobble joints and the wall foot (weeds grow in joints).
  SC_B9_MossTuft_A..C   (Moss_Tuft_A..C) low cushions with a fringe skirt.   SC_B9_GrassClump_A..C (Grass_A..C) ribbon blades.
  SC_B9_BasinMoss   (Basin_Moss) a 0.9 x 0.5 m cushion lying on the basin rim (own 1K set).
  SC_B9_MossBandA   (Moss_Band_A) a low 110 deg band at the basin's south-west foot with moss running out along the joints.
  SC_B9_MossDecal   (Moss_Band) a 70 deg band on the basin wall (r 1.7 m, 0-0.38 m) plus a ground lip (own 1K set).
                    Pivot = the basin centre; centred on local -Y (the cameras' side) at yaw 0.
Cards and ribbons carry bent normals (blended toward the clump's own up-and-out direction) and an AO colour attribute 'Col'
(dark core to lit rim, for a future foliage shader; URP Lit ignores it). Unity: Normals = Import, URP Lit, Alpha Clipping
0.5, Render Face Both, BaseColor 'Mip Maps Preserve Coverage' on; ORM = R occlusion, G roughness, B metallic.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_foliage_town.py -- --out <scratch>
      [--stage all|atlas|models|render] [--res 2048] [--jetty 3.0] [--export]   (--export writes the B9 folders)
      [--rres 960] [--samples 32]
Review renders lay the pieces at the builder's rows (BUILDER) plus suggested spots for the rest (EXTRA). Sources: B9/SOURCES.md.

Status (2026-10-30, PC): exported at 2048. Round 4 (from the controller's in-engine capture: ivy sparse, pale and blue-grey,
weeds too small, moss hidden on the north side) calibrated 31/40: ivy is two card layers of dense leaf-mass cells (inner #4a5a24,
outer #6f8a34, young #9aa83c tips only), roughness about 0.62, half-strength normal, a root mass at the foot and a few bare-wall
gaps; weeds are 1.6x with 12-20 blades each and five distinct sizes; Moss_Band_A covers the visible south-west foot. Earlier rounds:
22, 27, 28.5. Tris: ivy wall 6.0k (facade dressing, inside the building budget), patches 354 / 208, weeds 360-400, tufts 132,
grass 128-224, bands 72 / 104, rim moss 168. Parked: a foliage wind shader reading 'Col'.
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, noise
import sc_b9_lib as L

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
STAGE = arg('--stage', 'all'); RES = int(arg('--res', '2048')); JETTY = float(arg('--jetty', str(L.JETTY_Z)))
RRES = int(arg('--rres', '960')); SAMPLES = int(arg('--samples', '32')); EXPORT = '--export' in argv
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
SRC = os.path.join(REPO, 'Art', 'Source')
EXP = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B9')
TEX = os.path.join(OUT, 'tex'); os.makedirs(TEX, exist_ok=True)
ATLAS = os.path.join(TEX, 'T_B9_Foliage'); DECAL = os.path.join(TEX, 'T_B9_MossDecal'); RIM = os.path.join(TEX, 'T_B9_BasinMoss')
BANDA = os.path.join(TEX, 'T_B9_MossBandA')

# ivy is kept yellower than the facade's sage / forest-green shutters (brief section 0) so the two never merge
FOLIAGE = '#6f8a34'; IVY = '#6f8a34'; IVY_DARK = '#4a5a24'; IVY_YOUNG = '#9aa83c'; MOSS_D = '#616e2e'; MOSS_L = '#b6ab28'

# UV rectangles (u0, v0, u1, v1) in the atlas
Q_NETTLE = (0.0, 0.5, 0.5, 1.0); Q_WEED = (0.5, 0.5, 1.0, 1.0)
IVY_CELLS = [(0.0, 0.25, 0.25, 0.5), (0.25, 0.25, 0.5, 0.5)]            # green leaf masses (outer layer)
IVY_DARK_CELL = (0.0, 0.0, 0.25, 0.25)                                    # the shaded inner layer, #4a5a24
IVY_SINGLES = [(0.25, 0.125, 0.375, 0.25), (0.375, 0.125, 0.5, 0.25)]     # green singles (strands, fringe)
IVY_YOUNG_CELL = (0.25, 0.0, 0.375, 0.125); IVY_DARK_SINGLE = (0.375, 0.0, 0.5, 0.125)
C_GRASS = (0.5, 0.25, 0.625, 0.5); C_GRASS2 = (0.625, 0.25, 0.75, 0.5)
C_MOSS = (0.75, 0.25, 1.0, 0.5); C_FRINGE = (0.5, 0.0, 0.75, 0.25); C_STEM = (0.75, 0.0, 1.0, 0.25)

WEED_SRC = os.path.join(SRC, 'PolyHaven', 'weed_plant_02', 'weed_plant_02_2k.gltf')
NETTLE_SRC = os.path.join(SRC, 'PolyHaven', 'nettle_plant', 'nettle_plant_2k.gltf')

def import_gltf(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    obs = [o for o in bpy.data.objects if o not in before and o.type == 'MESH']
    for o in [o for o in bpy.data.objects if o not in before and o.type != 'MESH']: bpy.data.objects.remove(o)
    return obs

def mesh_uv_polys(obs):
    polys = []
    for o in obs:
        me = o.data; uv = np.zeros(len(me.loops) * 2, np.float32); me.uv_layers[0].data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
        for p in me.polygons: polys.append(uv[list(p.loop_indices)])
    return polys

# ============================================================================= ATLAS
def put(dst, rect, src, res):
    u0, v0, u1, v1 = rect
    x0, y0, x1, y1 = int(u0 * res), int(v0 * res), int(u1 * res), int(v1 * res)
    dst[y0:y1, x0:x1] = L.resize(src, y1 - y0, x1 - x0)

def leaf_crops(target=IVY, chroma=1.1):
    """The six LeafSet017 leaves as crops graded to `target`: channels r g b a nx ny nz rough; base (petiole) at the bottom centre.
    In-engine round 3 read pale and blue-grey: the veins are damped harder, the roughness sits at about 0.62 and the normal is
    flattened to half strength so the sky stops glinting off every leaf."""
    d = os.path.join(SRC, 'ambientCG', 'LeafSet017')
    col = L.load(os.path.join(d, 'LeafSet017_2K-JPG_Color.jpg'))[..., :3]
    op = L.load(os.path.join(d, 'LeafSet017_2K-JPG_Opacity.jpg'))[..., 0]
    nm = L.load(os.path.join(d, 'LeafSet017_2K-JPG_NormalGL.jpg'))[..., :3]
    ro = L.load(os.path.join(d, 'LeafSet017_2K-JPG_Roughness.jpg'))[..., 0]
    lin = L.srgb2lin(col); Lw = L.lum(lin) + 1e-5
    Lb = L.blur(Lw * op, 6) / np.maximum(L.blur(op, 6), 1e-3)
    col = L.lin2srgb(lin * np.clip(Lb / Lw, 0.3, 3.0)[..., None] ** 0.75)          # damp the pale backlit veins
    col = L.grade(col, op, target, hue_keep=0.35, contrast=1.08, chroma=chroma, delight=0.35, sigma=60)
    col = L.bleed(col, op, sigma=4, passes=2)
    nm = L.renorm(np.concatenate([(nm[..., :2] - 0.5) * 0.5 + 0.5, nm[..., 2:3]], -1))
    full = np.concatenate([col, op[..., None], nm, (0.58 + 0.1 * ro)[..., None]], -1)
    H = full.shape[0]; crops = []
    for c in range(2):
        for r in range(3):
            x0, x1 = c * 1024, (c + 1) * 1024; y1 = H - r * 683; y0 = max(0, H - (r + 1) * 683)
            m = op[y0:y1, x0:x1] > 0.5
            ys, xs = np.nonzero(m)
            a, b = max(ys.min() - 6, 0), min(ys.max() + 6, y1 - y0); e, f = max(xs.min() - 6, 0), min(xs.max() + 6, x1 - x0)
            crop = full[y0 + a:y0 + b, x0 + e:x0 + f].copy()
            mm = crop[..., 3] > 0.5; yy, xx = np.nonzero(mm)
            base = (float(np.median(xx[yy < yy.min() + 12])), float(yy.min()))   # petiole tip: the lowest leaf pixels
            crops.append((crop, base))
    return crops

def stroke(cell, ao, pts, width, rgb, shade=0.85):
    """Draw a petiole / stem polyline (opaque) into the cell (flat normal)."""
    H, W = cell.shape[:2]
    for p, q in zip(pts[:-1], pts[1:]):
        x0, x1 = int(max(0, min(p[0], q[0]) - width - 2)), int(min(W - 1, max(p[0], q[0]) + width + 2))
        y0, y1 = int(max(0, min(p[1], q[1]) - width - 2)), int(min(H - 1, max(p[1], q[1]) + width + 2))
        X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = np.array(q) - np.array(p); ll = max(d @ d, 1e-6)
        t = np.clip(((X - p[0]) * d[0] + (Y - p[1]) * d[1]) / ll, 0, 1)
        dist = np.hypot(X - (p[0] + t * d[0]), Y - (p[1] + t * d[1]))
        a = np.clip(width - dist + 0.5, 0, 1)
        sub = cell[y0:y1 + 1, x0:x1 + 1]
        across = np.clip(1 - dist / max(width, 1), 0, 1)               # rounded stem: lighter down its middle
        c = np.array(rgb, np.float32)[None, None] * (0.7 + 0.45 * across[..., None])
        sub[..., :3] = sub[..., :3] * (1 - a[..., None]) + c * a[..., None]
        sub[..., 3] = np.maximum(sub[..., 3], a)
        sub[..., 4:7] = sub[..., 4:7] * (1 - a[..., None]) + np.array([0.5, 0.5, 1.0]) * a[..., None]
        sub[..., 7] = sub[..., 7] * (1 - a) + 0.6 * a
        ao[y0:y1 + 1, x0:x1 + 1] = ao[y0:y1 + 1, x0:x1 + 1] * (1 - a) + shade * a

def stamp(cell, ao, crop, base, at, size, theta, flip, dark=1.0):
    """Composite one leaf: its base at `at`, its length scaled to `size` px, rotated theta (CCW, from +v), drop shadow first."""
    H, W = cell.shape[:2]; ch, cw = crop.shape[:2]
    s = size / ch
    R = np.array([[math.cos(theta), -math.sin(theta)], [math.sin(theta), math.cos(theta)]])
    corners = np.array([[0, 0], [cw, 0], [0, ch], [cw, ch]], np.float64) - base
    if flip: corners[:, 0] *= -1
    cc = (R @ (corners.T * s)).T + at
    x0, x1 = int(max(0, cc[:, 0].min() - 2)), int(min(W, cc[:, 0].max() + 2))
    y0, y1 = int(max(0, cc[:, 1].min() - 2)), int(min(H, cc[:, 1].max() + 2))
    if x1 <= x0 or y1 <= y0: return
    X, Y = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
    dx, dy = X - at[0], Y - at[1]
    lx = (math.cos(theta) * dx + math.sin(theta) * dy) / s; ly = (-math.sin(theta) * dx + math.cos(theta) * dy) / s
    if flip: lx = -lx
    smp = L.sample(crop, lx + base[0] - 0.5, ly + base[1] - 0.5)
    a = np.clip((smp[..., 3] - 0.35) / 0.3, 0, 1)
    # soft drop shadow of the new leaf on what is already there (light from the top: shadow falls down)
    sh = np.zeros((H, W), np.float32); sh[y0:y1, x0:x1] = a
    sh = np.roll(sh, -int(0.035 * H), 0); sh = L.blur(sh, 0.012 * H)
    shade = 1 - 0.45 * sh * cell[..., 3]
    cell[..., :3] *= shade[..., None]; ao *= shade
    n = smp[..., 4:7] * 2 - 1
    if flip: n[..., 0] = -n[..., 0]
    nx = math.cos(theta) * n[..., 0] - math.sin(theta) * n[..., 1]; ny = math.sin(theta) * n[..., 0] + math.cos(theta) * n[..., 1]
    n = np.stack([nx, ny, n[..., 2]], -1) * 0.5 + 0.5
    sub = cell[y0:y1, x0:x1]; A = a[..., None]
    sub[..., :3] = sub[..., :3] * (1 - A) + smp[..., :3] * dark * A
    sub[..., 3] = np.maximum(sub[..., 3], a)
    sub[..., 4:7] = sub[..., 4:7] * (1 - A) + n * A
    sub[..., 7] = sub[..., 7] * (1 - a) + smp[..., 7] * a
    ao[y0:y1, x0:x1] = ao[y0:y1, x0:x1] * (1 - a) + dark * a

STEM_RGB = L.lin2srgb(L.hex2lin('#4d4a2a')).tolist()

def ivy_mass(crops, S, seed, n_back, n_front):
    """A leaf MASS (not a sprig): a hidden runner, a back layer of shaded leaves and a front layer, filling about 85% of the
    cell so a card reads as solid foliage with only small gaps. Tips point up (+v)."""
    rr = random.Random(seed)
    cell = np.zeros((S, S, 8), np.float32); cell[..., 4:7] = (0.5, 0.5, 1.0); cell[..., 7] = 0.6
    ao = np.ones((S, S), np.float32)
    stroke(cell, ao, [(S * 0.5, 0), (S * 0.47, S * 0.3), (S * 0.53, S * 0.6)], 0.012 * S, STEM_RGB, 0.8)
    for layer, n, dark in ((0, n_back, 0.72), (1, n_front, 1.0)):
        for k in range(n):
            a = (k + rr.random() * 0.6) / n
            cx = S * (0.5 + 0.30 * math.cos(math.pi * a) * (0.6 + 0.4 * rr.random()))
            cy = S * (0.08 + 0.42 * math.sin(math.pi * a) * (0.6 + 0.4 * rr.random()) + 0.08 * layer)
            ang = math.atan2(-(cx - S * 0.5), max(cy, 1.0)) + rr.uniform(-0.25, 0.25)
            crop, base = crops[rr.randrange(len(crops))]
            stamp(cell, ao, crop, base, np.array([cx, cy]), S * rr.uniform(0.34, 0.44), ang, rr.random() < 0.5, dark=dark * rr.uniform(0.9, 1.0))
    return cell, ao

def ivy_single(crops, S, k):
    cell = np.zeros((S, S, 8), np.float32); cell[..., 4:7] = (0.5, 0.5, 1.0); cell[..., 7] = 0.5
    ao = np.ones((S, S), np.float32)
    crop, base = crops[k]
    stroke(cell, ao, [(S * 0.5, 0), (S * 0.5, S * 0.12)], 0.02 * S, STEM_RGB, 0.85)
    stamp(cell, ao, crop, base, np.array([S * 0.5, S * 0.1]), S * 0.78, 0.0, k % 2 == 1)
    return cell, ao

def build_atlas():
    res = RES; h = res // 2
    col = np.zeros((res, res, 3), np.float32); alp = np.zeros((res, res), np.float32)
    nrm = np.zeros((res, res, 3), np.float32); nrm[...] = (0.5, 0.5, 1.0)
    orm = np.zeros((res, res, 3), np.float32); orm[...] = (1.0, 0.6, 0.0)
    def rect_px(r):
        return int(r[0] * res), int(r[1] * res), int(r[2] * res), int(r[3] * res)
    def place(rect, c, a, n, o):
        x0, y0, x1, y1 = rect_px(rect); hh, ww = y1 - y0, x1 - x0
        col[y0:y1, x0:x1] = L.resize(c, hh, ww); alp[y0:y1, x0:x1] = L.resize(a[..., None], hh, ww)[..., 0]
        nrm[y0:y1, x0:x1] = L.renorm(L.resize(n, hh, ww)); orm[y0:y1, x0:x1] = L.resize(o, hh, ww)

    # --- weed / nettle quadrants: the scans, alpha = their own UV coverage (eroded) so the smeared padding never shows
    for path, rect, name in ((NETTLE_SRC, Q_NETTLE, 'nettle_plant'), (WEED_SRC, Q_WEED, 'weed_plant_02')):
        obs = import_gltf(path)
        cov = L.erode(L.raster_uv(mesh_uv_polys(obs), 1024), 1)
        for o in obs: bpy.data.objects.remove(o)
        d = os.path.dirname(path)
        c = L.load(os.path.join(d, 'textures', f'{name}_diff_2k.jpg'), 1024)[..., :3]
        n = L.load(os.path.join(d, 'textures', f'{name}_nor_gl_2k.jpg'), 1024)[..., :3]
        o = L.load(os.path.join(d, 'textures', f'{name}_arm_2k.jpg'), 1024)[..., :3]
        # the islands are loose polygons around each leaf on a black ground (the published alpha is not in the jpg):
        # alpha = UV coverage AND not-black, softened a little and eroded so no dark rim survives
        dark = L.blur((c.max(-1) > 0.075).astype(np.float32), 0.8)
        cov = L.erode(cov * (dark > 0.6), 1)
        c = L.grade(c, cov, FOLIAGE, hue_keep=0.7, contrast=1.08, chroma=1.18)
        c = L.bleed(c, cov, sigma=3, passes=2)
        o[..., 2] = 0.0; o[..., 1] = np.clip(o[..., 1], 0.55, 0.9)
        place(rect, c, cov, n, o)

    # --- ivy: two green leaf masses, one dark inner mass, singles (green x2, a young yellow tip, a dark one); composed at 2x
    green, dark, young = leaf_crops(IVY, 1.1), leaf_crops(IVY_DARK, 1.05), leaf_crops(IVY_YOUNG, 1.1)
    S = h                                    # a 512 cell (at 2048) composed at 1024
    def put_cell(rect, cell, ao):
        place(rect, cell[..., :3], cell[..., 3], cell[..., 4:7], np.stack([ao, cell[..., 7], np.zeros_like(ao)], -1))
    put_cell(IVY_CELLS[0], *ivy_mass(green, S, 3, 5, 6))
    put_cell(IVY_CELLS[1], *ivy_mass(green, S, 11, 4, 5))
    put_cell(IVY_DARK_CELL, *ivy_mass(dark, S, 23, 5, 5))
    for rect, crops, k in ((IVY_SINGLES[0], green, 0), (IVY_SINGLES[1], green, 2), (IVY_YOUNG_CELL, young, 3), (IVY_DARK_SINGLE, dark, 5)):
        put_cell(rect, *ivy_single(crops, S // 2, k))

    # --- grass blades: one blade per cell (u across, v along): dark base to sun-bleached tip, a midrib fold
    G = 512
    for rect, (base, mid, tip, seed) in ((C_GRASS, ('#3a4a1c', FOLIAGE, '#b0aa4c', 5)), (C_GRASS2, ('#3f4a20', '#7a8a38', '#c2b462', 9))):
        v = (np.arange(G * 2) + 0.5)[:, None] / (G * 2); u = (np.arange(G) + 0.5)[None, :] / G
        b, m, t = (L.hex2lin(x) for x in (base, mid, tip))
        g1 = L.sstep(v, 0.0, 0.55); g2 = L.sstep(v, 0.5, 1.0)
        lin = (b * (1 - g1)[..., None] + m * g1[..., None]) * (1 - g2)[..., None] + t * g2[..., None]
        streak = L.fbm((G * 2, G), seed, beta=1.4, kmin=3)
        streak = L.blur(streak, 1.0)
        lin = lin * (0.82 + 0.36 * streak[..., None]) * np.ones((1, G, 1), np.float32)
        across = np.abs(u - 0.5) * 2
        lin = lin * (1.08 - 0.25 * across ** 2)[..., None]
        lin = lin * (1 + 0.25 * np.exp(-((u - 0.5) / 0.04) ** 2))[..., None]           # pale midrib
        c = L.lin2srgb(lin)
        a = np.ones((G * 2, G), np.float32)
        nx = np.clip((u - 0.5) * 1.1, -0.45, 0.45) * np.ones_like(v)                  # V-fold across the blade
        n = np.stack([nx, np.zeros_like(nx), np.ones_like(nx)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True)
        o = np.stack([0.75 + 0.25 * g1, 0.55 - 0.1 * g2, np.zeros_like(g1)], -1) * np.ones((1, G, 1))
        place(rect, c, a, n * 0.5 + 0.5, o)

    # --- moss: tile, and a fringe cell with a ragged top edge
    d = os.path.join(SRC, 'ambientCG', 'Moss002')
    mc = L.load(os.path.join(d, 'Moss002_2K-JPG_Color.jpg'), 1024)[..., :3]
    mn = L.load(os.path.join(d, 'Moss002_2K-JPG_NormalGL.jpg'), 1024)[..., :3]
    ma = L.load(os.path.join(d, 'Moss002_2K-JPG_AmbientOcclusion.jpg'), 1024)[..., 0]
    mr = L.load(os.path.join(d, 'Moss002_2K-JPG_Roughness.jpg'), 1024)[..., 0]
    one = np.ones(mc.shape[:2], np.float32)
    mcol = L.grade(mc, one, MOSS_D, hue_keep=0.5, contrast=1.35, chroma=1.1)
    hi = L.fbm(mc.shape[:2], 41, beta=2.6, kmin=2)                                  # sunlit yellow tips in patches
    mcol = L.lin2srgb(L.srgb2lin(mcol) * (1 - 0.45 * L.sstep(hi, 0.55, 0.85))[..., None] + L.hex2lin(MOSS_L) * 0.45 * L.sstep(hi, 0.55, 0.85)[..., None] * (L.lum(L.srgb2lin(mc)) / L.lum(L.srgb2lin(mc)).mean())[..., None])
    morm = np.stack([0.55 + 0.45 * ma, np.clip(mr, 0.6, 1.0), np.zeros_like(ma)], -1)
    np.save(os.path.join(TEX, 'moss.npy'), np.concatenate([mcol, mn, morm], -1)[::2, ::2])
    place(C_MOSS, mcol, one, mn, morm)
    v = (np.arange(1024) + 0.5)[:, None] / 1024; u = (np.arange(1024) + 0.5)[None, :] / 1024
    edge = 0.62 + 0.18 * L.fbm((1, 1024), 7, beta=1.8, kmin=2)[0][None, :] + 0.08 * (L.fbm((1024, 1024), 8, beta=1.2, kmin=8) - 0.5)
    fa = np.clip((edge - v) / 0.04, 0, 1)
    fcol = mcol * (0.7 + 0.35 * L.sstep(v, 0.0, 0.6))[..., None]
    place(C_FRINGE, fcol, fa, mn, morm * np.array([1, 1, 1]) * (0.75 + 0.25 * v)[..., None] ** np.array([1, 0, 1]))

    # --- ivy stem bark (tiles along v only by mirroring in the UVs)
    bark = L.fbm((1024, 256), 13, beta=1.6, kmin=2)
    bark = np.repeat(bark, 4, 1)
    grooves = 0.5 + 0.5 * np.sin(u * math.pi * 10 + 3 * L.fbm((1024, 1024), 14, beta=2.4, kmin=1))
    b0, b1 = L.hex2lin('#4a3f30'), L.hex2lin('#5c5a34')
    lin = b0 * (1 - bark)[..., None] + b1 * bark[..., None]
    lin = lin * (0.8 + 0.3 * grooves)[..., None]
    place(C_STEM, L.lin2srgb(lin), one, L.normal_from_height(grooves * 0.6 + bark * 0.4, 3.0), np.stack([0.8 + 0.2 * grooves, 0.8 * one, 0 * one], -1))

    col2 = L.bleed(col, alp, sigma=2, passes=2)
    L.save(col2, ATLAS + '_BaseColor.png', alp)
    L.save(nrm, ATLAS + '_Normal.png'); L.save(orm, ATLAS + '_ORM.png')
    build_decal_tex()
    print('ATLAS ok', ATLAS)

def build_decal_tex():
    """T_B9_MossDecal (1024): moss tile with a mask that climbs from the basin foot, streaked by dripping water."""
    m = np.load(os.path.join(TEX, 'moss.npy'))               # 512 tile: col 3, normal 3, orm 3
    D = 1024
    tile = np.tile(m, (2, 2, 1))                               # 1024: two tiles across 1.2 m of band
    v = (np.arange(D) + 0.5)[:, None] / D; u = (np.arange(D) + 0.5)[None, :] / D
    n1 = L.fbm((D, D), 21, beta=2.2, kmin=2); n2 = L.fbm((D, D), 22, beta=1.4, kmin=6)
    streak = L.fbm((1, D), 23, beta=2.0, kmin=3)[0][None, :]
    height = 0.42 + 0.28 * n1 + 0.22 * (streak - 0.5) + 0.06 * (n2 - 0.5)       # how far up the band the moss climbs
    ends = L.sstep(u, 0.0, 0.18) * L.sstep(1 - u, 0.0, 0.18)
    a = np.clip((height * ends - v) / 0.09, 0, 1) * ends
    a = np.where(n2 > 0.86, a * 0.25, a)                                        # a few bare holes
    col = tile[..., :3] * (0.8 + 0.25 * L.sstep(v, 0, 0.5))[..., None]
    L.save(L.bleed(col, a, 3, 2), DECAL + '_BaseColor.png', a)
    L.save(L.renorm(tile[..., 3:6]), DECAL + '_Normal.png'); L.save(tile[..., 6:9], DECAL + '_ORM.png')
    # the rim cushion: an elliptical mask with a ragged, lobed edge and a few bare holes, darker at its foot
    r = np.hypot((u - 0.5) / 0.5, (v - 0.5) / 0.5)
    edge = 0.78 + 0.16 * (n1 - 0.5) * 2 + 0.06 * (n2 - 0.5)
    a2 = np.clip((edge - r) / 0.05, 0, 1) * np.where(n2 > 0.9, 0.2, 1.0)
    col2 = tile[..., :3] * (0.72 + 0.3 * np.clip(1 - r, 0, 1))[..., None]
    L.save(L.bleed(col2, a2, 3, 2), RIM + '_BaseColor.png', a2)
    L.save(L.renorm(tile[..., 3:6]), RIM + '_Normal.png'); L.save(tile[..., 6:9], RIM + '_ORM.png')
    # Moss_Band_A (2048 x 512): dense at the stone-meets-ground line, fingers running out along the joints, a ragged top on the wall
    Hh, Ww = 512, 2048
    tl = np.tile(m, (1, 4, 1))
    vv = (np.arange(Hh) + 0.5)[:, None] / Hh; uu = (np.arange(Ww) + 0.5)[None, :] / Ww
    f1 = L.fbm((Hh, Ww), 31, beta=2.0, kmin=2); f2 = L.fbm((Hh, Ww), 32, beta=1.2, kmin=10)
    joints = (0.5 + 0.5 * np.sin(uu * math.pi * 2 * 26 + 6 * f1)) ** 6                  # radial joint lines on the ground
    ground = np.clip(1.2 * L.sstep(vv, 0.15 + 0.35 * f1, 0.55) + 0.9 * joints * L.sstep(vv, 0.0, 0.5), 0, 1)
    wall = 1 - L.sstep(vv, 0.62 + 0.25 * f1, 0.72 + 0.25 * f1)
    a3 = np.where(vv < 0.55, ground, wall) * L.sstep(uu, 0, 0.12) * L.sstep(1 - uu, 0, 0.12)
    a3 = np.clip((a3 - 0.35) / 0.3, 0, 1) * np.where(f2 > 0.88, 0.3, 1.0)
    col3 = tl[..., :3] * (0.75 + 0.3 * f1[..., None])
    L.save(L.bleed(col3, a3, 3, 2), BANDA + '_BaseColor.png', a3)
    L.save(L.renorm(tl[..., 3:6]), BANDA + '_Normal.png'); L.save(tl[..., 6:9], BANDA + '_ORM.png')

# ============================================================================= MODELS
def cell_uv(rect, uv):
    return (rect[0] + (rect[2] - rect[0]) * uv[0], rect[1] + (rect[3] - rect[1]) * uv[1])

def card(mb, centre, nrm, up, size, rect, lobe_n, ao_c=1.0, ao_edge=1.0, aspect=1.0, bend=0.65, base_at_bottom=False):
    """One alpha card. up = the cell's +v direction in world. Corners get normals bent toward lobe_n."""
    nrm = Vector(nrm).normalized(); up = (Vector(up) - nrm * Vector(up).dot(nrm)).normalized(); rt = up.cross(nrm)
    hw, hh = size * 0.5 * aspect, size * 0.5
    c = Vector(centre) + (up * hh if base_at_bottom else Vector())
    pts = [c - rt * hw - up * hh, c + rt * hw - up * hh, c + rt * hw + up * hh, c - rt * hw + up * hh]
    uvs = [cell_uv(rect, q) for q in ((0, 0), (1, 0), (1, 1), (0, 1))]
    ns = []
    for p in pts:
        spread = (p - c).normalized() * 0.25
        ns.append((nrm * (1 - bend) + (Vector(lobe_n) + spread) * bend).normalized())
    ao = [ao_c * 0.9, ao_c * 0.9, ao_edge, ao_edge]
    mb.face(pts, uvs, ns, ao)

def tube(mb, pts, radii, sides, rect, outward=None, ao=(0.7, 0.9)):
    """Runner tube along pts, UV v mirrored along length (no seam in a single atlas cell)."""
    pts = [Vector(p) for p in pts]; n = len(pts)
    if n < 2: return
    tans = [(pts[min(k + 1, n - 1)] - pts[max(k - 1, 0)]).normalized() for k in range(n)]
    a = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    u = tans[0].cross(a).normalized(); rings = []; vs = [0.0]
    for k in range(1, n): vs.append(vs[-1] + (pts[k] - pts[k - 1]).length / 0.35)
    for k, p in enumerate(pts):
        t = tans[k]
        if k: u = (u - t * u.dot(t)).normalized()
        w = t.cross(u); ring = []
        for i in range(sides + 1):
            ang = 2 * math.pi * i / sides; d = u * math.cos(ang) + w * math.sin(ang)
            ring.append((p + d * radii[k], d, i / sides))
        rings.append(ring)
    def vv(x):
        f = x % 2.0; return f if f <= 1 else 2 - f
    for k in range(n - 1):
        for i in range(sides):
            A, B, C, D = rings[k][i], rings[k][i + 1], rings[k + 1][i + 1], rings[k + 1][i]
            cs = [A, B, C, D]; vk = [vs[k], vs[k], vs[k + 1], vs[k + 1]]
            uvs = [cell_uv(rect, (q[2], 0.02 + 0.96 * vv(vk[j]))) for j, q in enumerate(cs)]
            aos = [ao[0] + (ao[1] - ao[0]) * max(0.0, (q[1].dot(outward) if outward else 0.5)) for q in cs]
            mb.face([q[0] for q in cs], uvs, [q[1] for q in cs], aos)

class Wall:
    """The facade surface in 'unfolded' coordinates (x, w): w climbs the ground storey, crosses the jetty soffit outward
    and climbs the upper storey jettied `out` toward -Y. flat=True: a plain wall (patches)."""
    def __init__(self, jetty, out=0.45, beam=0.28, flat=False):
        self.flat = flat; self.jb = jetty - beam; self.out = out
    def at(self, x, w, off=0.0):
        """Point on the surface (+ off along its outward normal), the outward normal, and the 'up' direction."""
        if self.flat or w < self.jb:
            return Vector((x, -off, w)), Vector((0, -1, 0)), Vector((0, 0, 1))
        if w < self.jb + self.out:
            return Vector((x, -(w - self.jb), self.jb - off)), Vector((0, 0, -1)), Vector((0, -1, 0))
        return Vector((x, -self.out - off, w - self.out)), Vector((0, -1, 0)), Vector((0, 0, 1))

def grow_ivy(name, width, height, seed, wall, n_roots, fringe=True, tendrils=6, card_scale=1.0):
    """Runners random-walk up the wall and branch; leaf cards follow the runners where a lobed density field allows."""
    rr = random.Random(seed); mb = MB()
    top = wall.out + height if not wall.flat else height
    lobes = [(rr.uniform(0.1, width - 0.1), rr.uniform(0.2, top - 0.3), rr.uniform(0.35, 0.65) * min(1.0, width / 1.6 + 0.3)) for _ in range(max(4, int(width * top * 1.1)))]
    def topline(x):                                         # ragged upper edge: tallest at the corner (x 0)
        return top * (1.0 - 0.18 * (x / width) ** 1.3) - 0.35 * (0.5 + 0.5 * math.sin(x * 4.1 + seed))
    holes = [(rr.uniform(0.45, 0.85) * width, rr.uniform(0.3, 0.75) * top, rr.uniform(0.14, 0.24)) for _ in range(max(1, int(width * top / 3.5)))]
    def density(x, w):
        d = sum(math.exp(-((x - lx) ** 2 + (w - lw) ** 2) / (lr * lr)) for lx, lw, lr in lobes)
        d += 0.5 * math.exp(-w / 0.9) + 0.35 * math.exp(-x / 0.35)        # dense at the foot and in the corner
        for hx, hw_, hr in holes: d -= 1.2 * math.exp(-((x - hx) ** 2 + (w - hw_) ** 2) / (hr * hr))   # a few gaps of bare wall
        return max(0.0, min(1.0, d))
    runners = []
    heads = [(rr.uniform(0.05, width * 0.8) if i else 0.08, 0.0, math.radians(rr.uniform(-12, 12)), 0) for i in range(n_roots)]
    while heads:
        x, w, ang, order = heads.pop()
        path = [(x, w)]; step = 0.07
        for _ in range(400):
            ang += math.radians(rr.gauss(0, 9)) - ang * 0.04
            if x < 0.05: ang -= 0.08
            if x > width - 0.05: ang += 0.08
            x += math.sin(ang) * step * (-1); w += math.cos(ang) * step
            path.append((x, w))
            if w > topline(min(max(x, 0), width)) or x < -0.02 or x > width + 0.05: break
            if order < 3 and rr.random() < 0.055 / (1 + order) and len(heads) < 60:
                heads.append((x, w, ang + math.radians(rr.choice([-1, 1]) * rr.uniform(30, 60)), order + 1))
        if len(path) > 2: runners.append((path, order))
    # tubes
    for path, order in runners:
        if order > 0: continue                                     # only the trunks show (at the root); branches hide in the leaf mass
        r0 = (0.018 if order == 0 else 0.009) * (1.0 if width > 1.2 else 0.7)
        pts = []; radii = []; nlen = len(path)
        stride = 3
        sub = path[::stride] + ([path[-1]] if (nlen - 1) % stride else [])
        for k, (x, w) in enumerate(sub):
            p, nn, upv = wall.at(x, w, off=r0 + 0.004)
            pts.append(p); radii.append(max(0.003, r0 * (1 - 0.6 * k / max(1, len(sub)))))
        tube(mb, pts, radii, 4 if order == 0 else 3, C_STEM, outward=Vector((0, -1, 0)))
    # leaf cards (in-engine round 3 read sparse: now two layers): a shaded inner layer just off the wall fills the gaps, the
    # green outer layer stands off it in lobes; young yellow leaves only at the growing tips
    cards = 0
    def leaf(jx, jw, off, size, rect, inner=False):
        nonlocal cards
        jx = min(max(jx, 0.16 * card_scale), width)                       # no leaves in the air past the building corner
        p, nn, upv = wall.at(jx, max(jw, 0.0), off=off)
        lx, lw, lr = min(lobes, key=lambda l: ((jx - l[0]) ** 2 + (jw - l[1]) ** 2) / (l[2] * l[2]))
        bulge = (Vector((jx - lx, 0, 0)) + upv * (jw - lw)) / lr
        tilt = math.radians(rr.uniform(5, 20) if inner else rr.uniform(12, 35)); yaw = math.radians(rr.uniform(-25, 25))
        cn = Matrix.Rotation(yaw, 3, upv) @ (nn * math.cos(tilt) + upv * math.sin(tilt))
        cup = Matrix.Rotation(math.radians(rr.gauss(180, 55)), 3, cn) @ upv                 # leaf tips mostly droop
        lobe_n = (nn + upv * 0.3 + bulge * 0.75).normalized()                              # each lobe shades as one soft form
        ao_c = 0.45 if inner else 0.6 + 0.4 * min(1.0, off / 0.16)
        card(mb, p, cn, cup, size, rect, lobe_n, ao_c=ao_c, ao_edge=min(1.0, ao_c + 0.2))
        cards += 1
    for path, order in runners:
        acc = 0.0
        for k in range(1, len(path)):
            x, w = path[k]; acc += 0.07
            if acc < rr.uniform(0.05, 0.08) / max(card_scale, 0.6): continue
            acc = 0.0
            dn = density(min(max(x, 0), width), w)
            if rr.random() > dn * 1.3 + 0.15: continue
            if rr.random() < 0.7:
                leaf(x + rr.gauss(0, 0.06), w + rr.gauss(0, 0.06), rr.uniform(0.01, 0.035), rr.uniform(0.44, 0.6) * card_scale, IVY_DARK_CELL, inner=True)
            for rep in range(1 + int(dn > 0.55) + int(dn > 0.9)):
                leaf(x + rr.gauss(0, 0.08), w + rr.gauss(0, 0.08), rr.uniform(0.04, 0.08) + 0.12 * dn * rr.random(),
                     rr.uniform(0.34, 0.5) * card_scale, IVY_CELLS[rr.randrange(2)])
        x, w = path[-1]                                                     # a young yellow leaf or two at each growing tip
        for _ in range(2):
            p, nn, upv = wall.at(min(max(x + rr.gauss(0, 0.05), 0.1), width), w + rr.uniform(0, 0.08), off=rr.uniform(0.03, 0.08))
            card(mb, p, (nn + upv * 0.4).normalized(), upv, rr.uniform(0.11, 0.16) * card_scale, IVY_YOUNG_CELL, (nn + upv * 0.5).normalized(), ao_c=0.95)
            cards += 1
    # root mass: the ivy rises out of a dense clump at the foot instead of a ragged floating edge, and sprawls a little onto the ground
    for _ in range(int(width * 12)):
        leaf(rr.uniform(0.0, width), rr.uniform(0.0, 1.0) ** 1.8 * 0.7, rr.uniform(0.02, 0.14), rr.uniform(0.38, 0.54) * card_scale,
             IVY_DARK_CELL if rr.random() < 0.35 else IVY_CELLS[rr.randrange(2)], inner=False)
    for _ in range(int(width * 6)):
        x = rr.uniform(0.1, width - 0.05); p = Vector((x, -rr.uniform(0.06, 0.32) * card_scale, 0.03 + 0.03 * rr.random()))
        cn = Vector((rr.gauss(0, 0.2), -0.35, 1)).normalized()
        card(mb, p, cn, Vector((rr.gauss(0, 0.6), -1, 0)), rr.uniform(0.3, 0.42) * card_scale, IVY_CELLS[rr.randrange(2)], Vector((0, -0.3, 1)).normalized(), ao_c=0.6, ao_edge=0.9)
        cards += 1
    # hanging fringe from the jetty edge, and tendrils escaping at the top and the open side
    def strand(p0, direction, length, leaves, single=True):
        pts = [p0]; d = Vector(direction).normalized(); p = Vector(p0)
        for k in range(int(length / 0.06)):
            d = (d + Vector((rr.gauss(0, 0.12), rr.gauss(0, 0.06), rr.gauss(0, 0.08)))).normalized(); p = p + d * 0.06; pts.append(p.copy())
        tp = pts[::2] + ([pts[-1]] if len(pts) % 2 == 0 else [])
        tube(mb, tp, [max(0.0025, 0.006 * (1 - k / len(tp))) for k in range(len(tp))], 3, C_STEM)
        nonlocal cards
        for k in range(1, len(pts), 2):
            if rr.random() > leaves: continue
            fac = Vector((rr.uniform(-0.3, 0.3), -1, rr.uniform(0.0, 0.4))).normalized()
            rect = (IVY_YOUNG_CELL if rr.random() < 0.15 else IVY_SINGLES[rr.randrange(2)]) if single else IVY_CELLS[rr.randrange(2)]
            sz = rr.uniform(0.13, 0.19) if single else rr.uniform(0.22, 0.32)
            card(mb, pts[k], fac, Vector((rr.gauss(0, 0.4), 0, -1)), sz, rect, (fac + Vector((0, 0, 0.5))).normalized(), ao_c=0.85)
            cards += 1
    if fringe and not wall.flat:
        for i in range(int(width * 6)):
            x = rr.uniform(0.0, width * 0.9) ** 1.2 / max(width * 0.9, 1e-3) ** 0.2
            if rr.random() > density(x, wall.jb) + 0.2: continue
            strand(Vector((x, -wall.out + 0.03, wall.jb - 0.01)), (rr.gauss(0, 0.2), -0.15, -1), rr.uniform(0.25, 0.9), 0.85, single=rr.random() < 0.5)
    ends = sorted(runners, key=lambda r: -r[0][-1][1])[:tendrils]
    for path, order in ends:
        x, w = path[-1]; p, nn, upv = wall.at(x, w, off=0.02)
        strand(p, (rr.gauss(0, 0.4), -0.6, 0.8), rr.uniform(0.3, 0.55), 1.0)
    print(name, 'runners', len(runners), 'cards', cards, 'tris', mb.tris())
    return mb

MB = L.MB

def weed_clumps(mat):
    """Five crack-weed clumps from the adopted weed / nettle meshes (decimated, UVs packed into their atlas quadrant)."""
    weeds = {o.name.replace('_LOD0', ''): o for o in import_gltf(WEED_SRC)}
    nettles = {o.name.replace('_LOD0', ''): o for o in import_gltf(NETTLE_SRC)}
    for o in list(weeds.values()) + list(nettles.values()):
        o.data.uv_layers[0].name = 'UVMap'
        q = Q_WEED if o.name.startswith('weed') else Q_NETTLE
        uv = np.zeros(len(o.data.loops) * 2, np.float32); o.data.uv_layers[0].data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
        uv[:, 0] = q[0] + uv[:, 0] * 0.5; uv[:, 1] = q[1] + uv[:, 1] * 0.5
        o.data.uv_layers[0].data.foreach_set('uv', uv.ravel())
        o.matrix_world = Matrix.Identity(4)
    WEED_SCALE = 1.35          # storybook: crack weeds big enough to read at the facade foot from the mid camera
    SQUASH = 0.6               # weeds grow IN the joints: the leaf parts are a run along local x, narrow across (y)
    # name: ([(source, (x, y), yaw deg, scale)], tris, (blades, h min, h max, run length, run width), size factor)
    # A is the one the corner builder scatters (joints + wall foot): in-engine round 3 found it too small, so 1.6x and fuller.
    spec = {
        'A': ([('weed_plant_02_e', (-0.17, 0.0), 90, 1.7), ('nettle_plant_small_a', (0.0, 0.01), 0, 2.2), ('weed_plant_02_d', (0.16, -0.01), 0, 1.6)], 400, (18, 0.16, 0.34, 0.5, 0.12), 1.6),
        'B': ([('nettle_plant_tall_a', (0, 0), 0, 2.0), ('weed_plant_02_c', (-0.15, 0.0), 90, 1.5), ('nettle_plant_small_b', (0.14, 0.0), 0, 2.2)], 400, (14, 0.28, 0.52, 0.4, 0.1), 1.45),
        'C': ([('nettle_plant_medium_a', (-0.08, 0), 0, 2.1), ('nettle_plant_medium_b', (0.08, 0.0), 140, 2.0)], 360, (12, 0.08, 0.17, 0.18, 0.06), 0.8),
        'D': ([('weed_plant_02_a', (0, 0), 90, 1.6)], 380, (12, 0.12, 0.26, 0.3, 0.1), 1.15),
        'E': ([('weed_plant_02_b', (-0.08, 0), 90, 1.5), ('weed_plant_02_e', (0.14, 0.0), 0, 1.6)], 400, (20, 0.18, 0.4, 0.75, 0.14), 1.9),
    }
    out = []
    for key, (parts, target, bl, size) in spec.items():
        objs = []
        for src, (x, y), yaw, s in parts:
            x, y, s = x * WEED_SCALE * size, y * WEED_SCALE * size, s * WEED_SCALE * size
            o = (weeds.get(src) or nettles.get(src)).copy(); o.data = o.data.copy(); bpy.context.scene.collection.objects.link(o)
            o.matrix_world = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Scale(s, 4)
            o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)
            bm = bmesh.new(); bm.from_mesh(o.data); zmin = min(v.co.z for v in bm.verts)
            for v in bm.verts: v.co.z -= zmin
            bm.to_mesh(o.data); bm.free()
            objs.append(o)
        ob = objs[0]
        if len(objs) > 1:
            for o in bpy.context.view_layer.objects: o.select_set(False)
            for o in objs: o.select_set(True)
            bpy.context.view_layer.objects.active = ob; bpy.ops.object.join()
        ob.name = f'SC_B9_CrackWeed_{key}'; ob.data.name = ob.name
        ob.data.transform(Matrix.Diagonal((1.0, SQUASH, 1.0, 1.0)))
        bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0004); bm.to_mesh(ob.data); bm.free()
        t0 = L.tri_count(ob)
        bmb = blades(random.Random(ord(key)), *bl)                         # grass blades through the clump (fuller, reads at mid)
        leaf_target = max(120, target - bmb.tris())
        if t0 > leaf_target:
            m = ob.modifiers.new('dec', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = leaf_target / t0; m.use_collapse_triangulate = True
            bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='dec')
        bo = L.build(ob.name + '_blades', bmb, [ob.data.materials[0]] if ob.data.materials else [])
        for o in bpy.context.view_layer.objects: o.select_set(False)
        ob.select_set(True); bo.select_set(True); bpy.context.view_layer.objects.active = ob; bpy.ops.object.join()
        bend_normals(ob, 0.55)
        ob.data.materials.clear(); ob.data.materials.append(mat)
        print(ob.name, 'tris', t0, '->', L.tri_count(ob))
        out.append(ob)
    for o in list(weeds.values()) + list(nettles.values()): bpy.data.objects.remove(o)
    return out

def blades(rr, n, hmin, hmax, run, across, segs=3):
    """n tapered ribbon blades rooted along a joint run (length `run` on local x, `across` wide), leaning out of the joint."""
    mb = MB()
    for b in range(n):
        base = Vector((rr.uniform(-run / 2, run / 2), rr.gauss(0, across / 4), 0))
        a = rr.uniform(0, 2 * math.pi); outv = Vector((math.cos(a) * 0.5, math.sin(a), 0)).normalized()
        lean = rr.uniform(0.2, 0.75); hgt = rr.uniform(hmin, hmax); wid = rr.uniform(0.012, 0.02) * (hgt / 0.3) ** 0.5
        side = outv.cross(Vector((0, 0, 1))).normalized(); rect = C_GRASS if rr.random() < 0.6 else C_GRASS2
        prev = None
        for k in range(segs + 1):
            t = k / segs
            p = base + outv * (lean * hgt * t * t * 0.9) + Vector((0, 0, hgt * (t - 0.28 * lean * t * t)))
            hw = wid * (1 - t ** 1.6) + 0.0015; Lp, Rp = p - side * hw, p + side * hw
            nn = (outv * (0.3 + 0.5 * t) + Vector((0, 0, 1))).normalized(); ao = 0.45 + 0.55 * t
            if prev:
                pl, pr, pn, pa, pt = prev
                mb.face([pl, pr, Rp, Lp], [cell_uv(rect, (0, pt)), cell_uv(rect, (1, pt)), cell_uv(rect, (1, t)), cell_uv(rect, (0, t))], [pn, pn, nn, nn], [pa, pa, ao, ao])
            prev = (Lp, Rp, nn, ao, t)
    return mb

def bend_normals(ob, bend, up_bias=0.6):
    """Corner normals = face normal (flipped to face up/out) blended with the clump direction (up + out from the centre)."""
    me = ob.data; me.update()
    ctr = Vector((0, 0, 0)); zmax = max(v.co.z for v in me.vertices) or 1.0
    ns = []; aos = []
    for p in me.polygons:
        fn = p.normal.copy()
        for li in p.loop_indices:
            v = me.vertices[me.loops[li].vertex_index].co
            out = Vector((v.x - ctr.x, v.y - ctr.y, 0)); out = out.normalized() if out.length > 1e-4 else Vector()
            cl = (out + Vector((0, 0, up_bias + 0.6))).normalized()
            f = fn if fn.dot(cl) >= 0 else -fn
            ns.append(tuple((f * (1 - bend) + cl * bend).normalized()))
            aos.append(0.55 + 0.45 * min(1.0, v.z / zmax + 0.3 * out.length))
    for p in me.polygons: p.use_smooth = True
    me.normals_split_custom_set(ns)
    col = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    a = np.array(aos, np.float32); col.data.foreach_set('color', np.stack([a, a, a, np.ones_like(a)], -1).ravel())

def moss_tuft(name, seed, w, d, h):
    """A lumpy cushion (moss tile projected from above) with a ragged fringe skirt leaning out at its foot."""
    rr = random.Random(seed); mb = MB(); N = 12; R = 4
    ring = []
    for j in range(R + 1):
        t = j / R; row = []
        for i in range(N):
            a = 2 * math.pi * i / N + (0.13 * j if j else 0)
            rad = (1 - 0.85 * t ** 1.6) * (1 + 0.18 * math.sin(a * 3 + seed) + 0.1 * math.sin(a * 5 + seed * 2))
            x, y = math.cos(a) * w * 0.5 * rad, math.sin(a) * d * 0.5 * rad
            z = h * math.sin(t * math.pi / 2) ** 0.8 * (1 + 0.25 * noise.noise(Vector((x * 9, y * 9, seed))))
            row.append(Vector((x, y, z)))
        ring.append(row)
    top = Vector((0, 0, h * 1.05))
    def uvp(p):
        return cell_uv(C_MOSS, (0.5 + p.x / 0.6, 0.5 + p.y / 0.6))
    def nrm_at(p):
        return (Vector((p.x / (w * 0.5) ** 2, p.y / (d * 0.5) ** 2, max(p.z, 0.004) / h ** 2 * 1.6))).normalized()
    for j in range(R):
        for i in range(N):
            q = [ring[j][i], ring[j][(i + 1) % N], ring[j + 1][(i + 1) % N], ring[j + 1][i]]
            mb.face(q, [uvp(p) for p in q], [nrm_at(p) for p in q], [0.6 + 0.4 * min(1, p.z / h) for p in q])
    for i in range(N):
        q = [ring[R][i], ring[R][(i + 1) % N], top]
        mb.face(q, [uvp(p) for p in q], [nrm_at(p) for p in q], [1, 1, 1])
    for i in range(N):                                       # fringe skirt
        a = 2 * math.pi * (i + rr.random() * 0.5) / N
        p = Vector((math.cos(a) * w * 0.48, math.sin(a) * d * 0.48, 0.0))
        outv = Vector((math.cos(a), math.sin(a), 0))
        cn = (outv + Vector((0, 0, 0.9))).normalized()
        upv = (Vector((0, 0, 1)) + outv * 0.6).normalized()
        card(mb, p + Vector((0, 0, -0.005)), cn, upv, rr.uniform(0.06, 0.10), C_FRINGE, (outv * 0.6 + Vector((0, 0, 1))).normalized(),
             ao_c=0.6, ao_edge=0.95, aspect=rr.uniform(1.4, 2.0), base_at_bottom=True)
    print(name, 'tris', mb.tris()); return mb

def grass_clump(name, seed, n, hmin, hmax, spread):
    """Ribbon blades fanned from a small base: tapered, curved, a slight twist; normals bent toward the clump's up-and-out."""
    rr = random.Random(seed); mb = MB(); segs = 4
    for b in range(n):
        a = rr.uniform(0, 2 * math.pi); r0 = rr.uniform(0, spread * 0.35)
        base = Vector((math.cos(a) * r0, math.sin(a) * r0, 0))
        outv = Vector((math.cos(a), math.sin(a), 0))
        lean = rr.uniform(0.15, 0.7); hgt = rr.uniform(hmin, hmax); wid = rr.uniform(0.016, 0.026) * (hgt / 0.35) ** 0.5
        side = outv.cross(Vector((0, 0, 1))).normalized(); twist = rr.uniform(-0.6, 0.6)
        rect = C_GRASS if rr.random() < 0.6 else C_GRASS2
        prev = None
        for s in range(segs + 1):
            t = s / segs
            p = base + outv * (lean * hgt * t * t * 0.9) + Vector((0, 0, hgt * (t - 0.28 * lean * t * t)))
            sd = Matrix.Rotation(twist * t, 3, Vector((0, 0, 1))) @ side
            hw = wid * (1 - t ** 1.6) + 0.0015
            L_, R_ = p - sd * hw, p + sd * hw
            nn = (outv * (0.3 + 0.5 * t) + Vector((0, 0, 1))).normalized()
            ao = 0.45 + 0.55 * t
            if prev:
                pl, pr, pn, pa, pt = prev
                mb.face([pl, pr, R_, L_], [cell_uv(rect, (0, pt)), cell_uv(rect, (1, pt)), cell_uv(rect, (1, t)), cell_uv(rect, (0, t))], [pn, pn, nn, nn], [pa, pa, ao, ao])
            prev = (L_, R_, nn, ao, t)
    print(name, 'tris', mb.tris()); return mb

def moss_decal(name):
    """Band on the basin wall (r 1.7, centred on -Y, 70 deg) plus a narrow ground lip; UV u along the arc, v up."""
    mb = MB(); R = 1.705; A = math.radians(70); segs = 18; H = 0.38; lip = 0.07
    for i in range(segs):
        a0 = -math.pi / 2 - A / 2 + A * i / segs; a1 = a0 + A / segs
        u0, u1 = i / segs, (i + 1) / segs
        p = lambda a, r, z: Vector((math.cos(a) * r, math.sin(a) * r, z))
        n0 = Vector((math.cos(a0), math.sin(a0), 0)); n1 = Vector((math.cos(a1), math.sin(a1), 0))
        # wall band: v 0.18..1 maps 0..H (the lower 18% of the texture runs onto the ground lip)
        mb.face([p(a0, R, 0.0), p(a1, R, 0.0), p(a1, R, H), p(a0, R, H)], [(u0, 0.18), (u1, 0.18), (u1, 1.0), (u0, 1.0)], [n0, n1, n1, n0], [0.8, 0.8, 1, 1])
        up = Vector((0, 0, 1))
        mb.face([p(a0, R + lip, 0.004), p(a1, R + lip, 0.004), p(a1, R, 0.004), p(a0, R, 0.004)], [(u0, 0.0), (u1, 0.0), (u1, 0.18), (u0, 0.18)], [up] * 4, [0.9] * 4)
    print(name, 'tris', mb.tris()); return mb

def moss_band_a(name):
    """Low moss at the basin foot (round 3 in-engine: the north side is hidden from every camera): a 110 deg arc centred on the
    south-west (local -x -y = Unity west / south), 0.16 m up the wall and a 0.3 m ground strip whose moss runs out along the
    joints. UV u along the arc (4 moss tiles), v 0-0.55 ground (outer edge to the wall), 0.55-1 wall."""
    mb = MB(); R = 1.705; A = math.radians(110); c = math.radians(-135); segs = 26; H = 0.16; lip = 0.30
    p = lambda a, r, z: Vector((math.cos(a) * r, math.sin(a) * r, z))
    for i in range(segs):
        a0 = c - A / 2 + A * i / segs; a1 = a0 + A / segs; u0, u1 = i / segs, (i + 1) / segs
        n0 = Vector((math.cos(a0), math.sin(a0), 0)); n1 = Vector((math.cos(a1), math.sin(a1), 0)); up = Vector((0, 0, 1))
        mb.face([p(a0, R, 0.0), p(a1, R, 0.0), p(a1, R, H), p(a0, R, H)], [(u0, 0.55), (u1, 0.55), (u1, 1.0), (u0, 1.0)], [n0, n1, n1, n0], [0.75, 0.75, 1, 1])
        mb.face([p(a0, R + lip, 0.004), p(a1, R + lip, 0.004), p(a1, R, 0.004), p(a0, R, 0.004)], [(u0, 0.0), (u1, 0.0), (u1, 0.55), (u0, 0.55)], [up] * 4, [0.9, 0.9, 0.8, 0.8])
    print(name, 'tris', mb.tris()); return mb

def basin_moss(name):
    """A moss cushion lying on the basin rim (0.9 x 0.5 m, 2.5 cm proud, ragged alpha edge), domed and draping at its long edges.
    Origin = its centre on the rim top (the builder's BasinMoss row puts it on the rim, the side away from the sun)."""
    mb = MB(); NX, NY = 12, 7; W, D, H = 0.9, 0.5, 0.025
    def pt(i, j):
        u, v = i / NX, j / NY; x, y = (u - 0.5) * W, (v - 0.5) * D
        r = min(1.0, math.hypot(x / (W * 0.5), y / (D * 0.5)))
        z = H * (1 - r * r) + 0.004 * noise.noise(Vector((x * 14, y * 14, 3.0))) - 0.02 * max(0.0, abs(y) / (D * 0.5) - 0.7) / 0.3
        return Vector((x, y, z)), (u, v)
    def nrm(p):
        return Vector((-p.x * 0.4, -p.y * 0.4, 1.0)).normalized()
    for i in range(NX):
        for j in range(NY):
            q = [pt(i, j), pt(i + 1, j), pt(i + 1, j + 1), pt(i, j + 1)]
            mb.face([a[0] for a in q], [a[1] for a in q], [nrm(a[0]) for a in q], [0.7 + 0.3 * min(1, a[0].z / H) for a in q])
    print(name, 'tris', mb.tris()); return mb

def build_models():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    mat = L.pbr_material('M_B9_Foliage', ATLAS)
    dmat = L.pbr_material('M_B9_MossDecal', DECAL); rmat = L.pbr_material('M_B9_BasinMoss', RIM); amat = L.pbr_material('M_B9_MossBandA', BANDA)
    groups = {}
    wall = Wall(JETTY)
    # ivy pivots: bottom centre of the mass, back on the wall plane (y 0), growing toward -Y; the dense corner side is local -x
    for key, args, kw, w in (('SC_B9_IvyWall', ('IvyWall', 2.1, 5.0, 7, wall, 4), {}, 2.1),
                             ('SC_B9_IvyPatchA', ('IvyPatchA', 1.0, 1.4, 17, Wall(0, flat=True), 2), dict(fringe=False, tendrils=3, card_scale=0.85), 1.0),
                             ('SC_B9_IvyPatchB', ('IvyPatchB', 0.7, 0.6, 29, Wall(0, flat=True), 2), dict(fringe=False, tendrils=2, card_scale=0.75), 0.7)):
        groups[key] = L.build(key, grow_ivy(*args, **kw), [mat]); groups[key].data.transform(Matrix.Translation((-w / 2, 0, 0)))
    for ob in weed_clumps(mat): groups[ob.name] = ob
    for k, (seed, w, d, h) in zip('ABC', [(3, 0.30, 0.22, 0.07), (5, 0.20, 0.16, 0.05), (8, 0.42, 0.26, 0.08)]):
        groups[f'SC_B9_MossTuft_{k}'] = L.build(f'SC_B9_MossTuft_{k}', moss_tuft(k, seed, w, d, h), [mat])
    for k, (seed, n, h0, h1, sp) in zip('ABC', [(2, 22, 0.22, 0.42, 0.10), (4, 16, 0.16, 0.30, 0.07), (6, 28, 0.25, 0.50, 0.14)]):
        groups[f'SC_B9_GrassClump_{k}'] = L.build(f'SC_B9_GrassClump_{k}', grass_clump(k, seed, n, h0, h1, sp), [mat])
    groups['SC_B9_MossDecal'] = L.build('SC_B9_MossDecal', moss_decal('MossDecal'), [dmat])
    groups['SC_B9_BasinMoss'] = L.build('SC_B9_BasinMoss', basin_moss('BasinMoss'), [rmat])
    groups['SC_B9_MossBandA'] = L.build('SC_B9_MossBandA', moss_band_a('MossBandA'), [amat])
    report = {k: L.tri_count(o) for k, o in groups.items()}
    print('TRIS', report)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'b9_foliage.blend'))
    for k, o in groups.items():
        L.export_fbx([o], os.path.join(OUT, 'fbx', k + '.fbx'))
    return groups

# ============================================================================= REVIEW RENDERS
# The corner builder's rows (BeautyCornerTable.cs on main, 2026-10-30): (Unity position, yaw). Extra dressing it has no row for yet
# (tufts, grass, patches, the band) is laid where it would go and listed in SOURCES.md as suggested rows.
BUILDER = {
    'SC_B9_IvyWall': [((-4.0, 0, 7.5), 0)],
    'SC_B9_CrackWeed_A': [((-4.4, 0, 7.3), 0), ((-3.0, 0, 7.35), 40), ((-1.9, 0, 7.3), 80), ((0.2, 0, 7.35), 10), ((1.6, 0, 7.3), 120), ((2.3, 0, 7.3), 60),
                          ((2.75, 0, 0.8), 30), ((-2.4, 0, -1.7), 100), ((1.2, 0, -2.65), 160), ((-2.8, 0, 0.9), 70)],
    'SC_B9_BasinMoss': [((-0.9, 0.55, 1.45), -32)],
    'SC_B8_Stall_Lantern': [((3.58, 0.9, 4.3), 0)],
    'SC_B8_Lantern_Bracket': [((0.0, 2.45, 7.5), 0)],
}
EXTRA = {
    'SC_B9_IvyPatchA': [((1.6, 0.6, 7.5), 0)], 'SC_B9_IvyPatchB': [((-2.6, 0.0, 7.5), 0)],
    'SC_B9_MossTuft_A': [((-1.3, 0, -1.15), 20)], 'SC_B9_MossTuft_C': [((-1.65, 0, -0.45), 100)], 'SC_B9_MossTuft_B': [((-0.65, 0, -1.62), 0)],
    'SC_B9_MossBandA': [((0, 0, 0), 0)],           # yaw 0 = the south-west foot (the old Moss_Band stays exported, unplaced here)
    'SC_B9_GrassClump_A': [((-4.75, 0, 7.2), 0)], 'SC_B9_GrassClump_C': [((-4.3, 0, 7.15), 0)], 'SC_B9_GrassClump_B': [((3.3, 0, 3.7), 0)],
    'SC_B9_CrackWeed_B': [((-3.6, 0, 7.32), 0)], 'SC_B9_CrackWeed_C': [((-0.9, 0, 7.32), 0), ((2.2, 0, -1.9), 50)],
    'SC_B9_CrackWeed_D': [((1.0, 0, 7.3), 0), ((-2.9, 0, -0.3), 90)], 'SC_B9_CrackWeed_E': [((-2.2, 0, 7.3), 0), ((0.2, 0, -2.95), 170)],
}

def inst_u(src, pos, yaw):
    """Place an asset authored in the builder convention (local -Y faces Unity -Z) at a Unity position / yaw in the review scene."""
    o = src.copy(); bpy.context.scene.collection.objects.link(o); o.hide_render = False
    o.matrix_world = Matrix.Translation(L.u2b(*pos)) @ Matrix.Rotation(math.pi - math.radians(yaw), 4, 'Z')
    return o

def place(groups):
    for table in (BUILDER, EXTRA):
        for name, spots in table.items():
            if name in groups:
                for pos, yaw in spots: inst_u(groups[name], pos, yaw)
    for o in groups.values(): o.hide_render = True

def studio(groups, path):
    """All B9 pieces side by side on a grey stand (three-quarter), for the contact sheet."""
    sc = bpy.context.scene
    x = 0.0; row = []
    order = ['SC_B9_CrackWeed_A', 'SC_B9_CrackWeed_B', 'SC_B9_CrackWeed_C', 'SC_B9_CrackWeed_D', 'SC_B9_CrackWeed_E',
             'SC_B9_MossTuft_A', 'SC_B9_MossTuft_B', 'SC_B9_MossTuft_C', 'SC_B9_GrassClump_A', 'SC_B9_GrassClump_B', 'SC_B9_GrassClump_C']
    for name in order:
        o = groups[name].copy(); sc.collection.objects.link(o); o.hide_render = False
        o.location = (x, 30.0, 0); row.append(o); x += 0.6
    cam = bpy.data.objects.new('StudioCam', bpy.data.cameras.new('StudioCam')); sc.collection.objects.link(cam)
    cam.data.lens = 50; mid = Vector((x / 2 - 0.27, 30.0, 0.12))
    cam.location = mid + Vector((0.0, -6.4, 2.4)); cam.rotation_euler = (mid - cam.location).to_track_quat('-Z', 'Y').to_euler()
    L.render(cam, path, RRES, SAMPLES)
    for o in row: bpy.data.objects.remove(o)

def review(groups):
    for o in list(bpy.data.objects):
        if o.name not in groups: bpy.data.objects.remove(o)
    L.review_scene(jetty=JETTY)
    place(groups)
    rdir = os.path.join(OUT, 'review'); os.makedirs(rdir, exist_ok=True)
    lant = os.path.join(OUT, '..', 'lanterns', 'b8_lanterns.blend')      # sc_lanterns_adopt.py output, if run with the same parent dir
    if os.path.exists(lant):
        with bpy.data.libraries.load(lant) as (src, dst):
            dst.objects = [n for n in src.objects if n.startswith('SC_B8_')]
        for o in dst.objects:
            for pos, yaw in BUILDER.get(o.name, []): inst_u(o, pos, yaw)
    for k, (eye, at) in L.CAMS.items():
        L.render(L.camera(k, eye, at), os.path.join(rdir, f'b9_{k}.png'), RRES, SAMPLES)
    # ivy close-up from the mid camera's side, and the studio row
    L.render(L.camera('ivy', (-2.4, 1.7, 3.2), (-4.0, 2.4, 7.3), fov_y=50), os.path.join(rdir, 'b9_ivy.png'), RRES, SAMPLES)
    L.render(L.camera('base', (0.6, 0.9, -2.6), (-0.3, 0.2, -1.6), fov_y=45), os.path.join(rdir, 'b9_basin.png'), RRES, SAMPLES)
    studio(groups, os.path.join(rdir, 'b9_studio.png'))
    print('REVIEW', rdir)

# Asset folder per model (BeautyCornerArt matches folder names: exact alias first, then contains, ties to the first alphabetically).
# Ivy_Facade wins the Ivy row over Ivy_Patch_*; Weed_A is the clump the CrackWeeds row scatters; Basin_Moss is exact for BasinMoss.
FOLDERS = {
    'SC_B9_IvyWall': 'Ivy_Facade', 'SC_B9_IvyPatchA': 'Ivy_Patch_A', 'SC_B9_IvyPatchB': 'Ivy_Patch_B',
    'SC_B9_CrackWeed_A': 'Weed_A', 'SC_B9_CrackWeed_B': 'Weed_B', 'SC_B9_CrackWeed_C': 'Weed_C', 'SC_B9_CrackWeed_D': 'Weed_D', 'SC_B9_CrackWeed_E': 'Weed_E',
    'SC_B9_MossTuft_A': 'Moss_Tuft_A', 'SC_B9_MossTuft_B': 'Moss_Tuft_B', 'SC_B9_MossTuft_C': 'Moss_Tuft_C',
    'SC_B9_GrassClump_A': 'Grass_A', 'SC_B9_GrassClump_B': 'Grass_B', 'SC_B9_GrassClump_C': 'Grass_C',
    'SC_B9_MossDecal': 'Moss_Band', 'SC_B9_BasinMoss': 'Basin_Moss', 'SC_B9_MossBandA': 'Moss_Band_A',
}

def do_export(groups):
    """Each folder: its FBX + the texture set it uses (identical atlas copies dedupe in git and LFS)."""
    import shutil
    for k, o in groups.items():
        d = os.path.join(EXP, FOLDERS[k]); os.makedirs(d, exist_ok=True)
        L.export_fbx([o], os.path.join(d, k + '.fbx'))
        src = {'SC_B9_MossDecal': DECAL, 'SC_B9_BasinMoss': RIM, 'SC_B9_MossBandA': BANDA}.get(k, ATLAS)
        for m in ('BaseColor', 'Normal', 'ORM'):
            shutil.copy(f'{src}_{m}.png', os.path.join(d, f'{os.path.basename(src)}_{m}.png'))
    print('EXPORT', EXP)

if __name__ == '__main__':
    bpy.ops.wm.read_homefile(use_empty=True)
    if STAGE in ('all', 'atlas'): build_atlas()
    if STAGE in ('all', 'models', 'render'):
        g = build_models()
        if EXPORT: do_export(g)
        if STAGE in ('all', 'render'): review(g)
