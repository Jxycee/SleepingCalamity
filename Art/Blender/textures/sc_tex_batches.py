"""sc_tex_batches.py (M8 part 1): builds the graded, tileable 2K PBR sets for batches B1 (town), B2 (cellar) and B3 (trims, decals).
Plain python (numpy + Pillow); run from anywhere:
  python Art/Blender/textures/sc_tex_batches.py --batch B1|B2|B3|all --src C:/Dev/_private/textures_src/cands --out Art/Textures [--only plaster,cobble]
Reads CC0 Poly Haven scans from --src (fetch them with Art/Blender/tools/sc_fetch_textures.py; scans stay out of git) and writes, per material:
  Art/Textures/<batch>/<material>/<material>_albedo.png   sRGB, graded to the PHASE1 3.1 key hue, no baked light
                                  <material>_normal.png   OpenGL (Y+), tangent space
                                  <material>_orm.png      R occlusion, G roughness, B metallic (sc_pack_unity.py --orm makes the URP Lit maps)
plus Art/Textures/<batch>/SOURCES.md (sources, licences, grade knobs, seam ratios, measured albedo colour) and tiling3x3_<material>.jpg QA crops go to --qa (not the repo).
Consumers: PHASE1 3.3 tileables, 1024 px/m, so one 2048 tile covers 2.0 m; trims 1024 px/m horizontally.
Everything is deterministic (seeded numpy), so a rerun gives byte-identical PNGs.
"""
import argparse, importlib.util, json, math, os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'tools'))
import sc_texgrade as G

_spec = importlib.util.spec_from_file_location('sc_ref_palette', os.path.join(HERE, '..', 'tools', 'sc_ref_palette.py'))
RP = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(RP)

RES = 2048


# --------------------------------------------------------------------------- material specs
# src: Poly Haven id. target: PHASE1 3.1 key hue the mean albedo is pulled to. hue_keep/delight/contrast/chroma: sc_texgrade.grade_albedo.
# rough: (scale, offset) applied to the scan's roughness. nrm: scan normal strength. metal: 0/1 (iron). post: named procedural layer.
B1 = {
    'plaster':    dict(src='white_stucco', target='#cbbd9c', hue_keep=0.2, delight=0.8, contrast=1.0, rough=(0.0, 0.88), nrm=0.6, post='plaster',
                       why='lime plaster walls: warm cream (3.1 plaster #cbbd9c), faint trowel relief; wear comes from decals'),
    'oak_timber': dict(src='rough_wood', target='#3b322c', hue_keep=0.15, delight=0.6, contrast=1.35, rough=(0.8, 0.12), nrm=1.0,
                       why='adzed oak timber: 3.1 timber #2e2520 to #4e4844'),
    'cobble':     dict(src='cobblestone_floor_01', target='#9a928c', hue_keep=0.55, delight=0.7, contrast=1.15, rough=(0.9, 0.1), nrm=1.0, vary=0.12,
                       why='granite cobble with warm lights: 3.1 cobble #817a7b to #b7b0aa'),
    'sandstone':  dict(src='sandstone_blocks_05', target='#b9a07a', hue_keep=0.6, delight=0.7, contrast=1.1, rough=(0.8, 0.15), nrm=1.0,
                       why='dressed sandstone (the fountain): warm mid stone'),
    'roof_tile':  dict(src='clay_roof_tiles', target='#9c4a34', hue_keep=0.5, delight=0.5, contrast=1.15, chroma=0.95, vary=0.10, rough=(0.8, 0.12), nrm=1.0,
                       why='red clay roof tile: 3.1 roof #9c4a34'),
    'painted_wood': dict(src='blue_painted_planks', target='#46728f', hue_keep=0.5, delight=0.7, contrast=1.15, chroma=1.15, rough=(0.7, 0.2), nrm=1.0,
                       why='painted wood (shutters, stalls): confident blue, +15% chroma (3.1 painted wood)'),
    'canvas':     dict(src='rough_linen', target='#c4994a', hue_keep=0.0, delight=0.9, contrast=1.6, chroma=1.1, rough=(0.5, 0.5), nrm=1.4, post='canvas',
                       why='canvas and linen (awnings, banners): ochre, +10% chroma; stripes and madder variants are tints in the material'),
    'iron':       dict(src='rusty_metal_02', target='#4d4a48', hue_keep=0.0, delight=0.6, contrast=1.4, rough=(0.5, 0.35), nrm=1.0, post='iron', metal=1.0, vmin=0.04,
                       why='wrought iron: dark metal, rust only in placed patches (metallic 1 on bare metal, 0 on rust)'),
}
B2 = {
    'old_brick':  dict(src='brown_brick_02', target='#ab7b57', hue_keep=1.0, delight=0.6, contrast=1.1, chroma=1.05, rough=(0.8, 0.15), nrm=1.0, vary=0.10,
                       why='old brick, tan and orange: 3.1 brick #9d7d69 / #bf7a38'),
    'vault_stone': dict(src='medieval_blocks_05', target='#9c8460', hue_keep=0.7, delight=0.7, contrast=1.15, rough=(0.8, 0.15), nrm=1.1,
                       why='vault rubble stone, gold-lit stone relation (lit stone #c9aa76 once lamplight hits it)'),
    'limewash':   dict(src='worn_plaster_wall', target='#b3a588', hue_keep=0.25, delight=0.8, contrast=1.1, rough=(0.6, 0.35), nrm=0.9, post='limewash',
                       why='damp limewash: warm cream, darker cooler damp patches placed low on the sheet'),
    'flagstone':  dict(src='monastery_stone_floor', target='#8b7860', hue_keep=0.4, delight=0.6, contrast=1.2, rough=(0.8, 0.15), nrm=1.0, vary=0.10,
                       why='flagstone floor: warm, worn'),
    'barrel_oak': dict(src='wooden_barrel', target='#4d3828', hue_keep=0.5, delight=0.5, contrast=1.2, rough=(0.8, 0.12), nrm=1.0,
                       why='oak barrel staves with iron hoops (the scan carries the hoops and rivets)'),
    'soot_coal':  dict(src='burned_ground_01', target='#2d2725', hue_keep=0.25, delight=0.8, contrast=1.5, rough=(0.5, 0.45), nrm=1.2, post='coal', vmin=0.03,
                       why='soot and coal: dark warm, glinting chunks; no mass is pure black (3.1)'),
    'root_bark':  dict(src='bark_brown_02', target='#5e4731', hue_keep=0.2, delight=0.6, contrast=1.25, rough=(0.85, 0.1), nrm=1.0,
                       why='root bark: warm brown, the scan\'s green moss is painted out (moss is a decal)'),
}


# --------------------------------------------------------------------------- procedural layers (periodic, so tiles stay seamless)
def post_albedo(name, alb, ctx):
    """alb: sRGB float HxWx3. Returns (alb, rough_mod, extra_height, metal) with rough_mod an additive roughness map."""
    n = alb.shape[0]
    z = np.zeros((n, n), np.float32)
    if name == 'plaster':
        tint = G.fbm_periodic(n, 11, beta=3.2, kmax=10)
        t = (tint - 0.5) * 2
        lin = G.srgb2lin(alb)
        lin = lin * (1 + 0.07 * t[..., None] * np.array([1.0, 0.6, 0.0], np.float32)[None, None, :] + 0.05 * t[..., None])
        trowel = G.fbm_periodic(n, 12, beta=2.6, kmin=3, kmax=48)
        return G.lin2srgb(lin), (G.fbm_periodic(n, 13, beta=2.0, kmin=8, kmax=120) - 0.5) * 0.12, (trowel - 0.5) * 18.0, None
    if name == 'canvas':
        # woven stripes: ochre, with a madder (red) stripe band every 256 px along u (tile repeats cleanly)
        x = np.arange(n, dtype=np.float32)[None, :] / n
        band = (np.sin(x * 2 * np.pi * 2 + 0.6) > 0.93).astype(np.float32) * np.ones((n, 1), np.float32)
        band = G.blur(band, 3.0)
        lin = G.srgb2lin(alb)
        madder = G.hex2lin('#8e3a30')
        lum = G.luminance(lin)[..., None] / G.luminance(lin).mean()
        lin = lin * (1 - band[..., None]) + madder[None, None, :] * lum * band[..., None]
        return G.lin2srgb(lin), z, z, None
    if name == 'iron':
        src = ctx['src_rgb']
        rust = G.smoothstep(src[..., 0] - src[..., 2], 0.42, 0.60)
        rust = G.blur(rust, 2.0)
        lin = G.srgb2lin(alb)
        rc = G.hex2lin('#6c3f25')
        lum = (G.luminance(lin) / G.luminance(lin).mean())[..., None]
        lin = lin * (1 - rust[..., None]) + rc[None, None, :] * lum * rust[..., None]
        metal = 1.0 - G.smoothstep(rust, 0.2, 0.6)
        return G.lin2srgb(lin), rust * 0.45, z, metal.astype(np.float32)
    if name == 'limewash':
        damp = G.fbm_periodic(n, 21, beta=3.0, kmax=14)
        y = np.linspace(0, 1, n, dtype=np.float32)[:, None]
        # damp gathers toward the bottom edge of the tile; the 'y' wrap is hidden by blending with the periodic noise only
        mask = G.smoothstep(damp + 0.0 * y, 0.55, 0.75)
        lin = G.srgb2lin(alb)
        cool = np.array([0.82, 0.88, 0.95], np.float32)
        lin = lin * (1 - 0.30 * mask[..., None]) * (1 - mask[..., None] + mask[..., None] * cool[None, None, :])
        return G.lin2srgb(lin), -mask * 0.25 + 0.0, z, None
    if name == 'coal':
        chunks = G.fbm_periodic(n, 31, beta=1.8, kmin=10, kmax=300)
        glint = G.smoothstep(chunks, 0.64, 0.7)
        lin = G.srgb2lin(alb)
        lin = lin * (1 + 0.5 * glint[..., None])
        return G.lin2srgb(lin), -glint * 0.35, z, None
    return alb, z, z, None


def build_material(name, spec, src_dir, out_dir, rows):
    n = RES
    sid = spec['src']
    P = lambda k, ext='jpg': os.path.join(src_dir, f'{sid}_{k}_2k.{ext}')
    src_rgb = G.load_srgb(P('diff'), n)
    alb = G.grade_albedo(src_rgb, spec['target'], spec.get('hue_keep', 0.6), spec.get('delight', 0.7), spec.get('contrast', 1.0),
                         spec.get('chroma', 1.0), vmin=spec.get('vmin', 0.0))
    rough_src = G.load_gray(P('rough'), n)
    ao_src = G.load_gray(P('ao'), n) if os.path.exists(P('ao')) else np.ones((n, n), np.float32)
    nor = G.load_srgb(P('nor_gl'), n)
    alb, rmod, extra_h, metal = post_albedo(spec.get('post', ''), alb, dict(src_rgb=src_rgb))
    if spec.get('vary'):   # a large-scale value/hue variation layer breaks the 2 m repeat (periodic noise, so seams stay closed)
        v = (G.fbm_periodic(n, 77 + len(name), beta=3.0, kmax=8) - 0.5) * 2 * spec['vary']
        lin = G.srgb2lin(alb) * (1 + v[..., None] * np.array([1.0, 0.85, 0.6], np.float32)[None, None, :])
        alb = G.lin2srgb(lin)
    rs, ro = spec['rough']
    rough = np.clip(rough_src * rs + ro + rmod, 0.04, 1.0)
    nor = G.scale_normal(nor, spec.get('nrm', 1.0))
    if np.any(extra_h):
        nor = G.mix_normals(nor, G.normal_from_height(extra_h, 1.0))   # extra_h is in 'slope units' (gradient per texel x height)
    m = np.full((n, n), spec.get('metal', 0.0), np.float32) if metal is None else metal
    ao = np.clip(ao_src, 0, 1) ** 0.8
    orm = np.stack([ao, rough, m], -1)
    d = os.path.join(out_dir, name)
    os.makedirs(d, exist_ok=True)
    G.save_rgb(alb, os.path.join(d, f'{name}_albedo.png'))
    G.save_rgb(nor, os.path.join(d, f'{name}_normal.png'))
    G.save_rgb(orm, os.path.join(d, f'{name}_orm.png'))
    rows.append(dict(name=name, spec=spec, alb=alb, seam=dict(albedo=G.seam_ratio(alb), normal=G.seam_ratio(nor), orm=G.seam_ratio(orm))))
    print('built', name, 'seam a/n/o %.2f %.2f %.2f' % tuple(rows[-1]['seam'].values()))
    return alb


def measure(alb_srgb):
    im = Image.fromarray((np.clip(alb_srgb, 0, 1) * 255).astype(np.uint8)).resize((256, 256))
    rgb = np.asarray(im, np.float32).reshape(-1, 3) / 255.0
    a = RP.analyse(rgb, 7)
    mean = rgb.mean(0)
    a['mean_hex'] = '#%02x%02x%02x' % tuple(int(round(v * 255)) for v in G.lin2srgb(G.srgb2lin(rgb).mean(0)))
    return a


def write_sources(batch_dir, batch, rows, src_records):
    lines = [f'# {batch} sources and grade record', '',
             'Generated by `Art/Blender/textures/sc_tex_batches.py` (deterministic: a rerun gives identical PNGs). Scans are Poly Haven, CC0 1.0 (https://polyhaven.com/license);',
             'the source scans live outside git in `C:\\Dev\\_private\\textures_src\\` (fetched with `Art/Blender/tools/sc_fetch_textures.py`). Maps per material: albedo (sRGB), normal (OpenGL Y+), orm (R occlusion, G roughness, B metallic).', '',
             '| material | source scan | licence | author | fetched | target hue | measured mean | value p10/p50/p90 | seam ratio a/n/o | grade knobs |', '|---|---|---|---|---|---|---|---|---|---|']
    for r in rows:
        s = r['spec']; a = r['measure']
        rec = src_records.get(s['src'], {})
        lines.append('| %s | [%s](https://polyhaven.com/a/%s) | CC0 1.0 | %s | %s | %s | %s | %.2f / %.2f / %.2f | %.2f / %.2f / %.2f | hue_keep %.2f, delight %.2f, contrast %.2f, chroma %.2f, rough x%.2f+%.2f, normal x%.2f |'
                     % (r['name'], s['src'], s['src'], rec.get('author', '?'), rec.get('fetched', '?'), s['target'], a['mean_hex'], a['p10'], a['p50'], a['p90'],
                        r['seam']['albedo'], r['seam']['normal'], r['seam']['orm'], s.get('hue_keep', 0.6), s.get('delight', 0.7), s.get('contrast', 1.0), s.get('chroma', 1.0),
                        s['rough'][0], s['rough'][1], s.get('nrm', 1.0)))
    lines += ['', 'Used for:', ''] + ['- **%s**: %s' % (r['name'], r['spec'].get('why', '')) for r in rows]
    return '\n'.join(lines) + '\n'


def read_records(src_dir):
    out = {}
    p = os.path.join(src_dir, 'records.md')
    if os.path.exists(p):
        for ln in open(p, encoding='utf-8'):
            c = [x.strip() for x in ln.strip().strip('|').split('|')]
            if len(c) >= 6 and c[0] not in ('asset', '---'):
                out[c[0]] = dict(author=c[3], fetched=c[4])
    return out


def run_batch(batch, specs, args):
    out_dir = os.path.join(args.out, {'B1': 'B1_town', 'B2': 'B2_cellar'}[batch])
    os.makedirs(out_dir, exist_ok=True)
    rows = []
    only = args.only.split(',') if args.only else None
    for name, spec in specs.items():
        if only and name not in only:
            continue
        build_material(name, spec, args.src, out_dir, rows)
    for r in rows:
        r['measure'] = measure(r['alb'])
        if args.qa:
            os.makedirs(args.qa, exist_ok=True)
            G.tile3(r['alb'], 1024).save(os.path.join(args.qa, f"{batch}_{r['name']}_tile3.jpg"), quality=85)
    if not only:
        open(os.path.join(out_dir, 'SOURCES.md'), 'w', encoding='utf-8').write(write_sources(out_dir, batch, rows, read_records(args.src)))
    json.dump({r['name']: dict(mean=r['measure']['mean_hex'], p=[r['measure'][k] for k in ('p10', 'p50', 'p90')], seam=r['seam']) for r in rows},
              open(os.path.join(args.qa or out_dir, f'{batch}_measure.json'), 'w'), indent=1)
    return rows


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--batch', default='all'); ap.add_argument('--src', required=True); ap.add_argument('--out', required=True)
    ap.add_argument('--only', default=''); ap.add_argument('--qa', default='')
    args = ap.parse_args()
    if args.batch in ('B1', 'all'):
        run_batch('B1', B1, args)
    if args.batch in ('B2', 'all'):
        run_batch('B2', B2, args)
    if args.batch in ('B3', 'all'):
        import sc_tex_b3
        sc_tex_b3.run(args)
