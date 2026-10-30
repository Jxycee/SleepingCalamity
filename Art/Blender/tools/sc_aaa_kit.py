"""sc_aaa_kit - realistic (AAA/PBR) prop pipeline in headless Blender 5.2, no model generators.

Pipeline: parts as separate mid-poly objects (bevel + harden normals, hand-made warp)
-> each part stores its local coords ('lp') and a random id ('pid') as attributes
-> join per texture set -> smart UV -> procedural 'smart materials' (shader nodes that
read lp / pid / Bevel-node edges / AO-node cavity) -> Cycles bake to BaseColor, Normal,
Roughness, Metallic, AO -> game material (Principled with baked maps only) -> Eevee render.
"""
import bpy, bmesh, math, random, os, zlib
SCAN_DIR = os.environ.get('AAA_SCANS', '')   # folder of CC0 scans (<asset>_<diff|rough|disp>_2k.jpg); empty = procedural only
from mathutils import Vector, Matrix, noise

# ----------------------------------------------------------------- scene
def reset():
    bpy.ops.wm.read_homefile(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    return sc

def gpu(scene=None):
    sc = scene or bpy.context.scene
    sc.render.engine = 'CYCLES'
    p = bpy.context.preferences.addons['cycles'].preferences
    p.compute_device_type = 'OPTIX'; p.get_devices()
    for d in p.devices: d.use = d.type == 'OPTIX'
    sc.cycles.device = 'GPU'

def rng(stream, seed=1337):
    return random.Random(seed ^ zlib.crc32(stream.encode()))

# ----------------------------------------------------------------- geometry
def _obj_from_bm(name, bm, coll=None):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    (coll or bpy.context.scene.collection).objects.link(ob)
    return ob

def box(name, size, loc=(0, 0, 0), rot=(0, 0, 0), cuts=(1, 1, 1)):
    """Box centred on its origin, long axis should be local X for wood grain."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    ob = _obj_from_bm(name, bm)
    ob.location = loc; ob.rotation_euler = [math.radians(a) for a in rot]
    bpy.context.view_layer.update()   # lib fix 1: matrix_world is stale right after setting .location
    if any(c > 1 for c in cuts):
        subdivide_axes(ob, cuts)
    return ob

def subdivide_axes(ob, cuts):
    """Add edge loops along each local axis so warps/bends have vertices."""
    bm = bmesh.new(); bm.from_mesh(ob.data)
    for axis, n in enumerate(cuts):
        if n <= 1: continue
        lo = min(v.co[axis] for v in bm.verts); hi = max(v.co[axis] for v in bm.verts)
        for i in range(1, n):
            t = lo + (hi - lo) * i / n
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            no = Vector((0, 0, 0)); no[axis] = 1
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=no * t, plane_no=no)
    bm.to_mesh(ob.data); bm.free()

def smooth_profile(prof, step=0.004, closed=False):
    """Catmull-Rom resample of an (r, z) profile so revolved silhouettes stay round (no facet bands).
    A point given as (r, z, 'c') is a hard corner (the curve passes through it unsmoothed)."""
    pts = [Vector((p[0], p[1])) for p in prof]
    hard = [len(p) > 2 for p in prof]
    n = len(pts); out = []
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p1, p2 = pts[i], pts[(i + 1) % n]
        p0 = pts[i - 1] if (closed or i > 0) and not hard[i] else p1
        p3 = pts[(i + 2) % n] if (closed or i + 2 < n) and not hard[(i + 1) % n] else p2
        seg = max(1, int((p2 - p1).length / step))
        for s in range(seg):
            t = s / seg; t2 = t * t; t3 = t2 * t
            q = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            out.append((max(q.x, 0.0015), q.y))
    if not closed: out.append((pts[-1].x, pts[-1].y))
    return out

def lathe(name, prof, segs=32, loc=(0, 0, 0), rot=(0, 0, 0), close=False):
    """prof: [(r, z)] polyline, revolved about local Z. close joins last->first (solid walls)."""
    bm = bmesh.new()
    rings = []
    for (r, z) in prof:
        ring = [bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z)) for i in range(segs)]
        rings.append(ring)
    pairs = list(zip(rings[:-1], rings[1:])) + ([(rings[-1], rings[0])] if close else [])
    for a, b in pairs:
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _obj_from_bm(name, bm)
    ob.location = loc; ob.rotation_euler = [math.radians(a) for a in rot]
    bpy.context.view_layer.update()   # lib fix 1: matrix_world is stale right after setting .location
    return ob

def tube(name, pts, r, sides=12, cap=True, radii=None):
    """Tube along a 3D polyline (object coords), parallel-transport frames (no flips). radii: per-point radius."""
    bm = bmesh.new(); rings = []
    pts = [Vector(p) for p in pts]
    tans = [(pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized() for k in range(len(pts))]
    a = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    u = tans[0].cross(a).normalized()
    for k, p in enumerate(pts):
        t = tans[k]
        if k:
            u = (u - t * u.dot(t)).normalized()
        v = t.cross(u)
        rr = radii[k] if radii else r
        rings.append([bm.verts.new(p + (u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides)) * rr) for i in range(sides)])
    for a_, b_ in zip(rings[:-1], rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides; bm.faces.new((a_[i], a_[j], b_[j], b_[i]))
    if cap:
        bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _obj_from_bm(name, bm)

def bevel(ob, width, segs=3, angle=40, harden=True, profile=0.5):
    m = ob.modifiers.new('bevel', 'BEVEL')
    m.width = width; m.segments = segs; m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(angle); m.harden_normals = harden; m.profile = profile
    m.miter_outer = 'MITER_ARC'
    return m

def subsurf(ob, levels=1):
    m = ob.modifiers.new('sub', 'SUBSURF'); m.levels = levels; m.render_levels = levels
    return m

def apply_mods(ob):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects: o.select_set(False)
    ob.select_set(True)
    for m in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)

def smooth(ob):
    for p in ob.data.polygons: p.use_smooth = True

def warp(ob, amp, scale=0.6, seed=0.0, axes=(1, 1, 1)):
    """Low-frequency hand-made deformation in object space (metres)."""
    o = Vector((seed * 13.1, seed * 7.7, seed * 3.3))
    for v in ob.data.vertices:
        p = v.co * (1.0 / scale) + o
        d = Vector((noise.noise(p), noise.noise(p + Vector((5.2, 1.3, 2.8))), noise.noise(p + Vector((1.7, 9.2, 4.4)))))
        v.co += Vector((d.x * axes[0], d.y * axes[1], d.z * axes[2])) * amp

def sag(ob, axis=0, amount=0.005, up=2):
    """Bend a long part: centre drops by `amount` along -up axis."""
    vs = ob.data.vertices
    lo = min(v.co[axis] for v in vs); hi = max(v.co[axis] for v in vs)
    for v in vs:
        t = (v.co[axis] - lo) / max(hi - lo, 1e-6)
        v.co[up] -= amount * math.sin(math.pi * t)

def tag(ob, pid, kind):
    """Store part-local coords (lp) and a part id/kind before joining."""
    me = ob.data
    a = me.attributes.get('lp') or me.attributes.new('lp', 'FLOAT_VECTOR', 'POINT')
    for i, v in enumerate(me.vertices): a.data[i].vector = v.co
    b = me.attributes.get('pid') or me.attributes.new('pid', 'FLOAT', 'POINT')
    for i in range(len(me.vertices)): b.data[i].value = pid
    ob['kind'] = kind

def finish_part(ob, pid, kind, mat):
    """Apply modifiers, smooth, tag, assign material."""
    apply_mods(ob); smooth(ob); tag(ob, pid, kind)
    ob.data.materials.clear(); ob.data.materials.append(mat)
    return ob

def join(name, obs):
    for o in bpy.context.selected_objects: o.select_set(False)
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name; ob.data.name = name
    return ob

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def uv_unwrap(ob, margin=0.004, angle=60):
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')

# ----------------------------------------------------------------- shader DSL
class NT:
    """Tiny node-building DSL. Sockets are (node, key) where key is name or index."""
    def __init__(self, mat):
        self.m = mat; self.t = mat.node_tree; self.n = self.t.nodes; self.l = self.t.links
        self.x = -1400
        self.full = None          # lever 2 full scan (sc_aaa_scan.FULL), set by use_scan when AAA_FULL=1
        self.scan = None          # (asset, vector socket, tiles per metre, w_colour, w_rough, w_height); see use_scan
        self.grime = dict(GRIME) if GRIME else None   # global grime pass, see GRIME
        self._sc = {}
    def node(self, typ, **props):
        nd = self.n.new(typ)
        for k, v in props.items(): setattr(nd, k, v)
        nd.location = (self.x, 0); self.x += 40
        return nd
    def sock(self, s, out=True):
        if isinstance(s, tuple):
            nd, k = s
            return (nd.outputs if out else nd.inputs)[k]
        return s.outputs[0] if out else s.inputs[0]
    def link(self, a, b):
        self.l.new(self.sock(a), self.sock(b, out=False))
    def set(self, nd, key, v):
        """Value or socket into input key."""
        if isinstance(v, (tuple, bpy.types.Node)) and not (isinstance(v, tuple) and all(isinstance(c, (int, float)) for c in v)):
            self.link(v, (nd, key))
        else:
            sk = nd.inputs[key]
            if isinstance(v, np.ndarray): v = tuple(float(x) for x in v.ravel())   # lib fix 2: numpy into a socket segfaults
            if isinstance(v, tuple) and sk.type == 'RGBA' and len(v) == 3: v = (*v, 1.0)
            if not isinstance(v, tuple) and sk.type in ('RGBA', 'VECTOR'):
                v = (v, v, v, 1.0) if sk.type == 'RGBA' else (v, v, v)
            sk.default_value = v
    # attributes / inputs
    def attr(self, name, vec=True):
        nd = self.node('ShaderNodeAttribute', attribute_name=name)
        return (nd, 'Vector' if vec else 'Fac')
    def geo(self, out):
        return (self.node('ShaderNodeNewGeometry'), out)
    # math
    def math(self, op, a, b=0.0, c=0.0, clamp=False):
        nd = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.set(nd, 0, a); self.set(nd, 1, b)
        if len(nd.inputs) > 2: self.set(nd, 2, c)
        return (nd, 0)
    def vmath(self, op, a, b=(0, 0, 0), out=0):
        nd = self.node('ShaderNodeVectorMath', operation=op)
        self.set(nd, 0, a)
        if op not in ('LENGTH', 'NORMALIZE', 'ABSOLUTE', 'FLOOR', 'FRACTION', 'SINE', 'COSINE'):
            self.set(nd, 1, b)
        return (nd, 'Value' if op in ('DOT_PRODUCT', 'LENGTH', 'DISTANCE') else 'Vector')
    def sep(self, v):
        nd = self.node('ShaderNodeSeparateXYZ'); self.set(nd, 0, v); return nd
    def comb(self, x, y, z):
        nd = self.node('ShaderNodeCombineXYZ'); self.set(nd, 0, x); self.set(nd, 1, y); self.set(nd, 2, z); return (nd, 0)
    def maprange(self, v, a, b, c=0.0, d=1.0, clamp=True):
        nd = self.node('ShaderNodeMapRange', clamp=clamp)
        self.set(nd, 'Value', v); nd.inputs['From Min'].default_value = a; nd.inputs['From Max'].default_value = b
        nd.inputs['To Min'].default_value = c; nd.inputs['To Max'].default_value = d
        return (nd, 'Result')
    def smoothstep(self, v, a, b):
        nd = self.node('ShaderNodeMapRange', clamp=True, interpolation_type='SMOOTHSTEP')
        self.set(nd, 'Value', v); nd.inputs['From Min'].default_value = a; nd.inputs['From Max'].default_value = b
        return (nd, 'Result')
    # textures
    def noise(self, vec, scale=5.0, detail=4.0, rough=0.55, dist=0.0, dim='3D', out='Fac', lac=2.0):
        nd = self.node('ShaderNodeTexNoise', noise_dimensions=dim)
        self.set(nd, 'Vector', vec); nd.inputs['Scale'].default_value = scale
        nd.inputs['Detail'].default_value = detail; nd.inputs['Roughness'].default_value = rough
        nd.inputs['Distortion'].default_value = dist; nd.inputs['Lacunarity'].default_value = lac
        return (nd, out)
    def voronoi(self, vec, scale=5.0, feature='F1', metric='EUCLIDEAN', out='Distance', rand=1.0):
        nd = self.node('ShaderNodeTexVoronoi', feature=feature, distance=metric)
        self.set(nd, 'Vector', vec); nd.inputs['Scale'].default_value = scale
        nd.inputs['Randomness'].default_value = rand
        return (nd, out)
    def wave(self, vec, scale=2.0, dist=4.0, detail=3.0, wtype='RINGS', out='Fac'):
        nd = self.node('ShaderNodeTexWave', wave_type=wtype)
        self.set(nd, 'Vector', vec); nd.inputs['Scale'].default_value = scale
        nd.inputs['Distortion'].default_value = dist; nd.inputs['Detail'].default_value = detail
        return (nd, out)
    def white(self, vec, dim='1D', out='Value'):
        nd = self.node('ShaderNodeTexWhiteNoise', noise_dimensions=dim)
        self.set(nd, 'W' if dim == '1D' else 'Vector', vec)
        return (nd, out)
    def ramp(self, fac, stops):
        """stops: [(pos, (r,g,b))] colours in sRGB-ish linear given directly."""
        nd = self.node('ShaderNodeValToRGB')
        self.set(nd, 'Fac', fac)
        el = nd.color_ramp.elements
        while len(el) > 1: el.remove(el[-1])
        el[0].position = stops[0][0]; el[0].color = (*stops[0][1], 1)
        for p, c in stops[1:]:
            e = el.new(p); e.color = (*c, 1)
        return (nd, 'Color')
    def mix(self, fac, a, b, blend='MIX'):
        nd = self.node('ShaderNodeMix', data_type='RGBA', blend_type=blend, clamp_result=True)
        self.set(nd, 0, fac); self.set(nd, 6, a); self.set(nd, 7, b)
        return (nd, 2)
    def mixf(self, fac, a, b):
        nd = self.node('ShaderNodeMix', data_type='FLOAT', clamp_factor=True)
        self.set(nd, 0, fac); self.set(nd, 2, a); self.set(nd, 3, b)
        return (nd, 0)
    def hsv(self, col, h=0.5, s=1.0, v=1.0):
        nd = self.node('ShaderNodeHueSaturation')
        self.set(nd, 'Color', col); self.set(nd, 'Hue', h); self.set(nd, 'Saturation', s); self.set(nd, 'Value', v)
        return (nd, 'Color')
    def scan_map(self, kind):
        """Box-projected (UV-free) scanned map in part space: kind in diff, rough, disp."""
        asset, vec, tiles = self.scan[0], self.scan[1], self.scan[2]
        if kind in self._sc: return self._sc[kind]
        path = os.path.join(SCAN_DIR, f'{asset}_{kind}_2k.jpg')
        if kind == 'diff' and not os.path.exists(path): path = os.path.join(SCAN_DIR, f'{asset}_albedo_2k.jpg')
        img = bpy.data.images.load(os.path.abspath(path), check_existing=True)   # lib fix 4: relative image paths break
        img.colorspace_settings.name = 'sRGB' if kind == 'diff' else 'Non-Color'
        nd = self.node('ShaderNodeTexImage', projection='BOX', projection_blend=0.25); nd.image = img
        self.set(nd, 'Vector', self.vmath('MULTIPLY', vec, tiles))
        self._sc[kind] = (nd, 'Color'); return self._sc[kind]
    def bump(self, height, strength=1.0, distance=0.001, normal=None):
        if self.scan and self.scan[5]:
            d = self.sep(self.scan_map('disp'))
            height = self.math('ADD', height, self.math('MULTIPLY', self.math('SUBTRACT', (d, 'X'), 0.5), self.scan[5]))
        nd = self.node('ShaderNodeBump')
        self.set(nd, 'Height', height); nd.inputs['Strength'].default_value = 0.0 if os.environ.get('AAA_NOBUMP') else strength * (self.full.get('bump', 1.0) if self.full else 1.0)   # AAA_NOBUMP=1: form-only normal bake
        nd.inputs['Distance'].default_value = distance
        if normal is not None: self.set(nd, 'Normal', normal)
        return (nd, 'Normal')
    # masks
    def edge(self, radius=0.004, lo=0.02, hi=0.25, samples=8):
        """Convex edge mask: 1 - dot(bevel normal, true normal)."""
        bv = self.node('ShaderNodeBevel', samples=samples); bv.inputs['Radius'].default_value = radius
        d = self.vmath('DOT_PRODUCT', (bv, 'Normal'), self.geo('Normal'))
        e = self.math('SUBTRACT', 1.0, d)
        return self.smoothstep(e, lo, hi)
    def cavity(self, dist=0.03, lo=0.4, hi=1.0, samples=16):
        """1 = open, 0 = occluded crevice (local AO)."""
        ao = self.node('ShaderNodeAmbientOcclusion', samples=samples, only_local=True)
        ao.inputs['Distance'].default_value = dist
        return self.smoothstep((ao, 'AO'), lo, hi)
    def up(self, lo=0.5, hi=0.95):
        z = self.sep(self.geo('Normal'))
        return self.smoothstep((z, 'Z'), lo, hi)
    def _grime(self, color, rough, metal):
        g = self.grime; lp = self.attr('lp'); z = (self.sep(lp), 'Z'); nz = self.sep(self.geo('Normal'))
        n1 = self.noise(lp, scale=18, detail=6, rough=0.6); n2 = self.noise(lp, scale=70, detail=4)
        if g.get('contact'):
            cm = self.math('MULTIPLY', self.smoothstep(self.math('ADD', z, self.math('MULTIPLY', n1, 0.01)), 0.016, 0.002), g['contact'])
            color = self.mix(self.math('MULTIPLY', cm, 0.75), color, (0.028, 0.022, 0.016)); rough = self.mixf(cm, rough, 0.85)
            metal = self.mixf(cm, metal, 0.0)
        if g.get('dust'):
            fine = self.noise(lp, scale=400, detail=2)
            dm = self.math('MULTIPLY', self.math('MULTIPLY', self.smoothstep((nz, 'Z'), 0.55, 0.95), self.smoothstep(self.math('ADD', self.math('MULTIPLY', n2, 0.6), self.math('MULTIPLY', fine, 0.4)), 0.45, 0.62)), g['dust'])
            color = self.mix(self.math('MULTIPLY', dm, 0.45), color, (0.21, 0.195, 0.165)); rough = self.mixf(dm, rough, 0.92)
            metal = self.mixf(dm, metal, 0.0)
        if g.get('soot'):
            amt, d = g['soot']
            facing = self.smoothstep(self.vmath('DOT_PRODUCT', self.geo('Normal'), d), 0.0, 0.7)
            sm = self.math('MULTIPLY', self.math('MULTIPLY', facing, self.smoothstep(self.math('ADD', n1, self.math('MULTIPLY', z, 1.5)), 0.35, 0.75)), amt)
            color = self.mix(self.math('MULTIPLY', sm, 0.7), color, (0.014, 0.012, 0.011)); rough = self.mixf(self.math('MULTIPLY', sm, 0.6), rough, 0.9)
        if g.get('grease'):
            amt, lo, hi = g['grease']
            band = self.math('MULTIPLY', self.smoothstep(z, lo, lo + 0.01), self.smoothstep(z, hi, hi - 0.01))
            gm = self.math('MULTIPLY', self.math('MULTIPLY', band, self.smoothstep(n1, 0.45, 0.7)), amt)
            color = self.mix(self.math('MULTIPLY', gm, 0.35), color, self.hsv(color, 0.5, 1.1, 0.6)); rough = self.mixf(gm, rough, 0.3)
        return color, rough, metal
    def output(self, color, rough, metal=0.0, normal=None, **extra):
        if self.scan:
            if self.scan[3]:
                g = self.hsv(self.scan_map('diff'), 0.5, 0.0, 1.0)
                color = self.mix(self.scan[3], color, g, 'OVERLAY')
            if self.scan[4]:
                sr = self.sep(self.scan_map('rough'))
                rough = self.math('ADD', rough, self.math('MULTIPLY', self.math('SUBTRACT', (sr, 'X'), 0.5), self.scan[4]))
        if self.full and 'Alpha' not in extra:
            import sc_aaa_scan
            color, rough, normal = sc_aaa_scan.apply_output(self, color, rough, normal)
        if self.grime and 'Alpha' not in extra:
            color, rough, metal = self._grime(color, rough, metal)
        bs = self.node('ShaderNodeBsdfPrincipled')
        self.set(bs, 'Base Color', color)
        self.set(bs, 'Roughness', rough); self.set(bs, 'Metallic', metal)
        if normal is not None: self.set(bs, 'Normal', normal)
        for k, v in extra.items():
            self.set(bs, k.replace('_', ' '), v)
        out = self.node('ShaderNodeOutputMaterial')
        self.link((bs, 'BSDF'), (out, 'Surface'))
        self.bsdf = bs
        return bs

GRIME = None
"""Global grime pass for every opaque smart material built while it is set (props.py sets it per prop;
the bench leaves it None and uses its own decals). Keys (all optional, 0..1 amounts):
  contact: dirt and damp darkening in a band above the base (lp z < 12 mm) where the object meets the bench
  dust:    pale dust on up-facing surfaces, broken by noise, matte and non-metallic
  soot:    (amount, (x, y, z)) soot film on the side facing a fire, direction in prop-local space
  grease:  darker, glossier handling marks on convex areas between z_lo and z_hi: (amount, z_lo, z_hi)"""

def new_mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes.clear()
    return m, NT(m)

def use_scan(T, asset, vec, tiles=1.0, col=0.25, rough=0.3, height=0.8):
    """Layer a scanned CC0 material (Poly Haven etc.) into a smart material as detail: overlay on colour,
    offset on roughness, added bump height. No-op when AAA_SCANS is unset or the files are missing."""
    if SCAN_DIR and os.path.exists(os.path.join(SCAN_DIR, f'{asset}_disp_2k.jpg')):
        import sc_aaa_scan
        if os.environ.get('AAA_FULL') and asset in sc_aaa_scan.FULL and sc_aaa_scan.available(asset):
            T.full = dict(sc_aaa_scan.FULL[asset], asset=asset, p=vec); return
        T.scan = (asset, vec, tiles, col, rough, height)

# ----------------------------------------------------------------- baking
MAPS = ('BaseColor', 'Roughness', 'Metallic', 'Normal', 'AO')

def _bake_image(ob, name, res, non_color):
    img = bpy.data.images.get(name)
    if img: bpy.data.images.remove(img)
    img = bpy.data.images.new(name, res, res, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    for slot in ob.material_slots:
        nt = slot.material.node_tree
        for nd in [n for n in nt.nodes if n.name == '__bake']: nt.nodes.remove(nd)
        nd = nt.nodes.new('ShaderNodeTexImage'); nd.name = '__bake'; nd.image = img
        nt.nodes.active = nd
    return img

def _route_emission(ob, src_key, on=True):
    """Temporarily feed a Principled input (e.g. Metallic) into Emission for an EMIT bake."""
    for slot in ob.material_slots:
        nt = slot.material.node_tree
        bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
        if on:
            em = nt.nodes.new('ShaderNodeEmission'); em.name = '__em'
            sk = bs.inputs[src_key]
            if sk.is_linked:
                nt.links.new(sk.links[0].from_socket, em.inputs['Color'])
            else:
                v = sk.default_value; v = v if hasattr(v, '__len__') else (v, v, v, 1)
                em.inputs['Color'].default_value = v if len(v) == 4 else (*v, 1)
            nt.links.new(em.outputs[0], out.inputs['Surface'])
        else:
            for n in [n for n in nt.nodes if n.name == '__em']: nt.nodes.remove(n)
            nt.links.new(bs.outputs['BSDF'], out.inputs['Surface'])

def bake_set(ob, outdir, prefix, res=2048, samples=64, margin=16, ao_dist=0.15):
    """Bake the PBR set of a joined, unwrapped object into PNGs. Returns {map: path}."""
    sc = bpy.context.scene; gpu(sc)
    sc.cycles.samples = samples; sc.cycles.use_denoising = False
    bk = sc.render.bake; bk.margin = margin; bk.use_selected_to_active = False; bk.use_clear = True
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    os.makedirs(outdir, exist_ok=True); paths = {}
    def save(img, key):
        p = os.path.join(outdir, f'{prefix}_{key}.png')
        img.filepath_raw = p; img.file_format = 'PNG'; img.save(); paths[key] = p
    img = _bake_image(ob, f'{prefix}_BaseColor', res, False)
    _route_emission(ob, 'Base Color', True); bpy.ops.object.bake(type='EMIT'); _route_emission(ob, 'Base Color', False)   # not DIFFUSE: that bakes base * (1 - metallic)
    save(img, 'BaseColor')
    img = _bake_image(ob, f'{prefix}_Roughness', res, True)
    bpy.ops.object.bake(type='ROUGHNESS'); save(img, 'Roughness')
    img = _bake_image(ob, f'{prefix}_Normal', res, True)
    bk.normal_space = 'TANGENT'
    bpy.ops.object.bake(type='NORMAL'); save(img, 'Normal')
    img = _bake_image(ob, f'{prefix}_Metallic', res, True)
    _route_emission(ob, 'Metallic', True); bpy.ops.object.bake(type='EMIT'); _route_emission(ob, 'Metallic', False)
    save(img, 'Metallic')
    img = _bake_image(ob, f'{prefix}_AO', res, True)
    sc.world = sc.world or bpy.data.worlds.new('W')
    sc.world.light_settings.distance = ao_dist
    sc.cycles.samples = max(samples, 128)
    bpy.ops.object.bake(type='AO'); save(img, 'AO')
    for slot in ob.material_slots:
        nt = slot.material.node_tree
        for nd in [n for n in nt.nodes if n.name == '__bake']: nt.nodes.remove(nd)
    return paths

def game_material(name, paths, ao_strength=1.0):
    """Principled material driven ONLY by baked maps: the in-engine stand-in."""
    m, T = new_mat(name)
    T.grime = None      # grime is baked into the maps already; here it would read a missing lp (z=0) and coat everything
    def tex(key, nc):
        nd = T.node('ShaderNodeTexImage'); nd.image = bpy.data.images.load(os.path.abspath(paths[key]), check_existing=True)
        nd.image.colorspace_settings.name = 'Non-Color' if nc else 'sRGB'
        return nd
    bc = tex('BaseColor', False); ao = tex('AO', True)
    col = T.mix(ao_strength, (bc, 'Color'), (ao, 'Color'), 'MULTIPLY')
    nm = T.node('ShaderNodeNormalMap'); T.link((tex('Normal', True), 'Color'), (nm, 'Color'))
    extra = {}
    if 'Emission' in paths:
        extra = dict(Emission_Color=(tex('Emission', False), 'Color'), Emission_Strength=6.0)
    T.output(col, (tex('Roughness', True), 'Color'), (tex('Metallic', True), 'Color'), (nm, 'Normal'), **extra)
    return m

def apply_game_material(ob, mat):
    ob.data.materials.clear(); ob.data.materials.append(mat)

# ----------------------------------------------------------------- high poly (sculpt-like damage, numpy)
import numpy as np

def _hash(i, j, k, s):
    return np.modf(np.sin(i * 127.1 + j * 311.7 + k * 74.7 + s * 19.19) * 43758.5453)[0] % 1.0

def vnoise(P, seed=0.0):
    """Vectorised 3D value noise in [0,1], P: (n,3)."""
    Pi = np.floor(P); f = P - Pi; u = f * f * (3 - 2 * f)
    i, j, k = Pi[:, 0], Pi[:, 1], Pi[:, 2]
    def c(a, b, cc): return _hash(i + a, j + b, k + cc, seed)
    x00 = c(0,0,0) * (1-u[:,0]) + c(1,0,0) * u[:,0]; x10 = c(0,1,0) * (1-u[:,0]) + c(1,1,0) * u[:,0]
    x01 = c(0,0,1) * (1-u[:,0]) + c(1,0,1) * u[:,0]; x11 = c(0,1,1) * (1-u[:,0]) + c(1,1,1) * u[:,0]
    y0 = x00 * (1-u[:,1]) + x10 * u[:,1]; y1 = x01 * (1-u[:,1]) + x11 * u[:,1]
    return y0 * (1-u[:,2]) + y1 * u[:,2]

def fbm(P, scale=1.0, octaves=4, gain=0.5, seed=0.0):
    a, s, tot, norm = 1.0, scale, 0.0, 0.0
    for o in range(octaves):
        tot = tot + a * vnoise(P * s, seed + o * 7.3); norm += a; a *= gain; s *= 2.0
    return tot / norm

def _sstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def mesh_arrays(ob):
    me = ob.data; n = len(me.vertices)
    co = np.empty(n * 3); me.vertices.foreach_get('co', co); co = co.reshape(n, 3)
    no = np.empty(n * 3); me.vertices.foreach_get('normal', no); no = no.reshape(n, 3)
    return co, no

def set_co(ob, co):
    ob.data.vertices.foreach_set('co', co.reshape(-1)); ob.data.update()

def remesh(ob, voxel):
    m = ob.modifiers.new('remesh', 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = voxel; m.adaptivity = 0.0
    apply_mods(ob)

def erode_box(ob, half, wear=0.004, width=0.014, chips=1.0, dents=1.0, seed=0.0, grain_x=True, adze=0.0008):
    """Hand-worn edges on a (warped) box part whose rest half-sizes are `half` (local).
    Smooth low-frequency rounding of edges + crisp-walled chips (flat floor, sharp rim,
    elongated along the grain) + a few crisp dents. Verts move inward along normals."""
    co, no = mesh_arrays(ob)
    h = np.array(half)
    dfa = np.sort(h[None, :] - np.abs(co), axis=1)
    edist = dfa[:, 1]
    st = np.array([0.3, 1, 1]) if grain_x else np.array([1, 1, 1])
    # smooth rounding, varying slowly along the edge
    var = fbm(co * st, 6.0, 3, 0.5, seed)
    reach = width * (0.6 + 0.8 * var)
    d = wear * (0.4 + 0.9 * var) * np.clip(1.0 - _sstep(edist, 0.0, reach), 0.0, 1.0) ** 1.5   # lib fix 3: clamp the base of a fractional power (NaN when t > 1)
    # chips: crisp threshold of grain-stretched noise, only within a band of the edge
    cn = fbm(co * np.array([0.35, 1, 1]), 22.0, 3, 0.5, seed + 3.1)
    cm = _sstep(cn, 0.64, 0.655) * (1.0 - _sstep(edist, width * 0.8, width * 0.95))
    cm *= chips
    d = np.maximum(d, cm * wear * (1.2 + 0.6 * fbm(co, 90.0, 2, 0.5, seed + 4.4)))
    # dents: crisp shallow pits on faces
    dn = fbm(co, 45.0, 2, 0.5, seed + 5.5)
    d += dents * 0.0008 * _sstep(dn, 0.74, 0.75)
    d += 0.0004 * fbm(co * st, 5.0, 2, 0.5, seed + 1.7)
    # adze / drawknife facets across the long faces: shallow scallops along X
    if adze:
        ph = co[:, 0] / (0.07 + 0.02 * fbm(co, 3.0, 1, 0.5, seed + 8.8)) + 3.0 * fbm(co, 2.0, 2, 0.5, seed + 6.1)
        sc = (np.abs(np.sin(np.pi * ph))) ** 0.6
        d += adze * sc * _sstep(edist, 0.006, 0.02)
    set_co(ob, co - no * d[:, None])

def erode_generic(ob, amp=0.0008, scale=30.0, seed=0.0):
    co, no = mesh_arrays(ob)
    d = amp * (fbm(co, scale, 4, 0.55, seed) - 0.5) * 2
    set_co(ob, co - no * d[:, None])

def store_lp(ob, pid):
    co, _ = mesh_arrays(ob); me = ob.data
    a = me.attributes.get('lp') or me.attributes.new('lp', 'FLOAT_VECTOR', 'POINT')
    a.data.foreach_set('vector', co.reshape(-1))
    b = me.attributes.get('pid') or me.attributes.new('pid', 'FLOAT', 'POINT')
    b.data.foreach_set('value', np.full(len(me.vertices), pid))
    _, no = mesh_arrays(ob)
    c = me.attributes.get('ln') or me.attributes.new('ln', 'FLOAT_VECTOR', 'POINT')
    c.data.foreach_set('vector', no.reshape(-1))

def decimate(ob, ratio):
    m = ob.modifiers.new('dec', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = ratio
    m.use_collapse_triangulate = True
    apply_mods(ob)

def dup(ob, name):
    o2 = ob.copy(); o2.data = ob.data.copy(); o2.name = name; o2.data.name = name
    bpy.context.scene.collection.objects.link(o2); return o2

def bake_to_low(low, highs, outdir, prefix, res=2048, samples=64, margin=16, extrusion=0.003, ray=0.008, ao_dist=0.15):
    """Selected-to-active bake: procedural highs -> low's unique UVs. Low gets a bake target material."""
    sc = bpy.context.scene; gpu(sc)
    sc.cycles.samples = samples; sc.cycles.use_denoising = False
    bk = sc.render.bake; bk.margin = margin; bk.use_selected_to_active = True; bk.use_clear = True
    bk.cage_extrusion = extrusion; bk.max_ray_distance = ray
    tm, _ = new_mat(prefix + '_bake'); low.data.materials.clear(); low.data.materials.append(tm)
    for o in bpy.context.selected_objects: o.select_set(False)
    for h in highs: h.select_set(True)
    low.select_set(True); bpy.context.view_layer.objects.active = low
    os.makedirs(outdir, exist_ok=True); paths = {}
    def target(key, nc):
        img = bpy.data.images.get(f'{prefix}_{key}')
        if img: bpy.data.images.remove(img)
        img = bpy.data.images.new(f'{prefix}_{key}', res, res, alpha=False)
        img.colorspace_settings.name = 'Non-Color' if nc else 'sRGB'
        nt = tm.node_tree
        for nd in [n for n in nt.nodes if n.name == '__bake']: nt.nodes.remove(nd)
        nd = nt.nodes.new('ShaderNodeTexImage'); nd.name = '__bake'; nd.image = img; nt.nodes.active = nd
        return img
    def save(img, key):
        p = os.path.join(outdir, f'{prefix}_{key}.png'); img.filepath_raw = p; img.file_format = 'PNG'; img.save(); paths[key] = p
    # BaseColor through the emission route: a DIFFUSE/COLOR bake returns base * (1 - metallic), so metal
    # albedo baked near black and copper rendered as matte clay (s19-s21)
    img = target('BaseColor', False)
    for h in highs: _route_emission(h, 'Base Color', True)
    bpy.ops.object.bake(type='EMIT')
    for h in highs: _route_emission(h, 'Base Color', False)
    save(img, 'BaseColor')
    img = target('Roughness', True); bpy.ops.object.bake(type='ROUGHNESS'); save(img, 'Roughness')
    img = target('Normal', True); bk.normal_space = 'TANGENT'; bpy.ops.object.bake(type='NORMAL'); save(img, 'Normal')
    img = target('Emission', False); bpy.ops.object.bake(type='EMIT')
    px = np.empty(res * res * 4, dtype=np.float32); img.pixels.foreach_get(px)
    if px.reshape(-1, 4)[:, :3].max() > 0.02: save(img, 'Emission')
    img = target('Metallic', True)
    for h in highs: _route_emission(h, 'Metallic', True)
    bpy.ops.object.bake(type='EMIT')
    for h in highs: _route_emission(h, 'Metallic', False)
    save(img, 'Metallic')
    img = target('AO', True)
    sc.world = sc.world or bpy.data.worlds.new('W'); sc.world.light_settings.distance = ao_dist
    sc.cycles.samples = max(samples, 128); bpy.ops.object.bake(type='AO'); save(img, 'AO')
    return paths


LOWSRC = {}

def make_part(ob, mat, pid, vox=0.002, erode=None, bev=None, segs=3, fillet=0, low='decimate', low_sub=0, cuts=None, post=None):
    """Closed part(s) -> high. ob may be a list: they are voxel-unioned into one solid, and
    fillet>0 runs that many smooth iterations after the remesh (dynamesh + polish: real fillets
    where parts meet, softened edges). Then erosion callback(ob), attrs, material."""
    if isinstance(ob, (list, tuple)):
        for o in ob:
            if bev: bevel(o, bev, segs, harden=False)
            apply_mods(o)
        ob = join(ob[0].name, list(ob)) if len(ob) > 1 else ob[0]
    else:
        if bev: bevel(ob, bev, segs, harden=False)
        apply_mods(ob)
    if low == 'source':
        src = dup(ob, ob.name + '_lowsrc')
        if low_sub: subsurf(src, low_sub); apply_mods(src)
        smooth(src); LOWSRC[ob.name] = src
    if vox: remesh(ob, vox)
    if fillet:
        # fillet only where parts meet: smooth a vertex group of concave verts (dilated), so rims stay crisp
        cv = curvature(ob)
        me = ob.data; e = np.empty(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get('vertices', e); e = e.reshape(-1, 2)
        w = (cv < -25.0).astype(float)
        for _ in range(3):
            s = w.copy(); np.maximum.at(s, e[:, 0], w[e[:, 1]]); np.maximum.at(s, e[:, 1], w[e[:, 0]]); w = s
        vg = ob.vertex_groups.new(name='fillet')
        idx = np.nonzero(w > 0)[0].tolist()
        if idx: vg.add(idx, 1.0, 'REPLACE')
        m = ob.modifiers.new('fillet', 'SMOOTH'); m.factor = 0.5; m.iterations = fillet * 2; m.vertex_group = 'fillet'
        apply_mods(ob)
    # authored form (sc_aaa_cuts): cuts(ob) subtracts bounded cutters after the fillet, then a second remesh
    # heals the topology without re-filleting the new arrises; post(ob) cuts hairlines into the final high
    cutters = []
    if cuts:
        c = cuts(ob); cutters += [c] if c else []
        if vox: remesh(ob, vox)
    if erode: erode(ob)
    if post:
        c = post(ob)                                    # cracks stay dark (cavity), so not marked fresh
        if c: bpy.data.objects.remove(c)
    if cutters:
        import sc_aaa_cuts as C
        C.store_fr(ob, np.max([C.fresh_mask(ob, c) for c in cutters], axis=0))
        for c in cutters: bpy.data.objects.remove(c)
    smooth(ob); store_lp(ob, pid)
    ob.data.materials.clear(); ob.data.materials.append(mat)
    return ob

def decimate_to(ob, target):
    n = tris(ob)
    if n > target: decimate(ob, target / n)

def bake_prop(name, highs, outdir, target=8000, res=2048, samples=32, extrusion=0.003, ray=0.008, keep=()):
    """Highs -> low (per part: its clean source mesh if make_part(low='source'), else a decimated copy
    sharing `target` by triangle count) -> bake PBR -> game material. Returns the low."""
    srcs = [LOWSRC.pop(h.name) for h in highs if h.name in LOWSRC]
    dec = [h for h in highs if not any(s.name == h.name + '_lowsrc' for s in srcs)]
    budget = max(target - sum(tris(s) for s in srcs), 500)
    tot = sum(tris(h) for h in dec) or 1
    lows = list(srcs)
    for h in dec:
        l = dup(h, h.name + '_low'); decimate_to(l, max(int(budget * tris(h) / tot), 60)); lows.append(l)
    low = join(name, lows) if len(lows) > 1 else lows[0]
    low.name = name; smooth(low)
    for o in list(bpy.context.scene.objects): o.select_set(False)
    high = join(name + '_High', highs) if len(highs) > 1 else highs[0]
    print('PROP', name, 'high', tris(high), 'low', tris(low), 'src parts', len(srcs))
    uv_unwrap(low, margin=0.003, angle=66)
    paths = bake_to_low(low, [high], os.path.join(outdir, 'tex'), name, res=res, samples=samples, extrusion=extrusion, ray=ray)
    apply_game_material(low, game_material('M_' + name, paths))
    bpy.data.objects.remove(high)
    return low

def save_only(path, objs):
    """Save a .blend containing only objs (and what they use)."""
    keep = set(objs)
    for o in list(bpy.context.scene.objects):
        if o not in keep: bpy.data.objects.remove(o)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(path))


def curvature(ob, smooth_iters=3):
    """Per-vertex mean-curvature-like value (1/m): + convex (rims, edges), - concave. Dense meshes only."""
    me = ob.data; n = len(me.vertices)
    co, no = mesh_arrays(ob)
    e = np.empty(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get('vertices', e); e = e.reshape(-1, 2)
    i, j = e[:, 0], e[:, 1]
    d = co[j] - co[i]; l2 = (d * d).sum(1) + 1e-12
    ci = -(no[i] * d).sum(1) / l2; cj = (no[j] * d).sum(1) / l2
    acc = np.zeros(n); cnt = np.zeros(n)
    np.add.at(acc, i, ci); np.add.at(acc, j, cj); np.add.at(cnt, i, 1); np.add.at(cnt, j, 1)
    c = acc / np.maximum(cnt, 1)
    for _ in range(smooth_iters):
        s = np.zeros(n); np.add.at(s, i, c[j]); np.add.at(s, j, c[i])
        c = 0.5 * c + 0.5 * s / np.maximum(cnt, 1)
    return c * 2.0

def scoops(ob, count=20, rmin=0.002, rmax=0.006, depth=0.6, convex=(60.0, 160.0), seed=0.0, co=None, no=None, flake=False):
    """Conchoidal chip bites: `count` dome-shaped bites (radius rmin..rmax, depth = depth*radius)
    centred on convex vertices (rims, edges). Returns the per-vertex inward displacement."""
    from mathutils import kdtree
    if co is None: co, no = mesh_arrays(ob)
    cv = curvature(ob)
    cand = np.nonzero(cv > convex[0])[0]
    d = np.zeros(len(co))
    if len(cand) == 0 or count <= 0: return d
    rs = np.random.default_rng(int(seed * 1000) & 0xffffffff)
    kd = kdtree.KDTree(len(co))
    for i, c in enumerate(co): kd.insert(c, i)
    kd.balance()
    w = np.clip((cv[cand] - convex[0]) / (convex[1] - convex[0]), 0.05, 1.0)
    picks = rs.choice(cand, size=min(count, len(cand)), replace=False, p=w / w.sum())
    for pi in picks:
        R = rs.uniform(rmin, rmax)
        hits = kd.find_range(co[pi], R)
        if not hits: continue
        idx = np.array([h[1] for h in hits]); dist = np.array([h[2] for h in hits])
        if flake:
            # conchoidal flake: a planar slice tilted off the surface (flat scar, crisp edge), faded at R
            t = rs.normal(size=3); t -= no[pi] * t.dot(no[pi]); t /= (np.linalg.norm(t) + 1e-9)
            n2 = no[pi] + t * rs.uniform(0.5, 1.2); n2 /= np.linalg.norm(n2)
            p0 = co[pi] - no[pi] * depth * R
            bite = np.maximum(0.0, ((co[idx] - p0) * n2).sum(1)) * np.clip(1.4 - dist / R, 0, 1)
            bite = np.minimum(bite, depth * R * 1.6)
        else:
            c = co[pi] - no[pi] * R * rs.uniform(0.15, 0.45)
            dd = np.linalg.norm(co[idx] - c, axis=1)
            bite = depth * R * np.sqrt(np.clip(1 - (dd / R) ** 2, 0, 1))
        d[idx] = np.maximum(d[idx], bite)
    return d

def wear_generic(amp=0.0006, scale=40.0, chips=0, chip_r=(0.002, 0.006), chip_depth=0.6, convex=(60.0, 160.0), seed=0.0, flake=True):
    """Erosion callback for make_part: fbm surface noise + `chips` conchoidal bites on convex rims/edges."""
    def f(ob):
        co, no = mesh_arrays(ob)
        d = amp * (fbm(co, scale, 4, 0.55, seed) - 0.5) * 2
        if chips:
            d = d + scoops(ob, int(chips), chip_r[0], chip_r[1], chip_depth, convex, seed, co, no, flake)
        set_co(ob, co - no * d[:, None])
    return f


def chain(*fs):
    """Combine erosion callbacks for make_part."""
    def f(ob):
        for g in fs:
            if g: g(ob)
    return f

def dimples(count=400, rmin=0.004, rmax=0.008, depth=0.12, seed=0.0):
    """Hammered-metal dimples anywhere on the surface (shallow dome bites)."""
    def f(ob):
        co, no = mesh_arrays(ob)
        d = scoops(ob, count, rmin, rmax, depth, (-1e9, -1e9 + 1), seed, co, no)
        set_co(ob, co - no * d[:, None])
    return f

def tooled(amp=0.0006, freq=180.0, patch=8.0, seed=0.0):
    """Chisel / rasp striations in patches of random direction (dressed stone, carved wood)."""
    def f(ob):
        co, no = mesh_arrays(ob)
        rs = np.random.default_rng(int(seed * 977) & 0xffffffff)
        dirs = rs.normal(size=(6, 3)); dirs /= np.linalg.norm(dirs, axis=1)[:, None]
        sel = np.floor(fbm(co, patch, 1, 0.5, seed) * 6).astype(int).clip(0, 5)
        fq = freq * (0.75 + 0.5 * fbm(co, patch * 2, 1, 0.5, seed + 3))
        ph = (co * dirs[sel]).sum(1) * fq + 6.0 * fbm(co, 30.0, 3, 0.5, seed + 1)
        strength = _sstep(fbm(co, patch * 1.5, 2, 0.5, seed + 2), 0.35, 0.7)       # tooling only in patches
        d = amp * strength * (np.abs(np.sin(ph)) ** 0.5)
        set_co(ob, co - no * d[:, None])
    return f

def facets(count=300, rmin=0.006, rmax=0.014, depth=0.0004, tilt=0.15, seed=0.0):
    """Chisel-dressed stone: `count` shallow planar cuts (absolute depth in metres) nearly parallel to the
    local surface, so bumps become flat facets with natural crisp borders where the plane leaves the
    surface; faded over the outer 30% of the radius so no step or outline groove is left. A steep tilt or
    a depth relative to the radius engraves outline grooves (s17)."""
    from mathutils import kdtree
    def f(ob):
        co, no = mesh_arrays(ob)
        rs = np.random.default_rng(int(seed * 1000) & 0xffffffff)
        kd = kdtree.KDTree(len(co))
        for i, c in enumerate(co): kd.insert(c, i)
        kd.balance()
        d = np.zeros(len(co))
        for pi in rs.choice(len(co), size=min(count, len(co)), replace=False):
            R = rs.uniform(rmin, rmax)
            hits = kd.find_range(co[pi], R)
            if not hits: continue
            idx = np.array([h[1] for h in hits]); dist = np.array([h[2] for h in hits])
            t = rs.normal(size=3); t -= no[pi] * t.dot(no[pi]); t /= (np.linalg.norm(t) + 1e-9)
            n2 = no[pi] + t * rs.uniform(0.0, tilt); n2 /= np.linalg.norm(n2)
            p0 = co[pi] - no[pi] * depth * rs.uniform(0.5, 1.0)
            bite = np.maximum(0.0, ((co[idx] - p0) * n2).sum(1)) * np.clip((1.0 - dist / R) / 0.3, 0, 1)
            d[idx] = np.maximum(d[idx], np.minimum(bite, depth * 2.5))
        set_co(ob, co - no * d[:, None])
    return f

def seam_lap(angle, width=0.05, height=0.0007, zr=(-1e9, 1e9), rmin=0.05):
    """Lapped sheet-metal seam on a lathed part: the strip from `angle` to angle+width (radians) is pushed
    out by `height`, ending in a crisp lap edge on one side and a soft taper on the other."""
    def f(ob):
        co, no = mesh_arrays(ob)
        ang = np.arctan2(co[:, 1], co[:, 0]); da = (ang - angle + np.pi) % (2 * np.pi) - np.pi
        rad = np.linalg.norm(co[:, :2], axis=1)
        m = _sstep(da, -0.01, 0.0) * _sstep(-da, -width - 0.002, -width) * (rad > rmin) * (co[:, 2] > zr[0]) * (co[:, 2] < zr[1])
        set_co(ob, co + no * (height * m)[:, None])
    return f
