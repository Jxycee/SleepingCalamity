"""MarketStall: B8 market dressing (T2, PHASE1 4), the one stall of the Hollowmere beauty corner.

A storybook timber market stall, 2.6 m wide x 1.6 m deep, awning top 2.7 m. Chunky ochre-painted frame (posts 13 cm, about
1.4x real, each 1-2 deg out of true) on sandstone pads; knee braces and deep pegged lintels; a chunky planked oak counter
at 0.98 m with a shaped madder-trim apron board and iron corner straps; a closed front of nailed vertical boards below
it (one a fresh unpainted replacement);
a two-step display shelf behind the counter; a striped ochre and madder canvas awning sagging between three
rafter poles, with a scalloped valance on the front and both sides and a plain back cloth; a pennant string slung
across the opening under the lintel; an arched produce sign (carved apple) on the front bar; a bound wheat sheaf hung
head-down on each front post; an iron lantern hook on the fountain-side front post; a cream counter cloth with madder
dots and an ochre border lying on the counter centre and draped over the apron (frames the goods); a small loaf sign
on a stake just outside the fountain-side front corner (brief 5.5, 2026-10-30). Medium whimsy (controller, 2026-10-30).
Wear beats (each visible at the three-quarter and mid views): paint chipped at hand height and on edges, showing
dark oak; a dark splash-and-grime band along the foot; the counter top worn pale and polished where goods slide;
sun-faded canvas tops and tide stains at the valance scallops; one replaced pale board in the painted front.
Contrast pair: madder and straw-ochre canvas against the deep ochre frame (ochre so it reads against the facade's green
shutters), pale valance against the dark back cloth.

Landing spots for B7 props (flat within 3 mm): counter top x -1.30..1.30, y -0.86..-0.30, z 0.98 (the cloth adds 7 mm
over x -0.72..0.72: the goods cluster belongs on it);
shelf tier 1 x -1.10..1.10, y -0.24..-0.04, z 1.15; tier 2 y -0.02..0.20, z 1.33. Lantern hook eye at
(-1.25, -1.10, 1.94) (B8 lantern hangs here). All in the authoring frame below.

Authored in Blender axes: z up, front -Y, metres, origin at the bottom centre (FBX -Z forward, Y up: the front becomes
Unity +Z). The fountain-facing side is the stall's -X side (Blender) once placed at Unity (4.5, 0, 4.5) yaw 200; the
mid camera sees the +X side, so both sides carry the valance and paint wear; the back is plain.
Tier T2: parts carry metre UVs ('UVMap', 2.0 m per tile) on the shared B1 tileables (painted_wood, oak_timber,
canvas, iron, sandstone) plus procedural masks (chips, grime, polish, stripes, fade). --stage bake joins the stall,
adds an 'atlas' UV and bakes the masked look into one set (BaseColor, Normal, Roughness, Metallic, AO) so Unity gets
one material; the PC runs the 2K bake and the export.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_market_stall.py -- --out <scratch>
      [--stage look|bake]   look (default): procedural materials rendered directly; bake: join + atlas bake + render
      [--res 1024]          bake resolution (PC final: --res 2048)
      [--rres 960] [--samples 32] [--views wide,mid,tq,front,side] [--paint ochre|green] [--no-render] [--export]
Renders go to <out>/stall_<stage>_<view>.png; the bake writes <out>/maps/MarketStall_<Map>.png; --export writes
<out>/MarketStall.fbx. Cloud sessions commit this generator only (sc-asset-craft 4).
Budget: 35.3k tris (a compound dressing piece between kit and hero in PHASE1 3.3: sc_asset_audit's 'prop' class
FAILs at 6k, audit it as --class hero), one 2K set, 1 material.
Collider (at placement): a box over the counter and frame, 2.7 x 1.0 x 1.6 m, plus a thin box for the sign stake at
(-1.43, -0.92); the awning overhang has none.

Status (2026-10-30, cloud): relaxed review 30/40 on the --stage bake --res 1024 proof (rounds 25 -> 27 -> 28.5 -> 29,
then the brief 5.5 layers lift colour to 4 and adze-faceted posts and lintels lift form to 4). Lines: silhouette 4, proportion 3.5, form 4,
surface 3.5, colour 4, wear 4, fits set 3 (judged against proxies only), metal/efficiency 3.5. Audit PASS --class hero.
Next: the PC runs --stage bake --res 2048 --export and judges it in the Unity corner. Left: nail rows and grime band sit
in shade at mid; a stall lantern on the hook (PC lantern batch); hanging price boards.
"""
import sys, os, math, zlib, random
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import bpy, bmesh
from mathutils import Vector, Matrix, noise

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
STAGE = arg('--stage', 'look'); RES = int(arg('--res', '1024')); RRES = int(arg('--rres', '960'))
SAMPLES = int(arg('--samples', '32')); VIEWS = arg('--views', 'wide,mid,tq,front,side').split(',')
PAINT_KIND = arg('--paint', 'ochre')
NAME = 'MarketStall'
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
TEX = os.path.join(REPO, 'Art', 'Textures', 'B1_town')
TILE = 2.0          # metres per tileable repeat (1024 px/m on a 2048 tile)
SEED = 8008

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode()))

def lin(hexs):
    c = [int(hexs[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c) + (1.0,)

# ----------------------------------------------------------------------------- palette (PHASE1 3.1: cloth and paint +10-15%)
PAINT = lin('#4d7044') if PAINT_KIND == 'green' else lin('#93703e')
OCHRE, MADDER = lin('#d2ae62'), lin('#9c3b2c')
TRIM = lin('#8c3a2b')            # madder trim paint: apron, shelf lips, sign border
CREAM, TEAL = lin('#d8c9a2'), lin('#4c7682')
ROPE = lin('#9a845e')

# ----------------------------------------------------------------------------- dimensions (metres)
W, D = 2.6, 1.6
POST = 0.13                        # post section (about 1.4x real)
LIN_H = 0.19                       # lintel depth (chunky: about 1.4x real)
PX, PY = W / 2 - POST / 2, D / 2 - POST / 2
PAD_H = 0.07                       # sandstone pad under each post
COUNTER_Z = 0.98
AW_W = 2.84                        # awning width (overhangs the side rafters by 0.12)
AW_BACK = (0.86, 2.70)             # (y, z) back edge of the canvas
AW_FRONT = (-1.12, 2.30)           # (y, z) front bar
RAFT_X = (-1.30, 0.0, 1.30); RAFT_R = 0.045
N_STRIPE = 12; SW = AW_W / N_STRIPE

def canvas_z(y):
    t = (AW_BACK[0] - y) / (AW_BACK[0] - AW_FRONT[0])
    return AW_BACK[1] + (AW_FRONT[1] - AW_BACK[1]) * t
def lintel_top(y):
    return canvas_z(y) - 2 * RAFT_R - 0.004 - 0.045 + 0.04   # rafters sit half into a notch in the beam

# ----------------------------------------------------------------------------- scene + part helpers
bpy.ops.wm.read_homefile(use_empty=True)
SC = bpy.context.scene; SC.unit_settings.system = 'METRIC'
ROOT = bpy.data.objects.new(NAME, None); SC.collection.objects.link(ROOT)
PARTS = []

def obj_from_bm(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); SC.collection.objects.link(ob)
    return ob

def uv_metres(bm, grain, off):
    """Planar metre UVs per face, the u axis along the grain axis on every face that contains it."""
    uv = bm.loops.layers.uv.get('UVMap') or bm.loops.layers.uv.new('UVMap')
    bm.normal_update()
    for f in bm.faces:
        n = f.normal; d = max(range(3), key=lambda a: abs(n[a]))
        rest = [a for a in range(3) if a != d]
        ua, va = (grain, [a for a in rest if a != grain][0]) if grain in rest else rest
        for lp in f.loops:
            lp[uv].uv = (lp.vert.co[ua] / TILE + off[0], lp.vert.co[va] / TILE + off[1])

def apply_mods(ob):
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    for m in list(ob.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)

def bevel(ob, w, segs=2, ang=40):
    m = ob.modifiers.new('bev', 'BEVEL'); m.width = w; m.segments = segs; m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(ang); m.harden_normals = True; m.miter_outer = 'MITER_ARC'

def warp(ob, amp, scale, seed, axes=(1, 1, 1)):
    o = Vector((seed * 13.1, seed * 7.7, seed * 3.3))
    for v in ob.data.vertices:
        p = v.co / scale + o
        d = Vector((noise.noise(p), noise.noise(p + Vector((5.2, 1.3, 2.8))), noise.noise(p + Vector((1.7, 9.2, 4.4)))))
        v.co += Vector((d.x * axes[0], d.y * axes[1], d.z * axes[2])) * amp

def bow(ob, axis, up, amount):
    """Bend a long part: the middle moves by `amount` along `up`."""
    vs = ob.data.vertices
    lo = min(v.co[axis] for v in vs); hi = max(v.co[axis] for v in vs)
    for v in vs:
        v.co[up] += amount * math.sin(math.pi * (v.co[axis] - lo) / max(hi - lo, 1e-6))

def set_attrs(ob, tint=(1, 1, 1, 1), stripe=0.0, vh=None):
    me = ob.data
    a = me.attributes.get('tint') or me.attributes.new('tint', 'FLOAT_COLOR', 'FACE')
    for d in a.data: d.color = tint
    s = me.attributes.get('stripe') or me.attributes.new('stripe', 'FLOAT', 'FACE')
    if isinstance(stripe, (int, float)):
        for d in s.data: d.value = stripe
    else:
        for i, d in enumerate(s.data): d.value = stripe[i]
    h = me.attributes.get('vh') or me.attributes.new('vh', 'FLOAT', 'POINT')
    for i, d in enumerate(h.data): d.value = vh[i] if vh else 0.0

def finish(ob, mat, loc=(0, 0, 0), rot=(0, 0, 0), tint=None, stripe=0.0, vh=None):
    ob.data.materials.clear(); ob.data.materials.append(MATS[mat])
    for p in ob.data.polygons: p.use_smooth = True
    if not ob.data.attributes.get('tint'):
        set_attrs(ob, tint or (1, 1, 1, 1), stripe, vh)
    ob.location = loc; ob.rotation_euler = [math.radians(a) for a in rot]
    ob.parent = ROOT; PARTS.append(ob)
    bpy.context.view_layer.update()
    return ob

def adze_facets(ob, size, grain, depth, r):
    """Hewn timber: each long face is cut into flat adze bands along the grain, each band dished by its own depth."""
    seg = 0.17; marks = {}
    for v in ob.data.vertices:
        rest = [a for a in range(3) if a != grain]
        side = max(rest, key=lambda a: abs(v.co[a]) / (size[a] / 2))
        sgn = 1 if v.co[side] > 0 else -1
        k = math.floor((v.co[grain] + size[grain] / 2) / seg)
        key = (side, sgn, k)
        if key not in marks: marks[key] = r.uniform(0.2, 1.0) * depth
        t = ((v.co[grain] + size[grain] / 2) / seg) % 1.0
        v.co[side] -= sgn * marks[key] * (0.55 + 0.45 * math.sin(math.pi * t))   # scooped band, ridge between bands

def board(name, size, mat, loc, rot=(0, 0, 0), grain=0, cut=1, chamfer=0.008, wob=0.003, bend=0.0,
          bend_up=2, tint=None, stream=None, trim=0.0, adze=0.0):
    """A box part (centred), grain along local `grain` axis, chamfered, warped a little, metre UVs."""
    r = rng(stream or name)
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if cut > 1:
        for i in range(1, cut):
            t = -size[grain] / 2 + size[grain] * i / cut; no = Vector((0, 0, 0)); no[grain] = 1
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=no * t, plane_no=no)
    uv_metres(bm, grain, (r.random(), r.random()))
    ob = obj_from_bm(name, bm)
    if chamfer: bevel(ob, chamfer)
    apply_mods(ob)
    if wob: warp(ob, wob, 0.5, r.random() * 50)
    if adze: adze_facets(ob, size, grain, adze, r)
    if bend: bow(ob, grain, bend_up, bend * (0.6 + 0.8 * r.random()))
    if tint is None:
        k = 0.92 + 0.16 * r.random(); tint = (k * (0.98 + 0.04 * r.random()), k, k * (0.97 + 0.05 * r.random()), 1)
    return finish(ob, mat, loc, rot, tint, trim)

def prism(name, pts, thick, mat, y0, chamfer=0.006, grain=0, tint=None, trim=0.0):
    """A flat board cut to a 2D outline: pts are (a, z) in the XZ plane (a = x), extruded along +Y from y0."""
    r = rng(name)
    bm = bmesh.new()
    vs = [bm.verts.new((a, y0, z)) for a, z in pts]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, thick, 0), verts=[v for v in ext['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    big = [f for f in bm.faces if len(f.verts) > 4]
    bmesh.ops.triangulate(bm, faces=big, quad_method='BEAUTY', ngon_method='BEAUTY')
    uv_metres(bm, grain, (r.random(), r.random()))
    ob = obj_from_bm(name, bm)
    if chamfer: bevel(ob, chamfer, 2, 50)
    apply_mods(ob); warp(ob, 0.002, 0.5, r.random() * 50)
    return finish(ob, mat, tint=tint or (1, 1, 1, 1), stripe=trim)

def pole(name, pts, rad, mat, sides=12, tint=None, cap=True):
    """A round pole or bar along a polyline (parallel-transport frames), u along its length in metres."""
    pts = [Vector(p) for p in pts]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    tans = [(pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized() for k in range(len(pts))]
    a = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    u = tans[0].cross(a).normalized(); rings = []; lens = [0.0]
    for k in range(1, len(pts)): lens.append(lens[-1] + (pts[k] - pts[k - 1]).length)
    for k, p in enumerate(pts):
        t = tans[k]
        if k: u = (u - t * u.dot(t)).normalized()
        v = t.cross(u); rr = rad[k] if isinstance(rad, (list, tuple)) else rad
        rings.append([bm.verts.new(p + (u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides)) * rr)
                      for i in range(sides)])
    r0 = rad[0] if isinstance(rad, (list, tuple)) else rad
    for k in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            f = bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
            for lp, (kk, ii) in zip(f.loops, ((k, i), (k, i + 1), (k + 1, i + 1), (k + 1, i))):
                lp[uvl].uv = (lens[kk] / TILE, ii * 2 * math.pi * r0 / sides / TILE)
    if cap:
        for ring in (rings[0][::-1], rings[-1]):
            f = bm.faces.new(ring)
            for lp in f.loops: lp[uvl].uv = (lp.vert.co.x / TILE, lp.vert.co.y / TILE)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = obj_from_bm(name, bm)
    return finish(ob, mat, tint=tint or (1, 1, 1, 1))

def grid_part(name, nu, nv, fn, mat, solid=0.005, stripe_fn=None, tint_fn=None, uvfn=None):
    """A cloth sheet: fn(i/nu, j/nv) -> (pos, uv, vh). stripe_fn(u_mid, v_mid) gives the face stripe index."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    ux, uy = bm.verts.layers.float.new('uvx'), bm.verts.layers.float.new('uvy')
    grid = []; vhs = []
    for j in range(nv + 1):
        row = []
        for i in range(nu + 1):
            p, uvc, vh = fn(i / nu, j / nv)
            vt = bm.verts.new(p); vt[ux] = uvc[0]; vt[uy] = uvc[1]
            row.append(vt); vhs.append(vh)
        grid.append(row)
    stripes = []; tints = []
    for j in range(nv):
        for i in range(nu):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            for lp in f.loops: lp[uvl].uv = (lp.vert[ux], lp.vert[uy])
            um, vm = (i + 0.5) / nu, (j + 0.5) / nv
            stripes.append(stripe_fn(um, vm) if stripe_fn else 0.0)
            tints.append(tint_fn(um, vm) if tint_fn else (1, 1, 1, 1))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = obj_from_bm(name, bm)
    if ob.data.polygons[len(ob.data.polygons) // 2].normal.z < -0.2:   # keep the cloth's outside up
        ob.data.flip_normals()
    set_attrs(ob, stripe=stripes, vh=vhs)
    ta = ob.data.attributes['tint']
    for i, d in enumerate(ta.data): d.color = tints[i]
    if solid:
        m = ob.modifiers.new('sol', 'SOLIDIFY'); m.thickness = solid; m.offset = -1; m.use_even_offset = True
        apply_mods(ob)
    return finish(ob, mat)

# ----------------------------------------------------------------------------- materials
def N(nt, typ, **kw):
    n = nt.nodes.new(typ)
    for k, v in kw.items(): setattr(n, k, v)
    return n
def link(nt, src, dst):
    if isinstance(src, bpy.types.NodeSocket): nt.links.new(src, dst)
    else: dst.default_value = src
def mth(nt, op, a, b=0.0, c=None, clamp=False):
    n = N(nt, 'ShaderNodeMath', operation=op, use_clamp=clamp); link(nt, a, n.inputs[0]); link(nt, b, n.inputs[1])
    if c is not None: link(nt, c, n.inputs[2])
    return n.outputs[0]
def mix(nt, fac, a, b, blend='MIX'):
    n = N(nt, 'ShaderNodeMix', data_type='RGBA', blend_type=blend, clamp_result=True)
    link(nt, fac, n.inputs[0]); link(nt, a, n.inputs[6]); link(nt, b, n.inputs[7])
    return n.outputs[2]
def mixf(nt, fac, a, b):
    n = N(nt, 'ShaderNodeMix', data_type='FLOAT'); link(nt, fac, n.inputs[0]); link(nt, a, n.inputs[2]); link(nt, b, n.inputs[3])
    return n.outputs[0]
def ramp(nt, x, a, b):
    n = N(nt, 'ShaderNodeMapRange', interpolation_type='SMOOTHSTEP', clamp=True)
    link(nt, x, n.inputs['Value']); n.inputs['From Min'].default_value = a; n.inputs['From Max'].default_value = b
    return n.outputs['Result']
def img(nt, path, nc, uv):
    n = N(nt, 'ShaderNodeTexImage'); n.image = bpy.data.images.load(path, check_existing=True)
    n.image.colorspace_settings.name = 'Non-Color' if nc else 'sRGB'
    link(nt, uv, n.inputs['Vector']); return n
def attr(nt, name):
    return N(nt, 'ShaderNodeAttribute', attribute_name=name, attribute_type='GEOMETRY')
def tset(nt, kind, uv):
    p = os.path.join(TEX, kind, kind)
    a = img(nt, p + '_albedo.png', False, uv); nm = img(nt, p + '_normal.png', True, uv); o = img(nt, p + '_orm.png', True, uv)
    sep = N(nt, 'ShaderNodeSeparateColor'); link(nt, o.outputs['Color'], sep.inputs[0])
    return a.outputs['Color'], nm.outputs['Color'], sep
def nmap(nt, col, strength):
    n = N(nt, 'ShaderNodeNormalMap', uv_map='UVMap'); n.inputs['Strength'].default_value = strength
    link(nt, col, n.inputs['Color']); return n.outputs['Normal']
def lum_norm(nt, col, mean):
    bw = N(nt, 'ShaderNodeRGBToBW'); link(nt, col, bw.inputs[0])
    return mth(nt, 'MULTIPLY', bw.outputs[0], 1.0 / mean)
def detail(nt, base, ln, amount):
    """base x (1 + amount (ln - 1)): the tileable's value detail on our own graded colour."""
    k = mth(nt, 'MULTIPLY_ADD', mth(nt, 'SUBTRACT', ln, 1.0), amount); k = mth(nt, 'ADD', k, 1.0)
    return mix(nt, 1.0, base, N_rgb(nt, k), 'MULTIPLY')
def N_rgb(nt, f):
    c = N(nt, 'ShaderNodeCombineColor'); link(nt, f, c.inputs[0]); link(nt, f, c.inputs[1]); link(nt, f, c.inputs[2])
    return c.outputs[0]

TEX_MEAN = {}
def mean_lum(kind):
    if kind not in TEX_MEAN:
        im = bpy.data.images.load(os.path.join(TEX, kind, kind + '_albedo.png'), check_existing=True)
        im.colorspace_settings.name = 'sRGB'
        sm = im.copy(); sm.scale(64, 64)
        px = list(sm.pixels); n = len(px) // 4
        def l(x): return ((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92
        TEX_MEAN[kind] = sum(0.2126 * l(px[4 * i]) + 0.7152 * l(px[4 * i + 1]) + 0.0722 * l(px[4 * i + 2]) for i in range(n)) / n
        bpy.data.images.remove(sm)
    return TEX_MEAN[kind]

def base_nodes(name):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = N(nt, 'ShaderNodeOutputMaterial', name='OUT'); bs = N(nt, 'ShaderNodeBsdfPrincipled', name='BSDF')
    nt.links.new(bs.outputs[0], out.inputs['Surface'])
    uv = N(nt, 'ShaderNodeUVMap', uv_map='UVMap').outputs['UV']
    tc = N(nt, 'ShaderNodeTexCoord', object=ROOT)
    xyz = N(nt, 'ShaderNodeSeparateXYZ'); link(nt, tc.outputs['Object'], xyz.inputs[0])
    geo = N(nt, 'ShaderNodeNewGeometry')
    return m, nt, bs, uv, tc.outputs['Object'], xyz, geo

def noise_tex(nt, vec, scale, det=6.0, rough=0.6, w=0.0):
    n = N(nt, 'ShaderNodeTexNoise', noise_dimensions='4D'); link(nt, vec, n.inputs['Vector'])
    n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = det
    n.inputs['Roughness'].default_value = rough; n.inputs['W'].default_value = w
    return n.outputs['Fac']

def edge_mask(nt, geo, radius, gain):
    bv = N(nt, 'ShaderNodeBevel', samples=8); bv.inputs['Radius'].default_value = radius
    dp = N(nt, 'ShaderNodeVectorMath', operation='DOT_PRODUCT'); link(nt, bv.outputs[0], dp.inputs[0]); link(nt, geo.outputs['Normal'], dp.inputs[1])
    return mth(nt, 'MULTIPLY', mth(nt, 'SUBTRACT', 1.0, dp.outputs['Value']), gain, clamp=True)

def grime_band(nt, xyz, obj, top=0.32):
    g = mth(nt, 'SUBTRACT', 1.0, ramp(nt, xyz.outputs['Z'], 0.03, top))
    n = noise_tex(nt, obj, 5.0, 4.0, 0.5, 3.0)
    return mth(nt, 'MULTIPLY', g, mth(nt, 'MULTIPLY_ADD', n, 0.8, 0.6), clamp=True)

def mat_paint():
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Paint')
    alb, nrm, orm = tset(nt, 'painted_wood', uv)
    oak, oakn, oako = tset(nt, 'oak_timber', uv)
    tint = attr(nt, 'tint').outputs['Color']
    paint = detail(nt, mix(nt, 1.0, mix(nt, attr(nt, 'stripe').outputs['Fac'], PAINT, TRIM), tint, 'MULTIPLY'), lum_norm(nt, alb, mean_lum('painted_wood')), 0.2)
    sm = N(nt, 'ShaderNodeMapping'); link(nt, uv, sm.inputs['Vector']); sm.inputs['Scale'].default_value = (3.0, 70.0, 1.0)
    streak = noise_tex(nt, sm.outputs[0], 1.0, 4.0, 0.6, 2.0)            # brush strokes along the grain
    paint = mix(nt, 1.0, paint, N_rgb(nt, mth(nt, 'MULTIPLY_ADD', streak, 0.28, 0.86)), 'MULTIPLY')
    fade = noise_tex(nt, obj, 1.6, 3.0, 0.5, 7.0)                         # broad sun-fade blotches
    paint = mix(nt, mth(nt, 'MULTIPLY', ramp(nt, fade, 0.45, 0.75), 0.22), paint, lin('#a9ad8a') if PAINT_KIND == 'green' else lin('#d2b47a'))
    wood = mix(nt, 0.7, mix(nt, 1.0, oak, (1.6, 1.5, 1.4, 1), 'MULTIPLY'), lin('#7f6e5a'))       # weathered grey oak under the flakes
    z = xyz.outputs['Z']
    hand = mth(nt, 'MULTIPLY', ramp(nt, z, 0.70, 0.95), mth(nt, 'SUBTRACT', 1.0, ramp(nt, z, 1.35, 1.70)))
    edge = edge_mask(nt, geo, 0.012, 10.0)
    n1 = noise_tex(nt, obj, 6.5, 8.0, 0.62, 1.0)
    s = mth(nt, 'ADD', n1, mth(nt, 'MULTIPLY', hand, 0.19))
    s = mth(nt, 'ADD', s, mth(nt, 'MULTIPLY', edge, mth(nt, 'MULTIPLY_ADD', hand, 0.45, 0.22)))
    chip = ramp(nt, s, 0.742, 0.755)
    col = mix(nt, chip, paint, wood)
    g = grime_band(nt, xyz, obj, 0.42)
    col = mix(nt, mth(nt, 'MULTIPLY', g, 0.9), col, lin('#2f2a1f'))
    link(nt, col, bs.inputs['Base Color'])
    rough = mixf(nt, chip, mth(nt, 'MULTIPLY_ADD', orm.outputs[1], 0.3, 0.58), 0.82)
    link(nt, mth(nt, 'MULTIPLY_ADD', g, 0.15, rough, clamp=True), bs.inputs['Roughness'])
    bump = N(nt, 'ShaderNodeBump'); bump.inputs['Distance'].default_value = 0.0012; bump.inputs['Strength'].default_value = 0.9
    link(nt, mth(nt, 'SUBTRACT', 1.0, chip), bump.inputs['Height'])
    link(nt, nmap(nt, nrm, 0.45), bump.inputs['Normal'])
    link(nt, bump.outputs['Normal'], bs.inputs['Normal'])
    return m

def mat_oak():
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Oak')
    oak, oakn, oako = tset(nt, 'oak_timber', uv)
    tint = attr(nt, 'tint').outputs['Color']
    col = mix(nt, 1.0, mix(nt, 1.0, oak, (2.0, 1.85, 1.65, 1), 'MULTIPLY'), tint, 'MULTIPLY')
    edge = edge_mask(nt, geo, 0.008, 8.0)
    col = mix(nt, mth(nt, 'MULTIPLY', edge, 0.35), col, lin('#a08a6c'))           # worn pale arrises
    nz = N(nt, 'ShaderNodeSeparateXYZ'); link(nt, geo.outputs['Normal'], nz.inputs[0])
    up = ramp(nt, nz.outputs['Z'], 0.6, 0.9)
    col = mix(nt, mth(nt, 'MULTIPLY', up, 0.35), col, lin('#8e7c66'))           # up-facing oak sun-greyed
    pol = mth(nt, 'MULTIPLY', up, ramp(nt, noise_tex(nt, obj, 2.2, 3.0, 0.5, 4.0), 0.35, 0.65))
    col = mix(nt, mth(nt, 'MULTIPLY', pol, 0.6), col, lin('#b8a07a'))           # goods slid pale and smooth
    fresh = detail(nt, lin('#c2a274'), lum_norm(nt, oak, mean_lum('oak_timber')), 0.25)
    col = mix(nt, attr(nt, 'stripe').outputs['Fac'], col, fresh)                   # stripe 1 = a fresh replacement board
    g = grime_band(nt, xyz, obj)
    col = mix(nt, mth(nt, 'MULTIPLY', g, 0.5), col, lin('#2a2620'))
    link(nt, col, bs.inputs['Base Color'])
    rough = mth(nt, 'MULTIPLY_ADD', oako.outputs[1], 0.35, 0.5)
    link(nt, mth(nt, 'SUBTRACT', rough, mth(nt, 'MULTIPLY', pol, 0.22)), bs.inputs['Roughness'])
    link(nt, nmap(nt, oakn, 0.9), bs.inputs['Normal'])
    return m

def mat_canvas():
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Canvas')
    alb, nrm, orm = tset(nt, 'canvas', uv)
    st = attr(nt, 'stripe').outputs['Fac']
    odd = mth(nt, 'MODULO', mth(nt, 'FLOOR', mth(nt, 'ADD', st, 0.5)), 2.0)
    base = mix(nt, odd, OCHRE, MADDER)
    base = mix(nt, 1.0, base, attr(nt, 'tint').outputs['Color'], 'MULTIPLY')
    col = detail(nt, base, lum_norm(nt, alb, mean_lum('canvas')), 0.35)
    nz = N(nt, 'ShaderNodeSeparateXYZ'); link(nt, geo.outputs['Normal'], nz.inputs[0])
    sun = mth(nt, 'MULTIPLY', ramp(nt, nz.outputs['Z'], 0.2, 0.9), mth(nt, 'MULTIPLY_ADD', noise_tex(nt, obj, 1.2, 3.0, 0.5, 9.0), 0.25, 0.22))
    col = mix(nt, sun, col, CREAM)                                                   # sun-faded tops
    vh = attr(nt, 'vh').outputs['Fac']
    tide = mth(nt, 'MULTIPLY', ramp(nt, vh, 0.45, 0.95), ramp(nt, noise_tex(nt, obj, 4.0, 4.0, 0.55, 2.0), 0.4, 0.62))
    col = mix(nt, mth(nt, 'MULTIPLY', tide, 0.6), col, lin('#4a3524'))            # damp tide stains at the scallops
    bind = ramp(nt, vh, 0.86, 0.92)
    col = mix(nt, mth(nt, 'MULTIPLY', bind, 0.55), col, lin('#3e2a1e'))            # bound edge tape along the scallops
    link(nt, col, bs.inputs['Base Color'])
    link(nt, mth(nt, 'MULTIPLY_ADD', orm.outputs[1], 0.2, 0.72), bs.inputs['Roughness'])
    link(nt, nmap(nt, nrm, 0.4), bs.inputs['Normal'])
    bs.inputs['Subsurface Weight'].default_value = 0.0
    return m

def mat_cloth():
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Cloth')
    alb, nrm, orm = tset(nt, 'canvas', uv)
    col = detail(nt, attr(nt, 'tint').outputs['Color'], lum_norm(nt, alb, mean_lum('canvas')), 0.5)
    g = grime_band(nt, xyz, obj, 1.6)        # back cloth hem dirtier toward the bottom
    col = mix(nt, mth(nt, 'MULTIPLY', g, 0.0), col, lin('#3a3026'))
    link(nt, col, bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value = 0.88
    link(nt, nmap(nt, nrm, 0.6), bs.inputs['Normal'])
    return m

def mat_rope():
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Rope')
    w = N(nt, 'ShaderNodeTexWave', wave_type='BANDS', bands_direction='DIAGONAL'); link(nt, uv, w.inputs['Vector'])
    w.inputs['Scale'].default_value = 120.0; w.inputs['Distortion'].default_value = 2.0
    col = mix(nt, mth(nt, 'MULTIPLY', w.outputs['Fac'], 0.4), ROPE, lin('#5d4c34'))
    link(nt, col, bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = 0.92
    return m

def mat_iron():
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Iron')
    alb, nrm, orm = tset(nt, 'iron', uv)
    rust = ramp(nt, noise_tex(nt, obj, 14.0, 6.0, 0.6, 5.0), 0.52, 0.7)
    col = mix(nt, rust, mix(nt, 1.0, alb, (0.75, 0.75, 0.78, 1), 'MULTIPLY'), lin('#6b3a22'))
    link(nt, col, bs.inputs['Base Color'])
    link(nt, mixf(nt, rust, mth(nt, 'MULTIPLY_ADD', orm.outputs[2], 0.3, 0.55), 0.0), bs.inputs['Metallic'])
    link(nt, mixf(nt, rust, mth(nt, 'MULTIPLY_ADD', orm.outputs[1], 0.4, 0.3), 0.9), bs.inputs['Roughness'])
    link(nt, nmap(nt, nrm, 0.8), bs.inputs['Normal'])
    return m

def mat_stone():
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Stone')
    alb, nrm, orm = tset(nt, 'sandstone', uv)
    g = grime_band(nt, xyz, obj, 0.12)
    link(nt, mix(nt, mth(nt, 'MULTIPLY', g, 0.35), alb, lin('#3a3228')), bs.inputs['Base Color'])
    link(nt, mth(nt, 'MULTIPLY_ADD', orm.outputs[1], 0.3, 0.6), bs.inputs['Roughness'])
    link(nt, nmap(nt, nrm, 1.0), bs.inputs['Normal'])
    return m

def mat_straw():
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Straw')
    sm = N(nt, 'ShaderNodeMapping'); link(nt, obj, sm.inputs['Vector']); sm.inputs['Scale'].default_value = (90.0, 90.0, 4.0)
    n = noise_tex(nt, sm.outputs[0], 1.0, 3.0, 0.5, 1.0)
    col = mix(nt, 1.0, attr(nt, 'tint').outputs['Color'], N_rgb(nt, mth(nt, 'MULTIPLY_ADD', n, 0.7, 0.62)), 'MULTIPLY')
    link(nt, col, bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = 0.7
    return m

def mat_rug():
    """Counter cloth: cream field with madder dots, ochre and madder border bands (pattern in cloth metres from UVMap)."""
    m, nt, bs, uv, obj, xyz, geo = base_nodes('Rug')
    alb, nrm, orm = tset(nt, 'canvas', uv)
    sep = N(nt, 'ShaderNodeSeparateXYZ'); link(nt, uv, sep.inputs[0])
    u = mth(nt, 'MULTIPLY', sep.outputs['X'], TILE); v = mth(nt, 'MULTIPLY', sep.outputs['Y'], TILE)
    du = mth(nt, 'SUBTRACT', mth(nt, 'FRACT', mth(nt, 'MULTIPLY', u, 9.0)), 0.5)
    dv = mth(nt, 'SUBTRACT', mth(nt, 'FRACT', mth(nt, 'MULTIPLY', mth(nt, 'ADD', v, mth(nt, 'MULTIPLY', mth(nt, 'FLOOR', mth(nt, 'MULTIPLY', u, 9.0)), 0.0556)), 9.0)), 0.5)
    r = mth(nt, 'SQRT', mth(nt, 'ADD', mth(nt, 'MULTIPLY', du, du), mth(nt, 'MULTIPLY', dv, dv)))
    dot = mth(nt, 'LESS_THAN', r, 0.19)
    edge = mth(nt, 'MINIMUM', mth(nt, 'MINIMUM', u, mth(nt, 'SUBTRACT', 1.44, u)), mth(nt, 'SUBTRACT', 0.795, v))   # distance to the hem
    band = mth(nt, 'LESS_THAN', edge, 0.07)
    line = mth(nt, 'MULTIPLY', mth(nt, 'GREATER_THAN', edge, 0.085), mth(nt, 'LESS_THAN', edge, 0.1))
    col = mix(nt, dot, CREAM, MADDER)
    col = mix(nt, line, col, MADDER); col = mix(nt, band, col, OCHRE)
    col = detail(nt, col, lum_norm(nt, alb, mean_lum('canvas')), 0.4)
    link(nt, col, bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = 0.88
    link(nt, nmap(nt, nrm, 0.5), bs.inputs['Normal'])
    return m

MATS = dict(Rug=mat_rug(), Straw=mat_straw(), Paint=mat_paint(), Oak=mat_oak(), Canvas=mat_canvas(), Cloth=mat_cloth(), Rope=mat_rope(),
            Iron=mat_iron(), Stone=mat_stone())

# ----------------------------------------------------------------------------- frame
POSTS = []
for sx in (-1, 1):
    for sy in (-1, 1):
        nm = f"post_{'L' if sx < 0 else 'R'}{'F' if sy < 0 else 'B'}"; r = rng(nm)
        x, y = sx * PX, sy * PY
        top = lintel_top(y) - LIN_H + 0.03
        h = top - PAD_H
        board(f'pad_{nm}', (0.24 + 0.03 * r.random(), 0.24 + 0.03 * r.random(), PAD_H + 0.04), 'Stone',
              (x + 0.01 * (r.random() - .5), y + 0.01 * (r.random() - .5), (PAD_H - 0.04) / 2), (0, 0, r.uniform(-8, 8)),
              grain=0, cut=2, chamfer=0.014, wob=0.006)
        # lean 1-2 deg about the base: rotate the post about its foot, not its middle
        lx, ly = r.uniform(1.0, 2.0) * r.choice((-1, 1)), r.uniform(0.6, 1.6) * r.choice((-1, 1))
        ob = board(nm, (POST, POST, h), 'Paint', (x, y, PAD_H + h / 2), (0, 0, 0), grain=2, cut=24, chamfer=0.018,
                   wob=0.004, bend=0.012, bend_up=0, adze=0.004)
        ob.location = Matrix.Rotation(math.radians(lx), 4, 'X') @ Matrix.Rotation(math.radians(ly), 4, 'Y') @ Vector((0, 0, h / 2))
        ob.location += Vector((x, y, PAD_H)); ob.rotation_euler = (math.radians(lx), math.radians(ly), 0)
        POSTS.append((x, y, top))

# lintels front and back (rafters sit on them), side top beams following the slope
for sy, tag in ((-1, 'F'), (1, 'B')):
    y = sy * PY; zt = lintel_top(y)
    board(f'lintel_{tag}', (W + 0.22, POST, LIN_H), 'Paint', (0, y, zt - LIN_H / 2), (0, 0, 0), grain=0, cut=30,
          chamfer=0.016, wob=0.004, bend=-0.02, adze=0.004)
    for sx in (-1, 1):                                       # pegs through the post tenons, standing proud
        pole(f'peg_{tag}{sx}', [(sx * PX, y - sy * 0.055, zt - LIN_H / 2), (sx * PX, y - sy * 0.085, zt - LIN_H / 2)], 0.016, 'Oak', 8)
slope = math.atan2(canvas_z(-PY) - canvas_z(PY), 2 * PY)
for sx in (-1, 1):
    zc = (lintel_top(-PY) + lintel_top(PY)) / 2 - 0.07
    board(f'sidebeam_{sx}', (POST * 0.9, 2 * PY - POST, 0.13), 'Paint', (sx * PX, 0, zc), (-math.degrees(slope), 0, 0),
          grain=1, cut=6, chamfer=0.014, wob=0.005)
# knee braces at the front (outline breaks under the lintel), plain ones at the back
for sy, tag in ((-1, 'F'), (1, 'B')):
    y = sy * PY; zb = lintel_top(y) - LIN_H
    for sx in (-1, 1):
        L = 0.46; cx = sx * (PX - POST / 2 - 0.15); cz = zb - 0.15
        board(f'brace_{tag}{sx}', (L, 0.085, 0.085), 'Paint', (cx, y, cz), (0, sx * 45, 0), grain=0, cut=2,
              chamfer=0.012, wob=0.003)
# rails: sides at counter height and low, back the same; front has the counter and boards instead
for sx in (-1, 1):
    for z, nm in ((0.90, 'hi'), (0.24, 'lo')):
        board(f'siderail_{nm}{sx}', (0.085, 2 * PY - POST + 0.04, 0.10), 'Paint', (sx * PX, 0, z), (0, 0, 0), grain=1,
              cut=4, chamfer=0.012, wob=0.004, bend=-0.008)
    L = math.hypot(2 * PY - POST, 0.62); a = math.degrees(math.atan2(0.62, 2 * PY - POST))
    board(f'sidebrace{sx}', (0.075, L, 0.075), 'Paint', (sx * (PX + 0.005), 0, 0.57), (sx * a * 0 + a, 0, 0), grain=1,
          cut=3, chamfer=0.01, wob=0.003)
for z in (0.90, 0.24):
    board(f'backrail_{z}', (2 * PX - POST + 0.04, 0.085, 0.10), 'Paint', (0, PY, z), grain=0, cut=4, chamfer=0.012)

# ----------------------------------------------------------------------------- counter, apron, front boards
board('counter_rail', (2 * PX - POST + 0.02, 0.09, 0.10), 'Paint', (0, -PY + 0.01, COUNTER_Z - 0.11), grain=0, cut=4)
y0, y1 = -0.885, -0.27; nb = 4; gap = 0.008
bw = (y1 - y0 - gap * (nb - 1)) / nb
for i in range(nb):
    r = rng(f'plank{i}')
    yc = y0 + bw / 2 + i * (bw + gap)
    ln = W + 0.12 + r.uniform(-0.03, 0.03)
    board(f'plank{i}', (ln, bw, 0.07), 'Oak', (r.uniform(-0.015, 0.015), yc, COUNTER_Z - 0.035 + r.uniform(-0.002, 0.002)),
          (r.uniform(-0.4, 0.4), 0, r.uniform(-0.3, 0.3)), grain=0, cut=10, chamfer=0.007, wob=0.002, bend=-0.004)
# apron: one wide board with a shaped bottom (shallow central arch, notched ends)
ap = []
x0, x1, zt, zb = -W / 2 - 0.02, W / 2 + 0.02, COUNTER_Z - 0.055, COUNTER_Z - 0.36
ap += [(x0, zt), (x0, zb + 0.06), (x0 + 0.05, zb + 0.06), (x0 + 0.05, zb)]
for k in range(25):
    t = k / 24; xx = -0.85 + 1.7 * t
    ap.append((xx, zb + 0.07 * math.sin(math.pi * t) ** 0.8)) if 0 < k < 24 else ap.append((xx, zb))
ap += [(x1 - 0.05, zb), (x1 - 0.05, zb + 0.06), (x1, zb + 0.06), (x1, zt)]
ap = [ap[0]] + [p for p in ap[1:-1]] + [ap[-1]]
ap_sorted = ap[:4] + sorted(ap[4:-4], key=lambda p: p[0]) + ap[-4:]
apron = prism('apron', ap_sorted[::-1], 0.045, 'Paint', -0.905, chamfer=0.008, trim=1.0)
bow(apron, 0, 1, 0.006)
# lower front: vertical boards between the posts, one a fresh unpainted replacement
xa, xb = -PX + POST / 2, PX - POST / 2; nfb = 15; fw = (xb - xa) / nfb
for i in range(nfb):
    r = rng(f'fb{i}'); zb_ = 0.07 + r.uniform(0, 0.025); zt_ = COUNTER_Z - 0.30
    mat, fresh = ('Oak', 1.0) if i == 10 else ('Paint', 0.0)
    board(f'frontboard{i}', (fw - 0.007, 0.03, zt_ - zb_), mat, (xa + fw * (i + .5), -PY + 0.03, (zb_ + zt_) / 2),
          (0, r.uniform(-0.8, 0.8), 0), grain=2, cut=3, chamfer=0.005, wob=0.002, trim=fresh)

# nail rows: front boards nailed to a hidden low rail; iron corner straps on the apron
for i in range(nfb):
    r = rng(f'nail{i}')
    for dx in (-0.25, 0.25):
        x = xa + fw * (i + 0.5 + dx); z = 0.21 + r.uniform(-0.008, 0.008)
        pole(f'nail{i}{dx}', [(x, -PY + 0.012, z), (x, -PY + 0.006, z)], 0.0085, 'Iron', 6)
for sx in (-1, 1):
    x = sx * (W / 2 - 0.06)
    board(f'strap{sx}', (0.055, 0.008, 0.27), 'Iron', (x, -0.909, COUNTER_Z - 0.20), grain=2, chamfer=0.002, wob=0.0015)
    for dz in (-0.09, 0.0, 0.09):
        pole(f'strapnail{sx}{dz}', [(x, -0.912, COUNTER_Z - 0.20 + dz), (x, -0.919, COUNTER_Z - 0.20 + dz)], 0.008, 'Iron', 6)

# ----------------------------------------------------------------------------- tiered display shelf behind the counter
T1, T2 = 1.15, 1.33
for i, x in enumerate((-1.14, 0.0, 1.14)):
    prof = [(-0.255, COUNTER_Z - 0.06), (-0.255, T1 - 0.035), (-0.02, T1 - 0.035), (-0.02, T2 - 0.035), (0.22, T2 - 0.035),
            (0.22, 0.07), (0.0, 0.07), (0.0, COUNTER_Z - 0.06)]
    bm = bmesh.new(); vs = [bm.verts.new((x - 0.0175, a, z)) for a, z in prof]; f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0.035, 0, 0), verts=[v for v in ext['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    uv_metres(bm, 2, (i * 0.3, 0.1)); ob = obj_from_bm(f'cheek{i}', bm); bevel(ob, 0.007, 2, 50); apply_mods(ob)
    finish(ob, 'Paint', tint=(0.95, 0.95, 0.95, 1))
for nm, z, ya, yb in (('tier1', T1, -0.275, -0.01), ('tier2', T2, -0.035, 0.235)):
    board(nm, (2.34, yb - ya, 0.035), 'Oak', (0, (ya + yb) / 2, z - 0.0175), grain=0, cut=6, chamfer=0.006, bend=-0.005)
    board(nm + '_lip', (2.34, 0.018, 0.035), 'Paint', (0, ya - 0.004, z + 0.012), grain=0, cut=4, chamfer=0.005, trim=1.0)
board('riser1', (2.30, 0.022, T1 - COUNTER_Z - 0.03), 'Paint', (0, -0.25, (COUNTER_Z + T1 - 0.035) / 2), grain=0, cut=4)
board('riser2', (2.30, 0.022, T2 - T1 - 0.03), 'Paint', (0, -0.015, (T1 + T2 - 0.035) / 2), grain=0, cut=4)

# ----------------------------------------------------------------------------- rafters, front bar, awning canvas
def sag_x(x):
    ax = abs(x)
    if ax > RAFT_X[-1]:
        return 0.07 * ((ax - RAFT_X[-1]) / (AW_W / 2 - RAFT_X[-1])) ** 1.4      # rolls over the side rafters
    f = (ax % 1.30) / 1.30
    return 0.085 * math.sin(math.pi * f) ** 0.9
def canvas_pt(x, t):
    y = AW_BACK[0] + (AW_FRONT[0] - AW_BACK[0]) * t
    z = canvas_z(y)
    hold = 0.55 + 0.45 * math.sin(math.pi * min(t / 0.84, 1.0)) if t < 0.84 else 0.55 + 0.25 * math.sin(math.pi * (t - 0.84) / 0.16)
    s = sag_x(x)
    fold = 0.016 * math.sin(2 * math.pi * x / 0.21 + 2.6 * t + 0.7 * noise.noise(Vector((x * 2, t * 3, 1.0)))) * min(1, s / 0.03)
    z = z - s * hold + fold + 0.01 * noise.noise(Vector((x * 1.4, t * 2.2, 4.0)))
    return Vector((x, y, z))
for i, x in enumerate(RAFT_X):
    r = rng(f'raft{i}')
    pts = [Vector((x, AW_BACK[0] + 0.05, 0)), Vector((x, AW_FRONT[0] + 0.04, 0))]
    pts = [pts[0].lerp(pts[1], k / 8) for k in range(9)]
    pts = [Vector((p.x + 0.01 * math.sin(k * 0.9 + i), p.y, canvas_z(p.y) - RAFT_R - 0.004)) for k, p in enumerate(pts)]
    pole(f'rafter{i}', pts, RAFT_R, 'Oak', 12)
fb = [Vector((-AW_W / 2 + 0.05 + (AW_W - 0.1) * k / 10, AW_FRONT[0] + 0.05, 0)) for k in range(11)]
fb = [Vector((p.x, p.y, AW_FRONT[1] - 0.042 - 0.018 * math.sin(math.pi * k / 10))) for k, p in enumerate(fb)]
pole('frontbar', fb, 0.038, 'Oak', 12)
NU, NV = N_STRIPE * 5, 16
L_SL = (Vector(AW_BACK) - Vector(AW_FRONT)).length
def cv_fn(u, v):
    x = -AW_W / 2 + AW_W * u
    p = canvas_pt(x, v)
    return p, (x / TILE, v * L_SL / TILE), 0.0
grid_part('canvas', NU, NV, cv_fn, 'Canvas', stripe_fn=lambda u, v: math.floor(u * N_STRIPE),
          tint_fn=lambda u, v: (1, 1, 1, 1))
# scalloped valances: front and both sides, one scallop per stripe
VH, SD = 0.20, 0.085
def valance(name, p0, p1, n_sc, side_out, phase):
    rr = rng(name); lens = [rr.uniform(-0.012, 0.012) for _ in range(n_sc)]; swing = [rr.uniform(-0.025, 0.025) for _ in range(n_sc)]
    def fn(u, v):
        base = p0.lerp(p1, u)
        k = min(int(u * n_sc), n_sc - 1); f = u * n_sc - k
        circ = math.sqrt(max(0.0, 1 - (2 * f - 1) ** 2))
        drop = v * (VH + lens[k] + SD * circ)
        sway = 0.012 * math.sin(math.pi * v) + 0.006 * v * math.sin(u * 17 + phase) + swing[k] * v * v      # each scallop swings a little on its own
        p = base + side_out * sway + Vector((0, 0, -drop + 0.004 * math.sin(u * 23 + phase)))
        return p, ((p0 - base).length / TILE + phase, drop / TILE), v
    return grid_part(name, n_sc * 7, 4, fn, 'Canvas', stripe_fn=lambda u, v: math.floor(u * n_sc) + phase,
                     tint_fn=lambda u, v: (0.97, 0.97, 0.97, 1))
fr0 = canvas_pt(-AW_W / 2, 1.0) + Vector((0, -0.01, 0.0)); fr1 = canvas_pt(AW_W / 2, 1.0) + Vector((0, -0.01, 0.0))
valance('valance_F', fr0, fr1, N_STRIPE, Vector((0, -1, 0)), 0)
n_side = round(L_SL / SW)
for sx in (-1, 1):
    a = canvas_pt(sx * AW_W / 2, 0.0) + Vector((sx * 0.005, 0, 0)); b = canvas_pt(sx * AW_W / 2, 1.0) + Vector((sx * 0.005, 0, 0))
    valance(f'valance_{sx}', a, b, n_side, Vector((sx, 0, 0)), 1 if sx > 0 else 0)
# plain back cloth hanging from the back lintel
def back_fn(u, v):
    x = -1.18 + 2.36 * u; ztop = lintel_top(PY) - 0.17; zbot = 1.42 + 0.03 * math.sin(u * 9.0)
    z = ztop + (zbot - ztop) * v
    y = PY - 0.075 + 0.02 * math.sin(2 * math.pi * u * 7.5) * (0.3 + v)
    return Vector((x, y, z)), (x / TILE, z / TILE), 0.0
grid_part('backcloth', 40, 6, back_fn, 'Cloth', tint_fn=lambda u, v: lin('#6e4a3a'))

# ----------------------------------------------------------------------------- arched produce sign on the front bar (outline break)
SGW, SGH = 0.74, 0.34; sz0 = AW_FRONT[1] - 0.01; sy = AW_FRONT[0] + 0.03
arch = [(SGW / 2, sz0)] + [(SGW / 2 * math.cos(math.pi * k / 16), sz0 + (SGH - 0.28 * SGW / 2) + 0.28 * SGW / 2 * math.sin(math.pi * k / 16) * 1.0)
                           for k in range(17)] + [(-SGW / 2, sz0)]
sign = prism('sign', arch, 0.035, 'Paint', sy - 0.035, chamfer=0.008, trim=1.0)
inner = [(0.86 * a, sz0 + 0.03 + 0.86 * (z - sz0)) for a, z in arch]
prism('sign_face', inner, 0.012, 'Cloth', sy - 0.047, chamfer=0.003, tint=lin('#d6c69c'))
for sx in (-1, 1):
    board(f'sign_leg{sx}', (0.04, 0.04, 0.16), 'Paint', (sx * 0.26, sy - 0.0, sz0 - 0.03), grain=2, chamfer=0.006)
# carved apple and leaf in relief (the stall sells harvest produce)
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=0.085)
for v in bm.verts:
    v.co.y *= 0.45; v.co.x *= 1.08; v.co.z *= 0.95
    if v.co.z > 0.06 and abs(v.co.x) < 0.03: v.co.z -= 0.02 * (1 - abs(v.co.x) / 0.03)
uv_metres(bm, 2, (0.3, 0.7)); ap_ob = obj_from_bm('sign_apple', bm)
finish(ap_ob, 'Cloth', (0.0, sy - 0.05, sz0 + 0.165), tint=lin('#a83a2a'))
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.05)
for v in bm.verts: v.co.y *= 0.25; v.co.z *= 0.45
uv_metres(bm, 0, (0.1, 0.2)); lf = obj_from_bm('sign_leaf', bm)
finish(lf, 'Cloth', (0.055, sy - 0.05, sz0 + 0.26), (0, -30, 0), tint=lin('#5f7a32'))
pole('sign_stem', [(0.0, sy - 0.05, sz0 + 0.235), (0.012, sy - 0.05, sz0 + 0.275)], 0.008, 'Oak', 6)

# ----------------------------------------------------------------------------- patterned cloth framing the goods (flat on the counter, draped over the apron)
RUG_W, RUG_TOP, RUG_DROP = 1.44, 0.555, 0.24
def rug_fn(u, v):
    x = -RUG_W / 2 + RUG_W * u; sv = v * (RUG_TOP + RUG_DROP)
    if sv <= RUG_TOP:
        p = Vector((x, -0.33 - sv, COUNTER_Z + 0.004))
    else:
        d = sv - RUG_TOP; a = min(d / 0.05, 1.0) * math.pi / 2          # rolls over the counter's front edge
        p = Vector((x, -0.885 - 0.032 * math.sin(a) - 0.004, COUNTER_Z + 0.004 - 0.03 * (1 - math.cos(a)) - max(0.0, d - 0.05)))
        f = (d / RUG_DROP) ** 1.3
        p.y -= f * (0.014 * math.sin(2 * math.pi * x / 0.26 + 0.6) + 0.008)
        p.z += f * 0.012 * math.sin(x * 9.0)
    p = Matrix.Rotation(math.radians(1.5), 3, 'Z') @ p
    return p, (u * RUG_W / TILE, sv / TILE), 0.0
grid_part('counter_cloth', 36, 22, rug_fn, 'Rug', solid=0.003)

# ----------------------------------------------------------------------------- small sign on a stake at the fountain-side front corner (bread)
stx, sty = -W / 2 - 0.13, -PY - 0.12
board('stake', (0.06, 0.06, 1.12), 'Paint', (stx, sty, 0.56), (0, 2.0, 0), grain=2, cut=3, chamfer=0.01, wob=0.003)
board('stake_board', (0.36, 0.035, 0.24), 'Paint', (stx, sty - 0.05, 1.02), (-8, 0, 3), grain=0, chamfer=0.008, trim=1.0)
board('stake_face', (0.30, 0.01, 0.18), 'Cloth', (stx, sty - 0.07, 1.022), (-8, 0, 3), grain=0, chamfer=0.003, tint=lin('#d6c69c'))
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=1.0)
for v in bm.verts: v.co = Vector((v.co.x * 0.095, v.co.y * 0.02, v.co.z * 0.045 + 0.012 * (1 - (v.co.x) ** 2)))
uv_metres(bm, 0, (0.4, 0.2)); loaf = obj_from_bm('stake_loaf', bm)
finish(loaf, 'Cloth', (stx, sty - 0.078, 1.02), (-8, 0, 3), tint=lin('#a8703a'))
for k in (-1, 0, 1):                                  # scored crust
    board(f'stake_score{k}', (0.008, 0.006, 0.05), 'Cloth', (stx + k * 0.045, sty - 0.099, 1.03), (-8, 0, 3 + 25), chamfer=0, wob=0,
          tint=lin('#e0c48a'))

# ----------------------------------------------------------------------------- pennant string across the opening
pa, pb = Vector((-W / 2 - 0.01, -PY - 0.08, lintel_top(-PY) - 0.24)), Vector((W / 2 + 0.01, -PY - 0.08, lintel_top(-PY) - 0.24))
def rope_pt(t, sag=0.22):
    return pa.lerp(pb, t) + Vector((0, -0.02 * math.sin(math.pi * t), -sag * 4 * t * (1 - t)))
pole('bunting_rope', [rope_pt(k / 24) for k in range(25)], 0.007, 'Rope', 6)
for sx, pt in ((-1, pa), (1, pb)):          # rope wrapped round each post
    pole(f'rope_wrap{sx}', [pt + Vector((-sx * 0.02, 0.0, 0)), pt + Vector((-sx * 0.02, 0.10, 0.0)),
                            pt + Vector((-sx * 0.15, 0.10, 0.0)), pt + Vector((-sx * 0.15, 0.0, 0))], 0.009, 'Rope', 6, cap=True)
PCOL = [CREAM, MADDER, OCHRE, TEAL]
NP = 11
for k in range(NP):
    r = rng(f'pen{k}'); t0, t1 = (k + 0.18) / NP, (k + 0.86) / NP
    a, b = rope_pt(t0), rope_pt(t1); tw = math.radians(r.uniform(-14, 14)); ln = 0.21 + r.uniform(-0.01, 0.01)
    def pfn(u, v, a=a, b=b, tw=tw, ln=ln, r=r):
        top = a.lerp(b, 0.5 + (u - 0.5) * (1 - v))
        p = top + Vector((0, -math.sin(tw) * ln * v, -math.cos(tw) * ln * v))
        p += Vector((0, 0.02 * math.sin(math.pi * v) * math.sin(u * 3.0 + k), 0))
        return p, (u * 0.2 / TILE + k * 0.1, v * ln / TILE), 0.0
    c = PCOL[k % 4]; j = 0.94 + 0.1 * r.random()
    grid_part(f'pennant{k}', 4, 4, pfn, 'Cloth', solid=0.002, tint_fn=lambda u, v, c=c, j=j: (c[0] * j, c[1] * j, c[2] * j, 1))

# ----------------------------------------------------------------------------- wheat sheaves hung head-down on the front posts (harvest stall)
def sheaf(name, tie, out):
    """A bound sheaf hanging from `tie` (on the post's outer face), stalk ends up, ears flaring down. out: +1/-1 side."""
    r = rng(name); segs, prof = 12, [(0.068, 0.0), (0.08, 0.05), (0.055, 0.15), (0.062, 0.25), (0.085, 0.40), (0.105, 0.47)]
    bm = bmesh.new(); rings = []
    for rad, z in prof:
        rings.append([bm.verts.new((rad * (1 + 0.15 * noise.noise(Vector((i, z * 9, r.random())))) * math.cos(2 * math.pi * i / segs),
                                    rad * math.sin(2 * math.pi * i / segs), -z)) for i in range(segs)])
    for a_, b_ in zip(rings[:-1], rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs; bm.faces.new((a_[i], a_[j], b_[j], b_[i]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    for k in range(40):                                   # ears: spindles fanning out and down
        a = 2.4 * k + r.uniform(-0.2, 0.2); rr = 0.03 + 0.06 * ((k * 0.618) % 1.0); spread = 0.15 + 0.6 * rr / 0.09; ln = r.uniform(0.10, 0.13)
        base = Vector((rr * math.cos(a), rr * math.sin(a), -0.44 - r.uniform(0, 0.05)))
        d = Vector((math.sin(spread) * math.cos(a), math.sin(spread) * math.sin(a), -math.cos(spread)))
        sp = bmesh.ops.create_uvsphere(bm, u_segments=5, v_segments=4, radius=1.0)['verts']
        q = d.to_track_quat('Z', 'Y').to_matrix()
        for v in sp:
            v.co = base + d * ln * 0.5 + q @ Vector((v.co.x * 0.016, v.co.y * 0.012, v.co.z * ln * 0.5))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    uv_metres(bm, 2, (r.random(), r.random()))
    ob = obj_from_bm(name, bm)
    finish(ob, 'Straw', tie + Vector((out * 0.09, 0, -0.02)), (r.uniform(-6, 6), r.uniform(-8, 8), r.uniform(0, 360)),
           tint=lin('#dcc27a'))
    t = tie + Vector((out * 0.09, 0, -0.17))
    pole(name + '_band', [t + Vector((0.06 * math.cos(2 * math.pi * k / 10), 0.06 * math.sin(2 * math.pi * k / 10), 0)) for k in range(11)],
         0.011, 'Rope', 6)
    pole(name + '_cord', [tie, tie + Vector((out * 0.03, 0, -0.03)), t + Vector((0, 0, 0.03))], 0.006, 'Rope', 6)
    pole(name + '_nail', [tie - Vector((out * 0.01, 0, 0)), tie + Vector((out * 0.012, 0, 0))], 0.008, 'Iron', 6)
for sx in (-1, 1):
    sheaf(f'sheaf{sx}', Vector((sx * (PX + POST / 2), -PY + 0.01, 1.95)), sx)

# ----------------------------------------------------------------------------- lantern hook (iron) on the fountain-side front post
hx, hy, hz = -PX - 0.01, -PY - POST / 2, 1.98
board('hook_plate', (0.07, 0.016, 0.20), 'Iron', (hx, hy - 0.008, hz - 0.04), grain=2, chamfer=0.004, wob=0.001)
for dz in (0.03, -0.11):
    pole(f'hook_nail{dz}', [(hx, hy - 0.014, hz + dz), (hx, hy - 0.026, hz + dz)], 0.011, 'Iron', 8)
arm = [(hx, hy - 0.015, hz), (hx, hy - 0.20, hz + 0.01), (hx, hy - 0.33, hz + 0.0), (hx, hy - 0.36, hz - 0.03),
       (hx, hy - 0.345, hz - 0.06), (hx, hy - 0.32, hz - 0.05)]
pole('hook_arm', arm, 0.010, 'Iron', 8)
# scroll brace under the arm
sc = [(hx, hy - 0.015, hz - 0.13)]
for k in range(12):
    a = math.radians(-90 + 300 * k / 11); rr = 0.06 * (1 - 0.55 * k / 11)
    sc.append((hx, hy - 0.12 - rr * math.cos(a), hz - 0.07 + rr * math.sin(a)))
pole('hook_scroll', [(hx, hy - 0.015, hz - 0.13), (hx, hy - 0.07, hz - 0.11), (hx, hy - 0.16, hz - 0.03), (hx, hy - 0.2, hz + 0.005)], 0.007, 'Iron', 6)
pole('hook_curl', sc[1:], 0.006, 'Iron', 6)
pole('hook_eye', [(hx + 0.02 * math.cos(2 * math.pi * k / 12), hy - 0.30, hz - 0.025 + 0.02 * math.sin(2 * math.pi * k / 12) - 0.02)
                  for k in range(13)], 0.005, 'Iron', 6)

# ----------------------------------------------------------------------------- report
def tris_of(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)
TRIS = sum(tris_of(o) for o in PARTS)
print(f'STALL parts={len(PARTS)} tris={TRIS}')

# ----------------------------------------------------------------------------- bake stage: join, atlas, bake, game material
def bake():
    for o in PARTS:
        o.select_set(False)
    for o in PARTS:
        bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.objects.active = PARTS[0]
    bpy.ops.object.join()
    st = bpy.context.view_layer.objects.active; st.name = NAME; st.data.name = NAME
    me = st.data; at = me.uv_layers.new(name='atlas'); me.uv_layers.active = at
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.003, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')
    SC.render.engine = 'CYCLES'; SC.cycles.device = 'CPU'; SC.cycles.samples = 16; SC.cycles.use_denoising = False
    bk = SC.render.bake; bk.margin = 8; bk.use_selected_to_active = False; bk.use_clear = True
    md = os.path.join(OUT, 'maps'); os.makedirs(md, exist_ok=True); paths = {}
    def target(key, nc):
        im = bpy.data.images.new(f'{NAME}_{key}', RES, RES, alpha=False)
        im.colorspace_settings.name = 'Non-Color' if nc else 'sRGB'
        for s in st.material_slots:
            nt = s.material.node_tree
            for n in [n for n in nt.nodes if n.name == '__bake']: nt.nodes.remove(n)
            n = nt.nodes.new('ShaderNodeTexImage'); n.name = '__bake'; n.image = im; nt.nodes.active = n
        return im
    def route(key, on):
        for s in st.material_slots:
            nt = s.material.node_tree; bs = nt.nodes['BSDF']; out = nt.nodes['OUT']
            if on:
                em = nt.nodes.new('ShaderNodeEmission'); em.name = '__em'; sk = bs.inputs[key]
                if sk.is_linked: nt.links.new(sk.links[0].from_socket, em.inputs['Color'])
                else:
                    v = sk.default_value; em.inputs['Color'].default_value = tuple(v) if hasattr(v, '__len__') else (v, v, v, 1)
                nt.links.new(em.outputs[0], out.inputs['Surface'])
            else:
                for n in [n for n in nt.nodes if n.name == '__em']: nt.nodes.remove(n)
                nt.links.new(bs.outputs[0], out.inputs['Surface'])
    def save(im, key):
        p = os.path.join(md, f'{NAME}_{key}.png'); im.filepath_raw = p; im.file_format = 'PNG'; im.save(); paths[key] = p
    for key, sock, nc in (('BaseColor', 'Base Color', False), ('Metallic', 'Metallic', True)):
        im = target(key, nc); route(sock, True); bpy.ops.object.bake(type='EMIT'); route(sock, False); save(im, key)
    im = target('Roughness', True); bpy.ops.object.bake(type='ROUGHNESS'); save(im, 'Roughness')
    im = target('Normal', True); bk.normal_space = 'TANGENT'; bpy.ops.object.bake(type='NORMAL'); save(im, 'Normal')
    SC.world = SC.world or bpy.data.worlds.new('W'); SC.world.light_settings.distance = 0.25; SC.cycles.samples = 64
    im = target('AO', True); bpy.ops.object.bake(type='AO'); save(im, 'AO')
    # game material: baked maps only, on the atlas UV
    m = bpy.data.materials.new(NAME); m.use_nodes = True; nt = m.node_tree
    bs = nt.nodes['Principled BSDF']; uv = N(nt, 'ShaderNodeUVMap', uv_map='atlas').outputs['UV']
    def t(k, nc): return img(nt, paths[k], nc, uv).outputs['Color']
    link(nt, mix(nt, 1.0, t('BaseColor', False), t('AO', True), 'MULTIPLY'), bs.inputs['Base Color'])
    sep = lambda c: (lambda s: (link(nt, c, s.inputs[0]), s)[1])(N(nt, 'ShaderNodeSeparateColor'))
    link(nt, sep(t('Roughness', True)).outputs[0], bs.inputs['Roughness'])
    link(nt, sep(t('Metallic', True)).outputs[0], bs.inputs['Metallic'])
    nm = N(nt, 'ShaderNodeNormalMap', uv_map='atlas'); link(nt, t('Normal', True), nm.inputs['Color']); link(nt, nm.outputs[0], bs.inputs['Normal'])
    me.materials.clear(); me.materials.append(m)
    st.parent = ROOT; ROOT.matrix_world = Matrix.Identity(4)
    return st

STALL = bake() if STAGE == 'bake' else None
if '--export' in argv and STALL:
    for o in bpy.context.selected_objects: o.select_set(False)
    STALL.select_set(True); bpy.context.view_layer.objects.active = STALL
    for uvl in [l for l in STALL.data.uv_layers if l.name != 'atlas']:   # Unity samples UV0: ship the atlas only
        STALL.data.uv_layers.remove(uvl)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + '.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,   # identity root in Unity, like B4/B6
                             mesh_smooth_type='FACE', add_leaf_bones=False)

# ----------------------------------------------------------------------------- corner proxies, light, cameras
if '--no-render' in argv:
    sys.exit(0)
def u2b(p):               # Unity (x, y, z) -> Blender (-x, -z, y)
    return Vector((-p[0], -p[2], p[1]))
ROOT.location = u2b((4.5, 0, 4.5)); ROOT.rotation_euler = (0, 0, math.radians(160))   # Unity yaw 200
bpy.context.view_layer.update()

def proxy_mat(name, kind, scale=1.0, tint=(1, 1, 1, 1)):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    tc = N(nt, 'ShaderNodeTexCoord'); mp = N(nt, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = (1 / TILE,) * 3
    link(nt, tc.outputs['Object'], mp.inputs['Vector'])
    p = os.path.join(TEX, kind, kind)
    a = N(nt, 'ShaderNodeTexImage', projection='BOX', projection_blend=0.2); a.image = bpy.data.images.load(p + '_albedo.png', check_existing=True)
    link(nt, mp.outputs[0], a.inputs['Vector'])
    link(nt, mix(nt, 1.0, a.outputs['Color'], tint, 'MULTIPLY'), bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value = 0.85
    return m
def proxy(name, size, loc, mat):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    ob = obj_from_bm(name, bm); ob.location = loc; ob.data.materials.append(mat); return ob
proxy('ground', (24, 24, 0.1), (0, -1, -0.05), proxy_mat('pCobble', 'cobble'))
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=48, radius1=1.7, radius2=1.7, depth=0.55)
f = obj_from_bm('fountain', bm); f.location = (0, 0, 0.275); f.data.materials.append(proxy_mat('pSand', 'sandstone'))
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=0.3, radius2=0.22, depth=2.4)
c = obj_from_bm('spout', bm); c.location = (0, 0, 1.2); c.data.materials.append(bpy.data.materials['pSand'])
proxy('facade', (7.5, 0.5, 6.0), u2b((-1.25, 3.0, 7.75)), proxy_mat('pPlaster', 'plaster'))
proxy('plinth', (7.6, 0.6, 0.6), u2b((-1.25, 0.3, 7.7)), bpy.data.materials['pSand'])
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.06, radius2=0.05, depth=4.5)
bp = obj_from_bm('bannerpole', bm); bp.location = u2b((-2.6, 2.25, 1.8)); bp.data.materials.append(proxy_mat('pOak', 'oak_timber'))
# 1.75 m figure for scale (visible in the stall-local views only, beside the fountain-side front corner)
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=0.2, radius2=0.16, depth=1.5)
fig = obj_from_bm('figure', bm); fig.location = (-2.5, -1.4, 0.75)
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=0.13)
hd = obj_from_bm('figure_head', bm); hd.location = (-2.5, -1.4, 1.62)
fm = bpy.data.materials.new('pFig'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.25, 0.27, 1)
for o in (fig, hd): o.data.materials.append(fm); o.parent = ROOT

w = bpy.data.worlds.new('Day'); SC.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.36, 0.46, 0.62, 1); bg.inputs['Strength'].default_value = 0.9
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); SC.collection.objects.link(sun)
sun.data.energy = 4.2; sun.data.angle = math.radians(1.5)
k = 5200.0   # Kelvin -> rgb (approximate warm white)
sun.data.color = (1.0, 0.89, 0.76)
d = Vector((0.38, -0.55, -0.74)).normalized(); sun.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()

cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); SC.collection.objects.link(cam); SC.camera = cam
cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(55)
SC.render.engine = 'CYCLES'; SC.cycles.device = 'CPU'; SC.cycles.samples = SAMPLES; SC.cycles.use_denoising = True
SC.cycles.max_bounces = 4; SC.render.resolution_x = RRES; SC.render.resolution_y = RRES * 9 // 16
SC.view_settings.view_transform = 'AgX'; SC.view_settings.look = 'AgX - Medium High Contrast'
SC.render.image_settings.file_format = 'PNG'
M = ROOT.matrix_world
VIEWDEF = {   # name: (position, target, space)  unity = corner capture cameras (brief 1); local = stall frame
    'wide': ((1, 1.8, -9), (-1, 3, 6), 'unity'),
    'mid': ((5, 1.7, -2.5), (-1.5, 1.8, 5), 'unity'),
    'close': ((1.6, 1.4, -1.6), (0, 1.0, 0.6), 'unity'),
    'tq': ((-3.0, -4.0, 1.65), (0.0, 0.0, 1.35), 'local'),
    'front': ((0.0, -4.6, 1.6), (0.0, 0.0, 1.35), 'local'),
    'side': ((3.6, -3.0, 1.6), (0.3, 0.0, 1.3), 'local'),
}
for v in VIEWS:
    p, t, sp = VIEWDEF[v]
    if sp == 'unity': P, T = u2b(p), u2b(t)
    else: P, T = M @ Vector(p), M @ Vector(t)
    fig.hide_render = hd.hide_render = sp == 'unity'
    cam.location = P; cam.rotation_euler = (T - P).to_track_quat('-Z', 'Y').to_euler()
    SC.render.filepath = os.path.join(OUT, f'stall_{STAGE}_{v}.png')
    bpy.ops.render.render(write_still=True)
    print('RENDER', SC.render.filepath)
