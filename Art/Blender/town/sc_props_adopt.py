"""B7 props (beauty corner): free Poly Haven props restyled into the Hollowmere look, plus generated burlap sacks and produce.

Tier T2/T3 prop family (PHASE1 4, batch B7), method Adopt (sc-asset-adopt) + Gen (sacks, produce). One file, one headless
session, one bake per asset. Each adopted source is imported from Art/Source/PolyHaven/<src>/ (CC0, rows in its SOURCES.md),
then made ours:
  - about 10% oversize (PHASE1 3.4) and chunkier: a slight bilge bulge, a 1-2 deg lean about the low base edge, low-frequency
    wobble so nothing runs dead straight, iron hoops and bands pushed proud (the source ARM metallic channel finds them);
  - albedo regraded per region in numpy (wood and iron separately, so the iron never turns oak-brown): warm oak, no chroma
    boost on wood, iron a warm dark grey; one painted accent (the long crate's faded ochre band);
  - placed wear in the high's shader (object space, so it lands where hands, water and feet go): rust blooming on the hoops at
    the foot, rims and edges polished lighter and glossier at hand height, a dirt splash fading up from the cobbles; a Cycles
    bevel in the normal gives every play-distance edge a visible chamfer read;
  - decimated to the prop budget (audit class 'prop': 6k tris, 2 slots), bottom faces deleted (never seen), smart-UV'd low,
    one Cycles selected-to-active bake: BaseColor, Normal (OpenGL), ORM (R occlusion, G roughness, B metallic).
Visible-faces rule (docs/art/beauty-corner-brief.md 2): spend goes to tops and fronts; nothing special is done for backs.

refs: Reference/hollowmere/market_square/REFS.md (weekly-market barrels, crates, baskets, sacks); style bible docs/PHASE1.md 3.
Axes: authored in Blender axes (z up, front -Y, metres), origin at the bottom centre; FBX export -Z forward, Y up, so the
front is +z in Unity. Leans rotate about the low base edge, which stays at z 0.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_props_adopt.py -- --out <scratch>
      [--only Barrel_A,Sack_C] [--res-scale 0.5]  preview bakes at half the final texture size
      [--export]                write FBX + maps + SOURCES.md to Assets/_Project/Content/Art/Town/B7/<Asset>/ (final bake)
      [--no-render] [--rres 900] [--samples 16] [--cpu]
      [--render-only]           skip building: load <out>/<Asset>/<Asset>.fbx + maps and render the review set
      [--sources-only]          rewrite the exported SOURCES.md files from <out>/<Asset>/<Asset>.fbx (no bake)
  Batch run used on the PC (one process per asset, so a Cycles crash costs one asset; then one render pass):
      for a in <assets>; do blender ... -- --out <scratch> --only $a --no-render [--export]; done
      blender ... -- --out <scratch> --render-only
Outputs in <scratch>: <Asset>/ (maps, fbx), renders/<Asset>_tq.png, renders/group.png. Sheet: sc_sheet.py on the renders.

Assets (folder name, matched by BeautyCornerBuilder: source -> what changes; tex size):
  Barrel_A       wooden_barrels_01 barrel01 -> lean, bulge, proud thickened iron hoops          2048
  Barrel_B       wooden_barrels_01 barrel02 -> squatter (0.92 h), stronger bulge                2048
  Barrel_C       wine_barrel_01 (lid, bung, base) -> lighter oak                                2048
  Crate_A        wooden_crate_01 (lid, latch)                                                   2048
  Crate_B        wooden_crate_02 (lid) -> faded ochre painted band                              2048
  Crate_C        wooden_crate_01 body + latch, lid leaned on its side, apples, turnips, squash  2048
  Bucket_A       wooden_bucket_01 (rope-hung handle)                                            1024
  Bucket_B       wooden_bucket_02 (lug handles)                                                 1024
  Basket_Produce wicker_basket_01 + heaped apples, turnips, squash and a loaf (stall goods)     1024
  Basket_Lidded  wicker_basket_02 base + lid                                                    1024
  Basket_Bread   wicker_basket_02 base, lid off, two loaves                                     1024
  Jug_A          jug_01 -> brown slip glaze over the shoulder, bare clay foot                   1024
  Jug_B          ceramic_vase_02 -> squat salt-glaze stoneware crock, brown glaze lip           1024
  Sack_A         generated: upright grain sack, gathered neck tied with rope                    2048
  Sack_B         generated: tied sack slumped sideways, neck flopped over                       2048
  Sack_C         generated: open sack, rolled cuff, grain showing                               2048
  Sack_Pair      generated: a tied sack slumped against a slumped one (sacks slump together)    2048
Wear on wood (every barrel, crate, bucket): board-to-board tone, grime packed in the joints, up-facing lids and chimes
sun-bleached silver-grey, a dark splash at the cobble line, rust blooming on the iron at the foot, polished rims.
Budget per asset (PHASE1 3.3, audit class prop): at most 6000 tris, 1 material slot.

Status (2026-10-30, PC): final bake exported. Batch contact-sheet self-review, rubric as written, visible faces only:
round 1 26/40 (wear too faint at mid distance) -> 2: 27 (darker foot grime, rust bloom, polish, muted wood chroma) ->
3: 28.5 (stall goods with squash, slumped sack pair, cluster layout, sun-bleached tops, joint grime, board tone) ->
4: 29.5 (crate boards bow and sag, weathered darker crates, damp sack feet, crate_02's stencil letters painted out).
Target was round 1 + 5 = 31; stopped at the 4-round cap (round 4 gained 1). Extra ideas in docs/PARKING_LOT.md.
Known quirk: Blender 5.2 segfaulted in uv.pack_islands with locked pins (Crate_C); the generated islands now pack inside their own
box (ORIGINAL_AABB) instead, and the per-asset batch loop still retries once.
"""
import sys, os, math, glob, random, zlib, shutil
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, noise

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
ONLY = arg('--only', '').split(',') if arg('--only') else None
RES_SCALE = float(arg('--res-scale', '1.0')); RRES = int(arg('--rres', '900')); SAMPLES = int(arg('--samples', '16'))
EXPORT = '--export' in argv; RENDER = '--no-render' not in argv; CPU = '--cpu' in argv; RENDER_ONLY = '--render-only' in argv or '--sources-only' in argv
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
SRC = os.path.join(ROOT, 'Art', 'Source', 'PolyHaven')
TEX = os.path.join(ROOT, 'Art', 'Textures', 'B1_town')
DEST = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B7')
SEED = 7007
OVERSIZE = 1.10

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode()))

# ---------------------------------------------------------------- asset table
WOOD = dict(hue_keep=0.55, delight=0.5, contrast=1.15, chroma=0.88)
WEAR_WOOD = dict(tone=0.12, grime=0.75, bleach=0.55)
WEAR_WICKER = dict(tone=0.08, grime=0.5, bleach=0.25)
WEAR_CLAY = dict(tone=0.0, grime=0.4, bleach=0.0)
IRON = '#4a4542'
ASSETS = {   # folder names follow the BeautyCornerBuilder's name matching (barrel, crate, sack, bucket, basket, jug)
    'Barrel_A':  dict(src='wooden_barrels_01', parts=['wooden_barrels_01_barrel01'], rings=True, res=2048, tris=5200, wood='#664834',
                      lean=1.6, bulge=0.03, hoop=0.006, wob=0.004, dirt=0.13, polish=(0.80, 1.0), **WEAR_WOOD),
    'Barrel_B':  dict(src='wooden_barrels_01', parts=['wooden_barrels_01_barrel02'], rings=True, res=2048, tris=5200, wood='#5e4230',
                      lean=-1.2, bulge=0.05, squash=0.92, hoop=0.006, wob=0.004, dirt=0.13, polish=(0.80, 1.0), **WEAR_WOOD),
    'Barrel_C':  dict(src='wine_barrel_01', parts=None, rings=True, res=2048, tris=5200, wood='#74543a',
                      lean=1.0, bulge=0.02, hoop=0.005, wob=0.004, dirt=0.12, polish=(0.80, 1.0), **WEAR_WOOD),
    'Crate_A':   dict(src='wooden_crate_01', parts=None, res=2048, tris=5000, wood='#6c5038',
                      lean=0.8, bulge=0.03, sag=0.02, hoop=0.004, wob=0.008, dirt=0.10, polish=(0.75, 1.0), **WEAR_WOOD),
    'Crate_B':   dict(src='wooden_crate_02', parts=None, res=2048, tris=5000, wood='#694e36',
                      lean=-0.7, bulge=0.03, sag=0.02, hoop=0.004, wob=0.008, dirt=0.10, polish=(0.75, 1.0), paint=('#b38a40', 0.30, 0.62), **WEAR_WOOD,
                      erase=((0.02, 0.15, 0.15, 0.29), (0.235, 0.15, 0.37, 0.29))),
    'Crate_C':   dict(src='wooden_crate_01', parts=['wooden_crate_01', 'wooden_crate_01_latch'], res=2048, tris=5800, wood='#725539',
                      lean=0.0, bulge=0.03, sag=0.015, hoop=0.004, wob=0.008, dirt=0.10, polish=(0.75, 1.0), leaned_lid=True, **WEAR_WOOD,
                      produce=dict(kind='crate', n_apple=11, n_turnip=5, n_loaf=0, n_squash=3, max_h=0.09)),
    'Bucket_A':  dict(src='wooden_bucket_01', parts=None, rings=True, res=1024, tris=4000, wood='#6c4e36',
                      lean=1.5, bulge=0.025, hoop=0.004, wob=0.003, dirt=0.08, polish=(0.70, 1.6), **WEAR_WOOD),
    'Bucket_B':  dict(src='wooden_bucket_02', parts=None, rings=True, res=1024, tris=4500, wood='#664a34',
                      lean=-1.0, bulge=0.02, hoop=0.004, wob=0.003, dirt=0.08, polish=(0.75, 1.0), **WEAR_WOOD),
    'Basket_Produce': dict(src='wicker_basket_01', parts=None, res=1024, tris=5800, wood='#a07a48', basket=True,
                      lean=0.0, bulge=0.02, hoop=0.0, wob=0.003, dirt=0.05, polish=(0.85, 1.0), **WEAR_WICKER,
                      produce=dict(kind='basket', n_apple=5, n_turnip=2, n_loaf=0, n_squash=1, max_h=0.07)),
    'Basket_Lidded': dict(src='wicker_basket_02', parts=None, res=1024, tris=5800, wood='#9c7646', basket=True,
                      lean=1.0, bulge=0.03, hoop=0.0, wob=0.003, dirt=0.05, polish=(0.80, 1.0), **WEAR_WICKER),
    'Basket_Bread': dict(src='wicker_basket_02', parts=['wicker_basket_02_base'], res=1024, tris=5000, wood='#a47c4a', basket=True,
                      lean=-1.0, bulge=0.03, hoop=0.0, wob=0.003, dirt=0.05, polish=(0.80, 1.0), **WEAR_WICKER,
                      produce=dict(kind='bread', n_apple=0, n_turnip=0, n_loaf=2, n_squash=0)),
    'Jug_A':     dict(src='jug_01', parts=None, res=1024, tris=3000, clay='#a8714a', glaze='#5a3420', glaze_from=0.45,
                      lean=1.5, bulge=0.04, hoop=0.0, wob=0.002, dirt=0.04, polish=(0.85, 1.0), **WEAR_CLAY),
    'Jug_B':     dict(src='ceramic_vase_02', parts=None, res=1024, tris=3000, clay='#a89676', glaze='#6a4026', glaze_from=0.70,
                      lean=-1.2, bulge=0.08, squash=0.82, hoop=0.0, wob=0.002, dirt=0.05, polish=(0.85, 1.0), **WEAR_CLAY),   # a stoneware crock
    'Sack_A':    dict(gen='sack', variant='tied', res=2048, tris=4000, dirt=0.16),
    'Sack_B':    dict(gen='sack', variant='slumped', res=2048, tris=4000, dirt=0.16),
    'Sack_C':    dict(gen='sack', variant='open', res=2048, tris=5000, dirt=0.16),
    'Sack_Pair': dict(gen='sack', variant='pair', res=2048, tris=6000, dirt=0.16),   # a tied sack slumped against a slumped one
}

# ---------------------------------------------------------------- colour helpers (numpy; same maths as sc_texgrade, no Pillow)
def srgb2lin(x):
    x = np.asarray(x, dtype=np.float32)
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4).astype(np.float32)

def lin2srgb(x):
    x = np.clip(np.asarray(x, dtype=np.float32), 0.0, 1.0)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * x ** (1 / 2.4) - 0.055).astype(np.float32)

def hex2lin(h):
    h = h.lstrip('#'); return srgb2lin(np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32))

def lum(lin):
    return lin[..., 0] * 0.2126 + lin[..., 1] * 0.7152 + lin[..., 2] * 0.0722

def blur(a, sigma):
    H, W = a.shape
    fy = np.fft.fftfreq(H)[:, None]; fx = np.fft.fftfreq(W)[None, :]
    g = np.exp(-2 * (math.pi * sigma) ** 2 * (fx * fx + fy * fy))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g)).astype(np.float32)

def grade_region(lin, w, tgt_hex, hue_keep, delight, contrast, chroma):
    """grade_albedo (sc_texgrade) restricted to the pixels weighted by w: the scan's de-lit structure, re-pinned to the target mean."""
    L = lum(lin) + 1e-5
    ws = w.sum() + 1e-6
    if delight > 0:
        low = blur(L * w, 60) / np.maximum(blur(w, 60), 1e-3)
        f = np.clip(((L * w).sum() / ws / np.maximum(low, 1e-4)) ** delight, 0.6, 1.7)
        lin = lin * f[..., None]; L = lum(lin) + 1e-5
    Lm = (L * w).sum() / ws
    lin = lin * ((L / Lm) ** (contrast - 1.0))[..., None]; L = lum(lin) + 1e-5
    tgt = hex2lin(tgt_hex)
    mean = (lin * w[..., None]).reshape(-1, 3).sum(0) / ws
    gain = lin * (tgt / np.maximum(mean, 1e-5))
    paint = tgt[None, None, :] * (L / ((L * w).sum() / ws))[..., None]
    out = gain * hue_keep + paint * (1 - hue_keep)
    out = out * (tgt / np.maximum((out * w[..., None]).reshape(-1, 3).sum(0) / ws, 1e-5))
    Lo = lum(out)[..., None]
    return np.maximum(Lo + (out - Lo) * chroma, 0.0)

def img_np(path, colorspace='sRGB'):
    im = bpy.data.images.load(path, check_existing=True); im.colorspace_settings.name = colorspace
    a = np.empty(im.size[0] * im.size[1] * 4, np.float32); im.pixels.foreach_get(a)
    return im, a.reshape(im.size[1], im.size[0], 4)

def save_np(arr, path, colorspace='sRGB'):
    H, W = arr.shape[:2]
    if arr.shape[2] == 3: arr = np.dstack([arr, np.ones((H, W), np.float32)])
    name = os.path.basename(path)
    im = bpy.data.images.get(name) or bpy.data.images.new(name, W, H, alpha=False)
    if tuple(im.size) != (W, H): im.scale(W, H)
    im.colorspace_settings.name = colorspace
    im.pixels.foreach_set(np.ascontiguousarray(arr, np.float32).ravel())
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    return im

# ---------------------------------------------------------------- node helpers
class NT:
    def __init__(s, mat):
        s.nt = mat.node_tree; s.n = s.nt.nodes; s.l = s.nt.links; s.x = 0
    def new(s, t, **kw):
        nd = s.n.new(t); nd.location = (s.x, 0); s.x += 40
        for k, v in kw.items(): setattr(nd, k, v)
        return nd
    def link(s, a, b): s.l.new(a, b)
    def val(s, v):
        nd = s.new('ShaderNodeValue'); nd.outputs[0].default_value = v; return nd.outputs[0]
    def rgb(s, hexs):
        nd = s.new('ShaderNodeRGB'); c = hex2lin(hexs); nd.outputs[0].default_value = (*c, 1.0); return nd.outputs[0]
    def _in(s, sock, v):
        if isinstance(v, (int, float)): sock.default_value = v
        elif isinstance(v, tuple): sock.default_value = v
        else: s.link(v, sock)
    def math(s, op, a, b=0.0, clamp=False):
        nd = s.new('ShaderNodeMath', operation=op, use_clamp=clamp); s._in(nd.inputs[0], a); s._in(nd.inputs[1], b); return nd.outputs[0]
    def mrange(s, v, a, b, lo=0.0, hi=1.0, smooth=True):
        nd = s.new('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP' if smooth else 'LINEAR')
        s._in(nd.inputs['Value'], v); nd.inputs['From Min'].default_value = a; nd.inputs['From Max'].default_value = b
        nd.inputs['To Min'].default_value = lo; nd.inputs['To Max'].default_value = hi
        return nd.outputs['Result']
    def mixf(s, f, a, b):
        nd = s.new('ShaderNodeMix', data_type='FLOAT'); s._in(nd.inputs[0], f); s._in(nd.inputs[2], a); s._in(nd.inputs[3], b); return nd.outputs[0]
    def mixc(s, f, a, b, blend='MIX'):
        nd = s.new('ShaderNodeMix', data_type='RGBA', blend_type=blend); s._in(nd.inputs[0], f); s._in(nd.inputs[6], a); s._in(nd.inputs[7], b)
        return nd.outputs[2]
    def obj(s):
        if not hasattr(s, '_obj'): s._obj = s.new('ShaderNodeTexCoord').outputs['Object']
        return s._obj
    def z(s):
        if not hasattr(s, '_z'):
            sep = s.new('ShaderNodeSeparateXYZ'); s.link(s.obj(), sep.inputs[0]); s._z = sep.outputs['Z']
        return s._z
    def noise(s, scale, detail=3.0, rough=0.55, w=0.0, vec=None):
        nd = s.new('ShaderNodeTexNoise', noise_dimensions='4D'); s.link(vec or s.obj(), nd.inputs['Vector'])
        nd.inputs['Scale'].default_value = scale; nd.inputs['Detail'].default_value = detail
        nd.inputs['Roughness'].default_value = rough; nd.inputs['W'].default_value = w
        return nd.outputs['Fac']
    def image(s, path, cs, uv=None, box=None):
        nd = s.new('ShaderNodeTexImage'); nd.image = bpy.data.images.load(path, check_existing=True)
        nd.image.colorspace_settings.name = cs
        if box is not None:
            nd.projection = 'BOX'; nd.projection_blend = 0.25
            mp = s.new('ShaderNodeMapping'); s.link(s.obj(), mp.inputs['Vector']); mp.inputs['Scale'].default_value = (box, box, box)
            s.link(mp.outputs[0], nd.inputs['Vector'])
        return nd

def finish(t, col, rough, metal, ao, nrm, bevel=0.012):
    """Principled + the bake taps: an Emission node the baker rewires to any channel, the named sockets kept on the material."""
    bsdf = t.new('ShaderNodeBsdfPrincipled')
    t._in(bsdf.inputs['Base Color'], col); t._in(bsdf.inputs['Roughness'], rough); t._in(bsdf.inputs['Metallic'], metal)
    if bevel > 0:
        bv = t.new('ShaderNodeBevel', samples=8); bv.inputs['Radius'].default_value = bevel
        if nrm is not None: t.link(nrm, bv.inputs['Normal'])
        nrm = bv.outputs[0]
    if nrm is not None: t.link(nrm, bsdf.inputs['Normal'])
    out = t.new('ShaderNodeOutputMaterial'); t.link(bsdf.outputs[0], out.inputs['Surface'])
    em = t.new('ShaderNodeEmission'); em.name = 'BAKE_EMIT'
    taps = {}
    for k, v in (('col', col), ('rough', rough), ('metal', metal), ('ao', ao)):
        rr = t.new('NodeReroute'); rr.name = 'TAP_' + k
        if isinstance(v, (int, float)):
            vn = t.val(v); t.link(vn, rr.inputs[0])
        else: t.link(v, rr.inputs[0])
    bsdf.name = 'BSDF'; out.name = 'OUT'

def wear(t, col, rough, metal, H, p, metal_mask=None, ao=None):
    """Placed wear in object space: board-to-board tone, grime in the joints, sun-bleached tops, dirt splash at the foot,
    rust on iron near the foot, polished rims at hand height."""
    z = t.z(); n1 = t.noise(7.0, 4.0, 0.6, 1.3); n2 = t.noise(22.0, 3.0, 0.6, 4.1)
    wood = 1.0 if metal_mask is None else t.math('SUBTRACT', 1.0, metal_mask)
    if p.get('tone', 0) > 0:      # each board/stave a slightly different oak (coarse noise ~ board width)
        tn = t.mrange(t.noise(3.5, 1.0, 0.4, 5.5), 0.3, 0.7, 1 - p['tone'], 1 + p['tone'], smooth=False)
        col = t.mixc(wood, col, t.mixc(1.0, col, t.math('MULTIPLY', tn, 1.0), blend='MULTIPLY'))
    if ao is not None and p.get('grime', 0) > 0:   # dirt packed into the stave gaps and board joints (the scan's cavities)
        cav = t.math('MULTIPLY', t.mrange(ao, 0.85, 0.35), p['grime'])
        col = t.mixc(cav, col, t.rgb('#2b231c')); rough = t.mixf(cav, rough, 0.95)
    if p.get('bleach', 0) > 0:    # up-facing wood sun- and rain-bleached toward silver-grey (lids and chimes, what the cameras see)
        geo0 = t.new('ShaderNodeNewGeometry'); sep = t.new('ShaderNodeSeparateXYZ'); t.link(geo0.outputs['Normal'], sep.inputs[0])
        top = t.math('MULTIPLY', t.mrange(sep.outputs['Z'], 0.55, 0.92), t.mrange(t.noise(5.0, 4.0, 0.6, 7.7), 0.25, 0.6, 0.6, 1.0))
        top = t.math('MULTIPLY', t.math('MULTIPLY', top, wood), p['bleach'])
        grey = t.mixc(0.55, col, t.rgb('#9a9086'))
        col = t.mixc(top, col, grey); rough = t.mixf(top, rough, 0.88)
    zj = t.math('ADD', z, t.math('MULTIPLY', t.math('SUBTRACT', n1, 0.5), 0.09))
    dirt = t.math('MULTIPLY', t.mrange(zj, p['dirt'] * 1.3, 0.0), t.mrange(n2, 0.2, 0.6, 0.6, 1.0))
    col = t.mixc(t.math('MULTIPLY', dirt, 0.9), col, t.mixc(t.mrange(n2, 0.3, 0.7), t.rgb('#3a3129'), t.rgb('#5a4c3c')))
    rough = t.mixf(t.math('MULTIPLY', dirt, 0.8), rough, 0.95)
    if metal_mask is not None:
        rust = t.math('MULTIPLY', metal_mask, t.mrange(zj, 0.50 * H, 0.06 * H))
        rust = t.math('MULTIPLY', rust, t.mrange(t.noise(11.0, 5.0, 0.65, 2.2), 0.32, 0.55), clamp=True)
        rc = t.mixc(n2, t.rgb('#6e3519'), t.rgb('#9a5a2c'))
        col = t.mixc(rust, col, rc); rough = t.mixf(rust, rough, 0.9); metal = t.mixf(rust, metal, 0.1)
    lo, hi = p['polish']
    geo = t.new('ShaderNodeNewGeometry')
    edge = t.mrange(geo.outputs['Pointiness'], 0.505, 0.56)
    band = t.mrange(z, lo * H, min(hi, 1.0) * H * 0.98 if hi <= 1.0 else hi * H)
    pol = t.math('MULTIPLY', t.math('MULTIPLY', edge, band), t.mrange(n1, 0.3, 0.6, 0.5, 1.0), clamp=True)
    col = t.mixc(t.math('MULTIPLY', pol, 0.85), col, t.mixc(1.0, col, (1.6, 1.45, 1.25, 1.0), blend='MULTIPLY'))
    rough = t.mixf(pol, rough, 0.28)
    if metal_mask is not None:
        metal = t.mixf(t.math('MULTIPLY', pol, metal_mask), metal, 1.0)
    return col, rough, metal

# ---------------------------------------------------------------- adopted sources
def tex_paths(src, matname):
    d = os.path.join(SRC, src, 'textures')
    g = lambda k: (glob.glob(os.path.join(d, '%s_%s_2k.jpg' % (matname, k))) or [None])[0]
    return dict(diff=g('diff'), nor=g('nor_gl'), arm=g('arm'), rough=g('rough'))

def graded_albedo(name, src, matname, p):
    """Regrade the source diffuse per region (iron by the ARM metallic channel, wood/clay/wicker the rest)."""
    tp = tex_paths(src, matname)
    _, d = img_np(tp['diff'], 'sRGB')
    lin = srgb2lin(d[..., :3])        # loaded as byte sRGB: pixels are the stored sRGB values
    for (x0, y0, x1, y1) in p.get('erase', ()):
        # paint out source markings (crate_02's stencilled letters read as modern text): clone the clean stretch of the same
        # boards just below (boards run along v there; above sits a nail row), feathered at the edges
        Hh, Ww = lin.shape[:2]; c0, c1 = int(x0 * Ww), int(x1 * Ww)
        r0, r1 = int(y0 * Hh), int(y1 * Hh)   # Blender pixel rows run bottom-up, like UV v
        h = r1 - r0; src = lin[r0 - h:r0, c0:c1].copy()
        fy = np.minimum(np.arange(h) + 1, h - np.arange(h)) / 12.0; fx = np.minimum(np.arange(c1 - c0) + 1, (c1 - c0) - np.arange(c1 - c0)) / 12.0
        f = np.clip(np.minimum(fy[:, None], fx[None, :]), 0, 1)[..., None]
        lin[r0:r1, c0:c1] = lin[r0:r1, c0:c1] * (1 - f) + src * f
    L = lum(lin)
    valid = (L > 0.004).astype(np.float32)   # atlas padding is black: keep it out of the means
    if tp['arm']:
        _, a = img_np(tp['arm'], 'Non-Color'); m = np.clip((a[..., 2] - 0.6) / 0.25, 0, 1)   # crate_01 carries a 0.3 metallic haze on its wood
    else:
        m = np.zeros(L.shape, np.float32)
    if 'clay' in p:
        out = grade_region(lin, valid, p['clay'], hue_keep=0.25, delight=0.7, contrast=1.1, chroma=0.9)
    else:
        w = valid * (1 - m)
        out = grade_region(lin, w, p['wood'], **WOOD)
        if m.sum() > 50:
            iron = grade_region(lin, valid * m, IRON, hue_keep=0.3, delight=0.4, contrast=1.2, chroma=0.8)
            out = out * (1 - m[..., None]) + iron * m[..., None]
    path = os.path.join(OUT, name, '_src_%s_albedo.png' % matname)
    save_np(lin2srgb(out), path, 'sRGB')
    return path, tp

def adopted_material(name, src, matname, p, H):
    alb, tp = graded_albedo(name, src, matname, p)
    mat = bpy.data.materials.new('H_%s_%s' % (name, matname)); mat.use_nodes = True; mat.node_tree.nodes.clear()
    t = NT(mat)
    col = t.image(alb, 'sRGB').outputs['Color']
    if tp['arm']:
        sep = t.new('ShaderNodeSeparateColor'); t.link(t.image(tp['arm'], 'Non-Color').outputs['Color'], sep.inputs[0])
        ao, rough, metal = sep.outputs[0], sep.outputs[1], sep.outputs[2]
    else:
        ao, metal = 1.0, 0.0
        rough = t.image(tp['rough'], 'Non-Color').outputs['Color'] if tp['rough'] else 0.6
    nm = t.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = 1.0
    t.link(t.image(tp['nor'], 'Non-Color').outputs['Color'], nm.inputs['Color'])
    metal_mask = None
    if 'clay' not in p and not p.get('basket'):
        hm = t.mrange(t.new('ShaderNodeAttribute', attribute_name='hoopm').outputs['Fac'], 0.35, 0.6)
        metal_mask = t.math('MAXIMUM', t.mrange(metal, 0.6, 0.85), hm) if tp['arm'] else hm
        metal = t.mixf(metal_mask, 0.0, 1.0)     # crisp iron / wood split, the scan's half-metal haze gone
        # bands the scan painted without a metallic value: warm dark iron carrying the scan's luminance structure
        bw = t.new('ShaderNodeRGBToBW'); t.link(col, bw.inputs[0])
        iron = t.mixc(1.0, t.rgb(IRON), t.math('MULTIPLY', bw.outputs[0], 4.0, clamp=False), blend='MULTIPLY')
        col = t.mixc(hm, col, iron)
    if 'clay' in p:
        # slip glaze over the shoulder, ending in runs; bare clay below (vessels.md); glaze glossy, clay matte
        z = t.z()
        runs = t.math('MULTIPLY', t.math('SUBTRACT', t.noise(3.0, 2.0, 0.5, 6.0, vec=None), 0.5), 0.10 * H)
        stretch = t.new('ShaderNodeMapping'); t.link(t.obj(), stretch.inputs['Vector']); stretch.inputs['Scale'].default_value = (14, 14, 1.2)
        streak = t.noise(4.0, 2.0, 0.5, 9.0, vec=stretch.outputs[0])
        edge = t.math('ADD', t.math('ADD', p['glaze_from'] * H, runs), t.math('MULTIPLY', t.math('SUBTRACT', streak, 0.5), 0.22 * H))
        g = t.mrange(t.math('SUBTRACT', z, edge), -0.004, 0.004)
        gl = t.mixc(t.noise(30.0, 2.0, 0.5, 2.0), t.rgb(p['glaze']), t.mixc(1.0, t.rgb(p['glaze']), (0.75, 0.7, 0.68, 1.0), blend='MULTIPLY'))
        col = t.mixc(t.math('MULTIPLY', g, 0.9), col, gl)
        rough = t.mixf(g, t.math('ADD', t.math('MULTIPLY', rough, 0.3), 0.6), 0.22)
    if 'paint' in p:
        # faded painted band (chalky ochre) worn off the edges and the lower boards
        hexp, a, b = p['paint']
        z = t.z()
        band = t.math('MULTIPLY', t.mrange(z, a * H - 0.01, a * H + 0.01), t.mrange(z, b * H + 0.01, b * H - 0.01))
        geo = t.new('ShaderNodeNewGeometry')
        keep = t.math('MULTIPLY', band, t.math('SUBTRACT', 1.0, t.mrange(geo.outputs['Pointiness'], 0.53, 0.58)))
        keep = t.math('MULTIPLY', keep, t.mrange(t.noise(14.0, 6.0, 0.7, 3.3), 0.28, 0.45), clamp=True)
        bw = t.new('ShaderNodeRGBToBW'); t.link(col, bw.inputs[0])
        pc = t.mixc(1.0, t.rgb(hexp), t.math('ADD', t.math('MULTIPLY', bw.outputs[0], 2.5), 0.35), blend='MULTIPLY')
        col = t.mixc(t.math('MULTIPLY', keep, 0.85), col, pc)
        rough = t.mixf(keep, rough, 0.75); metal = t.mixf(keep, metal, 0.0)
    col, rough, metal = wear(t, col, rough, metal, H, p, metal_mask, ao if not isinstance(ao, float) else None)
    finish(t, col, rough, metal, ao, nm.outputs[0])
    return mat

def import_source(src, parts):
    bpy.ops.object.select_all(action='DESELECT')
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=glob.glob(os.path.join(SRC, src, '*.gltf'))[0])
    new = [o for o in bpy.data.objects if o not in before]
    keep = [o for o in new if o.type == 'MESH' and (parts is None or o.name.split('.')[0] in parts)]
    for o in new:
        if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
    for o in keep:
        o.data = o.data.copy()
        o.data.transform(o.matrix_world); o.matrix_world = Matrix()
    return keep

def join(objs, name):
    if len(objs) > 1:
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.join()
    o = objs[0]; o.name = name; o.data.name = name
    return o

def bounds(o):
    v = np.empty(len(o.data.vertices) * 3, np.float32); o.data.vertices.foreach_get('co', v); v = v.reshape(-1, 3)
    return v.min(0), v.max(0)

def recentre(o, scale):
    mn, mx = bounds(o)
    c = Vector(((mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]))
    o.data.transform(Matrix.Translation(-c)); o.data.transform(Matrix.Scale(scale, 4))

def metal_per_vertex(o, src):
    """Per-vertex metal mask from the source ARM maps (B channel) sampled at the loop UVs, per material."""
    me = o.data; mv = np.zeros(len(me.vertices), np.float32); cnt = np.zeros(len(me.vertices), np.float32)
    uv = me.uv_layers.active.data
    for mi, mat in enumerate(me.materials):
        tp = tex_paths(src, mat.name.split('.')[0])
        if not tp['arm']: continue
        _, a = img_np(tp['arm'], 'Non-Color'); H_, W_ = a.shape[:2]
        for poly in me.polygons:
            if poly.material_index != mi: continue
            for li in poly.loop_indices:
                u, v = uv[li].uv
                px = int((u % 1.0) * (W_ - 1)); py = int((v % 1.0) * (H_ - 1))
                mv[me.loops[li].vertex_index] += a[py, px, 2]; cnt[me.loops[li].vertex_index] += 1
    return np.where(cnt > 0, mv / np.maximum(cnt, 1), 0.0)

def hoop_islands(o):
    """Per-vertex flag for hoop bands modelled as their own islands (wooden_barrels_01 paints its iron without a metallic value):
    a loose part with many vertices, a short height and nearly the full width. Also thickens each band by 35% about its centre."""
    me = o.data; bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    mn, mx = bounds(o); H = float(mx[2] - mn[2]); Wd = float(max(mx[0] - mn[0], mx[1] - mn[1]))
    flag = np.zeros(len(me.vertices), np.float32); seen = np.zeros(len(me.vertices), bool)
    for v in bm.verts:
        if seen[v.index]: continue
        st = [v]; comp = []; seen[v.index] = True
        while st:
            x = st.pop(); comp.append(x.index)
            for e in x.link_edges:
                y = e.other_vert(x)
                if not seen[y.index]: seen[y.index] = True; st.append(y)
        co = np.array([me.vertices[i].co[:] for i in comp])
        zext = co[:, 2].max() - co[:, 2].min(); wd = co[:, 0].max() - co[:, 0].min()
        if len(comp) >= 40 and zext < 0.09 * H and wd > 0.6 * Wd:
            flag[comp] = 1.0
            zc = (co[:, 2].max() + co[:, 2].min()) / 2
            for i in comp: me.vertices[i].co.z = zc + (me.vertices[i].co.z - zc) * 1.35
    bm.free()
    return flag

def reshape(o, p, name, metal=None):
    """Chunkier, older: bilge bulge, squash, lean about the low base edge, low-frequency wobble, iron pushed proud."""
    me = o.data; r = rng(name)
    mn, mx = bounds(o); H = float(mx[2] - mn[2]); R = float(max(mx[0] - mn[0], mx[1] - mn[1])) / 2
    sq = p.get('squash', 1.0); off = Vector((r.uniform(-50, 50), r.uniform(-50, 50), r.uniform(-50, 50)))
    me.calc_normals_split() if hasattr(me, 'calc_normals_split') else None
    nrm = [v.normal.copy() for v in me.vertices]
    lean = math.radians(p.get('lean', 0.0)); ldir = r.uniform(0, 2 * math.pi)
    for i, v in enumerate(me.vertices):
        co = v.co.copy(); h = co.z / max(H, 1e-4)
        s = 1.0 + p.get('bulge', 0.0) * math.sin(math.pi * min(max(h, 0), 1))
        co.x *= s; co.y *= s; co.z *= sq
        if p.get('sag', 0) > 0 and h > 0.5:   # lid and top boards sag toward the middle of the long span
            u = co.x / max(R, 1e-3); w_ = co.y / max(R, 1e-3)
            co.z -= p['sag'] * (h - 0.5) * 2 * max(0.0, 1 - u * u) * max(0.0, 1 - 0.5 * w_ * w_)
        w = noise.noise_vector(co * (1.6 / max(R, 0.1)) + off) * p.get('wob', 0.0)
        co += Vector((w.x, w.y, w.z * 0.5))
        if metal is not None and p.get('hoop', 0) > 0 and metal[i] > 0.6:
            co += nrm[i] * p['hoop'] * min(1.0, (metal[i] - 0.6) / 0.25)
        # lean: shear toward ldir in proportion to height (the base ring stays on z 0)
        co.x += math.cos(ldir) * co.z * math.tan(lean); co.y += math.sin(ldir) * co.z * math.tan(lean)
        v.co = co
    me.update()
    return H * sq

# ---------------------------------------------------------------- generated parts: produce and sacks
def lathe_obj(name, prof, seg, fn_r=None):
    """prof: [(r, z)], bottom to top; capped at both ends (no r=0 points, sc-asset-craft 6)."""
    bm = bmesh.new(); rings = []
    for k, (rr, zz) in enumerate(prof):
        ring = []
        for j in range(seg):
            a = 2 * math.pi * j / seg
            rad = fn_r(rr, zz, a, k) if fn_r else rr
            ring.append(bm.verts.new((rad * math.cos(a), rad * math.sin(a), zz)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for j in range(seg):
            bm.faces.new((rings[k][j], rings[k][(j + 1) % seg], rings[k + 1][(j + 1) % seg], rings[k + 1][j]))
    bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o

def proc_material(kind):
    """Procedural PBR for generated parts (apples, turnips, bread, straw, burlap, grain, rope); placed wear added by the caller."""
    key = 'P_' + kind
    if key in bpy.data.materials: return bpy.data.materials[key]
    mat = bpy.data.materials.new(key); mat.use_nodes = True; mat.node_tree.nodes.clear(); t = NT(mat)
    z = t.z(); nrm = None; ao = 1.0; metal = 0.0
    if kind == 'apple':
        streak_m = t.new('ShaderNodeMapping'); t.link(t.obj(), streak_m.inputs['Vector']); streak_m.inputs['Scale'].default_value = (40, 40, 6)
        st = t.noise(1.0, 3.0, 0.6, 1.0, vec=streak_m.outputs[0])
        base = t.mixc(t.mrange(t.noise(5.0, 2.0, 0.5, 2.0), 0.3, 0.7), t.rgb('#a09a3a'), t.rgb('#b2402a'))
        col = t.mixc(t.mrange(st, 0.4, 0.7, 0.0, 0.7), base, t.rgb('#8a2420'))
        rough = t.mixf(t.noise(30.0), 0.38, 0.55)
        bump = t.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.08; t.link(t.noise(80.0), bump.inputs['Height']); nrm = bump.outputs[0]
    elif kind == 'turnip':
        # purple shoulder fading to cream root (object z of the produce piece is lost after the join: driven by the vertex colour 'tz')
        a = t.new('ShaderNodeAttribute', attribute_name='tz')
        col = t.mixc(t.mrange(t.math('ADD', a.outputs['Fac'], t.math('MULTIPLY', t.noise(12.0), 0.25)), 0.55, 0.8),
                     t.rgb('#d6ccb0'), t.rgb('#7a3a62'))
        rough = 0.55
        bump = t.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.15; t.link(t.noise(45.0, 4.0), bump.inputs['Height']); nrm = bump.outputs[0]
    elif kind == 'squash':
        # deep ochre-orange with paler lobe ridges and a few green-grey streaks (stall harvest)
        a = t.new('ShaderNodeAttribute', attribute_name='tz')
        mp = t.new('ShaderNodeMapping'); t.link(t.obj(), mp.inputs['Vector']); mp.inputs['Scale'].default_value = (30, 30, 3)
        st = t.noise(1.0, 2.0, 0.5, 3.0, vec=mp.outputs[0])
        col = t.mixc(t.mrange(t.noise(6.0), 0.3, 0.7), t.rgb('#b8641f'), t.rgb('#c98a34'))
        col = t.mixc(t.mrange(st, 0.55, 0.7, 0.0, 0.6), col, t.rgb('#6e7448'))
        col = t.mixc(t.mrange(a.outputs['Fac'], 0.8, 1.0, 0.0, 0.5), col, t.rgb('#8c6a3a'))
        rough = 0.5
        bump = t.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.12; t.link(t.noise(60.0), bump.inputs['Height']); nrm = bump.outputs[0]
    elif kind == 'leaf':
        col = t.mixc(t.noise(20.0), t.rgb('#5e7a2c'), t.rgb('#7c8c34')); rough = 0.6
    elif kind == 'bread':
        a = t.new('ShaderNodeAttribute', attribute_name='tz')
        col = t.mixc(t.mrange(t.math('ADD', a.outputs['Fac'], t.math('MULTIPLY', t.noise(10.0, 4.0), 0.3)), 0.4, 0.9),
                     t.rgb('#c89a5c'), t.rgb('#8c5228'))
        rough = t.mixf(t.noise(25.0), 0.55, 0.8)
        bump = t.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.35; t.link(t.noise(30.0, 5.0, 0.7), bump.inputs['Height']); nrm = bump.outputs[0]
    elif kind == 'straw':
        mp = t.new('ShaderNodeMapping'); t.link(t.obj(), mp.inputs['Vector']); mp.inputs['Scale'].default_value = (60, 6, 60)
        mp.inputs['Rotation'].default_value = (0.0, 0.0, 0.7)
        s = t.noise(1.0, 2.0, 0.5, 5.0, vec=mp.outputs[0])
        col = t.mixc(s, t.rgb('#7a6230'), t.rgb('#c8a856')); rough = 0.85
        bump = t.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.9; t.link(s, bump.inputs['Height']); nrm = bump.outputs[0]
    elif kind in ('burlap', 'burlap_in'):
        img = t.image(os.path.join(TEX, 'canvas', 'canvas_albedo.png'), 'sRGB', box=2.2)
        orm = t.image(os.path.join(TEX, 'canvas', 'canvas_orm.png'), 'Non-Color', box=2.2)
        nimg = t.image(os.path.join(TEX, 'canvas', 'canvas_normal.png'), 'Non-Color', box=2.2)
        # burlap: the shared canvas regraded coarser and paler-tan, plus a coarse jute weave (two crossed wave sets, ~5 mm pitch)
        hsv = t.new('ShaderNodeHueSaturation'); hsv.inputs['Saturation'].default_value = 0.85; hsv.inputs['Value'].default_value = 1.05
        t.link(img.outputs['Color'], hsv.inputs['Color'])
        wa = t.new('ShaderNodeTexWave', wave_type='BANDS', bands_direction='X'); t.link(t.obj(), wa.inputs['Vector']); wa.inputs['Scale'].default_value = 120
        wa.inputs['Distortion'].default_value = 1.5; wa.inputs['Detail'].default_value = 2.0
        wb = t.new('ShaderNodeTexWave', wave_type='BANDS', bands_direction='Z'); t.link(t.obj(), wb.inputs['Vector']); wb.inputs['Scale'].default_value = 120
        wb.inputs['Distortion'].default_value = 1.5; wb.inputs['Detail'].default_value = 2.0
        weave = t.math('MULTIPLY', t.math('ADD', wa.outputs['Fac'], wb.outputs['Fac']), 0.5)
        col = t.mixc(0.35, t.rgb('#b39568'), hsv.outputs[0], blend='OVERLAY')
        col = t.mixc(t.math('MULTIPLY', t.math('SUBTRACT', weave, 0.5), 0.45), col, t.rgb('#6a5638'))
        col = t.mixc(t.math('MULTIPLY', t.mrange(t.noise(3.0, 3.0), 0.4, 0.7), 0.35), col, t.rgb('#8a7550'))
        stain = t.mrange(t.noise(1.8, 4.0, 0.7, 8.0), 0.55, 0.72)
        col = t.mixc(t.math('MULTIPLY', stain, 0.45), col, t.rgb('#6e5636'))
        if kind == 'burlap_in': col = t.mixc(1.0, col, (0.7, 0.68, 0.64, 1.0), blend='MULTIPLY')
        sep = t.new('ShaderNodeSeparateColor'); t.link(orm.outputs['Color'], sep.inputs[0]); ao = sep.outputs[0]
        rough = t.math('ADD', t.math('MULTIPLY', sep.outputs[1], 0.3), 0.66)
        nm = t.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = 1.2; t.link(nimg.outputs['Color'], nm.inputs['Color'])
        bump = t.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.8; bump.inputs['Distance'].default_value = 0.003
        t.link(weave, bump.inputs['Height']); t.link(nm.outputs[0], bump.inputs['Normal']); nrm = bump.outputs[0]
    elif kind == 'grain':
        vo = t.new('ShaderNodeTexVoronoi'); t.link(t.obj(), vo.inputs['Vector']); vo.inputs['Scale'].default_value = 160
        col = t.mixc(t.noise(60.0), t.rgb('#c2a468'), t.rgb('#9c7c44'))
        col = t.mixc(t.mrange(vo.outputs['Distance'], 0.0, 0.5), t.rgb('#6e5530'), col)
        rough = 0.7
        bump = t.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.6; bump.inputs['Distance'].default_value = 0.003
        t.link(vo.outputs['Distance'], bump.inputs['Height']); nrm = bump.outputs[0]
    elif kind == 'rope':
        mp = t.new('ShaderNodeMapping'); t.link(t.obj(), mp.inputs['Vector']); mp.inputs['Scale'].default_value = (1, 1, 1)
        wa = t.new('ShaderNodeTexWave', wave_type='RINGS'); t.link(t.obj(), wa.inputs['Vector']); wa.inputs['Scale'].default_value = 60
        col = t.mixc(wa.outputs['Fac'], t.rgb('#6e5a3a'), t.rgb('#a08a5c')); rough = 0.85
        bump = t.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.6; t.link(wa.outputs['Fac'], bump.inputs['Height']); nrm = bump.outputs[0]
    t._kind = kind
    mat['kind'] = kind
    return mat, t, col, rough, metal, ao, nrm

def material_for(kind, p, H):
    key = 'P_%s_%s' % (kind, p.get('_name', ''))
    if key in bpy.data.materials: return bpy.data.materials[key]
    mat, t, col, rough, metal, ao, nrm = proc_material(kind)
    mat.name = key
    if kind in ('burlap', 'burlap_in'):
        z = t.z()
        # grain dust paler around the opening and the neck; damp, darker foot (where it sat on the cobbles)
        dust = t.math('MULTIPLY', t.mrange(z, 0.62 * H, 0.95 * H), t.mrange(t.noise(9.0, 3.0), 0.35, 0.6))
        col = t.mixc(t.math('MULTIPLY', dust, 0.35), col, t.rgb('#d2c09a'))
        damp = t.math('MULTIPLY', t.mrange(t.math('ADD', z, t.math('MULTIPLY', t.noise(4.0, 3.0), 0.08)), 0.16, 0.04), 0.55)
        col = t.mixc(damp, col, t.mixc(1.0, col, (0.62, 0.58, 0.52, 1.0), blend='MULTIPLY'))
        col, rough, metal = wear(t, col, rough, metal, H, dict(dirt=p.get('dirt', 0.1), polish=(2.0, 3.0)))
    finish(t, col, rough, metal, ao, nrm, bevel=0.0 if kind in ('grain', 'straw') else 0.004)
    return mat

def tag_tz(o):
    """Per-piece normalised height as a colour attribute (produce shading survives the join)."""
    me = o.data; zs = [v.co.z for v in me.vertices]; lo, hi = min(zs), max(zs)
    at = me.color_attributes.new('tz', 'FLOAT_COLOR', 'POINT')
    for i, v in enumerate(me.vertices):
        t = (v.co.z - lo) / max(hi - lo, 1e-5); at.data[i].color = (t, t, t, 1)

def apple(name, r):
    a = r.uniform(0.040, 0.047)
    prof = []
    for k in range(13):
        th = math.pi * (k + 0.5) / 13 - math.pi / 2
        rr = a * math.cos(th) * (1.0 + 0.06 * math.sin(th)); zz = a * 0.92 * (math.sin(th) + 1)
        if k >= 11: zz -= a * 0.18 * (k - 10)          # stem dimple
        if k <= 1: zz += a * 0.10 * (2 - k)            # calyx dimple
        prof.append((max(rr, a * 0.12), zz))
    o = lathe_obj(name, prof, 14, lambda rr, zz, ang, k: rr * (1 + 0.04 * math.sin(5 * ang + k)))
    st = lathe_obj(name + '_stem', [(0.0035, a * 1.55), (0.0028, a * 1.95), (0.002, a * 2.15)], 5)
    st.data.transform(Matrix.Rotation(r.uniform(-0.4, 0.4), 4, 'X'))
    o.data.materials.append(material_for('apple', {}, 0)); st.data.materials.append(material_for('rope', {}, 0))
    return [o, st]

def turnip(name, r):
    a = r.uniform(0.045, 0.055)
    prof = [(0.004, -a * 0.95), (0.010, -a * 0.75), (a * 0.45, -a * 0.35), (a * 0.85, 0.0), (a * 0.95, a * 0.35), (a * 0.75, a * 0.65),
            (a * 0.35, a * 0.85), (a * 0.12, a * 0.9)]
    prof = [(rr, zz + a * 0.95) for rr, zz in prof]
    o = lathe_obj(name, prof, 14, lambda rr, zz, ang, k: rr * (1 + 0.05 * math.sin(3 * ang + k * 1.3)))
    tag_tz(o); o.data.materials.append(material_for('turnip', {}, 0))
    leaves = []
    for j in range(3):
        lf = lathe_obj('%s_leaf%d' % (name, j), [(0.006, 0), (0.004, 0.05), (0.002, 0.08)], 5)
        lf.data.transform(Matrix.Rotation(0.35 + r.uniform(0, 0.3), 4, 'X') @ Matrix.Rotation(j * 2.1, 4, 'Z').inverted())
        lf.data.transform(Matrix.Rotation(j * 2.1, 4, 'Z')); lf.data.transform(Matrix.Translation((0, 0, a * 1.75)))
        lf.data.materials.append(material_for('leaf', {}, 0)); leaves.append(lf)
    return [o] + leaves

def squash(name, r):
    """Small ribbed winter squash (storybook chunky, ~13 cm): 8 lobes, flattened, a thick corky stem."""
    a = r.uniform(0.060, 0.072); lob = r.choice((8, 9, 10))
    prof = []
    for k in range(12):
        th = math.pi * (k + 0.5) / 12 - math.pi / 2
        rr = a * math.cos(th); zz = a * 0.72 * (math.sin(th) + 1)
        if k >= 10: zz -= a * 0.10 * (k - 9)
        if k <= 1: zz += a * 0.06 * (2 - k)
        prof.append((max(rr, a * 0.10), zz))
    o = lathe_obj(name, prof, lob * 4, lambda rr, zz, ang, k: rr * (1 - 0.09 * abs(math.sin(ang * lob / 2)) ** 0.7))
    tag_tz(o); o.data.materials.append(material_for('squash', {}, 0))
    st = lathe_obj(name + '_stem', [(0.011, a * 1.30), (0.008, a * 1.55), (0.009, a * 1.70)], 6)
    st.data.transform(Matrix.Rotation(r.uniform(-0.3, 0.3), 4, 'X')); st.data.materials.append(material_for('rope', {}, 0))
    return [o, st]

def loaf(name, r, size=1.0):
    L_, W_, Hh = r.uniform(0.20, 0.24) * size, r.uniform(0.11, 0.13) * size, r.uniform(0.075, 0.09) * size
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=1.0)
    o = bpy.context.active_object; o.name = name
    for v in o.data.vertices:
        x, y, z = v.co
        z = z if z > 0 else z * 0.25
        sc_ = 1 - 0.07 * math.cos(x * 9) * max(z, 0)    # scoring ridges across the top
        v.co = Vector((x * L_ / 2, y * W_ / 2 * (1 - 0.1 * x * x), (z * Hh * sc_) + 0.02 * Hh))
    tag_tz(o); o.data.materials.append(material_for('bread', {}, 0))
    return [o]

def place_produce(p, H, bounds_xy, z0, r):
    """Heap chunky produce inside an ellipse (rx, ry) from fill height z0, a dome toward the middle."""
    rx, ry = bounds_xy; objs = []
    items = ['loaf'] * p['n_loaf'] + ['squash'] * p.get('n_squash', 0) + ['turnip'] * p['n_turnip'] + ['apple'] * p['n_apple']
    placed = []   # (x, y, z of the centre, radius): spheres resting on the bed or on each other, lowest spot of a few tries
    for k, kind in enumerate(items):
        rad_k = {'apple': 0.042, 'turnip': 0.048, 'loaf': 0.05, 'squash': 0.058}[kind]
        best = None
        for _ in range(24):
            if kind == 'loaf':
                x, y = r.uniform(-0.3, 0.3) * rx, r.uniform(-0.2, 0.2) * ry
            else:
                ang = r.uniform(0, 2 * math.pi); rad = math.sqrt(r.uniform(0, 1))
                x, y = (rx - rad_k) * rad * math.cos(ang), (ry - rad_k) * rad * math.sin(ang)
            zc = z0 + rad_k * 0.8
            for px, py, pz, pr in placed:
                d2 = (x - px) ** 2 + (y - py) ** 2; s = (rad_k + pr) * 0.9
                if d2 < s * s: zc = max(zc, pz + math.sqrt(s * s - d2))
            if best is None or zc < best[2]: best = (x, y, zc)
        x, y, zc = best
        if zc > z0 + rad_k * 0.8 + p.get('max_h', 0.1): continue    # a low heap, never a tower
        placed.append((x, y, zc, rad_k))
        z = zc - rad_k * 0.8
        parts = loaf('pr_loaf_%d' % k, r, min(1.0, rx * 1.25 / 0.22)) if kind == 'loaf' else {'apple': apple, 'turnip': turnip, 'squash': squash}[kind]('pr_%s_%d' % (kind, k), r)
        rot = Matrix.Rotation(r.uniform(0, 6.28), 4, 'Z') @ Matrix.Rotation(r.uniform(-0.5, 0.5) if kind != 'loaf' else r.uniform(-0.1, 0.1), 4, 'X')
        if kind == 'turnip': rot = rot @ Matrix.Rotation(r.uniform(1.0, 1.5), 4, 'Y')   # turnips lie on their side
        for o in parts:
            if kind == 'turnip':   # turn about the bulb, then rest the bulb centre on the heap
                o.data.transform(Matrix.Translation((0, 0, -rad_k))); o.data.transform(rot); o.data.transform(Matrix.Translation((x, y, zc)))
            else:
                o.data.transform(rot); o.data.transform(Matrix.Translation((x, y, z)))
        objs += parts
    return objs

def straw_bed(rx, ry, z, name):
    bpy.ops.mesh.primitive_circle_add(vertices=32, radius=1.0, fill_type='TRIFAN')
    o = bpy.context.active_object; o.name = name
    for v in o.data.vertices:   # ragged edge, tufts poking up between the produce
        a = math.atan2(v.co.y, v.co.x); e = v.co.length
        v.co.x *= 1 + 0.12 * noise.noise(Vector((math.cos(a) * 3, math.sin(a) * 3, 1.1)))
        v.co.z = 0.012 * e * (0.5 + noise.noise(Vector((v.co.x * 9, v.co.y * 9, 2.0))))
    o.data.transform(Matrix.Diagonal((rx, ry, 1, 1))); o.data.transform(Matrix.Translation((0, 0, z)))
    o.data.materials.append(material_for('straw', {}, 0))
    return o

def sack(name, variant, p):
    """Chunky burlap grain sack: lathe rings with slump, gathered pleats at the neck, a rope tie; open variant has a rolled cuff and grain."""
    r = rng(name); seg = 40
    Hs = 0.80 * OVERSIZE; R = 0.27 * OVERSIZE
    if variant == 'open':
        # belly low and wide, a waist, then the mouth rolled down into a fat cuff
        prof = [(R * 0.70, 0.0), (R * 0.98, 0.04), (R * 1.12, 0.12), (R * 1.15, 0.25), (R * 1.08, 0.38), (R * 1.00, 0.47),
                (R * 1.04, 0.51), (R * 1.17, 0.54), (R * 1.22, 0.575), (R * 1.16, 0.607), (R * 1.03, 0.615), (R * 0.97, 0.60)]
        neck = None
    else:
        prof = [(R * 0.70, 0.0), (R * 0.98, 0.04), (R * 1.10, 0.12), (R * 1.12, 0.28), (R * 1.05, 0.45), (R * 0.86, 0.58),
                (R * 0.52, 0.68), (R * 0.26, 0.745), (R * 0.20, 0.77), (R * 0.30, 0.81), (R * 0.40, 0.86), (R * 0.34, 0.89), (R * 0.12, 0.90)]
        neck = 0.77
    prof = [(rr, zz * Hs / 0.9) for rr, zz in prof]
    off = Vector((r.uniform(0, 99), r.uniform(0, 99), 0))
    def fr(rr, zz, ang, k):
        h = zz / Hs * 0.9                  # back to profile units (0..0.9), where the neck height is given
        pleat = 0.0
        if neck is not None:
            pl = math.exp(-((h - neck) / 0.08) ** 2)
            amp = 0.6 + 0.4 * noise.noise(Vector((math.cos(ang) * 2, math.sin(ang) * 2, 7.7)) + off)
            pleat = pl * 0.26 * amp * (0.5 + 0.5 * math.cos(ang * 9 + 2 * math.sin(ang * 3)))
        elif 0.5 < h < 0.6:
            pleat = 0.04 * (0.5 + 0.5 * math.cos(ang * 7 + math.sin(ang * 2)))
        lump = (noise.noise(Vector((math.cos(ang) * 1.3, math.sin(ang) * 1.3, h * 2.2)) + off) * 0.11
                + noise.noise(Vector((math.cos(ang) * 4.0, math.sin(ang) * 4.0, h * 6.0)) + off) * 0.035)
        crease = 0.025 * max(0.0, math.sin(h * 31 + 3 * math.sin(ang * 2))) ** 6   # slack horizontal creases
        sag = 1 + 0.10 * math.exp(-((h - 0.18) / 0.15) ** 2)        # grain settles: belly low
        return rr * (1 - pleat + lump - crease) * sag
    rings = [(rr, zz) for rr, zz in prof]
    # resample the profile for smooth folds
    dense = []
    for i in range(len(rings) - 1):
        (r0, z0), (r1, z1) = rings[i], rings[i + 1]
        n = 3
        for j in range(n): dense.append((r0 + (r1 - r0) * j / n, z0 + (z1 - z0) * j / n))
    dense.append(rings[-1])
    o = lathe_obj(name + '_body', dense, seg, fr)
    me = o.data
    # front-back squash and a soft footprint flatten
    for v in me.vertices:
        v.co.y *= 0.86
        if v.co.z < 0.03: v.co.z *= 0.5
    parts = [o]; H = Hs
    if variant == 'open':
        # thickness so the inside reads, then the grain cap just under the cuff
        mod = o.modifiers.new('sol', 'SOLIDIFY'); mod.thickness = 0.012; mod.offset = -1
        dg = bpy.context.evaluated_depsgraph_get(); m2 = bpy.data.meshes.new_from_object(o.evaluated_get(dg)); o.modifiers.clear()
        o.data = m2
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=10, radius=1.0)
        g = bpy.context.active_object; g.name = name + '_grain'
        for v in g.data.vertices:
            x, y, z = v.co
            z = max(z, 0) * 0.12 + noise.noise(Vector((x * 6, y * 6, 3.3))) * 0.012
            v.co = Vector((x * R * 1.0, y * R * 0.86, z + 0.565 * Hs / 0.9))
        g.data.materials.append(material_for('grain', p, H))
        parts.append(g)
        o.data.materials.append(material_for('burlap', p, H))
    else:
        tz = neck * Hs / 0.9; rad = R * 0.21
        bpy.ops.mesh.primitive_torus_add(major_radius=rad, minor_radius=0.013, major_segments=24, minor_segments=8)
        tie = bpy.context.active_object; tie.name = name + '_tie'
        tie.data.transform(Matrix.Diagonal((1, 0.9, 1, 1))); tie.data.transform(Matrix.Rotation(0.12, 4, 'X'))
        tie.data.transform(Matrix.Translation((0, 0, tz)))
        # a loose rope end hanging down the front
        end = lathe_obj(name + '_end', [(0.011, 0.0), (0.011, 0.07), (0.009, 0.13), (0.013, 0.15), (0.004, 0.16)], 7)
        end.data.transform(Matrix.Rotation(math.pi, 4, 'X') @ Matrix.Rotation(-0.35, 4, 'X'))
        end.data.transform(Matrix.Translation((0.02, -rad * 0.95, tz)))
        for t_ in (tie, end): t_.data.materials.append(material_for('rope', p, H))
        parts += [tie, end]
        o.data.materials.append(material_for('burlap', p, H))
    if variant == 'slumped':
        # slump: lower and wider, neck flopped over to one side (bend about y above the shoulder)
        piv = 0.5 * Hs
        for part in parts:
            for v in part.data.vertices:
                c = v.co
                c.z *= 0.82; c.x *= 1.12
                if c.z > piv * 0.82:
                    t_ = (c.z - piv * 0.82) / (Hs * 0.82 - piv * 0.82)
                    a = 0.9 * t_ ** 1.5        # rigid turn per height slice, so the cap never folds through itself
                    dz = c.z - piv * 0.82
                    c.x, c.z = c.x * math.cos(a) + dz * math.sin(a), piv * 0.82 + dz * math.cos(a) - c.x * math.sin(a)
        H = Hs * 0.82
    for part in parts:
        part.data.transform(Matrix.Rotation(r.uniform(-0.5, 0.5), 4, 'Z'))
    return parts, H

# ---------------------------------------------------------------- build highs
def build_high(name, p):
    p['_name'] = name
    if p.get('gen') == 'sack':
        if p['variant'] == 'pair':
            # two sacks slumped together (brief 5): a slumped one, and a tied one sheared over onto its shoulder (base stays planted)
            pb, Hb = sack(name + '_b', 'slumped', p)
            pa, Ha = sack(name + '_a', 'tied', p)
            for o in pb: o.data.transform(Matrix.Translation((0.30, 0.04, 0)))
            for o in pa:
                o.data.transform(Matrix(((1, 0, 0.30, 0), (0, 1, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1))))   # x += 0.30 z
                o.data.transform(Matrix.Rotation(-0.25, 4, 'Z')); o.data.transform(Matrix.Translation((-0.26, -0.03, 0)))
            parts, H = pa + pb, max(Ha, Hb)
        else:
            parts, H = sack(name, p['variant'], p)
        hi = join(parts, name + '_high'); recentre(hi, 1.0)
        # smooth folds: a Catmull-Clark high (the low is decimated from it, the bake carries the rest)
        mod = hi.modifiers.new('sub', 'SUBSURF'); mod.levels = 2; mod.render_levels = 2
        dg = bpy.context.evaluated_depsgraph_get(); m2 = bpy.data.meshes.new_from_object(hi.evaluated_get(dg)); hi.modifiers.clear()
        hi.data = m2
        for f in m2.polygons: f.use_smooth = True
        return hi, H
    objs = import_source(p['src'], p['parts'])
    lid = None
    if p.get('leaned_lid'):
        lid = import_source(p['src'], ['wooden_crate_01_lid'])[0]
    hi = join(objs, name + '_high')
    recentre(hi, OVERSIZE)
    metal = metal_per_vertex(hi, p['src'])
    if p.get('rings'):
        metal = np.maximum(metal, hoop_islands(hi))
    at = hi.data.color_attributes.new('hoopm', 'FLOAT_COLOR', 'POINT')
    for i, mv in enumerate(metal): at.data[i].color = (mv, mv, mv, 1.0)
    H = reshape(hi, p, name, metal)
    # replace source materials with graded + worn ones (one per source material, same slot order)
    for i, m in enumerate(list(hi.data.materials)):
        hi.data.materials[i] = adopted_material(name, p['src'], m.name.split('.')[0], p, H)
    extra = []
    r = rng(name + 'produce')
    mn, mx = bounds(hi)
    if lid is not None:
        lid.data.transform(Matrix.Scale(OVERSIZE, 4))
        lmn, lmx = bounds(lid); lc = Vector(((lmn[0] + lmx[0]) / 2, (lmn[1] + lmx[1]) / 2, lmn[2]))
        lid.data.transform(Matrix.Translation(-lc))
        # stand the lid up on its long edge, leaning 18 deg onto the crate's +x side
        lid.data.transform(Matrix.Rotation(math.pi / 2, 4, 'Z'))
        lid.data.transform(Matrix.Rotation(math.radians(72), 4, 'Y'))
        lmn, lmx = bounds(lid)
        lid.data.transform(Matrix.Translation((float(mx[0]) - float(lmn[0]) + 0.005 + 0.02, 0.0, -float(lmn[2]))))
        for i, m in enumerate(list(lid.data.materials)):
            lid.data.materials[i] = hi.data.materials[0]
        extra.append(lid)
    if 'produce' in p:
        pr = p['produce']
        cx, cy = 0.0, 0.0
        rx = (float(mx[0]) - float(mn[0])) / 2 * 0.80; ry = (float(mx[1]) - float(mn[1])) / 2 * 0.78
        if lid is not None:
            rx = (float(mx[0]) - float(mn[0])) / 2 * 0.80
        ztop = float(mx[2])
        fill = ztop - (0.045 if pr['kind'] != 'bread' else 0.05)
        if pr['kind'] in ('crate', 'basket'):
            extra.append(straw_bed(rx * 1.08, ry * 1.08, fill - 0.01, name + '_straw'))
        extra += place_produce(pr, H, (rx, ry), fill - 0.03, r)
    if extra:
        hi = join([hi] + extra, name + '_high')
    return hi, H

# ---------------------------------------------------------------- low, bake, export
def make_low(hi, name, p):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(hi.evaluated_get(dg)); me.name = name
    lo = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(lo)
    tris = sum(len(f.vertices) - 2 for f in me.polygons)
    bm = bmesh.new(); bm.from_mesh(me)
    # bottom faces on the ground are never seen (brief section 2)
    dead = [f for f in bm.faces if f.normal.z < -0.85 and f.calc_center_median().z < 0.02]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)
    bm.to_mesh(me); bm.free()
    tris = sum(len(f.vertices) - 2 for f in me.polygons)
    if tris > p['tris']:
        mod = lo.modifiers.new('dec', 'DECIMATE'); mod.ratio = p['tris'] / tris; mod.use_collapse_triangulate = True
        dg = bpy.context.evaluated_depsgraph_get(); m2 = bpy.data.meshes.new_from_object(lo.evaluated_get(dg)); lo.modifiers.clear()
        lo.data = m2; m2.name = name
    bm = bmesh.new(); bm.from_mesh(lo.data); bmesh.ops.triangulate(bm, faces=bm.faces); bm.to_mesh(lo.data); bm.free()
    me = lo.data
    # adopted faces keep the source artist's UVs (decimation carries them; smart-UV on decimated wicker made thousands of
    # islands); generated faces (produce, straw, sacks) get a smart projection packed around the pinned source islands
    src_face = np.array([me.materials[f.material_index].name.startswith('H_') if me.materials else False for f in me.polygons])
    me.materials.clear()
    for a in [a.name for a in me.color_attributes]: me.color_attributes.remove(me.color_attributes[a])
    keep_src = src_face.any() and len(me.uv_layers) > 0
    while len(me.uv_layers) > (1 if keep_src else 0): me.uv_layers.remove(me.uv_layers[-1])
    if not keep_src: me.uv_layers.new(name='UVMap')
    uvl = me.uv_layers[0]; uvl.name = 'UVMap'; me.uv_layers.active = uvl
    if keep_src:
        uv = np.empty(len(me.loops) * 2, np.float32); uvl.data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
        loop_src = np.zeros(len(me.loops), bool)
        for f in me.polygons:
            if src_face[f.index]: loop_src[f.loop_start:f.loop_start + f.loop_total] = True
        s = 1.0 if src_face.all() else 0.80
        uv[loop_src] = uv[loop_src] * s
        uvl.data.foreach_set('uv', uv.ravel())
    if not keep_src or not src_face.all():
        bpy.ops.object.select_all(action='DESELECT'); lo.select_set(True); bpy.context.view_layer.objects.active = lo
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT')
        bpy.ops.object.mode_set(mode='OBJECT')
        for f in me.polygons: f.select = not src_face[f.index]
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.006, area_weight=0.0, scale_to_bounds=False)
        if keep_src:
            # the source atlas sits scaled in u, v < 0.80; squeeze the new islands into the free column (u 0.805..0.995), then
            # re-pack only them inside that box (ORIGINAL_AABB). Locked-pin packing segfaulted Blender 5.2 on Crate_C.
            bpy.ops.object.mode_set(mode='OBJECT')
            me = lo.data; uvl = me.uv_layers['UVMap']     # edit-mode toggles invalidate layer references
            uv = np.empty(len(me.loops) * 2, np.float32); uvl.data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
            gl = ~loop_src
            lo_, hi_ = uv[gl].min(0), uv[gl].max(0)
            uv[gl] = (0.805, 0.005) + (uv[gl] - lo_) / np.maximum(hi_ - lo_, 1e-6) * (0.19, 0.99)
            uvl.data.foreach_set('uv', uv.ravel())
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.uv.select_all(action='SELECT')
            bpy.ops.uv.pack_islands(udim_source='ORIGINAL_AABB', rotate=True, scale=True, margin_method='FRACTION', margin=0.004,
                                    shape_method='CONCAVE')
        else:
            bpy.ops.uv.pack_islands(margin=0.006, rotate=True)
    if bpy.context.object and bpy.context.object.mode == 'EDIT': bpy.ops.object.mode_set(mode='OBJECT')
    me = lo.data
    for f in me.polygons: f.use_smooth = True
    if hasattr(me, 'set_sharp_from_angle'): me.set_sharp_from_angle(angle=math.radians(50))
    return lo

def set_tap(mat, ch):
    nt = mat.node_tree; em = nt.nodes['BAKE_EMIT']; out = nt.nodes['OUT']
    for l in list(em.inputs['Color'].links): nt.links.remove(l)
    if ch is None:
        nt.links.new(nt.nodes['BSDF'].outputs[0], out.inputs['Surface'])
    else:
        nt.links.new(nt.nodes['TAP_' + ch].outputs[0], em.inputs['Color'])
        nt.links.new(em.outputs[0], out.inputs['Surface'])

def setup_cycles():
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'
    if not CPU:
        try:
            pr = bpy.context.preferences.addons['cycles'].preferences
            for dt in ('OPTIX', 'CUDA', 'HIP'):
                try:
                    pr.compute_device_type = dt; pr.get_devices()
                    if any(d.type == dt for d in pr.devices):
                        for d in pr.devices: d.use = d.type == dt
                        sc.cycles.device = 'GPU'; break
                except Exception: pass
        except Exception: pass
    sc.cycles.use_denoising = False

def bake(hi, lo, name, res):
    sc = bpy.context.scene; setup_cycles()
    d = os.path.join(OUT, name); os.makedirs(d, exist_ok=True)
    mats = [m for m in hi.data.materials if m is not None]
    lm = bpy.data.materials.new('M_' + name); lm.use_nodes = True; lo.data.materials.append(lm)
    nt = lm.node_tree
    imgs = {}
    for k, cs in (('col', 'sRGB'), ('rough', 'Non-Color'), ('metal', 'Non-Color'), ('ao', 'Non-Color'), ('nrm', 'Non-Color'), ('gao', 'Non-Color')):
        im = bpy.data.images.new('%s_%s' % (name, k), res, res, alpha=False, float_buffer=(k == 'nrm'))
        im.colorspace_settings.name = cs; imgs[k] = im
    tn = nt.nodes.new('ShaderNodeTexImage')
    nt.nodes.active = tn
    bpy.ops.object.select_all(action='DESELECT'); hi.select_set(True); lo.select_set(True); bpy.context.view_layer.objects.active = lo
    bk = sc.render.bake
    bk.use_selected_to_active = True; bk.cage_extrusion = 0.012; bk.max_ray_distance = 0.05; bk.margin = max(4, res // 128)
    bk.use_clear = True
    def run(kind, img, samples, **kw):
        tn.image = img; sc.cycles.samples = samples
        bpy.ops.object.bake(type=kind, **kw)
    for ch in ('col', 'rough', 'metal', 'ao'):
        for m in mats: set_tap(m, ch)
        run('EMIT', imgs[ch], 1)
    for m in mats: set_tap(m, None)
    run('NORMAL', imgs['nrm'], max(8, SAMPLES), normal_space='TANGENT')
    sc.world = sc.world or bpy.data.worlds.new('W'); sc.world.light_settings.distance = 0.35
    run('AO', imgs['gao'], max(16, SAMPLES * 2))
    def arr(im):
        a = np.empty(res * res * 4, np.float32); im.pixels.foreach_get(a); return a.reshape(res, res, 4)
    col = arr(imgs['col'])[..., :3]
    ao = np.clip(arr(imgs['ao'])[..., 0] * (0.35 + 0.65 * arr(imgs['gao'])[..., 0]), 0, 1)
    orm = np.dstack([ao, arr(imgs['rough'])[..., 0], arr(imgs['metal'])[..., 0]])
    paths = dict(col=os.path.join(d, name + '_BaseColor.png'), nrm=os.path.join(d, name + '_Normal.png'), orm=os.path.join(d, name + '_ORM.png'))
    save_np(col, paths['col'], 'sRGB')
    save_np(arr(imgs['nrm'])[..., :3], paths['nrm'], 'Non-Color')
    save_np(orm, paths['orm'], 'Non-Color')
    low_material(lm, paths)
    return paths

def low_material(lm, paths):
    """Low material for review and the FBX (URP Lit gets its maps on import; the FBX only names them)."""
    lm.node_tree.nodes.clear(); t = NT(lm)
    c = t.image(paths['col'], 'sRGB'); o_ = t.image(paths['orm'], 'Non-Color'); n_ = t.image(paths['nrm'], 'Non-Color')
    sep = t.new('ShaderNodeSeparateColor'); t.link(o_.outputs['Color'], sep.inputs[0])
    nm = t.new('ShaderNodeNormalMap'); t.link(n_.outputs['Color'], nm.inputs['Color'])
    bs = t.new('ShaderNodeBsdfPrincipled')
    # AO into the review render only through the base colour multiply (URP applies the occlusion map the same way)
    mul = t.mixc(1.0, c.outputs['Color'], sep.outputs[0], blend='MULTIPLY')
    t.link(mul, bs.inputs['Base Color']); t.link(sep.outputs[1], bs.inputs['Roughness']); t.link(sep.outputs[2], bs.inputs['Metallic'])
    t.link(nm.outputs[0], bs.inputs['Normal'])
    out = t.new('ShaderNodeOutputMaterial'); t.link(bs.outputs[0], out.inputs['Surface'])

def export(lo, name, p, paths):
    d = os.path.join(OUT, name); fbx = os.path.join(d, name + '.fbx')
    bpy.ops.object.select_all(action='DESELECT'); lo.select_set(True); bpy.context.view_layer.objects.active = lo
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False, path_mode='STRIP')
    if not EXPORT: return
    dd = os.path.join(DEST, name); os.makedirs(dd, exist_ok=True)
    for f in [fbx] + list(paths.values()): shutil.copy(f, os.path.join(dd, os.path.basename(f)))
    with open(os.path.join(dd, 'SOURCES.md'), 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(sources_md(name, p, lo))

CREDIT = {'wooden_barrels_01': 'James Ray Cock', 'wine_barrel_01': 'James Ray Cock', 'wooden_crate_01': 'James Ray Cock',
          'wooden_crate_02': 'James Ray Cock, Jurita Burger', 'wooden_bucket_01': 'James Ray Cock', 'wooden_bucket_02': 'James Ray Cock',
          'wicker_basket_01': 'Kuutti Siitonen', 'wicker_basket_02': 'Kuutti Siitonen', 'jug_01': 'Kuutti Siitonen',
          'ceramic_vase_02': 'James Ray Cock'}

def sources_md(name, p, lo):
    tris = sum(len(f.vertices) - 2 for f in lo.data.polygons)
    mn, mx = bounds(lo)
    res = int(p['res'] * RES_SCALE)
    head = '# %s (B7 beauty-corner props)\n\nGenerated by `Art/Blender/town/sc_props_adopt.py` (see its header for the run line).\n\n' % name
    stats = ('- Size (Blender x, y, z = Unity x, z, y): %.2f x %.2f x %.2f m; origin bottom centre, front +z in Unity.\n'
             '- %d tris, one material; maps %d px: `%s_BaseColor` (sRGB), `%s_Normal` (OpenGL, Y+), `%s_ORM` (R occlusion, G roughness, B metallic; linear).\n'
             % (mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2], tris, res, name, name, name))
    if p.get('gen'):
        src = ('- Source: original, generated in the script (no external mesh). Burlap regraded from the shared B1 canvas tileable '
               '(`Art/Textures/B1_town/canvas`, its own sources in `Art/Textures/B1_town/SOURCES.md`).\n- Licence: ours.\n')
    else:
        parts = ', '.join(p['parts']) if p['parts'] else 'all parts'
        src = ('- Source: Poly Haven `%s` (https://polyhaven.com/a/%s), %s; raw files in `Art/Source/PolyHaven/%s/`.\n'
               '- Licence: CC0 1.0 (https://polyhaven.com/license). Author: %s. No attribution required; credited here anyway.\n'
               '- What we changed: %s\n'
               % (p['src'], p['src'], parts, p['src'], CREDIT.get(p['src'], 'see Art/Source/PolyHaven/SOURCES.md'), changes(p)))
    return head + src + stats

def changes(p):
    """The 'what we changed' line of an adopted asset's SOURCES.md, from its table row."""
    shape = ['bulge', 'lean', 'wobble'] + (['iron hoops thickened and pushed proud'] if p.get('rings') else ['iron bands pushed proud'] if p.get('hoop', 0) > 0 else []) \
        + (['lid and top boards sag'] if p.get('sag') else []) + (['lid leaned against its side'] if p.get('leaned_lid') else [])
    if 'clay' in p:
        look = 'regraded to clay with a slip glaze over the shoulder ending in runs'
    else:
        look = ('regraded to a warm straw (PHASE1 3.1)' if p.get('basket') else 'regraded to PHASE1 3.1 (wood and iron separately)') + (', a faded ochre painted band' if 'paint' in p else '') \
            + (', its stencilled letters painted out' if p.get('erase') else '')
    wear = (['weave tone', 'grime in the weave'] if p.get('basket') else ['board tone', 'joint grime']) if p.get('tone') else (['cavity grime'] if 'clay' in p else [])
    wear += (['sun-bleached tops'] if p.get('bleach') else []) + (['rust at the foot'] if p.get('rings') or p.get('hoop', 0) > 0 else []) \
        + ['polished rims', 'dirt splash at the base']
    pr = ''
    if 'produce' in p:
        q = p['produce']
        kinds = [k for k, n in (('apples', q['n_apple']), ('turnips', q['n_turnip']), ('squash', q.get('n_squash', 0)), ('loaves', q['n_loaf'])) if n]
        pr = ' Generated produce (%s)%s added.' % (', '.join(kinds), ' on a straw bed' if q['kind'] in ('crate', 'basket') else '')
    return ('scaled %.0f%%, reshaped (%s), albedo %s, placed wear (%s), decimated and rebaked onto the source UVs '
            '(generated parts packed beside them).%s' % ((OVERSIZE - 1) * 100, ', '.join(shape), look, ', '.join(wear), pr))

# ---------------------------------------------------------------- review renders
def review_scene():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; setup_cycles(); sc.cycles.samples = 48; sc.cycles.use_denoising = True
    sc.render.resolution_x = RRES; sc.render.resolution_y = int(RRES * 0.8); sc.render.film_transparent = False
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
    w = sc.world or bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    wn = w.node_tree.nodes; bg = wn.get('Background') or wn.new('ShaderNodeBackground')
    bg.inputs['Color'].default_value = (0.42, 0.55, 0.72, 1.0); bg.inputs['Strength'].default_value = 0.5
    # cobble ground (shared B1 tileable) so the props sit on what they will sit on
    bpy.ops.mesh.primitive_plane_add(size=30); g = bpy.context.active_object; g.name = 'REV_ground'
    gm = bpy.data.materials.new('REV_cobble'); gm.use_nodes = True; gm.node_tree.nodes.clear(); t = NT(gm)
    mp = t.new('ShaderNodeMapping'); t.link(t.new('ShaderNodeTexCoord').outputs['Object'], mp.inputs['Vector']); mp.inputs['Scale'].default_value = (0.5, 0.5, 0.5)
    ims = {}
    for k in ('albedo', 'orm', 'normal'):
        nd = t.new('ShaderNodeTexImage'); nd.image = bpy.data.images.load(os.path.join(TEX, 'cobble', 'cobble_%s.png' % k), check_existing=True)
        nd.image.colorspace_settings.name = 'sRGB' if k == 'albedo' else 'Non-Color'; t.link(mp.outputs[0], nd.inputs['Vector']); ims[k] = nd
    sep = t.new('ShaderNodeSeparateColor'); t.link(ims['orm'].outputs['Color'], sep.inputs[0])
    nm = t.new('ShaderNodeNormalMap'); t.link(ims['normal'].outputs['Color'], nm.inputs['Color'])
    bs = t.new('ShaderNodeBsdfPrincipled'); t.link(ims['albedo'].outputs['Color'], bs.inputs['Base Color']); t.link(sep.outputs[1], bs.inputs['Roughness'])
    t.link(nm.outputs[0], bs.inputs['Normal']); o = t.new('ShaderNodeOutputMaterial'); t.link(bs.outputs[0], o.inputs['Surface'])
    g.data.materials.append(gm)
    # back wall in the plaster tan (the facade sits behind the cluster)
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 4.0, 0), rotation=(math.pi / 2, 0, 0)); wl = bpy.context.active_object; wl.name = 'REV_wall'
    wm = bpy.data.materials.new('REV_plaster'); wm.use_nodes = True
    wm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*hex2lin('#cbbd9c'), 1); wm.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.9
    wl.data.materials.append(wm)
    # midday sun about 5200 K from the south-east, cool sky fill from the world
    sd = bpy.data.lights.new('REV_sun', 'SUN'); sd.energy = 5.5; sd.color = (1.0, 0.9, 0.78); sd.angle = math.radians(1.5)
    so = bpy.data.objects.new('REV_sun', sd); sc.collection.objects.link(so); so.rotation_euler = (math.radians(42), 0, math.radians(-35))
    cam = bpy.data.cameras.new('REV_cam'); co = bpy.data.objects.new('REV_cam', cam); sc.collection.objects.link(co); sc.camera = co
    return co

def figure():
    """1.75 m scale figure (a plain capsule with a head)."""
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.45, location=(0, 0, 0.725)); b = bpy.context.active_object
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.14, location=(0, 0, 1.61)); h = bpy.context.active_object
    m = bpy.data.materials.new('REV_fig'); m.use_nodes = True; m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.25, 0.27, 1)
    for o in (b, h): o.data.materials.append(m)
    return join([b, h], 'REV_figure')

def aim(co, pos, look, lens):
    co.location = Vector(pos); d = Vector(look) - Vector(pos)
    co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); co.data.lens = lens

def render(path):
    bpy.context.scene.render.filepath = path; bpy.ops.render.render(write_still=True)

# ---------------------------------------------------------------- main
def main():
    bpy.ops.wm.read_homefile(use_empty=True)
    names = [n for n in ASSETS if ONLY is None or n in ONLY]
    lows = {}
    for name in (names if RENDER_ONLY else []):
        fbx = os.path.join(OUT, name, name + '.fbx')
        if not os.path.exists(fbx): continue
        before = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=fbx)
        lo = [o for o in bpy.data.objects if o not in before and o.type == 'MESH'][0]
        bpy.context.view_layer.update(); lo.data.transform(lo.matrix_world); lo.matrix_world = Matrix(); lo.name = name
        if '--sources-only' in argv:   # rewrite the exported SOURCES.md from the table without rebaking
            p = dict(ASSETS[name]); p['_name'] = name; dd = os.path.join(DEST, name)
            if os.path.isdir(dd):
                with open(os.path.join(dd, 'SOURCES.md'), 'w', encoding='utf-8', newline=chr(10)) as fh: fh.write(sources_md(name, p, lo))
            continue
        lm = bpy.data.materials.new('R_' + name); lm.use_nodes = True; lo.data.materials.clear(); lo.data.materials.append(lm)
        d = os.path.join(OUT, name)
        low_material(lm, dict(col=os.path.join(d, name + '_BaseColor.png'), nrm=os.path.join(d, name + '_Normal.png'),
                              orm=os.path.join(d, name + '_ORM.png')))
        lo.hide_render = True; lows[name] = lo
    for name in ([] if RENDER_ONLY else names):
        p = dict(ASSETS[name]); os.makedirs(os.path.join(OUT, name), exist_ok=True)
        hi, H = build_high(name, p)
        lo = make_low(hi, name, p)
        res = int(p['res'] * RES_SCALE)
        paths = bake(hi, lo, name, res)
        export(lo, name, p, paths)
        tris = sum(len(f.vertices) - 2 for f in lo.data.polygons)
        mn, mx = bounds(lo)
        print('B7 %-14s tris %5d  size %.2f x %.2f x %.2f  res %d' % (name, tris, mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2], res))
        bpy.data.objects.remove(hi, do_unlink=True)
        lo.hide_render = True; lows[name] = lo
    if not RENDER or '--sources-only' in argv: return
    rd = os.path.join(OUT, 'renders'); os.makedirs(rd, exist_ok=True)
    co = review_scene()
    for name, lo in lows.items():
        lo.hide_render = False; lo.location = (0, 0, 0)
        mn, mx = bounds(lo); c = Vector(((mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, (mn[2] + mx[2]) / 2)); s = max(mx - mn)
        d = s * 2.4 + 0.4
        aim(co, (c.x + d * 0.62, c.y - d * 0.85, c.z + d * 0.62), c, 50)
        render(os.path.join(rd, '%s_tq.png' % name))
        lo.hide_render = True
    # group: the cluster as the mid camera sees it (by the stall and the facade door), 1.75 m figure for scale
    # odd groups to one side against the wall (y 4): a barrel pair plus one, sacks slumped together, baskets on crates (brief 5)
    layout = {'Barrel_A': (-1.2, 3.5, 10), 'Barrel_B': (-0.35, 3.55, 40), 'Barrel_C': (-0.8, 2.75, 0),
              'Sack_Pair': (-2.35, 3.4, 15), 'Sack_A': (-2.0, 2.65, 0), 'Sack_B': (-2.75, 2.55, 40), 'Sack_C': (-1.55, 2.15, 0),
              'Crate_A': (0.55, 2.95, 8), 'Crate_B': (1.45, 3.55, 92), 'Crate_C': (1.65, 2.45, -15),
              'Bucket_A': (-0.15, 2.45, 0), 'Bucket_B': (2.7, 3.35, 0),
              'Basket_Lidded': (0.55, 2.95, 20), 'Basket_Produce': (1.45, 3.55, -10), 'Basket_Bread': (2.55, 2.3, 0),
              'Jug_A': (-0.45, 2.15, 30), 'Jug_B': (0.15, 2.2, 0)}
    stack = {'Basket_Lidded': 'Crate_A', 'Basket_Produce': 'Crate_B'}
    for name, lo in lows.items():
        x, y, rz = layout.get(name, (0, 0, 0)); z = 0.0
        if name in stack and stack[name] in lows:
            z = float(bounds(lows[stack[name]])[1][2])
        lo.location = (x, y, z); lo.rotation_euler = (0, 0, math.radians(rz)); lo.hide_render = False
    f = figure(); f.location = (3.3, 2.4, 0)
    aim(co, (2.9, -0.7, 1.7), (-0.2, 3.0, 0.45), 30)
    render(os.path.join(rd, 'group.png'))
    aim(co, (1.5, 0.6, 1.25), (0.3, 3.0, 0.45), 35)
    render(os.path.join(rd, 'group_close.png'))

main()
