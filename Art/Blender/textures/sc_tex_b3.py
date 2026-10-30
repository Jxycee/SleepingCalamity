"""sc_tex_b3.py (M8 part 1): batch B3, trim sheets (one per area) and placed-wear decals. Called by sc_tex_batches.py (--batch B3|all).
Trim sheets (2048 x 2048, 8 strips of 256 px, each strip 1024 px/m so 25 cm tall and 2.0 m long, tileable along u) are built from the graded B1/B2 tileables
(wood, stone, iron, brick) with profiled heights (chamfer, bead, ogee cornice, cyma cope, torus string, plinth step, brick corbel, strapwork with rivets), so a beam,
a sill and a cope share one texture set with the walls they sit on. Edge wear (convex arrises lighter), cavity AO and a gutter of 12 px per strip edge
(no mip bleed). Decals (1024 x 1024 RGBA albedo with alpha, normal, orm): moss, grime, drip, crack, all procedural (periodic or edge-faded noise), seeded.
Output: Art/Textures/B3_trims/<trim_town|trim_cellar|moss|grime|drip|crack>/<name>_{albedo,normal,orm}.png and Art/Textures/B3_trims/SOURCES.md.
"""
import math, os, json
import numpy as np
from PIL import Image, ImageDraw
import sc_texgrade as G

STRIP, GUT, W = 256, 12, 2048
PX_MM = 0.98    # 1024 px per metre: 1 px is about 1 mm


# ---------------------------------------------------------------- profiles: height in mm across the strip's inner height (v from 0 bottom to 1 top)
def prof_chamfer_board(v, depth=14.0, ch=16.0):
    x = v * 232.0
    d = np.minimum(x, 232.0 - x)
    return depth * np.clip(d / ch, 0, 1)

def prof_bead(v):
    x = (v - 0.5) * 2
    return 20.0 * np.sqrt(np.clip(1 - x * x, 0, 1))

def prof_ogee(v):
    # cornice: cove at the bottom flowing into an ogee and a flat fillet at the top
    return 8.0 + 26.0 * (np.sin(np.clip(v, 0, 1) * np.pi * 0.9 - 0.35) * 0.5 + 0.5) * (1 - 0.3 * np.cos(v * 6.28)) * np.clip(1.15 - v * 0.4, 0, 1)

def prof_cyma(v):
    s = np.clip(v, 0, 1)
    return 6.0 + 30.0 * (0.5 - 0.5 * np.cos(s * np.pi)) ** 1.6 * (1 - 0.25 * s) + 6.0 * np.clip((s - 0.82) / 0.18, 0, 1) ** 2

def prof_torus(v):
    x = (v - 0.5) * 2
    return 6.0 + 30.0 * np.sqrt(np.clip(1 - np.abs(x) ** 2.2, 0, 1))

def prof_plinth(v):
    # two steps and a chamfered top: a plinth or base moulding
    s = np.clip(v, 0, 1)
    return np.where(s < 0.45, 8.0, np.where(s < 0.78, 24.0, 32.0 - 20.0 * np.clip((s - 0.78) / 0.22, 0, 1)))

def prof_corbel(v):
    # brick corbel course: three brick heights stepping out
    s = np.clip(v, 0, 1)
    return np.where(s < 0.33, 8.0, np.where(s < 0.66, 20.0, 32.0)) - 3.0 * np.abs(((s * 3) % 1.0) - 0.5)

def prof_strap(v):
    x = (v - 0.5) * 2
    return 7.0 * (1 - np.clip((np.abs(x) - 0.82) / 0.18, 0, 1) ** 2) * np.where(np.abs(x) < 1, 1.0, 0.0)

def prof_band(v):
    x = (v - 0.5) * 2
    return 9.0 * np.clip(1 - np.abs(x) ** 3.0, 0, 1)


TOWN = [  # (name, profile, material, rotate fill?, kind, rivets)
    ('timber_chamfer', prof_chamfer_board, 'oak_timber', True, 'wood', False),
    ('timber_bead',    prof_bead,          'oak_timber', True, 'wood', False),
    ('timber_cornice', prof_ogee,          'oak_timber', True, 'wood', False),
    ('stone_cope',     prof_cyma,          'sandstone',  False, 'stone', False),
    ('stone_string',   prof_torus,         'sandstone',  False, 'stone', False),
    ('stone_plinth',   prof_plinth,        'sandstone',  False, 'stone', False),
    ('iron_strap',     prof_strap,         'iron',       False, 'iron', True),
    ('iron_band',      prof_band,          'iron',       False, 'iron', True),
]
CELLAR = [
    ('timber_beam',    prof_chamfer_board, 'barrel_oak', True, 'wood', False),
    ('timber_batten',  prof_bead,          'barrel_oak', True, 'wood', False),
    ('stone_cope',     prof_cyma,          'vault_stone', False, 'stone', False),
    ('stone_string',   prof_torus,         'vault_stone', False, 'stone', False),
    ('stone_plinth',   prof_plinth,        'vault_stone', False, 'stone', False),
    ('brick_corbel',   prof_corbel,        'old_brick',  False, 'stone', False),
    ('iron_strap',     prof_strap,         'iron',       False, 'iron', True),
    ('iron_band',      prof_band,          'iron',       False, 'iron', True),
]


def load_set(tex_root, batch_dir, name):
    d = os.path.join(tex_root, batch_dir, name)
    a = G.load_srgb(os.path.join(d, f'{name}_albedo.png'), W)
    n = G.load_srgb(os.path.join(d, f'{name}_normal.png'), W)
    o = G.load_srgb(os.path.join(d, f'{name}_orm.png'), W)
    return a, n, o


def build_trim(entries, tex_root, sources, seed):
    alb = np.zeros((W, W, 3), np.float32); nrm = np.zeros((W, W, 3), np.float32); orm = np.zeros((W, W, 3), np.float32)
    cache = {}
    inner = STRIP - 2 * GUT
    rng = np.random.default_rng(seed)
    for i, (name, prof, mat, rot, kind, rivets) in enumerate(entries):
        bd = 'B1_town' if mat in sources['B1'] else 'B2_cellar'
        if mat not in cache:
            cache[mat] = load_set(tex_root, bd, mat)
        a, n, o = cache[mat]
        y0 = (i * 311) % (W - STRIP)       # a different crop per strip so neighbours differ
        sl = slice(y0, y0 + STRIP)
        fa, fn, fo = a[sl], n[sl], o[sl]
        if rot:                                       # run the grain along the strip: take a W x STRIP window of the transposed image
            at = a.transpose(1, 0, 2); nt = n.transpose(1, 0, 2); ot = o.transpose(1, 0, 2)
            fa, fn, fo = at[sl], nt[sl], ot[sl]
            fn = fn.copy(); fn[..., [0, 1]] = fn[..., [1, 0]]     # swap x/y of the tangent normal for the 90 degree turn
        v = np.clip((np.arange(STRIP) - GUT + 0.5) / inner, 0.0, 1.0)
        hprof = prof(v).astype(np.float32)[:, None] * np.ones((1, W), np.float32)     # mm == px
        if rivets:
            hprof = hprof.copy()
            yy, xx = np.mgrid[0:STRIP, 0:W].astype(np.float32)
            cy = STRIP / 2.0
            for k in range(16):
                cx = (k + 0.5) * W / 16.0 + rng.uniform(-3, 3)
                d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
                hprof += 9.0 * np.sqrt(np.clip(1 - (d / 14.0) ** 2, 0, 1)) * (d < 14.0)
        # edge wear: convex arrises (height above its local mean) lighten and smooth; cavities darken
        hb = G.blur(hprof, 6.0)
        convex = np.clip(hprof - hb, 0, None); concave = np.clip(hb - hprof, 0, None)
        wear = G.smoothstep(convex, 0.5, 2.5) * (0.6 + 0.4 * G.fbm_periodic(W, 40 + i, beta=2.0, kmin=16)[:STRIP])
        cav = G.smoothstep(concave, 0.4, 3.5)
        lin = G.srgb2lin(fa)
        lift = {'wood': 1.35, 'stone': 1.22, 'iron': 1.9}[kind]
        lin = lin * (1 + (lift - 1) * wear[..., None]) * (1 - 0.35 * cav[..., None])
        sa = G.lin2srgb(lin)
        sn = G.mix_normals(G.scale_normal(fn, 0.7), G.normal_from_height(hprof, 1.0))
        ao = np.clip(1 - 0.55 * cav, 0, 1) * fo[..., 0]
        rough = np.clip(fo[..., 1] - 0.18 * wear, 0.05, 1)
        metal = fo[..., 2]
        # gutter: replicate the inner strip's edge rows into the 12 px margins so mips never bleed a neighbour
        for arr in (sa, sn):
            arr[:GUT] = arr[GUT]; arr[STRIP - GUT:] = arr[STRIP - GUT - 1]
        for arr in (ao, rough, metal):
            arr[:GUT] = arr[GUT]; arr[STRIP - GUT:] = arr[STRIP - GUT - 1]
        r0 = i * STRIP
        alb[r0:r0 + STRIP] = sa; nrm[r0:r0 + STRIP] = sn
        orm[r0:r0 + STRIP] = np.stack([ao, rough, metal], -1)
    return alb, nrm, orm


# ---------------------------------------------------------------- decals (1024)
D = 1024

def window(res, margin=0.12):
    x = np.linspace(-1, 1, res, dtype=np.float32)
    wx = G.smoothstep(1 - np.abs(x), 0.0, margin)
    return wx[None, :] * wx[:, None]

def decal_moss():
    n = D
    low = G.fbm_periodic(n, 101, beta=3.0, kmax=7)
    mask = G.smoothstep(low * window(n, 0.3) + 0.35 * window(n, 0.3), 0.36, 0.62)
    clump = G.fbm_periodic(n, 102, beta=1.4, kmin=24, kmax=260)
    fine = G.fbm_periodic(n, 103, beta=1.0, kmin=60, kmax=420)
    h = (clump * 0.7 + fine * 0.3)
    a = np.clip(mask * (0.75 + 1.4 * (h - 0.4)), 0, 1)
    g1, g2 = G.srgb2lin(np.array([0x61, 0x6e, 0x2e]) / 255.0), G.srgb2lin(np.array([0xb6, 0xab, 0x28]) / 255.0)
    t = G.smoothstep(G.fbm_periodic(n, 104, beta=2.0, kmin=4, kmax=40), 0.58, 0.78)[..., None] * 0.7
    lin = (g1[None, None] * (1 - t) + g2[None, None] * t) * (0.45 + 1.1 * (h[..., None] - 0.3).clip(0, 1) + 0.25)
    alb = np.dstack([G.lin2srgb(lin), a])
    nrm = G.normal_from_height(h * 9.0, 1.0)
    orm = np.stack([np.clip(0.4 + h * 0.8, 0, 1), np.full((n, n), 0.92, np.float32), np.zeros((n, n), np.float32)], -1)
    return alb, nrm, orm

def decal_grime():
    n = D
    rng = np.random.default_rng(201)
    w = rng.standard_normal((n, n)).astype(np.float32)
    # streaks: blur hard along v (down), a little along u
    F = np.fft.rfft2(w); fy = np.fft.fftfreq(n)[:, None]; fx = np.fft.rfftfreq(n)[None, :]
    k = np.exp(-2 * np.pi ** 2 * ((70.0 * fy) ** 2 + (2.2 * fx) ** 2)).astype(np.float32)
    s = np.fft.irfft2(F * k, s=(n, n)); s = (s - s.mean()) / (s.std() + 1e-6)
    top = np.linspace(1.0, 0.25, n, dtype=np.float32)[:, None]
    a = np.clip(G.smoothstep(s, -0.6, 1.4) * (0.35 + 0.65 * top), 0, 1) * window(n, 0.2) * 0.7
    mott = G.fbm_periodic(n, 202, beta=2.2, kmin=6, kmax=120)
    col = G.srgb2lin(np.array([0x2c, 0x25, 0x1e]) / 255.0)[None, None] * (0.7 + 0.8 * mott[..., None])
    alb = np.dstack([G.lin2srgb(col), a])
    nrm = np.dstack([np.full((n, n), 0.5, np.float32)] * 2 + [np.ones((n, n), np.float32)])
    orm = np.stack([np.ones((n, n), np.float32), np.full((n, n), 0.8, np.float32), np.zeros((n, n), np.float32)], -1)
    return alb, nrm, orm

def decal_drip():
    n = D
    rng = np.random.default_rng(301)
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    a = np.zeros((n, n), np.float32); core = np.zeros((n, n), np.float32)
    for k in range(7):
        cx = rng.uniform(0.12, 0.88) * n; wd = rng.uniform(12, 30); ln = rng.uniform(0.45, 0.95) * n
        wob = np.sin(yy / rng.uniform(60, 140) + rng.uniform(0, 6)) * rng.uniform(1.5, 4)
        t = np.clip(yy / ln, 0, 1)
        half = wd * (1 - t ** 3) * 0.5 + 1.2 * (1 - t) * 0   # tapers to a point, with a small bulb at the tip
        bulb = 5.0 * np.exp(-((yy - ln) / 14.0) ** 2) * (yy < ln + 18)
        half = np.where(yy <= ln, half + bulb, 0.0)
        m = G.smoothstep(half - np.abs(xx - cx - wob), -1.0, 1.5) * (yy <= ln + 16)
        a = np.maximum(a, m * (0.75 - 0.35 * t))
        core = np.maximum(core, G.smoothstep(half * 0.45 - np.abs(xx - cx - wob), -1.0, 1.0) * (yy <= ln))
    top_stain = G.smoothstep(1 - yy / n, 0.88, 1.0) * G.fbm_periodic(n, 302, beta=2.0, kmin=4, kmax=60)
    a = np.clip(np.maximum(a, top_stain * 0.5), 0, 1)
    col = G.srgb2lin(np.array([0x3a, 0x31, 0x28]) / 255.0)[None, None] * (1 - 0.35 * core[..., None])
    alb = np.dstack([G.lin2srgb(col), a * window(n, 0.04)])
    nrm = G.normal_from_height(G.blur(a, 1.2) * 4.0, 1.0)
    orm = np.stack([np.ones((n, n), np.float32), np.clip(0.8 - 0.55 * core, 0.2, 1), np.zeros((n, n), np.float32)], -1)
    return alb, nrm, orm

def decal_crack():
    n = D; S = 2
    rng = np.random.default_rng(401)
    img = Image.new('L', (n * S, n * S), 0); dr = ImageDraw.Draw(img)

    def walk(x, y, ang, length, width, depth):
        pts = [(x, y)]
        steps = int(length / 6)
        for i in range(steps):
            ang += rng.normal(0, 0.14) + (math.pi / 2 - ang) * 0.05
            x += math.cos(ang) * 6; y += math.sin(ang) * 6
            pts.append((x, y))
            w = max(1.0, width * (1 - i / steps) ** 0.8)
            dr.line([pts[-2], pts[-1]], fill=255, width=int(round(w * S)))
            if depth < 3 and rng.random() < 0.05 and i > 8:
                walk(x, y, ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.0), length * rng.uniform(0.25, 0.5), width * 0.6, depth + 1)
    walk(n * 0.5 + rng.uniform(-40, 40), 30, math.pi / 2, n * 0.95, 7.0, 0)
    c = np.asarray(img.resize((n, n), Image.LANCZOS), np.float32) / 255.0
    h = -(G.blur(c, 1.2)) * 8.0 + G.blur(c, 6.0) * 2.5           # groove with a slightly raised broken lip
    halo = G.blur(c, 9.0)
    a = np.clip(np.maximum(c * 0.95, halo * 0.35), 0, 1) * window(n, 0.03)
    col = G.srgb2lin(np.array([0x1c, 0x18, 0x15]) / 255.0)[None, None] * (1.0 + 0.8 * (1 - c[..., None]))
    alb = np.dstack([G.lin2srgb(col), a])
    nrm = G.normal_from_height(h, 1.0)
    orm = np.stack([np.clip(1 - 0.7 * c, 0, 1), np.full((n, n), 0.9, np.float32), np.zeros((n, n), np.float32)], -1)
    return alb, nrm, orm


DECALS = {'moss': decal_moss, 'grime': decal_grime, 'drip': decal_drip, 'crack': decal_crack}


def run(args):
    out = os.path.join(args.out, 'B3_trims')
    os.makedirs(out, exist_ok=True)
    src = {'B1': {'oak_timber', 'sandstone', 'iron'}, 'B2': {'barrel_oak', 'vault_stone', 'old_brick'}}
    rows = []
    for name, entries, seed in (('trim_town', TOWN, 11), ('trim_cellar', CELLAR, 12)):
        alb, nrm, orm = build_trim(entries, args.out, src, seed)
        d = os.path.join(out, name); os.makedirs(d, exist_ok=True)
        G.save_rgb(alb, os.path.join(d, f'{name}_albedo.png')); G.save_rgb(nrm, os.path.join(d, f'{name}_normal.png')); G.save_rgb(orm, os.path.join(d, f'{name}_orm.png'))
        rows.append((name, 'trim sheet 2048x2048, 8 strips x 256 px (25 cm), 1024 px/m, tileable along u: ' + ', '.join(f'{i}:{e[0]}' for i, e in enumerate(entries)), alb))
        print('built', name)
    for name, fn in DECALS.items():
        alb, nrm, orm = fn()
        d = os.path.join(out, name); os.makedirs(d, exist_ok=True)
        G.save_rgba(alb, os.path.join(d, f'{name}_albedo.png')); G.save_rgb(nrm, os.path.join(d, f'{name}_normal.png')); G.save_rgb(orm, os.path.join(d, f'{name}_orm.png'))
        rows.append((name, 'decal 1024x1024 RGBA (alpha = opacity), edge-faded, 1024 px/m (1 m square): placed wear over the tileables', alb[..., :3]))
        print('built decal', name)
    lines = ['# B3 sources and record', '',
             'Trim sheets are built from the graded B1/B2 tileables (oak_timber, sandstone, iron; barrel_oak, vault_stone, old_brick; see their SOURCES.md for the CC0 Poly Haven scans and licences) plus procedural profiles.',
             'Decals (moss, grime, drip, crack) are fully procedural (no scan): `Art/Blender/textures/sc_tex_b3.py`, seeded, deterministic. No external source or licence applies to them.', '',
             '| set | what |', '|---|---|'] + [f'| {n} | {w} |' for n, w, _ in rows]
    open(os.path.join(out, 'SOURCES.md'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    if args.qa:
        for n, _, a in rows:
            Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).resize((1024, 1024)).save(os.path.join(args.qa, f'B3_{n}.jpg'), quality=85)
