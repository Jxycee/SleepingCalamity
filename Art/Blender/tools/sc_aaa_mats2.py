"""sc_aaa_mats2 - more smart materials (stone, copper, ceramic, wax, leather, paper, charcoal, cork, herb, twine)
plus the unbaked glass/liquid materials. Same conventions as sc_aaa_mats (lp/pid/ln attributes)."""
import os
from sc_aaa_kit import new_mat, use_scan
from sc_aaa_mats import _part_space
import sc_aaa_scan as S

def _std_masks(T, edge_r=0.003, cav_d=0.02):
    _, p, pid = _part_space(T, jitter=(3.0, 3.0, 3.0))
    pt = T.smoothstep(T.geo('Pointiness'), 0.5, 0.56)
    edge = T.math('MAXIMUM', T.edge(radius=edge_r, lo=0.03, hi=0.2), pt)
    cav = T.cavity(dist=cav_d, lo=0.3, hi=0.95)
    crev = T.smoothstep(T.geo('Pointiness'), 0.49, 0.44)
    dirt = T.math('MAXIMUM', T.math('SUBTRACT', 1.0, cav), crev)
    return p, pid, edge, dirt

def stone(name='M_Stone', base=(0.30, 0.28, 0.25), speck=1.0, residue=None, inner_r=0.07, grain=0.0, scan_tiles=3.0, scan_col=0.35, bowl=None, ash=None, full=None):
    """Limestone/sandstone: mottled, speckled, polished where used; residue in a bowl below lp z=residue.
    grain > 0: mineral grain (per-grain tone cells ~1 mm plus dark and glinting specks, raised in the bump),
    the cue that separates carved stone from plasticine at close range.
    bowl = (inner_r, z_floor, z_rim): the whole inside of a vessel becomes a darker, stained grinding
    surface, polished at the bottom, with circular grinding swirls and powder caught in the pores."""
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.004, 0.03)
    # full=dict(asset=, tile=, mode='cyl'|'tri', sat=, rough=(lo, hi), nstr=): lever 2, the scan's own albedo,
    # normal and roughness replace the procedural base and its noise bump (sc_aaa_scan.scan_full)
    F = full if (full and S.available(full['asset'])) else None
    if F:
        c, r_scan, nrm = S.scan_full(T, tint=base, **F); h = 0.0; pits = 0.0
    else:
        use_scan(T, 'rock_surface', p, scan_tiles, col=scan_col, rough=0.4, height=1.5)
        mot = T.noise(p, scale=12.0, detail=6, rough=0.6)
        vein = T.noise(T.vmath('MULTIPLY', p, (1, 1, 3)), scale=6.0, detail=8, dist=2.0)
        vein = T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', vein, 0.5)), 0.03, 0.0)
        sp = T.voronoi(p, scale=400.0, out='Distance'); sp = T.math('MULTIPLY', T.smoothstep(sp, 0.12, 0.0), speck)
        c = T.mix(T.maprange(mot, 0.3, 0.7, 0, 1), T.hsv(base, 0.5, 1.0, 0.82), T.hsv(base, 0.5, 0.9, 1.12))
        c = T.mix(T.math('MULTIPLY', vein, 0.5), c, T.hsv(base, 0.52, 1.3, 0.6))
        c = T.mix(T.math('MULTIPLY', sp, 0.6), c, (0.04, 0.035, 0.03))
        gh = 0.0
        if grain:
            # mineral grain: each ~1 mm Voronoi cell is one grain with its own tone (quartz pale, feldspar warm,
            # dark lithics), plus sparse dark mica specks and pale glints
            gc = T.voronoi(p, scale=850.0, out='Color'); gv = T.sep(gc)
            gt = T.maprange((gv, 'X'), 0.0, 1.0, 0.72, 1.25)
            c = T.mix(T.math('MULTIPLY', grain, 0.85), c, T.mix(1.0, c, T.comb(gt, T.math('MULTIPLY', gt, 0.98), T.math('MULTIPLY', gt, 0.94)), 'MULTIPLY'))
            warm = T.math('MULTIPLY', T.smoothstep((gv, 'Y'), 0.8, 0.86), grain)
            c = T.mix(T.math('MULTIPLY', warm, 0.5), c, T.hsv(base, 0.47, 1.6, 1.15))
            dk = T.math('MULTIPLY', T.smoothstep(T.voronoi(p, scale=520.0, out='Distance', rand=1.0), 0.16, 0.04), T.smoothstep((gv, 'Z'), 0.75, 0.8))
            c = T.mix(T.math('MULTIPLY', dk, 0.9 * grain), c, (0.025, 0.022, 0.02))
            gl = T.math('MULTIPLY', T.smoothstep(T.voronoi(p, scale=700.0, out='Distance'), 0.1, 0.0), T.smoothstep((gv, 'Z'), 0.08, 0.03))
            c = T.mix(T.math('MULTIPLY', gl, 0.7 * grain), c, T.hsv(base, 0.5, 0.4, 1.6))
            gb = T.voronoi(p, scale=850.0, feature='DISTANCE_TO_EDGE', out='Distance')
            gh = T.math('MULTIPLY', T.math('ADD', T.math('MULTIPLY', T.smoothstep(gb, 0.0, 0.12), 0.15), T.math('MULTIPLY', (gv, 'X'), 0.1)), grain)
            gh = T.math('SUBTRACT', gh, T.math('MULTIPLY', dk, 0.6 * grain))
    MV10 = name == 'M_MortarStone' and os.environ.get('AAA_MORTAR') == '10'   # v10: large-scale stone variation; quieter fresh breaks (v9 reviewers: 'uniform speckle', 'rim chips a white bead / decal')
    if MV10:   # low-frequency staining and tone drift across the block (quarry beds, handling, water marks)
        c = T.mix(T.math('MULTIPLY', T.smoothstep(T.noise(p, scale=2.5, detail=3), 0.35, 0.68), 0.45), c, T.hsv(base, 0.5, 0.9, 0.62))
        c = T.mix(T.math('MULTIPLY', T.smoothstep(T.noise(p, scale=4.0, detail=4, rough=0.6), 0.62, 0.8), 0.35), c, T.hsv(base, 0.47, 1.3, 1.1))
    c = T.mix(T.math('MULTIPLY', edge, 0.2 if F else 0.4), c, T.hsv(base, 0.5, 0.8, 1.25))
    crv = T.smoothstep(T.geo('Pointiness'), 0.49, 0.45)
    # fresh breaks (sc_aaa_cuts 'fr': spall scars, chisel facets): paler, cleaner, slightly less saturated stone
    fr = T.smoothstep(T.attr('fr', vec=False), 0.05, 0.6)
    c = T.mix(T.math('MULTIPLY', fr, 0.45 if MV10 else 0.75), c, T.hsv(base, 0.5, 0.75, 1.15 if MV10 else 1.4))
    dirt = T.math('MULTIPLY', dirt, T.math('SUBTRACT', 1.0, T.math('MULTIPLY', fr, 0.7)))
    c = T.mix(T.math('MULTIPLY', crv, 0.6), c, T.hsv(base, 0.5, 0.6, 1.35))
    c = T.mix(T.math('MULTIPLY', T.math('SUBTRACT', dirt, crv), 0.8), c, (0.05, 0.04, 0.03))
    r = r_scan if F else T.mixf(T.maprange(mot, 0.3, 0.7, 0, 1), 0.62, 0.75)
    r = T.mixf(edge, r, 0.4); r = T.mixf(dirt, r, 0.9)
    if not F:
        pits = T.smoothstep(T.voronoi(p, scale=180.0, out='Distance'), 0.16, 0.05)
        pits = T.math('MULTIPLY', pits, T.smoothstep(T.white(T.vmath('FLOOR', T.vmath('MULTIPLY', p, 180.0)), dim='3D'), 0.6, 0.62))
        c = T.mix(T.math('MULTIPLY', pits, 0.6), c, (0.03, 0.025, 0.02))
        h = T.math('ADD', T.noise(p, scale=80, detail=6, rough=0.7), T.math('MULTIPLY', sp, -0.6))
        h = T.math('ADD', h, T.math('MULTIPLY', T.noise(p, scale=400, detail=3), 0.6))
        h = T.math('SUBTRACT', h, T.math('MULTIPLY', pits, 1.5))
        if grain:
            h = T.math('ADD', h, gh)
            r = T.math('ADD', r, T.math('MULTIPLY', T.math('SUBTRACT', (gv, 'Y'), 0.5), 0.12 * grain))
    if bowl is not None:
        ir, zf, zr = bowl
        lp = T.sep(T.attr('lp')); ln = T.sep(T.attr('ln'))
        rr = T.vmath('LENGTH', T.comb((lp, 'X'), (lp, 'Y'), 0.0))
        radial = T.math('DIVIDE', T.math('ADD', T.math('MULTIPLY', (ln, 'X'), (lp, 'X')), T.math('MULTIPLY', (ln, 'Y'), (lp, 'Y'))), T.math('MAXIMUM', rr, 0.001))
        facing_in = T.math('MAXIMUM', T.smoothstep(radial, 0.1, -0.3), T.smoothstep((ln, 'Z'), 0.4, 0.8))
        inside = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(rr, ir, ir - 0.004), T.smoothstep((lp, 'Z'), zf - 0.01, zf + 0.004)), facing_in)
        inside = T.math('MULTIPLY', inside, T.smoothstep((lp, 'Z'), zr + 0.001, zr - 0.006))
        depth = T.smoothstep((lp, 'Z'), zr, zf)                       # 0 at the rim .. 1 at the floor
        ang = T.math('ARCTAN2', (lp, 'Y'), (lp, 'X'))
        swirl = T.noise(T.comb(T.math('MULTIPLY', ang, 0.4), T.math('MULTIPLY', rr, 90.0), T.math('MULTIPLY', (lp, 'Z'), 60.0)), scale=3.0, detail=6, rough=0.6)
        stain = T.math('MULTIPLY', inside, T.maprange(T.noise(p, scale=14, detail=5), 0.3, 0.7, 0.45, 1.0))
        BW11 = name == 'M_MortarStone' and os.environ.get('AAA_MORTAR') == '11'   # v11: the grinding surface is worn paler and polished, not a dark mass (v9/v10 reviewers: 'interior a flat dark mass', 'no grinding polish')
        used = T.mix(swirl, T.hsv(base, 0.5, 0.75, 1.05), T.hsv(base, 0.5, 0.65, 1.3)) if BW11 else T.mix(swirl, T.hsv(base, 0.47, 1.25, 0.55), T.hsv(base, 0.5, 0.9, 0.78))
        used = T.mix(T.math('MULTIPLY', depth, 0.3 if BW11 else 0.6), used, (0.07, 0.06, 0.035))      # herb-green-brown stain deepest
        c = T.mix(T.math('MULTIPLY', stain, 0.9), c, used)
        pol = T.math('MULTIPLY', inside, T.math('MULTIPLY', T.smoothstep(depth, 0.35, 0.9), T.smoothstep(swirl, 0.35, 0.6)))
        r = T.mixf(inside, r, 0.45 if BW11 else 0.55); r = T.mixf(pol, r, 0.22 if BW11 else 0.32)
        h = T.math('ADD', T.math('MULTIPLY', h, T.math('SUBTRACT', 1.0, T.math('MULTIPLY', pol, 0.7))), T.math('MULTIPLY', T.math('MULTIPLY', swirl, inside), 0.6))
        # powder caught in the pores and the grain boundaries, thicker toward the floor
        pw = T.math('MULTIPLY', T.math('MULTIPLY', inside, T.math('MAXIMUM', pits, T.smoothstep(T.noise(p, scale=60, detail=4), 0.58, 0.68))), T.math('ADD', 0.3, depth))
        c = T.mix(T.math('MULTIPLY', pw, 0.7), c, (0.16, 0.16, 0.10)); r = T.mixf(pw, r, 0.95)
    if ash is not None:
        # spilled ash and cinders on the up faces in a ring (r0..r1 from the part centre): pale drifts, black cinder bits
        r0, r1 = ash; lp = T.sep(T.attr('lp')); rr = T.vmath('LENGTH', T.comb((lp, 'X'), (lp, 'Y'), 0.0))
        ring = T.math('MULTIPLY', T.smoothstep(T.math('ADD', rr, T.math('MULTIPLY', T.noise(p, scale=9, detail=4), 0.05)), r1, r0), T.up(0.6, 0.9))
        am = T.math('MULTIPLY', ring, T.smoothstep(T.noise(p, scale=30, detail=6, rough=0.65), 0.38, 0.6))
        c = T.mix(am, c, T.mix(T.noise(p, scale=200, detail=2), (0.2, 0.19, 0.17), (0.38, 0.36, 0.33))); r = T.mixf(am, r, 0.97)
        cin = T.math('MULTIPLY', ring, T.smoothstep(T.voronoi(p, scale=160.0, out='Distance'), 0.14, 0.06))
        cin = T.math('MULTIPLY', cin, T.smoothstep(T.white(T.vmath('FLOOR', T.vmath('MULTIPLY', p, 160.0)), dim='3D'), 0.7, 0.72))
        c = T.mix(cin, c, (0.012, 0.011, 0.01)); h = T.math('ADD', h, T.math('ADD', T.math('MULTIPLY', am, 0.6), T.math('MULTIPLY', cin, 1.2)))
    if residue is not None:
        z = T.sep(T.attr('lp')); nz = T.sep(T.attr('ln'))
        rr = T.vmath('LENGTH', T.comb((z, 'X'), (z, 'Y'), 0.0))
        inner = T.math('MULTIPLY', T.smoothstep((z, 'Z'), residue + 0.01, residue - 0.01), T.smoothstep((nz, 'Z'), -0.2, 0.4))
        inner = T.math('MULTIPLY', inner, T.smoothstep(rr, inner_r, inner_r - 0.006))
        res = T.math('MULTIPLY', inner, T.smoothstep(T.noise(p, scale=30, detail=4), 0.35, 0.5))
        c = T.mix(res, c, T.mix(T.noise(p, scale=150, detail=2), (0.035, 0.05, 0.02), (0.09, 0.08, 0.04)))
        r = T.mixf(res, r, 0.95)
        h = T.math('ADD', h, T.math('MULTIPLY', res, 1.5))
    T.output(c, r, 0.0, nrm if F else T.bump(h, strength=0.7, distance=0.0006))
    return m

def _runs(T, ang, z, lip, cells=3.5, maxlen=0.06, width=0.32, seed=0.0):
    """Vertical runs hanging below lp z=lip on a lathed part: one 1D-Voronoi angular cell = one run,
    with its own length and a tapered, rounded tip (the ceramic glaze-run trick). Returns a 0..1 mask."""
    vc = T.node('ShaderNodeTexVoronoi', feature='F1'); vc.voronoi_dimensions = '1D'
    T.set(vc, 'W', T.math('ADD', T.math('MULTIPLY', ang, cells), seed)); vc.inputs['Scale'].default_value = 1.0
    runlen = T.math('MULTIPLY', T.math('POWER', (T.sep((vc, 'Color')), 'X'), 1.6), maxlen)
    tz = T.math('DIVIDE', T.math('SUBTRACT', lip, z), T.math('ADD', runlen, 0.001))
    w = T.math('MULTIPLY', T.math('SUBTRACT', 1.0, T.math('POWER', T.math('MAXIMUM', tz, 0.0), 1.5)), width)
    run = T.math('MULTIPLY', T.smoothstep((vc, 'Distance'), w, T.math('MULTIPLY', w, 0.3)), T.smoothstep(tz, 1.0, 0.6))
    return T.math('MULTIPLY', run, T.smoothstep(tz, -0.05, 0.02))

def copper(name='M_Copper', patina=0.6, soot_z=None, lips=(), polish=(), seam=None, soot_down=0.0):
    """Hammered copper, aged in use: a dark, even oxide (not clouds); tarnish RUNS hanging from lips
    (lp z values); handling POLISH (bright, glossy) within a radius of given points and on the raised
    hammer facets; a riveted SEAM band at angle seam=(angle, half_width) with verdigris along its edges;
    soot below lp z=soot_z."""
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.003, 0.02)
    use_scan(T, 'metal_plate', p, 4.0, col=0.15, rough=0.35, height=0.4)
    lpv = T.attr('lp'); lp = T.sep(lpv); z = (lp, 'Z')
    ang = T.math('ARCTAN2', (lp, 'Y'), (lp, 'X'))
    ham = T.voronoi(p, scale=90.0, out='Distance')
    bright = (0.95, 0.56, 0.38); oxide = (0.38, 0.17, 0.09); dark = (0.17, 0.085, 0.05); verd = (0.10, 0.25, 0.17)   # metal albedo = F0: dark values read as matte clay (s19-s20)
    V2 = os.environ.get('AAA_CU') in ('2', '3', '4', '5', '6'); V3 = os.environ.get('AAA_CU') in ('3', '4', '5', '6'); V4 = os.environ.get('AAA_CU') in ('4', '5', '6')
    V5 = os.environ.get('AAA_CU') in ('5', '6'); V6 = os.environ.get('AAA_CU') == '6'   # v6: v5's fire history, taller and stronger, without v5's roughness lift (v5 split on 'satin' vs 'salmon plastic'; both: fire history not visible)   # v5: visible fire history (t7/t8 stand: 'plasticky orange, no soot or heat discolouration near the burner')
    if V2:   # browner, less orange oxide (reviewers: "orange plastic"); highs stay salmon
        bright = (0.92, 0.55, 0.40); oxide = (0.30, 0.14, 0.08); dark = (0.13, 0.07, 0.045); verd = (0.09, 0.24, 0.17); patina = max(patina, 0.8)
    # even oxide, varied only by broad, vertically stretched tone (gravity-led), never round blobs
    tone = T.noise(T.comb(T.math('MULTIPLY', ang, 2.0), T.math('MULTIPLY', z, 4.0), 0.0), scale=3.0, detail=4)
    c = T.mix(T.smoothstep(tone, 0.3, 0.75), oxide, T.mix(patina, oxide, dark))
    oxm = T.math('ADD', 0.75, T.math('MULTIPLY', patina, 0.25))
    isl = T.smoothstep(T.noise(p, scale=55, detail=6, rough=0.7), 0.6, 0.66)
    c = T.mix(T.math('MULTIPLY', isl, 0.7), c, (0.22, 0.09, 0.08))
    bloom = T.smoothstep(T.noise(p, scale=22, detail=6, rough=0.7), 0.66, 0.72)
    c = T.mix(T.math('MULTIPLY', bloom, 0.5), c, (0.16, 0.2, 0.12))
    # tarnish streaks: noise stretched along gravity (fine in angle, long in z), each streak fading out at
    # its own length below a lip; low contrast (a darker, matte tone of the oxide), never solid columns.
    # (1D-Voronoi runs looked like cartoon teeth on metal at any width, s17-s18.)
    runs = 0.0
    for i, lip in enumerate(lips):
        below = T.math('SUBTRACT', lip, z)
        st = T.noise(T.comb(T.math('MULTIPLY', ang, 9.0), T.math('MULTIPLY', z, 1.2), 1.7 * i), scale=3.0, detail=5, rough=0.65)
        ln_ = T.maprange(T.noise(T.comb(T.math('MULTIPLY', ang, 6.0), 0.0, 3.3 * i), scale=2.0, detail=2), 0.3, 0.7, 0.01, 0.09)
        fade = T.math('MULTIPLY', T.smoothstep(below, -0.002, 0.004), T.smoothstep(T.math('DIVIDE', below, ln_), 1.0, 0.3))
        rn = T.math('MULTIPLY', T.smoothstep(st, 0.5, 0.68), fade)
        runs = rn if i == 0 else T.math('MAXIMUM', runs, rn)
    if lips:
        c = T.mix(T.math('MULTIPLY', runs, 0.6), c, T.mix(T.noise(p, scale=30, detail=3), T.hsv(oxide, 0.5, 0.9, 0.45), (0.05, 0.06, 0.04)))
    # polish: hammer-facet highs (edge mask) and handled places go bright and glossy
    pol = T.math('MULTIPLY', edge, 0.35)
    for (px, py, pz, pr) in polish:
        d = T.vmath('LENGTH', T.vmath('SUBTRACT', lpv, (px, py, pz)))
        pol = T.math('MAXIMUM', pol, T.math('MULTIPLY', T.smoothstep(d, pr, pr * 0.35), T.smoothstep(T.noise(p, scale=25, detail=4), 0.25, 0.45)))
    seamm = 0.0
    if seam is not None:
        sa, sw = seam
        da = T.math('ABSOLUTE', T.math('SUBTRACT', T.math('MODULO', T.math('ADD', T.math('SUBTRACT', ang, sa), 9.42478), 6.28319), 3.14159))
        seamm = T.smoothstep(da, sw, sw * 0.8)
        seam_edge = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', da, sw)), 0.012, 0.0), T.smoothstep(T.noise(p, scale=50, detail=3), 0.35, 0.55))
        c = T.mix(T.math('MULTIPLY', seamm, 0.6), c, T.mix(0.5, dark, oxide))
        c = T.mix(seam_edge, c, verd)
    if lips: pol = T.math('MULTIPLY', pol, T.math('SUBTRACT', 1.0, runs))
    c = T.mix(pol, c, T.mix(T.noise(p, scale=40, detail=3), bright, T.hsv(bright, 0.49, 1.1, 0.75)))
    vm = T.math('MULTIPLY', T.math('MULTIPLY', T.math('POWER', dirt, 1.5), T.smoothstep(T.noise(p, scale=40, detail=4), 0.45, 0.6)), patina)   # crevices only
    c = T.mix(vm, c, verd)
    metal = T.mixf(pol, 0.9, 1.0); metal = T.mixf(vm, metal, 0.0)
    r = T.mixf(pol, 0.22, 0.12); r = T.mixf(vm, r, 0.85)
    if lips: r = T.mixf(T.math('MULTIPLY', runs, 0.8), r, 0.5); metal = T.mixf(T.math('MULTIPLY', runs, 0.8), metal, 0.55)
    if seam is not None: r = T.mixf(seamm, r, 0.55); metal = T.mixf(seam_edge, metal, 0.0); r = T.mixf(seam_edge, r, 0.85)
    r = T.math('ADD', r, T.math('MULTIPLY', T.math('SUBTRACT', T.noise(p, scale=60, detail=4), 0.5), 0.1))
    r = T.math('ADD', r, T.math('MULTIPLY', ham, 0.08))
    # fingerprints / wipe marks in the roughness of the polished zones
    fp = T.smoothstep(T.math('ABSOLUTE', T.math('SINE', T.math('MULTIPLY', T.vmath('LENGTH', T.vmath('SUBTRACT', T.vmath('FRACTION', T.vmath('MULTIPLY', p, (28.0, 28.0, 28.0))), (0.5, 0.5, 0.5))), 180.0))), 0.6, 0.95)
    r = T.math('ADD', r, T.math('MULTIPLY', T.math('MULTIPLY', fp, pol), 0.06))
    if soot_z is not None:
        soot = T.math('MULTIPLY', T.smoothstep(z, soot_z, soot_z - 0.035), T.smoothstep(T.noise(p, scale=8, detail=5), 0.3, 0.55))
        if soot_down:
            # smoke from the brazier also blackens every down-facing surface (head bulb underside, beak)
            dn = T.smoothstep((T.sep(T.attr('ln')), 'Z'), -0.15, -0.6)
            soot = T.math('MAXIMUM', soot, T.math('MULTIPLY', T.math('MULTIPLY', dn, T.smoothstep(T.noise(p, scale=12, detail=5), 0.25, 0.55)), soot_down))
        c = T.mix(soot, c, (0.012, 0.011, 0.01)); metal = T.mixf(soot, metal, 0.0); r = T.mixf(soot, r, 0.9)
    h = T.math('ADD', T.math('MULTIPLY', ham, 0.5), T.math('MULTIPLY', T.noise(p, scale=500, detail=3), 0.4))
    if lips: h = T.math('ADD', h, T.math('MULTIPLY', runs, 0.25))
    if V2:
        # hammer blows: spherical dimples (distance^2 of F1 cells ~9 mm, jittered) with crisp rims, so the broad
        # highlight breaks into many small glints; a lapped seam as a real step on one side of the seam line
        dimple = T.math('POWER', T.voronoi(T.vmath('ADD', p, T.vmath('MULTIPLY', T.noise(p, scale=8, detail=2, out='Color'), (0.004, 0.004, 0.004))), scale=110.0, out='Distance'), 0.6)
        h = T.math('ADD', T.math('MULTIPLY', dimple, 1.0), T.math('MULTIPLY', T.noise(p, scale=500, detail=3), 0.25))
        if lips: h = T.math('ADD', h, T.math('MULTIPLY', runs, 0.2))
        if seam is not None:
            step = T.smoothstep(T.math('SUBTRACT', T.math('MODULO', T.math('ADD', T.math('SUBTRACT', ang, sa), 9.42478), 6.28319), 3.14159), -0.004, 0.004)
            h = T.math('ADD', h, T.math('MULTIPLY', T.math('MULTIPLY', step, seamm), 0.8))
        r = T.math('ADD', r, T.math('MULTIPLY', T.math('SUBTRACT', 1.0, dimple), 0.06))   # dimple floors slightly duller (oxide stays in the hollows)
        c = T.mix(T.math('MULTIPLY', T.math('SUBTRACT', 1.0, dimple), 0.35), c, T.mix(0.5, c, dark))
        if V3:
            # fine planishing: dense crisp facets (~4 mm) over the coarse dents -> many tiny glints
            fine = T.voronoi(p, scale=260.0, out='Distance')
            dens = T.maprange(T.noise(p, scale=3.0, detail=2), 0.35, 0.65, 0.15, 1.0) if V4 else 1.0   # v4: planished smooth zones vs rough zones
            h = T.math('ADD', h, T.math('MULTIPLY', T.math('MULTIPLY', T.math('POWER', fine, 0.5), 0.55), dens))
            # patchy dull / bright (handled vs neglected), broad
            r = T.math('ADD', r, T.math('MULTIPLY', T.math('SUBTRACT', T.noise(p, scale=4.0, detail=3), 0.5), 0.3))
            # verdigris: real green patches with speckled edges, more low down and along the seam
            vn = T.math('ADD', T.noise(p, scale=7.0, detail=6, rough=0.65), T.math('MULTIPLY', T.smoothstep(z, 0.36, 0.2), 0.08))
            vp = T.math('MULTIPLY', T.smoothstep(vn, 0.6, 0.66), T.smoothstep(T.noise(p, scale=90, detail=3), 0.3, 0.5))
            if seam is not None: vp = T.math('MAXIMUM', vp, T.math('MULTIPLY', seam_edge, 1.0))
            if V4 and lips:   # moisture runs: verdigris gathers under the lips and joints
                vp = T.math('MAXIMUM', vp, T.math('MULTIPLY', runs, T.smoothstep(T.noise(p, scale=30, detail=4), 0.4, 0.6)))
            vcol = T.mix(T.noise(p, scale=40, detail=3), (0.09, 0.26, 0.19), (0.20, 0.33, 0.26))
            if V4: vcol = T.mix(T.noise(p, scale=40, detail=3), (0.16, 0.40, 0.30), (0.34, 0.52, 0.42))   # pale, chalky: survives warm firelight
            c = T.mix(T.math('MULTIPLY', vp, 0.9), c, vcol); metal = T.mixf(vp, metal, 0.0); r = T.mixf(vp, r, 0.85)
            h = T.math('ADD', h, T.math('MULTIPLY', vp, 0.3))
            if soot_z is not None:
                # heat tint above the soot line: dull purple-brown oxide band, then the soot gets heavier
                ht = T.math('MULTIPLY', T.smoothstep(z, soot_z + 0.06, soot_z), T.smoothstep(T.noise(p, scale=10, detail=4), 0.3, 0.6))
                c = T.mix(T.math('MULTIPLY', ht, 0.7), c, (0.16, 0.07, 0.08)); r = T.mixf(ht, r, 0.5)
                sootb = T.math('MULTIPLY', T.smoothstep(z, soot_z - 0.01, soot_z - 0.06), T.smoothstep(T.noise(p, scale=14, detail=5), 0.25, 0.5))
                c = T.mix(sootb, c, (0.01, 0.009, 0.008)); metal = T.mixf(sootb, metal, 0.0); r = T.mixf(sootb, r, 0.9)
            if seam is not None:
                # rivet heads every 22 mm along the seam (domed, a shade brighter where worn)
                rz = T.math('SUBTRACT', T.math('MODULO', T.math('ADD', z, 10.0), 0.022), 0.011)
                dr = T.math('SQRT', T.math('ADD', T.math('POWER', T.math('MULTIPLY', da, 0.12), 2.0), T.math('POWER', rz, 2.0)))
                riv = T.smoothstep(dr, 0.0032, 0.002)
                h = T.math('ADD', h, T.math('MULTIPLY', T.math('SQRT', T.math('SUBTRACT', 1.0, T.math('POWER', T.math('MINIMUM', T.math('DIVIDE', dr, 0.0032), 1.0), 2.0))), 1.4))
                c = T.mix(T.math('MULTIPLY', riv, 0.5), c, T.hsv(bright, 0.5, 0.9, 0.7)); metal = T.mixf(riv, metal, 1.0)
            if V5 and soot_z is not None:
                # tall heat-tint gradient rising from the burner: dark straw-bronze, then purple-brown, then the soot
                hz = T.math('SUBTRACT', z, soot_z)
                brk = T.maprange(T.noise(p, scale=6, detail=4), 0.3, 0.7, -0.025, 0.025)
                hz = T.math('ADD', hz, brk)
                K6 = 1.6 if V6 else 1.0   # v6: the gradient reaches higher up the lower bulb
                straw = T.math('MULTIPLY', T.smoothstep(hz, 0.13 * K6, 0.07 * K6), T.smoothstep(hz, 0.0, 0.04))
                purp = T.smoothstep(hz, 0.06 * K6, 0.015)
                c = T.mix(T.math('MULTIPLY', straw, 0.75 if V6 else 0.55), c, (0.26, 0.12, 0.05)); r = T.mixf(straw, r, 0.42)
                c = T.mix(T.math('MULTIPLY', purp, 0.9 if V6 else 0.75), c, (0.09, 0.035, 0.05)); r = T.mixf(purp, r, 0.55)
                # soot plumes licking up the lower bulb: vertical streaks, broken
                plume = T.smoothstep(T.noise(T.comb(T.math('MULTIPLY', ang, 2.2), 0.0, T.math('MULTIPLY', z, 0.6)), scale=4.0, detail=4), 0.5, 0.7)
                plume = T.math('MULTIPLY', T.math('MULTIPLY', plume, T.smoothstep(hz, 0.15 if V6 else 0.09, 0.0)), T.smoothstep(T.noise(p, scale=20, detail=5), 0.3, 0.55))
                c = T.mix(T.math('MULTIPLY', plume, 0.85), c, (0.015, 0.012, 0.01)); metal = T.mixf(plume, metal, 0.0); r = T.mixf(plume, r, 0.9)
            if V5 and not V6: r = T.math('ADD', r, 0.1)   # duller overall: KCD2's metal reads dull and oily, ours 'plasticky'
            if V4:   # worn bare copper on edges, rims, seam lap and rivet heads (rubbed bright)
                wear_ = T.math('MULTIPLY', T.math('MAXIMUM', edge, riv if seam is not None else 0.0), T.smoothstep(T.noise(p, scale=35, detail=4), 0.35, 0.55))
                c = T.mix(T.math('MULTIPLY', wear_, 0.85), c, bright); metal = T.mixf(wear_, metal, 1.0); r = T.mixf(wear_, r, 0.14)
        T.output(c, r, metal, T.bump(h, strength=0.9, distance=0.0012))
        return m
    T.output(c, r, metal, T.bump(h, strength=0.5, distance=0.0006))
    return m

def ceramic(name='M_Jug', body=(0.30, 0.12, 0.05), glaze=(0.10, 0.12, 0.05), glaze_z=0.03, gloss=0.12, paint=None, soot_above=None, soot_vents=(), flash=0.0):
    """Thrown earthenware: throwing rings, glaze over the top with a drippy edge, bare foot below lp z=glaze_z."""
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.002, 0.02)
    use_scan(T, 'clay_plaster', p, 4.0, col=0.3, rough=0.3, height=1.2)
    z = T.sep(T.attr('lp'))
    ang = T.math('ARCTAN2', (z, 'Y'), (z, 'X'))
    # glaze edge: a wavy line plus individual runs (each angular cell = one run with its own length,
    # tapering to a rounded tip), instead of a sawtooth
    wav = T.math('MULTIPLY', T.math('SUBTRACT', T.noise(T.comb(T.math('MULTIPLY', ang, 1.5), 0.0, 0.0), scale=2.0, detail=3), 0.5), 0.02)
    vc = T.node('ShaderNodeTexVoronoi', feature='F1'); vc.voronoi_dimensions = '1D'
    T.set(vc, 'W', T.math('MULTIPLY', ang, 3.5)); vc.inputs['Scale'].default_value = 1.0
    runlen = T.math('MULTIPLY', T.math('POWER', (T.sep((vc, 'Color')), 'X'), 2.0), 0.06)
    tz = T.math('DIVIDE', T.math('SUBTRACT', glaze_z, T.math('ADD', (z, 'Z'), wav)), T.math('ADD', runlen, 0.001))
    width = T.math('MULTIPLY', T.math('SUBTRACT', 1.0, T.math('POWER', T.math('MAXIMUM', tz, 0.0), 1.5)), 0.32)
    inrun = T.math('MULTIPLY', T.math('LESS_THAN', (vc, 'Distance'), width), T.smoothstep(tz, 1.0, 0.95))
    above = T.smoothstep(T.math('ADD', (z, 'Z'), wav), glaze_z - 0.0015, glaze_z + 0.0015)
    gm = T.math('MAXIMUM', above, inrun)
    bead = 0.0
    JV = os.environ.get('AAA_JUG', '1')
    if JV == '4':
        # v4: keep the old ragged tongues (reviewers prefer them to any straight edge) but give each its own
        # width and length, a rounded tip and soft sides, and a pooled darker bead along the edge and at tips
        rc4 = T.sep((vc, 'Color'))
        rl4 = T.math('ADD', T.math('MULTIPLY', T.math('POWER', (rc4, 'X'), 1.4), 0.07), 0.004)
        tz4 = T.math('DIVIDE', T.math('SUBTRACT', glaze_z, T.math('ADD', (z, 'Z'), wav)), rl4)
        w4 = T.math('MULTIPLY', T.math('SQRT', T.math('SUBTRACT', 1.0, T.math('MINIMUM', T.math('POWER', T.math('MAXIMUM', tz4, 0.0), 2.0), 1.0))),
                    T.math('ADD', 0.14, T.math('MULTIPLY', (rc4, 'Z'), 0.3)))
        side4 = T.math('DIVIDE', T.math('SUBTRACT', w4, (vc, 'Distance')), 0.05, clamp=True)
        inrun = T.math('MULTIPLY', side4, T.smoothstep(tz4, 1.0, 0.9))
        gm = T.math('MAXIMUM', above, inrun)
        bead = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', T.math('ADD', (z, 'Z'), wav), glaze_z + 0.003)), 0.0035, 0.0), above)
        bead = T.math('MAXIMUM', bead, T.math('MULTIPLY', inrun, T.smoothstep(tz4, 0.55, 0.95)))
    if JV in ('2', '3'):
        # v2 edge (reviewers: "sawtooth / harsh artificial drip"): a slow wave plus fine wobble, only a few
        # drips (about 1 in 2 of ~7 cells) of very different length with soft tapered sides, a 3 mm thinning
        # fade into the body, and a darker glossy bead where the glaze pooled just above the edge
        zz = (z, 'Z')
        wav2 = T.math('ADD', T.math('MULTIPLY', T.math('SUBTRACT', T.noise(T.comb(T.math('COSINE', ang), T.math('SINE', ang), 0.0), scale=0.9, detail=2), 0.5), 0.03),
                      T.math('MULTIPLY', T.math('SUBTRACT', T.noise(p, scale=45, detail=3), 0.5), 0.004))
        ze = T.math('ADD', zz, wav2)
        dc = T.node('ShaderNodeTexVoronoi', feature='F1'); dc.voronoi_dimensions = '1D'
        T.set(dc, 'W', T.math('MULTIPLY', ang, 5.0 if JV == '3' else 1.1)); dc.inputs['Scale'].default_value = 1.0
        rc = T.sep((dc, 'Color'))
        on = T.smoothstep((rc, 'Y'), 0.2, 0.25) if JV == '3' else T.smoothstep((rc, 'Y'), 0.45, 0.5)
        rl = T.math('MULTIPLY', T.math('POWER', (rc, 'X'), 2.2), 0.07 if JV == '3' else 0.05)   # v3: mostly short, a few long
        tz2 = T.math('DIVIDE', T.math('SUBTRACT', glaze_z, ze), T.math('ADD', rl, 0.001))
        if JV == '3':   # rounded tip: width follows a half-ellipse toward the end, sides wobble
            w2 = T.math('MULTIPLY', T.math('SQRT', T.math('SUBTRACT', 1.0, T.math('MINIMUM', T.math('POWER', T.math('MAXIMUM', tz2, 0.0), 2.0), 1.0))),
                        T.math('ADD', 0.16, T.math('MULTIPLY', T.math('SUBTRACT', T.noise(p, scale=60, detail=2), 0.5), 0.08)))
        else:
            w2 = T.math('MULTIPLY', T.math('SUBTRACT', 1.0, T.math('POWER', T.math('MAXIMUM', tz2, 0.0), 0.8)), 0.12)
        side = T.math('DIVIDE', T.math('SUBTRACT', w2, (dc, 'Distance')), T.math('ADD', T.math('MULTIPLY', w2, 0.45), 0.0001), clamp=True)   # soft tapered sides
        run2 = T.math('MULTIPLY', T.math('MULTIPLY', side, T.smoothstep(tz2, 1.05, 0.85)), on)
        above2 = T.smoothstep(ze, glaze_z - 0.003, glaze_z + 0.0005)
        gm = T.math('MAXIMUM', above2, run2)
        bead = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', ze, glaze_z + 0.0035)), 0.004, 0.0), above2)
        bead = T.math('MAXIMUM', bead, T.math('MULTIPLY', run2, T.smoothstep(tz2, 0.5, 0.95)))   # drips end in a fat tip
    thick = T.noise(p, scale=10, detail=4)
    rings = T.math('SINE', T.math('ADD', T.math('MULTIPLY', (z, 'Z'), 700.0), T.math('MULTIPLY', T.noise(p, scale=6, detail=2), 4.0)))
    bc = T.mix(T.noise(p, scale=30, detail=5), body, T.hsv(body, 0.5, 0.9, 1.25))
    if flash:
        # kiln flashing: low-frequency blotches between oxidised orange and reduced grey-brown, +/-25 % value
        fl = T.noise(p, scale=6.0, detail=5, rough=0.65)
        bc = T.mix(T.math('MULTIPLY', T.smoothstep(fl, 0.45, 0.65), flash), bc, T.hsv(body, 0.52, 1.25, 1.2))
        bc = T.mix(T.math('MULTIPLY', T.smoothstep(fl, 0.48, 0.32), flash), bc, T.hsv(body, 0.5, 0.25, 0.45))
    gc = T.mix(thick, T.hsv(glaze, 0.5, 1.0, 0.6), T.hsv(glaze, 0.5, 1.1, 1.4))
    thin = T.smoothstep(T.noise(p, scale=7, detail=5, rough=0.6), 0.58, 0.72)
    gc = T.mix(T.math('MULTIPLY', thin, 0.75), gc, T.mix(0.5, bc, gc, 'MULTIPLY'))
    gc = T.mix(T.math('MULTIPLY', thin, 0.6), gc, T.hsv(gc, 0.47, 1.1, 1.5))
    spk = T.smoothstep(T.voronoi(p, scale=260.0, out='Distance'), 0.08, 0.0)
    gc = T.mix(T.math('MULTIPLY', spk, 0.7), gc, (0.015, 0.01, 0.004))
    gc = T.mix(T.math('MULTIPLY', edge, 0.6), gc, bc)                     # glaze thins on rims
    c = T.mix(gm, bc, gc)
    if bead != 0.0: c = T.mix(T.math('MULTIPLY', bead, 0.6), c, T.hsv(gc, 0.5, 1.15, 0.55))
    if paint is not None:
        # albarello decoration: two cobalt bands and a scrolling leaf band between, brushy edges
        zz = (z, 'Z'); br = T.math('MULTIPLY', T.noise(p, scale=60, detail=3), 0.004)
        def band(lo, hi):
            return T.math('MULTIPLY', T.smoothstep(T.math('ADD', zz, br), lo - 0.001, lo + 0.001), T.smoothstep(T.math('ADD', zz, br), hi + 0.001, hi - 0.001))
        bands = T.math('MAXIMUM', band(0.028, 0.036), band(0.104, 0.112))
        wave = T.math('ADD', 0.07, T.math('MULTIPLY', T.math('SINE', T.math('MULTIPLY', ang, 5.0)), 0.016))
        scroll = T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', T.math('ADD', zz, br), wave)), 0.004, 0.002)
        leaf = T.voronoi(T.comb(T.math('MULTIPLY', ang, 6.0), T.math('MULTIPLY', zz, 28.0), 0.0), scale=1.0, out='Distance')
        hatch = T.smoothstep(T.math('ABSOLUTE', T.math('SINE', T.math('MULTIPLY', T.math('ADD', T.math('MULTIPLY', ang, 0.06), zz), 900.0))), 0.35, 0.1)
        leaf = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(leaf, 0.3, 0.22), T.math('ADD', 0.55, T.math('MULTIPLY', hatch, 0.45))), band(0.045, 0.095))
        scroll = T.math('MAXIMUM', scroll, T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', T.math('ADD', zz, br), T.math('ADD', wave, 0.007))), 0.0025, 0.0012))
        pm = T.math('MULTIPLY', T.math('MAXIMUM', T.math('MAXIMUM', bands, scroll), T.math('MULTIPLY', leaf, 0.85)), gm)
        pm = T.math('MULTIPLY', pm, T.maprange(T.noise(p, scale=80, detail=3), 0.3, 0.7, 0.75, 1.0))
        c = T.mix(pm, c, paint)
    c = T.mix(T.math('MULTIPLY', dirt, 0.6), c, (0.03, 0.025, 0.02))
    bare = T.math('SUBTRACT', 1.0, gm)
    CL2 = os.environ.get('AAA_CLAY') == '2'
    if CL2:
        # iron speckle (dark grog/iron spots, 0.5-1.5 mm), lighter grog flecks, darker foot (handled, set down in
        # damp and ash), and a slightly duller, browner body: the cues reviewers named for real earthenware
        isp = T.smoothstep(T.voronoi(p, scale=420.0, out='Distance'), 0.13, 0.04)
        isp = T.math('MULTIPLY', isp, T.smoothstep(T.noise(p, scale=30, detail=2), 0.35, 0.6))
        c = T.mix(T.math('MULTIPLY', T.math('MULTIPLY', isp, bare), 0.85), c, (0.04, 0.022, 0.012))
        grog = T.math('MULTIPLY', T.smoothstep(T.voronoi(T.vmath('ADD', p, (3.1, 1.7, 0.4)), scale=300.0, out='Distance'), 0.08, 0.02), bare)
        c = T.mix(T.math('MULTIPLY', grog, 0.5), c, T.hsv(c, 0.5, 0.6, 1.5))
        foot = T.math('MULTIPLY', T.smoothstep((z, 'Z'), 0.03, 0.004), T.smoothstep(T.noise(p, scale=12, detail=4), 0.25, 0.55))
        c = T.mix(T.math('MULTIPLY', foot, 0.75), c, (0.035, 0.026, 0.02))
        c = T.mix(T.math('MULTIPLY', bare, 0.35), c, T.hsv(c, 0.5, 0.8, 0.85))
    hair = T.smoothstep(T.voronoi(T.vmath('MULTIPLY', p, (1, 1, 0.6)), scale=35.0, feature='DISTANCE_TO_EDGE', out='Distance'), 0.012, 0.0)
    hair = T.math('MULTIPLY', T.math('MULTIPLY', hair, bare), T.smoothstep(T.noise(p, scale=5, detail=2), 0.55, 0.6))
    c = T.mix(T.math('MULTIPLY', hair, 0.8), c, (0.03, 0.015, 0.008))
    BZ2 = os.environ.get('AAA_BRZ') in ('2', '3', '4', '5') and soot_above is not None; BZ3 = os.environ.get('AAA_BRZ') in ('3', '4', '5') and soot_above is not None
    BZ4 = os.environ.get('AAA_BRZ') in ('4', '5') and soot_above is not None   # v4: glow only inside the bore, dim and red; the lip gets scorch, not light (v3 split: 'clean even dots', 'painted')
    if soot_above is not None:
        sm = T.math('MULTIPLY', T.smoothstep((z, 'Z'), soot_above - (0.1 if BZ2 else 0.05), soot_above + 0.01), T.smoothstep(T.noise(p, scale=9, detail=5), 0.22 if BZ2 else 0.3, 0.6))
        c = T.mix(sm, c, (0.012, 0.01, 0.009))
        if BZ2:   # pale wood ash caked on the rim and the inner lip, fine and patchy
            nzs = T.sep(T.attr('ln'))
            ash = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep((z, 'Z'), soot_above + 0.02, soot_above + 0.04), T.smoothstep((nzs, 'Z'), 0.1, 0.6)),
                         T.smoothstep(T.noise(p, scale=40, detail=5), 0.38, 0.55))
            c = T.mix(T.math('MULTIPLY', ash, 0.85), c, T.mix(T.noise(p, scale=120, detail=3), (0.30, 0.29, 0.27), (0.5, 0.48, 0.45)))
    emb = 0.0
    for va, vz in soot_vents:
        # flame-licked soot plume rising from a vent: narrows and breaks up with height
        da = T.math('ABSOLUTE', T.math('SUBTRACT', T.math('MODULO', T.math('ADD', T.math('SUBTRACT', ang, va), 9.42478), 6.28319), 3.14159))
        hz = T.math('SUBTRACT', (z, 'Z'), vz)
        width = T.math('SUBTRACT', 0.75 if BZ2 else 0.55, T.math('MULTIPLY', hz, 2.2 if BZ2 else 3.0))
        core = T.math('SUBTRACT', 1.0, T.math('DIVIDE', da, T.math('MAXIMUM', width, 0.01)), clamp=True)
        plume = T.math('MULTIPLY', T.smoothstep(core, 0.0, 0.6), T.smoothstep(hz, -0.01, 0.01))
        plume = T.math('MULTIPLY', plume, T.smoothstep(T.math('ADD', T.noise(p, scale=14, detail=5), T.math('MULTIPLY', hz, -3.0)), 0.15, 0.45))
        c = T.mix(T.math('MULTIPLY', plume, 0.9), c, (0.012, 0.01, 0.009))
        if BZ3:   # scorch ring hugging each vent: burnt orange-black clay right at the opening
            dd = T.math('SQRT', T.math('ADD', T.math('POWER', T.math('MULTIPLY', da, 0.12), 2.0), T.math('POWER', hz, 2.0)))   # metres from the hole centre
            ring_ = T.smoothstep(dd, 0.0135, 0.0095)   # the bore and a ~3 mm lip round the circular hole
            c = T.mix(T.math('MULTIPLY', ring_, 0.8), c, (0.05, 0.018, 0.006))
            if BZ4: ring_ = T.math('MULTIPLY', T.smoothstep(dd, 0.0105, 0.005), T.mixf(T.noise(p, scale=60, detail=3), 0.55, 1.0))   # bore only, broken up
            emb = ring_ if emb == 0.0 else T.math('MAXIMUM', emb, ring_)
    if BZ4 and os.environ.get('AAA_BRZ') == '5':   # v5: the inner wall is soot-black with a dull red glow low down (v4 tie: 'the lit hole reads as a pale pink disc' = the bright inside wall seen through it)
        ln = T.sep(T.attr('ln'))
        rl = T.math('MAXIMUM', T.math('SQRT', T.math('ADD', T.math('POWER', (z, 'X'), 2.0), T.math('POWER', (z, 'Y'), 2.0))), 0.001)
        inward = T.math('DIVIDE', T.math('ADD', T.math('MULTIPLY', (ln, 'X'), (z, 'X')), T.math('MULTIPLY', (ln, 'Y'), (z, 'Y'))), T.math('MULTIPLY', rl, -1.0))
        inner = T.math('MULTIPLY', T.smoothstep(inward, 0.2, 0.6), T.smoothstep((z, 'Z'), soot_above + 0.04, soot_above + 0.02))
        c = T.mix(inner, c, T.mix(T.noise(p, scale=30, detail=4), (0.012, 0.008, 0.006), (0.04, 0.016, 0.008)))
        glow = T.math('MULTIPLY', T.math('MULTIPLY', inner, T.smoothstep((z, 'Z'), soot_above - 0.01, soot_above - 0.07)), T.mixf(T.noise(p, scale=25, detail=4), 0.3, 1.0))
        emb = glow if emb == 0.0 else T.math('MAXIMUM', emb, glow)
    if JV in ('3', '4') and name == 'M_JugClay': gloss = 0.07   # reviewers: lead glaze reads wet and glassy
    r = T.mixf(gm, 0.85, gloss); r = T.mixf(T.math('MULTIPLY', dirt, 0.7), r, 0.8)
    r = T.math('ADD', r, T.math('MULTIPLY', T.noise(p, scale=20, detail=3), 0.08))
    craz = T.smoothstep(T.voronoi(p, scale=140.0, feature='DISTANCE_TO_EDGE', out='Distance'), 0.02, 0.0)
    c = T.mix(T.math('MULTIPLY', T.math('MULTIPLY', craz, gm), 0.25), c, (0.02, 0.016, 0.01))
    h = T.math('ADD', T.math('MULTIPLY', rings, 0.15), T.math('MULTIPLY', T.math('SUBTRACT', 1.0, gm), T.noise(p, scale=200, detail=3)))
    h = T.math('SUBTRACT', h, T.math('MULTIPLY', T.math('MULTIPLY', craz, gm), 0.3))
    h = T.math('SUBTRACT', h, T.math('MULTIPLY', hair, 0.8))
    h = T.math('ADD', h, T.math('MULTIPLY', bare, T.math('MULTIPLY', T.noise(p, scale=35, detail=6, rough=0.7), 0.8)))
    if CL2:   # throwing rings read on the bare body too (finger ridges, ~3 mm pitch, wandering), speckle pits
        h = T.math('ADD', h, T.math('MULTIPLY', T.math('MULTIPLY', rings, bare), 0.5))
        h = T.math('SUBTRACT', h, T.math('MULTIPLY', isp, 0.6))
    if bead != 0.0:
        r = T.mixf(bead, r, 0.06); h = T.math('ADD', h, T.math('MULTIPLY', bead, 0.6))
    if emb != 0.0:   # ember glow on the vent lips (the fire inside lights the opening): baked to the emission map
        T.output(c, r, 0.0, T.bump(h, strength=0.55, distance=0.0006), Emission_Color=(1.0, 0.2, 0.035) if BZ4 else (1.0, 0.36, 0.08), Emission_Strength=T.math('MULTIPLY', T.math('POWER', emb, 2.0), 0.7 if BZ4 else 1.2))
        return m
    T.output(c, r, 0.0, T.bump(h, strength=0.55, distance=0.0006))
    return m

def wax(name='M_Wax', col=(0.42, 0.30, 0.11)):
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.002, 0.01)
    c = T.mix(T.noise(p, scale=20, detail=4), T.hsv(col, 0.5, 1.0, 0.85), T.hsv(col, 0.5, 0.9, 1.1))
    c = T.mix(T.math('MULTIPLY', dirt, 0.5), c, (0.12, 0.08, 0.03))
    r = T.mixf(T.noise(p, scale=30, detail=3), 0.35, 0.55)
    T.output(c, r, 0.0, T.bump(T.noise(p, scale=120, detail=3), strength=0.15, distance=0.0004))
    return m

def leather(name='M_Leather', col=(0.09, 0.035, 0.015)):
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.003, 0.015)
    use_scan(T, 'brown_leather', p, 3.0, col=0.25, rough=0.3, height=1.0)
    grain = T.voronoi(p, scale=600.0, feature='DISTANCE_TO_EDGE', out='Distance')
    cr = T.smoothstep(grain, 0.06, 0.0)
    wrinkle = T.noise(T.vmath('MULTIPLY', p, (1, 4, 1)), scale=40, detail=4)
    c = T.mix(T.noise(p, scale=15, detail=5), T.hsv(col, 0.5, 1.0, 0.7), T.hsv(col, 0.5, 1.0, 1.3))
    c = T.mix(T.math('MULTIPLY', edge, 0.8), c, T.hsv(col, 0.49, 0.7, 2.4))        # scuffed lighter edges
    c = T.mix(T.math('MULTIPLY', dirt, 0.7), c, (0.01, 0.006, 0.004))
    r = T.mixf(edge, 0.55, 0.4); r = T.mixf(cr, r, 0.75)
    h = T.math('SUBTRACT', T.math('MULTIPLY', wrinkle, 0.6), T.math('MULTIPLY', cr, 0.3))
    T.output(c, r, 0.0, T.bump(h, strength=0.6, distance=0.0006))
    return m

def paper(name='M_Paper', col=(0.40, 0.31, 0.18), ink=True, half=(0.1, 0.13)):
    """Rag paper; ink lines on up-facing pages within a margin of the part's local X/Y half-size."""
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.002, 0.01)
    c = T.mix(T.noise(p, scale=8, detail=6), col, T.hsv(col, 0.5, 1.2, 0.8))
    c = T.mix(T.math('MULTIPLY', edge, 0.35), c, T.hsv(col, 0.48, 1.2, 0.75))
    fox = T.math('MULTIPLY', T.smoothstep(T.voronoi(p, scale=35.0, out='Distance'), 0.12, 0.04), T.smoothstep(T.noise(p, scale=4, detail=2), 0.5, 0.6))
    c = T.mix(T.math('MULTIPLY', fox, 0.5), c, T.hsv(col, 0.47, 1.4, 0.6))
    if ink:
        # manuscript page: two text columns of gothic minims (vertical strokes), word gaps,
        # ascenders, red rubric initials at paragraph starts; iron-gall brown ink
        lpv = T.attr('lp'); lp = T.sep(lpv); nz = T.sep(T.attr('ln'))
        X = (lp, 'X'); Y = (lp, 'Y')
        BK2 = os.environ.get('AAA_BOOK') == '2'   # v2: script legible at stand distance (bolder, larger, darker)
        LH = 0.0085 if BK2 else 0.0062                           # line pitch
        line_id = T.math('FLOOR', T.math('DIVIDE', Y, LH))
        fy = T.math('FRACT', T.math('DIVIDE', Y, LH))
        xh = T.math('MULTIPLY', T.smoothstep(fy, 0.25, 0.32), T.smoothstep(fy, 0.68, 0.61))     # x-height band
        asc = T.math('MULTIPLY', T.smoothstep(fy, 0.12, 0.2), T.smoothstep(fy, 0.68, 0.61))
        jit = T.math('MULTIPLY', T.noise(T.comb(X, line_id, 0.0), scale=600.0, detail=1), 0.0004)
        st = T.math('FRACT', T.math('MULTIPLY', T.math('ADD', X, jit), 800.0 if BK2 else 1100.0))  # minim pitch ~0.9 mm (v2 1.25 mm)
        stroke = T.math('MULTIPLY', T.smoothstep(st, 0.0, 0.08), T.smoothstep(st, 0.75, 0.55)) if BK2 else T.math('MULTIPLY', T.smoothstep(st, 0.0, 0.12), T.smoothstep(st, 0.55, 0.38))
        word = T.smoothstep(T.noise(T.comb(T.math('MULTIPLY', X, 160.0), T.math('MULTIPLY', line_id, 7.3), 0.0), scale=1.0, detail=1), 0.36, 0.40)
        tall = T.smoothstep(T.white(T.comb(T.math('FLOOR', T.math('MULTIPLY', X, 1100.0)), line_id, 1.0), dim='3D'), 0.86, 0.88)
        glyph = T.math('MULTIPLY', stroke, T.math('MAXIMUM', xh, T.math('MULTIPLY', asc, tall)))
        textm = T.math('MULTIPLY', glyph, word)
        # columns and margins (page half-size half[0] x half[1]); gutter between columns
        ax = T.math('ABSOLUTE', X); ay = T.math('ABSOLUTE', Y)
        colm = T.math('MULTIPLY', T.smoothstep(ax, half[0] - 0.016, half[0] - 0.02), T.smoothstep(ax, 0.004, 0.007))
        rowm = T.smoothstep(ay, half[1] - 0.02, half[1] - 0.024)
        para = T.smoothstep(T.white(T.comb(line_id, T.math('SIGN', X), 2.0), dim='3D'), 0.94, 0.945)        # some lines start a paragraph
        short = T.math('MULTIPLY', para, T.smoothstep(T.math('SUBTRACT', ax, 0.007), 0.05, 0.045))
        textm = T.math('MULTIPLY', textm, T.math('SUBTRACT', 1.0, T.math('MULTIPLY', short, 0.0)))
        page = T.smoothstep((nz, 'Z'), 0.7, 0.9)
        textm = T.math('MULTIPLY', T.math('MULTIPLY', textm, page), T.math('MULTIPLY', colm, rowm))
        fade = T.maprange(T.noise(p, scale=25, detail=3), 0.3, 0.7, 0.6, 1.0)
        c = T.mix(T.math('MULTIPLY', textm, T.math('MINIMUM', T.math('MULTIPLY', fade, 1.35 if BK2 else 1.0), 1.0)), c, (0.018, 0.009, 0.004) if BK2 else (0.035, 0.018, 0.008))
        # rubric initials: red squares two lines tall at the column start of paragraph lines
        ini = T.math('MULTIPLY', T.math('MULTIPLY', para, T.smoothstep(ax, 0.007, 0.0075)), T.smoothstep(ax, 0.0135, 0.013))
        ini = T.math('MULTIPLY', T.math('MULTIPLY', ini, T.smoothstep(fy, 0.1, 0.15)), T.math('MULTIPLY', page, rowm))
        c = T.mix(T.math('MULTIPLY', ini, 0.9), c, (0.30, 0.03, 0.02))
        side = T.smoothstep(T.math('ABSOLUTE', (nz, 'Z')), 0.5, 0.2)
        leaves = T.math('MULTIPLY', T.smoothstep(T.math('SINE', T.math('ADD', T.math('MULTIPLY', (lp, 'Z'), 4200.0), T.math('MULTIPLY', T.noise(p, scale=30, detail=2), 3.0))), 0.2, 0.9), side)
        c = T.mix(T.math('MULTIPLY', leaves, 0.35), c, T.hsv(col, 0.48, 1.3, 0.55))
    T.output(c, 0.8, 0.0, T.bump(T.noise(p, scale=200, detail=3), strength=0.1, distance=0.0003))
    return m

def charcoal(name='M_Charcoal', glow=1.0):
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.002, 0.01)
    cr = T.voronoi(p, scale=60.0, feature='DISTANCE_TO_EDGE', out='Distance')
    crk = T.smoothstep(cr, 0.05, 0.0)
    ash = T.smoothstep(T.noise(p, scale=25, detail=5), 0.5, 0.65)
    c = T.mix(ash, (0.015, 0.014, 0.013), (0.25, 0.24, 0.22))
    T.output(c, T.mixf(ash, 0.6, 0.95), 0.0, T.bump(T.math('SUBTRACT', T.noise(p, scale=90, detail=5), crk), strength=0.6, distance=0.001),
             Emission_Color=(1.0, 0.25, 0.04), Emission_Strength=T.math('MULTIPLY', T.math('MULTIPLY', T.math('MAXIMUM', T.math('MULTIPLY', crk, 0.6), T.smoothstep(T.noise(p, scale=18, detail=4), 0.55, 0.75)), T.math('SUBTRACT', 1.0, ash)), 1.0 * glow))
    return m

def ember(name='M_Ember', glow=1.0, ash_bed=False):
    """Burning charcoal pieces (use with separate, NOT voxel-unioned coal parts). Each piece: black char with
    alligator cracks and a satin sheen; it glows where it faces DOWN into the bed and inside the occluded gaps
    between pieces (world normal + local AO), hotter pieces chosen per pid; pale ash crust on the up faces
    kills the glow. ash_bed=True: the grey ash bed under the coals, glowing only in a few pores.
    Emission stays <= 1 (8-bit bake); scale it in the game material."""
    m, T = new_mat(name)
    _, p, pid = _part_space(T, jitter=(3.0, 3.0, 3.0))
    nz = T.sep(T.geo('Normal'))
    up = T.smoothstep((nz, 'Z'), 0.35, 0.85)
    down = T.smoothstep((nz, 'Z'), 0.2, -0.5)
    occ = T.math('SUBTRACT', 1.0, T.cavity(dist=0.012, lo=0.25, hi=0.9))     # 1 in gaps between pieces
    crk = T.smoothstep(T.voronoi(p, scale=140.0, feature='DISTANCE_TO_EDGE', out='Distance'), 0.045, 0.0)
    crk2 = T.smoothstep(T.voronoi(T.vmath('MULTIPLY', p, (1, 3, 1)), scale=45.0, feature='DISTANCE_TO_EDGE', out='Distance'), 0.03, 0.0)
    crk = T.math('MAXIMUM', crk, T.math('MULTIPLY', crk2, 0.8))
    piece = T.white(pid, out='Value')                                          # per-piece heat
    ashn = T.noise(p, scale=35, detail=6, rough=0.6)
    if ash_bed:
        AB5 = os.environ.get('AAA_BRZ') == '5'   # v5: the ash bed is what the air holes show; pale lit ash read as 'a pink-white disc'. More char, more glowing pores, greyer ash
        ash = T.smoothstep(ashn, 0.42, 0.66) if AB5 else T.smoothstep(ashn, 0.25, 0.5)
        heat = T.math('MULTIPLY', T.smoothstep(T.voronoi(p, scale=60.0, out='Distance'), 0.3 if AB5 else 0.12, 0.02), T.math('SUBTRACT', 1.0, ash))
        heat = T.math('MAXIMUM', heat, T.math('MULTIPLY', occ, 0.6))
    else:
        ash = T.math('MULTIPLY', up, T.smoothstep(ashn, 0.42, 0.62))
        ash = T.math('MAXIMUM', ash, T.math('MULTIPLY', T.math('MULTIPLY', up, 0.35), T.smoothstep(ashn, 0.3, 0.4)))
        heat = T.math('ADD', T.math('MULTIPLY', down, 0.85), T.math('MULTIPLY', occ, 0.9))
        heat = T.math('ADD', heat, T.math('MULTIPLY', crk, T.math('MULTIPLY', T.math('MAXIMUM', down, occ), 0.6)))
        heat = T.math('MULTIPLY', heat, T.maprange(piece, 0.0, 1.0, 0.25, 1.15))
        heat = T.math('MULTIPLY', heat, T.math('SUBTRACT', 1.0, ash))
    heat = T.math('MINIMUM', heat, 1.0)
    char = T.mix(T.noise(p, scale=80, detail=4), (0.008, 0.0075, 0.007), (0.035, 0.033, 0.031))
    ashc = T.mix(T.noise(p, scale=120, detail=3), (0.22, 0.21, 0.19), (0.42, 0.40, 0.37))
    if ash_bed and os.environ.get('AAA_BRZ') == '5': ashc = T.mix(T.noise(p, scale=120, detail=3), (0.09, 0.085, 0.08), (0.2, 0.19, 0.18))
    c = T.mix(ash, char, ashc)
    c = T.mix(T.math('MULTIPLY', heat, 0.7), c, (0.25, 0.05, 0.01))           # hot char reads orange-brown when unlit
    ecol = T.mix(T.math('POWER', heat, 1.5), (0.55, 0.06, 0.01), (1.0, 0.55, 0.15))
    r = T.mixf(ash, T.mixf(crk, 0.5, 0.8), 0.95)
    h = T.math('SUBTRACT', T.math('MULTIPLY', T.noise(p, scale=90, detail=5), 0.6), T.math('MULTIPLY', crk, 0.9))
    h = T.math('ADD', h, T.math('MULTIPLY', ash, T.math('MULTIPLY', T.noise(p, scale=300, detail=3), 0.6)))
    T.output(c, r, 0.0, T.bump(h, strength=0.7, distance=0.0008), Emission_Color=ecol, Emission_Strength=T.math('MULTIPLY', T.math('POWER', heat, 1.3), glow))
    return m

def cork(name='M_Cork', col=(0.25, 0.15, 0.08)):
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.002, 0.01)
    pits = T.smoothstep(T.voronoi(p, scale=350.0, out='Distance'), 0.15, 0.0)
    c = T.mix(T.noise(p, scale=60, detail=5), T.hsv(col, 0.5, 1, 0.75), T.hsv(col, 0.5, 1, 1.2))
    c = T.mix(T.math('MULTIPLY', pits, 0.7), c, (0.03, 0.015, 0.008))
    T.output(c, 0.85, 0.0, T.bump(T.math('SUBTRACT', T.noise(p, scale=200, detail=4), pits), strength=0.5, distance=0.0005))
    return m

def herb(name='M_Herb', col=(0.07, 0.07, 0.035)):
    """Dried herbs: olive to straw per part, darker in the bundle's crevices."""
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.001, 0.01)
    t = T.white(pid, out='Value')
    c = T.mix(t, col, (0.16, 0.12, 0.055))
    c = T.mix(T.noise(p, scale=60, detail=4), c, T.hsv(c, 0.5, 0.8, 0.6))
    c = T.mix(T.math('MULTIPLY', dirt, 0.6), c, (0.01, 0.01, 0.005))
    T.output(c, 0.75, 0.0, T.bump(T.noise(T.vmath('MULTIPLY', p, (1, 1, 6)), scale=150, detail=3), strength=0.3, distance=0.0003))
    return m

def twine(name='M_Twine', col=(0.22, 0.16, 0.09)):
    m, T = new_mat(name)
    p, pid, edge, dirt = _std_masks(T, 0.001, 0.005)
    lp = T.sep(T.attr('lp'))
    tw = T.math('SINE', T.math('MULTIPLY', T.math('ADD', T.math('MULTIPLY', T.math('ARCTAN2', (lp, 'Y'), (lp, 'X')), 0.01), (lp, 'Z')), 1400.0))
    c = T.mix(T.math('MULTIPLY', T.math('ADD', tw, 1), 0.5), T.hsv(col, 0.5, 1, 0.6), col)
    c = T.mix(T.math('MULTIPLY', dirt, 0.6), c, (0.02, 0.015, 0.01))
    T.output(c, 0.9, 0.0, T.bump(tw, strength=0.5, distance=0.0004))
    return m

# --- used directly in the game scene (transparent; not baked)
def glass(name='M_Glass', tint=(0.10, 0.14, 0.10), rough=0.03, dirty=0.5, alpha=0.07, dust=0.6, residue=None, tide=None):
    """Period forest glass as a game would draw it: alpha-blended, specular, with a strong Fresnel edge that
    darkens toward the tint (thick glass seen edge-on), smudges and fingerprints in roughness and alpha,
    dust on the up-facing shoulders, and an optional dried residue film below object z=residue[1]
    (residue=(colour, z)) so empty vessels still read. Rims/bases should be thick geometry (see props)."""
    m, T = new_mat(name)
    tc = T.node('ShaderNodeTexCoord'); po = (tc, 'Object'); pz = T.sep(po)
    smudge = T.math('MULTIPLY', T.smoothstep(T.noise(po, scale=12, detail=6), 0.5, 0.7), dirty)
    prints = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SINE', T.math('MULTIPLY', T.vmath('LENGTH', T.vmath('SUBTRACT', T.vmath('FRACTION', T.vmath('MULTIPLY', po, (55.0, 55.0, 40.0))), (0.5, 0.5, 0.5))), 160.0))), 0.7, 0.95),
                    T.smoothstep(T.noise(po, scale=18, detail=3), 0.55, 0.62))
    nz = T.sep(T.geo('Normal'))
    dm = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep((nz, 'Z'), 0.25, 0.8), T.smoothstep(T.noise(po, scale=60, detail=5), 0.35, 0.6)), dust)
    if os.environ.get('AAA_GLASS') == '13':   # g13: dust as fine specks plus a faint film, not a cloudy blotch over the dome (g12 reviewers)
        spk = T.smoothstep(T.voronoi(po, scale=320.0, out='Distance'), 0.2, 0.06)
        spk = T.math('MULTIPLY', spk, T.smoothstep(T.white(T.vmath('FLOOR', T.vmath('MULTIPLY', po, 320.0)), dim='3D'), 0.45, 0.5))
        dm = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep((nz, 'Z'), 0.2, 0.7), T.math('ADD', T.math('MULTIPLY', spk, 0.9), T.math('MULTIPLY', T.smoothstep(T.noise(po, scale=60, detail=5), 0.45, 0.65), 0.25))), dust)
    if os.environ.get('AAA_GLASS') == '8':   # dirt in local patches (reviewers: dome-wide grime reads as stained plastic)
        dm = T.math('MULTIPLY', dm, T.smoothstep(T.noise(po, scale=9, detail=3), 0.55, 0.65))
    r = T.math('ADD', rough, T.math('MULTIPLY', smudge, 0.3)); r = T.math('ADD', r, T.math('MULTIPLY', prints, 0.25))
    r = T.mixf(dm, r, 0.85)
    G7 = os.environ.get('AAA_GLASS') in ('7', '8', '9', '10', '11', '12', '13', '14', '15'); G8 = os.environ.get('AAA_GLASS') in ('8', '9', '10', '11', '12', '13', '14', '15')   # v7: clearer (half the milky veil), crisper edge-on darkening
    a_ = T.math('ADD', alpha * (0.6 if G7 else 1.0), T.math('MULTIPLY', smudge, 0.06 if G7 else 0.12)); a_ = T.math('ADD', a_, T.math('MULTIPLY', prints, 0.06))
    fr = T.node('ShaderNodeLayerWeight'); fr.inputs['Blend'].default_value = 0.45
    edge = T.math('POWER', (fr, 'Facing'), 2.4 if G7 else 1.6)
    a_ = T.math('ADD', a_, T.math('MULTIPLY', edge, 0.6))
    col = T.mix(edge, tint, T.hsv(tint, 0.5, 1.2, 0.45))
    col = T.mix(T.math('MULTIPLY', dm, 0.8), col, (0.30, 0.28, 0.24)); a_ = T.math('ADD', a_, T.math('MULTIPLY', dm, 0.5))
    if residue is not None:
        rc, rz = residue
        film = T.math('MULTIPLY', T.smoothstep((pz, 'Z'), rz + 0.004, rz - 0.002), T.maprange(T.noise(po, scale=25, detail=4), 0.3, 0.7, 0.35, 1.0))
        tide = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', (pz, 'Z'), rz)), 0.0025, 0.0005), 0.9)
        film = T.math('MAXIMUM', film, tide)
        col = T.mix(film, col, rc); a_ = T.math('ADD', a_, T.math('MULTIPLY', film, 0.45)); r = T.mixf(film, r, 0.6)
    if tide is not None:   # dried tide line a few mm above the current fill (evaporation), and a ragged faint ring a little higher
        tc_, tz = tide
        t1 = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', T.math('ADD', (pz, 'Z'), T.math('MULTIPLY', T.math('SUBTRACT', T.noise(po, scale=18, detail=3), 0.5), 0.003)), tz)), 0.0014, 0.0003), T.smoothstep(T.noise(po, scale=45, detail=5), 0.38, 0.62))
        t2 = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', (pz, 'Z'), tz + 0.007)), 0.0012, 0.0003), T.smoothstep(T.noise(po, scale=30, detail=4), 0.45, 0.6))
        tl = T.math('MAXIMUM', t1, T.math('MULTIPLY', t2, 0.6))
        col = T.mix(T.math('MULTIPLY', tl, 0.8), col, tc_); a_ = T.math('ADD', a_, T.math('MULTIPLY', tl, 0.28)); r = T.mixf(tl, r, 0.6)
    a_ = T.math('MINIMUM', a_, 1.0)
    if os.environ.get('AAA_GLASS') in ('8', '9', '10', '11', '12', '13', '14', '15'):
        # fire glint on the silhouette: a game glass shader's Fresnel rim term (the hearth seen at grazing angles)
        side_ = T.smoothstep(T.vmath('DOT_PRODUCT', T.geo('Normal'), (0.8, -0.5, 0.25)), 0.05, 0.6)   # only edges that face the fire
        glint = T.math('MULTIPLY', T.math('MULTIPLY', T.math('POWER', (fr, 'Facing'), 5.0), side_), 0.7)
        ecol, estr = (1.0, 0.55, 0.22), glint
        if os.environ.get('AAA_GLASS') in ('11', '12', '13', '14', '15'):
            # g11: fake reflection probe, as a URP glass shader samples a cubemap. Blended glass in Eevee reflects
            # nothing but point lights (one candle sparkle), so: reflect the view ray off the surface and look up a
            # procedural room with a crisp hearth patch (warm), a dim window (cool) and a soft ceiling; Schlick Fresnel.
            gm_ = T.node('ShaderNodeNewGeometry')
            vin = T.vmath('SCALE', (gm_, 'Incoming')); vin[0].inputs['Scale'].default_value = -1.0
            rfl = T.node('ShaderNodeVectorMath', operation='REFLECT'); T.link(vin, (rfl, 0)); T.link((gm_, 'Normal'), (rfl, 1))
            Rv = (rfl, 0)
            hearth = T.smoothstep(T.vmath('DOT_PRODUCT', Rv, (0.86, -0.5, 0.08)), 0.962, 0.972)
            window = T.smoothstep(T.vmath('DOT_PRODUCT', Rv, (-0.55, 0.55, 0.63)), 0.972, 0.982)
            ceil_ = T.smoothstep((T.sep(Rv), 'Z'), 0.5, 0.95)
            schl = T.math('ADD', 0.04, T.math('MULTIPLY', T.math('POWER', (fr, 'Facing'), 5.0), 0.96))
            env = T.mix(T.math('MULTIPLY', window, 0.5), T.mix(hearth, (0.0, 0.0, 0.0), (1.0, 0.5, 0.2)), (0.55, 0.62, 0.7), 'ADD')
            env = T.mix(T.math('MULTIPLY', ceil_, 0.08), env, (0.5, 0.4, 0.3), 'ADD')
            rs_ = T.math('MULTIPLY', T.math('ADD', T.math('MULTIPLY', hearth, 9.0), T.math('ADD', T.math('MULTIPLY', window, 2.5), T.math('MULTIPLY', ceil_, 0.4))), schl)
            spec_k = T.math('MULTIPLY', rs_, T.math('SUBTRACT', 1.0, T.math('MULTIPLY', dm, 0.8)))   # dust kills the reflection
            ecol = T.mix(T.math('DIVIDE', spec_k, T.math('ADD', T.math('ADD', spec_k, glint), 0.0001)), (1.0, 0.55, 0.22), env)
            estr = T.math('ADD', glint, spec_k)
            a_ = T.math('MINIMUM', T.math('ADD', a_, T.math('MULTIPLY', spec_k, 0.6)), 1.0)
        T.output(col, r, 0.0, T.bump(T.noise(po, scale=40, detail=3), strength=0.05, distance=0.0004), Alpha=T.math('MINIMUM', T.math('ADD', a_, T.math('MULTIPLY', glint, 0.5)), 1.0),
                 Specular_IOR_Level=1.0, Emission_Color=ecol, Emission_Strength=estr)
    else:
        T.output(col, r, 0.0, T.bump(T.noise(po, scale=40, detail=3), strength=0.05, distance=0.0004), Alpha=a_, Specular_IOR_Level=1.0)
    m.surface_render_method = 'BLENDED'
    if os.environ.get('AAA_GLASSFIX'): m.use_transparency_overlap = False   # overlap on mis-sorts and cuts dark tongues into the liquid; but off lost the A/B (liquid read opaque)
    return m

def liquid(name='M_Potion', col=(0.25, 0.8, 0.2), alpha=0.93):
    """Tinted liquid as a game draws it: alpha-blended, glossy, darker with depth below the surface,
    faint suspended murk, slightly emissive in its own colour."""
    m, T = new_mat(name)
    tc = T.node('ShaderNodeTexCoord'); zo = T.sep((tc, 'Generated'))
    depth = T.maprange((zo, 'Z'), 0.0, 1.0, 0.35, 1.0)
    murk = T.maprange(T.noise((tc, 'Object'), scale=30, detail=3), 0.3, 0.7, 0.85, 1.1)
    cc = T.mix(1.0, col, T.comb(depth, depth, depth), 'MULTIPLY')
    cc = T.mix(1.0, cc, T.comb(murk, murk, murk), 'MULTIPLY')
    T.output(cc, 0.05, 0.0, Alpha=alpha, Emission_Color=cc, Emission_Strength=0.06)
    m.surface_render_method = 'BLENDED'
    if os.environ.get('AAA_GLASSFIX'): m.use_transparency_overlap = False
    return m

def _refractive(m, thick='SLAB', depth=None):
    """Eevee screen-space refraction: the stand-in for a URP glass shader that samples the camera opaque
    texture with a normal-offset UV. Only opaque surfaces show through (same limit as the engine)."""
    m.surface_render_method = 'DITHERED'
    m.use_backface_culling = True   # else the far wall's back faces block the trace and hide the liquid
    if hasattr(m, 'use_raytrace_refraction'): m.use_raytrace_refraction = True
    if hasattr(m, 'thickness_mode'): m.thickness_mode = thick
    if depth is not None:   # wall thickness in metres: blown glass is a thin slab, so the view barely shifts
        out = next(n for n in m.node_tree.nodes if n.bl_idname == 'ShaderNodeOutputMaterial')
        if 'Thickness' in out.inputs: out.inputs['Thickness'].default_value = depth

def glass2(name='M_Glass2', tint=(0.62, 0.80, 0.55), rough=0.02, dirty=0.5, dust=0.12, residue=None):
    """Forest glass drawn the way a URP glass shader draws it: what is behind is MULTIPLIED by the glass tint
    (Transparent BSDF = the opaque-texture sample times tint, no milky alpha lerp), plus Fresnel reflection
    (Principled, specular only). Smudges, prints, dust and residue raise the reflective/opaque share.
    Eevee's screen-traced refraction fails inside spheres (the liquid vanishes; Cycles shows it fine),
    so refraction offset is left to the engine shader."""
    m, T = new_mat(name)
    tc = T.node('ShaderNodeTexCoord'); po = (tc, 'Object'); pz = T.sep(po)
    smudge = T.math('MULTIPLY', T.smoothstep(T.noise(po, scale=12, detail=6), 0.5, 0.7), dirty)
    prints = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SINE', T.math('MULTIPLY', T.vmath('LENGTH', T.vmath('SUBTRACT', T.vmath('FRACTION', T.vmath('MULTIPLY', po, (55.0, 55.0, 40.0))), (0.5, 0.5, 0.5))), 160.0))), 0.7, 0.95),
                    T.smoothstep(T.noise(po, scale=18, detail=3), 0.55, 0.62))
    nz = T.sep(T.geo('Normal'))
    dm = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep((nz, 'Z'), 0.25, 0.8), T.smoothstep(T.noise(po, scale=60, detail=5), 0.35, 0.6)), dust)
    r = T.math('ADD', rough, T.math('MULTIPLY', smudge, 0.12)); r = T.math('ADD', r, T.math('MULTIPLY', prints, 0.15))
    r = T.mixf(dm, r, 0.85)
    fr = T.node('ShaderNodeFresnel'); fr.inputs['IOR'].default_value = 1.5
    # thick glass seen edge-on: the tint deepens toward the silhouette (longer path through the wall)
    lw = T.node('ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = 0.5
    deep = T.hsv(tint, 0.5, 1.6, 0.55)
    tcol = T.mix(T.math('POWER', (lw, 'Facing'), 4.0), tint, deep)
    cover = T.math('ADD', T.math('MULTIPLY', dm, 0.7), T.math('MULTIPLY', smudge, 0.08))
    col = T.mix(T.math('MULTIPLY', dm, 0.9), (0.02, 0.022, 0.02), (0.14, 0.12, 0.1))
    if residue is not None:
        rc, rz = residue
        film = T.math('MULTIPLY', T.smoothstep((pz, 'Z'), rz + 0.004, rz - 0.002), T.maprange(T.noise(po, scale=25, detail=4), 0.3, 0.7, 0.35, 1.0))
        tide = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', (pz, 'Z'), rz)), 0.0025, 0.0005), 0.9)
        film = T.math('MAXIMUM', film, tide)
        col = T.mix(film, col, rc); cover = T.math('ADD', cover, T.math('MULTIPLY', film, 0.5)); r = T.mixf(film, r, 0.6)
    bs = T.output(col, r, 0.0, T.bump(T.noise(po, scale=40, detail=3), strength=0.05, distance=0.0004), Alpha=1.0, Specular_IOR_Level=1.0)
    tr = T.node('ShaderNodeBsdfTransparent'); T.set(tr, 'Color', tcol)
    mx = T.node('ShaderNodeMixShader')
    T.set(mx, 'Fac', T.math('MINIMUM', T.math('ADD', (fr, 'Fac'), cover), 1.0))
    T.link((tr, 'BSDF'), (mx, 1)); T.link((bs, 'BSDF'), (mx, 2))
    out = next(n for n in m.node_tree.nodes if n.bl_idname == 'ShaderNodeOutputMaterial')
    T.link((mx, 'Shader'), (out, 'Surface'))
    m.surface_render_method = 'BLENDED'; m.use_backface_culling = False
    m.use_transparency_overlap = False   # with overlap on, the vessel's own faces mis-sort and cut bands out of the liquid
    return m

def liquid2(name='M_Potion2', col=(0.25, 0.8, 0.2), dens=1.0):
    """Opaque shaded liquid inside the glass (opaque so the glass layer can sit over it, as in-engine).
    Beer-Lambert read: the thick core (facing the eye) is the darkest, most saturated colour; the thin
    edges go lighter and warmer where light passes through; glossy but low specular (the glass carries
    the reflections); faint suspended murk; a weak emissive glow so it reads in a dark room."""
    m, T = new_mat(name)
    tc = T.node('ShaderNodeTexCoord')
    murk = T.maprange(T.noise((tc, 'Object'), scale=30, detail=3), 0.3, 0.7, 0.8, 1.05)
    fr = T.node('ShaderNodeLayerWeight'); fr.inputs['Blend'].default_value = 0.5
    edge = T.math('POWER', (fr, 'Facing'), 1.3)                       # 0 core .. 1 silhouette
    core = tuple(v * 0.55 for v in col)
    thin = tuple(min(1.0, v * 3.2 + 0.03) for v in col)
    cc = T.mix(edge, core, thin)
    zg = T.sep((tc, 'Generated'))
    depth = T.maprange((zg, 'Z'), 0.0, 1.0, 0.7, 1.1)
    k = T.math('MULTIPLY', murk, depth)
    cc = T.mix(1.0, cc, T.comb(k, k, k), 'MULTIPLY')
    T.output(cc, 0.05, 0.0, Alpha=1.0, Emission_Color=thin, Emission_Strength=T.math('MULTIPLY', edge, 0.6 * dens), Specular_IOR_Level=0.3)
    return m

def glass3(name='M_Glass3', tint=(0.10, 0.14, 0.10), rough=0.015, dirty=0.5, alpha=0.04, dust=0.6, residue=None):
    """Round-3 glass: the old alpha glass (it won two A/Bs) with less milky haze (smudges mostly in roughness,
    a thinner Fresnel veil), dirt as fine specks plus a little blotch instead of soft blotches, a crisper
    specular (lower base roughness), and the sort fix (overlap off, back faces culled so only the near wall
    draws over the liquid)."""
    m, T = new_mat(name)
    tc = T.node('ShaderNodeTexCoord'); po = (tc, 'Object'); pz = T.sep(po)
    smudge = T.math('MULTIPLY', T.smoothstep(T.noise(po, scale=12, detail=6), 0.5, 0.7), dirty)
    prints = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SINE', T.math('MULTIPLY', T.vmath('LENGTH', T.vmath('SUBTRACT', T.vmath('FRACTION', T.vmath('MULTIPLY', po, (55.0, 55.0, 40.0))), (0.5, 0.5, 0.5))), 160.0))), 0.7, 0.95),
                    T.smoothstep(T.noise(po, scale=18, detail=3), 0.55, 0.62))
    nz = T.sep(T.geo('Normal'))
    up = T.smoothstep((nz, 'Z'), 0.1, 0.8)
    specks = T.smoothstep(T.noise(po, scale=420, detail=2), 0.66, 0.70)          # grit and dried drops
    blotch = T.math('MULTIPLY', T.smoothstep(T.noise(po, scale=60, detail=5), 0.45, 0.65), 0.35)
    dm = T.math('MULTIPLY', T.math('MULTIPLY', up, T.math('MAXIMUM', specks, blotch)), dust)
    r = T.math('ADD', rough, T.math('MULTIPLY', smudge, 0.3)); r = T.math('ADD', r, T.math('MULTIPLY', prints, 0.25))
    r = T.mixf(dm, r, 0.85)
    a_ = T.math('ADD', alpha, T.math('MULTIPLY', smudge, 0.04)); a_ = T.math('ADD', a_, T.math('MULTIPLY', prints, 0.04))
    fr = T.node('ShaderNodeLayerWeight'); fr.inputs['Blend'].default_value = 0.45
    edge = T.math('POWER', (fr, 'Facing'), 2.2)
    a_ = T.math('ADD', a_, T.math('MULTIPLY', edge, 0.45))
    col = T.mix(edge, tint, T.hsv(tint, 0.5, 1.3, 0.35))
    col = T.mix(T.math('MULTIPLY', dm, 0.9), col, (0.30, 0.28, 0.24)); a_ = T.math('ADD', a_, T.math('MULTIPLY', dm, 0.7))
    if residue is not None:
        rc, rz = residue
        film = T.math('MULTIPLY', T.smoothstep((pz, 'Z'), rz + 0.004, rz - 0.002), T.maprange(T.noise(po, scale=25, detail=4), 0.3, 0.7, 0.35, 1.0))
        tide = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', (pz, 'Z'), rz)), 0.0025, 0.0005), 0.9)
        film = T.math('MAXIMUM', film, tide)
        col = T.mix(film, col, rc); a_ = T.math('ADD', a_, T.math('MULTIPLY', film, 0.45)); r = T.mixf(film, r, 0.6)
    a_ = T.math('MINIMUM', a_, 1.0)
    T.output(col, r, 0.0, T.bump(T.noise(po, scale=40, detail=3), strength=0.05, distance=0.0004), Alpha=a_, Specular_IOR_Level=1.0)
    m.surface_render_method = 'BLENDED'; m.use_transparency_overlap = False; m.use_backface_culling = True
    return m

def liquid3(name='M_Potion3', col=(0.25, 0.8, 0.2), alpha=0.42):
    """Round-3 liquid: genuinely see-through (every opaque liquid lost the A/B). Alpha `alpha` at the core,
    rising toward the silhouette (longer path through the liquid at the edge of a round volume); colour deepens
    and saturates with depth below the surface; glossy; faint murk; weak emissive glow in its own colour."""
    m, T = new_mat(name)
    tc = T.node('ShaderNodeTexCoord'); zo = T.sep((tc, 'Generated'))
    depth = T.maprange((zo, 'Z'), 0.0, 1.0, 0.45, 1.0)
    murk = T.maprange(T.noise((tc, 'Object'), scale=30, detail=3), 0.3, 0.7, 0.85, 1.1)
    deep = T.hsv(col, 0.5, 1.25, 0.8)
    cc = T.mix(T.math('SUBTRACT', 1.0, depth), col, deep)
    cc = T.mix(1.0, cc, T.comb(murk, murk, murk), 'MULTIPLY')
    fr = T.node('ShaderNodeLayerWeight'); fr.inputs['Blend'].default_value = 0.5
    a_ = T.math('MINIMUM', T.math('ADD', alpha, T.math('MULTIPLY', T.math('POWER', (fr, 'Facing'), 1.5), 0.35)), 0.97)
    a_ = T.math('MINIMUM', T.math('ADD', a_, T.math('MULTIPLY', T.math('SUBTRACT', 1.0, depth), 0.25)), 0.97)
    # no diffuse: a liquid is tinted transmission (Transparent BSDF multiplies what is behind) plus a glossy
    # surface and a weak glow in its own colour; the share of the surface layer rises toward the silhouette
    mx_ = max(col); tint = tuple(min(1.0, (v / mx_) ** 1.6 * 0.95) for v in col)
    bs = T.output((0.0, 0.0, 0.0), 0.04, 0.0, Alpha=1.0, Emission_Color=cc, Emission_Strength=0.9, Specular_IOR_Level=0.7)
    tr = T.node('ShaderNodeBsdfTransparent'); T.set(tr, 'Color', T.mix(T.math('SUBTRACT', 1.0, depth), tint, T.hsv(tint, 0.5, 1.2, 0.7)))
    mxs = T.node('ShaderNodeMixShader'); T.set(mxs, 'Fac', a_)
    T.link((tr, 'BSDF'), (mxs, 1)); T.link((bs, 'BSDF'), (mxs, 2))
    out = next(n for n in m.node_tree.nodes if n.bl_idname == 'ShaderNodeOutputMaterial')
    T.link((mxs, 'Shader'), (out, 'Surface'))
    m.surface_render_method = 'BLENDED'; m.use_transparency_overlap = True   # draw the far side too: its highlights seen through the near side are the see-through cue
    return m

def liquid4(name='M_Potion4', col=(0.25, 0.8, 0.2), alpha=0.5, rim_dark=0.0, glow=0.8, lift=2.5):
    """Round-4 liquid: fake refraction, the way engines do it with a cubemap. The view ray is refracted
    (IOR 1.33) by the liquid's normal and looks up a procedural 'room' (brighter above, a warm lobe toward
    the fire side, mid-scale clutter noise). Curved vessels therefore show a bent, inverted, varying interior
    (the cue every reviewer read as depth) instead of a flat fill. A soft bright meniscus band replaces the
    hard top line. Tinted Transparent BSDF + glossy emissive surface, no diffuse (round 3 lesson)."""
    m, T = new_mat(name)
    tc = T.node('ShaderNodeTexCoord'); zo = T.sep((tc, 'Generated'))
    geo = T.node('ShaderNodeNewGeometry')
    inc = T.vmath('SCALE', (geo, 'Incoming')); inc[0].inputs['Scale'].default_value = -1.0
    rf = T.node('ShaderNodeVectorMath', operation='REFRACT')
    T.link(inc, (rf, 0)); T.link((geo, 'Normal'), (rf, 1)); rf.inputs['Scale'].default_value = 1.0 / 1.33
    R = (rf, 0); Rs = T.sep(R)
    G10 = os.environ.get('AAA_GLASS') == '10'   # g10: high-contrast refracted interior + crisp meniscus (t7 stand: 'flat emissive green fill', 'no refraction, no meniscus')
    room = T.maprange((Rs, 'Z'), -0.6, 0.8, 0.06 if G10 else 0.25, 1.0)                                   # floor dark, ceiling glow
    clutter = T.maprange(T.noise(T.vmath('MULTIPLY', R, (3.0, 3.0, 3.0)), scale=1.5, detail=3), 0.3, 0.7, 0.25 if G10 else 0.45, 1.4 if G10 else 1.25)
    fire = T.math('MULTIPLY', T.smoothstep(T.vmath('DOT_PRODUCT', R, (0.75, -0.45, 0.2)), 0.7 if G10 else 0.55, 0.95), 2.6 if G10 else 1.6)
    p = T.math('ADD', T.math('MULTIPLY', room, clutter), fire)
    depth = T.maprange((zo, 'Z'), 0.0, 1.0, 0.5, 1.0)
    band = T.math('MULTIPLY', T.smoothstep((zo, 'Z'), 0.86, 0.985), 0.9)               # meniscus: light caught at the surface
    if os.environ.get('AAA_GLASS') in ('12', '13', '14', '15'):   # g12: a bright line along the climbing meniscus lip (light caught in the curved edge)
        band = T.math('ADD', T.math('MULTIPLY', band, 0.5), T.math('MULTIPLY', T.smoothstep((zo, 'Z'), 0.982, 0.997), 0.8))
    if G10:   # a thin bright meniscus line over a thin dark line (total internal reflection under the surface)
        band = T.math('MULTIPLY', T.smoothstep((zo, 'Z'), 0.955, 0.985), 1.6)
        under = T.math('MULTIPLY', T.smoothstep((zo, 'Z'), 0.9, 0.94), T.smoothstep((zo, 'Z'), 0.965, 0.95))
        p = T.math('MULTIPLY', p, T.math('SUBTRACT', 1.0, T.math('MULTIPLY', under, 0.75)))
    lum = T.math('ADD', T.math('MULTIPLY', p, depth), band)
    if G10:   # the free surface seen from above reflects the room (bright), it does not refract down to the dark floor (that read as a donut)
        topf = T.smoothstep((T.sep((geo, 'Normal')), 'Z'), 0.55, 0.9)
        lum = T.mix(topf, lum, 0.7)
    if rim_dark:   # thick-edge absorption: the long path at the silhouette goes darker and more saturated
        fe = T.node('ShaderNodeLayerWeight'); fe.inputs['Blend'].default_value = 0.5
        lum = T.math('MULTIPLY', lum, T.math('SUBTRACT', 1.0, T.math('MULTIPLY', T.math('POWER', (fe, 'Facing'), 1.2), rim_dark)))
    lit = tuple(min(1.0, v * lift) for v in col)
    cc = T.mix(1.0, lit, T.comb(lum, lum, lum), 'MULTIPLY')
    mx_ = max(col); tint = tuple(min(1.0, (v / mx_) ** 1.6 * 0.95) for v in col)
    fr = T.node('ShaderNodeLayerWeight'); fr.inputs['Blend'].default_value = 0.5
    a_ = T.math('MINIMUM', T.math('ADD', alpha, T.math('MULTIPLY', T.math('POWER', (fr, 'Facing'), 1.5), 0.3)), 0.95)
    a_ = T.math('MINIMUM', T.math('ADD', a_, T.math('MULTIPLY', band, 0.2)), 0.97)
    bs = T.output((0.0, 0.0, 0.0), 0.04, 0.0, Alpha=1.0, Emission_Color=cc, Emission_Strength=glow, Specular_IOR_Level=0.7)
    tr = T.node('ShaderNodeBsdfTransparent'); T.set(tr, 'Color', T.mix(T.math('SUBTRACT', 1.0, depth), tint, T.hsv(tint, 0.5, 1.2, 0.6)))
    mxs = T.node('ShaderNodeMixShader'); T.set(mxs, 'Fac', a_)
    T.link((tr, 'BSDF'), (mxs, 1)); T.link((bs, 'BSDF'), (mxs, 2))
    out = next(n for n in m.node_tree.nodes if n.bl_idname == 'ShaderNodeOutputMaterial')
    T.link((mxs, 'Shader'), (out, 'Surface'))
    m.surface_render_method = 'BLENDED'
    return m
