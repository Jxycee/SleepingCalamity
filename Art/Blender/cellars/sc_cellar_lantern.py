"""CellarLantern: B11 hero prop (T4, PHASE1 4 and P40), the iron lantern that lights the dungeon corner.

A storybook wrought-iron candle lantern, about 15% oversized: a square body tapering upward, twisted corner posts,
four old-glass panes (front one a hinged door with a turn latch), an upturned-eave roof that turns from square to a
round dome, a vented cupola, and a hanging ring. Bun feet and a stepped plinth give it a heavy foot; roof and ring
give the top weight. Wear beats (each visible at the three-quarter view): soot smoking the upper glass and the
roof underside and vents; rust blooming at the foot and in crevices; hand-polished bare iron on the ring, the latch
and the door's edge; wax spilt out of the candle cup onto the plinth. Contrast pair: candle gold on black iron.

Authored in Blender axes: z up, front -Y, metres, origin at the bottom centre (Unity export -Z forward, Y up).
Iron, wax and wick are one unique bake (BaseColor, Normal, Roughness, Metallic, AO, Emission = the vent glow).
The glass panes and the flame card are not baked: in Unity the glass is a transparent material and the flame a
particle or emissive card, with a point light at FLAME.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/cellars/sc_cellar_lantern.py -- --out <scratch>
      [--stage look|bake]   look (default): procedural highs rendered directly, no bake; bake: low + baked maps + render
      [--res 1024]          bake resolution (the PC's final bake: --res 4096)
      [--tris 45000] [--rres 900] [--samples 64] [--views tq,front,close,wide] [--moods cellar,studio] [--no-render]
Cloud sessions commit this generator only; the PC runs --stage bake --res 4096 to write the textures (sc-asset-craft 4).
Optional CC0 scan layer: AAA_SCANS=<folder with rusty_metal_02_{diff,rough,disp}_2k.jpg> (sc_fetch_textures.py).

Status (2026-10-30, cloud): look locked at relaxed review 28.5/40 (bake 2: 2048 px, high 1.40M tris, low 44,986 tris;
review 1 was 23/40 before the wear/surface/proportion fixes). Next: the PC runs --stage bake --res 4096 with the scans
and exports the FBX + maps (cloud sessions commit only this generator). Optional later passes are in docs/PARKING_LOT.md.
"""
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
import sc_aaa_kit as K, sc_aaa_mats2 as M2, sc_aaa_cuts as C

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
STAGE = arg('--stage', 'look'); RES = int(arg('--res', '1024')); RRES = int(arg('--rres', '900'))
SAMPLES = int(arg('--samples', '64'))
VIEWS = arg('--views', 'tq,front,close,wide').split(','); MOODS = arg('--moods', 'cellar,studio').split(',')
NAME = 'CellarLantern'

# ----------------------------------------------------------------------------- dimensions (metres)
Z_BODY0, Z_BODY1 = 0.048, 0.336       # posts: plinth top to roof eave
HW0, HW1 = 0.108, 0.097               # post-line half-width at the bottom and top (the body tapers upward)
POST = 0.020                          # square bar (about 1.6x real)
Z_GUARD = 0.192                       # horizontal guard bars
FLAME = Vector((0.0, 0.0, 0.147))     # candle flame centre: the point light in Unity
VENTS = []                            # (outer point, inward axis, radius, depth): the cutters AND the glow mask

def hw(z):
    return HW0 + (HW1 - HW0) * (z - Z_BODY0) / (Z_BODY1 - Z_BODY0)

PIDS = {}
def pid(name):
    PIDS[name] = 0.173 * (len(PIDS) + 1); return PIDS[name]

def bake_xf(ob):
    """Bake the object transform into the mesh so 'lp' (and the story masks) are lantern coordinates."""
    bpy.context.view_layer.update()
    ob.data.transform(ob.matrix_world); ob.matrix_world = Matrix.Identity(4)
    return ob

def sq_loft(name, prof, segs=96, close_caps=True, lift=None):
    """Closed solid lofted through rounded-square rings. prof: [(half-width, z, n)] bottom -> top, n the
    superellipse exponent (2 = circle, 6 = nearly square). lift(t_corner, z) -> dz turns eave corners up."""
    bm = bmesh.new(); rings = []
    for hwid, z, n in prof:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs + math.pi / 4
            c, s = math.cos(a), math.sin(a)
            r = hwid / (abs(c) ** n + abs(s) ** n) ** (1.0 / n)
            corner = abs(math.sin(2 * a))                     # 1 on the diagonals
            dz = lift(corner, z) if lift else 0.0
            ring.append(bm.verts.new((r * c, r * s, z + dz)))
        rings.append(ring)
    for a_, b_ in zip(rings[:-1], rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs; bm.faces.new((a_[i], a_[j], b_[j], b_[i]))
    if close_caps:
        bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K._obj_from_bm(name, bm)

def twisted_post(name, x0, y0, x1, y1, turns=1.5, size=POST):
    """Square bar twisted between collars (the way a smith twists it hot), leaning with the taper."""
    L = Z_BODY1 - Z_BODY0
    ob = K.box(name, (size, size, L), cuts=(1, 1, 90))
    for v in ob.data.vertices:
        t = (v.co.z + L / 2) / L
        f = min(max((t - 0.22) / 0.56, 0.0), 1.0)                # twist only between 22% and 78% of the height
        f = f * f * (3 - 2 * f)
        a = turns * 2 * math.pi * f
        x, y = v.co.x, v.co.y
        v.co.x, v.co.y = x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a)
        v.co.x += x0 + (x1 - x0) * t; v.co.y += y0 + (y1 - y0) * t; v.co.z += Z_BODY0 + L / 2
    return ob

def collar(name, x, y, z, s=POST * 1.45, h=0.012):
    return K.box(name, (s, s, h), (x, y, z))

def ring_tube(name, ctr, R, r, axis='Y', sides=14, n=48, arc=(0.0, 2 * math.pi)):
    a0, a1 = arc; full = abs(a1 - a0 - 2 * math.pi) < 1e-6
    pts = []
    for k in range(n + (1 if full else 0) + (0 if full else 1)):
        a = a0 + (a1 - a0) * k / n
        u, w = R * math.cos(a), R * math.sin(a)
        p = (u, 0, w) if axis == 'Y' else (0, u, w) if axis == 'X' else (u, w, 0)
        pts.append(Vector(ctr) + Vector(p))
    if full: pts.append(pts[1])                              # overlap the seam; the voxel remesh unions it
    return K.tube(name, pts, r, sides)

def strap(name, p0, p1, width, thick, normal):
    """Flat iron strap from p0 to p1, width across, thickness along `normal`."""
    p0, p1, nrm = Vector(p0), Vector(p1), Vector(normal).normalized()
    d = p1 - p0; L = d.length; x = d.normalized(); y = nrm.cross(x).normalized()
    ob = K.box(name, (L, width, thick), cuts=(max(2, int(L / 0.01)), 1, 1))
    M = Matrix((x, y, nrm)).transposed().to_4x4(); M.translation = (p0 + p1) / 2
    ob.matrix_world = M
    return bake_xf(ob)

def rivet(name, p, normal, r=0.0062):
    ob = K.lathe(name, K.smooth_profile([(0.0008, -0.004), (r, -0.004), (r * 0.98, 0.0005), (r * 0.75, r * 0.45), (0.0008, r * 0.62)], 0.0012), 16, close=True)
    z = Vector(normal).normalized(); x = Vector((0, 0, 1)).cross(z)
    if x.length < 1e-4: x = Vector((1, 0, 0))
    x.normalize(); y = z.cross(x)
    M = Matrix((x, y, z)).transposed().to_4x4(); M.translation = Vector(p)
    ob.matrix_world = M
    return bake_xf(ob)

def cyl_cutter(name, p, axis, r, depth):
    """Cylinder from p (outside) going `depth` along the inward `axis` (vent holes, punched)."""
    a = Vector(axis).normalized()
    ob = K.lathe(name, [(0.0005, -0.006), (r, -0.006), (r, depth), (0.0005, depth)], 20, close=True)
    z = a; x = Vector((0, 0, 1)).cross(z)
    if x.length < 1e-4: x = Vector((1, 0, 0))
    x.normalize(); y = z.cross(x)
    M = Matrix((x, y, z)).transposed().to_4x4(); M.translation = Vector(p)
    ob.matrix_world = M
    return bake_xf(ob)

# ----------------------------------------------------------------------------- materials
def lantern_iron(name, polish_pids, rust=0.55):
    """Blackened wrought iron with the lantern's story: rust at the foot and in crevices, soot on the underside of
    the roof and smoked up from the vents, hand-polished bare metal on the ring, latch and door edge, and the
    vent pockets glowing candle-gold (Emission; strength scaled in the game material)."""
    m, T = K.new_mat(name)
    lp = T.attr('lp'); pidv = T.attr('pid', vec=False)
    w = T.node('ShaderNodeTexWhiteNoise', noise_dimensions='1D'); T.set(w, 'W', pidv)
    p = T.vmath('ADD', lp, T.vmath('MULTIPLY', (w, 'Color'), (3.0, 3.0, 3.0)))
    K.use_scan(T, 'rusty_metal_02', p, 6.0, col=0.3, rough=0.35, height=0.6)
    z = (T.sep(lp), 'Z'); nz = (T.sep(T.geo('Normal')), 'Z')
    edge = T.edge(radius=0.004, lo=0.03, hi=0.2)
    cav = T.cavity(dist=0.012, lo=0.3, hi=0.95)
    # rust: blotches everywhere a little, strongly in crevices and in a band at the foot where damp sits
    blot = T.noise(p, scale=45.0, detail=6, rough=0.7)
    foot = T.math('MULTIPLY', T.smoothstep(z, 0.034, 0.004), T.noise(p, scale=9.0, detail=3))   # damp creeping up from the floor
    thr = T.math('SUBTRACT', 0.66 - 0.06 * rust, T.math('MULTIPLY', foot, 0.5))
    rm = T.smoothstep(T.math('SUBTRACT', blot, thr), 0.0, 0.12)
    rm = T.math('MAXIMUM', rm, T.math('MULTIPLY', T.math('SUBTRACT', 1.0, cav), rust * 1.3))
    rm = T.math('MULTIPLY', rm, T.math('SUBTRACT', 1.0, T.math('MULTIPLY', edge, 0.7)))   # worn edges stay iron
    rust_c = T.mix(T.noise(p, scale=110, detail=3), (0.13, 0.05, 0.02), (0.34, 0.13, 0.04))
    base = T.mix(T.noise(p, scale=22, detail=4), (0.085, 0.072, 0.064), (0.17, 0.145, 0.125))   # warm charcoal, not neutral grey
    c = T.mix(T.math('MULTIPLY', edge, 0.75), base, (0.34, 0.31, 0.28))
    c = T.mix(rm, c, rust_c)
    r = T.mixf(T.math('MULTIPLY', edge, 0.8), T.mixf(T.noise(p, scale=8.0, detail=3), 0.42, 0.7), 0.28)   # scale patches rougher, worn edges smoother
    r = T.mixf(rm, r, 0.88)
    metal = T.mixf(rm, 0.8, 0.0)
    # soot: faces looking down above the glass (roof underside, top rail), and smoke trails rising from the vents
    under = T.math('MULTIPLY', T.smoothstep(z, 0.29, 0.335), T.smoothstep(nz, 0.2, -0.6))
    band = T.math('MULTIPLY', T.math('MULTIPLY', T.smoothstep(z, 0.262, 0.322), T.smoothstep(z, 0.36, 0.34)), T.smoothstep(nz, 0.8, 0.2))   # smoke over the top rail and posts
    trail = 0.0
    for (o, a, rad, dep) in VENTS:
        d = T.vmath('SUBTRACT', lp, tuple(o))
        up = T.math('MAXIMUM', T.math('SUBTRACT', (T.sep(d), 'Z'), 0.0), 0.0)
        flat = T.vmath('LENGTH', T.vmath('MULTIPLY', d, (1.0, 1.0, 0.0)), out=1)
        s = T.math('MULTIPLY', T.smoothstep(flat, rad * 2.4, rad * 0.7), T.math('MULTIPLY', T.smoothstep(up, 0.07, 0.0), T.smoothstep((T.sep(d), 'Z'), -0.004, 0.002)))
        trail = s if trail == 0.0 else T.math('MAXIMUM', trail, s)
    soot = T.math('MAXIMUM', T.math('MAXIMUM', under, band), trail)
    soot = T.math('MULTIPLY', soot, T.smoothstep(T.noise(p, scale=30, detail=5), 0.25, 0.5))
    c = T.mix(soot, c, (0.022, 0.016, 0.012)); r = T.mixf(soot, r, 0.95); metal = T.mixf(soot, metal, 0.0)
    # polish: bare metal where hands go (by part)
    pol = 0.0
    for q in polish_pids:
        e = T.math('COMPARE', pidv, q, 0.01)
        pol = e if pol == 0.0 else T.math('MAXIMUM', pol, e)
    pol = T.math('MULTIPLY', pol, T.math('ADD', 0.75, T.math('MULTIPLY', edge, 0.25)))
    c = T.mix(pol, c, (0.62, 0.58, 0.52)); r = T.mixf(pol, r, 0.2); metal = T.mixf(pol, metal, 1.0)
    # glow in the punched vent pockets: true distance to each hole's axis segment (same list the cutters used)
    glow = 0.0
    for (o, a, rad, dep) in VENTS:
        d = T.vmath('SUBTRACT', lp, tuple(o))
        t = T.math('MINIMUM', T.math('MAXIMUM', T.vmath('DOT_PRODUCT', d, tuple(a), out=1), 0.0), dep)
        perp = T.vmath('LENGTH', T.vmath('SUBTRACT', d, T.vmath('MULTIPLY', tuple(a), T.comb(t, t, t))))
        g = T.math('MULTIPLY', T.smoothstep(perp, rad * 1.04, rad * 0.8), T.smoothstep(t, 0.0015, dep))
        glow = g if glow == 0.0 else T.math('MAXIMUM', glow, g)
    ham = T.voronoi(p, scale=150.0, out='Distance')
    plan = T.voronoi(p, scale=30.0, out='Distance')                       # hammer dents (about 1-2 cm)
    pits = T.smoothstep(T.voronoi(p, scale=420.0, out='Distance'), 0.22, 0.0)   # forge-scale pitting
    h = T.math('ADD', T.math('ADD', T.math('MULTIPLY', ham, 0.25), T.math('MULTIPLY', plan, 1.0)), T.math('MULTIPLY', rm, 0.45))
    h = T.math('SUBTRACT', h, T.math('MULTIPLY', pits, T.math('ADD', 0.15, T.math('MULTIPLY', rm, 0.3))))
    T.output(c, r, metal, T.bump(h, strength=0.75, distance=0.0012), Emission_Color=(1.0, 0.42, 0.09), Emission_Strength=glow)
    return m

def candle_wax(name, col=(0.50, 0.40, 0.22)):
    """Tallow wax, a little dirty in the drips' creases, glowing warm near the flame (translucent wax lit from above)."""
    m, T = K.new_mat(name)
    lp = T.attr('lp'); z = (T.sep(lp), 'Z')
    cav = T.cavity(dist=0.006, lo=0.3, hi=0.95)
    c = T.mix(T.noise(lp, scale=60, detail=4), T.hsv(col, 0.5, 1.0, 0.85), T.hsv(col, 0.5, 0.9, 1.1))
    c = T.mix(T.math('MULTIPLY', T.math('SUBTRACT', 1.0, cav), 0.6), c, (0.12, 0.08, 0.03))
    r = T.mixf(T.noise(lp, scale=90, detail=3), 0.3, 0.5)
    glow = T.math('MULTIPLY', T.smoothstep(z, 0.10, 0.131), 0.07)   # x6 in the game material
    T.output(c, r, 0.0, T.bump(T.noise(lp, scale=300, detail=3), strength=0.15, distance=0.0004),
             Emission_Color=(1.0, 0.52, 0.18), Emission_Strength=glow)
    return m

def lantern_glass(name):
    """Old glass, not baked: faint amber, smudged, smoked dark toward the top (the soot beat), dusty at the foot."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    if hasattr(m, 'surface_render_method'): m.surface_render_method = 'BLENDED'
    nt = m.node_tree
    n, l = nt.nodes, nt.links
    bs = n['Principled BSDF']
    tc = n.new('ShaderNodeTexCoord'); sep = n.new('ShaderNodeSeparateXYZ'); l.new(tc.outputs['Object'], sep.inputs[0])
    sm = n.new('ShaderNodeMapRange'); sm.interpolation_type = 'SMOOTHSTEP'; l.new(sep.outputs['Z'], sm.inputs['Value'])
    sm.inputs['From Min'].default_value = 0.17; sm.inputs['From Max'].default_value = 0.30
    nz = n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 22; nz.inputs['Detail'].default_value = 6
    l.new(tc.outputs['Object'], nz.inputs['Vector'])
    sw = n.new('ShaderNodeMath'); sw.operation = 'MULTIPLY_ADD'; l.new(nz.outputs['Fac'], sw.inputs[0])
    sw.inputs[1].default_value = 0.6; sw.inputs[2].default_value = 0.7
    soot = n.new('ShaderNodeMath'); soot.operation = 'MULTIPLY'; soot.use_clamp = True
    l.new(sm.outputs['Result'], soot.inputs[0]); l.new(sw.outputs['Value'], soot.inputs[1])
    col = n.new('ShaderNodeMix'); col.data_type = 'RGBA'; l.new(soot.outputs['Value'], col.inputs[0])
    col.inputs[6].default_value = (0.50, 0.45, 0.32, 1); col.inputs[7].default_value = (0.012, 0.010, 0.008, 1)
    l.new(col.outputs[2], bs.inputs['Base Color'])
    al = n.new('ShaderNodeMapRange'); l.new(soot.outputs['Value'], al.inputs['Value'])
    al.inputs['To Min'].default_value = 0.08; al.inputs['To Max'].default_value = 0.93
    l.new(al.outputs['Result'], bs.inputs['Alpha'])
    ro = n.new('ShaderNodeMapRange'); l.new(nz.outputs['Fac'], ro.inputs['Value'])
    ro.inputs['From Min'].default_value = 0.35; ro.inputs['From Max'].default_value = 0.75
    ro.inputs['To Min'].default_value = 0.04; ro.inputs['To Max'].default_value = 0.35
    l.new(ro.outputs['Result'], bs.inputs['Roughness'])
    bs.inputs['IOR'].default_value = 1.5
    return m

def flame_mat(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n, l = m.node_tree.nodes, m.node_tree.links; n.remove(n['Principled BSDF'])
    em = n.new('ShaderNodeEmission'); em.inputs['Color'].default_value = (1.0, 0.50, 0.12, 1); em.inputs['Strength'].default_value = 60.0
    l.new(em.outputs[0], n['Material Output'].inputs['Surface'])
    return m

# ----------------------------------------------------------------------------- parts
def build():
    """Returns (highs, unbaked) with the highs made by K.make_part."""
    iron_parts = {}
    def iron(name, obs, vox=0.0012, **kw):
        iron_parts[name] = (obs, vox, kw)
    wear = K.wear_generic
    # feet and plinth: bun feet under a stepped, rounded-square plinth
    feet = []
    for i, (sx, sy) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        f = K.lathe(f'foot{i}', K.smooth_profile([(0.004, 0.0), (0.019, 0.001), (0.027, 0.009), (0.024, 0.017), (0.014, 0.021), (0.004, 0.022)], 0.002), 28, close=True)
        f.location = (sx * 0.104, sy * 0.104, 0.0); feet.append(bake_xf(f))
    plinth = sq_loft('plinth', [(0.120, 0.014, 6), (0.131, 0.017, 6), (0.134, 0.024, 6), (0.131, 0.030, 6), (0.121, 0.033, 6),
                                (0.117, 0.037, 6), (0.116, 0.044, 6), (0.111, 0.048, 6), (0.104, 0.050, 6)], 128)
    iron('plinth', feet + [plinth], 0.0014, erode=wear(0.0005, 60, chips=10, chip_r=(0.002, 0.005), seed=1.0), fillet=4)
    # corner posts with collars top and bottom, rails, guard bars and rivets
    posts = []
    for i, (sx, sy) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        posts.append(twisted_post(f'post{i}', sx * HW0, sy * HW0, sx * HW1, sy * HW1, turns=1.5 if i % 2 else -1.5))
        posts.append(collar(f'colA{i}', sx * hw(0.062), sy * hw(0.062), 0.062))
        posts.append(collar(f'colB{i}', sx * hw(0.322), sy * hw(0.322), 0.322))
    rails = []
    for zc in (0.062, 0.322):
        h = hw(zc)
        for k, (ax, sgn) in enumerate(((0, 1), (0, -1), (1, 1), (1, -1))):
            if ax == 0: p0, p1, nrm = (sgn * h, -h, zc), (sgn * h, h, zc), (sgn, 0, 0)
            else: p0, p1, nrm = (-h, sgn * h, zc), (h, sgn * h, zc), (0, sgn, 0)
            rails.append(strap(f'rail{zc}{k}', p0, p1, 0.022, 0.0065, nrm))
    iron('frame', posts + rails, 0.0011, bev=0.0016, erode=wear(0.0004, 70, chips=14, chip_r=(0.0015, 0.004), seed=2.0), fillet=3)
    guards = []
    h = hw(Z_GUARD) + 0.0045
    for side, (nx, ny) in enumerate(((1, 0), (-1, 0), (0, 1))):           # the front is the door
        if nx: a, b = Vector((nx * h, -h + 0.01, Z_GUARD)), Vector((nx * h, h - 0.01, Z_GUARD))
        else: a, b = Vector((-h + 0.01, ny * h, Z_GUARD)), Vector((h - 0.01, ny * h, Z_GUARD))
        guards.append(K.tube(f'guard{side}', [a.lerp(b, t / 8) for t in range(9)], 0.0058, 12))
        top, bot = (a + b) / 2 + Vector((0, 0, 0.122)), (a + b) / 2 - Vector((0, 0, 0.122))
        guards.append(K.tube(f'vguard{side}', [bot.lerp(top, t / 10) for t in range(11)], 0.0052, 12))
        nrm = Vector((nx, ny, 0))
        guards += [rivet(f'gr{side}{j}', q + nrm * 0.004, nrm) for j, q in enumerate((a, b, (a + b) / 2))]
    iron('guards', guards, 0.0009, erode=wear(0.0003, 80, seed=3.0))
    # door on the front (-Y): a raised frame around the pane, hinge knuckles on the left post, a turn latch on the right
    door = []
    zt, zb = 0.305, 0.078
    for (p0, p1) in (((-hw(zb) + 0.012, 0, zb), (hw(zb) - 0.012, 0, zb)), ((-hw(zt) + 0.012, 0, zt), (hw(zt) - 0.012, 0, zt)),
                     ((-hw(zb) + 0.014, 0, zb), (-hw(zt) + 0.014, 0, zt)), ((hw(zb) - 0.014, 0, zb), (hw(zt) - 0.014, 0, zt))):
        yb = -(hw(p0[2]) + 0.006); ye = -(hw(p1[2]) + 0.006)
        door.append(strap(f'door{len(door)}', (p0[0], yb, p0[2]), (p1[0], ye, p1[2]), 0.016, 0.007, (0, -1, 0)))
    hinge = []
    for j, zk in enumerate((0.105, 0.19, 0.275)):
        x = -hw(zk) - 0.004; y = -hw(zk) - 0.008
        k = K.lathe(f'knuckle{j}', K.smooth_profile([(0.002, -0.014), (0.0068, -0.014), (0.0072, -0.012), (0.0072, 0.012), (0.0068, 0.014), (0.002, 0.014)], 0.0015), 20, close=True)
        k.location = (x, y, zk); hinge.append(bake_xf(k))
        hinge.append(strap(f'leaf{j}', (x, y, zk), (x + 0.03, y + 0.002, zk), 0.018, 0.0035, (0, -1, 0)))
    hinge.append(K.tube('pin', [(-hw(0.09) - 0.004, -hw(0.09) - 0.008, 0.088), (-hw(0.29) - 0.004, -hw(0.29) - 0.008, 0.292)], 0.0026, 10))
    iron('door', door + hinge, 0.0009, bev=0.0012, erode=wear(0.0003, 80, chips=6, chip_r=(0.0012, 0.003), seed=4.0))
    zl = 0.19; xl = hw(zl) - 0.004; yl = -hw(zl) - 0.014
    plate = K.box('latchplate', (0.014, 0.004, 0.03), (xl, yl + 0.004, zl))
    turn = K.tube('latchturn', [(xl, yl - 0.002, zl - 0.015), (xl, yl - 0.002, zl + 0.017)], 0.0035, 10)
    loop = ring_tube('latchloop', (xl + 0.0, yl - 0.008, zl + 0.004), 0.009, 0.0024, axis='Y', n=24)
    iron('latch', [bake_xf(plate), turn, loop], 0.0007)
    # roof: upturned eaves, square to round, with punched vents on each face, then the cupola, cap, finial and ring
    def eave_lift(corner, z):
        return 0.009 * corner ** 3 * max(0.0, 1.0 - (z - 0.336) / 0.02)
    roof = sq_loft('roof', [(0.094, 0.331, 6), (0.13, 0.334, 6), (0.146, 0.338, 6), (0.149, 0.343, 5.5), (0.141, 0.348, 5),
                            (0.123, 0.358, 4.4), (0.101, 0.378, 3.6), (0.082, 0.400, 3.0), (0.064, 0.422, 2.6),
                            (0.050, 0.438, 2.3), (0.041, 0.450, 2.0), (0.036, 0.455, 2.0)], 160, lift=eave_lift)
    def ring_pt(hwid, z, n, a):
        c, s_ = math.cos(a), math.sin(a)
        r = hwid / (abs(c) ** n + abs(s_) ** n) ** (1.0 / n)
        return Vector((r * c, r * s_, z + eave_lift(abs(math.sin(2 * a)), z)))
    seams = []
    rp = [(0.149, 0.343, 5.5), (0.141, 0.348, 5), (0.123, 0.358, 4.4), (0.101, 0.378, 3.6), (0.082, 0.400, 3.0),
          (0.064, 0.422, 2.6), (0.050, 0.438, 2.3), (0.041, 0.450, 2.0)]
    for k in range(4):                                                     # folded seams up the four corners
        a = math.pi / 4 + k * math.pi / 2
        pts = [ring_pt(hh + 0.0015, zz, nn, a) for hh, zz, nn in rp]
        dense = []
        for i in range(len(pts) - 1):
            for t in range(4): dense.append(pts[i].lerp(pts[i + 1], t / 4))
        dense.append(pts[-1])
        seams.append(K.tube(f'seam{k}', dense, 0.0042, 10))
    bead = [ring_pt(0.1495, 0.3425, 5.5, 2 * math.pi * i / 160 + math.pi / 4) for i in range(161)] + [ring_pt(0.1495, 0.3425, 5.5, 2 * math.pi / 160 + math.pi / 4)]
    seams.append(K.tube('eavebead', bead, 0.0048, 12))                     # rolled edge on the eave
    drum = K.lathe('drum', K.smooth_profile([(0.004, 0.45), (0.036, 0.45), (0.037, 0.455), (0.036, 0.488), (0.040, 0.49), (0.004, 0.49)], 0.002), 48, close=True)
    cap = K.lathe('cap', K.smooth_profile([(0.004, 0.487), (0.046, 0.489), (0.052, 0.494), (0.048, 0.499), (0.03, 0.507), (0.016, 0.512), (0.004, 0.514)], 0.0015), 56, close=True)
    finial = K.lathe('finial', K.smooth_profile([(0.003, 0.51), (0.012, 0.512), (0.016, 0.52), (0.012, 0.528), (0.007, 0.531), (0.006, 0.536), (0.003, 0.537)], 0.0012), 32, close=True)
    eye = ring_tube('eye', (0, 0, 0.546), 0.0085, 0.004, axis='X', n=24)
    del VENTS[:]
    for i in range(8):                                                     # cupola vents
        a = 2 * math.pi * (i + 0.5) / 8; u = Vector((math.cos(a), math.sin(a), 0))
        VENTS.append((tuple(u * 0.039 + Vector((0, 0, 0.471))), tuple(-u), 0.0062, 0.016))
    for f in range(4):                                                     # three punched holes per roof face
        a = f * math.pi / 2; u = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-u.y, u.x, 0))
        for j, (off, zz, rad) in enumerate(((-0.03, 0.379, 0.0055), (0.0, 0.384, 0.0068), (0.03, 0.379, 0.0055))):
            hw_at = 0.101 - (zz - 0.378) * 0.86
            nrm = (u + Vector((0, 0, 0.95))).normalized()
            o = u * (hw_at + 0.004) + t * off + Vector((0, 0, zz + 0.004))
            VENTS.append((tuple(o), tuple(-nrm), rad, 0.012))
    def vent_cuts(ob):
        C.subtract(ob, [cyl_cutter(f'vent{i}', o, a, r, d) for i, (o, a, r, d) in enumerate(VENTS)])
        return None
    iron('roof', [roof, drum, cap, finial, eye] + seams, 0.0013, erode=wear(0.0004, 60, chips=10, chip_r=(0.002, 0.005), seed=5.0), fillet=3, cuts=vent_cuts)
    ring = ring_tube('ring', (0, 0, 0.588), 0.035, 0.0078, axis='Y', n=64, sides=16)
    iron('ring', [ring], 0.0009, erode=wear(0.0003, 90, seed=6.0))
    # candle: a cup socket, a burnt-down candle with drips, the wick, wax spilt over the cup onto the plinth
    cup = K.lathe('cup', K.smooth_profile([(0.004, 0.049), (0.028, 0.049), (0.03, 0.053), (0.026, 0.062), (0.03, 0.076), (0.034, 0.079),
                                           (0.031, 0.08), (0.024, 0.07), (0.004, 0.068)], 0.0015), 40, close=True)
    iron('cup', [cup], 0.0009, erode=wear(0.0003, 80, seed=7.0))
    rr = K.rng('drips')
    # a fat, burnt-down stub: the rim stands higher than the tunnelled centre, drips half-sunk into the side
    cd = K.lathe('candle', K.smooth_profile([(0.004, 0.066), (0.0245, 0.066), (0.0243, 0.11), (0.0238, 0.127), (0.0222, 0.1305),
                                             (0.019, 0.128), (0.012, 0.1225), (0.004, 0.1215)], 0.0015), 48, close=True)
    wax = [cd]
    for i in range(6):
        a = i * math.pi / 3 + rr.uniform(-0.4, 0.4); L = rr.uniform(0.015, 0.05) if i else 0.072; R = 0.0236; n = 10; pts = []; rad = []
        for k in range(n + 1):
            t = k / n; zz = 0.129 - t * L
            rr_ = R + 0.0006 * math.sin(t * math.pi) + (0.012 * max(0.0, (0.079 - zz)) / 0.03 if zz < 0.08 else 0.0)
            pts.append((rr_ * math.cos(a + 0.025 * math.sin(t * 7 + i)), rr_ * math.sin(a + 0.025 * math.sin(t * 7 + i)), max(zz, 0.0505)))
            rad.append(0.0034 * (1 - 0.3 * t) + (0.0014 if k == n else 0))
        wax.append(K.tube(f'drip{i}', pts, 0.0034, 8, radii=rad))
    spill = K.lathe('spill', K.smooth_profile([(0.003, 0.0495), (0.036, 0.0495), (0.033, 0.053), (0.018, 0.0555), (0.003, 0.056)], 0.0012), 32, close=True)
    spill.location = (0.018, -0.036, 0.0); spill.scale = (1.35, 0.85, 1.0); bake_xf(spill)
    wax.append(spill)
    highs = []
    for name, (obs, vox, kw) in iron_parts.items():
        highs.append(K.make_part(obs, None, pid(name), vox, **kw))
    highs.append(K.make_part(wax, None, pid('wax'), 0.0008, erode=wear(0.0002, 90, seed=8.0), fillet=6))
    wick = K.tube('wick', [(0, 0, 0.118), (0.0006, 0, 0.133), (0.0025, 0.0006, 0.141)], 0.0012, 6)
    highs.append(K.make_part(wick, None, pid('wick'), 0.0004))
    im = lantern_iron('M_LanternIron', [PIDS['ring'], PIDS['latch'], PIDS['door']])
    for hgh in highs:
        hgh.data.materials.clear()
        hgh.data.materials.append(candle_wax('M_Wax') if hgh.name.startswith('candle') else
                                  M2.charcoal('M_Wick', glow=0.6) if hgh.name.startswith('wick') else im)
    return highs, build_unbaked()

def build_unbaked():
    """Glass panes (front pane whole, the right pane missing a corner shard) and the flame card."""
    gm = lantern_glass('M_LanternGlass'); out = []
    za, zb = 0.071, 0.313
    for side, (nx, ny) in enumerate(((1, 0), (-1, 0), (0, 1), (0, -1))):
        bm = bmesh.new()
        def P(s, zz):
            h = hw(zz) - 0.002; w = hw(zz) - 0.011
            return Vector((nx * h, nx * s * w, zz)) if nx else Vector((s * w, ny * h, zz))
        outline = [P(-1, za), P(1, za), P(1, zb), P(-1, zb)]
        if side == 0:                                  # broken corner: the lower front corner shard is gone
            outline = [P(-1, za + 0.058), P(-0.62, za + 0.03), P(-0.48, za + 0.011), P(-0.3, za), P(1, za), P(1, zb), P(-1, zb)]
        vs = [bm.verts.new(v) for v in outline]
        f = bm.faces.new(vs)
        ext = bmesh.ops.extrude_face_region(bm, geom=[f])
        nrm = Vector((nx, ny, 0)) * -0.003
        bmesh.ops.translate(bm, vec=nrm, verts=[e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        ob = K._obj_from_bm(f'glass{side}', bm); ob.data.materials.append(gm); out.append(ob)
    fl = K.lathe('flame', K.smooth_profile([(0.0005, FLAME.z - 0.011), (0.0045, FLAME.z - 0.007), (0.0055, FLAME.z - 0.002), (0.0035, FLAME.z + 0.008),
                                            (0.0008, FLAME.z + 0.017)], 0.001), 16, close=True)
    fl.data.materials.append(flame_mat('M_Flame')); out.append(fl)
    for o in out: K.smooth(o)
    return out

# ----------------------------------------------------------------------------- review render (Cycles CPU)
def render(objs_note):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = RRES; sc.render.image_settings.file_format = 'JPEG'; sc.render.image_settings.quality = 92
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
    def plane(name, loc, rot, s, col, rough=0.85, scale=6.0):
        bpy.ops.mesh.primitive_plane_add(size=s, location=loc, rotation=rot); p = bpy.context.active_object; p.name = name
        m = bpy.data.materials.new(name); m.use_nodes = True; n, l = m.node_tree.nodes, m.node_tree.links
        bs = n['Principled BSDF']; bs.inputs['Roughness'].default_value = rough
        nz = n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = scale; nz.inputs['Detail'].default_value = 8
        mx = n.new('ShaderNodeMix'); mx.data_type = 'RGBA'; l.new(nz.outputs['Fac'], mx.inputs[0])
        mx.inputs[6].default_value = (*[c * 0.7 for c in col], 1); mx.inputs[7].default_value = (*col, 1)
        l.new(mx.outputs[2], bs.inputs['Base Color'])
        p.data.materials.append(m); return p
    plane('floor', (0, 0.3, 0), (0, 0, 0), 8, (0.16, 0.12, 0.09))
    plane('wall', (0, 0.55, 2), (math.radians(90), 0, 0), 8, (0.30, 0.19, 0.13), scale=9.0)
    fig = None
    if 'wide' in VIEWS:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(0.7, 0.15, 0.875)); fig = bpy.context.active_object; fig.name = 'figure_1.75m'
        fm = bpy.data.materials.new('fig'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.2, 0.2, 0.22, 1); fig.data.materials.append(fm)
    cl = bpy.data.lights.new('candle', 'POINT'); cl.shadow_soft_size = 0.006; cl.color = (1.0, 0.62, 0.30)
    co = bpy.data.objects.new('candle', cl); co.location = FLAME; sc.collection.objects.link(co)
    def area(name, loc, energy, col, size):
        ld = bpy.data.lights.new(name, 'AREA'); ld.energy = energy; ld.color = col; ld.size = size
        o = bpy.data.objects.new(name, ld); o.location = loc; sc.collection.objects.link(o)
        d = Vector((0, 0, 0.28)) - Vector(loc); o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); return o
    sc.world = bpy.data.worlds.new('W'); sc.world.use_nodes = True; wn, wl = sc.world.node_tree.nodes, sc.world.node_tree.links
    bg = wn['Background']; env = wn.new('ShaderNodeBackground'); mixw = wn.new('ShaderNodeMixShader'); lpth = wn.new('ShaderNodeLightPath')
    tcw = wn.new('ShaderNodeTexCoord'); sepw = wn.new('ShaderNodeSeparateXYZ'); wl.new(tcw.outputs['Generated'], sepw.inputs[0])
    rmp = wn.new('ShaderNodeValToRGB'); wl.new(sepw.outputs['Z'], rmp.inputs[0])
    rmp.color_ramp.elements[0].position = 0.45; rmp.color_ramp.elements[0].color = (0.02, 0.012, 0.008, 1)
    rmp.color_ramp.elements[1].position = 0.62; rmp.color_ramp.elements[1].color = (0.9, 0.55, 0.28, 1)
    wl.new(rmp.outputs['Color'], env.inputs['Color'])
    wl.new(lpth.outputs['Is Camera Ray'], mixw.inputs[0]); wl.new(env.outputs[0], mixw.inputs[1]); wl.new(bg.outputs[0], mixw.inputs[2])
    wl.new(mixw.outputs[0], wn['World Output'].inputs['Surface'])
    cams = {'tq': ((-0.62, -0.88, 0.62), (0, 0, 0.29), 50), 'front': ((0.0, -1.12, 0.33), (0, 0, 0.29), 50),
            'close': ((0.22, -0.44, 0.27), (0.04, -0.08, 0.19), 70), 'roof': ((-0.26, -0.40, 0.50), (0.0, 0.0, 0.36), 70), 'wide': ((-1.4, -3.4, 1.3), (0.35, 0, 0.6), 35)}
    for mood in MOODS:
        for o in [o for o in sc.objects if o.type == 'LIGHT' and o.name != 'candle']: bpy.data.objects.remove(o)
        if mood == 'cellar':
            cl.energy = 55.0; bg.inputs['Color'].default_value = (0.010, 0.006, 0.005, 1); bg.inputs['Strength'].default_value = 1.0; env.inputs['Strength'].default_value = 0.35
            area('goldkey', (-1.2, -0.9, 1.3), 12.0, (1.0, 0.70, 0.40), 0.9); area('bounce', (1.0, -0.6, 0.3), 3.0, (0.85, 0.45, 0.30), 1.5)
        else:
            cl.energy = 10.0; bg.inputs['Color'].default_value = (0.22, 0.22, 0.23, 1); bg.inputs['Strength'].default_value = 0.6; env.inputs['Strength'].default_value = 1.0
            area('key', (-1.0, -1.2, 1.4), 70.0, (1.0, 0.97, 0.93), 1.0); area('rim', (0.9, 0.8, 1.0), 40.0, (0.9, 0.95, 1.0), 0.8)
        for v in VIEWS:
            if fig: fig.hide_render = v != 'wide'
            pos, look, lens = cams[v]
            cd = bpy.data.cameras.new(v); cd.lens = lens; cam = bpy.data.objects.new(v, cd); sc.collection.objects.link(cam)
            cam.location = pos; cam.rotation_euler = (Vector(look) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
            sc.camera = cam; sc.render.filepath = os.path.join(OUT, f'{NAME}_{STAGE}_{mood}_{v}.jpg')
            bpy.ops.render.render(write_still=True); print('RENDER', sc.render.filepath)

# ----------------------------------------------------------------------------- main
K.reset()
highs, unbaked = build()
print('HIGH TRIS', {h.name: K.tris(h) for h in highs}, 'total', sum(K.tris(h) for h in highs))
if STAGE == 'bake':
    pdir = os.path.join(OUT, 'props'); os.makedirs(pdir, exist_ok=True)
    low = K.bake_prop(NAME, highs, pdir, target=int(arg('--tris', '45000')), res=RES)
    print('LOW TRIS', K.tris(low))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(pdir, NAME + '.blend'))
if '--no-render' not in argv:
    render(None)
