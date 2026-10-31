"""Cobbles: B6 sculpted granite cobble family (T3, PHASE1 4) and the Hollowmere plaza patch laid from it.

The family: 8 granite setts, 3 damp setts (darker, glossy, algae in the crevices: the dish around the ring), (chunky storybook size, 15-20 x 12-15 cm tops: about 1.3x the 11-13 cm setts of Alsace
and Rothenburg squares) plus 3 rubble fragments, each a dense damaged high (voxel remesh, domed and foot-worn tops,
square-arris spalls, one hairline crack) baked once into one shared set (BaseColor, Normal, Roughness, AO; 2K on the
PC). Two of the setts are the "path" variants: flatter, polished, a little paler, used where feet go. Lows are the
decimated highs with the buried underside cut away: LOD0 about 140 tris, LOD1 about 44 tris (setts more than 12 m from
the wide camera), rubble about 120.

The patch (brief section 1: x -8..8, z -6..8 Unity, extended to x -12 on 2026-10-31 so the wide camera never sees its west edge): fan/arc courses (2.0 m fans, offset
half a fan per 1.0 m row) laid by an occupancy raster, three concentric courses hugging the flagstone ring (r 2.9 m,
sc_flag_ring.py), a gutter course along the facade foot (z 7.45), small setts filling the leftovers. Heights follow
the 5 cm dish toward the fountain (sc_plaza_common.dish) plus a slow 6 mm undulation; clustered sunken and tilted
setts; missing setts (the dirt bed shows, a weed slot, and sometimes the dislodged sett lies beside its hole). Under
the setts a joint bed mesh (0.2 m grid, UV in 2 m tiles, vertex colour `damp`) (R damp/moss, G worn-path dust) carries a tileable dirt-and-grit set
(CobbleBed, 2 m, 2K) that darkens and greens toward the fountain and the facade foot.
Wear story: a worn, paler path from the south edge round the fountain to the stall and the facade door; damp,
mossy joints near the basin and the facade's shaded foot; sunken setts in the low dish around the ring.

Outputs (Unity metres, Unity axes): cobble_placements.json = every sett and loose stone {v, lod, p, q (x,y,z,w), s,
seen} plus weed_slots [{p, kind}] for the foliage session (B9) and stats. Instances share 11 meshes (GPU Resident
Drawer batches them); `seen` marks stones inside any capture frame (brief section 2: only those need be spent on).
Authoring: Blender axes (z up); placements are converted with sc_plaza_common.b2u / rot_b2u_quat, which assume the
project FBX convention (-Z forward, Y up, so Unity = (-x, z, -y)). The controller's builder should check one sett.

refs: Reference/hollowmere/market_square/REFS.md, Reference/hollowmere/materials/REFS.md (fan-laid granite setts:
Segmentbogenpflaster, arcs about 2 m wide; joints 1-2 cm filled with grit; foot-polished paths); palette PHASE1 3.1
cobble #817a7b to #b7b0aa with warm lights.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_cobbles.py -- --out <scratch>
      [--stage look|bake]   look (default): highs instanced with their procedural material, no bake (fast iteration);
                            bake: highs -> lows -> one baked set, LOD0/LOD1 instanced with the baked game material
      [--res 1024]          family and bed bake resolution (the PC's final bake: --res 2048)
      [--views wide,mid,close,ground] [--rres 960] [--samples 48] [--mood midday|lowsun] [--ring] [--no-render]
      [--export]            write CobbleFamily.fbx and PlazaBed.fbx to --out (PC only; cloud sessions commit the generator)
  --ring builds the flagstone ring (sc_flag_ring.py, look stage) into the renders.
Budget (PHASE1 3.3, T3): 50-400 tris per variant, one 2K set per family (+ the 2K bed tileable), 1024 px/m or more.

Status (2026-10-31, cloud): look at 29/40 (sc-asset-review rubric, scored on the brief's three cameras plus a low
ground check; first scored round 26, target 31 under the "+5" rule; stopped when r8 gave no visible gain).
Bake proof (--stage bake --res 1024, r6 code): LOD0 139-140 tris, LOD1 43-44, frags 118-120; 6,732 setts + 45 loose,
636k instanced tris (6,094 inside a capture frame); bed 9,820 tris. The baked lows hold the look-stage read.
Next: the PC runs --stage bake --res 2048 --export and the controller's builder instances the placements.
Round log (look stage, ground with the flag ring and stand-ins):
  r1-r2 (unscored setup): fan layout + fill pass; setts were pillow-soft, pale, polka-dot feldspar, joints too pale.
  r3 26/40: blocky setts (bevel 8-14 mm), per-variant granite tones (blue-grey to pink), green moss in the joints.
  r4 26.5: warmer sun / cooler fill, joints 3.4 cm deep, dusty moss-free joints on the worn path.
  r5 27.5: narrow tone band inside each stone, wide between stones (tone now changes stone to stone), wider joints.
  r6 28: (flag ring pass, sc_flag_ring.py).
  r7 29: fixed a crease + centre dimple on every sett top (sign() in the plan bulge); 3 damp setts in the dish.
  r8 29: rim dirt band, crown pitting, stronger grain: no visible gain at the mid camera, so the loop stopped.
  r9 (Unity feedback, 2026-10-31): wider tone spread between variants (k = 1 + 2.3 tint), dirt stain up to 3 mm
     under the crown, darker joint bed, ~1.6x crown relief; patch extended to x -12 (8,673 setts) so the wide
     camera never sees the west seam. Wide-camera ground value spread 19 -> 22 (p10 0.44 -> 0.41).
  The `damp` bed vertex colour is not read by the Unity shader yet: the CobbleBed tileable carries base moss alone.
  Parked ideas: docs/PARKING_LOT.md (B6 plaza ground).
"""
import sys, os, math, json, zlib, random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'tools')); sys.path.insert(0, HERE)
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
import sc_aaa_kit as K
import sc_plaza_common as P

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
STAGE = arg('--stage', 'look'); RES = int(arg('--res', '1024')); RRES = int(arg('--rres', '960'))
SAMPLES = int(arg('--samples', '48')); MOOD = arg('--mood', 'midday')
VIEWS = arg('--views', 'wide,mid,close,ground').split(',')
SEED = 6061

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode()))

# ----------------------------------------------------------------------------- family definition
#  name, top length L, top width W, body height H, kind, params
FAMILY = [   # tint: value offset (-0.3..+0.3), warm: -1 blue-grey .. +1 pink-tan (granite blocks differ: the refs' strongest cue)
    ('sett_a', 0.180, 0.140, 0.10, 'sett', dict(dome=0.004, bev=0.010, tint=0.00, warm=0.2)),
    ('sett_b', 0.195, 0.130, 0.10, 'sett', dict(dome=0.005, bev=0.009, tint=-0.26, warm=-0.9)),    # dark blue-grey
    ('sett_c', 0.160, 0.145, 0.10, 'sett', dict(dome=0.006, bev=0.013, tint=0.14, warm=1.0)),      # pale pink granite
    ('sett_d', 0.175, 0.135, 0.10, 'wedge', dict(dome=0.004, bev=0.010, tint=-0.06, warm=0.5, taper=0.16)),
    ('sett_e', 0.185, 0.140, 0.10, 'cracked', dict(dome=0.003, bev=0.009, tint=-0.18, warm=0.1)),
    ('sett_f', 0.170, 0.130, 0.10, 'chipped', dict(dome=0.005, bev=0.008, tint=0.20, warm=-0.4)),  # pale cool grey
    ('path_a', 0.185, 0.140, 0.10, 'worn', dict(dome=0.002, bev=0.013, tint=0.16, warm=0.6)),
    ('path_b', 0.165, 0.135, 0.10, 'worn', dict(dome=0.002, bev=0.014, tint=0.06, warm=-0.2)),
    ('damp_a', 0.180, 0.135, 0.10, 'sett', dict(dome=0.004, bev=0.010, tint=-0.20, warm=0.3, damp=1)),   # the low dish by the ring
    ('damp_b', 0.170, 0.140, 0.10, 'sett', dict(dome=0.005, bev=0.011, tint=-0.30, warm=-0.5, damp=1)),
    ('damp_c', 0.190, 0.130, 0.10, 'chipped', dict(dome=0.004, bev=0.009, tint=-0.12, warm=0.8, damp=1)),
    ('small_a', 0.120, 0.105, 0.09, 'sett', dict(dome=0.003, bev=0.009, tint=-0.10, warm=0.7)),
    ('frag_a', 0.120, 0.090, 0.060, 'frag', dict(tint=-0.12, warm=0.3)),
    ('frag_b', 0.090, 0.075, 0.050, 'frag', dict(tint=0.10, warm=0.8)),
]
VAR = {f[0]: f for f in FAMILY}
SETTS = ['sett_a', 'sett_b', 'sett_c', 'sett_d', 'sett_e', 'sett_f']
PATHS = ['path_a', 'path_b']
DAMPS = ['damp_a', 'damp_b', 'damp_c']
JOINT = 0.015            # nominal joint (m)
BURY = 0.045             # the lows keep this much body below the top; the rest is underground
LOD0_T, LOD1_T, FRAG_T = 140, 44, 120

# ----------------------------------------------------------------------------- granite smart material
def granite(name, tint=0.0, warm=0.4, worn=False, damp=False, L=0.18, W=0.14):
    """Granite: grey with warm lights (#817a7b to #b7b0aa), black mica and pale feldspar specks, foot-polished top,
    dirt below the joint line and in crevices. Object space, so instances and the bake agree."""
    m, T = K.new_mat(name); T.grime = None
    p = (T.node('ShaderNodeTexCoord'), 'Object')
    z = (T.sep(p), 'Z')
    mott = T.noise(p, scale=9.0, detail=5.0, rough=0.6)
    big = T.noise(p, scale=2.5, detail=2.0)
    # wide value spread between stones: the only per-stone value cue in Unity
    k = 1.0 + 2.3 * tint; hue = (1.0 + 0.10 * warm, 1.0 + 0.01 * warm, 1.0 - 0.10 * warm)
    lo = tuple(c * k * h for c, h in zip((0.175, 0.161, 0.163), hue))          # within one stone: a narrow band, so the stones differ
    hi = tuple(c * k * h for c, h in zip((0.295, 0.27, 0.25), hue))
    col = T.ramp(T.math('ADD', T.math('MULTIPLY', mott, 0.75), T.math('MULTIPLY', big, 0.35)),
                 [(0.30, lo), (0.52, tuple((a + b) / 2 for a, b in zip(lo, hi))), (0.72, hi)])
    # specks: feldspar (pale, a little warm) and mica (near black, glossy)
    vf = T.node('ShaderNodeTexVoronoi'); T.set(vf, 'Vector', p); vf.inputs['Scale'].default_value = 150.0
    felds = T.math('MULTIPLY', T.smoothstep((vf, 'Distance'), 0.36, 0.20), T.smoothstep((T.sep((vf, 'Color')), 'X'), 0.62, 0.70))
    vm = T.node('ShaderNodeTexVoronoi'); T.set(vm, 'Vector', T.vmath('ADD', p, (3.1, 1.7, 0.4))); vm.inputs['Scale'].default_value = 220.0
    mica = T.math('MULTIPLY', T.smoothstep((vm, 'Distance'), 0.26, 0.12), T.smoothstep((T.sep((vm, 'Color')), 'X'), 0.62, 0.68))
    col = T.mix(T.math('MULTIPLY', felds, 0.3), col, (0.46, 0.43, 0.39))
    col = T.mix(T.math('MULTIPLY', mica, 0.5), col, (0.05, 0.048, 0.052))
    # foot polish: the top faces are paler and smoother; worn variants more so
    up = T.up(0.75, 0.97)
    top = T.math('MULTIPLY', up, T.smoothstep(z, -0.012, -0.002))
    pol = T.math('MULTIPLY', top, 0.85 if worn else 0.45)
    col = T.mix(T.math('MULTIPLY', pol, 0.35), col, T.hsv(col, 0.5, 0.9, 1.12))
    # dirt creeps 1-3 cm in from each stone's rim; feet polish the centre (the strongest per-stone cue at mid distance)
    sx = T.sep(p)
    rim = T.math('MAXIMUM', T.math('ABSOLUTE', T.math('DIVIDE', (sx, 'X'), L / 2)), T.math('ABSOLUTE', T.math('DIVIDE', (sx, 'Y'), W / 2)))
    rimd = T.smoothstep(T.math('ADD', rim, T.math('MULTIPLY', T.math('SUBTRACT', mott, 0.5), 0.25)), 0.62, 0.98)
    col = T.mix(T.math('MULTIPLY', rimd, 0.32 if worn else 0.45), col, T.hsv(col, 0.5, 1.0, 0.62))
    # crisp convex edges catch a little light; crevices hold dirt
    ed = T.edge(0.004, 0.03, 0.25)
    col = T.mix(T.math('MULTIPLY', ed, 0.25), col, T.hsv(col, 0.5, 0.9, 1.15))
    cav = T.cavity(0.012, 0.35, 0.95)
    dirt = (0.085, 0.070, 0.055)
    col = T.mix(T.math('MULTIPLY', T.math('SUBTRACT', 1.0, cav), 0.75), col, dirt)
    # the body below the joint line sits in the grit bed: dirt-stained, matte
    bed = T.smoothstep(T.math('ADD', z, T.math('MULTIPLY', T.math('SUBTRACT', mott, 0.5), 0.012)), -0.003, -0.020)     # dirt stain rises to 3 mm under the crown: joints read wide and dark at 5-10 m
    col = T.mix(T.math('MULTIPLY', bed, 0.85), col, (0.11, 0.092, 0.072))
    # sparse pale lichen dots on the unworn setts, in the micro hollows of the top
    if not worn:
        lv = T.node('ShaderNodeTexVoronoi'); T.set(lv, 'Vector', p); lv.inputs['Scale'].default_value = 22.0
        lich = T.math('MULTIPLY', T.smoothstep((lv, 'Distance'), 0.22, 0.10), T.smoothstep((T.sep((lv, 'Color')), 'Y'), 0.86, 0.90))
        col = T.mix(T.math('MULTIPLY', lich, 0.6), col, (0.42, 0.40, 0.25))
    if damp:                         # damp setts: darker, glossier, green algae in the crevices and the joint band
        alg = T.math('MAXIMUM', T.math('SUBTRACT', 1.0, cav), bed)
        col = T.mix(T.math('MULTIPLY', alg, 0.7), T.hsv(col, 0.5, 1.05, 0.78), (0.06, 0.08, 0.025))
    rough = T.math('ADD', 0.74, T.math('MULTIPLY', T.math('SUBTRACT', mott, 0.5), 0.12))
    rough = T.mixf(pol, rough, 0.42 if worn else 0.55)
    rough = T.mixf(T.math('MULTIPLY', mica, 0.7), rough, 0.32)
    rough = T.mixf(T.math('MAXIMUM', bed, T.math('SUBTRACT', 1.0, cav)), rough, 0.92)
    if damp: rough = T.math('MULTIPLY', rough, 0.62)
    # micro relief: crystal grain + feldspar bumps (the dents and spalls are geometry)
    grain = T.noise(p, scale=380.0, detail=3.0)
    h = T.math('ADD', T.math('MULTIPLY', grain, 0.5), T.math('MULTIPLY', felds, 0.35))
    nrm = T.bump(h, strength=0.3 if worn else 0.55, distance=0.0008)
    T.output(col, rough, 0.0, nrm)
    return m

# ----------------------------------------------------------------------------- highs
def _top_to_zero(ob):
    co, _ = K.mesh_arrays(ob); co[:, 2] -= co[:, 2].max(); K.set_co(ob, co)

def make_sett(name, L, W, H, kind, prm):
    r = rng(name)
    ob = K.box(name, (L, W, H), (0, 0, -H / 2), cuts=(4, 4, 3))
    if kind == 'wedge':
        tp = prm['taper']
        for v in ob.data.vertices: v.co.x *= 1.0 + tp * (v.co.y / W)
    K.bevel(ob, prm['bev'], 4, harden=False); K.apply_mods(ob)
    K.remesh(ob, 0.0032)
    co, no = K.mesh_arrays(ob)
    x, y, zz = co[:, 0] / (L / 2), co[:, 1] / (W / 2), co[:, 2]
    topw = K._sstep(zz, -0.05, -0.005)
    # irregular plan: the sides bulge and lean a little (hand-dressed, never a machined box)
    sd = r.random() * 10
    # (a smooth ramp, not sign(x): sign creased every top along x=0 / y=0 and pinched a dimple at the centre)
    co[:, 0] += 0.006 * (K.fbm(co + sd, 6.0, 2, 0.5, 1.0) - 0.5) * 2 * np.clip(x, -1, 1)
    co[:, 1] += 0.006 * (K.fbm(co + sd, 6.0, 2, 0.5, 2.0) - 0.5) * 2 * np.clip(y, -1, 1)
    # dome, then the worn crown: everything above the wear plane is squashed flat (foot traffic)
    dome = prm['dome'] * np.clip(1 - x ** 2, 0, 1) * np.clip(1 - y ** 2, 0, 1) * topw
    co[:, 2] += dome + 0.0040 * (K.fbm(co + sd, 11.0, 3, 0.5, 3.0) - 0.5) * 2 * topw
    zt = co[:, 2].max() - (0.0045 if kind == 'worn' else 0.0022)
    hi_ = co[:, 2] > zt
    co[hi_, 2] = zt + (co[hi_, 2] - zt) * (0.18 if kind == 'worn' else 0.4)
    K.set_co(ob, co)
    co, no = K.mesh_arrays(ob)
    # surface lumps (coarse pick-dressing, softened by wear on the path variants)
    amp = 0.0011 if kind == 'worn' else 0.0022
    d = amp * (K.fbm(co, 38.0, 3, 0.55, 4.0 + sd) - 0.5) * 2
    nchip = {'chipped': 9, 'worn': 2, 'cracked': 4}.get(kind, 5)
    d = d + K.scoops(ob, nchip, 0.008, 0.020, 0.55, (75.0, 170.0), seed=sd + 5.0, co=co, no=no, flake=True) * topw
    co = co - no * d[:, None]
    if kind != 'worn':                          # pick-dressed pitting on the crown: shallow 2-5 mm bites
        d2 = K.scoops(ob, 70, 0.002, 0.005, 0.35, (-1e9, 1e9), seed=sd + 7.0, co=co, no=no) * K._sstep(co[:, 2], co[:, 2].max() - 0.012, co[:, 2].max() - 0.003)
        co = co - no * d2[:, None]
    if kind == 'cracked':                       # hairline crack across the crown, open at the top
        ang = r.uniform(0.3, 1.2); n2 = np.array([math.cos(ang), math.sin(ang), 0.0])
        dist = np.abs((co * n2).sum(1) + 0.01 * (K.fbm(co, 25.0, 2, 0.5, 9.0) - 0.5))
        crack = np.clip(1 - dist / 0.0022, 0, 1) * K._sstep(co[:, 2], -0.04, -0.003)
        co[:, 2] -= 0.006 * crack
    K.set_co(ob, co)
    _top_to_zero(ob); K.smooth(ob)
    return ob

def make_frag(name, L, W, H, prm):
    """A broken cobble: a lumpy chunk split by two fracture planes (flat faces, crisp arrises)."""
    r = rng(name)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=3, radius=0.5)
    bmesh.ops.scale(bm, vec=(L, W, H * 1.6), verts=bm.verts)
    for k in range(2):
        a = r.uniform(0, 2 * math.pi); tilt = r.uniform(-0.4, 0.4)
        nrm = Vector((math.cos(a), math.sin(a), tilt)).normalized()
        g = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=g, plane_co=nrm * (L * r.uniform(0.12, 0.25)), plane_no=nrm, clear_outer=True)
        edges = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
        if edges: bmesh.ops.holes_fill(bm, edges=edges)
    g = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=g, plane_co=(0, 0, -H * 0.35), plane_no=(0, 0, -1), clear_outer=True)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = K._obj_from_bm(name, bm)
    K.remesh(ob, 0.003)
    co, no = K.mesh_arrays(ob)
    d = 0.0015 * (K.fbm(co, 30.0, 3, 0.55, r.random() * 9) - 0.5) * 2 + K.scoops(ob, 5, 0.006, 0.016, 0.5, (40.0, 160.0), seed=r.random() * 9, co=co, no=no, flake=True)
    K.set_co(ob, co - no * d[:, None])
    co, _ = K.mesh_arrays(ob); co[:, 2] -= co[:, 2].min(); K.set_co(ob, co)    # frags rest on the ground: origin at the bottom
    K.smooth(ob)
    return ob

def build_family():
    highs = {}
    for i, (name, L, W, H, kind, prm) in enumerate(FAMILY):
        ob = make_frag(name, L, W, H, prm) if kind == 'frag' else make_sett(name, L, W, H, kind, prm)
        ob.data.materials.append(granite('M_' + name, prm['tint'], prm['warm'], kind == 'worn', prm.get('damp', False), L, W))
        highs[name] = ob
    return highs

def make_low(high, target, cut=True):
    low = K.dup(high, high.name + '_low')
    if cut:
        bm = bmesh.new(); bm.from_mesh(low.data)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if all(v.co.z < -BURY for v in f.verts)], context='FACES')
        bm.to_mesh(low.data); bm.free()
    K.decimate_to(low, target); K.smooth(low)
    return low

def bake_family(highs):
    """All lows side by side -> one atlas -> one bake -> split back per variant. Returns {name: (lod0, lod1)}."""
    names = list(highs); lows = []
    for i, n in enumerate(names):
        off = Vector(((i % 4) * 0.45, (i // 4) * 0.45, 0))
        highs[n].location = off
        lo = make_low(highs[n], FRAG_T if VAR[n][4] == 'frag' else LOD0_T, cut=VAR[n][4] != 'frag')
        lo.location = off
        a = lo.data.attributes.new('var', 'INT', 'FACE'); a.data.foreach_set('value', [i] * len(lo.data.polygons))
        lows.append(lo)
    bpy.context.view_layer.update()
    for o in lows + list(highs.values()):
        o.data.transform(o.matrix_world); o['off'] = tuple(o.location); o.matrix_world = Matrix.Identity(4)
    low = K.join('CobbleFamily', lows)
    high = K.join('CobbleFamily_High', list(highs.values()))
    K.uv_unwrap(low, margin=0.004, angle=66)
    paths = K.bake_to_low(low, [high], os.path.join(OUT, 'tex'), 'Cobble', res=RES, samples=32, extrusion=0.009, ray=0.024, ao_dist=0.06)
    gm = K.game_material('M_Cobble', paths); K.apply_game_material(low, gm)
    bpy.data.objects.remove(high)
    out = {}
    for i, n in enumerate(names):
        o = K.dup(low, n + '_LOD0')
        bm = bmesh.new(); bm.from_mesh(o.data); lay = bm.faces.layers.int['var']
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[lay] != i], context='FACES')
        off = Vector(((i % 4) * 0.45, (i // 4) * 0.45, 0))
        bmesh.ops.translate(bm, vec=-off, verts=bm.verts); bm.to_mesh(o.data); bm.free()
        l1 = None
        if VAR[n][4] != 'frag':
            l1 = K.dup(o, n + '_LOD1'); K.decimate_to(l1, LOD1_T)
        out[n] = (o, l1)
        print('LOW', n, K.tris(o), K.tris(l1) if l1 else '-')
    bpy.data.objects.remove(low)
    return out, paths

# ----------------------------------------------------------------------------- layout (Unity XZ plane)
RES_G = 0.02
X0, X1, Z0, Z1 = P.PATCH_U
NX, NZ = int((X1 - X0) / RES_G), int((Z1 - Z0) / RES_G)
GX, GZ = np.meshgrid(X0 + (np.arange(NX) + 0.5) * RES_G, Z0 + (np.arange(NZ) + 0.5) * RES_G, indexing='ij')
FACADE = (-5.0, 2.5, 7.45)       # front face x0, x1, z (the setts stop at its plinth)
DOOR_U = (-1.3, 7.45)
STALL_U = (4.5, 4.5)

def path_weight(x, z):
    """1 on the worn walking line: south edge -> round the fountain's east side -> the stall, and -> the door."""
    lines = [((0.6, -6.0), (2.6, -2.6)), ((2.6, -2.6), (3.3, 0.6)), ((3.3, 0.6), (STALL_U[0] - 0.9, STALL_U[1] - 1.5)),
             ((3.3, 0.6), (1.6, 3.6)), ((1.6, 3.6), (DOOR_U[0], DOOR_U[1] - 0.4))]
    best = 1e9
    for (ax, az), (bx, bz) in lines:
        dx, dz = bx - ax, bz - az; t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)))
        best = min(best, math.hypot(x - (ax + t * dx), z - (az + t * dz)))
    return P.smoothstep(1.0 - best / 1.1)

class Layout:
    def __init__(self):
        self.occ = np.zeros((NX, NZ), dtype=bool)
        r = np.hypot(GX, GZ)
        self.occ |= r < P.RING_R + 0.006
        self.occ |= (GZ > FACADE[2]) & (GX > FACADE[0] - 0.05) & (GX < FACADE[1] + 0.05)
        self.stones = []

    def _cells(self, x, z, L, W, a, m):
        hx = abs(L / 2 * math.cos(a)) + abs(W / 2 * math.sin(a)) + m; hz = abs(L / 2 * math.sin(a)) + abs(W / 2 * math.cos(a)) + m
        i0, i1 = int((x - hx - X0) / RES_G), int((x + hx - X0) / RES_G) + 1
        j0, j1 = int((z - hz - Z0) / RES_G), int((z + hz - Z0) / RES_G) + 1
        if i0 < 0 or j0 < 0 or i1 > NX or j1 > NZ:
            i0, j0, i1, j1 = max(i0, 0), max(j0, 0), min(i1, NX), min(j1, NZ)
            if i0 >= i1 or j0 >= j1: return None
        gx, gz = GX[i0:i1, j0:j1] - x, GZ[i0:i1, j0:j1] - z
        u = gx * math.cos(a) + gz * math.sin(a); v = -gx * math.sin(a) + gz * math.cos(a)
        return (slice(i0, i1), slice(j0, j1)), (np.abs(u) < L / 2 + m) & (np.abs(v) < W / 2 + m)

    def place(self, v, x, z, a, sc=1.0, tol=0.06):
        if not (X0 + 0.04 < x < X1 - 0.04 and Z0 + 0.04 < z < Z1 - 0.04): return False
        L, W = VAR[v][1] * sc, VAR[v][2] * sc
        c = self._cells(x, z, L, W, a, JOINT / 2)
        if c is None: return False
        sl, mask = c
        if mask.sum() == 0: return False
        if (self.occ[sl] & mask).sum() > tol * mask.sum(): return False
        self.occ[sl] |= mask
        self.stones.append(dict(v=v, x=x, z=z, a=a, s=sc))
        return True

    def pick(self, r, x, z, small=False):
        if small: return 'small_a'
        if r.random() < path_weight(x, z) * 0.85: return r.choice(PATHS)
        rr = math.hypot(x, z)                     # the dish holds the wet: damp setts within ~1.5 m of the ring
        if r.random() < 0.85 * P.smoothstep(1.0 - (rr - P.RING_R) / 1.6) + 0.04: return r.choice(DAMPS)
        return r.choice(SETTS)

    def course(self, r, pts_fn, length, tol=0.06):
        """Lay setts along a parametric course pts_fn(s) -> (x, z, tangent angle), s in metres."""
        s = r.uniform(0, 0.08)
        while s < length:
            x, z, _ = pts_fn(s)
            v = self.pick(r, x, z); sc = r.uniform(0.94, 1.06); L = VAR[v][1] * sc
            cx, cz, ca = pts_fn(s + L / 2)
            self.place(v, cx, cz, ca + r.uniform(-0.05, 0.05), sc, tol)
            s += L + JOINT * r.uniform(0.8, 1.3)

    def build(self):
        r = rng('layout')
        # 1. three courses hugging the flagstone ring
        for k in range(3):
            R = P.RING_R + JOINT + 0.07 + k * (0.14 + JOINT)
            self.course(r, lambda s, R=R: (R * math.cos(s / R), R * math.sin(s / R), s / R + math.pi / 2), 2 * math.pi * R, tol=0.10)
        # 2. gutter course along the facade foot
        zf = FACADE[2] - 0.075 - 0.004
        self.course(r, lambda s: (FACADE[0] + s, zf, 0.0), FACADE[1] - FACADE[0], tol=0.10)
        # 3. fans: 2.0 m wide, rows 1.0 m apart, every other row offset half a fan, arcs bulging north
        FW, FH, RMAX = 2.0, 1.0, 1.45
        for j in range(-1, int((Z1 - Z0) / FH) + 3):
            for i in range(-1, int((X1 - X0) / FW) + 2):
                ox = X0 + i * FW + (FW / 2 if j % 2 else 0.0) + r.uniform(-0.03, 0.03); oz = Z0 + j * FH - 0.6
                Rk = RMAX
                while Rk > 0.22:
                    a = math.asin(min(0.97, (FW * 0.52) / Rk))
                    arc = 2 * a * Rk
                    self.course(r, lambda s, R=Rk, a0=math.pi / 2 + a, ox=ox, oz=oz: (ox + R * math.cos(a0 - s / R), oz + R * math.sin(a0 - s / R), a0 - s / R - math.pi / 2), arc, tol=0.07)
                    Rk -= 0.14 + JOINT
        # 4. fill what is left: setts, then small setts, then cut small setts (scale 0.75), trying a few angles each
        for small, scl, step in ((False, (0.9, 1.0), 5), (True, (0.92, 1.04), 3), (True, (0.72, 0.8), 2)):
            free = np.argwhere(~self.occ).tolist()
            idx = list(range(0, len(free), step)); r.shuffle(idx)
            for t in idx:
                i, j = free[t]
                if self.occ[i, j]: continue
                x, z = GX[i, j], GZ[i, j]
                base = math.atan2(z, x) + math.pi / 2 if math.hypot(x, z) < 4.0 else r.uniform(-0.3, 0.3)
                v = self.pick(r, x, z, small); sc = r.uniform(*scl)
                for da in (0.0, math.pi / 2, math.pi / 4, -math.pi / 4):
                    if self.place(v, x, z, base + da, sc, tol=0.16): break
        return self.stones

def heights_and_damage(stones):
    """Uneven heights, clustered sunken and tilted setts, missing setts, loose stones; weed slots for B9."""
    r = rng('damage')
    pts = np.array([[s['x'], 0.0, s['z']] for s in stones])
    und = (K.fbm(pts, 0.7, 3, 0.5, 11.0) - 0.5) * 2              # slow undulation
    clus = K.fbm(pts, 0.45, 2, 0.5, 23.0)                          # sinking clusters
    kept, loose, weeds = [], [], []
    for s, u, c in zip(stones, und, clus):
        x, z = s['x'], s['z']; rr = math.hypot(x, z); pw = path_weight(x, z)
        top = P.dish(rr) + 0.006 * u + r.gauss(0, 0.0018)
        tx, tz = r.gauss(0, 0.012), r.gauss(0, 0.012)
        near_ring = P.smoothstep(1.0 - (rr - P.RING_R) / 2.0)
        sink_p = 0.03 + 0.25 * P.smoothstep((c - 0.58) / 0.12) + 0.06 * near_ring
        if r.random() < sink_p:
            top -= r.uniform(0.008, 0.022); tx += r.uniform(-1, 1) * 0.06; tz += r.uniform(-1, 1) * 0.06
        miss_p = 0.002 + 0.06 * P.smoothstep((c - 0.66) / 0.08) * (1 - pw)
        if r.random() < miss_p and s['v'] != 'small_a':
            weeds.append(dict(p=[x, P.dish(rr) - 0.02, z], kind='missing_sett'))
            if r.random() < 0.45:                                  # the dislodged sett lies beside its hole
                ang = r.uniform(0, 2 * math.pi); d = r.uniform(0.18, 0.32)
                loose.append(dict(v=s['v'], x=x + d * math.cos(ang), z=z + d * math.sin(ang), a=r.uniform(0, 6.28),
                                  top=None, tilt=(r.uniform(0.2, 0.5), r.uniform(-0.3, 0.3)), s=s['s']))
            continue
        s.update(top=top, tilt=(tx, tz)); kept.append(s)
    # rubble: by the facade foot (left end, under where the ivy grows), by the stall legs, strays
    def drop(x, z, n, spread, frag_bias=0.7):
        for _ in range(n):
            px, pz = x + r.gauss(0, spread), z + r.gauss(0, spread)
            v = r.choice(['frag_a', 'frag_b']) if r.random() < frag_bias else r.choice(SETTS)
            loose.append(dict(v=v, x=px, z=pz, a=r.uniform(0, 6.28), top=None, tilt=(r.uniform(-0.35, 0.35), r.uniform(-0.35, 0.35)), s=r.uniform(0.85, 1.1)))
    drop(-4.5, 7.15, 7, 0.22); drop(-2.6, 7.2, 3, 0.15); drop(3.6, 3.7, 4, 0.25); drop(5.6, 5.2, 3, 0.2)
    drop(-5.5, 1.5, 2, 0.6); drop(-2.0, -3.5, 2, 0.8, 1.0)
    # weed slots: joints along the facade foot and the ring's shaded (north) edge, plus sparse joints
    for k in range(int((FACADE[1] - FACADE[0]) / 0.45)):
        if r.random() < 0.7:
            x = FACADE[0] + 0.2 + k * 0.45 + r.uniform(-0.12, 0.12); weeds.append(dict(p=[x, P.dish(math.hypot(x, 7.4)) - 0.01, FACADE[2] - 0.01], kind='facade_foot'))
    for k in range(48):
        a = 2 * math.pi * k / 48 + r.uniform(-0.04, 0.04)
        north = 0.5 + 0.5 * math.sin(a)
        if r.random() < 0.15 + 0.6 * north:
            R = P.RING_R + 0.005; weeds.append(dict(p=[R * math.cos(a), P.dish(R) - 0.01, R * math.sin(a)], kind='ring_edge'))
    for _ in range(40):
        x, z = r.uniform(X0, X1), r.uniform(Z0, Z1)
        if math.hypot(x, z) > P.RING_R + 0.3 and path_weight(x, z) < 0.2 and z < FACADE[2]:
            weeds.append(dict(p=[x, P.dish(math.hypot(x, z)) - 0.01, z], kind='joint'))
    return kept, loose, weeds

# ----------------------------------------------------------------------------- joint bed (dirt and grit; material in sc_plaza_common)
def bed_damp(x, z):
    rr = math.hypot(x, z)
    ring = P.smoothstep(1.0 - (rr - P.RING_R) / 1.6)
    fac = P.smoothstep(1.0 - (FACADE[2] - z) / 1.4) * (1.0 if FACADE[0] - 0.5 < x < FACADE[1] + 0.5 else 0.3)
    return min(1.0, 0.15 + 0.7 * ring + 0.75 * fac)

def build_bed():
    bm = bmesh.new(); step = 0.2
    nx, nz = int((X1 - X0) / step), int((Z1 - Z0) / step)
    uvl = bm.loops.layers.uv.new('UVMap'); col = bm.loops.layers.color.new('damp')
    vs = {}
    for i in range(nx + 1):
        for j in range(nz + 1):
            xu, zu = X0 + i * step, Z0 + j * step
            y = P.dish(math.hypot(xu, zu)) - 0.034
            vs[i, j] = bm.verts.new(P.u2b((xu, y, zu)))
    for i in range(nx):
        for j in range(nz):
            xc, zc = X0 + (i + 0.5) * step, Z0 + (j + 0.5) * step
            if math.hypot(xc, zc) < P.RING_R - 0.1: continue
            if zc > FACADE[2] + 0.1 and FACADE[0] < xc < FACADE[1]: continue
            f = bm.faces.new((vs[i, j], vs[i, j + 1], vs[i + 1, j + 1], vs[i + 1, j]))
            for lp in f.loops:
                b = lp.vert.co; xu, zu = -b.x, -b.y
                lp[uvl].uv = (xu / 2.0, zu / 2.0)
                d = bed_damp(xu, zu); pw = path_weight(xu, zu); lp[col] = (d * (1 - pw), pw, 0.0, 1.0)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = K._obj_from_bm('PlazaBed', bm)
    if ob.data.polygons[0].normal.z < 0: ob.data.flip_normals()
    ob.data.materials.append(P.bed_material('M_CobbleBed'))
    return ob

def bake_bed_tile():
    """Bake the CobbleBed tileable: a 2 m plane with UV 0..1 shading itself (selected-to-active from a twin)."""
    bpy.ops.mesh.primitive_plane_add(size=2.0, location=(30, 0, 0)); lo = bpy.context.active_object; lo.name = 'BedTile'
    hi = K.dup(lo, 'BedTile_High'); hi.location.z += 0.001
    hi.data.materials.append(bpy.data.materials['M_CobbleBed'])
    bpy.context.view_layer.update()
    paths = K.bake_to_low(lo, [hi], os.path.join(OUT, 'tex'), 'CobbleBed', res=RES, samples=16, extrusion=0.01, ray=0.02, ao_dist=0.02)
    bpy.data.objects.remove(hi); bpy.data.objects.remove(lo)
    return paths

# ----------------------------------------------------------------------------- instancing + output
def instance_all(meshes, kept, loose):
    """meshes: {variant: (lod0 object, lod1 object|None)}. Returns the placement records (Unity)."""
    coll = bpy.data.collections.new('Cobbles'); bpy.context.scene.collection.children.link(coll)
    recs = []; wide = Vector(P.CAMS_U['wide'][0])
    r = rng('loose')
    for kind, lst in (('sett', kept), ('loose', loose)):
        for s in lst:
            v = s['v']; lo0, lo1 = meshes[v]
            rr = math.hypot(s['x'], s['z']); ground = P.dish(rr)
            far = (Vector((s['x'], 0, s['z'])) - Vector((wide.x, 0, wide.z))).length > 12.0
            lod = 1 if (far and lo1 is not None and kind == 'sett') else 0
            src = lo1 if lod else lo0
            yaw_b = math.atan2(-math.sin(s['a']), -math.cos(s['a']))
            tx, tz = s['tilt']
            Mb = Matrix.Rotation(tx, 4, 'X') @ Matrix.Rotation(tz, 4, 'Y') @ Matrix.Rotation(yaw_b, 4, 'Z')
            if kind == 'sett':
                y = s['top']
            elif VAR[v][4] == 'frag':
                y = ground - 0.004
            else:                                         # a loose sett lies on its side on the setts: top face points sideways
                Mb = Matrix.Rotation(r.uniform(-1, 1) * 0.3, 4, 'Z') @ Matrix.Rotation(math.pi / 2 * r.choice((-1, 1)), 4, 'X') @ Matrix.Rotation(yaw_b, 4, 'Z')
                y = ground + VAR[v][2] * s['s'] / 2 - 0.004
            loc = P.u2b((s['x'], y, s['z']))
            o = bpy.data.objects.new(f'{v}.{len(recs)}', src.data); coll.objects.link(o)
            sc = (s['s'], s['s'], 1.0 if kind == 'sett' else s['s'])
            o.matrix_world = Matrix.Translation(loc) @ Mb @ Matrix.Diagonal((*sc, 1.0))
            recs.append(dict(v=v, lod=lod, kind=kind, p=[round(c, 4) for c in P.b2u(loc)], q=[round(c, 5) for c in P.rot_b2u_quat(Mb)],
                             s=[round(sc[0], 4), round(sc[2], 4), round(sc[1], 4)], seen=P.in_any_frame((s['x'], y, s['z']))))
    for lo0, lo1 in meshes.values():
        for o in (lo0, lo1):
            if o: o.hide_render = True; o.location.z = -50
    return recs

def export(meshes, bed):
    for o in bpy.context.selected_objects: o.select_set(False)
    for lo0, lo1 in meshes.values():
        for o in (lo0, lo1):
            if o: o.location = (0, 0, 0); o.hide_render = False; o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'CobbleFamily.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='FACE')
    for o in bpy.context.selected_objects: o.select_set(False)
    bed.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'PlazaBed.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='FACE', colors_type='LINEAR')

def ground_cam():
    """A low look at the cobbles south-east of the fountain (not a capture camera: a material check)."""
    P.CAMS_U['ground'] = ((3.2, 0.9, -4.6), (1.8, 0.0, -2.4))

# ----------------------------------------------------------------------------- main
if __name__ == '__main__':
    K.reset(); sc = bpy.context.scene
    lay = Layout(); stones = lay.build()
    kept, loose, weeds = heights_and_damage(stones)
    print('LAYOUT setts', len(kept), 'loose', len(loose), 'weed slots', len(weeds))
    highs = build_family()
    print('HIGH TRIS', {n: K.tris(h) for n, h in highs.items()})
    if STAGE == 'bake':
        meshes, paths = bake_family(highs)
        bed = build_bed(); bake_bed_tile()
    else:
        meshes = {}
        for n, h in highs.items(): meshes[n] = (h, None)
        bed = build_bed()
    recs = instance_all(meshes, kept, loose)
    tri = {n: (K.tris(a), K.tris(b) if b else 0) for n, (a, b) in meshes.items()}
    inst_tris = sum(tri[r_['v']][r_['lod']] for r_ in recs)
    seen = sum(1 for r_ in recs if r_['seen'])
    stats = dict(setts=len(kept), loose=len(loose), weed_slots=len(weeds), seen=seen, instance_tris=inst_tris,
                 variant_tris=tri, bed_tris=K.tris(bed), stage=STAGE)
    print('STATS', json.dumps(stats))
    with open(os.path.join(OUT, 'cobble_placements.json'), 'w') as f:
        json.dump(dict(units='metres, Unity axes, origin = fountain centre', stats=stats, stones=recs,
                       weed_slots=[dict(p=[round(c, 3) for c in w['p']], kind=w['kind']) for w in weeds]), f)
    if '--export' in argv and STAGE == 'bake':
        export(meshes, bed)
    if '--ring' in argv:
        import sc_flag_ring as FR
        FR.build_ring(look=True)
    if '--no-render' not in argv:
        P.standins(); ground_cam()
        P.render_cams(sc, OUT, f'Cobbles_{STAGE}', VIEWS, RRES, SAMPLES, MOOD)
