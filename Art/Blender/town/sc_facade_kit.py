"""FacadeKit: B4 half-timbered facade kit (T2, PHASE1 4), assembled into the Hollowmere beauty-corner facade.

One two-storey half-timbered house front, 7.5 m wide: a dressed sandstone plinth (0.6 m), a ground storey with a
planked green-painted door (2.2 m clear, iron strap hinges with curled ends, ring pull) on a worn sandstone step and one leaded
window, a jettied upper storey (0.45 m, joist ends showing under a sagging bressumer) with two leaded windows whose
painted green shutters (faded sage-forest, brief 0) stand open against the wall, adzed oak framing at about 1.4x real section
(braced panels, St Andrew's crosses under the upper windows), warm cream lime plaster infill with a faint bulge, a
red clay tile roof (tile courses, half-round ridge tiles, sagging ridge, chunky overhang with rafter tails and
barge boards) and one sandstone chimney stack with two clay pots. Front plus the building's own LEFT return (+X
here, the end the mid camera sees; brief section 1 "left 2 m" read from inside the house) get the detail; the
back, the right (-X) end and the far part of the left end are plain plaster planes on the shared tileable.

Kit pieces (each one builder function, joined per piece into one object, brief/PHASE1 B4): Kit_Plinth, Kit_Posts,
Kit_Rails (sole plates, girts, wall plates, sill and head rails), Kit_Braces, Kit_JettyBeams (bressumer + joists),
Kit_Panels (plaster infill, soffit, gable, plain back/right planes), Kit_Windows (casings, sills, mullions, glass),
Kit_Shutters, Kit_Door, Kit_RoofModule (deck, tile courses, ridge, rafters, barge boards), Kit_Chimney, Kit_FlowerBox
(flat leaf/bloom placeholders until B9 foliage), Kit_Decals.
T2: no unique bake. Every piece samples the shared graded tileables (Art/Textures/B1_town: plaster, oak_timber,
sandstone, roof_tile, painted_wood, iron) with metre UVs (UV0, 2 m per tile = 1024 px/m on the 2K sets) plus the
B3 decals (drip, crack, moss) on offset quads. Placed wear lives in the CORNER float colour attribute "wear" (linear 0-1):
R = grime/soot (mix toward a dark warm grime colour; splashback band at the foot, soot round the chimney top, hand grime at the latch),
G = moss (plinth top, roof eave courses, chimney cap), B = polish (hand-worn door edge and step); the controller's
URP shader reads it (review renders here use the same blend). Shutters: painted_wood x tint SHUTTER_TINT.

refs: Reference/hollowmere/architecture/REFS.md (Alsace/Colmar half-timber: plinth ~0.5-0.7 m, jetty 0.3-0.6 m,
posts 18-22 cm real -> 26-30 cm here, panels ~0.8-1.4 m), PHASE1 3.1 palette, 3.4 shape language.

Axes (Blender): z up, front -Y, metres. Origin = bottom centre of the ground-storey plaster line (front face of the
plaster at y 0, plinth stands 6 cm proud). Width x -3.75..3.75, depth y 0..7.0 (upper storey front at y -0.45),
eave (wall plate) 6.0 m, ridge 9.6 m (sags 22 cm at mid), chimney top about 11.1 m; lean-to on the +X end. Unity placement (brief 1):
local origin at (-1.25, 0, 7.5), facing -Z. Export FBX -Z forward, Y up, transforms applied.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_facade_kit.py -- --out <scratch>
      [--views wide,mid,close,tq] [--rres 960] [--samples 48] [--no-render] [--export] [--blend]
  --export writes <out>/FacadeKit.fbx (cloud sessions commit only this generator; the PC exports to
  Assets/_Project/Content/Art/Town/B4/FacadeKit/). Renders go to <out>/facade_<view>.png. Review cameras are the
  brief's capture cameras (Unity (x,y,z) -> Blender (x,z,y)), vertical FOV 46/42/38 as LookTestBuilder.

Budget (PHASE1 3.3 building, <= 75k tris): see the tris line printed per kit piece. Collider: one box per storey
plus the plinth and step (chosen at placement).

Status (2026-10-31, cloud): round 13: upper windows 1.15 x 1.25 m clear with bigger shutters (Eguisheim ref
proportion). Self-review 30.5/40 (silhouette 4, proportion 4, form 4, surface 3.5, colour 4, wear 3.5, set 3.5,
glass/metal 3.5); target 31.
Round 12 (TotK study): plinth is two courses of a few big pillowy stones over a dark
recessed joint core, shade-damp band under the deep eave, ragged lifted eave course. Self-review 30/40.
Round 11 from the Unity corner review: plaster smooth-shaded and welded (the flat
cells showed as dotted vertical seams in Unity), grime up to ~0.85 at the foot and a sheltered band under the jetty,
timbers 7 cm proud and a shade darker (OAK_TINT) so the frame reads at the wide camera. Self-review 29.5/40.
Earlier: review 28.5/40 at round 10 (calibrated rubric, brief 3). First review 26/40 (round 6);
target 31 not reached after the 4-round limit: r7 +1 (plaster, flower box), r8 +0.5 (lean, kick, edge grime), r9 +1
(heavy roof cap with rolled eave, deep-set windows, lean-to woodshed), r10 ~0 (lime patches, paint chips, bleached
timber tops: too subtle at camera distance). About 31k tris, audit PASS (8 tiny degenerate faces in the hinge curls).
Next visible gains: plaster value variation that survives distance (darker lime-wash panels, not noise), a dormer
or oriel on the front slope, proud peg heads, B9 foliage in the flower box, a door hood beside B8's lantern; and a
layout table so width, storeys and pitch can vary for neighbour houses. In Unity the wear attribute needs the kit's
vertex-mask shader.
"""
import sys, os, math, random, zlib
import bpy, bmesh
from mathutils import Vector, Matrix, noise

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
VIEWS = arg('--views', 'wide,mid,close,tq').split(',')
RRES = int(arg('--rres', '960')); SAMPLES = int(arg('--samples', '48'))
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
TEX = os.path.join(REPO, 'Art', 'Textures')
SEED = 0xB4
TILE = 2.0                      # metres per tileable repeat (2K at 1024 px/m)
SHUTTER_TINT = (0.57, 0.73, 0.28)  # painted_wood (#627b85) -> faded sage-forest green, about #4a6a48 (brief 0)
DOOR_TINT = (0.50, 0.66, 0.25)     # the door a shade deeper green
OAK_TINT = (0.80, 0.76, 0.72)      # a shade darker than the tileable: the frame reads at the wide camera
DECAL_OPACITY = {'drip': 0.35, 'crack': 0.9, 'moss': 1.0}

# ----------------------------------------------------------------------------- dimensions (metres)
W = 3.75                         # half width of the front
DEPTH = 7.0
PLINTH = 0.60
TIM = 0.28                       # post / rail section (1.4x real)
PROUD = 0.07                     # timber face stands this far in front of the plaster (deep relief, Unity review r10)
GIRT0, GIRT1 = 2.80, 3.06        # ground-storey head beam
JETTY = 0.45
JOIST0, JOIST1 = 3.06, 3.28
BRES0, BRES1 = 3.28, 3.58        # bressumer = upper sole plate
PLATE0, PLATE1 = 5.78, 6.04      # wall plate (eave)
RIDGE = 9.6                      # heavy cap: roof about 38% of the height (brief 5)
RIDGE_SAG = 0.22
OVER_EAVE, OVER_VERGE = 0.85, 0.55
CHIM_TOP = 10.9
DOOR = (-0.68, 0.42, 0.16, 2.36)   # x0, x1, z0 (threshold top), z1: 1.10 x 2.20 clear
GWIN = (1.35, 2.65, 1.33, 2.25)    # ground window clear opening
UWIN = [(-2.55, -1.40), (0.78, 1.93)]   # 1.15 m clear: shutters read as big as the Eguisheim refs
UWIN_Z = (4.12, 5.37)
YU = -JETTY                      # upper storey plaster plane

def rng(stream):
    return random.Random(SEED ^ zlib.crc32(stream.encode()))

# ----------------------------------------------------------------------------- materials
MATS = {}
def _img(path, non_color):
    im = bpy.data.images.load(path, check_existing=True)
    if non_color:
        im.colorspace_settings.name = 'Non-Color'
    return im

def tile_mat(name, folder, stem=None, tint=(1, 1, 1), rough_add=0.0, normal=1.0):
    if name in MATS:
        return MATS[name]
    stem = stem or folder.split('/')[-1]
    base = os.path.join(TEX, folder, stem)
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links
    bsdf = N['Principled BSDF']
    uv = N.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'
    alb = N.new('ShaderNodeTexImage'); alb.image = _img(base + '_albedo.png', False)
    nrm = N.new('ShaderNodeTexImage'); nrm.image = _img(base + '_normal.png', True)
    orm = N.new('ShaderNodeTexImage'); orm.image = _img(base + '_orm.png', True)
    for t in (alb, nrm, orm):
        L.new(uv.outputs[0], t.inputs[0])
    col = N.new('ShaderNodeVertexColor'); col.layer_name = 'wear'
    sep = N.new('ShaderNodeSeparateColor'); L.new(col.outputs[0], sep.inputs[0])
    tin = N.new('ShaderNodeMix'); tin.data_type = 'RGBA'; tin.blend_type = 'MULTIPLY'
    tin.inputs[0].default_value = 1.0; tin.inputs[7].default_value = (*tint, 1)
    L.new(alb.outputs[0], tin.inputs[6])
    # R: grime / soot -> darker, warm-grey
    gri = N.new('ShaderNodeMix'); gri.data_type = 'RGBA'; gri.blend_type = 'MIX'
    gri.inputs[7].default_value = (0.055, 0.045, 0.036, 1)
    L.new(sep.outputs[0], gri.inputs[0]); L.new(tin.outputs[2], gri.inputs[6])
    # B: polish -> lighter
    pol = N.new('ShaderNodeMix'); pol.data_type = 'RGBA'; pol.blend_type = 'SCREEN'
    pol.inputs[7].default_value = (0.30, 0.26, 0.20, 1)
    L.new(sep.outputs[2], pol.inputs[0]); L.new(gri.outputs[2], pol.inputs[6])
    # G: moss, broken up by noise
    tco = N.new('ShaderNodeTexCoord')
    nz = N.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3.0; nz.inputs['Detail'].default_value = 6
    L.new(tco.outputs['Object'], nz.inputs['Vector'])
    mramp = N.new('ShaderNodeMapRange'); L.new(nz.outputs[0], mramp.inputs[0])
    mramp.inputs[1].default_value = 0.42; mramp.inputs[2].default_value = 0.62
    mm = N.new('ShaderNodeMath'); mm.operation = 'MULTIPLY'
    L.new(sep.outputs[1], mm.inputs[0]); L.new(mramp.outputs[0], mm.inputs[1])
    mcol = N.new('ShaderNodeMix'); mcol.data_type = 'RGBA'
    mcol.inputs[6].default_value = (0.10, 0.13, 0.03, 1); mcol.inputs[7].default_value = (0.30, 0.30, 0.04, 1)
    L.new(nz.outputs[0], mcol.inputs[0])
    mos = N.new('ShaderNodeMix'); mos.data_type = 'RGBA'
    L.new(mm.outputs[0], mos.inputs[0]); L.new(pol.outputs[2], mos.inputs[6]); L.new(mcol.outputs[2], mos.inputs[7])
    L.new(mos.outputs[2], bsdf.inputs['Base Color'])
    so = N.new('ShaderNodeSeparateColor'); L.new(orm.outputs[0], so.inputs[0])
    ra = N.new('ShaderNodeMath'); ra.operation = 'ADD'; ra.use_clamp = True
    L.new(so.outputs[1], ra.inputs[0]); ra.inputs[1].default_value = rough_add
    ra2 = N.new('ShaderNodeMath'); ra2.operation = 'MULTIPLY_ADD'; ra2.use_clamp = True
    L.new(mm.outputs[0], ra2.inputs[0]); ra2.inputs[1].default_value = 0.25; L.new(ra.outputs[0], ra2.inputs[2])
    L.new(ra2.outputs[0], bsdf.inputs['Roughness'])
    L.new(so.outputs[2], bsdf.inputs['Metallic'])
    nm = N.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = normal
    L.new(nrm.outputs[0], nm.inputs['Color']); L.new(nm.outputs[0], bsdf.inputs['Normal'])
    MATS[name] = m
    return m

def glass_mat():
    if 'FK_Glass' in MATS:
        return MATS['FK_Glass']
    m = bpy.data.materials.new('FK_Glass'); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (0.012, 0.016, 0.02, 1)
    b.inputs['Roughness'].default_value = 0.12
    b.inputs['Specular IOR Level'].default_value = 0.7
    MATS['FK_Glass'] = m
    return m

def flat_mat(name, col, rough):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough
    MATS[name] = m
    return m

def decal_mat(stem):
    name = 'FK_Decal_' + stem
    if name in MATS:
        return MATS[name]
    base = os.path.join(TEX, 'B3_trims', stem, stem)
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links
    bsdf = N['Principled BSDF']
    alb = N.new('ShaderNodeTexImage'); alb.image = _img(base + '_albedo.png', False)
    nrm = N.new('ShaderNodeTexImage'); nrm.image = _img(base + '_normal.png', True)
    orm = N.new('ShaderNodeTexImage'); orm.image = _img(base + '_orm.png', True)
    L.new(alb.outputs['Color'], bsdf.inputs['Base Color'])
    am = N.new('ShaderNodeMath'); am.operation = 'MULTIPLY'; am.inputs[1].default_value = DECAL_OPACITY.get(stem, 1.0)
    L.new(alb.outputs['Alpha'], am.inputs[0]); L.new(am.outputs[0], bsdf.inputs['Alpha'])
    so = N.new('ShaderNodeSeparateColor'); L.new(orm.outputs[0], so.inputs[0])
    L.new(so.outputs[1], bsdf.inputs['Roughness'])
    nm = N.new('ShaderNodeNormalMap'); L.new(nrm.outputs[0], nm.inputs['Color']); L.new(nm.outputs[0], bsdf.inputs['Normal'])
    MATS[name] = m
    return m

def M(key):
    return {
        'plaster': lambda: tile_mat('FK_Plaster', 'B1_town/plaster', normal=0.55),
        'oak': lambda: tile_mat('FK_Oak', 'B1_town/oak_timber', tint=OAK_TINT, normal=1.2),
        'oak_lt': lambda: tile_mat('FK_OakDoor', 'B1_town/oak_timber', tint=(1.35, 1.25, 1.15)),
        'stone': lambda: tile_mat('FK_Sandstone', 'B1_town/sandstone'),
        'mortar': lambda: tile_mat('FK_PlinthJoint', 'B1_town/sandstone', tint=(0.30, 0.28, 0.24)),
        'stone_dk': lambda: tile_mat('FK_ChimneyStone', 'B1_town/sandstone', tint=(0.80, 0.74, 0.68)),
        'roof': lambda: tile_mat('FK_RoofTile', 'B1_town/roof_tile', normal=1.3),
        'shutter': lambda: tile_mat('FK_Shutter', 'B1_town/painted_wood', tint=SHUTTER_TINT),
        'door': lambda: tile_mat('FK_DoorPaint', 'B1_town/painted_wood', tint=DOOR_TINT),
        'iron': lambda: tile_mat('FK_Iron', 'B1_town/iron'),
        'glass': glass_mat,
        'leaf': lambda: flat_mat('FK_Leaf', (0.07, 0.13, 0.025), 0.6),     # placeholder until B9 swaps in its foliage
        'bloom': lambda: flat_mat('FK_Bloom', (0.45, 0.04, 0.03), 0.5),
    }[key]()

# ----------------------------------------------------------------------------- mesh building
class Piece:
    """Accumulates geometry for one kit piece (one object, a few material slots)."""
    def __init__(self, name):
        self.name = name; self.bm = bmesh.new(); self.mats = []
        self.uv = self.bm.loops.layers.uv.new('UVMap')

    def slot(self, key):
        m = M(key)
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

def _uv_project(faces, uvl, to_local, long_axis=None, off=(0.0, 0.0), grain_u=False):
    """Box-projected metre UVs. to_local maps world co -> member-local co; long_axis (0/1/2) gets the grain (v)."""
    for f in faces:
        n = (to_local.to_3x3() @ f.normal)
        a = max(range(3), key=lambda i: abs(n[i]))
        others = [i for i in range(3) if i != a]
        if long_axis is not None and long_axis in others:
            uax, vax = [o for o in others if o != long_axis][0], long_axis
        else:
            uax, vax = others
            if a != 2:          # vertical faces: v = up
                uax, vax = [o for o in others if o != 2][0], 2
        for l in f.loops:
            p = to_local @ l.vert.co
            uu, vv = (p[vax], p[uax]) if grain_u and long_axis is not None else (p[uax], p[vax])
            l[uvl].uv = (uu / TILE + off[0], vv / TILE + off[1])

def add_box(pc, mat, size, xf, cham=0.02, cuts=(0, 0, 0), long_axis=None, uv_world=False, bend=None, wob=0.0, segs=1,
            seed=0.0, off=None):
    """Chamfered box of `size` centred at the local origin, then placed by matrix `xf`.
    cuts subdivide each axis (for bends); bend(p_local) -> offset applied before placing."""
    bm = bmesh.new()
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    for ax, c in enumerate(cuts):
        if c > 0:
            for k in range(1, c + 1):
                t = -size[ax] / 2 + size[ax] * k / (c + 1)
                no = Vector((0, 0, 0)); no[ax] = 1
                co = Vector((0, 0, 0)); co[ax] = t
                geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
                bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no)
    if cham > 0:
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > 0.5]
        bmesh.ops.bevel(bm, geom=edges, offset=cham, segments=segs, affect='EDGES', profile=0.5)
    if bend or wob:
        for v in bm.verts:
            d = bend(v.co.copy()) if bend else Vector()
            if wob:
                q = v.co * 1.7 + Vector((seed, seed * 1.3, seed * 0.7))
                d += Vector((noise.noise(q), noise.noise(q + Vector((5.2, 0, 0))), noise.noise(q + Vector((0, 9.1, 0))))) * wob
            v.co += d
    bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap')
    o = off if off is not None else (rng('uv%f' % seed).random(), rng('uvv%f' % seed).random())
    if uv_world:  # stone: project in world space after placing so courses line up across pieces
        bm.transform(xf); bm.normal_update()
        _uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
    else:         # timber: grain along the member, its own random offset
        _uv_project(bm.faces, uvl, Matrix.Identity(4), long_axis, o, grain_u=mat in ('door', 'shutter'))
        bm.transform(xf); bm.normal_update()
    _merge(pc, bm, mat)

def _merge(pc, bm, mat):
    """Append bm into the piece's bmesh with material slot `mat`."""
    si = pc.slot(mat)
    me = bpy.data.meshes.new('tmp'); bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.material_index = 0
    tmp = bmesh.new(); tmp.from_mesh(me)
    uvs = tmp.loops.layers.uv.get('UVMap')
    vmap = {}
    for v in tmp.verts:
        vmap[v] = pc.bm.verts.new(v.co)
    for f in tmp.faces:
        try:
            nf = pc.bm.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            continue
        nf.material_index = si
        for l, nl in zip(f.loops, nf.loops):
            nl[pc.uv].uv = l[uvs].uv
    tmp.free(); bpy.data.meshes.remove(me)

TIMBERS = []   # (a, b, half width) of every oak member, for the plaster edge grime

def member(pc, mat, a, b, w, d, cham=0.025, name='m', sag=0.0, wob=0.006, extend=0.0, up=None):
    """A timber from point a to b (world), section w (across, in the wall plane) x d (depth, along `up` normal).
    Off true by its own small angle, sagging `sag` m at mid-length, wobbled (adzed)."""
    a = Vector(a); b = Vector(b)
    if mat == 'oak':   # remember wall-plane timbers: the plaster darkens where it meets them
        TIMBERS.append((a.copy(), b.copy(), w / 2))
    axis = (b - a); L = axis.length + 2 * extend; axis.normalize()
    up = Vector(up) if up else Vector((0, -1, 0))        # wall normal (outward)
    side = axis.cross(up).normalized(); up = side.cross(axis).normalized()
    R = rng('mem' + name)
    tilt = math.radians(R.uniform(-1.6, 1.6))
    rot = Matrix.Rotation(tilt, 4, up)
    basis = Matrix((side, up, axis)).transposed().to_4x4()   # local x=side, y=up(out), z=axis
    xf = Matrix.Translation((a + b) / 2) @ rot @ basis
    cuts = max(0, int(L / 0.6))
    def bend(p, L=L):
        t = p.z / L * 2
        return Vector((0, -sag * (1 - t * t), 0)) if sag else Vector()
    # sag along the wall plane: local x is in-plane across; for horizontals that's world z
    def bend2(p, L=L):
        t = p.z / L * 2
        return Vector((sag * (1 - t * t), 0, 0))
    add_box(pc, mat, (w, d, L), xf, cham=cham, cuts=(0, 0, cuts), long_axis=2, bend=bend2 if sag else None,
            wob=wob, seed=R.random() * 100)

def quad_grid(pc, mat, corners, nu, nv, push=None, uv_fn=None):
    """Bilinear grid patch from 4 corners (p00, p10, p11, p01). push(p) -> displaced point."""
    p00, p10, p11, p01 = [Vector(c) for c in corners]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    vs = []
    for j in range(nv + 1):
        row = []
        for i in range(nu + 1):
            u, v = i / nu, j / nv
            p = p00.lerp(p10, u).lerp(p01.lerp(p11, u), v)
            row.append(bm.verts.new(push(p, u, v) if push else p))
        vs.append(row)
    for j in range(nv):
        for i in range(nu):
            f = bm.faces.new((vs[j][i], vs[j][i + 1], vs[j + 1][i + 1], vs[j + 1][i]))
            for l in f.loops:
                l[uvl].uv = uv_fn(l.vert.co) if uv_fn else (0, 0)
    bm.normal_update()
    _merge(pc, bm, mat)

def wall_panel(pc, mat, plane, x0, x1, z0, z1, holes=(), bulge=0.012, cell=0.125, uv=None, side=False):
    """Plaster infill on a wall plane, cut on a breakpoint grid around rectangular holes.
    plane: y of a front-facing wall (normal -Y), or x of a side wall (normal +X) when side=True."""
    xs = sorted(set([x0, x1] + [h[0] for h in holes] + [h[1] for h in holes] +
                    [x0 + (x1 - x0) * k / max(1, round((x1 - x0) / cell)) for k in range(1, max(1, round((x1 - x0) / cell)))]))
    zs = sorted(set([z0, z1] + [h[2] for h in holes] + [h[3] for h in holes] +
                    [z0 + (z1 - z0) * k / max(1, round((z1 - z0) / cell)) for k in range(1, max(1, round((z1 - z0) / cell)))]))
    xs = [x for x in xs if x0 <= x <= x1]; zs = [z for z in zs if z0 <= z <= z1]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    def P(x, z):
        # bulge toward the middle of the panel, faint low-frequency trowel unevenness
        u = (x - x0) / max(1e-6, x1 - x0); v = (z - z0) / max(1e-6, z1 - z0)
        b = bulge * math.sin(math.pi * u) * math.sin(math.pi * v)
        b += 0.006 * noise.noise(Vector((x * 1.3, z * 1.3, plane)))
        if side:
            return Vector((plane + b, x, z))
        return Vector((x, plane - b, z))
    V = {}
    for x in xs:
        for z in zs:
            V[(x, z)] = bm.verts.new(P(x, z))
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            if any(h[0] < cx < h[1] and h[2] < cz < h[3] for h in holes):
                continue
            q = [V[(xs[i], zs[j])], V[(xs[i + 1], zs[j])], V[(xs[i + 1], zs[j + 1])], V[(xs[i], zs[j + 1])]]
            f = bm.faces.new(q)
            for l in f.loops:
                c = l.vert.co
                l[uvl].uv = ((c.y if side else c.x) / TILE, c.z / TILE)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')   # grid points inside holes
    bm.normal_update()
    _merge(pc, bm, mat)

# ----------------------------------------------------------------------------- kit pieces
def plinth_run(pc, a, b, face, seed):
    """Two courses of big chamfered stones along one plinth face, joints broken, each stone its own size and tilt."""
    R = rng('plinth%s%f' % (face, seed))
    zc = [(0.0, 0.31), (0.31, PLINTH)]
    for ci, (z0, z1) in enumerate(zc):
        x = a + (R.uniform(0.1, 0.35) if ci else 0.0)
        while x < b - 0.05:
            ln = min(R.uniform(0.55, 0.95), b - x)
            if b - (x + ln) < 0.25:
                ln = b - x
            g = 0.035                                     # deep joint
            h = z1 - z0 - g; dpt = 0.40 + R.uniform(-0.02, 0.03)
            cz = z0 + g / 2 + h / 2
            proud = R.uniform(0.0, 0.025)
            if face == 'front':
                xf = Matrix.Translation((x + ln / 2, 0.14 - proud, cz))
                size = (ln - g, dpt, h)
            else:
                xf = Matrix.Translation((W - 0.14 + proud, x + ln / 2, cz))
                size = (dpt, ln - g, h)
            xf = xf @ Matrix.Rotation(math.radians(R.uniform(-1.2, 1.2)), 4, 'Y' if face == 'front' else 'X')
            add_box(pc, 'stone', size, xf, cham=0.05, segs=2, cuts=(1, 0, 0) if face == 'front' else (0, 1, 0),
                    uv_world=True, wob=0.012, seed=R.random() * 90)
            x += ln

def kit_plinth():
    pc = Piece('Kit_Plinth')
    xd0, xd1 = DOOR[0] - TIM, DOOR[1] + TIM
    # a dark mortar core, recessed, then two courses of a few big pillowy stones laid over it (TotK study, brief 5)
    for (a, b) in ((-W - 0.02, xd0), (xd1, W + 0.02)):
        add_box(pc, 'mortar', (b - a, 0.36, PLINTH - 0.02), Matrix.Translation(((a + b) / 2, 0.16, (PLINTH - 0.02) / 2)),
                cham=0.0, uv_world=True)
        plinth_run(pc, a, b, 'front', seed=a)
    add_box(pc, 'mortar', (0.36, DEPTH, PLINTH - 0.02), Matrix.Translation((W - 0.16, DEPTH / 2, (PLINTH - 0.02) / 2)),
            cham=0.0, uv_world=True)
    plinth_run(pc, -0.02, DEPTH + 0.02, 'side', seed=7.0)
    # worn threshold step, dished in the middle where feet go
    def dish(p):
        u = max(0.0, 1 - (p.x / 0.55) ** 2); v = max(0.0, 1 - ((p.y + 0.05) / 0.3) ** 2)
        return Vector((0, 0, -0.035 * u * v)) if p.z > 0 else Vector()
    add_box(pc, 'stone', (xd1 - xd0 + 0.25, 0.62, DOOR[2]), Matrix.Translation(((xd0 + xd1) / 2, -0.06, DOOR[2] / 2)),
            cham=0.03, cuts=(6, 4, 0), bend=dish, wob=0.006, seed=3.3)
    return pc

def kit_posts():
    pc = Piece('Kit_Posts')
    y0, y1 = -PROUD, TIM - PROUD
    gx = [-W + TIM / 2, -2.25, DOOR[0] - TIM / 2, DOOR[1] + TIM / 2, GWIN[0] - TIM / 2, GWIN[1] + TIM / 2, W - TIM / 2]
    for i, x in enumerate(gx):
        zb = DOOR[2] if i in (2, 3) else PLINTH + 0.24
        member(pc, 'oak', (x, (y0 + y1) / 2, zb), (x, (y0 + y1) / 2, GIRT0), TIM, TIM, name='gp%d' % i)
    # left return ground posts (+X face)
    for i, y in enumerate([1.25, 2.45, 4.2, 5.7, DEPTH - TIM / 2]):
        member(pc, 'oak', (W - TIM / 2 + PROUD, y, PLINTH + 0.24), (W - TIM / 2 + PROUD, y, GIRT0), TIM, TIM,
               name='sp%d' % i, up=(1, 0, 0))
    yu0, yu1 = YU - PROUD, YU - PROUD + TIM
    ux = [-W + TIM / 2, UWIN[0][0] - TIM / 2, UWIN[0][1] + TIM / 2, -0.30, UWIN[1][0] - TIM / 2, UWIN[1][1] + TIM / 2,
          2.78, W - TIM / 2]
    for i, x in enumerate(ux):
        member(pc, 'oak', (x, (yu0 + yu1) / 2, BRES1), (x, (yu0 + yu1) / 2, PLATE0), TIM, TIM, name='up%d' % i)
    for i, y in enumerate([1.0, 2.3, 4.0, 5.6, DEPTH - TIM / 2]):
        member(pc, 'oak', (W - TIM / 2 + PROUD, y, BRES1), (W - TIM / 2 + PROUD, y, PLATE0), TIM, TIM,
               name='usp%d' % i, up=(1, 0, 0))
    # gable king post
    yc = (YU + DEPTH) / 2
    member(pc, 'oak', (W - TIM / 2 + PROUD, yc, PLATE1), (W - TIM / 2 + PROUD, yc, RIDGE - 0.55), TIM, TIM,
           name='king', up=(1, 0, 0))
    return pc, gx, ux

def kit_rails():
    pc = Piece('Kit_Rails')
    yc = TIM / 2 - PROUD
    # ground sole plate (broken at the door) and girt
    for i, (a, b) in enumerate(((-W, DOOR[0] - TIM), (DOOR[1] + TIM, W))):
        member(pc, 'oak', (a, yc, PLINTH + 0.12), (b, yc, PLINTH + 0.12), 0.24, TIM, name='sole%d' % i, extend=0.02)
    member(pc, 'oak', (-W - 0.04, yc, (GIRT0 + GIRT1) / 2), (W + 0.04, yc, (GIRT0 + GIRT1) / 2), GIRT1 - GIRT0, TIM,
           name='girt', sag=0.025)
    # door head (lintel) and ground window sill/head rails
    member(pc, 'oak', (DOOR[0] - 0.02, yc, DOOR[3] + 0.11), (DOOR[1] + 0.02, yc, DOOR[3] + 0.11), 0.22, TIM,
           name='lintel', sag=0.012)
    member(pc, 'oak', (GWIN[0] - 0.02, yc, GWIN[3] + 0.09), (GWIN[1] + 0.02, yc, GWIN[3] + 0.09), 0.18, TIM, name='gwh')
    member(pc, 'oak', (GWIN[0] - 0.02, yc, GWIN[2] - 0.09), (GWIN[1] + 0.02, yc, GWIN[2] - 0.09), 0.18, TIM, name='gws')
    # left return sole, girt
    xs = W - TIM / 2 + PROUD
    member(pc, 'oak', (xs, 0, PLINTH + 0.12), (xs, DEPTH, PLINTH + 0.12), 0.24, TIM, name='ssole', up=(1, 0, 0))
    member(pc, 'oak', (xs, 0, (GIRT0 + GIRT1) / 2), (xs, DEPTH, (GIRT0 + GIRT1) / 2), GIRT1 - GIRT0, TIM,
           name='sgirt', up=(1, 0, 0), sag=0.02)
    # upper: sill and head rails across each window bay (one panel either side)
    yu = YU - PROUD + TIM / 2
    for k, (a, b) in enumerate(UWIN):
        member(pc, 'oak', (a - TIM - 0.02, yu, UWIN_Z[0] - 0.08), (b + TIM + 0.02, yu, UWIN_Z[0] - 0.08), 0.16, TIM,
               name='us%d' % k)
        member(pc, 'oak', (a - TIM - 0.02, yu, UWIN_Z[1] + 0.08), (b + TIM + 0.02, yu, UWIN_Z[1] + 0.08), 0.16, TIM,
               name='uh%d' % k)
    member(pc, 'oak', (-W - 0.1, yu, (PLATE0 + PLATE1) / 2), (W + 0.1, yu, (PLATE0 + PLATE1) / 2), PLATE1 - PLATE0,
           TIM + 0.04, name='plate', sag=0.04)
    member(pc, 'oak', (xs, YU, (PLATE0 + PLATE1) / 2), (xs, DEPTH, (PLATE0 + PLATE1) / 2), PLATE1 - PLATE0, TIM,
           name='splate', up=(1, 0, 0), sag=0.03)
    # gable collar
    yc2 = (YU + DEPTH) / 2; zc = 7.45
    half = (RIDGE - zc) / (RIDGE - PLATE1) * (DEPTH - YU) / 2
    member(pc, 'oak', (xs, yc2 - half + 0.1, zc), (xs, yc2 + half - 0.1, zc), 0.24, TIM, name='collar', up=(1, 0, 0))
    return pc

def kit_braces():
    pc = Piece('Kit_Braces')
    yc = TIM / 2 - PROUD; zb = PLINTH + 0.24
    B = 0.22
    def br(a, b, n, y=yc, up=None, w=B):
        member(pc, 'oak', a, b, w, TIM - 0.03, name=n, cham=0.02, up=up)
    # ground storey: chevron about the -2.25 post, short brace right of the window
    br((-W + TIM, yc, zb), (-2.25 - TIM / 2, yc, GIRT0 - 0.35), 'gb0')
    br((DOOR[0] - TIM, yc, zb), (-2.25 + TIM / 2, yc, GIRT0 - 0.35), 'gb1')
    br((W - TIM, yc, zb + 0.05), (GWIN[1] + TIM, yc, GWIN[2] - 0.18), 'gb2')
    # upper storey
    yu = YU - PROUD + TIM / 2 + 0.015; z0, z1 = BRES1, PLATE0
    br((-W + TIM, yu, z0), (UWIN[0][0] - TIM, yu, 5.0), 'ub0')
    br((UWIN[0][1] + TIM, yu, z0), (-0.30 - TIM / 2, yu, 5.15), 'ub1')
    br((UWIN[1][0] - TIM, yu, z0), (-0.30 + TIM / 2, yu, 5.15), 'ub2')
    br((-0.30 - TIM / 2, yu, 5.42), (UWIN[0][1] + TIM + 0.05, yu, z1), 'ub3', w=0.18)
    br((-0.30 + TIM / 2, yu, 5.42), (UWIN[1][0] - TIM - 0.05, yu, z1), 'ub4', w=0.18)
    br((UWIN[1][1] + TIM, yu, z0), (2.78 - TIM / 2, yu, 5.0), 'ub5')
    br((W - TIM, yu, z0), (2.78 + TIM / 2, yu, 5.0), 'ub6')
    # St Andrew's crosses under the upper windows
    for k, (a, b) in enumerate(UWIN):
        zt = UWIN_Z[0] - 0.16
        br((a, yu, z0 + 0.02), (b, yu, zt), 'x%da' % k, w=0.13)
        br((b, yu, z0 + 0.02), (a, yu, zt), 'x%db' % k, w=0.13)
    # left return
    xs = W - TIM / 2 + PROUD + 0.015; up = (1, 0, 0)
    br((xs, TIM, zb), (xs, 1.25 - TIM / 2, GIRT0 - 0.4), 'sb0', up=up)
    br((xs, 2.45 - TIM / 2, zb), (xs, 1.25 + TIM / 2, GIRT0 - 0.4), 'sb1', up=up)
    br((xs, YU + TIM, z0), (xs, 1.0 - TIM / 2, 5.0), 'sb2', up=up)
    br((xs, 2.3 - TIM / 2, z0), (xs, 1.0 + TIM / 2, 5.0), 'sb3', up=up)
    yc2 = (YU + DEPTH) / 2
    br((xs, yc2 - 1.6, PLATE1), (xs, yc2 - TIM / 2, 7.3), 'gb_l', up=up)
    br((xs, yc2 + 1.6, PLATE1), (xs, yc2 + TIM / 2, 7.3), 'gb_r', up=up)
    return pc

def kit_jetty():
    pc = Piece('Kit_JettyBeams')
    n = 15
    for i in range(n):
        x = -W + 0.25 + i * (2 * W - 0.5) / (n - 1)
        R = rng('joist%d' % i)
        member(pc, 'oak', (x, 0.15, (JOIST0 + JOIST1) / 2), (x, YU - 0.10 - R.uniform(0, 0.03), (JOIST0 + JOIST1) / 2),
               0.17, JOIST1 - JOIST0, name='joist%d' % i, cham=0.03, up=(0, 0, -1))
    member(pc, 'oak', (-W - 0.10, YU - PROUD + 0.15, (BRES0 + BRES1) / 2),
           (W + 0.10, YU - PROUD + 0.15, (BRES0 + BRES1) / 2), BRES1 - BRES0, 0.32, name='bressumer', sag=0.05, cham=0.03)
    return pc

def kit_panels():
    pc = Piece('Kit_Panels')
    # ground front: one plaster sheet behind the frame, holes at the door and window
    wall_panel(pc, 'plaster', 0.0, -W, W, PLINTH + 0.2, GIRT0 + 0.05,
               holes=[(DOOR[0], DOOR[1], DOOR[2] - 0.5, DOOR[3]), (GWIN[0], GWIN[1], GWIN[2], GWIN[3])])
    wall_panel(pc, 'plaster', YU, -W, W, BRES1 - 0.05, PLATE0 + 0.05,
               holes=[(a, b, UWIN_Z[0], UWIN_Z[1]) for a, b in UWIN])
    # jetty soffit (seen from below by the wide camera)
    quad_grid(pc, 'plaster', [(-W, 0.0, JOIST1), (W, 0.0, JOIST1), (W, YU, JOIST1), (-W, YU, JOIST1)], 8, 1,
              uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    # left return walls (+X): detailed framing everywhere, plaster sheet behind
    wall_panel(pc, 'plaster', W, 0.0, DEPTH, PLINTH + 0.2, GIRT0 + 0.05, side=True)
    wall_panel(pc, 'plaster', W, YU, DEPTH, BRES1 - 0.05, PLATE0 + 0.05, side=True)
    # band between girt and bressumer on the side
    wall_panel(pc, 'plaster', W, 0.0, DEPTH, GIRT1 - 0.02, BRES1 - 0.02, side=True, bulge=0.0)
    # gable triangle (+X)
    yc = (YU + DEPTH) / 2; half = (DEPTH - YU) / 2
    def gpush(p, u, v):
        return Vector((W + 0.004 * noise.noise(p * 1.3), p.y, p.z))
    bm_pts = []
    nz = 8
    for j in range(nz):
        za, zb = PLATE1 + (RIDGE - 0.2 - PLATE1) * j / nz, PLATE1 + (RIDGE - 0.2 - PLATE1) * (j + 1) / nz
        ha = half * (1 - (za - PLATE1) / (RIDGE - 0.2 - PLATE1)); hb = half * (1 - (zb - PLATE1) / (RIDGE - 0.2 - PLATE1))
        quad_grid(pc, 'plaster', [(W, yc - ha, za), (W, yc + ha, za), (W, yc + hb, zb), (W, yc - hb, zb)],
                  max(1, int(2 * ha / 0.5)), 1, push=gpush, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    # plain planes: back wall and right end (-X), unseen by the cameras
    quad_grid(pc, 'plaster', [(W, DEPTH, 0.0), (-W, DEPTH, 0.0), (-W, DEPTH, PLATE1), (W, DEPTH, PLATE1)], 1, 1,
              uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    quad_grid(pc, 'plaster', [(-W, DEPTH, 0.0), (-W, 0.0, 0.0), (-W, 0.0, JOIST1), (-W, DEPTH, JOIST1)], 1, 1,
              uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    quad_grid(pc, 'plaster', [(-W, DEPTH, JOIST1), (-W, YU, JOIST1), (-W, YU, PLATE1), (-W, DEPTH, PLATE1)], 1, 1,
              uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    quad_grid(pc, 'plaster', [(-W, DEPTH, PLATE1), (-W, YU, PLATE1), (-W, yc + 0.001, RIDGE - 0.2), (-W, yc, RIDGE - 0.2)],
              1, 1, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    return pc

def window(pc, plane, x0, x1, z0, z1, k, sill_out=0.10):
    """Oak casing, a projecting sill, a mullion and transom, leaded glass set back in the reveal."""
    yc = plane + 0.06
    c = 0.09
    member(pc, 'oak', (x0 + c / 2, yc, z0), (x0 + c / 2, yc, z1), c, 0.12, name='wc%dl' % k, cham=0.012, wob=0.003)
    member(pc, 'oak', (x1 - c / 2, yc, z0), (x1 - c / 2, yc, z1), c, 0.12, name='wc%dr' % k, cham=0.012, wob=0.003)
    member(pc, 'oak', (x0, yc, z1 - c / 2), (x1, yc, z1 - c / 2), c, 0.12, name='wc%dt' % k, cham=0.012, wob=0.003)
    # projecting sill with a drip edge
    add_box(pc, 'oak', (x1 - x0 + 0.22, 0.20, 0.07), Matrix.Translation(((x0 + x1) / 2, plane - sill_out + 0.06, z0 + 0.01))
            @ Matrix.Rotation(math.radians(-5), 4, 'X'), cham=0.015, long_axis=0, seed=k + 0.5)
    xm = (x0 + x1) / 2
    member(pc, 'oak', (xm, yc + 0.02, z0), (xm, yc + 0.02, z1), 0.07, 0.09, name='wm%d' % k, cham=0.01, wob=0.002)
    zt = z0 + (z1 - z0) * 0.68
    member(pc, 'oak', (x0, yc + 0.02, zt), (x1, yc + 0.02, zt), 0.06, 0.09, name='wt%d' % k, cham=0.01, wob=0.002)
    # glass and lead cames (a diamond lattice of thin iron-dark bars)
    gy = plane + 0.24     # deep-set: the glass sits back in a dark oak reveal
    quad_grid(pc, 'glass', [(x0, gy, z0), (x1, gy, z0), (x1, gy, z1), (x0, gy, z1)], 1, 1,
              uv_fn=lambda c: (c.x, c.z))
    # reveals (plaster-free inner jamb in oak so the opening never shows the interior)
    for (a, b, dz) in ((x0, x0, 0), (x1, x1, 0)):
        quad_grid(pc, 'oak', [(a, plane, z0), (a, gy, z0), (a, gy, z1), (a, plane, z1)], 1, 1,
                  uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    quad_grid(pc, 'oak', [(x0, plane, z1), (x1, plane, z1), (x1, gy, z1), (x0, gy, z1)], 1, 1,
              uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    s = 0.16
    lim = [(x0, z0), (x1, z1)]
    for i in range(-12, 13):
        for sgn in (1, -1):
            # line z = sgn*(x - xm)*1.6 + zm + i*s ; clip to the pane
            zm = (z0 + z1) / 2 + i * s
            pts = []
            for x in (x0, x1):
                pts.append((x, zm + sgn * (x - xm) * 1.6))
            for z in (z0, z1):
                pts.append((xm + (z - zm) / (1.6 * sgn), z))
            pts = [p for p in pts if x0 - 1e-6 <= p[0] <= x1 + 1e-6 and z0 - 1e-6 <= p[1] <= z1 + 1e-6]
            if len(pts) < 2:
                continue
            pts.sort()
            a, b = pts[0], pts[-1]
            if (Vector(a) - Vector(b)).length < 0.05:
                continue
            ln = (Vector(b) - Vector(a)).length
            ang = math.atan2(b[1] - a[1], b[0] - a[0])
            xf = Matrix.Translation(((a[0] + b[0]) / 2, gy - 0.006, (a[1] + b[1]) / 2)) @ Matrix.Rotation(-ang, 4, 'Y')
            add_box(pc, 'iron', (ln, 0.010, 0.012), xf, cham=0.0, long_axis=0, seed=i)

def kit_windows():
    pc = Piece('Kit_Windows')
    window(pc, 0.0, GWIN[0], GWIN[1], GWIN[2], GWIN[3], 0)
    for k, (a, b) in enumerate(UWIN):
        window(pc, YU, a, b, UWIN_Z[0], UWIN_Z[1], k + 1)
    return pc

def kit_shutters():
    pc = Piece('Kit_Shutters')
    for k, (a, b) in enumerate(UWIN):
        h = UWIN_Z[1] - UWIN_Z[0] + 0.06; w = (b - a) / 2 + 0.05
        for side, hx in ((-1, a), (1, b)):
            R = rng('shut%d%d' % (k, side))
            ang = math.radians(R.uniform(14, 28))   # stands off the wall at its outer edge
            hinge = Vector((hx, YU - PROUD - 0.035, (UWIN_Z[0] + UWIN_Z[1]) / 2))
            rot = Matrix.Rotation(-side * ang, 4, 'Z')
            base = Matrix.Translation(hinge) @ rot
            # three boards with gaps, the battens and Z brace face out (the inside face, shutter open)
            for i in range(3):
                bw = w / 3 - 0.006
                cx = side * (w / 3 * (i + 0.5))
                hh = h - R.uniform(0, 0.03)
                add_box(pc, 'shutter', (bw, 0.035, hh), base @ Matrix.Translation((cx, 0, R.uniform(-0.01, 0.01))),
                        cham=0.008, long_axis=2, wob=0.003, seed=R.random() * 50)
            for zz in (-h / 2 + 0.16, h / 2 - 0.16):
                add_box(pc, 'shutter', (w - 0.05, 0.03, 0.10), base @ Matrix.Translation((side * w / 2, -0.03, zz)),
                        cham=0.01, long_axis=0, seed=R.random() * 50)
                # strap hinge on the batten
                add_box(pc, 'iron', (w * 0.72, 0.008, 0.035),
                        base @ Matrix.Translation((side * w * 0.36, -0.049, zz)), cham=0.0, long_axis=0, seed=zz)
            dl = math.hypot(w - 0.12, h - 0.42)
            add_box(pc, 'shutter', (dl, 0.03, 0.09),
                    base @ Matrix.Translation((side * w / 2, -0.03, 0)) @
                    Matrix.Rotation(side * math.atan2(h - 0.42, w - 0.12), 4, 'Y'), cham=0.01, long_axis=0, seed=k)
    return pc

def kit_door():
    pc = Piece('Kit_Door')
    x0, x1, z0, z1 = DOOR
    n = 5; R = rng('door')
    pw = (x1 - x0) / n
    yd = 0.10
    for i in range(n):
        cx = x0 + pw * (i + 0.5)
        mat = 'oak_lt' if i == 3 else 'door'    # green paint; one replaced plank left bare, paler oak
        add_box(pc, mat, (pw - 0.008, 0.06, z1 - z0 - R.uniform(0.0, 0.015)),
                Matrix.Translation((cx, yd, (z0 + z1) / 2 + R.uniform(-0.004, 0.004))) @
                Matrix.Rotation(math.radians(R.uniform(-0.6, 0.6)), 4, 'Y'),
                cham=0.012, cuts=(0, 0, 3), long_axis=2, wob=0.004, seed=R.random() * 80)
    # strap hinges with curled tips and bolt heads
    for zz in (z0 + 0.42, z1 - 0.42):
        L = 0.82
        add_box(pc, 'iron', (L, 0.014, 0.065), Matrix.Translation((x0 + L / 2 - 0.02, yd - 0.037, zz)),
                cham=0.004, cuts=(6, 0, 0), long_axis=0, seed=zz,
                bend=lambda p, L=L: Vector((0, 0, 0.010 * math.sin((p.x / L + 0.5) * math.pi * 3))))
        # curl: a small fan of short segments at the tip
        tip = Vector((x0 + L - 0.02, yd - 0.037, zz))
        for j in range(6):
            a0 = j * 0.9
            seg = Vector((math.cos(a0), 0, math.sin(a0))) * 0.05
            add_box(pc, 'iron', (0.06, 0.014, 0.024),
                    Matrix.Translation(tip + Vector((0.04 + seg.x * 0.6, 0, seg.z * 0.6))) @
                    Matrix.Rotation(-a0 - math.pi / 2, 4, 'Y'), cham=0.0, long_axis=0, seed=j + zz)
        for bx in (0.06, 0.28, 0.5, 0.7):
            add_box(pc, 'iron', (0.028, 0.02, 0.028), Matrix.Translation((x0 + bx, yd - 0.048, zz)), cham=0.006, seed=bx)
        # pintle on the post
        add_box(pc, 'iron', (0.07, 0.05, 0.05), Matrix.Translation((x0 - 0.03, yd - 0.04, zz)), cham=0.008, seed=zz + 1)
    # lock plate, ring pull, latch bar
    hx = x1 - 0.17; hz = 1.12
    add_box(pc, 'iron', (0.11, 0.012, 0.19), Matrix.Translation((hx, yd - 0.036, hz)), cham=0.006, seed=9)
    ring_r = 0.07
    for j in range(12):
        a0 = j / 12 * 2 * math.pi
        p = Vector((hx + math.sin(a0) * ring_r, yd - 0.06, hz - 0.03 - ring_r + math.cos(a0) * ring_r))
        add_box(pc, 'iron', (0.042, 0.016, 0.016), Matrix.Translation(p) @ Matrix.Rotation(-a0, 4, 'Y'), cham=0.0, seed=j)
    add_box(pc, 'iron', (0.03, 0.04, 0.03), Matrix.Translation((hx, yd - 0.05, hz - 0.02)), cham=0.006, seed=11)
    return pc

def kit_flowerbox():
    """Oak flower box on brackets under the right upper window: a break in the plaster and a red accent."""
    pc = Piece('Kit_FlowerBox')
    a, b = UWIN[1]
    zt = UWIN_Z[0] - 0.02; y = YU - PROUD - 0.24
    add_box(pc, 'oak', (b - a + 0.30, 0.30, 0.24), Matrix.Translation(((a + b) / 2, y, zt - 0.12)), cham=0.02,
            long_axis=0, wob=0.004, seed=31.0)
    for xb in (a + 0.05, b - 0.05):
        member(pc, 'oak', (xb, YU - PROUD - 0.02, zt - 0.62), (xb, y + 0.02, zt - 0.24), 0.08, 0.08,
               name='fbk%f' % xb, cham=0.012, up=(1, 0, 0))
    R = rng('flowers')
    for i in range(14):   # trailing foliage spilling over the front lip
        x = (a - 0.08) + (b - a + 0.16) * (i + R.random()) / 14
        ln = R.uniform(0.18, 0.42)
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
        bm.transform(Matrix.Translation((x, y - 0.17, zt - 0.05 - ln / 2)) @ Matrix.Diagonal((0.06, 0.05, ln / 2, 1)))
        uvl = bm.loops.layers.uv.new('UVMap'); _merge(pc, bm, 'leaf')
        if R.random() < 0.6:
            bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=R.uniform(0.03, 0.045))
            bm.transform(Matrix.Translation((x + R.uniform(-0.03, 0.03), y - 0.2, zt - 0.05 - ln * R.uniform(0.3, 0.9))))
            uvl = bm.loops.layers.uv.new('UVMap'); _merge(pc, bm, 'bloom')
    for i in range(22):
        p = Vector(((a - 0.1) + (b - a + 0.2) * R.random(), y + R.uniform(-0.1, 0.1), zt + R.uniform(0.02, 0.16)))
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=R.uniform(0.07, 0.11))
        bm.transform(Matrix.Translation(p) @ Matrix.Diagonal((1.2, 1.0, 0.8, 1)))
        uvl = bm.loops.layers.uv.new('UVMap'); _merge(pc, bm, 'leaf')
    for i in range(30):
        p = Vector(((a - 0.08) + (b - a + 0.16) * R.random(), y + R.uniform(-0.16, 0.10), zt + R.uniform(0.10, 0.30)))
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=R.uniform(0.035, 0.05))
        bm.transform(Matrix.Translation(p)); uvl = bm.loops.layers.uv.new('UVMap'); _merge(pc, bm, 'bloom')
    return pc

def ridge_z(x):
    t = x / (W + OVER_VERGE)
    return RIDGE - RIDGE_SAG * (1 - t * t)

def roof_surface(x, s):
    """Top of the front slope. s = 0 at the eave edge, 1 at the ridge. Sag grows toward the ridge."""
    y_e, y_r = YU - OVER_EAVE, (YU + DEPTH) / 2
    z_e = PLATE1 + 0.22 - OVER_EAVE * (RIDGE - PLATE1) / (y_r - YU)
    t = x / (W + OVER_VERGE)
    zr = ridge_z(x); ze = z_e - 0.05 * (1 - t * t)
    wave = 0.03 * noise.noise(Vector((x * 0.55, s * 2.5, 0.37)))  # wavy, settled courses
    kick = 0.34 * max(0.0, 1 - s / 0.22) ** 2                      # bellcast: the slope flattens at the eave
    return Vector((x, y_e + (y_r - y_e) * s, ze + (zr - ze) * s + wave * math.sin(math.pi * min(1.0, s)) + kick))

def kit_roof():
    pc = Piece('Kit_RoofModule')
    X0, X1 = -W - OVER_VERGE, W + OVER_VERGE
    nx = int((X1 - X0) / 0.42)
    # front deck (sagging), thickness under it
    y_e, y_r = YU - OVER_EAVE, (YU + DEPTH) / 2
    slope_len = (roof_surface(0, 1) - roof_surface(0, 0)).length
    TH = 0.20
    nrm_dir = Vector((0, -(RIDGE - PLATE1), (y_r - YU))).normalized()
    def deck(p, u, v):
        return roof_surface(X0 + (X1 - X0) * u, v) - nrm_dir * 0.06
    quad_grid(pc, 'roof', [(0, 0, 0)] * 4, nx, 10, push=deck, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    def under(p, u, v):
        return roof_surface(X0 + (X1 - X0) * (1 - u), v) - nrm_dir * TH
    quad_grid(pc, 'oak', [(0, 0, 0)] * 4, nx, 6, push=under, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    # back slope: plain sheared plane on the tileable
    def back(p, u, v):
        x = X0 + (X1 - X0) * u
        yb = DEPTH + OVER_EAVE * 0.6
        zr = ridge_z(x); zb = PLATE1 + 0.22 - OVER_EAVE * 0.6 * (RIDGE - PLATE1) / (DEPTH - y_r)
        return Vector((x, y_r + (yb - y_r) * v, zr + (zb - zr) * v))
    quad_grid(pc, 'roof', [(0, 0, 0)] * 4, nx, 1, push=lambda p, u, v: back(p, u, v),
              uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    # tile courses: each a tilted strip whose butt stands proud, jittered per tile run
    course = 0.40
    nc = int(slope_len / course)
    for c in range(nc):
        R = rng('course%d' % c)
        s0, s1 = c / nc, (c + 1) / nc + 0.035
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        def row(x, j):
            pb = roof_surface(x, s0) + nrm_dir * (0.045 + j)        # butt (lower edge), proud
            pt = roof_surface(x, min(1.0, s1)) + nrm_dir * 0.0
            pb0 = pb - nrm_dir * 0.05                                # underside of the butt
            return (bm.verts.new(pb0), bm.verts.new(pb), bm.verts.new(pt))
        nt = nx * 2                                                  # tile runs of about 21 cm, split
        for i in range(nt):
            xa = X0 + (X1 - X0) * i / nt; xb = X0 + (X1 - X0) * (i + 1) / nt
            j = R.uniform(-0.012, 0.012) if c else R.uniform(-0.01, 0.045)   # the eave course is ragged, lifted
            a, b = row(xa + 0.002, j), row(xb - 0.002, j + R.uniform(-0.006, 0.006))
            for q in ((a[1], b[1], b[2], a[2]), (a[0], b[0], b[1], a[1])):
                f = bm.faces.new(q)
                for l in f.loops:
                    co = l.vert.co
                    sl = c * course + (co - roof_surface(co.x, s0)).dot((roof_surface(0, 1) - roof_surface(0, 0)).normalized())
                    l[uvl].uv = (co.x / TILE + R.random() * 0.0, sl / TILE + 0.03)
        bm.normal_update()
        _merge(pc, bm, 'roof')
    # ridge: overlapping half-round tiles following the sag
    nr = int((X1 - X0) / 0.38)
    for i in range(nr):
        x = X0 + (i + 0.5) * (X1 - X0) / nr
        R = rng('ridge%d' % i)
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        segs = 8; r = 0.15; L = (X1 - X0) / nr + 0.05
        ring = []
        for e in (-1, 1):
            rr = r * (1.0 if e < 0 else 1.06)
            row = []
            for k in range(segs + 1):
                a0 = math.pi * k / segs
                row.append(bm.verts.new(Vector((x + e * L / 2, y_r - rr * math.cos(a0), ridge_z(x) + rr * math.sin(a0) * 0.85 - 0.04))))
            ring.append(row)
        for k in range(segs):
            f = bm.faces.new((ring[0][k], ring[1][k], ring[1][k + 1], ring[0][k + 1]))
            for l in f.loops:
                l[uvl].uv = (l.vert.co.x / TILE, (math.atan2(l.vert.co.z - ridge_z(x), l.vert.co.y - y_r) * r) / TILE)
        bmesh.ops.rotate(bm, verts=bm.verts[:], cent=Vector((x, y_r, ridge_z(x))),
                         matrix=Matrix.Rotation(math.radians(R.uniform(-2.5, 2.5)), 3, 'Y'))
        bm.normal_update()
        _merge(pc, bm, 'roof')
    # rafter tails under the front overhang
    for i in range(13):
        x = -W + 0.3 + i * (2 * W - 0.6) / 12
        a = roof_surface(x, 0.03) - nrm_dir * (TH + 0.09)
        b = roof_surface(x, 0.22) - nrm_dir * (TH + 0.09)
        member(pc, 'oak', a, b, 0.13, 0.18, name='raft%d' % i, cham=0.025, up=tuple(-nrm_dir))
    # fascia along the eave, barge boards on the +X verge (and a plain one at -X)
    fa = roof_surface(X0, 0) - nrm_dir * 0.12; fb = roof_surface(X1, 0) - nrm_dir * 0.12
    member(pc, 'oak', fa, fb, 0.20, 0.07, name='fascia', up=(0, -1, 0.4), sag=0.0)
    for xv, nm in ((X1 - 0.04, 'bargeR'), (X0 + 0.04, 'bargeL')):
        a = roof_surface(xv, 0) - nrm_dir * 0.10; b = roof_surface(xv, 1) - nrm_dir * 0.06
        member(pc, 'oak', a, b, 0.30, 0.08, name=nm + 'f', up=(1, 0, 0), cham=0.02)
        ba = Vector((xv, DEPTH + OVER_EAVE * 0.6, a.z)); bb = Vector((xv, y_r, b.z))
        member(pc, 'oak', ba, bb, 0.30, 0.08, name=nm + 'b', up=(1, 0, 0), cham=0.02)
    # rolled eave: a fat half-round of tile along the eave edge, the storybook "lip" of the cap
    segs = 10; rr = 0.11; ring_rows = []
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    for i in range(nx * 2 + 1):
        x = X0 + (X1 - X0) * i / (nx * 2)
        c = roof_surface(x, 0.0) + nrm_dir * 0.0 + Vector((0, 0.02, -0.02))
        row = []
        for k in range(segs + 1):
            a0 = math.pi * 0.15 + math.pi * 1.25 * k / segs
            row.append(bm.verts.new(c + Vector((0, -math.cos(a0) * rr, math.sin(a0) * rr))))
        ring_rows.append(row)
    for i in range(len(ring_rows) - 1):
        for k in range(segs):
            f = bm.faces.new((ring_rows[i][k], ring_rows[i][k + 1], ring_rows[i + 1][k + 1], ring_rows[i + 1][k]))
            for l in f.loops:
                l[uvl].uv = (l.vert.co.x / TILE, (k + (l.vert in ring_rows[i] and 0 or 0)) * rr / TILE)
    bm.normal_update()
    _merge(pc, bm, 'roof')
    return pc

def kit_leanto():
    """Lean-to woodshed on the +X end: breaks the side, a low tiled mono-pitch over a stacked woodpile."""
    pc = Piece('Kit_LeanTo')
    x0, x1, y0, y1 = W + 0.02, W + 1.55, 2.75, 5.35
    zh, zl = 2.85, 2.05
    for i, (x, y) in enumerate(((x1 - 0.1, y0 + 0.1), (x1 - 0.1, y1 - 0.1))):
        member(pc, 'oak', (x, y, 0.0), (x, y, zl - 0.05), 0.18, 0.18, name='lp%d' % i, up=(1, 0, 0))
    member(pc, 'oak', (x1 - 0.1, y0 - 0.1, zl), (x1 - 0.1, y1 + 0.1, zl), 0.18, 0.18, name='lplate', up=(1, 0, 0), sag=0.03)
    # back and outer walls in vertical boards (the outer one half-height, so the pile shows)
    R = rng('leanto')
    nb = int((y1 - y0) / 0.2)
    for i in range(nb):
        y = y0 + 0.1 + i * (y1 - y0 - 0.2) / max(1, nb - 1)
        if i in (0, nb - 1) or R.random() < 0.85:
            hh = 1.05 + R.uniform(-0.03, 0.03)
            add_box(pc, 'oak', (0.035, 0.18, hh), Matrix.Translation((x1 - 0.02, y, hh / 2 + 0.04)) @
                    Matrix.Rotation(math.radians(R.uniform(-1.5, 1.5)), 4, 'X'), cham=0.008, long_axis=2,
                    wob=0.004, seed=R.random() * 60)
    # mono-pitch roof: deck plus tile courses
    def rs(y, s):
        return Vector((x0 + (x1 + 0.35 - x0) * s, y, zh + (zl - 0.12 - zh) * s))
    ny = int((y1 - y0 + 0.5) / 0.42)
    for c in range(4):
        s0, s1 = c / 4, (c + 1) / 4 + 0.05
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        nrm = Vector((zh - zl, 0, x1 - x0)).normalized()
        for i in range(ny):
            ya = y0 - 0.25 + (y1 - y0 + 0.5) * i / ny; yb = y0 - 0.25 + (y1 - y0 + 0.5) * (i + 1) / ny
            j = R.uniform(-0.01, 0.01)
            q = [rs(ya, 1 - s0) + nrm * (0.05 + j), rs(yb, 1 - s0) + nrm * (0.05 + j), rs(yb, max(0, 1 - s1)), rs(ya, max(0, 1 - s1))]
            v = [bm.verts.new(p) for p in q]
            f = bm.faces.new((v[1], v[0], v[3], v[2]))
            for l in f.loops:
                l[uvl].uv = (l.vert.co.y / TILE, (l.vert.co.x - x0) / TILE)
            u = [bm.verts.new(p - nrm * 0.05) for p in q[:2]]
            f = bm.faces.new((v[0], v[1], u[1], u[0]))
            for l in f.loops:
                l[uvl].uv = (l.vert.co.y / TILE, l.vert.co.z / TILE)
        bm.normal_update(); _merge(pc, bm, 'roof')
    quad_grid(pc, 'oak', [rs(y1 + 0.25, 0) , rs(y0 - 0.25, 0), rs(y0 - 0.25, 1), rs(y1 + 0.25, 1)], ny, 3,
              uv_fn=lambda c: (c.y / TILE, c.x / TILE))
    # woodpile: split logs, end grain toward the plaza
    for row in range(6):
        n = 9 - (row % 2)
        for i in range(n):
            r = R.uniform(0.075, 0.1)
            x = x0 + 0.15 + (i + 0.5 * (row % 2)) * 0.165 + R.uniform(-0.01, 0.01)
            if x > x1 - 0.25:
                continue
            z = 0.1 + row * 0.155
            bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
            bmesh.ops.create_cone(bm, cap_ends=True, segments=7, radius1=r, radius2=r * 0.95, depth=2.2)
            bm.transform(Matrix.Translation((x, (y0 + y1) / 2 + R.uniform(-0.06, 0.06), z)) @
                         Matrix.Rotation(math.pi / 2, 4, 'X') @ Matrix.Rotation(R.random() * 6.3, 4, 'Z'))
            for f in bm.faces:
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.x / TILE * 3, l.vert.co.y / TILE)
            bm.normal_update(); _merge(pc, bm, 'oak_lt')
    return pc

def kit_chimney():
    pc = Piece('Kit_Chimney')
    cx, cy = -2.05, 2.15
    z_base = roof_surface(cx, 0).z + 1.5
    top = CHIM_TOP
    lean = Matrix.Rotation(math.radians(1.3), 4, 'Y') @ Matrix.Rotation(math.radians(-0.8), 4, 'X')
    base = Matrix.Translation((cx, cy, z_base))
    h = top - z_base
    def taper(p, h=h):
        t = (p.z + h / 2) / h
        return Vector((p.x * -0.06 * t, p.y * -0.06 * t, 0))
    add_box(pc, 'stone_dk', (1.05, 0.85, h), base @ lean @ Matrix.Translation((0, 0, h / 2)), cham=0.035,
            cuts=(2, 2, int(h / 0.4)), bend=taper, wob=0.01, seed=21.0, uv_world=True)
    # corbelled cap: two oversailing courses
    add_box(pc, 'stone_dk', (1.18, 0.98, 0.14), base @ lean @ Matrix.Translation((0, 0, h - 0.02)), cham=0.03, seed=22.0, uv_world=True)
    add_box(pc, 'stone_dk', (1.08, 0.88, 0.12), base @ lean @ Matrix.Translation((0, 0, h + 0.11)), cham=0.03, seed=23.0, uv_world=True)
    # two clay pots (lathe), the second a little shorter and tipped
    for k, (px, ph, tip) in enumerate(((-0.22, 0.52, 0.0), (0.24, 0.42, 4.0))):
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        prof = [(0.15, 0.0), (0.16, 0.05), (0.13, 0.12), (0.11, ph * 0.7), (0.12, ph - 0.06), (0.145, ph - 0.03),
                (0.145, ph), (0.10, ph)]
        segs = 14; rings = []
        for (r, z) in prof:
            rings.append([bm.verts.new(Vector((r * math.cos(2 * math.pi * s / segs), r * math.sin(2 * math.pi * s / segs), z)))
                          for s in range(segs)])
        for j in range(len(rings) - 1):
            for s in range(segs):
                f = bm.faces.new((rings[j][s], rings[j][(s + 1) % segs], rings[j + 1][(s + 1) % segs], rings[j + 1][s]))
                for l in f.loops:
                    co = l.vert.co
                    l[uvl].uv = (math.atan2(co.y, co.x) * 0.15 / TILE, co.z / TILE)
        bm.normal_update()
        bm.transform(base @ lean @ Matrix.Translation((px, 0, h + 0.16)) @ Matrix.Rotation(math.radians(tip), 4, 'X'))
        _merge(pc, bm, 'roof')
    return pc

def kit_decals():
    pc_list = []
    def decal(stem, center, size, normal, rot=0.0, name=None):
        pc = Piece('Kit_Decal_%s_%d' % (stem, len(pc_list)))
        n = Vector(normal).normalized()
        up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((0, 1, 0))
        side = up.cross(n).normalized(); up = n.cross(side).normalized()
        c = Vector(center) + n * 0.004
        ca, sa = math.cos(rot), math.sin(rot)
        sx, sz = side * (size[0] / 2), up * (size[1] / 2)
        sx, sz = sx * ca + sz * sa, -sx * sa + sz * ca
        corners = [c - sx - sz, c + sx - sz, c + sx + sz, c - sx + sz]
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        vs = [bm.verts.new(p) for p in corners]
        f = bm.faces.new(vs)
        for l, uv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
            l[uvl].uv = uv
        bm.normal_update()
        si = 0; pc.mats.append(decal_mat(stem))
        me = bpy.data.meshes.new(pc.name); bm.to_mesh(me); bm.free()
        pc_list.append((pc, me))
    # drips under each window sill
    decal('drip', ((GWIN[0] + GWIN[1]) / 2, -0.003, GWIN[2] - 0.62), (1.1, 1.0), (0, -1, 0))
    for a, b in UWIN:
        decal('drip', ((a + b) / 2, YU - 0.003, UWIN_Z[0] - 0.42), (0.95, 0.65), (0, -1, 0))
    # drip under the wall plate, crack in two panels
    decal('crack', (-2.95, -0.012, 1.55), (0.7, 0.7), (0, -1, 0), rot=0.3)
    decal('crack', (-0.75, YU - 0.012, 4.6), (0.6, 0.6), (0, -1, 0), rot=-0.4)
    # moss on the plinth top and the step corner
    decal('moss', (-3.2, -0.06, PLINTH + 0.002), (0.7, 0.35), (0, 0, 1))
    decal('moss', (3.4, 0.1, PLINTH + 0.002), (0.5, 0.3), (0, 0, 1))
    return pc_list

# ----------------------------------------------------------------------------- wear (vertex colour)
LEAN_X, LEAN_Y = 0.011, 0.007   # whole frame leans 1.1 cm/m toward +X; the upper storey tips 0.7 cm/m outward

def lean(me):
    """Medium whimsy (brief 0): the house has settled, so nothing above the plinth stands quite plumb."""
    for v in me.vertices:
        z = v.co.z
        v.co.x += LEAN_X * max(0.0, z - PLINTH)
        v.co.y -= LEAN_Y * max(0.0, z - BRES0)

def lean_pt(p):
    return Vector((p.x + LEAN_X * max(0.0, p.z - PLINTH), p.y - LEAN_Y * max(0.0, p.z - BRES0), p.z))

def write_wear(ob, chimney_top=None):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')   # linear 0-1 masks, not sRGB
    mw = ob.matrix_world
    door_h = Vector(((DOOR[1] - 0.17), 0.05, 1.10))
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 2.1)
            # splashback band at the foot: strongest at the cobble line, ragged top
            streak = noise.noise(Vector((p.x * 5.0, p.y * 5.0, p.z * 0.7)))
            r = max(0.0, 1 - p.z / (1.45 + 0.3 * nz + 0.25 * streak)) ** 1.0 * 0.85
            # shade-damp band under the deep eave on the upper storey
            if p.z > PLATE0 - 0.75 and p.z < PLATE1 + 0.05 and p.y < YU + 0.15:
                r = max(r, (p.z - (PLATE0 - 0.75)) / 0.75 * 0.38)
            # sheltered dust and soot under the jetty: ground-storey front, girt, joists and soffit
            if 2.2 < p.z < JOIST1 + 0.02 and p.y > YU - 0.12 and p.x < W - 0.06:
                r = max(r, min(0.6, (p.z - 2.2) / 0.85 * 0.6) * (0.85 + 0.15 * streak))
            # broad lime-wash mottling (every surface), so no mass reads showroom-clean
            r = max(r, 0.22 * max(0.0, noise.noise(p * 0.45 + Vector((3.1, 0.0, 1.7)))) +
                    0.06 * max(0.0, noise.noise(p * 2.3)))
            # run-off stains on plaster just below the sill rails and the girt, streaked downward
            if ob.name == 'Kit_Panels':
                # edge grime: plaster darkens in a soft band where it meets a timber
                dmin = 9.0
                for (ta, tb, hw) in TIMBERS_L:
                    ab = tb - ta; L2 = ab.length_squared
                    t = 0.0 if L2 < 1e-9 else max(0.0, min(1.0, (p - ta).dot(ab) / L2))
                    d = (p - (ta + ab * t)).length - hw
                    if d < dmin:
                        dmin = d
                if dmin < 0.16:
                    r = max(r, (1 - max(0.0, dmin) / 0.16) ** 1.5 * (0.30 + 0.12 * nz))
                for zr_ in RUNOFF_Z:
                    dz = zr_ - p.z
                    if 0.0 < dz < 0.55:
                        r = max(r, (1 - dz / 0.55) ** 1.6 * (0.28 + 0.22 * streak))
            # damp darkening on the plinth, streaked downward
            if p.z < PLINTH + 0.05:
                r = max(r, 0.6 + 0.2 * streak)
            # hand grime round the latch and on the door post edges
            dh = (p - door_h).length
            r = max(r, max(0.0, 1 - dh / 0.35) * 0.4)
            # soot round the chimney top and above the pots
            if chimney_top is not None and p.z > chimney_top - 1.0 and abs(p.x - chimney_top_x) < 0.8 and abs(p.y - 2.15) < 0.7:
                r = max(r, min(1.0, (p.z - (chimney_top - 1.3)) / 1.1) ** 0.9 * 0.85)
            # roof: the lowest courses and the eave get grime streaks from the run-off
            g = 0.0
            if ob.name == 'Kit_RoofModule' and n.z > 0.3:
                s = (p.z - 5.6) / (RIDGE - 5.6)
                g = max(0.0, 1 - s * 2.2) * 0.9 + max(0.0, 0.1 + 0.5 * nz) * 0.3 + max(0.0, s - 0.88) * 5.0
                tv = ((poly.index * 2654435761) % 1009) / 1009.0          # tile-to-tile colour
                r = max(r, 0.32 * tv * tv)
                r = max(r, max(0.0, 0.5 - s) * 0.25 + 0.3 * max(0.0, noise.noise(Vector((p.x * 1.4, p.y * 0.8, p.z * 2.2)))))
            if ob.name in ('Kit_Plinth',) and n.z > 0.5:
                g = 0.7
            if ob.name == 'Kit_Chimney' and n.z > 0.6:
                g = 0.6
            # polish: the hand-worn door edge near the ring, the dished step centre
            b = 0.0
            if ob.name == 'Kit_Door':
                b = max(0.0, 1 - (Vector((p.x - door_h.x, 0, (p.z - door_h.z) * 0.6))).length / 0.3) * 0.6
            if ob.name == 'Kit_Plinth' and n.z > 0.5 and p.z < DOOR[2] + 0.01 and DOOR[0] < p.x < DOOR[1]:
                b = max(0.0, 1 - abs(p.x - (DOOR[0] + DOOR[1]) / 2) / 0.5) * 0.6
            # fresh lime patch repairs: a few lighter, ragged-edged patches on the plaster
            if ob.name == 'Kit_Panels':
                pn = noise.noise(p * 0.9 + Vector((11.0, 4.0, 2.0))) + 0.25 * noise.noise(p * 6.0)
                if pn > 0.34:
                    b = max(b, min(0.55, (pn - 0.34) * 5.0))
                    r = r * 0.4
            # chipped paint: dark bare wood showing along the shutter and door boards
            if ob.name in ('Kit_Shutters', 'Kit_Door'):
                ch = noise.noise(p * 9.0 + Vector((0.0, 7.0, 0.0)))
                r = max(r, min(0.6, max(0.0, ch - 0.22) * 3.0))
            # sun-bleached tops of the exposed timbers
            if ob.name in ('Kit_Posts', 'Kit_Rails', 'Kit_Braces', 'Kit_JettyBeams') and n.z > 0.55:
                b = max(b, 0.35)
            attr.data[li].color = (min(1, r), min(1, g), min(1, b), 1.0)

chimney_top_x = -2.05
RUNOFF_Z = [GWIN[2] - 0.18, UWIN_Z[0] - 0.16, BRES1 + 0.0, PLINTH + 0.36]

# ----------------------------------------------------------------------------- assembly
def build():
    global TIMBERS_L
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root = bpy.data.objects.new('FacadeKit', None); bpy.context.scene.collection.objects.link(root)
    pieces = [kit_plinth(), kit_posts()[0], kit_rails(), kit_braces(), kit_jetty(), kit_panels(), kit_windows(),
              kit_shutters(), kit_door(), kit_roof(), kit_chimney(), kit_flowerbox(), kit_leanto()]
    obs = []
    total = 0
    TIMBERS_L = [(lean_pt(a), lean_pt(b), hw) for (a, b, hw) in TIMBERS]
    for pc in pieces:
        me = bpy.data.meshes.new(pc.name)
        pc_smooth = pc.name == 'Kit_Panels'
        if pc_smooth:   # weld the plaster bands and sheets so no seam runs through a wall
            bmesh.ops.remove_doubles(pc.bm, verts=pc.bm.verts[:], dist=0.002)
        pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
        for m in pc.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
        ob.parent = root
        lean(me)
        write_wear(ob, chimney_top=CHIM_TOP)
        for p in me.polygons:
            p.use_smooth = pc_smooth   # plaster smooth: flat cells showed as dotted seams in Unity
        if pc_smooth:   # keep wall corners crisp
            me.set_sharp_from_angle(angle=math.radians(30))
        t = sum(len(p.vertices) - 2 for p in me.polygons); total += t
        print('  %-18s %6d tris  %s' % (pc.name, t, ', '.join(m.name for m in pc.mats)))
        obs.append(ob)
    for pc, me in kit_decals():
        lean(me)
        for m in pc.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob); ob.parent = root
        total += len(me.polygons) * 2
        obs.append(ob)
    print('FacadeKit total tris: %d (building budget 75000)' % total)
    return root, obs

# ----------------------------------------------------------------------------- review scene
def U(x, y, z):
    """Unity corner coordinates -> Blender review-scene coordinates."""
    return Vector((x, z, y))

def look(cam, frm, to):
    d = (to - frm)
    cam.location = frm
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()

def zero_wear(o):
    a = o.data.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    for d in a.data:
        d.color = (0, 0, 0, 1)

def review_scene(root):
    sc = bpy.context.scene
    root.location = U(-1.25, 0, 7.5)
    # ground: cobble tileable, fountain + stall proxies, a 1.75 m figure by the door
    gm = tile_mat('FK_Cobble', 'B1_town/cobble')
    bpy.ops.mesh.primitive_plane_add(size=1); g = bpy.context.object; g.name = 'Ground'
    g.scale = (40, 40, 1); g.location = (0, 6, 0)
    bpy.ops.object.transform_apply(scale=True)
    g.data.materials.append(gm)
    for l in g.data.uv_layers[0].data:
        pass
    uvl = g.data.uv_layers[0]
    for poly in g.data.polygons:
        for li in poly.loop_indices:
            co = g.data.vertices[g.data.loops[li].vertex_index].co
            uvl.data[li].uv = (co.x / TILE, co.y / TILE)
    zero_wear(g)
    st = tile_mat('FK_Sandstone', 'B1_town/sandstone')
    bpy.ops.mesh.primitive_cylinder_add(radius=1.7, depth=0.55, vertices=48, location=(0, 0, 0.275))
    f = bpy.context.object; f.name = 'FountainProxy'; f.data.materials.append(st)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.25, depth=2.4, vertices=24, location=(0, 0, 1.2))
    f2 = bpy.context.object; f2.data.materials.append(st)
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(4.5, 1.35, 4.5)); s = bpy.context.object
    s.scale = (2.6, 1.6, 2.7); s.rotation_euler = (0, 0, math.radians(-200))
    s.data.materials.append(tile_mat('FK_StallProxy', 'B1_town/canvas'))
    for o in (f, f2, s):
        bpy.context.view_layer.objects.active = o; o.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.uv.cube_project(cube_size=TILE); bpy.ops.object.mode_set(mode='OBJECT')
        zero_wear(o)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=U(-0.4, 0.875, 6.4))
    fig = bpy.context.object; fig.name = 'Figure175'
    fm = bpy.data.materials.new('Figure'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.18, 0.12, 1)
    fig.data.materials.append(fm)
    # light: warm midday sun (about 5200 K) + Nishita sky
    sun = bpy.data.lights.new('Sun', 'SUN'); sun.energy = 4.2; sun.color = (1.0, 0.90, 0.80); sun.angle = math.radians(1.2)
    so = bpy.data.objects.new('Sun', sun); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(38), 0, math.radians(30))
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    sky = w.node_tree.nodes.new('ShaderNodeTexSky'); sky.sky_type = 'NISHITA'
    sky.sun_disc = False; sky.sun_elevation = math.radians(52); sky.air_density = 1.4; sky.dust_density = 2.0
    bg = w.node_tree.nodes['Background']; bg.inputs['Strength'].default_value = 0.18
    w.node_tree.links.new(sky.outputs[0], bg.inputs[0])
    # haze-lite: a world volume would be costly; the corner's haze lives in Unity
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 4
    sc.render.resolution_x = RRES; sc.render.resolution_y = int(RRES * 9 / 16)
    try:
        sc.view_settings.view_transform = 'Khronos PBR Neutral'
    except TypeError:
        sc.view_settings.view_transform = 'AgX'
    sc.view_settings.exposure = 0.0
    cam_d = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cam_d); sc.collection.objects.link(cam)
    sc.camera = cam
    cam_d.sensor_fit = 'VERTICAL'
    return cam

VIEWS_DEF = {
    'wide': (U(1, 1.8, -9), U(-1, 3, 6), 46),
    'mid': (U(5, 1.7, -2.5), U(-1.5, 1.8, 5), 42),
    'close': (U(1.6, 1.4, -1.6), U(0, 1.0, 0.6), 38),
    'tq': (U(6.5, 3.0, 1.5), U(-1.6, 4.6, 7.8), 50),
    'door': (U(1.4, 1.6, 3.6), U(-1.2, 1.6, 7.5), 40),
}

def render(cam):
    for v in VIEWS:
        frm, to, fov = VIEWS_DEF[v]
        look(cam, frm, to); cam.data.angle = math.radians(fov)
        bpy.context.scene.render.filepath = os.path.join(OUT, 'facade_%s.png' % v)
        bpy.ops.render.render(write_still=True)
        print('rendered', v)

root, obs = build()
if '--export' in argv:
    for o in bpy.context.scene.objects:
        o.select_set(o.name.startswith('Kit_') or o == root)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'FacadeKit.fbx'), use_selection=True, axis_forward='-Z',
                             axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
                             mesh_smooth_type='FACE', add_leaf_bones=False)
if '--no-render' not in argv:
    cam = review_scene(root)
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'facade.blend'))
    render(cam)
