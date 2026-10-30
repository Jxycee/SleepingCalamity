"""sc_aaa_mats - procedural 'smart materials' for sc_aaa_kit (Cycles; baked to PBR maps).

Every material reads the part attributes written by sc_aaa_kit.tag():
  lp  (vector) part-local coords in metres, long axis = X (grain direction)
  pid (float)  part id: per-part variation
and derives wear masks from the Bevel node (convex edges) and a local AO node (cavities).
"""
from sc_aaa_kit import new_mat, use_scan

def _part_space(T, stretch=(1, 1, 1), jitter=(7.0, 3.0, 3.0)):
    """lp offset per part so no two boards share a pattern."""
    lp = T.attr('lp'); pid = T.attr('pid', vec=False)
    w = T.node('ShaderNodeTexWhiteNoise', noise_dimensions='1D'); T.set(w, 'W', pid)
    off = T.vmath('MULTIPLY', (w, 'Color'), jitter)
    p = T.vmath('ADD', lp, off)
    return T.vmath('MULTIPLY', p, stretch), p, pid

def oak(name='M_Oak', age=0.55, grime=1.0, stains=1.0, tone=1.0, decals=False):
    """Aged, hand-worn dark oak. Reads lp/pid; edges from Bevel node + pointiness (dense highs)."""
    m, T = new_mat(name)
    _, p, pid = _part_space(T, jitter=(7.0, 0.12, 0.08))
    use_scan(T, 'rough_wood', p, 0.8, col=0.3, rough=0.35, height=1.2)
    s = T.sep(p)
    # flat-sawn rings: log axis tilted against the board so rings cross faces as arcs/cathedrals
    dn = T.noise(T.vmath('MULTIPLY', p, (0.5, 2.5, 2.5)), scale=1.3, detail=3)
    yy = T.math('ADD', T.math('ADD', (s, 'Y'), T.math('MULTIPLY', (s, 'X'), 0.05)), T.math('MULTIPLY', dn, 0.06))
    zz = T.math('ADD', T.math('ADD', (s, 'Z'), T.math('MULTIPLY', (s, 'X'), 0.045)), T.math('ADD', 0.07, T.math('MULTIPLY', dn, 0.05)))
    rad = T.vmath('LENGTH', T.comb(0.0, T.math('ADD', yy, 0.02), zz))
    ringsn = T.noise(T.vmath('MULTIPLY', p, (2, 12, 12)), scale=1.0, detail=3)
    rings = T.math('FRACT', T.math('ADD', T.math('MULTIPLY', rad, 110.0), T.math('MULTIPLY', ringsn, 1.6)))
    late = T.smoothstep(rings, 0.55, 0.92)
    streak = T.noise(T.vmath('MULTIPLY', p, (1.5, 45, 45)), scale=1.0, detail=4, rough=0.6)
    ck = T.noise(T.vmath('MULTIPLY', p, (0.4, 30, 30)), scale=1.0, detail=2)
    ck = T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', ck, 0.5)), 0.01, 0.0)
    # pores: short dark dashes along grain
    pores = T.noise(T.vmath('MULTIPLY', p, (60, 700, 700)), scale=1.0, detail=1, rough=0.4)
    pores = T.smoothstep(pores, 0.62, 0.72)
    fleck = T.noise(T.vmath('MULTIPLY', p, (25, 120, 120)), scale=1.0, detail=2)
    fleck = T.smoothstep(fleck, 0.68, 0.74)
    drift = T.noise(T.vmath('MULTIPLY', p, (1.2, 4, 4)), scale=1.0, detail=3)
    pt = T.smoothstep(T.geo('Pointiness'), 0.5, 0.56)
    edge = T.math('MAXIMUM', T.edge(radius=0.005, lo=0.03, hi=0.2), pt)
    edge = T.math('MULTIPLY', edge, T.smoothstep(T.noise(p, scale=30, detail=3), 0.35, 0.6))
    cav = T.cavity(dist=0.03, lo=0.3, hi=0.95)
    crev = T.smoothstep(T.geo('Pointiness'), 0.49, 0.44)          # concave dents/chips
    up = T.up(0.6, 0.95)
    lnx = T.sep(T.attr('ln'))
    endg = T.smoothstep(T.math('ABSOLUTE', (lnx, 'X')), 0.55, 0.8)
    ang = T.math('ARCTAN2', zz, T.math('ADD', yy, 0.02))
    aa = T.math('ADD', T.math('MULTIPLY', ang, 2.5), T.math('MULTIPLY', T.noise(T.vmath('MULTIPLY', p, (1, 25, 25)), scale=1.0, detail=2), 0.25))
    rck = T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', T.math('FRACT', aa), 0.5)), 0.035, 0.0)
    sparse = T.smoothstep(T.white(T.math('ADD', T.math('FLOOR', aa), T.math('MULTIPLY', pid, 13.0)), out='Value'), 0.55, 0.6)
    rck = T.math('MULTIPLY', T.math('MULTIPLY', rck, sparse), T.smoothstep(rad, 0.02, 0.06))
    rck = T.math('MULTIPLY', rck, endg)
    ck = T.math('MULTIPLY', ck, T.math('SUBTRACT', 1.0, endg))
    ck = T.math('MAXIMUM', ck, rck)
    wz = T.sep(T.geo('Position'))
    low_band = T.smoothstep((wz, 'Z'), 0.5, 0.05)
    dust = T.math('MULTIPLY', T.math('MULTIPLY', up, T.smoothstep(T.noise(p, scale=9.0, detail=4), 0.4, 0.65)), T.math('ADD', 0.25, T.math('MULTIPLY', low_band, 0.75)))
    light = (0.17 * tone, 0.085 * tone, 0.032 * tone); dark = (0.045 * tone, 0.021 * tone, 0.008 * tone)
    grey = (0.075, 0.06, 0.045)
    c = T.mix(late, light, dark)
    c = T.mix(T.maprange(streak, 0.3, 0.7, 0.0, 0.7), c, (0.07, 0.034, 0.013))
    c = T.mix(T.math('MULTIPLY', pores, 0.4), c, (0.025, 0.014, 0.008))
    tj = T.white(pid, out='Value')
    c = T.mix(T.maprange(tj, 0, 1, 0, 0.45), c, (0.03, 0.016, 0.007))
    c = T.mix(endg, c, T.hsv(c, 0.5, 0.9, 0.55))
    c = T.mix(T.math('MULTIPLY', fleck, 0.3), c, (0.22, 0.13, 0.07))
    c = T.mix(T.maprange(drift, 0.3, 0.7, 0.0, 0.6), c, (0.05, 0.028, 0.015))
    c = T.mix(age * 0.5, c, grey)
    st = T.noise(T.vmath('MULTIPLY', p, (1.5, 1.5, 1.5)), scale=2.2, detail=6, rough=0.65)
    st = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(st, 0.56, 0.66), up), stains)
    c = T.mix(T.math('MULTIPLY', st, 0.7), c, (0.022, 0.013, 0.007))
    vr = T.voronoi(T.vmath('MULTIPLY', p, (1, 1, 0.0)), scale=4.0)
    ringmark = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', vr, 0.17)), 0.012, 0.0), up)
    ringmark = T.math('MULTIPLY', ringmark, T.smoothstep(T.noise(p, scale=3.0, detail=1), 0.5, 0.56))
    ringmark = T.math('MULTIPLY', ringmark, stains)
    c = T.mix(T.math('MULTIPLY', ringmark, 0.6), c, (0.018, 0.01, 0.006))
    c = T.mix(T.math('MULTIPLY', edge, 0.5), c, (0.22, 0.12, 0.05))
    dirt = T.math('MAXIMUM', T.math('SUBTRACT', 1.0, cav), crev)
    c = T.mix(T.math('MULTIPLY', dirt, 0.85 * grime), c, (0.012, 0.009, 0.007))
    c = T.mix(T.math('MULTIPLY', ck, 0.8), c, (0.01, 0.007, 0.005))
    c = T.mix(T.math('MULTIPLY', dust, 0.55), c, (0.13, 0.11, 0.085))
    c = T.mix(T.math('MULTIPLY', low_band, 0.35), c, (0.03, 0.022, 0.016))
    if decals:
        wp = T.geo('Position'); wps = T.sep(wp)
        def disc(cx, cy, rad, soft=0.02, warp=0.04, sc=6.0):
            dxy = T.vmath('LENGTH', T.vmath('SUBTRACT', T.comb((wps, 'X'), (wps, 'Y'), 0.0), (cx, cy, 0.0)))
            dxy = T.math('ADD', dxy, T.math('MULTIPLY', T.math('SUBTRACT', T.noise(wp, scale=sc, detail=5), 0.5), warp))
            return T.math('MULTIPLY', T.smoothstep(dxy, rad, rad - soft), up)
        # scorch around the brazier slab: charred ring, darkest at the slab edge
        sc_ = T.math('MULTIPLY', disc(0.46, 0.04, 0.33, 0.1, 0.1, 4.0), T.smoothstep(T.noise(wp, scale=14, detail=6), 0.3, 0.55))
        c = T.mix(sc_, c, T.mix(T.noise(wp, scale=60, detail=4), (0.006, 0.005, 0.004), (0.03, 0.016, 0.008)))
        # candle wax splats: pale droplets
        wx = T.voronoi(T.vmath('MULTIPLY', wp, (1, 1, 0)), scale=90.0, out='Distance')
        wxm = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(wx, 0.22, 0.15), disc(-0.66, 0.2, 0.11, 0.05)), T.smoothstep(T.white(T.vmath('FLOOR', T.vmath('MULTIPLY', wp, (90, 90, 0))), dim='3D'), 0.55, 0.56))
        c = T.mix(wxm, c, (0.45, 0.35, 0.16))
        # vessel ring marks where the jug and flask usually stand
        def ringm(cx, cy, rad):
            return T.math('MULTIPLY', disc(cx, cy, rad + 0.004, 0.003, 0.008, 20.0), T.smoothstep(disc(cx, cy, rad - 0.004, 0.003, 0.008, 20.0), 1.0, 0.0))
        rings2 = T.math('MAXIMUM', T.math('MAXIMUM', ringm(-0.30, 0.12, 0.062), ringm(0.22, 0.24, 0.038)), ringm(-0.52, -0.2, 0.05))
        c = T.mix(T.math('MULTIPLY', rings2, 0.65), c, (0.012, 0.008, 0.005))
        # potion spill by the mortar: green-brown tide-marked stain
        sp = T.math('MAXIMUM', disc(0.10, -0.24, 0.1, 0.01, 0.06, 10.0), disc(0.16, -0.10, 0.065, 0.01, 0.05, 12.0))
        tide = T.math('MULTIPLY', disc(0.10, -0.24, 0.1, 0.004, 0.06, 10.0), T.smoothstep(disc(0.10, -0.24, 0.09, 0.004, 0.06, 10.0), 1.0, 0.0))
        tide = T.math('MAXIMUM', tide, T.math('MULTIPLY', disc(0.16, -0.10, 0.065, 0.004, 0.05, 12.0), T.smoothstep(disc(0.16, -0.10, 0.057, 0.004, 0.05, 12.0), 1.0, 0.0)))
        c = T.mix(T.math('MULTIPLY', sp, 0.6), c, (0.012, 0.013, 0.005))
        c = T.mix(T.math('MULTIPLY', tide, 0.6), c, (0.01, 0.011, 0.004))
        # herb powder dusting around the mortar
        pw = T.math('MULTIPLY', disc(-0.02, -0.14, 0.16, 0.08, 0.06, 8.0), T.smoothstep(T.noise(wp, scale=80, detail=4), 0.5, 0.7))
        c = T.mix(T.math('MULTIPLY', pw, 0.6), c, (0.12, 0.12, 0.07))
        # ink blot and drips near the book
        ink = T.math('MAXIMUM', disc(-0.27, -0.2, 0.012, 0.002, 0.01, 30.0), disc(-0.255, -0.215, 0.004, 0.001, 0.004, 40.0))
        c = T.mix(ink, c, (0.005, 0.004, 0.006))
        # contact grime: soft dark halos round every object's footprint (dirt that collects where things stand)
        halo = 0.0
        for (hx, hy, hr) in ((-0.02, -0.14, 0.085), (-0.22, 0.2, 0.068), (-0.40, 0.24, 0.056), (-0.66, 0.2, 0.062), (0.12, 0.17, 0.042), (-0.02, 0.22, 0.04), (0.06, -0.02, 0.016)):
            hm = T.math('MULTIPLY', disc(hx, hy, hr + 0.022, 0.02, 0.012, 25.0), T.smoothstep(disc(hx, hy, hr - 0.002, 0.004, 0.006, 25.0), 1.0, 0.0))
            halo = hm if halo == 0.0 else T.math('MAXIMUM', halo, hm)
        c = T.mix(T.math('MULTIPLY', halo, 0.55), c, (0.012, 0.008, 0.005))
        # hand-worn work zone at the front edge: darker, greasy sheen where forearms and hands rest
        hw = T.math('MULTIPLY', T.smoothstep((wps, 'Y'), -0.12, -0.3), T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', (wps, 'X'), -0.1)), 0.42, 0.2))
        hw = T.math('MULTIPLY', T.math('MULTIPLY', hw, up), T.maprange(T.noise(wp, scale=7, detail=4), 0.3, 0.7, 0.4, 1.0))
        c = T.mix(T.math('MULTIPLY', hw, 0.45), c, T.hsv(c, 0.5, 1.1, 0.6))
        # knife cuts on the left front of the top
        kc = T.noise(T.vmath('MULTIPLY', wp, (3.0, 40.0, 1.0)), scale=1.0, detail=0)
        kcm = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', kc, 0.5)), 0.006, 0.0), disc(-0.15, -0.22, 0.12, 0.04))
        c = T.mix(T.math('MULTIPLY', kcm, 0.7), c, (0.02, 0.012, 0.006))
    r = T.mixf(late, 0.58, 0.72)
    r = T.mixf(pores, r, 0.85)
    r = T.mixf(T.math('MULTIPLY', edge, 0.8), r, 0.35)
    r = T.mixf(st, r, 0.45)
    r = T.mixf(dirt, r, 0.9)
    r = T.mixf(dust, r, 0.95)
    r = T.mixf(endg, r, 0.85)
    scr = T.noise(T.vmath('MULTIPLY', p, (8, 260, 30)), scale=1.0, detail=1)
    scr = T.math('MULTIPLY', T.smoothstep(T.math('ABSOLUTE', T.math('SUBTRACT', scr, 0.5)), 0.012, 0.0), up)
    h = T.math('MULTIPLY', late, 1.0)
    h = T.math('SUBTRACT', h, T.math('MULTIPLY', ck, 1.5))
    h = T.math('SUBTRACT', h, T.math('MULTIPLY', pores, 0.4))
    h = T.math('SUBTRACT', h, T.math('MULTIPLY', scr, 0.8))
    if decals:
        r = T.mixf(sc_, r, 0.92); r = T.mixf(wxm, r, 0.3); r = T.mixf(sp, r, 0.35); r = T.mixf(pw, r, 0.95)
        r = T.mixf(halo, r, 0.85); r = T.mixf(T.math('MULTIPLY', hw, 0.8), r, 0.32)
        h = T.math('ADD', h, T.math('MULTIPLY', wxm, 1.5))
        h = T.math('SUBTRACT', h, T.math('MULTIPLY', kcm, 2.0))
        h = T.math('ADD', h, T.math('MULTIPLY', T.math('MULTIPLY', sc_, T.noise(wp, scale=120, detail=5)), 1.5))
    n = T.bump(h, strength=0.8, distance=0.0009)
    T.output(c, r, 0.0, n)
    return m

def iron(name='M_Iron', rust=0.5):
    """Hand-forged blackened iron: hammer facets, rust in crevices, polished high points."""
    m, T = new_mat(name)
    _, p, pid = _part_space(T)
    use_scan(T, 'rusty_metal_02', p, 3.0, col=0.3, rough=0.4, height=1.0)
    edge = T.edge(radius=0.005, lo=0.03, hi=0.2)
    cav = T.cavity(dist=0.015, lo=0.3, hi=0.95)
    blot = T.noise(T.vmath('MULTIPLY', p, (1, 1, 1)), scale=40.0, detail=6, rough=0.7)
    rmask = T.math('MULTIPLY', T.smoothstep(blot, 0.62 - 0.12 * rust, 0.7), 1.0)
    rmask = T.math('MAXIMUM', rmask, T.math('MULTIPLY', T.math('SUBTRACT', 1.0, cav), rust))
    rust_c = T.mix(T.noise(p, scale=90, detail=3), (0.16, 0.06, 0.025), (0.30, 0.13, 0.05))
    base = T.mix(T.noise(p, scale=25, detail=4), (0.075, 0.072, 0.068), (0.14, 0.13, 0.12))
    c = T.mix(T.math('MULTIPLY', edge, 0.8), base, (0.30, 0.29, 0.28))
    c = T.mix(rmask, c, rust_c)
    metal = T.mixf(rmask, 0.75, 0.0)
    r = T.mixf(T.math('MULTIPLY', edge, 0.8), 0.58, 0.32)
    r = T.mixf(rmask, r, 0.9)
    ham = T.voronoi(p, scale=180.0, out='Distance')
    h = T.math('ADD', T.math('MULTIPLY', ham, 0.4), T.math('MULTIPLY', rmask, 0.5))
    n = T.bump(h, strength=0.4, distance=0.0006)
    T.output(c, r, metal, n)
    return m
