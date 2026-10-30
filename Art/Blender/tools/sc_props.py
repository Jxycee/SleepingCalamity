"""[Ported from FishGame fg-aaa-prop, M8. Example potion-stand props: use as a pattern for prop functions, not as SC content.] Potion-stand props. Each prop: parts -> sculpt-like highs -> decimated low -> own baked PBR set,
saved to <out>/props/<name>.blend (low only; glassware unbaked).
Run: blender -b --factory-startup --python props.py -- --out <dir> [--only mortar,jug] [--res 2048]
All props are authored with their base at z=0, origin at the base centre, front = -Y, metres.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector, Matrix
import sc_aaa_kit as K, sc_aaa_mats2 as M2

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); PDIR = os.path.join(OUT, 'props'); os.makedirs(PDIR, exist_ok=True)
RES = int(arg('--res', '2048')); ONLY = arg('--only', '').split(',') if arg('--only') else None
PID = [0]
def pid():
    PID[0] += 1; return PID[0] * 0.173

wear = K.wear_generic

def solid_lathe(name, outer, inner=None, segs=48, step=0.004):
    """Closed revolved solid: outer profile bottom->top, inner profile top->bottom (a vessel wall).
    The profile is Catmull-Rom resampled (step m) so turned forms stay round; (r, z, 'c') = hard corner."""
    prof = list(outer) + (list(inner) if inner else [])
    if step: prof = K.smooth_profile(prof, step, closed=True)
    return K.lathe(name, prof, segs, close=True)

def tube_path(name, pts, r, sides=12):
    return K.tube(name, pts, r, sides)

def finish(name, highs, target, res=None, keep=()):
    low = K.bake_prop(name, highs, PDIR, target=target, res=res or RES)
    K.save_only(os.path.join(PDIR, name + '.blend'), [low] + list(keep))
    K.reset()
    return low

# --------------------------------------------------------------------------- mortar & pestle
def mortar_cuts(ob):
    """Lever 1: chisel facets on the lugs and conchoidal spalls on the arrises, as real geometry."""
    import random, sc_aaa_cuts as C
    r = random.Random(11); cut = []
    U = lambda a: Vector((math.cos(a), math.sin(a), 0.0)); Z = Vector((0, 0, 1))
    for i in range(0 if os.environ.get('AAA_MORTAR') in ('6', '7', '9', '10', '11', '12') else 4):   # v6: no lug chamfers (reviewers: 'notched corners, slab planes, low-poly')
        a = i * math.pi / 2 + math.pi / 4; u = U(a); t = Vector((-math.sin(a), math.cos(a), 0.0))
        for sgn in (-1, 1):                                       # outer vertical arrises: uneven chisel chamfers
            n = (u + t * sgn * r.uniform(0.7, 1.4) + Z * r.uniform(-0.15, 0.25)).normalized()
            p = u * 0.098 + t * sgn * 0.0093 + Z * 0.066 - n * r.uniform(0.003, 0.005)
            cut.append(C.chamfer(p, n, (0.007, 0.03)))
        n = (u + Z * r.uniform(1.0, 1.6) + t * r.uniform(-0.3, 0.3)).normalized()  # outer top arris
        cut.append(C.chamfer(u * 0.097 + Z * 0.083 - n * r.uniform(0.002, 0.003), n, (0.02, 0.007)))
        n = (u - Z * r.uniform(0.8, 1.3)).normalized()           # under-cut bottom arris
        cut.append(C.chamfer(u * 0.097 + Z * 0.04 - n * 0.002, n, (0.02, 0.006)))
    def bite(rad, z, nrm, k, sz, dp, front=False):
        for _ in range(k):
            a = r.uniform(-math.pi / 2 - 1.3, -math.pi / 2 + 1.3) if front and r.random() < 0.7 else r.uniform(0, 2 * math.pi); u = U(a)
            n = (u * nrm[0] + Z * nrm[1]).normalized()
            cut.append(C.spall(u * rad + Z * z, n, (r.uniform(*sz), r.uniform(sz[0] * 0.6, sz[1] * 0.7)), r.uniform(*dp), r.uniform(-0.4, 0.4), r.randrange(999)))
    if os.environ.get('AAA_MORTAR') in ('9', '10', '11', '12'):   # v9: same spall sets as v6, re-aimed at the wide bowl's arrises
        bite(0.0995, 0.108, (0.75, 0.65), 6, (0.007, 0.016), (0.002, 0.0038), True)
        bite(0.073, 0.108, (-0.6, 0.8), 3, (0.004, 0.008), (0.0012, 0.002))
        bite(0.077, 0.004, (0.8, -0.6), 4, (0.008, 0.015), (0.002, 0.0035), True)
    elif os.environ.get('AAA_MORTAR') in ('5', '7'):   # v5: fewer, smaller, shallower spalls (reviewers: 'over-fractured, hard-edged chips')
        if os.environ.get('AAA_MORTAR') == '7': pass   # v7: no outer-rim spalls; on a rounded rim each one bakes a round fresh-stone disc (reads as a decal)
        else: bite(0.089, 0.108, (0.75, 0.65), 4, (0.006, 0.011), (0.0012, 0.0022), True)
        bite(0.068, 0.108, (-0.6, 0.8), 2, (0.004, 0.007), (0.0008, 0.0014))
        bite(0.071 if os.environ.get('AAA_MORTAR') == '7' else 0.078, 0.006, (0.8, -0.6), 2, (0.006, 0.01), (0.0012, 0.002), True)
    else:
        bite(0.089, 0.108, (0.75, 0.65), 8, (0.008, 0.016), (0.002, 0.0038), True)   # outer rim arris
        bite(0.068, 0.108, (-0.6, 0.8), 3, (0.004, 0.008), (0.0012, 0.002))     # inner rim arris
        bite(0.078, 0.006, (0.8, -0.6), 4, (0.008, 0.015), (0.002, 0.0035), True)     # foot arris
    return C.subtract(ob, cut)

def brazier_cuts(ob):
    import random, sc_aaa_cuts as C
    r = random.Random(31); cut = []
    for _ in range(6):
        a = r.uniform(-math.pi / 2 - 1.4, -math.pi / 2 + 1.4); u = Vector((math.cos(a), math.sin(a), 0))
        cut.append(C.spall(u * 0.138 + Vector((0, 0, 0.205)), (u * 0.7 + Vector((0, 0, 0.7))).normalized(), (r.uniform(0.008, 0.016), r.uniform(0.006, 0.01)), r.uniform(0.0025, 0.004), r.uniform(-0.4, 0.4), r.randrange(999)))
    return C.subtract(ob, cut)

def brazier_cracks(ob):
    import sc_aaa_cuts as C
    cs = [C.crack_cutter(ob, (math.cos(a) * 0.139, math.sin(a) * 0.139, 0.2), (0.1, 0.0, -1.0), L, width=0.0016, depth=0.004, seed=sd)
          for a, L, sd in ((math.radians(-70), 0.07, 5), (math.radians(-125), 0.045, 9))]
    return C.subtract(ob, cs)

def slab_cuts(ob):
    import random, sc_aaa_cuts as C
    r = random.Random(41); cut = []
    for _ in range(7):
        side = r.choice(((0, -1), (1, 0), (-1, 0), (0, -1)))
        t = r.uniform(-0.14, 0.14)
        p = Vector((side[0] * 0.17 + (t if side[0] == 0 else 0), side[1] * 0.15 + (t if side[1] == 0 else 0), 0.04))
        n = (Vector((side[0], side[1], 0)) * 0.7 + Vector((0, 0, 0.7))).normalized()
        cut.append(C.spall(p, n, (r.uniform(0.01, 0.022), r.uniform(0.007, 0.012)), r.uniform(0.003, 0.005), 0, r.randrange(999)))
    return C.subtract(ob, cut)

def mortar_cracks(ob):
    import sc_aaa_cuts as C
    cs = [C.crack_cutter(ob, (math.cos(a) * 0.092, math.sin(a) * 0.092, 0.106), (0.15, 0.1, -1.0), L, seed=sd)
          for a, L, sd in ((math.radians(-95), 0.05, 3), (math.radians(200), 0.032, 7))]
    return C.subtract(ob, cs, self_isect=True)

def mortar():
    MO7 = os.environ.get('AAA_MORTAR') == '7'   # v7: round bellied bowl with a rolled rim, small rounded lugs, rim-only chips, thicker pestle (reviewers: 'damaged cube', 'thin stick')
    MO6 = os.environ.get('AAA_MORTAR') in ('6', '7')
    MO4 = os.environ.get('AAA_MORTAR') in ('4', '5', '6', '7')   # v4: v3 + shallower, fewer chisel facets (reviewers: 'lumpy noise stone')
    MO3 = os.environ.get('AAA_MORTAR') in ('3', '4', '5', '6', '7')   # v3: old scan detail, cooler less saturated stone; pestle in the same scanned stone
    MO2 = os.environ.get('AAA_MORTAR') == '2'   # v2 (lost): calmer scan (reviewers: 'noisy rock'), softer worn lugs ('blocky')
    MO8 = os.environ.get('AAA_MORTAR') == '8'   # v8: v6 geometry, procedural pale limestone instead of the gritty full scan (t6 stand: 'sandpaper grit', 'sack of rock'; KCD2's mortar is smooth pale stone)
    if MO8: os.environ['AAA_MORTAR'] = '6'; MO6 = MO4 = MO3 = True
    MO12 = os.environ.get('AAA_MORTAR') == '12'   # v12: v9 without the body chisel facets: on a near-cylindrical wall a 1 mm trim plane is a long vertical flat stripe ('a stuck-on slab', every v9-v11 reviewer)
    MO11 = os.environ.get('AAA_MORTAR') == '11'   # v11: v9 + paler polished grinding bowl + paler, handling-polished pestle that separates from the bowl
    MO9 = os.environ.get('AAA_MORTAR') in ('9', '10', '11', '12')   # v9: KCD2-style turned bowl, wide, thick rounded rim, no lugs; keeps the scan stone and rim spalls on a sharp outer arris (every reviewer: 'clean turned symmetrical bowl')
    if MO9: MO6 = MO4 = MO3 = True
    st = M2.stone('M_MortarStone', base=(0.40, 0.38, 0.34), residue=0.05, inner_r=0.062, grain=0.5, scan_tiles=8.0, scan_col=0.3, bowl=(0.069, 0.036, 0.108)) if MO8 else M2.stone('M_MortarStone', base=(0.30, 0.29, 0.265) if MO3 else (0.34, 0.30, 0.24), residue=0.05, grain=0.4, scan_tiles=8.0, scan_col=0.7, bowl=(0.074, 0.042, 0.108) if MO9 else (0.069, 0.036, 0.108), inner_r=0.068 if MO9 else 0.062,
                  full=dict(asset='rock_surface', tile=0.4, mode='cyl', sat=0.8, rough=(0.45, 0.9), nstr=0.5, contrast=0.55) if MO2 else
                       dict(asset='rock_surface', tile=0.25, mode='cyl', sat=0.5 if MO3 else 0.8, rough=(0.45, 0.9), nstr=0.8, contrast=0.9))
    outer = [(0.004, 0.0), (0.072, 0.0), (0.078, 0.006, 'c'), (0.074, 0.016), (0.07, 0.03), (0.08, 0.075), (0.088, 0.1), (0.089, 0.108, 'c')]
    if MO9:
        outer = [(0.004, 0.0), (0.07, 0.0), (0.077, 0.004, 'c'), (0.083, 0.015), (0.092, 0.04), (0.098, 0.065), (0.1, 0.088), (0.1, 0.103), (0.0995, 0.108, 'c')]   # square outer arris: spalls need a corner to bite (a rounded one bakes pale discs)
        inner = [(0.073, 0.108, 'c'), (0.071, 0.1), (0.063, 0.07), (0.046, 0.05), (0.004, 0.042)]
    if MO7: outer = [(0.004, 0.0), (0.064, 0.0), (0.071, 0.005, 'c'), (0.079, 0.02), (0.087, 0.045), (0.092, 0.07), (0.094, 0.09), (0.092, 0.102, 'c'), (0.086, 0.108, 'c')]
    if not MO9: inner = [(0.068, 0.108, 'c'), (0.066, 0.1), (0.058, 0.07), (0.04, 0.045), (0.004, 0.036)]
    m = solid_lathe('mortar', outer, inner, 64)
    lugs = []
    for i in range(0 if MO9 else 4):
        # carved lug: a tapered block with flat chiselled faces, wider where it grows from the wall,
        # its top sloping down and out, its bottom undercut back into the body
        a = i * math.pi / 2 + math.pi / 4
        lug = K.box(f'lug{i}', (0.022, 0.024, 0.04) if MO7 else (0.027, 0.027, 0.054) if MO2 else (0.03, 0.03, 0.06), (0, 0, 0), cuts=(2, 2, 2))
        for v in lug.data.vertices:
            x, y, z = v.co                                          # x: radial out (-0.015 .. 0.015)
            t = (x + 0.015) / 0.03
            v.co.y = y * (1.0 - 0.38 * t)                           # narrower at the outer face
            v.co.z = z * (1.0 - 0.3 * t) - 0.006 * t + (0.004 * t if z < 0 else 0.0)
        lr = 0.093 if MO7 else 0.083
        lug.location = (math.cos(a) * lr, math.sin(a) * lr, 0.07 if MO7 else 0.068); lug.rotation_euler = (0, 0, a)
        K.bevel(lug, 0.008 if MO7 else 0.005 if (MO2 or MO6) else 0.0015, 4 if MO7 else 3 if (MO2 or MO6) else 1, harden=False)
        lugs.append(lug)
    parts = [K.make_part([m] + lugs, st, pid(), 0.0009, K.chain(__import__('sc_aaa_sculpt').chisel(0 if MO12 else 30, (0.015, 0.035), (0.0004, 0.001), 0.25, 3, rim=(0.106, 6 if MO9 else 10)) if MO4 else __import__('sc_aaa_sculpt').chisel(70, (0.012, 0.03), (0.0008, 0.002), 0.35, 3, rim=(0.106, 14)), wear(0.0004, 22, chips=0 if MO7 else 8, chip_r=(0.002, 0.0045), chip_depth=0.3, seed=1.0)), fillet=2,
                         cuts=None if os.environ.get('AAA_NOCUTS') else mortar_cuts, post=None if (os.environ.get('AAA_NOCUTS') or os.environ.get('AAA_NOCRACK')) else mortar_cracks)]
    # pestle resting in the bowl, leaning on the rim
    pe = solid_lathe('pestle', [(0.004, 0.0), (0.026, 0.003), (0.03, 0.018), (0.026, 0.04), (0.019, 0.08), (0.017, 0.15), (0.021, 0.175), (0.02, 0.188), (0.004, 0.195)], None, 32) if (MO7 or MO9) else solid_lathe('pestle', [(0.004, 0.0), (0.024, 0.004), (0.027, 0.02), (0.02, 0.05), (0.014, 0.09), (0.013, 0.15), (0.017, 0.175), (0.016, 0.185), (0.004, 0.19)], None, 32)
    pe.rotation_euler = (math.radians(-38), 0, math.radians(30)); pe.location = (-0.012, 0.02, 0.04)
    K.apply_mods(pe); bpy.context.view_layer.objects.active = pe
    bpy.ops.object.select_all(action='DESELECT'); pe.select_set(True); bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    pst = M2.stone('M_PestleStone', base=(0.39, 0.37, 0.33), grain=0.6, scan_tiles=20.0, scan_col=0.3) if MO8 else M2.stone('M_PestleStone', base=(0.40, 0.385, 0.35) if MO11 else (0.29, 0.28, 0.255) if MO3 else (0.32, 0.28, 0.22), grain=1.0, scan_tiles=20.0, scan_col=0.5,
                   full=dict(asset='rock_surface', tile=0.18, mode='tri', sat=0.4, rough=(0.28, 0.6), nstr=0.7, contrast=0.6) if MO11 else
                        dict(asset='rock_surface', tile=0.18, mode='tri', sat=0.5, rough=(0.35, 0.85), nstr=1.0, contrast=0.9) if MO3 else
                        dict(asset='rock_surface', tile=0.1, mode='tri', sat=0.8, rough=(0.35, 0.85), nstr=1.8, contrast=0.45))
    parts.append(K.make_part(pe, pst, pid(), 0.0009, wear(0.0003, 50, seed=5.0)))
    finish('Mortar', parts, 20000, 2048)

# --------------------------------------------------------------------------- glazed jug + albarello jar
def jug():
    cm = M2.ceramic('M_JugClay', body=(0.30, 0.12, 0.05), glaze=(0.05, 0.03, 0.012), glaze_z=0.16, gloss=0.25)
    ck = M2.cork('M_Cork')
    outer = [(0.004, 0.0), (0.062, 0.0), (0.066, 0.008), (0.06, 0.014), (0.085, 0.06), (0.102, 0.12), (0.096, 0.17), (0.07, 0.21), (0.04, 0.24), (0.036, 0.26), (0.042, 0.275), (0.04, 0.282)]
    inner = [(0.03, 0.282), (0.028, 0.27), (0.03, 0.24), (0.004, 0.235)]
    j = solid_lathe('jug', outer, inner, 64)
    parts = [K.make_part(j, cm, pid(), 0.0018, wear(0.0005, 30, chips=5, chip_r=(0.002, 0.006), seed=3.0))]
    # strap handle from shoulder to neck
    pts = [Vector((0.0, 0.0, 0.0))] * 0
    for i in range(9):
        t = i / 8
        ang = math.pi * (0.1 + 0.85 * t)
        pts.append(Vector((0.0, 0.095 + 0.05 * math.sin(ang), 0.17 + 0.09 * (1 - math.cos(ang)) * 0.55)))
    pts[0] = Vector((0, 0.088, 0.16)); pts[-1] = Vector((0, 0.04, 0.262))
    h = K.tube('handle', pts, 0.011, 10)
    h.scale = (1.0, 1.0, 1.0)
    parts.append(K.make_part(h, cm, pid(), 0.0015, wear(0.0004, 40, seed=4.0)))
    c = solid_lathe('cork', [(0.004, 0.25), (0.027, 0.25), (0.031, 0.29), (0.033, 0.3), (0.004, 0.302)], None, 24)
    parts.append(K.make_part(c, ck, pid(), 0.0012, wear(0.0008, 60, seed=6.0)))
    finish('Jug', parts, 9000, 2048)

def jar():
    """Albarello (apothecary jar): waisted cylinder, tin-glaze cream with blue bands; cloth cover tied with twine."""
    cm = M2.ceramic('M_JarClay', body=(0.30, 0.16, 0.08), glaze=(0.33, 0.30, 0.22), glaze_z=0.012, gloss=0.2, paint=(0.02, 0.035, 0.12))
    tw = M2.twine('M_Twine')
    outer = [(0.004, 0.0), (0.05, 0.0), (0.054, 0.012), (0.052, 0.03), (0.045, 0.07), (0.052, 0.11), (0.05, 0.125), (0.04, 0.132), (0.044, 0.14)]
    inner = [(0.036, 0.14), (0.034, 0.13), (0.004, 0.125)]
    j = solid_lathe('jar', outer, inner, 56)
    parts = [K.make_part(j, cm, pid(), 0.0015, wear(0.0004, 35, chips=4, chip_r=(0.002, 0.005), seed=7.0))]
    # cloth cap: draped disc with radial folds growing toward an uneven hem
    cap = solid_lathe('cap', [(0.004, 0.143), (0.046, 0.1435), (0.0505, 0.137), (0.0525, 0.118), (0.0512, 0.112), (0.048, 0.13), (0.043, 0.1395), (0.004, 0.1395)], None, 72)
    def folds(ob):
        import numpy as np
        co, no = K.mesh_arrays(ob)
        ang = np.arctan2(co[:, 1], co[:, 0]); z = co[:, 2]
        below = np.clip((0.134 - z) / 0.02, 0, 1)
        f = np.sin(ang * 11 + 1.3 * np.sin(ang * 3)) * 0.5 + 0.5
        rad = np.linalg.norm(co[:, :2], axis=1)[:, None] + 1e-6
        co[:, :2] += co[:, :2] / rad * (f * 0.004 * below)[:, None]
        co[:, 2] += (np.sin(ang * 4 + 0.7) * 0.004 + np.sin(ang * 9) * 0.002) * below
        K.set_co(ob, co)
    cloth = M2.paper('M_Cloth', col=(0.30, 0.25, 0.17), ink=False)
    parts.append(K.make_part(cap, cloth, pid(), 0.0011, folds, fillet=2))
    ring = K.lathe('tie', [(0.046, 0.128), (0.05, 0.128), (0.05, 0.134), (0.046, 0.134)], 40, close=True)
    parts.append(K.make_part(ring, tw, pid(), 0.0008, None))
    finish('Jar', parts, 7000, 2048)

# --------------------------------------------------------------------------- candle on an iron pricket
def candle():
    ir = M2_iron = __import__('sc_aaa_mats').iron('M_CandleIron', rust=0.3)
    wx = M2.wax('M_Wax')
    dish = solid_lathe('dish', [(0.004, 0.0), (0.055, 0.0), (0.06, 0.012), (0.057, 0.014), (0.05, 0.006), (0.012, 0.006), (0.01, 0.03), (0.004, 0.035)], None, 40)
    parts = [K.make_part(dish, ir, pid(), 0.0012, wear(0.0004, 70, seed=9.0), low='source')]
    for i in range(3):
        a = i * 2 * math.pi / 3
        foot = K.box(f'foot{i}', (0.03, 0.012, 0.008), (math.cos(a) * 0.05, math.sin(a) * 0.05, 0.002), (0, 0, math.degrees(a)))
        parts.append(K.make_part(foot, ir, pid(), 0.001, None, bev=0.003, low='source'))
    # candle: body + drip rivulets (tapered tubes with a bulb end) voxel-unioned and polished
    cd = solid_lathe('candle', [(0.004, 0.03), (0.013, 0.03), (0.0128, 0.10), (0.0126, 0.134), (0.0118, 0.139), (0.008, 0.136), (0.004, 0.133)], None, 32)
    import random as _r; rr = _r.Random(7)
    drips = [cd]
    for i in range(7):
        a = rr.uniform(0, 2 * math.pi); L = rr.uniform(0.015, 0.075); R = 0.0128
        n = 8; pts = []; rad = []
        for k in range(n + 1):
            t = k / n; z = 0.139 - t * L
            wob = 0.0015 * math.sin(t * 9 + i)
            rr_ = R + 0.0006 + 0.001 * math.sin(t * math.pi)
            pts.append((rr_ * math.cos(a + wob * 20), rr_ * math.sin(a + wob * 20), z))
            rad.append(0.0022 * (1 - 0.45 * t) + (0.0012 if k == n else 0))
        drips.append(K.tube(f'drip{i}', pts, 0.002, 8, radii=rad))
    parts.append(K.make_part(drips, wx, pid(), 0.0006, wear(0.0002, 80, seed=10.0), fillet=8))
    wick = K.tube('wick', [(0, 0, 0.128), (0.0005, 0, 0.142), (0.002, 0.0005, 0.149)], 0.0011, 6)
    parts.append(K.make_part(wick, M2.charcoal('M_Wick', glow=0.4), pid(), 0.0004, None))
    # wax pooled in the dish
    pool = solid_lathe('pool', [(0.004, 0.006), (0.03, 0.006), (0.028, 0.0085), (0.014, 0.01), (0.004, 0.01)], None, 32)
    parts.append(K.make_part(pool, wx, pid(), 0.0008, wear(0.0006, 60, seed=11.0)))
    finish('Candle', parts, 5000, 1024)

# --------------------------------------------------------------------------- open book
def book():
    lt = M2.leather('M_BookLeather'); pp = M2.paper('M_Pages', half=(0.1, 0.13))
    parts = []
    W, Hh, th = 0.2, 0.27, 0.004
    for s in (-1, 1):
        # cover board, slightly bigger than the block, lying flat, splayed 4 deg up
        cv = K.box(f'cover{s}', (W + 0.01, Hh + 0.012, th), (s * (W / 2 + 0.006), 0, th / 2), (0, s * -3, 0), cuts=(6, 6, 1))
        parts.append(K.make_part(cv, lt, pid(), 0.0012, wear(0.0006, 30, seed=12.0 + s), bev=0.002))
        # page block half: curved toward the spine (the 'gutter' dip)
        blk = K.box(f'pages{s}', (W, Hh, 0.018), (0, 0, 0), cuts=(24, 4, 2))
        for v in blk.data.vertices:
            u = (v.co.x + W / 2) / W            # 0 at spine side .. 1 at fore edge (for s=1)
            if s < 0: u = 1 - u
            u = min(max(u, 0.0), 1.0)
            v.co.z += 0.022 * math.sin(math.pi * min(u, 0.5)) ** 0.6 * (1 - 0.3 * u) - 0.006 * u * u + 0.0015 * math.sin(v.co.y * 40 + s)
        blk.location = (s * (W / 2 + 0.004), 0, th + 0.009)
        parts.append(K.make_part(blk, pp, pid(), 0.0012, wear(0.0003, 80, seed=14.0 + s)))
    sp = K.box('spine', (0.02, Hh + 0.012, 0.012), (0, 0, 0.004), cuts=(1, 4, 1))
    parts.append(K.make_part(sp, lt, pid(), 0.0012, None, bev=0.003))
    finish('Book', parts, 6000, 2048)

PROPS = dict(mortar=mortar, jug=jug, jar=jar, candle=candle, book=book)

# --------------------------------------------------------------------------- copper still on a clay brazier, on a stone slab
STILL_SPOUT_END = (-0.30, -0.12, 0.17)
SEAM_A = -1.9                              # pot seam/rivet angle (rad), toward the cameras (-Y) off the spout side     # spout tip in still-local coords (compose puts the receiver here)
def still():
    st = M2.stone('M_Slab', base=(0.16, 0.15, 0.13), ash=(0.115, 0.2))
    import random as _rh; rh = _rh.Random(5); HOLES = []
    for i in range(7):   # same draws, same order as before: (angle, radius, mid-pinch, y-squash, tilt, z offset)
        a = 2 * math.pi * (i + rh.uniform(-0.25, 0.25)) / 7; R_ = rh.uniform(0.0065, 0.011); pm = rh.uniform(0.85, 1.15)
        sy = rh.uniform(0.7, 1.3); rx = rh.uniform(-0.15, 0.15); zo = rh.uniform(-0.008, 0.008)
        HOLES.append((a, R_, pm, sy, rx, zo))
    hole_vents = tuple((h[0], 0.158 + h[5]) for h in HOLES) if os.environ.get('AAA_BRZ') in ('3', '4', '5') else tuple((2 * math.pi * (i + 0.5) / 7, 0.16) for i in range(7))
    clay = M2.ceramic('M_Brazier', body=(0.15, 0.06, 0.026), glaze=(0.1, 0.1, 0.1), glaze_z=5.0, soot_above=0.16, flash=1.0, soot_vents=((-0.5, 0.115), (1.6, 0.115), (3.7, 0.115)) + hole_vents)   # glaze_z above top: bare clay
    cu = M2.copper('M_StillCopper', patina=0.6, soot_z=0.285, lips=(0.333, 0.468, 0.40), polish=((0.118, 0, 0.295, 0.035), (-0.118, 0, 0.295, 0.035), (0.0, -0.08, 0.40, 0.05)), seam=(SEAM_A, 0.027), soot_down=0.8)
    ir = __import__('sc_aaa_mats').iron('M_StillIron', rust=0.6)
    coal = M2.ember('M_Coal', glow=1.0); ashm = M2.ember('M_AshBed', glow=0.6, ash_bed=True)
    parts = []
    slab = K.box('slab', (0.34, 0.30, 0.04), (0, 0, 0.02), (0, 0, 3), cuts=(6, 6, 2))
    parts.append(K.make_part(slab, st, pid(), 0.002, wear(0.0012, 18, chips=4, chip_r=(0.004, 0.012), seed=21.0), bev=0.006, cuts=slab_cuts))
    # brazier: thick-walled clay pot with a stoke hole, coal inside
    outer = [(0.004, 0.04), (0.10, 0.04), (0.112, 0.06), (0.118, 0.16), (0.124, 0.19), (0.12, 0.2)]
    inner = [(0.098, 0.2), (0.096, 0.17), (0.09, 0.075), (0.004, 0.07)]
    # bellied, dark-fired body with a rolled rim lip and a band of air holes (real braziers are pierced)
    outer = [(0.004, 0.04), (0.098, 0.04), (0.11, 0.055), (0.122, 0.1), (0.124, 0.15), (0.12, 0.178), (0.128, 0.186), (0.138, 0.194), (0.139, 0.203), (0.132, 0.209)]
    inner = [(0.112, 0.209), (0.104, 0.2), (0.1, 0.17), (0.096, 0.1), (0.09, 0.075), (0.004, 0.07)]
    br = solid_lathe('brazier', outer, inner, 64)
    holes = []
    for i, a in enumerate((-0.5, 1.6, 3.7)):                 # stoke / draught holes
        holes.append(K.box(f'vent{i}', (0.08, 0.05, 0.035), (math.cos(a) * 0.11, math.sin(a) * 0.11, 0.1), (0, 0, math.degrees(a))))
    for i, (a, R_, pm, sy, rx, zo) in enumerate(HOLES):       # hand-cut air holes: uneven size, spacing, height and shape
        prof = [(0.0001, -0.03), (R_, -0.03), (R_ * pm, 0.0), (R_, 0.03), (0.0001, 0.03)]
        hc = K.lathe(f'hole{i}', prof, 28, close=True)
        hc.scale = (1.0, sy, 1.0)
        hc.rotation_euler = (rx, math.radians(90), a); hc.location = (math.cos(a) * 0.12, math.sin(a) * 0.12, 0.158 + zo)
        holes.append(hc)
    for i, h in enumerate(holes):
        bo = br.modifiers.new(f'h{i}', 'BOOLEAN'); bo.operation = 'DIFFERENCE'; bo.object = h; bo.solver = 'EXACT'
        K.apply_mods(br); bpy.data.objects.remove(h)
    parts.append(K.make_part(br, clay, pid(), 0.0014, K.chain(__import__('sc_aaa_sculpt').handbuilt(140, seed=22, zr=(0.045, 0.2)), wear(0.0008, 14, chips=12, chip_r=(0.005, 0.014), chip_depth=0.3, seed=22.0)), fillet=1, cuts=brazier_cuts, post=brazier_cracks))
    ash = solid_lathe('ash', [(0.004, 0.068), (0.092, 0.07), (0.09, 0.12), (0.07, 0.15), (0.03, 0.16), (0.004, 0.162)], None, 40)
    parts.append(K.make_part(ash, ashm, pid(), 0.0015, wear(0.004, 30, seed=22.5)))
    import random as _r; rr = _r.Random(23); coals = []
    for i in range(26):
        a = rr.uniform(0, 2 * math.pi); r = rr.uniform(0, 0.075)
        c = K.box(f'coal{i}', (rr.uniform(0.02, 0.04), rr.uniform(0.015, 0.025), rr.uniform(0.012, 0.02)),
                  (r * math.cos(a), r * math.sin(a), 0.15 + rr.uniform(0, 0.03) - r * 0.4), (rr.uniform(0, 40), rr.uniform(0, 40), rr.uniform(0, 180)))
        coals.append(c)
    for i, c in enumerate(coals):                              # each coal its own piece: real gaps that glow
        parts.append(K.make_part(c, coal, pid(), 0.0012, K.chain(wear(0.0012, 70, chips=4, chip_r=(0.003, 0.007), chip_depth=0.4, seed=24.0 + i),
                                                              K.facets(12, 0.004, 0.008, 0.0008, 0.3, 24.5 + i)), bev=0.003))
    # iron trivet bars across the mouth
    for i, a in enumerate((0, 60, 120)):
        b = K.box(f'bar{i}', (0.26, 0.012, 0.012), (0, 0, 0.206), (0, 0, a), cuts=(8, 1, 1))
        parts.append(K.make_part(b, ir, pid(), 0.0012, wear(0.0004, 80, seed=25.0 + i), bev=0.002, low='source'))
    # copper pot (cucurbit) sitting on the trivet, with a rolled lip and two ring handles
    pot = solid_lathe('pot', [(0.004, 0.214), (0.06, 0.214), (0.095, 0.235), (0.112, 0.27), (0.108, 0.31), (0.085, 0.335), (0.06, 0.345), (0.062, 0.352),
                              (0.054, 0.352), (0.052, 0.345), (0.004, 0.34)], None, 64)
    potparts = [pot]
    for s in (-1, 1):
        ring = K.lathe(f'ear{s}', [(0.018, -0.003), (0.024, -0.003), (0.024, 0.003), (0.018, 0.003)], 24, close=True)
        ring.rotation_euler = (math.radians(90), 0, 0); ring.location = (s * 0.118, 0, 0.295)
        K.apply_mods(ring); bpy.ops.object.select_all(action='DESELECT'); ring.select_set(True)
        bpy.context.view_layer.objects.active = ring; bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)
        potparts.append(ring)
    for i in range(11):                                       # riveted side seam of the pot sheet
        z = 0.235 + i * 0.009; rr_ = {0: 0.096, 1: 0.103, 2: 0.108, 3: 0.111, 4: 0.112, 5: 0.112, 6: 0.111, 7: 0.109, 8: 0.105, 9: 0.1, 10: 0.094}[i] + 0.0005
        rv = K.lathe(f'rivet{i}', [(0.0005, -0.002), (0.0032, -0.002), (0.0032, 0.0), (0.0025, 0.0016), (0.0012, 0.0024), (0.0005, 0.0026)], 12, close=True)
        rv.rotation_euler = (0, math.radians(90), 0); rv.location = (math.cos(SEAM_A) * rr_, math.sin(SEAM_A) * rr_, z); rv.rotation_euler.rotate_axis('X', 0)
        rv.rotation_euler = (0, math.radians(90), SEAM_A)
        potparts.append(rv)
    parts.append(K.make_part(potparts, cu, pid(), 0.0011, K.chain(wear(0.0002, 50, seed=26.0), (lambda ob: __import__('sc_aaa_cuts').blows(ob, 10, 44, (0.007, 0.011), 0.0006, (0.22, 0.345), 26)), K.seam_lap(SEAM_A - 0.025, 0.05, 0.0008, (0.222, 0.344), 0.08)), fillet=2, low='source'))
    # alembic head: dome sitting in the pot neck, beak spout running down to the receiver
    head = solid_lathe('head', [(0.004, 0.338), (0.05, 0.338), (0.051, 0.36), (0.07, 0.38), (0.078, 0.41), (0.07, 0.44), (0.045, 0.462), (0.02, 0.47), (0.004, 0.471)], None, 56)
    sx, sy, sz = STILL_SPOUT_END
    pts = []
    for i in range(14):
        t = i / 13
        x = -0.06 + (sx + 0.06) * t; y = 0.0 + sy * t
        z = 0.41 + (sz - 0.41) * t - 0.03 * math.sin(math.pi * t)
        pts.append((x, y, z))
    rad = [0.016 - 0.009 * (i / 13) for i in range(14)]
    beak = K.tube('beak', pts, 0.01, 16, radii=rad)
    parts.append(K.make_part([head, beak], cu, pid(), 0.0011, K.chain(wear(0.0002, 50, seed=27.0), (lambda ob: __import__('sc_aaa_cuts').blows(ob, 8, 30, (0.006, 0.009), 0.0005, (0.345, 0.468), 27))), fillet=4, low='source'))
    # clay luting (sealing paste) where head meets pot: a lumpy ring
    lute = K.lathe('lute', [(0.048, 0.334), (0.064, 0.336), (0.066, 0.348), (0.05, 0.352)], 40, close=True)
    parts.append(K.make_part(lute, clay, pid(), 0.001, wear(0.0015, 70, seed=28.0)))
    finish('Still', parts, 32000, 2048)

# --------------------------------------------------------------------------- herb bundles (dried, tied)
def herbs():
    """Three dried herb bundles: dense stems fanning from a twine tie, many small crinkled leaves,
    one bundle with flower heads; plus loose leaves. Built along +X from the tie."""
    hb = M2.herb('M_Herb'); tw = M2.twine('M_HerbTwine'); fl = M2.herb('M_HerbFlower', col=(0.16, 0.07, 0.12))
    import random as _r, numpy as np; rr = _r.Random(31)
    parts = []
    for b in range(3):
        ox, oy, rot = [(0.0, 0.0, 0), (0.06, 0.12, 22), (-0.08, 0.25, -14)][b]
        stems, leaves, heads = [], [], []
        L = rr.uniform(0.2, 0.26)
        for s in range(34):
            ay = rr.gauss(0, 0.5); az = rr.uniform(0, 1)
            spread = rr.uniform(0.015, 0.05)
            pts = []
            for t in (0, 0.12, 0.3, 0.5, 0.7, 0.85, 1.0):
                fan = spread * max(0.0, t - 0.12) ** 1.2
                pts.append((t * L * rr.uniform(0.85, 1.0) if t == 1.0 else t * L, ay * 0.006 + math.copysign(fan, ay) * abs(ay), 0.011 + az * 0.008 * t + fan * 0.3 + 0.002 * math.sin(t * 9 + s)))
            stems.append(K.tube(f'stem{b}{s}', pts, 0.0011, 5))
            for l in range(rr.randint(5, 9)):
                t = rr.uniform(0.25, 1.0); i = min(int(t * 6), 5); f = t * 6 - i
                x, y, z = [pts[i][k] * (1 - f) + pts[i + 1][k] * f for k in range(3)]
                ln = rr.uniform(0.012, 0.024)
                lf = K.box(f'leaf{b}{s}{l}', (ln, ln * 0.35, 0.0012), (x, y, z), (rr.uniform(-60, 60), rr.uniform(-35, 35), rr.uniform(-70, 70)), cuts=(5, 3, 1))
                for v in lf.data.vertices:                       # pointed, curled, crinkled leaf
                    u = abs(v.co.x) / (ln / 2); v.co.y *= max(0.06, 1 - u ** 2)
                    v.co.z += 0.25 * ln * (u * u) + 0.0008 * math.sin(v.co.x * 900 + l) + 0.12 * ln * (v.co.y / (ln * 0.18)) ** 2
                leaves.append(lf)
            if b == 1 and s % 2 == 0:                           # flower heads on bundle 2 (lavender-like spikes)
                x, y, z = pts[-1]
                for q in range(6):
                    hd = K.box(f'fl{b}{s}{q}', (0.005, 0.004, 0.004), (x + 0.004 + q * 0.0045, y + rr.uniform(-0.001, 0.001), z + rr.uniform(-0.001, 0.001)), (rr.uniform(0, 90), rr.uniform(0, 90), 0))
                    heads.append(hd)
        tie = K.lathe(f'tie{b}', [(0.0, 0.03), (0.016, 0.03), (0.019, 0.032), (0.019, 0.042), (0.016, 0.044), (0.0, 0.044)], 20, close=True)
        tie.rotation_euler = (0, math.radians(90), 0); tie.location = (0.0, 0, 0.014)
        K.apply_mods(tie); bpy.ops.object.select_all(action='DESELECT'); tie.select_set(True); bpy.context.view_layer.objects.active = tie
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)
        grp = [K.make_part(stems + leaves, hb, pid(), 0.0006, None, fillet=1), K.make_part(tie, tw, pid(), 0.0006, None)]
        if heads: grp.append(K.make_part(heads, fl, pid(), 0.0006, K.wear_generic(0.0006, 400, seed=b), bev=0.0015))
        for o in grp:
            o.location = (ox, oy, 0); o.rotation_euler = (0, 0, math.radians(rot))
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)
        parts += grp
    finish('Herbs', parts, 20000, 2048)

# --------------------------------------------------------------------------- glassware (unbaked glass + liquid volumes)
def glassware():
    G2 = os.environ.get('AAA_GLASS') == '2'   # 2: multiply glass + opaque liquid (lost the A/B)
    G3 = os.environ.get('AAA_GLASS') == '3'   # 3: old glass, less haze, specks, see-through liquid, sort fix
    G6 = os.environ.get('AAA_GLASS') == '6'          # 6: glass 4 + less neon liquid, thicker rim bead and walls
    G7 = os.environ.get('AAA_GLASS') in ('7', '8', '9', '10', '11', '12', '13', '14', '15'); G8 = os.environ.get('AAA_GLASS') in ('8', '9', '10', '11', '12', '13', '14', '15')   # 8: + rim glint, patchy dirt, calmer liquid; 7: glass 4 + clearer glass + more see-through liquid
    G4 = os.environ.get('AAA_GLASS') in ('4', '5', '6', '7', '8', '9', '10', '11', '12', '13', '14', '15')   # 4: old glass + geometry, fake-refraction liquid (won the A/B, 15/15 vs 12/14)
    G5 = os.environ.get('AAA_GLASS') == '5'          # 5: + welded liquid axis (no dark cone) + thick-edge absorption
    if G4:
        rd = 0.55 if G5 else 0.0
        gk = dict(glow=0.45, lift=1.8) if G6 else dict(alpha=0.32, glow=0.5, lift=1.9, rim_dark=0.4) if G8 else dict(alpha=0.32) if G7 else {}
        gl = M2.glass('M_Glass', rough=0.012) if G6 else M2.glass('M_Glass'); green = M2.liquid4('M_PotionGreen', (0.025, 0.11, 0.012), **dict(dict(rim_dark=rd), **gk))
        red = M2.liquid4('M_PotionRed', (0.13, 0.008, 0.006), **dict(dict(rim_dark=rd), **gk)); amber = M2.liquid4('M_PotionAmber', (0.3, 0.12, 0.02), **dict(dict(rim_dark=rd), **dict(gk, alpha=0.25 if G7 else 0.4)))
    elif G3:
        gl = M2.glass3('M_Glass'); green = M2.liquid3('M_PotionGreen', (0.025, 0.11, 0.012))
        red = M2.liquid3('M_PotionRed', (0.13, 0.008, 0.006)); amber = M2.liquid3('M_PotionAmber', (0.3, 0.12, 0.02), 0.32)
    elif G2:
        gl = M2.glass2('M_Glass'); green = M2.liquid2('M_PotionGreen', (0.025, 0.11, 0.012))
        red = M2.liquid2('M_PotionRed', (0.13, 0.008, 0.006)); amber = M2.liquid2('M_PotionAmber', (0.3, 0.12, 0.02), 0.6)
    else:
        gl = M2.glass('M_Glass'); green = M2.liquid('M_PotionGreen', (0.025, 0.11, 0.012))
        red = M2.liquid('M_PotionRed', (0.13, 0.008, 0.006)); amber = M2.liquid('M_PotionAmber', (0.3, 0.12, 0.02), 0.7)
    objs = []
    def vessel(name, outer, wall, segs, loc, liq=None, fill=None, liq_mat=None, mat=None):
        if G6: wall *= 1.6   # g6: heavier blown glass
        # thick base (a kick-up punt three walls deep) and a fire-polished rolled rim bead: the cues that
        # make game glass read as heavy blown glass rather than a soap bubble
        inner = [(max(r - wall, 0.002), z) for (r, z) in reversed(outer)]
        inner[-1] = (0.002, outer[0][1] + wall * 3.5); inner[-2] = (inner[-2][0], max(inner[-2][1], outer[0][1] + wall * 2.5))
        v = K.lathe(name, list(outer) + inner, segs, close=True); v.location = loc
        sub = K.subsurf(v, 1); K.apply_mods(v); K.smooth(v)
        rt, zt = outer[-1]; br = wall * (2.2 if os.environ.get('AAA_GLASS') == '6' else 1.3)   # g6: fat fire-polished lip
        bead = K.lathe(name + '_rim', [(rt - wall * 0.6 + br * math.cos(math.radians(a)), zt - br * 0.4 + br * math.sin(math.radians(a))) for a in range(-90, 271, 30)], segs, close=True)
        K.subsurf(bead, 1); K.apply_mods(bead); K.smooth(bead); bead.location = loc
        bead.data.materials.append(mat or gl); objs.append(bead)
        v.data.materials.append(mat or gl); objs.append(v)
        if liq is not None:
            # liquid volume: follows the inner wall up to the fill line, with a meniscus that climbs the wall
            zb = outer[0][1] + wall + 0.0006
            # resample the wall densely: a sparse profile lets subdivision shrink the liquid into a barrel
            def r_at(z):
                for (r0, z0), (r1, z1) in zip(outer, outer[1:]):
                    if z0 <= z <= z1 and z1 > z0: return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
                return outer[-1][0]
            zs = [zb + (fill - zb) * k / 24 for k in range(1, 24)]
            wallpts = [(r_at(z) - wall - 0.0005, z) for z in zs if r_at(z) - wall - 0.0005 > 0.003]
            wallpts = [(max(wallpts[0][0] * 0.6, 0.003), zb + 0.0004)] + wallpts if wallpts else wallpts
            if not (G2 or G3 or os.environ.get('AAA_GLASSFIX')):   # baseline (won the A/B): the original sparse profile
                wallpts = [(r - wall - 0.0004, z) for (r, z) in outer if zb < z < fill and r - wall - 0.0004 > 0.003]
            rf = wallpts[-1][0] if wallpts else 0.01
            if G2 or G3 or os.environ.get('AAA_GLASSFIX'):
                lq = [(0.0, zb)] + wallpts + [(rf, fill + 0.0012), (rf * 0.9, fill + 0.0004), (rf * 0.5, fill + 0.0002), (0.0, fill + 0.0002)]
            elif G5:   # weld the axis only (keep the winning sparse profile)
                lq = [(0.0, zb)] + wallpts + [(rf, fill + 0.0012), (rf * 0.85, fill + 0.0002), (0.0, fill + 0.0002)]
            elif os.environ.get('AAA_GLASS') in ('12', '13', '14', '15'):   # g12: a meniscus that visibly climbs the wall (3 mm, curved), not a hard flat top
                lq = [(0.0015, zb)] + wallpts + [(rf, fill + 0.003), (rf * 0.97, fill + 0.0016), (rf * 0.9, fill + 0.0006), (rf * 0.7, fill + 0.0001), (0.0015, fill)]
            else:
                lq = [(0.0015, zb)] + wallpts + [(rf, fill + 0.0012), (rf * 0.85, fill + 0.0002), (0.0015, fill)]
            L = K.lathe(name + '_liq', lq, segs, close=True); L.location = loc
            import bmesh
            if lq[0][0] == 0.0:   # weld the axis points: an open axis tube opens into a donut hole under subdivision
                bm = bmesh.new(); bm.from_mesh(L.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
                axis = [e for e in bm.edges if all(abs(v.co.x) < 1e-5 and abs(v.co.y) < 1e-5 for v in e.verts)]
                bmesh.ops.delete(bm, geom=axis, context='EDGES'); bm.to_mesh(L.data); bm.free()
            K.subsurf(L, 1); K.apply_mods(L); K.smooth(L); L.data.materials.append(liq_mat); objs.append(L)
    # round-bottom flask (sits on a straw ring in compose), green
    rf = [(0.003, 0.0)] + [(0.065 * math.sin(math.radians(a)), 0.065 - 0.065 * math.cos(math.radians(a))) for a in range(10, 171, 10)] + [(0.016, 0.135), (0.014, 0.19), (0.017, 0.196)]
    G13 = os.environ.get('AAA_GLASS') == '13'   # g13: each potion vessel gets its own glass with a dried tide line just above its fill
    TD = (lambda nm, c, z: M2.glass(nm, tide=(c, z))) if G13 else (lambda nm, c, z: None)
    vessel('Flask', rf, 0.0022, 48, (0, 0, 0.008), True, 0.075, green, TD('M_GlassFlask', (0.10, 0.13, 0.05), 0.081))
    # tall bottle with red
    tb = [(0.003, 0.0), (0.035, 0.0), (0.038, 0.01), (0.038, 0.12), (0.03, 0.15), (0.012, 0.17), (0.011, 0.205), (0.014, 0.21)]
    if os.environ.get('AAA_GLASS') == '15':   # g15: eased shoulder ABOVE the full straight wall (g14 lost: its shoulder started at 0.10 and shortened the tall window streak reviewers credit)
        sh = [(0.012 + (0.038 - 0.012) * (0.5 + 0.5 * math.cos(math.pi * k / 10)), 0.12 + 0.05 * k / 10) for k in range(11)]
        tb = [(0.003, 0.0), (0.035, 0.0), (0.038, 0.01), (0.038, 0.11)] + sh + [(0.0112, 0.19), (0.011, 0.205), (0.014, 0.21)]
    if os.environ.get('AAA_GLASS') == '14':   # g14: eased shoulder (12 points), not a 2-segment kink that reads 'faceted, polygonal' (g12/g13 reviewers)
        sh = [(0.012 + (0.038 - 0.012) * (0.5 + 0.5 * math.cos(math.pi * k / 11)), 0.12 + 0.052 * k / 11) for k in range(12)]
        tb = [(0.003, 0.0), (0.035, 0.0), (0.038, 0.01), (0.038, 0.1)] + sh + [(0.0112, 0.19), (0.011, 0.205), (0.014, 0.21)]
    vessel('Bottle', tb, 0.002, 40, (0.16, 0.05, 0), True, 0.10, red, TD('M_GlassBottle', (0.12, 0.04, 0.025), 0.106))
    # small vial with amber
    vb = [(0.003, 0.0), (0.012, 0.0), (0.013, 0.005), (0.013, 0.07), (0.009, 0.078), (0.009, 0.09), (0.011, 0.093)]
    vessel('Vial', vb, 0.0012, 24, (0.10, -0.06, 0), True, 0.05, amber, TD('M_GlassVial', (0.16, 0.09, 0.03), 0.055))
    # receiver flask for the still (empty, a little distillate)
    rv = [(0.003, 0.0), (0.04, 0.0), (0.06, 0.03), (0.062, 0.06), (0.045, 0.1), (0.018, 0.12), (0.016, 0.16), (0.02, 0.165)]
    glr = (M2.glass3 if G3 else M2.glass2 if G2 else M2.glass)('M_GlassReceiver', dirty=0.8, dust=(0.25 if G2 else 0.9), residue=((0.16, 0.09, 0.03), 0.035))
    vessel('Receiver', rv, 0.002, 48, (0.32, -0.02, 0), True, 0.012, amber, glr)
    if G2:   # transparent glass casts no shadow in the engine (transparent queue); here it would black out the liquid
        for o in objs:
            if not o.name.endswith('_liq'): o.visible_shadow = False
    K.save_only(os.path.join(PDIR, 'Glass.blend'), objs); K.reset()

PROPS.update(still=still, herbs=herbs, glassware=glassware)

def ring():
    """Straw/twine coil ring that a round-bottom flask sits in."""
    tw = M2.twine('M_RingTwine', col=(0.26, 0.19, 0.09))
    pts = []
    for i in range(160):
        t = i / 159 * 3.0 * 2 * math.pi
        r = 0.034 + 0.0015 * math.sin(t * 7)
        pts.append((r * math.cos(t), r * math.sin(t), 0.004 + 0.0055 * (t / (2 * math.pi)) / 3.0 * 2 + 0.001 * math.sin(t * 3)))
    coil = K.tube('coil', pts, 0.0042, 10)
    parts = [K.make_part(coil, tw, pid(), 0.0007, wear(0.0003, 120, seed=41.0), fillet=1)]
    finish('Ring', parts, 5000, 1024)
PROPS.update(ring=ring)

def cork_for(name, r_top, r_bot, h):
    def f():
        ck = M2.cork('M_' + name)
        c = solid_lathe(name.lower(), [(0.004, 0.0), (r_bot, 0.0), (r_top, h * 0.7), (r_top * 1.08, h), (0.004, h * 1.02)], None, 24, step=0.001)
        finish(name, [K.make_part(c, ck, pid(), 0.0005, wear(0.0004, 150, chips=3, chip_r=(0.0015, 0.003), seed=len(name)))], 1500, 512)
    return f
PROPS.update(corkb=cork_for('CorkBottle', 0.0125, 0.009, 0.022), corkv=cork_for('CorkVial', 0.0095, 0.007, 0.016))
# global grime per prop: soot direction points (in prop-local space) at the brazier as compose places it
GRIME = {
    '_': dict(contact=1.0, dust=0.6),
    'mortar': dict(contact=1.0, dust=0.5, soot=(0.45, (1.0, 0.0, 0.15)), grease=(0.6, 0.06, 0.11)),
    'jug': dict(contact=1.0, dust=0.7, soot=(0.35, (0.957, 0.288, 0.1)), grease=(0.7, 0.2, 0.28)),
    'jar': dict(contact=1.0, dust=0.8, soot=(0.25, (0.6, -0.8, 0.1))),
    'herbs': dict(dust=0.5, soot=(0.25, (0.813, 0.582, 0.1))),
    'ring': dict(contact=1.0, dust=0.6, soot=(0.5, (0.934, -0.357, 0.1))),
    'still': dict(contact=1.0, dust=0.25),
    'candle': dict(contact=1.0, dust=0.3),
    'book': dict(dust=0.5, grease=(0.5, 0.0, 0.05)),
}
for n, f in PROPS.items():
    if ONLY and n not in ONLY: continue
    K.reset(); PID[0] = 0; K.GRIME = GRIME.get(n, GRIME['_']); f(); print('BUILT', n)
