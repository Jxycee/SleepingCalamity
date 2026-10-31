"""KeepTerrace: the Hollowmere keep terrace kit (Q9, T2 kit): buttressed retaining wall with a heavy coping, stone planters with
trailing ivy, worn stair treads, raked balustrades, newels and benches. Original storybook stonework in the Dark Cloud tradition
rendered in stylised realism: chunky coursed rubble on a plinth course, a string course at the terrace level, a pillowy coping,
fat irregular balusters, 1-3 degrees off true, chamfered stones. The wall is the bell tower's base stone (sc_bell_tower.py: the
sandstone tileable tinted to the same grey rubble, pale dressed quoins).
Contract: Reference/hollowmere/keep/CONTRACT.md (piece names, extents in Unity terms).

Pieces (each a direct child mesh of root KeepTerrace, authored at its own pivot at the origin, pieces overlap in the file):
  WallSegment     x -1..1, z -0.6..0.6 (Unity), y -0.6..4.9: 2 m of retaining wall, coursed rubble face on the square side (z -0.6),
                  plinth course, string course at y 4.0, parapet y 4.0..4.9 with a heavy coping (overhangs both sides to +-0.6).
                  Stones that cross the 2 m seam are cut on the seam plane, so neighbouring segments join without a straight joint line
                  (the course pattern repeats every 2 m; the world-metre UVs and the wear noise repeat every 2 m too).
  WallEnd         x -0.3..0.3, same y/z as the wall: a dressed-stone return pier (alternating quoin courses) with a coping; symmetric.
  Buttress        x -0.55..0.55, z -1.3..-0.6, y -0.6..4.0: battered (front 1.3 -> 0.85 m off the wall), corner quoins, weathered sloping top.
  Planter         x -0.9..0.9, z -0.3..0.3, y 0..0.35 (builder puts it at y 4.9 on the coping); soil mound and an ivy bush on top, ivy
                  cards trailing over the square side down the coping and parapet face (reaches y -0.8, z -0.68, x +-0.87).
  StairStep       x -1..1, z 0..0.32 visible tread (a hidden 0.08 tail at the back, and a hidden body down to y -1.0 so the soffit is
                  solid), y 0..0.22: nosing, worn dip. The builder stacks them (rise 0.222, run 0.367 for the 18-step flights).
  Balustrade      x -1..1, y 0..1.0, z +-0.15: 2 m flat balustrade (base rail, 7 balusters, rail).
  BalustradeRaked EXTRA piece: the same balustrade built on the stair slope (rail parallel to the flight, balusters vertical), spanning
                  6 steps of the 0.222 x 0.367 flight: horizontal run 2.2 m along x (-1.1..1.1), rising 1.333; origin at the middle of
                  the base underside; y -0.67..1.67 (see the Status block). Rotate 90 deg about the vertical to run it along a flight.
  Newel           0.4 x 0.4 (x, z), y 0..1.2: plinth, panelled shaft, cap, pyramid and ball finial.
  Bench           x -0.9..0.9, z -0.25..0.25, y 0..0.5: stone slab seat on two legs.

Authoring frame (this file): A = (x across, y along the wall, z up) = Unity (x, z, y). The finished meshes are rotated 180 degrees
about Z into Blender axes, so the FBX export (-Z forward, Y up) lands on Unity (x, y, z) = Blender (-x, z, -y) as the contract says.
Materials (shared B1 tileables, metre UVs 2 m per tile; linear CORNER float colour attribute "wear": R damp/grime, G moss, B polish):
KT_Rubble (sandstone tinted like BT_Rubble), KT_Dressed (sandstone, the tower's quoin stone), KT_Mortar (dark joint core), KT_Soil (flat),
M_B9_Foliage (the shared B9 foliage atlas, ivy cells, alpha clip; the export folder carries a copy of the atlas set).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_keep_terrace.py -- --out <scratch>
      [--views wall,stairs,close,terrace,line] [--rres 1000] [--samples 20] [--no-render] [--assemble] [--export] [--export-dir <dir>]
      [--tris] [--blend]
  --assemble lays out the review stretch (12 m of wall, 2 buttresses, 2 planters, a stair of 18 steps rising 4 m with raked
  balustrades and newels, 2 benches; never exported) and renders wall/stairs/close/terrace; without it the loose kit is lined up.
Budget: kit under 30k tris.

Status (2026-10-31, cloud): 3 render rounds, self-score 24 -> 25.5 -> 26.5/40 (calibrated; target first + 5 not reached, stopped at the
3-round limit). Final lines: silhouette 3.5, proportion 3.5, form 3.5, surface 3, colour 3, wear 3, set 3.5, efficiency 4 (no glass or
metal). 20,666 tris (WallSegment 7,860, WallEnd 1,264, Buttress 3,704, Planter 2,142, StairStep 108, Balustrade 1,872, BalustradeRaked
1,944, Newel 748, Bench 1,024; two runs agree). Sheet: Reference/hollowmere/keep/SHEET_keep_terrace.jpg (existing refs: the fetch
tool timed out on Openverse, 504/500). Next visible gains: per-stone value variation in the rubble (stones read as one tan tone), a
stronger damp foot at wide range, longer ivy strands that clear the trough so the planter still reads, cut-cap seam faces fixed
(a few seam stones show dark slits at the open end of a lone WallSegment; abutted ends are hidden), a wear-aware rubble tint that
matches the tower exactly once both are seen in Unity.
"""
import sys, os, math, zlib, shutil
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT
argv = FK.argv
RRES = int(FK.arg('--rres', '1000')); SAMPLES = int(FK.arg('--samples', '20'))
TILE = FK.TILE
R_ = lambda s: FK.rng('kt:' + s)

def Rx(d): return Matrix.Rotation(math.radians(d), 4, 'X')
def Ry(d): return Matrix.Rotation(math.radians(d), 4, 'Y')
def Rz(d): return Matrix.Rotation(math.radians(d), 4, 'Z')
def T(*v): return Matrix.Translation(Vector(v))

# ----------------------------------------------------------------------------- materials
ATLAS = os.path.join(FK.REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B9', 'Weed_A', 'T_B9_Foliage')

def foliage_mat():
    if 'M_B9_Foliage' in FK.MATS:
        return FK.MATS['M_B9_Foliage']
    m = bpy.data.materials.new('M_B9_Foliage'); m.use_nodes = True; nt = m.node_tree
    bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    def tex(path, nonc):
        nd = nt.nodes.new('ShaderNodeTexImage')
        nd.image = bpy.data.images.load(os.path.abspath(path), check_existing=True)
        nd.image.colorspace_settings.name = 'Non-Color' if nonc else 'sRGB'
        nd.image.alpha_mode = 'STRAIGHT'
        return nd
    bc = tex(ATLAS + '_BaseColor.png', False); nm = tex(ATLAS + '_Normal.png', True); orm = tex(ATLAS + '_ORM.png', True)
    sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(orm.outputs['Color'], sep.inputs['Color'])
    mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1.0
    nt.links.new(bc.outputs['Color'], mul.inputs['A']); nt.links.new(sep.outputs['Red'], mul.inputs['B'])
    nt.links.new(mul.outputs['Result'], bs.inputs['Base Color'])
    nt.links.new(sep.outputs['Green'], bs.inputs['Roughness']); nt.links.new(sep.outputs['Blue'], bs.inputs['Metallic'])
    nmap = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(nm.outputs['Color'], nmap.inputs['Color'])
    nt.links.new(nmap.outputs['Normal'], bs.inputs['Normal'])
    gt = nt.nodes.new('ShaderNodeMath'); gt.operation = 'GREATER_THAN'; gt.inputs[1].default_value = 0.5
    nt.links.new(bc.outputs['Alpha'], gt.inputs[0]); nt.links.new(gt.outputs[0], bs.inputs['Alpha'])
    m.surface_render_method = 'DITHERED'; m.use_backface_culling = False
    FK.MATS['M_B9_Foliage'] = m
    return m

EXTRA = {
    'rubble': lambda: FK.tile_mat('KT_Rubble', 'B1_town/sandstone', tint=(0.56, 0.56, 0.56), normal=1.5),   # the bell tower's rubble, a shade greyer
    'stone': lambda: FK.tile_mat('KT_Dressed', 'B1_town/sandstone', tint=(0.94, 0.94, 0.92), normal=1.3),      # its pale quoin stone
    'mortar': lambda: FK.tile_mat('KT_Mortar', 'B1_town/sandstone', tint=(0.26, 0.245, 0.21), normal=0.6),
    'soil': lambda: FK.flat_mat('KT_Soil', (0.034, 0.025, 0.017), 0.95),
    'leaf': foliage_mat,
}
_orig_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)
LEAF = 'M_B9_Foliage'

# ----------------------------------------------------------------------------- geometry helpers
def fbox(pc, mat, size, c, rot=None, cham=0.03, segs=1, cuts=(0, 0, 0), wob=0.0, seed=0.0, xclip=None):
    """Chamfered box (FK.add_box with world-metre UVs) placed at c; xclip=(lo, hi) cuts it flat on those x planes (capped)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    for ax, n_ in enumerate(cuts):
        for k in range(1, n_ + 1):
            co = Vector((0, 0, 0)); co[ax] = -size[ax] / 2 + size[ax] * k / (n_ + 1)
            no = Vector((0, 0, 0)); no[ax] = 1
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no)
    if cham > 0:
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > 0.5]
        bmesh.ops.bevel(bm, geom=edges, offset=cham, segments=segs, affect='EDGES', profile=0.5)
    if wob:
        for v in bm.verts:
            q = v.co * 1.7 + Vector((seed, seed * 1.3, seed * 0.7))
            v.co += Vector((noise.noise(q), noise.noise(q + Vector((5.2, 0, 0))), noise.noise(q + Vector((0, 9.1, 0))))) * wob
    bm.transform(T(*c) @ (rot if rot is not None else Matrix.Identity(4)))
    if xclip:
        clipped = False
        for px, inner in ((xclip[0], True), (xclip[1], False)):
            xs = [v.co.x for v in bm.verts]
            if (inner and min(xs) < px - 1e-5) or (not inner and max(xs) > px + 1e-5):
                bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(px, 0, 0), plane_no=(1, 0, 0),
                                       clear_inner=inner, clear_outer=not inner, dist=1e-6)
                bd = [e for e in bm.edges if len(e.link_faces) == 1]
                if bd:
                    bmesh.ops.edgeloop_fill(bm, edges=bd)
                clipped = True
        if clipped:
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
    FK._merge(pc, bm, mat)

def hull(pc, mat, pts, cham=0.0, segs=1, uv_world=True):
    """Convex solid from points (bevelled if cham > 0)."""
    bm = bmesh.new()
    vs = [bm.verts.new(Vector(p)) for p in pts]
    bmesh.ops.convex_hull(bm, input=vs)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if cham > 0:
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > 0.5]
        bmesh.ops.bevel(bm, geom=edges, offset=cham, segments=segs, affect='EDGES', profile=0.5)
    bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
    FK._merge(pc, bm, mat)

def merge_smooth(pc, bm, mat):
    """FK._merge that keeps the smooth flag on faces (turned balusters)."""
    si = pc.slot(mat)
    uvs = bm.loops.layers.uv.get('UVMap')
    vmap = {v: pc.bm.verts.new(v.co) for v in bm.verts}
    for f in bm.faces:
        try:
            nf = pc.bm.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            continue
        nf.material_index = si; nf.smooth = True
        for l, nl in zip(f.loops, nf.loops):
            nl[pc.uv].uv = l[uvs].uv
    bm.free()

def lathe(pc, mat, prof, c, sides=8, seed=0.0, lean=(0, 0), jit=0.0):
    """Surface of revolution about a vertical axis at c; prof = [(r, h)] bottom to top; irregular by jit."""
    R = R_('lathe%f' % seed)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    phase = R.uniform(0, 1)
    rings = []
    for (r, h) in prof:
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides
            rr = r * (1 + R.uniform(-jit, jit)) if r > 1e-6 else 0
            ring.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), h)) if r > 1e-6 else None)
        rings.append(ring if r > 1e-6 else [bm.verts.new((0, 0, h))] * sides)
    for j in range(len(prof) - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            vs = [rings[j][k], rings[j][k2], rings[j + 1][k2], rings[j + 1][k]]
            vs2 = []
            for v in vs:
                if v not in vs2:
                    vs2.append(v)
            if len(vs2) < 3:
                continue
            try:
                f = bm.faces.new(vs2)
            except ValueError:
                continue
            for l in f.loops:
                ang = math.atan2(l.vert.co.y, l.vert.co.x)
                if k2 == 0 and ang < 0.5:
                    ang += 2 * math.pi
                if k2 != 0 and ang < 0 and k == sides - 1:
                    ang += 2 * math.pi
                l[uvl].uv = (ang * 0.06 / TILE + phase, l.vert.co.z / TILE)
    bm.normal_update()
    m = T(*c) @ Rx(lean[0]) @ Ry(lean[1])
    bm.transform(m); bm.normal_update()
    merge_smooth(pc, bm, mat)

def extrude_x(pc, mat, poly, x0, x1, nx=4, dip=0.0):
    """Prism along x of a (y, z) polygon (concave allowed), nx rows along x (wear vertices); dip lowers the top by dip*(1-(x/x1)^2)."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    xs = [x0 + (x1 - x0) * i / nx for i in range(nx + 1)]
    ztop = max(p[1] for p in poly)
    rows = []
    for x in xs:
        row = []
        for (y, z) in poly:
            zz = z - (dip * max(0.0, 1 - (x / max(abs(x0), abs(x1))) ** 2) if z > ztop - 0.03 else 0.0)
            row.append(bm.verts.new((x, y, zz)))
        rows.append(row)
    n = len(poly)
    for i in range(nx):
        for k in range(n):
            k2 = (k + 1) % n
            bm.faces.new((rows[i][k], rows[i + 1][k], rows[i + 1][k2], rows[i][k2]))
    for row in (rows[0], rows[-1]):
        bm.faces.new(row if row is rows[-1] else row[::-1])
    bm.normal_update()
    # outward check: flip all if the solid is inside out
    cen = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    inside = sum(1 for f in bm.faces if (f.calc_center_median() - cen).dot(f.normal) < 0)
    if inside > len(bm.faces) / 2:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.normal_update()
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
    FK._merge(pc, bm, mat)

# ----------------------------------------------------------------------------- coursed stone rows
def course(pc, mat, R, z0, z1, yf, depth, lens, face=-1, period=2.0, xlo=-1.0, xhi=1.0, cham=0.03, segs=1, gap=0.035, proud=0.07,
           tilt=1.2, wob=0.012, cuts_over=0.55, hjit=0.02, dy=None, split=0.0):
    """One course of stones across [xlo, xhi]. The stone pattern repeats every `period`; the stone that crosses the segment seam is
    placed twice (shifted by +-period) and cut on the seam planes so neighbours continue it. face -1 = front (y < 0), +1 = back."""
    u0 = R.uniform(0.0, 0.5)
    seq = []; tot = 0.0
    while tot < period - 0.3:
        ln = R.uniform(*lens)
        if period - (tot + ln) < lens[0] * 0.8:
            ln = period - tot
        seq.append(ln); tot += ln
    if tot < period:
        seq[-1] += period - tot
    stones = []; a = xlo + u0
    for ln in seq:
        stones.append(dict(a=a, b=a + ln, h=(z1 - z0) - gap + R.uniform(-hjit, hjit), pr=R.uniform(0, proud),
                           tl=math.radians(R.uniform(-tilt, tilt)), sd=R.random() * 90, dz=R.uniform(-0.01, 0.01),
                           sp=(R.random() < split and (a + 0 > -9)), tl2=math.radians(R.uniform(-tilt, tilt)), hs=R.uniform(0.4, 0.6)))
        a += ln
    for s in stones:
        ln = s['b'] - s['a'] - gap
        for sh in (-period, 0.0, period):
            xa, xb = s['a'] + sh, s['b'] + sh
            if xb <= xlo + 0.02 or xa >= xhi - 0.02:
                continue
            clip = (xlo, xhi) if (xa < xlo or xb > xhi) else None
            yfront = yf + face * s['pr']
            cy = yfront - face * depth / 2 if face == 1 else yfront + depth / 2
            cuts = (1 if ln > cuts_over else 0, 0, 0)
            if s['sp'] and ln > 0.45:     # two stacked stones in one course
                h1 = s['h'] * s['hs'] - 0.015; h2 = s['h'] * (1 - s['hs']) - 0.015; zb = (z0 + z1) / 2 + s['dz'] - s['h'] / 2
                for (hh, zc, tl, sd) in ((h1, zb + h1 / 2, s['tl'], s['sd']), (h2, zb + s['h'] * s['hs'] + 0.015 + h2 / 2, s['tl2'], s['sd'] + 5.0)):
                    fbox(pc, mat, (ln, depth, hh), ((xa + xb) / 2, cy, zc), rot=Ry(math.degrees(tl)), cham=cham, segs=segs,
                         cuts=cuts, wob=wob, seed=sd, xclip=clip)
                continue
            fbox(pc, mat, (ln, depth, s['h']), ((xa + xb) / 2, cy, (z0 + z1) / 2 + s['dz']), rot=Ry(math.degrees(s['tl'])),
                 cham=cham, segs=segs, cuts=cuts, wob=wob, seed=s['sd'], xclip=clip)

def heights(R, z0, z1, lo=0.27, hi=0.38):
    out = []; z = z0
    while z1 - z > hi + lo:
        h = R.uniform(lo, hi); out.append((z, z + h)); z += h
    out.append((z, z1))
    return out

# ----------------------------------------------------------------------------- pieces
def p_wall_segment():
    pc = FK.Piece('WallSegment'); R = R_('wall')
    fbox(pc, 'mortar', (2.0, 1.06, 4.6), (0, 0.07, 1.7), cham=0)                  # core y -0.46..0.6, z -0.6..4.0
    fbox(pc, 'mortar', (2.0, 0.96, 0.62), (0, 0.02, 4.31), cham=0)                # parapet core y -0.46..0.5
    course(pc, 'stone', R, -0.6, -0.1, -0.6, 0.32, (0.85, 1.35), cham=0.045, segs=2, proud=0.03, tilt=0.6, wob=0.01)   # foundation course
    course(pc, 'stone', R, -0.1, 0.45, -0.6, 0.34, (0.8, 1.3), cham=0.05, segs=2, proud=0.03, tilt=0.6, wob=0.01)      # plinth course
    for (z0, z1) in heights(R, 0.45, 3.80):
        course(pc, 'rubble', R, z0, z1, -0.50, 0.24, (0.38, 1.05), cham=0.035, segs=2, proud=0.09, wob=0.02, split=0.22)
    course(pc, 'stone', R, 3.80, 4.0, -0.6, 0.30, (0.9, 1.3), cham=0.03, segs=2, proud=0.0, tilt=0.4, wob=0.006)      # string course
    for (z0, z1) in ((4.0, 4.31), (4.31, 4.62)):
        course(pc, 'rubble', R, z0, z1, -0.50, 0.22, (0.4, 1.0), cham=0.035, segs=2, proud=0.04, wob=0.015)                            # parapet, square side
        course(pc, 'rubble', R, z0, z1, 0.50, 0.22, (0.4, 1.0), face=1, cham=0.03, proud=0.04)                     # parapet, terrace side
    # heavy coping: through stones the full thickness, rounded
    u0 = R.uniform(0.0, 0.4)
    for (a, b) in ((-1.0 + u0 - 1.2, -1.0 + u0), (-1.0 + u0, -1.0 + u0 + 1.0), (-1.0 + u0 + 1.0, -1.0 + u0 + 2.0)):
        if b <= -0.98 or a >= 0.98:
            continue
        clip = (-1.0, 1.0) if (a < -1.0 or b > 1.0) else None
        fbox(pc, 'stone', (b - a - 0.03, 1.2, 0.28), ((a + b) / 2, 0.0, 4.76), cham=0.07, segs=3, cuts=(2, 1, 0), wob=0.008,
             seed=a * 7, xclip=clip)
    return pc

def p_wall_end():
    pc = FK.Piece('WallEnd'); R = R_('wallend')
    fbox(pc, 'mortar', (0.52, 1.0, 4.9), (0, 0, 1.85), cham=0)
    z = -0.6; i = 0
    plan = [(-0.6, -0.1), (-0.1, 0.45)] + heights(R, 0.45, 4.62, 0.30, 0.40)
    for (z0, z1) in plan:
        hw = 0.3 - (0.012 if i % 2 else 0.0); hd = 0.6 - (0.02 if i % 2 == 0 else 0.0)
        fbox(pc, 'stone', (2 * hw, 2 * hd, z1 - z0 - 0.03), (R.uniform(-0.006, 0.006), 0, (z0 + z1) / 2), rot=Rz(R.uniform(-0.9, 0.9)),
             cham=0.04, segs=2 if i < 3 else 1, cuts=(0, 1, 0), wob=0.008, seed=R.random() * 90)
        i += 1
    fbox(pc, 'stone', (0.6, 1.2, 0.28), (0, 0, 4.76), cham=0.07, segs=3, cuts=(1, 1, 0), wob=0.006, seed=3.0)
    return pc

BUT_Y0, BUT_Y1, BUT_Z0, BUT_Z1 = -1.30, -0.85, -0.6, 4.0       # front face of the battered buttress at z -0.6 and at the terrace top
def but_front(z):
    return BUT_Y0 + (BUT_Y1 - BUT_Y0) * (z - BUT_Z0) / (BUT_Z1 - BUT_Z0)

def p_buttress():
    pc = FK.Piece('Buttress'); R = R_('buttress')
    zc = 3.55
    hull(pc, 'mortar', [(sx, -0.6, -0.6) for sx in (-0.48, 0.48)] + [(sx, but_front(-0.6) + 0.08, -0.6) for sx in (-0.48, 0.48)] +
         [(sx, but_front(zc) + 0.1, zc) for sx in (-0.48, 0.48)] + [(sx, -0.6, zc) for sx in (-0.48, 0.48)])
    # foot: two heavy dressed courses
    for (z0, z1, pr) in ((-0.6, -0.1, 0.02), (-0.1, 0.40, 0.0)):
        yf = but_front(z1) - 0.05
        fbox(pc, 'stone', (1.1, -0.6 - yf + 0.0, z1 - z0 - 0.03), (R.uniform(-0.004, 0.004), (yf - 0.6) / 2, (z0 + z1) / 2),
             cham=0.05, segs=2, cuts=(2, 1, 0), wob=0.01, seed=R.random() * 50)
    i = 0
    for (z0, z1) in heights(R, 0.40, zc, 0.28, 0.36):
        zm = (z0 + z1) / 2; yf = but_front(zm); h = z1 - z0 - 0.03
        d = -0.6 - yf                                   # depth of the buttress at this height
        qa, qb = (0.5, 0.3) if i % 2 == 0 else (0.3, 0.5)
        qb = min(qb, d - 0.05); qa = min(qa, 0.5)
        for sx in (-1, 1):
            fbox(pc, 'stone', (qa, qb, h), (sx * (0.535 - qa / 2), yf + qb / 2 - 0.01, zm), rot=Rz(R.uniform(-1.2, 1.2)),
                 cham=0.04, segs=2, cuts=(0, 0, 0), wob=0.008, seed=R.random() * 90)
            # side face: one rubble stone between the quoin and the wall
            sl = d - qb - 0.03
            if sl > 0.1:
                fbox(pc, 'rubble', (0.2, sl, h - R.uniform(0, 0.03)), (sx * (0.55 - 0.1 + R.uniform(0, 0.03)), -0.6 - sl / 2, zm),
                     cham=0.03, wob=0.01, seed=R.random() * 90)
        mw = 1.07 - 2 * qa - 0.03
        fbox(pc, 'rubble', (mw, 0.24, h - R.uniform(0, 0.03)), (R.uniform(-0.01, 0.01), yf - R.uniform(0, 0.03) + 0.12, zm),
             rot=Rz(R.uniform(-1.0, 1.0)), cham=0.035, segs=2, cuts=(1 if mw > 0.6 else 0, 0, 0), wob=0.02, seed=R.random() * 90)
        i += 1
    # weathered sloping top
    yl = but_front(zc) - 0.03
    hull(pc, 'stone', [(sx, y, z) for sx in (-0.55, 0.55) for (y, z) in ((-0.6, 3.54), (yl, 3.54), (yl, 3.70), (-0.62, 4.0), (-0.6, 4.0))],
         cham=0.035, segs=2)
    return pc

def ivy_card(bm, uvl, c, up, nrm, w, h, rect, bow=0.03):
    nrm = Vector(nrm).normalized(); up = (Vector(up) - nrm * Vector(up).dot(nrm)).normalized(); rt = up.cross(nrm)
    rows = []
    for j in range(3):
        v = j / 2
        row = []
        for i in range(2):
            u = i
            p = c + rt * (u - 0.5) * w + up * (v - 0.5) * h + nrm * (bow * (1 - (2 * v - 1) ** 2))
            row.append(bm.verts.new(p))
        rows.append(row)
    for j in range(2):
        f = bm.faces.new((rows[j][0], rows[j][1], rows[j + 1][1], rows[j + 1][0]))
        for l in f.loops:
            ci = [v for v in (rows[j][0], rows[j][1], rows[j + 1][1], rows[j + 1][0])].index(l.vert)
            uu = (0, 1, 1, 0)[ci]; vv = (j / 2, j / 2, (j + 1) / 2, (j + 1) / 2)[ci]
            l[uvl].uv = (rect[0] + (rect[2] - rect[0]) * uu, rect[1] + (rect[3] - rect[1]) * vv)

IVY_CELLS = [(0.0, 0.25, 0.25, 0.5), (0.25, 0.25, 0.5, 0.5)]
IVY_DARK = (0.0, 0.0, 0.25, 0.25)
IVY_SINGLES = [(0.25, 0.125, 0.375, 0.25), (0.375, 0.125, 0.5, 0.25)]
DRAPE = [(0.31, 0.35), (0.34, 0.22), (0.35, 0.08), (0.43, 0.02), (0.56, 0.02), (0.645, -0.04), (0.665, -0.18), (0.62, -0.30), (0.545, -0.40),
         (0.535, -0.6), (0.535, -0.8)]

def drape_pt(s):
    """Point (d outward, h up) and tangent at arc fraction s along DRAPE."""
    L = [0.0]
    for a, b in zip(DRAPE, DRAPE[1:]):
        L.append(L[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    t = s * L[-1]
    for i in range(len(L) - 1):
        if t <= L[i + 1]:
            f = (t - L[i]) / (L[i + 1] - L[i]); a, b = DRAPE[i], DRAPE[i + 1]
            d = (b[0] - a[0], b[1] - a[1]); n = math.hypot(*d)
            return (a[0] + d[0] * f, a[1] + d[1] * f), (d[0] / n, d[1] / n)
    b = DRAPE[-1]; return b, (0.0, -1.0)

def p_planter():
    pc = FK.Piece('Planter'); R = R_('planter')
    fbox(pc, 'stone', (1.8, 0.6, 0.07), (0, 0, 0.035), cham=0.025, cuts=(2, 0, 0), wob=0.006, seed=1.0)
    # walls: three front panels, two back, two ends
    for (a, b) in ((-0.9, -0.3), (-0.3, 0.3), (0.3, 0.9)):
        fbox(pc, 'stone', (b - a - 0.02, 0.12, 0.23), ((a + b) / 2, -0.24, 0.185), cham=0.025, segs=2, cuts=(1, 0, 0), wob=0.008, seed=a * 9 + 3)
    for (a, b) in ((-0.9, 0.0), (0.0, 0.9)):
        fbox(pc, 'stone', (b - a - 0.02, 0.12, 0.23), ((a + b) / 2, 0.24, 0.185), cham=0.025, segs=1, cuts=(1, 0, 0), wob=0.008, seed=a * 9 + 5)
    for sx in (-1, 1):
        fbox(pc, 'stone', (0.12, 0.38, 0.23), (sx * 0.84, 0, 0.185), cham=0.025, wob=0.008, seed=sx + 7)
    # moulded rim: a continuous course of rim stones, a little proud of the walls
    for (a, b, y, w) in ((-0.9, 0.0, -0.25, 0.1), (0.0, 0.9, -0.25, 0.1), (-0.9, 0.0, 0.25, 0.1), (0.0, 0.9, 0.25, 0.1)):
        fbox(pc, 'stone', (b - a - 0.012, w * 2, 0.06), ((a + b) / 2, y, 0.32), cham=0.022, segs=2, cuts=(1, 0, 0), wob=0.005, seed=a * 3 + y)
    for sx in (-1, 1):
        fbox(pc, 'stone', (0.2, 0.4, 0.06), (sx * 0.8, 0, 0.32), cham=0.022, segs=2, wob=0.005, seed=sx * 5)
    # soil mound
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    nx, ny = 7, 3; vs = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            x = -0.78 + 1.56 * i / nx; y = -0.18 + 0.36 * j / ny
            z = 0.27 + 0.05 * math.sin(math.pi * i / nx) * math.sin(math.pi * j / ny) + 0.012 * noise.noise(Vector((x * 4, y * 4, 2.0)))
            row.append(bm.verts.new((x, y, z)))
        vs.append(row)
    for j in range(ny):
        for i in range(nx):
            f = bm.faces.new((vs[j][i], vs[j][i + 1], vs[j + 1][i + 1], vs[j + 1][i]))
            for l in f.loops:
                l[uvl].uv = (l.vert.co.x / TILE, l.vert.co.y / TILE)
    bm.normal_update(); FK._merge(pc, bm, 'soil')
    # ivy: an upright bush and strands trailing over the square side
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    for k in range(15):
        x = -0.78 + 1.56 * (k + R.uniform(-0.3, 0.3)) / 14; ang = R.uniform(0, math.pi)
        nrm = Vector((math.cos(ang), math.sin(ang), 0.25)); sz = R.uniform(0.26, 0.38)
        zc = 0.29 + sz * 0.3 + R.uniform(0, 0.05)
        ivy_card(bm, uvl, Vector((x, R.uniform(-0.08, 0.08), zc)), Vector((R.uniform(-0.2, 0.2), 0, 1)), nrm, sz, sz,
                 IVY_CELLS[k % 2] if k % 5 else IVY_DARK, bow=0.04)
    nstr = 20
    for k in range(nstr):
        x = -0.84 + 1.68 * (k + R.uniform(-0.25, 0.25)) / (nstr - 1)
        send = R.uniform(0.75, 1.0) if k % 3 else R.uniform(0.4, 0.6)
        s = 0.0
        while s < send:
            (d, h), (td, th) = drape_pt(s)
            c = Vector((x + R.uniform(-0.04, 0.04), -d, h)); up = Vector((0, -td, th)); nrm = Vector((0, -(-th), -td))   # outward: n = (-th, td) in (d, h)
            nrm = Vector((0, -(-th), td)); nrm = Vector((0, th, td))
            sz = R.uniform(0.36, 0.5) * (1.0 - 0.25 * s)
            ivy_card(bm, uvl, c + nrm.normalized() * R.uniform(0.0, 0.025), up, nrm, sz, sz, IVY_CELLS[(k + int(s * 7)) % 2] if (k + int(s * 7)) % 4 else IVY_DARK,
                     bow=0.02)
            s += 0.09 + R.uniform(0, 0.025)
    bm.normal_update(); FK._merge(pc, bm, 'leaf')
    return pc

STEP_RISE, STEP_RUN = 4.0 / 18, 6.6 / 18
def p_stair_step():
    pc = FK.Piece('StairStep')
    poly = [(0.04, -1.0), (0.04, 0.095), (0.0, 0.115), (0.0, 0.185), (0.012, 0.21), (0.04, 0.22), (0.40, 0.22), (0.40, -1.0)]
    extrude_x(pc, 'stone', poly, -1.0, 1.0, nx=6, dip=0.012)
    return pc

def balustrade(name, rake_steps=0):
    pc = FK.Piece(name); R = R_(name)
    if not rake_steps:
        L = 2.0; a = 0.0; rise = 0.0
    else:
        L = rake_steps * STEP_RUN; a = math.atan2(STEP_RISE, STEP_RUN); rise = L * math.tan(a)
    slope = math.tan(a)
    hb = 0.22; hr = 0.15; hbal = 1.0 - hb - hr
    ln_rail = L / math.cos(a)
    # base rail and top rail follow the slope; origin at the middle of the base underside
    for (z0, hh, w, nm, cham) in ((0.0, hb, 0.34, 'base', 0.045), (1.0 - hr, hr, 0.30, 'rail', 0.05)):
        n_seg = 2 if ln_rail < 2.3 else 3
        seglen = ln_rail / n_seg
        for sidx in range(n_seg):
            u = -ln_rail / 2 + seglen * (sidx + 0.5)
            c = (u * math.cos(a), 0.0, z0 + hh / 2 + u * math.sin(a))
            fbox(pc, 'stone', (seglen - 0.02, w, hh), c, rot=Ry(-math.degrees(a)), cham=cham, segs=2, cuts=(1, 0, 0), wob=0.006,
                 seed=R.random() * 50 + sidx)
    # balusters: fat vase profile, vertical, every step (raked) or 0.28 m (flat)
    xs = []
    if rake_steps:
        xs = [(-L / 2) + STEP_RUN * (k + 0.5) for k in range(rake_steps)]
    else:
        xs = [-0.84 + 0.28 * k for k in range(7)]
    for k, x in enumerate(xs):
        zb = hb + x * slope - 0.01
        prof = [(0.0, 0.0), (0.062, 0.0), (0.078, 0.025), (0.052, 0.08), (0.066, 0.13), (0.099, 0.22), (0.103, 0.30), (0.082, 0.38), (0.052, 0.48),
                (0.066, 0.54), (0.048, 0.60), (0.074, 0.645), (0.074, hbal + 0.012), (0.0, hbal + 0.012)]
        sc = R.uniform(0.94, 1.06)
        prof = [(r * sc * 1.18, h * hbal / 0.63) for (r, h) in prof]
        lathe(pc, 'stone', prof, (x, R.uniform(-0.008, 0.008), zb), sides=8, seed=R.random() * 70, lean=(R.uniform(-0.8, 0.8), R.uniform(-0.8, 0.8)), jit=0.05)
    return pc

def p_balustrade():
    return balustrade('Balustrade', 0)

def p_balustrade_raked():
    return balustrade('BalustradeRaked', 6)

def p_newel():
    pc = FK.Piece('Newel'); R = R_('newel')
    fbox(pc, 'stone', (0.4, 0.4, 0.24), (0, 0, 0.12), cham=0.04, segs=2, cuts=(1, 1, 0), wob=0.006, seed=1.0)
    fbox(pc, 'stone', (0.31, 0.31, 0.60), (0, 0, 0.54), cham=0.035, segs=2, cuts=(0, 0, 1), wob=0.008, seed=2.0)
    for (sx, sy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):   # raised panels on the four faces of the shaft
        fbox(pc, 'stone', (0.19 if sy == 0 else 0.19, 0.19 if sx == 0 else 0.19, 0.34) if False else (0.03 if sx else 0.19, 0.03 if sy else 0.19, 0.32),
             (sx * 0.16, sy * 0.16, 0.56), cham=0.01, wob=0.0, seed=3.0)
    fbox(pc, 'stone', (0.4, 0.4, 0.12), (0, 0, 0.90), cham=0.04, segs=2, wob=0.006, seed=4.0)
    fbox(pc, 'stone', (0.26, 0.26, 0.05), (0, 0, 0.985), cham=0.015, wob=0.0, seed=5.0)
    hull(pc, 'stone', [(sx * 0.15, sy * 0.15, 1.01) for sx in (-1, 1) for sy in (-1, 1)] + [(0, 0, 1.12)], cham=0.012)
    lathe(pc, 'stone', [(0.0, 1.04), (0.06, 1.045), (0.092, 1.09), (0.1, 1.135), (0.085, 1.18), (0.045, 1.2), (0.0, 1.2)], (0, 0, 0), sides=10, seed=9.0, jit=0.03)
    return pc

def p_bench():
    pc = FK.Piece('Bench'); R = R_('bench')
    fbox(pc, 'stone', (1.8, 0.5, 0.13), (0, 0, 0.435), cham=0.04, segs=3, cuts=(3, 1, 0), wob=0.008, seed=1.0)
    for sx in (-1, 1):
        x = sx * 0.62
        fbox(pc, 'stone', (0.34, 0.44, 0.07), (x, 0, 0.035), cham=0.025, segs=2, wob=0.006, seed=sx + 2)       # foot
        fbox(pc, 'stone', (0.18, 0.36, 0.20), (x, 0, 0.17), cham=0.03, segs=2, cuts=(0, 0, 1), wob=0.008, seed=sx + 3)   # leg
        fbox(pc, 'stone', (0.34, 0.44, 0.06), (x, 0, 0.34), cham=0.025, segs=2, wob=0.006, seed=sx + 4)        # capital
    return pc

PIECES = [('WallSegment', p_wall_segment), ('WallEnd', p_wall_end), ('Buttress', p_buttress), ('Planter', p_planter),
          ('StairStep', p_stair_step), ('Balustrade', p_balustrade), ('BalustradeRaked', p_balustrade_raked), ('Newel', p_newel),
          ('Bench', p_bench)]
SMOOTH = {'M_B9_Foliage', 'KT_Soil'}

# ----------------------------------------------------------------------------- wear (authoring frame; R damp/grime, G moss, B polish)
def sstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)

def pnoise(p, k=1.7, o=0.0):
    """Noise that repeats every 2 m along x, so wall segments join without a wear seam."""
    a = math.pi * p.x
    return noise.noise(Vector((math.cos(a) * 0.9 * k / 1.7, math.sin(a) * 0.9 * k / 1.7, p.z * k * 0.55 + o + p.y * 0.8)))

def wear_at(name, p, n, mname):
    if mname in (LEAF, 'KT_Soil'):
        return 0.0, 0.0, 0.0
    nz = pnoise(p); st = pnoise(p, 3.1, 4.0)
    nzp = noise.noise(p * 1.7); stp = noise.noise(Vector((p.x * 3.1, p.y * 3.1, p.z * 0.5)))
    r = 0.04 + 0.07 * max(0.0, nz) + 0.16 * max(0.0, noise.noise(p * 3.9 + Vector((3, 1, 7)))); g = 0.0; b = 0.0
    up = n.z > 0.5
    if name in ('WallSegment', 'WallEnd', 'Buttress'):
        h = 0.85 + 0.35 * nz + 0.15 * st
        if p.z < h:
            r = max(r, 0.92 - 0.3 * max(0.0, p.z) / h)                      # damp foot: dark wet courses up to a ragged line
            g = max(g, 0.42 * max(0.0, nz * 1.6 + 0.1) * (1 if n.y < 0.3 else 0.5))   # splash moss low down
        elif p.z < h + 0.55:
            r = max(r, 0.32 * (1 - (p.z - h) / 0.55) * (0.7 + 0.5 * st))
        if up and p.z < 0.6 and name != 'WallEnd':
            g = max(g, 0.5 * (0.6 + 0.4 * nz))                                 # plinth ledge
        if 3.5 < p.z < 3.82:
            r = max(r, 0.4 * (0.6 + 0.6 * abs(st)) * sstep(3.5, 3.78, p.z))   # run-off under the string course
        if up and 3.78 < p.z < 4.03:
            g = max(g, 0.45 * (0.6 + 0.5 * nz)); r = max(r, 0.2)               # moss on the string-course ledge
        if 4.3 < p.z < 4.64:
            r = max(r, 0.4 * sstep(4.3, 4.6, p.z) * (0.7 + 0.5 * st))          # run-off under the coping
        if p.z >= 4.6 and up:
            g = max(g, 0.62 + 0.38 * (nz + 0.2)); r = max(r, 0.25)             # coping top: thick moss
            b = 0.0
        elif p.z >= 4.6:
            r = max(r, 0.25 + 0.2 * abs(st))
        if name == 'Buttress':
            if 3.5 < p.z <= 4.02 and n.z > 0.2:
                g = max(g, 0.55 * (0.6 + 0.4 * nzp)); r = max(r, 0.25)           # weathered top
            if 1.0 < p.z < 3.4:
                r = max(r, 0.2 * max(0.0, stp + 0.2))
        if name == 'WallSegment' and p.y > 0.3 and p.z > 4.0:
            r = max(r, 0.22)
    if name == 'Planter':
        r = 0.12 + 0.2 * max(0.0, nzp)
        if p.z < 0.12:
            r = max(r, 0.5)
        if up:
            g = 0.4 * max(0.0, nzp + 0.2)
    if name == 'StairStep':
        xa = abs(p.x)
        if up and p.z > 0.19:
            b = 0.95 * math.exp(-(p.x / 0.52) ** 2) * (0.75 + 0.25 * nzp) * (1.0 if p.y < 0.28 else 0.7)
            r = 0.1 + 0.4 * sstep(0.7, 1.0, xa)
            g = 0.35 * sstep(0.75, 1.0, xa) * max(0.0, nzp + 0.4)
        else:
            r = 0.15 + 0.4 * max(0.0, -n.z) + 0.25 * sstep(0.8, 1.0, xa)
            if p.z < -0.3:
                r = max(r, 0.6)
        if p.z > 0.1 and p.y < 0.06:
            b = max(b, 0.7 * math.exp(-(p.x / 0.6) ** 2))                       # nosing polished
    if name.startswith('Balustrade'):
        if mname == 'KT_Dressed':
            if up:
                b = 0.28 * (0.6 + 0.4 * nzp); g = 0.25 * max(0.0, nzp) if p.z < 0.5 else 0.0
            r = max(r, 0.18 + 0.3 * max(0.0, 1 - ((p.z - 0.0) % 1.0) / 0.35) * (1 if p.z < 0.9 else 0))
            if p.z > 0.9:
                r = max(r, 0.2)
    if name == 'Newel':
        if p.z < 0.45:
            r = max(r, 0.5 * (1 - p.z / 0.45) + 0.1)
        if p.z > 0.9 and up:
            b = 0.35; g = 0.2 * max(0.0, nzp)
        if p.z > 1.0:
            r = max(r, 0.15); b = max(b, 0.3 * max(0.0, n.z))
    if name == 'Bench':
        if up and p.z > 0.47:
            b = 0.7 * math.exp(-(p.x / 0.7) ** 2) * (0.7 + 0.3 * nzp); g = 0.2 * max(0.0, nzp) * (1 if abs(p.y) > 0.15 else 0)
        r = max(r, 0.2 + 0.5 * max(0.0, 1 - p.z / 0.15))
    return min(1.0, r), min(1.0, g), min(1.0, b)

def write_wear(ob):
    me = ob.data
    if 'wear' in me.color_attributes:
        me.color_attributes.remove(me.color_attributes['wear'])
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    mats = [m.name for m in me.materials]
    inv = lambda v: Vector((-v.x, -v.y, v.z))      # Blender local -> authoring frame
    for poly in me.polygons:
        n = inv(poly.normal)
        mname = mats[poly.material_index] if mats else ''
        for li in poly.loop_indices:
            p = inv(me.vertices[me.loops[li].vertex_index].co)
            attr.data[li].color = (*wear_at(ob.get('piece', ob.name), p, n, mname), 1.0)

# ----------------------------------------------------------------------------- build
def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def to_object(pc, parent):
    bmesh.ops.transform(pc.bm, matrix=Rz(180), verts=pc.bm.verts[:])
    me = bpy.data.meshes.new(pc.name)
    pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
    ob['piece'] = pc.name
    ob.parent = parent
    bpy.context.view_layer.update()
    write_wear(ob)
    return ob

def build():
    root = bpy.data.objects.new('KeepTerrace', None); bpy.context.scene.collection.objects.link(root)
    obs = {}; total = 0
    for name, fn in PIECES:
        ob = to_object(fn(), root)
        obs[name] = ob; t = tris(ob); total += t
        bb = [Vector(c) for c in ob.bound_box]
        # Unity extents (x, y, z) = Blender (-x, z, -y)
        ux = sorted([-c.x for c in bb]); uy = sorted([c.z for c in bb]); uz = sorted([-c.y for c in bb])
        print('  %-16s %6d tris  %d slots  %s\n      Unity extent x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f' %
              (name, t, len(ob.data.materials), ', '.join(m.name for m in ob.data.materials), ux[0], ux[-1], uy[0], uy[-1], uz[0], uz[-1]))
    print('KeepTerrace total tris: %d (cap 30000)' % total)
    return root, obs

# ----------------------------------------------------------------------------- review scene
def W(x, y, z):
    """Unity coordinates -> Blender world (Unity (x, y, z) = Blender (-x, -z, y) for the Rz(180) kit)."""
    return Vector((-x, -z, y))

def instance(obs, name, ux, uy, uz, yaw=0.0):
    src = obs[name]
    ob = bpy.data.objects.new(name + '_i', src.data); ob['piece'] = name
    bpy.context.scene.collection.objects.link(ob)
    ob.location = W(ux, uy, uz); ob.rotation_euler = (0, 0, math.radians(yaw))
    return ob

def ground(sc, y, x0, x1, z0, z1):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    pts = [W(x0, y, z0), W(x1, y, z0), W(x1, y, z1), W(x0, y, z1)]
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    if f.normal.z < 0:
        f.normal_flip()
    for l in f.loops:
        l[uvl].uv = (l.vert.co.x / TILE, l.vert.co.y / TILE)
    me = bpy.data.meshes.new('Ground'); bm.to_mesh(me); bm.free()
    me.materials.append(FK.tile_mat('FK_Cobble', 'B1_town/cobble'))
    ob = bpy.data.objects.new('Ground', me); sc.collection.objects.link(ob)
    a = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    for d in a.data:
        d.color = (0.25, 0, 0, 1)

def proxy_prism(sc, name, pts_xyz, mat):
    """Review-only fill (stair core): hull of Unity points."""
    bm = bmesh.new()
    vs = [bm.verts.new(W(*p)) for p in pts_xyz]
    bmesh.ops.convex_hull(bm, input=vs); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob)
    a = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    for d in a.data:
        d.color = (0.3, 0.05, 0, 1)

WALL_Z = 19.6
def build_assembly(obs):
    sc = bpy.context.scene
    for o in obs.values():
        o.hide_render = True; o.hide_viewport = True
    # 12 m of wall, x -6..6, with a 2.8 m gap for the stair
    for cx in (-5.0, -3.0, 3.0, 5.0):
        instance(obs, 'WallSegment', cx, 0, WALL_Z)
    for cx in (-1.7, 1.7):
        instance(obs, 'WallEnd', cx, 0, WALL_Z)
    for cx in (-4.0, 4.0):
        instance(obs, 'Buttress', cx, 0, WALL_Z)
    for cx in (-4.7, 3.3):
        instance(obs, 'Planter', cx, 4.9, WALL_Z)
    # the stair: 18 steps, rise 4 m, run 6.6 m, climbing +z through the gap to the terrace at z = 19.6
    z_foot = WALL_Z - 6.6
    for k in range(18):
        instance(obs, 'StairStep', 0, STEP_RISE * k, z_foot + STEP_RUN * k).scale = (1.1, 1, 1)
    # raked balustrades on both sides (3 pieces of 6 steps each), newels at the foot and the head
    slope = STEP_RISE / STEP_RUN
    for sx in (-1.25, 1.25):
        for i in range(3):
            zc = z_foot + STEP_RUN * (6 * i + 3)
            yb = STEP_RISE * (6 * i + 3) + 0.0
            o = instance(obs, 'BalustradeRaked', sx, yb + 0.04, zc, yaw=90)
        instance(obs, 'Newel', sx, 0, z_foot - 0.12)
        instance(obs, 'Newel', sx, 4.0, WALL_Z + 0.25)
    # benches on the terrace by the parapet
    instance(obs, 'Bench', -4.2, 4.0, WALL_Z + 1.55)
    instance(obs, 'Bench', 4.4, 4.0, WALL_Z + 1.55, yaw=180)
    # ground and terrace paving, and a review-only core under the flight
    ground(sc, 0.0, -60, 60, -30, WALL_Z + 0.1)
    ground(sc, 4.0, -60, 60, WALL_Z + 0.6, 80)
    proxy_prism(sc, 'TerraceSlab', [(sx, y, z) for sx in (-60, 60) for (y, z) in ((3.2, WALL_Z + 0.6), (3.97, WALL_Z + 0.6), (3.97, 80), (3.2, 80))], EXTRA['rubble']())
    ground(sc, 4.0, -1.4, 1.4, WALL_Z - 0.2, WALL_Z + 0.7)
    proxy_prism(sc, 'StairCore', [(sx, y, z) for sx in (-1.4, 1.4) for (y, z) in ((0, z_foot + 0.3), (0, WALL_Z), (3.99 - 0.05, WALL_Z), (STEP_RISE - 0.05, z_foot + 0.3))],
                FK.MATS.get('KT_Rubble') or EXTRA['rubble']())

def look(cam, frm, to):
    cam.location = frm; cam.rotation_euler = (to - frm).to_track_quat('-Z', 'Y').to_euler()

ASSEMBLY_CAMS = {   # Unity (from, to, vertical fov)
    'wall': ((-5.5, 2.6, 5.0), (0.0, 2.6, WALL_Z), 56),
    'stairs': ((7.0, 2.4, 11.0), (0.0, 2.2, WALL_Z - 2.0), 55),
    'close': ((-7.0, 3.6, 14.8), (-4.3, 4.3, WALL_Z), 46),
    'foot': ((-6.0, 0.9, 16.5), (-4.0, 0.6, WALL_Z), 45),
    'terrace': ((-0.5, 5.6, 25.5), (-1.0, 4.2, WALL_Z), 58),
}
LINEUP = [('WallSegment', -4.6, 0), ('WallEnd', -2.2, 0), ('Buttress', -0.5, 0), ('Planter', 2.0, 0), ('Bench', 4.6, 0),
          ('Newel', 6.4, 0), ('Balustrade', 8.0, 0), ('StairStep', 10.1, 0)]
LINEUP = [(n, x * 1.0, y) for (n, x, y) in LINEUP]
LINE_CAMS = {'line': ((2.8, 2.6, -13.5), (2.8, 2.0, 0.0), 58)}

def setup_light(sc):
    FK.RRES = RRES; FK.SAMPLES = SAMPLES
    cam = FK.review_lights(sc)
    sun = bpy.data.objects['Sun']
    sun.rotation_euler = (math.radians(38), 0, math.radians(215))
    sc.view_settings.exposure = float(FK.arg('--expo', '0.0'))
    cam.data.clip_start = 0.05; cam.data.clip_end = 200
    return cam

def review(root, obs):
    sc = bpy.context.scene
    cam = setup_light(sc)
    views = FK.arg('--views', 'wall,stairs,close,terrace' if '--assemble' in argv else 'line').split(',')
    if '--assemble' in argv:
        build_assembly(obs)
        cams = ASSEMBLY_CAMS
        for v in views:
            frm, to, fov = cams[v]
            look(cam, W(*frm), W(*to)); cam.data.angle = math.radians(fov)
            sc.render.filepath = os.path.join(OUT, 'terrace_%s.png' % v)
            bpy.ops.render.render(write_still=True); print('rendered', v)
        return
    # loose kit lined up on a ground plane, in the contract frame (square side toward the camera at Blender +y)
    for o in obs.values():
        o.hide_render = True; o.hide_viewport = True
    for name, x, yaw in LINEUP:
        instance(obs, name, x, 0, 0, yaw)
    ground(sc, 0.0, -8, 16, -4, 3)
    # lineup viewed from the square side (Unity -z = Blender +y)
    for v in views:
        frm, to, fov = LINE_CAMS['line']
        look(cam, W(frm[0], frm[1], -frm[2] * -1 if False else frm[2]), W(*to)); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, 'terrace_%s.png' % v)
        bpy.ops.render.render(write_still=True); print('rendered', v)

def export_kit(root, ed, sheet_dir=None):
    os.makedirs(ed, exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(o == root or o.parent == root)
    bpy.ops.export_scene.fbx(filepath=os.path.join(ed, 'KeepTerrace.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                             mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR', path_mode='STRIP')
    for suf in ('_BaseColor.png', '_Normal.png', '_ORM.png'):
        dst = os.path.join(ed, 'T_B9_Foliage' + suf)
        if not os.path.exists(dst):
            shutil.copy(ATLAS + suf, dst)
    print('exported', os.path.join(ed, 'KeepTerrace.fbx'))

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, obs = build()
    if '--export' in argv:
        export_kit(root, os.path.abspath(FK.arg('--export-dir', OUT)))
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'terrace.blend'))
    if '--no-render' not in argv:
        review(root, obs)
