# sc_asset_preview.py (2026-10-25, asset skills): a contact sheet of SleepingCalamity model exports as the game draws them, for review and skill hillclimbing.
# Read-only for the repo: it imports the models, renders, and writes one PNG where you point it (use the session scratchpad, not the repo).
#
# Run:  blender -b --factory-startup --python Art/Blender/tools/sc_asset_preview.py -- <out.png> [--season summer] [--size 512] [--paint 1] [--grain 1]
#           [--ref human,door,wall] <model.fbx|.glb|a.fbx+a_Tri.fbx> [...]
#   --water 1 lays an opaque lake surface at the models' y 0 (waterline props, swimming birds: what is under the water hides).
#   --light neutral greys the light's colours (same values): judge a material's hue (is this timber grey or blue?) under it; score and
#   concept-match in the default game light, which the scorer's baselines use.
#   --shadows 1 adds the sun's cast shadows (and contact shadows on the ground), at 3x the render time.
#   --seasons 'spring=a.fbx;summer=a.fbx+a_Flowers.fbx;autumn=a.fbx+a_Pods.fbx;winter=a_Winter.fbx' renders each season with its own palette and
#   model set and writes a 4 x 2 montage to <out> (three-quarter on top, play distance below; per-season sheets beside it). Not a scored sheet.
#   --inside 1 also writes <out>_inside.png, a 2 x 1 sheet looking top-down and 60 deg down into the model (boats, carts, troughs: the insides
#   the close views miss); the scored 2 x 2 sheet is unchanged.
#   --play-yaw <deg> turns only the 48 px play-distance camera (e.g. 55 for a side profile from the 35 deg three-quarter): things read by
#   profile from the bank (a heron's bill and crest, a boat's sheer). Default 0 (the scored sheet as before).
#   --yaw <deg> turns every model about the vertical for the views only (e.g. 90 for a wall run authored along +z, which the three-quarter view
#   otherwise sees end-on).
#   --season spring|summer|autumn|winter: the slot colours from ArtPalette.asset (an unknown season stops with an error, never a summer fallback).
#   --atlas <png> draws foliage cards with a scratch atlas instead of the repo's T_MillpondFoliage.png (a new leaf cell before it ships); without
#   it, SC_OUT=<dir> picks up <dir>/T_MillpondFoliage.png when sc_millpond_trees built one there (use_wide / --out).
#   --ref adds grey scale references at real size: a 1.8 m figure beside the model, a 3 x 2.6 m wall behind it with a 0.95 x 1.95 m door
#   (the door sits in the wall, so 'door' draws the wall too; use 'human' alone for no wall).
# Several models are laid out side by side on a ground disc (a kit, or before/after); join the halves of one model with '+'
# (Millpond_Bldg_x.fbx+Millpond_Bldg_x_Tri.fbx) so they share their origin. The sheet has four panels:
#   three_quarter, side, back_three_quarter: close views (the model fills the frame)
#   play_distance: the same model at about 48 px tall, upscaled with nearest filtering: what a player sees from a stand or the tug deck (spec R79).
#
# Shading approximates SleepingCalamity/Toon in the Millpond look (R102 Riverlight): albedo = palette[uv1.x] x 2 x Col (the half-scale vertex colour) x
# painterly drift (SC_PaintVariation: brush-scale value and warm/cool, 3D world noise) lit by the soft world light: wrapped Lambert (wrap 0.2) from a
# warm sun plus a sky hemisphere ambient x the cool shade, plus a faint rim. It is drawn as emission (no shadows, no texture-array detail), so judge
# shape, value structure, colour and silhouette here, and the final grain in Unity. The texture-array grain IS drawn (T_MillpondDetailArray, the
# face's uv1.y layer, uv0 x 0.6 or world triplanar for *_Tri models; --grain 0 turns it off). A model without uv1 "Layer" / Col (a GLB under study) falls back
# to its own base colour (or grey) with the same lighting.
import bpy, math, os, re, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))

PALETTE_ASSET = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Art', 'ArtPalette.asset')

def palette(season):
    # The season's 16 slot colours (sRGB) from ArtPalette.asset (ArtPaletteDef: slotNames, then seasons[] each with a name and colors[]), the
    # palette the game draws with. Parsed as text (no YAML module in Blender's Python). Fails loudly on an unknown season or a palette it can't
    # read: an earlier version read sc_harbour.PALETTE_<SEASON>, which only has summer, and silently rendered every season in summer colours.
    src = open(PALETTE_ASSET, encoding='utf-8').read()
    names = re.findall(r'^  - name: (\w+)\s*$', src, re.M)
    blocks = re.split(r'^  - name: \w+\s*$', src, flags=re.M)[1:]
    for name, blk in zip(names, blocks):
        if name.lower() != season.lower():
            continue
        m = re.search(r'^    colors:\s*\n((?:    - \{[^\n]*\}\s*\n)+)', blk, re.M)
        cols = [tuple(float(x) for x in t) for t in re.findall(r'r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)', m.group(1))] if m else []
        if len(cols) != 16:
            raise SystemExit('sc_asset_preview: %s has %d colours for season %s, expected 16' % (PALETTE_ASSET, len(cols), name))
        return cols
    raise SystemExit('sc_asset_preview: no season %r in %s (has %s)' % (season, PALETTE_ASSET, ', '.join(names)))

def lin(c):
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

SUN_DIR = Vector((0.25, -0.65, 0.72)).normalized()    # towards the sun (Blender z up): high, in front of the model, a little right of the three-quarter camera
SUN = lin((1.0, 0.93, 0.80))                            # warm sun (MillpondLayout.Look sunTint toward (1, 0.89, 0.72))
SKY_TOP, SKY_GROUND = lin((0.70, 0.80, 0.92)), lin((0.50, 0.52, 0.44))
SHADE = lin((0.70, 0.74, 0.90))                         # cool coloured shade (the grade's bottom tint folded in)
WRAP, AMBIENT, RIM = 0.2, 1.3, 0.10                     # MillpondLayout: SoftLightWrap 0.2, SoftAmbientGain 1.3

def neutral_light():
    # --light neutral (2026-10-25): the same light with every colour swapped for a grey of its own luminance (warm sun, cool sky and shade
    # made white-ish), so a neutral material renders neutral. For judging a material's hue: in the game light a neutral grey timber reads
    # blue-grey (the sky ambient and the cool shade), which caused two false 'blue-grey' reads (the beacon, the lock gate). Values and the
    # light/shade split are unchanged; the scorer's baselines were made in the game light, so score in the default.
    global SUN, SKY_TOP, SKY_GROUND, SHADE
    grey = lambda c: (0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2],) * 3
    SUN, SKY_TOP, SKY_GROUND, SHADE = grey(SUN), grey(SKY_TOP), grey(SKY_GROUND), grey(SHADE)

def node(nt, kind, **kw):
    n = nt.nodes.new(kind)
    for k, v in kw.items():
        setattr(n, k, v)
    return n

def math_node(nt, op, a=None, b=None, clamp=False):
    n = nt.nodes.new('ShaderNodeMath'); n.operation = op; n.use_clamp = clamp
    if a is not None:
        if isinstance(a, (int, float)): n.inputs[0].default_value = a
        else: nt.links.new(a, n.inputs[0])
    if b is not None:
        if isinstance(b, (int, float)): n.inputs[1].default_value = b
        else: nt.links.new(b, n.inputs[1])
    return n.outputs[0]

def mix_rgb(nt, blend, a, b, fac=1.0):
    n = nt.nodes.new('ShaderNodeMix'); n.data_type = 'RGBA'; n.blend_type = blend
    n.inputs['Factor'].default_value = fac
    for sock, v in (('A', a), ('B', b)):
        if isinstance(v, tuple): n.inputs[sock].default_value = v + (1.0,) if len(v) == 3 else v
        else: nt.links.new(v, n.inputs[sock])
    return n.outputs['Result']

ARRAY_PATH = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Textures', 'T_MillpondDetailArray.png')
ARRAY_COLS, ARRAY_ROWS, TILING = 4, 8, 0.6   # TextureArrayPostprocessor (layer 0 = top-left, row by row); RenderMaterialLibrary.EnvTiling

def grain(nt, layer_out, uv0, geo, tri):
    # The detail array as SleepingCalamity/Toon samples it: tile = round(uv1.y), uv = uv0 x 0.6 (M_Toon_Base) or world triplanar x 0.6 blended by
    # |n|^4 (M_Toon_Tri), factor = 2 x sample (the grey-centred map; _FGArrayGain 2, _FGTexWeight 1). Returns a colour socket (or None).
    if not os.path.exists(ARRAY_PATH):
        return None
    img = bpy.data.images.get('SC_DetailArray') or bpy.data.images.load(ARRAY_PATH)
    img.name = 'SC_DetailArray'; img.colorspace_settings.name = 'Non-Color'
    lay = math_node(nt, 'ROUND', layer_out)
    col = math_node(nt, 'FLOORED_MODULO', lay, float(ARRAY_COLS))
    row = math_node(nt, 'FLOOR', math_node(nt, 'DIVIDE', lay, float(ARRAY_COLS)))
    vrow = math_node(nt, 'SUBTRACT', float(ARRAY_ROWS - 1), row)            # Blender v runs bottom-up
    def sample(u, v):
        fu = math_node(nt, 'FRACT', math_node(nt, 'MULTIPLY', u, TILING))
        fv = math_node(nt, 'FRACT', math_node(nt, 'MULTIPLY', v, TILING))
        fu = math_node(nt, 'ADD', math_node(nt, 'MULTIPLY', fu, 0.98), 0.01)   # keep off the tile edges (no bleed into the neighbour)
        fv = math_node(nt, 'ADD', math_node(nt, 'MULTIPLY', fv, 0.98), 0.01)
        tu = math_node(nt, 'DIVIDE', math_node(nt, 'ADD', col, fu), float(ARRAY_COLS))
        tv = math_node(nt, 'DIVIDE', math_node(nt, 'ADD', vrow, fv), float(ARRAY_ROWS))
        cmb = nt.nodes.new('ShaderNodeCombineXYZ'); nt.links.new(tu, cmb.inputs[0]); nt.links.new(tv, cmb.inputs[1])
        tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Linear'; tex.extension = 'EXTEND'
        nt.links.new(cmb.outputs[0], tex.inputs['Vector'])
        return tex.outputs['Color']
    if not tri:
        sp = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(uv0, sp.inputs[0])
        s0 = sample(sp.outputs['X'], sp.outputs['Y'])
    else:
        pos = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(geo.outputs['Position'], pos.inputs[0])
        nrm = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(geo.outputs['Normal'], nrm.inputs[0])
        sx = sample(pos.outputs['Y'], pos.outputs['Z']); sy = sample(pos.outputs['X'], pos.outputs['Z']); sz = sample(pos.outputs['X'], pos.outputs['Y'])
        w = [math_node(nt, 'POWER', math_node(nt, 'ABSOLUTE', nrm.outputs[k]), 4.0) for k in ('X', 'Y', 'Z')]
        tot = math_node(nt, 'ADD', math_node(nt, 'ADD', w[0], w[1]), w[2])
        acc = None
        for smp, wk in zip((sx, sy, sz), w):
            f = math_node(nt, 'DIVIDE', wk, tot)
            m = mix_rgb(nt, 'MULTIPLY', smp, (1.0, 1.0, 1.0))
            cm = nt.nodes.new('ShaderNodeCombineXYZ')
            for i in range(3): nt.links.new(f, cm.inputs[i])
            m = mix_rgb(nt, 'MULTIPLY', smp, cm.outputs[0])
            acc = m if acc is None else mix_rgb(nt, 'ADD', acc, m)
        s0 = acc
    return mix_rgb(nt, 'MULTIPLY', s0, (2.0, 2.0, 2.0))

CARD_ATLAS = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Textures', 'T_MillpondFoliage.png')

def preview_material(pal, paint, encoded, base=None, tri=False, use_grain=True, cards=False):
    m = bpy.data.materials.new('SC_AssetPreview_%s' % ('enc' if encoded else 'raw'))
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = node(nt, 'ShaderNodeOutputMaterial')
    if encoded:
        uvn = node(nt, 'ShaderNodeUVMap', uv_map='Layer')
        sep = node(nt, 'ShaderNodeSeparateXYZ'); nt.links.new(uvn.outputs['UV'], sep.inputs[0])
        f = math_node(nt, 'MULTIPLY_ADD', sep.outputs['X'], 1.0 / 16.0); f.node.inputs[2].default_value = 0.5 / 16.0
        ramp = node(nt, 'ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'CONSTANT'
        els = ramp.color_ramp.elements
        while len(els) < len(pal):
            els.new(0.5)
        for i, c in enumerate(pal):
            els[i].position = i / 16.0; els[i].color = lin(c) + (1.0,)
        nt.links.new(f, ramp.inputs['Fac'])
        vc = node(nt, 'ShaderNodeVertexColor', layer_name='Col')
        two = mix_rgb(nt, 'MULTIPLY', vc.outputs['Color'], (2.0, 2.0, 2.0))
        albedo = mix_rgb(nt, 'MULTIPLY', ramp.outputs['Color'], two)
        card_alpha = None
        if cards and os.path.exists(CARD_ATLAS):
            # M_Millpond_Foliage: albedo x atlas rgb x _CardGain 2, clipped at alpha 0.5 (no texture-array grain on cards)
            img = bpy.data.images.get('SC_CardAtlas') or bpy.data.images.load(CARD_ATLAS)
            img.name = 'SC_CardAtlas'; img.colorspace_settings.name = 'Non-Color'
            uvc = node(nt, 'ShaderNodeUVMap', uv_map='UVMap')
            tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Linear'
            uv_out = uvc.outputs['UV']
            if img.size[0] == 2 * img.size[1]:
                # the wide atlas (sc_millpond_trees ATLAS_WIDE): M_Millpond_Foliage's _BaseMap_ST (0.5, 1, 0, 0) maps authored u 0..2 onto it
                sc = nt.nodes.new('ShaderNodeVectorMath'); sc.operation = 'MULTIPLY'; sc.inputs[1].default_value = (0.5, 1.0, 1.0)
                nt.links.new(uv_out, sc.inputs[0]); uv_out = sc.outputs['Vector']
            nt.links.new(uv_out, tex.inputs['Vector'])
            albedo = mix_rgb(nt, 'MULTIPLY', albedo, mix_rgb(nt, 'MULTIPLY', tex.outputs['Color'], (2.0, 2.0, 2.0)))
            card_alpha = math_node(nt, 'GREATER_THAN', tex.outputs['Alpha'], 0.5)
            use_grain = False
        if use_grain:
            uv0n = node(nt, 'ShaderNodeUVMap', uv_map='UVMap')
            g = grain(nt, sep.outputs['Y'], uv0n.outputs['UV'], nt.nodes.new('ShaderNodeNewGeometry'), tri)
            if g is not None:
                albedo = mix_rgb(nt, 'MULTIPLY', albedo, g)
    else:
        if isinstance(base, bpy.types.Image):   # a textured source model (adopt studies): its base-colour texture on its first UV map
            tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = base
            albedo = mix_rgb(nt, 'MULTIPLY', tx.outputs['Color'], (1.0, 1.0, 1.0))
        else:
            albedo = mix_rgb(nt, 'MULTIPLY', base or (0.55, 0.55, 0.55), (1.0, 1.0, 1.0))
    geo = node(nt, 'ShaderNodeNewGeometry')
    if paint > 0:
        # SC_PaintVariation: value from two octaves at 0.7 m and 2.3 m, warm/cool at 1.6 m (the 140 m field is flat at asset scale)
        n1 = node(nt, 'ShaderNodeTexNoise'); n1.inputs['Scale'].default_value = 1.0 / 0.7; n1.inputs['Detail'].default_value = 0.0
        n2 = node(nt, 'ShaderNodeTexNoise'); n2.inputs['Scale'].default_value = 1.0 / 2.3; n2.inputs['Detail'].default_value = 0.0
        n3 = node(nt, 'ShaderNodeTexNoise'); n3.inputs['Scale'].default_value = 1.0 / 1.6; n3.inputs['Detail'].default_value = 0.0
        for n in (n1, n2, n3):
            nt.links.new(geo.outputs['Position'], n.inputs['Vector'])
        v = math_node(nt, 'ADD', math_node(nt, 'MULTIPLY', n1.outputs['Fac'], 0.45), math_node(nt, 'MULTIPLY', n2.outputs['Fac'], 0.55))
        v = math_node(nt, 'ADD', math_node(nt, 'MULTIPLY', math_node(nt, 'SUBTRACT', v, 0.5), 0.20 * paint * 2.0), 1.0)   # Blender noise spans less than value noise: x2
        comb = node(nt, 'ShaderNodeCombineXYZ'); nt.links.new(v, comb.inputs[0]); nt.links.new(v, comb.inputs[1]); nt.links.new(v, comb.inputs[2])
        albedo = mix_rgb(nt, 'MULTIPLY', albedo, comb.outputs[0])
        warm = mix_rgb(nt, 'MIX', (0.88, 0.98, 1.06), (1.10, 1.04, 0.82)); nt.links.new(n3.outputs['Fac'], warm.node.inputs['Factor'])
        tint = mix_rgb(nt, 'MIX', (1.0, 1.0, 1.0), warm, 0.16 * paint * 1.5)
        albedo = mix_rgb(nt, 'MULTIPLY', albedo, tint)
    # soft world light: diffuse = saturate((n.l + w) / (1 + w)); ambient = lerp(ground, top, n.z/2+.5) x shade x AMBIENT; rim
    ndl = node(nt, 'ShaderNodeVectorMath'); ndl.operation = 'DOT_PRODUCT'
    nt.links.new(geo.outputs['Normal'], ndl.inputs[0]); ndl.inputs[1].default_value = tuple(SUN_DIR)
    dif = math_node(nt, 'DIVIDE', math_node(nt, 'ADD', ndl.outputs['Value'], WRAP), 1.0 + WRAP, clamp=True)
    comb = node(nt, 'ShaderNodeCombineXYZ')
    for i in range(3): nt.links.new(dif, comb.inputs[i])
    sunlit = mix_rgb(nt, 'MULTIPLY', SUN, comb.outputs[0])
    sepn = node(nt, 'ShaderNodeSeparateXYZ'); nt.links.new(geo.outputs['Normal'], sepn.inputs[0])
    up = math_node(nt, 'MULTIPLY_ADD', sepn.outputs['Z'], 0.5); up.node.inputs[2].default_value = 0.5
    hemi = mix_rgb(nt, 'MIX', SKY_GROUND, SKY_TOP); nt.links.new(up, hemi.node.inputs['Factor'])
    amb = mix_rgb(nt, 'MULTIPLY', hemi, tuple(x * AMBIENT for x in SHADE))
    light = mix_rgb(nt, 'ADD', sunlit, amb)
    lit = mix_rgb(nt, 'MULTIPLY', albedo, light)
    lw = node(nt, 'ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = 0.35
    rim = mix_rgb(nt, 'MIX', lit, SKY_TOP, 0.0); nt.links.new(math_node(nt, 'MULTIPLY', lw.outputs['Facing'], RIM), rim.node.inputs['Factor'])
    em = node(nt, 'ShaderNodeEmission'); nt.links.new(rim, em.inputs['Color'])
    if encoded and card_alpha is not None:
        tr = node(nt, 'ShaderNodeBsdfTransparent'); mx = node(nt, 'ShaderNodeMixShader')
        nt.links.new(card_alpha, mx.inputs['Fac']); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2])
        nt.links.new(mx.outputs[0], out.inputs['Surface'])
    else:
        nt.links.new(em.outputs[0], out.inputs['Surface'])
    return m

def import_model(path):
    before = set(bpy.data.objects)
    ext = os.path.splitext(path)[1].lower()
    if ext == '.fbx': bpy.ops.import_scene.fbx(filepath=path, colors_type='LINEAR')   # the kit exports Col as LINEAR (export_fbx); the default SRGB would re-linearise it
    elif ext in ('.glb', '.gltf'): bpy.ops.import_scene.gltf(filepath=path)
    elif ext == '.obj': bpy.ops.wm.obj_import(filepath=path)
    bpy.context.view_layer.update()
    return [o for o in bpy.data.objects if o not in before and o.type == 'MESH']

def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi

def seasons_montage(out, spec, rest):
    # --seasons 'spring=a.fbx;summer=a.fbx+a_Flowers.fbx;autumn=a.fbx+a_Pods.fbx;winter=a_Winter.fbx' (duo round 30): one preview per season with
    # that season's palette and model set, then a 4 x 2 montage: the three-quarter view (top row) and the play-distance view (bottom row) in
    # spring, summer, autumn, winter order. The per-season sheets are kept beside it (<out>_<season>.png); none of them is the scored sheet.
    import tempfile
    order = ['spring', 'summer', 'autumn', 'winter']
    sets = dict(kv.split('=', 1) for kv in spec.split(';') if '=' in kv)
    size = int(rest[rest.index('--size') + 1]) if '--size' in rest else 512
    base = [a for a in rest if a not in ('--season',)]
    sheets = []
    for sn in order:
        if sn not in sets:
            continue
        o = os.path.splitext(out)[0] + '_' + sn + '.png'
        main([o, '--season', sn] + base + [sets[sn]])   # each run resets the scene, so the sheets are loaded after the last one
        sheets.append(o)
    sheets = [bpy.data.images.load(o) for o in sheets]
    W, H = size * len(sheets), size * 2
    px = [0.0] * (W * H * 4)
    for k, im in enumerate(sheets):
        iw = im.size[0]; src = list(im.pixels)
        for row, (sx0, sy0) in enumerate(((0, size), (size, 0))):   # three_quarter = top-left, play_distance = bottom-right (Blender bottom-up)
            oy = (1 - row) * size
            for yy in range(size):
                si = ((sy0 + yy) * iw + sx0) * 4; di = ((oy + yy) * W + k * size) * 4
                px[di:di + size * 4] = src[si:si + size * 4]
    m = bpy.data.images.new('seasons', W, H, alpha=False); m.pixels.foreach_set(px)
    m.filepath_raw = out; m.file_format = 'PNG'; m.save()
    print('SEASONS', out, [sn for sn in order if sn in sets])

def main(argv):
    if '--seasons' in argv:
        i = argv.index('--seasons'); spec = argv[i + 1]
        rest = argv[1:i] + argv[i + 2:]
        return seasons_montage(argv[0], spec, [a for a in rest if not a.endswith(('.fbx', '.glb'))])
    out = argv[0]; rest = argv[1:]
    def opt(name, default, cast=str):
        if name in rest:
            i = rest.index(name); v = rest[i + 1]; del rest[i:i + 2]; return cast(v)
        return default
    season = opt('--season', 'summer'); size = opt('--size', 512, int); paint = opt('--paint', 1.0, float)
    global CARD_ATLAS
    sc_out = os.environ.get('SC_OUT')                              # sc_millpond_trees' scratch outputs (use_wide / --out): their atlas, if built
    if sc_out and os.path.exists(os.path.join(sc_out, 'T_MillpondFoliage.png')):
        CARD_ATLAS = os.path.join(sc_out, 'T_MillpondFoliage.png')
    CARD_ATLAS = opt('--atlas', CARD_ATLAS)                       # a scratch foliage atlas for the cards (default: SC_OUT's, else the repo's)
    print('ATLAS', CARD_ATLAS)
    use_grain = opt('--grain', 1, int) > 0
    refs = [r for r in opt('--ref', '', str).split(',') if r]   # human, door, wall: scale references beside / behind the model
    shadows = opt('--shadows', 0, int) > 0                        # cast shadows from the sun (3 renders per view instead of 1)
    yaw = opt('--yaw', 0.0, float)
    play_yaw = opt('--play-yaw', 0.0, float)                      # degrees added to the play-distance camera only (the close views stay)
    water = opt('--water', 0, int) > 0                            # an opaque lake surface at the models' y 0 (waterline props, swimming birds)
    light = opt('--light', 'game')                                # game (default: the soft world light, warm sun, cool sky) | neutral
    if light not in ('game', 'neutral'):
        raise SystemExit('sc_asset_preview: --light game|neutral, not %r' % light)
    if light == 'neutral':
        neutral_light()
    inside = opt('--inside', 0, int) > 0                          # also write <out>_inside.png: top-down and 60 deg down (not scored)                                # degrees about the vertical, applied to every model before the views
    bpy.ops.wm.read_factory_settings(use_empty=True)
    pal = palette(season)
    groups = []
    x = 0.0
    for entry in rest:
        objs = []
        for path in entry.split('+'):        # a.fbx+a_Tri.fbx: halves of one model, kept on one origin
            objs += import_model(path)
        if not objs: continue
        if yaw:   # turn the model for the views only (a run authored along +z previews end-on otherwise); nothing is written back
            for o in bpy.data.objects:
                if o.parent is None and (o in objs or any(c in objs for c in o.children_recursive)):
                    o.rotation_euler.z += math.radians(yaw)
            bpy.context.view_layer.update()
        lo, hi = bounds(objs)
        shift = Vector((x - lo.x, -(lo.y + hi.y) / 2, 0.0 if water else -lo.z))   # --water keeps each model's own y 0 on the lake surface
        for o in bpy.data.objects:
            if o.parent is None and (o in objs or any(c in objs for c in o.children_recursive)): o.location += shift
        bpy.context.view_layer.update()
        x += (hi.x - lo.x) + max(0.3, 0.45 * max(hi.x - lo.x, hi.y - lo.y))   # room for the three-quarter views (buildings overlapped at 0.15 x)
        groups += objs
    if not groups:
        print('NO MODELS'); return
    for o in groups:
        me = o.data
        enc = me.uv_layers.get('Layer') is not None and len(me.color_attributes) > 0
        if enc and me.color_attributes.get('Col') is None:
            me.color_attributes[0].name = 'Col'
        base = None
        if not enc and o.material_slots and o.material_slots[0].material and o.material_slots[0].material.use_nodes:
            b = next((n for n in o.material_slots[0].material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if b and not b.inputs['Base Color'].is_linked: base = tuple(b.inputs['Base Color'].default_value)
            elif b:
                stack = [b.inputs['Base Color'].links[0].from_node]
                while stack and not isinstance(base, bpy.types.Image):
                    nd = stack.pop()
                    if nd.type == 'TEX_IMAGE' and nd.image: base = nd.image
                    else: stack += [l.from_node for i in nd.inputs for l in i.links]
        tri = o.name.endswith('_Tri') or '_Tri.' in o.name or o.name.endswith('_Tri.001')
        m = preview_material(pal, paint, enc, base, tri=tri, use_grain=use_grain, cards='_Cards' in o.name)
        me.materials.clear(); me.materials.append(m)
    lo, hi = bounds(groups)
    if refs:
        # neutral grey references at real size: a 1.8 m figure (0.5 m wide) left of the model, a 0.95 x 1.95 m door and a 3 m x 2.6 m wall behind it
        rm = bpy.data.materials.new('SC_Ref'); rm.use_nodes = True; rnt = rm.node_tree
        rb = next(n for n in rnt.nodes if n.type == 'BSDF_PRINCIPLED'); rnt.nodes.remove(rb)
        re_ = rnt.nodes.new('ShaderNodeEmission'); re_.inputs['Color'].default_value = (0.32, 0.32, 0.33, 1)
        rnt.links.new(re_.outputs[0], next(n for n in rnt.nodes if n.type == 'OUTPUT_MATERIAL').inputs[0])
        made = []
        if 'human' in refs:
            bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.22, depth=1.55, location=(lo.x - 0.6, (lo.y + hi.y) / 2, 0.775)); made.append(bpy.context.active_object)
            bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.12, location=(lo.x - 0.6, (lo.y + hi.y) / 2, 1.68)); made.append(bpy.context.active_object)   # a head: reads as a person
        if 'wall' in refs or 'door' in refs:
            ww = max(3.0, hi.x - lo.x + 1.0)
            bpy.ops.mesh.primitive_cube_add(size=1, location=((lo.x + hi.x) / 2, hi.y + 0.05, 1.3)); w_ = bpy.context.active_object   # flush with the model's back (Unity -z)
            w_.scale = (ww, 0.1, 2.6); made.append(w_)
            if 'door' in refs:
                bpy.ops.mesh.primitive_cube_add(size=1, location=((lo.x + hi.x) / 2 + ww * 0.3, hi.y - 0.01, 0.975)); d_ = bpy.context.active_object
                d_.scale = (0.95, 0.04, 1.95)
                dm = rm.copy(); dm.node_tree.nodes['Emission'].inputs['Color'].default_value = (0.12, 0.12, 0.13, 1); d_.data.materials.append(dm)
        for o in made:
            if not o.data.materials: o.data.materials.append(rm)
        made += [d_] if 'door' in refs else []
        bpy.context.view_layer.update()
        lo, hi = bounds(groups + made)
    centre = (lo + hi) / 2; radius = max((hi - lo).length / 2, 0.1)
    # ground disc in the meadow green slot, sky world
    bpy.ops.mesh.primitive_circle_add(vertices=64, radius=radius * 4, fill_type='TRIFAN', location=(centre.x, centre.y, lo.z - 0.002))
    gm = bpy.data.materials.new('SC_Ground'); gm.use_nodes = True; gnt = gm.node_tree; gnt.nodes.clear()
    ge = gnt.nodes.new('ShaderNodeEmission'); ge.inputs['Color'].default_value = tuple(0.55 * x + 0.45 * y for x, y in zip(lin(pal[0]), lin((0.55, 0.58, 0.48)))) + (1,)   # a muted meadow
    go = gnt.nodes.new('ShaderNodeOutputMaterial'); gnt.links.new(ge.outputs[0], go.inputs[0])
    bpy.context.active_object.data.materials.append(gm)
    if water:   # the lake: a dark green-teal plane at y 0 (the concepts' near water), so what sits under it hides as in the game
        bpy.ops.mesh.primitive_circle_add(vertices=64, radius=radius * 4, fill_type='TRIFAN', location=(centre.x, centre.y, 0.0))
        wm = bpy.data.materials.new('SC_Water'); wm.use_nodes = True; wnt = wm.node_tree; wnt.nodes.clear()
        we = wnt.nodes.new('ShaderNodeEmission'); we.inputs['Color'].default_value = lin((0.17, 0.29, 0.29)) + (1,)
        wo = wnt.nodes.new('ShaderNodeOutputMaterial'); wnt.links.new(we.outputs[0], wo.inputs[0])
        bpy.context.active_object.data.materials.append(wm)
    sc = bpy.context.scene
    sc.world = bpy.data.worlds.new('W'); sc.world.use_nodes = True
    sc.world.node_tree.nodes['Background'].inputs['Color'].default_value = lin((0.74, 0.80, 0.82)) + (1,)   # the look's horizon haze
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 16; sc.cycles.use_denoising = False
    sc.cycles.max_bounces = 0; sc.cycles.transparent_max_bounces = 32   # stacked foliage cards
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.view_settings.view_transform = 'Standard'
    cam_d = bpy.data.cameras.new('C'); cam = bpy.data.objects.new('C', cam_d); sc.collection.objects.link(cam); sc.camera = cam
    cam_d.lens = 50
    import tempfile
    tmp = tempfile.mkdtemp(prefix='fgprev_')
    shots = []
    if shadows:
        # Cast shadows (--shadows 1): the look is drawn as emission, so the sun's visibility comes from two extra renders of the scene in plain
        # white diffuse under a real sun lamp, with and without shadows. vis = with / without; the final pixel keeps its sky/ambient share and
        # loses the sun's share where vis < 1 (SUN_SHARE of a lit face's light is the sun in the soft model).
        import numpy as np
        ld = bpy.data.lights.new('SC_Sun', 'SUN'); ld.energy = 3.0; ld.angle = math.radians(3)
        sun_ob = bpy.data.objects.new('SC_Sun', ld); sc.collection.objects.link(sun_ob)
        sun_ob.rotation_euler = (-SUN_DIR).to_track_quat('-Z', 'Y').to_euler()
        white = bpy.data.materials.new('SC_White'); white.use_nodes = True
        wn = white.node_tree; wn.nodes.clear()
        dif = wn.nodes.new('ShaderNodeBsdfDiffuse'); dif.inputs['Color'].default_value = (0.8, 0.8, 0.8, 1)
        wout = wn.nodes.new('ShaderNodeOutputMaterial'); wn.links.new(dif.outputs[0], wout.inputs[0])
        meshes = [o for o in sc.objects if o.type == 'MESH']
        looks = {o.name: [sl.material for sl in o.material_slots] for o in meshes}
        world_col = tuple(sc.world.node_tree.nodes['Background'].inputs['Color'].default_value)
    SUN_SHARE = 0.65
    def with_shadows(path):
        if not shadows:
            return
        import numpy as np
        def render_to(pth):
            sc.render.filepath = pth; bpy.ops.render.render(write_still=True)
            im = bpy.data.images.load(pth); a = np.array(im.pixels[:], dtype=np.float32).reshape(-1, 4); bpy.data.images.remove(im); return a
        for o in meshes:
            for i, sl in enumerate(o.material_slots):
                if not (looks[o.name][i] and looks[o.name][i].name.startswith('SC_AssetPreview') and 'Cards' in o.name): sl.material = white
        sc.world.node_tree.nodes['Background'].inputs['Color'].default_value = (0, 0, 0, 1)
        ld.use_shadow = True; S = render_to(path + '.s.png')
        ld.use_shadow = False; U = render_to(path + '.u.png')
        ld.use_shadow = True
        for o in meshes:
            for i, sl in enumerate(o.material_slots): sl.material = looks[o.name][i]
        sc.world.node_tree.nodes['Background'].inputs['Color'].default_value = world_col
        im = bpy.data.images.load(path); A = np.array(im.pixels[:], dtype=np.float32).reshape(-1, 4)
        lu = U[:, :3].mean(1); ls = S[:, :3].mean(1)
        vis = np.where(lu > 0.02, np.clip(ls / np.maximum(lu, 1e-4), 0.0, 1.0), 1.0)
        A[:, :3] *= (1.0 - SUN_SHARE * (1.0 - vis))[:, None]
        im.pixels.foreach_set(A.ravel()); im.filepath_raw = path; im.file_format = 'PNG'; im.save(); bpy.data.images.remove(im)
    views = [('three_quarter', 35, 18), ('side', 90, 8), ('back_three_quarter', 215, 22)]
    fov = 2 * math.atan(18.0 / cam_d.lens)
    for name, az, el in views:
        d = radius / math.sin(fov / 2) * 1.08
        a, e = math.radians(az), math.radians(el)
        cam.location = centre + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * d
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        p = os.path.join(tmp, name + '.png'); sc.render.filepath = p
        bpy.ops.render.render(write_still=True); with_shadows(p); shots.append((name, p))
    # play distance: about 60 px tall for the whole group, upscaled nearest
    height = max(hi.z - lo.z, 0.05)
    small = max(96, int(size / 3))   # rendered small, the model about 48 px tall, then upscaled: the pixels a player actually gets
    sc.render.resolution_x = sc.render.resolution_y = small
    vfov = 2 * math.atan(18.0 / cam_d.lens)
    d = max(height, (hi - lo).length * 0.5) / (2 * math.tan(vfov / 2)) * (small / 48.0)
    a, e = math.radians(35 + play_yaw), math.radians(10)   # --play-yaw turns this camera only (a profile read at 48 px: birds, boats)
    cam.location = centre + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * d
    cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    p = os.path.join(tmp, 'play.png'); sc.render.filepath = p
    bpy.ops.render.render(write_still=True); with_shadows(p); shots.append(('play_distance', p))
    # compose 2 x 2
    imgs = []
    for name, p in shots:
        im = bpy.data.images.load(p); imgs.append((name, im))
    W = size * 2; H = size * 2
    px = [0.0] * (W * H * 4)
    for k, (name, im) in enumerate(imgs):
        ox, oy = (k % 2) * size, (1 - k // 2) * size
        iw, ih = im.size; src = list(im.pixels)
        for yy in range(size):
            sy = min(ih - 1, int(yy * ih / size))
            for xx in range(size):
                sx = min(iw - 1, int(xx * iw / size))
                si = (sy * iw + sx) * 4; di = ((oy + yy) * W + ox + xx) * 4
                px[di:di + 4] = src[si:si + 4]
    sheet = bpy.data.images.new('sheet', W, H, alpha=False)
    sheet.pixels.foreach_set(px)
    sheet.filepath_raw = out; sheet.file_format = 'PNG'; sheet.save()
    print('PREVIEW', out, [n for n, _ in shots])
    if inside:
        # --inside: a separate 2 x 1 sheet (<out>_inside.png; the scored 2 x 2 is unchanged): top-down and 60 deg down into the model, for open
        # things whose insides the close views miss (a boat's thwarts and bilge, a cart, a trough, a well)
        sc.render.resolution_x = sc.render.resolution_y = size
        ins = []
        for name, az, el in (('top_down', 0, 88), ('down_60', 35, 60)):
            d = radius / math.sin(fov / 2) * 1.08
            a, e = math.radians(az), math.radians(el)
            cam.location = centre + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * d
            cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
            p = os.path.join(tmp, name + '.png'); sc.render.filepath = p
            bpy.ops.render.render(write_still=True); with_shadows(p)
            ins.append(bpy.data.images.load(p))
        W2 = size * 2
        px = [0.0] * (W2 * size * 4)
        for k, im in enumerate(ins):
            src = list(im.pixels)
            for yy in range(size):
                px[(yy * W2 + k * size) * 4:(yy * W2 + k * size + size) * 4] = src[yy * size * 4:(yy + 1) * size * 4]
        o2 = os.path.splitext(out)[0] + '_inside.png'
        sh2 = bpy.data.images.new('inside', W2, size, alpha=False); sh2.pixels.foreach_set(px)
        sh2.filepath_raw = o2; sh2.file_format = 'PNG'; sh2.save()
        print('PREVIEW', o2, ['top_down', 'down_60'])

if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:])
