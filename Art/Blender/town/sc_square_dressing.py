"""Hollowmere square dressing props (town square, 40 x 28 m): benches, planter boxes, clay pots, lantern posts, bunting parts and
a drain grate, each in a whole and a broken "Ruined" variant. One reproducible headless generator in the locked style
(docs/PHASE1.md 3: chunky storybook forms about 10% oversize, real PBR, tidy placed wear, warm oak, warm dark iron #4a4542).

It reuses the B7 pipeline (Art/Blender/town/sc_props_adopt.py) unchanged: that file is exec'd here with its final `main()` call
removed, so import_source, reshape, graded_albedo, NT, finish, wear, make_low, bake, export, review_scene, aim, render and
setup_cycles are the very same functions that made the approved B7 props, and the CLI is the same shape.

Assets (folder name under Assets/_Project/Content/Art/Town/Square/; method; texture size):
  Bench_A          adopt painted_wooden_bench: 1.15x wide, z squashed to a 0.49 m seat, inflated chunky, sage paint faded, chipped to oak   2048
  Bench_Broken     same: front plank snapped and fallen at the right end, left-front foot gone (corner sags), paint peeled, grime          2048
  Planter_A        adopt planter_box_01: warm oak box, dark soil top (generated, 85% of height)                                          1024
  Planter_B        adopt planter_box_02: same, longer and lower                                                                            1024
  Planter_Broken   planter_box_01: a front board snapped out (the chunk lies beside it), dry soil spilled in a low mound                    1024
  Pot_Clay         adopt planter_pot_clay: terracotta, mossy foot, soil disc                                                              1024
  Pot_Broken       the same pot with a wedge broken out, three shards beside it, spilled soil                                             1024
  LanternPost_A    generated: oak post on a sandstone footing, wrought-iron bracket arm ending in a hook (hook point in SOURCES.md)        2048
  LanternPost_Broken  generated: post snapped 1.2 m up, the top section lying beside it with its arm bent                                  2048
  Bunting          generated parts in ONE FBX/texture set: Pennant_0..3 (four dyes), Pennant_T0/T1 (torn), Rope_1m                       1024
  Drain_A          generated: sandstone gully frame, cast-iron grate, dark void; origin at the TOP centre (flush with the cobbles)       1024

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_square_dressing.py -- --out <scratch>
      [--only Bench_A,Drain_A] [--res-scale 0.5] [--export] [--no-render] [--samples 16] [--cpu] [--rres 900]
      [--render-only]   skip building: load <out>/<Asset>/<Asset>.fbx + maps and render the review set
  One process per asset (a crash costs one asset), then one render pass:
      for a in Bench_A Bench_Broken ...; do blender ... -- --out <scratch> --only $a --no-render [--export]; done
      blender ... -- --out <scratch> --render-only
Outputs in <scratch>: <Asset>/ (maps, fbx), renders/<Asset>_tq.png. Sheet: sc_sheet.py on the renders.

Axes (as B7): authored Blender z-up, origin at the bottom centre on the ground, front = -Y; FBX -Z forward / Y up gives
Unity (x, y, z) = Blender (-x, z, -y) (sc_plaza_common.u2b), so Blender -Y = Unity +Z and Blender -X = Unity +X.
"""
import sys, os

_HERE = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(_HERE, 'sc_props_adopt.py'), encoding='utf-8') as _fh:
    _src = _fh.read().rstrip()
assert _src.endswith('\nmain()'), 'sc_props_adopt.py no longer ends with main()'
exec(compile(_src[:-len('main()')], os.path.join(_HERE, 'sc_props_adopt.py'), 'exec'), globals())   # B7 pipeline, parsed with our own --out etc.

DEST = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Art', 'Town', 'Square')
SEED = 8108
ORIGINS = {}      # per asset: lines that go into its SOURCES.md (origin, hook, soil heights ...)

# ---------------------------------------------------------------- generic helpers
def new_mat(key):
    mat = bpy.data.materials.new(key); mat.use_nodes = True; mat.node_tree.nodes.clear(); return mat

def to_obj(bm, name, mats=(), smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    for m in mats: me.materials.append(m)
    if smooth:
        for f in me.polygons: f.use_smooth = True
    return o

def pos_key(co):
    return (round(co.x * 1e5), round(co.y * 1e5), round(co.z * 1e5))

def avg_normals(me):
    """Per-vertex normal averaged over every vertex at the same position (split hard-edge vertices move together)."""
    me.update(); acc = {}
    for v in me.vertices: acc.setdefault(pos_key(v.co), Vector()); acc[pos_key(v.co)] += v.normal
    return [acc[pos_key(v.co)].normalized() for v in me.vertices]

def displace(o, fn):
    """co += avg_normal * fn(co)."""
    me = o.data; nr = avg_normals(me)
    for v, n in zip(me.vertices, nr): v.co = v.co + n * fn(v.co)
    me.update()

def inflate(o, d):
    displace(o, lambda co: d)

def weld(o, dist=1e-5):
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist); bm.to_mesh(o.data); bm.free()

def tris_of(o):
    return sum(len(f.vertices) - 2 for f in o.data.polygons)

def nz(co, scale, off=0.0, detail=3.0):
    return noise.fractal(Vector((co.x, co.y, co.z)) * scale + Vector((off, off * 0.7, off * 1.3)), 0.55, 2.0, int(detail))

def add_prism(bm, prof, zs, fn=None, caps=(True, True), mat=0):
    """Extrude a CCW 2D profile through heights zs (rings); fn(x, y, z, k, j) -> (x, y, z) shapes each ring vertex."""
    rings = []
    for k, z in enumerate(zs):
        ring = []
        for j, (x, y) in enumerate(prof):
            p = fn(x, y, z, k, j) if fn else (x, y, z)
            ring.append(bm.verts.new(p))
        rings.append(ring)
    n = len(prof); faces = []
    for k in range(len(rings) - 1):
        for j in range(n):
            faces.append(bm.faces.new((rings[k][j], rings[k][(j + 1) % n], rings[k + 1][(j + 1) % n], rings[k + 1][j])))
    if caps[0]: faces.append(bm.faces.new(list(reversed(rings[0]))))
    if caps[1]: faces.append(bm.faces.new(rings[-1]))
    for f in faces: f.material_index = mat
    return rings, faces

def chamfer_square(w, d, c):
    hx, hy = w / 2, d / 2
    return [(hx - c, -hy), (hx, -hy + c), (hx, hy - c), (hx - c, hy), (-hx + c, hy), (-hx, hy - c), (-hx, -hy + c), (-hx + c, -hy)]

def sweep(bm, pts, prof, mat=0, scale_fn=None, caps=True):
    """Sweep a small closed 2D profile (CCW) along a 3D polyline with parallel-transported frames."""
    pts = [Vector(p) for p in pts]; n = len(pts); rings = []
    t0 = (pts[1] - pts[0]).normalized()
    ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
    u = t0.cross(ref).normalized(); w = t0.cross(u).normalized()
    for k in range(n):
        if k == 0: t = t0
        else:
            t = ((pts[k] - pts[k - 1]) if k == n - 1 else (pts[k + 1] - pts[k - 1])).normalized()
            u = (u - t * u.dot(t)); u = u.normalized() if u.length > 1e-6 else t.cross(ref).normalized()
            w = t.cross(u).normalized()
        s = scale_fn(k / max(n - 1, 1)) if scale_fn else 1.0
        rings.append([bm.verts.new(pts[k] + u * (x * s) + w * (y * s)) for (x, y) in prof])
    m = len(prof); faces = []
    for k in range(n - 1):
        for j in range(m):
            faces.append(bm.faces.new((rings[k][j], rings[k + 1][j], rings[k + 1][(j + 1) % m], rings[k][(j + 1) % m])))
    if caps:
        faces.append(bm.faces.new(rings[0])); faces.append(bm.faces.new(list(reversed(rings[-1]))))
    for f in faces: f.material_index = mat
    # make winding outward whatever the frame handedness
    bm.normal_update(); cen = sum((v.co for r in rings for v in r), Vector()) / (n * m)
    return rings, faces

def fix_normals(bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

def apply_boolean(obj, cutter, op):
    """Exact boolean with the cutter's materials carried onto the cut faces (the broken wood / clay core)."""
    mod = obj.modifiers.new('b', 'BOOLEAN'); mod.operation = op; mod.object = cutter; mod.solver = 'EXACT'; mod.use_self = True
    try: mod.material_mode = 'TRANSFER'
    except Exception: pass
    dg = bpy.context.evaluated_depsgraph_get(); me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    obj.modifiers.clear(); old = obj.data; obj.data = me
    if old.users == 0: bpy.data.meshes.remove(old)
    return obj

def dup(o, name):
    d = o.copy(); d.data = o.data.copy(); d.name = name; bpy.context.scene.collection.objects.link(d); return d

def rough_box(name, c, size, r, jit, mat, rot=None, cuts=3):
    """A jittered subdivided box: its faces become the irregular fracture surfaces of a boolean cut."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        v.co += Vector((r.uniform(-jit, jit), r.uniform(-jit, jit), r.uniform(-jit, jit)))
    if rot is not None:
        for v in bm.verts: v.co = rot @ v.co
    for v in bm.verts: v.co += Vector(c)
    me = bpy.data.meshes.new(name); bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); me.materials.append(mat)
    return o

def loose_parts(o):
    """Split an object into its loose parts (largest first)."""
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    before = set(bpy.data.objects)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.separate(type='LOOSE'); bpy.ops.object.mode_set(mode='OBJECT')
    parts = [x for x in bpy.data.objects if x not in before] + [o]
    return sorted(parts, key=lambda x: -len(x.data.polygons))

def mesh_bounds(o):
    v = np.empty(len(o.data.vertices) * 3, np.float32); o.data.vertices.foreach_get('co', v); v = v.reshape(-1, 3)
    return v.min(0), v.max(0)

def lay_flat(o, yaw, pos, tilt=0.0):
    """Rest a loose piece on its broadest face (thinnest PCA axis up), turn it by yaw, put its lowest point on z 0 at pos (x, y)."""
    me = o.data; v = np.array([x.co[:] for x in me.vertices]); c = v.mean(0)
    w, e = np.linalg.eigh(np.cov((v - c).T)); up = e[:, 0]
    if up[2] < 0: up = -up
    R = Vector(up).rotation_difference(Vector((0, 0, 1))).to_matrix().to_4x4()
    M = Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(tilt, 4, 'Y') @ R @ Matrix.Translation(-Vector(c))
    me.transform(M); mn, mx = mesh_bounds(o)
    me.transform(Matrix.Translation((pos[0] - (mn[0] + mx[0]) / 2, pos[1] - (mn[1] + mx[1]) / 2, -mn[2] + 0.001)))

# ---------------------------------------------------------------- materials
def tile_img(t, kind, ch, scale, rot=(0, 0, 0)):
    nd = t.new('ShaderNodeTexImage'); nd.image = bpy.data.images.load(os.path.join(TEX, kind, '%s_%s.png' % (kind, ch)), check_existing=True)
    nd.image.colorspace_settings.name = 'sRGB' if ch == 'albedo' else 'Non-Color'
    nd.projection = 'BOX'; nd.projection_blend = 0.2
    mp = t.new('ShaderNodeMapping'); t.link(t.obj(), mp.inputs['Vector']); mp.inputs['Scale'].default_value = (scale, scale, scale)
    mp.inputs['Rotation'].default_value = rot; t.link(mp.outputs[0], nd.inputs['Vector'])
    return nd

def tile_set(t, kind, scale, rot=(0, 0, 0), bump=0.5):
    """Box-projected shared tileable: colour, AO, roughness, metallic, and a bump from its colour structure (the tangent frame of a
    box projection would break a baked normal map, so the tile's relief rides as bump)."""
    al = tile_img(t, kind, 'albedo', scale, rot); orm = tile_img(t, kind, 'orm', scale, rot)
    sep = t.new('ShaderNodeSeparateColor'); t.link(orm.outputs['Color'], sep.inputs[0])
    bw = t.new('ShaderNodeRGBToBW'); t.link(al.outputs['Color'], bw.inputs[0])
    bp = t.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = bump; bp.inputs['Distance'].default_value = 0.004; t.link(bw.outputs[0], bp.inputs['Height'])
    return al.outputs['Color'], sep.outputs[0], sep.outputs[1], sep.outputs[2], bp.outputs[0], bw.outputs[0]

def edge_mask(t, radius, flat=0.996, sharp=0.93):
    """1 on convex/concave edges, 0 on flats, from the Bevel node's normal against the true normal (independent of tessellation)."""
    bv = t.new('ShaderNodeBevel', samples=6); bv.inputs['Radius'].default_value = radius
    geo = t.new('ShaderNodeNewGeometry')
    d = t.new('ShaderNodeVectorMath', operation='DOT_PRODUCT'); t.link(bv.outputs[0], d.inputs[0]); t.link(geo.outputs['Normal'], d.inputs[1])
    return t.mrange(d.outputs['Value'], flat, sharp)

def moss_on(t, col, rough, z, zmax, amount, seed):
    """Moss creeping up from the foot: ragged upper edge, darker green, matte."""
    n = t.noise(14.0, 4.0, 0.62, seed); n2 = t.noise(40.0, 3.0, 0.6, seed + 3.3)
    top = t.math('ADD', zmax, t.math('MULTIPLY', t.math('SUBTRACT', n, 0.5), zmax * 1.6))
    m = t.math('MULTIPLY', t.mrange(t.math('SUBTRACT', z, top), 0.0, -0.04), t.mrange(n2, 0.30, 0.55, 0.55, 1.0)); m = t.math('MULTIPLY', m, amount, clamp=True)
    mc = t.mixc(t.mrange(n2, 0.3, 0.7), t.rgb('#3c5224'), t.rgb('#5f7a30'))
    return t.mixc(m, col, mc), t.mixf(m, rough, 0.92)

def sq_adopted(name, src, matname, p, H):
    """Adopted source material regraded (graded_albedo), with placed paint chips, moss, soil dust and the shared wear()."""
    global OAK_DARKEN
    od, hk = OAK_DARKEN, WOOD['hue_keep']
    OAK_DARKEN = p.get('darken', od); WOOD['hue_keep'] = p.get('hue_keep', hk)
    try: alb, tp = graded_albedo(name, src, matname, p)
    finally: OAK_DARKEN = od; WOOD['hue_keep'] = hk
    mat = new_mat('H_%s_%s' % (name, matname)); t = NT(mat)
    col = t.image(alb, 'sRGB').outputs['Color']
    if tp['arm']:
        sep = t.new('ShaderNodeSeparateColor'); t.link(t.image(tp['arm'], 'Non-Color').outputs['Color'], sep.inputs[0]); ao, rough = sep.outputs[0], sep.outputs[1]
    else:
        ao = 1.0; rough = t.image(tp['rough'], 'Non-Color').outputs['Color'] if tp['rough'] else 0.6
    nm = t.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = 1.0
    t.link(t.image(tp['nor'], 'Non-Color').outputs['Color'], nm.inputs['Color'])
    z = t.z(); metal = 0.0
    if 'chip' in p:        # paint chipped and peeled to the oak: edges first, the seat front, then flakes anywhere
        c = p['chip']; flake = t.noise(55.0, 5.0, 0.7, 9.1); big = t.noise(6.0, 3.0, 0.6, 2.2)
        edge = edge_mask(t, 0.011, 0.996, 0.90)
        drive = t.math('ADD', t.math('MULTIPLY', edge, 0.55 * c['edge']), t.math('MULTIPLY', t.mrange(big, 0.62, 0.40), 0.28 * c['patch']))
        v = t.math('ADD', flake, t.math('MULTIPLY', drive, 0.5))
        chip = t.mrange(v, 0.70 - 0.06 * c['amount'], 0.74 - 0.06 * c['amount'])
        oak = tile_img(t, 'oak_timber', 'albedo', 0.9, (0, 0, math.radians(90)))
        oakc = t.mixc(1.0, oak.outputs['Color'], (1.9, 1.7, 1.55, 1.0), blend='MULTIPLY')
        col = t.mixc(chip, col, oakc); rough = t.mixf(chip, rough, 0.85)
        ao = t.mixf(chip, ao, 1.0) if not isinstance(ao, float) else ao
    if 'moss' in p:
        col, rough = moss_on(t, col, rough, z, p['moss'][0] * H, p['moss'][1], 4.4)
    col, rough, metal = wear(t, col, rough, metal, H, p, None, ao if not isinstance(ao, float) else None)
    finish(t, col, rough, metal, ao, nm.outputs[0])
    return mat

def soil_mat(key, dry=False):
    mat = new_mat(key); t = NT(mat)
    n = t.noise(9.0, 5.0, 0.7, 1.7); g = t.noise(70.0, 3.0, 0.65, 3.1)
    vo = t.new('ShaderNodeTexVoronoi'); t.link(t.obj(), vo.inputs['Vector']); vo.inputs['Scale'].default_value = 90
    lo, hi = ('#4a3a2c', '#7a6650') if dry else ('#2c2118', '#4d3a29')
    col = t.mixc(t.mrange(n, 0.3, 0.7), t.rgb(lo), t.rgb(hi))
    col = t.mixc(t.mrange(vo.outputs['Distance'], 0.0, 0.18, 0.55, 0.0), col, t.rgb('#8a7a66'))     # pale grit and pebbles
    col = t.mixc(t.mrange(g, 0.55, 0.75, 0.0, 0.35), col, t.rgb('#1c150f'))
    rough = t.mixf(t.noise(30.0), 0.9, 0.98)
    bp = t.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = 0.7; bp.inputs['Distance'].default_value = 0.01; t.link(t.noise(55.0, 5.0, 0.65), bp.inputs['Height'])
    finish(t, col, rough, 0.0, 1.0, bp.outputs[0], bevel=0.0)
    return mat

def raw_mat(key, hexa='#a98a5e'):
    """Fresh-broken wood or clay core: pale, fibrous, matte."""
    mat = new_mat(key); t = NT(mat)
    mp = t.new('ShaderNodeMapping'); t.link(t.obj(), mp.inputs['Vector']); mp.inputs['Scale'].default_value = (45, 6, 45)
    s = t.noise(1.0, 3.0, 0.6, 2.0, vec=mp.outputs[0])
    col = t.mixc(t.mrange(s, 0.3, 0.7), t.rgb(hexa), t.mixc(1.0, t.rgb(hexa), (0.62, 0.55, 0.48, 1.0), blend='MULTIPLY'))
    bp = t.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = 0.8; bp.inputs['Distance'].default_value = 0.004; t.link(s, bp.inputs['Height'])
    finish(t, col, 0.9, 0.0, 1.0, bp.outputs[0], bevel=0.0)
    return mat

def iron_wear(t, col, rough, metal, ao, rust_amt, seed, zdrip=None):
    """Forged iron wear: rust blooms in the cavities and run down from the top, edges polished bright and glossy."""
    n1 = t.noise(9.0, 5.0, 0.65, seed); n2 = t.noise(34.0, 4.0, 0.6, seed + 2.2)
    cav = t.mrange(ao, 0.9, 0.35) if not isinstance(ao, float) else 0.0
    rust = t.math('MULTIPLY', t.math('ADD', t.math('MULTIPLY', cav, 0.7), t.mrange(n1, 0.45, 0.75, 0.0, 0.8)), rust_amt)
    rust = t.math('MULTIPLY', rust, t.mrange(n2, 0.28, 0.5), clamp=True)
    rc = t.mixc(n2, t.rgb('#6a3418'), t.rgb('#9a5a2c'))
    col = t.mixc(rust, col, rc); rough = t.mixf(rust, rough, 0.92); metal = t.mixf(rust, metal, 0.1)
    edge = t.math('MULTIPLY', edge_mask(t, 0.006, 0.998, 0.95), t.mrange(n1, 0.25, 0.6, 0.4, 1.0), clamp=True)
    col = t.mixc(t.math('MULTIPLY', edge, 0.8), col, t.mixc(1.0, col, (1.9, 1.75, 1.55, 1.0), blend='MULTIPLY'))
    rough = t.mixf(edge, rough, 0.3); metal = t.mixf(edge, metal, 1.0)
    return col, rough, metal

def mat_oak(key, H, p, seed=3.0, rot=(0, 0, 0)):
    mat = new_mat(key); t = NT(mat)
    col, ao, rough, metal, nrm, lumi = tile_set(t, 'oak_timber', p.get('tile', 0.9), rot, bump=0.6)
    col = t.mixc(1.0, col, p.get('lift', (1.55, 1.42, 1.3, 1.0)), blend='MULTIPLY')        # the tileable is dark; lift to warm oak
    rough = t.math('ADD', t.math('MULTIPLY', rough, 0.5), 0.45)
    col, rough, metal = wear(t, col, rough, 0.0, H, p, None, ao)
    finish(t, col, rough, 0.0, ao, nrm, bevel=p.get('bevel', 0.008))
    return mat

def mat_iron(key, H, p, seed=5.0, rust=0.9):
    mat = new_mat(key); t = NT(mat)
    col, ao, rough, metal, nrm, lumi = tile_set(t, 'iron', p.get('tile', 1.6), bump=0.5)
    col = t.mixc(1.0, col, (0.78, 0.76, 0.74, 1.0), blend='MULTIPLY')
    rough = t.math('ADD', t.math('MULTIPLY', rough, 0.45), 0.4)
    col, rough, metal = iron_wear(t, col, rough, 0.95, ao, rust, seed)
    finish(t, col, rough, metal, ao, nrm, bevel=0.003)
    return mat

def mat_stone(key, H, p, seed=7.0):
    mat = new_mat(key); t = NT(mat)
    col, ao, rough, metal, nrm, lumi = tile_set(t, 'sandstone', p.get('tile', 1.1), bump=0.9)
    col = t.mixc(1.0, col, p.get('lift', (0.95, 0.9, 0.84, 1.0)), blend='MULTIPLY')
    z = t.z(); n2 = t.noise(22.0, 3.0, 0.6, seed)
    dz = p.get('dirt_z', (0.30, 0.0))
    dirt = t.math('MULTIPLY', t.mrange(t.math('ADD', z, t.math('MULTIPLY', t.math('SUBTRACT', n2, 0.5), 0.12)), dz[0], dz[1]), p.get('dirt_amt', 0.8))
    col = t.mixc(dirt, col, t.rgb('#3a3028')); rough = t.mixf(dirt, rough, 0.95)
    cav = t.mrange(ao, 0.9, 0.4)
    col = t.mixc(t.math('MULTIPLY', cav, 0.45), col, t.rgb('#2e271f'))
    if p.get('moss', 0.75) > 0: col, rough = moss_on(t, col, rough, z, p.get('moss_h', 0.12), p.get('moss', 0.75), seed + 1.1)
    if p.get('void'):      # the gully: everything below the grate falls to near black
        col = t.mixc(t.mrange(z, -0.04, -0.20), col, t.rgb('#0b0907'))
    finish(t, col, rough, 0.0, ao, nrm, bevel=0.01)
    return mat

# ---------------------------------------------------------------- asset table
OAK = dict(wood='#664834')
SQ = {
    'Bench_A':   dict(kind='bench', res=2048, tris=6000, scale=1.15, squash=0.66, infl=0.007, lean=0.8, wob=0.003, wood='#7f9a84', darken=1.0, hue_keep=0.3,
                      tone=0.06, grime=0.6, bleach=0.0, dirt=0.2, polish=(0.7, 1.0),
                      chip=dict(amount=1.0, edge=1.0, patch=0.8)),
    'Bench_Broken': dict(kind='bench', broken=True, res=2048, tris=6000, scale=1.15, squash=0.66, infl=0.007, lean=1.4, wob=0.005, wood='#728a78', darken=0.92, hue_keep=0.3,
                      tone=0.08, grime=1.0, bleach=0.0, dirt=0.24, polish=(0.7, 1.0), moss=(0.12, 0.5),
                      chip=dict(amount=1.1, edge=1.2, patch=1.2)),
    'Planter_A': dict(kind='planter', src='planter_box_01', res=1024, tris=6000, scale=1.10, squash=1.10, infl=0.004, lean=0.8, wob=0.004, wood='#6a4c36',
                      tone=0.10, grime=0.8, bleach=0.5, dirt=0.12, polish=(0.8, 1.0), moss=(0.12, 0.6), soil=0.85),
    'Planter_B': dict(kind='planter', src='planter_box_02', res=1024, tris=6000, scale=0.93, squash=1.2, infl=0.004, lean=-0.7, wob=0.004, wood='#644730',
                      tone=0.10, grime=0.8, bleach=0.5, dirt=0.12, polish=(0.8, 1.0), moss=(0.1, 0.5), soil=0.85),
    'Planter_Broken': dict(kind='planter', broken=True, src='planter_box_01', res=1024, tris=6000, scale=1.10, squash=1.10, infl=0.004, lean=1.6, wob=0.006,
                      wood='#5c4330', tone=0.12, grime=1.0, bleach=0.8, dirt=0.16, polish=(0.8, 1.0), moss=(0.15, 0.7), soil=0.62),
    'Pot_Clay':  dict(kind='pot', src='planter_pot_clay', res=1024, tris=4500, scale=1.10, squash=1.0, infl=0.0, lean=1.0, wob=0.002, clay='#b0653f',
                      grime=0.5, dirt=0.06, polish=(0.8, 1.0), moss=(0.28, 0.8), soil=0.85, bulge=0.03),
    'Pot_Broken': dict(kind='pot', broken=True, iso_uv=True, src='planter_pot_clay', res=1024, tris=5500, scale=1.10, squash=1.0, infl=0.0, lean=0.0, wob=0.002, clay='#a85f3b',
                      grime=0.7, dirt=0.08, polish=(0.8, 1.0), moss=(0.22, 0.9), soil=0.80, bulge=0.03),
    'LanternPost_A': dict(kind='post', res=2048, tris=6000),
    'LanternPost_Broken': dict(kind='post', broken=True, res=2048, tris=6000),
    'Bunting':   dict(kind='bunting', res=1024, tris=4000),
    'Drain_A':   dict(kind='drain', res=1024, tris=4000),
}

# ---------------------------------------------------------------- adopted assets: benches, planters, pots
def shape_adopted(name, p):
    """Import the source, weld it (so booleans see a closed solid), centre, scale, squash, inflate chunky, lean and wobble."""
    src = p.get('src', 'painted_wooden_bench')
    hi = join(import_source(src, None), name + '_high'); weld(hi)
    recentre(hi, p['scale'])
    H = reshape(hi, dict(p, bulge=p.get('bulge', 0.0), hoop=0.0), name, None)
    if p.get('infl'): inflate(hi, p['infl'])
    return hi, H

def mats_for(name, p, hi, H):
    src = p.get('src', 'painted_wooden_bench')
    for i, m in enumerate(list(hi.data.materials)):
        hi.data.materials[i] = sq_adopted(name, src, m.name.split('.')[0], p, H)

def soil_slab(name, x0, x1, y0, y1, z, r, mat, amp=0.008, nx=22, ny=10, dome=0.012):
    """Soil top: a gently domed, lumpy grid (flat-ish at height z); the caller sinks its edge into the walls."""
    bm = bmesh.new(); vs = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            u, v = i / nx, j / ny; x = x0 + (x1 - x0) * u; y = y0 + (y1 - y0) * v
            e = math.sin(math.pi * u) ** 0.5 * math.sin(math.pi * v) ** 0.5
            row.append(bm.verts.new((x, y, z + dome * e + r.uniform(-amp, amp))))
        vs.append(row)
    for j in range(ny):
        for i in range(nx): bm.faces.new((vs[j][i], vs[j][i + 1], vs[j + 1][i + 1], vs[j + 1][i]))
    o = to_obj(bm, name, [mat]); return o

def mound(name, c, rx, ry, h, r, mat, n=26):
    """A low spilled-soil mound with a ragged skirt: a polar grid, height falling off smoothly to the ground."""
    bm = bmesh.new(); rings = []
    for k in range(7):
        s = k / 6.0; ring = []
        for j in range(n):
            a = 2 * math.pi * j / n; rag = 1.0 + 0.18 * math.sin(3 * a + 1.0) + 0.1 * math.sin(7 * a) + r.uniform(-0.05, 0.05)
            rr = s * rag
            zz = h * (1 - s * s) ** 1.4 * (1.0 + r.uniform(-0.12, 0.12)) if k < 6 else 0.0
            ring.append(bm.verts.new((c[0] + rx * rr * math.cos(a), c[1] + ry * rr * math.sin(a), zz + 0.0005)))
        rings.append(ring)
    for k in range(6):
        for j in range(n): bm.faces.new((rings[k][j], rings[k][(j + 1) % n], rings[k + 1][(j + 1) % n], rings[k + 1][j]))
    for f in bm.faces: f.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0: f.normal_flip()
    return to_obj(bm, name, [mat])

def build_bench(name, p):
    r = rng(name); hi, H = shape_adopted(name, p)
    mn, mx = bounds(hi); sx, sy = p['scale'], p['scale']; sz = p['scale'] * p['squash']
    seat = 0.65 * sz + p['infl']
    mats_for(name, p, hi, H)
    ORIGINS[name] = ['- Seat top height: %.3f m; overall %.2f x %.2f x %.2f m.' % (seat, mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2])]
    if not p.get('broken'): return hi, H
    raw = raw_mat('R_%s_core' % name)
    # 1. the front plank of the seat snaps at x 0.14 and the right part drops to the cobbles in front of the bench
    xa, xb = 0.12 * sx, 0.545 * sx + 0.04
    cut = rough_box(name + '_cut1', ((xa + xb) / 2, -0.12 * sy, 0.595 * sz), (xb - xa, 0.46 * sy * 0.62 + 0.0, 0.16 * sz), r, 0.012, raw, cuts=3)
    cut.location = (0, 0, 0)
    for v in cut.data.vertices: v.co.y = v.co.y - 0.07 * sy        # front strip: y from the front edge to about y -0.02
    piece = apply_boolean(dup(hi, name + '_plank'), cut, 'INTERSECT')
    apply_boolean(hi, cut, 'DIFFERENCE')
    # 2. the left-front foot breaks off (an end-panel foot): cut it away, then the corner sags
    foot = rough_box(name + '_cut2', (-0.62 * sx - 0.02, -0.16 * sy, 0.09 * sz), (0.16 * sx, 0.20 * sy, 0.20 * sz), r, 0.01, raw, cuts=2)
    foot_piece = apply_boolean(dup(hi, name + '_foot'), foot, 'INTERSECT')
    apply_boolean(hi, foot, 'DIFFERENCE')
    for c in (cut, foot): bpy.data.objects.remove(c, do_unlink=True)
    # sag the broken corner (left-front) by up to 8 cm, smoothly
    for v in hi.data.vertices:
        co = v.co; wx = max(0.0, min(1.0, (-co.x - 0.15 * sx) / (0.6 * sx))); wy = max(0.0, min(1.0, (-co.y + 0.05) / (0.3 * sy)))
        co.z -= 0.085 * (wx * wy) ** 1.3
    # the dropped plank: right part (x > 0.14) lies on the cobbles, turned a little
    parts = [x for x in loose_parts(piece) if len(x.data.polygons) > 20]
    plank = parts[0]
    for x in parts[1:]: bpy.data.objects.remove(x, do_unlink=True)
    lay_flat(plank, math.radians(24), (0.46 * sx, -0.43 * sy))
    # the broken-off foot lies by the sagging corner
    fp = [x for x in loose_parts(foot_piece) if len(x.data.polygons) > 12]
    if fp:
        lay_flat(fp[0], math.radians(-40), (-0.70 * sx, -0.46 * sy))
        for x in fp[1:]: bpy.data.objects.remove(x, do_unlink=True)
        plank = join([plank, fp[0]], name + '_loose')
    hi = join([hi, plank], name + '_high')
    mn2, mx2 = bounds(hi)
    hi.data.transform(Matrix.Translation((0, 0, -float(mn2[2]) + 0.0)))
    ORIGINS[name].append('- Pieces: the broken-off right front plank lies about %.2f m in front of the bench, the broken foot at its left-front corner.' % (0.43 * sy))
    return hi, H

def build_planter(name, p):
    r = rng(name); hi, H = shape_adopted(name, p)
    mn, mx = bounds(hi); H = float(mx[2] - mn[2]); mats_for(name, p, hi, H)
    inset = 0.012 * p['scale']; ztop = p['soil'] * H
    soil = soil_slab(name + '_soil', mn[0] + inset, mx[0] - inset, mn[1] + inset, mx[1] - inset, ztop, r, soil_mat('S_%s_soil' % name, dry=bool(p.get('broken'))), dome=0.01)
    ORIGINS[name] = ['- Size %.2f x %.2f x %.2f m (length x depth x height). Soil top height: %.3f m above the origin (%d%% of the %.3f m height; flat-ish, domed 1 cm); '
                     'inner soil area %.2f x %.2f m, centred on the origin. Wall top %.3f m.' % (mx[0] - mn[0], mx[1] - mn[1], H, ztop, round(p['soil'] * 100), H,
                                                                                         mx[0] - mn[0] - 2 * inset, mx[1] - mn[1] - 2 * inset, H)]
    extra = [soil]
    if p.get('broken'):
        raw = raw_mat('R_%s_core' % name, '#a28258')
        x0, x1 = -0.05 * p['scale'], 0.36 * p['scale']
        cut = rough_box(name + '_cut', ((x0 + x1) / 2, mn[1] + 0.03, H * 0.34), (x1 - x0, 0.10, 0.30 * H), r, 0.010, raw, cuts=3)
        board = apply_boolean(dup(hi, name + '_board'), cut, 'INTERSECT')
        apply_boolean(hi, cut, 'DIFFERENCE'); bpy.data.objects.remove(cut, do_unlink=True)
        parts = [x for x in loose_parts(board) if len(x.data.polygons) > 20]
        bd = parts[0]
        for x in parts[1:]: bpy.data.objects.remove(x, do_unlink=True)
        lay_flat(bd, math.radians(-28), (mx[0] * 0.80, mn[1] - 0.20), tilt=math.radians(6))
        mnd = mound(name + '_mound', (0.05 * p['scale'], mn[1] - 0.10), 0.30, 0.24, 0.085, r, soil_mat('S_%s_spill' % name, dry=True))
        extra += [bd, mnd]
        ORIGINS[name].append('- A front board is snapped out (jagged fresh-wood ends); the board chunk lies in front at the right, a dry soil mound is spilled in front of the gap '
                             '(low, up to 0.085 m high). Soil inside has sunk to %d%% of the height. No weeds: Unity adds them.' % round(p['soil'] * 100))
    hi = join([hi] + extra, name + '_high')
    return hi, H

def build_pot(name, p):
    r = rng(name); hi, H = shape_adopted(name, p)
    mn, mx = bounds(hi); H = float(mx[2] - mn[2]); mats_for(name, p, hi, H)
    ztop = p['soil'] * H
    # inner radius at the soil height: the widest vertex ring there, less the wall
    rr = max(math.hypot(v.co.x, v.co.y) for v in hi.data.vertices if abs(v.co.z - ztop) < 0.012) * 0.92
    bm = bmesh.new(); n = 28; ring = [bm.verts.new((rr * math.cos(2 * math.pi * j / n), rr * math.sin(2 * math.pi * j / n), ztop)) for j in range(n)]
    cen = bm.verts.new((0, 0, ztop + 0.012)); rin = [bm.verts.new((rr * 0.55 * math.cos(2 * math.pi * j / n), rr * 0.55 * math.sin(2 * math.pi * j / n), ztop + 0.008 + r.uniform(-0.004, 0.004))) for j in range(n)]
    for j in range(n):
        bm.faces.new((ring[j], ring[(j + 1) % n], rin[(j + 1) % n], rin[j])); bm.faces.new((rin[j], rin[(j + 1) % n], cen))
    for f in bm.faces:
        if f.normal.z < 0: f.normal_flip()
    soil = to_obj(bm, name + '_soil', [soil_mat('S_%s_soil' % name, dry=bool(p.get('broken')))])
    ORIGINS[name] = ['- Size %.2f x %.2f x %.2f m. Soil top height: %.3f m above the origin (%d%% of the height), soil disc radius %.3f m centred on the origin; rim top %.3f m.'
                     % (mx[0] - mn[0], mx[1] - mn[1], H, ztop, round(p['soil'] * 100), rr, H)]
    extra = [soil]
    if p.get('broken'):
        raw = raw_mat('R_%s_core' % name, '#b58459')
        ang0 = math.radians(35); shards = []
        for k, (da, hh) in enumerate(((-0.36, 0.56), (0.0, 0.66), (0.36, 0.50))):   # three adjacent pieces break out of the wall
            ang = ang0 + da
            cut = rough_box('%s_cut%d' % (name, k), (0.20 * math.cos(ang), 0.20 * math.sin(ang), H * (1.0 - hh / 2 + 0.04)), (0.34, 0.118, hh * H), r, 0.012,
                            raw, rot=Matrix.Rotation(ang, 3, 'Z'), cuts=3)
            sh = apply_boolean(dup(hi, '%s_shard%d' % (name, k)), cut, 'INTERSECT')
            apply_boolean(hi, cut, 'DIFFERENCE'); bpy.data.objects.remove(cut, do_unlink=True)
            pk = [x for x in loose_parts(sh) if len(x.data.polygons) > 12]
            for x in pk[1:]: bpy.data.objects.remove(x, do_unlink=True)
            shards.append(pk[0])
        parts = shards
        spots = [(0.24, -0.20), (0.13, -0.27), (-0.05, -0.30)]
        for k, sh in enumerate(parts):
            lay_flat(sh, r.uniform(0, 6.28), spots[k])
        mnd = mound(name + '_spill', (0.26, 0.17), 0.16, 0.11, 0.035, r, soil_mat('S_%s_spill' % name, dry=True))
        extra += parts + [mnd]
        ORIGINS[name].append('- A wedge about 70 degrees wide is broken out of the wall from the rim down to about 35-50%% height (fresh clay core on the break); %d shards lie in front, '
                             'a little dry soil spilled at the gap.' % len(parts))
    hi = join([hi] + extra, name + '_high')
    return hi, H

# ---------------------------------------------------------------- generated: lantern posts
POST_WEAR = dict(tone=0.12, grime=0.7, bleach=0.55, dirt=0.15, polish=(0.45, 0.62), lift=(1.55, 1.42, 1.3, 1.0), tile=0.9)
STONE_P = dict(tile=0.8, moss=0.5, moss_h=0.12)
IRON_P = dict(tile=1.6)
PROF_BAR = [(-1, -1), (1, -1), (1, 1), (-1, 1)]

def bar(h):
    return [(x * h, y * h) for x, y in PROF_BAR]

def hook_geometry(Za=2.63, y0=-0.07, yend=-0.54, R=0.045, hb=0.013):
    """Arm path (y, z) then the crook: the arm runs out to yend, then curls down, back toward the post and up (270 degrees)."""
    pts = [(y0 + (yend - y0) * i / 9.0, Za + 0.006 * math.sin(math.pi * i / 9.0)) for i in range(10)]
    cy, cz = yend, Za - R
    for i in range(1, 23):
        ph = math.radians(260.0 * i / 22.0); pts.append((cy - R * math.sin(ph), cz + R * math.cos(ph)))
    return pts, (cy, cz - R + hb * 0.7)

def build_footing(bm_s, r, z_top=0.40):
    w = 0.45; c = 0.045; base = chamfer_square(w, w, c)
    prof = []                      # 16 points: edge midpoints added for the noise to bite on
    for i in range(8):
        a, b = base[i], base[(i + 1) % 8]; prof += [a, ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)]
    zs = [-0.04, 0.03, 0.10, 0.17, 0.24, 0.31, 0.37, z_top]
    jit = [(r.uniform(-0.007, 0.007), r.uniform(-0.007, 0.007)) for _ in prof]
    def fn(x, y, z, k, j):
        s = 1.0 - 0.05 * max(0.0, (z - 0.15) / 0.25); top = 0.012 if k == len(zs) - 1 else 0.0
        return (x * s + jit[j][0] - math.copysign(top, x) * (abs(x) > 0.1), y * s + jit[j][1] - math.copysign(top, y) * (abs(y) > 0.1), z)
    return add_prism(bm_s, prof, zs, fn)

def build_post_geo(name, p, r, z0, z1, broken_bottom=False, broken_top=False, bracket=True, cap=True):
    """Returns (oak_obj, iron_obj, stone_obj or None) in post-local coordinates (z up, arm toward -Y)."""
    bm_o, bm_i = bmesh.new(), bmesh.new()
    prof = chamfer_square(0.16, 0.16, 0.022)
    zs = [z0 + (z1 - z0) * i / max(1, int(round((z1 - z0) / 0.06))) for i in range(int(round((z1 - z0) / 0.06)) + 1)]
    if cap and not broken_top: zs = [z for z in zs if z < z1 - 0.12] + [z1 - 0.10, z1 - 0.045, z1]
    jb = [(r.uniform(-0.03, 0.03), r.uniform(0, 1)) for _ in prof]
    def fn(x, y, z, k, j):
        x += 0.004 * math.sin(z * 1.7); y += 0.003 * math.sin(z * 2.3 + 1.0)
        if cap and not broken_top:
            if z > z1 - 0.11: s = 1.0 + 0.10 * (z - (z1 - 0.11)) / 0.11 if z < z1 - 0.05 else 1.0 - 0.28 * (z - (z1 - 0.05)) / 0.05 + 0.055   # flared cap, then a chamfered top
            else: s = 1.0
            x *= s; y *= s
        if broken_top and k == len(zs) - 1:      # splintered top: slanted, uneven, one long splinter
            z = z + 0.10 * (0.5 + 0.5 * x / 0.08) + jb[j][0] + (0.17 if j in (1, 2) else 0.0)
        if broken_bottom and k == 0:
            z = z + 0.08 * (0.5 - 0.5 * y / 0.08) + jb[j][0] * 0.8
        return (x, y, z)
    rings, faces = add_prism(bm_o, prof, zs, fn)
    for f in bm_o.faces:           # fracture faces are fresh wood (slot 1)
        if (broken_top and f.index >= len(faces) - 1 and all(v in rings[-1] for v in f.verts)) or (broken_bottom and all(v in rings[0] for v in f.verts)): f.material_index = 1
    if bracket:
        Za = 2.63
        for zc, h, w in ((Za, 0.075, 0.18), (2.16, 0.05, 0.178)):
            if z0 < zc - 0.05 and z1 > zc + 0.05:
                add_prism(bm_i, chamfer_square(w, w, 0.012), [zc - h / 2, zc - h / 2 + 0.006, zc + h / 2 - 0.006, zc + h / 2])
                for sx, sy in ((0, -1), (1, 0), (-1, 0)):
                    for dz in (-0.02, 0.02):
                        bmesh.ops.create_icosphere(bm_i, subdivisions=1, radius=0.0085, matrix=Matrix.Translation((sx * (w / 2 + 0.002), sy * (w / 2 + 0.002), zc + dz)))
        if z0 < Za - 0.1:
            pts, hk = hook_geometry(Za)
            sweep(bm_i, [(0.0, y, z) for y, z in pts], bar(0.013), scale_fn=lambda t: 1.0 - 0.38 * t)
            # scroll brace: starts up the post face, swings out to the arm underside, ends in a foot curl
            P0, P1, P2 = Vector((-0.07, 2.10)), Vector((-0.07, 2.50)), Vector((-0.38, Za - 0.012))
            br = [(0.0, *((1 - t) ** 2 * P0 + 2 * t * (1 - t) * P1 + t * t * P2)) for t in [i / 14.0 for i in range(15)]]
            sweep(bm_i, br, bar(0.0095), scale_fn=lambda t: 1.0 - 0.25 * t)
            cy, cz = -0.115, 2.10; sp = []
            for i in range(0, 17):
                th = -math.radians(360 * 1.25 * i / 16.0); rr = 0.040 * (1 - 0.7 * i / 16.0)
                sp.append((0.0, cy + rr * math.cos(th), cz + rr * math.sin(th)))
            sweep(bm_i, sp, bar(0.0085), scale_fn=lambda t: 1.0 - 0.3 * t)
            p['_hook'] = hk
    fix_normals(bm_i); fix_normals(bm_o)
    raw = raw_mat('R_%s_raw' % name)
    oak = to_obj(bm_o, name + '_oak', [mat_oak('G_%s_oak' % name, 3.0, POST_WEAR), raw], smooth=False)
    displace(oak, lambda co: 0.0035 * noise.fractal(Vector((co.x * 28, co.y * 28, co.z * 3.5)), 0.6, 2.0, 3))
    iron = to_obj(bm_i, name + '_iron', [mat_iron('G_%s_iron' % name, 3.0, IRON_P, rust=0.8)], smooth=False)
    return oak, iron

def build_post(name, p):
    r = rng(name); broken = bool(p.get('broken'))
    bm_s = bmesh.new(); build_footing(bm_s, r); fix_normals(bm_s)
    stone = to_obj(bm_s, name + '_stone', [mat_stone('G_%s_stone' % name, 0.4, STONE_P)], smooth=False)
    displace(stone, lambda co: 0.013 * noise.fractal(Vector((co.x * 9, co.y * 9, co.z * 9)), 0.55, 2.0, 3))
    parts = [stone]
    if not broken:
        oak, iron = build_post_geo(name, p, r, 0.30, 3.0)
        parts += [oak, iron]
        hk = p['_hook']; ORIGINS[name] = [
            '- Height 3.00 m (cap top); oak post 0.16 m square with 22 mm chamfers on a sandstone footing 0.45 m wide, 0.40 m tall (sunk 4 cm, bottom faces deleted).',
            '- Bracket arm reaches 0.54 m out along Blender -Y (Unity +Z) at about 2.63 m and ends in a crook (hook).',
            '- **hook (%.3f, %.3f, %.3f)** in Unity coordinates, relative to the origin (the lowest inner point of the crook where a lantern ring hangs; Blender (%.3f, %.3f, %.3f)). '
            'Hang the B8 Stall_Lantern with its top at this point.' % (0.0, hk[1], -hk[0], 0.0, hk[0], hk[1])]
        p['_hook_u'] = (0.0, hk[1], -hk[0])
    else:
        oak, iron = build_post_geo(name + 'a', p, r, 0.30, 1.20, broken_top=True, bracket=False, cap=False)      # the stump
        parts += [oak]
        up_oak, up_iron = build_post_geo(name + 'b', p, r, 1.20, 3.0, broken_bottom=True)                         # the fallen top
        for v in up_iron.data.vertices:          # arm and brace bend down under their own weight
            d = max(0.0, -v.co.y - 0.06)
            v.co.x += 0.30 * d * d
        yaw = math.radians(24)
        M = Matrix.Translation((0.40, -0.52, 0)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(math.radians(90), 4, 'Y') @ Matrix.Translation((0, 0, -1.2))
        for o in (up_oak, up_iron): o.data.transform(M)
        mn, mx = mesh_bounds(up_oak); dz = -float(mn[2]) + 0.002
        for o in (up_oak, up_iron): o.data.transform(Matrix.Translation((0, 0, dz)))
        parts += [up_oak, up_iron]
        ORIGINS[name] = ['- The post is snapped about 1.2 m up (stump with a splintered top, fresh wood showing). The top section (1.8 m, cap, collars, arm bent down by its weight, no lantern) '
                         'lies in front of it on the cobbles at about 24 degrees. Footing 0.45 m wide, 0.40 m tall.']
    p['_origin'] = 'at the bottom centre of the footing on the ground'
    hi = join(parts, name + '_high'); return hi, 3.0

# ---------------------------------------------------------------- generated: drain
def chamfer_box(bm, cx, cy, w, d, z0, z1, c, mat=0, ch=0.008):
    """Box centred at (cx, cy) with chamfered vertical edges (c) and a chamfered top edge (ch)."""
    def fn(x, y, z, k, j):
        top = k == 2
        return (x * (1 - ch * 1.6 / w * top) + cx, y * (1 - ch * 1.6 / d * top) + cy, z)
    return add_prism(bm, chamfer_square(w, d, c), [z0, z1 - ch, z1], fn, mat=mat)

def build_drain(name, p):
    r = rng(name)
    bm_s, bm_i = bmesh.new(), bmesh.new()
    for cx, cy, w, d in ((0, 0.21, 0.5, 0.08), (0, -0.21, 0.5, 0.08), (0.21, 0, 0.08, 0.34), (-0.21, 0, 0.08, 0.34)):
        chamfer_box(bm_s, cx, cy, w, d, -0.12, 0.0, 0.01)
    for cx, cy, w, d in ((0, 0.16, 0.34, 0.03), (0, -0.16, 0.34, 0.03), (0.16, 0, 0.03, 0.34), (-0.16, 0, 0.03, 0.34)):   # shaft walls
        chamfer_box(bm_s, cx, cy, w, d, -0.34, -0.06, 0.004, ch=0.002)
    chamfer_box(bm_s, 0, 0, 0.34, 0.34, -0.36, -0.30, 0.004, ch=0.002)                                                      # shaft floor
    for i in range(7):          # cast-iron bars
        chamfer_box(bm_i, -0.144 + i * 0.048, 0, 0.026, 0.37, -0.058, -0.012, 0.004, ch=0.004)
    for yy in (-0.085, 0.085):
        chamfer_box(bm_i, 0, yy, 0.37, 0.026, -0.052, -0.022, 0.004, ch=0.003)
    fix_normals(bm_s); fix_normals(bm_i)
    for k in range(14):         # grit and silt lodged between the bars
        bmesh.ops.create_icosphere(bm_s, subdivisions=1, radius=r.uniform(0.007, 0.013),
                                   matrix=Matrix.Translation((r.uniform(-0.15, 0.15), r.uniform(-0.15, 0.15), -0.045 + r.uniform(-0.01, 0.0))) @ Matrix.Diagonal((1, 1, 0.5, 1)))
    sp = dict(STONE_P, void=True, dirt_z=(0.0, -0.3), dirt_amt=0.5, moss=0.0, tile=0.9, lift=(0.80, 0.77, 0.72, 1.0))
    stone = to_obj(bm_s, name + '_stone', [mat_stone('G_%s_stone' % name, 0.4, sp)], smooth=False)
    displace(stone, lambda co: 0.004 * noise.fractal(Vector((co.x * 20, co.y * 20, co.z * 20)), 0.55, 2.0, 3))
    iron = to_obj(bm_i, name + '_iron', [mat_iron('G_%s_iron' % name, 1.0, IRON_P, rust=0.5)], smooth=False)
    ORIGINS[name] = ['- **Origin is the TOP centre**: the frame top surface is at height 0 (flush with the cobbles); the body is sunk to -0.36 m (bottom faces deleted). '
                     'Place it at the cobble surface height, not raised.',
                     '- Frame 0.50 x 0.50 m (80 mm stone border, 10 mm chamfer), opening 0.34 x 0.34 m with 7 chunky cast-iron bars (top at -12 mm) and two ties; dark gully below with grit in the slots.']
    p['_origin'] = 'at the TOP centre (frame top surface = height 0, flush with the cobbles)'
    return join([stone, iron], name + '_high'), 0.4

# ---------------------------------------------------------------- generated: bunting parts
ZOFF = 1.0            # parts are built 1 m up so the bottom-face rule of make_low leaves hanging cloth alone; split_bunting takes it back out
ROPE_Y = 0.35
DYES = ['#a8544a', '#c49a48', '#4f7388', '#d8ccaa', '#9a5a50', '#6b8794', '#9a8460']

def pennant_bm(bm, r, x_off, idx, nu, nv, ripple, torn=0, W=0.15, drop=0.38):
    R, th = 0.0105, 0.0015
    nflap, narc = max(2, nv // 5), max(3, nv // 5); nfront = nv - nflap - narc
    path = [(R, -0.04 + 0.04 * i / nflap, 0, 0.0) for i in range(nflap + 1)]
    path += [(R * math.cos(math.pi * i / narc), R * math.sin(math.pi * i / narc), 1, 0.0) for i in range(1, narc)]
    path += [(-R, -drop * i / nfront, 2, i / nfront) for i in range(nfront + 1)]
    sd = idx * 7.3
    def half(s, side):      # half width at drop fraction s on the left (-1) or right (+1)
        w = W * (1 - 0.965 * s) + 0.004
        if torn:
            rag = 1.0 + 0.22 * noise.noise(Vector((s * 7, side * 3 + sd, 0)))
            w *= rag
            if (torn == 1 and side < 0) or (torn == 2 and side > 0): w *= 0.30 if s > 0.38 else 1.0 - 0.7 * max(0, (s - 0.2) / 0.18)
        return w
    pl = bm.faces.layers.int.get('part') or bm.faces.layers.int.new('part')
    cl = bm.verts.layers.float_color.get('pc') or bm.verts.layers.float_color.new('pc')
    curl = r.uniform(0.7, 1.3) * (0.05 if torn else 0.03); lean = r.uniform(-1, 1) * 0.012
    def pt(i, j):
        y, z, kind, s = path[i]; u = -1 + 2 * j / nu; x = u * (half(s, -1 if u < 0 else 1))
        if kind == 2:
            y = y + curl * s * s + 0.012 * u * u * s + ripple * (0.0045 * noise.noise(Vector((x * 22, s * 14, sd))) + 0.0015 * noise.noise(Vector((x * 70, s * 40, sd + 3))))
            z = z + 0.012 * u * u * s * s * (1 if torn else 0.5)
            x = x + lean * s
        # normal in the (y, z) plane
        i0, i1 = max(0, i - 1), min(len(path) - 1, i + 1)
        ty, tz = path[i1][0] - path[i0][0], path[i1][1] - path[i0][1]; L = math.hypot(ty, tz) or 1.0
        return Vector((x, y, z)), Vector((0, -tz / L, ty / L))
    A, B = [], []
    for i in range(len(path)):
        ra, rb = [], []
        for j in range(nu + 1):
            P, N = pt(i, j); ra.append(bm.verts.new(P + N * th + Vector((x_off, 0, ZOFF)))); rb.append(bm.verts.new(P - N * th + Vector((x_off, 0, ZOFF))))
        A.append(ra); B.append(rb)
    fs = []
    for i in range(len(path) - 1):
        for j in range(nu):
            fs.append(bm.faces.new((A[i][j], A[i][j + 1], A[i + 1][j + 1], A[i + 1][j])))
            fs.append(bm.faces.new((B[i][j], B[i + 1][j], B[i + 1][j + 1], B[i][j + 1])))
    for i in range(len(path) - 1):
        fs.append(bm.faces.new((A[i][0], A[i + 1][0], B[i + 1][0], B[i][0]))); fs.append(bm.faces.new((A[i + 1][nu], A[i][nu], B[i][nu], B[i + 1][nu])))
    for j in range(nu):
        fs.append(bm.faces.new((A[-1][j], A[-1][j + 1], B[-1][j + 1], B[-1][j])))
        fs.append(bm.faces.new((A[0][j + 1], A[0][j], B[0][j], B[0][j + 1])))
    vs = {v for f in fs for v in f.verts}
    for f in fs: f[pl] = idx
    for v in vs: v[cl] = (idx / 8.0, idx / 8.0, idx / 8.0, 1.0)

def rope_bm(bm, r, idx, sides, nseg, ridge):
    pl = bm.faces.layers.int.get('part') or bm.faces.layers.int.new('part')
    cl = bm.verts.layers.float_color.get('pc') or bm.verts.layers.float_color.new('pc')
    rings = []
    for k in range(nseg + 1):
        x = -k / nseg; ring = []
        for j in range(sides):
            a = 2 * math.pi * j / sides; rr = 0.006 * (1 + ridge * math.cos(3 * a - 2 * math.pi * 17 * x))
            ring.append(bm.verts.new((x, ROPE_Y + rr * math.cos(a), ZOFF + rr * math.sin(a))))
        rings.append(ring)
    fs = []
    for k in range(nseg):
        for j in range(sides): fs.append(bm.faces.new((rings[k][j], rings[k][(j + 1) % sides], rings[k + 1][(j + 1) % sides], rings[k + 1][j])))
    fs.append(bm.faces.new(list(reversed(rings[0])))); fs.append(bm.faces.new(rings[-1]))
    for f in fs: f[pl] = idx
    for ring in rings:
        for v in ring: v[cl] = (idx / 8.0, idx / 8.0, idx / 8.0, 1.0)

def mat_cloth(key):
    mat = new_mat(key); t = NT(mat)
    pc = t.new('ShaderNodeAttribute', attribute_name='pc').outputs['Fac']
    col = t.rgb(DYES[0])
    steps = []
    for i in range(1, 7):
        st = t.mrange(pc, (i - 0.5) / 8.0, (i - 0.42) / 8.0); steps.append(st); col = t.mixc(st, col, t.rgb(DYES[i]))
    rope = steps[5]
    z = t.z(); sep = t.new('ShaderNodeSeparateXYZ'); t.link(t.obj(), sep.inputs[0])
    wa = t.new('ShaderNodeTexWave', wave_type='BANDS', bands_direction='X'); t.link(t.obj(), wa.inputs['Vector']); wa.inputs['Scale'].default_value = 260; wa.inputs['Distortion'].default_value = 1.0
    wb = t.new('ShaderNodeTexWave', wave_type='BANDS', bands_direction='Z'); t.link(t.obj(), wb.inputs['Vector']); wb.inputs['Scale'].default_value = 260; wb.inputs['Distortion'].default_value = 1.0
    weave = t.math('MULTIPLY', t.math('ADD', wa.outputs['Fac'], wb.outputs['Fac']), 0.5)
    n1 = t.noise(6.0, 4.0, 0.6, 1.0); n2 = t.noise(30.0, 4.0, 0.6, 5.0)
    col = t.mixc(t.math('MULTIPLY', t.math('SUBTRACT', weave, 0.5), 0.5), col, t.mixc(1.0, col, (0.7, 0.7, 0.7, 1.0), blend='MULTIPLY'))
    fade = t.math('MULTIPLY', t.mrange(z, ZOFF, ZOFF - 0.38), t.mrange(n1, 0.3, 0.7, 0.4, 1.0))         # sun-bleached toward the free edge
    col = t.mixc(t.math('MULTIPLY', fade, 0.40), col, t.rgb('#d6cdb8'))
    dirt = t.math('MULTIPLY', t.mrange(n1, 0.35, 0.85), t.mrange(t.math('ADD', t.math('SUBTRACT', ZOFF, z), t.math('MULTIPLY', n2, 0.1)), 0.05, 0.30, 0.15, 0.7))
    col = t.mixc(t.math('MULTIPLY', dirt, 0.28), col, t.rgb('#4a3d2e'))
    # rope: three strands twisting (angle about its axis + position along it), lighter on the ridges
    yy = t.math('SUBTRACT', sep.outputs['Y'], ROPE_Y); zz = t.math('SUBTRACT', sep.outputs['Z'], ZOFF)
    ang = t.math('ARCTAN2', yy, zz)
    ph = t.math('SUBTRACT', t.math('MULTIPLY', ang, 3.0 / 1.0), t.math('MULTIPLY', sep.outputs['X'], 2 * math.pi * 17 * -1.0))
    ridge = t.mrange(t.math('SINE', ph), -1.0, 1.0)
    rc = t.mixc(ridge, t.rgb('#5e4c32'), t.mixc(n2, t.rgb('#a89468'), t.rgb('#8c7852')))
    col = t.mixc(rope, col, rc)
    rough = t.mixf(rope, 0.88, 0.9)
    height = t.mixf(rope, t.math('MULTIPLY', weave, 0.6), ridge)
    bp = t.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = 0.6; bp.inputs['Distance'].default_value = 0.0025; t.link(height, bp.inputs['Height'])
    finish(t, col, rough, 0.0, 1.0, bp.outputs[0], bevel=0.0)
    return mat

def build_bunting(name, p):
    r = rng(name); mat = mat_cloth('G_%s_cloth' % name)
    hi, lo = bmesh.new(), bmesh.new()
    spec = [(0, 0), (1, 0), (2, 0), (3, 0), (4, 1), (5, 2)]            # (part idx, torn kind); pennants then T0 (left side torn), T1 (right)
    for k, (idx, torn) in enumerate(spec):
        pennant_bm(hi, rng('h%d' % idx), 0.42 * k, idx, 22, 40, 1.0, torn)
        pennant_bm(lo, rng('h%d' % idx), 0.42 * k, idx, 6, 14, 0.0, torn)
    rope_bm(hi, r, 6, 16, 90, 0.14); rope_bm(lo, r, 6, 8, 14, 0.0)
    fix_normals(hi); fix_normals(lo)
    ho = to_obj(hi, name + '_high', [mat]); lo_o = to_obj(lo, name + '_lowsrc', [mat], smooth=False)
    p['_lo_src'] = lo_o; p['_objs'] = True
    p['_origin'] = 'per child mesh (below); the parts are not laid out for placement: instantiate each by name'
    ORIGINS[name] = [
        '- One FBX, one material, one texture set, SEVEN child meshes (all at the FBX origin; each mesh is built about its own pivot): `Pennant_0` (madder red), `Pennant_1` (ochre), '
        '`Pennant_2` (woad blue), `Pennant_3` (cream), `Pennant_T0` and `Pennant_T1` (torn, ragged, faded madder / woad), `Rope_1m`. The torn pennants live in this FBX (no separate Bunting_Torn folder).',
        '- **Pennant**: origin = the middle of its top edge, where the rope passes. The top edge runs along local X, so **local X is "along the rope"** (it is Unity +X after export). '
        'The pennant **hangs down local -Y in Unity (Blender -Z)**; the cloth faces +/-Z in Unity (front = Unity +Z). About 0.30 m wide at the top, 0.38 m drop, hem folded over a 12 mm rope '
        '(hem inner radius 9 mm), solid 3 mm thick (two shells joined at the edges, so it is closed and double sided with no cull trouble).',
        '- **Rope_1m**: origin at its start; it runs 1.00 m along **Unity +X** (authored along Blender -X; Unity x = -Blender x), 12 mm diameter, 8 sides, three-strand twist baked into the normal.',
        '- Per-mesh bounds (m, Unity x/y/z): pennants 0.30 x 0.38 x ~0.03 (hem top at +0.0105 above the origin); rope 1.00 x 0.012 x 0.012.']
    return ho, 0.4

def split_bunting(lo, name):
    """Separate the baked low into Pennant_0..3, Pennant_T0/T1 and Rope_1m, each moved so its pivot sits at its own origin (z offset removed)."""
    names = {0: 'Pennant_0', 1: 'Pennant_1', 2: 'Pennant_2', 3: 'Pennant_3', 4: 'Pennant_T0', 5: 'Pennant_T1', 6: 'Rope_1m'}
    outs = []; mat = lo.data.materials[0]
    for idx, nm in names.items():
        bm = bmesh.new(); bm.from_mesh(lo.data); pl = bm.faces.layers.int.get('part')
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[pl] != idx], context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        off = Vector((0.42 * idx, 0, ZOFF)) if idx < 6 else Vector((0, ROPE_Y, ZOFF))
        for v in bm.verts: v.co -= off
        me = bpy.data.meshes.new(nm); bm.to_mesh(me); bm.free()
        for f in me.polygons: f.use_smooth = True
        if hasattr(me, 'set_sharp_from_angle'): me.set_sharp_from_angle(angle=math.radians(50))
        me.materials.append(mat)
        o = bpy.data.objects.new(nm, me); bpy.context.scene.collection.objects.link(o); outs.append(o)
    bpy.data.objects.remove(lo, do_unlink=True)
    return outs

# ---------------------------------------------------------------- SOURCES.md, build dispatch, main
SQ_CREDIT = {'painted_wooden_bench': 'Eugene Chebotar', 'planter_box_01': 'see Art/Source/PolyHaven/SOURCES.md', 'planter_box_02': 'see Art/Source/PolyHaven/SOURCES.md',
             'planter_pot_clay': 'Amal Kumar'}

def sq_sources(name, p, lo, extra=None):
    tris = tris_of(lo); mn, mx = bounds(lo); res = int(p['res'] * RES_SCALE)
    head = '# %s (Hollowmere square dressing)\n\nGenerated by `Art/Blender/town/sc_square_dressing.py` (see its header for the run line).\n\n' % name
    if p['kind'] in ('bench', 'planter', 'pot'):
        src = p.get('src', 'painted_wooden_bench')
        s = ('- Source: Poly Haven `%s` (https://polyhaven.com/a/%s); raw files in `Art/Source/PolyHaven/%s/`.\n- Licence: CC0 1.0 (https://polyhaven.com/license). Author: see '
             '`Art/Source/PolyHaven/SOURCES.md`. No attribution required.\n- What we changed: %s\n' % (src, src, src, p.get('_changes', 'regraded, reshaped, rebaked.')))
    else:
        s = ('- Source: original, generated in the script (no external mesh). Textures are procedural; wood, iron, stone and cloth structure comes from the shared B1 tileables '
             '(`Art/Textures/B1_town`, their sources in its SOURCES.md).\n- Licence: ours.\n')
    ax = ('- Size (Blender x, y, z; Unity x, z, y): %.2f x %.2f x %.2f m. Origin %s. Authored z up, front -Y in Blender = +Z in Unity (Unity (x,y,z) = Blender (-x, z, -y)).\n'
          '- %d tris, one material%s; maps %d px: `%s_BaseColor` (sRGB), `%s_Normal` (OpenGL, Y+), `%s_ORM` (R occlusion, G roughness, B metallic; linear).\n'
          % (mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2], p.get('_origin', 'at the bottom centre on the ground'), tris, '' if not p.get('_objs') else ' (all child meshes)', res, name, name, name))
    return head + s + ax + '\n'.join(ORIGINS.get(name, [])) + '\n'

def describe(name, p):
    k = p['kind']
    if k == 'bench':
        return ('scaled %.0f%% wide, height squashed to a %.2f m seat, planks inflated 7 mm for a chunkier read, paint regraded to a faded sage (the source red), '
                'chipped to oak at the edges, patchy and in flakes, dirt splashed up the legs%s; decimated and rebaked onto the source UVs (fresh-wood break faces packed beside them).'
                % ((p['scale'] - 1) * 100, 0.65 * p['scale'] * p['squash'], '; broken variant: front plank snapped and dropped, left-front foot gone and the corner sagged, more peeling' if p.get('broken') else ''))
    if k == 'planter':
        return ('scaled %.0f%%, height x%.2f, planks inflated, regraded to warm oak, board tone, joint grime, moss at the foot, dirt splash; a generated soil top added (own '
                'material baked into the same set)%s. The source has no iron straps, so none were added.' % ((p['scale'] - 1) * 100, p['squash'],
                '; a front board broken out with the chunk and a spilled soil mound' if p.get('broken') else ''))
    return ('scaled %.0f%%, regraded to warm terracotta, moss up from the foot, grime in the cavities, a generated soil disc%s.'
            % ((p['scale'] - 1) * 100, '; a wedge broken out with shards and spilled soil' if p.get('broken') else ''))

def build_high_sq(name, p):
    p['_name'] = name; p['_changes'] = describe(name, p) if p['kind'] in ('bench', 'planter', 'pot') else ''
    k = p['kind']
    if k == 'bench': return build_bench(name, p)
    if k == 'planter': return build_planter(name, p)
    if k == 'pot': return build_pot(name, p)
    return globals()['build_' + k](name, p)

def export_sq(lo, name, p, paths):
    d = os.path.join(OUT, name); fbx = os.path.join(d, name + '.fbx')
    objs = lo if isinstance(lo, list) else [lo]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False, path_mode='STRIP')
    if not EXPORT: return
    dd = os.path.join(DEST, name); os.makedirs(dd, exist_ok=True)
    for f in [fbx] + list(paths.values()): shutil.copy(f, os.path.join(dd, os.path.basename(f)))
    with open(os.path.join(dd, 'SOURCES.md'), 'w', encoding='utf-8', newline='\n') as fh: fh.write(sq_sources(name, p, objs[0] if len(objs) == 1 else join_copy(objs), None))

def join_copy(objs):
    cp = []
    for o in objs:
        c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c); cp.append(c)
    return join(cp, 'tmp_all')

def shrink(lo, budget):
    """Second decimate pass when make_low's single collapse left the mesh over budget (boolean-cut pieces keep many tiny faces)."""
    if tris_of(lo) > budget:     # touching boards share edges between 4 faces after the weld: split them so the collapse can work
        bm = bmesh.new(); bm.from_mesh(lo.data); bmesh.ops.split_edges(bm, edges=[e for e in bm.edges if len(e.link_faces) > 2]); bm.to_mesh(lo.data); bm.free()
    for _ in range(3):
        n = tris_of(lo)
        if n <= budget: break
        mod = lo.modifiers.new('dec2', 'DECIMATE'); mod.ratio = max(0.05, budget * 0.95 / n); mod.use_collapse_triangulate = True
        dg = bpy.context.evaluated_depsgraph_get(); m2 = bpy.data.meshes.new_from_object(lo.evaluated_get(dg)); lo.modifiers.clear()
        old = lo.data; lo.data = m2; m2.name = lo.name
    return lo

def write_index():
    """Square/SOURCES.md: one row per exported asset, rebuilt from the per-asset SOURCES.md files."""
    import re
    rows = []
    for n in SQ:
        f = os.path.join(DEST, n, 'SOURCES.md')
        if not os.path.exists(f): continue
        t = open(f, encoding='utf-8').read()
        tr = re.search(r'(\d+) tris', t); sz = re.search(r'Size \(Blender[^)]*\): ([\d.]+ x [\d.]+ x [\d.]+) m', t)
        src = re.search(r'Poly Haven `([^`]+)`', t)
        rows.append('| %s | %s | %s | %s | %s |' % (n, ('Poly Haven `%s` (CC0)' % src.group(1)) if src else 'generated (ours)', tr.group(1) if tr else '?', sz.group(1) if sz else '-', SQ[n]['res']))
    head = ('# Town square dressing (Hollowmere square)\n\nGenerated by `Art/Blender/town/sc_square_dressing.py` (re-run lines in its header). One folder per asset: `<Asset>.fbx`, '
            '`<Asset>_BaseColor.png`, `<Asset>_Normal.png` (OpenGL), `<Asset>_ORM.png` (R AO, G rough, B metal), and a `SOURCES.md` with source, licence, origin and the numbers '
            'Unity reads (`soil ... N m` for planters and pots, `hook (x, y, z)` for LanternPost_A, origin notes for Drain_A and Bunting).\n\n'
            '| asset | source | tris | size (Blender x, y, z m) | map px |\n|---|---|---|---|---|\n')
    with open(os.path.join(DEST, 'SOURCES.md'), 'w', encoding='utf-8', newline='\n') as fh: fh.write(head + '\n'.join(rows) + '\n')

def flatten_ao(path):
    """Thin cloth: the geometric AO bake reads the hi mesh's ripples as occlusion (blotchy camouflage), so the AO channel is set to 1."""
    im, a = img_np(path, 'Non-Color'); a = a.copy(); a[..., 0] = 1.0
    save_np(a[..., :3], path, 'Non-Color')

def main():
    bpy.ops.wm.read_homefile(use_empty=True)
    names = [n for n in SQ if ONLY is None or n in ONLY]
    lows = {}
    for name in (names if RENDER_ONLY else []):
        fbx = os.path.join(OUT, name, name + '.fbx')
        if not os.path.exists(fbx): continue
        before = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=fbx)
        los = [o for o in bpy.data.objects if o not in before and o.type == 'MESH']
        bpy.context.view_layer.update(); d = os.path.join(OUT, name)
        lm = bpy.data.materials.new('R_' + name); lm.use_nodes = True
        low_material(lm, dict(col=os.path.join(d, name + '_BaseColor.png'), nrm=os.path.join(d, name + '_Normal.png'), orm=os.path.join(d, name + '_ORM.png')))
        for lo in los:
            lo.data.transform(lo.matrix_world); lo.matrix_world = Matrix(); lo.data.materials.clear(); lo.data.materials.append(lm); lo.hide_render = True
        lows[name] = los
    for name in ([] if RENDER_ONLY else names):
        p = dict(SQ[name]); os.makedirs(os.path.join(OUT, name), exist_ok=True)
        hi, H = build_high_sq(name, p)
        res = int(p['res'] * RES_SCALE)
        print('SQ hi tris', name, tris_of(hi))
        lo = make_low(p.pop('_lo_src', None) or hi, name, p)
        lo = shrink(lo, p['tris'])
        paths = bake(hi, lo, name, res)
        if p['kind'] == 'bunting': flatten_ao(paths['orm'])
        if p['kind'] == 'bunting': lo = split_bunting(lo, name)
        export_sq(lo, name, p, paths)
        los = lo if isinstance(lo, list) else [lo]
        for o in los: print('SQ %-18s %-12s tris %5d  res %d' % (name, o.name, tris_of(o), res))
        bpy.data.objects.remove(hi, do_unlink=True)
        for o in los: o.hide_render = True
        lows[name] = los
    if EXPORT: write_index()
    if not RENDER: return
    rd = os.path.join(OUT, 'renders'); os.makedirs(rd, exist_ok=True)
    co = review_scene(); ground = bpy.data.objects['REV_ground']; ground.location.z = -0.003
    order = sorted(lows, key=lambda n: n == 'Drain_A')
    for name in order:
        los = lows[name]
        if name == 'Drain_A':
            bpy.context.view_layer.objects.active = ground
            cutb = bpy.data.objects.new('hole', bpy.data.meshes.new('hole')); bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bm.to_mesh(cutb.data); bm.free()
            cutb.scale = (0.44, 0.44, 1.0); bpy.context.scene.collection.objects.link(cutb)
            ground.modifiers.new('h', 'BOOLEAN').object = cutb; ground.modifiers['h'].operation = 'DIFFERENCE'; ground.modifiers['h'].solver = 'EXACT'
            ground.data = bpy.data.meshes.new_from_object(ground.evaluated_get(bpy.context.evaluated_depsgraph_get())); ground.modifiers.clear(); cutb.hide_render = True
        for lo in los: lo.hide_render = False; lo.location = (0, 0, 0)
        extra_objs = []
        if name == 'Bunting':       # review layout: two ropes end to end along Unity +X, the pennants hanging from them
            k = 0
            for lo in los:
                if lo.name.startswith('Pennant'): lo.location = (0.7 - 0.32 * k, 0, 1.2); k += 1
                else:
                    lo.location = (0.9, 0, 1.2); r2 = lo.copy(); r2.data = lo.data; r2.location = (-0.1, 0, 1.2)
                    bpy.context.scene.collection.objects.link(r2); extra_objs.append(r2)
            c = Vector((-0.1, 0, 1.0)); s = 2.2; d = s * 1.35
            aim(co, (c.x + 0.25 * d, c.y - d, c.z + 0.12 * d), c, 50)
        else:
            mins = np.array([bounds(lo)[0] for lo in los]).min(0); maxs = np.array([bounds(lo)[1] for lo in los]).max(0)
            c = Vector(((mins[0] + maxs[0]) / 2, (mins[1] + maxs[1]) / 2, (mins[2] + maxs[2]) / 2)); s = float((maxs - mins).max())
            d = s * 2.3 + 0.35; lens = 68 if s > 2.0 else 50
            aim(co, (c.x + d * 0.62, c.y - d * 0.85, c.z + d * 0.55), c, lens)
        render(os.path.join(rd, '%s_tq.png' % name))
        for o in extra_objs: bpy.data.objects.remove(o, do_unlink=True)
        for lo in los: lo.hide_render = True

main()
