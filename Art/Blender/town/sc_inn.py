"""Inn: Hollowmere's inn, the Drowsy Moon (ring 1, L class, frame + parts), the biggest building on the square.

Three storeys: a sandstone ground storey (quoins, a string course, a round-arched doorway with voussoirs and a keystone)
under two jettied half-timbered storeys in oxblood-painted timber on cream lime plaster, St Andrew's crosses under the
front windows, green shutters. The roof is a full hip with steep hip ends and deep front and back eaves on a bellcast kick,
two gabled dormers on the front slope and one on the back, and a louvred ridge vent with a crescent-moon weathervane;
so the roofline reads nothing like the bakery's clipped front gable or the approved eave-front facade. A tall rubble
chimney stack climbs the -X side through the hip eave, tied back with iron stays; the +X side has a cellar hatch with
barrels, the back a hooded back door and a water butt. Every side is detailed to mid distance (the building turns four
ways); the yard in front (stepping slabs to the porch step) turns with it. Reference read: the Gasthaus Loewen in Hagnau
(stone ground floor under red-framed upper storeys, green shutters) and the Kiedrich inn's corner sign bracket.

Parts (P28: the inn counts only when all are placed), each its own mesh with its pivot at its socket:
  Part_Door     round-arched planked green door with strap hinges, a ring pull and a barred spy hatch; without it the
                arched doorway stands open on a dark lined room.
  Part_Sign     the Drowsy Moon: an arch-topped blue board in an oxblood frame, a gilt sleeping crescent moon and stars
                above an oak tankard, on chains; without it the corner bracket (a scrolled wrought-iron arm out of the
                front-left corner post) hangs empty with two open hooks.
  Part_Porch    four oak posts, a beam with knee braces, rafters and a tiled lean-to roof, two benches against the wall;
                without it the flagstone porch deck shows four stone post pads with iron pins and an oak ledger with
                empty joist hangers under the jetty.
  Part_Lantern  a hanging iron lantern with glowing panes; without it the wall bracket by the door shows an empty hook.
SOCKET_Door / SOCKET_Sign / SOCKET_Porch / SOCKET_Lantern are empties parented to Frame. FOOTPRINT is a flat box over the
ground-storey body, the porch deck and the chimney (the collider and NPC-block shape), inside the L class's 10.5 x 10.5 m.

Built from the B4 facade kit helpers (sc_facade_kit: Piece, add_box, member, quad_grid, wall_panel, kit_shutters,
kit_flowerbox, materials) plus local copies of the bakery generator's lean helpers (sc_bakery.py: window, tube, merge,
face_xf, tile_courses, ridge_tiles, doorway) so this file stands on its own. T2: no unique bake; shared tileables with
metre UVs (2 m per tile, 2K maps): B1 plaster, sandstone, roof_tile, painted_wood, oak_timber, iron. Inn slots: IN_Timber
and IN_SignField are painted_wood in WearLit's paint mode (greyscale x paint colour, like the B4 neighbour shutters),
IN_Gilt is plaster tinted gold (the bakery's gilt), IN_Interior flat dark, IN_Flame the only emissive (lantern panes).
Wear in the linear float colour attribute "wear" like the facade kit (R grime/soot, G moss, B polish).

refs: Reference/hollowmere/inn/REFS.md, Reference/hollowmere/architecture/REFS.md (h_inn_03, h_inn_05, h_inn_06), the
approved corner captures, PHASE1 3.1/3.4.

Axes (Blender): z up, front -Y, metres. Origin = foundation centre at ground (the L foundation is 11 x 11 m). Export FBX
-Z forward, Y up: the front (Blender -Y) is local +Z in Unity, like the facade and the bakery, so the builder adds yaw 180.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_inn.py -- --out <scratch>
      [--views tq,tql,back,frame,close] [--rres 1280] [--samples 32] [--no-render] [--export] [--blend] [--tris]
  --export writes <out>/Inn.fbx (root Inn: Frame, Part_Door, Part_Sign, Part_Porch, Part_Lantern, SOCKET_* under Frame,
  FOOTPRINT). Renders go to <out>/inn_<view>.png; views starting 'frame' hide every part. --tris prints tris per piece.

Budget: under 50k tris (task cap), 2048 px maps (shared sets).
Status (2026-10-31, cloud): round 4, self-review 31.5/40 (silhouette 4.5, proportion 3.5, form 4, surface 3.5, colour 4,
wear 3.5, set 4.5, metal/emissive 4); first review 26, target 31 reached. 49,890 tris (Frame 45,132, Part_Door 1,150,
Part_Sign 1,488, Part_Porch 1,904, Part_Lantern 216); FOOTPRINT 9.96 x 8.97 m; everything inside x -5.49..5.50,
y -5.07..5.46. Sheet: Reference/hollowmere/inn/SHEET_inn_r4.jpg. Next visible gains: a wider, lower massing (the 4-way
turn and the chimney limit the width to the foundation), plaster value variation per panel, B9 weeds at the stone foot,
a warm lit taproom through the open doorway in the frame state, more tri room for shutters on every window.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT
argv = FK.argv
VIEWS = FK.arg('--views', 'tq,tql,back,frame,close').split(',')
RRES = int(FK.arg('--rres', '1280')); SAMPLES = int(FK.arg('--samples', '32'))
TILE = FK.TILE
TIM, PROUD = 0.26, FK.PROUD

# ----------------------------------------------------------------------------- dimensions (metres)
HW, XC = 4.50, 0.20                # body half width and centre x (the chimney side needs the room)
X0, X1 = XC - HW, XC + HW          # -4.30 .. 4.70
Y0, Y1 = -1.60, 4.55               # ground-storey front .. back
JET1, JET2 = 0.32, 0.30
YJ1 = Y0 - JET1; YJ2 = YJ1 - JET2  # first and second storey front planes
G1 = 3.10                          # top of the stone ground storey
B1A, B1B = 3.10, 3.40              # first-storey bressumer (front) / girt (sides, back)
B2A, B2B = 5.90, 6.20              # second-storey bressumer
PLATE0, PLATE1 = 8.50, 8.76
TANP, TANH = 1.0, 1.75             # front/back slope 45 deg, hip ends about 60 deg
OV = 0.85                          # front and back eave overhang; the hips overhang OV * TANP / TANH so every eave is level
OVH = OV * TANP / TANH
ZE = PLATE1 + 0.22 - OV * TANP     # eave edge
YF, YB = YJ2 - OV, Y1 + OV         # roof eave lines front / back
XL, XR = X0 - OVH, X1 + OVH
ZR = ZE + (YB - YF) / 2 * TANP     # ridge
YR = (YF + YB) / 2
RX0, RX1 = XL + (ZR - ZE) / TANH, XR - (ZR - ZE) / TANH   # ridge ends
KICK = 0.26
LEAN = (0.008, 0.007)              # settled: x per m above the ground storey, y (forward) per m above it
DOOR = (2.60, 3.90, 0.32, 2.10)    # front ground storey, local u (from X0): u0, u1, threshold, arch spring (r = half width)
DOOR_R = (DOOR[1] - DOOR[0]) / 2
DECK = (0.30, 6.00, 2.25, 0.32)    # porch deck u0, u1, depth out from Y0, top
POSTS_U = (0.52, 2.15, 4.35, 5.78)
LEDGER_Z = 2.98
BEAM_Z = 2.38
LANT_U, LANT_Z, LANT_OUT = 4.40, 2.62, 0.46
SIGN_Z, SIGN_ARM = 5.30, 1.50
SIGN_HOOKS = (0.42, 1.36)
SIGN_SCALE = 1.30                  # the board group is built at 1.16 x 0.84 m and scaled up: a big inn board
CHM = (1.05, 2.25)                 # chimney y range on the -X side
CH_TOP = 12.15

def _lin(h):
    return FK._lin(h)
TIMBER_TINT = (0.95, 0.44, 0.35)   # oak stained ox-blood red (multiply, like the facade's oak tint)
FIELD_PAINT = _lin('#2c3e52')

def ember_mat(name, base, emit, strength):
    if name in FK.MATS:
        return FK.MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*base, 1)
    b.inputs['Emission Color'].default_value = (*emit, 1)
    b.inputs['Emission Strength'].default_value = strength
    FK.MATS[name] = m
    return m

EXTRA = {
    'timber': lambda: FK.tile_mat('IN_Timber', 'B1_town/oak_timber', tint=TIMBER_TINT, normal=1.2),
    'field': lambda: FK.tile_mat('IN_SignField', 'B1_town/painted_wood', paint=FIELD_PAINT),
    'gilt': lambda: FK.tile_mat('IN_Gilt', 'B1_town/plaster', tint=(1.15, 0.80, 0.30), normal=0.3),
    'interior': lambda: FK.flat_mat('IN_Interior', (0.035, 0.025, 0.018), 0.9),
    'flame': lambda: ember_mat('IN_Flame', (0.40, 0.22, 0.06), (1.0, 0.62, 0.22), 4.0),
}
_orig_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)

def lean_member(pc, mat, a, b, w, d, cham=0.025, name='m', sag=0.0, wob=0.006, extend=0.0, up=None):
    """FK.member with a cut only on long sagging members (the 50k cap)."""
    a = Vector(a); b = Vector(b)
    axis = (b - a); L = axis.length + 2 * extend; axis.normalize()
    up = Vector(up) if up else Vector((0, -1, 0))
    side = axis.cross(up)
    if side.length < 1e-6:
        side = axis.cross(Vector((1, 0, 0)))
    side.normalize(); up = side.cross(axis).normalized()
    R = FK.rng('mem' + name)
    rot = Matrix.Rotation(math.radians(R.uniform(-1.4, 1.4)), 4, up)
    xf = Matrix.Translation((a + b) / 2) @ rot @ Matrix((side, up, axis)).transposed().to_4x4()
    cuts = max(1, int(L / 1.8)) if sag else 0
    def bend2(p, L=L):
        t = p.z / L * 2
        return Vector((sag * (1 - t * t), 0, 0))
    FK.add_box(pc, mat, (w, d, L), xf, cham=cham, cuts=(0, 0, cuts), long_axis=2, bend=bend2 if sag else None,
               wob=wob, seed=R.random() * 100)
FK.member = lean_member

# ----------------------------------------------------------------------------- piece plumbing (as sc_bakery.py)
def piece(name):
    return FK.Piece(name)

def merge(dst, src, xf=None):
    """Append piece src into dst (placed by xf), remapping material slots."""
    if '--tris' in argv:
        print('    %-14s %6d tris' % (src.name, sum(len(f.verts) - 2 for f in src.bm.faces)))
    if xf is not None:
        bmesh.ops.transform(src.bm, matrix=xf, verts=src.bm.verts[:])
        if xf.to_3x3().determinant() < 0:
            bmesh.ops.reverse_faces(src.bm, faces=src.bm.faces[:])
    remap = []
    for m in src.mats:
        if m not in dst.mats:
            dst.mats.append(m)
        remap.append(dst.mats.index(m))
    vmap = {v: dst.bm.verts.new(v.co) for v in src.bm.verts}
    for f in src.bm.faces:
        try:
            nf = dst.bm.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            continue
        nf.material_index = remap[f.material_index] if remap else 0
        for l, nl in zip(f.loops, nf.loops):
            nl[dst.uv].uv = l[src.uv].uv
    src.bm.free()

def face_xf(origin, n):
    """Wall frame: local x = along the wall (left to right seen from outside), local -y = outward n, z up."""
    n = Vector(n); t = Vector((0, 0, 1)).cross(n)
    return Matrix.Translation(origin) @ Matrix((t, -n, Vector((0, 0, 1)))).transposed().to_4x4()

F_FRONT = face_xf((X0, Y0, 0), (0, -1, 0))
F_FRONT1 = face_xf((X0, YJ1, 0), (0, -1, 0))
F_FRONT2 = face_xf((X0, YJ2, 0), (0, -1, 0))
F_RIGHT = face_xf((X1, Y0, 0), (1, 0, 0))
F_RIGHT1 = face_xf((X1, YJ1, 0), (1, 0, 0))
F_RIGHT2 = face_xf((X1, YJ2, 0), (1, 0, 0))
F_BACK = face_xf((X1, Y1, 0), (0, 1, 0))
F_LEFT = face_xf((X0, Y1, 0), (-1, 0, 0))
L_FB = X1 - X0
L_S0, L_S1, L_S2 = Y1 - Y0, Y1 - YJ1, Y1 - YJ2

def mem(pc, a, b, w=TIM, d=TIM, proud=PROUD, name='m', **kw):
    """Painted timber in wall-local coords (u, z pairs), standing `proud` in front of the plaster plane y=0."""
    yc = d / 2 - proud
    FK.member(pc, kw.pop('mat', 'timber'), (a[0], yc, a[1]), (b[0], yc, b[1]), w, d, name=name, **kw)

def prism(pc, mat, poly, y0, y1, uv_off=(0.0, 0.0)):
    """A slab from a convex-or-not (u, z) outline, between y0 and y1 (outline in order, any winding)."""
    bm = bmesh.new()
    a = [bm.verts.new((u, y0, z)) for u, z in poly]
    b = [bm.verts.new((u, y1, z)) for u, z in poly]
    n = len(poly)
    bm.faces.new(a); bm.faces.new(b[::-1])
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method='BEAUTY', ngon_method='EAR_CLIP')
    bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, uv_off); FK._merge(pc, bm, mat)

def bar(pc, mat, a, b, r, seed=0.0):
    """A square iron bar from a to b."""
    a = Vector(a); b = Vector(b); d = b - a
    FK.add_box(pc, mat, (r, r, d.length), Matrix.Translation((a + b) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4(),
               cham=0.0, seed=seed)

def window(pc, plane, x0, x1, z0, z1, k, sill_out=0.10, came=0.26, mat='timber', lattice=True, cheap=False):
    """Bakery window (casing, projecting sill, mullion, transom, deep-set glass, leaded lattice) in painted timber."""
    yc = plane + 0.06; c = 0.09
    for nm, a, b in (('l', (x0 + c / 2, yc, z0), (x0 + c / 2, yc, z1)), ('r', (x1 - c / 2, yc, z0), (x1 - c / 2, yc, z1)),
                     ('t', (x0, yc, z1 - c / 2), (x1, yc, z1 - c / 2))):
        FK.member(pc, mat, a, b, c, 0.12, name='iwc%d%s' % (k, nm), cham=0.0 if cheap else 0.012, wob=0.003)
    FK.add_box(pc, mat, (x1 - x0 + 0.22, 0.20, 0.07), Matrix.Translation(((x0 + x1) / 2, plane - sill_out + 0.06, z0 + 0.01)) @
               Matrix.Rotation(math.radians(-5), 4, 'X'), cham=0.015, long_axis=0, seed=k + 0.5)
    xm = (x0 + x1) / 2; zt = z0 + (z1 - z0) * 0.68
    FK.member(pc, mat, (xm, yc + 0.02, z0), (xm, yc + 0.02, z1), 0.07, 0.09, name='iwm%d' % k, cham=0.01, wob=0.002)
    FK.member(pc, mat, (x0, yc + 0.02, zt), (x1, yc + 0.02, zt), 0.06, 0.09, name='iwt%d' % k, cham=0.01, wob=0.002)
    gy = plane + 0.24
    q = FK.quad_grid
    q(pc, 'glass', [(x0, gy, z0), (x1, gy, z0), (x1, gy, z1), (x0, gy, z1)], 1, 1, uv_fn=lambda c: (c.x, c.z))
    rev = 'oak'
    q(pc, rev, [(x0, plane, z0), (x0, gy, z0), (x0, gy, z1), (x0, plane, z1)], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, rev, [(x1, gy, z0), (x1, plane, z0), (x1, plane, z1), (x1, gy, z1)], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, rev, [(x1, plane, z1), (x0, plane, z1), (x0, gy, z1), (x1, gy, z1)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    q(pc, rev, [(x0, plane, z0), (x1, plane, z0), (x1, gy, z0), (x0, gy, z0)], 1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    if not lattice or cheap:
        return
    for i in range(-8, 9):
        for sgn in (1, -1):
            zm = (z0 + z1) / 2 + i * came
            pts = [(x, zm + sgn * (x - xm) * 1.6) for x in (x0, x1)] + [(xm + (z - zm) / (1.6 * sgn), z) for z in (z0, z1)]
            pts = sorted(p for p in pts if x0 - 1e-6 <= p[0] <= x1 + 1e-6 and z0 - 1e-6 <= p[1] <= z1 + 1e-6)
            if len(pts) < 2 or (Vector(pts[0]) - Vector(pts[-1])).length < 0.08:
                continue
            a, b = pts[0], pts[-1]
            ln = (Vector(b) - Vector(a)).length; ang = math.atan2(b[1] - a[1], b[0] - a[0])
            FK.add_box(pc, 'iron', (ln, 0.010, 0.014), Matrix.Translation(((a[0] + b[0]) / 2, gy - 0.006, (a[1] + b[1]) / 2)) @
                       Matrix.Rotation(-ang, 4, 'Y'), cham=0.0, long_axis=0, seed=i)

def tube(pc, mat, pts, r, sides=6):
    """A round bar swept along pts."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rings = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        s = d.cross(Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))).normalized(); u = s.cross(d)
        rings.append([bm.verts.new(p + (s * math.cos(2 * math.pi * k / sides) + u * math.sin(2 * math.pi * k / sides)) * r) for k in range(sides)])
    for i in range(len(rings) - 1):
        for k in range(sides):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
            for l in f.loops:
                l[uvl].uv = (k / sides * 2 * math.pi * r / TILE, i * 0.05 / TILE)
    for ring in (rings[0], rings[-1][::-1]):
        bm.faces.new(ring[::-1])
    bm.normal_update(); FK._merge(pc, bm, mat)

def cyl(pc, mat, c, r1, r2, h, segs=12, caps=True, axis='Z', two=False):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=caps, segments=segs, radius1=r1, radius2=r2, depth=h)
    rot = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.pi / 2, 4, 'Y'), 'Y': Matrix.Rotation(math.pi / 2, 4, 'X')}[axis]
    bm.transform(Matrix.Translation(c) @ rot); bm.normal_update()
    if two:
        FK.two_sided(bm, 0.01)
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), 2 if axis == 'Z' else None, (c[0] % 1, c[1] % 1)); FK._merge(pc, bm, mat)

# ----------------------------------------------------------------------------- walls
def timber_wall(tag, L, za, zb, posts, rails, opens=(), braces=(), crosses=(), shutters=True, flower=None, came=0.30, cheap=False,
                shut_only=None):
    """One half-timbered storey in local coords. rails: (z centre, height); opens: (kind, u0, u1, z0, z1);
    kinds: win (window + shutters), win_plain (no shutters). crosses: (u0, u1, z0, z1) St Andrew's crosses."""
    pc = piece(tag)
    holes = [(o[1], o[2], o[3], o[4]) for o in opens]
    FK.wall_panel(pc, 'plaster', 0.0, 0.0, L, za, zb, holes=holes, bulge=0.014, cell=0.7)
    for i, u in enumerate(posts):
        mem(pc, (u, za), (u, zb), name='%sp%d' % (tag, i))
    for j, (zc, h) in enumerate(rails):
        mem(pc, (0.0, zc), (L, zc), d=TIM, w=h, proud=PROUD + 0.02, name='%sr%d' % (tag, j), sag=0.012 if L > 4 else 0.0)
    for k, (ua, za_, ub, zb_) in enumerate(braces):
        mem(pc, (ua, za_), (ub, zb_), w=0.20, proud=PROUD - 0.01, name='%sb%d' % (tag, k))
    for k, (u0, u1, z0, z1) in enumerate(crosses):
        mem(pc, (u0, z0), (u1, z1), w=0.15, proud=PROUD - 0.015, name='%sx%da' % (tag, k))
        mem(pc, (u0, z1), (u1, z0), w=0.15, proud=PROUD - 0.02, name='%sx%db' % (tag, k))
    wins = []
    for k, o in enumerate(opens):
        kind, u0, u1, z0, z1 = o
        window(pc, 0.0, u0, u1, z0, z1, hash(tag) % 97 + k, came=came, cheap=cheap)
        mem(pc, (u0 - 0.2, z0 - 0.11), (u1 + 0.2, z0 - 0.11), w=0.18, proud=PROUD + 0.01, name='%sws%d' % (tag, k))
        mem(pc, (u0 - 0.2, z1 + 0.11), (u1 + 0.2, z1 + 0.11), w=0.18, proud=PROUD + 0.01, name='%swh%d' % (tag, k))
        if kind == 'win' and shutters and (shut_only is None or k in shut_only):
            wins.append((u0, u1, z0, z1, 0.0))
    if wins:
        merge(pc, FK.kit_shutters(wins))
    if flower:
        a, b, zt = flower
        merge(pc, FK.kit_flowerbox(a, b, zt, 0.0))
    return pc

def stone_wall(tag, L, opens=(), arch=None, quoins=(True, True), zb=G1, cheap=False):
    """Sandstone ground storey: trowelled stone face, quoins at the corners, window surrounds (lintel, sill, jamb blocks),
    a moulded string course at the top and a base course. opens: (u0, u1, z0, z1) windows with oak casings.
    arch: (u0, u1, z0, spring) a round-arched opening with voussoirs and a keystone (the opening only)."""
    pc = piece(tag)
    holes = [tuple(o) for o in opens]
    if arch:
        a0, a1, az0, asp = arch
        holes.append((a0, a1, az0, asp + (a1 - a0) / 2))
    FK.wall_panel(pc, 'stone', 0.0, 0.0, L, 0.0, zb - 0.16, holes=holes, bulge=0.008, cell=0.9)
    R = FK.rng('sw' + tag)
    # base course and string course
    FK.add_box(pc, 'stone_dk', (L + 0.16, 0.16, 0.42), Matrix.Translation((L / 2, -0.06, 0.21)), cham=0.04, uv_world=False,
               long_axis=0, wob=0.006, seed=R.random() * 9, cuts=(int(L / 1.4), 0, 0))
    FK.add_box(pc, 'stone', (L + 0.22, 0.24, 0.20), Matrix.Translation((L / 2, -0.08, zb - 0.08)), cham=0.05, segs=2,
               long_axis=0, wob=0.004, seed=R.random() * 9)
    # quoins: alternating long and short blocks standing proud at each end
    for e, on in enumerate(quoins):
        if not on:
            continue
        z = 0.42; k = 0
        while z < zb - 0.3:
            h = R.uniform(0.32, 0.40); ln = 0.62 if k % 2 == 0 else 0.38
            u = ln / 2 - 0.02 if e == 0 else L - ln / 2 + 0.02
            FK.add_box(pc, 'stone', (ln, 0.10, h - 0.03), Matrix.Translation((u, -0.03, z + h / 2)) @
                       Matrix.Rotation(math.radians(R.uniform(-0.8, 0.8)), 4, 'Y'), cham=0.0 if cheap else 0.03, wob=0.006, seed=R.random() * 50)
            z += h; k += 1
    for k, (u0, u1, z0, z1) in enumerate(opens):
        window(pc, 0.0, u0, u1, z0, z1, hash(tag) % 53 + k, mat='oak', came=0.32, cheap=cheap)
        FK.add_box(pc, 'stone', (u1 - u0 + 0.50, 0.16, 0.26), Matrix.Translation(((u0 + u1) / 2, -0.04, z1 + 0.13)), cham=0.03,
                   wob=0.005, seed=k + 3.1, long_axis=0)
        FK.add_box(pc, 'stone', (u1 - u0 + 0.36, 0.20, 0.10), Matrix.Translation(((u0 + u1) / 2, -0.07, z0 - 0.05)) @
                   Matrix.Rotation(math.radians(-4), 4, 'X'), cham=0.025, wob=0.004, seed=k + 4.3, long_axis=0)
        for uu, s in ((u0, -1), (u1, 1)):
            z = z0
            j = 0
            while z < z1 - 0.1:
                h = min(z1 - z, R.uniform(0.34, 0.46)); w = 0.26 if j % 2 == 0 else 0.18
                FK.add_box(pc, 'stone', (w, 0.08, h - 0.025), Matrix.Translation((uu + s * w / 2, -0.02, z + h / 2)), cham=0.0,
                           wob=0.004, seed=R.random() * 50)
                z += h; j += 1
    if arch:
        a0, a1, az0, asp = arch
        r = (a1 - a0) / 2; uc = (a0 + a1) / 2
        # spandrels: fill the hole's top corners down to the arch
        for s in (-1, 1):
            pts = [(uc + s * r, asp + r)]
            for i in range(9):
                t = math.pi / 2 * i / 8
                pts.append((uc + s * r * math.cos(t), asp + r * math.sin(t)))
            poly = [pts[0]] + pts[1:][::-1] if s < 0 else pts
            bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
            c = bm.verts.new((uc + s * r, 0.0, asp + r))
            ring = [bm.verts.new((uu, 0.0, zz)) for uu, zz in pts[1:]]
            for i in range(len(ring) - 1):
                f = bm.faces.new((c, ring[i], ring[i + 1]) if s > 0 else (c, ring[i + 1], ring[i]))
            bm.normal_update()
            for f in bm.faces:
                if f.normal.y > 0:
                    f.normal_flip()
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.x / TILE, l.vert.co.z / TILE)
            FK._merge(pc, bm, 'stone')
        # voussoirs and keystone
        n = 11
        for i in range(n):
            t0 = math.pi * i / n; t1 = math.pi * (i + 1) / n
            key = i == n // 2
            ro = r + (0.40 if key else 0.30)
            pts = []
            for t in (t0 + 0.012, t1 - 0.012):
                pts.append((uc + math.cos(t) * r, asp + math.sin(t) * r))
            for t in (t1 - 0.012, t0 + 0.012):
                pts.append((uc + math.cos(t) * ro, asp + math.sin(t) * ro + (0.06 if key else 0.0)))
            prism(pc, 'stone', pts, -0.08 - (0.03 if key else 0.0), 0.02, uv_off=(i * 0.13, 0.0))
        # jamb blocks under the spring
        for uu, s in ((a0, -1), (a1, 1)):
            z = az0; j = 0
            while z < asp - 0.05:
                h = min(asp - z, 0.42); w = 0.30 if j % 2 == 0 else 0.22
                FK.add_box(pc, 'stone', (w, 0.10, h - 0.025), Matrix.Translation((uu + s * w / 2, -0.03, z + h / 2)), cham=0.025,
                           wob=0.004, seed=j + s * 7.0)
                z += h; j += 1
    return pc

def arched_doorway(pc, u0, u1, z0, spring, depth=0.42, inner=1.4):
    """Open arched doorway: stone reveals and soffit, a worn threshold and a dark lined room behind."""
    r = (u1 - u0) / 2; uc = (u0 + u1) / 2
    q = FK.quad_grid
    q(pc, 'stone', [(u0, 0, z0), (u0, depth, z0), (u0, depth, spring), (u0, 0, spring)], 1, 2, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'stone', [(u1, depth, z0), (u1, 0, z0), (u1, 0, spring), (u1, depth, spring)], 1, 2, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    def soffit(p, u, v):
        t = math.pi * (1 - u)
        return Vector((uc + math.cos(t) * r, depth * v, spring + math.sin(t) * r))
    q(pc, 'stone', [(0, 0, 0)] * 4, 10, 1, push=soffit, uv_fn=lambda c: (c.x / TILE + c.z / TILE, c.y / TILE))
    FK.add_box(pc, 'stone', (u1 - u0 + 0.20, depth + 0.08, 0.10), Matrix.Translation((uc, depth / 2 - 0.04, z0 - 0.05)),
               cham=0.02, wob=0.004, seed=u0)
    a, b = u0 - 0.5, u1 + 0.5; yb = depth + inner; zt = spring + r + 0.5
    q(pc, 'oak', [(u0, depth, z0), (u1, depth, z0), (u1, yb, z0), (u0, yb, z0)], 2, 3, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    q(pc, 'interior', [(a, yb, z0), (b, yb, z0), (b, yb, zt), (a, yb, zt)], 1, 1, uv_fn=lambda c: (c.x, c.z))
    q(pc, 'interior', [(u0, depth, z0), (a, yb, z0), (a, yb, zt), (u0, depth, zt)], 1, 1, uv_fn=lambda c: (c.y, c.z))
    q(pc, 'interior', [(b, yb, z0), (u1, depth, z0), (u1, depth, zt), (b, yb, zt)], 1, 1, uv_fn=lambda c: (c.y, c.z))
    q(pc, 'interior', [(u0, depth, zt), (a, yb, zt), (b, yb, zt), (u1, depth, zt)], 1, 1, uv_fn=lambda c: (c.x, c.y))
    # a warm hint of the taproom: a settle back and a hanging pot rail
    FK.add_box(pc, 'oak', (u1 - u0 + 0.6, 0.06, 1.1), Matrix.Translation((uc + 0.3, yb - 0.04, z0 + 0.55)), cham=0.01, seed=2.2)

def front_walls():
    pcs = []
    du0, du1, dz0, dsp = DOOR
    g = stone_wall('FrontG', L_FB, opens=[(0.80, 1.80, 1.05, 2.25), (4.85, 5.60, 1.05, 2.25), (6.55, 8.25, 1.00, 2.30)],
                   arch=(du0, du1, dz0, dsp))
    arched_doorway(g, du0, du1, dz0, dsp)
    porch_deck(g)
    lantern_bracket(g)
    pcs.append((g, F_FRONT))
    # first jetty: joist ends and soffit
    j = piece('Jetty1')
    for k in range(13):
        u = 0.2 + k * (L_FB - 0.4) / 12
        FK.add_box(j, 'timber', (0.15, JET1 + 0.12, 0.19), Matrix.Translation((u, -JET1 / 2 - 0.02, G1 + 0.03)),
                   cham=0.0, long_axis=1, wob=0.004, seed=k)
    FK.quad_grid(j, 'oak', [(0, 0.05, G1 - 0.06), (L_FB, 0.05, G1 - 0.06), (L_FB, -JET1, G1 - 0.06), (0, -JET1, G1 - 0.06)],
                 6, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    pcs.append((j, F_FRONT))
    WU = [(0.75, 1.65), (2.85, 3.75), (5.25, 6.15), (7.35, 8.25)]
    def posts_for(ws):
        p = [TIM / 2]
        for a, b in ws:
            p += [a - TIM / 2, b + TIM / 2]
        return p + [L_FB - TIM / 2]
    z1w = (4.15, 5.25)
    u1 = timber_wall('Front1', L_FB, B1A, B2A, posts_for(WU), [((B1A + B1B) / 2, B1B - B1A + 0.04)],
                     opens=[('win', a, b, z1w[0], z1w[1]) for a, b in WU],
                     braces=[(TIM, B1B, WU[0][0] - TIM, z1w[0] - 0.2), (L_FB - TIM, B1B, WU[3][1] + TIM, z1w[0] - 0.2),
                             (WU[1][1] + TIM, B1B + 0.05, (WU[1][1] + WU[2][0]) / 2, B2A - 0.05),
                             (WU[2][0] - TIM, B1B + 0.05, (WU[1][1] + WU[2][0]) / 2, B2A - 0.05)],
                     crosses=[(a + 0.02, b - 0.02, B1B + 0.04, z1w[0] - 0.24) for a, b in WU],
                     flower=(WU[1][0], WU[1][1], z1w[0] - 0.02), shut_only=(0, 2, 3), came=0.34)
    sign_bracket(u1)
    pcs.append((u1, F_FRONT1))
    j2 = piece('Jetty2')
    for k in range(13):
        u = 0.2 + k * (L_FB - 0.4) / 12
        FK.add_box(j2, 'timber', (0.14, JET2 + 0.12, 0.17), Matrix.Translation((u, -JET2 / 2 - 0.02, B2A + 0.02)),
                   cham=0.0, long_axis=1, wob=0.004, seed=k + 40)
    FK.quad_grid(j2, 'oak', [(0, 0.05, B2A - 0.07), (L_FB, 0.05, B2A - 0.07), (L_FB, -JET2, B2A - 0.07), (0, -JET2, B2A - 0.07)],
                 6, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    pcs.append((j2, F_FRONT1))
    z2w = (6.80, 7.80)
    u2 = timber_wall('Front2', L_FB, B2A, PLATE1, posts_for(WU), [((B2A + B2B) / 2, B2B - B2A + 0.04), ((PLATE0 + PLATE1) / 2, 0.26)],
                     opens=[('win', a, b, z2w[0], z2w[1]) for a, b in WU],
                     braces=[(TIM, B2B, WU[0][0] - TIM, PLATE0), (L_FB - TIM, B2B, WU[3][1] + TIM, PLATE0),
                             ((WU[1][1] + WU[2][0]) / 2 - 0.12, B2B, WU[1][1] + TIM, PLATE0),
                             ((WU[1][1] + WU[2][0]) / 2 + 0.12, B2B, WU[2][0] - TIM, PLATE0)],
                     shut_only=(0, 3), came=0.34)
    mem(u2, ((WU[1][1] + WU[2][0]) / 2, B2B), ((WU[1][1] + WU[2][0]) / 2, PLATE0), name='f2mid')
    pcs.append((u2, F_FRONT2))
    return pcs

def porch_deck(pc):
    """The porch's ground half (frame): a flagstone deck on a stone kerb, a step, four post pads with iron pins and an oak
    ledger with empty joist hangers under the jetty. Front-wall local coords (outward = -y)."""
    u0, u1, dp, top = DECK
    R = FK.rng('deck')
    FK.add_box(pc, 'stone_dk', (u1 - u0, dp, top - 0.06), Matrix.Translation(((u0 + u1) / 2, -dp / 2, (top - 0.06) / 2)), cham=0.03,
               uv_world=True, wob=0.006, seed=1.7)
    nx = 7; ny = 3
    for i in range(nx):
        for j in range(ny):
            a = u0 + (u1 - u0) * i / nx; b = u0 + (u1 - u0) * (i + 1) / nx
            if j % 2:
                a += (u1 - u0) / nx / 2; b += (u1 - u0) / nx / 2
                if i == nx - 1:
                    b = u1
                if i == 0:
                    FK.add_box(pc, 'stone', ((u1 - u0) / nx / 2 - 0.03, dp / ny - 0.03, 0.08),
                               Matrix.Translation((u0 + (u1 - u0) / nx / 4, -dp * (j + 0.5) / ny, top - 0.04)), cham=0.02,
                               uv_world=True, wob=0.008, seed=R.random() * 9)
            b = min(b, u1)
            FK.add_box(pc, 'stone', (b - a - 0.03, dp / ny - 0.03, 0.08 + R.uniform(-0.01, 0.012)),
                       Matrix.Translation(((a + b) / 2, -dp * (j + 0.5) / ny, top - 0.04)) @ Matrix.Rotation(R.uniform(-0.015, 0.015), 4, 'X'),
                       cham=0.025, uv_world=True, wob=0.008, seed=R.random() * 9)
    uc = (DOOR[0] + DOOR[1]) / 2
    FK.add_box(pc, 'stone', (1.9, 0.46, 0.17), Matrix.Translation((uc, -dp - 0.21, 0.085)) @ Matrix.Rotation(math.radians(0.8), 4, 'Y'),
               cham=0.035, segs=2, uv_world=True, wob=0.01, seed=8.0)
    for k, u in enumerate(POSTS_U):
        FK.add_box(pc, 'stone_dk', (0.36, 0.36, 0.12), Matrix.Translation((u, -dp + 0.22, top + 0.06)), cham=0.03, seed=k + 20.0)
        FK.add_box(pc, 'iron', (0.035, 0.035, 0.12), Matrix.Translation((u, -dp + 0.22, top + 0.18)), cham=0.0, seed=k)
    # ledger on the stone under the jetty, iron stirrup hangers waiting for the rafters
    FK.add_box(pc, 'oak', (u1 - u0 + 0.2, 0.14, 0.22), Matrix.Translation(((u0 + u1) / 2, -0.07, LEDGER_Z)), cham=0.02, long_axis=0,
               cuts=(3, 0, 0), wob=0.005, seed=3.3)
    for u in rafter_us():
        for s in (-1, 1):
            FK.add_box(pc, 'iron', (0.012, 0.16, 0.14), Matrix.Translation((u + s * 0.07, -0.22, LEDGER_Z - 0.06)), cham=0.0, seed=u + s)
        FK.add_box(pc, 'iron', (0.15, 0.16, 0.012), Matrix.Translation((u, -0.22, LEDGER_Z - 0.13)), cham=0.0, seed=u)

def rafter_us():
    u0, u1 = DECK[0], DECK[1]
    return [u0 + 0.15 + k * (u1 - u0 - 0.3) / 6 for k in range(7)]

def lantern_bracket(pc):
    """Wrought-iron arm by the door with a curl and an open hook for the lantern (front-wall local coords)."""
    u, z, o = LANT_U, LANT_Z, LANT_OUT
    FK.add_box(pc, 'iron', (0.06, 0.025, 0.34), Matrix.Translation((u, -0.012, z - 0.12)), cham=0.004, seed=31)
    bar(pc, 'iron', (u, -0.02, z), (u, -o, z), 0.035, seed=32)
    bar(pc, 'iron', (u, -0.02, z - 0.26), (u, -o * 0.62, z - 0.01), 0.025, seed=33)
    c = Vector((u, -0.17, z - 0.09)); pts = []
    for k in range(16):
        t = k / 15 * 2.2 * math.pi; r = 0.075 * (1 - t / (2.8 * math.pi))
        pts.append(c + Vector((0, -math.cos(t) * r, math.sin(t) * r)))
    for p, q in zip(pts, pts[1:]):
        bar(pc, 'iron', p, q, 0.016, seed=p.z)
    for k in range(7):   # open hook
        a0 = k / 6 * math.pi * 1.5
        bar(pc, 'iron', (u, -o + math.sin(a0) * 0.025, z - 0.03 - (1 - math.cos(a0)) * 0.025),
            (u, -o + math.sin(a0 + 0.5) * 0.025, z - 0.03 - (1 - math.cos(a0 + 0.5)) * 0.025), 0.014, seed=k + 40)

def sign_bracket(pc):
    """Scrolled wrought-iron arm out of the first storey's front-left corner post, 45 deg out over the street
    (front-wall local coords on the first storey: the corner is u = 0)."""
    sub = piece('SignBracket')
    z = SIGN_Z; A = SIGN_ARM
    # local frame of the bracket: arm along -y, plane y-z, origin at the corner post face
    FK.add_box(sub, 'iron', (0.07, 0.03, 0.62), Matrix.Translation((0, -0.015, z - 0.26)), cham=0.004, seed=1)
    for zz in (z - 0.05, z - 0.48):
        FK.add_box(sub, 'iron', (0.03, 0.04, 0.03), Matrix.Translation((0, -0.04, zz)), cham=0.005, seed=zz)
    bar(sub, 'iron', (0, -0.02, z), (0, -A, z), 0.05, seed=2)
    bar(sub, 'iron', (0, -0.02, z - 0.58), (0, -A * 0.70, z - 0.02), 0.04, seed=3)
    for cc, rr, turns, ph in (((0, -0.34, z - 0.20), 0.16, 2.4, 0.0), ((0, -0.86, z - 0.09), 0.08, 2.0, 1.0)):
        c = Vector(cc); pts = []
        for k in range(22):
            t = k / 21 * turns * math.pi
            r = rr * (1 - t / ((turns + 0.6) * math.pi))
            pts.append(c + Vector((0, -math.cos(t + ph) * r, math.sin(t + ph) * r)))
        for p, q in zip(pts, pts[1:]):
            bar(sub, 'iron', p, q, 0.024, seed=p.z + p.y)
    tip = Vector((0, -A, z))
    for k in range(8):   # curled finial
        a0 = k / 7 * math.pi * 1.6
        p = tip + Vector((0, -math.sin(a0) * 0.07, math.cos(a0) * 0.07 - 0.07))
        q = tip + Vector((0, -math.sin(a0 + 0.7) * 0.07, math.cos(a0 + 0.7) * 0.07 - 0.07))
        bar(sub, 'iron', p, q, 0.03, seed=k + 9)
    for hy in SIGN_HOOKS:   # open S-hooks waiting for the sign's chains
        for k in range(8):
            a0 = k / 7 * math.pi * 1.5
            p = Vector((0, -hy + math.sin(a0) * 0.028, z - 0.05 - (1 - math.cos(a0)) * 0.028))
            q = Vector((0, -hy + math.sin(a0 + 0.6) * 0.028, z - 0.05 - (1 - math.cos(a0 + 0.6)) * 0.028))
            bar(sub, 'iron', p, q, 0.016, seed=k + hy)
    merge(pc, sub, SIGN_XF_LOCAL)

# the bracket frame in first-storey wall-local coords: at the corner (u=0, standing on the corner post's face), turned so
# its arm points out diagonally (-u, -y): rotate the arm (local -y) 45 deg toward -u.
SIGN_XF_LOCAL = Matrix.Translation((0.0, -PROUD - 0.05, 0.0)) @ Matrix.Rotation(math.radians(-45), 4, 'Z')

def side_walls(tag, frames, Ls, wins_g, wins_1, wins_2, extra=None, quoins=(True, True), shutters1=True, cheap1=False):
    """A side or back: stone ground storey + two timber storeys (no jetty on these sides: the first and second storeys
    sit on the girt, their extra length is the front jetties' return)."""
    pcs = []
    Fg, F1, F2 = frames
    L0, L1, L2 = Ls
    g = stone_wall(tag + 'G', L0, opens=wins_g, quoins=quoins, cheap=True)
    if extra:
        extra(g)
    pcs.append((g, Fg))
    def posts_for(L, ws, mid=()):
        p = [TIM / 2]
        for a, b in ws:
            p += [a - TIM / 2, b + TIM / 2]
        return sorted(p + list(mid) + [L - TIM / 2])
    def braces_for(L, ws, za, zb):
        br = []
        ws = sorted(ws)
        edges = [TIM] + [x for a, b in ws for x in (a - TIM, b + TIM)] + [L - TIM]
        for i in range(0, len(edges) - 1, 2):
            a, b = edges[i], edges[i + 1]
            if b - a > 1.1:
                br.append((a, za + 0.05, (a + b) / 2, zb - 0.05)); br.append((b, za + 0.05, (a + b) / 2, zb - 0.05))
        return br
    z1w = (4.15, 5.20); z2w = (6.80, 7.75)
    u1 = timber_wall(tag + '1', L1, B1A, B2A, posts_for(L1, wins_1), [((B1A + B1B) / 2, B1B - B1A), ((B2A - 0.1), 0.22)],
                     opens=[('win' if shutters1 else 'win_plain', a, b, z1w[0], z1w[1]) for a, b in wins_1],
                     braces=braces_for(L1, wins_1, B1B, B2A - 0.2), came=0.45, cheap=cheap1)
    pcs.append((u1, F1))
    u2 = timber_wall(tag + '2', L2, B2A, PLATE1, posts_for(L2, wins_2), [((B2A + B2B) / 2, B2B - B2A), ((PLATE0 + PLATE1) / 2, 0.26)],
                     opens=[('win_plain', a, b, z2w[0], z2w[1]) for a, b in wins_2],
                     braces=braces_for(L2, wins_2, B2B, PLATE0), cheap=True)
    pcs.append((u2, F2))
    return pcs

def left_walls():
    # the chimney climbs this side at y CHM (local u = Y1 - y): keep the windows off it
    cu0, cu1 = Y1 - CHM[1], Y1 - CHM[0]
    def extra(g):
        woodstack(g, 0.5, cu0 - 0.45)
    return side_walls('Left', (F_LEFT, F_LEFT, F_LEFT), (L_S0, L_S1, L_S2),
                      [(cu1 + 0.6, cu1 + 1.45, 1.05, 2.20)], [(0.55, 1.45), (cu1 + 0.55, cu1 + 1.45)],
                      [(0.55, 1.45), (cu1 + 0.55, cu1 + 1.45)], extra=extra)

def right_walls():
    def extra(g):
        cellar_hatch(g, 3.4, 4.9)
    return side_walls('Right', (F_RIGHT, F_RIGHT1, F_RIGHT2), (L_S0, L_S1, L_S2),
                      [(1.0, 1.9, 1.05, 2.25)], [(1.15, 2.05), (4.30, 5.20)], [(1.45, 2.35), (4.55, 5.45)], extra=extra)

def back_walls():
    bd = (5.6, 6.6, 0.18, 2.30)
    def extra(g):
        back_door(g, bd)
    return side_walls('Back', (F_BACK, F_BACK, F_BACK), (L_FB, L_FB, L_FB),
                      [(1.2, 2.2, 1.05, 2.25), (3.4, 4.4, 1.05, 2.25)],
                      [(0.9, 1.8), (3.3, 4.2), (6.4, 7.3)], [(0.9, 1.8), (6.4, 7.3)], extra=extra, shutters1=False, cheap1=True)

def woodstack(pc, u0, u1):
    """Split logs stacked against the -X wall beside the chimney (wall-local)."""
    R = FK.rng('wood'); depth = 0.42; zt = 1.25; z = 0.06; row = 0
    while z < zt:
        r = R.uniform(0.085, 0.11)
        u = u0 + (r if row % 2 else r * 2)
        while u < u1 - r:
            rr = r * R.uniform(0.85, 1.12)
            bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=5, radius1=rr, radius2=rr, depth=depth - R.uniform(0, 0.06))
            bm.transform(Matrix.Translation((u, -depth / 2 - 0.10 + R.uniform(-0.03, 0.03), z + rr)) @ Matrix.Rotation(math.pi / 2, 4, 'X')
                         @ Matrix.Rotation(R.uniform(0, 1), 4, 'Z'))
            bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
            FK._uv_project(bm.faces, uvl, Matrix.Identity(4), 1, (R.random(), R.random()))
            ends = [f for f in bm.faces if abs(f.normal.y) > 0.9]
            bme = bm.copy()
            bmesh.ops.delete(bm, geom=ends, context='FACES'); FK._merge(pc, bm, 'oak')
            bmesh.ops.delete(bme, geom=[f for f in bme.faces if abs(f.normal.y) <= 0.9], context='FACES'); FK._merge(pc, bme, 'oak_lt')
            u += rr * 2.05
        z += r * 1.85; row += 1

def cellar_hatch(pc, u0, u1):
    """Sloping double cellar doors in a stone kerb at the wall foot, three barrels beside them (wall-local, +X side)."""
    R = FK.rng('hatch'); dp = 0.72; h0, h1 = 0.55, 0.20
    FK.add_box(pc, 'stone_dk', (u1 - u0 + 0.3, dp + 0.08, 0.16), Matrix.Translation(((u0 + u1) / 2, -dp / 2, 0.08)), cham=0.03,
               uv_world=True, seed=5.0)
    for s in (-1, 1):   # cheek stones
        FK.add_box(pc, 'stone', (0.15, dp, h0), Matrix.Translation(((u0 + u1) / 2 + s * ((u1 - u0) / 2 + 0.07), -dp / 2, h0 / 2)), cham=0.025,
                   uv_world=True, wob=0.006, seed=s + 6.0)
    ang = math.atan2(h0 - h1, dp)
    for leaf in range(2):
        a = u0 + (u1 - u0) * leaf / 2; b = u0 + (u1 - u0) * (leaf + 1) / 2
        for i in range(4):
            pw = (b - a) / 4
            FK.add_box(pc, 'door', (pw - 0.01, math.hypot(dp, h0 - h1), 0.05),
                       Matrix.Translation((a + pw * (i + 0.5), -dp / 2, (h0 + h1) / 2 + 0.03)) @ Matrix.Rotation(-ang, 4, 'X'),
                       cham=0.008, long_axis=1, wob=0.003, seed=R.random() * 20)
        for t in (0.2, 0.8):
            FK.add_box(pc, 'iron', ((b - a) * 0.7, 0.05, 0.012),
                       Matrix.Translation(((a + b) / 2 + (0.1 if leaf else -0.1), -dp * t, h0 - (h0 - h1) * t + 0.06)) @ Matrix.Rotation(-ang, 4, 'X'),
                       cham=0.0, seed=t + leaf)
    for k, (uu, yy, zz) in enumerate(((u1 + 0.70, -0.40, 0.0), (u1 + 1.40, -0.38, 0.0), (u1 + 1.05, -0.40, 0.82))):
        c = Vector((uu, yy, zz + 0.40))
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.30, radius2=0.30, depth=0.80)
        for v in bm.verts:
            v.co.x *= 1 + 0.12 * (1 - (v.co.z / 0.4) ** 2); v.co.y *= 1 + 0.12 * (1 - (v.co.z / 0.4) ** 2)
        if k == 2:
            bm.transform(Matrix.Translation(c) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
        else:
            bm.transform(Matrix.Translation(c))
        bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), 2, (k * 0.3, 0.1)); FK._merge(pc, bm, 'oak_lt')
        for dz in (-0.26, 0.26):
            p = c + (Vector((0, dz, 0)) if k == 2 else Vector((0, 0, dz)))
            cyl(pc, 'iron', p, 0.335, 0.335, 0.05, segs=12, caps=False, axis='Y' if k == 2 else 'Z', two=False)

def back_door(pc, bd):
    """Plain ledged oak back door, stone step, a little tiled hood on brackets and a water butt (wall-local)."""
    R = FK.rng('backdoor')
    for i in range(5):
        pw = (bd[1] - bd[0]) / 5
        FK.add_box(pc, 'oak_lt', (pw - 0.008, 0.05, bd[3] - bd[2]), Matrix.Translation((bd[0] + pw * (i + 0.5), 0.10, (bd[2] + bd[3]) / 2)),
                   cham=0.01, long_axis=2, wob=0.003, seed=R.random() * 30)
    for zz in (bd[2] + 0.4, bd[3] - 0.4):
        FK.add_box(pc, 'iron', (0.6, 0.012, 0.05), Matrix.Translation((bd[0] + 0.3, 0.07, zz)), cham=0.0, long_axis=0, seed=zz)
    FK.add_box(pc, 'stone', (bd[1] - bd[0] + 0.3, 0.36, 0.18), Matrix.Translation(((bd[0] + bd[1]) / 2, -0.16, 0.09)), cham=0.03,
               uv_world=True, wob=0.008, seed=9)
    q = FK.quad_grid
    q(pc, 'stone', [(bd[0], 0, bd[3]), (bd[1], 0, bd[3]), (bd[1], 0.12, bd[3]), (bd[0], 0.12, bd[3])], 1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    for uu, s in ((bd[0], 1), (bd[1], -1)):
        q(pc, 'stone', [(uu, 0, bd[2]), (uu, 0.12, bd[2]), (uu, 0.12, bd[3]), (uu, 0, bd[3])][::s], 1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    FK.add_box(pc, 'stone', (bd[1] - bd[0] + 0.5, 0.18, 0.26), Matrix.Translation(((bd[0] + bd[1]) / 2, -0.04, bd[3] + 0.13)), cham=0.03,
               wob=0.004, seed=9.5, long_axis=0)
    hc = (bd[0] + bd[1]) / 2; hw = (bd[1] - bd[0]) / 2 + 0.35; hz = bd[3] + 0.42
    for sgn in (-1, 1):
        a = Vector((hc, -0.02, hz + 0.42)); b = Vector((hc + sgn * hw, -0.02, hz))
        q(pc, 'roof', [a, b, b + Vector((0, -0.75, 0)), a + Vector((0, -0.75, 0))][::sgn], 3, 2, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
        q(pc, 'oak', [a - Vector((0, 0, 0.06)), b - Vector((0, 0, 0.06)), b + Vector((0, -0.75, -0.06)), a + Vector((0, -0.75, -0.06))][::-sgn],
          1, 1, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
        FK.member(pc, 'timber', (hc + sgn * (hw - 0.2), -0.03, hz - 0.55), (hc + sgn * (hw - 0.2), -0.62, hz - 0.03), 0.08, 0.08,
                  name='hood%d' % sgn, up=(1, 0, 0))
    FK.member(pc, 'timber', (hc, -0.79, hz + 0.40), (hc, -0.02, hz + 0.44), 0.10, 0.10, name='hoodridge', up=(1, 0, 0))
    cyl(pc, 'oak_lt', (bd[1] + 0.6, -0.42, 0.425), 0.30, 0.33, 0.85, segs=14)
    for zz in (0.15, 0.7):
        cyl(pc, 'iron', (bd[1] + 0.6, -0.42, zz), 0.30 + zz * 0.035 + 0.012, 0.30 + zz * 0.035 + 0.012, 0.05, segs=14, caps=False)

# ----------------------------------------------------------------------------- roof
def kickz(t):
    return KICK * max(0.0, 1 - t / 0.22) ** 2

def slope_pt(side, s, t, off=0.0):
    """Roof top surface. side: 'f', 'b' (main slopes, s = 0..1 along x) or 'l', 'r' (hips, s = 0..1 along y);
    t = 0 at the eave edge, 1 at the ridge. off pushes along the inward normal (down into the roof)."""
    z = ZE + (ZR - ZE) * t + kickz(t)
    xl = XL + (RX0 - XL) * t; xr = XR + (RX1 - XR) * t
    yf = YF + (YR - YF) * t; yb = YB + (YR - YB) * t
    w = 0.02 * noise.noise(Vector((s * 4.0, t * 3.0, ord(side) * 0.37))) * math.sin(math.pi * min(1.0, t))
    if side == 'f':
        p = Vector((xl + (xr - xl) * s, yf, z)); n = Vector((0, -TANP, 1)).normalized()
    elif side == 'b':
        p = Vector((xr + (xl - xr) * s, yb, z)); n = Vector((0, TANP, 1)).normalized()
    elif side == 'l':
        p = Vector((xl, yb + (yf - yb) * s, z)); n = Vector((-TANH, 0, 1)).normalized()
    else:
        p = Vector((xr, yf + (yb - yf) * s, z)); n = Vector((TANH, 0, 1)).normalized()
    return p + n * w - n * off, n

def roof():
    pc = piece('Roof')
    TH = 0.22
    for side, ns, nt in (('f', 20, 10), ('b', 20, 10), ('l', 10, 10), ('r', 10, 10)):
        def deck(p, u, v, side=side):
            return slope_pt(side, u, v * 0.999, 0.05)[0]
        # v = true arc length up the slope (the bellcast kick and steep hip ends change the pitch), u = metres along the eave
        arc = [(0.0, 0.0)]; prev = slope_pt(side, 0.5, 0.0, 0.05)[0]
        for k in range(1, 41):
            cur = slope_pt(side, 0.5, k / 40 * 0.999, 0.05)[0]
            arc.append((cur.z, arc[-1][1] + (cur - prev).length)); prev = cur
        arc[0] = (slope_pt(side, 0.5, 0.0, 0.05)[0].z, 0.0)
        def arc_v(z, arc=arc):
            for (z0, s0), (z1, s1) in zip(arc, arc[1:]):
                if z <= z1 or (z1, s1) == arc[-1]:
                    return s0 + (s1 - s0) * (z - z0) / (z1 - z0) if abs(z1 - z0) > 1e-9 else s0
        FK.quad_grid(pc, 'roof', [(0, 0, 0)] * 4, ns, nt, push=deck,
                     uv_fn=lambda c, side=side, arc_v=arc_v: ((c.x if side in 'fb' else c.y) / TILE, arc_v(c.z) / TILE))
        def under(p, u, v, side=side):
            return slope_pt(side, 1 - u, v * 0.999, TH)[0]
        FK.quad_grid(pc, 'oak', [(0, 0, 0)] * 4, ns // 2, 4, push=under,
                     uv_fn=lambda c, side=side: ((c.x if side in 'fb' else c.y) / TILE, c.z / TILE))
        n = slope_pt(side, 0.5, 0.0)[1]
        tile_courses(pc, lambda t, side=side: (slope_pt(side, 0.0, t)[0], slope_pt(side, 1.0, t)[0]), n,
                     11 if side in 'fb' else 8, 'c' + side, flip=False)
        # eave fascia and rafter tails
        a = slope_pt(side, 0.0, 0.0, 0.12)[0]; b = slope_pt(side, 1.0, 0.0, 0.12)[0]
        FK.member(pc, 'timber', a, b, 0.22, 0.07, name='fascia' + side, up=tuple(Vector((n.x, n.y, 0)).normalized() + Vector((0, 0, 0.4))))
        L = (b - a).length
        nr = max(3, int(L / 0.75))
        for k in range(nr):
            s = (k + 0.5) / nr
            if side in 'lr' and not 0.18 < s < 0.82:
                continue
            p0 = slope_pt(side, s, 0.02, TH + 0.08)[0]; p1 = slope_pt(side, s, 0.16, TH + 0.08)[0]
            FK.member(pc, 'timber', p0, p1, 0.12, 0.16, name='raft%s%d' % (side, k), up=tuple(-n))
    # hip ridges and the main ridge
    for (sa, ea) in ((('f', 0.0), ('l', 1.0)), (('f', 1.0), ('r', 0.0)), (('b', 0.0), ('r', 1.0)), (('b', 1.0), ('l', 0.0))):
        a = slope_pt(sa[0], sa[1], 0.0)[0]; b = slope_pt(sa[0], sa[1], 1.0)[0]
        ridge_tiles(pc, a + Vector((0, 0, 0.0)), b + Vector((0, 0, 0.02)), 9, 'hip%s%.0f' % sa)
    ridge_tiles(pc, Vector((RX0, YR, ZR + 0.08)), Vector((RX1, YR, ZR + 0.08)), 8, 'ridge')
    return pc

def tile_courses(pc, edge, n, nc, tag, flip=False, lift=0.0):
    """Courses of clay tile: tilted strips whose butts stand proud, jittered per run. edge(t) -> (a, b) at height t."""
    for c in range(nc):
        R = FK.rng('course%s%d' % (tag, c))
        t0, t1 = c / nc, min(0.99, (c + 1) / nc + 0.03)
        a0, b0 = edge(t0); a1, b1 = edge(t1)
        L = (b0 - a0).length
        if L < 0.15:
            continue
        nt = max(2, int(L / 0.50))
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        for i in range(nt):
            u0, u1 = i / nt, (i + 1) / nt
            j = R.uniform(-0.012, 0.012) if c else R.uniform(-0.008, 0.04)
            pts = []
            for u in (u0 + 0.002, u1 - 0.002):
                pb = a0.lerp(b0, u) + n * (0.045 + j + lift)
                pt = a1.lerp(b1, u) + n * lift
                pts.append((bm.verts.new(pb - n * 0.05), bm.verts.new(pb), bm.verts.new(pt)))
            a, b = pts
            for qd in ((a[1], b[1], b[2], a[2]), (a[0], b[0], b[1], a[1])):
                f = bm.faces.new(qd[::-1] if flip else qd)
                for l in f.loops:
                    co = l.vert.co
                    l[uvl].uv = ((co - a0).dot((b0 - a0).normalized()) / TILE, (c * 0.37 + (co - a0).dot((a1 - a0).normalized()) + (co - a0).dot(n)) / TILE)   # + n term: the butt riser has no extent along the slope, so it gets its height on V (no collapsed UVs)
        bm.normal_update()
        for f in bm.faces:
            if f.normal.dot(n) < -0.2:
                f.normal_flip()
        FK._merge(pc, bm, 'roof')

def ridge_tiles(pc, a, b, n, tag):
    ax = (b - a); L = ax.length; ax.normalize()
    side = ax.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize(); up = side.cross(ax).normalized()
    if up.z < 0:
        up = -up
    for i in range(n):
        R = FK.rng('rt%s%d' % (tag, i))
        c = a + ax * (L * (i + 0.5) / n)
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        segs = 7; r = 0.15; Lt = L / n + 0.05
        rows = []
        for e in (-1, 1):
            rr = r * (1.0 if e < 0 else 1.06)
            rows.append([bm.verts.new(c + ax * (e * Lt / 2) + side * (-rr * math.cos(math.pi * k / segs)) +
                                      up * (rr * math.sin(math.pi * k / segs) * 0.85 - 0.04)) for k in range(segs + 1)])
        for k in range(segs):
            f = bm.faces.new((rows[0][k], rows[1][k], rows[1][k + 1], rows[0][k + 1]))
            for l in f.loops:
                l[uvl].uv = ((l.vert.co - a).dot(ax) / TILE, k * r * 0.4 / TILE)
        bmesh.ops.rotate(bm, verts=bm.verts[:], cent=c, matrix=Matrix.Rotation(math.radians(R.uniform(-2.5, 2.5)), 3, ax))
        bm.normal_update(); bm.faces.ensure_lookup_table()
        if bm.faces[0].normal.dot(up) < 0:
            bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        FK.two_sided(bm)
        FK._merge(pc, bm, 'roof')

def main_z(y, side):
    """Height of the main front (side 'f') or back slope at y, ignoring the kick."""
    if side == 'f':
        return ZE + (y - YF) * TANP
    return ZE + (YB - y) * TANP

def dormer(tag, xc, side, td=0.30, hw=0.78, wh=1.30):
    """A gabled dormer on the main front or back slope: plaster cheeks, a timber-framed front with a casement and
    shutters, a steep tiled gable roof running back into the main slope, carved barge boards."""
    pc = piece(tag)
    sg = -1 if side == 'f' else 1
    y_edge = YF if side == 'f' else YB
    yd = y_edge + (YR - y_edge) * td            # dormer front plane
    zb = main_z(yd, side) + kickz(td)
    zw = zb + wh
    pitch = 1.25
    zap = zw + (hw + 0.05) * pitch
    def y_at(z):   # where the main slope reaches height z
        return YF + (z - ZE) / TANP if side == 'f' else YB - (z - ZE) / TANP
    yback = y_at(zap) - sg * 0.25
    # front wall (in a local wall frame: u along x, outward -y for the front)
    Fw = face_xf((xc - hw, yd, 0), (0, sg, 0)) if side == 'f' else face_xf((xc + hw, yd, 0), (0, sg, 0))
    w = piece(tag + 'W')
    L = 2 * hw
    def top(u):
        return zw + (hw - abs(u - hw)) * pitch
    zb0 = zb - 0.25
    wu0, wu1, wz0, wz1 = hw - 0.42, hw + 0.42, zb + 0.25, zb + 1.18
    FK.wall_panel(w, 'plaster', 0.0, 0.0, L, zb0, zw, holes=[(wu0, wu1, wz0, wz1)], bulge=0.01, cell=0.5)
    def gpush(p, u, v):
        uu = 0.02 + (L - 0.04) * u
        return Vector((uu, 0.0, zw + (max(top(uu), zw + 0.02) - zw) * v))
    FK.quad_grid(w, 'plaster', [(0, 0, 0)] * 4, 6, 2, push=gpush, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    for u in (TIM / 2, L - TIM / 2):
        mem(w, (u, zb0), (u, zw), name=tag + 'p%f' % u)
    mem(w, (0, zw + 0.08), (L, zw + 0.08), w=0.20, proud=PROUD + 0.02, name=tag + 'tie')
    mem(w, (0, zb0 + 0.1), (L, zb0 + 0.1), w=0.20, proud=PROUD + 0.02, name=tag + 'sill')
    mem(w, (hw, zw + 0.15), (hw, zap - 0.12), w=0.18, name=tag + 'king')
    window(w, 0.0, wu0, wu1, wz0, wz1, hash(tag) % 31, came=0.30)
    merge(pc, w, Fw)
    # cheeks: plaster triangles from the front wall back to the slope, both sides
    for sx in (-1, 1):
        x = xc + sx * hw
        y1 = y_at(zw)
        pts = [(yd, zb0), (yd, zw), (y1, zw)]
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        vs = [bm.verts.new((x, yy, zz)) for yy, zz in pts]
        f = bm.faces.new(vs)
        bm.normal_update()
        if f.normal.x * sx < 0:
            f.normal_flip()
        for l in f.loops:
            l[uvl].uv = (l.vert.co.y / TILE, l.vert.co.z / TILE)
        FK._merge(pc, bm, 'plaster')
    # roof: two slopes from the ridge (x = xc, z = zap) to the eaves, from the front verge back into the main slope
    ovf = 0.30; ove = 0.22
    yfv = yd + sg * ovf
    for sx in (-1, 1):
        ze_ = zw - ove * pitch + 0.10
        xe = xc + sx * (hw + ove)
        def dpt(u, v, off=0.0, sx=sx, xe=xe, ze_=ze_):
            y = yfv + (yback - yfv) * u
            p = Vector((xe + (xc - xe) * v, y, ze_ + (zap + 0.12 - ze_) * v))
            nrm = Vector((sx * pitch, 0, 1)).normalized()
            return p - nrm * off
        nrm = Vector((sx * pitch, 0, 1)).normalized()
        FK.quad_grid(pc, 'roof', [(0, 0, 0)] * 4, 4, 3, push=lambda p, u, v, d=dpt: d(u, v),
                     uv_fn=lambda c: (c.y / TILE, c.z * 1.6 / TILE))
        FK.quad_grid(pc, 'oak', [(0, 0, 0)] * 4, 2, 1, push=lambda p, u, v, d=dpt: d(1 - u, v, 0.14),
                     uv_fn=lambda c: (c.y / TILE, c.z / TILE))
        tile_courses(pc, lambda t, d=dpt: (d(0.0, t), d(1.0, t)), nrm, 3, tag + 'tc%d' % sx)
        # carved barge board on the front verge
        a = dpt(0.0, 0.0, 0.08) + Vector((0, sg * 0.03, 0)); b = dpt(0.0, 1.0, 0.08) + Vector((0, sg * 0.03, 0))
        FK.member(pc, 'timber', a, b, 0.26, 0.07, name=tag + 'barge%d' % sx, up=(0, sg, 0), wob=0.003)
    ridge_tiles(pc, Vector((xc, yfv, zap + 0.16)), Vector((xc, yback, zap + 0.16)), 4, tag + 'r')
    # finial at the gable apex
    cyl(pc, 'timber', (xc, yfv + sg * 0.04, zap + 0.32), 0.05, 0.02, 0.42, segs=6)
    return pc

def ridge_vent():
    """A louvred ventilator on the ridge with a little pyramid cap and a crescent-moon weathervane."""
    pc = piece('Vent')
    xc, y = XC + 0.2, YR
    zb = ZR - 0.25; h = 0.95; w = 0.95
    FK.add_box(pc, 'timber', (w + 0.08, w + 0.08, 0.14), Matrix.Translation((xc, y, zb + 0.07)), cham=0.02, seed=1)
    for k in range(4):
        a = k * math.pi / 2
        d = Vector((math.cos(a), math.sin(a), 0))
        for j in range(4):   # louvre boards
            z = zb + 0.24 + j * 0.17
            FK.add_box(pc, 'oak', (w - 0.1, 0.03, 0.15), Matrix.Translation(Vector((xc, y, z)) + d * (w / 2 - 0.05)) @
                       Matrix.Rotation(a + math.pi / 2, 4, 'Z') @ Matrix.Rotation(math.radians(38), 4, 'X'), cham=0.0, long_axis=0, seed=k + j * 0.1)
        FK.add_box(pc, 'timber', (0.10, 0.10, h), Matrix.Translation(Vector((xc, y, zb + h / 2)) + Vector((math.cos(a + 0.785), math.sin(a + 0.785), 0)) * (w * 0.68)),
                   cham=0.015, seed=k + 5)
    FK.add_box(pc, 'interior', (w - 0.2, w - 0.2, h - 0.15), Matrix.Translation((xc, y, zb + h / 2 + 0.05)), cham=0.0, seed=9)
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=(w + 0.36) * 0.7071, radius2=0.03, depth=0.75)
    bm.transform(Matrix.Translation((xc, y, zb + h + 0.37)) @ Matrix.Rotation(math.pi / 4, 4, 'Z')); bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'roof')
    zt = zb + h + 0.72
    bar(pc, 'iron', (xc, y, zt), (xc, y, zt + 1.05), 0.03, seed=3)
    for k, (dx, dy) in enumerate(((0.32, 0), (-0.32, 0), (0, 0.32), (0, -0.32))):
        bar(pc, 'iron', (xc, y, zt + 0.42), (xc + dx, y + dy, zt + 0.42), 0.018, seed=k)
    # crescent moon vane (plane x-z), with an arrow tail
    pts = []
    for i in range(13):
        t = -math.pi * 0.8 + math.pi * 1.6 * i / 12
        pts.append((math.sin(t) * 0.30, math.cos(t) * 0.30))
    for i in range(12, -1, -1):
        t = -math.pi * 0.8 + math.pi * 1.6 * i / 12
        pts.append((math.sin(t) * 0.22 + 0.10, math.cos(t) * 0.24 + 0.0))
    sub = piece('Vane')
    prism(sub, 'gilt', [(-p[1], p[0]) for p in pts], -0.012, 0.012)
    merge(pc, sub, Matrix.Translation((xc + 0.05, y, zt + 0.80)) @ Matrix.Rotation(math.pi / 2, 4, 'Z'))
    bar(pc, 'iron', (xc - 0.6, y, zt + 0.80), (xc + 0.0, y, zt + 0.80), 0.02, seed=7)
    prism_xz = piece('Tail')
    prism(prism_xz, 'iron', [(-0.22, -0.1), (0.0, 0.0), (-0.22, 0.1), (-0.15, 0.0)], -0.01, 0.01)
    merge(pc, prism_xz, Matrix.Translation((xc - 0.45, y, zt + 0.80)))
    return pc

# ----------------------------------------------------------------------------- chimney
def chimney():
    """Rubble stone stack on the -X side: a wide breast to the first storey, a weathered set-off, a tall shaft through
    the hip eave, a corbelled cap and two pots; iron stays back to the roof. It leans out a touch."""
    pc = piece('Chimney')
    y0, y1 = CHM
    yc = (y0 + y1) / 2
    xin = X0
    def sbox(size, c, seed, mat='stone_dk', cuts=(0, 0, 0)):
        FK.add_box(pc, mat, size, Matrix.Translation(c), cham=0.04, cuts=cuts, uv_world=True, wob=0.014, seed=seed)
    d0 = 0.78; sh = 5.6
    sbox((d0, y1 - y0 + 0.30, sh), Vector((xin - d0 / 2, yc, sh / 2)), 1, cuts=(1, 1, 5))
    # quoin-ish courses on the breast edges for a rubble read
    R = FK.rng('chq')
    z = 0.3
    while z < sh - 0.3:
        h = R.uniform(0.3, 0.42)
        for s in (-1, 1):
            FK.add_box(pc, 'stone', (0.34 if int(z * 10) % 2 else 0.22, 0.30, h - 0.03),
                       Matrix.Translation((xin - d0 + 0.10, yc + s * ((y1 - y0 + 0.30) / 2 - 0.12), z + h / 2)), cham=0.0, wob=0.008,
                       uv_world=True, seed=z + s)
        z += h
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=0.7071, radius2=0.7071 * 0.72, depth=0.36)
    bm.transform(Matrix.Translation((xin - d0 / 2, yc, sh + 0.18)) @ Matrix.Diagonal((d0 + 0.02, y1 - y0 + 0.32, 1, 1)) @
                 Matrix.Rotation(math.pi / 4, 4, 'Z'))
    bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'stone')
    d1 = 0.62; w1 = y1 - y0 - 0.12
    top = CH_TOP
    sbox((d1, w1, top - 0.45 - (sh + 0.36)), Vector((xin - d0 / 2 - 0.02, yc, (sh + 0.36 + top - 0.45) / 2)), 2, cuts=(1, 1, 6))
    for k, (dz, grow) in enumerate(((0.0, 0.08), (0.14, 0.16), (0.29, 0.11))):
        sbox((d1 + grow * 2, w1 + grow * 2, 0.14), Vector((xin - d0 / 2 - 0.02, yc, top - 0.45 + dz + 0.07)), 3 + k, mat='stone')
    FK.add_box(pc, 'stone', (d1 + 0.24, w1 + 0.24, 0.07), Matrix.Translation((xin - d0 / 2 - 0.02, yc, top - 0.02)), cham=0.02,
               uv_world=True, seed=7)
    for k, dy in enumerate((-0.22, 0.24)):
        c = (xin - d0 / 2 - 0.02, yc + dy, top + 0.22 + 0.04 * k)
        cyl(pc, 'stone_dk', c, 0.13, 0.10, 0.46 + 0.08 * k, segs=10, caps=False, two=True)
        bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, segments=10, radius=0.095)
        bm.transform(Matrix.Translation((c[0], c[1], top + 0.16))); uvl = bm.loops.layers.uv.new('UVMap')
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'interior')
    rot = Matrix.Translation((xin, yc, sh)) @ Matrix.Rotation(math.radians(1.3), 4, 'Y') @ Matrix.Translation((-xin, -yc, -sh))
    bmesh.ops.transform(pc.bm, matrix=rot, verts=[v for v in pc.bm.verts if v.co.z > sh])
    # iron stays to the hip slope
    for z in (9.9, 11.0):
        xs = xin - d0 / 2 - 0.02 + d1 / 2 - math.tan(math.radians(1.3)) * (z - sh)
        xr = XL + (z - ZE) / TANH + 0.05
        for dy in (-w1 / 2 + 0.06, w1 / 2 - 0.06):
            bar(pc, 'iron', (xs, yc + dy, z), (xr, yc + dy * 0.6, z - 0.08), 0.04, seed=z + dy)
        FK.add_box(pc, 'iron', (0.04, w1 + 0.08, 0.07), Matrix.Translation((xs - 0.02, yc, z)), cham=0.0, seed=z + 1)
    return pc

def yard():
    pc = piece('Yard')
    R = FK.rng('yard')
    xc = X0 + (DOOR[0] + DOOR[1]) / 2
    y = Y0 - DECK[2] - 0.95
    k = 0
    while y > -5.15:
        c = Vector((xc + R.uniform(-0.12, 0.12) + (0.1 if k % 2 else -0.1), y, 0.025))
        FK.add_box(pc, 'stone', (0.72 + R.uniform(-0.08, 0.08), 0.50 + R.uniform(-0.05, 0.05), 0.07), Matrix.Translation(c) @
                   Matrix.Rotation(R.uniform(-0.25, 0.25), 4, 'Z'), cham=0.03, segs=2, uv_world=True, wob=0.012, seed=k + 50)
        y -= 0.78; k += 1
    # a mounting block and a hitching ring post by the porch end
    FK.add_box(pc, 'stone', (0.7, 0.55, 0.36), Matrix.Translation((X0 + DECK[1] + 0.85, Y0 - 1.5, 0.18)), cham=0.04, segs=2, uv_world=True,
               wob=0.01, seed=60)
    FK.add_box(pc, 'stone', (0.7, 0.30, 0.18), Matrix.Translation((X0 + DECK[1] + 0.85, Y0 - 1.95, 0.09)), cham=0.03, uv_world=True,
               wob=0.01, seed=61)
    return pc

# ----------------------------------------------------------------------------- parts
def part_door():
    """Round-arched planked door (front-wall local; pivot at the threshold centre)."""
    pc = piece('Part_Door')
    u0, u1, z0, sp = DOOR
    u0 += 0.01; u1 -= 0.01
    r = (u1 - u0) / 2; uc = (u0 + u1) / 2
    yd = 0.12; n = 6; pw = (u1 - u0) / n
    R = FK.rng('idoor')
    def top(u):
        return sp + math.sqrt(max(0.0, r * r - (u - uc) ** 2)) - 0.01
    for i in range(n):
        a = u0 + pw * i + 0.004; b = u0 + pw * (i + 1) - 0.004
        poly = [(a, z0 + 0.005), (b, z0 + 0.005)]
        for k in range(4):
            uu = b - (b - a) * k / 3
            poly.append((uu, top(uu)))
        mat = 'oak_lt' if i == 4 else 'door'
        prism(pc, mat, poly, yd - 0.03 + R.uniform(-0.004, 0.004), yd + 0.03, uv_off=(R.random(), R.random()))
    FK.quad_grid(pc, 'oak', [(u0, yd + 0.035, z0), (u1, yd + 0.035, z0), (u1, yd + 0.035, sp), (u0, yd + 0.035, sp)], 1, 1,
                 uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    for zz in (z0 + 0.42, sp - 0.20):   # strap hinges on the left edge, curled tips
        Ls = 0.95
        FK.add_box(pc, 'iron', (Ls, 0.014, 0.065), Matrix.Translation((u0 + Ls / 2 - 0.02, yd - 0.037, zz)), cham=0.004, cuts=(5, 0, 0),
                   long_axis=0, seed=zz, bend=lambda p, Ls=Ls: Vector((0, 0, 0.010 * math.sin((p.x / Ls + 0.5) * math.pi * 3))))
        tip = Vector((u0 + Ls - 0.02, yd - 0.037, zz))
        for j in range(6):
            a0 = j * 0.9; seg = Vector((math.cos(a0), 0, math.sin(a0))) * 0.05
            FK.add_box(pc, 'iron', (0.06, 0.014, 0.024), Matrix.Translation(tip + Vector((0.04 + seg.x * 0.6, 0, seg.z * 0.6))) @
                       Matrix.Rotation(-a0 - math.pi / 2, 4, 'Y'), cham=0.0, long_axis=0, seed=j + zz)
        for bx in (0.08, 0.32, 0.56, 0.8):
            FK.add_box(pc, 'iron', (0.028, 0.02, 0.028), Matrix.Translation((u0 + bx, yd - 0.048, zz)), cham=0.006, seed=bx)
    # spy hatch: a small barred square at eye height
    hz = 1.62; hw = 0.14
    FK.add_box(pc, 'interior', (hw * 2, 0.01, hw * 2), Matrix.Translation((uc, yd - 0.035, hz)), cham=0.0, seed=1)
    for s in (-1, 1):
        FK.add_box(pc, 'iron', (hw * 2 + 0.06, 0.02, 0.035), Matrix.Translation((uc, yd - 0.045, hz + s * (hw + 0.015))), cham=0.0, seed=s)
        FK.add_box(pc, 'iron', (0.035, 0.02, hw * 2 + 0.06), Matrix.Translation((uc + s * (hw + 0.015), yd - 0.045, hz)), cham=0.0, seed=s + 2)
    for s in (-1, 0, 1):
        FK.add_box(pc, 'iron', (0.018, 0.03, hw * 2), Matrix.Translation((uc + s * 0.08, yd - 0.055, hz)), cham=0.0, seed=s + 4)
    hx = u1 - 0.18; hzr = 1.10
    FK.add_box(pc, 'iron', (0.11, 0.016, 0.19), Matrix.Translation((hx, yd - 0.036, hzr)), cham=0.004, seed=9)
    for j in range(12):
        a0 = j / 12 * 2 * math.pi
        p = Vector((hx + math.sin(a0) * 0.07, yd - 0.06, hzr - 0.10 + math.cos(a0) * 0.07))
        FK.add_box(pc, 'iron', (0.042, 0.016, 0.016), Matrix.Translation(p) @ Matrix.Rotation(-a0, 4, 'Y'), cham=0.0, seed=j)
    return pc, F_FRONT, Vector((uc, 0.0, z0))

def part_porch():
    """Posts, beam, knee braces, rafters, a boarded and tiled lean-to roof and two benches (front-wall local coords;
    pivot at the deck's front edge centre)."""
    pc = piece('Part_Porch')
    u0, u1, dp, top = DECK
    py = -dp + 0.22
    R = FK.rng('porch')
    for k, u in enumerate(POSTS_U):
        FK.add_box(pc, 'oak', (0.22, 0.22, BEAM_Z - 0.13 - (top + 0.12)), Matrix.Translation((u, py, (BEAM_Z - 0.13 + top + 0.12) / 2)) @
                   Matrix.Rotation(math.radians(R.uniform(-0.8, 0.8)), 4, 'Y'), cham=0.035, segs=1, long_axis=2, wob=0.006, seed=k + 1.3)
        for s in (-1, 1):   # knee braces
            if (k == 0 and s < 0) or (k == len(POSTS_U) - 1 and s > 0):
                continue
            FK.member(pc, 'oak', (u + s * 0.08, py, BEAM_Z - 0.75), (u + s * 0.58, py, BEAM_Z - 0.12), 0.12, 0.12, name='kb%d%d' % (k, s))
    FK.member(pc, 'oak', (u0 - 0.10, py, BEAM_Z), (u1 + 0.10, py, BEAM_Z), 0.26, 0.22, name='pbeam', sag=0.015, up=(0, 0, 1))
    zr0, zr1 = LEDGER_Z + 0.10, BEAM_Z + 0.16
    ang = math.atan2(zr0 - zr1, -py)
    for k, u in enumerate(rafter_us()):
        a = Vector((u, -0.14, zr0)); b = Vector((u, py - 0.42, zr1 - 0.42 * math.tan(ang)))
        FK.member(pc, 'oak', a, b, 0.10, 0.14, name='praf%d' % k, up=(0, -math.sin(ang), math.cos(ang)))
    # boards, then tile courses
    yb0, yb1 = -0.05, py - 0.55
    zb0, zb1 = zr0 + 0.10, zr1 + 0.10 - 0.55 * math.tan(ang)
    nrm = Vector((0, -math.sin(ang), math.cos(ang)))
    FK.quad_grid(pc, 'oak', [(u0 - 0.2, yb0, zb0 - 0.03), (u0 - 0.2, yb1, zb1 - 0.03), (u1 + 0.2, yb1, zb1 - 0.03), (u1 + 0.2, yb0, zb0 - 0.03)],
                 3, 2, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    FK.quad_grid(pc, 'roof', [(u0 - 0.2, yb1, zb1), (u0 - 0.2, yb0, zb0), (u1 + 0.2, yb0, zb0), (u1 + 0.2, yb1, zb1)],
                 8, 3, uv_fn=lambda c: (c.x / TILE, c.y * 1.4 / TILE))
    def edge(t):
        y = yb1 + (yb0 - yb1) * t; z = zb1 + (zb0 - zb1) * t
        return Vector((u0 - 0.2, y, z)), Vector((u1 + 0.2, y, z))
    tile_courses(pc, edge, nrm, 6, 'porch')
    for s, u in ((-1, u0 - 0.2), (1, u1 + 0.2)):   # verge boards closing the ends
        FK.member(pc, 'timber', (u, yb0, zb0 + 0.02), (u, yb1, zb1 + 0.02), 0.20, 0.05, name='pv%d' % s, up=(s, 0, 0))
    FK.member(pc, 'timber', (u0 - 0.2, yb1 - 0.01, zb1 - 0.03), (u1 + 0.2, yb1 - 0.01, zb1 - 0.03), 0.18, 0.05, name='pfasc', up=(0, -1, 0.2))
    # two benches against the wall, either side of the door
    for k, (a, b) in enumerate(((u0 + 0.25, DOOR[0] - 0.30), (DOOR[1] + 0.85, u1 - 0.95))):
        sy = -0.38; sz = top + 0.46
        FK.add_box(pc, 'oak_lt', (b - a, 0.40, 0.07), Matrix.Translation(((a + b) / 2, sy, sz)), cham=0.015, long_axis=0, wob=0.004, seed=k + 0.2)
        for uu in (a + 0.12, b - 0.12):
            FK.add_box(pc, 'oak', (0.08, 0.34, sz - top - 0.035), Matrix.Translation((uu, sy, (sz + top - 0.035) / 2)), cham=0.012, seed=uu)
        FK.add_box(pc, 'oak_lt', (b - a, 0.05, 0.16), Matrix.Translation(((a + b) / 2, -0.12, sz + 0.42)), cham=0.012, long_axis=0, seed=k + 0.6)
        for uu in (a + 0.12, b - 0.12):
            FK.add_box(pc, 'oak', (0.07, 0.06, 0.55), Matrix.Translation((uu, -0.13, sz + 0.24)) @ Matrix.Rotation(math.radians(-8), 4, 'X'),
                       cham=0.01, seed=uu + 1)
    # a tankard and a folded cloth on the left bench (a lived-in touch)
    cyl(pc, 'oak_lt', ((u0 + DOOR[0]) / 2 - 0.25, -0.38, top + 0.58), 0.055, 0.05, 0.16, segs=10)
    cyl(pc, 'iron', ((u0 + DOOR[0]) / 2 - 0.25, -0.38, top + 0.62), 0.058, 0.058, 0.02, segs=10, caps=False)
    return pc, F_FRONT, Vector(((u0 + u1) / 2, -dp, top))

def part_lantern():
    """Hanging iron lantern with glowing panes (front-wall local; pivot at the bracket hook)."""
    pc = piece('Part_Lantern')
    hk = Vector((LANT_U, -LANT_OUT, LANT_Z - 0.06))
    for k in range(3):
        FK.add_box(pc, 'iron', (0.014, 0.034, 0.05), Matrix.Translation(hk + Vector((0, 0, -0.02 - k * 0.045))) @
                   Matrix.Rotation((k % 2) * 1.57, 4, 'Z'), cham=0.0, seed=k)
    c = hk + Vector((0, 0, -0.42))
    w = 0.13; h = 0.30
    cyl(pc, 'iron', c + Vector((0, 0, h / 2 + 0.11)), w * 1.35, 0.025, 0.20, segs=4)       # cap
    cyl(pc, 'iron', c + Vector((0, 0, h / 2 + 0.235)), 0.03, 0.03, 0.06, segs=8, caps=False)   # ring boss
    cyl(pc, 'iron', c + Vector((0, 0, h / 2 + 0.01)), w * 1.22, w * 1.22, 0.03, segs=4)
    cyl(pc, 'iron', c + Vector((0, 0, -h / 2 - 0.02)), w * 1.15, w * 0.9, 0.05, segs=4)      # base
    cyl(pc, 'iron', c + Vector((0, 0, -h / 2 - 0.09)), 0.025, 0.005, 0.10, segs=6)           # drip finial
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        p = c + Vector((math.cos(a), math.sin(a), 0)) * (w * 1.18 * 0.7071 * 1.414 * 0.70)
        FK.add_box(pc, 'iron', (0.022, 0.022, h + 0.02), Matrix.Translation(p), cham=0.0, seed=k)
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=w * 1.12, radius2=w * 1.12, depth=h - 0.01)
    bm.transform(Matrix.Translation(c) @ Matrix.Rotation(math.pi / 4, 4, 'Z')); bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'flame')
    for k in range(4):   # a cross bar on each pane
        a = k * math.pi / 2
        d = Vector((math.cos(a), math.sin(a), 0))
        FK.add_box(pc, 'iron', (0.012, w * 1.6, 0.012), Matrix.Translation(c + d * (w * 0.80)) @ Matrix.Rotation(a, 4, 'Z'), cham=0.0, seed=k + 9)
    return pc, F_FRONT, hk

def part_sign():
    """The Drowsy Moon board on chains (built in the bracket's local frame, then placed like the bracket)."""
    pc = piece('Part_Sign')
    z = SIGN_Z
    hy0, hy1 = SIGN_HOOKS
    bw, bh = 1.16, 0.84
    yc = -(hy0 + hy1) / 2
    ztop = z - 0.32
    zc = ztop - bh / 2 - 0.10
    for hy in SIGN_HOOKS:
        for k in range(5):
            FK.add_box(pc, 'iron', (0.014, 0.034, 0.06), Matrix.Translation((0, -hy, z - 0.06 - k * 0.052)) @ Matrix.Rotation((k % 2) * 1.57, 4, 'Z'),
                       cham=0.0, seed=k + hy)
    def outline(hw_, hh_, arch_):
        pts = [(-hw_, -hh_), (hw_, -hh_)]
        for i in range(11):
            t = i / 10
            x = hw_ - 2 * hw_ * t
            pts.append((x, hh_ + arch_ * math.sin(math.pi * t)))
        return pts
    def slab(mat, pts, th, dx=0.0, dz=0.0):
        sub = piece('s')
        prism(sub, mat, [(p[0], p[1]) for p in pts], -th / 2, th / 2)
        merge(pc, sub, Matrix.Translation((dx, yc, zc + dz)) @ Matrix.Rotation(math.pi / 2, 4, 'Z'))
    slab('timber', outline(bw / 2, bh / 2 - 0.08, 0.14), 0.07)
    slab('field', outline(bw / 2 - 0.07, bh / 2 - 0.15, 0.12), 0.095)
    for k in range(18):   # gilt bead round the field
        t = k / 18
        pass
    for s in (-1, 1):
        xo = s * 0.05
        # sleeping crescent moon (outer arc minus inner arc), upper left of the field
        mpts = []
        for i in range(15):
            t = -math.pi * 0.85 + math.pi * 1.7 * i / 14
            mpts.append((math.sin(t) * 0.22, math.cos(t) * 0.22))
        for i in range(14, -1, -1):
            t = -math.pi * 0.85 + math.pi * 1.7 * i / 14
            mpts.append((math.sin(t) * 0.16 + 0.085, math.cos(t) * 0.18 + 0.01))
        sub = piece('moon')
        prism(sub, 'gilt', [(-p[0], p[1]) for p in mpts], -0.012, 0.012)
        merge(sub_into := pc, sub, Matrix.Translation((xo, yc + 0.20, zc + 0.10)) @ Matrix.Rotation(math.pi / 2, 4, 'Z') @
              Matrix.Rotation(math.radians(-18), 4, 'Y'))
        # its closed eye and smile: two small arcs of gilt bar on the moon's inner edge
        for (ec, er, a0, a1) in (((0.12, 0.13), 0.035, 0.2, 2.9), ((0.13, 0.02), 0.03, 3.4, 6.0)):
            pts = []
            for i in range(6):
                t = a0 + (a1 - a0) * i / 5
                pts.append(Vector((xo * 1.25, yc + 0.20 - (ec[0] - 0.12) - math.cos(t) * er * 0.0 + 0.0 - (ec[0] + math.cos(t) * er - 0.12),
                                   zc + 0.10 + ec[1] + math.sin(t) * er * (1 if a1 < 3.2 else 1))))
            for p, q in zip(pts, pts[1:]):
                bar(pc, 'crust' if False else 'iron', p, q, 0.012, seed=p.z)
        # stars
        for (sy_, sz_, rr) in ((-0.18, 0.24, 0.07), (-0.36, 0.06, 0.05), (-0.05, 0.32, 0.04)):
            spts = []
            for i in range(10):
                a = math.pi / 2 + i * math.pi / 5
                r_ = rr if i % 2 == 0 else rr * 0.45
                spts.append((math.cos(a) * r_, math.sin(a) * r_))
            sub = piece('star')
            prism(sub, 'gilt', spts, -0.01, 0.01)
            merge(pc, sub, Matrix.Translation((xo, yc + sy_, zc + sz_)) @ Matrix.Rotation(math.pi / 2, 4, 'Z'))
        # tankard: an oak half-barrel in relief with hoops, a handle and a foam head, lower right
        tc = Vector((xo * 1.2, yc - 0.22, zc - 0.18))
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.10, radius2=0.085, depth=0.26)
        for v in bm.verts:
            v.co.x *= 0.45
        bm.transform(Matrix.Translation(tc)); bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap')
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), 2, (0.3, 0.1)); FK._merge(pc, bm, 'oak_lt')
        for dz in (-0.08, 0.08):
            cyl(pc, 'iron', tc + Vector((0, 0, dz)), 0.10 - dz * 0.06, 0.10 - dz * 0.06, 0.022, segs=10, caps=False)
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
        bm.transform(Matrix.Translation(tc + Vector((0, 0, 0.15))) @ Matrix.Diagonal((0.05, 0.10, 0.05, 1))); bm.normal_update()
        uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'plaster')
        hp = []
        for i in range(8):
            t = -math.pi / 2 + math.pi * i / 7
            hp.append(tc + Vector((0, -0.10 - math.cos(t) * 0.07, math.sin(t) * 0.07)))
        tube(pc, 'oak_lt', hp, 0.016, 5)
    # gilt bead on the frame's top arch
    for i in range(12):
        t = (i + 0.5) / 12
        x = bw / 2 - 0.035 - (bw - 0.07) * t
        zz = (bh / 2 - 0.08) + 0.14 * math.sin(math.pi * t) - 0.02
        cyl(pc, 'gilt', (0, yc - x, zc + zz), 0.022, 0.022, 0.09, segs=6, axis='X')
    # scale the board and its motifs (everything under the chains) about the chain foot
    piv = Vector((0, yc, ztop + 0.02))
    for v in pc.bm.verts:
        if v.co.z < ztop + 0.03:
            v.co = piv + (v.co - piv) * SIGN_SCALE
    return pc, 'bracket', Vector((0, -(hy0 + hy1) / 2, z - 0.05))

# ----------------------------------------------------------------------------- whimsy, wear, assembly
def lean_co(p):
    if p.x < X0 - 0.52:     # the chimney stands on its own footing (the hip eave overhang is 0.49)
        return p
    dz = max(0.0, p.z - G1)
    return Vector((p.x + LEAN[0] * dz, p.y - LEAN[1] * dz, p.z))

SOCKETS = {}
RUNOFF = [1.05 - 0.10, 4.15 - 0.15, 6.80 - 0.15, G1 - 0.18, B2A - 0.05]

def write_wear(ob):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    mw = ob.matrix_world
    door_w = F_FRONT @ Vector(((DOOR[0] + DOOR[1]) / 2, -0.4, 0))
    cx, cy = X0 - 0.4, (CHM[0] + CHM[1]) / 2
    mats = [m.name for m in me.materials]
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        mname = mats[poly.material_index] if mats else ''
        roofish = mname == 'FK_RoofTile'
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 1.7)
            streak = noise.noise(Vector((p.x * 4.0, p.y * 4.0, p.z * 0.5)))
            r = max(0.0, 1 - p.z / (0.85 + 0.25 * nz)) ** 1.2 * 0.75
            r = max(r, 0.13 * max(0.0, noise.noise(p * 0.5 + Vector((3.0, 1.0, 2.0)))) + 0.05 * max(0.0, nz))
            if G1 - 0.6 < p.z < G1 + 0.05 and p.y < Y0 + 0.2:            # damp shade under the jetty
                r = max(r, 0.16 + 0.1 * streak)
            if PLATE0 - 0.7 < p.z < PLATE1 + 0.1 and mname != 'FK_RoofTile':   # under the deep eaves
                r = max(r, 0.20 + 0.1 * streak)
            if abs(p.x - cx) < 0.9 and abs(p.y - cy) < 1.0 and p.z > CH_TOP - 1.5:
                r = max(r, min(0.9, (p.z - (CH_TOP - 1.5)) / 1.2) * (0.7 + 0.2 * streak))
            for zs_ in RUNOFF:
                dz = zs_ - p.z
                if 0.0 < dz < 0.9 and abs(n.z) < 0.5:
                    r = max(r, (1 - dz / 0.9) ** 1.6 * max(0.0, 0.10 + 0.30 * streak))
            g = 0.0
            if roofish and n.z > 0.2:
                z_rel = p.z - (BEAM_Z if p.z < G1 + 0.6 else ZE)   # the porch roof has its own eave
                g = max(0.0, 1 - z_rel / 1.6) * 0.7 + max(0.0, 0.15 + 0.45 * nz) * 0.3
                if p.z < G1 + 0.6:
                    g *= 0.45                                     # the porch and hood roofs: sheltered, a little moss only
                if p.y > Y1:
                    g = min(1.0, g + 0.2)                       # the north-ish back slope holds the damp
                tv = ((poly.index * 2654435761) % 1009) / 1009.0
                r = max(r, 0.26 * tv * tv)
            elif n.z > 0.6 and p.z < 0.5:
                g = 0.25 + 0.3 * nz
            elif mname == 'FK_Sandstone' and n.z < 0.5 and p.z < 1.2:
                g = max(g, 0.25 * max(0.0, 0.3 + nz) * (1 - p.z / 1.2))   # green at the stone foot
            b = 0.0
            dd = (Vector((p.x, p.y, 0)) - Vector((door_w.x, door_w.y, 0))).length
            if p.z < 0.45 and dd < 1.4 and n.z > 0.5:
                b = 0.85 * (1 - dd / 1.4) ** 0.5                  # foot-polished threshold and deck
                g = 0.0
            if ob.name == 'Part_Door':
                lx = abs(p.x - (X0 + DOOR[1] - 0.18)); lz = abs(p.z - 1.0)
                if lx < 0.25 and lz < 0.35:
                    b = max(b, 0.5 * (1 - lx / 0.25)); r = max(r, 0.35 * (1 - lz / 0.35))
            if ob.name == 'Part_Porch' and mname == 'FK_OakDoor' and n.z > 0.7:
                b = max(b, 0.45)                                  # bench seats polished by sitting
            if mname == 'IN_Gilt':
                r = min(r, 0.12); g = 0.0
            attr.data[li].color = (min(1.0, r), min(1.0, g), min(1.0, b), 1.0)

SOFT = {'IN_Gilt', 'FK_Leaf', 'FK_Bloom'}

def to_object(pc, parent, origin=None, lean=True):
    if lean:
        for v in pc.bm.verts:
            v.co = lean_co(v.co)
    if origin is not None:
        bmesh.ops.translate(pc.bm, verts=pc.bm.verts[:], vec=-origin)
    me = bpy.data.meshes.new(pc.name)
    pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats:
        me.materials.append(m)
    for poly in me.polygons:
        if me.materials[poly.material_index].name in SOFT:
            poly.use_smooth = True
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    if origin is not None:
        ob.location = origin
    bpy.context.view_layer.update()      # write_wear reads matrix_world
    write_wear(ob)
    return ob

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

SIGN_XF = F_FRONT1 @ SIGN_XF_LOCAL

def build():
    root = bpy.data.objects.new('Inn', None); bpy.context.scene.collection.objects.link(root)
    frame = piece('Frame')
    for pc, xf in front_walls() + left_walls() + right_walls() + back_walls():
        merge(frame, pc, xf)
    dormers = [dormer('DormerFL', XC - 2.1, 'f'), dormer('DormerFR', XC + 2.1, 'f')]
    for pc in [roof(), ridge_vent(), chimney(), yard()] + dormers:
        merge(frame, pc)
    fo = to_object(frame, root)
    total = tris(fo)
    print('  %-12s %6d tris  %d slots' % ('Frame', total, len(fo.data.materials)))
    parts = {}
    for fn, sock in ((part_door, 'Door'), (part_sign, 'Sign'), (part_porch, 'Porch'), (part_lantern, 'Lantern')):
        pc, xf, s_local = fn()
        if xf == 'bracket':
            xf = SIGN_XF
        bmesh.ops.transform(pc.bm, matrix=xf, verts=pc.bm.verts[:])
        s = lean_co(xf @ s_local)
        ob = to_object(pc, root, origin=s)
        parts[sock] = ob
        t = tris(ob); total += t
        print('  %-12s %6d tris  %s' % (ob.name, t, ', '.join(m.name for m in ob.data.materials)))
        e = bpy.data.objects.new('SOCKET_' + sock, None); bpy.context.scene.collection.objects.link(e)
        e.parent = fo; e.location = s; e.empty_display_size = 0.3
        SOCKETS[sock] = s
    # footprint: the ground-storey body, the porch deck and step, the chimney; flat
    fx0, fx1 = X0 - 0.80 - 0.06, X1 + 0.10
    fy0, fy1 = Y0 - DECK[2] - 0.45, Y1 + 0.12
    me = bpy.data.meshes.new('FOOTPRINT'); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation(((fx0 + fx1) / 2, (fy0 + fy1) / 2, 0.05)) @ Matrix.Diagonal((fx1 - fx0, fy1 - fy0, 0.1, 1)))
    bm.to_mesh(me); bm.free()
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root
    fp.display_type = 'WIRE'; fp.hide_render = True
    print('FOOTPRINT x %.2f..%.2f  y %.2f..%.2f  (%.2f x %.2f m; L class max 10.5)' % (fx0, fx1, fy0, fy1, fx1 - fx0, fy1 - fy0))
    allx = [(o.matrix_world @ Vector(c)).x for o in [fo] + list(parts.values()) for c in o.bound_box]
    ally = [(o.matrix_world @ Vector(c)).y for o in [fo] + list(parts.values()) for c in o.bound_box]
    allz = [(o.matrix_world @ Vector(c)).z for o in [fo] + list(parts.values()) for c in o.bound_box]
    print('bounds x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f  (foundation +-5.5)' % (min(allx), max(allx), min(ally), max(ally), min(allz), max(allz)))
    print('Inn total tris: %d (cap 50000)' % total)
    return root, fo, parts, fp

# ----------------------------------------------------------------------------- review
VIEWS_DEF = {
    'tq': (Vector((13.5, -17.0, 3.4)), Vector((0.3, 0, 5.6)), 46),
    'tql': (Vector((-15.0, -15.5, 3.2)), Vector((-0.2, 0, 5.6)), 46),
    'back': (Vector((12.0, 17.5, 3.6)), Vector((0.3, 0.5, 5.8)), 46),
    'front': (Vector((0.3, -21.0, 3.2)), Vector((0.3, 0, 6.0)), 44),
    'frame': (Vector((13.5, -17.0, 3.4)), Vector((0.3, 0, 5.6)), 46),
    'framel': (Vector((-15.0, -15.5, 3.2)), Vector((-0.2, 0, 5.6)), 46),
    'close': (Vector((4.0, -9.0, 1.7)), Vector((-0.8, -2.6, 1.9)), 46),
    'frameclose': (Vector((4.0, -9.0, 1.7)), Vector((-0.8, -2.6, 1.9)), 46),
    'sign': (Vector((-0.5, -8.5, 3.6)), Vector((-4.9, -2.6, 4.7)), 34),
    'side': (Vector((16.0, 1.0, 3.0)), Vector((0.5, 1.0, 5.4)), 46),
    'left': (Vector((-16.5, 2.5, 3.0)), Vector((0.0, 1.3, 5.6)), 46),
}

def review(root, parts):
    sc = bpy.context.scene
    FK.RRES = RRES; FK.SAMPLES = SAMPLES
    cam = FK.review_scene(root, variant=True)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(X0 + DECK[1] + 0.8, Y0 - DECK[2] - 1.2, 0.875))
    fig = bpy.context.object; fig.name = 'Figure175'
    fm = bpy.data.materials.new('Figure'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.18, 0.12, 1)
    fig.data.materials.append(fm)
    for v in VIEWS:
        frm, to, fov = VIEWS_DEF[v]
        hide = v.startswith('frame')
        for o in parts.values():
            o.hide_render = hide
        FK.look(cam, frm, to); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, 'inn_%s.png' % v)
        bpy.ops.render.render(write_still=True)
        print('rendered', v)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, fo, parts, fp = build()
    if '--export' in argv:
        for o in bpy.context.scene.objects:
            o.select_set(o == root or o.parent in (root, fo))
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Inn.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                                 mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR',
                                 path_mode='STRIP')   # no texture paths: Unity names textured slots 'Lit' when a cloud path exists
        print('exported', os.path.join(OUT, 'Inn.fbx'))
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'inn.blend'))
    if '--no-render' not in argv:
        review(root, parts)
