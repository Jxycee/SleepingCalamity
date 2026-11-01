"""CellarClutter: B5 cellar clutter kit (T2/T3 props), five loose pieces to dress generated Cellars rooms like the approved dungeon corner
(Reference/hollowmere/dungeon_corner/SHEET_dungeon_corner_unity.jpg: clay jars, barrels, oak, vault stone; paler gold light, grey-brown darks).

Pieces (each a separate ROOT object, no parent empty; authored with its pivot at the bottom centre of its footprint, resting on y = 0 in Unity):
  Crate       one oak crate, 0.7 m cube: plank faces, corner posts, iron corner straps, a cracked plank and a scuffed corner
  CrateStack  three crates, two on the floor side by side and one on top, plus a smaller lid-off crate on the second; wider along Unity z
  JarCluster  five clay pots (amphora, belly pot, small jar, crock, one lying on its side) with amber / cream glaze lips
  Rubble      fallen vault stones, broken bricks and mortar crumbs, heaped higher against the back (-X) side: reads as collapse at a wall base
  SackPile    three slumped grain sacks (CV_Limewash canvas stand-in, oak ties); no new texture work

Convention (same as the vault kit's wall pieces LarderShelf / Barrels, which CellarFloorBuilder.Art.cs places with faceLeft: "local +x points
into the room"): the FRONT of every piece is its local +X in Unity, the BACK (the side that touches the wall) is -X, Unity +Z runs along the wall, +Y is up.
Authoring frame is the vault's: A = (x front, y along the wall, z up) = Unity (x, z, y); to_object rotates it 180 degrees about Z into Blender axes so the
FBX export (-Z forward, Y up, FBX_SCALE_UNITS, bake_space_transform, path_mode STRIP: the exact vault settings) lands on Unity (x, y, z).
Materials are the vault's CV_* names only (CV_Oak, CV_BarrelOak, CV_Iron, CV_Clay, CV_GlazeAmber, CV_GlazeCream, CV_VaultStone, CV_Brick,
CV_Mortar, CV_Limewash), all of which DungeonCornerBuilder.Slots / FlatSlots already map. Each mesh carries the CORNER float colour attribute "wear"
(R grime, G moss, B polish) the WearLit materials read.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/cellars/sc_cellar_clutter.py -- --export
      [--export-dir <dir>] [--out <scratch>] [--render] [--rres 1000] [--samples 20] [--tris]
  --export-dir defaults to Assets/_Project/Content/Art/Cellar/B5/CellarClutter (CellarClutter.fbx). --render writes clutter_sheet.png to --out.
Budget: piece 1.5-4k tris, all under 15k. Reuses sc_cellar_vault.py as a module (helpers, materials, lathe); that file and CellarVault.fbx are untouched.
"""
import sys, os, math, tempfile
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
if '--' not in sys.argv:
    sys.argv.append('--')
if '--out' not in sys.argv:
    sys.argv += ['--out', os.path.join(tempfile.gettempdir(), 'sc_cellar_clutter')]
sys.path.insert(0, HERE)
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_cellar_vault as CV
FK = CV.FK
argv = FK.argv
Rx, Ry, Rz, T = CV.Rx, CV.Ry, CV.Rz, CV.T
SAMPLES = int(FK.arg('--samples', '20')); CV.SAMPLES = SAMPLES; CV.RRES = int(FK.arg('--rres', '1000'))
R_ = lambda s: FK.rng('cc:' + s)

# ----------------------------------------------------------------------------- crates
def crate(pc, c, s, yaw=0.0, lid=True, seed=0, broken=True, planks=5, buried=False):
    """Oak crate of side s centred at (c.x, c.y), floor at c.z. Planks run horizontally on the four sides, a posts-and-straps frame, optional lid."""
    R = R_('crate%d' % seed)
    base = T(c[0], c[1], c[2]) @ Rz(yaw)
    def place(mat, size, pos, rot=None, cham=0.012, **kw):
        xf = base @ T(*pos) @ (rot if rot is not None else Matrix.Identity(4))
        FK.add_box(pc, mat, size, xf, cham=cham, uv_world=False, long_axis=kw.pop('long_axis', None), **kw)
    t = 0.035
    place('barrel', (s - 0.09, s - 0.09, s - 0.10), (0, 0, s / 2), cham=0)                       # dark core hides the gaps
    ph = (s - 0.03) / planks
    for sx in (-1, 1):                                                              # front / back faces (full width)
        for i in range(planks):
            if broken and seed == 0 and sx == 1 and i == 2:                          # the cracked plank: two short halves, one knocked out
                for sy, ln in ((-1, 0.26), (1, 0.18)):
                    place('oak', (t, ln, ph - 0.012), (sx * (s / 2 - t / 2 + 0.01 * sy), sy * (s / 2 - ln / 2 - 0.02), 0.015 + ph * (i + 0.5)),
                          Ry(R.uniform(-3, 3) * sy), long_axis=1, wob=0.002, seed=i + sx)
                continue
            place('oak', (t, s - 0.01, ph - 0.012), (sx * (s / 2 - t / 2), 0, 0.015 + ph * (i + 0.5)), Ry(R.uniform(-0.6, 0.6)), long_axis=1, wob=0.002, seed=i + 10 * sx + seed)
    for sy in (-1, 1):                                                              # side faces, between the posts
        for i in range(planks):
            place('oak', (s - 0.10, t, ph - 0.012), (0, sy * (s / 2 - t / 2), 0.015 + ph * (i + 0.5)), Rx(R.uniform(-0.6, 0.6)), long_axis=0, wob=0.002, seed=i + 5 * sy + seed)
    for sx in (-1, 1):
        for sy in (-1, 1):
            place('barrel', (0.075, 0.075, s), (sx * (s / 2 - 0.0375), sy * (s / 2 - 0.0375), s / 2), cham=0.012 if planks >= 5 else 0, long_axis=2, wob=0.002, seed=sx + 2 * sy + seed)
    if buried:                                                                      # a crate carrying another: one flat lid board is enough
        place('oak', (s - 0.01, s - 0.01, 0.03), (0, 0, s + 0.005), cham=0.008, long_axis=0)
    elif lid:
        lp = 4
        lw = (s - 0.01) / lp
        for i in range(lp):
            place('oak', (s - 0.01, lw - 0.012, 0.03), (0, -s / 2 + 0.005 + lw * (i + 0.5), s + 0.005), Rz(R.uniform(-0.4, 0.4)), long_axis=0, wob=0.002, seed=i + seed)
        for sy in (-1, 1):
            place('oak', (s - 0.02, 0.07, 0.03), (0, sy * (s / 2 - 0.16), s + 0.035), cham=0.008)                     # battens
    else:
        place('oak', (s - 0.12, s - 0.12, 0.03), (0, 0, s * 0.45), cham=0.006, long_axis=0)                         # inner floor of the open crate
    # iron straps: an L at each top and bottom corner, flush plates with a nail head
    for sx in (-1, 1):
        for sy in (-1, 1):
            for zz in (0.075, s - 0.075):
                place('iron', (0.012, 0.17, 0.05), (sx * (s / 2 + 0.002), sy * (s / 2 - 0.085), zz), cham=0)
                place('iron', (0.17, 0.012, 0.05), (sx * (s / 2 - 0.085), sy * (s / 2 + 0.002), zz), cham=0)

# ----------------------------------------------------------------------------- pots
POTS = {
    # (r, h) bottom to top, lathe about z; key bands: lip glaze from index `lip`
    'amphora': ([(0, 0), (0.09, 0.0), (0.15, 0.06), (0.21, 0.24), (0.19, 0.44), (0.11, 0.60), (0.085, 0.68), (0.115, 0.71), (0.115, 0.75), (0.075, 0.75), (0.075, 0.70), (0, 0.68)], 6),
    'belly':   ([(0, 0), (0.13, 0.0), (0.2, 0.05), (0.27, 0.2), (0.26, 0.34), (0.17, 0.46), (0.145, 0.50), (0.18, 0.53), (0.18, 0.56), (0.12, 0.56), (0, 0.52)], 6),
    'jar':     ([(0, 0), (0.10, 0.0), (0.14, 0.05), (0.16, 0.16), (0.12, 0.28), (0.085, 0.31), (0.10, 0.34), (0.10, 0.36), (0.065, 0.36), (0, 0.33)], 5),
    'crock':   ([(0, 0), (0.14, 0.0), (0.17, 0.05), (0.19, 0.18), (0.18, 0.26), (0.205, 0.28), (0.205, 0.31), (0.15, 0.31), (0.15, 0.28), (0, 0.27)], 5),
}

def pot(pc, kind, xf, glaze, seed, sides=16):
    prof, lip = POTS[kind]
    Rj = R_('pot%d' % seed)
    def key(i, k, lip=lip):
        if i > lip: return glaze
        if i == lip: return glaze if (k + seed) % 3 else 'clay'                  # glaze drips over the shoulder unevenly
        return 'clay'
    CV.lathe(pc, prof, xf @ Rz(Rj.uniform(0, 90)), sides=sides, keyfn=key, seed=seed * 1.3)

def p_crate():
    pc = FK.Piece('Crate')
    crate(pc, (0, 0, 0), 0.70, 0, seed=0)
    return pc

def p_crate_stack():
    pc = FK.Piece('CrateStack')
    crate(pc, (0.0, -0.36, 0.0), 0.70, 3, seed=1, planks=4, buried=True)
    crate(pc, (0.02, 0.36, 0.0), 0.70, -4, seed=2, planks=4, buried=True)
    crate(pc, (-0.01, -0.35, 0.70), 0.70, -7, seed=3, planks=4)
    crate(pc, (0.02, 0.38, 0.70), 0.50, 18, lid=False, seed=4, planks=3)           # the small open one, turned
    return pc

def p_jar_cluster():
    pc = FK.Piece('JarCluster')
    pot(pc, 'amphora', T(-0.18, -0.28, 0), 'glaze', 1)
    pot(pc, 'belly', T(-0.14, 0.20, 0), 'glaze2', 2)
    pot(pc, 'jar', T(0.22, -0.30, 0), 'glaze2', 3)
    pot(pc, 'crock', T(0.14, -0.04, 0), 'glaze', 4)
    # the lying jar: axis along +y, mouth toward +y, resting on its belly
    pot(pc, 'jar', T(0.24, 0.02, 0.16) @ Rz(12) @ Rx(-90), 'glaze', 5)
    return pc

def p_rubble():
    pc = FK.Piece('Rubble'); R = R_('rubble')
    def heap(x, y):
        return max(0.0, 0.24 * (1 - ((x + 0.2) / 0.62) ** 2 - (y / 0.78) ** 2))
    def drop(mat, size, x, y, cham, tilt=22, hmax=0.43):
        rot = Rz(R.uniform(0, 360)) @ Rx(R.uniform(-tilt, tilt)) @ Ry(R.uniform(-tilt, tilt))
        m3 = rot.to_3x3()
        hz = sum(abs(m3[2][j]) * size[j] / 2 for j in range(3))
        z = min(max(hz - 0.015, heap(x, y) + hz * 0.55), hmax - hz)
        z = max(z, hz - 0.015)
        FK.add_box(pc, mat, size, T(x, y, z) @ rot, cham=cham, uv_world=True, wob=cham * 0.4, seed=x * 7 + y)
    def spot(rx, ry):
        while True:
            x, y = R.uniform(-1, 1), R.uniform(-1, 1)
            if x * x + y * y < 1:
                return x * rx, y * ry
    for i in range(18):      # fallen vault stones
        x, y = spot(0.40, 0.55)
        sz = (R.uniform(0.26, 0.46), R.uniform(0.18, 0.30), R.uniform(0.12, 0.2))
        drop('vault', sz, x - 0.1, y, 0.025)
    for i in range(11):      # broken bricks, scattered wider
        x, y = spot(0.46, 0.66)
        sz = (R.uniform(0.12, 0.23), 0.105, 0.07)
        drop('brick', sz, x - 0.08, y, 0.01, tilt=30)
    for i in range(12):      # mortar crumbs
        x, y = spot(0.48, 0.68)
        s = R.uniform(0.03, 0.065)
        drop('mortar', (s * R.uniform(1, 1.6), s, s * 0.8), x, y, 0, tilt=40)
    return pc

def p_sack_pile():
    pc = FK.Piece('SackPile'); R = R_('sacks')
    prof = [(0, 0), (0.17, 0.0), (0.27, 0.05), (0.31, 0.16), (0.28, 0.30), (0.20, 0.38), (0.10, 0.42), (0.065, 0.46), (0.10, 0.50), (0.09, 0.55), (0.04, 0.57)]
    def key(i, k):
        return 'oak' if i in (7,) else 'lime'
    CV.SMOOTH.add('CV_Limewash')                                                   # soft cloth, not facets (only this process; the vault kit is not exported here)
    def sack(xf, sx, sy, sz, seed):
        S = Matrix.Diagonal((sx, sy, sz, 1))
        CV.lathe(pc, prof, xf @ S, sides=16, keyfn=key, seed=seed)
    sack(T(-0.12, -0.28, 0) @ Rz(R.uniform(0, 90)), 1.0, 0.9, 0.9, 1)
    sack(T(-0.10, 0.28, 0) @ Rz(R.uniform(0, 90)) @ Rx(6), 1.0, 1.0, 0.8, 2)
    sack(T(0.22, 0.0, 0.2) @ Rz(25) @ Rx(-80), 0.9, 0.9, 0.9, 3)                      # slumped over on its side, front
    return pc

PIECES = [('Crate', p_crate), ('CrateStack', p_crate_stack), ('JarCluster', p_jar_cluster), ('Rubble', p_rubble), ('SackPile', p_sack_pile)]

# ----------------------------------------------------------------------------- wear + object
def wear(name, p, n, mname):
    nz = noise.noise(p * 2.3); st = noise.noise(Vector((p.x * 3.0, p.y * 3.0, p.z * 0.6)))
    r = 0.05 + 0.08 * max(0.0, nz) + 0.4 * max(0.0, 1 - p.z / 0.25)                  # floor grime / damp at the foot
    g = 0.0; b = 0.0
    if mname in ('CV_Oak', 'CV_BarrelOak'):
        b = 0.5 * max(0.0, n.z) * (0.7 + 0.3 * nz) + 0.2 * max(0.0, 1 - abs(n.x))
        if name in ('Crate', 'CrateStack') and p.x > 0.2 and p.y < -0.15 and p.z < 0.3:
            r = max(r, 0.75)                                                       # the scuffed, dirty front corner
    if mname == 'CV_Iron':
        r = 0.25 + 0.2 * max(0.0, nz); b = 0.25 * max(0.0, n.z)
    if mname in ('CV_Clay', 'CV_GlazeAmber', 'CV_GlazeCream'):
        r = 0.12 + 0.3 * max(0.0, nz) + 0.3 * max(0.0, 1 - p.z / 0.3); b = 0.25 * max(0.0, n.z)
    if name == 'Rubble':
        r = max(r, 0.18 + 0.3 * max(0.0, st)); b = 0.3 * max(0.0, n.z) * max(0.0, nz + 0.3)
        g = 0.25 * max(0.0, nz * 2 - 0.4) * (p.x < 0)
    if name == 'SackPile':
        r = max(r, 0.2 + 0.3 * max(0.0, st)); b = 0.12 * max(0.0, n.z)
    return min(1.0, r), min(1.0, g), min(1.0, b)

def to_object(pc):
    """Recentre on the footprint, floor to z = 0, rotate into Blender axes (as the vault kit does), add the wear attribute."""
    bm = pc.bm
    cs = [v.co for v in bm.verts]
    lo = Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs))); hi = Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))
    bmesh.ops.translate(bm, vec=Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z)), verts=bm.verts[:])
    ext = (hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
    bmesh.ops.transform(bm, matrix=Rz(180), verts=bm.verts[:])
    me = bpy.data.meshes.new(pc.name)
    bm.normal_update(); bm.to_mesh(me); bm.free()
    for m in pc.mats:
        me.materials.append(m)
    for poly in me.polygons:
        if me.materials[poly.material_index].name in CV.SMOOTH:
            poly.use_smooth = True
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    mats = [m.name for m in me.materials]
    inv = lambda v: Vector((-v.x, -v.y, v.z))
    for poly in me.polygons:
        n = inv(poly.normal)
        for li in poly.loop_indices:
            p = inv(me.vertices[me.loops[li].vertex_index].co)
            attr.data[li].color = (*wear(pc.name, p, n, mats[poly.material_index]), 1.0)
    return ob, ext

def build():
    obs = {}; total = 0
    for name, fn in PIECES:
        ob, ext = to_object(fn())
        t = CV.tris(ob); total += t; obs[name] = ob
        print('  %-11s %5d tris  x %.2f z %.2f h %.2f (Unity m)  %d slots  %s' % (name, t, ext[0], ext[1], ext[2], len(ob.data.materials), ', '.join(m.name for m in ob.data.materials)))
    print('CellarClutter total tris: %d (cap 15000)' % total)
    return obs

def render_sheet(obs):
    sc = bpy.context.scene
    CV.setup_render(sc); sc.render.resolution_x = CV.RRES; sc.render.resolution_y = int(CV.RRES * 0.72)
    CV.world_color(sc, (0.30, 0.26, 0.22), 1.0)
    sun = CV.light(sc, 'SUN', 'Key', (0, 0, 10), (1.0, 0.82, 0.60), 3.0, angle=math.radians(4)); sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
    fill = CV.light(sc, 'SUN', 'Fill', (0, 0, 10), (0.9, 0.9, 1.0), 0.7, angle=math.radians(40)); fill.rotation_euler = (math.radians(60), 0, math.radians(150))
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, -0.005)); fl = bpy.context.object
    fl.data.materials.append(FK.M('flag') if False else FK.flat_mat('CV_proxyGround', (0.12, 0.10, 0.085), 0.9))
    # fronts face Blender -X; spread along Blender Y, two rows
    slots = {'Crate': (0, -2.2), 'CrateStack': (0, 0.0), 'JarCluster': (0, 2.0), 'Rubble': (-2.6, -1.4), 'SackPile': (-2.6, 1.0)}
    for n, (x, y) in slots.items():
        obs[n].location = (x, y, 0)
    cam_d = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cam_d); sc.collection.objects.link(cam)
    sc.camera = cam; cam_d.clip_start = 0.05; cam_d.angle = math.radians(46)
    CV.look(cam, Vector((5.6, 0.0, 3.6)) * Vector((-1, 1, 1)) + Vector((0, 0, 0)), Vector((-1.2, -0.2, 0.45)))
    # camera on the front side (Blender -X is the front); recompute explicit
    CV.look(cam, Vector((-6.2, -0.4, 3.4)), Vector((-1.1, -0.2, 0.45)))
    sc.render.filepath = os.path.join(FK.OUT, 'clutter_sheet.png')
    bpy.ops.render.render(write_still=True); print('rendered', sc.render.filepath)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    obs = build()
    if '--export' in argv:
        ed = os.path.abspath(FK.arg('--export-dir', os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Cellar', 'B5', 'CellarClutter')))
        os.makedirs(ed, exist_ok=True)
        for o in bpy.context.scene.objects:
            o.select_set(o.name in obs)
        bpy.ops.export_scene.fbx(filepath=os.path.join(ed, 'CellarClutter.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                                 mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR', path_mode='STRIP')
        print('exported', os.path.join(ed, 'CellarClutter.fbx'))
    if '--render' in argv:
        render_sheet(obs)
