"""FlagRing: B6 flagstone ring around the Hollowmere dry fountain (T3 pieces, one unique bake; PHASE1 4).

A 1.2 m ring of big storybook flags around the basin (basin outer diameter 3.4 m at the origin, so r 1.7 to 2.9 m):
two courses, 14 inner flags (0.53 m deep) and 18 outer flags (0.64 m), joints staggered, every flag a little off
in width, angle and level. The ring falls with the plaza's 5 cm dish (sc_plaza_common.dish) toward the basin foot,
where the old overflow ran.
Wear story (each visible at the mid camera): foot-worn flags on the approach sides (south toward the capture cameras
and east toward the stall) are dished, rounded and polished paler; the unworn north and west flags keep crisp
spalled arrises, lichen and dirt in the joints; a dark damp tide band with a green algae stain hugs the basin foot on
the shaded north side; one outer flag is cracked through, one has sunk and tipped.
Material: weathered granite a shade warmer than the cobbles (lights #b6ab9c, darks #7c736d), so the ring reads as a
separate dressed course between the cobbles (#817a7b-#b7b0aa) and the sandstone basin (B10).

Authored in Blender axes (z up, Unity north = -Y), origin = fountain centre at Unity y 0 (export -Z forward, Y up).
One mesh: low = per-flag decimated highs (about 12k tris for the ring), unique bake BaseColor, Normal, Roughness,
AO (PHASE1 3.3: ground at 1024 px/m needs 4K for the ring's 17 m2; cloud proof renders bake at 1024).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_flag_ring.py -- --out <scratch>
      [--stage look|bake] [--res 1024] [--tris 12000] [--views wide,mid,close] [--rres 960] [--samples 48]
      [--mood midday|lowsun] [--no-render] [--export]
  The whole ground in one render: sc_cobbles.py --ring.
Cloud sessions commit this generator only; the PC runs --stage bake --res 4096 --export.

Status (2026-10-30, cloud): look locked with the plaza at 28/40 (scored together, see sc_cobbles.py). Next: the PC
runs --stage bake --res 4096 --export.
Round log: r1 smooth pale slabs with invisible joints -> r2 dirt bed under the ring, per-flag tone -> r3 darker,
lumpier, mossy apron all round (darkest at the basin foot) -> r4 bigger spalls on quiet flags, polished approach
flags -> r6 grime band creeping in from every joint, weathering crust patches on the quiet flags.
"""
import sys, os, math, zlib, random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'tools')); sys.path.insert(0, HERE)
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
import sc_aaa_kit as K
import sc_plaza_common as P

SEED = 6062
R0, R1, R2 = P.BASIN_R + 0.006, P.BASIN_R + 0.54, P.RING_R     # inner edge, course split, outer edge
JOINT = 0.020
THICK = 0.11

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode()))

def traffic(theta_u):
    """0..1 foot traffic by angle round the basin (Unity angle atan2(z, x)): the south approach (toward the cameras)
    and the east side (toward the stall) are worn; the north and west are quiet."""
    south = math.exp(-((math.atan2(math.sin(theta_u + math.pi / 2), math.cos(theta_u + math.pi / 2))) / 0.75) ** 2)
    east = math.exp(-((math.atan2(math.sin(theta_u - 0.35), math.cos(theta_u - 0.35))) / 0.6) ** 2)
    return min(1.0, south + 0.8 * east)

def flag_mat(name='M_FlagRing'):
    m, T = K.new_mat(name); T.grime = None
    p = (T.node('ShaderNodeTexCoord'), 'Object'); s = T.sep(p)
    wear = T.attr('wear', vec=False)
    mott = T.noise(p, scale=4.0, detail=6.0, rough=0.62); fine = T.noise(p, scale=55.0, detail=3.0)
    lo, hi = (0.105, 0.096, 0.088), (0.29, 0.26, 0.225)
    col = T.ramp(T.math('ADD', T.math('MULTIPLY', mott, 0.8), T.math('MULTIPLY', fine, 0.25)), [(0.32, lo), (0.55, (0.22, 0.20, 0.18)), (0.78, hi)])
    tone = T.attr('tone', vec=False)                    # per-flag tone: each flag cut from a different block
    col = T.hsv(col, T.math('ADD', 0.5, T.math('MULTIPLY', T.math('SUBTRACT', tone, 0.5), 0.03)), 1.0, T.math('ADD', 0.70, T.math('MULTIPLY', tone, 0.60)))
    vf = T.node('ShaderNodeTexVoronoi'); T.set(vf, 'Vector', p); vf.inputs['Scale'].default_value = 90.0
    speck = T.math('MULTIPLY', T.smoothstep((vf, 'Distance'), 0.30, 0.14), T.smoothstep((T.sep((vf, 'Color')), 'X'), 0.6, 0.7))
    col = T.mix(T.math('MULTIPLY', speck, 0.45), col, (0.55, 0.51, 0.46))
    # foot polish on worn flags: paler, smoother, the grain softened
    top = T.up(0.8, 0.97)
    pol = T.math('MULTIPLY', wear, top)
    col = T.mix(T.math('MULTIPLY', pol, 0.7), col, T.hsv(col, 0.5, 0.8, 1.35))
    ed = T.edge(0.006, 0.03, 0.25)
    col = T.mix(T.math('MULTIPLY', ed, 0.3), col, T.hsv(col, 0.5, 0.9, 1.2))
    cav = T.cavity(0.02, 0.35, 0.95)
    col = T.mix(T.math('MULTIPLY', T.math('SUBTRACT', 1.0, cav), 0.8), col, (0.07, 0.058, 0.045))
    # grime creeps 3-6 cm in from every joint (broad cavity), and dark weathering crust in patches on the quiet flags
    jg = T.math('SUBTRACT', 1.0, T.cavity(0.09, 0.55, 0.98))
    col = T.mix(T.math('MULTIPLY', jg, 0.55), col, T.hsv(col, 0.5, 1.05, 0.5))
    crust = T.math('MULTIPLY', T.smoothstep(T.noise(p, scale=2.2, detail=5.0, rough=0.65), 0.5, 0.68), T.math('SUBTRACT', 1.0, wear))
    col = T.mix(T.math('MULTIPLY', crust, 0.55), col, T.hsv(col, 0.52, 1.1, 0.62))
    # lichen on the quiet flags: pale grey-ochre crusty rosettes
    lv = T.node('ShaderNodeTexVoronoi'); T.set(lv, 'Vector', p); lv.inputs['Scale'].default_value = 9.0
    lich = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep((lv, 'Distance'), 0.30, 0.12), T.smoothstep((T.sep((lv, 'Color')), 'Y'), 0.78, 0.84)),
                  T.math('SUBTRACT', 1.0, wear))
    lich = T.math('MULTIPLY', lich, T.smoothstep(fine, 0.35, 0.55))
    col = T.mix(T.math('MULTIPLY', lich, 0.8), col, (0.40, 0.39, 0.26))
    # damp tide band + algae at the basin foot, strongest on the shaded north side (Unity +z = Blender -y)
    rr = T.vmath('LENGTH', T.comb((s, 'X'), (s, 'Y'), 0.0))
    north = T.smoothstep(T.math('MULTIPLY', (s, 'Y'), -1.0), -0.8, 1.6)
    band = T.math('MULTIPLY', T.smoothstep(T.math('ADD', rr, T.math('MULTIPLY', T.math('SUBTRACT', mott, 0.5), 0.25)), R0 + 0.42, R0 + 0.04), T.math('ADD', 0.6, T.math('MULTIPLY', north, 0.4)))
    col = T.mix(T.math('MULTIPLY', band, 0.75), col, T.hsv(col, 0.5, 1.0, 0.45))
    alg = T.math('MULTIPLY', T.math('MULTIPLY', band, T.math('ADD', 0.45, T.math('MULTIPLY', north, 0.55))), T.smoothstep(T.noise(p, scale=14.0, detail=4.0), 0.38, 0.58))
    col = T.mix(T.math('MULTIPLY', alg, 0.75), col, (0.085, 0.105, 0.035))
    rough = T.math('ADD', 0.78, T.math('MULTIPLY', T.math('SUBTRACT', fine, 0.5), 0.1))
    rough = T.mixf(pol, rough, 0.30)
    rough = T.mixf(T.math('MULTIPLY', band, 0.6), rough, 0.5)
    rough = T.mixf(T.math('SUBTRACT', 1.0, cav), rough, 0.92)
    h = T.math('ADD', T.math('MULTIPLY', T.noise(p, scale=260.0, detail=3.0), T.math('SUBTRACT', 1.0, T.math('MULTIPLY', wear, 0.7))), T.math('MULTIPLY', speck, 0.3))
    T.output(col, rough, 0.0, T.bump(h, 0.35, 0.0012))
    return m

def flag_solid(name, ra, rb, a0, a1, z_off=0.0, tilt=(0.0, 0.0)):
    """Annular-sector slab (Blender angles), top following the dish, THICK deep."""
    nr = 8; na = max(6, int((a1 - a0) * rb / 0.035))
    bm = bmesh.new(); top, bot = {}, {}
    ac = (a0 + a1) / 2; rc = (ra + rb) / 2
    cx, cy = rc * math.cos(ac), rc * math.sin(ac)
    for i in range(nr + 1):
        r = ra + (rb - ra) * i / nr
        for j in range(na + 1):
            a = a0 + (a1 - a0) * j / na
            x, y = r * math.cos(a), r * math.sin(a)
            z = P.dish(r) + z_off + tilt[0] * (x - cx) + tilt[1] * (y - cy)
            top[i, j] = bm.verts.new((x, y, z)); bot[i, j] = bm.verts.new((x, y, z - THICK))
    for i in range(nr):
        for j in range(na):
            bm.faces.new((top[i, j], top[i + 1, j], top[i + 1, j + 1], top[i, j + 1]))
            bm.faces.new((bot[i, j], bot[i, j + 1], bot[i + 1, j + 1], bot[i + 1, j]))
    for j in range(na):
        bm.faces.new((top[0, j], top[0, j + 1], bot[0, j + 1], bot[0, j])); bm.faces.new((top[nr, j], bot[nr, j], bot[nr, j + 1], top[nr, j + 1]))
    for i in range(nr):
        bm.faces.new((top[i, 0], bot[i, 0], bot[i + 1, 0], top[i + 1, 0])); bm.faces.new((top[i, na], top[i + 1, na], bot[i + 1, na], bot[i, na]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K._obj_from_bm(name, bm)

def ring_bed():
    """Dirt bed under the flags so the joints read dark (render only: in Unity the PlazaBed and the basin cover it)."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); cl = bm.loops.layers.color.new('damp'); n = 192; rs = []
    for ri, rr in enumerate((R0 - 0.02, R1, R2 + 0.06)):
        rs.append([bm.verts.new((rr * math.cos(2 * math.pi * k / n), rr * math.sin(2 * math.pi * k / n), P.dish(rr) - 0.03)) for k in range(n)])
    for a_, b_ in zip(rs[:-1], rs[1:]):
        for k in range(n):
            f = bm.faces.new((a_[k], a_[(k + 1) % n], b_[(k + 1) % n], b_[k]))
            for lp in f.loops:
                lp[uvl].uv = (-lp.vert.co.x / 2, lp.vert.co.y / 2); lp[cl] = (0.9, 0.0, 0.0, 1)
    ob = K._obj_from_bm('RingBed', bm); ob.data.materials.append(P.bed_material('M_CobbleBed'))
    if ob.data.polygons[0].normal.z < 0: ob.data.flip_normals()
    return ob

def build_ring(look=True):
    r = rng('ring'); highs = []
    mat = flag_mat()
    courses = [(R0, R1 - JOINT / 2, 14, 0.0), (R1 + JOINT / 2, R2, 18, 0.11)]
    crack_k, sink_k = None, None
    for ci, (ra, rb, n, off) in enumerate(courses):
        w = [r.uniform(0.85, 1.15) for _ in range(n)]; tot = sum(w)
        a = off + r.uniform(-0.03, 0.03)
        for k in range(n):
            da = 2 * math.pi * w[k] / tot; jg = JOINT / ((ra + rb) / 2)
            a0, a1 = a + jg / 2, a + da - jg / 2; a += da
            ac = (a0 + a1) / 2; theta_u = math.atan2(-math.sin(ac), -math.cos(ac))     # Blender angle -> Unity angle
            tw = traffic(theta_u) * (1.0 if ci == 0 else 0.75)
            name = f'flag_{ci}_{k:02d}'
            north = math.sin(theta_u) > 0.55
            z_off = r.gauss(0, 0.0025); tilt = (r.gauss(0, 0.004), r.gauss(0, 0.004))
            if ci == 1 and north and sink_k is None and k % 2 == 0:
                sink_k = name; z_off -= 0.018; tilt = (0.035 * math.cos(ac), 0.035 * math.sin(ac))
            ob = flag_solid(name, ra + r.uniform(0, 0.006), rb - r.uniform(0, 0.006), a0, a1, z_off, tilt)
            K.bevel(ob, 0.016 + 0.020 * tw, 4, harden=False); K.apply_mods(ob)
            K.remesh(ob, 0.0065)
            co, no = K.mesh_arrays(ob)
            topw = K._sstep(co[:, 2] - np.array([P.dish(math.hypot(x, y)) for x, y in co[:, :2]]), -0.05, -0.006)
            sd = r.random() * 10
            d = (0.0045 * (1 - tw) + 0.0012) * (K.fbm(co, 7.0, 4, 0.55, sd) - 0.5) * 2
            d += 0.0008 * (K.fbm(co, 40.0, 3, 0.5, sd + 1) - 0.5) * 2 * (1 - 0.7 * tw)
            # foot dish: worn flags are hollowed a few mm in the middle of the tread
            rr = np.hypot(co[:, 0], co[:, 1]); aa = np.arctan2(co[:, 1], co[:, 0])
            u = np.clip((rr - ra) / (rb - ra), 0, 1); v = np.clip((np.angle(np.exp(1j * (aa - ac)))) / ((a1 - a0) / 2), -1, 1)
            d += 0.006 * tw * np.clip(1 - (2 * u - 1) ** 2, 0, 1) * np.clip(1 - v ** 2, 0, 1) * topw
            nchip = int(round(11 * (1 - tw) + 2))
            d += K.scoops(ob, nchip, 0.018, 0.05, 0.75, (35.0, 160.0), seed=sd + 3, co=co, no=no, flake=True) * topw
            co = co - no * d[:, None]
            if ci == 1 and north and crack_k is None and name != sink_k:
                crack_k = name                     # a crack right through, across the flag
                ang = ac + r.uniform(-0.4, 0.4); n2 = np.array([-math.sin(ang), math.cos(ang), 0.0])
                c0 = np.array([math.cos(ac), math.sin(ac), 0.0]) * ((ra + rb) / 2)
                dist = np.abs(((co - c0) * n2).sum(1) + 0.012 * (K.fbm(co, 18.0, 3, 0.5, 4.0) - 0.5) * 2)
                co[:, 2] -= 0.010 * np.clip(1 - dist / 0.004, 0, 1) * topw
            K.set_co(ob, co); K.smooth(ob)
            wa = ob.data.attributes.new('wear', 'FLOAT', 'POINT')
            wa.data.foreach_set('value', (tw * np.clip(0.6 + 0.4 * (1 - u), 0, 1)).astype(np.float32))
            ta = ob.data.attributes.new('tone', 'FLOAT', 'POINT'); ta.data.foreach_set('value', [r.random()] * len(ob.data.vertices))
            ob.data.materials.append(mat)
            highs.append(ob)
    if look: ring_bed()
    print('RING flags', len(highs), 'crack', crack_k, 'sunk', sink_k, 'high tris', sum(K.tris(h) for h in highs))
    return highs

if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    def arg(k, d=None):
        return argv[argv.index(k) + 1] if k in argv else d
    OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
    STAGE = arg('--stage', 'look'); RES = int(arg('--res', '1024'))
    K.reset(); sc = bpy.context.scene
    highs = build_ring()
    if STAGE == 'bake':
        low = K.bake_prop('FlagRing', highs, OUT, target=int(arg('--tris', '12000')), res=RES, samples=32, extrusion=0.008, ray=0.02)
        print('LOW TRIS', K.tris(low))
        if '--export' in argv:
            for o in bpy.context.selected_objects: o.select_set(False)
            low.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'FlagRing.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                                     apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='FACE')
    if '--no-render' not in argv:
        P.standins()
        P.render_cams(sc, OUT, f'FlagRing_{STAGE}', arg('--views', 'wide,mid,close').split(','), int(arg('--rres', '960')), int(arg('--samples', '48')), arg('--mood', 'midday'))
