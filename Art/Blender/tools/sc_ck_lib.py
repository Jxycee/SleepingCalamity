"""ck_lib - shared helpers for the expK cottage (Blender 4.5 headless, Cycles CPU).

Adapted from the fg-aaa-prop pipeline (aaa_kit.py: NT shader DSL, numpy fbm/erosion, selected-to-active
PBR bake with BaseColor/Metallic through the emission route) for a building:
  * Cycles on CPU (no OPTIX), bake samples kept low per pass;
  * scans: CC0 Poly Haven maps projected in part space (attribute 'lp' + normal 'ln'), triplanar;
  * atlas bakes (many variant lows -> one image) and unique bakes (one joined low).
Paths always come from the caller (env CK_TEX = folder of scans); nothing session-specific here.
"""
import bpy, bmesh, math, os, random, zlib
import numpy as np
from mathutils import Vector, Matrix

TEXDIR = os.environ.get('CK_TEX', '')

# ------------------------------------------------------------------ scene
def reset():
    bpy.ops.wm.read_homefile(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    return sc

def cycles(sc=None, samples=16):
    sc = sc or bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.render.threads_mode = 'AUTO'
    return sc

def rng(stream, seed=1337):
    return random.Random(seed ^ zlib.crc32(stream.encode()))

def coll(name):
    c = bpy.data.collections.get(name)
    if not c:
        c = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c)
    return c

# ------------------------------------------------------------------ meshes
def mesh_obj(name, V, F, collection=None, smooth=False):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(map(float, v)) for v in V], [], [tuple(int(i) for i in f) for f in F])
    me.validate(); me.update()
    ob = bpy.data.objects.new(name, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    if smooth: set_smooth(ob, True)
    return ob

def set_smooth(ob, on=True):
    ob.data.polygons.foreach_set('use_smooth', [on] * len(ob.data.polygons)); ob.data.update()

def box_VF(sx, sy, sz, c=(0, 0, 0)):
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    V = [(c[0] + x * hx, c[1] + y * hy, c[2] + z * hz) for z in (-1, 1) for y in (-1, 1) for x in (-1, 1)]
    F = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)]
    return V, F

def prism_VF(outline, z0, z1):
    """Extrude a CCW 2D outline (n,2) from z0 to z1. Returns V, F (n-gon caps, quad sides)."""
    n = len(outline)
    V = [(x, y, z0) for x, y in outline] + [(x, y, z1) for x, y in outline]
    F = [tuple(range(n))[::-1], tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        F.append((i, j, n + j, n + i))
    return V, F

def join(name, obs):
    obs = [o for o in obs if o]
    if len(obs) == 1:
        obs[0].name = name; return obs[0]
    with bpy.context.temp_override(active_object=obs[0], selected_editable_objects=obs, selected_objects=obs):
        bpy.ops.object.join()
    obs[0].name = name; obs[0].data.name = name
    return obs[0]

def apply_xf(ob):
    bpy.context.view_layer.update()      # matrix_world is stale right after setting .location
    ob.data.transform(ob.matrix_world); ob.matrix_world = Matrix.Identity(4)

def apply_mods(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    old = ob.data; ob.modifiers.clear(); ob.data = me
    if old.users == 0: bpy.data.meshes.remove(old)

def remesh(ob, voxel):
    m = ob.modifiers.new('rm', 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = voxel; m.adaptivity = 0.0
    apply_mods(ob)

def triangulate(ob):
    m = ob.modifiers.new('tri', 'TRIANGULATE'); m.quad_method = 'BEAUTY'; m.ngon_method = 'BEAUTY'; apply_mods(ob)

def decimate(ob, ratio):
    m = ob.modifiers.new('dec', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = ratio; m.use_collapse_triangulate = True
    apply_mods(ob)

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def dup(ob, name, link=True):
    o2 = ob.copy(); o2.data = ob.data.copy(); o2.name = name; o2.data.name = name
    if link: bpy.context.scene.collection.objects.link(o2)
    return o2

def mesh_arrays(ob):
    me = ob.data; n = len(me.vertices)
    co = np.empty(n * 3); me.vertices.foreach_get('co', co)
    no = np.empty(n * 3); me.vertices.foreach_get('normal', no)
    return co.reshape(n, 3), no.reshape(n, 3)

def set_co(ob, co):
    ob.data.vertices.foreach_set('co', np.asarray(co, dtype=np.float64).reshape(-1)); ob.data.update()

def store_attr(ob, name, arr, kind='FLOAT_VECTOR'):
    me = ob.data; a = me.attributes.get(name) or me.attributes.new(name, kind, 'POINT')
    a.data.foreach_set('vector' if kind == 'FLOAT_VECTOR' else 'value', np.asarray(arr, dtype=np.float64).reshape(-1))

def store_lp(ob, pid=0.0):
    """Part-space coords and normal (before the part is placed), plus a part id."""
    co, no = mesh_arrays(ob)
    store_attr(ob, 'lp', co); store_attr(ob, 'ln', no); store_attr(ob, 'pid', np.full(len(co), pid), 'FLOAT')

def uv_unwrap(ob, margin=0.003, angle=66, area_weight=0.0):
    for o in list(bpy.context.scene.objects): o.select_set(False)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin, area_weight=area_weight, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')

def uv_world_box(ob, scale=1.0):
    """Tiling UVs in metres (box projection by face normal) for tiling materials."""
    me = ob.data
    if not me.uv_layers: me.uv_layers.new(name='UVMap')
    uv = me.uv_layers.active.data
    co, _ = mesh_arrays(ob)
    for p in me.polygons:
        n = p.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for li in p.loop_indices:
            v = co[me.loops[li].vertex_index]
            if ax == 0: u = (v[1] * np.sign(n[0]), v[2])
            elif ax == 1: u = (-v[0] * np.sign(n[1]), v[2])
            else: u = (v[0], v[1])
            uv[li].uv = (u[0] * scale, u[1] * scale)

# ------------------------------------------------------------------ numpy noise / erosion
def _hash(i, j, k, s):
    return np.modf(np.sin(i * 127.1 + j * 311.7 + k * 74.7 + s * 19.19) * 43758.5453)[0] % 1.0

def vnoise(P, seed=0.0):
    Pi = np.floor(P); f = P - Pi; u = f * f * (3 - 2 * f)
    i, j, k = Pi[:, 0], Pi[:, 1], Pi[:, 2]
    def c(a, b, cc): return _hash(i + a, j + b, k + cc, seed)
    x00 = c(0, 0, 0) * (1 - u[:, 0]) + c(1, 0, 0) * u[:, 0]; x10 = c(0, 1, 0) * (1 - u[:, 0]) + c(1, 1, 0) * u[:, 0]
    x01 = c(0, 0, 1) * (1 - u[:, 0]) + c(1, 0, 1) * u[:, 0]; x11 = c(0, 1, 1) * (1 - u[:, 0]) + c(1, 1, 1) * u[:, 0]
    y0 = x00 * (1 - u[:, 1]) + x10 * u[:, 1]; y1 = x01 * (1 - u[:, 1]) + x11 * u[:, 1]
    return y0 * (1 - u[:, 2]) + y1 * u[:, 2]

def fbm(P, scale=1.0, octaves=4, gain=0.5, seed=0.0):
    a, s, tot, norm = 1.0, scale, 0.0, 0.0
    for o in range(octaves):
        tot = tot + a * vnoise(P * s, seed + o * 7.3); norm += a; a *= gain; s *= 2.0
    return tot / norm

def sstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def edges_of(ob):
    me = ob.data; e = np.empty(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get('vertices', e); return e.reshape(-1, 2)

def curvature(ob, smooth_iters=3):
    me = ob.data; n = len(me.vertices); co, no = mesh_arrays(ob); e = edges_of(ob)
    i, j = e[:, 0], e[:, 1]; d = co[j] - co[i]; l2 = (d * d).sum(1) + 1e-12
    ci = -(no[i] * d).sum(1) / l2; cj = (no[j] * d).sum(1) / l2
    acc = np.zeros(n); cnt = np.zeros(n)
    np.add.at(acc, i, ci); np.add.at(acc, j, cj); np.add.at(cnt, i, 1); np.add.at(cnt, j, 1)
    c = acc / np.maximum(cnt, 1)
    for _ in range(smooth_iters):
        s = np.zeros(n); np.add.at(s, i, c[j]); np.add.at(s, j, c[i]); c = 0.5 * c + 0.5 * s / np.maximum(cnt, 1)
    return c * 2.0

def laplace(ob, field, iters=2):
    e = edges_of(ob); n = len(field); cnt = np.zeros(n); np.add.at(cnt, e[:, 0], 1); np.add.at(cnt, e[:, 1], 1)
    f = field.copy()
    for _ in range(iters):
        s = np.zeros(n); np.add.at(s, e[:, 0], f[e[:, 1]]); np.add.at(s, e[:, 1], f[e[:, 0]]); f = 0.5 * f + 0.5 * s / np.maximum(cnt, 1)
    return f

def erode_box(ob, half, wear=0.006, width=0.02, chips=1.0, dents=1.0, seed=0.0, checks=1.0, adze=0.0012, grain=0):
    """Hand-worn timber on a box part (rest half-sizes `half`, long axis = `grain`): rounded wear that varies
    along the edge, crisp chips in an edge band, crisp dents, drying checks (splits) along the grain on the
    long faces, and adze scallops across the long faces. Verts move inward along the normal."""
    co, no = mesh_arrays(ob); h = np.array(half)
    dfa = np.sort(h[None, :] - np.abs(co), axis=1); edist = dfa[:, 1]
    st = np.ones(3); st[grain] = 0.25
    var = fbm(co * st, 5.0, 3, 0.5, seed)
    reach = width * (0.5 + 1.0 * var)
    d = wear * (0.35 + 1.0 * var) * (1.0 - sstep(edist, 0.0, reach)) ** 1.6
    cs = np.ones(3); cs[grain] = 0.3
    cn = fbm(co * cs, 18.0, 3, 0.5, seed + 3.1)
    cm = sstep(cn, 0.63, 0.645) * (1.0 - sstep(edist, width * 0.9, width * 1.3)) * chips
    d = np.maximum(d, cm * wear * (1.3 + 0.8 * fbm(co, 60.0, 2, 0.5, seed + 4.4)))
    dn = fbm(co, 30.0, 2, 0.5, seed + 5.5)
    d += dents * 0.0015 * sstep(dn, 0.74, 0.755)
    d += 0.0008 * fbm(co * st, 4.0, 2, 0.5, seed + 1.7)
    if checks:
        # drying checks: narrow V grooves along the grain, wandering, broken into lengths
        ax = [a for a in range(3) if a != grain]
        for k, a in enumerate(ax):
            onface = sstep(np.abs(co[:, a]) / h[a], 0.9, 0.98)        # faces normal to axis a
            b = ax[1 - k]
            wob = 0.012 * (fbm(co * st, 3.0, 2, 0.5, seed + 9 + k) - 0.5)
            ph = (co[:, b] + wob) / (h[b] * 0.55)
            line = np.abs(ph - np.round(ph))
            seg = sstep(fbm(co * st, 2.0, 2, 0.5, seed + 12 + k), 0.5, 0.62)
            d += checks * 0.006 * onface * seg * np.clip(1 - line / 0.03, 0, 1) ** 2
    if adze:
        ph = co[:, grain] / (0.09 + 0.03 * fbm(co, 3.0, 1, 0.5, seed + 8.8)) + 3.0 * fbm(co, 2.0, 2, 0.5, seed + 6.1)
        d += adze * (np.abs(np.sin(np.pi * ph))) ** 0.6 * sstep(edist, 0.01, 0.03)
    set_co(ob, co - no * d[:, None])

def scoops(ob, count, rmin, rmax, depth=0.5, convex=60.0, seed=0.0, flake=True, co=None, no=None, cv=None):
    """Flake chips on convex vertices (planar tilted slice: flat scar, crisp edge). Returns inward displacement."""
    from mathutils import kdtree
    if co is None: co, no = mesh_arrays(ob)
    if cv is None: cv = curvature(ob)
    cand = np.nonzero(cv > convex)[0]; d = np.zeros(len(co))
    if len(cand) == 0 or count <= 0: return d
    rs = np.random.default_rng(int(seed * 1000) & 0xffffffff)
    kd = kdtree.KDTree(len(co))
    for i, c in enumerate(co): kd.insert(c, i)
    kd.balance()
    for pi in rs.choice(cand, size=min(count, len(cand)), replace=False):
        R = rs.uniform(rmin, rmax); hits = kd.find_range(co[pi], R)
        if not hits: continue
        idx = np.array([h[1] for h in hits]); dist = np.array([h[2] for h in hits])
        if flake:
            t = rs.normal(size=3); t -= no[pi] * t.dot(no[pi]); t /= (np.linalg.norm(t) + 1e-9)
            n2 = no[pi] + t * rs.uniform(0.5, 1.2); n2 /= np.linalg.norm(n2)
            p0 = co[pi] - no[pi] * depth * R
            bite = np.maximum(0.0, ((co[idx] - p0) * n2).sum(1)) * np.clip(1.4 - dist / R, 0, 1)
            bite = np.minimum(bite, depth * R * 1.6)
        else:
            c = co[pi] - no[pi] * R * rs.uniform(0.15, 0.45); dd = np.linalg.norm(co[idx] - c, axis=1)
            bite = depth * R * np.sqrt(np.clip(1 - (dd / R) ** 2, 0, 1))
        d[idx] = np.maximum(d[idx], bite)
    return d

# ------------------------------------------------------------------ shader DSL (from aaa_kit.NT, trimmed)
class NT:
    def __init__(self, mat):
        self.m = mat; self.t = mat.node_tree; self.n = self.t.nodes; self.l = self.t.links; self.x = -1400; self._img = {}
    def node(self, typ, **props):
        nd = self.n.new(typ)
        for k, v in props.items(): setattr(nd, k, v)
        nd.location = (self.x, 0); self.x += 40
        return nd
    def sock(self, s, out=True):
        if isinstance(s, tuple):
            nd, k = s; return (nd.outputs if out else nd.inputs)[k]
        return s.outputs[0] if out else s.inputs[0]
    def link(self, a, b): self.l.new(self.sock(a), self.sock(b, out=False))
    def set(self, nd, key, v):
        if isinstance(v, np.ndarray): v = tuple(float(x) for x in v.ravel())
        if isinstance(v, (tuple, bpy.types.Node)) and not (isinstance(v, tuple) and all(isinstance(c, (int, float)) for c in v)):
            self.link(v, (nd, key))
        else:
            sk = nd.inputs[key]
            if isinstance(v, tuple) and sk.type == 'RGBA' and len(v) == 3: v = (*v, 1.0)
            if not isinstance(v, tuple) and sk.type in ('RGBA', 'VECTOR'):
                v = (v, v, v, 1.0) if sk.type == 'RGBA' else (v, v, v)
            sk.default_value = v
    def attr(self, name, vec=True):
        nd = self.node('ShaderNodeAttribute', attribute_name=name); return (nd, 'Vector' if vec else 'Fac')
    def geo(self, out): return (self.node('ShaderNodeNewGeometry'), out)
    def math(self, op, a, b=0.0, c=0.0, clamp=False):
        nd = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.set(nd, 0, a); self.set(nd, 1, b)
        if len(nd.inputs) > 2: self.set(nd, 2, c)
        return (nd, 0)
    def vmath(self, op, a, b=(0, 0, 0)):
        nd = self.node('ShaderNodeVectorMath', operation=op); self.set(nd, 0, a)
        if op not in ('LENGTH', 'NORMALIZE', 'ABSOLUTE', 'FLOOR', 'FRACTION', 'SINE', 'COSINE'): self.set(nd, 1, b)
        return (nd, 'Value' if op in ('DOT_PRODUCT', 'LENGTH', 'DISTANCE') else 'Vector')
    def sep(self, v):
        nd = self.node('ShaderNodeSeparateXYZ'); self.set(nd, 0, v); return nd
    def comb(self, x, y, z):
        nd = self.node('ShaderNodeCombineXYZ'); self.set(nd, 0, x); self.set(nd, 1, y); self.set(nd, 2, z); return (nd, 0)
    def maprange(self, v, a, b, c=0.0, d=1.0, clamp=True):
        nd = self.node('ShaderNodeMapRange', clamp=clamp); self.set(nd, 'Value', v)
        nd.inputs['From Min'].default_value = a; nd.inputs['From Max'].default_value = b
        nd.inputs['To Min'].default_value = c; nd.inputs['To Max'].default_value = d
        return (nd, 'Result')
    def smoothstep(self, v, a, b):
        nd = self.node('ShaderNodeMapRange', clamp=True, interpolation_type='SMOOTHSTEP'); self.set(nd, 'Value', v)
        self.set(nd, 'From Min', a); self.set(nd, 'From Max', b)
        return (nd, 'Result')
    def noise(self, vec, scale=5.0, detail=4.0, rough=0.55, dist=0.0, out='Fac', lac=2.0):
        nd = self.node('ShaderNodeTexNoise', noise_dimensions='3D'); self.set(nd, 'Vector', vec)
        nd.inputs['Scale'].default_value = scale; nd.inputs['Detail'].default_value = detail
        nd.inputs['Roughness'].default_value = rough; nd.inputs['Distortion'].default_value = dist; nd.inputs['Lacunarity'].default_value = lac
        return (nd, out)
    def voronoi(self, vec, scale=5.0, feature='F1', out='Distance', rand=1.0):
        nd = self.node('ShaderNodeTexVoronoi', feature=feature); self.set(nd, 'Vector', vec)
        nd.inputs['Scale'].default_value = scale; nd.inputs['Randomness'].default_value = rand
        return (nd, out)
    def white(self, vec, dim='1D', out='Value'):
        nd = self.node('ShaderNodeTexWhiteNoise', noise_dimensions=dim); self.set(nd, 'W' if dim == '1D' else 'Vector', vec)
        return (nd, out)
    def mix(self, fac, a, b, blend='MIX'):
        nd = self.node('ShaderNodeMix', data_type='RGBA', blend_type=blend, clamp_result=True)
        self.set(nd, 0, fac); self.set(nd, 6, a); self.set(nd, 7, b); return (nd, 2)
    def mixf(self, fac, a, b):
        nd = self.node('ShaderNodeMix', data_type='FLOAT', clamp_factor=True)
        self.set(nd, 0, fac); self.set(nd, 2, a); self.set(nd, 3, b); return (nd, 0)
    def hsv(self, col, h=0.5, s=1.0, v=1.0):
        nd = self.node('ShaderNodeHueSaturation'); self.set(nd, 'Color', col); self.set(nd, 'Hue', h); self.set(nd, 'Saturation', s); self.set(nd, 'Value', v)
        return (nd, 'Color')
    def gray(self, col):
        nd = self.node('ShaderNodeRGBToBW'); self.set(nd, 0, col); return (nd, 0)
    def bump(self, height, strength=1.0, distance=0.001, normal=None):
        nd = self.node('ShaderNodeBump'); self.set(nd, 'Height', height)
        nd.inputs['Strength'].default_value = strength; nd.inputs['Distance'].default_value = distance
        if normal is not None: self.set(nd, 'Normal', normal)
        return (nd, 'Normal')
    def edge(self, radius=0.004, lo=0.02, hi=0.25, samples=8):
        bv = self.node('ShaderNodeBevel', samples=min(samples, 6)); bv.inputs['Radius'].default_value = radius
        d = self.vmath('DOT_PRODUCT', (bv, 'Normal'), self.geo('Normal'))
        return self.smoothstep(self.math('SUBTRACT', 1.0, d), lo, hi)
    def cavity(self, dist=0.03, lo=0.4, hi=1.0, samples=16):
        ao = self.node('ShaderNodeAmbientOcclusion', samples=min(samples, 8), only_local=True); ao.inputs['Distance'].default_value = dist
        return self.smoothstep((ao, 'AO'), lo, hi)
    # ---- scans
    def image(self, asset, kind):
        key = (asset, kind)
        if key not in self._img:
            p = os.path.join(TEXDIR, f'{asset}_{kind}_2k.jpg')
            img = bpy.data.images.load(p, check_existing=True)
            img.colorspace_settings.name = 'sRGB' if kind == 'diff' else 'Non-Color'
            self._img[key] = img
        return self._img[key]
    def tex(self, asset, kind, vec):
        nd = self.node('ShaderNodeTexImage'); nd.image = self.image(asset, kind); nd.interpolation = 'Linear'
        self.set(nd, 'Vector', vec); return (nd, 'Color')
    def tri(self, asset, kind, P, N, scale=1.0, sharp=4.0, rot=0.0):
        """Triplanar scan lookup in part space P (vector socket) with normal N (vector socket).
        Weights |n|^sharp. Each plane is offset so the three projections don't line up."""
        ps = self.vmath('SCALE', P) if False else self.vmath('MULTIPLY', P, (scale, scale, scale))
        s = self.sep(ps); ns = self.sep(self.vmath('ABSOLUTE', N))
        wx = self.math('POWER', (ns, 'X'), sharp); wy = self.math('POWER', (ns, 'Y'), sharp); wz = self.math('POWER', (ns, 'Z'), sharp)
        tot = self.math('ADD', self.math('ADD', wx, wy), wz)
        cx = self.tex(asset, kind, self.comb((s, 'Y'), (s, 'Z'), 0.0))
        cy = self.tex(asset, kind, self.comb(self.math('ADD', (s, 'X'), 0.37), (s, 'Z'), 0.0))
        cz = self.tex(asset, kind, self.comb(self.math('ADD', (s, 'X'), 0.71), self.math('ADD', (s, 'Y'), 0.13), 0.0))
        m1 = self.mix(self.math('DIVIDE', wy, self.math('ADD', self.math('ADD', wx, wy), 1e-5)), cx, cy)
        return self.mix(self.math('DIVIDE', wz, self.math('ADD', tot, 1e-5)), m1, cz)
    def grain_map(self, asset, kind, P, N, scale=1.0, grain_axis='X', img_grain='V'):
        """Planar scan lookup for timber: the image's grain direction follows the part's long axis.
        P, N part-space; long axis X. Side faces (normal ~Y) use (x, z), top/bottom (normal ~Z) use (x, y);
        end grain gets the cross plane (y, z)."""
        ps = self.vmath('MULTIPLY', P, (scale, scale, scale)); s = self.sep(ps); ns = self.sep(self.vmath('ABSOLUTE', N))
        def uvc(a, b):     # a along the grain
            return self.comb(b, a, 0.0) if img_grain == 'V' else self.comb(a, b, 0.0)
        cy = self.tex(asset, kind, uvc((s, 'X'), (s, 'Z')))
        cz = self.tex(asset, kind, uvc(self.math('ADD', (s, 'X'), 0.41), self.math('ADD', (s, 'Y'), 0.23)))
        cx = self.tex(asset, kind, self.comb(self.math('ADD', (s, 'Y'), 0.61), (s, 'Z'), 0.0))
        wy = self.math('POWER', (ns, 'Y'), 6.0); wz = self.math('POWER', (ns, 'Z'), 6.0); wx = self.math('POWER', (ns, 'X'), 6.0)
        m1 = self.mix(self.math('DIVIDE', wz, self.math('ADD', self.math('ADD', wy, wz), 1e-5)), cy, cz)
        return self.mix(self.math('DIVIDE', wx, self.math('ADD', self.math('ADD', self.math('ADD', wx, wy), wz), 1e-5)), m1, cx)
    def output(self, color, rough, metal=0.0, normal=None, **extra):
        bs = self.node('ShaderNodeBsdfPrincipled')
        self.set(bs, 'Base Color', color); self.set(bs, 'Roughness', rough); self.set(bs, 'Metallic', metal)
        if normal is not None: self.set(bs, 'Normal', normal)
        for k, v in extra.items(): self.set(bs, k.replace('_', ' '), v)
        out = self.node('ShaderNodeOutputMaterial'); self.link((bs, 'BSDF'), (out, 'Surface')); self.bsdf = bs
        return bs

def new_mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True; m.node_tree.nodes.clear()
    return m, NT(m)

# ------------------------------------------------------------------ baking (Cycles CPU)
def _route_emission(ob, key, on):
    for slot in ob.material_slots:
        if not slot.material: continue
        nt = slot.material.node_tree
        bs = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None)
        if bs is None or out is None: continue
        if on:
            em = nt.nodes.new('ShaderNodeEmission'); em.name = '__em'
            sk = bs.inputs[key]
            if sk.is_linked: nt.links.new(sk.links[0].from_socket, em.inputs['Color'])
            else:
                v = sk.default_value; v = tuple(v) if hasattr(v, '__len__') else (v, v, v, 1)
                em.inputs['Color'].default_value = v if len(v) == 4 else (*v, 1)
            nt.links.new(em.outputs[0], out.inputs['Surface'])
        else:
            for n in [n for n in nt.nodes if n.name == '__em']: nt.nodes.remove(n)
            nt.links.new(bs.outputs['BSDF'], out.inputs['Surface'])

def _route_rm(ob, on):
    """Emission = (roughness, metallic, 0): one EMIT pass gives both maps."""
    for slot in ob.material_slots:
        if not slot.material: continue
        nt = slot.material.node_tree
        bs = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None)
        if bs is None or out is None: continue
        if on:
            em = nt.nodes.new('ShaderNodeEmission'); em.name = '__em'
            cb = nt.nodes.new('ShaderNodeCombineXYZ'); cb.name = '__em'
            for i, key in enumerate(('Roughness', 'Metallic')):
                sk = bs.inputs[key]
                if sk.is_linked: nt.links.new(sk.links[0].from_socket, cb.inputs[i])
                else: cb.inputs[i].default_value = sk.default_value
            nt.links.new(cb.outputs[0], em.inputs['Color'])
            nt.links.new(em.outputs[0], out.inputs['Surface'])
        else:
            for n in [n for n in nt.nodes if n.name == '__em']: nt.nodes.remove(n)
            nt.links.new(bs.outputs['BSDF'], out.inputs['Surface'])

def _split_channel(img, ch, name, path):
    import numpy as _np
    w, h = img.size; px = _np.empty(w * h * 4, dtype=_np.float32); img.pixels.foreach_get(px); px = px.reshape(-1, 4)
    o = bpy.data.images.new(name, w, h, alpha=False, float_buffer=False); o.colorspace_settings.name = 'Non-Color'
    q = _np.repeat(px[:, ch:ch + 1], 4, 1); q[:, 3] = 1.0
    o.pixels.foreach_set(q.reshape(-1)); return _save(o, path)

def _target(mats, name, res, nc):
    img = bpy.data.images.get(name)
    if img: bpy.data.images.remove(img)
    img = bpy.data.images.new(name, res, res, alpha=False)
    img.colorspace_settings.name = 'Non-Color' if nc else 'sRGB'
    for m in mats:
        nt = m.node_tree
        for nd in [n for n in nt.nodes if n.name == '__bake']: nt.nodes.remove(nd)
        nd = nt.nodes.new('ShaderNodeTexImage'); nd.name = '__bake'; nd.image = img; nt.nodes.active = nd
    return img

def _save(img, path):
    img.filepath_raw = path; img.file_format = 'PNG'; img.save(); return path

def bake(low, highs, outdir, prefix, res=2048, maps=('BaseColor', 'Roughness', 'Normal', 'Metallic', 'AO'),
         extrusion=0.01, ray=0.03, margin=8, ao_dist=0.25, ao_samples=32, samples=4, emission=False, ao_res=None, ao_extra=(), metal_const=None):
    """Bake PBR maps to low's UVs. highs=None bakes the low's own materials (procedural on the low itself);
    otherwise selected-to-active from highs. ao_extra: extra objects visible during the AO pass (whole-house AO)."""
    sc = bpy.context.scene; cycles(sc, samples); sc.cycles.use_denoising = False
    bk = sc.render.bake; bk.margin = margin; bk.use_clear = True
    s2a = bool(highs)
    bk.use_selected_to_active = s2a
    if s2a and len(highs) > 1:
        # Blender runs the engine ONCE PER selected high (full scene sync each time): 138 timber highs x 9M tris
        # spent ~1 h per pass syncing on one core. One joined high = one sync per pass.
        highs = [join(highs[0].name + '_joined', list(highs))]
    if s2a:
        bk.cage_extrusion = extrusion; bk.max_ray_distance = ray
        tm, _ = new_mat(prefix + '_bake'); low.data.materials.clear(); low.data.materials.append(tm)
        mats = [tm]; srcs = highs
    else:
        mats = [s.material for s in low.material_slots if s.material]; srcs = [low]
    for o in list(bpy.context.scene.objects): o.select_set(False)
    if s2a:
        for h in highs: h.select_set(True)
    low.select_set(True); bpy.context.view_layer.objects.active = low
    os.makedirs(outdir, exist_ok=True); paths = {}
    P = lambda k: os.path.join(outdir, f'{prefix}_{k}.png')
    combo = 'Roughness' in maps and 'Metallic' in maps and metal_const is None
    for key in maps:
        nc = key not in ('BaseColor', 'Emission')
        if key == 'Metallic' and combo: continue
        if key == 'Roughness' and combo:
            img = _target(mats, f'{prefix}_RM', res, True)
            for h in srcs: _route_rm(h, True)
            bpy.ops.object.bake(type='EMIT')
            for h in srcs: _route_rm(h, False)
            paths['Roughness'] = _split_channel(img, 0, f'{prefix}_Roughness', P('Roughness'))
            paths['Metallic'] = _split_channel(img, 1, f'{prefix}_Metallic', P('Metallic'))
            print('BAKED', prefix, 'Roughness+Metallic', res, flush=True); continue
        if key == 'Metallic' and metal_const is not None:      # non-metal set: no need to evaluate the shader again
            os.makedirs(outdir, exist_ok=True)
            im = bpy.data.images.new(f'{prefix}_{key}', 256, 256, alpha=False); im.colorspace_settings.name = 'Non-Color'
            im.generated_color = (metal_const, metal_const, metal_const, 1.0)
            paths[key] = _save(im, P(key)); continue
        r = (ao_res or res) if key == 'AO' else res
        img = _target(mats, f'{prefix}_{key}', r, nc)
        if key in ('BaseColor', 'Metallic'):
            for h in srcs: _route_emission(h, 'Base Color' if key == 'BaseColor' else 'Metallic', True)
            bpy.ops.object.bake(type='EMIT')
            for h in srcs: _route_emission(h, None, False)
        elif key == 'Roughness':
            for h in srcs: _route_rm(h, True)
            bpy.ops.object.bake(type='EMIT')
            for h in srcs: _route_rm(h, False)
            paths[key] = _split_channel(img, 0, f'{prefix}_Roughness_', P(key)); print('BAKED', prefix, key, flush=True); continue
        elif key == 'Normal':
            bk.normal_space = 'TANGENT'; bpy.ops.object.bake(type='NORMAL')
        elif key == 'Emission':
            bpy.ops.object.bake(type='EMIT')
        elif key == 'AO':
            sc.world = sc.world or bpy.data.worlds.new('W'); sc.world.light_settings.distance = ao_dist
            sc.cycles.samples = ao_samples
            hidden = []
            bpy.ops.object.bake(type='AO')
            sc.cycles.samples = samples
        paths[key] = _save(img, P(key))
        print('BAKED', prefix, key, r, flush=True)
    for m in mats:
        for nd in [n for n in m.node_tree.nodes if n.name == '__bake']: m.node_tree.nodes.remove(nd)
    return paths

def game_material(name, paths, ao=0.6, emission=0.0, detail=None, detail_scale=1.0, detail_strength=0.6, normal_strength=1.0):
    """Principled driven ONLY by the baked maps (the URP Lit stand-in). AO multiplies albedo at `ao`
    (URP applies occlusion to indirect light only; Cycles already traces direct shadows).
    detail: optional (path) tiling detail normal in UV2-free mode: mapped by object coords (metres) like
    URP Lit's detail normal on a tiling UV."""
    m, T = new_mat(name)
    def tex(key, nc):
        nd = T.node('ShaderNodeTexImage'); nd.image = bpy.data.images.load(paths[key], check_existing=True)
        nd.image.colorspace_settings.name = 'Non-Color' if nc else 'sRGB'; return nd
    bc = tex('BaseColor', False)
    col = (bc, 'Color')
    if 'AO' in paths and ao > 0:
        col = T.mix(ao, col, (tex('AO', True), 'Color'), 'MULTIPLY')
    nm = T.node('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = normal_strength
    T.link((tex('Normal', True), 'Color'), (nm, 'Color'))
    nrm = (nm, 'Normal')
    if detail:
        dn = T.node('ShaderNodeTexImage'); dn.image = bpy.data.images.load(detail, check_existing=True); dn.image.colorspace_settings.name = 'Non-Color'
        T.set(dn, 'Vector', T.vmath('MULTIPLY', T.attr('UVMap.001') if False else (T.node('ShaderNodeUVMap', uv_map='Detail'), 'UV'), (detail_scale, detail_scale, 1)))
        dm = T.node('ShaderNodeNormalMap', uv_map='Detail'); dm.inputs['Strength'].default_value = detail_strength
        T.link((dn, 'Color'), (dm, 'Color'))
        # blend: add the detail perturbation to the base normal (UDN-ish)
        g = T.geo('Normal')
        nrm = T.vmath('NORMALIZE', T.vmath('ADD', nrm, T.vmath('SUBTRACT', (dm, 'Normal'), g)))
    rough = (tex('Roughness', True), 'Color'); metal = (tex('Metallic', True), 'Color') if 'Metallic' in paths else 0.0
    extra = {}
    if 'Emission' in paths and emission > 0:
        extra = dict(Emission_Color=(tex('Emission', False), 'Color'), Emission_Strength=emission)
    T.output(col, rough, metal, nrm, **extra)
    return m

def assign(ob, mat):
    ob.data.materials.clear(); ob.data.materials.append(mat)

def save_blend(path):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(path), compress=True, relative_remap=False)

def lathe_VF(prof, segs=32, cap_bottom=True, cap_top=True):
    """Revolve a profile [(r, z), ...] (bottom to top) around Z. Returns V, F (outward normals)."""
    V = []; n = len(prof)
    for i in range(segs):
        a = 2 * math.pi * i / segs; c, s = math.cos(a), math.sin(a)
        for r, z in prof: V.append((r * c, r * s, z))
    F = []
    for i in range(segs):
        i2 = (i + 1) % segs
        for k in range(n - 1):
            F.append((i * n + k, i2 * n + k, i2 * n + k + 1, i * n + k + 1))
    if cap_bottom: F.append(tuple(i * n for i in range(segs))[::-1])
    if cap_top: F.append(tuple(i * n + n - 1 for i in range(segs)))
    return V, F

def recalc_normals(ob):
    bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(ob.data); bm.free(); ob.data.update()

def erode_edges(ob, wear=0.005, band_iters=6, chips=30, chip_r=(0.006, 0.02), seed=0.0, dents=1.0, grain=None, checks=0.0, check_face=2, check_w=1):
    """Shape-agnostic wear for a remeshed part: rounded edges from a diffused convex-curvature band (varying
    along the edge), conchoidal flake chips on convex rims, crisp dents; optional drying checks on the faces
    whose normal is +-check_face, running along `grain` and spaced across `check_w`."""
    co, no = mesh_arrays(ob); cv = curvature(ob)
    band = laplace(ob, sstep(cv, 20.0, 160.0), band_iters)
    var = fbm(co, 6.0, 3, 0.5, seed)
    d = wear * band * (0.4 + 1.1 * var)
    if chips:
        d = np.maximum(d, scoops(ob, chips, chip_r[0], chip_r[1], 0.45, 40.0, seed + 1, True, co, no, cv))
    d += dents * 0.0012 * sstep(fbm(co, 30.0, 2, 0.5, seed + 5.5), 0.74, 0.755)
    if checks and grain is not None:
        st = np.ones(3); st[grain] = 0.25
        onface = sstep(np.abs(no[:, check_face]), 0.8, 0.95)
        wob = 0.01 * (fbm(co * st, 3.0, 2, 0.5, seed + 9) - 0.5)
        ph = (co[:, check_w] + wob) / 0.045
        line = np.abs(ph - np.round(ph))
        seg = sstep(fbm(co * st, 2.0, 2, 0.5, seed + 12), 0.52, 0.62)
        d += checks * 0.004 * onface * seg * np.clip(1 - line / 0.05, 0, 1) ** 2
    set_co(ob, co - no * d[:, None])

def remove_objects(obs):
    """Remove objects that may already be gone (bake() joins the highs)."""
    for o in list(obs):
        try: bpy.data.objects.remove(o)
        except (ReferenceError, RuntimeError): pass
    for o in [o for o in bpy.data.objects if o.name.endswith('_joined')]: bpy.data.objects.remove(o)
