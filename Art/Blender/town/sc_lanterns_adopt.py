"""B8 lanterns for the beauty corner (PHASE1 4, B8; docs/art/beauty-corner-brief.md): two adopted Poly Haven lanterns,
restyled chunky in the locked style: thicker iron, warm glass, soot climbing the upper glass and the cap, 10% oversized.

  SC_B8_MarketLantern  wooden_lantern_01 (CC0, James Ray Cock): the stall's hanging lantern. Body widened 15% and raised
                       10%, the carry handle thickened, a forged S-hook and ring added on top, a stub candle inside.
                       Origin = the top of the hook (hang it from the awning beam). About 0.5 m tall.
  SC_B8_DoorLantern    Lantern_01 (CC0, Rajil Jose Macatangay): the wall lantern beside the facade door. Brass regraded
                       to blackened wrought iron with worn-bright edges, decimated 30.8k -> about 6k tris, thickened,
                       scaled from 0.29 m to a real wall-lantern size + 10% (0.46 m), hung from a forged wall bracket
                       (wall plate, square-bar arm with an end curl, scrolled brace). Origin = the back of the wall plate
                       at its centre (y 0 is the wall face; the bracket points -Y, Unity +z: rotate 180 about Y on the facade).
Each lantern is ONE texture set re-baked onto a fresh UV layout (the sources' UVs overlap or tile): the source maps are
sampled through their own UVs and graded, the generated iron is a procedural wrought iron, soot is added by height, the
glass is warm amber whose alpha rises with the soot. Two material slots share the set: M_<name> (opaque, alpha clip 0.5)
and M_<name>_Glass (URP Lit Transparent, uses _BaseColor alpha). ORM = R occlusion, G roughness, B metallic.
In Unity: a warm point light (about 2700 K) at the candle / burner (LIGHT below) for dusk; unlit at midday.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_lanterns_adopt.py -- --out <scratch>
      [--res 2048] [--samples 64] [--rres 960] [--export]   (--export writes Assets/_Project/Content/Art/Town/B8/<Lantern>/)

Status (2026-10-30, PC): exported at 2048. Calibrated review 27/40 (round 1 22: tris over budget, clear glass, door lantern
too small at mid distance; round 2: decimated to 5.7k / 5.8k, amber glass, door lantern 0.52 m on a 0.46 m bracket).
"""
import sys, os, math, random, shutil
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
import sc_b9_lib as L

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
RES = int(arg('--res', '1024')); SAMPLES = int(arg('--samples', '64')); RRES = int(arg('--rres', '960')); EXPORT = '--export' in argv
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
PH = os.path.join(REPO, 'Art', 'Source', 'PolyHaven')
EXP = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B8')
TEX = os.path.join(OUT, 'tex'); os.makedirs(TEX, exist_ok=True)
LIGHT = {}

IRON_D, IRON_L = '#221e1b', '#4d463f'; SOOT = '#17120f'; GLASS = '#d99a3e'; WAX = '#e6d6b0'

def import_gltf(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update()
    for o in new:
        if o.type == 'MESH':
            o.data = o.data.copy(); o.data.transform(o.matrix_world)
    for o in new:
        if o.type == 'MESH': o.parent = None; o.matrix_world = Matrix.Identity(4)
    for o in [o for o in new if o.type != 'MESH']: bpy.data.objects.remove(o)
    return [o for o in new if o.type == 'MESH']

def join(objs, name):
    for o in bpy.context.view_layer.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active; ob.name = name; ob.data.name = name
    return ob

def weld(ob, d=0.0002):
    bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=d); bm.to_mesh(ob.data); bm.free()

def bbox(ob):
    """Mesh-space bounds from the vertices (bound_box goes stale after data.transform)."""
    co = np.zeros(len(ob.data.vertices) * 3, np.float32); ob.data.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    return Vector(co.min(0)), Vector(co.max(0))

def decimate(ob, target):
    t0 = L.tri_count(ob)
    if t0 <= target: return
    m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = target / t0; m.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='dec'); ob.data.update()

def iron_tube(name, pts, r, sides=6, square=False):
    """Forged bar along a polyline (square=True: 4 sides turned 45 deg, wrought square stock)."""
    bm = bmesh.new(); pts = [Vector(p) for p in pts]; n = len(pts); rings = []
    tans = [(pts[min(k + 1, n - 1)] - pts[max(k - 1, 0)]).normalized() for k in range(n)]
    a = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0)); u = tans[0].cross(a).normalized()
    sides = 4 if square else sides; off = math.pi / 4 if square else 0
    for k, p in enumerate(pts):
        t = tans[k]
        if k: u = (u - t * u.dot(t)).normalized()
        w = t.cross(u); rr = r[k] if hasattr(r, '__len__') else r
        rings.append([bm.verts.new(p + (u * math.cos(off + 2 * math.pi * i / sides) + w * math.sin(off + 2 * math.pi * i / sides)) * rr) for i in range(sides)])
    for A, B in zip(rings[:-1], rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides; bm.faces.new((A[i], A[j], B[j], B[i]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    if square:
        m = ob.modifiers.new('bev', 'BEVEL'); m.width = (r[0] if hasattr(r, '__len__') else r) * 0.35; m.segments = 1; m.limit_method = 'ANGLE'
        bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='bev')
    for p in ob.data.polygons: p.use_smooth = True
    return ob

def ring(name, centre, radius, r, axis='Y', segs=20):
    pts = []
    for i in range(segs + 1):
        a = 2 * math.pi * i / segs
        d = Vector((math.cos(a), 0, math.sin(a))) if axis == 'Y' else Vector((0, math.cos(a), math.sin(a)))
        pts.append(Vector(centre) + d * radius)
    return iron_tube(name, pts, r, sides=6)

def scroll(c, r0, r1, turns, a0, plane='YZ', n=40):
    pts = []
    for i in range(n + 1):
        t = i / n; a = a0 + turns * 2 * math.pi * t; rr = r0 + (r1 - r0) * t
        pts.append(Vector(c) + (Vector((0, math.cos(a), math.sin(a))) if plane == 'YZ' else Vector((math.cos(a), 0, math.sin(a)))) * rr)
    return pts

# ----------------------------------------------------------------------------- bake material
def bake_material(name, src_prefix, kind, h0, h1, soot_from=0.7):
    """kind: 'wood' (keep the source, warmer, its grey bits darkened to iron), 'brass2iron', 'iron' (procedural), 'glass', 'wax'.
    Outputs on a Principled: Base Color, Roughness, Metallic, Normal, Alpha (all read by the EMIT/NORMAL bakes)."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; N = nt.nodes; Lk = nt.links
    bs = next(n for n in N if n.type == 'BSDF_PRINCIPLED')
    def node(t, **kw):
        n = N.new(t)
        for k, v in kw.items(): setattr(n, k, v)
        return n
    def math_(op, a, b=None):
        n = node('ShaderNodeMath', operation=op)
        for i, x in enumerate((a, b)):
            if x is None: continue
            if isinstance(x, (int, float)): n.inputs[i].default_value = x
            else: Lk.new(x, n.inputs[i])
        return n.outputs[0]
    def mix(fac, a, b, blend='MIX'):
        n = node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        for sock, x in (('Factor', fac), ('A', a), ('B', b)):
            if isinstance(x, (int, float)): n.inputs[sock].default_value = x
            elif isinstance(x, tuple): n.inputs[sock].default_value = x
            else: Lk.new(x, n.inputs[sock])
        return n.outputs['Result']
    rgba = lambda h: (*L.hex2lin(h), 1.0)
    geo = node('ShaderNodeNewGeometry'); tc = node('ShaderNodeTexCoord')
    sep = node('ShaderNodeSeparateXYZ'); Lk.new(tc.outputs['Object'], sep.inputs[0])
    hz = math_('DIVIDE', math_('SUBTRACT', sep.outputs['Z'], h0), h1 - h0)                        # 0 at the foot, 1 at the top
    nz = node('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 28.0; nz.inputs['Detail'].default_value = 6
    Lk.new(tc.outputs['Object'], nz.inputs['Vector'])
    mr = node('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = soot_from; mr.inputs['From Max'].default_value = 1.02
    Lk.new(hz, mr.inputs['Value'])
    soot = math_('MULTIPLY', math_('ADD', mr.outputs[0], math_('MULTIPLY', math_('SUBTRACT', nz.outputs['Fac'], 0.5), 0.6)), 1.0)
    soot = math_('MINIMUM', math_('MAXIMUM', soot, 0.0), 0.92)
    out_col = out_rough = out_metal = None; out_alpha = 1.0
    if src_prefix:
        uvm = node('ShaderNodeUVMap', uv_map='UVMap')
        def src(key, nonc):
            t = node('ShaderNodeTexImage'); t.image = bpy.data.images.load(f'{src_prefix}_{key}_2k.jpg', check_existing=True)
            t.image.colorspace_settings.name = 'Non-Color' if nonc else 'sRGB'; Lk.new(uvm.outputs['UV'], t.inputs['Vector']); return t
        dif = src('diff', False); arm = src('arm', True); nor = src('nor_gl', True)
        sarm = node('ShaderNodeSeparateColor'); Lk.new(arm.outputs['Color'], sarm.inputs[0])
        nm = node('ShaderNodeNormalMap', uv_map='UVMap'); nm.inputs['Strength'].default_value = 1.2; Lk.new(nor.outputs['Color'], nm.inputs['Color'])
        Lk.new(nm.outputs['Normal'], bs.inputs['Normal'])
        if kind == 'wood':
            hsv = node('ShaderNodeHueSaturation'); hsv.inputs['Saturation'].default_value = 1.12; hsv.inputs['Value'].default_value = 1.05
            Lk.new(dif.outputs['Color'], hsv.inputs['Color'])
            # metallic parts of the source (hinges, cap) become dark wrought iron
            bw = node('ShaderNodeRGBToBW'); Lk.new(dif.outputs['Color'], bw.inputs[0])
            iron = mix(math_('POWER', bw.outputs[0], 0.8), rgba(IRON_D), rgba(IRON_L))
            out_col = mix(sarm.outputs['Blue'], hsv.outputs['Color'], iron)
            out_rough = math_('ADD', sarm.outputs['Green'], 0.05); out_metal = sarm.outputs['Blue']
        else:   # brass2iron: brass value structure mapped onto blackened iron, its brightest worn edges a dull warm steel
            bw = node('ShaderNodeRGBToBW'); Lk.new(dif.outputs['Color'], bw.inputs[0])
            v = math_('POWER', math_('MULTIPLY', bw.outputs[0], 1.9), 1.15)
            out_col = mix(v, rgba(IRON_D), rgba(IRON_L))
            edge = math_('MAXIMUM', math_('SUBTRACT', math_('MULTIPLY', bw.outputs[0], 3.0), 1.25), 0.0)
            out_col = mix(math_('MINIMUM', edge, 0.6), out_col, rgba('#7c6a55'))
            out_rough = math_('ADD', math_('MULTIPLY', sarm.outputs['Green'], 0.5), 0.42); out_metal = 0.85
    elif kind == 'iron':
        b2 = node('ShaderNodeBevel'); b2.inputs['Radius'].default_value = 0.003
        dp = node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
        Lk.new(b2.outputs['Normal'], dp.inputs[0]); Lk.new(geo.outputs['Normal'], dp.inputs[1])
        edge = math_('POWER', math_('SUBTRACT', 1.0, math_('ABSOLUTE', dp.outputs['Value'])), 6.0)        # rounded edges: worn bright
        hammer = node('ShaderNodeTexVoronoi'); hammer.inputs['Scale'].default_value = 90.0; Lk.new(tc.outputs['Object'], hammer.inputs['Vector'])
        out_col = mix(math_('MULTIPLY', nz.outputs['Fac'], 0.8), rgba(IRON_D), rgba('#3a332d'))
        rust = math_('MAXIMUM', math_('MULTIPLY', math_('SUBTRACT', nz.outputs['Fac'], 0.62), 4.0), 0.0)
        out_col = mix(math_('MINIMUM', rust, 0.55), out_col, rgba('#5e3420'))
        out_col = mix(math_('MINIMUM', math_('MULTIPLY', edge, 3.0), 0.7), out_col, rgba('#6e6359'))
        out_rough = math_('ADD', math_('MULTIPLY', hammer.outputs['Distance'], 0.4), 0.45); out_metal = 0.85
        bump = node('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.25; bump.inputs['Distance'].default_value = 0.002
        Lk.new(hammer.outputs['Distance'], bump.inputs['Height']); Lk.new(b2.outputs['Normal'], bump.inputs['Normal']); Lk.new(bump.outputs['Normal'], bs.inputs['Normal'])
    elif kind == 'glass':
        out_col = mix(math_('MULTIPLY', nz.outputs['Fac'], 0.25), rgba(GLASS), rgba('#f4d79a'))
        out_rough = 0.12; out_metal = 0.0
        out_alpha = math_('ADD', 0.5, math_('MULTIPLY', soot, 0.5))
    elif kind == 'wax':
        drip = math_('MULTIPLY', nz.outputs['Fac'], 0.3)
        out_col = mix(drip, rgba(WAX), rgba('#c9b27e')); out_rough = 0.55; out_metal = 0.0
    out_col = mix(soot, out_col, rgba(SOOT))                                           # soot climbs every material
    out_rough = math_('ADD', out_rough if not isinstance(out_rough, float) else math_('ADD', out_rough, 0.0), math_('MULTIPLY', soot, 0.35))
    Lk.new(out_col, bs.inputs['Base Color'])
    Lk.new(math_('MINIMUM', out_rough, 1.0), bs.inputs['Roughness'])
    if isinstance(out_metal, float): bs.inputs['Metallic'].default_value = out_metal
    else: Lk.new(out_metal, bs.inputs['Metallic'])
    if isinstance(out_alpha, float): bs.inputs['Alpha'].default_value = out_alpha
    else: Lk.new(out_alpha, bs.inputs['Alpha'])
    return m

def bake(ob, prefix, res, samples):
    """EMIT/NORMAL/AO bakes of the bake materials into UVBake; writes _BaseColor (alpha), _Normal, _ORM."""
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'
    try:
        p = bpy.context.preferences.addons['cycles'].preferences; p.compute_device_type = 'OPTIX'; p.get_devices()
        for d in p.devices: d.use = d.type == 'OPTIX'
        sc.cycles.device = 'GPU'
    except Exception: pass
    sc.cycles.samples = samples; sc.cycles.use_denoising = False
    bk = sc.render.bake; bk.margin = 12; bk.use_selected_to_active = False; bk.use_clear = True
    ob.data.uv_layers.active = ob.data.uv_layers['UVBake']; ob.data.uv_layers['UVBake'].active_render = True
    for o in bpy.context.view_layer.objects: o.select_set(False)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    imgs = {}
    def target(key):
        img = bpy.data.images.new(f'{prefix}_{key}', res, res, alpha=False, float_buffer=True)
        img.colorspace_settings.name = 'Non-Color'
        for s in ob.material_slots:
            nt = s.material.node_tree
            for n in [n for n in nt.nodes if n.name == '__bake']: nt.nodes.remove(n)
            n = nt.nodes.new('ShaderNodeTexImage'); n.name = '__bake'; n.image = img; nt.nodes.active = n
            uv = nt.nodes.new('ShaderNodeUVMap'); uv.uv_map = 'UVBake'; nt.links.new(uv.outputs['UV'], n.inputs['Vector'])
        return img
    def route(sock):
        for s in ob.material_slots:
            nt = s.material.node_tree; bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
            out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
            if sock is None:
                for n in [n for n in nt.nodes if n.name == '__em']: nt.nodes.remove(n)
                nt.links.new(bs.outputs['BSDF'], out.inputs['Surface']); continue
            em = nt.nodes.new('ShaderNodeEmission'); em.name = '__em'
            si = bs.inputs[sock]
            if si.is_linked: nt.links.new(si.links[0].from_socket, em.inputs['Color'])
            else:
                v = si.default_value; em.inputs['Color'].default_value = (v[0], v[1], v[2], 1) if hasattr(v, '__len__') else (v, v, v, 1)
            nt.links.new(em.outputs[0], out.inputs['Surface'])
    def grab(img):
        a = np.empty(res * res * 4, np.float32); img.pixels.foreach_get(a); return a.reshape(res, res, 4)[..., :3]
    for key, sock in (('col', 'Base Color'), ('rough', 'Roughness'), ('metal', 'Metallic'), ('alpha', 'Alpha')):
        img = target(key); route(sock); bpy.ops.object.bake(type='EMIT'); route(None); imgs[key] = grab(img)
    img = target('nrm'); bk.normal_space = 'TANGENT'; bpy.ops.object.bake(type='NORMAL'); imgs['nrm'] = grab(img)
    sc.world = sc.world or bpy.data.worlds.new('W'); sc.world.light_settings.distance = 0.05
    sc.cycles.samples = max(samples, 128)
    img = target('ao'); bpy.ops.object.bake(type='AO'); imgs['ao'] = grab(img)
    for s in ob.material_slots:
        nt = s.material.node_tree
        for n in [n for n in nt.nodes if n.name == '__bake' or (n.type == 'UVMAP' and n.uv_map == 'UVBake')]: nt.nodes.remove(n)
    col = L.lin2srgb(imgs['col']); alpha = np.clip(imgs['alpha'][..., 0], 0, 1)
    ao = np.clip(0.35 + 0.65 * imgs['ao'][..., 0], 0, 1)
    L.save(col, prefix + '_BaseColor.png', alpha)
    L.save(imgs['nrm'], prefix + '_Normal.png')
    L.save(np.stack([ao, np.clip(imgs['rough'][..., 0], 0, 1), np.clip(imgs['metal'][..., 0], 0, 1)], -1), prefix + '_ORM.png')

def finish(ob, name, glass_slots):
    """Smart-UV a fresh layout, bake, then swap to the two game materials (opaque + glass) sharing the set."""
    for o in bpy.context.view_layer.objects: o.select_set(False)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    if 'UVMap' not in ob.data.uv_layers: ob.data.uv_layers.new(name='UVMap')
    uvb = ob.data.uv_layers.new(name='UVBake'); ob.data.uv_layers.active = uvb
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.006, area_weight=0.0, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    prefix = os.path.join(TEX, f'T_{name}')
    bake(ob, prefix, RES, SAMPLES)
    # final: keep only UVBake (as UVMap), two materials
    me = ob.data
    me.uv_layers.remove(me.uv_layers['UVMap']); me.uv_layers['UVBake'].name = 'UVMap'
    opaque = L.pbr_material(f'M_{name}', prefix, clip=False, two_sided=False)   # opaque: the alpha is the glass's, never clip it
    glass = L.pbr_material(f'M_{name}_Glass', prefix, clip=False, two_sided=True, alpha_blend=True)
    gi = [i for i, s in enumerate(ob.material_slots) if s.material.name in glass_slots]
    idx = np.array([1 if p.material_index in gi else 0 for p in me.polygons], np.int32)
    me.materials.clear(); me.materials.append(opaque); me.materials.append(glass)
    me.polygons.foreach_set('material_index', idx); me.update()
    for p in me.polygons: p.use_smooth = True
    return prefix

# ----------------------------------------------------------------------------- the two lanterns
def market_lantern():
    src = os.path.join(PH, 'wooden_lantern_01')
    parts = import_gltf(os.path.join(src, 'wooden_lantern_01_2k.gltf'))
    ob = join(parts, 'SC_B8_MarketLantern'); weld(ob); decimate(ob, 5200)
    lo, hi = bbox(ob)
    # the carry handle (everything above 80% of the height) is iron: thicken it
    me = ob.data; me.update(); cap = lo.z + (hi.z - lo.z) * 0.80
    for v in me.vertices:
        if v.co.z > cap: v.co += v.normal * 0.0022
    # chunky: 15% wider, 10% taller, then the 10% storybook oversize
    S = Matrix.Diagonal((1.15 * 1.1, 1.15 * 1.1, 1.1 * 1.1, 1.0))
    me.transform(Matrix.Translation((0, 0, -lo.z))); me.transform(S); me.update()
    lo, hi = bbox(ob); top = hi.z
    wood = bake_material('bk_wood', os.path.join(src, 'textures', 'wooden_lantern_01'), 'wood', 0.0, top, soot_from=0.72)
    glass = bake_material('bk_glass', None, 'glass', 0.0, top, soot_from=0.55)
    iron = bake_material('bk_iron', None, 'iron', 0.0, top + 0.12, soot_from=0.9)
    wax = bake_material('bk_wax', None, 'wax', 0.0, top)
    for s in ob.material_slots:
        s.material = glass if 'glass' in s.material.name else wood
    # forged S-hook and ring above the handle
    hook = iron_tube('hook', [Vector((0, 0, top - 0.012)), Vector((0, 0, top + 0.03))] +
                     [Vector((0, 0.022 * math.sin(a), top + 0.055 + 0.022 * -math.cos(a))) for a in np.linspace(0, math.pi * 1.6, 12)], 0.0055)
    rng_ = ring('ring', (0, 0, top + 0.11), 0.028, 0.006, axis='X')
    candle = None
    # stub candle on the lantern floor (inside the glass), with a wax drip collar
    lo_g = min((v.co.z for p in me.polygons if ob.material_slots[p.material_index].material == glass for v in [me.vertices[i] for i in p.vertices]), default=0.06)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.026, depth=0.075, vertices=14, location=(0, 0, lo_g + 0.0375)); candle = bpy.context.active_object
    LIGHT['SC_B8_MarketLantern'] = (0, 0, lo_g + 0.09)
    for o, m in ((hook, iron), (rng_, iron), (candle, wax)):
        o.data.materials.clear(); o.data.materials.append(m)
    ob = join([ob, hook, rng_, candle], 'SC_B8_MarketLantern')
    # origin at the top of the ring
    lo, hi = bbox(ob); ob.data.transform(Matrix.Translation((0, 0, -hi.z))); LIGHT['SC_B8_MarketLantern'] = tuple(Vector(LIGHT['SC_B8_MarketLantern']) - Vector((0, 0, hi.z)))
    return ob, finish(ob, 'SC_B8_MarketLantern', {'bk_glass'})

def door_lantern():
    src = os.path.join(PH, 'Lantern_01')
    parts = import_gltf(os.path.join(src, 'Lantern_01_2k.gltf'))
    body = [o for o in parts if 'glass' not in o.name][0]; gl = [o for o in parts if 'glass' in o.name][0]
    weld(body, 0.0001)
    t0 = L.tri_count(body); decimate(body, 4200); weld(gl, 0.0001); decimate(gl, 500)
    for v in body.data.vertices: v.co += v.normal * 0.0012                     # thicker iron
    print('door lantern body', t0, '->', L.tri_count(body), 'glass', L.tri_count(gl))
    ob = join([body, gl], 'SC_B8_DoorLantern')
    lo, hi = bbox(ob); s = 0.52 / (hi.z - lo.z)
    ob.data.transform(Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -hi.z))); ob.data.transform(Matrix.Scale(s, 4))
    lo, hi = bbox(ob)
    brass = bake_material('bk_brass', os.path.join(src, 'textures', 'Lantern_01_brass'), 'brass2iron', lo.z, hi.z, soot_from=0.62)
    glass = bake_material('bk_glass2', None, 'glass', lo.z, hi.z, soot_from=0.45)
    iron = bake_material('bk_iron2', None, 'iron', lo.z, hi.z + 0.25, soot_from=0.95)
    for sl in ob.material_slots:
        sl.material = glass if 'glass' in sl.material.name else brass
    LIGHT['SC_B8_DoorLantern_rel'] = (0, 0, lo.z + (hi.z - lo.z) * 0.38)
    # wall bracket: plate on the wall (y 0), arm out along -Y, end curl, a scrolled brace under it
    arm_z = 0.07; reach = 0.46
    hang = Vector((0, -reach + 0.03, arm_z - 0.015))
    plate = iron_tube('plate', [Vector((0, -0.009, -0.22)), Vector((0, -0.009, 0.14))], 0.04, square=True)
    plate.data.transform(Matrix.Diagonal((1.0, 0.32, 1.0, 1.0)))                       # a flat strap plate
    arm = iron_tube('arm', [Vector((0, -0.01, arm_z)), Vector((0, -reach * 0.5, arm_z + 0.004)), Vector((0, -reach, arm_z - 0.004))] +
                    [p for p in scroll((0, -reach, arm_z + 0.04), 0.042, 0.014, 0.9, -math.pi / 2, 'YZ', 10)][1:], 0.015, square=True)
    brace_pts = [Vector((0, -0.01, -0.16))] + [Vector((0, -0.01 - 0.25 * t, -0.16 + (arm_z - 0.012 + 0.16) * t ** 0.7)) for t in np.linspace(0.1, 1.0, 6)]
    brace = iron_tube('brace', brace_pts, 0.011, square=True)
    curl = iron_tube('curl', scroll((0, -0.12, -0.03), 0.065, 0.014, 1.15, math.pi * 0.2, 'YZ', 18), 0.008, square=True)
    hook = iron_tube('hook2', [hang + Vector((0, 0, 0.012)), hang + Vector((0, 0, -0.02))] + [hang + Vector((0, 0.014 * math.sin(a), -0.035 - 0.014 * math.cos(a))) for a in np.linspace(0, math.pi * 1.5, 9)], 0.005)
    bolts = []
    for z in (-0.17, 0.10):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.014, segments=10, ring_count=6, location=(0, -0.016, z)); b = bpy.context.active_object
        b.scale = (1, 0.5, 1); bpy.ops.object.transform_apply(scale=True); bolts.append(b)
    for o in [plate, arm, brace, curl, hook] + bolts:
        o.data.materials.clear(); o.data.materials.append(iron)
    # hang the lantern: its top (z 0) under the hook
    ob.data.transform(Matrix.Translation(hang + Vector((0, 0, -0.045))))
    LIGHT['SC_B8_DoorLantern'] = tuple(Vector(LIGHT['SC_B8_DoorLantern_rel']) + hang + Vector((0, 0, -0.045)))
    ob = join([ob, plate, arm, brace, curl, hook] + bolts, 'SC_B8_DoorLantern')
    return ob, finish(ob, 'SC_B8_DoorLantern', {'bk_glass2'})

# ----------------------------------------------------------------------------- review
def review(lanterns):
    rdir = os.path.join(OUT, 'review'); os.makedirs(rdir, exist_ok=True)
    stand = L.review_scene()
    R = Matrix.Rotation(math.pi, 4, 'Z')
    mk, dr = lanterns
    # market lantern hangs from the awning's front edge (stand-in awning bottom z 2.6); door lantern beside the door at 2.35 m
    mk.matrix_world = Matrix.Translation((-4.0, -3.35, 2.6)); dr.matrix_world = Matrix.Translation((1.95, -7.5, 2.35)) @ R
    L.render(L.camera('mid', *L.CAMS['mid']), os.path.join(rdir, 'b8_mid.png'), RRES, SAMPLES)
    L.render(L.camera('wide', *L.CAMS['wide']), os.path.join(rdir, 'b8_wide.png'), RRES, SAMPLES)
    L.render(L.camera('doorc', (-1.0, 2.0, 5.3), (-1.9, 2.1, 7.4), fov_y=40), os.path.join(rdir, 'b8_door.png'), RRES, SAMPLES)
    L.render(L.camera('stallc', (3.3, 2.1, 2.2), (4.0, 2.25, 3.35), fov_y=40), os.path.join(rdir, 'b8_stall.png'), RRES, SAMPLES)
    print('REVIEW', rdir)

if __name__ == '__main__':
    bpy.ops.wm.read_homefile(use_empty=True)
    mk, mkp = market_lantern()
    dr, drp = door_lantern()
    for o in (mk, dr): print(o.name, 'tris', L.tri_count(o), 'dims', [round(d, 3) for d in o.dimensions])
    LIGHT.pop('SC_B8_DoorLantern_rel', None); print('LIGHT', LIGHT)
    os.makedirs(os.path.join(OUT, 'fbx'), exist_ok=True)
    for o in (mk, dr): L.export_fbx([o], os.path.join(OUT, 'fbx', o.name + '.fbx'))
    if EXPORT:
        for o, pre, folder in ((mk, mkp, 'MarketLantern'), (dr, drp, 'DoorLantern')):
            d = os.path.join(EXP, folder); L.export_fbx([o], os.path.join(d, o.name + '.fbx'))
            for k in ('BaseColor', 'Normal', 'ORM'): shutil.copy(f'{pre}_{k}.png', os.path.join(d, f'T_{o.name}_{k}.png'))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'b8_lanterns.blend'))
    review((mk, dr))
