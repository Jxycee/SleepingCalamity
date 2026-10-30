"""Lever 2: FULL scan materials (albedo, GL normal, roughness) projected onto the UV-less high in part
space, then carried to the game low by the normal Cycles selected-to-active bake (the high is the
"UV'd intermediate": its projection is resolved per shading point and baked into the low's UVs).
- mode 'cyl': lathe parts. u = angle * K / 2pi (K integer, so the wrap seam tiles), v = z / tile.
  Tangent frame (-sin a, cos a, 0) / (0, 0, 1); faces with |n.z| > ~0.7 blend to a planar Z projection.
- mode 'tri': everything else. Three planar projections, each GL normal swizzled into part space with
  the sign of the facing axis, blended with |n|^4 weights (UDN: geometry normal + weighted detail).
Albedo is graded to `tint` by ratio (scan / scan mean * tint), so the scan's own variation survives."""
import os, math
import bpy
import numpy as np
import sc_aaa_kit as K

_MEAN = {}

def available(asset):
    return bool(K.SCAN_DIR) and os.path.exists(os.path.join(K.SCAN_DIR, f'{asset}_nor_gl_2k.jpg'))

def _img(asset, kind):
    path = os.path.join(K.SCAN_DIR, f'{asset}_{kind}_2k.jpg')
    img = bpy.data.images.load(os.path.abspath(path), check_existing=True)
    img.colorspace_settings.name = 'sRGB' if kind == 'diff' else 'Non-Color'
    return img

def _mean(asset):
    if asset not in _MEAN:
        img = _img(asset, 'diff'); w, h = img.size
        px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px)
        x = px.reshape(-1, 4)[:, :3]                          # 8-bit images report the sRGB-encoded values
        _MEAN[asset] = np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4).mean(0)
    return _MEAN[asset]

def _tex(T, img, uv):
    nd = T.node('ShaderNodeTexImage'); nd.image = img; nd.interpolation = 'Cubic'
    T.set(nd, 'Vector', uv); return (nd, 'Color')

def _nrm(T, col):
    """GL normal map colour -> tangent vector (x, y, z) in -1..1."""
    v = T.vmath('SUBTRACT', T.vmath('MULTIPLY', col, (2.0, 2.0, 2.0)), (1.0, 1.0, 1.0))
    return T.sep(v)

def scan_full(T, asset, tile=0.2, mode='tri', tint=(0.5, 0.5, 0.5), sat=1.0, rough=(0.45, 0.85), nstr=1.0, p=None, base_n=None, grain_x=False, contrast=0.0):
    """Returns (colour, roughness, normal) sockets. p: part-space position (default lp); projection
    weights and frames use the part-space normal 'ln' (matches p for parts rotated after store_lp);
    base_n: the normal to perturb (default the shading normal). grain_x: wood, grain along part X."""
    p = p or T.attr('lp')
    N = T.geo('Normal'); ns = T.sep(T.attr('ln'))
    imgs = {k: _img(asset, k) for k in ('diff', 'rough', 'nor_gl')}
    m = _mean(asset); gain = tuple(float(t / max(mm, 1e-4)) for t, mm in zip(tint, m))
    ps = T.sep(p)
    projs = []                                   # (weight, uv, tangent-u vector, tangent-v vector)
    if mode == 'cyl':
        ang = T.math('ARCTAN2', (ps, 'Y'), (ps, 'X'))
        rr = T.vmath('LENGTH', T.comb((ps, 'X'), (ps, 'Y'), 0.0))
        K_ = max(1, round(2 * math.pi * 0.08 / tile))  # integer tiles around ~0.08 m radius
        u = T.math('MULTIPLY', ang, K_ / (2 * math.pi)); v = T.math('DIVIDE', (ps, 'Z'), tile)
        wz = T.smoothstep(T.math('ABSOLUTE', (ns, 'Z')), 0.6, 0.85)
        wc = T.math('SUBTRACT', 1.0, wz)
        tu = T.comb(T.math('MULTIPLY', T.math('SINE', ang), -1.0), T.math('COSINE', ang), 0.0)
        projs.append((wc, T.comb(u, v, 0.0), tu, (0.0, 0.0, 1.0)))
        sz = T.math('SIGN', (ns, 'Z'))
        projs.append((wz, T.comb(T.math('DIVIDE', T.math('MULTIPLY', (ps, 'X'), sz), tile), T.math('DIVIDE', (ps, 'Y'), tile), 0.0),
                      T.comb(sz, 0.0, 0.0), (0.0, 1.0, 0.0)))
    else:
        ax = [T.math('POWER', T.math('ABSOLUTE', (ns, c)), 4.0) for c in 'XYZ']
        tot = T.math('ADD', T.math('ADD', ax[0], ax[1]), ax[2])
        w = [T.math('DIVIDE', a, tot) for a in ax]
        sx, sy, sz = (T.math('SIGN', (ns, c)) for c in 'XYZ')
        d = lambda a: T.math('DIVIDE', a, tile)
        projs.append((w[0], T.comb(d(T.math('MULTIPLY', (ps, 'Y'), sx)), d((ps, 'Z')), 0.0), T.comb(0.0, sx, 0.0), (0.0, 0.0, 1.0)))
        if grain_x:                                    # image v (the scan's grain) runs along part X on long faces
            projs.append((w[1], T.comb(d(T.math('MULTIPLY', (ps, 'Z'), sy)), d((ps, 'X')), 0.0), T.comb(0.0, 0.0, sy), (1.0, 0.0, 0.0)))
            projs.append((w[2], T.comb(d(T.math('MULTIPLY', (ps, 'Y'), T.math('MULTIPLY', sz, -1.0))), d((ps, 'X')), 0.0), T.comb(0.0, T.math('MULTIPLY', sz, -1.0), 0.0), (1.0, 0.0, 0.0)))
        else:
            projs.append((w[1], T.comb(d(T.math('MULTIPLY', (ps, 'X'), T.math('MULTIPLY', sy, -1.0))), d((ps, 'Z')), 0.0), T.comb(T.math('MULTIPLY', sy, -1.0), 0.0, 0.0), (0.0, 0.0, 1.0)))
            projs.append((w[2], T.comb(d(T.math('MULTIPLY', (ps, 'X'), sz)), d((ps, 'Y')), 0.0), T.comb(sz, 0.0, 0.0), (0.0, 1.0, 0.0)))
    col = None; rgh = None; det = None
    for wgt, uv, tu, tv in projs:
        c = _tex(T, imgs['diff'], uv)
        r = (T.sep(_tex(T, imgs['rough'], uv)), 'X')
        t = _nrm(T, _tex(T, imgs['nor_gl'], uv))
        dd = T.vmath('ADD', _scale(T, tu, (t, 'X')), _scale(T, tv, (t, 'Y')))
        cw = _scale(T, c, wgt); rw = T.math('MULTIPLY', r, wgt); dw = _scale(T, dd, wgt)
        col = cw if col is None else T.vmath('ADD', col, cw)
        rgh = rw if rgh is None else T.math('ADD', rgh, rw)
        det = dw if det is None else T.vmath('ADD', det, dw)
    col = T.vmath('MULTIPLY', col, gain)
    if contrast:                                    # the graded scan is low-contrast at prop scale: stretch it
        bc = T.node('ShaderNodeBrightContrast'); T.set(bc, 'Color', col); bc.inputs['Contrast'].default_value = contrast; col = (bc, 'Color')
    if sat != 1.0: col = T.hsv(col, 0.5, sat, 1.0)
    rgh = T.maprange(rgh, 0.0, 1.0, rough[0], rough[1])
    nrm = T.vmath('NORMALIZE', T.vmath('ADD', base_n or N, _scale(T, det, nstr)))
    return col, rgh, nrm

def _scale(T, vec, s):
    nd = T.node('ShaderNodeVectorMath', operation='SCALE')
    T.set(nd, 0, vec); T.set(nd, 'Scale', s)
    return (nd, 'Vector')

# generic rollout (AAA_FULL=1): materials that call use_scan(asset) with an asset listed here get the full
# scan through NT.output: albedo x (scan / scan mean)^amt (keeps the material's hue and storytelling layers),
# roughness offset by the scan's, scan normal over the procedural bump at `bump` x its strength
FULL = {'rock_surface': dict(tile=0.15, mode='tri', rough=(-0.2, 0.2), nstr=1.5, amt=1.0, bump=0.3, contrast=0.35),
        'clay_plaster': dict(tile=0.3, mode='tri', rough=(-0.2, 0.2), nstr=1.5, amt=1.0, bump=0.35, contrast=0.3),
        'rough_wood':   dict(tile=0.6, mode='tri', rough=(-0.2, 0.2), nstr=1.0, amt=0.8, bump=0.4, grain_x=True)}

def apply_output(T, color, rough, normal):
    f = dict(T.full); amt = f.pop('amt', 1.0); f.pop('bump', None)
    sc, sr, sn = scan_full(T, tint=(1.0, 1.0, 1.0), base_n=normal, **f)
    color = T.mix(amt, color, T.vmath('MULTIPLY', color, sc))
    rough = T.math('ADD', rough, T.math('MULTIPLY', sr, amt))
    return color, rough, sn
